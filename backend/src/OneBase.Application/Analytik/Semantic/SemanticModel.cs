using OneBase.Application.Security;

namespace OneBase.Application.Analytik.Semantic;

/// <summary>Фильтр по измерению: in / not_in (значения — id или названия, названия сопоставляет сервер), contains (часть названия).</summary>
public sealed record AnalyticsFilter(string Dimension, string Op = "in", IReadOnlyList<string>? Values = null);

/// <summary>Сортировка по коду показателя или измерения; dir — asc | desc.</summary>
public sealed record AnalyticsSort(string By, string? Dir = null);

/// <summary>
/// Универсальный аналитический запрос: какие показатели, по каким измерениям, за какой период и с каким шагом времени. Модель (или экран
/// «Срезы») описывает, что нужно; SQL строит сервер по реестру (SemanticCatalog) — модель SQL не пишет и правил продаж не знает.
/// </summary>
public sealed record AnalyticsQuery
{
    /// <summary>Компания (развёртывание). Пусто — текущая; чужая — отказ (данных других компаний в базе нет и не будет в ответе).</summary>
    public string? Tenant { get; init; }

    public IReadOnlyList<string> Metrics { get; init; } = [];
    public IReadOnlyList<string>? Dimensions { get; init; }
    public DateOnly? DateFrom { get; init; }
    public DateOnly? DateTo { get; init; }

    /// <summary>Шаг времени для измерения date: day, week, month, quarter, year.</summary>
    public string? Grain { get; init; }

    public IReadOnlyList<AnalyticsFilter>? Filters { get; init; }
    public IReadOnlyList<AnalyticsSort>? Sort { get; init; }
    public int? Limit { get; init; }
    public int? Offset { get; init; }

    /// <summary>Сравнение: previous (такой же отрезок перед периодом), year_ago, custom (CompareFrom / CompareTo).</summary>
    public string? Compare { get; init; }
    public DateOnly? CompareFrom { get; init; }
    public DateOnly? CompareTo { get; init; }

    public bool IncludeTotals { get; init; } = true;

    /// <summary>Добавить дни (недели, месяцы) без продаж нулями — для графиков; продажи не придумываются, отсутствующие дни — ноль.</summary>
    public bool FillZeros { get; init; }

    /// <summary>Какие продажи: secondary — вторичка (как «Продажи»), factory — «Завод» (экспорт и опт), all — вместе.</summary>
    public string? Segment { get; init; }

    /// <summary>Источник плана: auto (как «Продажи»), regions (Google Sheets / AI Data Manager), agents (Linko).</summary>
    public string? PlanSource { get; init; }

    /// <summary>Вид плана регионов: rop, factory, primary.</summary>
    public string? PlanKind { get; init; }

    /// <summary>Сколько дней запаса держать — для потребности в товаре (по умолчанию 14).</summary>
    public int? CoverDays { get; init; }
}

/// <summary>Показатель реестра: формула, источник, единица, факт, право, допустимые измерения и шаги, ограничения качества.</summary>
public sealed record MetricDef(
    string Code,
    string Fact,
    string Title,
    string Unit,
    string Formula,
    string Source,
    string Permission,
    string? Note = null,
    IReadOnlyList<string>? Requires = null)
{
    /// <summary>Считается калькулятором из других показателей (средний чек, доля, выполнение плана…), а не суммой строк.</summary>
    public bool Calculated => Requires is { Count: > 0 };
}

/// <summary>Измерение: в каких фактах оно есть, подпись, пояснение (откуда берётся).</summary>
public sealed record DimensionDef(string Code, string Title, IReadOnlyList<string> Facts, string Note, IReadOnlyList<string>? Aliases = null);

/// <summary>Колонка результата: измерение или показатель, единица, формат для экрана и Excel.</summary>
public sealed record AnalyticsColumn(string Code, string Title, string Kind, string? Unit, string Format);

