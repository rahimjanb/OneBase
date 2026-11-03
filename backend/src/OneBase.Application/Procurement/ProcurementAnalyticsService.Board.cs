using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.Application.Field;
using OneBase.Application.Security;
using OneBase.Domain.Procurement;

namespace OneBase.Application.Procurement;

/// <summary>Что пользователю показать и разрешить на обзоре снабжения.</summary>
public sealed record ProcurementBoardAccess(
    bool CanApprove,
    bool CanCreateRequest,
    bool CanCreateOrder,
    bool CanManageSuppliers,
    bool CanSeeTeam,
    bool SeesRequests,
    bool SeesMetrics);

/// <summary>Итог шапки: заявки на согласовании директора (число и сумма к закупке), просроченные поставки и задачи снабжения; null — не видно по роли.</summary>
public sealed record ProcurementBoardSummary(int? PendingApprovalCount, decimal? PendingApprovalAmount, int? OverdueDeliveries, int? OverdueTasks);

/// <summary>
/// Путь закупки сейчас — три этапа: заявки на согласовании (руководитель отдела, финансист, директор: число и сумма к закупке); к закупке —
/// утверждённые заявки, по которым снабжению осталось что-то купить (число, остаток по согласованным ценам, сколько из них с прошедшей
/// желаемой датой); на складе — заявки, товар по которым ждёт выдачи (резерв). Этапы заявок — null, если пользователь не видит все
/// заявки. Заказы поставщикам до отправки, в пути и ждущие приёмки — прежний путь (заказы больше не оформляются, но старые доводятся).
/// </summary>
public sealed record ProcurementPipeline(
    int? RequestsAwaitingOrder,
    int? OnApprovalCount,
    decimal? OnApprovalAmount,
    int OrdersInProgress,
    int InTransit,
    int AwaitingReceipt,
    decimal? ToBuyAmount = null,
    int? OverdueToBuy = null,
    int ToIssue = 0);

public sealed record ProcurementApprovalBrief(Guid Id, string Number, string Title, decimal Amount, string Department, string Initiator, DateTimeOffset? SubmittedAt);

public sealed record ProcurementUpcomingDelivery(Guid Id, string Number, string SupplierName, DateOnly ExpectedDate);

/// <summary>Показатели периода: закуплено и поступило (как в блоке директора) и KPI (как в отчёте KPI).</summary>
public sealed record ProcurementBoardMetrics(decimal PurchasedAmount, int OrdersCount, decimal ReceivedAmount, ProcurementKpis Kpis);

/// <summary>Поставщик в аналитике: заказов за период и рейтинг за всё время (в срок %, претензии, общая оценка).</summary>
public sealed record ProcurementSupplierStat(Guid Id, string Name, int Orders, decimal Total, decimal? OnTime, int Claims, int? Rating);

public sealed record ProcurementBoardAnalytics(
    IReadOnlyList<ProcurementAmount> Monthly,
    IReadOnlyList<ProcurementAmount> ByDepartment,
    IReadOnlyList<ProcurementAmount> ByCategory,
    IReadOnlyList<ProcurementSupplierStat> Suppliers);

/// <summary>
/// Сотрудник снабжения: открытые задачи, заказы в работе, просрочено (задачи + заказы), нагрузка и её уровень (free, low, high), куплено за
/// период (покупки «Куплено» и поставленные заказы).
/// </summary>
public sealed record ProcurementTeamRow(Guid UserId, string Name, string Role, int Tasks, int Orders, int Overdue, int Load, string LoadLevel, int Bought = 0);

