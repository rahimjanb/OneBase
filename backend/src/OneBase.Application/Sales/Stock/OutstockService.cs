using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales.Metrics;

namespace OneBase.Application.Sales.Stock;

/// <summary>
/// Пара «товар × регион» за период: дни в нуле, упущенные продажи и чья это потеря.
/// Days — флаги по дням периода: «1» — товар утром был, «0» — не было.
/// </summary>
public sealed record OutstockPair(
    string RegionId,
    string Region,
    long ProductId,
    string Product,
    string? Code,
    string Category,
    bool InReport,
    decimal PeriodKg,
    decimal PerDayKg,
    decimal? AvgPrice,
    int ZeroDays,
    int DealerDays,
    int FactoryDays,
    decimal LostKg,
    decimal LostSum,
    decimal DealerLossSum,
    decimal FactoryLossSum,
    bool Core,
    bool Chronic,
    decimal SnapshotKg,
    string Days);

public sealed record OutstockGroup(string Id, string Name, int Pairs, int CorePairs, int Chronic, decimal LostKg, decimal LostSum, decimal DealerLossSum, decimal FactoryLossSum);

public sealed record OutstockTotals(
    decimal LostKg,
    decimal LostSum,
    int Pairs,
    int PairsWithLoss,
    int CorePairs,
    decimal CoreSum,
    int Chronic,
    decimal DealerLossSum,
    decimal FactoryLossSum,
    int Cells,
    int NegativeCells,
    decimal? NegativeSharePct);

public sealed record OutstockView(
    int Year,
    int Month,
    DateOnly From,
    DateOnly To,
    int Days,
    int DaysInMonth,
    DateOnly SnapshotDate,
    DateTimeOffset? SyncedAt,
    string? RegionId,
    bool FactoryKnown,
    IReadOnlyList<UnitRef> Regions,
    OutstockTotals Totals,
    IReadOnlyList<OutstockPair> Pairs,
    IReadOnlyList<OutstockGroup> ByRegion,
    IReadOnlyList<OutstockGroup> ByProduct);

/// <summary>
/// Аутсток за месяц. Остаток дилера по дням восстанавливается назад от снимка Linko (product_balances) по продажам
/// (заказы по дате приёмки) и приходу с завода (перемещения «Завод → склад региона» минус возвраты). Остаток завода —
/// так же от его снимка по перемещениям; выпуска цехов в Linko нет, поэтому прошлый остаток завода завышен, а доля
/// его потерь — оценка снизу. Считаются только товары, которые регион в периоде продавал.
/// </summary>
public sealed class OutstockService(IAppDbContext db, SalesOptions options, StockService stock, SalesDataLoader loader, IMemoryCache cache, SalesCacheSignal signal)
{
    /// <summary>Узбекистан — UTC+5: дата снимка остатков считается по местному времени.</summary>
    private static readonly TimeSpan CompanyOffset = TimeSpan.FromHours(5);

    public async Task<OutstockView> GetAsync(int? year, int? month, string? regionId, CancellationToken ct = default)
    {
        var months = await loader.MonthsAsync(ct);
        var latest = months.FirstOrDefault();
        var y = year ?? latest?.Year ?? DateTime.Today.Year;
        var m = month is >= 1 and <= 12 ? month.Value : latest?.Month ?? DateTime.Today.Month;
        return await SalesViewCache.GetAsync(cache, signal, $"sales:outstock:{y}-{m}:{regionId}", () => BuildAsync(y, m, regionId, ct));
    }

