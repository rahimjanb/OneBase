using System.Security.Cryptography;
using System.Text;
using OneBase.Finance.Integration.Worker.OneC;

namespace OneBase.Finance.Integration.Worker.Mock;

/// <summary>
/// Справочники тестового источника: вымышленные организации и контрагенты (ИНН — не настоящие), план счетов НСБУ №21 (подмножество),
/// номенклатура кондитерского производства. Идентификаторы — стабильные UUID из названия: один и тот же набор при каждом запуске.
/// </summary>
internal static class MockCatalog
{
    public const string InfobaseId = "mock-1c-demo-infobase-0001";
    public const string InfobaseName = "Демо: Бухгалтерия для Узбекистана (тестовый источник OneBase)";

    public static string Id(string key) => new Guid(MD5.HashData(Encoding.UTF8.GetBytes("onebase-mock:" + key))).ToString();

    public sealed record Org(int Index, string Id, string Code, string Name, string Inn, double Volume);

    public static readonly Org[] Organizations =
    [
        new(0, Id("org:0"), "000000001", "ООО «Демо Кондитер»", "301234567", 1.0),
        new(1, Id("org:1"), "000000002", "ООО «Демо Дистрибуция»", "302345678", 0.45),
    ];

    public sealed record Account(string Code, string Name, string? Parent, string Type, bool Currency, bool Quantity, (string Type, bool TurnoverOnly)[] Subconto);

    public const string SubCounterparty = "Контрагенты";
    public const string SubContract = "Договоры";
    public const string SubItem = "Номенклатура";
    public const string SubWarehouse = "Склады";
    public const string SubDepartment = "Подразделения";
    public const string SubBankAccount = "БанковскиеСчета";
    public const string SubCashFlow = "СтатьиДвиженияДенежныхСредств";
    public const string SubCostItem = "СтатьиЗатрат";

    private static readonly (string, bool)[] CpContract = [(SubCounterparty, false), (SubContract, false)];
    private static readonly (string, bool)[] ItemWarehouse = [(SubItem, false), (SubWarehouse, false)];

