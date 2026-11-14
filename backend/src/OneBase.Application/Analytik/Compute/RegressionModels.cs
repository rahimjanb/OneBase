using System.Globalization;

namespace OneBase.Application.Analytik.Compute;

/// <summary>Коэффициент регрессии с доверительным интервалом: значение, стандартная ошибка, t, p (двусторонний), нижняя и верхняя граница.</summary>
public sealed record CoefficientCi(string Name, double Coefficient, double StdError, double T, double P, double Lower, double Upper);

/// <summary>
/// Диагностика линейной регрессии: Дарбин—Уотсон (≈ 2 — нет автокорреляции остатков), p Харке—Бера остатков (нормальность), p Бройша—Пэгана
/// (гетероскедастичность; null — в модели нет признаков), VIF каждого признака, число обусловленности стандартизованных признаков, RMSE на
/// обучении, RMSE скользящего контроля с исключением по одному (LOOCV; null — есть точка с рычагом 1), предупреждения для человека.
/// </summary>
public sealed record RegressionDiagnostics(double DurbinWatson, double JarqueBeraP, double? BreuschPaganP, IReadOnlyList<(string Name, double Vif)> Vif,
    double ConditionNumber, double Rmse, double? LoocvRmse, IReadOnlyList<string> Warnings);

/// <summary>Подробная регрессия: итог МНК, коэффициенты с интервалами, остатки (y − прогноз), диагностика.</summary>
public sealed record DetailedRegression(RegressionResult Ols, IReadOnlyList<CoefficientCi> Coefficients, IReadOnlyList<double> Residuals,
    RegressionDiagnostics Diagnostics);

/// <summary>
/// Регрессия со штрафом: модель (ridge — гребневая, lasso — лассо), λ и как он выбран (given — задан, gcv — обобщённая перекрёстная проверка,
/// cv5 — 5 блоков подряд), константа и коэффициенты в исходных единицах признаков, отобранные признаки (ненулевой коэффициент), R² и RMSE на
/// обучении, RMSE перекрёстной проверки (null — наблюдений мало для проверки), число наблюдений.
/// </summary>
public sealed record PenalizedRegression(string Model, double Lambda, string LambdaChoice, double Intercept, IReadOnlyList<(string Name, double Coefficient)> Coefficients,
    IReadOnlyList<string> Selected, double RSquared, double Rmse, double? CvRmse, int N);

