namespace OneBase.Application.Sales.Metrics;

/// <summary>
/// Какие продажи смотреть: категории отчёта (группы SalesCategories), товары, регион продаж и ТП, за которым закреплена точка.
/// null — без ограничения.
/// </summary>
public sealed record CustomerLapseQuery(IReadOnlySet<long>? Groups, IReadOnlySet<long>? Products, Guid? Region, long? Agent)
{
    public string Key =>
        $"{(Groups is null ? "*" : string.Join(",", Groups.Order()))}:{(Products is null ? "*" : string.Join(",", Products.Order()))}:{Region}:{Agent}";
}

/// <summary>
/// Клиент (ТТ) в разрезе категории или товаров: покупки в базовом периоде и в месяце отчёта. Bought — в месяце отчёта чистые кг или
/// выручка по этим товарам больше нуля. OtherNowRevenue — выручка точки за месяц по остальным товарам (точка жива, но эти не взяла).
/// Визиты — выполненные визиты к точке в месяце отчёта (любым ТП).
/// </summary>
public sealed record CustomerLapseRow(
    long MarketId,
    string Name,
    string? RegionId,
    string? Region,
    long? AgentId,
    string? Agent,
    int Rank,
    decimal BaseKg,
    decimal BaseRevenue,
    int BaseMonths,
    DateOnly? LastPurchase,
    decimal NowKg,
    decimal NowRevenue,
    bool Bought,
    decimal OtherNowRevenue,
    int VisitsNow,
    DateOnly? LastVisit);

/// <summary>
/// «Кто не купил»: база — точки с покупкой этих товаров в базовом периоде (BaseFrom–BaseTo), строки — по убыванию кг базы (Rank — место).
/// NoData — в охвате за месяц отчёта нет ни одной покупки (дыра в выгрузке, а не разом замолчавшая база).
/// </summary>
public sealed record CustomerLapseView(
    PeriodInfo Period,
    DateOnly BaseFrom,
    DateOnly BaseTo,
    int BaseCustomers,
    decimal BaseKg,
    decimal BaseRevenue,
    int Bought,
    int NotBought,
    decimal NotBoughtBaseKg,
    decimal NotBoughtBaseRevenue,
    int NotBoughtActive,
    int NewCustomers,
    bool NoData,
    IReadOnlyList<CustomerLapseRow> Rows);

public sealed partial class SalesAnalytics
{
    /// <summary>
    /// Клиенты категории или товаров: кто покупал в базовом периоде и купил ли в месяце отчёта — по тем же строкам вторички, что страницы
    /// продаж (без точек-каналов: базар и сети в АКБ не входят). База — прошлый месяц; earlier — отчёт месяца на два раньше: его месяц
    /// (earlierMonths = 1) и ещё прошлый (2) добавляются к базе. Регион ограничивает базу продажами региона; покупка в месяце отчёта
    /// засчитывается в любом регионе. Точка закреплена за ТП, который продал ей больше всех в базе (нет продаж с ТП — ответственный в Linko).
    /// </summary>
    public CustomerLapseView CustomerLapse(CustomerLapseQuery query, SalesAnalytics? earlier = null, int earlierMonths = 0)
    {
        var depth = earlier is null ? 0 : Math.Clamp(earlierMonths, 0, 2);
        return View($"lapse:{query.Key}:{(depth == 0 ? "" : $"{earlier!._d.Year}-{earlier._d.Month}:{depth}")}", () => BuildLapse(query, depth == 0 ? null : earlier, depth));
    }

    /// <summary>Товары справочника в этих категориях отчёта (группах SalesCategories).</summary>
    public IReadOnlySet<long> ProductsIn(IReadOnlySet<long> groups) =>
        _d.Products.Values.Where(x => GroupOf(x.CategoryId) is { } g && groups.Contains(g)).Select(x => x.Id).ToHashSet();

    /// <summary>Товары справочника с этими типами Linko (по названию типа, без учёта регистра).</summary>
    public IReadOnlySet<long> ProductsOfTypes(IReadOnlyCollection<string> typeNames)
    {
        var types = _d.Categories.Where(t => typeNames.Contains(t.Value, StringComparer.OrdinalIgnoreCase)).Select(t => t.Key).ToHashSet();
        return _d.Products.Values.Where(x => x.CategoryId is { } c && types.Contains(c)).Select(x => x.Id).ToHashSet();
    }

