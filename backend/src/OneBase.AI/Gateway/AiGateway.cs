using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OneBase.AI.Llm;
using OneBase.AI.Providers;

namespace OneBase.AI.Gateway;

/// <summary>Кто и зачем вызывает модель — для учёта токенов и журнала.</summary>
public sealed record AiCallContext(Guid? UserId = null, string? AgentCode = null, Guid? ConversationId = null, string Purpose = "chat");

public sealed record AiCallOptions
{
    /// <summary>Модель агента; null — основная из настроек.</summary>
    public AiModelRef? Model { get; init; }

    /// <summary>Модель на случай ошибки основной; null — резервная из настроек.</summary>
    public AiModelRef? Fallback { get; init; }

    /// <summary>false — только указанная модель, без переключения на резервную (проверка модели в настройках).</summary>
    public bool AllowFallback { get; init; } = true;

    public double? Temperature { get; init; }
    public int? MaxOutputTokens { get; init; }

    /// <summary>Ответ — JSON-объект (у OpenAI включается response_format, у Anthropic — только инструкцией).</summary>
    public bool JsonOutput { get; init; }

    public AiCallContext Context { get; init; } = new();
}

public sealed record AiCallResult(
    string? Text,
    IReadOnlyList<LlmToolCall> ToolCalls,
    AiTokenUsage Usage,
    AiModelRef Model,
    bool UsedFallback,
    long ElapsedMs,
    string? StopReason);

/// <summary>Одна попытка вызова провайдера — для учёта использования и стоимости.</summary>
public sealed record AiCallRecord(
    AiCallContext Context,
    AiModelRef Model,
    AiTokenUsage Usage,
    long ElapsedMs,
    bool Success,
    string? Error);

/// <summary>Учёт вызовов моделей. По умолчанию ничего не пишет.</summary>
public interface IAiUsageRecorder
{
    Task RecordAsync(AiCallRecord record, CancellationToken cancellationToken = default);
}

internal sealed class NoUsageRecorder : IAiUsageRecorder
{
    public Task RecordAsync(AiCallRecord record, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>Готов ли AI к работе и что не так, если нет. Message безопасен для показа пользователю.</summary>
public sealed record AiReadiness(bool Ready, string? Message);

/// <summary>
/// AI Gateway — единая точка вызова моделей: выбирает провайдера и модель по настройкам, подставляет ключ,
/// повторяет запрос при временной ошибке, переключается на резервного провайдера и учитывает токены.
/// </summary>
public interface IAiGateway
{
    Task<AiReadiness> GetReadinessAsync(CancellationToken cancellationToken = default);

    Task<AiCallResult> CompleteAsync(
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<LlmToolDefinition> tools,
        AiCallOptions options,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<float[]> Vectors, AiModelRef Model)> EmbedAsync(
        IReadOnlyList<string> inputs,
        AiCallContext context,
        CancellationToken cancellationToken = default);
}

internal sealed class AiGateway(
    IAiSettingsSource store,
    AiProviderRegistry registry,
    IAiUsageRecorder recorder,
    ILogger<AiGateway> logger) : IAiGateway, ILlmClient
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1.5);

    public async Task<AiReadiness> GetReadinessAsync(CancellationToken cancellationToken = default)
    {
        var settings = await store.GetSettingsAsync(cancellationToken);
        if (!settings.Enabled)
        {
            return new AiReadiness(false, "AI выключен администратором.");
        }

        if (settings.Primary is null)
        {
            return new AiReadiness(false, "AI не настроен: администратор должен выбрать основную модель в «Настройки → AI».");
        }

        var provider = await store.GetProviderAsync(settings.Primary.Provider, cancellationToken);
        if (provider is { IsReady: true })
        {
            return new AiReadiness(true, null);
        }

        if (settings.Fallback is { } fallback && await store.GetProviderAsync(fallback.Provider, cancellationToken) is { IsReady: true })
        {
            return new AiReadiness(true, null);
        }

        return new AiReadiness(false, provider is null
            ? "AI не настроен: провайдер основной модели не поддерживается."
            : !provider.HasKey
                ? $"AI не настроен: нет ключа API {provider.Name}. Администратор добавляет его в «Настройки → AI → Провайдеры»."
                : $"Провайдер {provider.Name} выключен в «Настройки → AI → Провайдеры».");
    }

