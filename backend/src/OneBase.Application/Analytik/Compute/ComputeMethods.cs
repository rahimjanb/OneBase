using OneBase.Application.Analytik.Forecasting;
using OneBase.Application.Analytik.Risk;
using OneBase.Application.Analytik.Semantic;

namespace OneBase.Application.Analytik.Compute;

/// <summary>Методы вычислительного движка: какие срезы нужны каждому методу, расчёт кодом и короткий вывод с классом вывода.</summary>
public sealed partial class ComputeService
{
    private static async Task<MethodResult> ExecuteAsync(ComputeMethod method, ComputeData d, Params p, Loader l, DateOnly today) => method.Code switch
    {
        "describe" => await DescribeAsync(d, l),
        "trend" => await TrendAsync(d, p, l),
        "seasonality" => await SeasonalityAsync(d, l),
        "anomalies" => await AnomaliesAsync(d, p, l),
        "forecast" => await ForecastAsync(d, p, l),
        "correlation" => await CorrelationAsync(d, l),
        "regression" => await RegressionAsync(d, l),
        "lag" => await LagAsync(d, p, l),
        "drivers" => await DriversAsync(d, l),
        "pvm" => await PvmAsync(d, p, l),
        "before_after" => await BeforeAfterAsync(d, p, l),
        "did" => await DidAsync(d, p, l),
        "abc" => await AbcAsync(d, p, l),
        "abc_xyz" => await AbcXyzAsync(d, p, l),
        "rfm" => await RfmAsync(d, p, l),
        "cohorts" => await CohortsAsync(d, p, l),
        "clusters" => await ClustersAsync(d, p, l),
        "churn" => await ChurnAsync(d, p, l),
        "mix" => await MixAsync(d, l),
        "plan_variance" => await PlanVarianceAsync(d, p, l),
        "unit_economics" => await UnitEconomicsAsync(d, p, l),
        "cash_conversion" => await CashConversionAsync(d, p, l),
        "stock_health" => await StockHealthAsync(d, p, l, today),
        _ => throw new ComputeException($"Метод «{method.Code}» не реализован."),
    };

    private static string Title(string metric) => SemanticCatalog.Metric(metric)?.Title.ToLowerInvariant() ?? metric;

    private static string Grain(ComputeData d, string fallback) => d.Grain ?? fallback;

    private static IReadOnlyList<T> Top<T>(IEnumerable<T> items) => items.Take(ShownItems).ToList();

    private static string Strength(double p, int n, int min = 8) => p < EvidenceClasses.Alpha && n >= min ? EvidenceClasses.Statistical : EvidenceClasses.Observed;

    /// <summary>Значения по участникам измерения или, без измерения, по шагам времени.</summary>
    private static async Task<IReadOnlyList<(string Key, IReadOnlyList<double> Values)>> PointsAsync(ComputeData d, Loader l, IReadOnlyList<string> metrics)
    {
        if (d.Dimension is { } dim)
        {
            var rows = await l.RowsAsync(metrics, [dim], d.From!.Value, d.To!.Value);
            return rows.Select(r => (r.Label, (IReadOnlyList<double>)r.Values.Select(v => v ?? 0).ToList())).ToList();
        }

        var grain = Grain(d, "week");
        var series = new List<IReadOnlyList<(DateOnly Date, double Value)>>();
        foreach (var m in metrics)
        {
            series.Add(await l.SeriesAsync(m, grain, d.From!.Value, d.To!.Value));
        }

        var dates = series[0].Select(s => s.Date).ToList();
        return dates.Select(date => (date.ToString("yyyy-MM-dd"),
            (IReadOnlyList<double>)series.Select(s => s.FirstOrDefault(x => x.Date == date).Value).ToList())).ToList();
    }

    private static async Task<MethodResult> DescribeAsync(ComputeData d, Loader l)
    {
        var points = await PointsAsync(d, l, d.Metrics);
        var values = points.Select(x => x.Values[0]).ToList();
        var stats = StatMath.Describe(values);
        var sorted = points.OrderByDescending(x => x.Values[0]).ToList();
        var by = d.Dimension is null ? $"по шагам «{Grain(d, "week")}»" : $"по «{SemanticCatalog.Dimension(d.Dimension)!.Title}»";
        return new MethodResult(new
        {
            metric = d.Metrics[0], by, stats,
            top = Top(sorted.Take(5).Select(x => new { key = x.Key, value = x.Values[0] })),
            bottom = Top(sorted.TakeLast(5).Select(x => new { key = x.Key, value = x.Values[0] })),
        }, $"{Title(d.Metrics[0])} {by}: n = {stats.N}, среднее {N(stats.Mean)}, медиана {N(stats.Median)}, σ {N(stats.StdDev)}" +
           (stats.Cv is { } cv ? $", вариация {N(cv * 100)} %" : ""), null, []);
    }

    private static async Task<MethodResult> TrendAsync(ComputeData d, Params p, Loader l)
    {
        var grain = Grain(d, "week");
        var series = await l.SeriesAsync(d.Metrics[0], grain, d.From!.Value, d.To!.Value);
        var values = series.Select(s => s.Value).ToList();
        var trend = SeriesMath.Trend(values);
        var window = p.Int("window", grain == "day" ? 7 : 4, 2, 52);
        var ma = SeriesMath.MovingAverage(values, window);
        var cls = Strength(trend.P, trend.N);
        var direction = trend.Slope > 0 ? "рост" : trend.Slope < 0 ? "снижение" : "без изменения";
        return new MethodResult(new
        {
            metric = d.Metrics[0], grain, trend,
            series = series.Select((s, i) => new { date = s.Date, value = s.Value, movingAverage = ma[i] }).TakeLast(StoredItems),
        }, $"Тренд «{Title(d.Metrics[0])}» ({grain}): {direction} {N(trend.Slope)} за шаг" +
           (trend.SlopePctOfMean is { } pct ? $" ({N(pct)} % от среднего)" : "") + $", p = {P(trend.P)}, R² = {N(trend.RSquared, 2)} — {EvidenceClasses.Title(cls)}.",
            cls, []);
    }

