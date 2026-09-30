using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using OneBase.AI.Llm;

namespace OneBase.AI.Providers;

/// <summary>
/// OpenAI Chat Completions API (POST {base}/chat/completions) с function calling,
/// список моделей — GET {base}/models, эмбеддинги — POST {base}/embeddings.
/// Модели, которым в Chat Completions нельзя вызывать инструменты (reasoning-модели отвечают 400 и просят /v1/responses),
/// переводятся на Responses API (POST {base}/responses) — это запоминается до перезапуска.
/// </summary>
public sealed class OpenAiProvider(IHttpClientFactory httpFactory) : HttpAiProvider(httpFactory)
{
    public const string ProviderCode = "openai";

    /// <summary>Адрес API + модель, которые работают только через Responses API.</summary>
    private readonly ConcurrentDictionary<string, bool> _responsesOnly = new(StringComparer.Ordinal);

    public override string Code => ProviderCode;
    public override string Name => "OpenAI";
    public override string DefaultBaseUrl => "https://api.openai.com/v1";
    public override bool SupportsEmbeddings => true;

    protected override void Authorize(HttpRequestHeaders headers, string apiKey) =>
        headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

    public override async Task<AiCompletion> CompleteAsync(AiCredentials credentials, AiCompletionRequest request, CancellationToken cancellationToken = default)
    {
        var key = $"{credentials.BaseUrl.TrimEnd('/')}|{request.Model}";
        if (!_responsesOnly.ContainsKey(key))
        {
            try
            {
                using var doc = await PostAsync(credentials, Endpoint(credentials, "/chat/completions"), BuildBody(request), cancellationToken);
                return ParseCompletion(doc.RootElement, request.Model);
            }
            catch (AiProviderException ex) when (NeedsResponsesApi(ex))
            {
                _responsesOnly[key] = true;
            }
        }

        using (var doc = await PostAsync(credentials, Endpoint(credentials, "/responses"), BuildResponsesBody(request), cancellationToken))
        {
            return ParseResponse(doc.RootElement, request.Model);
        }
    }

    /// <summary>
    /// «Function tools with reasoning_effort are not supported for … in /v1/chat/completions. To use function tools, use /v1/responses…»:
    /// провайдер сам указывает на Responses API.
    /// </summary>
    internal static bool NeedsResponsesApi(AiProviderException ex) =>
        ex.StatusCode == 400 && ex.Message.Contains("/responses", StringComparison.Ordinal);

