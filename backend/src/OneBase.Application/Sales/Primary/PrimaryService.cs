using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Sales.Stock;
using OneBase.Domain.Sales;

namespace OneBase.Application.Sales.Primary;

/// <summary>
/// Отгрузка в четырёх единицах: кг, коробки (только там, где фасовка из названия сходится с весом единицы), сумма завода
/// (цена перемещения) и сумма дилера (прайс дилера, SalesOptions.PrimaryDealerPriceList).
/// </summary>
public sealed record PrimaryAmounts(decimal Kg, decimal Boxes, decimal SumFactory, decimal SumDealer)
{
    public static readonly PrimaryAmounts Zero = new(0, 0, 0, 0);

    public PrimaryAmounts Add(PrimaryAmounts other) =>
        new(Kg + other.Kg, Boxes + other.Boxes, SumFactory + other.SumFactory, SumDealer + other.SumDealer);
}

/// <summary>Строка разреза (категория или дилер): выбранный месяц, все месяцы года и план в кг (null — плана нет).</summary>
public sealed record PrimaryRow(
    string Id,
    string Name,
    string? Sub,
    PrimaryAmounts Month,
    IReadOnlyList<PrimaryAmounts> Months,
    decimal? PlanMonthKg,
    IReadOnlyList<decimal?> PlanMonths);

/// <summary>Товар с начала года.</summary>
public sealed record PrimaryItemRow(long ProductId, string Name, string? Code, string Category, PrimaryAmounts Ytd, bool BoxesKnown);

/// <summary>Строка отгрузки выбранного месяца — для календаря («что отгрузили в этот день»).</summary>
public sealed record PrimaryLine(int Day, string DealerId, long? ProductId, decimal Kg, decimal Boxes, decimal SumFactory, decimal SumDealer);

/// <summary>Плитка входа в «Первичку»: с начала года.</summary>
public sealed record PrimaryCard(decimal Kg, decimal SumFactory, int Counterparties, int Transfers);

public sealed record PrimaryView(
    int Year,
    int Month,
    DateOnly? DataThrough,
    int DaysInMonth,
    int WorkedDays,
    DateTimeOffset? SyncedAt,
    string? FactoryStock,
    string? ExportStock,
    string? DealerPriceList,
    PrimaryCard Republic,
    PrimaryCard Export,
    PrimaryAmounts MonthTotal,
    decimal? PlanMonthKg,
    decimal? ForecastKg,
    int MonthTransfers,
    IReadOnlyList<int> MonthsWithData,
    IReadOnlyList<PrimaryAmounts> Months,
    IReadOnlyList<decimal?> PlanMonths,
    PrimaryAmounts Ytd,
    int YtdArticles,
    decimal YtdReturnsKg,
    int YtdReturnLines,
    decimal? PlanYtdKg,
    decimal BoxesUnknownKg,
    IReadOnlyList<PrimaryRow> Categories,
    IReadOnlyList<PrimaryRow> Dealers,
    IReadOnlyList<PrimaryItemRow> Items,
    IReadOnlyList<PrimaryLine> MonthLines,
    IReadOnlyDictionary<long, string> ProductNames,
    IReadOnlyList<string>? Notes = null);

/// <summary>
/// Первичка — отгрузка завода дилерам: перемещения Linko со склада завода на склады дилеров (кроме склада экспорта) в статусах
/// «отдано» или «принято». Дата отгрузки — время выдачи (given_time), без него — время приёмки.
/// Возвраты — перемещения со склада дилера на склад завода; из отгрузки не вычитаются, показываются отдельно.
/// План — загруженный в OneBase план вида «Первичка» по регионам (регион = склад дилера с тем же названием).
/// </summary>
public sealed partial class PrimaryService(IAppDbContext db, SalesOptions options, Microsoft.Extensions.Caching.Memory.IMemoryCache cache, SalesCacheSignal signal)
{
    /// <summary>Строка плана без категории (итог региона) — отдельной строкой «Без разбивки» в плане по категориям.</summary>
    private const string NoBreakdown = "Без разбивки";

    public Task<PrimaryView> GetAsync(int? year, int? month, CancellationToken ct = default) =>
        SalesViewCache.GetAsync(cache, signal, $"sales:primary:{year}:{month}", () => BuildAsync(year, month, ct));

    private sealed record Shipment(long Id, long? From, long? To, DateOnly Date, long? ProductId, PrimaryAmounts Amounts, bool BoxesKnown);