    private static async Task<MethodResult> SeasonalityAsync(ComputeData d, Loader l)
    {
        var grain = Grain(d, "day");
        StatMath.Require(grain is "day" or "month", "Сезонность — по дням (день недели) или по месяцам (месяц года): шаг day или month.");
        var series = await l.SeriesAsync(d.Metrics[0], grain, d.From!.Value, d.To!.Value);
        var idx = grain == "day" ? SeriesMath.Weekday(series) : SeriesMath.Monthly(series);
        var peak = idx.MaxBy(i => i.Index)!;
        var low = idx.MinBy(i => i.Index)!;
        string Name(int pos) => grain == "day"
            ? new[] { "", "пн", "вт", "ср", "чт", "пт", "сб", "вс" }[pos]
            : Ru.DateTimeFormat.GetMonthName(pos);
        return new MethodResult(new { metric = d.Metrics[0], grain, indices = idx.Select(i => new { position = i.Position, name = Name(i.Position), i.Mean, i.Index, i.N }) },
            $"Сезонность «{Title(d.Metrics[0])}»: максимум — {Name(peak.Position)} (индекс {N(peak.Index, 2)}), минимум — {Name(low.Position)} ({N(low.Index, 2)}).",
            EvidenceClasses.Observed, []);
    }

    private static async Task<MethodResult> AnomaliesAsync(ComputeData d, Params p, Loader l)
    {
        var grain = Grain(d, "day");
        StatMath.Require(grain is "day" or "week" or "month", "Аномалии — по дням, неделям или месяцам.");
        var series = await l.SeriesAsync(d.Metrics[0], grain, d.From!.Value, d.To!.Value);
        var hits = AnomalyMath.Scan(series.Select(s => new SeriesPoint(s.Date, (decimal)s.Value)).ToList(), grain, p.Int("check_last", 7, 1, 60));
        return new MethodResult(new { metric = d.Metrics[0], grain, checkedPoints = series.Count, anomalies = hits },
            hits.Count == 0
                ? $"Аномалий «{Title(d.Metrics[0])}» в последних шагах нет."
                : $"Аномалий «{Title(d.Metrics[0])}»: {hits.Count}; сильнейшая — {hits.MaxBy(h => Math.Abs(h.Z))!.Date:dd.MM.yyyy} (z = {N(hits.MaxBy(h => Math.Abs(h.Z))!.Z, 1)}).",
            EvidenceClasses.Observed, ["Аномалия — отклонение от обычного уровня, а не объяснение причины."]);
    }

    private static async Task<MethodResult> ForecastAsync(ComputeData d, Params p, Loader l)
    {
        var grain = Grain(d, "day");
        StatMath.Require(grain is "day" or "week" or "month", "Прогноз — по дням, неделям или месяцам.");
        var series = await l.SeriesAsync(d.Metrics[0], grain, d.From!.Value, d.To!.Value);
        StatMath.Require(series.Count >= 4, "Для прогноза нужно хотя бы 4 шага истории.");
        var horizon = p.Int("horizon", grain == "day" ? 7 : grain == "week" ? 4 : 3, 1, 366);
        ForecastResult f;
        try
        {
            f = Forecaster.Forecast(series.Select(s => new SeriesPoint(s.Date, (decimal)s.Value)).ToList(), grain, horizon);
        }
        catch (ArgumentException ex)
        {
            throw new ComputeException(ex.Message);
        }

        return new MethodResult(new { metric = d.Metrics[0], forecast = f },
            $"Прогноз «{Title(d.Metrics[0])}» на {horizon} шаг(ов) ({grain}): {N((double)f.Total)} (диапазон {N((double)f.TotalLower)}–{N((double)f.TotalUpper)}), " +
            $"метод {f.Method}{(f.Wape is { } w ? $", WAPE {N((double)w * 100)} %" : "")}{(f.Reliable ? "" : " — ориентировочно")}.",
            EvidenceClasses.Assumption, ["Прогноз — расчёт по истории, а не факт; на него не влияют будущие решения и события."]);
    }

    private static async Task<MethodResult> CorrelationAsync(ComputeData d, Loader l)
    {
        var points = await PointsAsync(d, l, d.Metrics);
        var y = points.Select(x => x.Values[0]).ToList();
        var pairs = new List<object>();
        var best = EvidenceClasses.Observed;
        var lines = new List<string>();
        for (var i = 1; i < d.Metrics.Count; i++)
        {
            var x = points.Select(v => v.Values[i]).ToList();
            var pearson = StatMath.Pearson(y, x);
            var spearman = StatMath.Spearman(y, x);
            var cls = CausalMath.ForCorrelation(pearson.P, pearson.N);
            if (EvidenceClasses.Rank(cls) > EvidenceClasses.Rank(best))
            {
                best = cls;
            }

            pairs.Add(new { metric = d.Metrics[i], pearson, spearman, evidenceClass = cls });
            lines.Add($"с «{Title(d.Metrics[i])}» r = {N(pearson.R, 2)} (p = {P(pearson.P)}, n = {pearson.N})");
        }

        return new MethodResult(new { metric = d.Metrics[0], by = d.Dimension ?? Grain(d, "week"), pairs },
            $"Корреляция «{Title(d.Metrics[0])}»: {string.Join("; ", lines)}. Корреляция — не причина.", best,
            ["Корреляция показывает совместное изменение, но не причину; общий фактор может влиять на оба показателя."]);
    }

