namespace OneBase.Application.Twin;

/// <summary>
/// Состояние блока снимка: ok — данные есть; noaccess — данные в OneBase есть, но у пользователя нет права; nodata — в OneBase таких данных нет
/// (или источник не настроен); error — источник не ответил, остальные блоки при этом работают.
/// </summary>
public static class TwinStates
{
    public const string Ok = "ok";
    public const string NoAccess = "noaccess";
    public const string NoData = "nodata";
    public const string Error = "error";
}

/// <summary>Что привязано к зданию: отдел OneBase, склад снабжения или склад Linko (готовая продукция).</summary>
public static class TwinLinkKinds
{
    public const string Department = "department";
    public const string Warehouse = "warehouse";
    public const string Plant = "plant";
}

/// <summary>Блок снимка: состояние и данные (только при ok).</summary>
public sealed record TwinPart<T>(string State, T? Data) where T : class
{
    public static TwinPart<T> Of(T data) => new(TwinStates.Ok, data);
    public static TwinPart<T> NoAccess { get; } = new(TwinStates.NoAccess, null);
    public static TwinPart<T> NoData { get; } = new(TwinStates.NoData, null);
    public static TwinPart<T> Failed { get; } = new(TwinStates.Error, null);
}

/// <summary>Здание модели территории (factory-3d): постоянный ключ и название из модели.</summary>
public sealed record TwinBuilding(string Key, string Name);

/// <summary>
/// Здания модели территории завода (factory-3d.html: 10 корпусов по спутнику). Ключи постоянные — по ним хранятся привязки; геометрия — во
/// фронтенде (frontend/src/lib/twin/site.ts) с теми же ключами.
/// </summary>
public static class TwinSite
{
    public static readonly IReadOnlyList<TwinBuilding> Buildings =
    [
        new("A", "Цех 1 (северо-запад)"),
        new("B", "Северный корпус"),
        new("C", "Корпус с двускатной крышей"),
        new("D", "Восточный корпус"),
        new("E", "Центральный корпус"),
        new("F", "Центральная площадка"),
        new("G", "Западный корпус"),
        new("H", "Восточный длинный корпус"),
        new("I", "Юго-западный корпус"),
        new("J", "Офис (Южный корпус)"),
    ];

    public static TwinBuilding? Find(string? key) => Buildings.FirstOrDefault(b => string.Equals(b.Key, key, StringComparison.Ordinal));
}

public sealed record TwinLinkView(string Kind, string Id, string? Code, string Name);

/// <summary>Здание в снимке: название (своё или из модели) и что в нём находится.</summary>
public sealed record TwinObjectView(string Key, string Name, string DefaultName, IReadOnlyList<TwinLinkView> Links, DateTimeOffset? UpdatedAt);

// ---------- Показатели предприятия ----------

/// <summary>
/// Продажи (вторичка) месяца — как «Обзор» раздела «Продажи». Level — цвет: в идущем месяце по прогнозу выполнения, в закрытом — по
/// выполнению (ExecutionLevelOf: от 90% — Good, от 60% — Warning, ниже — Bad); LevelBasis — по чему он взят (forecast | execution).
/// </summary>
public sealed record TwinSales(
    int Year,
    int Month,
    bool Closed,
    DateOnly DataThrough,
    decimal FactKg,
    decimal? PlanKg,
    decimal? Execution,
    decimal? ForecastExecution,
    string? Level,
    string LevelBasis,
    decimal Revenue,
    int Akb,
    int Critical,
    int Risk,
    int Vacancies);

/// <summary>Первичка — отгрузки завода дилерам и прямым клиентам за месяц (не выпуск продукции).</summary>
public sealed record TwinPrimary(int Year, int Month, decimal Kg, decimal Sum, decimal? PlanKg, decimal? Execution, decimal? ForecastExecution, bool HasPlan,
    DateOnly? AsOf);

/// <summary>Явка сегодня по сотрудникам, которых пользователь видит в HR (все или подразделения своего отдела).</summary>
public sealed record TwinHr(bool SeesAll, bool IsToday, int Active, int Scheduled, int Present, int Late, int Absent, int OnLeave, decimal? PresentPct,
    DateTimeOffset? SyncedAt);

public sealed record TwinTasks(int Open, int Overdue, int Review, int Blocked, int DueThisWeek, int DoneThisMonth);

/// <summary>
/// Заявки на закупку, видимые пользователю (как в «Заявках»): открытые, на согласовании, срочные, на доработке, со «Сырьём». OverdueToBuy и
/// SpendMonth (закупки месяца с НДС) — только снабжению и директору, иначе null.
/// </summary>
public sealed record TwinSupply(bool ReadsAll, int Open, int Approval, int Urgent, int Revision, int Raw, int? OverdueToBuy, decimal? SpendMonth,
    DateOnly SpendFrom, DateOnly SpendTo);

