using OneBase.Domain.Common;

namespace OneBase.Domain.Telegram;

public enum TelegramMessageDirection
{
    In,
    Out,
}

/// <summary>
/// Сообщение диалога подразделения (входящее и исходящее) — история переписки там, где подразделение её ведёт (HR: переписка с кандидатом).
/// Агент продаж сюда не пишет. Удаляется вместе с данными того, с кем переписка (у HR — с данными кандидата).
/// </summary>
public class TelegramConversationMessage
{
    public long Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid AccountId { get; set; }
    public long PeerId { get; set; }

    /// <summary>ID сообщения в Telegram (у входящего — всегда; у исходящего — после отправки).</summary>
    public int? MessageId { get; set; }

    public TelegramMessageDirection Direction { get; set; }
    public string? Text { get; set; }
    public string? FileName { get; set; }

    /// <summary>Файл сохранён как резюме кандидата.</summary>
    public Guid? ResumeId { get; set; }

    /// <summary>candidate, ai, user (сотрудник из OneBase), phone (с телефона аккаунта), system.</summary>
    public required string Author { get; set; }

    public Guid? AuthorUserId { get; set; }
    public DateTimeOffset SentAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum TelegramOutboxStatus
{
    Pending,
    Sending,
    Sent,
    Failed,

    /// <summary>Telegram не подтвердил отправку (тайм-аут, обрыв, перезапуск посреди отправки) — могло уйти; автоповтора нет.</summary>
    Unknown,

    Cancelled,
}

/// <summary>
/// Очередь исходящих подразделений (HR: ответы рекрутера, ручные сообщения, публикации вакансий, напоминания). Один ключ идемпотентности —
/// одна отправка: повтор того же действия дубля не даёт. Статусы и повторы — как у рассылок: «неизвестно, дошло ли» — без автоповтора.
/// </summary>
public class TelegramOutgoingMessage : Entity
{
    public Guid AccountId { get; set; }
    public long PeerId { get; set; }
    public Guid? ConversationId { get; set; }
    public required string Department { get; set; }

    /// <summary>reply, manual, publication, reminder, followup.</summary>
    public required string Kind { get; set; }

    public required string Text { get; set; }
    public int? ReplyToMessageId { get; set; }
    public required string IdempotencyKey { get; set; }
    public TelegramOutboxStatus Status { get; set; } = TelegramOutboxStatus.Pending;
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }

    /// <summary>ID отправленных сообщений Telegram через запятую.</summary>
    public string? MessageIds { get; set; }

    public string? Error { get; set; }

    /// <summary>С чем связана отправка (publication, interview…), чтобы после отправки обновить запись.</summary>
    public string? RelatedType { get; set; }

    public Guid? RelatedId { get; set; }

    /// <summary>Перед отправкой показать «печатает…» столько секунд (ИИ-агент пишет как человек); 0 — сразу.</summary>
    public int TypingSeconds { get; set; }

    /// <summary>ai, user, system.</summary>
    public required string Author { get; set; }

    /// <summary>Файл к сообщению (отправил HR из OneBase): ключ в хранилище, имя, тип, размер; текст — подпись (может быть пустым).</summary>
    public string? FileKey { get; set; }

    public string? FileName { get; set; }
    public string? FileContentType { get; set; }
    public long? FileSize { get; set; }

    public Guid? CreatedById { get; set; }
}
