using OneBase.Application.Production;
using OneBase.Domain.Production;

namespace OneBase.AI.Tests;

/// <summary>Правила производства: потребность по норме, отклонение и перерасход, переходы заданий, код рецептуры.</summary>
public class ProductionRulesTests
{
    [Fact]
    public void Requirement_scales_the_norm_to_the_plan_with_losses_and_rounds_to_a_gram()
    {
        Assert.Equal(140.25m, ProductionRules.Requirement(55, 250, 100, 2)); // 55 × 2,5 × 1,02
        Assert.Equal(31.25m, ProductionRules.Requirement(12.5m, 250, 100, 0));
        Assert.Equal(0.334m, ProductionRules.Requirement(1, 1, 3, 0.1m)); // 0,33366… → 0,334
        Assert.Equal(0m, ProductionRules.Requirement(10, 5, 0, 0)); // выход 0 — потребности нет
        Assert.Equal(2m, ProductionRules.Requirement(1, 100, 100, 100)); // потери 100 %
    }

    [Fact]
    public void Deviation_and_overconsumption_compare_the_fact_with_the_norm_on_actual_output()
    {
        Assert.Equal((7m, 15.6m), ProductionRules.Deviation(52, 45));
        Assert.Equal((-5m, (decimal?)-10m), ProductionRules.Deviation(45, 50));
        Assert.Equal((3m, (decimal?)null), ProductionRules.Deviation(3, 0));
        Assert.True(ProductionRules.Overconsumed(52, 45, 5));
        Assert.False(ProductionRules.Overconsumed(47.25m, 45, 5)); // ровно порог — не перерасход
        Assert.True(ProductionRules.Overconsumed(0.1m, 0, 5));
        Assert.False(ProductionRules.Overconsumed(0, 0, 5));
        Assert.Equal(0m, ProductionRules.LeftToIssue(50, 55, 3)); // выдали больше потребности
        Assert.Equal(8m, ProductionRules.LeftToIssue(50, 45, 3));
    }

    [Fact]
    public void Orders_move_draft_to_released_to_in_progress_and_cancel_only_with_nothing_on_hand()
    {
        Assert.True(ProductionRules.Editable(ProductionOrderStatus.Draft));
        Assert.False(ProductionRules.Editable(ProductionOrderStatus.Released));
        Assert.True(ProductionRules.Issuable(ProductionOrderStatus.Released));
        Assert.True(ProductionRules.Issuable(ProductionOrderStatus.InProgress));
        Assert.False(ProductionRules.Issuable(ProductionOrderStatus.Completed));
        Assert.True(ProductionRules.Cancellable(ProductionOrderStatus.InProgress, 0));
        Assert.False(ProductionRules.Cancellable(ProductionOrderStatus.InProgress, 2.5m));
        Assert.False(ProductionRules.Cancellable(ProductionOrderStatus.Completed, 0));
        Assert.True(ProductionRules.ValidCode("CK-01.v"));
        Assert.True(ProductionRules.ValidCode("Печенье_1"));
        Assert.False(ProductionRules.ValidCode("-CK"));
        Assert.False(ProductionRules.ValidCode("CK 01"));
        Assert.False(ProductionRules.ValidCode(new string('A', 33)));
        Assert.True(ProductionRules.ValidQuantity(0.001m, 3));
        Assert.False(ProductionRules.ValidQuantity(0.0001m, 3));
    }
}
