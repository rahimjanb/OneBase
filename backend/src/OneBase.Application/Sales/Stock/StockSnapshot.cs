using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales.Metrics;

namespace OneBase.Application.Sales.Stock;

/// <summary>Склад Linko, сопоставленный с регионом (DirectionId — его РМ), заводом («factory») или экспортом («export»).</summary>
public sealed record StockRegion(string Id, string Name, long StockId, string? DirectionId = null);

/// <summary>Склад, не сопоставленный ни с регионом, ни с заводом: в остаток страны не входит, показывается отдельно. Region — чей это склад по Sales:StockRegionAliases.</summary>
public sealed record OtherStock(long StockId, string Name, decimal Pieces, decimal? Kg, int Items, string? Region = null);

/// <summary>
/// Товар в снимке: вес единицы учёта (UnitKg, откуда — UnitKgSource), коробка по правилу StockMath.BoxWeight, фасовка из названия (PackKg)
/// и входная цена дилера на начало текущего месяца (Price). Общие для остатка, аутстока и первички.
/// </summary>
public sealed record StockProduct(
    long Id,
    string Name,
    string? Code,
    long? TypeId,
    PackInfo? Pack,
    decimal? UnitKg,
    string UnitKgSource,
    decimal? BoxKg,
    string BoxNote,
    decimal? PackKg,
    decimal? Price);

/// <summary>
/// Снимок остатков Linko (product_balances, штуки) на момент последней загрузки: склады дилеров (по одному на регион), завод, экспорт,
/// прочие склады; у товаров — вес единицы, коробка и цена. Один снимок кормит и рекомендуемый остаток, и аутсток, поэтому их кг совпадают.
/// </summary>
public sealed record StockSnapshot(
    DateTimeOffset? SyncedAt,
    DateOnly SnapshotDate,
    DateTime SnapshotLocal,
    int ClosedYear,
    int ClosedMonth,
    DateOnly UnitWeightFrom,
    DateOnly PriceAsOf,
    string? PriceList,
    IReadOnlyList<StockRegion> Regions,
    StockRegion? Factory,
    StockRegion? Export,
    IReadOnlyList<OtherStock> OtherStocks,
    IReadOnlyDictionary<long, StockProduct> Products,
    IReadOnlyDictionary<(long ProductId, long StockId), decimal> Pieces,
    IReadOnlyList<RegionInfo> KeptRegions,
    IReadOnlyDictionary<long, Guid> BranchAliases)
{
    public decimal PiecesOf(long productId, long stockId) => Pieces.GetValueOrDefault((productId, stockId));

    /// <summary>Остаток в кг: штуки × вес единицы; null — веса нет.</summary>
    public decimal? KgOf(long productId, long stockId) => StockMath.Kg(PiecesOf(productId, stockId), Products.GetValueOrDefault(productId)?.UnitKg);

    /// <summary>Филиал Linko → регион (старые филиалы — в текущем регионе).</summary>
    public Dictionary<long, string> RegionOfBranch()
    {
        var map = KeptRegions.ToDictionary(r => r.BranchId, r => r.Id.ToString());
        foreach (var (branch, region) in BranchAliases)
        {
            map.TryAdd(branch, region.ToString());
        }

        return map;
    }
}

/// <summary>
/// Строит снимок остатков. Склад дилера — склад с названием региона (или склад, который Sales:StockRegionAliases относит к региону,
/// если склада с таким названием нет); склады старых филиалов («Жиззах (эски)»), «Основной», «Нукус (интеграция учун)» и прочие
/// в страну не входят. Вес единицы — по строкам заказов с 1-го числа последнего закрытого месяца, иначе за год, иначе из названия.
/// Цена — строка прайса Sales:StockPriceList, действовавшая на начало текущего месяца.
/// </summary>
public static class StockSnapshotBuilder
{
    /// <summary>Узбекистан — UTC+5: дата снимка считается по местному времени.</summary>
    public static readonly TimeSpan CompanyOffset = TimeSpan.FromHours(5);

    /// <summary>Запасное окно веса единицы — год продаж.</summary>
    public const int UnitWeightYearDays = 365;

    public const string CacheKey = "sales:stock:snapshot";

    public static Task<StockSnapshot> GetAsync(IMemoryCache cache, SalesCacheSignal signal, IAppDbContext db, SalesOptions options, CancellationToken ct) =>
        SalesViewCache.GetAsync(cache, signal, CacheKey, () => BuildAsync(db, options, ct));

