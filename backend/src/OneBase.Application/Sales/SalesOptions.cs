namespace OneBase.Application.Sales;

public enum SaleDateField
{
    /// <summary>Дата создания заказа (created_date) — день ввода, для отчёта не годится (заказы вводят задним числом).</summary>
    Created,

    /// <summary>Плановая дата доставки (date_delivery) — назначается и на воскресенье, когда приёмок нет.</summary>
    Delivery,

    /// <summary>Приёмка магазином (accepted_time) — дата реализации; так же считает супер-отчёт Linko.</summary>
    Accepted,
}

/// <summary>Настройки аналитики продаж (секция "Sales" в appsettings).</summary>
public sealed class SalesOptions
{
    public const string Section = "Sales";

    /// <summary>Статусы заказов, которые считаются продажей.</summary>
    public string[] SoldStatuses { get; set; } = ["delivered"];

    /// <summary>Статусы возвратов, которые вычитаются из продаж.</summary>
    public string[] ReturnStatuses { get; set; } = ["delivered"];

    public SaleDateField DateField { get; set; } = SaleDateField.Accepted;

    /// <summary>
    /// Филиалы заказов, которые не входят во вторичку. «Завод» — экспорт и крупный опт (Казахстан, Монголия,
    /// Киргизия…), не регион: в республике он завысил бы факт на треть. Показывается отдельным блоком.
    /// Сравнение по названию филиала, без учёта регистра.
    /// </summary>
    public string[] ExcludedBranches { get; set; } = ["Завод"];

    /// <summary>
    /// Старые филиалы Linko: «Жиззах (эски)», «Андижон (эски) 2» — это прежние Жиззах и Андижон до переноса точек
    /// в новый филиал. Филиал, чьё название без этого окончания (регулярное выражение) совпадает с названием региона,
    /// считается в этом регионе — так же сводит «Полевой контроль». Пусто — не объединять.
    /// </summary>
    public string OldBranchSuffix { get; set; } = @"\s*\(эски\)\s*\d*$";

    /// <summary>
    /// Категории отчёта: название → типы товаров Linko (product.type), через запятую. Помадка — пять фасовок
    /// одной категорией; «Снек» (Twizos) в отчёте называется «Придзел». Типы, которых здесь нет (импорт, бонус,
    /// оборудование), в восемь категорий не входят и показываются в диагностике с весом и суммой.
    /// </summary>
    public Dictionary<string, string> Categories { get; set; } = new()
    {
        ["Бамбук"] = "4",
        ["Кекс"] = "10",
        ["Трубочки"] = "9",
        ["Печенье"] = "6",
        ["Шоколад"] = "5",
        ["Песочный"] = "7",
        ["Придзел"] = "22",
        ["Помадка"] = "8,17,18,19,20,21",
    };

