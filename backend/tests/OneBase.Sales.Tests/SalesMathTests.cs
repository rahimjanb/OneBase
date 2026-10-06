using OneBase.Application.Sales.Metrics;

namespace OneBase.Sales.Tests;

public class SalesMathTests
{
    private static readonly DateOnly Sep1 = new(2026, 9, 1);

    private static SaleLine Sale(long? agent, long market, decimal revenue, decimal kg = 1, int day = 1, long? order = 1, long? branch = 1, long? category = 1) =>
        new(new DateOnly(2026, 9, day), agent, market, branch, category, 100, kg, revenue, order);

    [Fact]
    public void Forecast_scales_fact_by_worked_days()
    {
        Assert.Equal(1000m, SalesMath.Forecast(900, 27, 30));
        Assert.Null(SalesMath.Forecast(900, 0, 30));
    }

    [Theory]
    [InlineData(2026, 9, 27, 27)] // 1–27 сентября
    [InlineData(2026, 10, 5, 30)] // данные уже за октябрь — сентябрь отработан целиком
    [InlineData(2026, 8, 31, 0)] // данных за сентябрь ещё нет
    public void WorkedDays_counts_from_first_day_to_last_data(int year, int month, int day, int expected) =>
        Assert.Equal(expected, SalesMath.WorkedDays(Sep1, new DateOnly(year, month, day)));

    [Fact]
    public void Akb_counts_each_outlet_once_across_agents()
    {
        var lines = new[]
        {
            Sale(agent: 1, market: 10, revenue: 100),
            Sale(agent: 2, market: 10, revenue: 50, order: 2),
            Sale(agent: 2, market: 11, revenue: 70, order: 3),
        };

        Assert.Equal(2, SalesMath.Akb(lines));
    }

    [Fact]
    public void Akb_counts_outlets_with_positive_net_weight()
    {
        var lines = new[]
        {
            Sale(agent: 1, market: 10, revenue: 100, kg: 5),
            Sale(agent: 1, market: 10, revenue: -100, kg: -5, order: null), // вернули всю покупку
            Sale(agent: 1, market: 11, revenue: 30, kg: 3, order: 2),
            Sale(agent: 1, market: 11, revenue: -10, kg: -1, order: null), // частичный возврат
            Sale(agent: 1, market: 12, revenue: -50, kg: -2, order: null), // возврат без заказа в месяце
        };

        Assert.Equal(1, SalesMath.Akb(lines)); // только ТТ 11: у ТТ 10 чистый вес 0, у ТТ 12 — минус (как в «Полевом контроле»)
        Assert.Equal(2, SalesMath.OrderCount(lines)); // возврат не заказ
    }

    [Fact]
    public void Conversion_is_orders_accepted_in_the_month_over_done_visits()
    {
        var visits = new[]
        {
            new VisitRecord(new DateOnly(2026, 9, 1), 1, 10, VisitStatus.Done, true),
            new VisitRecord(new DateOnly(2026, 9, 1), 1, 11, VisitStatus.Done, true),
            new VisitRecord(new DateOnly(2026, 9, 2), 1, 12, VisitStatus.Done, false),
            new VisitRecord(new DateOnly(2026, 9, 3), 1, 13, VisitStatus.Pending, true), // не выполнен — не считается
        };
        // Заказы по дню ввода — только для справки «визитов с заказом».
        var created = new[]
        {
            Sale(agent: 1, market: 10, revenue: 100, day: 1, order: 1),
            Sale(agent: 1, market: 12, revenue: 100, day: 2, order: 2),
            Sale(agent: 1, market: 11, revenue: 100, day: 5, order: 3), // заказ в другой день — визит 1.09 без заказа
        };
        // Заказы месяца по дате приёмки (факт): два заказа и возврат — возврат заказом не считается.
        var accepted = new[]
        {
            Sale(agent: 1, market: 10, revenue: 100, day: 2, order: 1),
            Sale(agent: 1, market: 10, revenue: 50, day: 2, order: 1),
            Sale(agent: 1, market: 12, revenue: 100, day: 3, order: 2),
            Sale(agent: 1, market: 12, revenue: -20, kg: -1, day: 4, order: null),
        };

        var summary = VisitSummary.Of(visits, created, accepted);

        Assert.Equal(3, summary.Done);
        Assert.Equal(2, summary.WithOrder);
        Assert.Equal(2, summary.Orders);
        Assert.Equal(1, summary.WithoutOrder); // визиты − заказы
        Assert.Equal(2m / 3m, summary.Conversion);
        Assert.False(summary.DataMismatch);
    }

    [Fact]
    public void More_orders_than_visits_is_data_mismatch()
    {
        var visits = new[] { new VisitRecord(new DateOnly(2026, 9, 1), 1, 10, VisitStatus.Done, true) };
        var lines = new[] { Sale(1, 10, 100, order: 1), Sale(1, 11, 100, order: 2) };

        var summary = VisitSummary.Of(visits, lines, lines);
        Assert.True(summary.DataMismatch);
        Assert.Equal(2m, summary.Conversion); // 2 заказа на 1 визит — 200%, не обрезается
    }

    [Fact]
    public void Same_days_cutoff_matches_day_number_and_clamps_to_short_month()
    {
        Assert.Equal(new DateOnly(2026, 8, 25), SalesMath.SameDaysCutoff(new DateOnly(2026, 8, 1), 25));
        Assert.Equal(new DateOnly(2026, 2, 28), SalesMath.SameDaysCutoff(new DateOnly(2026, 2, 1), 30));
    }

