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
    OrderSizeReport OrderSize,
    IReadOnlyList<string> Notes);

/// <summary>
/// Серия без заказа: пара «ТП × точка», где после последнего заказа было Visits выполненных визитов подряд, а заказа нет (визиты за 60 дней до
/// периода тоже в счёте). LastOrder — день последнего заказа этого ТП в этой точке; null — заказа не было с начала окна (LookbackFrom).
/// </summary>
public sealed record VisitOpenStreak(long AgentId, string Agent, long MarketId, string Market, string Region, int Visits, DateOnly LastVisit, DateOnly? LastOrder);

/// <summary>Отметки пары «ТП × точка» по дням: был ли выполненный визит и был ли заказ этого ТП в этой точке (по дню ввода), кг заказов дня.</summary>
public sealed record VisitDay(DateOnly Day, bool Visited, bool Ordered, decimal Kg = 0);

/// <summary>
/// Что было после одного заказа пары «ТП × точка»: размер (кг) и во сколько раз он больше обычного заказа этой пары (Ratio; null — у пары
/// меньше трёх заказов с весом), сколько выполненных визитов до следующего заказа, был ли следующий заказ до конца периода (Closed), на
/// визите ли он, через сколько дней.
/// </summary>
public sealed record OrderCycle(DateOnly Start, decimal Kg, double? Ratio, int Visits, bool Closed, bool NextOnVisit, int? Days);

/// <summary>
/// Группа заказов по размеру и что было после них: заказов, сколько из них дождались следующего заказа в периоде (на визите и без), сколько
/// ещё ждут; конверсия визитов после заказа по номеру (1-й, 2-й… 5+), среднее и медиана визитов и дней до следующего заказа.
/// </summary>
public sealed record OrderSizeGroup(
    string Id,
    string Label,
    int Orders,
    double? AverageKg,
    int NextOrders,
    int NextOnVisit,
    int NextWithoutVisit,
    int Waiting,
    IReadOnlyList<VisitSequenceBucket> Buckets,
    double? AverageVisitsToNext,
    double? MedianVisitsToNext,
    double? NextOnFirstVisitShare,
    double? AverageDaysToNext,
    double? MedianDaysToNext);

/// <summary>
/// «Связь размера заказа и следующих заказов»: группы по размеру относительно обычного заказа пары (Relative) и по кг (Absolute, квартили),
/// ранговая корреляция Спирмена «во сколько раз больше обычного» ↔ визитов и ↔ дней до следующего заказа.
/// </summary>
public sealed record OrderSizeReport(
    string Rule,
    IReadOnlyList<OrderSizeGroup> Relative,
    IReadOnlyList<OrderSizeGroup> Absolute,
    int WithoutTypical,
    double? RatioVisitsSpearman,
    int RatioVisitsPairs,
    double? RatioDaysSpearman,
    int RatioDaysPairs);

/// <summary>Циклы «заказ → следующий заказ» пары (без базы — проверяется тестами).</summary>
public static class OrderCycles
{
    public const int MinOrdersForTypical = 3;

    /// <summary>
    /// days — по возрастанию дня. Цикл начинается каждым заказом в [from, to]; визиты после него (в день следующего заказа — тоже, если визит
    /// был) считаются до следующего заказа; нет следующего заказа до конца данных — цикл ждёт (Closed = false). Обычный заказ пары — медиана кг
    /// всех её заказов в загруженном окне.
    /// </summary>
    public static IReadOnlyList<OrderCycle> Of(IReadOnlyList<VisitDay> days, DateOnly from, DateOnly to)
    {
        var weights = days.Where(d => d.Ordered && d.Kg > 0).Select(d => d.Kg).Order().ToList();
        decimal? typical = weights.Count >= MinOrdersForTypical
            ? weights.Count % 2 == 1 ? weights[weights.Count / 2] : (weights[weights.Count / 2 - 1] + weights[weights.Count / 2]) / 2
            : null;
        double? Ratio(VisitDay d) => typical is > 0 && d.Kg > 0 ? (double)(d.Kg / typical.Value) : null;

        var cycles = new List<OrderCycle>();
        VisitDay? start = null;
        var k = 0;
        foreach (var d in days)
        {
            if (start is not null && d.Visited)
            {
                k++;
            }

            if (!d.Ordered)
            {
                continue;
            }

            if (start is not null)
            {
                cycles.Add(new OrderCycle(start.Day, start.Kg, Ratio(start), k, true, d.Visited, d.Day.DayNumber - start.Day.DayNumber));
            }

            start = d.Day >= from && d.Day <= to ? d : null;
            k = 0;
        }

        if (start is not null)
        {
            cycles.Add(new OrderCycle(start.Day, start.Kg, Ratio(start), k, false, false, null));
        }

        return cycles;
    }

