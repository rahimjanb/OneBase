using OneBase.Application.Sales.Metrics;

namespace OneBase.Application.Telegram.Weekly;

// Еженедельный отчёт (PDF РОП / РМ / СВ и карточки ТП): единая модель данных. Все цифры — из расчётов OneBase («Продажи», KPI, «Акции»,
// «Остатки», «Аутсток», DSO); null — «нет данных» (в PDF «—»), а не ноль. У каждого блока — источник и время данных.

/// <summary>Состояние источника: ok — данные есть и свежие; stale — давно не обновлялись; missing — данных нет (в отчёте «нет данных»).</summary>
public static class WeeklySourceStates
{
    public const string Ok = "ok";
    public const string Stale = "stale";
    public const string Missing = "missing";
}

/// <summary>Источники блоков отчёта.</summary>
public static class WeeklySources
{
    public const string Orders = "linko-orders";
    public const string Visits = "linko-visits";
    public const string Plans = "linko-plans";
    public const string Kpi = "linko-kpi";
    public const string Promo = "promo";
    public const string Stock = "linko-stock";
    public const string Dso = "dso";
    public const string Dmp = "dmp";
}

public sealed record WeeklySourceStatus(string Key, string State, DateTimeOffset? AsOf, string? Note = null);

/// <summary>Замечание о данных или рекомендация: код и аргументы — текст и перевод собирает PDF (ReportTexts).</summary>
public sealed record WeeklyNote(string Code, IReadOnlyDictionary<string, string> Args)
{
    public static WeeklyNote Of(string code, params (string Key, object? Value)[] args) =>
        new(code, args.ToDictionary(a => a.Key, a => a.Value switch { null => "", decimal d => d.ToString(System.Globalization.CultureInfo.InvariantCulture), _ => a.Value.ToString() ?? "" }));
}

/// <summary>Рабочие дни (пн–сб без праздников справочника): месяц, прошло по отчётный день включительно, осталось после него; неделя.</summary>
public sealed record WeeklyCalendar(
    int MonthWorkDays,
    int MonthWorkDaysDone,
    int MonthWorkDaysLeft,
    int WeekWorkDays,
    int WeekWorkDaysDone,
    decimal? MonthTempo);

/// <summary>
/// Еженедельный отчёт области. Role — rop (вся страна), rm (направление), sv (регион), tp (один ТП). Неделя — пн–сб (PeriodStart–PeriodEnd),
/// данные по DataThrough (отчётный день «Продаж»). Regions — строки регионов (у РОП и РМ), у СВ — одна строка его региона. RegionName —
/// регион области из одного региона (СВ, ТП): шапка карточек ТП «Сырдарья · СВР …».
/// </summary>
public sealed record WeeklySupervisorReport(
    string ReportId,
    string Role,
    string ScopeKey,
    string ScopeName,
    string? SupervisorName,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateOnly DataThrough,
    DateTimeOffset GeneratedAt,
    DateTimeOffset? DataAsOf,
    WeeklyCalendar Calendar,
    IReadOnlyList<WeeklySourceStatus> SourceStatuses,
    WeeklySalesMetrics SalesMetrics,
    IReadOnlyList<WeeklyRegionMetrics> Regions,
    IReadOnlyList<WeeklyEmployeeMetrics> EmployeeMetrics,
    WeeklyClientMetrics ClientMetrics,
    WeeklyProductMetrics ProductMetrics,
    WeeklyPromotionMetrics PromotionMetrics,
    WeeklyDmpMetrics DmpMetrics,
    IReadOnlyList<WeeklyDealerMetrics> DealerMetrics,
    WeeklyKpiMetrics KpiMetrics,
    WeeklyGoals WeeklyGoals,
    IReadOnlyList<WeeklyNote> Recommendations,
    IReadOnlyList<WeeklyNote> DataWarnings,
    string? RegionName = null)
{
    /// <summary>Каких обязательных данных не хватает (ключи WeeklySources); пусто — данные полные.</summary>
    public IReadOnlyList<string> Incomplete => WeeklyReportMath.Incomplete(this);
}

