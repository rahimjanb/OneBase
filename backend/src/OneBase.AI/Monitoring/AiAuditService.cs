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
        bool fromCache = false)
    {
        // Токены и стоимость — по записям учёта вызовов этого запроса (они пишутся AI Gateway). Вход в журнале — весь промпт, включая кэш.
        var usage = await db.AiUsage.AsNoTracking()
            .Where(u => u.UserId == userId && u.ConversationId == conversationId && u.CreatedAt >= startedAt)
            .Select(u => new { InputTokens = u.InputTokens + u.CacheReadTokens + u.CacheWriteTokens, u.OutputTokens, u.CostUsd })
            .ToListAsync(ct);

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
        });
        await db.SaveChangesAsync(ct);
    }

    private static string? Trim(string? text, int max) => text is null ? null : text.Length <= max ? text : text[..max];
}
