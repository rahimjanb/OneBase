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

    /// <summary>Из .env: LINKO_BASE_URL, например https://sfademo.linko.uz</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Из .env: LINKO_TOKEN.</summary>
    public string Token { get; set; } = string.Empty;

    public int PageSize { get; set; } = 1000;
    public int TimeoutSeconds { get; set; } = 60;
    public int MaxRetries { get; set; } = 4;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(Token);
}

public sealed class LinkoApiException(string message, HttpStatusCode? status = null) : Exception(message)
{
    public HttpStatusCode? Status { get; } = status;
}

/// <summary>Клиент Linko External API: пагинация limit/offset, повторы с backoff, разбор поля errors.</summary>
public sealed class LinkoClient(HttpClient http, LinkoOptions options, ILogger<LinkoClient> logger)
{
    private const string Prefix = "api/v1/integration/external-api/";

    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public static void Configure(HttpClient http, LinkoOptions options)
    {
        http.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("External", options.Token);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<long> CountAsync(string entity, IDictionary<string, string?>? filters = null, CancellationToken ct = default)
    {
        var result = await SendAsync<LinkoCount>($"{entity}_count/", filters, ct);
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
        var total = 0;
        for (var offset = 0; ; offset += options.PageSize)
        {
            var query = new Dictionary<string, string?>(filters ?? new Dictionary<string, string?>())
            {
                ["limit"] = options.PageSize.ToString(),
                ["offset"] = offset.ToString(),
            };

            var page = await SendAsync<LinkoPage<T>>($"{entity}/", query, ct);
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

    private async Task<T> SendAsync<T>(string path, IDictionary<string, string?>? query, CancellationToken ct)
    {
        var url = Prefix + path + BuildQuery(query);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var response = await http.GetAsync(url, ct);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<T>(Json, ct)
                        ?? throw new LinkoApiException($"Linko {path}: пустой ответ");
                }

                var body = await response.Content.ReadAsStringAsync(ct);
                var retryable = response.StatusCode is HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
                if (!retryable || attempt > options.MaxRetries)
                {
                    throw new LinkoApiException(
                        $"Linko {path}: HTTP {(int)response.StatusCode} {Truncate(body)}", response.StatusCode);
                }

                logger.LogWarning("Linko {Path}: HTTP {Status}, попытка {Attempt}", path, (int)response.StatusCode, attempt);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                if (attempt > options.MaxRetries)
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
