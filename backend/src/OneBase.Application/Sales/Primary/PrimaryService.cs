using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales.Metrics;

namespace OneBase.Application.Sales.Primary;

public sealed record PrimaryMonth(int Month, decimal Kg, decimal Sum, decimal ToFactoryKg);

public sealed record PrimaryDealer(long StockId, string Name, string? RegionId, decimal Kg, decimal Sum, int Shipments, decimal? Share,
    IReadOnlyList<decimal?> Days, IReadOnlyList<decimal> Months);

public sealed record PrimaryCategory(string Name, bool InReport, decimal Kg, decimal Sum, decimal? Share, IReadOnlyList<decimal> Months);

public sealed record PrimaryItem(long ProductId, string Name, string? Code, string Category, decimal Pieces, decimal Kg, decimal Sum, decimal? SumPerKg);

public sealed record PrimaryView(
    int Year,
    int Month,
    DateOnly? DataThrough,
    int DaysInMonth,
    DateTimeOffset? SyncedAt,
    string? FactoryStock,
    decimal Kg,
    decimal Sum,
    int Shipments,
    int Dealers,
    int ShipmentDays,
    decimal? ForecastKg,
    decimal PrevMonthKg,
    decimal ToFactoryKg,
    int ToFactoryTransfers,
    IReadOnlyList<int> MonthsAvailable,
    IReadOnlyList<PrimaryMonth> Months,
    IReadOnlyList<PrimaryDealer> DealerRows,
    IReadOnlyList<PrimaryCategory> Categories,
    IReadOnlyList<PrimaryItem> Items);

/// <summary>
/// Первичка — отгрузка завода дилерам: перемещения Linko со склада завода на склады регионов в статусах
/// «отдано» или «принято». Дата отгрузки — время выдачи (given_time), без него — время приёмки.
/// Вес — брутто строк перемещения, сумма — по прайс-листу перемещения (какой это прайс — завода или дилера — в API не указано).
/// Перемещения на склад завода показываются отдельно и не вычитаются: их смысл (возврат, внутреннее перемещение) не подтверждён.
/// </summary>
public sealed class PrimaryService(IAppDbContext db, SalesOptions options, Microsoft.Extensions.Caching.Memory.IMemoryCache cache, SalesCacheSignal signal)
{
    public Task<PrimaryView> GetAsync(int? year, int? month, CancellationToken ct = default) =>
        SalesViewCache.GetAsync(cache, signal, $"sales:primary:{year}:{month}", () => BuildAsync(year, month, ct));

    private sealed record Shipment(long Id, long? From, long? To, DateOnly Date, long? ProductId, decimal Pieces, decimal Kg, decimal Sum);

    public async Task<PrimaryView> BuildAsync(int? yearArg, int? monthArg, CancellationToken ct = default)
    {
        var stocks = await db.LinkoStocks.AsNoTracking().Select(s => new { s.Id, s.Name }).ToListAsync(ct);
        var factoryIds = stocks.Where(s => options.IsFactoryStock(s.Name)).Select(s => s.Id).ToHashSet();
        var factoryName = stocks.FirstOrDefault(s => factoryIds.Contains(s.Id))?.Name;
        var shipped = options.ShippedTransferStatuses;

        var raw = await (
                from l in db.LinkoStockTransferLines
                join t in db.LinkoStockTransfers on l.TransferId equals t.Id
                where shipped.Contains(t.Status)
                      && ((t.FromStockId != null && factoryIds.Contains(t.FromStockId.Value)) || (t.ToStockId != null && factoryIds.Contains(t.ToStockId.Value)))
                select new { t.Id, t.FromStockId, t.ToStockId, t.GivenAt, t.AcceptedAt, t.CreatedAt, l.ProductId, l.Amount, l.TotalWeight, l.TotalPrice })
            .AsNoTracking()
            .ToListAsync(ct);

        var all = raw
            .Select(r => (Date: r.GivenAt ?? r.AcceptedAt ?? r.CreatedAt, r))
            .Where(x => x.Date is not null)
            .Select(x => new Shipment(x.r.Id, x.r.FromStockId, x.r.ToStockId, DateOnly.FromDateTime(x.Date!.Value), x.r.ProductId, x.r.Amount, x.r.TotalWeight, x.r.TotalPrice))
            .ToList();
        bool IsFactory(long? id) => id is { } s && factoryIds.Contains(s);
        var outbound = all.Where(s => IsFactory(s.From) && !IsFactory(s.To)).ToList();
        var toFactory = all.Where(s => !IsFactory(s.From) && IsFactory(s.To)).ToList();

        var last = outbound.Count == 0 ? (DateOnly?)null : outbound.Max(s => s.Date);
        var year = yearArg ?? last?.Year ?? DateTime.Today.Year;
        var month = monthArg is >= 1 and <= 12 ? monthArg.Value : last?.Month ?? DateTime.Today.Month;
        var monthStart = new DateOnly(year, month, 1);
        var days = DateTime.DaysInMonth(year, month);
        var inMonth = outbound.Where(s => s.Date.Year == year && s.Date.Month == month).ToList();
        var dataThrough = inMonth.Count == 0 ? (DateOnly?)null : inMonth.Max(s => s.Date);

        var products = await db.LinkoProducts.AsNoTracking().Select(p => new { p.Id, p.Name, p.Code, p.TypeId }).ToDictionaryAsync(p => p.Id, ct);
        var types = await db.LinkoProductTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, ct);
        var categories = SalesCategories.Build(options.Categories, types);
        long? GroupOf(long? product) => categories.GroupOf(product is { } p && products.TryGetValue(p, out var info) ? info.TypeId : null);

