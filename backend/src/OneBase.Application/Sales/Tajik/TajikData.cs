using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Domain.Sales;

namespace OneBase.Application.Sales.Tajik;

/// <summary>Товар 1С Таджикистана в отчёте: категория, товар Linko, вес единицы.</summary>
public sealed record TajikProductInfo(string Code, string Name, string? Category, long? LinkoProductId, string? LinkoName, TajikUnitWeight Weight);

/// <summary>Регион точки и откуда он: manual — закреплена, name — слово в названии, agent — регион ТП, none — не определён.</summary>
public sealed record TajikStoreRegion(string Id, string Name, string Source);

/// <summary>Строка продажи с весом, категорией и регионом — для расчётов.</summary>
public sealed record TajikFact(
    string Doc,
    DateOnly Date,
    int LineNo,
    string StoreId,
    string Store,
    string? Agent,
    string Code,
    string Name,
    string? Category,
    decimal Qty,
    decimal Price,
    decimal Amount,
    decimal Discount,
    decimal? DiscountPercent,
    decimal Kg,
    bool KnownWeight,
    TajikStoreRegion Region);

/// <summary>Регионы и закрепления: определяет регион точки.</summary>
public sealed class TajikRegionResolver(
    IReadOnlyList<TajikRegion> regions,
    IReadOnlyDictionary<string, Guid> stores,
    IReadOnlyDictionary<string, Guid> agents)
{
    public const string NoneId = "none";
    public const string NoneName = "Регион не определён";

    private readonly Dictionary<Guid, TajikRegion> _byId = regions.ToDictionary(r => r.Id);
    private readonly (Guid Key, IReadOnlyList<string> Keywords)[] _keywords = regions.Select(r => (r.Id, (IReadOnlyList<string>)r.Keywords)).ToArray();
    private readonly Dictionary<(string, string?), TajikStoreRegion> _cache = new();

    public IReadOnlyList<TajikRegion> Regions { get; } = regions;

    public IReadOnlyDictionary<string, Guid> Stores { get; } = stores;

    public IReadOnlyDictionary<string, Guid> Agents { get; } = agents;

    public TajikStoreRegion Resolve(string store, string? agent)
    {
        if (_cache.TryGetValue((store, agent), out var cached))
        {
            return cached;
        }

        TajikStoreRegion result;
        if (stores.TryGetValue(store, out var manual) && _byId.TryGetValue(manual, out var m))
        {
            result = new TajikStoreRegion(m.Id.ToString(), m.Name, "manual");
        }
        else if (TajikText.MatchRegion(store, _keywords) is { } found && _byId.TryGetValue(found, out var n))
        {
            result = new TajikStoreRegion(n.Id.ToString(), n.Name, "name");
        }
        else if (agent is not null && agents.TryGetValue(agent, out var byAgent) && _byId.TryGetValue(byAgent, out var a))
        {
            result = new TajikStoreRegion(a.Id.ToString(), a.Name, "agent");
        }
        else
        {
            result = new TajikStoreRegion(NoneId, NoneName, "none");
        }

        _cache[(store, agent)] = result;
        return result;
    }

    public TajikRegion? Find(string id) => Guid.TryParse(id, out var g) && _byId.TryGetValue(g, out var r) ? r : null;
}

/// <summary>
/// Загрузка продаж 1С Таджикистана: действующие строки (RemovedAt пусто), справочник товаров (категория по товару Linko с тем же номером,
/// вес единицы — <see cref="TajikWeights"/>; кэш до следующей синхронизации или 30 минут) и регионы точек.
/// </summary>
public sealed class TajikDataLoader(IAppDbContext db, SalesOptions options, TajikCatalogCache cache, TimeProvider time)
{
    public async Task<IReadOnlyList<TajikMonth>> MonthsAsync(CancellationToken ct)
    {
        var months = await db.TajikSaleLines.AsNoTracking()
            .Where(l => l.RemovedAt == null)
            .GroupBy(l => new { l.DocDate.Year, l.DocDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month })
            .ToListAsync(ct);
        return months.OrderBy(m => m.Year).ThenBy(m => m.Month).Select(m => new TajikMonth(m.Year, m.Month)).ToList();
    }