    private static async Task<MethodResult> RegressionAsync(ComputeData d, Loader l)
    {
        var points = await PointsAsync(d, l, d.Metrics);
        var y = points.Select(x => x.Values[0]).ToList();
        var xs = Enumerable.Range(1, d.Metrics.Count - 1).Select(i => (IReadOnlyList<double>)points.Select(v => v.Values[i]).ToList()).ToList();
        var reg = RegressionMath.Ols(y, xs, d.Metrics.Skip(1).ToList());
        var cls = Strength(reg.FP, reg.N);
        var terms = string.Join("; ", reg.Terms.Skip(1).Select(t => $"{Title(t.Name)}: {N(t.Coefficient, 3)} (p = {P(t.P)})"));
        return new MethodResult(new { metric = d.Metrics[0], by = d.Dimension ?? Grain(d, "week"), regression = reg with { Fitted = [] } },
            $"Регрессия «{Title(d.Metrics[0])}»: {terms}; R² = {N(reg.RSquared, 2)}, p(F) = {P(reg.FP)}, n = {reg.N} — {EvidenceClasses.Title(cls)}.", cls,
            ["Коэффициент — связь при прочих равных в этих данных, а не доказанное влияние."]);
    }

    private static async Task<MethodResult> LagAsync(ComputeData d, Params p, Loader l)
    {
        var grain = Grain(d, "day");
        var x = await l.SeriesAsync(d.Metrics[0], grain, d.From!.Value, d.To!.Value);
        var y = await l.SeriesAsync(d.Metrics[1], grain, d.From!.Value, d.To!.Value);
        var dates = x.Select(v => v.Date).Intersect(y.Select(v => v.Date)).OrderBy(v => v).ToList();
        var xs = dates.Select(dt => x.First(v => v.Date == dt).Value).ToList();
        var ys = dates.Select(dt => y.First(v => v.Date == dt).Value).ToList();
        var lag = SeriesMath.CrossCorrelation(xs, ys, p.Int("max_lag", 7, 0, 60));
        var cls = lag.Best is null ? EvidenceClasses.Observed : Strength(lag.BestAdjustedP ?? 1, lag.Best.N);
        return new MethodResult(new { lead = d.Metrics[0], follow = d.Metrics[1], grain, lag },
            lag.Best is null
                ? "Связь со сдвигом не найдена (ряды без вариации)."
                : $"«{Title(d.Metrics[0])}» и «{Title(d.Metrics[1])}»: сильнейшая связь при сдвиге {lag.Best.Lag} шаг(ов) ({grain}), r = {N(lag.Best.R, 2)}, " +
                  $"p с поправкой = {P(lag.BestAdjustedP ?? 1)} — {EvidenceClasses.Title(cls)}.",
            cls, ["Опережение одного ряда другим — не доказательство причины."]);
    }

    private static async Task<MethodResult> DriversAsync(ComputeData d, Loader l)
    {
        var dim = d.Dimension!;
        var a = await l.RowsAsync([d.Metrics[0]], [dim], d.CompareFrom!.Value, d.CompareTo!.Value);
        var b = await l.RowsAsync([d.Metrics[0]], [dim], d.From!.Value, d.To!.Value);
        var members = Merge(a, b);
        var r = DriverMath.Contribution(members);
        var down = r.Items.Where(i => i.Delta < 0).Take(3).Select(i => $"{i.Key} {N(i.Delta)}").ToList();
        var up = r.Items.Where(i => i.Delta > 0).Take(3).Select(i => $"{i.Key} +{N(i.Delta)}").ToList();
        return new MethodResult(new
            {
                metric = d.Metrics[0], dimension = dim, r.TotalA, r.TotalB, r.Delta, r.ChangePct, r.Top3ShareOfGross, members = r.Items.Count,
                items = r.Items.Take(StoredItems),
            },
            $"«{Title(d.Metrics[0])}»: {N(r.TotalA)} → {N(r.TotalB)} ({(r.ChangePct is { } pct ? (pct >= 0 ? "+" : "") + N(pct) + " %" : "—")}). " +
            (down.Count > 0 ? $"Вниз тянут: {string.Join(", ", down)}. " : "") + (up.Count > 0 ? $"Вверх: {string.Join(", ", up)}. " : "") +
            $"Три крупнейших дают {N(r.Top3ShareOfGross * 100)} % всех изменений.",
            EvidenceClasses.Observed, ["Разложение показывает, где изменилось, но не почему."]);
    }

    /// <summary>Участники двух периодов: подпись, значение «было» и «стало» (нет строки — ноль).</summary>
    private static List<(string Key, double A, double B)> Merge(IReadOnlyList<SliceRow> a, IReadOnlyList<SliceRow> b)
    {
        var map = new Dictionary<string, (string Label, double A, double B)>();
        foreach (var r in a)
        {
            map[string.Join('|', r.Keys)] = (r.Label, r.Value, 0);
        }

        foreach (var r in b)
        {
            var key = string.Join('|', r.Keys);
            map[key] = map.TryGetValue(key, out var x) ? (x.Label, x.A, r.Value) : (r.Label, 0, r.Value);
        }

        return map.Values.Select(v => (v.Label, v.A, v.B)).ToList();
    }

