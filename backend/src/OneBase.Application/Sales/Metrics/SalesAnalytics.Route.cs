namespace OneBase.Application.Sales.Metrics;

/// <summary>
/// Часть продаж по маршруту: заказы (принятые в днях отрезка — те же, что дали факт «Продаж»), выручка и кг по строкам, АКБ — точки,
/// у которых чистый вес в этой части больше нуля.
/// </summary>
public sealed record RouteSales(int Orders, decimal Revenue, decimal Kg, int Akb);

/// <summary>
/// Маршрут ТП, региона, направления или республики за дни месяца: визиты (план, по маршруту, вне маршрута, не посещено, % плана), визиты
/// с заказом по маршруту и вне его, продажи (заказы, выручка, кг, АКБ) с визитов по маршруту, с визитов вне маршрута и без визита,
/// возвраты (к маршруту не относятся) и итог — факт «Продаж». Agents — строки ТП (у региона и одного ТП).
/// </summary>
public sealed record RouteSplitRow(
    string Id,
    string Name,
    string? Region,
    int Plan,
    int DoneInPlan,
    int DoneOffPlan,
    int NotVisited,
    decimal? PlanShare,
    int VisitsWithOrderInPlan,
    int VisitsWithOrderOffPlan,
    RouteSales ByRoute,
    RouteSales OffRoute,
    RouteSales NoVisit,
    decimal ReturnsRevenue,
    decimal ReturnsKg,
    RouteSales Total,
    IReadOnlyList<RouteSplitRow> Agents)
{
    /// <summary>Доля выручки заказов с визитов по маршруту в выручке всех заказов (без возвратов).</summary>
    public decimal? ByRouteRevenueShare => SalesMath.Ratio(ByRoute.Revenue, ByRoute.Revenue + OffRoute.Revenue + NoVisit.Revenue);
}

/// <summary>День области: факт (кг, выручка, заказы, АКБ дня — с возвратами, как в «Продажах») и выполненные визиты.</summary>
public sealed record RouteDay(DateOnly Date, decimal Kg, decimal Revenue, int Orders, int Akb, int VisitsDone);

/// <summary>OutsideTeam — продажи вне команды (заказы без ТП, не ТП — «Филиал Оператор», «Офис», ТП без региона): они в итоге и днях, строк ТП у них нет.</summary>
public sealed record RouteSplitView(PeriodInfo Period, DateOnly From, DateOnly To, RouteSplitRow Total, IReadOnlyList<RouteSplitRow> Regions,
    IReadOnlyList<RouteDay> Days, RouteSales? OutsideTeam = null);

