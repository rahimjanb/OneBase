using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales.Metrics;

namespace OneBase.Application.Sales.Stock;

/// <summary>
/// Пара «товар × регион» за период: дни в нуле, упущенные продажи и чья это потеря.
/// Days — флаги по дням периода: «1» — товар утром был, «0» — не было. Received — «1» в дни, когда с завода привезли.
/// </summary>
public sealed record OutstockPair(
    string RegionId,
    string Region,
    long ProductId,
    string Product,
    string? Code,
    string Category,
    bool Top,
    decimal PeriodKg,
    decimal PeriodSum,
    decimal PerDayKg,
    decimal? AvgPrice,
    int ZeroDays,
    int DealerDays,
    int FactoryDays,
    int NegativeDays,
    decimal LostKg,
    decimal LostSum,
    decimal DealerLossSum,
    decimal FactoryLossSum,
    bool Core,
    bool Chronic,
    decimal SnapshotKg,
    string Days,
    string Received);

/// <summary>Карточка категории: Share — доля во всех потерях области, LossShare — упущенные кг к проданным («к факту»).</summary>
public sealed record OutstockCategory(string Name, decimal LostKg, decimal LostSum, decimal SoldKg, decimal Share, decimal? LossShare, int Pairs, int ZeroDays, bool Selected);

/// <summary>Товар из тройки самых дорогих потерь региона; Share — доля в потерях региона.</summary>
public sealed record OutstockTopProduct(long ProductId, string Name, string? Code, bool Top, decimal LostSum, decimal Share);

public sealed record OutstockRegion(
    string Id,
    string Name,
    string? Dealer,
    decimal SoldKg,
    decimal LostKg,
    decimal LostSum,
    decimal? LossShare,
    int ZeroDays,
    int Pairs,
    int CorePairs,
    int Chronic,
    decimal DealerLossSum,
    decimal FactoryLossSum,
    IReadOnlyList<OutstockTopProduct> Top);

/// <summary>Товар по всем регионам: ZeroShare — доля дней в нуле среди дней всех регионов, где товар продавался.</summary>
public sealed record OutstockProduct(
    long Id,
    string Name,
    string? Code,
    string Category,
    bool Top,
    decimal SoldKg,
    decimal? ZeroShare,
    decimal LostKg,
    decimal LostSum,
    int Regions,
    int RegionsSold,
    int CorePairs,
    int Chronic,
    decimal DealerLossSum,
    decimal FactoryLossSum);

public sealed record OutstockMatrixRow(string Id, string Name, string? Dealer, IReadOnlyList<decimal> Sum, IReadOnlyList<decimal> Kg, decimal TotalSum, decimal TotalKg);

/// <summary>Дилеры × категории: упущено по каждой категории у каждого региона, в сумах и в кг.</summary>
public sealed record OutstockMatrix(IReadOnlyList<string> Categories, IReadOnlyList<OutstockMatrixRow> Rows, IReadOnlyList<decimal> TotalSum, IReadOnlyList<decimal> TotalKg);

public sealed record OutstockTotals(
    decimal LostKg,
    decimal LostSum,
    decimal SoldKg,
    decimal SoldSum,
    decimal? LossShare,
    int Pairs,
    int PairsWithLoss,
    int ProductsWithLoss,
    int RegionsWithLoss,
    int ZeroDays,
    int DealerDays,
    int FactoryDays,
    int CorePairs,
    decimal CoreSum,
    int Chronic,
    decimal DealerLossSum,
    decimal FactoryLossSum,
    int Cells,
    int NegativeCells,
    decimal? NegativeSharePct);

public sealed record OutstockRegionRef(string Id, string Name, string? Dealer);

public static class OutstockScope
{
    public const string Top = "top";
    public const string All = "all";
    public const string Rest = "rest";
}

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
    string Scope,
    IReadOnlyList<string> SelectedCategories,
    bool TopConfigured,
    string TopHint,
    bool FactoryKnown,
    IReadOnlyList<OutstockRegionRef> Regions,
    OutstockTotals Totals,
    IReadOnlyList<OutstockCategory> Categories,
    IReadOnlyList<OutstockPair> Pairs,
    IReadOnlyList<OutstockRegion> ByRegion,
    IReadOnlyList<OutstockProduct> ByProduct,
    OutstockMatrix Matrix);

