using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Security;
using OneBase.Application.Work;
using OneBase.Domain.Procurement;
using OneBase.Domain.Work;

namespace OneBase.Application.Procurement;

/// <summary>Строка приёмки из формы: позиция заказа, пришло (с браком), брак, недостача по накладной, карточка склада, комментарий.</summary>
public sealed record ProcurementReceiptLineInput(Guid OrderItemId, decimal Received, decimal Defective = 0, decimal Shortage = 0, Guid? ItemId = null, string? Comment = null);

/// <summary>
/// Приёмка из формы: заказ, номер накладной поставщика, комментарий, строки. Post — сразу подтвердить (склад меняется только при
/// подтверждении); ClientKey — ключ повтора формы; Version — версия черновика при правке.
/// </summary>
public sealed record ProcurementReceiptInput(
    Guid OrderId,
    string? Reference,
    string? Comment,
    IReadOnlyList<ProcurementReceiptLineInput>? Lines,
    bool Post = false,
    Guid? ClientKey = null,
    uint? Version = null);

/// <summary>Заказ, который ждёт приёмки: сколько строк ещё ждут поставки, черновики приёмки по нему.</summary>
public sealed record ProcurementExpectedRow(
    Guid OrderId,
    string Number,
    ProcurementOrderStatus Status,
    Guid SupplierId,
    string SupplierName,
    string ResponsibleName,
    DateOnly? ExpectedDate,
    bool Overdue,
    int Lines,
    int Waiting,
    IReadOnlyList<string> Requests,
    int Drafts);

public sealed record ProcurementReceiptRow(
    Guid Id,
    string Number,
    ProcurementStockDocumentStatus Status,
    Guid OrderId,
    string OrderNumber,
    Guid SupplierId,
    string SupplierName,
    string? Reference,
    string? ActorName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PostedAt,
    int Lines,
    bool Problems,
    int OpenClaims);

/// <summary>Приёмки: status — draft, posted, claims (есть открытые претензии); заказ, поставщик, поиск (номер, накладная, заказ, поставщик).</summary>
public sealed record ProcurementReceiptSearch(
    string? Status = null,
    Guid? OrderId = null,
    Guid? SupplierId = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 25);

/// <summary>Строка заказа для формы приёмки: заказано, получено, закрыто, ждёт поставки, цена; карточка склада позиции заявки (если есть).</summary>
public sealed record ProcurementReceiptOrderLine(
    Guid OrderItemId,
    int LineNo,
    string Name,
    string UnitCode,
    decimal Ordered,
    decimal Received,
    decimal Closed,
    decimal Remaining,
    decimal UnitPrice,
    Guid RequestId,
    string RequestNumber,
    Guid CategoryId,
    Guid? ItemId,
    string? ItemSku,
    string? ItemName);

public sealed record ProcurementReceiptForm(
    Guid OrderId,
    string OrderNumber,
    ProcurementOrderStatus OrderStatus,
    Guid SupplierId,
    string SupplierName,
    DateOnly? ExpectedDate,
    bool Overdue,
    bool CanReceive,
    IReadOnlyList<ProcurementReceiptOrderLine> Lines);

public sealed record ProcurementReceiptLineView(
    Guid Id,
    int LineNo,
    Guid OrderItemId,
    string Name,
    string UnitCode,
    decimal Ordered,
    decimal UnitPrice,
    Guid RequestId,
    string RequestNumber,
    Guid? ItemId,
    string? ItemSku,
    string? ItemName,
    decimal Received,
    decimal Defective,
    decimal Shortage,
    decimal Accepted,
    string? Comment,
    ProcurementClaimStatus ClaimStatus,
    string? ClaimResolution,
    DateTimeOffset? ClaimResolvedAt,
    string? ClaimResolvedByName);

public sealed record ProcurementReceiptAbilities(bool Edit, bool Post, bool Delete, bool Attach, bool ResolveClaims);

public sealed record ProcurementReceiptDetail(
    Guid Id,
    string Number,
    ProcurementStockDocumentStatus Status,
    Guid OrderId,
    string OrderNumber,
    ProcurementOrderStatus OrderStatus,
    Guid SupplierId,
    string SupplierName,
    string? Reference,
    string? Comment,
    string? ActorName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PostedAt,
    uint Version,
    IReadOnlyList<ProcurementReceiptLineView> Lines,
    IReadOnlyList<ProcurementDocumentView> Documents,
    IReadOnlyList<ProcurementEventView> History,
    ProcurementReceiptAbilities Can,
    ProcurementReceiptForm? Form);

/// <summary>Претензия поставщику (строка приёмки с браком или недостачей) — в карточке заказа и поставщика.</summary>
public sealed record ProcurementClaimView(
    Guid LineId,
    Guid ReceiptId,
    string ReceiptNumber,
    Guid OrderId,
    string OrderNumber,
    string Name,
    string UnitCode,
    decimal Defective,
    decimal Shortage,
    decimal Amount,
    string? Comment,
    ProcurementClaimStatus Status,
    string? Resolution,
    DateTimeOffset? PostedAt,
    DateTimeOffset? ResolvedAt);

