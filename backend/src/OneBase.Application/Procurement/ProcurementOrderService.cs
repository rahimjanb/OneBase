using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Security;
using OneBase.Application.Work;
using OneBase.Domain.Procurement;
using OneBase.Domain.Work;

namespace OneBase.Application.Procurement;

public sealed record ProcurementOrderLineInput(Guid RequestItemId, decimal Quantity, decimal UnitPrice);

/// <summary>
/// Заказ из формы. Lines — позиции утверждённых заявок (количество не больше незаказанной части «к закупке»). Submit — сразу оформить
/// (в рамках утверждённой заявки — утверждается сам, иначе — к директору). ClientKey — ключ повтора формы; Version — версия при правке.
/// </summary>
public sealed record ProcurementOrderInput(
    Guid SupplierId,
    Guid? OfferId,
    Guid? ResponsibleId,
    DateOnly? ExpectedDate,
    string? PaymentTerms,
    decimal Discount,
    decimal VatRate,
    bool PricesIncludeVat,
    decimal DeliveryCost,
    string? Comment,
    IReadOnlyList<ProcurementOrderLineInput>? Lines,
    bool Submit = false,
    Guid? ClientKey = null,
    uint? Version = null);

public sealed record ProcurementOrderRow(
    Guid Id,
    string Number,
    ProcurementOrderStatus Status,
    Guid SupplierId,
    string SupplierName,
    Guid ResponsibleId,
    string ResponsibleName,
    int Lines,
    string? FirstItem,
    IReadOnlyList<string> Requests,
    decimal Total,
    DateOnly? OrderDate,
    DateOnly? ExpectedDate,
    bool Overdue,
    DateTimeOffset CreatedAt,
    int ApprovalRound);

/// <summary>
/// Заказы: Status — группа (open, awaiting, overdue, expected — ожидаются в ближайшие 7 дней, partial, done, cancelled) или статусы через
/// запятую; поставщик, ответственный (фильтр по сотрудникам), поиск (номер, поставщик, позиция, номер заявки), дата создания.
/// </summary>
public sealed record ProcurementOrderSearch(
    string? Status = null,
    Guid? SupplierId = null,
    Guid? ResponsibleId = null,
    string? Search = null,
    DateOnly? From = null,
    DateOnly? To = null,
    string? Sort = null,
    bool Desc = false,
    int Page = 1,
    int PageSize = 25);

public sealed record ProcurementOrderSummary(int Open, int Draft, int AwaitingApproval, int Overdue, int ExpectedWeek, int Partial, int CancelledMonth, decimal OpenTotal);

public sealed record ProcurementOrderItemView(
    Guid Id,
    int LineNo,
    Guid RequestItemId,
    Guid RequestId,
    string RequestNumber,
    string Name,
    string UnitCode,
    decimal Quantity,
    decimal UnitPrice,
    decimal Sum,
    decimal ReceivedQty,
    decimal? EstimatedPrice,
    decimal ClosedQty = 0);

public sealed record ProcurementOrderApprovalView(Guid Id, int Round, ProcurementApprovalDecision Decision, string? Comment, string DeciderName, decimal Total, DateTimeOffset CreatedAt);

/// <summary>Кнопки карточки заказа (сервер проверяет то же самое при каждом действии).</summary>
public sealed record ProcurementOrderAbilities(
    bool Edit,
    bool Submit,
    bool Approve,
    bool Return,
    bool MarkOrdered,
    bool Cancel,
    bool Attach,
    bool Receive = false,
    bool CloseRemainder = false,
    bool Complete = false,
    bool ResolveClaims = false);

/// <summary>Приёмка по заказу (в карточке заказа): черновик или подтверждена, кто, есть ли брак или недостача.</summary>
public sealed record ProcurementOrderReceiptRef(Guid Id, string Number, ProcurementStockDocumentStatus Status, string? Reference, string? ActorName, DateTimeOffset CreatedAt,
    DateTimeOffset? PostedAt, bool Problems);

public sealed record ProcurementOrderDetail(
    Guid Id,
    string Number,
    ProcurementOrderStatus Status,
    Guid SupplierId,
    string SupplierName,
    string? SupplierPhone,
    Guid? OfferId,
    Guid ResponsibleId,
    string ResponsibleName,
    DateOnly? OrderDate,
    DateOnly? ExpectedDate,
    bool Overdue,
    string? PaymentTerms,
    decimal Discount,
    decimal VatRate,
    bool PricesIncludeVat,
    decimal DeliveryCost,
    decimal Subtotal,
    decimal VatAmount,
    decimal Total,
    string? Comment,
    int ApprovalRound,
    DateTimeOffset? ApprovedAt,
    string? ApprovedByName,
    DateTimeOffset CreatedAt,
    string? CreatedByName,
    uint Version,
    Guid? TaskId,
    IReadOnlyList<ProcurementOrderItemView> Items,
    IReadOnlyList<ProcurementOrderApprovalView> Approvals,
    IReadOnlyList<ProcurementDocumentView> Documents,
    IReadOnlyList<ProcurementEventView> History,
    ProcurementOrderAbilities Can,
    string? ApprovalReasons,
    IReadOnlyList<ProcurementOrderReceiptRef>? Receipts = null,
    IReadOnlyList<ProcurementClaimView>? Claims = null,
    DateTimeOffset? DeliveredAt = null,
    DateTimeOffset? CompletedAt = null);

/// <summary>Утверждённая потребность для нового заказа: позиция заявки «к закупке», сколько уже заказано и сколько осталось.</summary>
public sealed record ProcurementNeedRow(
    Guid RequestItemId,
    Guid RequestId,
    string RequestNumber,
    string RequestTitle,
    string DepartmentName,
    string Name,
    string UnitCode,
    decimal ToPurchase,
    decimal Ordered,
    decimal Remaining,
    decimal? EstimatedPrice,
    string CategoryName);

public sealed record ProcurementUserOption(Guid Id, string Name);