/// <summary>
/// Обзор снабжения: что требует действия, путь закупки, показатели и аналитика периода, сотрудники — по правам роли. MyApprovalCount —
/// сколько заявок ждут решения самого пользователя (Approvals — первые три из них).
/// </summary>
public sealed record ProcurementBoard(
    DateOnly From,
    DateOnly To,
    ProcurementBoardAccess Access,
    ProcurementBoardSummary Summary,
    ProcurementPipeline Pipeline,
    IReadOnlyList<ProcurementApprovalBrief>? Approvals,
    int OrdersOnApproval,
    IReadOnlyList<ProcurementUpcomingDelivery>? Upcoming,
    ProcurementBoardMetrics? Metrics,
    ProcurementBoardAnalytics? Analytics,
    IReadOnlyList<ProcurementTeamRow>? Team,
    IReadOnlyList<ProcurementDigestRow>? Digests,
    bool Empty,
    IReadOnlyList<string> Errors,
    int MyApprovalCount = 0);

public sealed partial class ProcurementAnalyticsService
{
    /// <summary>Нагрузка сотрудника (задачи + заказы в работе), с которой она «высокая».</summary>
    public const int HighLoad = 6;

    /// <summary>
    /// Обзор снабжения одним запросом. Расчёты — те же, что в блоках директора и руководителя снабжения и в KPI; новое — только счёт этапов пути
    /// закупки. Видят снабжение, директор и кладовщик; заявки, показатели и аналитику — снабжение и директор; согласовать — директор (роль);
    /// всех сотрудников — директор и руководитель снабжения, остальные из снабжения — себя. Сбой одного блока не ломает остальные: блок
    /// приходит null, его код — в Errors.
    /// </summary>
    public async Task<ProcurementBoard> BoardAsync(ProcurementContext ctx, ProcurementFilter f, CancellationToken ct)
    {
        if (!ctx.SeesStock && !ctx.IsDirector)
        {
            throw new FieldForbiddenException("Обзор снабжения видят снабжение, директор и кладовщик.");
        }

        var head = await SupplyHeadAsync(ctx, ct);
        var seesRequests = ctx.ReadsAll || ctx.IsDirector;
        // Согласовать из обзора может тот, кто решает шаг маршрута (руководитель отдела, финансист, директор).
        var access = new ProcurementBoardAccess(ProcurementRules.DecidesAny(ctx), ctx.CanCreate, ctx.Manages, ctx.Manages, ctx.IsDirector || ctx.Configures || head, seesRequests,
            seesRequests);
        var errors = new List<string>();

        async Task<T?> Safe<T>(string block, Func<Task<T>> load) where T : class
        {
            try
            {
                return await load();
            }
            catch (Exception ex) when (ex is not OperationCanceledException && ex is not FieldForbiddenException)
            {
                logger.LogError(ex, "Снабжение: блок обзора {Block} не построен", block);
                errors.Add(block);
                return null;
            }
        }

        var today = FieldClock.Today;
        var live = ProcurementOrderRules.Live;
        var director = seesRequests ? await Safe("metrics", () => DirectorAsync(f, ct)) : null;
        var headBoard = seesRequests ? await Safe("team", () => HeadAsync(f, ct)) : null;
        var overdueDeliveries = await db.ProcurementPurchaseOrders.AsNoTracking()
            .CountAsync(o => live.Contains(o.Status) && o.ExpectedDate != null && o.ExpectedDate < today, ct);

        var prepared = new[] { ProcurementOrderStatus.Draft, ProcurementOrderStatus.AwaitingApproval, ProcurementOrderStatus.Approved };
        var inProgress = await db.ProcurementPurchaseOrders.AsNoTracking().CountAsync(o => prepared.Contains(o.Status), ct);
        var transit = await db.ProcurementPurchaseOrders.AsNoTracking()
            .Where(o => o.Status == ProcurementOrderStatus.Ordered || o.Status == ProcurementOrderStatus.PartiallyDelivered)
            .Select(o => new
            {
                o.Id,
                o.Number,
                Supplier = o.Supplier!.Name,
                o.ExpectedDate,
                Draft = db.ProcurementStockDocuments.Any(d => d.OrderId == o.Id && d.Status == ProcurementStockDocumentStatus.Draft),
            })
            .ToListAsync(ct);
        var awaitingReceipt = transit.Count(o => o.Draft || (o.ExpectedDate is { } d && d <= today));
        var toBuy = seesRequests ? await Safe("pipeline", () => ToBuyAsync(today, ct)) : null;
        var issuable = ProcurementRules.IssuableStatuses;
        var toIssue = await db.ProcurementStockReservations.AsNoTracking()
            .Where(x => x.Status == ProcurementReservationStatus.Active && x.Quantity > x.Issued
                        && db.ProcurementRequests.Any(r => r.Id == x.RequestId && issuable.Contains(r.Status)))
            .Select(x => x.RequestId).Distinct().CountAsync(ct);
        var pipeline = new ProcurementPipeline(
            toBuy?.Count ?? headBoard?.WaitingOrder,
            director?.AwaitingRequests,
            director?.AwaitingRequestsTotal,
            inProgress,
            transit.Count - awaitingReceipt,
            awaitingReceipt,
            toBuy?.Amount,
            toBuy?.Overdue,
            toIssue);

        // «Ждёт согласования» — заявки, которые ждут решения пользователя на его шаге маршрута (руководитель отдела, финансист, директор).
        var myApprovals = ProcurementRules.DecidesAny(ctx) ? await db.ProcurementRequests.AsNoTracking().AwaitingDecision(ctx).CountAsync(ct) : 0;
        var approvals = ProcurementRules.DecidesAny(ctx)
            ? await Safe("approvals", () => db.ProcurementRequests.AsNoTracking().AwaitingDecision(ctx)
                .OrderBy(r => r.ForwardedAt ?? r.SubmittedAt)
                .Take(3)
                .Select(r => new ProcurementApprovalBrief(r.Id, r.Number, r.Title, r.PurchaseTotal, r.Department!.Name, r.Initiator!.FullName, r.SubmittedAt))
                .ToListAsync(ct))
            : null;
        var upcoming = transit.Where(o => !o.Draft && o.ExpectedDate is { } d && d > today)
            .OrderBy(o => o.ExpectedDate)
            .Take(3)
            .Select(o => new ProcurementUpcomingDelivery(o.Id, o.Number, o.Supplier, o.ExpectedDate!.Value))
            .ToList();

        ProcurementBoardMetrics? metrics = null;
        ProcurementBoardAnalytics? analytics = null;
        if (director is not null)
        {
            var kpis = await Safe("metrics", () => KpisAsync(f, ct));
            metrics = kpis is null ? null : new ProcurementBoardMetrics(director.OrderedTotal, director.OrderedCount, director.ReceivedTotal, kpis);
            var supplierStats = await Safe("analytics", () => SupplierStatsAsync(f, ct));
            analytics = supplierStats is null ? null : new ProcurementBoardAnalytics(director.ByMonth, director.ByDepartment, director.ByCategory, supplierStats);
        }

        var team = headBoard is null ? null : await Safe("team", () => TeamAsync(ctx, headBoard, access.CanSeeTeam, ct));
        var digests = ctx.IsDirector || ctx.Configures || head ? await Safe("digests", () => DigestsAsync(3, ct)) : null;
        var empty = !await db.ProcurementRequests.AsNoTracking().AnyAsync(r => r.Status != ProcurementRequestStatus.Draft, ct)
                    && !await db.ProcurementPurchaseOrders.AsNoTracking().AnyAsync(ct);

        return new ProcurementBoard(
            f.From,
            f.To,
            access,
            new ProcurementBoardSummary(director?.AwaitingRequests, director?.AwaitingRequestsTotal, overdueDeliveries, access.CanSeeTeam ? headBoard?.OverdueTasks : null),
            pipeline,
            approvals,
            director?.AwaitingOrders ?? 0,
            upcoming,
            metrics,
            analytics,
            team,
            digests,
            empty,
            errors.Distinct().ToList(),
            myApprovals);
    }