public sealed partial class SalesAnalytics
{
    /// <summary>
    /// Разделение «по маршруту / вне маршрута» за дни from–to месяца (по отчётный день) — по тем же правилам, что календарь визитов
    /// (DOC-rules §9.1): заказ «по маршруту», если в день его ввода в эту точку был выполненный визит по плану, «вне маршрута» — если только
    /// визит без плана, иначе «без визита»; визит «с заказом» — у этого ТП в этой точке в этот день есть заказ (по дню ввода).
    /// Область: агент, иначе регион, иначе направление (РМ), иначе республика. Строки ТП — у региона и одного ТП; withAgents — и у шире.
    /// </summary>
    public RouteSplitView RouteSplit(DateOnly from, DateOnly to, Guid? region = null, Guid? direction = null, long? agent = null, bool withAgents = false)
    {
        var monthEnd = _d.MonthStart.AddMonths(1).AddDays(-1);
        var last = _d.DataThrough < monthEnd ? _d.DataThrough : monthEnd;
        from = from < _d.MonthStart ? _d.MonthStart : from;
        to = to > last ? last : to;

        IReadOnlyList<Guid> regions = agent is { } a
            ? [_agentRegion.GetValueOrDefault(a, NoRegionId)]
            : region is { } r
                ? [r]
                : _regions.Keys.Where(id => id != NoRegionId && (direction is not { } d || _regions[id].DirectionId == d)).ToList();

        var keep = agent is not null || region is not null || withAgents;
        var rows = new List<RouteSplitRow>();
        var scopeLines = new List<SaleLine>();
        var scopeAgents = new List<long>();
        // Продажи вне команды — по филиалу в итоге региона, как в «Продажах»: иначе факт дней меньше факта страниц (Самарканд — «Филиал Оператор»,
        // Термез — «Офис»). ТП команды считаются по себе (в любом филиале), поэтому их строки сюда не попадают. У одного ТП — только его строки.
        var counted = agent is null ? AgentsIn(null).Where(x => InTeam(x) && _agentRegion.GetValueOrDefault(x, NoRegionId) != NoRegionId).ToHashSet() : null;
        var outside = new List<SaleLine>();
        foreach (var id in regions)
        {
            List<long> team = agent is { } one ? [one] : AgentsIn(new HashSet<Guid> { id }).Where(InTeam).ToList();
            var agentRows = team.Select(x => RouteAgent(x, from, to))
                .Where(x => x.Plan + x.DoneInPlan + x.DoneOffPlan + x.Total.Orders > 0 || x.ReturnsKg != 0)
                .OrderByDescending(x => x.Total.Revenue)
                .ToList();
            var lines = team.SelectMany(x => AgentLines(x, from, to)).ToList();
            if (counted is not null)
            {
                var extra = _currentByRegion[id].Where(l => l.Date >= from && l.Date <= to && (l.AgentId is not { } owner || !counted.Contains(owner))).ToList();
                lines.AddRange(extra);
                outside.AddRange(extra);
            }

            scopeLines.AddRange(lines);
            scopeAgents.AddRange(team);
            var row = Combine(id.ToString(), _regions.TryGetValue(id, out var info) ? info.Name : "Без региона", null, agentRows, lines, keep);
            if (row.Plan + row.DoneInPlan + row.DoneOffPlan + row.Total.Orders > 0 || agent is not null || region is not null)
            {
                rows.Add(row);
            }
        }

        var regionRows = rows.OrderByDescending(x => x.Total.Revenue).ToList();
        var name = agent is { } ag ? AgentName(ag)
            : region is { } rg ? (_regions.TryGetValue(rg, out var ri) ? ri.Name : "Без региона")
            : direction is { } dg && _directions.TryGetValue(dg, out var di) ? di.Name
            : "Республика";
        var total = Combine("total", name, null, regionRows, scopeLines, keepChildren: false);
        var byDay = scopeLines.ToLookup(l => l.Date);
        var visitsByDay = scopeAgents.Distinct().SelectMany(x => _visitsByAgent[x]).Where(v => v.Status == VisitStatus.Done && v.Date >= from && v.Date <= to)
            .ToLookup(v => v.Date);
        var days = new List<RouteDay>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var lines = byDay[day].ToList();
            days.Add(new RouteDay(day, lines.Sum(l => l.Kg), lines.Sum(l => l.Revenue), SalesMath.OrderCount(lines), SalesMath.Akb(lines), visitsByDay[day].Count()));
        }

