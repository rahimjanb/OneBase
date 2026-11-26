using System.Text.Json;

namespace OneBase.Finance.Integration.Worker.OneC;

// Данные в форме API 1С (docs/integrations/ONEC_API_SPECIFICATION.md). Это проект контракта для VENKON GROUP: если реальный API
// окажется другим, меняется только адаптер (OneCHttpDataProvider) и эти модели — нормализация и OneBase остаются прежними.

public sealed record OneCPing
{
    public string? ApiVersion { get; init; }
    public string? InfobaseId { get; init; }
    public string? InfobaseName { get; init; }
    public OneCConfiguration? Configuration { get; init; }
    public DateTimeOffset? ServerTime { get; init; }
    public string? AccountingCurrency { get; init; }
    public string? User { get; init; }
    public bool? ReadOnly { get; init; }

    /// <summary>Как отслеживаются изменения (v1.1): versions — только чтением через /versions, changes — план обмена (/changes).</summary>
    public string? ChangeTracking { get; init; }

    /// <summary>Возможности сервиса (v1.1): versions, ids, changes.</summary>
    public List<string>? Capabilities { get; init; }

    /// <summary>Изменения — сверкой версий: так объявила 1С (changeTracking или возможность versions без changes).</summary>
    public bool TracksByVersions =>
        string.Equals(ChangeTracking, "versions", StringComparison.OrdinalIgnoreCase)
        || (ChangeTracking is null && Capabilities is { } c && c.Contains("versions", StringComparer.OrdinalIgnoreCase) && !c.Contains("changes", StringComparer.OrdinalIgnoreCase));
}

/// <summary>Версия объекта 1С из /versions (v1.1): что есть в 1С сейчас, без самих данных.</summary>
public sealed record OneCVersionDto
{
    public string? Id { get; init; }
    public string? Version { get; init; }
    public bool DeletionMark { get; init; }
    public bool? Posted { get; init; }
    public DateTimeOffset? Date { get; init; }
    public string? Type { get; init; }
}

/// <summary>Отбор /versions: тип записи OneBase (справочник целиком или документы организации за период).</summary>
public sealed record OneCVersionQuery(string EntityType, string? OrganizationId = null, DateOnly? From = null, DateOnly? To = null);

public sealed record OneCConfiguration(string? Name, string? Version);

public sealed record OneCPageInfo(int Limit, string? NextCursor, bool HasMore);

public sealed record OneCTotals(int? Count, decimal? Amount);

public sealed record OneCPage<T>
{
    public string? ApiVersion { get; init; }
    public List<T> Items { get; init; } = [];
    public OneCPageInfo? Page { get; init; }
    public OneCTotals? Totals { get; init; }

    public bool HasMore => Page?.HasMore == true && !string.IsNullOrEmpty(Page.NextCursor);
}

/// <summary>Страница запроса: размер и курсор (непрозрачная строка 1С).</summary>
public sealed record OneCPageRequest(int Limit, string? Cursor = null, bool IncludeTotals = false);

public abstract record OneCObject
{
    public string? Id { get; init; }
    public string? Version { get; init; }
    public DateTimeOffset? ModifiedAt { get; init; }
    public bool DeletionMark { get; init; }
}

public sealed record OneCOrganizationDto : OneCObject
{
    public string? Code { get; init; }
    public string? Name { get; init; }
    public string? FullName { get; init; }
    public string? Inn { get; init; }
    public string? VatCode { get; init; }
    public string? AccountingCurrency { get; init; }
    public bool? IsActive { get; init; }
}

public sealed record OneCSubcontoTypeDto(int Index, string? Type, bool TurnoverOnly = false);

public sealed record OneCAccountDto : OneCObject
{
    public string? Code { get; init; }
    public string? Name { get; init; }
    public string? ParentCode { get; init; }
    public string? Type { get; init; }
    public bool OffBalance { get; init; }
    public bool Currency { get; init; }
    public bool Quantity { get; init; }
    public List<OneCSubcontoTypeDto> Subconto { get; init; } = [];
}

public sealed record OneCBankAccountDto(string? Account, string? Mfo, string? Bank, string? Currency);

public sealed record OneCCounterpartyDto : OneCObject
{
    public string? Code { get; init; }
    public string? Name { get; init; }
    public string? FullName { get; init; }
    public string? Inn { get; init; }
    public string? Pinfl { get; init; }
    public string? Kind { get; init; }
    public string? LegalForm { get; init; }
    public string? ParentId { get; init; }
    public bool IsGroup { get; init; }
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public List<OneCBankAccountDto> BankAccounts { get; init; } = [];
}

public sealed record OneCContractDto : OneCObject
{
    public string? Code { get; init; }
    public string? Name { get; init; }
    public string? CounterpartyId { get; init; }
    public string? OrganizationId { get; init; }
    public string? Number { get; init; }
    public DateOnly? Date { get; init; }
    public string? Kind { get; init; }
    public string? Currency { get; init; }
    public int? PaymentTermDays { get; init; }
    public DateOnly? ValidTo { get; init; }
}

