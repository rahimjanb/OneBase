namespace OneBase.Application.Analytik.Simulation;

/// <summary>
/// Параметр каталога: код, название, единица, вид (change — изменение относительно базы, value — значение сценария, input — исходное значение,
/// которого нет в OneBase, вводит пользователь), пределы и шаг, значение по умолчанию, узел графа, на что нацелен (region, product, item,
/// item_supplier, division, или пусто — на всё), в каких видах сценариев показывается, пояснение.
/// </summary>
public sealed record ParameterDefinition(
    string Code, string Title, string Unit, string Mode, decimal Min, decimal Max, decimal Step, decimal Default, string Node, string? TargetKind,
    IReadOnlyList<string> Kinds, string Description)
{
    public const string Change = "change";
    public const string Value = "value";
    public const string Input = "input";
}

/// <summary>Вид сценария для экрана: код, название, пояснение, какие результаты главные.</summary>
public sealed record ScenarioKindView(string Code, string Title, string Description, IReadOnlyList<string> Focus);

/// <summary>
/// Каталог параметров симулятора. Один смысл — один параметр (и один узел графа), даже если он показан в нескольких видах сценариев: смены —
/// в производстве и персонале, зарплаты — в финансах и персонале, сроки поставки — в сырье и закупках, объём закупки — в закупках и финансах,
/// спрос и объём продаж — один драйвер. Поэтому комбинированный сценарий не учитывает одно воздействие дважды.
/// </summary>
public static class ScenarioCatalog
{
    private static readonly string[] S = [ScenarioKinds.Sales, ScenarioKinds.Combined];
    private static readonly string[] P = [ScenarioKinds.Production, ScenarioKinds.Combined];
    private static readonly string[] M = [ScenarioKinds.Materials, ScenarioKinds.Combined];
    private static readonly string[] Pr = [ScenarioKinds.Procurement, ScenarioKinds.Combined];
    private static readonly string[] F = [ScenarioKinds.Finance, ScenarioKinds.Combined];
    private static readonly string[] W = [ScenarioKinds.Workforce, ScenarioKinds.Combined];
    private static readonly string[] E = [ScenarioKinds.Expansion, ScenarioKinds.Combined];

    private static string[] Of(params string[][] groups) => groups.SelectMany(g => g).Distinct().ToArray();

