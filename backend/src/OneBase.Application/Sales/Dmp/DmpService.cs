using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales.SkuSales;

namespace OneBase.Application.Sales.Dmp;

/// <summary>
/// «ДМП»: стеллажи в магазинах из копии Linko. Стеллаж — товар типа из Sales:DmpProductTypes («Оборудование» с подтипами). Приход — принятые
/// перемещения со склада завода на склады регионов; остаток — остатки складов; установка — заказы магазинов со строкой стеллажа
/// (Sales:DmpInstalledStatuses / DmpIssuedStatuses, дата реализации — как во вторичке), возвраты стеллажа вычитаются. Заказы магазинов —
/// проданные статусы вторички за вычетом возвратов, без строк оборудования. Реестра «Склад → Работа с инвентарём» (серийные номера)
/// внешний API Linko не отдаёт — его в разделе нет.
/// </summary>
public sealed class DmpService(IAppDbContext db, SalesOptions options, SalesDataLoader loader, IMemoryCache cache, SalesCacheSignal signal)
{
    public Task<DmpView?> GetAsync(CancellationToken ct = default) => SalesViewCache.GetAsync(cache, signal, "sales:dmp", () => BuildAsync(ct));

    private async Task<DmpView?> BuildAsync(CancellationToken ct)
    {
        var types = await LinkoTypeTree.LoadAsync(db, ct);
        var roots = types.Names
            .Where(t => options.DmpProductTypes.Any(n => string.Equals(n.Trim(), t.Value.Trim(), StringComparison.OrdinalIgnoreCase)))
            .Select(t => t.Key);
        var shelfTypes = SalesCategories.WithDescendants(roots, types.Parents);
        var shelfProducts = await db.LinkoProducts.AsNoTracking()
            .Where(p => p.TypeId != null && shelfTypes.Contains(p.TypeId.Value))
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(ct);
        if (shelfProducts.Count == 0)
        {
            return null;
        }

        var shelfIds = shelfProducts.Select(p => p.Id).ToList();
        var asOf = await loader.LastDataAsync(ct) ?? SecondarySales.Today(DateTimeOffset.UtcNow);
        var stocks = await db.LinkoStocks.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        string StockRegion(long? id) => id is { } s && stocks.TryGetValue(s, out var name) ? options.StockRegionName(name) : "—";
        bool Factory(long? id) => id is { } s && stocks.TryGetValue(s, out var name) && options.IsFactoryStock(name);

        // Приход: перемещения стеллажа с завода на склады регионов, принятые дилером.
        var transfers = await (
                from l in db.LinkoStockTransferLines
                join t in db.LinkoStockTransfers on l.TransferId equals t.Id
                where l.ProductId != null && shelfIds.Contains(l.ProductId.Value) && t.AcceptedDate != null
                select new { t.FromStockId, t.ToStockId, Date = t.AcceptedDate!.Value, l.Amount })
            .ToListAsync(ct);
        var receipts = transfers
            .Where(t => Factory(t.FromStockId) && !Factory(t.ToStockId) && !IsExport(t.ToStockId) && t.Date <= asOf)
            .Select(t => new DmpReceipt(StockRegion(t.ToStockId), t.Date, t.Amount))
            .ToList();

        var balances = (await db.LinkoProductBalances.AsNoTracking()
                .Where(b => shelfIds.Contains(b.ProductId) && b.Balance != 0)
                .Select(b => new { b.StockId, b.Balance })
                .ToListAsync(ct))
            .Where(b => !IsExport(b.StockId))
            .Select(b => new DmpBalance(StockRegion(b.StockId), Factory(b.StockId), b.Balance))
            .ToList();

        var structure = SkuSalesService.RegionsOf(await db.SalesRegions.AsNoTracking().ToListAsync(ct), options);
        var regionNames = structure.Regions.ToDictionary(r => r.Id, r => r.Name);
        string RegionOf(long? branch, string? branchName) =>
            branch is { } b && structure.BranchRegions.TryGetValue(b, out var id) && regionNames.TryGetValue(id, out var name) ? name : branchName ?? "—";

        // Строки стеллажа в заказах и возвратах магазинов.
        var moves = options.DmpInstalledStatuses.Concat(options.DmpIssuedStatuses).ToList();
        var orderShelves = await (
                from l in db.LinkoOrderLines
                join o in db.LinkoOrders on l.OrderId equals o.Id
                where l.ProductId != null && shelfIds.Contains(l.ProductId.Value) && o.MarketId != null && moves.Contains(o.Status)
                select new { o.Id, o.Status, o.CreatedDate, o.DeliveryDate, o.AcceptedDate, MarketId = o.MarketId!.Value, o.AgentId, o.BranchId, o.BranchName, l.Amount })
            .ToListAsync(ct);
        var returned = options.ReturnStatuses;
        var returnShelves = await (
                from l in db.LinkoOrderReturnLines
                join r in db.LinkoOrderReturns on l.ReturnId equals r.Id
                where l.ProductId != null && shelfIds.Contains(l.ProductId.Value) && r.MarketId != null && returned.Contains(r.Status)
                select new { r.Id, r.CreatedDate, MarketId = r.MarketId!.Value, r.AgentId, r.BranchId, r.BranchName, l.Amount })
            .ToListAsync(ct);
        // Выданный заказ ещё не принят — его день: доставка, без неё — создание.
        var shelves = orderShelves
            .Select(o => (Line: o, Installed: options.DmpInstalledStatuses.Contains(o.Status)))
            .Select(x => (x.Line, x.Installed,
                Date: x.Installed ? SaleDate(x.Line.CreatedDate, x.Line.DeliveryDate, x.Line.AcceptedDate) : x.Line.AcceptedDate ?? x.Line.DeliveryDate ?? x.Line.CreatedDate))
            .Where(x => x.Date is not null)
            .Select(x => new DmpShelfLine(x.Line.Id, x.Installed ? DmpMove.Installed : DmpMove.Issued, x.Date!.Value,
                x.Line.MarketId, x.Line.AgentId, RegionOf(x.Line.BranchId, x.Line.BranchName), x.Line.Amount))
            .Concat(returnShelves.Select(r => new DmpShelfLine(r.Id, DmpMove.Returned, r.CreatedDate, r.MarketId, r.AgentId,
                RegionOf(r.BranchId, r.BranchName), r.Amount)))
            .ToList();

        // Заказы магазинов со стеллажом: окна до и после установки и последние месяцы (возвраты вычтены, оборудование не считается).
        var marketIds = shelves.Select(s => s.MarketId).Distinct().ToList();
        var monthsFrom = new DateOnly(asOf.Year, asOf.Month, 1).AddMonths(1 - DmpCompute.MonthsShown);
        var installs = shelves.Where(s => s.Move != DmpMove.Returned).Select(s => s.Date).ToList();
        var windowFrom = installs.Count == 0 ? monthsFrom : installs.Min().AddDays(-(asOf.DayNumber - installs.Min().DayNumber));
        var since = windowFrom < monthsFrom ? windowFrom : monthsFrom;
        var categories = SalesCategories.Build(options.Categories, types);
        var focus = options.DmpFocusCategory is { Length: > 0 } f ? categories.GroupByName(f) : null;
        var sold = options.SoldStatuses;
        var productTypes = await db.LinkoProducts.AsNoTracking().Where(p => p.TypeId != null).ToDictionaryAsync(p => p.Id, p => p.TypeId!.Value, ct);
        bool IsFocus(long? product) =>
            focus is { } g && product is { } p && productTypes.TryGetValue(p, out var type) && categories.GroupOf(type) == g;

        var createdFrom = since.AddDays(-31); // дата реализации позже создания — заказ, созданный раньше окна, может попасть в окно
        var orderLines = await (
                from l in db.LinkoOrderLines
                join o in db.LinkoOrders on l.OrderId equals o.Id
                where o.MarketId != null && marketIds.Contains(o.MarketId.Value) && sold.Contains(o.Status) && o.CreatedDate >= createdFrom
                      && (l.ProductId == null || !shelfIds.Contains(l.ProductId.Value))
                select new { o.Id, o.CreatedDate, o.DeliveryDate, o.AcceptedDate, MarketId = o.MarketId!.Value, l.ProductId, l.TotalWeight })
            .ToListAsync(ct);
        var returnLines = await (
                from l in db.LinkoOrderReturnLines
                join r in db.LinkoOrderReturns on l.ReturnId equals r.Id
                where r.MarketId != null && marketIds.Contains(r.MarketId.Value) && returned.Contains(r.Status) && r.CreatedDate >= since
                      && (l.ProductId == null || !shelfIds.Contains(l.ProductId.Value))
                select new { r.Id, r.CreatedDate, MarketId = r.MarketId!.Value, l.ProductId, l.TotalWeight })
            .ToListAsync(ct);
        var sales = orderLines
            .Select(o => (Line: o, Date: SaleDate(o.CreatedDate, o.DeliveryDate, o.AcceptedDate)))
            .Where(x => x.Date is { } d && d >= since)
            .GroupBy(x => (x.Line.MarketId, x.Line.Id, Date: x.Date!.Value))
            .Select(g => new DmpSale(g.Key.MarketId, g.Key.Id, false, g.Key.Date, g.Sum(x => x.Line.TotalWeight),
                g.Where(x => IsFocus(x.Line.ProductId)).Sum(x => x.Line.TotalWeight)))
            .Concat(returnLines
                .GroupBy(r => (r.MarketId, r.Id, r.CreatedDate))
                .Select(g => new DmpSale(g.Key.MarketId, g.Key.Id, true, g.Key.CreatedDate, -g.Sum(x => x.TotalWeight),
                    -g.Where(x => IsFocus(x.ProductId)).Sum(x => x.TotalWeight))))
            .ToList();

        var markets = await db.LinkoMarkets.AsNoTracking().Where(m => marketIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Name, ct);
        var agentIds = shelves.Select(s => s.AgentId).OfType<long>().Distinct().ToList();
        var agents = (await db.LinkoUsers.AsNoTracking().Where(u => agentIds.Contains(u.Id)).ToListAsync(ct))
            .ToDictionary(u => u.Id, u => u.DisplayName.Length > 0 ? u.DisplayName : u.Username ?? $"ТП {u.Id}");

        return DmpCompute.Build(asOf, shelfProducts.Select(p => p.Name).Order(StringComparer.CurrentCulture).ToList(),
            focus is null ? null : options.DmpFocusCategory, receipts, balances, shelves, sales, markets, agents);

        bool IsExport(long? id) => id is { } s && stocks.TryGetValue(s, out var name) && options.IsExportStock(name);
    }

    /// <summary>Дата реализации заказа — как во вторичке (Sales:DateField); без неё (приёмки ещё нет) — null.</summary>
    private DateOnly? SaleDate(DateOnly created, DateOnly? delivery, DateOnly? accepted) => options.DateField switch
    {
        SaleDateField.Accepted => accepted,
        SaleDateField.Delivery => delivery ?? created,
        _ => created,
    };
}
