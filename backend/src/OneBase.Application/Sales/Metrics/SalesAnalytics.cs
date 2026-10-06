using System.Collections.Concurrent;

namespace OneBase.Application.Sales.Metrics;

/// <summary>
/// Расчёт всех блоков аналитики за месяц. Не обращается к БД — только к MonthData.
/// Факт региона считается по branch строк продаж, поэтому:
/// сумма по регионам = итог республики; сумма по ТП региона + факт без агента = итог региона.
/// </summary>
public sealed partial class SalesAnalytics
{
    public static readonly Guid NoRegionId = Guid.Empty;
    public const string NoDirectionId = "none";

    private readonly MonthData _d;
    private readonly Dictionary<Guid, RegionInfo> _regions;
    private readonly Dictionary<long, Guid> _regionByBranch;
    private readonly Dictionary<Guid, DirectionInfo> _directions;
    private readonly ILookup<Guid, SaleLine> _currentByRegion;
    private readonly ILookup<Guid, SaleLine> _previousByRegion;
    private readonly ILookup<long, SaleLine> _currentByAgent;
    private readonly ILookup<long, SaleLine> _previousByAgent;
    private readonly ILookup<long, VisitRecord> _visitsByAgent;
    private readonly Dictionary<long, Guid> _agentRegion = [];
    private readonly Dictionary<long, Guid> _marketRegion = [];
    private readonly HashSet<long> _roster;
    private readonly Dictionary<long, AgentStats> _stats = [];
    private readonly Dictionary<Guid, RegionMedians> _medians = [];
    private readonly Dictionary<long, IReadOnlyList<AgentFlag>> _flags = [];
    private readonly SalesCategories _cats;
    private readonly ILookup<long, SaleLine> _visitByAgent;
    private readonly DateOnly _previousStart;
    private readonly DateOnly _previousCutoff;

    /// <summary>Выполненные визиты месяца по плану и без плана: (ТТ, день) любого ТП — сшивка заказа с визитом в календаре визитов.</summary>
    private readonly HashSet<(long Market, DateOnly Date)> _doneInPlan;
    private readonly HashSet<(long Market, DateOnly Date)> _doneOffPlan;

    /// <summary>Линий категорий в «АКБ по месяцам» — не больше семи (DOC-filters §1), остальные категории есть в карточках.</summary>
    private const int AkbChartMaxCategories = 7;

    public SalesAnalytics(MonthData data)
    {
        _d = data;
        _regions = data.Regions.ToDictionary(r => r.Id);
        _regions[NoRegionId] = new RegionInfo(NoRegionId, 0, "Без региона", null, null, null);
        _regionByBranch = data.Regions.ToDictionary(r => r.BranchId, r => r.Id);
        foreach (var (branch, region) in data.BranchAliases)
        {
            _regionByBranch.TryAdd(branch, region); // старый филиал («Жиззах (эски)») — в текущем регионе
        }
        _directions = data.Directions.ToDictionary(x => x.Id);
        _cats = data.CategoryMap ?? SalesCategories.Build(new Dictionary<string, string>(), data.Categories);

        _currentByRegion = data.Current.ToLookup(RegionOf);
        _previousByRegion = data.Previous.ToLookup(RegionOf);
        _currentByAgent = data.Current.Where(l => l.AgentId != null).ToLookup(l => l.AgentId!.Value);
        _previousByAgent = data.Previous.Where(l => l.AgentId != null).ToLookup(l => l.AgentId!.Value);
        _visitsByAgent = data.Visits.ToLookup(v => v.AgentId);
        _visitByAgent = data.VisitLines.Where(l => l.AgentId != null).ToLookup(l => l.AgentId!.Value);
        var done = data.Visits.Where(v => v.Status == VisitStatus.Done).ToList();
        _doneInPlan = done.Where(v => v.InPlan).Select(v => (v.MarketId, v.Date)).ToHashSet();
        _doneOffPlan = done.Where(v => !v.InPlan).Select(v => (v.MarketId, v.Date)).ToHashSet();

        _previousStart = data.MonthStart.AddMonths(-1);
        _previousCutoff = SalesMath.SameDaysCutoff(_previousStart, Math.Max(1, data.WorkedDays));

        ResolveRegions();
        _roster = BuildRoster();
        ComputeAgentStats();
    }

    public PeriodInfo Period => new(_d.Year, _d.Month, _d.DataThrough, _d.WorkedDays, _d.DaysInMonth, _previousCutoff, _d.SelectedPlan, _d.AvailablePlans);

    // ================= Готовые ответы =================
    // SalesAnalytics живёт в кэше, пока не изменились данные, поэтому посчитанный уровень можно отдавать повторно:
    // переходы между страницами не пересчитывают одно и то же.

    private readonly ConcurrentDictionary<string, Lazy<object?>> _views = new();

    /// <summary>Посчитанный ответ по ключу. null тоже запоминается: «такой категории в охвате нет» не пересчитывается.</summary>
    private T View<T>(string key, Func<T> build) where T : class? =>
        (T)_views.GetOrAdd(key, _ => new Lazy<object?>(() => build(), LazyThreadSafetyMode.ExecutionAndPublication)).Value!;

    /// <summary>Дни календаря визитов: по умолчанию с 1-го по последний день данных.</summary>
    public (DateOnly From, DateOnly To) VisitRange(int? from, int? to)
    {
        var last = Math.Max(1, _d.WorkedDays);
        var f = Math.Clamp(from ?? 1, 1, _d.DaysInMonth);
        var t = Math.Clamp(to ?? last, f, _d.DaysInMonth);
        return (new DateOnly(_d.Year, _d.Month, f), new DateOnly(_d.Year, _d.Month, t));
    }

    public OverviewView CachedOverview() => View("overview", Overview);

    /// <summary>metric и category — календарь месяца по регионам (kg | sum | akb, АКБ по категории), как у календаря региона.</summary>
    public GroupView CachedRepublic(int? from, int? to, string metric = "kg", long? category = null)
    {
        var (f, t) = VisitRange(from, to);
        return View($"republic:{f}:{t}:{metric}:{category}", () => Republic(f, t, metric, category));
    }

    public GroupView CachedDirection(string id, int? from, int? to, string metric = "kg", long? category = null)
    {
        var (f, t) = VisitRange(from, to);
        return View($"direction:{id}:{f}:{t}:{metric}:{category}", () => Direction(id, f, t, metric, category));
    }

    public RegionView CachedRegion(Guid id, int? from, int? to, string metric, long? category)
    {
        var (f, t) = VisitRange(from, to);
        return View($"region:{id}:{f}:{t}:{metric}:{category}", () => Region(id, f, t, metric, category));
    }

    public AgentView CachedAgent(long id) => View($"agent:{id}", () => Agent(id));

    public PlansView CachedPlans() => View("plans", Plans);

    public ProblemsView CachedProblems(string? direction, FlagKind? criterion, bool vacancies) =>
        View($"problems:{direction}:{criterion}:{vacancies}", () => Problems(direction, criterion, vacancies));

    public bool HasRegion(Guid id) => _regions.ContainsKey(id);

    public bool HasDirection(string id) => id == NoDirectionId || (Guid.TryParse(id, out var g) && _directions.ContainsKey(g));

    public bool HasAgent(long id) => _roster.Contains(id) || _d.Agents.ContainsKey(id);

    // ================= Уровни =================

    public OverviewView Overview()
    {
        var agents = AgentsIn(null).Where(IsSalesRep).ToList();
        return new OverviewView(Period, Kpi(null), agents.Count(a => !IsVacancy(a)), FlagCountsOf(agents),
            agents.Count(IsVacancy), Unit("republic", "Республика", null, null, UnitKinds.Republic), Excluded());
    }

    /// <summary>
    /// Республика: карточки РМ/направлений (если они заведены и в них есть регионы), дальше — категории. Карточек регионов нет:
    /// регионы — в таблице «Все регионы». Служебная группа «Без направления» не показывается.
    /// </summary>
    public GroupView Republic(DateOnly visitsFrom, DateOnly visitsTo, string calendarMetric = "kg", long? calendarCategory = null)
    {
        var directionCards = DirectionGroups()
            .Where(g => g.Id != NoDirectionId && g.Regions.Count > 0)
            .Select(g => Unit(g.Id, g.Name, g.Subtitle, g.Regions, UnitKinds.Direction))
            .ToList();

        var regions = RegionIds(null).Count(r => r != NoRegionId);
        var parts = new List<string>();
        if (directionCards.Count > 0)
        {
            parts.Add($"{directionCards.Count} {SalesFormat.Plural(directionCards.Count, "направление", "направления", "направлений")}");
        }

        parts.Add($"{regions} {SalesFormat.Plural(regions, "регион", "региона", "регионов")}");
        return Group("Республика", string.Join(" · ", parts), null, directionCards, visitsFrom, visitsTo, calendarMetric, calendarCategory);
    }

    public GroupView Direction(string id, DateOnly visitsFrom, DateOnly visitsTo, string calendarMetric = "kg", long? calendarCategory = null)
    {
        var group = DirectionGroups().FirstOrDefault(g => g.Id == id)
            ?? throw new KeyNotFoundException($"Направление {id} не найдено");
        var cards = group.Regions.Select(RegionUnit).ToList();
        return Group(group.Name, group.Subtitle, group.Regions, cards, visitsFrom, visitsTo, calendarMetric, calendarCategory);
    }

    public RegionView Region(Guid id, DateOnly visitsFrom, DateOnly visitsTo, string calendarMetric, long? calendarCategory)
    {
        var region = _regions[id];
        var scope = new HashSet<Guid> { id };
        var direction = region.DirectionId is { } dirId && _directions.TryGetValue(dirId, out var dir) ? dir : null;
        var team = TeamOf(id);
        var categories = CategoryCardsOf(scope);
        var notBought = AgentNotBought(id);
        var categoryPlans = CategoryPlansOf(scope);

        return new RegionView(
            Period,
            id.ToString(),
            region.Name,
            direction?.Id.ToString(),
            direction?.Name,
            region.Supervisor,
            region.Dealer,
            Kpi(scope),
            Unassigned(scope),
            MonthsOf(region),
            CategoriesOf(id),
            CalendarOf(_currentByRegion[id].ToList(), team.Select(a => (a.ToString(), AgentName(a))).ToList(), l => l.AgentId?.ToString(),
                calendarMetric, calendarCategory),
            [RegionVisitRow(id, visitsFrom, visitsTo)],
            team.Select(TeamRowOf(id)).OrderBy(t => t.IsVacancy).ThenByDescending(t => t.FactKg).ToList(),
            team.Select(a => Compare(a.ToString(), AgentName(a), $"ID {a}",
                    _currentByRegion[id].Where(l => l.AgentId == a), _previousByRegion[id].Where(l => l.AgentId == a)))
                .OrderByDescending(r => r.KgNow).ToList(),
            notBought,
            NotInDirectory(id),
            categories,
            AkbMonthsOf(scope, categories),
            QualityOf(scope),
            categoryPlans,
            NextMonthOf(scope, byCategory: true),
            NotBoughtTotal(region.Name, notBought),
            CategoryPlanTotal(categoryPlans),
            SalesOptions.IsChannelBranch(region.BranchId));
    }

