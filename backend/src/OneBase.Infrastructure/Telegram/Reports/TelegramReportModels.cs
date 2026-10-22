namespace OneBase.Infrastructure.Telegram.Reports;

/// <summary>Неверные данные запроса автоотчётов (400).</summary>
public sealed class ReportValidationException(string message) : Exception(message);

/// <summary>Нет прав на данные продаж (400 с причиной — интерфейсу нужна причина, а не «недостаточно прав»).</summary>
public sealed class ReportForbiddenException(string message) : Exception(message);

/// <summary>Отчёт уже отправлен или отправляется (409): повторно — только явно.</summary>
public sealed class ReportConflictException(string message) : Exception(message);

public sealed record ReportSettingsView(Guid? AccountId, string Language, DateTimeOffset? UpdatedAt);

public sealed record ReportSettingsInput(Guid? AccountId, string? Language);

public sealed record ReportAccountOption(Guid Id, string Name, string Status, string? Username);

/// <summary>Расписание: время «08:00», дни 1–7 (пн–вс), показатель sum/kg, следующий запуск.</summary>
public sealed record ReportScheduleView(string Kind, bool Enabled, string Time, string TimeZone, IReadOnlyList<int> Days, string Metric, DateTimeOffset? NextRunAt,
    DateTimeOffset? UpdatedAt);

public sealed record ReportScheduleInput(bool Enabled, string? Time, string? TimeZone, IReadOnlyList<int>? Days, string? Metric);

/// <summary>Последняя отправка в чат — для строки списка.</summary>
public sealed record ReportDeliveryBrief(string Kind, string Chat, string Status, DateTimeOffset At, string? Error);

public sealed record ReportChatRef(Guid Id, string Title, string Type, bool CanSend);

/// <summary>
/// Автоотправка: регион → супервайзер → чаты и группы, какие отчёты. Problem — почему сейчас отправить нельзя (регион или супервайзер
/// пропали из данных, нет чатов, в чат писать нельзя); строка сохранена, но отчёт не уйдёт, пока причина не устранена.
/// </summary>
public sealed record ReportSubscriptionView(
    Guid Id,
    Guid RegionId,
    string Region,
    long? SupervisorId,
    string Supervisor,
    int Agents,
    IReadOnlyList<ReportChatRef> Chats,
    bool Morning,
    bool Midday,
    bool Evening,
    bool Enabled,
    string? Problem,
    IReadOnlyList<ReportDeliveryBrief> Last);

public sealed record ReportSubscriptionInput(Guid RegionId, long? SupervisorId, IReadOnlyList<Guid>? ChatIds, bool Morning, bool Midday, bool Evening, bool Enabled);

public sealed record ReportPersonOption(long Id, string Name);

/// <summary>Регион OneBase: РМ, «СВР региона», супервайзеры и агенты месяца (агенты — имена, для подсказки «кто будет на картинке»).</summary>
public sealed record ReportRegionOption(Guid Id, string Name, string? Manager, string? SupervisorName, IReadOnlyList<ReportPersonOption> Supervisors, IReadOnlyList<string> Agents);

public sealed record ReportChatOption(Guid Id, string Title, string Type, string? Username, bool CanSend, string? Reason);

public sealed record ReportOverview(
    ReportSettingsView Settings,
    IReadOnlyList<ReportAccountOption> Accounts,
    IReadOnlyList<ReportScheduleView> Schedules,
    IReadOnlyList<ReportSubscriptionView> Subscriptions,
    IReadOnlyList<ReportRegionOption> Regions,
    IReadOnlyList<ReportChatOption> Chats,
    bool CanUseSales,
    bool TelegramEnabled);

/// <summary>
/// Предпросмотр и тест: demo — демонстрационные данные (название региона, агентов); real — регион и супервайзер (или автоотправка).
/// Metric и язык — как в расписании и настройках, если не заданы.
/// </summary>
public sealed record ReportPreviewInput(
    string? Kind,
    string? Mode,
    string? Region,
    int? Agents,
    Guid? SubscriptionId,
    Guid? RegionId,
    long? SupervisorId,
    string? Metric,
    string? Language);

public sealed record ReportPreview(IReadOnlyList<string> Pages, string Caption, string Summary, IReadOnlyList<string> Notes, IReadOnlyList<string> Problems, long Bytes);

public sealed record ReportTestInput(ReportPreviewInput Report, Guid ChatId);

public sealed record ReportSendInput(string? Kind, bool Force);

/// <summary>Итог отправки в один чат: sent, pending (временная ошибка — повтор автоматически), failed.</summary>
public sealed record ReportSendResult(Guid DeliveryId, string Status, string RecipientTitle, string ScopeTitle, string? Error, int Pages, DateTimeOffset? SentAt,
    DateTimeOffset? NextAttemptAt);

public sealed record ReportDeliveryView(
    Guid Id,
    DateTimeOffset CreatedAt,
    string Kind,
    DateOnly Period,
    string Trigger,
    string DataMode,
    string RecipientTitle,
    string ScopeTitle,
    string Status,
    int Attempts,
    DateTimeOffset? SentAt,
    DateTimeOffset? NextAttemptAt,
    string? Error,
    int Pages,
    long Bytes,
    string? Summary,
    DateTimeOffset? DataAsOf,
    string? By);
