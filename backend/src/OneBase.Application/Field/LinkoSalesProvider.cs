using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales;
using OneBase.Domain.Sales;

namespace OneBase.Application.Field;

/// <summary>
/// Как считать продажи: Taken — заказы, принятые агентом в этот день (по дате создания, без отменённых и недоставленных) —
/// работа агента за день; Delivered — доставленные по дате приёмки, как во вторичке OneBase.
/// </summary>
public enum SalesBasis
{
    Taken,
    Delivered,
}

public sealed record DaySales(long LinkoUserId, DateOnly Date, int Orders, decimal Sum, decimal Kg, int Markets);

/// <summary>План агента на месяц из staff_balance Linko и факт, который посчитал сам Linko.</summary>
public sealed record AgentMonthPlan(long LinkoUserId, decimal? PlanKg, decimal? PlanSum, decimal? PlanAkb, decimal? LinkoFactKg, decimal? LinkoFactSum, decimal? LinkoFactAkb);

public sealed record LinkoVisitRow(long Id, long LinkoUserId, long MarketId, DateOnly Day, DateTime Date, string Status, bool InPlan)
{
    public bool Done => Status == "done";
}

public sealed record MarketRow(long Id, string Name, string? Address, double? Lat, double? Lon, long? ResponsibleAgentId, long? BranchId, string? BranchName, string? Type);

public sealed record MarketOrder(long Id, DateOnly CreatedDate, DateOnly? AcceptedDate, string Status, long? AgentId, decimal Sum, decimal Kg);

public sealed record MarketSales(long MarketId, int Orders, decimal Sum, decimal Kg);

/// <summary>Итог агента за период: заказы, сумма, кг, разных точек с заказами (АКБ).</summary>
public sealed record AgentTotals(long LinkoUserId, int Orders, decimal Sum, decimal Kg, int Markets);

/// <summary>Визиты Linko агента за период: в плане, выполнено, выполнено из плана, разных посещённых точек.</summary>
public sealed record AgentVisitStats(long LinkoUserId, int Planned, int Done, int DoneInPlan, int Markets);

/// <summary>
/// Источник фактических данных Sales Base. Сейчас — зеркало Linko в БД OneBase (синхронизация каждые 20 минут);
/// при прямом API Linko или другой SFA заменяется реализация, интерфейс и UI остаются.
/// </summary>
public interface ILinkoSalesProvider
{
    Task<List<DaySales>> AgentDaySalesAsync(IReadOnlyCollection<long> agents, DateOnly from, DateOnly to, SalesBasis basis, CancellationToken ct);
    Task<List<AgentMonthPlan>> AgentPlansAsync(IReadOnlyCollection<long> agents, int year, int month, CancellationToken ct);
    Task<List<LinkoVisitRow>> VisitsAsync(IReadOnlyCollection<long> agents, DateOnly from, DateOnly to, CancellationToken ct);
    Task<List<LinkoVisitRow>> MarketVisitsAsync(long marketId, int take, CancellationToken ct);
    Task<Dictionary<long, MarketRow>> MarketsAsync(IReadOnlyCollection<long> ids, CancellationToken ct);
    Task<List<MarketOrder>> MarketOrdersAsync(long marketId, DateOnly from, CancellationToken ct);
    Task<Dictionary<long, MarketSales>> MarketSalesAsync(IReadOnlyCollection<long> markets, DateOnly from, DateOnly to, SalesBasis basis, CancellationToken ct);
    Task<Dictionary<long, AgentTotals>> AgentTotalsAsync(IReadOnlyCollection<long> agents, DateOnly from, DateOnly to, SalesBasis basis, CancellationToken ct);
    Task<Dictionary<long, AgentVisitStats>> AgentVisitStatsAsync(IReadOnlyCollection<long> agents, DateOnly from, DateOnly to, CancellationToken ct);
    Task<Dictionary<long, string>> UserNamesAsync(IReadOnlyCollection<long> ids, CancellationToken ct);
    Task<List<(long Id, string Name)>> BranchesAsync(CancellationToken ct);

    /// <summary>Последний день, за который в зеркале есть заказы, — «данные на».</summary>
    Task<DateOnly?> LastDataDateAsync(CancellationToken ct);
}

public sealed class LinkoMirrorSalesProvider(IAppDbContext db, SalesOptions options) : ILinkoSalesProvider
{
    /// <summary>Заказ «принят агентом», если его не отменили и магазин не отказался.</summary>
    private static readonly string[] NotTaken = ["cancelled", "not_delivered"];

    private string[] Excluded => options.ExcludedBranches.Select(b => b.Trim().ToLowerInvariant()).ToArray();

    private IQueryable<LinkoOrder> Orders(SalesBasis basis)
    {
        var excluded = Excluded;
        var sold = options.SoldStatuses;
        var q = db.LinkoOrders.AsNoTracking().Where(o => o.BranchName == null || !excluded.Contains(o.BranchName.ToLower()));
        return basis == SalesBasis.Taken ? q.Where(o => !NotTaken.Contains(o.Status)) : q.Where(o => sold.Contains(o.Status) && o.AcceptedDate != null);
    }

