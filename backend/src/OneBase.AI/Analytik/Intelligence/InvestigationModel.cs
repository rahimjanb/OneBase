using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using OneBase.AI.Consultant;
using OneBase.Application.Analytik.Compute;
using OneBase.Application.Analytik.Semantic;

namespace OneBase.AI.Analytik.Intelligence;

/// <summary>Статусы исследования.</summary>
public static class InvestigationStatuses
{
    public const string Planned = "planned";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Partial = "partial";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
}

/// <summary>Статус гипотезы — ставит код по результату проверки, а не модель (Multi-Hypothesis Engine).</summary>
public static class HypothesisStatuses
{
    public const string Confirmed = "confirmed";
    public const string PartiallyConfirmed = "partially_confirmed";
    public const string Rejected = "rejected";
    public const string Inconclusive = "inconclusive";

    public static string Title(string status) => status switch
    {
        Confirmed => "подтверждена",
        PartiallyConfirmed => "подтверждена частично",
        Rejected => "отклонена",
        _ => "не доказана",
    };
}

/// <summary>Роли исследования (Intelligence Orchestrator): включаются по сложности, не все сразу.</summary>
public static class InvestigationRoles
{
    public const string Planner = "planner";
    public const string Investigator = "investigator";
    public const string DomainExpert = "domain_expert";
    public const string Statistician = "statistician";
    public const string HypothesisAnalyst = "hypothesis_analyst";
    public const string Skeptic = "skeptic";
    public const string Reviewer = "reviewer";
    public const string DecisionAdvisor = "decision_advisor";

    public static string Title(string role) => role switch
    {
        Planner => "Планировщик",
        Investigator => "Исследователь",
        DomainExpert => "Эксперт отдела",
        Statistician => "Статистик",
        HypothesisAnalyst => "Аналитик гипотез",
        Skeptic => "Скептик",
        Reviewer => "Ревизор",
        DecisionAdvisor => "Советник по решениям",
        _ => role,
    };
}

/// <summary>Подзадача плана: вопрос, аналитик, от каких шагов зависит, зачем.</summary>
public sealed record InvestigationStep(string Id, string Question, string Analyst, IReadOnlyList<string> DependsOn, string? Purpose, int Round = 0);

/// <summary>Ожидание гипотезы: направление (up / down / any), участник (регион, группа, агент…), эффект PVM (price, volume, mix, new, lost), минимальная доля.</summary>
public sealed record HypothesisExpectation(string? Direction = null, string? Member = null, string? Effect = null, double? MinShare = null);

/// <summary>Гипотеза: формулировка, кто предложил (planner, replan, skeptic), проверка — расчёт движка, ожидание.</summary>
public sealed record PlannedHypothesis(string Id, string Statement, ComputeRequest? Test, HypothesisExpectation Expect, string Origin = "planner");

/// <summary>План исследования: цель, глубина (standard / deep), шаги, гипотезы, какие данные нужны.</summary>
public sealed record InvestigationPlan(string Goal, string Depth, IReadOnlyList<InvestigationStep> Steps, IReadOnlyList<PlannedHypothesis> Hypotheses,
    IReadOnlyList<string> DataNeeded, bool Fallback = false);

/// <summary>Промежуточный результат шага: статус, вывод, ключевые числа, источники, вызовов инструментов, неподтверждённые числа.</summary>
public sealed record StepOutcome(InvestigationStep Step, string Status, string Summary, IReadOnlyList<string> Findings, IReadOnlyList<Metric> Metrics,
    IReadOnlyList<DataSource> Sources, int ToolCalls, IReadOnlyList<string> Unverified, long Ms);

/// <summary>
/// Вердикт гипотезы: статус, класс вывода, уверенность (0–1), подтверждающие и опровергающие данные, чего не хватает, масштаб влияния, расчёт.
/// </summary>
public sealed record HypothesisVerdict(
    PlannedHypothesis Hypothesis, string Status, string? EvidenceClass, double Confidence, IReadOnlyList<string> Supporting, IReadOnlyList<string> Refuting,
    IReadOnlyList<string> Needed, double? Impact, string? ImpactUnit, Guid? ComputationId, string? Summary);

/// <summary>Противоречие: вид, текст, кто и что сообщил.</summary>
public sealed record Contradiction(string Kind, string Text, IReadOnlyList<string> Parties);

/// <summary>Замечание проверки ответа: вид, важность (error / warning), текст, действие (rewrite / retest / none).</summary>
public sealed record ReviewIssue(string Kind, string Severity, string Text, string Action = "rewrite");

/// <summary>Цикл проверки: номер, кто проверял (code / model), вердикт (approve / revise), замечания, что сделано.</summary>
public sealed record ReviewCycle(int Round, string By, string Verdict, IReadOnlyList<ReviewIssue> Issues, string? Action);

/// <summary>Событие трассы исследования: когда (мс от начала), вид, текст.</summary>
public sealed record TraceEvent(long AtMs, string Kind, string Text);

