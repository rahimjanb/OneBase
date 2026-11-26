using System.Globalization;
using System.Net;
using System.Text.Json;
using OneBase.Application.Sales.Tajik;

namespace OneBase.Infrastructure.Tajik;

/// <summary>Настройки 1С Таджикистан из окружения (секция TajikSales): запасной адрес и ключ, если они не заданы в OneBase.</summary>
public sealed class TajikSalesOptions
{
    public const string Section = "TajikSales";

    /// <summary>Адрес API дилера (из документа «API (1).pdf»); ключ доступа — только на странице интеграции или в TajikSales__Token.</summary>
    public string BaseUrl { get; set; } = "http://62.60.246.93";

    public string Token { get; set; } = string.Empty;

    /// <summary>Фоновая синхронизация по расписанию (на стендах и в тестах — false).</summary>
    public bool WorkerEnabled { get; set; } = true;

    public int TimeoutSeconds { get; set; } = 120;
}

/// <summary>Строка продажи из ответа /sales.</summary>
public sealed record TajikApiSale(
    string Number,
    DateOnly Date,
    string Customer,
    string? Manager,
    string Code,
    string Name,
    decimal Qty,
    decimal Price,
    decimal Amount,
    decimal Discount,
    decimal? DiscountPercent);

/// <summary>Строка остатка из ответа /sklad.</summary>
public sealed record TajikApiStock(string Code, string Name, string Warehouse, decimal Qty);

/// <summary>Разобранный ответ: строки и сколько строк не удалось прочитать.</summary>
public sealed record TajikApiPage<T>(IReadOnlyList<T> Items, int Rejected);

/// <summary>Ошибка API 1С Таджикистана: текст без ключа доступа и без тела ответа.</summary>
public sealed class TajikApiException(string message, bool unauthorized = false) : Exception(message)
{
    public bool Unauthorized { get; } = unauthorized;
}

/// <summary>
/// Клиент API 1С дилера в Таджикистане (только GET): /sales?token=…&amp;from=ДД-ММ-ГГГГ — продажи с даты, /sklad?token=… — остаток.
/// Ключ передаётся в адресе (так устроен API) — поэтому у этого HttpClient нет журнала запросов (RemoveAllLoggers), а тексты ошибок
/// очищаются от ключа.
/// </summary>
public sealed class TajikApiClient(HttpClient http)
{
    public Task<TajikApiPage<TajikApiSale>> SalesAsync(string baseUrl, string token, DateOnly from, CancellationToken ct) =>
        GetAsync(Url(baseUrl, "sales", token, from), token, ParseSale, ct);

    public Task<TajikApiPage<TajikApiStock>> StockAsync(string baseUrl, string token, CancellationToken ct) =>
        GetAsync(Url(baseUrl, "sklad", token, null), token, ParseStock, ct);

    private static Uri Url(string baseUrl, string method, string token, DateOnly? from)
    {
        var query = "token=" + Uri.EscapeDataString(token) + (from is { } f ? "&from=" + TajikText.ApiDate(f) : string.Empty);
        if (!Uri.TryCreate(baseUrl.TrimEnd('/') + "/" + method + "?" + query, UriKind.Absolute, out var uri))
        {
            throw new TajikApiException("Неверный адрес API 1С Таджикистан.");
        }

        return uri;
    }

    private async Task<TajikApiPage<T>> GetAsync<T>(Uri uri, string token, Func<JsonElement, T?> parse, CancellationToken ct)
        where T : class
    {
        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync(uri, HttpCompletionOption.ResponseContentRead, ct);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TajikApiException($"1С Таджикистан не ответила за {http.Timeout.TotalSeconds:0} с.");
        }
        catch (HttpRequestException e)
        {
            throw new TajikApiException("Нет связи с 1С Таджикистан: " + Clean(e.Message, token));
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            JsonDocument? json = null;
            try
            {
                json = JsonDocument.Parse(body);
            }
            catch (JsonException)
            {
                // Не JSON — ошибку покажет код ответа ниже.
            }

            using (json)
            {
                var error = json?.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String
                    ? Clean(e.GetString(), token)
                    : null;
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    throw new TajikApiException($"1С Таджикистан не приняла ключ доступа ({(int)response.StatusCode}){(error is null ? "" : ": " + error)}.", unauthorized: true);
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new TajikApiException($"1С Таджикистан ответила {(int)response.StatusCode}{(error is null ? "" : ": " + error)}.");
                }

                if (json is null || json.RootElement.ValueKind != JsonValueKind.Object)
                {
                    throw new TajikApiException("1С Таджикистан вернула не JSON.");
                }

                var root = json.RootElement;
                var status = root.TryGetProperty("status", out var st) && st.ValueKind == JsonValueKind.String ? st.GetString() : null;
                if (!string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    throw new TajikApiException($"1С Таджикистан вернула статус «{Clean(status, token) ?? "нет"}»{(error is null ? "" : ": " + error)}.");
                }

                if (!root.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                {
                    throw new TajikApiException("В ответе 1С Таджикистан нет списка items.");
                }

                // count — сколько строк должно быть: расхождение значит, что ответ обрезан.
                if (root.TryGetProperty("count", out var count) && count.ValueKind == JsonValueKind.Number && count.TryGetInt32(out var declared)
                    && declared != items.GetArrayLength())
                {
                    throw new TajikApiException($"В ответе 1С Таджикистан {items.GetArrayLength()} строк, а count = {declared} — ответ неполный.");
                }

                var list = new List<T>(items.GetArrayLength());
                var rejected = 0;
                foreach (var item in items.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object && parse(item) is { } parsed)
                    {
                        list.Add(parsed);
                    }
                    else
                    {
                        rejected++;
                    }
                }

                return new TajikApiPage<T>(list, rejected);
            }
        }
    }

    private static TajikApiSale? ParseSale(JsonElement e)
    {
        var number = Text(e, "number");
        var customer = Text(e, "customer");
        var code = Text(e, "code");
        if (number is null || customer is null || code is null || !TajikText.TryParseApiDate(Text(e, "date"), out var date))
        {
            return null;
        }

        var qty = Number(e, "qty");
        var price = Number(e, "price");
        var amount = Number(e, "amount");
        if (qty is null || amount is null)
        {
            return null;
        }

        return new TajikApiSale(number, date, customer, Text(e, "manager"), code, Text(e, "name") ?? code, qty.Value, price ?? 0, amount.Value,
            Number(e, "discount") ?? 0, Number(e, "discountPercent"));
    }

    private static TajikApiStock? ParseStock(JsonElement e)
    {
        var code = Text(e, "code");
        var qty = Number(e, "qty");
        if (code is null || qty is null)
        {
            return null;
        }

        return new TajikApiStock(code, Text(e, "name") ?? code, Text(e, "warehouse") ?? "—", qty.Value);
    }

    private static string? Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString()?.Trim() is { Length: > 0 } s ? s : null;

    private static decimal? Number(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v))
        {
            return null;
        }

        if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d))
        {
            return d;
        }

        // Число строкой («69,35», «1 456.35»).
        return v.ValueKind == JsonValueKind.String
            && decimal.TryParse(v.GetString()?.Replace(" ", "").Replace(" ", "").Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var s)
                ? s
                : null;
    }

    /// <summary>Текст без ключа доступа, не длиннее 300 символов.</summary>
    public static string? Clean(string? text, string? token)
    {
        if (text is null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(token))
        {
            text = text.Replace(token, "•••", StringComparison.Ordinal).Replace(Uri.EscapeDataString(token), "•••", StringComparison.Ordinal);
        }

        return text.Length > 300 ? text[..300] : text;
    }
}
