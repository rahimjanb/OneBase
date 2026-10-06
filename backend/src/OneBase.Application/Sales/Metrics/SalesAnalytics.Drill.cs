namespace OneBase.Application.Sales.Metrics;

// Уровни и блоки из «Полевого контроля»: магазин, ассортимент агента, экспорт («Завод»), вкладка «Ассортимент».

public sealed record StoreCategoryRow(string Name, decimal Kg, decimal Revenue, decimal? Share);

/// <summary>Товар в магазине за месяц; IsTop — товар из списка ТОП.</summary>
public sealed record StoreProductRow(long ProductId, string Name, string? Code, string Category, decimal Kg, decimal Revenue, bool IsTop = false);

/// <summary>Магазин (торговая точка) за месяц: что и на сколько ему продали. ShareOfAgent — «доля в объёме ТП»: кг магазина ÷ кг ТП.</summary>
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

/// <summary>
/// Товар категорий отчёта в наборе: вес, выручка, доля выручки (от выручки товаров категорий отчёта в наборе), Akb — ТТ артикула
/// (положительная строка, SalesMath.SkuTt) и дистрибуция — их доля от АКБ набора. IsTop — товар из списка ТОП.
/// </summary>
public sealed record ProductRow(long ProductId, string Name, string? Code, string Category, bool InReport, decimal Kg, decimal Revenue,
    decimal? Share, int Akb, decimal? Distribution, bool IsTop = false);

/// <summary>Товар, который регион ставит шире, чем этот ТП.</summary>
public sealed record LaggingProductRow(long ProductId, string Name, string Category, int AgentAkb, decimal? AgentDistribution,
    decimal? RegionDistribution, decimal RegionRevenue);

/// <summary>Ассортимент ТП; ProductsOutsideReport — товаров вне категорий отчёта (бонус, подарки), в «Товары» они не входят.</summary>
public sealed record AgentAssortment(
    IReadOnlyList<CategoryCard> Categories,
    IReadOnlyList<AgentStoreRow> Stores,
    IReadOnlyList<ProductRow> Products,
    IReadOnlyList<LaggingProductRow> Lagging,
    int ProductsOutsideReport = 0);

public sealed record ExportMarketRow(long MarketId, string Name, decimal Kg, decimal Revenue, int Orders, decimal PrevMonthKg);

public sealed record ExportAgentRow(long? AgentId, string Name, decimal Kg, decimal Revenue, int Markets);

/// <summary>Экспорт и опт — филиал «Завод» в Linko: отдельно от вторички. ProductsOutsideReport — товаров вне категорий отчёта.</summary>
public sealed record ExportView(
    PeriodInfo Period,
    ExcludedSummary? Summary,
    IReadOnlyList<CategoryCard> Categories,
    IReadOnlyList<ExportMarketRow> Markets,
    IReadOnlyList<ExportAgentRow> Agents,
    IReadOnlyList<ProductRow> Products,
    IReadOnlyList<CurrencyTotal> OtherCurrency,
    int ProductsOutsideReport = 0);

public sealed record UnitRef(string Id, string Name);

/// <summary>Регион в «По регионам»; NoData — в регионе за месяц нет ни одной покупки («нет данных», счётчики SKU — нули).</summary>
public sealed record AssortmentRegionRow(string Id, string Name, decimal Kg, decimal Revenue, int SkuSelling, int SkuNotCarried, int SkuLost, int Akb,
    bool NoData = false);

public static class MatrixLevels
{
    /// <summary>Товара в регионе нет вовсе.</summary>
    public const string None = "none";

    /// <summary>Стоит меньше чем в 40% от своей средней дистрибуции.</summary>
    public const string Low = "low";

    public const string Ok = "ok";
}

/// <summary>Клетка матрицы: дистрибуция товара в регионе (Tt — ТТ с положительной строкой товара ÷ АКБ региона) и её цвет (MatrixLevels).</summary>
public sealed record MatrixCell(string RegionId, decimal? Distribution, string Level, int Tt = 0);

/// <summary>Строка матрицы; AverageDistribution — дистрибуция по всему охвату: Σ ТТ с товаром ÷ Σ АКБ регионов матрицы.</summary>
public sealed record MatrixRow(long ProductId, string Name, string Category, decimal Revenue, decimal? AverageDistribution, IReadOnlyList<MatrixCell> Cells,
    bool IsTop = false);

/// <summary>
/// Плитки над категориями: факт и выручка охвата, факт всего прошлого месяца, SKU в продаже / в ассортименте
/// и пропавшие (суммы по категориям отчёта), ТТ с покупкой; Mono — «Только он»: ТТ с положительной строкой ровно по одному SKU.
/// </summary>
public sealed record AssortmentSummary(decimal FactKg, decimal Revenue, decimal PrevMonthKg, int SkuSold, int SkuTotal, int SkuLost, int Outlets, int Mono = 0);

