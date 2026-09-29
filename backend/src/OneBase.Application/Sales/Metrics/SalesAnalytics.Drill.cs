namespace OneBase.Application.Sales.Metrics;

// Уровни и блоки из «Полевого контроля»: магазин, ассортимент агента, экспорт («Завод»), вкладка «Ассортимент».

public sealed record StoreCategoryRow(string Name, decimal Kg, decimal Revenue, decimal? Share);

public sealed record StoreProductRow(long ProductId, string Name, string? Code, string Category, decimal Kg, decimal Revenue);

/// <summary>Магазин (торговая точка) за месяц: что и на сколько ему продали.</summary>
public sealed record StoreView(
    PeriodInfo Period,
    long MarketId,
    string Name,
    string? RegionId,
    string? RegionName,
    long? AgentId,
    string? AgentName,
    decimal FactKg,
    decimal Revenue,
    int Orders,
    int Categories,
    int Positions,
    decimal? ShareOfAgent,
    decimal PrevMonthKg,
    decimal PrevMonthRevenue,
    IReadOnlyList<StoreCategoryRow> CategoryRows,
    IReadOnlyList<StoreProductRow> Products,
    IReadOnlyList<string> Agents);

public sealed record AgentStoreRow(long MarketId, string Name, decimal Kg, decimal Revenue, int Categories, int Positions, decimal? Share);

/// <summary>Товар в наборе: вес, выручка, доля выручки, АКБ и дистрибуция (доля ТТ набора, купивших товар).</summary>
public sealed record ProductRow(long ProductId, string Name, string? Code, string Category, bool InReport, decimal Kg, decimal Revenue,
    decimal? Share, int Akb, decimal? Distribution);

/// <summary>Товар, который регион ставит шире, чем этот ТП.</summary>
public sealed record LaggingProductRow(long ProductId, string Name, string Category, int AgentAkb, decimal? AgentDistribution,
    decimal? RegionDistribution, decimal RegionRevenue);

public sealed record AgentAssortment(
    IReadOnlyList<CategoryCard> Categories,
    IReadOnlyList<AgentStoreRow> Stores,
    IReadOnlyList<ProductRow> Products,
    IReadOnlyList<LaggingProductRow> Lagging);

public sealed record ExportMarketRow(long MarketId, string Name, decimal Kg, decimal Revenue, int Orders, decimal PrevMonthKg);

public sealed record ExportAgentRow(long? AgentId, string Name, decimal Kg, decimal Revenue, int Markets);

/// <summary>Экспорт и опт — филиал «Завод» в Linko: отдельно от вторички.</summary>
public sealed record ExportView(
    PeriodInfo Period,
    ExcludedSummary? Summary,
    IReadOnlyList<CategoryCard> Categories,
    IReadOnlyList<ExportMarketRow> Markets,
    IReadOnlyList<ExportAgentRow> Agents,
    IReadOnlyList<ProductRow> Products);

public sealed record UnitRef(string Id, string Name);

public sealed record AssortmentRegionRow(string Id, string Name, decimal Kg, decimal Revenue, int SkuSelling, int SkuNotCarried, int SkuLost, int Akb);

public static class MatrixLevels
{
    /// <summary>Товара в регионе нет вовсе.</summary>
    public const string None = "none";

    /// <summary>Стоит меньше чем в 40% от своей средней дистрибуции.</summary>
    public const string Low = "low";

    public const string Ok = "ok";
}

public sealed record MatrixCell(string RegionId, decimal? Distribution, string Level);

public sealed record MatrixRow(long ProductId, string Name, string Category, decimal Revenue, decimal? AverageDistribution, IReadOnlyList<MatrixCell> Cells);

/// <summary>Вкладка «Ассортимент»: категории, АКБ по месяцам, регионы, товары и матрица «товар × регион».</summary>
public sealed record AssortmentView(
    PeriodInfo Period,
    string ScopeName,
    IReadOnlyList<CategoryCard> Categories,
    AkbByMonth AkbMonths,
    IReadOnlyList<AssortmentRegionRow> Regions,
    IReadOnlyList<ProductRow> Products,
    IReadOnlyList<UnitRef> MatrixRegions,
    IReadOnlyList<MatrixRow> Matrix,
    DataQualityView Quality);

