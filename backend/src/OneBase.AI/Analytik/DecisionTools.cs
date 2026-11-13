using System.Globalization;
using System.Text;
using System.Text.Json;
using OneBase.AI.Consultant;
using OneBase.AI.Tools;
using OneBase.AI.Tools.Data;
using OneBase.Application.Analytik.Decisions;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Semantic;

namespace OneBase.AI.Analytik;

/// <summary>
/// analytik_decisions — память управленческих решений: что решили, когда, кто и почему, исходный показатель, ожидание, срок, факт и вердикт.
/// Аналитик опирается на неё, когда спрашивают, что уже пробовали и что сработало. Вердикт — сравнение «до / после», а не доказанная причина.
/// </summary>
public sealed class DecisionsTool(QueryEngine engine, DecisionService decisions) : AnalytikTool(engine)
{
    public override string Name => "analytik_decisions";
    public override string Title => "Решения и их результат";

    public override string Description =>
        "Память управленческих решений OneBase: решение, дата, автор, причина, показатель и его исходное значение, ожидание (рост / снижение / " +
        "без изменений, на сколько %), срок проверки, фактический результат и вердикт (achieved, partial, missed, inconclusive), связанные " +
        "исследования. Вердикт — сравнение до и после решения, без контрольной группы он не доказывает, что результат дало решение.";

    public override JsonElement InputSchema { get; } = Schema(
        ("status", new { type = "string", @enum = new[] { "active", "evaluated", "cancelled" }, description = "Статус решений (пусто — все)." }),
        ("q", new { type = "string", description = "Поиск по названию и причине." }));

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        var p = await PrincipalAsync(context, ct);
        var list = await decisions.ListAsync(p, args.Str("status"), args.Str("q"), ct);
        return Data(new
        {
            Decisions = args.Top(list, 30, "decisions").Select(d => new
            {
                d.Id, d.Title, d.Reason, DecidedOn = d.DecidedOn.ToString("yyyy-MM-dd"), Author = d.AuthorName, d.MetricTitle, d.Metric,
                Baseline = new { From = d.BaselineFrom.ToString("yyyy-MM-dd"), To = d.BaselineTo.ToString("yyyy-MM-dd"), Value = d.BaselineValue },
                Expected = new { d.ExpectedDirection, d.ExpectedChangePct, d.ExpectedText }, ReviewOn = d.ReviewOn.ToString("yyyy-MM-dd"), d.Status, d.Outcome,
                d.ActualValue, Result = d.Actual, d.Links, d.Due,
            }),
            Total = list.Count,
            Note = "Вердикт — «до / после» решения; причинность без контрольной группы не доказана.",
        }, new DataSource("Аналитик → Решения", null, "/analytik/decisions"));
    }
}

/// <summary>
/// analytik_decision_draft — уровень «подготовить» (Execution Safety): AI готовит черновик решения для записи в «Решения» по итогам
/// исследования, но ничего не сохраняет — человек проверяет черновик по ссылке и сохраняет сам.
/// </summary>
public sealed class DecisionDraftTool(QueryEngine engine, SemanticEngine semantic) : AnalytikTool(engine)
{
    public override string Name => "analytik_decision_draft";
    public override string Title => "Черновик решения";
    public override int Level => ToolLevels.Prepare;

    public override string Description =>
        "Подготовить черновик управленческого решения для «Аналитик → Решения» (не сохраняет): название, причина, показатель для проверки " +
        "(код движка срезов и фильтры), ожидание — рост / снижение / без изменений и на сколько %, дата решения и дата проверки. Возвращает ссылку, " +
        "по которой человек проверит и сохранит решение; после срока система сама сравнит факт с ожиданием.";

    public override JsonElement InputSchema { get; } = Schema(
        ("title", new { type = "string", description = "Решение (что делаем)." }),
        ("reason", new { type = "string", description = "Почему: вывод исследования, данные." }),
        ("metric", new { type = "string", @enum = SemanticCatalog.Metrics.Where(m => !m.Calculated).Select(m => m.Code).ToArray(), description = "Показатель проверки." }),
        ("filters", new
        {
            type = "array",
            items = new { type = "object", properties = new { dimension = new { type = "string" }, op = new { type = "string" }, values = new { type = "array", items = new { type = "string" } } } },
            description = "Фильтры показателя: регион, группа, агент…",
        }),
        ("expected_direction", new { type = "string", @enum = DecisionService.Directions }),
        ("expected_change_pct", new { type = "number", description = "На сколько % (в среднем за день к периоду до решения)." }),
        ("decided_on", new { type = "string", description = "Дата решения YYYY-MM-DD (по умолчанию сегодня)." }),
        ("review_on", new { type = "string", description = "Дата проверки YYYY-MM-DD (по умолчанию через 30 дней)." }));

    protected override Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        static DateOnly? Date(string? s) => s is { Length: > 0 } && DateOnly.TryParse(s, CultureInfo.InvariantCulture, out var d) ? d : null;
        var metric = args.Str("metric") ?? "revenue";
        if (SemanticCatalog.Metric(metric) is null)
        {
            return Task.FromResult(ToolResult.Failure(Name, ToolFailures.InvalidArguments, $"Показателя «{metric}» нет."));
        }

        var decidedOn = Date(args.Str("decided_on")) ?? semantic.Today;
        var draft = new
        {
            title = args.Str("title") ?? "", reason = args.Str("reason"), metric, filters = AnalyticsQueryTool.Filters(args.Element("filters")),
            expectedDirection = args.Str("expected_direction") is { } d && DecisionService.Directions.Contains(d) ? d : "up",
            expectedChangePct = args.Dec("expected_change_pct", -1000, 1000), decidedOn = decidedOn.ToString("yyyy-MM-dd"),
            reviewOn = (Date(args.Str("review_on")) ?? decidedOn.AddDays(30)).ToString("yyyy-MM-dd"),
        };
        var json = JsonSerializer.Serialize(draft, DataTool.Json);
        var link = "/analytik/decisions?draft=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return Task.FromResult(Data(new
        {
            Draft = draft, Link = link, Saved = false,
            Instructions = "Черновик не сохранён: дай пользователю ссылку link — он проверит и сохранит решение сам.",
        }));
    }
}
