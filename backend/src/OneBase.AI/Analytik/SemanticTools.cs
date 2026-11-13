using System.Globalization;
using System.Text;
using System.Text.Json;
using OneBase.AI.Consultant;
using OneBase.AI.Knowledge;
using OneBase.AI.Tools;
using OneBase.AI.Tools.Data;
using OneBase.Application.Analytik.Business;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Semantic;
using OneBase.Application.Security;

namespace OneBase.AI.Analytik;

/// <summary>
/// analytics_query — универсальный срез данных: модель описывает показатели, измерения, шаг, период и фильтры, движок сам строит запрос по
/// правилам «Продаж», проверяет полноту и возвращает строки с источником. Есть у консультанта и у аналитиков: вопрос, на который нет готового
/// отчёта («выручка по товарным группам за каждый день по регионам»), считается здесь, а не заканчивается словами «данных нет».
/// </summary>
public sealed class AnalyticsQueryTool(SemanticEngine engine) : DataTool
{
    public const string ToolName = "analytics_query";
    public const int ChatRows = 200;

    public override string Name => ToolName;
    public override string Title => "Срез данных (универсальный движок)";
    public override string? Source => KnowledgeSources.SalesSlices;
    public override string? RequiredPermission => Permissions.SalesRead;
    public override IReadOnlyList<string>? AlternativePermissions => [Permissions.FinanceRead, Permissions.AnalytikUse];

    public override string Description =>
        "Любой срез продаж, плана, оплат и остатков: показатели × измерения × шаг времени × период × фильтры — одним вызовом, с итогами, " +
        "сравнением периодов и проверкой полноты. Примеры: выручка по товарным группам за каждый день (metrics=[revenue,kg], dimensions=[date," +
        "product_group], grain=day); регион × товарная группа (dimensions=[region,product_group]); день × регион × группа; топ-10 групп (sort revenue " +
        "desc, limit 10); группы с падением (compare=previous, сортировка по revenue__pct); план и факт по регионам (metrics=[kg,plan_kg," +
        "plan_execution], dimensions=[region], grain=month); товары к пополнению (metrics=[stock_units,cover_days,need_units], dimensions=[product]). " +
        "Числа считает сервер по правилам «Продаж» (дата приёмки, возвраты, валюта, каналы, исключённые филиалы); сам их не пересчитывай и не " +
        "дополняй. Дни — фактические продажи дня: не заменяй их среднемесячными и не делись месячными итогами по дням.";

    public override string? UsageHint =>
        "Если готовый инструмент продаж не даёт нужного разреза (дни × товарные группы, регион × группа, агент × товар, точки по дням…), " +
        "вызывай analytics_query, а не отвечай «данных нет». Названия регионов, групп, агентов и товаров — как сказал пользователь, сервер " +
        "сопоставит их сам. complete=false — перечисли из checks, чего не хватает и за какой период; данные есть по дату из sources. " +
        "Большой список (has_more) — create_report с этим инструментом и all_rows=true или ссылка link («Срезы»).";

