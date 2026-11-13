using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneBase.Api.Auth;
using OneBase.Application.Analytik;
using OneBase.Application.Analytik.Decisions;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Security;

namespace OneBase.Api.Controllers;

/// <summary>Исследование в списке: вопрос, статус, глубина, шаги, гипотезы (подтверждено / отклонено), проверки, стоимость, время.</summary>
public sealed record InvestigationRow(Guid Id, string Question, string? Goal, string Status, int Complexity, int Depth, int StepCount, int Hypotheses, int Confirmed,
    int Rejected, int ReviewRejections, int Replans, int LlmCalls, int ToolCalls, decimal? CostUsd, long DurationMs, DateTimeOffset StartedAt, Guid? ConversationId,
    string? Author);

/// <summary>Исследование целиком: план, шаги с промежуточными результатами, гипотезы, противоречия, проверки, трасса, ограничения, метрики.</summary>
public sealed record InvestigationDetail(InvestigationRow Row, JsonElement? Roles, JsonElement? Plan, JsonElement? Steps, JsonElement? Hypotheses,
    JsonElement? Contradictions, JsonElement? Reviews, JsonElement? Trace, JsonElement? Limitations, string? Summary, long InputTokens, long OutputTokens,
    string? FailureReason);

/// <summary>Событие истории находки.</summary>
public sealed record InsightEventView(DateTimeOffset At, string Event, string? Severity, decimal? Impact, string? Note);

/// <summary>История и авторасследование находки: события, уверенность, класс вывода, расследование.</summary>
public sealed record InsightHistory(Guid Id, decimal? Confidence, string? EvidenceClass, JsonElement? Investigation, DateTimeOffset? InvestigatedAt,
    IReadOnlyList<InsightEventView> Events);

public sealed record CancelDecision(string? Note);

