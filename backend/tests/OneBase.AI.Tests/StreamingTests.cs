using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using OneBase.AI.Consultant;
using OneBase.AI.Gateway;
using OneBase.AI.Grounding;
using OneBase.AI.Llm;
using OneBase.AI.Providers;

namespace OneBase.AI.Tests;

/// <summary>
/// Потоковая отдача итогового ответа: разбор потока Anthropic (текст по мере прихода, тот же ответ целиком, что без потока, ошибки и обрыв),
/// сброс показанного текста при повторе в AI Gateway, черновик консультанта — текст, «проверяется», при исправлении — «переписывается»
/// и новый текст; итог — только проверенный ответ.
/// </summary>
public class StreamingTests
{
    private static Stream Sse(params (string Event, string Data)[] events)
    {
        var sb = new StringBuilder();
        foreach (var (name, data) in events)
        {
            sb.Append("event: ").Append(name).Append('\n').Append("data: ").Append(data).Append("\n\n");
        }

        return new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
    }

    private static readonly (string, string)[] Answer =
    [
        ("message_start", """{"type":"message_start","message":{"id":"msg_1","type":"message","role":"assistant","content":[],"model":"claude-opus-5-5","stop_reason":null,"usage":{"input_tokens":100,"cache_read_input_tokens":50,"cache_creation_input_tokens":0,"output_tokens":1}}}"""),
        ("content_block_start", """{"type":"content_block_start","index":0,"content_block":{"type":"thinking","thinking":"","signature":""}}"""),
        ("content_block_delta", """{"type":"content_block_delta","index":0,"delta":{"type":"thinking_delta","thinking":"Считаю"}}"""),
        ("content_block_delta", """{"type":"content_block_delta","index":0,"delta":{"type":"signature_delta","signature":"sig-1"}}"""),
        ("content_block_stop", """{"type":"content_block_stop","index":0}"""),
        ("ping", """{"type":"ping"}"""),
        ("content_block_start", """{"type":"content_block_start","index":1,"content_block":{"type":"text","text":""}}"""),
        ("content_block_delta", """{"type":"content_block_delta","index":1,"delta":{"type":"text_delta","text":"Продажи "}}"""),
        ("content_block_delta", """{"type":"content_block_delta","index":1,"delta":{"type":"text_delta","text":"за сентябрь — "}}"""),
        ("content_block_delta", """{"type":"content_block_delta","index":1,"delta":{"type":"text_delta","text":"1 400 кг."}}"""),
        ("content_block_stop", """{"type":"content_block_stop","index":1}"""),
        ("message_delta", """{"type":"message_delta","delta":{"stop_reason":"end_turn","stop_sequence":null},"usage":{"output_tokens":42}}"""),
        ("message_stop", """{"type":"message_stop"}"""),
    ];