/// <summary>Проверка доказательств: код, пройдена ли, текст для пользователя и модели.</summary>
public sealed record EvidenceCheck(string Code, bool Ok, string Text);

/// <summary>Источник результата: система, таблицы, когда загружено, правило отбора.</summary>
public sealed record AnalyticsSource(string System, string Title, string Tables, DateTimeOffset? SyncedAt, DateOnly? DataThrough, string Rules);

/// <summary>
/// Результат: колонки (сначала измерения, потом показатели), строки (подписи измерений и значения), ключи строк (id измерений — для перехода
/// вглубь), итоги, сравнение, полнота (Complete и проверки), источники и правила, сколько групп всего (до лимита), id записи журнала analytik.Queries.
/// </summary>
public sealed record AnalyticsResult(
    Guid QueryId,
    AnalyticsQuery Query,
    IReadOnlyList<AnalyticsColumn> Columns,
    IReadOnlyList<object?[]> Rows,
    IReadOnlyList<object?[]> Keys,
    IReadOnlyDictionary<string, decimal?> Totals,
    IReadOnlyDictionary<string, decimal?>? CompareTotals,
    int TotalRows,
    bool HasMore,
    bool Complete,
    IReadOnlyList<EvidenceCheck> Checks,
    IReadOnlyList<AnalyticsSource> Sources,
    IReadOnlyList<string> Notes,
    string? Scope,
    long ElapsedMs,
    DateOnly From,
    DateOnly To,
    DateOnly? CompareFrom,
    DateOnly? CompareTo);

/// <summary>Запрос отклонён: код (invalid, denied, unsupported, too_heavy, timeout) и текст для пользователя и модели.</summary>
public sealed class AnalyticsQueryException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>
/// Реестр показателей и измерений (Metrics Engine): единственное место, где написано, что такое «выручка», «АКБ» или «выполнение плана»,
/// откуда они берутся и по каким измерениям их можно разложить. Выручка, отгрузка (первичка) и оплаты — разные показатели и не смешиваются.
/// </summary>
public static class SemanticCatalog
{
    public const string Sales = "sales";
    public const string Plan = "plan";
    public const string Payments = "payments";
    public const string Stock = "stock";

    /// <summary>Визиты ТП (Linko): маршрут (план), выполненные визиты по маршруту и вне его, визиты с заказом — правила «Календаря визитов».</summary>
    public const string Visits = "visits";

    public static readonly IReadOnlyList<string> Grains = ["day", "week", "month", "quarter", "year"];

    private const string VisitsSource = "Linko: визиты ТП (linko.Visits: день, ТП, точка, в маршруте или нет, статус done / pending); заказы — для визитов с заказом";

    private const string SalesSource = "Linko: заказы и строки заказов (статусы «доставлен» и «выдан», дата приёмки), возвраты по строкам (статус «доставлен», дата создания)";

