namespace OneBase.Application.Sales.Metrics;

// Результаты расчёта — отдаются API как есть. Доли — от 0 до 1; null — «нет данных» (в UI «—»).

/// <summary>
/// Период отчёта. DataThrough — отчётный день (последний полный день с данными); Closed — месяц закрыт: прогноза нет, чип «данные по» не нужен.
/// Plan — вид плана, по которому посчитан ответ (rop / factory); AvailablePlans — какие планы регионов заведены на этот месяц (подмножество
/// rop, factory): переключатель плана не предлагает остальные, а без выбранного вида план подразделений — из Linko (KpiTiles.PlanSource).
/// </summary>
public sealed record PeriodInfo(
    int Year,
    int Month,
    DateOnly DataThrough,
    int WorkedDays,
    int DaysInMonth,
    DateOnly PreviousCutoff,
    string Plan,
    IReadOnlyList<string> AvailablePlans)
{
    public bool Closed => WorkedDays >= DaysInMonth;
}

public sealed record TargetValue(decimal? Value, decimal Target, decimal? Ratio, TargetLevel? Level)
{
    public static TargetValue Of(decimal? value, decimal target) =>
        new(value, target, value is null ? null : SalesMath.Ratio(value.Value, target), SalesMath.TargetLevelOf(value, target));
}

/// <summary>
/// Откуда план подразделений (регион, РМ, республика): rop — план РОП, factory — план «Завод» (планы регионов × категорий из
/// sales."RegionPlans", переключатель plan=rop|factory); linko — планов регионов выбранного вида на месяц нет, план — сумма планов ТП
/// из Linko (staff_balance). План ТП (карточка агента, команда региона) — всегда из Linko.
/// </summary>
public static class PlanSources
{
    public const string Rop = "rop";
    public const string Factory = "factory";
    public const string Linko = "linko";
}

/// <summary>
/// Плитки KPI. Прогнозы — null на закрытом месяце. Визиты, визиты без заказа и конверсия — по ТП подразделения (команда с вакансиями):
/// конверсия = заказы ТП, принятые в месяце ÷ их выполненные визиты, без заказа = визиты − заказы. VisitsOutsideTeam — выполненные
/// визиты остальных (операторы, супервайзеры, пользователи вне справочника): в конверсию не входят, но и не теряются.
/// ActiveAgents — ТП месяца без вакансий (продажи, визиты или план) — знаменатель «АКБ на агента». PlanSource — откуда план (PlanSources):
/// у плана РОП и «Завод» выполнение = весь факт подразделения (регион без своего плана добавляет факт, но не план) ÷ сумма планов регионов,
/// у планов ТП из Linko — факт ТП с планом ÷ их план. ExecutionLevel — цвет выполнения (SalesMath.ExecutionLevelOf).
/// </summary>
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
    int PlanAgents = 0,
    int VisitsOutsideTeam = 0,
    string PlanSource = PlanSources.Linko,
    TargetLevel? ExecutionLevel = null);

/// <summary>
/// План по выручке: только агенты, у которых он есть в Linko (sales_sum).
/// Fact — наша выручка этих же агентов, поэтому выполнение не завышается продажами агентов без плана.
/// </summary>
public sealed record RevenuePlanTile(decimal Plan, decimal Fact, decimal? Execution, decimal? Forecast, decimal? ForecastExecution, int Agents,
    TargetLevel? ExecutionLevel = null);

public sealed record FlagCounts(int Critical, int Risk);

/// <summary>Строка/карточка подразделения: республика, направление (РМ) или регион. Уровни цвета выполнения и прогноза — с сервера.</summary>
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
    string Kind = UnitKinds.Region,
    TargetLevel? ExecutionLevel = null,
    TargetLevel? ForecastExecutionLevel = null);

public static class UnitKinds
{
    public const string Republic = "republic";
    public const string Direction = "direction";
    public const string Region = "region";
}

public sealed record UnassignedFact(decimal Kg, decimal? Share);