    public static readonly IReadOnlyList<ParameterDefinition> Parameters =
    [
        // ---- продажи ----
        new("sales.demand", "Спрос и объём продаж", "%", ParameterDefinition.Change, -90, 300, 1, 0, "in.demand", null, Of(S, P),
            "Изменение запрошенного объёма по всем товарам и регионам. Проданный объём — результат: он может быть меньше из-за мощности и сырья."),
        new("sales.region", "Продажи региона", "%", ParameterDefinition.Change, -100, 300, 1, 0, "in.region", "region", S,
            "Изменение запрошенного объёма в одном регионе — поверх общего изменения спроса."),
        new("sales.product", "Продажи товара", "%", ParameterDefinition.Change, -100, 300, 1, 0, "in.product", "product", S,
            "Изменение запрошенного объёма одного товара — поверх общего изменения спроса."),
        new("sales.price", "Цена", "%", ParameterDefinition.Change, -50, 100, 0.5m, 0, "in.price", null, S,
            "Изменение цены до скидки. Объём от цены меняется только при заданной эластичности."),
        new("sales.discount", "Скидки", "п.п.", ParameterDefinition.Change, -30, 50, 0.5m, 0, "in.discount", null, S,
            "Изменение доли скидки в процентных пунктах: цена нетто = цена до скидки × (1 − скидка)."),
        new("sales.elasticity", "Эластичность спроса по цене", "коэф.", ParameterDefinition.Value, -5, 0, 0.1m, 0, "in.elasticity", null, S,
            "На сколько % меняется объём при изменении цены на 1 %. В OneBase её нет — по умолчанию 0 (объём от цены не зависит); задаётся как допущение."),
        new("sales.plan", "План продаж", "%", ParameterDefinition.Change, -90, 300, 1, 0, "in.plan", null, S,
            "Изменение плана продаж (кг). Влияет на выполнение плана, не на спрос."),

        // ---- производство ----
        new("production.volume", "Объём производства", "%", ParameterDefinition.Change, -90, 300, 1, 0, "in.prod_volume", null, P,
            "Выпуск относительно спроса: больше нуля — выпуск на запас готовой продукции, меньше — выпуск ниже спроса."),
        new("production.capacity", "Мощность производства", "%", ParameterDefinition.Change, -90, 300, 1, 0, "in.capacity", null, P,
            "Изменение мощности оборудования (кг в смену)."),
        new("production.utilization", "Допустимая загрузка мощностей", "%", ParameterDefinition.Value, 10, 100, 1, 100, "in.capacity", null, P,
            "Какую долю мощности можно занять (резерв на ремонт, переналадку)."),
        new("production.shifts", "Смен в сутки", "смен", ParameterDefinition.Value, 1, 3, 1, 0, "in.shifts", null, Of(P, W),
            "Число смен в рабочий день. По умолчанию — как по утверждённым сменам. Больше трёх нельзя."),
        new("production.defect", "Производственные потери (брак)", "п.п.", ParameterDefinition.Change, -50, 50, 0.1m, 0, "in.defect", null, P,
            "Изменение доли брака: больше брака — больше сырья на кг годного и меньше годного выпуска с той же мощности."),
        new("production.norms", "Нормы расхода сырья", "%", ParameterDefinition.Change, -50, 100, 0.5m, 0, "in.norms", null, Of(P, M),
            "Изменение норм всех техкарт в рамках сценария (сами техкарты не меняются)."),
        new("production.norm_item", "Норма расхода позиции", "%", ParameterDefinition.Change, -90, 300, 0.5m, 0, "in.norms", "item", Of(P, M),
            "Изменение нормы одной позиции сырья во всех техкартах."),

        // ---- сырьё ----
        new("materials.price", "Цена сырья", "%", ParameterDefinition.Change, -90, 500, 0.5m, 0, "in.mat_price", null, Of(M, Pr, F),
            "Изменение закупочной цены всех позиций."),
        new("materials.price_item", "Цена позиции сырья", "%", ParameterDefinition.Change, -90, 500, 0.5m, 0, "in.mat_price", "item", Of(M, Pr),
            "Изменение закупочной цены одной позиции — поверх общего изменения."),
        new("materials.stock", "Доступный запас сырья", "%", ParameterDefinition.Change, -100, 500, 1, 0, "in.stock", null, M,
            "Изменение доступного остатка всех позиций (например, потеря партии)."),
        new("materials.stock_item", "Доступный запас позиции", "%", ParameterDefinition.Change, -100, 500, 1, 0, "in.stock", "item", M,
            "Изменение доступного остатка одной позиции."),
        new("materials.loss", "Технологические потери сырья", "п.п.", ParameterDefinition.Change, -30, 50, 0.1m, 0, "in.loss", null, M,
            "Изменение потерь сырья в процентных пунктах к потерям строк техкарт."),
        new("materials.lead_time", "Срок поставки", "дн.", ParameterDefinition.Change, -60, 120, 1, 0, "in.lead_time", null, Of(M, Pr),
            "Задержка (больше нуля) или ускорение поставок: сдвигает ожидаемые поставки и срок новой закупки."),

        // ---- закупки ----
        new("procurement.supplier", "Поставщик позиции", "", ParameterDefinition.Value, 0, 0, 0, 0, "in.supplier", "item_supplier", Pr,
            "Закупка позиции у другого поставщика из истории приходов: его последняя цена и срок поставки."),
        new("procurement.min_lot", "Минимальная партия позиции", "ед.", ParameterDefinition.Value, 0, 100_000_000, 1, 0, "in.min_lot", "item", Pr,
            "Минимальное количество одной закупки в единице склада (в OneBase не ведётся — задаётся в сценарии)."),
        new("procurement.volume", "Объём закупки", "%", ParameterDefinition.Change, -100, 300, 1, 0, "in.purchase_volume", null, Of(Pr, F),
            "Изменение закупки относительно расчётной потребности (план закупок Аналитика)."),
        new("procurement.safety", "Страховой запас", "%", ParameterDefinition.Change, -100, 300, 1, 0, "in.safety", null, Pr,
            "Изменение страхового запаса (по умолчанию — как в плане закупок: уровень сервиса 95 % и минимум позиции)."),
        new("procurement.supplier_terms", "Отсрочка оплаты поставщикам", "дн.", ParameterDefinition.Change, -180, 180, 1, 0, "in.supplier_terms", null, Of(Pr, F),
            "Изменение срока оплаты закупок после поставки."),

        // ---- финансы ----
        new("finance.revenue", "Выручка (без изменения продаж)", "%", ParameterDefinition.Change, -90, 300, 1, 0, "in.revenue", null, F,
            "Прямое изменение выручки. Нельзя вместе с параметрами продаж — иначе рост учтётся дважды."),
        new("finance.expenses", "Расходы компании", "%", ParameterDefinition.Change, -90, 300, 1, 0, "in.expenses", null, F,
            "Изменение расходов компании в месяц (исходное значение вводится — в OneBase расходов нет)."),
        new("finance.cost", "Прочая себестоимость", "%", ParameterDefinition.Change, -90, 300, 1, 0, "in.cost", null, F,
            "Изменение введённой себестоимости кг (не сырья техкарт — оно меняется ценой и нормами сырья)."),
        new("finance.customer_terms", "Срок оплаты покупателей", "дн.", ParameterDefinition.Change, -60, 180, 1, 0, "in.customer_terms", null, F,
            "Изменение среднего срока оплаты точек после реализации."),

        // ---- персонал ----
        new("workforce.headcount", "Численность персонала", "%", ParameterDefinition.Change, -90, 300, 1, 0, "in.workforce", "division", Of(W, F),
            "Изменение численности: всех или одного подразделения. Мощность меняет только численность производственного подразделения."),
        new("workforce.salary", "Зарплаты", "%", ParameterDefinition.Change, -50, 300, 1, 0, "in.salary", null, Of(W, F),
            "Изменение средней зарплаты (исходное значение вводится — ФОТ в OneBase нет)."),
        new("workforce.productivity", "Производительность", "%", ParameterDefinition.Change, -90, 300, 1, 0, "in.productivity", null, Of(W, P),
            "Выпуск на сотрудника производства. Выше мощности оборудования выпуск не растёт."),

        // ---- новый регион ----
        new("expansion.plan", "План продаж нового региона", "кг/мес", ParameterDefinition.Value, 0, 10_000_000, 100, 0, "in.expansion", null, E,
            "Сколько кг в месяц планируется продавать в новом регионе."),
        new("expansion.demand", "Предполагаемый спрос", "кг/мес", ParameterDefinition.Value, 0, 10_000_000, 100, 0, "in.expansion", null, E,
            "Оценка спроса в регионе; продажи не больше спроса. Не задан — принимается равным плану (допущение)."),
        new("expansion.reference", "Регион-образец", "", ParameterDefinition.Value, 0, 0, 0, 0, "in.expansion", "region", E,
            "Регион, по которому берутся цены, состав продаж и продажи на агента. Не задан — средние по компании."),
        new("expansion.staff", "Сотрудников в регионе", "чел.", ParameterDefinition.Value, 0, 1000, 1, 0, "in.expansion", null, E,
            "Сколько сотрудников будет в регионе (ФОТ — по введённой средней зарплате)."),
        new("expansion.opex", "Операционные расходы региона", "сум/мес", ParameterDefinition.Value, 0, 1_000_000_000_000, 100_000, 0, "in.expansion", null, E,
            "Аренда, транспорт, связь и прочие расходы региона в месяц."),
        new("expansion.logistics", "Логистика в регион", "сум/кг", ParameterDefinition.Value, 0, 1_000_000, 10, 0, "in.expansion", null, E,
            "Затраты на доставку одного кг в регион."),
        new("expansion.investment", "Вложения на запуск", "сум", ParameterDefinition.Value, 0, 1_000_000_000_000, 1_000_000, 0, "in.expansion", null, E,
            "Разовые затраты на открытие (для срока окупаемости)."),

        // ---- исходные данные, которых нет в OneBase ----
        new("input.capacity", "Мощность производства", "кг/сут", ParameterDefinition.Input, 0, 100_000_000, 100, 0, "in.inputs", null, Of(P, W, ScenarioKinds.All),
            "Сколько кг годной продукции можно выпустить в сутки при текущих сменах. Без ввода — оценка по утверждённым сменам."),
        new("input.salary", "Средняя зарплата", "сум/мес", ParameterDefinition.Input, 0, 1_000_000_000, 100_000, 0, "in.inputs", null, Of(W, F, E, ScenarioKinds.All),
            "Средняя начисленная зарплата сотрудника в месяц — для ФОТ."),
        new("input.opex", "Расходы компании", "сум/мес", ParameterDefinition.Input, 0, 1_000_000_000_000, 1_000_000, 0, "in.inputs", null, Of(F, ScenarioKinds.All),
            "Постоянные расходы компании в месяц без зарплат, сырья и логистики."),
        new("input.cash", "Деньги на начало", "сум", ParameterDefinition.Input, -1_000_000_000_000, 1_000_000_000_000, 1_000_000, 0, "in.inputs", null,
            Of(F, ScenarioKinds.All), "Остаток денег на дату данных — для кассовых разрывов."),
        new("input.credit_limit", "Кредитный лимит", "сум", ParameterDefinition.Input, 0, 1_000_000_000_000, 1_000_000, 0, "in.inputs", null,
            Of(F, ScenarioKinds.All), "Доступное финансирование: потребность больше лимита — ограничение."),
        new("input.cost_per_kg", "Себестоимость кг (без техкарты)", "сум/кг", ParameterDefinition.Input, 0, 100_000_000, 100, 0, "in.inputs", null,
            Of(F, S, ScenarioKinds.All), "Себестоимость кг товаров, у которых нет связанной техкарты с ценами сырья."),
        new("input.logistics_per_kg", "Логистика", "сум/кг", ParameterDefinition.Input, 0, 1_000_000, 10, 0, "in.inputs", null, Of(F, ScenarioKinds.All),
            "Затраты на доставку кг проданной продукции."),
        new("input.supplier_terms", "Отсрочка поставщикам сейчас", "дн.", ParameterDefinition.Input, 0, 365, 1, 0, "in.inputs", null, Of(Pr, F, ScenarioKinds.All),
            "Через сколько дней после поставки оплачивается закупка. Без ввода — оплата при поставке (допущение)."),
        new("input.warehouse_kg", "Вместимость склада сырья", "кг", ParameterDefinition.Input, 0, 1_000_000_000, 1000, 0, "in.inputs", null,
            Of(M, Pr, ScenarioKinds.All), "Сколько кг сырья помещается на складе (позиции в единицах массы)."),
        new("input.production_division", "Производственное подразделение", "", ParameterDefinition.Input, 0, 0, 0, 0, "in.inputs", "division",
            Of(W, P, ScenarioKinds.All), "Подразделение Verifix, численность которого ограничивает мощность."),
    ];

