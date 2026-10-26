using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OneBase.Infrastructure.Logging;

namespace OneBase.Infrastructure.Hr;

public sealed class VerifixOptions
{
    public const string Section = "Verifix";

    /// <summary>Адрес API по документации — https://app.verifix.com. Можно поменять в настройках интеграции.</summary>
    public string BaseUrl { get; set; } = "https://app.verifix.com";

    /// <summary>Запасные client_id и client_secret из окружения (Verifix__ClientId, Verifix__ClientSecret), если они не заданы в OneBase.</summary>
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>Попыток на запрос (первая + повторы). Повторяются только сетевые ошибки, таймауты, 429 и 5xx.</summary>
    public int MaxAttempts { get; set; } = 4;

    /// <summary>Паузы перед повторами, секунды; Retry-After сервера важнее (не дольше MaxRetryAfterSeconds).</summary>
    public int[] RetryDelaysSeconds { get; set; } = [2, 5, 10];

    public int MaxRetryAfterSeconds { get; set; } = 60;

    /// <summary>Предел страниц одной выгрузки — защита от бесконечного цикла, если cursor перестанет двигаться.</summary>
    public int MaxPages { get; set; } = 5000;

    /// <summary>Фоновая синхронизация по расписанию из настроек интеграции. false — только кнопка «Синхронизировать».</summary>
    public bool WorkerEnabled { get; set; } = true;
}

/// <summary>Вид ошибки Verifix — от него зависят повтор, сообщение человеку и итог шага.</summary>
public enum VerifixErrorKind
{
    NotConfigured,
    Auth,
    Forbidden,
    NotFound,
    BadRequest,
    RateLimited,
    Server,
    Network,
    Format,
}

public sealed class VerifixApiException(string message, VerifixErrorKind kind, HttpStatusCode? status = null, string? reason = null) : Exception(message)
{
    public VerifixErrorKind Kind { get; } = kind;
    public HttpStatusCode? Status { get; } = status;

    /// <summary>Причина из ответа Verifix (текст ошибки, обрезан и без секретов), например «Пользователь не прикреплен ни к одному филиалу или проекту».</summary>
    public string? Reason { get; } = reason;

    /// <summary>Временная ошибка — повтор синхронизации может пройти.</summary>
    public bool Transient => Kind is VerifixErrorKind.RateLimited or VerifixErrorKind.Server or VerifixErrorKind.Network;
}

public sealed record VerifixCheck(string Method, bool Ok, string Message);

public sealed record VerifixTestResult(bool Ok, string Message, string? Organization, IReadOnlyList<VerifixCheck> Checks, long ElapsedMs);

/// <summary>
/// Клиент Verifix Public API: OAuth2 client credentials (POST /security/oauth/token; токен живёт expires_in секунд и запрашивается заново
/// по истечении), методы — POST {base}/b/vhr/api/v1/{модуль}/{сущность}$list с JSON-телом, заголовки project_code: vhr, filial_id
/// (необязателен с client credentials), cursor и limit; конец выгрузки — meta.next_cursor = -1.
/// Повторы ограничены: сетевые ошибки, таймауты, 429 и 5xx — до MaxAttempts попыток с паузами; 401 — один раз с новым токеном.
/// Токен, client_secret и тела ответов в журнал не пишутся: в сообщениях только метод и код HTTP.
/// </summary>
public sealed class VerifixClient(HttpClient http, VerifixOptions options, VerifixTokenCache tokens, ILogger<VerifixClient> logger)
{
    public const string ProjectCode = "vhr";
    private const string ApiPrefix = "b/vhr/api/v1/";




