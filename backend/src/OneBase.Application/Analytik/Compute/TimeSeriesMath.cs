namespace OneBase.Application.Analytik.Compute;

/// <summary>
/// Экспоненциальное сглаживание: вид (ses — только уровень, holt — уровень и тренд, holt_winters — плюс аддитивная сезонность), подобранные
/// параметры α, β, γ, длина сезона (0 — без сезона), прогноз на шаг вперёд для каждой точки (та же длина, что у ряда; у ses и holt первое
/// значение — y[0]), прогноз на горизонт, сумма квадратов ошибок на шаг вперёд и RMSE.
/// </summary>
public sealed record SmoothingResult(string Kind, double Alpha, double? Beta, double? Gamma, int Season, IReadOnlyList<double> Fitted, IReadOnlyList<double> Forecast,
    double Sse, double Rmse);

/// <summary>
/// Классическое аддитивное разложение ряда: период, тренд (центрированное скользящее среднее; null — края ряда), сезонная составляющая,
/// остаток (null там, где нет тренда), сезонный индекс на каждую позицию периода (сумма 0), сила сезонности max(0, 1 − var(R)/var(S + R)).
/// </summary>
public sealed record Decomposition(int Period, IReadOnlyList<double?> Trend, IReadOnlyList<double> Seasonal, IReadOnlyList<double?> Residual,
    IReadOnlyList<double> SeasonalIndex, double SeasonalStrength);

/// <summary>Скользящие показатели окна, которое заканчивается на индексе End: среднее, выборочное стандартное отклонение, минимум, максимум.</summary>
public sealed record RollingStat(int End, double Mean, double StdDev, double Min, double Max);

/// <summary>
/// Смена уровня ряда: первый индекс нового участка, среднее до и после, сдвиг (после − до), сдвиг в % от уровня до (null — уровень до равен 0),
/// p t-теста Уэлча между соседними участками.
/// </summary>
public sealed record ChangePoint(int Index, double MeanBefore, double MeanAfter, double Shift, double? ShiftPct, double P);

/// <summary>Тест Чоу на излом линейного тренда: индекс начала второго участка, F, p, наклон за шаг до и после излома.</summary>
public sealed record ChowResult(int Break, double F, double P, double SlopeBefore, double SlopeAfter);

/// <summary>
/// Временные ряды без внешних библиотек: экспоненциальное сглаживание (простое, Хольт, Хольт—Винтерс) с подбором параметров по сетке,
/// классическое аддитивное разложение (как statsmodels seasonal_decompose), скользящие показатели (как pandas rolling), точки смены уровня
/// (бинарная сегментация) и тест Чоу на излом тренда. Всё детерминировано.
/// </summary>
public static class TimeSeriesMath
{
    private const string Ses = "ses";
    private const string Holt = "holt";
    private const string HoltWinters = "holt_winters";

    /// <summary>
    /// Аддитивное экспоненциальное сглаживание; α, β, γ — перебором по сетке 0,05…0,95 с шагом 0,05 по наименьшей сумме квадратов ошибок на
    /// шаг вперёд (при равенстве — меньшие параметры). ses: уровень, старт ℓ = y[0], ошибки с t = 1. holt: уровень и тренд, старт ℓ = y[0],
    /// b = y[1] − y[0], ошибки с t = 2 (прогноз на t = 1 совпадает с фактом по построению). holt_winters: аддитивная сезонность длины season,
    /// нужно n ≥ 2·season; старт: ℓ — среднее первого сезона, b = (среднее второго − среднее первого)/season, сезонные — первый сезон минус его
    /// среднее; рекурсия идёт с t = 0, ошибки — с t = season (первый сезон ушёл на старт). Сезонная поправка: s = γ(y − ℓ) + (1 − γ)s.
    /// Прогноз на h шагов: ℓ + h·b (+ s своей позиции сезона).
    /// </summary>
    public static SmoothingResult Smooth(IReadOnlyList<double> y, string kind, int season = 0, int horizon = 0)
    {
        var k = (kind ?? "").Trim().ToLowerInvariant();
        StatMath.Require(k is Ses or Holt or HoltWinters, $"Вид сглаживания: ses, holt или holt_winters, а не «{kind}».");
        StatMath.Require(horizon is >= 0 and <= 10_000, "Горизонт прогноза — от 0 до 10 000 шагов.");
        ComputeGuard.Finite(y);
        var n = y.Count;
        switch (k)
        {
            case Ses:
                StatMath.Require(n >= 3, "Для простого сглаживания нужно хотя бы 3 точки.");
                season = 0;
                break;
            case Holt:
                StatMath.Require(n >= 4, "Для сглаживания Хольта нужно хотя бы 4 точки.");
                season = 0;
                break;
            default:
                StatMath.Require(season >= 2, "Для Хольта—Винтерса нужна длина сезона от 2.");
                StatMath.Require(n >= 2 * season, $"Для Хольта—Винтерса нужно хотя бы два полных сезона: {2 * season} точек, а есть {n}.");
                break;
        }

        var grid = Enumerable.Range(1, 19).Select(i => i / 20d).ToArray();
        double[] betas = k == Ses ? [0] : grid;
        double[] gammas = k == HoltWinters ? grid : [0];
        var best = (Alpha: grid[0], Beta: 0d, Gamma: 0d, Sse: double.MaxValue);
        foreach (var a in grid)
        {
            foreach (var b in betas)
            {
                foreach (var g in gammas)
                {
                    var sse = Run(y, k, a, b, g, season, null, null);
                    if (sse < best.Sse)
                    {
                        best = (a, b, g, sse);
                    }
                }
            }
        }

        var fitted = new double[n];
        var forecast = new double[horizon];
        var finalSse = Run(y, k, best.Alpha, best.Beta, best.Gamma, season, fitted, forecast);
        var errors = n - ErrorsFrom(k, season);
        return new SmoothingResult(k, best.Alpha, k == Ses ? null : best.Beta, k == HoltWinters ? best.Gamma : null, season, fitted, forecast, finalSse,
            Math.Sqrt(finalSse / errors));
    }

