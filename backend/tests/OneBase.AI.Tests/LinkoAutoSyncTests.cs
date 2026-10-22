using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Sales;
using OneBase.Application.Security;
using OneBase.Domain.Identity;
using OneBase.Domain.Sales;
using OneBase.Domain.Work;
using OneBase.Infrastructure.Linko;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// Обмен с Linko без людей: изменение давнего месяца сбрасывает и кэш давних месяцев (а свежее — только свежих), контроль обмена
/// уведомляет администраторов интеграций один раз, когда данные устарели, и ещё раз — когда обмен восстановился.
/// </summary>
public class LinkoAutoSyncTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    [PostgresFact]
    public async Task Change_in_an_old_month_resets_the_history_cache_and_a_fresh_change_only_the_recent_one()
    {
        await fixture.ResetAsync();
        var signal = fixture.Services.GetRequiredService<SalesCacheSignal>();
        var coordinator = fixture.Services.GetRequiredService<LinkoSyncCoordinator>();
        await coordinator.RunAsync(LinkoSyncMode.Incremental); // первая загрузка может быть полной — дальше обычные обновления

        // Вчерашний заказ — сбрасывается только кэш свежих расчётов.
        fixture.Linko.Orders.Add(FakeLinko.Order(DataManagerDb.FakeLinkoIds + 91, 5, "Хоразм", DateTime.Today.AddDays(-1), 10));
        var recent = signal.Token;
        var history = signal.HistoryToken;
        Assert.NotNull(await coordinator.RunAsync(LinkoSyncMode.Incremental));
        Assert.True(recent.HasChanged);
        Assert.False(history.HasChanged);

        // Заказ трёхмесячной давности (поздняя приёмка, смена статуса) — и кэш давних месяцев, без ожидания полной загрузки.
        fixture.Linko.Orders.Clear();
        fixture.Linko.Orders.Add(FakeLinko.Order(DataManagerDb.FakeLinkoIds + 92, 5, "Хоразм", DateTime.Today.AddMonths(-3), 10));
        history = signal.HistoryToken;
        Assert.NotNull(await coordinator.RunAsync(LinkoSyncMode.Incremental));
        Assert.True(history.HasChanged);
        fixture.Linko.Orders.Clear();
    }

    [PostgresFact]
    public async Task Stale_Linko_data_notifies_integration_admins_once_and_again_when_the_exchange_recovers()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var health = sp.GetRequiredService<LinkoSyncHealth>();

        var role = new Role { Name = $"Интеграции {Guid.NewGuid():N}"[..20] };
        role.Permissions.Add(new RolePermission { Code = Permissions.IntegrationsManage });
        var admin = new User { Login = $"int-{Guid.NewGuid():N}"[..20], FullName = "Администратор интеграций" };
        admin.Roles.Add(new UserRole { Role = role });
        db.Users.Add(admin);
        var now = DateTimeOffset.UtcNow;
        await db.LinkoSyncStates.Where(s => s.Entity == LinkoSyncSchedule.AlertState).ExecuteDeleteAsync();
        foreach (var entity in LinkoSyncSchedule.KeyEntities.Keys)
        {
            var state = await db.LinkoSyncStates.FirstOrDefaultAsync(s => s.Entity == entity);
            if (state is null)
            {
                db.LinkoSyncStates.Add(state = new LinkoSyncState { Entity = entity });
            }

            state.LastSuccessAt = now.AddHours(-5);
            state.LastError = entity == "orders" ? "Linko ответил 401: токен недействителен" : null;
        }

        await db.SaveChangesAsync();
        Task<List<UserNotification>> Notes() => db.UserNotifications.AsNoTracking().Where(n => n.RecipientId == admin.Id).OrderBy(n => n.CreatedAt).ToListAsync();

        // Без запуска пять часов — ещё не повод (между запусками по расписанию до 18 часов).
        Assert.False(await health.CheckAsync());
        Assert.Empty(await Notes());

        // Запуск по расписанию 10 минут назад данные не обновил — одно уведомление с причиной, следующим запуском и ссылкой на настройки;
        // повторная проверка не дублирует.
        var run = DateTimeOffset.UtcNow.AddMinutes(-10);
        Assert.True(await health.CheckAsync(run));
        var problem = Assert.Single(await Notes());
        Assert.Equal(UserNotificationKind.DataSyncProblem, problem.Kind);
        Assert.Contains("не работает", problem.Title);
        Assert.Contains("заказы", problem.Body);
        Assert.Contains("401", problem.Body);
        Assert.Contains("Следующая синхронизация по расписанию", problem.Body);
        Assert.Equal("/settings/integrations/sales/linko", problem.Link);
        Assert.False(await health.CheckAsync(run));
        Assert.Single(await Notes());

        // Обмен восстановился — второе уведомление, дальше тишина.
        await db.LinkoSyncStates.Where(s => LinkoSyncSchedule.KeyEntities.Keys.Contains(s.Entity))
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.LastSuccessAt, DateTimeOffset.UtcNow).SetProperty(s => s.LastError, (string?)null));
        db.ChangeTracker.Clear();
        Assert.True(await health.CheckAsync(run));
        var notes = await Notes();
        Assert.Equal(2, notes.Count);
        Assert.Contains("восстановлен", notes[^1].Title);
        Assert.False(await health.CheckAsync(run));
        await db.LinkoSyncStates.Where(s => s.Entity == LinkoSyncSchedule.AlertState).ExecuteDeleteAsync();
    }
}

