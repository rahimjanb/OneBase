using OneBase.Domain.Common;

namespace OneBase.Domain.AI;

/// <summary>
/// Модель провайдера AI. Список загружается из API провайдера (GET /models), администратор выбирает,
/// какие модели можно назначать консультанту и агентам.
/// </summary>
public class AiModel : Entity
{
    public required string ProviderCode { get; set; }

    /// <summary>Идентификатор модели у провайдера — то, что уходит в поле "model" запроса.</summary>
    public required string ModelId { get; set; }

    public string? DisplayName { get; set; }

    /// <summary>Модель есть в последнем списке провайдера.</summary>
    public bool Available { get; set; } = true;

    /// <summary>Разрешена для выбора в настройках AI.</summary>
    public bool Enabled { get; set; }

    /// <summary>Цена 1 млн входных токенов, USD. API провайдеров цен не отдаёт — вводит администратор.</summary>
    public decimal? InputPricePerMillion { get; set; }

    /// <summary>Цена 1 млн выходных токенов, USD.</summary>
    public decimal? OutputPricePerMillion { get; set; }

    /// <summary>Дата выпуска модели по данным провайдера.</summary>
    public DateTimeOffset? ProviderCreatedAt { get; set; }
}
