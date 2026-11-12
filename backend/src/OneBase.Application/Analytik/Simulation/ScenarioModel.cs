using System.Text.Json;
using System.Text.Json.Serialization;

namespace OneBase.Application.Analytik.Simulation;

/// <summary>Вид сценария — набор панелей параметров на экране; считает один и тот же движок, параметры — общие узлы.</summary>
public static class ScenarioKinds
{
    public const string Sales = "sales";
    public const string Production = "production";
    public const string Materials = "materials";
    public const string Procurement = "procurement";
    public const string Finance = "finance";
    public const string Workforce = "workforce";
    public const string Expansion = "expansion";
    public const string Combined = "combined";

    public static readonly string[] All = [Sales, Production, Materials, Procurement, Finance, Workforce, Expansion, Combined];
}

/// <summary>Набор сценариев: базовый (без изменений), оптимистичный и пессимистичный (по разбросу прогноза продаж), свой.</summary>
public static class ScenarioPresets
{
    public const string Base = "base";
    public const string Optimistic = "optimistic";
    public const string Pessimistic = "pessimistic";
    public const string Custom = "custom";

    public static readonly string[] All = [Base, Optimistic, Pessimistic, Custom];
}

/// <summary>Изменение или исходное значение сценария: код из каталога, значение, цель (регион, товар, позиция сырья, «позиция/поставщик», подразделение).</summary>
public sealed record ScenarioParameter(string Code, decimal Value, string? Target = null);

/// <summary>Сценарий: вид, горизонт в днях от даты данных, параметры, название нового региона (для «Новый регион»).</summary>
public sealed record ScenarioDefinition(string Kind, int HorizonDays, IReadOnlyList<ScenarioParameter> Parameters, string? NewRegionName = null);

// ---------------- снимок базы ----------------

/// <summary>Источник данных снимка: код, название, статус свежести (DataReadiness), время данных, пояснение.</summary>
public sealed record BaselineSource(string Code, string Title, string Status, DateTimeOffset? UpdatedAt, string? Note);

/// <summary>Регион продаж в базе: кг и выручка за базовый период, план месяца (кг), торговых представителей с продажами.</summary>
public sealed record BaselineRegion(string Id, string Name, decimal Kg, decimal Revenue, decimal? PlanKgMonth, int Agents);

/// <summary>Норма сырья на 1 кг продукта по действующей техкарте (с выходом этапов, браком и потерями) — в единице склада позиции.</summary>
public sealed record BaselineNorm(Guid ItemId, decimal PerKg);

/// <summary>
/// Товар в базе: кг, выручка нетто, доля скидки (скидка ÷ цена до скидки); связанная действующая техкарта и нормы сырья на 1 кг.
/// RecipeProblem — почему нормы не посчитаны (нет техкарты, единица выхода без массы).
/// </summary>
public sealed record BaselineProduct(long Id, string Name, decimal Kg, decimal Revenue, decimal DiscountShare, Guid? RecipeId, string? RecipeCode,
    IReadOnlyList<BaselineNorm> Norms, string? RecipeProblem);

/// <summary>Продажи товара в регионе за базовый период.</summary>
public sealed record BaselineCell(long ProductId, string RegionId, decimal Kg, decimal Revenue);

/// <summary>
/// Продажи базы (вторичка Linko по правилам «Продажи по SKU»), только область пользователя. Detailed = false — агент: только итог (один товар
/// «Все продажи», один регион). План месяца — сумма планов регионов области (кг), как на странице «Продажи».
/// </summary>
public sealed record BaselineSales(decimal Kg, decimal Revenue, decimal DiscountShare, DateOnly? DataThrough, string? Scope, bool Detailed,
    decimal? PlanKgMonth, string? PlanSource, IReadOnlyList<BaselineRegion> Regions, IReadOnlyList<BaselineProduct> Products, IReadOnlyList<BaselineCell> Cells);

/// <summary>
/// Производство базы по утверждённым сменам (до 90 дней): выпуск годного в среднем за календарный день, оценка мощности (типичная
/// лучшая смена — 90-й процентиль — × смен в рабочий день × доля рабочих дней), брак, сколько смен. CapacityKgDay = null — смен нет.
/// </summary>
public sealed record BaselineProduction(decimal? CapacityKgDay, decimal? OutputKgDay, decimal? OutputPerShiftKg, decimal? ShiftsPerDay, decimal? WorkdayShare,
    decimal DefectRate, int Shifts, int HistoryDays, string CapacitySource, IReadOnlyList<string> Notes);