/// <summary>Разбор JSON модели (план, перепланирование, скептик) в записи исследования; неверное — пропускается, а не роняет исследование.</summary>
public static class InvestigationJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);

    /// <summary>Шаги плана: id, question, analyst, depends_on, purpose. Аналитики не из списка — BI-аналитику; больше max — отбрасываются.</summary>
    public static List<InvestigationStep> Steps(JsonElement root, IReadOnlySet<string> analysts, string fallbackAnalyst, int max, int round, string prefix = "s")
    {
        var steps = new List<InvestigationStep>();
        foreach (var e in JsonText.Array(root, "steps"))
        {
            if (steps.Count >= max || e.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var question = JsonText.Str(e, "question");
            if (string.IsNullOrWhiteSpace(question))
            {
                continue;
            }

            var id = JsonText.Str(e, "id") is { Length: > 0 } i ? i.Trim() : $"{prefix}{steps.Count + 1}";
            var analyst = JsonText.Str(e, "analyst") is { } a && analysts.Contains(a.Trim()) ? a.Trim() : fallbackAnalyst;
            var deps = JsonText.Array(e, "depends_on").Select(d => d.ToString().Trim()).Where(d => d.Length > 0).ToList();
            steps.Add(new InvestigationStep(id, question.Trim(), analyst, deps, JsonText.Str(e, "purpose"), round));
        }

        // Зависимость только от известных шагов этого раунда; цикл (s1 ↔ s2) разрывается — шаг идёт без зависимости.
        var ids = steps.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        steps = steps.Select(s => s with { DependsOn = s.DependsOn.Where(d => ids.Contains(d) && d != s.Id).Distinct().ToList() }).ToList();
        return BreakCycles(steps);
    }

    /// <summary>Волны выполнения по зависимостям (Tool Dependency Graph): шаги волны независимы и идут параллельно.</summary>
    public static IReadOnlyList<IReadOnlyList<InvestigationStep>> Waves(IReadOnlyList<InvestigationStep> steps, IReadOnlySet<string>? done = null)
    {
        var finished = new HashSet<string>(done ?? new HashSet<string>(), StringComparer.Ordinal);
        var left = steps.ToList();
        var waves = new List<IReadOnlyList<InvestigationStep>>();
        while (left.Count > 0)
        {
            var wave = left.Where(s => s.DependsOn.All(finished.Contains)).ToList();
            if (wave.Count == 0)
            {
                wave = [left[0]];
            }

            waves.Add(wave);
            foreach (var s in wave)
            {
                finished.Add(s.Id);
                left.Remove(s);
            }
        }

        return waves;
    }

    private static List<InvestigationStep> BreakCycles(List<InvestigationStep> steps)
    {
        var byId = steps.ToDictionary(s => s.Id, StringComparer.Ordinal);
        var result = new List<InvestigationStep>();
        foreach (var s in steps)
        {
            var keep = s.DependsOn.Where(d => !Reaches(byId, d, s.Id, [])).ToList();
            var fixedStep = s with { DependsOn = keep };
            byId[s.Id] = fixedStep;
            result.Add(fixedStep);
        }

        return result;
    }

    private static bool Reaches(Dictionary<string, InvestigationStep> byId, string from, string target, HashSet<string> seen)
    {
        if (from == target)
        {
            return true;
        }

        if (!seen.Add(from) || !byId.TryGetValue(from, out var step))
        {
            return false;
        }

        return step.DependsOn.Any(d => Reaches(byId, d, target, seen));
    }

    /// <summary>Гипотезы: statement, test {method, metrics, dimension, grain, date_from, date_to, compare_from, compare_to, filters, params}, expect.</summary>
    public static List<PlannedHypothesis> Hypotheses(JsonElement root, int max, string origin, string prefix = "h")
    {
        var list = new List<PlannedHypothesis>();
        foreach (var e in JsonText.Array(root, root.TryGetProperty("hypotheses", out _) ? "hypotheses" : "alternatives"))
        {
            if (list.Count >= max || e.ValueKind != JsonValueKind.Object || JsonText.Str(e, "statement") is not { Length: > 0 } statement)
            {
                continue;
            }

            var id = JsonText.Str(e, "id") is { Length: > 0 } i ? i.Trim() : $"{prefix}{list.Count + 1}";
            ComputeRequest? test = null;
            if (e.TryGetProperty("test", out var t) && t.ValueKind == JsonValueKind.Object && JsonText.Str(t, "method") is { } method
                && ComputeCatalog.Method(method) is not null)
            {
                test = new ComputeRequest
                {
                    Method = method,
                    Data = new ComputeData
                    {
                        Metrics = JsonText.Array(t, "metrics").Select(x => x.ToString()).Where(x => x.Length > 0).ToList(),
                        Dimension = JsonText.Str(t, "dimension"), Grain = JsonText.Str(t, "grain"),
                        From = Date(JsonText.Str(t, "date_from")), To = Date(JsonText.Str(t, "date_to")),
                        CompareFrom = Date(JsonText.Str(t, "compare_from")), CompareTo = Date(JsonText.Str(t, "compare_to")),
                        Filters = t.TryGetProperty("filters", out var f) ? AnalyticsQueryTool.Filters(f) : null,
                        Segment = JsonText.Str(t, "segment"),
                    },
                    Params = t.TryGetProperty("params", out var p) && p.ValueKind == JsonValueKind.Object
                        ? p.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone())
                        : null,
                };
            }

            var expect = e.TryGetProperty("expect", out var x) && x.ValueKind == JsonValueKind.Object
                ? new HypothesisExpectation(JsonText.Str(x, "direction"), JsonText.Str(x, "member"), JsonText.Str(x, "effect"),
                    x.TryGetProperty("min_share", out var ms) && ms.ValueKind == JsonValueKind.Number ? ms.GetDouble() : null)
                : new HypothesisExpectation();
            list.Add(new PlannedHypothesis(id, statement.Trim(), test, expect, origin));
        }

        return list;
    }

    private static DateOnly? Date(string? s) => s is { Length: > 0 } && DateOnly.TryParse(s, CultureInfo.InvariantCulture, out var d) ? d : null;
}