    public async Task<OutstockView> BuildAsync(int year, int month, string? regionId, CancellationToken ct = default)
    {
        var view = await stock.GetAsync(null, ct);
        var snapshotDate = view.SyncedAt is { } at ? DateOnly.FromDateTime(at.ToOffset(CompanyOffset).DateTime) : DateOnly.FromDateTime(DateTime.Today);
        var monthStart = new DateOnly(year, month, 1);
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var monthEnd = new DateOnly(year, month, daysInMonth);
        var to = snapshotDate < monthEnd ? snapshotDate : monthEnd;
        var regions = view.Regions.Select(r => new UnitRef(r.Id, r.Name)).ToList();
        if (to < monthStart)
        {
            return Empty(year, month, monthStart, daysInMonth, snapshotDate, view, regionId, regions);
        }

        // Диапазон восстановления — от первого дня месяца до снимка (у закрытого месяца — через все дни после него).
        var n = snapshotDate.DayNumber - monthStart.DayNumber + 1;
        var analyzed = to.DayNumber - monthStart.DayNumber + 1;
        int Index(DateOnly d) => d.DayNumber - monthStart.DayNumber;

        var sold = options.SoldStatuses;
        var excluded = options.ExcludedBranches.Select(b => b.Trim().ToLowerInvariant()).ToArray();

        // Филиал Linko → регион, включая старые филиалы («Жиззах (эски)») — как во вторичке.
        var regionRows = await db.SalesRegions.AsNoTracking().Select(r => new { r.Id, r.Name, r.LinkoBranchId, r.DirectionId, r.SupervisorName, r.DealerName }).ToListAsync(ct);
        var (kept, aliases) = OldBranches.Merge(regionRows.Select(r => new RegionInfo(r.Id, r.LinkoBranchId, r.Name, r.DirectionId, r.SupervisorName, r.DealerName)).ToList(), options.OldBranchSuffix);
        var regionOfBranch = kept.ToDictionary(r => r.BranchId, r => r.Id.ToString());
        foreach (var (branch, region) in aliases)
        {
            regionOfBranch.TryAdd(branch, region.ToString());
        }

        // Продажи по дням: заказы дилеров магазинам по дате приёмки, без филиалов «Завод» и «Экспорт».
        var sales = await (
                from l in db.LinkoOrderLines
                join o in db.LinkoOrders on l.OrderId equals o.Id
                where sold.Contains(o.Status) && o.AcceptedDate >= monthStart && o.AcceptedDate <= snapshotDate && l.ProductId != null
                      && (o.BranchName == null || !excluded.Contains(o.BranchName.ToLower()))
                group l by new { o.AcceptedDate, o.BranchId, l.ProductId } into g
                select new { Date = g.Key.AcceptedDate!.Value, g.Key.BranchId, ProductId = g.Key.ProductId!.Value, Kg = g.Sum(x => x.TotalWeight), Sum = g.Sum(x => x.TotalPrice) })
            .ToListAsync(ct);

        var soldByDay = new Dictionary<(string Region, long Product), decimal[]>();
        var revenue = new Dictionary<(string Region, long Product), (decimal Kg, decimal Sum)>();
        foreach (var s in sales)
        {
            if (s.BranchId is not { } branch || !regionOfBranch.TryGetValue(branch, out var region))
            {
                continue;
            }

            var key = (region, s.ProductId);
            if (!soldByDay.TryGetValue(key, out var arr))
            {
                soldByDay[key] = arr = new decimal[n];
            }

            var i = Index(s.Date);
            arr[i] += s.Kg;
            if (i < analyzed)
            {
                var r = revenue.GetValueOrDefault(key);
                revenue[key] = (r.Kg + s.Kg, r.Sum + s.Sum);
            }
        }

        // Приход дилеру и движения завода — перемещения в статусах отгрузки; дата — выдача, без неё приёмка, без неё создание.
        var regionOfStock = view.Regions.ToDictionary(r => r.StockId, r => r.Id);
        var factoryStock = view.Factory?.StockId;
        var shipped = options.ShippedTransferStatuses;
        var coarseFrom = monthStart.AddDays(-45);
        var transfers = await (
                from l in db.LinkoStockTransferLines
                join t in db.LinkoStockTransfers on l.TransferId equals t.Id
                where shipped.Contains(t.Status) && l.ProductId != null && t.CreatedDate >= coarseFrom
                select new { t.FromStockId, t.ToStockId, t.GivenAt, t.AcceptedAt, t.CreatedAt, ProductId = l.ProductId!.Value, l.TotalWeight })
            .ToListAsync(ct);

        var receivedByDay = new Dictionary<(string Region, long Product), decimal[]>();
        var factoryOut = new Dictionary<long, decimal[]>();
        var factoryIn = new Dictionary<long, decimal[]>();
        decimal[] Series<TKey>(Dictionary<TKey, decimal[]> map, TKey key) where TKey : notnull
        {
            if (!map.TryGetValue(key, out var arr))
            {
                map[key] = arr = new decimal[n];
            }

            return arr;
        }

        foreach (var t in transfers)
        {
            var when = t.GivenAt ?? t.AcceptedAt ?? t.CreatedAt;
            if (when is null)
            {
                continue;
            }

            var date = DateOnly.FromDateTime(when.Value);
            if (date < monthStart || date > snapshotDate)
            {
                continue;
            }

            var i = Index(date);
            var fromFactory = factoryStock is { } f && t.FromStockId == f;
            var toFactory = factoryStock is { } f2 && t.ToStockId == f2;
            if (fromFactory)
            {
                Series(factoryOut, t.ProductId)[i] += t.TotalWeight;
                if (t.ToStockId is { } toStock && regionOfStock.TryGetValue(toStock, out var region))
                {
                    Series(receivedByDay, (region, t.ProductId))[i] += t.TotalWeight;
                }
            }

            if (toFactory)
            {
                Series(factoryIn, t.ProductId)[i] += t.TotalWeight;
                if (t.FromStockId is { } fromStock && regionOfStock.TryGetValue(fromStock, out var region))
                {
                    Series(receivedByDay, (region, t.ProductId))[i] -= t.TotalWeight; // возврат дилера заводу
                }
            }
        }

        // Снимки: остаток дилера и завода в кг (штуки × вес единицы) — из рекомендуемого остатка.
        var itemOf = view.Items.ToDictionary(i => i.ProductId);
        var products = await db.LinkoProducts.AsNoTracking().Select(p => new { p.Id, p.Name, p.Code, p.TypeId }).ToDictionaryAsync(p => p.Id, ct);
        var types = await db.LinkoProductTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, ct);
        var categories = SalesCategories.Build(options.Categories, types);
        var factoryMorning = new Dictionary<long, decimal[]>();
        var factoryKnown = view.Factory is not null;

