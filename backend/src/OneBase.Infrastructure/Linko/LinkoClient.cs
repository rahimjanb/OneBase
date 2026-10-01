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

    /// <summary>Размер страницы по умолчанию. Максимум Linko по документации — 1000.</summary>
    public int PageSize { get; set; } = 1000;

    /// <summary>
    /// Свой предел страницы у отдельных ресурсов. Возвраты — 500: так отдаёт сервер (из опыта интеграции);
    /// если сервер на самом деле отдаёт меньше лимита, клиент это замечает и подстраивается сам.
    /// </summary>
    public Dictionary<string, int> PageSizes { get; set; } = new() { ["order_returns"] = 500, ["stock_transfers"] = 200 };

    /// <summary>
    /// Сколько запросов к Linko одновременно — на весь процесс, не на одну выгрузку. Больше четырёх нельзя:
    /// 25.07.2026 двенадцать потоков положили прод Linko, и IP получил блок на сутки.
    /// </summary>
    public int Parallelism { get; set; } = 4;

    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>Попыток на запрос (первая + повторы). Повторяются только сетевые ошибки, таймауты, 429 и 5xx.</summary>
    public int MaxAttempts { get; set; } = 4;

    /// <summary>Паузы перед повторами, секунды. Retry-After сервера важнее, если он есть (но не дольше MaxRetryAfterSeconds).</summary>
    public int[] RetryDelaysSeconds { get; set; } = [3, 6, 9];

    public int MaxRetryAfterSeconds { get; set; } = 60;

    public const int MaxParallelism = 4;

    public int PageSizeFor(string entity) =>
        Math.Clamp(PageSizes.TryGetValue(entity, out var size) ? size : PageSize, 1, 1000);
}

public sealed class LinkoApiException(string message, HttpStatusCode? status = null) : Exception(message)
{
    public HttpStatusCode? Status { get; } = status;
}

