namespace OneBase.Domain.OneC;

// Данные 1С:Бухгалтерии в OneBase (схема onec) — факты источника. Пишет их только загрузка из 1С (OneCImportService): правки
// в OneBase, расчёты, планы и прогнозы сюда не записываются. Исправление в 1С приходит новой версией записи; удаление и отмена
// проведения помечают запись (IsDeleted), история изменений — в OneCChange.

/// <summary>
/// Общие поля записи источника. Ключ — SourceSystem + SourceOrganizationId + SourceEntityType + SourceEntityId (уникальный индекс):
/// повторная загрузка той же записи обновляет её, а не создаёт дубликат.
/// </summary>
public abstract class OneCRecord
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Всегда «1C».</summary>
    public string SourceSystem { get; set; } = "1C";

    /// <summary>Организация 1С (UUID); "" — общий справочник информационной базы (план счетов, валюты, контрагенты…).</summary>
    public string SourceOrganizationId { get; set; } = "";

    public required string SourceEntityType { get; set; }
    public required string SourceEntityId { get; set; }

    /// <summary>Версия данных 1С.</summary>
    public string? SourceVersion { get; set; }

    /// <summary>Когда запись изменена в 1С (если 1С это хранит).</summary>
    public DateTimeOffset? SourceUpdatedAt { get; set; }

    /// <summary>Когда запись впервые загружена.</summary>
    public DateTimeOffset ImportedAt { get; set; }

    /// <summary>Когда загрузка в последний раз изменила запись.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Отпечаток содержимого: то же содержимое — запись не трогается.</summary>
    public string ContentHash { get; set; } = "";

    /// <summary>Пометка удаления, удаление или отмена проведения в 1С. Запись остаётся ради истории.</summary>
    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? LastRunId { get; set; }
}

public class OneCOrganization : OneCRecord
{
    public string? Code { get; set; }
    public required string Name { get; set; }
    public string? FullName { get; set; }
    public string? Inn { get; set; }
    public string? VatCode { get; set; }
    public string AccountingCurrency { get; set; } = "UZS";
    public bool IsActive { get; set; } = true;
}

/// <summary>Счёт плана счетов. Subconto — виды субконто по порядку (jsonb).</summary>
public class OneCAccount : OneCRecord
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? ParentCode { get; set; }
    public string Type { get; set; } = "active_passive";
    public bool OffBalance { get; set; }
    public bool IsCurrency { get; set; }
    public bool IsQuantity { get; set; }
    public string Subconto { get; set; } = "[]";
}

public class OneCCounterparty : OneCRecord
{
    public string? Code { get; set; }
    public required string Name { get; set; }
    public string? FullName { get; set; }
    public string? Inn { get; set; }
    public string? Kind { get; set; }
    public string? LegalForm { get; set; }
    public string? ParentSourceId { get; set; }
    public bool IsGroup { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }

    /// <summary>Банковские реквизиты (jsonb).</summary>
    public string BankAccounts { get; set; } = "[]";
}

public class OneCContract : OneCRecord
{
    public required string CounterpartySourceId { get; set; }
    public string? Code { get; set; }
    public required string Name { get; set; }
    public string? Number { get; set; }
    public DateOnly? Date { get; set; }
    public string Kind { get; set; } = "other";
    public string Currency { get; set; } = "UZS";
    public int? PaymentTermDays { get; set; }
    public DateOnly? ValidTo { get; set; }
}

public class OneCCurrency : OneCRecord
{
    public required string Code { get; set; }
    public string? NumericCode { get; set; }
    public required string Name { get; set; }
}

/// <summary>Курс валюты на дату — как в 1С: курс за Multiplier единиц, источник курса.</summary>
public class OneCExchangeRate : OneCRecord
{
    public required string Currency { get; set; }
    public DateOnly Date { get; set; }
    public decimal Rate { get; set; }
    public int Multiplier { get; set; } = 1;
    public string? RateSource { get; set; }
}

/// <summary>Склад, подразделение или прочий справочник из субконто; тип — SourceEntityType (warehouse, department, reference:…).</summary>
public class OneCReference : OneCRecord
{
    public string? Code { get; set; }
    public required string Name { get; set; }
    public string? ParentSourceId { get; set; }
    public bool IsGroup { get; set; }

    /// <summary>Прочие реквизиты (jsonb).</summary>
    public string? Attributes { get; set; }
}

public class OneCItem : OneCRecord
{
    public string? Code { get; set; }
    public required string Name { get; set; }
    public string? FullName { get; set; }
    public string? Article { get; set; }
    public string Kind { get; set; } = "other";
    public string? Unit { get; set; }
    public string? ParentSourceId { get; set; }
    public bool IsGroup { get; set; }
    public decimal? VatRate { get; set; }
    public string? Ikpu { get; set; }
    public string? Barcode { get; set; }
}

/// <summary>
/// Документ 1С: реализация, поступление, касса, банк, производство и др. Суммы — в валюте документа и в валюте учёта, с курсом,
/// его датой и источником — как в 1С.
/// </summary>
public class OneCDocument : OneCRecord
{
    public required string Type { get; set; }
    public required string SourceType { get; set; }
    public required string Number { get; set; }
    public DateTimeOffset Date { get; set; }

    /// <summary>Дата документа по Ташкенту — для отчётов по дням и месяцам.</summary>
    public DateOnly DocumentDate { get; set; }