    /// <summary>
    /// Классическое аддитивное разложение (как statsmodels seasonal_decompose(y, model='additive', period=p, two_sided=True)): тренд —
    /// центрированное скользящее среднее (для чётного периода — 2×p: крайние точки окна с весом ½), сезонный индекс — среднее ряда без тренда
    /// по каждой позиции периода (позиция = индекс mod p), центрированное к сумме 0; остаток = y − тренд − сезон. Нужно n ≥ 2·period.
    /// </summary>
    public static Decomposition Decompose(IReadOnlyList<double> y, int period)
    {
        StatMath.Require(period >= 2, "Период разложения — от 2.");
        StatMath.Require(y.Count >= 2 * period, $"Для разложения нужно хотя бы два полных периода: {2 * period} точек, а есть {y.Count}.");
        ComputeGuard.Finite(y);
        var n = y.Count;
        var half = period / 2;
        var even = period % 2 == 0;
        var trend = new double?[n];
        for (var t = half; t < n - half; t++)
        {
            var s = 0d;
            if (even)
            {
                s += 0.5 * y[t - half] + 0.5 * y[t + half];
                for (var j = -half + 1; j < half; j++)
                {
                    s += y[t + j];
                }
            }
            else
            {
                for (var j = -half; j <= half; j++)
                {
                    s += y[t + j];
                }
            }

            trend[t] = s / period;
        }

        var index = new double[period];
        for (var pos = 0; pos < period; pos++)
        {
            double sum = 0;
            var count = 0;
            for (var t = pos; t < n; t += period)
            {
                if (trend[t] is { } tr)
                {
                    sum += y[t] - tr;
                    count++;
                }
            }

            index[pos] = sum / count;
        }

        var indexMean = index.Average();
        for (var pos = 0; pos < period; pos++)
        {
            index[pos] -= indexMean;
        }

        var seasonal = new double[n];
        var residual = new double?[n];
        var r = new List<double>();
        var sr = new List<double>();
        for (var t = 0; t < n; t++)
        {
            seasonal[t] = index[t % period];
            if (trend[t] is { } tr)
            {
                var e = y[t] - tr - seasonal[t];
                residual[t] = e;
                r.Add(e);
                sr.Add(seasonal[t] + e);
            }
        }

        var varSr = StatMath.Variance(sr);
        var strength = varSr > 0 ? Math.Max(0, 1 - StatMath.Variance(r) / varSr) : 0;
        return new Decomposition(period, trend, seasonal, residual, index, strength);
    }

    /// <summary>Скользящие среднее, выборочное стандартное отклонение (n − 1), минимум и максимум по окну из window последних точек (как pandas rolling).</summary>
    public static IReadOnlyList<RollingStat> Rolling(IReadOnlyList<double> y, int window)
    {
        StatMath.Require(window >= 2 && window <= y.Count, $"Окно — от 2 до {y.Count} точек.");
        ComputeGuard.Finite(y);
        var result = new List<RollingStat>(y.Count - window + 1);
        for (var end = window - 1; end < y.Count; end++)
        {
            var w = new double[window];
            for (var i = 0; i < window; i++)
            {
                w[i] = y[end - window + 1 + i];
            }

            result.Add(new RollingStat(end, w.Average(), StatMath.StdDev(w), w.Min(), w.Max()));
        }

        return result;
    }

