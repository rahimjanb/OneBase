using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;
using OneBase.Domain.Sales;

namespace OneBase.Sales.Tests;

public class SalesPlansTests
{
    private static readonly Guid North = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid South = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid OldSouth = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly Guid Factory = Guid.Parse("00000000-0000-0000-0000-000000000004");

    /// <summary>Север и Юг — регионы вторички, «Юг (эски)» считается в Юге, «Завод» — вне вторички.</summary>
    private static Guid? RegionOf(Guid id) => id == OldSouth ? South : id == North || id == South ? id : null;

    /// <summary>План ТП 1 из Linko на сентябрь.</summary>
    private static readonly PlanRow[] AgentPlans = [new(null, 1, 9, null, 100)];

    private static readonly RegionPlanRow[] Rows =
    [
        new(North, PlanKind.Rop, 2026, 9, 4, 300),
        new(North, PlanKind.Factory, 2026, 9, 4, 280),
        new(OldSouth, PlanKind.Rop, 2026, 9, 4, 50),
        new(Factory, PlanKind.Rop, 2026, 9, 4, 999),
        new(North, PlanKind.Rop, 2026, 8, 4, 200),
        new(North, PlanKind.Rop, 2026, 10, 4, 310),
        new(North, PlanKind.Primary, 2026, 9, 4, 777),
    ];

    [Fact]
    public void Selected_plan_kind_gives_region_plans_of_the_month_together_with_linko_agent_plans()
    {
        var rop = SalesPlans.Select(2026, 9, PlanKind.Rop, Rows, RegionOf, AgentPlans);

        Assert.Equal(PlanSources.Rop, rop.Source);
        Assert.Equal([300m, 50m, 100m], rop.Plans.Select(p => p.PlanKg)); // планы регионов и план ТП 1 из Linko (карточка ТП)
        Assert.Equal(South, rop.Plans[1].RegionId); // старый филиал — в текущем регионе; «Завод» отброшен
        Assert.Equal([9, 9, 8, 10], rop.YearRegionPlans.Select(p => p.Month)); // год месяца — для «План и факт по месяцам»
        Assert.Equal(310m, Assert.Single(rop.NextRegionPlans).PlanKg);

        var factory = SalesPlans.Select(2026, 9, PlanKind.Factory, Rows, RegionOf, AgentPlans);
        Assert.Equal(PlanSources.Factory, factory.Source);
        Assert.Equal([280m, 100m], factory.Plans.Select(p => p.PlanKg));

        // Плана первички во вторичке нет — вместо него РОП.
        Assert.Equal(rop.Plans, SalesPlans.Select(2026, 9, PlanKind.Primary, Rows, RegionOf, AgentPlans).Plans);
    }

    [Fact]
    public void Month_without_region_plans_of_the_selected_kind_falls_back_to_linko_agent_plans()
    {
        // «Завод» заведён только на сентябрь: в октябре — планы ТП из Linko, и ответ это показывает.
        var october = SalesPlans.Select(2026, 10, PlanKind.Factory, Rows, RegionOf, AgentPlans);
        Assert.Equal(PlanSources.Linko, october.Source);
        Assert.Equal(AgentPlans, october.Plans);
        Assert.Equal(280m, Assert.Single(october.YearRegionPlans).PlanKg); // в графике по месяцам сентябрь остаётся с планом «Завод»

        Assert.Equal(PlanSources.Rop, SalesPlans.Select(2026, 10, PlanKind.Rop, Rows, RegionOf, AgentPlans).Source);
        Assert.Equal(PlanSources.Linko, SalesPlans.Select(2026, 9, PlanKind.Rop, [], RegionOf, AgentPlans).Source);
    }

    [Fact]
    public void Next_month_of_december_is_january_of_the_next_year()
    {
        RegionPlanRow[] rows = [new(North, PlanKind.Rop, 2026, 12, 4, 400), new(North, PlanKind.Rop, 2027, 1, 4, 410)];

        var plans = SalesPlans.Select(2026, 12, PlanKind.Rop, rows, RegionOf, []);

        Assert.Equal(410m, Assert.Single(plans.NextRegionPlans).PlanKg);
        Assert.Equal(400m, Assert.Single(plans.YearRegionPlans).PlanKg); // январь следующего года — не в графике этого года
    }

    [Fact]
    public void Available_plans_of_a_month_are_the_kinds_with_rows_of_secondary_regions()
    {
        Assert.Equal([PlanSources.Rop, PlanSources.Factory], SalesPlans.Available(2026, 9, Rows, RegionOf));
        Assert.Equal([PlanSources.Rop], SalesPlans.Available(2026, 8, Rows, RegionOf)); // «Завод» заведён только на сентябрь
        Assert.Equal([PlanSources.Rop], SalesPlans.Available(2026, 10, Rows, RegionOf));
        Assert.Empty(SalesPlans.Available(2026, 11, Rows, RegionOf)); // планов регионов нет — переключателю предлагать нечего
        Assert.Empty(SalesPlans.Available(2025, 9, Rows, RegionOf));

        // Строки «Завода» (регион вне вторички) и план первички плана вторички не дают — как и в Select.
        RegionPlanRow[] outside = [new(Factory, PlanKind.Factory, 2026, 9, 4, 100), new(North, PlanKind.Primary, 2026, 9, 4, 100)];
        Assert.Empty(SalesPlans.Available(2026, 9, outside, RegionOf));
        Assert.Equal(PlanSources.Linko, SalesPlans.Select(2026, 9, PlanKind.Factory, outside, RegionOf, AgentPlans).Source);
    }

    [Theory]
    [InlineData("factory", PlanKind.Factory)]
    [InlineData("FACTORY", PlanKind.Factory)]
    [InlineData(" Factory ", PlanKind.Factory)]
    [InlineData("rop", PlanKind.Rop)]
    [InlineData("ROP", PlanKind.Rop)]
    [InlineData(null, PlanKind.Rop)]
    [InlineData("", PlanKind.Rop)]
    [InlineData("primary", PlanKind.Rop)]
    [InlineData("1", PlanKind.Rop)]
    [InlineData("завод", PlanKind.Rop)]
    public void Plan_parameter_is_factory_only_for_factory_and_rop_for_anything_else(string? value, PlanKind expected) =>
        Assert.Equal(expected, SalesPlans.Parse(value));

    [Fact]
    public void Plan_kind_is_part_of_the_cache_key()
    {
        Assert.NotEqual(SalesPlans.CacheKey(2026, 9, PlanKind.Rop), SalesPlans.CacheKey(2026, 9, PlanKind.Factory));
        Assert.NotEqual(SalesPlans.CacheKey(2026, 9, PlanKind.Rop), SalesPlans.CacheKey(2026, 10, PlanKind.Rop));
        Assert.Equal(SalesPlans.CacheKey(2026, 9, PlanKind.Rop), SalesPlans.CacheKey(2026, 9, PlanKind.Primary)); // первичка — тот же РОП
        Assert.Equal((PlanSources.Rop, PlanSources.Factory), (SalesPlans.SourceOf(PlanKind.Rop), SalesPlans.SourceOf(PlanKind.Factory)));
    }
}