    public static readonly Account[] Accounts =
    [
        new("0100", "Основные средства", null, "active", false, false, []),
        new("0200", "Износ основных средств", null, "passive", false, false, []),
        new("1000", "Материалы", null, "active", false, true, ItemWarehouse),
        new("1010", "Сырьё и материалы", "1000", "active", false, true, ItemWarehouse),
        new("2000", "Основное производство", null, "active", false, false, [(SubDepartment, false)]),
        new("2010", "Основное производство", "2000", "active", false, false, [(SubDepartment, false)]),
        new("2800", "Готовая продукция", null, "active", false, true, ItemWarehouse),
        new("2810", "Готовая продукция на складе", "2800", "active", false, true, ItemWarehouse),
        new("2900", "Товары", null, "active", false, true, ItemWarehouse),
        new("2910", "Товары на складе", "2900", "active", false, true, ItemWarehouse),
        new("4000", "Счета к получению", null, "active", true, false, CpContract),
        new("4010", "Счета к получению от покупателей и заказчиков", "4000", "active", true, false, CpContract),
        new("4300", "Авансы выданные", null, "active", true, false, CpContract),
        new("4310", "Авансы, выданные поставщикам и подрядчикам", "4300", "active", true, false, CpContract),
        new("4400", "Авансовые платежи по налогам", null, "active", false, false, []),
        new("4410", "Авансовые платежи по налогам и сборам в бюджет (НДС к зачёту)", "4400", "active", false, false, []),
        new("5000", "Денежные средства в кассе", null, "active", false, false, [(SubCashFlow, true)]),
        new("5010", "Денежные средства в кассе в национальной валюте", "5000", "active", false, false, [(SubCashFlow, true)]),
        new("5100", "Денежные средства на расчётном счёте", null, "active", false, false, [(SubBankAccount, false), (SubCashFlow, true)]),
        new("5110", "Расчётный счёт", "5100", "active", false, false, [(SubBankAccount, false), (SubCashFlow, true)]),
        new("5200", "Денежные средства в иностранной валюте", null, "active", true, false, [(SubBankAccount, false), (SubCashFlow, true)]),
        new("5210", "Валютные счета внутри страны", "5200", "active", true, false, [(SubBankAccount, false), (SubCashFlow, true)]),
        new("6000", "Счета к оплате", null, "passive", true, false, CpContract),
        new("6010", "Счета к оплате поставщикам и подрядчикам", "6000", "passive", true, false, CpContract),
        new("6300", "Авансы полученные", null, "passive", true, false, CpContract),
        new("6310", "Авансы, полученные от покупателей и заказчиков", "6300", "passive", true, false, CpContract),
        new("6400", "Задолженность по платежам в бюджет", null, "passive", false, false, []),
        new("6410", "Задолженность по платежам в бюджет (по видам)", "6400", "passive", false, false, []),
        new("6500", "Задолженность по страхованию и целевым фондам", null, "passive", false, false, []),
        new("6520", "Платежи в государственные целевые фонды", "6500", "passive", false, false, []),
        new("6600", "Задолженность участникам", null, "passive", false, false, []),
        new("6610", "Задолженность по дивидендам", "6600", "passive", false, false, []),
        new("6700", "Расчёты с персоналом по оплате труда", null, "passive", false, false, []),
        new("6710", "Расчёты с персоналом по оплате труда", "6700", "passive", false, false, []),
        new("6800", "Краткосрочные банковские кредиты", null, "passive", false, false, [(SubCounterparty, false)]),
        new("6810", "Краткосрочные банковские кредиты", "6800", "passive", false, false, [(SubCounterparty, false)]),
        new("8300", "Уставный капитал", null, "passive", false, false, []),
        new("8330", "Паи и доли", "8300", "passive", false, false, []),
        new("8700", "Нераспределённая прибыль", null, "active_passive", false, false, []),
        new("8710", "Нераспределённая прибыль (непокрытый убыток) отчётного периода", "8700", "active_passive", false, false, []),
        new("9000", "Доходы от основной деятельности", null, "passive", false, false, [(SubItem, false)]),
        new("9010", "Доходы от реализации готовой продукции", "9000", "passive", false, false, [(SubItem, false)]),
        new("9020", "Доходы от реализации товаров", "9000", "passive", false, false, [(SubItem, false)]),
        new("9040", "Возврат проданных товаров", "9000", "active", false, false, [(SubItem, false)]),
        new("9100", "Себестоимость реализованной продукции", null, "active", false, false, [(SubItem, false)]),
        new("9110", "Себестоимость реализованной готовой продукции", "9100", "active", false, false, [(SubItem, false)]),
        new("9120", "Себестоимость реализованных товаров", "9100", "active", false, false, [(SubItem, false)]),
        new("9400", "Расходы периода", null, "active", false, false, [(SubCostItem, false)]),
        new("9410", "Расходы по реализации", "9400", "active", false, false, [(SubCostItem, false)]),
        new("9420", "Административные расходы", "9400", "active", false, false, [(SubCostItem, false)]),
        new("9430", "Прочие операционные расходы", "9400", "active", false, false, [(SubCostItem, false)]),
        new("9500", "Доходы от финансовой деятельности", null, "passive", false, false, []),
        new("9540", "Доходы от валютных курсовых разниц", "9500", "passive", false, false, []),
        new("9600", "Расходы по финансовой деятельности", null, "active", false, false, []),
        new("9610", "Расходы в виде процентов", "9600", "active", false, false, []),
        new("9620", "Убытки от валютных курсовых разниц", "9600", "active", false, false, []),
        new("9800", "Расходы по налогу на прибыль", null, "active", false, false, []),
        new("9810", "Расходы по налогу на прибыль", "9800", "active", false, false, []),
        new("9900", "Конечный финансовый результат", null, "active_passive", false, false, []),
        new("9910", "Конечный финансовый результат", "9900", "active_passive", false, false, []),
    ];

    public static Account AccountOf(string code) => Accounts.First(a => a.Code == code);

    public sealed record Currency(string Code, string Numeric, string Name, decimal Base, decimal Drift, int Multiplier);

    /// <summary>Курсы — правдоподобные, плавно меняются день ко дню (не данные ЦБ).</summary>
    public static readonly Currency[] Currencies =
    [
        new("UZS", "860", "Узбекский сум", 1, 0, 1),
        new("USD", "840", "Доллар США", 12_350, 0.00018m, 1),
        new("EUR", "978", "Евро", 13_400, 0.00015m, 1),
        new("RUB", "643", "Российский рубль", 138, 0.0001m, 1),
    ];

    public sealed record Party(string Id, string Code, string Name, string Inn, string Kind, string Currency, int TermDays, double Weight, bool LatePayer, int[] Orgs);

