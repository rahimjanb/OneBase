using System.Text.Json;

namespace OneBase.AI.Llm;

public enum LlmRole
{
    System,
    User,
    Assistant,
    Tool,
}

public sealed record LlmToolCall(string Id, string Name, JsonElement Arguments);

public sealed record LlmMessage(
    LlmRole Role,
    string Content,
    string? ToolCallId = null,
    IReadOnlyList<LlmToolCall>? ToolCalls = null);

public sealed record LlmToolDefinition(string Name, string Description, JsonElement InputSchema);

public sealed record LlmResponse(string? Text, IReadOnlyList<LlmToolCall> ToolCalls);

/// <summary>Провайдер-независимый клиент LLM с поддержкой tool calling.</summary>
public interface ILlmClient
{
    Task<LlmResponse> CompleteAsync(
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<LlmToolDefinition> tools,
        CancellationToken cancellationToken = default);
}

/// <summary>AI не настроен или выключен. Message безопасен для показа пользователю.</summary>
public sealed class LlmNotConfiguredException(string? message = null)
    : InvalidOperationException(message ?? "AI не настроен: администратор должен добавить ключ провайдера и выбрать модель в «Настройки → AI».");