/// <summary>
/// Регрессионные модели поверх МНК (RegressionMath.Ols): интервалы коэффициентов и диагностика (Дарбин—Уотсон, Харке—Бера, Бройш—Пэган, VIF,
/// число обусловленности, LOOCV), полиномиальная регрессия, гребневая (ridge) и лассо (lasso) регрессия на стандартизованных признаках. Без
/// внешних библиотек, детерминировано; эталоны — statsmodels, numpy, scikit-learn (см. тесты).
/// </summary>
public static class RegressionModels
{
    private const int CvFolds = 5;
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    /// <summary>
    /// МНК с константой (RegressionMath.Ols) и подробностями: интервалы коэффициентов (квантиль Стьюдента, df = n − k), остатки, Дарбин—Уотсон
    /// (как statsmodels durbin_watson), p Харке—Бера остатков, p Бройша—Пэгана в варианте Коэнкера LM = n·R² регрессии e² на признаки (как
    /// statsmodels het_breuschpagan, lm_pvalue), VIF каждого признака (как statsmodels variance_inflation_factor на матрице с константой), число
    /// обусловленности стандартизованных признаков √(λmax/λmin), RMSE = √(SSE/n), LOOCV RMSE через PRESS (eᵢ/(1 − hᵢᵢ)) и предупреждения.
    /// x — список столбцов (каждый — значения одного признака). Точная подгонка (нулевые остатки) — ComputeException: диагностика не определена.
    /// </summary>
    public static DetailedRegression Ols(IReadOnlyList<double> y, IReadOnlyList<IReadOnlyList<double>> x, IReadOnlyList<string> names, double level = 0.95)
    {
        StatMath.Require(level > 0 && level < 1, "Доверительная вероятность — строго между 0 и 1.");
        ComputeGuard.Finite(y);
        foreach (var col in x)
        {
            ComputeGuard.Finite(col);
        }

        var ols = RegressionMath.Ols(y, x, names);
        var n = ols.N;
        var k = ols.K;
        var p = x.Count;
        var q = StatMath.TQuantile(1 - (1 - level) / 2, n - k);
        var coefficients = ols.Terms
            .Select(t => new CoefficientCi(t.Name, t.Coefficient, t.StdError, t.T, t.P, t.Coefficient - q * t.StdError, t.Coefficient + q * t.StdError))
            .ToList();

        var residuals = new double[n];
        var sse = 0d;
        var yScale = 1d;
        for (var i = 0; i < n; i++)
        {
            residuals[i] = y[i] - ols.Fitted[i];
            sse += residuals[i] * residuals[i];
            yScale = Math.Max(yScale, Math.Abs(y[i]));
        }

        StatMath.Require(residuals.Any(e => Math.Abs(e) > 1e-12 * yScale),
            "Модель описывает данные без ошибки (остатки равны нулю) — диагностика остатков не определена.");

        var dwNum = 0d;
        for (var i = 1; i < n; i++)
        {
            dwNum += (residuals[i] - residuals[i - 1]) * (residuals[i] - residuals[i - 1]);
        }

        var dw = dwNum / sse;
        var jbP = CorrelationMath.JarqueBera(residuals).P;
        double? bpP = null;
        if (p > 0)
        {
            var squared = residuals.Select(e => e * e).ToArray();
            var lm = n * RegressionMath.Ols(squared, x, names).RSquared;
            bpP = ChiSquareSf(lm, p);
        }

        var vif = new List<(string Name, double Vif)>(p);
        for (var j = 0; j < p; j++)
        {
            if (p == 1)
            {
                vif.Add((names[j], 1));
                continue;
            }

            var others = x.Where((_, idx) => idx != j).ToList();
            var otherNames = names.Where((_, idx) => idx != j).ToList();
            var r2 = RegressionMath.Ols(x[j], others, otherNames).RSquared;
            vif.Add((names[j], 1 / Math.Max(1 - r2, 1e-12)));
        }

        var condition = ConditionNumber(x);
        var rmse = Math.Sqrt(sse / n);
        var loocv = Loocv(x, residuals);

        var warnings = new List<string>();
        var collinear = vif.Where(v => v.Vif > 10).Select(v => $"«{v.Name}» ({N(v.Vif, 1)})").ToList();
        if (collinear.Count > 0)
        {
            warnings.Add($"Мультиколлинеарность: VIF больше 10 у {string.Join(", ", collinear)} — коэффициенты этих признаков неустойчивы, их знак и величину трактовать осторожно.");
        }

        if (dw < 1.5 || dw > 2.5)
        {
            warnings.Add($"Автокорреляция остатков: статистика Дарбина—Уотсона {N(dw, 2)} (норма около 2) — ошибки идут сериями, p-значения занижены; возможно, пропущен тренд или сезонность.");
        }

        if (jbP < 0.05)
        {
            warnings.Add($"Остатки не нормальны (Харке—Бера, p = {P(jbP)}) — p-значения и интервалы коэффициентов приблизительны; проверьте выбросы.");
        }

        if (bpP is < 0.05)
        {
            warnings.Add($"Гетероскедастичность (Бройш—Пэган, p = {P(bpP.Value)}) — разброс ошибок зависит от признаков, стандартные ошибки коэффициентов ненадёжны.");
        }

        if ((double)n / k < 10)
        {
            warnings.Add($"Мало наблюдений на признак: {n} наблюдений на {k} параметров модели (n/k = {N((double)n / k, 1)}, желательно от 10) — оценки неустойчивы.");
        }

        if (loocv is { } cv && cv > 1.3 * rmse)
        {
            warnings.Add($"Возможное переобучение: ошибка на скользящем контроле (LOOCV RMSE {N(cv, 2)}) в {N(cv / rmse, 1)} раза больше ошибки на обучении (RMSE {N(rmse, 2)}).");
        }

        var diagnostics = new RegressionDiagnostics(dw, jbP, bpP, vif, condition, rmse, loocv, warnings);
        return new DetailedRegression(ols, coefficients, residuals, diagnostics);
    }

