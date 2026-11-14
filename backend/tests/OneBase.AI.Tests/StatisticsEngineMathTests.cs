using OneBase.Application.Analytik.Compute;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>
/// Статистическая библиотека без базы: CorrelationMath, RegressionModels, TimeSeriesMath. Данные — детерминированный генератор (линейный
/// конгруэнтный + сумма 12 равномерных − 6, точно воспроизводится в Python), эталоны — scipy 1.13, statsmodels 0.14, pandas 2.3, scikit-learn
/// 1.6 на тех же данных. Допуск 1e-6 для формул в замкнутом виде, 1e-4 для итерационных методов.
/// </summary>
public sealed class StatisticsEngineMathTests
{
    private const double Tol = 1e-6;
    private const double IterTol = 1e-4;

    /// <summary>Шум ~N(0, 1): LCG (1664525, 1013904223, mod 2³²), сумма 12 равномерных − 6. Все операции точны — совпадает с Python бит в бит.</summary>
    private static double[] Noise(uint seed, int n)
    {
        var s = seed;
        var r = new double[n];
        for (var i = 0; i < n; i++)
        {
            var acc = 0d;
            for (var k = 0; k < 12; k++)
            {
                s = unchecked(s * 1664525u + 1013904223u);
                acc += s / 4294967296d;
            }

            r[i] = acc - 6;
        }

        return r;
    }

    private static double[] Build(int n, Func<int, double> f) => Enumerable.Range(0, n).Select(f).ToArray();

    private static void Close(double expected, double actual, double tol = Tol) => Assert.Equal(expected, actual, tol);

