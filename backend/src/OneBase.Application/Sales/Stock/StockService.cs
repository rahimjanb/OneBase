using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;

namespace OneBase.Application.Sales.Stock;

public sealed record StockRegion(string Id, string Name, long StockId);

/// <summary>Остаток SKU на одном складе: штуки как в Linko, кг и дни запаса — если известен вес штуки.</summary>
public sealed record StockCell(decimal Pieces, decimal? Kg, decimal? KgPerDay, decimal? DaysOfCover);

public static class StockStatuses
{
    /// <summary>Хватит меньше чем на 15 дней продаж.</summary>
    public const string Deficit = "deficit";

    /// <summary>Хватит больше чем на 30 дней продаж.</summary>
    public const string Overstock = "overstock";

    /// <summary>Остаток лежит, а продаж за базовый период не было.</summary>
    public const string Dead = "dead";

    public const string Ok = "ok";

    /// <summary>Нет веса штуки — дни запаса не посчитать.</summary>
    public const string Unknown = "unknown";
}

public sealed record StockItem(
    long ProductId,
    string Name,
    string? Code,
    string Category,
    bool InReport,
    decimal? UnitKg,
    string UnitKgSource,
    decimal? BoxKg,
    string BoxNote,
    decimal Pieces,
    decimal? Kg,
    decimal? Boxes,
    decimal? KgPerDay,
    decimal? DaysOfCover,
    decimal? Need15Kg,
    string Status,
    IReadOnlyDictionary<string, StockCell> Regions,
    StockCell? Factory);

/// <summary>Склад, не сопоставленный ни с регионом, ни с заводом: в остаток страны не входит, показывается отдельно.</summary>
public sealed record OtherStock(long StockId, string Name, decimal Pieces, decimal? Kg, int Items);

public sealed record StockTotals(decimal? Kg, decimal? Boxes, decimal? KgPerDay, decimal? DaysOfCover, int Deficit, int Overstock, int Dead, int WithoutWeight);

public sealed record StockView(
    DateTimeOffset? SyncedAt,
    int VelocityDays,
    DateOnly VelocityFrom,
    DateOnly VelocityTo,
    IReadOnlyList<StockRegion> Regions,
    StockRegion? Factory,
    IReadOnlyList<StockItem> Items,
    IReadOnlyList<OtherStock> OtherStocks,
    StockTotals Totals,
    StockTotals? FactoryTotals);

/// <summary>
/// Рекомендуемый остаток: остатки Linko (штуки) по складам регионов → кг по весу штуки → коробки по весу коробки,
/// скорость продаж (кг в день) — из вторички за последние StockVelocityDays дней по региону склада.
/// Склад региона — склад с тем же названием, что у региона; склад завода — в остаток страны не входит.
/// </summary>
public sealed class StockService(IAppDbContext db, SalesOptions options, Microsoft.Extensions.Caching.Memory.IMemoryCache cache, SalesCacheSignal signal)
{
    public Task<StockView> GetAsync(string? regionId, CancellationToken ct = default) =>
        SalesViewCache.GetAsync(cache, signal, $"sales:stock:{regionId}", () => BuildAsync(regionId, ct));

    /// <summary>Вес единицы учёта считается по продажам за этот период.</summary>
    private const int UnitWeightDays = 365;

