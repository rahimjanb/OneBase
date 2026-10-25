using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OneBase.AI.Tests;

/// <summary>
/// Подставной Verifix Public API (по документации Postman): POST /security/oauth/token, POST /b/vhr/api/v1/core/{метод} с заголовками
/// project_code, Authorization: Bearer, cursor и limit; ответ { data, meta: { count, next_cursor } }, числа — строками, как в примерах.
/// Никаких запросов к настоящему Verifix: адрес — https://verifix.test.
/// </summary>
public sealed class FakeVerifix : HttpMessageHandler
{
    public const string BaseUrl = "https://verifix.test";
    public const string ClientId = "test-client";
    public const string ClientSecret = "test-secret-0123456789abcdef";

    public sealed record Call(string Path, IReadOnlyDictionary<string, string> Headers, string Body);

    public List<JsonObject> Employees { get; } = [];
    public List<JsonObject> Divisions { get; } = [];
    public List<JsonObject> Jobs { get; } = [];
    public List<JsonObject> Schedules { get; } = [];
    public List<JsonObject> Locations { get; } = [];
    public List<JsonObject> TimeKinds { get; } = [];
    public List<JsonObject> Tracks { get; } = [];

    /// <summary>Строки табеля: staff с массивом days (date — dd.MM.yyyy).</summary>
    public List<JsonObject> Timesheet { get; } = [];

    /// <summary>Отказы по методу (часть пути, например "core/track$list"): каждый запрос берёт следующий код из очереди.</summary>
    public ConcurrentDictionary<string, Queue<HttpStatusCode>> Failures { get; } = new();

    public ConcurrentQueue<Call> Calls { get; } = new();
    public int TokenRequests;
    public int IssuedTokens;

    /// <summary>Сколько первых запросов API отклонить с 401 (истёкший токен).</summary>
    public int RejectApiTokens;

    /// <summary>Последняя страница возвращает не -1, а позицию для следующей выгрузки (как в примерах документации); следующий запрос — пустой.</summary>
    public bool TrailingCursor;

    /// <summary>Задержка ответа API — для проверки параллельного запуска.</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    public void Fail(string method, params HttpStatusCode[] codes) => Failures[method] = new Queue<HttpStatusCode>(codes);

    /// <summary>Методы, которые отвечают 200, но не JSON (например, страница ошибки прокси).</summary>
    public HashSet<string> Garbage { get; } = [];

    public void Clear()
    {
        Employees.Clear();
        Divisions.Clear();
        Jobs.Clear();
        Schedules.Clear();
        Locations.Clear();
        TimeKinds.Clear();
        Tracks.Clear();
        Timesheet.Clear();
        Failures.Clear();
        Garbage.Clear();
        Calls.Clear();
        RejectApiTokens = 0;
        TrailingCursor = false;
        Delay = TimeSpan.Zero;
    }

    public static JsonObject Employee(long id, string first, string last, long division, string status = "W", string? hired = "15.01.2025", string? dismissed = null) => new()
    {
        ["employee_id"] = id.ToString(), ["employee_code"] = null, ["first_name"] = first, ["last_name"] = last, ["middle_name"] = null,
        ["phone_number"] = $"99890{id:0000000}", ["email"] = null, ["birthday"] = "01.02.1990", ["gender"] = "M", ["state"] = "A",
        ["status"] = status, ["status_name"] = status == "W" ? "Работает" : "Уволен", ["hiring_date"] = hired, ["dismissal_date"] = dismissed,
        ["job_id"] = "721", ["division_id"] = division.ToString(), ["schedule_id"] = "943", ["org_unit_id"] = division.ToString(),
        // Чувствительные поля, как в ответе Verifix, — OneBase не должен их сохранять.
        ["tin"] = "123456789", ["npin"] = "31234567890123", ["passport_doc_series"] = "AA", ["passport_doc_number"] = "1234567",
        ["has_identification_photo"] = "Y", ["card_number"] = "5555555555555",
        ["identification_photos"] = new JsonArray(new JsonObject { ["photo_sha"] = "abc" }),
    };

    public static JsonObject Track(long id, long employee, string time, string type = "I", long? location = 1, long division = 901) => new()
    {
        ["track_id"] = id.ToString(), ["schedule_id"] = "943", ["employee_id"] = employee.ToString(), ["staff_id"] = (employee + 20000).ToString(),
        ["division_id"] = division.ToString(), ["location_id"] = location?.ToString(), ["track_type"] = type, ["mark_type"] = "F", ["track_datetime"] = time,
        ["created_by"] = "321", ["created_by_name"] = "Admin",
    };