    public AgentView Agent(long id)
    {
        var stats = StatsOf(id);
        var regionId = _agentRegion.GetValueOrDefault(id, NoRegionId);
        var region = _regions[regionId];
        var direction = region.DirectionId is { } dirId && _directions.TryGetValue(dirId, out var dir) ? dir : null;
        var medians = _medians.GetValueOrDefault(regionId) ?? new RegionMedians(null, null, null);

        var plans = _d.Plans.Where(p => p.AgentId == id).ToList();
        var plan = SalesMath.PlanTotal(plans);
        var revenuePlans = _d.RevenuePlans.Where(p => p.AgentId == id).ToList();
        decimal? revenuePlan = revenuePlans.Count == 0 ? null : revenuePlans.Sum(p => p.PlanKg);
        var indicators = _d.Indicators.Where(i => i.AgentId == id)
            .OrderBy(i => i.PlanType == "product_sales_weight" ? 0 : 1)
            .ThenByDescending(i => i.Plan)
            .Select(Indicator)
            .ToList();

        // «Категорий в плане»: категории весовых показателей Linko этого ТП.
        var categoryPlans = CurrentCategoryPlans().Where(p => p.AgentId == id).ToList();
        var planCategories = categoryPlans.SelectMany(p => p.Groups).Distinct().Count();

        var lines = _currentByAgent[id].ToList();
        var prevLines = _previousByAgent[id].ToList();
        var akb = SalesMath.Akb(lines);
        var execution = SalesMath.Ratio(stats.Kg, plan);
        var revenueExecution = SalesMath.Ratio(stats.Revenue, revenuePlan);

        // «Ещё не купили»: ТТ агента из прошлого месяца, которые в этом месяце не купили ни у кого.
        var boughtNow = SalesMath.ActiveMarkets(_d.Current);
        var prevMarkets = SalesMath.ActiveMarkets(prevLines);
        var silent = prevMarkets.Except(boughtNow)
            .Select(m => SilentOf(m, prevLines))
            .OrderByDescending(s => s.PrevRevenue)
            .ToList();

        // «Новые точки»: купили у агента в этом месяце, а в прошлом не покупали ни у кого.
        var boughtBefore = SalesMath.ActiveMarkets(_d.Previous);
        var newMarkets = SalesMath.ActiveMarkets(lines).Except(boughtBefore)
            .Select(m =>
            {
                var ml = lines.Where(l => l.MarketId == m).ToList();
                return new NewMarket(m, MarketName(m), ml.Sum(l => l.Kg), ml.Sum(l => l.Revenue));
            })
            .OrderByDescending(n => n.Revenue)
            .ToList();

        return new AgentView(
            Period,
            id,
            AgentName(id),
            regionId == NoRegionId ? null : regionId.ToString(),
            region.Name,
            direction?.Id.ToString(),
            direction?.Name,
            IsVacancy(id),
            plan,
            execution,
            stats.Kg,
            stats.Revenue,
            revenuePlan,
            revenueExecution,
            stats.Visits.Done,
            stats.Visits.WithOrder,
            new MedianValue(stats.Conversion, medians.Conversion),
            new MedianValue(stats.SumPerVisit, medians.SumPerVisit),
            new MedianValue(stats.AvgCheck, medians.AvgCheck),
            stats.Categories,
            planCategories == 0 ? null : planCategories,
            _d.Targets.CategoriesPerOutlet,
            stats.Tempo,
            FlagsOf(id),
            PlanFacts(categoryPlans, lines),
            indicators,
            Compare(id.ToString(), AgentName(id), null, lines, prevLines),
            prevMarkets.Count,
            silent.Sum(s => s.PrevRevenue),
            silent,
            newMarkets,
            AgentAssortmentOf(id, regionId),
            akb,
            AgentAkbMonths(id),
            stats.Orders,
            SalesMath.Ratio(stats.Revenue, akb),
            SalesMath.Ratio(stats.Kg, akb),
            SalesMath.ExecutionLevelOf(execution),
            SalesMath.ExecutionLevelOf(revenueExecution));
    }

    /// <summary>
    /// «Проблемные агенты»: ТП с замечаниями, по убыванию тяжести (SalesMath.ProblemScore: 10 за критичное, 4 за риск, плюс (1 − страйк) × 3
    /// у оцениваемых), при равной тяжести — с меньшей выручкой выше; вакансии (без оценки) — после. Номер — место в этом списке.
    /// </summary>
    public ProblemsView Problems(string? directionId, FlagKind? criterion, bool includeVacancies)
    {
        var reps = _roster.Where(IsSalesRep).ToList();

        var list = reps
            .Where(a => directionId is null || DirectionIdOf(a) == directionId)
            .Where(a => includeVacancies ? true : !IsVacancy(a))
            .Where(a =>
            {
                var flags = FlagsOf(a);
                if (IsVacancy(a)) return criterion is null;
                return criterion is { } c
                    ? flags.Any(f => f.Kind == c)
                    : flags.Any(f => f.Severity != FlagSeverity.Info);
            })
            .Select(a =>
            {
                var stats = StatsOf(a);
                var flags = FlagsOf(a);
                var regionId = _agentRegion.GetValueOrDefault(a, NoRegionId);
                var region = _regions[regionId];
                var direction = region.DirectionId is { } dirId && _directions.TryGetValue(dirId, out var dir) ? dir : null;
                var score = SalesMath.ProblemScore(flags.Count(f => f.Severity == FlagSeverity.Critical), flags.Count(f => f.Severity == FlagSeverity.Risk),
                    stats.Conversion, stats.Visits.Done, _d.Thresholds.MinVisits);
                return new ProblemAgent(a, AgentName(a), regionId == NoRegionId ? null : regionId.ToString(), region.Name,
                    direction?.Name, null, stats.Conversion, stats.Visits.Done, stats.Revenue, IsVacancy(a), flags, score);
            })
            .OrderBy(p => p.IsVacancy)
            .ThenByDescending(p => p.Score)
            .ThenBy(p => p.Revenue)
            .Select((p, i) => p with { Rank = i + 1 })
            .ToList();

        var fact = _d.Current.Sum(l => l.Kg);
        var unassigned = _d.Current.Where(l => l.AgentId == null).Sum(l => l.Kg);
        return new ProblemsView(
            Period,
            reps.Count(IsVacancy),
            list,
            list.Count(p => !p.IsVacancy && p.Flags.Any(f => f.Severity != FlagSeverity.Info)),
            DirectionGroups().Where(g => g.Id != NoDirectionId && g.Regions.Count > 0).Select(g => new UnitRef(g.Id, g.Name)).ToList(),
            new ProblemsFact(fact, unassigned, SalesMath.Ratio(unassigned, fact), fact - unassigned));
    }

    // ================= Блоки =================

    private GroupView Group(
        string name, string? subtitle, IReadOnlyCollection<Guid>? scope, List<UnitRow> cards, DateOnly from, DateOnly to, string calendarMetric, long? calendarCategory)
    {
        var regionIds = RegionIds(scope).ToList();
        var set = scope?.ToHashSet();
        var categories = CategoryCardsOf(set);
        var notBought = regionIds.Select(RegionNotBought).OrderBy(r => r.Name).ToList();
        var categoryPlans = CategoryPlansOf(set);
        // Календарь визитов: регионы с планом, визитами или заказами; итог — по этим же строкам (с сервера, а не в браузере).
        var visitRows = regionIds.Select(r => RegionVisitRow(r, from, to)).Where(r => r.Plan + r.DoneAll + r.OrdersTotal + r.VisitsOutsideTeam > 0)
            .OrderBy(r => r.Name).ToList();
        var calendarRows = regionIds.Select(r => (r.ToString(), _regions[r].Name)).OrderBy(r => r.Item2).ToList();

        return new GroupView(
            Period,
            name,
            subtitle,
            Kpi(set),
            Unassigned(set),
            cards,
            regionIds.Select(RegionUnit).OrderBy(r => r.Name).ToList(),
            visitRows,
            regionIds.Select(r => Compare(r.ToString(), _regions[r].Name, DirectionName(r), _currentByRegion[r], _previousByRegion[r]))
                .OrderBy(r => r.Name).ToList(),
            notBought,
            categories,
            AkbMonthsOf(set, categories),
            QualityOf(set),
            set is null ? Excluded() : null,
            categoryPlans,
            NextMonthOf(set, byCategory: false),
            NotBoughtTotal("Итого", notBought),
            CategoryPlanTotal(categoryPlans),
            Sum("total", "Итого", null, visitRows),
            CalendarOf(Lines(set).ToList(), calendarRows, l => RegionOf(l).ToString(), calendarMetric, calendarCategory));
    }

    private KpiTiles Kpi(IReadOnlySet<Guid>? scope)
    {
        var lines = Lines(scope).ToList();
        var fact = lines.Sum(l => l.Kg);
        var revenue = lines.Sum(l => l.Revenue);
        var akb = SalesMath.Akb(lines);
        var coverage = PlanScope(scope);
        var plan = coverage.Plan;
        var forecast = ForecastOf(fact);
        var planForecast = ForecastOf(coverage.Fact);

        // Визиты и заказы — по ТП подразделения (команда с вакансиями, как в таблице «Команда ТП»): сумма по агентам их выполненных
        // визитов и заказов, принятых в месяце. Визиты остальных (операторы, супервайзеры, пользователи вне справочника) — отдельно.
        var agents = AgentsIn(scope).ToList();
        var team = agents.Where(InTeam).Select(a => StatsOf(a).Visits).ToList();
        var visits = new VisitSummary(team.Sum(v => v.Done), team.Sum(v => v.WithOrder), team.Sum(v => v.Orders));
        var outsideTeam = agents.Where(a => !InTeam(a)).Sum(a => StatsOf(a).Visits.Done);
        var active = ActiveReps(scope);
        var execution = plan is null ? null : SalesMath.Ratio(coverage.Fact, plan);

        return new KpiTiles(
            fact,
            plan,
            execution,
            forecast,
            planForecast is null ? null : SalesMath.Ratio(planForecast.Value, plan),
            revenue,
            akb,
            TargetValue.Of(visits.Conversion, _d.Targets.Conversion),
            TargetValue.Of(SalesMath.Ratio(revenue, akb), _d.Targets.RevenuePerOutlet),
            TargetValue.Of(SalesMath.Ratio(akb, active), _d.Targets.AkbPerAgent),
            visits.WithoutOrder,
            visits.Done,
            active,
            RevenuePlanOf(scope, lines),
            plan is null ? null : coverage.Fact,
            plan is null ? null : planForecast,
            coverage.Agents,
            outsideTeam,
            _d.PlanSource,
            SalesMath.ExecutionLevelOf(execution));
    }

