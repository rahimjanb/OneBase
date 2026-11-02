using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Security;
using OneBase.Application.Work;
using OneBase.Domain.Procurement;
using OneBase.Domain.Work;

namespace OneBase.Application.Procurement;

/// <summary>
/// Срез аналитики: период (даты по Ташкенту, включительно) и фильтры — отдел и категория (по позициям заявок), сотрудник (ответственный
/// за заказ), поставщик. Без периода — текущий месяц.
/// </summary>
public sealed record ProcurementFilter(DateOnly From, DateOnly To, Guid? DepartmentId = null, Guid? CategoryId = null, Guid? EmployeeId = null, Guid? SupplierId = null)
{
    public DateTimeOffset FromUtc => FieldClock.DayStartUtc(From);
    public DateTimeOffset ToUtc => FieldClock.DayStartUtc(To.AddDays(1));

    public static ProcurementFilter Of(DateOnly? from, DateOnly? to, Guid? department = null, Guid? category = null, Guid? employee = null, Guid? supplier = null)
    {
        var today = FieldClock.Today;
        var start = from ?? new DateOnly(today.Year, today.Month, 1);
        var end = to ?? today;
        if (end < start)
        {
            throw new FieldValidationException("Конец периода раньше начала.");
        }

        if (end.DayNumber - start.DayNumber > 3 * 366)
        {
            throw new FieldValidationException("Период — не длиннее трёх лет.");
        }

        return new ProcurementFilter(start, end, department, category, employee, supplier);
    }
}

/// <summary>
/// KPI снабжения за период — только из данных базы; нет данных — null (на экране «нет данных»). Среднее время закупки — от утверждения
/// заявки директором до поставки заказа целиком; в срок — заказ поставлен не позже ожидаемой даты; экономия — утверждённая цена в заявке
/// минус цена заказа (с НДС) по позициям, где цена в заявке была; выполненные заявки — перешедшие в «Выполнена» за период; претензии —
/// строки приёмок с браком или недостачей; качество поставщиков — средний рейтинг тех, у кого были закупки за период.
/// </summary>
public sealed record ProcurementKpis(
    decimal? AvgPurchaseDays,
    int PurchasesMeasured,
    decimal? OnTimePercent,
    int DeliveriesMeasured,
    decimal Savings,
    decimal? SavingsPercent,
    int SavingsLines,
    int FulfilledRequests,
    int Claims,
    decimal? SupplierQuality,
    int SuppliersRated);

/// <summary>Сумма по разрезу (отдел, категория, месяц, поставщик): ключ, подпись, сумма, сколько строк или заказов.</summary>
public sealed record ProcurementAmount(string Key, string Label, decimal Amount, int Count);

public sealed record ProcurementSupplierScore(Guid Id, string Name, int Rating, int Orders);

public sealed record ProcurementOrderBrief(Guid Id, string Number, string SupplierName, string ResponsibleName, ProcurementOrderStatus Status, DateOnly? ExpectedDate, decimal Total, bool Overdue);

public sealed record ProcurementDirectorBoard(
    decimal OrderedTotal,
    int OrderedCount,
    decimal ReceivedTotal,
    int AwaitingRequests,
    decimal AwaitingRequestsTotal,
    int AwaitingOrders,
    decimal AwaitingOrdersTotal,
    int ActiveOrders,
    decimal ActiveTotal,
    int OverdueOrders,
    IReadOnlyList<ProcurementOrderBrief> Overdue,
    IReadOnlyList<ProcurementAmount> ByDepartment,
    IReadOnlyList<ProcurementAmount> ByCategory,
    IReadOnlyList<ProcurementAmount> ByMonth,
    IReadOnlyList<ProcurementSupplierScore> Best,
    IReadOnlyList<ProcurementSupplierScore> Worst);

/// <summary>Сотрудник снабжения: задачи, заказы в работе и просрочки, поставлено за период, сроки (дни), нагрузка = открытые задачи + заказы в работе.</summary>
public sealed record ProcurementStaffRow(
    Guid UserId,
    string Name,
    int OpenTasks,
    int OverdueTasks,
    int ActiveOrders,
    int OverdueOrders,
    int Delivered,
    decimal? AvgToOrderDays,
    decimal? AvgDeliveryDays,
    int Load);