/// <summary>День недели: факт (null — день впереди или данных ещё нет), рабочий ли день.</summary>
public sealed record WeeklyDay(DateOnly Date, decimal? Kg, decimal? Revenue, int? Orders, bool WorkDay);

/// <summary>Нужный темп до конца месяца: осталось до цели и сколько в день (null — цель уже взята или рабочих дней не осталось).</summary>
public sealed record WeeklyPace(decimal Target, decimal? Remaining, decimal? PerDay);

/// <summary>
/// Продажи области. Неделя — с понедельника по отчётный день, PrevWeek* — те же дни прошлой недели. Месяц — как в «Продажах»; прогноз —
/// по рабочим дням (факт ÷ прошло × всего); PrevMonthSameDaysKg — прошлый месяц за то же число рабочих дней. AkbPlan — сумма планов АКБ ТП
/// из Linko (active_client_count). Months — план и факт по месяцам года.
/// </summary>
public sealed record WeeklySalesMetrics(
    decimal WeekKg,
    decimal WeekRevenue,
    int WeekOrders,
    int WeekAkb,
    decimal? PrevWeekKg,
    decimal? WeekVsPrev,
    IReadOnlyList<WeeklyDay> Days,
    decimal? DailyNormKg,
    decimal MonthKg,
    decimal MonthRevenue,
    decimal? PlanKg,
    decimal? Execution,
    decimal? ForecastKg,
    decimal? ForecastExecution,
    string PlanSource,
    int MonthAkb,
    decimal? AkbPlan,
    decimal? AkbExecution,
    decimal? AvgCheck,
    decimal? PrevMonthSameDaysKg,
    decimal? VsPrevMonth,
    decimal? CurrentPerDayKg,
    WeeklyPace? To80,
    WeeklyPace? To100,
    IReadOnlyList<MonthPlanFact> Months);

/// <summary>Строка региона у РОП и РМ: СВР, продажи недели и месяца, план, прогноз, АКБ.</summary>
public sealed record WeeklyRegionMetrics(
    Guid RegionId,
    string Name,
    string? DirectionName,
    string? SupervisorName,
    string? DealerName,
    int Agents,
    WeeklySalesMetrics Sales);

/// <summary>
/// ТП: неделя (кг, сумма, к прошлой неделе, заказы, АКБ недели, выполненные визиты, конверсия = заказы ÷ визиты, маршрут = по маршруту ÷
/// план), месяц (кг по правилам «Продаж», план, АКБ и план АКБ), премия КПУ сейчас (null — нет KPI в Linko или нет права на деньги).
/// Execution и ForecastExecution — «% плана» из Linko (решение 31.10.2026): показатель «Тотал» KPI (в кг, без него — в сумах), прогноз —
/// по рабочим дням; LinkoPlan, LinkoFact, LinkoUnit — его план и факт («4 450 из 4 700 кг»). Нет KPI в Linko — null («нет данных»).
/// </summary>
public sealed record WeeklyEmployeeMetrics(
    long AgentId,
    string Name,
    decimal WeekKg,
    decimal WeekRevenue,
    decimal? PrevWeekKg,
    decimal? WeekVsPrev,
    int WeekOrders,
    int WeekAkb,
    int WeekVisits,
    decimal? WeekConversion,
    int WeekRoutePlan,
    int WeekRouteDone,
    decimal? WeekRouteShare,
    decimal MonthKg,
    decimal? PlanKg,
    decimal? Execution,
    decimal? ForecastExecution,
    int MonthAkb,
    decimal? AkbPlan,
    decimal? PremiumNow,
    decimal? PremiumMax,
    decimal? LinkoPlan = null,
    decimal? LinkoFact = null,
    string? LinkoUnit = null);