    public override JsonElement InputSchema { get; } = Schema(
        ("metrics", new
        {
            type = "array",
            items = new { type = "string", @enum = SemanticCatalog.Metrics.Select(m => m.Code).ToArray() },
            description = "Показатели: " + string.Join("; ", SemanticCatalog.Metrics.Select(m => $"{m.Code} — {m.Title}, {m.Unit}")),
        }),
        ("dimensions", new
        {
            type = "array",
            items = new { type = "string", @enum = SemanticCatalog.Dimensions.Select(d => d.Code).ToArray() },
            description = "Измерения (до 5, любые совместимые): " + string.Join("; ", SemanticCatalog.Dimensions.Select(d => $"{d.Code} — {d.Title}")),
        }),
        ("grain", new { type = "string", @enum = SemanticCatalog.Grains.ToArray(), description = "Шаг для измерения date: day (по умолчанию), week, month, quarter, year." }),
        ("period", new
        {
            type = "string",
            @enum = new[] { "today", "yesterday", "week", "month", "quarter", "year", "last_month", "last_7_days", "last_30_days", "last_90_days", "last_3_months", "last_6_months", "last_12_months" },
            description = "Готовый период, если не заданы date_from/date_to: month — с начала месяца, last_month — прошлый месяц целиком, last_3_months — 3 полных месяца и т.п.",
        }),
        ("date_from", new { type = "string", description = "Начало периода YYYY-MM-DD." }),
        ("date_to", new { type = "string", description = "Конец периода YYYY-MM-DD (включительно)." }),
        ("filters", new
        {
            type = "array",
            description = "Фильтры: {dimension, op: in | not_in | contains, values: [...]} — значения id или названия («Самарканд», «Кекс», имя агента); «Без региона» — пустое значение.",
            items = new
            {
                type = "object",
                properties = new
                {
                    dimension = new { type = "string" },
                    op = new { type = "string", @enum = new[] { "in", "not_in", "contains" } },
                    values = new { type = "array", items = new { type = "string" } },
                },
                required = new[] { "dimension", "values" },
            },
        }),
        ("compare", new { type = "string", @enum = new[] { "previous", "year_ago", "custom" }, description = "Сравнение: previous — предыдущий такой же отрезок (целые месяцы — предыдущие месяцы), year_ago, custom (compare_from/compare_to). Добавляет колонки {metric}__prev, __delta, __pct." }),
        ("compare_from", new { type = "string", description = "Начало периода сравнения YYYY-MM-DD (compare=custom)." }),
        ("compare_to", new { type = "string", description = "Конец периода сравнения YYYY-MM-DD (compare=custom)." }),
        ("sort", new
        {
            type = "array",
            description = "Сортировка: [{by: код показателя или измерения (или revenue__pct при сравнении), dir: asc|desc}]. По умолчанию — дата, затем первый показатель по убыванию.",
            items = new { type = "object", properties = new { by = new { type = "string" }, dir = new { type = "string", @enum = new[] { "asc", "desc" } } }, required = new[] { "by" } },
        }),
        ("limit", new { type = "integer", description = $"Сколько строк вернуть (по умолчанию {ChatRows}; весь список — all_rows=true или create_report)." }),
        ("offset", new { type = "integer", description = "Сколько строк пропустить (следующая страница)." }),
        ("segment", new { type = "string", @enum = new[] { "secondary", "factory", "all" }, description = "Продажи: secondary — вторичка (по умолчанию, как «Продажи»), factory — «Завод» (экспорт и опт), all — вместе." }),
        ("plan_source", new { type = "string", @enum = new[] { "auto", "regions", "agents" }, description = "План: auto (как «Продажи»), regions (Google Sheets), agents (Linko)." }),
        ("plan_kind", new { type = "string", @enum = new[] { "rop", "factory", "primary" }, description = "Вид плана регионов: rop (по умолчанию), factory, primary." }),
        ("cover_days", new { type = "integer", description = "Дней запаса для need_units (по умолчанию 14)." }),
        ("fill_zeros", new { type = "boolean", description = "Добавить дни без продаж нулями (для графика). Только если пользователь просит непрерывный ряд." }));

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        AnalyticsQuery query;
        try
        {
            query = Parse(args, engine.Today);
        }
        catch (FormatException ex)
        {
            return ToolResult.Failure(Name, ToolFailures.InvalidArguments, ex.Message);
        }

