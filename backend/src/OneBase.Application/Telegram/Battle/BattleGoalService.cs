using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Sales;
using OneBase.Domain.Battle;

namespace OneBase.Application.Telegram.Battle;

/// <summary>
/// Цели «Поля боя»: ставит цели ТП на магазины маршрута недели (план визитов Linko; нет плана — собственный маршрут по истории) по правилам
/// ТЗ v2 и оценивает их по заказам Linko. Цели недели создаются один раз при первом обращении (любая карточка недели) и хранятся в
/// battle.Goals; оценка (13:00, 18:00 — по заказам, итог недели — по доставленным) обновляет статусы. Только чтение Linko.
/// </summary>
public sealed class BattleGoalService(IAppDbContext db, SalesOptions sales, BattleOptions options, TimeProvider time)
{
    private static readonly string[] NotTaken = LinkoMirrorSalesProvider.NotTaken;

    /// <summary>Карточка дня: цели на date (при необходимости — постановка целей на всю неделю), оценка по заказам до cutoff (null — весь день).</summary>
    public async Task<BattleDay> DayAsync(long agentId, string agentName, string? region, DateOnly date, TimeOnly? cutoff, bool final, CancellationToken ct)
    {
        var monday = BattleRules.Monday(date);
        var mogulActive = await EnsureWeekAsync(agentId, agentName, monday, ct);
        var goals = await EvaluateAsync(agentId, monday, date, cutoff, final, ct);
        var ordersAsOf = await db.LinkoSyncStates.AsNoTracking().Where(s => s.Entity == "orders").Select(s => s.LastSuccessAt).FirstOrDefaultAsync(ct);
        var today = goals.Where(g => g.Date == date).ToList();
        var source = today.FirstOrDefault()?.RouteSource ?? BattleRouteSource.Linko;
        return new BattleDay(agentId, agentName, region, date, source, Stores(today), mogulActive.Contains(date), Progress(goals), ordersAsOf);
    }

    /// <summary>Карточка недели: дни пн–сб со статусами, ход недели, место среди ТП региона (peers), у кого есть цели этой недели.</summary>
    public async Task<BattleWeek> WeekAsync(long agentId, string agentName, string? region, DateOnly monday, DateOnly through, TimeOnly? cutoff, bool final,
        IReadOnlyCollection<long>? peers, CancellationToken ct)
    {
        await EnsureWeekAsync(agentId, agentName, monday, ct);
        var goals = await EvaluateAsync(agentId, monday, through, cutoff, final, ct);
        var ordersAsOf = await db.LinkoSyncStates.AsNoTracking().Where(s => s.Entity == "orders").Select(s => s.LastSuccessAt).FirstOrDefaultAsync(ct);
        var days = Enumerable.Range(0, 6).Select(monday.AddDays).Select(d =>
        {
            var own = goals.Where(g => g.Date == d).ToList();
            return new BattleWeekDay(d, Stores(own), own.Count(g => g.Status == BattleGoalStatus.Taken), own.Count);
        }).ToList();

        int? rank = null;
        var participants = 0;
        if (peers is { Count: > 0 })
        {
            var ids = peers.ToArray();
            var standings = (await db.BattleGoals.AsNoTracking().Where(g => g.Week == monday && ids.Contains(g.AgentId))
                    .GroupBy(g => g.AgentId)
                    .Select(g => new { Agent = g.Key, Taken = g.Count(x => x.Status == BattleGoalStatus.Taken), Sum = g.Sum(x => x.TakenSum ?? 0) })
                    .ToListAsync(ct))
                .OrderByDescending(s => s.Taken).ThenByDescending(s => s.Sum).ThenBy(s => s.Agent).ToList();
            participants = standings.Count;
            var index = standings.FindIndex(s => s.Agent == agentId);
            rank = index >= 0 ? index + 1 : null;
        }

        return new BattleWeek(agentId, agentName, region, monday, monday.AddDays(5), days, Progress(goals), rank, participants, ordersAsOf);
    }