    /// <summary>Прогноз на конец месяца (факт по текущему темпу) — только у идущего месяца; у закрытого прогноза нет.</summary>
    private decimal? ForecastOf(decimal fact) => _d.Closed ? null : SalesMath.Forecast(fact, _d.WorkedDays, _d.DaysInMonth);

    /// <summary>План по выручке подразделения: агенты с планом sales_sum и их же выручка (без агентов без плана).</summary>
    private RevenuePlanTile? RevenuePlanOf(IReadOnlySet<Guid>? scope, IReadOnlyList<SaleLine> scopeLines)
    {
        var plans = _d.RevenuePlans
            .Where(p => p.AgentId is { } a && (scope is null || scope.Contains(_agentRegion.GetValueOrDefault(a, NoRegionId))))
            .GroupBy(p => p.AgentId!.Value)
            .Select(g => (Agent: g.Key, Plan: g.Sum(p => p.PlanKg)))
            .Where(x => x.Plan > 0)
            .ToList();
        if (plans.Count == 0)
        {
            return null;
        }

        var agents = plans.Select(x => x.Agent).ToHashSet();
        var plan = plans.Sum(x => x.Plan);
        var fact = scopeLines.Where(l => l.AgentId is { } a && agents.Contains(a)).Sum(l => l.Revenue);
        var forecast = ForecastOf(fact);
        var execution = SalesMath.Ratio(fact, plan);
        return new RevenuePlanTile(plan, fact, execution, forecast,
            forecast is null ? null : SalesMath.Ratio(forecast.Value, plan), plans.Count, SalesMath.ExecutionLevelOf(execution));
    }

    /// <summary>Вкладка «Планы»: все планы Linko за месяц. Факт здесь — по расчёту Linko.</summary>
    public PlansView Plans()
    {
        static bool Weight(string t) => t == "product_sales_weight";
        static bool Money(string t) => t == "sales_sum";
        static bool Akb(string t) => t == "active_client_count";
        static decimal? SumOrNull(IEnumerable<decimal> values)
        {
            var list = values.ToList();
            return list.Count == 0 ? null : list.Sum();
        }

        var people = _d.Indicators.GroupBy(i => i.AgentId).Select(g =>
        {
            var agent = g.Key;
            var list = g.OrderBy(i => Weight(i.PlanType) ? 0 : Money(i.PlanType) ? 1 : 2).ThenByDescending(i => i.Plan).ToList();
            var team = _d.TeamPlanStaff.Contains(agent);
            var regionId = _agentRegion.GetValueOrDefault(agent, NoRegionId);
            var weightPlan = SumOrNull(list.Where(i => Weight(i.PlanType)).Select(i => i.Plan));
            var weightFact = list.Where(i => Weight(i.PlanType)).Sum(i => i.Fact);
            var weightExecution = SalesMath.Ratio(weightFact, weightPlan);

            return new PlanPersonRow(
                agent,
                AgentName(agent),
                _d.Agents.TryGetValue(agent, out var info) ? info.Job : null,
                team || regionId == NoRegionId ? null : regionId.ToString(),
                team ? null : _regions[regionId].Name,
                team,
                weightPlan,
                weightFact,
                weightExecution,
                SumOrNull(list.Where(i => Money(i.PlanType)).Select(i => i.Plan)),
                list.Where(i => Money(i.PlanType)).Sum(i => i.Fact),
                SumOrNull(list.Where(i => Akb(i.PlanType)).Select(i => i.Plan)),
                list.Where(i => Akb(i.PlanType)).Sum(i => i.Fact),
                list.Select(Indicator).ToList(),
                SalesMath.ExecutionLevelOf(weightExecution));
        }).ToList();

        var agents = people.Where(p => !p.IsTeamPlan).OrderByDescending(p => p.WeightPlan ?? 0).ToList();
        var teamPlans = people.Where(p => p.IsTeamPlan).OrderByDescending(p => p.WeightPlan ?? 0).ToList();

        var regions = agents
            .GroupBy(p => p.RegionId ?? NoRegionId.ToString())
            .Select(g =>
            {
                var weightPlan = SumOrNull(g.Where(p => p.WeightPlan != null).Select(p => p.WeightPlan!.Value));
                var weightFact = g.Sum(p => p.WeightFact);
                var weightExecution = SalesMath.Ratio(weightFact, weightPlan);
                return new PlanRegionRow(g.Key, g.First().RegionName ?? "Без региона", g.Count(), weightPlan, weightFact,
                    weightExecution, SumOrNull(g.Where(p => p.RevenuePlan != null).Select(p => p.RevenuePlan!.Value)),
                    g.Sum(p => p.RevenueFact), SalesMath.ExecutionLevelOf(weightExecution));
            })
            .OrderByDescending(r => r.WeightPlan ?? 0)
            .ToList();

        var totalWeightPlan = SumOrNull(agents.Where(p => p.WeightPlan != null).Select(p => p.WeightPlan!.Value));
        var totalWeightFact = agents.Sum(p => p.WeightFact);
        var totalWeightExecution = SalesMath.Ratio(totalWeightFact, totalWeightPlan);
        return new PlansView(
            Period,
            totalWeightPlan,
            totalWeightFact,
            totalWeightExecution,
            SumOrNull(agents.Where(p => p.RevenuePlan != null).Select(p => p.RevenuePlan!.Value)),
            agents.Sum(p => p.RevenueFact),
            agents.Count(p => p.WeightPlan != null || p.RevenuePlan != null),
            _d.Indicators.Count,
            regions,
            agents,
            teamPlans,
            SalesMath.ExecutionLevelOf(totalWeightExecution));
    }

    /// <summary>Показатель Linko с выполнением и его цветом.</summary>
    private static IndicatorPlan Indicator(StaffIndicator i)
    {
        var execution = SalesMath.Ratio(i.Fact, i.Plan);
        return new IndicatorPlan(i.IndicatorId, i.Name, i.PlanType, i.Plan, i.Fact, execution, SalesMath.ExecutionLevelOf(execution));
    }

    private UnitRow RegionUnit(Guid id) => Unit(id.ToString(), _regions[id].Name, DirectionName(id), [id]);

    private UnitRow Unit(string id, string name, string? subtitle, IReadOnlyCollection<Guid>? scope, string kind = UnitKinds.Region)
    {
        var set = scope?.ToHashSet();
        var kpi = Kpi(set);
        var agents = AgentsIn(set).ToList();
        var regionNames = RegionIds(scope).Where(r => r != NoRegionId).Select(r => _regions[r].Name).OrderBy(n => n).ToList();

        return new UnitRow(id, name, subtitle, kpi.PlanKg, kpi.FactKg, kpi.Execution, kpi.ForecastKg, kpi.ForecastExecution,
            kpi.Revenue, kpi.Akb, kpi.Conversion.Value, kpi.VisitsWithoutOrder, agents.Count(a => !IsVacancy(a) && IsSalesRep(a)),
            regionNames.Count, regionNames, FlagCountsOf(agents), kpi.PlanFactKg, kind, kpi.ExecutionLevel, SalesMath.ExecutionLevelOf(kpi.ForecastExecution));
    }

    private UnassignedFact Unassigned(IReadOnlySet<Guid>? scope)
    {
        var lines = Lines(scope).ToList();
        var kg = lines.Where(l => l.AgentId == null).Sum(l => l.Kg);
        return new UnassignedFact(kg, SalesMath.Ratio(kg, lines.Sum(l => l.Kg)));
    }

    /// <summary>
    /// Регион в календаре визитов: строки — ТП подразделения (те же, что в плитках и «Команде ТП»: должность ТП и вакансии), итог региона —
    /// по их суммам. Выполненные визиты остальных пользователей региона (операторы, супервайзеры) — отдельным числом, не в строках.
    /// </summary>
    private VisitCalendarRow RegionVisitRow(Guid id, DateOnly from, DateOnly to)
    {
        var agents = AgentsIn(new HashSet<Guid> { id }).ToList();
        var children = agents
            .Where(InTeam)
            .Select(a => AgentVisitRow(a, from, to))
            .Where(r => r.Plan + r.DoneAll + r.OrdersTotal > 0)
            .OrderByDescending(r => r.DoneAll)
            .ToList();
        var outsideTeam = agents
            .Where(a => !InTeam(a))
            .Sum(a => _visitsByAgent[a].Count(v => v.Status == VisitStatus.Done && v.Date >= from && v.Date <= to));

        var okb = _marketRegion.Count(m => m.Value == id);
        var route = agents
            .SelectMany(VisitsOf)
            .Where(v => v.InPlan && v.Date >= from && v.Date <= to)
            .Select(v => v.MarketId)
            .Distinct()
            .Count();

        return Sum(id.ToString(), _regions[id].Name, $"ОКБ {okb} · в маршруте {route}", children, outsideTeam);
    }

    /// <summary>
    /// ТП в календаре визитов (DOC-rules §9.1). План — его визиты по маршруту за дни, факт — выполненные по маршруту и вне его.
    /// Заказы — принятые в эти дни (те же, что дали факт: статусы вторички, отчётный день); заказ «с визита по маршруту», если в день
    /// его ввода (created_date) в этот магазин был выполненный визит по плану — любого ТП, «вне маршрута» — если только визит без плана,
    /// иначе «без визита». Не посещено = max(0, план − по маршруту), % плана = по маршруту ÷ план.
    /// </summary>
    private VisitCalendarRow AgentVisitRow(long agent, DateOnly from, DateOnly to)
    {
        var visits = _visitsByAgent[agent].Where(v => v.Date >= from && v.Date <= to).ToList();

        var orders = _currentByAgent[agent]
            .Where(l => l.OrderId != null && l.Date >= from && l.Date <= to)
            .GroupBy(l => l.OrderId)
            .Select(g => (Key: (g.First().MarketId ?? 0, g.First().CreatedDate ?? g.First().Date), Sum: g.Sum(l => l.Revenue)))
            .ToList();

        var inPlan = orders.Where(o => _doneInPlan.Contains(o.Key)).ToList();
        var offPlan = orders.Where(o => !_doneInPlan.Contains(o.Key) && _doneOffPlan.Contains(o.Key)).ToList();
        var plan = visits.Count(v => v.InPlan);
        var doneInCount = visits.Count(v => v.Status == VisitStatus.Done && v.InPlan);
        var doneOffCount = visits.Count(v => v.Status == VisitStatus.Done && !v.InPlan);
        var planShare = SalesMath.Ratio(doneInCount, plan);

        return new VisitCalendarRow(agent.ToString(), AgentName(agent), $"ID {agent}",
            plan, doneInCount, doneOffCount, doneInCount + doneOffCount,
            inPlan.Count, inPlan.Sum(o => o.Sum), offPlan.Count, offPlan.Sum(o => o.Sum), orders.Count, orders.Sum(o => o.Sum),
            Math.Max(0, plan - doneInCount), planShare, [], orders.Count - inPlan.Count - offPlan.Count, 0, SalesMath.ExecutionLevelOf(planShare));
    }