    private static readonly string[] CustomerNames =
    [
        "ООО «Ширин Савдо»", "ЧП «Нур Маркет»", "ООО «Самарканд Таъминот»", "ООО «Бухоро Дистрибьютер»", "ЧП «Фаргона Ширинлик»", "ООО «Андижон Савдо Маркази»",
        "ООО «Наманган Опт»", "ЧП «Хоразм Тижорат»", "ООО «Карши Кондитер Савдо»", "ООО «Термиз Таъминот»", "ЧП «Жиззах Маркет»", "ООО «Навои Опт Савдо»",
        "ООО «Ташкент Сити Ритейл»", "ЧП «Чирчик Савдо»", "ООО «Ангрен Таъминот»", "ООО «Гулистон Опт»", "ЧП «Нукус Ширинликлари»", "ООО «Коканд Савдо»",
        "ООО «Маргилан Маркет»", "ЧП «Олмалик Дистрибьюшн»", "ООО «Зарафшон Савдо»", "ЧП «Денов Опт»", "ООО «Ургенч Таъминот»", "ООО «Kazakh Sweet Trade»",
    ];

    private static readonly string[] SupplierNames =
    [
        "ООО «Шакар Импорт»", "ООО «Какао Трейд Азия»", "ООО «Ун Комбинат»", "ООО «Ёг-Мой Савдо»", "ООО «Упаковка Пласт»", "ООО «Картон Принт»",
        "ООО «Ароматика»", "ООО «Сут Маҳсулотлари»", "ООО «Энерго Сервис»", "ООО «Логистик Транс»", "ООО «Бизнес Центр Аренда»", "ООО «Реклама Медиа»",
        "АКБ «Демо Банк»",
    ];

    public static readonly Party[] Customers = CustomerNames.Select((name, i) => new Party(
        Id($"cp:customer:{i}"), $"C{i + 1:000}", name, $"{300_000_000 + i * 1_234_567}", "customer",
        i == CustomerNames.Length - 1 ? "USD" : "UZS", i % 4 == 0 ? 45 : i % 3 == 0 ? 14 : 30,
        1.0 + (i % 5) * 0.4, i % 6 == 5, i % 3 == 2 ? [0, 1] : [0])).ToArray();

    public static readonly Party[] Suppliers = SupplierNames.Select((name, i) => new Party(
        Id($"cp:supplier:{i}"), $"S{i + 1:000}", name, $"{200_000_000 + i * 7_654_321}", "supplier",
        i == 1 ? "USD" : "UZS", i < 8 ? 20 : 10, 1, false, [0, 1])).ToArray();

    public static Party Landlord => Suppliers[10];
    public static Party Utility => Suppliers[8];
    public static Party Logistics => Suppliers[9];
    public static Party Advertising => Suppliers[11];
    public static Party Bank => Suppliers[12];

    public static string ContractId(Party p, int org) => Id($"contract:{p.Id}:{org}");

    public sealed record Ref(string Id, string Code, string Name, string Kind, int? Org = null);

    public static readonly Ref[] Warehouses =
    [
        new(Id("wh:raw"), "W01", "Склад сырья и материалов", "warehouse"),
        new(Id("wh:fg"), "W02", "Склад готовой продукции", "warehouse"),
        new(Id("wh:sam"), "W03", "Склад филиала Самарканд", "warehouse"),
    ];

    public static Ref RawWarehouse => Warehouses[0];
    public static Ref FinishedWarehouse => Warehouses[1];

    public static readonly Ref[] Departments =
    [
        .. Organizations.SelectMany(o => new[]
        {
            new Ref(Id($"dept:{o.Index}:prod"), "D01", "Производство", "department", o.Index),
            new Ref(Id($"dept:{o.Index}:sales"), "D02", "Отдел продаж", "department", o.Index),
            new Ref(Id($"dept:{o.Index}:admin"), "D03", "Администрация", "department", o.Index),
        }),
    ];

    public static Ref Department(int org, string key) => Departments.First(d => d.Org == org && d.Id == Id($"dept:{org}:{key}"));

    public static readonly Ref[] CashFlowItems =
    [
        new(Id("cfi:customers"), "CF01", "Поступления от покупателей", "cash_flow_items"),
        new(Id("cfi:suppliers"), "CF02", "Оплата поставщикам", "cash_flow_items"),
        new(Id("cfi:payroll"), "CF03", "Заработная плата", "cash_flow_items"),
        new(Id("cfi:taxes"), "CF04", "Налоги и сборы", "cash_flow_items"),
        new(Id("cfi:transfer"), "CF05", "Перемещение денежных средств", "cash_flow_items"),
        new(Id("cfi:other"), "CF06", "Прочие расходы", "cash_flow_items"),
    ];

    public static Ref CashFlow(string key) => CashFlowItems.First(c => c.Id == Id($"cfi:{key}"));

