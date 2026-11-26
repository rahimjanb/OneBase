using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;

namespace OneBase.Application.Sales.Tajik;

/// <summary>
/// «Экспорт → Таджикистан»: продажи дилера по его 1С — все регионы, регион (точки и что в них продано по категориям), точка (накладные),
/// остаток склада. Вес — по весу единицы 1С (<see cref="TajikWeights"/>), суммы — сомони, без пересчёта в сумы (курса в данных нет).
/// </summary>
public sealed class TajikSalesService(IAppDbContext db, TajikDataLoader loader, ITajikSettingsStore settings, TimeProvider time)
{
    public const string NoCategory = "Без категории";

    public async Task<TajikOverview> OverviewAsync(int? year, int? month, CancellationToken ct)
    {
        var (period, facts, prevFacts, prev) = await LoadAsync(year, month, ct);
        var catalog = await loader.CatalogAsync(ct);
        var totals = Totals(facts);

        // Регионы с продажами в месяце и те, что продавали в прошлом месяце за те же дни, но в этом молчат.

        var regions = facts.GroupBy(f => f.Region.Id)
            .Select(g => RegionRow(g.Key, g.First().Region.Name, g.ToList(), totals.Kg, prevFacts.Where(p => p.Region.Id == g.Key).ToList()))
            .Concat(prevFacts.Where(p => facts.All(f => f.Region.Id != p.Region.Id)).GroupBy(p => p.Region.Id)
                .Select(g => RegionRow(g.Key, g.First().Region.Name, [], totals.Kg, g.ToList())))
            .OrderBy(r => r.Id == TajikRegionResolver.NoneId ? 1 : 0).ThenByDescending(r => r.Kg).ThenBy(r => r.Name)
            .ToList();

        var unresolved = facts.Where(f => f.Region.Id == TajikRegionResolver.NoneId).ToList();
        var (stockDate, stock) = await StockAsync(catalog, ct);

        return new TajikOverview(
            period,
            await SyncAsync(ct),
            totals,
            prev,
            Categories(facts, prevFacts),
            regions,
            Agents(facts),
            Products(facts, catalog, withStores: false),
            stockDate,
            stock,
            new TajikUnresolved(unresolved.Select(f => f.StoreId).Distinct().Count(), unresolved.Sum(f => f.Kg), Share(unresolved.Sum(f => f.Kg), totals.Kg)),
            catalog.Values.Count(p => p.Weight.Kg is null));
    }

    public async Task<TajikRegionView?> RegionAsync(string id, int? year, int? month, string? category, CancellationToken ct)
    {
        var regions = await loader.RegionsAsync(ct);
        var region = regions.Find(id);
        if (region is null && id != TajikRegionResolver.NoneId)
        {
            return null;
        }

        var (period, all, allPrev, _) = await LoadAsync(year, month, ct, regions);
        var catalog = await loader.CatalogAsync(ct);
        var facts = all.Where(f => f.Region.Id == id).ToList();
        var prevFacts = allPrev.Where(f => f.Region.Id == id).ToList();
        var categories = Categories(facts, prevFacts);

        var filter = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        var shown = filter is null ? facts : facts.Where(f => (f.Category ?? NoCategory) == filter).ToList();
        var shownPrev = filter is null ? prevFacts : prevFacts.Where(f => (f.Category ?? NoCategory) == filter).ToList();

        return new TajikRegionView(
            period,
            id,
            region?.Name ?? TajikRegionResolver.NoneName,
            region?.Keywords ?? [],
            filter,
            Totals(shown),
            Compare(period, shown, shownPrev),
            categories,
            Stores(shown),
            Products(shown, catalog, withStores: true),
            Agents(shown));
    }

