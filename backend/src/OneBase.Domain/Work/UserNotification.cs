using OneBase.Domain.Common;
using OneBase.Domain.Identity;

namespace OneBase.Domain.Work;

public enum UserNotificationKind
{
    /// <summary>Поставлена задача отдела.</summary>
    TaskAssigned,

    /// <summary>Задачу отдела изменили (срок, исполнитель) или вернули.</summary>
    TaskChanged,

    /// <summary>Исполнитель выполнил или отменил задачу отдела — автору.</summary>
    TaskDone,

    /// <summary>Агент или супервайзер выполнил полевую задачу, поставленную из OneBase, — автору.</summary>
    FieldTaskDone,

    /// <summary>Исполнитель отложил полевую задачу, поставленную из OneBase, — автору.</summary>
    FieldTaskPostponed,
}

/// <summary>
/// Уведомление пользователя OneBase (колокольчик в шапке и push на телефон). Уведомления участников Sales Base —
/// в field.Notifications; push доходит и до них — через пользователя, привязанного к карточке участника.
/// </summary>
public class UserNotification : Entity
{
    public Guid RecipientId { get; set; }
    public User? Recipient { get; set; }

    public UserNotificationKind Kind { get; set; }
    public required string Title { get; set; }
    public string? Body { get; set; }

    /// <summary>Путь внутри OneBase, куда ведёт уведомление (например, /tasks/sales).</summary>
    public string? Link { get; set; }

    public DateTimeOffset? ReadAt { get; set; }
}