    public async Task<StockView> BuildAsync(string? regionId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var velocityTo = today;
        var velocityFrom = today.AddDays(-(options.StockVelocityDays - 1));
        var sold = options.SoldStatuses;
        var excluded = options.ExcludedBranches.Select(b => b.Trim().ToLowerInvariant()).ToArray();

        var regions = await db.SalesRegions.AsNoTracking().Select(r => new { r.Id, r.Name, r.LinkoBranchId }).ToListAsync(ct);
        var stocks = await db.LinkoStocks.AsNoTracking().Select(s => new { s.Id, s.Name }).ToListAsync(ct);
        var regionByName = regions
            .GroupBy(r => r.Name.Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        var regionStocks = stocks
            .Where(s => !options.IsFactoryStock(s.Name) && regionByName.ContainsKey(s.Name.Trim().ToLowerInvariant()))
            .Select(s => new StockRegion(regionByName[s.Name.Trim().ToLowerInvariant()].Id.ToString(), s.Name.Trim(), s.Id))
            .Where(r => !options.IsExcludedBranch(r.Name))
            .OrderBy(r => r.Name)
            .ToList();
        var factory = stocks.Where(s => options.IsFactoryStock(s.Name)).Select(s => new StockRegion("factory", s.Name.Trim(), s.Id)).FirstOrDefault();
        var regionStockIds = regionStocks.Select(r => r.StockId).ToHashSet();

        var balances = await db.LinkoProductBalances.AsNoTracking().Where(b => b.Balance != 0).ToListAsync(ct);
        var balanceOf = balances.GroupBy(b => (b.ProductId, b.StockId)).ToDictionary(g => g.Key, g => g.Sum(b => b.Balance));

        var weightFrom = today.AddDays(-UnitWeightDays);
        var unitWeights = (await (
                    from l in db.LinkoOrderLines
                    join o in db.LinkoOrders on l.OrderId equals o.Id
                    where sold.Contains(o.Status) && o.CreatedDate >= weightFrom && l.ProductId != null && l.Amount > 0
                    group l by l.ProductId into g
                    select new { ProductId = g.Key!.Value, Weight = g.Sum(x => x.TotalWeight), Amount = g.Sum(x => x.Amount) })
                .ToListAsync(ct))
            .ToDictionary(x => x.ProductId, x => StockMath.UnitKg(x.Weight, x.Amount));

        var velocity = (await (
                    from l in db.LinkoOrderLines
                    join o in db.LinkoOrders on l.OrderId equals o.Id
                    where sold.Contains(o.Status) && o.AcceptedDate >= velocityFrom && o.AcceptedDate <= velocityTo && l.ProductId != null
                          && (o.BranchName == null || !excluded.Contains(o.BranchName.ToLower()))
                    group l by new { o.BranchId, l.ProductId } into g
                    select new { g.Key.BranchId, ProductId = g.Key.ProductId!.Value, Kg = g.Sum(x => x.TotalWeight) })
                .ToListAsync(ct));
        var regionOfBranch = regions.ToDictionary(r => r.LinkoBranchId, r => r.Id.ToString());
        var perDay = velocity
            .Where(v => v.BranchId is { } b && regionOfBranch.ContainsKey(b))
            .GroupBy(v => (Region: regionOfBranch[v.BranchId!.Value], v.ProductId))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Kg) / options.StockVelocityDays);

