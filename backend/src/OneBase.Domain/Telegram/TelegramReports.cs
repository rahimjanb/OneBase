using OneBase.Domain.Common;

namespace OneBase.Domain.Telegram;

/// <summary>
/// Прежние три запуска дневного отчёта (утро, обед, вечер). Хранятся в старых строках журнала и расписаниях; новые запуски — коды
/// <see cref="TelegramReportJob"/> («daily.morning», «promo.evening»…).
/// </summary>
public enum TelegramReportKind
{
    Morning,
    Midday,
    Evening,
}

/// <summary>
/// Общие настройки автоотчётов (одна строка): с какого аккаунта Telegram отправлять, язык картинок, порог «без закупа» и обновление данных
/// Linko перед отправкой. Аккаунт один на все отчёты — личные чаты и группы получателей принадлежат его диалогам.
/// </summary>
public class TelegramReportSettings
{
    /// <summary>Всегда 1: строка одна.</summary>
    public int Id { get; set; } = 1;

    public Guid? AccountId { get; set; }

    /// <summary>ru или uz — подписи на картинке (имена и названия — как в Linko).</summary>
    public string Language { get; set; } = "ru";

    /// <summary>«Клиенты без закупа»: сколько дней без подтверждённого закупа (по умолчанию 30).</summary>
    public int NoPurchaseDays { get; set; } = 30;

    /// <summary>Перед отправкой обновить данные Linko (заказы, визиты), если последнее обновление старше 10 минут.</summary>
    public bool RefreshBeforeSend { get; set; } = true;

    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedById { get; set; }
}

/// <summary>Прежнее расписание дневного отчёта по трём видам. Перенесено в <see cref="TelegramReportJob"/> (daily.*) и больше не читается.</summary>
public class TelegramReportSchedule
{
    public TelegramReportKind Kind { get; set; }

    public bool Enabled { get; set; }

    public TimeOnly Time { get; set; }

    public string TimeZone { get; set; } = "Asia/Tashkent";

    public int Days { get; set; } = 63;

    public string Metric { get; set; } = "sum";

    public DateTimeOffset? ArmedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedById { get; set; }
}

/// <summary>
/// Расписание одного запуска отчёта: код «отчёт.запуск» («daily.midday» — дневной план в 13:00, «weekly.monday» — итоги недели в понедельник).
/// Включается отдельно; ArmedAt — с какого момента действует (запуск, время которого прошло до включения или изменения, не догоняется).
/// CatchUpMinutes — сколько минут после времени запуска отчёт ещё можно отправить, если сервер в это время не работал: старый дневной
/// отчёт после долгого простоя не уходит. Version растёт при каждом изменении — входит в ключ отправки.
/// </summary>
public class TelegramReportJob
{
    public required string Code { get; set; }

    public bool Enabled { get; set; }

    /// <summary>Время отправки в часовом поясе TimeZone.</summary>
    public TimeOnly Time { get; set; }

    /// <summary>IANA, по умолчанию Asia/Tashkent.</summary>
    public string TimeZone { get; set; } = "Asia/Tashkent";

    /// <summary>Дни недели битами: пн = 1, вт = 2, … вс = 64.</summary>
    public int Days { get; set; }

    public int CatchUpMinutes { get; set; } = 120;

    public int Version { get; set; } = 1;

    public DateTimeOffset? ArmedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedById { get; set; }
}

/// <summary>
/// Запуск по расписанию: один на код и день (уникальный индекс — второй экземпляр службы тот же запуск не начнёт). Planned/Sent/Failed/Skipped —
/// отправки запуска; Status: running (идут отправки), done, partial (есть ошибки), failed (запуск не выполнен), skipped (опоздал: простой
/// дольше CatchUpMinutes).
/// </summary>
public class TelegramReportRun : Entity
{
    public required string Job { get; set; }

    /// <summary>День отчёта (в часовом поясе расписания).</summary>
    public DateOnly Period { get; set; }

    public DateTimeOffset ScheduledAt { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }

    public string Status { get; set; } = "running";

    public int Planned { get; set; }
    public int Sent { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }

    /// <summary>Свежесть данных Linko на момент постановки отправок.</summary>
    public DateTimeOffset? DataAsOf { get; set; }

    public string? Error { get; set; }
}

/// <summary>
/// Дневной план ТП для «ТП без заказов»: считается один раз утром на бизнес-дату — остаток месячного плана (план − факт по вчера) ÷
/// оставшиеся рабочие дни (пн–сб без праздников, с сегодняшнего) — и не меняется до конца дня, как бы ни менялся факт. Status: ok — план
/// есть; no-plan — плана месяца в Linko нет (не ноль, а «—»); done — месячный план выполнен; day-off — воскресенье или праздник; no-days —
/// рабочих дней не осталось. Пересчёт (администратор с причиной, план появился в Linko) не правит строку, а заменяет её новой: прежняя
/// остаётся историей с SupersededAt. Одна действующая строка на ТП и день (частичный уникальный индекс).
/// </summary>
public class TelegramReportDailyPlan : Entity
{
    public long LinkoUserId { get; set; }
    public DateOnly Date { get; set; }

    /// <summary>План месяца, сум (staff_balance Linko); null — в Linko плана нет.</summary>
    public decimal? MonthPlanSum { get; set; }

    /// <summary>Факт с 1-го числа по вчера включительно (проданное по дате приёмки, как в «Продажах»).</summary>
    public decimal FactBeforeSum { get; set; }

    public decimal? RemainingSum { get; set; }
    public int WorkDaysLeft { get; set; }
    public decimal? DailyPlanSum { get; set; }
    public required string Status { get; set; }
    public string? Note { get; set; }

