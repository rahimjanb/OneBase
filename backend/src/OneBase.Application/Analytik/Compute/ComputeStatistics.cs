using System.Globalization;
using OneBase.Application.Analytik.Semantic;

namespace OneBase.Application.Analytik.Compute;

/// <summary>
/// Statistics Engine поверх вычислительного движка: корреляции по контракту ТЗ (с проверками данных, поправкой на множественные сравнения,
/// «частью и целым»), частная и скользящая корреляция, регрессии (МНК с диагностикой, полиномиальная, Ridge, Lasso), сглаживание, декомпозиция,
/// точки перелома, скользящая статистика и обзор аномалий. Данные — срез движка, сохранённый срез (query_id) или структурированные ряды;
/// модель числа в формулах не передаёт.
/// </summary>
public sealed partial class ComputeService
{
    /// <summary>
    /// Выровненные данные метода: ключи шагов (дата или участник измерения), даты, колонки показателей (null — пропуск), сколько шагов выпало
    /// при выравнивании рядов, сколько дублей ключей, по чему строки (шаг или измерение), ряд ли это по времени.
    /// </summary>
    private sealed record Matrix(IReadOnlyList<string> Keys, IReadOnlyList<DateOnly?> Dates, IReadOnlyList<string> Metrics, IReadOnlyList<double?[]> Columns,
        int Dropped, int Duplicates, string By, bool Time, string? Grain)
    {
        public double?[] Column(string metric) => Columns[Metrics.ToList().IndexOf(metric)];

        public string? From => Dates.FirstOrDefault(d => d is not null)?.ToString("yyyy-MM-dd");

        public string? To => Dates.LastOrDefault(d => d is not null)?.ToString("yyyy-MM-dd");
    }

    /// <summary>Шаг по умолчанию: дни — для периода до полугода, иначе недели.</summary>
    private static string DefaultGrain(ComputeData d) => d.Grain ?? (d.To!.Value.DayNumber - d.From!.Value.DayNumber <= 186 ? "day" : "week");

    /// <summary>Сезонный цикл ряда для проверки связи: дневной ряд — неделя (7), иначе — без проверки.</summary>
    private static int SeasonOf(Matrix m) => m.Time && m.Grain == "day" ? 7 : 0;

    /// <summary>Аддитивный показатель (сумма, счёт): отсутствие строки — ноль; отношение (средний чек, страйк) — пропуск.</summary>
    private static bool AdditiveMetric(string metric) => SemanticCatalog.Metric(metric) is not { } m || MetricPassports.Profile(m).Additive;

    /// <summary>
    /// Оговорка сравнения участников: у кого аддитивного показателя нет вовсе (ни одной записи источника) — названия и что это не ноль.
    /// null — пропусков нет, пропуск у всех (тогда расчёт сам скажет, что данных нет) или показатель-отношение (пусто и при нулевом знаменателе).
    /// </summary>
    public static string? MissingMembersNote(string metric, IReadOnlyList<string> members, IReadOnlyList<double?> values)
    {
        var missing = members.Where((_, i) => values[i] is null).ToList();
        return !AdditiveMetric(metric) || missing.Count == 0 || missing.Count == members.Count
            ? null
            : $"«{Label(metric)}»: нет данных у {missing.Count} из {members.Count} ({string.Join(", ", missing.Take(3))}" +
              $"{(missing.Count > 3 ? $" и ещё {missing.Count - 3}" : "")}) — в расчёт по этому показателю не вошли: у них нет ни одной записи, это не ноль.";
    }

    private static async Task<Matrix> MatrixAsync(ComputeData d, Loader l, IReadOnlyList<string> metrics)
    {
        if (l.Inline is { Count: > 0 } inline)
        {
            return InlineMatrix(inline, metrics);
        }

        if (d.Dimension is { } dim && dim != "date")
        {
            var rows = await l.RowsAsync(metrics, [dim], d.From!.Value, d.To!.Value);
            // Сравнение участников: пусто — у участника нет ни одной строки этого источника (продажи есть, визитов нет: сети, где визиты ТП
            // не ведутся). Это «нет данных», а не ноль: участник в расчёт по этому показателю не идёт, и это сказано. Внутри источника суммы
            // и счётчики пустыми не бывают. Ноль вместо пропуска менял вывод: два региона без визитов давали связи значимость, которой у
            // регионов с визитами нет.
            var cols = metrics.Select((_, i) => rows.Select(r => r.Values[i]).ToArray()).ToList();
            for (var i = 0; i < metrics.Count; i++)
            {
                if (MissingMembersNote(metrics[i], rows.Select(r => r.Label).ToList(), cols[i]) is { } note)
                {
                    l.Notes.Add(note);
                }
            }

            return new Matrix(rows.Select(r => r.Label).ToList(), rows.Select(_ => (DateOnly?)null).ToList(), metrics, cols, 0, 0,
                SemanticCatalog.Dimension(dim)?.Title ?? dim, false, null);
        }

        var grain = DefaultGrain(d);
        var all = await l.RowsAsync(metrics, ["date"], d.From!.Value, d.To!.Value, grain, fillZeros: true);
        var through = l.DataThrough is { } dt && dt < d.To!.Value ? dt : d.To!.Value;
        if (through < d.To!.Value)
        {
            l.Notes.Add($"Данные источника — по {through:dd.MM.yyyy}: шаги позже не учтены (это отставание загрузки, а не нули).");
        }

        var points = all.Select(r => (Date: DateOnly.ParseExact(r.Keys[0], "yyyy-MM-dd", CultureInfo.InvariantCulture), r.Values))
            .OrderBy(p => p.Date).ToList();
        var kept = points.Where(p => Loader.End(p.Date, grain) <= through).ToList();
        if (kept.Count < points.Count(p => p.Date <= through))
        {
            l.Notes.Add("Последний неполный шаг времени не учтён.");
        }

        var columns = metrics.Select((m, i) => kept.Select(p => p.Values[i] ?? (AdditiveMetric(m) ? 0 : (double?)null)).ToArray()).ToList();
        return new Matrix(kept.Select(p => p.Date.ToString("yyyy-MM-dd")).ToList(), kept.Select(p => (DateOnly?)p.Date).ToList(), metrics, columns,
            points.Count - kept.Count, 0, grain, true, grain);
    }