/// <summary>
/// Вкладка «Ассортимент»: плитки, категории, АКБ по месяцам, регионы (у охвата из двух и больше регионов), товары категорий отчёта
/// (ProductsOutsideReport — сколько товаров других типов Linko в таблицу не вошло) и матрица «товар × регион».
/// </summary>
public sealed record AssortmentView(
    PeriodInfo Period,
    string ScopeName,
    IReadOnlyList<CategoryCard> Categories,
    AkbByMonth AkbMonths,
    IReadOnlyList<AssortmentRegionRow> Regions,
    IReadOnlyList<ProductRow> Products,
    IReadOnlyList<UnitRef> MatrixRegions,
    IReadOnlyList<MatrixRow> Matrix,
    DataQualityView Quality,
    AssortmentSummary Summary,
    int ProductsOutsideReport = 0);

public sealed partial class SalesAnalytics
{
    /// <summary>Порог «матрицы»: оранжевым — товар стоит меньше чем в этой доле от своей средней дистрибуции.</summary>
    private const decimal MatrixLowShare = 0.4m;

    /// <summary>Строк в матрице «товар × регион»: товары с наибольшей выручкой в охвате.</summary>
    private const int MatrixRows = 20;

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
        var kg = lines.Sum(l => l.Kg);
        var revenue = lines.Sum(l => l.Revenue);
        // «Доля в объёме ТП» — по весу (DOC): кг магазина ÷ кг ТП за месяц.
        var agentKg = agentId is { } a ? _currentByAgent[a].Sum(l => l.Kg) : (decimal?)null;

        var categories = lines.GroupBy(l => GroupOf(l.CategoryId))
            .Select(g => new StoreCategoryRow(_cats.NameOf(g.Key), g.Sum(l => l.Kg), g.Sum(l => l.Revenue), SalesMath.Ratio(g.Sum(l => l.Revenue), revenue)))
            .OrderByDescending(c => c.Revenue)
            .ToList();
        var products = lines.Where(l => l.ProductId != null).GroupBy(l => l.ProductId!.Value)
            .Select(g =>
            {
                var product = _d.Products.GetValueOrDefault(g.Key);
                return new StoreProductRow(g.Key, product?.Name ?? $"Товар {g.Key}", product?.Code, _cats.NameOf(GroupOf(product?.CategoryId)),
                    g.Sum(l => l.Kg), g.Sum(l => l.Revenue), _d.Top.Contains(product?.Code));
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
            kg,
            revenue,
            SalesMath.OrderCount(lines),
            SalesMath.CategoryCount(lines, GroupOf),
            lines.Where(l => l.OrderId != null && l.ProductId != null).Select(l => l.ProductId).Distinct().Count(),
            agentKg is { } ak ? SalesMath.Ratio(kg, ak) : null,
            previous.Sum(l => l.Kg),
            previous.Sum(l => l.Revenue),
            categories,
            products,
            lines.Where(l => l.AgentId != null).Select(l => AgentName(l.AgentId!.Value)).Distinct().Order().ToList());
    }

    /// <summary>
    /// Ассортимент агента: категории, магазины, товары и товары, которые регион ставит шире. Дистрибуция товара у ТП и в регионе —
    /// ТТ с положительной строкой товара ÷ АКБ ТП или региона.
    /// </summary>
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
                var regionDist = SalesMath.Ratio(SalesMath.SkuTt(g), regionAkb);
                var own = SalesMath.SkuTt(agentByProduct[g.Key]);
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

        var products = ProductsOf(lines);
        return new AgentAssortment(CategoryCardsFor(lines, _previousByAgent[agent].ToList(), republic: false), stores, products.Rows, lagging,
            products.OutsideReport);
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

        var products = ProductsOf(now);
        return new ExportView(Period, Excluded(), CategoryCardsFor(now, before, republic: false), markets, agents, products.Rows, _d.ExcludedOtherCurrency,
            products.OutsideReport);
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
        var previous = scope is null ? _d.Previous : scope.SelectMany(x => _previousByRegion[x]).ToList();
        // Категории — по каталогу: знаменатель «из M SKU» не зависит от охвата, категория без продаж в охвате — «0 из M».
        var categories = CatalogCardsFor(lines, previous, republic: scope is null);
        var products = ProductsOf(lines);
        var regionRows = RegionRowsOf(scope, category: null);
        var summary = new AssortmentSummary(lines.Sum(l => l.Kg), lines.Sum(l => l.Revenue), previous.Sum(l => l.Kg),
            categories.Sum(c => c.SkuSold), categories.Sum(c => c.SkuTotal), categories.Sum(c => c.Lost), SalesMath.Akb(lines), SoloOf(lines).Values.Sum());

