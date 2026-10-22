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

/// <summary>
/// Ответ модели в формате её провайдера — уходит обратно без изменений. У Claude так между вызовами инструментов сохраняются блоки
/// рассуждений (thinking): без них модель теряет ход мысли, а API может отклонить запрос.
/// </summary>
public sealed record LlmProviderContent(string Provider, JsonElement Content);

public sealed record LlmMessage(
    LlmRole Role,
    string Content,
    string? ToolCallId = null,
    IReadOnlyList<LlmToolCall>? ToolCalls = null,
    LlmProviderContent? Raw = null);

public sealed record LlmToolDefinition(string Name, string Description, JsonElement InputSchema);

public sealed record LlmResponse(string? Text, IReadOnlyList<LlmToolCall> ToolCalls, LlmProviderContent? Raw = null)
{
    /// <summary>Ответ как сообщение ассистента — для продолжения диалога.</summary>
    public LlmMessage Reply => new(LlmRole.Assistant, Text ?? string.Empty, ToolCalls: ToolCalls.Count > 0 ? ToolCalls : null, Raw: Raw);
}

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
