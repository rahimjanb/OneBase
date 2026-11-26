namespace OneBase.Finance.Integration.Contracts;

/// <summary>
/// Типы сущностей 1С в OneBase (<c>SourceEntityType</c>). Вместе с организацией и идентификатором 1С образуют ключ записи:
/// повторная загрузка той же записи обновляет её, а не добавляет дубликат.
/// </summary>
public static class OneCEntityTypes
{
    public const string SourceSystem = "1C";

    public const string Organization = "organization";
    public const string Account = "account";
    public const string Counterparty = "counterparty";
    public const string Contract = "contract";
    public const string Currency = "currency";
    public const string ExchangeRate = "exchange_rate";
    public const string Item = "item";
    public const string Document = "document";
    public const string Entry = "entry";
    public const string Balance = "balance";

    /// <summary>Справочники-«виды»: склады, подразделения, статьи ДДС и затрат, кассы, банковские счета, партии…</summary>
    public const string Warehouse = "warehouse";
    public const string Department = "department";

    /// <summary>Прочие справочники из субконто: тип записи — <c>reference:&lt;вид&gt;</c>.</summary>
    public const string ReferencePrefix = "reference:";

    /// <summary>Общий справочник информационной базы (план счетов, валюты, контрагенты…): организация не указывается.</summary>
    public const string Shared = "";

    public static readonly IReadOnlyList<string> ReferenceKinds =
        ["cash_flow_items", "cost_items", "bank_accounts", "cash_desks", "batches", "fixed_assets", "other_income_expense"];

    public static string Reference(string kind) => ReferencePrefix + kind;

    /// <summary>Ключ курса: ВАЛЮТА:ГГГГ-ММ-ДД.</summary>
    public static string RateKey(string currency, DateOnly date) => $"{currency.ToUpperInvariant()}:{date:yyyy-MM-dd}";

    /// <summary>Ключ проводки: документ-регистратор и номер строки набора.</summary>
    public static string EntryKey(string registrarId, int lineNumber) => $"{registrarId}:{lineNumber}";
}

/// <summary>Типы документов 1С в OneBase (поле <c>type</c> спецификации API).</summary>
public static class OneCDocumentTypes
{
    public const string Sale = "sale";
    public const string SaleReturn = "sale_return";
    public const string Purchase = "purchase";
    public const string PurchaseReturn = "purchase_return";
    public const string InvoiceOut = "invoice_out";
    public const string InvoiceIn = "invoice_in";
    public const string CashIn = "cash_in";
    public const string CashOut = "cash_out";
    public const string BankIn = "bank_in";
    public const string BankOut = "bank_out";
    public const string Production = "production";
    public const string MaterialIssue = "material_issue";
    public const string Transfer = "transfer";
    public const string WriteOff = "write_off";
    public const string Inventory = "inventory";
    public const string Manual = "manual";
    public const string Other = "other";

    /// <summary>Заказ покупателя / заказ поставщику (v1.1): документы без проводок.</summary>
    public const string OrderCustomer = "order_customer";
    public const string OrderSupplier = "order_supplier";

    public static readonly IReadOnlyList<string> All =
    [
        Sale, SaleReturn, Purchase, PurchaseReturn, InvoiceOut, InvoiceIn, CashIn, CashOut, BankIn, BankOut,
        Production, MaterialIssue, Transfer, WriteOff, Inventory, Manual, Other, OrderCustomer, OrderSupplier,
    ];

    public static readonly IReadOnlyList<string> Sales = [Sale, SaleReturn];
    public static readonly IReadOnlyList<string> Purchases = [Purchase, PurchaseReturn];
    public static readonly IReadOnlyList<string> Cash = [CashIn, CashOut];
    public static readonly IReadOnlyList<string> Bank = [BankIn, BankOut];
    public static readonly IReadOnlyList<string> ProductionTypes = [Production, MaterialIssue];

    public static bool IsKnown(string? type) => type is not null && All.Contains(type);
}

/// <summary>
/// Модули загрузки 1С: администратор включает и выключает их на странице «1С». Модуль документов — набор типов документов; «Бухгалтерия» —
/// проводки и оборотно-сальдовая ведомость (из них строятся отчёты «Финансов»). Справочники и курсы загружаются всегда.
/// </summary>
public static class OneCModules
{
    public const string Sales = "sales";
    public const string Purchases = "purchases";
    public const string Warehouse = "warehouse";
    public const string Money = "money";
    public const string Production = "production";
    public const string Accounting = "accounting";
    public const string Other = "other";