    /// <summary>
    /// Полиномиальная регрессия y ~ x + x² (+ x³), степень 1…3. x центрируется по среднему до возведения в степень (иначе x и x² почти
    /// коллинеарны); признаки называются name, name^2, name^3. Итог — подробная регрессия со всей диагностикой.
    /// </summary>
    public static DetailedRegression Polynomial(IReadOnlyList<double> y, IReadOnlyList<double> x, int degree, string name = "x", double level = 0.95)
    {
        StatMath.Require(degree is >= 1 and <= 3, "Степень многочлена — от 1 до 3.");
        StatMath.Require(x.Count == y.Count, "Признак и отклик разной длины.");
        StatMath.Require(x.Count >= 1, "Нет наблюдений для регрессии.");
        ComputeGuard.Finite(x);
        var mean = StatMath.Mean(x);
        var columns = new List<IReadOnlyList<double>>(degree);
        var names = new List<string>(degree);
        for (var d = 1; d <= degree; d++)
        {
            var power = d;
            columns.Add(x.Select(v => Math.Pow(v - mean, power)).ToArray());
            names.Add(d == 1 ? name : $"{name}^{d}");
        }

        return Ols(y, columns, names, level);
    }

    /// <summary>
    /// Гребневая регрессия на стандартизованных признаках (z-оценки со стандартным отклонением генеральной совокупности, отклик центрирован):
    /// минимум ‖y − Xb‖² + λ‖b‖² (как sklearn Ridge(alpha=λ) на тех же данных). λ задан или выбирается обобщённой перекрёстной проверкой (GCV)
    /// по 50 значениям n·10⁻⁴…n·10⁴ (равномерно по логарифму): GCV = (SSE/n)/(1 − (1 + tr H)/n)², единица — за константу. Коэффициенты —
    /// в исходных единицах признаков, с константой; CvRmse — √GCV.
    /// </summary>
    public static PenalizedRegression Ridge(IReadOnlyList<double> y, IReadOnlyList<IReadOnlyList<double>> x, IReadOnlyList<string> names, double? lambda = null)
    {
        var data = Standardize(y, x, names);
        var n = data.N;
        var p = data.P;
        var gram = Gram(data.Z, n, p, 1);
        var zy = Cross(data.Z, data.Yc, n, p, 1);

        (double[] Beta, double? Gcv) Fit(double lam)
        {
            var a = (double[,])gram.Clone();
            for (var j = 0; j < p; j++)
            {
                a[j, j] += lam;
            }

            var inv = RegressionMath.Invert(a);
            var beta = new double[p];
            var trace = 0d;
            for (var j = 0; j < p; j++)
            {
                for (var l = 0; l < p; l++)
                {
                    beta[j] += inv[j, l] * zy[l];
                    trace += inv[j, l] * gram[l, j];
                }
            }

            var rss = 0d;
            for (var i = 0; i < n; i++)
            {
                var fit = 0d;
                for (var j = 0; j < p; j++)
                {
                    fit += data.Z[i, j] * beta[j];
                }

                rss += (data.Yc[i] - fit) * (data.Yc[i] - fit);
            }

            var denominator = 1 - (1 + trace) / n;
            return (beta, denominator > 1e-12 ? rss / n / (denominator * denominator) : null);
        }

        double chosen;
        string choice;
        if (lambda is { } given)
        {
            StatMath.Require(given >= 0 && double.IsFinite(given), "λ — неотрицательное число.");
            chosen = given;
            choice = "given";
        }
        else
        {
            chosen = double.NaN;
            var best = double.MaxValue;
            for (var i = 0; i < 50; i++)
            {
                var lam = n * Math.Pow(10, -4 + 8d * i / 49);
                if (Fit(lam).Gcv is { } g && g < best)
                {
                    best = g;
                    chosen = lam;
                }
            }

            StatMath.Require(!double.IsNaN(chosen), "Мало наблюдений, чтобы подобрать λ обобщённой перекрёстной проверкой.");
            choice = "gcv";
        }

        var (b, gcv) = Fit(chosen);
        return Penalized("ridge", chosen, choice, data, names, b, gcv is { } v ? Math.Sqrt(v) : null);
    }