public sealed partial class SalesAnalytics
{
    /// <summary>Порог «матрицы»: оранжевым — товар стоит меньше чем в этой доле от своей средней дистрибуции.</summary>
    private const decimal MatrixLowShare = 0.4m;

    public StoreView CachedStore(long marketId, long? agentId) => View($"store:{marketId}:{agentId}", () => Store(marketId, agentId));

    public ExportView CachedExport() => View("export", Export);

    public AssortmentView CachedAssortment(string? direction, Guid? region) =>
        View($"assortment:{direction}:{region}", () => Assortment(direction, region));

    public bool HasMarket(long id) => _d.Markets.ContainsKey(id) || _d.Current.Any(l => l.MarketId == id);

    public StoreView Store(long marketId, long? agentId)
    {
        var lines = _d.Current.Where(l => l.MarketId == marketId && (agentId is null || l.AgentId == agentId)).ToList();
        var previous = _d.Previous.Where(l => l.MarketId == marketId && (agentId is null || l.AgentId == agentId)).ToList();
        var regionId = _marketRegion.TryGetValue(marketId, out var r) ? r : lines.Select(RegionOf).FirstOrDefault(NoRegionId);
        var revenue = lines.Sum(l => l.Revenue);
        var agentRevenue = agentId is { } a ? _currentByAgent[a].Sum(l => l.Revenue) : (decimal?)null;

        var categories = lines.GroupBy(l => GroupOf(l.CategoryId))
            .Select(g => new StoreCategoryRow(_cats.NameOf(g.Key), g.Sum(l => l.Kg), g.Sum(l => l.Revenue), SalesMath.Ratio(g.Sum(l => l.Revenue), revenue)))
            .OrderByDescending(c => c.Revenue)
            .ToList();
        var products = lines.Where(l => l.ProductId != null).GroupBy(l => l.ProductId!.Value)
            .Select(g =>
            {
                var product = _d.Products.GetValueOrDefault(g.Key);
                return new StoreProductRow(g.Key, product?.Name ?? $"Товар {g.Key}", product?.Code, _cats.NameOf(GroupOf(product?.CategoryId)),
                    g.Sum(l => l.Kg), g.Sum(l => l.Revenue));
            })
            .OrderByDescending(p => p.Revenue)
            .ToList();

        return new StoreView(
            Period,
            marketId,
            MarketName(marketId),
            regionId == NoRegionId ? null : regionId.ToString(),
            _regions.TryGetValue(regionId, out var region) ? region.Name : null,
            agentId,
            agentId is { } id ? AgentName(id) : null,
            lines.Sum(l => l.Kg),
            revenue,
            SalesMath.OrderCount(lines),
            SalesMath.CategoryCount(lines, GroupOf),
            lines.Where(l => l.OrderId != null && l.ProductId != null).Select(l => l.ProductId).Distinct().Count(),
            agentRevenue is { } ar ? SalesMath.Ratio(revenue, ar) : null,
            previous.Sum(l => l.Kg),
            previous.Sum(l => l.Revenue),
            categories,
            products,
            lines.Where(l => l.AgentId != null).Select(l => AgentName(l.AgentId!.Value)).Distinct().Order().ToList());
    }