        decimal[] FactoryMorning(long productId)
        {
            if (factoryMorning.TryGetValue(productId, out var cached))
            {
                return cached;
            }

            var item = itemOf.GetValueOrDefault(productId);
            var snapshot = item?.Factory?.Kg ?? 0;
            var (morning, _) = OutstockMath.Reconstruct(snapshot, factoryOut.GetValueOrDefault(productId) ?? new decimal[n], factoryIn.GetValueOrDefault(productId) ?? new decimal[n]);
            return factoryMorning[productId] = morning;
        }

        var scope = regionId is null ? regions : regions.Where(r => r.Id == regionId).ToList();
        var regionName = regions.ToDictionary(r => r.Id, r => r.Name);
        var pairs = new List<OutstockPair>();
        var cells = 0;
        var negativeCells = 0;
        foreach (var (key, soldArr) in soldByDay)
        {
            if (!regionName.ContainsKey(key.Region) || (regionId is not null && key.Region != regionId))
            {
                continue;
            }

            var (periodKg, periodSum) = revenue.GetValueOrDefault(key);
            if (periodKg <= 0)
            {
                continue; // товар, который регион в периоде не продавал, аутстоком не считается
            }

            var item = itemOf.GetValueOrDefault(key.Product);
            var snapshotKg = item is not null && item.Regions.TryGetValue(key.Region, out var cell) ? cell.Kg ?? 0 : 0;
            var (morning, negative) = OutstockMath.Reconstruct(snapshotKg, soldArr, receivedByDay.GetValueOrDefault(key) ?? new decimal[n]);
            var factory = factoryKnown ? FactoryMorning(key.Product) : null;

            var flags = new char[analyzed];
            int zero = 0, dealer = 0, factoryDays = 0;
            for (var i = 0; i < analyzed; i++)
            {
                var present = OutstockMath.InStock(morning[i]);
                flags[i] = present ? '1' : '0';
                if (negative[i])
                {
                    negativeCells++;
                }

                if (present)
                {
                    continue;
                }

                zero++;
                if (factory is null || OutstockMath.InStock(factory[i]))
                {
                    dealer++; // на заводе товар был (или склад завода неизвестен) — недовоз
                }
                else
                {
                    factoryDays++;
                }
            }

            cells += analyzed;
            var perDay = periodKg / analyzed;
            var avgPrice = periodKg > 0 ? periodSum / periodKg : (decimal?)null;
            var lostKg = OutstockMath.LostKg(zero, periodKg, analyzed);
            var lostSum = avgPrice is { } price ? lostKg * price : 0;
            var product = products.GetValueOrDefault(key.Product);
            var group = categories.GroupOf(product?.TypeId);
            pairs.Add(new OutstockPair(
                key.Region,
                regionName[key.Region],
                key.Product,
                product?.Name ?? $"Товар {key.Product}",
                product?.Code,
                categories.NameOf(group),
                SalesCategories.IsConfigured(group),
                periodKg,
                perDay,
                avgPrice,
                zero,
                dealer,
                factoryDays,
                lostKg,
                lostSum,
                zero == 0 ? 0 : lostSum * dealer / zero,
                zero == 0 ? 0 : lostSum * factoryDays / zero,
                false,
                OutstockMath.IsChronic(zero, analyzed),
                snapshotKg,
                new string(flags)));
        }