    /// <summary>Структурированные ряды: по датам (пересечение дат всех рядов; дубли дат — первое значение) или по номеру шага.</summary>
    private static Matrix InlineMatrix(IReadOnlyDictionary<string, InlineSeries> inline, IReadOnlyList<string> metrics)
    {
        var series = metrics.Select(m => inline.TryGetValue(m, out var s) ? s : throw new ComputeException($"Ряда «{m}» нет среди переданных рядов.")).ToList();
        if (series.All(s => s.Dates is { Count: > 0 }))
        {
            var duplicates = 0;
            var maps = new List<Dictionary<DateOnly, double?>>();
            foreach (var s in series)
            {
                StatMath.Require(s.Dates!.Count == s.Values.Count, $"Ряд «{s.Name}»: дат {s.Dates.Count}, значений {s.Values.Count}.");
                var map = new Dictionary<DateOnly, double?>();
                for (var i = 0; i < s.Values.Count; i++)
                {
                    if (!map.TryAdd(s.Dates[i], s.Values[i]))
                    {
                        duplicates++;
                    }
                }

                maps.Add(map);
            }

            var union = maps.SelectMany(m => m.Keys).Distinct().Count();
            var dates = maps.Select(m => (IEnumerable<DateOnly>)m.Keys).Aggregate((a, b) => a.Intersect(b)).OrderBy(x => x).ToList();
            var cols = maps.Select(m => dates.Select(dt => m[dt]).ToArray()).ToList();
            return new Matrix(dates.Select(x => x.ToString("yyyy-MM-dd")).ToList(), dates.Select(x => (DateOnly?)x).ToList(), metrics, cols, union - dates.Count,
                duplicates, "переданные ряды", true, null);
        }

        var n = series.Min(s => s.Values.Count);
        var dropped = series.Max(s => s.Values.Count) - n;
        return new Matrix(Enumerable.Range(1, n).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToList(), Enumerable.Repeat((DateOnly?)null, n).ToList(),
            metrics, series.Select(s => s.Values.Take(n).ToArray()).ToList(), dropped, 0, "переданные ряды", true, null);
    }

    /// <summary>
    /// Ряд показателя для методов рядов: шаги со значением и их позиции. У аддитивного показателя пропуск — ноль (продаж не было); у отношения
    /// (средний чек, страйк, выполнение маршрута) пропуск — «не определено» (знаменатель ноль): такой шаг не участвует в расчёте и не может
    /// оказаться «аномалией 0 %».
    /// </summary>
    private static (List<double> Values, List<int> Positions) Observed(Matrix m, string metric)
    {
        var column = m.Column(metric);
        var additive = AdditiveMetric(metric);
        var positions = Enumerable.Range(0, column.Length).Where(i => column[i] is not null || additive).ToList();
        return (positions.Select(i => column[i] ?? 0).ToList(), positions);
    }

    /// <summary>Методам, которым нужен непрерывный ряд (сезон, сглаживание), — отказ, если у отношения есть шаги без значения.</summary>
    private static List<double> Continuous(Matrix m, string metric)
    {
        var (values, positions) = Observed(m, metric);
        StatMath.Require(positions.Count == m.Keys.Count,
            $"«{Label(metric)}» — отношение и не определено в {m.Keys.Count - positions.Count} шагах (нет знаменателя): возьмите аддитивный показатель или шаг крупнее.");
        return values;
    }

    /// <summary>Подпись показателя или переданного ряда.</summary>
    private static string Label(string metric) => SemanticCatalog.Metric(metric)?.Title ?? metric;

    /// <summary>Код участника для ряда «все, кроме него» (<c>revenue:без:Кекс</c> → <c>revenue:Кекс</c>).</summary>
    private static string OwnMember(string restCode)
    {
        var at = restCode.IndexOf(":без:", StringComparison.Ordinal);
        return restCode[..at] + ":" + restCode[(at + ":без:".Length)..];
    }

    /// <summary>Совместимость пары для отчёта (только показатели движка; у переданных рядов — не проверяется).</summary>
    private static (string? Class, string? Text, string? Dependency) PairCheck(string a, string b, ComputeData d, Matrix m)
    {
        if (SemanticCatalog.Metric(a) is null || SemanticCatalog.Metric(b) is null)
        {
            return (null, null, null);
        }

        var dims = d.Dimension is { } dim && dim != "date" ? new[] { dim } : m.Time ? new[] { "date" } : [];
        var c = DataCompatibility.Check(a, b, dims, m.Grain);
        var worst = c.Aspects.Where(x => x.Class == c.Class && x.Code is not "units" and not "source").Select(x => x.Text).FirstOrDefault();
        return (c.Class, worst, c.Dependency);
    }

    private static object ReportView(CorrelationReport r) => new
    {
        metric_a = r.MetricA, metric_b = r.MetricB, r.Method, r.Coefficient, p_value = r.PValue, q_value = r.QValue,
        confidence_interval = r.ConfidenceInterval is { } ci ? new { lower = ci.Lower, upper = ci.Upper, level = ci.Level } : null,
        sample_size = r.SampleSize, missing_values = r.MissingValues, duplicates = r.Duplicates, outliers = new { a = r.OutliersA, b = r.OutliersB },
        period = new { from = r.PeriodFrom, to = r.PeriodTo }, coefficients = r.Coefficients, detrended = r.Detrended, detrended_p = r.DetrendedP,
        deseasonalized = r.Deseasonalized, deseasonalized_p = r.DeseasonalizedP,
        r.Dependency, r.Compatibility, r.Warnings, r.Status, level = r.Level,
    };

    private static string Phrase(CorrelationReport r) =>
        r.Coefficient is not { } c
            ? $"«{Label(r.MetricA)}» × «{Label(r.MetricB)}»: не посчитано — {r.Warnings.LastOrDefault()}"
            : $"«{Label(r.MetricA)}» × «{Label(r.MetricB)}»: {r.Method} {N(c, 2)} ({CorrelationAnalysis.Strength(c)}, " +
              $"{(r.QValue is { } q ? $"q = {P(q)}" : $"p = {P(r.PValue ?? 1)}")}, n = {r.SampleSize}) — {StatusText(r.Status)}";

