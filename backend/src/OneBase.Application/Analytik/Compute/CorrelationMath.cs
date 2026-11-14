namespace OneBase.Application.Analytik.Compute;

/// <summary>Доверительный интервал коэффициента корреляции: нижняя и верхняя граница, доверительная вероятность (0,95 — 95 %).</summary>
public sealed record CorrelationCi(double Lower, double Upper, double Level);

/// <summary>
/// Частная корреляция x и y при фиксированных контрольных переменных: метод, число наблюдений, число контрольных, коэффициент, p (двусторонний),
/// степени свободы (n − 2 − k), 95 % интервал (null — наблюдений слишком мало для интервала).
/// </summary>
public sealed record PartialCorrelationResult(string Method, int N, int Controls, double R, double P, int Df, CorrelationCi? Ci);

/// <summary>Скользящая корреляция: End — индекс последнего элемента окна, N — точек в окне, R — коэффициент (null — в окне один из рядов не меняется).</summary>
public sealed record RollingPoint(int End, int N, double? R);

/// <summary>Проверка нормальности (Харке—Бера): число точек, асимметрия, эксцесс (избыточный, у нормального 0), статистика и её p.</summary>
public sealed record NormalityResult(int N, double Skewness, double Kurtosis, double JarqueBera, double P);

/// <summary>Границы Тьюки: квартили (тип 7), межквартильный размах, нижняя Q1 − k·IQR и верхняя Q3 + k·IQR граница выбросов.</summary>
public sealed record IqrFences(double Q1, double Q3, double Iqr, double Low, double High);

/// <summary>
/// Корреляции и выбросы без внешних библиотек: тау-b Кендалла, доверительные интервалы через z Фишера, частная корреляция, скользящая
/// корреляция, поправка Бенджамини—Хохберга на множественные проверки, тест нормальности Харке—Бера, модифицированный z, z-оценки и границы
/// Тьюки. Всё детерминировано; эталоны — scipy, statsmodels, pandas (см. тесты).
/// </summary>
public static class CorrelationMath
{
    private const string PearsonMethod = "pearson";
    private const string SpearmanMethod = "spearman";
    private const string KendallMethod = "kendall";

    /// <summary>
    /// Тау-b Кендалла с поправкой на совпадения; p — нормальное приближение с дисперсией, учитывающей совпадения (как
    /// scipy.stats.kendalltau(x, y, method='asymptotic')).
    /// </summary>
    public static CorrelationResult Kendall(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        ComputeGuard.Pairs(x, y, 3, "Для корреляции Кендалла нужно хотя бы 3 пары значений.");
        var n = x.Count;
        // S = согласованные − несогласованные пары; совпадения по x, по y.
        long s = 0, xTies = 0, yTies = 0;
        for (var i = 0; i < n; i++)
        {
            for (var j = i + 1; j < n; j++)
            {
                var sx = Math.Sign(x[i] - x[j]);
                var sy = Math.Sign(y[i] - y[j]);
                s += sx * sy;
                if (sx == 0)
                {
                    xTies++;
                }

                if (sy == 0)
                {
                    yTies++;
                }
            }
        }

        var total = (long)n * (n - 1) / 2;
        StatMath.Require(xTies < total && yTies < total, "Один из рядов не меняется — корреляция не определена.");
        var tau = Math.Clamp(s / Math.Sqrt(total - xTies) / Math.Sqrt(total - yTies), -1, 1);
        var (x0, x1) = TieSums(x);
        var (y0, y1) = TieSums(y);
        double m = n * (n - 1d);
        var variance = (m * (2 * n + 5) - x1 - y1) / 18 + 2d * xTies * yTies / m + x0 * y0 / (9 * m * (n - 2));
        var z = s / Math.Sqrt(variance);
        return new CorrelationResult(KendallMethod, n, tau, TwoSidedNormal(z));
    }

