using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;
using Microsoft.Extensions.Logging;
using OneBase.AI.Gateway;
using OneBase.AI.Providers;
using OneBase.Application.DataManager;

namespace OneBase.AI.DataManager;

/// <summary>
/// Блок переписки с моделью — формат хранения в DataConversation.Transcript (не зависит от версии SDK).
/// type: text, thinking, redacted_thinking, tool_use, tool_result, fallback.
/// </summary>
public sealed record TranscriptBlock
{
    public required string Type { get; init; }
    public string? Text { get; init; }
    public string? Thinking { get; init; }
    public string? Signature { get; init; }
    public string? Data { get; init; }
    public string? Id { get; init; }
    public string? Name { get; init; }
    public JsonElement? Input { get; init; }
    public string? ToolUseId { get; init; }
    public bool? IsError { get; init; }

    /// <summary>Исходный JSON блока, который возвращается модели без изменений (переключение на резервную модель).</summary>
    public JsonElement? Raw { get; init; }

    public static TranscriptBlock FromText(string text) => new() { Type = "text", Text = text };
}

public sealed record TranscriptMessage(string Role, List<TranscriptBlock> Content);

public sealed record DataToolSpec(string Name, string Description, JsonElement Schema);

public sealed record ModelReply(IReadOnlyList<TranscriptBlock> Content, string StopReason, int InputTokens, int OutputTokens, string Model);

/// <summary>Модель для AI Data Manager. В тестах — подставная реализация с заранее заданными ответами.</summary>
public interface IDataManagerModel
{
    Task<AiReadiness> GetReadinessAsync(CancellationToken ct = default);

    /// <exception cref="DataManagerModelException">Ошибка API — текст безопасен для показа.</exception>
    Task<ModelReply> NextAsync(string system, IReadOnlyList<TranscriptMessage> transcript, IReadOnlyList<DataToolSpec> tools, AiCallContext context,
        CancellationToken ct = default);
}

public sealed class DataManagerModelException(string message, bool transient) : Exception(message)
{
    public bool Transient { get; } = transient;
}

