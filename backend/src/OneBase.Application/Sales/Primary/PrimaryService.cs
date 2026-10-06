using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Sales.Stock;
using OneBase.Domain.Sales;

namespace OneBase.Application.Sales.Primary;

/// <summary>
/// Отгрузка в четырёх единицах: кг, коробки (только там, где фасовка из названия сходится с весом единицы; у экспорта коробок нет — null),
/// сумма по цене дилера (SumFactory — сумма перемещения или заказа: по этой цене дилер берёт товар у завода, «сумма дилера» таблицы первички)
/// и сумма по цене продажи дилера (SumDealer — прайс SalesOptions.PrimaryDealerPriceList). Цены завода в Linko нет — «суммы завода» нет.
/// Возврат — со знаком минус.
/// </summary>
public sealed record PrimaryAmounts(decimal Kg, decimal? Boxes, decimal SumFactory, decimal SumDealer)
{
    public static readonly PrimaryAmounts Zero = new(0, 0, 0, 0);

    /// <summary>Ноль без коробок — у экспорта: сумма с ним остаётся без коробок.</summary>
    public static readonly PrimaryAmounts NoBoxes = new(0, null, 0, 0);

    /// <summary>Сумма; коробки — только если они есть у обоих слагаемых.</summary>
    public PrimaryAmounts Add(PrimaryAmounts other) =>
        new(Kg + other.Kg, Boxes is { } a && other.Boxes is { } b ? a + b : null, SumFactory + other.SumFactory, SumDealer + other.SumDealer);

    /// <summary>Со знаком минус — возврат.</summary>
    public PrimaryAmounts Negate() => new(-Kg, -Boxes, -SumFactory, -SumDealer);
}

/// <summary>Доли в итоге по каждой единице (null — итог 0 или коробок нет).</summary>
public sealed record PrimaryShares(decimal? Kg, decimal? Boxes, decimal? SumFactory, decimal? SumDealer);

/// <summary>
/// Строка разреза (категория, контрагент или страна): выбранный месяц, все месяцы года и с начала года (месяцы по выбранный), доли в итогах,
/// план в кг (null — плана нет), выполнение = факт ÷ план и «осталось» = план − факт (не меньше 0) — за месяц и с начала года.
/// Group — у контрагентов: dealer — склад дилера (регион), direct — точка с заказами завода (базар, сеть, фирменный магазин); иначе null.
/// </summary>
public sealed record PrimaryRow(
    string Id,
    string Name,
    string? Sub,
    PrimaryAmounts Month,
    IReadOnlyList<PrimaryAmounts> Months,
    decimal? PlanMonthKg,
    IReadOnlyList<decimal?> PlanMonths,
    string? Group,
    PrimaryShares MonthShare,
    decimal? MonthExecution,
    decimal? MonthRemainingKg,
    PrimaryAmounts Ytd,
    PrimaryShares YtdShare,
    decimal? PlanYtdKg,
    decimal? YtdExecution);

/// <summary>
/// Товар с начала года (месяцы по выбранный): PricePerKg — сумма по цене дилера за кг, Markup — наценка дилера (сумма по цене продажи дилера
/// ÷ сумма по цене дилера − 1; null — одной из сумм нет).
/// </summary>
public sealed record PrimaryItemRow(long ProductId, string Name, string? Code, string Category, PrimaryAmounts Ytd, bool BoxesKnown, decimal? PricePerKg, decimal? Markup);

/// <summary>Плитка входа в «Первичку»: с начала года; Share — доля в отгрузке завода (республика + экспорт) по весу.</summary>
public sealed record PrimaryCard(decimal Kg, decimal SumFactory, int Counterparties, int Transfers, decimal? Share = null);

/// <summary>Ряд «Клиентов по месяцам»: значения по месяцам (null — отгрузок за месяц нет); Average — среднее за закрытые месяцы с данными.</summary>
public sealed record PrimaryClientSeries(string Id, string Name, IReadOnlyList<decimal?> Values, decimal? Average = null);

