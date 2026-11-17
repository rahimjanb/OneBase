namespace OneBase.Finance.Integration.Contracts;

// Нормализованные записи 1С — то, что служба интеграции передаёт во внутренний API OneBase после проверки.
// Идентификаторы — строки 1С (UUID ссылки), суммы — как в 1С, без пересчёта. Неизвестные OneBase реквизиты не теряются: Attributes.

/// <summary>Общие поля записи источника.</summary>
public abstract record ImportRecord
{
    /// <summary>Идентификатор 1С (UUID ссылки; у курса — ВАЛЮТА:ДАТА, у проводки — регистратор:строка).</summary>
    public required string SourceId { get; init; }

    /// <summary>Версия данных 1С (ВерсияДанных): по ней видно, что запись изменилась.</summary>
    public string? Version { get; init; }

    /// <summary>Когда запись изменена в 1С (если 1С это хранит).</summary>
    public DateTimeOffset? ModifiedAt { get; init; }

    /// <summary>Пометка удаления в 1С или объект удалён.</summary>
    public bool Deleted { get; init; }
}

public sealed record ImportOrganization : ImportRecord
{
    public string? Code { get; init; }
    public required string Name { get; init; }
    public string? FullName { get; init; }
    public string? Inn { get; init; }
    public string? VatCode { get; init; }
    public string AccountingCurrency { get; init; } = "UZS";
    public bool IsActive { get; init; } = true;
}

public sealed record ImportSubcontoType
{
    public int Index { get; init; }
    public required string Type { get; init; }
    public bool TurnoverOnly { get; init; }
}

public sealed record ImportAccount : ImportRecord
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string? ParentCode { get; init; }

    /// <summary>active / passive / active_passive.</summary>
    public string Type { get; init; } = "active_passive";

    public bool OffBalance { get; init; }
    public bool IsCurrency { get; init; }
    public bool IsQuantity { get; init; }
    public List<ImportSubcontoType> Subconto { get; init; } = [];
}

public sealed record ImportBankAccount
{
    public string? Account { get; init; }
    public string? Mfo { get; init; }
    public string? Bank { get; init; }
    public string? Currency { get; init; }
}

public sealed record ImportCounterparty : ImportRecord
{
    public string? Code { get; init; }
    public required string Name { get; init; }
    public string? FullName { get; init; }
    public string? Inn { get; init; }

    /// <summary>customer / supplier / both / other.</summary>
    public string? Kind { get; init; }

    /// <summary>legal / individual / entrepreneur.</summary>
    public string? LegalForm { get; init; }

    public string? ParentId { get; init; }
    public bool IsGroup { get; init; }
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public List<ImportBankAccount> BankAccounts { get; init; } = [];
}

public sealed record ImportContract : ImportRecord
{
    public required string OrganizationId { get; init; }
    public required string CounterpartyId { get; init; }
    public string? Code { get; init; }
    public required string Name { get; init; }
    public string? Number { get; init; }
    public DateOnly? Date { get; init; }

    /// <summary>with_customer / with_supplier / other.</summary>
    public string Kind { get; init; } = "other";

    public string Currency { get; init; } = "UZS";
    public int? PaymentTermDays { get; init; }
    public DateOnly? ValidTo { get; init; }
}

public sealed record ImportCurrency : ImportRecord
{
    /// <summary>Буквенный код ISO 4217.</summary>
    public required string Code { get; init; }

    public string? NumericCode { get; init; }
    public required string Name { get; init; }
}

public sealed record ImportExchangeRate : ImportRecord
{
    public required string Currency { get; init; }
    public DateOnly Date { get; init; }

    /// <summary>Курс за <see cref="Multiplier"/> единиц валюты в валюте учёта.</summary>
    public decimal Rate { get; init; }

    public int Multiplier { get; init; } = 1;
    public string? Source { get; init; }
}

/// <summary>Склад, подразделение и прочие справочники из субконто (статьи ДДС и затрат, кассы, банковские счета, партии…).</summary>
public sealed record ImportReference : ImportRecord
{
    /// <summary>Тип записи: warehouse, department или reference:&lt;вид&gt;.</summary>
    public required string Kind { get; init; }

    public string? OrganizationId { get; init; }
    public string? Code { get; init; }
    public required string Name { get; init; }
    public string? ParentId { get; init; }
    public bool IsGroup { get; init; }
    public Dictionary<string, string?>? Attributes { get; init; }
}

public sealed record ImportItem : ImportRecord
{
    public string? Code { get; init; }
    public required string Name { get; init; }
    public string? FullName { get; init; }
    public string? Article { get; init; }

    /// <summary>product / material / goods / service / semi_finished / other.</summary>
    public string Kind { get; init; } = "other";

    public string? Unit { get; init; }
    public string? ParentId { get; init; }
    public bool IsGroup { get; init; }
    public decimal? VatRate { get; init; }
    public string? Ikpu { get; init; }
    public string? Barcode { get; init; }
}

