using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;

namespace OneBase.Finance.Integration.Worker.OneC;

/// <summary>
/// HTTP API 1С по проекту спецификации (docs/integrations/ONEC_API_SPECIFICATION.md): только GET, JSON, страницы с курсором. Включается
/// после подтверждения API со стороны VENKON GROUP; пути методов можно переопределить (OneC__Paths__documents=…), если они назовут их иначе.
/// Пароль и токен — только в заголовке Authorization; в тексты ошибок и журнал не попадают ни они, ни тела ответов.
/// </summary>
public sealed class OneCHttpDataProvider : IOneCDataProvider, IDisposable
{
    public static readonly IReadOnlyDictionary<string, string> DefaultPaths = new Dictionary<string, string>
    {
        ["ping"] = "ping", ["organizations"] = "organizations", ["accounts"] = "accounts", ["counterparties"] = "counterparties",
        ["contracts"] = "contracts", ["currencies"] = "currencies", ["exchange-rates"] = "exchange-rates", ["warehouses"] = "warehouses",
        ["departments"] = "departments", ["references"] = "references", ["items"] = "items", ["documents"] = "documents",
        ["entries"] = "entries", ["balances"] = "balances", ["changes"] = "changes",
    };

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly OneCOptions _options;
    private readonly HttpClient? _http;
    private readonly string? _configError;