    /// <summary>
    /// Итог строк (регион по ТП, республика и РМ по регионам): суммы, а «не посещено» и «% плана» — по суммарному плану и суммарным визитам
    /// по маршруту (DOC-rules §9.1), а не сумма недоборов строк: перевыполнение одного ТП закрывает недобор другого.
    /// </summary>
    private static VisitCalendarRow Sum(string id, string name, string? subtitle, IReadOnlyList<VisitCalendarRow> rows, int outsideTeam = 0)
    {
        var plan = rows.Sum(r => r.Plan);
        var doneIn = rows.Sum(r => r.DoneInPlan);
        var planShare = SalesMath.Ratio(doneIn, plan);
        return new VisitCalendarRow(id, name, subtitle, plan, doneIn, rows.Sum(r => r.DoneOffPlan), rows.Sum(r => r.DoneAll),
            rows.Sum(r => r.OrdersInPlan), rows.Sum(r => r.OrdersInPlanSum), rows.Sum(r => r.OrdersOffPlan), rows.Sum(r => r.OrdersOffPlanSum),
            rows.Sum(r => r.OrdersTotal), rows.Sum(r => r.OrdersTotalSum), Math.Max(0, plan - doneIn), planShare, rows,
            rows.Sum(r => r.OrdersNoVisit), outsideTeam + rows.Sum(r => r.VisitsOutsideTeam), SalesMath.ExecutionLevelOf(planShare));
    }

    /// <summary>«К прошлому месяцу за те же дни»: прошлый месяц урезан до тех же чисел.</summary>
    private SameDaysRow Compare(string id, string name, string? subtitle, IEnumerable<SaleLine> now, IEnumerable<SaleLine> previousMonth)
    {
        var nowList = now.ToList();
        var before = previousMonth.Where(l => l.Date <= _previousCutoff).ToList();
        var kgBefore = before.Sum(l => l.Kg);
        var kgNow = nowList.Sum(l => l.Kg);
        var sumBefore = before.Sum(l => l.Revenue);
        var sumNow = nowList.Sum(l => l.Revenue);
        var akbBefore = SalesMath.Akb(before);
        var akbNow = SalesMath.Akb(nowList);

        return new SameDaysRow(id, name, subtitle, kgBefore, kgNow, SalesMath.Delta(kgBefore, kgNow),
            sumBefore, sumNow, SalesMath.Delta(sumBefore, sumNow), akbNow, SalesMath.Delta(akbBefore, akbNow));
    }

    /// <summary>
    /// «Ещё не купили» по региону. Регион, где в этом месяце нет ни одной покупки, — почти всегда дыра в данных, а не разом
    /// замолчавшая база: доли нет, строка помечена «нет данных».
    /// </summary>
    private NotBoughtRow RegionNotBought(Guid id)
    {
        var prev = _previousByRegion[id].ToList();
        var prevMarkets = SalesMath.ActiveMarkets(prev);
        var nowMarkets = SalesMath.ActiveMarkets(_currentByRegion[id]);
        var silent = prevMarkets.Except(nowMarkets).ToHashSet();
        var noData = nowMarkets.Count == 0;

        return new NotBoughtRow(id.ToString(), _regions[id].Name, DirectionName(id), prevMarkets.Count, silent.Count,
            noData ? null : SalesMath.Ratio(silent.Count, prevMarkets.Count),
            prev.Where(l => l.MarketId is { } m && silent.Contains(m)).Sum(l => l.Revenue),
            nowMarkets.Except(prevMarkets).Count(), [], noData);
    }

    /// <summary>
    /// «Ещё не купили» по ТП региона: ТТ прошлого месяца закрепляется за агентом, который продал ей больше всех.
    /// В регионе без покупок за месяц доли нет и у ТП («нет данных»).
    /// </summary>
    private List<NotBoughtRow> AgentNotBought(Guid id)
    {
        var prev = _previousByRegion[id].Where(l => l.AgentId != null && l.MarketId != null).ToList();
        var now = _currentByRegion[id].Where(l => l.AgentId != null && l.MarketId != null).ToList();
        var prevMarkets = SalesMath.ActiveMarkets(prev);
        var nowMarkets = SalesMath.ActiveMarkets(_currentByRegion[id]);
        var noData = nowMarkets.Count == 0;

        var prevOwner = OwnerOf(prev);
        var nowOwner = OwnerOf(now);

        return prevOwner.Values.Concat(nowOwner.Values).Distinct()
            .Select(agent =>
            {
                var baseMarkets = prevOwner.Where(kv => kv.Value == agent && prevMarkets.Contains(kv.Key)).Select(kv => kv.Key).ToList();
                var silent = baseMarkets.Where(m => !nowMarkets.Contains(m)).ToList();
                var agentPrev = prev.Where(l => l.AgentId == agent).ToList();
                var created = nowOwner.Count(kv => kv.Value == agent && nowMarkets.Contains(kv.Key) && !prevMarkets.Contains(kv.Key));

                return new NotBoughtRow(agent.ToString(), AgentName(agent), $"ID {agent}", baseMarkets.Count, silent.Count,
                    noData ? null : SalesMath.Ratio(silent.Count, baseMarkets.Count),
                    prev.Where(l => l.MarketId is { } m && silent.Contains(m)).Sum(l => l.Revenue),
                    created,
                    silent.Select(m => SilentOf(m, agentPrev)).OrderByDescending(s => s.PrevRevenue).ToList(),
                    noData);
            })
            .Where(r => r.Base + r.New > 0)
            .OrderByDescending(r => r.Silent)
            .ToList();
    }

    /// <summary>
    /// Итог «Ещё не купили» — суммы строк таблицы (база, молчат, их выручка, новые). Если у какой-то строки нет данных,
    /// доли у итога нет: молчание такого региона не отличить от дыры в данных.
    /// </summary>
    private static NotBoughtRow NotBoughtTotal(string name, IReadOnlyList<NotBoughtRow> rows)
    {
        var baseCount = rows.Sum(r => r.Base);
        var silent = rows.Sum(r => r.Silent);
        var noData = rows.Any(r => r.NoData);
        return new NotBoughtRow("total", name, null, baseCount, silent, noData ? null : SalesMath.Ratio(silent, baseCount),
            rows.Sum(r => r.SilentPrevRevenue), rows.Sum(r => r.New), [], noData);
    }

    private static Dictionary<long, long> OwnerOf(IEnumerable<SaleLine> lines) =>
        lines.GroupBy(l => (Market: l.MarketId!.Value, Agent: l.AgentId!.Value))
            .Select(g => (g.Key.Market, g.Key.Agent, Revenue: g.Sum(l => l.Revenue)))
            .GroupBy(x => x.Market)
            .ToDictionary(g => g.Key, g => g.MaxBy(x => x.Revenue).Agent);

    private SilentMarket SilentOf(long market, IEnumerable<SaleLine> prevLines)
    {
        var lines = prevLines.Where(l => l.MarketId == market).ToList();
        return new SilentMarket(market, MarketName(market), lines.Sum(l => l.Kg), lines.Sum(l => l.Revenue),
            lines.Where(l => l.ProductId != null).Select(l => l.ProductId).Distinct().Count());
    }

    /// <summary>
    /// «План и факт по месяцам»: план месяца — план региона выбранного вида (РОП или «Завод»), если на этот месяц есть планы регионов,
    /// иначе — сумма планов ТП региона из Linko; так же, как плитка выполнения того месяца.
    /// </summary>
    private List<MonthPlanFact> MonthsOf(RegionInfo region) =>
        Enumerable.Range(1, 12).Select(m =>
        {
            var regionPlans = _d.YearRegionPlans.Where(p => p.Month == m).ToList();
            var plan = regionPlans.Count > 0
                ? SalesMath.PlanTotal(regionPlans.Where(p => p.RegionId == region.Id))
                : RegionPlan(region.Id, _d.YearAgentPlans.Where(p => p.Month == m).ToList());
            decimal? fact = m > _d.Month
                ? null
                : _d.History.Where(h => h.Year == _d.Year && h.Month == m && BranchRegion(h.BranchId) == region.Id)
                    .Sum(h => h.Kg);
            var source = plan is null ? null : regionPlans.Count > 0 ? _d.SelectedPlan : PlanSources.Linko;
            return new MonthPlanFact(m, plan, fact, source);
        }).ToList();

    private List<CategoryShare> CategoriesOf(Guid region)
    {
        var groups = _currentByRegion[region]
            .GroupBy(l => GroupOf(l.CategoryId))
            .Select(g => (Category: g.Key, Revenue: g.Sum(l => l.Revenue)))
            .Where(x => x.Revenue > 0)
            .ToList();
        var total = groups.Sum(x => x.Revenue);

        return groups
            .Select(x => new CategoryShare(x.Category, _cats.NameOf(x.Category), x.Revenue, SalesMath.Ratio(x.Revenue, total)))
            .OrderByDescending(x => x.Revenue)
            .ToList();
    }

    /// <summary>Есть ли настройка категорий отчёта. Без неё (тесты) каждый тип Linko — своя категория.</summary>
    private bool HasReportCategories => _cats.Mapping.Values.Any(g => SalesCategories.IsConfigured(g));

    private bool InReport(long? group) => !HasReportCategories || SalesCategories.IsConfigured(group);

