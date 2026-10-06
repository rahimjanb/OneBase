namespace OneBase.Application.Sales.Metrics;

// Уровни «категория» и «артикул» из «Полевого контроля»: все категории → категория → артикул.
// Охват задаёт страница, с которой пришли: республика, направление, регион, ТП или экспорт («Завод»).

/// <summary>Охват страниц категории и артикула. Всё пустое — республика.</summary>
public sealed record AssortmentScope(string? Direction, Guid? Region, long? Agent, bool Export)
{
    public string Key => $"{Direction}:{Region}:{Agent}:{Export}";
}

public static class ScopeKinds
{
    public const string Republic = "republic";
    public const string Direction = "direction";
    public const string Region = "region";
    public const string Agent = "agent";
    public const string Export = "export";
}

/// <summary>
/// Категория в охвате: карточка (плитки и артикулы) и, для республики и направления из двух и больше регионов, регионы.
/// Mono — «Только он»: ТТ охвата с положительной строкой ровно по одному SKU категорий отчёта («всего таких точек»).
/// </summary>
public sealed record CategoryView(
    PeriodInfo Period,
    string Scope,
    string ScopeName,
    string CategoryId,
    string Name,
    CategoryCard? Card,
    IReadOnlyList<AssortmentRegionRow> Regions,
    int Mono = 0);

/// <summary>
/// Строка «где товар идёт, а где нет»: регион, ТП или магазин. Tt — ТТ с положительной строкой товара (SalesMath.SkuTt).
/// NoData — в регионе в этом месяце нет ни одной покупки («нет данных»: почти всегда дыра в выгрузке, а не «не возят»).
/// </summary>
public sealed record ProductBreakdownRow(
    string Id,
    string Name,
    string? Sub,
    string Status,
    decimal Kg,
    decimal Revenue,
    int Tt,
    int Outlets,
    decimal? Distribution,
    decimal PrevMonthKg,
    bool NoData = false);

/// <summary>
/// Артикул в охвате: факт, ТТ с товаром (положительная строка), цена за кг и разбивка — по регионам (охват из двух и больше регионов),
/// ТП региона или магазинам ТП. Solo — «Только он»: моно-точки, которые держатся на этом артикуле, SoloShare — их доля от ТТ артикула,
/// Mono — все моно-точки охвата. IsTop — товар из списка ТОП.
/// </summary>
public sealed record ProductView(
    PeriodInfo Period,
    string Scope,
    string ScopeName,
    long ProductId,
    string Name,
    string? Code,
    string CategoryId,
    string Category,
    string Status,
    decimal FactKg,
    decimal Revenue,
    int Tt,
    int Outlets,
    decimal? Distribution,
    decimal? PricePerKg,
    decimal PrevMonthKg,
    string Breakdown,
    IReadOnlyList<ProductBreakdownRow> Rows,
    int Solo = 0,
    decimal? SoloShare = null,
    int Mono = 0,
    bool IsTop = false);

public static class ProductBreakdowns
{
    public const string Regions = "regions";
    public const string Agents = "agents";
    public const string Stores = "stores";
}

public sealed partial class SalesAnalytics
{
    private sealed record ScopeData(string Kind, string Name, IReadOnlyList<SaleLine> Now, IReadOnlyList<SaleLine> Before, IReadOnlySet<Guid>? Regions);

    private HashSet<long>? _republicSold;

    /// <summary>Артикулы, которые продаются по республике в этом месяце, — база для «не возят».</summary>
    private HashSet<long> RepublicSold => _republicSold ??= Sold(_d.Current);

    public CategoryView? CachedCategory(string id, AssortmentScope scope) => View($"category:{id}:{scope.Key}", () => Category(id, scope));

    public ProductView? CachedProduct(long id, AssortmentScope scope) => View($"product:{id}:{scope.Key}", () => Product(id, scope));

    public CategoryView? Category(string id, AssortmentScope s)
    {
        long? group = null;
        if (id != "none")
        {
            if (!long.TryParse(id, out var g))
            {
                return null;
            }

            group = g;
        }

        if (ScopeOf(s) is not { } scope)
        {
            return null;
        }

        var cards = View($"cards:{s.Key}", () => CatalogCardsFor(scope.Now, scope.Before, scope.Kind == ScopeKinds.Republic));
        var regions = scope.Kind is ScopeKinds.Republic or ScopeKinds.Direction ? RegionRowsOf(scope.Regions, id) : [];
        return new CategoryView(Period, scope.Kind, scope.Name, id, _cats.NameOf(group), cards.FirstOrDefault(c => c.Id == id), regions,
            ScopeSolo(s, scope).Values.Sum());
    }