        // Ядро потерь: самые дорогие пары, которые вместе дают 80% упущенного.
        var ordered = pairs.OrderByDescending(p => p.LostSum).ThenByDescending(p => p.LostKg).ThenBy(p => p.Region).ThenBy(p => p.Product).ToList();
        var coreCount = OutstockMath.CoreCount(ordered.Select(p => p.LostSum).ToList());
        ordered = ordered.Select((p, i) => i < coreCount && p.LostSum > 0 ? p with { Core = true } : p).ToList();

        var withLoss = ordered.Where(p => p.ZeroDays > 0).ToList();
        var totals = new OutstockTotals(
            ordered.Sum(p => p.LostKg),
            ordered.Sum(p => p.LostSum),
            ordered.Count,
            withLoss.Count,
            ordered.Count(p => p.Core),
            ordered.Where(p => p.Core).Sum(p => p.LostSum),
            ordered.Count(p => p.Chronic),
            ordered.Sum(p => p.DealerLossSum),
            ordered.Sum(p => p.FactoryLossSum),
            cells,
            negativeCells,
            cells == 0 ? null : Math.Round(100m * negativeCells / cells, 1));

        return new OutstockView(
            year,
            month,
            monthStart,
            to,
            analyzed,
            daysInMonth,
            snapshotDate,
            view.SyncedAt,
            regionId,
            factoryKnown,
            regions,
            totals,
            ordered.Where(p => p.ZeroDays > 0).ToList(), // пары без дней в нуле — только в итогах и группах, в списке они не нужны
            Group(ordered, p => (p.RegionId, p.Region)),
            Group(ordered, p => (p.ProductId.ToString(), p.Product)));
    }

    private static List<OutstockGroup> Group(IEnumerable<OutstockPair> pairs, Func<OutstockPair, (string Id, string Name)> key) =>
        pairs.GroupBy(key)
            .Select(g => new OutstockGroup(g.Key.Id, g.Key.Name, g.Count(), g.Count(p => p.Core), g.Count(p => p.Chronic),
                g.Sum(p => p.LostKg), g.Sum(p => p.LostSum), g.Sum(p => p.DealerLossSum), g.Sum(p => p.FactoryLossSum)))
            .OrderByDescending(g => g.LostSum)
            .ThenBy(g => g.Name)
            .ToList();

    private static OutstockView Empty(int year, int month, DateOnly monthStart, int daysInMonth, DateOnly snapshotDate, StockView view, string? regionId, IReadOnlyList<UnitRef> regions) =>
        new(year, month, monthStart, monthStart, 0, daysInMonth, snapshotDate, view.SyncedAt, regionId, view.Factory is not null, regions,
            new OutstockTotals(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null), [], [], []);
}
