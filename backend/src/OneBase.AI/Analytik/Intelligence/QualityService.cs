using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Analytik;
using OneBase.Domain.AI;

namespace OneBase.AI.Analytik.Intelligence;

public sealed record CountItem(string Key, int Count);

/// <summary>Ответы чата Аналитика: всего, сбоев, с неподтверждёнными числами, доля успешных и неполных, среднее время и стоимость, ушло в исследование.</summary>
public sealed record ChatQuality(int Answers, int Failed, int WithUnverified, double SuccessRate, double IncompleteRate, double AvgDurationMs, decimal? AvgCostUsd,
    int Investigated);

/// <summary>Исследования: статусы, средние шаги, глубина, время, стоимость, вызовы модели и инструментов, доля отклонений проверки и перепланирований.</summary>
public sealed record InvestigationQuality(int Total, int Completed, int Partial, int Failed, double AvgSteps, double AvgDepth, double AvgDurationMs,
    decimal? AvgCostUsd, double AvgLlmCalls, double AvgToolCalls, double ReviewRejectionRate, double ReplanRate, IReadOnlyList<CountItem> FailureReasons);

public sealed record HypothesisQuality(int Total, IReadOnlyList<CountItem> ByStatus, IReadOnlyList<CountItem> ByClass);

/// <summary>Запросы движков (срезы и планы): всего, ошибок, доля, частые ошибки, по статусам.</summary>
public sealed record QueryQuality(int Total, int Errors, double ErrorRate, IReadOnlyList<CountItem> TopErrors, IReadOnlyList<CountItem> ByStatus);

public sealed record ComputeQuality(int Total, IReadOnlyList<CountItem> ByStatus, IReadOnlyList<CountItem> TopErrors, IReadOnlyList<CountItem> ByMethod);

public sealed record BenchmarkSummary(Guid Id, string Mode, string Status, int Total, int Passed, decimal Score, DateTimeOffset StartedAt, string? Model,
    decimal? CostUsd, JsonElement? ByCategory);

/// <summary>Слабое место аналитика: область, что повторяется, сколько раз, важность.</summary>
public sealed record WeakSpot(string Area, string Text, int Count, string Severity);

/// <summary>Сводка качества аналитики за период.</summary>
public sealed record QualityReport(DateOnly From, DateOnly To, ChatQuality Chat, InvestigationQuality Investigations, HypothesisQuality Hypotheses,
    QueryQuality Queries, ComputeQuality Computations, IReadOnlyList<CountItem> ToolErrors, IReadOnlyList<CountItem> ReviewIssues,
    IReadOnlyList<BenchmarkSummary> Benchmarks, IReadOnlyList<WeakSpot> WeakSpots);

/// <summary>
/// Analytics Quality Monitoring: доля решённых запросов, неполные данные, ошибки планировщика срезов, расчётов и инструментов, средние шаги,
/// время и стоимость исследований, частота исправлений проверки, повторяющиеся причины отказов — по журналам, которые уже ведутся (ai.AuditLogs,
/// analytik.Queries, analytik.Computations, analytik.Investigations, аудит инструментов, прогоны набора сложных вопросов). Слабые места —
/// повторяющиеся причины, отсортированные по частоте.
/// </summary>
public sealed partial class QualityService(IAnalytikDb adb, IAppDbContext db, TimeProvider clock)
{
    private const int MaxRows = 5000;

