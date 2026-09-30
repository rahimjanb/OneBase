using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OneBase.AI.Llm;
using OneBase.AI.Providers;

namespace OneBase.AI.Tests;

public class ProviderTests
{
    private static readonly JsonElement Schema = JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new { month = new { type = "string" } },
    });

    private static readonly LlmToolCall Call = new("call_1", "get_sales", JsonSerializer.SerializeToElement(new { month = "2026-09" }));

    private static AiCompletionRequest Conversation(double? temperature = null) => new(
        "model-x",
        [
            new LlmMessage(LlmRole.System, "Ты консультант."),
            new LlmMessage(LlmRole.User, "Как продажи?"),
            new LlmMessage(LlmRole.Assistant, "", ToolCalls: [Call, Call with { Id = "call_2" }]),
            new LlmMessage(LlmRole.Tool, "{\"kg\":100}", ToolCallId: "call_1"),
            new LlmMessage(LlmRole.Tool, "{\"kg\":200}", ToolCallId: "call_2"),
        ],
        [new LlmToolDefinition("get_sales", "Продажи за месяц", Schema)],
        temperature,
        1024);

    [Fact]
    public void OpenAi_request_maps_roles_tool_calls_and_tools()
    {
        var body = OpenAiProvider.BuildBody(Conversation());
        var messages = body["messages"]!.AsArray();

        Assert.Equal("model-x", (string?)body["model"]);
        Assert.Equal(1024, (int)body["max_completion_tokens"]!);
        Assert.False(body.ContainsKey("temperature"));
        Assert.Equal(["system", "user", "assistant", "tool", "tool"], messages.Select(m => (string?)m!["role"]));

        var assistant = messages[2]!;
        Assert.Null(assistant["content"]);
        var call = assistant["tool_calls"]![0]!;
        Assert.Equal("function", (string?)call["type"]);
        Assert.Equal("get_sales", (string?)call["function"]!["name"]);
        Assert.Equal("{\"month\":\"2026-09\"}", (string?)call["function"]!["arguments"]);
        Assert.Equal("call_2", (string?)messages[4]!["tool_call_id"]);

        var tool = body["tools"]![0]!;
        Assert.Equal("function", (string?)tool["type"]);
        Assert.Equal("object", (string?)tool["function"]!["parameters"]!["type"]);
    }

    [Fact]
    public void OpenAi_request_sends_temperature_and_json_mode_only_when_asked()
    {
        var body = OpenAiProvider.BuildBody(Conversation(0.2) with { JsonOutput = true });

        Assert.Equal(0.2, (double)body["temperature"]!);
        Assert.Equal("json_object", (string?)body["response_format"]!["type"]);
    }

    [Fact]
    public void OpenAi_response_gives_text_tool_calls_and_usage()
    {
        using var doc = JsonDocument.Parse("""
            {"model":"model-x-2026","choices":[{"finish_reason":"tool_calls","message":{"role":"assistant","content":null,
             "tool_calls":[{"id":"c1","type":"function","function":{"name":"get_sales","arguments":"{\"month\":\"2026-08\"}"}},
                           {"id":"c2","type":"function","function":{"name":"get_sales","arguments":"не json"}}]}}],
             "usage":{"prompt_tokens":120,"completion_tokens":30}}
            """);

        var result = OpenAiProvider.ParseCompletion(doc.RootElement, "model-x");

        Assert.Null(result.Text);
        Assert.Equal("model-x-2026", result.Model);
        Assert.Equal("tool_calls", result.StopReason);
        Assert.Equal(new AiTokenUsage(120, 30), result.Usage);
        Assert.Equal("2026-08", result.ToolCalls[0].Arguments.GetProperty("month").GetString());
        Assert.Equal(JsonValueKind.Object, result.ToolCalls[1].Arguments.ValueKind);
    }

    [Fact]
    public void OpenAi_responses_request_maps_messages_calls_outputs_and_tools()
    {
        var body = OpenAiProvider.BuildResponsesBody(Conversation() with { JsonOutput = true });
        var input = body["input"]!.AsArray();

        Assert.Equal(1024, (int)body["max_output_tokens"]!);
        Assert.False((bool)body["store"]!);
        Assert.False(body.ContainsKey("temperature"));
        Assert.Equal(["system", "user", null, null, null, null], input.Select(i => (string?)i!["role"]));
        Assert.Equal([null, null, "function_call", "function_call", "function_call_output", "function_call_output"], input.Select(i => (string?)i!["type"]));
        Assert.Equal(("call_1", "get_sales", "{\"month\":\"2026-09\"}"), ((string?)input[2]!["call_id"], (string?)input[2]!["name"], (string?)input[2]!["arguments"]));
        Assert.Equal(("call_2", "{\"kg\":200}"), ((string?)input[5]!["call_id"], (string?)input[5]!["output"]));

        var tool = body["tools"]![0]!;
        Assert.Equal(("function", "get_sales", false), ((string?)tool["type"], (string?)tool["name"], (bool)tool["strict"]!));
        Assert.Equal("object", (string?)tool["parameters"]!["type"]);
        Assert.Equal("json_object", (string?)body["text"]!["format"]!["type"]);
    }

    [Fact]
    public void OpenAi_responses_answer_gives_text_calls_usage_and_truncation()
    {
        using var calls = JsonDocument.Parse("""
            {"model":"model-x-2026","status":"completed","output":[
              {"type":"reasoning","summary":[]},
              {"type":"message","role":"assistant","content":[{"type":"output_text","text":"Смотрю продажи."}]},
              {"type":"function_call","id":"fc_1","call_id":"call_9","name":"get_sales","arguments":"{\"month\":\"2026-08\"}"}],
             "usage":{"input_tokens":120,"output_tokens":30}}
            """);
        using var truncated = JsonDocument.Parse("""
            {"status":"incomplete","incomplete_details":{"reason":"max_output_tokens"},"output":[{"type":"reasoning","summary":[]}]}
            """);

        var result = OpenAiProvider.ParseResponse(calls.RootElement, "model-x");
        var cut = OpenAiProvider.ParseResponse(truncated.RootElement, "model-x");

        Assert.Equal(("Смотрю продажи.", "model-x-2026", "tool_calls"), (result.Text, result.Model, result.StopReason));
        Assert.Equal(new AiTokenUsage(120, 30), result.Usage);
        var call = Assert.Single(result.ToolCalls);
        Assert.Equal(("call_9", "get_sales", "2026-08"), (call.Id, call.Name, call.Arguments.GetProperty("month").GetString()));
        Assert.Equal(((string?)null, "length", "model-x"), (cut.Text, cut.StopReason, cut.Model));
    }

    [Fact]
    public async Task OpenAi_switches_to_responses_api_when_chat_completions_refuses_tools_and_remembers_it()
    {
        var factory = new StubHttp(
            (HttpStatusCode.BadRequest, """{"error":{"message":"Function tools with reasoning_effort are not supported for model-x in /v1/chat/completions. To use function tools, use /v1/responses or set reasoning_effort to 'none'.","type":"invalid_request_error"}}"""),
            (HttpStatusCode.OK, """{"status":"completed","output":[{"type":"message","content":[{"type":"output_text","text":"Готово."}]}]}"""),
            (HttpStatusCode.OK, """{"status":"completed","output":[{"type":"message","content":[{"type":"output_text","text":"Ещё."}]}]}"""));
        var provider = new OpenAiProvider(factory);
        var credentials = new AiCredentials("key-123456789", "https://api.example.test/v1");

        var first = await provider.CompleteAsync(credentials, Conversation());
        var second = await provider.CompleteAsync(credentials, Conversation());

        Assert.Equal(("Готово.", "Ещё."), (first.Text, second.Text));
        Assert.Equal(
            ["https://api.example.test/v1/chat/completions", "https://api.example.test/v1/responses", "https://api.example.test/v1/responses"],
            factory.Requests.Select(r => r.RequestUri!.ToString()));
    }

    [Fact]
    public async Task OpenAi_other_bad_requests_are_not_retried_on_responses_api()
    {
        var factory = new StubHttp(HttpStatusCode.BadRequest, """{"error":{"message":"Invalid schema for function 'get_sales'.","type":"invalid_request_error"}}""");

        var error = await Assert.ThrowsAsync<AiProviderException>(() =>
            new OpenAiProvider(factory).CompleteAsync(new AiCredentials("key-123456789", "https://api.example.test/v1"), Conversation()));

        Assert.Equal(400, error.StatusCode);
        Assert.Single(factory.Requests);
    }

    [Fact]
    public void Anthropic_request_puts_system_on_top_and_groups_tool_results()
    {
        var body = AnthropicProvider.BuildBody(Conversation(0.3));
        var messages = body["messages"]!.AsArray();

        Assert.Equal("Ты консультант.", (string?)body["system"]);
        Assert.Equal(1024, (int)body["max_tokens"]!);
        Assert.Equal(0.3, (double)body["temperature"]!);
        Assert.Equal(["user", "assistant", "user"], messages.Select(m => (string?)m!["role"]));

        var assistant = messages[1]!["content"]!.AsArray();
        Assert.Equal(2, assistant.Count); // пустой текст не отправляется
        Assert.All(assistant, b => Assert.Equal("tool_use", (string?)b!["type"]));
        Assert.Equal("2026-09", (string?)assistant[0]!["input"]!["month"]);

        var results = messages[2]!["content"]!.AsArray();
        Assert.Equal(["call_1", "call_2"], results.Select(r => (string?)r!["tool_use_id"]));
        Assert.All(results, r => Assert.Equal("tool_result", (string?)r!["type"]));

        var tool = body["tools"]![0]!;
        Assert.Equal("get_sales", (string?)tool["name"]);
        Assert.Equal("object", (string?)tool["input_schema"]!["type"]);
    }

    [Fact]
    public void Anthropic_response_gives_text_tool_calls_and_usage()
    {
        using var doc = JsonDocument.Parse("""
            {"model":"claude-x","stop_reason":"tool_use","content":[
              {"type":"text","text":"Смотрю продажи."},
              {"type":"tool_use","id":"tu_1","name":"get_sales","input":{"month":"2026-09"}}],
             "usage":{"input_tokens":50,"output_tokens":12}}
            """);

        var result = AnthropicProvider.ParseCompletion(doc.RootElement, "claude");

        Assert.Equal("Смотрю продажи.", result.Text);
        Assert.Equal("claude-x", result.Model);
        Assert.Equal("tool_use", result.StopReason);
        Assert.Equal(new AiTokenUsage(50, 12), result.Usage);
        var call = Assert.Single(result.ToolCalls);
        Assert.Equal(("tu_1", "get_sales"), (call.Id, call.Name));
        Assert.Equal("2026-09", call.Arguments.GetProperty("month").GetString());
    }

    [Fact]
    public async Task Provider_error_never_contains_the_key()
    {
        const string key = "sk-proj-SECRETSECRETSECRET1234";
        var factory = new StubHttp(HttpStatusCode.Unauthorized,
            """{"error":{"message":"Incorrect API key provided: sk-proj-SECR************1234. You can find your API key at https://platform.openai.com.","type":"invalid_request_error"}}""");
        var provider = new OpenAiProvider(factory);

        var error = await Assert.ThrowsAsync<AiProviderException>(() =>
            provider.CompleteAsync(new AiCredentials(key, "https://api.example.test/v1"), Conversation()));

        Assert.Equal(401, error.StatusCode);
        Assert.False(error.Transient);
        Assert.DoesNotContain("SECR", error.Message);
        Assert.DoesNotContain("1234", error.Message);
        Assert.Contains("Ключ API не принят", error.Message);
        Assert.Equal("Bearer " + key, factory.LastRequest!.Headers.Authorization!.ToString());
        Assert.Equal("https://api.example.test/v1/chat/completions", factory.LastRequest.RequestUri!.ToString());
    }

    [Fact]
    public async Task Overloaded_provider_is_transient()
    {
        var provider = new AnthropicProvider(new StubHttp((HttpStatusCode)529, """{"type":"error","error":{"type":"overloaded_error","message":"Overloaded"}}"""));

        var error = await Assert.ThrowsAsync<AiProviderException>(() =>
            provider.CompleteAsync(new AiCredentials("key-123456789", "https://api.example.test"), Conversation()));

        Assert.True(error.Transient);
        Assert.Contains("временно недоступен", error.Message);
    }

    [Fact]
    public async Task Anthropic_sends_version_header_and_pages_models()
    {
        var factory = new StubHttp(
            (HttpStatusCode.OK, """{"data":[{"id":"m1","display_name":"Model 1","created_at":"2026-01-01T00:00:00Z"}],"has_more":true,"last_id":"m1"}"""),
            (HttpStatusCode.OK, """{"data":[{"id":"m2","display_name":"Model 2","created_at":"2026-02-01T00:00:00Z"}],"has_more":false,"last_id":"m2"}"""));
        var provider = new AnthropicProvider(factory);

        var models = await provider.ListModelsAsync(new AiCredentials("key-123456789", "https://api.example.test"));

        Assert.Equal(["m1", "m2"], models.Select(m => m.Id));
        Assert.Equal("Model 2", models[1].DisplayName);
        Assert.Contains("after_id=m1", factory.Requests[1].RequestUri!.Query);
        Assert.Equal(AnthropicProvider.ApiVersion, factory.Requests[0].Headers.GetValues("anthropic-version").Single());
        Assert.Equal("key-123456789", factory.Requests[0].Headers.GetValues("x-api-key").Single());
    }

    [Fact]
    public async Task Missing_key_is_reported_without_calling_the_provider()
    {
        var factory = new StubHttp(HttpStatusCode.OK, "{}");

        var error = await Assert.ThrowsAsync<AiProviderException>(() =>
            new OpenAiProvider(factory).ListModelsAsync(new AiCredentials("", "https://api.example.test/v1")));

        Assert.Contains("не задан", error.Message);
        Assert.Empty(factory.Requests);
    }
}

/// <summary>Отдаёт заготовленные ответы и запоминает запросы.</summary>
internal sealed class StubHttp : IHttpClientFactory
{
    private readonly Queue<(HttpStatusCode Status, string Body)> _responses;

    public StubHttp(HttpStatusCode status, string body) : this((status, body))
    {
    }

    public StubHttp(params (HttpStatusCode Status, string Body)[] responses) => _responses = new(responses);

    public List<HttpRequestMessage> Requests { get; } = [];

    public HttpRequestMessage? LastRequest => Requests.LastOrDefault();

    public HttpClient CreateClient(string name) => new(new Handler(this));

    private sealed class Handler(StubHttp owner) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            owner.Requests.Add(request);
            var (status, body) = owner._responses.Count > 1 ? owner._responses.Dequeue() : owner._responses.Peek();
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
