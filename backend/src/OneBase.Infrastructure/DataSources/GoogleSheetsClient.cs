using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OneBase.Application.Abstractions;
using OneBase.Application.DataManager;
using OneBase.Application.DataSources;
using OneBase.Domain.Integrations;

namespace OneBase.Infrastructure.DataSources;

/// <summary>
/// Таблица Google → листы и значения ячеек в той же записи, что разбор отчётов AI Data Manager (числа — инвариантно, даты — ГГГГ-ММ-ДД).
/// Только чтение.
/// Сервисным аккаунтом — Google Sheets API (токен OAuth по JWT из ключа, scope drive.readonly): список листов и значения только нужных
/// листов — большая книга целиком не скачивается. Файл Excel, загруженный в Drive (не таблица Google), или Sheets API, выключенный
/// в проекте, — файлом xlsx через Drive API: files.export (до 10 МБ; больше — выгрузка docs.google.com с тем же токеном), файл Excel —
/// files.get?alt=media.
/// По ссылке — выгрузка docs.google.com/…/export?format=xlsx (таблица открыта «всем, у кого есть ссылка»).
/// </summary>
public sealed partial class GoogleSheetsClient(HttpClient http, IReportReader reader, DataManagerOptions options) : IGoogleSheetsClient
{
    private const string XlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string Scope = "https://www.googleapis.com/auth/drive.readonly";

    /// <summary>Файл больше — не читаем: выгрузка Google такого размера — это уже не справочник плана.</summary>
    private const long MaxBytes = 20 * 1024 * 1024;

    /// <summary>
    /// Файл xlsx разбирается в памяти целиком (ClosedXML): книгу, листы которой в распакованном виде больше, не разбираем — иначе
    /// не хватит памяти сервера. Такую таблицу читает только Sheets API, по листам.
    /// </summary>
    private const long MaxUnpackedBytes = 50 * 1024 * 1024;

    /// <summary>Ячеек за одно чтение через Sheets API — не больше: «все листы» большой книги заняли бы память сервера.</summary>
    private const long MaxCells = 3_000_000;

    /// <summary>Листов и ячеек (по размеру сетки) в одном запросе values:batchGet.</summary>
    private const int BatchSheets = 10;
    private const long BatchCells = 500_000;

    private const string LinkHint = "Подключите её сервисным аккаунтом Google — тогда читаются только выбранные листы.";
    private const string ExcelHint = "Это файл Excel в Google Drive: сохраните его как таблицу Google («Файл → Сохранить как таблицу Google») — тогда читаются только выбранные листы.";
    private const string SheetsApiHint = "Включите Google Sheets API в проекте Google Cloud сервисного аккаунта (APIs & Services → Library) — тогда читаются только выбранные листы.";

    private static readonly ConcurrentDictionary<string, (string Token, DateTimeOffset Expires)> Tokens = new();

    /// <summary>Ошибки формул: Sheets API отдаёт их текстом.</summary>
    private static readonly HashSet<string> CellErrors = ["#NULL!", "#DIV/0!", "#VALUE!", "#REF!", "#NAME?", "#NUM!", "#N/A", "#ERROR!"];

    private sealed record GridSheet(string Title, int Rows, int Columns);

    public async Task<IReadOnlyList<SheetInfo>> SheetsAsync(SheetSource source, CancellationToken ct = default)
    {
        if (source.Access == SheetAccess.ServiceAccount)
        {
            var (key, token) = await AuthorizeAsync(source, ct);
            var (grid, hint) = await GridAsync(source.SpreadsheetId, key, token, ct);
            return grid is not null
                ? grid.Select(g => new SheetInfo(g.Title, g.Rows, g.Columns)).ToList()
                : Info(await DriveBookAsync(source.SpreadsheetId, key, token, hint, ct));
        }

        return Info(Parse(await LinkAsync(source.SpreadsheetId, ct), LinkHint));
    }

