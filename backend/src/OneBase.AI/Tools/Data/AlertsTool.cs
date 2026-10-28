using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.AI.Consultant;
using OneBase.AI.Knowledge;
using OneBase.AI.Security;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Security;
using OneBase.Domain.AI;

namespace OneBase.AI.Tools.Data;

/// <summary>
/// Находки проактивного анализа («Фокус дня» на главной, «AI → Находки»): действующие или закрытые за 30 дней — как GET /api/ai/alerts.
/// Видны только находки, право которых есть у пользователя (находка финансов — с «Финансами»).
/// </summary>
internal sealed class GetAiAlertsTool(IAppDbContext db, IUserPermissions permissions) : DataTool
{
    internal const int DefaultLimit = 20;
    private const int ResolvedDays = 30;

    internal static readonly IReadOnlyDictionary<string, string> CategoryNames = new Dictionary<string, string>
    {
        ["sales"] = "продажи",
        ["finance"] = "финансы",
        ["marketing"] = "маркетинг",
        ["hr"] = "HR",
        ["production"] = "производство",
        ["supply"] = "снабжение",
    };

    internal static readonly IReadOnlyList<ArgChoice<AiAlertSeverity?>> Severities =
    [
        new(AiAlertSeverity.Critical, "critical", "критичные", "критично", "критичная"),
        new(AiAlertSeverity.Warning, "warning", "предупреждения", "внимание", "риски"),
        new(AiAlertSeverity.Opportunity, "opportunity", "возможности", "возможность"),
        new(null, "all", "все", "любые"),
    ];

    public override string Name => "get_ai_alerts";
    public override string Title => "Находки AI («Фокус дня»)";
    public override string Source => KnowledgeSources.AiAlerts;
    public override string? RequiredPermission => Permissions.AgentsRun;

    public override string Description =>
        "Находки проактивного анализа OneBase — «Фокус дня» на главной и «AI → Находки»: что сейчас требует внимания (критичные, предупреждения) " +
        "и возможности по отделам (продажи, финансы, маркетинг, HR, производство, снабжение): заголовок, суть с цифрами, рекомендация, ссылка на страницу, " +
        "когда найдено и подтверждено в последний раз; resolved — закрытые за 30 дней. Только находки, к данным которых у пользователя есть доступ. " +
        "Вопросы: «что сейчас требует внимания», «какие риски», «что важного нашёл AI», «какие есть возможности».";

    public override string UsageHint =>
        "Общие вопросы «что требует внимания / какие риски / что горит» — начни с get_ai_alerts, затем проверь цифры инструментами отдела.";

    public override JsonElement InputSchema { get; } = Schema(
        ("category", new { type = "string", @enum = CategoryNames.Keys.Append("all").ToArray(), description = "Отдел: sales, finance, marketing, hr, production, supply; all или без него — все." }),
        ("severity", new { type = "string", @enum = WorkFieldText.Codes(Severities), description = "critical — критичные, warning — предупреждения, opportunity — возможности, all — все (по умолчанию)." }),
        ("resolved", new { type = "boolean", description = "true — закрытые за последние 30 дней вместо действующих." }),
        ("limit", new { type = "integer", description = $"Сколько находок: по умолчанию {DefaultLimit}, до 50." }));

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        var limit = args.Int("limit", 1, 50) ?? DefaultLimit;
        var severity = WorkFieldText.Pick(args.Str("severity"), "severity", null, Severities);
        var category = args.Str("category")?.ToLowerInvariant() is { } c && c != "all" ? c : null;
        if (category is not null && !CategoryNames.ContainsKey(category))
        {
            throw new ToolArgumentException($"category — {string.Join(", ", CategoryNames.Keys)} или all.");
        }

        var resolved = args.Bool("resolved") == true;
        var userId = context.UserId ?? throw new ToolArgumentException("Находки AI отдаются только от имени пользователя.");
        var own = await permissions.GetAsync(userId, ct);
        var since = DateTimeOffset.UtcNow.AddDays(-ResolvedDays);
        var rows = await db.AiAlerts.AsNoTracking()
            .Where(a => resolved ? a.ResolvedAt != null && a.ResolvedAt > since : a.ResolvedAt == null)
            .ToListAsync(ct);
        var visible = rows
            .Where(a => a.RequiredPermission is null || own.Contains(a.RequiredPermission))
            .Where(a => category is null || a.Category == category)
            .ToList();
        var picked = visible.Where(a => severity is null || a.Severity == severity)
            .OrderBy(a => a.Severity).ThenByDescending(a => a.LastSeenAt)
            .ToList();
        var shown = picked.Take(args.Cap(limit)).ToList();

        return Data(new
        {
            Resolved = resolved,
            Summary = new
            {
                Critical = visible.Count(a => a.Severity == AiAlertSeverity.Critical),
                Warnings = visible.Count(a => a.Severity == AiAlertSeverity.Warning),
                Opportunities = visible.Count(a => a.Severity == AiAlertSeverity.Opportunity),
                ByCategory = visible.GroupBy(a => a.Category).Select(g => new { Category = CategoryNames.GetValueOrDefault(g.Key, g.Key), Count = g.Count() }),
            },
            Total = picked.Count,
            Shown = shown.Count,
            Alerts = shown.Select(a => new
            {
                Category = CategoryNames.GetValueOrDefault(a.Category, a.Category),
                Severity = a.Severity switch { AiAlertSeverity.Critical => "критично", AiAlertSeverity.Warning => "внимание", _ => "возможность" },
                a.Title,
                a.Message,
                a.Recommendation,
                Link = a.Href,
                Detected = a.DetectedAt.ToOffset(FieldClock.Offset).ToString("dd.MM.yyyy HH:mm"),
                LastSeen = a.LastSeenAt.ToOffset(FieldClock.Offset).ToString("dd.MM.yyyy HH:mm"),
                Closed = a.ResolvedAt?.ToOffset(FieldClock.Offset).ToString("dd.MM.yyyy HH:mm"),
            }),
            Note = "Находки пересчитываются по расписанию проактивного анализа; LastSeen — когда находка подтвердилась последний раз. " +
                   "Цифры в находке — на момент проверки: для свежих данных вызови инструмент отдела.",
        }, new DataSource("AI · Находки", null, "/ai"));
    }
}
