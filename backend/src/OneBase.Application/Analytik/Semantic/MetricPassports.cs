namespace OneBase.Application.Analytik.Semantic;

/// <summary>
/// Профиль показателя движка — то, чего нет в SemanticCatalog: бизнес-смысл, таблица и поле, тип данных, как агрегируется, складывается ли по
/// строкам, частью какого показателя является и от каких зависит математически (формулой). По нему строятся паспорт показателя и проверка
/// совместимости пар (DataCompatibility).
/// </summary>
public sealed record MetricProfile(
    string Meaning,
    string Tables,
    string Field,
    string DataType,
    string Aggregation,
    bool Additive,
    string? PartOf = null,
    IReadOnlyList<string>? DependsOn = null);

/// <summary>
/// Паспорт показателя для AI и экрана: всё из реестра (формула, источник, единица, право, измерения и шаги) плюс профиль, диапазон данных,
/// последняя загрузка и свежесть источника, совместимые факты, ограничения и доступ пользователя.
/// </summary>
public sealed record MetricPassport(
    string Code,
    string Fact,
    string Title,
    string Meaning,
    string Formula,
    string Source,
    string Tables,
    string Field,
    string DataType,
    string Unit,
    string Aggregation,
    bool Additive,
    bool Calculated,
    string? PartOf,
    IReadOnlyList<string> DependsOn,
    IReadOnlyList<string> Grains,
    IReadOnlyList<string> Dimensions,
    IReadOnlyList<string> CompatibleFacts,
    DateOnly? First,
    DateOnly? Last,
    DateTimeOffset? SyncedAt,
    string Freshness,
    string? Limits,
    string Permission,
    bool Allowed);

/// <summary>
/// Показатель отдела из карты аналитики (ТЗ Phase 2): где он считается — движок срезов (engine, коды показателей), метод расчёта (compute),
/// готовый инструмент отдела (tool) или нигде (missing — с модулем, который должен его дать). Tables — таблицы-источники для живой проверки:
/// есть ли в них строки (по каталогу базы Аналитика); через запятую — нужны все, через «|» — достаточно любой.
/// </summary>
public sealed record BusinessMetric(string Domain, string Title, string Kind, string Implementation, string Tables, string? Module, string Note);

/// <summary>Показатель карты с живым состоянием: есть ли данные в таблицах-источниках (оценка строк каталога) и доступен ли пользователю.</summary>
public sealed record BusinessMetricState(BusinessMetric Metric, string Status, long Rows, bool Allowed);

/// <summary>Профили показателей движка и карта показателей отделов. Единственное место, где записано, откуда берётся каждый показатель.</summary>
public static class MetricPassports
{
    private const string OrderLines = "linko.OrderLines, linko.Orders";
    private const string ReturnLines = "linko.OrderReturnLines, linko.OrderReturns";