    /// <summary>
    /// Лассо циклическим покоординатным спуском на стандартизованных признаках (стандартное отклонение генеральной совокупности, отклик
    /// центрирован): минимум (1/(2n))‖y − Xb‖² + λ‖b‖₁ (соглашение sklearn Lasso), точность 1e-10 (изменение коэффициента за проход относительно
    /// наибольшего), не больше 10 000 проходов. λ задан или выбирается 5-блочной перекрёстной проверкой с блоками подряд (без перемешивания) по
    /// 30 значениям λmax·10^(0…−3), λmax = max|Xᵀy|/n; в каждом блоке признаки стандартизуются по обучающей части. Коэффициенты — в исходных
    /// единицах признаков; отобранные — с ненулевым коэффициентом. CvRmse — RMSE 5-блочной проверки при выбранном λ (null — меньше 10 наблюдений).
    /// </summary>
    public static PenalizedRegression Lasso(IReadOnlyList<double> y, IReadOnlyList<IReadOnlyList<double>> x, IReadOnlyList<string> names, double? lambda = null)
    {
        var data = Standardize(y, x, names);
        var n = data.N;
        var p = data.P;
        var gram = Gram(data.Z, n, p, n);
        var zy = Cross(data.Z, data.Yc, n, p, n);
        double chosen;
        string choice;
        double? cvRmse;
        if (lambda is { } given)
        {
            StatMath.Require(given >= 0 && double.IsFinite(given), "λ — неотрицательное число.");
            chosen = given;
            choice = "given";
            cvRmse = n >= 2 * CvFolds ? Math.Sqrt(LassoCv(y, x, [chosen])[0] / n) : null;
        }
        else
        {
            StatMath.Require(n >= 2 * CvFolds, $"Для подбора λ перекрёстной проверкой нужно хотя бы {2 * CvFolds} наблюдений.");
            var lambdaMax = zy.Select(Math.Abs).Max();
            var grid = Enumerable.Range(0, 30).Select(i => lambdaMax * Math.Pow(10, -3d * i / 29)).ToArray();
            var sse = LassoCv(y, x, grid);
            var bestIndex = 0;
            for (var i = 1; i < grid.Length; i++)
            {
                if (sse[i] < sse[bestIndex])
                {
                    bestIndex = i;
                }
            }

            chosen = grid[bestIndex];
            choice = "cv5";
            cvRmse = Math.Sqrt(sse[bestIndex] / n);
        }

        var beta = CoordinateDescent(gram, zy, chosen, new double[p]);
        return Penalized("lasso", chosen, choice, data, names, beta, cvRmse);
    }

    private sealed record StandardizedData(int N, int P, double[,] Z, double[] Yc, double[] Means, double[] Sds, double YMean);

    private static StandardizedData Standardize(IReadOnlyList<double> y, IReadOnlyList<IReadOnlyList<double>> x, IReadOnlyList<string> names)
    {
        StatMath.Require(x.Count == names.Count, "Число признаков и их названий не совпадает.");
        StatMath.Require(x.Count >= 1, "Нужен хотя бы один признак.");
        StatMath.Require(y.Count >= 3, "Для регрессии нужно хотя бы 3 наблюдения.");
        ComputeGuard.Finite(y);
        foreach (var col in x)
        {
            StatMath.Require(col.Count == y.Count, "Признак и отклик разной длины.");
            ComputeGuard.Finite(col);
        }

        var n = y.Count;
        var p = x.Count;
        var yMean = StatMath.Mean(y);
        var yc = y.Select(v => v - yMean).ToArray();
        StatMath.Require(yc.Any(v => v != 0), "Отклик не меняется — регрессию строить не на чем.");
        var z = new double[n, p];
        var means = new double[p];
        var sds = new double[p];
        for (var j = 0; j < p; j++)
        {
            means[j] = StatMath.Mean(x[j]);
            var ss = 0d;
            for (var i = 0; i < n; i++)
            {
                ss += (x[j][i] - means[j]) * (x[j][i] - means[j]);
            }

            sds[j] = Math.Sqrt(ss / n);
            StatMath.Require(sds[j] > 0, $"Признак «{names[j]}» не меняется — уберите его из модели.");
            for (var i = 0; i < n; i++)
            {
                z[i, j] = (x[j][i] - means[j]) / sds[j];
            }
        }

        return new StandardizedData(n, p, z, yc, means, sds, yMean);
    }