/// <summary>Подтверждённая поставка позиции: количество в единице склада, ожидаемая дата (null — без даты), откуда.</summary>
public sealed record BaselineIncoming(decimal Quantity, DateOnly? Expected, string Source);

/// <summary>Поставщик позиции по истории приходов: ключ, название, последняя цена, срок поставки (карточка или факт заказов), дата последнего прихода.</summary>
public sealed record BaselineSupplier(string Key, string Name, decimal? Price, int? LeadTimeDays, DateOnly? LastDate);

/// <summary>
/// Позиция сырья: остатки (как в плане закупок Аналитика), средний расход и его разброс за историю, невыданная потребность заданий, поставки,
/// срок поставки, цена, поставщик, кратность закупки, средние технологические потери по строкам техкарт, масса единицы, другие поставщики.
/// </summary>
public sealed record BaselineMaterial(
    Guid ItemId, string Sku, string Name, string Unit,
    decimal OnHand, decimal Reserved, decimal Unusable, decimal Available, decimal MinQty,
    decimal HistoryRate, decimal HistorySigma, int HistoryDays, decimal Committed,
    IReadOnlyList<BaselineIncoming> Incoming,
    int LeadTimeDays, bool LeadTimeAssumed, string LeadTimeSource,
    decimal? Price, string? PriceSource, string? Supplier,
    decimal? PurchaseFactor, string? PurchaseUnit, decimal LossPercent, decimal? KgPerUnit,
    IReadOnlyList<BaselineSupplier> Suppliers);

/// <summary>
/// Финансы базы: принятые оплаты точек за период, доля оплат к выручке, средний срок оплаты (дней от реализации, взвешенно по сумме, по оплатам
/// с заказом), расходы на закупки за период, загруженных месяцев P&amp;L.
/// </summary>
public sealed record BaselineFinance(decimal? Payments, decimal? CollectionRate, decimal? PaymentLagDays, decimal? ProcurementSpend, int PnlMonths,
    IReadOnlyList<string> Notes);

public sealed record BaselineDivision(string Id, string Name, int Employees);

/// <summary>Персонал базы (Verifix): работающие сотрудники в области HR пользователя, по подразделениям.</summary>
public sealed record BaselineWorkforce(int Employees, bool SeesAll, IReadOnlyList<BaselineDivision> Divisions);

/// <summary>
/// Снимок исходных данных сценария — всё, из чего считает движок; сохраняется вместе со сценарием, поэтому сохранённый сценарий
/// воспроизводим и не меняется от обновления данных OneBase. Блок null — у автора нет права на раздел (или данных нет).
/// </summary>
public sealed record ScenarioBaselineData(
    string FormulaVersion, string Tenant, DateOnly From, DateOnly To, DateOnly AsOf, DateTimeOffset CreatedAt,
    IReadOnlyList<string> Sections, string? SalesScope,
    BaselineSales? Sales, BaselineProduction? Production, IReadOnlyList<BaselineMaterial>? Materials, BaselineFinance? Finance,
    BaselineWorkforce? Workforce, IReadOnlyList<BaselineSource> Sources, IReadOnlyList<string> Notes)
{
    [JsonIgnore]
    public int Days => To.DayNumber - From.DayNumber + 1;
}

// ---------------- результат ----------------

/// <summary>Замечание к параметрам: error — расчёт не выполняется, warning — параметр не учтён или учтён с оговоркой.</summary>
public sealed record ScenarioIssue(string Severity, string Code, string Text, string? Parameter = null, string? Target = null)
{
    public const string Error = "error";
    public const string Warning = "warning";
}

/// <summary>
/// Показатель: база, сценарий, разница (абсолютная и в %). Kind: fact — база прочитана из данных, calculated — посчитана движком,
/// input — зависит от введённых пользователем исходных данных. Base/Scenario = null — посчитать нельзя, причина в Note. Node — узел графа.
/// </summary>
public sealed record SimMetric(string Code, string Group, string Title, string Unit, string Node, decimal? Base, decimal? Scenario, decimal? Delta,
    decimal? DeltaPercent, string Kind, string? Note = null);

