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
    int ActiveAgents,
    RevenuePlanTile? RevenuePlan = null,
    decimal? PlanFactKg = null,
    decimal? PlanForecastKg = null,
    int PlanAgents = 0);

/// <summary>
/// План по выручке: только агенты, у которых он есть в Linko (sales_sum).
/// Fact — наша выручка этих же агентов, поэтому выполнение не завышается продажами агентов без плана.
/// </summary>
public sealed record RevenuePlanTile(decimal Plan, decimal Fact, decimal? Execution, decimal? Forecast, decimal? ForecastExecution, int Agents);

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
    FlagCounts Flags,
    decimal? PlanFactKg = null,
    string Kind = UnitKinds.Region);

public static class UnitKinds
{
    public const string Republic = "republic";
    public const string Direction = "direction";
    public const string Region = "region";
}

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
public sealed record OverviewView(PeriodInfo Period, KpiTiles Kpi, int ActiveAgents, FlagCounts Flags, int Vacancies, UnitRow Republic, ExcludedSummary? Excluded = null);

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
    IReadOnlyList<NotBoughtRow> NotBought,
    IReadOnlyList<CategoryCard> CategoryCards,
    AkbByMonth AkbMonths,
    DataQualityView Quality,
    ExcludedSummary? Excluded = null,
    IReadOnlyList<CategoryPlanFact>? CategoryPlans = null,
    NextMonthPlan? NextMonth = null);

/// <summary>Тип товара Linko вне категорий отчёта (импорт, бонус, оборудование): не пропадает, а показывается отдельно.</summary>
public sealed record UncategorizedType(string Id, string Name, decimal Kg, decimal Revenue, int Orders);

/// <summary>
/// Качество данных: заказы без даты приёмки (неполная синхронизация), возвраты с нулевой шапкой (учтены по строкам),
/// возвраты без строк (не учтены), типы товаров вне категорий отчёта.
/// </summary>
public sealed record DataQualityView(
    int DeliveredWithoutAcceptance,
    int ZeroHeaderReturns,
    decimal ZeroHeaderReturnsKg,
    int ReturnsWithoutLines,
    decimal ReturnsWithoutLinesHeaderKg,
    IReadOnlyList<UncategorizedType> Uncategorized,
    int AcceptedInFuture = 0,
    IReadOnlyList<CurrencyTotal>? OtherCurrency = null);

/// <summary>Исключённые из вторички филиалы («Завод» — экспорт и опт): факт отдельной строкой.</summary>
public sealed record ExcludedSummary(decimal FactKg, decimal Revenue, int Orders, int Akb, decimal? ForecastKg, decimal PrevMonthKg, decimal? VsPrevMonth);

public sealed record AkbSeries(string Id, string Name, IReadOnlyList<int?> Values);

/// <summary>
/// АКБ по месяцам года: итог и по категориям (те же, что в карточках). null — данных за месяц нет.
/// LastPartial — последний месяц ещё не закончился.
/// </summary>
public sealed record AkbByMonth(int Year, IReadOnlyList<int> Months, bool LastPartial, IReadOnlyList<int?> Total, IReadOnlyList<AkbSeries> Categories);

/// <summary>Статус SKU в подразделении за месяц.</summary>
public static class SkuStatuses
{
    /// <summary>Продаётся в этом месяце.</summary>
    public const string Selling = "selling";

    /// <summary>Не продаётся, и в прошлом месяце тоже не продавался.</summary>
    public const string Silent = "silent";

    /// <summary>Пропал: в прошлом месяце продавался, в этом — нет.</summary>
    public const string Lost = "lost";

    /// <summary>Не возят: здесь ни в этом, ни в прошлом месяце, а по республике в этом месяце продаётся.</summary>
    public const string Elsewhere = "elsewhere";

    /// <summary>Порядок в таблицах: продаётся, пропал, не возят, молчит.</summary>
    public static int Rank(string status) => status switch
    {
        Selling => 0,
        Lost => 1,
        Elsewhere => 2,
        _ => 3,
    };
}

/// <summary>Артикул категории. Дистрибуция — доля АКБ подразделения, купившей этот SKU.</summary>
public sealed record SkuRow(
    long ProductId,
    string Name,
    string? Code,
    decimal FactKg,
    decimal Revenue,
    int Akb,
    decimal? Distribution,
    decimal PrevMonthKg,
    string Status);

