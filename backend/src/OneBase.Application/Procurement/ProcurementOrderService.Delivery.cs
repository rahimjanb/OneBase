using Microsoft.EntityFrameworkCore;
using OneBase.Application.Field;
using OneBase.Application.Security;
using OneBase.Domain.Procurement;
using OneBase.Domain.Work;

namespace OneBase.Application.Procurement;

public sealed partial class ProcurementOrderService
{
    /// <summary>
    /// Закрыть недопоставленный остаток (снабжение, причина обязательна): поставщик больше не довезёт. Непоставленное перестаёт занимать
    /// часть «к закупке» — его можно заказать у другого поставщика; заказ — «поставлен», задача ответственному выполнена. Инициаторам,
    /// директору, руководителю снабжения и кладовщикам — уведомления.
    /// </summary>
    public Task CloseRemainderAsync(ProcurementContext ctx, Guid id, string? reason, CancellationToken ct) => ledger.RetryAsync(async () =>
    {
        RequireManage(ctx);
        var order = await TrackedAsync(id, ct);
        if (order.Status != ProcurementOrderStatus.PartiallyDelivered)
        {
            throw new FieldConflictException(order.Status == ProcurementOrderStatus.Ordered
                ? "По заказу ещё ничего не принято — если поставки не будет, отмените заказ."
                : $"Заказ «{ProcurementOrderRules.Label(order.Status)}» — закрывать нечего.");
        }

        var note = Text(reason, 2000, "Причина") ?? throw new FieldValidationException("Укажите, почему остаток не будет поставлен.");
        var closed = new List<(ProcurementPurchaseOrderItem Item, decimal Quantity)>();
        foreach (var item in order.Items)
        {
            var left = ProcurementOrderRules.Remaining(item.Quantity, item.ReceivedQty, item.ClosedQty);
            if (left > 0)
            {
                item.ClosedQty += left;
                closed.Add((item, left));
            }
        }

        var from = order.Status;
        order.Status = ProcurementOrderStatus.Delivered;
        order.DeliveredAt = Now;
        order.UpdatedAt = Now;
        await ReleaseAsync(order, ct);
        var text = string.Join(", ", closed.Select(c => $"{c.Item.Name} — {ProcurementRules.Quantity(c.Quantity)} {c.Item.UnitCode}"));
        await FinishTaskAsync(ctx, order, $"Поставка завершена с недопоставкой: {text}", ct);
        Log(ctx, order, "remainder-closed", from, order.Status, $"Остаток закрыт без поставки: {text}", note, null);

        var requests = await db.ProcurementPurchaseOrderItems.AsNoTracking().Where(i => i.OrderId == id && i.Quantity > i.ReceivedQty)
            .Select(i => new { i.RequestItem!.RequestId, i.RequestItem.Request!.Number, i.RequestItem.Request.InitiatorId }).Distinct().ToListAsync(ct);
        foreach (var r in requests)
        {
            db.ProcurementAuditLogs.Add(new ProcurementAuditLog
            {
                DocType = ProcurementRules.RequestDoc, DocId = r.RequestId, ActorId = ctx.UserId, Action = "remainder-closed",
                Text = $"По заказу {order.Number} поставка закрыта с недопоставкой — недопоставленное снова можно заказать", Comment = note,
            });
        }

        var supplier = order.Supplier!.Name;
        void Send(IEnumerable<Guid> recipients, UserNotificationKind kind, string title, string? body, string link)
        {
            foreach (var user in recipients.Distinct().Where(u => u != ctx.UserId))
            {
                notifier.Notify(user, kind, title, body, link);
            }
        }

        foreach (var r in requests)
        {
            Send([r.InitiatorId], UserNotificationKind.ProcurementStatus, $"Недопоставка по заявке {r.Number}", $"{supplier}: {note}", $"/requests/{r.RequestId}");
        }

        Send([order.ResponsibleId, .. await ProcurementPeople.LeadersAsync(db, ct)], UserNotificationKind.ProcurementStatus,
            $"Заказ {order.Number} закрыт с недопоставкой — {supplier}", $"{text}. {note}", $"/procurement/orders/{order.Id}");
        Send(await ProcurementPeople.WithPermissionAsync(db, Permissions.ProcurementWarehouse, ct), UserNotificationKind.ProcurementStatus,
            $"Поставки по заказу {order.Number} больше не ждём", text, $"/procurement/orders/{order.Id}");
        await db.SaveChangesAsync(ct);
        return true;
    });

    /// <summary>Закрыть поставленный заказ (снабжение): документы собраны, открытых претензий нет. Оплаты — с модулем финансов.</summary>
    public Task CompleteAsync(ProcurementContext ctx, Guid id, CancellationToken ct) => ledger.RetryAsync(async () =>
    {
        RequireManage(ctx);
        var order = await TrackedAsync(id, ct);
        if (order.Status == ProcurementOrderStatus.Completed)
        {
            return true;
        }

        if (order.Status != ProcurementOrderStatus.Delivered)
        {
            throw new FieldConflictException($"Заказ «{ProcurementOrderRules.Label(order.Status)}» — закрыть можно только поставленный заказ.");
        }

        if (await db.ProcurementReceiptLines.AnyAsync(l => l.Document!.OrderId == id && l.ClaimStatus == ProcurementClaimStatus.Open, ct))
        {
            throw new FieldConflictException("По заказу есть открытые претензии — сначала закройте их решением.");
        }

        var from = order.Status;
        order.Status = ProcurementOrderStatus.Completed;
        order.CompletedAt = Now;
        order.UpdatedAt = Now;
        Log(ctx, order, "completed", from, order.Status, "Заказ закрыт", null, null);
        await db.SaveChangesAsync(ct);
        return true;
    });
}