    private static async Task<MethodResult> PvmAsync(ComputeData d, Params p, Loader l)
    {
        var dim = p.Text("dimension") ?? d.Dimension ?? "product";
        StatMath.Require(dim is "product" or "sku" or "product_group" or "product_type", "PVM — по товару, артикулу, типу или товарной группе.");
        var a = await l.RowsAsync(["revenue", "kg"], [dim], d.CompareFrom!.Value, d.CompareTo!.Value);
        var b = await l.RowsAsync(["revenue", "kg"], [dim], d.From!.Value, d.To!.Value);
        var map = new Dictionary<string, (string Label, double QA, double RA, double QB, double RB)>();
        foreach (var r in a)
        {
            map[string.Join('|', r.Keys)] = (r.Label, r.Values[1] ?? 0, r.Values[0] ?? 0, 0, 0);
        }

        foreach (var r in b)
        {
            var key = string.Join('|', r.Keys);
            var x = map.GetValueOrDefault(key, (r.Label, 0, 0, 0, 0));
            map[key] = (x.Label, x.QA, x.RA, r.Values[1] ?? 0, r.Values[0] ?? 0);
        }

        var pvm = DriverMath.PriceVolumeMix(map.Values.Select(v => (v.Label, v.QA, v.RA, v.QB, v.RB)).ToList());
        return new MethodResult(new { dimension = dim, pvm = pvm with { Items = pvm.Items.Take(StoredItems).ToList() } },
            $"Выручка {N(pvm.RevenueA, 0)} → {N(pvm.RevenueB, 0)} (Δ {N(pvm.Delta, 0)}): объём {N(pvm.Volume, 0)}, ассортимент {N(pvm.Mix, 0)}, " +
            $"цена {N(pvm.Price, 0)}, новые позиции {N(pvm.NewItems, 0)}, выбывшие {N(pvm.LostItems, 0)}.",
            EvidenceClasses.Observed, ["Точное арифметическое разложение изменения; причины изменения цены или объёма оно не называет."]);
    }

    private static async Task<MethodResult> BeforeAfterAsync(ComputeData d, Params p, Loader l)
    {
        var change = p.Date("change_date") ?? throw new ComputeException("Нужна дата события: change_date (ГГГГ-ММ-ДД).");
        var series = await l.SeriesAsync(d.Metrics[0], "day", d.From!.Value, d.To!.Value);
        var before = series.Where(s => s.Date < change).Select(s => s.Value).ToList();
        var after = series.Where(s => s.Date >= change).Select(s => s.Value).ToList();
        var r = CausalMath.BeforeAfter(before, after);
        return new MethodResult(new { metric = d.Metrics[0], changeDate = change, result = r },
            $"«{Title(d.Metrics[0])}» в день: до {change:dd.MM.yyyy} — {N(r.Test.MeanA)}, после — {N(r.Test.MeanB)} " +
            $"({(r.Test.DiffPct is { } pct ? (pct >= 0 ? "+" : "") + N(pct) + " %" : "—")}, p = {P(r.Test.P)}) — {EvidenceClasses.Title(r.EvidenceClass)}.",
            r.EvidenceClass, r.Notes);
    }

    private static async Task<MethodResult> DidAsync(ComputeData d, Params p, Loader l)
    {
        var change = p.Date("change_date") ?? throw new ComputeException("Нужна дата события: change_date (ГГГГ-ММ-ДД).");
        var treatment = p.List("treatment");
        StatMath.Require(treatment.Count > 0, "Нужны участники группы: treatment (названия или id).");
        var control = p.List("control");
        var rows = await l.RowsAsync([d.Metrics[0]], [d.Dimension!, "date"], d.From!.Value, d.To!.Value, "day");
        bool In(SliceRow r, IReadOnlyList<string> names) =>
            names.Any(n => string.Equals(n, r.Keys[0], StringComparison.OrdinalIgnoreCase) || string.Equals(n.Trim(), r.Labels[0].Trim(), StringComparison.OrdinalIgnoreCase));
        var treatedRows = rows.Where(r => In(r, treatment)).ToList();
        StatMath.Require(treatedRows.Count > 0, $"Участники группы не найдены в данных: {string.Join(", ", treatment)}.");
        var controlRows = rows.Where(r => !In(r, treatment) && (control.Count == 0 || In(r, control))).ToList();
        StatMath.Require(controlRows.Count > 0, "Нет участников контрольной группы с данными.");
        var through = l.DataThrough is { } dt && dt < d.To ? dt : d.To!.Value;
        var days = Enumerable.Range(0, through.DayNumber - d.From!.Value.DayNumber + 1).Select(i => d.From!.Value.AddDays(i)).ToList();
        double Sum(IEnumerable<SliceRow> set, DateOnly day) => set.Where(r => r.Keys[1] == day.ToString("yyyy-MM-dd")).Sum(r => r.Value);
        var t = days.Select(day => Sum(treatedRows, day)).ToList();
        var c = days.Select(day => Sum(controlRows, day)).ToList();
        var idx = days.FindIndex(day => day >= change);
        StatMath.Require(idx > 0, "Дата события вне периода данных.");
        var r = CausalMath.DifferenceInDifferences(t, c, idx);
        return new MethodResult(new
            {
                metric = d.Metrics[0], dimension = d.Dimension, changeDate = change, treatment = treatedRows.Select(x => x.Labels[0]).Distinct(),
                controlMembers = controlRows.Select(x => x.Keys[0]).Distinct().Count(), result = r,
            },
            $"Разность разностей «{Title(d.Metrics[0])}» (событие {change:dd.MM.yyyy}): эффект {N(r.Effect)} в день" +
            (r.EffectPct is { } pct ? $" ({(pct >= 0 ? "+" : "")}{N(pct)} %)" : "") + $", p = {P(r.EffectP)}" +
            (r.PreTrendP is { } pre ? $", тренды до события {(pre >= EvidenceClasses.Alpha ? "параллельны" : "расходились")}" : "") + $" — {EvidenceClasses.Title(r.EvidenceClass)}.",
            r.EvidenceClass, [.. r.Notes, "p-значения без поправки на автокорреляцию дневных рядов."]);
    }

