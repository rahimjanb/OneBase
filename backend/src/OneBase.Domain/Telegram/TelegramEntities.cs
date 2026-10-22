using OneBase.Domain.Common;

namespace OneBase.Domain.Telegram;

public enum TelegramAccountStatus
{
    /// <summary>Создан, вход не начат.</summary>
    New,

    /// <summary>Telegram отправил код — ждём его.</summary>
    CodeRequired,

    /// <summary>Включена двухэтапная защита — ждём пароль.</summary>
    PasswordRequired,

    Connected,

    /// <summary>Отключён администратором или сессия отозвана в Telegram.</summary>
    Disconnected,

    Error,
}

/// <summary>
/// Подключённый пользовательский аккаунт Telegram (MTProto). Сессия и номер телефона — только зашифрованные (ASP.NET Data Protection);
/// коды и пароль не сохраняются вовсе. Сессия наружу не отдаётся.
/// </summary>
public class TelegramAccount : Entity
{
    public required string Name { get; set; }

    /// <summary>Номер телефона, зашифрованный; в интерфейсе — только PhoneHint (последние цифры).</summary>
    public string? ProtectedPhone { get; set; }

    public string? PhoneHint { get; set; }

    public TelegramAccountStatus Status { get; set; } = TelegramAccountStatus.New;

    public long? TelegramUserId { get; set; }
    public string? Username { get; set; }
    public string? DisplayName { get; set; }

    /// <summary>Сессия MTProto, зашифрованная (base64 зашифрованных байтов).</summary>
    public string? ProtectedSession { get; set; }

    /// <summary>Состояние получения обновлений (UpdateManager) — чтобы после перезапуска не пропустить и не повторить сообщения.</summary>
    public string? UpdateState { get; set; }

    public DateTimeOffset? ConnectedAt { get; set; }
    public DateTimeOffset? LastActivityAt { get; set; }
    public string? LastError { get; set; }

    public Guid CreatedById { get; set; }
}

/// <summary>Настройки AI-агента аккаунта (одна строка на аккаунт). Применяются сразу, без пересборки.</summary>
public class TelegramAgentSettings
{
    public Guid AccountId { get; set; }

    public bool Enabled { get; set; }

    /// <summary>Отвечать на входящие автоматически.</summary>
    public bool AutoReply { get; set; } = true;

    /// <summary>Отвечать тем, кого нет в списке пользователей (только по отправленным им материалам).</summary>
    public bool ReplyToUnknown { get; set; }

    /// <summary>Модель ответов (провайдер и модель из «Настройки → AI»); пусто — основная.</summary>
    public string? ModelProvider { get; set; }
    public string? Model { get; set; }

    /// <summary>
    /// Модель генерации — когда просят сформировать отчёт, документ, письмо, таблицу (Claude). Пусто — модель Claude из AI Data Manager,
    /// если ключ Anthropic подключён, иначе модель ответов.
    /// </summary>
    public string? GenerationModelProvider { get; set; }
    public string? GenerationModel { get; set; }

    /// <summary>Инструкция агента.</summary>
    public string? SystemPrompt { get; set; }

    /// <summary>Ограничения обычным текстом — дополняют, но не заменяют проверки прав на сервере.</summary>
    public string? Restrictions { get; set; }

    /// <summary>short, normal, detailed.</summary>
    public string ResponseStyle { get; set; } = "normal";

    /// <summary>auto (на языке вопроса), ru, uz, en.</summary>
    public string Language { get; set; } = "auto";

    /// <summary>Сообщений, на которые агент отвечает одному пользователю за час.</summary>
    public int MaxMessagesPerHour { get; set; } = 30;

    public int MaxOutputTokens { get; set; } = 1500;

    /// <summary>Пауза перед ответом, секунд.</summary>
    public int ReplyDelaySeconds { get; set; } = 2;

    /// <summary>Что ответить, если подходящих данных нет.</summary>
    public string NoInfoReply { get; set; } = "У меня нет подтверждённой информации по этому вопросу.";

    /// <summary>Расширения файлов, которые агент принимает (через запятую).</summary>
    public string AllowedAttachments { get; set; } = "pdf,xlsx,csv,docx,txt";

    public int MaxFileMb { get; set; } = 20;

    /// <summary>Агенту разрешено формировать и отправлять отчёты (пользователю — ещё и его разрешение).</summary>
    public bool AllowReports { get; set; } = true;

