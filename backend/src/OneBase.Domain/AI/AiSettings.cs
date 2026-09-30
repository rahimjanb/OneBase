namespace OneBase.Domain.AI;

/// <summary>Общие настройки AI: основной и резервный провайдер, модели для задач, параметры генерации. Одна строка.</summary>
public class AiSettings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    /// <summary>AI включён для пользователей.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Основная модель: консультант и агенты, у которых своя модель не задана.</summary>
    public string? PrimaryProvider { get; set; }
    public string? PrimaryModel { get; set; }

    /// <summary>Резерв: вызывается, если основной провайдер вернул ошибку или не ответил.</summary>
    public string? FallbackProvider { get; set; }
    public string? FallbackModel { get; set; }

    /// <summary>Быстрая модель для маршрутизации вопросов; пусто — основная.</summary>
    public string? RouterProvider { get; set; }
    public string? RouterModel { get; set; }

    /// <summary>Модель эмбеддингов для базы знаний (семантический поиск).</summary>
    public string? EmbeddingProvider { get; set; }
    public string? EmbeddingModel { get; set; }

    /// <summary>Температура по умолчанию; пусто — значение провайдера.</summary>
    public double? Temperature { get; set; }

    public int MaxOutputTokens { get; set; } = 4096;

    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedById { get; set; }
}
