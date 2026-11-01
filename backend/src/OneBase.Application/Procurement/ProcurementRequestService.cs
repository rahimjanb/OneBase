using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Security;
using OneBase.Application.Work;
using OneBase.Domain.Identity;
using OneBase.Domain.Procurement;
using OneBase.Domain.Work;

namespace OneBase.Application.Procurement;

/// <summary>Строка заявки для таблицы, карточек на телефоне и очереди согласования.</summary>
public sealed record ProcurementRequestRow(
    Guid Id,
    string Number,
    string Title,
    ProcurementRequestStatus Status,
    ProcurementPriority Priority,
    Guid DepartmentId,
    string DepartmentCode,
    string DepartmentName,
    Guid InitiatorId,
    string InitiatorName,
    int ItemsCount,
    string? FirstItem,
    IReadOnlyList<string> Categories,
    decimal EstimatedTotal,
    DateOnly? DesiredDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SubmittedAt,
    int Attachments,
    bool Mine,
    decimal PurchaseTotal = 0,
    int ApprovalRound = 0,
    DateTimeOffset? ForwardedAt = null,
    string? Reason = null);

/// <summary>
/// Поиск заявок. Scope: all — все доступные; my — мои; department — моего отдела и отделов, которыми я руковожу; approvals — ждут решения
/// директора. Status — группа (open, closed, draft) или статусы через запятую; Priority и Department (коды) — через запятую; From/To — дата
/// создания (Ташкент).
/// </summary>
public sealed record ProcurementRequestSearch(
    string Scope = "all",
    string? Status = null,
    string? Priority = null,
    Guid? CategoryId = null,
    string? Department = null,
    DateOnly? From = null,
    DateOnly? To = null,
    string? Search = null,
    string? Sort = null,
    bool Desc = false,
    int Page = 1,
    int PageSize = 25);

/// <summary>
/// Позиция из формы. ItemId — товар из каталога склада (тогда категория и единица — из каталога, остаток проверяется); FromStock — сколько
/// взять со склада (остальное — к закупке); PurchaseReason — почему купить, хотя на складе есть.
/// </summary>
public sealed record ProcurementItemInput(
    Guid? Id,
    string? Name,
    Guid? CategoryId,
    string? UnitCode,
    decimal Quantity,
    decimal? EstimatedPrice,
    string? Comment = null,
    Guid? ItemId = null,
    decimal FromStock = 0,
    string? PurchaseReason = null);

/// <summary>
/// Заявка из формы. Submit — сразу отправить (иначе черновик). ClientKey — ключ повтора формы (двойное нажатие не создаёт вторую заявку).
/// Version — версия, которую видел пользователь при правке: заявку изменили в другой вкладке — 409.
/// </summary>
public sealed record ProcurementRequestInput(
    string? Title,
    Guid? DepartmentId,
    ProcurementPriority Priority,
    DateOnly? DesiredDate,
    string? Reason,
    string? Comment,
    IReadOnlyList<ProcurementItemInput>? Items,
    bool Submit = false,
    Guid? ClientKey = null,
    uint? Version = null);

public sealed record ProcurementItemView(
    Guid Id,
    int LineNo,
    string Name,
    Guid CategoryId,
    string CategoryName,
    string UnitCode,
    string UnitName,
    decimal Quantity,
    decimal? EstimatedPrice,
    decimal? Sum,
    string? Comment,
    ProcurementItemStatus Status,
    Guid? ItemId = null,
    string? Sku = null,
    decimal FromStock = 0,
    decimal ToPurchase = 0,
    decimal IssuedQty = 0,
    string? PurchaseReason = null,
    decimal? Available = null,
    decimal ReservedLeft = 0,
    decimal OrderedQty = 0,
    decimal ReceivedQty = 0);

public sealed record ProcurementDocumentView(Guid Id, string FileName, string ContentType, long SizeBytes, string? UploadedByName, DateTimeOffset CreatedAt, bool CanDelete,
    string? Kind = null);

public sealed record ProcurementEventView(Guid Id, string Action, string Text, string ActorName, string? FromStatus, string? ToStatus, string? Comment, DateTimeOffset CreatedAt);

public sealed record ProcurementApprovalView(Guid Id, int Round, ProcurementApprovalDecision Decision, string? Comment, string DeciderName, decimal PurchaseTotal, DateTimeOffset CreatedAt);

/// <summary>
/// Что пользователь может сделать с заявкой — кнопки карточки (сервер проверяет то же самое при каждом действии). ApprovedEdit — правка
/// утверждённой заявки снабжением (существенное изменение уйдёт директору повторно); Approve — решение директора; Issue — выдача со склада.
/// </summary>
public sealed record ProcurementAbilities(
    bool Edit,
    bool Submit,
    bool Forward,
    bool Return,
    bool Cancel,
    bool Attach,
    bool Approve = false,
    bool Issue = false,
    bool ApprovedEdit = false,
    bool Offer = false,
    bool Order = false);

/// <summary>Заказ поставщику по заявке (в карточке заявки).</summary>
public sealed record ProcurementRequestOrder(Guid Id, string Number, ProcurementOrderStatus Status, string SupplierName, decimal Total, DateOnly? ExpectedDate);

public sealed record ProcurementRequestDetail(
    Guid Id,
    string Number,
    string Title,
    ProcurementRequestStatus Status,
    ProcurementPriority Priority,
    Guid DepartmentId,
    string DepartmentCode,
    string DepartmentName,
    Guid InitiatorId,
    string InitiatorName,
    string? InitiatorPosition,
    DateOnly? DesiredDate,
    string? Reason,
    string? Comment,
    decimal EstimatedTotal,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ForwardedAt,
    DateTimeOffset? CancelledAt,
    uint Version,
    string? RevisionComment,
    IReadOnlyList<ProcurementItemView> Items,
    IReadOnlyList<ProcurementDocumentView> Documents,
    IReadOnlyList<ProcurementEventView> History,
    ProcurementAbilities Can,
    decimal PurchaseTotal = 0,
    int ApprovalRound = 0,
    DateTimeOffset? ApprovedAt = null,
    string? ApprovedByName = null,
    IReadOnlyList<ProcurementApprovalView>? Approvals = null,
    int OffersCount = 0,
    IReadOnlyList<ProcurementRequestOrder>? Orders = null);

public sealed record ProcurementCategoryStat(string Name, int Requests, decimal Total);

/// <summary>
/// Главная раздела «Заявки»: заявки по статусам (из доступных пользователю), заявки месяца и их сумма, срочные, категории месяца,
/// что ждёт действия пользователя (свои черновики и возвраты, у снабжения — отправленные, у директора — на согласовании) и последние.
/// </summary>
public sealed record ProcurementOverview(
    IReadOnlyDictionary<string, int> ByStatus,
    int Open,
    int UrgentOpen,
    int MonthRequests,
    decimal MonthTotal,
    IReadOnlyList<ProcurementCategoryStat> MonthCategories,
    IReadOnlyList<ProcurementRequestRow> NeedsAction,
    IReadOnlyList<ProcurementRequestRow> Recent);

/// <summary>
/// Кнопки разделов в шапке и счётчики: «ждут действия» (возвраты моих заявок, очередь снабжения, согласования директора), заявки на
/// согласовании (директору), заявки к выдаче со склада (кладовщику).
/// </summary>
public sealed record ProcurementNav(
    bool CanCreate,
    bool ReadsAll,
    bool Manages,
    bool Configures,
    bool Heads,
    int NeedsAction,
    bool IsDirector = false,
    bool Warehouse = false,
    bool SeesStock = false,
    int AwaitingApproval = 0,
    int ToIssue = 0,
    int ToReceive = 0);

