namespace OneBase.Application.Sales.Metrics;

/// <summary>
/// Расчёт всех блоков аналитики за месяц. Не обращается к БД — только к MonthData.
/// Факт региона считается по branch строк продаж, поэтому:
/// сумма по регионам = итог республики; сумма по ТП региона + факт без агента = итог региона.
/// </summary>
public sealed class SalesAnalytics
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
    private readonly DateOnly _previousStart;
    private readonly DateOnly _previousCutoff;

    public SalesAnalytics(MonthData data)
    {
        _d = data;
        _regions = data.Regions.ToDictionary(r => r.Id);
        _regions[NoRegionId] = new RegionInfo(NoRegionId, 0, "Без региона", null, null, null);
        _regionByBranch = data.Regions.ToDictionary(r => r.BranchId, r => r.Id);
        _directions = data.Directions.ToDictionary(x => x.Id);

        _currentByRegion = data.Current.ToLookup(RegionOf);
        _previousByRegion = data.Previous.ToLookup(RegionOf);
        _currentByAgent = data.Current.Where(l => l.AgentId != null).ToLookup(l => l.AgentId!.Value);
        _previousByAgent = data.Previous.Where(l => l.AgentId != null).ToLookup(l => l.AgentId!.Value);
        _visitsByAgent = data.Visits.ToLookup(v => v.AgentId);

        _previousStart = data.MonthStart.AddMonths(-1);
        _previousCutoff = SalesMath.SameDaysCutoff(_previousStart, Math.Max(1, data.WorkedDays));

        ResolveRegions();
        _roster = BuildRoster();
        ComputeAgentStats();
    }

    public PeriodInfo Period => new(_d.Year, _d.Month, _d.DataThrough, _d.WorkedDays, _d.DaysInMonth, _previousCutoff);

    public bool HasRegion(Guid id) => _regions.ContainsKey(id);

    public bool HasDirection(string id) => id == NoDirectionId || (Guid.TryParse(id, out var g) && _directions.ContainsKey(g));

    public bool HasAgent(long id) => _roster.Contains(id) || _d.Agents.ContainsKey(id);

    // ================= Уровни =================

    public OverviewView Overview()
    {
        var agents = AgentsIn(null).ToList();
        return new OverviewView(Period, Kpi(null), agents.Count(a => !IsVacancy(a)), FlagCountsOf(agents),
            agents.Count(IsVacancy), Unit("republic", "Республика", null, null));
    }

    public GroupView Republic(DateOnly visitsFrom, DateOnly visitsTo)
    {
        var cards = DirectionGroups()
            .Select(g => Unit(g.Id, g.Name, g.Subtitle, g.Regions))
            .ToList();

        var regions = RegionIds(null).Count(r => r != NoRegionId);
        var subtitle = $"{cards.Count} {SalesFormat.Plural(cards.Count, "направление", "направления", "направлений")} · " +
            $"{regions} {SalesFormat.Plural(regions, "регион", "региона", "регионов")}";
        return Group("Республика", subtitle, null, cards, visitsFrom, visitsTo);
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
            NotInDirectory(id));
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
        var planCategories = plans.Where(p => p.CategoryId != null).Select(p => p.CategoryId).Distinct().Count();

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
            Compare(id.ToString(), AgentName(id), null, lines, prevLines),
            prevMarkets.Count,
            silent.Sum(s => s.PrevRevenue),
            silent,
            newMarkets);
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
            regionIds.Select(RegionNotBought).OrderBy(r => r.Name).ToList());
    }

    private KpiTiles Kpi(IReadOnlySet<Guid>? scope)
    {
        var lines = Lines(scope).ToList();
        var fact = lines.Sum(l => l.Kg);
        var revenue = lines.Sum(l => l.Revenue);
        var akb = SalesMath.Akb(lines);
        var plan = PlanOf(scope);
        var forecast = SalesMath.Forecast(fact, _d.WorkedDays, _d.DaysInMonth);
        var visits = VisitSummary.Of(AgentsIn(scope).SelectMany(VisitsOf), lines);
        var active = AgentsIn(scope).Count(a => !IsVacancy(a));

        return new KpiTiles(
            fact,
            plan,
            SalesMath.Ratio(fact, plan),
            forecast,
            forecast is null ? null : SalesMath.Ratio(forecast.Value, plan),
            revenue,
            akb,
            TargetValue.Of(visits.Conversion, _d.Targets.Conversion),
            TargetValue.Of(SalesMath.Ratio(revenue, akb), _d.Targets.RevenuePerOutlet),
            TargetValue.Of(SalesMath.Ratio(akb, active), _d.Targets.AkbPerAgent),
            visits.WithoutOrder,
            visits.Done,
            active);
    }

    private UnitRow RegionUnit(Guid id) => Unit(id.ToString(), _regions[id].Name, DirectionName(id), [id]);

    private UnitRow Unit(string id, string name, string? subtitle, IReadOnlyCollection<Guid>? scope)
    {
        var set = scope?.ToHashSet();
        var kpi = Kpi(set);
        var agents = AgentsIn(set).ToList();
        var regionNames = RegionIds(scope).Where(r => r != NoRegionId).Select(r => _regions[r].Name).OrderBy(n => n).ToList();

        return new UnitRow(id, name, subtitle, kpi.PlanKg, kpi.FactKg, kpi.Execution, kpi.ForecastKg, kpi.ForecastExecution,
            kpi.Revenue, kpi.Akb, kpi.Conversion.Value, kpi.VisitsWithoutOrder, agents.Count(a => !IsVacancy(a)),
            regionNames.Count, regionNames, FlagCountsOf(agents));
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

        var orders = _currentByAgent[agent]
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
            var plan = SalesMath.PlanTotal(_d.YearRegionPlans.Where(p => p.RegionId == region.Id && p.Month == m));
            decimal? fact = m > _d.Month
                ? null
                : _d.History.Where(h => h.Year == _d.Year && h.Month == m && (region.Id == NoRegionId ? h.BranchId == null || !_regionByBranch.ContainsKey(h.BranchId.Value) : h.BranchId == region.BranchId))
                    .Sum(h => h.Kg);
            return new MonthPlanFact(m, plan, fact);
        }).ToList();

    private List<CategoryShare> CategoriesOf(Guid region)
    {
        var groups = _currentByRegion[region]
            .GroupBy(l => l.CategoryId)
            .Select(g => (Category: g.Key, Revenue: g.Sum(l => l.Revenue)))
            .Where(x => x.Revenue > 0)
            .ToList();
        var total = groups.Sum(x => x.Revenue);

        return groups
            .Select(x => new CategoryShare(x.Category, CategoryName(x.Category), x.Revenue, SalesMath.Ratio(x.Revenue, total)))
            .OrderByDescending(x => x.Revenue)
            .ToList();
    }

    /// <summary>Календарь месяца по ТП: kg | sum | akb | akb по категории. null — нет данных за день.</summary>
    private MonthCalendar Calendar(Guid region, IReadOnlyList<long> team, string metric, long? category)
    {
        metric = metric is "sum" or "akb" ? metric : "kg";
        var days = _d.DaysInMonth;
        var lastDay = _d.WorkedDays;
        var regionLines = _currentByRegion[region]
            .Where(l => metric != "akb" || category is null || l.CategoryId == category)
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

    private List<CategoryPlanFact> CategoryPlanOf(IReadOnlyList<PlanRow> plans, IReadOnlyList<SaleLine> lines)
    {
        var categories = plans.Where(p => p.CategoryId != null).Select(p => p.CategoryId)
            .Concat(lines.GroupBy(l => l.CategoryId).Where(g => g.Sum(l => l.Revenue) > 0).Select(g => g.Key))
            .Distinct();

        return categories.Select(c =>
            {
                var plan = SalesMath.PlanTotal(plans.Where(p => p.CategoryId == c && c != null));
                var cl = lines.Where(l => l.CategoryId == c).ToList();
                var fact = cl.Sum(l => l.Kg);
                return new CategoryPlanFact(c, CategoryName(c), plan, fact, cl.Sum(l => l.Revenue), SalesMath.Ratio(fact, plan));
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

        // Все регионы с данными или планами + «Без региона», если в нём есть продажи.
        return _d.Regions.Select(r => r.Id)
            .Concat(_currentByRegion.Select(g => g.Key))
            .Distinct();
    }

    private IEnumerable<SaleLine> Lines(IReadOnlySet<Guid>? scope) =>
        scope is null ? _d.Current : scope.SelectMany(r => _currentByRegion[r]);

    private IEnumerable<long> AgentsIn(IReadOnlySet<Guid>? scope) =>
        _roster.Where(a => scope is null || scope.Contains(_agentRegion.GetValueOrDefault(a, NoRegionId)));

    private IEnumerable<VisitRecord> VisitsOf(long agent) => _visitsByAgent[agent].Where(v => v.Date <= _d.DataThrough);

    private decimal? PlanOf(IReadOnlySet<Guid>? scope)
    {
        var plans = RegionIds(scope?.ToList())
            .Select(r => SalesMath.PlanTotal(_d.Plans.Where(p => p.RegionId == r)))
            .Where(p => p != null)
            .ToList();
        return plans.Count == 0 ? null : plans.Sum();
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

    private string CategoryName(long? category) =>
        category is { } c && _d.Categories.TryGetValue(c, out var name) ? name : "Без категории";

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

    /// <summary>Действующие ТП месяца: есть продажи или визиты, плюс все из оргструктуры.</summary>
    private HashSet<long> BuildRoster() =>
        _currentByAgent.Select(g => g.Key)
            .Concat(_d.Visits.Where(v => v.Date <= _d.DataThrough).Select(v => v.AgentId))
            .Concat(_d.Agents.Values.Where(a => a.InDirectory).Select(a => a.Id))
            .ToHashSet();

    private void ComputeAgentStats()
    {
        var previousMonths = Enumerable.Range(1, 3).Select(i => _d.MonthStart.AddMonths(-i)).ToList();
        var history = _d.History.Where(h => h.AgentId != null).ToLookup(h => h.AgentId!.Value);

        foreach (var agent in _roster)
        {
            var lines = _currentByAgent[agent].ToList();
            var visits = VisitSummary.Of(VisitsOf(agent), lines);
            var kg = lines.Sum(l => l.Kg);
            var past = previousMonths.Select(m => history[agent].Where(h => h.Year == m.Year && h.Month == m.Month).Sum(h => h.Kg));

            _stats[agent] = new AgentStats(agent, kg, lines.Sum(l => l.Revenue), SalesMath.Akb(lines), SalesMath.CategoryCount(lines),
                visits, SalesMath.TempoToOwnAverage(kg, _d.WorkedDays, _d.DaysInMonth, past), IsVacancy(agent));
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