    public static readonly IReadOnlyList<string> All = [Sales, Purchases, Warehouse, Money, Production, Accounting, Other];

    /// <summary>Типы документов модуля (у «Бухгалтерии» — ручные операции; проводки всех документов она включает отдельно).</summary>
    public static IReadOnlyList<string> TypesOf(string module) => module switch
    {
        Sales => [OneCDocumentTypes.Sale, OneCDocumentTypes.SaleReturn, OneCDocumentTypes.InvoiceOut, OneCDocumentTypes.OrderCustomer],
        Purchases => [OneCDocumentTypes.Purchase, OneCDocumentTypes.PurchaseReturn, OneCDocumentTypes.InvoiceIn, OneCDocumentTypes.OrderSupplier],
        Warehouse => [OneCDocumentTypes.Transfer, OneCDocumentTypes.WriteOff, OneCDocumentTypes.Inventory],
        Money => [OneCDocumentTypes.CashIn, OneCDocumentTypes.CashOut, OneCDocumentTypes.BankIn, OneCDocumentTypes.BankOut],
        Production => [OneCDocumentTypes.Production, OneCDocumentTypes.MaterialIssue],
        Accounting => [OneCDocumentTypes.Manual],
        Other => [OneCDocumentTypes.Other],
        _ => [],
    };

    /// <summary>Модуль типа документа (неизвестный тип — «Прочие»).</summary>
    public static string Of(string? documentType) =>
        All.FirstOrDefault(m => TypesOf(m).Contains(documentType ?? "")) ?? Other;

    /// <summary>Включённые модули: пусто — все; неизвестные коды отбрасываются.</summary>
    public static IReadOnlyList<string> Normalize(IEnumerable<string>? modules)
    {
        var list = (modules ?? []).Select(m => m.Trim().ToLowerInvariant()).Where(All.Contains).Distinct().ToList();
        return list.Count == 0 ? All : All.Where(list.Contains).ToList();
    }

    /// <summary>Тип документа загружается при этих модулях.</summary>
    public static bool Allows(IReadOnlyList<string> enabled, string? documentType) => enabled.Count == 0 || enabled.Contains(Of(documentType));
}

/// <summary>Потоки синхронизации: курсор каждого хранится в OneBase и сдвигается только вместе с записью пакета.</summary>
public static class OneCStreams
{
    /// <summary>Изменения 1С (план обмена): курсор — номер последнего сохранённого изменения.</summary>
    public const string Changes = "changes";

    /// <summary>
    /// Сверка версий (изменения только чтением, /versions): «versions:&lt;тип&gt;» — шаг журнала; курсор «versions:full» — день последней
    /// полной ночной сверки (ГГГГ-ММ-ДД).
    /// </summary>
    public const string VersionsPrefix = "versions:";

    public const string VersionsFull = "versions:full";

    /// <summary>Первичная загрузка справочника: «ref:&lt;тип&gt;», курсор — страница; по окончании — <see cref="Done"/>.</summary>
    public const string ReferencePrefix = "ref:";

    /// <summary>Курсор справочника, загруженного целиком.</summary>
    public const string Done = "done";

    /// <summary>Остатки и обороты за месяц: снимок целиком.</summary>
    public const string Balances = "balances";

    /// <summary>Историческая загрузка: «history:&lt;поток&gt;», положение хранится в задании загрузки.</summary>
    public const string HistoryPrefix = "history:";

    /// <summary>Справочники в порядке загрузки: организации и план счетов — первыми.</summary>
    public static readonly IReadOnlyList<string> ReferenceOrder =
    [
        OneCEntityTypes.Organization, OneCEntityTypes.Account, OneCEntityTypes.Currency, OneCEntityTypes.Counterparty,
        OneCEntityTypes.Contract, OneCEntityTypes.Warehouse, OneCEntityTypes.Department, OneCEntityTypes.Item,
        .. OneCEntityTypes.ReferenceKinds.Select(OneCEntityTypes.Reference),
    ];

    /// <summary>Потоки исторической загрузки за месяц.</summary>
    public const string HistoryRates = "rates";
    public const string HistoryDocuments = "documents";
    public const string HistoryEntries = "entries";
    public const string HistoryBalances = "balances";

    public static readonly IReadOnlyList<string> HistoryOrder = [HistoryRates, HistoryDocuments, HistoryEntries, HistoryBalances];

    public static string Reference(string entityType) => ReferencePrefix + entityType;
}