    /// <summary>Группа циклов: конверсия визитов по номеру после заказа, визиты и дни до следующего заказа.</summary>
    public static OrderSizeGroup Group(string id, string label, IReadOnlyList<OrderCycle> cycles)
    {
        var orders = new int[VisitSequenceCounter.Buckets];
        var reached = new int[VisitSequenceCounter.Buckets];
        foreach (var c in cycles)
        {
            for (var v = 1; v <= c.Visits; v++)
            {
                reached[VisitSequenceCounter.Bucket(v)]++;
            }

            if (c.Closed && c.NextOnVisit && c.Visits > 0)
            {
                orders[VisitSequenceCounter.Bucket(c.Visits)]++;
            }
        }

        var onVisit = cycles.Where(c => c.Closed && c.NextOnVisit && c.Visits > 0).Select(c => (double)c.Visits).ToList();
        var days = cycles.Where(c => c.Closed && c.Days is not null).Select(c => (double)c.Days!.Value).ToList();
        var buckets = Enumerable.Range(0, VisitSequenceCounter.Buckets).Select(i => new VisitSequenceBucket(
            i == VisitSequenceCounter.Buckets - 1 ? $"{VisitSequenceCounter.Buckets}+" : (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
            orders[i], onVisit.Count == 0 ? null : (double)orders[i] / onVisit.Count, reached[i], reached[i] == 0 ? null : (double)orders[i] / reached[i])).ToList();
        var closed = cycles.Count(c => c.Closed);
        return new OrderSizeGroup(id, label, cycles.Count, cycles.Count == 0 ? null : (double)cycles.Average(c => c.Kg), closed,
            onVisit.Count, closed - onVisit.Count, cycles.Count - closed, buckets,
            onVisit.Count == 0 ? null : onVisit.Average(), Median(onVisit), onVisit.Count == 0 ? null : (double)orders[0] / onVisit.Count,
            days.Count == 0 ? null : days.Average(), Median(days));
    }

    public static readonly (double Below, string Id, string Label)[] RelativeBands =
    [
        (0.5, "lt_0_5", "меньше 0,5× обычного"),
        (0.8, "0_5_0_8", "0,5–0,8× обычного"),
        (1.25, "typical", "обычный (0,8–1,25×)"),
        (2.0, "1_25_2", "крупнее: 1,25–2× обычного"),
        (double.PositiveInfinity, "ge_2", "крупный: 2× обычного и больше"),
    ];

    public static OrderSizeReport Report(IReadOnlyList<OrderCycle> cycles)
    {
        var withRatio = cycles.Where(c => c.Ratio is not null).ToList();
        var relative = new List<OrderSizeGroup>();
        var lower = 0.0;
        foreach (var (below, id, label) in RelativeBands)
        {
            var low = lower;
            relative.Add(Group(id, label, withRatio.Where(c => c.Ratio >= low && c.Ratio < below).ToList()));
            lower = below;
        }

        var weighted = cycles.Where(c => c.Kg > 0).OrderBy(c => c.Kg).ToList();
        var absolute = new List<OrderSizeGroup>();
        if (weighted.Count >= 4)
        {
            var bounds = new[] { 0.25, 0.5, 0.75 }.Select(q => weighted[(int)Math.Floor(q * (weighted.Count - 1))].Kg).ToArray();
            static string Kg(decimal v) => Math.Round(v, v < 10 ? 1 : 0).ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo("ru-RU"));
            absolute.Add(Group("q1", $"до {Kg(bounds[0])} кг (мелкие 25%)", weighted.Where(c => c.Kg <= bounds[0]).ToList()));
            absolute.Add(Group("q2", $"{Kg(bounds[0])}–{Kg(bounds[1])} кг", weighted.Where(c => c.Kg > bounds[0] && c.Kg <= bounds[1]).ToList()));
            absolute.Add(Group("q3", $"{Kg(bounds[1])}–{Kg(bounds[2])} кг", weighted.Where(c => c.Kg > bounds[1] && c.Kg <= bounds[2]).ToList()));
            absolute.Add(Group("q4", $"больше {Kg(bounds[2])} кг (крупные 25%)", weighted.Where(c => c.Kg > bounds[2]).ToList()));
        }

        var visits = withRatio.Where(c => c.Closed && c.NextOnVisit && c.Visits > 0).ToList();
        var days = withRatio.Where(c => c.Closed && c.Days is not null).ToList();
        return new OrderSizeReport(
            "Каждый заказ пары «ТП × точка» в периоде начинает цикл: визиты этого ТП в эту точку после заказа считаются до следующего заказа (заказ в " +
            "день визита — «на k-м визите после заказа»). Размер — кг заказов дня; «обычный» — медиана кг заказов этой пары за период и 60 дней до " +
            "него (нужно не меньше 3 заказов). Waiting — следующего заказа до конца периода ещё нет (у заказов в конце периода цикл не успел закончиться).",
            relative, absolute, cycles.Count - withRatio.Count,
            Spearman(visits.Select(c => c.Ratio!.Value).ToList(), visits.Select(c => (double)c.Visits).ToList()), visits.Count,
            Spearman(days.Select(c => c.Ratio!.Value).ToList(), days.Select(c => (double)c.Days!.Value).ToList()), days.Count);
    }

