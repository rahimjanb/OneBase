using OneBase.Domain.Common;

namespace OneBase.Domain.AI;

/// <summary>
/// Чат администратора с AI Data Manager: Claude разбирает загруженные отчёты и готовит изменения данных OneBase.
/// Виден только своему автору.
/// </summary>
public class DataConversation : Entity
{
    public Guid UserId { get; set; }

    public required string Title { get; set; }

    public DateTimeOffset LastMessageAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ArchivedAt { get; set; }

    /// <summary>
    /// Переписка с моделью в формате Messages API (текст, рассуждения, вызовы инструментов и их результаты) — JSON.
    /// Только дописывается: следующий запрос продолжает её с того же места.
    /// </summary>
    public string Transcript { get; set; } = "[]";

    /// <summary>Идёт ли сейчас ход модели: второй запрос в тот же чат ждёт окончания первого.</summary>
    public DateTimeOffset? BusySince { get; set; }

    public uint Version { get; set; }

    public List<DataMessage> Messages { get; set; } = [];
}

public enum DataMessageRole
{
    User,
    Assistant,

    /// <summary>Действие администратора или системы: файл загружен, план подтверждён, откат.</summary>
    Event,
}

public enum DataMessageStatus
{
    Completed,
    Failed,
}

/// <summary>Сообщение чата AI Data Manager — то, что видит администратор.</summary>
public class DataMessage : Entity
{
    public Guid ConversationId { get; set; }
    public DataConversation? Conversation { get; set; }

    public DataMessageRole Role { get; set; }
    public DataMessageStatus Status { get; set; } = DataMessageStatus.Completed;
    public required string Content { get; set; }

    /// <summary>Вызовы инструментов за ход — JSON-массив шагов (название, итог, ошибка).</summary>
    public string? Steps { get; set; }

    /// <summary>Файлы, приложенные к сообщению пользователя (id через запятую).</summary>
    public string? FileIds { get; set; }

    /// <summary>Планы изменений, подготовленные за этот ход (id через запятую).</summary>
    public string? ChangeSetIds { get; set; }

    public string? Error { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public long DurationMs { get; set; }
}

/// <summary>Отчёт, загруженный в AI Data Manager: сам файл — в хранилище, здесь — метаданные и отпечаток.</summary>
public class DataFile : Entity
{
    public Guid UserId { get; set; }
    public Guid? ConversationId { get; set; }

    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }

    /// <summary>SHA-256 содержимого (hex): один и тот же файл узнаётся при повторной загрузке.</summary>
    public required string Sha256 { get; set; }

    public required string ObjectKey { get; set; }

    /// <summary>Листы и размеры таблиц — JSON для подсказки модели и экрана.</summary>
    public string? Summary { get; set; }
}

public enum DataChangeSetStatus
{
    /// <summary>Подготовлен, ждёт подтверждения администратора.</summary>
    Draft,

    /// <summary>Применяется (ставится на время транзакции).</summary>
    Applying,

    Applied,

    /// <summary>Данные изменились после подготовки — план нужно подготовить заново.</summary>
    Stale,

    Cancelled,
    Failed,
    RolledBack,

    /// <summary>Откачен частично: часть строк с тех пор изменили другие — их значения сохранены.</summary>
    PartiallyRolledBack,
}

/// <summary>
/// План изменений данных OneBase: что создать, изменить, удалить и что пропустить — со значениями до и после.
/// Применяется только после подтверждения администратором, целиком в одной транзакции.
/// </summary>
public class DataChangeSet : Entity
{
    public Guid UserId { get; set; }
    public Guid? ConversationId { get; set; }

    /// <summary>Отчёт, из которого взяты значения (null — значения из сообщения администратора).</summary>
    public Guid? FileId { get; set; }
    public string? FileSha256 { get; set; }

    /// <summary>Что меняется: region_plans, sales_targets, region_profiles.</summary>
    public required string Target { get; set; }

    public required string Title { get; set; }

    /// <summary>Период изменений: «2026-10» или null.</summary>
    public string? Period { get; set; }

    /// <summary>Вид плана (Rop, Factory, Primary) — у планов регионов.</summary>
    public string? PlanKind { get; set; }

    /// <summary>
    /// Привязка Google Sheets, синхронизация которой подготовила план (null — чат или файл администратора). По ней при применении
    /// проверяется владелец поля: поле, которое ведёт привязка, меняет только она.
    /// </summary>
    public Guid? SourceBindingId { get; set; }

    /// <summary>upsert — только строки отчёта; replace — отчёт полный за период, строк вне отчёта быть не должно.</summary>
    public required string Mode { get; set; }

    public DataChangeSetStatus Status { get; set; } = DataChangeSetStatus.Draft;

    /// <summary>Счётчики, предупреждения, оценка влияния на KPI — JSON.</summary>
    public string Summary { get; set; } = "{}";

    /// <summary>Отпечаток исходных параметров (файл, лист, сопоставление): тот же запрос не плодит одинаковые планы.</summary>
    public required string RequestHash { get; set; }

    public DateTimeOffset? AppliedAt { get; set; }
    public Guid? AppliedById { get; set; }

    /// <summary>Проверка после применения: сохранённые значения и пересчитанные показатели — JSON.</summary>
    public string? Verification { get; set; }

    public DateTimeOffset? RolledBackAt { get; set; }
    public Guid? RolledBackById { get; set; }

    public string? Error { get; set; }

    public uint Version { get; set; }

    public List<DataChangeItem> Items { get; set; } = [];
}

public enum DataChangeAction
{
    Create,
    Update,
    Delete,

    /// <summary>Значение уже такое же — строка пропускается.</summary>
    Unchanged,

    /// <summary>Строку нельзя применить: регион или категория не найдены, дубликат с другим значением и т.п.</summary>
    Conflict,
}

public enum DataChangeItemStatus
{
    Pending,
    Applied,
    Skipped,
    RolledBack,

    /// <summary>При откате значение уже изменил кто-то другой — откат строки пропущен.</summary>
    RollbackConflict,
}

/// <summary>Одна строка плана изменений: бизнес-ключ, действие, значения до и после.</summary>
public class DataChangeItem : Entity
{
    public Guid ChangeSetId { get; set; }
    public DataChangeSet? ChangeSet { get; set; }

    public int Position { get; set; }

    /// <summary>region_plan, sales_target, region_profile.</summary>
    public required string EntityType { get; set; }

    /// <summary>Бизнес-ключ строки: по нему ищется существующая запись.</summary>
    public required string EntityKey { get; set; }

    /// <summary>Подпись для экрана: «Хоразм · Кекс · октябрь 2026 · план РОП».</summary>
    public required string Label { get; set; }

    public DataChangeAction Action { get; set; }

    public string? Before { get; set; }
    public string? After { get; set; }

    /// <summary>Откуда значение: «Лист1, строка 12» или «из сообщения».</summary>
    public string? SourceRef { get; set; }

    public string? Message { get; set; }

    public DataChangeItemStatus Status { get; set; } = DataChangeItemStatus.Pending;
}