    public static async Task<StockSnapshot> BuildAsync(IAppDbContext db, SalesOptions options, CancellationToken ct)
    {
        var syncedAt = (await db.LinkoSyncStates.AsNoTracking().FirstOrDefaultAsync(s => s.Entity == "product_balances", ct))?.LastSuccessAt;
        var snapshotLocal = (syncedAt ?? DateTimeOffset.UtcNow).ToOffset(CompanyOffset).DateTime;
        var snapshotDate = DateOnly.FromDateTime(snapshotLocal);
        var (closedYear, closedMonth) = StockMath.ClosedMonth(snapshotDate);
        var unitFrom = new DateOnly(closedYear, closedMonth, 1);
        var priceAsOf = new DateOnly(snapshotDate.Year, snapshotDate.Month, 1);
        var priceCutoff = StockMath.UnixSeconds(priceAsOf, CompanyOffset);

        var regionRows = await db.SalesRegions.AsNoTracking().Select(r => new { r.Id, r.Name, r.LinkoBranchId, r.DirectionId, r.SupervisorName, r.DealerName }).ToListAsync(ct);
        var (kept, aliases) = OldBranches.Merge(regionRows.Select(r => new RegionInfo(r.Id, r.LinkoBranchId, r.Name, r.DirectionId, r.SupervisorName, r.DealerName)).ToList(), options.OldBranchSuffix);
        var dealerRegions = kept.Where(r => !options.IsExcludedBranch(r.Name) && !options.IsIgnoredBranch(r.Name)).ToList();

        var stocks = (await db.LinkoStocks.AsNoTracking().Select(s => new { s.Id, s.Name }).ToListAsync(ct)).Select(s => (s.Id, Name: s.Name.Trim())).ToList();
        var factory = stocks.Where(s => options.IsFactoryStock(s.Name)).Select(s => new StockRegion("factory", s.Name, s.Id)).FirstOrDefault();
        var export = stocks.Where(s => options.IsExportStock(s.Name)).Select(s => new StockRegion("export", s.Name, s.Id)).FirstOrDefault();
        var regions = MatchDealerStocks(dealerRegions, stocks, factory?.StockId, export?.StockId, options);

        var balances = await db.LinkoProductBalances.AsNoTracking().Where(b => b.Balance != 0).ToListAsync(ct);
        var pieces = balances.GroupBy(b => (b.ProductId, b.StockId)).ToDictionary(g => g.Key, g => g.Sum(b => b.Balance));

        var sold = options.SoldStatuses;
        var yearFrom = snapshotDate.AddDays(-UnitWeightYearDays);

        // Коробка по отгрузкам завода за год — на случай, когда название коробки не даёт (StockMath.BoxUnitsFromShipments).
        var shipped = options.ShippedTransferStatuses;
        var boxUnits = new Dictionary<long, decimal?>();
        if (factory is { } factoryStock)
        {
            var factoryStockId = factoryStock.StockId;
            boxUnits = (await (
                    from l in db.LinkoStockTransferLines
                    join t in db.LinkoStockTransfers on l.TransferId equals t.Id
                    where shipped.Contains(t.Status) && t.FromStockId == factoryStockId && t.CreatedDate >= yearFrom && l.ProductId != null && l.Amount > 0
                    select new { ProductId = l.ProductId!.Value, l.Amount })
                .ToListAsync(ct))
                .GroupBy(x => x.ProductId)
                .ToDictionary(g => g.Key, g => StockMath.BoxUnitsFromShipments(g.Select(x => x.Amount).ToList()));
        }
        var weights = await (
                from l in db.LinkoOrderLines
                join o in db.LinkoOrders on l.OrderId equals o.Id
                where sold.Contains(o.Status) && o.CreatedDate >= yearFrom && l.ProductId != null && l.Amount > 0
                group l by new { l.ProductId, Recent = o.CreatedDate >= unitFrom } into g
                select new { ProductId = g.Key.ProductId!.Value, g.Key.Recent, Weight = g.Sum(x => x.TotalWeight), Amount = g.Sum(x => x.Amount) })
            .ToListAsync(ct);
        var recentWeight = weights.Where(w => w.Recent).ToDictionary(w => w.ProductId, w => StockMath.UnitKg(w.Weight, w.Amount));
        var yearWeight = weights.GroupBy(w => w.ProductId).ToDictionary(g => g.Key, g => StockMath.UnitKg(g.Sum(w => w.Weight), g.Sum(w => w.Amount)));

        var priceList = (await db.LinkoPriceLists.AsNoTracking().Select(p => new { p.Id, p.Name }).ToListAsync(ct))
            .FirstOrDefault(p => string.Equals(p.Name.Trim(), options.StockPriceList.Trim(), StringComparison.OrdinalIgnoreCase));
        var prices = priceList is null
            ? new Dictionary<long, decimal>()
            : (await db.LinkoPriceListItems.AsNoTracking()
                    .Where(i => i.PriceListId == priceList.Id && i.ProductId != null)
                    .Select(i => new { ProductId = i.ProductId!.Value, i.Price, i.Tm })
                    .ToListAsync(ct))
                .GroupBy(i => i.ProductId)
                .Select(g => (g.Key, Price: StockMath.PriceAsOf(g.Select(i => (i.Price, i.Tm)).ToList(), priceCutoff)))
                .Where(x => x.Price is not null)
                .ToDictionary(x => x.Key, x => x.Price!.Value);

        var products = (await db.LinkoProducts.AsNoTracking().Select(p => new { p.Id, p.Name, p.Code, p.TypeId }).ToListAsync(ct))
            .Select(p =>
            {
                var pack = StockMath.ParsePack(p.Name);
                var (unitKg, source) = StockMath.UnitWeight(recentWeight.GetValueOrDefault(p.Id), yearWeight.GetValueOrDefault(p.Id), pack);
                var (boxKg, boxNote) = StockMath.BoxWeight(pack, unitKg, boxUnits.GetValueOrDefault(p.Id));
                return new StockProduct(p.Id, p.Name, p.Code, p.TypeId, pack, unitKg, source, boxKg, boxNote, StockMath.PackKg(p.Name),
                    prices.TryGetValue(p.Id, out var price) ? price : null);
            })
            .ToDictionary(p => p.Id);

        var known = regions.Select(r => r.StockId).Concat(new[] { factory?.StockId ?? -1, export?.StockId ?? -1 }).ToHashSet();
        var regionNames = dealerRegions.Select(r => r.Name.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var others = balances
            .Where(b => !known.Contains(b.StockId))
            .GroupBy(b => b.StockId)
            .Select(g =>
            {
                var name = stocks.FirstOrDefault(s => s.Id == g.Key).Name ?? $"Склад {g.Key}";
                var region = options.StockRegionName(name);
                return new OtherStock(
                    g.Key,
                    name,
                    g.Sum(b => b.Balance),
                    g.All(b => products.GetValueOrDefault(b.ProductId)?.UnitKg is not null) ? g.Sum(b => StockMath.Kg(b.Balance, products[b.ProductId].UnitKg) ?? 0) : null,
                    g.Count(),
                    !string.Equals(region, name, StringComparison.OrdinalIgnoreCase) && regionNames.Contains(region) ? region : null);
            })
            .OrderByDescending(o => o.Pieces)
            .ToList();

        return new StockSnapshot(syncedAt, snapshotDate, snapshotLocal, closedYear, closedMonth, unitFrom, priceAsOf, priceList?.Name.Trim(),
            regions, factory, export, others, products, pieces, kept, aliases);
    }

    /// <summary>
    /// Склады дилеров: у региона справочника — склад Linko с его названием (точное имя в приоритете) или склад, который
    /// Sales:StockRegionAliases относит к региону («Коканд бозор» → Коканд); не завод, не экспорт, не склад старого филиала
    /// (Sales:OldBranchSuffix — «Жиззах (эски)»: он уходит в «прочие склады», даже если по алиасу относится к региону). Один склад — одному
    /// региону; регион без склада пропускается. По алфавиту.
    /// </summary>
    public static List<StockRegion> MatchDealerStocks(
        IEnumerable<RegionInfo> dealerRegions,
        IReadOnlyList<(long Id, string Name)> stocks,
        long? factoryStockId,
        long? exportStockId,
        SalesOptions options)
    {
        var old = string.IsNullOrWhiteSpace(options.OldBranchSuffix) ? null : new System.Text.RegularExpressions.Regex(options.OldBranchSuffix, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        var taken = new HashSet<long>();
        var regions = new List<StockRegion>();
        foreach (var region in dealerRegions)
        {
            var name = region.Name.Trim();
            var stock = stocks
                .Where(s => !taken.Contains(s.Id) && s.Id != factoryStockId && s.Id != exportStockId
                            && string.Equals(options.StockRegionName(s.Name), name, StringComparison.OrdinalIgnoreCase)
                            && (old is null || !old.IsMatch(s.Name)))
                .OrderBy(s => string.Equals(s.Name.Trim(), name, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .Select(s => (long?)s.Id)
                .FirstOrDefault();
            if (stock is not { } id)
            {
                continue; // у региона нет склада в Linko
            }

            taken.Add(id);
            regions.Add(new StockRegion(region.Id.ToString(), name, id, region.DirectionId?.ToString()));
        }

        return regions.OrderBy(r => r.Name).ToList();
    }
}
