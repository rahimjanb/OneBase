using OneBase.Domain.Common;

namespace OneBase.Domain.Telegram;

/// <summary>Автоматический отчёт в Telegram: утро — план на день, обед — план/факт, вечер — итоги дня.</summary>
public enum TelegramReportKind
{
    Morning,
    Midday,
    Evening,
}

/// <summary>
/// Общие настройки автоотчётов (одна строка): с какого аккаунта Telegram отправлять и на каком языке подписи картинок. Аккаунт один на
/// все отчёты — получатели-чаты принадлежат его диалогам.
/// </summary>
public class TelegramReportSettings
{
    /// <summary>Всегда 1: строка одна.</summary>
    public int Id { get; set; } = 1;

    public Guid? AccountId { get; set; }

    /// <summary>ru или uz — подписи на картинке (имена и названия — как в Linko и Sales Base).</summary>
    public string Language { get; set; } = "ru";

    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedById { get; set; }
}

/// <summary>
/// Расписание одного вида отчёта. Включается отдельно: сохранение настроек ничего не отправляет. ArmedAt — с какого момента расписание
/// действует: запуск, время которого прошло до включения (или до изменения времени), не догоняется.
/// </summary>
public class TelegramReportSchedule
{
    public TelegramReportKind Kind { get; set; }

    public bool Enabled { get; set; }

    /// <summary>Время отправки в часовом поясе TimeZone.</summary>
    public TimeOnly Time { get; set; }

    /// <summary>IANA, по умолчанию Asia/Tashkent.</summary>
    public string TimeZone { get; set; } = "Asia/Tashkent";

    /// <summary>Дни недели битами: пн = 1, вт = 2, … вс = 64. По умолчанию пн–сб (63).</summary>
    public int Days { get; set; } = 63;

    /// <summary>sum — план и факт в сумах, kg — в килограммах.</summary>
    public string Metric { get; set; } = "sum";

    public DateTimeOffset? ArmedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedById { get; set; }
}

/// <summary>
/// Автоотправка: регион OneBase (sales.Regions) → супервайзер региона → чаты и группы аккаунта, куда уходят отчёты, и какие из трёх
/// отчётов. Агенты на картинке — команда региона, подставляются сами. Таких строк может быть сколько угодно.
/// </summary>
public class TelegramReportSubscription : Entity
{
    public Guid RegionId { get; set; }

    /// <summary>Супервайзер — пользователь Linko; null — без супервайзера (на картинке «СВР региона» из оргструктуры).</summary>
    public long? SupervisorId { get; set; }

    /// <summary>Имя супервайзера при сохранении — для списка, если он пропадёт из данных месяца.</summary>
    public string? SupervisorName { get; set; }

    public bool Morning { get; set; }
    public bool Midday { get; set; }
    public bool Evening { get; set; }

    public bool Enabled { get; set; } = true;

    public Guid CreatedById { get; set; }
    public Guid? UpdatedById { get; set; }

    public List<TelegramReportSubscriptionChat> Chats { get; set; } = [];
}

/// <summary>Чат или группа автоотправки (один чат — один раз в строке).</summary>
public class TelegramReportSubscriptionChat
{
    public Guid SubscriptionId { get; set; }
    public Guid ChatId { get; set; }
}

/// <summary>
/// Журнал отправок: одна строка — один отчёт в один чат. По расписанию один и тот же отчёт автоотправки за один день в один чат уходит
/// один раз (уникальный индекс); вручную — повторно только явно.
/// </summary>
public class TelegramReportDelivery : Entity
{
    public TelegramReportKind Kind { get; set; }

    /// <summary>День отчёта (в часовом поясе расписания).</summary>
    public DateOnly Period { get; set; }

    /// <summary>schedule, manual, test.</summary>
    public required string Trigger { get; set; }

    /// <summary>real — данные OneBase, demo — демонстрационные (на картинке «ТЕСТОВЫЕ ДАННЫЕ»).</summary>
    public required string DataMode { get; set; }

    /// <summary>Автоотправка, по которой ушёл отчёт (тест — без неё).</summary>
    public Guid? SubscriptionId { get; set; }

    public Guid AccountId { get; set; }
    public Guid? ChatId { get; set; }
    public long? PeerId { get; set; }

    /// <summary>Чат или группа — как в момент отправки.</summary>
    public required string RecipientTitle { get; set; }

    /// <summary>Регион и супервайзер — как на картинке.</summary>
    public required string ScopeTitle { get; set; }

    public Guid? RegionId { get; set; }
    public long? SupervisorId { get; set; }

    /// <summary>pending, sending, sent, failed.</summary>
    public string Status { get; set; } = "pending";

    public int Attempts { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public string? Error { get; set; }

    public int Pages { get; set; }
    public long Bytes { get; set; }
    public string? MessageIds { get; set; }

    /// <summary>Кратко, что ушло: «План 300 млн · факт 120 млн (40%)».</summary>
    public string? Summary { get; set; }

    /// <summary>Свежесть данных Linko на момент формирования.</summary>
    public DateTimeOffset? DataAsOf { get; set; }

    public Guid? CreatedById { get; set; }
}
