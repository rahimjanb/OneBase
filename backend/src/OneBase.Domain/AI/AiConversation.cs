using OneBase.Domain.Common;

namespace OneBase.Domain.AI;

/// <summary>Чат пользователя с главным AI-консультантом. Виден только своему автору.</summary>
public class AiConversation : Entity
{
    public Guid UserId { get; set; }

    public required string Title { get; set; }

    public DateTimeOffset LastMessageAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Чат убран из истории пользователем. Сообщения сохраняются для журнала AI.</summary>
    public DateTimeOffset? ArchivedAt { get; set; }

    public List<AiMessage> Messages { get; set; } = [];
}

public enum AiMessageRole
{
    User,
    Assistant,
}

public enum AiMessageStatus
{
    Completed,
    Failed,
}

/// <summary>Сообщение чата консультанта.</summary>
public class AiMessage : Entity
{
    public Guid ConversationId { get; set; }
    public AiConversation? Conversation { get; set; }

    public AiMessageRole Role { get; set; }
    public AiMessageStatus Status { get; set; } = AiMessageStatus.Completed;

    /// <summary>Текст вопроса или ответа (Markdown).</summary>
    public required string Content { get; set; }

    /// <summary>
    /// Разбор ответа в JSON (jsonb): какие AI-сотрудники работали, их находки, источники данных, модель.
    /// Внутренних промптов и секретов не содержит.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>Текст ошибки для пользователя, если ответ не получен.</summary>
    public string? Error { get; set; }

    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public long DurationMs { get; set; }
}