    public static readonly IReadOnlyList<MetricDef> Metrics =
    [
        new("revenue", Sales, "Выручка", "сум", "Σ сумма строк заказов − Σ (цена × количество) строк возвратов; заказы не в сумах — 0 (курса нет)",
            SalesSource, Permissions.SalesRead),
        new("gross_revenue", Sales, "Выручка заказов (до возвратов)", "сум", "Σ сумма строк заказов в сумах", SalesSource, Permissions.SalesRead),
        new("returns_revenue", Sales, "Возвраты", "сум", "Σ цена × количество строк возвратов", SalesSource, Permissions.SalesRead,
            "Возврат считается в день его создания, а не в день исходного заказа"),
        new("discount", Sales, "Скидки", "сум", "Σ скидка строк заказов", SalesSource, Permissions.SalesRead),
        new("kg", Sales, "Продажи, кг", "кг", "Σ вес строк заказов − Σ вес строк возвратов", SalesSource, Permissions.SalesRead),
        new("gross_kg", Sales, "Заказано, кг", "кг", "Σ вес строк заказов", SalesSource, Permissions.SalesRead),
        new("returns_kg", Sales, "Возвраты, кг", "кг", "Σ вес строк возвратов", SalesSource, Permissions.SalesRead),
        new("units", Sales, "Количество, ед.", "ед.", "Σ количество строк заказов − возвратов в единице товара", SalesSource, Permissions.SalesRead,
            "Единица у товара своя (шт или кг) — складывать единицы разных товаров имеет смысл только внутри товара"),
        new("orders", Sales, "Заказы", "шт", "Число различных заказов", SalesSource, Permissions.SalesRead),
        new("outlets", Sales, "АКБ (точки с покупкой)", "точек", "Различные точки, у которых чистый вес за период в группе больше нуля (как АКБ «Продаж»)",
            SalesSource, Permissions.SalesRead, "Точки-каналы (базар, сети) в АКБ не входят"),
        new("avg_check", Sales, "Средний чек", "сум", "Выручка ÷ заказы", SalesSource, Permissions.SalesRead, null, ["revenue", "orders"]),
        new("avg_price_kg", Sales, "Средняя цена кг", "сум/кг", "Выручка ÷ кг", SalesSource, Permissions.SalesRead, null, ["revenue", "kg"]),
        new("revenue_share", Sales, "Доля выручки", "%", "Выручка строки ÷ выручка всех строк того же дня (недели, месяца) или всего периода × 100",
            SalesSource, Permissions.SalesRead, null, ["revenue"]),
        new("other_currency_revenue", Sales, "Выручка не в сумах (не учтена)", "в валюте", "Σ сумма заказов в другой валюте — в выручку не входит",
            SalesSource, Permissions.SalesRead),

        new("plan_kg", Plan, "План, кг", "кг",
            "План регионов (Google Sheets / AI Data Manager, sales.RegionPlans) или, если на месяц планов регионов нет, сумма планов ТП Linko",
            "sales.RegionPlans (Google Sheets / AI Data Manager), sales.StaffPlans (Linko staff_balance)", Permissions.SalesRead,
            "План — помесячный: по дням и неделям не делится"),
        new("plan_revenue", Plan, "План, сум", "сум", "Сумма денежных планов ТП Linko (sales_sum)", "sales.StaffPlans (Linko staff_balance)",
            Permissions.SalesRead, "Денежный план есть только у ТП; планы регионов — в кг"),
        new("plan_fact_kg", Plan, "Факт для плана, кг", "кг",
            "Факт, с которым «Продажи» сравнивают план: при планах регионов — продажи региона; при планах ТП Linko — продажи ТП, у которых есть план",
            "Месяц «Продаж» (SalesAnalytics)", Permissions.SalesRead, "Есть у среза по регионам; по товарным группам и агентам — продажи, кг"),
        new("plan_execution", Plan, "Выполнение плана", "%", "Факт для плана, кг ÷ план, кг × 100 (как «Продажи»)", "План и факт", Permissions.SalesRead, null,
            ["kg", "plan_kg", "plan_fact_kg"]),
        new("plan_deviation_kg", Plan, "Отклонение от плана, кг", "кг", "Факт для плана, кг − план, кг", "План и факт", Permissions.SalesRead, null,
            ["kg", "plan_kg", "plan_fact_kg"]),
        new("forecast_kg", Plan, "Прогноз месяца, кг", "кг", "Продажи, кг ÷ прошедшие дни месяца × дни месяца (идущий месяц); закрытый месяц — факт",
            "Факт и календарь", Permissions.SalesRead, null, ["kg"]),
        new("forecast_execution", Plan, "Прогноз выполнения", "%", "Прогноз месяца по факту для плана ÷ план, кг × 100", "План и факт", Permissions.SalesRead, null,
            ["kg", "plan_kg", "plan_fact_kg"]),

        new("payments", Payments, "Оплаты", "сум", "Σ принятых оплат точек (без удалённых) по дате создания", "Linko: оплаты (linko.Payments)",
            Permissions.FinanceRead, "Оплаты — деньги, поступившие от точек; это не выручка и не отгрузка"),

        new("visits_planned", Visits, "План визитов (маршрут)", "виз.",
            "Визиты из плана (маршрута) Linko: выполненные и невыполненные прошедших дней; сегодняшние и будущие невыполненные не входят (как «Календарь визитов»)",
            VisitsSource, Permissions.SalesRead),
        new("visits_done", Visits, "Выполнено визитов", "виз.", "Визиты со статусом done — по маршруту и вне его", VisitsSource, Permissions.SalesRead),
        new("visits_done_in_plan", Visits, "Выполнено по маршруту", "виз.", "Выполненные визиты из плана (маршрута)", VisitsSource, Permissions.SalesRead),
        new("visits_off_plan", Visits, "Визиты вне маршрута", "виз.", "Выполненные визиты не из плана", VisitsSource, Permissions.SalesRead),
        new("visits_missed", Visits, "Не посещено по маршруту", "виз.", "Визиты из плана прошедших дней, оставшиеся невыполненными (pending)", VisitsSource,
            Permissions.SalesRead),
        new("visits_with_order", Visits, "Визиты с заказом", "виз.",
            "Выполненные визиты, после которых у этого ТП в этой точке есть проданный заказ, введённый в день визита (как «Визиты с заказом» карточки ТП)",
            VisitsSource, Permissions.SalesRead, "Заказ сшивается с визитом по дню ввода (created_date), а не по дате приёмки"),
        new("visited_outlets", Visits, "Посещённые точки", "точек", "Различные точки с выполненным визитом", VisitsSource, Permissions.SalesRead,
            "За период не суммируется по дням: точка, посещённая в разные дни, — одна"),
        new("route_execution", Visits, "Выполнение маршрута", "%", "Выполнено по маршруту ÷ план визитов × 100", VisitsSource, Permissions.SalesRead, null,
            ["visits_planned", "visits_done_in_plan"]),
        new("visits_without_order", Visits, "Визиты без заказа", "виз.", "Выполненные визиты − визиты с заказом", VisitsSource, Permissions.SalesRead, null,
            ["visits_done", "visits_with_order"]),
        new("visit_strike", Visits, "Страйк (визиты с заказом)", "%", "Визиты с заказом ÷ выполненные визиты × 100", VisitsSource, Permissions.SalesRead, null,
            ["visits_done", "visits_with_order"]),

        new("stock_units", Stock, "Остаток", "ед.", "Остаток товара на складах Linko сейчас (снимок, в единице товара)",
            "Linko: остатки (linko.ProductBalances, linko.Stocks)", Permissions.SalesRead, "Истории остатков нет: только текущий снимок"),
        new("cover_days", Stock, "Дней запаса", "дн.", "Остаток ÷ средние продажи в день за последние 30 дней (единицы)", "Остатки и продажи",
            Permissions.SalesRead, null, ["stock_units"]),
        new("need_units", Stock, "Потребность до запаса", "ед.", "max(0, средние продажи в день × дни запаса − остаток)", "Остатки и продажи",
            Permissions.SalesRead, "Дни запаса — параметр cover_days (по умолчанию 14)", ["stock_units"]),
    ];