    public async Task<DateOnly?> FirstDateAsync(CancellationToken ct) =>
        await db.TajikSaleLines.AsNoTracking().Where(l => l.RemovedAt == null).OrderBy(l => l.DocDate).Select(l => (DateOnly?)l.DocDate).FirstOrDefaultAsync(ct);

    public async Task<TajikRegionResolver> RegionsAsync(CancellationToken ct)
    {
        var regions = await db.TajikRegions.AsNoTracking().OrderBy(r => r.SortOrder).ThenBy(r => r.Name).ToListAsync(ct);
        var assignments = await db.TajikRegionAssignments.AsNoTracking().ToListAsync(ct);
        return new TajikRegionResolver(
            regions,
            assignments.Where(a => a.Kind == TajikRegionAssignment.StoreKind).ToDictionary(a => a.Name, a => a.RegionId, StringComparer.Ordinal),
            assignments.Where(a => a.Kind == TajikRegionAssignment.AgentKind).ToDictionary(a => a.Name, a => a.RegionId, StringComparer.Ordinal));
    }

    /// <summary>Справочник товаров по всем загруженным продажам (вес единицы не зависит от выбранного месяца).</summary>
    public async Task<IReadOnlyDictionary<string, TajikProductInfo>> CatalogAsync(CancellationToken ct)
    {
        if (cache.TryGet(time.GetUtcNow()) is { } cached)
        {
            return cached;
        }

        var version = cache.Version;
        var sold = await db.TajikSaleLines.AsNoTracking()
            .Where(l => l.RemovedAt == null)
            .GroupBy(l => l.ProductCode)
            .Select(g => new
            {
                Code = g.Key,
                Name = g.OrderByDescending(l => l.DocDate).Select(l => l.ProductName).First(),
                Qty = g.Sum(l => l.Qty),
                Value = g.Sum(l => l.Qty * l.Price),
            })
            .ToListAsync(ct);
        var stockOnly = await db.TajikStockItems.AsNoTracking()
            .GroupBy(s => s.ProductCode)
            .Select(g => new { Code = g.Key, Name = g.OrderByDescending(s => s.Date).Select(s => s.ProductName).First() })
            .ToListAsync(ct);

        var products = sold.Select(s => (s.Code, s.Name, Price: s.Qty > 0 ? s.Value / s.Qty : (decimal?)null))
            .Concat(stockOnly.Where(s => sold.All(x => x.Code != s.Code)).Select(s => (s.Code, s.Name, Price: (decimal?)null)))
            .ToList();

        var linko = await db.LinkoProducts.AsNoTracking().Select(p => new { p.Id, p.Name, p.Code, p.TypeId }).ToListAsync(ct);
        var tree = await LinkoTypeTree.LoadAsync(db, ct);
        var byType = options.CategoryByType();

        var matched = products.ToDictionary(p => p.Code, p =>
        {
            var code = TajikText.LinkoCode(p.Name);
            if (code is null) return null;
            return linko.Where(l => l.Code == code).FirstOrDefault() ?? linko.FirstOrDefault(l => TajikText.SameCode(l.Code, code));
        });

        var ids = matched.Values.Where(v => v is not null).Select(v => v!.Id).Distinct().ToList();
        var since = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime).AddDays(-180);
        var prices = ids.Count == 0
            ? []
            : await (from l in db.LinkoOrderLines.AsNoTracking()
                     join o in db.LinkoOrders.AsNoTracking() on l.OrderId equals o.Id
                     where l.ProductId != null && ids.Contains(l.ProductId.Value) && l.Amount > 0 && l.TotalWeight > 0 && o.CreatedDate >= since
                           && (o.Currency == null || o.Currency == "SUM")
                     group l by l.ProductId into g
                     select new { Id = g.Key!.Value, Price = g.Sum(x => x.TotalPrice), Kg = g.Sum(x => x.TotalWeight), Qty = g.Sum(x => x.Amount) })
                .ToListAsync(ct);
        var priceOf = prices.ToDictionary(p => p.Id);