    public async Task<AiCallResult> CompleteAsync(
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<LlmToolDefinition> tools,
        AiCallOptions options,
        CancellationToken cancellationToken = default)
    {
        var settings = await store.GetSettingsAsync(cancellationToken);
        var readiness = await GetReadinessAsync(cancellationToken);
        if (!settings.Enabled || settings.Primary is null && options.Model is null)
        {
            throw new LlmNotConfiguredException(readiness.Message);
        }

        var primary = options.Model ?? settings.Primary!;
        var fallback = options.AllowFallback ? options.Fallback ?? settings.Fallback : null;
        if (fallback == primary)
        {
            fallback = null;
        }

        var temperature = options.Temperature ?? settings.Temperature;
        var maxTokens = options.MaxOutputTokens ?? settings.MaxOutputTokens;

        try
        {
            return await AttemptAsync(primary, false);
        }
        catch (AiProviderException primaryError) when (fallback is not null && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("AI: {Model} недоступна ({Status}), переключаюсь на резервную {Fallback}", primary, primaryError.StatusCode, fallback);
            try
            {
                return await AttemptAsync(fallback, true);
            }
            catch (AiProviderException fallbackError)
            {
                throw new AiProviderException(fallbackError.Provider, fallbackError.StatusCode,
                    $"Основная модель: {primaryError.Message} Резервная модель: {fallbackError.Message}", fallbackError.Transient);
            }
        }

        async Task<AiCallResult> AttemptAsync(AiModelRef model, bool usedFallback)
        {
            var provider = registry.Find(model.Provider)
                ?? throw new AiProviderException(model.Provider, null, $"Провайдер «{model.Provider}» не поддерживается.", false);
            var state = await store.GetProviderAsync(model.Provider, cancellationToken);
            if (state is null || !state.HasKey)
            {
                throw new AiProviderException(model.Provider, null, $"Нет ключа API {provider.Name} — добавьте его в «Настройки → AI → Провайдеры».", false);
            }

            if (!state.Enabled)
            {
                throw new AiProviderException(model.Provider, null, $"Провайдер {provider.Name} выключен в настройках AI.", false);
            }

            var request = new AiCompletionRequest(model.Model, messages, tools, temperature, maxTokens, options.JsonOutput);
            for (var attempt = 1; ; attempt++)
            {
                var watch = Stopwatch.StartNew();
                try
                {
                    var completion = await provider.CompleteAsync(state.Credentials, request, cancellationToken);
                    watch.Stop();
                    await RecordAsync(new AiCallRecord(options.Context, model, completion.Usage, watch.ElapsedMilliseconds, true, null));
                    return new AiCallResult(completion.Text, completion.ToolCalls, completion.Usage, model, usedFallback, watch.ElapsedMilliseconds, completion.StopReason);
                }
                catch (AiProviderException ex)
                {
                    watch.Stop();
                    await RecordAsync(new AiCallRecord(options.Context, model, AiTokenUsage.None, watch.ElapsedMilliseconds, false, ex.Message));

                    // Один повтор при сбое сервера или сети; при лимите (429) повтор обычно бесполезен — сразу резерв.
                    if (attempt == 1 && ex.Transient && ex.StatusCode is not 429)
                    {
                        await Task.Delay(RetryDelay, cancellationToken);
                        continue;
                    }

                    throw;
                }
            }
        }
    }

    public async Task<(IReadOnlyList<float[]> Vectors, AiModelRef Model)> EmbedAsync(
        IReadOnlyList<string> inputs, AiCallContext context, CancellationToken cancellationToken = default)
    {
        var settings = await store.GetSettingsAsync(cancellationToken);
        var model = settings.Embedding ?? throw new LlmNotConfiguredException("Модель эмбеддингов не выбрана в «Настройки → AI».");
        var provider = registry.Find(model.Provider)
            ?? throw new AiProviderException(model.Provider, null, $"Провайдер «{model.Provider}» не поддерживается.", false);
        var state = await store.GetProviderAsync(model.Provider, cancellationToken);
        if (state is not { IsReady: true })
        {
            throw new AiProviderException(model.Provider, null, $"Провайдер {provider.Name} для эмбеддингов не настроен.", false);
        }

        var watch = Stopwatch.StartNew();
        try
        {
            var (vectors, usage) = await provider.EmbedAsync(state.Credentials, model.Model, inputs, cancellationToken);
            await RecordAsync(new AiCallRecord(context, model, usage, watch.ElapsedMilliseconds, true, null));
            return (vectors, model);
        }
        catch (AiProviderException ex)
        {
            await RecordAsync(new AiCallRecord(context, model, AiTokenUsage.None, watch.ElapsedMilliseconds, false, ex.Message));
            throw;
        }
    }

    /// <summary>ILlmClient: основная модель из настроек, без контекста пользователя.</summary>
    async Task<LlmResponse> ILlmClient.CompleteAsync(
        IReadOnlyList<LlmMessage> messages, IReadOnlyList<LlmToolDefinition> tools, CancellationToken cancellationToken)
    {
        var result = await CompleteAsync(messages, tools, new AiCallOptions(), cancellationToken);
        return new LlmResponse(result.Text, result.ToolCalls);
    }

    private async Task RecordAsync(AiCallRecord record)
    {
        try
        {
            // Учёт не должен срывать ответ пользователю — пишем независимо от отмены запроса.
            await recorder.RecordAsync(record, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI: не удалось записать учёт вызова {Model}", record.Model);
        }
    }
}