    public ProductView? Product(long id, AssortmentScope s)
    {
        if (ScopeOf(s) is not { } scope)
        {
            return null;
        }

        var info = _d.Products.GetValueOrDefault(id);
        var now = scope.Now.Where(l => l.ProductId == id).ToList();
        var before = scope.Before.Where(l => l.ProductId == id).ToList();
        if (info is null && now.Count == 0 && before.Count == 0)
        {
            return null;
        }

        var group = GroupOf(info?.CategoryId ?? now.Concat(before).Select(l => l.CategoryId).FirstOrDefault());
        var kg = now.Sum(l => l.Kg);
        var revenue = now.Sum(l => l.Revenue);
        var tt = SalesMath.SkuTt(now);
        var outlets = SalesMath.Akb(scope.Now);
        var solo = ScopeSolo(s, scope);
        var own = solo.GetValueOrDefault(id);

        // По регионам — если в охвате два региона и больше; у направления из одного региона — по его ТП, как у региона.
        var regionIds = scope.Kind is ScopeKinds.Republic or ScopeKinds.Direction
            ? RegionIds(scope.Regions).Where(r => r != NoRegionId).ToList()
            : [];
        var (breakdown, rows) = scope.Kind switch
        {
            ScopeKinds.Republic or ScopeKinds.Direction when regionIds.Count == 1 => (ProductBreakdowns.Agents, ProductAgents(id, regionIds[0])),
            ScopeKinds.Republic or ScopeKinds.Direction => (ProductBreakdowns.Regions, ProductRegions(id, regionIds)),
            ScopeKinds.Region => (ProductBreakdowns.Agents, ProductAgents(id, scope.Regions!.Single())),
            _ => (ProductBreakdowns.Stores, ProductStores(id, scope)),
        };

        return new ProductView(Period, scope.Kind, scope.Name, id, info?.Name ?? $"Товар {id}", info?.Code, group?.ToString() ?? "none", _cats.NameOf(group),
            SkuStatusOf(id, SoldIn(now), SoldIn(before), scope.Kind == ScopeKinds.Republic), kg, revenue, tt, outlets, SalesMath.Ratio(tt, outlets),
            kg > 0 ? revenue / kg : null, before.Sum(l => l.Kg), breakdown, rows, own, SalesMath.Ratio(own, tt), solo.Values.Sum(),
            _d.Top.Contains(info?.Code));
    }

    private ScopeData? ScopeOf(AssortmentScope s)
    {
        if (s.Export)
        {
            return new ScopeData(ScopeKinds.Export, "Экспорт и опт", _d.ExcludedCurrent, _d.ExcludedPrevious, null);
        }

        if (s.Agent is { } agent)
        {
            return HasAgent(agent)
                ? new ScopeData(ScopeKinds.Agent, AgentName(agent), _currentByAgent[agent].ToList(), _previousByAgent[agent].ToList(), null)
                : null;
        }

        if (s.Region is { } region)
        {
            if (region == NoRegionId || !_regions.TryGetValue(region, out var info))
            {
                return null;
            }

            var set = new HashSet<Guid> { region };
            return new ScopeData(ScopeKinds.Region, info.Name, Lines(set).ToList(), _previousByRegion[region].ToList(), set);
        }

        if (s.Direction is { } direction)
        {
            var group = DirectionGroups().FirstOrDefault(g => g.Id == direction);
            if (group is null)
            {
                return null;
            }

            var set = group.Regions.ToHashSet();
            return new ScopeData(ScopeKinds.Direction, group.Name, Lines(set).ToList(), set.SelectMany(r => _previousByRegion[r]).ToList(), set);
        }

        return new ScopeData(ScopeKinds.Republic, "Республика", _d.Current, _d.Previous, null);
    }

