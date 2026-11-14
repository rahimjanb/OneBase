using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using OneBase.AI.Consultant;
using OneBase.AI.Knowledge;
using OneBase.AI.Tools;
using OneBase.AI.Tools.Data;
using OneBase.Application.Analytik.Compute;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Semantic;
using OneBase.Application.Security;

namespace OneBase.AI.Analytik;

/// <summary>
/// analytik_compute — вычислительный движок для аналитиков: статистика, тренд, сезонность, аномалии, прогноз, корреляция, регрессия,
/// запаздывание, вклад в изменение, PVM, до/после, разность разностей, ABC/XYZ, RFM, когорты, кластеры, отток, структура, отклонение от плана,
/// юнит-экономика, поступление денег, здоровье запаса. Данные — срез движка (с правами пользователя): модель называет показатели, измерение и
/// период, но не передаёт чисел. Каждый расчёт — в журнале analytik.Computations с алгоритмом, версией, входом и классом вывода.
/// </summary>
public sealed class ComputeTool(ComputeService compute, SemanticEngine engine) : DataTool
{
    public const string ToolName = "analytik_compute";

    public override string Name => ToolName;
    public override string Title => "Расчёт (статистика, причины, сегменты)";
    public override int Level => ToolLevels.Analyze;
    public override string? Source => KnowledgeSources.SalesStats;
    public override string? RequiredPermission => Permissions.SalesRead;
    public override IReadOnlyList<string>? AlternativePermissions => [Permissions.FinanceRead, Permissions.AnalytikUse];

    public override string Description =>
        "Statistics Engine — детерминированный расчёт по срезу данных OneBase (код, не модель): " +
        string.Join("; ", ComputeCatalog.Methods.Select(m => $"{m.Code} — {m.Title}")) + ". " +
        "Данные задаёшь как срез: metrics, dimension, grain, период, фильтры (как analytics_query); или query_id — id уже выполненного среза; или " +
        "series — структурированные ряды (имя, значения, даты), если данные пришли из другого инструмента. Числа формулой не передавай. " +
        "Результат: summary (вывод кодом), level (FACT, CALCULATED, STATISTICALLY_SUPPORTED, CORRELATION, HYPOTHESIS, FORECAST, UNVERIFIED), " +
        "evidence_class, limitations, computation_id. Корреляция — «связано», не «из-за»; причину называй только при evidence_class tested_causal.";

    public override string? UsageHint =>
        "«Почему изменилось» — сначала drivers (по region, product_group, agent) и pvm, затем проверь гипотезы: before_after / did (есть дата " +
        "события и контрольная группа), correlation / regression / lag (связь показателей). Сегменты — abc, abc_xyz, rfm, cohorts, clusters, churn. " +
        "Корреляции, регрессии, тренды, сезонность, переломы, аномалии по рядам — только здесь, а не формулами в calculate: correlation_matrix " +
        "(все пары с поправкой на множественные сравнения; members=product_group — группы против итога и остальных), partial_correlation, " +
        "rolling_correlation, regression (model=linear | polynomial | ridge | lasso), decomposition, change_points, smoothing, anomaly_scan.";