    public async Task<ReportDocument> ReadAsync(SheetSource source, IReadOnlyCollection<string> sheets, CancellationToken ct = default)
    {
        if (source.Access == SheetAccess.ServiceAccount)
        {
            var (key, token) = await AuthorizeAsync(source, ct);
            var (grid, hint) = await GridAsync(source.SpreadsheetId, key, token, ct);
            if (grid is null)
            {
                return Pick(await DriveBookAsync(source.SpreadsheetId, key, token, hint, ct), sheets);
            }

            var names = DataSourceRules.ResolveSheets(grid.Select(g => g.Title).ToList(), sheets);
            return await ValuesAsync(source.SpreadsheetId, key, token, names.Select(n => grid.First(g => g.Title == n)).ToList(), ct);
        }

        return Pick(Parse(await LinkAsync(source.SpreadsheetId, ct), LinkHint), sheets);
    }

    private static IReadOnlyList<SheetInfo> Info(ReportDocument document) =>
        document.Sheets.Select(s => new SheetInfo(s.Name, s.TotalRows, s.TotalColumns)).ToList();

    private static ReportDocument Pick(ReportDocument document, IReadOnlyCollection<string> sheets)
    {
        var names = DataSourceRules.ResolveSheets(document.Sheets.Select(s => s.Name).ToList(), sheets);
        return new ReportDocument(names.Select(n => document.Sheets.First(s => s.Name == n)).ToList());
    }

    // ---------- Sheets API ----------

    /// <summary>
    /// Листы таблицы через Sheets API. Grid null — Sheets API тут не подходит, таблица читается файлом xlsx: это файл Excel в Drive
    /// (Sheets API отвечает 400) или Sheets API выключен в проекте; Hint — что подсказать, если файл окажется слишком большим.
    /// </summary>
    private async Task<(IReadOnlyList<GridSheet>? Grid, string Hint)> GridAsync(string id, ServiceAccountKey key, string token, CancellationToken ct)
    {
        var url = $"https://sheets.googleapis.com/v4/spreadsheets/{Uri.EscapeDataString(id)}?fields=sheets.properties(title,sheetType,gridProperties(rowCount,columnCount))";
        using var response = await SendAsync(Authorized(token, url), ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await ErrorAsync(response, ct);
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                return (null, ExcelHint);
            }

            if (IsApiDisabled(error.Reason))
            {
                return (null, SheetsApiHint);
            }

            throw Failure(response.StatusCode, error.Message, key);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var sheets = new List<GridSheet>();
        if (json.RootElement.TryGetProperty("sheets", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var sheet in list.EnumerateArray())
            {
                var properties = sheet.GetProperty("properties");
                // Лист-диаграмма: ячеек нет.
                if (properties.TryGetProperty("sheetType", out var type) && type.GetString() != "GRID")
                {
                    continue;
                }

                var size = properties.TryGetProperty("gridProperties", out var g) ? g : default;
                sheets.Add(new GridSheet(properties.GetProperty("title").GetString() ?? string.Empty, Int(size, "rowCount"), Int(size, "columnCount")));
            }
        }

        return (sheets, string.Empty);
    }

    /// <summary>Значения листов через values:batchGet — по нескольку листов за запрос, не больше лимитов строк и столбцов AI Data Manager.</summary>
    private async Task<ReportDocument> ValuesAsync(string id, ServiceAccountKey key, string token, IReadOnlyList<GridSheet> sheets, CancellationToken ct)
    {
        var result = new List<ReportSheet>(sheets.Count);
        long cells = 0;
        foreach (var batch in Batches(sheets))
        {
            // Дважды: даты числом и даты строкой — по разнице видно, какие ячейки даты (как в xlsx), а числа остаются точными.
            using var serial = await BatchGetAsync(id, key, token, batch, "SERIAL_NUMBER", ct);
            using var shown = await BatchGetAsync(id, key, token, batch, "FORMATTED_STRING", ct);
            var serialRanges = serial.RootElement.TryGetProperty("valueRanges", out var s) ? s : default;
            var shownRanges = shown.RootElement.TryGetProperty("valueRanges", out var f) ? f : default;
            if (serialRanges.ValueKind != JsonValueKind.Array || shownRanges.ValueKind != JsonValueKind.Array
                || serialRanges.GetArrayLength() != batch.Count || shownRanges.GetArrayLength() != batch.Count)
            {
                throw new DataSourceException("Google вернул не все листы — попробуйте ещё раз.");
            }

            for (var i = 0; i < batch.Count; i++)
            {
                var sheet = Sheet(batch[i].Title, serialRanges[i], shownRanges[i], batch[i].Rows, batch[i].Columns, options.MaxRowsPerSheet, options.MaxColumns);
                cells += sheet.Rows.Sum(r => (long)r.Count);
                if (cells > MaxCells)
                {
                    throw new DataSourceException($"Выбранные листы слишком большие — больше {MaxCells / 1_000_000} млн ячеек. Выберите в привязке только нужные листы.");
                }

                result.Add(sheet);
            }
        }

        return new ReportDocument(result);
    }

