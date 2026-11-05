using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Sales;
using OneBase.Application.Telegram.Battle;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Battle;
using OneBase.Domain.Incentive;

namespace OneBase.Application.Telegram.Incentive;

/// <summary>
/// Цели магазинов маршрута дня в кг (карточка «Маршрут дня · Twizos»). Маршрут — как у «Поля боя»: план визитов Linko на день, нет плана —
/// собственный маршрут по истории (<see cref="FieldRoutes"/>), порядок — ближайший сосед по координатам. Цель — набор по среднему закупу ТТ
/// (ТЗ: от 5 млн — 140 + 200 г … меньше 0,5 млн — 35 г); где «Поле боя» уже поставило Твизос-цель — набор того же уровня (решение 05.11.2026).
/// «Подключить» — ТТ без товара акции за окно закупа, «дозаказ» — брала. Цели ставятся один раз на день (за прошедшие дни задним числом не
/// ставятся) и хранятся в incentive.RouteTargets; статусы считаются заново по заказам и визитам дня. Только чтение Linko.
/// </summary>
public sealed class IncentiveRouteService(IAppDbContext db, SalesOptions sales, BattleOptions battle, IncentiveService incentive, TimeProvider time)
{
    private static readonly string[] NotTaken = LinkoMirrorSalesProvider.NotTaken;

    /// <summary>
    /// Маршрут дня с итогами: цели (при необходимости — постановка, если день не прошёл) и статусы по заказам и визитам до until (null — весь
    /// день). У ТП, участвующего в «Поле боя», цели «Поля боя» на день должны быть поставлены раньше — иначе набор возьмётся по правилу акции.
    /// persist = false — цели не записываются (предпросмотр): какими они были бы, если бы карточку строили сейчас.
    /// </summary>
    public async Task<IncentiveRoute> DayAsync(IncentiveSettings s, long agentId, DateOnly date, TimeOnly? until, CancellationToken ct, bool persist = true)
    {
        var products = await incentive.ProductIdsAsync(s, ct);
        var targets = await TargetsAsync(s, agentId, date, ct);
        if (targets.Count == 0 && date >= Today() && s.Covers(date))
        {
            targets = await EnsureAsync(s, agentId, date, products, persist, ct);
        }

        var routePoints = await db.LinkoVisits.AsNoTracking()
            .Where(v => v.UserId == agentId && v.MarketId != null && v.IsInPlan && v.Day == date).Select(v => v.MarketId).Distinct().CountAsync(ct);
        return new IncentiveRoute(agentId, date, targets.FirstOrDefault()?.RouteSource, await EvaluateAsync(targets, agentId, date, until, products, ct), routePoints);
    }

    /// <summary>Маршруты дней с from по to — только поставленные цели (неделя: дни, когда ТП получал карточку); в последний день — до until.</summary>
    public async Task<IReadOnlyList<IncentiveRoute>> DaysAsync(IncentiveSettings s, long agentId, DateOnly from, DateOnly to, TimeOnly? until, CancellationToken ct)
    {
        var products = await incentive.ProductIdsAsync(s, ct);
        var all = await db.IncentiveRouteTargets.AsNoTracking()
            .Where(t => t.ProgramId == s.Id && t.AgentId == agentId && t.Date >= from && t.Date <= to).OrderBy(t => t.Order).ToListAsync(ct);
        var result = new List<IncentiveRoute>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var own = all.Where(t => t.Date == d).ToList();
            result.Add(new IncentiveRoute(agentId, d, own.FirstOrDefault()?.RouteSource, await EvaluateAsync(own, agentId, d, d == to ? until : null, products, ct), 0));
        }