    private static void Close(double[] expected, IReadOnlyList<double> actual, double tol = Tol)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], actual[i], tol);
        }
    }

    [Fact]
    public void Generator_matches_python_reference() =>
        Close([-1.105566803831607, -0.24472779827192426, 0.17511342791840434], Noise(1, 3), 1e-15);

    // ---------------------------------------------------------------- CorrelationMath

    [Fact]
    public void Kendall_tau_b_with_ties_matches_scipy_asymptotic()
    {
        double[] x = [1, 2, 2, 3, 4, 4, 4, 5, 6, 7, 7, 8];
        double[] y = [2, 1, 3, 3, 5, 4, 6, 6, 8, 7, 9, 9];
        var r = CorrelationMath.Kendall(x, y);
        Assert.Equal("kendall", r.Method);
        Assert.Equal(12, r.N);
        Close(0.8710810532924891, r.R);
        Close(0.00015767962747712257, r.P);

        double[] x2 = [3.1, 1.2, 5.5, 2.2, 4.8, 6.0, 0.5, 7.7, 2.9, 5.1];
        double[] y2 = [2.0, 1.0, 4.0, 3.5, 3.0, 6.5, 2.5, 5.0, 1.5, 4.5];
        var r2 = CorrelationMath.Kendall(x2, y2);
        Close(0.6444444444444444, r2.R);
        Close(0.009491096053440909, r2.P);
    }

    [Fact]
    public void Kendall_refuses_constant_series_and_mismatched_lengths()
    {
        Assert.Throws<ComputeException>(() => CorrelationMath.Kendall([1, 1, 1, 1], [1, 2, 3, 4]));
        Assert.Throws<ComputeException>(() => CorrelationMath.Kendall([1, 2, 3], [1, 2, 3, 4]));
        Assert.Throws<ComputeException>(() => CorrelationMath.Kendall([1, 2], [2, 1]));
        Assert.Throws<ComputeException>(() => CorrelationMath.Kendall([1, double.NaN, 3], [1, 2, 3]));
    }

    [Fact]
    public void Fisher_confidence_intervals_for_all_three_methods()
    {
        var p = CorrelationMath.Ci("pearson", 0.5, 30)!;
        Close(0.17043136511180015, p.Lower);
        Close(0.7289585563883555, p.Upper);
        Assert.Equal(0.95, p.Level);

        var s = CorrelationMath.Ci("spearman", 0.5, 30)!;
        Close(0.1481321833905543, s.Lower);
        Close(0.7395031949196347, s.Upper);

        var k = CorrelationMath.Ci("kendall", 0.35, 30)!;
        Close(0.11088736089523701, k.Lower);
        Close(0.5508092529979798, k.Upper);

        var neg = CorrelationMath.Ci("Pearson", -0.62, 12)!;
        Close(-0.8805759579770298, neg.Lower);
        Close(-0.07156122762233917, neg.Upper);

        var ninety = CorrelationMath.Ci("pearson", 0.5, 30, 0.9)!;
        Close(0.22863994197302784, ninety.Lower);
        Close(0.6992637580848639, ninety.Upper);
    }

    [Fact]
    public void Confidence_interval_edge_cases()
    {
        Assert.Null(CorrelationMath.Ci("pearson", 0.5, 3));
        Assert.Null(CorrelationMath.Ci("spearman", 0.5, 3));
        Assert.Null(CorrelationMath.Ci("kendall", 0.5, 4));
        Assert.NotNull(CorrelationMath.Ci("kendall", 0.5, 5));
        var perfect = CorrelationMath.Ci("pearson", 1, 10)!;
        Assert.Equal(1, perfect.Lower);
        Assert.Equal(1, perfect.Upper);
        Assert.Throws<ComputeException>(() => CorrelationMath.Ci("cosine", 0.5, 30));
        Assert.Throws<ComputeException>(() => CorrelationMath.Ci("pearson", 1.5, 30));
    }

    private static (double[] X, double[] Y, double[] Z1, double[] Z2) PartialData()
    {
        const int n = 40;
        var z1 = Noise(1, n);
        var z2 = Noise(2, n);
        var e1 = Noise(3, n);
        var e2 = Noise(4, n);
        var x = Build(n, i => 10 + 2 * z1[i] + e1[i]);
        var y = Build(n, i => 5 + z1[i] - 0.8 * z2[i] + 0.6 * e1[i] + e2[i]);
        return (x, y, z1, z2);
    }

    [Fact]
    public void Partial_correlation_pearson_with_one_and_two_controls_matches_residual_method()
    {
        var (x, y, z1, z2) = PartialData();
        var one = CorrelationMath.Partial(x, y, [z1]);
        Assert.Equal("pearson", one.Method);
        Assert.Equal(1, one.Controls);
        Assert.Equal(37, one.Df);
        Close(0.4238032305588108, one.R);
        Close(0.007176008249401729, one.P);
        Close(0.12500094952967344, one.Ci!.Lower);
        Close(0.652120579307695, one.Ci.Upper);

        var two = CorrelationMath.Partial(x, y, [z1, z2]);
        Assert.Equal(36, two.Df);
        Close(0.35220911489652285, two.R);
        Close(0.030108437031231824, two.P);
        Close(0.036652682985397624, two.Ci!.Lower);
        Close(0.6038965064063619, two.Ci.Upper);
    }

    [Fact]
    public void Partial_correlation_spearman_ranks_all_variables()
    {
        var (x, y, z1, z2) = PartialData();
        var one = CorrelationMath.Partial(x, y, [z1], "spearman");
        Assert.Equal("spearman", one.Method);
        Close(0.444252992748014, one.R);
        Close(0.00460778553853257, one.P);
        Close(0.14972244613235886, one.Ci!.Lower);
        Close(0.6663659345458376, one.Ci.Upper);

        var two = CorrelationMath.Partial(x, y, [z1, z2], "spearman");
        Close(0.4364567930136868, two.R);
        Close(0.006153204464629785, two.P);
        Close(0.13570858099236435, two.Ci!.Lower);
        Close(0.663555583303598, two.Ci.Upper);
    }

    [Fact]
    public void Partial_correlation_validates_input()
    {
        var (x, y, z1, _) = PartialData();
        Assert.Throws<ComputeException>(() => CorrelationMath.Partial(x, y, [z1.Take(10).ToArray()]));
        Assert.Throws<ComputeException>(() => CorrelationMath.Partial(x, y.Take(39).ToArray(), [z1]));
        Assert.Throws<ComputeException>(() => CorrelationMath.Partial([1, 2, 3], [3, 1, 2], [new double[] { 1, 5, 2 }]));
        Assert.Throws<ComputeException>(() => CorrelationMath.Partial(x, y, [z1], "kendall"));
        // x полностью объясняется контрольной переменной.
        var explained = z1.Select(v => 3 * v + 1).ToArray();
        Assert.Throws<ComputeException>(() => CorrelationMath.Partial(explained, y, [z1]));
    }

    [Fact]
    public void Rolling_correlation_matches_pandas_and_scipy_windows()
    {
        const int n = 30;
        var a = Noise(5, n);
        var b = Noise(6, n);
        var x = Build(n, i => i * 0.3 + a[i]);
        var y = Build(n, i => 0.5 * x[i] + b[i]);
        var pearson = CorrelationMath.Rolling(x, y, 10);
        Assert.Equal(21, pearson.Count);
        Assert.Equal(9, pearson[0].End);
        Assert.Equal(10, pearson[0].N);
        Close(0.43271499705394606, pearson[0].R!.Value);
        Close(0.23736844222244077, pearson[6].R!.Value);
        Close(0.1945098355038745, pearson[20].R!.Value);

        var spearman = CorrelationMath.Rolling(x, y, 10, "spearman");
        Close(0.5515151515151515, spearman[0].R!.Value);
        Close(0.33333333333333326, spearman[6].R!.Value);
        Close(0.4424242424242424, spearman[20].R!.Value);

        var kendall = CorrelationMath.Rolling(x, y, 10, "kendall");
        Close(0.4222222222222222, kendall[0].R!.Value);
        Close(0.19999999999999998, kendall[6].R!.Value);
        Close(0.3333333333333333, kendall[20].R!.Value);
    }

    [Fact]
    public void Rolling_correlation_gives_null_in_flat_window()
    {
        double[] x = [5, 5, 5, 5, 1, 2, 3];
        double[] y = [1, 2, 3, 4, 2, 4, 5];
        var r = CorrelationMath.Rolling(x, y, 4);
        Assert.Null(r[0].R);
        Assert.NotNull(r[3].R);
        Assert.Throws<ComputeException>(() => CorrelationMath.Rolling(x, y, 2));
        Assert.Throws<ComputeException>(() => CorrelationMath.Rolling(x, y, 8));
    }

    [Fact]
    public void Benjamini_hochberg_matches_statsmodels_including_ties()
    {
        double[] p = [0.01, 0.04, 0.03, 0.005, 0.2, 0.04, 0.9, 0.0001];
        Close([0.02666666666666667, 0.05333333333333334, 0.05333333333333334, 0.02, 0.2285714285714286, 0.05333333333333334, 0.9, 0.0008],
            CorrelationMath.BenjaminiHochberg(p), 1e-12);
        Assert.Empty(CorrelationMath.BenjaminiHochberg([]));
        // Монотонность: q меньшего p не больше q следующего (0,9·2/1 = 1,8 → 0,95).
        Close([0.95, 0.95], CorrelationMath.BenjaminiHochberg([0.9, 0.95]), 1e-15);
        Assert.Throws<ComputeException>(() => CorrelationMath.BenjaminiHochberg([0.5, 1.2]));
    }

    [Fact]
    public void Jarque_bera_matches_scipy()
    {
        var v = Noise(7, 50);
        var x = v.Select(a => a + 0.3 * a * a).ToArray();
        var r = CorrelationMath.JarqueBera(x);
        Assert.Equal(50, r.N);
        Close(1.9291617155444918, r.Skewness);
        Close(4.4085272476631605, r.Kurtosis);
        Close(71.50369206724747, r.JarqueBera);
        Assert.Equal(2.972833249894482e-16, r.P, 2.972833249894482e-16 * 1e-6);
        Assert.Throws<ComputeException>(() => CorrelationMath.JarqueBera([3, 3, 3, 3, 3]));
        Assert.Throws<ComputeException>(() => CorrelationMath.JarqueBera([1, 2]));
    }

    [Fact]
    public void Outlier_scores_and_tukey_fences()
    {
        double[] x = [2, 3, 3, 4, 5, 5, 6, 40];
        Close([-1.1241666666666668, -0.6745, -0.6745, -0.22483333333333333, 0.22483333333333333, 0.22483333333333333, 0.6745, 15.963166666666666],
            CorrelationMath.ModifiedZ(x));
        // MAD = 0 — запасная формула через среднее абсолютное отклонение от медианы.
        Close([0, 0, 0, 0, 0, 4.787307889323825], CorrelationMath.ModifiedZ([5, 5, 5, 5, 5, 9]));
        Close([0, 0, 0], CorrelationMath.ModifiedZ([7, 7, 7]));

        Close([-0.5080074342257417, -0.42985244434485836, -0.42985244434485836, -0.35169745446397505, -0.27354246458309167, -0.27354246458309167,
            -0.19538747470220835, 2.4618821812478253], CorrelationMath.ZScores(x));
        Close([0, 0, 0], CorrelationMath.ZScores([4, 4, 4]));

        var f = CorrelationMath.Iqr(x);
        Close(3.0, f.Q1);
        Close(5.25, f.Q3);
        Close(2.25, f.Iqr);
        Close(-0.375, f.Low);
        Close(8.625, f.High);
        Close(12.0, CorrelationMath.Iqr(x, 3).High);
        Assert.Throws<ComputeException>(() => CorrelationMath.ZScores([1, double.PositiveInfinity]));
    }

    // ---------------------------------------------------------------- RegressionModels

    private static (double[] Y, double[] X1, double[] X2, double[] X3) OlsData()
    {
        const int n = 60;
        var x1 = Noise(11, n);
        var n12 = Noise(12, n);
        var x2 = Build(n, i => 0.6 * x1[i] + n12[i]);
        var x3 = Build(n, i => i / 10d);
        var e = Noise(13, n);
        var y = Build(n, i => 5 + 2 * x1[i] - x2[i] + 0.5 * x3[i] + e[i] * (1 + 0.5 * Math.Abs(x1[i])));
        return (y, x1, x2, x3);
    }

    [Fact]
    public void Ols_coefficients_and_confidence_intervals_match_statsmodels()
    {
        var (y, x1, x2, x3) = OlsData();
        var r = RegressionModels.Ols(y, [x1, x2, x3], ["x1", "x2", "x3"]);
        Assert.Equal(4, r.Coefficients.Count);
        Assert.Equal("(константа)", r.Coefficients[0].Name);
        Close([4.891455357345382, 2.246374335130719, -1.0095080463240138, 0.5075489277064811], r.Coefficients.Select(c => c.Coefficient).ToArray());
        Close([0.38292935659016947, 0.22872538104546858, 0.20836875990486242, 0.1154909652909455], r.Coefficients.Select(c => c.StdError).ToArray());
        Close([4.124355677781738, 1.7881823383864413, -1.42692083070127, 0.2761927233766128], r.Coefficients.Select(c => c.Lower).ToArray());
        Close([5.658555036909027, 2.704566331874997, -0.5920952619467577, 0.7389051320363493], r.Coefficients.Select(c => c.Upper).ToArray());
        Close(0.7307491852567755, r.Ols.RSquared);
        Close(-0.8430456068826344, r.Residuals[0]);
        Close(2.2775039145207465, r.Residuals[59]);

        var ninety = RegressionModels.Ols(y, [x1, x2, x3], ["x1", "x2", "x3"], 0.9);
        Close([4.250997467945943, 1.8638260340527129, -1.3580094445290964, 0.3143877124536473], ninety.Coefficients.Select(c => c.Lower).ToArray());
        Close([5.531913246744821, 2.628922636208725, -0.6610066481189312, 0.7007101429593149], ninety.Coefficients.Select(c => c.Upper).ToArray());
    }

    [Fact]
    public void Ols_diagnostics_match_statsmodels()
    {
        var (y, x1, x2, x3) = OlsData();
        var d = RegressionModels.Ols(y, [x1, x2, x3], ["x1", "x2", "x3"]).Diagnostics;
        Close(1.8328912500103907, d.DurbinWatson);
        Close(0.6310435067986476, d.JarqueBeraP);
        Close(0.09361620690560742, d.BreuschPaganP!.Value);
        Assert.Equal(["x1", "x2", "x3"], d.Vif.Select(v => v.Name));
        Close([1.5924392913828804, 1.4844022215957344, 1.0970635785005716], d.Vif.Select(v => v.Vif).ToArray());
        Close(2.0495444149898288, d.ConditionNumber);
        Close(1.4289809083233147, d.Rmse);
        Close(1.5558768880422318, d.LoocvRmse!.Value);
        Assert.Empty(d.Warnings);
    }

    [Fact]
    public void Ols_warns_about_multicollinearity_and_autocorrelation()
    {
        const int n = 40;
        var c1 = Noise(14, n);
        var n15 = Noise(15, n);
        var c2 = Build(n, i => c1[i] + 0.05 * n15[i]);
        var u = Noise(16, n);
        var ar = new double[n];
        for (var i = 0; i < n; i++)
        {
            ar[i] = (i > 0 ? 0.9 * ar[i - 1] : 0) + u[i];
        }

        var y = Build(n, i => 1 + c1[i] + c2[i] + ar[i]);
        var d = RegressionModels.Ols(y, [c1, c2], ["c1", "c2"]).Diagnostics;
        Close([573.4042942685408, 573.4042942685408], d.Vif.Select(v => v.Vif).ToArray(), 1e-5);
        Close(0.3634513953726804, d.DurbinWatson);
        Close(0.477165940070161, d.JarqueBeraP);
        Close(0.2327946243310141, d.BreuschPaganP!.Value);
        Close(1.7741302744680822, d.Rmse);
        Close(1.9415714524748058, d.LoocvRmse!.Value);
        Assert.Equal(2, d.Warnings.Count);
        Assert.Contains(d.Warnings, w => w.Contains("Мультиколлинеарность") && w.Contains("«c1»") && w.Contains("«c2»"));
        Assert.Contains(d.Warnings, w => w.Contains("Автокорреляция остатков") && w.Contains("0,36"));
    }

    [Fact]
    public void Ols_small_sample_warning_and_edge_cases()
    {
        double[] x = [1, 2, 3, 4, 5, 6, 7, 8];
        double[] y = [2.1, 3.9, 6.2, 8.1, 9.7, 12.2, 13.8, 16.1];
        var r = RegressionModels.Ols(y, [x], ["x"]);
        Assert.Contains(r.Diagnostics.Warnings, w => w.StartsWith("Мало наблюдений на признак"));
        Assert.Equal(1, r.Diagnostics.Vif[0].Vif);
        Assert.Equal(1, r.Diagnostics.ConditionNumber);

        // Без признаков (только константа): Бройш—Пэган не определён, VIF пуст.
        var constant = RegressionModels.Ols(y, [], []);
        Assert.Null(constant.Diagnostics.BreuschPaganP);
        Assert.Empty(constant.Diagnostics.Vif);

        // Точная подгонка — диагностика остатков не определена.
        Assert.Throws<ComputeException>(() => RegressionModels.Ols(x.Select(v => 2 * v + 1).ToArray(), [x], ["x"]));
        Assert.Throws<ComputeException>(() => RegressionModels.Ols(y, [x.Take(7).ToArray()], ["x"]));
        Assert.Throws<ComputeException>(() => RegressionModels.Ols(y, [x], ["x"], 1.5));
    }

    [Fact]
    public void Polynomial_matches_polyfit_on_centered_x()
    {
        const int n = 40;
        var x = Build(n, i => i);
        var e = Noise(21, n);
        var y = Build(n, i => 3 + 0.5 * x[i] - 0.02 * x[i] * x[i] + 0.001 * x[i] * x[i] * x[i] + e[i]);

        var quadratic = RegressionModels.Polynomial(y, x, 2);
        Assert.Equal(["(константа)", "x", "x^2"], quadratic.Coefficients.Select(c => c.Name));
        Close([12.613139320991174, 1.1198067013294462, 0.03718613119423957], quadratic.Coefficients.Select(c => c.Coefficient).ToArray());
        Close([0.3421042501277849, 0.01974726517635986, 0.0019144152455959642], quadratic.Coefficients.Select(c => c.StdError).ToArray());
        Close(0.9898071006332084, quadratic.Ols.RSquared);
        Close(1.2009365426673289, quadratic.Diagnostics.DurbinWatson);

        var cubic = RegressionModels.Polynomial(y, x, 3, "день");
        Assert.Equal(["(константа)", "день", "день^2", "день^3"], cubic.Coefficients.Select(c => c.Name));
        Close([12.613139320991174, 0.9128445514536072, 0.037186131194239594, 0.000863601710310197], cubic.Coefficients.Select(c => c.Coefficient).ToArray());
        Close([0.22939682428743108, 0.03317641040740956, 0.001283704536097165, 0.00012693247525607685], cubic.Coefficients.Select(c => c.StdError).ToArray());
        Close(0.9955408059021423, cubic.Ols.RSquared);
        Close(2.5834647341297043, cubic.Diagnostics.DurbinWatson);

        Assert.Throws<ComputeException>(() => RegressionModels.Polynomial(y, x, 4));
        Assert.Throws<ComputeException>(() => RegressionModels.Polynomial(y, x, 0));
    }

    private static (double[] Y, IReadOnlyList<IReadOnlyList<double>> X, string[] Names) PenaltyData()
    {
        const int n = 50;
        var n30 = Noise(30, n);
        var n31 = Noise(31, n);
        var n32 = Noise(32, n);
        var n33 = Noise(33, n);
        var n34 = Noise(34, n);
        var n35 = Noise(35, n);
        var x0 = Build(n, i => 10 + 2 * n30[i]);
        var x1 = Build(n, i => 5 * n31[i]);
        var x2 = Build(n, i => n32[i] + 0.5 * n30[i]);
        var x3 = Build(n, i => 100 + 20 * n33[i]);
        var x4 = n34;
        var y = Build(n, i => 20 + 1.5 * x0[i] + 0.8 * x1[i] - 2 * x2[i] + 3 * n35[i]);
        return (y, [x0, x1, x2, x3, x4], ["x0", "x1", "x2", "x3", "x4"]);
    }

    [Fact]
    public void Ridge_at_fixed_lambda_matches_sklearn_on_standardized_data()
    {
        var (y, x, names) = PenaltyData();
        var r = RegressionModels.Ridge(y, x, names, 10);
        Assert.Equal("ridge", r.Model);
        Assert.Equal("given", r.LambdaChoice);
        Assert.Equal(10, r.Lambda);
        Assert.Equal(50, r.N);
        Close([0.902744068184416, 0.6659201177924735, -1.6690125746194557, -0.0065509539014361375, 0.7845323480434042],
            r.Coefficients.Select(c => c.Coefficient).ToArray());
        Close(26.32561681302577, r.Intercept);
        Close(0.6644971908279627, r.RSquared);
        Close(2.7592965785864054, r.Rmse);
        Close(3.0705020617719594, r.CvRmse!.Value);
        Assert.Equal(names, r.Selected);
    }

    [Fact]
    public void Ridge_gcv_choice_is_deterministic_and_matches_numpy_replication()
    {
        var (y, x, names) = PenaltyData();
        var a = RegressionModels.Ridge(y, x, names);
        var b = RegressionModels.Ridge(y, x, names);
        Assert.Equal("gcv", a.LambdaChoice);
        Close(0.9653488644416248, a.Lambda, 1e-9);
        Close(2.8975995575662457, a.CvRmse!.Value);
        Assert.Equal(a.Lambda, b.Lambda);
        Assert.Equal(a.Coefficients, b.Coefficients);
        Assert.Throws<ComputeException>(() => RegressionModels.Ridge(y, x, names, -1));
    }

    [Fact]
    public void Lasso_at_fixed_lambda_matches_sklearn_on_standardized_data()
    {
        var (y, x, names) = PenaltyData();
        var half = RegressionModels.Lasso(y, x, names, 0.5);
        Assert.Equal("lasso", half.Model);
        Assert.Equal("given", half.LambdaChoice);
        Close([0.7470760150949501, 0.6708817678365577, -1.4933609464421482, 0, 0.31819156672119553], half.Coefficients.Select(c => c.Coefficient).ToArray(),
            IterTol);
        Close(27.225836790234403, half.Intercept, IterTol);
        Close(0.6369426433485599, half.RSquared, IterTol);
        Close(2.8703702666633135, half.Rmse, IterTol);
        Close(3.4302612496103544, half.CvRmse!.Value, IterTol);
        Assert.Equal(["x0", "x1", "x2", "x4"], half.Selected);
        Assert.Equal(0, half.Coefficients[3].Coefficient);

        var two = RegressionModels.Lasso(y, x, names, 2);
        Close([0, 0.21005104371670652, 0, 0, 0], two.Coefficients.Select(c => c.Coefficient).ToArray(), IterTol);
        Close(34.54580005533594, two.Intercept, IterTol);
        Close(0.20840059606883776, two.RSquared, IterTol);
        Close(4.238410640572951, two.Rmse, IterTol);
        Assert.Equal(["x1"], two.Selected);

        // λ выше λmax = max|Xᵀy|/n обнуляет все коэффициенты.
        var all = RegressionModels.Lasso(y, x, names, 3);
        Assert.Empty(all.Selected);
        Close(y.Average(), all.Intercept, 1e-9);
    }

    [Fact]
    public void Lasso_cross_validation_is_deterministic_and_matches_sklearn_replication()
    {
        var (y, x, names) = PenaltyData();
        var a = RegressionModels.Lasso(y, x, names);
        var b = RegressionModels.Lasso(y, x, names);
        Assert.Equal("cv5", a.LambdaChoice);
        // λmax = 2,954543850819304; выбран 18-й из 30 (индекс 17), как в sklearn на тех же блоках.
        Close(0.05150741451587035, a.Lambda, 1e-9);
        Close(3.077341188429622, a.CvRmse!.Value, IterTol);
        Assert.Equal(a.Lambda, b.Lambda);
        Assert.Equal(a.Coefficients, b.Coefficients);
        Assert.Throws<ComputeException>(() => RegressionModels.Lasso(y.Take(8).ToArray(), x.Select(c => (IReadOnlyList<double>)c.Take(8).ToArray()).ToList(), names));
    }

    [Fact]
    public void Penalized_regressions_refuse_constant_predictor_and_bad_input()
    {
        var (y, x, names) = PenaltyData();
        var withConstant = x.Take(4).Append(Enumerable.Repeat(3d, 50).ToArray()).ToList();
        var ex = Assert.Throws<ComputeException>(() => RegressionModels.Ridge(y, withConstant, names, 1));
        Assert.Contains("x4", ex.Message);
        Assert.Throws<ComputeException>(() => RegressionModels.Lasso(y, withConstant, names, 1));
        Assert.Throws<ComputeException>(() => RegressionModels.Ridge(y, x, names.Take(3).ToArray(), 1));
        Assert.Throws<ComputeException>(() => RegressionModels.Lasso(Enumerable.Repeat(1d, 50).ToArray(), x, names, 1));
    }

    // ---------------------------------------------------------------- TimeSeriesMath

    private static readonly double[] Pattern7 = [5, -3, 2, 0, -4, 8, -8];

    [Fact]
    public void Decompose_period_7_matches_statsmodels_seasonal_decompose()
    {
        const int n = 56;
        var e = Noise(41, n);
        var y = Build(n, i => 100 + 0.5 * i + Pattern7[i % 7] + e[i]);
        var d = TimeSeriesMath.Decompose(y, 7);
        Assert.Equal(7, d.Period);
        Assert.Null(d.Trend[2]);
        Assert.Null(d.Residual[53]);
        Assert.NotNull(d.Trend[3]);
        Close(101.886625239426, d.Trend[3]!.Value);
        Close(-0.03217202233851119, d.Seasonal[3]);
        Close(0.8202104975113796, d.Residual[3]!.Value);
        Close(104.62988333197842, d.Trend[10]!.Value);
        Close(-0.9998597692122532, d.Residual[10]!.Value);
        Close(113.35951808154849, d.Trend[27]!.Value);
        Close(-8.192046004203593, d.Seasonal[27]);
        Close(0.7328506798507881, d.Residual[27]!.Value);
        Close(126.31842583956728, d.Trend[52]!.Value);
        Close(0.999609987867825, d.Residual[52]!.Value);
        Close([5.072755994324082, -3.3710558115567126, 2.091324461444822, -0.03217202233851119, -3.545394794586452, 7.976588176916365, -8.192046004203593],
            d.SeasonalIndex);
        Close(0, d.SeasonalIndex.Sum(), 1e-12);
        Close(0.9779418646184671, d.SeasonalStrength);
    }

    [Fact]
    public void Decompose_even_period_uses_2x12_moving_average()
    {
        double[] pattern = [3, 1, -2, -5, -1, 0, 4, 6, 2, -3, -4, -1];
        const int n = 48;
        var e = Noise(42, n);
        var y = Build(n, i => 50 + 0.2 * i + pattern[i % 12] + e[i]);
        var d = TimeSeriesMath.Decompose(y, 12);
        Assert.Null(d.Trend[5]);
        Assert.Null(d.Trend[42]);
        Close(51.390808639395985, d.Trend[6]!.Value);
        Close(4.048210854883545, d.Seasonal[6]);
        Close(0.2811697318598094, d.Residual[6]!.Value);
        Close(54.21890348490948, d.Trend[20]!.Value);
        Close(2.773193593378421, d.Seasonal[20]);
        Close(1.1701947842483182, d.Residual[20]!.Value);
        Close(58.426547111291434, d.Trend[41]!.Value);
        Close(0.03880648701279499, d.Seasonal[41]);
        Close(-0.3355538585671626, d.Residual[41]!.Value);
        Assert.Throws<ComputeException>(() => TimeSeriesMath.Decompose(y.Take(23).ToArray(), 12));
        Assert.Throws<ComputeException>(() => TimeSeriesMath.Decompose(y, 1));
    }

    [Fact]
    public void Rolling_stats_match_pandas()
    {
        var a = Noise(43, 20);
        var y = a.Select(v => 10 + 3 * v).ToArray();
        var r = TimeSeriesMath.Rolling(y, 5);
        Assert.Equal(16, r.Count);
        double[][] expected =
        [
            [7.224992093909532, 3.7079433701126447, 2.822749086190015, 11.823264684993774],
            [10.018766895402223, 3.896082292628994, 4.87998614506796, 14.72305402578786],
            [10.1363012698479, 1.299229218829187, 8.47059588553384, 12.036015514750034],
        ];
        int[] ends = [4, 12, 19];
        for (var k = 0; k < ends.Length; k++)
        {
            var s = r[ends[k] - 4];
            Assert.Equal(ends[k], s.End);
            Close(expected[k], [s.Mean, s.StdDev, s.Min, s.Max]);
        }

        Assert.Throws<ComputeException>(() => TimeSeriesMath.Rolling(y, 1));
        Assert.Throws<ComputeException>(() => TimeSeriesMath.Rolling(y, 21));
    }

    [Fact]
    public void Ses_picks_alpha_by_grid_and_fitted_values_match_hand_recursion()
    {
        const int n = 40;
        var a = Noise(51, n);
        var y = Build(n, i => 50 + 5 * a[i] + (i >= 20 ? 10 : 0));
        var r = TimeSeriesMath.Smooth(y, "ses", horizon: 3);
        Assert.Equal("ses", r.Kind);
        Assert.Equal(0.25, r.Alpha);
        Assert.Null(r.Beta);
        Assert.Null(r.Gamma);
        Assert.Equal(0, r.Season);
        Close(1521.221979532875, r.Sse, 1e-8);
        Close(Math.Sqrt(1521.221979532875 / 39), r.Rmse, 1e-9);

        // Рекурсия вручную: прогноз на t — уровень после t − 1.
        var level = y[0];
        Assert.Equal(y[0], r.Fitted[0]);
        for (var t = 1; t < n; t++)
        {
            Close(level, r.Fitted[t], 1e-12);
            level = r.Alpha * y[t] + (1 - r.Alpha) * level;
        }

        Close([level, level, level], r.Forecast, 1e-12);
        // И statsmodels SimpleExpSmoothing (initial_level = y[0], α = 0,25).
        Close([54.193295936565846, 54.193295936565846, 50.59732261033787, 60.18395088419678], [r.Fitted[0], r.Fitted[1], r.Fitted[5], r.Fitted[39]]);
        Close(60.61182421661718, r.Forecast[0]);
    }

    [Fact]
    public void Holt_matches_statsmodels_with_known_initial_state()
    {
        var a = Noise(52, 30);
        var y = Build(30, i => 20 + 1.5 * i + 2 * a[i]);
        var r = TimeSeriesMath.Smooth(y, "holt", horizon: 2);
        Assert.Equal(0.5, r.Alpha);
        Assert.Equal(0.35, r.Beta);
        Close(226.53545405025574, r.Sse, 1e-8);
        Assert.Equal(y[0], r.Fitted[0]);
        Close([24.22371665854007, 28.21234590280801, 32.7950192791262, 62.041708141751606], [r.Fitted[1], r.Fitted[2], r.Fitted[10], r.Fitted[29]]);
        Close([63.44675806830148, 64.68201120913912], r.Forecast);
    }

    [Fact]
    public void Holt_winters_is_deterministic_and_follows_the_season()
    {
        const int n = 42;
        var e = Noise(53, n);
        var y = Build(n, i => 100 + 0.3 * i + Pattern7[i % 7] + e[i]);
        var a = TimeSeriesMath.Smooth(y, "holt_winters", 7, 7);
        var b = TimeSeriesMath.Smooth(y, "holt_winters", 7, 7);
        Assert.Equal("holt_winters", a.Kind);
        Assert.Equal(7, a.Season);
        Assert.NotNull(a.Beta);
        Assert.NotNull(a.Gamma);
        Assert.Equal(a.Alpha, b.Alpha);
        Assert.Equal(a.Beta, b.Beta);
        Assert.Equal(a.Gamma, b.Gamma);
        Assert.Equal(a.Fitted, b.Fitted);
        Assert.Equal(a.Forecast, b.Forecast);
        Assert.Equal(n, a.Fitted.Count);
        Assert.Equal(7, a.Forecast.Count);
        // Прогноз повторяет недельный рисунок: отклонение от истинной линии без шума — в пределах 3 σ шума.
        for (var h = 0; h < 7; h++)
        {
            var truth = 100 + 0.3 * (n + h) + Pattern7[(n + h) % 7];
            Assert.InRange(a.Forecast[h], truth - 3, truth + 3);
        }

        var ses = TimeSeriesMath.Smooth(y, "ses");
        Assert.True(a.Rmse < ses.Rmse / 2, $"Хольт—Винтерс {a.Rmse} должен быть заметно точнее простого сглаживания {ses.Rmse}");
        Assert.Throws<ComputeException>(() => TimeSeriesMath.Smooth(y.Take(13).ToArray(), "holt_winters", 7));
        Assert.Throws<ComputeException>(() => TimeSeriesMath.Smooth(y, "arima"));
        Assert.Throws<ComputeException>(() => TimeSeriesMath.Smooth([1, 2], "ses"));
    }

    [Fact]
    public void Change_points_find_planted_shift_and_nothing_in_noise()
    {
        const int n = 80;
        var a = Noise(61, n);
        var y = Build(n, i => (i < 40 ? 100 : 130) + 5 * a[i]);
        var points = TimeSeriesMath.ChangePoints(y);
        var cp = Assert.Single(points);
        Assert.InRange(cp.Index, 39, 41);
        Assert.InRange(cp.MeanBefore, 97, 103);
        Assert.InRange(cp.MeanAfter, 127, 133);
        Close(cp.MeanAfter - cp.MeanBefore, cp.Shift, 1e-12);
        Close(cp.Shift / cp.MeanBefore * 100, cp.ShiftPct!.Value, 1e-9);
        Assert.True(cp.P < 1e-10);
        Assert.Equal(points, TimeSeriesMath.ChangePoints(y));

        var b = Noise(62, n);
        Assert.Empty(TimeSeriesMath.ChangePoints(b.Select(v => 100 + 5 * v).ToArray()));
        Assert.Empty(TimeSeriesMath.ChangePoints(Enumerable.Repeat(7d, 30).ToArray()));
        Assert.Throws<ComputeException>(() => TimeSeriesMath.ChangePoints(y.Take(9).ToArray()));
    }

    [Fact]
    public void Change_points_find_two_shifts_sorted_by_index()
    {
        var a = Noise(63, 90);
        var y = Build(90, i => (i < 30 ? 50 : i < 60 ? 80 : 60) + 4 * a[i]);
        var points = TimeSeriesMath.ChangePoints(y);
        Assert.Equal(2, points.Count);
        Assert.InRange(points[0].Index, 29, 31);
        Assert.InRange(points[1].Index, 59, 61);
        Assert.True(points[0].Shift > 0 && points[1].Shift < 0);
        Assert.Single(TimeSeriesMath.ChangePoints(y, maxPoints: 1));
    }

    [Fact]
    public void Chow_test_detects_planted_slope_break()
    {
        const int n = 60;
        var e = Noise(71, n);
        var y = Build(n, i => (i < 30 ? 10 + 0.5 * i : 25 + 2 * (i - 30)) + e[i]);
        var r = TimeSeriesMath.Chow(y, 30);
        Assert.Equal(30, r.Break);
        Close(1353.6548000610462, r.F, 1e-6 * 1353.6548000610462);
        Assert.Equal(3.883626653916662e-48, r.P, 3.883626653916662e-48 * 1e-4);
        Close(0.49781462576766566, r.SlopeBefore);
        Close(1.9960268767254294, r.SlopeAfter);

        var e2 = Noise(72, n);
        var flat = Build(n, i => 10 + 0.5 * i + e2[i]);
        var none = TimeSeriesMath.Chow(flat, 30);
        Close(2.8650221057673786, none.F);
        Close(0.06536726162670864, none.P);

        Assert.Throws<ComputeException>(() => TimeSeriesMath.Chow(y, 2));
        Assert.Throws<ComputeException>(() => TimeSeriesMath.Chow(y, 58));
        Assert.Throws<ComputeException>(() => TimeSeriesMath.Chow(Build(10, i => 2 * i), 5));
    }
}