    private static string StatusText(string status) => status switch
    {
        "valid" => "связь подтверждена",
        "warning" => "связь значима, но с оговорками",
        "not_significant" => "связь не подтверждена",
        "unreliable" => "мало данных для вывода",
        _ => "не посчитано",
    };

    private static string OldClass(IEnumerable<CorrelationReport> reports) =>
        reports.Any(r => r.Significant && r.Level == EvidenceLevels.Correlation) ? EvidenceClasses.Statistical : EvidenceClasses.Observed;

    private static async Task<MethodResult> CorrelationReportAsync(ComputeData d, Params p, Loader l)
    {
        var m = await MatrixAsync(d, l, d.Metrics);
        var method = CorrelationAnalysis.NormalizeMethod(p.Text("method"));
        var reports = new List<CorrelationReport>();
        var a = d.Metrics[0];
        for (var i = 1; i < d.Metrics.Count; i++)
        {
            var b = d.Metrics[i];
            var (cls, text, dep) = PairCheck(a, b, d, m);
            reports.Add(CorrelationAnalysis.Analyze(a, b, m.Column(a), m.Column(b), method, m.Time, m.Duplicates, m.From, m.To, dep, cls, text, SeasonOf(m)));
        }

        IReadOnlyList<CorrelationReport> final = reports.Count > 1 ? CorrelationAnalysis.Adjust(reports) : reports;
        // Прежний вид результата (pairs: pearson, spearman) — его читают проверка гипотез и набор сложных вопросов.
        var pairs = final.Where(r => r.Coefficient is not null).Select(r => new
        {
            metric = r.MetricB,
            pearson = new CorrelationResult(CorrelationAnalysis.Pearson, r.SampleSize, r.Coefficients[CorrelationAnalysis.Pearson] ?? 0,
                StatMath.CorrelationP(r.Coefficients[CorrelationAnalysis.Pearson] ?? 0, r.SampleSize)),
            spearman = new CorrelationResult(CorrelationAnalysis.Spearman, r.SampleSize, r.Coefficients[CorrelationAnalysis.Spearman] ?? 0,
                StatMath.CorrelationP(r.Coefficients[CorrelationAnalysis.Spearman] ?? 0, r.SampleSize)),
            evidenceClass = r.Significant && r.Level == EvidenceLevels.Correlation ? EvidenceClasses.Statistical : EvidenceClasses.Observed,
        }).ToList();
        if (m.Dropped > 0)
        {
            l.Notes.Add($"При выравнивании рядов выпало шагов: {m.Dropped}.");
        }

        return new MethodResult(new { metric = a, by = m.By, method, pairs, reports = final.Select(ReportView).ToList() },
            $"Корреляция ({method}, по «{m.By}»): {string.Join("; ", final.Select(Phrase))}. Корреляция — не причина.", OldClass(final),
            ["Корреляция показывает совместное изменение, но не причину; общий фактор может влиять на оба показателя."]);
    }

    private static async Task<MethodResult> PartialCorrelationAsync(ComputeData d, Params p, Loader l)
    {
        var m = await MatrixAsync(d, l, d.Metrics);
        var method = CorrelationAnalysis.NormalizeMethod(p.Text("method")) == CorrelationAnalysis.Spearman ? CorrelationAnalysis.Spearman : CorrelationAnalysis.Pearson;
        var rows = Enumerable.Range(0, m.Keys.Count).Where(i => m.Columns.All(c => c[i] is not null)).ToList();
        StatMath.Require(rows.Count >= d.Metrics.Count + 4, $"Для частной корреляции с {d.Metrics.Count - 2} контрольными нужно хотя бы {d.Metrics.Count + 4} полных шагов.");
        IReadOnlyList<double> Col(int i) => rows.Select(r => m.Columns[i][r]!.Value).ToList();
        var controls = Enumerable.Range(2, d.Metrics.Count - 2).Select(Col).ToList();
        var partial = CorrelationMath.Partial(Col(0), Col(1), controls, method);
        var plain = method == CorrelationAnalysis.Spearman ? StatMath.Spearman(Col(0), Col(1)) : StatMath.Pearson(Col(0), Col(1));
        var level = partial.P < EvidenceClasses.Alpha && partial.N >= 10 ? EvidenceLevels.Correlation : EvidenceLevels.Unverified;
        var names = string.Join(", ", d.Metrics.Skip(2).Select(x => $"«{Label(x)}»"));
        return new MethodResult(new { x = d.Metrics[0], y = d.Metrics[1], controls = d.Metrics.Skip(2), by = m.By, partial, plain, level },
            $"Частная корреляция «{Label(d.Metrics[0])}» × «{Label(d.Metrics[1])}» при учёте {names}: r = {N(partial.R, 2)} (p = {P(partial.P)}, n = {partial.N}); " +
            $"без учёта — r = {N(plain.R, 2)}. " + (Math.Abs(plain.R) - Math.Abs(partial.R) > 0.2
                ? "Связь заметно слабеет при учёте контрольных — часть её объясняют они."
                : "Контрольные показатели связь почти не меняют."),
            partial.P < EvidenceClasses.Alpha && partial.N >= 10 ? EvidenceClasses.Statistical : EvidenceClasses.Observed,
            ["Частная корреляция убирает только линейное влияние перечисленных показателей; неучтённые факторы остаются."]);
    }

