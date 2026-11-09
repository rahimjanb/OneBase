using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Domain.Procurement;

namespace OneBase.Application.Production;

/// <summary>
/// Стоимость по цене партий, выданных в задание: по позиции — средняя цена выданного за вычетом возвращённого (возврат — в ту же партию, по её
/// цене). Выдано без цены партии (позиция без партий или партия без цены) — цены нет: другая цена не подставляется.
/// </summary>
internal static class ProductionCost
{
    private sealed record Issued(Guid Order, Guid ItemId, decimal Qty, decimal Priced, decimal Cost);

    private static async Task<List<Issued>> IssuedAsync(IAppDbContext db, IReadOnlyCollection<Guid> orderIds, CancellationToken ct) =>
        await db.ProcurementStockMovements.AsNoTracking()
            .Where(m => (m.Kind == ProcurementMovementKind.ProductionIssue || m.Kind == ProcurementMovementKind.ProductionReturn) && m.Document!.ProductionOrderId != null
                        && orderIds.Contains(m.Document.ProductionOrderId.Value))
            .GroupBy(m => new { Order = m.Document!.ProductionOrderId!.Value, m.ItemId })
            .Select(g => new Issued(g.Key.Order, g.Key.ItemId, -g.Sum(m => m.Quantity),
                -g.Where(m => m.BatchId != null && m.UnitCost != null).Sum(m => m.Quantity),
                -g.Where(m => m.BatchId != null && m.UnitCost != null).Sum(m => m.Quantity * m.UnitCost!.Value)))
            .ToListAsync(ct);

    /// <summary>Цена единицы склада по партиям задания: (задание, позиция) → цена; не выдано или часть выдана без цены партии — null.</summary>
    public static async Task<Dictionary<(Guid Order, Guid Item), decimal?>> BatchPricesAsync(IAppDbContext db, IReadOnlyCollection<Guid> orderIds, CancellationToken ct) =>
        (await IssuedAsync(db, orderIds, ct)).ToDictionary(r => (r.Order, r.ItemId), r => r.Qty > 0 && r.Priced == r.Qty ? r.Cost / r.Qty : (decimal?)null);

    /// <summary>Материалы задания по цене партий: всё выданное за вычетом возвращённого; ничего не выдано или выдано без цены партии — null.</summary>
    public static async Task<decimal?> MaterialsAsync(IAppDbContext db, Guid orderId, CancellationToken ct)
    {
        var issued = (await IssuedAsync(db, [orderId], ct)).Where(r => r.Qty != 0).ToList();
        return issued.Count > 0 && issued.All(r => r.Priced == r.Qty) ? issued.Sum(r => r.Cost) : null;
    }
}
