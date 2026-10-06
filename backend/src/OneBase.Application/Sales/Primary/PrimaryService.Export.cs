using Microsoft.EntityFrameworkCore;
using OneBase.Application.Sales.Metrics;

namespace OneBase.Application.Sales.Primary;

/// <summary>
/// «Первичка → Экспорт»: заказы филиала «Завод» (SalesOptions.ExcludedBranches) торговым точкам с типом EXPORT
/// (SalesOptions.ExportMarketTypes) минус возвраты по строкам. Статусы — SalesOptions.ExportSoldStatuses (доставленные), а не статусы
/// вторички. Дата — приёмка заказа (как во вторичке), у возврата — дата возврата.
/// Строки — страны: страна берётся из названия или адреса точки (SalesOptions.ExportCountries), иначе — название точки.
/// Сумма — сумма заказа в основной валюте; заказы в другой валюте дают вес без суммы. Коробок нет (в строках заказа — штуки и кг): null.
/// Приёмки позже отчётного дня (вчера, не позже синхронизации) в отчёт не входят.
/// </summary>
public sealed partial class PrimaryService
{
    public Task<PrimaryView> GetExportAsync(int? year, int? month, CancellationToken ct = default) => GetExportAsync(year, month, new PrimaryCalendarQuery(), ct);

    /// <summary>Экспорт месяца; calendar — период и разбор календаря отгрузок по странам.</summary>
    public async Task<PrimaryView> GetExportAsync(int? year, int? month, PrimaryCalendarQuery calendar, CancellationToken ct = default) =>
        WithCalendar(await SalesViewCache.GetAsync(cache, signal, $"sales:primary-export:{year}:{month}", () => BuildExportAsync(year, month, ct)), calendar);

    private sealed record ExportSale(long? OrderId, long MarketId, DateOnly Date, long? ProductId, decimal Amount, decimal Kg, decimal Sum);

    /// <summary>Экспорт: строки, страна и название точки, заказы в другой валюте — id → дата приёмки.</summary>
    private sealed record ExportData(
        IReadOnlyList<ExportSale> Sales,
        IReadOnlyDictionary<long, string> Countries,
        IReadOnlyDictionary<long, string> MarketNames,
        IReadOnlyDictionary<long, DateOnly> OtherCurrencyOrders);

    private async Task<ExportData> LoadExportAsync(CancellationToken ct)
    {
        var markets = (await db.LinkoMarkets.AsNoTracking()
                .Where(m => m.MarketTypeName != null)
                .Select(m => new { m.Id, m.Name, m.Address, m.MarketTypeName })
                .ToListAsync(ct))
            .Where(m => options.IsExportMarketType(m.MarketTypeName))
            .ToList();
        var ids = markets.Select(m => m.Id).ToList();
        var branches = options.ExcludedBranches.Select(b => b.Trim()).ToList();
        var sold = options.ExportSoldStatuses; // только доставленные — экспорт сверен с «Полевым контролем» до килограмма
        var returned = options.ReturnStatuses;

        var orders = await (
                from l in db.LinkoOrderLines
                join o in db.LinkoOrders on l.OrderId equals o.Id
                where sold.Contains(o.Status) && o.AcceptedDate != null && o.MarketId != null && ids.Contains(o.MarketId.Value)
                      && o.BranchName != null && branches.Contains(o.BranchName)
                select new { o.Id, MarketId = o.MarketId!.Value, Date = o.AcceptedDate!.Value, o.Currency, l.ProductId, l.Amount, l.TotalWeight, l.TotalPrice })
            .AsNoTracking()
            .ToListAsync(ct);

        var returns = await (
                from l in db.LinkoOrderReturnLines
                join r in db.LinkoOrderReturns on l.ReturnId equals r.Id
                where returned.Contains(r.Status) && r.MarketId != null && ids.Contains(r.MarketId.Value) && r.BranchName != null && branches.Contains(r.BranchName)
                select new { MarketId = r.MarketId!.Value, r.CreatedDate, l.ProductId, l.Amount, l.Price, l.TotalWeight })
            .AsNoTracking()
            .ToListAsync(ct);

        var sales = orders
            .Select(o => new ExportSale(o.Id, o.MarketId, o.Date, o.ProductId, o.Amount, o.TotalWeight, options.IsBaseCurrency(o.Currency) ? o.TotalPrice : 0))
            .Concat(returns.Select(r => new ExportSale(null, r.MarketId, r.CreatedDate, r.ProductId, -r.Amount, -r.TotalWeight, -(r.Amount * r.Price))))
            .ToList();

        return new ExportData(
            sales,
            markets.ToDictionary(m => m.Id, m => options.ExportCountryOf(m.Name, m.Address) ?? m.Name.Trim()),
            markets.ToDictionary(m => m.Id, m => m.Name.Trim()),
            orders.Where(o => !options.IsBaseCurrency(o.Currency)).GroupBy(o => o.Id).ToDictionary(g => g.Key, g => g.First().Date));
    }

