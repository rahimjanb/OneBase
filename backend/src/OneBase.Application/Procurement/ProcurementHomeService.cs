using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Domain.Procurement;

namespace OneBase.Application.Procurement;

/// <summary>Кто пользователь в снабжении и производстве — какие блоки главной ему показывать.</summary>
public sealed record ProcurementHomeRoles(bool Warehouse, bool Quality, bool DecidesQuality, bool Supply, bool Analyst, bool Head, bool Director, bool Financier);

/// <summary>Проверка качества на главной: шаг, позиция, поставщик, поставка (товар ещё в машине) или партия, когда заведена, на шаге ли пользователя.</summary>
public sealed record ProcurementHomeCheck(
    Guid Id,
    string Number,
    ProcurementQualityCheckStatus Status,
    string ItemName,
    string? SupplierName,
    Guid? ArrivalId,
    string? ArrivalNumber,
    string? BatchNumber,
    decimal Received,
    string UnitCode,
    DateTimeOffset CreatedAt,
    bool AtMyStep);

/// <summary>Качество: открытых проверок по шагам, из них — по машинам (до склада), на шаге пользователя; самые давние.</summary>
public sealed record ProcurementHomeQuality(int Open, int Documents, int Lab, int Decision, int InTrucks, int AtMyStep, IReadOnlyList<ProcurementHomeCheck> Checks);

/// <summary>Склад: заявок к выдаче, позиций ниже минимума, партий — истекает срок, просрочено, на контроле.</summary>
public sealed record ProcurementHomeStock(int ToIssue, int BelowMinimum, int Expiring, int Expired, int OnControl);

/// <summary>
/// Заявки: ждут решения пользователя (число и первые), заявок отдела в работе и «можно получить» (руководителю), своих «можно получить»,
/// к закупке у снабжения (заявок и остаток по согласованным ценам).
/// </summary>
public sealed record ProcurementHomeRequests(
    int AwaitingMe,
    IReadOnlyList<ProcurementApprovalBrief> Approvals,
    int DepartmentOpen,
    int DepartmentToPickUp,
    int MineToPickUp,
    int? ToBuy,
    decimal? ToBuyAmount);

/// <summary>
/// Главная по ролям: ближайшие поставки (сколько всего, сегодня, просрочено), машины на приёмке, принято на склад за неделю (и на сколько),
/// качество (лаборатории и назначенным), склад (кладовщику), заявки (согласования, отдел, свои, к закупке). Блок null — не по роли.
/// </summary>
public sealed record ProcurementHomeView(
    ProcurementHomeRoles Roles,
    bool SeesDeliveries,
    IReadOnlyList<ProcurementExpectedDelivery> Upcoming,
    int UpcomingTotal,
    int DueToday,
    int Overdue,
    IReadOnlyList<ProcurementArrivalRow> Arrivals,
    IReadOnlyList<ProcurementArrivalRow> Accepted,
    int AcceptedCount,
    decimal AcceptedAmount,
    ProcurementHomeQuality? Quality,
    ProcurementHomeStock? Stock,
    ProcurementHomeRequests Requests);