    public async Task<PrimaryView> BuildAsync(int? yearArg, int? monthArg, CancellationToken ct = default)
    {
        var stocks = await db.LinkoStocks.AsNoTracking().Select(s => new { s.Id, s.Name }).ToListAsync(ct);
        var factoryIds = stocks.Where(s => options.IsFactoryStock(s.Name)).Select(s => s.Id).ToHashSet();
        var exportIds = stocks.Where(s => options.IsExportStock(s.Name)).Select(s => s.Id).ToHashSet();
        var shipped = options.ShippedTransferStatuses;

        var dealerList = (await db.LinkoPriceLists.AsNoTracking().Select(p => new { p.Id, p.Name }).ToListAsync(ct))
            .FirstOrDefault(p => string.Equals(p.Name.Trim(), options.PrimaryDealerPriceList.Trim(), StringComparison.OrdinalIgnoreCase));
        var dealerPrices = dealerList is null
            ? new Dictionary<long, decimal>()
            : await db.LinkoPriceListItems.AsNoTracking()
                .Where(i => i.PriceListId == dealerList.Id && i.ProductId != null)
                .GroupBy(i => i.ProductId!.Value)
                .Select(g => new { g.Key, Price = g.Max(i => i.Price) })
                .ToDictionaryAsync(x => x.Key, x => x.Price, ct);

        var products = await db.LinkoProducts.AsNoTracking().Select(p => new { p.Id, p.Name, p.Code, p.TypeId }).ToDictionaryAsync(p => p.Id, ct);
        var packs = products.ToDictionary(p => p.Key, p => StockMath.ParsePack(p.Value.Name));

        var raw = await (
                from l in db.LinkoStockTransferLines
                join t in db.LinkoStockTransfers on l.TransferId equals t.Id
                where shipped.Contains(t.Status)
                      && ((t.FromStockId != null && factoryIds.Contains(t.FromStockId.Value)) || (t.ToStockId != null && factoryIds.Contains(t.ToStockId.Value)))
                select new { t.Id, t.FromStockId, t.ToStockId, t.GivenAt, t.AcceptedAt, t.CreatedAt, l.ProductId, l.Amount, l.TotalWeight, l.TotalPrice })
            .AsNoTracking()
            .ToListAsync(ct);

        Shipment ToShipment(long id, long? from, long? to, DateOnly date, long? productId, decimal amount, decimal kg, decimal sum)
        {
            var dealerSum = productId is { } pid && dealerPrices.TryGetValue(pid, out var price) ? amount * price : 0;
            var (amounts, boxesKnown) = AmountsOf(productId is { } p ? packs.GetValueOrDefault(p) : null, amount, kg, sum, dealerSum);
            return new Shipment(id, from, to, date, productId, amounts, boxesKnown);
        }

        var all = raw
            .Select(r => (Date: r.GivenAt ?? r.AcceptedAt ?? r.CreatedAt, r))
            .Where(x => x.Date is not null)
            .Select(x => ToShipment(x.r.Id, x.r.FromStockId, x.r.ToStockId, DateOnly.FromDateTime(x.Date!.Value), x.r.ProductId, x.r.Amount, x.r.TotalWeight, x.r.TotalPrice))
            .ToList();
        bool IsFactory(long? id) => id is { } s && factoryIds.Contains(s);
        bool IsExport(long? id) => id is { } s && exportIds.Contains(s);
        // С завода на склад экспорта — не отгрузка дилеру: в первичку республики не входит. Сам экспорт — заказы
        // филиала «Завод» экспортным точкам (PrimaryService.Export.cs).
        var outbound = all.Where(s => IsFactory(s.From) && !IsFactory(s.To) && !IsExport(s.To)).ToList();
        var returns = all.Where(s => !IsFactory(s.From) && !IsExport(s.From) && IsFactory(s.To)).ToList();

        var last = outbound.Count == 0 ? (DateOnly?)null : outbound.Max(s => s.Date);
        var year = yearArg ?? last?.Year ?? DateTime.Today.Year;
        var month = monthArg is >= 1 and <= 12 ? monthArg.Value : last?.Month ?? DateTime.Today.Month;
        var days = DateTime.DaysInMonth(year, month);
        var yearRows = outbound.Where(s => s.Date.Year == year).ToList();
        var inMonth = yearRows.Where(s => s.Date.Month == month).ToList();
        var dataThrough = inMonth.Count == 0 ? (DateOnly?)null : inMonth.Max(s => s.Date);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var running = year == today.Year && month == today.Month;
        var workedDays = dataThrough is null ? 0 : running ? today.Day : days;

        var types = await db.LinkoProductTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, ct);
        var categories = SalesCategories.Build(options.Categories, types);
        long? GroupOf(long? product) => categories.GroupOf(product is { } p && products.TryGetValue(p, out var info) ? info.TypeId : null);
        string StockName(long? id) => (stocks.FirstOrDefault(s => s.Id == id)?.Name ?? $"Склад {id}").Trim();