    /// <summary>Качество данных подразделения: чего не хватает и что учтено иначе, чем выглядит в Linko.</summary>
    private DataQualityView QualityOf(IReadOnlySet<Guid>? scope)
    {
        var q = _d.Quality;
        var uncategorized = Lines(scope)
            .Where(l => !InReport(GroupOf(l.CategoryId)))
            .GroupBy(l => GroupOf(l.CategoryId))
            .Select(g => new UncategorizedType(g.Key?.ToString() ?? "none", _cats.NameOf(g.Key), g.Sum(l => l.Kg), g.Sum(l => l.Revenue),
                SalesMath.OrderCount(g)))
            .OrderByDescending(x => x.Revenue)
            .ToList();
        return new DataQualityView(q.DeliveredWithoutAcceptance, q.ZeroHeaderReturns, q.ZeroHeaderReturnsKg, q.ReturnsWithoutLines,
            q.ReturnsWithoutLinesHeaderKg, uncategorized, q.AcceptedInFuture, scope is null ? _d.OtherCurrency : null);
    }

    /// <summary>Исключённые филиалы («Завод»): экспорт и опт — отдельно от вторички, чтобы не завышать республику.</summary>
    public ExcludedSummary? Excluded()
    {
        if (_d.ExcludedCurrent.Count == 0 && _d.ExcludedPrevious.Count == 0)
        {
            return null;
        }

        var now = _d.ExcludedCurrent;
        var fact = now.Sum(l => l.Kg);
        var forecast = ForecastOf(fact);
        var prevKg = _d.ExcludedPrevious.Sum(l => l.Kg);
        // «К прошлому месяцу» — прогноз к факту прошлого месяца, как у карточек категорий: у закрытого месяца прогноза нет — и сравнения нет.
        return new ExcludedSummary(fact, now.Sum(l => l.Revenue), SalesMath.OrderCount(now), SalesMath.Akb(now), forecast, prevKg,
            _d.Closed ? null : SalesMath.Delta(prevKg, forecast ?? fact));
    }

    /// <summary>
    /// Карточки категорий отчёта (клик — артикулы): категории из настройки (фасовки помадки — одна категория).
    /// Доля по весу и дистрибуция — от итога подразделения; «к прошлому месяцу» — прогноз месяца к факту всего прошлого месяца
    /// (прогноз и «к прошлому месяцу» — только у идущего месяца). Типы вне настройки (импорт, бонус) сюда не входят — они в диагностике (QualityOf).
    /// </summary>
    private List<CategoryCard> CategoryCardsOf(IReadOnlySet<Guid>? scope) =>
        CategoryCardsFor(Lines(scope).ToList(), scope is null ? _d.Previous : scope.SelectMany(r => _previousByRegion[r]).ToList(), republic: scope is null);

    /// <summary>
    /// Карточки категорий по произвольному набору строк (подразделение, агент, экспорт).
    /// republic — набор и есть республика: статуса «не возят» (здесь ноль, а по республике идёт) у неё быть не может.
    /// Артикул «продаётся», если в наборе есть ТТ с положительной строкой товара; ТТ артикула и «Только он» — по положительным
    /// строкам (SalesMath.SkuTt, SoloByProduct); АКБ категории — ТТ с чистым весом категории больше нуля (SalesMath.Akb), как в DOC-rules §3.
    /// </summary>
    private List<CategoryCard> CategoryCardsFor(IReadOnlyList<SaleLine> lines, IReadOnlyList<SaleLine> previousLines, bool republic)
    {
        var totalKg = lines.Sum(l => l.Kg);
        var akb = SalesMath.Akb(lines);
        var solo = SoloOf(lines);

        var current = lines.ToLookup(l => GroupOf(l.CategoryId));
        var previous = previousLines.ToLookup(l => GroupOf(l.CategoryId));
        var assortment = _d.ActiveSkus
            .Where(_d.Products.ContainsKey)
            .ToLookup(p => GroupOf(_d.Products[p].CategoryId));

        return current.Select(g => g.Key).Concat(previous.Select(g => g.Key)).Distinct()
            .Where(InReport)
            .Select(category =>
            {
                var now = current[category].ToList();
                var before = previous[category].ToList();
                var soldNow = Sold(now);
                var soldBefore = Sold(before);
                var universe = assortment[category].Concat(soldNow).Concat(soldBefore).ToHashSet();
                var fact = now.Sum(l => l.Kg);
                var revenue = now.Sum(l => l.Revenue);

                var nowByProduct = now.Where(l => l.ProductId != null).ToLookup(l => l.ProductId!.Value);
                var beforeByProduct = before.Where(l => l.ProductId != null).ToLookup(l => l.ProductId!.Value);
                var skus = universe
                    .Select(p =>
                    {
                        var sl = nowByProduct[p].ToList();
                        var skuAkb = SalesMath.SkuTt(sl);
                        var skuKg = sl.Sum(l => l.Kg);
                        var skuSolo = solo.GetValueOrDefault(p);
                        var status = SkuStatusOf(p, soldNow.Contains(p), soldBefore.Contains(p), republic);
                        var product = _d.Products.GetValueOrDefault(p);
                        return new SkuRow(p, product?.Name ?? $"Товар {p}", product?.Code, skuKg, sl.Sum(l => l.Revenue),
                            skuAkb, SalesMath.Ratio(skuAkb, akb), beforeByProduct[p].Sum(l => l.Kg), status,
                            skuKg == 0 ? null : SalesMath.Ratio(skuKg, fact), skuSolo, SalesMath.Ratio(skuSolo, skuAkb), _d.Top.Contains(product?.Code));
                    })
                    .OrderBy(s => SkuStatuses.Rank(s.Status))
                    .ThenByDescending(s => s.FactKg)
                    .ThenByDescending(s => s.PrevMonthKg)
                    .ThenBy(s => s.Name)
                    .ToList();

                var categoryAkb = SalesMath.Akb(now);
                var forecast = ForecastOf(fact);
                var prevKg = before.Sum(l => l.Kg);

                return new CategoryCard(
                    category?.ToString() ?? "none",
                    _cats.NameOf(category),
                    soldNow.Count,
                    universe.Count,
                    fact,
                    SalesMath.Ratio(fact, totalKg),
                    revenue,
                    categoryAkb,
                    SalesMath.Ratio(categoryAkb, akb),
                    forecast,
                    ForecastOf(revenue),
                    prevKg,
                    _d.Closed ? null : SalesMath.Delta(prevKg, forecast ?? fact),
                    universe.Count - soldNow.Count,
                    soldBefore.Count(p => !soldNow.Contains(p)),
                    skus);
            })
            .Where(c => c.FactKg != 0 || c.PrevMonthKg != 0 || c.Revenue != 0) // категория без продаж в обоих месяцах — не показываем
            .OrderByDescending(c => c.FactKg)
            .ThenByDescending(c => c.Revenue)
            .ToList();
    }

    /// <summary>Тип товара Linko → категория отчёта (см. SalesCategories).</summary>
    private long? GroupOf(long? category) => _cats.GroupOf(category);

    /// <summary>
    /// АКБ по месяцам года (январь — выбранный месяц): итог и по категориям карточек.
    /// Текущий и прошлый месяц — из строк продаж (как плитка АКБ), более ранние — из агрегатов БД.
    /// Для направления АКБ более ранних месяцев — сумма по его филиалам.
    /// </summary>
    private AkbByMonth AkbMonthsOf(IReadOnlySet<Guid>? scope, IReadOnlyList<CategoryCard> cards) =>
        AkbMonthsCore(
            _d.AkbHistory.Where(r => r.Year == _d.Year && r.AgentId == null && (scope is null ? !r.ByBranch : r.ByBranch && scope.Contains(BranchRegion(r.BranchId)))).ToList(),
            Lines(scope).ToList(),
            scope is null ? _d.Previous : scope.SelectMany(r => _previousByRegion[r]).ToList(),
            cards.Select(c => (c.Id, c.Name)).ToList());

    /// <summary>
    /// АКБ агента по месяцам: итог и категории отчёта, в которых у агента в году была хоть одна точка
    /// (от самой широкой). Точка агента — ТТ с чистым весом у этого агента за месяц больше нуля.
    /// </summary>
    private AkbByMonth AgentAkbMonths(long agent)
    {
        var history = _d.AkbHistory.Where(r => r.Year == _d.Year && r.AgentId == agent).ToList();
        var current = _currentByAgent[agent].ToList();
        var previous = _previousByAgent[agent].ToList();
        var series = history.Where(r => !r.IsTotal).Select(r => r.CategoryId)
            .Concat(current.Concat(previous).Select(l => GroupOf(l.CategoryId)))
            .Where(InReport)
            .Distinct()
            .Select(g => (Id: g?.ToString() ?? "none", Name: _cats.NameOf(g)))
            .ToList();
        return AkbMonthsCore(history, current, previous, series);
    }