        var caller = new QueryCaller(context.UserId, "chat", context.AgentCode, context.ConversationId);
        AnalyticsResult r;
        string? repaired = null;
        try
        {
            r = await engine.RunAsync(query, caller, ct);
        }
        catch (AnalyticsQueryException ex) when (ex.Code != "denied")
        {
            // Query Repair: исправление строит код. Безопасное (смысл вопроса тот же) выполняется сразу, остальное — подсказкой с готовым запросом.
            var repair = QueryRepair.Suggest(query, ex);
            if (repair is not { Safe: true, Queries.Count: 1 })
            {
                return RepairFailure(ex, repair);
            }

            try
            {
                query = repair.Queries[0];
                r = await engine.RunAsync(query, caller, ct);
                repaired = $"Исходный срез отклонён ({ex.Message}). Исправлено автоматически: {repair.Hint}";
            }
            catch (AnalyticsQueryException again)
            {
                return RepairFailure(again, QueryRepair.Suggest(query, again));
            }
        }
        catch (AnalyticsQueryException ex)
        {
            return ToolResult.Failure(Name, ToolFailures.Forbidden, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ToolResult.Failure(Name, ToolFailures.Forbidden, ex.Message);
        }

        var link = Link(query);
        var codes = r.Columns.Select(c => c.Code).ToList();
        return Data(new
        {
            Period = new { From = r.From.ToString("yyyy-MM-dd"), To = r.To.ToString("yyyy-MM-dd"), Days = r.To.DayNumber - r.From.DayNumber + 1 },
            Compare = r.CompareFrom is { } cf ? new { From = cf.ToString("yyyy-MM-dd"), To = r.CompareTo!.Value.ToString("yyyy-MM-dd") } : null,
            Grain = query.Dimensions?.Contains("date") == true ? query.Grain ?? "day" : null,
            Metrics = SemanticCatalog.Expand(query.Metrics.Select(SemanticCatalog.Metric).OfType<MetricDef>())
                .Where(m => query.Metrics.Contains(m.Code)).Select(m => new { m.Code, m.Title, m.Unit, m.Formula, m.Note }),
            Columns = r.Columns.Select(c => new { c.Code, c.Title, c.Unit }),
            Rows = r.Rows.Select(row => codes.Select((c, i) => (c, row[i])).ToDictionary(x => x.c, x => x.Item2)),
            Totals = r.Totals,
            CompareTotals = r.CompareTotals,
            TotalRows = r.TotalRows,
            ReturnedRows = r.Rows.Count,
            HasMore = r.HasMore,
            r.Complete,
            Checks = r.Checks,
            Notes = r.Notes,
            Sources = r.Sources.Select(s => new { s.Title, s.Tables, SyncedAt = s.SyncedAt?.ToString("yyyy-MM-dd HH:mm"), DataThrough = s.DataThrough?.ToString("yyyy-MM-dd"), s.Rules }),
            r.Scope,
            Repaired = repaired,
            Link = link,
            Instructions = "Числа ответа — только из rows и totals; доли, средние и разницы уже посчитаны. complete=false — назови, чего не хватает (checks). " +
                           "В ответе дай таблицу (первые строки) и вывод; полный список — ссылка link или create_report.",
        }, new DataSource("Аналитик → Срезы", $"{r.From:dd.MM.yyyy} — {r.To:dd.MM.yyyy}", link));
    }

    /// <summary>Отказ среза с исправлением для модели: что не так, что делать и готовые аргументы исправленного вызова (или нескольких).</summary>
    private ToolResult RepairFailure(AnalyticsQueryException ex, RepairSuggestion? repair)
    {
        var kind = ex.Code is "too_heavy" or "timeout" ? ToolFailures.OutOfRange : ToolFailures.InvalidArguments;
        var payload = new
        {
            error = new
            {
                tool = Name, kind, code = ex.Code, message = ex.Message, next = ToolFailures.Next(kind),
                repair = repair is null ? null : new { hint = repair.Hint, calls = repair.Queries.Select(Args).ToList() },
            },
        };
        return new ToolResult(false, JsonSerializer.Serialize(payload, Json), ErrorKind: kind);
    }