    private static readonly Dictionary<string, ParameterDefinition> ByCode = Parameters.ToDictionary(p => p.Code, StringComparer.Ordinal);

    public static ParameterDefinition? Find(string code) => ByCode.GetValueOrDefault(code);

    /// <summary>Параметры, которые меняют продажи (драйверы выручки) — с ними нельзя прямое изменение выручки.</summary>
    public static readonly IReadOnlySet<string> SalesDrivers = new HashSet<string>(StringComparer.Ordinal)
    {
        "sales.demand", "sales.region", "sales.product", "sales.price", "sales.discount", "sales.elasticity", "expansion.plan", "expansion.demand",
    };

    public static readonly IReadOnlyList<ScenarioKindView> Kinds =
    [
        new(ScenarioKinds.Sales, "Продажи", "Спрос, цена, скидки, план, регионы и товары → выручка, валовая прибыль, нагрузка на производство и склад.",
            ["sales.revenue", "finance.gross_profit", "production.required", "materials.need_value"]),
        new(ScenarioKinds.Production, "Производство", "Объём, мощность, смены, брак, нормы → выпуск, загрузка, потребность в сырье, себестоимость.",
            ["production.output", "production.utilization", "materials.need_value", "finance.cost_per_kg"]),
        new(ScenarioKinds.Materials, "Сырьё", "Цена, расход, запас, потери, сроки поставки → себестоимость, потребность, остатки, дефицит, бюджет.",
            ["finance.cost_per_kg", "materials.items_short", "materials.end_stock_value", "procurement.budget"]),
        new(ScenarioKinds.Procurement, "Закупки", "Поставщик, цена, партия, сроки, объём, страховой запас → стоимость, поставки, дефицит, излишки, оборотный капитал.",
            ["procurement.budget", "materials.items_short", "procurement.surplus_items", "finance.working_capital"]),
        new(ScenarioKinds.Finance, "Финансы", "Выручка, расходы, себестоимость, зарплаты, условия оплаты, закупки → прибыль, денежный поток, разрывы.",
            ["finance.gross_profit", "finance.operating_profit", "finance.net_cash", "finance.financing_need"]),
        new(ScenarioKinds.Workforce, "Персонал", "Численность, зарплаты, производительность, смены → ФОТ, мощность, ограничения.",
            ["workforce.headcount", "finance.payroll", "production.capacity", "production.output"]),
        new(ScenarioKinds.Expansion, "Новый регион", "План, спрос, персонал, расходы, логистика → выручка, расходы, безубыточность, окупаемость.",
            ["sales.revenue", "finance.operating_profit", "production.required", "workforce.headcount"]),
        new(ScenarioKinds.Combined, "Комбинированный", "Все параметры сразу — зависимые показатели пересчитываются один раз, без двойного учёта.",
            ["sales.revenue", "finance.gross_profit", "production.output", "procurement.budget"]),
    ];

    /// <summary>Пределы сложности сценария.</summary>
    public const int MinHorizonDays = 7;

    public const int MaxHorizonDays = 365;
    public const int MaxTargetedParameters = 100;
    public const int MaxParameters = 200;
}
