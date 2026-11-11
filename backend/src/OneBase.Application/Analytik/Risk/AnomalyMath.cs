using OneBase.Application.Analytik.Forecasting;

namespace OneBase.Application.Analytik.Risk;

/// <summary>
/// Отклонение точки ряда: значение, ожидаемое (медиана сравнимых точек), робастный z (через MAD), направление, на сколько процентов
/// отличается, с чем сравнивалось.
/// </summary>
public sealed record AnomalyHit(DateOnly Date, decimal Value, decimal Expected, double Z, string Direction, decimal? ChangePercent, int Baseline, string Basis);

/// <summary>Изменение показателя к прошлому периоду.</summary>
public sealed record PeriodChange(decimal Current, decimal Previous, decimal Delta, decimal? Percent);

/// <summary>
/// Математика отклонений без модели. Робастный z = (x − медиана) ÷ (1,4826 × MAD): одна выброс-точка в базе не прячет другую, как среднее и
/// стандартное отклонение. Для дневных рядов база — тот же день недели за прошлые недели (понедельник сравнивается с понедельниками). Порог
/// |z| ≥ 3 — отклонение, ≥ 4 — сильное; при MAD = 0 (база одинаковая) — по относительному изменению. Мало базы — не оценивается.
/// </summary>
public static class AnomalyMath
{
    public const double Threshold = 3.0;
    public const double StrongThreshold = 4.0;

    /// <summary>Последняя точка ряда против базы: дневной ряд — те же дни недели (до 8), иначе — предыдущие точки (до 12).</summary>
    public static AnomalyHit? Latest(IReadOnlyList<SeriesPoint> series, string granularity, int minBaseline = 4)
    {
        var points = series.OrderBy(p => p.Date).ToList();
        if (points.Count < minBaseline + 1)
        {
            return null;
        }

        var last = points[^1];
        var (baseline, basis) = granularity == "day"
            ? (points.Where(p => p.Date < last.Date && p.Date.DayOfWeek == last.Date.DayOfWeek).TakeLast(8).ToList(), "тот же день недели за прошлые недели")
            : (points.Take(points.Count - 1).TakeLast(12).ToList(), "предыдущие периоды");
        return Score(last, baseline, minBaseline, basis);
    }

    /// <summary>Все точки последних checkLast шагов, отклоняющиеся от своей базы (для поиска отклонений за период).</summary>
    public static IReadOnlyList<AnomalyHit> Scan(IReadOnlyList<SeriesPoint> series, string granularity, int checkLast, int minBaseline = 4)
    {
        var points = series.OrderBy(p => p.Date).ToList();
        var hits = new List<AnomalyHit>();
        for (var i = Math.Max(minBaseline, points.Count - checkLast); i < points.Count; i++)
        {
            if (Latest(points.Take(i + 1).ToList(), granularity, minBaseline) is { } hit && Math.Abs(hit.Z) >= Threshold)
            {
                hits.Add(hit);
            }
        }

        return hits;
    }

    public static AnomalyHit? Score(SeriesPoint point, IReadOnlyList<SeriesPoint> baseline, int minBaseline, string basis)
    {
        if (baseline.Count < minBaseline)
        {
            return null;
        }

        var values = baseline.Select(p => (double)p.Value).ToArray();
        var median = Median(values);
        var mad = Median(values.Select(v => Math.Abs(v - median)).ToArray());
        var x = (double)point.Value;
        double z;
        if (mad > 0)
        {
            z = (x - median) / (1.4826 * mad);
        }
        else if (median != 0)
        {
            // База одинаковая: любое отличие больше 50 % считается сильным, иначе — нет отклонения.
            var relative = (x - median) / Math.Abs(median);
            z = Math.Abs(relative) >= 0.5 ? Math.Sign(relative) * StrongThreshold : 0;
        }
        else
        {
            z = x == 0 ? 0 : StrongThreshold * Math.Sign(x);
        }

        var change = median == 0 ? (decimal?)null : Math.Round((decimal)((x - median) / Math.Abs(median) * 100), 1);
        return new AnomalyHit(point.Date, point.Value, Math.Round((decimal)median, 2), Math.Round(z, 2), z >= 0 ? "up" : "down", change, baseline.Count, basis);
    }

    public static PeriodChange Change(decimal current, decimal previous) =>
        new(current, previous, current - previous, previous == 0 ? null : Math.Round((current - previous) / Math.Abs(previous) * 100, 1));

    public static double Median(double[] values)
    {
        if (values.Length == 0)
        {
            return 0;
        }

        var sorted = values.Order().ToArray();
        var mid = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }

    /// <summary>Критичность по силе отклонения.</summary>
    public static string Severity(double z) => Math.Abs(z) >= StrongThreshold ? "critical" : Math.Abs(z) >= Threshold ? "warning" : "info";
}
