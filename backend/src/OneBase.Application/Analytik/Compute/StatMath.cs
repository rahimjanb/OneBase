namespace OneBase.Application.Analytik.Compute;

/// <summary>Расчёт невозможен на этих данных (мало точек, коллинеарность, нет вариации) — сообщение для человека, без служебных подробностей.</summary>
public sealed class ComputeException(string message) : Exception(message);

/// <summary>Описательная статистика выборки.</summary>
public sealed record Descriptive(int N, double Sum, double Mean, double Median, double StdDev, double Min, double Max, double Q1, double Q3, double? Cv,
    double Skewness);

/// <summary>Сравнение двух выборок (t-тест Уэлча): средние, разница и её 95 % интервал, t, степени свободы, p (двусторонний), размер эффекта (d Коэна).</summary>
public sealed record TTestResult(int NA, int NB, double MeanA, double MeanB, double Diff, double? DiffPct, double CiLow, double CiHigh, double T, double Df,
    double P, double CohenD);

/// <summary>Корреляция: коэффициент, число пар, p (двусторонний, H0: связи нет), метод.</summary>
public sealed record CorrelationResult(string Method, int N, double R, double P);

/// <summary>
/// Статистика без внешних библиотек: описательная, квантили, корреляции Пирсона и Спирмена, t-тест Уэлча, распределения (нормальное,
/// Стьюдента, F) через регуляризованную неполную бета-функцию. Всё детерминировано; на одних данных — одни и те же числа.
/// </summary>
public static class StatMath
{
    public static double Mean(IReadOnlyList<double> x)
    {
        Require(x.Count >= 1, "Нужна хотя бы одна точка.");
        var s = 0d;
        foreach (var v in x)
        {
            s += v;
        }

        return s / x.Count;
    }

    /// <summary>Выборочная дисперсия (n − 1).</summary>
    public static double Variance(IReadOnlyList<double> x)
    {
        if (x.Count < 2)
        {
            return 0;
        }

        var m = Mean(x);
        var s = 0d;
        foreach (var v in x)
        {
            s += (v - m) * (v - m);
        }

        return s / (x.Count - 1);
    }

    public static double StdDev(IReadOnlyList<double> x) => Math.Sqrt(Variance(x));

    public static double Median(IReadOnlyList<double> x) => Quantile(x, 0.5);

    /// <summary>Квантиль с линейной интерполяцией (тип 7, как в Excel PERCENTILE.INC и R по умолчанию).</summary>
    public static double Quantile(IReadOnlyList<double> x, double q)
    {
        Require(x.Count >= 1, "Нужна хотя бы одна точка.");
        var sorted = x.OrderBy(v => v).ToArray();
        var h = (sorted.Length - 1) * Math.Clamp(q, 0, 1);
        var lo = (int)Math.Floor(h);
        var hi = Math.Min(lo + 1, sorted.Length - 1);
        return sorted[lo] + (h - lo) * (sorted[hi] - sorted[lo]);
    }

    public static Descriptive Describe(IReadOnlyList<double> x)
    {
        Require(x.Count >= 1, "Нет значений для описательной статистики.");
        var mean = Mean(x);
        var sd = StdDev(x);
        var skew = 0d;
        if (x.Count >= 3 && sd > 0)
        {
            var n = (double)x.Count;
            var m3 = x.Sum(v => Math.Pow((v - mean) / sd, 3));
            skew = n / ((n - 1) * (n - 2)) * m3;
        }

        return new Descriptive(x.Count, x.Sum(), mean, Median(x), sd, x.Min(), x.Max(), Quantile(x, 0.25), Quantile(x, 0.75),
            mean != 0 ? sd / Math.Abs(mean) : null, skew);
    }

