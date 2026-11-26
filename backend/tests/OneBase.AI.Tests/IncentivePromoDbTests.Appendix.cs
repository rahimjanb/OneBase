using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Telegram.Incentive;
using OneBase.Domain.Incentive;
using OneBase.Domain.Sales;
using OneBase.Infrastructure.Persistence;
using OneBase.Infrastructure.Persistence.Migrations;
using OneBase.Infrastructure.Telegram.Reports;

namespace OneBase.AI.Tests;

/// <summary>
/// Список спящих = «Приложение 1» ТЗ (миграция Incentive6_SleepingAppendix1): заказ, принятый 01.10, — покупка октября; список из файла заменяет
/// снимок, по Linko не пересчитывается и откатывается; кешбек Могула по точке не из списка журнал отменяет.
/// </summary>
public partial class IncentivePromoDbTests
{
    private static LinkoOrder YmOrder(ref long n, long agent, long market, DateOnly created, DateOnly? accepted, int packs = 16) => new()
    {
        Id = ++n, AgentId = agent, MarketId = market, Status = "delivered", CreatedDate = created, CreatedAt = created.ToDateTime(new TimeOnly(18, 0)),
        AcceptedDate = accepted, BranchName = "Андижон",
        Lines = [new LinkoOrderLine { Id = ++n, ProductId = Ym420A, Amount = packs, TotalWeight = packs * 0.42m, OriginPrice = 17_700, TotalPrice = packs * 16_461 }],
    };

    [PostgresFact]
    public async Task Sleeping_scan_counts_the_accepted_day_like_sales_and_appendix_1()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var s = await SeedProgramAsync(db, "sleep-accepted");
        long[] m = [9_764_001, 9_764_002];
        foreach (var id in m)
        {
            db.LinkoMarkets.Add(new LinkoMarket { Id = id, Name = $"Точка {id}", ResponsibleAgentId = 9_764_900, BranchName = "Андижон" });
        }

        // 1 — заказ 30.09 вечером, принят 01.10: покупка октября (как у Ergasheva в Андижон 3) — не спящая; 2 — 15.09, принят 16.09 — спящая.
        var n = 9_764_500L;
        db.LinkoOrders.AddRange(YmOrder(ref n, 9_764_900, m[0], new(2026, 9, 30), new(2026, 10, 1)), YmOrder(ref n, 9_764_900, m[1], new(2026, 9, 15), new(2026, 9, 16)));
        await db.SaveChangesAsync();