    public async Task<TajikStoreView?> StoreAsync(string id, int? year, int? month, CancellationToken ct)
    {
        var regions = await loader.RegionsAsync(ct);
        var catalog = await loader.CatalogAsync(ct);

        // Название точки — по id (хэш названия) среди всех загруженных продаж.
        var names = await db.TajikSaleLines.AsNoTracking().Where(l => l.RemovedAt == null).Select(l => l.Customer).Distinct().ToListAsync(ct);
        var name = names.FirstOrDefault(n => TajikText.StoreId(n) == id);
        if (name is null)
        {
            return null;
        }

        var lines = await db.TajikSaleLines.AsNoTracking().Where(l => l.RemovedAt == null && l.Customer == name)
            .OrderBy(l => l.DocDate).ThenBy(l => l.DocNumber).ThenBy(l => l.LineNo).ToListAsync(ct);
        var all = lines.Select(l => TajikDataLoader.Fact(l, catalog, regions)).ToList();
        var period = await PeriodAsync(year, month, ct);
        var facts = all.Where(f => f.Date.Year == period.Year && f.Date.Month == period.Month).ToList();
        var compare = Compare(period, facts, all.Where(f => f.Date < new DateOnly(period.Year, period.Month, 1)).ToList());
        var prevFacts = compare is null ? [] : all.Where(f => f.Date >= compare.From && f.Date <= compare.To).ToList();
        var last = all[^1];

        var documents = facts.GroupBy(f => (f.Doc, f.Date))
            .Select(g => new TajikDocument(g.Key.Doc, g.Key.Date, g.First().Agent, g.Sum(f => f.Amount), g.Sum(f => f.Kg), g.Sum(f => f.Discount),
                g.OrderBy(f => f.LineNo).Select(f => new TajikDocumentLine(f.Code, f.Name, f.Category, f.Qty, f.Price, f.Amount, f.Discount, f.DiscountPercent,
                    f.KnownWeight ? f.Kg : null)).ToList()))
            .OrderByDescending(d => d.Date).ThenByDescending(d => d.Number)
            .ToList();

        var months = all.GroupBy(f => (f.Date.Year, f.Date.Month))
            .Select(g => new TajikStoreMonth(g.Key.Year, g.Key.Month, g.Sum(f => f.Kg), g.Sum(f => f.Amount), g.Select(f => (f.Doc, f.Date)).Distinct().Count()))
            .OrderBy(m => m.Year).ThenBy(m => m.Month)
            .ToList();

        return new TajikStoreView(period, id, last.Store, last.Agent, last.Region.Id, last.Region.Name, last.Region.Source, Totals(facts),
            Categories(facts, prevFacts), Products(facts, catalog, withStores: false), documents, months);
    }

    /// <summary>Период, строки месяца, строки прошлого месяца за те же дни (нет сравнения — пусто) и само сравнение.</summary>
    private async Task<(TajikPeriod Period, List<TajikFact> Facts, List<TajikFact> PrevFacts, TajikCompare? Prev)> LoadAsync(
        int? year,
        int? month,
        CancellationToken ct,
        TajikRegionResolver? regions = null)
    {
        var period = await PeriodAsync(year, month, ct);
        regions ??= await loader.RegionsAsync(ct);
        var catalog = await loader.CatalogAsync(ct);
        var first = new DateOnly(period.Year, period.Month, 1);
        var prevFirst = first.AddMonths(-1);
        var all = await loader.FactsAsync(prevFirst, first.AddMonths(1).AddDays(-1), catalog, regions, ct);
        var facts = all.Where(f => f.Date >= first).ToList();
        var compare = Compare(period, facts, all.Where(f => f.Date < first).ToList());
        var window = compare is null ? [] : all.Where(f => f.Date >= compare.From && f.Date <= compare.To).ToList();
        return (period, facts, window, compare);
    }