    private static PenalizedRegression Penalized(string model, double lambda, string choice, StandardizedData data, IReadOnlyList<string> names, double[] beta,
        double? cvRmse)
    {
        var coefficients = new List<(string Name, double Coefficient)>(data.P);
        var intercept = data.YMean;
        for (var j = 0; j < data.P; j++)
        {
            var c = beta[j] / data.Sds[j];
            coefficients.Add((names[j], c));
            intercept -= c * data.Means[j];
        }

        // Прогноз в исходных единицах равен ȳ + Σ bⱼ·zᵢⱼ — остатки считаются в стандартизованном пространстве.
        double sse = 0, sst = 0;
        for (var i = 0; i < data.N; i++)
        {
            var residual = data.Yc[i];
            for (var j = 0; j < data.P; j++)
            {
                residual -= beta[j] * data.Z[i, j];
            }

            sse += residual * residual;
            sst += data.Yc[i] * data.Yc[i];
        }

        var selected = coefficients.Where(c => c.Coefficient != 0).Select(c => c.Name).ToList();
        return new PenalizedRegression(model, lambda, choice, intercept, coefficients, selected, 1 - sse / sst, Math.Sqrt(sse / data.N), cvRmse, data.N);
    }

    /// <summary>ZᵀZ / divisor.</summary>
    private static double[,] Gram(double[,] z, int n, int p, double divisor)
    {
        var g = new double[p, p];
        for (var a = 0; a < p; a++)
        {
            for (var b = a; b < p; b++)
            {
                var s = 0d;
                for (var i = 0; i < n; i++)
                {
                    s += z[i, a] * z[i, b];
                }

                g[a, b] = s / divisor;
                g[b, a] = s / divisor;
            }
        }

        return g;
    }

    /// <summary>Zᵀy / divisor.</summary>
    private static double[] Cross(double[,] z, double[] y, int n, int p, double divisor)
    {
        var c = new double[p];
        for (var j = 0; j < p; j++)
        {
            var s = 0d;
            for (var i = 0; i < n; i++)
            {
                s += z[i, j] * y[i];
            }

            c[j] = s / divisor;
        }

        return c;
    }

    /// <summary>
    /// Покоординатный спуск лассо в ковариационной форме: G = ZᵀZ/n, c = Zᵀy/n, bⱼ = S(cⱼ − Σₖ≠ⱼ Gⱼₖbₖ, λ)/Gⱼⱼ; начинает с start (тёплый старт по
    /// сетке λ). Признак без разброса (Gⱼⱼ = 0) получает 0.
    /// </summary>
    private static double[] CoordinateDescent(double[,] g, double[] c, double lambda, double[] start)
    {
        const double tol = 1e-10;
        const int maxSweeps = 10_000;
        var p = c.Length;
        var b = (double[])start.Clone();
        for (var sweep = 0; sweep < maxSweeps; sweep++)
        {
            double maxDelta = 0, maxAbs = 0;
            for (var j = 0; j < p; j++)
            {
                var next = 0d;
                if (g[j, j] > 0)
                {
                    var rho = c[j];
                    for (var l = 0; l < p; l++)
                    {
                        if (l != j)
                        {
                            rho -= g[j, l] * b[l];
                        }
                    }

                    next = Math.Sign(rho) * Math.Max(Math.Abs(rho) - lambda, 0) / g[j, j];
                }

                maxDelta = Math.Max(maxDelta, Math.Abs(next - b[j]));
                b[j] = next;
                maxAbs = Math.Max(maxAbs, Math.Abs(next));
            }

            if (maxDelta <= tol * Math.Max(1, maxAbs))
            {
                break;
            }
        }

        return b;
    }