/// <summary>
/// Одна мера «Клиентов по месяцам» — в форме «АКБ по месяцам» вторички: итог и ряды категорий; LastPartial — последний месяц идёт;
/// Average — среднее итога за закрытые месяцы с данными (идущий месяц не в счёт).
/// </summary>
public sealed record PrimaryClientsMetric(
    int Year,
    IReadOnlyList<int> Months,
    bool LastPartial,
    IReadOnlyList<decimal?> Total,
    IReadOnlyList<PrimaryClientSeries> Categories,
    decimal? Average = null);

/// <summary>«Клиенты по месяцам»: АКБ (контрагенты с нетто больше нуля), кг (нетто) и сумма (нетто по цене дилера) — переключатель «АКБ / кг / сум».</summary>
public sealed record PrimaryClients(PrimaryClientsMetric Akb, PrimaryClientsMetric Kg, PrimaryClientsMetric Sum);

/// <summary>Строка календаря отгрузок: контрагент, клетки по дням периода (Days; null — отгрузок в этот день не было), итог за период и «Дней» с отгрузкой.</summary>
public sealed record PrimaryCalendarRow(string Id, string Name, string? Sub, IReadOnlyList<PrimaryAmounts?> Cells, PrimaryAmounts Total, int Days);

/// <summary>Товар разбора: контрагент (когда смотрят всех), артикул, категория, возврат ли, суммы.</summary>
public sealed record PrimaryCalendarItem(string? DealerId, string? DealerName, long? ProductId, string Name, string? Code, string Category, bool IsReturn, PrimaryAmounts Amounts);

/// <summary>
/// Календарь отгрузок месяца: MonthDays — дни месяца с отгрузкой (выбор периода), From–To — период, Days — его дни с отгрузкой (колонки),
/// DayTotals — итоги дней, Total — итог периода; Day и Dealer — выбранные день и контрагент разбора, Detail — товары разбора (null — не выбран).
/// </summary>
public sealed record PrimaryCalendar(
    IReadOnlyList<int> MonthDays,
    int? From,
    int? To,
    IReadOnlyList<int> Days,
    IReadOnlyList<PrimaryCalendarRow> Rows,
    IReadOnlyList<PrimaryAmounts> DayTotals,
    PrimaryAmounts Total,
    int? Day,
    string? Dealer,
    IReadOnlyList<PrimaryCalendarItem>? Detail,
    PrimaryAmounts? DetailTotal);

/// <summary>
/// Первичка за месяц и год. AsOf — дата данных (последний день с отгрузкой не позже отчётного дня — вчера и не позже синхронизации; строки
/// позже отчётного дня в отчёт не входят); Running — выбранный месяц идёт (прогноз только у него), RunningMonth — какой месяц года идёт.
/// Выполнение, «осталось», прогноз к плану, доли, цены за кг, наценку, долю возвратов и итоги таблиц считает сервер — страница их только
/// форматирует. Calendar — по периоду и разбору из запроса. OtherStocks — склады вне справочника регионов за год: не дилеры, в первичке их нет.
/// </summary>
public sealed record PrimaryView(
    int Year,
    int Month,
    DateOnly? DataThrough,
    DateOnly? AsOf,
    bool Running,
    int? RunningMonth,
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
    decimal? MonthExecution,
    decimal? MonthRemainingKg,
    decimal? MonthOverPlanKg,
    decimal? ForecastKg,
    decimal? ForecastExecution,
    int MonthTransfers,
    int MonthCounterparties,
    bool HasPlan,
    IReadOnlyList<int> MonthsWithData,
    IReadOnlyList<PrimaryAmounts> Months,
    IReadOnlyList<decimal?> PlanMonths,
    IReadOnlyList<decimal?> MonthExecutions,
    PrimaryAmounts Ytd,
    int YtdMonths,
    int YtdArticles,
    decimal YtdReturnsKg,
    int YtdReturnLines,
    decimal? YtdReturnsShare,
    decimal? PlanYtdKg,
    decimal? YtdExecution,
    decimal? YtdPricePerKg,
    decimal? YtdMarkupSum,
    decimal? YtdMarkup,
    decimal BoxesUnknownKg,
    IReadOnlyList<PrimaryRow> Categories,
    PrimaryRow CategoryTotal,
    IReadOnlyList<PrimaryRow> Dealers,
    IReadOnlyList<PrimaryRow> DealerGroups,
    PrimaryRow DealerTotal,
    IReadOnlyList<PrimaryItemRow> Items,
    PrimaryClients? Clients,
    PrimaryCalendar? Calendar,
    IReadOnlyList<string>? Notes = null,
    IReadOnlyList<PrimaryOtherStock>? OtherStocks = null);