    private static async Task<MethodResult> RollingCorrelationAsync(ComputeData d, Params p, Loader l)
    {
        var m = await MatrixAsync(d, l, d.Metrics);
        StatMath.Require(m.Time, "Скользящая корреляция — по шагам времени, без измерения.");
        var rows = Enumerable.Range(0, m.Keys.Count).Where(i => m.Columns[0][i] is not null && m.Columns[1][i] is not null).ToList();
        var window = p.Int("window", m.Grain == "day" ? 28 : 12, 5, 365);
        StatMath.Require(rows.Count >= window + 2, $"Для окна {window} нужно больше {window + 1} шагов (есть {rows.Count}).");
        var method = CorrelationAnalysis.NormalizeMethod(p.Text("method"));
        var points = CorrelationMath.Rolling(rows.Select(r => m.Columns[0][r]!.Value).ToList(), rows.Select(r => m.Columns[1][r]!.Value).ToList(), window, method);
        var valued = points.Where(x => x.R is not null).ToList();
        StatMath.Require(valued.Count > 0, "Во всех окнах один из рядов не меняется — корреляция не определена.");
        var signChanges = valued.Zip(valued.Skip(1)).Count(x => Math.Sign(x.First.R!.Value) != Math.Sign(x.Second.R!.Value));
        var min = valued.MinBy(x => x.R)!;
        var max = valued.MaxBy(x => x.R)!;
        var series = points.Select(x => new { date = m.Keys[rows[x.End]], r = x.R, n = x.N }).ToList();
        var stable = max.R - min.R < 0.4 && signChanges == 0;
        return new MethodResult(new { x = d.Metrics[0], y = d.Metrics[1], method, window, grain = m.Grain, series = series.TakeLast(StoredItems), min = min.R, max = max.R, signChanges },
            $"Скользящая корреляция ({method}, окно {window} шагов): от {N(min.R!.Value, 2)} до {N(max.R!.Value, 2)}, смен знака {signChanges} — " +
            (stable ? "связь устойчива во времени." : "связь неустойчива: в разные периоды она разная."),
            EvidenceClasses.Observed, ["Каждое окно — малая выборка: отдельные значения шумные, смотрите на общую картину."]);
    }

    /// <summary>
    /// Матрица корреляций (поиск закономерностей): все пары показателей и, с измерением members, участники измерения как отдельные ряды по
    /// времени (каждый — против итога и против «всех остальных»: часть и целое). Поправка Бенджамини — Хохберга по всем парам; подтверждённые
    /// связи (q &lt; 0,05) — отдельно от исследовательских гипотез (p &lt; 0,05, но q ≥ 0,05).
    /// </summary>
    private static async Task<MethodResult> CorrelationMatrixAsync(ComputeData d, Params p, Loader l)
    {
        var method = CorrelationAnalysis.NormalizeMethod(p.Text("method"));
        var series = new List<(string Code, string Title, double?[] Values, string? PartOf)>();
        Matrix? m = null;
        if (p.Text("members") is { Length: > 0 } membersDim)
        {
            // Участники измерения (например, товарные группы) как ряды показателя по времени.
            var dim = SemanticCatalog.Dimension(membersDim)?.Code ?? throw new ComputeException($"Измерения «{membersDim}» нет.");
            var metric = d.Metrics.Count > 0 ? d.Metrics[0] : "revenue";
            StatMath.Require(AdditiveMetric(metric), "Участников сравнивают по аддитивному показателю (выручка, кг, заказы, визиты): иначе «остальные» не посчитать.");
            var grain = DefaultGrain(d);
            m = await MatrixAsync(d with { Dimension = null }, l, [metric]);
            var rows = await l.RowsAsync([metric], ["date", dim], d.From!.Value, d.To!.Value, grain);
            var top = p.Int("top", 10, 2, 25);
            var members = rows.GroupBy(r => r.Labels[1]).Select(g => (Name: g.Key, Total: g.Sum(r => r.Values[0] ?? 0)))
                .OrderByDescending(x => x.Total).Take(top).Select(x => x.Name).ToList();
            var index = m.Keys.Select((k, i) => (k, i)).ToDictionary(x => x.k, x => x.i);
            var total = m.Column(metric);
            series.Add((metric, $"{Label(metric)} — итог", total, null));
            foreach (var member in members)
            {
                var values = new double?[m.Keys.Count];
                for (var i = 0; i < values.Length; i++)
                {
                    values[i] = 0;
                }

                foreach (var r in rows.Where(r => r.Labels[1] == member && index.ContainsKey(r.Keys[0])))
                {
                    values[index[r.Keys[0]]] = r.Values[0] ?? 0;
                }

                series.Add(($"{metric}:{member}", member, values, metric));
                var rest = values.Select((v, i) => total[i] - v).ToArray();
                series.Add(($"{metric}:без:{member}", $"Все, кроме «{member}»", rest, null));
            }
        }
        else
        {
            StatMath.Require(d.Metrics.Count >= 2, "Матрице нужно хотя бы 2 показателя (или members — участники измерения).");
            m = await MatrixAsync(d, l, d.Metrics);
            series.AddRange(d.Metrics.Select(x => (x, Label(x), m.Column(x), (string?)null)));
        }

        var reports = new List<(CorrelationReport Report, string TitleA, string TitleB, string Kind)>();
        for (var i = 0; i < series.Count; i++)
        {
            for (var j = i + 1; j < series.Count; j++)
            {
                var (ca, ta, va, partA) = series[i];
                var (cb, tb, vb, partB) = series[j];
                var restA = ca.Contains(":без:", StringComparison.Ordinal);
                var restB = cb.Contains(":без:", StringComparison.Ordinal);
                // «Все, кроме A» сравнивается только с самим A. Любой другой участник B входит во «всех, кроме A» — связь B с ними завышена
                // (часть и целое); «все, кроме A» против «всех, кроме B» или против итога — почти одно и то же. Такие пары не считаются.
                if ((restA || restB) && !(restA && cb == OwnMember(ca) || restB && ca == OwnMember(cb)))
                {
                    continue;
                }

                var kind = partA == cb || partB == ca ? "part_whole" : restA || restB ? "part_rest" : "pair";
                string? dependency = kind == "part_whole" ? $"«{(partA == cb ? ta : tb)}» — часть итога: корреляция с итогом завышена, смотрите сравнение с «остальными»" : null;
                string? cls = null, text = null;
                if (!ca.Contains(':') && !cb.Contains(':'))
                {
                    (cls, text, var dep) = PairCheck(ca, cb, d, m);
                    dependency ??= dep;
                }

                var r = CorrelationAnalysis.Analyze(ca, cb, va, vb, method, m.Time, m.Duplicates, m.From, m.To, dependency, cls, text, SeasonOf(m));
                reports.Add((r, ta, tb, kind));
            }
        }

        StatMath.Require(reports.Count > 0, "Нет пар для сравнения.");
        var adjusted = CorrelationAnalysis.Adjust(reports.Select(x => x.Report).ToList());
        var all = adjusted.Select((r, i) => (Report: r, reports[i].TitleA, reports[i].TitleB, reports[i].Kind)).ToList();
        var confirmed = all.Where(x => x.Report.Significant && x.Report.Level == EvidenceLevels.Correlation).OrderByDescending(x => Math.Abs(x.Report.Coefficient!.Value)).ToList();
        var flagged = all.Where(x => x.Report.Significant && x.Report.Level != EvidenceLevels.Correlation).OrderByDescending(x => Math.Abs(x.Report.Coefficient!.Value)).ToList();
        var exploratory = all.Where(x => x.Report.Status == "not_significant" && x.Report.Level == EvidenceLevels.Hypothesis).ToList();
        object Pair((CorrelationReport Report, string TitleA, string TitleB, string Kind) x) => new
        {
            a = x.TitleA, b = x.TitleB, kind = x.Kind, r = x.Report.Coefficient, p = x.Report.PValue, q = x.Report.QValue, n = x.Report.SampleSize,
            ci = x.Report.ConfidenceInterval, detrended = x.Report.Detrended, deseasonalized = x.Report.Deseasonalized, status = x.Report.Status, level = x.Report.Level,
            warnings = x.Report.Warnings,
        };
        var titles = series.Select(s => s.Title).ToList();
        var grid = titles.Select(ta => titles.Select(tb => ta == tb ? 1.0 : all.FirstOrDefault(x => x.TitleA == ta && x.TitleB == tb || x.TitleA == tb && x.TitleB == ta).Report?.Coefficient).ToList()).ToList();
        var qgrid = titles.Select(ta => titles.Select(tb => ta == tb ? (double?)null : all.FirstOrDefault(x => x.TitleA == ta && x.TitleB == tb || x.TitleA == tb && x.TitleB == ta).Report?.QValue).ToList()).ToList();
        var top3 = confirmed.Take(3).Select(x => $"«{x.TitleA}» × «{x.TitleB}» {N(x.Report.Coefficient!.Value, 2)} (q = {P(x.Report.QValue ?? 1)})");
        return new MethodResult(new
        {
            method, by = m!.By, grain = m.Grain, tested = all.Count, confirmedCount = confirmed.Count, flaggedCount = flagged.Count, exploratoryCount = exploratory.Count,
            confirmed = confirmed.Take(StoredItems).Select(Pair).ToList(), flagged = flagged.Take(StoredItems).Select(Pair).ToList(),
            exploratory = exploratory.Take(StoredItems).Select(Pair).ToList(), matrix = new { titles, r = grid, q = qgrid, n = m.Keys.Count },
        },
            $"Проверено пар: {all.Count} ({method}, поправка Бенджамини — Хохберга). Подтверждённых связей: {confirmed.Count}" +
            (confirmed.Count > 0 ? $" — сильнейшие: {string.Join("; ", top3)}" : "") +
            $". Значимых, но с оговорками (часть и целое, общий тренд): {flagged.Count}; исследовательских гипотез (значимы только без поправки): {exploratory.Count}.",
            confirmed.Count > 0 ? EvidenceClasses.Statistical : EvidenceClasses.Observed,
            ["Корреляция — совместное изменение, а не причина.", "При переборе многих пар часть «значимых» связей случайна — подтверждёнными считаются только пары с q < 0,05."]);
    }

