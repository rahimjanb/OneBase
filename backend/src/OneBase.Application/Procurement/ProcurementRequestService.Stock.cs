using Microsoft.EntityFrameworkCore;
using OneBase.Application.Field;
using OneBase.Domain.Audit;
using OneBase.Domain.Procurement;
using OneBase.Domain.Work;

namespace OneBase.Application.Procurement;

public sealed record ProcurementIssueLineInput(Guid RequestItemId, decimal Quantity);

/// <summary>Выдача по заявке: какие позиции и сколько (из резерва), кто получил, комментарий, ключ повтора формы.</summary>
public sealed record ProcurementRequestIssueInput(IReadOnlyList<ProcurementIssueLineInput>? Lines, string? Receiver, string? Comment, Guid? ClientKey = null);

/// <summary>
/// Настройки снабжения для экрана («Настройки → Отделы → Снабжение»): маршрут согласования заявок — финансист, директор и лимит покупки без
/// директора (сум); допуск роста цены или суммы без повторного согласования, %; через сколько дней просрочки задача снабжения уходит
/// руководителю снабжения и директору; RawSteps — маршрут заявок «Закупка сырья»: шаги по автору (supply, analyst, finance → analyst,
/// finance, director, director_limit), null при сохранении — не менять. RawRouteEnabled и SupplyApproverId — прежний маршрут сырья (для
/// заявок, отправленных до типов заявок; новые заявки он не меняет), null — не менять. Имена согласующих — только для показа.
/// </summary>
public sealed record ProcurementSettingsView(
    decimal ReapprovalTolerancePercent,
    int EscalateHeadDays = 1,
    int EscalateDirectorDays = 3,
    Guid? FinanceApproverId = null,
    Guid? DirectorApproverId = null,
    decimal DirectorLimit = ProcurementSettings.DefaultDirectorLimit,
    string? FinanceApproverName = null,
    string? DirectorApproverName = null,
    bool? RawRouteEnabled = null,
    Guid? SupplyApproverId = null,
    string? SupplyApproverName = null,
    IReadOnlyList<string>? SupplyHeads = null,
    decimal? OverconsumptionPercent = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? RawSteps = null);

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
        var store = await ledger.WarehouseAsync(ctx, warehouse, ct); // закреплённый кладовщик выдаёт только со своих складов
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

            var balance = balances[reservation.ItemId];
            if (ledger.Tracks(reservation.ItemId))
            {
                // Позиция с партиями: из годных партий по FEFO, резерв уменьшается вместе с остатком.
                foreach (var (batch, quantity) in await ledger.PickAsync(balance, store.Name, item.Name, line.Quantity, null, usableOnly: true, keepReserve: false, ct))
                {
                    ledger.MoveBatch(doc, balance, batch, ProcurementMovementKind.Issue, -quantity, item.Name, requestItemId: item.Id, fromReserve: true);
                }
            }
            else
            {
                ledger.Move(doc, balance, ProcurementMovementKind.Issue, -line.Quantity, item.Name, requestItemId: item.Id, fromReserve: true);
            }

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
        var ids = new[] { settings?.FinanceApproverId, settings?.DirectorApproverId, settings?.SupplyApproverId }.OfType<Guid>().ToList();
        var names = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        string? Name(Guid? id) => id is { } x ? names.GetValueOrDefault(x) : null;
        // Руководители отдела «Снабжение» — кто согласует сырьё, пока слот не выбран.
        var supplyHeads = await db.WorkDepartmentHeads.AsNoTracking().Where(h => h.Department!.Code == ProcurementPeople.SupplyDepartment && h.User!.IsActive)
            .Select(h => h.User!.FullName).OrderBy(n => n).ToListAsync(ct);
        return new ProcurementSettingsView(settings?.ReapprovalTolerancePercent ?? 0m, settings?.EscalateHeadDays ?? 1, settings?.EscalateDirectorDays ?? 3,
            settings?.FinanceApproverId, settings?.DirectorApproverId, settings?.DirectorLimit ?? ProcurementSettings.DefaultDirectorLimit,
            Name(settings?.FinanceApproverId), Name(settings?.DirectorApproverId), settings?.RawRouteEnabled ?? false, settings?.SupplyApproverId,
            Name(settings?.SupplyApproverId), supplyHeads, settings?.OverconsumptionPercent ?? ProcurementSettings.DefaultOverconsumptionPercent,
            ProcurementRawRoute.Parse(settings?.RawRouteSteps));
    }

    /// <summary>Наибольший лимит покупки без директора: 1 трлн сум.</summary>
    public const decimal MaxDirectorLimit = 1_000_000_000_000m;

    /// <summary>
    /// Настройки снабжения (директор и администратор — право «Настройки снабжения»): допуск роста цены или суммы без повторного согласования
    /// 0–100 %, до двух знаков; дни эскалации; маршрут согласования — финансист и директор (действующие пользователи OneBase, можно не
    /// выбирать) и лимит покупки без директора (больше нуля, до 1 трлн сум, целые сумы); маршрут заявок «Закупка сырья» по автору
    /// (ProcurementRawRoute.Check).
    /// </summary>
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

        if (input.DirectorLimit <= 0 || input.DirectorLimit > MaxDirectorLimit || decimal.Truncate(input.DirectorLimit) != input.DirectorLimit)
        {
            throw new FieldValidationException("Лимит без директора — целая сумма больше нуля, до 1 трлн сум.");
        }

        if (input.OverconsumptionPercent is { } over && (over < 0 || over > 100 || decimal.Round(over, 2) != over))
        {
            throw new FieldValidationException("Порог перерасхода сырья — от 0 до 100 %, не больше двух знаков после запятой.");
        }

        if (input.RawSteps is { } rawSteps && ProcurementRawRoute.Check(rawSteps) is { } wrongRoute)
        {
            throw new FieldValidationException(wrongRoute);
        }

        var approvers = new[] { input.FinanceApproverId, input.DirectorApproverId, input.SupplyApproverId }.OfType<Guid>().Distinct().ToList();
        if (await db.Users.CountAsync(u => approvers.Contains(u.Id) && u.IsActive, ct) != approvers.Count)
        {
            throw new FieldValidationException("Финансист, директор и руководитель снабжения — действующие пользователи OneBase.");
        }

        var settings = await db.ProcurementSettings.FirstOrDefaultAsync(ct);
        if (settings is null)
        {
            settings = new ProcurementSettings();
            db.ProcurementSettings.Add(settings);
        }

        object Snapshot() => new
        {
            settings.ReapprovalTolerancePercent, settings.EscalateHeadDays, settings.EscalateDirectorDays, settings.FinanceApproverId, settings.DirectorApproverId,
            settings.DirectorLimit, settings.RawRouteEnabled, settings.SupplyApproverId, settings.OverconsumptionPercent, settings.RawRouteSteps,
        };
        var before = Snapshot();
        settings.ReapprovalTolerancePercent = tolerance;
        settings.EscalateHeadDays = input.EscalateHeadDays;
        settings.EscalateDirectorDays = input.EscalateDirectorDays;
        settings.FinanceApproverId = input.FinanceApproverId;
        settings.DirectorApproverId = input.DirectorApproverId;
        settings.DirectorLimit = input.DirectorLimit;
        if (input.RawRouteEnabled is { } raw)
        {
            // Маршрут сырья и его руководитель снабжения меняются вместе (прежние клиенты их не присылают — остаются как были).
            settings.RawRouteEnabled = raw;
            settings.SupplyApproverId = input.SupplyApproverId;
        }
        if (input.OverconsumptionPercent is { } threshold)
        {
            settings.OverconsumptionPercent = threshold;
        }

        if (input.RawSteps is { } route)
        {
            // Маршрут сырья по авторам: действует для заявок, отправленных после сохранения; заявки в пути идут своими шагами.
            settings.RawRouteSteps = ProcurementRawRoute.Serialize(route);
        }

        settings.UpdatedAt = Now;
        settings.UpdatedById = ctx.UserId;
        await db.SaveChangesAsync(ct);
        // Маршрут согласования, правила и эскалация — в журнал аудита: было и стало.
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "procurement.settings.updated", "procurement_settings", settings.Id.ToString(),
            new { before, after = Snapshot() }, ct);
    }
}
