using OneBase.Finance.Integration.Contracts;

namespace OneBase.Finance.Integration.Worker.OneC;

/// <summary>
/// Источник данных 1С — проектный интерфейс OneBase (имена методов не означают, что такие методы есть в установленной 1С). Реализации:
/// <see cref="OneCHttpDataProvider"/> — HTTP API 1С по спецификации для VENKON; <c>OneCMockDataProvider</c> — тестовый источник.
/// Все методы только читают. Ошибки — <see cref="OneCException"/> с видом (сеть, тайм-аут, авторизация…).
/// </summary>
public interface IOneCDataProvider
{
    /// <summary>mock или http.</summary>
    string Name { get; }

    /// <summary>Адрес 1С без логина и пароля — для страницы настроек.</summary>
    string? DisplayUrl { get; }

    /// <summary>Проверка соединения и авторизации: версия API, идентификатор базы.</summary>
    Task<OneCPing> TestConnectionAsync(CancellationToken ct);

    Task<OneCPage<OneCOrganizationDto>> GetOrganizationsAsync(OneCPageRequest page, CancellationToken ct);

    Task<OneCPage<OneCAccountDto>> GetAccountsAsync(OneCPageRequest page, CancellationToken ct);

    Task<OneCPage<OneCCounterpartyDto>> GetCounterpartiesAsync(OneCPageRequest page, CancellationToken ct);

    Task<OneCPage<OneCContractDto>> GetContractsAsync(OneCPageRequest page, CancellationToken ct);

    Task<OneCPage<OneCCurrencyDto>> GetCurrenciesAsync(OneCPageRequest page, CancellationToken ct);

    Task<OneCPage<OneCRateDto>> GetExchangeRatesAsync(DateOnly from, DateOnly to, OneCPageRequest page, CancellationToken ct);

    Task<OneCPage<OneCReferenceDto>> GetWarehousesAsync(OneCPageRequest page, CancellationToken ct);

    Task<OneCPage<OneCReferenceDto>> GetDepartmentsAsync(OneCPageRequest page, CancellationToken ct);

    /// <summary>Прочие справочники из субконто: cash_flow_items, cost_items, bank_accounts, cash_desks, batches…</summary>
    Task<OneCPage<OneCReferenceDto>> GetReferencesAsync(string kind, OneCPageRequest page, CancellationToken ct);

    Task<OneCPage<OneCItemDto>> GetNomenclatureAsync(OneCPageRequest page, CancellationToken ct);

    /// <summary>Остатки и обороты по счетам за месяц (оборотно-сальдовая ведомость) с разворотом по субконто.</summary>
    Task<OneCPage<OneCBalanceDto>> GetAccountBalancesAsync(string organizationId, DateOnly month, OneCPageRequest page, CancellationToken ct);

    /// <summary>Проводки за период — наборами по документу-регистратору.</summary>
    Task<OneCPage<OneCEntrySetDto>> GetTransactionsAsync(string organizationId, DateOnly from, DateOnly to, OneCPageRequest page, CancellationToken ct);

    /// <summary>Документы за период; types — null: все типы.</summary>
    Task<OneCPage<OneCDocumentDto>> GetDocumentsAsync(string organizationId, IReadOnlyList<string>? types, DateOnly from, DateOnly to, OneCPageRequest page, CancellationToken ct);

    /// <summary>Изменения с прошлой выгрузки (план обмена). Без курсора — текущий курсор и пустой список.</summary>
    Task<OneCPage<OneCChangeDto>> GetModifiedRecordsAsync(string? cursor, int limit, CancellationToken ct);

    /// <summary>
    /// Версии объектов (v1.1, только чтение): справочник целиком или документы организации за период — идентификатор, ВерсияДанных,
    /// пометка удаления (у документов — проведён, дата, тип).
    /// </summary>
    Task<OneCPage<OneCVersionDto>> GetVersionsAsync(OneCVersionQuery query, OneCPageRequest page, CancellationToken ct);

