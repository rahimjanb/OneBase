using System.Globalization;

namespace OneBase.Application.Analytik.Business;

/// <summary>
/// Период аналитики и период сравнения: today, yesterday, week (последние 7 дней), month, quarter, year (с начала по сегодня) или custom.
/// Сравнение — тот же отрезок раньше: месяц с 1 по 11-е — с 1 по 11-е прошлого месяца, неделя — с прошлой неделей, свой период из целых
/// месяцев — с тем же числом целых месяцев перед ним, остальной свой период — с отрезком той же длины перед ним.
/// </summary>
public sealed record AnalytikPeriod(string Kind, DateOnly From, DateOnly To, DateOnly PreviousFrom, DateOnly PreviousTo, string Label, string PreviousLabel)
{
    public const int MaxDays = 366;

    public int Days => To.DayNumber - From.DayNumber + 1;

    public static readonly string[] Kinds = ["today", "yesterday", "week", "month", "quarter", "year", "custom"];

    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    public static AnalytikPeriod Of(string? kind, DateOnly? from, DateOnly? to, DateOnly today)
    {
        kind = string.IsNullOrWhiteSpace(kind) ? "month" : kind.Trim().ToLowerInvariant();
        switch (kind)
        {
            case "today":
                return Same(kind, today, today, today.AddDays(-1), today.AddDays(-1));
            case "yesterday":
                return Same(kind, today.AddDays(-1), today.AddDays(-1), today.AddDays(-2), today.AddDays(-2));
            case "week":
                return Same(kind, today.AddDays(-6), today, today.AddDays(-13), today.AddDays(-7));
            case "month":
            {
                var start = new DateOnly(today.Year, today.Month, 1);
                var prevStart = start.AddMonths(-1);
                var prevEnd = new DateOnly(prevStart.Year, prevStart.Month, Math.Min(today.Day, DateTime.DaysInMonth(prevStart.Year, prevStart.Month)));
                return Same(kind, start, today, prevStart, prevEnd);
            }

            case "quarter":
            {
                var start = new DateOnly(today.Year, (today.Month - 1) / 3 * 3 + 1, 1);
                var prevStart = start.AddMonths(-3);
                var prevEnd = prevStart.AddDays(today.DayNumber - start.DayNumber);
                return Same(kind, start, today, prevStart, Min(prevEnd, start.AddDays(-1)));
            }

            case "year":
            {
                var start = new DateOnly(today.Year, 1, 1);
                var prevStart = start.AddYears(-1);
                var prevEnd = new DateOnly(today.Year - 1, today.Month, Math.Min(today.Day, DateTime.DaysInMonth(today.Year - 1, today.Month)));
                return Same(kind, start, today, prevStart, prevEnd);
            }

            case "custom":
            {
                if (from is not { } f || to is not { } t)
                {
                    throw new ArgumentException("Для своего периода нужны даты «с» и «по».");
                }

                if (t < f)
                {
                    throw new ArgumentException("Дата «по» раньше даты «с».");
                }

                if (t.DayNumber - f.DayNumber + 1 > MaxDays)
                {
                    throw new ArgumentException($"Период — не больше {MaxDays} дней.");
                }

                // Целые календарные месяцы (октябрь; июль — сентябрь) сравниваются с теми же числом целых месяцев перед ними, а не с «теми же
                // 31 днями»: иначе октябрь сравнивался бы с 31.08 — 30.09.
                if (f.Day == 1 && t.Day == DateTime.DaysInMonth(t.Year, t.Month))
                {
                    var months = (t.Year - f.Year) * 12 + t.Month - f.Month + 1;
                    return Same(kind, f, t, f.AddMonths(-months), f.AddDays(-1));
                }

                var length = t.DayNumber - f.DayNumber + 1;
                return Same(kind, f, t, f.AddDays(-length), f.AddDays(-1));
            }

            default:
                throw new ArgumentException($"Период: {string.Join(", ", Kinds)}.");
        }
    }

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;

    private static AnalytikPeriod Same(string kind, DateOnly from, DateOnly to, DateOnly prevFrom, DateOnly prevTo) =>
        new(kind, from, to, prevFrom, prevTo, LabelOf(from, to), LabelOf(prevFrom, prevTo));

    public static string LabelOf(DateOnly from, DateOnly to) =>
        from == to ? from.ToString("d MMMM yyyy", Ru)
        : from.Year == to.Year && from.Month == to.Month ? $"{from.Day}–{to.ToString("d MMMM yyyy", Ru)}"
        : $"{from.ToString("dd.MM.yyyy", Ru)} — {to.ToString("dd.MM.yyyy", Ru)}";
}

/// <summary>Вид показателя: факт из данных, расчёт кода по фактам, прогноз.</summary>
public static class KpiKinds
{
    public const string Fact = "fact";
    public const string Calculated = "calculated";
    public const string Forecast = "forecast";
}

/// <summary>
/// Показатель для панели и отчёта: код, название, значение, единица, значение прошлого периода и изменение, вид (факт / расчёт / прогноз),
/// откуда взят (источник) и страница OneBase, где его проверить. Value = null — данных нет (а не ноль), причина — в Note.
/// </summary>
public sealed record Kpi(
    string Code, string Title, decimal? Value, string Unit, decimal? Previous, decimal? ChangePercent, string Kind, string Source, string? Href,
    string? Note = null)
{
    public static Kpi Of(string code, string title, decimal? value, string unit, decimal? previous, string kind, string source, string? href, string? note = null) =>
        new(code, title, value, unit, previous, value is { } v && previous is { } p && p != 0 ? Math.Round((v - p) / Math.Abs(p) * 100, 1) : null, kind, source,
            href, note);
}

/// <summary>Блок отдела: показатели, заметки о полноте данных, источники, время данных.</summary>
public interface IAnalytikBlock
{
    IReadOnlyList<Kpi> Kpis { get; }
    IReadOnlyList<string> Notes { get; }
    IReadOnlyList<string> Sources { get; }
}