    /// <summary>
    /// Точки смены среднего уровня бинарной сегментацией: из всех участков выбирается разрез с наибольшим уменьшением суммы квадратов
    /// отклонений от средних участков; разрез принимается, если уменьшение больше штрафа 2·σ²·ln n. σ — устойчивая оценка шума по первым
    /// разностям: MAD(разностей)/0,6745/√2 (MAD = 0 — выборочное стандартное отклонение ряда; ряд без вариации — точек нет). С каждой стороны
    /// разреза — не меньше minSegment точек, всего не больше maxPoints точек. Итог — по возрастанию индекса, p — t-тест Уэлча соседних участков.
    /// </summary>
    public static IReadOnlyList<ChangePoint> ChangePoints(IReadOnlyList<double> y, int maxPoints = 5, int minSegment = 5)
    {
        StatMath.Require(maxPoints >= 1, "Число точек смены — от 1.");
        StatMath.Require(minSegment >= 2, "Минимальный участок — от 2 точек.");
        StatMath.Require(y.Count >= 2 * minSegment, $"Ряд слишком короткий для поиска смены уровня: нужно хотя бы {2 * minSegment} точек.");
        ComputeGuard.Finite(y);
        var n = y.Count;
        var diffs = Enumerable.Range(1, n - 1).Select(i => y[i] - y[i - 1]).ToArray();
        var diffMedian = StatMath.Median(diffs);
        var mad = StatMath.Median(diffs.Select(d => Math.Abs(d - diffMedian)).ToArray());
        var sigma = mad > 0 ? mad / 0.6745 / Math.Sqrt(2) : StatMath.StdDev(y);
        if (sigma <= 0)
        {
            return [];
        }

        var penalty = 2 * sigma * sigma * Math.Log(n);
        // Префиксные суммы центрированного ряда: SSE участка за O(1) без потери точности на больших уровнях.
        var mean = StatMath.Mean(y);
        var s1 = new double[n + 1];
        var s2 = new double[n + 1];
        for (var i = 0; i < n; i++)
        {
            var c = y[i] - mean;
            s1[i + 1] = s1[i] + c;
            s2[i + 1] = s2[i] + c * c;
        }

        double Sse(int a, int b) => s2[b] - s2[a] - (s1[b] - s1[a]) * (s1[b] - s1[a]) / (b - a);

        var bounds = new List<int> { 0, n };
        while (bounds.Count - 2 < maxPoints)
        {
            var bestGain = double.NegativeInfinity;
            var bestSplit = -1;
            for (var seg = 0; seg + 1 < bounds.Count; seg++)
            {
                int a = bounds[seg], b = bounds[seg + 1];
                if (b - a < 2 * minSegment)
                {
                    continue;
                }

                var total = Sse(a, b);
                for (var s = a + minSegment; s <= b - minSegment; s++)
                {
                    var gain = total - Sse(a, s) - Sse(s, b);
                    if (gain > bestGain)
                    {
                        bestGain = gain;
                        bestSplit = s;
                    }
                }
            }

            if (bestSplit < 0 || bestGain <= penalty)
            {
                break;
            }

            bounds.Insert(bounds.FindIndex(v => v > bestSplit), bestSplit);
        }

        var result = new List<ChangePoint>(bounds.Count - 2);
        for (var i = 1; i + 1 < bounds.Count; i++)
        {
            var before = Slice(y, bounds[i - 1], bounds[i]);
            var after = Slice(y, bounds[i], bounds[i + 1]);
            var welch = StatMath.Welch(before, after);
            result.Add(new ChangePoint(bounds[i], welch.MeanA, welch.MeanB, welch.Diff, welch.DiffPct, welch.P));
        }

        return result;
    }

