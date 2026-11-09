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
    string? Reason = null,
    ProcurementRequestKind Kind = ProcurementRequestKind.Regular);

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
/// Version — версия, которую видел пользователь при правке: заявку изменили в другой вкладке — 409. Kind — тип заявки (null: при создании —
/// обычная, при правке — без изменений).
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
    uint? Version = null,
    ProcurementRequestKind? Kind = null);

/// <summary>Позиция в карточке заявки; TracksBatches, ShelfLifeDays — карточка склада ведётся по партиям («Куплено» вносит партии).</summary>
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
    decimal ReceivedQty = 0,
    bool TracksBatches = false,
    int? ShelfLifeDays = null);

public sealed record ProcurementDocumentView(Guid Id, string FileName, string ContentType, long SizeBytes, string? UploadedByName, DateTimeOffset CreatedAt, bool CanDelete,
    string? Kind = null);

public sealed record ProcurementEventView(Guid Id, string Action, string Text, string ActorName, string? FromStatus, string? ToStatus, string? Comment, DateTimeOffset CreatedAt);

public sealed record ProcurementApprovalView(Guid Id, int Round, ProcurementApprovalDecision Decision, string? Comment, string DeciderName, decimal PurchaseTotal, DateTimeOffset CreatedAt,
    string Stage = ProcurementApprovalStages.Director);

/// <summary>
/// Шаг маршрута в карточке заявки: head — руководитель отдела, analyst — аналитик, finance — финансист, director — директор, supply —
/// снабжение. State: done (согласовал), current (решает сейчас), pending (впереди), skipped (не нужен — Reason: initiatorHead, noHeads,
/// noPurchase, underLimit, initiatorDirector), rejected, returned. Who — кто решил или кто решает (имена); пусто у финансиста и директора —
/// не выбран в настройках, у аналитика — нет пользователей с ролью «Аналитик».
/// </summary>
public sealed record ProcurementRouteStep(string Key, string State, IReadOnlyList<string> Who, DateTimeOffset? At = null, string? Comment = null, string? Reason = null);

/// <summary>Цена позиции, уточнённая финансистом при согласовании (UZS за единицу).</summary>
public sealed record ProcurementPriceInput(Guid ItemId, decimal Price);

/// <summary>
/// Что пользователь может сделать с заявкой — кнопки карточки (сервер проверяет то же самое при каждом действии). ApprovedEdit — правка
/// утверждённой заявки снабжением (существенное изменение уйдёт на повторное согласование); Approve — решение согласующего текущего шага
/// (согласовать, отклонить, вернуть); Price — финансист уточняет цены при согласовании; Issue — выдача со склада; Purchase — «Куплено»
/// (снабжение, по утверждённой заявке, пока есть что купить).
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
    bool Order = false,
    bool Price = false,
    bool Purchase = false);

/// <summary>Заказ поставщику по заявке (в карточке заявки); Direct — покупка «Куплено»: кто купил, когда и на какую сумму по этой заявке.</summary>
public sealed record ProcurementRequestOrder(
    Guid Id,
    string Number,
    ProcurementOrderStatus Status,
    string SupplierName,
    decimal Total,
    DateOnly? ExpectedDate,
    bool Direct = false,
    DateTimeOffset? DeliveredAt = null,
    string? ResponsibleName = null);

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
    IReadOnlyList<ProcurementRequestOrder>? Orders = null,
    IReadOnlyList<ProcurementRouteStep>? Route = null,
    decimal DirectorLimit = ProcurementSettings.DefaultDirectorLimit,
    IReadOnlyList<ProcurementRequestTender>? Tenders = null,
    bool RawRoute = false,
    ProcurementRequestKind Kind = ProcurementRequestKind.Regular);

/// <summary>Тендерная закупка, исполняющая заявку (в карточке заявки).</summary>
public sealed record ProcurementRequestTender(Guid Id, string Number, string Title, ProcurementTenderStatus Status, decimal Total, string SupplierName);

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
/// согласовании (директору), заявки к выдаче со склада (кладовщику). Quality — лаборатория (проверки, спецификации), DecidesQuality —
/// назначен решать по качеству партий. CanCreateRaw — может создавать заявки «Закупка сырья» (финансист, аналитик, снабжение).
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
    int ToReceive = 0,
    bool Decides = false,
    bool Quality = false,
    bool DecidesQuality = false,
    bool CanCreateRaw = false);

