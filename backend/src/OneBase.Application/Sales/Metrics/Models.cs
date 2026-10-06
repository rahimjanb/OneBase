namespace OneBase.Application.Sales.Metrics;

// Входные данные расчёта — простые записи без EF, чтобы формулы были чистыми и тестируемыми.

/// <summary>
/// Строка продажи: одна позиция заказа (+) или возврата (−) — кг и выручка со знаком.
/// OrderId = null у возвратов: они не считаются заказами. CreatedDate — день ввода заказа (created_date): по нему заказ сшивается
/// с визитом в календаре визитов (агент стоял в магазине в день ввода, приёмка — позже); null — у возвратов и в старых данных.
/// </summary>
public sealed record SaleLine(
    DateOnly Date,
    long? AgentId,
    long? MarketId,
    long? BranchId,
    long? CategoryId,
    long? ProductId,
    decimal Kg,
    decimal Revenue,
    long? OrderId,
    DateOnly? CreatedDate = null);

public enum VisitStatus
{
    Done,
    Pending,
    Undone,
}

public sealed record VisitRecord(DateOnly Date, long AgentId, long MarketId, VisitStatus Status, bool InPlan);

public sealed record AgentInfo(long Id, string Name, bool IsActive, Guid? ProfileRegionId, bool IsVacancy, bool InDirectory, string? Job = null);

public sealed record RegionInfo(Guid Id, long BranchId, string Name, Guid? DirectionId, string? Supervisor, string? Dealer);

public sealed record DirectionInfo(Guid Id, string Name, bool IsChannel, string? ManagerName, string? Description, int SortOrder);

public sealed record MarketInfo(long Id, string Name, long? ResponsibleAgentId, long? BranchId);

public sealed record ProductInfo(long Id, string Name, string? Code, long? CategoryId);

/// <summary>Факт кг по месяцам (история для «План и факт по месяцам» и темпа агента).</summary>
public sealed record MonthlyFact(int Year, int Month, long? AgentId, long? BranchId, decimal Kg);

/// <summary>
/// АКБ давнего месяца из БД: по республике (ByBranch = false, AgentId = null), по филиалу или по агенту (AgentId);
/// итог (IsTotal) или по категории-группе. Kg и Revenue — чистые кг и выручка того же разреза за месяц (ряды «кг» и «сум» в «АКБ по месяцам»).
/// </summary>
public sealed record MonthlyAkb(int Year, int Month, bool ByBranch, long? BranchId, bool IsTotal, long? CategoryId, int Akb, long? AgentId = null,
    decimal Kg = 0, decimal Revenue = 0);

public sealed record PlanRow(Guid? RegionId, long? AgentId, int Month, long? CategoryId, decimal PlanKg);

/// <summary>
/// План ТП по весу из показателя Linko, отнесённый к категориям отчёта по названию показателя:
/// «Сентябрь Бамбук Бухоро» — одна категория, «Могуль + Шоколад …» — две (Groups — id групп категорий).
/// </summary>
public sealed record CategoryPlanRow(long AgentId, int Year, int Month, IReadOnlyList<long> Groups, decimal PlanKg);

/// <summary>KPI-показатель агента из Linko (staff_balance): план и факт так, как их считает Linko.</summary>
public sealed record StaffIndicator(long AgentId, long IndicatorId, string Name, string PlanType, decimal Plan, decimal Fact);

public sealed record SalesTargets(decimal Conversion, decimal RevenuePerOutlet, decimal AkbPerAgent, decimal CategoriesPerOutlet);

/// <summary>Всё, что нужно для расчёта одного месяца.</summary>
public sealed class MonthData
{
    public required int Year { get; init; }
    public required int Month { get; init; }

    /// <summary>Дата последних данных (для прогноза и «тех же дней»).</summary>
    public required DateOnly DataThrough { get; init; }

