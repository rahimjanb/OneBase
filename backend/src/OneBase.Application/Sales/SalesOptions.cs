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

    /// <summary>
    /// Статусы заказов, которые считаются продажей во всех расчётах «Продаж»: вторичка, история и АКБ по месяцам, скорость
    /// остатка, аутсток. Доставлен (delivered) и отдан (given): у «отдан» в Linko есть приёмка — товар принят, статус не переставлен;
    /// у отменённых и недоставленных приёмки нет. Так считает «Полевой контроль» (статус не фильтруется с 01.10.2026).
    /// </summary>
    public string[] SoldStatuses { get; set; } = ["delivered", "given"];

    /// <summary>
    /// Статусы заказов «Первичка → Экспорт»: только доставленные — так экспорт сверен с «Полевым контролем» до килограмма
    /// (у «Завода» в сентябре 2026 ещё 27 отданных заказов на 12,9 т, в экспорт по странам они не входят).
    /// </summary>
    public string[] ExportSoldStatuses { get; set; } = ["delivered"];

    /// <summary>
    /// Статусы «доставленных» заказов Sales Base (/api/field, sales.1base.uz): только доставленные — поведение Sales Base
    /// от правил вторички не зависит и не меняется.
    /// </summary>
    public string[] FieldSoldStatuses { get; set; } = ["delivered"];

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
    /// Филиалы заказов, которых во вторичке нет совсем: ни региона, ни блока «Экспорт и опт», ни месяцев, отчётного дня, АКБ по месяцам,
    /// визитов, «Проблемных агентов», остатка и аутстока. «К К Мерч» — сетевые магазины (заказы оформляет админ), в «Полевом контроле»
    /// это не регион. На первичку не влияет. Сравнение по названию филиала, без учёта регистра.
    /// </summary>
    public string[] IgnoredBranches { get; set; } = ["К К Мерч"];

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

    /// <summary>
    /// ТОП-товары — коды товаров Linko (product.code), без учёта регистра; в appsettings — 59 кодов списка ТОП «Полевого контроля».
    /// Читаются через <see cref="TopProductSet"/>: метка «ТОП» в ассортименте и фильтр ТОП в «Продажах по SKU» (пусто — меток нет).
    /// В аутстоке пусто — ТОП считаются все товары категорий отчёта (<see cref="Categories"/>), а типы вне отчёта — «кроме ТОПа».
    /// </summary>
    public string[] TopProducts { get; set; } = [];

    /// <summary>
    /// Категория отчёта → слова в названии весового показателя плана Linko («Сентябрь Бамбук Бухоро»,
    /// «Сентябрь Могуль + Шоколад …»). «Могуль» — так в планах называется помадка. Показатель без этих слов — план
    /// без разбивки по категориям.
    /// </summary>
    public Dictionary<string, string[]> PlanIndicatorCategories { get; set; } = new()
    {
        ["Бамбук"] = ["бамбук"],
        ["Кекс"] = ["кекс"],
        ["Трубочки"] = ["трубоч"],
        ["Печенье"] = ["печен"],
        ["Шоколад"] = ["шоколад"],
        ["Песочный"] = ["песоч"],
        ["Придзел"] = ["придзел", "twizos", "снек"],
        ["Помадка"] = ["помадк", "могул"],
    };

    /// <summary>
    /// Категории отчёта, которых нет в «АКБ по месяцам» (DOC-filters §1: «Песочный» скрыт — 2–155 точек, в общей картине он только занимал
    /// строку). В карточках категорий и в плане они остаются. Названия категорий отчёта (<see cref="Categories"/>), без учёта регистра.
    /// </summary>
    public string[] AkbChartHiddenCategories { get; set; } = ["Песочный"];

    /// <summary>Категории отчёта, к которым относится показатель плана Linko.</summary>
    public IReadOnlyList<string> PlanCategoriesOf(string? indicatorName)
    {
        // В порядке упоминания в названии: «Могуль + Шоколад» → Помадка, Шоколад.
        var text = (indicatorName ?? string.Empty).ToLowerInvariant();
        return PlanIndicatorCategories
            .Select(c => (c.Key, At: c.Value.Select(w => text.IndexOf(w.ToLowerInvariant(), StringComparison.Ordinal)).Where(i => i >= 0).DefaultIfEmpty(-1).Min()))
            .Where(c => c.At >= 0)
            .OrderBy(c => c.At)
            .Select(c => c.Key)
            .ToList();
    }

    /// <summary>
    /// Вакансия в Linko — незакрытая позиция, а не агент: в имени есть «вакан» («вакант», «Вакан (Термиз туман)») или ID равен нулю.
    /// Вакансии не входят в рейтинг «Проблемных агентов», в медианы региона и в численность ТП.
    /// </summary>
    public string[] VacancyMarkers { get; set; } = ["вакан"];

    public bool IsVacancy(long id, string? name) => IsVacancy(id, name, VacancyMarkers);

    /// <summary>Вакансия для Sales Base (кандидаты из Linko): прежнее слово «вакант» — поведение Sales Base не меняется.</summary>
    public string[] FieldVacancyMarkers { get; set; } = ["вакант"];

    public bool IsFieldVacancy(long id, string? name) => IsVacancy(id, name, FieldVacancyMarkers);

    private static bool IsVacancy(long id, string? name, string[] markers) =>
        id == 0 || (name is { } n && markers.Any(m => n.Contains(m, StringComparison.OrdinalIgnoreCase)));

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
        // Россия — по точкам, как в «Полевом контроле»: «Дагестан ФАЙДА» (Россия, СКФО) и «ООО ВЛАДКОН» (в Linko — Москва, у эталона — Уфа);
        // другие российские точки — «Россия». Порядок важен: первое совпадение.
        ["Россия (Дагестан)"] = ["dagestan", "дагестан", "ставропол", "скфо"],
        ["Россия (Уфа)"] = ["владкон", "vladkon"],
        ["Россия"] = ["russia", "россия"],
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
    /// Филиалы, заказы которых — тоже первичка: контрагенты завода помимо складов дилеров. «Завод» — базары, сети, фирменный магазин (точки
    /// не экспортного типа, <see cref="ExportMarketTypes"/>; экспорт — «Первичка → Экспорт»), «К К Мерч» — сетевые магазины. Без учёта регистра.
    /// Во вторичку эти заказы по-прежнему не входят (<see cref="ExcludedBranches"/>, <see cref="IgnoredBranches"/>).
    /// </summary>
    public string[] PrimaryOrderBranches { get; set; } = ["Завод", "К К Мерч"];

    /// <summary>Статусы заказов первички (<see cref="PrimaryOrderBranches"/>): доставлен и отдан — у этих точек «отдан» и есть отгрузка.</summary>
    public string[] PrimaryOrderStatuses { get; set; } = ["delivered", "given"];

    /// <summary>
    /// Точки, заказы которых в первичку не входят, — название (без учёта регистра) или id точки Linko: «Дегустатсия + Акция» (id 27896,
    /// филиал «Завод») — дегустации и акции, не продажа контрагенту.
    /// </summary>
    public string[] PrimaryExcludedMarkets { get; set; } = ["Дегустатсия + Акция"];

    public bool IsPrimaryOrderBranch(string? branchName) => IsBranchIn(PrimaryOrderBranches, branchName);

    /// <summary>
    /// Каналы вторички — точки завода, которые в закрытом месяце считаются отдельными регионами (как в «Полевом контроле»: «Урикзор» —
    /// базар, «Сети» — ключевые клиенты): заказы этих точек Linko из филиалов <see cref="PrimaryOrderBranches"/> («Завод», «К К Мерч») в
    /// проданных статусах (<see cref="SoldStatuses"/>: доставлен и отдан), по дате реализации, по строкам. В идущем месяце не учитываются —
    /// подключаются, когда месяц закрыт. В АКБ и ТТ артикула не входят (у канала нет полевой базы ТТ). Регион канала — виртуальный филиал
    /// с отрицательным id (−1, −2 … по порядку списка); направление — по названию: канал из «Настроек продаж» или новое с этим названием.
    /// </summary>
    public List<SalesChannelRegion> ChannelRegions { get; set; } = [];

    private Dictionary<long, (long BranchId, string Name, DateOnly? Since)>? _channelMarkets;

    /// <summary>Регион-канал точки (виртуальный филиал, название и первый месяц) или null — точка не канал.</summary>
    public (long BranchId, string Name, DateOnly? Since)? ChannelOf(long? marketId)
    {
        var map = _channelMarkets ??= ChannelRegions
            .SelectMany((c, i) => c.Markets.Select(m => (Market: m, BranchId: ChannelBranchId(i), c.Name, Since: c.SinceMonth())))
            .GroupBy(x => x.Market)
            .ToDictionary(g => g.Key, g => (g.First().BranchId, g.First().Name.Trim(), g.First().Since));
        return marketId is { } id && map.TryGetValue(id, out var channel) ? channel : null;
    }

    /// <summary>Виртуальный филиал канала с номером index в <see cref="ChannelRegions"/>: −1, −2 …</summary>
    public static long ChannelBranchId(int index) => -(index + 1);

    /// <summary>Строка канала: виртуальный филиал (отрицательный id), не филиал Linko.</summary>
    public static bool IsChannelBranch(long? branchId) => branchId < 0;

    public bool IsPrimaryExcludedMarket(long id, string? name) =>
        PrimaryExcludedMarkets.Any(m => m.Trim() == id.ToString(System.Globalization.CultureInfo.InvariantCulture)
                                        || (name is { } n && string.Equals(m.Trim(), n.Trim(), StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// Склад Linko → регион, в котором он считается: «Коканд бозор» — склад дилера Коканда, «Андижон эски 2», «Андижон эски склад»,
    /// «Жиззах (эски)» — прежние склады Андижона и Жиззаха. Склад без записи — регион со своим названием. Без учёта регистра:
    /// словарь с OrdinalIgnoreCase (привязка настроек его сохраняет), поэтому запись из настроек с тем же складом в другом регистре
    /// заменяет запись из кода, а не добавляется второй. Первичка сводит по нему строки дилеров (<see cref="StockRegionName"/>);
    /// остаток и аутсток — пока по названию склада.
    /// </summary>
    public Dictionary<string, string> StockRegionAliases { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Коканд бозор"] = "Коканд",
        ["Андижон эски 2"] = "Андижон",
        ["Андижон эски склад"] = "Андижон",
        ["Жиззах (эски)"] = "Жиззах",
    };

    /// <summary>
    /// Регион склада: название из <see cref="StockRegionAliases"/>, иначе название самого склада (без пробелов по краям). Ключ ищется
    /// по словарю без учёта регистра; ключи с пробелами по краям (так бывает в настройках) — перебором.
    /// </summary>
    public string StockRegionName(string stockName)
    {
        var name = stockName.Trim();
        var alias = StockRegionAliases.TryGetValue(name, out var exact)
            ? exact
            : StockRegionAliases.FirstOrDefault(a => string.Equals(a.Key.Trim(), name, StringComparison.OrdinalIgnoreCase)).Value;
        return string.IsNullOrWhiteSpace(alias) ? name : alias.Trim();
    }

    /// <summary>
    /// Прайс-лист Linko с ценой, по которой дилер продаёт дальше («Дилердан чикиш нарх»): по нему считается «сумма по цене продажи дилера»
    /// в первичке (количество × максимальная цена прайса). «Сумма (цена дилера)» — цена самого перемещения или заказа (у отгрузок дилерам
    /// это прайс «Дилерга кириш нарх»). Цены завода в Linko нет — «суммы завода» в первичке нет.
    /// </summary>
    public string PrimaryDealerPriceList { get; set; } = "Дилердан чикиш нарх";

    /// <summary>
    /// База скорости продаж для запаса на складах: столько дней до последнего полного дня данных (вчера или последняя приёмка не позже
    /// вчера), вторичка нетто возвратов. 87 — как база «Полевого контроля» (три месяца без первых дней); дни в нуле из аутстока вычитаются.
    /// </summary>
    public int StockVelocityDays { get; set; } = 87;

    /// <summary>
    /// Прайс Linko с входной ценой дилера («Дилерга кириш нарх») — по нему считается стоимость рекомендуемого остатка:
    /// штуки × цена за единицу учёта, последняя по времени строка товара.
    /// </summary>
    public string StockPriceList { get; set; } = "Дилерга кириш нарх";

    public bool IsFactoryStock(string? stockName) =>
        stockName is { } name && FactoryStocks.Any(s => string.Equals(s.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Основная валюта выручки (как Linko пишет её в заказе: «SUM»). Выручку в других валютах курса нет —
    /// она не складывается с основной, а показывается отдельно. Заказ без валюты считается в основной.
    /// </summary>
    public string BaseCurrency { get; set; } = "SUM";

    public bool IsBaseCurrency(string? currency) =>
        string.IsNullOrWhiteSpace(currency) || string.Equals(currency.Trim(), BaseCurrency.Trim(), StringComparison.OrdinalIgnoreCase);

    public bool IsExcludedBranch(string? branchName) => IsBranchIn(ExcludedBranches, branchName);

    public bool IsIgnoredBranch(string? branchName) => IsBranchIn(IgnoredBranches, branchName);

    /// <summary>Исключённые и пропускаемые филиалы в нижнем регистре — для SQL (lower(branch) in …): их нет во вторичке.</summary>
    public string[] NotSecondaryBranchesLower() =>
        ExcludedBranches.Concat(IgnoredBranches).Select(b => b.Trim().ToLowerInvariant()).Distinct().ToArray();

    private static bool IsBranchIn(string[] branches, string? branchName) =>
        branchName is { } name && branches.Any(b => string.Equals(b.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase));

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

/// <summary>Регион-канал вторички (<see cref="SalesOptions.ChannelRegions"/>): название, направление и точки Linko.</summary>
public sealed class SalesChannelRegion
{
    /// <summary>Название региона: «Урикзор», «Сети».</summary>
    public string Name { get; set; } = "";

    /// <summary>Направление (канал): «Базар», «КА».</summary>
    public string Direction { get; set; } = "";

    /// <summary>Подпись направления, если его нет в «Настройках продаж»: «Ключевые клиенты».</summary>
    public string? Description { get; set; }

    /// <summary>Id точек Linko.</summary>
    public long[] Markets { get; set; } = [];

    /// <summary>
    /// С какого месяца канал считается («2026-08»): продажи раньше в отчёт не входят — как в «Полевом контроле», где «Урикзор»
    /// начинается с августа 2026. Пусто — с начала данных.
    /// </summary>
    public string? Since { get; set; }

    /// <summary>Первый день месяца <see cref="Since"/>; null — не задан или не в формате «гггг-ММ».</summary>
    public DateOnly? SinceMonth() =>
        DateOnly.TryParseExact($"{Since?.Trim()}-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var month) ? month : null;
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
    public decimal SmallCheckOfMedian { get; set; } = 0.50m;

    /// <summary>...при конверсии не ниже этой...</summary>
    public decimal SmallCheckMinConversion { get; set; } = 0.25m;

    /// <summary>...и не меньше этого числа заказов.</summary>
    public int SmallCheckMinOrders { get; set; } = 20;

    /// <summary>«Узкий ассортимент» (критично): категорий не больше этого числа. Риск — на одну меньше цели.</summary>
    public int NarrowAssortmentCritical { get; set; } = 2;

    public decimal TempoCritical { get; set; } = 0.75m;
    public decimal TempoRisk { get; set; } = 0.90m;
}