/// <summary>
/// Строка календаря визитов (DOC-rules §9.1): ТП, регион или итог. Заказы — принятые в выбранные дни (те же, что дали факт); «с визита
/// по маршруту» / «вне маршрута» — в день ввода заказа в этот магазин был выполненный визит по плану / без плана (любого ТП),
/// OrdersNoVisit — заказы без такого визита. NotVisited = max(0, план − по маршруту) и PlanShare = по маршруту ÷ план — по итогам самой
/// строки (у региона и республики — по их суммам, а не сумме недоборов ТП); PlanShareLevel — цвет (SalesMath.ExecutionLevelOf).
/// VisitsOutsideTeam — выполненные визиты не ТП подразделения (операторы, супервайзеры): в строки не входят, но и не теряются.
/// </summary>
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
    IReadOnlyList<VisitCalendarRow> Children,
    int OrdersNoVisit = 0,
    int VisitsOutsideTeam = 0,
    TargetLevel? PlanShareLevel = null);

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

/// <summary>
/// «Ещё не купили»: база — ТТ прошлого месяца, молчат — те, что в этом ещё не купили. NoData — в регионе в этом месяце нет ни одной
/// покупки (почти всегда дыра в данных, а не разом замолчавшая база): доли нет. У строки итога NoData — нет данных хотя бы у одной строки.
/// </summary>
public sealed record NotBoughtRow(
    string Id,
    string Name,
    string? Subtitle,
    int Base,
    int Silent,
    decimal? Share,
    decimal SilentPrevRevenue,
    int New,
    IReadOnlyList<SilentMarket> SilentMarkets,
    bool NoData = false);

/// <summary>Уровень 0: старт раздела.</summary>
public sealed record OverviewView(PeriodInfo Period, KpiTiles Kpi, int ActiveAgents, FlagCounts Flags, int Vacancies, UnitRow Republic, ExcludedSummary? Excluded = null);

/// <summary>
/// Уровни 1 и 2: республика и направление (РМ). VisitCalendarTotal — итог календаря визитов по строкам регионов (с сервера);
/// Calendar — календарь месяца по регионам (те же меры, что у календаря по ТП региона: metric и category в запросе).
/// </summary>
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
    ExcludedSummary? Excluded,
    IReadOnlyList<CategoryPlanFact> CategoryPlans,
    NextMonthPlan? NextMonth,
    NotBoughtRow NotBoughtTotal,
    CategoryPlanFact? CategoryPlanTotal,
    VisitCalendarRow? VisitCalendarTotal = null,
    MonthCalendar? Calendar = null);

/// <summary>Тип товара Linko вне категорий отчёта (импорт, бонус, оборудование): не пропадает, а показывается отдельно.</summary>
public sealed record UncategorizedType(string Id, string Name, decimal Kg, decimal Revenue, int Orders);

/// <summary>
/// Качество данных: заказы без даты приёмки (неполная синхронизация), возвраты с нулевой шапкой (учтены по строкам),
/// возвраты без строк (не учтены), типы товаров вне категорий отчёта; AcceptedInFuture — заказы, принятые в этом месяце после отчётного
/// дня (сегодня или с приёмкой в будущем): в отчёт войдут на следующий день после приёмки. У закрытого месяца их нет — 0.
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

/// <summary>
/// Исключённые из вторички филиалы («Завод» — экспорт и опт): факт отдельной строкой. VsPrevMonth — прогноз месяца к факту прошлого месяца:
/// как у карточек категорий, только у идущего месяца (у закрытого — null).
/// </summary>
public sealed record ExcludedSummary(decimal FactKg, decimal Revenue, int Orders, int Akb, decimal? ForecastKg, decimal PrevMonthKg, decimal? VsPrevMonth);

/// <summary>Ряд категории в «АКБ по месяцам»; Average — среднее за месяц по месяцам с данными (SalesMath.Average).</summary>
public sealed record AkbSeries(string Id, string Name, IReadOnlyList<int?> Values, decimal? Average = null);

/// <summary>Ряд категории другой меры «по месяцам» (кг или сум): те же месяцы и строки, что у АКБ.</summary>
public sealed record MonthSeries(string Id, string Name, IReadOnlyList<decimal?> Values, decimal? Average = null);

/// <summary>Мера «по месяцам» (кг или сум): итог, те же категории, что у АКБ, и среднее за месяц.</summary>
public sealed record MonthMetric(IReadOnlyList<decimal?> Total, IReadOnlyList<MonthSeries> Categories, decimal? Average = null);