        return new RouteSplitView(Period, from, to, total, regionRows, days, counted is null ? null : Part(outside));
    }

    /// <summary>Выручка ТП за дни отрезка по правилам вторички (проданное по дате приёмки, без «Завода») — факт месяца к дню для дневного плана.</summary>
    public decimal AgentRevenue(long agent, DateOnly from, DateOnly to) => AgentLines(agent, from, to).Sum(l => l.Revenue);

    /// <summary>Строки факта ТП за дни отрезка (по дате реализации — как в «Продажах»).</summary>
    private IEnumerable<SaleLine> AgentLines(long agent, DateOnly from, DateOnly to) =>
        _currentByAgent[agent].Where(l => l.Date >= from && l.Date <= to);

    private RouteSplitRow RouteAgent(long agent, DateOnly from, DateOnly to)
    {
        var visits = _visitsByAgent[agent].Where(v => v.Date >= from && v.Date <= to).ToList();
        var done = visits.Where(v => v.Status == VisitStatus.Done).ToList();
        // Визит «с заказом» — у этого ТП в этой точке в этот день есть заказ по дню ввода (как «Визиты с заказом» карточки ТП).
        var orderKeys = _visitByAgent[agent].Where(l => l.OrderId != null && l.MarketId != null).Select(l => (l.MarketId!.Value, l.Date)).ToHashSet();
        var plan = visits.Count(PlannedSoFar);
        var doneIn = done.Count(v => v.InPlan);
        var lines = AgentLines(agent, from, to).ToList();
        var (byRoute, offRoute, noVisit, returns) = SplitLines(lines);
        var region = _agentRegion.GetValueOrDefault(agent, NoRegionId);
        return new RouteSplitRow(agent.ToString(), AgentName(agent), _regions.TryGetValue(region, out var info) ? info.Name : null,
            plan, doneIn, done.Count - doneIn, Math.Max(0, plan - doneIn), SalesMath.Ratio(doneIn, plan),
            done.Count(v => v.InPlan && orderKeys.Contains((v.MarketId, v.Date))), done.Count(v => !v.InPlan && orderKeys.Contains((v.MarketId, v.Date))),
            Part(byRoute), Part(offRoute), Part(noVisit), returns.Sum(l => l.Revenue), returns.Sum(l => l.Kg), Part(lines), []);
    }

    /// <summary>Итог строк: визиты и заказы — суммы, АКБ — по строкам продаж всей области (точка у двух ТП — одна).</summary>
    private RouteSplitRow Combine(string id, string name, string? region, IReadOnlyList<RouteSplitRow> children, IReadOnlyList<SaleLine> lines, bool keepChildren)
    {
        var (byRoute, offRoute, noVisit, returns) = SplitLines(lines);
        var plan = children.Sum(c => c.Plan);
        var doneIn = children.Sum(c => c.DoneInPlan);
        return new RouteSplitRow(id, name, region, plan, doneIn, children.Sum(c => c.DoneOffPlan), Math.Max(0, plan - doneIn), SalesMath.Ratio(doneIn, plan),
            children.Sum(c => c.VisitsWithOrderInPlan), children.Sum(c => c.VisitsWithOrderOffPlan),
            Part(byRoute), Part(offRoute), Part(noVisit), returns.Sum(l => l.Revenue), returns.Sum(l => l.Kg), Part(lines), keepChildren ? children : []);
    }

    /// <summary>
    /// Строки заказов по маршруту: в день ввода заказа (created_date) в точке был выполненный визит по плану — «по маршруту», только визит
    /// без плана — «вне маршрута», иначе «без визита». Возвраты (строки без заказа) — отдельно.
    /// </summary>
    private (List<SaleLine> ByRoute, List<SaleLine> OffRoute, List<SaleLine> NoVisit, List<SaleLine> Returns) SplitLines(IEnumerable<SaleLine> lines)
    {
        List<SaleLine> byRoute = [], offRoute = [], noVisit = [], returns = [];
        foreach (var l in lines)
        {
            if (l.OrderId is null)
            {
                returns.Add(l);
                continue;
            }

            var key = (l.MarketId ?? 0, l.CreatedDate ?? l.Date);
            (_doneInPlan.Contains(key) ? byRoute : _doneOffPlan.Contains(key) ? offRoute : noVisit).Add(l);
        }

        return (byRoute, offRoute, noVisit, returns);
    }

    private static RouteSales Part(IReadOnlyCollection<SaleLine> lines) =>
        new(SalesMath.OrderCount(lines), lines.Sum(l => l.Revenue), lines.Sum(l => l.Kg), SalesMath.Akb(lines));
}
