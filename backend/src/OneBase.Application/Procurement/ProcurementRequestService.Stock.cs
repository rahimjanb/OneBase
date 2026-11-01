using Microsoft.EntityFrameworkCore;
using OneBase.Application.Field;
using OneBase.Domain.Procurement;
using OneBase.Domain.Work;

namespace OneBase.Application.Procurement;

public sealed record ProcurementIssueLineInput(Guid RequestItemId, decimal Quantity);

/// <summary>Выдача по заявке: какие позиции и сколько (из резерва), кто получил, комментарий, ключ повтора формы.</summary>
public sealed record ProcurementRequestIssueInput(IReadOnlyList<ProcurementIssueLineInput>? Lines, string? Receiver, string? Comment, Guid? ClientKey = null);

/// <summary>
/// Настройки снабжения для экрана: допуск роста цены или суммы без повторного согласования, %; через сколько дней просрочки задача
/// снабжения уходит руководителю снабжения и директору.
/// </summary>
public sealed record ProcurementSettingsView(decimal ReapprovalTolerancePercent, int EscalateHeadDays = 1, int EscalateDirectorDays = 3);

public sealed partial class ProcurementRequestService
{
    /// <summary>
    /// Выдача со склада по заявке (кладовщик): из резерва позиции — остаток и резерв уменьшаются вместе, строка журнала, документ ISS-…
    /// Утверждённая заявка, у которой выдано всё, становится выполненной, частично — частично выполненной. Повтор формы с тем же ключом
    /// не выдаёт второй раз.
    /// </summary>
    public Task<Guid> IssueAsync(ProcurementContext ctx, Guid requestId, ProcurementRequestIssueInput input, CancellationToken ct) => ledger.RetryAsync(async () =>
    {
        RequireOpen(ctx);
        if (!ctx.Warehouse)
        {
            throw new FieldForbiddenException("Выдаёт товар со склада кладовщик.");
        }

        if (input.ClientKey is { } key)
        {
            var done = await db.ProcurementStockDocuments.AsNoTracking().Where(d => d.ActorId == ctx.UserId && d.ClientKey == key).Select(d => d.Id).FirstOrDefaultAsync(ct);
            if (done != Guid.Empty)
            {
                return done;
            }
        }

        var r = await RequireTrackedAsync(ctx, requestId, ct);
        if (!ProcurementRules.Issuable(r.Status))
        {
            throw new FieldConflictException($"Заявка «{ProcurementRules.Label(r.Status)}» — выдача по ней недоступна.");
        }

        var lines = (input.Lines ?? []).Where(l => l.Quantity != 0).ToList();
        if (lines.Count == 0)
        {
            throw new FieldValidationException("Укажите, что и сколько выдать.");
        }

        var reservations = await db.ProcurementStockReservations
            .Where(x => x.RequestId == r.Id && x.Status == ProcurementReservationStatus.Active).ToListAsync(ct);
        if (reservations.Count == 0)
        {
            throw new FieldConflictException("По заявке нет резерва на складе — выдавать нечего.");
        }

        var warehouse = reservations[0].WarehouseId;
        var doc = await ledger.DocumentAsync(ProcurementStockDocumentKind.Issue, warehouse, ctx.UserId, ct);
        doc.DepartmentId = r.DepartmentId;
        doc.RequestId = r.Id;
        doc.Receiver = Text(input.Receiver, 200, "Кто получил");
        doc.Comment = Text(input.Comment, 1000, "Комментарий");
        doc.ClientKey = input.ClientKey;

        var balances = await ledger.BalancesAsync(warehouse, reservations.Select(x => x.ItemId), ct);
        var issued = new List<string>();
        foreach (var line in lines.GroupBy(l => l.RequestItemId).Select(g => (Id: g.Key, Quantity: g.Sum(l => l.Quantity))))
        {
            var item = r.Items.FirstOrDefault(i => i.Id == line.Id) ?? throw new FieldValidationException("Позиция не из этой заявки.");
            if (line.Quantity <= 0 || decimal.Round(line.Quantity, 3) != line.Quantity)
            {
                throw new FieldValidationException($"«{item.Name}»: количество — больше нуля, не больше трёх знаков после запятой.");
            }

            var reservation = reservations.FirstOrDefault(x => x.RequestItemId == item.Id)
                              ?? throw new FieldConflictException($"«{item.Name}»: резерва на складе нет — выдавать нечего.");
            var left = reservation.Quantity - reservation.Issued;
            if (line.Quantity > left)
            {
                throw new FieldConflictException($"«{item.Name}»: в резерве осталось {ProcurementRules.Quantity(left)} {item.UnitCode} — больше не выдать.");
            }

            ledger.Move(doc, balances[reservation.ItemId], ProcurementMovementKind.Issue, -line.Quantity, item.Name, requestItemId: item.Id, fromReserve: true);
            reservation.Issued += line.Quantity;
            if (reservation.Issued >= reservation.Quantity)
            {
                reservation.Status = ProcurementReservationStatus.Issued;
                reservation.ClosedAt = Now;
            }

            item.IssuedQty += line.Quantity;
            item.UpdatedAt = Now;
            issued.Add($"{item.Name} — {ProcurementRules.Quantity(line.Quantity)} {item.UnitCode}");
        }

        var from = r.Status;
        Refresh(r);
        if (ProcurementRules.ApprovedEditable(r.Status))
        {
            var active = r.Items.Where(i => i.Status != ProcurementItemStatus.Cancelled).ToList();
            r.Status = active.All(i => i.IssuedQty >= i.Quantity) ? ProcurementRequestStatus.Fulfilled : ProcurementRequestStatus.PartiallyFulfilled;
        }

        r.UpdatedAt = Now;
        var changed = from != r.Status;
        Log(r, ctx.UserId, "issued", changed ? from : null, changed ? r.Status : null,
            $"Выдано со склада ({doc.Number}): {string.Join(", ", issued)}{(doc.Receiver is { } who ? $"; получил: {who}" : "")}", doc.Comment, null);
        notifier.Notify(r.InitiatorId == ctx.UserId ? null : r.InitiatorId, UserNotificationKind.ProcurementStatus,
            r.Status == ProcurementRequestStatus.Fulfilled ? $"Заявка {r.Number} выполнена: всё выдано со склада" : $"Выдано со склада по заявке {r.Number}",
            string.Join(", ", issued), $"/requests/{r.Id}");
        await db.SaveChangesAsync(ct);
        return doc.Id;
    });