    /// <summary>Продажи текущего месяца (с 1-го по DataThrough).</summary>
    public required IReadOnlyList<SaleLine> Current { get; init; }

    /// <summary>Продажи всего прошлого месяца.</summary>
    public required IReadOnlyList<SaleLine> Previous { get; init; }

    /// <summary>Визиты текущего месяца (включая запланированные).</summary>
    public required IReadOnlyList<VisitRecord> Visits { get; init; }

    /// <summary>Факт по месяцам: 12 месяцев до текущего включительно.</summary>
    public required IReadOnlyList<MonthlyFact> History { get; init; }

    /// <summary>
    /// Планы на выбранный месяц: планы регионов выбранного вида (РОП или «Завод», регион × категория) и планы ТП из Linko —
    /// по ним карточка ТП, команда региона и состав месяца. Без планов регионов план подразделения — сумма планов его ТП (PlanSource).
    /// </summary>
    public required IReadOnlyList<PlanRow> Plans { get; init; }

    /// <summary>Планы регионов выбранного вида на весь год выбранного месяца (для графика по месяцам).</summary>
    public required IReadOnlyList<PlanRow> YearRegionPlans { get; init; }

    /// <summary>Планы регионов выбранного вида на следующий месяц; пусто — карточка следующего месяца по планам ТП из Linko.</summary>
    public IReadOnlyList<PlanRow> NextRegionPlans { get; init; } = [];

    /// <summary>Выбранный вид плана: PlanSources.Rop или PlanSources.Factory.</summary>
    public string SelectedPlan { get; init; } = PlanSources.Rop;

    /// <summary>Какие планы регионов заведены на месяц (rop, factory) — для переключателя плана; пусто — ни одного, план из Linko.</summary>
    public IReadOnlyList<string> AvailablePlans { get; init; } = [];

    /// <summary>
    /// Откуда план подразделений месяца (PlanSources): rop / factory — планы регионов выбранного вида; linko — их на месяц нет,
    /// план — сумма планов ТП из Linko. Планы регионов и ТП в одном месяце не смешиваются.
    /// </summary>
    public string PlanSource { get; init; } = PlanSources.Linko;

    /// <summary>Планы ТП из Linko на весь год — план региона в месяце без планов регионов = сумма планов его ТП.</summary>
    public IReadOnlyList<PlanRow> YearAgentPlans { get; init; } = [];

    /// <summary>Планы ТП по категориям (текущий и следующий месяц) — из показателей Linko.</summary>
    public IReadOnlyList<CategoryPlanRow> CategoryPlans { get; init; } = [];

    /// <summary>Следующий месяц и планы ТП на него из Linko; пусто — в Linko их ещё нет.</summary>
    public (int Year, int Month) NextMonth { get; init; }

    public IReadOnlyList<PlanRow> NextPlans { get; init; } = [];

    /// <summary>KPI-показатели агентов из Linko за выбранный месяц (включая супервайзеров).</summary>
    public IReadOnlyList<StaffIndicator> Indicators { get; init; } = [];

    /// <summary>Планы агентов по выручке (sales_sum), сум, за выбранный месяц. PlanKg здесь — сумма.</summary>
    public IReadOnlyList<PlanRow> RevenuePlans { get; init; } = [];

    /// <summary>Сотрудники, чей план — план команды (супервайзеры): не суммируется с планами агентов.</summary>
    public IReadOnlySet<long> TeamPlanStaff { get; init; } = new HashSet<long>();

    public required IReadOnlyDictionary<long, AgentInfo> Agents { get; init; }
    public required IReadOnlyList<RegionInfo> Regions { get; init; }

    /// <summary>Старые филиалы Linko → регион, в котором они считаются (см. OldBranches).</summary>
    public IReadOnlyDictionary<long, Guid> BranchAliases { get; init; } = new Dictionary<long, Guid>();

    public required IReadOnlyList<DirectionInfo> Directions { get; init; }
    public required IReadOnlyDictionary<long, MarketInfo> Markets { get; init; }
    public required IReadOnlyDictionary<long, string> Categories { get; init; }