    /// <summary>
    /// Пользователь OneBase, с чьими правами агент отвечает тем, кому дан полный доступ (если у строки пользователя не указан свой).
    /// </summary>
    public Guid? FullAccessUserId { get; set; }

    /// <summary>Кем агент представляется («сотрудник компании Candy Gold…»); пусто — стандартное представление.</summary>
    public string? Persona { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedById { get; set; }
}

public enum TelegramAccessMode
{
    /// <summary>Ответы по данным OneBase с правами привязанного пользователя OneBase (личность подтверждена кодом).</summary>
    Full,

    /// <summary>Только по материалам, отправленным в этот чат через OneBase.</summary>
    SentOnly,
}

/// <summary>
/// Пользователь Telegram в списке агента. Ключ — Telegram user ID (username меняется и не даёт прав). Полный доступ действует, только
/// если указан пользователь OneBase и личность подтверждена (VerifiedAt): человек прислал агенту код из своего профиля OneBase.
/// </summary>
public class TelegramUserPermission : Entity
{
    public Guid AccountId { get; set; }
    public TelegramAccount? Account { get; set; }

    /// <summary>Telegram user ID — основной идентификатор; до подтверждения может быть пустым (добавлен по username).</summary>
    public long? TelegramUserId { get; set; }

    public string? Username { get; set; }
    public string? Title { get; set; }

    public TelegramAccessMode Mode { get; set; } = TelegramAccessMode.SentOnly;

    /// <summary>Пользователь OneBase, чьи права наследует полный доступ.</summary>
    public Guid? UserId { get; set; }

    public bool AllowReports { get; set; }
    public bool Enabled { get; set; } = true;

    /// <summary>Когда личность подтверждена кодом привязки.</summary>
    public DateTimeOffset? VerifiedAt { get; set; }

    /// <summary>Чат консультанта OneBase, в котором идёт переписка этого пользователя (история ответов).</summary>
    public Guid? ConversationId { get; set; }

    public Guid CreatedById { get; set; }
    public Guid? UpdatedById { get; set; }
}

/// <summary>Одноразовый код привязки Telegram к пользователю OneBase: хранится только хэш, действует 15 минут.</summary>
public class TelegramLinkCode : Entity
{
    public Guid UserId { get; set; }
    public required string CodeHash { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public Guid? AccountId { get; set; }
    public long? TelegramUserId { get; set; }
}

public enum TelegramChatType
{
    Private,
    Group,
    Channel,
}

/// <summary>
/// Чат, доступный аккаунту (из его диалогов): личный, группа или канал. AccessHash нужен Telegram для отправки (не секрет входа).
/// В группе агент отвечает, только если он включён для неё (AgentEnabled).
/// </summary>
public class TelegramChat : Entity
{
    public Guid AccountId { get; set; }
    public long PeerId { get; set; }
    public TelegramChatType Type { get; set; }

    /// <summary>Супергруппа или канал (InputPeerChannel), иначе — обычная группа или пользователь.</summary>
    public bool IsChannelPeer { get; set; }

    public long AccessHash { get; set; }
    public required string Title { get; set; }
    public string? Username { get; set; }

    /// <summary>Аккаунт может писать в чат (не канал только для чтения, не забанен).</summary>
    public bool CanSend { get; set; } = true;

    /// <summary>Почему писать нельзя (для интерфейса): канал без прав администратора, аккаунт вышел, в группе запрещено писать…</summary>
    public string? CannotSendReason { get; set; }

    public bool AgentEnabled { get; set; }
    public DateTimeOffset? SyncedAt { get; set; }
}

/// <summary>Материал, отправленный в чат через OneBase (сообщение, файл, отчёт), — источник ответов в режиме «Только отправленные данные».</summary>
public class TelegramMaterial : Entity
{
    public Guid AccountId { get; set; }
    public long PeerId { get; set; }

    /// <summary>message, file, report.</summary>
    public required string Kind { get; set; }

    public required string Title { get; set; }
    public string? FileName { get; set; }
    public string? ObjectKey { get; set; }
    public long? SizeBytes { get; set; }
    public Guid? CampaignId { get; set; }
    public Guid? CreatedById { get; set; }

