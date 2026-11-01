using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Security;
using OneBase.Application.Work;
using OneBase.Domain.Procurement;
using OneBase.Domain.Work;

namespace OneBase.Application.Procurement;

public sealed record ProcurementReminderResult(int DueSoon, int Overdue, int LowStock, bool Digest = false, int Escalated = 0);

/// <summary>
/// Напоминания снабжения — вызываются вместе с напоминаниями «Задач» (WorkTaskReminderWorker, раз в 15 минут после 09:00):
/// поставка завтра — кладовщикам (готовить приёмку); поставка просрочена — директору и руководителю снабжения (ответственному
/// напоминает его задача по заказу); остаток ниже минимума — снабжению и кладовщикам. Каждое — один раз: отметка ставится условным
/// обновлением в базе, поэтому перезапуск и частые проверки не дублируют; перенесли дату поставки — будет новое напоминание.
/// Тем же проходом — ежедневная сводка директору и руководителю снабжения (одна на день, хранится журналом) и эскалация просроченных
/// задач снабжения: руководителю снабжения, позже — директору (пороги — в настройках снабжения).
/// </summary>
public sealed partial class ProcurementReminderService(IAppDbContext db, UserNotifier notifier)
{
    public async Task<ProcurementReminderResult> RemindAsync(DateOnly today, CancellationToken ct)
    {
        var dueSoon = await DueSoonAsync(today, ct);
        var overdue = await OverdueAsync(today, ct);
        var low = await LowStockAsync(ct);
        var digest = await DigestAsync(today, ct);
        var escalated = await EscalateAsync(today, ct);
        return new ProcurementReminderResult(dueSoon, overdue, low, digest, escalated);
    }

    private async Task<int> DueSoonAsync(DateOnly today, CancellationToken ct)
    {
        var tomorrow = today.AddDays(1);
        var receivable = ProcurementOrderRules.Receivable;
        var orders = await db.ProcurementPurchaseOrders.AsNoTracking()
            .Where(o => receivable.Contains(o.Status) && o.ExpectedDate == tomorrow && (o.DueSoonNotifiedFor == null || o.DueSoonNotifiedFor != o.ExpectedDate))
            .Select(o => new { o.Id, o.Number, o.ExpectedDate, Supplier = o.Supplier!.Name, Lines = o.Items.Count(i => i.Quantity > i.ReceivedQty + i.ClosedQty) })
            .ToListAsync(ct);
        if (orders.Count == 0)
        {
            return 0;
        }

        var warehouse = await ProcurementPeople.WithPermissionAsync(db, Permissions.ProcurementWarehouse, ct);
        var sent = 0;
        foreach (var o in orders)
        {
            var claimed = await db.ProcurementPurchaseOrders
                .Where(x => x.Id == o.Id && x.ExpectedDate == o.ExpectedDate && (x.DueSoonNotifiedFor == null || x.DueSoonNotifiedFor != o.ExpectedDate))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.DueSoonNotifiedFor, o.ExpectedDate), ct);
            if (claimed == 0)
            {
                continue;
            }

            foreach (var user in warehouse)
            {
                notifier.Notify(user, UserNotificationKind.ProcurementStatus, $"Завтра поставка по заказу {o.Number} — {o.Supplier}",
                    $"Ждут приёмки: {ProcurementRequestService.Positions(o.Lines)}", "/procurement/receipts");
            }