    /// <summary>Справочник товаров (SKU).</summary>
    public IReadOnlyDictionary<long, ProductInfo> Products { get; init; } = new Dictionary<long, ProductInfo>();

    /// <summary>
    /// Ассортимент — SKU, которые продавались во вторичке с 1 января года месяца по конец месяца (по всей компании, без «Завода»
    /// и «К К Мерч»): знаменатель «продаётся N из M SKU». Признака «товар активен» в Linko нет, поэтому ассортимент определяется по продажам.
    /// </summary>
    public IReadOnlySet<long> ActiveSkus { get; init; } = new HashSet<long>();

    /// <summary>ТОП-товары (Sales:TopProducts) — метка «ТОП» в ассортименте.</summary>
    public TopProductSet Top { get; init; } = TopProductSet.Empty;

    /// <summary>
    /// АКБ по месяцам года до прошлого месяца (не включая его): текущий и прошлый месяц считаются из строк продаж.
    /// Категории уже объединены в категории отчёта (см. SalesCategories).
    /// </summary>
    public IReadOnlyList<MonthlyAkb> AkbHistory { get; init; } = [];

    /// <summary>
    /// Заказы для сшивки с визитами (календарь визитов, «визиты с заказом» для справки): созданные в месяце (дата строки — created_date),
    /// а не принятые в нём. Конверсия считается по заказам факта (Current). null — сшивать по строкам факта (так в тестах и в старых данных).
    /// </summary>
    public IReadOnlyList<SaleLine>? VisitOrders { get; init; }

    public IReadOnlyList<SaleLine> VisitLines => VisitOrders ?? Current;

    /// <summary>Продажи исключённых филиалов («Завод» — экспорт и опт) за месяц и за прошлый месяц: не вторичка, отдельный блок.</summary>
    public IReadOnlyList<SaleLine> ExcludedCurrent { get; init; } = [];

    public IReadOnlyList<SaleLine> ExcludedPrevious { get; init; } = [];

    /// <summary>Категории отчёта поверх типов Linko. None — каждый тип сам по себе (так в тестах без настройки).</summary>
    public SalesCategories? CategoryMap { get; init; }

    public SalesDataQuality Quality { get; init; } = SalesDataQuality.Empty;

    /// <summary>Выручка заказов месяца в других валютах (вторичка / исключённые филиалы) — не складывается с основной.</summary>
    public IReadOnlyList<CurrencyTotal> OtherCurrency { get; init; } = [];

    public IReadOnlyList<CurrencyTotal> ExcludedOtherCurrency { get; init; } = [];

    /// <summary>ТТ, закреплённые за агентами через market_users (агент → ТТ).</summary>
    public required IReadOnlyList<(long AgentId, long MarketId)> MarketAssignments { get; init; }

    public required SalesTargets Targets { get; init; }
    public required FlagThresholds Thresholds { get; init; }

    /// <summary>Должности Linko, которые считаются ТП (Sales:SalesRepJobs). Пусто — ТП все, у кого есть продажи или визиты.</summary>
    public IReadOnlyList<string> SalesRepJobs { get; init; } = [];

    /// <summary>Категории отчёта, которых нет в «АКБ по месяцам» (Sales:AkbChartHiddenCategories; «Песочный»). Без учёта регистра.</summary>
    public IReadOnlyList<string> AkbChartHiddenCategories { get; init; } = [];

    public DateOnly MonthStart => new(Year, Month, 1);
    public int DaysInMonth => DateTime.DaysInMonth(Year, Month);
    public int WorkedDays => SalesMath.WorkedDays(MonthStart, DataThrough);

    /// <summary>Месяц закрыт: данные есть за все его дни — прогноза на конец месяца нет.</summary>
    public bool Closed => WorkedDays >= DaysInMonth;
}