    /// <summary>Все страницы метода $list: каждая передаётся в onPage. Возвращает число строк.</summary>
    public async Task<int> ListAsync<T>(VerifixConnection connection, string method, object body, int limit, Func<IReadOnlyList<T>, Task> onPage, CancellationToken ct)
    {
        long? cursor = null;
        var total = 0;
        var seen = new HashSet<long>();
        for (var page = 1; ; page++)
        {
            if (page > options.MaxPages)
            {
                throw new VerifixApiException($"Verifix {method}: больше {options.MaxPages} страниц — выгрузка остановлена", VerifixErrorKind.Format);
            }

            var result = await SendAsync<VerifixPage<T>>(connection, method, body, cursor, limit, ct);
            var items = result.Data ?? [];
            if (items.Count > 0)
            {
                await onPage(items);
                total += items.Count;
            }

            var next = result.Meta?.NextCursor;
            // Конец — next_cursor = -1 (по документации) или пустая страница. Тот же cursor второй раз — сервер не двигается: стоп, а не цикл.
            if (next is null or -1 || items.Count == 0)
            {
                return total;
            }

            if (!seen.Add(next.Value))
            {
                logger.LogWarning("Verifix {Method}: cursor не меняется — выгрузка остановлена на странице {Page}", method, page);
                return total;
            }

            cursor = next;
        }
    }

    /// <summary>Один запрос без пагинации (filial$info).</summary>
    public Task<T> PostAsync<T>(VerifixConnection connection, string method, object? body, CancellationToken ct) =>
        SendAsync<T>(connection, method, body ?? new { }, null, null, ct);

