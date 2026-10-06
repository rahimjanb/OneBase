using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Sales.SkuSales;
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

    /// <summary>
    /// «Продажи по SKU» за дни [from; to] (любые месяцы, в том числе через год) — по правилам вторички (SecondarySales): заказы проданных
    /// статусов по дате реализации, возвраты по строкам (сумма — количество × цена) по дате создания, без исключённых и пропускаемых
    /// филиалов; выручка заказа в другой валюте — 0, вес учитывается. branchRegions — филиал Linko → регион (старые филиалы — в текущем
    /// регионе, как OldBranches.Merge); филиал вне словаря — регион null. Кг и выручка — по товару × региону × месяцу; АКБ SKU за весь
    /// отрезок — различные ТТ с положительной месячной строкой товара (кг или выручка больше нуля) хотя бы в одном месяце.
    /// </summary>
    Task<SkuSalesAggregate> SkuSalesAsync(DateOnly from, DateOnly to, IReadOnlyDictionary<long, Guid> branchRegions, CancellationToken ct);
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

    /// <summary>Заказ с датой реализации по настройке (по умолчанию — дата приёмки). Вес и сумма — только из строк, шапка заказа не берётся.</summary>
    private sealed class DatedOrder
    {
        public long Id { get; init; }
        public DateOnly? Date { get; init; }
        public string Status { get; init; } = "";
        public string? BranchName { get; init; }
        public long? BranchId { get; init; }
        public long? AgentId { get; init; }
        public long? MarketId { get; init; }
    }

    private IQueryable<DatedOrder> Dated() => options.DateField switch
    {
        SaleDateField.Accepted => db.LinkoOrders.Select(o => new DatedOrder
        {
            Id = o.Id, Date = o.AcceptedDate, Status = o.Status, BranchName = o.BranchName, BranchId = o.BranchId, AgentId = o.AgentId, MarketId = o.MarketId,
        }),
        SaleDateField.Delivery => db.LinkoOrders.Select(o => new DatedOrder
        {
            Id = o.Id, Date = o.DeliveryDate ?? o.CreatedDate, Status = o.Status, BranchName = o.BranchName, BranchId = o.BranchId, AgentId = o.AgentId, MarketId = o.MarketId,
        }),
        _ => db.LinkoOrders.Select(o => new DatedOrder
        {
            Id = o.Id, Date = o.CreatedDate, Status = o.Status, BranchName = o.BranchName, BranchId = o.BranchId, AgentId = o.AgentId, MarketId = o.MarketId,
        }),
    };

    /// <summary>Исключённые филиалы в нижнем регистре — для SQL (lower(branch) in …).</summary>
    private string[] ExcludedLower => options.ExcludedBranches.Select(b => b.Trim().ToLowerInvariant()).ToArray();

    /// <summary>Пропускаемые филиалы («К К Мерч») в нижнем регистре.</summary>
    private string[] IgnoredLower => options.IgnoredBranches.Select(b => b.Trim().ToLowerInvariant()).ToArray();

    /// <summary>Филиалы не вторички (исключённые и пропускаемые) в нижнем регистре.</summary>
    private string[] NotSecondaryLower => options.NotSecondaryBranchesLower();

    /// <summary>Месяцы, за которые есть продажи вторички (по дате реализации, до отчётного дня) — от новых к старым.</summary>
    public async Task<IReadOnlyList<SalesMonth>> MonthsAsync(CancellationToken ct = default) =>
        await Cached("sales:months", async () =>
        {
            var sold = options.SoldStatuses;
            var skipped = NotSecondaryLower;
            // Приёмка сегодня или «в будущем» (бывает у части заказов) не должна давать в выборе периода месяц, которого в отчёте ещё нет.
            var cutoff = ReportCutoff;
            var months = await Dated()
                .Where(o => sold.Contains(o.Status) && o.Date != null && o.Date <= cutoff && (o.BranchName == null || !skipped.Contains(o.BranchName.ToLower())))
                .Select(o => new { o.Date!.Value.Year, o.Date!.Value.Month })
                .Distinct()
                .ToListAsync(ct);

            return (IReadOnlyList<SalesMonth>)months.OrderByDescending(m => m.Year).ThenByDescending(m => m.Month)
                .Select(m => new SalesMonth(m.Year, m.Month))
                .ToList();
        });

    /// <summary>Отчёт месяца по плану по умолчанию — РОП: AI-инструменты, проактивные проверки, прогрев после синхронизации.</summary>
    public Task<SalesAnalytics> LoadAsync(int? year, int? month, CancellationToken ct = default) => LoadAsync(year, month, PlanKind.Rop, ct);

    /// <summary>
    /// Отчёт месяца; plan — план вторички: РОП или «Завод» (sales."RegionPlans"). На месяц без планов регионов этого вида
    /// план подразделений — сумма планов ТП из Linko. Вид плана — часть ключа кэша.
    /// </summary>
    public async Task<SalesAnalytics> LoadAsync(int? year, int? month, PlanKind plan, CancellationToken ct = default)
    {
        var lastData = await Cached("sales:last-data", () => LastDataDateAsync(ct));
        var y = year ?? lastData?.Year ?? DateTime.Today.Year;
        var m = month is >= 1 and <= 12 ? month.Value : lastData?.Month ?? DateTime.Today.Month;
        var kind = SalesPlans.Secondary(plan);

        var key = SalesPlans.CacheKey(y, m, kind);
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

    /// <summary>Отчётный день (последний день с продажами вторички, не позже вчера) — для отчётов за несколько месяцев («Продажи по SKU»).</summary>
    public Task<DateOnly?> LastDataAsync(CancellationToken ct = default) => Cached("sales:last-data", () => LastDataDateAsync(ct));

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
    /// Отчётный день: последний день с продажами вторички (по дате реализации, без «Завода» и пропускаемых филиалов), но не позже
    /// последнего полного дня — вчера по местному времени. Сегодняшние приёмки войдут в отчёт завтра; у части заказов Linko
    /// дата приёмки стоит в будущем — отчётный день из-за них не уезжает вперёд.
    /// </summary>
    private async Task<DateOnly?> LastDataDateAsync(CancellationToken ct)
    {
        var sold = options.SoldStatuses;
        var skipped = NotSecondaryLower;
        var cutoff = ReportCutoff;
        return await Dated()
            .Where(o => sold.Contains(o.Status) && o.Date <= cutoff && (o.BranchName == null || !skipped.Contains(o.BranchName.ToLower())))
            .MaxAsync(o => o.Date, ct);
    }

    /// <summary>Последний полный день по местному времени (вчера) — см. SecondarySales.ReportCutoff.</summary>
    private static DateOnly ReportCutoff => SecondarySales.ReportCutoff(DateTimeOffset.UtcNow);

    /// <summary>
    /// Заказ вторички по филиалу: без филиала или не «Завод» и не «К К Мерч» (skippedLower — их названия в нижнем регистре, NotSecondaryLower).
    /// Для запросов к заказам напрямую; выборки по дате реализации (Dated) проверяют то же самое.
    /// </summary>
    public static Expression<Func<LinkoOrder, bool>> SecondaryBranch(string[] skippedLower) =>
        o => o.BranchName == null || !skippedLower.Contains(o.BranchName.ToLower());

    /// <summary>
    /// Проданные заказы вторички этого месяца, принятые после отчётного дня — сегодня или с датой приёмки в будущем (окно —
    /// SecondarySales.AcceptedAfterReport): в факт попадут на следующий день после приёмки. У закрытого месяца окна нет — 0.
    /// </summary>
    private async Task<int> AcceptedInFutureAsync(DateOnly monthStart, DateOnly dataThrough, CancellationToken ct)
    {
        if (options.DateField != SaleDateField.Accepted || SecondarySales.AcceptedAfterReport(monthStart, dataThrough) is not { } window)
        {
            return 0;
        }

        var sold = options.SoldStatuses;
        var (from, to) = window;
        return await db.LinkoOrders
            .Where(SecondaryBranch(NotSecondaryLower))
            .CountAsync(o => sold.Contains(o.Status) && o.AcceptedDate >= from && o.AcceptedDate <= to, ct);
    }

    private async Task<MonthData> BuildAsync(int year, int month, PlanKind kind, DateOnly? lastData, CancellationToken ct)
    {
        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var previousStart = monthStart.AddMonths(-1);

        var dataThrough = SecondarySales.DataThrough(monthStart, lastData);
        var salesEnd = dataThrough < monthStart ? monthEnd : dataThrough;

        // Сырые строки: заказы, реализованные с начала прошлого месяца, и заказы, созданные в месяце (для визитов);
        // возвраты с начала прошлого месяца. Правила применяет SecondarySales.
        var rawOrders = await RawOrdersAsync(previousStart, salesEnd, monthStart, salesEnd, ct);
        var (rawReturns, returnHeaders) = await RawReturnsAsync(previousStart, salesEnd, ct);
        var missingAcceptance = await DeliveredWithoutAcceptanceAsync(monthStart.AddDays(-45), salesEnd, ct);

        var current = SecondarySales.Build(rawOrders, rawReturns, returnHeaders, monthStart, dataThrough, options, missingAcceptance);
        current = current with { Quality = current.Quality with { AcceptedInFuture = await AcceptedInFutureAsync(monthStart, dataThrough, ct) } };
        var previous = SecondarySales.Build(rawOrders, rawReturns, returnHeaders, previousStart, monthStart.AddDays(-1), options);
        var historyFrom = new[] { new DateOnly(year, 1, 1), monthStart.AddMonths(-3) }.Min();

        var visits = await db.LinkoVisits.AsNoTracking()
            .Where(v => v.Day >= monthStart && v.Day <= monthEnd && v.UserId != null && v.MarketId != null)
            .Select(v => new { v.Day, v.UserId, v.MarketId, v.Status, v.IsInPlan })
            .ToListAsync(ct);

        // Планы ТП из Linko (API планов, staff_balance): план ТП — его план по весу; по ним карточка ТП и команда региона.
        // Планом региона и республики они становятся, только если на месяц нет планов регионов выбранного вида (см. SalesPlans).
        // Год месяца и следующий месяц: для карточки «План на следующий месяц».
        var next = new DateOnly(year, month, 1).AddMonths(1);
        var staff = await db.SalesStaffPlans.AsNoTracking()
            .Where(p => p.Year == year || (p.Year == next.Year && p.Month == next.Month))
            .ToListAsync(ct);
        var staffIds = staff.Select(s => s.LinkoUserId).Distinct().ToList();
        var excludedStaff = (await db.LinkoUsers.AsNoTracking()
                .Where(u => staffIds.Contains(u.Id))
                .Select(u => new { u.Id, u.JobName })
                .ToListAsync(ct))
            .Where(u => u.JobName is { } job && options.StaffPlanExcludeJobs.Any(x => job.Contains(x, StringComparison.OrdinalIgnoreCase)))
            .Select(u => u.Id)
            .ToHashSet();
        var weight = staff.Where(s => s.PlanType == StaffPlanTypes.SalesWeight && !excludedStaff.Contains(s.LinkoUserId)).ToList();
        var yearAgentPlans = weight
            .Where(s => s.Year == year)
            .GroupBy(s => (AgentId: (long?)s.LinkoUserId, s.Month))
            .Select(g => new PlanRow(null, g.Key.AgentId, g.Key.Month, null, g.Sum(s => s.PlanAmount)))
            .ToList();
        var agentPlans = yearAgentPlans.Where(p => p.Month == month).ToList();
        var nextPlans = weight
            .Where(s => s.Year == next.Year && s.Month == next.Month)
            .GroupBy(s => s.LinkoUserId)
            .Select(g => new PlanRow(null, g.Key, next.Month, null, g.Sum(s => s.PlanAmount)))
            .ToList();

        // Справочник ТП — пользователи Linko с должностью ТП (Sales:SalesRepJobs); вакансия — «вакан» в имени («вакант», «Вакан …»;
        // Sales:VacancyMarkers) или ID 0. У Sales Base своё слово — «вакант» (Sales:FieldVacancyMarkers).
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

        // План ТП по категориям — по названию весового показателя Linko; текущий и следующий месяц.
        var categoryPlans = weight
            .Where(s => s.PlanAmount > 0 && ((s.Year == year && s.Month == month) || (s.Year == next.Year && s.Month == next.Month)))
            .Select(s => new CategoryPlanRow(s.LinkoUserId, s.Year, s.Month,
                options.PlanCategoriesOf(s.IndicatorName).Select(categoryMap.GroupByName).OfType<long>().Distinct().ToList(),
                s.PlanAmount))
            .ToList();
        var yearStart = new DateOnly(year, 1, 1);
        var akbHistoryTo = previousStart.AddDays(-1); // прошлый и текущий месяц считаются из строк продаж
        IReadOnlyList<MonthlyAkb> akbHistory = yearStart <= akbHistoryTo
            ? await AkbHistoryAsync(yearStart, akbHistoryTo, categoryMap.Mapping, ct)
            : [];

        // Регионы — филиалы вторички: исключённые («Завод») в структуре не показываются, у них отдельный блок;
        // пропускаемых («К К Мерч») во вторичке нет совсем — ни региона, ни визитов в их точках.
        // Старые филиалы («Жиззах (эски)») считаются в текущем регионе с тем же названием.
        var excludedBranchIds = await BranchIdsAsync("sales:excluded-branches", ExcludedLower, ct);
        var ignoredBranchIds = await BranchIdsAsync("sales:ignored-branches", IgnoredLower, ct);
        // Оргструктура — данные OneBase («Настройки продаж»): направления (РМ, каналы), у региона — направление, СВР и дилер; в Linko их нет.
        var structure = SalesStructure.Build(
            await db.SalesDirections.AsNoTracking().ToListAsync(ct),
            await db.SalesRegions.AsNoTracking().ToListAsync(ct),
            excludedBranchIds.Concat(ignoredBranchIds).ToHashSet(),
            options.OldBranchSuffix,
            options.ChannelRegions); // регионы-каналы (базар, сети): их строки — только в закрытом месяце (SecondarySales.ChannelsOn)

        // План вторички — РОП или «Завод» из sales."RegionPlans" (год месяца и следующий месяц); на месяц без них — планы ТП из Linko.
        // Читаются оба вида: план — выбранного, а по обоим — какие планы на месяц вообще заведены (переключатель плана).
        var regionPlans = await db.SalesRegionPlans.AsNoTracking()
            .Where(p => (p.Kind == PlanKind.Rop || p.Kind == PlanKind.Factory) && (p.Year == year || (p.Year == next.Year && p.Month == next.Month)))
            .Select(p => new RegionPlanRow(p.RegionId, p.Kind, p.Year, p.Month, p.CategoryId, p.PlanKg))
            .ToListAsync(ct);
        var plans = SalesPlans.Select(year, month, kind, regionPlans, structure.RegionOf, agentPlans);

        var markets = await db.LinkoMarkets.AsNoTracking()
            .Select(x => new MarketInfo(x.Id, x.Name, x.ResponsibleAgentId, x.BranchId))
            .ToDictionaryAsync(x => x.Id, ct);
        var ignoredMarkets = markets.Values.Where(m => m.BranchId is { } b && ignoredBranchIds.Contains(b)).Select(m => m.Id).ToHashSet();

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
            Visits = visits.Where(v => !nonSales.Contains(v.UserId!.Value) && !ignoredMarkets.Contains(v.MarketId!.Value))
                .Select(v => new VisitRecord(v.Day, v.UserId!.Value, v.MarketId!.Value, ParseStatus(v.Status), v.IsInPlan)).ToList(),
            // Текущий месяц в истории — по отчётный день, как плитка факта.
            History = await HistoryAsync(historyFrom, dataThrough, ct),
            Plans = plans.Plans,
            YearRegionPlans = plans.YearRegionPlans,
            NextRegionPlans = plans.NextRegionPlans,
            SelectedPlan = SalesPlans.SourceOf(kind),
            AvailablePlans = SalesPlans.Available(year, month, regionPlans, structure.RegionOf),
            PlanSource = plans.Source,
            YearAgentPlans = yearAgentPlans,
            CategoryPlans = categoryPlans,
            NextMonth = (next.Year, next.Month),
            NextPlans = nextPlans,
            Indicators = staff
                .Where(s => s.Year == year && s.Month == month)
                .Select(s => new StaffIndicator(s.LinkoUserId, s.IndicatorId, CleanIndicatorName(s.IndicatorName), s.PlanType, s.PlanAmount, s.FactAmount))
                .ToList(),
            RevenuePlans = staff
                .Where(s => s.Year == year && s.Month == month && s.PlanType == StaffPlanTypes.SalesSum && !excludedStaff.Contains(s.LinkoUserId))
                .GroupBy(s => s.LinkoUserId)
                .Select(g => new PlanRow(null, g.Key, month, null, g.Sum(s => s.PlanAmount)))
                .ToList(),
            TeamPlanStaff = excludedStaff,
            Agents = users.ToDictionary(u => u.Id, u => new AgentInfo(
                u.Id,
                u.DisplayName,
                u.IsActive,
                null,
                options.IsVacancy(u.Id, $"{u.DisplayName} {u.Username}"),
                u.JobName is { } job && options.SalesRepJobs.Any(j => string.Equals(j.Trim(), job.Trim(), StringComparison.OrdinalIgnoreCase)),
                u.JobName)),
            Regions = structure.Regions,
            BranchAliases = structure.BranchAliases,
            Directions = structure.Directions,
            Markets = markets,
            Categories = linkoTypes,
            CategoryMap = categoryMap,
            AkbHistory = akbHistory,
            Products = await db.LinkoProducts.AsNoTracking()
                .Select(p => new ProductInfo(p.Id, p.Name, p.Code, p.TypeId))
                .ToDictionaryAsync(p => p.Id, ct),
            ActiveSkus = await ActiveSkusAsync(AssortmentWindow(monthStart, salesEnd), ct),
            Top = TopProductSet.Of(options),
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
            AkbChartHiddenCategories = options.AkbChartHiddenCategories,
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
    /// Проданные заказы вторички без даты приёмки, созданные в окне: признак неполной загрузки (копия старше поля accepted_time).
    /// Без «Завода» и «К К Мерч», как остальные выборки вторички. При дате реализации не по приёмке не считается.
    /// </summary>
    private async Task<int> DeliveredWithoutAcceptanceAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (options.DateField != SaleDateField.Accepted)
        {
            return 0;
        }

        var sold = options.SoldStatuses;
        return await db.LinkoOrders
            .Where(SecondaryBranch(NotSecondaryLower))
            .CountAsync(o => sold.Contains(o.Status) && o.AcceptedDate == null && o.CreatedDate >= from && o.CreatedDate <= to, ct);
    }

    /// <summary>Филиалы с этими названиями (в нижнем регистре) — по названию филиала в заказах.</summary>
    private async Task<HashSet<long>> BranchIdsAsync(string key, string[] names, CancellationToken ct) =>
        await Cached(key, async () =>
        {
            var ids = await db.LinkoOrders
                .Where(o => o.BranchId != null && o.BranchName != null && names.Contains(o.BranchName.ToLower()))
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
        var key = $"sales:akb:v4:{from:yyyy-MM-dd}:{to:yyyy-MM-dd}:{options.DateField}:{string.Join(",", options.SoldStatuses)}:" +
                  $"{string.Join(",", NotSecondaryLower)}:{string.Join(",", merged.Select(g => $"{g.Key}>{g.Value}"))}";
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

    /// <summary>
    /// Окно ассортимента («из M SKU»): с 1 января года выбранного месяца по его конец — salesEnd (у идущего месяца — отчётный день).
    /// Не полгода назад и не будущие месяцы: SKU, впервые проданный после выбранного месяца, в его знаменатель не входит.
    /// </summary>
    public static (DateOnly From, DateOnly To) AssortmentWindow(DateOnly monthStart, DateOnly salesEnd) => (new DateOnly(monthStart.Year, 1, 1), salesEnd);

    /// <summary>
    /// Ассортимент — знаменатель «продаётся N из M SKU»: SKU с продажами во вторичке за окно (проданные статусы, по дате реализации,
    /// без «Завода» и пропускаемых филиалов). Каталог Linko целиком сюда не входит; SKU, которые продавал только «Завод», — тоже.
    /// </summary>
    private async Task<HashSet<long>> ActiveSkusAsync((DateOnly From, DateOnly To) window, CancellationToken ct)
    {
        var (start, end) = window;
        var sold = options.SoldStatuses;
        var skipped = NotSecondaryLower;
        var ids = await (
                from l in db.LinkoOrderLines
                join o in Dated() on l.OrderId equals o.Id
                where sold.Contains(o.Status) && o.Date >= start && o.Date <= end && l.ProductId != null
                      && (o.BranchName == null || !skipped.Contains(o.BranchName.ToLower()))
                select l.ProductId!.Value)
            .Distinct()
            .ToListAsync(ct);
        return ids.ToHashSet();
    }

    /// <summary>
    /// Факт кг по месяцам в разрезе агента и филиала — по правилам факта месяца: строки проданных заказов по дате реализации
    /// (вес строк, а не шапки заказа) минус возвраты по строкам (по дате возврата), без исключённых и пропускаемых филиалов.
    /// </summary>
    private async Task<List<MonthlyFact>> HistoryAsync(DateOnly start, DateOnly end, CancellationToken ct)
    {
        var sold = options.SoldStatuses;
        var returned = options.ReturnStatuses;
        var skipped = NotSecondaryLower;

        var sales = await (
                from l in db.LinkoOrderLines
                join o in Dated() on l.OrderId equals o.Id
                where sold.Contains(o.Status) && o.Date >= start && o.Date <= end && (o.BranchName == null || !skipped.Contains(o.BranchName.ToLower()))
                group l by new { o.Date!.Value.Year, o.Date!.Value.Month, o.AgentId, o.BranchId } into g
                select new MonthlyFact(g.Key.Year, g.Key.Month, g.Key.AgentId, g.Key.BranchId, g.Sum(x => x.TotalWeight)))
            .ToListAsync(ct);

        var returns = await (
                from l in db.LinkoOrderReturnLines
                join r in db.LinkoOrderReturns on l.ReturnId equals r.Id
                where returned.Contains(r.Status) && r.CreatedDate >= start && r.CreatedDate <= end
                      && (r.BranchName == null || !skipped.Contains(r.BranchName.ToLower()))
                group l by new { r.CreatedDate.Year, r.CreatedDate.Month, r.AgentId, r.BranchId } into g
                select new MonthlyFact(g.Key.Year, g.Key.Month, g.Key.AgentId, g.Key.BranchId, -g.Sum(x => x.TotalWeight)))
            .ToListAsync(ct);

        return sales.Concat(returns).Concat(await ChannelHistoryAsync(start, end, ct)).ToList();
    }

    /// <summary>
    /// Помесячный факт точек-каналов (Sales:ChannelRegions) из филиалов первички — только за закрытые месяцы (как SecondarySales.Build)
    /// не раньше первого месяца канала (Since), в виртуальном филиале своего региона-канала.
    /// </summary>
    private async Task<List<MonthlyFact>> ChannelHistoryAsync(DateOnly start, DateOnly end, CancellationToken ct)
    {
        var afterCutoff = ReportCutoff.AddDays(1);
        var closedThrough = new DateOnly(afterCutoff.Year, afterCutoff.Month, 1).AddDays(-1);
        var to = end < closedThrough ? end : closedThrough;
        var markets = options.ChannelRegions.SelectMany(c => c.Markets).Distinct().ToArray();
        if (markets.Length == 0 || to < start)
        {
            return [];
        }

        var sold = options.SoldStatuses;
        var returned = options.ReturnStatuses;
        var branches = options.PrimaryOrderBranches.Select(b => b.Trim().ToLowerInvariant()).ToArray();

        var sales = await (
                from l in db.LinkoOrderLines
                join o in Dated() on l.OrderId equals o.Id
                where sold.Contains(o.Status) && o.Date >= start && o.Date <= to && o.MarketId != null && markets.Contains(o.MarketId.Value)
                      && o.BranchName != null && branches.Contains(o.BranchName.ToLower())
                group l by new { o.Date!.Value.Year, o.Date!.Value.Month, o.AgentId, o.MarketId } into g
                select new { g.Key.Year, g.Key.Month, g.Key.AgentId, g.Key.MarketId, Kg = g.Sum(x => x.TotalWeight) })
            .ToListAsync(ct);

        var returns = await (
                from l in db.LinkoOrderReturnLines
                join r in db.LinkoOrderReturns on l.ReturnId equals r.Id
                where returned.Contains(r.Status) && r.CreatedDate >= start && r.CreatedDate <= to && r.MarketId != null
                      && markets.Contains(r.MarketId.Value) && r.BranchName != null && branches.Contains(r.BranchName.ToLower())
                group l by new { r.CreatedDate.Year, r.CreatedDate.Month, r.AgentId, r.MarketId } into g
                select new { g.Key.Year, g.Key.Month, g.Key.AgentId, g.Key.MarketId, Kg = -g.Sum(x => x.TotalWeight) })
            .ToListAsync(ct);

        return sales.Concat(returns)
            .Select(x => (Row: x, Channel: options.ChannelOf(x.MarketId)!.Value))
            .Where(x => x.Channel.Since is not { } since || new DateOnly(x.Row.Year, x.Row.Month, 1) >= since) // канал с первого своего месяца
            .Select(x => new MonthlyFact(x.Row.Year, x.Row.Month, x.Row.AgentId, x.Channel.BranchId, x.Row.Kg))
            .ToList();
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