    private IEnumerable<IReadOnlyList<GridSheet>> Batches(IReadOnlyList<GridSheet> sheets)
    {
        var batch = new List<GridSheet>();
        long size = 0;
        foreach (var sheet in sheets)
        {
            var estimate = (long)Math.Min(sheet.Rows, options.MaxRowsPerSheet + 1) * Math.Min(sheet.Columns, options.MaxColumns + 1);
            if (batch.Count > 0 && (batch.Count == BatchSheets || size + estimate > BatchCells))
            {
                yield return batch;
                batch = [];
                size = 0;
            }

            batch.Add(sheet);
            size += estimate;
        }

        if (batch.Count > 0)
        {
            yield return batch;
        }
    }

    private async Task<JsonDocument> BatchGetAsync(string id, ServiceAccountKey key, string token, IReadOnlyList<GridSheet> sheets, string dates, CancellationToken ct)
    {
        // На строку и столбец больше лимита — чтобы знать, что лист обрезан; диапазон больше листа Google сам сужает до сетки.
        var last = $"{DataValues.ColumnLetter(options.MaxColumns)}{options.MaxRowsPerSheet + 1}";
        var ranges = string.Join("&", sheets.Select(s => "ranges=" + Uri.EscapeDataString($"'{s.Title.Replace("'", "''")}'!A1:{last}")));
        var url = $"https://sheets.googleapis.com/v4/spreadsheets/{Uri.EscapeDataString(id)}/values:batchGet?{ranges}" +
                  $"&majorDimension=ROWS&valueRenderOption=UNFORMATTED_VALUE&dateTimeRenderOption={dates}";
        using var response = await SendAsync(Authorized(token, url), ct);
        if (!response.IsSuccessStatusCode)
        {
            throw Failure(response.StatusCode, (await ErrorAsync(response, ct)).Message, key);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }

    /// <summary>
    /// Лист из ответа values:batchGet. serial — даты числом (SERIAL_NUMBER), shown — даты строкой (FORMATTED_STRING): ячейка-число в первом
    /// и строка во втором — дата или время (дата — ГГГГ-ММ-ДД, время — как в таблице). Строк и столбцов — не больше maxRows и maxColumns;
    /// Google отдаёт на строку и столбец больше — по ним видно, что лист обрезан (тогда размер — по сетке листа).
    /// </summary>
    public static ReportSheet Sheet(string name, JsonElement serial, JsonElement shown, int gridRows, int gridColumns, int maxRows, int maxColumns)
    {
        var values = Rows(serial);
        var shownValues = Rows(shown);
        var width = values.Select(r => r.ValueKind == JsonValueKind.Array ? r.GetArrayLength() : 0).DefaultIfEmpty(0).Max();
        var rowLimit = Math.Min(values.Count, maxRows);
        var columnLimit = Math.Min(width, maxColumns);
        var rows = new List<IReadOnlyList<string>>(rowLimit);
        for (var r = 0; r < rowLimit; r++)
        {
            var shownRow = r < shownValues.Count ? shownValues[r] : default;
            var cells = new string[columnLimit];
            for (var c = 0; c < columnLimit; c++)
            {
                cells[c] = Value(Cell(values[r], c), Cell(shownRow, c));
            }

            rows.Add(cells);
        }

        var moreRows = values.Count > maxRows;
        var moreColumns = width > maxColumns;
        return new ReportSheet(name, rows, moreRows ? Math.Max(gridRows, values.Count) : values.Count, moreColumns ? Math.Max(gridColumns, width) : width,
            moreRows || moreColumns);
    }

    private static List<JsonElement> Rows(JsonElement range) =>
        range.ValueKind == JsonValueKind.Object && range.TryGetProperty("values", out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().ToList() : [];

    private static JsonElement? Cell(JsonElement row, int column) =>
        row.ValueKind == JsonValueKind.Array && column < row.GetArrayLength() ? row[column] : null;

    /// <summary>Значение ячейки как в разборе xlsx: число — инвариантно и полностью, дата — ГГГГ-ММ-ДД, логическое — «да»/«нет», ошибка — «#ОШИБКА».</summary>
    private static string Value(JsonElement? cell, JsonElement? shown)
    {
        if (cell is not { } value)
        {
            return string.Empty;
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.Number:
                var number = value.GetDouble();
                if (shown is { ValueKind: JsonValueKind.String } text)
                {
                    // Дата: число дней от 30.12.1899, как в Excel. Время и длительность («36:00:00») — как показано в таблице.
                    var display = (text.GetString() ?? string.Empty).Trim();
                    return number is >= 1 and < 2958466 && !TimeText().IsMatch(display)
                        ? DateTime.FromOADate(number).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                        : display;
                }

                return number.ToString("0.###########", CultureInfo.InvariantCulture);
            case JsonValueKind.String:
                var s = (value.GetString() ?? string.Empty).Trim();
                return CellErrors.Contains(s) ? "#ОШИБКА" : s;
            case JsonValueKind.True:
                return "да";
            case JsonValueKind.False:
                return "нет";
            default:
                return string.Empty;
        }
    }

    [GeneratedRegex(@"^\d+:\d{2}(:\d{2}([.,]\d+)?)?$")]
    private static partial Regex TimeText();

    private static int Int(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.TryGetInt32(out var n) ? n : 0;

    // ---------- Файл xlsx: по ссылке и через Drive API ----------

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

    /// <summary>Таблица файлом xlsx через Drive API (Sheets API не подходит) — разбирается целиком, если не слишком большая.</summary>
    private async Task<ReportDocument> DriveBookAsync(string id, ServiceAccountKey key, string token, string hint, CancellationToken ct)
    {
        var escaped = Uri.EscapeDataString(id);
        using var exported = await SendAsync(Authorized(token, $"https://www.googleapis.com/drive/v3/files/{escaped}/export?mimeType={Uri.EscapeDataString(XlsxMime)}"), ct);
        if (exported.IsSuccessStatusCode)
        {
            return Parse(await ReadBodyAsync(exported, ct), hint);
        }

        var error = await ErrorAsync(exported, ct);
        if (error.Reason == "exportSizeLimitExceeded")
        {
            // files.export отдаёт до 10 МБ; больше — та же выгрузка, что по ссылке, с токеном сервисного аккаунта.
            return Parse(await DocsExportAsync(id, key, token, ct), hint);
        }

        if (error.Reason == "fileNotExportable")
        {
            // Загруженный в Drive файл Excel: экспортировать нечего — скачиваем как есть.
            using var media = await SendAsync(Authorized(token, $"https://www.googleapis.com/drive/v3/files/{escaped}?alt=media&supportsAllDrives=true"), ct);
            if (!media.IsSuccessStatusCode)
            {
                throw Failure(media.StatusCode, (await ErrorAsync(media, ct)).Message, key);
            }

            return Parse(await ReadBodyAsync(media, ct), ExcelHint);
        }

        if (IsApiDisabled(error.Reason))
        {
            throw new DataSourceException("В проекте Google Cloud сервисного аккаунта выключен доступ к таблицам: включите Google Sheets API и Google Drive API " +
                                          "(APIs & Services → Library).");
        }

        throw Failure(exported.StatusCode, error.Message, key);
    }

    private async Task<byte[]> DocsExportAsync(string id, ServiceAccountKey key, string token, CancellationToken ct)
    {
        using var response = await SendAsync(Authorized(token, $"https://docs.google.com/spreadsheets/d/{Uri.EscapeDataString(id)}/export?format=xlsx"), ct);
        var type = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        if (!response.IsSuccessStatusCode || type.Contains("html", StringComparison.OrdinalIgnoreCase))
        {
            throw Failure(response.IsSuccessStatusCode ? HttpStatusCode.Forbidden : response.StatusCode, string.Empty, key);
        }

        return await ReadBodyAsync(response, ct);
    }

    private ReportDocument Parse(byte[] bytes, string hint)
    {
        var unpacked = UnpackedSize(bytes);
        if (unpacked > MaxUnpackedBytes)
        {
            throw new DataSourceException($"Таблица слишком большая, чтобы читать её файлом целиком: {unpacked / (1024 * 1024)} МБ данных в листах. {hint}");
        }

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

    /// <summary>Размер листов книги xlsx в распакованном виде — по нему видно, хватит ли памяти разобрать книгу целиком; не zip — 0.</summary>
    public static long UnpackedSize(byte[] xlsx)
    {
        try
        {
            using var zip = new ZipArchive(new MemoryStream(xlsx), ZipArchiveMode.Read);
            return zip.Entries
                .Where(e => e.FullName.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase) || e.FullName.Equals("xl/sharedStrings.xml", StringComparison.OrdinalIgnoreCase))
                .Sum(e => e.Length);
        }
        catch (InvalidDataException)
        {
            return 0;
        }
    }

    // ---------- Доступ ----------

    private async Task<(ServiceAccountKey Key, string Token)> AuthorizeAsync(SheetSource source, CancellationToken ct)
    {
        var key = ServiceAccountKey.Parse(source.ServiceAccountJson);
        return (key, await TokenAsync(key, ct));
    }

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
            throw new DataSourceException($"Google не принял ключ сервисного аккаунта — вставьте действующий ключ (удалённый в Google Cloud ключ не работает).{Google(TokenError(body))}");
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

    // ---------- Ответы Google ----------

    /// <summary>Ошибка API Google: текст и причина — reason из errors[] (Drive) или details[] (Sheets: SERVICE_DISABLED), иначе status.</summary>
    private static async Task<(string Message, string Reason)> ErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (!json.RootElement.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object)
            {
                return (string.Empty, string.Empty);
            }

            var message = error.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() ?? string.Empty : string.Empty;
            var reason = Reasons(error, "errors").Concat(Reasons(error, "details")).FirstOrDefault()
                         ?? (error.TryGetProperty("status", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null);
            return (message, reason ?? string.Empty);
        }
        catch (JsonException)
        {
            return (string.Empty, string.Empty);
        }
    }

    private static IEnumerable<string> Reasons(JsonElement error, string property) =>
        error.TryGetProperty(property, out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.Object && e.TryGetProperty("reason", out var r) && r.ValueKind == JsonValueKind.String)
                .Select(e => e.GetProperty("reason").GetString()!)
                .ToList()
            : [];

    private static string TokenError(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            var code = root.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;
            var description = root.TryGetProperty("error_description", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null;
            return string.Join(": ", new[] { code, description }.Where(x => !string.IsNullOrWhiteSpace(x)));
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static bool IsApiDisabled(string reason) => reason is "SERVICE_DISABLED" or "accessNotConfigured";

    /// <summary>Отказ Google — текст для администратора вместе с ответом Google: по нему видно, доступ это, ссылка или что-то ещё.</summary>
    private static DataSourceException Failure(HttpStatusCode status, string message, ServiceAccountKey key) => new(status switch
    {
        HttpStatusCode.NotFound or HttpStatusCode.Forbidden => $"Нет доступа к таблице: поделитесь ею с {key.Email} (читатель) или проверьте ссылку.{Google(message)}",
        HttpStatusCode.TooManyRequests => $"Google ограничил частоту запросов — попробуйте через минуту.{Google(message)}",
        _ => $"Google не отдал таблицу: HTTP {(int)status}.{Google(message)}",
    });

    private static string Google(string message) =>
        string.IsNullOrWhiteSpace(message) ? string.Empty : $" Ответ Google: «{(message.Length > 200 ? message[..200] + "…" : message)}».";

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
