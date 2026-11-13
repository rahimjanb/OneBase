namespace OneBase.Application.Analytik.Compute;

/// <summary>Класс ABC: значение, доля, накопленная доля до позиции (%), класс.</summary>
public sealed record AbcItem(string Key, double Value, double SharePct, double CumulativePct, string Class);

/// <summary>Класс XYZ: среднее за период, коэффициент вариации (null — продаж не было), периодов, класс.</summary>
public sealed record XyzItem(string Key, double Mean, double? Cv, int Periods, string Class);

/// <summary>RFM клиента: давность (дней), частота, деньги, баллы 1–5, сегмент.</summary>
public sealed record RfmItem(string Key, double RecencyDays, double Frequency, double Monetary, int R, int F, int M, string Segment);

/// <summary>Когорта: период первой покупки, размер, доля активных в каждом следующем периоде (0 — сам период первой покупки, всегда 1).</summary>
public sealed record Cohort(int Start, int Size, IReadOnlyList<double> Retention);

/// <summary>Кластер k-средних: номер, размер, центр в исходных единицах, участники.</summary>
public sealed record ClusterItem(int Cluster, int Size, IReadOnlyList<double> Centroid, IReadOnlyList<string> Members);

/// <summary>Итог k-средних: кластеры, сумма квадратов расстояний (в стандартизованных единицах), силуэт (null — точек слишком много для расчёта), итераций.</summary>
public sealed record ClusterResult(IReadOnlyList<ClusterItem> Clusters, double Inertia, double? Silhouette, int Iterations);

/// <summary>Сегментация: ABC, XYZ, RFM, когорты удержания, кластеры k-средних (детерминированное начало по seed).</summary>
public static class SegmentMath
{
    /// <summary>ABC по накопленной доле: позиция входит в класс, если доля до неё меньше порога (первая позиция — всегда A).</summary>
    public static IReadOnlyList<AbcItem> Abc(IReadOnlyList<(string Key, double Value)> items, double a = 80, double b = 95)
    {
        StatMath.Require(items.Count > 0, "Нет позиций для ABC-анализа.");
        var sorted = items.Where(i => i.Value > 0).OrderByDescending(i => i.Value).ThenBy(i => i.Key, StringComparer.Ordinal).ToList();
        var total = sorted.Sum(i => i.Value);
        StatMath.Require(total > 0, "Сумма показателя по позициям — ноль: ABC-анализ невозможен.");
        var result = new List<AbcItem>();
        var before = 0d;
        foreach (var (key, value) in sorted)
        {
            var share = value / total * 100;
            var cls = before < a ? "A" : before < b ? "B" : "C";
            before += share;
            result.Add(new AbcItem(key, value, share, before, cls));
        }

        // Позиции без продаж — класс C с нулевой долей (они есть в справочнике, но ничего не дали).
        result.AddRange(items.Where(i => i.Value <= 0).OrderBy(i => i.Key, StringComparer.Ordinal)
            .Select(i => new AbcItem(i.Key, i.Value, 0, 100, "C")));
        return result;
    }