/// <summary>
/// Advanced Intelligence: исследования (ход и трасса), память решений, история находок. Права — analytik.use; исследование видит его автор и
/// управляющий Аналитиком; решения — автор и управляющий (DecisionService); данные решений — по правам автора.
/// </summary>
[ApiController]
[Authorize]
[HasPermission(Permissions.AnalytikUse)]
[AnalytikErrors]
[Route("api/analytik")]
public sealed class AnalytikIntelligenceController(AnalytikAccess access, IAnalytikDb adb, OneBase.Application.Abstractions.IAppDbContext db, DecisionService decisions,
    OneBase.AI.Analytik.Intelligence.QualityService quality, OneBase.AI.Analytik.Intelligence.BenchmarkRunner benchmark) : ControllerBase
{
    private Task<AnalytikPrincipal> PrincipalAsync(CancellationToken ct) => access.ResolveAsync(User.GetUserId(), ct);

    // ---------- исследования ----------

    [HttpGet("investigations")]
    public async Task<IReadOnlyList<InvestigationRow>> Investigations([FromQuery] bool all, [FromQuery] int take, CancellationToken ct)
    {
        var p = await PrincipalAsync(ct);
        var q = adb.Investigations.AsNoTracking();
        if (!(all && p.Manager))
        {
            q = q.Where(i => i.UserId == p.UserId);
        }

        var rows = await q.OrderByDescending(i => i.StartedAt).Take(Math.Clamp(take == 0 ? 50 : take, 1, 200)).ToListAsync(ct);
        var names = await Names(rows.Select(r => r.UserId), ct);
        return rows.Select(r => Row(r, names)).ToList();
    }

    [HttpGet("investigations/{id:guid}")]
    public async Task<InvestigationDetail> Investigation(Guid id, CancellationToken ct)
    {
        var p = await PrincipalAsync(ct);
        var r = await adb.Investigations.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);
        if (r is null || (r.UserId != p.UserId && !p.Manager))
        {
            throw new KeyNotFoundException("Исследование не найдено.");
        }

        var names = await Names([r.UserId], ct);
        return new InvestigationDetail(Row(r, names), Json(r.Roles), Json(r.Plan), Json(r.Steps), Json(r.Hypotheses), Json(r.Contradictions), Json(r.Reviews),
            Json(r.Trace), Json(r.Limitations), r.Summary, r.InputTokens, r.OutputTokens, r.FailureReason);
    }

    private static InvestigationRow Row(OneBase.Domain.Analytik.AnalytikInvestigation r, IReadOnlyDictionary<Guid, string> names)
    {
        var (total, confirmed, rejected) = (0, 0, 0);
        if (Json(r.Hypotheses) is { ValueKind: JsonValueKind.Array } h)
        {
            foreach (var x in h.EnumerateArray())
            {
                total++;
                var status = x.TryGetProperty("status", out var s) ? s.GetString() : null;
                confirmed += status is "confirmed" or "partially_confirmed" ? 1 : 0;
                rejected += status == "rejected" ? 1 : 0;
            }
        }

        return new InvestigationRow(r.Id, r.Question, r.Goal, r.Status, r.Complexity, r.Depth, r.StepCount, total, confirmed, rejected, r.ReviewRejections, r.Replans,
            r.LlmCalls, r.ToolCalls, r.CostUsd, r.DurationMs, r.StartedAt, r.ConversationId, r.UserId is { } u ? names.GetValueOrDefault(u) : null);
    }

    // ---------- качество ----------

    /// <summary>Качество аналитика за период (дней): ответы, исследования, гипотезы, ошибки срезов, расчётов и инструментов, слабые места — управляющему.</summary>
    [HttpGet("quality")]
    [HasPermission(Permissions.AnalytikManage)]
    public async Task<OneBase.AI.Analytik.Intelligence.QualityReport> Quality([FromQuery] int days, CancellationToken ct) =>
        await quality.ReportAsync(days == 0 ? 30 : days, ct);

    // ---------- набор сложных вопросов ----------

    /// <summary>Каталог сценариев (категория, вопрос) и последние прогоны.</summary>
    [HttpGet("benchmark")]
    [HasPermission(Permissions.AnalytikManage)]
    public async Task<object> Benchmark(CancellationToken ct) => new
    {
        Categories = OneBase.AI.Analytik.Intelligence.BenchmarkCatalog.Categories,
        Cases = OneBase.AI.Analytik.Intelligence.BenchmarkCatalog.Cases.Select(c => new { c.Id, c.Category, c.Question, Expected = c.Check.Status }),
        Runs = await adb.BenchmarkRuns.AsNoTracking().OrderByDescending(r => r.StartedAt).Take(20)
            .Select(r => new { r.Id, r.Mode, r.Status, r.Total, r.Passed, r.Score, r.CostUsd, r.StartedAt, r.FinishedAt, r.Error }).ToListAsync(ct),
    };

    [HttpGet("benchmark/runs/{id:guid}")]
    [HasPermission(Permissions.AnalytikManage)]
    public async Task<object> BenchmarkRun(Guid id, CancellationToken ct)
    {
        var r = await adb.BenchmarkRuns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Прогон не найден.");
        return new { r.Id, r.Mode, r.Status, r.Total, r.Passed, r.Score, r.CostUsd, r.StartedAt, r.FinishedAt, r.Error, ByCategory = Json(r.ByCategory), Results = Json(r.Results) };
    }

    /// <summary>Регрессия набора на текущих данных — без вызовов модели.</summary>
    [HttpPost("benchmark/regression")]
    [HasPermission(Permissions.AnalytikManage)]
    public async Task<object> BenchmarkRegression(CancellationToken ct)
    {
        var run = await benchmark.RegressionAsync(await PrincipalAsync(ct), null, ct);
        return new { run.Id, run.Status, run.Total, run.Passed, run.Score, run.Error };
    }

    public sealed record LiveRequest(bool Confirm, IReadOnlyList<string>? Cases, decimal? MaxCostUsd);

    /// <summary>Живой прогон вопросов моделью — платный: только с подтверждением; бюджет прогона по умолчанию $2.</summary>
    [HttpPost("benchmark/live")]
    [HasPermission(Permissions.AnalytikManage)]
    public async Task<object> BenchmarkLive([FromBody] LiveRequest body, CancellationToken ct)
    {
        var run = await benchmark.LiveAsync(await PrincipalAsync(ct), body.Cases, Math.Clamp(body.MaxCostUsd ?? 2m, 0, 50), body.Confirm, ct);
        return new { run.Id, run.Status, run.Total, run.Passed, run.Score, run.CostUsd, run.Error };
    }

    // ---------- решения ----------

    [HttpGet("decisions")]
    public async Task<IReadOnlyList<DecisionView>> Decisions([FromQuery] string? status, [FromQuery] string? q, CancellationToken ct) =>
        await decisions.ListAsync(await PrincipalAsync(ct), status, q, ct);

    [HttpGet("decisions/{id:guid}")]
    public async Task<DecisionView> Decision(Guid id, CancellationToken ct) => await decisions.GetAsync(await PrincipalAsync(ct), id, ct);

    [HttpPost("decisions")]
    public async Task<DecisionView> CreateDecision([FromBody] DecisionInput input, CancellationToken ct) => await decisions.CreateAsync(await PrincipalAsync(ct), input, ct);

    [HttpPost("decisions/{id:guid}/evaluate")]
    public async Task<DecisionView> EvaluateDecision(Guid id, CancellationToken ct) => await decisions.EvaluateAsync(await PrincipalAsync(ct), id, ct);

    [HttpPost("decisions/{id:guid}/cancel")]
    public async Task<DecisionView> CancelDecision(Guid id, [FromBody] CancelDecision body, CancellationToken ct) =>
        await decisions.CancelAsync(await PrincipalAsync(ct), id, body.Note, ct);

    // ---------- история находки ----------

    /// <summary>
    /// История и авторасследование находки — тем, кто видит находку: её право, а находки продаж и финансов — только при доступе ко всей
    /// организации (как на экране «Риски»).
    /// </summary>
    [HttpGet("insights/{id:guid}/history")]
    public async Task<InsightHistory> History(Guid id, CancellationToken ct)
    {
        var p = await PrincipalAsync(ct);
        var i = await adb.Insights.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        var allowed = i is not null
                      && (i.RequiredPermission is not { Length: > 0 } perm || perm.Split('|', StringSplitOptions.RemoveEmptyEntries).Any(p.Has))
                      && (i.Module is not ("sales" or "finance") || !p.Sales.Restricted || p.System);
        if (!allowed)
        {
            throw new KeyNotFoundException("Находка не найдена.");
        }

        var events = await adb.InsightEvents.AsNoTracking().Where(e => e.InsightId == id).OrderBy(e => e.At).ThenBy(e => e.CreatedAt)
            .Select(e => new InsightEventView(e.At, e.Event, e.Severity, e.Impact, e.Note)).ToListAsync(ct);
        return new InsightHistory(i!.Id, i.Confidence, i.EvidenceClass, Json(i.Investigation), i.InvestigatedAt, events);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> Names(IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var list = ids.OfType<Guid>().Distinct().ToList();
        return await db.Users.AsNoTracking().Where(u => list.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    private static JsonElement? Json(string? json) => json is null ? null : JsonDocument.Parse(json).RootElement.Clone();
}