    /// <summary>
    /// Сумма квадратов ошибок 5-блочной перекрёстной проверки лассо для каждого λ сетки (по убыванию — тёплый старт): блоки подряд, признаки
    /// стандартизуются по обучающей части, прогноз — в исходных единицах.
    /// </summary>
    private static double[] LassoCv(IReadOnlyList<double> y, IReadOnlyList<IReadOnlyList<double>> x, IReadOnlyList<double> grid)
    {
        var n = y.Count;
        var p = x.Count;
        var sse = new double[grid.Count];
        for (var f = 0; f < CvFolds; f++)
        {
            var start = f * n / CvFolds;
            var end = (f + 1) * n / CvFolds;
            var train = Enumerable.Range(0, n).Where(i => i < start || i >= end).ToArray();
            var m = train.Length;
            var yMean = train.Average(i => y[i]);
            var means = new double[p];
            var sds = new double[p];
            var z = new double[m, p];
            for (var j = 0; j < p; j++)
            {
                var col = x[j];
                means[j] = train.Average(i => col[i]);
                sds[j] = Math.Sqrt(train.Sum(i => (col[i] - means[j]) * (col[i] - means[j])) / m);
                for (var r = 0; r < m; r++)
                {
                    z[r, j] = sds[j] > 0 ? (col[train[r]] - means[j]) / sds[j] : 0;
                }
            }

            var yc = train.Select(i => y[i] - yMean).ToArray();
            var g = Gram(z, m, p, m);
            var c = Cross(z, yc, m, p, m);
            var beta = new double[p];
            for (var li = 0; li < grid.Count; li++)
            {
                beta = CoordinateDescent(g, c, grid[li], beta);
                for (var i = start; i < end; i++)
                {
                    var fit = yMean;
                    for (var j = 0; j < p; j++)
                    {
                        if (sds[j] > 0)
                        {
                            fit += beta[j] * (x[j][i] - means[j]) / sds[j];
                        }
                    }

                    sse[li] += (y[i] - fit) * (y[i] - fit);
                }
            }
        }

        return sse;
    }

    /// <summary>Число обусловленности стандартизованных признаков: √(λmax/λmin) корреляционной матрицы (без признаков или с одним — 1).</summary>
    private static double ConditionNumber(IReadOnlyList<IReadOnlyList<double>> x)
    {
        var p = x.Count;
        if (p < 2)
        {
            return 1;
        }

        var n = x[0].Count;
        var centered = x.Select(col =>
        {
            var mean = StatMath.Mean(col);
            var c = col.Select(v => v - mean).ToArray();
            var norm = Math.Sqrt(c.Sum(v => v * v));
            return c.Select(v => v / norm).ToArray();
        }).ToArray();
        var corr = new double[p, p];
        for (var a = 0; a < p; a++)
        {
            for (var b = a; b < p; b++)
            {
                var s = 0d;
                for (var i = 0; i < n; i++)
                {
                    s += centered[a][i] * centered[b][i];
                }

                corr[a, b] = s;
                corr[b, a] = s;
            }
        }

        var eig = SymmetricEigenvalues(corr);
        var max = eig.Max();
        var min = Math.Max(eig.Min(), max * 1e-32);
        return Math.Sqrt(max / min);
    }