    /// <summary>
    /// Проверка подключения: токен, организация (filial$info) и по одной странице каждого используемого метода. Без повторов —
    /// человек быстро получает ответ; какой метод недоступен ролью клиента — видно по строкам проверки.
    /// </summary>
    public async Task<VerifixTestResult> TestAsync(VerifixConnection connection, DateOnly today, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        if (!connection.HasCredentials)
        {
            return new VerifixTestResult(false, "Задайте client_id и client_secret.", null, [], 0);
        }

        if (!VerifixSettingsStore.IsValidUrl(connection.BaseUrl))
        {
            return new VerifixTestResult(false, "Адрес API должен начинаться с https://", null, [], 0);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        var probe = connection with { };
        try
        {
            await TokenAsync(probe, force: true, maxAttempts: 1, timeout.Token);
        }
        catch (VerifixApiException ex)
        {
            return new VerifixTestResult(false, Human(ex), null, [], watch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new VerifixTestResult(false, "Verifix не ответил за 45 секунд.", null, [], watch.ElapsedMilliseconds);
        }

        string? organization = null;
        var checks = new List<VerifixCheck>();
        async Task Check(string title, string method, object body, Func<Task<string>> run)
        {
            try
            {
                checks.Add(new VerifixCheck(title, true, await run()));
            }
            catch (VerifixApiException ex)
            {
                checks.Add(new VerifixCheck(title, false, Human(ex)));
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                checks.Add(new VerifixCheck(title, false, "нет ответа"));
            }
        }

        await Check("Организация", "core/filial$info", new { }, async () =>
        {
            var filial = await SendAsync<VerifixFilialDto>(probe, "core/filial$info", new { }, null, null, timeout.Token, maxAttempts: 1);
            organization = filial.FilialName;
            return filial.FilialName ?? "доступна";
        });
        var day = VerifixFormats.Date(today);
        await Check("Сотрудники", "core/employee$list", new { }, async () =>
            Rows(await SendAsync<VerifixPage<VerifixEmployeeDto>>(probe, "core/employee$list", new { employee_ids = Array.Empty<long>(), statuses = Array.Empty<string>() }, null, 1, timeout.Token, 1)));
        await Check("Подразделения", "core/division$list", new { }, async () =>
            Rows(await SendAsync<VerifixPage<VerifixDivisionDto>>(probe, "core/division$list", new { }, null, 1, timeout.Token, 1)));
        await Check("Отметки", "core/track$list", new { }, async () =>
            Rows(await SendAsync<VerifixPage<VerifixTrackDto>>(probe, "core/track$list",
                new { begin_datetime = $"{day} 00:00:00", end_datetime = $"{day} 23:59:59" }, null, 1, timeout.Token, 1)));
        await Check("Табель посещений", "core/timesheet$export", new { }, async () =>
            Rows(await SendAsync<VerifixPage<VerifixTimesheetDto>>(probe, "core/timesheet$export",
                new { period_begin_date = day, period_end_date = day, division_ids = Array.Empty<long>(), employee_ids = Array.Empty<long>() }, null, 1, timeout.Token, 1)));

        var essential = checks.Where(c => c.Method is "Сотрудники" or "Отметки").ToList();
        var ok = essential.Count == 2 && essential.All(c => c.Ok);
        var message = ok
            ? checks.All(c => c.Ok) ? "Подключение работает." : "Подключение работает, но часть методов недоступна роли клиента OAuth2 — см. проверки."
            : "Токен получен, но сотрудники или отметки недоступны роли клиента OAuth2 в Verifix.";
        return new VerifixTestResult(ok, message, organization, checks, watch.ElapsedMilliseconds);

        static string Rows<TItem>(VerifixPage<TItem> page) => (page.Data?.Count ?? 0) > 0 ? "доступно, данные есть" : "доступно, за сегодня данных нет";
    }

    /// <summary>
    /// Текст ошибки для человека: без адресов и токенов; причина из ответа Verifix — в кавычках, если она есть. 401 при выданном токене —
    /// не неверные ключи, а нет доступа: так Verifix отвечает, когда клиент OAuth2 не привязан к организации.
    /// </summary>
    public static string Human(VerifixApiException ex)
    {
        var text = HumanKind(ex);
        if (string.IsNullOrWhiteSpace(ex.Reason))
        {
            return text;
        }

        text += $" Verifix: «{ex.Reason}».";
        if (ex.Reason.Contains("филиал", StringComparison.OrdinalIgnoreCase) || ex.Reason.Contains("проект", StringComparison.OrdinalIgnoreCase))
        {
            text += " Привяжите клиента OAuth2 к организации, из которой загружаются данные (Администрирование → Настройки → Внешние системы → "
                    + "«Клиенты OAuth2 для сервера для компании»), и назначьте его роль в этой организации.";
        }

        return text;
    }

    private static string HumanKind(VerifixApiException ex) => ex.Kind switch
    {
        VerifixErrorKind.NotConfigured => ex.Message,
        VerifixErrorKind.Auth => "Verifix не принял client_id или client_secret.",
        VerifixErrorKind.Forbidden => ex.Status == HttpStatusCode.Unauthorized
            ? "Токен выдан, но Verifix не дал доступ к этому методу (HTTP 401)."
            : "Нет доступа (HTTP 403): роли клиента OAuth2 в Verifix не прикреплена эта форма «Внешние системы».",
        VerifixErrorKind.NotFound => "Метод не найден (HTTP 404) — проверьте адрес API или тариф Verifix.",
        VerifixErrorKind.BadRequest => $"Verifix отклонил запрос (HTTP {(int?)ex.Status ?? 400}).",
        VerifixErrorKind.RateLimited => "Verifix ограничил частоту запросов (HTTP 429).",
        VerifixErrorKind.Server => $"Ошибка сервера Verifix (HTTP {(int?)ex.Status ?? 500}).",
        VerifixErrorKind.Network => "Verifix недоступен (сеть или таймаут).",
        _ => "Verifix ответил не в формате API.",
    };

    /// <summary>Текст ошибки из ответа: поле error / message / error_description JSON или сам текст; без HTML, до 200 символов, без секретов.</summary>
    private static async Task<string?> ReasonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        string text;
        try
        {
            text = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        text = text.Length > 2000 ? text[..2000] : text;
        try
        {
            using var json = JsonDocument.Parse(text);
            if (json.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var name in new[] { "error_description", "message", "error" })
                {
                    if (json.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                    {
                        text = value.GetString() ?? string.Empty;
                        break;
                    }
                }
            }
        }
        catch (JsonException)
        {
        }

        text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]*>", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
        if (text.Length == 0)
        {
            return null;
        }

        return VerifixLog.Safe(text.Length > 200 ? text[..200] + "…" : text);
    }

    private async Task<T> SendAsync<T>(VerifixConnection connection, string method, object body, long? cursor, int? limit, CancellationToken ct, int? maxAttempts = null)
    {
        var attempts = Math.Max(1, maxAttempts ?? options.MaxAttempts);
        var url = new Uri(new Uri(connection.BaseUrl.TrimEnd('/') + "/"), ApiPrefix + method);
        var reauthorized = false;
        for (var attempt = 1; ; attempt++)
        {
            TimeSpan? retryAfter = null;
            var token = await TokenAsync(connection, force: false, attempts, ct);
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body, options: VerifixFormats.Json) };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.TryAddWithoutValidation("project_code", ProjectCode);
                if (connection.FilialId is { } filial)
                {
                    request.Headers.TryAddWithoutValidation("filial_id", filial.ToString());
                }

                if (cursor is { } c)
                {
                    request.Headers.TryAddWithoutValidation("cursor", c.ToString());
                }

                if (limit is { } l)
                {
                    request.Headers.TryAddWithoutValidation("limit", l.ToString());
                }

                using var response = await http.SendAsync(request, ct);
                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        return await response.Content.ReadFromJsonAsync<T>(VerifixFormats.Json, ct)
                               ?? throw new VerifixApiException($"Verifix {method}: пустой ответ", VerifixErrorKind.Format);
                    }
                    catch (JsonException)
                    {
                        throw new VerifixApiException($"Verifix {method}: ответ не в формате API", VerifixErrorKind.Format);
                    }
                }

                var status = response.StatusCode;
                if (status == HttpStatusCode.Unauthorized)
                {
                    // Токен мог истечь раньше срока — один раз берём новый.
                    if (!reauthorized)
                    {
                        reauthorized = true;
                        Forget();
                        attempt--;
                        continue;
                    }

                    // Новый токен выдан (ключи верны), а метод всё равно 401 — это отказ в доступе (например, клиент не привязан к организации),
                    // не неверные ключи: остальные методы синхронизации пробуются дальше.
                    var denied = await ReasonAsync(response, ct);
                    throw new VerifixApiException($"Verifix {method}: нет доступа (HTTP 401){(denied is null ? "" : $" — {denied}")}", VerifixErrorKind.Forbidden, status, denied);
                }

                var kind = Kind(status);
                if (kind is not (VerifixErrorKind.RateLimited or VerifixErrorKind.Server) || attempt >= attempts)
                {
                    var reason = await ReasonAsync(response, ct);
                    throw new VerifixApiException($"Verifix {method}: HTTP {(int)status}{(reason is null ? "" : $" — {reason}")}", kind, status, reason);
                }

                retryAfter = RetryAfter(response);
                logger.LogWarning("Verifix {Method}: HTTP {Status}, попытка {Attempt} из {Max}", method, (int)status, attempt, attempts);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                if (attempt >= attempts)
                {
                    throw new VerifixApiException(ex is TaskCanceledException
                        ? $"Verifix {method}: нет ответа за {options.TimeoutSeconds} с"
                        : $"Verifix {method}: сетевая ошибка ({ex.GetType().Name})", VerifixErrorKind.Network);
                }

                logger.LogWarning("Verifix {Method}: {Error}, попытка {Attempt} из {Max}", method, ex.GetType().Name, attempt, attempts);
            }

            await Task.Delay(retryAfter ?? RetryDelay(attempt), ct);
        }
    }

    /// <summary>Действующий access_token: из памяти процесса до истечения (с запасом 2 минуты), иначе новый. Нигде не сохраняется и не логируется.</summary>
    private async Task<string> TokenAsync(VerifixConnection connection, bool force, int maxAttempts, CancellationToken ct)
    {
        if (!connection.HasCredentials)
        {
            throw new VerifixApiException("Verifix не настроен: задайте client_id и client_secret в «Настройки → Интеграции → HR → Verifix».", VerifixErrorKind.NotConfigured);
        }

        var key = KeyOf(connection);
        if (!force && tokens.Current is { } cached && cached.Key == key && cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return cached.Token;
        }

        await tokens.Gate.WaitAsync(ct);
        try
        {
            if (!force && tokens.Current is { } again && again.Key == key && again.ExpiresAt > DateTimeOffset.UtcNow)
            {
                return again.Token;
            }

            var url = new Uri(new Uri(connection.BaseUrl.TrimEnd('/') + "/"), "security/oauth/token");
            var body = new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = connection.ClientId,
                ["client_secret"] = connection.ClientSecret,
                ["scope"] = "read",
            };
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    using var response = await http.PostAsJsonAsync(url, body, ct);
                    if (response.IsSuccessStatusCode)
                    {
                        VerifixToken? token;
                        try
                        {
                            token = await response.Content.ReadFromJsonAsync<VerifixToken>(VerifixFormats.Json, ct);
                        }
                        catch (JsonException)
                        {
                            throw new VerifixApiException("Verifix: ответ на запрос токена не в формате OAuth2", VerifixErrorKind.Format);
                        }

                        if (string.IsNullOrWhiteSpace(token?.AccessToken))
                        {
                            throw new VerifixApiException("Verifix: в ответе нет access_token", VerifixErrorKind.Format);
                        }

                        var lifetime = TimeSpan.FromSeconds(Math.Max(60, (token.ExpiresIn ?? 3600) - 120));
                        tokens.Current = (key, token.AccessToken, DateTimeOffset.UtcNow + lifetime);
                        return token.AccessToken;
                    }

                    var status = response.StatusCode;
                    var kind = status is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ? VerifixErrorKind.Auth : Kind(status);
                    if (kind is not (VerifixErrorKind.RateLimited or VerifixErrorKind.Server) || attempt >= maxAttempts)
                    {
                        // Причина отказа в токене (например, invalid_client) — без самих ключей.
                        var reason = await ReasonAsync(response, ct);
                        throw new VerifixApiException($"Verifix: токен не выдан (HTTP {(int)status})", kind, status, reason);
                    }

                    logger.LogWarning("Verifix: токен не выдан (HTTP {Status}), попытка {Attempt} из {Max}", (int)status, attempt, maxAttempts);
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
                {
                    if (attempt >= maxAttempts)
                    {
                        throw new VerifixApiException($"Verifix: сервер авторизации недоступен ({ex.GetType().Name})", VerifixErrorKind.Network);
                    }
                }

                await Task.Delay(RetryDelay(attempt), ct);
            }
        }
        finally
        {
            tokens.Gate.Release();
        }
    }

    /// <summary>Сбросить токен (после 401 или смены настроек).</summary>
    public void Forget() => tokens.Current = null;

    private static VerifixErrorKind Kind(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized => VerifixErrorKind.Auth,
        HttpStatusCode.Forbidden => VerifixErrorKind.Forbidden,
        HttpStatusCode.NotFound => VerifixErrorKind.NotFound,
        HttpStatusCode.TooManyRequests => VerifixErrorKind.RateLimited,
        HttpStatusCode.RequestTimeout => VerifixErrorKind.Server,
        _ when (int)status >= 500 => VerifixErrorKind.Server,
        _ => VerifixErrorKind.BadRequest,
    };

    /// <summary>Ключ кэша токена — без самого секрета: хэш адреса, клиента, секрета и организации.</summary>
    private static string KeyOf(VerifixConnection c) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{c.BaseUrl}|{c.ClientId}|{c.ClientSecret}|{c.FilialId}")));

    private TimeSpan RetryDelay(int attempt)
    {
        var delays = options.RetryDelaysSeconds is { Length: > 0 } d ? d : [2, 5, 10];
        return TimeSpan.FromSeconds(delays[Math.Min(attempt, delays.Length) - 1]);
    }

    private TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        var wait = header?.Delta ?? (header?.Date is { } date ? date - DateTimeOffset.UtcNow : null);
        return wait is { } w && w > TimeSpan.Zero ? TimeSpan.FromSeconds(Math.Min(w.TotalSeconds, options.MaxRetryAfterSeconds)) : null;
    }
}

internal static class VerifixLog
{
    /// <summary>Текст ошибки для журнала синхронизации: без секретов (тот же фильтр, что у журнала ошибок).</summary>
    public static string Safe(string message) => LogScrubber.Scrub(message.Length > 900 ? message[..900] : message);
}

/// <summary>access_token Verifix на весь процесс (клиент HTTP создаётся на каждый запрос). Только в памяти — не в базе и не в журнале.</summary>
public sealed class VerifixTokenCache
{
    public SemaphoreSlim Gate { get; } = new(1, 1);

    public (string Key, string Token, DateTimeOffset ExpiresAt)? Current { get; set; }
}
