namespace OneBase.Domain.AI;

/// <summary>
/// Подключение к провайдеру AI (OpenAI, Anthropic). Ключ API хранится только зашифрованным (ASP.NET Data Protection)
/// и наружу не отдаётся — в интерфейсе видны лишь последние символы.
/// </summary>
public class AiProvider
{
    /// <summary>Код провайдера: "openai", "anthropic".</summary>
    public required string Code { get; set; }

    public bool Enabled { get; set; } = true;

    /// <summary>Свой адрес API (прокси, совместимый сервер); пусто — адрес провайдера по умолчанию.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Зашифрованный ключ API.</summary>
    public string? ProtectedApiKey { get; set; }

    /// <summary>Последние символы ключа — чтобы было видно, какой ключ сохранён.</summary>
    public string? ApiKeyHint { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedById { get; set; }

    public DateTimeOffset? LastTestAt { get; set; }
    public bool? LastTestOk { get; set; }
    public string? LastTestMessage { get; set; }

    /// <summary>Когда список моделей последний раз загружался у провайдера.</summary>
    public DateTimeOffset? ModelsRefreshedAt { get; set; }
}
