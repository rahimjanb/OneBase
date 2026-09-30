using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.AI.Llm;
using OneBase.AI.Providers;
using OneBase.Application.Abstractions;
using OneBase.Domain.AI;

namespace OneBase.AI.Consultant;

/// <summary>Событие чата для потоковой отдачи в браузер.</summary>
public sealed record ChatEvent(string Type, object Data);

public sealed record ChatMessageView(
    Guid Id,
    string Role,
    string Status,
    string Content,
    JsonElement? Details,
    string? Error,
    DateTimeOffset CreatedAt,
    long DurationMs);

public sealed record ConversationSummary(Guid Id, string Title, DateTimeOffset CreatedAt, DateTimeOffset LastMessageAt);

public sealed record ConversationView(Guid Id, string Title, DateTimeOffset CreatedAt, IReadOnlyList<ChatMessageView> Messages);

/// <summary>Чаты консультанта: история, сохранение вопросов и ответов. Пользователь видит только свои чаты.</summary>
public sealed class ConsultantChatService(
    IAppDbContext db,
    IConsultantEngine engine,
    ILogger<ConsultantChatService> logger)
{
    public const int MaxQuestionLength = 4000;

    /// <summary>Сколько последних сообщений чата передаётся модели как контекст.</summary>
    private const int HistoryMessages = 12;

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<ConversationSummary>> ListAsync(Guid userId, CancellationToken ct) =>
        await db.AiConversations.AsNoTracking()
            .Where(c => c.UserId == userId && c.ArchivedAt == null)
            .OrderByDescending(c => c.LastMessageAt)
            .Take(200)
            .Select(c => new ConversationSummary(c.Id, c.Title, c.CreatedAt, c.LastMessageAt))
            .ToListAsync(ct);

    public async Task<ConversationView?> GetAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var conversation = await db.AiConversations.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId && c.ArchivedAt == null, ct);
        if (conversation is null)
        {
            return null;
        }

        var messages = await db.AiMessages.AsNoTracking()
            .Where(m => m.ConversationId == id)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);
        return new ConversationView(conversation.Id, conversation.Title, conversation.CreatedAt, messages.Select(View).ToList());
    }

    public async Task<bool> RenameAsync(Guid userId, Guid id, string title, CancellationToken ct)
    {
        var conversation = await db.AiConversations.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId && c.ArchivedAt == null, ct);
        if (conversation is null)
        {
            return false;
        }

        conversation.Title = TitleOf(title);
        conversation.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Убирает чат из истории. Сообщения остаются в базе для журнала AI.</summary>
    public async Task<bool> ArchiveAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var conversation = await db.AiConversations.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId && c.ArchivedAt == null, ct);
        if (conversation is null)
        {
            return false;
        }

        conversation.ArchivedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Вопрос в чат: сохраняет вопрос, получает ответ консультанта и сохраняет его (или ошибку).
    /// События: start → progress… → done | error. KeyNotFoundException — чата нет или он чужой.
    /// </summary>
    public async Task SendAsync(Guid userId, Guid? conversationId, string question, Func<ChatEvent, Task> emit, CancellationToken ct)
    {
        question = question.Trim();
        AiConversation conversation;
        if (conversationId is { } id)
        {
            conversation = await db.AiConversations.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId && c.ArchivedAt == null, ct)
                ?? throw new KeyNotFoundException("Чат не найден.");
        }
        else
        {
            conversation = new AiConversation { UserId = userId, Title = TitleOf(question) };
            db.AiConversations.Add(conversation);
        }

        var history = await HistoryAsync(conversation.Id, ct);

        var userMessage = new AiMessage { ConversationId = conversation.Id, Role = AiMessageRole.User, Content = question };
        db.AiMessages.Add(userMessage);
        conversation.LastMessageAt = userMessage.CreatedAt;
        await db.SaveChangesAsync(ct);

        await emit(new ChatEvent("start", new { conversationId = conversation.Id, title = conversation.Title, userMessage = View(userMessage) }));

        var watch = Stopwatch.StartNew();
        var reply = new AiMessage { ConversationId = conversation.Id, Role = AiMessageRole.Assistant, Content = string.Empty };
        try
        {
            var answer = await engine.AnswerAsync(
                new ConsultantTurn(userId, conversation.Id, question, history),
                p => emit(new ChatEvent("progress", p)),
                ct);
            reply.Content = answer.Content.Length > 0 ? answer.Content : "Модель вернула пустой ответ.";
            reply.Details = JsonSerializer.Serialize(answer.Details, Json);
            reply.InputTokens = answer.Usage.InputTokens;
            reply.OutputTokens = answer.Usage.OutputTokens;
        }
        catch (Exception ex) when (ex is LlmNotConfiguredException or AiProviderException)
        {
            reply.Status = AiMessageStatus.Failed;
            reply.Error = Trim(ex.Message);
        }
        catch (OperationCanceledException)
        {
            reply.Status = AiMessageStatus.Failed;
            reply.Error = "Ответ не получен: время ожидания истекло или сервер останавливается.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Консультант: ошибка ответа в чате {ConversationId}", conversation.Id);
            reply.Status = AiMessageStatus.Failed;
            reply.Error = "Не удалось получить ответ из-за внутренней ошибки OneBase. Подробности — в журнале сервера.";
        }

        reply.DurationMs = watch.ElapsedMilliseconds;
        reply.CreatedAt = DateTimeOffset.UtcNow;
        db.AiMessages.Add(reply);
        conversation.LastMessageAt = reply.CreatedAt;
        await db.SaveChangesAsync(CancellationToken.None);

        await emit(reply.Status == AiMessageStatus.Completed
            ? new ChatEvent("done", new { message = View(reply) })
            : new ChatEvent("error", new { message = View(reply), error = reply.Error }));
    }

    private async Task<IReadOnlyList<LlmMessage>> HistoryAsync(Guid conversationId, CancellationToken ct)
    {
        var recent = await db.AiMessages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.Status == AiMessageStatus.Completed)
            .OrderByDescending(m => m.CreatedAt)
            .Take(HistoryMessages)
            .Select(m => new { m.Role, m.Content })
            .ToListAsync(ct);
        recent.Reverse();

        // Модели ждут, что история начинается с вопроса пользователя.
        var start = recent.FindIndex(m => m.Role == AiMessageRole.User);
        return start < 0
            ? []
            : recent.Skip(start).Select(m => new LlmMessage(m.Role == AiMessageRole.User ? LlmRole.User : LlmRole.Assistant, m.Content)).ToList();
    }

    private static ChatMessageView View(AiMessage m) => new(
        m.Id,
        m.Role == AiMessageRole.User ? "user" : "assistant",
        m.Status == AiMessageStatus.Completed ? "completed" : "failed",
        m.Content,
        m.Details is null ? null : JsonDocument.Parse(m.Details).RootElement.Clone(),
        m.Error,
        m.CreatedAt,
        m.DurationMs);

    private static string TitleOf(string text)
    {
        var line = text.Trim().Split('\n', 2)[0].Trim();
        if (line.Length == 0)
        {
            return "Новый чат";
        }

        return line.Length <= 80 ? line : line[..80].TrimEnd() + "…";
    }

    private static string Trim(string text) => text.Length <= 1000 ? text : text[..1000];
}