        return result;
    }

    private DateOnly Today() => DateOnly.FromDateTime(ReportSchedules.Local(time.GetUtcNow(), ReportSchedules.DefaultTimeZone).DateTime);

    private Task<List<IncentiveRouteTarget>> TargetsAsync(IncentiveSettings s, long agentId, DateOnly date, CancellationToken ct) =>
        db.IncentiveRouteTargets.AsNoTracking().Where(t => t.ProgramId == s.Id && t.AgentId == agentId && t.Date == date).OrderBy(t => t.Order).ToListAsync(ct);

    private sealed record MarketInfo(long Id, string Name, string? Type, string? Branch, double? Lat, double? Lon);

    /// <summary>Цели на день: магазины маршрута → набор и «подключить / дозаказ»; не больше MaxStores акции (сначала цели «Поля боя», потом крупные).</summary>
    private async Task<List<IncentiveRouteTarget>> EnsureAsync(IncentiveSettings s, long agentId, DateOnly date, long[] products, bool persist, CancellationToken ct)
    {
        var planned = await db.LinkoVisits.AsNoTracking()
            .Where(v => v.UserId == agentId && v.MarketId != null && v.IsInPlan && v.Day == date).Select(v => v.MarketId!.Value).Distinct().ToListAsync(ct);
        var source = BattleRouteSource.Linko;
        if (planned.Count == 0)
        {
            var own = await FieldRoutes.OwnWeekdaysAsync(db, sales, agentId, BattleRules.Monday(date), ct);
            planned = own.Where(kv => kv.Value == date.DayOfWeek).Select(kv => kv.Key).ToList();
            source = BattleRouteSource.Own;
        }

        if (planned.Count == 0)
        {
            return [];
        }

        var ids = planned.ToArray();
        var markets = (await db.LinkoMarkets.AsNoTracking().Where(m => ids.Contains(m.Id))
                .Select(m => new MarketInfo(m.Id, m.Name, m.MarketTypeName, m.BranchName, m.Lat, m.Lon)).ToListAsync(ct))
            .Where(m => !BattleRules.Mentions(m.Type, IncentiveRules.ExcludedMarketTypeWords)
                        && !BattleRules.Mentions(m.Branch, battle.ExcludedBranchWords)
                        && !BattleRules.Mentions(m.Name, ["тест", "test"]))
            .ToList();
        if (markets.Count == 0)
        {
            return [];
        }

        var stats = await StatsAsync(markets.Select(m => m.Id).ToArray(), date, products, ct);
        var battleGoals = (await db.BattleGoals.AsNoTracking()
                .Where(g => g.AgentId == agentId && g.Date == date && g.Kind == BattleGoalKind.Twizos).Select(g => new { g.MarketId, g.Level, g.Sub }).ToListAsync(ct))
            .GroupBy(g => g.MarketId).ToDictionary(g => g.Key, g => g.First());
        var route = BattleRules.OrderByRoute(markets, m => (m.Lat, m.Lon)).Select((m, i) => (m.Id, Index: i)).ToDictionary(x => x.Id, x => x.Index);

        var candidates = markets.Select(m =>
        {
            var (avg, ever) = stats.GetValueOrDefault(m.Id);
            var fromBattle = battleGoals.GetValueOrDefault(m.Id);
            var set = (fromBattle is null ? null : IncentiveRules.SetForLevel(s.StoreSets, fromBattle.Level)) ?? IncentiveRules.StoreSet(s.StoreSets, avg);
            var sub = fromBattle is not null
                ? fromBattle.Sub == BattleGoalSub.Connect ? IncentiveTargetSub.Connect : IncentiveTargetSub.Reorder
                : ever ? IncentiveTargetSub.Reorder : IncentiveTargetSub.Connect;
            return (Market: m, Avg: avg, Set: set, Sub: sub, Battle: fromBattle is not null && set?.Level == fromBattle.Level ? fromBattle.Level : null);
        }).Where(c => c.Set is not null).ToList();

        var chosen = candidates
            .OrderByDescending(c => c.Battle is not null)
            .ThenBy(c => c.Sub == IncentiveTargetSub.Reorder ? 0 : 1)
            .ThenByDescending(c => c.Avg)
            .Take(Math.Clamp(s.MaxStores, 1, IncentiveRules.MaxStoresLimit))
            .OrderBy(c => route[c.Market.Id])
            .ToList();
        var now = time.GetUtcNow();
        var targets = chosen.Select((c, i) => new IncentiveRouteTarget
        {
            ProgramId = s.Id, AgentId = agentId, Date = date, MarketId = c.Market.Id, MarketName = c.Market.Name, Order = i + 1, RouteSource = source,
            Sub = c.Sub, Set = c.Set!.Contents, TargetKg = c.Set.Kg, Prize = c.Set.Prize, BattleLevel = c.Battle, AvgMonth = Math.Round(c.Avg, 2), CreatedAt = now,
        }).ToList();
        if (!persist)
        {
            return targets;
        }

        db.IncentiveRouteTargets.AddRange(targets);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Цели этого дня уже поставил другой экземпляр (уникальный индекс): свои несохранённые — отбросить, взять его.
            foreach (var entry in db.ChangeTracker.Entries<IncentiveRouteTarget>().Where(e => e.State == EntityState.Added).ToList())
            {
                entry.State = EntityState.Detached;
            }

            return await TargetsAsync(s, agentId, date, ct);
        }

        foreach (var entry in db.ChangeTracker.Entries<IncentiveRouteTarget>().ToList())
        {
            entry.State = EntityState.Detached;
        }

        return targets;
    }

    /// <summary>
    /// Средний закуп ТТ в месяц (доставленные заказы по дате приёмки за окно «Поля боя»: Battle:WindowMonths месяцев, включая текущий, до дня
    /// цели) и брала ли ТТ товар акции в окне.
    /// </summary>
    private async Task<Dictionary<long, (decimal Avg, bool Ever)>> StatsAsync(long[] marketIds, DateOnly date, long[] products, CancellationToken ct)
    {
        var monthStart = new DateOnly(date.Year, date.Month, 1);
        var windowStart = monthStart.AddMonths(1 - battle.WindowMonths);
        var sold = sales.FieldSoldStatuses;
        var orders = await db.LinkoOrders.AsNoTracking()
            .Where(o => o.MarketId != null && marketIds.Contains(o.MarketId.Value) && sold.Contains(o.Status) && o.AcceptedDate != null
                        && o.AcceptedDate >= windowStart && o.AcceptedDate < date)
            .Select(o => new { o.Id, MarketId = o.MarketId!.Value, o.TotalPrice })
            .ToListAsync(ct);
        var orderIds = orders.Select(o => o.Id).ToArray();
        var withProduct = (await db.LinkoOrderLines.AsNoTracking()
                .Where(l => orderIds.Contains(l.OrderId) && l.ProductId != null && products.Contains(l.ProductId.Value)).Select(l => l.OrderId).Distinct().ToListAsync(ct))
            .ToHashSet();
        var months = Math.Max(1, battle.WindowMonths);
        return orders.GroupBy(o => o.MarketId).ToDictionary(g => g.Key, g => (g.Sum(o => o.TotalPrice) / months, g.Any(o => withProduct.Contains(o.Id))));
    }

    /// <summary>Статусы магазинов: кг товара акции в заказах ТП в этой ТТ за день (кроме отменённых) и выполненные визиты — до until.</summary>
    private async Task<IReadOnlyList<IncentiveStore>> EvaluateAsync(IReadOnlyList<IncentiveRouteTarget> targets, long agentId, DateOnly date, TimeOnly? until, long[] products,
        CancellationToken ct)
    {
        if (targets.Count == 0)
        {
            return [];
        }

        var marketIds = targets.Select(t => t.MarketId).ToArray();
        var orders = (await db.LinkoOrders.AsNoTracking()
                .Where(o => o.AgentId == agentId && o.MarketId != null && marketIds.Contains(o.MarketId.Value) && o.CreatedDate == date && !NotTaken.Contains(o.Status))
                .Select(o => new { o.Id, MarketId = o.MarketId!.Value, o.CreatedAt }).ToListAsync(ct))
            .Where(o => until is not { } u || TimeOnly.FromDateTime(o.CreatedAt) < u)
            .ToList();
        var orderIds = orders.Select(o => o.Id).ToArray();
        var kg = (await db.LinkoOrderLines.AsNoTracking()
                .Where(l => orderIds.Contains(l.OrderId) && l.ProductId != null && products.Contains(l.ProductId.Value))
                .Select(l => new { l.OrderId, l.TotalWeight }).ToListAsync(ct))
            .GroupBy(l => l.OrderId).ToDictionary(g => g.Key, g => g.Sum(l => l.TotalWeight));
        var visits = (await db.LinkoVisits.AsNoTracking()
                .Where(v => v.UserId == agentId && v.MarketId != null && marketIds.Contains(v.MarketId.Value) && v.Day == date && v.Status == "done")
                .Select(v => new { MarketId = v.MarketId!.Value, v.Date }).ToListAsync(ct))
            .Where(v => until is not { } u || TimeOnly.FromDateTime(v.Date) < u)
            .Select(v => v.MarketId)
            .ToHashSet();

        return targets.Select(t =>
        {
            var own = orders.Where(o => o.MarketId == t.MarketId).ToList();
            var taken = own.Sum(o => kg.GetValueOrDefault(o.Id));
            var visited = own.Count > 0 || visits.Contains(t.MarketId);
            return new IncentiveStore(t.MarketId, t.MarketName, t.Order, t.Sub, t.Set, t.TargetKg, t.Prize, t.BattleLevel,
                IncentiveRules.StoreStatus(taken, t.TargetKg, visited), taken, visited);
        }).ToList();
    }
}
