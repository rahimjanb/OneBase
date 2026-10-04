using OneBase.Domain.Common;

namespace OneBase.Domain.Field;

/// <summary>Кто поставил задачу.</summary>
public enum FieldActorType
{
    Rm,
    Supervisor,
    Agent,
    Ai,
    System,
}

public enum FieldTaskStatus
{
    New,
    Accepted,
    InProgress,
    Completed,
    Verified,
    Cancelled,
    Postponed,
}

public class FieldTask : Entity
{
    public required string Title { get; set; }
    public string? Description { get; set; }

    public Guid? CreatedById { get; set; }
    public FieldActorType CreatedByType { get; set; }

    public Guid AssignedToId { get; set; }
    public FieldMember? AssignedTo { get; set; }

    /// <summary>Супервайзер исполнителя на момент постановки (для фильтра «задачи моей команды»).</summary>
    public Guid? SupervisorId { get; set; }

    public long? MarketId { get; set; }
    public Guid? RouteId { get; set; }

    public FieldPriority Priority { get; set; } = FieldPriority.Medium;
    public FieldTaskStatus Status { get; set; } = FieldTaskStatus.New;
    public DateOnly? DueDate { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public string? Result { get; set; }

    /// <summary>Рекомендация AI, из которой создана задача.</summary>
    public Guid? RecommendationId { get; set; }
}