public sealed record OneCCurrencyDto : OneCObject
{
    public string? Code { get; init; }
    public string? NumericCode { get; init; }
    public string? Name { get; init; }
}

public sealed record OneCRateDto
{
    public string? Currency { get; init; }
    public DateOnly Date { get; init; }
    public decimal Rate { get; init; }
    public int Multiplier { get; init; } = 1;
    public string? Source { get; init; }
}

/// <summary>Склад, подразделение или прочий справочник.</summary>
public sealed record OneCReferenceDto : OneCObject
{
    public string? Code { get; init; }
    public string? Name { get; init; }
    public string? ParentId { get; init; }
    public bool IsGroup { get; init; }
    public string? OrganizationId { get; init; }
    public string? Kind { get; init; }
    public Dictionary<string, string?>? Attributes { get; init; }
}

public sealed record OneCItemDto : OneCObject
{
    public string? Code { get; init; }
    public string? Name { get; init; }
    public string? FullName { get; init; }
    public string? Article { get; init; }
    public string? Kind { get; init; }
    public string? Unit { get; init; }
    public string? ParentId { get; init; }
    public bool IsGroup { get; init; }
    public decimal? VatRate { get; init; }
    public string? Ikpu { get; init; }
    public string? Barcode { get; init; }
}

public sealed record OneCRateRefDto(decimal Value, int Multiplier = 1, DateOnly? Date = null, string? Source = null);

public sealed record OneCDocumentLineDto
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

public sealed record OneCDocumentDto : OneCObject
{
    public string? Type { get; init; }
    public string? SourceType { get; init; }
    public string? Number { get; init; }
    public DateTimeOffset? Date { get; init; }
    public string? OrganizationId { get; init; }
    public bool Posted { get; init; }
    public string? CounterpartyId { get; init; }
    public string? ContractId { get; init; }
    public string? WarehouseId { get; init; }
    public string? DepartmentId { get; init; }
    public string? Currency { get; init; }
    public decimal Amount { get; init; }
    public decimal? VatAmount { get; init; }
    public decimal? AmountAccounting { get; init; }
    public OneCRateRefDto? Rate { get; init; }
    public DateOnly? DueDate { get; init; }
    public string? BasisDocumentId { get; init; }
    public string? CashFlowItemId { get; init; }
    public string? BankAccountId { get; init; }
    public string? CashDeskId { get; init; }
    public string? Purpose { get; init; }
    public string? Comment { get; init; }
    public List<OneCDocumentLineDto> Lines { get; init; } = [];

    /// <summary>Проводки документа — в журнале изменений (/changes) приходят вместе с документом.</summary>
    public List<OneCEntryDto>? Entries { get; init; }
}

public sealed record OneCSubcontoDto(int Index, string? Type, string? Id = null, string? Code = null, string? Name = null);

public sealed record OneCEntrySideDto
{
    public string? Account { get; init; }
    public List<OneCSubcontoDto> Subconto { get; init; } = [];
    public string? Currency { get; init; }
    public decimal? CurrencyAmount { get; init; }
    public decimal? Quantity { get; init; }
}

public sealed record OneCEntryDto
{
    public int LineNumber { get; init; }
    public DateTimeOffset? Period { get; init; }
    public bool Active { get; init; } = true;
    public decimal Amount { get; init; }
    public string? Content { get; init; }
    public string? DepartmentId { get; init; }
    public OneCEntrySideDto? Debit { get; init; }
    public OneCEntrySideDto? Credit { get; init; }
    public OneCRateRefDto? Rate { get; init; }
}

public sealed record OneCRegistrarDto(string? Id, string? Type, string? SourceType, string? Number, DateTimeOffset? Date);

public sealed record OneCEntrySetDto
{
    public OneCRegistrarDto? Registrar { get; init; }
    public string? OrganizationId { get; init; }
    public string? Version { get; init; }
    public List<OneCEntryDto> Entries { get; init; } = [];
}

public sealed record OneCBalanceAmountsDto
{
    public decimal Debit { get; init; }
    public decimal Credit { get; init; }
    public decimal? CurrencyDebit { get; init; }
    public decimal? CurrencyCredit { get; init; }
    public decimal? QuantityDebit { get; init; }
    public decimal? QuantityCredit { get; init; }
}

public sealed record OneCBalanceDto
{
    /// <summary>Месяц «ГГГГ-ММ».</summary>
    public string? Period { get; init; }

    public string? OrganizationId { get; init; }
    public string? Account { get; init; }
    public List<OneCSubcontoDto> Subconto { get; init; } = [];
    public string? Currency { get; init; }
    public OneCBalanceAmountsDto? Opening { get; init; }
    public OneCBalanceAmountsDto? Turnover { get; init; }
    public OneCBalanceAmountsDto? Closing { get; init; }
}

/// <summary>Изменение из журнала 1С (план обмена).</summary>
public sealed record OneCChangeDto
{
    public long Seq { get; init; }
    public string? EntityType { get; init; }
    public string? Id { get; init; }
    public string? OrganizationId { get; init; }
    public bool Deleted { get; init; }
    public JsonElement? Data { get; init; }
}