    [Fact]
    public async Task Anthropic_stream_gives_text_as_it_comes_and_the_same_completion_as_without_stream()
    {
        var chunks = new List<string>();
        var streamed = await AnthropicProvider.ReadStreamAsync(Sse(Answer), "claude-opus-5-5", t =>
        {
            chunks.Add(t);
            return Task.CompletedTask;
        }, CancellationToken.None);

        Assert.Equal(["Продажи ", "за сентябрь — ", "1 400 кг."], chunks);
        using var whole = JsonDocument.Parse("""
            {"id":"msg_1","type":"message","role":"assistant","model":"claude-opus-5-5","stop_reason":"end_turn",
             "content":[{"type":"thinking","thinking":"Считаю","signature":"sig-1"},{"type":"text","text":"Продажи за сентябрь — 1 400 кг."}],
             "usage":{"input_tokens":100,"cache_read_input_tokens":50,"cache_creation_input_tokens":0,"output_tokens":42}}
            """);
        var plain = AnthropicProvider.ParseCompletion(whole.RootElement, "claude-opus-5-5");
        Assert.Equal(plain.Text, streamed.Text);
        Assert.Equal(plain.Usage, streamed.Usage);
        Assert.Equal(new AiTokenUsage(100, 42, 50, 0), streamed.Usage);
        Assert.Equal((plain.StopReason, plain.Model), (streamed.StopReason, streamed.Model));
        // Блоки для продолжения диалога — с рассуждениями и подписью, как без потока.
        Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(System.Text.Json.Nodes.JsonNode.Parse(plain.Raw!.Value.GetRawText()),
            System.Text.Json.Nodes.JsonNode.Parse(streamed.Raw!.Value.GetRawText())));
        Assert.Contains("Считаю", streamed.Raw!.Value.GetRawText());
    }

    [Fact]
    public async Task Anthropic_stream_collects_tool_calls_from_json_pieces()
    {
        var streamed = await AnthropicProvider.ReadStreamAsync(Sse(
            ("message_start", """{"type":"message_start","message":{"model":"claude-opus-5-5","usage":{"input_tokens":10,"output_tokens":1}}}"""),
            ("content_block_start", """{"type":"content_block_start","index":0,"content_block":{"type":"tool_use","id":"toolu_1","name":"get_sales","input":{}}}"""),
            ("content_block_delta", """{"type":"content_block_delta","index":0,"delta":{"type":"input_json_delta","partial_json":"{\"year\": 20"}}"""),
            ("content_block_delta", """{"type":"content_block_delta","index":0,"delta":{"type":"input_json_delta","partial_json":"26, \"month\": 9}"}}"""),
            ("content_block_stop", """{"type":"content_block_stop","index":0}"""),
            ("message_delta", """{"type":"message_delta","delta":{"stop_reason":"tool_use"},"usage":{"output_tokens":20}}"""),
            ("message_stop", """{"type":"message_stop"}""")), "claude-opus-5-5", _ => Task.CompletedTask, CancellationToken.None);

        var call = Assert.Single(streamed.ToolCalls);
        Assert.Equal(("toolu_1", "get_sales", 2026, 9), (call.Id, call.Name, call.Arguments.GetProperty("year").GetInt32(), call.Arguments.GetProperty("month").GetInt32()));
        Assert.Null(streamed.Text);
        Assert.Equal("tool_use", streamed.StopReason);
    }

    [Fact]
    public async Task Anthropic_stream_error_or_cut_stream_is_a_provider_error()
    {
        var overloaded = await Assert.ThrowsAsync<AiProviderException>(() => AnthropicProvider.ReadStreamAsync(Sse(
            Answer[0], Answer[6], Answer[7],
            ("error", """{"type":"error","error":{"type":"overloaded_error","message":"Overloaded"}}""")), "m", _ => Task.CompletedTask, CancellationToken.None));
        Assert.True(overloaded.Transient);
        Assert.Contains("Overloaded", overloaded.Message);

        var cut = await Assert.ThrowsAsync<AiProviderException>(() =>
            AnthropicProvider.ReadStreamAsync(Sse(Answer[..9]), "m", _ => Task.CompletedTask, CancellationToken.None));
        Assert.True(cut.Transient);
    }

    [Fact]
    public async Task Gateway_streams_text_and_resets_it_before_a_retry()
    {
        var provider = new StreamingProvider("anthropic", ["Прод", "ажи"]) { FailAfterFirstChunk = 1 };
        var gateway = new AiGateway(new SingleSettings(new AiModelRef("anthropic", "claude-opus-5-5")), new AiProviderRegistry([provider]), new NoRecorder(),
            NullLogger<AiGateway>.Instance, OneBase.AI.Monitoring.NoAiLimits.Instance);
        var events = new List<string>();
        var stream = new RecordingStream(events);

        var result = await gateway.CompleteAsync([new(LlmRole.User, "Как продажи?")], [], new AiCallOptions { Stream = stream });

        Assert.Equal("Продажи", result.Text);
        Assert.Equal(["text:Прод", "reset", "text:Прод", "text:ажи"], events);

        // Без приёмника потока — обычный запрос целиком.
        events.Clear();
        await gateway.CompleteAsync([new(LlmRole.User, "Как продажи?")], [], new AiCallOptions());
        Assert.Empty(events);
        Assert.Equal(1, provider.Plain);
    }

    [Fact]
    public async Task Draft_is_written_then_checked_and_a_revision_replaces_it()
    {
        var evidence = new EvidenceSet();
        evidence.AddContent("""{"factKg":1400}""");
        var gateway = new DraftGateway("Продажи — 9 999 кг.", "Продажи — 1 400 кг.");
        var drafts = new List<string>();

        var answer = await new GroundedComposer(gateway).ComposeAsync([new(LlmRole.User, "Сколько продали?")], new AiCallOptions(), evidence, "Сколько продали?",
            CancellationToken.None, draft: d =>
            {
                drafts.Add(d.Text is null ? d.Kind : $"{d.Kind}:{d.Text}");
                return Task.CompletedTask;
            });

        Assert.Equal(["text:Продажи — 9 999 кг.", "checking", "revising", "text:Продажи — 1 400 кг.", "checking"], drafts);
        Assert.Equal("Продажи — 1 400 кг.", answer.Text);
        Assert.True(answer.Grounding.Revised);
        Assert.False(answer.Grounding.Warned);

        // Подтверждённый сразу ответ — без исправления.
        drafts.Clear();
        await new GroundedComposer(new DraftGateway("Продажи — 1 400 кг.")).ComposeAsync([new(LlmRole.User, "Сколько?")], new AiCallOptions(), evidence, "Сколько?",
            CancellationToken.None, draft: d =>
            {
                drafts.Add(d.Kind);
                return Task.CompletedTask;
            });
        Assert.Equal(["text", "checking"], drafts);
    }

    /// <summary>Шлюз, который «стримит» каждый ответ одним фрагментом, если ему дали приёмник потока.</summary>
    private sealed class DraftGateway(params string[] replies) : IAiGateway
    {
        private readonly Queue<string> _replies = new(replies);

        public Task<AiReadiness> GetReadinessAsync(CancellationToken cancellationToken = default) => Task.FromResult(new AiReadiness(true, null));

        public async Task<AiCallResult> CompleteAsync(IReadOnlyList<LlmMessage> messages, IReadOnlyList<LlmToolDefinition> tools, AiCallOptions options,
            CancellationToken cancellationToken = default)
        {
            var text = _replies.Dequeue();
            if (options.Stream is { } stream)
            {
                await stream.TextAsync(text);
            }

            return new AiCallResult(text, [], new AiTokenUsage(10, 5), new AiModelRef("fake", "consultant"), false, 1, "stop");
        }

        public Task<(IReadOnlyList<float[]> Vectors, AiModelRef Model)> EmbedAsync(IReadOnlyList<string> inputs, AiCallContext context,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingStream(List<string> events) : IAiTextStream
    {
        public Task TextAsync(string text)
        {
            events.Add($"text:{text}");
            return Task.CompletedTask;
        }

        public Task ResetAsync()
        {
            events.Add("reset");
            return Task.CompletedTask;
        }
    }

    /// <summary>Провайдер с потоком: отдаёт фрагменты; FailAfterFirstChunk раз обрывается после первого фрагмента (временная ошибка).</summary>
    private sealed class StreamingProvider(string code, string[] chunks) : IAiProvider, IAiStreamingProvider
    {
        public int FailAfterFirstChunk { get; set; }
        public int Plain { get; private set; }
        public string Code => code;
        public string Name => code;
        public string DefaultBaseUrl => $"https://{code}.test";
        public bool SupportsEmbeddings => false;

        public Task<AiCompletion> CompleteAsync(AiCredentials credentials, AiCompletionRequest request, CancellationToken cancellationToken = default)
        {
            Plain++;
            return Task.FromResult(new AiCompletion(string.Concat(chunks), [], new AiTokenUsage(10, 5), request.Model, "end_turn"));
        }

        public async Task<AiCompletion> StreamAsync(AiCredentials credentials, AiCompletionRequest request, Func<string, Task> onText,
            CancellationToken cancellationToken = default)
        {
            foreach (var chunk in chunks)
            {
                await onText(chunk);
                if (FailAfterFirstChunk > 0)
                {
                    FailAfterFirstChunk--;
                    throw new AiProviderException(code, null, "Поток оборвался.", true);
                }
            }

            return new AiCompletion(string.Concat(chunks), [], new AiTokenUsage(10, 5), request.Model, "end_turn");
        }

        public Task<IReadOnlyList<AiProviderModel>> ListModelsAsync(AiCredentials credentials, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AiProviderModel>>([]);

        public Task<(IReadOnlyList<float[]> Vectors, AiTokenUsage Usage)> EmbedAsync(AiCredentials credentials, string model, IReadOnlyList<string> inputs,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class SingleSettings(AiModelRef primary) : IAiSettingsSource
    {
        public Task<AiSettingsSnapshot> GetSettingsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AiSettingsSnapshot(true, primary, null, null, null, null, 2048, false));

        public Task<AiProviderState?> GetProviderAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult<AiProviderState?>(new AiProviderState(code, code, $"https://{code}.test", false, true, "key", AiKeySource.OneBase, null));
    }

    private sealed class NoRecorder : IAiUsageRecorder
    {
        public Task RecordAsync(AiCallRecord record, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
