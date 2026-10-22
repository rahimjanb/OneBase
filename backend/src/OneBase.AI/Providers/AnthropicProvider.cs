using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using OneBase.AI.Llm;

namespace OneBase.AI.Providers;

/// <summary>
/// Anthropic Messages API (POST {base}/v1/messages) с tool use, список моделей — GET {base}/v1/models.
/// Эмбеддингов у Anthropic нет.
/// </summary>
public sealed partial class AnthropicProvider(IHttpClientFactory httpFactory) : HttpAiProvider(httpFactory)
{
    public const string ProviderCode = "anthropic";
    public const string ApiVersion = "2023-06-01";

    /// <summary>Основная модель OneBase по умолчанию.</summary>
    public const string DefaultModel = "claude-opus-5-5";

    public override string Code => ProviderCode;
    public override string Name => "Anthropic";
    public override string DefaultBaseUrl => "https://api.anthropic.com";

    protected override void Authorize(HttpRequestHeaders headers, string apiKey)
    {
        headers.Add("x-api-key", apiKey);
        headers.Add("anthropic-version", ApiVersion);
    }

    public override async Task<AiCompletion> CompleteAsync(AiCredentials credentials, AiCompletionRequest request, CancellationToken cancellationToken = default)
    {
        using var doc = await PostAsync(credentials, Endpoint(credentials, "/v1/messages"), BuildBody(request), cancellationToken);
        return ParseCompletion(doc.RootElement, request.Model);
    }

    public override async Task<IReadOnlyList<AiProviderModel>> ListModelsAsync(AiCredentials credentials, CancellationToken cancellationToken = default)
    {
        var list = new List<AiProviderModel>();
        string? after = null;
        for (var page = 0; page < 20; page++)
        {
            var url = Endpoint(credentials, "/v1/models?limit=1000") + (after is null ? string.Empty : $"&after_id={Uri.EscapeDataString(after)}");
            using var doc = await GetAsync(credentials, url, cancellationToken);
            var root = doc.RootElement;
            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var m in data.EnumerateArray())
                {
                    var id = Str(m, "id");
                    if (string.IsNullOrEmpty(id))
                    {
                        continue;
                    }

                    DateTimeOffset? created = DateTimeOffset.TryParse(Str(m, "created_at"), out var at) ? at : null;
                    list.Add(new AiProviderModel(id, Str(m, "display_name"), created));
                }
            }