    /// <summary>Запрос движка → аргументы этого инструмента (для подсказки исправленного вызова).</summary>
    public static object Args(AnalyticsQuery q) => new
    {
        metrics = q.Metrics, dimensions = q.Dimensions, grain = q.Grain, date_from = q.DateFrom?.ToString("yyyy-MM-dd"), date_to = q.DateTo?.ToString("yyyy-MM-dd"),
        filters = q.Filters?.Select(f => new { dimension = f.Dimension, op = f.Op, values = f.Values }), compare = q.Compare,
        compare_from = q.CompareFrom?.ToString("yyyy-MM-dd"), compare_to = q.CompareTo?.ToString("yyyy-MM-dd"), segment = q.Segment,
        sort = q.Sort?.Select(s => new { by = s.By, dir = s.Dir }), limit = q.Limit,
    };

    /// <summary>Аргументы инструмента → запрос движка (даты, готовые периоды, фильтры, сортировка, лимит: в чате 200, в отчёте — все).</summary>
    public static AnalyticsQuery Parse(ToolArgs args, DateOnly today)
    {
        static DateOnly? Date(string? s, string name) => s is null ? null
            : DateOnly.TryParse(s, CultureInfo.InvariantCulture, out var d) ? d : throw new FormatException($"{name}: дата «{s}», ожидается YYYY-MM-DD.");
        var from = Date(args.Str("date_from"), "date_from");
        var to = Date(args.Str("date_to"), "date_to");
        if (from is null && to is null && args.Str("period") is { } period)
        {
            (from, to) = PeriodRange(period, today);
        }

        var limit = args.Report || args.All ? SemanticEngine.MaxLimit : args.Int("limit", 1, SemanticEngine.MaxLimit) ?? ChatRows;
        return new AnalyticsQuery
        {
            Metrics = Strings(args.Element("metrics")),
            Dimensions = Strings(args.Element("dimensions")),
            Grain = args.Str("grain"),
            DateFrom = from,
            DateTo = to,
            Filters = Filters(args.Element("filters")),
            Sort = Sorts(args.Element("sort")),
            Limit = limit,
            Offset = args.Int("offset", 0, 1_000_000),
            Compare = args.Str("compare"),
            CompareFrom = Date(args.Str("compare_from"), "compare_from"),
            CompareTo = Date(args.Str("compare_to"), "compare_to"),
            Segment = args.Str("segment"),
            PlanSource = args.Str("plan_source"),
            PlanKind = args.Str("plan_kind"),
            CoverDays = args.Int("cover_days", 1, 120),
            FillZeros = args.Bool("fill_zeros") == true,
        };
    }

    public static (DateOnly From, DateOnly To) PeriodRange(string period, DateOnly today)
    {
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        DateOnly Months(int n) => monthStart.AddMonths(-n);
        return period.Trim().ToLowerInvariant() switch
        {
            "last_month" => (Months(1), monthStart.AddDays(-1)),
            "last_7_days" => (today.AddDays(-6), today),
            "last_30_days" => (today.AddDays(-29), today),
            "last_90_days" => (today.AddDays(-89), today),
            "last_3_months" => (Months(3), monthStart.AddDays(-1)),
            "last_6_months" => (Months(6), monthStart.AddDays(-1)),
            "last_12_months" => (Months(12), monthStart.AddDays(-1)),
            var kind => AnalytikPeriod.Of(kind, null, null, today) is var p ? (p.From, p.To) : default,
        };
    }

    private static IReadOnlyList<string> Strings(JsonElement? e) => e switch
    {
        { ValueKind: JsonValueKind.Array } a => a.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : x.ToString())
            .OfType<string>().Where(s => s.Trim().Length > 0).ToList(),
        { ValueKind: JsonValueKind.String } s => (s.GetString() ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        _ => [],
    };