/// <summary>Расписание обмена с Linko без базы: запуски в 11:00 и 17:00, пропущенный запуск, сверка, давние месяцы, текст проблемы.</summary>
public class LinkoSyncScheduleTests
{
    private static DateTimeOffset Tashkent(int year, int month, int day, int hour, int minute = 0) => new(year, month, day, hour, minute, 0, TimeSpan.FromHours(5));

    private static readonly IReadOnlyList<TimeOnly> Times = LinkoSyncSchedule.ParseTimes(["17:00", "11:00", " 11:00 ", "9:5", "", "не время"]);

    [Fact]
    public void Times_are_parsed_ordered_and_deduplicated()
    {
        Assert.Equal([new TimeOnly(11, 0), new TimeOnly(17, 0)], Times);
        Assert.Equal([new TimeOnly(9, 30)], LinkoSyncSchedule.ParseTimes(["9:30"]));
        Assert.Empty(LinkoSyncSchedule.ParseTimes(null));
    }

    [Fact]
    public void Next_run_is_the_next_scheduled_time_in_Tashkent()
    {
        Assert.Equal(Tashkent(2026, 10, 22, 11), LinkoSyncSchedule.NextRun(Tashkent(2026, 10, 22, 10), Times));
        Assert.Equal(Tashkent(2026, 10, 22, 17), LinkoSyncSchedule.NextRun(Tashkent(2026, 10, 22, 11), Times)); // ровно в 11 — уже следующий
        Assert.Equal(Tashkent(2026, 10, 22, 17), LinkoSyncSchedule.NextRun(Tashkent(2026, 10, 22, 12, 30), Times));
        Assert.Equal(Tashkent(2026, 10, 23, 11), LinkoSyncSchedule.NextRun(Tashkent(2026, 10, 22, 18), Times));
        // Сервер в UTC: 20:00 UTC 22.10 — это 01:00 23.10 в Ташкенте, следующий запуск — 23.10 в 11:00 (06:00 UTC).
        var next = LinkoSyncSchedule.NextRun(new DateTimeOffset(2026, 10, 22, 20, 0, 0, TimeSpan.Zero), Times);
        Assert.Equal(new DateTimeOffset(2026, 10, 23, 6, 0, 0, TimeSpan.Zero), next);
        Assert.Throws<InvalidOperationException>(() => LinkoSyncSchedule.NextRun(Tashkent(2026, 10, 22, 10), []));
    }

    [Fact]
    public void Missed_run_is_caught_up_after_a_restart()
    {
        Assert.Equal(Tashkent(2026, 10, 21, 17), LinkoSyncSchedule.LastSlot(Tashkent(2026, 10, 22, 9), Times));
        Assert.Equal(Tashkent(2026, 10, 22, 11), LinkoSyncSchedule.LastSlot(Tashkent(2026, 10, 22, 11), Times));

        // Сервер не работал в 11:00, данные — со вчерашних 17:00: догнать сразу.
        Assert.True(LinkoSyncSchedule.Missed(Tashkent(2026, 10, 22, 12), Tashkent(2026, 10, 21, 17, 5), Times));
        // В 11:00 прошло, или кнопкой «Обновить» после 11:00 — ждать 17:00.
        Assert.False(LinkoSyncSchedule.Missed(Tashkent(2026, 10, 22, 12), Tashkent(2026, 10, 22, 11, 3), Times));
        // Ночью после вечернего запуска — ждать утра.
        Assert.False(LinkoSyncSchedule.Missed(Tashkent(2026, 10, 23, 2), Tashkent(2026, 10, 22, 17, 4), Times));
        Assert.True(LinkoSyncSchedule.Missed(Tashkent(2026, 10, 22, 12), null, Times)); // ни разу не загружалось
        Assert.False(LinkoSyncSchedule.Missed(Tashkent(2026, 10, 22, 12), null, [])); // расписания нет — только кнопкой
    }

