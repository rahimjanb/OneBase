namespace OneBase.Application.Analytik.Compute;

/// <summary>
/// Отчёт о связи двух рядов по контракту ТЗ: метод и коэффициент, p и q (после поправки на множественные сравнения), доверительный интервал,
/// размер выборки, пропуски, дубли, выбросы, период, коэффициенты всех трёх методов, связь без общего тренда (первые разности — для рядов
/// по времени), математическая зависимость показателей, совместимость, предупреждения, статус и уровень доказательности.
/// Status: valid — значимо и без оговорок; warning — значимо, но с оговорками; not_significant — связь не подтверждена; unreliable — слишком
/// мало наблюдений для вывода; invalid — посчитать нельзя (нулевая дисперсия, мало точек, несовместимые данные).
/// </summary>
public sealed record CorrelationReport(
    string MetricA,
    string MetricB,
    string Method,
    double? Coefficient,
    double? PValue,
    double? QValue,
    CorrelationCi? ConfidenceInterval,
    int SampleSize,
    int MissingValues,
    int Duplicates,
    int OutliersA,
    int OutliersB,
    string? PeriodFrom,
    string? PeriodTo,
    IReadOnlyDictionary<string, double?> Coefficients,
    double? Detrended,
    double? DetrendedP,
    string? Dependency,
    string? Compatibility,
    IReadOnlyList<string> Warnings,
    string Status,
    string Level)
{
    /// <summary>Коэффициент без общего сезонного цикла (для дневных рядов — без дня недели) и его p.</summary>
    public double? Deseasonalized { get; init; }

    public double? DeseasonalizedP { get; init; }

    /// <summary>Значимо после поправки (q), а без поправки — по p.</summary>
    public bool Significant => (QValue ?? PValue) is < EvidenceClasses.Alpha && Status is "valid" or "warning";
}

/// <summary>
/// Анализ корреляции с проверками данных и предпосылок (Statistics Engine). Пороги выборки — свои у метода: Пирсону и Спирмену нужно хотя бы 5
/// пар для расчёта и 10 для вывода, Кендаллу — так же; меньше 30 — предупреждение. Корреляция никогда не выдаётся за причину: значимая связь —
/// уровень CORRELATION, связь, исчезающая без общего тренда, — HYPOTHESIS, незначимая — UNVERIFIED.
/// </summary>
public static class CorrelationAnalysis
{
    public const string Pearson = "pearson";
    public const string Spearman = "spearman";
    public const string Kendall = "kendall";

    public static readonly IReadOnlyList<string> Methods = [Pearson, Spearman, Kendall];

    /// <summary>Сколько пар нужно, чтобы посчитать коэффициент.</summary>
    public static int MinN(string method) => 5;

    /// <summary>Меньше — коэффициент считается, но вывод о связи ненадёжен.</summary>
    public static int ReliableN(string method) => method == Kendall ? 10 : 10;

    /// <summary>Меньше — предупреждение о малой выборке.</summary>
    public const int ComfortableN = 30;

    /// <summary>Кендалл — O(n²): на длинных рядах не считается.</summary>
    public const int KendallMaxN = 3000;

    public static string NormalizeMethod(string? method) => (method ?? Pearson).Trim().ToLowerInvariant() switch
    {
        Spearman or "спирмен" => Spearman,
        Kendall or "кендалл" => Kendall,
        _ => Pearson,
    };

    /// <summary>Коэффициент выбранного метода (без проверок).</summary>
    public static CorrelationResult Coefficient(string method, IReadOnlyList<double> x, IReadOnlyList<double> y) => method switch
    {
        Spearman => StatMath.Spearman(x, y),
        Kendall => CorrelationMath.Kendall(x, y),
        _ => StatMath.Pearson(x, y),
    };

