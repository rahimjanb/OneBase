using OneBase.AI.Agents;
using OneBase.AI.Gateway;
using OneBase.AI.Llm;
using OneBase.AI.Orchestration;
using OneBase.AI.Providers;

namespace OneBase.AI.Tests;

public class RouterTests
{
    private static AgentConfig Agent(string code) => new(code, code + " AI", "роль", "описание", "prompt", true, null, null, null, true, 1, code, null, []);

    private static readonly AgentConfig[] Available = [Agent("sales"), Agent("supply"), Agent("marketing")];

    private static AiRouter Router(string? reply) =>
        new(new ScriptedGateway(reply), new StaticSettings(new AiSettingsSnapshot(true, new("openai", "main"), null, new("openai", "fast"), null, null, 1024, false)));

    [Fact]
    public async Task Router_takes_agents_and_tasks_from_model()
    {
        var router = Router("""{"agents":["sales","supply"],"tasks":{"sales":"Продажи за сентябрь по регионам"},"reason":"продажи и остатки"}""");

        var decision = await router.RouteAsync("Почему продажи упали?", [], Available, new AiCallContext(), default);

        Assert.Equal(["sales", "supply"], decision.Agents);
        Assert.Equal("Продажи за сентябрь по регионам", decision.Tasks["sales"]);
        Assert.False(decision.Tasks.ContainsKey("supply"));
        Assert.False(decision.ByKeywords);
    }

    [Fact]
    public async Task Unavailable_agents_from_model_are_dropped()
    {
        var router = Router("""{"agents":["finance","sales"],"tasks":{"finance":"прибыль"},"reason":"x"}""");

        var decision = await router.RouteAsync("Почему упала прибыль?", [], Available, new AiCallContext(), default);

        Assert.Equal(["sales"], decision.Agents);
        Assert.Empty(decision.Tasks);
    }

    [Fact]
    public async Task Empty_list_means_consultant_answers_alone()
    {
        var decision = await Router("""{"agents":[],"reason":"приветствие"}""").RouteAsync("Привет!", [], Available, new AiCallContext(), default);

        Assert.Empty(decision.Agents);
    }

    [Fact]
    public async Task Unreadable_answer_falls_back_to_keywords()
    {
        var decision = await Router("не знаю").RouteAsync("Какие товары заканчиваются на складах?", [], Available, new AiCallContext(), default);

        Assert.True(decision.ByKeywords);
        Assert.Equal(["sales", "supply"], decision.Agents);
    }

    [Fact]
    public void Glossary_word_routes_to_sales()
    {
        var decision = AiRouter.ByKeywords("Как идёт помадка в Хоразме?", Available, AiTokenUsage.None, ["помадка", "хоразм"]);

        Assert.Equal(["sales"], decision.Agents);
    }

    [Fact]
    public void Short_glossary_stem_does_not_match_inside_other_words()
    {
        // «соки» → основа «sok»: внутри «высокий» она есть, но слово вопроса с неё не начинается — продажи не привлекаются.
        var decision = AiRouter.ByKeywords("Какой высокий остаток у поставщика?", Available, AiTokenUsage.None, ["соки"]);

        Assert.Equal(["supply"], decision.Agents);
    }

    [Fact]
    public void Question_without_keywords_goes_to_everyone()
    {
        var decision = AiRouter.ByKeywords("Что нам улучшить?", Available, AiTokenUsage.None);

        Assert.Equal(["sales", "supply", "marketing"], decision.Agents);
    }

    private sealed class ScriptedGateway(string? reply) : IAiGateway
    {
        public Task<AiReadiness> GetReadinessAsync(CancellationToken cancellationToken = default) => Task.FromResult(new AiReadiness(true, null));

        public Task<AiCallResult> CompleteAsync(IReadOnlyList<LlmMessage> messages, IReadOnlyList<LlmToolDefinition> tools, AiCallOptions options, CancellationToken cancellationToken = default)
        {
            Assert.True(options.JsonOutput);
            Assert.Equal("fast", options.Model?.Model);
            return Task.FromResult(new AiCallResult(reply, [], new AiTokenUsage(5, 5), options.Model!, false, 1, "stop"));
        }

        public Task<(IReadOnlyList<float[]> Vectors, AiModelRef Model)> EmbedAsync(IReadOnlyList<string> inputs, AiCallContext context, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StaticSettings(AiSettingsSnapshot settings) : IAiSettingsSource
    {
        public Task<AiSettingsSnapshot> GetSettingsAsync(CancellationToken ct = default) => Task.FromResult(settings);

        public Task<AiProviderState?> GetProviderAsync(string code, CancellationToken ct = default) => Task.FromResult<AiProviderState?>(null);
    }
}