    /// <summary>
    /// Тест Чоу на излом линейного тренда y = a + b·t в точке breakAt (первый индекс второго участка): F = ((SSEp − (SSE1 + SSE2))/k) /
    /// ((SSE1 + SSE2)/(n − 2k)), k = 2; p — правый хвост F(k, n − 2k). На каждом участке — не меньше 3 точек.
    /// </summary>
    public static ChowResult Chow(IReadOnlyList<double> y, int breakAt)
    {
        const int k = 2;
        ComputeGuard.Finite(y);
        var n = y.Count;
        StatMath.Require(breakAt >= 3 && n - breakAt >= 3, "Для теста Чоу на каждом участке нужно хотя бы 3 точки.");
        var pooled = LinearFit(y, 0, n);
        var first = LinearFit(y, 0, breakAt);
        var second = LinearFit(y, breakAt, n);
        var mean = StatMath.Mean(y);
        var sst = y.Sum(v => (v - mean) * (v - mean));
        StatMath.Require(sst > 0, "Ряд не меняется — тест Чоу не определён.");
        var sseU = first.Sse + second.Sse;
        StatMath.Require(sseU > 1e-20 * sst, "Оба участка лежат точно на прямых — F-тест Чоу не определён.");
        var f = Math.Max(0, (pooled.Sse - sseU) / k / (sseU / (n - 2 * k)));
        return new ChowResult(breakAt, f, StatMath.FPValue(f, k, n - 2 * k), first.Slope, second.Slope);
    }

    /// <summary>Первая ошибка прогноза на шаг, которая идёт в SSE и RMSE.</summary>
    private static int ErrorsFrom(string kind, int season) => kind switch
    {
        Ses => 1,
        Holt => 2,
        _ => season,
    };

    /// <summary>Один прогон сглаживания: SSE ошибок на шаг вперёд; при fitted/forecast — заполняет прогнозы.</summary>
    private static double Run(IReadOnlyList<double> y, string kind, double alpha, double beta, double gamma, int m, double[]? fitted, double[]? forecast)
    {
        var n = y.Count;
        var errorsFrom = ErrorsFrom(kind, m);
        double level, trend = 0;
        var seasonal = Array.Empty<double>();
        int start;
        if (kind == HoltWinters)
        {
            double first = 0, second = 0;
            for (var i = 0; i < m; i++)
            {
                first += y[i];
                second += y[m + i];
            }

            first /= m;
            second /= m;
            level = first;
            trend = (second - first) / m;
            seasonal = new double[m];
            for (var i = 0; i < m; i++)
            {
                seasonal[i] = y[i] - first;
            }

            start = 0;
        }
        else
        {
            level = y[0];
            trend = kind == Holt ? y[1] - y[0] : 0;
            if (fitted is not null)
            {
                fitted[0] = y[0];
            }

            start = 1;
        }

        var sse = 0d;
        for (var t = start; t < n; t++)
        {
            var s = kind == HoltWinters ? seasonal[t % m] : 0;
            var predicted = level + trend + s;
            if (fitted is not null)
            {
                fitted[t] = predicted;
            }

            if (t >= errorsFrom)
            {
                sse += (y[t] - predicted) * (y[t] - predicted);
            }

            var previous = level;
            level = alpha * (y[t] - s) + (1 - alpha) * (level + trend);
            if (kind != Ses)
            {
                trend = beta * (level - previous) + (1 - beta) * trend;
            }

            if (kind == HoltWinters)
            {
                seasonal[t % m] = gamma * (y[t] - level) + (1 - gamma) * s;
            }
        }

        if (forecast is not null)
        {
            for (var h = 0; h < forecast.Length; h++)
            {
                forecast[h] = level + (h + 1) * trend + (kind == HoltWinters ? seasonal[(n + h) % m] : 0);
            }
        }

        return sse;
    }

    private static double[] Slice(IReadOnlyList<double> y, int from, int to)
    {
        var r = new double[to - from];
        for (var i = from; i < to; i++)
        {
            r[i - from] = y[i];
        }

        return r;
    }

    /// <summary>МНК-прямая y = a + b·t на участке [from, to) с t — индексом ряда: наклон и сумма квадратов остатков.</summary>
    private static (double Slope, double Sse) LinearFit(IReadOnlyList<double> y, int from, int to)
    {
        var len = to - from;
        double tm = 0, ym = 0;
        for (var t = from; t < to; t++)
        {
            tm += t;
            ym += y[t];
        }

        tm /= len;
        ym /= len;
        double sxx = 0, sxy = 0;
        for (var t = from; t < to; t++)
        {
            sxx += (t - tm) * (t - tm);
            sxy += (t - tm) * (y[t] - ym);
        }

        var slope = sxy / sxx;
        var intercept = ym - slope * tm;
        var sse = 0d;
        for (var t = from; t < to; t++)
        {
            var e = y[t] - intercept - slope * t;
            sse += e * e;
        }

        return (slope, sse);
    }
}