/// <summary>
/// Приёмка товара по заказу поставщику (кладовщик): черновик с фактическим количеством, браком, недостачей и накладной — склад не меняется;
/// подтверждение проводит приход (документ RC-, журнал движений), резервирует поступившее под заявку (к выдаче), обновляет «получено» в
/// заказе и заявке, статус поставки заказа. Частичная приёмка — несколько приёмок по одному заказу; лишнее сверх заказанного не
/// принимается. Брак и недостача — претензия поставщику. Повтор формы (ключ) и двойное подтверждение (xmin) не проводят приёмку дважды.
/// </summary>
public sealed class ProcurementReceiptService(
    IAppDbContext db,
    ProcurementStockLedger ledger,
    UserNotifier notifier,
    ProcurementDocumentService documents,
    ProcurementOrderService orders)
{
    private static DateTimeOffset Now => DateTimeOffset.UtcNow;

    private static void RequireView(ProcurementContext ctx)
    {
        if (!ctx.ReadsAll && !ctx.IsDirector && !ctx.Warehouse)
        {
            throw new FieldForbiddenException("Приёмку видят кладовщик, снабжение и директор.");
        }
    }

    private static void RequireWarehouse(ProcurementContext ctx)
    {
        if (!ctx.Warehouse)
        {
            throw new FieldForbiddenException("Принимает товар на склад кладовщик.");
        }
    }

    // ---------- Списки ----------

    /// <summary>Заказы, которые ждут приёмки (заказаны или поставлены частично): сначала просроченные, потом по ожидаемой дате.</summary>
    public async Task<List<ProcurementExpectedRow>> ExpectedAsync(ProcurementContext ctx, CancellationToken ct)
    {
        RequireView(ctx);
        var today = FieldClock.Today;
        var receivable = ProcurementOrderRules.Receivable;
        var rows = await db.ProcurementPurchaseOrders.AsNoTracking().Where(o => receivable.Contains(o.Status))
            .Select(o => new
            {
                o.Id, o.Number, o.Status, o.SupplierId, Supplier = o.Supplier!.Name, Responsible = o.Responsible!.FullName, o.ExpectedDate,
                Lines = o.Items.Count,
                Waiting = o.Items.Count(i => i.Quantity > i.ReceivedQty + i.ClosedQty),
                Requests = o.Items.Select(i => i.RequestItem!.Request!.Number).Distinct().ToList(),
                Drafts = db.ProcurementStockDocuments.Count(d => d.OrderId == o.Id && d.Status == ProcurementStockDocumentStatus.Draft),
            })
            .ToListAsync(ct);
        return rows
            .Select(o => new ProcurementExpectedRow(o.Id, o.Number, o.Status, o.SupplierId, o.Supplier, o.Responsible, o.ExpectedDate,
                ProcurementOrderRules.IsOverdue(o.Status, o.ExpectedDate, today), o.Lines, o.Waiting, o.Requests.Order().ToList(), o.Drafts))
            .OrderByDescending(o => o.Overdue).ThenBy(o => o.ExpectedDate ?? DateOnly.MaxValue).ThenBy(o => o.Number)
            .ToList();
    }

    public async Task<PageResult<ProcurementReceiptRow>> SearchAsync(ProcurementContext ctx, ProcurementReceiptSearch s, CancellationToken ct)
    {
        RequireView(ctx);
        var q = db.ProcurementStockDocuments.AsNoTracking().Where(d => d.OrderId != null);
        q = s.Status switch
        {
            "draft" => q.Where(d => d.Status == ProcurementStockDocumentStatus.Draft),
            "posted" => q.Where(d => d.Status == ProcurementStockDocumentStatus.Posted),
            "claims" => q.Where(d => d.ReceiptLines.Any(l => l.ClaimStatus == ProcurementClaimStatus.Open)),
            _ => q,
        };
        if (s.OrderId is { } order)
        {
            q = q.Where(d => d.OrderId == order);
        }

        if (s.SupplierId is { } supplier)
        {
            q = q.Where(d => d.Order!.SupplierId == supplier);
        }

        if (!string.IsNullOrWhiteSpace(s.Search))
        {
            var text = s.Search.Trim().ToLower();
            q = q.Where(d => d.Number.ToLower().Contains(text) || (d.Reference != null && d.Reference.ToLower().Contains(text))
                             || d.Order!.Number.ToLower().Contains(text) || d.Order.Supplier!.Name.ToLower().Contains(text)
                             || d.ReceiptLines.Any(l => l.OrderItem!.Name.ToLower().Contains(text)));
        }

        var total = await q.CountAsync(ct);
        var pageSize = Math.Clamp(s.PageSize, 5, 200);
        var page = Math.Max(s.Page, 1);
        var rows = await q.OrderBy(d => d.Status == ProcurementStockDocumentStatus.Posted).ThenByDescending(d => d.PostedAt ?? d.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(d => new ProcurementReceiptRow(d.Id, d.Number, d.Status, d.OrderId!.Value, d.Order!.Number, d.Order.SupplierId, d.Order.Supplier!.Name, d.Reference,
                d.Actor == null ? null : d.Actor.FullName, d.CreatedAt, d.PostedAt, d.ReceiptLines.Count,
                d.ReceiptLines.Any(l => l.Defective > 0 || l.Shortage > 0), d.ReceiptLines.Count(l => l.ClaimStatus == ProcurementClaimStatus.Open)))
            .ToListAsync(ct);
        return new PageResult<ProcurementReceiptRow>(rows, total, page, pageSize);
    }

    // ---------- Форма и карточка ----------

    /// <summary>Строки заказа для приёмки: заказано, получено, ждёт поставки, карточка склада.</summary>
    public async Task<ProcurementReceiptForm> FormAsync(ProcurementContext ctx, Guid orderId, CancellationToken ct)
    {
        RequireView(ctx);
        var o = await db.ProcurementPurchaseOrders.AsNoTracking().Where(x => x.Id == orderId)
                    .Select(x => new { x.Id, x.Number, x.Status, x.SupplierId, Supplier = x.Supplier!.Name, x.ExpectedDate })
                    .FirstOrDefaultAsync(ct)
                ?? throw new FieldNotFoundException("Заказ не найден.");
        var lines = await OrderLinesAsync(orderId, ct);
        return new ProcurementReceiptForm(o.Id, o.Number, o.Status, o.SupplierId, o.Supplier, o.ExpectedDate,
            ProcurementOrderRules.IsOverdue(o.Status, o.ExpectedDate, FieldClock.Today), ctx.Warehouse && ProcurementOrderRules.Receivable.Contains(o.Status), lines);
    }

    private Task<List<ProcurementReceiptOrderLine>> OrderLinesAsync(Guid orderId, CancellationToken ct) =>
        db.ProcurementPurchaseOrderItems.AsNoTracking().Where(i => i.OrderId == orderId).OrderBy(i => i.LineNo)
            .Select(i => new ProcurementReceiptOrderLine(i.Id, i.LineNo, i.Name, i.UnitCode, i.Quantity, i.ReceivedQty, i.ClosedQty,
                i.Quantity - i.ReceivedQty - i.ClosedQty > 0 ? i.Quantity - i.ReceivedQty - i.ClosedQty : 0, i.UnitPrice, i.RequestItem!.RequestId,
                i.RequestItem.Request!.Number, i.RequestItem.CategoryId, i.RequestItem.ItemId, i.RequestItem.Item!.Sku, i.RequestItem.Item.Name))
            .ToListAsync(ct);

    public async Task<ProcurementReceiptDetail> GetAsync(ProcurementContext ctx, Guid id, CancellationToken ct)
    {
        RequireView(ctx);
        var d = await db.ProcurementStockDocuments.AsNoTracking().Where(x => x.Id == id && x.OrderId != null)
                    .Select(x => new
                    {
                        x.Id, x.Number, x.Status, OrderId = x.OrderId!.Value, OrderNumber = x.Order!.Number, OrderStatus = x.Order.Status, x.Order.SupplierId,
                        Supplier = x.Order.Supplier!.Name, x.Reference, x.Comment, Actor = x.Actor == null ? null : x.Actor.FullName, x.CreatedAt, x.PostedAt, x.Version,
                    })
                    .FirstOrDefaultAsync(ct)
                ?? throw new FieldNotFoundException("Приёмка не найдена.");
        var lines = await db.ProcurementReceiptLines.AsNoTracking().Where(l => l.DocumentId == id).OrderBy(l => l.LineNo)
            .Select(l => new ProcurementReceiptLineView(l.Id, l.LineNo, l.OrderItemId, l.OrderItem!.Name, l.OrderItem.UnitCode, l.OrderItem.Quantity, l.OrderItem.UnitPrice,
                l.OrderItem.RequestItem!.RequestId, l.OrderItem.RequestItem.Request!.Number, l.ItemId, l.Item!.Sku, l.Item.Name, l.Received, l.Defective, l.Shortage,
                l.Accepted, l.Comment, l.ClaimStatus, l.ClaimResolution, l.ClaimResolvedAt,
                db.Users.Where(u => u.Id == l.ClaimResolvedById).Select(u => u.FullName).FirstOrDefault()))
            .ToListAsync(ct);
        var history = await db.ProcurementAuditLogs.AsNoTracking().Where(e => e.DocType == ProcurementOrderRules.ReceiptDoc && e.DocId == id)
            .OrderBy(e => e.CreatedAt).ThenBy(e => e.Id)
            .Select(e => new ProcurementEventView(e.Id, e.Action, e.Text, e.Actor == null ? "Система" : e.Actor.FullName, e.FromStatus, e.ToStatus, e.Comment, e.CreatedAt))
            .ToListAsync(ct);
        var draft = d.Status == ProcurementStockDocumentStatus.Draft;
        var editable = draft && ctx.Warehouse && ProcurementOrderRules.Receivable.Contains(d.OrderStatus);
        var can = new ProcurementReceiptAbilities(editable, editable, draft && ctx.Warehouse, ctx.Warehouse || ctx.Manages,
            ctx.Manages && lines.Any(l => l.ClaimStatus == ProcurementClaimStatus.Open));
        return new ProcurementReceiptDetail(d.Id, d.Number, d.Status, d.OrderId, d.OrderNumber, d.OrderStatus, d.SupplierId, d.Supplier, d.Reference, d.Comment, d.Actor,
            d.CreatedAt, d.PostedAt, d.Version, lines, await documents.ListAsync(ctx, ProcurementOrderRules.ReceiptDoc, id, ct), history, can,
            draft ? await FormAsync(ctx, d.OrderId, ct) : null);
    }

    /// <summary>
    /// Акт приёмки (для PDF): реквизиты приёмки, заказа и поставщика, строки (заказано, пришло, брак, недостача, принято, цена, сумма),
    /// подписи. Только подтверждённая приёмка — черновик ещё не акт.
    /// </summary>
    public async Task<OneBase.Application.Reports.AiReportContent> ActAsync(ProcurementContext ctx, Guid id, CancellationToken ct)
    {
        var r = await GetAsync(ctx, id, ct);
        if (r.Status != ProcurementStockDocumentStatus.Posted)
        {
            throw new FieldConflictException("Акт формируется по подтверждённой приёмке.");
        }

        var uz = UiLanguage.IsUzbek;
        string L(string ru, string uzText) => uz ? UiLanguage.Uzbek(uzText) : ru;
        System.Text.Json.JsonElement J(object? v) => System.Text.Json.JsonSerializer.SerializeToElement(v);
        var rows = r.Lines.Select(l => (IReadOnlyList<System.Text.Json.JsonElement>)
        [
            J(l.LineNo), J(l.Name + (l.ItemSku is { } sku ? $" ({sku})" : "")), J(l.RequestNumber), J(l.UnitCode), J(l.Ordered), J(l.Received), J(l.Defective), J(l.Shortage),
            J(l.Accepted), J(l.UnitPrice), J(Math.Round(l.Accepted * l.UnitPrice, 2)),
        ]).ToList();
        var posted = r.PostedAt is { } at ? at.ToOffset(FieldClock.Offset).ToString("dd.MM.yyyy HH:mm") : "—";
        var note = string.Join(" · ", new[]
        {
            $"{L("Заказ", "Buyurtma")} {r.OrderNumber}",
            $"{L("поставщик", "yetkazib beruvchi")} {r.SupplierName}",
            r.Reference is { } reference ? $"{L("накладная", "yuk xati")} {reference}" : null,
            $"{L("подтверждена", "tasdiqlangan")} {posted}{(r.ActorName is { } actor ? $", {actor}" : "")}",
            r.Comment,
        }.OfType<string>()) + "\n\n" + L("Сдал (поставщик): ____________________      Принял (кладовщик): ____________________", "Topshirdi (yetkazib beruvchi): ____________________      Qabul qildi (omborchi): ____________________");
        return new OneBase.Application.Reports.AiReportContent($"{L("Акт приёмки", "Qabul dalolatnomasi")} {r.Number}",
        [
            new OneBase.Application.Reports.AiReportSheet(L("Принятый товар", "Qabul qilingan tovar"),
            [
                new("№", OneBase.Application.Reports.ReportFormats.Integer, false), new(L("Наименование", "Nomi"), OneBase.Application.Reports.ReportFormats.Text),
                new(L("Заявка", "Ariza"), OneBase.Application.Reports.ReportFormats.Text), new(L("Ед.", "Birl."), OneBase.Application.Reports.ReportFormats.Text),
                new(L("Заказано", "Buyurtma qilingan"), OneBase.Application.Reports.ReportFormats.Number, false),
                new(L("Пришло", "Keldi"), OneBase.Application.Reports.ReportFormats.Number, false),
                new(L("Брак", "Yaroqsiz"), OneBase.Application.Reports.ReportFormats.Number, false),
                new(L("Недостача", "Kamomad"), OneBase.Application.Reports.ReportFormats.Number, false),
                new(L("Принято", "Qabul qilindi"), OneBase.Application.Reports.ReportFormats.Number, false),
                new(L("Цена, сум", "Narx, so'm"), OneBase.Application.Reports.ReportFormats.Money, false),
                new(L("Сумма, сум", "Summa, so'm"), OneBase.Application.Reports.ReportFormats.Money),
            ], rows, true, note),
        ], [$"OneBase · {L("Снабжение", "Ta'minot")}"]);
    }

    /// <summary>Претензии по заказу или поставщику (строки приёмок с браком или недостачей).</summary>
    internal static async Task<List<ProcurementClaimView>> ClaimsAsync(IAppDbContext db, Guid? orderId, Guid? supplierId, CancellationToken ct)
    {
        var q = db.ProcurementReceiptLines.AsNoTracking().Where(l => l.ClaimStatus != ProcurementClaimStatus.None);
        if (orderId is { } order)
        {
            q = q.Where(l => l.Document!.OrderId == order);
        }

        if (supplierId is { } supplier)
        {
            q = q.Where(l => l.Document!.Order!.SupplierId == supplier);
        }

        return await q.OrderBy(l => l.ClaimStatus == ProcurementClaimStatus.Resolved).ThenByDescending(l => l.Document!.PostedAt).ThenBy(l => l.LineNo)
            .Select(l => new ProcurementClaimView(l.Id, l.DocumentId, l.Document!.Number, l.Document.OrderId!.Value, l.Document.Order!.Number, l.OrderItem!.Name,
                l.OrderItem.UnitCode, l.Defective, l.Shortage, Math.Round((l.Defective + l.Shortage) * l.OrderItem.UnitPrice, 2), l.Comment, l.ClaimStatus,
                l.ClaimResolution, l.Document.PostedAt, l.ClaimResolvedAt))
            .Take(200)
            .ToListAsync(ct);
    }

    // ---------- Черновик и подтверждение ----------

    /// <summary>Новая приёмка: черновик (склад не меняется) или сразу подтверждённая (Post). Повтор формы с тем же ключом — та же приёмка.</summary>
    public Task<Guid> CreateAsync(ProcurementContext ctx, ProcurementReceiptInput input, CancellationToken ct) => ledger.RetryAsync(async () =>
    {
        RequireWarehouse(ctx);
        if (input.ClientKey is { } key)
        {
            var done = await db.ProcurementStockDocuments.AsNoTracking().Where(d => d.ActorId == ctx.UserId && d.ClientKey == key).Select(d => d.Id).FirstOrDefaultAsync(ct);
            if (done != Guid.Empty)
            {
                return done;
            }
        }

        var order = await ReceivableOrderAsync(input.OrderId, ct);
        var warehouse = await ledger.DefaultWarehouseIdAsync(ct);
        var doc = await ledger.DocumentAsync(ProcurementStockDocumentKind.Receipt, warehouse, ctx.UserId, ct);
        doc.Status = ProcurementStockDocumentStatus.Draft;
        doc.OrderId = order.Id;
        doc.ClientKey = input.ClientKey;
        await ApplyAsync(doc, order, input, ct);
        Log(ctx, doc, "created", null, ProcurementStockDocumentStatus.Draft, $"Черновик приёмки по заказу {order.Number}: {Describe(doc, order)}", null);
        if (input.Post)
        {
            await PostCoreAsync(ctx, doc, order, ct);
        }

        await db.SaveChangesAsync(ct);
        return doc.Id;
    });

    /// <summary>Правка черновика (и подтверждение, если Post). Подтверждённую приёмку не правят — исправление делается новым документом.</summary>
    public Task UpdateAsync(ProcurementContext ctx, Guid id, ProcurementReceiptInput input, CancellationToken ct) => ledger.RetryAsync(async () =>
    {
        RequireWarehouse(ctx);
        var doc = await TrackedAsync(id, ct);
        if (doc.Status != ProcurementStockDocumentStatus.Draft)
        {
            throw new FieldConflictException("Приёмка уже подтверждена — её не правят. Исправление — новой приёмкой или списанием.");
        }

        if (input.Version is { } version && version != doc.Version)
        {
            throw new FieldConflictException("Приёмку уже изменили в другом окне — обновите страницу.");
        }

        var order = await ReceivableOrderAsync(doc.OrderId!.Value, ct);
        db.ProcurementReceiptLines.RemoveRange(doc.ReceiptLines);
        doc.ReceiptLines.Clear();
        await ApplyAsync(doc, order, input, ct);
        doc.UpdatedAt = Now;
        Log(ctx, doc, "updated", null, null, $"Черновик изменён: {Describe(doc, order)}", null);
        if (input.Post)
        {
            await PostCoreAsync(ctx, doc, order, ct);
        }

        await db.SaveChangesAsync(ct);
        return true;
    });

    /// <summary>Подтвердить черновик: приход на склад, резерв под заявки, статус поставки. Уже подтверждённая — ничего не делает.</summary>
    public Task PostAsync(ProcurementContext ctx, Guid id, uint? version, CancellationToken ct) => ledger.RetryAsync(async () =>
    {
        RequireWarehouse(ctx);
        var doc = await TrackedAsync(id, ct);
        if (doc.Status == ProcurementStockDocumentStatus.Posted)
        {
            return true;
        }

        if (version is { } v && v != doc.Version)
        {
            throw new FieldConflictException("Приёмку уже изменили в другом окне — обновите страницу.");
        }

        var order = await ReceivableOrderAsync(doc.OrderId!.Value, ct);
        await PostCoreAsync(ctx, doc, order, ct);
        await db.SaveChangesAsync(ct);
        return true;
    });

    /// <summary>Удалить черновик (склад он не менял). Подтверждённую приёмку удалить нельзя.</summary>
    public async Task DeleteAsync(ProcurementContext ctx, Guid id, CancellationToken ct)
    {
        RequireWarehouse(ctx);
        var doc = await TrackedAsync(id, ct);
        if (doc.Status != ProcurementStockDocumentStatus.Draft)
        {
            throw new FieldConflictException("Подтверждённую приёмку удалить нельзя — журнал склада не правится.");
        }

        if (await db.ProcurementDocuments.AnyAsync(x => x.OwnerType == ProcurementOrderRules.ReceiptDoc && x.OwnerId == id, ct))
        {
            throw new FieldConflictException("К черновику прикреплены файлы — сначала удалите их.");
        }

        db.ProcurementStockDocuments.Remove(doc);
        db.ProcurementAuditLogs.Add(new ProcurementAuditLog
        {
            DocType = ProcurementOrderRules.OrderDoc, DocId = doc.OrderId!.Value, ActorId = ctx.UserId, Action = "receipt-deleted",
            Text = $"Удалён черновик приёмки {doc.Number}",
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Закрыть претензию решением (замена, возврат денег, уценка…) — снабжение.</summary>
    public async Task ResolveClaimAsync(ProcurementContext ctx, Guid lineId, string? resolution, CancellationToken ct)
    {
        if (!ctx.Manages)
        {
            throw new FieldForbiddenException("Претензии поставщикам ведёт снабжение.");
        }

        var line = await db.ProcurementReceiptLines.Include(l => l.Document).ThenInclude(d => d!.Order).Include(l => l.OrderItem)
                       .FirstOrDefaultAsync(l => l.Id == lineId, ct)
                   ?? throw new FieldNotFoundException("Претензия не найдена.");
        if (line.ClaimStatus != ProcurementClaimStatus.Open)
        {
            throw new FieldConflictException(line.ClaimStatus == ProcurementClaimStatus.Resolved ? "Претензия уже закрыта." : "По этой строке претензии нет.");
        }

        var text = Text(resolution, 1000, "Решение") ?? throw new FieldValidationException("Опишите решение по претензии: замена, возврат денег, уценка…");
        line.ClaimStatus = ProcurementClaimStatus.Resolved;
        line.ClaimResolution = text;
        line.ClaimResolvedAt = Now;
        line.ClaimResolvedById = ctx.UserId;
        var doc = line.Document!;
        Log(ctx, doc, "claim", null, null, $"Претензия по «{line.OrderItem!.Name}» закрыта", text);
        db.ProcurementAuditLogs.Add(new ProcurementAuditLog
        {
            DocType = ProcurementOrderRules.OrderDoc, DocId = doc.OrderId!.Value, ActorId = ctx.UserId, Action = "claim",
            Text = $"Претензия по «{line.OrderItem.Name}» ({doc.Number}) закрыта", Comment = text,
        });
        await db.SaveChangesAsync(ct);
    }

    private async Task<ProcurementStockDocument> TrackedAsync(Guid id, CancellationToken ct) =>
        await db.ProcurementStockDocuments.Include(d => d.ReceiptLines).FirstOrDefaultAsync(d => d.Id == id && d.OrderId != null, ct)
        ?? throw new FieldNotFoundException("Приёмка не найдена.");

    private async Task<ProcurementPurchaseOrder> ReceivableOrderAsync(Guid orderId, CancellationToken ct)
    {
        var order = await db.ProcurementPurchaseOrders.Include(o => o.Items).Include(o => o.Supplier).FirstOrDefaultAsync(o => o.Id == orderId, ct)
                    ?? throw new FieldNotFoundException("Заказ не найден.");
        if (!ProcurementOrderRules.Receivable.Contains(order.Status))
        {
            throw new FieldConflictException(order.Status is ProcurementOrderStatus.Approved or ProcurementOrderStatus.Draft or ProcurementOrderStatus.AwaitingApproval
                ? $"Заказ «{ProcurementOrderRules.Label(order.Status)}» — снабжение ещё не отметило его заказанным, принимать нечего."
                : $"Заказ «{ProcurementOrderRules.Label(order.Status)}» — поставки по нему больше не ждут.");
        }

        return order;
    }

    /// <summary>Строки из формы: позиция этого заказа, без повторов; пришло, брак, недостача; карточка склада (у позиции с карточкой — она).</summary>
    private async Task ApplyAsync(ProcurementStockDocument doc, ProcurementPurchaseOrder order, ProcurementReceiptInput input, CancellationToken ct)
    {
        doc.Reference = Text(input.Reference, 100, "Номер накладной");
        doc.Comment = Text(input.Comment, 1000, "Комментарий");
        var lines = (input.Lines ?? []).Where(l => l.Received != 0 || l.Defective != 0 || l.Shortage != 0).ToList();
        if (lines.Count == 0)
        {
            throw new FieldValidationException("Укажите, что пришло: фактическое количество, брак или недостачу хотя бы по одной позиции.");
        }

        if (lines.GroupBy(l => l.OrderItemId).Any(g => g.Count() > 1))
        {
            throw new FieldValidationException("Позиция заказа повторяется — укажите её одной строкой.");
        }

        var requestItemIds = order.Items.Select(i => i.RequestItemId).ToList();
        var catalog = await db.ProcurementRequestItems.AsNoTracking().Where(i => requestItemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.ItemId, ct);
        var chosen = lines.Select(l => l.ItemId).OfType<Guid>().Distinct().ToList();
        var items = await db.ProcurementItems.AsNoTracking().Where(i => chosen.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var no = 0;
        foreach (var row in lines)
        {
            var orderItem = order.Items.FirstOrDefault(i => i.Id == row.OrderItemId) ?? throw new FieldValidationException("Позиция не из этого заказа.");
            var remaining = ProcurementOrderRules.Remaining(orderItem.Quantity, orderItem.ReceivedQty, orderItem.ClosedQty);
            var (_, error) = ProcurementOrderRules.ReceiptLine(orderItem.Name, orderItem.UnitCode, row.Received, row.Defective, row.Shortage, remaining);
            if (error is not null)
            {
                throw new FieldValidationException(error);
            }

            var itemId = catalog.GetValueOrDefault(orderItem.RequestItemId) ?? row.ItemId;
            if (catalog.GetValueOrDefault(orderItem.RequestItemId) is { } linked && row.ItemId is { } other && other != linked)
            {
                throw new FieldValidationException($"«{orderItem.Name}»: позиция заявки уже связана с карточкой склада — приходуется на неё.");
            }

            if (catalog.GetValueOrDefault(orderItem.RequestItemId) is null && row.ItemId is { } picked)
            {
                var item = items.GetValueOrDefault(picked) ?? throw new FieldValidationException($"«{orderItem.Name}»: карточка склада не найдена.");
                if (!item.IsActive)
                {
                    throw new FieldValidationException($"«{orderItem.Name}»: карточка «{item.Name}» отключена — выберите другую.");
                }

                if (item.UnitCode != orderItem.UnitCode)
                {
                    throw new FieldValidationException(
                        $"«{orderItem.Name}»: в заказе единица «{orderItem.UnitCode}», у карточки «{item.Name}» — «{item.UnitCode}». Выберите карточку с той же единицей или создайте новую.");
                }
            }

            var line = new ProcurementReceiptLine
            {
                DocumentId = doc.Id,
                LineNo = ++no,
                OrderItemId = orderItem.Id,
                ItemId = itemId,
                Received = row.Received,
                Defective = row.Defective,
                Shortage = row.Shortage,
                Comment = Text(row.Comment, 500, "Комментарий к позиции"),
            };
            doc.ReceiptLines.Add(line);
            db.ProcurementReceiptLines.Add(line);
        }
    }

    /// <summary>
    /// Подтверждение: каждая строка заново сверяется с тем, что ждёт поставки (другие приёмки могли успеть); принятое (пришло − брак)
    /// приходуется на карточку склада по цене заказа и резервируется под позицию заявки — она появляется «К выдаче». Брак и недостача —
    /// открытая претензия. Заказ — «поставлен частично» или «поставлен» (задача ответственному закрывается). Уведомления — всем участникам.
    /// </summary>
    private async Task PostCoreAsync(ProcurementContext ctx, ProcurementStockDocument doc, ProcurementPurchaseOrder order, CancellationToken ct)
    {
        var requestItemIds = order.Items.Select(i => i.RequestItemId).ToList();
        var requestItems = await db.ProcurementRequestItems.Include(i => i.Request).Where(i => requestItemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var accepted = new List<(ProcurementReceiptLine Line, ProcurementPurchaseOrderItem OrderItem, ProcurementRequestItem RequestItem, decimal Quantity)>();
        foreach (var line in doc.ReceiptLines.OrderBy(l => l.LineNo))
        {
            var orderItem = order.Items.First(i => i.Id == line.OrderItemId);
            var requestItem = requestItems[orderItem.RequestItemId];
            var remaining = ProcurementOrderRules.Remaining(orderItem.Quantity, orderItem.ReceivedQty, orderItem.ClosedQty);
            var (quantity, error) = ProcurementOrderRules.ReceiptLine(orderItem.Name, orderItem.UnitCode, line.Received, line.Defective, line.Shortage, remaining);
            if (error is not null)
            {
                throw new FieldConflictException(error);
            }

            line.ItemId = requestItem.ItemId ?? line.ItemId;
            if (quantity > 0 && line.ItemId is null)
            {
                throw new FieldValidationException($"«{orderItem.Name}»: выберите карточку склада (или создайте новую), на которую принять товар.");
            }

            line.Accepted = quantity;
            line.ClaimStatus = line.Defective > 0 || line.Shortage > 0 ? ProcurementClaimStatus.Open : ProcurementClaimStatus.None;
            accepted.Add((line, orderItem, requestItem, quantity));
        }

        var takes = accepted.Where(a => a.Quantity > 0).ToList();
        var balances = await ledger.BalancesAsync(doc.WarehouseId, takes.Select(a => a.Line.ItemId!.Value), ct);
        var activeReservations = await db.ProcurementStockReservations
            .Where(x => requestItemIds.Contains(x.RequestItemId) && x.Status == ProcurementReservationStatus.Active).ToDictionaryAsync(x => x.RequestItemId, ct);
        doc.ActorId = ctx.UserId;
        foreach (var (line, orderItem, requestItem, quantity) in takes)
        {
            var itemId = line.ItemId!.Value;
            var balance = balances[itemId];
            ledger.Move(doc, balance, ProcurementMovementKind.Receipt, quantity, orderItem.Name, orderItem.UnitPrice, requestItem.Id);
            orderItem.ReceivedQty += quantity;
            requestItem.ReceivedQty += quantity;
            requestItem.ItemId ??= itemId;
            requestItem.UpdatedAt = Now;
            var request = requestItem.Request!;
            if (ProcurementRules.Issuable(request.Status) && requestItem.Status != ProcurementItemStatus.Cancelled)
            {
                // Поступившее — под заявку: на складе, но занято до выдачи инициатору.
                if (activeReservations.GetValueOrDefault(requestItem.Id) is { } reservation)
                {
                    if (reservation.ItemId != itemId)
                    {
                        throw new FieldConflictException($"«{orderItem.Name}»: под заявку уже зарезервирована другая карточка склада — примите на неё.");
                    }

                    reservation.Quantity += quantity;
                }
                else
                {
                    var created = new ProcurementStockReservation
                    {
                        WarehouseId = doc.WarehouseId, ItemId = itemId, RequestId = request.Id, RequestItemId = requestItem.Id, Quantity = quantity,
                    };
                    db.ProcurementStockReservations.Add(created);
                    activeReservations[requestItem.Id] = created;
                }

                balance.Reserved += quantity;
                request.UsesStock = true;
            }

            if (requestItem.Status != ProcurementItemStatus.Cancelled)
            {
                requestItem.Status = ProcurementRules.ItemStatus(requestItem, submitted: true);
            }

            request.UpdatedAt = Now;
        }

        doc.Status = ProcurementStockDocumentStatus.Posted;
        doc.PostedAt = Now;
        doc.UpdatedAt = Now;
        var from = order.Status;
        order.Status = ProcurementOrderRules.DeliveryStatus(order.Status, order.Items.Select(i => (i.Quantity, i.ReceivedQty, i.ClosedQty)));
        order.UpdatedAt = Now;
        var delivered = order.Status == ProcurementOrderStatus.Delivered;
        if (delivered)
        {
            order.DeliveredAt = Now;
            await orders.FinishTaskAsync(ctx, order, $"Поставка получена полностью ({doc.Number})", ct);
        }

        var summary = string.Join("; ", accepted.Select(a => LineText(a.OrderItem, a.Line)));
        var problems = accepted.Where(a => a.Line.ClaimStatus == ProcurementClaimStatus.Open).ToList();
        Log(ctx, doc, "posted", ProcurementStockDocumentStatus.Draft, ProcurementStockDocumentStatus.Posted, $"Приёмка подтверждена: {summary}", doc.Comment);
        db.ProcurementAuditLogs.Add(new ProcurementAuditLog
        {
            DocType = ProcurementOrderRules.OrderDoc, DocId = order.Id, ActorId = ctx.UserId, Action = "received",
            FromStatus = from != order.Status ? from.ToString() : null, ToStatus = from != order.Status ? order.Status.ToString() : null,
            Text = Cut($"Приёмка {doc.Number}{(doc.Reference is { } reference ? $" (накладная {reference})" : "")}: {summary}", 1000),
            Comment = doc.Comment,
        });
        foreach (var group in takes.GroupBy(a => a.RequestItem.Request!))
        {
            db.ProcurementAuditLogs.Add(new ProcurementAuditLog
            {
                DocType = ProcurementRules.RequestDoc, DocId = group.Key.Id, ActorId = ctx.UserId, Action = "received",
                Text = Cut($"Поступило на склад по заказу {order.Number} ({doc.Number}): {string.Join(", ", group.Select(a => $"{a.OrderItem.Name} — {ProcurementRules.Quantity(a.Quantity)} {a.OrderItem.UnitCode}"))}", 1000),
            });
        }

        await NotifyAsync(ctx, doc, order, takes.Select(a => (a.RequestItem, a.OrderItem, a.Quantity)).ToList(), problems.Select(a => (a.OrderItem, a.Line)).ToList(),
            summary, delivered, ct);
    }

    /// <summary>
    /// Уведомления о приёмке: ответственному снабжения — что принято (или «поставлен полностью»); инициаторам — товар на складе, можно
    /// получать; другим кладовщикам — к выдаче; брак или недостача — претензия ответственному, руководителю снабжения и директору; заказ
    /// поставлен полностью — директору и руководителю снабжения.
    /// </summary>
    private async Task NotifyAsync(ProcurementContext ctx, ProcurementStockDocument doc, ProcurementPurchaseOrder order,
        List<(ProcurementRequestItem RequestItem, ProcurementPurchaseOrderItem OrderItem, decimal Quantity)> takes,
        List<(ProcurementPurchaseOrderItem OrderItem, ProcurementReceiptLine Line)> problems, string summary, bool delivered, CancellationToken ct)
    {
        var supplier = order.Supplier!.Name;
        var orderLink = $"/procurement/orders/{order.Id}";

        void Send(IEnumerable<Guid> recipients, UserNotificationKind kind, string title, string? body, string link)
        {
            foreach (var id in recipients.Distinct().Where(id => id != ctx.UserId))
            {
                notifier.Notify(id, kind, title, body is null ? null : Cut(body, 500), link);
            }
        }

        Send([order.ResponsibleId], UserNotificationKind.ProcurementStatus,
            delivered ? $"Заказ {order.Number} поставлен полностью" : $"Принято по заказу {order.Number}", $"{supplier} · {summary}", orderLink);
        var reserved = takes.Where(t => ProcurementRules.Issuable(t.RequestItem.Request!.Status) && t.RequestItem.Status != ProcurementItemStatus.Cancelled).ToList();
        foreach (var group in reserved.GroupBy(t => t.RequestItem.Request!))
        {
            var r = group.Key;
            Send([r.InitiatorId], UserNotificationKind.ProcurementStatus, $"Поступило на склад по заявке {r.Number} — можно получить",
                string.Join(", ", group.Select(t => $"{t.OrderItem.Name} — {ProcurementRules.Quantity(t.Quantity)} {t.OrderItem.UnitCode}")), $"/requests/{r.Id}");
        }

        if (reserved.Count > 0)
        {
            Send(await ProcurementPeople.WithPermissionAsync(db, Permissions.ProcurementWarehouse, ct), UserNotificationKind.ProcurementRequest,
                $"К выдаче: поступило по заказу {order.Number}", $"Заявки: {string.Join(", ", reserved.Select(t => t.RequestItem.Request!.Number).Distinct().Order())}",
                "/procurement/issue");
        }

        var leaders = await ProcurementPeople.LeadersAsync(db, ct);
        if (problems.Count > 0)
        {
            var text = string.Join("; ", problems.Select(p => ClaimText(p.OrderItem, p.Line)));
            Send([order.ResponsibleId, .. leaders], UserNotificationKind.ProcurementRequest, $"Претензия к «{supplier}»: заказ {order.Number}", text, $"/procurement/receipts/{doc.Id}");
        }

        if (delivered)
        {
            Send(leaders, UserNotificationKind.ProcurementStatus, $"Заказ {order.Number} поставлен полностью — {supplier}",
                $"{ProcurementRules.Money(order.Total)} сум · {ProcurementRequestService.Positions(order.Items.Count)}", orderLink);
        }
    }

    private static string LineText(ProcurementPurchaseOrderItem item, ProcurementReceiptLine line)
    {
        var parts = new List<string> { $"{item.Name} — принято {ProcurementRules.Quantity(line.Accepted)} {item.UnitCode}" };
        if (line.Defective > 0)
        {
            parts.Add($"брак {ProcurementRules.Quantity(line.Defective)}");
        }

        if (line.Shortage > 0)
        {
            parts.Add($"недостача {ProcurementRules.Quantity(line.Shortage)}");
        }

        return string.Join(", ", parts);
    }

    private static string ClaimText(ProcurementPurchaseOrderItem item, ProcurementReceiptLine line) =>
        $"{item.Name}: {string.Join(", ", new[] { line.Defective > 0 ? $"брак {ProcurementRules.Quantity(line.Defective)} {item.UnitCode}" : null, line.Shortage > 0 ? $"недостача {ProcurementRules.Quantity(line.Shortage)} {item.UnitCode}" : null }.OfType<string>())}";

    private static string Describe(ProcurementStockDocument doc, ProcurementPurchaseOrder order) =>
        string.Join("; ", doc.ReceiptLines.OrderBy(l => l.LineNo).Select(l =>
        {
            var item = order.Items.First(i => i.Id == l.OrderItemId);
            var extra = new[] { l.Defective > 0 ? $"брак {ProcurementRules.Quantity(l.Defective)}" : null, l.Shortage > 0 ? $"недостача {ProcurementRules.Quantity(l.Shortage)}" : null }
                .OfType<string>().ToList();
            return $"{item.Name} — пришло {ProcurementRules.Quantity(l.Received)} {item.UnitCode}{(extra.Count > 0 ? $" ({string.Join(", ", extra)})" : "")}";
        }));

    private void Log(ProcurementContext ctx, ProcurementStockDocument doc, string action, ProcurementStockDocumentStatus? from, ProcurementStockDocumentStatus? to, string text,
        string? comment) =>
        db.ProcurementAuditLogs.Add(new ProcurementAuditLog
        {
            DocType = ProcurementOrderRules.ReceiptDoc,
            DocId = doc.Id,
            ActorId = ctx.UserId,
            Action = action,
            FromStatus = from?.ToString(),
            ToStatus = to?.ToString(),
            Text = Cut(text, 1000),
            Comment = comment is null ? null : Cut(comment, 2000),
        });

    private static string Cut(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";

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
