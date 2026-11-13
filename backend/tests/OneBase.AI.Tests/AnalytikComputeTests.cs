using OneBase.Application.Analytik.Compute;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>
/// Вычислительный движок Advanced Intelligence без базы: эталоны — известные наборы (квартет Анскомба, табличные t) и независимый расчёт
/// другим алгоритмом (численное интегрирование плотности Стьюдента в Python, см. отчёт). Все методы детерминированы.
/// </summary>
public sealed class AnalytikComputeTests
{
    private const double Tol = 1e-4;

    [Theory]
    [InlineData(2.228138852, 10, 0.05)]
    [InlineData(1.0, 1, 0.5)]
    [InlineData(3.5, 7.3, 0.009343)]
    public void Student_two_sided_p_matches_reference(double t, double df, double p) =>
        Assert.Equal(p, StatMath.TwoSidedT(t, df), 5);

    [Fact]
    public void Normal_distribution_quantile_and_cdf_are_consistent()
    {
        Assert.Equal(1.959964, StatMath.NormalQuantile(0.975), 5);
        Assert.Equal(0.975, StatMath.NormalCdf(1.959964), 6);
        Assert.Equal(-1.644854, StatMath.NormalQuantile(0.05), 5);
        Assert.Equal(0.5, StatMath.NormalCdf(0), 9);
    }

    [Fact]
    public void Welch_test_matches_independent_reference()
    {
        double[] a = [19.8, 20.4, 19.6, 17.8, 18.5, 18.9, 18.3, 18.9, 19.5, 22.0];
        double[] b = [28.2, 26.6, 20.1, 23.3, 25.2, 22.1, 17.7, 27.6, 20.6, 13.7, 23.2, 17.5, 20.6, 18.0, 23.9, 21.6, 24.3, 20.4, 23.9, 13.3];
        var r = StatMath.Welch(a, b);
        Assert.Equal(2.225512, r.T, 5);
        Assert.Equal(24.524635, r.Df, 4);
        Assert.InRange(r.P, 0.035485 - 1e-5, 0.035485 + 1e-5);
        Assert.True(r.CiLow > 0 && r.CiHigh > r.Diff, "95 % интервал разницы не содержит ноль");
    }

    [Fact]
    public void Quantile_and_describe_follow_excel_conventions()
    {
        Assert.Equal(1.75, StatMath.Quantile([1, 2, 3, 4], 0.25), 9);
        var d = StatMath.Describe([2, 4, 4, 4, 5, 5, 7, 9]);
        Assert.Equal(5, d.Mean, 9);
        Assert.Equal(2.13809, d.StdDev, 5);
        Assert.Equal(4.5, d.Median, 9);
    }

    [Fact]
    public void Regression_reproduces_anscombe_quartet()
    {
        double[] x = [10, 8, 13, 9, 11, 14, 6, 4, 12, 7, 5];
        double[] y = [8.04, 6.95, 7.58, 8.81, 8.33, 9.96, 7.24, 4.26, 10.84, 4.82, 5.68];
        var r = RegressionMath.Ols(y, [x], ["x"]);
        Assert.Equal(3.000091, r.Terms[0].Coefficient, 5);
        Assert.Equal(0.500091, r.Terms[1].Coefficient, 5);
        Assert.Equal(0.117906, r.Terms[1].StdError, 5);
        Assert.Equal(4.241455, r.Terms[1].T, 4);
        Assert.Equal(0.002170, r.Terms[1].P, 5);
        Assert.Equal(0.666542, r.RSquared, 5);
        // Простая регрессия: F = t², p(F) = p(t).
        Assert.Equal(r.Terms[1].T * r.Terms[1].T, r.F, 6);
        Assert.Equal(r.Terms[1].P, r.FP, 6);
    }

    [Fact]
    public void Collinear_features_are_refused_with_clear_message()
    {
        double[] x1 = [1, 2, 3, 4, 5, 6, 7];
        double[] x2 = [2, 4, 6, 8, 10, 12, 14];
        double[] y = [3, 5, 7, 9, 12, 13, 15];
        var ex = Assert.Throws<ComputeException>(() => RegressionMath.Ols(y, [x1, x2], ["a", "b"]));
        Assert.Contains("коллинеарность", ex.Message);
    }

    [Fact]
    public void Spearman_matches_reference_and_handles_ties()
    {
        var r = StatMath.Spearman([1, 2, 3, 4, 5, 6, 7, 8], [2, 1, 4, 3, 7, 8, 6, 9]);
        Assert.Equal(0.880952, r.R, 5);
        Assert.Equal(0.003850, r.P, 5);
        Assert.Equal([1.5, 1.5, 3], StatMath.Ranks([5, 5, 9]));
    }