    public bool Posted { get; set; }
    public string? CounterpartySourceId { get; set; }
    public string? ContractSourceId { get; set; }
    public string? WarehouseSourceId { get; set; }
    public string? DepartmentSourceId { get; set; }
    public string Currency { get; set; } = "UZS";
    public decimal Amount { get; set; }
    public decimal? VatAmount { get; set; }
    public decimal AmountAccounting { get; set; }
    public decimal? RateValue { get; set; }
    public int? RateMultiplier { get; set; }
    public DateOnly? RateDate { get; set; }
    public string? RateSource { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? BasisSourceId { get; set; }
    public string? CashFlowItemSourceId { get; set; }
    public string? BankAccountSourceId { get; set; }
    public string? CashDeskSourceId { get; set; }
    public string? Purpose { get; set; }
    public string? Comment { get; set; }
    public List<OneCDocumentLine> Lines { get; set; } = [];
}

/// <summary>Строка документа. Заменяется вместе с документом.</summary>
public class OneCDocumentLine
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid DocumentId { get; set; }
    public int LineNumber { get; set; }
    public string? ItemSourceId { get; set; }
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal? Price { get; set; }
    public decimal Amount { get; set; }
    public decimal? VatRate { get; set; }
    public decimal? VatAmount { get; set; }
    public decimal? CostAmount { get; set; }
    public string? Account { get; set; }
    public string? WarehouseSourceId { get; set; }
    public string? BatchSourceId { get; set; }
}

/// <summary>
/// Бухгалтерская проводка. Ключ — регистратор и номер строки набора. Субконто хранятся полностью (jsonb: вид, ссылка, код, наименование);
/// контрагент, номенклатура и склад вынесены в колонки для отчётов.
/// </summary>
public class OneCEntry : OneCRecord
{
    public required string RegistrarSourceId { get; set; }
    public required string RegistrarType { get; set; }
    public string? RegistrarSourceType { get; set; }
    public string? RegistrarNumber { get; set; }
    public DateTimeOffset? RegistrarDate { get; set; }
    public int LineNumber { get; set; }
    public DateTimeOffset Period { get; set; }

    /// <summary>Дата проводки по Ташкенту — для отчётов по дням и месяцам.</summary>
    public DateOnly PeriodDate { get; set; }

    public bool Active { get; set; } = true;

    /// <summary>Сумма в валюте учёта.</summary>
    public decimal Amount { get; set; }

    public string? Content { get; set; }
    public string? DepartmentSourceId { get; set; }

    public required string DebitAccount { get; set; }
    public string DebitSubconto { get; set; } = "[]";
    public string? DebitCurrency { get; set; }
    public decimal? DebitCurrencyAmount { get; set; }
    public decimal? DebitQuantity { get; set; }
    public string? DebitCounterpartyId { get; set; }
    public string? DebitItemId { get; set; }
    public string? DebitWarehouseId { get; set; }

    public required string CreditAccount { get; set; }
    public string CreditSubconto { get; set; } = "[]";
    public string? CreditCurrency { get; set; }
    public decimal? CreditCurrencyAmount { get; set; }
    public decimal? CreditQuantity { get; set; }
    public string? CreditCounterpartyId { get; set; }
    public string? CreditItemId { get; set; }
    public string? CreditWarehouseId { get; set; }

    public decimal? RateValue { get; set; }
    public int? RateMultiplier { get; set; }
    public DateOnly? RateDate { get; set; }
    public string? RateSource { get; set; }
}

/// <summary>Строка оборотно-сальдовой ведомости за месяц (снимок 1С): сальдо на начало, обороты, сальдо на конец.</summary>
public class OneCBalance : OneCRecord
{
    public DateOnly Period { get; set; }
    public required string Account { get; set; }
    public string Subconto { get; set; } = "[]";
    public string? Currency { get; set; }
    public string? CounterpartyId { get; set; }
    public string? ItemId { get; set; }
    public string? WarehouseId { get; set; }
    public Guid SnapshotId { get; set; }

    public decimal OpeningDebit { get; set; }
    public decimal OpeningCredit { get; set; }
    public decimal? OpeningCurrencyDebit { get; set; }
    public decimal? OpeningCurrencyCredit { get; set; }
    public decimal? OpeningQuantityDebit { get; set; }
    public decimal? OpeningQuantityCredit { get; set; }

    public decimal TurnoverDebit { get; set; }
    public decimal TurnoverCredit { get; set; }
    public decimal? TurnoverCurrencyDebit { get; set; }
    public decimal? TurnoverCurrencyCredit { get; set; }
    public decimal? TurnoverQuantityDebit { get; set; }
    public decimal? TurnoverQuantityCredit { get; set; }

    public decimal ClosingDebit { get; set; }
    public decimal ClosingCredit { get; set; }
    public decimal? ClosingCurrencyDebit { get; set; }
    public decimal? ClosingCurrencyCredit { get; set; }
    public decimal? ClosingQuantityDebit { get; set; }
    public decimal? ClosingQuantityCredit { get; set; }
}

/// <summary>Журнал изменений загруженных записей: что, когда и каким запуском изменилось (старое и новое значение полей).</summary>
public class OneCChange
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public DateTimeOffset At { get; set; }
    public Guid? RunId { get; set; }
    public string? BatchKey { get; set; }
    public required string EntityType { get; set; }
    public string SourceOrganizationId { get; set; } = "";
    public required string SourceEntityId { get; set; }
    public Guid? RecordId { get; set; }

    /// <summary>updated, deleted, restored, cancelled (отмена проведения), reposted (перепроведение).</summary>
    public required string Action { get; set; }

    /// <summary>Изменённые поля: { поле: [было, стало] } (jsonb).</summary>
    public string? Changes { get; set; }

    public string? SourceVersion { get; set; }
}