    /// <summary>Кто посчитал: morning (утренняя задача), report (первое построение отчёта), admin (пересчёт), plan-appeared (план появился в Linko).</summary>
    public required string Trigger { get; set; }

    public string? Reason { get; set; }
    public DateTimeOffset ComputedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? SupersededAt { get; set; }
    public Guid? SupersededById { get; set; }
}

/// <summary>Роль получателя: РМ — свод по регионам направления, СВ — по ТП своего региона, ТП — только свои цифры и точки.</summary>
public static class TelegramReportRoles
{
    public const string Rm = "rm";
    public const string Sv = "sv";
    public const string Tp = "tp";

    public static readonly IReadOnlyList<string> All = [Rm, Sv, Tp];
}

/// <summary>
/// Получатель автоотчётов: роль и область из раздела «Продажи» (РМ — направление, пусто — вся страна; СВ — регион и супервайзер; ТП — агент
/// Linko), чаты аккаунта, куда уходят отчёты (личный чат человека и/или группы), и на какие запуски он подписан (Reports — коды через
/// запятую). Таких строк может быть сколько угодно.
/// </summary>
public class TelegramReportSubscription : Entity
{
    public string Role { get; set; } = TelegramReportRoles.Sv;

    /// <summary>Имя получателя для списка («Азиз Каримов»); пусто — по области.</summary>
    public string? Title { get; set; }

    /// <summary>РМ: направление (sales.Directions); null — все направления.</summary>
    public Guid? DirectionId { get; set; }

    /// <summary>СВ и ТП: регион OneBase (sales.Regions).</summary>
    public Guid? RegionId { get; set; }

    /// <summary>Супервайзер — пользователь Linko; null — без супервайзера (на картинке «СВР региона» из оргструктуры).</summary>
    public long? SupervisorId { get; set; }

    public string? SupervisorName { get; set; }

    /// <summary>ТП: агент Linko.</summary>
    public long? AgentId { get; set; }

    public string? AgentName { get; set; }

    /// <summary>Коды запусков через запятую: «daily.morning,daily.evening,weekly.saturday».</summary>
    public string Reports { get; set; } = "";

    /// <summary>Прежние флаги дневного отчёта — перенесены в Reports.</summary>
    public bool Morning { get; set; }
    public bool Midday { get; set; }
    public bool Evening { get; set; }

    public bool Enabled { get; set; } = true;

    public Guid CreatedById { get; set; }
    public Guid? UpdatedById { get; set; }

    public List<TelegramReportSubscriptionChat> Chats { get; set; } = [];
}

/// <summary>Чат или группа получателя (один чат — один раз в строке).</summary>
public class TelegramReportSubscriptionChat
{
    public Guid SubscriptionId { get; set; }
    public Guid ChatId { get; set; }
}

/// <summary>
/// Журнал отправок: одна строка — один отчёт в один чат. Ключ отправки по расписанию — запуск (Report), день, чат и область (ScopeKey):
/// уникальный индекс не даст отправить тот же отчёт той же области в тот же чат дважды, даже если человек попал в несколько получателей
/// или служба запущена в двух экземплярах. Status: pending, sending, sent, failed, skipped (условие не выполнено, нет чата и т. п. —
/// причина в Error), unknown (сеть оборвалась посреди отправки: дошёл ли отчёт — неизвестно, автоповтора нет).
/// </summary>
public class TelegramReportDelivery : Entity
{
    /// <summary>Прежний вид дневного отчёта (старые строки); у новых — null.</summary>
    public TelegramReportKind? Kind { get; set; }

    /// <summary>Код запуска: «daily.midday».</summary>
    public string? Report { get; set; }

    /// <summary>Запуск по расписанию, в который вошла отправка.</summary>
    public Guid? RunId { get; set; }

    /// <summary>День отчёта (в часовом поясе расписания).</summary>
    public DateOnly Period { get; set; }

    /// <summary>schedule, manual, test.</summary>
    public required string Trigger { get; set; }

    /// <summary>real — данные OneBase.</summary>
    public required string DataMode { get; set; }

    /// <summary>Получатель, по которому ушёл отчёт (тест — без него).</summary>
    public Guid? SubscriptionId { get; set; }

    /// <summary>Роль и область отчёта: «sv:регион:супервайзер», «tp:агент», «rm:направление».</summary>
    public string? Role { get; set; }
    public string? ScopeKey { get; set; }

    public Guid AccountId { get; set; }
    public Guid? ChatId { get; set; }
    public long? PeerId { get; set; }

    /// <summary>Чат или группа — как в момент отправки.</summary>
    public required string RecipientTitle { get; set; }

    /// <summary>Область — как на картинке: «Ташкент · Азиз Каримов».</summary>
    public required string ScopeTitle { get; set; }

    public Guid? RegionId { get; set; }
    public long? SupervisorId { get; set; }

    public string Status { get; set; } = "pending";

    public int Attempts { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public string? Error { get; set; }

    /// <summary>Сколько миллисекунд заняли формирование и отправка.</summary>
    public int? DurationMs { get; set; }

    public int Pages { get; set; }
    public long Bytes { get; set; }
    public string? MessageIds { get; set; }

    /// <summary>Кратко, что ушло: «План 300 млн · факт 120 млн (40%)».</summary>
    public string? Summary { get; set; }

    /// <summary>Свежесть данных Linko на момент формирования.</summary>
    public DateTimeOffset? DataAsOf { get; set; }

    public Guid? CreatedById { get; set; }
}
