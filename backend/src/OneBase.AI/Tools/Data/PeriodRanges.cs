using System.Globalization;
using System.Text.RegularExpressions;

namespace OneBase.AI.Tools.Data;

/// <summary>Отрезок дат включительно (обе границы входят): даты — по Ташкенту, как во всех данных OneBase.</summary>
public sealed record DateRange(DateOnly From, DateOnly To)
{
    public int Days => To.DayNumber - From.DayNumber + 1;

    /// <summary>«01.09.2026 — 30.09.2026».</summary>
    public string Label => From == To ? From.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)
        : $"{From.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)} — {To.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}";
}

/// <summary>
/// Периоды для AI: разбор («2026-09», «2026-Q3», «2026-H1», «2026», from/to), разбиение по месяцам, неделям (ISO, с понедельника) и на
/// части не длиннее N дней — подряд, без пересечений и пропусков (EnsureContiguous это проверяет), сравнительный период (предыдущий той же
/// длины, тот же год назад). Ничего не считает — только даты; расчёты — существующими сервисами по каждой части.
/// </summary>
public static partial class PeriodRanges
{
    /// <summary>Самый длинный период для AI — год (високосный).</summary>
    public const int MaxDays = 366;

    /// <summary>
    /// Период по тексту: «2026-09» — месяц, «2026-Q3» — квартал, «2026-H1» — полугодие, «2026» — год, «2026-09-01..2026-10-15» — отрезок.
    /// null — текст не распознан.
    /// </summary>
    public static DateRange? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var t = text.Trim().ToUpperInvariant().Replace('К', 'Q').Replace('П', 'H');
        if (Span().Match(t) is { Success: true } span
            && DateOnly.TryParseExact(span.Groups["from"].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from)
            && DateOnly.TryParseExact(span.Groups["to"].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var to))
        {
            return from <= to ? new DateRange(from, to) : new DateRange(to, from);
        }

        if (Month().Match(t) is { Success: true } m && int.Parse(m.Groups["m"].Value, CultureInfo.InvariantCulture) is var month and >= 1 and <= 12)
        {
            var year = int.Parse(m.Groups["y"].Value, CultureInfo.InvariantCulture);
            return MonthRange(year, month);
        }

        if (Quarter().Match(t) is { Success: true } q && int.Parse(q.Groups["q"].Value, CultureInfo.InvariantCulture) is var quarter and >= 1 and <= 4)
        {
            var year = int.Parse(q.Groups["y"].Value, CultureInfo.InvariantCulture);
            var first = new DateOnly(year, (quarter - 1) * 3 + 1, 1);
            return new DateRange(first, first.AddMonths(3).AddDays(-1));
        }

        if (Half().Match(t) is { Success: true } h && int.Parse(h.Groups["h"].Value, CultureInfo.InvariantCulture) is var half and >= 1 and <= 2)
        {
            var year = int.Parse(h.Groups["y"].Value, CultureInfo.InvariantCulture);
            var first = new DateOnly(year, half == 1 ? 1 : 7, 1);
            return new DateRange(first, first.AddMonths(6).AddDays(-1));
        }

        if (Year().Match(t) is { Success: true } y)
        {
            var year = int.Parse(y.Groups["y"].Value, CultureInfo.InvariantCulture);
            return new DateRange(new DateOnly(year, 1, 1), new DateOnly(year, 12, 31));
        }