    public override async Task<IReadOnlyList<AiProviderModel>> ListModelsAsync(AiCredentials credentials, CancellationToken cancellationToken = default)
    {
        using var doc = await GetAsync(credentials, Endpoint(credentials, "/models"), cancellationToken);
        var list = new List<AiProviderModel>();
        if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var m in data.EnumerateArray())
            {
                var id = Str(m, "id");
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                DateTimeOffset? created = m.TryGetProperty("created", out var c) && c.TryGetInt64(out var unix) && unix > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(unix)
                    : null;
                list.Add(new AiProviderModel(id, null, created));
            }
        }

        return list;
    }

    public override async Task<(IReadOnlyList<float[]> Vectors, AiTokenUsage Usage)> EmbedAsync(
        AiCredentials credentials, string model, IReadOnlyList<string> inputs, CancellationToken cancellationToken = default)
    {
        var body = new JsonObject
        {
            ["model"] = model,
            ["input"] = new JsonArray(inputs.Select(i => (JsonNode?)JsonValue.Create(i)).ToArray()),
        };
        using var doc = await PostAsync(credentials, Endpoint(credentials, "/embeddings"), body, cancellationToken);

        var vectors = new float[inputs.Count][];
        foreach (var item in doc.RootElement.GetProperty("data").EnumerateArray())
        {
            var index = item.GetProperty("index").GetInt32();
            vectors[index] = item.GetProperty("embedding").EnumerateArray().Select(v => v.GetSingle()).ToArray();
        }

        var usage = doc.RootElement.TryGetProperty("usage", out var u) ? new AiTokenUsage(Int(u, "prompt_tokens"), 0) : AiTokenUsage.None;
        return (vectors, usage);
    }

    internal static JsonObject BuildBody(AiCompletionRequest request)
    {
        var messages = new JsonArray();
        foreach (var m in request.Messages)
        {
            messages.Add(m.Role switch
            {
                LlmRole.System => new JsonObject { ["role"] = "system", ["content"] = m.Content },
                LlmRole.User => new JsonObject { ["role"] = "user", ["content"] = m.Content },
                LlmRole.Tool => new JsonObject { ["role"] = "tool", ["tool_call_id"] = m.ToolCallId, ["content"] = m.Content },
                _ => Assistant(m),
            });
        }

        var body = new JsonObject
        {
            ["model"] = request.Model,
            ["messages"] = messages,
            ["max_completion_tokens"] = request.MaxOutputTokens,
        };

        if (request.Temperature is { } temperature)
        {
            body["temperature"] = temperature;
        }

        if (request.ReasoningEffort is { } effort)
        {
            body["reasoning_effort"] = effort;
        }

        if (request.Tools.Count > 0)
        {
            body["tools"] = new JsonArray(request.Tools.Select(t => (JsonNode?)new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = t.Name,
                    ["description"] = t.Description,
                    ["parameters"] = JsonNode.Parse(t.InputSchema.GetRawText()),
                },
            }).ToArray());
        }

        if (request.JsonOutput)
        {
            body["response_format"] = new JsonObject { ["type"] = "json_object" };
        }

        return body;
    }

    /// <summary>
    /// Тело запроса Responses API: сообщения и вызовы инструментов — элементами input, инструменты — без обёртки function.
    /// store = false — OpenAI не хранит переписку; strict = false — схемы инструментов не обязаны быть строгими.
    /// </summary>
    internal static JsonObject BuildResponsesBody(AiCompletionRequest request)
    {
        var input = new JsonArray();
        foreach (var m in request.Messages)
        {
            switch (m.Role)
            {
                case LlmRole.System:
                    input.Add(new JsonObject { ["role"] = "system", ["content"] = m.Content });
                    break;
                case LlmRole.User:
                    input.Add(new JsonObject { ["role"] = "user", ["content"] = m.Content });
                    break;
                case LlmRole.Tool:
                    input.Add(new JsonObject { ["type"] = "function_call_output", ["call_id"] = m.ToolCallId, ["output"] = m.Content });
                    break;
                default:
                    if (!string.IsNullOrEmpty(m.Content))
                    {
                        input.Add(new JsonObject { ["role"] = "assistant", ["content"] = m.Content });
                    }

                    foreach (var c in m.ToolCalls ?? [])
                    {
                        input.Add(new JsonObject { ["type"] = "function_call", ["call_id"] = c.Id, ["name"] = c.Name, ["arguments"] = c.Arguments.GetRawText() });
                    }

                    break;
            }
        }

        var body = new JsonObject
        {
            ["model"] = request.Model,
            ["input"] = input,
            ["max_output_tokens"] = request.MaxOutputTokens,
            ["store"] = false,
        };

        if (request.Temperature is { } temperature)
        {
            body["temperature"] = temperature;
        }

        if (request.ReasoningEffort is { } effort)
        {
            body["reasoning"] = new JsonObject { ["effort"] = effort };
        }

        if (request.Tools.Count > 0)
        {
            body["tools"] = new JsonArray(request.Tools.Select(t => (JsonNode?)new JsonObject
            {
                ["type"] = "function",
                ["name"] = t.Name,
                ["description"] = t.Description,
                ["parameters"] = JsonNode.Parse(t.InputSchema.GetRawText()),
                ["strict"] = false,
            }).ToArray());
        }

        if (request.JsonOutput)
        {
            body["text"] = new JsonObject { ["format"] = new JsonObject { ["type"] = "json_object" } };
        }

        return body;
    }

    /// <summary>
    /// Ответ Responses API: текст — части output_text сообщений, вызовы — элементы function_call (рассуждения пропускаются).
    /// Обрыв по лимиту токенов отдаётся как «length» — так же, как в Chat Completions.
    /// </summary>
    internal static AiCompletion ParseResponse(JsonElement root, string requestedModel)
    {
        var text = new List<string>();
        var calls = new List<LlmToolCall>();
        if (root.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in output.EnumerateArray())
            {
                switch (Str(item, "type"))
                {
                    case "message" when item.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array:
                        text.AddRange(content.EnumerateArray().Where(c => Str(c, "type") == "output_text").Select(c => Str(c, "text")).OfType<string>());
                        break;
                    case "function_call":
                        calls.Add(new LlmToolCall(Str(item, "call_id") ?? Str(item, "id") ?? Guid.NewGuid().ToString("N"), Str(item, "name") ?? string.Empty,
                            ParseArguments(Str(item, "arguments"))));
                        break;
                }
            }
        }

        var truncated = Str(root, "status") == "incomplete"
            && root.TryGetProperty("incomplete_details", out var details) && Str(details, "reason") == "max_output_tokens";
        var stop = truncated ? "length" : calls.Count > 0 ? "tool_calls" : "stop";
        var usage = root.TryGetProperty("usage", out var u) ? new AiTokenUsage(Int(u, "input_tokens"), Int(u, "output_tokens")) : AiTokenUsage.None;
        return new AiCompletion(text.Count == 0 ? null : string.Concat(text), calls, usage, Str(root, "model") ?? requestedModel, stop);
    }

    private static JsonObject Assistant(LlmMessage m)
    {
        var message = new JsonObject
        {
            ["role"] = "assistant",
            ["content"] = string.IsNullOrEmpty(m.Content) ? null : m.Content,
        };

        if (m.ToolCalls is { Count: > 0 } calls)
        {
            message["tool_calls"] = new JsonArray(calls.Select(c => (JsonNode?)new JsonObject
            {
                ["id"] = c.Id,
                ["type"] = "function",
                ["function"] = new JsonObject { ["name"] = c.Name, ["arguments"] = c.Arguments.GetRawText() },
            }).ToArray());
        }

        return message;
    }

    internal static AiCompletion ParseCompletion(JsonElement root, string requestedModel)
    {
        var choice = root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0
            ? choices[0]
            : default;
        var message = choice.ValueKind == JsonValueKind.Object && choice.TryGetProperty("message", out var msg) ? msg : default;

        var calls = new List<LlmToolCall>();
        if (message.ValueKind == JsonValueKind.Object && message.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.ValueKind == JsonValueKind.Array)
        {
            foreach (var call in toolCalls.EnumerateArray())
            {
                if (!call.TryGetProperty("function", out var function))
                {
                    continue;
                }

                calls.Add(new LlmToolCall(Str(call, "id") ?? Guid.NewGuid().ToString("N"), Str(function, "name") ?? string.Empty, ParseArguments(Str(function, "arguments"))));
            }
        }

        var usage = root.TryGetProperty("usage", out var u) ? new AiTokenUsage(Int(u, "prompt_tokens"), Int(u, "completion_tokens")) : AiTokenUsage.None;
        return new AiCompletion(Str(message, "content"), calls, usage, Str(root, "model") ?? requestedModel, Str(choice, "finish_reason"));
    }
}