    /// <summary>
    /// Отчёт по паре рядов. Пропуски (null) удаляются попарно; timeSeries — ряды по шагам времени (проверяется общий тренд и автокорреляция);
    /// duplicates — сколько дублей ключей нашёл загрузчик; compatibility — класс совместимости пары (NOT_COMPATIBLE — отчёт invalid).
    /// </summary>
    public static CorrelationReport Analyze(string metricA, string metricB, IReadOnlyList<double?> a, IReadOnlyList<double?> b, string method, bool timeSeries,
        int duplicates = 0, string? from = null, string? to = null, string? dependency = null, string? compatibility = null, string? compatibilityText = null,
        int seasonPeriod = 0)
    {
        method = NormalizeMethod(method);
        var warnings = new List<string>();
        var n0 = Math.Min(a.Count, b.Count);
        if (a.Count != b.Count)
        {
            warnings.Add($"Ряды разной длины ({a.Count} и {b.Count}): сопоставлены первые {n0} шагов.");
        }

        var x = new List<double>();
        var y = new List<double>();
        var kept = new List<int>();
        var missing = 0;
        for (var i = 0; i < n0; i++)
        {
            if (a[i] is { } va && b[i] is { } vb && double.IsFinite(va) && double.IsFinite(vb))
            {
                x.Add(va);
                y.Add(vb);
                kept.Add(i);
            }
            else
            {
                missing++;
            }
        }

        missing += Math.Abs(a.Count - b.Count);
        if (missing > 0)
        {
            warnings.Add($"Пропущенных значений: {missing} — пары с пропуском исключены.");
        }

        if (duplicates > 0)
        {
            warnings.Add($"Дублей ключей: {duplicates} — оставлено первое значение каждого ключа.");
        }

        CorrelationReport Invalid(string reason) => new(metricA, metricB, method, null, null, null, null, x.Count, missing, duplicates, 0, 0, from, to,
            new Dictionary<string, double?>(), null, null, dependency, compatibility, [.. warnings, reason], "invalid", EvidenceLevels.Unverified);

        if (compatibility == Semantic.CompatibilityClasses.NotCompatible || compatibility == Semantic.CompatibilityClasses.SourceMissing
            || compatibility == Semantic.CompatibilityClasses.AccessDenied)
        {
            return Invalid(compatibilityText ?? "Показатели несовместимы — сравнивать их нельзя.");
        }

        if (x.Count < MinN(method))
        {
            return Invalid($"Пар значений {x.Count} — для метода «{method}» нужно хотя бы {MinN(method)}.");
        }

        if (StatMath.Variance(x) <= 0 || StatMath.Variance(y) <= 0)
        {
            var flat = StatMath.Variance(x) <= 0 ? metricA : metricB;
            return Invalid($"«{flat}» не меняется (нулевая дисперсия) — корреляция не определена.");
        }

        var coefficients = new Dictionary<string, double?>
        {
            [Pearson] = StatMath.Pearson(x, y).R,
            [Spearman] = StatMath.Spearman(x, y).R,
            [Kendall] = x.Count <= KendallMaxN ? CorrelationMath.Kendall(x, y).R : null,
        };
        if (method == Kendall && x.Count > KendallMaxN)
        {
            warnings.Add($"Кендалл на {x.Count} парах не считается (дольше разумного) — взят Спирмен.");
            method = Spearman;
        }

        var main = Coefficient(method, x, y);
        var ci = CorrelationMath.Ci(method, main.R, main.N);

        // Выбросы — модифицированная z-оценка > 3,5 (Иглевич — Хоглин).
        var outA = CorrelationMath.ModifiedZ(x).Count(z => Math.Abs(z) > 3.5);
        var outB = CorrelationMath.ModifiedZ(y).Count(z => Math.Abs(z) > 3.5);
        if (outA + outB > 0)
        {
            warnings.Add($"Выбросы: {outA} у «{metricA}», {outB} у «{metricB}» (модифицированная z > 3,5)" +
                         (method == Pearson ? " — Пирсон к ним чувствителен; сверяйте со Спирменом." : "."));
        }

        // Предпосылки Пирсона: нормальность и линейность.
        if (method == Pearson && x.Count >= 8)
        {
            var nonNormal = new[] { (metricA, x), (metricB, y) }.Where(s => CorrelationMath.JarqueBera(s.Item2).P < EvidenceClasses.Alpha).Select(s => s.Item1).ToList();
            if (nonNormal.Count > 0)
            {
                warnings.Add($"Распределение не похоже на нормальное ({string.Join(", ", nonNormal)}; тест Жарка — Бера) — опирайтесь на ранговые коэффициенты.");
            }
        }

        if (coefficients[Pearson] is { } pr && coefficients[Spearman] is { } sr && Math.Abs(pr - sr) > 0.2)
        {
            warnings.Add($"Пирсон ({pr:0.00}) и Спирмен ({sr:0.00}) расходятся — связь нелинейная или её задают несколько крайних точек.");
        }

        if (x.Count < ComfortableN)
        {
            warnings.Add($"Малая выборка ({x.Count} пар): интервал широкий, вывод осторожный.");
        }

        // Общий тренд: у рядов по времени — связь первых разностей.
        double? detrended = null, detrendedP = null;
        var trendDriven = false;
        if (timeSeries && x.Count >= MinN(method) + 1)
        {
            var dx = Enumerable.Range(1, x.Count - 1).Select(i => x[i] - x[i - 1]).ToList();
            var dy = Enumerable.Range(1, y.Count - 1).Select(i => y[i] - y[i - 1]).ToList();
            if (StatMath.Variance(dx) > 0 && StatMath.Variance(dy) > 0)
            {
                var d = Coefficient(method == Kendall && dx.Count > KendallMaxN ? Spearman : method, dx, dy);
                detrended = d.R;
                detrendedP = d.P;
                var persistent = SeriesMath.Autocorrelation(x, 1) > 0.5 && SeriesMath.Autocorrelation(y, 1) > 0.5;
                if (main.P < EvidenceClasses.Alpha && d.P >= EvidenceClasses.Alpha && persistent)
                {
                    trendDriven = true;
                    warnings.Add("Без общего тренда (по первым разностям) связь не значима — совпадение может объясняться общим ростом или спадом обоих рядов.");
                }
            }
        }

        // Общий сезонный цикл (у дневных рядов — день недели): связь остатков после вычитания среднего своего дня цикла. Оба ряда проседают в
        // воскресенье — это общий календарь, а не связь показателей.
        double? deseasonalized = null, deseasonalizedP = null;
        var seasonDriven = false;
        if (timeSeries && seasonPeriod >= 2 && x.Count >= seasonPeriod * 3)
        {
            List<double> Residuals(List<double> v)
            {
                var means = kept.Select((pos, k) => (pos % seasonPeriod, v[k])).GroupBy(t => t.Item1).ToDictionary(g => g.Key, g => g.Average(t => t.Item2));
                return kept.Select((pos, k) => v[k] - means[pos % seasonPeriod]).ToList();
            }

            var rx = Residuals(x);
            var ry = Residuals(y);
            if (StatMath.Variance(rx) > 1e-12 && StatMath.Variance(ry) > 1e-12)
            {
                var ds = Coefficient(method == Kendall && rx.Count > KendallMaxN ? Spearman : method, rx, ry);
                deseasonalized = ds.R;
                deseasonalizedP = ds.P;
                if (main.P < EvidenceClasses.Alpha && ds.P >= EvidenceClasses.Alpha)
                {
                    seasonDriven = true;
                    warnings.Add($"Связь объясняется общим циклом дня недели: без него r = {ds.R:0.00} (p = {ds.P:0.000}) — оба показателя просто одинаково меняются по дням недели.");
                }
            }
        }

        if (dependency is not null)
        {
            warnings.Add($"Математическая зависимость показателей: {dependency}.");
        }

        if (compatibility == Semantic.CompatibilityClasses.WithLimitations && compatibilityText is { Length: > 0 })
        {
            warnings.Add(compatibilityText);
        }

        var report = new CorrelationReport(metricA, metricB, method, main.R, main.P, null, ci, main.N, missing, duplicates, outA, outB, from, to, coefficients,
            detrended, detrendedP, dependency, compatibility, warnings, "valid", EvidenceLevels.Unverified)
        {
            Deseasonalized = deseasonalized, DeseasonalizedP = deseasonalizedP,
        };
        return Classify(report, main.P, trendDriven || seasonDriven);
    }

