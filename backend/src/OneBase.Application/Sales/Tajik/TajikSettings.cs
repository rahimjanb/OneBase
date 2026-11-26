namespace OneBase.Application.Sales.Tajik;

/// <summary>
/// Подключение к API 1С дилера в Таджикистане (строка «tj1c» в Integrations, отдел «Продажи»): адрес, ключ доступа (зашифрован, наружу —
/// только последние символы), включение, интервал и с какой даты загружать историю. Ключ можно задать и переменной TajikSales__Token —
/// сохранённый в OneBase главнее.
/// </summary>
public sealed record TajikSettings
{
    public const string Code = "tj1c";
    public const string Department = "sales";
    public const int MinInterval = 10;
    public const int MaxInterval = 1440;
    public const int DefaultInterval = 30;

    /// <summary>Данные в 1С Таджикистана — с 03.11.2026; API без даты отдаёт с 01.11.2026.</summary>
    public static readonly DateOnly DefaultHistoryFrom = new(2026, 11, 1);

    public string BaseUrl { get; init; } = string.Empty;
    public bool HasToken { get; init; }
    public string? TokenHint { get; init; }

    /// <summary>onebase — ключ сохранён на странице; env — из переменной окружения; none — не задан.</summary>
    public string TokenSource { get; init; } = "none";

    public bool Enabled { get; init; } = true;
    public int IntervalMinutes { get; init; } = DefaultInterval;
    public DateOnly HistoryFrom { get; init; } = DefaultHistoryFrom;
    public DateTimeOffset? UpdatedAt { get; init; }
    public DateTimeOffset? LastTestAt { get; init; }
    public bool? LastTestOk { get; init; }
    public string? LastTestMessage { get; init; }

    public bool Configured => BaseUrl.Length > 0 && HasToken;

    public bool IsReady => Enabled && Configured;

    /// <summary>Адрес без HTTPS: ключ и данные идут по сети открытым текстом — показывается предупреждение.</summary>
    public bool Insecure => BaseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase);

    public static int ClampInterval(int value) => Math.Clamp(value, MinInterval, MaxInterval);
}

/// <summary>Изменение настроек: null — не менять. Token: "" — удалить сохранённый в OneBase (останется переменная окружения, если задана).</summary>
public sealed record TajikSettingsChange(string? BaseUrl = null, string? Token = null, bool? Enabled = null, int? IntervalMinutes = null, DateOnly? HistoryFrom = null);

public interface ITajikSettingsStore
{
    Task<TajikSettings> GetAsync(CancellationToken ct = default);

    Task<TajikSettings> SaveAsync(TajikSettingsChange change, Guid userId, CancellationToken ct = default);

    /// <summary>Ключ доступа (расшифрованный) — только для клиента API; null — не задан.</summary>
    Task<string?> TokenAsync(CancellationToken ct = default);

    Task SaveTestResultAsync(bool ok, string message, CancellationToken ct = default);
}

/// <summary>Сигнал «данные или регионы Таджикистана изменились» — сбрасывает справочник товаров (вес единицы, категории).</summary>
public sealed class TajikDataVersion
{
    private long _value;

    public long Value => Interlocked.Read(ref _value);

    public void Bump() => Interlocked.Increment(ref _value);
}