        var weights = TajikWeights.Resolve(products.Select(p =>
        {
            var l = matched[p.Code];
            var pr = l is null ? null : priceOf.GetValueOrDefault(l.Id);
            return new TajikWeightInput(p.Code, p.Name, p.Price, pr is { Kg: > 0 } ? pr.Price / pr.Kg : null, pr is { Qty: > 0 } ? pr.Kg / pr.Qty : null);
        }));

        var result = products.ToDictionary(p => p.Code, p =>
        {
            var l = matched[p.Code];
            var category = l?.TypeId is { } type ? CategoryOf(type, tree, byType) : null;
            category ??= options.PlanCategoriesOf(p.Name).FirstOrDefault();
            return new TajikProductInfo(p.Code, p.Name, category, l?.Id, l?.Name, weights[p.Code]);
        }, StringComparer.Ordinal);

        cache.Set(result, version, time.GetUtcNow());
        return result;
    }

    /// <summary>Действующие строки за период — с весом, категорией и регионом точки.</summary>
    public async Task<List<TajikFact>> FactsAsync(
        DateOnly from,
        DateOnly to,
        IReadOnlyDictionary<string, TajikProductInfo> catalog,
        TajikRegionResolver regions,
        CancellationToken ct)
    {
        var lines = await db.TajikSaleLines.AsNoTracking()
            .Where(l => l.RemovedAt == null && l.DocDate >= from && l.DocDate <= to)
            .OrderBy(l => l.DocDate).ThenBy(l => l.DocNumber).ThenBy(l => l.LineNo)
            .ToListAsync(ct);
        return lines.Select(l => Fact(l, catalog, regions)).ToList();
    }

    public static TajikFact Fact(TajikSaleLine l, IReadOnlyDictionary<string, TajikProductInfo> catalog, TajikRegionResolver regions)
    {
        var info = catalog.GetValueOrDefault(l.ProductCode);
        var unit = info?.Weight.Kg;
        return new TajikFact(
            l.DocNumber, l.DocDate, l.LineNo, TajikText.StoreId(l.Customer), l.Customer.Trim(), l.Manager?.Trim(), l.ProductCode, info?.Name ?? l.ProductName,
            info?.Category, l.Qty, l.Price, l.Amount, l.Discount, l.DiscountPercent, unit is { } u ? l.Qty * u : 0, unit is not null,
            regions.Resolve(l.Customer.Trim(), l.Manager?.Trim()));
    }

    /// <summary>Порядок категорий — как в отчёте (Sales:Categories), «без категории» — последней.</summary>
    public IReadOnlyList<string> CategoryOrder => options.Categories.Keys.ToList();

    private static string? CategoryOf(long type, LinkoTypeTree tree, IReadOnlyDictionary<long, string> byType)
    {
        // Подтип («Снек 200 г» → «Снек») — по родителям, не глубже 10 уровней.
        long? current = type;
        for (var depth = 0; current is { } id && depth < 10; depth++)
        {
            if (byType.TryGetValue(id, out var name)) return name;
            current = tree.Parents.GetValueOrDefault(id);
        }

        return null;
    }
}

/// <summary>Кэш справочника товаров Таджикистана: до смены версии данных (синхронизация) или 30 минут.</summary>
public sealed class TajikCatalogCache(TajikDataVersion version)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);
    private (IReadOnlyDictionary<string, TajikProductInfo> Value, long Version, DateTimeOffset At)? _entry;

    public long Version => version.Value;

    public IReadOnlyDictionary<string, TajikProductInfo>? TryGet(DateTimeOffset now) =>
        _entry is { } e && e.Version == version.Value && now - e.At < Lifetime ? e.Value : null;

    public void Set(IReadOnlyDictionary<string, TajikProductInfo> value, long atVersion, DateTimeOffset now) => _entry = (value, atVersion, now);
}
