using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OneBase.Application.Abstractions;
using OneBase.Application.DataSources;
using OneBase.Domain.Integrations;

namespace OneBase.Infrastructure.DataSources;

/// <summary>
/// Таблица Google целиком как xlsx → листы и значения ячеек (тот же разбор, что у отчётов AI Data Manager). Только чтение.
/// По ссылке — выгрузка docs.google.com/…/export?format=xlsx (таблица должна быть открыта «всем, у кого есть ссылка»).
/// Сервисным аккаунтом — Drive API files.export (scope drive.readonly) с токеном OAuth по JWT из ключа; загруженный в Drive файл
/// .xlsx (не таблица Google) — files.get?alt=media.
/// </summary>
public sealed class GoogleSheetsClient(HttpClient http, IReportReader reader) : IGoogleSheetsClient
{
    private const string XlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string Scope = "https://www.googleapis.com/auth/drive.readonly";

    /// <summary>Таблица больше — не читаем: выгрузка Google такого размера — это уже не справочник плана.</summary>
    private const long MaxBytes = 20 * 1024 * 1024;

    private static readonly ConcurrentDictionary<string, (string Token, DateTimeOffset Expires)> Tokens = new();

    public async Task<ReportDocument> ReadAsync(SheetSource source, CancellationToken ct = default)
    {
        var bytes = source.Access == SheetAccess.ServiceAccount
            ? await ServiceAccountAsync(source, ct)
            : await LinkAsync(source.SpreadsheetId, ct);
        try
        {
            using var stream = new MemoryStream(bytes);
            return reader.Read(stream, "google-sheet.xlsx");
        }
        catch (NotSupportedException ex)
        {
            throw new DataSourceException($"Таблицу не удалось прочитать: {ex.Message}");
        }
    }

    private async Task<byte[]> LinkAsync(string id, CancellationToken ct)
    {
        using var response = await SendAsync(new HttpRequestMessage(HttpMethod.Get, $"https://docs.google.com/spreadsheets/d/{id}/export?format=xlsx"), ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new DataSourceException("Таблица не найдена — проверьте ссылку.");
        }

        // Закрытая таблица: Google отвечает страницей входа (HTML) или 401/403, а не файлом.
        var type = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden || type.Contains("html", StringComparison.OrdinalIgnoreCase))
        {
            throw new DataSourceException("Таблица закрыта. Откройте доступ «Все, у кого есть ссылка — читатель» или подключите сервисный аккаунт Google.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new DataSourceException($"Google не отдал таблицу: HTTP {(int)response.StatusCode}.");
        }

        return await ReadBodyAsync(response, ct);
    }

    private async Task<byte[]> ServiceAccountAsync(SheetSource source, CancellationToken ct)
    {
        var key = ServiceAccountKey.Parse(source.ServiceAccountJson);
        var token = await TokenAsync(key, ct);
        var id = Uri.EscapeDataString(source.SpreadsheetId);
        using var exported = await SendAsync(Authorized(token, $"https://www.googleapis.com/drive/v3/files/{id}/export?mimeType={Uri.EscapeDataString(XlsxMime)}"), ct);
        if (exported.IsSuccessStatusCode)
        {
            return await ReadBodyAsync(exported, ct);
        }

        if (exported.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden && !await IsNotExportableAsync(exported, ct))
        {
            throw new DataSourceException($"Нет доступа к таблице: поделитесь ею с {key.Email} (читатель) или проверьте ссылку.");
        }

        // Загруженный в Drive файл Excel: экспортировать нечего — скачиваем как есть.
        using var media = await SendAsync(Authorized(token, $"https://www.googleapis.com/drive/v3/files/{id}?alt=media&supportsAllDrives=true"), ct);
        if (!media.IsSuccessStatusCode)
        {
            throw new DataSourceException(media.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden
                ? $"Нет доступа к таблице: поделитесь ею с {key.Email} (читатель) или проверьте ссылку."
                : $"Google Drive не отдал таблицу: HTTP {(int)media.StatusCode}.");
        }

        return await ReadBodyAsync(media, ct);
    }