        var matrixRegions = regionRows.Where(x => !x.NoData).Select(x => new UnitRef(x.Id, x.Name)).ToList();
        var matrix = matrixRegions.Count >= 2 ? MatrixOf(products.Rows, matrixRegions) : [];

        return new AssortmentView(Period, scopeName, categories, AkbMonthsOf(scope, categories), regionRows, products.Rows,
            matrix.Count > 0 ? matrixRegions : [], matrix, QualityOf(scope), summary, products.OutsideReport);
    }

    /// <summary>
    /// Матрица «товар × регион» (DOC §9.2): 20 товаров категорий отчёта с наибольшей выручкой в охвате × регионы с продажами.
    /// Клетка — дистрибуция: ТТ региона с положительной строкой товара ÷ АКБ региона. Средняя — дистрибуция товара по всему охвату:
    /// Σ ТТ с товаром ÷ Σ АКБ регионов. Красный — товара в регионе нет, оранжевый — клетка меньше 40% средней (SalesMath.MatrixLevelOf).
    /// </summary>
    private List<MatrixRow> MatrixOf(IReadOnlyList<ProductRow> products, IReadOnlyList<UnitRef> regions)
    {
        var regionLines = regions.ToDictionary(x => x.Id, x => _currentByRegion[Guid.Parse(x.Id)].ToList());
        var regionAkb = regionLines.ToDictionary(x => x.Key, x => SalesMath.Akb(x.Value));
        var totalAkb = regionAkb.Values.Sum();
        var byProduct = regionLines.ToDictionary(x => x.Key, x => x.Value.Where(l => l.ProductId != null).ToLookup(l => l.ProductId!.Value));

        return products.OrderByDescending(p => p.Revenue).Take(MatrixRows)
            .Select(p =>
            {
                var tt = regions.ToDictionary(x => x.Id, x => SalesMath.SkuTt(byProduct[x.Id][p.ProductId]));
                var average = SalesMath.Ratio(tt.Values.Sum(), totalAkb);
                var cells = regions.Select(x =>
                {
                    var distribution = SalesMath.Ratio(tt[x.Id], regionAkb[x.Id]);
                    return new MatrixCell(x.Id, distribution, SalesMath.MatrixLevelOf(tt[x.Id], distribution, average, MatrixLowShare), tt[x.Id]);
                }).ToList();
                return new MatrixRow(p.ProductId, p.Name, p.Category, p.Revenue, average, cells, p.IsTop);
            })
            .ToList();
    }

    private sealed record ProductList(List<ProductRow> Rows, int OutsideReport);

    /// <summary>
    /// «Товары» набора — только категории отчёта: вес, выручка, доля выручки (от выручки товаров категорий отчёта в наборе), ТТ артикула
    /// (положительная строка) и дистрибуция — их доля от АКБ набора. Товары других типов Linko (бонус, подарки, оборудование)
    /// в таблицу не входят: их число — OutsideReport, вес и сумма — в «Качестве данных» (QualityOf).
    /// </summary>
    private ProductList ProductsOf(IReadOnlyList<SaleLine> lines)
    {
        var akb = SalesMath.Akb(lines);
        var all = lines.Where(l => l.ProductId != null).GroupBy(l => l.ProductId!.Value)
            .Select(g =>
            {
                var product = _d.Products.GetValueOrDefault(g.Key);
                var items = g.ToList();
                return (Id: g.Key, Product: product, Group: GroupOf(product?.CategoryId ?? items[0].CategoryId), Lines: items,
                    Kg: items.Sum(l => l.Kg), Revenue: items.Sum(l => l.Revenue));
            })
            .Where(x => x.Kg != 0 || x.Revenue != 0)
            .ToList();
        var report = all.Where(x => InReport(x.Group)).ToList();
        var revenue = report.Sum(x => x.Revenue);

        var rows = report
            .Select(x =>
            {
                var tt = SalesMath.SkuTt(x.Lines);
                return new ProductRow(x.Id, x.Product?.Name ?? $"Товар {x.Id}", x.Product?.Code, _cats.NameOf(x.Group), true,
                    x.Kg, x.Revenue, SalesMath.Ratio(x.Revenue, revenue), tt, SalesMath.Ratio(tt, akb), _d.Top.Contains(x.Product?.Code));
            })
            .OrderByDescending(p => p.Revenue)
            .ToList();
        return new ProductList(rows, all.Count - report.Count);
    }
}
