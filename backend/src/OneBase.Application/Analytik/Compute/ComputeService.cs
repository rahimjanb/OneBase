using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Semantic;
using OneBase.Domain.Analytik;

namespace OneBase.Application.Analytik.Compute;

/// <summary>Данные расчёта — срез движка: показатели, измерение, шаг, период, период сравнения, фильтры, сегмент продаж.</summary>
public sealed record ComputeData
{
    public IReadOnlyList<string> Metrics { get; init; } = [];
    public string? Dimension { get; init; }
    public string? Grain { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public DateOnly? CompareFrom { get; init; }
    public DateOnly? CompareTo { get; init; }
    public IReadOnlyList<AnalyticsFilter>? Filters { get; init; }
    public string? Segment { get; init; }
}

/// <summary>Запрос расчёта: метод, данные (только через движок срезов — числа модель не передаёт), параметры метода.</summary>
public sealed record ComputeRequest
{
    public required string Method { get; init; }
    public ComputeData Data { get; init; } = new();
    public IReadOnlyDictionary<string, JsonElement>? Params { get; init; }
}

/// <summary>Кто и откуда считает: пользователь (его права), откуда вызов, исследование и чат, агент.</summary>
public sealed record ComputeContext(Guid? UserId, string Origin, Guid? InvestigationId = null, Guid? ConversationId = null, string? AgentCode = null);

/// <summary>
/// Итог расчёта: запись журнала, метод и алгоритм с версией, период, результат, короткий вывод кодом (Summary), класс вывода, ограничения,
/// источники и запросы срезов, из которых взяты данные. Status: ok | insufficient (данных не хватает — с причиной) | error.
/// </summary>
public sealed record ComputeOutcome(
    Guid Id, string Method, string Title, string Domain, string Status, string Algorithm, string Version, DateOnly From, DateOnly To, DateOnly? CompareFrom,
    DateOnly? CompareTo, object? Result, string? Summary, string? EvidenceClass, IReadOnlyList<string> Limitations, IReadOnlyList<AnalyticsSource> Sources,
    IReadOnlyList<Guid> QueryIds, string? Error, long ElapsedMs, string? Scope);

/// <summary>
/// Advanced Computation Engine: все статистические и финансовые расчёты — детерминированным кодом (Compute/*Math). Данные берутся только
/// движком срезов (SemanticEngine) с правами пользователя — модель выбирает метод и срез, но не передаёт чисел. Каждый расчёт пишется в
/// analytik.Computations: вход (запросы срезов, их id в журнале, число строк, отпечаток данных), алгоритм, версия, параметры, результат,
/// период, источники, ограничения, класс вывода. ReplayAsync повторяет расчёт по записи и сверяет результат.
/// </summary>
public sealed partial class ComputeService(SemanticEngine engine, AnalytikAccess access, IAnalytikDb adb, TimeProvider clock, ILogger<ComputeService> logger)
{
    /// <summary>Сколько строк списков результата уходит в ответ инструмента (полный результат — в журнале, до StoredItems).</summary>
    public const int ShownItems = 30;