    /// <summary>Рабочий день 09:00–18:00 с обедом 13:00–14:00; facts — (time_kind_id, value).</summary>
    public static JsonObject Day(DateOnly date, string kind = "W", string? input = null, string? output = null, params (long Kind, long Value)[] facts)
    {
        var d = date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        var work = kind == "W";
        return new JsonObject
        {
            ["date"] = d, ["day_kind"] = kind, ["plan_time"] = work ? "480" : "0", ["done_marks"] = "0", ["planned_marks"] = "0",
            ["begin_time"] = work ? $"{d} 09:00:00" : null, ["end_time"] = work ? $"{d} 18:00:00" : null,
            ["break_begin_time"] = work ? $"{d} 13:00:00" : null, ["break_end_time"] = work ? $"{d} 14:00:00" : null,
            ["input_time"] = input is null ? null : $"{d} {input}", ["output_time"] = output is null ? null : $"{d} {output}",
            ["input_location_id"] = null, ["output_location_id"] = null,
            ["facts"] = new JsonArray(facts.Select(f => (JsonNode)new JsonObject { ["time_kind_id"] = f.Kind, ["fact_value"] = f.Value }).ToArray()),
        };
    }

    public static JsonObject Staff(long employee, long division, params JsonObject[] days) => new()
    {
        ["staff_id"] = (employee + 20000).ToString(), ["employee_id"] = employee.ToString(), ["employee_name"] = $"Сотрудник {employee}",
        ["job_id"] = "721", ["job_name"] = "Менеджер", ["division_id"] = division.ToString(), ["division_name"] = "Отдел", ["schedule_id"] = "943",
        ["schedule_name"] = "9-18", ["rank_id"] = null, ["rank_name"] = null, ["days"] = new JsonArray(days.Cast<JsonNode>().ToArray()),
    };

    /// <summary>Стандартные справочники: два подразделения (второе — дочернее первого), третье — отдельное, должность, график, локация, виды времени.</summary>
    public void SeedReferences()
    {
        Divisions.Add(new JsonObject { ["division_id"] = "901", ["name"] = "Отдел продаж", ["parent_id"] = null, ["opened_date"] = "09.01.2025", ["closed_date"] = null, ["state"] = "A" });
        Divisions.Add(new JsonObject { ["division_id"] = "902", ["name"] = "Команда Ташкент", ["parent_id"] = "901", ["opened_date"] = "09.01.2025", ["closed_date"] = null, ["state"] = "A" });
        Divisions.Add(new JsonObject { ["division_id"] = "903", ["name"] = "Бухгалтерия", ["parent_id"] = null, ["opened_date"] = "09.01.2025", ["closed_date"] = null, ["state"] = "A" });
        Jobs.Add(new JsonObject { ["job_id"] = "721", ["name"] = "Менеджер", ["code"] = null, ["state"] = "A" });
        Schedules.Add(new JsonObject { ["schedule_id"] = "943", ["name"] = "9-18", ["code"] = null, ["kind"] = "F", ["state"] = "A", ["schedule_days"] = new JsonArray() });
        Locations.Add(new JsonObject { ["location_id"] = "1", ["name"] = "Главный офис", ["address"] = "Ташкент", ["latlng"] = "41.3,69.2", ["accuracy"] = "500", ["state"] = "A" });
        foreach (var (id, name, code) in new[] { (81, "Явка", "Я"), (82, "Опоздание", "ОП"), (84, "Отсутствие", "ОТС"), (88, "Больничный", "Б"), (91, "Отпуск", "ОТ") })
        {
            TimeKinds.Add(new JsonObject { ["time_kind_id"] = id.ToString(), ["name"] = name, ["letter_code"] = code, ["digital_code"] = null });
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        if (uri.Host != "verifix.test")
        {
            throw new InvalidOperationException($"Тест обратился не к подставному Verifix: {uri.Host}");
        }

        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        var headers = request.Headers.ToDictionary(h => h.Key.ToLowerInvariant(), h => string.Join(",", h.Value));
        var path = uri.AbsolutePath.TrimStart('/');
        Calls.Enqueue(new Call(path, headers, body));

        if (path == "security/oauth/token")
        {
            Interlocked.Increment(ref TokenRequests);
            var json = JsonNode.Parse(body)!;
            if ((string?)json["grant_type"] != "client_credentials" || (string?)json["client_id"] != ClientId || (string?)json["client_secret"] != ClientSecret)
            {
                return Json(HttpStatusCode.Unauthorized, new JsonObject { ["error"] = "invalid_client" });
            }

            var n = Interlocked.Increment(ref IssuedTokens);
            return Json(HttpStatusCode.OK, new JsonObject
            {
                ["access_token"] = $"token-{n}", ["refresh_token"] = "refresh", ["token_type"] = "bearer", ["expires_in"] = 10800,
            });
        }

        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, cancellationToken);
        }