    private static async Task<MethodResult> RegressionModelAsync(ComputeData d, Params p, Loader l)
    {
        var m = await MatrixAsync(d, l, d.Metrics);
        var rows = Enumerable.Range(0, m.Keys.Count).Where(i => m.Columns.All(c => c[i] is not null)).ToList();
        IReadOnlyList<double> Col(int i) => rows.Select(r => m.Columns[i][r]!.Value).ToList();
        var y = Col(0);
        var names = d.Metrics.Skip(1).ToList();
        var xs = Enumerable.Range(1, d.Metrics.Count - 1).Select(Col).ToList();
        var model = (p.Text("model") ?? "linear").Trim().ToLowerInvariant();
        var missing = m.Keys.Count - rows.Count;
        var limits = new List<string> { "Регрессия описывает связь в этих данных, а не доказанное влияние; причинный вывод — только с контрольной группой (did)." };
        if (missing > 0)
        {
            limits.Add($"Шагов с пропуском исключено: {missing}.");
        }

        switch (model)
        {
            case "ridge":
            case "lasso":
            {
                var pr = model == "ridge" ? RegressionModels.Ridge(y, xs, names, p.Number("lambda")) : RegressionModels.Lasso(y, xs, names, p.Number("lambda"));
                var terms = string.Join("; ", pr.Coefficients.Select(c => $"{Label(c.Name)}: {N(c.Coefficient, 3)}"));
                return new MethodResult(new { metric = d.Metrics[0], by = m.By, model, penalized = pr },
                    $"{(model == "ridge" ? "Ridge" : "Lasso")}-регрессия «{Label(d.Metrics[0])}» (λ = {N(pr.Lambda, 4)}, выбор {pr.LambdaChoice}): {terms}; R² = {N(pr.RSquared, 2)}" +
                    (pr.CvRmse is { } cv ? $", ошибка на проверке {N(cv)}" : "") +
                    (model == "lasso" ? $"; отобраны: {(pr.Selected.Count == 0 ? "ни один признак" : string.Join(", ", pr.Selected.Select(Label)))}." : "."),
                    EvidenceClasses.Observed, [.. limits, "Коэффициенты со штрафом смещены к нулю: p-значений у них нет, это отбор и устойчивость, а не проверка значимости."]);
            }
            case "polynomial":
            {
                StatMath.Require(xs.Count == 1, "Полиномиальная регрессия — один признак (второй показатель).");
                var degree = p.Int("degree", 2, 1, 3);
                var dr = RegressionModels.Polynomial(y, xs[0], degree, names[0]);
                var linear = RegressionModels.Polynomial(y, xs[0], 1, names[0]);
                return Detailed(d, m, dr, $"Полиномиальная регрессия степени {degree}", limits,
                    $" Линейная модель: R² = {N(linear.Ols.RSquared, 2)}, скорр. R² = {N(linear.Ols.AdjRSquared, 2)}; " +
                    (dr.Ols.AdjRSquared - linear.Ols.AdjRSquared > 0.02 ? "кривая заметно лучше прямой." : "кривая почти не лучше прямой — берите линейную."));
            }
            default:
            {
                var dr = RegressionModels.Ols(y, xs, names);
                return Detailed(d, m, dr, xs.Count == 1 ? "Простая линейная регрессия" : "Множественная линейная регрессия", limits, "");
            }
        }
    }

