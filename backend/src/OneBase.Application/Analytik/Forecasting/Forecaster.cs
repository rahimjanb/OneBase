namespace OneBase.Application.Analytik.Forecasting;

/// <summary>Точка ряда: дата начала интервала (день, неделя с понедельника, месяц) и значение.</summary>
public sealed record SeriesPoint(DateOnly Date, decimal Value);

/// <summary>Прогноз на дату: оценка и диапазон неопределённости (нижняя и верхняя граница при заданной доверительной вероятности).</summary>
public sealed record ForecastPoint(DateOnly Date, decimal Value, decimal Lower, decimal Upper);

/// <summary>Метод-кандидат и его ошибка на отложенной части ряда (WAPE — сумма |ошибок| ÷ сумма факта).</summary>
public sealed record MethodScore(string Method, decimal? Wape, decimal? Mape);

/// <summary>
/// Край месяца в дневном ряде: Kind — last (последний день месяца) или first (1-е число), Factor — во сколько раз этот день отличается от
/// обычного того же дня недели (медиана по месяцам истории), Months — по скольким месяцам оценено.
/// </summary>
public sealed record CalendarEffect(string Kind, double Factor, int Months);

/// <summary>
/// Итог прогноза: выбранный метод, точки, сценарии (пессимистичный — нижняя граница, реалистичный — оценка, оптимистичный — верхняя),
/// точность на истории (WAPE, MAPE по отложенной части), все проверенные методы, допущения, учтённый край месяца (Calendar). Reliable =
/// false — истории мало, прогноз ориентировочный.
/// </summary>
public sealed record ForecastResult(
    string Granularity, string Method, IReadOnlyList<ForecastPoint> Points, decimal Total, decimal TotalLower, decimal TotalUpper, decimal? Wape, decimal? Mape,
    int HoldoutPoints, IReadOnlyList<MethodScore> Methods, int History, int Seasonality, double Confidence, bool Reliable, IReadOnlyList<string> Assumptions,
    IReadOnlyList<CalendarEffect> Calendar);

/// <summary>
/// Forecasting Engine — математика без модели: наивный, скользящее среднее, линейный тренд, сезонный наивный, Хольт (уровень и тренд),
/// Хольт—Винтерс (аддитивная сезонность). Метод выбирается по ошибке на отложенной части ряда (последние точки), затем модель
/// пересчитывается на всём ряде. Диапазон — по ошибкам на отложенной части (или остаткам), растёт с горизонтом как √шаг. Отрицательный
/// прогноз неотрицательного ряда (продажи, расход) обрезается до нуля. Прогноз — не гарантия: это видно в допущениях и диапазоне.
/// Край месяца по дням (MonthEdges): если последний день месяца или 1-е число устойчиво отличаются от обычного дня (продажи в конце месяца
/// почти вдвое выше), ряд сначала выравнивается на этот множитель, методы выбираются и считают по выровненному ряду, а в прогноз и его
/// диапазон множитель возвращается. Ошибка методов (WAPE) — по исходному ряду.
/// </summary>
public static class Forecaster
{
    public const string Naive = "naive";
    public const string MovingAverage = "moving_average";
    public const string LinearTrend = "linear_trend";
    public const string SeasonalNaive = "seasonal_naive";
    public const string Holt = "holt";
    public const string HoltWinters = "holt_winters";

    /// <summary>Сезонность по шагу ряда: день — неделя (7), неделя — нет, месяц — год (12).</summary>
    public static int SeasonOf(string granularity) => granularity switch
    {
        "day" => 7,
        "month" => 12,
        _ => 0,
    };

    public static DateOnly Next(DateOnly date, string granularity, int steps = 1) => granularity switch
    {
        "day" => date.AddDays(steps),
        "week" => date.AddDays(7 * steps),
        "month" => date.AddMonths(steps),
        _ => throw new ArgumentException($"Шаг ряда: day, week или month, а не «{granularity}»."),
    };