/// <summary>
/// Раздел «Заявки» (снабжение): заявки на закупку с позициями, категориями, приоритетом, вложениями и историей; проверка склада, резерв,
/// согласование директором, выдача со склада. Сотрудник видит свои заявки, руководитель отдела — отправленные заявки отдела, снабжение и
/// директор — все отправленные, кладовщик — те, где есть часть «со склада». Черновик видит только инициатор. Доступ решается здесь по
/// правам, отделу и роли «Директор» из базы — никогда по параметрам запроса. Каждое изменение пишется в историю в той же записи.
/// </summary>
public sealed partial class ProcurementRequestService(IAppDbContext db, IProcurementNumbers numbers, UserNotifier notifier, ProcurementStockLedger ledger)
{
    private const string NotVisible = "Эту заявку видят её инициатор, руководитель отдела и снабжение.";

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<ProcurementContext> ContextAsync(Guid userId, IReadOnlySet<string> permissions, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => new { u.DepartmentId, Director = u.IsActive && u.Roles.Any(r => r.Role.Name == SystemRoles.Director) })
            .FirstOrDefaultAsync(ct);
        var heads = await db.WorkDepartmentHeads.AsNoTracking().Where(h => h.UserId == userId).Select(h => h.DepartmentId).ToListAsync(ct);
        return new ProcurementContext(userId, permissions, user?.DepartmentId, heads.ToHashSet(), user?.Director ?? false);
    }

    private static void RequireOpen(ProcurementContext ctx)
    {
        if (!ctx.CanOpen)
        {
            throw new FieldForbiddenException("Раздел «Заявки» вам недоступен — нужно право «Заявки».");
        }
    }

    /// <summary>Заявки, которые видит пользователь (фильтр в базе) — то же правило, что ProcurementRules.CanView.</summary>
    private IQueryable<ProcurementRequest> Visible(ProcurementContext ctx)
    {
        var q = db.ProcurementRequests.AsNoTracking();
        var me = ctx.UserId;
        if (ctx.ReadsAll || ctx.IsDirector)
        {
            return q.Where(r => r.InitiatorId == me || r.Status != ProcurementRequestStatus.Draft);
        }

        var heads = ctx.HeadOf.ToArray();
        var warehouse = ctx.Warehouse;
        return q.Where(r => r.InitiatorId == me || (r.Status != ProcurementRequestStatus.Draft && (heads.Contains(r.DepartmentId) || (warehouse && r.UsesStock))));
    }

    private static DateTimeOffset Now => DateTimeOffset.UtcNow;

    // ---------- Шапка и главная ----------

    public async Task<ProcurementNav> NavAsync(ProcurementContext ctx, CancellationToken ct)
    {
        RequireOpen(ctx);
        var me = ctx.UserId;
        var revisions = await db.ProcurementRequests.CountAsync(r => r.InitiatorId == me && r.Status == ProcurementRequestStatus.RevisionRequired, ct);
        var queue = ctx.Manages ? await Visible(ctx).CountAsync(r => r.Status == ProcurementRequestStatus.Submitted, ct) : 0;
        var awaiting = ctx.IsDirector
            ? await db.ProcurementRequests.CountAsync(r => r.Status == ProcurementRequestStatus.DirectorApproval, ct)
              + await db.ProcurementPurchaseOrders.CountAsync(o => o.Status == ProcurementOrderStatus.AwaitingApproval, ct)
            : 0;
        var toIssue = ctx.Warehouse
            ? await db.ProcurementStockReservations.Where(x => x.Status == ProcurementReservationStatus.Active).Select(x => x.RequestId).Distinct().CountAsync(ct)
            : 0;
        var receivable = ProcurementOrderRules.Receivable;
        var toReceive = ctx.Warehouse ? await db.ProcurementPurchaseOrders.CountAsync(o => receivable.Contains(o.Status), ct) : 0;
        return new ProcurementNav(ctx.CanCreate, ctx.ReadsAll, ctx.Manages, ctx.Configures, ctx.HeadOf.Count > 0, revisions + queue + awaiting, ctx.IsDirector,
            ctx.Warehouse, ctx.SeesStock, awaiting, toIssue, toReceive);
    }

    public async Task<ProcurementOverview> OverviewAsync(ProcurementContext ctx, CancellationToken ct)
    {
        RequireOpen(ctx);
        var visible = Visible(ctx);
        var me = ctx.UserId;
        var byStatus = await visible.GroupBy(r => r.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var counts = Enum.GetValues<ProcurementRequestStatus>().ToDictionary(s => s.ToString(), s => byStatus.FirstOrDefault(x => x.Key == s)?.Count ?? 0);
        var open = ProcurementRules.Open;

        var today = FieldClock.Today;
        var monthStart = FieldClock.DayStartUtc(new DateOnly(today.Year, today.Month, 1));
        var month = visible.Where(r => r.CreatedAt >= monthStart && r.Status != ProcurementRequestStatus.Draft && r.Status != ProcurementRequestStatus.Cancelled);
        var monthRequests = await month.CountAsync(ct);
        var monthTotal = monthRequests == 0 ? 0m : await month.SumAsync(r => r.EstimatedTotal, ct);
        // Группировка — в базе, порядок и первые шесть — здесь (категорий немного).
        var byCategory = await db.ProcurementRequestItems.AsNoTracking()
            .Where(i => i.Status != ProcurementItemStatus.Cancelled && month.Any(r => r.Id == i.RequestId))
            .GroupBy(i => i.Category!.Name)
            .Select(g => new { Name = g.Key, Requests = g.Select(i => i.RequestId).Distinct().Count(), Total = g.Sum(i => i.EstimatedPrice == null ? 0m : i.Quantity * i.EstimatedPrice.Value) })
            .ToListAsync(ct);
        var categories = byCategory.OrderByDescending(x => x.Requests).ThenByDescending(x => x.Total).Take(6)
            .Select(x => new ProcurementCategoryStat(x.Name, x.Requests, Math.Round(x.Total, 2)))
            .ToList();
        var urgent = await visible.CountAsync(r => open.Contains(r.Status) && r.Priority == ProcurementPriority.Urgent, ct);

        var manages = ctx.Manages;
        var director = ctx.IsDirector;
        var action = visible.Where(r => (r.InitiatorId == me && (r.Status == ProcurementRequestStatus.Draft || r.Status == ProcurementRequestStatus.RevisionRequired))
                                        || (manages && r.Status == ProcurementRequestStatus.Submitted)
                                        || (director && r.Status == ProcurementRequestStatus.DirectorApproval));
        var needs = await RowsAsync(ctx, action
            .OrderBy(r => r.Status == ProcurementRequestStatus.DirectorApproval ? 0 : r.Status == ProcurementRequestStatus.RevisionRequired ? 1 : r.Status == ProcurementRequestStatus.Submitted ? 2 : 3)
            .ThenByDescending(r => r.Priority == ProcurementPriority.Urgent)
            .ThenBy(r => r.SubmittedAt ?? r.CreatedAt)
            .Take(6), ct);
        var recent = await RowsAsync(ctx, visible.Where(r => r.Status != ProcurementRequestStatus.Draft).OrderByDescending(r => r.CreatedAt).Take(8), ct);

        return new ProcurementOverview(counts, await visible.CountAsync(r => open.Contains(r.Status), ct), urgent, monthRequests,
            Math.Round(monthTotal, 2), categories, needs, recent);
    }

    // ---------- Список ----------

    public async Task<PageResult<ProcurementRequestRow>> SearchAsync(ProcurementContext ctx, ProcurementRequestSearch s, CancellationToken ct)
    {
        RequireOpen(ctx);
        var q = Visible(ctx);
        var me = ctx.UserId;
        switch (s.Scope)
        {
            case "my":
                q = q.Where(r => r.InitiatorId == me);
                break;
            case "department":
                var ids = ctx.HeadOf.ToList();
                if (ctx.DepartmentId is { } own)
                {
                    ids.Add(own);
                }

                q = q.Where(r => ids.Contains(r.DepartmentId));
                break;
            case "approvals":
                q = q.Where(r => r.Status == ProcurementRequestStatus.DirectorApproval);
                break;
        }

        var open = ProcurementRules.Open;
        var closed = ProcurementRules.Closed;
        q = s.Status switch
        {
            null or "" or "any" => q,
            "open" => q.Where(r => open.Contains(r.Status)),
            "closed" => q.Where(r => closed.Contains(r.Status)),
            "draft" => q.Where(r => r.Status == ProcurementRequestStatus.Draft),
            _ => Parse<ProcurementRequestStatus>(s.Status) is { Count: > 0 } list ? q.Where(r => list.Contains(r.Status)) : q,
        };

        var priorities = Parse<ProcurementPriority>(s.Priority);
        if (priorities.Count > 0)
        {
            q = q.Where(r => priorities.Contains(r.Priority));
        }

        if (s.CategoryId is { } category)
        {
            q = q.Where(r => r.Items.Any(i => i.CategoryId == category));
        }

        if (!string.IsNullOrWhiteSpace(s.Department))
        {
            var codes = s.Department.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
            q = q.Where(r => codes.Contains(r.Department!.Code));
        }

        // Дата создания — по Ташкенту: граница дня — полночь Ташкента.
        if (s.From is { } from)
        {
            var fromUtc = FieldClock.DayStartUtc(from);
            q = q.Where(r => r.CreatedAt >= fromUtc);
        }

        if (s.To is { } to)
        {
            var toUtc = FieldClock.DayStartUtc(to.AddDays(1));
            q = q.Where(r => r.CreatedAt < toUtc);
        }

        if (!string.IsNullOrWhiteSpace(s.Search))
        {
            var text = s.Search.Trim().ToLower();
            q = q.Where(r => r.Number.ToLower().Contains(text) || r.Title.ToLower().Contains(text) || r.Initiator!.FullName.ToLower().Contains(text)
                             || r.Items.Any(i => i.Name.ToLower().Contains(text)));
        }

        var total = await q.CountAsync(ct);
        var pageSize = Math.Clamp(s.PageSize, 5, 200);
        var page = Math.Max(s.Page, 1);
        // Очередь директора — сначала давно ждущие.
        var sorted = s.Scope == "approvals" && s.Sort is null ? q.OrderBy(r => r.ForwardedAt) : Sorted(q, s.Sort, s.Desc);
        var rows = await RowsAsync(ctx, sorted.Skip((page - 1) * pageSize).Take(pageSize), ct);
        return new PageResult<ProcurementRequestRow>(rows, total, page, pageSize);
    }

    private static IQueryable<ProcurementRequest> Sorted(IQueryable<ProcurementRequest> q, string? sort, bool desc) => sort switch
    {
        "number" => desc ? q.OrderByDescending(r => r.Number) : q.OrderBy(r => r.Number),
        "title" => desc ? q.OrderByDescending(r => r.Title) : q.OrderBy(r => r.Title),
        "total" => desc ? q.OrderByDescending(r => r.EstimatedTotal) : q.OrderBy(r => r.EstimatedTotal),
        "purchase" => desc ? q.OrderByDescending(r => r.PurchaseTotal) : q.OrderBy(r => r.PurchaseTotal),
        "desired" => desc
            ? q.OrderBy(r => r.DesiredDate == null).ThenByDescending(r => r.DesiredDate)
            : q.OrderBy(r => r.DesiredDate == null).ThenBy(r => r.DesiredDate),
        "priority" => desc
            ? q.OrderByDescending(r => r.Priority == ProcurementPriority.Urgent ? 3 : r.Priority == ProcurementPriority.High ? 2 : r.Priority == ProcurementPriority.Normal ? 1 : 0)
                .ThenByDescending(r => r.CreatedAt)
            : q.OrderBy(r => r.Priority == ProcurementPriority.Urgent ? 3 : r.Priority == ProcurementPriority.High ? 2 : r.Priority == ProcurementPriority.Normal ? 1 : 0)
                .ThenByDescending(r => r.CreatedAt),
        "status" => desc ? q.OrderByDescending(r => r.Status).ThenByDescending(r => r.CreatedAt) : q.OrderBy(r => r.Status).ThenByDescending(r => r.CreatedAt),
        "department" => desc ? q.OrderByDescending(r => r.Department!.Name) : q.OrderBy(r => r.Department!.Name),
        "initiator" => desc ? q.OrderByDescending(r => r.Initiator!.FullName) : q.OrderBy(r => r.Initiator!.FullName),
        "created" when !desc => q.OrderBy(r => r.CreatedAt),
        _ => q.OrderByDescending(r => r.CreatedAt),
    };

    private async Task<List<ProcurementRequestRow>> RowsAsync(ProcurementContext ctx, IQueryable<ProcurementRequest> q, CancellationToken ct)
    {
        var rows = await q.Select(r => new
            {
                r.Id, r.Number, r.Title, r.Status, r.Priority, r.DepartmentId, DepartmentCode = r.Department!.Code, DepartmentName = r.Department.Name, r.InitiatorId,
                Initiator = r.Initiator!.FullName, Items = r.Items.Count, First = r.Items.OrderBy(i => i.LineNo).Select(i => i.Name).FirstOrDefault(),
                Categories = r.Items.Select(i => i.Category!.Name).Distinct().ToList(), r.EstimatedTotal, r.DesiredDate, r.CreatedAt, r.SubmittedAt,
                Attachments = db.ProcurementDocuments.Count(d => d.OwnerType == ProcurementRules.RequestDoc && d.OwnerId == r.Id),
                r.PurchaseTotal, r.ApprovalRound, r.ForwardedAt, r.Reason,
            })
            .ToListAsync(ct);
        return rows.Select(r => new ProcurementRequestRow(r.Id, r.Number, r.Title, r.Status, r.Priority, r.DepartmentId, r.DepartmentCode, r.DepartmentName,
                r.InitiatorId, r.Initiator, r.Items, r.First, r.Categories.Order().ToList(), r.EstimatedTotal, r.DesiredDate, r.CreatedAt, r.SubmittedAt,
                r.Attachments, r.InitiatorId == ctx.UserId, r.PurchaseTotal, r.ApprovalRound, r.ForwardedAt, r.Reason))
            .ToList();
    }

    private static List<T> Parse<T>(string? csv) where T : struct, Enum =>
        string.IsNullOrWhiteSpace(csv) ? [] : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => Enum.TryParse<T>(v, true, out var e) && Enum.IsDefined(e) ? (T?)e : null).OfType<T>().Distinct().ToList();

    // ---------- Карточка ----------

    public async Task<ProcurementRequestDetail> GetAsync(ProcurementContext ctx, Guid id, CancellationToken ct)
    {
        RequireOpen(ctx);
        var r = await db.ProcurementRequests.AsNoTracking().Include(x => x.Department).Include(x => x.Initiator).FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new FieldNotFoundException("Заявка не найдена.");
        if (!ProcurementRules.CanView(ctx, r))
        {
            throw new FieldForbiddenException(NotVisible);
        }

        var units = await db.ProcurementUnits.AsNoTracking().ToDictionaryAsync(u => u.Code, u => u.Name, ct);
        var items = await db.ProcurementRequestItems.AsNoTracking().Where(i => i.RequestId == id).OrderBy(i => i.LineNo)
            .Select(i => new { Item = i, Category = i.Category!.Name, Sku = i.Item == null ? null : i.Item.Sku })
            .ToListAsync(ct);
        var catalogIds = items.Where(x => x.Item.ItemId != null).Select(x => x.Item.ItemId!.Value).Distinct().ToList();
        var available = new Dictionary<Guid, decimal>();
        if (catalogIds.Count > 0)
        {
            var warehouse = await ledger.DefaultWarehouseIdAsync(ct);
            available = await db.ProcurementStockBalances.AsNoTracking().Where(b => b.WarehouseId == warehouse && catalogIds.Contains(b.ItemId))
                .ToDictionaryAsync(b => b.ItemId, b => b.OnHand - b.Reserved, ct);
        }

        var reserved = (await db.ProcurementStockReservations.AsNoTracking()
                .Where(x => x.RequestId == id && x.Status == ProcurementReservationStatus.Active).ToListAsync(ct))
            .GroupBy(x => x.RequestItemId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity - x.Issued));
        var docs = await db.ProcurementDocuments.AsNoTracking().Where(d => d.OwnerType == ProcurementRules.RequestDoc && d.OwnerId == id).OrderBy(d => d.CreatedAt)
            .Select(d => new { Doc = d, By = d.UploadedBy == null ? null : d.UploadedBy.FullName })
            .ToListAsync(ct);
        var history = await db.ProcurementAuditLogs.AsNoTracking().Where(e => e.DocType == ProcurementRules.RequestDoc && e.DocId == id)
            .OrderBy(e => e.CreatedAt).ThenBy(e => e.Id)
            .Select(e => new { Event = e, Actor = e.Actor == null ? null : e.Actor.FullName })
            .ToListAsync(ct);
        var approvals = await db.ProcurementApprovals.AsNoTracking().Where(a => a.RequestId == id).OrderBy(a => a.Round)
            .Select(a => new ProcurementApprovalView(a.Id, a.Round, a.Decision, a.Comment, a.Decider!.FullName, a.PurchaseTotal, a.CreatedAt))
            .ToListAsync(ct);
        var approvedBy = r.ApprovedById is { } by ? await db.Users.AsNoTracking().Where(u => u.Id == by).Select(u => u.FullName).FirstOrDefaultAsync(ct) : null;
        var buyer = ctx.ReadsAll || ctx.IsDirector;
        var offers = buyer ? await db.ProcurementOffers.CountAsync(o => o.RequestId == id, ct) : 0;
        var orders = buyer
            ? await db.ProcurementPurchaseOrders.AsNoTracking().Where(o => o.Items.Any(i => i.RequestItem!.RequestId == id)).OrderBy(o => o.CreatedAt)
                .Select(o => new ProcurementRequestOrder(o.Id, o.Number, o.Status, o.Supplier!.Name, o.Total, o.ExpectedDate)).ToListAsync(ct)
            : [];
        var toOrder = items.Any(x => x.Item.Status != ProcurementItemStatus.Cancelled && x.Item.ToPurchase > x.Item.OrderedQty);

        var owner = ProcurementRules.IsOwner(ctx, r);
        var approvedEdit = ctx.Manages && ProcurementRules.ApprovedEditable(r.Status);
        var hasPurchase = items.Any(x => x.Item.Status != ProcurementItemStatus.Cancelled && x.Item.ToPurchase > 0);
        bool Can(ProcurementRequestStatus to) => ProcurementRules.CanTransition(r.Status, to, owner, ctx.Manages, ctx.IsDirector);
        var can = new ProcurementAbilities(
            (owner && ProcurementRules.Editable(r.Status)) || approvedEdit,
            Can(ProcurementRequestStatus.Submitted),
            Can(ProcurementRequestStatus.DirectorApproval) && hasPurchase,
            Can(ProcurementRequestStatus.RevisionRequired),
            Can(ProcurementRequestStatus.Cancelled),
            CanAttach(ctx, r),
            Can(ProcurementRequestStatus.Approved),
            ctx.Warehouse && ProcurementRules.Issuable(r.Status) && reserved.Values.Any(v => v > 0),
            approvedEdit && !(owner && ProcurementRules.Editable(r.Status)),
            ctx.Manages && hasPurchase && ProcurementOrderService.Offerable.Contains(r.Status),
            ctx.Manages && toOrder && ProcurementOrderService.Orderable.Contains(r.Status));
        var revision = r.Status == ProcurementRequestStatus.RevisionRequired
            ? history.LastOrDefault(h => h.Event.ToStatus == nameof(ProcurementRequestStatus.RevisionRequired))?.Event.Comment
            : null;

        return new ProcurementRequestDetail(r.Id, r.Number, r.Title, r.Status, r.Priority, r.DepartmentId, r.Department!.Code, r.Department.Name, r.InitiatorId,
            r.Initiator!.FullName, r.Initiator.Position, r.DesiredDate, r.Reason, r.Comment, r.EstimatedTotal, r.CreatedAt, r.UpdatedAt, r.SubmittedAt,
            r.ForwardedAt, r.CancelledAt, r.Version, revision,
            items.Select(x => new ProcurementItemView(x.Item.Id, x.Item.LineNo, x.Item.Name, x.Item.CategoryId, x.Category, x.Item.UnitCode,
                units.GetValueOrDefault(x.Item.UnitCode, x.Item.UnitCode), x.Item.Quantity, x.Item.EstimatedPrice,
                ProcurementRules.Sum(x.Item.Quantity, x.Item.EstimatedPrice), x.Item.Comment, x.Item.Status, x.Item.ItemId, x.Sku, x.Item.FromStock,
                x.Item.ToPurchase, x.Item.IssuedQty, x.Item.PurchaseReason,
                x.Item.ItemId is { } itemId ? available.GetValueOrDefault(itemId) : null, reserved.GetValueOrDefault(x.Item.Id), x.Item.OrderedQty, x.Item.ReceivedQty)).ToList(),
            docs.Select(x => new ProcurementDocumentView(x.Doc.Id, x.Doc.FileName, x.Doc.ContentType, x.Doc.SizeBytes, x.By, x.Doc.CreatedAt,
                CanDeleteDocument(ctx, r, x.Doc))).ToList(),
            history.Select(x => new ProcurementEventView(x.Event.Id, x.Event.Action, x.Event.Text, x.Actor ?? "Система", x.Event.FromStatus, x.Event.ToStatus,
                x.Event.Comment, x.Event.CreatedAt)).ToList(),
            can, r.PurchaseTotal, r.ApprovalRound, r.ApprovedAt, approvedBy, approvals, offers, orders);
    }

    // ---------- Создание и правка ----------

    /// <summary>Создаёт черновик (или сразу отправляет — Submit). Повтор с тем же ClientKey возвращает уже созданную заявку.</summary>
    public async Task<Guid> CreateAsync(ProcurementContext ctx, ProcurementRequestInput input, CancellationToken ct)
    {
        RequireOpen(ctx);
        if (!ctx.CanCreate)
        {
            throw new FieldForbiddenException("Создавать заявки может сотрудник с правом «Заявки».");
        }

        return await ledger.RetryAsync(async () =>
        {
            if (input.ClientKey is { } key && await ByClientKeyAsync(ctx, key, ct) is { } existing)
            {
                return existing;
            }

            var department = await ResolveDepartmentAsync(ctx, input.DepartmentId, ct);
            var refs = await RefsAsync(input.Items, ct);
            var items = CheckItems(input.Items, refs, new Dictionary<Guid, ProcurementRequestItem>(), approvedEdit: false);
            var (title, reason, comment) = CheckFields(input, null);
            var request = new ProcurementRequest
            {
                Number = await numbers.NextAsync(ProcurementRules.RequestPrefix, ct),
                Title = title,
                DepartmentId = department.Id,
                InitiatorId = ctx.UserId,
                Priority = input.Priority,
                DesiredDate = input.DesiredDate,
                Reason = reason,
                Comment = comment,
                ClientKey = input.ClientKey,
            };
            for (var i = 0; i < items.Count; i++)
            {
                request.Items.Add(NewItem(request.Id, i + 1, items[i]));
            }

            Totals(request);
            db.ProcurementRequests.Add(request);
            Log(request, ctx.UserId, "created", null, ProcurementRequestStatus.Draft,
                $"Создана заявка: {Positions(items.Count)}{(request.EstimatedTotal > 0 ? $", ≈ {ProcurementRules.Money(request.EstimatedTotal)} сум" : "")}", null, null);
            if (input.Submit)
            {
                await MoveAsync(ctx, request, ProcurementRequestStatus.Submitted, null, department.Name, ct);
            }

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException) when (input.ClientKey is { } k)
            {
                // Та же форма пришла дважды одновременно: вторая упёрлась в уникальный ключ повтора — возвращаем первую заявку.
                db.ChangeTracker.Clear();
                if (await ByClientKeyAsync(ctx, k, ct) is { } first)
                {
                    return first;
                }

                throw;
            }

            return request.Id;
        });
    }

    private async Task<Guid?> ByClientKeyAsync(ProcurementContext ctx, Guid key, CancellationToken ct)
    {
        var id = await db.ProcurementRequests.AsNoTracking().Where(r => r.InitiatorId == ctx.UserId && r.ClientKey == key).Select(r => r.Id).FirstOrDefaultAsync(ct);
        return id == Guid.Empty ? null : id;
    }

    /// <summary>
    /// Правка: черновик или заявка на доработке — инициатором; утверждённая — снабжением (позиции, количества, цены, желаемая дата, приоритет,
    /// комментарий): существенное изменение (рост к закупке, цены сверх допуска, новые или удалённые позиции, другой товар) отправляет её
    /// директору на повторное согласование, остальное — только в историю. Submit — отправить после правки (инициатор).
    /// </summary>
    public Task UpdateAsync(ProcurementContext ctx, Guid id, ProcurementRequestInput input, CancellationToken ct) => ledger.RetryAsync(async () =>
    {
        var r = await RequireTrackedAsync(ctx, id, ct);
        var owner = ProcurementRules.IsOwner(ctx, r);
        var ownEdit = owner && ProcurementRules.Editable(r.Status);
        var approvedEdit = !ownEdit && ctx.Manages && ProcurementRules.ApprovedEditable(r.Status);
        if (!ownEdit && !approvedEdit)
        {
            if (!owner && !ctx.Manages)
            {
                throw new FieldForbiddenException("Изменить заявку может только её инициатор.");
            }

            throw new FieldConflictException($"Заявка «{ProcurementRules.Label(r.Status)}» — изменить её можно в черновике или после возврата на доработку.");
        }

        if (input.Version is { } version && version != r.Version)
        {
            throw new FieldConflictException("Заявку уже изменили в другом окне — обновите страницу.");
        }

        var department = approvedEdit ? r.Department! : await ResolveDepartmentAsync(ctx, input.DepartmentId ?? r.DepartmentId, ct);
        var refs = await RefsAsync(input.Items, ct);
        var existing = r.Items.ToDictionary(i => i.Id);
        var items = CheckItems(input.Items, refs, existing, approvedEdit);
        var (title, reason, comment) = approvedEdit ? (r.Title, r.Reason, Text(input.Comment, 2000, "Комментарий")) : CheckFields(input, r.DesiredDate);
        if (approvedEdit)
        {
            CheckFields(input with { Title = r.Title, Submit = false }, r.DesiredDate);
        }

        var before = Snapshot.Of(r, r.Department!.Name, r.Items, refs);
        r.Title = title;
        r.DepartmentId = department.Id;
        r.Priority = input.Priority;
        r.DesiredDate = input.DesiredDate;
        r.Reason = reason;
        r.Comment = comment;

        var keep = items.Where(i => i.Id is not null).Select(i => i.Id!.Value).ToHashSet();
        foreach (var gone in r.Items.Where(i => !keep.Contains(i.Id)).ToList())
        {
            if (gone.IssuedQty > 0 || (approvedEdit && gone.FromStock > 0))
            {
                throw new FieldValidationException($"Позицию «{gone.Name}» не удалить: по ней уже есть выдача или резерв со склада — уменьшите количество.");
            }

            r.Items.Remove(gone);
            db.ProcurementRequestItems.Remove(gone);
        }

        for (var i = 0; i < items.Count; i++)
        {
            var draft = items[i];
            if (draft.Id is { } itemId && existing.TryGetValue(itemId, out var item))
            {
                item.LineNo = i + 1;
                item.Name = draft.Name;
                item.ItemId = draft.ItemId;
                item.CategoryId = draft.CategoryId;
                item.UnitCode = draft.UnitCode;
                item.Quantity = draft.Quantity;
                item.FromStock = draft.FromStock;
                item.PurchaseReason = draft.PurchaseReason;
                item.EstimatedPrice = draft.Price;
                item.Comment = draft.Comment;
                item.UpdatedAt = Now;
            }
            else
            {
                var added = NewItem(r.Id, i + 1, draft);
                r.Items.Add(added);
                db.ProcurementRequestItems.Add(added);
            }
        }

        Totals(r);
        Refresh(r);
        var after = Snapshot.Of(r, department.Name, r.Items, refs);
        var diff = Snapshot.Diff(before, after);
        if (diff is not null)
        {
            r.UpdatedAt = Now;
        }

        if (approvedEdit && diff is not null)
        {
            var settings = await db.ProcurementSettings.AsNoTracking().FirstOrDefaultAsync(ct);
            var approved = r.ApprovedSnapshot is { } json ? JsonSerializer.Deserialize<List<ProcurementApprovedLine>>(json, Json) ?? [] : [];
            var reasons = ProcurementRules.MaterialChanges(approved, Lines(r), settings?.ReapprovalTolerancePercent ?? 0m);
            if (reasons.Count > 0)
            {
                var from = r.Status;
                r.Status = ProcurementRequestStatus.DirectorApproval;
                r.ApprovalRound++;
                r.ForwardedAt = Now;
                Log(r, ctx.UserId, "reapproval", from, r.Status,
                    $"Существенное изменение после утверждения — повторное согласование директором: {string.Join("; ", reasons)}", null, diff.Value.Json);
                await NotifyAsync(ctx, r, from, r.Status, null, department.Name, ct);
            }
            else
            {
                Log(r, ctx.UserId, "updated", null, null, $"Изменено после утверждения, без повторного согласования: {diff.Value.Text["Изменено: ".Length..]}", null, diff.Value.Json);
            }
        }
        else if (diff is { } d)
        {
            Log(r, ctx.UserId, "updated", null, null, d.Text, null, d.Json);
        }

        if (input.Submit && ownEdit)
        {
            await MoveAsync(ctx, r, ProcurementRequestStatus.Submitted, null, department.Name, ct);
        }

        await db.SaveChangesAsync(ct);
        return true;
    });

    // ---------- Статусы ----------

    /// <summary>Отправить черновик или исправленную заявку (инициатор): часть «со склада» резервируется. Повторная отправка — ничего не меняет.</summary>
    public Task SubmitAsync(ProcurementContext ctx, Guid id, CancellationToken ct) => ChangeAsync(ctx, id, ProcurementRequestStatus.Submitted, null, ct);

    /// <summary>Передать отправленную заявку директору на согласование (снабжение).</summary>
    public Task ForwardAsync(ProcurementContext ctx, Guid id, CancellationToken ct) => ChangeAsync(ctx, id, ProcurementRequestStatus.DirectorApproval, null, ct);

    /// <summary>Вернуть на доработку с обязательным комментарием: отправленную — снабжение, на согласовании — директор. Резерв снимается.</summary>
    public Task ReturnAsync(ProcurementContext ctx, Guid id, string? comment, CancellationToken ct) =>
        ChangeAsync(ctx, id, ProcurementRequestStatus.RevisionRequired, comment, ct);

    /// <summary>Отменить заявку до утверждения (инициатор); комментарий — по желанию. Резерв снимается.</summary>
    public Task CancelAsync(ProcurementContext ctx, Guid id, string? comment, CancellationToken ct) =>
        ChangeAsync(ctx, id, ProcurementRequestStatus.Cancelled, comment, ct);

    /// <summary>Утвердить заявку на согласовании — только директор (роль проверяется по базе); комментарий — по желанию.</summary>
    public Task ApproveAsync(ProcurementContext ctx, Guid id, string? comment, CancellationToken ct) =>
        ChangeAsync(ctx, id, ProcurementRequestStatus.Approved, comment, ct);

    /// <summary>Отклонить заявку на согласовании — только директор, с обязательным комментарием. Резерв снимается.</summary>
    public Task RejectAsync(ProcurementContext ctx, Guid id, string? comment, CancellationToken ct) =>
        ChangeAsync(ctx, id, ProcurementRequestStatus.Rejected, comment, ct);

    private Task ChangeAsync(ProcurementContext ctx, Guid id, ProcurementRequestStatus to, string? comment, CancellationToken ct) => ledger.RetryAsync(async () =>
    {
        var r = await RequireTrackedAsync(ctx, id, ct);
        if (r.Status == to)
        {
            return true; // двойное нажатие: заявка уже в этом статусе
        }

        await MoveAsync(ctx, r, to, comment, r.Department!.Name, ct);
        await db.SaveChangesAsync(ct);
        return true;
    });

    /// <summary>
    /// Переход статуса с проверкой прав и комментария, резервом или его снятием, решением директора, историей и уведомлениями — сохраняет
    /// вызывающий вместе с остальными изменениями. Заявка, где всё берётся со склада, после отправки утверждается сама: закупки нет —
    /// согласовывать директору нечего.
    /// </summary>
    private async Task MoveAsync(ProcurementContext ctx, ProcurementRequest r, ProcurementRequestStatus to, string? comment, string departmentName, CancellationToken ct)
    {
        var from = r.Status;
        var owner = ProcurementRules.IsOwner(ctx, r);
        if (!ProcurementRules.CanTransition(from, to, owner, ctx.Manages, ctx.IsDirector))
        {
            if (ProcurementRules.CanTransition(from, to, true, true, true))
            {
                throw new FieldForbiddenException((from, to) switch
                {
                    (ProcurementRequestStatus.DirectorApproval, _) or (_, ProcurementRequestStatus.Approved or ProcurementRequestStatus.Rejected) =>
                        "Утвердить, отклонить или вернуть заявку на согласовании может только директор.",
                    (_, ProcurementRequestStatus.DirectorApproval) => "Передать заявку директору может снабжение.",
                    (_, ProcurementRequestStatus.RevisionRequired) => "Вернуть заявку на доработку может снабжение.",
                    _ => "Это может сделать только инициатор заявки.",
                });
            }

            throw new FieldConflictException($"Заявка «{ProcurementRules.Label(from)}» — это действие для неё недоступно.");
        }

        var note = Text(comment, 2000, "Комментарий");
        if (ProcurementRules.CommentRequired(to) && note is null)
        {
            throw new FieldValidationException(to == ProcurementRequestStatus.Rejected
                ? "Напишите комментарий: почему заявка отклонена."
                : "Напишите комментарий: что нужно исправить в заявке.");
        }

        var active = r.Items.Where(i => i.Status != ProcurementItemStatus.Cancelled).ToList();
        if (to == ProcurementRequestStatus.Submitted)
        {
            if (active.Count == 0)
            {
                throw new FieldValidationException("Добавьте хотя бы одну позицию — пустую заявку не отправить.");
            }

            if (string.IsNullOrWhiteSpace(r.Reason))
            {
                throw new FieldValidationException("Укажите причину закупки — без неё заявку не отправить.");
            }
        }

        if (to == ProcurementRequestStatus.DirectorApproval && active.All(i => i.ToPurchase <= 0))
        {
            throw new FieldConflictException("В заявке нет позиций к закупке — всё выдаётся со склада, согласование директора не нужно.");
        }

        var now = Now;
        string action, text;
        switch (to)
        {
            case ProcurementRequestStatus.Submitted:
                var reservedText = await ReserveAsync(r, ct);
                r.SubmittedAt = now;
                (action, text) = from == ProcurementRequestStatus.RevisionRequired ? ("resubmitted", "Исправленная заявка отправлена") : ("submitted", "Заявка отправлена");
                if (reservedText is not null)
                {
                    text += $"; зарезервировано на складе: {reservedText}";
                }

                break;
            case ProcurementRequestStatus.DirectorApproval:
                r.ForwardedAt = now;
                r.ApprovalRound++;
                (action, text) = ("forwarded", r.ApprovalRound > 1 ? $"Передана директору на согласование (раунд {r.ApprovalRound})" : "Передана директору на согласование");
                break;
            case ProcurementRequestStatus.Approved:
                r.ApprovedAt = now;
                r.ApprovedById = ctx.UserId;
                r.ApprovedSnapshot = JsonSerializer.Serialize(Lines(r), Json);
                Decide(ctx, r, ProcurementApprovalDecision.Approved, note);
                (action, text) = ("approved", $"Утверждена директором: к закупке ≈ {ProcurementRules.Money(r.PurchaseTotal)} сум");
                break;
            case ProcurementRequestStatus.Rejected:
                await ReleaseAsync(r, ct);
                CancelOpenItems(r);
                Decide(ctx, r, ProcurementApprovalDecision.Rejected, note);
                (action, text) = ("rejected", "Отклонена директором");
                break;
            case ProcurementRequestStatus.RevisionRequired:
                await ReleaseAsync(r, ct);
                if (from == ProcurementRequestStatus.DirectorApproval)
                {
                    Decide(ctx, r, ProcurementApprovalDecision.Revision, note);
                }

                (action, text) = ("returned", from == ProcurementRequestStatus.DirectorApproval ? "Директор вернул на доработку" : "Возвращена на доработку");
                break;
            case ProcurementRequestStatus.Cancelled:
                await ReleaseAsync(r, ct);
                r.CancelledAt = now;
                CancelOpenItems(r);
                (action, text) = ("cancelled", "Заявка отменена");
                break;
            default:
                (action, text) = ("status", $"Статус: {ProcurementRules.Label(from)} → {ProcurementRules.Label(to)}");
                break;
        }

        r.Status = to;
        r.UpdatedAt = now;
        Refresh(r);
        Log(r, ctx.UserId, action, from, to, text, note, null);
        await NotifyAsync(ctx, r, from, to, note, departmentName, ct);

        // Всё со склада — закупать нечего: утверждается сама, без директора.
        if (to == ProcurementRequestStatus.Submitted && r.Items.Where(i => i.Status != ProcurementItemStatus.Cancelled).All(i => i.ToPurchase <= 0))
        {
            r.Status = ProcurementRequestStatus.Approved;
            r.ApprovedAt = now;
            r.ApprovedById = null;
            r.ApprovedSnapshot = JsonSerializer.Serialize(Lines(r), Json);
            Log(r, ctx.UserId, "auto_approved", ProcurementRequestStatus.Submitted, ProcurementRequestStatus.Approved,
                "Всё есть на складе — закупка не нужна, заявка утверждена без согласования директора", null, null);
            notifier.Notify(r.InitiatorId == ctx.UserId ? null : r.InitiatorId, UserNotificationKind.ProcurementStatus,
                $"Заявка {r.Number} утверждена: всё есть на складе", r.Title, $"/requests/{r.Id}");
        }
    }

    /// <summary>Решение директора: запись раунда со снимком позиций и суммой к закупке.</summary>
    private void Decide(ProcurementContext ctx, ProcurementRequest r, ProcurementApprovalDecision decision, string? comment) =>
        db.ProcurementApprovals.Add(new ProcurementApproval
        {
            RequestId = r.Id,
            Round = Math.Max(r.ApprovalRound, 1),
            Decision = decision,
            Comment = comment,
            DeciderId = ctx.UserId,
            PurchaseTotal = r.PurchaseTotal,
            Snapshot = JsonSerializer.Serialize(Lines(r), Json),
        });

    private static void CancelOpenItems(ProcurementRequest r)
    {
        foreach (var item in r.Items.Where(i => i.IssuedQty <= 0))
        {
            item.Status = ProcurementItemStatus.Cancelled;
        }
    }

    /// <summary>Позиции для снимка утверждения (без отменённых).</summary>
    private static List<ProcurementApprovedLine> Lines(ProcurementRequest r) =>
        r.Items.Where(i => i.Status != ProcurementItemStatus.Cancelled).OrderBy(i => i.LineNo)
            .Select(i => new ProcurementApprovedLine(i.Id, i.Name, i.ItemId, i.UnitCode, i.Quantity, i.FromStock, i.ToPurchase, i.EstimatedPrice))
            .ToList();

    /// <summary>Суммы и признак склада: предполагаемая сумма, сумма к закупке, идёт ли заявка через склад (часть «со склада» или закупленное уже на складе).</summary>
    private static void Totals(ProcurementRequest r)
    {
        var active = r.Items.Where(i => i.Status != ProcurementItemStatus.Cancelled).ToList();
        r.EstimatedTotal = ProcurementRules.Total(active.Select(i => (i.Quantity, i.EstimatedPrice)));
        r.PurchaseTotal = ProcurementRules.Total(active.Select(i => (i.ToPurchase, i.EstimatedPrice)));
        r.UsesStock = active.Any(i => i.FromStock > 0 || i.ReceivedQty > 0);
    }

    /// <summary>Статусы позиций по количествам (отменённые остаются отменёнными).</summary>
    private static void Refresh(ProcurementRequest r)
    {
        Totals(r);
        var submitted = r.Status is not (ProcurementRequestStatus.Draft or ProcurementRequestStatus.RevisionRequired);
        foreach (var item in r.Items.Where(i => i.Status != ProcurementItemStatus.Cancelled))
        {
            item.Status = ProcurementRules.ItemStatus(item, submitted);
        }
    }

    // ---------- Склад: резерв ----------

    /// <summary>
    /// Резерв части «со склада» при отправке: доступно = на складе − в резерве. Просят больше доступного — 409 (остаток мог уйти другой
    /// заявке); на складе есть, а просят купить — нужно обоснование. Возвращает текст «Бумага А4 — 40 пачка» для истории или null.
    /// </summary>
    private async Task<string?> ReserveAsync(ProcurementRequest r, CancellationToken ct)
    {
        var stock = r.Items.Where(i => i.Status != ProcurementItemStatus.Cancelled).OrderBy(i => i.LineNo).ToList();
        foreach (var item in stock.Where(i => i.ItemId is null))
        {
            item.FromStock = 0;
        }

        var catalog = stock.Where(i => i.ItemId is not null).ToList();
        if (catalog.Count == 0)
        {
            return null;
        }

        var warehouse = await ledger.DefaultWarehouseIdAsync(ct);
        var balances = await ledger.BalancesAsync(warehouse, catalog.Select(i => i.ItemId!.Value), ct);
        var reserved = new List<string>();
        foreach (var item in catalog)
        {
            var balance = balances[item.ItemId!.Value];
            var available = balance.OnHand - balance.Reserved;
            var need = Math.Max(0, item.FromStock - item.IssuedQty);
            if (need > available)
            {
                throw new FieldConflictException(
                    $"Позиция {item.LineNo} «{item.Name}»: на складе доступно {ProcurementRules.Quantity(available)} {item.UnitCode} — уменьшите количество «со склада» и отправьте снова.");
            }

            var coverable = Math.Min(available, item.Quantity - item.IssuedQty);
            if (need < coverable && string.IsNullOrWhiteSpace(item.PurchaseReason))
            {
                throw new FieldValidationException(
                    $"Позиция {item.LineNo} «{item.Name}»: на складе есть {ProcurementRules.Quantity(available)} {item.UnitCode} — возьмите со склада или обоснуйте закупку.");
            }

            if (need <= 0)
            {
                continue;
            }

            balance.Reserved += need;
            balance.UpdatedAt = Now;
            db.ProcurementStockReservations.Add(new ProcurementStockReservation
            {
                WarehouseId = warehouse,
                ItemId = item.ItemId.Value,
                RequestId = r.Id,
                RequestItemId = item.Id,
                Quantity = need,
            });
            reserved.Add($"{item.Name} — {ProcurementRules.Quantity(need)} {item.UnitCode}");
        }

        return reserved.Count > 0 ? string.Join(", ", reserved) : null;
    }

    /// <summary>Снять активный резерв заявки (невыданная часть возвращается в доступное).</summary>
    private async Task ReleaseAsync(ProcurementRequest r, CancellationToken ct)
    {
        var active = await db.ProcurementStockReservations.Where(x => x.RequestId == r.Id && x.Status == ProcurementReservationStatus.Active).ToListAsync(ct);
        if (active.Count == 0)
        {
            return;
        }

        foreach (var group in active.GroupBy(x => x.WarehouseId))
        {
            var balances = await ledger.BalancesAsync(group.Key, group.Select(x => x.ItemId), ct);
            foreach (var reservation in group)
            {
                var balance = balances[reservation.ItemId];
                balance.Reserved = Math.Max(0, balance.Reserved - (reservation.Quantity - reservation.Issued));
                balance.UpdatedAt = Now;
                reservation.Status = ProcurementReservationStatus.Released;
                reservation.ClosedAt = Now;
            }
        }
    }

    // ---------- Уведомления ----------

    private async Task NotifyAsync(ProcurementContext ctx, ProcurementRequest r, ProcurementRequestStatus from, ProcurementRequestStatus to, string? comment,
        string departmentName, CancellationToken ct)
    {
        var link = $"/requests/{r.Id}";
        var initiator = r.Initiator?.FullName
                        ?? await db.Users.AsNoTracking().Where(u => u.Id == r.InitiatorId).Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? "";
        var positions = Positions(r.Items.Count(i => i.Status != ProcurementItemStatus.Cancelled));
        var money = r.PurchaseTotal > 0 ? $" · к закупке ≈ {ProcurementRules.Money(r.PurchaseTotal)} сум" : r.EstimatedTotal > 0 ? $" · ≈ {ProcurementRules.Money(r.EstimatedTotal)} сум" : "";
        var summary = $"{departmentName} · {initiator} · {positions}{money}";

        void Send(IEnumerable<Guid> recipients, UserNotificationKind kind, string title, string? body)
        {
            foreach (var id in recipients.Distinct().Where(id => id != ctx.UserId))
            {
                notifier.Notify(id, kind, title, body, link);
            }
        }

        switch (to)
        {
            case ProcurementRequestStatus.Submitted:
                var heads = await HeadsAsync(r.DepartmentId, ct);
                var processors = await RoleUsersAsync(Permissions.ProcurementManage, ct);
                var title = from == ProcurementRequestStatus.RevisionRequired ? $"Заявка исправлена: {r.Number}" : $"Новая заявка {r.Number}";
                Send(heads.Concat(processors), UserNotificationKind.ProcurementRequest, $"{title} — {r.Title}", summary);
                if (r.Items.Any(i => i.Status != ProcurementItemStatus.Cancelled && i.ToPurchase > 0))
                {
                    // Директору — сразу, для сведения: решать он будет, когда снабжение передаст заявку на согласование.
                    Send((await DirectorsAsync(ct)).Except(heads).Except(processors), UserNotificationKind.ProcurementStatus, $"{title} — {r.Title}",
                        $"{summary} · сейчас у снабжения");
                }

                if (r.UsesStock)
                {
                    Send(await RoleUsersAsync(Permissions.ProcurementWarehouse, ct), UserNotificationKind.ProcurementRequest, $"К выдаче со склада: {r.Number} — {r.Title}", summary);
                }

                break;
            case ProcurementRequestStatus.DirectorApproval:
                var again = from != ProcurementRequestStatus.Submitted;
                Send(await DirectorsAsync(ct), UserNotificationKind.ProcurementRequest,
                    again ? $"Повторное согласование: {r.Number} — {r.Title}" : $"Заявка ждёт согласования: {r.Number} — {r.Title}", summary);
                Send([r.InitiatorId], UserNotificationKind.ProcurementStatus,
                    again ? $"Заявка {r.Number} изменена и снова на согласовании у директора" : $"Заявка {r.Number} передана директору на согласование", r.Title);
                break;
            case ProcurementRequestStatus.Approved:
                Send([r.InitiatorId], UserNotificationKind.ProcurementStatus, $"Заявка {r.Number} утверждена директором", comment ?? r.Title);
                Send(await RoleUsersAsync(Permissions.ProcurementManage, ct), UserNotificationKind.ProcurementRequest, $"Утверждена к закупке: {r.Number} — {r.Title}", summary);
                break;
            case ProcurementRequestStatus.Rejected:
                Send([r.InitiatorId, .. await RoleUsersAsync(Permissions.ProcurementManage, ct)], UserNotificationKind.ProcurementStatus,
                    $"Заявка {r.Number} отклонена директором", comment);
                break;
            case ProcurementRequestStatus.RevisionRequired:
                var recipients = from == ProcurementRequestStatus.DirectorApproval ? [r.InitiatorId, .. await RoleUsersAsync(Permissions.ProcurementManage, ct)] : new List<Guid> { r.InitiatorId };
                Send(recipients, UserNotificationKind.ProcurementStatus, $"Заявка {r.Number} возвращена на доработку", comment);
                break;
            case ProcurementRequestStatus.Cancelled when from != ProcurementRequestStatus.Draft:
                var notify = (await RoleUsersAsync(Permissions.ProcurementManage, ct)).Concat(from == ProcurementRequestStatus.DirectorApproval ? await DirectorsAsync(ct) : []);
                Send(notify, UserNotificationKind.ProcurementStatus, $"Заявка {r.Number} отменена инициатором", comment ?? r.Title);
                break;
        }
    }

    private Task<List<Guid>> HeadsAsync(Guid departmentId, CancellationToken ct) =>
        db.WorkDepartmentHeads.AsNoTracking().Where(h => h.DepartmentId == departmentId && h.User!.IsActive).Select(h => h.UserId).ToListAsync(ct);

    /// <summary>У кого право от роли (снабжение, кладовщик), кроме администратора — технической роли со всеми правами.</summary>
    private Task<List<Guid>> RoleUsersAsync(string permission, CancellationToken ct) => ProcurementPeople.WithPermissionAsync(db, permission, ct);

    private Task<List<Guid>> DirectorsAsync(CancellationToken ct) => ProcurementPeople.DirectorsAsync(db, ct);

    // ---------- Общее ----------

    /// <summary>Заявка для изменения (отслеживается, с позициями и отделом); не видна — 403.</summary>
    private async Task<ProcurementRequest> RequireTrackedAsync(ProcurementContext ctx, Guid id, CancellationToken ct)
    {
        RequireOpen(ctx);
        var r = await db.ProcurementRequests.Include(x => x.Items).Include(x => x.Department).FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new FieldNotFoundException("Заявка не найдена.");
        if (!ProcurementRules.CanView(ctx, r))
        {
            throw new FieldForbiddenException(NotVisible);
        }

        return r;
    }

    private void Log(ProcurementRequest r, Guid actorId, string action, ProcurementRequestStatus? from, ProcurementRequestStatus? to, string text, string? comment,
        string? changes) =>
        db.ProcurementAuditLogs.Add(new ProcurementAuditLog
        {
            DocType = ProcurementRules.RequestDoc,
            DocId = r.Id,
            ActorId = actorId,
            Action = action,
            FromStatus = from?.ToString(),
            ToStatus = to?.ToString(),
            Text = text.Length <= 1000 ? text : text[..999] + "…",
            Comment = comment,
            Changes = changes,
        });

    /// <summary>«1 позиция», «3 позиции», «5 позиций».</summary>
    public static string Positions(int n)
    {
        var word = (n % 100) is >= 11 and <= 14 ? "позиций" : (n % 10) switch { 1 => "позиция", 2 or 3 or 4 => "позиции", _ => "позиций" };
        return $"{n} {word}";
    }

    /// <summary>Текст поля без пробелов по краям; пустой — null; длиннее max — ошибка с названием поля.</summary>
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
