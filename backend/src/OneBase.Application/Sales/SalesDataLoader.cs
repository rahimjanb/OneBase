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
    private CancellationTokenSource _history = new();

    public IChangeToken Token => new CancellationChangeToken(_cts.Token);

    /// <summary>Кэш агрегатов по давним месяцам: сбрасывается только полной загрузкой или очисткой данных.</summary>
    public IChangeToken HistoryToken => new CancellationChangeToken(_history.Token);

    public void Invalidate() => Reset(ref _cts);

    /// <summary>Полная перезагрузка данных: сбрасывает и кэш давних месяцев.</summary>
    public void InvalidateHistory()
    {
        Reset(ref _history);
        Reset(ref _cts);
    }

    private static void Reset(ref CancellationTokenSource source)
    {
        var old = Interlocked.Exchange(ref source, new CancellationTokenSource());
        old.Cancel();
        old.Dispose();
    }
}

/// <summary>Агрегаты по прошлым месяцам, которые дешевле посчитать в БД, чем выгружать строки продаж.</summary>
public interface ISalesHistoryReader
{
    /// <summary>АКБ по месяцам периода: по республике и по филиалам, итог и по категориям (с объединением подтипов).</summary>
    Task<IReadOnlyList<MonthlyAkb>> AkbByMonthAsync(DateOnly from, DateOnly to, IReadOnlyDictionary<long, long> categoryGroups, CancellationToken ct);
}

public sealed record SalesMonth(int Year, int Month);