/// <summary>Спящая ТТ: последняя покупка, дней без покупки, сколько брала (кг за месяц последней покупки), ТП — кто продавал больше всех.</summary>
public sealed record WeeklySleepingMarket(long MarketId, string Name, long? AgentId, string? AgentName, DateOnly LastPurchase, int Days, decimal Kg, string Bucket);

public static class WeeklySleepBuckets
{
    /// <summary>Брали в этом месяце, последняя покупка 15–29 дней назад.</summary>
    public const string Days15To30 = "15-30";

    /// <summary>Брали в прошлом месяце, в этом — нет.</summary>
    public const string Days30Plus = "30+";
}

/// <summary>Клиенты ТП: база прошлого месяца, купили в этом, удержание, спят 15–30 и 30+, новые.</summary>
public sealed record WeeklyClientRow(long AgentId, string Name, int Base, int Bought, decimal? Retention, int Sleeping15To30, int Sleeping30Plus, int New);

public sealed record WeeklyClientMetrics(
    int Base,
    int Bought,
    int Sleeping15To30,
    decimal Sleeping15To30Kg,
    int Sleeping30Plus,
    decimal Sleeping30PlusRevenue,
    int New,
    decimal NewRevenue,
    IReadOnlyList<WeeklyClientRow> Agents,
    IReadOnlyList<WeeklySleepingMarket> Sleeping);

/// <summary>Категория: план и факт месяца (кг), выполнение.</summary>
public sealed record WeeklyCategoryRow(long? CategoryId, string Name, decimal? PlanKg, decimal FactKg, decimal? Execution);

/// <summary>ТП × категория: кг за месяц и сколько ТТ купили (чистый вес категории больше нуля).</summary>
public sealed record WeeklyAgentCategory(string Name, decimal Kg, int Outlets);

/// <summary>Приоритетная линия ТП из KPI Linko (Могуль, Twizos): план и факт (кг), выполнение.</summary>
public sealed record WeeklyPriorityLine(string Label, decimal Plan, decimal Fact, decimal? Execution);

public sealed record WeeklyAgentProducts(long AgentId, string Name, IReadOnlyList<WeeklyAgentCategory> Categories, IReadOnlyList<WeeklyPriorityLine> Priority);

public sealed record WeeklyProductMetrics(IReadOnlyList<WeeklyCategoryRow> Categories, IReadOnlyList<WeeklyAgentProducts> Agents);

/// <summary>Акции ТП: уникальные ТТ с заказом по акции за прошлую полную неделю, за текущую неделю, за месяц; продажи и затраты месяца.</summary>
public sealed record WeeklyPromoAgent(long? AgentId, string Name, int OutletsPrevWeek, int OutletsWeek, int OutletsMonth, decimal Sale, decimal Cost, decimal? CostShare);

/// <summary>Акция за месяц: ТТ, продажи по акции, затраты.</summary>
public sealed record WeeklyPromoKind(long PromoId, string Name, int Outlets, decimal Sale, decimal Cost);

public sealed record WeeklyPromotionMetrics(
    DateOnly PrevWeekFrom,
    DateOnly PrevWeekTo,
    int OutletsPrevWeek,
    int OutletsWeek,
    int OutletsMonth,
    decimal Sale,
    decimal Cost,
    decimal? CostShare,
    IReadOnlyList<WeeklyPromoAgent> Agents,
    IReadOnlyList<WeeklyPromoKind> Kinds);

/// <summary>ДМП (стеллажи): данных в OneBase пока нет — Available = false, отчёт пишет «нет данных».</summary>
public sealed record WeeklyDmpMetrics(bool Available);

/// <summary>SKU дилера: остаток, продажи в день, хватит дней, рекомендованная отгрузка до запаса (кг).</summary>
public sealed record WeeklyDealerSku(long ProductId, string Name, string? Code, string Category, decimal? StockKg, decimal? KgPerDay, decimal? DaysOfCover, decimal OrderKg, bool Top);