    public override JsonElement InputSchema { get; } = Schema(
        ("method", new
        {
            type = "string",
            @enum = ComputeCatalog.Methods.Select(m => m.Code).ToArray(),
            description = string.Join(" | ", ComputeCatalog.Methods.Select(m =>
                $"{m.Code}: {m.Title}; показателей {m.MinMetrics}–{m.MaxMetrics}{(m.NeedsDimension ? ", нужно dimension" : "")}" +
                $"{(m.NeedsGrain ? ", шаг grain" : "")}{(m.NeedsCompare ? ", сравнение (по умолчанию — предыдущий такой же отрезок)" : "")}" +
                $"{(m.Params.Length > 0 ? "; params: " + m.Params : "")}")),
        }),
        ("metrics", new
        {
            type = "array",
            items = new { type = "string" },
            description = "Показатели среза (первый — главный / отклик регрессии / ведущий ряд lag): " + string.Join(", ", SemanticCatalog.Metrics.Select(m => m.Code)) +
                          "; при series — имена рядов.",
        }),
        ("dimension", new { type = "string", @enum = SemanticCatalog.Dimensions.Select(d => d.Code).Where(c => c != "date").ToArray(), description = "Измерение участников: region, agent, product_group, product, market…" }),
        ("grain", new { type = "string", @enum = SemanticCatalog.Grains.ToArray(), description = "Шаг времени рядов: day, week, month…" }),
        ("period", new
        {
            type = "string",
            @enum = new[] { "month", "quarter", "year", "last_month", "last_7_days", "last_30_days", "last_90_days", "last_3_months", "last_6_months", "last_12_months" },
            description = "Готовый период, если нет date_from/date_to (по умолчанию — последние 90 дней).",
        }),
        ("date_from", new { type = "string", description = "Начало периода YYYY-MM-DD." }),
        ("date_to", new { type = "string", description = "Конец периода YYYY-MM-DD." }),
        ("compare_from", new { type = "string", description = "Начало периода сравнения (база) YYYY-MM-DD." }),
        ("compare_to", new { type = "string", description = "Конец периода сравнения YYYY-MM-DD." }),
        ("filters", new
        {
            type = "array",
            description = "Фильтры среза: {dimension, op: in | not_in | contains, values: [...]}.",
            items = new
            {
                type = "object",
                properties = new { dimension = new { type = "string" }, op = new { type = "string" }, values = new { type = "array", items = new { type = "string" } } },
                required = new[] { "dimension", "values" },
            },
        }),
        ("segment", new { type = "string", @enum = new[] { "secondary", "factory", "all" }, description = "Продажи: secondary (по умолчанию), factory, all." }),
        ("query_id", new { type = "string", description = "Id уже выполненного среза (analytics_query → query_id): расчёт по его показателям, шагу, периоду и фильтрам." }),
        ("series", new
        {
            type = "array",
            description = "Структурированные ряды вместо среза (до 25 рядов, 5 000 значений): [{name, values: [числа или null], dates: [YYYY-MM-DD] — необязательно}]. " +
                          "Только для статистических методов; metrics тогда — имена рядов (или пусто — все).",
            items = new
            {
                type = "object",
                properties = new
                {
                    name = new { type = "string" },
                    values = new { type = "array", items = new { type = new[] { "number", "null" } } },
                    dates = new { type = "array", items = new { type = "string" } },
                },
                required = new[] { "name", "values" },
            },
        }),
        ("params", new { type = "object", description = "Параметры метода: method (pearson | spearman | kendall), members, top, model, degree, lambda, window, period, kind, " +
                                                        "horizon, change_date, treatment, control, k, max_lag, check_last, lead_days…" }));

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        ComputeRequest request;
        try
        {
            request = Parse(args, engine.Today);
        }
        catch (FormatException ex)
        {
            return ToolResult.Failure(Name, ToolFailures.InvalidArguments, ex.Message);
        }

        ComputeOutcome outcome;
        try
        {
            outcome = await compute.RunAsync(request, new ComputeContext(context.UserId, context.Depth > 0 ? "investigation" : "chat", null, context.ConversationId,
                context.AgentCode), ct);
        }
        catch (ComputeException ex)
        {
            return ToolResult.Failure(Name, ToolFailures.InvalidArguments, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ToolResult.Failure(Name, ToolFailures.Forbidden, ex.Message);
        }

        return Present(outcome, Name);
    }