/// <summary>
/// Claude через официальный SDK Anthropic (Messages API, beta-клиент ради резервной модели при отказе):
/// адаптивные рассуждения, кэширование подсказки и инструментов, повторы SDK на 429/5xx/сетевые ошибки.
/// Ключ — из «Настройки → AI → Провайдеры» (зашифрован в БД) или ANTHROPIC_API_KEY; в браузер и журналы не попадает.
/// </summary>
internal sealed class ClaudeDataManagerModel(
    IAiSettingsSource settings,
    IAiUsageRecorder usage,
    DataManagerOptions options,
    ILogger<ClaudeDataManagerModel> logger) : IDataManagerModel
{
    private const string ProviderCode = AnthropicProvider.ProviderCode;

    /// <summary>Серверная резервная модель при отказе по политике (refusal) — маршрут выбирает API.</summary>
    private const string FallbackBeta = "server-side-fallback-2026-07-01";

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(5);

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<AiReadiness> GetReadinessAsync(CancellationToken ct = default)
    {
        var snapshot = await settings.GetSettingsAsync(ct);
        if (!snapshot.Enabled)
        {
            return new AiReadiness(false, "AI выключен в «Настройки → AI».");
        }

        var provider = await settings.GetProviderAsync(ProviderCode, ct);
        if (provider is null || !provider.Enabled || string.IsNullOrWhiteSpace(provider.ApiKey))
        {
            return new AiReadiness(false,
                "Нет ключа Claude API: добавьте ключ Anthropic в «Настройки → AI → Провайдеры» или переменную ANTHROPIC_API_KEY на сервере.");
        }

        return new AiReadiness(true, null);
    }

    public async Task<ModelReply> NextAsync(string system, IReadOnlyList<TranscriptMessage> transcript, IReadOnlyList<DataToolSpec> tools,
        AiCallContext context, CancellationToken ct = default)
    {
        var provider = await settings.GetProviderAsync(ProviderCode, ct);
        if (provider is null || !provider.Enabled || string.IsNullOrWhiteSpace(provider.ApiKey))
        {
            throw new DataManagerModelException("Нет ключа Claude API (Anthropic) — см. «Настройки → AI → Провайдеры».", transient: false);
        }

        // 408/409/429/5xx и обрывы связи SDK повторяет сам (3 раза, с паузами и Retry-After).
        var client = provider.CustomBaseUrl
            ? new AnthropicClient { ApiKey = provider.ApiKey, MaxRetries = 3, Timeout = RequestTimeout, BaseUrl = provider.BaseUrl.TrimEnd('/') }
            : new AnthropicClient { ApiKey = provider.ApiKey, MaxRetries = 3, Timeout = RequestTimeout };

        var request = new MessageCreateParams
        {
            Model = options.Model,
            MaxTokens = options.MaxOutputTokens,
            Betas = [FallbackBeta],
            Fallbacks = new Default(),
            Thinking = new BetaThinkingConfigAdaptive(),
            OutputConfig = new BetaOutputConfig { Effort = EffortOf(options.Effort) },
            // Подсказка и инструменты не меняются между запросами — кэшируются; переписка кэшируется автоматически.
            System = new List<BetaTextBlockParam> { new() { Text = system, CacheControl = new BetaCacheControlEphemeral() } },
            CacheControl = new BetaCacheControlEphemeral(),
            Tools = tools.Select(ToTool).ToList(),
            Messages = transcript.Select(ToParam).ToList(),
        };

        var watch = Stopwatch.StartNew();
        var model = new AiModelRef(ProviderCode, options.Model);
        try
        {
            BetaMessage response = await client.Beta.Messages.Create(request, ct);
            var tokens = new AiTokenUsage(
                (int)(response.Usage.InputTokens + (response.Usage.CacheReadInputTokens ?? 0) + (response.Usage.CacheCreationInputTokens ?? 0)),
                (int)response.Usage.OutputTokens);
            await usage.RecordAsync(new AiCallRecord(context, model, tokens, watch.ElapsedMilliseconds, true, null), ct);
            return new ModelReply(FromResponse(response), StopReasonOf(response), tokens.InputTokens, tokens.OutputTokens, options.Model);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            var (message, transient) = Describe(ex);
            logger.LogWarning("AI Data Manager: Claude API — {Error} ({Type})", message, ex.GetType().Name);
            await usage.RecordAsync(new AiCallRecord(context, model, AiTokenUsage.None, watch.ElapsedMilliseconds, false, message), CancellationToken.None);
            throw new DataManagerModelException(message, transient);
        }
    }

    /// <summary>Ошибка API — по-русски, без тела ответа и ключа.</summary>
    private static (string Message, bool Transient) Describe(Exception ex) => ex switch
    {
        AnthropicUnauthorizedException => ("Claude API не принял ключ. Проверьте ключ Anthropic в «Настройки → AI → Провайдеры».", false),
        AnthropicForbiddenException => ("У ключа Anthropic нет доступа к этой модели или функции.", false),
        AnthropicNotFoundException => ("Модель не найдена в Claude API — проверьте DataManager:Model.", false),
        AnthropicRateLimitException => ("Claude API временно ограничил запросы (лимит). Повторите через минуту.", true),
        AnthropicBadRequestException => ("Claude API отклонил запрос (неверный формат). Подробности — в журнале сервера.", false),
        Anthropic5xxException => ("Claude API временно недоступен. Повторите позже.", true),
        AnthropicIOException or HttpRequestException => ("Нет связи с Claude API.", true),
        TaskCanceledException or TimeoutException or OperationCanceledException => ("Claude API не ответил вовремя.", true),
        _ => ("Ошибка Claude API. Подробности — в журнале сервера.", false),
    };

    private static Effort EffortOf(string? effort) => (effort ?? "high").ToLowerInvariant() switch
    {
        "low" => Effort.Low,
        "medium" => Effort.Medium,
        "xhigh" => Effort.Xhigh,
        "max" => Effort.Max,
        _ => Effort.High,
    };

    private static string StopReasonOf(BetaMessage response)
    {
        var raw = response.StopReason?.Raw();
        return string.IsNullOrEmpty(raw) ? "end_turn" : raw;
    }

    private static BetaToolUnion ToTool(DataToolSpec spec)
    {
        var properties = spec.Schema.TryGetProperty("properties", out var p)
            ? p.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone())
            : new Dictionary<string, JsonElement>();
        var required = spec.Schema.TryGetProperty("required", out var r)
            ? r.EnumerateArray().Select(x => x.GetString()!).ToList()
            : [];
        return new BetaTool
        {
            Name = spec.Name,
            Description = spec.Description,
            InputSchema = new() { Properties = properties, Required = required },
        };
    }

    internal static BetaMessageParam ToParam(TranscriptMessage message)
    {
        var blocks = new List<BetaContentBlockParam>();
        foreach (var b in message.Content)
        {
            switch (b.Type)
            {
                case "text" when !string.IsNullOrEmpty(b.Text):
                    blocks.Add(new BetaTextBlockParam { Text = b.Text });
                    break;
                case "thinking":
                    blocks.Add(new BetaThinkingBlockParam { Thinking = b.Thinking ?? string.Empty, Signature = b.Signature ?? string.Empty });
                    break;
                case "redacted_thinking":
                    blocks.Add(new BetaRedactedThinkingBlockParam { Data = b.Data ?? string.Empty });
                    break;
                case "tool_use":
                    blocks.Add(new BetaToolUseBlockParam { ID = b.Id!, Name = b.Name!, Input = Dictionary(b.Input) });
                    break;
                case "tool_result":
                    blocks.Add(new BetaToolResultBlockParam { ToolUseID = b.ToolUseId!, Content = b.Text ?? string.Empty, IsError = b.IsError ?? false });
                    break;
                case "fallback" when b.Raw is { ValueKind: JsonValueKind.Object } raw:
                    blocks.Add(BetaFallbackBlockParam.FromRawUnchecked(Dictionary(raw)));
                    break;
            }
        }

        if (blocks.Count == 0)
        {
            blocks.Add(new BetaTextBlockParam { Text = "…" });
        }

        return new BetaMessageParam { Role = message.Role == "assistant" ? Role.Assistant : Role.User, Content = blocks };
    }

    private static Dictionary<string, JsonElement> Dictionary(JsonElement? element) =>
        element is { ValueKind: JsonValueKind.Object } e ? e.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone()) : [];

    internal static List<TranscriptBlock> FromResponse(BetaMessage response)
    {
        var result = new List<TranscriptBlock>();
        foreach (var block in response.Content)
        {
            if (block.TryPickText(out var text))
            {
                result.Add(TranscriptBlock.FromText(text.Text));
            }
            else if (block.TryPickThinking(out var thinking))
            {
                // подпись обязательна — без неё модель не примет рассуждение обратно
                result.Add(new TranscriptBlock { Type = "thinking", Thinking = thinking.Thinking, Signature = thinking.Signature });
            }
            else if (block.TryPickRedactedThinking(out var redacted))
            {
                result.Add(new TranscriptBlock { Type = "redacted_thinking", Data = redacted.Data });
            }
            else if (block.TryPickToolUse(out var toolUse))
            {
                result.Add(new TranscriptBlock
                {
                    Type = "tool_use",
                    Id = toolUse.ID,
                    Name = toolUse.Name,
                    Input = JsonSerializer.SerializeToElement(toolUse.Input),
                });
            }
            else if (block.TryPickFallback(out var fallback))
            {
                result.Add(new TranscriptBlock { Type = "fallback", Raw = JsonSerializer.SerializeToElement(fallback.RawData) });
            }
        }

        return result;
    }
}
