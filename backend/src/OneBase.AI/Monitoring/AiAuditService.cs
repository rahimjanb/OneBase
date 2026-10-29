using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.AI.Consultant;
using OneBase.Application.Abstractions;
using OneBase.Domain.AI;

namespace OneBase.AI.Monitoring;

/// <summary>AI-журнал: запись вопроса консультанту или задачи AI-сотруднику с тем, как получен ответ и сколько он стоил.</summary>
public sealed class AiAuditService(IAppDbContext db)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public async Task WriteAsync(
        string kind,
        Guid userId,
        Guid? conversationId,
        Guid? messageId,
        string question,
        DateTimeOffset startedAt,
        long durationMs,
        ConsultantDetails? details,
        string? response,
        string? error,
        CancellationToken ct,
        bool fromCache = false,
        AiTimingsView? timings = null,
        AiTurnTimings? turn = null)
    {
        // Токены и стоимость — по записям учёта вызовов этого ответа (их пишет AI Gateway): по общему идентификатору ответа, без него (записи
        // до TurnId) — по пользователю, чату и времени. Вход в журнале — весь промпт, включая кэш.
        var query = db.AiUsage.AsNoTracking();
        query = turn is not null
            ? query.Where(u => u.TurnId == turn.Id)
            : query.Where(u => u.UserId == userId && u.ConversationId == conversationId && u.CreatedAt >= startedAt);
        var calls = await query
            .Select(u => new UsageRow(u.Purpose, u.Provider + "/" + u.Model, u.InputTokens, u.CacheReadTokens, u.CacheWriteTokens, u.OutputTokens, u.CostUsd,
                u.DurationMs, u.Success, u.UsedFallback))
            .ToListAsync(ct);
        var usage = calls.Select(u => new { InputTokens = u.Input + u.CacheRead + u.CacheWrite, OutputTokens = u.Output, u.CostUsd }).ToList();

        var agents = details?.Agents.Select(a => a.Agent).ToList() ?? [];
        var tools = details?.Agents.SelectMany(a => a.ToolsUsed).Distinct().ToList() ?? [];
        db.AiAuditLogs.Add(new AiAuditLog
        {
            UserId = userId,
            ConversationId = conversationId,
            MessageId = messageId,
            Kind = kind,
            Question = Trim(question, 4000)!,
            Agents = agents.Count == 0 ? null : Trim(string.Join(",", agents), 500),
            Model = details?.Model,
            Tools = tools.Count == 0 ? null : Trim(string.Join(",", tools), 1000),
            Sources = details is { Sources.Count: > 0 } ? JsonSerializer.Serialize(details.Sources, Json) : null,
            InputTokens = usage.Sum(u => u.InputTokens),
            OutputTokens = usage.Sum(u => u.OutputTokens),
            CostUsd = usage.Count == 0 ? 0 : usage.Any(u => u.CostUsd is null) ? null : usage.Sum(u => u.CostUsd),
            DurationMs = durationMs,
            FromCache = fromCache,
            Status = error is null ? "completed" : "failed",
            ResponsePreview = Trim(response, 1000),
            Error = Trim(error, 1000),
            // Audit Log проверки: числа ответа против данных (сколько, какие не подтверждены, исправлялся ли ответ) и итог перепроверки, если
            // вопрос оспаривал прошлый ответ: аргумент пользователя — Question, исходный ответ — сообщение CorrectionOfMessageId.
            Grounding = details?.Grounding is null && details?.Correction is null
                ? null
                : JsonSerializer.Serialize(new { check = details?.Grounding, correction = details?.Correction }, Json),
            CorrectionVerdict = details?.Correction?.Verdict,
            CorrectionOfMessageId = details?.Correction?.PreviousMessageId,
            Timings = timings is null ? null : JsonSerializer.Serialize(timings, Json),
            TurnId = turn?.Id,
            Routing = turn is null ? null : JsonSerializer.Serialize(RoutingSummary(turn, timings, details, calls), Json),
        });
        await db.SaveChangesAsync(ct);
    }

    private sealed record UsageRow(string Purpose, string Model, int Input, int CacheRead, int CacheWrite, int Output, decimal? CostUsd, long DurationMs,
        bool Success, bool UsedFallback);

    /// <summary>
    /// Итог ответа по вызовам моделей (ai.AuditLogs.Routing): маршрутизация (режим, сложность, модели и причины), перепроверки, вызовы — всего,
    /// по моделям и назначениям, токены и стоимость (расчётная по ценам моделей; неполная, если у части вызовов нет цены — тогда сумма без них,
    /// а не ноль), время этапов и первого текста, полнота (статусы сотрудников, неподтверждённые числа) и проверка чисел.
    /// </summary>
    private static object RoutingSummary(AiTurnTimings turn, AiTimingsView? timings, ConsultantDetails? details, IReadOnlyList<UsageRow> calls)
    {
        object Group(IEnumerable<UsageRow> rows) => new
        {
            Calls = rows.Count(),
            Failed = rows.Count(r => !r.Success),
            InputTokens = rows.Sum(r => (long)r.Input),
            CacheReadTokens = rows.Sum(r => (long)r.CacheRead),
            CacheWriteTokens = rows.Sum(r => (long)r.CacheWrite),
            OutputTokens = rows.Sum(r => (long)r.Output),
            CostUsd = rows.Sum(r => r.CostUsd ?? 0),
            CostIncomplete = rows.Any(r => r.CostUsd is null && (r.Input + r.CacheRead + r.CacheWrite + r.Output) > 0),
            DurationMs = rows.Sum(r => r.DurationMs),
        };

        long Stage(string name) => timings?.Stages.Where(s => s.Name == name).Sum(s => s.Ms) ?? 0;
        var grounding = details?.Grounding;
        var agents = details?.Agents ?? [];
        return new
        {
            turn.Routing?.Mode,
            Complexity = turn.Routing?.Complexity,
            turn.Routing?.AgentModel,
            turn.Routing?.AgentReason,
            turn.Routing?.FinalModel,
            turn.Routing?.FinalReason,
            Planned = turn.Routing is { } r && (r.PlannedAgentModel is not null || r.PlannedFinalModel is not null)
                ? new { Agent = r.PlannedAgentModel, Final = r.PlannedFinalModel, FinalReason = r.PlannedFinalReason }
                : null,
            Reviews = turn.Reviews,
            Total = Group(calls),
            CostEstimated = true,
            ByModel = calls.GroupBy(c => c.Model).ToDictionary(g => g.Key, g => Group(g)),
            ByPurpose = calls.GroupBy(c => c.Purpose).ToDictionary(g => g.Key, g => Group(g)),
            Fallbacks = calls.Count(c => c.UsedFallback),
            StagesMs = new
            {
                Routing = Stage("routing"),
                Agents = Stage("agents"),
                Compose = Stage("compose"),
                Review = calls.Where(c => c.Purpose is "consultant.revise" or "consultant.review").Sum(c => c.DurationMs),
            },
            timings?.TotalMs,
            timings?.FirstTextMs,
            Revisions = grounding?.Revisions ?? 0,
            grounding?.EscalatedTo,
            NumbersChecked = grounding?.Checked ?? 0,
            NumbersPassed = grounding is null || grounding.Unsupported.Count == 0,
            Completeness = new
            {
                Agents = agents.Count,
                Completed = agents.Count(a => a.Status == "completed"),
                Partial = agents.Count(a => a.Status == "partial"),
                NoData = agents.Count(a => a.Status == "no_data"),
                Failed = agents.Count(a => a.Status is not ("completed" or "partial" or "no_data")),
                UnverifiedNumbers = agents.Sum(a => a.Unverified?.Count ?? 0),
            },
        };
    }

    private static string? Trim(string? text, int max) => text is null ? null : text.Length <= max ? text : text[..max];
}