    private sealed record ToBuyCount(int Count, decimal Amount, int Overdue);

    /// <summary>К закупке: утверждённые заявки с некупленным остатком — число, остаток по согласованным ценам, с прошедшей желаемой датой.</summary>
    private async Task<ToBuyCount> ToBuyAsync(DateOnly today, CancellationToken ct)
    {
        var orderable = ProcurementOrderService.Orderable;
        var rows = await db.ProcurementRequests.AsNoTracking()
            .Where(r => orderable.Contains(r.Status) && r.Items.Any(i => i.Status != ProcurementItemStatus.Cancelled && i.Quantity - i.FromStock > i.OrderedQty))
            .Select(r => new
            {
                r.DesiredDate,
                Left = r.Items.Where(i => i.Status != ProcurementItemStatus.Cancelled && i.Quantity - i.FromStock > i.OrderedQty && i.EstimatedPrice != null)
                    .Sum(i => (i.Quantity - i.FromStock - i.OrderedQty) * i.EstimatedPrice!.Value),
            })
            .ToListAsync(ct);
        return new ToBuyCount(rows.Count, Math.Round(rows.Sum(r => r.Left), 2), rows.Count(r => r.DesiredDate is { } d && d < today));
    }

    /// <summary>Поставщики с утверждёнными заказами за период — по сумме; рейтинг и его части — за всё время (как в карточке поставщика).</summary>
    private async Task<List<ProcurementSupplierStat>> SupplierStatsAsync(ProcurementFilter f, CancellationToken ct)
    {
        var placed = ProcurementOrderRules.Placed;
        var (from, to) = (f.FromUtc, f.ToUtc);
        var stats = await db.ProcurementPurchaseOrders.AsNoTracking()
            .Where(o => placed.Contains(o.Status) && o.ApprovedAt >= from && o.ApprovedAt < to)
            .GroupBy(o => new { o.SupplierId, o.Supplier!.Name })
            .Select(g => new { g.Key.SupplierId, g.Key.Name, Orders = g.Count(), Total = g.Sum(o => o.Total) })
            .OrderByDescending(s => s.Total)
            .Take(8)
            .ToListAsync(ct);
        var ratings = await suppliers.RatingsAsync(stats.Select(s => s.SupplierId).ToList(), ct);
        return stats.Select(s => new ProcurementSupplierStat(s.SupplierId, s.Name, s.Orders, Math.Round(s.Total, 2), ratings[s.SupplierId].OnTime,
            ratings[s.SupplierId].ClaimLines, ratings[s.SupplierId].Overall)).ToList();
    }

