namespace OneBase.Application.Finance;

/// <summary>
/// Настройки интеграции 1С в OneBase (строка «onec» в Integrations, отдел «Финансы»). Учётные данные 1С здесь не хранятся — они только
/// у службы интеграции (переменные окружения). Ключ службы — только SHA-256 хэш и последние символы для подсказки.
/// </summary>
public sealed record OneCSettings
{
    public const string Code = "onec";
    public const string Department = "finance";
    public const int MinInterval = 5;
    public const int MaxInterval = 1440;
    public const int DefaultInterval = 30;
    public const int MinBatch = 50;
    public const int MaxBatch = 2000;
    public const int DefaultBatch = 500;

    /// <summary>Автоматическая синхронизация приостановлена администратором.</summary>
    public bool Paused { get; init; }

    public int IntervalMinutes { get; init; } = DefaultInterval;
    public int BatchSize { get; init; } = DefaultBatch;

    /// <summary>AI-консультант может читать данные 1С (данные уходят во внешнюю модель) — по умолчанию выключено.</summary>
    public bool AiEnabled { get; init; }

    /// <summary>База 1С, данные которой лежат в OneBase (ping: infobaseId). Другая база — загрузка остановлена до решения администратора.</summary>
    public string? AcceptedInfobaseId { get; init; }

    public string? AcceptedInfobaseName { get; init; }

    /// <summary>Служба подключилась к другой базе 1С: загрузка остановлена до решения администратора.</summary>
    public string? PendingInfobaseId { get; init; }

    public string? PendingInfobaseName { get; init; }

    /// <summary>Тестовый источник (демо-данные): показывается пометкой на экранах финансов.</summary>
    public bool DemoData { get; init; }

    public bool HasToken { get; init; }
    public string? TokenHint { get; init; }
    public DateTimeOffset? TokenCreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }

    public static int ClampInterval(int value) => Math.Clamp(value, MinInterval, MaxInterval);

    public static int ClampBatch(int value) => Math.Clamp(value, MinBatch, MaxBatch);
}

/// <summary>Изменение настроек: null — не менять.</summary>
public sealed record OneCSettingsChange(bool? Paused = null, int? IntervalMinutes = null, int? BatchSize = null, bool? AiEnabled = null);

public interface IOneCSettingsStore
{
    Task<OneCSettings> GetAsync(CancellationToken ct = default);

    Task<OneCSettings> SaveAsync(OneCSettingsChange change, Guid userId, CancellationToken ct = default);

    /// <summary>Новый ключ службы: показывается один раз, в базе — только хэш. Прежний ключ перестаёт действовать.</summary>
    Task<string> CreateTokenAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Сверка ключа службы (сравнение хэшей за постоянное время).</summary>
    Task<bool> VerifyTokenAsync(string? token, CancellationToken ct = default);

    /// <summary>Принять базу 1С (первое подключение или после очистки данных прежней базы).</summary>
    Task AcceptInfobaseAsync(string? infobaseId, string? infobaseName, bool demo, CancellationToken ct = default);

    /// <summary>Запомнить другую базу 1С, к которой подключилась служба (показывается администратору).</summary>
    Task NotePendingInfobaseAsync(string infobaseId, string? infobaseName, CancellationToken ct = default);
}