    /// <summary>ТП вне программы: оптовые рынки (по словам в имени Linko).</summary>
    public async Task<bool> IsExcludedAgentAsync(long agentId, CancellationToken ct)
    {
        var user = await db.LinkoUsers.AsNoTracking().Where(u => u.Id == agentId).Select(u => new { u.FirstName, u.SecondName, u.Username }).FirstOrDefaultAsync(ct);
        return user is not null && BattleRules.Mentions($"{user.FirstName} {user.SecondName} {user.Username}", options.ExcludedAgentWords);
    }

    // ---------- Постановка целей ----------

    private sealed record MarketInfo(long Id, string Name, string? Type, string? Branch, long? Agent, double? Lat, double? Lon);

    /// <summary>Закупы ТТ: средний в месяц за окно (всего, Твизос, Могул), были ли Твизос и Могул в окне, Могул в текущем месяце, лучшие накладные месяца (до скидки).</summary>
    private sealed record MarketStats(decimal Avg, decimal AvgTwizos, decimal AvgMogul, bool TwizosEver, bool MogulEver, bool MogulThisMonth, decimal MaxTwizosInvoice, decimal MaxMogulInvoice);

    /// <summary>
    /// Цели на дни недели, которых ещё нет. Возвращает дни недели, в которые действует сетка Могул (акция в Linko), — у дней без неё целей Могул нет.
    /// </summary>
    private async Task<HashSet<DateOnly>> EnsureWeekAsync(long agentId, string agentName, DateOnly monday, CancellationToken ct)
    {
        var days = Enumerable.Range(0, 6).Select(monday.AddDays).ToList();
        var mogulDays = await MogulDaysAsync(days, ct);
        var existing = await db.BattleGoals.AsNoTracking().Where(g => g.AgentId == agentId && g.Week == monday).Select(g => g.Date).Distinct().ToListAsync(ct);
        var missing = days.Where(d => !existing.Contains(d)).ToList();
        if (missing.Count == 0 || BattleRules.Mentions(agentName, options.ExcludedAgentWords))
        {
            return mogulDays;
        }

        var saturday = days[^1];
        var plan = (await db.LinkoVisits.AsNoTracking()
                .Where(v => v.UserId == agentId && v.MarketId != null && v.IsInPlan && v.Day >= monday && v.Day <= saturday)
                .Select(v => new { v.Day, MarketId = v.MarketId!.Value })
                .ToListAsync(ct))
            .GroupBy(v => v.Day).ToDictionary(g => g.Key, g => g.Select(v => v.MarketId).Distinct().ToList());
        var routes = new Dictionary<DateOnly, (List<long> Markets, string Source)>();
        Dictionary<long, DayOfWeek>? own = null;
        foreach (var day in missing)
        {
            if (plan.TryGetValue(day, out var planned) && planned.Count > 0)
            {
                routes[day] = (planned, BattleRouteSource.Linko);
                continue;
            }

            // Плана в Linko нет — свой маршрут: день недели точки по её плановым визитам или заказам.
            own ??= await OwnWeekdaysAsync(agentId, monday, ct);
            var mine = own.Where(kv => kv.Value == day.DayOfWeek).Select(kv => kv.Key).ToList();
            if (mine.Count > 0)
            {
                routes[day] = (mine, BattleRouteSource.Own);
            }
        }

        if (routes.Count == 0)
        {
            return mogulDays;
        }

        var marketIds = routes.Values.SelectMany(r => r.Markets).Distinct().ToArray();
        var markets = await db.LinkoMarkets.AsNoTracking().Where(m => marketIds.Contains(m.Id))
            .Select(m => new MarketInfo(m.Id, m.Name, m.MarketTypeName, m.BranchName, m.ResponsibleAgentId, m.Lat, m.Lon)).ToDictionaryAsync(m => m.Id, ct);
        var stats = await StatsAsync(marketIds, monday, ct);
        var previous = await db.BattleGoals.AsNoTracking().Where(g => g.AgentId == agentId && g.Week == monday.AddDays(-7)).ToListAsync(ct);
        var mogulAllowedAgent = !BattleRules.Mentions(agentName, options.MogulExcludedAgentWords);
        var now = time.GetUtcNow();

        foreach (var (day, route) in routes.OrderBy(r => r.Key))
        {
            var ordered = BattleRules.OrderByRoute(route.Markets.Where(markets.ContainsKey).Select(id => markets[id]).ToList(), m => (m.Lat, m.Lon));
            var eligible = ordered.Where(m => Eligible(m, stats.GetValueOrDefault(m.Id))).ToList();
            var repeat = previous.Where(p => p.Date == day.AddDays(-7) && p.Status != BattleGoalStatus.Taken).Select(p => (p.MarketId, p.Kind)).ToHashSet();
            var goals = new List<BattleGoal>();
            goals.AddRange(TwizosGoals(eligible, stats, repeat));
            if (mogulDays.Contains(day) && mogulAllowedAgent)
            {
                goals.AddRange(MogulGoals(eligible, stats, repeat));
            }

            // Порядок магазинов на карточке — порядок маршрута; магазинов с целями не больше MaxStores.
            var withGoals = ordered.Where(m => goals.Any(g => g.MarketId == m.Id)).Take(options.MaxStores).ToList();
            var order = withGoals.Select((m, i) => (m.Id, Index: i + 1)).ToDictionary(x => x.Id, x => x.Index);
            foreach (var g in goals.Where(g => order.ContainsKey(g.MarketId)))
            {
                g.Date = day;
                g.Week = monday;
                g.AgentId = agentId;
                g.Order = order[g.MarketId];
                g.RouteSource = route.Source;
                g.CreatedAt = now;
                db.BattleGoals.Add(g);
            }
        }

        await db.SaveChangesAsync(ct);
        return mogulDays;
    }