    /// <summary>Итог расчёта для модели: длинные списки — первые строки (полный результат — в журнале расчётов).</summary>
    internal static ToolResult Present(ComputeOutcome o, string tool)
    {
        if (o.Status == "error")
        {
            return ToolResult.Failure(tool, ToolFailures.ToolError, o.Error ?? "Расчёт не выполнен.");
        }

        var result = o.Result is null ? null : JsonSerializer.SerializeToNode(o.Result, ComputeServiceJson.Options);
        var truncated = new List<string>();
        Trim(result, "result", truncated);
        var payload = new JsonObject
        {
            ["computation_id"] = o.Id.ToString(),
            ["method"] = o.Method,
            ["title"] = o.Title,
            ["status"] = o.Status,
            ["summary"] = o.Summary ?? o.Error,
            ["evidence_class"] = o.EvidenceClass,
            ["evidence_class_title"] = o.EvidenceClass is null ? null : EvidenceClasses.Title(o.EvidenceClass),
            ["level"] = LevelOf(o),
            ["level_title"] = EvidenceLevels.Title(LevelOf(o)),
            ["period"] = new JsonObject { ["from"] = o.From.ToString("yyyy-MM-dd"), ["to"] = o.To.ToString("yyyy-MM-dd") },
            ["compare"] = o.CompareFrom is { } cf ? new JsonObject { ["from"] = cf.ToString("yyyy-MM-dd"), ["to"] = o.CompareTo!.Value.ToString("yyyy-MM-dd") } : null,
            ["algorithm"] = o.Algorithm,
            ["version"] = o.Version,
            ["result"] = result,
            ["limitations"] = new JsonArray(o.Limitations.Select(l => (JsonNode?)l).ToArray()),
            ["sources"] = new JsonArray(o.Sources.Select(s => (JsonNode?)new JsonObject
            {
                ["title"] = s.Title, ["synced_at"] = s.SyncedAt?.ToString("yyyy-MM-dd HH:mm"), ["data_through"] = s.DataThrough?.ToString("yyyy-MM-dd"),
            }).ToArray()),
            ["scope"] = o.Scope,
        };
        if (truncated.Count > 0)
        {
            payload["truncated_lists"] = new JsonArray(truncated.Select(t => (JsonNode?)t).ToArray());
        }

        if (o.Status == "insufficient")
        {
            payload["next"] = "Данных для метода не хватает (summary). Измени период, шаг или измерение, возьми другой метод или назови ограничение — не подставляй числа.";
        }

        return new ToolResult(true, payload.ToJsonString(DataTool.Json), null,
            [new DataSource($"Аналитик → расчёт «{o.Title}»", $"{o.From:dd.MM.yyyy} — {o.To:dd.MM.yyyy}", null)]);
    }

    /// <summary>Уровень доказательности итога: связи — CORRELATION, прогнозы — FORECAST, описания и разложения — CALCULATED, без результата — UNVERIFIED.</summary>
    public static string LevelOf(ComputeOutcome o) => o.Status != "ok" ? EvidenceLevels.Unverified
        : o.Method is "forecast" or "smoothing" ? EvidenceLevels.Forecast
        : EvidenceLevels.FromClass(o.EvidenceClass, o.Method is "correlation" or "partial_correlation" or "rolling_correlation" or "correlation_matrix" or "lag" or "regression");

    /// <summary>Списки длиннее ShownItems — первые строки; что обрезано — в truncated.</summary>
    private static void Trim(JsonNode? node, string path, List<string> truncated)
    {
        switch (node)
        {
            case JsonArray array:
                if (array.Count > ComputeService.ShownItems)
                {
                    truncated.Add($"{path}: показано {ComputeService.ShownItems} из {array.Count}");
                    while (array.Count > ComputeService.ShownItems)
                    {
                        array.RemoveAt(array.Count - 1);
                    }
                }

                for (var i = 0; i < array.Count; i++)
                {
                    Trim(array[i], $"{path}[{i}]", truncated);
                }

                break;
            case JsonObject obj:
                foreach (var (key, value) in obj.ToList())
                {
                    Trim(value, $"{path}.{key}", truncated);
                }

                break;
        }
    }

