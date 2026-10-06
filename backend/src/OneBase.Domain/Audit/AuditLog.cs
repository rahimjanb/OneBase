namespace OneBase.Domain.Audit;

public enum ActorType
{
    User,
    Agent,
    System,

    /// <summary>Подключение папки отдела к Windows (WebDAV): ActorId — id подключения, логин — в Data.</summary>
    Connection,
}

/// <summary>Неизменяемая запись аудита: действия людей, AI-агентов и системы.</summary>
public class AuditLog
{
    public long Id { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public ActorType ActorType { get; set; }
    public required string ActorId { get; set; }

    public required string Action { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }

    /// <summary>Детали в JSON (хранится как jsonb).</summary>
    public string? Data { get; set; }
}