    /// <summary>
    /// «По регионам» вкладки «Ассортимент» и страницы категории — только у охвата из двух и больше регионов (DOC-filters §2).
    /// «SKU идёт» — сколько артикулов из тех, что продаются по республике в этом месяце, есть в регионе; «не возят» — остальные;
    /// «пропало» — продавались в прошлом месяце, в этом нет. Регион без единой покупки за месяц не пропадает из таблицы, а помечен
    /// «нет данных» (NoData, счётчики — нули): это почти всегда дыра в выгрузке, а не регион, который ничего не возит.
    /// category — id категории отчёта (как у карточки); null — все категории отчёта.
    /// </summary>
    private List<AssortmentRegionRow> RegionRowsOf(IReadOnlySet<Guid>? scope, string? category)
    {
        var ids = RegionIds(scope).Where(id => id != NoRegionId).ToList();
        if (ids.Count < 2)
        {
            return [];
        }

        bool Keep(long? linkoType) => category is null ? InReport(GroupOf(linkoType)) : CategoryIdOf(linkoType) == category;

        var universe = Sold(_d.Current.Where(l => Keep(l.CategoryId)));
        return ids
            .Select(id =>
            {
                var all = _currentByRegion[id].ToList();
                var outlets = SalesMath.Akb(all);
                var now = category is null ? all : all.Where(l => Keep(l.CategoryId)).ToList();
                if (outlets == 0)
                {
                    return new AssortmentRegionRow(id.ToString(), _regions[id].Name, now.Sum(l => l.Kg), now.Sum(l => l.Revenue), 0, 0, 0, 0, NoData: true);
                }

                var selling = Sold(now);
                var lost = Sold(_previousByRegion[id].Where(l => Keep(l.CategoryId))).Count(p => !selling.Contains(p));
                return new AssortmentRegionRow(id.ToString(), _regions[id].Name, now.Sum(l => l.Kg), now.Sum(l => l.Revenue),
                    selling.Count(universe.Contains), universe.Count(p => !selling.Contains(p)), lost, outlets);
            })
            .OrderBy(x => x.NoData)
            .ThenByDescending(x => x.Kg)
            .ThenBy(x => x.Name)
            .ToList();
    }

    /// <summary>
    /// Артикул по регионам охвата: статус, факт, ТТ с положительной строкой товара и дистрибуция — их доля от ТТ региона с покупкой.
    /// Регион без покупок за месяц — строка «нет данных» в конце.
    /// </summary>
    private List<ProductBreakdownRow> ProductRegions(long product, IReadOnlyList<Guid> regions) =>
        regions
            .Select(id =>
            {
                var all = _currentByRegion[id];
                var outlets = SalesMath.Akb(all);
                var now = all.Where(l => l.ProductId == product).ToList();
                var before = _previousByRegion[id].Where(l => l.ProductId == product).ToList();
                if (outlets == 0)
                {
                    return new ProductBreakdownRow(id.ToString(), _regions[id].Name, null, SkuStatuses.Silent, now.Sum(l => l.Kg), now.Sum(l => l.Revenue),
                        0, 0, null, before.Sum(l => l.Kg), NoData: true);
                }

                var tt = SalesMath.SkuTt(now);
                return new ProductBreakdownRow(id.ToString(), _regions[id].Name, null, SkuStatusOf(product, SoldIn(now), SoldIn(before), republic: false),
                    now.Sum(l => l.Kg), now.Sum(l => l.Revenue), tt, outlets, SalesMath.Ratio(tt, outlets), before.Sum(l => l.Kg));
            })
            .OrderBy(r => r.NoData)
            .ThenBy(r => SkuStatuses.Rank(r.Status))
            .ThenByDescending(r => r.Kg)
            .ThenByDescending(r => r.PrevMonthKg)
            .ThenBy(r => r.Name)
            .ToList();

    /// <summary>Артикул по ТП региона (у кого в этом месяце есть продажи): у кого идёт, у кого пропал, кто не возит.</summary>
    private List<ProductBreakdownRow> ProductAgents(long product, Guid region)
    {
        var before = _previousByRegion[region].Where(l => l.AgentId != null && l.ProductId == product).ToLookup(l => l.AgentId!.Value);
        return _currentByRegion[region].Where(l => l.AgentId != null).GroupBy(l => l.AgentId!.Value)
            .Select(g =>
            {
                var outlets = SalesMath.Akb(g);
                var now = g.Where(l => l.ProductId == product).ToList();
                var prev = before[g.Key].ToList();
                var tt = SalesMath.SkuTt(now);
                return new ProductBreakdownRow(g.Key.ToString(), AgentName(g.Key), $"ID {g.Key}", SkuStatusOf(product, SoldIn(now), SoldIn(prev), republic: false),
                    now.Sum(l => l.Kg), now.Sum(l => l.Revenue), tt, outlets, SalesMath.Ratio(tt, outlets), prev.Sum(l => l.Kg));
            })
            .Where(r => r.Outlets > 0)
            .OrderBy(r => SkuStatuses.Rank(r.Status))
            .ThenByDescending(r => r.Kg)
            .ThenByDescending(r => r.PrevMonthKg)
            .ToList();
    }