        // Регион ↔ склад дилера — по названию; у региона может быть записано имя дилера.
        var regions = await db.SalesRegions.AsNoTracking().Select(r => new { r.Id, r.Name, r.DealerName }).ToListAsync(ct);
        var regionByName = regions.GroupBy(r => r.Name.Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // План первички: регион × категория × месяц, кг.
        var planRows = (await db.SalesRegionPlans.AsNoTracking()
                .Where(p => p.Kind == PlanKind.Primary && p.Year == year)
                .Select(p => new { p.RegionId, p.Month, p.CategoryId, p.PlanKg })
                .ToListAsync(ct))
            .Select(p => new PlanRow(p.RegionId, null, p.Month, p.CategoryId, p.PlanKg))
            .ToList();
        var hasPlan = planRows.Count > 0;
        decimal? RegionPlan(Guid region, int m) => SalesMath.PlanTotal(planRows.Where(p => p.RegionId == region && p.Month == m));
        decimal? TotalPlan(int m) =>
            planRows.Any(p => p.Month == m) ? planRows.Where(p => p.Month == m).GroupBy(p => p.RegionId).Sum(g => SalesMath.PlanTotal(g) ?? 0) : null;

        // План по категории отчёта: строки с категорией; регион, у которого на месяц только итог, — в «Без разбивки».
        decimal? CategoryPlan(string categoryId, int m)
        {
            if (!hasPlan)
            {
                return null;
            }

            var byRegion = planRows.Where(p => p.Month == m).GroupBy(p => p.RegionId).ToList();
            if (categoryId == NoBreakdown)
            {
                var totals = byRegion.Where(g => g.All(p => p.CategoryId == null)).Sum(g => g.Sum(p => p.PlanKg));
                return totals == 0 ? null : totals;
            }

            var sum = byRegion.SelectMany(g => g.Where(p => p.CategoryId != null && CategoryKey(categories.GroupOf(p.CategoryId)) == categoryId)).Sum(p => p.PlanKg);
            return sum == 0 ? null : sum;
        }

        static PrimaryAmounts Sum(IEnumerable<Shipment> source) => source.Aggregate(PrimaryAmounts.Zero, (a, s) => a.Add(s.Amounts));
        IReadOnlyList<PrimaryAmounts> ByMonth(IEnumerable<Shipment> source)
        {
            var list = source.ToLookup(s => s.Date.Month);
            return Enumerable.Range(1, 12).Select(m => Sum(list[m])).ToList();
        }

        var planMonths = Enumerable.Range(1, 12).Select(TotalPlan).ToList();

        var dealerRows = yearRows
            .GroupBy(s => s.To!.Value)
            .Select(g =>
            {
                var name = StockName(g.Key);
                var region = regionByName.GetValueOrDefault(name);
                var plans = Enumerable.Range(1, 12).Select(m => region is null ? null : RegionPlan(region.Id, m)).ToList();
                return new PrimaryRow(g.Key.ToString(), name, region?.DealerName, Sum(g.Where(s => s.Date.Month == month)), ByMonth(g), plans[month - 1], plans);
            })
            .ToList();
        // Регионы с планом, но без отгрузок в году — тоже в таблице плана (факт 0).
        foreach (var region in regions.Where(r => planRows.Any(p => p.RegionId == r.Id)))
        {
            var stock = stocks.FirstOrDefault(s => string.Equals(s.Name.Trim(), region.Name.Trim(), StringComparison.OrdinalIgnoreCase));
            var id = stock?.Id.ToString() ?? $"region:{region.Id}";
            if (dealerRows.Any(d => d.Id == id))
            {
                continue;
            }

            var plans = Enumerable.Range(1, 12).Select(m => RegionPlan(region.Id, m)).ToList();
            dealerRows.Add(new PrimaryRow(id, region.Name, region.DealerName, PrimaryAmounts.Zero, ByMonth([]), plans[month - 1], plans));
        }

        dealerRows = dealerRows.OrderByDescending(d => d.Months.Sum(m => m.Kg)).ThenByDescending(d => d.PlanMonthKg ?? 0).ToList();

        var categoryRows = yearRows
            .GroupBy(s => CategoryKey(GroupOf(s.ProductId)))
            .Select(g =>
            {
                var group = GroupOf(g.First().ProductId);
                var plans = Enumerable.Range(1, 12).Select(m => CategoryPlan(g.Key, m)).ToList();
                return new PrimaryRow(g.Key, categories.NameOf(group), SalesCategories.IsConfigured(group) ? null : "вне категорий отчёта",
                    Sum(g.Where(s => s.Date.Month == month)), ByMonth(g), plans[month - 1], plans);
            })
            .ToList();
        var noBreakdown = Enumerable.Range(1, 12).Select(m => CategoryPlan(NoBreakdown, m)).ToList();
        if (noBreakdown.Any(v => v is not null))
        {
            categoryRows.Add(new PrimaryRow(NoBreakdown, NoBreakdown, "план одной строкой, без категорий", PrimaryAmounts.Zero, ByMonth([]), noBreakdown[month - 1], noBreakdown));
        }

        categoryRows = categoryRows.OrderByDescending(c => c.Months.Sum(m => m.Kg)).ThenByDescending(c => c.PlanMonthKg ?? 0).ToList();

        var items = yearRows
            .Where(s => s.ProductId != null)
            .GroupBy(s => s.ProductId!.Value)
            .Select(g =>
            {
                var product = products.GetValueOrDefault(g.Key);
                return new PrimaryItemRow(g.Key, product?.Name ?? $"Товар {g.Key}", product?.Code, categories.NameOf(GroupOf(g.Key)), Sum(g), g.All(s => s.BoxesKnown));
            })
            .OrderByDescending(i => i.Ytd.Kg)
            .ToList();

        var lines = inMonth
            .Select(s => new PrimaryLine(s.Date.Day, s.To!.Value.ToString(), s.ProductId, s.Amounts.Kg, s.Amounts.Boxes, s.Amounts.SumFactory, s.Amounts.SumDealer))
            .ToList();
        var productNames = inMonth.Where(s => s.ProductId != null).Select(s => s.ProductId!.Value).Distinct()
            .ToDictionary(id => id, id => products.TryGetValue(id, out var p) ? p.Name : $"Товар {id}");

        var monthKg = inMonth.Sum(s => s.Amounts.Kg);
        var ytdReturns = returns.Where(s => s.Date.Year == year && s.Date.Month <= month).ToList();
        var ytdRows = yearRows.Where(s => s.Date.Month <= month).ToList();
        var syncedAt = (await db.LinkoSyncStates.AsNoTracking().FirstOrDefaultAsync(s => s.Entity == "stock_transfers", ct))?.LastSuccessAt;
        var export = await LoadExportAsync(ct);

        return new PrimaryView(
            year,
            month,
            dataThrough,
            days,
            workedDays,
            syncedAt,
            stocks.FirstOrDefault(s => factoryIds.Contains(s.Id))?.Name,
            stocks.FirstOrDefault(s => exportIds.Contains(s.Id))?.Name,
            dealerList?.Name,
            new PrimaryCard(yearRows.Sum(s => s.Amounts.Kg), yearRows.Sum(s => s.Amounts.SumFactory), yearRows.Select(s => s.To).Distinct().Count(),
                yearRows.Select(s => s.Id).Distinct().Count()),
            ExportCard(export, year),
            Sum(inMonth),
            planMonths[month - 1],
            running && workedDays > 0 ? SalesMath.Forecast(monthKg, workedDays, days) : null, // идущий месяц; закрытый — это факт
            inMonth.Select(s => s.Id).Distinct().Count(),
            yearRows.Select(s => s.Date.Month).Distinct().Order().ToList(),
            ByMonth(yearRows),
            planMonths,
            Sum(ytdRows),
            ytdRows.Where(s => s.ProductId != null).Select(s => s.ProductId).Distinct().Count(),
            ytdReturns.Sum(s => s.Amounts.Kg),
            ytdReturns.Count,
            hasPlan ? planMonths.Take(month).Sum(v => v ?? 0) : null,
            ytdRows.Where(s => !s.BoxesKnown).Sum(s => s.Amounts.Kg),
            categoryRows,
            dealerRows,
            items,
            lines,
            productNames);
    }

    /// <summary>Ключ категории отчёта (как id карточки категории): группа или «none».</summary>
    private static string CategoryKey(long? group) => group?.ToString() ?? "none";
}