    public static ForecastResult Forecast(IReadOnlyList<SeriesPoint> series, string granularity, int horizon, double confidence = 0.8)
    {
        if (horizon is < 1 or > 366)
        {
            throw new ArgumentException("Горизонт прогноза — от 1 до 366 шагов.");
        }

        var points = series.OrderBy(p => p.Date).ToList();
        if (points.Count == 0)
        {
            throw new ArgumentException("Нет истории — прогноз не строится.");
        }

        var raw = points.Select(p => (double)p.Value).ToArray();
        var nonNegative = raw.All(v => v >= 0);
        // Край месяца: ряд выравнивается (особый день ÷ множитель), прогноз и диапазон — обратно (× множитель).
        var calendar = granularity == "day" ? MonthEdges.Estimate(points) : [];
        double Factor(DateOnly d) => MonthEdges.Factor(calendar, d);
        var y = points.Select(p => (double)p.Value / Factor(p.Date)).ToArray();
        var season = SeasonOf(granularity);
        var usableSeason = season > 1 && y.Length >= 2 * season + 2 ? season : 0;
        var z = Z(confidence);
        var assumptions = new List<string>
        {
            $"История: {y.Length} {Unit(granularity, y.Length)} ({points[0].Date:dd.MM.yyyy} — {points[^1].Date:dd.MM.yyyy}).",
            usableSeason > 0
                ? $"Учтена сезонность: {(granularity == "day" ? "день недели" : "месяц года")}."
                : season > 1 ? $"Сезонность не учтена: для неё нужно не меньше {2 * season + 2} {Unit(granularity, 2 * season + 2)} истории." : "Сезонность для этого шага не моделируется.",
            "Прогноз продолжает закономерности истории: новые акции, цены, ассортимент и форс-мажор в нём не учтены.",
        };

        var candidates = Methods(usableSeason);
        // Отложенная часть: до горизонта, но не больше пятой части ряда и не меньше 3 точек (иначе выбор метода — без проверки).
        var holdout = Math.Min(horizon, Math.Max(3, y.Length / 5));
        var scores = new List<MethodScore>();
        var errors = new Dictionary<string, double[]>();
        if (y.Length - holdout >= 4)
        {
            var train = y[..^holdout];
            var test = y[^holdout..];
            var testRaw = raw[^holdout..];
            var testFactors = points.Skip(points.Count - holdout).Select(p => Factor(p.Date)).ToArray();
            foreach (var method in candidates)
            {
                if (!Applicable(method, train.Length, usableSeason))
                {
                    continue;
                }

                var predicted = Predict(method, train, holdout, usableSeason);
                if (nonNegative)
                {
                    predicted = predicted.Select(v => Math.Max(0, v)).ToArray();
                }

                // Ошибка шага для диапазона — по выровненному ряду; WAPE и MAPE — по исходному (с краем месяца на своих днях).
                var e = test.Select((v, i) => v - predicted[i]).ToArray();
                errors[method] = e;
                var back = predicted.Select((v, i) => v * testFactors[i]).ToArray();
                scores.Add(new MethodScore(method, Wape(testRaw, back), Mape(testRaw, back)));
            }
        }

        string chosen;
        if (scores.Count > 0)
        {
            chosen = scores.OrderBy(s => s.Wape ?? decimal.MaxValue).ThenBy(s => Rank(s.Method)).First().Method;
        }
        else
        {
            chosen = y.Length >= 3 ? MovingAverage : Naive;
            assumptions.Add("Истории мало для проверки методов на отложенной части — взято скользящее среднее (или последнее значение).");
        }

        var forecast = Predict(chosen, y, horizon, usableSeason);
        // Ошибка одного шага: по отложенной части выбранного метода, иначе по остаткам скользящего среднего.
        var sigma = errors.TryGetValue(chosen, out var held) && held.Length >= 2
            ? Std(held)
            : Std(Residuals(y));
        if (double.IsNaN(sigma) || sigma <= 0)
        {
            sigma = y.Length > 1 ? Std(y) * 0.5 : Math.Abs(y[0]) * 0.2;
        }

        var result = new List<ForecastPoint>();
        var date = points[^1].Date;
        var spreadWeights = 0.0;
        for (var i = 0; i < horizon; i++)
        {
            date = Next(date, granularity);
            var factor = Factor(date);
            spreadWeights += (i + 1) * factor * factor;
            var value = forecast[i] * factor;
            var spread = z * sigma * Math.Sqrt(i + 1) * factor;
            var lower = value - spread;
            var upper = value + spread;
            if (nonNegative)
            {
                value = Math.Max(0, value);
                lower = Math.Max(0, lower);
                upper = Math.Max(0, upper);
            }

            result.Add(new ForecastPoint(date, Round(value), Round(lower), Round(upper)));
        }

        var best = scores.FirstOrDefault(s => s.Method == chosen);
        var reliable = y.Length >= 12 && scores.Count > 0 && best?.Wape is { } w && w <= 0.35m;
        if (!reliable)
        {
            assumptions.Add(best?.Wape is { } bw && bw > 0.35m
                ? $"Ошибка на истории высокая (WAPE {bw:P0}) — прогноз ориентировочный."
                : "Истории мало — прогноз ориентировочный, диапазон широкий.");
        }

        foreach (var effect in calendar)
        {
            assumptions.Add(effect.Kind == MonthEdges.Last
                ? $"Учтён конец месяца: последний день — ×{effect.Factor:0.0#} к обычному дню того же дня недели (по {effect.Months} мес.)."
                : $"Учтено начало месяца: 1-е число — ×{effect.Factor:0.0#} к обычному дню того же дня недели (по {effect.Months} мес.).");
        }

        // Сумма за горизонт: границы суммы — √(Σ дисперсий шагов), а не сумма границ (ошибки шагов частично гасят друг друга); у дней края
        // месяца дисперсия — с их множителем.
        var total = result.Sum(p => p.Value);
        var totalSpread = (decimal)(z * sigma * Math.Sqrt(spreadWeights));
        return new ForecastResult(granularity, chosen, result, total, nonNegative ? Math.Max(0, total - totalSpread) : total - totalSpread, total + totalSpread,
            best?.Wape, best?.Mape, scores.Count > 0 ? holdout : 0, scores, y.Length, usableSeason, confidence, reliable, assumptions, calendar);
    }

