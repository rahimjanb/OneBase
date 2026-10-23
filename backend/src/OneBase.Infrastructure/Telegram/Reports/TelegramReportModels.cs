namespace OneBase.Infrastructure.Telegram.Reports;

/// <summary>Неверные данные запроса автоотчётов (400).</summary>
public sealed class ReportValidationException(string message) : Exception(message);

/// <summary>Нет прав на данные продаж (400 с причиной — интерфейсу нужна причина, а не «недостаточно прав»).</summary>
public sealed class ReportForbiddenException(string message) : Exception(message);

/// <summary>Отчёт уже отправлен или отправляется (409): повторно — только явно.</summary>
public sealed class ReportConflictException(string message) : Exception(message);

public sealed record ReportSettingsView(Guid? AccountId, string Language, int NoPurchaseDays, bool RefreshBeforeSend, DateTimeOffset? UpdatedAt);

public sealed record ReportSettingsInput(Guid? AccountId, string? Language, int? NoPurchaseDays, bool? RefreshBeforeSend);

public sealed record ReportAccountOption(Guid Id, string Name, string Status, string? Username);

/// <summary>Последний запуск по расписанию: когда, итог и счётчики отправок.</summary>
public sealed record ReportRunBrief(Guid Id, DateOnly Period, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, string Status, int Planned, int Sent, int Failed,
    int Skipped, string? Error);

/// <summary>Запуск отчёта в расписании: что за отчёт, кому, время «13:00», дни 1–7 (пн–вс), пояс, догон, следующий и последний запуск.</summary>
public sealed record ReportJobView(
    string Code,
    string Report,
    string ReportTitle,
    string Slot,
    string SlotTitle,
    IReadOnlyList<string> Roles,
    bool Enabled,
    string Time,
    string TimeZone,
    IReadOnlyList<int> Days,
    int CatchUpMinutes,
    DateTimeOffset? NextRunAt,
    ReportRunBrief? LastRun,
    int Recipients,
    DateTimeOffset? UpdatedAt);

public sealed record ReportJobInput(bool Enabled, string? Time, string? TimeZone, IReadOnlyList<int>? Days, int? CatchUpMinutes);

/// <summary>Чат получателя. Linked — личный чат человека, чей Telegram подтверждён кодом привязки в OneBase (имя пользователя OneBase).</summary>
public sealed record ReportChatRef(Guid Id, string Title, string Type, bool CanSend, long PeerId, string? Linked);

/// <summary>Последняя отправка получателю — для строки списка.</summary>
public sealed record ReportDeliveryBrief(string Report, string Chat, string Status, DateTimeOffset At, string? Error);

/// <summary>
/// Получатель: роль, область («Ташкент · СВ Азиз Каримов»), чаты, привязка Telegram (linked — подтверждена кодом, chat — есть личный чат,
/// group — только группы, blocked — писать нельзя, none — чатов нет), запуски, на которые подписан. Problem — почему сейчас отправить
/// нельзя (область пропала из данных, нет чатов, в чат писать нельзя): строка сохранена, но отчёт не уйдёт, пока причина не устранена.
/// </summary>
public sealed record ReportRecipientView(
    Guid Id,
    string Role,
    string Name,
    string Scope,
    Guid? DirectionId,
    Guid? RegionId,
    string? Region,
    long? SupervisorId,
    long? AgentId,
    int Agents,
    IReadOnlyList<ReportChatRef> Chats,
    string TelegramStatus,
    IReadOnlyList<string> Reports,
    bool Enabled,
    string? Problem,
    IReadOnlyList<ReportDeliveryBrief> Last);

public sealed record ReportRecipientInput(
    string? Role,
    string? Title,
    Guid? DirectionId,
    Guid? RegionId,
    long? SupervisorId,
    long? AgentId,
    IReadOnlyList<Guid>? ChatIds,
    IReadOnlyList<string>? Reports,
    bool Enabled);

public sealed record ReportPersonOption(long Id, string Name);

