using OneBase.Domain.Common;
using OneBase.Domain.Identity;

namespace OneBase.Domain.Work;

public enum WorkTaskStatus
{
    New,
    InProgress,
    Done,
    Cancelled,
}

public enum WorkTaskPriority
{
    Low,
    Medium,
    High,
    Urgent,
}

/// <summary>
/// Задача отдела в разделе «Задачи» OneBase: ставится сотруднику отдела. Задачи полевой команды продаж (агенты и
/// супервайзеры) живут в Sales Base (field.Tasks) и показываются в отделе «Продажи» отдельно.
/// </summary>
public class WorkTask : Entity
{
    public Guid DepartmentId { get; set; }
    public Department? Department { get; set; }

    public required string Title { get; set; }
    public string? Description { get; set; }

    /// <summary>Исполнитель; null — задача отдела без исполнителя (возьмёт любой сотрудник).</summary>
    public Guid? AssigneeId { get; set; }
    public User? Assignee { get; set; }

    public Guid? CreatedById { get; set; }
    public User? CreatedBy { get; set; }

    public WorkTaskPriority Priority { get; set; } = WorkTaskPriority.Medium;
    public WorkTaskStatus Status { get; set; } = WorkTaskStatus.New;
    public DateOnly? DueDate { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Итог или причина отмены.</summary>
    public string? Result { get; set; }
}