    /// <summary>XYZ по коэффициенту вариации значений по периодам: X ≤ x, Y ≤ y, иначе Z; без продаж — Z.</summary>
    public static IReadOnlyList<XyzItem> Xyz(IReadOnlyList<(string Key, IReadOnlyList<double> Values)> items, double x = 0.10, double y = 0.25)
    {
        StatMath.Require(items.Count > 0, "Нет позиций для XYZ-анализа.");
        StatMath.Require(items.All(i => i.Values.Count >= 3), "Для XYZ нужно хотя бы 3 периода у каждой позиции.");
        return items.Select(i =>
        {
            var mean = StatMath.Mean(i.Values);
            double? cv = mean > 0 ? StatMath.StdDev(i.Values) / mean : null;
            var cls = cv is null ? "Z" : cv <= x ? "X" : cv <= y ? "Y" : "Z";
            return new XyzItem(i.Key, mean, cv, i.Values.Count, cls);
        }).OrderBy(i => i.Class).ThenByDescending(i => i.Mean).ThenBy(i => i.Key, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// RFM: баллы 1–5 по квинтилям рангов (давность — чем меньше, тем выше балл). Сегменты: «Чемпионы» (R, F, M ≥ 4), «Новые» (R = 5, F = 1),
    /// «Лояльные» (F ≥ 4, R ≥ 3), «Перспективные» (R ≥ 4), «В зоне риска» (R ≤ 2, F ≥ 3), «Потерянные» (R = 1), «Спящие» (R = 2),
    /// «Требуют внимания» — остальные.
    /// </summary>
    public static IReadOnlyList<RfmItem> Rfm(IReadOnlyList<(string Key, double RecencyDays, double Frequency, double Monetary)> customers)
    {
        StatMath.Require(customers.Count >= 5, "Для RFM нужно хотя бы 5 клиентов.");
        var r = Scores(customers.Select(c => -c.RecencyDays).ToList());
        var f = Scores(customers.Select(c => c.Frequency).ToList());
        var m = Scores(customers.Select(c => c.Monetary).ToList());
        return customers.Select((c, i) => new RfmItem(c.Key, c.RecencyDays, c.Frequency, c.Monetary, r[i], f[i], m[i], RfmSegment(r[i], f[i], m[i])))
            .OrderByDescending(c => c.R + c.F + c.M).ThenBy(c => c.Key, StringComparer.Ordinal).ToList();
    }

    public static string RfmSegment(int r, int f, int m) => (r, f, m) switch
    {
        ( >= 4, >= 4, >= 4) => "Чемпионы",
        (5, 1, _) => "Новые",
        ( >= 3, >= 4, _) => "Лояльные",
        ( >= 4, _, _) => "Перспективные",
        ( <= 2, >= 3, _) => "В зоне риска",
        (1, _, _) => "Потерянные",
        (2, _, _) => "Спящие",
        _ => "Требуют внимания",
    };

    /// <summary>Баллы 1–5 по рангу (одинаковые значения — одинаковый балл): балл = ⌈ранг / n · 5⌉.</summary>
    private static int[] Scores(IReadOnlyList<double> values)
    {
        var ranks = StatMath.Ranks(values);
        var n = values.Count;
        return ranks.Select(rank => Math.Clamp((int)Math.Ceiling(rank / n * 5), 1, 5)).ToArray();
    }

    /// <summary>
    /// Когорты удержания: клиент принадлежит когорте периода первой активности; доля активных в периоде start + k среди когорты. Периоды —
    /// номера (0, 1, 2…), последний — maxPeriod.
    /// </summary>
    public static IReadOnlyList<Cohort> Cohorts(IReadOnlyList<(string Customer, int Period)> activity, int maxPeriod)
    {
        StatMath.Require(activity.Count > 0, "Нет активности клиентов для когорт.");
        var byCustomer = activity.GroupBy(a => a.Customer).ToDictionary(g => g.Key, g => g.Select(a => a.Period).ToHashSet());
        return byCustomer.GroupBy(c => c.Value.Min()).OrderBy(g => g.Key).Select(g =>
        {
            var size = g.Count();
            var retention = Enumerable.Range(0, maxPeriod - g.Key + 1)
                .Select(k => (double)g.Count(c => c.Value.Contains(g.Key + k)) / size)
                .ToList();
            return new Cohort(g.Key, size, retention);
        }).ToList();
    }

    /// <summary>
    /// k-средних на стандартизованных признаках (z-оценки): начало k-means++ с генератором по seed, итерации Ллойда до неизменных назначений
    /// (не больше 100). Силуэт — до 3 000 точек.
    /// </summary>
    public static ClusterResult KMeans(IReadOnlyList<(string Key, IReadOnlyList<double> Features)> points, int k, ulong seed = 42)
    {
        StatMath.Require(k >= 2, "Кластеров должно быть хотя бы 2.");
        StatMath.Require(points.Count >= k * 2, $"Мало объектов для {k} кластеров: {points.Count}.");
        var dims = points[0].Features.Count;
        StatMath.Require(dims >= 1 && points.All(p => p.Features.Count == dims), "У объектов разное число признаков.");
        var means = new double[dims];
        var sds = new double[dims];
        for (var d = 0; d < dims; d++)
        {
            var col = points.Select(p => p.Features[d]).ToList();
            means[d] = StatMath.Mean(col);
            sds[d] = StatMath.StdDev(col);
        }

        var z = points.Select(p => Enumerable.Range(0, dims).Select(d => sds[d] > 0 ? (p.Features[d] - means[d]) / sds[d] : 0).ToArray()).ToArray();
        var rng = new SplitMix64(seed);
        var centers = new List<double[]> { z[(int)(rng.NextULong() % (ulong)z.Length)] };
        while (centers.Count < k)
        {
            var dist = z.Select(p => centers.Min(c => Dist2(p, c))).ToArray();
            var total = dist.Sum();
            if (total <= 0)
            {
                break;
            }

            var target = rng.NextDouble() * total;
            var acc = 0d;
            var pick = z.Length - 1;
            for (var i = 0; i < z.Length; i++)
            {
                acc += dist[i];
                if (acc >= target)
                {
                    pick = i;
                    break;
                }
            }

            centers.Add(z[pick]);
        }

        StatMath.Require(centers.Count == k, "Объекты неразличимы по признакам — кластеры не выделяются.");
        var assign = new int[z.Length];
        Array.Fill(assign, -1);
        var iterations = 0;
        for (; iterations < 100; iterations++)
        {
            var changed = false;
            for (var i = 0; i < z.Length; i++)
            {
                var best = 0;
                var bestD = double.MaxValue;
                for (var c = 0; c < k; c++)
                {
                    var d2 = Dist2(z[i], centers[c]);
                    if (d2 < bestD)
                    {
                        bestD = d2;
                        best = c;
                    }
                }

                if (assign[i] != best)
                {
                    assign[i] = best;
                    changed = true;
                }
            }

            for (var c = 0; c < k; c++)
            {
                var members = Enumerable.Range(0, z.Length).Where(i => assign[i] == c).ToList();
                if (members.Count > 0)
                {
                    centers[c] = Enumerable.Range(0, dims).Select(d => members.Average(i => z[i][d])).ToArray();
                }
            }

            if (!changed)
            {
                break;
            }
        }

        var inertia = Enumerable.Range(0, z.Length).Sum(i => Dist2(z[i], centers[assign[i]]));
        double? silhouette = z.Length <= 3000 ? Silhouette(z, assign, k) : null;
        var clusters = Enumerable.Range(0, k).Select(c =>
        {
            var members = Enumerable.Range(0, z.Length).Where(i => assign[i] == c).ToList();
            var centroid = Enumerable.Range(0, dims).Select(d => members.Count == 0 ? 0 : members.Average(i => points[i].Features[d])).ToList();
            return new ClusterItem(c + 1, members.Count, centroid, members.Select(i => points[i].Key).ToList());
        }).Where(c => c.Size > 0).OrderByDescending(c => c.Size).ToList();
        return new ClusterResult(clusters, inertia, silhouette, iterations + 1);
    }

    private static double Dist2(double[] a, double[] b)
    {
        var s = 0d;
        for (var i = 0; i < a.Length; i++)
        {
            s += (a[i] - b[i]) * (a[i] - b[i]);
        }

        return s;
    }

    /// <summary>Средний силуэт: (b − a) / max(a, b), где a — среднее расстояние до своих, b — до ближайшего чужого кластера.</summary>
    private static double? Silhouette(double[][] z, int[] assign, int k)
    {
        var scores = new List<double>();
        for (var i = 0; i < z.Length; i++)
        {
            var own = Enumerable.Range(0, z.Length).Where(j => j != i && assign[j] == assign[i]).ToList();
            if (own.Count == 0)
            {
                continue;
            }

            var a = own.Average(j => Math.Sqrt(Dist2(z[i], z[j])));
            var b = double.MaxValue;
            for (var c = 0; c < k; c++)
            {
                if (c == assign[i])
                {
                    continue;
                }

                var others = Enumerable.Range(0, z.Length).Where(j => assign[j] == c).ToList();
                if (others.Count > 0)
                {
                    b = Math.Min(b, others.Average(j => Math.Sqrt(Dist2(z[i], z[j]))));
                }
            }

            if (b < double.MaxValue)
            {
                scores.Add((b - a) / Math.Max(a, b));
            }
        }

        return scores.Count == 0 ? null : scores.Average();
    }
}

/// <summary>Генератор псевдослучайных чисел SplitMix64: одинаковая последовательность на любой машине и версии .NET (в отличие от System.Random).</summary>
public sealed class SplitMix64(ulong seed)
{
    private ulong _state = seed;

    public ulong NextULong()
    {
        var z = _state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Равномерное число в [0, 1).</summary>
    public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

    /// <summary>Нормальное N(0, 1) (Бокс–Мюллер).</summary>
    public double NextNormal()
    {
        var u1 = 1 - NextDouble();
        var u2 = NextDouble();
        return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
    }

    /// <summary>Треугольное распределение (min, наиболее вероятное, max).</summary>
    public double NextTriangular(double min, double mode, double max)
    {
        if (max <= min)
        {
            return min;
        }

        var u = NextDouble();
        var c = (mode - min) / (max - min);
        return u < c
            ? min + Math.Sqrt(u * (max - min) * (mode - min))
            : max - Math.Sqrt((1 - u) * (max - min) * (max - mode));
    }
}
