namespace OneBase.Application.Analytik.Compute;

/// <summary>Тренд ряда: наклон за шаг, наклон в % от среднего, p наклона, R², число точек.</summary>
public sealed record TrendResult(int N, double Slope, double? SlopePctOfMean, double P, double RSquared, double Intercept);

/// <summary>Сезонный индекс: позиция (день недели 1–7 или месяц 1–12), среднее, индекс к общему среднему (1 — как в среднем), точек.</summary>
public sealed record SeasonIndex(int Position, double Mean, double Index, int N);

/// <summary>Корреляция рядов со сдвигом: x опережает y на Lag шагов.</summary>
public sealed record LagPoint(int Lag, int N, double R, double P);

/// <summary>Анализ запаздывания: все сдвиги, лучший (наибольший |r|), его p с поправкой Бонферрони на число проверенных сдвигов.</summary>
public sealed record LagResult(IReadOnlyList<LagPoint> Lags, LagPoint? Best, double? BestAdjustedP);

/// <summary>Анализ временных рядов: тренд, сезонность (день недели, месяц), автокорреляция, корреляция со сдвигом, скользящее среднее.</summary>
public static class SeriesMath
{
    public static TrendResult Trend(IReadOnlyList<double> values)
    {
        StatMath.Require(values.Count >= 4, "Для тренда нужно хотя бы 4 точки.");
        var t = Enumerable.Range(0, values.Count).Select(i => (double)i).ToArray();
        var reg = RegressionMath.Ols(values, [t], ["шаг"]);
        var mean = StatMath.Mean(values);
        var slope = reg.Terms[1];
        return new TrendResult(values.Count, slope.Coefficient, mean != 0 ? slope.Coefficient / Math.Abs(mean) * 100 : null, slope.P, reg.RSquared,
            reg.Terms[0].Coefficient);
    }

    /// <summary>Сезонность дня недели для дневного ряда (понедельник = 1). Нужно хотя бы 2 полные недели.</summary>
    public static IReadOnlyList<SeasonIndex> Weekday(IReadOnlyList<(DateOnly Date, double Value)> daily)
    {
        StatMath.Require(daily.Count >= 14, "Для сезонности дня недели нужно хотя бы 14 дней.");
        var mean = StatMath.Mean(daily.Select(p => p.Value).ToList());
        return daily.GroupBy(p => p.Date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)p.Date.DayOfWeek)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var m = g.Average(p => p.Value);
                return new SeasonIndex(g.Key, m, mean != 0 ? m / mean : 0, g.Count());
            })
            .ToList();
    }

    /// <summary>Сезонность месяца года для месячного ряда. Нужно хотя бы 24 месяца — иначе сезон не отличить от тренда.</summary>
    public static IReadOnlyList<SeasonIndex> Monthly(IReadOnlyList<(DateOnly Date, double Value)> monthly)
    {
        StatMath.Require(monthly.Count >= 24, "Для сезонности месяца нужно хотя бы 24 месяца истории.");
        var mean = StatMath.Mean(monthly.Select(p => p.Value).ToList());
        return monthly.GroupBy(p => p.Date.Month).OrderBy(g => g.Key)
            .Select(g =>
            {
                var m = g.Average(p => p.Value);
                return new SeasonIndex(g.Key, m, mean != 0 ? m / mean : 0, g.Count());
            })
            .ToList();
    }

    public static double Autocorrelation(IReadOnlyList<double> x, int lag)
    {
        StatMath.Require(lag >= 1 && x.Count > lag + 2, "Ряд слишком короткий для автокорреляции с таким сдвигом.");
        var mean = StatMath.Mean(x);
        double num = 0, den = 0;
        for (var i = 0; i < x.Count; i++)
        {
            den += (x[i] - mean) * (x[i] - mean);
            if (i + lag < x.Count)
            {
                num += (x[i] - mean) * (x[i + lag] - mean);
            }
        }

        return den > 0 ? num / den : 0;
    }

    /// <summary>
    /// Корреляция x(t) и y(t + lag) для lag = 0…maxLag: x опережает y. Лучший сдвиг — наибольший |r|; его p умножается на число проверенных
    /// сдвигов (Бонферрони) — иначе среди многих сдвигов «значимый» найдётся случайно.
    /// </summary>
    public static LagResult CrossCorrelation(IReadOnlyList<double> x, IReadOnlyList<double> y, int maxLag)
    {
        StatMath.Require(x.Count == y.Count, "Ряды разной длины.");
        StatMath.Require(maxLag >= 0 && x.Count - maxLag >= 5, "Ряды слишком короткие для такого сдвига: нужно хотя бы 5 пар на каждом сдвиге.");
        var points = new List<LagPoint>();
        for (var lag = 0; lag <= maxLag; lag++)
        {
            var xs = x.Take(x.Count - lag).ToList();
            var ys = y.Skip(lag).ToList();
            try
            {
                var c = StatMath.Pearson(xs, ys);
                points.Add(new LagPoint(lag, xs.Count, c.R, c.P));
            }
            catch (ComputeException)
            {
                // Ряд без вариации на этом сдвиге — сдвиг пропускается.
            }
        }

        var best = points.OrderByDescending(p => Math.Abs(p.R)).ThenBy(p => p.Lag).FirstOrDefault();
        return new LagResult(points, best, best is null ? null : Math.Min(1, best.P * points.Count));
    }

    public static IReadOnlyList<double?> MovingAverage(IReadOnlyList<double> x, int window)
    {
        var result = new double?[x.Count];
        var sum = 0d;
        for (var i = 0; i < x.Count; i++)
        {
            sum += x[i];
            if (i >= window)
            {
                sum -= x[i - window];
            }

            result[i] = i >= window - 1 ? sum / window : null;
        }

        return result;
    }
}
