using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Semantic;

namespace OneBase.Application.Sales.Visits;

/// <summary>Номер визита: 1, 2, 3, 4 и «5+». Orders — заказов на этом визите, Reached — сколько раз дошли до этого номера, Conversion — Orders / Reached.</summary>
public sealed record VisitSequenceBucket(string Label, int Orders, double? Share, int Reached, double? Conversion);

/// <summary>
/// Итог по группе (всё, регион, ТП): пар «ТП × точка» с визитами в периоде, выполненных визитов, заказов на визитах и без визита, по номеру визита,
/// среднее и медиана визитов до заказа, доля заказов с первого визита, пары, где после последнего заказа были визиты, а заказа ещё нет.
/// </summary>
public sealed record VisitSequenceGroup(
    string Id,
    string Name,
    int Pairs,
    int Visits,
    int OrdersOnVisit,
    int OrdersWithoutVisit,
    IReadOnlyList<VisitSequenceBucket> Buckets,
    double? AverageVisitsToOrder,
    double? MedianVisitsToOrder,
    double? FirstVisitShare,
    int OpenPairs,
    double? AverageOpenStreak);

/// <summary>
/// «На какой по счёту визит приходится заказ» за период: правило, итог, регионы, ТП (больше заказов — выше), замечания. Номер визита считается по
/// каждой паре «ТП × точка»: визиты по порядку дней, счёт сбрасывается на заказе; визиты за LookbackDays до начала периода только задают счёт.
/// </summary>
public sealed record VisitSequenceReport(
    DateOnly From,
    DateOnly To,
    int LookbackDays,
    string Rule,
    VisitSequenceGroup Total,
    IReadOnlyList<VisitSequenceGroup> Regions,
    IReadOnlyList<VisitSequenceGroup> Agents,
    IReadOnlyList<VisitOpenStreak> OpenStreaks,
    int OpenStreaksTotal,
    IReadOnlyList<string> Notes);

/// <summary>
/// Серия без заказа: пара «ТП × точка», где после последнего заказа было Visits выполненных визитов подряд, а заказа нет (визиты за 60 дней до
/// периода тоже в счёте). LastOrder — день последнего заказа этого ТП в этой точке; null — заказа не было с начала окна (LookbackFrom).
/// </summary>
public sealed record VisitOpenStreak(long AgentId, string Agent, long MarketId, string Market, string Region, int Visits, DateOnly LastVisit, DateOnly? LastOrder);

/// <summary>Отметки пары «ТП × точка» по дням: был ли выполненный визит и был ли заказ этого ТП в этой точке (по дню ввода).</summary>
public sealed record VisitDay(DateOnly Day, bool Visited, bool Ordered);

/// <summary>
/// Счёт номера визита (без базы — проверяется тестами). По дням пары: выполненный визит увеличивает счёт; заказ в день визита — «заказ на k-м
/// визите», заказ без визита (по телефону, через другого) — отдельно; любой заказ сбрасывает счёт. Дни до from только задают счёт.
/// </summary>
public sealed class VisitSequenceCounter
{
    public const int Buckets = 5;

    private readonly int[] _orders = new int[Buckets];
    private readonly int[] _reached = new int[Buckets];
    private readonly List<int> _orderVisitNumbers = [];
    private readonly List<int> _openStreaks = [];

    public int Pairs { get; private set; }
    public int Visits { get; private set; }
    public int OrdersWithoutVisit { get; private set; }

    public static int Bucket(int k) => Math.Min(k, Buckets) - 1;

    /// <summary>Одна пара «ТП × точка»; days — по возрастанию дня, без повторов. Возвращает серию без заказа на конец (0 — нет или пары нет в периоде).</summary>
    public int Add(IReadOnlyList<VisitDay> days, DateOnly from)
    {
        var k = 0;
        var counted = false;
        foreach (var d in days)
        {
            var inPeriod = d.Day >= from;
            if (d.Visited)
            {
                k++;
                if (inPeriod)
                {
                    counted = true;
                    Visits++;
                    _reached[Bucket(k)]++;
                }
            }

            if (d.Ordered)
            {
                if (inPeriod)
                {
                    if (d.Visited)
                    {
                        _orders[Bucket(k)]++;
                        _orderVisitNumbers.Add(k);
                    }
                    else
                    {
                        OrdersWithoutVisit++;
                    }
                }

                k = 0;
            }
        }

        if (!counted)
        {
            return 0;
        }

        Pairs++;
        if (k > 0)
        {
            _openStreaks.Add(k);
        }

        return k;
    }