    public async Task<TajikPeriod> PeriodAsync(int? year, int? month, CancellationToken ct)
    {
        var months = await loader.MonthsAsync(ct);
        var today = TajikText.Today(time.GetUtcNow());
        var (y, m) = year is { } yy and >= 2000 and <= 2100 && month is { } mm and >= 1 and <= 12
            ? (yy, mm)
            : months.Count > 0 ? (months[^1].Year, months[^1].Month) : (today.Year, today.Month);
        var first = new DateOnly(y, m, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var through = await db.TajikSaleLines.AsNoTracking().Where(l => l.RemovedAt == null && l.DocDate >= first && l.DocDate <= last)
            .MaxAsync(l => (DateOnly?)l.DocDate, ct);
        return new TajikPeriod(y, m, through, last < today, await loader.FirstDateAsync(ct), months);
    }

    /// <summary>
    /// Прошлый месяц за те же дни: у идущего месяца — с 1-го по день последних данных, у закрытого — весь месяц. Нет продаж в прошлом
    /// месяце вовсе (данные начинаются позже) — сравнения нет.
    /// </summary>
    private static TajikCompare? Compare(TajikPeriod period, List<TajikFact> facts, List<TajikFact> prevFacts)
    {
        var prevFirst = new DateOnly(period.Year, period.Month, 1).AddMonths(-1);
        if (period.DataFrom is not { } dataFrom || dataFrom > prevFirst)
        {
            return null;
        }

        var prevLast = prevFirst.AddMonths(1).AddDays(-1);
        var to = !period.Closed && period.DataThrough is { } through ? new DateOnly(prevFirst.Year, prevFirst.Month, Math.Min(through.Day, prevLast.Day)) : prevLast;
        var window = prevFacts.Where(f => f.Date <= to).ToList();
        var kg = window.Sum(f => f.Kg);
        var amount = window.Sum(f => f.Amount);
        return new TajikCompare(prevFirst, to, kg, amount, Ratio(facts.Sum(f => f.Kg), kg), Ratio(facts.Sum(f => f.Amount), amount));
    }

    private async Task<TajikSyncInfo> SyncAsync(CancellationToken ct)
    {
        var s = await settings.GetAsync(ct);
        var last = await db.TajikSyncRuns.AsNoTracking().OrderByDescending(r => r.Id).FirstOrDefaultAsync(ct);
        var ok = await db.TajikSyncRuns.AsNoTracking().Where(r => r.Status == "ok").OrderByDescending(r => r.Id).Select(r => r.FinishedAt).FirstOrDefaultAsync(ct);
        return new TajikSyncInfo(s.Configured, s.Enabled, ok, last?.StartedAt, last?.Status, last?.Error);
    }

    private async Task<(DateOnly? Date, IReadOnlyList<TajikStockRow> Rows)> StockAsync(IReadOnlyDictionary<string, TajikProductInfo> catalog, CancellationToken ct)
    {
        var date = await db.TajikStockItems.AsNoTracking().MaxAsync(s => (DateOnly?)s.Date, ct);
        if (date is not { } d)
        {
            return (null, []);
        }

        var rows = await db.TajikStockItems.AsNoTracking().Where(s => s.Date == d).ToListAsync(ct);
        var from = d.AddDays(-29);
        var sold = await db.TajikSaleLines.AsNoTracking()
            .Where(l => l.RemovedAt == null && l.DocDate >= from && l.DocDate <= d)
            .GroupBy(l => l.ProductCode)
            .Select(g => new { Code = g.Key, Qty = g.Sum(l => l.Qty) })
            .ToDictionaryAsync(x => x.Code, x => x.Qty, ct);

        return (d, rows.Select(r =>
        {
            var info = catalog.GetValueOrDefault(r.ProductCode);
            var soldQty = sold.GetValueOrDefault(r.ProductCode);
            return new TajikStockRow(r.ProductCode, r.ProductName, r.Warehouse, info?.Category, r.Qty, info?.Weight.Kg is { } kg ? r.Qty * kg : null, soldQty,
                soldQty > 0 ? Math.Round(r.Qty / (soldQty / 30m), 1) : null);
        }).OrderBy(r => r.Category is null ? 1 : 0).ThenBy(r => r.Category).ThenBy(r => r.Name).ToList());
    }

    public static TajikTotals Totals(IReadOnlyCollection<TajikFact> facts) => new(
        facts.Sum(f => f.Amount),
        facts.Sum(f => f.Kg),
        facts.Sum(f => f.Qty),
        facts.Sum(f => f.Discount),
        facts.Select(f => (f.Doc, f.Date)).Distinct().Count(),
        facts.Select(f => f.StoreId).Distinct().Count(),
        facts.Where(f => f.Agent != null).Select(f => f.Agent).Distinct().Count(),
        facts.Select(f => f.Code).Distinct().Count());

    private IReadOnlyList<TajikCategoryRow> Categories(List<TajikFact> facts, List<TajikFact> prevFacts)
    {
        var total = facts.Sum(f => f.Kg);
        var order = loader.CategoryOrder;
        return facts.Select(f => f.Category ?? NoCategory).Concat(prevFacts.Select(f => f.Category ?? NoCategory)).Distinct()
            .Select(name =>
            {
                var rows = facts.Where(f => (f.Category ?? NoCategory) == name).ToList();
                return new TajikCategoryRow(name, rows.Sum(f => f.Kg), rows.Sum(f => f.Amount), rows.Sum(f => f.Qty), rows.Select(f => f.StoreId).Distinct().Count(),
                    Share(rows.Sum(f => f.Kg), total), prevFacts.Where(f => (f.Category ?? NoCategory) == name).Sum(f => f.Kg));
            })
            .OrderBy(c => c.Name == NoCategory ? 1 : 0)
            .ThenByDescending(c => c.Kg)
            .ThenBy(c => order.ToList().IndexOf(c.Name))
            .ToList();
    }

    private static TajikRegionRow RegionRow(string id, string name, List<TajikFact> facts, decimal totalKg, List<TajikFact> prevFacts)
    {
        var kg = facts.Sum(f => f.Kg);
        var prevKg = prevFacts.Sum(f => f.Kg);
        return new TajikRegionRow(id, name, kg, facts.Sum(f => f.Amount), facts.Select(f => (f.Doc, f.Date)).Distinct().Count(),
            facts.Select(f => f.StoreId).Distinct().Count(), facts.Where(f => f.Agent != null).Select(f => f.Agent).Distinct().Count(),
            Share(kg, totalKg), prevKg, Ratio(kg, prevKg), Shares(facts));
    }

    private static List<TajikCategoryShare> Shares(List<TajikFact> facts)
    {
        var total = facts.Sum(f => f.Kg);
        return facts.GroupBy(f => f.Category ?? NoCategory)
            .Select(g => new TajikCategoryShare(g.Key, g.Sum(f => f.Kg), Share(g.Sum(f => f.Kg), total)))
            .OrderByDescending(c => c.Kg)
            .ToList();
    }

    private static IReadOnlyList<TajikAgentRow> Agents(List<TajikFact> facts) =>
        facts.GroupBy(f => f.Agent ?? "—")
            .Select(g => new TajikAgentRow(g.Key, g.Sum(f => f.Kg), g.Sum(f => f.Amount), g.Select(f => (f.Doc, f.Date)).Distinct().Count(),
                g.Select(f => f.StoreId).Distinct().Count(),
                g.GroupBy(f => f.Region.Name).OrderByDescending(r => r.Sum(f => f.Kg)).Select(r => r.Key).ToList()))
            .OrderByDescending(a => a.Kg)
            .ToList();

    private static IReadOnlyList<TajikStoreRow> Stores(List<TajikFact> facts) =>
        facts.GroupBy(f => f.StoreId)
            .Select(g =>
            {
                var last = g.OrderBy(f => f.Date).Last();
                return new TajikStoreRow(g.Key, last.Store, last.Agent, last.Region.Id, last.Region.Name, last.Region.Source, g.Sum(f => f.Kg), g.Sum(f => f.Amount),
                    g.Select(f => (f.Doc, f.Date)).Distinct().Count(), last.Date, Shares(g.ToList()));
            })
            .OrderByDescending(s => s.Kg)
            .ToList();

    private static IReadOnlyList<TajikProductRow> Products(List<TajikFact> facts, IReadOnlyDictionary<string, TajikProductInfo> catalog, bool withStores)
    {
        return facts.GroupBy(f => f.Code)
            .Select(g =>
            {
                var info = catalog.GetValueOrDefault(g.Key);
                var qty = g.Sum(f => f.Qty);
                var gross = g.Sum(f => f.Qty * f.Price);
                return new TajikProductRow(g.Key, g.Last().Name, g.First().Category, info?.LinkoName, info?.Weight.Kg, info?.Weight.Source ?? TajikWeights.Unknown, qty,
                    g.Sum(f => f.Kg), g.Sum(f => f.Amount), g.Sum(f => f.Discount), g.Select(f => f.StoreId).Distinct().Count(), qty > 0 ? Math.Round(gross / qty, 2) : null,
                    withStores
                        ? g.GroupBy(f => f.StoreId)
                            .Select(s => new TajikProductStore(s.Key, s.Last().Store, s.Last().Region.Name, s.Sum(f => f.Qty), s.Sum(f => f.Kg), s.Sum(f => f.Amount)))
                            .OrderByDescending(s => s.Kg).ThenByDescending(s => s.Qty)
                            .ToList()
                        : null);
            })
            .OrderBy(p => p.Category is null ? 1 : 0)
            .ThenByDescending(p => p.Kg)
            .ToList();
    }

    private static decimal? Share(decimal part, decimal total) => total > 0 ? Math.Round(part / total, 4) : null;

    private static decimal? Ratio(decimal current, decimal previous) => previous > 0 ? Math.Round(current / previous - 1, 4) : null;
}