    private static MethodResult Detailed(ComputeData d, Matrix m, DetailedRegression dr, string title, List<string> limits, string extra)
    {
        var reg = dr.Ols;
        var cls = Strength(reg.FP, reg.N);
        var terms = string.Join("; ", dr.Coefficients.Skip(1).Select(t =>
            $"{Label(t.Name)}: {N(t.Coefficient, 3)} [{N(t.Lower, 3)}; {N(t.Upper, 3)}] (p = {P(t.P)})"));
        limits.AddRange(dr.Diagnostics.Warnings);
        return new MethodResult(new
        {
            metric = d.Metrics[0], by = m.By, model = title, regression = reg with { Fitted = [] }, coefficients = dr.Coefficients, diagnostics = dr.Diagnostics,
            residuals = dr.Residuals.Take(StoredItems),
        },
            $"{title} «{Label(d.Metrics[0])}»: {terms}; R² = {N(reg.RSquared, 2)}, скорр. R² = {N(reg.AdjRSquared, 2)}, p(F) = {P(reg.FP)}, n = {reg.N}" +
            (dr.Diagnostics.Warnings.Count > 0 ? $". Диагностика: {string.Join("; ", dr.Diagnostics.Warnings)}" : ". Диагностика остатков без замечаний") + "." + extra,
            cls, limits);
    }

    private static async Task<MethodResult> SmoothingAsync(ComputeData d, Params p, Loader l)
    {
        var m = await MatrixAsync(d, l, [d.Metrics[0]]);
        var values = Continuous(m, d.Metrics[0]);
        var kind = (p.Text("kind") ?? "auto").Trim().ToLowerInvariant();
        var season = p.Int("season", m.Grain switch { "day" => 7, "month" => 12, "quarter" => 4, _ => 0 }, 0, 52);
        var horizon = p.Int("horizon", m.Grain == "day" ? 7 : 4, 0, 120);
        var candidates = kind == "auto" ? new[] { "ses", "holt", "holt_winters" } : [kind];
        var fits = candidates.Where(k => k != "holt_winters" || season >= 2 && values.Count >= 2 * season)
            .Select(k => TimeSeriesMath.Smooth(values, k, k == "holt_winters" ? season : 0, horizon)).ToList();
        StatMath.Require(fits.Count > 0, "Для сезонного сглаживания нужно хотя бы два полных сезона.");
        var best = fits.MinBy(f => f.Rmse)!;
        return new MethodResult(new
        {
            metric = d.Metrics[0], grain = m.Grain, chosen = best.Kind, compared = fits.Select(f => new { f.Kind, f.Rmse, f.Alpha, f.Beta, f.Gamma }),
            fitted = best.Fitted.Select((v, i) => new { date = m.Keys[i], value = values[i], fitted = v }).TakeLast(StoredItems), forecast = best.Forecast, level = EvidenceLevels.Forecast,
        },
            $"Сглаживание «{Label(d.Metrics[0])}»: лучшая модель — {best.Kind} (α = {N(best.Alpha, 2)}" + (best.Beta is { } b ? $", β = {N(b, 2)}" : "") +
            (best.Gamma is { } g ? $", γ = {N(g, 2)}" : "") + $"), ошибка на шаг {N(best.Rmse)}" +
            (best.Forecast.Count > 0 ? $"; продолжение на {best.Forecast.Count} шаг(ов): {string.Join(", ", best.Forecast.Take(7).Select(v => N(v, 0)))}." : "."),
            EvidenceClasses.Assumption, ["Продолжение ряда — прогноз, а не факт; не смешивайте его с фактическими значениями."]);
    }

    private static int PeriodOf(string? grain, Params p) => p.Int("period", grain switch { "day" => 7, "month" => 12, "quarter" => 4, "week" => 52, _ => 7 }, 2, 60);

    private static async Task<MethodResult> DecompositionAsync(ComputeData d, Params p, Loader l)
    {
        var m = await MatrixAsync(d, l, [d.Metrics[0]]);
        var values = Continuous(m, d.Metrics[0]);
        var period = PeriodOf(m.Grain, p);
        StatMath.Require(values.Count >= 2 * period, $"Для декомпозиции с периодом {period} нужно хотя бы {2 * period} шагов (есть {values.Count}).");
        var dec = TimeSeriesMath.Decompose(values, period);
        var residuals = dec.Residual.Select((r, i) => (r, i)).Where(x => x.r is not null).ToList();
        var z = residuals.Count > 0 ? CorrelationMath.ModifiedZ(residuals.Select(x => x.r!.Value).ToList()) : [];
        var unusual = residuals.Select((x, k) => (x.i, z[k])).Where(x => Math.Abs(x.Item2) > 3.5)
            .Select(x => new { date = m.Keys[x.i], value = values[x.i], expected = (dec.Trend[x.i] ?? values[x.i]) + dec.Seasonal[x.i], z = x.Item2 }).ToList();
        var peak = dec.SeasonalIndex.Select((v, i) => (v, i)).MaxBy(x => x.v);
        var low = dec.SeasonalIndex.Select((v, i) => (v, i)).MinBy(x => x.v);
        string Pos(int i) => m.Grain == "day" && m.Dates.FirstOrDefault() is { } first
            ? Ru.DateTimeFormat.GetAbbreviatedDayName(first.AddDays(i).DayOfWeek)
            : $"позиция {i + 1}";
        var trendNotNull = dec.Trend.Where(t => t is not null).Select(t => t!.Value).ToList();
        return new MethodResult(new
        {
            metric = d.Metrics[0], grain = m.Grain, period, seasonalStrength = dec.SeasonalStrength, seasonalIndex = dec.SeasonalIndex,
            series = values.Select((v, i) => new { date = m.Keys[i], value = v, trend = dec.Trend[i], seasonal = dec.Seasonal[i], residual = dec.Residual[i] }).TakeLast(StoredItems),
            unusual,
        },
            $"Декомпозиция «{Label(d.Metrics[0])}» (период {period}): сила сезонности {N(dec.SeasonalStrength * 100, 0)} %; сезонный пик — {Pos(peak.i)} (+{N(peak.v)}), " +
            $"минимум — {Pos(low.i)} ({N(low.v)}); тренд {(trendNotNull.Count > 1 ? $"от {N(trendNotNull[0])} до {N(trendNotNull[^1])}" : "не определён")}; " +
            $"необычных остатков: {unusual.Count}.",
            dec.SeasonalStrength >= 0.4 ? EvidenceClasses.Statistical : EvidenceClasses.Observed,
            ["Классическая декомпозиция: тренд по краям ряда не определён (половина периода с каждой стороны)."]);
    }