/// <summary>Товар: запрос, выпуск, продано, выручка, себестоимость кг, доля обеспечения (1 — полностью), что ограничило.</summary>
public sealed record SimProductRow(long Id, string Name, bool HasRecipe, decimal BaseKg, decimal RequestedKg, decimal? ProducedKg, decimal SoldKg,
    decimal BaseRevenue, decimal Revenue, decimal? BaseCostPerKg, decimal? CostPerKg, decimal? GrossProfit, decimal Coverage, string? Limit);

public sealed record SimRegionRow(string Id, string Name, bool New, decimal BaseKg, decimal Kg, decimal BaseRevenue, decimal Revenue, decimal? PlanKg,
    decimal? Execution);

/// <summary>
/// Позиция сырья: расход в день (база и сценарий), доступно, поставки в горизонте, закупка (с округлением), её стоимость, цены, дата
/// дефицита, дни без сырья, «заказать до», срок поставки, остаток на конец, излишек, доля обеспечения, риск, поставщик.
/// </summary>
public sealed record SimMaterialRow(Guid ItemId, string Name, string Unit, decimal BaseDaily, decimal Daily, decimal Available, decimal Incoming,
    decimal Purchase, decimal? PurchaseCost, decimal? BasePrice, decimal? Price, DateOnly? ShortageDate, decimal LostDays, DateOnly? OrderBy,
    bool OrderLate, int LeadTimeDays, decimal EndStock, decimal? EndValue, bool Surplus, decimal Coverage, string Risk, string? Supplier);

/// <summary>Ряд по дням горизонта (база и сценарий) — для графиков: деньги, запас сырья.</summary>
public sealed record SimSeries(string Code, string Title, string Unit, IReadOnlyList<decimal?> Base, IReadOnlyList<decimal?> Scenario);

/// <summary>Ограничение, найденное в результате: код, важность (critical, warning, info), заголовок, пояснение, к чему относится.</summary>
public sealed record SimConstraint(string Code, string Severity, string Title, string Text, string? Target = null);

public sealed record SimRisk(string Code, string Level, string Title, string Text);

/// <summary>Новый регион: продажи и выручка за горизонт, расходы, прибыль в месяц, точка безубыточности, окупаемость, нужный персонал.</summary>
public sealed record SimExpansion(string Name, string? Reference, decimal Kg, decimal Revenue, decimal? Cost, decimal? FixedMonth, decimal? ProfitMonth,
    decimal? BreakEvenKgMonth, decimal? PaybackMonths, int? StaffNeed, int Staff, IReadOnlyList<string> Notes);

/// <summary>Заключение, собранное кодом (без модели): главный вывод, изменения, ограничения, риски, рекомендации, источники, допущения.</summary>
public sealed record SimConclusion(string Headline, IReadOnlyList<string> Changes, IReadOnlyList<string> Constraints, IReadOnlyList<string> Risks,
    IReadOnlyList<string> Recommendations, IReadOnlyList<string> Sources, IReadOnlyList<string> Assumptions);

/// <summary>Использованное исходное значение: код, название, значение, откуда (OneBase или «введено пользователем»).</summary>
public sealed record SimInput(string Code, string Title, decimal? Value, string Unit, string Origin);

/// <summary>
/// Результат расчёта сценария. Valid = false — параметры с ошибками (Issues), показатели не считались. Chain — узлы, которые меняются от
/// заданных параметров, в порядке расчёта.
/// </summary>
public sealed record SimulationResult(
    string FormulaVersion, string Kind, bool Valid, int HorizonDays, DateOnly From, DateOnly To,
    IReadOnlyList<SimMetric> Metrics, IReadOnlyList<SimProductRow> Products, IReadOnlyList<SimRegionRow> Regions, IReadOnlyList<SimMaterialRow> Materials,
    IReadOnlyList<SimSeries> Series, IReadOnlyList<SimConstraint> Constraints, IReadOnlyList<SimRisk> Risks, IReadOnlyList<ScenarioIssue> Issues,
    IReadOnlyList<string> Chain, SimConclusion Conclusion, IReadOnlyList<SimInput> Inputs, SimExpansion? Expansion)
{
    public SimMetric? Metric(string code) => Metrics.FirstOrDefault(m => m.Code == code);
}

/// <summary>Сериализация снимков и результатов: как ответы API (camelCase), даты — ISO.</summary>
public static class SimulationJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, Options) ?? throw new InvalidOperationException("Пустой JSON сценария.");
}
