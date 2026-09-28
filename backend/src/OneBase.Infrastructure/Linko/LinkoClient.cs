using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace OneBase.Infrastructure.Linko;

public sealed class LinkoOptions
{
    public const string Section = "Linko";

    /// <summary>Запасной адрес из окружения (LINKO_BASE_URL), если он не задан в OneBase.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Запасной токен из окружения (LINKO_TOKEN), если он не задан в OneBase.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Запасной токен API планов из окружения (LINKO_PLAN_TOKEN).</summary>
    public string PlanToken { get; set; } = string.Empty;

    public int PageSize { get; set; } = 1000;

    /// <summary>Сколько страниц запрашивать одновременно при выгрузке списков.</summary>
    public int Parallelism { get; set; } = 4;
    public int TimeoutSeconds { get; set; } = 60;
    public int MaxRetries { get; set; } = 4;
}

public sealed class LinkoApiException(string message, HttpStatusCode? status = null) : Exception(message)
{
    public HttpStatusCode? Status { get; } = status;
}

public sealed record LinkoTestResult(
    bool Ok,
    string Message,
    long? Users,
    long? Markets,
    long? Orders,
    long ElapsedMs,
    bool? PlansOk = null,
    string? PlansMessage = null,
    DateTime? PlansLastDate = null);

/// <summary>
/// Клиент Linko External API: пагинация limit/offset, повторы с backoff, разбор поля errors.
/// Адрес и токен берутся из LinkoSettingsStore на каждый запрос — изменения в настройках действуют сразу.
/// </summary>
public sealed class LinkoClient(HttpClient http, LinkoOptions options, LinkoSettingsStore settings, ILogger<LinkoClient> logger)
{
    private const string Prefix = "api/v1/integration/external-api/";

    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public async Task<long> CountAsync(string entity, IDictionary<string, string?>? filters = null, CancellationToken ct = default)
    {
        var result = await SendAsync<LinkoCount>(await ReadyAsync(ct), $"{entity}_count/", filters, options.MaxRetries, ct);
        return result.Count;
    }

    /// <summary>
    /// Выгружает все страницы списка. Останавливается, когда пришло меньше limit записей.
    /// Каждая страница передаётся в onPage — так большие выгрузки не держатся в памяти целиком.
    /// </summary>
    public async Task<int> ReadAllAsync<T>(
        string entity,
        IDictionary<string, string?>? filters,
        Func<IReadOnlyList<T>, Task> onPage,
        CancellationToken ct = default)
    {
        var connection = await ReadyAsync(ct);
        var parallel = Math.Clamp(options.Parallelism, 1, 8);
        var total = 0;

        // Страницы запрашиваются пачками по `parallel` штук одновременно, а обрабатываются строго по порядку.
        for (var offset = 0; ; offset += options.PageSize * parallel)
        {
            var batch = Enumerable.Range(0, parallel)
                .Select(i => SendAsync<LinkoPage<T>>(connection, $"{entity}/", PageQuery(filters, offset + i * options.PageSize), options.MaxRetries, ct))
                .ToList();
            var pages = await Task.WhenAll(batch);

            foreach (var page in pages)
            {
                if (page.Errors is { Count: > 0 })
                {
                    logger.LogWarning("Linko {Entity}: {Count} ошибок в ответе, например: {Error}",
                        entity, page.Errors.Count, page.Errors[0].Error?.ToString());
                }

                var items = page.Results ?? [];
                if (items.Count > 0)
                {
                    await onPage(items);
                }

                total += items.Count;
                if (items.Count < options.PageSize)
                {
                    return total;
                }
            }
        }
    }

    private Dictionary<string, string?> PageQuery(IDictionary<string, string?>? filters, int offset) =>
        new(filters ?? new Dictionary<string, string?>())
        {
            ["limit"] = options.PageSize.ToString(),
            ["offset"] = offset.ToString(),
        };