    private static IReadOnlyList<string> Methods(int season) =>
        season > 0 ? [Naive, MovingAverage, LinearTrend, Holt, SeasonalNaive, HoltWinters] : [Naive, MovingAverage, LinearTrend, Holt];

    private static bool Applicable(string method, int n, int season) => method switch
    {
        SeasonalNaive => season > 0 && n >= season,
        HoltWinters => season > 0 && n >= 2 * season,
        LinearTrend or Holt => n >= 4,
        MovingAverage => n >= 2,
        _ => n >= 1,
    };

    /// <summary>При равной ошибке — более простой метод.</summary>
    private static int Rank(string method) => method switch
    {
        Naive => 0,
        MovingAverage => 1,
        SeasonalNaive => 2,
        LinearTrend => 3,
        Holt => 4,
        _ => 5,
    };

    internal static double[] Predict(string method, double[] y, int h, int season) => method switch
    {
        Naive => Enumerable.Repeat(y[^1], h).ToArray(),
        MovingAverage => Enumerable.Repeat(y[^Math.Min(y.Length, season > 0 ? season : 4)..].Average(), h).ToArray(),
        LinearTrend => Linear(y, h),
        SeasonalNaive => Enumerable.Range(0, h).Select(i => y[y.Length - season + i % season]).ToArray(),
        Holt => HoltLinear(y, h),
        HoltWinters => HoltWintersAdditive(y, h, season),
        _ => throw new ArgumentException(method),
    };

    private static double[] Linear(double[] y, int h)
    {
        var n = y.Length;
        var meanX = (n - 1) / 2.0;
        var meanY = y.Average();
        var sxx = Enumerable.Range(0, n).Sum(i => (i - meanX) * (i - meanX));
        var slope = sxx == 0 ? 0 : Enumerable.Range(0, n).Sum(i => (i - meanX) * (y[i] - meanY)) / sxx;
        var intercept = meanY - slope * meanX;
        return Enumerable.Range(n, h).Select(i => intercept + slope * i).ToArray();
    }

    /// <summary>Хольт: параметры сглаживания — перебором по сетке с наименьшей ошибкой одного шага.</summary>
    private static double[] HoltLinear(double[] y, int h)
    {
        double[] grid = [0.1, 0.3, 0.5, 0.7, 0.9];
        var best = (Alpha: 0.5, Beta: 0.1, Sse: double.MaxValue);
        foreach (var a in grid)
        {
            foreach (var b in new[] { 0.05, 0.1, 0.2, 0.4 })
            {
                var (_, _, sse) = HoltFit(y, a, b);
                if (sse < best.Sse)
                {
                    best = (a, b, sse);
                }
            }
        }

        var (level, trend, _) = HoltFit(y, best.Alpha, best.Beta);
        // Затухание тренда: линия Хольта на длинном горизонте иначе уходит в бесконечность.
        var result = new double[h];
        var damp = 0.0;
        for (var i = 0; i < h; i++)
        {
            damp += Math.Pow(0.9, i + 1);
            result[i] = level + damp * trend;
        }

        return result;
    }

    private static (double Level, double Trend, double Sse) HoltFit(double[] y, double alpha, double beta)
    {
        var level = y[0];
        var trend = y.Length > 1 ? y[1] - y[0] : 0;
        var sse = 0.0;
        for (var t = 1; t < y.Length; t++)
        {
            var forecast = level + trend;
            sse += (y[t] - forecast) * (y[t] - forecast);
            var previous = level;
            level = alpha * y[t] + (1 - alpha) * (level + trend);
            trend = beta * (level - previous) + (1 - beta) * trend;
        }

        return (level, trend, sse);
    }