    public static ComputeRequest Parse(ToolArgs args, DateOnly today)
    {
        static DateOnly? Date(string? s, string name) => s is null ? null
            : DateOnly.TryParse(s, CultureInfo.InvariantCulture, out var d) ? d : throw new FormatException($"{name}: дата «{s}», ожидается YYYY-MM-DD.");
        var from = Date(args.Str("date_from"), "date_from");
        var to = Date(args.Str("date_to"), "date_to");
        if (from is null && to is null && args.Str("period") is { } period)
        {
            (from, to) = AnalyticsQueryTool.PeriodRange(period, today);
        }

        var metrics = args.Element("metrics") is { ValueKind: JsonValueKind.Array } m
            ? m.EnumerateArray().Select(x => x.ToString()).Where(x => x.Length > 0).ToList()
            : args.Str("metrics") is { } ms ? ms.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList() : [];
        var parameters = args.Element("params") is { ValueKind: JsonValueKind.Object } p
            ? p.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone())
            : null;
        Guid? queryId = args.Str("query_id") is { Length: > 0 } q
            ? Guid.TryParse(q, out var g) ? g : throw new FormatException("query_id — не id среза (ожидается GUID из результата analytics_query).")
            : null;
        return new ComputeRequest
        {
            Method = args.Str("method") ?? throw new FormatException("Нужен метод расчёта: method."),
            Data = new ComputeData
            {
                Metrics = metrics, Dimension = args.Str("dimension"), Grain = args.Str("grain"), From = from, To = to,
                CompareFrom = Date(args.Str("compare_from"), "compare_from"), CompareTo = Date(args.Str("compare_to"), "compare_to"),
                Filters = AnalyticsQueryTool.Filters(args.Element("filters")), Segment = args.Str("segment"),
                QueryId = queryId, Series = Series(args.Element("series")),
            },
            Params = parameters,
        };
    }

    /// <summary>Структурированные ряды из аргументов: имя, значения (числа или null), даты (необязательно).</summary>
    private static IReadOnlyList<InlineSeries>? Series(JsonElement? element)
    {
        if (element is not { ValueKind: JsonValueKind.Array } array)
        {
            return null;
        }

        var list = new List<InlineSeries>();
        foreach (var s in array.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object))
        {
            var name = s.TryGetProperty("name", out var n) ? n.ToString() : throw new FormatException("У ряда нет имени (name).");
            var values = s.TryGetProperty("values", out var v) && v.ValueKind == JsonValueKind.Array
                ? v.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.Number ? x.GetDouble() : x.ValueKind == JsonValueKind.String
                    && double.TryParse(x.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : (double?)null).ToList()
                : throw new FormatException($"У ряда «{name}» нет значений (values).");
            var dates = s.TryGetProperty("dates", out var dt) && dt.ValueKind == JsonValueKind.Array
                ? dt.EnumerateArray().Select(x => DateOnly.TryParse(x.GetString(), CultureInfo.InvariantCulture, out var day) ? day : throw new FormatException($"Ряд «{name}»: дата «{x}».")).ToList()
                : null;
            list.Add(new InlineSeries(name, values, dates));
        }

        return list;
    }
}

/// <summary>analytik_methods — какие аналитические методы есть по отделам, чем считаются и каких данных нет (Expert Analytics).</summary>
public sealed class MethodsTool(QueryEngine engine) : AnalytikTool(engine)
{
    public override string Name => "analytik_methods";
    public override string Title => "Методы анализа по отделам";

    public override string Description =>
        "Методы анализа отделов (продажи, финансы, производство, снабжение, склад, персонал, маркетинг): чем считается каждый — методом " +
        "analytik_compute, готовым инструментом OneBase или не считается из-за отсутствия данных (с причиной). Вызови, чтобы выбрать метод, и " +
        "прежде чем сказать, что анализ невозможен.";

    public override JsonElement InputSchema { get; } = Schema(
        ("domain", new { type = "string", @enum = new[] { "sales", "finance", "production", "supply", "warehouse", "hr", "marketing" }, description = "Отдел (пусто — все)." }));

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        var principal = await PrincipalAsync(context, ct);
        var domain = args.Str("domain");
        var methods = ComputeCatalog.Expert.Where(m => domain is null || m.Domain == domain).Select(m => new
        {
            m.Domain, m.Code, m.Title,
            How = m.Kind switch { "compute" => $"analytik_compute method={m.Implementation}", "tool" => $"инструмент {m.Implementation}", _ => "не считается" },
            Available = m.Kind != "missing" && (m.Permission is null || principal.Has(m.Permission)),
            Note = m.Kind == "missing" ? m.Note : m.Permission is { } perm && !principal.Has(perm) ? "Нет прав на данные этого метода." : m.Note,
        }).ToList();
        return Data(new
        {
            Methods = methods,
            Compute = ComputeCatalog.Methods.Select(m => new { m.Code, m.Title, m.Domain, m.Algorithm, m.Params, m.Limits }),
        });
    }
}

/// <summary>Параметры JSON результатов расчёта — те же, что у журнала (округление дробей, без NaN).</summary>
internal static class ComputeServiceJson
{
    public static JsonSerializerOptions Options => ComputeService.JsonOptions;
}