    /// <summary>
    /// Артикул по магазинам ТП или экспорта: магазины с заказом в этом месяце и те, кто брал товар в прошлом.
    /// Статус магазина — «продаётся» (положительная строка товара), «пропал» (была в прошлом месяце) или «молчит» (не брал).
    /// </summary>
    private List<ProductBreakdownRow> ProductStores(long product, ScopeData scope)
    {
        var now = scope.Now.Where(l => l.MarketId != null).ToLookup(l => l.MarketId!.Value);
        var before = scope.Before.Where(l => l.MarketId != null && l.ProductId == product).ToLookup(l => l.MarketId!.Value);
        return now.Where(g => g.Any(l => l.OrderId != null)).Select(g => g.Key)
            .Concat(before.Select(g => g.Key))
            .Distinct()
            .Select(market =>
            {
                var lines = now[market].Where(l => l.ProductId == product).ToList();
                var prev = before[market].ToList();
                var status = SoldIn(lines) ? SkuStatuses.Selling : SoldIn(prev) ? SkuStatuses.Lost : SkuStatuses.Silent;
                return new ProductBreakdownRow(market.ToString(), MarketName(market), $"№ {market}", status, lines.Sum(l => l.Kg), lines.Sum(l => l.Revenue),
                    SalesMath.SkuTt(lines), 1, null, prev.Sum(l => l.Kg));
            })
            .OrderBy(r => SkuStatuses.Rank(r.Status))
            .ThenByDescending(r => r.Kg)
            .ThenByDescending(r => r.PrevMonthKg)
            .ThenBy(r => r.Name)
            .ToList();
    }

    /// <summary>Статус артикула в наборе: продаётся → пропал → не возят (по республике идёт) → молчит.</summary>
    private string SkuStatusOf(long product, bool soldNow, bool soldBefore, bool republic) =>
        soldNow ? SkuStatuses.Selling
        : soldBefore ? SkuStatuses.Lost
        : !republic && RepublicSold.Contains(product) ? SkuStatuses.Elsewhere
        : SkuStatuses.Silent;

    /// <summary>Id категории отчёта для типа Linko — как у карточки категории.</summary>
    private string CategoryIdOf(long? linkoType) => GroupOf(linkoType)?.ToString() ?? "none";

    /// <summary>
    /// «Продаётся» — в наборе есть ТТ с положительной строкой артикула (кг или выручка больше нуля, SalesMath.SkuTt больше нуля).
    /// Артикул, который каждая ТТ вернула целиком, не продаётся; погашенный возвратами в одной ТТ и купленный в другой — продаётся.
    /// </summary>
    private static HashSet<long> Sold(IEnumerable<SaleLine> source) =>
        SalesMath.PositiveSkus(source).Values.SelectMany(skus => skus).ToHashSet();

    /// <summary>Строки одного артикула: продаётся ли он (есть ТТ с положительной строкой).</summary>
    private static bool SoldIn(IEnumerable<SaleLine> productLines) => SalesMath.SkuTt(productLines) > 0;

    /// <summary>«Только он» набора — по товарам категорий отчёта: бонусы, подарки и прочие типы Linko моно-точку не делают.</summary>
    private Dictionary<long, int> SoloOf(IEnumerable<SaleLine> lines) =>
        SalesMath.SoloByProduct(lines.Where(l => InReport(GroupOf(l.CategoryId))));

    /// <summary>«Только он» охвата страниц категории и артикула — один раз на охват: артикулов охвата открывают много.</summary>
    private Dictionary<long, int> ScopeSolo(AssortmentScope s, ScopeData scope) => View($"solo:{s.Key}", () => SoloOf(scope.Now));