    private static async Task<MethodResult> AbcAsync(ComputeData d, Params p, Loader l)
    {
        var rows = await l.RowsAsync([d.Metrics[0]], [d.Dimension!], d.From!.Value, d.To!.Value);
        var abc = SegmentMath.Abc(rows.Select(r => (r.Label, r.Value)).ToList(), p.Number("a") ?? 80, p.Number("b") ?? 95);
        var total = abc.Sum(i => Math.Max(0, i.Value));
        var hhi = total > 0 ? abc.Sum(i => Math.Pow(Math.Max(0, i.Value) / total * 100, 2)) : 0;
        var counts = abc.GroupBy(i => i.Class).ToDictionary(g => g.Key, g => g.Count());
        var top10 = abc.Take(10).Sum(i => i.SharePct);
        return new MethodResult(new { metric = d.Metrics[0], dimension = d.Dimension, counts, hhi, top10SharePct = top10, items = abc.Take(StoredItems) },
            $"ABC «{Title(d.Metrics[0])}» по «{SemanticCatalog.Dimension(d.Dimension!)!.Title}»: A — {counts.GetValueOrDefault("A")}, B — {counts.GetValueOrDefault("B")}, " +
            $"C — {counts.GetValueOrDefault("C")}; топ-10 дают {N(top10)} %, HHI {N(hhi, 0)}.", null, []);
    }

