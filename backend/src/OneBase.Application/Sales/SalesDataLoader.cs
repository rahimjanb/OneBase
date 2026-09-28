using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales.Metrics;
using OneBase.Domain.Sales;

namespace OneBase.Application.Sales;

/// <summary>Сбрасывает кэш расчётов после синхронизации и изменений в настройках продаж.</summary>
public sealed class SalesCacheSignal
{
    private CancellationTokenSource _cts = new();

    public IChangeToken Token => new CancellationChangeToken(_cts.Token);

    public void Invalidate()
    {
        var old = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
        old.Cancel();
        old.Dispose();
    }
}

public sealed record SalesMonth(int Year, int Month);

/// <summary>Читает данные месяца из нашей БД (не из Linko) и собирает SalesAnalytics. Результат кэшируется.</summary>
public sealed class SalesDataLoader(IAppDbContext db, SalesOptions options, IMemoryCache cache, SalesCacheSignal signal)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    /// <summary>Месяцы, за которые есть продажи — от новых к старым.</summary>
    public async Task<IReadOnlyList<SalesMonth>> MonthsAsync(CancellationToken ct = default)
    {
        var sold = options.SoldStatuses;
        var months = await db.LinkoOrders
            .Where(o => sold.Contains(o.Status))
            .Select(o => new { o.CreatedDate.Year, o.CreatedDate.Month })
            .Distinct()
            .ToListAsync(ct);

        return months.OrderByDescending(m => m.Year).ThenByDescending(m => m.Month)
            .Select(m => new SalesMonth(m.Year, m.Month))
            .ToList();
    }

    public async Task<SalesAnalytics> LoadAsync(int? year, int? month, PlanKind kind, CancellationToken ct = default)
    {
        var lastData = await LastDataDateAsync(ct);
        var y = year ?? lastData?.Year ?? DateTime.Today.Year;
        var m = month is >= 1 and <= 12 ? month.Value : lastData?.Month ?? DateTime.Today.Month;

        var key = $"sales:{y}-{m}:{kind}";
        if (cache.TryGetValue(key, out SalesAnalytics? cached) && cached is not null)
        {
            return cached;
        }

        var data = await BuildAsync(y, m, kind, lastData, ct);
        var analytics = new SalesAnalytics(data);
        cache.Set(key, analytics, new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(CacheTtl)
            .AddExpirationToken(signal.Token));
        return analytics;
    }

    private async Task<DateOnly?> LastDataDateAsync(CancellationToken ct)
    {
        var sold = options.SoldStatuses;
        return options.DateField == SaleDateField.Delivery
            ? await db.LinkoOrders.Where(o => sold.Contains(o.Status)).MaxAsync(o => (DateOnly?)(o.DeliveryDate ?? o.CreatedDate), ct)
            : await db.LinkoOrders.Where(o => sold.Contains(o.Status)).MaxAsync(o => (DateOnly?)o.CreatedDate, ct);
    }

    private async Task<MonthData> BuildAsync(int year, int month, PlanKind kind, DateOnly? lastData, CancellationToken ct)
    {
        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var previousStart = monthStart.AddMonths(-1);

        var dataThrough = lastData is null || lastData < monthStart
            ? monthStart.AddDays(-1)
            : lastData > monthEnd ? monthEnd : lastData.Value;

        var lines = await LinesAsync(previousStart, dataThrough < monthStart ? monthEnd : dataThrough, ct);
        var historyFrom = new[] { new DateOnly(year, 1, 1), monthStart.AddMonths(-3) }.Min();

        var visits = await db.LinkoVisits.AsNoTracking()
            .Where(v => v.Day >= monthStart && v.Day <= monthEnd && v.UserId != null && v.MarketId != null)
            .Select(v => new { v.Day, v.UserId, v.MarketId, v.Status, v.IsInPlan })
            .ToListAsync(ct);

        var regionPlans = await db.SalesRegionPlans.AsNoTracking()
            .Where(p => p.Year == year && p.Kind == kind)
            .Select(p => new PlanRow(p.RegionId, null, p.Month, p.CategoryId, p.PlanKg))
            .ToListAsync(ct);
        var manualAgentPlans = await db.SalesAgentPlans.AsNoTracking()
            .Where(p => p.Year == year && p.Kind == kind)
            .Select(p => new PlanRow(null, p.LinkoUserId, p.Month, p.CategoryId, p.PlanKg))
            .ToListAsync(ct);

        // Планы агентов из Linko (staff_balance) — это план РОП. Ручной план OneBase на тот же месяц важнее.
        var staff = kind == PlanKind.Rop
            ? await db.SalesStaffPlans.AsNoTracking().Where(p => p.Year == year).ToListAsync(ct)
            : [];
        var manualKeys = manualAgentPlans.Select(p => (p.AgentId, p.Month)).ToHashSet();
        var staffIds = staff.Select(s => s.LinkoUserId).Distinct().ToList();
        var excludedStaff = (await db.LinkoUsers.AsNoTracking()
                .Where(u => staffIds.Contains(u.Id))
                .Select(u => new { u.Id, u.JobName })
                .ToListAsync(ct))
            .Where(u => u.JobName is { } job && options.StaffPlanExcludeJobs.Any(x => job.Contains(x, StringComparison.OrdinalIgnoreCase)))
            .Select(u => u.Id)
            .ToHashSet();
        var autoAgentPlans = staff
            .Where(s => s.PlanType == StaffPlanTypes.SalesWeight && !excludedStaff.Contains(s.LinkoUserId))
            .GroupBy(s => (AgentId: (long?)s.LinkoUserId, s.Month))
            .Where(g => !manualKeys.Contains(g.Key))
            .Select(g => new PlanRow(null, g.Key.AgentId, g.Key.Month, null, g.Sum(s => s.PlanAmount)));
        var yearAgentPlans = manualAgentPlans.Concat(autoAgentPlans).ToList();
        var agentPlans = yearAgentPlans.Where(p => p.Month == month).ToList();

        var profiles = await db.SalesAgentProfiles.AsNoTracking().ToDictionaryAsync(p => p.LinkoUserId, ct);
        var users = await db.LinkoUsers.AsNoTracking().ToListAsync(ct);
        var targets = await db.SalesTargets.AsNoTracking().ToDictionaryAsync(t => t.Key, t => t.Value, ct);

        decimal Target(string k) => targets.TryGetValue(k, out var v) ? v : SalesTargetKeys.Defaults[k];

        return new MonthData
        {
            Year = year,
            Month = month,
            DataThrough = dataThrough,
            Current = lines.Where(l => l.Date >= monthStart && l.Date <= dataThrough).ToList(),
            Previous = lines.Where(l => l.Date >= previousStart && l.Date < monthStart).ToList(),
            Visits = visits.Select(v => new VisitRecord(v.Day, v.UserId!.Value, v.MarketId!.Value, ParseStatus(v.Status), v.IsInPlan)).ToList(),
            History = await HistoryAsync(historyFrom, monthEnd, ct),
            Plans = regionPlans.Where(p => p.Month == month).Concat(agentPlans).ToList(),
            YearRegionPlans = regionPlans,
            YearAgentPlans = yearAgentPlans,
            Indicators = staff
                .Where(s => s.Month == month)
                .Select(s => new StaffIndicator(s.LinkoUserId, s.IndicatorId, CleanIndicatorName(s.IndicatorName), s.PlanType, s.PlanAmount, s.FactAmount))
                .ToList(),
            Agents = users.ToDictionary(u => u.Id, u =>
            {
                var profile = profiles.GetValueOrDefault(u.Id);
                return new AgentInfo(u.Id, u.DisplayName, u.IsActive, profile?.RegionId, profile?.IsVacancy ?? false, profile is not null);
            }),
            Regions = await db.SalesRegions.AsNoTracking()
                .Select(r => new RegionInfo(r.Id, r.LinkoBranchId, r.Name, r.DirectionId, r.SupervisorName, r.DealerName))
                .ToListAsync(ct),
            Directions = await db.SalesDirections.AsNoTracking()
                .Select(d => new DirectionInfo(d.Id, d.Name, d.Kind == DirectionKind.Channel, d.ManagerName, d.Description, d.SortOrder))
                .ToListAsync(ct),
            Markets = await db.LinkoMarkets.AsNoTracking()
                .Select(x => new MarketInfo(x.Id, x.Name, x.ResponsibleAgentId, x.BranchId))
                .ToDictionaryAsync(x => x.Id, ct),
            Categories = await db.LinkoProductTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, ct),
            MarketAssignments = (await db.LinkoMarketUsers.AsNoTracking().Where(x => !x.IsDelete)
                    .Select(x => new { x.UserId, x.MarketId }).ToListAsync(ct))
                .Select(x => (x.UserId, x.MarketId)).ToList(),
            Targets = new SalesTargets(
                Target(SalesTargetKeys.VisitConversion),
                Target(SalesTargetKeys.RevenuePerOutlet),
                Target(SalesTargetKeys.AkbPerAgent),
                Target(SalesTargetKeys.CategoriesPerOutlet)),
            Thresholds = options.Flags,
        };
    }

    /// <summary>Строки продаж (+) и возвратов (−) за период.</summary>
    private async Task<List<SaleLine>> LinesAsync(DateOnly start, DateOnly end, CancellationToken ct)
    {
        var sold = options.SoldStatuses;
        var returned = options.ReturnStatuses;
        var byDelivery = options.DateField == SaleDateField.Delivery;

        var orders = await (
                from l in db.LinkoOrderLines
                join o in db.LinkoOrders on l.OrderId equals o.Id
                join p in db.LinkoProducts on l.ProductId equals (long?)p.Id into pj
                from p in pj.DefaultIfEmpty()
                let date = byDelivery ? o.DeliveryDate ?? o.CreatedDate : o.CreatedDate
                where sold.Contains(o.Status) && date >= start && date <= end
                select new SaleLine(date, o.AgentId, o.MarketId, o.BranchId, p.TypeId, l.ProductId, l.TotalWeight, l.TotalPrice, o.Id))
            .AsNoTracking()
            .ToListAsync(ct);

        var returns = await (
                from l in db.LinkoOrderReturnLines
                join r in db.LinkoOrderReturns on l.ReturnId equals r.Id
                join p in db.LinkoProducts on l.ProductId equals (long?)p.Id into pj
                from p in pj.DefaultIfEmpty()
                where returned.Contains(r.Status) && r.CreatedDate >= start && r.CreatedDate <= end
                select new SaleLine(r.CreatedDate, r.AgentId, r.MarketId, r.BranchId, p.TypeId, l.ProductId, -l.TotalWeight, -(l.Price * l.Amount), null))
            .AsNoTracking()
            .ToListAsync(ct);

        orders.AddRange(returns);
        return orders;
    }

    /// <summary>Факт кг по месяцам в разрезе агента и филиала (продажи минус возвраты). Месяц — по дате создания.</summary>
    private async Task<List<MonthlyFact>> HistoryAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var sold = options.SoldStatuses;
        var returned = options.ReturnStatuses;

        var sales = await db.LinkoOrders
            .Where(o => sold.Contains(o.Status) && o.CreatedDate >= from && o.CreatedDate <= to)
            .GroupBy(o => new { o.CreatedDate.Year, o.CreatedDate.Month, o.AgentId, o.BranchId })
            .Select(g => new MonthlyFact(g.Key.Year, g.Key.Month, g.Key.AgentId, g.Key.BranchId, g.Sum(o => o.TotalWeight)))
            .ToListAsync(ct);

        var returns = await db.LinkoOrderReturns
            .Where(r => returned.Contains(r.Status) && r.CreatedDate >= from && r.CreatedDate <= to)
            .GroupBy(r => new { r.CreatedDate.Year, r.CreatedDate.Month, r.AgentId, r.BranchId })
            .Select(g => new MonthlyFact(g.Key.Year, g.Key.Month, g.Key.AgentId, g.Key.BranchId, -g.Sum(r => r.TotalWeight)))
            .ToListAsync(ct);

        return sales.Concat(returns).ToList();
    }

    private static readonly System.Text.RegularExpressions.Regex MonthPrefix = new(
        "^(январь|февраль|март|апрель|май|июнь|июль|август|сентябрь|октябрь|ноябрь|декабрь)\\s+",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>«Сентябрь Кекс Коканд / Бекобод» → «Кекс Коканд / Бекобод»: месяц и так выбран в фильтре.</summary>
    internal static string CleanIndicatorName(string name) => MonthPrefix.Replace(name.Trim(), string.Empty);

    private static VisitStatus ParseStatus(string status) => status switch
    {
        "done" => VisitStatus.Done,
        "pending" => VisitStatus.Pending,
        _ => VisitStatus.Undone,
    };
}