    /// <summary>Собственные значения симметричной матрицы (циклический метод Якоби).</summary>
    private static double[] SymmetricEigenvalues(double[,] matrix)
    {
        var n = matrix.GetLength(0);
        var a = (double[,])matrix.Clone();
        for (var sweep = 0; sweep < 100; sweep++)
        {
            double off = 0, total = 0;
            for (var i = 0; i < n; i++)
            {
                for (var j = 0; j < n; j++)
                {
                    total += a[i, j] * a[i, j];
                    if (i != j)
                    {
                        off += a[i, j] * a[i, j];
                    }
                }
            }

            if (off <= 1e-30 * total)
            {
                break;
            }

            for (var pIdx = 0; pIdx < n - 1; pIdx++)
            {
                for (var qIdx = pIdx + 1; qIdx < n; qIdx++)
                {
                    if (a[pIdx, qIdx] == 0)
                    {
                        continue;
                    }

                    var theta = (a[qIdx, qIdx] - a[pIdx, pIdx]) / (2 * a[pIdx, qIdx]);
                    var t = Math.Sign(theta == 0 ? 1 : theta) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1));
                    var cos = 1 / Math.Sqrt(t * t + 1);
                    var sin = t * cos;
                    for (var k = 0; k < n; k++)
                    {
                        var akp = a[k, pIdx];
                        var akq = a[k, qIdx];
                        a[k, pIdx] = cos * akp - sin * akq;
                        a[k, qIdx] = sin * akp + cos * akq;
                    }

                    for (var k = 0; k < n; k++)
                    {
                        var apk = a[pIdx, k];
                        var aqk = a[qIdx, k];
                        a[pIdx, k] = cos * apk - sin * aqk;
                        a[qIdx, k] = sin * apk + cos * aqk;
                    }
                }
            }
        }

        return Enumerable.Range(0, n).Select(i => a[i, i]).ToArray();
    }

    /// <summary>LOOCV RMSE через PRESS: остаток без точки i равен eᵢ/(1 − hᵢᵢ), h — диагональ матрицы-шляпы X(XᵀX)⁻¹Xᵀ с константой.</summary>
    private static double? Loocv(IReadOnlyList<IReadOnlyList<double>> x, IReadOnlyList<double> residuals)
    {
        var n = residuals.Count;
        var k = x.Count + 1;
        var design = new double[n, k];
        for (var i = 0; i < n; i++)
        {
            design[i, 0] = 1;
            for (var j = 0; j < x.Count; j++)
            {
                design[i, j + 1] = x[j][i];
            }
        }

        var inv = RegressionMath.Invert(Gram(design, n, k, 1));
        var press = 0d;
        for (var i = 0; i < n; i++)
        {
            var h = 0d;
            for (var a = 0; a < k; a++)
            {
                for (var b = 0; b < k; b++)
                {
                    h += design[i, a] * inv[a, b] * design[i, b];
                }
            }

            if (h >= 1 - 1e-10)
            {
                return null;
            }

            var e = residuals[i] / (1 - h);
            press += e * e;
        }

        return Math.Sqrt(press / n);
    }

    /// <summary>P(χ²(df) ≥ x) = Q(df/2, x/2) — регуляризованная верхняя неполная гамма-функция (ряд или цепная дробь).</summary>
    private static double ChiSquareSf(double x, int df)
    {
        if (x <= 0)
        {
            return 1;
        }

        var a = df / 2d;
        var z = x / 2;
        var logFront = -z + a * Math.Log(z) - StatMath.LogGamma(a);
        if (z < a + 1)
        {
            double sum = 1 / a, term = sum;
            for (var i = 1; i < 1000; i++)
            {
                term *= z / (a + i);
                sum += term;
                if (Math.Abs(term) < Math.Abs(sum) * 1e-16)
                {
                    break;
                }
            }

            return Math.Clamp(1 - sum * Math.Exp(logFront), 0, 1);
        }

        const double tiny = 1e-300;
        double bb = z + 1 - a, c = 1 / tiny, d = 1 / bb, h = d;
        for (var i = 1; i < 1000; i++)
        {
            var an = -i * (i - a);
            bb += 2;
            d = an * d + bb;
            d = Math.Abs(d) < tiny ? tiny : d;
            c = bb + an / c;
            c = Math.Abs(c) < tiny ? tiny : c;
            d = 1 / d;
            var del = d * c;
            h *= del;
            if (Math.Abs(del - 1) < 1e-16)
            {
                break;
            }
        }

        return Math.Clamp(Math.Exp(logFront) * h, 0, 1);
    }

    private static string N(double value, int digits) => value.ToString("N" + digits, Ru);

    private static string P(double p) => p < 0.001 ? "< 0,001" : p.ToString("0.000", Ru);
}