    /// <summary>
    /// Проверка подключения: три лёгких запроса *_count. Без повторов и с коротким таймаутом,
    /// чтобы пользователь быстро получил ответ. Токен в сообщениях не показывается.
    /// </summary>
    public async Task<LinkoTestResult> TestAsync(string baseUrl, string token, string? planToken, CancellationToken ct = default)
    {
        var result = await TestExternalAsync(baseUrl, token, ct);
        if (string.IsNullOrWhiteSpace(planToken) || !LinkoSettingsStore.IsValidUrl(baseUrl))
        {
            return result;
        }

        // API планов: время последнего пересчёта — лёгкий запрос, которого достаточно для проверки токена.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        var connection = new LinkoConnectionSettings(LinkoSettingsStore.Normalize(baseUrl), token, true, LinkoSettingsSource.OneBase, null, planToken.Trim());
        try
        {
            var query = new Dictionary<string, string?> { ["by"] = "today", ["token"] = connection.PlanToken };
            var last = (await SendAsync<LinkoStaffLastDate>(connection, StaffPrefix, "staff_balance/last_date/", query, false, 0, timeout.Token)).LastDate;
            return result with { PlansOk = true, PlansMessage = "API планов работает.", PlansLastDate = last };
        }
        catch (LinkoApiException ex)
        {
            var message = ex.Status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                ? $"Сервер Linko не принял токен планов (HTTP {(int)ex.Status})."
                : ex.Status is { } status ? $"API планов ответил ошибкой HTTP {(int)status}." : $"API планов недоступен: {ex.Message}";
            return result with { PlansOk = false, PlansMessage = message };
        }
        catch (Exception ex) when (ex is OperationCanceledException or JsonException && !ct.IsCancellationRequested)
        {
            return result with { PlansOk = false, PlansMessage = "API планов не ответил или ответил не в том формате." };
        }
    }

