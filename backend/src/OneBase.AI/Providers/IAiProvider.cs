using System.Text.Json;
using System.Text.RegularExpressions;
using OneBase.AI.Llm;

namespace OneBase.AI.Providers;

/// <summary>Ключ и адрес, с которыми вызывается провайдер. Живёт только в памяти backend.</summary>
public sealed record AiCredentials(string ApiKey, string BaseUrl);

public sealed record AiTokenUsage(int InputTokens, int OutputTokens)
{
    public static readonly AiTokenUsage None = new(0, 0);

    public AiTokenUsage Add(AiTokenUsage other) => new(InputTokens + other.InputTokens, OutputTokens + other.OutputTokens);
}

/// <summary>Запрос генерации в провайдер-независимом виде.</summary>
public sealed record AiCompletionRequest(
    string Model,
    IReadOnlyList<LlmMessage> Messages,
    IReadOnlyList<LlmToolDefinition> Tools,
    double? Temperature,
    int MaxOutputTokens,
    bool JsonOutput = false,
    string? ReasoningEffort = null);

public sealed record AiCompletion(
    string? Text,
    IReadOnlyList<LlmToolCall> ToolCalls,
    AiTokenUsage Usage,
    string Model,
    string? StopReason);

/// <summary>Модель из списка провайдера.</summary>
public sealed record AiProviderModel(string Id, string? DisplayName, DateTimeOffset? CreatedAt);

/// <summary>
/// Провайдер LLM (OpenAI, Anthropic, в будущем — другие). Новый провайдер = новая реализация этого интерфейса,
/// остальная AI-архитектура от провайдера не зависит.
/// </summary>
public interface IAiProvider
{
    string Code { get; }
    string Name { get; }
    string DefaultBaseUrl { get; }

    /// <summary>Провайдер умеет строить эмбеддинги (для семантического поиска).</summary>
    bool SupportsEmbeddings { get; }

    Task<AiCompletion> CompleteAsync(AiCredentials credentials, AiCompletionRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiProviderModel>> ListModelsAsync(AiCredentials credentials, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<float[]> Vectors, AiTokenUsage Usage)> EmbedAsync(
        AiCredentials credentials, string model, IReadOnlyList<string> inputs, CancellationToken cancellationToken = default);
}

/// <summary>
/// Ошибка провайдера. Message безопасен для показа пользователю: ключ и похожие на ключи строки вырезаны.
/// Transient — стоит повторить или переключиться на резервный провайдер.
/// </summary>
public sealed class AiProviderException(string provider, int? statusCode, string message, bool transient)
    : Exception(message)
{
    public string Provider { get; } = provider;
    public int? StatusCode { get; } = statusCode;
    public bool Transient { get; } = transient;
}

internal static partial class ProviderErrors
{
    [GeneratedRegex(@"(sk-ant-|sk-|key-)[A-Za-z0-9_\-\*\.]{4,}", RegexOptions.IgnoreCase)]
    private static partial Regex KeyLike();

    /// <summary>Убирает ключ и всё, что похоже на ключ, и обрезает длинный текст.</summary>
    public static string Scrub(string? text, string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var clean = text;
        if (!string.IsNullOrEmpty(apiKey))
        {
            clean = clean.Replace(apiKey, "•••", StringComparison.Ordinal);
        }

        clean = KeyLike().Replace(clean, "•••").Trim();
        return clean.Length > 300 ? clean[..300] + "…" : clean;
    }

    /// <summary>Текст ошибки из тела ответа OpenAI ({"error":{"message"}}) или Anthropic ({"error":{"type","message"}}).</summary>
    public static string? MessageOf(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String)
                {
                    return error.GetString();
                }

                if (error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var message))
                {
                    return message.GetString();
                }
            }
        }
        catch (JsonException)
        {
            // не JSON — ниже вернётся null
        }

        return null;
    }

    public static AiProviderException FromStatus(string provider, int status, string body, string apiKey)
    {
        var detail = Scrub(MessageOf(body), apiKey);
        var suffix = detail.Length > 0 ? $" Ответ провайдера: {detail}" : string.Empty;
        return status switch
        {
            401 => new AiProviderException(provider, status, "Ключ API не принят провайдером — проверьте ключ в «Настройки → AI»." + suffix, false),
            403 => new AiProviderException(provider, status, "Провайдер отказал в доступе: у ключа нет прав на эту модель или запрос." + suffix, false),
            404 => new AiProviderException(provider, status, "Модель или адрес API не найдены." + suffix, false),
            408 => new AiProviderException(provider, status, "Провайдер не дождался запроса (HTTP 408)." + suffix, true),
            429 => new AiProviderException(provider, status, "Превышен лимит запросов или закончился баланс у провайдера." + suffix, true),
            >= 500 => new AiProviderException(provider, status, $"Провайдер временно недоступен (HTTP {status})." + suffix, true),
            _ => new AiProviderException(provider, status, $"Провайдер отклонил запрос (HTTP {status})." + suffix, false),
        };
    }
}