    /// <summary>Итог экспорта с начала года (строки не позже отчётного дня cutoff) — для плитки «Экспорт» на входе в «Первичку».</summary>
    private static PrimaryCard ExportCard(ExportData export, int year, DateOnly cutoff)
    {
        var rows = export.Sales.Where(s => s.Date.Year == year && s.Date <= cutoff).ToList();
        return new PrimaryCard(
            rows.Sum(s => s.Kg),
            rows.Sum(s => s.Sum),
            rows.Select(s => export.Countries[s.MarketId]).Distinct().Count(),
            rows.Where(s => s.OrderId != null).Select(s => s.OrderId).Distinct().Count());
    }

    private async Task<PrimaryMonth> BuildExportAsync(int? yearArg, int? monthArg, CancellationToken ct)
    {
        var export = await LoadExportAsync(ct);
        var branches = options.ExcludedBranches.Select(b => b.Trim()).ToList();
        var sold = options.ExportSoldStatuses;

        // Месяц по умолчанию, дата данных и «данные по» — по заказам филиала «Завод» (любого типа точки), а не только экспорта.
        // Приёмки позже отчётного дня (сегодня и в будущем) в отчёт не входят — как во вторичке.
        var syncedAt = (await db.LinkoSyncStates.AsNoTracking().FirstOrDefaultAsync(s => s.Entity == "orders", ct))?.LastSuccessAt;
        var cutoff = PrimaryMath.DataLimit(SecondarySales.ReportCutoff(DateTimeOffset.UtcNow), SyncedOn([syncedAt]));
        var factoryDays = (await db.LinkoOrders.AsNoTracking()
                .Where(o => sold.Contains(o.Status) && o.AcceptedDate != null && o.BranchName != null && branches.Contains(o.BranchName))
                .Select(o => o.AcceptedDate!.Value)
                .Distinct()
                .ToListAsync(ct))
            .Where(d => d <= cutoff)
            .ToList();
        var asOf = PrimaryMath.AsOf(factoryDays, cutoff, null);
        var year = yearArg ?? asOf?.Year ?? DateTime.Today.Year;
        var month = monthArg is >= 1 and <= 12 ? monthArg.Value : asOf?.Month ?? DateTime.Today.Month;
        var days = DateTime.DaysInMonth(year, month);
        var monthFactoryDays = factoryDays.Where(d => d.Year == year && d.Month == month).ToList();

        var products = await db.LinkoProducts.AsNoTracking().Select(p => new { p.Id, p.Name, p.Code, p.TypeId }).ToDictionaryAsync(p => p.Id, ct);
        var types = await db.LinkoProductTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, ct);
        var categories = SalesCategories.Build(options.Categories, types);
        long? GroupOf(long? product) => categories.GroupOf(product is { } p && products.TryGetValue(p, out var info) ? info.TypeId : null);

        var pricing = PrimaryPricing.WeightOnly;
        var zero = pricing.Zero;
        var (all, afterCutoff) = PrimaryMath.ReportRows(export.Sales
            .Select(s => new PrimaryShipment(s.OrderId is { } id ? $"o:{id}" : "return", export.Countries[s.MarketId], s.Date, s.ProductId,
                pricing.Value(s.ProductId, s.Amount, s.Kg, s.Sum).Amounts, false, s.OrderId is null)), cutoff);
        var yearSales = export.Sales.Where(s => s.Date.Year == year && s.Date <= cutoff).ToList();
        var yearRows = all.Where(s => s.Date.Year == year).ToList();
        var inMonth = yearRows.Where(s => s.Date.Month == month).ToList();
        var ytdRows = yearRows.Where(s => s.Date.Month <= month).ToList();
        var running = PrimaryMath.IsRunning(year, month, asOf);

        // Строки — страны; подпись — точки Linko этой страны.
        var parties = yearSales
            .GroupBy(s => export.Countries[s.MarketId])
            .ToDictionary(g => g.Key, g => new PrimaryParty(g.Key, g.Key, string.Join(", ", g.Select(s => export.MarketNames[s.MarketId]).Distinct().Order()), null, PrimaryMath.NoPlan));

        var months = PrimaryMath.ByMonth(yearRows, zero);
        var monthTotal = months[month - 1];
        var ytd = PrimaryMath.SumOf(ytdRows.Select(s => s.Amounts), zero);
        var countryRows = PrimaryMath.PartyRows(yearRows, parties, month, zero, monthTotal, ytd)
            .OrderByDescending(r => r.Month.Kg).ThenByDescending(r => r.Months.Sum(m => m.Kg))
            .ToList();