    private static async Task<MethodResult> ChangePointsAsync(ComputeData d, Params p, Loader l)
    {
        var m = await MatrixAsync(d, l, [d.Metrics[0]]);
        StatMath.Require(m.Time, "Точки перелома — по шагам времени.");
        var (values, positions) = Observed(m, d.Metrics[0]);
        var keys = positions.Select(i => m.Keys[i]).ToList();
        var points = TimeSeriesMath.ChangePoints(values, p.Int("max_points", 5, 1, 10), p.Int("min_segment", m.Grain == "day" ? 7 : 4, 3, 60));
        ChowResult? chow = null;
        if (points.Count > 0)
        {
            var strongest = points.MaxBy(x => Math.Abs(x.Shift))!;
            if (strongest.Index >= 3 && values.Count - strongest.Index >= 3)
            {
                chow = TimeSeriesMath.Chow(values, strongest.Index);
            }
        }

        var view = points.Select(x => new { date = keys[x.Index], x.MeanBefore, x.MeanAfter, x.Shift, x.ShiftPct, x.P }).ToList();
        var significant = points.Where(x => x.P < EvidenceClasses.Alpha).ToList();
        return new MethodResult(new { metric = d.Metrics[0], grain = m.Grain, points = view, chow },
            points.Count == 0
                ? $"«{Label(d.Metrics[0])}»: устойчивых сдвигов уровня не найдено."
                : $"«{Label(d.Metrics[0])}»: сдвигов уровня {points.Count} — " + string.Join("; ", points.Select(x =>
                    $"с {keys[x.Index]} {N(x.MeanBefore, 0)} → {N(x.MeanAfter, 0)} ({(x.ShiftPct is { } sp ? $"{(sp >= 0 ? "+" : "")}{N(sp)} %" : N(x.Shift, 0))}, p = {P(x.P)})")) +
                  (chow is not null ? $". Тест Чоу (перелом тренда): F = {N(chow.F, 2)}, p = {P(chow.P)}." : "."),
            significant.Count > 0 ? EvidenceClasses.Statistical : EvidenceClasses.Observed,
            ["Перелом — дата, после которой уровень другой; причину перелома метод не объясняет."]);
    }

    private static async Task<MethodResult> RollingStatsAsync(ComputeData d, Params p, Loader l)
    {
        var m = await MatrixAsync(d, l, [d.Metrics[0]]);
        var (values, positions) = Observed(m, d.Metrics[0]);
        var window = p.Int("window", m.Grain == "day" ? 7 : 4, 2, 365);
        StatMath.Require(values.Count >= window, $"Шагов {values.Count} — меньше окна {window}.");
        var stats = TimeSeriesMath.Rolling(values, window);
        var last = stats[^1];
        var first = stats[0];
        return new MethodResult(new { metric = d.Metrics[0], grain = m.Grain, window, series = stats.Select(s => new { date = m.Keys[positions[s.End]], s.Mean, s.StdDev, s.Min, s.Max }).TakeLast(StoredItems) },
            $"Скользящая статистика «{Label(d.Metrics[0])}» (окно {window}): среднее {N(first.Mean)} → {N(last.Mean)}, разброс σ {N(first.StdDev)} → {N(last.StdDev)}.",
            EvidenceClasses.Observed, []);
    }

    /// <summary>
    /// Устойчивое разложение для поиска аномалий: сезонный профиль — медиана значений каждого места цикла (минус общая медиана), тренд —
    /// скользящая медиана ряда без сезона (окно 2·period + 1, у краёв — укороченное). Один выброс не сдвигает ни сезон, ни тренд соседей.
    /// Возвращает ожидаемое значение (тренд + сезон) и остаток каждого шага.
    /// </summary>
    private static (double[] Fit, double[] Residual) RobustResiduals(IReadOnlyList<double> values, int period)
    {
        var n = values.Count;
        var season = new double[n];
        if (period >= 2)
        {
            var overall = StatMath.Median(values);
            var profile = Enumerable.Range(0, period)
                .Select(k => StatMath.Median(Enumerable.Range(0, n).Where(i => i % period == k).Select(i => values[i]).ToList()) - overall).ToArray();
            for (var i = 0; i < n; i++)
            {
                season[i] = profile[i % period];
            }
        }

        var adjusted = Enumerable.Range(0, n).Select(i => values[i] - season[i]).ToArray();
        var half = period >= 2 ? period : 3;
        var fit = new double[n];
        var residual = new double[n];
        for (var i = 0; i < n; i++)
        {
            var window = adjusted.Skip(Math.Max(0, i - half)).Take(Math.Min(n, i + half + 1) - Math.Max(0, i - half)).ToList();
            var trend = StatMath.Median(window);
            fit[i] = trend + season[i];
            residual[i] = values[i] - fit[i];
        }

        return (fit, residual);
    }

    /// <summary>Найденная аномалия: показатель, шаг, значение, ожидаемое, отклонение, направление, какие методы её подтвердили, серьёзность.</summary>
    private sealed record AnomalyFinding(string Metric, string Title, string Date, double Value, double Expected, double Deviation, double? DeviationPct,
        string Direction, IReadOnlyList<string> Methods, string Severity);