    public static readonly IReadOnlyDictionary<string, MetricProfile> Profiles = new Dictionary<string, MetricProfile>
    {
        ["revenue"] = new("Чистая выручка вторички: сколько денег принесли продажи точкам за вычетом возвратов", $"{OrderLines}, {ReturnLines}",
            "OrderLines.TotalPrice − OrderReturnLines.Price × Amount", "numeric (сум)", "sum", true, "gross_revenue"),
        ["gross_revenue"] = new("Выручка заказов до возвратов", OrderLines, "OrderLines.TotalPrice", "numeric (сум)", "sum", true),
        ["returns_revenue"] = new("Сумма возвратов от точек", ReturnLines, "OrderReturnLines.Price × Amount", "numeric (сум)", "sum", true, "gross_revenue"),
        ["discount"] = new("Скидки, предоставленные в заказах (акции и ручные скидки Linko)", OrderLines, "OrderLines.TotalDiscount", "numeric (сум)", "sum", true),
        ["kg"] = new("Чистый объём продаж в весе", $"{OrderLines}, {ReturnLines}", "OrderLines.TotalWeight − OrderReturnLines.TotalWeight", "numeric (кг)", "sum", true,
            "gross_kg"),
        ["gross_kg"] = new("Заказанный вес до возвратов", OrderLines, "OrderLines.TotalWeight", "numeric (кг)", "sum", true),
        ["returns_kg"] = new("Вес возвратов", ReturnLines, "OrderReturnLines.TotalWeight", "numeric (кг)", "sum", true, "gross_kg"),
        ["units"] = new("Количество в единице товара", $"{OrderLines}, {ReturnLines}", "OrderLines.Amount − OrderReturnLines.Amount", "numeric (ед.)", "sum", true),
        ["orders"] = new("Сколько заказов сделали точки", "linko.Orders", "Orders.Id", "integer", "count distinct", false),
        ["outlets"] = new("АКБ — активная клиентская база: точки с чистой покупкой", OrderLines, "Orders.MarketId", "integer", "count distinct (вес > 0)", false),
        ["avg_check"] = new("Средняя сумма одного заказа", OrderLines, "revenue ÷ orders", "numeric (сум)", "ratio", false, null, ["revenue", "orders"]),
        ["avg_price_kg"] = new("Средняя цена килограмма (со скидками)", OrderLines, "revenue ÷ kg", "numeric (сум/кг)", "ratio", false, null, ["revenue", "kg"]),
        ["revenue_share"] = new("Доля строки в выручке", OrderLines, "revenue ÷ revenue всех строк", "numeric (%)", "ratio", false, "revenue", ["revenue"]),
        ["other_currency_revenue"] = new("Заказы не в сумах (в выручку не входят)", OrderLines, "OrderLines.TotalPrice (Currency ≠ сум)", "numeric (валюта)", "sum", true),
        ["plan_kg"] = new("План продаж в весе на месяц", "sales.RegionPlans, sales.StaffPlans", "RegionPlans.Kg / StaffPlans (вес)", "numeric (кг)", "sum по месяцам", true),
        ["plan_revenue"] = new("Денежный план ТП на месяц", "sales.StaffPlans", "StaffPlans (sales_sum)", "numeric (сум)", "sum по месяцам", true),
        ["plan_fact_kg"] = new("Факт, с которым сравнивается план", $"{OrderLines}, {ReturnLines}", "kg по правилам месяца «Продаж»", "numeric (кг)", "sum", true, null, ["kg"]),
        ["plan_execution"] = new("Насколько выполнен план", "sales.RegionPlans, sales.StaffPlans, linko.OrderLines", "plan_fact_kg ÷ plan_kg", "numeric (%)", "ratio", false, null,
            ["plan_fact_kg", "plan_kg"]),
        ["plan_deviation_kg"] = new("Сколько не хватает до плана (или перевыполнено)", "sales.RegionPlans, sales.StaffPlans, linko.OrderLines", "plan_fact_kg − plan_kg",
            "numeric (кг)", "difference", true, null, ["plan_fact_kg", "plan_kg"]),
        ["forecast_kg"] = new("Ожидаемый вес месяца при текущем темпе", OrderLines, "kg ÷ прошедшие дни × дни месяца", "numeric (кг)", "projection", false, null, ["kg"]),
        ["forecast_execution"] = new("Ожидаемое выполнение плана месяца", "sales.RegionPlans, linko.OrderLines", "forecast ÷ plan_kg", "numeric (%)", "ratio", false, null,
            ["plan_fact_kg", "plan_kg"]),
        ["payments"] = new("Деньги, поступившие от точек", "linko.Payments", "Payments.Amount", "numeric (сум)", "sum", true),
        ["stock_units"] = new("Остаток готовой продукции на складах сейчас", "linko.ProductBalances", "ProductBalances.Balance", "numeric (ед.)", "snapshot", true),
        ["cover_days"] = new("На сколько дней хватит остатка", "linko.ProductBalances, linko.OrderLines", "stock_units ÷ продажи в день за 30 дней", "numeric (дн.)", "ratio",
            false, null, ["stock_units"]),
        ["need_units"] = new("Сколько товара не хватает до нормы запаса", "linko.ProductBalances, linko.OrderLines", "max(0, продажи в день × дни − остаток)", "numeric (ед.)",
            "projection", true, null, ["stock_units"]),
        ["visits_planned"] = new("Сколько визитов было в маршрутах", "linko.Visits", "Visits.IsInPlan", "integer", "count", true),
        ["visits_done"] = new("Сколько визитов выполнили ТП", "linko.Visits", "Visits.Status = done", "integer", "count", true),
        ["visits_done_in_plan"] = new("Выполненные визиты маршрута", "linko.Visits", "Status = done и IsInPlan", "integer", "count", true, "visits_done"),
        ["visits_off_plan"] = new("Выполненные визиты вне маршрута", "linko.Visits", "Status = done и не IsInPlan", "integer", "count", true, "visits_done"),
        ["visits_missed"] = new("Визиты маршрута, которые не состоялись", "linko.Visits", "IsInPlan и Status = pending (прошедшие дни)", "integer", "count", true, "visits_planned"),
        ["visits_with_order"] = new("Визиты, закончившиеся заказом", "linko.Visits, linko.Orders", "Visits ⋈ Orders (ТП, точка, день ввода)", "integer", "count", true, "visits_done"),
        ["visits_without_order"] = new("Визиты без заказа", "linko.Visits, linko.Orders", "visits_done − visits_with_order", "integer", "difference", true, "visits_done",
            ["visits_done", "visits_with_order"]),
        ["visited_outlets"] = new("Охват: сколько разных точек посетили", "linko.Visits", "Visits.MarketId", "integer", "count distinct", false),
        ["route_execution"] = new("Дисциплина маршрута", "linko.Visits", "visits_done_in_plan ÷ visits_planned", "numeric (%)", "ratio", false, null,
            ["visits_done_in_plan", "visits_planned"]),
        ["visit_strike"] = new("Конверсия визита в заказ", "linko.Visits, linko.Orders", "visits_with_order ÷ visits_done", "numeric (%)", "ratio", false, null,
            ["visits_with_order", "visits_done"]),
    };

