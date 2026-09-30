using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneBase.AI.Proactive;
using OneBase.AI.Security;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Security;
using OneBase.Domain.AI;
using OneBase.Domain.Audit;

namespace OneBase.Api.Controllers;

/// <summary>AI Alerts и AI Dashboard: находки проактивного анализа, видимые пользователю по его правам.</summary>
[ApiController]
[Route("api/ai/alerts")]
public sealed class AiAlertsController(IAppDbContext db, IUserPermissions permissions, ProactiveAnalyzer analyzer, ProactiveStatus status, IAuditLogger audit)
    : ControllerBase
{
    private static readonly string[] Categories = ["finance", "sales", "marketing", "hr", "production", "supply"];

    /// <summary>active=true — действующие находки; false — закрытые за последние 30 дней.</summary>
    [HttpGet]
    [HasPermission(Permissions.AgentsRun)]
    public async Task<IActionResult> List([FromQuery] bool active = true, CancellationToken ct = default)
    {
        var own = await permissions.GetAsync(User.GetUserId(), ct);
        var since = DateTimeOffset.UtcNow.AddDays(-30);
        var rows = await db.AiAlerts.AsNoTracking()
            .Where(a => active ? a.ResolvedAt == null : a.ResolvedAt != null && a.ResolvedAt > since)
            .ToListAsync(ct);
        // Severity хранится строкой — порядок «критичные → предупреждения → возможности» задаём в памяти.
        var visible = rows
            .Where(a => a.RequiredPermission is null || own.Contains(a.RequiredPermission))
            .OrderBy(a => a.Severity).ThenByDescending(a => a.LastSeenAt)
            .ToList();
        var current = active ? visible : await VisibleActiveAsync(own, ct);
        var problems = current.Where(a => a.Severity != AiAlertSeverity.Opportunity).ToList();

        return Ok(new
        {
            Summary = new
            {
                Problems = problems.Count,
                Critical = problems.Count(a => a.Severity == AiAlertSeverity.Critical),
                Recommendations = problems.Count(a => a.Recommendation is not null),
                Opportunities = current.Count(a => a.Severity == AiAlertSeverity.Opportunity),
                ByCategory = Categories.Select(c => new
                {
                    Category = c,
                    Problems = problems.Count(a => a.Category == c),
                    Critical = problems.Count(a => a.Category == c && a.Severity == AiAlertSeverity.Critical),
                    Opportunities = current.Count(a => a.Category == c && a.Severity == AiAlertSeverity.Opportunity),
                }),
            },
            LastRun = status.Last is { } last ? new { last.At, last.Active, last.Created, last.Resolved, last.Errors } : null,
            Alerts = visible.Select(a => new
            {
                a.Id,
                a.Category,
                a.Rule,
                Severity = a.Severity.ToString(),
                a.Title,
                a.Message,
                a.Recommendation,
                a.Href,
                a.DetectedAt,
                a.LastSeenAt,
                a.ResolvedAt,
            }),
        });
    }

    /// <summary>Проверить данные сейчас (не дожидаясь расписания).</summary>
    [HttpPost("run")]
    [HasPermission(Permissions.AiSettingsManage)]
    public async Task<IActionResult> Run(CancellationToken ct)
    {
        var result = await analyzer.RunAsync(ct);
        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "ai.alerts.run", "ai_alerts", "all",
            new { result.Active, result.Created, result.Resolved, errors = result.Errors.Count }, ct);
        return Ok(result);
    }

    private async Task<List<AiAlert>> VisibleActiveAsync(IReadOnlySet<string> own, CancellationToken ct) =>
        (await db.AiAlerts.AsNoTracking().Where(a => a.ResolvedAt == null).ToListAsync(ct))
        .Where(a => a.RequiredPermission is null || own.Contains(a.RequiredPermission))
        .ToList();
}