            var hasMore = root.TryGetProperty("has_more", out var more) && more.ValueKind == JsonValueKind.True;
            after = Str(root, "last_id");
            if (!hasMore || after is null)
            {
                break;
            }
        }

        return list;
    }

    internal static JsonObject BuildBody(AiCompletionRequest request)
    {
        var system = new StringBuilder();
        var messages = new JsonArray();
        JsonArray? pendingResults = null;

        void FlushResults()
        {
            if (pendingResults is not null)
            {
                messages.Add(new JsonObject { ["role"] = "user", ["content"] = pendingResults });
                pendingResults = null;
            }
        }

        foreach (var m in request.Messages)
        {
            switch (m.Role)
            {
                case LlmRole.System:
                    if (system.Length > 0)
                    {
                        system.Append("\n\n");
                    }

                    system.Append(m.Content);
                    break;

                case LlmRole.Tool:
                    // Результаты инструментов — блоки tool_result в одном сообщении пользователя.
                    pendingResults ??= new JsonArray();
                    pendingResults.Add(new JsonObject
                    {
                        ["type"] = "tool_result",
                        ["tool_use_id"] = m.ToolCallId,
                        ["content"] = string.IsNullOrEmpty(m.Content) ? "(пусто)" : m.Content,
                    });
                    break;

                case LlmRole.User:
                    FlushResults();
                    messages.Add(new JsonObject { ["role"] = "user", ["content"] = string.IsNullOrEmpty(m.Content) ? "(пусто)" : m.Content });
                    break;

                default:
                    FlushResults();

                    // Ответ Claude — как пришёл, вместе с блоками рассуждений: их нельзя убирать или пересобирать между вызовами инструментов.
                    if (m.Raw is { Provider: ProviderCode, Content: { ValueKind: JsonValueKind.Array } raw } && raw.GetArrayLength() > 0)
                    {
                        messages.Add(new JsonObject { ["role"] = "assistant", ["content"] = JsonNode.Parse(raw.GetRawText()) });
                        break;
                    }

                    var content = new JsonArray();
                    if (!string.IsNullOrWhiteSpace(m.Content))
                    {
                        content.Add(new JsonObject { ["type"] = "text", ["text"] = m.Content });
                    }

                    foreach (var call in m.ToolCalls ?? [])
                    {
                        content.Add(new JsonObject
                        {
                            ["type"] = "tool_use",
                            ["id"] = call.Id,
                            ["name"] = call.Name,
                            ["input"] = JsonNode.Parse(call.Arguments.ValueKind == JsonValueKind.Object ? call.Arguments.GetRawText() : "{}"),
                        });
                    }

                    if (content.Count > 0)
                    {
                        messages.Add(new JsonObject { ["role"] = "assistant", ["content"] = content });
                    }

                    break;
            }
        }

        FlushResults();

        var body = new JsonObject
        {
            ["model"] = request.Model,
            ["max_tokens"] = request.MaxOutputTokens,
            ["messages"] = messages,
        };

        if (system.Length > 0)
        {
            body["system"] = system.ToString();
        }

        if (request.Temperature is { } temperature && AcceptsTemperature(request.Model))
        {
            body["temperature"] = temperature;
        }

        // Глубина рассуждений. Рассуждения (adaptive thinking) у Claude 5 включены всегда — effort задаёт, сколько модель думает.
        if (request.ReasoningEffort is { } effort && SupportsEffort(request.Model))
        {
            body["output_config"] = new JsonObject { ["effort"] = effort };
        }

        if (request.Tools.Count > 0)
        {
            body["tools"] = new JsonArray(request.Tools.Select(t => (JsonNode?)new JsonObject
            {
                ["name"] = t.Name,
                ["description"] = t.Description,
                ["input_schema"] = JsonNode.Parse(t.InputSchema.GetRawText()),
            }).ToArray());

            if (!request.AllowToolCalls)
            {
                body["tool_choice"] = new JsonObject { ["type"] = "none" };
            }
        }

        return body;
    }

    /// <summary>
    /// Принимает ли модель temperature. Claude 5 (Opus, Sonnet, Haiku, Fable) и Opus 4.7–4.8 отвечают на неё ошибкой 400 —
    /// параметр им не отправляется. Неизвестные идентификаторы — как старые модели (если что, AI Gateway повторит запрос без неё).
    /// </summary>
    internal static bool AcceptsTemperature(string model) => Version(model) switch
    {
        null => true,
        ("fable" or "mythos", _, _) => false,
        (_, var major, _) when major >= 5 => false,
        ("opus", 4, var minor) => minor < 7,
        _ => true,
    };

    /// <summary>Принимает ли модель глубину рассуждений (output_config.effort): Claude 5, Opus 4.5–4.8, Sonnet 4.6.</summary>
    internal static bool SupportsEffort(string model) => Version(model) switch
    {
        null => false,
        ("fable" or "mythos", _, _) => true,
        (_, var major, _) when major >= 5 => true,
        ("opus", 4, var minor) => minor >= 5,
        ("sonnet", 4, var minor) => minor >= 6,
        _ => false,
    };

    /// <summary>Семейство и версия из идентификатора: claude-opus-5-5 → (opus, 5, 5), claude-haiku-4-5-20251001 → (haiku, 4, 5), claude-opus-5 → (opus, 5, 0).</summary>
    private static (string Family, int Major, int Minor)? Version(string model)
    {
        var match = ClaudeModelId().Match(model.Trim().ToLowerInvariant());
        if (!match.Success)
        {
            return null;
        }

        return (match.Groups[1].Value, int.Parse(match.Groups[2].Value), match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0);
    }

    [GeneratedRegex(@"^claude-(opus|sonnet|haiku|fable|mythos)-(\d{1,2})(?:-(\d{1,2}))?(?=-|$)")]
    private static partial Regex ClaudeModelId();

    internal static AiCompletion ParseCompletion(JsonElement root, string requestedModel)
    {
        var text = new StringBuilder();
        var calls = new List<LlmToolCall>();
        if (root.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in content.EnumerateArray())
            {
                switch (Str(block, "type"))
                {
                    case "text":
                        text.Append(Str(block, "text"));
                        break;
                    case "tool_use":
                        var input = block.TryGetProperty("input", out var i) && i.ValueKind == JsonValueKind.Object
                            ? i.Clone()
                            : JsonSerializer.SerializeToElement(new { });
                        calls.Add(new LlmToolCall(Str(block, "id") ?? Guid.NewGuid().ToString("N"), Str(block, "name") ?? string.Empty, input));
                        break;
                }
            }
        }

        var usage = root.TryGetProperty("usage", out var u) ? new AiTokenUsage(Int(u, "input_tokens"), Int(u, "output_tokens")) : AiTokenUsage.None;
        return new AiCompletion(text.Length > 0 ? text.ToString() : null, calls, usage, Str(root, "model") ?? requestedModel, Str(root, "stop_reason"),
            content.ValueKind == JsonValueKind.Array ? content.Clone() : null);
    }
}