    private const int StoredItems = 2000;

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new RoundedDoubleConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Параметры JSON результатов (для инструментов AI): округление дробей, без NaN.</summary>
    public static JsonSerializerOptions JsonOptions => Json;

    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    public async Task<ComputeOutcome> RunAsync(ComputeRequest request, ComputeContext context, CancellationToken ct, AnalytikPrincipal? principal = null)
    {
        var watch = Stopwatch.StartNew();
        var method = ComputeCatalog.Method(request.Method)
                     ?? throw new ComputeException($"Метода «{request.Method}» нет. Есть: {string.Join(", ", ComputeCatalog.Methods.Select(m => m.Code))}.");
        principal ??= context.UserId is { } uid ? await access.ResolveAsync(uid, ct)
            : context.Origin is "proactive" or "benchmark" or "decision" ? AnalytikPrincipal.SystemPrincipal
            : throw new UnauthorizedAccessException("Расчёт без пользователя — только для фоновых задач Аналитика.");
        var today = engine.Today;
        var data = Normalize(request.Data, method, today);
        // Источник в журнале срезов — коротко (колонка 16 символов): chat, api или compute (исследование, фон, повтор).
        var queryOrigin = context.Origin is "chat" or "api" ? context.Origin : context.Origin == "benchmark" ? "bench" : "compute";
        var loader = new Loader(engine, new QueryCaller(context.UserId, queryOrigin, context.AgentCode, context.ConversationId), principal, data, ct);
        var row = new AnalytikComputation
        {
            UserId = context.UserId, Origin = context.Origin, InvestigationId = context.InvestigationId, ConversationId = context.ConversationId,
            Method = method.Code, Domain = method.Domain, Algorithm = method.Algorithm.Length > 300 ? method.Algorithm[..300] : method.Algorithm,
            Version = method.Version, Parameters = JsonSerializer.Serialize(request.Params ?? new Dictionary<string, JsonElement>(), Json), Input = "{}",
            PeriodFrom = data.From, PeriodTo = data.To, Status = "ok",
        };

        MethodResult result;
        try
        {
            ValidateShape(method, data);
            result = await ExecuteAsync(method, data, new Params(request.Params), loader, today);
        }
        catch (AnalyticsQueryException ex) when (ex.Code == "denied")
        {
            // Нет прав на данные метода — отказ доступа, а не «данных не хватает»: обходить права другим срезом нельзя.
            throw new UnauthorizedAccessException(ex.Message);
        }
        catch (Exception ex) when (ex is ComputeException or AnalyticsQueryException)
        {
            result = new MethodResult(null, null, null, [ex.Message]) { Status = "insufficient", Error = ex.Message };
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Аналитик: расчёт {Method} не выполнен", method.Code);
            result = new MethodResult(null, null, null, []) { Status = "error", Error = "Расчёт не выполнен из-за внутренней ошибки OneBase." };
        }

        var limitations = new List<string>(result.Limitations);
        limitations.AddRange(loader.Notes.Distinct());
        if (!string.IsNullOrWhiteSpace(method.Limits))
        {
            limitations.Add(method.Limits);
        }

        if (!loader.Complete)
        {
            limitations.Add("Срез данных неполный (см. проверки среза) — результат расчёта может быть смещён.");
        }

        var resultJson = result.Value is null ? null : JsonSerializer.Serialize(result.Value, Json);
        row.Input = JsonSerializer.Serialize(new
        {
            request = new { method = method.Code, data, @params = request.Params },
            queries = loader.Log,
            hash = Hash(loader.Fingerprint.ToString()),
        }, Json);
        row.Result = resultJson is { Length: > 400_000 } ? JsonSerializer.Serialize(new { truncated = true, summary = result.Summary }, Json) : resultJson;
        row.Sources = JsonSerializer.Serialize(loader.Sources, Json);
        row.Limitations = JsonSerializer.Serialize(limitations.Distinct().ToList(), Json);
        row.EvidenceClass = result.EvidenceClass;
        row.Status = result.Status;
        row.Error = result.Error is { Length: > 2000 } e ? e[..2000] : result.Error;
        row.Scope = loader.Scope is { Length: > 500 } s ? s[..500] : loader.Scope;
        row.ElapsedMs = watch.ElapsedMilliseconds;
        adb.Computations.Add(row);
        try
        {
            await adb.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Аналитик: расчёт {Method} не записан в журнал", method.Code);
        }

        return new ComputeOutcome(row.Id, method.Code, method.Title, method.Domain, result.Status, method.Algorithm, method.Version, data.From!.Value, data.To!.Value,
            data.CompareFrom, data.CompareTo, result.Value, result.Summary, result.EvidenceClass, limitations.Distinct().ToList(), loader.Sources, loader.QueryIds,
            result.Error, watch.ElapsedMilliseconds, loader.Scope);
    }

    /// <summary>
    /// Повтор расчёта по записи журнала (Reviewer: «пересчитай»): тот же метод, данные и параметры с правами того же пользователя; Same — совпал
    /// ли результат. Данные могли обновиться (новая загрузка Linko) — тогда расхождение объясняется отпечатком данных.
    /// </summary>
    public async Task<(ComputeOutcome Outcome, bool Same, bool DataChanged)> ReplayAsync(Guid id, ComputeContext context, CancellationToken ct,
        AnalytikPrincipal? principal = null)
    {
        var row = await adb.Computations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct)
                  ?? throw new KeyNotFoundException("Расчёт не найден.");
        if (context.UserId is { } uid && row.UserId != uid && principal?.Manager != true)
        {
            throw new UnauthorizedAccessException("Чужой расчёт.");
        }