    /// <summary>
    /// Обзор аномалий по нескольким показателям: z-оценка, модифицированная z, IQR, сезонный остаток (с учётом дня недели — падение в выходной
    /// не аномалия), резкий рост или падение к обычному уровню того же дня недели, пропавшие данные (ноль там, где обычно есть значения),
    /// сдвиг уровня. Аномалия — когда её подтверждают метод с учётом сезонности и хотя бы один простой метод (или пропали данные).
    /// </summary>
    private static async Task<MethodResult> AnomalyScanAsync(ComputeData d, Params p, Loader l)
    {
        var m = await MatrixAsync(d, l, d.Metrics);
        StatMath.Require(m.Time, "Обзор аномалий — по шагам времени.");
        var checkLast = p.Int("check_last", m.Keys.Count, 1, m.Keys.Count);
        var period = PeriodOf(m.Grain, p);
        var sharpShare = (p.Number("sharp_pct") ?? 50) / 100;
        var found = new List<AnomalyFinding>();
        var lines = new List<string>();
        var structural = new List<object>();
        foreach (var metric in d.Metrics)
        {
            // Шаги без значения у отношений (нет знаменателя) не проверяются; сезонный метод — только на непрерывном ряду.
            var (values, positions) = Observed(m, metric);
            var gaps = positions.Count < m.Keys.Count;
            if (values.Count < 8 || StatMath.Variance(values) <= 0)
            {
                continue;
            }

            // Устойчивая схема: сезон — медианы дня цикла, тренд — скользящая медиана; все методы — по остаткам, а не по сырым значениям:
            // на ряде с недельной сезонностью сырые z и IQR помечали бы каждое воскресенье, а среднее заражалось бы выбросом соседнего дня.
            var seasonal = !gaps && period >= 2 && values.Count >= 2 * period;
            var (fit, residual) = RobustResiduals(values, seasonal ? period : 0);
            var z = CorrelationMath.ZScores(residual);
            var mz = CorrelationMath.ModifiedZ(residual);
            var iqr = CorrelationMath.Iqr(residual, 3);
            double?[] seasonalZ = new double?[values.Count];
            double?[] expected = new double?[values.Count];
            for (var k = 0; k < values.Count; k++)
            {
                seasonalZ[k] = mz[k];
                expected[k] = fit[k];
            }

            var start = Math.Max(0, values.Count - checkLast);
            for (var i = start; i < values.Count; i++)
            {
                // Обычный уровень того же места в сезоне: медиана прошлых значений с шагом period (по позициям исходного ряда).
                var at = positions[i];
                var peers = Enumerable.Range(1, 8).Select(k => at - k * period).Select(j => positions.IndexOf(j)).Where(j => j >= 0).Select(j => values[j]).ToList();
                var typical = peers.Count >= 3 ? StatMath.Median(peers) : (double?)null;
                var methods = new List<string>();
                if (Math.Abs(z[i]) > 3)
                {
                    methods.Add("z");
                }

                if (Math.Abs(mz[i]) > 3.5)
                {
                    methods.Add("modified_z");
                }

                if (residual[i] < iqr.Low || residual[i] > iqr.High)
                {
                    methods.Add("iqr");
                }

                var robust = seasonalZ[i] is { } sz && Math.Abs(sz) > 3.5;
                if (robust && seasonal)
                {
                    methods.Add("seasonal");
                }

                var missingData = values[i] == 0 && typical is > 0 && peers.Count(v => v > 0) >= peers.Count * 0.8;
                if (missingData)
                {
                    methods.Add("missing_data");
                }

                // Резкое изменение — к той же опоре, что и отклонение в отчёте: ожидаемому по сезонной декомпозиции, иначе обычному уровню дня цикла.
                var reference = expected[i] ?? typical;
                var sharp = reference is > 0 && Math.Abs(values[i] - reference.Value) / reference.Value > sharpShare;
                if (sharp)
                {
                    methods.Add(values[i] > reference ? "sharp_rise" : "sharp_drop");
                }

                // Подтверждение: устойчивая z остатка (с учётом сезона, если ряд позволяет) и ещё хотя бы один метод, либо пропавшие данные.
                var confirmed = missingData || robust && methods.Count(x => x is "z" or "iqr" or "sharp_rise" or "sharp_drop") >= 1;
                if (!confirmed)
                {
                    continue;
                }

                var expect = expected[i] ?? typical ?? StatMath.Median(values);
                var dev = values[i] - expect;
                found.Add(new AnomalyFinding(metric, Label(metric), m.Keys[positions[i]], values[i], expect, dev, expect != 0 ? dev / Math.Abs(expect) * 100 : null,
                    dev >= 0 ? "up" : "down", methods, missingData || Math.Abs(mz[i]) >= 5 ? "critical" : "warning"));
            }

            if (values.Count >= 2 * (m.Grain == "day" ? 7 : 4))
            {
                foreach (var cp in TimeSeriesMath.ChangePoints(values, 3, m.Grain == "day" ? 7 : 4).Where(x => x.P < 0.01 && Math.Abs(x.ShiftPct ?? 0) >= 15))
                {
                    structural.Add(new { metric, title = Label(metric), date = m.Keys[positions[cp.Index]], cp.MeanBefore, cp.MeanAfter, cp.ShiftPct, cp.P });
                }
            }
        }

        var byMetric = found.GroupBy(x => x.Title).Select(g => $"«{g.Key}» — {g.Count()}").ToList();
        lines.Add(found.Count == 0 ? "Подтверждённых аномалий нет" : $"Аномалий: {found.Count} ({string.Join(", ", byMetric)})");
        if (structural.Count > 0)
        {
            lines.Add($"сдвигов уровня: {structural.Count}");
        }

        return new MethodResult(new { grain = m.Grain, period, metrics = d.Metrics, checkedSteps = Math.Min(checkLast, m.Keys.Count), anomalies = found.Take(StoredItems), structural },
            $"Обзор аномалий ({d.Metrics.Count} показ., шаг {m.Grain}, сезон {period}): {string.Join("; ", lines)}.",
            EvidenceClasses.Observed,
            ["Аномалия — отклонение от обычного уровня с учётом дня недели (сезона); причину она не объясняет.",
             "Ноль там, где обычно есть данные, может быть и отсутствием продаж, и задержкой загрузки — проверьте загрузку Linko."]);
    }
}