    /// <summary>Категория скрыта в «АКБ по месяцам» (Sales:AkbChartHiddenCategories, по названию без учёта регистра).</summary>
    private bool HiddenInAkbChart(string name) =>
        _d.AkbChartHiddenCategories.Any(h => string.Equals(h.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// «АКБ по месяцам» (DOC-filters §1): АКБ, кг и сум по одним строкам и правилам — месяцы с января по выбранный, итог и категории.
    /// Скрытые категории не отдаются; линий — не больше AkbChartMaxCategories, по объёму (кг за месяцы); среднее за месяц — по месяцам с данными.
    /// </summary>
    private AkbByMonth AkbMonthsCore(
        IReadOnlyList<MonthlyAkb> history,
        IReadOnlyList<SaleLine> currentLines,
        IReadOnlyList<SaleLine> previousLines,
        IReadOnlyList<(string Id, string Name)> cards)
    {
        var months = Enumerable.Range(1, _d.Month).ToList();

        IReadOnlyList<SaleLine>? LinesOf(int month) =>
            month == _d.Month ? currentLines
            : _previousStart.Year == _d.Year && month == _previousStart.Month ? previousLines
            : null;

        var monthLines = months.ToDictionary(m => m, LinesOf);
        var monthHistory = history.ToLookup(r => r.Month);

        // Значение месяца: из строк продаж (текущий и прошлый месяц) или из агрегата БД; null — данных за месяц нет.
        (int? Akb, decimal? Kg, decimal? Sum) Value(int month, Func<SaleLine, bool>? inCategory, Func<MonthlyAkb, bool> row)
        {
            if (monthLines[month] is { } lines)
            {
                if (lines.Count == 0)
                {
                    return (null, null, null);
                }

                var set = (inCategory is null ? lines : lines.Where(inCategory)).ToList();
                return (SalesMath.Akb(set), set.Sum(l => l.Kg), set.Sum(l => l.Revenue));
            }

            var rows = monthHistory[month].Where(row).ToList();
            return monthHistory[month].Any() ? (rows.Sum(r => r.Akb), rows.Sum(r => r.Kg), rows.Sum(r => r.Revenue)) : (null, null, null);
        }

        var total = months.Select(m => Value(m, null, r => r.IsTotal)).ToList();
        var series = cards
            .Where(card => !HiddenInAkbChart(card.Name))
            .Select(card =>
            {
                long? id = long.TryParse(card.Id, out var parsed) ? parsed : null;
                return (card.Id, card.Name, Values: months.Select(m => Value(m, l => GroupOf(l.CategoryId) == id, r => !r.IsTotal && r.CategoryId == id)).ToList());
            })
            .OrderByDescending(s => s.Values.Sum(v => v.Kg ?? 0))
            .ThenByDescending(s => s.Values.Sum(v => v.Akb ?? 0))
            .Take(AkbChartMaxCategories)
            .ToList();

        static MonthMetric Metric(List<(int? Akb, decimal? Kg, decimal? Sum)> totals, IEnumerable<(string Id, string Name, List<(int? Akb, decimal? Kg, decimal? Sum)> Values)> rows, Func<(int? Akb, decimal? Kg, decimal? Sum), decimal?> pick)
        {
            var totalValues = totals.Select(pick).ToList();
            return new MonthMetric(totalValues, rows.Select(s =>
            {
                var values = s.Values.Select(pick).ToList();
                return new MonthSeries(s.Id, s.Name, values, SalesMath.Average(values));
            }).ToList(), SalesMath.Average(totalValues));
        }

        var akbTotal = total.Select(v => v.Akb).ToList();
        var lastPartial = _d.DataThrough < _d.MonthStart.AddMonths(1).AddDays(-1);
        return new AkbByMonth(
            _d.Year,
            months,
            lastPartial,
            akbTotal,
            series.Select(s =>
            {
                var values = s.Values.Select(v => v.Akb).ToList();
                return new AkbSeries(s.Id, s.Name, values, SalesMath.Average(values.Select(v => (decimal?)v)));
            }).ToList(),
            SalesMath.Average(akbTotal.Select(v => (decimal?)v)),
            Metric(total, series, v => v.Kg),
            Metric(total, series, v => v.Sum));
    }

    private Guid BranchRegion(long? branch) => branch is { } b && _regionByBranch.TryGetValue(b, out var id) ? id : NoRegionId;

    /// <summary>
    /// Календарь месяца (DOC-rules §9.1): строка — ТП региона или регион подразделения (rowOf — ключ строки у строки продаж), столбец — день;
    /// kg | sum | akb | akb по категории. null — нет данных за день. Кг и деньги: сумма дней = итог месяца; АКБ итога — уникальные ТТ за месяц.
    /// </summary>
    private MonthCalendar CalendarOf(IReadOnlyList<SaleLine> scopeLines, IReadOnlyList<(string Id, string Name)> rowKeys, Func<SaleLine, string?> rowOf,
        string metric, long? category)
    {
        metric = metric is "sum" or "akb" ? metric : "kg";
        var days = _d.DaysInMonth;
        var lastDay = _d.WorkedDays;
        var lines = scopeLines
            .Where(l => metric != "akb" || category is null || GroupOf(l.CategoryId) == category)
            .ToList();

        decimal? Cell(IReadOnlyCollection<SaleLine> set) => set.Count == 0 ? null : metric switch
        {
            "sum" => set.Sum(l => l.Revenue),
            "akb" => SalesMath.Akb(set),
            _ => set.Sum(l => l.Kg),
        };

        var byRowDay = lines.Select(l => (Row: rowOf(l), Line: l)).Where(x => x.Row != null).ToLookup(x => (x.Row!, x.Line.Date.Day), x => x.Line);
        var byRow = lines.Select(l => (Row: rowOf(l), Line: l)).Where(x => x.Row != null).ToLookup(x => x.Row!, x => x.Line);
        var rows = rowKeys.Select(row =>
        {
            var cells = Enumerable.Range(1, days)
                .Select(day => day > lastDay ? null : Cell(byRowDay[(row.Id, day)].ToList()))
                .ToList();
            return new MonthCalendarRow(row.Id, row.Name, cells, Cell(byRow[row.Id].ToList()) ?? 0);
        }).ToList();

        var byDay = lines.ToLookup(l => l.Date.Day);
        var totals = Enumerable.Range(1, days).Select(day => day > lastDay ? null : Cell(byDay[day].ToList())).ToList();
        var sundays = Enumerable.Range(1, days).Select(day => new DateOnly(_d.Year, _d.Month, day).DayOfWeek == DayOfWeek.Sunday).ToList();

        return new MonthCalendar(metric, metric == "akb" ? category : null, sundays, rows, totals, Cell(lines) ?? 0);
    }

    /// <summary>Команда региона — ТП из справочника (должность ТП в Linko) и вакансии; остальные продающие — в NotInDirectory.</summary>
    private List<long> TeamOf(Guid region) =>
        _currentByRegion[region].Where(l => l.AgentId != null).Select(l => l.AgentId!.Value)
            .Concat(AgentsIn(new HashSet<Guid> { region }))
            .Distinct()
            .Where(InTeam)
            .ToList();

    private Func<long, TeamRow> TeamRowOf(Guid region) => agent =>
    {
        var lines = _currentByRegion[region].Where(l => l.AgentId == agent).ToList();
        var stats = StatsOf(agent);
        var fact = lines.Sum(l => l.Kg);
        var plan = SalesMath.PlanTotal(_d.Plans.Where(p => p.AgentId == agent));
        var forecast = ForecastOf(fact);
        var execution = SalesMath.Ratio(fact, plan);
        var forecastExecution = forecast is null ? null : SalesMath.Ratio(forecast.Value, plan);

        return new TeamRow(agent, AgentName(agent), plan, fact, execution, forecast, forecastExecution, lines.Sum(l => l.Revenue),
            stats.Visits.Done, stats.Orders, stats.Conversion, stats.SumPerVisit, stats.Categories,
            IsVacancy(agent), _d.Agents.TryGetValue(agent, out var info) && info.InDirectory, FlagsOf(agent), IsSalesRep(agent), info?.Job,
            SalesMath.ExecutionLevelOf(execution), SalesMath.ExecutionLevelOf(forecastExecution));
    };

    private List<NotInDirectoryRow> NotInDirectory(Guid region) =>
        _currentByRegion[region].Where(l => l.AgentId != null && !SalesOptions.IsChannelBranch(l.BranchId)) // каналы оформляет офис, это не ТП
            .GroupBy(l => l.AgentId!.Value)
            .Where(g => !IsSalesRep(g.Key) && !IsVacancy(g.Key))
            .Select(g => new NotInDirectoryRow(g.Key, AgentName(g.Key), g.Sum(l => l.Kg), g.Sum(l => l.Revenue)))
            .OrderByDescending(r => r.Kg)
            .ToList();

    private IEnumerable<CategoryPlanRow> CurrentCategoryPlans() =>
        _d.CategoryPlans.Where(p => p.Year == _d.Year && p.Month == _d.Month);

    /// <summary>
    /// План по категориям подразделения. План РОП / «Завод» — сумма планов «регион × категория» его регионов, факт — все продажи
    /// категории в подразделении. Без планов регионов на месяц — сумма планов его ТП по показателям Linko.
    /// </summary>
    private List<CategoryPlanFact> CategoryPlansOf(IReadOnlySet<Guid>? scope)
    {
        if (RegionPlansUsed)
        {
            return RegionCategoryPlans(scope);
        }

        var agents = AgentsIn(scope).ToHashSet();
        var rows = CurrentCategoryPlans().Where(p => scope is null || agents.Contains(p.AgentId)).ToList();
        return PlanFacts(rows, Lines(scope).ToList());
    }

    /// <summary>
    /// План категории = сумма планов РОП (или «Завод») «регион × категория» по регионам охвата: тип товара Linko в плане сводится
    /// к категории отчёта (фасовки помадки — одна строка). Факт — все строки категории в охвате, а не только ТП: у ТП плана по категориям
    /// из планов регионов нет. Категории с продажами, но без плана — строки «плана нет».
    /// </summary>
    private List<CategoryPlanFact> RegionCategoryPlans(IReadOnlySet<Guid>? scope)
    {
        var plans = _d.Plans
            .Where(p => p.RegionId is { } r && p.CategoryId != null && (scope is null || scope.Contains(r)))
            .GroupBy(p => GroupOf(p.CategoryId)!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.PlanKg));
        var lines = Lines(scope).Where(l => GroupOf(l.CategoryId) != null).ToLookup(l => GroupOf(l.CategoryId)!.Value);

        return plans.Keys
            .Concat(lines.Select(g => g.Key).Where(c => InReport(c)))
            .Distinct()
            .Select(category =>
            {
                var categoryLines = lines[category].ToList();
                var fact = categoryLines.Sum(l => l.Kg);
                decimal? plan = plans.TryGetValue(category, out var p) ? p : null;
                return PlanFact(category, _cats.NameOf(category), plan, fact, categoryLines.Sum(l => l.Revenue), fact);
            })
            .Where(x => x.PlanKg != null || x.FactKg > 0)
            .OrderBy(x => x.PlanKg is null)
            .ThenByDescending(x => x.PlanKg ?? x.FactKg)
            .ToList();
    }

    /// <summary>Строка плана по категории: выполнение, «осталось», прогноз и прогноз к плану считаются здесь, а не в интерфейсе.</summary>
    private CategoryPlanFact PlanFact(long? categoryId, string name, decimal? plan, decimal fact, decimal revenue, decimal? scopeFact)
    {
        var forecast = ForecastOf(fact);
        var forecastExecution = plan is null || forecast is null ? null : SalesMath.Ratio(forecast.Value, plan);
        var execution = plan is null ? null : SalesMath.Ratio(fact, plan);
        return new CategoryPlanFact(categoryId, name, plan, fact, revenue, execution, scopeFact,
            plan is { } total ? SalesMath.Remaining(total, fact) : null, forecast, forecastExecution, SalesMath.ForecastLevelOf(forecastExecution),
            SalesMath.ExecutionLevelOf(execution));
    }

    /// <summary>Итог «План и факт по категориям» — по строкам с планом: сумма плана и факта, выполнение, «осталось», прогноз к плану.</summary>
    private CategoryPlanFact? CategoryPlanTotal(IReadOnlyList<CategoryPlanFact> rows)
    {
        var planned = rows.Where(r => r.PlanKg != null).ToList();
        return planned.Count == 0
            ? null
            : PlanFact(null, "Итого с планом", planned.Sum(r => r.PlanKg!.Value), planned.Sum(r => r.FactKg), planned.Sum(r => r.Revenue), null);
    }

    /// <summary>
    /// План и факт по группам категорий показателей Linko. Факт — ТП, у которых есть этот план (как у плитки выполнения),
    /// рядом — весь факт по этим категориям. Категории с продажами, но без плана — отдельными строками «плана нет».
    /// </summary>
    private List<CategoryPlanFact> PlanFacts(IReadOnlyList<CategoryPlanRow> rows, IReadOnlyList<SaleLine> lines)
    {
        var result = new List<CategoryPlanFact>();
        foreach (var g in rows.GroupBy(r => string.Join(",", r.Groups)))
        {
            var groups = g.First().Groups;
            var agents = g.Select(r => r.AgentId).ToHashSet();
            var inGroups = groups.Count == 0 ? lines : lines.Where(l => GroupOf(l.CategoryId) is { } c && groups.Contains(c)).ToList();
            var own = inGroups.Where(l => l.AgentId is { } a && agents.Contains(a)).ToList();
            var plan = g.Sum(r => r.PlanKg);
            var fact = own.Sum(l => l.Kg);
            var name = groups.Count == 0 ? "Без разбивки по категориям" : string.Join(" + ", groups.Select(x => _cats.NameOf(x)));
            result.Add(PlanFact(groups.Count == 1 ? groups[0] : null, name, plan, fact, own.Sum(l => l.Revenue), inGroups.Sum(l => l.Kg)));
        }

        var planned = rows.SelectMany(r => r.Groups).ToHashSet();
        foreach (var g in lines.GroupBy(l => GroupOf(l.CategoryId)).Where(g => g.Key is { } c && SalesCategories.IsConfigured(c) && !planned.Contains(c)))
        {
            var kg = g.Sum(l => l.Kg);
            if (kg > 0)
            {
                result.Add(PlanFact(g.Key, _cats.NameOf(g.Key), null, kg, g.Sum(l => l.Revenue), kg));
            }
        }

        return result
            .OrderBy(x => x.PlanKg is null)
            .ThenByDescending(x => x.PlanKg ?? x.FactKg)
            .ToList();
    }

    /// <summary>
    /// План на следующий месяц: планы регионов выбранного вида (РОП или «Завод»), если на него они уже есть, иначе — планы ТП из Linko.
    /// На верхних уровнях — по регионам, в регионе — по категориям. null — плана на следующий месяц ещё нет.
    /// </summary>
    private NextMonthPlan? NextMonthOf(IReadOnlySet<Guid>? scope, bool byCategory) =>
        _d.NextRegionPlans.Count > 0 ? NextMonthOfRegions(scope, byCategory) : NextMonthOfAgents(scope, byCategory);

    /// <summary>
    /// План на следующий месяц по планам регионов. Сравнение — с планом регионов текущего месяца; если на текущий месяц планов регионов нет
    /// (план — из Linko), сравнивать не с чем.
    /// </summary>
    private NextMonthPlan? NextMonthOfRegions(IReadOnlySet<Guid>? scope, bool byCategory)
    {
        bool InScope(PlanRow p) => p.RegionId is { } r && (scope is null || scope.Contains(r));
        var next = _d.NextRegionPlans.Where(InScope).ToList();
        if (next.Count == 0)
        {
            return null;
        }

        var current = RegionPlansUsed ? _d.Plans.Where(InScope).ToList() : [];
        static decimal? Total(IEnumerable<PlanRow> plans)
        {
            var byRegion = plans.GroupBy(p => p.RegionId).Select(g => SalesMath.PlanTotal(g)!.Value).ToList();
            return byRegion.Count == 0 ? null : byRegion.Sum();
        }

        var rows = byCategory
            ? next.Where(p => p.CategoryId != null)
                .GroupBy(p => GroupOf(p.CategoryId)!.Value)
                .Select(g =>
                {
                    var was = current.Where(p => p.CategoryId != null && GroupOf(p.CategoryId) == g.Key).ToList();
                    return NextRow(g.Key.ToString(), _cats.NameOf(g.Key), g.Sum(p => p.PlanKg), was.Count == 0 ? null : was.Sum(p => p.PlanKg));
                })
                .OrderByDescending(r => r.PlanKg)
                .ToList()
            : next.GroupBy(p => p.RegionId!.Value)
                .Select(g => NextRow(g.Key.ToString(), _regions.TryGetValue(g.Key, out var r) ? r.Name : "Без региона", SalesMath.PlanTotal(g)!.Value,
                    SalesMath.PlanTotal(current.Where(p => p.RegionId == g.Key))))
                .OrderByDescending(r => r.PlanKg)
                .ToList();

        var plan = Total(next)!.Value;
        var currentPlan = Total(current);
        return new NextMonthPlan(_d.NextMonth.Year, _d.NextMonth.Month, plan, currentPlan, 0, rows, Change(currentPlan, plan), _d.SelectedPlan);
    }

    /// <summary>Строка плана на следующий месяц и изменение к текущему месяцу (null — в текущем месяце плана не было).</summary>
    private static NextMonthPlanRow NextRow(string id, string name, decimal plan, decimal? was) => new(id, name, plan, was, Change(was, plan));

    private static decimal? Change(decimal? was, decimal plan) => was is { } w ? SalesMath.Delta(w, plan) : null;

    /// <summary>
    /// План на следующий месяц из Linko: на верхних уровнях — по регионам, в регионе — по категориям.
    /// null — в Linko планов ТП на следующий месяц ещё нет.
    /// </summary>
    private NextMonthPlan? NextMonthOfAgents(IReadOnlySet<Guid>? scope, bool byCategory)
    {
        var agents = AgentsIn(scope).ToHashSet();
        bool InScope(long agent) => scope is null ? true : agents.Contains(agent);
        var next = _d.NextPlans.Where(p => p.AgentId is { } a && InScope(a)).ToList();
        if (next.Count == 0)
        {
            return null;
        }

        var current = _d.Plans.Where(p => p.AgentId is { } a && InScope(a)).ToList();
        List<NextMonthPlanRow> rows;
        if (byCategory)
        {
            var (year, month) = _d.NextMonth;
            var nextRows = _d.CategoryPlans.Where(p => p.Year == year && p.Month == month && InScope(p.AgentId)).ToList();
            var currentRows = CurrentCategoryPlans().Where(p => InScope(p.AgentId)).ToList();
            rows = nextRows.GroupBy(r => string.Join(",", r.Groups))
                .Select(g =>
                {
                    var groups = g.First().Groups;
                    var name = groups.Count == 0 ? "Без разбивки по категориям" : string.Join(" + ", groups.Select(x => _cats.NameOf(x)));
                    var was = currentRows.Where(r => string.Join(",", r.Groups) == g.Key).ToList();
                    return NextRow(g.Key, name, g.Sum(r => r.PlanKg), was.Count == 0 ? null : was.Sum(r => r.PlanKg));
                })
                .OrderByDescending(r => r.PlanKg)
                .ToList();
        }
        else
        {
            rows = next.GroupBy(p => _agentRegion.GetValueOrDefault(p.AgentId!.Value, NoRegionId))
                .Select(g => NextRow(g.Key.ToString(), _regions.TryGetValue(g.Key, out var r) ? r.Name : "Без региона", g.Sum(p => p.PlanKg),
                    SalesMath.PlanTotal(current.Where(p => _agentRegion.GetValueOrDefault(p.AgentId!.Value, NoRegionId) == g.Key))))
                .OrderByDescending(r => r.PlanKg)
                .ToList();
        }

        var plan = next.Sum(p => p.PlanKg);
        decimal? currentPlan = current.Count == 0 ? null : current.Sum(p => p.PlanKg);
        return new NextMonthPlan(_d.NextMonth.Year, _d.NextMonth.Month, plan, currentPlan, next.Select(p => p.AgentId).Distinct().Count(), rows,
            Change(currentPlan, plan));
    }

    // ================= Справочники и области =================

    private sealed record DirectionGroup(string Id, string Name, string? Subtitle, IReadOnlyList<Guid> Regions);

    private List<DirectionGroup> DirectionGroups()
    {
        var groups = _d.Directions
            .OrderBy(x => x.IsChannel).ThenBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new DirectionGroup(x.Id.ToString(), x.Name, x.Description ?? x.ManagerName,
                _d.Regions.Where(r => r.DirectionId == x.Id).Select(r => r.Id).ToList()))
            .ToList();

        var orphan = RegionIds(null).Where(r => _regions[r].DirectionId is not { } dirId || !_directions.ContainsKey(dirId)).ToList();
        if (orphan.Count > 0)
        {
            groups.Add(new DirectionGroup(NoDirectionId, "Без направления", "Регионы, не назначенные РМ", orphan));
        }

        return groups;
    }