/// <summary>
/// Раздел «Заявки» (снабжение): заявки на закупку с позициями, категориями, приоритетом, вложениями и историей; проверка склада, резерв,
/// согласование директором, выдача со склада. Сотрудник видит свои заявки, руководитель отдела — отправленные заявки отдела, снабжение и
/// директор — все отправленные, кладовщик — те, где есть часть «со склада». Черновик видит только инициатор. Доступ решается здесь по
/// правам, отделу и роли «Директор» из базы — никогда по параметрам запроса. Каждое изменение пишется в историю в той же записи.
/// </summary>
public sealed partial class ProcurementRequestService(IAppDbContext db, IProcurementNumbers numbers, UserNotifier notifier, ProcurementStockLedger ledger, IAuditLogger audit)
{
    private const string NotVisible = "Эту заявку видят её инициатор, руководитель отдела и снабжение.";

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<ProcurementContext> ContextAsync(Guid userId, IReadOnlySet<string> permissions, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => new
            {
                u.DepartmentId, u.IsActive, Director = u.IsActive && u.Roles.Any(r => r.Role.Name == SystemRoles.Director),
                Analyst = u.IsActive && u.Roles.Any(r => r.Role.Name == SystemRoles.AnalystRole),
            })
            .FirstOrDefaultAsync(ct);
        var heads = await db.WorkDepartmentHeads.AsNoTracking().Where(h => h.UserId == userId).Select(h => h.DepartmentId).ToListAsync(ct);
        var keeps = await db.ProcurementWarehouseKeepers.AsNoTracking().Where(k => k.UserId == userId).Select(k => k.WarehouseId).ToListAsync(ct);
        var quality = await db.ProcurementQualityApprovers.AsNoTracking().AnyAsync(a => a.UserId == userId, ct);
        var route = await RouteSettingsAsync(ct);
        var active = user?.IsActive ?? false;
        var director = user?.Director ?? false;
        // Руководитель снабжения маршрута сырья — выбранный в настройках; пока не выбран — руководители отдела «Снабжение».
        var supply = route.Supply is { } s ? s == userId : heads.Count > 0 && await db.Departments.AnyAsync(x => x.Code == ProcurementPeople.SupplyDepartment && heads.Contains(x.Id), ct);
        // Директор маршрута — выбранный в настройках; пока не выбран — любой с ролью «Директор». Решает по качеству — только назначенный.
        // Аналитик — роль «Аналитик» (шаг маршрута заявок на сырьё).
        return new ProcurementContext(userId, permissions, user?.DepartmentId, heads.ToHashSet(), director,
            active && route.Finance == userId, route.Director is { } d ? active && d == userId : director, keeps.ToHashSet(), active && quality, active && supply,
            user?.Analyst ?? false);
    }

    /// <summary>
    /// Маршрут согласования из «Настройки → Отделы → Снабжение»: финансист, директор и руководитель снабжения прежнего маршрута сырья
    /// (отключённые — как не выбранные), лимит без директора, шаги маршрута заявок «Закупка сырья» по автору.
    /// </summary>
    private sealed record RouteSettings(Guid? Finance, Guid? Director, decimal Limit, Guid? Supply = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? RawSteps = null)
    {
        public IReadOnlyDictionary<string, IReadOnlyList<string>> Raw => RawSteps ?? ProcurementRawRoute.Default;
    }

    private async Task<RouteSettings> RouteSettingsAsync(CancellationToken ct)
    {
        var s = await db.ProcurementSettings.AsNoTracking()
            .Select(x => new
            {
                x.FinanceApproverId, x.DirectorApproverId, x.DirectorLimit, x.SupplyApproverId, x.RawRouteSteps,
                FinanceActive = db.Users.Any(u => u.Id == x.FinanceApproverId && u.IsActive),
                DirectorActive = db.Users.Any(u => u.Id == x.DirectorApproverId && u.IsActive),
                SupplyActive = db.Users.Any(u => u.Id == x.SupplyApproverId && u.IsActive),
            })
            .FirstOrDefaultAsync(ct);
        return s is null
            ? new RouteSettings(null, null, ProcurementSettings.DefaultDirectorLimit)
            : new RouteSettings(s.FinanceActive ? s.FinanceApproverId : null, s.DirectorActive ? s.DirectorApproverId : null, s.DirectorLimit,
                s.SupplyActive ? s.SupplyApproverId : null, ProcurementRawRoute.Parse(s.RawRouteSteps));
    }

    /// <summary>Кто согласует шаг «руководитель снабжения»: выбранный в настройках, а пока не выбран — руководители отдела «Снабжение».</summary>
    private async Task<List<Guid>> SupplyApproversAsync(RouteSettings route, CancellationToken ct) =>
        route.Supply is { } s ? [s] : await ProcurementPeople.SupplyHeadsAsync(db, ct);

    /// <summary>Аналитики — пользователи с ролью «Аналитик», кроме автора заявки (свою заявку не согласует).</summary>
    private Task<List<Guid>> AnalystsAsync(Guid initiatorId, CancellationToken ct) =>
        db.Users.AsNoTracking().Where(u => u.IsActive && u.Id != initiatorId && u.Roles.Any(x => x.Role.Name == SystemRoles.AnalystRole))
            .Select(u => u.Id).ToListAsync(ct);

    /// <summary>Автор заявки — директор маршрута (выбранный, а пока не выбран — с ролью «Директор»): шаг директора в маршруте сырья ему не нужен.</summary>
    private async Task<bool> InitiatorDirectsAsync(ProcurementRequest r, RouteSettings route, CancellationToken ct) =>
        route.Director is { } d ? d == r.InitiatorId : (await DirectorsAsync(ct)).Contains(r.InitiatorId);

    /// <summary>
    /// Состав по типу заявки: в заявке на сырьё — только позиции категорий сырья, в обычной — без них (сырьё закупается отдельной заявкой).
    /// Текст ошибки или null.
    /// </summary>
    private static string? KindError(ProcurementRequestKind kind, IEnumerable<(int LineNo, string Name, ProcurementCategory? Category)> items)
    {
        foreach (var (line, name, category) in items)
        {
            var raw = category?.IsProduction ?? false;
            if (kind == ProcurementRequestKind.Raw && !raw)
            {
                return $"Позиция {line} «{name}»: категория «{category?.Name}» — не сырьё. В заявке на закупку сырья — только позиции категорий сырья; остальное оформите обычной заявкой.";
            }

            if (kind == ProcurementRequestKind.Regular && raw)
            {
                return $"Позиция {line} «{name}»: категория «{category!.Name}» — сырьё. Сырьё закупается отдельной заявкой «Закупка сырья» — её создают финансист, аналитик или снабжение.";
            }
        }

        return null;
    }

    private static void RequireRawAuthor(ProcurementContext ctx)
    {
        if (!ctx.CanCreateRaw)
        {
            throw new FieldForbiddenException("Заявки на закупку сырья создают финансист, аналитик (роль «Аналитик») и снабжение.");
        }
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
        if (ctx.ReadsAll || ctx.IsDirector || ctx.Approves)
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
        var awaiting = await db.ProcurementRequests.AwaitingDecision(ctx).CountAsync(ct)
                       + (ctx.IsDirector ? await db.ProcurementPurchaseOrders.CountAsync(o => o.Status == ProcurementOrderStatus.AwaitingApproval, ct) : 0);
        var toIssue = ctx.Warehouse
            ? await db.ProcurementStockReservations.Where(x => x.Status == ProcurementReservationStatus.Active).Select(x => x.RequestId).Distinct().CountAsync(ct)
            : 0;
        var receivable = ProcurementOrderRules.Receivable;
        var toReceive = ctx.Warehouse ? await db.ProcurementPurchaseOrders.CountAsync(o => receivable.Contains(o.Status), ct) : 0;
        return new ProcurementNav(ctx.CanCreate, ctx.ReadsAll, ctx.Manages, ctx.Configures, ctx.HeadOf.Count > 0, revisions + queue + awaiting, ctx.IsDirector,
            ctx.Warehouse, ctx.SeesStock, awaiting, toIssue, toReceive, ProcurementRules.DecidesAny(ctx), ctx.Quality, ctx.DecidesQuality, ctx.CanCreate && ctx.CanCreateRaw);
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
        var heads = ctx.HeadOf.ToArray();
        var finance = ctx.IsFinancier;
        var director = ctx.ApprovesAsDirector;
        var supply = ctx.ApprovesAsSupply;
        var analyst = ctx.IsAnalyst;
        var action = visible.Where(r => (r.InitiatorId == me && (r.Status == ProcurementRequestStatus.Draft || r.Status == ProcurementRequestStatus.RevisionRequired))
                                        || (manages && r.Status == ProcurementRequestStatus.Submitted)
                                        || (r.Status == ProcurementRequestStatus.HeadApproval && heads.Contains(r.DepartmentId))
                                        || (finance && r.Status == ProcurementRequestStatus.FinanceApproval)
                                        || (analyst && r.Status == ProcurementRequestStatus.AnalystApproval && r.InitiatorId != me)
                                        || (supply && r.Status == ProcurementRequestStatus.SupplyApproval)
                                        || (director && r.Status == ProcurementRequestStatus.DirectorApproval));
        var needs = await RowsAsync(ctx, action
            .OrderBy(r => r.Status == ProcurementRequestStatus.HeadApproval || r.Status == ProcurementRequestStatus.FinanceApproval
                          || r.Status == ProcurementRequestStatus.SupplyApproval || r.Status == ProcurementRequestStatus.DirectorApproval
                          || r.Status == ProcurementRequestStatus.AnalystApproval ? 0
                : r.Status == ProcurementRequestStatus.RevisionRequired ? 1 : r.Status == ProcurementRequestStatus.Submitted ? 2 : 3)
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
                q = q.AwaitingDecision(ctx);
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
        // Очередь согласования — сначала давно ждущие (с момента прихода на шаг).
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
                r.PurchaseTotal, r.ApprovalRound, r.ForwardedAt, r.Reason, r.Kind,
            })
            .ToListAsync(ct);
        return rows.Select(r => new ProcurementRequestRow(r.Id, r.Number, r.Title, r.Status, r.Priority, r.DepartmentId, r.DepartmentCode, r.DepartmentName,
                r.InitiatorId, r.Initiator, r.Items, r.First, r.Categories.Order().ToList(), r.EstimatedTotal, r.DesiredDate, r.CreatedAt, r.SubmittedAt,
                r.Attachments, r.InitiatorId == ctx.UserId, r.PurchaseTotal, r.ApprovalRound, r.ForwardedAt, r.Reason, r.Kind))
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
            .Select(i => new { Item = i, Category = i.Category!.Name, Sku = i.Item == null ? null : i.Item.Sku, Tracks = i.Item != null && i.Item.TracksBatches, ShelfLife = i.Item == null ? null : i.Item.ShelfLifeDays })
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
        var approvals = await db.ProcurementApprovals.AsNoTracking().Where(a => a.RequestId == id).OrderBy(a => a.Round).ThenBy(a => a.CreatedAt)
            .Select(a => new ProcurementApprovalView(a.Id, a.Round, a.Decision, a.Comment, a.Decider!.FullName, a.PurchaseTotal, a.CreatedAt, a.Stage))
            .ToListAsync(ct);
        var approvedBy = r.ApprovedById is { } by ? await db.Users.AsNoTracking().Where(u => u.Id == by).Select(u => u.FullName).FirstOrDefaultAsync(ct) : null;
        var route = await RouteSettingsAsync(ct);
        var buyer = ctx.ReadsAll || ctx.IsDirector;
        var offers = buyer ? await db.ProcurementOffers.CountAsync(o => o.RequestId == id, ct) : 0;
        var orders = buyer
            ? await db.ProcurementPurchaseOrders.AsNoTracking().Where(o => o.Items.Any(i => i.RequestItem!.RequestId == id)).OrderBy(o => o.CreatedAt)
                .Select(o => new ProcurementRequestOrder(o.Id, o.Number, o.Status, o.Supplier!.Name, o.Total, o.ExpectedDate, o.Direct, o.DeliveredAt, o.Responsible!.FullName))
                .ToListAsync(ct)
            : [];
        var tenders = buyer
            ? await db.ProcurementTenders.AsNoTracking().Where(t => t.RequestId == id).OrderBy(t => t.TenderDate)
                .Select(t => new ProcurementRequestTender(t.Id, t.Number, t.Title, t.Status, t.Total, t.Supplier!.Name)).ToListAsync(ct)
            : [];
        var toOrder = items.Any(x => x.Item.Status != ProcurementItemStatus.Cancelled && x.Item.ToPurchase > x.Item.OrderedQty);

        var owner = ProcurementRules.IsOwner(ctx, r);
        var approvedEdit = ctx.Manages && ProcurementRules.ApprovedEditable(r.Status);
        var hasPurchase = items.Any(x => x.Item.Status != ProcurementItemStatus.Cancelled && x.Item.ToPurchase > 0);
        var decides = ProcurementRules.Decides(ctx, r);
        bool Can(ProcurementRequestStatus to) => ProcurementRules.CanTransition(r.Status, to, owner, ctx.Manages, decides);
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
            ctx.Manages && toOrder && ProcurementOrderService.Orderable.Contains(r.Status),
            decides && r.Status == ProcurementRequestStatus.FinanceApproval && hasPurchase,
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
                x.Item.ItemId is { } itemId ? available.GetValueOrDefault(itemId) : null, reserved.GetValueOrDefault(x.Item.Id), x.Item.OrderedQty, x.Item.ReceivedQty, x.Tracks, x.ShelfLife)).ToList(),
            docs.Select(x => new ProcurementDocumentView(x.Doc.Id, x.Doc.FileName, x.Doc.ContentType, x.Doc.SizeBytes, x.By, x.Doc.CreatedAt,
                CanDeleteDocument(ctx, r, x.Doc))).ToList(),
            history.Select(x => new ProcurementEventView(x.Event.Id, x.Event.Action, x.Event.Text, x.Actor ?? "Система", x.Event.FromStatus, x.Event.ToStatus,
                x.Event.Comment, x.Event.CreatedAt)).ToList(),
            can, r.PurchaseTotal, r.ApprovalRound, r.ApprovedAt, approvedBy, approvals, offers, orders,
            await RouteAsync(r, approvals, hasPurchase, route, ct), route.Limit, tenders, r.RawRoute, r.Kind);
    }

    /// <summary>
    /// Маршрут заявки для карточки (текущий раунд): руководитель отдела, финансист, директор, снабжение — кто решил, кто решает, какие шаги не
    /// нужны и почему; заявка «Закупка сырья» — шаги, с которыми её отправили (аналитик, финансист, директор), и снабжение; прежний маршрут
    /// сырья — руководитель снабжения, директор, снабжение. Заявки прежнего маршрута (у снабжения, переданные директору снабжением) и
    /// отменённые — без маршрута (null).
    /// </summary>
    private async Task<List<ProcurementRouteStep>?> RouteAsync(ProcurementRequest r, IReadOnlyList<ProcurementApprovalView> approvals, bool hasPurchase, RouteSettings route,
        CancellationToken ct)
    {
        var round = approvals.Where(a => a.Round == Math.Max(r.ApprovalRound, 1)).ToList();
        var newFlow = round.Any(a => a.Stage != ProcurementApprovalStages.Director) || r.RawRoute
                      || r.Status is ProcurementRequestStatus.Draft or ProcurementRequestStatus.RevisionRequired or ProcurementRequestStatus.HeadApproval
                          or ProcurementRequestStatus.FinanceApproval or ProcurementRequestStatus.SupplyApproval or ProcurementRequestStatus.AnalystApproval
                      || (r.ApprovalRound > 0 && round.Count == 0 && r.Status is not (ProcurementRequestStatus.Submitted or ProcurementRequestStatus.DirectorApproval));
        if (!newFlow || r.Status == ProcurementRequestStatus.Cancelled)
        {
            return null;
        }

        var heads = await db.WorkDepartmentHeads.AsNoTracking().Where(h => h.DepartmentId == r.DepartmentId && h.User!.IsActive)
            .Select(h => new { h.UserId, h.User!.FullName }).ToListAsync(ct);
        var ids = new[] { route.Finance, route.Director }.OfType<Guid>().ToList();
        var names = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        var directors = route.Director is { } d
            ? [names.GetValueOrDefault(d, "—")]
            : await db.Users.AsNoTracking().Where(u => u.IsActive && u.Roles.Any(x => x.Role.Name == SystemRoles.Director)).Select(u => u.FullName).ToListAsync(ct);
        var finance = route.Finance is { } f ? new List<string> { names.GetValueOrDefault(f, "—") } : [];
        var closed = r.Status is ProcurementRequestStatus.Rejected;

        ProcurementRouteStep Step(string key, ProcurementRequestStatus step, bool needed, string? reason, IReadOnlyList<string> who)
        {
            if (round.LastOrDefault(a => a.Stage == key) is { } decision)
            {
                var state = decision.Decision switch
                {
                    ProcurementApprovalDecision.Approved => "done",
                    ProcurementApprovalDecision.Rejected => "rejected",
                    _ => "returned",
                };
                return new ProcurementRouteStep(key, state, [decision.DeciderName], decision.CreatedAt, decision.Comment);
            }

            if (!needed)
            {
                return new ProcurementRouteStep(key, "skipped", [], Reason: reason);
            }

            return new ProcurementRouteStep(key, r.Status == step ? "current" : closed ? "skipped" : "pending", who);
        }

        var initiatorHeads = heads.Any(h => h.UserId == r.InitiatorId);
        var needsDirector = hasPurchase && (r.RawRoute || r.PurchaseTotal > route.Limit);
        var supplyState = r.Status switch
        {
            ProcurementRequestStatus.Fulfilled => "done",
            ProcurementRequestStatus.Approved or ProcurementRequestStatus.PartiallyFulfilled => "current",
            _ => closed ? "skipped" : "pending",
        };
        if (r.RouteSteps is { } routeSteps)
        {
            // Заявка «Закупка сырья»: шаги, с которыми её отправили; директор — не если автор сам директор маршрута, «если больше лимита» — по сумме.
            var analystIds = await AnalystsAsync(r.InitiatorId, ct);
            var analysts = await db.Users.AsNoTracking().Where(u => analystIds.Contains(u.Id)).OrderBy(u => u.FullName).Select(u => u.FullName).ToListAsync(ct);
            var initiatorDirects = await InitiatorDirectsAsync(r, route, ct);
            var overLimit = hasPurchase && r.PurchaseTotal > route.Limit;
            return
            [
                .. ProcurementRawRoute.Split(routeSteps).Select(step => step switch
                {
                    ProcurementRawRoute.Analyst => Step(ProcurementApprovalStages.Analyst, ProcurementRequestStatus.AnalystApproval, true, null, analysts),
                    ProcurementRawRoute.Finance => Step(ProcurementApprovalStages.Finance, ProcurementRequestStatus.FinanceApproval, true, null, finance),
                    ProcurementRawRoute.Director => Step(ProcurementApprovalStages.Director, ProcurementRequestStatus.DirectorApproval, !initiatorDirects,
                        "initiatorDirector", directors),
                    _ => Step(ProcurementApprovalStages.Director, ProcurementRequestStatus.DirectorApproval, overLimit && !initiatorDirects,
                        initiatorDirects ? "initiatorDirector" : hasPurchase ? "underLimit" : "noPurchase", directors),
                }),
                new ProcurementRouteStep("supply", supplyState, []),
            ];
        }

        if (r.RawRoute)
        {
            // Прежний маршрут сырья: руководитель снабжения (заявку самого руководителя снабжения — пропуск), директор всегда, снабжение.
            var approvers = await SupplyApproversAsync(route, ct);
            var supplyNames = await db.Users.AsNoTracking().Where(u => approvers.Contains(u.Id)).Select(u => u.FullName).ToListAsync(ct);
            var initiatorSupply = approvers.Contains(r.InitiatorId);
            return
            [
                Step(ProcurementApprovalStages.SupplyHead, ProcurementRequestStatus.SupplyApproval, hasPurchase && !initiatorSupply,
                    initiatorSupply ? "initiatorSupply" : "noPurchase", supplyNames),
                Step(ProcurementApprovalStages.Director, ProcurementRequestStatus.DirectorApproval, needsDirector, "noPurchase", directors),
                new ProcurementRouteStep("supply", supplyState, []),
            ];
        }

        return
        [
            Step(ProcurementApprovalStages.Head, ProcurementRequestStatus.HeadApproval, heads.Count > 0 && !initiatorHeads, initiatorHeads ? "initiatorHead" : "noHeads",
                heads.Select(h => h.FullName).ToList()),
            Step(ProcurementApprovalStages.Finance, ProcurementRequestStatus.FinanceApproval, hasPurchase, "noPurchase", finance),
            Step(ProcurementApprovalStages.Director, ProcurementRequestStatus.DirectorApproval, needsDirector, hasPurchase ? "underLimit" : "noPurchase", directors),
            new ProcurementRouteStep("supply", supplyState, []),
        ];
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

            var kind = input.Kind ?? ProcurementRequestKind.Regular;
            if (kind == ProcurementRequestKind.Raw)
            {
                RequireRawAuthor(ctx);
            }

            var department = await ResolveDepartmentAsync(ctx, input.DepartmentId, ct);
            var refs = await RefsAsync(input.Items, ct);
            var items = CheckItems(input.Items, refs, new Dictionary<Guid, ProcurementRequestItem>(), approvedEdit: false);
            if (KindError(kind, items.Select((d, i) => (i + 1, d.Name, refs.Categories.GetValueOrDefault(d.CategoryId)))) is { } wrong)
            {
                throw new FieldValidationException(wrong);
            }

            var (title, reason, comment) = CheckFields(input, null);
            var request = new ProcurementRequest
            {
                Number = await numbers.NextAsync(ProcurementRules.RequestPrefix, ct),
                Title = title,
                Kind = kind,
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
                $"Создана заявка{(kind == ProcurementRequestKind.Raw ? " на закупку сырья" : "")}: {Positions(items.Count)}{(request.EstimatedTotal > 0 ? $", ≈ {ProcurementRules.Money(request.EstimatedTotal)} сум" : "")}",
                null, null);
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
    /// финансисту на повторное согласование, остальное — только в историю. Submit — отправить после правки (инициатор).
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

        // Тип меняется, пока заявка в черновике или на доработке; утверждённая остаётся своего типа.
        var kind = input.Kind ?? r.Kind;
        if (approvedEdit && kind != r.Kind)
        {
            throw new FieldConflictException("Тип утверждённой заявки не меняется.");
        }

        if (kind == ProcurementRequestKind.Raw && ownEdit)
        {
            RequireRawAuthor(ctx);
        }

        var department = approvedEdit ? r.Department! : await ResolveDepartmentAsync(ctx, input.DepartmentId ?? r.DepartmentId, ct);
        var refs = await RefsAsync(input.Items, ct);
        var existing = r.Items.ToDictionary(i => i.Id);
        var items = CheckItems(input.Items, refs, existing, approvedEdit);
        // Состав — по типу; в утверждённой заявке проверяются только новые позиции (прежние уже утверждены).
        if (KindError(kind, items.Select((d, i) => (i + 1, d.Name, refs.Categories.GetValueOrDefault(d.CategoryId))).Where((_, i) => !approvedEdit || items[i].Id is null))
            is { } wrong)
        {
            throw new FieldValidationException(wrong);
        }
        var (title, reason, comment) = approvedEdit ? (r.Title, r.Reason, Text(input.Comment, 2000, "Комментарий")) : CheckFields(input, r.DesiredDate);
        if (approvedEdit)
        {
            CheckFields(input with { Title = r.Title, Submit = false }, r.DesiredDate);
        }

        var before = Snapshot.Of(r, r.Department!.Name, r.Items, refs);

        if (kind != r.Kind)
        {
            Log(r, ctx.UserId, "kind", null, null, $"Тип заявки: {ProcurementRules.Label(r.Kind)} → {ProcurementRules.Label(kind)}", null, null);
            r.Kind = kind;
        }

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
            var route = await RouteSettingsAsync(ct);
            // Существенное изменение — снова к финансисту (новый раунд): он решит по новой сумме, нужен ли директор; заявка «Закупка сырья» —
            // с первого нужного шага её маршрута; прежний маршрут сырья — к руководителю снабжения, затем директору.
            var again = reasons.Count == 0 ? ProcurementRequestStatus.Approved
                : r.RouteSteps is { } steps
                    ? ProcurementRawRoute.Next(ProcurementRawRoute.Split(steps), null, r.PurchaseTotal, route.Limit, await InitiatorDirectsAsync(r, route, ct))
                    : ProcurementRules.ReapprovalStep(r.RawRoute);
            if (again != ProcurementRequestStatus.Approved)
            {
                var from = r.Status;
                r.Status = again;
                r.ApprovalRound++;
                r.ForwardedAt = Now;
                Log(r, ctx.UserId, "reapproval", from, r.Status,
                    $"Существенное изменение после утверждения — повторное согласование {By(ProcurementRules.Stage(again)!)}: {string.Join("; ", reasons)}",
                    null, diff.Value.Json);
                await NotifyAsync(ctx, r, from, r.Status, null, department.Name, route, ct);
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

    /// <summary>
    /// Отправить черновик или исправленную заявку (инициатор): часть «со склада» резервируется, заявка идёт на первый шаг маршрута —
    /// руководителю отдела или (заявка руководителя) финансисту; заявка «Закупка сырья» — на первый шаг маршрута сырья её автора.
    /// Повторная отправка — ничего не меняет.
    /// </summary>
    public Task SubmitAsync(ProcurementContext ctx, Guid id, CancellationToken ct) => ChangeAsync(ctx, id, ProcurementRequestStatus.Submitted, null, ct);

    /// <summary>Передать директору заявку прежнего шага «у снабжения» (снабжение) — для заявок, отправленных до нового маршрута.</summary>
    public Task ForwardAsync(ProcurementContext ctx, Guid id, CancellationToken ct) => ChangeAsync(ctx, id, ProcurementRequestStatus.DirectorApproval, null, ct);

    /// <summary>Вернуть на доработку с обязательным комментарием — согласующий текущего шага (заявку прежнего шага — снабжение). Резерв снимается.</summary>
    public Task ReturnAsync(ProcurementContext ctx, Guid id, string? comment, CancellationToken ct) =>
        ChangeAsync(ctx, id, ProcurementRequestStatus.RevisionRequired, comment, ct);

    /// <summary>Отменить заявку до утверждения (инициатор); комментарий — по желанию. Резерв снимается.</summary>
    public Task CancelAsync(ProcurementContext ctx, Guid id, string? comment, CancellationToken ct) =>
        ChangeAsync(ctx, id, ProcurementRequestStatus.Cancelled, comment, ct);

    /// <summary>
    /// Согласовать текущий шаг — его согласующий (руководитель отдела, финансист, директор маршрута); комментарий — по желанию. Финансист может
    /// уточнить цены позиций (prices) — по новой сумме решается, нужен ли директор. Дальше — следующий шаг или снабжение.
    /// </summary>
    public Task ApproveAsync(ProcurementContext ctx, Guid id, string? comment, CancellationToken ct, IReadOnlyList<ProcurementPriceInput>? prices = null) =>
        ChangeAsync(ctx, id, ProcurementRequestStatus.Approved, comment, ct, prices);

    /// <summary>Отклонить заявку — согласующий текущего шага, с обязательным комментарием. Резерв снимается.</summary>
    public Task RejectAsync(ProcurementContext ctx, Guid id, string? comment, CancellationToken ct) =>
        ChangeAsync(ctx, id, ProcurementRequestStatus.Rejected, comment, ct);

    private Task ChangeAsync(ProcurementContext ctx, Guid id, ProcurementRequestStatus to, string? comment, CancellationToken ct,
        IReadOnlyList<ProcurementPriceInput>? prices = null) => ledger.RetryAsync(async () =>
    {
        var r = await RequireTrackedAsync(ctx, id, ct);
        if (r.Status == to)
        {
            return true; // двойное нажатие: заявка уже в этом статусе
        }

        // Двойное нажатие «Отправить»: заявка уже ушла по маршруту (на согласовании или утверждена) — второй отправки нет.
        if (to == ProcurementRequestStatus.Submitted && ProcurementRules.IsOwner(ctx, r) && r.Status is ProcurementRequestStatus.HeadApproval
                or ProcurementRequestStatus.FinanceApproval or ProcurementRequestStatus.SupplyApproval or ProcurementRequestStatus.DirectorApproval
                or ProcurementRequestStatus.AnalystApproval or ProcurementRequestStatus.Approved)
        {
            return true;
        }

        // Двойное нажатие «Согласовать»: шаг уже согласован этим же человеком и заявка ушла дальше — второго решения нет.
        if (to == ProcurementRequestStatus.Approved && !ProcurementRules.Decides(ctx, r)
            && await db.ProcurementApprovals.AnyAsync(a => a.RequestId == r.Id && a.Round == r.ApprovalRound && a.DeciderId == ctx.UserId
                                                          && a.Decision == ProcurementApprovalDecision.Approved, ct))
        {
            return true;
        }

        await MoveAsync(ctx, r, to, comment, r.Department!.Name, ct, prices);
        await db.SaveChangesAsync(ct);
        return true;
    });

    /// <summary>
    /// Переход статуса с проверкой прав и комментария, резервом или его снятием, решением согласующего, историей и уведомлениями — сохраняет
    /// вызывающий вместе с остальными изменениями. Submitted — «отправить»: шаг выбирает маршрут (FirstStep), а заявка, где всё со склада и
    /// руководитель не нужен, утверждается сама. Approved — «согласовать шаг»: дальше — следующий шаг (NextStep) или снабжение.
    /// </summary>
    private async Task MoveAsync(ProcurementContext ctx, ProcurementRequest r, ProcurementRequestStatus to, string? comment, string departmentName, CancellationToken ct,
        IReadOnlyList<ProcurementPriceInput>? prices = null)
    {
        var from = r.Status;
        var owner = ProcurementRules.IsOwner(ctx, r);
        if (!ProcurementRules.CanTransition(from, to, owner, ctx.Manages, ProcurementRules.Decides(ctx, r)))
        {
            if (ProcurementRules.CanTransition(from, to, true, true, true))
            {
                throw new FieldForbiddenException((from, to) switch
                {
                    (_, ProcurementRequestStatus.Cancelled) or (_, ProcurementRequestStatus.Submitted) => "Это может сделать только инициатор заявки.",
                    (ProcurementRequestStatus.HeadApproval, _) => $"Согласовать, отклонить или вернуть заявку на этом шаге может руководитель отдела «{departmentName}».",
                    (ProcurementRequestStatus.FinanceApproval, _) => "Согласовать, отклонить или вернуть заявку на этом шаге может финансист — его выбирают в «Настройки → Отделы → Снабжение».",
                    (ProcurementRequestStatus.SupplyApproval, _) =>
                        "Согласовать, отклонить или вернуть заявку на этом шаге может руководитель снабжения — его выбирают в «Настройки → Отделы → Снабжение».",
                    (ProcurementRequestStatus.AnalystApproval, _) =>
                        "Согласовать, отклонить или вернуть заявку на этом шаге может аналитик (роль «Аналитик»); свою заявку аналитик не согласует.",
                    (ProcurementRequestStatus.DirectorApproval, _) => "Согласовать, отклонить или вернуть заявку на этом шаге может директор.",
                    (_, ProcurementRequestStatus.DirectorApproval) => "Передать заявку директору может снабжение.",
                    _ => "Вернуть заявку на доработку может снабжение.",
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

            // Тип и состав — и при отправке: права автора и флажок «сырьё» у категорий могли измениться после создания.
            if (r.Kind == ProcurementRequestKind.Raw)
            {
                RequireRawAuthor(ctx);
            }

            var categoryIds = active.Select(i => i.CategoryId).Distinct().ToList();
            var categories = await db.ProcurementCategories.AsNoTracking().Where(c => categoryIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
            if (KindError(r.Kind, active.OrderBy(i => i.LineNo).Select(i => (i.LineNo, i.Name, categories.GetValueOrDefault(i.CategoryId)))) is { } wrong)
            {
                throw new FieldValidationException(wrong);
            }
        }

        if (to == ProcurementRequestStatus.DirectorApproval && active.All(i => i.ToPurchase <= 0))
        {
            throw new FieldConflictException("В заявке нет позиций к закупке — всё выдаётся со склада, согласование директора не нужно.");
        }

        if (to == ProcurementRequestStatus.Approved && from == ProcurementRequestStatus.FinanceApproval && prices is { Count: > 0 })
        {
            ApplyPrices(ctx, r, prices);
        }

        var now = Now;
        var target = to;
        var stage = ProcurementRules.Stage(from);
        var route = await RouteSettingsAsync(ct);
        string action, text;
        switch (to)
        {
            case ProcurementRequestStatus.Submitted:
                var reservedText = await ReserveAsync(r, ct);
                // Цена обязательна у части к закупке — после резерва: позиции без каталога резерв переводит целиком в закупку.
                if (ProcurementRules.MissingPrice(active) is { } missing)
                {
                    throw new FieldValidationException(missing);
                }

                Totals(r);
                r.SubmittedAt = now;
                r.ApprovalRound++;
                // Маршрут выбирается при каждой отправке: заявка «Закупка сырья» — шаги из настроек по автору (запоминаются в заявке),
                // обычная — руководитель отдела, финансист, директор по лимиту.
                var raw = r.Kind == ProcurementRequestKind.Raw;
                r.RawRoute = raw;
                if (raw)
                {
                    var steps = route.Raw[ProcurementRawRoute.Author(ctx)!];
                    r.RouteSteps = ProcurementRawRoute.Join(steps);
                    target = ProcurementRawRoute.Next(steps, null, r.PurchaseTotal, route.Limit, await InitiatorDirectsAsync(r, route, ct));
                }
                else
                {
                    r.RouteSteps = null;
                    var heads = await HeadsAsync(r.DepartmentId, ct);
                    target = ProcurementRules.FirstStep(heads.Contains(r.InitiatorId), heads.Count > 0, active.Any(i => i.ToPurchase > 0));
                }

                action = from == ProcurementRequestStatus.RevisionRequired ? "resubmitted" : "submitted";
                text = (from == ProcurementRequestStatus.RevisionRequired ? "Исправленная заявка отправлена" : "Заявка отправлена") + target switch
                {
                    ProcurementRequestStatus.HeadApproval => " на согласование руководителю отдела",
                    ProcurementRequestStatus.AnalystApproval => " на согласование аналитику",
                    ProcurementRequestStatus.FinanceApproval => " на согласование финансисту",
                    ProcurementRequestStatus.DirectorApproval => " на согласование директору",
                    _ => "",
                } + (raw ? " (закупка сырья)" : "");
                if (target == ProcurementRequestStatus.Approved)
                {
                    Approve(ctx, r, now, byUser: false);
                }
                else
                {
                    r.ForwardedAt = now;
                }

                if (reservedText is not null)
                {
                    text += $"; зарезервировано на складе: {reservedText}";
                }

                break;
            case ProcurementRequestStatus.Approved:
                Decide(ctx, r, stage!, ProcurementApprovalDecision.Approved, note);
                // Заявка «Закупка сырья» — следующий нужный шаг её маршрута, иначе — обычный маршрут (и прежний маршрут сырья).
                var rawSteps = r.RouteSteps is { } stored ? ProcurementRawRoute.Split(stored) : null;
                target = rawSteps is not null
                    ? ProcurementRawRoute.Next(rawSteps, from, r.PurchaseTotal, route.Limit, await InitiatorDirectsAsync(r, route, ct))
                    : ProcurementRules.NextStep(from, active.Any(i => i.ToPurchase > 0), r.PurchaseTotal, route.Limit);
                action = target == ProcurementRequestStatus.Approved ? "approved" : "step_approved";
                var who = By(stage!);
                if (target == ProcurementRequestStatus.Approved)
                {
                    Approve(ctx, r, now, byUser: true);
                    text = $"{(stage == ProcurementApprovalStages.Director ? "Утверждена" : "Согласована")} {who}: к закупке ≈ {ProcurementRules.Money(r.PurchaseTotal)} сум — передана в снабжение";
                }
                else if (rawSteps is not null)
                {
                    r.ForwardedAt = now;
                    text = target switch
                    {
                        ProcurementRequestStatus.AnalystApproval => $"Согласована {who} — передана аналитику",
                        ProcurementRequestStatus.FinanceApproval => $"Согласована {who} — передана финансисту",
                        _ when rawSteps.Contains(ProcurementRawRoute.DirectorOverLimit) =>
                            $"Согласована {who}: к закупке ≈ {ProcurementRules.Money(r.PurchaseTotal)} сум — больше лимита {ProcurementRules.Money(route.Limit)} сум, передана директору",
                        _ => $"Согласована {who}: к закупке ≈ {ProcurementRules.Money(r.PurchaseTotal)} сум — передана директору",
                    };
                }
                else
                {
                    r.ForwardedAt = now;
                    text = target != ProcurementRequestStatus.DirectorApproval
                        ? $"Согласована {who} — передана финансисту"
                        : from == ProcurementRequestStatus.SupplyApproval
                            ? $"Согласована {who}: к закупке ≈ {ProcurementRules.Money(r.PurchaseTotal)} сум — закупка сырья, передана директору"
                            : $"Согласована {who}: к закупке ≈ {ProcurementRules.Money(r.PurchaseTotal)} сум — больше лимита {ProcurementRules.Money(route.Limit)} сум, передана директору";
                }

                break;
            case ProcurementRequestStatus.Rejected:
                await ReleaseAsync(r, ct);
                CancelOpenItems(r);
                Decide(ctx, r, stage!, ProcurementApprovalDecision.Rejected, note);
                (action, text) = ("rejected", $"Отклонена {By(stage!)}");
                break;
            case ProcurementRequestStatus.RevisionRequired:
                await ReleaseAsync(r, ct);
                if (stage is not null)
                {
                    Decide(ctx, r, stage, ProcurementApprovalDecision.Revision, note);
                }

                (action, text) = ("returned", stage is null ? "Возвращена на доработку" : $"Возвращена на доработку {By(stage)}");
                break;
            case ProcurementRequestStatus.Cancelled:
                await ReleaseAsync(r, ct);
                r.CancelledAt = now;
                CancelOpenItems(r);
                (action, text) = ("cancelled", "Заявка отменена");
                break;
            case ProcurementRequestStatus.DirectorApproval:
                // Заявка прежнего шага «у снабжения»: снабжение передаёт её директору.
                r.ForwardedAt = now;
                r.ApprovalRound++;
                (action, text) = ("forwarded", r.ApprovalRound > 1 ? $"Передана директору на согласование (раунд {r.ApprovalRound})" : "Передана директору на согласование");
                break;
            default:
                (action, text) = ("status", $"Статус: {ProcurementRules.Label(from)} → {ProcurementRules.Label(to)}");
                break;
        }

        r.Status = target;
        r.UpdatedAt = now;
        Refresh(r);
        // Всё со склада и руководитель не нужен (заявка на сырьё — шаги её маршрута не нужны по сумме): в истории — отправка и отдельно
        // «утверждена сама».
        var auto = to == ProcurementRequestStatus.Submitted && target == ProcurementRequestStatus.Approved;
        Log(r, ctx.UserId, action, from, auto ? ProcurementRequestStatus.Submitted : target, text, note, null);
        if (auto)
        {
            Log(r, ctx.UserId, "auto_approved", ProcurementRequestStatus.Submitted, ProcurementRequestStatus.Approved,
                r.RouteSteps is not null
                    ? "Шаги маршрута сырья не нужны (сумма к закупке в пределах лимита) — заявка утверждена без согласования"
                    : "Всё есть на складе — закупка не нужна, заявка утверждена без согласования", null, null);
        }

        await NotifyAsync(ctx, r, from, target, note, departmentName, route, ct);
    }

    /// <summary>Заявка утверждена — передана в снабжение: кто и когда (null — утверждена сама: всё со склада), снимок утверждённых позиций.</summary>
    private static void Approve(ProcurementContext ctx, ProcurementRequest r, DateTimeOffset now, bool byUser)
    {
        r.ApprovedAt = now;
        r.ApprovedById = byUser ? ctx.UserId : null;
        r.ApprovedSnapshot = JsonSerializer.Serialize(Lines(r), Json);
    }

    /// <summary>«руководителем отдела», «финансистом», «директором» — кто решил на шаге.</summary>
    private static string By(string stage) => stage switch
    {
        ProcurementApprovalStages.Head => "руководителем отдела",
        ProcurementApprovalStages.Finance => "финансистом",
        ProcurementApprovalStages.SupplyHead => "руководителем снабжения",
        ProcurementApprovalStages.Analyst => "аналитиком",
        _ => "директором",
    };

    /// <summary>
    /// Финансист уточняет цены позиций к закупке при согласовании: цена больше нуля, до 100 млрд, два знака; позиция — этой заявки. Изменения —
    /// в историю; суммы пересчитываются (по ним решается, нужен ли директор).
    /// </summary>
    private void ApplyPrices(ProcurementContext ctx, ProcurementRequest r, IReadOnlyList<ProcurementPriceInput> prices)
    {
        var changes = new List<string>();
        foreach (var input in prices)
        {
            var item = r.Items.FirstOrDefault(i => i.Id == input.ItemId && i.Status != ProcurementItemStatus.Cancelled)
                       ?? throw new FieldValidationException("Позиция не найдена в заявке — обновите страницу.");
            if (input.Price <= 0 || ProcurementRules.CheckNumbers(item.Quantity, input.Price) is not null)
            {
                throw new FieldValidationException($"Позиция {item.LineNo} «{item.Name}»: цена — больше нуля, до 100 млрд, не больше двух знаков после запятой.");
            }

            if (item.EstimatedPrice == input.Price)
            {
                continue;
            }

            changes.Add($"«{item.Name}»: {(item.EstimatedPrice is { } p ? ProcurementRules.Money(p) : "—")} → {ProcurementRules.Money(input.Price)}");
            item.EstimatedPrice = input.Price;
            item.UpdatedAt = Now;
        }

        if (changes.Count == 0)
        {
            return;
        }

        var before = r.PurchaseTotal;
        Totals(r);
        Log(r, ctx.UserId, "prices", null, null,
            $"Финансист уточнил цены: {string.Join("; ", changes)}; к закупке {ProcurementRules.Money(before)} → {ProcurementRules.Money(r.PurchaseTotal)} сум", null, null);
    }

    /// <summary>Решение согласующего: запись шага раунда со снимком позиций и суммой к закупке.</summary>
    private void Decide(ProcurementContext ctx, ProcurementRequest r, string stage, ProcurementApprovalDecision decision, string? comment) =>
        db.ProcurementApprovals.Add(new ProcurementApproval
        {
            RequestId = r.Id,
            Round = Math.Max(r.ApprovalRound, 1),
            Stage = stage,
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
        // Не резервируются партии, которые не выдать: просроченные, на контроле качества, забракованные и заблокированные.
        var unusable = await ledger.UnusableAsync(warehouse, catalog.Select(i => i.ItemId!.Value), ct);
        var reserved = new List<string>();
        foreach (var item in catalog)
        {
            var balance = balances[item.ItemId!.Value];
            var available = balance.OnHand - balance.Reserved - unusable.GetValueOrDefault(item.ItemId.Value);

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

    /// <summary>
    /// Уведомления по переходу: согласующему нового шага — «ждёт вашего согласования», инициатору — где заявка сейчас; утверждённая — снабжению;
    /// отклонённая, возвращённая — инициатору; отменённая — согласующим шага, где она была. Финансист не выбран — тем, кто настраивает
    /// снабжение. Часть «со склада» стала к выдаче — кладовщику.
    /// </summary>
    private async Task NotifyAsync(ProcurementContext ctx, ProcurementRequest r, ProcurementRequestStatus from, ProcurementRequestStatus to, string? comment,
        string departmentName, RouteSettings route, CancellationToken ct)
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
            case ProcurementRequestStatus.HeadApproval or ProcurementRequestStatus.FinanceApproval or ProcurementRequestStatus.SupplyApproval
                or ProcurementRequestStatus.DirectorApproval or ProcurementRequestStatus.AnalystApproval:
                var approvers = await ApproversAsync(r, to, route, ct);
                var again = from is ProcurementRequestStatus.Approved or ProcurementRequestStatus.PartiallyFulfilled;
                Send(approvers, UserNotificationKind.ProcurementRequest,
                    again ? $"Повторное согласование: {r.Number} — {r.Title}" : $"Заявка ждёт вашего согласования: {r.Number} — {r.Title}", summary);
                if (to == ProcurementRequestStatus.FinanceApproval && approvers.Count == 0)
                {
                    Send(await RoleUsersAsync(Permissions.ProcurementSettings, ct), UserNotificationKind.ProcurementStatus,
                        $"Заявка {r.Number} ждёт финансиста, а он не выбран", "Выберите финансиста в «Настройки → Отделы → Снабжение».");
                }

                if (to == ProcurementRequestStatus.AnalystApproval && approvers.Count == 0)
                {
                    Send(await RoleUsersAsync(Permissions.ProcurementSettings, ct), UserNotificationKind.ProcurementStatus,
                        $"Заявка {r.Number} ждёт аналитика, а аналитиков нет",
                        "Назначьте роль «Аналитик» в «Настройки → Пользователи» (свою заявку аналитик не согласует).");
                }

                if (to == ProcurementRequestStatus.SupplyApproval && approvers.Count == 0)
                {
                    Send(await RoleUsersAsync(Permissions.ProcurementSettings, ct), UserNotificationKind.ProcurementStatus,
                        $"Заявка {r.Number} ждёт руководителя снабжения, а он не выбран",
                        "Выберите руководителя снабжения в «Настройки → Отделы → Снабжение» или назначьте руководителя отдела «Снабжение».");
                }

                Send([r.InitiatorId], UserNotificationKind.ProcurementStatus, to switch
                {
                    ProcurementRequestStatus.HeadApproval => $"Заявка {r.Number} на согласовании у руководителя отдела",
                    ProcurementRequestStatus.FinanceApproval => $"Заявка {r.Number} на согласовании у финансиста",
                    ProcurementRequestStatus.AnalystApproval when !again => $"Заявка {r.Number} на согласовании у аналитика",
                    ProcurementRequestStatus.SupplyApproval when !again => $"Заявка {r.Number} на согласовании у руководителя снабжения",
                    _ => again ? $"Заявка {r.Number} изменена и снова на согласовании" : $"Заявка {r.Number} на согласовании у директора",
                }, r.Title);
                break;
            case ProcurementRequestStatus.Approved when from != ProcurementRequestStatus.Draft && from != ProcurementRequestStatus.RevisionRequired:
                Send([r.InitiatorId], UserNotificationKind.ProcurementStatus, $"Заявка {r.Number} согласована — передана в снабжение", comment ?? r.Title);
                Send(await RoleUsersAsync(Permissions.ProcurementManage, ct), UserNotificationKind.ProcurementRequest, $"Согласована к закупке: {r.Number} — {r.Title}", summary);
                // Прежний маршрут сырья: финансист не согласует, но узнаёт об утверждённой закупке (D4). В маршруте заявок «Закупка сырья» он —
                // шаг (или автор).
                if (r.RawRoute && r.RouteSteps is null && route.Finance is { } financier)
                {
                    Send([financier], UserNotificationKind.ProcurementStatus, $"Утверждена закупка сырья: {r.Number} — {r.Title}", summary);
                }

                break;
            case ProcurementRequestStatus.Approved:
                notifier.Notify(r.InitiatorId == ctx.UserId ? null : r.InitiatorId, UserNotificationKind.ProcurementStatus,
                    $"Заявка {r.Number} утверждена: всё есть на складе", r.Title, link);
                break;
            case ProcurementRequestStatus.Rejected:
                Send([r.InitiatorId], UserNotificationKind.ProcurementStatus, $"Заявка {r.Number} отклонена", comment);
                break;
            case ProcurementRequestStatus.RevisionRequired:
                Send([r.InitiatorId], UserNotificationKind.ProcurementStatus, $"Заявка {r.Number} возвращена на доработку", comment);
                break;
            case ProcurementRequestStatus.Cancelled when from != ProcurementRequestStatus.Draft:
                var notify = from == ProcurementRequestStatus.Submitted ? await RoleUsersAsync(Permissions.ProcurementManage, ct) : await ApproversAsync(r, from, route, ct);
                Send(notify, UserNotificationKind.ProcurementStatus, $"Заявка {r.Number} отменена инициатором", comment ?? r.Title);
                break;
        }

        // Часть «со склада» стала к выдаче (руководитель согласовал или заявка утверждена) — кладовщику.
        if (r.UsesStock && ProcurementRules.Issuable(to) && !ProcurementRules.Issuable(from))
        {
            Send(await RoleUsersAsync(Permissions.ProcurementWarehouse, ct), UserNotificationKind.ProcurementRequest, $"К выдаче со склада: {r.Number} — {r.Title}", summary);
        }
    }

    /// <summary>
    /// Кто решает шаг: руководители отдела заявки; финансист маршрута; аналитики (роль «Аналитик», кроме автора); директор маршрута (не выбран —
    /// все с ролью «Директор»).
    /// </summary>
    private async Task<List<Guid>> ApproversAsync(ProcurementRequest r, ProcurementRequestStatus step, RouteSettings route, CancellationToken ct) => step switch
    {
        ProcurementRequestStatus.HeadApproval => await HeadsAsync(r.DepartmentId, ct),
        ProcurementRequestStatus.FinanceApproval => route.Finance is { } f ? [f] : [],
        ProcurementRequestStatus.AnalystApproval => await AnalystsAsync(r.InitiatorId, ct),
        ProcurementRequestStatus.SupplyApproval => await SupplyApproversAsync(route, ct),
        ProcurementRequestStatus.DirectorApproval => route.Director is { } d ? [d] : await DirectorsAsync(ct),
        _ => [],
    };

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
