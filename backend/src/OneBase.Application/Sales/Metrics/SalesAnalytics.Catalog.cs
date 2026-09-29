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

/// <summary>Категория в охвате: карточка (плитки и артикулы) и, для республики и направления, регионы.</summary>
public sealed record CategoryView(
    PeriodInfo Period,
    string Scope,
    string ScopeName,
    string CategoryId,
    string Name,
    CategoryCard? Card,
    IReadOnlyList<AssortmentRegionRow> Regions);

/// <summary>Строка «где товар идёт, а где нет»: регион, ТП или магазин.</summary>
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
    decimal PrevMonthKg);

/// <summary>Артикул в охвате: факт, ТТ с товаром, цена за кг и разбивка — по регионам, ТП региона или магазинам ТП.</summary>
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
    IReadOnlyList<ProductBreakdownRow> Rows);

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

        var cards = View($"cards:{s.Key}", () => CategoryCardsFor(scope.Now, scope.Before, scope.Kind == ScopeKinds.Republic));
        var regions = scope.Kind is ScopeKinds.Republic or ScopeKinds.Direction ? RegionRowsOf(scope.Regions, id) : [];
        return new CategoryView(Period, scope.Kind, scope.Name, id, _cats.NameOf(group), cards.FirstOrDefault(c => c.Id == id), regions);
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
        var tt = SalesMath.Akb(now);
        var outlets = SalesMath.Akb(scope.Now);
        var (breakdown, rows) = scope.Kind switch
        {
            ScopeKinds.Republic or ScopeKinds.Direction => (ProductBreakdowns.Regions, ProductRegions(id, scope.Regions)),
            ScopeKinds.Region => (ProductBreakdowns.Agents, ProductAgents(id, scope.Regions!.Single())),
            _ => (ProductBreakdowns.Stores, ProductStores(id, scope)),
        };

        return new ProductView(Period, scope.Kind, scope.Name, id, info?.Name ?? $"Товар {id}", info?.Code, group?.ToString() ?? "none", _cats.NameOf(group),
            SkuStatusOf(id, SoldIn(now), SoldIn(before), scope.Kind == ScopeKinds.Republic), kg, revenue, tt, outlets, SalesMath.Ratio(tt, outlets),
            kg > 0 ? revenue / kg : null, before.Sum(l => l.Kg), breakdown, rows);
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
    /// «По регионам» вкладки «Ассортимент» и страницы категории. «SKU идёт» — сколько артикулов из тех, что продаются
    /// по республике в этом месяце, есть в регионе; «не возят» — остальные; «пропало» — продавались в прошлом месяце, в этом нет.
    /// category — id категории отчёта (как у карточки); null — все категории отчёта.
    /// </summary>
    private List<AssortmentRegionRow> RegionRowsOf(IReadOnlySet<Guid>? scope, string? category)
    {
        bool Keep(long? linkoType) => category is null ? InReport(GroupOf(linkoType)) : CategoryIdOf(linkoType) == category;

        var universe = Sold(_d.Current.Where(l => Keep(l.CategoryId)));
        return RegionIds(scope).Where(id => id != NoRegionId)
            .Select(id =>
            {
                var all = _currentByRegion[id].ToList();
                var now = category is null ? all : all.Where(l => Keep(l.CategoryId)).ToList();
                var selling = Sold(now);
                var lost = Sold(_previousByRegion[id].Where(l => Keep(l.CategoryId))).Count(p => !selling.Contains(p));
                return new AssortmentRegionRow(id.ToString(), _regions[id].Name, now.Sum(l => l.Kg), now.Sum(l => l.Revenue),
                    selling.Count(universe.Contains), universe.Count(p => !selling.Contains(p)), lost, SalesMath.Akb(all));
            })
            .Where(x => x.Kg != 0 || x.Akb > 0)
            .OrderByDescending(x => x.Kg)
            .ThenBy(x => x.Name)
            .ToList();
    }

    /// <summary>Артикул по регионам охвата: статус, факт, ТТ с товаром и дистрибуция — доля ТТ региона с покупкой.</summary>
    private List<ProductBreakdownRow> ProductRegions(long product, IReadOnlySet<Guid>? scope) =>
        RegionIds(scope).Where(id => id != NoRegionId)
            .Select(id =>
            {
                var all = _currentByRegion[id];
                var outlets = SalesMath.Akb(all);
                var now = all.Where(l => l.ProductId == product).ToList();
                var before = _previousByRegion[id].Where(l => l.ProductId == product).ToList();
                var tt = SalesMath.Akb(now);
                return new ProductBreakdownRow(id.ToString(), _regions[id].Name, null, SkuStatusOf(product, SoldIn(now), SoldIn(before), republic: false),
                    now.Sum(l => l.Kg), now.Sum(l => l.Revenue), tt, outlets, SalesMath.Ratio(tt, outlets), before.Sum(l => l.Kg));
            })
            .Where(r => r.Outlets > 0)
            .OrderBy(r => SkuStatuses.Rank(r.Status))
            .ThenByDescending(r => r.Kg)
            .ThenByDescending(r => r.PrevMonthKg)
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
                var tt = SalesMath.Akb(now);
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
    /// Статус магазина — «продаётся», «пропал» (брал в прошлом месяце) или «молчит» (не брал).
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
                    SalesMath.Akb(lines), 1, null, prev.Sum(l => l.Kg));
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
    /// «Продаётся» — чистая выручка артикула (продажи минус возвраты) больше нуля: артикул, который вернули целиком,
    /// не продаётся.
    /// </summary>
    private static HashSet<long> Sold(IEnumerable<SaleLine> source) =>
        source.Where(l => l.ProductId != null)
            .GroupBy(l => l.ProductId!.Value)
            .Where(g => g.Sum(l => l.Revenue) > 0)
            .Select(g => g.Key)
            .ToHashSet();

    /// <summary>Строки одного артикула: продаётся ли он (чистая выручка больше нуля).</summary>
    private static bool SoldIn(IEnumerable<SaleLine> productLines) => productLines.Sum(l => l.Revenue) > 0;
}
