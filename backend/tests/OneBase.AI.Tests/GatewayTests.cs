using Microsoft.Extensions.Logging.Abstractions;
using OneBase.AI.Gateway;
using OneBase.AI.Llm;
using OneBase.AI.Providers;

namespace OneBase.AI.Tests;

public class GatewayTests
{
    private static readonly AiModelRef Primary = new("openai", "main-model");
    private static readonly AiModelRef Reserve = new("anthropic", "reserve-model");
    private static readonly LlmMessage[] Question = [new(LlmRole.User, "Как продажи?")];

    private static (AiGateway Gateway, FakeProvider OpenAi, FakeProvider Anthropic, Recorder Recorder) Build(
        AiSettingsSnapshot? settings = null, bool anthropicKey = true)
    {
        var openAi = new FakeProvider("openai");
        var anthropic = new FakeProvider("anthropic");
        var source = new FakeSettings(
            settings ?? new AiSettingsSnapshot(true, Primary, Reserve, null, null, null, 2048, false),
            new()
            {
                ["openai"] = new AiProviderState("openai", "OpenAI", "https://openai.test", false, true, "sk-test-key", AiKeySource.OneBase, "••••-key"),
                ["anthropic"] = new AiProviderState("anthropic", "Anthropic", "https://anthropic.test", false, true, anthropicKey ? "ant-key" : "", AiKeySource.OneBase, null),
            });
        var recorder = new Recorder();
        var gateway = new AiGateway(source, new AiProviderRegistry([openAi, anthropic]), recorder, NullLogger<AiGateway>.Instance);
        return (gateway, openAi, anthropic, recorder);
    }

    [Fact]
    public async Task Answers_with_the_primary_model()
    {
        var (gateway, openAi, anthropic, recorder) = Build();

        var result = await gateway.CompleteAsync(Question, [], new AiCallOptions { Context = new AiCallContext(Purpose: "test") });

        Assert.Equal("ответ openai", result.Text);
        Assert.Equal(Primary, result.Model);
        Assert.False(result.UsedFallback);
        Assert.Equal(2048, openAi.Requests.Single().MaxOutputTokens);
        Assert.Empty(anthropic.Requests);
        var record = Assert.Single(recorder.Records);
        Assert.True(record.Success);
        Assert.Equal(new AiTokenUsage(10, 5), record.Usage);
    }

    [Fact]
    public async Task Server_error_is_retried_once_then_reserve_answers()
    {
        var (gateway, openAi, anthropic, recorder) = Build();
        openAi.Fail(new AiProviderException("openai", 503, "Провайдер временно недоступен (HTTP 503).", true), times: 2);

        var result = await gateway.CompleteAsync(Question, [], new AiCallOptions());

        Assert.Equal(2, openAi.Requests.Count);
        Assert.Single(anthropic.Requests);
        Assert.True(result.UsedFallback);
        Assert.Equal(Reserve, result.Model);
        Assert.Equal([false, false, true], recorder.Records.Select(r => r.Success));
    }

    [Fact]
    public async Task Rate_limit_goes_straight_to_reserve()
    {
        var (gateway, openAi, anthropic, _) = Build();
        openAi.Fail(new AiProviderException("openai", 429, "Превышен лимит.", true), times: 1);

        var result = await gateway.CompleteAsync(Question, [], new AiCallOptions());

        Assert.Single(openAi.Requests);
        Assert.Equal("ответ anthropic", result.Text);
    }

    [Fact]
    public async Task Both_failures_are_reported_together()
    {
        var (gateway, openAi, _, _) = Build(anthropicKey: false);
        openAi.Fail(new AiProviderException("openai", 401, "Ключ API не принят провайдером.", false), times: 1);

        var error = await Assert.ThrowsAsync<AiProviderException>(() => gateway.CompleteAsync(Question, [], new AiCallOptions()));

        Assert.Contains("Основная модель: Ключ API не принят", error.Message);
        Assert.Contains("Резервная модель: Нет ключа API anthropic", error.Message);
    }

    [Fact]
    public async Task Model_test_without_fallback_uses_only_the_given_model()
    {
        var (gateway, openAi, anthropic, _) = Build();
        openAi.Fail(new AiProviderException("openai", 404, "Модель не найдена.", false), times: 1);

        await Assert.ThrowsAsync<AiProviderException>(() =>
            gateway.CompleteAsync(Question, [], new AiCallOptions { Model = Primary, AllowFallback = false }));

        Assert.Empty(anthropic.Requests);
    }

    [Fact]
    public async Task Not_configured_ai_explains_what_to_do()
    {
        var (gateway, _, _, _) = Build(new AiSettingsSnapshot(true, null, null, null, null, null, 2048, false));

        var readiness = await gateway.GetReadinessAsync();
        var error = await Assert.ThrowsAsync<LlmNotConfiguredException>(() => gateway.CompleteAsync(Question, [], new AiCallOptions()));

        Assert.False(readiness.Ready);
        Assert.Contains("Настройки → AI", error.Message);
    }

    [Fact]
    public async Task Disabled_ai_does_not_call_providers()
    {
        var (gateway, openAi, _, _) = Build(new AiSettingsSnapshot(false, Primary, null, null, null, null, 2048, false));

        var error = await Assert.ThrowsAsync<LlmNotConfiguredException>(() => gateway.CompleteAsync(Question, [], new AiCallOptions()));

        Assert.Contains("выключен", error.Message);
        Assert.Empty(openAi.Requests);
    }

    private sealed class FakeSettings(AiSettingsSnapshot settings, Dictionary<string, AiProviderState> providers) : IAiSettingsSource
    {
        public Task<AiSettingsSnapshot> GetSettingsAsync(CancellationToken ct = default) => Task.FromResult(settings);

        public Task<AiProviderState?> GetProviderAsync(string code, CancellationToken ct = default) =>
            Task.FromResult(providers.GetValueOrDefault(code));
    }

    private sealed class Recorder : IAiUsageRecorder
    {
        public List<AiCallRecord> Records { get; } = [];

        public Task RecordAsync(AiCallRecord record, CancellationToken cancellationToken = default)
        {
            Records.Add(record);
            return Task.CompletedTask;
        }
    }
}

internal sealed class FakeProvider(string code) : IAiProvider
{
    private readonly Queue<AiProviderException> _failures = new();

    public string Code => code;
    public string Name => code;
    public string DefaultBaseUrl => $"https://{code}.test";
    public bool SupportsEmbeddings => false;
    public List<AiCompletionRequest> Requests { get; } = [];

    /// <summary>Ответы по очереди; когда очередь пуста — ответ «ответ {code}».</summary>
    public Queue<AiCompletion> Replies { get; } = new();

    public void Fail(AiProviderException error, int times)
    {
        for (var i = 0; i < times; i++)
        {
            _failures.Enqueue(error);
        }
    }

    public Task<AiCompletion> CompleteAsync(AiCredentials credentials, AiCompletionRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        if (_failures.TryDequeue(out var error))
        {
            throw error;
        }

        return Task.FromResult(Replies.TryDequeue(out var reply) ? reply : new AiCompletion($"ответ {code}", [], new AiTokenUsage(10, 5), request.Model, "stop"));
    }

    public Task<IReadOnlyList<AiProviderModel>> ListModelsAsync(AiCredentials credentials, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AiProviderModel>>([]);

    public Task<(IReadOnlyList<float[]> Vectors, AiTokenUsage Usage)> EmbedAsync(
        AiCredentials credentials, string model, IReadOnlyList<string> inputs, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
