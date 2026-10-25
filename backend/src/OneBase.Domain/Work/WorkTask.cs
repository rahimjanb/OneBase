using OneBase.Domain.Common;
using OneBase.Domain.Identity;

namespace OneBase.Domain.Work;

/// <summary>
/// Статус задачи. Хранится строкой — новые значения добавляются без миграции данных.
/// New → InProgress → Review («На проверке») → Done; Cancelled — отменена. Заблокированность — отдельный признак (IsBlocked), не статус.
/// </summary>
public enum WorkTaskStatus
{
    New,
    InProgress,
    Done,
    Cancelled,

    /// <summary>Исполнитель отправил результат на проверку; принимает или возвращает на доработку постановщик или руководитель.</summary>
    Review,
}

/// <summary>Приоритет: низкий, обычный, высокий, критический (Urgent — прежнее имя критического, хранится строкой).</summary>
public enum WorkTaskPriority
{
    Low,
    Medium,
    High,
    Urgent,
}

/// <summary>
/// Задача в разделе «Задачи» OneBase: задача отдела (и, если указан, проекта). Задачи полевой команды продаж (агенты и
/// супервайзеры) живут в Sales Base (field.Tasks) и показываются в отделе «Продажи» отдельно.
/// </summary>
public class WorkTask : Entity
{
    public Guid DepartmentId { get; set; }
    public Department? Department { get; set; }

    /// <summary>Проект; null — задача только отдела.</summary>
    public Guid? ProjectId { get; set; }
    public WorkProject? Project { get; set; }

    public required string Title { get; set; }
    public string? Description { get; set; }

    /// <summary>Исполнитель; null — задача отдела без исполнителя (возьмёт любой сотрудник).</summary>
    public Guid? AssigneeId { get; set; }
    public User? Assignee { get; set; }

    /// <summary>Постановщик.</summary>
    public Guid? CreatedById { get; set; }
    public User? CreatedBy { get; set; }

    public WorkTaskPriority Priority { get; set; } = WorkTaskPriority.Medium;
    public WorkTaskStatus Status { get; set; } = WorkTaskStatus.New;
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Процент выполнения 0–100: с чек-листом — доля выполненных пунктов, без него — как указал исполнитель.</summary>
    public int Progress { get; set; }

    /// <summary>Работа остановлена внешней причиной (ждёт данных, решения, другой задачи) — индикатор на доске и в таблице.</summary>
    public bool IsBlocked { get; set; }
    public string? BlockedReason { get; set; }

    /// <summary>Итог или причина отмены.</summary>
    public string? Result { get; set; }

    /// <summary>Отправлена на проверку (последний раз).</summary>
    public DateTimeOffset? ReviewRequestedAt { get; set; }

    /// <summary>В архиве — только выполненная или отменённая задача; из архива возвращается. Задачи не удаляются автоматически.</summary>
    public DateTimeOffset? ArchivedAt { get; set; }
    public Guid? ArchivedById { get; set; }

    /// <summary>Создана из предложения AI-диспетчера (после подтверждения руководителем).</summary>
    public Guid? SuggestionId { get; set; }

    /// <summary>Срок, о приближении которого уже напомнили (новый срок — новое напоминание).</summary>
    public DateOnly? DueSoonNotifiedFor { get; set; }

    /// <summary>Срок, о просрочке которого уже сообщили.</summary>
    public DateOnly? OverdueNotifiedFor { get; set; }

    public ICollection<WorkTaskChecklistItem> Checklist { get; set; } = [];
}

/// <summary>Пункт чек-листа (подзадача).</summary>
public class WorkTaskChecklistItem : Entity
{
    public Guid TaskId { get; set; }
    public WorkTask? Task { get; set; }
    public required string Title { get; set; }
    public bool IsDone { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset? DoneAt { get; set; }
    public Guid? DoneById { get; set; }
}

/// <summary>Комментарий к задаче.</summary>
public class WorkTaskComment : Entity
{
    public Guid TaskId { get; set; }
    public WorkTask? Task { get; set; }
    public Guid? AuthorId { get; set; }
    public User? Author { get; set; }
    public required string Text { get; set; }
}

/// <summary>Вложение задачи: содержимое — в хранилище файлов (MinIO) под ObjectKey, здесь — описание.</summary>
public class WorkTaskAttachment : Entity
{
    public Guid TaskId { get; set; }
    public WorkTask? Task { get; set; }
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public required string ObjectKey { get; set; }
    public Guid? UploadedById { get; set; }
    public User? UploadedBy { get; set; }
}

/// <summary>Событие истории задачи: что изменилось, кто и когда. Пишется сервисом при каждом изменении.</summary>
public class WorkTaskEvent : Entity
{
    public Guid TaskId { get; set; }
    public WorkTask? Task { get; set; }
    public Guid? ActorId { get; set; }
    public User? Actor { get; set; }

