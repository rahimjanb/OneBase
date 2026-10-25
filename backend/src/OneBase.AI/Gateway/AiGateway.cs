using System.Collections.Concurrent;
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

    /// <summary>
    /// Повторить ту же модель один раз при сбое сервера или сети. false — сразу резервная модель: для модели категории простых задач, чтобы
    /// сбой не стоил двух её вызовов плюс вызова резервной (запрос, оборвавшийся по времени, провайдер мог уже посчитать).
    /// </summary>
    public bool RetrySameModel { get; init; } = true;

    public double? Temperature { get; init; }
    public int? MaxOutputTokens { get; init; }

    /// <summary>Глубина рассуждений; null — из настроек AI.</summary>
    public string? ReasoningEffort { get; init; }

    /// <summary>Ответ — JSON-объект (у OpenAI включается response_format, у Anthropic — только инструкцией).</summary>
    public bool JsonOutput { get; init; }

    /// <summary>false — инструменты остаются в запросе (они есть в истории), но вызывать их нельзя: модель отвечает текстом.</summary>
    public bool AllowToolCalls { get; init; } = true;

    /// <summary>
    /// Кэш промпта у провайдера (Anthropic prompt caching): инструменты и первое системное сообщение (неизменная часть инструкций) помечаются
    /// для кэширования, повторные вызовы с тем же началом читают его по сниженной цене. false — для промптов, уникальных в каждом запросе:
    /// запись в кэш стоит дороже входа. Меняющееся (дата, данные под вопрос) — во втором системном сообщении или в сообщениях пользователя.
    /// </summary>
    public bool CachePrompt { get; init; } = true;

    /// <summary>
    /// Кэшировать и переписку (хвост промпта) — для циклов с инструментами, где каждый шаг повторяет все предыдущие. false — для разового
    /// вызова, у которого повтор с тем же хвостом маловероятен: тогда кэшируется только неизменное начало.
    /// </summary>
    public bool CacheConversation { get; init; } = true;

    public AiCallContext Context { get; init; } = new();
}

public sealed record AiCallResult(
    string? Text,
    IReadOnlyList<LlmToolCall> ToolCalls,
    AiTokenUsage Usage,
    AiModelRef Model,
    bool UsedFallback,
    long ElapsedMs,
    string? StopReason,
    LlmProviderContent? Raw = null)
{
    /// <summary>Ответ как сообщение ассистента — для продолжения диалога (у Claude — вместе с блоками рассуждений).</summary>
    public LlmMessage Reply => new LlmResponse(Text, ToolCalls, Raw).Reply;
}

/// <summary>Одна попытка вызова провайдера — для учёта использования и стоимости.</summary>
public sealed record AiCallRecord(
    AiCallContext Context,
    AiModelRef Model,
    AiTokenUsage Usage,
    long ElapsedMs,
    bool Success,
    string? Error);

