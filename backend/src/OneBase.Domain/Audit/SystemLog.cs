namespace OneBase.Domain.Audit;

/// <summary>Откуда запись журнала ошибок.</summary>
public static class SystemLogSources
{
    /// <summary>Интеграция Linko: подключение, токен, ответы и доступность API Linko.</summary>
    public const string Linko = "linko";

    /// <summary>Синхронизация данных Linko в OneBase: шаги загрузки, отмена.</summary>
    public const string Sync = "sync";

    /// <summary>Всё остальное: ошибки сервера OneBase.</summary>
    public const string System = "system";
}

/// <summary>
/// Запись журнала ошибок («Настройки → Журнал ошибок», только администратор): предупреждения и ошибки Linko и синхронизации,
/// ошибки сервера. Секреты из текста вырезаются при записи; хранится 90 дней.
/// </summary>
public class SystemLog
{
    public long Id { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Warning | Error | Critical.</summary>
    public required string Level { get; set; }

    /// <summary>SystemLogSources: linko | sync | system.</summary>
    public required string Source { get; set; }

    /// <summary>Где возникло (категория логгера, например OneBase.Infrastructure.Linko.LinkoSyncService).</summary>
    public required string Category { get; set; }

    public required string Message { get; set; }

    /// <summary>Тип, текст и стек исключения (с вложенными).</summary>
    public string? Exception { get; set; }

    /// <summary>Идентификатор запроса — чтобы найти связанные записи.</summary>
    public string? TraceId { get; set; }
}
