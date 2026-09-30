using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.AI.Consultant;
using OneBase.Application.Abstractions;
using OneBase.Domain.AI;

namespace OneBase.AI.Memory;

/// <summary>Запись памяти для подсказки модели: когда, какой сотрудник, что анализировал и что выяснил.</summary>
public sealed record MemoryNote(DateTimeOffset At, string AgentCode, string Topic, string Content);

/// <summary>
/// Память AI: аналитическая (результаты сотрудников в чате) и память агента (его недавние выводы для пользователя).
/// Вся память — пользователя; чаты, убранные из истории, из памяти не читаются.
/// </summary>
public sealed class AiMemoryStore(IAppDbContext db)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Сохраняет результаты сотрудников, у которых есть что вспомнить (не ошибки и не «нет доступа»).</summary>
    public async Task SaveAsync(Guid userId, Guid conversationId, IReadOnlyDictionary<string, string> tasks, string question,
        IReadOnlyList<AgentResult> results, CancellationToken ct)
    {
        foreach (var r in results.Where(r => r.Status is "completed" or "partial" or "no_data"))
        {
            var topic = Trim(tasks.GetValueOrDefault(r.Agent) ?? question, 500);
            var content = Summary(r);
            var data = JsonSerializer.Serialize(r, Json);
            db.AiMemories.Add(new AiMemory { Kind = AiMemoryKind.Analytical, UserId = userId, AgentCode = r.Agent, ConversationId = conversationId, Topic = topic, Content = content, Data = data });
            if (r.Status != "no_data")
            {
                db.AiMemories.Add(new AiMemory { Kind = AiMemoryKind.Agent, UserId = userId, AgentCode = r.Agent, ConversationId = conversationId, Topic = topic, Content = content });
            }
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Что уже проанализировано в этом чате — сотрудниками из списка доступных.</summary>
    public async Task<IReadOnlyList<MemoryNote>> ConversationAsync(Guid userId, Guid conversationId, IReadOnlyCollection<string> agents, int limit, CancellationToken ct)
    {
        var rows = await db.AiMemories.AsNoTracking()
            .Where(m => m.UserId == userId && m.ConversationId == conversationId && m.Kind == AiMemoryKind.Analytical && agents.Contains(m.AgentCode!))
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .Select(m => new MemoryNote(m.CreatedAt, m.AgentCode!, m.Topic, m.Content))
            .ToListAsync(ct);
        rows.Reverse();
        return rows;
    }

    /// <summary>Недавние выводы сотрудника для пользователя в других (не убранных) чатах.</summary>
    public async Task<IReadOnlyList<MemoryNote>> AgentAsync(Guid userId, string agentCode, Guid? exceptConversation, int limit, CancellationToken ct) =>
        await db.AiMemories.AsNoTracking()
            .Where(m => m.UserId == userId && m.AgentCode == agentCode && m.Kind == AiMemoryKind.Agent && m.ConversationId != exceptConversation)
            .Where(m => db.AiConversations.Any(c => c.Id == m.ConversationId && c.ArchivedAt == null))
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .Select(m => new MemoryNote(m.CreatedAt, m.AgentCode!, m.Topic, m.Content))
            .ToListAsync(ct);

    public static string Format(IReadOnlyList<MemoryNote> notes, IReadOnlyDictionary<string, string>? names = null)
    {
        var sb = new StringBuilder();
        foreach (var n in notes)
        {
            var who = names?.GetValueOrDefault(n.AgentCode) ?? n.AgentCode;
            sb.AppendLine($"- {n.At.ToOffset(TimeSpan.FromHours(5)):dd.MM.yyyy HH:mm}, {who}. Задача: {n.Topic}. Вывод: {n.Content}");
        }

        return sb.ToString();
    }

    private static string Summary(AgentResult r)
    {
        var sb = new StringBuilder(r.Summary.Trim());
        foreach (var f in r.Findings.Take(4))
        {
            sb.Append(" • ").Append(f.Trim());
        }

        return Trim(sb.ToString(), 1500);
    }

    private static string Trim(string text, int max) => text.Length <= max ? text : text[..max] + "…";
}
