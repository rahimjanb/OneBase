using OneBase.Domain.Common;

namespace OneBase.Domain.AI;

public enum AiAlertSeverity
{
    Critical,
    Warning,

    /// <summary>Возможность: где можно заработать больше.</summary>
    Opportunity,
}

/// <summary>
/// Находка проактивного анализа: снижение продаж, риск невыполнения плана, заканчивающиеся запасы, возможность роста.
/// Одна находка — один ключ: повторная проверка обновляет её, а когда условие перестаёт выполняться, находка закрывается.
/// </summary>
public class AiAlert : Entity
{
    /// <summary>Отдел: sales, supply, finance, marketing, hr, production.</summary>
    public required string Category { get; set; }

    /// <summary>Правило, которое нашло: sales.region.drop, supply.stock.deficit…</summary>
    public required string Rule { get; set; }

    /// <summary>Ключ находки — правило, объект и период.</summary>
    public required string Key { get; set; }

    public AiAlertSeverity Severity { get; set; }
    public required string Title { get; set; }
    public required string Message { get; set; }

    /// <summary>Рекомендация, следующая из найденного (не из модели).</summary>
    public string? Recommendation { get; set; }

    /// <summary>Страница OneBase с данными находки.</summary>
    public string? Href { get; set; }

    /// <summary>Право, без которого находка пользователю не показывается.</summary>
    public string? RequiredPermission { get; set; }

    public DateTimeOffset DetectedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }
}