    public static readonly IReadOnlyList<DimensionDef> Dimensions =
    [
        new("date", "Дата", [Sales, Plan, Payments, Visits], "Продажа — дата приёмки заказа, возврат — дата создания, оплата — дата создания; визит — день визита; план — месяц",
            ["day", "дата", "день"]),
        new("region", "Регион", [Sales, Plan, Payments, Visits],
            "Регион вторички по филиалу заказа (как «Продажи» и «Продажи по SKU»): старые филиалы — в текущем регионе, точки-каналы — в своём регионе " +
            "в закрытых месяцах; у оплаты и визита — по филиалу точки", ["регион", "область"]),
        new("branch", "Филиал Linko", [Sales, Payments, Visits], "Филиал документа в Linko (без объединения старых филиалов)", ["филиал"]),
        new("direction", "Направление", [Sales, Plan, Payments, Visits], "Направление региона в «Настройках продаж»: РМ 1–4 или канал", ["направление", "рм"]),
        new("manager", "РМ (менеджер)", [Sales, Plan, Payments, Visits], "Руководитель направления региона (РМ)", ["менеджер", "manager"]),
        new("supervisor", "СВР", [Sales, Plan, Payments, Visits], "СВР региона из «Настроек продаж» (в Linko связи агент → СВР нет)", ["супервайзер", "свр"]),
        new("dealer", "Дилер (контрагент)", [Sales, Plan, Payments, Visits], "Дилер региона из «Настроек продаж»", ["контрагент", "дилер", "counterparty"]),
        new("sales_channel", "Канал продаж", [Sales], "Регионы (вторичка), каналы «Настроек продаж» (базар, сети), «Завод» (экспорт и опт)",
            ["канал", "channel"]),
        new("agent", "Агент (ТП)", [Sales, Plan, Payments, Visits], "Торговый представитель документа (Linko)", ["агент", "тп", "торговый представитель"]),
        new("market", "Торговая точка (клиент)", [Sales, Payments, Visits], "Точка (клиент) Linko", ["точка", "клиент", "customer", "outlet"]),
        new("market_type", "Тип точки", [Sales, Payments, Visits], "Тип точки в Linko (например, EXPORT)", ["тип точки"]),
        new("product", "Товар", [Sales, Stock], "Товар Linko", ["товар", "sku_name"]),
        new("sku", "Артикул (SKU)", [Sales, Stock], "Код товара Linko", ["артикул"]),
        new("product_group", "Товарная группа", [Sales, Plan, Stock],
            "Категория отчёта «Продаж» (Sales:Categories): тип Linko и его подтипы → категория; типы вне настройки (бонус, оборудование) — своей группой",
            ["группа", "товарная группа", "category", "категория"]),
        new("product_type", "Тип товара Linko", [Sales, Stock], "Тип (подгруппа) товара в справочнике Linko, например «Помадка шт»", ["тип", "подгруппа"]),
        new("stock", "Склад", [Stock], "Склад Linko", ["склад"]),
        new("source", "Источник", [Sales, Plan, Payments, Stock, Visits], "Система, из которой взяты данные", ["источник"]),
    ];