    /// <summary>Хольт—Винтерс с аддитивной сезонностью: начальные сезонные отклонения — по первым двум сезонам, параметры — по сетке.</summary>
    private static double[] HoltWintersAdditive(double[] y, int h, int m)
    {
        double[] grid = [0.1, 0.3, 0.5];
        var best = (A: 0.3, B: 0.05, G: 0.3, Sse: double.MaxValue);
        foreach (var a in grid)
        {
            foreach (var b in new[] { 0.01, 0.05, 0.2 })
            {
                foreach (var g in new[] { 0.1, 0.3, 0.5 })
                {
                    var sse = HwFit(y, m, a, b, g, null);
                    if (sse < best.Sse)
                    {
                        best = (a, b, g, sse);
                    }
                }
            }
        }

        var forecast = new double[h];
        HwFit(y, m, best.A, best.B, best.G, forecast);
        return forecast;
    }

    private static double HwFit(double[] y, int m, double alpha, double beta, double gamma, double[]? forecast)
    {
        var first = y[..m].Average();
        var second = y[m..(2 * m)].Average();
        var level = first;
        var trend = (second - first) / m;
        var seasonal = Enumerable.Range(0, m).Select(i => (y[i] - first + y[m + i] - second) / 2).ToArray();
        var sse = 0.0;
        for (var t = 0; t < y.Length; t++)
        {
            var s = seasonal[t % m];
            var predicted = level + trend + s;
            if (t >= m)
            {
                sse += (y[t] - predicted) * (y[t] - predicted);
            }

            var previous = level;
            level = alpha * (y[t] - s) + (1 - alpha) * (level + trend);
            trend = beta * (level - previous) + (1 - beta) * trend;
            seasonal[t % m] = gamma * (y[t] - level) + (1 - gamma) * s;
        }

        if (forecast is not null)
        {
            for (var i = 0; i < forecast.Length; i++)
            {
                forecast[i] = level + (i + 1) * trend + seasonal[(y.Length + i) % m];
            }
        }

        return sse;
    }

    /// <summary>Остатки скользящего среднего (окно 3) — оценка разброса, когда отложенной части нет.</summary>
    private static double[] Residuals(double[] y) =>
        y.Length < 4 ? y.Select(v => v - y.Average()).ToArray() : Enumerable.Range(3, y.Length - 3).Select(i => y[i] - (y[i - 1] + y[i - 2] + y[i - 3]) / 3).ToArray();

    private static double Std(double[] e) => e.Length < 2 ? double.NaN : Math.Sqrt(e.Sum(v => v * v) / (e.Length - 1));

    private static decimal? Wape(double[] actual, double[] predicted)
    {
        var sum = actual.Sum(Math.Abs);
        return sum == 0 ? null : Math.Round((decimal)(actual.Select((v, i) => Math.Abs(v - predicted[i])).Sum() / sum), 4);
    }

    private static decimal? Mape(double[] actual, double[] predicted)
    {
        var pairs = actual.Select((v, i) => (v, p: predicted[i])).Where(x => x.v != 0).ToList();
        return pairs.Count == 0 ? null : Math.Round((decimal)pairs.Average(x => Math.Abs((x.v - x.p) / x.v)), 4);
    }

    /// <summary>Квантиль нормального распределения для двусторонней вероятности.</summary>
    public static double Z(double confidence) => confidence switch
    {
        >= 0.99 => 2.576,
        >= 0.95 => 1.96,
        >= 0.9 => 1.645,
        >= 0.8 => 1.282,
        _ => 1.0,
    };

    private static decimal Round(double value) => double.IsFinite(value) ? Math.Round((decimal)value, 2) : 0;

    private static string Unit(string granularity, int n) => granularity switch
    {
        "day" => n % 10 == 1 && n % 100 != 11 ? "день" : n % 10 is >= 2 and <= 4 && n % 100 is < 12 or > 14 ? "дня" : "дней",
        "week" => n % 10 == 1 && n % 100 != 11 ? "неделя" : n % 10 is >= 2 and <= 4 && n % 100 is < 12 or > 14 ? "недели" : "недель",
        _ => n % 10 == 1 && n % 100 != 11 ? "месяц" : n % 10 is >= 2 and <= 4 && n % 100 is < 12 or > 14 ? "месяца" : "месяцев",
    };

    /// <summary>Сценарий «что если»: значения прогноза, умноженные на (1 + изменение) — например, продажи +20 %.</summary>
    public static IReadOnlyList<ForecastPoint> Scenario(IReadOnlyList<ForecastPoint> points, decimal change) =>
        points.Select(p => new ForecastPoint(p.Date, Math.Round(p.Value * (1 + change), 2), Math.Round(p.Lower * (1 + change), 2), Math.Round(p.Upper * (1 + change), 2)))
            .ToList();
}