    public OneCHttpDataProvider(OneCOptions options, IHostEnvironment environment)
    {
        _options = options;
        _configError = CheckConfiguration(options, environment.IsProduction(), out var baseUri);
        if (_configError is not null)
        {
            return;
        }

        var roots = OneCTls.LoadRoots(options.CaCertificatePath);
        _http = new HttpClient(OneCTls.CreateHandler(roots))
        {
            BaseAddress = baseUri,
            Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 5, 600)),
        };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OneBase-Finance-Integration", "1.0"));
        if (!string.IsNullOrWhiteSpace(options.Token))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Token.Trim());
        }
        else if (!string.IsNullOrWhiteSpace(options.Username))
        {
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.Username}:{options.Password}"));
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
        }
    }

    public string Name => "http";

    public string? DisplayUrl => Display(_options.BaseUrl);

    /// <summary>Адрес без логина, пароля и параметров.</summary>
    public static string? Display(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) ? $"{u.Scheme}://{u.Host}{(u.IsDefaultPort ? "" : ":" + u.Port)}{u.AbsolutePath.TrimEnd('/')}" : null;

    /// <summary>Ошибка настройки или null: адрес обязателен, только https (http — лишь на этом компьютере и не в production).</summary>
    public static string? CheckConfiguration(OneCOptions options, bool production, out Uri? baseUri)
    {
        baseUri = null;
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            return "Адрес 1С не задан (OneC__BaseUrl). Его сообщит VENKON GROUP после публикации HTTP-сервиса.";
        }

        if (!Uri.TryCreate(options.BaseUrl.Trim().TrimEnd('/') + "/", UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
        {
            return "Адрес 1С (OneC__BaseUrl) — не адрес https.";
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return "Логин и пароль нельзя писать в адресе 1С — задайте OneC__Username и OneC__Password.";
        }

        if (uri.Scheme == "http" && !(uri.IsLoopback || (options.AllowInsecureHttp && !production)))
        {
            return "Подключение к 1С — только по HTTPS. http разрешён лишь для проверки на этом компьютере.";
        }

        if (string.IsNullOrWhiteSpace(options.Token) && string.IsNullOrWhiteSpace(options.Username))
        {
            return "Не заданы учётные данные 1С (OneC__Username и OneC__Password или OneC__Token).";
        }

        baseUri = uri;
        return null;
    }

    public Task<OneCPing> TestConnectionAsync(CancellationToken ct) => GetAsync<OneCPing>("ping", [], ct);

    public Task<OneCPage<OneCOrganizationDto>> GetOrganizationsAsync(OneCPageRequest page, CancellationToken ct) => PageAsync<OneCOrganizationDto>("organizations", page, [], ct);

    public Task<OneCPage<OneCAccountDto>> GetAccountsAsync(OneCPageRequest page, CancellationToken ct) => PageAsync<OneCAccountDto>("accounts", page, [], ct);

    public Task<OneCPage<OneCCounterpartyDto>> GetCounterpartiesAsync(OneCPageRequest page, CancellationToken ct) => PageAsync<OneCCounterpartyDto>("counterparties", page, [], ct);

    public Task<OneCPage<OneCContractDto>> GetContractsAsync(OneCPageRequest page, CancellationToken ct) => PageAsync<OneCContractDto>("contracts", page, [], ct);

    public Task<OneCPage<OneCCurrencyDto>> GetCurrenciesAsync(OneCPageRequest page, CancellationToken ct) => PageAsync<OneCCurrencyDto>("currencies", page, [], ct);

    public Task<OneCPage<OneCRateDto>> GetExchangeRatesAsync(DateOnly from, DateOnly to, OneCPageRequest page, CancellationToken ct) =>
        PageAsync<OneCRateDto>("exchange-rates", page, [("from", Date(from)), ("to", Date(to))], ct);

    public Task<OneCPage<OneCReferenceDto>> GetWarehousesAsync(OneCPageRequest page, CancellationToken ct) => PageAsync<OneCReferenceDto>("warehouses", page, [], ct);

    public Task<OneCPage<OneCReferenceDto>> GetDepartmentsAsync(OneCPageRequest page, CancellationToken ct) => PageAsync<OneCReferenceDto>("departments", page, [], ct);

    public Task<OneCPage<OneCReferenceDto>> GetReferencesAsync(string kind, OneCPageRequest page, CancellationToken ct) =>
        PageAsync<OneCReferenceDto>("references", page, [("kind", kind)], ct);

    public Task<OneCPage<OneCItemDto>> GetNomenclatureAsync(OneCPageRequest page, CancellationToken ct) => PageAsync<OneCItemDto>("items", page, [], ct);

    public Task<OneCPage<OneCBalanceDto>> GetAccountBalancesAsync(string organizationId, DateOnly month, OneCPageRequest page, CancellationToken ct) =>
        PageAsync<OneCBalanceDto>("balances", page, [("organizationId", organizationId), ("period", month.ToString("yyyy-MM", CultureInfo.InvariantCulture)), ("subconto", "true")], ct);

    public Task<OneCPage<OneCEntrySetDto>> GetTransactionsAsync(string organizationId, DateOnly from, DateOnly to, OneCPageRequest page, CancellationToken ct) =>
        PageAsync<OneCEntrySetDto>("entries", page, [("organizationId", organizationId), ("from", Date(from)), ("to", Date(to))], ct);

    public Task<OneCPage<OneCDocumentDto>> GetDocumentsAsync(string organizationId, IReadOnlyList<string>? types, DateOnly from, DateOnly to, OneCPageRequest page, CancellationToken ct) =>
        PageAsync<OneCDocumentDto>("documents", page,
            [("organizationId", organizationId), ("from", Date(from)), ("to", Date(to)), ("type", types is { Count: > 0 } ? string.Join(",", types) : null)], ct);

    public Task<OneCPage<OneCChangeDto>> GetModifiedRecordsAsync(string? cursor, int limit, CancellationToken ct) =>
        PageAsync<OneCChangeDto>("changes", new OneCPageRequest(limit, cursor), [], ct);

    private static string Date(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private Task<OneCPage<T>> PageAsync<T>(string method, OneCPageRequest page, List<(string Key, string? Value)> query, CancellationToken ct)
    {
        query.Add(("limit", Math.Clamp(page.Limit, 1, 1000).ToString(CultureInfo.InvariantCulture)));
        query.Add(("cursor", page.Cursor));
        if (page.IncludeTotals)
        {
            query.Add(("includeTotals", "true"));
        }

        return GetAsync<OneCPage<T>>(method, query, ct);
    }

    private async Task<T> GetAsync<T>(string method, List<(string Key, string? Value)> query, CancellationToken ct)
    {
        if (_configError is not null || _http is null)
        {
            throw new OneCException(OneCErrorKind.Configuration, _configError ?? "Подключение к 1С не настроено.");
        }

        var path = _options.Paths.TryGetValue(method, out var custom) && !string.IsNullOrWhiteSpace(custom) ? custom.Trim('/') : DefaultPaths[method];
        var qs = string.Join("&", query.Where(q => !string.IsNullOrEmpty(q.Value)).Select(q => $"{Uri.EscapeDataString(q.Key)}={Uri.EscapeDataString(q.Value!)}"));
        var where = $"1С /{path}";
        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync(qs.Length == 0 ? path : $"{path}?{qs}", HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (TaskCanceledException e) when (!ct.IsCancellationRequested)
        {
            throw new OneCException(OneCErrorKind.Timeout, $"{where}: нет ответа за {_http.Timeout.TotalSeconds:N0} с", inner: e);
        }
        catch (HttpRequestException e)
        {
            throw Network(where, e);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw await HttpErrorAsync(where, response, ct);
            }

            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                return await JsonSerializer.DeserializeAsync<T>(stream, Json, ct)
                    ?? throw new OneCException(OneCErrorKind.InvalidResponse, $"{where}: пустой ответ");
            }
            catch (JsonException e)
            {
                throw new OneCException(OneCErrorKind.InvalidResponse, $"{where}: ответ не в формате спецификации ({e.Path})", inner: e);
            }
            catch (TaskCanceledException e) when (!ct.IsCancellationRequested)
            {
                throw new OneCException(OneCErrorKind.Timeout, $"{where}: ответ не дочитан за {_http.Timeout.TotalSeconds:N0} с", inner: e);
            }
        }
    }

    private static OneCException Network(string where, HttpRequestException e)
    {
        if (e.InnerException is AuthenticationException auth)
        {
            return new OneCException(OneCErrorKind.Tls, $"{where}: защищённое соединение не установлено — {auth.Message}", inner: e);
        }

        var socket = e.InnerException as SocketException ?? e.InnerException?.InnerException as SocketException;
        var reason = socket?.SocketErrorCode switch
        {
            SocketError.TimedOut => "сервер не отвечает (тайм-аут) — проверьте сеть и брандмауэр",
            SocketError.ConnectionRefused => "соединение отклонено — на порту нет веб-сервера или он закрыт",
            SocketError.HostUnreachable or SocketError.NetworkUnreachable => "сервер недоступен по сети",
            SocketError.HostNotFound => "адрес сервера не найден (DNS)",
            _ => e.HttpRequestError switch
            {
                HttpRequestError.ConnectionError => "не удалось подключиться",
                HttpRequestError.NameResolutionError => "адрес сервера не найден (DNS)",
                HttpRequestError.SecureConnectionError => "ошибка защищённого соединения (TLS)",
                _ => "ошибка сети",
            },
        };
        var kind = e.HttpRequestError == HttpRequestError.SecureConnectionError ? OneCErrorKind.Tls : OneCErrorKind.Network;
        return new OneCException(kind, $"{where}: {reason}", inner: e);
    }

    private static async Task<OneCException> HttpErrorAsync(string where, HttpResponseMessage response, CancellationToken ct)
    {
        string? code = null, message = null;
        try
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(text.Length > 20_000 ? "{}" : text);
            if (doc.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            {
                code = error.TryGetProperty("code", out var c) ? c.GetString() : null;
                message = error.TryGetProperty("message", out var m) ? m.GetString() : null;
            }
        }
        catch (Exception)
        {
            // Тело ответа — не JSON спецификации: в текст ошибки не попадает.
        }

        var detail = message is null ? "" : " — " + (message.Length > 300 ? message[..300] + "…" : message);
        var retryAfter = response.Headers.RetryAfter?.Delta
            ?? (response.Headers.RetryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : null);
        var status = (int)response.StatusCode;
        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => new OneCException(OneCErrorKind.Unauthorized, $"{where}: 1С отклонила авторизацию (401) — проверьте пользователя и пароль{detail}"),
            HttpStatusCode.Forbidden => new OneCException(OneCErrorKind.Forbidden, $"{where}: у пользователя 1С нет прав (403){detail}"),
            HttpStatusCode.NotFound => new OneCException(OneCErrorKind.NotFound, $"{where}: метод не найден (404) — проверьте адрес публикации{detail}"),
            HttpStatusCode.Conflict when code == "cursor_expired" => new OneCException(OneCErrorKind.CursorExpired, $"{where}: курсор изменений устарел (409){detail}"),
            HttpStatusCode.TooManyRequests => new OneCException(OneCErrorKind.RateLimited, $"{where}: превышен лимит запросов (429){detail}", retryAfter),
            HttpStatusCode.ServiceUnavailable => new OneCException(OneCErrorKind.Unavailable, $"{where}: 1С временно недоступна (503){detail}", retryAfter),
            HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => new OneCException(OneCErrorKind.Timeout, $"{where}: тайм-аут ({status}){detail}"),
            _ when status >= 500 => new OneCException(OneCErrorKind.Server, $"{where}: ошибка 1С ({status}){detail}"),
            _ => new OneCException(OneCErrorKind.BadRequest, $"{where}: запрос отклонён ({status}){detail}"),
        };
    }

    public void Dispose() => _http?.Dispose();
}