    /// <summary>
    /// Карточки категорий вкладки «Ассортимент» и страницы категории — по каталогу, а не по продажам охвата: категории отчёта, у которых
    /// есть SKU в ассортименте (ActiveSkus — продажи по республике с 1 января), в этом или в прошлом месяце. Знаменатель «из M SKU» от охвата
    /// не зависит; категория без продаж в охвате не пропадает — «продаётся 0 из M», её SKU — «не возят» (по республике идут) или «нет продаж».
    /// Карточки вторички (CategoryCardsOf) категорий без продаж не показывают (DOC-filters §1); здесь они — после карточек с продажами.
    /// </summary>
    private List<CategoryCard> CatalogCardsFor(IReadOnlyList<SaleLine> lines, IReadOnlyList<SaleLine> previousLines, bool republic)
    {
        var cards = CategoryCardsFor(lines, previousLines, republic);
        var shown = cards.Select(c => c.Id).ToHashSet();
        var current = lines.ToLookup(l => GroupOf(l.CategoryId));
        var previous = previousLines.ToLookup(l => GroupOf(l.CategoryId));
        var assortment = _d.ActiveSkus.Where(_d.Products.ContainsKey).ToLookup(p => GroupOf(_d.Products[p].CategoryId));
        var totalKg = lines.Sum(l => l.Kg);
        var akb = SalesMath.Akb(lines);
        var solo = SoloOf(lines);

        var missing = assortment.Select(g => g.Key).Concat(current.Select(g => g.Key)).Concat(previous.Select(g => g.Key))
            .Distinct()
            .Where(c => InReport(c) && !shown.Contains(c?.ToString() ?? "none"))
            .Select(c => CategoryCardOf(c, assortment[c], current[c].ToList(), previous[c].ToList(), republic, totalKg, akb, solo))
            .OrderByDescending(c => c.SkuTotal)
            .ThenBy(c => c.Name)
            .ToList();
        return cards.Concat(missing).ToList();
    }

    /// <summary>
    /// Карточка категории, у которой в охвате нет продаж ни в этом, ни в прошлом месяце (CategoryCardsFor такие не отдаёт): те же правила,
    /// что у карточки с продажами — ассортимент категории плюс проданное, статусы SkuStatusOf, ТТ и «Только он» по положительным строкам
    /// (строки, погашенные возвратами, учтены так же).
    /// </summary>
    private CategoryCard CategoryCardOf(
        long? category,
        IEnumerable<long> assortment,
        IReadOnlyList<SaleLine> now,
        IReadOnlyList<SaleLine> before,
        bool republic,
        decimal totalKg,
        int akb,
        IReadOnlyDictionary<long, int> solo)
    {
        var soldNow = Sold(now);
        var soldBefore = Sold(before);
        var universe = assortment.Concat(soldNow).Concat(soldBefore).ToHashSet();
        var fact = now.Sum(l => l.Kg);
        var revenue = now.Sum(l => l.Revenue);
        var nowByProduct = now.Where(l => l.ProductId != null).ToLookup(l => l.ProductId!.Value);
        var beforeByProduct = before.Where(l => l.ProductId != null).ToLookup(l => l.ProductId!.Value);

        var skus = universe
            .Select(p =>
            {
                var sl = nowByProduct[p].ToList();
                var skuAkb = SalesMath.SkuTt(sl);
                var skuKg = sl.Sum(l => l.Kg);
                var skuSolo = solo.GetValueOrDefault(p);
                var product = _d.Products.GetValueOrDefault(p);
                return new SkuRow(p, product?.Name ?? $"Товар {p}", product?.Code, skuKg, sl.Sum(l => l.Revenue), skuAkb, SalesMath.Ratio(skuAkb, akb),
                    beforeByProduct[p].Sum(l => l.Kg), SkuStatusOf(p, soldNow.Contains(p), soldBefore.Contains(p), republic),
                    skuKg == 0 ? null : SalesMath.Ratio(skuKg, fact), skuSolo, SalesMath.Ratio(skuSolo, skuAkb), _d.Top.Contains(product?.Code));
            })
            .OrderBy(s => SkuStatuses.Rank(s.Status))
            .ThenByDescending(s => s.FactKg)
            .ThenByDescending(s => s.PrevMonthKg)
            .ThenBy(s => s.Name)
            .ToList();

        var categoryAkb = SalesMath.Akb(now);
        var forecast = ForecastOf(fact);
        var prevKg = before.Sum(l => l.Kg);
        return new CategoryCard(
            category?.ToString() ?? "none",
            _cats.NameOf(category),
            soldNow.Count,
            universe.Count,
            fact,
            SalesMath.Ratio(fact, totalKg),
            revenue,
            categoryAkb,
            SalesMath.Ratio(categoryAkb, akb),
            forecast,
            ForecastOf(revenue),
            prevKg,
            _d.Closed ? null : SalesMath.Delta(prevKg, forecast ?? fact),
            universe.Count - soldNow.Count,
            soldBefore.Count(p => !soldNow.Contains(p)),
            skus);
    }
}
