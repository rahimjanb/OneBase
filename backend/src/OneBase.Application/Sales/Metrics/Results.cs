namespace OneBase.Application.Sales.Metrics;

// Результаты расчёта — отдаются API как есть. Доли — от 0 до 1; null — «нет данных» (в UI «—»).

public sealed record PeriodInfo(int Year, int Month, DateOnly DataThrough, int WorkedDays, int DaysInMonth, DateOnly PreviousCutoff);

public sealed record TargetValue(decimal? Value, decimal Target, decimal? Ratio, TargetLevel? Level)
{
    public static TargetValue Of(decimal? value, decimal target) =>
        new(value, target, value is null ? null : SalesMath.Ratio(value.Value, target), SalesMath.TargetLevelOf(value, target));
}

public sealed record KpiTiles(
    decimal FactKg,
    decimal? PlanKg,
    decimal? Execution,
    decimal? ForecastKg,
    decimal? ForecastExecution,
    decimal Revenue,
    int Akb,
    TargetValue Conversion,
    TargetValue RevenuePerOutlet,
    TargetValue AkbPerAgent,
    int VisitsWithoutOrder,
    int VisitsDone,
    int ActiveAgents);

public sealed record FlagCounts(int Critical, int Risk);

/// <summary>Строка/карточка подразделения: республика, направление (РМ) или регион.</summary>
public sealed record UnitRow(
    string Id,
    string Name,
    string? Subtitle,
    decimal? PlanKg,
    decimal FactKg,
    decimal? Execution,
    decimal? ForecastKg,
    decimal? ForecastExecution,
    decimal Revenue,
    int Akb,
    decimal? Strike,
    int VisitsWithoutOrder,
    int Agents,
    int RegionCount,
    IReadOnlyList<string> RegionNames,
    FlagCounts Flags);

public sealed record UnassignedFact(decimal Kg, decimal? Share);

public sealed record VisitCalendarRow(
    string Id,
    string Name,
    string? Subtitle,
    int Plan,
    int DoneInPlan,
    int DoneOffPlan,
    int DoneAll,
    int OrdersInPlan,
    decimal OrdersInPlanSum,
    int OrdersOffPlan,
    decimal OrdersOffPlanSum,
    int OrdersTotal,
    decimal OrdersTotalSum,
    int NotVisited,
    decimal? PlanShare,
    IReadOnlyList<VisitCalendarRow> Children);

public sealed record SameDaysRow(
    string Id,
    string Name,
    string? Subtitle,
    decimal KgBefore,
    decimal KgNow,
    decimal? KgDelta,
    decimal SumBefore,
    decimal SumNow,
    decimal? SumDelta,
    int AkbNow,
    decimal? AkbDelta);

public sealed record SilentMarket(long MarketId, string Name, decimal PrevKg, decimal PrevRevenue, int Sku);

public sealed record NewMarket(long MarketId, string Name, decimal Kg, decimal Revenue);

public sealed record NotBoughtRow(
    string Id,
    string Name,
    string? Subtitle,
    int Base,
    int Silent,
    decimal? Share,
    decimal SilentPrevRevenue,
    int New,
    IReadOnlyList<SilentMarket> SilentMarkets);

/// <summary>Уровень 0: старт раздела.</summary>
public sealed record OverviewView(PeriodInfo Period, KpiTiles Kpi, int ActiveAgents, FlagCounts Flags, int Vacancies, UnitRow Republic);

/// <summary>Уровни 1 и 2: республика и направление (РМ).</summary>
public sealed record GroupView(
    PeriodInfo Period,
    string Name,
    string? Subtitle,
    KpiTiles Kpi,
    UnassignedFact Unassigned,
    IReadOnlyList<UnitRow> Cards,
    IReadOnlyList<UnitRow> Regions,
    IReadOnlyList<VisitCalendarRow> VisitCalendar,
    IReadOnlyList<SameDaysRow> SameDays,
    IReadOnlyList<NotBoughtRow> NotBought);

public sealed record MonthPlanFact(int Month, decimal? PlanKg, decimal? FactKg);

public sealed record CategoryShare(long? CategoryId, string Name, decimal Revenue, decimal? Share);

public sealed record MonthCalendarRow(string Id, string Name, IReadOnlyList<decimal?> Days, decimal Total);

public sealed record MonthCalendar(string Metric, long? CategoryId, IReadOnlyList<bool> Sundays, IReadOnlyList<MonthCalendarRow> Rows, IReadOnlyList<decimal?> TotalDays, decimal Total);

public sealed record TeamRow(
    long AgentId,
    string Name,
    decimal? PlanKg,
    decimal FactKg,
    decimal? Execution,
    decimal? ForecastKg,
    decimal? ForecastExecution,
    decimal Revenue,
    int Visits,
    int Orders,
    decimal? Strike,
    decimal? SumPerVisit,
    int Categories,
    bool IsVacancy,
    bool InDirectory,
    IReadOnlyList<AgentFlag> Flags);

public sealed record NotInDirectoryRow(long AgentId, string Name, decimal Kg, decimal Revenue);

/// <summary>Уровень 3: регион.</summary>
public sealed record RegionView(
    PeriodInfo Period,
    string Id,
    string Name,
    string? DirectionId,
    string? DirectionName,
    string? Supervisor,
    string? Dealer,
    KpiTiles Kpi,
    UnassignedFact Unassigned,
    IReadOnlyList<MonthPlanFact> Months,
    IReadOnlyList<CategoryShare> Categories,
    MonthCalendar Calendar,
    IReadOnlyList<VisitCalendarRow> VisitCalendar,
    IReadOnlyList<TeamRow> Team,
    IReadOnlyList<SameDaysRow> SameDays,
    IReadOnlyList<NotBoughtRow> NotBought,
    IReadOnlyList<NotInDirectoryRow> NotInDirectory);

public sealed record MedianValue(decimal? Value, decimal? RegionMedian);

public sealed record CategoryPlanFact(long? CategoryId, string Name, decimal? PlanKg, decimal FactKg, decimal Revenue, decimal? Execution);

/// <summary>Уровень 4: карточка агента.</summary>
public sealed record AgentView(
    PeriodInfo Period,
    long AgentId,
    string Name,
    string? RegionId,
    string? RegionName,
    string? DirectionId,
    string? DirectionName,
    bool IsVacancy,
    decimal? PlanKg,
    decimal? Execution,
    decimal FactKg,
    decimal Revenue,
    int Visits,
    int VisitsWithOrder,
    MedianValue Conversion,
    MedianValue SumPerVisit,
    MedianValue AvgCheck,
    int Categories,
    int? PlanCategories,
    decimal CategoryTarget,
    decimal? Tempo,
    IReadOnlyList<AgentFlag> Flags,
    IReadOnlyList<CategoryPlanFact> CategoryPlan,
    SameDaysRow SameDays,
    int SilentBase,
    decimal SilentPrevRevenue,
    IReadOnlyList<SilentMarket> Silent,
    IReadOnlyList<NewMarket> NewMarkets);

public sealed record ProblemAgent(
    long AgentId,
    string Name,
    string? RegionId,
    string? RegionName,
    string? DirectionName,
    int? Rank,
    decimal? Conversion,
    int Visits,
    decimal Revenue,
    bool IsVacancy,
    IReadOnlyList<AgentFlag> Flags);

public sealed record ProblemsView(PeriodInfo Period, int Vacancies, IReadOnlyList<ProblemAgent> Agents);
