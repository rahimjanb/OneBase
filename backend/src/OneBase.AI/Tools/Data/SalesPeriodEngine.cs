using OneBase.Application.Sales.Metrics;

namespace OneBase.AI.Tools.Data;

/// <summary>Область продаж: ТП, иначе регион, иначе направление РМ, иначе республика.</summary>
public sealed record SalesScope(Guid? Region = null, Guid? Direction = null, long? Agent = null)
{
    public bool Republic => Region is null && Direction is null && Agent is null;
}

/// <summary>Слагаемые продаж части периода: заказы, выручка (сум), кг (заказы − возвраты), возвраты (кг). АКБ — отдельно: она не складывается.</summary>
public sealed record SalesFigures(int Orders, decimal Revenue, decimal Kg, decimal ReturnsKg)
{
    public static readonly SalesFigures Zero = new(0, 0, 0, 0);

    public SalesFigures Add(SalesFigures other) => new(Orders + other.Orders, Revenue + other.Revenue, Kg + other.Kg, ReturnsKg + other.ReturnsKg);
}

public sealed record SalesRowFigures(string Id, string Name, string? Region, SalesFigures Figures);

public sealed record SalesDayFigures(DateOnly Date, decimal Kg, decimal Revenue, int Orders, int Akb);

/// <summary>Продажи отрезка внутри одного месяца: итог, каждый день, строки регионов и ТП.</summary>
public sealed record SalesSlice(SalesFigures Total, IReadOnlyList<SalesDayFigures> Days, IReadOnlyList<SalesRowFigures> Regions, IReadOnlyList<SalesRowFigures> Agents);

/// <summary>
/// Месяц продаж для расчёта периода: по чьим правилам считать отрезок и вес по точкам. В OneBase — SalesAnalytics месяца (AnalyticsMonth),
/// в тестах — подставной месяц.
/// </summary>
public interface ISalesMonth
{
    /// <summary>По какой день есть данные (сегодня — неполный день на время синхронизации Linko).</summary>
    DateOnly DataThrough { get; }

    SalesSlice Slice(DateOnly from, DateOnly to, SalesScope scope, bool agents);

    /// <summary>Чистый вес по точкам (MarketId → кг) — для АКБ через границу месяца.</summary>
    IReadOnlyDictionary<long, decimal> Markets(DateOnly from, DateOnly to, SalesScope scope);
}

/// <summary>Часть периода: месяц или неделя. Missing — данных за неё нет (месяц не загружен или ещё не наступил); Partial — данные не до её конца.</summary>
public sealed record PeriodPart(DateRange Range, SalesFigures Figures, int Akb, bool Partial, bool Missing);

public sealed record PeriodRow(string Id, string Name, string? Region, SalesFigures Figures, int Akb);

/// <summary>
/// Продажи за период: итог (АКБ — точки с чистой покупкой за весь период), месяцы, недели, дни, регионы, ТП и покрытие — какие части без
/// данных, по какой день данные, полный ли период.
/// </summary>
public sealed record PeriodSales(
    DateRange Range,
    IReadOnlyList<DateRange> Missing,
    DateOnly? DataThrough,
    bool Complete,
    SalesFigures Total,
    int Akb,
    IReadOnlyList<PeriodPart> Months,
    IReadOnlyList<PeriodPart> Weeks,
    IReadOnlyList<SalesDayFigures> Days,
    IReadOnlyList<PeriodRow> Regions,
    IReadOnlyList<PeriodRow> Agents);