    private static async Task<bool> IsNotExportableAsync(HttpResponseMessage response, CancellationToken ct) =>
        (await response.Content.ReadAsStringAsync(ct)).Contains("fileNotExportable", StringComparison.Ordinal);

    private async Task<string> TokenAsync(ServiceAccountKey key, CancellationToken ct)
    {
        if (Tokens.TryGetValue(key.Email, out var cached) && cached.Expires > DateTimeOffset.UtcNow.AddMinutes(2))
        {
            return cached.Token;
        }

        var now = DateTimeOffset.UtcNow;
        var assertion = Jwt(key, now);
        using var request = new HttpRequestMessage(HttpMethod.Post, key.TokenUri)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = assertion,
            }),
        };
        using var response = await SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new DataSourceException("Google не принял ключ сервисного аккаунта: проверьте, что ключ действующий и Drive API включён в проекте.");
        }

        using var json = JsonDocument.Parse(body);
        var token = json.RootElement.GetProperty("access_token").GetString() ?? throw new DataSourceException("Google не вернул токен доступа.");
        var expires = json.RootElement.TryGetProperty("expires_in", out var e) && e.TryGetInt32(out var seconds) ? seconds : 3600;
        Tokens[key.Email] = (token, now.AddSeconds(expires));
        return token;
    }

    /// <summary>JWT для обмена на токен OAuth: RS256 закрытым ключом сервисного аккаунта, срок — час.</summary>
    public static string Jwt(ServiceAccountKey key, DateTimeOffset now)
    {
        static string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var header = B64(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
        var claims = B64(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = key.Email,
            scope = Scope,
            aud = key.TokenUri,
            iat = now.ToUnixTimeSeconds(),
            exp = now.AddHours(1).ToUnixTimeSeconds(),
        }));
        using var rsa = RSA.Create();
        rsa.ImportFromPem(key.PrivateKey);
        var signature = rsa.SignData(Encoding.ASCII.GetBytes($"{header}.{claims}"), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{header}.{claims}.{B64(signature)}";
    }

    private static HttpRequestMessage Authorized(string token, string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new DataSourceException($"Нет связи с Google: {ex.Message}");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new DataSourceException("Google не ответил вовремя — попробуйте ещё раз.");
        }
    }

    private static async Task<byte[]> ReadBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.Content.Headers.ContentLength > MaxBytes)
        {
            throw new DataSourceException("Таблица больше 20 МБ — для синхронизации нужна таблица поменьше.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxBytes)
            {
                throw new DataSourceException("Таблица больше 20 МБ — для синхронизации нужна таблица поменьше.");
            }
        }

        return buffer.ToArray();
    }
}

/// <summary>Ключ сервисного аккаунта Google (JSON из консоли Google Cloud): почта, закрытый ключ, адрес выдачи токенов.</summary>
public sealed record ServiceAccountKey(string Email, string PrivateKey, string TokenUri)
{
    /// <exception cref="DataSourceException">Не JSON-ключ сервисного аккаунта.</exception>
    public static ServiceAccountKey Parse(string? json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json ?? string.Empty);
            var root = doc.RootElement;
            var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
            var email = root.TryGetProperty("client_email", out var e) ? e.GetString() : null;
            var key = root.TryGetProperty("private_key", out var k) ? k.GetString() : null;
            var uri = root.TryGetProperty("token_uri", out var u) ? u.GetString() : null;
            if (type != "service_account" || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(key))
            {
                throw new DataSourceException("Это не ключ сервисного аккаунта: нужен JSON с type «service_account», client_email и private_key.");
            }

            return new ServiceAccountKey(email.Trim(), key, string.IsNullOrWhiteSpace(uri) ? "https://oauth2.googleapis.com/token" : uri);
        }
        catch (JsonException)
        {
            throw new DataSourceException("Ключ сервисного аккаунта — не JSON. Скачайте ключ в Google Cloud: IAM → Сервисные аккаунты → Ключи → JSON.");
        }
    }
}
