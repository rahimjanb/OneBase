using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;

namespace OneBase.Sales.Tests;

public class AgentFlagsTests
{
    private static readonly FlagThresholds T = new();

    private static AgentStats Agent(
        int visits = 100,
        int withOrder = 80,
        int orders = 80,
        decimal revenue = 80_000_000,
        int categories = 5,
        decimal? tempo = 1,
        bool vacancy = false,
        long id = 1) =>
        new(id, 1000, revenue, 50, categories, new VisitSummary(visits, withOrder, orders), tempo, vacancy);

    private static readonly RegionMedians NoMedians = new(null, null, null);

    private static IReadOnlyList<AgentFlag> Flags(AgentStats a, RegionMedians? m = null) =>
        AgentFlags.Evaluate(a, m ?? NoMedians, T, categoryTarget: 4, workedDays: 27, daysInMonth: 30);

    private static FlagSeverity? SeverityOf(IReadOnlyList<AgentFlag> flags, FlagKind kind) =>
        flags.FirstOrDefault(f => f.Kind == kind)?.Severity;

    [Fact]
    public void Healthy_agent_has_no_flags() => Assert.Empty(Flags(Agent()));

    [Fact]
    public void Vacancy_is_not_evaluated() => Assert.Empty(Flags(Agent(visits: 5, revenue: 0, vacancy: true)));

    [Fact]
    public void Fewer_than_min_visits_gives_only_low_data()
    {
        var flags = Flags(Agent(visits: 19, withOrder: 1, orders: 1, revenue: 0));
        Assert.Single(flags);
        Assert.Equal(FlagKind.LowData, flags[0].Kind);
        Assert.Null(AgentFlags.Worst(flags));
    }

    [Theory]
    [InlineData(14, FlagSeverity.Critical)] // < 15%
    [InlineData(20, FlagSeverity.Risk)] // < 25%
    [InlineData(30, null)]
    public void Low_conversion_absolute_thresholds(int withOrderOf100, FlagSeverity? expected) =>
        Assert.Equal(expected, SeverityOf(Flags(Agent(withOrder: withOrderOf100, orders: withOrderOf100)), FlagKind.LowConversion));

    [Theory]
    [InlineData(39, FlagSeverity.Critical)] // < 50% медианы 80%
    [InlineData(55, FlagSeverity.Risk)] // < 70% медианы
    [InlineData(60, null)]
    public void Low_conversion_relative_to_region_median(int withOrderOf100, FlagSeverity? expected)
    {
        var medians = new RegionMedians(0.80m, null, null);
        Assert.Equal(expected, SeverityOf(Flags(Agent(withOrder: withOrderOf100, orders: withOrderOf100), medians), FlagKind.LowConversion));
    }

    [Fact]
    public void Visits_without_revenue_is_critical() =>
        Assert.Equal(FlagSeverity.Critical, SeverityOf(Flags(Agent(revenue: 0)), FlagKind.VisitsNoSales));

    [Fact]
    public void Low_sum_per_visit_versus_median_is_risk()
    {
        // 80 млн / 100 визитов = 800 тыс; медиана 2,5 млн → 32% < 40%.
        var medians = new RegionMedians(null, 2_500_000m, null);
        Assert.Equal(FlagSeverity.Risk, SeverityOf(Flags(Agent(), medians), FlagKind.VisitsNoSales));
    }

    [Fact]
    public void Small_check_requires_normal_conversion_and_enough_orders()
    {
        // Чек 1 млн против медианы 2,5 млн (40% < 50%).
        var medians = new RegionMedians(null, null, 2_500_000m);
        Assert.Equal(FlagSeverity.Risk, SeverityOf(Flags(Agent(), medians), FlagKind.SmallCheck));

        // Ровно половина медианы — ещё не мелкий чек (порог — ½ медианы, как в «Полевом контроле»).
        Assert.Null(SeverityOf(Flags(Agent(), new RegionMedians(null, null, 2_000_000m)), FlagKind.SmallCheck));

        // Мало заказов — не оцениваем.
        Assert.Null(SeverityOf(Flags(Agent(withOrder: 19, orders: 19, revenue: 19_000_000), medians), FlagKind.SmallCheck));
    }

    [Theory]
    [InlineData(2, FlagSeverity.Critical)]
    [InlineData(3, FlagSeverity.Risk)] // ровно 3 при цели 4
    [InlineData(4, null)]
    public void Narrow_assortment(int categories, FlagSeverity? expected) =>
        Assert.Equal(expected, SeverityOf(Flags(Agent(categories: categories)), FlagKind.NarrowAssortment));

    [Fact]
    public void Narrow_assortment_only_for_agents_with_sales() =>
        Assert.Null(SeverityOf(Flags(Agent(categories: 0, revenue: 0)), FlagKind.NarrowAssortment));

    [Theory]
    [InlineData(0.74, FlagSeverity.Critical)]
    [InlineData(0.85, FlagSeverity.Risk)]
    [InlineData(0.95, null)]
    public void Tempo_drop(double tempo, FlagSeverity? expected) =>
        Assert.Equal(expected, SeverityOf(Flags(Agent(tempo: (decimal)tempo)), FlagKind.TempoDrop));

    [Fact]
    public void More_orders_than_visits_is_a_data_quality_risk_and_conversion_is_not_judged()
    {
        // 60 заказов на 50 визитов — конверсия 120%: это вопрос к данным, а не к агенту; низкую конверсию не оцениваем,
        // даже если медиана региона выше.
        var flags = Flags(Agent(visits: 50, withOrder: 50, orders: 60), new RegionMedians(3m, null, null));

        Assert.Equal(FlagSeverity.Risk, SeverityOf(flags, FlagKind.DataMismatch));
        Assert.Null(SeverityOf(flags, FlagKind.LowConversion));
        Assert.Equal(FlagSeverity.Risk, AgentFlags.Worst(flags));
    }

    [Fact]
    public void Conversion_is_orders_over_done_visits_and_visits_without_order_may_be_negative()
    {
        var summary = new VisitSummary(Done: 100, WithOrder: 30, Orders: 45);
        Assert.Equal(0.45m, summary.Conversion); // заказы ÷ визиты, а не визиты с заказом
        Assert.Equal(55, summary.WithoutOrder);

        var busy = new VisitSummary(Done: 10, WithOrder: 9, Orders: 12);
        Assert.Equal(-2, busy.WithoutOrder); // заказов больше, чем визитов — не обрезаем
        Assert.True(busy.DataMismatch);
    }

    [Fact]
    public void Region_median_peers_are_evaluated_agents_without_vacancies_and_mismatches()
    {
        var agents = new[]
        {
            Agent(withOrder: 20, orders: 20, id: 1), // 20%
            Agent(withOrder: 50, orders: 50, id: 2), // 50%
            Agent(withOrder: 90, orders: 90, vacancy: true, id: 3), // вакансия
            Agent(visits: 50, withOrder: 50, orders: 70, id: 4), // данные не сходятся
            Agent(visits: 19, withOrder: 19, orders: 19, id: 5), // меньше 20 визитов — не оценивается
        };

        Assert.Equal(0.35m, RegionMedians.Of(agents, T.MinVisits).Conversion);
    }
}