    /// <summary>
    /// Доверительный интервал коэффициента через преобразование Фишера z = atanh(r): Пирсон — se = 1/√(n−3), Спирмен — поправка
    /// Бонетта—Райта se = √((1 + r²/2)/(n−3)), Кендалл — поправка Филлера se = √(0,437/(n−4)). Null — наблюдений слишком мало (n ≤ 3, у Кендалла
    /// n ≤ 4); |r| = 1 — интервал [r, r].
    /// </summary>
    public static CorrelationCi? Ci(string method, double r, int n, double level = 0.95)
    {
        var m = NormalizeMethod(method, allowKendall: true);
        StatMath.Require(!double.IsNaN(r) && Math.Abs(r) <= 1, "Коэффициент корреляции должен быть от −1 до 1.");
        StatMath.Require(level > 0 && level < 1, "Доверительная вероятность — строго между 0 и 1.");
        var minN = m == KendallMethod ? 4 : 3;
        if (n <= minN)
        {
            return null;
        }

        if (Math.Abs(r) >= 1)
        {
            return new CorrelationCi(r, r, level);
        }

        var se = m switch
        {
            SpearmanMethod => Math.Sqrt((1 + r * r / 2) / (n - 3)),
            KendallMethod => Math.Sqrt(0.437 / (n - 4)),
            _ => 1 / Math.Sqrt(n - 3),
        };
        return FisherCi(r, se, level);
    }

    /// <summary>
    /// Частная корреляция x и y при фиксированных контрольных переменных: x и y очищаются от контрольных (МНК с константой), коррелируются
    /// остатки. Спирмен — то же на рангах всех переменных. df = n − 2 − k, p по t = r·√(df/(1−r²)), 95 % интервал Фишера с se = 1/√(n−3−k).
    /// </summary>
    public static PartialCorrelationResult Partial(IReadOnlyList<double> x, IReadOnlyList<double> y, IReadOnlyList<IReadOnlyList<double>> controls,
        string method = PearsonMethod)
    {
        var m = NormalizeMethod(method, allowKendall: false);
        ComputeGuard.Pairs(x, y, 3, "Для частной корреляции нужно хотя бы 3 пары значений.");
        foreach (var c in controls)
        {
            StatMath.Require(c.Count == x.Count, "Контрольная переменная и ряды разной длины.");
            ComputeGuard.Finite(c);
        }

        var n = x.Count;
        var k = controls.Count;
        var df = n - 2 - k;
        StatMath.Require(df >= 1, $"Мало наблюдений для частной корреляции: {n} при {k} контрольных (нужно хотя бы {k + 3}).");
        IReadOnlyList<double> xs = x, ys = y;
        var cs = controls;
        if (m == SpearmanMethod)
        {
            xs = StatMath.Ranks(x);
            ys = StatMath.Ranks(y);
            cs = controls.Select(c => (IReadOnlyList<double>)StatMath.Ranks(c)).ToList();
        }

        var rx = Residualize(xs, cs, "x");
        var ry = Residualize(ys, cs, "y");
        var r = StatMath.Pearson(rx, ry).R;
        double p;
        if (Math.Abs(r) >= 1)
        {
            p = 0;
        }
        else
        {
            p = StatMath.TwoSidedT(r * Math.Sqrt(df / (1 - r * r)), df);
        }

        CorrelationCi? ci = null;
        if (n - 3 - k >= 1)
        {
            ci = Math.Abs(r) >= 1 ? new CorrelationCi(r, r, 0.95) : FisherCi(r, 1 / Math.Sqrt(n - 3 - k), 0.95);
        }

        return new PartialCorrelationResult(m, n, k, r, p, df, ci);
    }

    /// <summary>
    /// Скользящая корреляция по окну из window последних точек (как pandas x.rolling(window).corr(y) для Пирсона): метод pearson, spearman или
    /// kendall. Точка на каждый конец окна End ≥ window − 1; в окне без вариации одного из рядов R = null.
    /// </summary>
    public static IReadOnlyList<RollingPoint> Rolling(IReadOnlyList<double> x, IReadOnlyList<double> y, int window, string method = PearsonMethod)
    {
        var m = NormalizeMethod(method, allowKendall: true);
        ComputeGuard.Pairs(x, y, 3, "Для скользящей корреляции нужно хотя бы 3 пары значений.");
        StatMath.Require(window >= 3 && window <= x.Count, $"Окно — от 3 до {x.Count} точек.");
        var result = new List<RollingPoint>(x.Count - window + 1);
        for (var end = window - 1; end < x.Count; end++)
        {
            var wx = new double[window];
            var wy = new double[window];
            for (var i = 0; i < window; i++)
            {
                wx[i] = x[end - window + 1 + i];
                wy[i] = y[end - window + 1 + i];
            }

            if (wx.Min() == wx.Max() || wy.Min() == wy.Max())
            {
                result.Add(new RollingPoint(end, window, null));
                continue;
            }

            var r = m switch
            {
                SpearmanMethod => StatMath.Spearman(wx, wy).R,
                KendallMethod => Kendall(wx, wy).R,
                _ => StatMath.Pearson(wx, wy).R,
            };
            result.Add(new RollingPoint(end, window, r));
        }

        return result;
    }