        var categoryRows = yearRows
            .GroupBy(s => CategoryKey(GroupOf(s.ProductId)))
            .Select(g =>
            {
                var group = GroupOf(g.First().ProductId);
                return PrimaryMath.Row(g.Key, categories.NameOf(group), SalesCategories.IsConfigured(group) ? null : "вне категорий отчёта", null,
                    PrimaryMath.ByMonth(g, zero), PrimaryMath.NoPlan, month, zero, monthTotal, ytd);
            })
            .OrderByDescending(c => c.Months.Sum(m => m.Kg))
            .ToList();

        var items = ytdRows
            .Where(s => s.ProductId != null)
            .GroupBy(s => s.ProductId!.Value)
            .Select(g =>
            {
                var product = products.GetValueOrDefault(g.Key);
                var sum = PrimaryMath.SumOf(g.Select(s => s.Amounts), zero);
                return new PrimaryItemRow(g.Key, product?.Name ?? $"Товар {g.Key}", product?.Code, categories.NameOf(GroupOf(g.Key)), sum, false,
                    PrimaryMath.PricePerKg(sum), null);
            })
            .Where(i => i.Ytd.Kg != 0 || i.Ytd.SumFactory != 0)
            .OrderByDescending(i => i.Ytd.Kg)
            .ToList();

        var notes = new List<string>();
        var otherCurrency = export.OtherCurrencyOrders.Values.Count(d => d.Year == year && d <= cutoff);
        if (otherCurrency > 0)
        {
            notes.Add($"Заказов в другой валюте за {year} год: {otherCurrency} — их вес учтён, сумма нет (курса в Linko нет).");
        }

        if (afterCutoff > 0)
        {
            notes.Add($"Строк экспорта после отчётного дня ({cutoff:dd.MM.yyyy}): {afterCutoff} — в отчёт войдут, когда их день станет отчётным.");
        }

        var ytdReturns = ytdRows.Where(s => s.IsReturn).ToList();
        var ytdReturnsKg = -ytdReturns.Sum(s => s.Amounts.Kg);
        var monthsWithData = yearRows.Select(s => s.Date.Month).Distinct().Order().ToList();
        var forecast = PrimaryMath.Forecast(monthTotal.Kg, year, month, asOf);
        var view = new PrimaryView(
            Year: year,
            Month: month,
            DataThrough: running ? asOf : monthFactoryDays.Count > 0 ? monthFactoryDays.Max() : inMonth.Count == 0 ? null : inMonth.Max(s => s.Date),
            AsOf: asOf,
            Running: running,
            RunningMonth: PrimaryMath.RunningMonth(year, asOf),
            DaysInMonth: days,
            WorkedDays: PrimaryMath.WorkedDays(year, month, asOf, inMonth.Count > 0 || monthFactoryDays.Count > 0),
            SyncedAt: syncedAt,
            FactoryStock: null,
            ExportStock: null,
            DealerPriceList: null,
            Republic: new PrimaryCard(0, 0, 0, 0),
            Export: ExportCard(export, year, cutoff),
            MonthTotal: monthTotal,
            PlanMonthKg: null,
            MonthExecution: null,
            MonthRemainingKg: null,
            MonthOverPlanKg: null,
            ForecastKg: forecast,
            ForecastExecution: null,
            MonthTransfers: inMonth.Where(s => !s.IsReturn).Select(s => s.Doc).Distinct().Count(),
            MonthCounterparties: countryRows.Count(r => r.Month.Kg > 0),
            HasPlan: false,
            MonthsWithData: monthsWithData,
            Months: months,
            PlanMonths: PrimaryMath.NoPlan,
            MonthExecutions: PrimaryMath.NoPlan,
            Ytd: ytd,
            YtdMonths: monthsWithData.Count(m => m <= month),
            YtdArticles: ytdRows.Where(s => s.ProductId != null).Select(s => s.ProductId).Distinct().Count(),
            YtdReturnsKg: ytdReturnsKg,
            YtdReturnLines: ytdReturns.Count,
            YtdReturnsShare: SalesMath.Ratio(ytdReturnsKg, ytdRows.Where(s => !s.IsReturn).Sum(s => s.Amounts.Kg)),
            PlanYtdKg: null,
            YtdExecution: null,
            YtdPricePerKg: PrimaryMath.PricePerKg(ytd),
            YtdMarkupSum: null,
            YtdMarkup: null,
            BoxesUnknownKg: 0, // коробок у экспорта нет вовсе
            Categories: categoryRows,
            CategoryTotal: PrimaryMath.Total("total", "Итого", null, categoryRows, month, zero, monthTotal, ytd),
            Dealers: countryRows,
            DealerGroups: [],
            DealerTotal: PrimaryMath.Total("total", "Итого", null, countryRows, month, zero, monthTotal, ytd),
            Items: items,
            Clients: null,
            Calendar: null,
            Notes: notes);

        return new PrimaryMonth(view, inMonth, parties, ProductsOf(inMonth, products.ToDictionary(p => p.Key, p => (p.Value.Name, p.Value.Code)), product => categories.NameOf(GroupOf(product))), zero);
    }
}