/// <summary>Учёт вызовов моделей (реализация — Monitoring.DbUsageRecorder).</summary>
public interface IAiUsageRecorder
{
    Task RecordAsync(AiCallRecord record, CancellationToken cancellationToken = default);
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
    ILogger<AiGateway> logger,
    OneBase.AI.Monitoring.IAiLimits limits) : IAiGateway, ILlmClient
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1.5);

    /// <summary>
    /// Модели, которые не принимают temperature (reasoning-модели OpenAI берут только значение по умолчанию):
    /// узнаём по ответу провайдера и дальше не отправляем параметр.
    /// </summary>
    private readonly ConcurrentDictionary<string, bool> _noTemperature = new(StringComparer.Ordinal);

    /// <summary>Модели, которые не принимают глубину рассуждений (не reasoning-модели или другое значение) — параметр не отправляется.</summary>
    private readonly ConcurrentDictionary<string, bool> _noReasoning = new(StringComparer.Ordinal);

    /// <summary>Модели (адреса), отвергнувшие пометки кэша промпта (cache_control) ошибкой 400: дальше запросы идут без них.</summary>
    private readonly ConcurrentDictionary<string, bool> _noPromptCache = new(StringComparer.Ordinal);

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

        // Бюджет на AI исчерпан — новые вызовы не выполняются (пользователь видит понятную причину, администратор получил уведомление).
        await limits.EnsureBudgetAsync(cancellationToken);

        var primary = options.Model ?? settings.Primary!;
        var fallback = options.AllowFallback ? options.Fallback ?? settings.Fallback : null;
        if (fallback == primary)
        {
            fallback = null;
        }

        var temperature = options.Temperature ?? settings.Temperature;
        var maxTokens = options.MaxOutputTokens ?? settings.MaxOutputTokens;
        var reasoning = options.ReasoningEffort ?? settings.ReasoningEffort;

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

            var key = model.ToString();
            var request = new AiCompletionRequest(model.Model, messages, tools, _noTemperature.ContainsKey(key) ? null : temperature, maxTokens, options.JsonOutput,
                _noReasoning.ContainsKey(key) ? null : reasoning, options.AllowToolCalls, options.CachePrompt && !_noPromptCache.ContainsKey(key),
                options.CacheConversation);
            for (var attempt = 1; ; attempt++)
            {
                var watch = Stopwatch.StartNew();
                AiCompletion completion;
                try
                {
                    // Одновременных вызовов не больше лимита из настроек: остальные ждут очереди (а не получают 429 от провайдера).
                    using (await limits.EnterAsync(cancellationToken))
                    {
                        completion = await provider.CompleteAsync(state.Credentials, request, cancellationToken);
                    }

                    watch.Stop();
                    await RecordAsync(new AiCallRecord(options.Context, model, completion.Usage, watch.ElapsedMilliseconds, true, null));
                }
                catch (AiProviderException ex)
                {
                    watch.Stop();
                    await RecordAsync(new AiCallRecord(options.Context, model, AiTokenUsage.None, watch.ElapsedMilliseconds, false, ex.Message));

                    // Модель не принимает temperature — повторяем без неё (это не попытка из-за сбоя) и запоминаем.
                    if (request.Temperature is not null && ex.StatusCode == 400 && ex.Message.Contains("temperature", StringComparison.OrdinalIgnoreCase))
                    {
                        _noTemperature[key] = true;
                        logger.LogInformation("AI: модель {Model} не принимает temperature — запросы отправляются без неё", key);
                        request = request with { Temperature = null };
                        attempt--;
                        continue;
                    }

                    // Модель не принимает глубину рассуждений (reasoning у OpenAI, effort у Claude) — тоже повторяем без неё и запоминаем.
                    if (request.ReasoningEffort is not null && ex.StatusCode == 400
                        && (ex.Message.Contains("reasoning", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("effort", StringComparison.OrdinalIgnoreCase)))
                    {
                        _noReasoning[key] = true;
                        logger.LogInformation("AI: модель {Model} не принимает глубину рассуждений — запросы отправляются без неё", key);
                        request = request with { ReasoningEffort = null };
                        attempt--;
                        continue;
                    }

                    // Провайдер или адрес (прокси, старая модель) не принимает пометки кэша промпта — повторяем без кэша и запоминаем.
                    if (request.CachePrompt && ex.StatusCode == 400 && ex.Message.Contains("cache_control", StringComparison.OrdinalIgnoreCase))
                    {
                        _noPromptCache[key] = true;
                        logger.LogInformation("AI: модель {Model} не принимает кэш промпта (cache_control) — запросы отправляются без него", key);
                        request = request with { CachePrompt = false };
                        attempt--;
                        continue;
                    }

                    // Один повтор при сбое сервера или сети; при лимите (429) повтор обычно бесполезен — сразу резерв.
                    if (attempt == 1 && ex.Transient && ex.StatusCode is not 429 && options.RetrySameModel)
                    {
                        await Task.Delay(RetryDelay, cancellationToken);
                        continue;
                    }

                    throw;
                }

                // Отказ по правилам безопасности провайдера (Claude: stop_reason "refusal") — это не ответ; если есть резервная модель, спросим её.
                if (completion.StopReason == "refusal")
                {
                    throw new AiProviderException(model.Provider, null,
                        $"Модель {model.Model} отказалась отвечать на этот запрос — сработали ограничения безопасности провайдера. Переформулируйте вопрос.", false);
                }

                // Весь лимит ушёл на рассуждения (reasoning-модели) — ответа нет; прямо говорим, что делать.
                if (completion.ToolCalls.Count == 0 && string.IsNullOrWhiteSpace(completion.Text) && completion.StopReason is "length" or "max_tokens")
                {
                    throw new AiProviderException(model.Provider, null,
                        $"Модель {model.Model} израсходовала лимит токенов ответа ({maxTokens}) и не дописала ответ — у reasoning-моделей часть лимита уходит " +
                        "на рассуждения. Увеличьте «Максимум токенов ответа» в «Настройки → AI».", false);
                }

                return new AiCallResult(completion.Text, completion.ToolCalls, completion.Usage, model, usedFallback, watch.ElapsedMilliseconds, completion.StopReason,
                    completion.Raw is { } raw ? new LlmProviderContent(model.Provider, raw) : null);
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
        return new LlmResponse(result.Text, result.ToolCalls, result.Raw);
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