        return null;
    }

    public static DateRange MonthRange(int year, int month)
    {
        var first = new DateOnly(year, month, 1);
        return new DateRange(first, first.AddMonths(1).AddDays(-1));
    }

    /// <summary>Части по календарным месяцам: первая и последняя — неполные, если отрезок начинается или кончается не на границе месяца.</summary>
    public static IReadOnlyList<DateRange> ByMonth(DateRange range)
    {
        var parts = new List<DateRange>();
        for (var from = range.From; from <= range.To;)
        {
            var monthEnd = new DateOnly(from.Year, from.Month, 1).AddMonths(1).AddDays(-1);
            var to = monthEnd < range.To ? monthEnd : range.To;
            parts.Add(new DateRange(from, to));
            from = to.AddDays(1);
        }

        return parts;
    }

    /// <summary>Части по неделям ISO (понедельник — воскресенье), обрезанные отрезком: неделя через границу месяца — одна часть.</summary>
    public static IReadOnlyList<DateRange> ByWeek(DateRange range)
    {
        var parts = new List<DateRange>();
        for (var from = range.From; from <= range.To;)
        {
            var sunday = from.AddDays((7 - (int)from.DayOfWeek) % 7);
            var to = sunday < range.To ? sunday : range.To;
            parts.Add(new DateRange(from, to));
            from = to.AddDays(1);
        }

        return parts;
    }

    /// <summary>Части не длиннее maxDays подряд (для сервисов с пределом периода, например HR — 92 дня).</summary>
    public static IReadOnlyList<DateRange> Chunks(DateRange range, int maxDays)
    {
        if (maxDays < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDays));
        }

        var parts = new List<DateRange>();
        for (var from = range.From; from <= range.To;)
        {
            var to = from.AddDays(maxDays - 1);
            to = to < range.To ? to : range.To;
            parts.Add(new DateRange(from, to));
            from = to.AddDays(1);
        }

        return parts;
    }

    /// <summary>Части покрывают отрезок ровно: идут подряд, без пересечений и пропусков, от его начала до конца. Иначе — исключение.</summary>
    public static void EnsureContiguous(IReadOnlyList<DateRange> parts, DateRange whole)
    {
        if (parts.Count == 0 || parts[0].From != whole.From || parts[^1].To != whole.To)
        {
            throw new InvalidOperationException($"Части периода не покрывают {whole.Label}.");
        }

        for (var i = 0; i < parts.Count; i++)
        {
            if (parts[i].From > parts[i].To || (i > 0 && parts[i].From != parts[i - 1].To.AddDays(1)))
            {
                throw new InvalidOperationException($"Части периода {whole.Label} пересекаются или с пропуском: {parts[i].Label}.");
            }
        }
    }

    /// <summary>Предыдущий период той же длины, сразу перед отрезком; целые месяцы — те же месяцы раньше (сентябрь → август, Q3 → Q2).</summary>
    public static DateRange Previous(DateRange range)
    {
        var months = WholeMonths(range);
        if (months > 0)
        {
            var from = range.From.AddMonths(-months);
            return new DateRange(from, range.From.AddDays(-1));
        }

        return new DateRange(range.From.AddDays(-range.Days), range.From.AddDays(-1));
    }

    /// <summary>Тот же период год назад (29 февраля — 28-го).</summary>
    public static DateRange YearAgo(DateRange range)
    {
        var from = range.From.AddYears(-1);
        var to = WholeMonths(range) > 0 ? new DateOnly(range.To.Year - 1, range.To.Month, 1).AddMonths(1).AddDays(-1) : range.To.AddYears(-1);
        return new DateRange(from, to);
    }

    /// <summary>Сколько целых месяцев в отрезке, если он от 1-го числа до последнего дня месяца; иначе 0.</summary>
    private static int WholeMonths(DateRange range)
    {
        if (range.From.Day != 1 || range.To.AddDays(1).Day != 1)
        {
            return 0;
        }

        return (range.To.Year - range.From.Year) * 12 + range.To.Month - range.From.Month + 1;
    }

    [GeneratedRegex(@"^(?<from>\d{4}-\d{2}-\d{2})\s*(?:\.\.|—|–|-{1,2}|/|\s+)\s*(?<to>\d{4}-\d{2}-\d{2})$")]
    private static partial Regex Span();

    [GeneratedRegex(@"^(?<y>\d{4})-(?<m>\d{1,2})$")]
    private static partial Regex Month();

    [GeneratedRegex(@"^(?<y>\d{4})[-\s]?Q(?<q>\d)$")]
    private static partial Regex Quarter();

    [GeneratedRegex(@"^(?<y>\d{4})[-\s]?H(?<h>\d)$")]
    private static partial Regex Half();

    [GeneratedRegex(@"^(?<y>\d{4})$")]
    private static partial Regex Year();
}
