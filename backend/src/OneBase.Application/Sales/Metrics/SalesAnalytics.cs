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
    private readonly ILookup<Guid, SaleLine> _visitByRegion;
    private readonly DateOnly _previousStart;
    private readonly DateOnly _previousCutoff;

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
        _visitByRegion = data.VisitLines.ToLookup(RegionOf);

        _previousStart = data.MonthStart.AddMonths(-1);
        _previousCutoff = SalesMath.SameDaysCutoff(_previousStart, Math.Max(1, data.WorkedDays));

        ResolveRegions();
        _roster = BuildRoster();
        ComputeAgentStats();
    }

    public PeriodInfo Period => new(_d.Year, _d.Month, _d.DataThrough, _d.WorkedDays, _d.DaysInMonth, _previousCutoff);

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

    public GroupView CachedRepublic(int? from, int? to)
    {
        var (f, t) = VisitRange(from, to);
        return View($"republic:{f}:{t}", () => Republic(f, t));
    }

    public GroupView CachedDirection(string id, int? from, int? to)
    {
        var (f, t) = VisitRange(from, to);
        return View($"direction:{id}:{f}:{t}", () => Direction(id, f, t));
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
        var agents = AgentsIn(null).ToList();
        return new OverviewView(Period, Kpi(null), agents.Count(a => !IsVacancy(a)), FlagCountsOf(agents),
            agents.Count(IsVacancy), Unit("republic", "Республика", null, null, UnitKinds.Republic), Excluded());
    }

    /// <summary>
    /// Республика: карточки РМ/направлений (если они заведены), дальше — категории. Карточек регионов нет:
    /// регионы — в таблице «Все регионы». Служебная группа «Без направления» не показывается.
    /// </summary>
    public GroupView Republic(DateOnly visitsFrom, DateOnly visitsTo)
    {
        var directionCards = DirectionGroups()
            .Where(g => g.Id != NoDirectionId)
            .Select(g => Unit(g.Id, g.Name, g.Subtitle, g.Regions, UnitKinds.Direction))
            .ToList();

        var regions = RegionIds(null).Count(r => r != NoRegionId);
        var parts = new List<string>();
        if (directionCards.Count > 0)
        {
            parts.Add($"{directionCards.Count} {SalesFormat.Plural(directionCards.Count, "направление", "направления", "направлений")}");
        }

        parts.Add($"{regions} {SalesFormat.Plural(regions, "регион", "региона", "регионов")}");
        return Group("Республика", string.Join(" · ", parts), null, directionCards, visitsFrom, visitsTo);
    }

    public GroupView Direction(string id, DateOnly visitsFrom, DateOnly visitsTo)
    {
        var group = DirectionGroups().FirstOrDefault(g => g.Id == id)
            ?? throw new KeyNotFoundException($"Направление {id} не найдено");
        var cards = group.Regions.Select(RegionUnit).ToList();
        return Group(group.Name, group.Subtitle, group.Regions, cards, visitsFrom, visitsTo);
    }

    public RegionView Region(Guid id, DateOnly visitsFrom, DateOnly visitsTo, string calendarMetric, long? calendarCategory)
    {
        var region = _regions[id];
        var scope = new HashSet<Guid> { id };
        var direction = region.DirectionId is { } dirId && _directions.TryGetValue(dirId, out var dir) ? dir : null;
        var team = TeamOf(id);
        var categories = CategoryCardsOf(scope);

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
            Calendar(id, team, calendarMetric, calendarCategory),
            [RegionVisitRow(id, visitsFrom, visitsTo)],
            team.Select(TeamRowOf(id)).OrderBy(t => t.IsVacancy).ThenByDescending(t => t.FactKg).ToList(),
            team.Select(a => Compare(a.ToString(), AgentName(a), $"ID {a}",
                    _currentByRegion[id].Where(l => l.AgentId == a), _previousByRegion[id].Where(l => l.AgentId == a)))
                .OrderByDescending(r => r.KgNow).ToList(),
            AgentNotBought(id),
            NotInDirectory(id),
            categories,
            AkbMonthsOf(scope, categories),
            QualityOf(scope));
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
            .Select(i => new IndicatorPlan(i.IndicatorId, i.Name, i.PlanType, i.Plan, i.Fact, SalesMath.Ratio(i.Fact, i.Plan)))
            .ToList();

        // «Категорий в плане»: категории ручного плана OneBase, иначе — весовые показатели Linko.
        var planCategories = plans.Where(p => p.CategoryId != null).Select(p => p.CategoryId).Distinct().Count();
        if (planCategories == 0)
        {
            planCategories = indicators.Count(i => i.PlanType == "product_sales_weight");
        }

        var lines = _currentByAgent[id].ToList();
        var prevLines = _previousByAgent[id].ToList();

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
            SalesMath.Ratio(stats.Kg, plan),
            stats.Kg,
            stats.Revenue,
            revenuePlan,
            SalesMath.Ratio(stats.Revenue, revenuePlan),
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
            CategoryPlanOf(plans, lines),
            indicators,
            Compare(id.ToString(), AgentName(id), null, lines, prevLines),
            prevMarkets.Count,
            silent.Sum(s => s.PrevRevenue),
            silent,
            newMarkets,
            AgentAssortmentOf(id, regionId),
            SalesMath.Akb(lines),
            AgentAkbMonths(id));
    }

    public ProblemsView Problems(string? directionId, FlagKind? criterion, bool includeVacancies)
    {
        var rank = _roster.Where(a => !IsVacancy(a))
            .OrderByDescending(a => StatsOf(a).Revenue)
            .Select((a, i) => (a, i + 1))
            .ToDictionary(x => x.a, x => x.Item2);

        var list = _roster
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
                var regionId = _agentRegion.GetValueOrDefault(a, NoRegionId);
                var region = _regions[regionId];
                var direction = region.DirectionId is { } dirId && _directions.TryGetValue(dirId, out var dir) ? dir : null;
                return new ProblemAgent(a, AgentName(a), regionId == NoRegionId ? null : regionId.ToString(), region.Name,
                    direction?.Name, rank.TryGetValue(a, out var r) ? r : null, stats.Conversion, stats.Visits.Done,
                    stats.Revenue, IsVacancy(a), FlagsOf(a));
            })
            .OrderBy(p => p.IsVacancy)
            .ThenByDescending(p => p.Flags.Count(f => f.Severity == FlagSeverity.Critical))
            .ThenByDescending(p => p.Flags.Count(f => f.Severity == FlagSeverity.Risk))
            .ThenBy(p => p.Revenue)
            .ToList();

        return new ProblemsView(Period, _roster.Count(IsVacancy), list);
    }

    // ================= Блоки =================

    private GroupView Group(string name, string? subtitle, IReadOnlyCollection<Guid>? scope, List<UnitRow> cards, DateOnly from, DateOnly to)
    {
        var regionIds = RegionIds(scope).ToList();
        var set = scope?.ToHashSet();
        var categories = CategoryCardsOf(set);

        return new GroupView(
            Period,
            name,
            subtitle,
            Kpi(set),
            Unassigned(set),
            cards,
            regionIds.Select(RegionUnit).OrderBy(r => r.Name).ToList(),
            regionIds.Select(r => RegionVisitRow(r, from, to)).Where(r => r.Plan + r.DoneAll + r.OrdersTotal > 0).OrderBy(r => r.Name).ToList(),
            regionIds.Select(r => Compare(r.ToString(), _regions[r].Name, DirectionName(r), _currentByRegion[r], _previousByRegion[r]))
                .OrderBy(r => r.Name).ToList(),
            regionIds.Select(RegionNotBought).OrderBy(r => r.Name).ToList(),
            categories,
            AkbMonthsOf(set, categories),
            QualityOf(set),
            set is null ? Excluded() : null);
    }

    private KpiTiles Kpi(IReadOnlySet<Guid>? scope)
    {
        var lines = Lines(scope).ToList();
        var fact = lines.Sum(l => l.Kg);
        var revenue = lines.Sum(l => l.Revenue);
        var akb = SalesMath.Akb(lines);
        var coverage = PlanScope(scope);
        var plan = coverage.Plan;
        var forecast = SalesMath.Forecast(fact, _d.WorkedDays, _d.DaysInMonth);
        var planForecast = SalesMath.Forecast(coverage.Fact, _d.WorkedDays, _d.DaysInMonth);
        var visits = VisitSummary.Of(AgentsIn(scope).SelectMany(VisitsOf), scope is null ? _d.VisitLines : scope.SelectMany(r => _visitByRegion[r]));
        var active = AgentsIn(scope).Count(a => !IsVacancy(a));

        return new KpiTiles(
            fact,
            plan,
            plan is null ? null : SalesMath.Ratio(coverage.Fact, plan),
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
            planForecast,
            coverage.Agents);
    }

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
        var forecast = SalesMath.Forecast(fact, _d.WorkedDays, _d.DaysInMonth);
        return new RevenuePlanTile(plan, fact, SalesMath.Ratio(fact, plan), forecast,
            forecast is null ? null : SalesMath.Ratio(forecast.Value, plan), plans.Count);
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

            return new PlanPersonRow(
                agent,
                AgentName(agent),
                _d.Agents.TryGetValue(agent, out var info) ? info.Job : null,
                team || regionId == NoRegionId ? null : regionId.ToString(),
                team ? null : _regions[regionId].Name,
                team,
                weightPlan,
                weightFact,
                SalesMath.Ratio(weightFact, weightPlan),
                SumOrNull(list.Where(i => Money(i.PlanType)).Select(i => i.Plan)),
                list.Where(i => Money(i.PlanType)).Sum(i => i.Fact),
                SumOrNull(list.Where(i => Akb(i.PlanType)).Select(i => i.Plan)),
                list.Where(i => Akb(i.PlanType)).Sum(i => i.Fact),
                list.Select(i => new IndicatorPlan(i.IndicatorId, i.Name, i.PlanType, i.Plan, i.Fact, SalesMath.Ratio(i.Fact, i.Plan))).ToList());
        }).ToList();

        var agents = people.Where(p => !p.IsTeamPlan).OrderByDescending(p => p.WeightPlan ?? 0).ToList();
        var teamPlans = people.Where(p => p.IsTeamPlan).OrderByDescending(p => p.WeightPlan ?? 0).ToList();

        var regions = agents
            .GroupBy(p => p.RegionId ?? NoRegionId.ToString())
            .Select(g =>
            {
                var weightPlan = SumOrNull(g.Where(p => p.WeightPlan != null).Select(p => p.WeightPlan!.Value));
                var weightFact = g.Sum(p => p.WeightFact);
                return new PlanRegionRow(g.Key, g.First().RegionName ?? "Без региона", g.Count(), weightPlan, weightFact,
                    SalesMath.Ratio(weightFact, weightPlan), SumOrNull(g.Where(p => p.RevenuePlan != null).Select(p => p.RevenuePlan!.Value)),
                    g.Sum(p => p.RevenueFact));
            })
            .OrderByDescending(r => r.WeightPlan ?? 0)
            .ToList();

        var totalWeightPlan = SumOrNull(agents.Where(p => p.WeightPlan != null).Select(p => p.WeightPlan!.Value));
        var totalWeightFact = agents.Sum(p => p.WeightFact);
        return new PlansView(
            Period,
            totalWeightPlan,
            totalWeightFact,
            SalesMath.Ratio(totalWeightFact, totalWeightPlan),
            SumOrNull(agents.Where(p => p.RevenuePlan != null).Select(p => p.RevenuePlan!.Value)),
            agents.Sum(p => p.RevenueFact),
            agents.Count(p => p.WeightPlan != null || p.RevenuePlan != null),
            _d.Indicators.Count,
            regions,
            agents,
            teamPlans);
    }

    private UnitRow RegionUnit(Guid id) => Unit(id.ToString(), _regions[id].Name, DirectionName(id), [id]);

    private UnitRow Unit(string id, string name, string? subtitle, IReadOnlyCollection<Guid>? scope, string kind = UnitKinds.Region)
    {
        var set = scope?.ToHashSet();
        var kpi = Kpi(set);
        var agents = AgentsIn(set).ToList();
        var regionNames = RegionIds(scope).Where(r => r != NoRegionId).Select(r => _regions[r].Name).OrderBy(n => n).ToList();

        return new UnitRow(id, name, subtitle, kpi.PlanKg, kpi.FactKg, kpi.Execution, kpi.ForecastKg, kpi.ForecastExecution,
            kpi.Revenue, kpi.Akb, kpi.Conversion.Value, kpi.VisitsWithoutOrder, agents.Count(a => !IsVacancy(a)),
            regionNames.Count, regionNames, FlagCountsOf(agents), kpi.PlanFactKg, kind);
    }

    private UnassignedFact Unassigned(IReadOnlySet<Guid>? scope)
    {
        var lines = Lines(scope).ToList();
        var kg = lines.Where(l => l.AgentId == null).Sum(l => l.Kg);
        return new UnassignedFact(kg, SalesMath.Ratio(kg, lines.Sum(l => l.Kg)));
    }

    private VisitCalendarRow RegionVisitRow(Guid id, DateOnly from, DateOnly to)
    {
        var children = AgentsIn(new HashSet<Guid> { id })
            .Select(a => AgentVisitRow(a, from, to))
            .Where(r => r.Plan + r.DoneAll + r.OrdersTotal > 0)
            .OrderByDescending(r => r.DoneAll)
            .ToList();

        var okb = _marketRegion.Count(m => m.Value == id);
        var route = AgentsIn(new HashSet<Guid> { id })
            .SelectMany(VisitsOf)
            .Where(v => v.InPlan && v.Date >= from && v.Date <= to)
            .Select(v => v.MarketId)
            .Distinct()
            .Count();

        return Sum(id.ToString(), _regions[id].Name, $"ОКБ {okb} · в маршруте {route}", children);
    }

    private VisitCalendarRow AgentVisitRow(long agent, DateOnly from, DateOnly to)
    {
        var visits = _visitsByAgent[agent].Where(v => v.Date >= from && v.Date <= to).ToList();
        var doneIn = visits.Where(v => v.Status == VisitStatus.Done && v.InPlan).Select(v => (v.MarketId, v.Date)).ToHashSet();
        var doneOff = visits.Where(v => v.Status == VisitStatus.Done && !v.InPlan).Select(v => (v.MarketId, v.Date)).ToHashSet();

        // Заказ относится к визиту по дате ввода (created_date), а не приёмки: агент стоял в магазине в день ввода.
        var orders = _visitByAgent[agent]
            .Where(l => l.OrderId != null && l.Date >= from && l.Date <= to)
            .GroupBy(l => l.OrderId)
            .Select(g => (Key: (g.First().MarketId ?? 0, g.First().Date), Sum: g.Sum(l => l.Revenue)))
            .ToList();

        var inPlan = orders.Where(o => doneIn.Contains(o.Key)).ToList();
        var offPlan = orders.Where(o => !doneIn.Contains(o.Key) && doneOff.Contains(o.Key)).ToList();
        var plan = visits.Count(v => v.InPlan);
        var doneInCount = visits.Count(v => v.Status == VisitStatus.Done && v.InPlan);
        var doneOffCount = visits.Count(v => v.Status == VisitStatus.Done && !v.InPlan);

        return new VisitCalendarRow(agent.ToString(), AgentName(agent), $"ID {agent}",
            plan, doneInCount, doneOffCount, doneInCount + doneOffCount,
            inPlan.Count, inPlan.Sum(o => o.Sum), offPlan.Count, offPlan.Sum(o => o.Sum), orders.Count, orders.Sum(o => o.Sum),
            Math.Max(0, plan - doneInCount), SalesMath.Ratio(doneInCount, plan), []);
    }

    private static VisitCalendarRow Sum(string id, string name, string? subtitle, IReadOnlyList<VisitCalendarRow> rows)
    {
        var plan = rows.Sum(r => r.Plan);
        var doneIn = rows.Sum(r => r.DoneInPlan);
        return new VisitCalendarRow(id, name, subtitle, plan, doneIn, rows.Sum(r => r.DoneOffPlan), rows.Sum(r => r.DoneAll),
            rows.Sum(r => r.OrdersInPlan), rows.Sum(r => r.OrdersInPlanSum), rows.Sum(r => r.OrdersOffPlan), rows.Sum(r => r.OrdersOffPlanSum),
            rows.Sum(r => r.OrdersTotal), rows.Sum(r => r.OrdersTotalSum), rows.Sum(r => r.NotVisited), SalesMath.Ratio(doneIn, plan), rows);
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

    private NotBoughtRow RegionNotBought(Guid id)
    {
        var prev = _previousByRegion[id].ToList();
        var prevMarkets = SalesMath.ActiveMarkets(prev);
        var nowMarkets = SalesMath.ActiveMarkets(_currentByRegion[id]);
        var silent = prevMarkets.Except(nowMarkets).ToHashSet();

        return new NotBoughtRow(id.ToString(), _regions[id].Name, DirectionName(id), prevMarkets.Count, silent.Count,
            SalesMath.Ratio(silent.Count, prevMarkets.Count),
            prev.Where(l => l.MarketId is { } m && silent.Contains(m)).Sum(l => l.Revenue),
            nowMarkets.Except(prevMarkets).Count(), []);
    }

    /// <summary>«Ещё не купили» по ТП региона: ТТ прошлого месяца закрепляется за агентом, который продал ей больше всех.</summary>
    private List<NotBoughtRow> AgentNotBought(Guid id)
    {
        var prev = _previousByRegion[id].Where(l => l.AgentId != null && l.MarketId != null).ToList();
        var now = _currentByRegion[id].Where(l => l.AgentId != null && l.MarketId != null).ToList();
        var prevMarkets = SalesMath.ActiveMarkets(prev);
        var nowMarkets = SalesMath.ActiveMarkets(_currentByRegion[id]);

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
                    SalesMath.Ratio(silent.Count, baseMarkets.Count),
                    prev.Where(l => l.MarketId is { } m && silent.Contains(m)).Sum(l => l.Revenue),
                    created,
                    silent.Select(m => SilentOf(m, agentPrev)).OrderByDescending(s => s.PrevRevenue).ToList());
            })
            .Where(r => r.Base + r.New > 0)
            .OrderByDescending(r => r.Silent)
            .ToList();
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

    private List<MonthPlanFact> MonthsOf(RegionInfo region) =>
        Enumerable.Range(1, 12).Select(m =>
        {
            var plan = RegionPlan(region.Id, _d.YearRegionPlans.Where(p => p.Month == m).Concat(_d.YearAgentPlans.Where(p => p.Month == m)).ToList());
            decimal? fact = m > _d.Month
                ? null
                : _d.History.Where(h => h.Year == _d.Year && h.Month == m && BranchRegion(h.BranchId) == region.Id)
                    .Sum(h => h.Kg);
            return new MonthPlanFact(m, plan, fact);
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
        var forecast = SalesMath.Forecast(fact, _d.WorkedDays, _d.DaysInMonth);
        var prevKg = _d.ExcludedPrevious.Sum(l => l.Kg);
        return new ExcludedSummary(fact, now.Sum(l => l.Revenue), SalesMath.OrderCount(now), SalesMath.Akb(now), forecast, prevKg,
            SalesMath.Delta(prevKg, forecast ?? fact));
    }

    /// <summary>
    /// Карточки категорий отчёта (клик — артикулы): категории из настройки (фасовки помадки — одна категория).
    /// Доля по весу и дистрибуция — от итога подразделения; «к прошлому месяцу» — прогноз месяца к факту всего прошлого месяца.
    /// Типы вне настройки (импорт, бонус) сюда не входят — они в диагностике (QualityOf).
    /// </summary>
    private List<CategoryCard> CategoryCardsOf(IReadOnlySet<Guid>? scope) =>
        CategoryCardsFor(Lines(scope).ToList(), scope is null ? _d.Previous : scope.SelectMany(r => _previousByRegion[r]).ToList(), republic: scope is null);

    /// <summary>
    /// Карточки категорий по произвольному набору строк (подразделение, агент, экспорт).
    /// republic — набор и есть республика: статуса «не возят» (здесь ноль, а по республике идёт) у неё быть не может.
    /// </summary>
    private List<CategoryCard> CategoryCardsFor(IReadOnlyList<SaleLine> lines, IReadOnlyList<SaleLine> previousLines, bool republic)
    {
        var totalKg = lines.Sum(l => l.Kg);
        var akb = SalesMath.Akb(lines);

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

                var nowByProduct = now.Where(l => l.ProductId != null).ToLookup(l => l.ProductId!.Value);
                var beforeByProduct = before.Where(l => l.ProductId != null).ToLookup(l => l.ProductId!.Value);
                var skus = universe
                    .Select(p =>
                    {
                        var sl = nowByProduct[p].ToList();
                        var skuAkb = SalesMath.Akb(sl);
                        var status = SkuStatusOf(p, soldNow.Contains(p), soldBefore.Contains(p), republic);
                        var product = _d.Products.GetValueOrDefault(p);
                        return new SkuRow(p, product?.Name ?? $"Товар {p}", product?.Code, sl.Sum(l => l.Kg), sl.Sum(l => l.Revenue),
                            skuAkb, SalesMath.Ratio(skuAkb, akb), beforeByProduct[p].Sum(l => l.Kg), status);
                    })
                    .OrderBy(s => SkuStatuses.Rank(s.Status))
                    .ThenByDescending(s => s.FactKg)
                    .ThenByDescending(s => s.PrevMonthKg)
                    .ThenBy(s => s.Name)
                    .ToList();

                var fact = now.Sum(l => l.Kg);
                var revenue = now.Sum(l => l.Revenue);
                var categoryAkb = SalesMath.Akb(now);
                var forecast = SalesMath.Forecast(fact, _d.WorkedDays, _d.DaysInMonth);
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
                    SalesMath.Forecast(revenue, _d.WorkedDays, _d.DaysInMonth),
                    prevKg,
                    SalesMath.Delta(prevKg, forecast ?? fact),
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
        var result = AkbMonthsCore(history, current, previous, series);
        return result with { Categories = result.Categories.OrderByDescending(s => s.Values.Sum(v => v ?? 0)).ToList() };
    }

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

        int? Value(int month, Func<SaleLine, bool>? inCategory, Func<MonthlyAkb, bool> row)
        {
            if (monthLines[month] is { } lines)
            {
                return lines.Count == 0 ? null : SalesMath.Akb(inCategory is null ? lines : lines.Where(inCategory));
            }

            var rows = monthHistory[month].ToList();
            return rows.Count == 0 ? null : rows.Where(row).Sum(r => r.Akb);
        }

        var total = months.Select(m => Value(m, null, r => r.IsTotal)).ToList();
        var series = cards.Select(card =>
        {
            long? id = long.TryParse(card.Id, out var parsed) ? parsed : null;
            var values = months.Select(m => Value(m, l => GroupOf(l.CategoryId) == id, r => !r.IsTotal && r.CategoryId == id)).ToList();
            return new AkbSeries(card.Id, card.Name, values);
        }).ToList();

        var lastPartial = _d.DataThrough < _d.MonthStart.AddMonths(1).AddDays(-1);
        return new AkbByMonth(_d.Year, months, lastPartial, total, series);
    }

    private Guid BranchRegion(long? branch) => branch is { } b && _regionByBranch.TryGetValue(b, out var id) ? id : NoRegionId;

    /// <summary>Календарь месяца по ТП: kg | sum | akb | akb по категории. null — нет данных за день.</summary>
    private MonthCalendar Calendar(Guid region, IReadOnlyList<long> team, string metric, long? category)
    {
        metric = metric is "sum" or "akb" ? metric : "kg";
        var days = _d.DaysInMonth;
        var lastDay = _d.WorkedDays;
        var regionLines = _currentByRegion[region]
            .Where(l => metric != "akb" || category is null || GroupOf(l.CategoryId) == category)
            .ToList();

        decimal? Cell(IReadOnlyCollection<SaleLine> lines) => lines.Count == 0 ? null : metric switch
        {
            "sum" => lines.Sum(l => l.Revenue),
            "akb" => SalesMath.Akb(lines),
            _ => lines.Sum(l => l.Kg),
        };

        var byAgentDay = regionLines.Where(l => l.AgentId != null).ToLookup(l => (l.AgentId!.Value, l.Date.Day));
        var rows = team.Select(agent =>
        {
            var cells = Enumerable.Range(1, days)
                .Select(day => day > lastDay ? null : Cell(byAgentDay[(agent, day)].ToList()))
                .ToList();
            var agentLines = regionLines.Where(l => l.AgentId == agent).ToList();
            return new MonthCalendarRow(agent.ToString(), AgentName(agent), cells, Cell(agentLines) ?? 0);
        }).ToList();

        var byDay = regionLines.ToLookup(l => l.Date.Day);
        var totals = Enumerable.Range(1, days).Select(day => day > lastDay ? null : Cell(byDay[day].ToList())).ToList();
        var sundays = Enumerable.Range(1, days).Select(day => new DateOnly(_d.Year, _d.Month, day).DayOfWeek == DayOfWeek.Sunday).ToList();

        return new MonthCalendar(metric, metric == "akb" ? category : null, sundays, rows, totals, Cell(regionLines) ?? 0);
    }

    private List<long> TeamOf(Guid region) =>
        _currentByRegion[region].Where(l => l.AgentId != null).Select(l => l.AgentId!.Value)
            .Concat(AgentsIn(new HashSet<Guid> { region }))
            .Distinct()
            .ToList();

    private Func<long, TeamRow> TeamRowOf(Guid region) => agent =>
    {
        var lines = _currentByRegion[region].Where(l => l.AgentId == agent).ToList();
        var stats = StatsOf(agent);
        var fact = lines.Sum(l => l.Kg);
        var plan = SalesMath.PlanTotal(_d.Plans.Where(p => p.AgentId == agent));
        var forecast = SalesMath.Forecast(fact, _d.WorkedDays, _d.DaysInMonth);

        return new TeamRow(agent, AgentName(agent), plan, fact, SalesMath.Ratio(fact, plan), forecast,
            forecast is null ? null : SalesMath.Ratio(forecast.Value, plan), lines.Sum(l => l.Revenue),
            stats.Visits.Done, stats.Orders, stats.Conversion, stats.SumPerVisit, stats.Categories,
            IsVacancy(agent), _d.Agents.TryGetValue(agent, out var info) && info.InDirectory, FlagsOf(agent));
    };

    private List<NotInDirectoryRow> NotInDirectory(Guid region) =>
        _currentByRegion[region].Where(l => l.AgentId != null)
            .GroupBy(l => l.AgentId!.Value)
            .Where(g => !(_d.Agents.TryGetValue(g.Key, out var a) && a.InDirectory))
            .Select(g => new NotInDirectoryRow(g.Key, AgentName(g.Key), g.Sum(l => l.Kg), g.Sum(l => l.Revenue)))
            .OrderByDescending(r => r.Kg)
            .ToList();

    /// <summary>
    /// План и факт агента по категориям отчёта: ручные планы OneBase заведены по типам Linko, поэтому и план,
    /// и факт сводятся к категориям отчёта (фасовки помадки — одна строка).
    /// </summary>
    private List<CategoryPlanFact> CategoryPlanOf(IReadOnlyList<PlanRow> plans, IReadOnlyList<SaleLine> lines)
    {
        var categories = plans.Where(p => p.CategoryId != null).Select(p => GroupOf(p.CategoryId))
            .Concat(lines.GroupBy(l => GroupOf(l.CategoryId)).Where(g => g.Sum(l => l.Revenue) > 0).Select(g => g.Key))
            .Distinct();

        return categories.Select(c =>
            {
                var plan = SalesMath.PlanTotal(plans.Where(p => p.CategoryId != null && GroupOf(p.CategoryId) == c));
                var cl = lines.Where(l => GroupOf(l.CategoryId) == c).ToList();
                var fact = cl.Sum(l => l.Kg);
                return new CategoryPlanFact(c, _cats.NameOf(c), plan, fact, cl.Sum(l => l.Revenue), SalesMath.Ratio(fact, plan));
            })
            .OrderBy(x => x.PlanKg is null)
            .ThenByDescending(x => x.FactKg)
            .ToList();
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
    /// Регион с ручным планом — весь факт региона; регион без него — план и факт только тех ТП, у кого есть план
    /// (иначе продажи агентов без плана «выполняли» бы чужой план).
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

    /// <summary>План региона: заданный вручную в OneBase, иначе — сумма планов его агентов (из Linko или ручных).</summary>
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
        var worst = agents.Where(a => !IsVacancy(a)).Select(a => AgentFlags.Worst(FlagsOf(a))).ToList();
        return new FlagCounts(worst.Count(w => w == FlagSeverity.Critical), worst.Count(w => w == FlagSeverity.Risk));
    }

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
            .Concat(_d.Agents.Values.Where(a => a.InDirectory).Select(a => a.Id))
            .ToHashSet();

    private void ComputeAgentStats()
    {
        var previousMonths = Enumerable.Range(1, 3).Select(i => _d.MonthStart.AddMonths(-i)).ToList();
        var history = _d.History.Where(h => h.AgentId != null).ToLookup(h => h.AgentId!.Value);

        foreach (var agent in _roster)
        {
            var lines = _currentByAgent[agent].ToList();
            var visits = VisitSummary.Of(VisitsOf(agent), _visitByAgent[agent]);
            var kg = lines.Sum(l => l.Kg);
            var past = previousMonths.Select(m => history[agent].Where(h => h.Year == m.Year && h.Month == m.Month).Sum(h => h.Kg));

            _stats[agent] = new AgentStats(agent, kg, lines.Sum(l => l.Revenue), SalesMath.Akb(lines), SalesMath.CategoryCount(lines, GroupOf),
                visits, SalesMath.TempoToOwnAverage(kg, _d.WorkedDays, _d.DaysInMonth, past), IsVacancy(agent), SalesMath.OrderCount(lines));
        }

        foreach (var group in _stats.Values.GroupBy(s => _agentRegion.GetValueOrDefault(s.AgentId, NoRegionId)))
        {
            _medians[group.Key] = RegionMedians.Of(group);
        }

        var categoryTarget = (int)Math.Round(_d.Targets.CategoriesPerOutlet);
        foreach (var (agent, stats) in _stats)
        {
            var medians = _medians.GetValueOrDefault(_agentRegion.GetValueOrDefault(agent, NoRegionId)) ?? new RegionMedians(null, null, null);
            _flags[agent] = AgentFlags.Evaluate(stats, medians, _d.Thresholds, categoryTarget, _d.WorkedDays, _d.DaysInMonth);
        }
    }
}