    internal static IReadOnlyList<AnalyticsFilter> Filters(JsonElement? e)
    {
        if (e is not { ValueKind: JsonValueKind.Array } a)
        {
            return [];
        }

        return a.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object).Select(x =>
        {
            var dim = x.TryGetProperty("dimension", out var d) ? d.GetString() ?? "" : "";
            var op = x.TryGetProperty("op", out var o) && o.ValueKind == JsonValueKind.String ? o.GetString() ?? "in" : "in";
            var values = x.TryGetProperty("values", out var v) ? Strings(v) : x.TryGetProperty("value", out var one) ? Strings(one.ValueKind == JsonValueKind.Array ? one : JsonSerializer.SerializeToElement(new[] { one.ToString() })) : [];
            return new AnalyticsFilter(dim, op, values);
        }).ToList();
    }

    private static IReadOnlyList<AnalyticsSort> Sorts(JsonElement? e) => e is { ValueKind: JsonValueKind.Array } a
        ? a.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String
            ? new AnalyticsSort(x.GetString() ?? "", "desc")
            : new AnalyticsSort(x.TryGetProperty("by", out var b) ? b.GetString() ?? "" : "", x.TryGetProperty("dir", out var dr) ? dr.GetString() : null)).ToList()
        : [];

    /// <summary>Ссылка на «Срезы» с этим запросом (base64url JSON) — открыть таблицу и график целиком.</summary>
    public static string Link(AnalyticsQuery query)
    {
        var json = JsonSerializer.Serialize(query with { Limit = null, Offset = null }, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        });
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"/analytik/explore?q={b64}";
    }
}

/// <summary>analytics_capabilities — реестр возможностей: что можно посчитать, по каким измерениям, за какие даты и где искать остальное.</summary>
public sealed class AnalyticsCapabilitiesTool(CapabilityRegistry registry) : DataTool
{
    public override string Name => "analytics_capabilities";
    public override string Title => "Какие данные есть (реестр возможностей)";
    public override string? Source => KnowledgeSources.SalesSlices;
    public override string? RequiredPermission => Permissions.SalesRead;
    public override IReadOnlyList<string>? AlternativePermissions => [Permissions.FinanceRead, Permissions.AnalytikUse];

    public override string Description =>
        "Реестр данных OneBase для аналитики: показатели (формула, единица, источник), измерения, шаги времени, за какие даты есть данные и когда " +
        "они загружены, какие данные отдают готовые инструменты отделов и чего в источниках нет совсем. Вызывай, прежде чем ответить, что данных нет.";

    public override JsonElement InputSchema { get; } = Schema();

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        var view = await registry.ViewAsync(new QueryCaller(context.UserId, "chat", context.AgentCode, context.ConversationId), ct);
        return Data(new
        {
            Facts = view.Facts.Select(f => new
            {
                f.Code, f.Title, f.Source, f.Detail, f.Metrics, f.Dimensions, f.Grains,
                DataFrom = f.First?.ToString("yyyy-MM-dd"), DataThrough = f.Last?.ToString("yyyy-MM-dd"), SyncedAt = f.SyncedAt?.ToString("yyyy-MM-dd HH:mm"),
                f.Status, f.Limits, Allowed = f.Allowed,
            }),
            Metrics = view.Metrics.Select(m => new { m.Code, m.Title, m.Unit, m.Fact, m.Formula, m.Note }),
            Dimensions = view.Dimensions.Select(d => new { d.Code, d.Title, d.Facts, d.Note }),
            view.Grains,
            OtherTools = view.Tools,
            NotInSources = view.Missing,
            LinkoSyncRuns = view.SyncRuns.Take(5).Select(r => new { r.Mode, Started = r.StartedAt.ToString("yyyy-MM-dd HH:mm"), r.Status, r.Rows, r.Error }),
            Instructions = "Срез по этим показателям и измерениям — analytics_query (любые совместимые сочетания). Нет показателя — посмотри OtherTools; " +
                           "нет и там — скажи, чего не хватает в источниках (NotInSources), а не «данных нет».",
        }, new DataSource("Аналитик → Срезы: реестр данных"));
    }
}