        var regions = await db.SalesRegions.AsNoTracking().Select(r => new { r.Id, r.Name }).ToListAsync(ct);
        string? RegionOf(string name) => regions.FirstOrDefault(r => string.Equals(r.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase))?.Id.ToString();
        string StockName(long? id) => stocks.FirstOrDefault(s => s.Id == id)?.Name ?? $"Склад {id}";

        var yearRows = outbound.Where(s => s.Date.Year == year).ToList();
        var monthKg = inMonth.Sum(s => s.Kg);
        var workedDays = dataThrough is not null ? (year == DateTime.Today.Year && month == DateTime.Today.Month ? DateTime.Today.Day : days) : 0;
        var prevStart = monthStart.AddMonths(-1);
        var toFactoryMonth = toFactory.Where(s => s.Date.Year == year && s.Date.Month == month).ToList();

        var dealers = inMonth
            .GroupBy(s => s.To!.Value)
            .Select(g => new PrimaryDealer(
                g.Key,
                StockName(g.Key).Trim(),
                RegionOf(StockName(g.Key)),
                g.Sum(s => s.Kg),
                g.Sum(s => s.Sum),
                g.Select(s => s.Id).Distinct().Count(),
                SalesMath.Ratio(g.Sum(s => s.Kg), monthKg),
                Enumerable.Range(1, days).Select(d => g.Where(s => s.Date.Day == d).Sum(s => s.Kg) is var kg && kg != 0 ? kg : (decimal?)null).ToList(),
                Enumerable.Range(1, 12).Select(m => yearRows.Where(s => s.To == g.Key && s.Date.Month == m).Sum(s => s.Kg)).ToList()))
            .OrderByDescending(d => d.Kg)
            .ToList();

        var categoryRows = yearRows
            .GroupBy(s => GroupOf(s.ProductId))
            .Select(g => new PrimaryCategory(
                categories.NameOf(g.Key),
                SalesCategories.IsConfigured(g.Key),
                g.Where(s => s.Date.Month == month).Sum(s => s.Kg),
                g.Where(s => s.Date.Month == month).Sum(s => s.Sum),
                SalesMath.Ratio(g.Where(s => s.Date.Month == month).Sum(s => s.Kg), monthKg),
                Enumerable.Range(1, 12).Select(m => g.Where(s => s.Date.Month == m).Sum(s => s.Kg)).ToList()))
            .Where(c => c.Months.Any(v => v != 0))
            .OrderByDescending(c => c.Kg)
            .ToList();

        var items = inMonth
            .Where(s => s.ProductId != null)
            .GroupBy(s => s.ProductId!.Value)
            .Select(g =>
            {
                var product = products.GetValueOrDefault(g.Key);
                var kg = g.Sum(s => s.Kg);
                return new PrimaryItem(g.Key, product?.Name ?? $"Товар {g.Key}", product?.Code, categories.NameOf(GroupOf(g.Key)),
                    g.Sum(s => s.Pieces), kg, g.Sum(s => s.Sum), SalesMath.Ratio(g.Sum(s => s.Sum), kg));
            })
            .OrderByDescending(i => i.Kg)
            .ToList();

        var syncedAt = (await db.LinkoSyncStates.AsNoTracking().FirstOrDefaultAsync(s => s.Entity == "stock_transfers", ct))?.LastSuccessAt;

        return new PrimaryView(
            year,
            month,
            dataThrough,
            days,
            syncedAt,
            factoryName,
            monthKg,
            inMonth.Sum(s => s.Sum),
            inMonth.Select(s => s.Id).Distinct().Count(),
            dealers.Count,
            inMonth.Select(s => s.Date).Distinct().Count(),
            workedDays > 0 && workedDays < days ? SalesMath.Forecast(monthKg, workedDays, days) : null,
            outbound.Where(s => s.Date.Year == prevStart.Year && s.Date.Month == prevStart.Month).Sum(s => s.Kg),
            toFactoryMonth.Sum(s => s.Kg),
            toFactoryMonth.Select(s => s.Id).Distinct().Count(),
            outbound.Where(s => s.Date.Year == year).Select(s => s.Date.Month).Distinct().Order().ToList(),
            Enumerable.Range(1, 12).Select(m => new PrimaryMonth(m,
                yearRows.Where(s => s.Date.Month == m).Sum(s => s.Kg),
                yearRows.Where(s => s.Date.Month == m).Sum(s => s.Sum),
                toFactory.Where(s => s.Date.Year == year && s.Date.Month == m).Sum(s => s.Kg))).ToList(),
            dealers,
            categoryRows,
            items);
    }
}
