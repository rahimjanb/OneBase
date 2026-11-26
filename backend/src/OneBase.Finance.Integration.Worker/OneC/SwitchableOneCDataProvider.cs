using System.Text.Json;
using OneBase.Finance.Integration.Contracts;

namespace OneBase.Finance.Integration.Worker.OneC;

/// <summary>
/// HTTP API 1С с подключением, которое может меняться без перезапуска службы. Источник подключения: переменные OneC__* службы (если задан
/// OneC__BaseUrl — они главнее и страница «1С» на них не влияет) или настройки на странице «1С» в OneBase (адрес, вход, сертификат; пароль
/// и API Secret приходят от OneBase по ключу службы во внутренней сети). Запросы, начатые со старым подключением, дорабатывают на нём.
/// </summary>
public sealed class SwitchableOneCDataProvider : IOneCDataProvider, IDisposable
{
    public const string FromEnvironment = "env";
    public const string FromOneBase = "onebase";
    public const string NotConfigured = "none";

    private readonly OneCOptions _env;
    private readonly IHostEnvironment _environment;
    private readonly object _lock = new();
    private readonly List<OneCHttpDataProvider> _retired = [];
    private volatile OneCHttpDataProvider _current;

    public SwitchableOneCDataProvider(OneCOptions env, IHostEnvironment environment)
    {
        _env = env;
        _environment = environment;
        _current = new OneCHttpDataProvider(env, environment);
        Options = env;
        Source = HasOwnConnection ? FromEnvironment : NotConfigured;
    }

    /// <summary>Подключение задано переменными службы — страница «1С» его не меняет.</summary>
    public bool HasOwnConnection => !string.IsNullOrWhiteSpace(_env.BaseUrl);

    /// <summary>Действующие настройки подключения (для диагностики).</summary>
    public OneCOptions Options { get; private set; }

    /// <summary>env, onebase или none.</summary>
    public string Source { get; private set; }

    /// <summary>Версия подключения со страницы «1С», которое сейчас действует (null — не применялось).</summary>
    public string? Version { get; private set; }

    /// <summary>
    /// Применить подключение со страницы «1С»: новые запросы идут уже с ним. Если у службы свои переменные OneC__BaseUrl — ничего не меняется.
    /// Возвращает true, если подключение сменилось.
    /// </summary>
    public bool Apply(WorkerConnection connection)
    {
        if (HasOwnConnection)
        {
            return false;
        }

        var options = Merge(_env, connection);
        lock (_lock)
        {
            _retired.Add(_current);
            _current = new OneCHttpDataProvider(options, _environment);
            Options = options;
            Version = connection.Version;
            Source = string.IsNullOrWhiteSpace(connection.BaseUrl) ? NotConfigured : FromOneBase;
        }

        return true;
    }

    /// <summary>Настройки службы с подключением со страницы «1С»: адрес, вход и сертификат — оттуда, остальное (тайм-аут, пути) — от службы.</summary>
    public static OneCOptions Merge(OneCOptions env, WorkerConnection c) => new()
    {
        Enabled = env.Enabled, Provider = env.Provider, BaseUrl = c.BaseUrl ?? "", Username = c.Username ?? "", Password = c.Password ?? "",
        ApiSecret = c.ApiSecret ?? "", Token = "", TimeoutSeconds = env.TimeoutSeconds, AllowInsecureHttp = env.AllowInsecureHttp, Paths = env.Paths,
        CaCertificatePem = c.CaCertificatePem ?? "", CaCertificatePath = string.IsNullOrWhiteSpace(c.CaCertificatePem) ? env.CaCertificatePath : "",
        AllowMockInProduction = env.AllowMockInProduction, Mock = env.Mock,
    };

    private OneCHttpDataProvider P => _current;

    public string Name => "http";

    public string? DisplayUrl => P.DisplayUrl;

    public Task<OneCPing> TestConnectionAsync(CancellationToken ct) => P.TestConnectionAsync(ct);

    public Task<OneCPage<OneCOrganizationDto>> GetOrganizationsAsync(OneCPageRequest page, CancellationToken ct) => P.GetOrganizationsAsync(page, ct);

    public Task<OneCPage<OneCAccountDto>> GetAccountsAsync(OneCPageRequest page, CancellationToken ct) => P.GetAccountsAsync(page, ct);

    public Task<OneCPage<OneCCounterpartyDto>> GetCounterpartiesAsync(OneCPageRequest page, CancellationToken ct) => P.GetCounterpartiesAsync(page, ct);

    public Task<OneCPage<OneCContractDto>> GetContractsAsync(OneCPageRequest page, CancellationToken ct) => P.GetContractsAsync(page, ct);

    public Task<OneCPage<OneCCurrencyDto>> GetCurrenciesAsync(OneCPageRequest page, CancellationToken ct) => P.GetCurrenciesAsync(page, ct);

    public Task<OneCPage<OneCRateDto>> GetExchangeRatesAsync(DateOnly from, DateOnly to, OneCPageRequest page, CancellationToken ct) =>
        P.GetExchangeRatesAsync(from, to, page, ct);

    public Task<OneCPage<OneCReferenceDto>> GetWarehousesAsync(OneCPageRequest page, CancellationToken ct) => P.GetWarehousesAsync(page, ct);

    public Task<OneCPage<OneCReferenceDto>> GetDepartmentsAsync(OneCPageRequest page, CancellationToken ct) => P.GetDepartmentsAsync(page, ct);

    public Task<OneCPage<OneCReferenceDto>> GetReferencesAsync(string kind, OneCPageRequest page, CancellationToken ct) => P.GetReferencesAsync(kind, page, ct);

    public Task<OneCPage<OneCItemDto>> GetNomenclatureAsync(OneCPageRequest page, CancellationToken ct) => P.GetNomenclatureAsync(page, ct);

    public Task<OneCPage<OneCBalanceDto>> GetAccountBalancesAsync(string organizationId, DateOnly month, OneCPageRequest page, CancellationToken ct) =>
        P.GetAccountBalancesAsync(organizationId, month, page, ct);

    public Task<OneCPage<OneCEntrySetDto>> GetTransactionsAsync(string organizationId, DateOnly from, DateOnly to, OneCPageRequest page, CancellationToken ct) =>
        P.GetTransactionsAsync(organizationId, from, to, page, ct);

    public Task<OneCPage<OneCDocumentDto>> GetDocumentsAsync(string organizationId, IReadOnlyList<string>? types, DateOnly from, DateOnly to, OneCPageRequest page,
        CancellationToken ct) => P.GetDocumentsAsync(organizationId, types, from, to, page, ct);

    public Task<OneCPage<OneCChangeDto>> GetModifiedRecordsAsync(string? cursor, int limit, CancellationToken ct) => P.GetModifiedRecordsAsync(cursor, limit, ct);

    public Task<OneCPage<OneCVersionDto>> GetVersionsAsync(OneCVersionQuery query, OneCPageRequest page, CancellationToken ct) => P.GetVersionsAsync(query, page, ct);

    public Task<IReadOnlyList<JsonElement>> GetByIdsAsync(string entityType, IReadOnlyList<string> ids, CancellationToken ct) => P.GetByIdsAsync(entityType, ids, ct);

    public void Dispose()
    {
        lock (_lock)
        {
            _current.Dispose();
            _retired.ForEach(p => p.Dispose());
            _retired.Clear();
        }
    }
}