    /// <summary>Тип товара → название категории отчёта.</summary>
    public IReadOnlyDictionary<long, string> CategoryByType() =>
        Categories
            .SelectMany(c => c.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => long.TryParse(t, out var id) ? (Id: id, Name: c.Key) : (Id: -1, Name: c.Key)))
            .Where(x => x.Id >= 0)
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.First().Name);

    /// <summary>
    /// Склады завода. Перемещение со склада завода на склад региона — отгрузка дилеру (первичка);
    /// остаток завода в остаток страны не входит — он ещё не отгружен дилерам.
    /// </summary>
    public string[] FactoryStocks { get; set; } = ["Завод"];

    /// <summary>
    /// Склады экспорта: перемещение с завода туда — экспорт, а не отгрузка дилеру; в первичку не входит, показывается отдельно.
    /// </summary>
    public string[] ExportStocks { get; set; } = ["Экспорт"];

    public bool IsExportStock(string? stockName) =>
        stockName is { } name && ExportStocks.Any(s => string.Equals(s.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Типы торговых точек Linko, заказы которых с филиала «Завод» — экспорт («Первичка → Экспорт»).
    /// Тип EXPORT стоит и у части магазинов других филиалов — их заказы в экспорт не входят.
    /// </summary>
    public string[] ExportMarketTypes { get; set; } = ["EXPORT"];

    public bool IsExportMarketType(string? type) =>
        type is { } name && ExportMarketTypes.Any(t => string.Equals(t.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Страна экспорта — по словам в названии или адресе торговой точки (отдельного поля страны в Linko нет):
    /// страна → слова (без учёта регистра). Точка без совпадений показывается своим названием.
    /// </summary>
    public Dictionary<string, string[]> ExportCountries { get; set; } = new()
    {
        ["Таджикистан"] = ["tojikiston", "tajikistan", "таджикистан", "тожикистон", "dushanbe", "душанбе", "hujand", "khujand", "худжанд"],
        ["Киргизия"] = ["kyrgyz", "киргиз", "кыргыз", "bishkek", "бишкек"],
        ["Казахстан"] = ["kazakh", "казахстан", "qazaq", "aktobe", "актобе", "almaty", "алматы"],
        ["Азербайджан"] = ["azerbaijan", "азербайджан", "baku", "баку"],
        ["Армения"] = ["armenia", "армения", "yerevan", "ереван"],
        ["Монголия"] = ["mongolia", "монголия", "ulaanbaatar", "улан-батор"],
        ["Афганистан"] = ["afghanistan", "афганистан"],
        ["Ирак"] = ["iraq", "ирак"],
        ["Латвия"] = ["latvia", "латвия"],
        ["Россия"] = ["russia", "россия", "dagestan", "дагестан", "ставропол"],
        ["Туркменистан"] = ["turkmen", "туркмен"],
        ["Грузия"] = ["georgia", "грузия"],
        ["Палестина"] = ["palestine", "палестин"],
    };

    /// <summary>Страна экспортной точки по названию и адресу; null — слова не найдены.</summary>
    public string? ExportCountryOf(string name, string? address)
    {
        var text = $"{name} {address}".ToLowerInvariant();
        return ExportCountries.FirstOrDefault(c => c.Value.Any(w => text.Contains(w.ToLowerInvariant()))).Key;
    }

    /// <summary>Статусы перемещений, которые считаются отгрузкой: отдано (given) или уже принято (accepted).</summary>
    public string[] ShippedTransferStatuses { get; set; } = ["given", "accepted"];

    /// <summary>
    /// Прайс-лист Linko с ценой, по которой дилер продаёт дальше («Дилердан чикиш нарх»): по нему считается «сумма дилера»
    /// в первичке. «Сумма завода» — цена самого перемещения (у отгрузок дилерам это прайс «Дилерга кириш нарх»).
    /// </summary>
    public string PrimaryDealerPriceList { get; set; } = "Дилердан чикиш нарх";

    /// <summary>За сколько дней считать скорость продаж для запаса на складах.</summary>
    public int StockVelocityDays { get; set; } = 90;

    public bool IsFactoryStock(string? stockName) =>
        stockName is { } name && FactoryStocks.Any(s => string.Equals(s.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Основная валюта выручки (как Linko пишет её в заказе: «SUM»). Выручку в других валютах курса нет —
    /// она не складывается с основной, а показывается отдельно. Заказ без валюты считается в основной.
    /// </summary>
    public string BaseCurrency { get; set; } = "SUM";

    public bool IsBaseCurrency(string? currency) =>
        string.IsNullOrWhiteSpace(currency) || string.Equals(currency.Trim(), BaseCurrency.Trim(), StringComparison.OrdinalIgnoreCase);

    public bool IsExcludedBranch(string? branchName) =>
        branchName is { } name && ExcludedBranches.Any(b => string.Equals(b.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase));

    public SalesSyncOptions Sync { get; set; } = new();

    /// <summary>
    /// Должности Linko, чьи планы из staff_balance не считаются планом ТП: у супервайзеров план — это план их команды,
    /// и при суммировании он задвоил бы планы агентов. Сравнение — «содержит», без учёта регистра.
    /// </summary>
    public string[] StaffPlanExcludeJobs { get; set; } = ["Супервайзер", "Supervisor"];

    /// <summary>
    /// Должности Linko, которые не продают: их визиты — это доставки, а не визиты ТП. Такие сотрудники не попадают
    /// в список ТП, конверсию, «визиты без заказа» и «Проблемных агентов». Их заказы (если есть) остаются в факте.
    /// Сравнение — «содержит», без учёта регистра.
    /// </summary>
    public string[] NonSalesJobs { get; set; } = ["Доставщик", "Курьер", "Экспедитор"];

    /// <summary>
    /// Должности Linko, которые считаются торговыми представителями (ТП): только они входят в численность ТП («АКБ на агента»,
    /// «действующие ТП»), в медианы региона и в «Проблемных агентов». Операторы, супервайзеры, админы оформляют заказы, но не ТП —
    /// их продажи остаются в итогах региона. Человек из оргструктуры OneBase считается ТП всегда. Пусто — ТП все.
    /// </summary>
    public string[] SalesRepJobs { get; set; } = ["Агент"];

    public FlagThresholds Flags { get; set; } = new();
}

public sealed class SalesSyncOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 20;

    /// <summary>Сколько месяцев истории загружать при первой синхронизации.</summary>
    public int BackfillMonths { get; set; } = 12;
}

/// <summary>Пороги флагов «Проблемных агентов». Доли — от 0 до 1.</summary>
public sealed class FlagThresholds
{
    /// <summary>Минимум визитов за месяц, чтобы агента оценивать.</summary>
    public int MinVisits { get; set; } = 20;

    public decimal ConversionCritical { get; set; } = 0.15m;
    public decimal ConversionCriticalOfMedian { get; set; } = 0.50m;
    public decimal ConversionRisk { get; set; } = 0.25m;
    public decimal ConversionRiskOfMedian { get; set; } = 0.70m;

    /// <summary>«Ходит, но не продаёт» (риск): сумма с визита ниже этой доли медианы.</summary>
    public decimal SumPerVisitRiskOfMedian { get; set; } = 0.40m;

    /// <summary>«Мелкий чек» (риск): средний чек ниже этой доли медианы...</summary>
    public decimal SmallCheckOfMedian { get; set; } = 0.60m;

    /// <summary>...при конверсии не ниже этой...</summary>
    public decimal SmallCheckMinConversion { get; set; } = 0.25m;

    /// <summary>...и не меньше этого числа заказов.</summary>
    public int SmallCheckMinOrders { get; set; } = 20;

    /// <summary>«Узкий ассортимент» (критично): категорий не больше этого числа. Риск — на одну меньше цели.</summary>
    public int NarrowAssortmentCritical { get; set; } = 2;

    public decimal TempoCritical { get; set; } = 0.75m;
    public decimal TempoRisk { get; set; } = 0.90m;
}
