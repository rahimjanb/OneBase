using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;

namespace OneBase.Sales.Tests;

public class SalesAnalyticsTests
{
    private static readonly Guid North = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid South = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Rm1 = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

    private static SaleLine Line(int day, long? agent, long market, long branch, decimal kg, decimal revenue, long? order, int month = 9) =>
        new(new DateOnly(2026, month, day), agent, market, branch, 1, 100, kg, revenue, order);

    private static MonthData Data(IReadOnlyList<SaleLine>? previous = null) => new()
    {
        Year = 2026,
        Month = 9,
        DataThrough = new DateOnly(2026, 9, 10),
        Current =
        [
            Line(1, 1, 10, branch: 101, kg: 100, revenue: 1000, order: 1),
            Line(2, 2, 11, branch: 101, kg: 50, revenue: 500, order: 2),
            Line(3, null, 12, branch: 101, kg: 25, revenue: 250, order: 3), // факт без агента
            Line(4, 3, 13, branch: 102, kg: 40, revenue: 400, order: 4),
            Line(5, 3, 13, branch: 102, kg: -10, revenue: -100, order: null), // возврат
            Line(6, 4, 14, branch: 999, kg: 5, revenue: 50, order: 5), // филиал без региона
        ],
        Previous = previous ?? [],
        Visits = [],
        History = [],
        Plans = [new PlanRow(North, null, 9, null, 300), new PlanRow(South, null, 9, null, 60)],
        YearRegionPlans = [],
        Agents = new Dictionary<long, AgentInfo>
        {
            [1] = new(1, "Агент 1", true, North, false, true),
            [2] = new(2, "Агент 2", true, North, false, true),
            [3] = new(3, "Агент 3", true, South, false, true),
        },
        Regions = [new RegionInfo(North, 101, "Север", Rm1, null, null), new RegionInfo(South, 102, "Юг", Rm1, null, null)],
        Directions = [new DirectionInfo(Rm1, "РМ 1", false, null, null, 1)],
        Markets = new Dictionary<long, MarketInfo>(),
        Categories = new Dictionary<long, string> { [1] = "Печенье" },
        MarketAssignments = [],
        Targets = new SalesTargets(0.7m, 1_439_000m, 125m, 4m),
        Thresholds = new FlagThresholds(),
    };

    [Fact]
    public void Sum_of_regions_equals_republic_total()
    {
        var republic = new SalesAnalytics(Data()).Republic(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10));

        Assert.Equal(210m, republic.Kpi.FactKg); // 100 + 50 + 25 + 40 − 10 + 5
        Assert.Equal(republic.Kpi.FactKg, republic.Regions.Sum(r => r.FactKg));
        Assert.Equal(republic.Kpi.Revenue, republic.Regions.Sum(r => r.Revenue));
        Assert.Equal(360m, republic.Kpi.PlanKg);
    }

    [Fact]
    public void Sum_of_agents_plus_unassigned_equals_region_total()
    {
        var region = new SalesAnalytics(Data()).Region(North, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10), "kg", null);

        Assert.Equal(175m, region.Kpi.FactKg);
        Assert.Equal(25m, region.Unassigned.Kg);
        Assert.Equal(region.Kpi.FactKg, region.Team.Sum(t => t.FactKg) + region.Unassigned.Kg);
        Assert.Equal(175m / 300m, region.Kpi.Execution);
    }

    [Fact]
    public void Returns_are_subtracted_in_the_region_of_the_return()
    {
        var region = new SalesAnalytics(Data()).Region(South, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10), "kg", null);

        Assert.Equal(30m, region.Kpi.FactKg);
        Assert.Equal(300m, region.Kpi.Revenue);
    }

    [Fact]
    public void Same_days_comparison_cuts_previous_month_to_the_same_day_numbers()
    {
        // Отработано 10 дней: из августа берём 1–10, а продажа 20 августа не участвует.
        var previous = new[]
        {
            Line(5, 1, 10, branch: 101, kg: 80, revenue: 800, order: 90, month: 8),
            Line(20, 1, 10, branch: 101, kg: 500, revenue: 5000, order: 91, month: 8),
        };

        var republic = new SalesAnalytics(Data(previous)).Republic(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10));
        var north = republic.SameDays.Single(r => r.Id == North.ToString());

        Assert.Equal(80m, north.KgBefore);
        Assert.Equal(175m, north.KgNow);
    }

    [Fact]
    public void Not_bought_yet_is_previous_month_outlets_minus_current_ones()
    {
        var previous = new[]
        {
            Line(5, 1, 10, branch: 101, kg: 1, revenue: 100, order: 90, month: 8), // купила и в сентябре
            Line(6, 1, 20, branch: 101, kg: 1, revenue: 300, order: 91, month: 8), // молчит
        };

        var republic = new SalesAnalytics(Data(previous)).Republic(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10));
        var north = republic.NotBought.Single(r => r.Id == North.ToString());

        Assert.Equal(2, north.Base);
        Assert.Equal(1, north.Silent);
        Assert.Equal(300m, north.SilentPrevRevenue);
        Assert.Equal(2, north.New); // ТТ 11 и 12 — новые
    }
}