/// <summary>
/// Карточка категории. SKU в категории — «живой» ассортимент: товары, которые продавались за последние полгода.
/// «Молчат» — SKU ассортимента без продаж в этом месяце, из них «пропало» — продавались в прошлом месяце.
/// </summary>
public sealed record CategoryCard(
    string Id,
    string Name,
    int SkuSold,
    int SkuTotal,
    decimal FactKg,
    decimal? WeightShare,
    decimal Revenue,
    int Akb,
    decimal? Distribution,
    decimal? ForecastKg,
    decimal? ForecastRevenue,
    decimal PrevMonthKg,
    decimal? VsPrevMonth,
    int Silent,
    int Lost,
    IReadOnlyList<SkuRow> Skus);

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
    IReadOnlyList<AgentFlag> Flags,
    bool IsSalesRep = true,
    string? Job = null);

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
    IReadOnlyList<NotInDirectoryRow> NotInDirectory,
    IReadOnlyList<CategoryCard> CategoryCards,
    AkbByMonth AkbMonths,
    DataQualityView Quality,
    IReadOnlyList<CategoryPlanFact>? CategoryPlans = null,
    NextMonthPlan? NextMonth = null);

public sealed record MedianValue(decimal? Value, decimal? RegionMedian);

/// <summary>
/// План и факт по категории (или паре категорий, если так заведён показатель Linko: «Могуль + Шоколад»).
/// FactKg — факт ТП, у которых есть этот план; ScopeFactKg — весь факт подразделения по этим категориям.
/// Строка без плана — категория продаётся, а плана на неё нет.
/// </summary>
public sealed record CategoryPlanFact(long? CategoryId, string Name, decimal? PlanKg, decimal FactKg, decimal Revenue, decimal? Execution, decimal? ScopeFactKg = null);

/// <summary>Строка плана на следующий месяц: регион (на верхних уровнях) или категория (в регионе).</summary>
public sealed record NextMonthPlanRow(string Id, string Name, decimal PlanKg, decimal? CurrentPlanKg);

/// <summary>План на следующий месяц из Linko — появляется, когда в Linko есть планы ТП на этот месяц.</summary>
public sealed record NextMonthPlan(int Year, int Month, decimal PlanKg, decimal? CurrentPlanKg, int Agents, IReadOnlyList<NextMonthPlanRow> Rows);

/// <summary>План и факт по KPI-показателю Linko (кг по группе товаров, АКБ и т.п.).</summary>
public sealed record IndicatorPlan(long IndicatorId, string Name, string PlanType, decimal Plan, decimal Fact, decimal? Execution);

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
    decimal? RevenuePlan,
    decimal? RevenueExecution,
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
    IReadOnlyList<IndicatorPlan> Indicators,
    SameDaysRow SameDays,
    int SilentBase,
    decimal SilentPrevRevenue,
    IReadOnlyList<SilentMarket> Silent,
    IReadOnlyList<NewMarket> NewMarkets,
    AgentAssortment? Assortment = null,
    int Akb = 0,
    AkbByMonth? AkbMonths = null);

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

/// <summary>Планы сотрудника из Linko за месяц: итоги по типам и все показатели. Факт — по расчёту Linko.</summary>
public sealed record PlanPersonRow(
    long AgentId,
    string Name,
    string? Job,
    string? RegionId,
    string? RegionName,
    bool IsTeamPlan,
    decimal? WeightPlan,
    decimal WeightFact,
    decimal? WeightExecution,
    decimal? RevenuePlan,
    decimal RevenueFact,
    decimal? AkbPlan,
    decimal AkbFact,
    IReadOnlyList<IndicatorPlan> Indicators);

public sealed record PlanRegionRow(string Id, string Name, int Agents, decimal? WeightPlan, decimal WeightFact, decimal? WeightExecution, decimal? RevenuePlan, decimal RevenueFact);

/// <summary>Вкладка «Планы»: всё, что загружено из Linko за месяц.</summary>
public sealed record PlansView(
    PeriodInfo Period,
    decimal? WeightPlan,
    decimal WeightFact,
    decimal? WeightExecution,
    decimal? RevenuePlan,
    decimal RevenueFact,
    int AgentsWithPlan,
    int Indicators,
    IReadOnlyList<PlanRegionRow> Regions,
    IReadOnlyList<PlanPersonRow> Agents,
    IReadOnlyList<PlanPersonRow> TeamPlans);