    private CustomerLapseView BuildLapse(CustomerLapseQuery query, SalesAnalytics? earlier, int depth)
    {
        bool Match(SaleLine l) =>
            l.MarketId != null
            && (query.Groups is null || (GroupOf(l.CategoryId) is { } g && query.Groups.Contains(g)))
            && (query.Products is null || (l.ProductId is { } p && query.Products.Contains(p)))
            && !IsChannelRegion(RegionOf(l));

        IEnumerable<SaleLine> baseSource = _d.Previous;
        var baseFrom = _previousStart;
        if (earlier is not null)
        {
            baseSource = baseSource.Concat(earlier._d.Current);
            baseFrom = earlier._d.MonthStart;
            if (depth > 1)
            {
                baseSource = baseSource.Concat(earlier._d.Previous);
                baseFrom = earlier._previousStart;
            }
        }

        var baseLines = baseSource.Where(l => Match(l) && (query.Region is null || RegionOf(l) == query.Region)).ToList();
        var nowLines = _d.Current.Where(Match).ToList();
        var owners = OwnerOf(baseLines.Where(l => l.AgentId != null));
        long? OwnerOfMarket(long market) =>
            owners.TryGetValue(market, out var a) ? a : _d.Markets.TryGetValue(market, out var m) ? m.ResponsibleAgentId : null;

        var now = nowLines.GroupBy(l => l.MarketId!.Value).ToDictionary(g => g.Key, g => (Kg: g.Sum(l => l.Kg), Revenue: g.Sum(l => l.Revenue)));
        var allNow = View("lapse:revenue-now", () => _d.Current.Where(l => l.MarketId != null)
            .GroupBy(l => l.MarketId!.Value).ToDictionary(g => g.Key, g => g.Sum(l => l.Revenue)));
        var visits = View("lapse:visits-now", () => _d.Visits.Where(v => v.Status == VisitStatus.Done)
            .GroupBy(v => v.MarketId).ToDictionary(g => g.Key, g => (Count: g.Count(), Last: g.Max(v => v.Date))));

        var rows = baseLines
            .GroupBy(l => l.MarketId!.Value)
            .Select(g =>
            {
                var kg = g.Sum(l => l.Kg);
                var revenue = g.Sum(l => l.Revenue);
                var months = g.GroupBy(l => (l.Date.Year, l.Date.Month)).Count(m => SalesMath.Positive(m.Sum(l => l.Kg), m.Sum(l => l.Revenue)));
                var last = g.Where(l => l.OrderId != null && l.Kg > 0).Select(l => (DateOnly?)l.Date).Max();
                var region = g.GroupBy(RegionOf).MaxBy(r => r.Sum(l => l.Revenue))!.Key;
                return (Market: g.Key, Kg: kg, Revenue: revenue, Months: months, Last: last, Region: region);
            })
            .Where(x => SalesMath.Positive(x.Kg, x.Revenue))
            .Select(x => (Base: x, Agent: OwnerOfMarket(x.Market)))
            .Where(x => query.Agent is null || x.Agent == query.Agent)
            .OrderByDescending(x => x.Base.Kg)
            .ThenByDescending(x => x.Base.Revenue)
            .ThenBy(x => x.Base.Market) // равные объёмы — всегда в одном порядке: «топ-20» не меняется от перезапуска
            .Select((x, i) =>
            {
                var (nowKg, nowRevenue) = now.GetValueOrDefault(x.Base.Market);
                var bought = SalesMath.Positive(nowKg, nowRevenue);
                var other = allNow.GetValueOrDefault(x.Base.Market) - nowRevenue;
                var visit = visits.GetValueOrDefault(x.Base.Market);
                return new CustomerLapseRow(
                    x.Base.Market,
                    MarketName(x.Base.Market),
                    x.Base.Region == NoRegionId ? null : x.Base.Region.ToString(),
                    x.Base.Region == NoRegionId ? null : _regions[x.Base.Region].Name,
                    x.Agent,
                    x.Agent is { } a ? AgentName(a) : null,
                    i + 1,
                    x.Base.Kg,
                    x.Base.Revenue,
                    x.Base.Months,
                    x.Base.Last,
                    nowKg,
                    nowRevenue,
                    bought,
                    Math.Max(0, other),
                    visit.Count,
                    visit.Count > 0 ? visit.Last : null);
            })
            .ToList();

        // Новые: купили эти товары в месяце отчёта (в охвате), а в базе не покупали.
        var baseMarkets = rows.Select(r => r.MarketId).ToHashSet();
        var nowInScope = nowLines.Where(l => query.Region is null || RegionOf(l) == query.Region).ToList();
        var nowOwners = OwnerOf(nowInScope.Where(l => l.AgentId != null));
        var newCustomers = SalesMath.ActiveMarkets(nowInScope)
            .Count(m => !baseMarkets.Contains(m) && (query.Agent is null || (nowOwners.TryGetValue(m, out var a) ? a : (long?)null) == query.Agent));

        var lost = rows.Where(r => !r.Bought).ToList();
        var noData = query.Region is { } scope ? !_currentByRegion[scope].Any() : _d.Current.Count == 0;
        return new CustomerLapseView(
            Period,
            baseFrom,
            _d.MonthStart.AddDays(-1),
            rows.Count,
            rows.Sum(r => r.BaseKg),
            rows.Sum(r => r.BaseRevenue),
            rows.Count - lost.Count,
            lost.Count,
            lost.Sum(r => r.BaseKg),
            lost.Sum(r => r.BaseRevenue),
            lost.Count(r => r.OtherNowRevenue > 0),
            newCustomers,
            noData,
            rows);
    }
}
