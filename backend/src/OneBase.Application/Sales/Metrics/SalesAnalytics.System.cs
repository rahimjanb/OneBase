namespace OneBase.Application.Sales.Metrics;

/// <summary>
/// «Система», маршрут: плановые визиты по отчётный день (Plan), выполненные по плану и вне плана, визиты по плану с заказом (OrdersInPlan —
/// у этого ТП в этой точке в этот день есть заказ по дню ввода) и все заказы — те же правила, что «Маршрут» региона (RouteSplit).
/// </summary>
public sealed record SystemRouteRow(string Id, string Name, int Plan, int DoneInPlan, int DoneOffPlan, int OrdersInPlan, int Orders, IReadOnlyList<SystemRouteRow> Agents);

/// <summary>
/// «Система», качество дистрибуции области за месяц: АКБ и вес, SKU на точку, точки с одним SKU, качественные точки (не меньше CoreMin SKU
/// ядра), точки с двумя и больше категориями, верхние 20% точек по весу — их доля веса (TopShare), сколько их (TopCount) и сколько из них
/// брали каждый из прошлых месяцев окна ядра (TopRegular).
/// </summary>
public sealed record SystemQualityRow(
    string Id,
    string Name,
    int Akb,
    decimal Kg,
    decimal? SkuAvg,
    int OneSku,
    int Quality,
    int TwoCategories,
    decimal? TopShare,
    int TopCount,
    int TopRegular,
    IReadOnlyList<SystemQualityRow> Agents);

public sealed partial class SalesAnalytics
{
    /// <summary>Маршрут месяца по отчётный день: республика и регионы со строками ТП.</summary>
    public (SystemRouteRow Total, IReadOnlyList<SystemRouteRow> Regions) SystemRoute() => View("system:route", () =>
    {
        var (from, to) = ClipToMonth(_d.MonthStart, _d.DataThrough);
        var split = RouteSplit(from, to, withAgents: true);
        static SystemRouteRow Row(RouteSplitRow r, bool agents) => new(r.Id, r.Name, r.Plan, r.DoneInPlan, r.DoneOffPlan, r.VisitsWithOrderInPlan,
            r.Total.Orders, agents ? r.Agents.Select(a => Row(a, false)).ToList() : []);
        return Tuple.Create(Row(split.Total, false), (IReadOnlyList<SystemRouteRow>)split.Regions.Select(r => Row(r, true)).ToList());
    }).ToValueTuple();

    /// <summary>
    /// Строки факта месяца по отчётный день — те же, что «Маршрут» и «Продажи»: регион — продажи ТП его команды и продажи вне команды по его
    /// филиалу, у ТП — его строки.
    /// </summary>
    private IReadOnlyList<(Guid Id, string Name, List<SaleLine> Lines, List<(long Id, string Name, List<SaleLine> Lines)> Agents)> SystemTeams() => View("system:teams", () =>
    {
        var (from, to) = ClipToMonth(_d.MonthStart, _d.DataThrough);
        var counted = CountedAgents(null);
        return (IReadOnlyList<(Guid, string, List<SaleLine>, List<(long, string, List<SaleLine>)>)>)ScopeRegions(null, null, null)
            .Select(id =>
            {
                var (team, lines, _) = RegionLines(id, from, to, null, counted);
                return (id, _regions[id].Name, lines, team.Select(a => (a, AgentName(a), AgentLines(a, from, to).ToList())).ToList());
            })
            .ToList();
    });

    /// <summary>Вес товаров месяца по стране (строки регионов) — основа ядра ассортимента.</summary>
    public IReadOnlyDictionary<long, decimal> SystemProductKg() => View("system:product-kg", () =>
        (IReadOnlyDictionary<long, decimal>)SystemTeams().SelectMany(t => t.Lines).Where(l => l.ProductId != null)
            .GroupBy(l => l.ProductId!.Value).ToDictionary(g => g.Key, g => g.Sum(l => l.Kg)));

    /// <summary>Точки, купившие в месяце (чистый вес больше нуля) по стране.</summary>
    public IReadOnlySet<long> SystemBuyers() => View("system:buyers", () => (IReadOnlySet<long>)SalesMath.ActiveMarkets(SystemTeams().SelectMany(t => t.Lines)));

    /// <summary>Категория отчёта товара (null — тип вне категорий отчёта: бонус, оборудование) и её название.</summary>
    public (long Group, string Name)? SystemCategoryOf(long productId) =>
        _d.Products.TryGetValue(productId, out var p) && _cats.GroupOf(p.CategoryId) is { } g && SalesCategories.IsConfigured(g) ? (g, _cats.NameOf(g)) : null;

    public string ProductName(long productId) => _d.Products.TryGetValue(productId, out var p) ? p.Name : $"Товар {productId}";

    /// <summary>Вес регионов месяца по отчётный день (по названию региона) — вторичка для запаса дилера.</summary>
    public IReadOnlyDictionary<string, decimal> SystemRegionKg() => View("system:region-kg", () =>
        (IReadOnlyDictionary<string, decimal>)SystemTeams().ToDictionary(t => t.Name, t => t.Lines.Sum(l => l.Kg), StringComparer.OrdinalIgnoreCase));

    /// <summary>Качество дистрибуции месяца: республика и регионы со строками ТП. core — ядро, otherMonths — покупатели прошлых месяцев окна.</summary>
    public (SystemQualityRow Total, IReadOnlyList<SystemQualityRow> Regions) SystemQuality(IReadOnlySet<long> core, IReadOnlyList<IReadOnlySet<long>> otherMonths)
    {
        long? Group(long? product) => product is { } p ? SystemCategoryOf(p)?.Group : null;
        SystemQualityRow Row(string id, string name, IReadOnlyList<SaleLine> lines, IReadOnlyList<SystemQualityRow> agents) =>
            SystemMath.Quality(id, name, lines, core, otherMonths, Group, agents);

        var teams = SystemTeams();
        var regions = teams
            .Select(t => Row(t.Id.ToString(), t.Name, t.Lines, t.Agents.Select(a => Row(a.Id.ToString(), a.Name, a.Lines, [])).Where(a => a.Akb > 0).ToList()))
            .Where(r => r.Akb > 0)
            .ToList();
        return (Row("total", "Республика", teams.SelectMany(t => t.Lines).ToList(), []), regions);
    }
}