    /// <summary>
    /// Объекты по идентификаторам (v1.1, не больше 100): JSON в формате метода сущности; документы — вместе с проводками. Удалённых из базы
    /// в ответе нет.
    /// </summary>
    Task<IReadOnlyList<System.Text.Json.JsonElement>> GetByIdsAsync(string entityType, IReadOnlyList<string> ids, CancellationToken ct);

    /// <summary>Обороты за период — те же строки оборотно-сальдовой ведомости по месяцам периода.</summary>
    async Task<List<OneCBalanceDto>> GetTurnoversAsync(string organizationId, DateOnly from, DateOnly to, int limit, CancellationToken ct)
    {
        var rows = new List<OneCBalanceDto>();
        for (var month = new DateOnly(from.Year, from.Month, 1); month <= to; month = month.AddMonths(1))
        {
            string? cursor = null;
            do
            {
                var page = await GetAccountBalancesAsync(organizationId, month, new OneCPageRequest(limit, cursor), ct);
                rows.AddRange(page.Items);
                cursor = page.HasMore ? page.Page!.NextCursor : null;
            }
            while (cursor is not null);
        }

        return rows;
    }

    Task<OneCPage<OneCDocumentDto>> GetSalesAsync(string organizationId, DateOnly from, DateOnly to, OneCPageRequest page, CancellationToken ct) =>
        GetDocumentsAsync(organizationId, OneCDocumentTypes.Sales, from, to, page, ct);

    Task<OneCPage<OneCDocumentDto>> GetPurchasesAsync(string organizationId, DateOnly from, DateOnly to, OneCPageRequest page, CancellationToken ct) =>
        GetDocumentsAsync(organizationId, OneCDocumentTypes.Purchases, from, to, page, ct);

    Task<OneCPage<OneCDocumentDto>> GetCashOperationsAsync(string organizationId, DateOnly from, DateOnly to, OneCPageRequest page, CancellationToken ct) =>
        GetDocumentsAsync(organizationId, OneCDocumentTypes.Cash, from, to, page, ct);

    Task<OneCPage<OneCDocumentDto>> GetBankOperationsAsync(string organizationId, DateOnly from, DateOnly to, OneCPageRequest page, CancellationToken ct) =>
        GetDocumentsAsync(organizationId, OneCDocumentTypes.Bank, from, to, page, ct);

    Task<OneCPage<OneCDocumentDto>> GetProductionOperationsAsync(string organizationId, DateOnly from, DateOnly to, OneCPageRequest page, CancellationToken ct) =>
        GetDocumentsAsync(organizationId, OneCDocumentTypes.ProductionTypes, from, to, page, ct);
}

/// <summary>Вид ошибки 1С: от него зависит, повторять ли запрос.</summary>
public enum OneCErrorKind
{
    Network,
    Timeout,
    Tls,
    Unauthorized,
    Forbidden,
    NotFound,
    BadRequest,
    RateLimited,
    Unavailable,
    Server,
    CursorExpired,
    InvalidResponse,
    Configuration,
}

/// <summary>Ошибка обращения к 1С. Текст — без пароля, токена и тела ответа.</summary>
public sealed class OneCException(OneCErrorKind kind, string message, TimeSpan? retryAfter = null, Exception? inner = null) : Exception(message, inner)
{
    public OneCErrorKind Kind { get; } = kind;
    public TimeSpan? RetryAfter { get; } = retryAfter;

    /// <summary>Временная ошибка — повторить с паузой.</summary>
    public bool Transient => Kind is OneCErrorKind.Network or OneCErrorKind.Timeout or OneCErrorKind.RateLimited or OneCErrorKind.Unavailable
        or OneCErrorKind.Server or OneCErrorKind.InvalidResponse;

    /// <summary>Повторять бессмысленно до вмешательства человека: остановить обмен.</summary>
    public bool Fatal => Kind is OneCErrorKind.Unauthorized or OneCErrorKind.Tls or OneCErrorKind.Configuration;
}