    /// <summary>Текст удалось извлечь (иначе материал есть, но отвечать по нему нельзя).</summary>
    public bool HasText { get; set; }
}

/// <summary>Фрагмент текста материала: поиск идёт только по фрагментам своего аккаунта и чата.</summary>
public class TelegramMaterialChunk
{
    public long Id { get; set; }
    public Guid MaterialId { get; set; }
    public Guid AccountId { get; set; }
    public long PeerId { get; set; }
    public int Position { get; set; }
    public required string Text { get; set; }
}

/// <summary>Входящее сообщение (метаданные): по уникальному ключу аккаунт × чат × сообщение повторная доставка не обрабатывается дважды.</summary>
public class TelegramIncomingMessage : Entity
{
    public Guid AccountId { get; set; }
    public long PeerId { get; set; }
    public int MessageId { get; set; }
    public long? FromUserId { get; set; }
    public DateTimeOffset SentAt { get; set; }

    /// <summary>received, processed, ignored.</summary>
    public string Status { get; set; } = "received";

    public string? Reason { get; set; }
}

/// <summary>Работа агента над сообщением: режим, итог, вопрос и ответ (для журнала администратора), отчёт, модель, токены.</summary>
public class TelegramAgentExecution : Entity
{
    public Guid AccountId { get; set; }
    public Guid? IncomingMessageId { get; set; }
    public long PeerId { get; set; }
    public long? TelegramUserId { get; set; }

    /// <summary>full, sent_only, link.</summary>
    public required string Mode { get; set; }

    /// <summary>answered, no_info, refused, rate_limited, error, linked.</summary>
    public required string Status { get; set; }

    public string? Question { get; set; }
    public string? Answer { get; set; }
    public Guid? ReportId { get; set; }
    public string? Model { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public long DurationMs { get; set; }
    public string? Error { get; set; }
    public int? ReplyMessageId { get; set; }
}

public enum TelegramCampaignStatus
{
    Draft,
    Scheduled,
    Queued,
    Sending,
    Completed,
    CompletedWithErrors,
    Paused,
    Cancelled,
    Failed,
}

/// <summary>Рассылка: аккаунт, текст, вложения (зафиксированы при добавлении), получатели со своими статусами, расписание и скорость.</summary>
public class TelegramCampaign : Entity
{
    public Guid AccountId { get; set; }
    public TelegramAccount? Account { get; set; }

    public required string Name { get; set; }
    public string? Description { get; set; }
    public TelegramCampaignStatus Status { get; set; } = TelegramCampaignStatus.Draft;

    public string? Text { get; set; }

    /// <summary>Текст с разметкой (жирный, курсив, ссылки — Markdown Telegram).</summary>
    public bool Formatted { get; set; }

    /// <summary>Когда отправлять (UTC); null — сразу после запуска.</summary>
    public DateTimeOffset? ScheduledAt { get; set; }

    public string TimeZone { get; set; } = "Asia/Tashkent";

    /// <summary>Пауза между отправками одного аккаунта, секунд.</summary>
    public int DelaySeconds { get; set; } = 5;

    public Guid CreatedById { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? LastError { get; set; }

    public List<TelegramCampaignRecipient> Recipients { get; set; } = [];
    public List<TelegramCampaignAttachment> Attachments { get; set; } = [];
}

public enum TelegramRecipientStatus
{
    Pending,
    Sending,
    Sent,
    Failed,
    Cancelled,
}

/// <summary>
/// Получатель рассылки: один чат — одна строка (уникально по рассылке и чату). Отправленный не отправляется повторно; «Повторить ошибки»
/// возвращает в очередь только ошибочные.
/// </summary>
public class TelegramCampaignRecipient : Entity
{
    public Guid CampaignId { get; set; }
    public TelegramCampaign? Campaign { get; set; }

    public Guid ChatId { get; set; }
    public long PeerId { get; set; }
    public required string Title { get; set; }
    public string? Username { get; set; }
    public TelegramChatType Type { get; set; }

    public TelegramRecipientStatus Status { get; set; } = TelegramRecipientStatus.Pending;
    public int Attempts { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public string? Error { get; set; }

    /// <summary>ID сообщений Telegram через запятую (текст и файлы).</summary>
    public string? MessageIds { get; set; }
}

/// <summary>Файл рассылки: загружен в хранилище OneBase или отчёт, зафиксированный файлом при добавлении (не меняется во время рассылки).</summary>
public class TelegramCampaignAttachment : Entity
{
    public Guid CampaignId { get; set; }
    public TelegramCampaign? Campaign { get; set; }

    /// <summary>file или report.</summary>
    public required string Kind { get; set; }

    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public required string ObjectKey { get; set; }
    public required string Sha256 { get; set; }
    public Guid? ReportId { get; set; }
    public int Position { get; set; }
}