/// <summary>Курс, по которому 1С пересчитала сумму в валюту учёта.</summary>
public sealed record ImportRate
{
    public decimal Value { get; init; }
    public int Multiplier { get; init; } = 1;
    public DateOnly? Date { get; init; }
    public string? Source { get; init; }
}

public sealed record ImportDocumentLine
{
    public int LineNumber { get; init; }
    public string? ItemId { get; init; }
    public decimal Quantity { get; init; }
    public string? Unit { get; init; }
    public decimal? Price { get; init; }
    public decimal Amount { get; init; }
    public decimal? VatRate { get; init; }
    public decimal? VatAmount { get; init; }
    public decimal? CostAmount { get; init; }
    public string? Account { get; init; }
    public string? WarehouseId { get; init; }
    public string? BatchId { get; init; }
}

public sealed record ImportDocument : ImportRecord
{
    public required string OrganizationId { get; init; }

    /// <summary>Код типа (<see cref="OneCDocumentTypes"/>).</summary>
    public required string Type { get; init; }

    /// <summary>Имя документа в конфигурации 1С.</summary>
    public required string SourceType { get; init; }

    public required string Number { get; init; }
    public DateTimeOffset Date { get; init; }
    public bool Posted { get; init; }
    public string? CounterpartyId { get; init; }
    public string? ContractId { get; init; }
    public string? WarehouseId { get; init; }
    public string? DepartmentId { get; init; }
    public string Currency { get; init; } = "UZS";
    public decimal Amount { get; init; }
    public decimal? VatAmount { get; init; }
    public decimal AmountAccounting { get; init; }
    public ImportRate? Rate { get; init; }
    public DateOnly? DueDate { get; init; }
    public string? BasisDocumentId { get; init; }
    public string? CashFlowItemId { get; init; }
    public string? BankAccountId { get; init; }
    public string? CashDeskId { get; init; }
    public string? Purpose { get; init; }
    public string? Comment { get; init; }
    public List<ImportDocumentLine> Lines { get; init; } = [];
}

/// <summary>Значение субконто проводки или строки остатков: вид, ссылка, код и наименование — ничего не теряется.</summary>
public sealed record ImportSubconto
{
    public int Index { get; init; }
    public required string Type { get; init; }
    public string? Id { get; init; }
    public string? Code { get; init; }
    public string? Name { get; init; }
}

public sealed record ImportEntrySide
{
    public required string Account { get; init; }
    public List<ImportSubconto> Subconto { get; init; } = [];
    public string? Currency { get; init; }
    public decimal? CurrencyAmount { get; init; }
    public decimal? Quantity { get; init; }
}

public sealed record ImportEntry
{
    public int LineNumber { get; init; }
    public DateTimeOffset Period { get; init; }
    public bool Active { get; init; } = true;

    /// <summary>Сумма в валюте учёта.</summary>
    public decimal Amount { get; init; }

    public string? Content { get; init; }
    public string? DepartmentId { get; init; }
    public required ImportEntrySide Debit { get; init; }
    public required ImportEntrySide Credit { get; init; }
    public ImportRate? Rate { get; init; }
}

/// <summary>
/// Все проводки одного документа-регистратора. Набор заменяет прежние проводки документа целиком: перепроведение приходит новым набором,
/// отмена проведения — пустым набором. <see cref="ImportRecord.SourceId"/> — идентификатор регистратора.
/// </summary>
public sealed record ImportEntrySet : ImportRecord
{
    public required string OrganizationId { get; init; }
    public required string RegistrarType { get; init; }
    public string? RegistrarSourceType { get; init; }
    public string? RegistrarNumber { get; init; }
    public DateTimeOffset? RegistrarDate { get; init; }
    public List<ImportEntry> Entries { get; init; } = [];
}

public sealed record ImportBalanceAmounts
{
    public decimal Debit { get; init; }
    public decimal Credit { get; init; }
    public decimal? CurrencyDebit { get; init; }
    public decimal? CurrencyCredit { get; init; }
    public decimal? QuantityDebit { get; init; }
    public decimal? QuantityCredit { get; init; }
}

/// <summary>Строка оборотно-сальдовой ведомости за месяц: счёт, субконто, валюта; сальдо на начало, обороты, сальдо на конец.</summary>
public sealed record ImportBalance
{
    public required string OrganizationId { get; init; }

    /// <summary>Первое число месяца.</summary>
    public DateOnly Period { get; init; }

    public required string Account { get; init; }
    public List<ImportSubconto> Subconto { get; init; } = [];
    public string? Currency { get; init; }
    public ImportBalanceAmounts Opening { get; init; } = new();
    public ImportBalanceAmounts Turnover { get; init; } = new();
    public ImportBalanceAmounts Closing { get; init; } = new();
}

/// <summary>Объект физически удалён в 1С (из журнала изменений).</summary>
public sealed record ImportDeletion
{
    public required string EntityType { get; init; }
    public required string SourceId { get; init; }
    public string? OrganizationId { get; init; }
}