    [Fact]
    public void Longest_gap_wraps_around_midnight()
    {
        Assert.Equal(TimeSpan.FromHours(18), LinkoSyncSchedule.LongestGap(Times)); // 17:00 → 11:00
        Assert.Equal(TimeSpan.FromHours(24), LinkoSyncSchedule.LongestGap([new TimeOnly(11, 0)]));
    }

    [Fact]
    public void Reconcile_runs_once_a_day_after_the_hour_in_Tashkent()
    {
        // При запусках в 11:00 и 17:00 и часе 3 сверка — утренний запуск, вечерний — обычное обновление.
        Assert.True(LinkoSyncSchedule.ReconcileDue(Tashkent(2026, 10, 22, 11), Tashkent(2026, 10, 21, 11), 3));
        Assert.False(LinkoSyncSchedule.ReconcileDue(Tashkent(2026, 10, 22, 17), Tashkent(2026, 10, 22, 11), 3));
        Assert.False(LinkoSyncSchedule.ReconcileDue(Tashkent(2026, 10, 21, 2), null, 3));          // до трёх ночи — нет
        Assert.True(LinkoSyncSchedule.ReconcileDue(Tashkent(2026, 10, 21, 3), null, 3));           // ни разу не было — сразу
        Assert.True(LinkoSyncSchedule.ReconcileDue(Tashkent(2026, 10, 21, 4), Tashkent(2026, 10, 20, 3), 3));
        Assert.False(LinkoSyncSchedule.ReconcileDue(Tashkent(2026, 10, 21, 23), Tashkent(2026, 10, 21, 3), 3)); // сегодня уже была
        // Сервер в UTC: 22:30 UTC 20.10 — это 03:30 21.10 в Ташкенте.
        Assert.True(LinkoSyncSchedule.ReconcileDue(new DateTimeOffset(2026, 10, 20, 22, 30, 0, TimeSpan.Zero), Tashkent(2026, 10, 20, 3), 3));
    }

    [Fact]
    public void Only_changes_older_than_the_previous_month_touch_the_history_cache()
    {
        var now = Tashkent(2026, 10, 21, 12);
        Assert.False(LinkoSyncSchedule.TouchesHistory(null, now));
        Assert.False(LinkoSyncSchedule.TouchesHistory(new DateOnly(2026, 10, 20), now));
        Assert.False(LinkoSyncSchedule.TouchesHistory(new DateOnly(2026, 9, 1), now));   // прошлый месяц считается из строк продаж
        Assert.True(LinkoSyncSchedule.TouchesHistory(new DateOnly(2026, 8, 31), now));
    }

    [Fact]
    public void Problem_names_stale_data_and_the_reason()
    {
        var now = Tashkent(2026, 10, 21, 12);
        var fresh = LinkoSyncSchedule.KeyEntities.Keys.Select(e => new LinkoSyncState { Entity = e, LastSuccessAt = now.AddMinutes(-20) }).ToList();
        Assert.Null(LinkoSyncSchedule.Problem(fresh, now, TimeSpan.FromHours(2)));

        fresh.Single(s => s.Entity == "product_balances").LastSuccessAt = now.AddHours(-3);
        fresh.Single(s => s.Entity == "product_balances").LastError = "нет ответа за 60 с";
        var text = LinkoSyncSchedule.Problem(fresh, now, TimeSpan.FromHours(2))!;
        Assert.Contains("остатки", text);
        Assert.Contains("21.10.2026 09:00", text);
        Assert.Contains("нет ответа за 60 с", text);

        Assert.Contains("ни разу", LinkoSyncSchedule.Problem([], now, TimeSpan.FromHours(2)));
        Assert.Equal("Сменился сервер Linko", LinkoSyncSchedule.Problem([.. fresh, new LinkoSyncState { Entity = "source", LastError = "Сменился сервер Linko" }], now, TimeSpan.FromHours(2)));
    }
}
