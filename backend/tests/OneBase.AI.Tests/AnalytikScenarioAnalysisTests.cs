using OneBase.Application.Analytik.Compute;
using OneBase.Application.Analytik.Simulation;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>
/// What-If Decision Intelligence без базы — на снимке тестов симулятора (AnalytikSimulatorEngineTests.Baseline): торнадо, матрица, подбор под
/// цель, безубыточность, Монте-Карло, случаи и оптимальный выпуск. Эталон — тот же движок при найденных значениях и ручной расчёт симплекса.
/// </summary>
public sealed class AnalytikScenarioAnalysisTests
{
    private static ScenarioDefinition Def(params ScenarioParameter[] p) => new(ScenarioKinds.Combined, 30, p);

    private static decimal Revenue(ScenarioBaselineData b, ScenarioDefinition d) => ScenarioEngine.Run(b, d).Metric("sales.revenue")!.Scenario!.Value;

    [Fact]
    public void Tornado_ranks_parameters_by_their_effect_and_leaves_the_scenario_unchanged()
    {
        var b = AnalytikSimulatorEngineTests.Baseline(sugarAvailable: 5000);
        var def = Def(new ScenarioParameter("sales.demand", 5));
        var r = ScenarioAnalysis.Sensitivity(b, def, "sales.revenue", ["materials.price", "sales.demand", "sales.price"]);
        Assert.Equal(Revenue(b, def), r.Base);
        Assert.Equal("materials.price", r.Rows[^1].Code); // цена сырья выручку не меняет
        Assert.Equal(0, r.Rows[^1].Swing);
        var demand = r.Rows.Single(x => x.Code == "sales.demand");
        Assert.Equal(-5, demand.Low);
        Assert.Equal(15, demand.High);
        Assert.Equal(Revenue(b, ScenarioAnalysis.With(def, "sales.demand", 15)), demand.MetricHigh);
        Assert.Single(def.Parameters); // исходный сценарий не изменился
    }

    [Fact]
    public void Matrix_values_equal_engine_runs_at_each_grid_point()
    {
        var b = AnalytikSimulatorEngineTests.Baseline(sugarAvailable: 5000);
        var def = Def();
        var r = ScenarioAnalysis.Matrix(b, def, "sales.revenue", "sales.demand", [0, 10], "sales.price", [-5, 0, 5]);
        Assert.Equal(3, r.Values.Count);
        Assert.Equal(Revenue(b, ScenarioAnalysis.With(ScenarioAnalysis.With(def, "sales.demand", 10), "sales.price", 5)), r.Values[2][1]);
    }

    [Fact]
    public void Goal_seek_finds_the_parameter_value_and_reports_unreachable_and_no_effect()
    {
        var b = AnalytikSimulatorEngineTests.Baseline(sugarAvailable: 5000);
        var def = Def();
        var target = Revenue(b, Def(new ScenarioParameter("sales.demand", 12)));
        var found = ScenarioAnalysis.GoalSeek(b, def, "sales.revenue", target, "sales.demand");
        Assert.Equal("found", found.Status);
        Assert.Equal(12, found.Value);
        Assert.Equal(target, found.Achieved);

        var far = ScenarioAnalysis.GoalSeek(b, def, "sales.revenue", target * 100, "sales.demand");
        Assert.Equal("unreachable", far.Status);
        Assert.Contains("недостижима", far.Text);

        Assert.Equal("no_effect", ScenarioAnalysis.GoalSeek(b, def, "sales.revenue", target, "materials.price").Status);
    }