    public void Merge(VisitSequenceCounter other)
    {
        for (var i = 0; i < Buckets; i++)
        {
            _orders[i] += other._orders[i];
            _reached[i] += other._reached[i];
        }

        _orderVisitNumbers.AddRange(other._orderVisitNumbers);
        _openStreaks.AddRange(other._openStreaks);
        Pairs += other.Pairs;
        Visits += other.Visits;
        OrdersWithoutVisit += other.OrdersWithoutVisit;
    }

    public int OrdersOnVisit => _orderVisitNumbers.Count;

    public VisitSequenceGroup Result(string id, string name)
    {
        var total = _orderVisitNumbers.Count;
        var buckets = Enumerable.Range(0, Buckets).Select(i => new VisitSequenceBucket(
            i == Buckets - 1 ? $"{Buckets}+" : (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
            _orders[i],
            total == 0 ? null : (double)_orders[i] / total,
            _reached[i],
            _reached[i] == 0 ? null : (double)_orders[i] / _reached[i])).ToList();
        var sorted = _orderVisitNumbers.Order().ToList();
        double? median = sorted.Count == 0 ? null
            : sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2.0;
        return new VisitSequenceGroup(id, name, Pairs, Visits, total, OrdersWithoutVisit, buckets,
            total == 0 ? null : _orderVisitNumbers.Average(), median, total == 0 ? null : (double)_orders[0] / total,
            _openStreaks.Count, _openStreaks.Count == 0 ? null : _openStreaks.Average());
    }
}

/// <summary>
/// Последовательность визитов по данным Linko (только чтение): выполненные визиты (`status = done`) и заказы того же ТП в той же точке по дню ввода
/// (статусы вторички, без «Завода») — те же правила «визита с заказом», что у страйка «Продаж». Область пользователя соблюдается: ТП — только его
/// точки, регион — точки филиалов региона.
/// </summary>
public sealed class VisitSequenceService(IAppDbContext db, SalesOptions options, SemanticContextLoader contextLoader)
{
    public const int LookbackDays = 60;
    public const int MaxDays = 366;

    /// <summary>Серии без заказа в отчёте — самые длинные (остальные — только числом OpenStreaksTotal).</summary>
    public const int OpenStreaksShown = 200;

    public const string RuleText =
        "Номер визита считается по каждой паре «ТП × точка»: выполненные визиты по порядку дней; заказ в день визита (этот ТП, эта точка, день ввода " +
        "заказа) — «заказ на k-м визите»; после любого заказа счёт начинается заново. Визиты за 60 дней до начала периода только задают счёт. Заказ " +
        "без визита в этот день (по телефону или другим путём) считается отдельно и тоже сбрасывает счёт. Точки «Завода» и мерча не в счёте.";