/// <summary>
/// Продажи за любой период до года — из расчётов месяцев, по тем же правилам, что «Продажи» (SalesAnalytics.RouteSplit): период режется по
/// месяцам подряд, без пересечений и пропусков (PeriodRanges.EnsureContiguous), заказы, выручка, кг и возвраты частей складываются, а АКБ —
/// не сумма: веса точек частей складываются, и АКБ — точки с суммой больше нуля. Так же — недели через границу месяца и строки регионов и ТП.
/// Месяцы без данных и будущие дни — в Missing, период тогда неполный.
/// </summary>
public static class SalesPeriodEngine
{
    public static async Task<PeriodSales> AggregateAsync(DateRange range, SalesScope scope, Func<int, int, CancellationToken, Task<ISalesMonth?>> month,
        bool regions, bool agents, CancellationToken ct)
    {
        var parts = PeriodRanges.ByMonth(range);
        PeriodRanges.EnsureContiguous(parts, range);

        var months = new Dictionary<(int, int), ISalesMonth?>();
        var covered = new List<(DateRange Part, ISalesMonth Month)>();
        var missing = new List<DateRange>();
        var monthParts = new List<PeriodPart>();
        var total = SalesFigures.Zero;
        var markets = new Dictionary<long, decimal>();
        var days = new List<SalesDayFigures>();
        var regionRows = new Dictionary<string, RowTotals>();
        var agentRows = new Dictionary<string, RowTotals>();
        DateOnly? dataThrough = null;

        foreach (var part in parts)
        {
            var key = (part.From.Year, part.From.Month);
            if (!months.TryGetValue(key, out var m))
            {
                m = await month(part.From.Year, part.From.Month, ct);
                months[key] = m;
            }

            var to = m is null ? part.From.AddDays(-1) : part.To < m.DataThrough ? part.To : m.DataThrough;
            if (m is null || to < part.From)
            {
                missing.Add(part);
                monthParts.Add(new PeriodPart(part, SalesFigures.Zero, 0, false, true));
                continue;
            }

            if (to < part.To)
            {
                missing.Add(new DateRange(to.AddDays(1), part.To));
            }

            dataThrough = to;
            covered.Add((new DateRange(part.From, to), m));
            var slice = m.Slice(part.From, to, scope, agents || scope.Region is not null || scope.Agent is not null);
            total = total.Add(slice.Total);
            var partMarkets = m.Markets(part.From, to, scope);
            Merge(markets, partMarkets);
            monthParts.Add(new PeriodPart(part, slice.Total, Akb(partMarkets), to < part.To, false));
            days.AddRange(slice.Days);

            // Строки регионов — у республики и направления (у региона и ТП строка региона — это вся область, не регион целиком).
            if (regions && scope.Region is null && scope.Agent is null)
            {
                foreach (var r in slice.Regions.Where(r => Guid.TryParse(r.Id, out _)))
                {
                    var row = regionRows.TryGetValue(r.Id, out var known) ? known : regionRows[r.Id] = new RowTotals(r.Name, null);
                    row.Add(r.Figures, m.Markets(part.From, to, new SalesScope(Region: Guid.Parse(r.Id))));
                }
            }

            foreach (var a in slice.Agents.Where(a => long.TryParse(a.Id, out _)))
            {
                var row = agentRows.TryGetValue(a.Id, out var known) ? known : agentRows[a.Id] = new RowTotals(a.Name, a.Region);
                row.Add(a.Figures, m.Markets(part.From, to, new SalesScope(Agent: long.Parse(a.Id))));
            }
        }

        // Недели — по дням (кг, выручка, заказы складываются), АКБ недели — по весу точек её частей в каждом месяце. Полнота недели — по дням
        // с данными (части месяцев до DataThrough), а не по дням с продажами: неделя без продаж при данных — не «без данных».
        var byDay = days.ToDictionary(d => d.Date);
        var weeks = new List<PeriodPart>();
        foreach (var week in PeriodRanges.ByWeek(range))
        {
            var weekDays = Enumerable.Range(0, week.Days).Select(i => week.From.AddDays(i)).Where(byDay.ContainsKey).Select(d => byDay[d]).ToList();
            var weekMarkets = new Dictionary<long, decimal>();
            var withData = 0;
            foreach (var (part, m) in covered)
            {
                var from = week.From > part.From ? week.From : part.From;
                var to = week.To < part.To ? week.To : part.To;
                if (from <= to)
                {
                    withData += to.DayNumber - from.DayNumber + 1;
                    Merge(weekMarkets, m.Markets(from, to, scope));
                }
            }

            var figures = new SalesFigures(weekDays.Sum(d => d.Orders), weekDays.Sum(d => d.Revenue), weekDays.Sum(d => d.Kg), 0);
            weeks.Add(new PeriodPart(week, figures, Akb(weekMarkets), withData > 0 && withData < week.Days, withData == 0));
        }

        return new PeriodSales(
            range,
            missing,
            dataThrough,
            missing.Count == 0,
            total,
            Akb(markets),
            monthParts,
            weeks,
            days,
            regionRows.Select(p => p.Value.Row(p.Key)).OrderByDescending(r => r.Figures.Revenue).ToList(),
            agentRows.Select(p => p.Value.Row(p.Key)).OrderByDescending(r => r.Figures.Revenue).ToList());
    }

    /// <summary>Строка региона или ТП за период: слагаемые и вес по точкам всех частей.</summary>
    private sealed class RowTotals(string name, string? region)
    {
        private readonly Dictionary<long, decimal> _markets = new();
        private SalesFigures _figures = SalesFigures.Zero;

        public void Add(SalesFigures figures, IReadOnlyDictionary<long, decimal> markets)
        {
            _figures = _figures.Add(figures);
            Merge(_markets, markets);
        }

        public PeriodRow Row(string id) => new(id, name, region, _figures, Akb(_markets));
    }

    /// <summary>АКБ — точки с суммой чистого веса больше нуля.</summary>
    public static int Akb(IReadOnlyDictionary<long, decimal> markets) => markets.Count(p => p.Value > 0);

    private static void Merge(Dictionary<long, decimal> into, IReadOnlyDictionary<long, decimal> part)
    {
        foreach (var (market, kg) in part)
        {
            into[market] = into.GetValueOrDefault(market) + kg;
        }
    }
}

/// <summary>Месяц продаж OneBase: отрезок и вес по точкам — SalesAnalytics месяца (те же расчёты, что страницы «Продаж»).</summary>
internal sealed class AnalyticsMonth(SalesAnalytics analytics) : ISalesMonth
{
    public DateOnly DataThrough => analytics.Period.DataThrough;

    public SalesSlice Slice(DateOnly from, DateOnly to, SalesScope scope, bool agents)
    {
        var view = analytics.RouteSplit(from, to, scope.Region, scope.Direction, scope.Agent, agents);
        static SalesFigures Figures(RouteSplitRow r) => new(r.Total.Orders, r.Total.Revenue, r.Total.Kg, r.ReturnsKg);
        return new SalesSlice(
            Figures(view.Total),
            view.Days.Select(d => new SalesDayFigures(d.Date, d.Kg, d.Revenue, d.Orders, d.Akb)).ToList(),
            view.Regions.Select(r => new SalesRowFigures(r.Id, r.Name, null, Figures(r))).ToList(),
            view.Regions.SelectMany(r => r.Agents.Select(a => new SalesRowFigures(a.Id, a.Name, r.Name, Figures(a)))).ToList());
    }

    public IReadOnlyDictionary<long, decimal> Markets(DateOnly from, DateOnly to, SalesScope scope) =>
        analytics.MarketKg(from, to, scope.Region, scope.Direction, scope.Agent);
}