/// <summary>
/// Первичка — отгрузка завода контрагентам (DOC-rules §3, §9.3): перемещения Linko со склада завода на склады дилеров (кроме склада экспорта)
/// в статусах «отдано» или «принято» и заказы филиалов SalesOptions.PrimaryOrderBranches («Завод», «К К Мерч») точкам не экспортного типа —
/// базарам, сетям, фирменному магазину (статусы PrimaryOrderStatuses, без PrimaryExcludedMarkets). Возвраты — перемещения со склада дилера
/// на завод и возвраты по таким заказам — строки со знаком минус: месяцы, строки, календарь и с начала года — нетто.
/// Дата перемещения — доставка (DeliveryDate), без неё — выдача, приёмка, создание; дата заказа — создание, возврата — его создание.
/// Строки позже отчётного дня (вчера, не позже синхронизации) в отчёт не входят — как salesEnd во вторичке.
/// Склады сводятся к регионам (SalesOptions.StockRegionAliases), дилер — склад с регионом из справочника, подпись строки — дилер региона.
/// План — sales."RegionPlans" вида «Первичка».
/// </summary>
public sealed partial class PrimaryService(IAppDbContext db, SalesOptions options, Microsoft.Extensions.Caching.Memory.IMemoryCache cache, SalesCacheSignal signal)
{
    /// <summary>Строка плана без категории (итог региона) — отдельной строкой «Без разбивки» в плане по категориям.</summary>
    private const string NoBreakdown = "Без разбивки";

    /// <summary>Расчёт месяца в кэше: ответ без календаря, строки месяца и подписи — календарь по периоду и разбор считаются на каждый запрос.</summary>
    private sealed record PrimaryMonth(
        PrimaryView View,
        IReadOnlyList<PrimaryShipment> MonthRows,
        IReadOnlyDictionary<string, PrimaryParty> Parties,
        IReadOnlyDictionary<long, PrimaryProduct> Products,
        PrimaryAmounts Zero);

    public Task<PrimaryView> GetAsync(int? year, int? month, CancellationToken ct = default) => GetAsync(year, month, new PrimaryCalendarQuery(), ct);

    /// <summary>Первичка месяца; calendar — период «с … по …» и разбор календаря отгрузок (расчёт месяца в кэше от них не зависит).</summary>
    public async Task<PrimaryView> GetAsync(int? year, int? month, PrimaryCalendarQuery calendar, CancellationToken ct = default) =>
        WithCalendar(await SalesViewCache.GetAsync(cache, signal, $"sales:primary:{year}:{month}", () => BuildAsync(year, month, ct)), calendar);

    private static PrimaryView WithCalendar(PrimaryMonth month, PrimaryCalendarQuery query) =>
        month.View with
        {
            Calendar = PrimaryMath.Calendar(month.MonthRows, month.View.DaysInMonth, query, month.Parties,
                id => id is { } p && month.Products.TryGetValue(p, out var product) ? product : new PrimaryProduct(id is null ? "Без товара" : $"Товар {id}", null, "—"),
                month.Zero),
        };