    /// <summary>Ассортимент агента: категории, магазины, товары и товары, которые регион ставит шире.</summary>
    private AgentAssortment AgentAssortmentOf(long agent, Guid regionId)
    {
        var lines = _currentByAgent[agent].ToList();
        var revenue = lines.Sum(l => l.Revenue);

        var stores = lines.Where(l => l.MarketId != null).GroupBy(l => l.MarketId!.Value)
            .Select(g => new AgentStoreRow(g.Key, MarketName(g.Key), g.Sum(l => l.Kg), g.Sum(l => l.Revenue), SalesMath.CategoryCount(g, GroupOf),
                g.Where(l => l.OrderId != null && l.ProductId != null).Select(l => l.ProductId).Distinct().Count(), SalesMath.Ratio(g.Sum(l => l.Revenue), revenue)))
            .OrderByDescending(s => s.Revenue)
            .ToList();

        var regionLines = _currentByRegion[regionId].ToList();
        var regionAkb = SalesMath.Akb(regionLines);
        var agentAkb = SalesMath.Akb(lines);
        var regionByProduct = regionLines.Where(l => l.ProductId != null).ToLookup(l => l.ProductId!.Value);
        var agentByProduct = lines.Where(l => l.ProductId != null).ToLookup(l => l.ProductId!.Value);

        var lagging = regionByProduct
            .Select(g =>
            {
                var regionDist = SalesMath.Ratio(SalesMath.Akb(g), regionAkb);
                var own = SalesMath.Akb(agentByProduct[g.Key]);
                return (Product: g.Key, RegionDist: regionDist, Own: own, OwnDist: SalesMath.Ratio(own, agentAkb), RegionRevenue: g.Sum(l => l.Revenue));
            })
            .Where(x => x.RegionDist >= 0.15m && agentAkb > 0 && (x.OwnDist ?? 0) < x.RegionDist!.Value / 2)
            .OrderByDescending(x => x.RegionRevenue)
            .Take(15)
            .Select(x =>
            {
                var product = _d.Products.GetValueOrDefault(x.Product);
                return new LaggingProductRow(x.Product, product?.Name ?? $"Товар {x.Product}", _cats.NameOf(GroupOf(product?.CategoryId)),
                    x.Own, x.OwnDist, x.RegionDist, x.RegionRevenue);
            })
            .ToList();

        return new AgentAssortment(CategoryCardsFor(lines, _previousByAgent[agent].ToList()), stores, ProductsOf(lines), lagging);
    }

    public ExportView Export()
    {
        var now = _d.ExcludedCurrent;
        var before = _d.ExcludedPrevious;

        var markets = now.Concat(before).Where(l => l.MarketId != null).GroupBy(l => l.MarketId!.Value)
            .Select(g => new ExportMarketRow(g.Key, MarketName(g.Key), now.Where(l => l.MarketId == g.Key).Sum(l => l.Kg),
                now.Where(l => l.MarketId == g.Key).Sum(l => l.Revenue), SalesMath.OrderCount(now.Where(l => l.MarketId == g.Key)),
                before.Where(l => l.MarketId == g.Key).Sum(l => l.Kg)))
            .OrderByDescending(m => m.Kg)
            .ToList();
        var agents = now.GroupBy(l => l.AgentId)
            .Select(g => new ExportAgentRow(g.Key, g.Key is { } a ? AgentName(a) : "Без агента", g.Sum(l => l.Kg), g.Sum(l => l.Revenue), SalesMath.Akb(g)))
            .OrderByDescending(a => a.Kg)
            .ToList();

        return new ExportView(Period, Excluded(), CategoryCardsFor(now, before), markets, agents, ProductsOf(now));
    }

