using OneBase.AI.Monitoring;

namespace OneBase.AI.Tests;

public class UsageCostTests
{
    [Fact]
    public void Cost_uses_prices_per_million_tokens()
    {
        // 12 000 входных по $2.50 за 1М и 800 выходных по $10 за 1М.
        Assert.Equal(0.038m, DbUsageRecorder.Cost(12_000, 800, 2.5m, 10m));
    }

    [Fact]
    public void Unknown_price_gives_no_cost_instead_of_zero()
    {
        Assert.Null(DbUsageRecorder.Cost(1000, 100, null, 10m));
    }

    [Fact]
    public void Failed_call_without_tokens_costs_nothing()
    {
        Assert.Equal(0m, DbUsageRecorder.Cost(0, 0, null, null));
    }
}