    private async Task<PrimaryMonth> BuildAsync(int? yearArg, int? monthArg, CancellationToken ct)
    {
        var stocks = await db.LinkoStocks.AsNoTracking().Select(s => new { s.Id, s.Name }).ToListAsync(ct);
        var stockNames = stocks.ToDictionary(s => s.Id, s => s.Name.Trim());
        var stockByName = stocks.GroupBy(s => s.Name.Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
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
        var pricing = new PrimaryPricing(dealerPrices, products.ToDictionary(p => p.Key, p => StockMath.ParsePack(p.Value.Name)));
        var zero = pricing.Zero;

        var transferLines = await (
                from l in db.LinkoStockTransferLines
                join t in db.LinkoStockTransfers on l.TransferId equals t.Id
                where shipped.Contains(t.Status)
                      && ((t.FromStockId != null && factoryIds.Contains(t.FromStockId.Value)) || (t.ToStockId != null && factoryIds.Contains(t.ToStockId.Value)))
                select new PrimaryTransferLine(t.Id, t.FromStockId, t.ToStockId, t.DeliveryDate, t.GivenAt, t.AcceptedAt, t.CreatedAt, l.ProductId, l.Amount, l.TotalWeight, l.TotalPrice))
            .AsNoTracking()
            .ToListAsync(ct);

        // Заказы точек завода: базары, сети, фирменный магазин — филиалы PrimaryOrderBranches; тип точки и исключения проверяет PrimaryMath.
        var branches = options.PrimaryOrderBranches.Select(b => b.Trim().ToLowerInvariant()).ToList();
        var orderStatuses = options.PrimaryOrderStatuses;
        var returnStatuses = options.ReturnStatuses;
        var orderLines = await (
                from l in db.LinkoOrderLines
                join o in db.LinkoOrders on l.OrderId equals o.Id
                join m in db.LinkoMarkets on o.MarketId equals (long?)m.Id
                where orderStatuses.Contains(o.Status) && o.BranchName != null && branches.Contains(o.BranchName.ToLower())
                select new PrimaryOrderLine(o.Id, m.Id, m.Name, m.MarketTypeName, o.BranchName, o.Status, o.CreatedDate, o.Currency, l.ProductId, l.Amount, l.TotalWeight, l.TotalPrice))
            .AsNoTracking()
            .ToListAsync(ct);
        var returnLines = await (
                from l in db.LinkoOrderReturnLines
                join r in db.LinkoOrderReturns on l.ReturnId equals r.Id
                join m in db.LinkoMarkets on r.MarketId equals (long?)m.Id
                where returnStatuses.Contains(r.Status) && r.BranchName != null && branches.Contains(r.BranchName.ToLower())
                select new PrimaryReturnLine(r.Id, m.Id, m.Name, m.MarketTypeName, r.BranchName, r.Status, r.CreatedDate, l.ProductId, l.Amount, l.Price, l.TotalWeight))
            .AsNoTracking()
            .ToListAsync(ct);

        // Строка дилера — регион склада (StockRegionAliases: «Коканд бозор» — Коканд): ключ — склад с названием региона, без него — регион OneBase.
        // Дилер — склад, чей регион есть в справочнике регионов (без «Завода» и «К К Мерч»); «Основной», «Нукус (интеграция учун)» — не дилеры.
        var regions = await db.SalesRegions.AsNoTracking().Select(r => new { r.Id, r.Name, r.DealerName }).ToListAsync(ct);
        var regionByName = regions.GroupBy(r => r.Name.Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var dealerRegionNames = regions.Where(r => !options.IsExcludedBranch(r.Name) && !options.IsIgnoredBranch(r.Name))
            .Select(r => r.Name.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dealerRegions = new Dictionary<string, string>();
        string DealerId(string region)
        {
            var id = stockByName.TryGetValue(region, out var stock) ? stock.ToString(CultureInfo.InvariantCulture)
                : regionByName.TryGetValue(region, out var r) ? $"region:{r.Id}"
                : region;
            dealerRegions.TryAdd(id, region);
            return id;
        }

        var transfers = PrimaryMath.Transfers(transferLines, factoryIds, exportIds, id => stockNames.GetValueOrDefault(id) ?? $"Склад {id}", options,
            dealerRegionNames.Contains, DealerId, pricing);
        var direct = PrimaryMath.DirectOrders(orderLines, returnLines, options, pricing);

        // Отчётный день — вчера по местному времени и не позже синхронизации: строки позже него (сегодня, плановая доставка в будущем)
        // в факт, прогноз, календарь и «данные по» не входят — войдут, когда их день станет отчётным.
        var syncs = await db.LinkoSyncStates.AsNoTracking().Where(s => s.Entity == "stock_transfers" || s.Entity == "orders").ToListAsync(ct);
        var syncedAt = syncs.FirstOrDefault(s => s.Entity == "stock_transfers")?.LastSuccessAt;
        var cutoff = PrimaryMath.DataLimit(SecondarySales.ReportCutoff(DateTimeOffset.UtcNow), SyncedOn(syncs.Select(s => s.LastSuccessAt)));
        var (all, afterCutoff) = PrimaryMath.ReportRows(transfers.Rows.Concat(direct.Rows), cutoff);
        var asOf = PrimaryMath.AsOf(all.Select(s => s.Date), cutoff, null);

        var year = yearArg ?? asOf?.Year ?? DateTime.Today.Year;
        var month = monthArg is >= 1 and <= 12 ? monthArg.Value : asOf?.Month ?? DateTime.Today.Month;
        var days = DateTime.DaysInMonth(year, month);
        var yearRows = all.Where(s => s.Date.Year == year).ToList();
        var inMonth = yearRows.Where(s => s.Date.Month == month).ToList();
        var ytdRows = yearRows.Where(s => s.Date.Month <= month).ToList();
        var running = PrimaryMath.IsRunning(year, month, asOf);

        var types = await db.LinkoProductTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, ct);
        var categories = SalesCategories.Build(options.Categories, types);
        long? GroupOf(long? product) => categories.GroupOf(product is { } p && products.TryGetValue(p, out var info) ? info.TypeId : null);

        // План — загруженный в OneBase план вида «Первичка» (sales."RegionPlans"); в Linko плана первички нет.
        var planRows = PrimaryMath.PlanRows(await db.SalesRegionPlans.AsNoTracking()
            .Where(p => p.Kind == PlanKind.Primary && p.Year == year)
            .Select(p => new RegionPlanRow(p.RegionId, p.Kind, p.Year, p.Month, p.CategoryId, p.PlanKg))
            .ToListAsync(ct), year);
        var regionDealer = regions.ToDictionary(r => r.Id, r => DealerId(options.StockRegionName(r.Name)));
        var dealerPlans = PrimaryMath.DealerPlans(planRows, id => regionDealer.GetValueOrDefault(id));

        // План по категории отчёта: строки с категорией; регион, у которого на месяц только итог, — в «Без разбивки».
        decimal? CategoryPlan(string categoryId, int m)
        {
            var byRegion = planRows.Where(p => p.Month == m).GroupBy(p => p.RegionId).ToList();
            if (categoryId == NoBreakdown)
            {
                var totals = byRegion.Where(g => g.All(p => p.CategoryId == null)).Sum(g => g.Sum(p => p.PlanKg));
                return totals == 0 ? null : totals;
            }

            var sum = byRegion.SelectMany(g => g.Where(p => p.CategoryId != null && CategoryKey(categories.GroupOf(p.CategoryId)) == categoryId)).Sum(p => p.PlanKg);
            return sum == 0 ? null : sum;
        }

        var parties = new Dictionary<string, PrimaryParty>(direct.Parties);
        foreach (var (id, region) in dealerRegions)
        {
            parties[id] = PrimaryMath.DealerParty(id, region, regionByName.GetValueOrDefault(region)?.DealerName, dealerPlans.GetValueOrDefault(id) ?? PrimaryMath.NoPlan);
        }

        var months = PrimaryMath.ByMonth(yearRows, zero);
        var monthTotal = months[month - 1];
        var ytd = PrimaryMath.SumOf(ytdRows.Select(s => s.Amounts), zero);
        var planMonths = Enumerable.Range(0, 12).Select(i => PrimaryMath.PlanSum(dealerPlans.Values.Select(p => p[i]))).ToList();

        var dealerRows = PrimaryMath.PartyRows(yearRows, parties, month, zero, monthTotal, ytd);

        var categoryRows = yearRows
            .GroupBy(s => CategoryKey(GroupOf(s.ProductId)))
            .Select(g =>
            {
                var group = GroupOf(g.First().ProductId);
                return PrimaryMath.Row(g.Key, categories.NameOf(group), SalesCategories.IsConfigured(group) ? null : "вне категорий отчёта", null,
                    PrimaryMath.ByMonth(g, zero), Enumerable.Range(1, 12).Select(m => CategoryPlan(g.Key, m)).ToList(), month, zero, monthTotal, ytd);
            })
            .ToList();
        // Категории с планом, но без отгрузок в году, и план одной строкой («Без разбивки») — тоже строки (факт 0).
        foreach (var group in planRows.Where(p => p.CategoryId != null).Select(p => categories.GroupOf(p.CategoryId)).Distinct()
                     .Where(g => categoryRows.All(r => r.Id != CategoryKey(g))).ToList())
        {
            categoryRows.Add(PrimaryMath.Row(CategoryKey(group), categories.NameOf(group), SalesCategories.IsConfigured(group) ? null : "вне категорий отчёта", null,
                PrimaryMath.ByMonth([], zero), Enumerable.Range(1, 12).Select(m => CategoryPlan(CategoryKey(group), m)).ToList(), month, zero, monthTotal, ytd));
        }

        var noBreakdown = Enumerable.Range(1, 12).Select(m => CategoryPlan(NoBreakdown, m)).ToList();
        if (noBreakdown.Any(v => v is not null))
        {
            categoryRows.Add(PrimaryMath.Row(NoBreakdown, NoBreakdown, "план одной строкой, без категорий", null, PrimaryMath.ByMonth([], zero), noBreakdown, month, zero, monthTotal, ytd));
        }

        categoryRows = categoryRows.OrderByDescending(c => c.Months.Sum(m => m.Kg)).ThenByDescending(c => c.PlanMonthKg ?? 0).ToList();

        var items = ytdRows
            .Where(s => s.ProductId != null)
            .GroupBy(s => s.ProductId!.Value)
            .Select(g =>
            {
                var product = products.GetValueOrDefault(g.Key);
                var sum = PrimaryMath.SumOf(g.Select(s => s.Amounts), zero);
                return new PrimaryItemRow(g.Key, product?.Name ?? $"Товар {g.Key}", product?.Code, categories.NameOf(GroupOf(g.Key)), sum, g.All(s => s.BoxesKnown),
                    PrimaryMath.PricePerKg(sum), PrimaryMath.Markup(sum));
            })
            .Where(i => i.Ytd.Kg != 0 || i.Ytd.SumFactory != 0)
            .OrderByDescending(i => i.Ytd.Kg)
            .ToList();

        var export = await LoadExportAsync(ct);
        var exportCard = ExportCard(export, year, cutoff);
        var shipments = yearRows.Where(s => !s.IsReturn).ToList();
        var yearKg = yearRows.Sum(s => s.Amounts.Kg);
        var allKg = yearKg + exportCard.Kg;
        var republic = new PrimaryCard(yearKg, yearRows.Sum(s => s.Amounts.SumFactory), shipments.Select(s => s.Counterparty).Distinct().Count(),
            shipments.Select(s => s.Doc).Distinct().Count(), SalesMath.Ratio(yearKg, allKg));

        var plan = planMonths[month - 1];
        var forecast = PrimaryMath.Forecast(monthTotal.Kg, year, month, asOf);
        var planYtd = PrimaryMath.PlanSum(planMonths.Take(month));
        var ytdReturns = ytdRows.Where(s => s.IsReturn).ToList();
        var ytdReturnsKg = -ytdReturns.Sum(s => s.Amounts.Kg);
        var monthsWithData = yearRows.Select(s => s.Date.Month).Distinct().Order().ToList();
        var notes = new List<string>();
        var otherCurrency = direct.OtherCurrencyOrders.Values.Count(d => d.Year == year);
        if (otherCurrency > 0)
        {
            notes.Add($"Заказов точек завода в другой валюте за {year} год: {otherCurrency} — их вес учтён, сумма нет (курса в Linko нет).");
        }

        if (afterCutoff > 0)
        {
            notes.Add($"Строк первички после отчётного дня ({cutoff:dd.MM.yyyy}): {afterCutoff} — в отчёт войдут, когда их день станет отчётным.");
        }

        var view = new PrimaryView(
            Year: year,
            Month: month,
            DataThrough: running ? asOf : inMonth.Count == 0 ? null : inMonth.Max(s => s.Date),
            AsOf: asOf,
            Running: running,
            RunningMonth: PrimaryMath.RunningMonth(year, asOf),
            DaysInMonth: days,
            WorkedDays: PrimaryMath.WorkedDays(year, month, asOf, inMonth.Count > 0),
            SyncedAt: syncedAt,
            FactoryStock: stocks.FirstOrDefault(s => factoryIds.Contains(s.Id))?.Name,
            ExportStock: stocks.FirstOrDefault(s => exportIds.Contains(s.Id))?.Name,
            DealerPriceList: dealerList?.Name,
            Republic: republic,
            Export: exportCard with { Share = SalesMath.Ratio(exportCard.Kg, allKg) },
            MonthTotal: monthTotal,
            PlanMonthKg: plan,
            MonthExecution: SalesMath.Ratio(monthTotal.Kg, plan),
            MonthRemainingKg: plan is { } left ? SalesMath.Remaining(left, monthTotal.Kg) : null,
            MonthOverPlanKg: plan is { } over ? Math.Max(0, monthTotal.Kg - over) : null,
            ForecastKg: forecast,
            ForecastExecution: forecast is { } f ? SalesMath.Ratio(f, plan) : null,
            MonthTransfers: inMonth.Where(s => !s.IsReturn).Select(s => s.Doc).Distinct().Count(),
            MonthCounterparties: dealerRows.Count(r => r.Month.Kg > 0),
            HasPlan: planRows.Count > 0,
            MonthsWithData: monthsWithData,
            Months: months,
            PlanMonths: planMonths,
            MonthExecutions: Enumerable.Range(0, 12).Select(i => SalesMath.Ratio(months[i].Kg, planMonths[i])).ToList(),
            Ytd: ytd,
            YtdMonths: monthsWithData.Count(m => m <= month),
            YtdArticles: ytdRows.Where(s => s.ProductId != null).Select(s => s.ProductId).Distinct().Count(),
            YtdReturnsKg: ytdReturnsKg,
            YtdReturnLines: ytdReturns.Count,
            YtdReturnsShare: SalesMath.Ratio(ytdReturnsKg, ytdRows.Where(s => !s.IsReturn).Sum(s => s.Amounts.Kg)),
            PlanYtdKg: planYtd,
            YtdExecution: SalesMath.Ratio(ytd.Kg, planYtd),
            YtdPricePerKg: PrimaryMath.PricePerKg(ytd),
            YtdMarkupSum: ytd.SumDealer - ytd.SumFactory,
            YtdMarkup: PrimaryMath.Markup(ytd),
            BoxesUnknownKg: ytdRows.Where(s => !s.BoxesKnown).Sum(s => s.Amounts.Kg),
            Categories: categoryRows,
            CategoryTotal: PrimaryMath.Total("total", "Итого", null, categoryRows, month, zero, monthTotal, ytd),
            Dealers: dealerRows,
            DealerGroups: PrimaryMath.GroupTotals(dealerRows, month, zero, monthTotal, ytd),
            DealerTotal: PrimaryMath.Total("total", "Итого", null, dealerRows, month, zero, monthTotal, ytd),
            Items: items,
            Clients: PrimaryMath.Clients(year, month, running, yearRows, product => (CategoryKey(GroupOf(product)), categories.NameOf(GroupOf(product)))),
            Calendar: null,
            Notes: notes,
            OtherStocks: PrimaryMath.OtherStocksOf(transfers.OtherStocks, year));

        return new PrimaryMonth(view, inMonth, parties, ProductsOf(inMonth, products.ToDictionary(p => p.Key, p => (p.Value.Name, p.Value.Code)), product => categories.NameOf(GroupOf(product))), zero);
    }

    /// <summary>Товары строк месяца — для разбора дня в календаре.</summary>
    private static Dictionary<long, PrimaryProduct> ProductsOf(IEnumerable<PrimaryShipment> rows, IReadOnlyDictionary<long, (string Name, string? Code)> products, Func<long?, string> categoryOf) =>
        rows.Where(s => s.ProductId != null).Select(s => s.ProductId!.Value).Distinct()
            .ToDictionary(id => id, id => products.TryGetValue(id, out var p) ? new PrimaryProduct(p.Name, p.Code, categoryOf(id)) : new PrimaryProduct($"Товар {id}", null, categoryOf(id)));

    /// <summary>День синхронизации по местному времени — самой ранней из источников; null — синхронизаций не было.</summary>
    private static DateOnly? SyncedOn(IEnumerable<DateTimeOffset?> times)
    {
        var days = times.OfType<DateTimeOffset>().Select(t => DateOnly.FromDateTime(t.ToOffset(SecondarySales.CompanyOffset).DateTime)).ToList();
        return days.Count == 0 ? null : days.Min();
    }

    /// <summary>Ключ категории отчёта (как id карточки категории): группа или «none».</summary>
    private static string CategoryKey(long? group) => group?.ToString() ?? "none";
}