    public static CorrelationResult Pearson(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        Require(x.Count == y.Count, "Ряды разной длины.");
        Require(x.Count >= 3, "Для корреляции нужно хотя бы 3 пары значений.");
        var mx = Mean(x);
        var my = Mean(y);
        double sxy = 0, sxx = 0, syy = 0;
        for (var i = 0; i < x.Count; i++)
        {
            sxy += (x[i] - mx) * (y[i] - my);
            sxx += (x[i] - mx) * (x[i] - mx);
            syy += (y[i] - my) * (y[i] - my);
        }

        Require(sxx > 0 && syy > 0, "Один из рядов не меняется — корреляция не определена.");
        var r = Math.Clamp(sxy / Math.Sqrt(sxx * syy), -1, 1);
        return new CorrelationResult("pearson", x.Count, r, CorrelationP(r, x.Count));
    }

    /// <summary>Корреляция Спирмена: Пирсон по рангам (одинаковые значения — средний ранг).</summary>
    public static CorrelationResult Spearman(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        var p = Pearson(Ranks(x), Ranks(y));
        return p with { Method = "spearman" };
    }

    /// <summary>Ранги 1…n, одинаковым значениям — средний ранг.</summary>
    public static double[] Ranks(IReadOnlyList<double> x)
    {
        var order = Enumerable.Range(0, x.Count).OrderBy(i => x[i]).ToArray();
        var ranks = new double[x.Count];
        var k = 0;
        while (k < order.Length)
        {
            var j = k;
            while (j + 1 < order.Length && x[order[j + 1]] == x[order[k]])
            {
                j++;
            }

            var rank = (k + j) / 2d + 1;
            for (var t = k; t <= j; t++)
            {
                ranks[order[t]] = rank;
            }

            k = j + 1;
        }

        return ranks;
    }

    /// <summary>p-значение коэффициента корреляции (двустороннее): t = r·√((n−2)/(1−r²)).</summary>
    public static double CorrelationP(double r, int n)
    {
        if (n < 3)
        {
            return 1;
        }

        if (Math.Abs(r) >= 1)
        {
            return 0;
        }

        var t = r * Math.Sqrt((n - 2) / (1 - r * r));
        return TwoSidedT(t, n - 2);
    }

    /// <summary>t-тест Уэлча (неравные дисперсии): b − a.</summary>
    public static TTestResult Welch(IReadOnlyList<double> a, IReadOnlyList<double> b)
    {
        Require(a.Count >= 2 && b.Count >= 2, "Для сравнения нужно хотя бы по 2 значения в каждой группе.");
        var ma = Mean(a);
        var mb = Mean(b);
        var va = Variance(a) / a.Count;
        var vb = Variance(b) / b.Count;
        var se = Math.Sqrt(va + vb);
        var diff = mb - ma;
        var pooled = Math.Sqrt(((a.Count - 1) * Variance(a) + (b.Count - 1) * Variance(b)) / (a.Count + b.Count - 2));
        var d = pooled > 0 ? diff / pooled : 0;
        if (se == 0)
        {
            // Обе группы без разброса: различие либо точно ноль, либо детерминированное.
            return new TTestResult(a.Count, b.Count, ma, mb, diff, Pct(diff, ma), diff, diff, diff == 0 ? 0 : double.PositiveInfinity * Math.Sign(diff),
                a.Count + b.Count - 2, diff == 0 ? 1 : 0, d);
        }

        var df = (va + vb) * (va + vb) / (va * va / (a.Count - 1) + vb * vb / (b.Count - 1));
        var t = diff / se;
        var q = TQuantile(0.975, df);
        return new TTestResult(a.Count, b.Count, ma, mb, diff, Pct(diff, ma), diff - q * se, diff + q * se, t, df, TwoSidedT(t, df), d);
    }

    private static double? Pct(double diff, double baseValue) => baseValue != 0 ? diff / Math.Abs(baseValue) * 100 : null;