    public async Task<List<DaySales>> AgentDaySalesAsync(IReadOnlyCollection<long> agents, DateOnly from, DateOnly to, SalesBasis basis, CancellationToken ct)
    {
        if (agents.Count == 0)
        {
            return [];
        }

        var ids = agents.ToArray();
        var q = Orders(basis).Where(o => o.AgentId != null && ids.Contains(o.AgentId.Value));
        var rows = basis == SalesBasis.Taken
            ? await q.Where(o => o.CreatedDate >= from && o.CreatedDate <= to)
                .GroupBy(o => new { Agent = o.AgentId!.Value, Date = o.CreatedDate })
                .Select(g => new { g.Key.Agent, g.Key.Date, Orders = g.Count(), Sum = g.Sum(o => o.TotalPrice), Kg = g.Sum(o => o.TotalWeight), Markets = g.Select(o => o.MarketId).Distinct().Count() })
                .ToListAsync(ct)
            : await q.Where(o => o.AcceptedDate >= from && o.AcceptedDate <= to)
                .GroupBy(o => new { Agent = o.AgentId!.Value, Date = o.AcceptedDate!.Value })
                .Select(g => new { g.Key.Agent, g.Key.Date, Orders = g.Count(), Sum = g.Sum(o => o.TotalPrice), Kg = g.Sum(o => o.TotalWeight), Markets = g.Select(o => o.MarketId).Distinct().Count() })
                .ToListAsync(ct);
        return rows.Select(r => new DaySales(r.Agent, r.Date, r.Orders, r.Sum, r.Kg, r.Markets)).ToList();
    }

