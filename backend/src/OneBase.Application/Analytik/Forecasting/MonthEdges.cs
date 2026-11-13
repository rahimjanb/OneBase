namespace OneBase.Application.Analytik.Forecasting;

/// <summary>
/// Край месяца в дневном ряде (Forecaster): последний день месяца и 1-е число. Для каждого такого дня истории — отношение к обычному дню того же
/// дня недели (медиана значений за −14, −7, +7, +14 дней, без краёв месяца); множитель — медиана отношений. Учитывается, только если он
/// устойчив: не меньше 3 месяцев, отклонение от обычного дня не меньше 25 %, и не меньше трёх четвертей месяцев — в ту же сторону.
/// Продажи «Сладкой жизни»: в последний день месяца — около ×2, 1-го числа — около ×0,6.
/// </summary>
public static class MonthEdges
{
    public const string Last = "last";
    public const string First = "first";

    public const int MinMonths = 3;
    public const double MinEffect = 0.25;
    public const double MinAgreement = 0.75;

    public static string? KindOf(DateOnly d) => d.AddDays(1).Day == 1 ? Last : d.Day == 1 ? First : null;

    public static double Factor(IReadOnlyList<CalendarEffect> effects, DateOnly d)
    {
        if (effects.Count == 0)
        {
            return 1;
        }

        var kind = KindOf(d);
        return kind is null ? 1 : effects.FirstOrDefault(e => e.Kind == kind)?.Factor ?? 1;
    }

    public static IReadOnlyList<CalendarEffect> Estimate(IReadOnlyList<SeriesPoint> points)
    {
        var byDate = new Dictionary<DateOnly, double>();
        foreach (var p in points)
        {
            byDate[p.Date] = (double)p.Value;
        }

        var effects = new List<CalendarEffect>();
        foreach (var kind in new[] { Last, First })
        {
            var ratios = new List<double>();
            foreach (var (day, value) in byDate.Where(x => KindOf(x.Key) == kind))
            {
                var around = new[] { -14, -7, 7, 14 }.Select(k => day.AddDays(k)).Where(d => KindOf(d) is null && byDate.ContainsKey(d)).Select(d => byDate[d]).ToList();
                if (around.Count < 2)
                {
                    continue;
                }

                var baseline = Median(around);
                if (baseline > 0 && value >= 0)
                {
                    ratios.Add(value / baseline);
                }
            }

            if (ratios.Count < MinMonths)
            {
                continue;
            }

            var factor = Median(ratios);
            var agree = ratios.Count(r => factor > 1 ? r > 1 : r < 1) / (double)ratios.Count;
            // Множитель из нулей (дни без данных) или выброса больше чем в 5 раз — не эффект календаря.
            if (Math.Abs(factor - 1) >= MinEffect && agree >= MinAgreement && factor is >= 0.2 and <= 5)
            {
                effects.Add(new CalendarEffect(kind, Math.Round(factor, 3), ratios.Count));
            }
        }

        return effects;
    }

    private static double Median(List<double> values)
    {
        var sorted = values.Order().ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }
}