    public AssortmentView Assortment(string? direction, Guid? region)
    {
        IReadOnlySet<Guid>? scope = region is { } r
            ? new HashSet<Guid> { r }
            : direction is { } d
                ? (DirectionGroups().FirstOrDefault(g => g.Id == d)?.Regions ?? []).ToHashSet()
                : null;
        var scopeName = region is { } rid && _regions.TryGetValue(rid, out var info) ? info.Name
            : direction is { } did ? DirectionGroups().FirstOrDefault(g => g.Id == did)?.Name ?? "Направление"
            : "Республика";

        var lines = Lines(scope).ToList();
        var categories = CategoryCardsFor(lines, scope is null ? _d.Previous : scope.SelectMany(x => _previousByRegion[x]).ToList());
        var products = ProductsOf(lines);

        static HashSet<long> Sold(IEnumerable<SaleLine> source) =>
            source.Where(l => l.ProductId != null).GroupBy(l => l.ProductId!.Value).Where(g => g.Sum(l => l.Revenue) > 0).Select(g => g.Key).ToHashSet();

        var universe = _d.ActiveSkus.Where(p => _d.Products.TryGetValue(p, out var info) && InReport(GroupOf(info.CategoryId))).ToHashSet();
        var regionIds = RegionIds(scope).Where(x => x != NoRegionId).ToList();
        var regionRows = regionIds
            .Select(id =>
            {
                var now = _currentByRegion[id].ToList();
                var before = _previousByRegion[id].ToList();
                var selling = Sold(now);
                var lost = Sold(before).Where(p => !selling.Contains(p)).Count();
                return new AssortmentRegionRow(id.ToString(), _regions[id].Name, now.Sum(l => l.Kg), now.Sum(l => l.Revenue),
                    selling.Count(universe.Contains), universe.Count(p => !selling.Contains(p)), lost, SalesMath.Akb(now));
            })
            .Where(x => x.Kg != 0 || x.Akb > 0)
            .OrderBy(x => x.Name)
            .ToList();

        // Матрица: топ товаров категорий отчёта по выручке × регионы с продажами.
        var matrixRegions = regionRows.Where(x => x.Akb > 0).Select(x => new UnitRef(x.Id, x.Name)).ToList();
        var regionAkb = matrixRegions.ToDictionary(x => x.Id, x => SalesMath.Akb(_currentByRegion[Guid.Parse(x.Id)]));
        var matrix = products.Where(p => p.InReport).OrderByDescending(p => p.Revenue).Take(25)
            .Select(p =>
            {
                var dist = matrixRegions.ToDictionary(x => x.Id, x =>
                    SalesMath.Ratio(SalesMath.Akb(_currentByRegion[Guid.Parse(x.Id)].Where(l => l.ProductId == p.ProductId)), regionAkb[x.Id]));
                var present = dist.Values.Where(v => v is > 0).Select(v => v!.Value).ToList();
                decimal? avg = present.Count == 0 ? null : present.Average();
                var cells = matrixRegions.Select(x =>
                {
                    var v = dist[x.Id];
                    var level = v is null or 0 ? MatrixLevels.None : avg is { } a && v < a * MatrixLowShare ? MatrixLevels.Low : MatrixLevels.Ok;
                    return new MatrixCell(x.Id, v, level);
                }).ToList();
                return new MatrixRow(p.ProductId, p.Name, p.Category, p.Revenue, avg, cells);
            })
            .ToList();

        return new AssortmentView(Period, scopeName, categories, AkbMonthsOf(scope, categories), regionRows, products, matrixRegions, matrix, QualityOf(scope));
    }

    /// <summary>Товары набора с долей выручки, АКБ и дистрибуцией (доля ТТ набора, купивших товар).</summary>
    private List<ProductRow> ProductsOf(IReadOnlyList<SaleLine> lines)
    {
        var revenue = lines.Sum(l => l.Revenue);
        var akb = SalesMath.Akb(lines);
        return lines.Where(l => l.ProductId != null).GroupBy(l => l.ProductId!.Value)
            .Select(g =>
            {
                var product = _d.Products.GetValueOrDefault(g.Key);
                var group = GroupOf(product?.CategoryId);
                var productAkb = SalesMath.Akb(g);
                return new ProductRow(g.Key, product?.Name ?? $"Товар {g.Key}", product?.Code, _cats.NameOf(group), InReport(group),
                    g.Sum(l => l.Kg), g.Sum(l => l.Revenue), SalesMath.Ratio(g.Sum(l => l.Revenue), revenue), productAkb, SalesMath.Ratio(productAkb, akb));
            })
            .Where(p => p.Kg != 0 || p.Revenue != 0)
            .OrderByDescending(p => p.Revenue)
            .ToList();
    }
}
