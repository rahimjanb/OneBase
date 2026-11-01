using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Field;
using OneBase.Application.Work;
using OneBase.Domain.Procurement;
using OneBase.Domain.Work;

namespace OneBase.Application.Procurement;

/// <summary>Раздел сводки: название, сколько всего и до десяти строк со ссылками.</summary>
public sealed record ProcurementDigestSection(string Title, int Count, IReadOnlyList<ProcurementDigestItem> Items);

public sealed record ProcurementDigestItem(string Text, string? Link);

public sealed partial class ProcurementReminderService
{
    private const int DigestItems = 10;
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>
    /// Ежедневная сводка (первый проход после 09:00, одна на день): что ждёт решения директора, новые заявки и закупки за вчера, просроченные
    /// поставки и поставки сегодня, принятое за вчера, открытые претензии, остатки ниже минимума, просроченные задачи снабжения. Сохраняется
    /// снимком в журнал сводок; директору и руководителю снабжения — уведомление со ссылкой.
    /// </summary>
    public async Task<bool> DigestAsync(DateOnly today, CancellationToken ct)
    {
        if (await db.ProcurementDigests.AnyAsync(d => d.Day == today, ct))
        {
            return false;
        }

        var sections = await DigestSectionsAsync(today, ct);
        int CountOf(int index) => sections[index].Count;
        var summary = $"Ждут директора: {CountOf(0)} · новых заявок: {CountOf(1)} · просрочено поставок: {CountOf(3)} · претензий: {CountOf(6)} · ниже минимума: {CountOf(7)} · просрочено задач: {CountOf(8)}";
        var leaders = await ProcurementPeople.LeadersAsync(db, ct);
        var digest = new ProcurementDigest
        {
            Day = today,
            Title = $"Сводка снабжения за {today:dd.MM.yyyy}",
            Summary = summary,
            Sections = JsonSerializer.Serialize(sections, Json),
            Recipients = leaders.Count,
        };
        db.ProcurementDigests.Add(digest);
        foreach (var user in leaders)
        {
            notifier.Notify(user, UserNotificationKind.ProcurementStatus, digest.Title, summary, $"/procurement/digests/{digest.Id}");
        }

        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException e) when (e.InnerException is System.Data.Common.DbException { SqlState: "23505" })
        {
            // Сводку за этот день уже сохранил другой экземпляр сервера — второй не нужен.
            db.ChangeTracker.Clear();
            return false;
        }
    }

    private async Task<List<ProcurementDigestSection>> DigestSectionsAsync(DateOnly today, CancellationToken ct)
    {
        var yesterday = today.AddDays(-1);
        var (yFrom, yTo) = (FieldClock.DayStartUtc(yesterday), FieldClock.DayStartUtc(today));
        var live = ProcurementOrderRules.Live;
        var receivable = ProcurementOrderRules.Receivable;
        var placed = ProcurementOrderRules.Placed;

        ProcurementDigestSection Section(string title, int count, IEnumerable<ProcurementDigestItem> items) => new(title, count, items.Take(DigestItems).ToList());

        var awaitingRequests = await db.ProcurementRequests.AsNoTracking().Where(r => r.Status == ProcurementRequestStatus.DirectorApproval).OrderBy(r => r.ForwardedAt)
            .Select(r => new { r.Id, r.Number, r.Title, r.PurchaseTotal }).ToListAsync(ct);
        var awaitingOrders = await db.ProcurementPurchaseOrders.AsNoTracking().Where(o => o.Status == ProcurementOrderStatus.AwaitingApproval).OrderBy(o => o.UpdatedAt)
            .Select(o => new { o.Id, o.Number, Supplier = o.Supplier!.Name, o.Total }).ToListAsync(ct);
        var awaiting = Section("Ждут решения директора", awaitingRequests.Count + awaitingOrders.Count,
            awaitingRequests.Select(r => new ProcurementDigestItem($"Заявка {r.Number} — {r.Title}, к закупке ≈ {ProcurementRules.Money(r.PurchaseTotal)} сум", $"/requests/{r.Id}"))
                .Concat(awaitingOrders.Select(o => new ProcurementDigestItem($"Заказ {o.Number} — {o.Supplier}, {ProcurementRules.Money(o.Total)} сум", $"/procurement/orders/{o.Id}"))));

        var submitted = await db.ProcurementRequests.AsNoTracking().Where(r => r.SubmittedAt >= yFrom && r.SubmittedAt < yTo).OrderBy(r => r.SubmittedAt)
            .Select(r => new { r.Id, r.Number, r.Title, Department = r.Department!.Name, r.EstimatedTotal }).ToListAsync(ct);
        var newRequests = Section("Новые заявки за вчера", submitted.Count,
            submitted.Select(r => new ProcurementDigestItem($"{r.Number} — {r.Title} ({r.Department}), ≈ {ProcurementRules.Money(r.EstimatedTotal)} сум", $"/requests/{r.Id}")));

        var bought = await db.ProcurementPurchaseOrders.AsNoTracking().Where(o => placed.Contains(o.Status) && o.ApprovedAt >= yFrom && o.ApprovedAt < yTo)
            .OrderByDescending(o => o.Total).Select(o => new { o.Id, o.Number, Supplier = o.Supplier!.Name, o.Total }).ToListAsync(ct);
        var purchases = Section($"Закупки за вчера: {ProcurementRules.Money(bought.Sum(o => o.Total))} сум", bought.Count,
            bought.Select(o => new ProcurementDigestItem($"{o.Number} — {o.Supplier}, {ProcurementRules.Money(o.Total)} сум", $"/procurement/orders/{o.Id}")));

        var overdueOrders = await db.ProcurementPurchaseOrders.AsNoTracking().Where(o => live.Contains(o.Status) && o.ExpectedDate != null && o.ExpectedDate < today)
            .OrderBy(o => o.ExpectedDate).Select(o => new { o.Id, o.Number, Supplier = o.Supplier!.Name, o.ExpectedDate, Responsible = o.Responsible!.FullName }).ToListAsync(ct);
        var overdue = Section("Просроченные поставки", overdueOrders.Count,
            overdueOrders.Select(o => new ProcurementDigestItem($"{o.Number} — {o.Supplier}, ожидалась {o.ExpectedDate:dd.MM.yyyy} ({o.Responsible})", $"/procurement/orders/{o.Id}")));

        var dueToday = await db.ProcurementPurchaseOrders.AsNoTracking().Where(o => receivable.Contains(o.Status) && o.ExpectedDate == today).OrderBy(o => o.Number)
            .Select(o => new { o.Id, o.Number, Supplier = o.Supplier!.Name }).ToListAsync(ct);
        var expected = Section("Поставки сегодня", dueToday.Count,
            dueToday.Select(o => new ProcurementDigestItem($"{o.Number} — {o.Supplier}", $"/procurement/orders/{o.Id}")));

        var posted = await db.ProcurementStockDocuments.AsNoTracking()
            .Where(d => d.OrderId != null && d.Status == ProcurementStockDocumentStatus.Posted && d.PostedAt >= yFrom && d.PostedAt < yTo).OrderBy(d => d.PostedAt)
            .Select(d => new { d.Id, d.Number, Order = d.Order!.Number, Supplier = d.Order.Supplier!.Name, Problems = d.ReceiptLines.Any(l => l.Defective > 0 || l.Shortage > 0) })
            .ToListAsync(ct);
        var received = Section("Принято на склад за вчера", posted.Count,
            posted.Select(d => new ProcurementDigestItem($"Приёмка {d.Number} по заказу {d.Order} — {d.Supplier}{(d.Problems ? ", есть брак или недостача" : "")}", $"/procurement/receipts/{d.Id}")));

        var claimRows = await db.ProcurementReceiptLines.AsNoTracking().Where(l => l.ClaimStatus == ProcurementClaimStatus.Open).OrderBy(l => l.Document!.PostedAt)
            .Select(l => new { l.DocumentId, Supplier = l.Document!.Order!.Supplier!.Name, l.OrderItem!.Name, l.OrderItem.UnitCode, l.Defective, l.Shortage }).ToListAsync(ct);
        var claims = Section("Открытые претензии", claimRows.Count,
            claimRows.Select(c => new ProcurementDigestItem(
                $"{c.Supplier}: {c.Name} — {string.Join(", ", new[] { c.Defective > 0 ? $"брак {ProcurementRules.Quantity(c.Defective)} {c.UnitCode}" : null, c.Shortage > 0 ? $"недостача {ProcurementRules.Quantity(c.Shortage)} {c.UnitCode}" : null }.OfType<string>())}",
                $"/procurement/receipts/{c.DocumentId}")));

        var lowItems = await db.ProcurementItems.AsNoTracking().Where(i => i.IsActive && i.MinQty > 0)
            .Select(i => new { i.Name, i.UnitCode, i.MinQty, Available = db.ProcurementStockBalances.Where(b => b.ItemId == i.Id).Sum(b => (decimal?)(b.OnHand - b.Reserved)) ?? 0m })
            .Where(i => i.Available < i.MinQty).OrderBy(i => i.Name).ToListAsync(ct);
        var low = Section("Ниже минимального остатка", lowItems.Count,
            lowItems.Select(i => new ProcurementDigestItem($"{i.Name} — {ProcurementRules.Quantity(i.Available)} {i.UnitCode} (минимум {ProcurementRules.Quantity(i.MinQty)})", "/procurement/stock?filter=low")));

        var open = WorkTaskService.Open;
        var supplyTasks = await db.WorkTasks.AsNoTracking()
            .Where(t => t.Department!.Code == ProcurementPeople.SupplyDepartment && t.ArchivedAt == null && open.Contains(t.Status) && t.DueDate != null && t.DueDate < today)
            .OrderBy(t => t.DueDate).Select(t => new { t.Id, t.Title, t.DueDate, Assignee = t.Assignee == null ? null : t.Assignee.FullName }).ToListAsync(ct);
        var tasks = Section("Просроченные задачи снабжения", supplyTasks.Count,
            supplyTasks.Select(t => new ProcurementDigestItem($"{t.Title} — {t.Assignee ?? "без исполнителя"}, срок {t.DueDate:dd.MM.yyyy}", $"/tasks/task/{t.Id}")));

        return [awaiting, newRequests, purchases, overdue, expected, received, claims, low, tasks];
    }

    /// <summary>
    /// Эскалация просроченных задач отдела «Снабжение»: просрочка не меньше порога руководителя — руководителю снабжения, не меньше порога
    /// директора — директору. Каждый уровень — один раз на срок задачи (отметка в базе); исполнителю не дублируется — ему напоминает «Задачи».
    /// </summary>
    public async Task<int> EscalateAsync(DateOnly today, CancellationToken ct)
    {
        var settings = await db.ProcurementSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var headDays = settings?.EscalateHeadDays ?? 1;
        var directorDays = settings?.EscalateDirectorDays ?? 3;
        var latest = today.AddDays(-headDays);
        var open = WorkTaskService.Open;
        var tasks = await db.WorkTasks.AsNoTracking()
            .Where(t => t.Department!.Code == ProcurementPeople.SupplyDepartment && t.ArchivedAt == null && open.Contains(t.Status) && t.DueDate != null && t.DueDate <= latest)
            .Select(t => new { t.Id, t.Title, DueDate = t.DueDate!.Value, t.AssigneeId, Assignee = t.Assignee == null ? null : t.Assignee.FullName })
            .ToListAsync(ct);
        if (tasks.Count == 0)
        {
            return 0;
        }

        var ids = tasks.Select(t => t.Id).ToList();
        var done = (await db.ProcurementEscalations.AsNoTracking().Where(e => ids.Contains(e.TaskId)).Select(e => new { e.TaskId, e.Level, e.DueDate }).ToListAsync(ct))
            .Select(e => (e.TaskId, e.Level, e.DueDate)).ToHashSet();
        var heads = await ProcurementPeople.SupplyHeadsAsync(db, ct);
        var directors = await ProcurementPeople.DirectorsAsync(db, ct);
        var sent = 0;
        foreach (var t in tasks)
        {
            var days = today.DayNumber - t.DueDate.DayNumber;
            foreach (var (level, threshold, recipients, who) in new[] { (1, headDays, heads, "руководителю снабжения"), (2, directorDays, directors, "директору") })
            {
                if (days < threshold || done.Contains((t.Id, level, t.DueDate)))
                {
                    continue;
                }

                db.ProcurementEscalations.Add(new ProcurementEscalation { TaskId = t.Id, Level = level, DueDate = t.DueDate });
                db.WorkTaskEvents.Add(new WorkTaskEvent { TaskId = t.Id, Kind = "escalated", Text = $"Эскалация {who}: просрочено {days} дн." });
                foreach (var user in recipients.Where(u => u != t.AssigneeId))
                {
                    notifier.Notify(user, UserNotificationKind.TaskOverdue, $"Просрочена задача снабжения: {t.Title}",
                        $"{t.Assignee ?? "Без исполнителя"} · срок {t.DueDate:dd.MM.yyyy}, просрочено {days} дн.", $"/tasks/task/{t.Id}");
                }

                sent++;
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is System.Data.Common.DbException { SqlState: "23505" })
        {
            // Ту же эскалацию успел записать другой экземпляр сервера.
            db.ChangeTracker.Clear();
            return 0;
        }

        return sent;
    }

    /// <summary>Сводка из журнала (для страницы сводки): разделы — снимок на день формирования.</summary>
    public static IReadOnlyList<ProcurementDigestSection> Sections(ProcurementDigest digest) =>
        JsonSerializer.Deserialize<List<ProcurementDigestSection>>(digest.Sections, Json) ?? [];
}