    private async Task<LinkoTestResult> TestExternalAsync(string baseUrl, string token, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        if (!LinkoSettingsStore.IsValidUrl(baseUrl))
        {
            return new LinkoTestResult(false, "Укажите адрес сервера Linko, например https://имя.linko.uz", null, null, null, 0);
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return new LinkoTestResult(false, "Токен не задан.", null, null, null, 0);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        var connection = new LinkoConnectionSettings(LinkoSettingsStore.Normalize(baseUrl), token.Trim(), true, LinkoSettingsSource.OneBase, null);

        try
        {
            var users = (await SendAsync<LinkoCount>(connection, "users_count/", null, 0, timeout.Token)).Count;
            var markets = (await SendAsync<LinkoCount>(connection, "markets_count/", null, 0, timeout.Token)).Count;
            var orders = (await SendAsync<LinkoCount>(connection, "orders_count/", null, 0, timeout.Token)).Count;
            return new LinkoTestResult(true, "Подключение работает.", users, markets, orders, watch.ElapsedMilliseconds);
        }
        catch (LinkoApiException ex)
        {
            var message = ex.Status switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => $"Сервер Linko не принял токен (HTTP {(int)ex.Status}).",
                HttpStatusCode.NotFound => "По этому адресу нет Linko External API (HTTP 404). Проверьте адрес сервера.",
                { } status => $"Сервер Linko ответил ошибкой HTTP {(int)status}.",
                null => $"Сервер недоступен: {ex.Message}",
            };
            return new LinkoTestResult(false, message, null, null, null, watch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new LinkoTestResult(false, "Сервер Linko не ответил за 20 секунд.", null, null, null, watch.ElapsedMilliseconds);
        }
        catch (JsonException)
        {
            return new LinkoTestResult(false, "Сервер ответил не в формате Linko API. Проверьте адрес сервера.", null, null, null, watch.ElapsedMilliseconds);
        }
    }

    private async Task<LinkoConnectionSettings> ReadyAsync(CancellationToken ct)
    {
        var connection = await settings.GetAsync(ct);
        if (!connection.HasCredentials)
        {
            throw new LinkoApiException("Linko не настроен: задайте адрес и токен в «Настройки → Интеграции → Продажи → Linko».");
        }

        return connection;
    }

    // ---------- API планов (staff_balance): отдельный токен, передаётся параметром token ----------

    private const string StaffPrefix = "ru/api/v1/staff/";

    /// <summary>Время последнего пересчёта планов в Linko.</summary>
    public async Task<DateTime?> StaffLastDateAsync(CancellationToken ct = default)
    {
        var connection = await ReadyPlansAsync(ct);
        var query = new Dictionary<string, string?> { ["by"] = "today", ["token"] = connection.PlanToken };
        return (await SendAsync<LinkoStaffLastDate>(connection, StaffPrefix, "staff_balance/last_date/", query, false, options.MaxRetries, ct)).LastDate;
    }

    /// <summary>Планы и факт агентов по KPI-показателям за месяц.</summary>
    public async Task<List<LinkoStaffBalanceDto>> StaffBalanceAsync(int year, int month, CancellationToken ct = default)
    {
        var connection = await ReadyPlansAsync(ct);
        var query = new Dictionary<string, string?>
        {
            ["year"] = year.ToString(),
            ["month"] = month.ToString(),
            ["token"] = connection.PlanToken,
            ["format"] = "json",
        };
        return await SendAsync<List<LinkoStaffBalanceDto>>(connection, StaffPrefix, "staff_balance/", query, false, options.MaxRetries, ct);
    }

    private async Task<LinkoConnectionSettings> ReadyPlansAsync(CancellationToken ct)
    {
        var connection = await settings.GetAsync(ct);
        if (!connection.HasPlanCredentials)
        {
            throw new LinkoApiException("Не задан токен API планов Linko (staff_balance).");
        }

        return connection;
    }

    private Task<T> SendAsync<T>(LinkoConnectionSettings connection, string path, IDictionary<string, string?>? query, int maxRetries, CancellationToken ct) =>
        SendAsync<T>(connection, Prefix, path, query, externalAuth: true, maxRetries, ct);

    /// <summary>GET с повторами. В сообщения об ошибках попадает только path — без параметров и токенов.</summary>
    private async Task<T> SendAsync<T>(
        LinkoConnectionSettings connection,
        string prefix,
        string path,
        IDictionary<string, string?>? query,
        bool externalAuth,
        int maxRetries,
        CancellationToken ct)
    {
        var url = new Uri(new Uri(connection.BaseUrl.TrimEnd('/') + "/"), prefix + path + BuildQuery(query));

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (externalAuth)
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("External", connection.Token);
                }

                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using var response = await http.SendAsync(request, ct);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<T>(Json, ct)
                        ?? throw new LinkoApiException($"Linko {path}: пустой ответ");
                }

                var body = await response.Content.ReadAsStringAsync(ct);
                var retryable = response.StatusCode is HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
                if (!retryable || attempt > maxRetries)
                {
                    throw new LinkoApiException(
                        $"Linko {path}: HTTP {(int)response.StatusCode} {Truncate(body)}", response.StatusCode);
                }

                logger.LogWarning("Linko {Path}: HTTP {Status}, попытка {Attempt}", path, (int)response.StatusCode, attempt);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                if (attempt > maxRetries)
                {
                    throw new LinkoApiException($"Linko {path}: {ex.Message}");
                }

                logger.LogWarning(ex, "Linko {Path}: сетевая ошибка, попытка {Attempt}", path, attempt);
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
        }
    }

    private static string BuildQuery(IDictionary<string, string?>? query)
    {
        if (query is null || query.Count == 0)
        {
            return string.Empty;
        }

        var parts = query
            .Where(kv => !string.IsNullOrEmpty(kv.Value))
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}");
        return "?" + string.Join('&', parts);
    }

    private static string Truncate(string s) => s.Length <= 300 ? s : s[..300] + "…";
}
