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
public sealed class IncentiveRouteService(IAppDbContext db, SalesOptions sales, BattleOptions battle, IncentiveService incentive, IncentivePromoService promos,
    IncentiveGoalService goalData, TimeProvider time)
{
    private static readonly string[] NotTaken = LinkoMirrorSalesProvider.NotTaken;

    /// <summary>
    /// Маршрут дня с итогами: цели (при необходимости — постановка, если день не прошёл) и статусы по заказам и визитам до until (null — весь
    /// день). У ТП, участвующего в «Поле боя», цели «Поля боя» на день должны быть поставлены раньше — иначе набор возьмётся по правилу акции.
    /// persist = false — цели не записываются (предпросмотр): какими они были бы, если бы карточку строили сейчас. norm — норма дня до приза для
    /// расчёта на начало дня (акции ноября: цели маршрута с запасом и добор вне маршрута считаются от неё).
    /// </summary>
    public async Task<IncentiveRoute> DayAsync(IncentiveSettings s, long agentId, DateOnly date, TimeOnly? until, CancellationToken ct, bool persist = true,
        decimal? norm = null)
    {
        var products = await incentive.ProductIdsAsync(s, ct);
        var targets = await TargetsAsync(s, agentId, date, ct);
        var v2 = V2(s, date);
        // Прошедший день задним числом не ставится; просмотр v2 (persist = false) считает цели на лету по истории до этого дня, без записи.
        if (targets.Count == 0 && s.Covers(date) && (date >= Today() || (!persist && v2 is not null)))
        {
            targets = v2 is { } goals && s.Offers.Twizos is { } tw2
                ? await EnsureGoalsAsync(s, goals, tw2, agentId, date, persist, ct)
                : s.Offers.Twizos is { } tw
                ? await EnsureNovemberAsync(s, tw, agentId, date, norm, persist, ct)
                : await EnsureAsync(s, agentId, date, products, persist, ct);
        }

        var routePoints = await db.LinkoVisits.AsNoTracking()
            .Where(v => v.UserId == agentId && v.MarketId != null && v.IsInPlan && v.Day == date).Select(v => v.MarketId).Distinct().CountAsync(ct);
        if (v2 is { } g2 && (targets.Count == 0 || targets.Any(t => t.Goals is not null)))
        {
            var (route, outside) = await EvaluateGoalsAsync(s, g2, targets, agentId, date, until, ct);
            return new IncentiveRoute(agentId, date, targets.FirstOrDefault()?.RouteSource, route, routePoints, [], outside);
        }

        if (s.Offers.Twizos is not null)
        {
            var (route, extra, outside) = await EvaluateNovemberAsync(s, targets, agentId, date, until, ct);
            return new IncentiveRoute(agentId, date, targets.FirstOrDefault()?.RouteSource, route, routePoints, extra, outside);
        }

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
            if (V2(s, d) is { } g2 && own.Any(t => t.Goals is not null))
            {
                var (route2, outside2) = await EvaluateGoalsAsync(s, g2, own, agentId, d, d == to ? until : null, ct);
                result.Add(new IncentiveRoute(agentId, d, own.FirstOrDefault()?.RouteSource, route2, 0, [], outside2));
                continue;
            }

            if (s.Offers.Twizos is not null)
            {
                var (route, extra, outside) = await EvaluateNovemberAsync(s, own, agentId, d, d == to ? until : null, ct);
                result.Add(new IncentiveRoute(agentId, d, own.FirstOrDefault()?.RouteSource, route, 0, extra, outside));
                continue;
            }

            result.Add(new IncentiveRoute(agentId, d, own.FirstOrDefault()?.RouteSource, await EvaluateAsync(own, agentId, d, d == to ? until : null, products, ct), 0));
        }

        return result;
    }

    private DateOnly Today() => DateOnly.FromDateTime(ReportSchedules.Local(time.GetUtcNow(), ReportSchedules.DefaultTimeZone).DateTime);

    /// <summary>Цели v2 «Цели на маршрут» на день: есть раздел goals и день в его периоде (и есть Twizos-акция ноября с подарками).</summary>
    public static IncentiveGoalsPromo? V2(IncentiveSettings s, DateOnly date) =>
        s.Offers.Goals is { } g && g.Covers(date) && s.Offers.Twizos is not null ? g : null;

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

    /// <summary>Точки маршрута дня: план визитов Linko, нет плана — собственный маршрут по истории (как у «Поля боя»).</summary>
    private async Task<(List<long> Markets, string Source)> RouteMarketsAsync(long agentId, DateOnly date, CancellationToken ct)
    {
        var planned = await db.LinkoVisits.AsNoTracking()
            .Where(v => v.UserId == agentId && v.MarketId != null && v.IsInPlan && v.Day == date).Select(v => v.MarketId!.Value).Distinct().ToListAsync(ct);
        if (planned.Count > 0)
        {
            return (planned, BattleRouteSource.Linko);
        }

        var own = await FieldRoutes.OwnWeekdaysAsync(db, sales, agentId, BattleRules.Monday(date), ct);
        return (own.Where(kv => kv.Value == date.DayOfWeek).Select(kv => kv.Key).ToList(), BattleRouteSource.Own);
    }

    /// <summary>Точка годится для цели: не офис, персонал, экспорт и тестовая, не исключённый филиал.</summary>
    private bool Eligible(MarketInfo m) =>
        !BattleRules.Mentions(m.Type, IncentiveRules.ExcludedMarketTypeWords) && !BattleRules.Mentions(m.Branch, battle.ExcludedBranchWords)
        && !BattleRules.Mentions(m.Name, ["тест", "test"]);

    /// <summary>
    /// Цели дня по акциям ноября (ТЗ 4.2–4.3): все точки маршрута в порядке маршрута; наборы Twizos — пара брали / старт не брали / «+ план» при
    /// недостаче до нормы × запас; «Добери вне маршрута» — точки ТП с других дней с самым большим чеком; спящие Yamelly — точки списка ТП на
    /// маршруте, а если их меньше нормы визитов в день — ещё спящие по приоритету вне маршрута. Ставятся один раз на день.
    /// </summary>
    private async Task<List<IncentiveRouteTarget>> EnsureNovemberAsync(IncentiveSettings s, IncentiveTwizosPromo tw, long agentId, DateOnly date, decimal? norm,
        bool persist, CancellationToken ct)
    {
        var (planned, source) = await RouteMarketsAsync(agentId, date, ct);
        if (planned.Count == 0)
        {
            return [];
        }

        var ids = planned.ToArray();
        var route = BattleRules.OrderByRoute((await db.LinkoMarkets.AsNoTracking().Where(m => ids.Contains(m.Id))
                .Select(m => new MarketInfo(m.Id, m.Name, m.MarketTypeName, m.BranchName, m.Lat, m.Lon)).ToListAsync(ct))
            .Where(Eligible).ToList(), m => (m.Lat, m.Lon));
        if (route.Count == 0)
        {
            return [];
        }

        var routeIds = route.Select(m => m.Id).ToHashSet();
        var others = (await db.LinkoMarkets.AsNoTracking().Where(m => m.ResponsibleAgentId == agentId)
                .Select(m => new MarketInfo(m.Id, m.Name, m.MarketTypeName, m.BranchName, m.Lat, m.Lon)).ToListAsync(ct))
            .Where(m => !routeIds.Contains(m.Id) && Eligible(m)).ToList();
        var checks = await promos.ChecksAsync(s, route.Select(m => m.Id).Concat(others.Select(m => m.Id)).ToList(), date, tw.CheckDays, ct);
        decimal KgOf(string code) => s.SetByCode(code)?.Kg ?? 0;
        IncentivePromoRules.Candidate Candidate(MarketInfo m) => new(m.Id, checks.GetValueOrDefault(m.Id).Check, checks.GetValueOrDefault(m.Id).Bought);

        var goals = IncentivePromoRules.RouteGoals(tw, KgOf, route.Select(Candidate).ToList(), norm).ToDictionary(g => g.MarketId);
        var gap = IncentivePromoRules.Gap(tw, goals.Values.Sum(g => IncentivePromoRules.Kg(g.Sets, KgOf)), norm);
        var extras = gap > 0 ? IncentivePromoRules.ExtraGoals(tw, KgOf, others.Select(Candidate).ToList(), gap) : [];

        // Спящие Yamelly ТП (ещё не разбуженные до сегодняшнего дня): на маршруте — предложение в выноске; меньше нормы визитов — добор по приоритету.
        var sleepingIds = new HashSet<long>();
        var sleepingExtra = new List<IncentiveSleepingPoint>();
        if (s.Offers.Yamelly is { } ym && ym.Covers(date))
        {
            var points = (await promos.SleepingAsync(s, [agentId], date, TimeOnly.MinValue, ct))[agentId].Where(p => p.WokenOn is null).ToList();
            sleepingIds = points.Select(p => p.MarketId).ToHashSet();
            var quota = IncentivePromoRules.VisitsPerDay(points.Count, IncentiveRules.WorkDays(date, ym.End, await incentive.HolidaysAsync(s, ct)));
            var onRoute = route.Count(m => sleepingIds.Contains(m.Id));
            var extraIds = extras.Select(e => e.MarketId).ToHashSet();
            sleepingExtra = points.Where(p => !routeIds.Contains(p.MarketId) && !extraIds.Contains(p.MarketId)).Take(Math.Max(0, quota - onRoute)).ToList();
        }

        var names = route.Concat(others).ToDictionary(m => m.Id, m => m.Name);
        var now = time.GetUtcNow();
        IncentiveRouteTarget Target(long market, int order, IncentivePromoRules.Goal? goal, string placement)
        {
            var sets = goal?.Sets.Select(c => s.SetByCode(c)).Where(x => x is not null).Select(x => x!).ToList() ?? [];
            return new IncentiveRouteTarget
            {
                ProgramId = s.Id, AgentId = agentId, Date = date, MarketId = market, MarketName = names.GetValueOrDefault(market, $"#{market}"), Order = order,
                RouteSource = source, Sub = goal?.Sub ?? IncentiveTargetSub.None, Set = string.Join(" + ", sets.Select(x => x.Contents)),
                TargetKg = sets.Sum(x => x.Kg), Prize = sets.Count == 0 ? null : string.Join(" + ", sets.Select(x => x.Prize)), Sets = goal is null ? null : string.Join("+", goal.Sets),
                PlanAdded = goal?.PlanAdded ?? false, Sleeping = sleepingIds.Contains(market), Placement = placement, AvgCheck = checks.GetValueOrDefault(market).Check,
                AvgMonth = 0, CreatedAt = now,
            };
        }

        var targets = route.Select((m, i) => Target(m.Id, i + 1, goals.GetValueOrDefault(m.Id), IncentivePlacement.Route)).ToList();
        targets.AddRange(extras.Select((g, i) => Target(g.MarketId, 1000 + i, g, IncentivePlacement.Extra)));
        foreach (var p in sleepingExtra)
        {
            names.TryAdd(p.MarketId, p.Name);
            targets.Add(Target(p.MarketId, 1000 + targets.Count, null, IncentivePlacement.Extra));
        }

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
    /// Итоги точек по акциям ноября до until: наборы Twizos — сколько заказов дотянули до подарка (пара — два), Yamelly — порог YM-01 у спящей и
    /// кэшбек; точка без цели — был ли заказ. Продажи вне маршрута — заказы Twizos и разбуженные спящие в точках без цели дня.
    /// </summary>
    private async Task<(List<IncentiveStore> Route, List<IncentiveStore> Extra, List<IncentiveOutOfRouteSale> Outside)> EvaluateNovemberAsync(IncentiveSettings s,
        IReadOnlyList<IncentiveRouteTarget> targets, long agentId, DateOnly date, TimeOnly? until, CancellationToken ct)
    {
        var twizos = await promos.TwizosOrdersAsync(s, [agentId], date, date, until, ct);
        var yamelly = await promos.YamellyOrdersAsync(s, [agentId], date, date, until, ct);
        var targetIds = targets.Select(t => t.MarketId).ToHashSet();
        var ordered = (await db.LinkoOrders.AsNoTracking()
                .Where(o => o.AgentId == agentId && o.MarketId != null && targetIds.Contains(o.MarketId.Value) && o.CreatedDate == date && !NotTaken.Contains(o.Status))
                .Select(o => new { MarketId = o.MarketId!.Value, o.CreatedAt }).ToListAsync(ct))
            .Where(o => until is not { } u || TimeOnly.FromDateTime(o.CreatedAt) < u).Select(o => o.MarketId).ToHashSet();
        var visits = (await db.LinkoVisits.AsNoTracking()
                .Where(v => v.UserId == agentId && v.MarketId != null && targetIds.Contains(v.MarketId.Value) && v.Day == date && v.Status == "done")
                .Select(v => new { MarketId = v.MarketId!.Value, v.Date }).ToListAsync(ct))
            .Where(v => until is not { } u || TimeOnly.FromDateTime(v.Date) < u).Select(v => v.MarketId).ToHashSet();

        IncentiveStore Store(IncentiveRouteTarget t)
        {
            var sets = string.IsNullOrEmpty(t.Sets) ? [] : t.Sets.Split('+', StringSplitOptions.RemoveEmptyEntries);
            var own = twizos.Where(o => o.MarketId == t.MarketId).ToList();
            var gifts = own.Where(o => o.Gift is not null).Select(o => o.Gift!).ToList();
            var kg = own.Sum(o => o.Kg);
            var hasOrder = ordered.Contains(t.MarketId);
            var visited = hasOrder || visits.Contains(t.MarketId);
            string? tw = sets.Length == 0 ? null
                : gifts.Count >= sets.Length ? BattleStoreStatus.Taken
                : gifts.Count > 0 || kg > 0 ? BattleStoreStatus.Partial
                : visited ? BattleStoreStatus.Refused
                : BattleStoreStatus.Ahead;
            var ym = yamelly.Where(o => o.MarketId == t.MarketId).ToList();
            var woken = ym.Where(o => o.Qualifies && o.CashbackStatus != IncentiveCashbackStatus.Cancelled).ToList();
            string? sleep = !t.Sleeping ? null
                : woken.Count > 0 ? BattleStoreStatus.Taken
                : ym.Count > 0 ? BattleStoreStatus.Partial
                : visited ? BattleStoreStatus.Refused
                : BattleStoreStatus.Ahead;
            var offers = new[] { tw, sleep }.Where(x => x is not null).ToList();
            var status = offers.Count == 0 ? hasOrder ? BattleStoreStatus.Taken : visited ? BattleStoreStatus.Refused : BattleStoreStatus.Ahead
                : offers.All(x => x == BattleStoreStatus.Taken) ? BattleStoreStatus.Taken
                : offers.Any(x => x is BattleStoreStatus.Taken or BattleStoreStatus.Partial) ? BattleStoreStatus.Partial
                : visited ? BattleStoreStatus.Refused
                : BattleStoreStatus.Ahead;
            return new IncentiveStore(t.MarketId, t.MarketName, t.Order, t.Sub, t.Set, t.TargetKg, t.Prize, t.BattleLevel, status, kg, visited, sets, t.PlanAdded,
                t.Sleeping, t.Placement, t.AvgCheck, gifts.Count, gifts, tw, sleep, woken.Sum(o => o.Cashback), hasOrder);
        }

        var route = targets.Where(t => t.Placement != IncentivePlacement.Extra).OrderBy(t => t.Order).Select(Store).ToList();
        var extra = targets.Where(t => t.Placement == IncentivePlacement.Extra).OrderBy(t => t.Order).Select(Store).ToList();
        var outsideIds = twizos.Select(o => o.MarketId).Concat(yamelly.Where(o => o.Qualifies && o.CashbackStatus != IncentiveCashbackStatus.Cancelled).Select(o => o.MarketId))
            .Where(m => !targetIds.Contains(m)).Distinct().ToArray();
        var names = await db.LinkoMarkets.AsNoTracking().Where(m => outsideIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Name, ct);
        var outside = outsideIds.Select(m =>
        {
            var own = twizos.Where(o => o.MarketId == m).ToList();
            return new IncentiveOutOfRouteSale(m, names.GetValueOrDefault(m, $"#{m}"), own.Sum(o => o.Kg), own.Sum(o => o.ListSum),
                own.Where(o => o.Gift is not null).Select(o => o.Gift!).ToList(),
                yamelly.Where(o => o.MarketId == m && o.Qualifies && o.CashbackStatus != IncentiveCashbackStatus.Cancelled).Sum(o => o.Cashback));
        }).OrderByDescending(o => o.Kg).ThenBy(o => o.Name).ToList();
        return (route, extra, outside);
    }

    /// <summary>
    /// Цели дня v2 (ТЗ v2, 4.1–4.4): все точки маршрута по коду ТТ; Могул — спящей точке своего списка (ещё не разбуженной, не в регионе YM-03):
    /// средний закуп × 1,2 в коробках фасовки; Twizos — по сегменту и истории (лестница действующим, наборы новым по чеку, стойка — новым с чеком от
    /// 600 тыс. по убыванию чека в квоте ТП). Вне маршрута целей нет. Ставятся один раз на день.
    /// </summary>
    private async Task<List<IncentiveRouteTarget>> EnsureGoalsAsync(IncentiveSettings s, IncentiveGoalsPromo g, IncentiveTwizosPromo tw, long agentId, DateOnly date,
        bool persist, CancellationToken ct)
    {
        var (planned, source) = await RouteMarketsAsync(agentId, date, ct);
        if (planned.Count == 0)
        {
            return [];
        }

        var ids = planned.ToArray();
        var route = (await db.LinkoMarkets.AsNoTracking().Where(m => ids.Contains(m.Id))
                .Select(m => new MarketInfo(m.Id, m.Name, m.MarketTypeName, m.BranchName, m.Lat, m.Lon)).ToListAsync(ct))
            .Where(Eligible).OrderBy(m => m.Id).ToList();
        if (route.Count == 0)
        {
            return [];
        }

        var routeIds = route.Select(m => m.Id).ToList();
        var checks = await promos.ChecksAsync(s, routeIds, date, g.CheckDays, ct);
        var history = await goalData.TwizosHistoryAsync(s, g, routeIds, date, ct);
        var sleeping = new HashSet<long>();
        if (s.Offers.Yamelly is { } ym && ym.Covers(date))
        {
            sleeping = (await promos.SleepingAsync(s, [agentId], date, TimeOnly.MinValue, ct))[agentId]
                .Where(p => p.WokenOn is null).Select(p => p.MarketId).ToHashSet();
        }

        var mogulIds = route.Where(m => sleeping.Contains(m.Id) && !g.MogulExcluded(m.Branch)).Select(m => m.Id).ToList();
        var mogulHistory = await goalData.MogulHistoryAsync(s, g, mogulIds, ct);
        var quota = await goalData.AgentQuotaAsync(g, agentId, date, ct);
        var standOffers = IncentiveGoalRules.StandOffers(
            route.Where(m => IncentiveGoalRules.StandCandidate(g, history.ContainsKey(m.Id), checks.GetValueOrDefault(m.Id).Check))
                .Select(m => (m.Id, checks.GetValueOrDefault(m.Id).Check)),
            quota?.PerAgent ?? 0);

        var now = time.GetUtcNow();
        var targets = route.Select((m, i) =>
        {
            var check = checks.GetValueOrDefault(m.Id).Check;
            var bought = history.TryGetValue(m.Id, out var h);
            IncentiveMogulGoal? mogul = null;
            if (s.Offers.Yamelly is { } y && mogulIds.Contains(m.Id))
            {
                var (avg, months, box) = mogulHistory.TryGetValue(m.Id, out var mh) ? mh : (0m, 0, null);
                if ((box ?? g.MogulBoxes?.FirstOrDefault()) is { } b)
                {
                    mogul = IncentiveGoalRules.MogulGoal(g, y, avg, months, b);
                }
            }

            var twizos = IncentiveGoalRules.TwizosGoal(g, tw, bought, bought ? h.AvgInvoice : 0, check, standOffers.Contains(m.Id));
            var goal = new IncentiveRouteGoal(mogul, twizos);
            return new IncentiveRouteTarget
            {
                ProgramId = s.Id, AgentId = agentId, Date = date, MarketId = m.Id, MarketName = m.Name, Order = i + 1, RouteSource = source,
                Sub = twizos is null ? IncentiveTargetSub.None : bought ? IncentiveTargetSub.Reorder : IncentiveTargetSub.Connect,
                Set = twizos?.Set ?? "", TargetKg = twizos?.Kg ?? 0, Prize = twizos?.Gift, Sleeping = mogul is not null, Placement = IncentivePlacement.Route,
                AvgCheck = check, AvgMonth = mogul?.AvgKg ?? 0, Goals = goal.ToJson(), CreatedAt = now,
            };
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
    /// Итоги точек по целям v2 до until (ТЗ v2, 4.6): Могул — накладная по YM-01 с коробками ≥ цели ✓, YM-01 меньше цели ½, визит или заказ без
    /// YM-01 ✗; Twizos — сумма в накладной ≥ рекомендованной ✓, меньше ½, без Twizos ✗; цвет точки — по всем целям. Вне маршрута (4.5) — точки без
    /// цели дня, где ТП оформил Могул по YM-01 спящей точке своего списка или любой заказ Twizos; заказ без акции не показывается.
    /// </summary>
    private async Task<(List<IncentiveStore> Route, List<IncentiveOutOfRouteSale> Outside)> EvaluateGoalsAsync(IncentiveSettings s, IncentiveGoalsPromo g,
        IReadOnlyList<IncentiveRouteTarget> targets, long agentId, DateOnly date, TimeOnly? until, CancellationToken ct)
    {
        var twizos = await promos.TwizosOrdersAsync(s, [agentId], date, date, until, ct);
        var cashback = (await goalData.TwizosCashbackOrdersAsync(s, g, [agentId], date, date, until, ct)).ToDictionary(o => o.OrderId);
        var yamelly = (await promos.YamellyOrdersAsync(s, [agentId], date, date, until, ct)).Where(o => o.CashbackStatus != IncentiveCashbackStatus.Cancelled || !o.Qualifies)
            .ToList();
        var targetIds = targets.Select(t => t.MarketId).ToHashSet();
        var ordered = (await db.LinkoOrders.AsNoTracking()
                .Where(o => o.AgentId == agentId && o.MarketId != null && targetIds.Contains(o.MarketId.Value) && o.CreatedDate == date && !NotTaken.Contains(o.Status))
                .Select(o => new { MarketId = o.MarketId!.Value, o.CreatedAt }).ToListAsync(ct))
            .Where(o => until is not { } u || TimeOnly.FromDateTime(o.CreatedAt) < u).Select(o => o.MarketId).ToHashSet();
        var visits = (await db.LinkoVisits.AsNoTracking()
                .Where(v => v.UserId == agentId && v.MarketId != null && targetIds.Contains(v.MarketId.Value) && v.Day == date && v.Status == "done")
                .Select(v => new { MarketId = v.MarketId!.Value, v.Date }).ToListAsync(ct))
            .Where(v => until is not { } u || TimeOnly.FromDateTime(v.Date) < u).Select(v => v.MarketId).ToHashSet();
        var markets = await db.LinkoMarkets.AsNoTracking().Where(m => m.ResponsibleAgentId == agentId || targetIds.Contains(m.Id))
            .Select(m => new { m.Id, m.BranchName }).ToDictionaryAsync(m => m.Id, m => m.BranchName, ct);

        IncentiveGoalFact Fact(long market, IncentiveRouteGoal? goal, bool visited)
        {
            var tw = twizos.Where(o => o.MarketId == market).ToList();
            var ym = yamelly.Where(o => o.MarketId == market && !g.MogulExcluded(markets.GetValueOrDefault(market))).ToList();
            var woken = ym.Where(o => o.Qualifies && o.CashbackStatus != IncentiveCashbackStatus.Cancelled).ToList();
            var cb = tw.Select(o => cashback.GetValueOrDefault(o.OrderId)).Where(o => o is not null).Select(o => o!).ToList();
            var gifts = tw.Where(o => o.Gift is not null).Select(o => o.Gift!).ToList();
            return new IncentiveGoalFact(
                goal?.Mogul is { } m ? IncentiveGoalRules.MogulStatus(m, ym.Select(o => (o.Qualifies && o.CashbackStatus != IncentiveCashbackStatus.Cancelled, o.Boxes)), visited) : null,
                woken.Count == 0 ? 0 : woken.Max(o => o.Boxes),
                woken.Sum(o => o.Cashback),
                goal?.Twizos is { } t ? IncentiveGoalRules.TwizosStatus(t, tw.Select(o => o.ListSum).ToList(), visited) : null,
                tw.Count == 0 ? 0 : tw.Max(o => o.ListSum),
                tw.Sum(o => o.Kg),
                cb.Where(o => o.Status != IncentiveCashbackStatus.Cancelled).Sum(o => o.Cashback),
                cb.FirstOrDefault()?.Segment,
                gifts,
                cb.Any(o => o.Stand));
        }

        IncentiveStore Store(IncentiveRouteTarget t)
        {
            var goal = IncentiveRouteGoal.Parse(t.Goals) ?? new IncentiveRouteGoal(null, null);
            var hasOrder = ordered.Contains(t.MarketId);
            var visited = hasOrder || visits.Contains(t.MarketId);
            var fact = Fact(t.MarketId, goal, visited);
            var offers = new[] { fact.MogulStatus, fact.TwizosStatus }.Where(x => x is not null).Select(x => x!).ToList();
            var status = IncentiveGoalRules.StoreStatus(offers, hasOrder, visited);
            return new IncentiveStore(t.MarketId, t.MarketName, t.Order, t.Sub, t.Set, t.TargetKg, t.Prize, null, status, fact.TwizosKg, visited, null, false,
                goal.Mogul is not null, IncentivePlacement.Route, t.AvgCheck, fact.Gifts.Count, fact.Gifts, fact.TwizosStatus, fact.MogulStatus, fact.MogulCashback,
                hasOrder, goal, fact);
        }

        var route = targets.OrderBy(t => t.Order).Select(Store).ToList();
        var outsideIds = twizos.Select(o => o.MarketId)
            .Concat(yamelly.Where(o => o.Qualifies && o.CashbackStatus != IncentiveCashbackStatus.Cancelled && !g.MogulExcluded(markets.GetValueOrDefault(o.MarketId)))
                .Select(o => o.MarketId))
            .Where(m => !targetIds.Contains(m)).Distinct().ToArray();
        var names = await db.LinkoMarkets.AsNoTracking().Where(m => outsideIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Name, ct);
        var outside = outsideIds.Select(m =>
        {
            var fact = Fact(m, null, true);
            return new IncentiveOutOfRouteSale(m, names.GetValueOrDefault(m, $"#{m}"), fact.TwizosKg, twizos.Where(o => o.MarketId == m).Sum(o => o.ListSum), fact.Gifts,
                fact.MogulCashback, fact);
        }).OrderByDescending(o => o.Fact!.Cashback).ThenBy(o => o.Name).ToList();
        return (route, outside);
    }
}
