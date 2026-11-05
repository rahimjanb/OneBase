namespace OneBase.Application.Sales.Metrics;

/// <summary>
/// Карточка ТП в разделе «Агенты» — те же цифры, что в карточке агента (Agent): план ТП из Linko и выполнение по весу, прогноз (у идущего
/// месяца), выручка и её план, АКБ, визиты, заказы, страйк, сумма на визит, категории, темп к своему среднему и замечания. Регион, РМ и
/// супервайзер — по региону ТП (профиль или продажи).
/// </summary>
public sealed record AgentCardRow(
    long AgentId,
    string Name,
    string? RegionId,
    string RegionName,
    string? DirectionId,
    string? DirectionName,
    string? Supervisor,
    bool IsVacancy,
    bool IsSalesRep,
    string? Job,
    decimal? PlanKg,
    decimal FactKg,
    decimal? Execution,
    decimal? ForecastKg,
    decimal? ForecastExecution,
    decimal Revenue,
    decimal? RevenuePlan,
    decimal? RevenueExecution,
    int Akb,
    int Visits,
    int VisitsWithOrder,
    int Orders,
    decimal? Conversion,
    decimal? SumPerVisit,
    int Categories,
    decimal? Tempo,
    IReadOnlyList<AgentFlag> Flags,
    TargetLevel? ExecutionLevel,
    TargetLevel? ForecastExecutionLevel);

/// <summary>«Агенты»: все ТП команд регионов за месяц (и вакансии) и справочники для фильтров — РМ и регионы с ТП. Targets — цели компании.</summary>
public sealed record AgentsView(
    PeriodInfo Period,
    SalesTargets Targets,
    IReadOnlyList<AgentCardRow> Agents,
    IReadOnlyList<UnitRef> Directions,
    IReadOnlyList<UnitRef> Regions);

public sealed partial class SalesAnalytics
{
    public AgentsView CachedAgents() => View("agents", Agents);

    /// <summary>ТП месяца: с продажами, визитами или планом, из команды (ТП по должности или справочнику, вакансии).</summary>
    private AgentsView Agents()
    {
        var rows = _roster.Where(InTeam).Select(AgentCard)
            .OrderBy(a => a.RegionName, StringComparer.CurrentCulture).ThenByDescending(a => a.FactKg).ThenBy(a => a.Name, StringComparer.CurrentCulture)
            .ToList();
        var directions = rows.Where(r => r.DirectionId is not null).GroupBy(r => r.DirectionId!)
            .Select(g => new UnitRef(g.Key, g.First().DirectionName ?? "—"))
            .OrderBy(d => _directions.TryGetValue(Guid.Parse(d.Id), out var info) ? info.SortOrder : int.MaxValue).ThenBy(d => d.Name, StringComparer.CurrentCulture)
            .ToList();
        var regions = rows.GroupBy(r => r.RegionId ?? NoRegionId.ToString())
            .Select(g => new UnitRef(g.Key, g.First().RegionName))
            .OrderBy(r => r.Name, StringComparer.CurrentCulture)
            .ToList();
        return new AgentsView(Period, _d.Targets, rows, directions, regions);
    }

    private AgentCardRow AgentCard(long id)
    {
        var stats = StatsOf(id);
        var regionId = _agentRegion.GetValueOrDefault(id, NoRegionId);
        var region = _regions[regionId];
        var direction = region.DirectionId is { } dirId && _directions.TryGetValue(dirId, out var dir) ? dir : null;
        var plan = SalesMath.PlanTotal(_d.Plans.Where(p => p.AgentId == id));
        var revenuePlans = _d.RevenuePlans.Where(p => p.AgentId == id).ToList();
        decimal? revenuePlan = revenuePlans.Count == 0 ? null : revenuePlans.Sum(p => p.PlanKg);
        var execution = SalesMath.Ratio(stats.Kg, plan);
        var forecast = ForecastOf(stats.Kg);
        var forecastExecution = forecast is null ? null : SalesMath.Ratio(forecast.Value, plan);
        var info = _d.Agents.GetValueOrDefault(id);

        return new AgentCardRow(
            id,
            AgentName(id),
            regionId == NoRegionId ? null : regionId.ToString(),
            region.Name,
            direction?.Id.ToString(),
            direction is null ? null : direction.ManagerName ?? direction.Name,
            region.Supervisor,
            IsVacancy(id),
            IsSalesRep(id),
            info?.Job,
            plan,
            stats.Kg,
            execution,
            forecast,
            forecastExecution,
            stats.Revenue,
            revenuePlan,
            SalesMath.Ratio(stats.Revenue, revenuePlan),
            stats.Akb,
            stats.Visits.Done,
            stats.Visits.WithOrder,
            stats.Orders,
            stats.Conversion,
            stats.SumPerVisit,
            stats.Categories,
            stats.Tempo,
            FlagsOf(id),
            SalesMath.ExecutionLevelOf(execution),
            SalesMath.ExecutionLevelOf(forecastExecution));
    }
}