/// <summary>
/// Заказы поставщикам и коммерческие предложения. Заказ создаётся только из утверждённой директором потребности (позиции «к закупке»
/// утверждённых заявок, не больше незаказанного остатка): одна заявка — несколько заказов, один заказ — позиции нескольких заявок.
/// Черновик не утверждён: оформление сверяет заказ с утверждённой заявкой — дороже допуска или без цены решает директор (роль), иначе
/// утверждается сам. Правка утверждённого (цена, количество, поставщик) — повторное согласование. Ответственному — задача в «Задачах».
/// Видят снабжение, директор и кладовщик; ведёт снабжение.
/// </summary>
public sealed partial class ProcurementOrderService(
    IAppDbContext db,
    IProcurementNumbers numbers,
    UserNotifier notifier,
    ProcurementStockLedger ledger,
    ProcurementDocumentService documents)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>Заявки, по которым можно собирать предложения: отправленные и дальше, до выполнения.</summary>
    internal static readonly ProcurementRequestStatus[] Offerable =
    [
        ProcurementRequestStatus.Submitted, ProcurementRequestStatus.HeadApproval, ProcurementRequestStatus.FinanceApproval, ProcurementRequestStatus.DirectorApproval,
        ProcurementRequestStatus.Approved, ProcurementRequestStatus.PartiallyFulfilled, ProcurementRequestStatus.SupplyApproval,
        ProcurementRequestStatus.AnalystApproval,
    ];

    /// <summary>Заявки, по которым можно заказывать: утверждённые директором (или сами — всё со склада) и частично выполненные.</summary>
    internal static readonly ProcurementRequestStatus[] Orderable = [ProcurementRequestStatus.Approved, ProcurementRequestStatus.PartiallyFulfilled];

    private static DateTimeOffset Now => DateTimeOffset.UtcNow;

    private static void RequireView(ProcurementContext ctx)
    {
        if (!ctx.ReadsAll && !ctx.IsDirector && !ctx.Warehouse)
        {
            throw new FieldForbiddenException("Заказы поставщикам видят снабжение, директор и кладовщик.");
        }
    }

    private static void RequireManage(ProcurementContext ctx)
    {
        if (!ctx.Manages)
        {
            throw new FieldForbiddenException("Заказы поставщикам и предложения ведёт снабжение.");
        }
    }

    // ---------- Потребности и справочники ----------

    /// <summary>Утверждённые потребности, которые ещё не заказаны целиком (для нового заказа); requestId — только одной заявки.</summary>
    public async Task<List<ProcurementNeedRow>> NeedsAsync(ProcurementContext ctx, Guid? requestId, CancellationToken ct)
    {
        RequireView(ctx);
        var orderable = Orderable;
        var q = db.ProcurementRequestItems.AsNoTracking()
            .Where(i => orderable.Contains(i.Request!.Status) && i.Status != ProcurementItemStatus.Cancelled && i.Quantity - i.FromStock > i.OrderedQty);
        if (requestId is { } id)
        {
            q = q.Where(i => i.RequestId == id);
        }

        return await q.OrderBy(i => i.Request!.ApprovedAt).ThenBy(i => i.Request!.Number).ThenBy(i => i.LineNo)
            .Select(i => new ProcurementNeedRow(i.Id, i.RequestId, i.Request!.Number, i.Request.Title, i.Request.Department!.Name, i.Name, i.UnitCode,
                i.Quantity - i.FromStock, i.OrderedQty, i.Quantity - i.FromStock - i.OrderedQty, i.EstimatedPrice, i.Category!.Name))
            .ToListAsync(ct);
    }

    /// <summary>Кого можно назначить ответственным: активные сотрудники с правом обработки заявок (роль или отмечено в карточке).</summary>
    public async Task<List<ProcurementUserOption>> ResponsiblesAsync(ProcurementContext ctx, CancellationToken ct)
    {
        RequireView(ctx);
        var ids = await db.ActiveUsersWithAsync(Permissions.ProcurementManage, ct);
        return await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .OrderBy(u => u.FullName)
            .Select(u => new ProcurementUserOption(u.Id, u.FullName))
            .ToListAsync(ct);
    }

    // ---------- Список и сводка ----------

    public async Task<PageResult<ProcurementOrderRow>> SearchAsync(ProcurementContext ctx, ProcurementOrderSearch s, CancellationToken ct)
    {
        RequireView(ctx);
        var today = FieldClock.Today;
        var week = today.AddDays(7);
        var live = ProcurementOrderRules.Live;
        var q = db.ProcurementPurchaseOrders.AsNoTracking();
        q = s.Status switch
        {
            null or "" or "any" => q,
            "open" => q.Where(o => o.Status != ProcurementOrderStatus.Cancelled && o.Status != ProcurementOrderStatus.Delivered && o.Status != ProcurementOrderStatus.Completed),
            "awaiting" => q.Where(o => o.Status == ProcurementOrderStatus.AwaitingApproval),
            "overdue" => q.Where(o => live.Contains(o.Status) && o.ExpectedDate != null && o.ExpectedDate < today),
            "expected" => q.Where(o => live.Contains(o.Status) && o.ExpectedDate != null && o.ExpectedDate >= today && o.ExpectedDate <= week),
            "partial" => q.Where(o => o.Status == ProcurementOrderStatus.PartiallyDelivered),
            "done" => q.Where(o => o.Status == ProcurementOrderStatus.Delivered || o.Status == ProcurementOrderStatus.Completed),
            "cancelled" => q.Where(o => o.Status == ProcurementOrderStatus.Cancelled),
            _ => ParseStatuses(s.Status) is { Count: > 0 } list ? q.Where(o => list.Contains(o.Status)) : q,
        };
        if (s.SupplierId is { } supplier)
        {
            q = q.Where(o => o.SupplierId == supplier);
        }

        if (s.ResponsibleId is { } responsible)
        {
            q = q.Where(o => o.ResponsibleId == responsible);
        }

        if (s.From is { } from)
        {
            var fromUtc = FieldClock.DayStartUtc(from);
            q = q.Where(o => o.CreatedAt >= fromUtc);
        }

        if (s.To is { } to)
        {
            var toUtc = FieldClock.DayStartUtc(to.AddDays(1));
            q = q.Where(o => o.CreatedAt < toUtc);
        }

        if (!string.IsNullOrWhiteSpace(s.Search))
        {
            var text = s.Search.Trim().ToLower();
            q = q.Where(o => o.Number.ToLower().Contains(text) || o.Supplier!.Name.ToLower().Contains(text)
                             || o.Items.Any(i => i.Name.ToLower().Contains(text) || i.RequestItem!.Request!.Number.ToLower().Contains(text)));
        }

        var total = await q.CountAsync(ct);
        var pageSize = Math.Clamp(s.PageSize, 5, 200);
        var page = Math.Max(s.Page, 1);
        q = s.Sort switch
        {
            "number" => s.Desc ? q.OrderByDescending(o => o.Number) : q.OrderBy(o => o.Number),
            "supplier" => s.Desc ? q.OrderByDescending(o => o.Supplier!.Name) : q.OrderBy(o => o.Supplier!.Name),
            "total" => s.Desc ? q.OrderByDescending(o => o.Total) : q.OrderBy(o => o.Total),
            "expected" => s.Desc ? q.OrderBy(o => o.ExpectedDate == null).ThenByDescending(o => o.ExpectedDate) : q.OrderBy(o => o.ExpectedDate == null).ThenBy(o => o.ExpectedDate),
            "responsible" => s.Desc ? q.OrderByDescending(o => o.Responsible!.FullName) : q.OrderBy(o => o.Responsible!.FullName),
            "status" => s.Desc ? q.OrderByDescending(o => o.Status) : q.OrderBy(o => o.Status),
            _ => q.OrderByDescending(o => o.CreatedAt),
        };
        var rows = await q.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new
            {
                o.Id, o.Number, o.Status, o.SupplierId, Supplier = o.Supplier!.Name, o.ResponsibleId, Responsible = o.Responsible!.FullName,
                Lines = o.Items.Count, First = o.Items.OrderBy(i => i.LineNo).Select(i => i.Name).FirstOrDefault(),
                Requests = o.Items.Select(i => i.RequestItem!.Request!.Number).Distinct().ToList(),
                o.Total, o.OrderDate, o.ExpectedDate, o.CreatedAt, o.ApprovalRound,
            })
            .ToListAsync(ct);
        return new PageResult<ProcurementOrderRow>(rows.Select(o => new ProcurementOrderRow(o.Id, o.Number, o.Status, o.SupplierId, o.Supplier, o.ResponsibleId,
            o.Responsible, o.Lines, o.First, o.Requests.Order().ToList(), o.Total, o.OrderDate, o.ExpectedDate,
            ProcurementOrderRules.IsOverdue(o.Status, o.ExpectedDate, today), o.CreatedAt, o.ApprovalRound)).ToList(), total, page, pageSize);
    }

    public async Task<ProcurementOrderSummary> SummaryAsync(ProcurementContext ctx, CancellationToken ct)
    {
        RequireView(ctx);
        var today = FieldClock.Today;
        var week = today.AddDays(7);
        var monthStart = FieldClock.DayStartUtc(new DateOnly(today.Year, today.Month, 1));
        var live = ProcurementOrderRules.Live;
        var all = db.ProcurementPurchaseOrders.AsNoTracking();
        var open = all.Where(o => o.Status != ProcurementOrderStatus.Cancelled && o.Status != ProcurementOrderStatus.Delivered && o.Status != ProcurementOrderStatus.Completed);
        return new ProcurementOrderSummary(
            await open.CountAsync(ct),
            await all.CountAsync(o => o.Status == ProcurementOrderStatus.Draft, ct),
            await all.CountAsync(o => o.Status == ProcurementOrderStatus.AwaitingApproval, ct),
            await all.CountAsync(o => live.Contains(o.Status) && o.ExpectedDate != null && o.ExpectedDate < today, ct),
            await all.CountAsync(o => live.Contains(o.Status) && o.ExpectedDate != null && o.ExpectedDate >= today && o.ExpectedDate <= week, ct),
            await all.CountAsync(o => o.Status == ProcurementOrderStatus.PartiallyDelivered, ct),
            await all.CountAsync(o => o.Status == ProcurementOrderStatus.Cancelled && o.CancelledAt >= monthStart, ct),
            await open.SumAsync(o => (decimal?)o.Total, ct) ?? 0m);
    }

    private static List<ProcurementOrderStatus> ParseStatuses(string csv) =>
        csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => Enum.TryParse<ProcurementOrderStatus>(v, true, out var e) && Enum.IsDefined(e) ? (ProcurementOrderStatus?)e : null).OfType<ProcurementOrderStatus>()
            .Distinct().ToList();

    // ---------- Карточка ----------

    public async Task<ProcurementOrderDetail> GetAsync(ProcurementContext ctx, Guid id, CancellationToken ct)
    {
        RequireView(ctx);
        var o = await db.ProcurementPurchaseOrders.AsNoTracking().Include(x => x.Supplier).Include(x => x.Responsible).FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new FieldNotFoundException("Заказ не найден.");
        var items = await db.ProcurementPurchaseOrderItems.AsNoTracking().Where(i => i.OrderId == id).OrderBy(i => i.LineNo)
            .Select(i => new ProcurementOrderItemView(i.Id, i.LineNo, i.RequestItemId, i.RequestItem!.RequestId, i.RequestItem.Request!.Number, i.Name, i.UnitCode,
                i.Quantity, i.UnitPrice, Math.Round(i.Quantity * i.UnitPrice, 2), i.ReceivedQty, i.RequestItem.EstimatedPrice, i.ClosedQty))
            .ToListAsync(ct);
        var approvals = await db.ProcurementOrderApprovals.AsNoTracking().Where(a => a.OrderId == id).OrderBy(a => a.Round)
            .Select(a => new ProcurementOrderApprovalView(a.Id, a.Round, a.Decision, a.Comment, a.Decider!.FullName, a.Total, a.CreatedAt))
            .ToListAsync(ct);
        var history = await db.ProcurementAuditLogs.AsNoTracking().Where(e => e.DocType == ProcurementOrderRules.OrderDoc && e.DocId == id)
            .OrderBy(e => e.CreatedAt).ThenBy(e => e.Id)
            .Select(e => new ProcurementEventView(e.Id, e.Action, e.Text, e.Actor == null ? "Система" : e.Actor.FullName, e.FromStatus, e.ToStatus, e.Comment, e.CreatedAt))
            .ToListAsync(ct);
        var names = await db.Users.AsNoTracking().Where(u => u.Id == o.ApprovedById || u.Id == o.CreatedById).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        var receipts = await db.ProcurementStockDocuments.AsNoTracking().Where(d => d.OrderId == id).OrderBy(d => d.CreatedAt)
            .Select(d => new ProcurementOrderReceiptRef(d.Id, d.Number, d.Status, d.Reference, d.Actor == null ? null : d.Actor.FullName, d.CreatedAt, d.PostedAt,
                d.ReceiptLines.Any(l => l.Defective > 0 || l.Shortage > 0)))
            .ToListAsync(ct);
        var claims = await ProcurementReceiptService.ClaimsAsync(db, id, null, ct);
        var manage = ctx.Manages;
        var can = new ProcurementOrderAbilities(
            manage && ProcurementOrderRules.Editable(o.Status) && items.All(i => i.ReceivedQty == 0),
            manage && o.Status == ProcurementOrderStatus.Draft,
            ctx.IsDirector && o.Status == ProcurementOrderStatus.AwaitingApproval,
            ctx.IsDirector && o.Status == ProcurementOrderStatus.AwaitingApproval,
            manage && o.Status == ProcurementOrderStatus.Approved,
            manage && ProcurementOrderRules.Cancellable(o.Status) && items.All(i => i.ReceivedQty == 0),
            manage || ctx.Warehouse,
            // Покупку «Куплено — привезёт поставщик» принимают поставкой (машина → качество → вес), не приёмкой по заказу.
            ctx.Warehouse && ProcurementOrderRules.Receivable.Contains(o.Status) && !o.Direct,
            manage && o.Status == ProcurementOrderStatus.PartiallyDelivered,
            manage && o.Status == ProcurementOrderStatus.Delivered && claims.All(c => c.Status != ProcurementClaimStatus.Open),
            manage && claims.Any(c => c.Status == ProcurementClaimStatus.Open));
        var reasons = o.Status == ProcurementOrderStatus.AwaitingApproval
            ? history.LastOrDefault(h => h.ToStatus == nameof(ProcurementOrderStatus.AwaitingApproval))?.Comment
            : null;
        return new ProcurementOrderDetail(o.Id, o.Number, o.Status, o.SupplierId, o.Supplier!.Name, o.Supplier.Phone, o.OfferId, o.ResponsibleId, o.Responsible!.FullName,
            o.OrderDate, o.ExpectedDate, ProcurementOrderRules.IsOverdue(o.Status, o.ExpectedDate, FieldClock.Today), o.PaymentTerms, o.Discount, o.VatRate,
            o.PricesIncludeVat, o.DeliveryCost, o.Subtotal, o.VatAmount, o.Total, o.Comment, o.ApprovalRound, o.ApprovedAt,
            o.ApprovedById is { } a ? names.GetValueOrDefault(a) : null, o.CreatedAt, o.CreatedById is { } c ? names.GetValueOrDefault(c) : null, o.Version, o.TaskId,
            items, approvals, await documents.ListAsync(ctx, ProcurementOrderRules.OrderDoc, id, ct), history, can, reasons, receipts, claims, o.DeliveredAt, o.CompletedAt);
    }

    // ---------- Создание и правка ----------

    public Task<Guid> CreateAsync(ProcurementContext ctx, ProcurementOrderInput input, CancellationToken ct) => ledger.RetryAsync(async () =>
    {
        RequireManage(ctx);
        if (input.ClientKey is { } key)
        {
            var done = await db.ProcurementPurchaseOrders.AsNoTracking().Where(o => o.CreatedById == ctx.UserId && o.ClientKey == key).Select(o => o.Id).FirstOrDefaultAsync(ct);
            if (done != Guid.Empty)
            {
                return done;
            }
        }

        var order = new ProcurementPurchaseOrder
        {
            Number = "",
            CreatedById = ctx.UserId,
            ClientKey = input.ClientKey,
        };
        var (supplier, needs) = await ApplyAsync(ctx, order, input, ct);
        order.Number = await numbers.NextAsync(ProcurementOrderRules.OrderPrefix, ct);
        db.ProcurementPurchaseOrders.Add(order);
        await SyncOrderedAsync(order, needs, ct);
        Log(ctx, order, "created", null, ProcurementOrderStatus.Draft,
            $"Создан заказ у «{supplier.Name}»: {ProcurementRequestService.Positions(order.Items.Count)}, ≈ {ProcurementRules.Money(order.Total)} сум", null, null);
        if (order.OfferId is { } offerId && await db.ProcurementOffers.FirstOrDefaultAsync(x => x.Id == offerId, ct) is { } offer)
        {
            offer.Status = ProcurementOfferStatus.Selected;
            offer.UpdatedAt = Now;
        }

        await CreateTaskAsync(ctx, order, supplier.Name, ct);
        if (input.Submit)
        {
            await SubmitCoreAsync(ctx, order, needs, ct);
        }

        await db.SaveChangesAsync(ct);
        return order.Id;
    });

    /// <summary>
    /// Правка: черновик — свободно; утверждённый и заказанный — существенное изменение (поставщик, рост количества, цены или итога сверх
    /// допуска, новые и удалённые позиции) отправляет заказ директору повторно, остальное — только в историю.
    /// </summary>
    public Task UpdateAsync(ProcurementContext ctx, Guid id, ProcurementOrderInput input, CancellationToken ct) => ledger.RetryAsync(async () =>
    {
        RequireManage(ctx);
        var order = await db.ProcurementPurchaseOrders.Include(o => o.Items).Include(o => o.Supplier).FirstOrDefaultAsync(o => o.Id == id, ct)
                    ?? throw new FieldNotFoundException("Заказ не найден.");
        if (!ProcurementOrderRules.Editable(order.Status))
        {
            throw new FieldConflictException($"Заказ «{ProcurementOrderRules.Label(order.Status)}» — изменить его нельзя.");
        }

        if (input.Version is { } version && version != order.Version)
        {
            throw new FieldConflictException("Заказ уже изменили в другом окне — обновите страницу.");
        }

        if (order.Items.Any(i => i.ReceivedQty > 0))
        {
            throw new FieldConflictException("По заказу уже есть приёмка — изменить его нельзя.");
        }

        var before = Snapshot(order, order.Supplier!.Name);
        var responsible = order.ResponsibleId;
        var previousItems = order.Items.Select(i => i.RequestItemId).ToList();
        var (supplier, needs) = await ApplyAsync(ctx, order, input, ct);
        await SyncOrderedAsync(order, needs, ct, previousItems);
        var after = Snapshot(order, supplier.Name);
        order.UpdatedAt = Now;
        var changes = Describe(before, after);
        if (order.ResponsibleId != responsible)
        {
            await ReassignTaskAsync(ctx, order, ct);
        }

        await SyncTaskDueAsync(ctx, order, ct);

        if (order.Status is ProcurementOrderStatus.Approved or ProcurementOrderStatus.Ordered && order.ApprovedSnapshot is { } json)
        {
            var approved = JsonSerializer.Deserialize<ProcurementOrderSnapshot>(json, Json)!;
            var settings = await db.ProcurementSettings.AsNoTracking().FirstOrDefaultAsync(ct);
            var reasons = ProcurementOrderRules.MaterialChanges(approved, after, settings?.ReapprovalTolerancePercent ?? 0m);
            if (reasons.Count > 0)
            {
                var from = order.Status;
                order.Status = ProcurementOrderStatus.AwaitingApproval;
                order.ApprovalRound++;
                var text = string.Join("; ", reasons);
                Log(ctx, order, "reapproval", from, order.Status, "Существенное изменение после утверждения — повторное согласование директором", text,
                    JsonSerializer.Serialize(new { before, after }, Json));
                await NotifyDirectorsAsync(ctx, order, supplier.Name, text, again: true, ct);
                await db.SaveChangesAsync(ct);
                return true;
            }

            Log(ctx, order, "updated", null, null, $"Изменено после утверждения, без повторного согласования{(changes is null ? "" : $": {changes}")}", null,
                JsonSerializer.Serialize(new { before, after }, Json));
        }
        else if (changes is not null)
        {
            Log(ctx, order, "updated", null, null, $"Изменено: {changes}", null, JsonSerializer.Serialize(new { before, after }, Json));
        }

        if (input.Submit && order.Status == ProcurementOrderStatus.Draft)
        {
            await SubmitCoreAsync(ctx, order, needs, ct);
        }

        await db.SaveChangesAsync(ct);
        return true;
    });

    /// <summary>
    /// Поля и строки заказа из формы с проверкой: поставщик активен; ответственный — сотрудник снабжения; строки — позиции утверждённых
    /// заявок, не больше незаказанного остатка «к закупке» (без учёта этого же заказа); цены и суммы — не отрицательные. Итоги считаются.
    /// </summary>
    private async Task<(ProcurementSupplier Supplier, Dictionary<Guid, ProcurementRequestItem> Needs)> ApplyAsync(ProcurementContext ctx, ProcurementPurchaseOrder order,
        ProcurementOrderInput input, CancellationToken ct)
    {
        var supplier = await db.ProcurementSuppliers.FirstOrDefaultAsync(s => s.Id == input.SupplierId, ct) ?? throw new FieldValidationException("Выберите поставщика.");
        if (!supplier.IsActive && supplier.Id != order.SupplierId)
        {
            throw new FieldValidationException($"Поставщик «{supplier.Name}» отключён.");
        }

        if (input.OfferId is { } offerId)
        {
            var offerSupplier = await db.ProcurementOffers.AsNoTracking().Where(o => o.Id == offerId).Select(o => (Guid?)o.SupplierId).FirstOrDefaultAsync(ct);
            if (offerSupplier != supplier.Id)
            {
                throw new FieldValidationException("Предложение другого поставщика — выберите поставщика из предложения.");
            }
        }

        var responsibleId = input.ResponsibleId ?? (order.ResponsibleId == Guid.Empty ? ctx.UserId : order.ResponsibleId);
        if (!await db.Users.AnyAsync(u => u.Id == responsibleId && u.IsActive, ct))
        {
            throw new FieldValidationException("Ответственный не найден.");
        }

        var lines = (input.Lines ?? []).ToList();
        if (lines.Count == 0)
        {
            throw new FieldValidationException("Добавьте хотя бы одну позицию из утверждённых заявок.");
        }

        if (lines.Count > 200 || lines.GroupBy(l => l.RequestItemId).Any(g => g.Count() > 1))
        {
            throw new FieldValidationException("Позиция заявки повторяется — укажите её одной строкой.");
        }

        CheckMoney(input.Discount, "Скидка");
        CheckMoney(input.DeliveryCost, "Доставка");
        if (input.VatRate is < 0 or > 50 || decimal.Round(input.VatRate, 2) != input.VatRate)
        {
            throw new FieldValidationException("НДС — от 0 до 50 %.");
        }

        var ids = lines.Select(l => l.RequestItemId).ToList();
        var needs = await db.ProcurementRequestItems.Include(i => i.Request).Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var mine = order.Items.ToDictionary(i => i.RequestItemId, i => i.Quantity);
        var orderable = Orderable;
        for (var n = 0; n < lines.Count; n++)
        {
            var line = lines[n];
            var item = needs.GetValueOrDefault(line.RequestItemId) ?? throw new FieldValidationException($"Строка {n + 1}: позиция заявки не найдена.");
            if (!orderable.Contains(item.Request!.Status) || item.Status == ProcurementItemStatus.Cancelled)
            {
                throw new FieldConflictException(
                    $"«{item.Name}» ({item.Request.Number}): заявка «{ProcurementRules.Label(item.Request.Status)}» — заказывать можно только по утверждённой директором заявке.");
            }

            if (line.Quantity <= 0 || decimal.Round(line.Quantity, 3) != line.Quantity)
            {
                throw new FieldValidationException($"«{item.Name}»: количество — больше нуля, не больше трёх знаков после запятой.");
            }

            var free = item.ToPurchase - item.OrderedQty + mine.GetValueOrDefault(item.Id);
            if (line.Quantity > free)
            {
                throw new FieldConflictException(
                    $"«{item.Name}» ({item.Request.Number}): к закупке утверждено {ProcurementRules.Quantity(item.ToPurchase)} {item.UnitCode}, ещё не заказано {ProcurementRules.Quantity(free)} — больше не заказать.");
            }

            if (line.UnitPrice < 0 || line.UnitPrice > ProcurementRules.MaxPrice || decimal.Round(line.UnitPrice, 2) != line.UnitPrice)
            {
                throw new FieldValidationException($"«{item.Name}»: цена — не отрицательная, не больше двух знаков после запятой.");
            }
        }

        order.SupplierId = supplier.Id;
        order.OfferId = input.OfferId ?? order.OfferId;
        order.ResponsibleId = responsibleId;
        order.ExpectedDate = input.ExpectedDate;
        order.PaymentTerms = Text(input.PaymentTerms, 500, "Условия оплаты") ?? supplier.PaymentTerms;
        order.Discount = input.Discount;
        order.VatRate = input.VatRate;
        order.PricesIncludeVat = input.PricesIncludeVat;
        order.DeliveryCost = input.DeliveryCost;
        order.Comment = Text(input.Comment, 2000, "Комментарий");

        foreach (var gone in order.Items.Where(i => !ids.Contains(i.RequestItemId)).ToList())
        {
            order.Items.Remove(gone);
            db.ProcurementPurchaseOrderItems.Remove(gone);
        }

        for (var n = 0; n < lines.Count; n++)
        {
            var line = lines[n];
            var item = needs[line.RequestItemId];
            var existing = order.Items.FirstOrDefault(i => i.RequestItemId == line.RequestItemId);
            if (existing is null)
            {
                existing = new ProcurementPurchaseOrderItem { OrderId = order.Id, RequestItemId = item.Id, Name = item.Name, UnitCode = item.UnitCode };
                order.Items.Add(existing);
                db.ProcurementPurchaseOrderItems.Add(existing);
            }

            existing.LineNo = n + 1;
            existing.Quantity = line.Quantity;
            existing.UnitPrice = line.UnitPrice;
        }

        var (subtotal, vat, total) = ProcurementOrderRules.Totals(order.Items.Select(i => (i.Quantity, i.UnitPrice)), order.Discount, order.VatRate,
            order.PricesIncludeVat, order.DeliveryCost);
        if (order.Discount > subtotal)
        {
            throw new FieldValidationException("Скидка больше суммы позиций.");
        }

        (order.Subtotal, order.VatAmount, order.Total) = (subtotal, vat, total);
        return (supplier, needs);
    }

    /// <summary>
    /// «Заказано» у позиций заявок: сумма по всем неотменённым заказам без закрытого недопоставленного остатка. Не больше утверждённого
    /// «к закупке» — иначе 409 (одновременные
    /// заказы по одной позиции). Заявка помечается изменённой — её версия (xmin) не даёт двум заказам занять одно и то же.
    /// </summary>
    private async Task SyncOrderedAsync(ProcurementPurchaseOrder order, Dictionary<Guid, ProcurementRequestItem> needs, CancellationToken ct, IEnumerable<Guid>? previous = null)
    {
        var ids = order.Items.Select(i => i.RequestItemId).Concat(previous ?? []).Distinct().ToList();
        foreach (var missing in ids.Where(id => !needs.ContainsKey(id)).ToList())
        {
            needs[missing] = await db.ProcurementRequestItems.Include(i => i.Request).FirstAsync(i => i.Id == missing, ct);
        }

        var others = await db.ProcurementPurchaseOrderItems.AsNoTracking()
            .Where(i => ids.Contains(i.RequestItemId) && i.OrderId != order.Id && i.Order!.Status != ProcurementOrderStatus.Cancelled)
            .GroupBy(i => i.RequestItemId)
            .Select(g => new { g.Key, Quantity = g.Sum(i => i.Quantity - i.ClosedQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Quantity, ct);
        foreach (var id in ids)
        {
            var item = needs[id];
            var mine = order.Status == ProcurementOrderStatus.Cancelled ? 0m : order.Items.Where(i => i.RequestItemId == id).Sum(i => i.Quantity - i.ClosedQty);
            var ordered = others.GetValueOrDefault(id) + mine;
            if (ordered > item.ToPurchase)
            {
                throw new FieldConflictException(
                    $"«{item.Name}»: заказано бы {ProcurementRules.Quantity(ordered)} при утверждённых к закупке {ProcurementRules.Quantity(item.ToPurchase)} — обновите и проверьте заказы.");
            }

            item.OrderedQty = ordered;
            item.UpdatedAt = Now;
            if (item.Status != ProcurementItemStatus.Cancelled)
            {
                item.Status = ProcurementRules.ItemStatus(item, submitted: true);
            }

            item.Request!.UpdatedAt = Now;
        }
    }

    private static void CheckMoney(decimal value, string field)
    {
        if (value < 0 || value > ProcurementRules.MaxPrice || decimal.Round(value, 2) != value)
        {
            throw new FieldValidationException($"{field} — не отрицательная сумма, не больше двух знаков после запятой.");
        }
    }

    private static ProcurementOrderSnapshot Snapshot(ProcurementPurchaseOrder order, string supplierName) => new(order.SupplierId, supplierName,
        order.Items.OrderBy(i => i.LineNo).Select(i => new ProcurementOrderLine(i.RequestItemId, i.Name, i.Quantity, i.UnitPrice)).ToList(), order.Total);

    /// <summary>Что изменилось в заказе — текстом для истории; null — ничего.</summary>
    private static string? Describe(ProcurementOrderSnapshot a, ProcurementOrderSnapshot b)
    {
        var parts = new List<string>();
        if (a.SupplierId != b.SupplierId)
        {
            parts.Add($"поставщик {a.SupplierName} → {b.SupplierName}");
        }

        var before = a.Lines.ToDictionary(l => l.RequestItemId);
        var after = b.Lines.ToDictionary(l => l.RequestItemId);
        parts.AddRange(a.Lines.Where(l => !after.ContainsKey(l.RequestItemId)).Select(l => $"удалена позиция «{l.Name}»"));
        foreach (var line in b.Lines)
        {
            if (!before.TryGetValue(line.RequestItemId, out var old))
            {
                parts.Add($"добавлена позиция «{line.Name}»");
            }
            else if (old.Quantity != line.Quantity || old.UnitPrice != line.UnitPrice)
            {
                parts.Add($"«{line.Name}»: {ProcurementRules.Quantity(old.Quantity)} × {ProcurementRules.Money(old.UnitPrice)} → {ProcurementRules.Quantity(line.Quantity)} × {ProcurementRules.Money(line.UnitPrice)}");
            }
        }

        if (a.Total != b.Total && parts.Count == 0)
        {
            parts.Add($"итог {ProcurementRules.Money(a.Total)} → {ProcurementRules.Money(b.Total)}");
        }

        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    // ---------- Статусы ----------

    /// <summary>Оформить черновик: в рамках утверждённой заявки — утверждён сам, иначе — к директору.</summary>
    public Task SubmitAsync(ProcurementContext ctx, Guid id, CancellationToken ct) => ledger.RetryAsync(async () =>
    {
        RequireManage(ctx);
        var order = await TrackedAsync(id, ct);
        if (order.Status != ProcurementOrderStatus.Draft)
        {
            return order.Status is ProcurementOrderStatus.Approved or ProcurementOrderStatus.AwaitingApproval
                ? true
                : throw new FieldConflictException($"Заказ «{ProcurementOrderRules.Label(order.Status)}» — оформить его нельзя.");
        }

        var ids = order.Items.Select(i => i.RequestItemId).ToList();
        var needs = await db.ProcurementRequestItems.Include(i => i.Request).Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        await SubmitCoreAsync(ctx, order, needs, ct);
        await db.SaveChangesAsync(ct);
        return true;
    });

    private async Task SubmitCoreAsync(ProcurementContext ctx, ProcurementPurchaseOrder order, Dictionary<Guid, ProcurementRequestItem> needs, CancellationToken ct)
    {
        if (needs.Values.Any(i => !Orderable.Contains(i.Request!.Status)))
        {
            throw new FieldConflictException("В заказе есть позиции неутверждённых заявок — заказывать можно только утверждённое директором.");
        }

        var supplier = order.Supplier?.Name ?? await db.ProcurementSuppliers.AsNoTracking().Where(s => s.Id == order.SupplierId).Select(s => s.Name).FirstAsync(ct);
        var settings = await db.ProcurementSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var approved = needs.Values.ToDictionary(i => i.Id, i => new ProcurementApprovedNeed(i.Id, i.Name, i.EstimatedPrice));
        var lines = order.Items.Select(i => new ProcurementOrderLine(i.RequestItemId, i.Name, i.Quantity, i.UnitPrice)).ToList();
        var reasons = ProcurementOrderRules.NeedsDirector(lines, approved, order.VatRate, order.PricesIncludeVat, order.Total, settings?.ReapprovalTolerancePercent ?? 0m);
        var from = order.Status;
        if (reasons.Count == 0)
        {
            order.Status = ProcurementOrderStatus.Approved;
            order.ApprovedAt = Now;
            order.ApprovedById = null;
            order.ApprovedSnapshot = JsonSerializer.Serialize(Snapshot(order, supplier), Json);
            Log(ctx, order, "approved", from, order.Status, "Оформлен: в рамках утверждённой директором заявки — согласование не нужно", null, null);
            return;
        }

        order.Status = ProcurementOrderStatus.AwaitingApproval;
        order.ApprovalRound++;
        var text = string.Join("; ", reasons);
        Log(ctx, order, "submitted", from, order.Status, "Оформлен и передан директору на согласование", text, null);
        await NotifyDirectorsAsync(ctx, order, supplier, text, again: false, ct);
    }

    /// <summary>Утвердить — только директор (роль из базы); снимок условий — для проверки следующих правок.</summary>
    public Task ApproveAsync(ProcurementContext ctx, Guid id, string? comment, CancellationToken ct) => DecideAsync(ctx, id, ProcurementApprovalDecision.Approved, comment, ct);

    /// <summary>Отклонить — только директор, с комментарием: заказ отменяется, заказанное количество освобождается.</summary>
    public Task RejectAsync(ProcurementContext ctx, Guid id, string? comment, CancellationToken ct) => DecideAsync(ctx, id, ProcurementApprovalDecision.Rejected, comment, ct);

    /// <summary>Вернуть на доработку — только директор, с комментарием: заказ снова черновик.</summary>
    public Task ReturnAsync(ProcurementContext ctx, Guid id, string? comment, CancellationToken ct) => DecideAsync(ctx, id, ProcurementApprovalDecision.Revision, comment, ct);

    private Task DecideAsync(ProcurementContext ctx, Guid id, ProcurementApprovalDecision decision, string? comment, CancellationToken ct) => ledger.RetryAsync(async () =>
    {
        RequireView(ctx);
        var order = await TrackedAsync(id, ct);
        var target = decision switch
        {
            ProcurementApprovalDecision.Approved => ProcurementOrderStatus.Approved,
            ProcurementApprovalDecision.Rejected => ProcurementOrderStatus.Cancelled,
            _ => ProcurementOrderStatus.Draft,
        };
        if (order.Status == target && order.ApprovalRound > 0 && await db.ProcurementOrderApprovals.AnyAsync(a => a.OrderId == id && a.Round == order.ApprovalRound, ct))
        {
            return true; // двойное нажатие
        }

        if (!ctx.IsDirector)
        {
            throw new FieldForbiddenException("Утвердить, отклонить или вернуть заказ на согласовании может только директор.");
        }

        if (order.Status != ProcurementOrderStatus.AwaitingApproval)
        {
            throw new FieldConflictException($"Заказ «{ProcurementOrderRules.Label(order.Status)}» — решения директора он не ждёт.");
        }

        var note = Text(comment, 2000, "Комментарий");
        if (decision != ProcurementApprovalDecision.Approved && note is null)
        {
            throw new FieldValidationException(decision == ProcurementApprovalDecision.Rejected ? "Напишите комментарий: почему заказ отклонён." : "Напишите комментарий: что исправить в заказе.");
        }

        var supplier = order.Supplier!.Name;
        var snapshot = JsonSerializer.Serialize(Snapshot(order, supplier), Json);
        db.ProcurementOrderApprovals.Add(new ProcurementOrderApproval
        {
            OrderId = order.Id, Round = Math.Max(order.ApprovalRound, 1), Decision = decision, Comment = note, DeciderId = ctx.UserId, Total = order.Total, Snapshot = snapshot,
        });
        var from = order.Status;
        order.Status = target;
        order.UpdatedAt = Now;
        string action, text, notice;
        switch (decision)
        {
            case ProcurementApprovalDecision.Approved:
                order.ApprovedAt = Now;
                order.ApprovedById = ctx.UserId;
                order.ApprovedSnapshot = snapshot;
                (action, text, notice) = ("approved", "Утверждён директором", $"Заказ {order.Number} утверждён директором");
                break;
            case ProcurementApprovalDecision.Rejected:
                order.CancelledAt = Now;
                await ReleaseAsync(order, ct);
                await CloseTaskAsync(ctx, order, $"Заказ отклонён директором: {note}", ct);
                (action, text, notice) = ("rejected", "Отклонён директором — заказ отменён", $"Заказ {order.Number} отклонён директором");
                break;
            default:
                (action, text, notice) = ("returned", "Директор вернул на доработку", $"Заказ {order.Number} возвращён на доработку");
                break;
        }

        Log(ctx, order, action, from, order.Status, text, note, null);
        if (order.ResponsibleId != ctx.UserId)
        {
            notifier.Notify(order.ResponsibleId, UserNotificationKind.ProcurementStatus, notice, note ?? $"{supplier} · {ProcurementRules.Money(order.Total)} сум",
                $"/procurement/orders/{order.Id}");
        }

        await db.SaveChangesAsync(ct);
        return true;
    });

    /// <summary>Заказ отправлен поставщику (снабжение): дата заказа — сегодня, если не была указана; инициаторам заявок — уведомление.</summary>
    public Task MarkOrderedAsync(ProcurementContext ctx, Guid id, DateOnly? orderDate, CancellationToken ct) => ledger.RetryAsync(async () =>
    {
        RequireManage(ctx);
        var order = await TrackedAsync(id, ct);
        if (order.Status == ProcurementOrderStatus.Ordered)
        {
            return true;
        }

        if (order.Status != ProcurementOrderStatus.Approved)
        {
            throw new FieldConflictException(order.Status is ProcurementOrderStatus.Draft or ProcurementOrderStatus.AwaitingApproval
                ? "Заказ ещё не утверждён — сначала оформите его (дороже утверждённого решает директор)."
                : $"Заказ «{ProcurementOrderRules.Label(order.Status)}» — отметить заказанным нельзя.");
        }

        var date = orderDate ?? FieldClock.Today;
        if (date > FieldClock.Today)
        {
            throw new FieldValidationException("Дата заказа — не позже сегодняшней.");
        }

        order.Status = ProcurementOrderStatus.Ordered;
        order.OrderDate = date;
        order.OrderedAt = Now;
        order.UpdatedAt = Now;
        Log(ctx, order, "ordered", ProcurementOrderStatus.Approved, order.Status,
            $"Заказан у поставщика {date:dd.MM.yyyy}{(order.ExpectedDate is { } e ? $", ожидается {e:dd.MM.yyyy}" : "")}", null, null);
        var initiators = await db.ProcurementPurchaseOrderItems.AsNoTracking().Where(i => i.OrderId == id)
            .Select(i => new { i.RequestItem!.Request!.InitiatorId, i.RequestItem.RequestId, i.RequestItem.Request.Number }).Distinct().ToListAsync(ct);
        foreach (var r in initiators.Where(r => r.InitiatorId != ctx.UserId))
        {
            notifier.Notify(r.InitiatorId, UserNotificationKind.ProcurementStatus, $"Заказано у поставщика по заявке {r.Number}",
                $"{order.Supplier!.Name}{(order.ExpectedDate is { } expected ? $" · ожидается {expected:dd.MM.yyyy}" : "")}", $"/requests/{r.RequestId}");
        }

        await db.SaveChangesAsync(ct);
        return true;
    });

    /// <summary>Отменить до поставки (снабжение): причина обязательна (кроме черновика), заказанное количество освобождается, задача закрывается.</summary>
    public Task CancelAsync(ProcurementContext ctx, Guid id, string? reason, CancellationToken ct) => ledger.RetryAsync(async () =>
    {
        RequireManage(ctx);
        var order = await TrackedAsync(id, ct);
        if (order.Status == ProcurementOrderStatus.Cancelled)
        {
            return true;
        }

        if (!ProcurementOrderRules.Cancellable(order.Status) || order.Items.Any(i => i.ReceivedQty > 0))
        {
            throw new FieldConflictException($"Заказ «{ProcurementOrderRules.Label(order.Status)}» — поставка уже началась, отменить нельзя.");
        }

        if (await ProcurementArrivalService.HasOpenAsync(db, order.Id, ct))
        {
            throw new FieldConflictException($"По заказу {order.Number} машина на приёмке — сначала примите или отмените поставку.");
        }

        var note = Text(reason, 2000, "Причина");
        if (note is null && order.Status != ProcurementOrderStatus.Draft)
        {
            throw new FieldValidationException("Укажите причину отмены заказа.");
        }

        var from = order.Status;
        order.Status = ProcurementOrderStatus.Cancelled;
        order.CancelledAt = Now;
        order.UpdatedAt = Now;
        await ReleaseAsync(order, ct);
        await CloseTaskAsync(ctx, order, $"Заказ отменён{(note is null ? "" : $": {note}")}", ct);
        Log(ctx, order, "cancelled", from, order.Status, "Заказ отменён", note, null);
        if (order.ResponsibleId != ctx.UserId)
        {
            notifier.Notify(order.ResponsibleId, UserNotificationKind.ProcurementStatus, $"Заказ {order.Number} отменён", note, $"/procurement/orders/{order.Id}");
        }

        await db.SaveChangesAsync(ct);
        return true;
    });

    /// <summary>Отменённый заказ освобождает «заказано» у позиций заявок.</summary>
    private async Task ReleaseAsync(ProcurementPurchaseOrder order, CancellationToken ct)
    {
        var ids = order.Items.Select(i => i.RequestItemId).ToList();
        var needs = await db.ProcurementRequestItems.Include(i => i.Request).Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        await SyncOrderedAsync(order, needs, ct);
    }

    private async Task<ProcurementPurchaseOrder> TrackedAsync(Guid id, CancellationToken ct) =>
        await db.ProcurementPurchaseOrders.Include(o => o.Items).Include(o => o.Supplier).FirstOrDefaultAsync(o => o.Id == id, ct)
        ?? throw new FieldNotFoundException("Заказ не найден.");

    // ---------- Задача ответственному и уведомления ----------

    /// <summary>Задача ответственному в «Задачах» (отдел «Снабжение»): оформить заказ у поставщика и проконтролировать поставку к сроку.</summary>
    private async Task CreateTaskAsync(ProcurementContext ctx, ProcurementPurchaseOrder order, string supplier, CancellationToken ct)
    {
        var department = await db.Departments.AsNoTracking().Where(d => d.Code == "supply").Select(d => (Guid?)d.Id).FirstOrDefaultAsync(ct);
        if (department is null)
        {
            return;
        }

        var task = new WorkTask
        {
            DepartmentId = department.Value,
            Title = $"Заказ {order.Number}: оформить у «{supplier}» и проконтролировать поставку",
            Description = string.Join("\n", order.Items.OrderBy(i => i.LineNo).Select(i => $"• {i.Name} — {ProcurementRules.Quantity(i.Quantity)} {i.UnitCode}")),
            AssigneeId = order.ResponsibleId,
            CreatedById = ctx.UserId,
            DueDate = order.ExpectedDate,
            Link = $"/procurement/orders/{order.Id}",
        };
        db.WorkTasks.Add(task);
        db.WorkTaskEvents.Add(new WorkTaskEvent { TaskId = task.Id, ActorId = ctx.UserId, Kind = "created", Text = $"Создана из заказа поставщику {order.Number}" });
        order.TaskId = task.Id;
        if (order.ResponsibleId != ctx.UserId)
        {
            notifier.Notify(order.ResponsibleId, UserNotificationKind.TaskAssigned, $"Вам поставлена задача: {task.Title}", supplier, $"/tasks/task/{task.Id}");
        }
    }

    private async Task ReassignTaskAsync(ProcurementContext ctx, ProcurementPurchaseOrder order, CancellationToken ct)
    {
        if (order.TaskId is not { } taskId || await db.WorkTasks.FirstOrDefaultAsync(t => t.Id == taskId, ct) is not { } task)
        {
            return;
        }

        task.AssigneeId = order.ResponsibleId;
        task.UpdatedAt = Now;
        db.WorkTaskEvents.Add(new WorkTaskEvent { TaskId = task.Id, ActorId = ctx.UserId, Kind = "assigned", Text = "Сменился ответственный за заказ" });
        if (order.ResponsibleId != ctx.UserId)
        {
            notifier.Notify(order.ResponsibleId, UserNotificationKind.TaskAssigned, $"Вам поставлена задача: {task.Title}", null, $"/tasks/task/{task.Id}");
        }
    }

    /// <summary>Срок задачи — ожидаемая дата поставки: перенесли дату в заказе — переносится и срок задачи (и её напоминания).</summary>
    private async Task SyncTaskDueAsync(ProcurementContext ctx, ProcurementPurchaseOrder order, CancellationToken ct)
    {
        if (order.TaskId is not { } taskId || await db.WorkTasks.FirstOrDefaultAsync(t => t.Id == taskId, ct) is not { } task
            || task.Status is WorkTaskStatus.Done or WorkTaskStatus.Cancelled || task.DueDate == order.ExpectedDate)
        {
            return;
        }

        task.DueDate = order.ExpectedDate;
        task.UpdatedAt = Now;
        db.WorkTaskEvents.Add(new WorkTaskEvent
        {
            TaskId = task.Id, ActorId = ctx.UserId, Kind = "due",
            Text = order.ExpectedDate is { } date ? $"Срок перенесён на {date:dd.MM.yyyy} (ожидаемая дата поставки)" : "Срок снят: в заказе нет ожидаемой даты",
        });
    }

    /// <summary>Поставка завершена: задача ответственному выполнена.</summary>
    internal async Task FinishTaskAsync(ProcurementContext ctx, ProcurementPurchaseOrder order, string result, CancellationToken ct)
    {
        if (order.TaskId is not { } taskId || await db.WorkTasks.FirstOrDefaultAsync(t => t.Id == taskId, ct) is not { } task
            || task.Status is WorkTaskStatus.Done or WorkTaskStatus.Cancelled)
        {
            return;
        }

        task.Status = WorkTaskStatus.Done;
        task.Result = result.Length <= 2000 ? result : result[..1999] + "…";
        task.CompletedAt = Now;
        task.UpdatedAt = Now;
        db.WorkTaskEvents.Add(new WorkTaskEvent { TaskId = task.Id, ActorId = ctx.UserId, Kind = "accepted", Text = $"Выполнена: {task.Result}" });
    }

    private async Task CloseTaskAsync(ProcurementContext ctx, ProcurementPurchaseOrder order, string result, CancellationToken ct)
    {
        if (order.TaskId is not { } taskId || await db.WorkTasks.FirstOrDefaultAsync(t => t.Id == taskId, ct) is not { } task
            || task.Status is WorkTaskStatus.Done or WorkTaskStatus.Cancelled)
        {
            return;
        }

        task.Status = WorkTaskStatus.Cancelled;
        task.Result = result.Length <= 2000 ? result : result[..1999] + "…";
        task.CompletedAt = Now;
        task.UpdatedAt = Now;
        db.WorkTaskEvents.Add(new WorkTaskEvent { TaskId = task.Id, ActorId = ctx.UserId, Kind = "status", Text = $"Задача отменена: {task.Result}" });
    }

    private async Task NotifyDirectorsAsync(ProcurementContext ctx, ProcurementPurchaseOrder order, string supplier, string reasons, bool again, CancellationToken ct)
    {
        var directors = await ProcurementPeople.DirectorsAsync(db, ct);
        foreach (var id in directors.Where(id => id != ctx.UserId))
        {
            notifier.Notify(id, UserNotificationKind.ProcurementRequest,
                again ? $"Повторное согласование заказа {order.Number} — {supplier}" : $"Заказ ждёт согласования: {order.Number} — {supplier}",
                $"{ProcurementRules.Money(order.Total)} сум · {reasons}", $"/procurement/orders/{order.Id}");
        }
    }

    private void Log(ProcurementContext ctx, ProcurementPurchaseOrder order, string action, ProcurementOrderStatus? from, ProcurementOrderStatus? to, string text, string? comment,
        string? changes) =>
        db.ProcurementAuditLogs.Add(new ProcurementAuditLog
        {
            DocType = ProcurementOrderRules.OrderDoc,
            DocId = order.Id,
            ActorId = ctx.UserId,
            Action = action,
            FromStatus = from?.ToString(),
            ToStatus = to?.ToString(),
            Text = text.Length <= 1000 ? text : text[..999] + "…",
            Comment = comment is { Length: > 2000 } ? comment[..1999] + "…" : comment,
            Changes = changes,
        });

    private static string? Text(string? value, int max, string field)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return text.Length <= max ? text : throw new FieldValidationException($"{field} — до {max} символов.");
    }
}
