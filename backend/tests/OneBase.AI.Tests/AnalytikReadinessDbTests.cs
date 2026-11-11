using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OneBase.Application.Analytik;
using OneBase.Application.Analytik.Readiness;
using OneBase.Application.Security;
using OneBase.Domain.Identity;
using OneBase.Domain.Sales;
using OneBase.Domain.Work;
using OneBase.Infrastructure.Linko;
using OneBase.Infrastructure.Persistence;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>
/// Готовность данных Аналитика на настоящем PostgreSQL: отстающие данные Linko обновляются до ответа (настоящая синхронизация с подставным
/// Linko), повторы — с паузой, утром ежедневный отчёт ждёт вчерашних продаж, а то, что исправить нельзя (Linko не подключён, автообмен выключен), —
/// одно уведомление администраторам интеграций и одно — когда данные снова свежие.
/// </summary>
public sealed class AnalytikReadinessDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly string[] Documents = ["orders", "payments", "product_balances"];

    /// <summary>Подставные подключения: состояние задаёт тест; уведомления — настоящим контролем обмена Linko.</summary>
    private sealed class FakeSources(IServiceProvider services) : IAnalytikSourceSync
    {
        public SourceLink Link { get; set; } = Of(true, true);
        public SyncStartResult Result { get; set; } = SyncStartResult.Started;
        public int Starts { get; private set; }

        public static SourceLink Of(bool connected, bool auto) =>
            new(SourceCodes.Linko, "Linko", connected, auto, false, TimeSpan.FromMinutes(90), "/settings/integrations/sales/linko",
                connected ? null : "Linko не подключён: в «Настройки → Интеграции → Linko» не указан адрес или токен.",
                $"{Permissions.SalesManage}|{Permissions.IntegrationsManage}");

        public Task<IReadOnlyList<SourceLink>> LinksAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<SourceLink>>([Link]);

        public Task<SyncStartResult> StartAsync(string code, CancellationToken ct)
        {
            Starts++;
            return Task.FromResult(Result);
        }

        public Task<bool> AlertAsync(string code, string cause, CancellationToken ct) =>
            services.GetRequiredService<LinkoSyncHealth>().CheckAsync(null, ct, cause);
    }

    private static (DataKeeper Keeper, DataReadiness Readiness, DataKeeperState State) Keeper(IServiceProvider sp, IAnalytikSourceSync sources, TimeProvider clock,
        string dailyAt = "07:30")
    {
        var state = new DataKeeperState();
        var readiness = new DataReadiness(sp.GetRequiredService<OneBaseDbContext>(), sources, state, clock);
        return (new DataKeeper(readiness, sources, state, new AnalytikOptions { DailyAt = dailyAt }, clock, NullLogger<DataKeeper>.Instance), readiness, state);
    }

    private static async Task SetLoadedAsync(OneBaseDbContext db, DateTimeOffset at, IEnumerable<string>? entities = null)
    {
        foreach (var entity in entities ?? Documents)
        {
            var state = await db.LinkoSyncStates.FirstOrDefaultAsync(s => s.Entity == entity);
            if (state is null)
            {
                db.LinkoSyncStates.Add(state = new LinkoSyncState { Entity = entity });
            }

            state.LastSuccessAt = at.ToUniversalTime();
            state.LastError = null;
        }

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    [PostgresFact]
    public async Task Stale_Linko_data_is_loaded_before_the_answer()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        await sp.GetRequiredService<LinkoSyncCoordinator>().RunAsync(LinkoSyncMode.Incremental); // первая загрузка

        // Обмен не проходил 5 часов, а в Linko появился сегодняшний заказ.
        var old = DateTimeOffset.UtcNow.AddHours(-5);
        await SetLoadedAsync(db, old, LinkoSyncSchedule.KeyEntities.Keys.Append("payments"));
        var state = sp.GetRequiredService<DataKeeperState>();
        state.Clear(SourceCodes.Linko);
        fixture.Linko.Orders.Add(FakeLinko.Order(DataManagerDb.FakeLinkoIds + 301, 5, "Хоразм", DateTime.Today, 10));

        // Перед ответом Аналитик сам запускает обновление (настоящая синхронизация с подставным Linko), говорит, что обновляет, и дожидается его.
        var told = new List<string>();
        var (report, waited) = await sp.GetRequiredService<DataKeeper>().EnsureFreshAsync(TimeSpan.FromSeconds(60), t =>
        {
            told.Add(t);
            return Task.CompletedTask;
        }, CancellationToken.None);

        Assert.True(waited);
        Assert.Equal(["Linko"], told);
        // Данные уже свежие; синхронизация может ещё пересчитывать отчёты — тогда «обновляется», без причины отставания.
        var sales = report.Datasets.Single(d => d.Code == "sales");
        Assert.Contains(sales.Status, new[] { ReadinessStatus.Ok, ReadinessStatus.Updating });
        Assert.False(sales.Lagging);
        Assert.Null(sales.Reason);
        Assert.True(sales.UpdatedAt > old.AddHours(4));
        Assert.True(await db.LinkoOrders.AnyAsync(o => o.Id == DataManagerDb.FakeLinkoIds + 301));
        Assert.Equal(SyncStartResult.Started, state.Last(SourceCodes.Linko)?.Result);
        fixture.Linko.Orders.Clear();

        // Свежие данные — ничего не запускается и не ждётся.
        var (_, again) = await sp.GetRequiredService<DataKeeper>().EnsureFreshAsync(TimeSpan.FromSeconds(60), null, CancellationToken.None);
        Assert.False(again);
        Assert.Null(state.Last(SourceCodes.Linko));

        // Дождаться конца фоновой синхронизации, чтобы она не задела следующие тесты.
        var coordinator = sp.GetRequiredService<LinkoSyncCoordinator>();
        for (var i = 0; i < 120 && coordinator.IsRunning; i++)
        {
            await Task.Delay(500);
        }
    }

    [PostgresFact]
    public async Task Lagging_source_is_retried_with_growing_pauses_until_fresh()
    {
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var t0 = new DateTimeOffset(2026, 11, 12, 14, 0, 0, TimeSpan.FromHours(5));
        var clock = new TestClock { Now = t0 };
        var sources = new FakeSources(sp) { Result = SyncStartResult.Failed };
        var (keeper, _, state) = Keeper(sp, sources, clock);
        await SetLoadedAsync(db, t0.AddHours(-5));

        await keeper.RunAsync("schedule", CancellationToken.None);
        Assert.Equal(1, sources.Starts);
        await keeper.RunAsync("schedule", CancellationToken.None); // пауза 10 минут
        Assert.Equal(1, sources.Starts);
        clock.Now = t0.AddMinutes(11);
        await keeper.RunAsync("schedule", CancellationToken.None);
        Assert.Equal(2, sources.Starts);
        clock.Now = t0.AddMinutes(25); // после второй неудачи — 20 минут
        await keeper.RunAsync("schedule", CancellationToken.None);
        Assert.Equal(2, sources.Starts);
        clock.Now = t0.AddMinutes(32);
        var report = await keeper.RunAsync("schedule", CancellationToken.None);
        Assert.Equal(3, sources.Starts);
        Assert.Equal(3, state.Last(SourceCodes.Linko)?.Streak);
        Assert.Contains("не смог запустить обновление", report.Datasets.Single(d => d.Code == "sales").Action);

        // Данные снова свежие — счётчик повторов сброшен, запусков нет.
        await SetLoadedAsync(db, t0.AddMinutes(31));
        await keeper.RunAsync("schedule", CancellationToken.None);
        Assert.Equal(3, sources.Starts);
        Assert.Null(state.Last(SourceCodes.Linko));
    }

    [PostgresFact]
    public async Task Daily_report_waits_for_yesterdays_sales_in_the_morning()
    {
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var morning = new DateTimeOffset(2026, 11, 12, 7, 35, 0, TimeSpan.FromHours(5));
        var clock = new TestClock { Now = morning };
        var sources = new FakeSources(sp);
        var (keeper, _, _) = Keeper(sp, sources, clock);

        // Обмен по расписанию в 11:00 и 17:00: последний — вчера в 17:00, это ещё «свежо» по расписанию, но вечерних продаж в нём нет.
        sources.Link = FakeSources.Of(true, true) with { StaleAfter = TimeSpan.FromHours(19) };
        await SetLoadedAsync(db, new DateTimeOffset(2026, 11, 11, 17, 0, 0, TimeSpan.FromHours(5)));
        Assert.True(await keeper.HoldDailyAsync(CancellationToken.None));
        Assert.Equal(1, sources.Starts); // утренняя догрузка запущена

        // Обмен прошёл после полуночи — отчёт можно строить.
        await SetLoadedAsync(db, morning.AddMinutes(1));
        Assert.False(await keeper.HoldDailyAsync(CancellationToken.None));

        // Ждать дольше двух часов после времени отчёта или того, что само не загрузится, — не нужно.
        await SetLoadedAsync(db, new DateTimeOffset(2026, 11, 11, 17, 0, 0, TimeSpan.FromHours(5)));
        clock.Now = new DateTimeOffset(2026, 11, 12, 9, 31, 0, TimeSpan.FromHours(5));
        Assert.False(await keeper.HoldDailyAsync(CancellationToken.None));
        clock.Now = morning;
        sources.Link = FakeSources.Of(false, true);
        Assert.False(await keeper.HoldDailyAsync(CancellationToken.None));
        await db.LinkoSyncStates.Where(s => s.Entity == LinkoSyncSchedule.AlertState).ExecuteDeleteAsync();
    }

    [PostgresFact]
    public async Task Unfixable_lag_notifies_integration_admins_once_and_again_on_recovery()
    {
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var role = new Role { Name = $"Интеграции {Guid.NewGuid():N}"[..20] };
        role.Permissions.Add(new RolePermission { Code = Permissions.IntegrationsManage });
        var admin = new User { Login = $"rdy-{Guid.NewGuid():N}"[..20], FullName = "Администратор интеграций" };
        admin.Roles.Add(new UserRole { Role = role });
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        await db.LinkoSyncStates.Where(s => s.Entity == LinkoSyncSchedule.AlertState).ExecuteDeleteAsync();
        await SetLoadedAsync(db, DateTimeOffset.UtcNow.AddDays(-13), LinkoSyncSchedule.KeyEntities.Keys.Append("payments"));
        Task<List<UserNotification>> Notes() => db.UserNotifications.AsNoTracking().Where(n => n.RecipientId == admin.Id).OrderBy(n => n.CreatedAt).ToListAsync();

        // Linko не подключён: Аналитик ничего не запускает, а один раз говорит администраторам, почему данные устарели и что будет дальше.
        var sources = new FakeSources(sp) { Link = FakeSources.Of(false, true) };
        var (keeper, readiness, _) = Keeper(sp, sources, TimeProvider.System);
        await keeper.RunAsync("schedule", CancellationToken.None);
        await keeper.RunAsync("schedule", CancellationToken.None);
        Assert.Equal(0, sources.Starts);
        var problem = Assert.Single(await Notes());
        Assert.Equal(UserNotificationKind.DataSyncProblem, problem.Kind);
        Assert.Contains("Причина: Linko не подключён", problem.Body);
        Assert.Contains("после восстановления обмена данные догрузятся", problem.Body);
        Assert.DoesNotContain("Следующая синхронизация по расписанию", problem.Body);
        Assert.Equal("/settings/integrations/sales/linko", problem.Link);

        // Это видно в готовности данных: причина и что сделано.
        var sales = (await readiness.CheckAsync(CancellationToken.None)).Datasets.Single(d => d.Code == "sales");
        Assert.Equal(ReadinessStatus.NotConnected, sales.Status);
        Assert.Contains("Администраторы интеграций уведомлены", sales.Action);
        Assert.Contains("после подключения данные догрузятся автоматически", sales.Action);

        // Подключили, но автообмен выключен; данные обновили кнопкой — сообщение о восстановлении, дальше тишина. Сам Аналитик обмен не запускал.
        sources.Link = FakeSources.Of(true, false);
        await SetLoadedAsync(db, DateTimeOffset.UtcNow, LinkoSyncSchedule.KeyEntities.Keys.Append("payments"));
        await keeper.RunAsync("schedule", CancellationToken.None);
        await keeper.RunAsync("schedule", CancellationToken.None);
        var notes = await Notes();
        Assert.Equal(2, notes.Count);
        Assert.Contains("восстановлен", notes[^1].Title);
        Assert.Equal(0, sources.Starts);
        await db.LinkoSyncStates.Where(s => s.Entity == LinkoSyncSchedule.AlertState).ExecuteDeleteAsync();
    }
}