public sealed record ProcurementHeadBoard(
    int WaitingOrder,
    int OrdersInWork,
    int OverdueOrders,
    int OverdueTasks,
    decimal? AvgToOrderDays,
    decimal? AvgDeliveryDays,
    IReadOnlyList<ProcurementStaffRow> Staff);

public sealed record ProcurementDeadline(string Kind, Guid Id, string Title, DateOnly Due, bool Overdue, string Link);

public sealed record ProcurementBuyerBoard(
    int MyRequestsOpen,
    int MyOrders,
    decimal MyOrdersTotal,
    int MyTasks,
    int MyTasksOverdue,
    IReadOnlyList<ProcurementOrderBrief> Urgent,
    IReadOnlyList<ProcurementDeadline> Deadlines);

public sealed record ProcurementLowItem(Guid Id, string Sku, string Name, string UnitCode, decimal Available, decimal MinQty);

public sealed record ProcurementIssueBrief(Guid RequestId, string Number, string Title, string DepartmentName, int Lines);

public sealed record ProcurementWarehouseBoard(
    int Expected,
    int ExpectedSoon,
    int Drafts,
    int ReceiptsWeek,
    ProcurementStockSummary Stock,
    IReadOnlyList<ProcurementExpectedRow> ExpectedList,
    IReadOnlyList<ProcurementReceiptRow> Recent,
    IReadOnlyList<ProcurementLowItem> Low,
    IReadOnlyList<ProcurementIssueBrief> ToIssue);

public sealed record ProcurementDigestRow(Guid Id, DateOnly Day, string Title, string Summary, DateTimeOffset CreatedAt);

/// <summary>Обзор «Снабжения»: каждому — свои блоки по роли (директор, руководитель снабжения, сотрудник снабжения, кладовщик).</summary>
public sealed record ProcurementDashboard(
    DateOnly From,
    DateOnly To,
    ProcurementKpis? Kpis,
    ProcurementDirectorBoard? Director,
    ProcurementHeadBoard? Head,
    ProcurementBuyerBoard? Buyer,
    ProcurementWarehouseBoard? Warehouse,
    IReadOnlyList<ProcurementDigestRow>? Digests);

