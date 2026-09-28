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

public sealed class LlmNotConfiguredException()
    : InvalidOperationException("LLM-провайдер не настроен. Зарегистрируйте реализацию ILlmClient и задайте секцию Llm в конфигурации.");

/// <summary>Заглушка до подключения реального провайдера.</summary>
internal sealed class NotConfiguredLlmClient : ILlmClient
{
    public Task<LlmResponse> CompleteAsync(
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<LlmToolDefinition> tools,
        CancellationToken cancellationToken = default) =>
        throw new LlmNotConfiguredException();
}