    /// <summary>
    /// Поправка Бенджамини—Хохберга на множественные проверки: q-значения в порядке входа, монотонные, не больше 1 (как statsmodels
    /// multipletests(p, method='fdr_bh')[1]).
    /// </summary>
    public static double[] BenjaminiHochberg(IReadOnlyList<double> p)
    {
        foreach (var v in p)
        {
            StatMath.Require(!double.IsNaN(v) && v >= 0 && v <= 1, "p-значения должны быть от 0 до 1.");
        }

        var n = p.Count;
        var order = Enumerable.Range(0, n).OrderBy(i => p[i]).ThenBy(i => i).ToArray();
        var q = new double[n];
        var running = 1d;
        for (var rank = n; rank >= 1; rank--)
        {
            var i = order[rank - 1];
            running = Math.Min(running, p[i] * n / rank);
            q[i] = Math.Min(1, running);
        }

        return q;
    }

    /// <summary>
    /// Тест нормальности Харке—Бера: JB = n/6·(S² + K²/4), p = exp(−JB/2) (χ² с 2 степенями свободы) — как scipy.stats.jarque_bera; асимметрия
    /// и избыточный эксцесс — смещённые моментные оценки (scipy.stats.skew, kurtosis с bias=True).
    /// </summary>
    public static NormalityResult JarqueBera(IReadOnlyList<double> x)
    {
        StatMath.Require(x.Count >= 3, "Для проверки нормальности нужно хотя бы 3 значения.");
        ComputeGuard.Finite(x);
        var n = x.Count;
        var mean = StatMath.Mean(x);
        double m2 = 0, m3 = 0, m4 = 0;
        foreach (var v in x)
        {
            var d = v - mean;
            var d2 = d * d;
            m2 += d2;
            m3 += d2 * d;
            m4 += d2 * d2;
        }

        m2 /= n;
        m3 /= n;
        m4 /= n;
        // Как scipy: дисперсия на уровне точности double — ряд считается постоянным.
        StatMath.Require(m2 > Math.Pow(1e-15 * mean, 2) && m2 > 0, "Ряд не меняется — асимметрия и эксцесс не определены.");
        var skew = m3 / Math.Pow(m2, 1.5);
        var kurt = m4 / (m2 * m2) - 3;
        var jb = n / 6d * (skew * skew + kurt * kurt / 4);
        return new NormalityResult(n, skew, kurt, jb, Math.Exp(-jb / 2));
    }

    /// <summary>
    /// Модифицированный z (Иглевич—Хоаглин): 0,6745·(x − медиана)/MAD. Если MAD = 0 — (x − медиана)/(1,253314·среднее |x − медиана|); если и
    /// оно 0 — все нули. |z| &gt; 3,5 — принятый порог выброса.
    /// </summary>
    public static double[] ModifiedZ(IReadOnlyList<double> x)
    {
        StatMath.Require(x.Count >= 1, "Нужна хотя бы одна точка.");
        ComputeGuard.Finite(x);
        var median = StatMath.Median(x);
        var deviations = x.Select(v => Math.Abs(v - median)).ToArray();
        var mad = StatMath.Median(deviations);
        if (mad > 0)
        {
            return x.Select(v => 0.6745 * (v - median) / mad).ToArray();
        }

        var meanAbs = deviations.Average();
        return meanAbs > 0 ? x.Select(v => (v - median) / (1.253314 * meanAbs)).ToArray() : new double[x.Count];
    }

    /// <summary>Обычные z-оценки (x − среднее)/s, s — выборочное стандартное отклонение (n − 1); s = 0 — все нули.</summary>
    public static double[] ZScores(IReadOnlyList<double> x)
    {
        StatMath.Require(x.Count >= 1, "Нужна хотя бы одна точка.");
        ComputeGuard.Finite(x);
        var mean = StatMath.Mean(x);
        var sd = StatMath.StdDev(x);
        return sd > 0 ? x.Select(v => (v - mean) / sd).ToArray() : new double[x.Count];
    }