    public static readonly Ref[] CostItems =
    [
        new(Id("cost:rent"), "Z01", "Аренда", "cost_items"),
        new(Id("cost:utilities"), "Z02", "Коммунальные услуги", "cost_items"),
        new(Id("cost:transport"), "Z03", "Транспортные расходы", "cost_items"),
        new(Id("cost:ads"), "Z04", "Реклама и маркетинг", "cost_items"),
        new(Id("cost:payroll"), "Z05", "Оплата труда", "cost_items"),
        new(Id("cost:office"), "Z06", "Канцелярские и хозяйственные", "cost_items"),
        new(Id("cost:bank"), "Z07", "Услуги банка", "cost_items"),
    ];

    public static Ref Cost(string key) => CostItems.First(c => c.Id == Id($"cost:{key}"));

    public static Ref BankAccount(int org, string currency) => BankAccounts.First(b => b.Org == org && b.Id == Id($"bank:{org}:{currency}"));

    public static readonly Ref[] BankAccounts =
    [
        .. Organizations.SelectMany(o => new[]
        {
            new Ref(Id($"bank:{o.Index}:UZS"), "B01", $"Р/с 2020 8000 {o.Index}00 1234 5678 (UZS)", "bank_accounts", o.Index),
            new Ref(Id($"bank:{o.Index}:USD"), "B02", $"В/с 2020 8840 {o.Index}00 8765 4321 (USD)", "bank_accounts", o.Index),
        }),
    ];

    public static readonly Ref[] CashDesks = [.. Organizations.Select(o => new Ref(Id($"cash:{o.Index}"), "K01", "Основная касса", "cash_desks", o.Index))];

    public sealed record Product(string Id, string Code, string Name, string Kind, string Unit, decimal Price, decimal Cost);

    /// <summary>Готовая продукция (цена без НДС за единицу, себестоимость) и сырьё (цена закупки).</summary>
    public static readonly Product[] Products =
    [
        new(Id("item:p1"), "P001", "Конфеты «Ширин» ассорти, 1 кг", "product", "кг", 46_000, 29_500),
        new(Id("item:p2"), "P002", "Конфеты «Бахор» шоколадные, 1 кг", "product", "кг", 58_000, 37_800),
        new(Id("item:p3"), "P003", "Карамель «Лимонная», 1 кг", "product", "кг", 27_000, 15_900),
        new(Id("item:p4"), "P004", "Помадка «Сливочная», 0,42 кг", "product", "шт", 14_500, 8_700),
        new(Id("item:p5"), "P005", "Печенье «Чай вақти», 0,5 кг", "product", "шт", 12_800, 7_400),
        new(Id("item:p6"), "P006", "Вафли «Лесной орех», 0,3 кг", "product", "шт", 9_900, 5_600),
        new(Id("item:p7"), "P007", "Зефир ванильный, 1 кг", "product", "кг", 39_000, 24_200),
        new(Id("item:p8"), "P008", "Мармелад фруктовый, 1 кг", "product", "кг", 33_500, 19_800),
        new(Id("item:p9"), "P009", "Шоколадная плитка 90 г", "product", "шт", 8_900, 5_300),
        new(Id("item:p10"), "P010", "Набор конфет подарочный, 0,5 кг", "product", "шт", 64_000, 38_000),
    ];

    public static readonly Product[] Materials =
    [
        new(Id("item:m1"), "M001", "Сахар-песок", "material", "кг", 0, 9_600),
        new(Id("item:m2"), "M002", "Какао-порошок", "material", "кг", 0, 72_000),
        new(Id("item:m3"), "M003", "Мука пшеничная в/с", "material", "кг", 0, 6_100),
        new(Id("item:m4"), "M004", "Масло растительное", "material", "кг", 0, 21_000),
        new(Id("item:m5"), "M005", "Патока крахмальная", "material", "кг", 0, 11_500),
        new(Id("item:m6"), "M006", "Плёнка упаковочная", "material", "кг", 0, 34_000),
        new(Id("item:m7"), "M007", "Коробка картонная", "material", "шт", 0, 2_300),
        new(Id("item:m8"), "M008", "Ароматизатор «Ваниль»", "material", "кг", 0, 155_000),
        new(Id("item:m9"), "M009", "Молоко сухое", "material", "кг", 0, 48_000),
    ];

    /// <summary>Поставщик материала (индекс в Suppliers).</summary>
    public static int SupplierOf(int material) => material switch
    {
        0 => 0, 1 => 1, 2 => 2, 3 => 3, 4 => 0, 5 => 4, 6 => 5, 7 => 6, _ => 7,
    };
}

internal static class Mock
{
    /// <summary>Округление до step (100, 1000…): у decimal нет Math.Round с отрицательным числом знаков.</summary>
    public static decimal RoundTo(decimal value, decimal step) => Math.Round(value / step, MidpointRounding.AwayFromZero) * step;
}