/// <summary>
/// АКБ по месяцам года: итог и по категориям (те же, что в карточках, без скрытых — Sales:AkbChartHiddenCategories — и не больше семи
/// по объёму; остальные есть в карточках). null — данных за месяц нет. LastPartial — последний месяц ещё не закончился.
/// Average — среднее за месяц; Kg и Sum — те же строки и правила в кг и сумах (переключатель АКБ / кг / сум — на странице).
/// </summary>
public sealed record AkbByMonth(
    int Year,
    IReadOnlyList<int> Months,
    bool LastPartial,
    IReadOnlyList<int?> Total,
    IReadOnlyList<AkbSeries> Categories,
    decimal? Average = null,
    MonthMetric? Kg = null,
    MonthMetric? Sum = null);

/// <summary>Статус SKU в подразделении за месяц.</summary>
public static class SkuStatuses
{
    /// <summary>Продаётся в этом месяце: в подразделении есть ТТ с положительной строкой товара (ТТ артикула больше нуля).</summary>
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

/// <summary>
/// Артикул категории. Akb — ТТ артикула: точки с положительной чистой строкой товара (кг или выручка больше нуля, SalesMath.SkuTt);
/// дистрибуция — их доля от АКБ подразделения. WeightShare — доля артикула в весе категории. Solo — «Только он»: моно-точки
/// (положительная строка ровно по одному SKU), которые держатся на этом артикуле; SoloShare — их доля от ТТ артикула. IsTop — товар
/// из списка ТОП (Sales:TopProducts).
/// </summary>
public sealed record SkuRow(
    long ProductId,
    string Name,
    string? Code,
    decimal FactKg,
    decimal Revenue,
    int Akb,
    decimal? Distribution,
    decimal PrevMonthKg,
    string Status,
    decimal? WeightShare = null,
    int Solo = 0,
    decimal? SoloShare = null,
    bool IsTop = false);

/// <summary>
/// Карточка категории. SKU в категории (SkuTotal, M) — ассортимент: товары категории с продажами во вторичке с 1 января года месяца
/// по конец месяца (плюс проданные в прошлом месяце — для «пропало» в январе); SkuSold (N) — товары, у которых в подразделении есть
/// ТТ с положительной строкой. «Молчат» = M − N, из них «пропало» — продавались в прошлом месяце. АКБ категории — ТТ с чистым весом
/// категории больше нуля (DOC-rules §3).
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

/// <summary>«План и факт по месяцам»: PlanSource — откуда план месяца (PlanSources), null — плана нет.</summary>
public sealed record MonthPlanFact(int Month, decimal? PlanKg, decimal? FactKg, string? PlanSource = null);

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
    string? Job = null,
    TargetLevel? ExecutionLevel = null,
    TargetLevel? ForecastExecutionLevel = null);

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
    IReadOnlyList<CategoryPlanFact> CategoryPlans,
    NextMonthPlan? NextMonth,
    NotBoughtRow NotBoughtTotal,
    CategoryPlanFact? CategoryPlanTotal,
    bool IsChannel = false); // регион-канал (Sales:ChannelRegions): СВР и дилера у него нет и в «Настройках продаж» их не задать

public sealed record MedianValue(decimal? Value, decimal? RegionMedian);

/// <summary>
/// План и факт по категории. План РОП / «Завод» — сумма планов «регион × категория» по регионам подразделения, FactKg — весь факт
/// подразделения по категории. План из Linko — показатель ТП (бывает на пару категорий: «Могуль + Шоколад»), FactKg — факт ТП с этим
/// планом, ScopeFactKg — весь факт подразделения по этим категориям. Строка без плана — категория продаётся, а плана на неё нет.
/// RemainingKg — сколько осталось до плана (не меньше нуля); ForecastKg — прогноз факта, ForecastExecution — прогноз к плану (только
/// у идущего месяца); ForecastLevel — его цвет: от 100% — зелёный, от 90% — оранжевый, ниже — красный; ExecutionLevel — цвет выполнения.
/// </summary>
public sealed record CategoryPlanFact(
    long? CategoryId,
    string Name,
    decimal? PlanKg,
    decimal FactKg,
    decimal Revenue,
    decimal? Execution,
    decimal? ScopeFactKg = null,
    decimal? RemainingKg = null,
    decimal? ForecastKg = null,
    decimal? ForecastExecution = null,
    TargetLevel? ForecastLevel = null,
    TargetLevel? ExecutionLevel = null);