    /// <summary>Границы Тьюки по квартилям типа 7 (numpy percentile по умолчанию): Low = Q1 − k·IQR, High = Q3 + k·IQR.</summary>
    public static IqrFences Iqr(IReadOnlyList<double> x, double k = 1.5)
    {
        StatMath.Require(x.Count >= 1, "Нужна хотя бы одна точка.");
        StatMath.Require(k >= 0 && double.IsFinite(k), "Множитель границ k — неотрицательное число.");
        ComputeGuard.Finite(x);
        var q1 = StatMath.Quantile(x, 0.25);
        var q3 = StatMath.Quantile(x, 0.75);
        var iqr = q3 - q1;
        return new IqrFences(q1, q3, iqr, q1 - k * iqr, q3 + k * iqr);
    }

    private static string NormalizeMethod(string method, bool allowKendall)
    {
        var m = (method ?? "").Trim().ToLowerInvariant();
        var ok = m == PearsonMethod || m == SpearmanMethod || (allowKendall && m == KendallMethod);
        StatMath.Require(ok, allowKendall
            ? $"Метод корреляции: pearson, spearman или kendall, а не «{method}»."
            : $"Метод частной корреляции: pearson или spearman, а не «{method}».");
        return m;
    }

    private static CorrelationCi FisherCi(double r, double se, double level)
    {
        var z = Math.Atanh(r);
        var q = PreciseNormalQuantile(1 - (1 - level) / 2);
        return new CorrelationCi(Math.Tanh(z - q * se), Math.Tanh(z + q * se), level);
    }

    /// <summary>Квантиль нормального: приближение Акклама (StatMath.NormalQuantile, ~1e-9) и один шаг Галлея по точной функции распределения.</summary>
    private static double PreciseNormalQuantile(double p)
    {
        var x = StatMath.NormalQuantile(p);
        var e = StatMath.NormalCdf(x) - p;
        var u = e * Math.Sqrt(2 * Math.PI) * Math.Exp(x * x / 2);
        return x - u / (1 + x * u / 2);
    }

    /// <summary>P(|Z| ≥ |z|) стандартного нормального.</summary>
    private static double TwoSidedNormal(double z) => Math.Clamp(2 * StatMath.NormalCdf(-Math.Abs(z)), 0, 1);

    /// <summary>Суммы по группам одинаковых значений размера t: Σ t(t−1)(t−2) и Σ t(t−1)(2t+5) — поправки дисперсии S Кендалла.</summary>
    private static (double V0, double V1) TieSums(IReadOnlyList<double> x)
    {
        double v0 = 0, v1 = 0;
        foreach (var g in x.GroupBy(v => v))
        {
            double t = g.Count();
            if (t > 1)
            {
                v0 += t * (t - 1) * (t - 2);
                v1 += t * (t - 1) * (2 * t + 5);
            }
        }

        return (v0, v1);
    }

    /// <summary>Остатки МНК-регрессии ряда на контрольные переменные (с константой).</summary>
    private static double[] Residualize(IReadOnlyList<double> v, IReadOnlyList<IReadOnlyList<double>> controls, string label)
    {
        var mean = StatMath.Mean(v);
        var sst = v.Sum(a => (a - mean) * (a - mean));
        StatMath.Require(sst > 0, $"Ряд {label} не меняется — корреляция не определена.");
        var names = Enumerable.Range(1, controls.Count).Select(i => $"z{i}").ToList();
        var fit = RegressionMath.Ols(v, controls, names);
        var resid = new double[v.Count];
        var sse = 0d;
        for (var i = 0; i < v.Count; i++)
        {
            resid[i] = v[i] - fit.Fitted[i];
            sse += resid[i] * resid[i];
        }

        StatMath.Require(sse > 1e-20 * sst, $"Ряд {label} полностью объясняется контрольными переменными — частная корреляция не определена.");
        return resid;
    }
}

/// <summary>Общие проверки входа статистических методов: длины, конечные значения.</summary>
internal static class ComputeGuard
{
    public static void Finite(IReadOnlyList<double> x)
    {
        foreach (var v in x)
        {
            StatMath.Require(double.IsFinite(v), "В данных есть пустые или нечисловые значения (NaN, бесконечность) — уберите их перед расчётом.");
        }
    }

    public static void Pairs(IReadOnlyList<double> x, IReadOnlyList<double> y, int min, string tooShort)
    {
        StatMath.Require(x.Count == y.Count, "Ряды разной длины.");
        StatMath.Require(x.Count >= min, tooShort);
        Finite(x);
        Finite(y);
    }
}
