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
        AiSettingsSnapshot? settings = null, bool anthropicKey = true, OneBase.AI.Monitoring.IAiLimits? limits = null)
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
        var gateway = new AiGateway(source, new AiProviderRegistry([openAi, anthropic]), recorder, NullLogger<AiGateway>.Instance,
            limits ?? OneBase.AI.Monitoring.NoAiLimits.Instance);
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
    public async Task Turn_timings_record_each_attempt_queue_wait_and_tokens_without_text()
    {
        var (gateway, openAi, _, recorder) = Build(limits: new SlowLimits(TimeSpan.FromMilliseconds(80)));
        openAi.Fail(new AiProviderException("openai", 503, "Провайдер временно недоступен (HTTP 503).", true), times: 1);
        var timings = OneBase.AI.Monitoring.AiTurnTimings.Start();

        await gateway.CompleteAsync(Question, [], new AiCallOptions { Context = new AiCallContext(Purpose: "consultant.answer", AgentCode: "director") });

        var calls = timings.View().Calls;
        Assert.Equal([1, 2], calls.Select(c => c.Attempt));
        Assert.Equal([false, true], calls.Select(c => c.Success));
        Assert.All(calls, c => Assert.True(c.QueueMs >= 70, $"очередь {c.QueueMs} мс"));
        Assert.All(calls, c => Assert.Equal(("consultant.answer", "director", "openai/main-model", false), (c.Purpose, c.Agent, c.Model, c.Fallback)));
        Assert.Equal((10, 5), (calls[1].InputTokens, calls[1].OutputTokens));
        Assert.Equal([1, 2], recorder.Records.Select(r => r.Attempt));
        Assert.All(recorder.Records, r => Assert.True(r.QueueMs >= 70 && r.ElapsedMs >= r.QueueMs));
        Assert.DoesNotContain("продажи", System.Text.Json.JsonSerializer.Serialize(timings.View()), StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public async Task Model_without_temperature_support_is_retried_without_it_and_remembered()
    {
        var (gateway, openAi, anthropic, _) = Build(new AiSettingsSnapshot(true, Primary, Reserve, null, null, 0.2, 2048, false));
        openAi.Fail(new AiProviderException("openai", 400,
            "Провайдер отклонил запрос (HTTP 400). Ответ провайдера: Unsupported value: 'temperature' does not support 0.2 with this model.", false), times: 1);

        var first = await gateway.CompleteAsync(Question, [], new AiCallOptions());
        var second = await gateway.CompleteAsync(Question, [], new AiCallOptions());

        Assert.Equal(Primary, first.Model);
        Assert.Equal(Primary, second.Model);
        Assert.Empty(anthropic.Requests);
        Assert.Equal([0.2, null, null], openAi.Requests.Select(r => r.Temperature));
    }

    [Fact]
    public async Task Prompt_cache_is_on_by_default_and_switched_off_for_a_model_that_rejects_it()
    {
        var (gateway, openAi, _, _) = Build(new AiSettingsSnapshot(true, Primary, Reserve, null, null, null, 2048, false));
        openAi.Fail(new AiProviderException("openai", 400, "Провайдер отклонил запрос (HTTP 400). Ответ провайдера: cache_control: Extra inputs are not permitted", false), times: 1);

        await gateway.CompleteAsync(Question, [], new AiCallOptions());
        await gateway.CompleteAsync(Question, [], new AiCallOptions());

        // Первый запрос — с кэшем; после отказа повтор без кэша и все следующие — тоже; это не считается сбоем (без резервной модели).
        Assert.Equal([true, false, false], openAi.Requests.Select(r => r.CachePrompt));
    }

    [Fact]
    public async Task Caller_can_disable_the_prompt_cache_for_a_one_off_prompt()
    {
        var (gateway, openAi, _, _) = Build(new AiSettingsSnapshot(true, Primary, Reserve, null, null, null, 2048, false));

        await gateway.CompleteAsync(Question, [], new AiCallOptions { CachePrompt = false });

        Assert.False(Assert.Single(openAi.Requests).CachePrompt);
    }

    [Fact]
    public async Task Reasoning_effort_comes_from_settings_unless_the_call_sets_its_own()
    {
        var (gateway, openAi, _, _) = Build(new AiSettingsSnapshot(true, Primary, Reserve, null, null, null, 2048, false, "medium"));

        await gateway.CompleteAsync(Question, [], new AiCallOptions());
        await gateway.CompleteAsync(Question, [], new AiCallOptions { ReasoningEffort = "low" });

        Assert.Equal(["medium", "low"], openAi.Requests.Select(r => r.ReasoningEffort));
    }

    [Fact]
    public async Task Model_without_reasoning_effort_support_is_retried_without_it_and_remembered()
    {
        var (gateway, openAi, anthropic, _) = Build(new AiSettingsSnapshot(true, Primary, Reserve, null, null, null, 2048, false, "low"));
        openAi.Fail(new AiProviderException("openai", 400,
            "Провайдер отклонил запрос (HTTP 400). Ответ провайдера: Unrecognized request argument supplied: reasoning_effort", false), times: 1);

        var first = await gateway.CompleteAsync(Question, [], new AiCallOptions());
        var second = await gateway.CompleteAsync(Question, [], new AiCallOptions());

        Assert.Equal((Primary, Primary), (first.Model, second.Model));
        Assert.Empty(anthropic.Requests);
        Assert.Equal(["low", null, null], openAi.Requests.Select(r => r.ReasoningEffort));
    }

    [Fact]
    public async Task Empty_answer_cut_by_token_limit_explains_what_to_do()
    {
        var (gateway, openAi, _, _) = Build(new AiSettingsSnapshot(true, Primary, null, null, null, null, 1024, false));
        openAi.Replies.Enqueue(new AiCompletion(null, [], new AiTokenUsage(100, 1024), "main-model", "length"));

        var error = await Assert.ThrowsAsync<AiProviderException>(() => gateway.CompleteAsync(Question, [], new AiCallOptions()));

        Assert.Contains("Максимум токенов ответа", error.Message);
    }

    [Fact]
    public async Task Claude_refusal_is_not_an_answer_the_reserve_answers()
    {
        var claude = new AiModelRef("anthropic", "claude-opus-5-5");
        var (gateway, openAi, anthropic, _) = Build(new AiSettingsSnapshot(true, claude, Primary, null, null, null, 2048, false));
        anthropic.Replies.Enqueue(new AiCompletion(null, [], new AiTokenUsage(100, 0), "claude-opus-5-5", "refusal"));

        var result = await gateway.CompleteAsync(Question, [], new AiCallOptions());

        Assert.Single(anthropic.Requests);
        Assert.Single(openAi.Requests);
        Assert.True(result.UsedFallback);
        Assert.Equal("ответ openai", result.Text);
    }

    [Fact]
    public async Task Reply_carries_the_provider_content_and_tool_calls_can_be_forbidden()
    {
        var claude = new AiModelRef("anthropic", "claude-opus-5-5");
        var (gateway, _, anthropic, _) = Build(new AiSettingsSnapshot(true, claude, null, null, null, null, 16000, false));
        var raw = System.Text.Json.JsonSerializer.SerializeToElement(new object[] { new { type = "thinking", thinking = "", signature = "sig" }, new { type = "text", text = "Готово." } });
        anthropic.Replies.Enqueue(new AiCompletion("Готово.", [], new AiTokenUsage(10, 5), "claude-opus-5-5", "end_turn", raw));

        var result = await gateway.CompleteAsync(Question, [], new AiCallOptions { AllowToolCalls = false });

        Assert.False(anthropic.Requests.Single().AllowToolCalls);
        Assert.Equal(LlmRole.Assistant, result.Reply.Role);
        Assert.Equal(new LlmProviderContent("anthropic", raw), result.Reply.Raw);
    }

    private sealed class FakeSettings(AiSettingsSnapshot settings, Dictionary<string, AiProviderState> providers) : IAiSettingsSource
    {
        public Task<AiSettingsSnapshot> GetSettingsAsync(CancellationToken ct = default) => Task.FromResult(settings);

        public Task<AiProviderState?> GetProviderAsync(string code, CancellationToken ct = default) =>
            Task.FromResult(providers.GetValueOrDefault(code));
    }

    /// <summary>Лимит одновременных вызовов, который всякий раз заставляет ждать места.</summary>
    private sealed class SlowLimits(TimeSpan wait) : OneBase.AI.Monitoring.IAiLimits
    {
        public Task EnsureBudgetAsync(CancellationToken ct = default) => Task.CompletedTask;

        public async Task<IDisposable> EnterAsync(CancellationToken ct = default)
        {
            await Task.Delay(wait, ct);
            return await OneBase.AI.Monitoring.NoAiLimits.Instance.EnterAsync(ct);
        }

        public Task SpentAsync(decimal costUsd, CancellationToken ct = default) => Task.CompletedTask;

        public Task<OneBase.AI.Monitoring.AiBudgetStatus> StatusAsync(CancellationToken ct = default) => OneBase.AI.Monitoring.NoAiLimits.Instance.StatusAsync(ct);
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