    private static async Task<MethodResult> AbcXyzAsync(ComputeData d, Params p, Loader l)
    {
        var grain = Grain(d, "month");
        var rows = await l.RowsAsync([d.Metrics[0]], [d.Dimension!, "date"], d.From!.Value, d.To!.Value, grain);
        var buckets = rows.Select(r => r.Keys[1]).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
        var through = l.DataThrough is { } dt && dt < d.To ? dt : d.To!.Value;
        buckets = buckets.Where(b => Loader.End(DateOnly.Parse(b), grain) <= through).ToList();
        StatMath.Require(buckets.Count >= 3, $"Для XYZ нужно хотя бы 3 полных шага «{grain}» — возьмите период длиннее или шаг мельче.");
        var members = rows.GroupBy(r => r.Keys[0]).Select(g => (Label: g.First().Labels[0],
            Values: (IReadOnlyList<double>)buckets.Select(b => g.Where(r => r.Keys[1] == b).Sum(r => r.Value)).ToList())).ToList();
        var abc = SegmentMath.Abc(members.Select(m => (m.Label, m.Values.Sum())).ToList()).ToDictionary(i => i.Key);
        var xyz = SegmentMath.Xyz(members.Select(m => (m.Label, m.Values)).ToList(), p.Number("x") ?? 0.10, p.Number("y") ?? 0.25);
        var items = xyz.Select(x => new { key = x.Key, abc = abc.TryGetValue(x.Key, out var a) ? a.Class : "C", xyz = x.Class, value = abc.TryGetValue(x.Key, out var v) ? v.Value : 0, x.Cv })
            .OrderByDescending(i => i.value).ToList();
        var matrix = items.GroupBy(i => i.abc + i.xyz).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count());
        return new MethodResult(new { metric = d.Metrics[0], dimension = d.Dimension, grain, periods = buckets.Count, matrix, items = items.Take(StoredItems) },
            $"ABC/XYZ «{Title(d.Metrics[0])}» ({buckets.Count} шагов «{grain}»): {string.Join(", ", matrix.Select(m => $"{m.Key} — {m.Value}"))}.", null, []);
    }

    private static async Task<MethodResult> RfmAsync(ComputeData d, Params p, Loader l)
    {
        var dim = p.Text("dimension") ?? d.Dimension ?? "market";
        StatMath.Require(dim is "market" or "agent", "RFM — по точкам (market) или агентам (agent).");
        var to = l.DataThrough is { } dt && dt < d.To ? dt : d.To!.Value;
        var from = d.From!.Value;
        var all = await l.RowsAsync(["revenue", "orders"], [dim], from, to);
        var customers = all.Where(r => (r.Values[1] ?? 0) > 0).ToDictionary(r => r.Keys[0]);
        StatMath.Require(customers.Count >= 5, "Для RFM нужно хотя бы 5 клиентов с заказами за период.");
        var recency = customers.Keys.ToDictionary(k => k, _ => (double)(to.DayNumber - from.DayNumber + 1));
        foreach (var w in new[] { 90, 60, 30, 14, 7 }.Where(w => w < to.DayNumber - from.DayNumber + 1))
        {
            var active = await l.RowsAsync(["orders"], [dim], to.AddDays(-(w - 1)), to);
            foreach (var r in active.Where(r => (r.Values[0] ?? 0) > 0 && recency.ContainsKey(r.Keys[0])))
            {
                recency[r.Keys[0]] = w;
            }
        }

        var rfm = SegmentMath.Rfm(customers.Select(c => (c.Value.Label, recency[c.Key], c.Value.Values[1] ?? 0, c.Value.Values[0] ?? 0)).ToList());
        var segments = rfm.GroupBy(r => r.Segment).Select(g => new { segment = g.Key, customers = g.Count(), revenue = g.Sum(x => x.Monetary) })
            .OrderByDescending(s => s.revenue).ToList();
        return new MethodResult(new { dimension = dim, through = to, customers = rfm.Count, segments, items = rfm.Take(StoredItems) },
            $"RFM ({rfm.Count} клиентов по {to:dd.MM.yyyy}): " + string.Join("; ", segments.Take(5).Select(s => $"{s.segment} — {s.customers}, выручка {N(s.revenue, 0)}")) + ".",
            null, ["Давность — по окнам 7/14/30/60/90 дней до последнего дня данных."]);
    }

    private static async Task<MethodResult> CohortsAsync(ComputeData d, Params p, Loader l)
    {
        var dim = p.Text("dimension") ?? d.Dimension ?? "market";
        var start = SemanticEngine.Trunc(d.From!.Value, "month");
        var through = l.DataThrough is { } dt && dt < d.To ? dt : d.To!.Value;
        var months = new List<DateOnly>();
        for (var m = start; m <= through && months.Count < 12; m = m.AddMonths(1))
        {
            months.Add(m);
        }

        StatMath.Require(months.Count >= 2, "Для когорт нужно хотя бы 2 месяца.");
        var activity = new List<(string, int)>();
        for (var i = 0; i < months.Count; i++)
        {
            var end = Loader.End(months[i], "month");
            var rows = await l.RowsAsync(["kg"], [dim], months[i] < d.From ? d.From!.Value : months[i], end > through ? through : end);
            activity.AddRange(rows.Where(r => r.Value > 0).Select(r => (r.Keys[0], i)));
        }

        var cohorts = SegmentMath.Cohorts(activity, months.Count - 1);
        var avg = Enumerable.Range(1, months.Count - 1).Select(k =>
        {
            var eligible = cohorts.Where(c => c.Retention.Count > k).ToList();
            var size = eligible.Sum(c => c.Size);
            return new { offset = k, retention = size == 0 ? (double?)null : eligible.Sum(c => c.Retention[k] * c.Size) / size };
        }).ToList();
        return new MethodResult(new
            {
                dimension = dim, months = months.Select(m => m.ToString("yyyy-MM")), cohorts = cohorts.Select(c => new { start = months[c.Start].ToString("yyyy-MM"), c.Size, c.Retention }),
                averageRetention = avg,
            },
            $"Когорты ({cohorts.Count}): удержание через месяц — {(avg.FirstOrDefault()?.retention is { } r1 ? N(r1 * 100) + " %" : "—")}" +
            (avg.Count > 1 && avg[1].retention is { } r2 ? $", через два — {N(r2 * 100)} %" : "") + ".",
            null, ["Первая покупка — первая в окне анализа, а не за всю историю: первый месяц окна включает давних клиентов."]);
    }

    private static async Task<MethodResult> ClustersAsync(ComputeData d, Params p, Loader l)
    {
        var rows = await l.RowsAsync(d.Metrics, [d.Dimension!], d.From!.Value, d.To!.Value);
        var k = p.Int("k", 4, 2, 10);
        var result = SegmentMath.KMeans(rows.Select(r => (r.Label, (IReadOnlyList<double>)r.Values.Select(v => v ?? 0).ToList())).ToList(), k,
            (ulong)(p.Number("seed") ?? 42));
        var clusters = result.Clusters.Select(c => new
        {
            c.Cluster, c.Size,
            centroid = d.Metrics.Select((m, i) => new { metric = m, value = c.Centroid[i] }),
            members = c.Members.Take(20),
        });
        return new MethodResult(new { dimension = d.Dimension, metrics = d.Metrics, k, result.Inertia, result.Silhouette, result.Iterations, clusters },
            $"{result.Clusters.Count} кластеров по «{string.Join(", ", d.Metrics.Select(Title))}»: размеры {string.Join(", ", result.Clusters.Select(c => c.Size))}" +
            (result.Silhouette is { } s ? $", силуэт {N(s, 2)}" : "") + ".", EvidenceClasses.Observed, []);
    }

    private static async Task<MethodResult> ChurnAsync(ComputeData d, Params p, Loader l)
    {
        var by = p.Text("by");
        var dims = by is { Length: > 0 } ? new[] { "market", SemanticCatalog.Dimension(by)?.Code ?? by } : ["market"];
        var baseRows = await l.RowsAsync(["revenue"], dims, d.CompareFrom!.Value, d.CompareTo!.Value);
        var recent = (await l.RowsAsync(["revenue"], ["market"], d.From!.Value, d.To!.Value)).Where(r => r.Value > 0).Select(r => r.Keys[0]).ToHashSet();
        var buyers = baseRows.Where(r => r.Value > 0).GroupBy(r => r.Keys[0]).Select(g => (Key: g.Key, Label: g.First().Labels[0], Revenue: g.Sum(x => x.Value),
            Group: dims.Length > 1 ? g.OrderByDescending(x => x.Value).First().Labels[1] : null)).ToList();
        StatMath.Require(buyers.Count > 0, "В базовом периоде нет покупателей.");
        var lost = buyers.Where(b => !recent.Contains(b.Key)).OrderByDescending(b => b.Revenue).ToList();
        var baseRevenue = buyers.Sum(b => b.Revenue);
        var groups = by is null ? null : lost.GroupBy(x => x.Group ?? "—").Select(g => new
        {
            group = g.Key, lost = g.Count(), revenueAtRisk = g.Sum(x => x.Revenue),
            baseCustomers = buyers.Count(b => (b.Group ?? "—") == g.Key),
        }).OrderByDescending(x => x.revenueAtRisk).ToList();
        return new MethodResult(new
            {
                baseCustomers = buyers.Count, lost = lost.Count, lostShare = (double)lost.Count / buyers.Count, revenueAtRisk = lost.Sum(x => x.Revenue),
                revenueAtRiskShare = baseRevenue > 0 ? lost.Sum(x => x.Revenue) / baseRevenue : 0, groups,
                items = lost.Take(StoredItems).Select(x => new { market = x.Label, baseRevenue = x.Revenue, group = x.Group }),
            },
            $"Из {buyers.Count} покупателей базы не купили в периоде {lost.Count} ({N(100d * lost.Count / buyers.Count)} %), их выручка в базе {N(lost.Sum(x => x.Revenue), 0)} " +
            $"({N(baseRevenue > 0 ? lost.Sum(x => x.Revenue) / baseRevenue * 100 : 0)} % базы).", EvidenceClasses.Observed, []);
    }

    private static async Task<MethodResult> MixAsync(ComputeData d, Loader l)
    {
        var a = await l.RowsAsync([d.Metrics[0]], [d.Dimension!], d.CompareFrom!.Value, d.CompareTo!.Value);
        var b = await l.RowsAsync([d.Metrics[0]], [d.Dimension!], d.From!.Value, d.To!.Value);
        var members = Merge(a, b);
        var ta = members.Sum(m => m.A);
        var tb = members.Sum(m => m.B);
        StatMath.Require(ta != 0 && tb != 0, "Итог одного из периодов — ноль: доли не определены.");
        var items = members.Select(m => new { key = m.Key, shareA = m.A / ta * 100, shareB = m.B / tb * 100, shiftPp = (m.B / tb - m.A / ta) * 100, m.A, m.B })
            .OrderByDescending(x => Math.Abs(x.shiftPp)).ToList();
        return new MethodResult(new { metric = d.Metrics[0], dimension = d.Dimension, totalA = ta, totalB = tb, items = items.Take(StoredItems) },
            $"Структура «{Title(d.Metrics[0])}»: сильнее всего изменились доли — " +
            string.Join(", ", items.Take(3).Select(x => $"{x.key} {(x.shiftPp >= 0 ? "+" : "")}{N(x.shiftPp)} п.п.")) + ".", EvidenceClasses.Observed, []);
    }

    private static async Task<MethodResult> PlanVarianceAsync(ComputeData d, Params p, Loader l)
    {
        var dim = p.Text("dimension") ?? d.Dimension ?? "region";
        var rows = await l.RowsAsync(["plan_kg", "plan_fact_kg"], [dim], SemanticEngine.Trunc(d.From!.Value, "month"), d.To!.Value);
        var withPlan = rows.Where(r => r.Values[0] is > 0).ToList();
        StatMath.Require(withPlan.Count > 0, "За период планов нет: план — помесячный (Google Sheets / AI Data Manager или планы ТП Linko).");
        var r = DriverMath.Contribution(withPlan.Select(x => (x.Label, x.Values[0] ?? 0, x.Values[1] ?? 0)).ToList());
        var items = r.Items.Select(i => new { key = i.Key, plan = i.A, fact = i.B, deviation = i.Delta, execution = i.A != 0 ? i.B / i.A * 100 : (double?)null, shareOfDeviation = i.ShareOfNet })
            .ToList();
        var noPlan = rows.Count - withPlan.Count;
        return new MethodResult(new { dimension = dim, plan = r.TotalA, fact = r.TotalB, deviation = r.Delta, execution = r.TotalA != 0 ? r.TotalB / r.TotalA * 100 : (double?)null, withoutPlan = noPlan, items = items.Take(StoredItems) },
            $"План {N(r.TotalA, 0)} кг, факт для плана {N(r.TotalB, 0)} кг, отклонение {N(r.Delta, 0)} кг ({N(r.TotalA != 0 ? r.TotalB / r.TotalA * 100 : 0)} %); " +
            $"больше всего отклонение у: {string.Join(", ", items.Take(3).Select(i => $"{i.key} {N(i.deviation, 0)} кг"))}.",
            EvidenceClasses.Observed, noPlan > 0 ? [$"У {noPlan} участников плана нет — они не входят в отклонение."] : []);
    }

    private static async Task<MethodResult> UnitEconomicsAsync(ComputeData d, Params p, Loader l)
    {
        var dim = p.Text("dimension") ?? d.Dimension ?? "region";
        var rows = await l.RowsAsync(["revenue", "kg", "orders", "outlets"], [dim], d.From!.Value, d.To!.Value);
        var agents = dim == "agent" ? null : (await l.RowsAsync(["revenue"], [dim, "agent"], d.From!.Value, d.To!.Value))
            .Where(r => r.Value > 0).GroupBy(r => r.Keys[0]).ToDictionary(g => g.Key, g => g.Select(x => x.Keys[1]).Distinct().Count());
        double? Div(double? a, double? b) => a is { } x && b is > 0 ? x / b.Value : null;
        var items = rows.Select(r => new
        {
            key = r.Label, revenue = r.Values[0], kg = r.Values[1], orders = r.Values[2], outlets = r.Values[3],
            agents = agents?.GetValueOrDefault(r.Keys[0]),
            revenuePerOutlet = Div(r.Values[0], r.Values[3]), revenuePerOrder = Div(r.Values[0], r.Values[2]), kgPerOutlet = Div(r.Values[1], r.Values[3]),
            revenuePerAgent = agents is null ? null : Div(r.Values[0], agents.GetValueOrDefault(r.Keys[0])),
            pricePerKg = Div(r.Values[0], r.Values[1]),
        }).OrderByDescending(x => x.revenue).ToList();
        return new MethodResult(new { dimension = dim, items = items.Take(StoredItems) },
            $"Юнит-экономика по «{SemanticCatalog.Dimension(dim)?.Title}» ({items.Count}): выручка на точку — от {N(items.Min(i => i.revenuePerOutlet ?? double.MaxValue), 0)} " +
            $"до {N(items.Max(i => i.revenuePerOutlet ?? 0), 0)}.", null, ["Себестоимости и расходов на точку в OneBase нет — маржа на единицу не считается."]);
    }

    private static async Task<MethodResult> CashConversionAsync(ComputeData d, Params p, Loader l)
    {
        var grain = Grain(d, "week");
        var pay = await l.SeriesAsync("payments", grain, d.From!.Value, d.To!.Value);
        var rev = await l.SeriesAsync("revenue", grain, d.From!.Value, d.To!.Value);
        var dates = pay.Select(x => x.Date).Intersect(rev.Select(x => x.Date)).OrderBy(x => x).ToList();
        StatMath.Require(dates.Count >= 3, "Мало общих шагов оплат и продаж (оплаты в OneBase — с 01.09.2026).");
        var rows = dates.Select(dt => new { date = dt, payments = pay.First(x => x.Date == dt).Value, revenue = rev.First(x => x.Date == dt).Value }).ToList();
        var totalPay = rows.Sum(r => r.payments);
        var totalRev = rows.Sum(r => r.revenue);
        LagResult? lag = null;
        if (rows.Count >= 8)
        {
            try
            {
                lag = SeriesMath.CrossCorrelation(rows.Select(r => r.revenue).ToList(), rows.Select(r => r.payments).ToList(), p.Int("max_lag", 4, 0, Math.Max(0, rows.Count - 5)));
            }
            catch (ComputeException)
            {
                lag = null;
            }
        }

        return new MethodResult(new { grain, totalPayments = totalPay, totalRevenue = totalRev, ratio = totalRev > 0 ? totalPay / totalRev : (double?)null, lag, rows },
            $"Оплаты {N(totalPay, 0)} к выручке {N(totalRev, 0)} — {N(totalRev > 0 ? totalPay / totalRev * 100 : 0)} %" +
            (lag?.Best is { } b ? $"; оплаты сильнее всего связаны с продажами {b.Lag} шаг(ов) назад (r = {N(b.R, 2)})" : "") + ".",
            lag?.Best is { } best ? Strength(lag.BestAdjustedP ?? 1, best.N) : EvidenceClasses.Observed,
            ["Это поступления от точек; выплат, расходов и остатков денег в OneBase нет — полный денежный поток не считается."]);
    }

    private static async Task<MethodResult> StockHealthAsync(ComputeData d, Params p, Loader l, DateOnly today)
    {
        var dim = p.Text("dimension") ?? d.Dimension ?? "product";
        StatMath.Require(dim is "product" or "sku" or "product_group" or "product_type", "Здоровье запаса — по товару, артикулу, типу или группе.");
        var lead = p.Number("lead_days") ?? 7;
        var excess = p.Number("excess_days") ?? 60;
        // Остатки — по складам и товарам: фильтры по регионам и агентам к ним не применяются (только к продажам).
        var stockFilters = (d.Filters ?? []).Where(f => SemanticCatalog.Dimension(f.Dimension)?.Facts.Contains(SemanticCatalog.Stock) == true).ToList();
        var limitations = new List<string> { "Остатки — снимок Linko на сейчас; оборачиваемость — продажи 30 дней ÷ текущий остаток." };
        if (stockFilters.Count < (d.Filters?.Count ?? 0))
        {
            limitations.Add("Остатки — по складам Linko, без регионов и агентов: фильтр по ним применён только к продажам.");
        }

        var stock = await l.RowsAsync(["stock_units", "cover_days"], [dim], today.AddDays(-29), today, filters: stockFilters);
        var sales30 = (await l.RowsAsync(["units"], [dim], today.AddDays(-29), today)).ToDictionary(r => string.Join('|', r.Keys), r => r.Value);
        var sales90 = (await l.RowsAsync(["units"], [dim], today.AddDays(-89), today)).ToDictionary(r => string.Join('|', r.Keys), r => r.Value);
        var items = stock.Where(r => (r.Values[0] ?? 0) > 0).Select(r =>
        {
            var key = string.Join('|', r.Keys);
            var s30 = sales30.GetValueOrDefault(key);
            var s90 = sales90.GetValueOrDefault(key);
            var cover = r.Values[1];
            var status = s90 <= 0 ? "dead_90" : s30 <= 0 ? "no_sales_30" : cover is { } c && c < lead ? "stockout_risk" : cover is { } e && e > excess ? "excess" : "ok";
            return new { key = r.Label, stock = r.Values[0] ?? 0, sales30 = s30, sales90 = s90, coverDays = cover, turnover30 = r.Values[0] > 0 ? s30 / r.Values[0] : null, status };
        }).OrderBy(i => i.status == "ok").ThenByDescending(i => i.stock).ToList();
        var counts = items.GroupBy(i => i.status).ToDictionary(g => g.Key, g => g.Count());
        var deadShare = items.Sum(i => i.stock) is var total and > 0 ? items.Where(i => i.status == "dead_90").Sum(i => i.stock) / total : 0;
        return new MethodResult(new { dimension = dim, leadDays = lead, excessDays = excess, counts, deadStockShare = deadShare, items = items.Take(StoredItems) },
            $"Запас по {items.Count} позициям: риск дефицита — {counts.GetValueOrDefault("stockout_risk")}, излишек — {counts.GetValueOrDefault("excess")}, " +
            $"без продаж 30 дней — {counts.GetValueOrDefault("no_sales_30")}, 90 дней (неликвид) — {counts.GetValueOrDefault("dead_90")} ({N(deadShare * 100)} % запаса).",
            EvidenceClasses.Observed, limitations);
    }
}