    [Fact]
    public void Tempo_compares_forecast_with_average_of_months_with_sales()
    {
        // Прогноз 700 / 15 × 30 = 1400; среднее по месяцам с продажами (1000, 2000) = 1500.
        var tempo = SalesMath.TempoToOwnAverage(700, 15, 30, [1000, 0, 2000]);
        Assert.Equal(1400m / 1500m, tempo);
        Assert.Null(SalesMath.TempoToOwnAverage(700, 15, 30, [0, 0]));
    }

    [Fact]
    public void Plan_total_prefers_row_without_category_otherwise_sums_categories()
    {
        var withTotal = new[] { new PlanRow(null, 1, 9, null, 1000), new PlanRow(null, 1, 9, 5, 300) };
        var byCategory = new[] { new PlanRow(null, 1, 9, 5, 300), new PlanRow(null, 1, 9, 6, 200) };

        Assert.Equal(1000m, SalesMath.PlanTotal(withTotal));
        Assert.Equal(500m, SalesMath.PlanTotal(byCategory));
        Assert.Null(SalesMath.PlanTotal([]));
    }

    [Theory]
    [InlineData(1.0, TargetLevel.Good)]
    [InlineData(0.95, TargetLevel.Good)] // как в «Полевом контроле»: от 95% цели — зелёный
    [InlineData(0.94, TargetLevel.Warning)]
    [InlineData(0.75, TargetLevel.Warning)]
    [InlineData(0.74, TargetLevel.Bad)]
    public void Target_level_thresholds(double share, TargetLevel expected) =>
        Assert.Equal(expected, SalesMath.TargetLevelOf((decimal)share * 100, 100));

    [Theory]
    [InlineData(1.0, TargetLevel.Good)]
    [InlineData(0.9, TargetLevel.Good)] // execLevel «Полевого контроля»: от 90% — зелёный
    [InlineData(0.89, TargetLevel.Warning)]
    [InlineData(0.6, TargetLevel.Warning)]
    [InlineData(0.59, TargetLevel.Bad)]
    public void Execution_level_thresholds(double execution, TargetLevel expected) =>
        Assert.Equal(expected, SalesMath.ExecutionLevelOf((decimal)execution));

    [Fact]
    public void Execution_level_is_absent_without_a_value() => Assert.Null(SalesMath.ExecutionLevelOf(null));

    [Fact]
    public void Problem_score_weighs_critical_risk_and_strike_of_evaluated_agents()
    {
        Assert.Equal(10.4m, SalesMath.ProblemScore(critical: 0, risk: 2, conversion: 0.2m, visits: 20, minVisits: 20)); // 8 + 0,8 × 3
        Assert.Equal(20m, SalesMath.ProblemScore(2, 0, 1.5m, 20, 20)); // страйк больше 100% — слагаемое 0
        Assert.Equal(14m, SalesMath.ProblemScore(1, 1, 0m, 19, 20)); // меньше 20 визитов — без слагаемого страйка
        Assert.Equal(4m, SalesMath.ProblemScore(0, 1, null, 20, 20));
    }

    [Fact]
    public void Average_takes_months_with_data()
    {
        Assert.Equal(20m, SalesMath.Average([10m, null, 30m]));
        Assert.Null(SalesMath.Average([null, null]));
    }

    [Fact]
    public void Median_handles_even_and_empty_sets()
    {
        Assert.Equal(2.5m, SalesMath.Median([4, 1, 2, 3]));
        Assert.Equal(2m, SalesMath.Median([3, 1, 2]));
        Assert.Null(SalesMath.Median([]));
    }

    [Fact]
    public void Report_categories_follow_configured_type_mapping_and_keep_unknown_types_separate()
    {
        var linkoTypes = new Dictionary<long, string>
        {
            [8] = "Помадка", [20] = "Помадка 0,420 кг", [21] = "Помадка 0,5 кг", [22] = "Снек", [15] = "Импорт Шоколад", [16] = "бонус",
        };
        var cats = OneBase.Application.Sales.SalesCategories.Build(
            new Dictionary<string, string> { ["Придзел"] = "22", ["Помадка"] = "8,20,21" }, linkoTypes);

        Assert.Equal(cats.GroupOf(8), cats.GroupOf(20)); // пять фасовок — одна категория
        Assert.Equal(cats.GroupOf(8), cats.GroupOf(21));
        Assert.Equal("Помадка", cats.NameOf(cats.GroupOf(21)));
        Assert.Equal("Придзел", cats.NameOf(cats.GroupOf(22))); // «Снек» в отчёте — «Придзел»
        Assert.True(OneBase.Application.Sales.SalesCategories.IsConfigured(cats.GroupOf(22)));

        // Импорт и бонус не входят в категории отчёта, но и не пропадают: у них своя строка с именем из Linko.
        Assert.False(OneBase.Application.Sales.SalesCategories.IsConfigured(cats.GroupOf(15)));
        Assert.Equal("Импорт Шоколад", cats.NameOf(cats.GroupOf(15)));
        Assert.Equal("Тип 99", cats.NameOf(cats.GroupOf(99))); // тип, которого нет даже в справочнике
        Assert.Null(cats.GroupOf(null));
    }
}