        using var input = JsonDocument.Parse(row.Input);
        var req = input.RootElement.GetProperty("request");
        var data = req.GetProperty("data").Deserialize<ComputeData>(Json) ?? new ComputeData();
        var parameters = req.TryGetProperty("params", out var p) && p.ValueKind == JsonValueKind.Object
            ? p.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone())
            : null;
        var outcome = await RunAsync(new ComputeRequest { Method = row.Method, Data = data, Params = parameters }, context with { Origin = "replay" }, ct, principal);
        var replayed = await adb.Computations.AsNoTracking().FirstAsync(c => c.Id == outcome.Id, ct);
        var oldHash = input.RootElement.TryGetProperty("hash", out var h) ? h.GetString() : null;
        using var newInput = JsonDocument.Parse(replayed.Input);
        var newHash = newInput.RootElement.TryGetProperty("hash", out var nh) ? nh.GetString() : null;
        return (outcome, Canonical(row.Result) == Canonical(replayed.Result), oldHash != newHash);
    }

    private static string? Canonical(string? json) => json is null ? null : JsonSerializer.Serialize(JsonDocument.Parse(json).RootElement);

    /// <summary>Период по умолчанию — последние 90 дней; сравнение по умолчанию — такой же отрезок перед периодом (для методов со сравнением).</summary>
    private static ComputeData Normalize(ComputeData data, ComputeMethod method, DateOnly today)
    {
        var to = data.To ?? today;
        var from = data.From ?? to.AddDays(-89);
        if (from > to)
        {
            (from, to) = (to, from);
        }

        StatMath.Require(to.DayNumber - from.DayNumber <= SemanticEngine.MaxDays, $"Период расчёта — не больше {SemanticEngine.MaxDays} дней.");
        DateOnly? cf = data.CompareFrom, ctTo = data.CompareTo;
        if (method.NeedsCompare && (cf is null || ctTo is null))
        {
            var len = to.DayNumber - from.DayNumber + 1;
            ctTo = from.AddDays(-1);
            cf = ctTo.Value.AddDays(-(len - 1));
        }

        var grain = data.Grain?.Trim().ToLowerInvariant();
        if (grain is not null && !SemanticCatalog.Grains.Contains(grain))
        {
            throw new ComputeException($"Шаг времени — day, week, month, quarter или year, а не «{data.Grain}».");
        }

        return data with
        {
            From = from, To = to, CompareFrom = cf, CompareTo = ctTo, Grain = grain,
            Metrics = data.Metrics.Select(m => m.Trim().ToLowerInvariant()).Where(m => m.Length > 0).Distinct().ToList(),
            Dimension = data.Dimension is { Length: > 0 } d ? SemanticCatalog.Dimension(d)?.Code ?? d.Trim().ToLowerInvariant() : null,
        };
    }

    private static void ValidateShape(ComputeMethod method, ComputeData data)
    {
        if (method.MaxMetrics > 0)
        {
            StatMath.Require(data.Metrics.Count >= method.MinMetrics,
                $"Методу «{method.Title}» нужно показателей: {method.MinMetrics}{(method.MaxMetrics > method.MinMetrics ? $"–{method.MaxMetrics}" : "")}.");
            StatMath.Require(data.Metrics.Count <= method.MaxMetrics, $"Методу «{method.Title}» — не больше {method.MaxMetrics} показателей.");
        }

        foreach (var m in data.Metrics)
        {
            if (SemanticCatalog.Metric(m) is null)
            {
                throw new ComputeException($"Показателя «{m}» нет. Есть: {string.Join(", ", SemanticCatalog.Metrics.Select(x => x.Code))}.");
            }
        }

        if (method.NeedsDimension)
        {
            StatMath.Require(data.Dimension is not null, $"Методу «{method.Title}» нужно измерение (dimension): регион, агент, товар, группа, точка…");
        }

        if (data.Dimension is { } dim && SemanticCatalog.Dimension(dim) is null)
        {
            throw new ComputeException($"Измерения «{dim}» нет. Есть: {string.Join(", ", SemanticCatalog.Dimensions.Select(x => x.Code))}.");
        }
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..32].ToLowerInvariant();

    internal static string N(double value, int digits = 1) => value.ToString("N" + digits, Ru);

    internal static string P(double p) => p < 0.001 ? "< 0,001" : p.ToString("0.000", Ru);

    /// <summary>Параметры метода из JSON: числа, строки, даты, списки строк.</summary>
    internal sealed class Params(IReadOnlyDictionary<string, JsonElement>? values)
    {
        private JsonElement? Get(string name) => values is not null && values.TryGetValue(name, out var v) && v.ValueKind is not JsonValueKind.Null ? v : null;

        public double? Number(string name) => Get(name) is { } v
            ? v.ValueKind == JsonValueKind.Number ? v.GetDouble()
            : v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString()?.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d
            : throw new ComputeException($"Параметр {name} — число.")
            : null;

        public int Int(string name, int fallback, int min, int max) => Number(name) is { } d ? Math.Clamp((int)Math.Round(d), min, max) : fallback;

        public string? Text(string name) => Get(name) is { } v ? v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString() : null;

        public DateOnly? Date(string name) => Text(name) is { Length: > 0 } s
            ? DateOnly.TryParse(s, CultureInfo.InvariantCulture, out var d) ? d : throw new ComputeException($"Параметр {name} — дата ГГГГ-ММ-ДД.")
            : null;

        public IReadOnlyList<string> List(string name) => Get(name) is { } v
            ? v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().Select(x => x.ToString()).Where(x => x.Length > 0).ToList()
            : (v.GetString() ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
            : [];
    }

    /// <summary>Итог метода: результат, вывод кодом, класс вывода, ограничения метода на этих данных.</summary>
    internal sealed record MethodResult(object? Value, string? Summary, string? EvidenceClass, IReadOnlyList<string> Limitations)
    {
        public string Status { get; init; } = "ok";
        public string? Error { get; init; }
    }

    /// <summary>Округление дробных чисел в JSON до 6 значащих цифр (без потери смысла, без «шума» двоичной арифметики).</summary>
    private sealed class RoundedDoubleConverter : JsonConverter<double>
    {
        public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetDouble();

        public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                writer.WriteNullValue();
                return;
            }

            if (value == 0)
            {
                writer.WriteNumberValue(0);
                return;
            }

            var digits = Math.Clamp(5 - (int)Math.Floor(Math.Log10(Math.Abs(value))), 0, 10);
            writer.WriteNumberValue(Math.Round(value, digits));
        }
    }

    /// <summary>
    /// Загрузка данных расчёта движком срезов: все страницы результата, журнал запросов (id, что, сколько строк), источники, область прав,
    /// полнота, последний день данных (пустые дни позже него — не продажи, а отставание загрузки).
    /// </summary>
    internal sealed class Loader(SemanticEngine engine, QueryCaller caller, AnalytikPrincipal principal, ComputeData data, CancellationToken ct)
    {
        public List<Guid> QueryIds { get; } = [];
        public List<AnalyticsSource> Sources { get; } = [];
        public List<string> Notes { get; } = [];
        public List<object> Log { get; } = [];
        public StringBuilder Fingerprint { get; } = new();
        public string? Scope { get; private set; }
        public bool Complete { get; private set; } = true;
        public DateOnly? DataThrough { get; private set; }
        public ComputeData Data => data;

        /// <summary>Строки среза: ключи (id) и подписи измерений, значения показателей (null — нет значения).</summary>
        public async Task<IReadOnlyList<SliceRow>> RowsAsync(IReadOnlyList<string> metrics, IReadOnlyList<string> dims, DateOnly from, DateOnly to,
            string? grain = null, bool fillZeros = false, IReadOnlyList<AnalyticsFilter>? filters = null)
        {
            var rows = new List<SliceRow>();
            var offset = 0;
            while (true)
            {
                var query = new AnalyticsQuery
                {
                    Metrics = metrics, Dimensions = dims, DateFrom = from, DateTo = to, Grain = grain, Filters = filters ?? data.Filters, Segment = data.Segment,
                    FillZeros = fillZeros, IncludeTotals = false, Limit = SemanticEngine.MaxLimit, Offset = offset,
                };
                var r = await engine.RunAsync(query, caller, ct, principal);
                QueryIds.Add(r.QueryId);
                Scope ??= r.Scope;
                Complete &= r.Complete;
                foreach (var s in r.Sources)
                {
                    if (!Sources.Any(x => x.Title == s.Title && x.Tables == s.Tables))
                    {
                        Sources.Add(s);
                    }

                    if (s.DataThrough is { } dt && (DataThrough is null || dt < DataThrough))
                    {
                        DataThrough = dt;
                    }
                }

                var dimCount = dims.Count;
                for (var i = 0; i < r.Rows.Count; i++)
                {
                    var values = new double?[metrics.Count];
                    for (var m = 0; m < metrics.Count; m++)
                    {
                        var col = r.Columns.ToList().FindIndex(c => c.Code == metrics[m]);
                        values[m] = col >= 0 && r.Rows[i][col] is { } v ? Convert.ToDouble(v, CultureInfo.InvariantCulture) : null;
                    }

                    var keys = Enumerable.Range(0, dimCount).Select(d => r.Keys[i][d]?.ToString() ?? "").ToArray();
                    var labels = Enumerable.Range(0, dimCount).Select(d => r.Rows[i][d]?.ToString() ?? "").ToArray();
                    rows.Add(new SliceRow(keys, labels, values));
                    Fingerprint.Append(string.Join('|', keys)).Append('=').Append(string.Join(';', values)).Append('\n');
                }

                Log.Add(new { id = r.QueryId, metrics, dimensions = dims, from, to, grain, rows = r.Rows.Count, complete = r.Complete });
                if (!r.HasMore || r.Rows.Count == 0)
                {
                    break;
                }

                offset += r.Rows.Count;
            }

            return Distinct(rows, dims.Count);
        }

        /// <summary>
        /// Одинаковые подписи разных записей (две точки «Азамат») различаются id в скобках: расчёты складывают и показывают участников по подписи,
        /// и без этого две точки слились бы в одну или сломали бы разбор.
        /// </summary>
        private static IReadOnlyList<SliceRow> Distinct(List<SliceRow> rows, int dims)
        {
            for (var d = 0; d < dims; d++)
            {
                var clashes = rows.GroupBy(r => r.Labels[d]).Where(g => g.Select(r => r.Keys[d]).Distinct().Count() > 1).Select(g => g.Key).ToHashSet();
                if (clashes.Count == 0)
                {
                    continue;
                }

                foreach (var r in rows.Where(r => clashes.Contains(r.Labels[d])))
                {
                    r.Labels[d] = $"{r.Labels[d]} ({r.Keys[d]})";
                }
            }

            return rows;
        }

        /// <summary>Ряд по шагу времени: пустые шаги — ноль; шаги позже последнего дня данных и неполный последний шаг отбрасываются (с пометкой).</summary>
        public async Task<IReadOnlyList<(DateOnly Date, double Value)>> SeriesAsync(string metric, string grain, DateOnly from, DateOnly to)
        {
            var rows = await RowsAsync([metric], ["date"], from, to, grain, fillZeros: true);
            var points = rows.Select(r => (Date: DateOnly.ParseExact(r.Keys[0], "yyyy-MM-dd", CultureInfo.InvariantCulture), Value: r.Values[0] ?? 0))
                .OrderBy(p => p.Date).ToList();
            var through = DataThrough is { } dt && dt < to ? dt : to;
            if (through < to)
            {
                Notes.Add($"Данные источника — по {through:dd.MM.yyyy}: шаги позже не учтены (это отставание загрузки, а не нулевые продажи).");
            }

            var complete = points.Where(p => End(p.Date, grain) <= through).ToList();
            if (complete.Count < points.Count(p => p.Date <= through))
            {
                Notes.Add("Последний неполный шаг времени не учтён.");
            }

            return complete;
        }

        public static DateOnly End(DateOnly start, string grain) => grain switch
        {
            "week" => start.AddDays(6),
            "month" => start.AddMonths(1).AddDays(-1),
            "quarter" => start.AddMonths(3).AddDays(-1),
            "year" => start.AddYears(1).AddDays(-1),
            _ => start,
        };
    }

    /// <summary>Строка среза: id и подписи измерений, значения показателей.</summary>
    internal sealed record SliceRow(string[] Keys, string[] Labels, double?[] Values)
    {
        public string Label => string.Join(" · ", Labels);
        public double Value => Values[0] ?? 0;
    }
}