    [Fact]
    public void Break_even_of_gross_profit_is_where_the_engine_gives_zero()
    {
        var b = AnalytikSimulatorEngineTests.Baseline(sugarAvailable: 5000);
        var def = Def(new ScenarioParameter("input.cost_per_kg", 10_000));
        var be = ScenarioAnalysis.BreakEven(b, def, "materials.price", "finance.gross_profit");
        Assert.Equal("found", be.Status);
        var at = ScenarioEngine.Run(b, ScenarioAnalysis.With(def, "materials.price", be.Value!.Value)).Metric("finance.gross_profit")!.Scenario!.Value;
        var step = ScenarioEngine.Run(b, ScenarioAnalysis.With(def, "materials.price", be.Value!.Value + 1)).Metric("finance.gross_profit")!.Scenario!.Value;
        Assert.True(Math.Abs(at) <= Math.Abs(at - step) * 0.6m, $"прибыль при найденном значении {at}, шаг даёт {at - step}");
    }

    [Fact]
    public void Monte_carlo_is_reproducible_by_seed_and_ordered()
    {
        var b = AnalytikSimulatorEngineTests.Baseline(sugarAvailable: 5000);
        var inputs = new[] { new UncertainParameter("sales.demand", -20, 0, 15), new UncertainParameter("sales.price", -5, 0, 5) };
        var a = ScenarioAnalysis.MonteCarlo(b, Def(), "sales.revenue", inputs, 200, 7);
        var again = ScenarioAnalysis.MonteCarlo(b, Def(), "sales.revenue", inputs, 200, 7);
        Assert.Equal(a.P50, again.P50);
        Assert.Equal(a.Mean, again.Mean);
        Assert.True(a.P5 <= a.P10 && a.P10 <= a.P50 && a.P50 <= a.P90 && a.P90 <= a.P95);
        Assert.Equal(200, a.Histogram.Sum(h => h.Count));
        Assert.True(a.ProbBelowBase is > 0.3 and < 0.8, $"ниже базы {a.ProbBelowBase}");
        Assert.Equal(0, a.ProbBelowZero);
    }

    [Fact]
    public void Cases_order_worst_base_best()
    {
        var b = AnalytikSimulatorEngineTests.Baseline(sugarAvailable: 5000);
        var r = ScenarioAnalysis.Cases(b, Def(), "sales.revenue", [new UncertainParameter("sales.demand", -20, 0, 15), new UncertainParameter("sales.price", -5, 0, 5)]);
        Assert.Equal(["worst", "base", "best"], r.Cases.Select(c => c.Name));
        Assert.True(r.Cases[0].Value < r.Cases[1].Value && r.Cases[1].Value < r.Cases[2].Value);
        Assert.Contains(r.Cases[0].Parameters, p => p.Code == "sales.demand" && p.Value == -20);
    }

    [Fact]
    public void Optimal_output_is_limited_by_sugar_with_its_shadow_price()
    {
        // Печенье: цена 20 000 сум/кг, сырьё 0,6 × 5000 + 0,2 × 8000 = 4600 сум/кг → маржа 15 400; сахара 120 кг → 600 кг печенья (мука 1500 кг
        // → 2500 кг, мощность 1800 кг, спрос 1500 кг). Теневая цена сахара — 15 400 ÷ 0,2 = 77 000 сум за кг. Вафли — без техкарты.
        var b = AnalytikSimulatorEngineTests.Baseline();
        var r = ScenarioAnalysis.Optimize(b, 30);
        Assert.Equal("optimal", r.Status);
        var cookies = Assert.Single(r.Plan);
        Assert.Equal(600, cookies.Kg, 6);
        Assert.Equal(15_400, cookies.MarginPerKg);
        Assert.Equal(600 * 15_400, r.Margin, 0);
        var sugar = Assert.Single(r.Constraints);
        Assert.Contains("Сахар", sugar.Name);
        Assert.Equal(77_000, sugar.ShadowPrice, 3);
        Assert.Contains(r.Excluded, e => e.StartsWith("Вафли"));
    }

    [Fact]
    public void Missing_inputs_are_explained_not_invented()
    {
        var b = AnalytikSimulatorEngineTests.Baseline();
        var ex = Assert.Throws<ComputeException>(() => ScenarioAnalysis.Sensitivity(b, Def(), "finance.operating_profit", ["sales.demand"]));
        Assert.Contains("не считается", ex.Message);
    }
}