    /// <summary>Профиль показателя; у нового показателя без профиля — общий (сумма из реестра), чтобы паспорт не падал.</summary>
    public static MetricProfile Profile(MetricDef m) =>
        Profiles.GetValueOrDefault(m.Code) ?? new MetricProfile(m.Title, m.Source, m.Formula, "numeric", m.Calculated ? "ratio" : "sum", !m.Calculated, null, m.Requires);

    /// <summary>
    /// Математическая связь пары показателей: один — часть другого, один считается из другого, общий источник формулы. null — независимы
    /// по определению (связь между ними может быть только фактической).
    /// </summary>
    public static string? Dependency(string a, string b)
    {
        if (a == b)
        {
            return "один и тот же показатель";
        }

        var pa = Profiles.GetValueOrDefault(a);
        var pb = Profiles.GetValueOrDefault(b);
        if (pa?.PartOf == b || pb?.PartOf == a)
        {
            var (part, whole) = pa?.PartOf == b ? (a, b) : (b, a);
            return $"«{Title(part)}» — часть «{Title(whole)}»: корреляция части с целым завышена";
        }

        if (pa?.DependsOn?.Contains(b) == true || pb?.DependsOn?.Contains(a) == true)
        {
            var (derived, from) = pa?.DependsOn?.Contains(b) == true ? (a, b) : (b, a);
            return $"«{Title(derived)}» считается из «{Title(from)}» — связь частично заложена формулой";
        }

        if (pa?.PartOf is { } wa && wa == pb?.PartOf)
        {
            return $"оба — части «{Title(wa)}»";
        }

        return null;
    }

    private static string Title(string code) => SemanticCatalog.Metric(code)?.Title ?? code;

    private const string Sales = "Продажи";
    private const string Visits = "Визиты";
    private const string Promo = "Промо";
    private const string Finance = "Финансы";
    private const string Warehouse = "Склад";
    private const string Production = "Производство";
    private const string Hr = "HR";