        var scan = await scope.ServiceProvider.GetRequiredService<IncentivePromoService>().ScanSleepingAsync(s, new(2026, 11, 6), default);
        Assert.DoesNotContain(m[0], scan.Sleeping);
        Assert.Contains(m[1], scan.Sleeping);
        Assert.Equal(new DateOnly(2026, 10, 1), scan.BoughtSince[m[0]]);
    }

    [PostgresFact]
    public async Task Appendix_1_replaces_the_sleeping_snapshot_is_not_recalculated_and_reverts()
    {
        fixture.Clock.Now = Morning;
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var s = await SeedProgramAsync(db, "sleep-file");
        var none = await SeedProgramAsync(db, "sleep-none");
        // Снимок по Linko: 1 — есть в Приложении 1; 2 — нет; 3 — есть, но пересчёт исключил её с 23.11; 4 — только в Приложении 1.
        long[] m = [9_765_001, 9_765_002, 9_765_003, 9_765_004];
        foreach (var id in m)
        {
            db.LinkoMarkets.Add(new LinkoMarket { Id = id, Name = $"Точка {id}", ResponsibleAgentId = 9_765_900, BranchName = "Андижон" });
        }

        var start = new DateOnly(2026, 11, 6);
        db.IncentiveSleepingMarkets.AddRange(
            new IncentiveSleepingMarket { ProgramId = s.Id, MarketId = m[0], ListedOn = start },
            new IncentiveSleepingMarket { ProgramId = s.Id, MarketId = m[1], ListedOn = new(2026, 11, 16) },
            new IncentiveSleepingMarket { ProgramId = s.Id, MarketId = m[2], ListedOn = start, RemovedOn = new(2026, 11, 23) });
        await db.SaveChangesAsync();
        async Task<List<(long Market, DateOnly Listed, DateOnly? Removed, string? Priority)>> Rows(Guid program) =>
            (await db.IncentiveSleepingMarkets.AsNoTracking().Where(x => x.ProgramId == program).OrderBy(x => x.MarketId).ToListAsync())
            .Select(x => (x.MarketId, x.ListedOn, x.RemovedOn, x.FilePriority)).ToList();
        var before = await Rows(s.Id);
        var promos = scope.ServiceProvider.GetRequiredService<IncentivePromoService>();
        Assert.False(await promos.SleepingFromFileAsync(s, default));

        // ID через запятую с переносами — как в миграции; повторный запуск ничего не меняет; у акции без снимка — ничего не делает.
        var apply = Incentive6_SleepingAppendix1.Apply(s.Code, $"{m[0]},\n            {m[3]}", $"{m[2]}", "");
        await db.Database.ExecuteSqlRawAsync(apply);
        var once = await Rows(s.Id);
        await db.Database.ExecuteSqlRawAsync(apply);
        await db.Database.ExecuteSqlRawAsync(Incentive6_SleepingAppendix1.Apply(none.Code, $"{m[0]}", "", ""));
        Assert.Equal(once, await Rows(s.Id));
        Assert.False(await db.IncentiveSleepingMarkets.AnyAsync(x => x.ProgramId == none.Id));
        Assert.Equal(
        [
            (m[0], start, (DateOnly?)null, (string?)Incentive6_SleepingAppendix1.Route),
            (m[1], new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 16), null),
            (m[2], start, null, Incentive6_SleepingAppendix1.Recent),
            (m[3], start, null, Incentive6_SleepingAppendix1.Route),
        ], once);
        Assert.True(await promos.SleepingFromFileAsync(s, default));
        Assert.Equal([m[0], m[2], m[3]], (await promos.ListedAsync(s, new(2026, 11, 25), default)).Where(m.Contains).Order().ToArray());

        // Суббота: список из файла не пересчитывается; вручную — отказ с причиной; сводка — «из Приложения 1».
        var results = scope.ServiceProvider.GetRequiredService<IncentiveResultsService>();
        var week = await results.RefreshSleepingAsync(s, new(2026, 11, 29), new(2026, 11, 30), null, default);
        Assert.Equal((0, 0, 0, 3), (week.Added, week.Removed, week.Restored, week.Active));
        Assert.Equal(once, await Rows(s.Id));
        var sleeping = scope.ServiceProvider.GetRequiredService<IncentiveSleepingService>();
        var refused = await Assert.ThrowsAsync<ReportValidationException>(() => sleeping.RefreshAsync(s.Code, Guid.NewGuid(), default));
        Assert.Contains("Приложение 1", refused.Message);
        var summary = await sleeping.SummaryAsync(s.Code, default);
        Assert.True(summary.FromFile);
        Assert.Equal((3, 1), (summary.Active, summary.Removed));

        // Откат: добавленная точка 4 удалена, у остальных — прежние значения.
        await db.Database.ExecuteSqlRawAsync(Incentive6_SleepingAppendix1.Revert(s.Code));
        Assert.Equal(before, await Rows(s.Id));
        Assert.False(await promos.SleepingFromFileAsync(s, default));
        fixture.Clock.Now = null;
    }

    [PostgresFact]
    public async Task Mogul_cashback_of_a_point_not_on_the_sleeping_list_is_cancelled_in_the_ledger()
    {
        fixture.Clock.Now = Morning;
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var s = await SeedProgramAsync(db, "ledger-list");
        const long agent = 9_766_900;
        long[] m = [9_766_001, 9_766_002];
        foreach (var id in m)
        {
            db.LinkoMarkets.Add(new LinkoMarket { Id = id, Name = $"Спящая {id}", ResponsibleAgentId = agent, BranchName = "Андижон" });
            db.IncentiveSleepingMarkets.Add(new IncentiveSleepingMarket { ProgramId = s.Id, MarketId = id, ListedOn = new(2026, 11, 6) });
        }

        // По 2 коробки 420 г 10.11 обеим точкам — кешбек по 26 338.
        var n = 9_766_500L;
        var tue = new DateOnly(2026, 11, 10);
        var orders = m.Select(id => YmOrder(ref n, agent, id, tue, tue.AddDays(1))).ToList();
        db.LinkoOrders.AddRange(orders);
        await db.SaveChangesAsync();
        var ledger = scope.ServiceProvider.GetRequiredService<IncentiveCashbackLedger>();
        await ledger.RefreshAsync(s, Today, default);
        async Task<List<(long Market, string Status, decimal Amount)>> Mogul() =>
            (await db.IncentiveCashbackAccruals.AsNoTracking().Where(a => a.ProgramId == s.Id && a.Kind == IncentiveCashbackKind.Mogul).OrderBy(a => a.MarketId).ToListAsync())
            .Select(a => (a.MarketId, a.Status, a.Amount)).ToList();
        Assert.Equal([(m[0], IncentiveCashbackStatus.Confirmed, 26_338m), (m[1], IncentiveCashbackStatus.Confirmed, 26_338m)], await Mogul());

        // Точку 2 убрали из списка с начала (её нет в «Приложении 1») — её кешбек отменяется, у точки 1 всё как было.
        await db.IncentiveSleepingMarkets.Where(x => x.ProgramId == s.Id && x.MarketId == m[1]).ExecuteUpdateAsync(u => u.SetProperty(x => x.RemovedOn, x => x.ListedOn));
        await ledger.RefreshAsync(s, Today, default);
        Assert.Equal([(m[0], IncentiveCashbackStatus.Confirmed, 26_338m), (m[1], IncentiveCashbackStatus.Cancelled, 0m)], await Mogul());
        fixture.Clock.Now = null;
    }
}