    public async Task<List<AgentMonthPlan>> AgentPlansAsync(IReadOnlyCollection<long> agents, int year, int month, CancellationToken ct)
    {
        if (agents.Count == 0)
        {
            return [];
        }

        var ids = agents.ToArray();
        var rows = await db.SalesStaffPlans.AsNoTracking()
            .Where(p => p.Year == year && p.Month == month && ids.Contains(p.LinkoUserId))
            .Select(p => new { p.LinkoUserId, p.PlanType, p.PlanAmount, p.FactAmount })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.LinkoUserId)
            .Select(g =>
            {
                decimal? Plan(string type) => g.Where(r => r.PlanType == type).Select(r => (decimal?)r.PlanAmount).DefaultIfEmpty(null).Sum() is { } v && v > 0 ? v : null;
                decimal? Fact(string type) => g.Any(r => r.PlanType == type) ? g.Where(r => r.PlanType == type).Sum(r => r.FactAmount) : null;
                return new AgentMonthPlan(g.Key, Plan(StaffPlanTypes.SalesWeight), Plan(StaffPlanTypes.SalesSum), Plan(StaffPlanTypes.ActiveClients),
                    Fact(StaffPlanTypes.SalesWeight), Fact(StaffPlanTypes.SalesSum), Fact(StaffPlanTypes.ActiveClients));
            })
            .ToList();
    }

    public async Task<List<LinkoVisitRow>> VisitsAsync(IReadOnlyCollection<long> agents, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (agents.Count == 0)
        {
            return [];
        }

        var ids = agents.ToArray();
        var rows = await db.LinkoVisits.AsNoTracking()
            .Where(v => v.Day >= from && v.Day <= to && v.UserId != null && v.MarketId != null && ids.Contains(v.UserId.Value))
            .Select(v => new { v.Id, UserId = v.UserId!.Value, MarketId = v.MarketId!.Value, v.Day, v.Date, v.Status, v.IsInPlan })
            .ToListAsync(ct);
        return rows.Select(v => new LinkoVisitRow(v.Id, v.UserId, v.MarketId, v.Day, v.Date, v.Status, v.IsInPlan)).ToList();
    }

    public async Task<List<LinkoVisitRow>> MarketVisitsAsync(long marketId, int take, CancellationToken ct)
    {
        var rows = await db.LinkoVisits.AsNoTracking()
            .Where(v => v.MarketId == marketId && v.UserId != null)
            .OrderByDescending(v => v.Date)
            .Take(take)
            .Select(v => new { v.Id, UserId = v.UserId!.Value, MarketId = v.MarketId!.Value, v.Day, v.Date, v.Status, v.IsInPlan })
            .ToListAsync(ct);
        return rows.Select(v => new LinkoVisitRow(v.Id, v.UserId, v.MarketId, v.Day, v.Date, v.Status, v.IsInPlan)).ToList();
    }

    public async Task<Dictionary<long, MarketRow>> MarketsAsync(IReadOnlyCollection<long> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var list = ids.Distinct().ToArray();
        var rows = await db.LinkoMarkets.AsNoTracking()
            .Where(m => list.Contains(m.Id))
            .Select(m => new MarketRow(m.Id, m.Name, m.Address, m.Lat, m.Lon, m.ResponsibleAgentId, m.BranchId, m.BranchName, m.MarketTypeName))
            .ToListAsync(ct);
        return rows.ToDictionary(m => m.Id);
    }

    public async Task<List<MarketOrder>> MarketOrdersAsync(long marketId, DateOnly from, CancellationToken ct)
    {
        var rows = await db.LinkoOrders.AsNoTracking()
            .Where(o => o.MarketId == marketId && o.CreatedDate >= from)
            .OrderByDescending(o => o.CreatedDate).ThenByDescending(o => o.Id)
            .Select(o => new MarketOrder(o.Id, o.CreatedDate, o.AcceptedDate, o.Status, o.AgentId, o.TotalPrice, o.TotalWeight))
            .ToListAsync(ct);
        return rows;
    }

    public async Task<Dictionary<long, MarketSales>> MarketSalesAsync(IReadOnlyCollection<long> markets, DateOnly from, DateOnly to, SalesBasis basis, CancellationToken ct)
    {
        if (markets.Count == 0)
        {
            return [];
        }

        var ids = markets.Distinct().ToArray();
        var q = Orders(basis).Where(o => o.MarketId != null && ids.Contains(o.MarketId.Value));
        q = basis == SalesBasis.Taken ? q.Where(o => o.CreatedDate >= from && o.CreatedDate <= to) : q.Where(o => o.AcceptedDate >= from && o.AcceptedDate <= to);
        var rows = await q.GroupBy(o => o.MarketId!.Value)
            .Select(g => new MarketSales(g.Key, g.Count(), g.Sum(o => o.TotalPrice), g.Sum(o => o.TotalWeight)))
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.MarketId);
    }

    public async Task<Dictionary<long, AgentTotals>> AgentTotalsAsync(IReadOnlyCollection<long> agents, DateOnly from, DateOnly to, SalesBasis basis, CancellationToken ct)
    {
        if (agents.Count == 0)
        {
            return [];
        }

        var ids = agents.ToArray();
        var q = Orders(basis).Where(o => o.AgentId != null && ids.Contains(o.AgentId.Value));
        q = basis == SalesBasis.Taken ? q.Where(o => o.CreatedDate >= from && o.CreatedDate <= to) : q.Where(o => o.AcceptedDate >= from && o.AcceptedDate <= to);
        var rows = await q.GroupBy(o => o.AgentId!.Value)
            .Select(g => new AgentTotals(g.Key, g.Count(), g.Sum(o => o.TotalPrice), g.Sum(o => o.TotalWeight), g.Select(o => o.MarketId).Distinct().Count()))
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.LinkoUserId);
    }

    public async Task<Dictionary<long, AgentVisitStats>> AgentVisitStatsAsync(IReadOnlyCollection<long> agents, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (agents.Count == 0)
        {
            return [];
        }

        var ids = agents.ToArray();
        var rows = await db.LinkoVisits.AsNoTracking()
            .Where(v => v.Day >= from && v.Day <= to && v.UserId != null && ids.Contains(v.UserId.Value))
            .GroupBy(v => v.UserId!.Value)
            .Select(g => new AgentVisitStats(
                g.Key,
                g.Count(v => v.IsInPlan),
                g.Count(v => v.Status == "done"),
                g.Count(v => v.IsInPlan && v.Status == "done"),
                g.Where(v => v.Status == "done").Select(v => v.MarketId).Distinct().Count()))
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.LinkoUserId);
    }

    public async Task<Dictionary<long, string>> UserNamesAsync(IReadOnlyCollection<long> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var list = ids.Distinct().ToArray();
        var rows = await db.LinkoUsers.AsNoTracking().Where(u => list.Contains(u.Id)).Select(u => new { u.Id, u.FirstName, u.SecondName, u.Username }).ToListAsync(ct);
        return rows.ToDictionary(u => u.Id, u => string.Join(' ', new[] { u.FirstName, u.SecondName }.Where(s => !string.IsNullOrWhiteSpace(s))) is { Length: > 0 } n ? n : u.Username ?? $"#{u.Id}");
    }

    public async Task<List<(long Id, string Name)>> BranchesAsync(CancellationToken ct)
    {
        var excluded = Excluded;
        var rows = await db.LinkoMarkets.AsNoTracking()
            .Where(m => m.BranchId != null && m.BranchName != null)
            .GroupBy(m => new { m.BranchId, m.BranchName })
            .Select(g => new { g.Key.BranchId, g.Key.BranchName, Count = g.Count() })
            .ToListAsync(ct);
        return rows.Where(r => !excluded.Contains(r.BranchName!.Trim().ToLowerInvariant()))
            .GroupBy(r => r.BranchId!.Value)
            .Select(g => (g.Key, g.OrderByDescending(r => r.Count).First().BranchName!))
            .OrderBy(b => b.Item2)
            .ToList();
    }

    public async Task<DateOnly?> LastDataDateAsync(CancellationToken ct) =>
        await db.LinkoOrders.AsNoTracking().MaxAsync(o => (DateOnly?)o.CreatedDate, ct);
}