    /// <summary>Сотрудники снабжения из блока руководителя с ролью и уровнем нагрузки; без права видеть команду — только сам пользователь.</summary>
    private async Task<List<ProcurementTeamRow>> TeamAsync(ProcurementContext ctx, ProcurementHeadBoard board, bool all, CancellationToken ct)
    {
        var supplyDept = await db.Departments.AsNoTracking().Where(d => d.Code == ProcurementPeople.SupplyDepartment).Select(d => (Guid?)d.Id).FirstOrDefaultAsync(ct);
        var heads = supplyDept is { } dept
            ? (await db.WorkDepartmentHeads.AsNoTracking().Where(h => h.DepartmentId == dept).Select(h => h.UserId).ToListAsync(ct)).ToHashSet()
            : [];
        var keepers = (await ProcurementPeople.WithPermissionAsync(db, Permissions.ProcurementWarehouse, ct)).ToHashSet();
        return board.Staff
            .Where(s => all || s.UserId == ctx.UserId)
            .Select(s => new ProcurementTeamRow(
                s.UserId,
                s.Name,
                heads.Contains(s.UserId) ? "Руководитель снабжения" : keepers.Contains(s.UserId) ? "Кладовщик" : "Закупщик",
                s.OpenTasks,
                s.ActiveOrders,
                s.OverdueTasks + s.OverdueOrders,
                s.Load,
                s.Load == 0 ? "free" : s.Load >= HighLoad ? "high" : "low",
                s.Delivered))
            .ToList();
    }
}