    public static MetricDef? Metric(string code) => Metrics.FirstOrDefault(m => m.Code == code.Trim().ToLowerInvariant());

    /// <summary>Измерение по коду или синониму («категория» → product_group).</summary>
    public static DimensionDef? Dimension(string code)
    {
        var c = code.Trim().ToLowerInvariant();
        return Dimensions.FirstOrDefault(d => d.Code == c) ?? Dimensions.FirstOrDefault(d => d.Aliases?.Contains(c) == true);
    }

    /// <summary>Факты показателей: для расчётных — факты исходных показателей (выполнение плана — план и продажи).</summary>
    public static IReadOnlySet<string> FactsOf(IEnumerable<MetricDef> metrics)
    {
        var set = new HashSet<string>();
        foreach (var m in metrics)
        {
            if (m.Calculated)
            {
                foreach (var r in m.Requires!)
                {
                    set.Add(Metric(r)!.Fact);
                }
            }
            else
            {
                set.Add(m.Fact);
            }
        }

        return set;
    }

    /// <summary>Все показатели для расчёта: запрошенные и те, из которых считаются расчётные.</summary>
    public static IReadOnlyList<MetricDef> Expand(IEnumerable<MetricDef> metrics)
    {
        var list = new List<MetricDef>();
        foreach (var m in metrics)
        {
            foreach (var r in m.Requires ?? [])
            {
                if (Metric(r) is { } dep && !list.Contains(dep))
                {
                    list.Add(dep);
                }
            }

            if (!list.Contains(m))
            {
                list.Add(m);
            }
        }

        return list;
    }
}