        if (!headers.TryGetValue("authorization", out var auth) || !auth.StartsWith("Bearer token-", StringComparison.Ordinal) || headers.GetValueOrDefault("project_code") != "vhr")
        {
            return Json(HttpStatusCode.Unauthorized, new JsonObject { ["error"] = "unauthorized" });
        }

        if (RejectApiTokens > 0)
        {
            RejectApiTokens--;
            return Json(HttpStatusCode.Unauthorized, new JsonObject { ["error"] = "token expired" });
        }

        var method = path.Replace("b/vhr/api/v1/", "", StringComparison.Ordinal);
        if (Failures.TryGetValue(method, out var failures) && failures.TryDequeue(out var code))
        {
            var failure = Json(code, new JsonObject { ["error"] = "fail" });
            if (code == HttpStatusCode.TooManyRequests)
            {
                failure.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
            }

            return failure;
        }

        if (Garbage.Contains(method))
        {
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>502 Bad Gateway</html>", Encoding.UTF8, "text/html") };
        }

        var filter = JsonNode.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body)!.AsObject();
        if (method == "core/filial$info")
        {
            return Json(HttpStatusCode.OK, new JsonObject { ["filial_name"] = "Тестовая организация", ["region_name"] = "Tashkent" });
        }

        List<JsonObject>? source = method switch
        {
            "core/employee$list" => Employees,
            "core/division$list" => Divisions,
            "core/job$list" => Jobs,
            "core/schedule$list" => Schedules,
            "core/location$list" => Locations,
            "core/time_kind$list" => TimeKinds,
            "core/track$list" => TracksIn(filter),
            "core/timesheet$export" => TimesheetIn(filter),
            _ => null,
        };
        if (source is null)
        {
            return Json(HttpStatusCode.NotFound, new JsonObject { ["error"] = "not found" });
        }

        var limit = int.TryParse(headers.GetValueOrDefault("limit"), out var l) && l > 0 ? l : 100;
        var offset = int.TryParse(headers.GetValueOrDefault("cursor"), out var c) ? c : 0;
        var page = source.Skip(offset).Take(limit).Select(x => (JsonNode)x.DeepClone()).ToArray();
        var end = offset + page.Length >= source.Count;
        var next = end ? (TrailingCursor && page.Length > 0 ? offset + page.Length : -1) : offset + page.Length;
        return Json(HttpStatusCode.OK, new JsonObject
        {
            ["data"] = new JsonArray(page),
            ["meta"] = new JsonObject { ["count"] = page.Length.ToString(), ["next_cursor"] = next.ToString() },
        });
    }

    private List<JsonObject> TracksIn(JsonObject filter)
    {
        var from = Parse((string?)filter["begin_datetime"]) ?? throw new InvalidOperationException("track$list: нет begin_datetime");
        var to = Parse((string?)filter["end_datetime"]) ?? throw new InvalidOperationException("track$list: нет end_datetime");
        return Tracks.Where(t => Parse((string?)t["track_datetime"]) is { } at && at >= from && at <= to).ToList();
    }

    private List<JsonObject> TimesheetIn(JsonObject filter)
    {
        var from = DateOnly.ParseExact((string)filter["period_begin_date"]!, "dd.MM.yyyy", CultureInfo.InvariantCulture);
        var to = DateOnly.ParseExact((string)filter["period_end_date"]!, "dd.MM.yyyy", CultureInfo.InvariantCulture);
        return Timesheet.Select(s =>
        {
            var copy = s.DeepClone().AsObject();
            var days = copy["days"]!.AsArray().Where(d => DateOnly.ParseExact((string)d!["date"]!, "dd.MM.yyyy", CultureInfo.InvariantCulture) is var date && date >= from && date <= to)
                .Select(d => d!.DeepClone()).ToArray();
            copy["days"] = new JsonArray(days);
            return copy;
        }).ToList();
    }

    private static DateTime? Parse(string? text) =>
        DateTime.TryParseExact(text, "dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var v) ? v : null;

    private static HttpResponseMessage Json(HttpStatusCode code, JsonNode body) =>
        new(code) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };

    /// <summary>Запросы к методу API (без токена).</summary>
    public List<Call> CallsTo(string method) => Calls.Where(c => c.Path.EndsWith(method, StringComparison.Ordinal)).ToList();

    public static string Serialize(object value) => JsonSerializer.Serialize(value);
}