/// <summary>Регион OneBase: направление и РМ, «СВР региона», супервайзеры и ТП месяца.</summary>
public sealed record ReportRegionOption(Guid Id, string Name, Guid? DirectionId, string? Manager, string? SupervisorName, IReadOnlyList<ReportPersonOption> Supervisors,
    IReadOnlyList<ReportPersonOption> Agents);

public sealed record ReportDirectionOption(Guid Id, string Name, string? Manager, int Regions);

/// <summary>Чат аккаунта для выбора получателя; LinkedUser — пользователь OneBase, чей Telegram подтверждён кодом и чей это личный чат.</summary>
public sealed record ReportChatOption(Guid Id, string Title, string Type, string? Username, long PeerId, bool CanSend, string? Reason, string? LinkedUser);

/// <summary>Отправки за 7 дней — для плитки мониторинга.</summary>
public sealed record ReportMetrics(int Sent, int Failed, int Skipped, int Unknown, int Pending);

public sealed record ReportOverview(
    ReportSettingsView Settings,
    IReadOnlyList<ReportAccountOption> Accounts,
    IReadOnlyList<ReportJobView> Jobs,
    IReadOnlyList<ReportRecipientView> Recipients,
    IReadOnlyList<ReportDirectionOption> Directions,
    IReadOnlyList<ReportRegionOption> Regions,
    IReadOnlyList<ReportChatOption> Chats,
    bool CanUseSales,
    bool TelegramEnabled,
    ReportMetrics Metrics);

/// <summary>
/// Предпросмотр и тест: запуск, роль и область (или получатель), день и время формирования (по умолчанию — сейчас), язык (по умолчанию — из
/// настроек).
/// </summary>
public sealed record ReportPreviewInput(
    string? Job,
    string? Role,
    Guid? DirectionId,
    Guid? RegionId,
    long? SupervisorId,
    long? AgentId,
    Guid? RecipientId,
    DateOnly? Date,
    string? Time,
    string? Language);

public sealed record ReportPreview(IReadOnlyList<string> Pages, string Title, string Scope, string Caption, string Summary, IReadOnlyList<string> Notes,
    IReadOnlyList<string> Problems, long Bytes, bool Applies, string? SkipReason, bool Incomplete);

public sealed record ReportTestInput(ReportPreviewInput Report, Guid ChatId);

public sealed record ReportSendInput(string? Job, bool Force);

/// <summary>Итог отправки в один чат: sent, pending (временная ошибка — повтор автоматически), failed, skipped, unknown.</summary>
public sealed record ReportSendResult(Guid DeliveryId, string Status, string RecipientTitle, string ScopeTitle, string? Error, int Pages, DateTimeOffset? SentAt,
    DateTimeOffset? NextAttemptAt);

public sealed record ReportDeliveryView(
    Guid Id,
    DateTimeOffset CreatedAt,
    string Report,
    string ReportTitle,
    DateOnly Period,
    string Trigger,
    string? Role,
    string RecipientTitle,
    string ScopeTitle,
    Guid? RegionId,
    string Status,
    int Attempts,
    DateTimeOffset? SentAt,
    DateTimeOffset? NextAttemptAt,
    string? Error,
    int Pages,
    long Bytes,
    string? Summary,
    DateTimeOffset? DataAsOf,
    int? DurationMs,
    string? By,
    bool CanRetry);

public sealed record ReportRunView(Guid Id, string Job, string Title, DateOnly Period, DateTimeOffset ScheduledAt, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt,
    string Status, int Planned, int Sent, int Failed, int Skipped, string? Error, DateTimeOffset? DataAsOf);

public sealed record ReportHistoryQuery(DateOnly? From, DateOnly? To, string? Report, Guid? RegionId, string? Status, int Page = 1);

public sealed record ReportHistory(IReadOnlyList<ReportDeliveryView> Items, int Total, int Page, int PageSize, IReadOnlyList<ReportRunView> Runs);