    /// <summary>P(|T| ≥ |t|) для распределения Стьюдента с df степенями свободы.</summary>
    public static double TwoSidedT(double t, double df)
    {
        if (double.IsInfinity(t))
        {
            return 0;
        }

        var x = df / (df + t * t);
        return Math.Clamp(IncompleteBeta(df / 2, 0.5, x), 0, 1);
    }

    /// <summary>Функция распределения Стьюдента.</summary>
    public static double TCdf(double t, double df)
    {
        var tail = TwoSidedT(t, df) / 2;
        return t >= 0 ? 1 - tail : tail;
    }

    /// <summary>Квантиль распределения Стьюдента (бисекция по функции распределения).</summary>
    public static double TQuantile(double p, double df)
    {
        if (p <= 0 || p >= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(p));
        }

        double lo = -1e3, hi = 1e3;
        for (var i = 0; i < 200; i++)
        {
            var mid = (lo + hi) / 2;
            if (TCdf(mid, df) < p)
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }

        return (lo + hi) / 2;
    }

    /// <summary>p-значение F-статистики (правый хвост).</summary>
    public static double FPValue(double f, double d1, double d2)
    {
        if (f <= 0)
        {
            return 1;
        }

        return Math.Clamp(IncompleteBeta(d2 / 2, d1 / 2, d2 / (d2 + d1 * f)), 0, 1);
    }

    /// <summary>Функция стандартного нормального распределения (через erf, точность ~1e-7).</summary>
    public static double NormalCdf(double z) => 0.5 * (1 + Erf(z / Math.Sqrt(2)));

    /// <summary>Квантиль стандартного нормального распределения (алгоритм Акклама, точность ~1e-9).</summary>
    public static double NormalQuantile(double p)
    {
        if (p <= 0 || p >= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(p));
        }

        double[] a = [-3.969683028665376e+01, 2.209460984245205e+02, -2.759285104469687e+02, 1.383577518672690e+02, -3.066479806614716e+01, 2.506628277459239e+00];
        double[] b = [-5.447609879822406e+01, 1.615858368580409e+02, -1.556989798598866e+02, 6.680131188771972e+01, -1.328068155288572e+01];
        double[] c = [-7.784894002430293e-03, -3.223964580411365e-01, -2.400758277161838e+00, -2.549732539343734e+00, 4.374664141464968e+00, 2.938163982698783e+00];
        double[] d = [7.784695709041462e-03, 3.224671290700398e-01, 2.445134137142996e+00, 3.754408661907416e+00];
        const double low = 0.02425;
        if (p < low)
        {
            var q = Math.Sqrt(-2 * Math.Log(p));
            return (((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) / ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1);
        }

        if (p > 1 - low)
        {
            var q = Math.Sqrt(-2 * Math.Log(1 - p));
            return -(((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) / ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1);
        }

        var r = p - 0.5;
        var s = r * r;
        return (((((a[0] * s + a[1]) * s + a[2]) * s + a[3]) * s + a[4]) * s + a[5]) * r / (((((b[0] * s + b[1]) * s + b[2]) * s + b[3]) * s + b[4]) * s + 1);
    }

    /// <summary>Функция ошибок (Abramowitz–Stegun 7.1.26 с уточнением через ряд для малых |x|).</summary>
    public static double Erf(double x)
    {
        var sign = Math.Sign(x);
        x = Math.Abs(x);
        if (x < 0.5)
        {
            // Ряд Тейлора: точнее приближения A–S около нуля.
            double sum = x, term = x;
            for (var n = 1; n < 30; n++)
            {
                term *= -x * x / n;
                sum += term / (2 * n + 1);
            }

            return sign * 2 / Math.Sqrt(Math.PI) * sum;
        }

        // Дополнительная функция через неполную гамму: erfc(x) = Γ(1/2, x²)/√π.
        return sign * (1 - UpperIncompleteGammaRegularized(0.5, x * x));
    }

    /// <summary>Логарифм гамма-функции (Ланцош, g = 7).</summary>
    public static double LogGamma(double x)
    {
        double[] g = [0.99999999999980993, 676.5203681218851, -1259.1392167224028, 771.32342877765313, -176.61502916214059, 12.507343278686905,
            -0.13857109526572012, 9.9843695780195716e-6, 1.5056327351493116e-7];
        if (x < 0.5)
        {
            return Math.Log(Math.PI / Math.Abs(Math.Sin(Math.PI * x))) - LogGamma(1 - x);
        }

        x -= 1;
        var a = g[0];
        var t = x + 7.5;
        for (var i = 1; i < 9; i++)
        {
            a += g[i] / (x + i);
        }

        return 0.5 * Math.Log(2 * Math.PI) + (x + 0.5) * Math.Log(t) - t + Math.Log(a);
    }

    /// <summary>Регуляризованная неполная бета-функция I_x(a, b) (цепная дробь Лентца).</summary>
    public static double IncompleteBeta(double a, double b, double x)
    {
        if (x <= 0)
        {
            return 0;
        }

        if (x >= 1)
        {
            return 1;
        }

        var lbeta = LogGamma(a + b) - LogGamma(a) - LogGamma(b) + a * Math.Log(x) + b * Math.Log(1 - x);
        var front = Math.Exp(lbeta);
        return x < (a + 1) / (a + b + 2)
            ? front * BetaFraction(a, b, x) / a
            : 1 - front * BetaFraction(b, a, 1 - x) / b;
    }

    private static double BetaFraction(double a, double b, double x)
    {
        const double tiny = 1e-300;
        double c = 1, d = 1 - (a + b) * x / (a + 1);
        d = Math.Abs(d) < tiny ? tiny : d;
        d = 1 / d;
        var h = d;
        for (var m = 1; m <= 500; m++)
        {
            var m2 = 2 * m;
            var aa = m * (b - m) * x / ((a + m2 - 1) * (a + m2));
            d = 1 + aa * d;
            d = Math.Abs(d) < tiny ? tiny : d;
            c = 1 + aa / c;
            c = Math.Abs(c) < tiny ? tiny : c;
            d = 1 / d;
            h *= d * c;
            aa = -(a + m) * (a + b + m) * x / ((a + m2) * (a + m2 + 1));
            d = 1 + aa * d;
            d = Math.Abs(d) < tiny ? tiny : d;
            c = 1 + aa / c;
            c = Math.Abs(c) < tiny ? tiny : c;
            d = 1 / d;
            var del = d * c;
            h *= del;
            if (Math.Abs(del - 1) < 1e-15)
            {
                break;
            }
        }

        return h;
    }

    /// <summary>Регуляризованная верхняя неполная гамма-функция Q(a, x).</summary>
    private static double UpperIncompleteGammaRegularized(double a, double x)
    {
        if (x < a + 1)
        {
            // Ряд для P(a, x).
            double sum = 1 / a, term = sum;
            for (var n = 1; n < 500; n++)
            {
                term *= x / (a + n);
                sum += term;
                if (Math.Abs(term) < Math.Abs(sum) * 1e-16)
                {
                    break;
                }
            }

            return 1 - sum * Math.Exp(-x + a * Math.Log(x) - LogGamma(a));
        }

        // Цепная дробь для Q(a, x).
        const double tiny = 1e-300;
        double b = x + 1 - a, c = 1 / tiny, d = 1 / b, h = d;
        for (var i = 1; i < 500; i++)
        {
            var an = -i * (i - a);
            b += 2;
            d = an * d + b;
            d = Math.Abs(d) < tiny ? tiny : d;
            c = b + an / c;
            c = Math.Abs(c) < tiny ? tiny : c;
            d = 1 / d;
            var del = d * c;
            h *= del;
            if (Math.Abs(del - 1) < 1e-16)
            {
                break;
            }
        }

        return Math.Exp(-x + a * Math.Log(x) - LogGamma(a)) * h;
    }

    public static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new ComputeException(message);
        }
    }
}