            sent++;
        }

        await db.SaveChangesAsync(ct);
        return sent;
    }

    private async Task<int> OverdueAsync(DateOnly today, CancellationToken ct)
    {
        var live = ProcurementOrderRules.Live;
        var orders = await db.ProcurementPurchaseOrders.AsNoTracking()
            .Where(o => live.Contains(o.Status) && o.ExpectedDate != null && o.ExpectedDate < today
                        && (o.OverdueNotifiedFor == null || o.OverdueNotifiedFor != o.ExpectedDate))
            .Select(o => new { o.Id, o.Number, o.ExpectedDate, o.ResponsibleId, Supplier = o.Supplier!.Name, Responsible = o.Responsible!.FullName })
            .ToListAsync(ct);
        if (orders.Count == 0)
        {
            return 0;
        }

        var leaders = await ProcurementPeople.LeadersAsync(db, ct);
        var sent = 0;
        foreach (var o in orders)
        {
            var claimed = await db.ProcurementPurchaseOrders
                .Where(x => x.Id == o.Id && x.ExpectedDate == o.ExpectedDate && (x.OverdueNotifiedFor == null || x.OverdueNotifiedFor != o.ExpectedDate))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.OverdueNotifiedFor, o.ExpectedDate), ct);
            if (claimed == 0)
            {
                continue;
            }

            foreach (var user in leaders.Where(u => u != o.ResponsibleId))
            {
                notifier.Notify(user, UserNotificationKind.ProcurementRequest, $"Поставка просрочена: {o.Number} — {o.Supplier}",
                    $"Ожидалась {o.ExpectedDate:dd.MM.yyyy} · ответственный {o.Responsible}", $"/procurement/orders/{o.Id}");
            }

            db.ProcurementAuditLogs.Add(new ProcurementAuditLog
            {
                DocType = ProcurementOrderRules.OrderDoc, DocId = o.Id, Action = "overdue", Text = $"Поставка просрочена: ожидалась {o.ExpectedDate:dd.MM.yyyy}",
            });
            sent++;
        }

        await db.SaveChangesAsync(ct);
        return sent;
    }

    /// <summary>
    /// Остаток ниже минимума: доступно (на складе − в резерве) меньше минимального — одно уведомление на всё, что опустилось с прошлой
    /// проверки; поднялось обратно — отметка снимается (опустится снова — будет новое уведомление).
    /// </summary>
    private async Task<int> LowStockAsync(CancellationToken ct)
    {
        var items = await db.ProcurementItems.AsNoTracking().Where(i => i.IsActive && i.MinQty > 0)
            .Select(i => new
            {
                i.Id, i.Name, i.UnitCode, i.MinQty, i.LowStockNotifiedAt,
                Available = db.ProcurementStockBalances.Where(b => b.ItemId == i.Id).Sum(b => (decimal?)(b.OnHand - b.Reserved)) ?? 0m,
            })
            .ToListAsync(ct);
        var recovered = items.Where(i => i.LowStockNotifiedAt != null && i.Available >= i.MinQty).Select(i => i.Id).ToList();
        if (recovered.Count > 0)
        {
            await db.ProcurementItems.Where(i => recovered.Contains(i.Id)).ExecuteUpdateAsync(s => s.SetProperty(i => i.LowStockNotifiedAt, (DateTimeOffset?)null), ct);
        }

        var low = new List<string>();
        foreach (var item in items.Where(i => i.LowStockNotifiedAt == null && i.Available < i.MinQty).OrderBy(i => i.Name))
        {
            var now = DateTimeOffset.UtcNow;
            var claimed = await db.ProcurementItems.Where(i => i.Id == item.Id && i.LowStockNotifiedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.LowStockNotifiedAt, now), ct);
            if (claimed > 0)
            {
                low.Add($"{item.Name} — {ProcurementRules.Quantity(item.Available)} {item.UnitCode} (минимум {ProcurementRules.Quantity(item.MinQty)})");
            }
        }

        if (low.Count == 0)
        {
            return 0;
        }

        var recipients = (await ProcurementPeople.WithPermissionAsync(db, Permissions.ProcurementManage, ct))
            .Concat(await ProcurementPeople.WithPermissionAsync(db, Permissions.ProcurementWarehouse, ct)).Distinct().ToList();
        var title = low.Count == 1 ? $"Остаток ниже минимума: {low[0]}" : $"Ниже минимального остатка: {ProcurementRequestService.Positions(low.Count)}";
        var body = low.Count == 1 ? null : string.Join("; ", low.Take(8)) + (low.Count > 8 ? $" и ещё {low.Count - 8}" : "");
        foreach (var user in recipients)
        {
            notifier.Notify(user, UserNotificationKind.ProcurementStatus, title, body, "/procurement/stock?filter=low");
        }

        await db.SaveChangesAsync(ct);
        return low.Count;
    }
}