    private IEnumerable<Guid> RegionIds(IReadOnlyCollection<Guid>? scope)
    {
        if (scope is not null)
        {
            return scope;
        }

        // Все регионы + «Без региона», если в нём есть продажи или планы агентов, чей регион не определился.
        var noRegion = RegionPlan(NoRegionId, _d.Plans) is not null ? [NoRegionId] : Array.Empty<Guid>();
        return _d.Regions.Select(r => r.Id)
            .Concat(_currentByRegion.Select(g => g.Key))
            .Concat(noRegion)
            .Distinct();
    }

    private IEnumerable<SaleLine> Lines(IReadOnlySet<Guid>? scope) =>
        scope is null ? _d.Current : scope.SelectMany(r => _currentByRegion[r]);

    private IEnumerable<long> AgentsIn(IReadOnlySet<Guid>? scope) =>
        _roster.Where(a => scope is null || scope.Contains(_agentRegion.GetValueOrDefault(a, NoRegionId)));

    private IEnumerable<VisitRecord> VisitsOf(long agent) => _visitsByAgent[agent].Where(v => v.Date <= _d.DataThrough);

    private sealed record PlanCoverage(decimal? Plan, decimal Fact, int Agents);

    /// <summary>
    /// План подразделения и факт, с которым его честно сравнивать.
    /// План месяца — планы регионов (РОП или «Завод»): выполнение = факт ÷ план (DOC-rules §3), у региона — весь его факт, у РМ и республики —
    /// весь факт их регионов ÷ сумма планов регионов. Регион без своего плана добавляет факт, но не план: продано в нём — продано
    /// в республике (июнь 2026 по РОП: Олмалик без плана, республика 325 995,93 ÷ 387 900 = 84,04%); планы его ТП из Linko не подставляются.
    /// Без планов регионов на месяц — план и факт только тех ТП, у кого есть план в Linko (иначе продажи агентов без плана «выполняли» бы
    /// чужой план).
    /// </summary>
    private PlanCoverage PlanScope(IReadOnlySet<Guid>? scope)
    {
        decimal? plan = null;
        decimal fact = 0;
        var agents = 0;

        foreach (var region in scope?.ToList() ?? _regions.Keys.ToList())
        {
            var manual = SalesMath.PlanTotal(_d.Plans.Where(p => p.RegionId == region));
            if (manual is not null)
            {
                plan = (plan ?? 0) + manual;
                fact += _currentByRegion[region].Sum(l => l.Kg);
                continue;
            }

            if (RegionPlansUsed)
            {
                fact += _currentByRegion[region].Sum(l => l.Kg); // регион без своего плана: факт — в числитель РМ и республики, плана нет
                continue;
            }

            var agentPlans = _d.Plans
                .Where(p => p.AgentId is { } a && _agentRegion.GetValueOrDefault(a, NoRegionId) == region)
                .GroupBy(p => p.AgentId!.Value)
                .Select(g => (Agent: g.Key, Plan: SalesMath.PlanTotal(g)))
                .Where(x => x.Plan != null)
                .ToList();
            if (agentPlans.Count == 0)
            {
                continue;
            }

            plan = (plan ?? 0) + agentPlans.Sum(x => x.Plan!.Value);
            fact += agentPlans.Sum(x => _currentByAgent[x.Agent].Sum(l => l.Kg));
            agents += agentPlans.Count;
        }

        return new PlanCoverage(plan, fact, agents);
    }