/// <summary>Восстановленный месяц целиком — все пары с продажами по всем регионам; регион, ТОП и категории накладываются сверху без пересчёта.</summary>
public sealed record OutstockBase(
    int Year,
    int Month,
    DateOnly From,
    DateOnly To,
    int Days,
    int DaysInMonth,
    DateOnly SnapshotDate,
    DateTimeOffset? SyncedAt,
    bool FactoryKnown,
    bool TopConfigured,
    string TopHint,
    IReadOnlyList<OutstockRegionRef> Regions,
    IReadOnlyList<OutstockPair> Pairs);

/// <summary>
/// Аутсток за месяц. Остаток дилера по дням восстанавливается назад от снимка Linko (product_balances) по продажам
/// (заказы по дате приёмки) и приходу с завода (перемещения «Завод → склад региона» минус возвраты). Остаток завода —
/// так же от его снимка по перемещениям; выпуска цехов в Linko нет, поэтому прошлый остаток завода завышен, а доля
/// его потерь — оценка снизу. Считаются только товары, которые регион в периоде продавал.
/// ТОП-товары — коды из Sales:TopProducts, а без них — все товары категорий отчёта.
/// </summary>
public sealed class OutstockService(IAppDbContext db, SalesOptions options, StockService stock, SalesDataLoader loader, IMemoryCache cache, SalesCacheSignal signal)
{
    /// <summary>Узбекистан — UTC+5: дата снимка остатков считается по местному времени.</summary>
    private static readonly TimeSpan CompanyOffset = TimeSpan.FromHours(5);

    /// <summary>Карточка категории показывается, если на неё приходится хотя бы столько потерь области (мелочь — только через «Все пары»).</summary>
    private const decimal MinCardShare = 0.005m;

    public async Task<OutstockView> GetAsync(int? year, int? month, string? regionId, string? scope = null, IReadOnlyList<string>? categories = null, CancellationToken ct = default)
    {
        var months = await loader.MonthsAsync(ct);
        var latest = months.FirstOrDefault();
        var y = year ?? latest?.Year ?? DateTime.Today.Year;
        var m = month is >= 1 and <= 12 ? month.Value : latest?.Month ?? DateTime.Today.Month;
        var b = await SalesViewCache.GetAsync(cache, signal, $"sales:outstock:{y}-{m}", () => BuildAsync(y, m, ct));
        return Compose(b, regionId, scope, categories ?? []);
    }