    private static double? Median(List<double> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        var sorted = values.Order().ToList();
        return sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2;
    }

    /// <summary>Ранговая корреляция Спирмена (средние ранги при равных значениях); меньше 3 пар или нет разброса — null.</summary>
    public static double? Spearman(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        if (x.Count != y.Count || x.Count < 3)
        {
            return null;
        }

        var rx = Ranks(x);
        var ry = Ranks(y);
        var mx = rx.Average();
        var my = ry.Average();
        double sxy = 0, sxx = 0, syy = 0;
        for (var i = 0; i < rx.Length; i++)
        {
            sxy += (rx[i] - mx) * (ry[i] - my);
            sxx += (rx[i] - mx) * (rx[i] - mx);
            syy += (ry[i] - my) * (ry[i] - my);
        }

        return sxx == 0 || syy == 0 ? null : sxy / Math.Sqrt(sxx * syy);
    }

    private static double[] Ranks(IReadOnlyList<double> values)
    {
        var order = Enumerable.Range(0, values.Count).OrderBy(i => values[i]).ToArray();
        var ranks = new double[values.Count];
        var i = 0;
        while (i < order.Length)
        {
            var j = i;
            while (j + 1 < order.Length && values[order[j + 1]] == values[order[i]])
            {
                j++;
            }

            var rank = (i + j) / 2.0 + 1;
            for (var t = i; t <= j; t++)
            {
                ranks[order[t]] = rank;
            }

            i = j + 1;
        }

        return ranks;
    }
}

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

        var byPair = new Dictionary<(long Agent, long Market), SortedDictionary<DateOnly, (bool Visited, bool Ordered, decimal Kg)>>();
        var branchOf = new Dictionary<long, long?>();
        void Mark(long agent, long market, DateOnly day, long? branch, bool visited, bool ordered, decimal kg)
        {
            if (!byPair.TryGetValue((agent, market), out var days))
            {
                byPair[(agent, market)] = days = new SortedDictionary<DateOnly, (bool, bool, decimal)>();
            }

            var cur = days.GetValueOrDefault(day);
            days[day] = (cur.Visited || visited, cur.Ordered || ordered, cur.Kg + kg);
            branchOf.TryAdd(market, branch);
        }

        foreach (var v in visits)
        {
            Mark(v.Agent, v.Market, v.Day, v.Branch, true, false, 0);
        }

        foreach (var o in orders)
        {
            Mark(o.Agent, o.Market, o.Day, o.Branch, false, true, o.Kg);
        }

        var total = new VisitSequenceCounter();
        var regions = new Dictionary<Guid, VisitSequenceCounter>();
        var agents = new Dictionary<long, VisitSequenceCounter>();
        var streaks = new List<(long Agent, long Market, Guid Region, int Visits, DateOnly LastVisit, DateOnly? LastOrder)>();
        var cycles = new List<OrderCycle>();
        foreach (var ((agent, market), days) in byPair)
        {
            var counter = new VisitSequenceCounter();
            var series = days.Select(d => new VisitDay(d.Key, d.Value.Visited, d.Value.Ordered, d.Value.Kg)).ToList();
            var open = counter.Add(series, from);
            cycles.AddRange(OrderCycles.Of(series, from, to));
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
            agentRows, streakRows, streaks.Count, OrderCycles.Report(cycles), notes);
    }

    private sealed record Row(long Agent, long Market, DateOnly Day, long? Branch, decimal Kg = 0);

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
                   select new { Agent = o.AgentId!.Value, Market = o.MarketId!.Value, Day = o.CreatedDate, Branch = m == null ? null : m.BranchId, Kg = o.TotalWeight };
        if (branches is not null)
        {
            var list = branches.ToArray();
            rows = rows.Where(x => x.Branch != null && list.Contains(x.Branch.Value));
        }

        // Заказы пары за день — одной строкой с суммой кг (размер заказа дня).
        var grouped = rows.GroupBy(x => new { x.Agent, x.Market, x.Day, x.Branch })
            .Select(g => new { g.Key.Agent, g.Key.Market, g.Key.Day, g.Key.Branch, Kg = g.Sum(x => x.Kg) });
        return (await grouped.ToListAsync(ct)).Select(x => new Row(x.Agent, x.Market, x.Day, x.Branch, x.Kg)).ToList();
    }
}