    /// <summary>Карта показателей отделов из ТЗ: что из перечня есть в OneBase и где, чего нет и какой модуль должен это дать.</summary>
    public static readonly IReadOnlyList<BusinessMetric> Business =
    [
        new(Sales, "Выручка", "engine", "gross_revenue", OrderLines, null, "До возвратов"),
        new(Sales, "Чистая выручка", "engine", "revenue", $"{OrderLines}, {ReturnLines}", null, "Заказы − возвраты, как «Продажи»"),
        new(Sales, "Возвраты", "engine", "returns_revenue, returns_kg", ReturnLines, null, "В день создания возврата"),
        new(Sales, "Скидки", "engine", "discount", OrderLines, null, "Скидки строк заказов"),
        new(Sales, "Объём, кг", "engine", "kg", OrderLines, null, ""),
        new(Sales, "Количество заказов", "engine", "orders", "linko.Orders", null, ""),
        new(Sales, "Средний чек", "engine", "avg_check", OrderLines, null, "Выручка ÷ заказы"),
        new(Sales, "АКБ", "engine", "outlets", OrderLines, null, "Точки с чистой покупкой"),
        new(Sales, "Выполнение плана", "engine", "plan_execution, plan_deviation_kg", "sales.RegionPlans | sales.StaffPlans", null, "План — помесячный: планы регионов или ТП"),
        new(Sales, "Продажи по товарным группам, регионам, агентам, точкам", "engine", "dimensions: product_group, region, agent, market", OrderLines, null, "Любые сочетания"),
        new(Visits, "Количество визитов", "engine", "visits_done, visits_planned", "linko.Visits", null, ""),
        new(Visits, "Выполненные визиты", "engine", "visits_done, visits_done_in_plan, visits_off_plan", "linko.Visits", null, ""),
        new(Visits, "Невыполненные визиты", "engine", "visits_missed", "linko.Visits", null, "Визиты маршрута прошедших дней в статусе pending"),
        new(Visits, "Визиты с заказом", "engine", "visits_with_order", "linko.Visits, linko.Orders", null, "Заказ ТП в точке в день визита"),
        new(Visits, "Визиты без заказа", "engine", "visits_without_order", "linko.Visits, linko.Orders", null, "Выполненные − с заказом"),
        new(Visits, "Конверсия визитов в заказ", "engine", "visit_strike", "linko.Visits, linko.Orders", null, "Страйк"),
        new(Visits, "Эффективность ТП", "compute", "research agent_efficiency", "linko.Visits, linko.Orders", null, "Визиты × продажи по ТП"),
        new(Visits, "Выполнение маршрутов", "engine", "route_execution", "linko.Visits", null, "Как «Календарь визитов»"),
        new(Promo, "Промо-активности", "tool", "get_promo", "linko.Promotions, linko.PromotionProducts", null, "Акции Linko: даты, товары, вид скидки"),
        new(Promo, "Скидки", "engine", "discount", OrderLines, null, "Скидки строк заказов"),
        new(Promo, "Промо-расходы", "engine", "discount (по товарам акции и её датам)", OrderLines, null,
            "Расход = скидки заказов; бонусов товаром и внешних расходов на акции в OneBase нет"),
        new(Promo, "Продажи по акциям", "compute", "research promo_effect", "linko.Promotions, linko.PromotionProducts, linko.OrderLines", null,
            "Строка заказа с акцией в Linko не связана — продажи акции = продажи её товаров в её даты"),
        new(Promo, "Прирост продаж, эффективность", "compute", "research promo_effect (до / после и контрольная группа)", "linko.Promotions, linko.OrderLines", null, ""),
        new(Promo, "ROI акций", "compute", "research promo_effect (прирост выручки ÷ скидки)", "linko.Promotions, linko.OrderLines", "Финансы (себестоимость)",
            "ROI по выручке: себестоимости продукции в OneBase нет, ROI по прибыли не считается"),
        new(Finance, "Оплаты (поступления)", "engine", "payments", "linko.Payments", null, "Деньги от точек"),
        new(Finance, "Дебиторская задолженность", "tool", "get_dso", "sales.DealerLedger", null, "Долг дилеров по FIFO"),
        new(Finance, "Просроченная задолженность", "tool", "get_dso", "sales.DealerLedger", null, "По норме дней дилера"),
        new(Finance, "Расходы", "missing", "", "", "Финансы (модуль не построен)", "Расходов компании в OneBase нет"),
        new(Finance, "Денежный поток", "compute", "cash_conversion", "linko.Payments", "Финансы (модуль не построен)", "Только поступления от точек; выплат и остатков денег нет"),
        new(Finance, "Финансовые отклонения", "compute", "plan_variance", "sales.RegionPlans | sales.StaffPlans", "Финансы (бюджеты)", "План и факт продаж; бюджетов нет"),
        new(Warehouse, "Остатки", "engine", "stock_units", "linko.ProductBalances", null, "Снимок сейчас"),
        new(Warehouse, "Движение товаров", "tool", "analytik_warehouse (сырьё); перемещения Linko не в движке", "linko.StockTransfers, procurement.StockMovements", null,
            "Перемещения готовой продукции между складами Linko загружаются, но в срезы не входят"),
        new(Warehouse, "Оборачиваемость, дни запаса", "engine", "cover_days; compute stock_health", "linko.ProductBalances", null, ""),
        new(Warehouse, "Дефицит и излишки", "compute", "stock_health, need_units", "linko.ProductBalances", null, ""),
        new(Warehouse, "Неликвиды", "compute", "stock_health (без продаж 30/90 дней)", "linko.ProductBalances", null, ""),
        new(Production, "Выпуск", "tool", "analytik_production", "production.Orders, production.Shifts", null, ""),
        new(Production, "Выполнение плана производства", "tool", "analytik_production", "production.Orders", null, ""),
        new(Production, "Загрузка линий, производительность", "missing", "", "production.Shifts", "Производство (учёт смен и мощности линий)",
            "Мощности линий в OneBase не заведены"),
        new(Production, "Брак", "tool", "analytik_production", "production.ShiftDefects", null, ""),
        new(Production, "Простои", "missing", "", "", "Производство (журнал простоев)", "Простои не учитываются"),
        new(Production, "Себестоимость", "tool", "analytik_production (сырьё по техкартам)", "production.Recipes, procurement.Items", null, "Только сырьё по техкартам"),
        new(Hr, "Численность", "tool", "get_hr_employees", "hr.Employees", null, "Verifix"),
        new(Hr, "Посещаемость", "tool", "get_hr_timesheet", "hr.TimesheetDays, hr.AttendanceEvents", null, ""),
        new(Hr, "Опоздания", "tool", "get_hr_timesheet", "hr.TimesheetDays", null, ""),
        new(Hr, "Отсутствия", "tool", "get_hr_timesheet", "hr.TimesheetDays", null, ""),
        new(Hr, "Производительность сотрудников", "compute", "unit_economics (продажи на ТП)", OrderLines, "HR (выработка цехов)", "Только у ТП; выработки цехов нет"),
    ];
}