    // ---------- Настройки ----------

    public async Task<ProcurementSettingsView> SettingsAsync(ProcurementContext ctx, CancellationToken ct)
    {
        RequireOpen(ctx);
        var settings = await db.ProcurementSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        return new ProcurementSettingsView(settings?.ReapprovalTolerancePercent ?? 0m, settings?.EscalateHeadDays ?? 1, settings?.EscalateDirectorDays ?? 3);
    }

    /// <summary>Допуск роста цены или суммы без повторного согласования: 0–100 %, до двух знаков. Настраивают директор и администратор.</summary>
    public async Task UpdateSettingsAsync(ProcurementContext ctx, ProcurementSettingsView input, CancellationToken ct)
    {
        RequireConfigures(ctx);
        var tolerance = input.ReapprovalTolerancePercent;
        if (tolerance < 0 || tolerance > 100 || decimal.Round(tolerance, 2) != tolerance)
        {
            throw new FieldValidationException("Допуск — от 0 до 100 %, не больше двух знаков после запятой.");
        }

        if (input.EscalateHeadDays is < 1 or > 30)
        {
            throw new FieldValidationException("Руководителю снабжения — через 1–30 дней просрочки.");
        }

        if (input.EscalateDirectorDays < input.EscalateHeadDays || input.EscalateDirectorDays > 60)
        {
            throw new FieldValidationException("Директору — не раньше, чем руководителю снабжения, и не позже 60 дней просрочки.");
        }

        var settings = await db.ProcurementSettings.FirstOrDefaultAsync(ct);
        if (settings is null)
        {
            settings = new ProcurementSettings();
            db.ProcurementSettings.Add(settings);
        }

        settings.ReapprovalTolerancePercent = tolerance;
        settings.EscalateHeadDays = input.EscalateHeadDays;
        settings.EscalateDirectorDays = input.EscalateDirectorDays;
        settings.UpdatedAt = Now;
        settings.UpdatedById = ctx.UserId;
        await db.SaveChangesAsync(ct);
    }
}