        var products = await db.LinkoProducts.AsNoTracking().Select(p => new { p.Id, p.Name, p.Code, p.TypeId }).ToDictionaryAsync(p => p.Id, ct);
        var types = await db.LinkoProductTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, ct);
        var categories = SalesCategories.Build(options.Categories, types);

        var scopeRegions = regionId is null ? regionStocks : regionStocks.Where(r => r.Id == regionId).ToList();
        var scopeStockIds = scopeRegions.Select(r => r.StockId).ToHashSet();
        var scopeRegionIds = scopeRegions.Select(r => r.Id).ToHashSet();

        var productIds = balances.Where(b => regionStockIds.Contains(b.StockId) || b.StockId == factory?.StockId).Select(b => b.ProductId)
            .Concat(perDay.Keys.Where(k => scopeRegionIds.Contains(k.Region)).Select(k => k.ProductId))
            .Distinct();

        var items = new List<StockItem>();
        foreach (var productId in productIds)
        {
            var product = products.GetValueOrDefault(productId);
            var unitKg = unitWeights.GetValueOrDefault(productId);
            var pack = StockMath.ParsePack(product?.Name);
            var (boxKg, boxNote) = pack?.BoxKg is not { } parsedBox
                ? (null, "в названии нет веса коробки")
                : unitKg is null
                    ? (null, "нет продаж — вес штуки неизвестен")
                    : StockMath.BoxConsistent(parsedBox, unitKg.Value)
                        ? ((decimal?)parsedBox, $"по названию: {parsedBox:0.###} кг = {Math.Round(parsedBox / unitKg.Value):0} × {unitKg:0.###} кг")
                        : (null, $"в названии {parsedBox:0.###} кг — не делится на вес штуки {unitKg:0.###} кг, коробки не считаются");

            StockCell Cell(long stockId, string? region)
            {
                var pieces = balanceOf.GetValueOrDefault((productId, stockId));
                var kg = StockMath.Kg(pieces, unitKg);
                var day = region is null ? null : perDay.TryGetValue((region, productId), out var v) ? v : (decimal?)0;
                return new StockCell(pieces, kg, day, kg is { } k && day is > 0 ? k / day.Value : null);
            }

            var cells = regionStocks.ToDictionary(r => r.Id, r => Cell(r.StockId, r.Id));
            var scopeCells = scopeRegions.Select(r => cells[r.Id]).ToList();
            var pieces = scopeCells.Sum(c => c.Pieces);
            decimal? kgTotal = unitKg is null ? null : scopeCells.Sum(c => c.Kg ?? 0);
            var kgPerDay = scopeCells.Sum(c => c.KgPerDay ?? 0);
            var days = kgTotal is { } kt && kgPerDay > 0 ? kt / kgPerDay : (decimal?)null;
            var status = unitKg is null ? StockStatuses.Unknown
                : kgPerDay == 0 ? (pieces > 0 ? StockStatuses.Dead : StockStatuses.Ok)
                : days < 15 ? StockStatuses.Deficit
                : days > 30 ? StockStatuses.Overstock
                : StockStatuses.Ok;
            var group = categories.GroupOf(product?.TypeId);

            if (pieces == 0 && kgPerDay == 0)
            {
                continue; // в выбранной области ни остатка, ни продаж
            }

            items.Add(new StockItem(
                productId,
                product?.Name ?? $"Товар {productId}",
                product?.Code,
                categories.NameOf(group),
                SalesCategories.IsConfigured(group),
                unitKg,
                unitKg is null ? WeightSources.None : WeightSources.Orders,
                boxKg,
                boxNote,
                pieces,
                kgTotal,
                StockMath.Boxes(kgTotal, boxKg),
                kgPerDay,
                days,
                kgPerDay * 15,
                status,
                cells,
                factory is null ? null : Cell(factory.StockId, null)));
        }

        var others = balances
            .Where(b => !regionStockIds.Contains(b.StockId) && b.StockId != factory?.StockId)
            .GroupBy(b => b.StockId)
            .Select(g => new OtherStock(
                g.Key,
                stocks.FirstOrDefault(s => s.Id == g.Key)?.Name ?? $"Склад {g.Key}",
                g.Sum(b => b.Balance),
                g.All(b => unitWeights.GetValueOrDefault(b.ProductId) is not null) ? g.Sum(b => StockMath.Kg(b.Balance, unitWeights.GetValueOrDefault(b.ProductId)) ?? 0) : null,
                g.Count()))
            .OrderByDescending(o => o.Pieces)
            .ToList();

        var syncedAt = (await db.LinkoSyncStates.AsNoTracking().FirstOrDefaultAsync(s => s.Entity == "product_balances", ct))?.LastSuccessAt;

        return new StockView(
            syncedAt,
            options.StockVelocityDays,
            velocityFrom,
            velocityTo,
            regionStocks,
            factory,
            items.OrderBy(i => i.InReport ? 0 : 1).ThenBy(i => i.Category).ThenByDescending(i => i.Kg ?? 0).ToList(),
            others,
            Totals(items, i => (i.Kg, i.Boxes, i.KgPerDay, i.Status)),
            factory is null ? null : Totals(items.Where(i => i.Factory is not null), i => (i.Factory!.Kg, StockMath.Boxes(i.Factory.Kg, i.BoxKg), null, null)));
    }

    private static StockTotals Totals(IEnumerable<StockItem> source, Func<StockItem, (decimal? Kg, decimal? Boxes, decimal? PerDay, string? Status)> pick)
    {
        var rows = source.Select(pick).ToList();
        var kg = rows.Sum(r => r.Kg ?? 0);
        var perDay = rows.Sum(r => r.PerDay ?? 0);
        return new StockTotals(
            kg,
            rows.Sum(r => r.Boxes ?? 0),
            perDay == 0 ? null : perDay,
            perDay > 0 ? kg / perDay : null,
            rows.Count(r => r.Status == StockStatuses.Deficit),
            rows.Count(r => r.Status == StockStatuses.Overstock),
            rows.Count(r => r.Status == StockStatuses.Dead),
            rows.Count(r => r.Status == StockStatuses.Unknown));
    }
}