    [Fact]
    public void Trend_and_lag_are_found_in_constructed_series()
    {
        var trend = SeriesMath.Trend(Enumerable.Range(0, 20).Select(i => 100 + 2.5 * i).ToList());
        Assert.Equal(2.5, trend.Slope, 9);
        Assert.Equal(1, trend.RSquared, 9);

        // y(t) = x(t − 3) + слабый шум: x опережает y на 3 шага.
        var rng = new SplitMix64(7);
        var x = Enumerable.Range(0, 60).Select(_ => rng.NextDouble() * 100).ToList();
        var y = Enumerable.Range(0, 60).Select(t => (t >= 3 ? x[t - 3] : 50) + rng.NextDouble()).ToList();
        var lag = SeriesMath.CrossCorrelation(x, y, 6);
        Assert.Equal(3, lag.Best!.Lag);
        Assert.True(lag.Best.R > 0.99);
        Assert.True(lag.BestAdjustedP < 0.001);
    }

    [Fact]
    public void Abc_xyz_classify_by_cumulative_share_and_variation()
    {
        var abc = SegmentMath.Abc([("a", 50), ("b", 30), ("c", 10), ("d", 5), ("e", 5), ("z", 0)]);
        Assert.Equal("A A B B C C", string.Join(" ", abc.Select(i => i.Class)));
        Assert.Equal(100, abc.Where(i => i.Value > 0).Sum(i => i.SharePct), 9);

        var xyz = SegmentMath.Xyz([("flat", [10, 10, 10, 10]), ("mid", [10, 12, 8, 10]), ("spiky", [0, 0, 30, 0]), ("none", [0, 0, 0])])
            .ToDictionary(i => i.Key);
        Assert.Equal("X", xyz["flat"].Class);
        Assert.Equal("Y", xyz["mid"].Class);
        Assert.Equal(0.163299, xyz["mid"].Cv!.Value, 5);
        Assert.Equal("Z", xyz["spiky"].Class);
        Assert.Null(xyz["none"].Cv);
        Assert.Equal("Z", xyz["none"].Class);
    }

    [Fact]
    public void Rfm_scores_and_segments_are_deterministic()
    {
        var rfm = SegmentMath.Rfm(
        [
            ("best", 1, 30, 9_000), ("loyal", 10, 25, 4_000), ("new", 2, 1, 100), ("risk", 80, 20, 5_000), ("lost", 200, 2, 200),
            ("mid1", 30, 8, 1_000), ("mid2", 40, 6, 900), ("mid3", 50, 5, 800), ("mid4", 60, 4, 700), ("mid5", 70, 3, 600),
        ]).ToDictionary(i => i.Key);
        Assert.Equal("Чемпионы", rfm["best"].Segment);
        Assert.Equal((5, 5, 5), (rfm["best"].R, rfm["best"].F, rfm["best"].M));
        Assert.Equal("Новые", rfm["new"].Segment);
        Assert.Equal("Потерянные", rfm["lost"].Segment);
        Assert.Equal("В зоне риска", rfm["risk"].Segment);
    }

    [Fact]
    public void Cohorts_measure_retention_from_first_period()
    {
        var cohorts = SegmentMath.Cohorts([("a", 0), ("a", 1), ("a", 2), ("b", 0), ("b", 2), ("c", 1), ("c", 2)], 2);
        Assert.Equal(2, cohorts.Count);
        Assert.Equal(2, cohorts[0].Size);
        Assert.Equal([1, 0.5, 1], cohorts[0].Retention);
        Assert.Equal([1, 1], cohorts[1].Retention);
    }

    [Fact]
    public void Kmeans_separates_distinct_groups_and_is_reproducible()
    {
        var points = new List<(string, IReadOnlyList<double>)>();
        for (var i = 0; i < 10; i++)
        {
            points.Add(($"small{i}", [10 + i % 3, 1 + i % 2]));
            points.Add(($"big{i}", [1000 + i % 4, 50 + i % 3]));
        }

        var a = SegmentMath.KMeans(points, 2, 1);
        var b = SegmentMath.KMeans(points, 2, 1);
        Assert.Equal(a.Inertia, b.Inertia);
        Assert.All(a.Clusters, c => Assert.Equal(10, c.Size));
        Assert.All(a.Clusters, c => Assert.True(c.Members.All(m => m.StartsWith("small")) || c.Members.All(m => m.StartsWith("big"))));
        Assert.True(a.Silhouette > 0.9);
    }

    [Fact]
    public void Contribution_explains_the_whole_change()
    {
        var r = DriverMath.Contribution([("Хоразм", 100, 60), ("Тошкент", 200, 210), ("Бухоро", 50, 50)]);
        Assert.Equal(-30, r.Delta, 9);
        Assert.Equal("Хоразм", r.Items[0].Key);
        Assert.Equal(-40 / -30d, r.Items[0].ShareOfNet!.Value, 9);
        Assert.Equal(1, r.Items.Sum(i => i.ShareOfNet ?? 0), 9);
        Assert.Equal(-40 / 350d * 100, r.Items[0].ContributionPp!.Value, 9);
    }