    private bool Eligible(MarketInfo m, MarketStats? s) =>
        s is not null && s.Avg >= options.ActiveBaseMinAvg
        && !BattleRules.Mentions(m.Type, options.ExcludedMarketTypeWords)
        && !BattleRules.Mentions(m.Branch, options.ExcludedBranchWords)
        && !BattleRules.Mentions(m.Name, ["тест", "test"]);

    /// <summary>Твизос: сначала «подключить» (без Твизос в окне, крупные первыми), затем призовой набор (уже берёт, но не брала набор своего уровня).</summary>
    private IEnumerable<BattleGoal> TwizosGoals(IReadOnlyList<MarketInfo> eligible, IReadOnlyDictionary<long, MarketStats> stats, HashSet<(long, string)> repeat)
    {
        var candidates = new List<(MarketInfo M, MarketStats S, BattleTwizosSet Set, string Sub, decimal Sort)>();
        foreach (var m in eligible)
        {
            var s = stats[m.Id];
            if (!s.TwizosEver)
            {
                if (BattleRules.TwizosSet(options, s.Avg) is { } set)
                {
                    candidates.Add((m, s, set, BattleGoalSub.Connect, s.Avg));
                }
            }
            else if (BattleRules.TwizosSetToOffer(options, s.Avg, s.MaxTwizosInvoice) is { } next)
            {
                candidates.Add((m, s, next, BattleGoalSub.Prize, s.AvgTwizos));
            }
        }

        var chosen = candidates
            .OrderByDescending(c => repeat.Contains((c.M.Id, BattleGoalKind.Twizos)))
            .ThenBy(c => c.Sub == BattleGoalSub.Connect ? 0 : 1)
            .ThenByDescending(c => c.Sort)
            .Take(options.MaxTwizosGoals)
            .ToList();
        return chosen.Select((c, i) => new BattleGoal
        {
            MarketId = c.M.Id,
            MarketName = c.M.Name,
            RouteSource = BattleRouteSource.Linko,
            Kind = BattleGoalKind.Twizos,
            Sub = c.Sub,
            Level = c.Set.Level,
            TargetSum = c.Set.Sum,
            Percent = 0,
            ProductCode = options.TwizosTopCodes.Length == 0 ? null : options.TwizosTopCodes[i % options.TwizosTopCodes.Length],
            AvgMonth = c.S.Avg,
        });
    }

