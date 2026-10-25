using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneBase.AI.Gateway;
using OneBase.AI.Monitoring;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Security;
using OneBase.Domain.Audit;

namespace OneBase.Api.Controllers;

/// <summary>
/// «Настройки → AI → Модели по задачам»: сравнение исходной модели и модели категории простых задач (фон и эталонный набор) и запуск
/// проверки на эталонном наборе. Сами модели и режимы задаются в «Общие».
/// </summary>
[ApiController]
[Route("api/ai/routing")]
[HasPermission(Permissions.AiSettingsManage)]
public sealed class AiRoutingController(IAppDbContext db, IAiSettingsSource settings, RoutingEvaluationRunner runner, IAuditLogger audit) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int days = 30, CancellationToken ct = default)
    {
        days = Math.Clamp(days, 1, 366);
        var from = DateTimeOffset.UtcNow.AddDays(-days);
        var rows = await db.AiModelComparisons.AsNoTracking().Where(c => c.CreatedAt >= from).OrderByDescending(c => c.CreatedAt).ToListAsync(ct);
        var s = await settings.GetSettingsAsync(ct);

        static double Share(int part, int total) => total == 0 ? 0 : Math.Round((double)part / total, 4);

        return Ok(new
        {
            Days = days,
            Settings = new
            {
                s.TaskRoutingEnabled,
                Primary = s.Primary?.ToString(),
                Router = s.Router?.ToString(),
                s.RouterMode,
                Extract = s.Extract?.ToString(),
                s.ExtractMode,
                Route = Describe(AiTaskRouting.For(s, AiTaskCategory.Route)),
                ExtractRoute = Describe(AiTaskRouting.For(s, AiTaskCategory.Extract)),
            },
            Groups = rows
                .GroupBy(r => (r.Category, r.Source, r.BaselineModel, r.CandidateModel))
                .OrderBy(g => g.Key.Category).ThenBy(g => g.Key.Source)
                .Select(g =>
                {
                    var list = g.ToList();
                    var graded = list.Where(r => r.BaselineCorrect is not null).ToList();
                    return new
                    {
                        g.Key.Category,
                        g.Key.Source,
                        g.Key.BaselineModel,
                        g.Key.CandidateModel,
                        Count = list.Count,
                        Agree = Share(list.Count(r => r.Agree), list.Count),
                        Similarity = list.Count == 0 ? 0 : Math.Round(list.Average(r => r.Similarity), 4),
                        BaselineValid = Share(list.Count(r => r.BaselineValid), list.Count),
                        CandidateValid = Share(list.Count(r => r.CandidateValid), list.Count),
                        Graded = graded.Count,
                        BaselineCorrect = Share(graded.Count(r => r.BaselineCorrect == true), graded.Count),
                        CandidateCorrect = Share(graded.Count(r => r.CandidateCorrect == true), graded.Count),
                        CandidateErrors = list.Count(r => r.CandidateError is not null),
                        Baseline = Side(list.Select(r => (r.BaselineInputTokens, r.BaselineCacheReadTokens, r.BaselineCacheWriteTokens, r.BaselineOutputTokens, r.BaselineCostUsd, r.BaselineMs)).ToList()),
                        Candidate = Side(list.Select(r => (r.CandidateInputTokens, r.CandidateCacheReadTokens, r.CandidateCacheWriteTokens, r.CandidateOutputTokens, r.CandidateCostUsd, r.CandidateMs)).ToList()),
                    };
                }),
            // Последние сравнения — сначала расхождения: их и нужно разбирать перед включением модели категории.
            Recent = rows
                .OrderBy(r => r.Agree).ThenByDescending(r => r.CreatedAt)
                .Take(60)
                .Select(r => new
                {
                    r.CreatedAt, r.Category, r.Source, r.CaseCode, r.Task, r.Expected, r.BaselineModel, r.CandidateModel, r.BaselineResult, r.CandidateResult,
                    r.Agree, r.Similarity, r.BaselineValid, r.CandidateValid, r.BaselineCorrect, r.CandidateCorrect, r.BaselineCostUsd, r.CandidateCostUsd,
                    r.BaselineMs, r.CandidateMs, r.CandidateError,
                }),
            Evaluation = runner.Last,
            EvalCases = RouteEvalCases.All.Count,
        });
    }

    /// <summary>Проверка модели маршрутизации на эталонном наборе (в фоне). 409 — уже идёт.</summary>
    [HttpPost("evaluate")]
    public async Task<IActionResult> Evaluate(CancellationToken ct)
    {
        if (!runner.Start(User.GetUserId()))
        {
            return Conflict(new { error = "Проверка уже идёт — дождитесь итога." });
        }

        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "ai.routing.evaluated", "ai_settings", "route", new { cases = RouteEvalCases.All.Count }, ct);
        return Accepted(new { started = true });
    }

    private static object Describe(AiTaskRoute route) => new
    {
        Model = route.Model?.ToString(),
        Fallback = route.Fallback?.ToString(),
        Shadow = route.Shadow?.ToString(),
        Baseline = route.Baseline?.ToString(),
    };

    private static object Side(IReadOnlyList<(int Input, int CacheRead, int CacheWrite, int Output, decimal? Cost, long Ms)> rows) => new
    {
        InputTokens = rows.Sum(r => (long)r.Input),
        CacheReadTokens = rows.Sum(r => (long)r.CacheRead),
        CacheWriteTokens = rows.Sum(r => (long)r.CacheWrite),
        OutputTokens = rows.Sum(r => (long)r.Output),
        CostUsd = rows.Sum(r => r.Cost ?? 0),
        CostIncomplete = rows.Any(r => r.Cost is null),
        AvgMs = rows.Count == 0 ? 0 : (long)rows.Average(r => r.Ms),
    };
}