    /// <summary>Статус и уровень по p (или q после поправки), размеру выборки и оговоркам.</summary>
    private static CorrelationReport Classify(CorrelationReport r, double p, bool trendDriven)
    {
        if (r.Coefficient is null)
        {
            return r;
        }

        if (r.SampleSize < ReliableN(r.Method))
        {
            return r with { Status = "unreliable", Level = EvidenceLevels.Unverified };
        }

        if (p >= EvidenceClasses.Alpha)
        {
            return r with { Status = "not_significant", Level = EvidenceLevels.Unverified };
        }

        var material = r.Warnings.Count > 0;
        return r with
        {
            Status = material ? "warning" : "valid",
            Level = trendDriven || r.Dependency is not null ? EvidenceLevels.Hypothesis : EvidenceLevels.Correlation,
        };
    }

    /// <summary>
    /// Поправка Бенджамини — Хохберга по всем парам перебора: q-value у каждой пары; статус и уровень пересчитываются по q. Пары, значимые по p,
    /// но не по q, — исследовательские гипотезы (HYPOTHESIS), а не подтверждённые связи.
    /// </summary>
    public static IReadOnlyList<CorrelationReport> Adjust(IReadOnlyList<CorrelationReport> reports)
    {
        var tested = reports.Select((r, i) => (r, i)).Where(x => x.r.PValue is not null && x.r.Status != "invalid").ToList();
        if (tested.Count == 0)
        {
            return reports;
        }

        var q = CorrelationMath.BenjaminiHochberg(tested.Select(x => x.r.PValue!.Value).ToList());
        var result = reports.ToArray();
        for (var k = 0; k < tested.Count; k++)
        {
            var (r, i) = tested[k];
            var adjusted = r with { QValue = q[k] };
            if (r.SampleSize < ReliableN(r.Method))
            {
                result[i] = adjusted with { Status = "unreliable", Level = EvidenceLevels.Unverified };
            }
            else if (q[k] >= EvidenceClasses.Alpha)
            {
                result[i] = adjusted with
                {
                    Status = "not_significant",
                    Level = r.PValue < EvidenceClasses.Alpha ? EvidenceLevels.Hypothesis : EvidenceLevels.Unverified,
                    Warnings = r.PValue < EvidenceClasses.Alpha
                        ? [.. r.Warnings, $"Значимо только без поправки на {tested.Count} сравнений (p = {r.PValue:0.000}, q = {q[k]:0.000}) — исследовательская гипотеза."]
                        : r.Warnings,
                };
            }
            else
            {
                var trendDriven = r.Warnings.Any(w => w.StartsWith("Без общего тренда", StringComparison.Ordinal) || w.StartsWith("Связь объясняется общим циклом", StringComparison.Ordinal));
                result[i] = adjusted with
                {
                    Status = r.Warnings.Count > 0 ? "warning" : "valid",
                    Level = trendDriven || r.Dependency is not null ? EvidenceLevels.Hypothesis : EvidenceLevels.Correlation,
                };
            }
        }

        return result;
    }

    /// <summary>Сила связи словами (по модулю коэффициента).</summary>
    public static string Strength(double r) => Math.Abs(r) switch
    {
        >= 0.8 => "очень сильная",
        >= 0.6 => "сильная",
        >= 0.4 => "умеренная",
        >= 0.2 => "слабая",
        _ => "очень слабая",
    };
}