/// <summary>Склад снабжения: позиций, ниже минимума, нет в наличии, с резервом, заявок к выдаче; RawLow — «Сырьё» ниже минимума.</summary>
public sealed record TwinStock(Guid? WarehouseId, string? WarehouseName, int Items, int Low, int Empty, int WithReserve, int ToIssue, int? RawLow);

/// <summary>Склад готовой продукции завода в Linko: остаток, дни покрытия, SKU с дефицитом, затоваркой и без продаж.</summary>
public sealed record TwinPlant(long StockId, string Name, decimal? Kg, decimal? DaysOfCover, int Skus, int Deficit, int Overstock, int Dead,
    DateOnly SnapshotDate, DateTimeOffset? SyncedAt);

/// <summary>Дебиторка дилеров (DSO): долг, просрочено (ПДЗ) и его доля, оплачено и отгружено за месяц. Без реестра оплат — nodata (как раздел DSO).</summary>
public sealed record TwinFinance(DateOnly AsOf, decimal Debt, decimal Overdue, decimal OverdueShare, int OverdueDealers, decimal PaidMonth,
    decimal ShippedMonth);

/// <summary>Акции — скидки и бонусы вторички за месяц (Linko); единственные данные, близкие к маркетингу.</summary>
public sealed record TwinPromo(string Month, decimal Total, int Promos, decimal? ToSales);

public sealed record TwinAlert(Guid Id, string Category, string Severity, string Title, string? Href);

public sealed record TwinAlerts(IReadOnlyList<TwinAlert> Items);

// ---------- По отделам ----------

public sealed record TwinDeptTasks(int Open, int Overdue, int Review, int Blocked);

/// <summary>HR отдела — по подразделениям Verifix, привязанным к отделу (с дочерними). Нет привязанных подразделений — nodata.</summary>
public sealed record TwinDeptHr(int Divisions, int Employees, int Scheduled, int Present, int Late, int Absent, decimal? PresentPct);

/// <summary>Открытые заявки отдела (видимые пользователю); Overdue — просрочена желаемая дата закупки (снабжению и директору, иначе null).</summary>
public sealed record TwinDeptSupply(int Open, int Approval, int Urgent, int Revision, int Raw, int? Overdue, decimal PurchaseTotal);

/// <summary>Закупки отдела за месяц (строки заказов с НДС, по дате утверждения) — не все расходы отдела.</summary>
public sealed record TwinDeptSpend(decimal Total, int Lines);

/// <summary>Отдел: показатели разделов. Users — действующие пользователи OneBase отдела (только администратору пользователей).</summary>
public sealed record TwinDepartmentView(
    Guid Id,
    string Code,
    string Name,
    TwinPart<TwinDeptTasks> Tasks,
    TwinPart<TwinDeptHr> Hr,
    TwinPart<TwinDeptSupply> Supply,
    TwinPart<TwinDeptSpend> Spend,
    int? Users);

/// <summary>
/// Снимок цифрового двойника для пользователя: здания и привязки, показатели предприятия и отделов. Только числа и коды — подписи и перевод
/// на фронтенде. Производства (план и выпуск, загрузка), бюджетов отделов и маркетинговых кампаний в OneBase нет — их блоков в снимке нет.
/// </summary>
public sealed record TwinSnapshot(
    DateTimeOffset GeneratedAt,
    bool CanEdit,
    IReadOnlyList<TwinObjectView> Objects,
    IReadOnlyList<TwinDepartmentView> Departments,
    TwinPart<TwinSales> Sales,
    TwinPart<TwinPrimary> Primary,
    TwinPart<TwinHr> Hr,
    TwinPart<TwinTasks> Tasks,
    TwinPart<TwinSupply> Supply,
    TwinPart<TwinStock> Stock,
    TwinPart<TwinPlant> Plant,
    TwinPart<TwinFinance> Finance,
    TwinPart<TwinPromo> Promo,
    TwinPart<TwinAlerts> Alerts);

// ---------- Привязки ----------

public sealed record TwinOption(string Id, string? Code, string Name);

/// <summary>Что можно привязать к зданию: отделы, склады снабжения, склады Linko готовой продукции завода (из настроек продаж).</summary>
public sealed record TwinOptions(IReadOnlyList<TwinOption> Departments, IReadOnlyList<TwinOption> Warehouses, IReadOnlyList<TwinOption> Plants);

/// <summary>Привязки здания целиком: название (пусто — из модели) и списки отделов, складов снабжения и складов Linko.</summary>
public sealed record TwinObjectInput(string? Name, IReadOnlyList<Guid>? DepartmentIds, IReadOnlyList<Guid>? WarehouseIds, IReadOnlyList<long>? PlantIds);