    [Fact]
    public void Price_volume_mix_reconciles_to_revenue_change_exactly()
    {
        var r = DriverMath.PriceVolumeMix(
        [
            ("A", 100, 1_000, 120, 1_320), // цена 10 → 11, объём +20
            ("B", 50, 1_000, 40, 800), // цена 20, объём −10
            ("C", 10, 300, 0, -30), // выбыла; в B только возврат
            ("D", 0, 0, 5, 75), // новая
        ]);
        Assert.Equal(r.RevenueB - r.RevenueA, r.Volume + r.Mix + r.Price + r.NewItems + r.LostItems, 9);
        Assert.Equal(120, r.Price, 9); // 120 × (11 − 10)
        Assert.Equal(75, r.NewItems, 9);
        Assert.Equal(-330, r.LostItems, 9);
        Assert.Equal((1, 1, 2), (r.New, r.Lost, r.Common));
    }

    [Fact]
    public void Before_after_needs_a_real_step_to_claim_a_statistical_change()
    {
        var rng = new SplitMix64(3);
        var before = Enumerable.Range(0, 14).Select(_ => 100 + rng.NextNormal()).ToList();
        var after = Enumerable.Range(0, 14).Select(_ => 120 + rng.NextNormal()).ToList();
        var step = CausalMath.BeforeAfter(before, after);
        Assert.Equal(EvidenceClasses.Statistical, step.EvidenceClass);
        Assert.Equal(20, step.Test.Diff, 0);

        // Те же значения в обратном порядке: средние равны — изменения нет.
        var flat = CausalMath.BeforeAfter(before, before.AsEnumerable().Reverse().ToList());
        Assert.Equal(EvidenceClasses.Observed, flat.EvidenceClass);
        Assert.Contains(flat.Notes, n => n.Contains("контрольной группы"));
    }

    [Fact]
    public void Difference_in_differences_finds_the_effect_only_with_parallel_trends()
    {
        var rng = new SplitMix64(11);
        var control = Enumerable.Range(0, 20).Select(t => 100 + t + rng.NextNormal()).ToList();
        var treated = Enumerable.Range(0, 20).Select(t => 80 + t + (t >= 10 ? 10 : 0) + rng.NextNormal()).ToList();
        var did = CausalMath.DifferenceInDifferences(treated, control, 10);
        Assert.Equal(10, did.Effect, 0);
        Assert.True(did.EffectP < 0.001);
        Assert.Equal(EvidenceClasses.TestedCausal, did.EvidenceClass);

        // До события группа росла в 3 раза быстрее контроля — причинный вывод недопустим.
        var diverging = Enumerable.Range(0, 20).Select(t => 80 + 3 * t + (t >= 10 ? 10 : 0) + rng.NextNormal()).ToList();
        var bad = CausalMath.DifferenceInDifferences(diverging, control, 10);
        Assert.NotEqual(EvidenceClasses.TestedCausal, bad.EvidenceClass);
        Assert.Contains(bad.Notes, n => n.Contains("расходились"));
    }

    [Fact]
    public void Inventory_policy_uses_wilson_and_safety_stock_formulas()
    {
        Assert.Equal(200, InventoryMath.Eoq(1000, 10, 0.5), 9);
        var daily = Enumerable.Range(0, 30).Select(i => i % 2 == 0 ? 8d : 12d).ToList();
        var p = InventoryMath.Policy("сахар", daily, 9, 0.95, stock: 95);
        var sigma = StatMath.StdDev(daily);
        Assert.Equal(1.644854 * sigma * 3, p.SafetyStock, 4);
        Assert.Equal(10 * 9 + p.SafetyStock, p.ReorderPoint, 9);
        Assert.True(p.BelowReorder);
        Assert.Equal(9.5, p.DaysOfSupply!.Value, 9);
        Assert.Equal("медленная", InventoryMath.Turnover("x", 30, 100, 30).Class);
    }

    [Fact]
    public void Linear_program_solves_classic_problem_with_shadow_prices()
    {
        var r = LinearProgram.Maximize(
            [new LpVariable("x", 3), new LpVariable("y", 5)],
            [new LpConstraint("c1", [1, 0], 4), new LpConstraint("c2", [0, 2], 12), new LpConstraint("c3", [3, 2], 18)]);
        Assert.Equal("optimal", r.Status);
        Assert.Equal(36, r.Objective, 9);
        Assert.Equal(2, r.Values[0].Value, 9);
        Assert.Equal(6, r.Values[1].Value, 9);
        Assert.Equal([0, 1.5, 1], r.Constraints.Select(c => Math.Round(c.ShadowPrice, 9)));
        Assert.False(r.Constraints[0].Binding);

        var infeasible = LinearProgram.Maximize([new LpVariable("x", 1, Min: 10)], [new LpConstraint("cap", [1], 5)]);
        Assert.Equal("infeasible", infeasible.Status);
        var unbounded = LinearProgram.Maximize([new LpVariable("x", 1), new LpVariable("y", 1)], [new LpConstraint("c", [1, -1], 5)]);
        Assert.Equal("unbounded", unbounded.Status);
        var capped = LinearProgram.Maximize([new LpVariable("x", 2, 1, 3), new LpVariable("y", 1)], [new LpConstraint("c", [1, 1], 10)]);
        Assert.Equal(3, capped.Values[0].Value, 9);
        Assert.Equal(7, capped.Values[1].Value, 9);
    }
}
