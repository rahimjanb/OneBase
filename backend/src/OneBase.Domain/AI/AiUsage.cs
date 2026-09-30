namespace OneBase.Domain.AI;

/// <summary>Один вызов модели (генерация или эмбеддинги): для учёта токенов и стоимости.</summary>
public class AiUsage
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? UserId { get; set; }
    public string? AgentCode { get; set; }
    public Guid? ConversationId { get; set; }

    /// <summary>Зачем вызвана модель: consultant.route, agent, consultant.answer, knowledge.index…</summary>
    public required string Purpose { get; set; }

    public required string Provider { get; set; }
    public required string Model { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }

    /// <summary>Стоимость по ценам модели на момент вызова, USD; null — цена модели не задана.</summary>
    public decimal? CostUsd { get; set; }

    public long DurationMs { get; set; }
    public bool Success { get; set; }

    /// <summary>Текст ошибки провайдера — без ключей (очищен в AI Gateway).</summary>
    public string? Error { get; set; }
}

/// <summary>
/// AI-журнал: один вопрос консультанту или задача AI-сотруднику — кто спросил, какие сотрудники, модели и инструменты работали,
/// источники, токены, стоимость, время, ответ и ошибки.
/// </summary>
public class AiAuditLog
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid UserId { get; set; }
    public Guid? ConversationId { get; set; }
    public Guid? MessageId { get; set; }

    /// <summary>consultant — вопрос в чате; agent — задача одному сотруднику.</summary>
    public required string Kind { get; set; }

    public required string Question { get; set; }

    /// <summary>Коды AI-сотрудников через запятую.</summary>
    public string? Agents { get; set; }

    public string? Model { get; set; }
    public string? Tools { get; set; }

    /// <summary>Источники данных ответа (jsonb).</summary>
    public string? Sources { get; set; }

    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal? CostUsd { get; set; }
    public long DurationMs { get; set; }

    /// <summary>completed | failed.</summary>
    public required string Status { get; set; }

    /// <summary>Начало ответа (полный ответ — в сообщении чата).</summary>
    public string? ResponsePreview { get; set; }

    public string? Error { get; set; }
}