    /// <summary>Могул: 5 подключений (без Могул в этом месяце: сначала кто брал раньше, потом новые — H) и 5 объёмов (следующий порог от лучшей накладной месяца).</summary>
    private IEnumerable<BattleGoal> MogulGoals(IReadOnlyList<MarketInfo> eligible, IReadOnlyDictionary<long, MarketStats> stats, HashSet<(long, string)> repeat)
    {
        var allowed = eligible.Where(m => !BattleRules.Mentions(m.Branch, options.MogulExcludedBranchWords)).ToList();
        var connect = allowed.Where(m => !stats[m.Id].MogulThisMonth)
            .Select(m => (M: m, S: stats[m.Id], Tier: BattleRules.MogulTier(options, stats[m.Id].AvgMogul)))
            .OrderByDescending(c => repeat.Contains((c.M.Id, BattleGoalKind.Mogul)))
            .ThenBy(c => c.S.MogulEver ? 0 : 1)
            .ThenByDescending(c => c.S.AvgMogul)
            .Take(options.MaxMogulConnect)
            .Select(c => new BattleGoal
            {
                MarketId = c.M.Id, MarketName = c.M.Name, RouteSource = BattleRouteSource.Linko, Kind = BattleGoalKind.Mogul, Sub = BattleGoalSub.Connect,
                Level = c.Tier.Letter, TargetSum = c.Tier.Threshold1, SecondSum = c.Tier.Threshold2, Percent = options.MogulPercent, SecondPercent = c.Tier.Percent2,
                AvgMonth = c.S.AvgMogul,
            });
        var volume = allowed.Where(m => stats[m.Id].MogulThisMonth)
            .Select(m =>
            {
                var s = stats[m.Id];
                var tier = BattleRules.MogulTier(options, s.AvgMogul);
                return (M: m, S: s, Tier: tier, Target: BattleRules.MogulVolumeTarget(options, tier, s.MaxMogulInvoice));
            })
            .Where(c => c.Target is not null)
            .OrderByDescending(c => repeat.Contains((c.M.Id, BattleGoalKind.Mogul)))
            .ThenByDescending(c => c.S.MaxMogulInvoice / c.Target!.Value.Target) // сначала те, кому ближе до цели
            .Take(options.MaxMogulVolume)
            .Select(c => new BattleGoal
            {
                MarketId = c.M.Id, MarketName = c.M.Name, RouteSource = BattleRouteSource.Linko, Kind = BattleGoalKind.Mogul, Sub = BattleGoalSub.Volume,
                Level = c.Tier.Letter, TargetSum = c.Target!.Value.Target, SecondSum = c.Target.Value.Second, Percent = c.Target.Value.Percent,
                SecondPercent = c.Target.Value.SecondPercent, AvgMonth = c.S.AvgMogul,
            });
        return connect.Concat(volume);
    }

    /// <summary>Дни недели, в которые в Linko действует хоть одна акция сетки Могул (по названию).</summary>
    private async Task<HashSet<DateOnly>> MogulDaysAsync(IReadOnlyList<DateOnly> days, CancellationToken ct)
    {
        var from = days[0];
        var to = days[^1];
        var promos = await db.LinkoPromotions.AsNoTracking()
            .Where(p => p.Type == "discount" && (p.BeginDate == null || p.BeginDate <= to) && (p.TillDate == null || p.TillDate >= from))
            .Select(p => new { p.Name, p.BeginDate, p.TillDate }).ToListAsync(ct);
        var grid = promos.Where(p => BattleRules.ParseMogulPromo(options, p.Name) is not null).ToList();
        return days.Where(d => grid.Any(p => (p.BeginDate is null || p.BeginDate <= d) && (p.TillDate is null || p.TillDate >= d))).ToHashSet();
    }