/// <summary>
/// Читает данные месяца из нашей копии Linko (не из Linko) и собирает SalesAnalytics. Результат кэшируется.
/// Что считается продажей, возвратом и заказом для визита — решает SecondarySales; здесь только выборка строк.
/// </summary>
public sealed class SalesDataLoader(IAppDbContext db, SalesOptions options, IMemoryCache cache, SalesCacheSignal signal, ISalesHistoryReader history)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan HistoryCacheTtl = TimeSpan.FromHours(6);

    /// <summary>Один пересчёт за раз: запрос пользователя и прогрев после синхронизации не считают одно и то же дважды.</summary>
    private static readonly SemaphoreSlim BuildGate = new(1, 1);

    /// <summary>«Живой» ассортимент: SKU, проданные за столько месяцев до выбранного (плюс сам месяц).</summary>
    private const int AssortmentMonths = 6;

    /// <summary>Заказ с датой реализации по настройке (по умолчанию — дата приёмки).</summary>
    private sealed class DatedOrder
    {
        public long Id { get; init; }
        public DateOnly? Date { get; init; }
        public string Status { get; init; } = "";
        public string? BranchName { get; init; }
        public long? BranchId { get; init; }
        public long? AgentId { get; init; }
        public decimal TotalWeight { get; init; }
    }

    private IQueryable<DatedOrder> Dated() => options.DateField switch
    {
        SaleDateField.Accepted => db.LinkoOrders.Select(o => new DatedOrder
        {
            Id = o.Id, Date = o.AcceptedDate, Status = o.Status, BranchName = o.BranchName, BranchId = o.BranchId, AgentId = o.AgentId, TotalWeight = o.TotalWeight,
        }),
        SaleDateField.Delivery => db.LinkoOrders.Select(o => new DatedOrder
        {
            Id = o.Id, Date = o.DeliveryDate ?? o.CreatedDate, Status = o.Status, BranchName = o.BranchName, BranchId = o.BranchId, AgentId = o.AgentId, TotalWeight = o.TotalWeight,
        }),
        _ => db.LinkoOrders.Select(o => new DatedOrder
        {
            Id = o.Id, Date = o.CreatedDate, Status = o.Status, BranchName = o.BranchName, BranchId = o.BranchId, AgentId = o.AgentId, TotalWeight = o.TotalWeight,
        }),
    };

    /// <summary>Исключённые филиалы в нижнем регистре — для SQL (lower(branch) in …).</summary>
    private string[] ExcludedLower => options.ExcludedBranches.Select(b => b.Trim().ToLowerInvariant()).ToArray();

    /// <summary>Месяцы, за которые есть продажи (по дате реализации) — от новых к старым.</summary>
    public async Task<IReadOnlyList<SalesMonth>> MonthsAsync(CancellationToken ct = default) =>
        await Cached("sales:months", async () =>
        {
            var sold = options.SoldStatuses;
            // Приёмка «в будущем» (бывает у части заказов) не должна давать в выборе периода месяц, которого ещё нет.
            var today = DateOnly.FromDateTime(DateTime.Today);
            var months = await Dated()
                .Where(o => sold.Contains(o.Status) && o.Date != null && o.Date <= today)
                .Select(o => new { o.Date!.Value.Year, o.Date!.Value.Month })
                .Distinct()
                .ToListAsync(ct);

            return (IReadOnlyList<SalesMonth>)months.OrderByDescending(m => m.Year).ThenByDescending(m => m.Month)
                .Select(m => new SalesMonth(m.Year, m.Month))
                .ToList();
        });

    public async Task<SalesAnalytics> LoadAsync(int? year, int? month, PlanKind kind, CancellationToken ct = default)
    {
        var lastData = await Cached("sales:last-data", () => LastDataDateAsync(ct));
        var y = year ?? lastData?.Year ?? DateTime.Today.Year;
        var m = month is >= 1 and <= 12 ? month.Value : lastData?.Month ?? DateTime.Today.Month;

        var key = $"sales:{y}-{m}:{kind}";
        if (cache.TryGetValue(key, out SalesAnalytics? cached) && cached is not null)
        {
            return cached;
        }

        await BuildGate.WaitAsync(ct);
        try
        {
            if (cache.TryGetValue(key, out cached) && cached is not null)
            {
                return cached; // пока ждали, этот месяц уже посчитал другой запрос
            }

            var token = signal.Token; // до загрузки: если данные обновятся во время расчёта, результат сразу устареет
            var data = await BuildAsync(y, m, kind, lastData, ct);
            var analytics = new SalesAnalytics(data);
            cache.Set(key, analytics, new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(CacheTtl)
                .AddExpirationToken(token));
            return analytics;
        }
        finally
        {
            BuildGate.Release();
        }
    }

    /// <summary>Небольшие справочные запросы (месяцы, дата последних данных) — до следующей синхронизации.</summary>
    private async Task<T> Cached<T>(string key, Func<Task<T>> load)
    {
        if (cache.TryGetValue(key, out T? value))
        {
            return value!;
        }

        var token = signal.Token;
        value = await load();
        cache.Set(key, value, new MemoryCacheEntryOptions().SetAbsoluteExpiration(CacheTtl).AddExpirationToken(token));
        return value;
    }

    /// <summary>
    /// Последний день, за который есть продажи (по дате реализации), но не позже сегодняшнего: у части заказов Linko
    /// дата приёмки стоит в будущем (например, завтра) — отчётный день из-за них не должен уезжать вперёд.
    /// </summary>
    private async Task<DateOnly?> LastDataDateAsync(CancellationToken ct)
    {
        var sold = options.SoldStatuses;
        var today = Today;
        return await Dated().Where(o => sold.Contains(o.Status) && o.Date <= today).MaxAsync(o => o.Date, ct);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    /// <summary>Проданные заказы с датой приёмки позже сегодняшнего дня: в факт попадут, когда этот день наступит.</summary>
    private async Task<int> AcceptedInFutureAsync(CancellationToken ct)
    {
        if (options.DateField != SaleDateField.Accepted)
        {
            return 0;
        }

        var sold = options.SoldStatuses;
        var today = Today;
        return await db.LinkoOrders.CountAsync(o => sold.Contains(o.Status) && o.AcceptedDate > today, ct);
    }

    private async Task<MonthData> BuildAsync(int year, int month, PlanKind kind, DateOnly? lastData, CancellationToken ct)
    {
        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var previousStart = monthStart.AddMonths(-1);

        var dataThrough = lastData is null || lastData < monthStart
            ? monthStart.AddDays(-1)
            : lastData > monthEnd ? monthEnd : lastData.Value;
        var salesEnd = dataThrough < monthStart ? monthEnd : dataThrough;

        // Сырые строки: заказы, реализованные с начала прошлого месяца, и заказы, созданные в месяце (для визитов);
        // возвраты с начала прошлого месяца. Правила применяет SecondarySales.
        var rawOrders = await RawOrdersAsync(previousStart, salesEnd, monthStart, salesEnd, ct);
        var (rawReturns, returnHeaders) = await RawReturnsAsync(previousStart, salesEnd, ct);
        var missingAcceptance = await DeliveredWithoutAcceptanceAsync(monthStart.AddDays(-45), salesEnd, ct);

        var current = SecondarySales.Build(rawOrders, rawReturns, returnHeaders, monthStart, dataThrough, options, missingAcceptance);
        current = current with { Quality = current.Quality with { AcceptedInFuture = await AcceptedInFutureAsync(ct) } };
        var previous = SecondarySales.Build(rawOrders, rawReturns, returnHeaders, previousStart, monthStart.AddDays(-1), options);
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

        // Доставщики и т.п.: их «визиты» — доставки, в работу ТП они не входят.
        var nonSales = users
            .Where(u => u.JobName is { } job && options.NonSalesJobs.Any(x => job.Contains(x, StringComparison.OrdinalIgnoreCase)))
            .Select(u => u.Id)
            .ToHashSet();
        var targets = await db.SalesTargets.AsNoTracking().ToDictionaryAsync(t => t.Key, t => t.Value, ct);

        decimal Target(string k) => targets.TryGetValue(k, out var v) ? v : SalesTargetKeys.Defaults[k];

        var linkoTypes = await db.LinkoProductTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, ct);
        var categoryMap = SalesCategories.Build(options.Categories, linkoTypes);
        var yearStart = new DateOnly(year, 1, 1);
        var akbHistoryTo = previousStart.AddDays(-1); // прошлый и текущий месяц считаются из строк продаж
        IReadOnlyList<MonthlyAkb> akbHistory = yearStart <= akbHistoryTo
            ? await AkbHistoryAsync(yearStart, akbHistoryTo, categoryMap.Mapping, ct)
            : [];

        // Регионы — филиалы вторички: исключённые («Завод») в структуре не показываются, у них отдельный блок.
        // Старые филиалы («Жиззах (эски)») считаются в текущем регионе с тем же названием.
        var excludedBranchIds = await ExcludedBranchIdsAsync(ct);
        var (regions, branchAliases) = OldBranches.Merge(
            (await db.SalesRegions.AsNoTracking()
                .Select(r => new RegionInfo(r.Id, r.LinkoBranchId, r.Name, r.DirectionId, r.SupervisorName, r.DealerName))
                .ToListAsync(ct))
            .Where(r => !excludedBranchIds.Contains(r.BranchId))
            .ToList(),
            options.OldBranchSuffix);

        return new MonthData
        {
            Year = year,
            Month = month,
            DataThrough = dataThrough,
            Current = current.Lines,
            Previous = previous.Lines,
            VisitOrders = SecondarySales.VisitOrders(rawOrders, monthStart, salesEnd, options),
            ExcludedCurrent = current.Excluded,
            ExcludedPrevious = previous.Excluded,
            OtherCurrency = current.OtherCurrency ?? [],
            ExcludedOtherCurrency = current.ExcludedOtherCurrency ?? [],
            Quality = current.Quality,
            Visits = visits.Where(v => !nonSales.Contains(v.UserId!.Value)).Select(v => new VisitRecord(v.Day, v.UserId!.Value, v.MarketId!.Value, ParseStatus(v.Status), v.IsInPlan)).ToList(),
            History = await HistoryAsync(historyFrom, monthEnd, ct),
            Plans = regionPlans.Where(p => p.Month == month).Concat(agentPlans).ToList(),
            YearRegionPlans = regionPlans,
            YearAgentPlans = yearAgentPlans,
            Indicators = staff
                .Where(s => s.Month == month)
                .Select(s => new StaffIndicator(s.LinkoUserId, s.IndicatorId, CleanIndicatorName(s.IndicatorName), s.PlanType, s.PlanAmount, s.FactAmount))
                .ToList(),
            RevenuePlans = staff
                .Where(s => s.Month == month && s.PlanType == StaffPlanTypes.SalesSum && !excludedStaff.Contains(s.LinkoUserId))
                .GroupBy(s => s.LinkoUserId)
                .Select(g => new PlanRow(null, g.Key, month, null, g.Sum(s => s.PlanAmount)))
                .ToList(),
            TeamPlanStaff = excludedStaff,
            Agents = users.ToDictionary(u => u.Id, u =>
            {
                var profile = profiles.GetValueOrDefault(u.Id);
                return new AgentInfo(u.Id, u.DisplayName, u.IsActive, profile?.RegionId, profile?.IsVacancy ?? false, profile is not null, u.JobName);
            }),
            Regions = regions,
            BranchAliases = branchAliases,
            Directions = await db.SalesDirections.AsNoTracking()
                .Select(d => new DirectionInfo(d.Id, d.Name, d.Kind == DirectionKind.Channel, d.ManagerName, d.Description, d.SortOrder))
                .ToListAsync(ct),
            Markets = await db.LinkoMarkets.AsNoTracking()
                .Select(x => new MarketInfo(x.Id, x.Name, x.ResponsibleAgentId, x.BranchId))
                .ToDictionaryAsync(x => x.Id, ct),
            Categories = linkoTypes,
            CategoryMap = categoryMap,
            AkbHistory = akbHistory,
            Products = await db.LinkoProducts.AsNoTracking()
                .Select(p => new ProductInfo(p.Id, p.Name, p.Code, p.TypeId))
                .ToDictionaryAsync(p => p.Id, ct),
            ActiveSkus = await ActiveSkusAsync(monthStart.AddMonths(-AssortmentMonths), salesEnd, ct),
            MarketAssignments = (await db.LinkoMarketUsers.AsNoTracking().Where(x => !x.IsDelete)
                    .Select(x => new { x.UserId, x.MarketId }).ToListAsync(ct))
                .Select(x => (x.UserId, x.MarketId)).ToList(),
            Targets = new SalesTargets(
                Target(SalesTargetKeys.VisitConversion),
                Target(SalesTargetKeys.RevenuePerOutlet),
                Target(SalesTargetKeys.AkbPerAgent),
                Target(SalesTargetKeys.CategoriesPerOutlet)),
            Thresholds = options.Flags,
            SalesRepJobs = options.SalesRepJobs,
        };
    }

    /// <summary>
    /// Строки заказов-кандидатов: реализованные в [saleFrom; saleTo] (по дате реализации) или созданные в
    /// [createdFrom; createdTo] (для сшивки с визитами). Статусы, филиалы и точные даты проверяет SecondarySales.
    /// </summary>
    private async Task<List<RawOrderLine>> RawOrdersAsync(DateOnly saleFrom, DateOnly saleTo, DateOnly createdFrom, DateOnly createdTo, CancellationToken ct)
    {
        var sold = options.SoldStatuses;
        var byAccepted = options.DateField == SaleDateField.Accepted;
        var byDelivery = options.DateField == SaleDateField.Delivery;

        return await (
                from l in db.LinkoOrderLines
                join o in db.LinkoOrders on l.OrderId equals o.Id
                join p in db.LinkoProducts on l.ProductId equals (long?)p.Id into pj
                from p in pj.DefaultIfEmpty()
                where sold.Contains(o.Status)
                      && ((byAccepted && o.AcceptedDate >= saleFrom && o.AcceptedDate <= saleTo)
                          || (byDelivery && (o.DeliveryDate ?? o.CreatedDate) >= saleFrom && (o.DeliveryDate ?? o.CreatedDate) <= saleTo)
                          || (!byAccepted && !byDelivery && o.CreatedDate >= saleFrom && o.CreatedDate <= saleTo)
                          || (o.CreatedDate >= createdFrom && o.CreatedDate <= createdTo))
                select new RawOrderLine(o.Id, o.Status, o.CreatedDate, o.DeliveryDate, o.AcceptedDate, o.BranchId, o.BranchName,
                    o.AgentId, o.MarketId, l.ProductId, p.TypeId, l.TotalWeight, l.TotalPrice, o.Currency))
            .AsNoTracking()
            .ToListAsync(ct);
    }

    /// <summary>Строки возвратов (вес и сумма по строкам) и шапки для контроля качества.</summary>
    private async Task<(List<RawReturnLine> Lines, List<RawReturnHeader> Headers)> RawReturnsAsync(DateOnly start, DateOnly end, CancellationToken ct)
    {
        var returned = options.ReturnStatuses;

        var lines = await (
                from l in db.LinkoOrderReturnLines
                join r in db.LinkoOrderReturns on l.ReturnId equals r.Id
                join p in db.LinkoProducts on l.ProductId equals (long?)p.Id into pj
                from p in pj.DefaultIfEmpty()
                where returned.Contains(r.Status) && r.CreatedDate >= start && r.CreatedDate <= end
                select new RawReturnLine(r.Id, r.Status, r.CreatedDate, r.BranchId, r.BranchName, r.AgentId, r.MarketId,
                    l.ProductId, p.TypeId, l.TotalWeight, l.Price * l.Amount))
            .AsNoTracking()
            .ToListAsync(ct);

        var headers = await db.LinkoOrderReturns.AsNoTracking()
            .Where(r => returned.Contains(r.Status) && r.CreatedDate >= start && r.CreatedDate <= end)
            .Select(r => new RawReturnHeader(r.Id, r.Status, r.CreatedDate, r.BranchName, r.TotalWeight,
                r.Lines.Count, r.Lines.Sum(x => x.TotalWeight)))
            .ToListAsync(ct);

        return (lines, headers);
    }

    /// <summary>
    /// Проданные заказы без даты приёмки, созданные в окне: признак неполной загрузки (копия старше поля accepted_time).
    /// При дате реализации не по приёмке не считается.
    /// </summary>
    private async Task<int> DeliveredWithoutAcceptanceAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (options.DateField != SaleDateField.Accepted)
        {
            return 0;
        }

        var sold = options.SoldStatuses;
        return await db.LinkoOrders.CountAsync(o => sold.Contains(o.Status) && o.AcceptedDate == null && o.CreatedDate >= from && o.CreatedDate <= to, ct);
    }

    /// <summary>Филиалы, исключённые из вторички (по названию филиала в заказах).</summary>
    private async Task<HashSet<long>> ExcludedBranchIdsAsync(CancellationToken ct) =>
        await Cached("sales:excluded-branches", async () =>
        {
            var excluded = ExcludedLower;
            var ids = await db.LinkoOrders
                .Where(o => o.BranchId != null && o.BranchName != null && excluded.Contains(o.BranchName.ToLower()))
                .Select(o => o.BranchId!.Value)
                .Distinct()
                .ToListAsync(ct);
            return ids.ToHashSet();
        });

    /// <summary>
    /// АКБ давних месяцев — тяжёлый агрегат по всем строкам. Эти месяцы почти не меняются, поэтому кэш живёт дольше
    /// и не сбрасывается обычной синхронизацией (только полной загрузкой или очисткой).
    /// </summary>
    private async Task<IReadOnlyList<MonthlyAkb>> AkbHistoryAsync(DateOnly from, DateOnly to, IReadOnlyDictionary<long, long> groups, CancellationToken ct)
    {
        var merged = groups.Where(g => g.Key != g.Value).OrderBy(g => g.Key).ToDictionary();
        var key = $"sales:akb:v2:{from:yyyy-MM-dd}:{to:yyyy-MM-dd}:{options.DateField}:{string.Join(",", ExcludedLower)}:{string.Join(",", merged.Select(g => $"{g.Key}>{g.Value}"))}";
        if (cache.TryGetValue(key, out IReadOnlyList<MonthlyAkb>? cached) && cached is not null)
        {
            return cached;
        }

        var rows = await history.AkbByMonthAsync(from, to, merged, ct);
        cache.Set(key, rows, new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(HistoryCacheTtl)
            .AddExpirationToken(signal.HistoryToken));
        return rows;
    }

    /// <summary>«Живой» ассортимент: SKU, проданные во вторичке (без исключённых филиалов) за период.</summary>
    private async Task<HashSet<long>> ActiveSkusAsync(DateOnly start, DateOnly end, CancellationToken ct)
    {
        var sold = options.SoldStatuses;
        var excluded = ExcludedLower;
        var ids = await (
                from l in db.LinkoOrderLines
                join o in Dated() on l.OrderId equals o.Id
                where sold.Contains(o.Status) && o.Date >= start && o.Date <= end && l.ProductId != null
                      && (o.BranchName == null || !excluded.Contains(o.BranchName.ToLower()))
                select l.ProductId!.Value)
            .Distinct()
            .ToListAsync(ct);
        return ids.ToHashSet();
    }

    /// <summary>
    /// Факт кг по месяцам в разрезе агента и филиала: заказы по дате реализации минус возвраты по строкам
    /// (по дате возврата), без исключённых филиалов.
    /// </summary>
    private async Task<List<MonthlyFact>> HistoryAsync(DateOnly start, DateOnly end, CancellationToken ct)
    {
        var sold = options.SoldStatuses;
        var returned = options.ReturnStatuses;
        var excluded = ExcludedLower;

        var sales = await Dated()
            .Where(o => sold.Contains(o.Status) && o.Date >= start && o.Date <= end && (o.BranchName == null || !excluded.Contains(o.BranchName.ToLower())))
            .GroupBy(o => new { o.Date!.Value.Year, o.Date!.Value.Month, o.AgentId, o.BranchId })
            .Select(g => new MonthlyFact(g.Key.Year, g.Key.Month, g.Key.AgentId, g.Key.BranchId, g.Sum(o => o.TotalWeight)))
            .ToListAsync(ct);

        var returns = await (
                from l in db.LinkoOrderReturnLines
                join r in db.LinkoOrderReturns on l.ReturnId equals r.Id
                where returned.Contains(r.Status) && r.CreatedDate >= start && r.CreatedDate <= end
                      && (r.BranchName == null || !excluded.Contains(r.BranchName.ToLower()))
                group l by new { r.CreatedDate.Year, r.CreatedDate.Month, r.AgentId, r.BranchId } into g
                select new MonthlyFact(g.Key.Year, g.Key.Month, g.Key.AgentId, g.Key.BranchId, -g.Sum(x => x.TotalWeight)))
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
