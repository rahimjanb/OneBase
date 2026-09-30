using Microsoft.EntityFrameworkCore;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Sales.Stock;

namespace OneBase.Application.Sales.Primary;

/// <summary>
/// «Первичка → Экспорт»: заказы филиала «Завод» (SalesOptions.ExcludedBranches) торговым точкам с типом EXPORT
/// (SalesOptions.ExportMarketTypes) минус возвраты по строкам. Дата — приёмка заказа (как во вторичке), у возврата — дата возврата.
/// Строки — страны: страна берётся из названия или адреса точки (SalesOptions.ExportCountries), иначе — название точки.
/// Сумма — сумма заказа в основной валюте; заказы в другой валюте дают вес без суммы.
/// </summary>
public sealed partial class PrimaryService
{
    public Task<PrimaryView> GetExportAsync(int? year, int? month, CancellationToken ct = default) =>
        SalesViewCache.GetAsync(cache, signal, $"sales:primary-export:{year}:{month}", () => BuildExportAsync(year, month, ct));

    private sealed record ExportSale(long? OrderId, long MarketId, DateOnly Date, long? ProductId, decimal Amount, decimal Kg, decimal Sum);

    private sealed record ExportData(IReadOnlyList<ExportSale> Sales, IReadOnlyDictionary<long, string> Countries, IReadOnlyDictionary<long, string> MarketNames, int OtherCurrencyOrders);

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
        var sold = options.SoldStatuses;
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
            orders.Where(o => !options.IsBaseCurrency(o.Currency)).Select(o => o.Id).Distinct().Count());
    }

    /// <summary>Итог экспорта с начала года — для плитки «Экспорт» на входе в «Первичку».</summary>
    private static PrimaryCard ExportCard(ExportData export, int year)
    {
        var rows = export.Sales.Where(s => s.Date.Year == year).ToList();
        return new PrimaryCard(
            rows.Sum(s => s.Kg),
            rows.Sum(s => s.Sum),
            rows.Select(s => export.Countries[s.MarketId]).Distinct().Count(),
            rows.Where(s => s.OrderId != null).Select(s => s.OrderId).Distinct().Count());
    }

    public async Task<PrimaryView> BuildExportAsync(int? yearArg, int? monthArg, CancellationToken ct = default)
    {
        var export = await LoadExportAsync(ct);
        var branches = options.ExcludedBranches.Select(b => b.Trim()).ToList();
        var sold = options.SoldStatuses;

        // Месяц по умолчанию и «данные по» — по последнему заказу филиала «Завод» (любого типа точки), а не только экспорта.
        var factoryLast = await db.LinkoOrders.AsNoTracking()
            .Where(o => sold.Contains(o.Status) && o.AcceptedDate != null && o.BranchName != null && branches.Contains(o.BranchName))
            .MaxAsync(o => o.AcceptedDate, ct);
        var last = factoryLast ?? (export.Sales.Count == 0 ? (DateOnly?)null : export.Sales.Max(s => s.Date));
        var year = yearArg ?? last?.Year ?? DateTime.Today.Year;
        var month = monthArg is >= 1 and <= 12 ? monthArg.Value : last?.Month ?? DateTime.Today.Month;
        var days = DateTime.DaysInMonth(year, month);
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1);
        var monthFactoryLast = await db.LinkoOrders.AsNoTracking()
            .Where(o => sold.Contains(o.Status) && o.AcceptedDate >= from && o.AcceptedDate < to && o.BranchName != null && branches.Contains(o.BranchName))
            .MaxAsync(o => o.AcceptedDate, ct);

        var products = await db.LinkoProducts.AsNoTracking().Select(p => new { p.Id, p.Name, p.Code, p.TypeId }).ToDictionaryAsync(p => p.Id, ct);
        var packs = products.ToDictionary(p => p.Key, p => StockMath.ParsePack(p.Value.Name));
        var types = await db.LinkoProductTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, ct);
        var categories = SalesCategories.Build(options.Categories, types);
        long? GroupOf(long? product) => categories.GroupOf(product is { } p && products.TryGetValue(p, out var info) ? info.TypeId : null);

        var shipments = export.Sales
            .Select(s =>
            {
                var (amounts, boxesKnown) = AmountsOf(s.ProductId is { } p ? packs.GetValueOrDefault(p) : null, s.Amount, s.Kg, s.Sum, 0);
                return (Sale: s, Country: export.Countries[s.MarketId], Amounts: amounts, BoxesKnown: boxesKnown);
            })
            .ToList();
        var yearRows = shipments.Where(s => s.Sale.Date.Year == year).ToList();
        var inMonth = yearRows.Where(s => s.Sale.Date.Month == month).ToList();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var running = year == today.Year && month == today.Month;
        var dataThrough = monthFactoryLast ?? (inMonth.Count == 0 ? (DateOnly?)null : inMonth.Max(s => s.Sale.Date));
        var workedDays = dataThrough is null ? 0 : running ? today.Day : days;

        static PrimaryAmounts Sum(IEnumerable<PrimaryAmounts> source) => source.Aggregate(PrimaryAmounts.Zero, (a, s) => a.Add(s));
        IReadOnlyList<PrimaryAmounts> ByMonth<T>(IEnumerable<T> source, Func<T, DateOnly> dateOf, Func<T, PrimaryAmounts> amountsOf)
        {
            var list = source.ToLookup(s => dateOf(s).Month);
            return Enumerable.Range(1, 12).Select(m => Sum(list[m].Select(amountsOf))).ToList();
        }

        IReadOnlyList<decimal?> noPlan = Enumerable.Repeat<decimal?>(null, 12).ToList();
        var countryRows = yearRows
            .GroupBy(s => s.Country)
            .Select(g => new PrimaryRow(
                g.Key,
                g.Key,
                string.Join(", ", g.Select(s => export.MarketNames[s.Sale.MarketId]).Distinct().Order()),
                Sum(g.Where(s => s.Sale.Date.Month == month).Select(s => s.Amounts)),
                ByMonth(g, s => s.Sale.Date, s => s.Amounts),
                null,
                noPlan))
            .OrderByDescending(r => r.Month.Kg).ThenByDescending(r => r.Months.Sum(m => m.Kg))
            .ToList();

        var categoryRows = yearRows
            .GroupBy(s => CategoryKey(GroupOf(s.Sale.ProductId)))
            .Select(g =>
            {
                var group = GroupOf(g.First().Sale.ProductId);
                return new PrimaryRow(g.Key, categories.NameOf(group), SalesCategories.IsConfigured(group) ? null : "вне категорий отчёта",
                    Sum(g.Where(s => s.Sale.Date.Month == month).Select(s => s.Amounts)), ByMonth(g, s => s.Sale.Date, s => s.Amounts), null, noPlan);
            })
            .OrderByDescending(c => c.Months.Sum(m => m.Kg))
            .ToList();

        var ytdRows = yearRows.Where(s => s.Sale.Date.Month <= month).ToList();
        var items = yearRows
            .Where(s => s.Sale.ProductId != null)
            .GroupBy(s => s.Sale.ProductId!.Value)
            .Select(g =>
            {
                var product = products.GetValueOrDefault(g.Key);
                return new PrimaryItemRow(g.Key, product?.Name ?? $"Товар {g.Key}", product?.Code, categories.NameOf(GroupOf(g.Key)),
                    Sum(g.Select(s => s.Amounts)), g.All(s => s.BoxesKnown));
            })
            .OrderByDescending(i => i.Ytd.Kg)
            .ToList();

        var monthKg = inMonth.Sum(s => s.Amounts.Kg);
        var notes = new List<string>();
        if (export.OtherCurrencyOrders > 0)
        {
            notes.Add($"Заказов в другой валюте: {export.OtherCurrencyOrders} — их вес учтён, сумма нет (курса в Linko нет).");
        }

        var syncedAt = (await db.LinkoSyncStates.AsNoTracking().FirstOrDefaultAsync(s => s.Entity == "orders", ct))?.LastSuccessAt;
        return new PrimaryView(
            year,
            month,
            dataThrough,
            days,
            workedDays,
            syncedAt,
            null,
            null,
            null,
            new PrimaryCard(0, 0, 0, 0),
            ExportCard(export, year),
            Sum(inMonth.Select(s => s.Amounts)),
            null,
            running && workedDays > 0 ? SalesMath.Forecast(monthKg, workedDays, days) : null,
            inMonth.Where(s => s.Sale.OrderId != null).Select(s => s.Sale.OrderId).Distinct().Count(),
            yearRows.Select(s => s.Sale.Date.Month).Distinct().Order().ToList(),
            ByMonth(yearRows, s => s.Sale.Date, s => s.Amounts),
            noPlan,
            Sum(ytdRows.Select(s => s.Amounts)),
            ytdRows.Where(s => s.Sale.ProductId != null).Select(s => s.Sale.ProductId).Distinct().Count(),
            -ytdRows.Where(s => s.Sale.OrderId == null).Sum(s => s.Amounts.Kg),
            ytdRows.Count(s => s.Sale.OrderId == null),
            null,
            ytdRows.Where(s => !s.BoxesKnown).Sum(s => s.Amounts.Kg),
            categoryRows,
            countryRows,
            items,
            inMonth.Select(s => new PrimaryLine(s.Sale.Date.Day, s.Country, s.Sale.ProductId, s.Amounts.Kg, s.Amounts.Boxes, s.Amounts.SumFactory, 0)).ToList(),
            inMonth.Where(s => s.Sale.ProductId != null).Select(s => s.Sale.ProductId!.Value).Distinct()
                .ToDictionary(id => id, id => products.TryGetValue(id, out var p) ? p.Name : $"Товар {id}"),
            notes);
    }

    /// <summary>
    /// Коробки — по весу коробки из названия: у весового товара (единица учёта — 1 кг) любой, у штучного — если в коробке
    /// целое число штук этой строки. Иначе название не совпадает с тем, как товар учитывается, и коробки не считаются.
    /// </summary>
    private static (PrimaryAmounts Amounts, bool BoxesKnown) AmountsOf(PackInfo? pack, decimal amount, decimal kg, decimal sumFactory, decimal sumDealer)
    {
        var unitKg = StockMath.UnitKg(kg, amount);
        var boxKg = pack?.BoxKg is { } b && unitKg is { } u && (Math.Abs(u - 1) < 0.001m || StockMath.BoxConsistent(b, u)) ? b : (decimal?)null;
        return (new PrimaryAmounts(kg, boxKg is { } bk ? kg / bk : 0, sumFactory, sumDealer), boxKg is not null);
    }
}