    /// <summary>
    /// Собственный маршрут (нет плана в Linko): точки, где ТП ответственный, по дню недели — чаще всего в плане за 8 недель, иначе чаще всего
    /// заказывали за 90 дней, иначе ровно по дням.
    /// </summary>
    private async Task<Dictionary<long, DayOfWeek>> OwnWeekdaysAsync(long agentId, DateOnly monday, CancellationToken ct)
    {
        var markets = await db.LinkoMarkets.AsNoTracking().Where(m => m.ResponsibleAgentId == agentId).Select(m => m.Id).ToListAsync(ct);
        if (markets.Count == 0)
        {
            return [];
        }

        var ids = markets.ToArray();
        var since = monday.AddDays(-56);
        var planned = (await db.LinkoVisits.AsNoTracking()
                .Where(v => v.UserId == agentId && v.MarketId != null && ids.Contains(v.MarketId.Value) && v.IsInPlan && v.Day >= since && v.Day < monday)
                .Select(v => new { MarketId = v.MarketId!.Value, v.Day }).ToListAsync(ct))
            .GroupBy(v => v.MarketId)
            .ToDictionary(g => g.Key, g => g.GroupBy(v => v.Day.DayOfWeek).OrderByDescending(x => x.Count()).First().Key);
        var sold = sales.FieldSoldStatuses;
        var orderSince = monday.AddDays(-90);
        var ordered = (await db.LinkoOrders.AsNoTracking()
                .Where(o => o.MarketId != null && ids.Contains(o.MarketId.Value) && sold.Contains(o.Status) && o.AcceptedDate != null && o.AcceptedDate >= orderSince && o.AcceptedDate < monday)
                .Select(o => new { MarketId = o.MarketId!.Value, Day = o.AcceptedDate!.Value }).ToListAsync(ct))
            .GroupBy(o => o.MarketId)
            .ToDictionary(g => g.Key, g => g.GroupBy(o => o.Day.DayOfWeek).Where(x => x.Key != DayOfWeek.Sunday).OrderByDescending(x => x.Count()).Select(x => (DayOfWeek?)x.Key).FirstOrDefault());
        DayOfWeek[] workDays = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday];
        return markets.ToDictionary(id => id, id =>
            planned.TryGetValue(id, out var p) ? p : ordered.TryGetValue(id, out var o) && o is { } day ? day : workDays[(int)(id % 6)]);
    }

    /// <summary>Закупы ТТ за окно месяцев (доставленные заказы по дате приёмки): средние в месяц и лучшие накладные текущего месяца до скидки.</summary>
    private async Task<Dictionary<long, MarketStats>> StatsAsync(long[] marketIds, DateOnly monday, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var windowStart = monthStart.AddMonths(1 - options.WindowMonths);
        var sold = sales.FieldSoldStatuses;
        var orders = await db.LinkoOrders.AsNoTracking()
            .Where(o => o.MarketId != null && marketIds.Contains(o.MarketId.Value) && sold.Contains(o.Status) && o.AcceptedDate != null && o.AcceptedDate >= windowStart && o.AcceptedDate <= today)
            .Select(o => new { o.Id, MarketId = o.MarketId!.Value, Accepted = o.AcceptedDate!.Value, o.TotalPrice })
            .ToListAsync(ct);
        var sums = await CategorySumsAsync(orders.Select(o => o.Id).ToArray(), ct);
        var months = Math.Max(1, options.WindowMonths);
        var result = new Dictionary<long, MarketStats>();
        foreach (var g in orders.GroupBy(o => o.MarketId))
        {
            decimal total = 0, twizos = 0, mogul = 0, maxTwizos = 0, maxMogul = 0;
            bool twizosEver = false, mogulEver = false, mogulThisMonth = false;
            foreach (var o in g)
            {
                var (tw, mo, twBefore, moBefore) = sums.TryGetValue(o.Id, out var s) ? s : (0, 0, 0, 0);
                total += o.TotalPrice;
                twizos += tw;
                mogul += mo;
                twizosEver |= tw > 0;
                mogulEver |= mo > 0;
                if (o.Accepted >= monthStart)
                {
                    mogulThisMonth |= mo > 0;
                    maxTwizos = Math.Max(maxTwizos, twBefore);
                    maxMogul = Math.Max(maxMogul, moBefore);
                }
            }

            result[g.Key] = new MarketStats(total / months, twizos / months, mogul / months, twizosEver, mogulEver, mogulThisMonth, maxTwizos, maxMogul);
        }

        return result;
    }

    /// <summary>По заказам: суммы Твизос и Могул после скидки и до скидки (origin — TotalPrice + TotalDiscount; без скидок по строкам — TotalPrice).</summary>
    private async Task<Dictionary<long, (decimal Twizos, decimal Mogul, decimal TwizosBefore, decimal MogulBefore)>> CategorySumsAsync(long[] orderIds, CancellationToken ct)
    {
        if (orderIds.Length == 0)
        {
            return [];
        }

        var twizosTypes = options.TwizosTypeIds;
        var mogulTypes = options.MogulTypeIds;
        var rows = await (from l in db.LinkoOrderLines.AsNoTracking()
                join p in db.LinkoProducts.AsNoTracking() on l.ProductId equals p.Id
                where orderIds.Contains(l.OrderId) && p.TypeId != null && (twizosTypes.Contains(p.TypeId.Value) || mogulTypes.Contains(p.TypeId.Value))
                select new { l.OrderId, Type = p.TypeId!.Value, l.TotalPrice, l.TotalDiscount })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.OrderId).ToDictionary(g => g.Key, g =>
        {
            decimal tw = 0, mo = 0, twb = 0, mob = 0;
            foreach (var r in g)
            {
                var before = r.TotalPrice + (r.TotalDiscount ?? 0);
                if (twizosTypes.Contains(r.Type))
                {
                    tw += r.TotalPrice;
                    twb += before;
                }
                else
                {
                    mo += r.TotalPrice;
                    mob += before;
                }
            }

            return (tw, mo, twb, mob);
        });
    }

    // ---------- Оценка ----------

    /// <summary>
    /// Статусы целей недели по заказам ТП: взята — накладная категории (до скидки) не меньше цели; визит или заказ есть, цели нет — отказ.
    /// final — по доставленным накладным (итог недели), после чего статус по текущим заказам не пересматривается.
    /// </summary>
    private async Task<List<BattleGoal>> EvaluateAsync(long agentId, DateOnly monday, DateOnly through, TimeOnly? cutoff, bool final, CancellationToken ct)
    {
        var goals = await db.BattleGoals.Where(g => g.AgentId == agentId && g.Week == monday).OrderBy(g => g.Date).ThenBy(g => g.Order).ToListAsync(ct);
        var toEvaluate = goals.Where(g => g.Date <= through && (!g.Final || final)).ToList();
        if (toEvaluate.Count == 0)
        {
            return goals;
        }

        var sold = sales.FieldSoldStatuses;
        var ordersQuery = db.LinkoOrders.AsNoTracking().Where(o => o.AgentId == agentId && o.MarketId != null && o.CreatedDate >= monday && o.CreatedDate <= through);
        ordersQuery = final ? ordersQuery.Where(o => sold.Contains(o.Status)) : ordersQuery.Where(o => !NotTaken.Contains(o.Status));
        var orders = await ordersQuery.Select(o => new { o.Id, MarketId = o.MarketId!.Value, o.CreatedDate, o.CreatedAt, o.TotalPrice }).ToListAsync(ct);
        var sums = await CategorySumsAsync(orders.Select(o => o.Id).ToArray(), ct);
        var visits = (await db.LinkoVisits.AsNoTracking()
                .Where(v => v.UserId == agentId && v.MarketId != null && v.Status == "done" && v.Day >= monday && v.Day <= through)
                .Select(v => new { MarketId = v.MarketId!.Value, v.Day, v.Date }).ToListAsync(ct))
            .Select(v => (v.MarketId, v.Day, Time: TimeOnly.FromDateTime(v.Date))).ToHashSet();
        var now = time.GetUtcNow();
        var changed = false;
        foreach (var g in toEvaluate)
        {
            var until = g.Date == through ? cutoff : null;
            var own = orders.Where(o => o.MarketId == g.MarketId && o.CreatedDate == g.Date && (until is not { } u || TimeOnly.FromDateTime(o.CreatedAt) < u)).ToList();
            var best = own.Select(o => (o.Id, Sum: sums.TryGetValue(o.Id, out var s) ? g.Kind == BattleGoalKind.Twizos ? s.TwizosBefore : s.MogulBefore : 0))
                .OrderByDescending(x => x.Sum).FirstOrDefault();
            var visited = own.Count > 0 || visits.Any(v => v.MarketId == g.MarketId && v.Day == g.Date && (until is not { } u2 || v.Time < u2));
            var status = best.Sum >= g.TargetSum && g.TargetSum > 0 ? BattleGoalStatus.Taken : visited ? BattleGoalStatus.Refused : BattleGoalStatus.Pending;
            var takenSum = status == BattleGoalStatus.Taken ? best.Sum : (decimal?)null;
            var takenOrder = status == BattleGoalStatus.Taken ? best.Id : (long?)null;
            var takenPercent = status == BattleGoalStatus.Taken && g.Kind == BattleGoalKind.Mogul
                ? g.SecondSum is { } second && best.Sum >= second ? g.SecondPercent : g.Percent
                : null;
            if (g.Status != status || g.TakenSum != takenSum || g.TakenOrderId != takenOrder || g.TakenPercent != takenPercent || (final && !g.Final))
            {
                g.Status = status;
                g.TakenSum = takenSum;
                g.TakenOrderId = takenOrder;
                g.TakenPercent = takenPercent;
                g.Final = final || g.Final;
                g.EvaluatedAt = now;
                g.UpdatedAt = now;
                changed = true;
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync(ct);
        }

        // Магазин «пройден» — визит или заказ: для карточки нужны и магазины, где цели не взяты.
        return goals;
    }

    private static IReadOnlyList<BattleStore> Stores(IReadOnlyList<BattleGoal> goals) =>
        goals.GroupBy(g => g.MarketId).OrderBy(g => g.First().Order).Select(g =>
        {
            var views = g.OrderBy(x => x.Kind == BattleGoalKind.Twizos ? 0 : 1).Select(x => new BattleGoalView(x.Id, x.Kind, x.Sub, x.Level, x.TargetSum, x.SecondSum, x.Percent,
                x.SecondPercent, x.ProductCode, x.Status, x.TakenSum, x.TakenPercent)).ToList();
            var taken = views.Count(v => v.IsTaken);
            var visited = g.Any(x => x.Status != BattleGoalStatus.Pending);
            var status = taken == views.Count ? BattleStoreStatus.Taken : taken > 0 ? BattleStoreStatus.Partial : visited ? BattleStoreStatus.Refused : BattleStoreStatus.Ahead;
            return new BattleStore(g.Key, g.First().MarketName, g.First().Order, views, status, visited, g.Max(x => x.TakenSum ?? 0));
        }).ToList();

    private BattleWeekProgress Progress(IReadOnlyList<BattleGoal> week)
    {
        var total = week.Count;
        var taken = week.Count(g => g.Status == BattleGoalStatus.Taken);
        var newTotal = week.Count(g => g.Sub == BattleGoalSub.Connect);
        var newTaken = week.Count(g => g.Sub == BattleGoalSub.Connect && g.Status == BattleGoalStatus.Taken);
        var newOk = newTaken >= Math.Min(options.MinNewAkb, newTotal);
        var next = BattleRules.NextStep(options, taken, total);
        return new BattleWeekProgress(taken, total, newTaken, newTotal, newOk ? BattleRules.Bonus(options, taken, total) : 0, next?.Step, next?.Goals ?? 0, newOk);
    }
}
