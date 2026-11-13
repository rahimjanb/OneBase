namespace OneBase.Application.Analytik.Compute;

/// <summary>Коэффициент регрессии: значение, стандартная ошибка, t, p (двусторонний).</summary>
public sealed record RegressionTerm(string Name, double Coefficient, double StdError, double T, double P);

/// <summary>Итог линейной регрессии (МНК): коэффициенты, R², скорректированный R², ошибка остатков, F и его p, наблюдений и параметров.</summary>
public sealed record RegressionResult(int N, int K, IReadOnlyList<RegressionTerm> Terms, double RSquared, double AdjRSquared, double ResidualStdError,
    double F, double FP, IReadOnlyList<double> Fitted);

/// <summary>
/// Линейная регрессия методом наименьших квадратов (нормальные уравнения, обращение Гаусса–Жордана с выбором главного элемента). Коллинеарные
/// признаки и мало наблюдений — понятная ошибка, а не «бесконечные» коэффициенты.
/// </summary>
public static class RegressionMath
{
    public static RegressionResult Ols(IReadOnlyList<double> y, IReadOnlyList<IReadOnlyList<double>> x, IReadOnlyList<string> names, bool intercept = true)
    {
        StatMath.Require(x.Count == names.Count, "Число признаков и их названий не совпадает.");
        foreach (var col in x)
        {
            StatMath.Require(col.Count == y.Count, "Признак и отклик разной длины.");
        }

        var k = x.Count + (intercept ? 1 : 0);
        var n = y.Count;
        StatMath.Require(n > k + 1, $"Мало наблюдений для регрессии: {n} при {k} параметрах (нужно больше {k + 1}).");
        var design = new double[n, k];
        for (var i = 0; i < n; i++)
        {
            var c = 0;
            if (intercept)
            {
                design[i, c++] = 1;
            }

            foreach (var col in x)
            {
                design[i, c++] = col[i];
            }
        }

        var xtx = new double[k, k];
        var xty = new double[k];
        for (var a = 0; a < k; a++)
        {
            for (var i = 0; i < n; i++)
            {
                xty[a] += design[i, a] * y[i];
            }

            for (var b = 0; b < k; b++)
            {
                var s = 0d;
                for (var i = 0; i < n; i++)
                {
                    s += design[i, a] * design[i, b];
                }

                xtx[a, b] = s;
            }
        }

        var inv = Invert(xtx);
        var beta = new double[k];
        for (var a = 0; a < k; a++)
        {
            for (var b = 0; b < k; b++)
            {
                beta[a] += inv[a, b] * xty[b];
            }
        }

        var fitted = new double[n];
        double sse = 0, sst = 0;
        var mean = StatMath.Mean(y);
        for (var i = 0; i < n; i++)
        {
            for (var a = 0; a < k; a++)
            {
                fitted[i] += design[i, a] * beta[a];
            }

            sse += (y[i] - fitted[i]) * (y[i] - fitted[i]);
            sst += (y[i] - mean) * (y[i] - mean);
        }

        var dfResid = n - k;
        var sigma2 = sse / dfResid;
        var terms = new List<RegressionTerm>();
        var allNames = intercept ? ["(константа)", .. names] : names.ToList();
        for (var a = 0; a < k; a++)
        {
            var se = Math.Sqrt(Math.Max(0, inv[a, a] * sigma2));
            var t = se > 0 ? beta[a] / se : 0;
            terms.Add(new RegressionTerm(allNames[a], beta[a], se, t, se > 0 ? StatMath.TwoSidedT(t, dfResid) : 1));
        }

        var r2 = sst > 0 ? 1 - sse / sst : 0;
        var p = k - (intercept ? 1 : 0);
        var adj = sst > 0 && dfResid > 0 ? 1 - (1 - r2) * (n - (intercept ? 1 : 0)) / dfResid : 0;
        var f = p > 0 && sse > 0 ? (sst - sse) / p / (sse / dfResid) : 0;
        return new RegressionResult(n, k, terms, r2, adj, Math.Sqrt(sigma2), f, p > 0 && sse > 0 ? StatMath.FPValue(f, p, dfResid) : 1, fitted);
    }

    /// <summary>Обратная матрица (Гаусс–Жордан). Вырожденная (признаки линейно зависимы) — ComputeException.</summary>
    public static double[,] Invert(double[,] m)
    {
        var n = m.GetLength(0);
        var a = new double[n, 2 * n];
        var scale = 0d;
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                a[i, j] = m[i, j];
                scale = Math.Max(scale, Math.Abs(m[i, j]));
            }

            a[i, n + i] = 1;
        }

        var eps = Math.Max(scale, 1) * 1e-12;
        for (var col = 0; col < n; col++)
        {
            var pivot = col;
            for (var r = col + 1; r < n; r++)
            {
                if (Math.Abs(a[r, col]) > Math.Abs(a[pivot, col]))
                {
                    pivot = r;
                }
            }

            StatMath.Require(Math.Abs(a[pivot, col]) > eps, "Признаки линейно зависимы (коллинеарность) — уберите повторяющийся признак.");
            if (pivot != col)
            {
                for (var j = 0; j < 2 * n; j++)
                {
                    (a[col, j], a[pivot, j]) = (a[pivot, j], a[col, j]);
                }
            }

            var div = a[col, col];
            for (var j = 0; j < 2 * n; j++)
            {
                a[col, j] /= div;
            }

            for (var r = 0; r < n; r++)
            {
                if (r == col || a[r, col] == 0)
                {
                    continue;
                }

                var f = a[r, col];
                for (var j = 0; j < 2 * n; j++)
                {
                    a[r, j] -= f * a[col, j];
                }
            }
        }

        var inv = new double[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                inv[i, j] = a[i, n + j];
            }
        }

        return inv;
    }
}
