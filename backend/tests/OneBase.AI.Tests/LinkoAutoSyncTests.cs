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

        // Данные не обновлялись 5 часов — одно уведомление с причиной и ссылкой на настройки; повторная проверка не дублирует.
        Assert.True(await health.CheckAsync(default));
        var problem = Assert.Single(await Notes());
        Assert.Equal(UserNotificationKind.DataSyncProblem, problem.Kind);
        Assert.Contains("не работает", problem.Title);
        Assert.Contains("заказы", problem.Body);
        Assert.Contains("401", problem.Body);
        Assert.Equal("/settings/integrations/sales/linko", problem.Link);
        Assert.False(await health.CheckAsync(default));
        Assert.Single(await Notes());

        // Обмен восстановился — второе уведомление, дальше тишина.
        await db.LinkoSyncStates.Where(s => LinkoSyncSchedule.KeyEntities.Keys.Contains(s.Entity))
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.LastSuccessAt, DateTimeOffset.UtcNow).SetProperty(s => s.LastError, (string?)null));
        db.ChangeTracker.Clear();
        Assert.True(await health.CheckAsync(default));
        var notes = await Notes();
        Assert.Equal(2, notes.Count);
        Assert.Contains("восстановлен", notes[^1].Title);
        Assert.False(await health.CheckAsync(default));
        await db.LinkoSyncStates.Where(s => s.Entity == LinkoSyncSchedule.AlertState).ExecuteDeleteAsync();
    }
}

/// <summary>Расписание обмена с Linko без базы: ночная сверка, давние месяцы, текст проблемы.</summary>
public class LinkoSyncScheduleTests
{
    private static DateTimeOffset Tashkent(int year, int month, int day, int hour) => new(year, month, day, hour, 0, 0, TimeSpan.FromHours(5));

    [Fact]
    public void Nightly_reconcile_runs_once_a_day_after_the_hour_in_Tashkent()
    {
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