/// <summary>
/// Данные главной для кладовщика, лаборатории, снабжения, аналитика, руководителей отделов, директора и финансиста — одним ответом, по
/// правам пользователя: каждый видит только то, что ему открыто в разделах (сервисы разделов проверяют то же).
/// </summary>
public sealed class ProcurementHomeService(
    IAppDbContext db,
    ProcurementStockLedger ledger,
    ProcurementArrivalService arrivals,
    ProcurementQualityService quality,
    ProcurementStockService stock,
    ProcurementBatchService batches,
    ProcurementPurchaseService purchases)
{
    /// <summary>Сколько поставок и проверок показывать в блоке.</summary>
    public const int Shown = 6;

    public async Task<ProcurementHomeView> GetAsync(ProcurementContext ctx, CancellationToken ct)
    {
        var roles = new ProcurementHomeRoles(ctx.Warehouse, ctx.Quality, ctx.DecidesQuality, ctx.Manages, ctx.IsAnalyst, ctx.HeadOf.Count > 0, ctx.IsDirector, ctx.IsFinancier);
        var sees = ProcurementArrivalService.Sees(ctx);
        IReadOnlyList<ProcurementExpectedDelivery> expected = [];
        IReadOnlyList<ProcurementArrivalRow> open = [];
        IReadOnlyList<ProcurementArrivalRow> accepted = [];
        var acceptedCount = 0;
        var acceptedAmount = 0m;
        if (sees)
        {
            expected = await arrivals.ExpectedAsync(ctx, null, 200, ct);
            open = (await arrivals.SearchAsync(ctx, new ProcurementArrivalSearch("open", PageSize: 50), ct)).Items;
            var since = DateTimeOffset.UtcNow.AddDays(-7);
            var week = db.ProcurementArrivals.AsNoTracking().Where(a => a.Status == ProcurementArrivalStatus.Accepted && a.AcceptedAt >= since);
            acceptedCount = await week.CountAsync(ct);
            acceptedAmount = acceptedCount == 0 ? 0m : await week.SumAsync(a => a.AcceptedTotal, ct);
            accepted = acceptedCount == 0
                ? []
                : (await arrivals.SearchAsync(ctx, new ProcurementArrivalSearch("accepted", PageSize: Shown), ct)).Items.Where(a => a.AcceptedAt >= since).ToList();
        }

        return new ProcurementHomeView(roles, sees, expected.Take(Shown).ToList(), expected.Count, expected.Count(d => d.DaysLeft == 0), expected.Count(d => d.Overdue), open,
            accepted, acceptedCount, Math.Round(acceptedAmount, 2), await QualityAsync(ctx, ct), await StockAsync(ctx, ct), await RequestsAsync(ctx, ct));
    }

    private async Task<ProcurementHomeQuality?> QualityAsync(ProcurementContext ctx, CancellationToken ct)
    {
        if (!ctx.Quality && !ctx.DecidesQuality)
        {
            return null;
        }

        var nav = await quality.NavAsync(ctx, ct);
        bool Mine(ProcurementQualityCheckStatus s) =>
            (ctx.Quality && s is ProcurementQualityCheckStatus.Documents or ProcurementQualityCheckStatus.Lab) || (ctx.DecidesQuality && s == ProcurementQualityCheckStatus.Decision);
        var rows = (await quality.ListAsync(ctx, new ProcurementQualitySearch("open", PageSize: 50), ct)).Items;
        var open = ProcurementQualityRules.Open;
        var inTrucks = await db.ProcurementQualityChecks.AsNoTracking().CountAsync(c => open.Contains(c.Status) && c.ArrivalId != null && c.BatchId == null, ct);
        var checks = rows.OrderByDescending(r => Mine(r.Status)).ThenByDescending(r => r.ArrivalId != null && r.BatchId == null).ThenBy(r => r.CreatedAt).Take(Shown)
            .Select(r => new ProcurementHomeCheck(r.Id, r.Number, r.Status, r.ItemName, r.SupplierName, r.ArrivalId, r.ArrivalNumber, r.BatchNumber, r.Received, r.UnitCode,
                r.CreatedAt, Mine(r.Status)))
            .ToList();
        var atMine = (ctx.Quality ? nav.Documents + nav.Lab : 0) + (ctx.DecidesQuality ? nav.Decision : 0);
        return new ProcurementHomeQuality(nav.Open, nav.Documents, nav.Lab, nav.Decision, inTrucks, atMine, checks);
    }

    private async Task<ProcurementHomeStock?> StockAsync(ProcurementContext ctx, CancellationToken ct)
    {
        if (!ctx.Warehouse && !ctx.Quality && !ctx.DecidesQuality)
        {
            return null;
        }

        var toIssue = ctx.Warehouse
            ? await db.ProcurementStockReservations.AsNoTracking().Where(x => x.Status == ProcurementReservationStatus.Active).Select(x => x.RequestId).Distinct().CountAsync(ct)
            : 0;
        var low = ctx.Warehouse ? (await stock.StockAsync(ctx, new ProcurementStockSearch(Filter: "low", PageSize: 1), ct)).Total : 0;
        async Task<int> Batches(string state) => (await batches.ListAsync(ctx, new ProcurementBatchSearch(State: state, PageSize: 1), ct)).Total;
        return new ProcurementHomeStock(toIssue, low, await Batches("expiring"), await Batches("expired"), await Batches("control"));
    }

    private async Task<ProcurementHomeRequests> RequestsAsync(ProcurementContext ctx, CancellationToken ct)
    {
        var decides = ProcurementRules.DecidesAny(ctx);
        var awaiting = decides ? await db.ProcurementRequests.AsNoTracking().AwaitingDecision(ctx).CountAsync(ct) : 0;
        var approvals = awaiting > 0
            ? await db.ProcurementRequests.AsNoTracking().AwaitingDecision(ctx)
                .OrderBy(r => r.ForwardedAt ?? r.SubmittedAt)
                .Take(5)
                .Select(r => new ProcurementApprovalBrief(r.Id, r.Number, r.Title, r.PurchaseTotal > 0 ? r.PurchaseTotal : r.EstimatedTotal, r.Department!.Name, r.Initiator!.FullName,
                    r.SubmittedAt))
                .ToListAsync(ct)
            : [];
        var heads = ctx.HeadOf.ToList();
        var openStatuses = ProcurementRules.Open;
        var departmentOpen = heads.Count > 0
            ? await db.ProcurementRequests.AsNoTracking().CountAsync(r => heads.Contains(r.DepartmentId) && openStatuses.Contains(r.Status), ct)
            : 0;
        var reserved = db.ProcurementStockReservations.AsNoTracking().Where(x => x.Status == ProcurementReservationStatus.Active);
        var departmentPickUp = heads.Count > 0
            ? await reserved.Where(x => db.ProcurementRequests.Any(r => r.Id == x.RequestId && heads.Contains(r.DepartmentId))).Select(x => x.RequestId).Distinct().CountAsync(ct)
            : 0;
        var me = ctx.UserId;
        var minePickUp = await reserved.Where(x => db.ProcurementRequests.Any(r => r.Id == x.RequestId && r.InitiatorId == me)).Select(x => x.RequestId).Distinct().CountAsync(ct);
        int? toBuy = null;
        decimal? toBuyAmount = null;
        if (ctx.Manages)
        {
            var view = await purchases.ToBuyAsync(ctx, ct);
            toBuy = view.Requests.Count;
            toBuyAmount = Math.Round(view.Requests.Sum(r => r.Total), 2);
        }

        return new ProcurementHomeRequests(awaiting, approvals, departmentOpen, departmentPickUp, minePickUp, toBuy, toBuyAmount);
    }
}