    /// <summary>created, updated, assigned, due, status, review, returned, accepted, blocked, progress, checklist, comment, attachment, archived, restored.</summary>
    public required string Kind { get; set; }

    /// <summary>Описание по-русски: «Срок: 25.10.2026 → 30.10.2026».</summary>
    public required string Text { get; set; }
}

/// <summary>Проект: задачи нескольких людей (в том числе из разных отделов) под одной целью. Принадлежит отделу.</summary>
public class WorkProject : Entity
{
    public required string Name { get; set; }
    public string? Description { get; set; }

    /// <summary>Отдел-владелец: задачи проекта относятся к нему.</summary>
    public Guid DepartmentId { get; set; }
    public Department? Department { get; set; }

    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }

    public ICollection<WorkProjectMember> Members { get; set; } = [];
}

/// <summary>Участник проекта; IsManager — руководитель проекта (PM): управляет задачами проекта.</summary>
public class WorkProjectMember
{
    public Guid ProjectId { get; set; }
    public WorkProject? Project { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public bool IsManager { get; set; }
}

/// <summary>Руководитель отдела в разделе «Задачи»: управляет всеми задачами отдела. Один человек может руководить несколькими отделами.</summary>
public class WorkDepartmentHead
{
    public Guid DepartmentId { get; set; }
    public Department? Department { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
}

public enum WorkSuggestionStatus
{
    /// <summary>Ожидает подтверждения руководителя.</summary>
    Pending,
    Accepted,
    Rejected,
}

/// <summary>
/// Предложение AI-диспетчера: задача, которую стоит поставить, с обоснованием по данным OneBase. Задача не создаётся, пока руководитель
/// не подтвердит предложение (можно отредактировать) — тогда CreatedTaskId. Fingerprint — источник и период: повторный запуск не
/// создаёт такое же предложение.
/// </summary>
public class WorkTaskSuggestion : Entity
{
    /// <summary>Источник и объект: alert:{ключ находки}, overdue:{отдел}:{неделя}, review:{отдел}:{неделя}. Уникален.</summary>
    public required string Fingerprint { get; set; }

    /// <summary>alert | overdue | review.</summary>
    public required string SourceKind { get; set; }

    public required string Title { get; set; }

    /// <summary>Описание проблемы.</summary>
    public required string Problem { get; set; }

    /// <summary>Обоснование — только факты из данных OneBase (числа сверяются с фактами источника).</summary>
    public required string Rationale { get; set; }

    /// <summary>Факты источника, на которых построено предложение (для проверки и показа).</summary>
    public required string Facts { get; set; }

    /// <summary>Данных недостаточно для уверенного предложения — так и показывается руководителю.</summary>
    public bool InsufficientData { get; set; }

    public Guid DepartmentId { get; set; }
    public Department? Department { get; set; }
    public Guid? AssigneeId { get; set; }
    public User? Assignee { get; set; }
    public WorkTaskPriority Priority { get; set; } = WorkTaskPriority.Medium;
    public DateOnly? DueDate { get; set; }

    /// <summary>Страница OneBase с данными источника.</summary>
    public string? SourceHref { get; set; }

    /// <summary>Право, без которого предложение (как и находка-источник) не показывается.</summary>
    public string? RequiredPermission { get; set; }

    /// <summary>Модель, сформулировавшая предложение; null — по правилу, без модели.</summary>
    public string? Model { get; set; }

    public WorkSuggestionStatus Status { get; set; } = WorkSuggestionStatus.Pending;
    public Guid? DecidedById { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionNote { get; set; }
    public Guid? CreatedTaskId { get; set; }
    public Guid? GeneratedById { get; set; }
}