    private decimal? PlanOf(IReadOnlySet<Guid>? scope) => PlanScope(scope).Plan;

    /// <summary>План месяца — планы регионов (РОП или «Завод»), а не сумма планов ТП из Linko.</summary>
    private bool RegionPlansUsed => _d.PlanSource != PlanSources.Linko;

    /// <summary>План региона: план региона (РОП или «Завод»), иначе — сумма планов его агентов из Linko.</summary>
    private decimal? RegionPlan(Guid region, IEnumerable<PlanRow> monthPlans)
    {
        var plans = monthPlans as IReadOnlyCollection<PlanRow> ?? monthPlans.ToList();
        var manual = SalesMath.PlanTotal(plans.Where(p => p.RegionId == region));
        if (manual is not null)
        {
            return manual;
        }

        var agents = plans
            .Where(p => p.AgentId is { } a && _agentRegion.GetValueOrDefault(a, NoRegionId) == region)
            .GroupBy(p => p.AgentId)
            .Select(g => SalesMath.PlanTotal(g))
            .Where(t => t != null)
            .ToList();
        return agents.Count == 0 ? null : agents.Sum();
    }

    private FlagCounts FlagCountsOf(IEnumerable<long> agents)
    {
        var worst = agents.Where(a => !IsVacancy(a) && IsSalesRep(a)).Select(a => AgentFlags.Worst(FlagsOf(a))).ToList();
        return new FlagCounts(worst.Count(w => w == FlagSeverity.Critical), worst.Count(w => w == FlagSeverity.Risk));
    }

    /// <summary>
    /// ТП — должность Linko из Sales:SalesRepJobs («Агент») или человек из оргструктуры OneBase. Операторы, супервайзеры, админы
    /// оформляют заказы, но торговыми представителями не считаются: их продажи — в итогах, но не в численности ТП и не в рейтинге.
    /// </summary>
    private bool IsSalesRep(long agent) =>
        _d.SalesRepJobs.Count == 0
        || (_d.Agents.TryGetValue(agent, out var a)
            && (a.InDirectory || (a.Job is { } job && _d.SalesRepJobs.Any(j => string.Equals(j.Trim(), job.Trim(), StringComparison.OrdinalIgnoreCase)))));

    /// <summary>Член команды ТП — ТП или вакансия, как в таблице «Команда ТП»: их визиты и заказы дают конверсию подразделения.</summary>
    private bool InTeam(long agent) => IsSalesRep(agent) || IsVacancy(agent);

    /// <summary>
    /// ТП месяца без вакансий — все, кто есть в составе месяца (продажи, визиты или план), а не только продававшие:
    /// знаменатель «АКБ на агента» и число ТП на плитках.
    /// </summary>
    private int ActiveReps(IReadOnlySet<Guid>? scope) =>
        AgentsIn(scope).Count(a => !IsVacancy(a) && IsSalesRep(a));

    private string? DirectionIdOf(long agent)
    {
        var region = _regions[_agentRegion.GetValueOrDefault(agent, NoRegionId)];
        return region.DirectionId is { } id && _directions.ContainsKey(id) ? id.ToString() : NoDirectionId;
    }

    private string? DirectionName(Guid region) =>
        _regions[region].DirectionId is { } id && _directions.TryGetValue(id, out var dir) ? dir.Name : null;

    private Guid RegionOf(SaleLine line) =>
        line.BranchId is { } b && _regionByBranch.TryGetValue(b, out var id) ? id : NoRegionId;

    private bool IsVacancy(long agent) => _d.Agents.TryGetValue(agent, out var a) && a.IsVacancy;

    private string AgentName(long agent) =>
        _d.Agents.TryGetValue(agent, out var a) && !string.IsNullOrWhiteSpace(a.Name) ? a.Name : $"Агент {agent}";

    private string MarketName(long market) => _d.Markets.TryGetValue(market, out var m) ? m.Name : $"ТТ {market}";

    private AgentStats StatsOf(long agent) =>
        _stats.TryGetValue(agent, out var s) ? s : new AgentStats(agent, 0, 0, 0, 0, new VisitSummary(0, 0, 0), null, IsVacancy(agent));

    private IReadOnlyList<AgentFlag> FlagsOf(long agent) => _flags.GetValueOrDefault(agent) ?? [];

    // ================= Подготовка =================

    private void ResolveRegions()
    {
        var byMarketLines = _d.Current.Concat(_d.Previous).Where(l => l.MarketId != null).ToLookup(l => l.MarketId!.Value);

        // Регион ТТ: её branch, иначе branch её заказов.
        foreach (var market in _d.Markets.Values)
        {
            if (market.BranchId is { } b && _regionByBranch.TryGetValue(b, out var region))
            {
                _marketRegion[market.Id] = region;
                continue;
            }

            var fromLines = byMarketLines[market.Id].GroupBy(RegionOf).MaxBy(g => g.Count())?.Key;
            if (fromLines is { } r && r != NoRegionId)
            {
                _marketRegion[market.Id] = r;
            }
        }

        // Регион агента: из оргструктуры, иначе — где у него больше всего выручки, иначе — по ТТ визитов.
        var agents = _d.Agents.Keys
            .Concat(_d.Current.Concat(_d.Previous).Where(l => l.AgentId != null).Select(l => l.AgentId!.Value))
            .Concat(_d.Visits.Select(v => v.AgentId))
            .Distinct();

        foreach (var agent in agents)
        {
            if (_d.Agents.TryGetValue(agent, out var info) && info.ProfileRegionId is { } profile && _regions.ContainsKey(profile))
            {
                _agentRegion[agent] = profile;
                continue;
            }

            var bySales = _currentByAgent[agent].Concat(_previousByAgent[agent])
                .GroupBy(RegionOf)
                .Where(g => g.Key != NoRegionId)
                .OrderByDescending(g => g.Sum(l => l.Revenue))
                .Select(g => (Guid?)g.Key)
                .FirstOrDefault();
            if (bySales is { } salesRegion)
            {
                _agentRegion[agent] = salesRegion;
                continue;
            }

            var byVisits = _visitsByAgent[agent]
                .Select(v => _marketRegion.GetValueOrDefault(v.MarketId, NoRegionId))
                .Where(r => r != NoRegionId)
                .GroupBy(r => r)
                .MaxBy(g => g.Count())?.Key;
            _agentRegion[agent] = byVisits ?? NoRegionId;
        }

        // ТТ без branch и без заказов — по региону ответственного агента.
        foreach (var market in _d.Markets.Values.Where(m => !_marketRegion.ContainsKey(m.Id)))
        {
            if (market.ResponsibleAgentId is { } agent && _agentRegion.TryGetValue(agent, out var region) && region != NoRegionId)
            {
                _marketRegion[market.Id] = region;
            }
        }
    }

    /// <summary>Действующие ТП месяца: есть продажи, визиты или план, плюс все из оргструктуры.</summary>
    private HashSet<long> BuildRoster() =>
        _currentByAgent.Select(g => g.Key)
            .Concat(_d.Visits.Where(v => v.Date <= _d.DataThrough).Select(v => v.AgentId))
            .Concat(_d.Plans.Where(p => p.AgentId != null).Select(p => p.AgentId!.Value))
            .ToHashSet();

    private void ComputeAgentStats()
    {
        var previousMonths = Enumerable.Range(1, 3).Select(i => _d.MonthStart.AddMonths(-i)).ToList();
        var history = _d.History.Where(h => h.AgentId != null).ToLookup(h => h.AgentId!.Value);

        foreach (var agent in _roster)
        {
            var lines = _currentByAgent[agent].ToList();
            // Заказы — те же, что дали факт (принятые в месяце); сшивка визита с заказом по дню ввода — только для справки.
            var visits = VisitSummary.Of(VisitsOf(agent), _visitByAgent[agent], lines);
            var kg = lines.Sum(l => l.Kg);
            var past = previousMonths.Select(m => history[agent].Where(h => h.Year == m.Year && h.Month == m.Month).Sum(h => h.Kg));
            // Категорий — только категории отчёта: «бонус», импорт и прочие типы Linko ассортимент ТП не расширяют.
            var categories = SalesMath.CategoryCount(lines.Where(l => InReport(GroupOf(l.CategoryId))), GroupOf);

            _stats[agent] = new AgentStats(agent, kg, lines.Sum(l => l.Revenue), SalesMath.Akb(lines), categories,
                visits, SalesMath.TempoToOwnAverage(kg, _d.WorkedDays, _d.DaysInMonth, past), IsVacancy(agent));
        }

        // Медиана региона — по ТП: операторы и супервайзеры с их «визитами» сдвинули бы базу сравнения;
        // из ТП — только оцениваемые (не меньше MinVisits визитов) и без флага «данные не сходятся».
        foreach (var group in _stats.Values.Where(s => IsSalesRep(s.AgentId)).GroupBy(s => _agentRegion.GetValueOrDefault(s.AgentId, NoRegionId)))
        {
            _medians[group.Key] = RegionMedians.Of(group, _d.Thresholds.MinVisits);
        }

        var categoryTarget = (int)Math.Round(_d.Targets.CategoriesPerOutlet);
        foreach (var (agent, stats) in _stats)
        {
            var medians = _medians.GetValueOrDefault(_agentRegion.GetValueOrDefault(agent, NoRegionId)) ?? new RegionMedians(null, null, null);
            _flags[agent] = AgentFlags.Evaluate(stats, medians, _d.Thresholds, categoryTarget, _d.WorkedDays, _d.DaysInMonth);
        }
    }
}