/// <summary>
/// Аналитика снабжения: обзор по ролям и KPI. Расходы — по закупкам (утверждённые заказы, дата — утверждение заказа) и по принятому на
/// склад; оплаты придут с модулем «Финансы». Суммы по отделам и категориям — по позициям заказов с НДС, без скидки и доставки.
/// </summary>
public sealed partial class ProcurementAnalyticsService(
    IAppDbContext db,
    ProcurementSupplierService suppliers,
    ProcurementReceiptService receipts,
    ProcurementStockService stock,
    Microsoft.Extensions.Logging.ILogger<ProcurementAnalyticsService> logger)
{
    /// <summary>Руководство закупок: директор (роль), руководитель снабжения, кто настраивает снабжение.</summary>
    public async Task<bool> LeaderAsync(ProcurementContext ctx, CancellationToken ct) =>
        ctx.IsDirector || ctx.Configures || await SupplyHeadAsync(ctx, ct);

    private async Task<bool> SupplyHeadAsync(ProcurementContext ctx, CancellationToken ct) =>
        ctx.HeadOf.Count > 0 && await db.Departments.AnyAsync(d => d.Code == ProcurementPeople.SupplyDepartment && ctx.HeadOf.Contains(d.Id), ct);

    public async Task<ProcurementDashboard> DashboardAsync(ProcurementContext ctx, ProcurementFilter f, CancellationToken ct)
    {
        if (!ctx.SeesStock && !ctx.IsDirector)
        {
            throw new FieldForbiddenException("Обзор снабжения видят снабжение, директор и кладовщик.");
        }

        var head = await SupplyHeadAsync(ctx, ct);
        var director = ctx.IsDirector || ctx.Configures;
        var buyer = ctx.ReadsAll || ctx.IsDirector;
        return new ProcurementDashboard(
            f.From,
            f.To,
            buyer ? await KpisAsync(f, ct) : null,
            director ? await DirectorAsync(f, ct) : null,
            director || head ? await HeadAsync(f, ct) : null,
            ctx.Manages ? await BuyerAsync(ctx, ct) : null,
            ctx.Warehouse ? await WarehouseAsync(ctx, ct) : null,
            director || head ? await DigestsAsync(5, ct) : null);
    }

    // ---------- Срезы ----------

    /// <summary>Позиции закупок за период: утверждённые заказы (дата — утверждение заказа) с фильтрами отдела, категории, сотрудника, поставщика.</summary>
    internal IQueryable<ProcurementPurchaseOrderItem> Lines(ProcurementFilter f)
    {
        var placed = ProcurementOrderRules.Placed;
        var (from, to) = (f.FromUtc, f.ToUtc);
        var q = db.ProcurementPurchaseOrderItems.AsNoTracking().Where(i => placed.Contains(i.Order!.Status) && i.Order.ApprovedAt >= from && i.Order.ApprovedAt < to);
        if (f.DepartmentId is { } department)
        {
            q = q.Where(i => i.RequestItem!.Request!.DepartmentId == department);
        }

        if (f.CategoryId is { } category)
        {
            q = q.Where(i => i.RequestItem!.CategoryId == category);
        }

        if (f.EmployeeId is { } employee)
        {
            q = q.Where(i => i.Order!.ResponsibleId == employee);
        }

        if (f.SupplierId is { } supplier)
        {
            q = q.Where(i => i.Order!.SupplierId == supplier);
        }

        return q;
    }

    /// <summary>Заказы с фильтрами (отдел и категория — есть такая позиция); без условия на дату и статус.</summary>
    internal IQueryable<ProcurementPurchaseOrder> Orders(ProcurementFilter f)
    {
        var q = db.ProcurementPurchaseOrders.AsNoTracking();
        if (f.DepartmentId is { } department)
        {
            q = q.Where(o => o.Items.Any(i => i.RequestItem!.Request!.DepartmentId == department));
        }

        if (f.CategoryId is { } category)
        {
            q = q.Where(o => o.Items.Any(i => i.RequestItem!.CategoryId == category));
        }

        if (f.EmployeeId is { } employee)
        {
            q = q.Where(o => o.ResponsibleId == employee);
        }

        if (f.SupplierId is { } supplier)
        {
            q = q.Where(o => o.SupplierId == supplier);
        }

        return q;
    }

    // ---------- KPI ----------

    public async Task<ProcurementKpis> KpisAsync(ProcurementFilter f, CancellationToken ct)
    {
        var (from, to) = (f.FromUtc, f.ToUtc);
        var delivered = await Orders(f).Where(o => o.DeliveredAt >= from && o.DeliveredAt < to)
            .Select(o => new { o.DeliveredAt, o.ExpectedDate, Approved = o.Items.Min(i => i.RequestItem!.Request!.ApprovedAt) })
            .ToListAsync(ct);
        var timed = delivered.Where(o => o.Approved != null).Select(o => (decimal)(o.DeliveredAt!.Value - o.Approved!.Value).TotalDays).ToList();
        var dated = delivered.Where(o => o.ExpectedDate != null).ToList();
        var onTime = dated.Count(o => FieldClock.LocalDate(o.DeliveredAt!.Value) <= o.ExpectedDate);

        var savings = await Lines(f).Where(i => i.RequestItem!.EstimatedPrice != null)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                Base = g.Sum(i => i.RequestItem!.EstimatedPrice!.Value * i.Quantity),
                Saved = g.Sum(i => (i.RequestItem!.EstimatedPrice!.Value - (i.Order!.PricesIncludeVat ? i.UnitPrice : i.UnitPrice * (1 + i.Order.VatRate / 100))) * i.Quantity),
            })
            .FirstOrDefaultAsync(ct);

        var fulfilledQ = db.ProcurementAuditLogs.AsNoTracking()
            .Where(e => e.DocType == ProcurementRules.RequestDoc && e.ToStatus == nameof(ProcurementRequestStatus.Fulfilled) && e.CreatedAt >= from && e.CreatedAt < to);
        if (f.DepartmentId is { } department)
        {
            fulfilledQ = fulfilledQ.Where(e => db.ProcurementRequests.Any(r => r.Id == e.DocId && r.DepartmentId == department));
        }

        var fulfilled = await fulfilledQ.Select(e => e.DocId).Distinct().CountAsync(ct);

        var claimsQ = db.ProcurementReceiptLines.AsNoTracking()
            .Where(l => l.ClaimStatus != ProcurementClaimStatus.None && l.Document!.PostedAt >= from && l.Document.PostedAt < to);
        if (f.DepartmentId is { } claimDepartment)
        {
            claimsQ = claimsQ.Where(l => l.OrderItem!.RequestItem!.Request!.DepartmentId == claimDepartment);
        }

        if (f.CategoryId is { } claimCategory)
        {
            claimsQ = claimsQ.Where(l => l.OrderItem!.RequestItem!.CategoryId == claimCategory);
        }

        if (f.SupplierId is { } supplier)
        {
            claimsQ = claimsQ.Where(l => l.Document!.Order!.SupplierId == supplier);
        }

        if (f.EmployeeId is { } employee)
        {
            claimsQ = claimsQ.Where(l => l.Document!.Order!.ResponsibleId == employee);
        }

        var claims = await claimsQ.CountAsync(ct);
        var supplierIds = await Lines(f).Select(i => i.Order!.SupplierId).Distinct().ToListAsync(ct);
        var ratings = (await suppliers.RatingsAsync(supplierIds, ct)).Values.Select(r => r.Overall).OfType<int>().ToList();

        return new ProcurementKpis(
            timed.Count == 0 ? null : Math.Round(timed.Average(), 1),
            timed.Count,
            dated.Count == 0 ? null : Math.Round(100m * onTime / dated.Count, 1),
            dated.Count,
            Math.Round(savings?.Saved ?? 0m, 2),
            savings is { Base: > 0 } s ? Math.Round(100m * s.Saved / s.Base, 1) : null,
            savings?.Count ?? 0,
            fulfilled,
            claims,
            ratings.Count == 0 ? null : Math.Round((decimal)ratings.Average(), 1),
            ratings.Count);
    }

    // ---------- Директор ----------

    private async Task<ProcurementDirectorBoard> DirectorAsync(ProcurementFilter f, CancellationToken ct)
    {
        var (from, to) = (f.FromUtc, f.ToUtc);
        var placed = ProcurementOrderRules.Placed;
        var live = ProcurementOrderRules.Live;
        var today = FieldClock.Today;
        var ordered = await db.ProcurementPurchaseOrders.AsNoTracking().Where(o => placed.Contains(o.Status) && o.ApprovedAt >= from && o.ApprovedAt < to)
            .GroupBy(_ => 1).Select(g => new { Count = g.Count(), Total = g.Sum(o => o.Total) }).FirstOrDefaultAsync(ct);
        var received = await db.ProcurementReceiptLines.AsNoTracking()
            .Where(l => l.Accepted > 0 && l.Document!.PostedAt >= from && l.Document.PostedAt < to)
            .SumAsync(l => (decimal?)(l.Accepted * (l.OrderItem!.Order!.PricesIncludeVat ? l.OrderItem.UnitPrice : l.OrderItem.UnitPrice * (1 + l.OrderItem.Order.VatRate / 100))), ct) ?? 0m;
        var awaitingRequests = await db.ProcurementRequests.AsNoTracking().Where(r => r.Status == ProcurementRequestStatus.DirectorApproval)
            .GroupBy(_ => 1).Select(g => new { Count = g.Count(), Total = g.Sum(r => r.PurchaseTotal) }).FirstOrDefaultAsync(ct);
        var awaitingOrders = await db.ProcurementPurchaseOrders.AsNoTracking().Where(o => o.Status == ProcurementOrderStatus.AwaitingApproval)
            .GroupBy(_ => 1).Select(g => new { Count = g.Count(), Total = g.Sum(o => o.Total) }).FirstOrDefaultAsync(ct);
        var active = await db.ProcurementPurchaseOrders.AsNoTracking().Where(o => live.Contains(o.Status))
            .GroupBy(_ => 1).Select(g => new { Count = g.Count(), Total = g.Sum(o => o.Total) }).FirstOrDefaultAsync(ct);
        var overdueQ = db.ProcurementPurchaseOrders.AsNoTracking().Where(o => live.Contains(o.Status) && o.ExpectedDate != null && o.ExpectedDate < today);
        var overdue = await overdueQ.OrderBy(o => o.ExpectedDate).Take(5)
            .Select(o => new ProcurementOrderBrief(o.Id, o.Number, o.Supplier!.Name, o.Responsible!.FullName, o.Status, o.ExpectedDate, o.Total, true))
            .ToListAsync(ct);

        var lines = Lines(f);
        var byDepartment = await lines.GroupBy(i => new { i.RequestItem!.Request!.DepartmentId, i.RequestItem.Request.Department!.Name })
            .Select(g => new
            {
                g.Key.DepartmentId, g.Key.Name, Count = g.Count(),
                Amount = g.Sum(i => i.Order!.PricesIncludeVat ? i.Quantity * i.UnitPrice : i.Quantity * i.UnitPrice * (1 + i.Order.VatRate / 100)),
            })
            .ToListAsync(ct);
        var byCategory = await lines.GroupBy(i => new { i.RequestItem!.CategoryId, i.RequestItem.Category!.Name })
            .Select(g => new
            {
                g.Key.CategoryId, g.Key.Name, Count = g.Count(),
                Amount = g.Sum(i => i.Order!.PricesIncludeVat ? i.Quantity * i.UnitPrice : i.Quantity * i.UnitPrice * (1 + i.Order.VatRate / 100)),
            })
            .ToListAsync(ct);

        // Динамика — 12 месяцев по месяц окончания периода включительно.
        var monthEnd = new DateOnly(f.To.Year, f.To.Month, 1);
        var monthStart = monthEnd.AddMonths(-11);
        var (mFrom, mTo) = (FieldClock.DayStartUtc(monthStart), FieldClock.DayStartUtc(monthEnd.AddMonths(1)));
        var monthly = await db.ProcurementPurchaseOrders.AsNoTracking().Where(o => placed.Contains(o.Status) && o.ApprovedAt >= mFrom && o.ApprovedAt < mTo)
            .Select(o => new { o.ApprovedAt, o.Total }).ToListAsync(ct);
        var byMonth = Enumerable.Range(0, 12).Select(n => monthStart.AddMonths(n)).Select(m =>
        {
            var rows = monthly.Where(o => FieldClock.LocalDate(o.ApprovedAt!.Value) is var d && d.Year == m.Year && d.Month == m.Month).ToList();
            return new ProcurementAmount($"{m:yyyy-MM}", $"{m:MM.yyyy}", rows.Sum(o => o.Total), rows.Count);
        }).ToList();

        var withOrders = await db.ProcurementPurchaseOrders.AsNoTracking().Where(o => placed.Contains(o.Status))
            .GroupBy(o => new { o.SupplierId, o.Supplier!.Name }).Select(g => new { g.Key.SupplierId, g.Key.Name, Orders = g.Count() }).ToListAsync(ct);
        var ratings = await suppliers.RatingsAsync(withOrders.Select(s => s.SupplierId).ToList(), ct);
        var scored = withOrders.Where(s => ratings[s.SupplierId].Overall is not null)
            .Select(s => new ProcurementSupplierScore(s.SupplierId, s.Name, ratings[s.SupplierId].Overall!.Value, s.Orders)).ToList();
        var best = scored.OrderByDescending(s => s.Rating).ThenByDescending(s => s.Orders).Take(3).ToList();
        var worst = scored.Where(s => best.All(b => b.Id != s.Id)).OrderBy(s => s.Rating).ThenByDescending(s => s.Orders).Take(3).ToList();

        return new ProcurementDirectorBoard(
            ordered?.Total ?? 0, ordered?.Count ?? 0, Math.Round(received, 2),
            awaitingRequests?.Count ?? 0, awaitingRequests?.Total ?? 0, awaitingOrders?.Count ?? 0, awaitingOrders?.Total ?? 0,
            active?.Count ?? 0, active?.Total ?? 0, await overdueQ.CountAsync(ct), overdue,
            byDepartment.OrderByDescending(x => x.Amount).Select(x => new ProcurementAmount(x.DepartmentId.ToString(), x.Name, Math.Round(x.Amount, 2), x.Count)).ToList(),
            byCategory.OrderByDescending(x => x.Amount).Select(x => new ProcurementAmount(x.CategoryId.ToString(), x.Name, Math.Round(x.Amount, 2), x.Count)).ToList(),
            byMonth, best, worst);
    }

    // ---------- Руководитель снабжения ----------

    internal async Task<ProcurementHeadBoard> HeadAsync(ProcurementFilter f, CancellationToken ct)
    {
        var (from, to) = (f.FromUtc, f.ToUtc);
        var today = FieldClock.Today;
        var live = ProcurementOrderRules.Live;
        var open = WorkTaskService.Open;
        var supplyDept = await db.Departments.AsNoTracking().Where(d => d.Code == ProcurementPeople.SupplyDepartment).Select(d => (Guid?)d.Id).FirstOrDefaultAsync(ct);
        var staffIds = await ProcurementPeople.WithPermissionAsync(db, Permissions.ProcurementManage, ct);
        var liveOrders = await db.ProcurementPurchaseOrders.AsNoTracking().Where(o => live.Contains(o.Status))
            .Select(o => new { o.ResponsibleId, Overdue = o.ExpectedDate != null && o.ExpectedDate < today }).ToListAsync(ct);
        var ordered = await db.ProcurementPurchaseOrders.AsNoTracking().Where(o => o.OrderedAt >= from && o.OrderedAt < to)
            .Select(o => new { o.ResponsibleId, o.OrderedAt, Approved = o.Items.Min(i => i.RequestItem!.Request!.ApprovedAt) }).ToListAsync(ct);
        var delivered = await db.ProcurementPurchaseOrders.AsNoTracking().Where(o => o.DeliveredAt >= from && o.DeliveredAt < to && o.OrderedAt != null)
            .Select(o => new { o.ResponsibleId, o.DeliveredAt, o.OrderedAt }).ToListAsync(ct);
        var tasks = supplyDept is { } dept
            ? await db.WorkTasks.AsNoTracking().Where(t => t.DepartmentId == dept && t.ArchivedAt == null && open.Contains(t.Status))
                .Select(t => new { t.AssigneeId, Overdue = t.DueDate != null && t.DueDate < today }).ToListAsync(ct)
            : [];
        var ids = staffIds.Concat(liveOrders.Select(o => o.ResponsibleId)).Concat(delivered.Select(o => o.ResponsibleId)).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        decimal? Avg(IEnumerable<double> days) => days.ToList() is { Count: > 0 } list ? Math.Round((decimal)list.Average(), 1) : null;
        var toOrder = ordered.Where(o => o.Approved != null).Select(o => (o.ResponsibleId, Days: (o.OrderedAt!.Value - o.Approved!.Value).TotalDays)).ToList();
        var toDeliver = delivered.Select(o => (o.ResponsibleId, Days: (o.DeliveredAt!.Value - o.OrderedAt!.Value).TotalDays)).ToList();
        var staff = ids.Where(names.ContainsKey).Select(id =>
            {
                var openTasks = tasks.Count(t => t.AssigneeId == id);
                var activeOrders = liveOrders.Count(o => o.ResponsibleId == id);
                return new ProcurementStaffRow(id, names[id], openTasks, tasks.Count(t => t.AssigneeId == id && t.Overdue), activeOrders,
                    liveOrders.Count(o => o.ResponsibleId == id && o.Overdue), delivered.Count(o => o.ResponsibleId == id),
                    Avg(toOrder.Where(x => x.ResponsibleId == id).Select(x => x.Days)), Avg(toDeliver.Where(x => x.ResponsibleId == id).Select(x => x.Days)),
                    openTasks + activeOrders);
            })
            .OrderByDescending(s => s.Load).ThenBy(s => s.Name).ToList();
        var orderable = ProcurementOrderService.Orderable;
        var waiting = await db.ProcurementRequests.AsNoTracking()
            .CountAsync(r => orderable.Contains(r.Status) && r.Items.Any(i => i.Status != ProcurementItemStatus.Cancelled && i.Quantity - i.FromStock > i.OrderedQty), ct);
        return new ProcurementHeadBoard(waiting, liveOrders.Count, liveOrders.Count(o => o.Overdue), tasks.Count(t => t.Overdue),
            Avg(toOrder.Select(x => x.Days)), Avg(toDeliver.Select(x => x.Days)), staff);
    }

    // ---------- Сотрудник снабжения ----------

    private async Task<ProcurementBuyerBoard> BuyerAsync(ProcurementContext ctx, CancellationToken ct)
    {
        var me = ctx.UserId;
        var today = FieldClock.Today;
        var soon = today.AddDays(2);
        var live = ProcurementOrderRules.Live;
        var open = WorkTaskService.Open;
        var done = new[] { ProcurementRequestStatus.Fulfilled, ProcurementRequestStatus.Cancelled, ProcurementRequestStatus.Rejected };
        var myRequests = await db.ProcurementRequests.AsNoTracking().CountAsync(r => r.InitiatorId == me && !done.Contains(r.Status), ct);
        var mine = await db.ProcurementPurchaseOrders.AsNoTracking().Where(o => o.ResponsibleId == me && live.Contains(o.Status))
            .Select(o => new ProcurementOrderBrief(o.Id, o.Number, o.Supplier!.Name, o.Responsible!.FullName, o.Status, o.ExpectedDate, o.Total,
                o.ExpectedDate != null && o.ExpectedDate < today))
            .ToListAsync(ct);
        var myTasks = await db.WorkTasks.AsNoTracking().Where(t => t.AssigneeId == me && t.ArchivedAt == null && open.Contains(t.Status))
            .Select(t => new { t.Id, t.Title, t.DueDate }).ToListAsync(ct);
        var urgent = mine.Where(o => o.Overdue || (o.ExpectedDate is { } d && d <= soon)).OrderBy(o => o.ExpectedDate).Take(8).ToList();
        var deadlines = myTasks.Where(t => t.DueDate != null)
            .Select(t => new ProcurementDeadline("task", t.Id, t.Title, t.DueDate!.Value, t.DueDate < today, $"/tasks/task/{t.Id}"))
            .Concat(mine.Where(o => o.ExpectedDate != null)
                .Select(o => new ProcurementDeadline("order", o.Id, $"{o.Number} — {o.SupplierName}", o.ExpectedDate!.Value, o.Overdue, $"/procurement/orders/{o.Id}")))
            .OrderBy(d => d.Due).Take(8).ToList();
        return new ProcurementBuyerBoard(myRequests, mine.Count, mine.Sum(o => o.Total), myTasks.Count, myTasks.Count(t => t.DueDate < today), urgent, deadlines);
    }

    // ---------- Кладовщик ----------

    private async Task<ProcurementWarehouseBoard> WarehouseAsync(ProcurementContext ctx, CancellationToken ct)
    {
        var today = FieldClock.Today;
        var expected = await receipts.ExpectedAsync(ctx, ct);
        var weekAgo = FieldClock.DayStartUtc(today.AddDays(-6));
        var drafts = await db.ProcurementStockDocuments.AsNoTracking().CountAsync(d => d.OrderId != null && d.Status == ProcurementStockDocumentStatus.Draft, ct);
        var week = await db.ProcurementStockDocuments.AsNoTracking().CountAsync(d => d.OrderId != null && d.Status == ProcurementStockDocumentStatus.Posted && d.PostedAt >= weekAgo, ct);
        var recent = await receipts.SearchAsync(ctx, new ProcurementReceiptSearch("posted", PageSize: 5), ct);
        var low = await db.ProcurementItems.AsNoTracking().Where(i => i.IsActive && i.MinQty > 0)
            .Select(i => new
            {
                i.Id, i.Sku, i.Name, i.UnitCode, i.MinQty,
                Available = db.ProcurementStockBalances.Where(b => b.ItemId == i.Id).Sum(b => (decimal?)(b.OnHand - b.Reserved)) ?? 0m,
            })
            .Where(i => i.Available < i.MinQty)
            .OrderBy(i => i.Available / i.MinQty).Take(5)
            .ToListAsync(ct);
        var toIssue = await stock.ToIssueAsync(ctx, ct);
        return new ProcurementWarehouseBoard(expected.Count, expected.Count(e => e.ExpectedDate is { } d && d <= today.AddDays(1)), drafts, week,
            await stock.SummaryAsync(ctx, ct), expected.Take(5).ToList(), recent.Items,
            low.Select(i => new ProcurementLowItem(i.Id, i.Sku, i.Name, i.UnitCode, i.Available, i.MinQty)).ToList(),
            toIssue.Take(5).Select(r => new ProcurementIssueBrief(r.RequestId, r.Number, r.Title, r.DepartmentName, r.Lines.Count)).ToList());
    }

    // ---------- Сводки ----------

    public Task<List<ProcurementDigestRow>> DigestsAsync(int take, CancellationToken ct) =>
        db.ProcurementDigests.AsNoTracking().OrderByDescending(d => d.Day).Take(take)
            .Select(d => new ProcurementDigestRow(d.Id, d.Day, d.Title, d.Summary, d.CreatedAt)).ToListAsync(ct);

    /// <summary>Журнал ежедневных сводок — руководству (директор, руководитель снабжения, кто настраивает снабжение).</summary>
    public async Task<PageResult<ProcurementDigestRow>> DigestPageAsync(ProcurementContext ctx, int page, CancellationToken ct)
    {
        await RequireLeaderAsync(ctx, ct);
        const int size = 30;
        page = Math.Max(page, 1);
        var total = await db.ProcurementDigests.CountAsync(ct);
        var items = await db.ProcurementDigests.AsNoTracking().OrderByDescending(d => d.Day).Skip((page - 1) * size).Take(size)
            .Select(d => new ProcurementDigestRow(d.Id, d.Day, d.Title, d.Summary, d.CreatedAt)).ToListAsync(ct);
        return new PageResult<ProcurementDigestRow>(items, total, page, size);
    }

    public async Task<ProcurementDigestView> DigestAsync(ProcurementContext ctx, Guid id, CancellationToken ct)
    {
        await RequireLeaderAsync(ctx, ct);
        var d = await db.ProcurementDigests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new FieldNotFoundException("Сводка не найдена.");
        var previous = await db.ProcurementDigests.AsNoTracking().Where(x => x.Day < d.Day).OrderByDescending(x => x.Day).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        var next = await db.ProcurementDigests.AsNoTracking().Where(x => x.Day > d.Day).OrderBy(x => x.Day).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        return new ProcurementDigestView(d.Id, d.Day, d.Title, d.Summary, d.CreatedAt, d.Recipients, ProcurementReminderService.Sections(d), previous, next);
    }

    private async Task RequireLeaderAsync(ProcurementContext ctx, CancellationToken ct)
    {
        if (!await LeaderAsync(ctx, ct))
        {
            throw new FieldForbiddenException("Сводки снабжения видят директор и руководитель снабжения.");
        }
    }
}

public sealed record ProcurementDigestView(
    Guid Id,
    DateOnly Day,
    string Title,
    string Summary,
    DateTimeOffset CreatedAt,
    int Recipients,
    IReadOnlyList<ProcurementDigestSection> Sections,
    Guid? PreviousId,
    Guid? NextId);