    /// <summary>Категории из адреса: «Кекс,Помадка».</summary>
    public static IReadOnlyList<string> ParseCategories(string? cat) =>
        string.IsNullOrWhiteSpace(cat) ? [] : cat.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Область страницы поверх восстановленного месяца: регион, ТОП/все/кроме ТОПа, выбранные категории.</summary>
    public static OutstockView Compose(OutstockBase b, string? regionId, string? scope, IReadOnlyList<string> selectedCategories)
    {
        var s = b.TopConfigured
            ? scope switch { OutstockScope.All => OutstockScope.All, OutstockScope.Rest => OutstockScope.Rest, _ => OutstockScope.Top }
            : OutstockScope.All;
        var chosen = selectedCategories.Select(c => c.Trim()).Where(c => c.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dealerOf = b.Regions.ToDictionary(r => r.Id, r => r.Dealer);

        var scoped = b.Pairs
            .Where(p => (regionId is null || p.RegionId == regionId) && (s == OutstockScope.All || p.Top == (s == OutstockScope.Top)))
            .ToList();

        // Карточки категорий — до фильтра по категориям: по ним и выбирают.
        var scopedLoss = scoped.Sum(p => p.LostSum);
        var categories = scoped.GroupBy(p => p.Category)
            .Select(g =>
            {
                var lost = g.Sum(p => p.LostSum);
                var lostKg = g.Sum(p => p.LostKg);
                var sold = g.Sum(p => p.PeriodKg);
                return new OutstockCategory(g.Key, lostKg, lost, sold, scopedLoss > 0 ? lost / scopedLoss : 0, sold > 0 ? lostKg / sold : null,
                    g.Count(p => p.ZeroDays > 0), g.Sum(p => p.ZeroDays), chosen.Contains(g.Key));
            })
            .Where(c => c.Selected || c.Share >= MinCardShare)
            .OrderByDescending(c => c.LostSum)
            .ThenBy(c => c.Name)
            .ToList();
        var selected = categories.Where(c => c.Selected).Select(c => c.Name).ToList();

        var filtered = chosen.Count == 0 ? scoped : scoped.Where(p => chosen.Contains(p.Category)).ToList();

        // Ядро потерь: самые дорогие пары, которые вместе дают 80% упущенного — внутри выбранной области.
        var ordered = filtered.OrderByDescending(p => p.LostSum).ThenByDescending(p => p.LostKg).ThenBy(p => p.Region).ThenBy(p => p.Product).ToList();
        var coreCount = OutstockMath.CoreCount(ordered.Select(p => p.LostSum).ToList());
        ordered = ordered.Select((p, i) => i < coreCount && p.LostSum > 0 ? p with { Core = true } : p).ToList();
        var withLoss = ordered.Where(p => p.ZeroDays > 0).ToList();

        var soldKg = ordered.Sum(p => p.PeriodKg);
        var lostKgTotal = ordered.Sum(p => p.LostKg);
        var cells = ordered.Count * b.Days;
        var negative = ordered.Sum(p => p.NegativeDays);
        var totals = new OutstockTotals(
            lostKgTotal,
            ordered.Sum(p => p.LostSum),
            soldKg,
            ordered.Sum(p => p.PeriodSum),
            soldKg > 0 ? lostKgTotal / soldKg : null,
            ordered.Count,
            withLoss.Count,
            withLoss.Select(p => p.ProductId).Distinct().Count(),
            withLoss.Select(p => p.RegionId).Distinct().Count(),
            withLoss.Sum(p => p.ZeroDays),
            withLoss.Sum(p => p.DealerDays),
            withLoss.Sum(p => p.FactoryDays),
            ordered.Count(p => p.Core),
            ordered.Where(p => p.Core).Sum(p => p.LostSum),
            ordered.Count(p => p.Chronic),
            ordered.Sum(p => p.DealerLossSum),
            ordered.Sum(p => p.FactoryLossSum),
            cells,
            negative,
            cells == 0 ? null : Math.Round(100m * negative / cells, 1));

        var byRegion = ordered.GroupBy(p => (p.RegionId, p.Region))
            .Select(g =>
            {
                var lost = g.Sum(p => p.LostSum);
                var lostKg = g.Sum(p => p.LostKg);
                var sold = g.Sum(p => p.PeriodKg);
                var top = g.Where(p => p.LostSum > 0).OrderByDescending(p => p.LostSum).Take(3)
                    .Select(p => new OutstockTopProduct(p.ProductId, p.Product, p.Code, p.Top, p.LostSum, lost > 0 ? p.LostSum / lost : 0))
                    .ToList();
                return new OutstockRegion(g.Key.RegionId, g.Key.Region, dealerOf.GetValueOrDefault(g.Key.RegionId), sold, lostKg, lost, sold > 0 ? lostKg / sold : null,
                    g.Sum(p => p.ZeroDays), g.Count(p => p.ZeroDays > 0), g.Count(p => p.Core), g.Count(p => p.Chronic),
                    g.Sum(p => p.DealerLossSum), g.Sum(p => p.FactoryLossSum), top);
            })
            .OrderByDescending(r => r.LostSum)
            .ThenBy(r => r.Name)
            .ToList();

        var byProduct = ordered.GroupBy(p => p.ProductId)
            .Select(g =>
            {
                var first = g.First();
                var days = g.Count() * b.Days;
                var zero = g.Sum(p => p.ZeroDays);
                return new OutstockProduct(g.Key, first.Product, first.Code, first.Category, first.Top, g.Sum(p => p.PeriodKg), days > 0 ? (decimal)zero / days : null,
                    g.Sum(p => p.LostKg), g.Sum(p => p.LostSum), g.Count(p => p.ZeroDays > 0), g.Count(), g.Count(p => p.Core), g.Count(p => p.Chronic),
                    g.Sum(p => p.DealerLossSum), g.Sum(p => p.FactoryLossSum));
            })
            .Where(p => p.Regions > 0)
            .OrderByDescending(p => p.LostSum)
            .ThenBy(p => p.Name)
            .ToList();

        // Дилеры × категории: колонки — категории по убыванию потерь, строки — регионы с потерями.
        var cats = withLoss.GroupBy(p => p.Category)
            .Select(g => (Name: g.Key, Sum: g.Sum(p => p.LostSum)))
            .OrderByDescending(x => x.Sum)
            .ThenBy(x => x.Name)
            .Select(x => x.Name)
            .ToList();
        var catIndex = cats.Select((c, i) => (c, i)).ToDictionary(x => x.c, x => x.i);
        var rows = byRegion.Where(r => r.LostSum > 0 || r.LostKg > 0)
            .Select(r =>
            {
                var sum = new decimal[cats.Count];
                var kgs = new decimal[cats.Count];
                foreach (var p in withLoss.Where(p => p.RegionId == r.Id))
                {
                    var i = catIndex[p.Category];
                    sum[i] += p.LostSum;
                    kgs[i] += p.LostKg;
                }

                return new OutstockMatrixRow(r.Id, r.Name, r.Dealer, sum, kgs, r.LostSum, r.LostKg);
            })
            .ToList();
        var matrix = new OutstockMatrix(cats, rows, cats.Select((_, i) => rows.Sum(r => r.Sum[i])).ToList(), cats.Select((_, i) => rows.Sum(r => r.Kg[i])).ToList());

        return new OutstockView(
            b.Year,
            b.Month,
            b.From,
            b.To,
            b.Days,
            b.DaysInMonth,
            b.SnapshotDate,
            b.SyncedAt,
            regionId,
            s,
            selected,
            b.TopConfigured,
            b.TopHint,
            b.FactoryKnown,
            b.Regions,
            totals,
            categories,
            withLoss, // пары без дней в нуле — только в итогах и группах, в списке они не нужны
            byRegion,
            byProduct,
            matrix);
    }

    private async Task<OutstockBase> BuildAsync(int year, int month, CancellationToken ct)
    {
        var view = await stock.GetAsync(null, ct);
        var snapshotDate = view.SyncedAt is { } at ? DateOnly.FromDateTime(at.ToOffset(CompanyOffset).DateTime) : DateOnly.FromDateTime(DateTime.Today);
        var monthStart = new DateOnly(year, month, 1);
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var monthEnd = new DateOnly(year, month, daysInMonth);
        var to = snapshotDate < monthEnd ? snapshotDate : monthEnd;

        // Филиал Linko → регион, включая старые филиалы («Жиззах (эски)») — как во вторичке; дилер региона — из справочника.
        var regionRows = await db.SalesRegions.AsNoTracking().Select(r => new { r.Id, r.Name, r.LinkoBranchId, r.DirectionId, r.SupervisorName, r.DealerName }).ToListAsync(ct);
        var (kept, aliases) = OldBranches.Merge(regionRows.Select(r => new RegionInfo(r.Id, r.LinkoBranchId, r.Name, r.DirectionId, r.SupervisorName, r.DealerName)).ToList(), options.OldBranchSuffix);
        var dealerOf = kept.ToDictionary(r => r.Id.ToString(), r => r.Dealer);
        var regions = view.Regions.Select(r => new OutstockRegionRef(r.Id, r.Name, dealerOf.GetValueOrDefault(r.Id))).ToList();

        var products = await db.LinkoProducts.AsNoTracking().Select(p => new { p.Id, p.Name, p.Code, p.TypeId }).ToDictionaryAsync(p => p.Id, ct);
        var types = await db.LinkoProductTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, ct);
        var categories = SalesCategories.Build(options.Categories, types);
        var (topConfigured, topHint, isTop) = TopRule(categories);
        var factoryKnown = view.Factory is not null;

        if (to < monthStart)
        {
            return new OutstockBase(year, month, monthStart, monthStart, 0, daysInMonth, snapshotDate, view.SyncedAt, factoryKnown, topConfigured, topHint, regions, []);
        }

        // Диапазон восстановления — от первого дня месяца до снимка (у закрытого месяца — через все дни после него).
        var n = snapshotDate.DayNumber - monthStart.DayNumber + 1;
        var analyzed = to.DayNumber - monthStart.DayNumber + 1;
        int Index(DateOnly d) => d.DayNumber - monthStart.DayNumber;

        var sold = options.SoldStatuses;
        var excluded = options.ExcludedBranches.Select(b => b.Trim().ToLowerInvariant()).ToArray();
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
        var factoryMorning = new Dictionary<long, decimal[]>();

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

        var regionName = regions.ToDictionary(r => r.Id, r => r.Name);
        var pairs = new List<OutstockPair>();
        foreach (var (key, soldArr) in soldByDay)
        {
            if (!regionName.TryGetValue(key.Region, out var name))
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
            var received = receivedByDay.GetValueOrDefault(key) ?? new decimal[n];
            var (morning, negative) = OutstockMath.Reconstruct(snapshotKg, soldArr, received);
            var factory = factoryKnown ? FactoryMorning(key.Product) : null;

            var flags = new char[analyzed];
            var arrivals = new char[analyzed];
            int zero = 0, dealer = 0, factoryDays = 0, negativeDays = 0;
            for (var i = 0; i < analyzed; i++)
            {
                var present = OutstockMath.InStock(morning[i]);
                flags[i] = present ? '1' : '0';
                arrivals[i] = received[i] > 0 ? '1' : '0';
                if (negative[i])
                {
                    negativeDays++;
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

            var perDay = periodKg / analyzed;
            var avgPrice = periodKg > 0 ? periodSum / periodKg : (decimal?)null;
            var lostKg = OutstockMath.LostKg(zero, periodKg, analyzed);
            var lostSum = avgPrice is { } price ? lostKg * price : 0;
            var product = products.GetValueOrDefault(key.Product);
            var group = categories.GroupOf(product?.TypeId);
            pairs.Add(new OutstockPair(
                key.Region,
                name,
                key.Product,
                product?.Name ?? $"Товар {key.Product}",
                product?.Code,
                categories.NameOf(group),
                isTop(product?.Code, group),
                periodKg,
                periodSum,
                perDay,
                avgPrice,
                zero,
                dealer,
                factoryDays,
                negativeDays,
                lostKg,
                lostSum,
                zero == 0 ? 0 : lostSum * dealer / zero,
                zero == 0 ? 0 : lostSum * factoryDays / zero,
                false,
                OutstockMath.IsChronic(zero, analyzed),
                snapshotKg,
                new string(flags),
                new string(arrivals)));
        }

        // Без списка кодов ТОП — категории отчёта; если в них попали все проданные товары, делить не на что: ТОП не настроен.
        if (topConfigured && options.TopProducts.Length == 0 && pairs.All(p => p.Top))
        {
            topConfigured = false;
            topHint = "ТОП-товары не настроены (Sales:TopProducts) — показаны все SKU";
            pairs = pairs.Select(p => p with { Top = false }).ToList();
        }

        return new OutstockBase(year, month, monthStart, to, analyzed, daysInMonth, snapshotDate, view.SyncedAt, factoryKnown, topConfigured, topHint, regions, pairs);
    }

    /// <summary>Что считать ТОП-товаром: коды из настройки, а без них — категории отчёта.</summary>
    private (bool Configured, string Hint, Func<string?, long?, bool> IsTop) TopRule(SalesCategories categories)
    {
        var codes = options.TopProducts.Select(c => c.Trim()).Where(c => c.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (codes.Count > 0)
        {
            return (true, $"ТОП — {codes.Count} товаров из настройки Sales:TopProducts (коды Linko)", (code, _) => code is { } c && codes.Contains(c.Trim()));
        }

        var hasReportCategories = categories.Mapping.Values.Any(g => SalesCategories.IsConfigured(g));
        return hasReportCategories
            ? (true, $"ТОП — товары категорий отчёта: {string.Join(", ", options.Categories.Keys)}; остальные типы Linko — «кроме ТОПа»", (_, group) => SalesCategories.IsConfigured(group))
            : (false, "ТОП-товары не настроены — показаны все SKU", (_, _) => true);
    }
}
