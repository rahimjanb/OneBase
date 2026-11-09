using OneBase.Domain.Common;

namespace OneBase.Domain.Telegram;

/// <summary>
/// Подразделение, для которого работает аккаунт Telegram (продажи, HR, …). У аккаунта без строк всё как до маршрутизации: каждый диалог —
/// у продаж. Строки задаёт администратор в «Настройки → Telegram»; аккаунт и его сессия от этого не меняются.
/// </summary>
public class TelegramAccountDepartment
{
    public Guid AccountId { get; set; }

    /// <summary>Код подразделения OneBase (<c>public.Departments.Code</c>): <c>sales</c>, <c>hr</c>, …</summary>
    public required string Department { get; set; }

    /// <summary>Новые диалоги без признака подразделения уходят ему. Не больше одного на аккаунт; нет ни одного — новые диалоги «не распределены».</summary>
    public bool IsDefault { get; set; }

    /// <summary>Сообщений подразделения с этого номера в сутки — не больше (очередь исходящих; продажи шлют своими путями).</summary>
    public int DailyLimit { get; set; } = 200;

    /// <summary>Пауза между сообщениями подразделения с этого номера, секунд.</summary>
    public int MinIntervalSeconds { get; set; } = 5;

    /// <summary>Отправка подразделения с номера остановлена до этого времени (Telegram ограничил аккаунт — PEER_FLOOD) и почему.</summary>
    public DateTimeOffset? PausedUntil { get; set; }

    public string? PauseReason { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedById { get; set; }
}

/// <summary>Отвечает ли в диалоге AI подразделения или ведёт человек.</summary>
public enum TelegramConversationMode
{
    Ai,
    Manual,
}

public enum TelegramConversationStatus
{
    Active,
    Closed,
}

/// <summary>
/// Диалог аккаунта Telegram (личный чат или группа) и его хозяин — подразделение. Одна строка на аккаунт × чат. Department = null —
/// «не распределён»: автоматически не отвечает никто, решает человек. Строки появляются только у аккаунтов с настроенными
/// подразделениями (<see cref="TelegramAccountDepartment"/>); у остальных диалоги, как и раньше, у продаж без записи.
/// </summary>
public class TelegramConversation : Entity
{
    public Guid AccountId { get; set; }

    /// <summary>ID чата Telegram (у личного чата — ID собеседника).</summary>
    public long PeerId { get; set; }

    public TelegramChatType ChatType { get; set; }

    /// <summary>Собеседник личного чата (в группе — пусто: в ней пишут разные люди).</summary>
    public long? TelegramUserId { get; set; }

    /// <summary>Код подразделения-хозяина; null — не распределён.</summary>
    public string? Department { get; set; }

    /// <summary>
    /// Почему у этого хозяина: <c>default</c> (подразделение аккаунта по умолчанию), <c>claim</c> (признак подразделения в первом
    /// сообщении), <c>conflict</c> (признаки двух подразделений), <c>sales_user</c> (человек из списка агента продаж — у продаж),
    /// <c>no_default</c> (у аккаунта нет подразделения по умолчанию), <c>manual</c> (назначил человек), <c>agent_account</c> (номер выбран у
    /// ИИ-агента — диалог без хозяина отдан его отделу).
    /// </summary>
    public required string RouteReason { get; set; }

    /// <summary>Тип диалога в подразделении (у HR — <c>candidate</c>); пусто — обычный.</summary>
    public string? Kind { get; set; }

    public TelegramConversationStatus Status { get; set; } = TelegramConversationStatus.Active;

    public TelegramConversationMode Mode { get; set; } = TelegramConversationMode.Ai;

    /// <summary>Какой AI-агент ведёт диалог (код агента подразделения).</summary>
    public string? AssignedAgent { get; set; }

    /// <summary>Сотрудник OneBase, которому передан диалог.</summary>
    public Guid? AssignedUserId { get; set; }

    /// <summary>С чем связан диалог (у HR — вакансия): тип и ID записи.</summary>
    public string? RelatedType { get; set; }
    public Guid? RelatedId { get; set; }

    public DateTimeOffset? LastMessageAt { get; set; }
    public DateTimeOffset? LastInboundAt { get; set; }

    /// <summary>Кто последним менял хозяина или режим вручную.</summary>
    public Guid? UpdatedById { get; set; }

}