/// <summary>Строка плана на следующий месяц: регион (на верхних уровнях) или категория (в регионе); Change — к плану текущего месяца.</summary>
public sealed record NextMonthPlanRow(string Id, string Name, decimal PlanKg, decimal? CurrentPlanKg, decimal? Change = null);

/// <summary>
/// План на следующий месяц: планы регионов выбранного вида (Source — rop / factory), если они на него уже есть, иначе — планы ТП
/// из Linko (linko; Agents — сколько ТП с планом). Change — к плану текущего месяца того же источника.
/// </summary>
public sealed record NextMonthPlan(
    int Year,
    int Month,
    decimal PlanKg,
    decimal? CurrentPlanKg,
    int Agents,
    IReadOnlyList<NextMonthPlanRow> Rows,
    decimal? Change = null,
    string Source = PlanSources.Linko);

/// <summary>План и факт по KPI-показателю Linko (кг по группе товаров, АКБ и т.п.); ExecutionLevel — цвет выполнения.</summary>
public sealed record IndicatorPlan(long IndicatorId, string Name, string PlanType, decimal Plan, decimal Fact, decimal? Execution,
    TargetLevel? ExecutionLevel = null);

/// <summary>
/// Уровень 4: карточка агента. Orders — заказы агента, принятые в месяце (конверсия = Orders ÷ Visits);
/// VisitsWithOrder — для справки: визиты, в день которых агент ввёл заказ в этой ТТ. RevenuePerOutlet и KgPerOutlet — выручка и кг
/// на точку с покупкой (÷ Akb); ExecutionLevel и RevenueExecutionLevel — цвета выполнения планов ТП.
/// </summary>
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
    AkbByMonth? AkbMonths = null,
    int Orders = 0,
    decimal? RevenuePerOutlet = null,
    decimal? KgPerOutlet = null,
    TargetLevel? ExecutionLevel = null,
    TargetLevel? RevenueExecutionLevel = null);

/// <summary>
/// «Проблемный агент». Score — тяжесть (SalesMath.ProblemScore: 10 за критичное, 4 за риск, плюс (1 − страйк) × 3 у оцениваемых),
/// список отсортирован по ней; Rank — место в этом списке.
/// </summary>
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
    IReadOnlyList<AgentFlag> Flags,
    decimal Score = 0);

/// <summary>Факт месяца для «Проблемных агентов»: весь, без агента (в заказах Linko нет агента) и остальной — по нему рейтинг.</summary>
public sealed record ProblemsFact(decimal FactKg, decimal UnassignedKg, decimal? UnassignedShare, decimal AssignedKg);

/// <summary>
/// «Проблемные агенты»: Found — ТП с замечаниями в списке (без вакансий и «мало данных»); Directions — РМ для фильтра;
/// Fact — факт месяца республики (сколько не привязано к агентам).
/// </summary>
public sealed record ProblemsView(
    PeriodInfo Period,
    int Vacancies,
    IReadOnlyList<ProblemAgent> Agents,
    int Found = 0,
    IReadOnlyList<UnitRef>? Directions = null,
    ProblemsFact? Fact = null);

/// <summary>Планы сотрудника из Linko за месяц: итоги по типам и все показатели. Факт — по расчёту Linko. WeightExecutionLevel — цвет выполнения.</summary>
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
    IReadOnlyList<IndicatorPlan> Indicators,
    TargetLevel? WeightExecutionLevel = null);

public sealed record PlanRegionRow(string Id, string Name, int Agents, decimal? WeightPlan, decimal WeightFact, decimal? WeightExecution, decimal? RevenuePlan, decimal RevenueFact,
    TargetLevel? WeightExecutionLevel = null);

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
    IReadOnlyList<PlanPersonRow> TeamPlans,
    TargetLevel? WeightExecutionLevel = null);