/// <summary>Общий на процесс ограничитель одновременных запросов к Linko.</summary>
public sealed class LinkoThrottle(int parallelism)
{
    public SemaphoreSlim Gate { get; } = new(Math.Clamp(parallelism, 1, LinkoOptions.MaxParallelism));
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
public sealed class LinkoClient(HttpClient http, LinkoOptions options, LinkoSettingsStore settings, LinkoThrottle throttle, ILogger<LinkoClient> logger)
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
        var result = await SendAsync<LinkoCount>(await ReadyAsync(ct), $"{entity}_count/", filters, options.MaxAttempts, ct);
        return result.Count;
    }

    /// <summary>Ресурс, который отдаётся простым массивом без пагинации (currencies/).</summary>
    public async Task<List<T>> ListAsync<T>(string path, CancellationToken ct = default) =>
        await SendAsync<List<T>>(await ReadyAsync(ct), path, null, options.MaxAttempts, ct);

    /// <summary>Один GET с произвольным ответом — для диагностики (команды linko-audit, linko-page). Только чтение.</summary>
    public async Task<JsonElement> GetJsonAsync(string path, IDictionary<string, string?>? query = null, CancellationToken ct = default, int? maxAttempts = null) =>
        await SendAsync<JsonElement>(await ReadyAsync(ct), path, query, maxAttempts ?? options.MaxAttempts, ct);

    /// <summary>
    /// Выгружает все страницы списка (limit/offset). Каждая страница передаётся в onPage — большие выгрузки
    /// не держатся в памяти целиком. Конец набора — страница короче лимита.
    /// Защита от потерь: если первая страница короче запрошенного лимита, это может быть не конец, а предел сервера
    /// (у возвратов он меньше 1000). Тогда запрашивается следующая страница: пустая — набор кончился,
    /// непустая — дальше идём шагом, который реально отдаёт сервер, иначе строки между ними пропали бы.
    /// </summary>
    public async Task<int> ReadAllAsync<T>(
        string entity,
        IDictionary<string, string?>? filters,
        Func<IReadOnlyList<T>, Task> onPage,
        CancellationToken ct = default)
    {
        var connection = await ReadyAsync(ct);

        async Task<IReadOnlyList<T>> Page(int offset, int size)
        {
            var page = await SendAsync<LinkoPage<T>>(connection, $"{entity}/", PageQuery(filters, offset, size), options.MaxAttempts, ct);
            if (page.Errors is { Count: > 0 })
            {
                logger.LogWarning("Linko {Entity}: {Count} ошибок в ответе", entity, page.Errors.Count);
            }

            return page.Results ?? [];
        }

        return await LinkoPaging.ReadAllAsync(Page, options.PageSizeFor(entity), Math.Clamp(options.Parallelism, 1, LinkoOptions.MaxParallelism), onPage,
            size => logger.LogInformation("Linko {Entity}: сервер отдаёт по {Size} строк за страницу", entity, size));
    }

    private static Dictionary<string, string?> PageQuery(IDictionary<string, string?>? filters, int offset, int limit) =>
        new(filters ?? new Dictionary<string, string?>())
        {
            ["limit"] = limit.ToString(),
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
        return (await SendAsync<LinkoStaffLastDate>(connection, StaffPrefix, "staff_balance/last_date/", query, false, options.MaxAttempts, ct)).LastDate;
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
        return await SendAsync<List<LinkoStaffBalanceDto>>(connection, StaffPrefix, "staff_balance/", query, false, options.MaxAttempts, ct);
    }

    /// <summary>Строки staff_balance как есть — только для диагностики набора полей (linko-fields staff_balance); значения не сохраняются.</summary>
    public async Task<List<System.Text.Json.JsonElement>> StaffBalanceRawAsync(int year, int month, CancellationToken ct = default)
    {
        var connection = await ReadyPlansAsync(ct);
        var query = new Dictionary<string, string?>
        {
            ["year"] = year.ToString(),
            ["month"] = month.ToString(),
            ["token"] = connection.PlanToken,
            ["format"] = "json",
        };
        return await SendAsync<List<System.Text.Json.JsonElement>>(connection, StaffPrefix, "staff_balance/", query, false, options.MaxAttempts, ct);
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

    private Task<T> SendAsync<T>(LinkoConnectionSettings connection, string path, IDictionary<string, string?>? query, int maxAttempts, CancellationToken ct) =>
        SendAsync<T>(connection, Prefix, path, query, externalAuth: true, maxAttempts, ct);

    /// <summary>
    /// GET с ограниченными повторами (только чтение — POST сюда не ходят и не повторяются).
    /// Повторяются сетевые ошибки, таймауты, 429 и 5xx (в том числе 502 под нагрузкой): паузы 3, 6, 9 с,
    /// всего не больше MaxAttempts попыток; Retry-After сервера важнее. Одновременно — не больше Parallelism запросов на процесс.
    /// В сообщения об ошибках попадает только path — без параметров (там бывает токен API планов) и без тела ответа.
    /// </summary>
    private async Task<T> SendAsync<T>(
        LinkoConnectionSettings connection,
        string prefix,
        string path,
        IDictionary<string, string?>? query,
        bool externalAuth,
        int maxAttempts,
        CancellationToken ct)
    {
        var url = new Uri(new Uri(connection.BaseUrl.TrimEnd('/') + "/"), prefix + path + BuildQuery(query));
        maxAttempts = Math.Max(1, maxAttempts);

        for (var attempt = 1; ; attempt++)
        {
            TimeSpan? retryAfter = null;
            await throttle.Gate.WaitAsync(ct);
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
                    try
                    {
                        return await response.Content.ReadFromJsonAsync<T>(Json, ct)
                            ?? throw new LinkoApiException($"Linko {path}: пустой ответ");
                    }
                    catch (JsonException)
                    {
                        throw new LinkoApiException($"Linko {path}: ответ не в формате JSON API (проверьте адрес сервера)");
                    }
                }

                var status = response.StatusCode;
                if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    throw new LinkoApiException($"Linko {path}: сервер не принял токен (HTTP {(int)status})", status);
                }

                var retryable = status is HttpStatusCode.TooManyRequests || (int)status >= 500;
                if (!retryable || attempt >= maxAttempts)
                {
                    throw new LinkoApiException($"Linko {path}: HTTP {(int)status}", status);
                }

                retryAfter = RetryAfter(response);
                logger.LogWarning("Linko {Path}: HTTP {Status}, попытка {Attempt} из {Max}", path, (int)status, attempt, maxAttempts);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                if (attempt >= maxAttempts)
                {
                    throw new LinkoApiException(ex is TaskCanceledException
                        ? $"Linko {path}: нет ответа за {options.TimeoutSeconds} с"
                        : $"Linko {path}: сетевая ошибка ({ex.GetType().Name})");
                }

                logger.LogWarning("Linko {Path}: {Error}, попытка {Attempt} из {Max}", path, ex.GetType().Name, attempt, maxAttempts);
            }
            finally
            {
                throttle.Gate.Release();
            }

            await Task.Delay(retryAfter ?? RetryDelay(attempt), ct);
        }
    }

    /// <summary>Пауза перед повтором номер `attempt` (1 — после первой неудачи): 3, 6, 9 с.</summary>
    private TimeSpan RetryDelay(int attempt)
    {
        var delays = options.RetryDelaysSeconds is { Length: > 0 } d ? d : [3, 6, 9];
        return TimeSpan.FromSeconds(delays[Math.Min(attempt, delays.Length) - 1]);
    }

    private TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        var wait = header?.Delta ?? (header?.Date is { } date ? date - DateTimeOffset.UtcNow : null);
        return wait is { } w && w > TimeSpan.Zero
            ? TimeSpan.FromSeconds(Math.Min(w.TotalSeconds, options.MaxRetryAfterSeconds))
            : null;
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

}