    public async Task<VisitSequenceReport> BuildAsync(SalesRowScope scope, DateOnly from, DateOnly to, Guid? regionId, long? agentId, CancellationToken ct)
    {
        if (to < from)
        {
            (from, to) = (to, from);
        }

        if (to.DayNumber - from.DayNumber + 1 > MaxDays)
        {
            throw new ArgumentException($"Период — не больше {MaxDays} дней.");
        }

        var context = await contextLoader.LoadAsync(ct);
        if (scope.Level == SalesRowScope.AgentLevel)
        {
            if (agentId is { } asked && asked != scope.AgentId)
            {
                throw new UnauthorizedAccessException("Чужой ТП недоступен: видны только свои точки.");
            }

            agentId = scope.AgentId ?? -1;
        }

        IReadOnlyList<long>? branches = null;
        if (scope.Level == SalesRowScope.RegionLevel)
        {
            if (regionId is { } r && !scope.RegionIds.Contains(r))
            {
                throw new UnauthorizedAccessException("Этот регион вне вашей области «Продаж».");
            }

            branches = scope.BranchIds;
        }

        if (regionId is { } region)
        {
            var own = context.BranchRegion.Where(x => x.Value == region).Select(x => x.Key).ToList();
            branches = branches is null ? own : branches.Intersect(own).ToList();
        }

        var start = from.AddDays(-LookbackDays);
        var visits = await VisitsAsync(start, to, agentId, branches, ct);
        var orders = await OrdersAsync(start, to, agentId, branches, ct);

        var byPair = new Dictionary<(long Agent, long Market), SortedDictionary<DateOnly, (bool Visited, bool Ordered)>>();
        var branchOf = new Dictionary<long, long?>();
        void Mark(long agent, long market, DateOnly day, long? branch, bool visited, bool ordered)
        {
            if (!byPair.TryGetValue((agent, market), out var days))
            {
                byPair[(agent, market)] = days = new SortedDictionary<DateOnly, (bool, bool)>();
            }

            var cur = days.GetValueOrDefault(day);
            days[day] = (cur.Visited || visited, cur.Ordered || ordered);
            branchOf.TryAdd(market, branch);
        }

        foreach (var v in visits)
        {
            Mark(v.Agent, v.Market, v.Day, v.Branch, true, false);
        }

        foreach (var o in orders)
        {
            Mark(o.Agent, o.Market, o.Day, o.Branch, false, true);
        }

        var total = new VisitSequenceCounter();
        var regions = new Dictionary<Guid, VisitSequenceCounter>();
        var agents = new Dictionary<long, VisitSequenceCounter>();
        var streaks = new List<(long Agent, long Market, Guid Region, int Visits, DateOnly LastVisit, DateOnly? LastOrder)>();
        foreach (var ((agent, market), days) in byPair)
        {
            var counter = new VisitSequenceCounter();
            var open = counter.Add(days.Select(d => new VisitDay(d.Key, d.Value.Visited, d.Value.Ordered)).ToList(), from);
            if (counter.Pairs == 0 && counter.OrdersWithoutVisit == 0)
            {
                continue;
            }

            total.Merge(counter);
            var regionKey = branchOf.GetValueOrDefault(market) is { } b && context.BranchRegion.TryGetValue(b, out var rid) ? rid : Guid.Empty;
            if (open > 0)
            {
                var lastOrder = days.LastOrDefault(d => d.Value.Ordered);
                streaks.Add((agent, market, regionKey, open, days.Last(d => d.Value.Visited).Key, lastOrder.Value.Ordered ? lastOrder.Key : null));
            }

            (regions.TryGetValue(regionKey, out var rc) ? rc : regions[regionKey] = new VisitSequenceCounter()).Merge(counter);
            (agents.TryGetValue(agent, out var ac) ? ac : agents[agent] = new VisitSequenceCounter()).Merge(counter);
        }

        var names = await db.LinkoUsers.AsNoTracking().Where(u => agents.Keys.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        string AgentName(long id) => names.TryGetValue(id, out var n) && !string.IsNullOrWhiteSpace(n) ? n : $"ТП {id}";
        string RegionName(Guid id) => id != Guid.Empty && context.Regions.TryGetValue(id, out var info) ? info.Name : "Без региона";
        var topStreaks = streaks.OrderByDescending(x => x.Visits).ThenByDescending(x => x.LastVisit).ThenBy(x => x.Agent).ThenBy(x => x.Market)
            .Take(OpenStreaksShown).ToList();
        var marketIds = topStreaks.Select(x => x.Market).Distinct().ToList();
        var markets = await db.LinkoMarkets.AsNoTracking().Where(m => marketIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Name, ct);
        var streakRows = topStreaks.Select(x => new VisitOpenStreak(x.Agent, AgentName(x.Agent), x.Market,
            markets.TryGetValue(x.Market, out var mn) && !string.IsNullOrWhiteSpace(mn) ? mn : $"Точка {x.Market}", RegionName(x.Region), x.Visits,
            x.LastVisit, x.LastOrder)).ToList();
        var regionRows = regions
            .Select(x => x.Value.Result(x.Key == Guid.Empty ? "none" : x.Key.ToString(), RegionName(x.Key)))
            .OrderByDescending(x => x.OrdersOnVisit).ThenBy(x => x.Name, StringComparer.CurrentCulture).ToList();
        var agentRows = agents
            .Select(x => x.Value.Result(x.Key.ToString(System.Globalization.CultureInfo.InvariantCulture), AgentName(x.Key)))
            .Where(x => x.Visits > 0)
            .OrderByDescending(x => x.OrdersOnVisit).ThenBy(x => x.Name, StringComparer.CurrentCulture).ToList();

        var notes = new List<string>();
        if (total.Visits == 0)
        {
            notes.Add("Выполненных визитов в периоде нет — проверьте период и загрузку визитов Linko.");
        }

        if (streaks.Count > OpenStreaksShown)
        {
            notes.Add($"Серии без заказа: показаны {OpenStreaksShown} самых длинных из {streaks.Count}.");
        }

        notes.Add("«Визит» — день, когда ТП отметил выполненный визит в точке (несколько отметок за день — один визит).");
        notes.Add($"Серия без заказа считает и визиты за {LookbackDays} дней до начала периода; «последнего заказа нет» — не было с {start:dd.MM.yyyy}.");
        return new VisitSequenceReport(from, to, LookbackDays, RuleText, total.Result("all", scope.Restricted ? scope.Description : "Вся организация"), regionRows,
            agentRows, streakRows, streaks.Count, notes);
    }

    private sealed record Row(long Agent, long Market, DateOnly Day, long? Branch);

    private async Task<List<Row>> VisitsAsync(DateOnly from, DateOnly to, long? agentId, IReadOnlyList<long>? branches, CancellationToken ct)
    {
        // Точки «Завода» и мерча — не вторичка: их заказы не считаются, значит и визиты туда не в счёте (иначе вечные «серии без заказа»).
        var excluded = options.NotSecondaryBranchesLower();
        var visits = db.LinkoVisits.AsNoTracking().Where(v => v.Status == "done" && v.Day >= from && v.Day <= to && v.UserId != null && v.MarketId != null);
        if (agentId is { } a)
        {
            visits = visits.Where(v => v.UserId == a);
        }

        var rows = from v in visits
                   join m in db.LinkoMarkets.AsNoTracking() on v.MarketId equals (long?)m.Id into mm
                   from m in mm.DefaultIfEmpty()
                   where m == null || !excluded.Contains((m.BranchName ?? "").ToLower())
                   select new { Agent = v.UserId!.Value, Market = v.MarketId!.Value, v.Day, Branch = m == null ? null : m.BranchId };
        if (branches is not null)
        {
            var list = branches.ToArray();
            rows = rows.Where(x => x.Branch != null && list.Contains(x.Branch.Value));
        }

        return (await rows.Distinct().ToListAsync(ct)).Select(x => new Row(x.Agent, x.Market, x.Day, x.Branch)).ToList();
    }

    private async Task<List<Row>> OrdersAsync(DateOnly from, DateOnly to, long? agentId, IReadOnlyList<long>? branches, CancellationToken ct)
    {
        var sold = options.SoldStatuses;
        var excluded = options.NotSecondaryBranchesLower();
        var orders = db.LinkoOrders.AsNoTracking().Where(o => sold.Contains(o.Status) && o.CreatedDate >= from && o.CreatedDate <= to && o.AgentId != null
            && o.MarketId != null && !excluded.Contains((o.BranchName ?? "").ToLower()));
        if (agentId is { } a)
        {
            orders = orders.Where(o => o.AgentId == a);
        }

        var rows = from o in orders
                   join m in db.LinkoMarkets.AsNoTracking() on o.MarketId equals (long?)m.Id into mm
                   from m in mm.DefaultIfEmpty()
                   select new { Agent = o.AgentId!.Value, Market = o.MarketId!.Value, Day = o.CreatedDate, Branch = m == null ? null : m.BranchId };
        if (branches is not null)
        {
            var list = branches.ToArray();
            rows = rows.Where(x => x.Branch != null && list.Contains(x.Branch.Value));
        }

        return (await rows.Distinct().ToListAsync(ct)).Select(x => new Row(x.Agent, x.Market, x.Day, x.Branch)).ToList();
    }
}