    public async Task<QualityReport> ReportAsync(int days, CancellationToken ct)
    {
        days = Math.Clamp(days, 1, 365);
        var now = clock.GetUtcNow();
        var since = now.AddDays(-days);
        var to = DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(5)).DateTime);

        var chats = await db.AiAuditLogs.AsNoTracking().Where(a => a.CreatedAt >= since && a.Kind == AnalytikAiRegistration.ChatKey)
            .Select(a => new { a.Status, a.Grounding, a.DurationMs, a.CostUsd, a.TurnId }).Take(MaxRows).ToListAsync(ct);
        var investigations = await adb.Investigations.AsNoTracking().Where(i => i.StartedAt >= since).OrderByDescending(i => i.StartedAt).Take(MaxRows).ToListAsync(ct);
        var turns = investigations.Where(i => i.TurnId is not null).Select(i => i.TurnId!.Value).ToHashSet();

        var withUnverified = chats.Count(c => Unsupported(c.Grounding) > 0);
        var failed = chats.Count(c => c.Status != "completed");
        // Неполный ответ: остались неподтверждённые числа или его исследование закончилось без всех данных (partial, failed).
        var partialTurns = investigations.Where(i => i.TurnId is not null && i.Status != Statuses.Completed).Select(i => i.TurnId!.Value).ToHashSet();
        var incomplete = chats.Count(c => Unsupported(c.Grounding) > 0 || (c.TurnId is { } t && partialTurns.Contains(t)));
        var chat = new ChatQuality(chats.Count, failed, withUnverified, Rate(chats.Count - failed - withUnverified, chats.Count),
            Rate(incomplete, chats.Count), Avg(chats.Select(c => (double)c.DurationMs)),
            AvgMoney(chats.Select(c => c.CostUsd)), chats.Count(c => c.TurnId is { } t && turns.Contains(t)));

        var invQuality = new InvestigationQuality(investigations.Count, investigations.Count(i => i.Status == Statuses.Completed),
            investigations.Count(i => i.Status == Statuses.Partial), investigations.Count(i => i.Status == Statuses.Failed), Avg(investigations.Select(i => (double)i.StepCount)),
            Avg(investigations.Select(i => (double)i.Depth)), Avg(investigations.Select(i => (double)i.DurationMs)), AvgMoney(investigations.Select(i => i.CostUsd)),
            Avg(investigations.Select(i => (double)i.LlmCalls)), Avg(investigations.Select(i => (double)i.ToolCalls)),
            Rate(investigations.Count(i => i.ReviewRejections > 0), investigations.Count), Rate(investigations.Count(i => i.Replans > 0), investigations.Count),
            Top(investigations.Where(i => i.FailureReason is not null).Select(i => i.FailureReason!)));

        var statuses = new List<string>();
        var classes = new List<string>();
        var reviewIssues = new List<string>();
        foreach (var i in investigations)
        {
            foreach (var h in Array(i.Hypotheses))
            {
                statuses.Add(Str(h, "status") ?? "inconclusive");
                classes.Add(Str(h, "evidenceClass") ?? "—");
            }

            if (Parse(i.Reviews) is { ValueKind: JsonValueKind.Object } r && r.TryGetProperty("cycles", out var cycles) && cycles.ValueKind == JsonValueKind.Array)
            {
                foreach (var issue in cycles.EnumerateArray().SelectMany(c => c.TryGetProperty("issues", out var x) && x.ValueKind == JsonValueKind.Array ? x.EnumerateArray() : []))
                {
                    reviewIssues.Add(Str(issue, "kind") ?? "other");
                }
            }
        }

        // Прогоны набора сложных вопросов не считаются: их провокационные сценарии ошибаются нарочно.
        var queries = await adb.Queries.AsNoTracking().Where(q => q.CreatedAt >= since && !q.Origin.StartsWith(BenchmarkRunner.Origin))
            .Select(q => new { q.Status, q.Error }).Take(MaxRows * 4).ToListAsync(ct);
        var queryErrors = queries.Where(q => q.Status != "ok").ToList();
        var queryQuality = new QueryQuality(queries.Count, queryErrors.Count, Rate(queryErrors.Count, queries.Count),
            Top(queryErrors.Select(q => Shape(q.Error ?? q.Status)), 10), Top(queries.Select(q => q.Status)));

        var computations = await adb.Computations.AsNoTracking().Where(c => c.CreatedAt >= since && c.Origin != "benchmark")
            .Select(c => new { c.Status, c.Error, c.Method }).Take(MaxRows * 2).ToListAsync(ct);
        var computeQuality = new ComputeQuality(computations.Count, Top(computations.Select(c => c.Status)),
            Top(computations.Where(c => c.Status != "ok").Select(c => Shape(c.Error ?? c.Status)), 10), Top(computations.Select(c => c.Method), 15));

        var toolErrors = Top(await db.AuditLogs.AsNoTracking().Where(a => a.Timestamp >= since && a.Action == "ai.tool.failed" && a.EntityId != null)
            .Select(a => a.EntityId!).Take(MaxRows).ToListAsync(ct), 10);

        var benchmarks = (await adb.BenchmarkRuns.AsNoTracking().OrderByDescending(b => b.StartedAt).Take(10).ToListAsync(ct))
            .Select(b => new BenchmarkSummary(b.Id, b.Mode, b.Status, b.Total, b.Passed, b.Score, b.StartedAt, b.Model, b.CostUsd, Parse(b.ByCategory))).ToList();

        var review = Top(reviewIssues);
        return new QualityReport(DateOnly.FromDateTime(since.ToOffset(TimeSpan.FromHours(5)).DateTime), to, chat, invQuality,
            new HypothesisQuality(statuses.Count, Top(statuses), Top(classes)), queryQuality, computeQuality, toolErrors, review, benchmarks,
            WeakSpots(queryQuality, computeQuality, toolErrors, review, invQuality, chat, benchmarks));
    }

    /// <summary>Слабые места: повторяющиеся причины (от двух раз) по областям; важность — по доле и частоте.</summary>
    internal static IReadOnlyList<WeakSpot> WeakSpots(QueryQuality q, ComputeQuality c, IReadOnlyList<CountItem> tools, IReadOnlyList<CountItem> review,
        InvestigationQuality inv, ChatQuality chat, IReadOnlyList<BenchmarkSummary> benchmarks)
    {
        var spots = new List<WeakSpot>();
        spots.AddRange(q.TopErrors.Where(e => e.Count >= 2).Select(e => new WeakSpot("Планировщик срезов", e.Key, e.Count, q.ErrorRate > 0.1 ? "critical" : "warning")));
        spots.AddRange(c.TopErrors.Where(e => e.Count >= 2).Select(e => new WeakSpot("Расчёты", e.Key, e.Count, "warning")));
        spots.AddRange(tools.Where(e => e.Count >= 2).Select(e => new WeakSpot("Инструменты", $"«{e.Key}» завершается ошибкой", e.Count, e.Count >= 10 ? "critical" : "warning")));
        spots.AddRange(review.Where(e => e.Count >= 2).Select(e => new WeakSpot("Проверка ответов", ReviewText(e.Key), e.Count, "warning")));
        spots.AddRange(inv.FailureReasons.Where(e => e.Count >= 2).Select(e => new WeakSpot("Исследования", $"Исследование не дало данных: {e.Key}", e.Count, "critical")));
        if (chat.Answers >= 10 && chat.WithUnverified > chat.Answers / 10)
        {
            spots.Add(new WeakSpot("Ответы", "В ответах часто остаются числа, не подтверждённые данными", chat.WithUnverified, "critical"));
        }

        if (benchmarks.FirstOrDefault(b => b.Status == "completed") is { ByCategory: { ValueKind: JsonValueKind.Object } cats })
        {
            foreach (var cat in cats.EnumerateObject())
            {
                var passed = cat.Value.TryGetProperty("passed", out var p) ? p.GetInt32() : 0;
                var total = cat.Value.TryGetProperty("total", out var t) ? t.GetInt32() : 0;
                if (total > 0 && passed < total)
                {
                    spots.Add(new WeakSpot("Набор сложных вопросов", $"Категория «{cat.Name}»: решено {passed} из {total}", total - passed, passed * 2 < total ? "critical" : "warning"));
                }
            }
        }

        return spots.OrderByDescending(s => s.Severity == "critical").ThenByDescending(s => s.Count).Take(20).ToList();
    }

    private static string ReviewText(string kind) => kind switch
    {
        "unproven_cause" => "Ответ называет причину без подтверждённой гипотезы",
        "rejected_cause" => "Ответ выдаёт отклонённую гипотезу за причину",
        "limitations" => "Не названы ограничения данных при неполных данных",
        "numbers" => "Числа ответа не подтверждены данными",
        "period" => "Период вывода не совпадает с данными",
        "recalc" => "Пересчёт финансового вывода не совпал",
        _ => $"Замечание проверки: {kind}",
    };

    /// <summary>Текст ошибки без конкретных значений (числа, кавычки) — чтобы одинаковые причины сложились.</summary>
    internal static string Shape(string text)
    {
        var s = Quoted().Replace(text, "«…»");
        s = Digits().Replace(s, "N");
        return s.Length > 160 ? s[..160] + "…" : s;
    }

    private static IReadOnlyList<CountItem> Top(IEnumerable<string> items, int take = 8) =>
        items.GroupBy(x => x).Select(g => new CountItem(g.Key, g.Count())).OrderByDescending(x => x.Count).ThenBy(x => x.Key, StringComparer.Ordinal).Take(take).ToList();

    private static double Rate(int part, int total) => total == 0 ? 0 : Math.Round((double)part / total, 4);

    private static double Avg(IEnumerable<double> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? 0 : Math.Round(list.Average(), 1);
    }

    private static decimal? AvgMoney(IEnumerable<decimal?> values)
    {
        var list = values.OfType<decimal>().ToList();
        return list.Count == 0 ? null : Math.Round(list.Average(), 4);
    }

    private static int Unsupported(string? grounding) =>
        Parse(grounding) is { ValueKind: JsonValueKind.Object } g && g.TryGetProperty("unsupported", out var u) && u.ValueKind == JsonValueKind.Array ? u.GetArrayLength() : 0;

    private static JsonElement? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IEnumerable<JsonElement> Array(string? json) => Parse(json) is { ValueKind: JsonValueKind.Array } a ? a.EnumerateArray() : [];

    private static string? Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static class Statuses
    {
        public const string Completed = InvestigationStatuses.Completed;
        public const string Partial = InvestigationStatuses.Partial;
        public const string Failed = InvestigationStatuses.Failed;
    }

    [GeneratedRegex(@"«[^»]*»|""[^""]*""")]
    private static partial Regex Quoted();

    [GeneratedRegex(@"\d[\d\s.,]*")]
    private static partial Regex Digits();
}