/// <summary>Упущено из-за аутстока (оценка): товар, дней без товара, кг, сумма.</summary>
public sealed record WeeklyLostSku(string Name, int ZeroDays, decimal Kg, decimal Sum);

/// <summary>Неоплаченная отгрузка дилера: дата, остаток долга, срок оплаты.</summary>
public sealed record WeeklyDealerOpen(DateOnly Date, decimal Remaining, DateOnly? Due);

/// <summary>
/// Дилер региона: дефицит (запас меньше 7 дней), отгрузка до запаса (OrderKg по товарам с продажами), упущено из-за аутстока (оценка по
/// закрытому месяцу), деньги (DSO) — null, если дилер не найден в DSO по названию.
/// </summary>
public sealed record WeeklyDealerMetrics(
    Guid RegionId,
    string Region,
    string? Dealer,
    DateOnly? StockDate,
    int ZeroSku,
    decimal ShipKg,
    IReadOnlyList<WeeklyDealerSku> Deficit,
    IReadOnlyList<WeeklyDealerSku> Ship,
    int? LostMonth,
    decimal? LostKg,
    decimal? LostSum,
    IReadOnlyList<WeeklyLostSku> Lost,
    WeeklyDealerMoney? Money);

public sealed record WeeklyDealerMoney(string Name, decimal Debt, decimal Overdue, decimal? Dso, int? Norm, string Status, decimal PaidMonth, decimal ShippedMonth, DateOnly? LastPay,
    IReadOnlyList<WeeklyDealerOpen> Open);

/// <summary>
/// Показатель КПУ: план, факт, % и порог (Linko), сколько добрать до порога и в день (по оставшимся рабочим дням), статус и премия
/// (порог пройден — начисленная Linko; ниже — сколько даст выход на порог). Money — null без права finance.read.
/// </summary>
public sealed record WeeklyKpiCell(string Label, string Unit, decimal Plan, decimal Fact, decimal Pct, decimal? Threshold, decimal? Need, decimal? NeedPerDay, string Status,
    decimal? Premium);

public static class WeeklyKpiStatuses
{
    public const string Passed = "passed";
    public const string Realistic = "realistic";
    public const string Far = "far";
    public const string NoThreshold = "none";
}

public sealed record WeeklyKpiPerson(long Id, string Name, string Role, decimal? PremiumNow, decimal? PremiumMax, IReadOnlyList<WeeklyKpiCell> Cells);

public sealed record WeeklyKpiMetrics(bool Available, bool Money, DateTimeOffset? LoadedAt, IReadOnlyList<WeeklyKpiPerson> People);

/// <summary>
/// Цели ТП на следующую неделю (формулы — WeeklyReportOptions): продажи = план месяца ÷ рабочие дни месяца × рабочие дни недели; АКБ = ТТ
/// прошлой полной недели × коэффициент; ТТ по акциям = ТТ прошлой полной недели × коэффициент (не меньше минимума); вернуть спящих —
/// доля от 15–30 и от 30+; списки ТТ — крупные первыми.
/// </summary>
public sealed record WeeklyAgentGoal(
    long AgentId,
    string Name,
    decimal? SalesKg,
    int AkbWas,
    int Akb,
    int PromoWas,
    int Promo,
    int Return15To30,
    int Sleeping15To30,
    int Return30Plus,
    int Sleeping30Plus,
    IReadOnlyList<WeeklySleepingMarket> ReturnList15To30,
    IReadOnlyList<WeeklySleepingMarket> ReturnList30Plus);

public sealed record WeeklyGoals(
    DateOnly WeekFrom,
    DateOnly WeekTo,
    int WeekWorkDays,
    bool PlanOfNextMonth,
    decimal? SalesKg,
    int Akb,
    int Promo,
    int Return15To30,
    int Return30Plus,
    bool DmpAvailable,
    IReadOnlyList<WeeklyAgentGoal> Agents);
