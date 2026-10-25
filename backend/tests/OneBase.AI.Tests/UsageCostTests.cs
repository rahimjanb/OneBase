using OneBase.AI.Monitoring;
using OneBase.AI.Providers;

namespace OneBase.AI.Tests;

public class UsageCostTests
{
    // Прайс Anthropic от 25.10.2026: Opus 5.5 — вход $4, выход $20, кэш-чтение $0.20, кэш-запись $5 за 1 млн.
    private static readonly AiModelPrices Opus = new(4m, 20m, 0.20m, 5m);

    [Fact]
    public void Cost_uses_prices_per_million_tokens()
    {
        // 12 000 входных по $2.50 за 1М и 800 выходных по $10 за 1М.
        Assert.Equal(0.038m, AiPricing.Cost(new AiTokenUsage(12_000, 800), new AiModelPrices(2.5m, 10m, null, null), "claude-sonnet-5-5"));
    }

    [Fact]
    public void Cache_tokens_are_priced_at_cache_rates_not_input_rate()
    {
        // 1 000 входных ($0.004) + 20 000 из кэша ($0.004) + 2 000 записано в кэш ($0.01) + 500 выходных ($0.01) = $0.028.
        var usage = new AiTokenUsage(1_000, 500, CacheReadTokens: 20_000, CacheWriteTokens: 2_000);
        Assert.Equal(0.028m, AiPricing.Cost(usage, Opus, "claude-opus-5-5"));

        // Те же токены без кэша стоили бы в разы дороже — именно это и экономит кэш промпта.
        Assert.Equal(0.102m, AiPricing.Cost(new AiTokenUsage(23_000, 500), Opus, "claude-opus-5-5"));
    }

    [Fact]
    public void Cache_tokens_without_cache_price_give_no_cost_instead_of_a_guess()
    {
        var usage = new AiTokenUsage(1_000, 500, CacheReadTokens: 20_000);
        Assert.Null(AiPricing.Cost(usage, new AiModelPrices(4m, 20m, null, 5m), "claude-opus-5-5"));
        Assert.Null(AiPricing.Cost(new AiTokenUsage(1_000, 500, CacheWriteTokens: 100), new AiModelPrices(4m, 20m, 0.2m, null), "claude-opus-5-5"));

        // Без кэш-токенов цены кэша не нужны.
        Assert.Equal(0.014m, AiPricing.Cost(new AiTokenUsage(1_000, 500), new AiModelPrices(4m, 20m, null, null), "claude-opus-5-5"));
    }

    [Fact]
    public void Haiku_long_prompt_costs_five_times_more_and_counts_cache_in_the_length()
    {
        // Haiku 5.5: $0.10 / $0.50 / $0.01 / $0.125; промпт 90 000 + 20 000 из кэша = 110 000 > 100 000 → тариф ×5.
        var haiku = new AiModelPrices(0.10m, 0.50m, 0.01m, 0.125m);
        var longPrompt = new AiTokenUsage(90_000, 1_000, CacheReadTokens: 20_000);
        var shortPrompt = new AiTokenUsage(70_000, 1_000, CacheReadTokens: 20_000);
        Assert.Equal(5, AiPricing.LongPromptMultiplier("claude-haiku-5-5", longPrompt.PromptTokens));
        Assert.Equal(1, AiPricing.LongPromptMultiplier("claude-haiku-5-5", shortPrompt.PromptTokens));
        Assert.Equal(1, AiPricing.LongPromptMultiplier("claude-sonnet-5-5", longPrompt.PromptTokens));

        // 90 000 × $0.10 + 20 000 × $0.01 + 1 000 × $0.50 = $0.0097 → ×5 = $0.0485.
        Assert.Equal(0.0485m, AiPricing.Cost(longPrompt, haiku, "claude-haiku-5-5"));
        Assert.Equal(0.0077m, AiPricing.Cost(shortPrompt, haiku, "claude-haiku-5-5"));
    }

    [Fact]
    public void Unknown_price_gives_no_cost_instead_of_zero()
    {
        Assert.Null(AiPricing.Cost(new AiTokenUsage(1000, 100), new AiModelPrices(null, 10m, null, null), "claude-opus-5-5"));
        Assert.Null(AiPricing.Cost(new AiTokenUsage(1000, 100), null, "claude-opus-5-5"));
    }

    [Fact]
    public void Failed_call_without_tokens_costs_nothing()
    {
        Assert.Equal(0m, AiPricing.Cost(AiTokenUsage.None, null, null));
    }

    [Fact]
    public void Usage_sums_all_four_counters()
    {
        var sum = new AiTokenUsage(10, 5, 100, 20).Add(new AiTokenUsage(1, 2, 3, 4));
        Assert.Equal(new AiTokenUsage(11, 7, 103, 24), sum);
        Assert.Equal(138, sum.PromptTokens);
    }
}
