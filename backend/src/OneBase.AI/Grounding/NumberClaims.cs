using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OneBase.AI.Grounding;

/// <summary>
/// Число в тексте ответа. Value — в единицах данных (с учётом «тыс», «млн», «млрд», «т»), Tolerance — половина последнего показанного разряда
/// (как округлил автор: «1,2 млрд» покрывает 1 150–1 250 млн), Percent — со знаком «%», Marked — у числа есть масштаб, «%» или единица.
/// ContextScale — масштаб из подписи, а не у самого числа: строка таблицы «| Выручка, млн сум | 13 457,8 |», заголовок столбца «Выручка,
/// тыс. сум» или подпись «Выручка (млн сум): 13 457,8». У числа со своим масштабом или «%» — нет.
/// </summary>
public sealed record NumberClaim(string Text, decimal Value, decimal Tolerance, bool Percent, int Index, int Length, bool Marked, decimal? ContextScale = null);

/// <summary>
/// Числа в тексте для проверки по данным. Не считаются утверждениями: даты, время, годы, номера пунктов списка, «15-е», ссылки, малые целые
/// без единицы («3 региона», «топ-5»); числа в разделах «Рекомендации» и «Прогноз» — это предложения и оценки, а не факты.
/// </summary>
public static partial class NumberClaims
{
    private const string Space = @"[   ]";

    [GeneratedRegex(@"(?<![\p{L}\p{N}_.,])(?<num>\d{1,3}(?:" + Space + @"\d{3})+(?:[.,]\d+)?|\d+(?:[.,]\d+)?)" +
                    @"(?:" + Space + @"*(?<scale>трлн|млрд|миллиард\p{L}*|млн|миллион\p{L}*|тыс(?:\.|\p{L}*)|тонн\p{L}*|т(?!\p{L})|mlrd|mln|ming))?" +
                    @"(?:" + Space + @"*(?<pct>%|процент\p{L}*|foiz))?" +
                    @"(?:" + Space + @"*(?<unit>сум\p{L}*|so['ʻ’‘]?m|кг|kg|шт|дн\p{L}*|kun\p{L}*))?", RegexOptions.IgnoreCase)]
    private static partial Regex Number();

    /// <summary>Даты «05.10», «05.10.2026», «1/7»: день и месяц в допустимых пределах.</summary>
    [GeneratedRegex(@"(?<![\p{N}.,])(?<d>\d{1,2})[./](?<m>\d{1,2})(?:[./]\d{2,4})?(?![\p{N}])")]
    private static partial Regex ShortDate();

    [GeneratedRegex(@"\d{4}-\d{2}(?:-\d{2})?(?:[T ]\d{2}:\d{2}(?::\d{2})?)?|(?<!\d)\d{1,2}:\d{2}(?::\d{2})?(?!\d)|\d+-(?:е|го|й|х|м|ти|ми|я|ая|ое|ые|ий|ой)(?!\p{L})|https?://\S+")]
    private static partial Regex Masked();

    [GeneratedRegex(@"^[ \t>]*(?:#+[ \t]*)?\d+[.)](?=[ \t])", RegexOptions.Multiline)]
    private static partial Regex ListMarker();

    [GeneratedRegex(@"^\s*(?:#{1,6}\s*|\*\*|__)", RegexOptions.None)]
    private static partial Regex HeadingStart();

    [GeneratedRegex(@"рекомендац|прогноз|tavsiya|prognoz|совет|что делать|план действий", RegexOptions.IgnoreCase)]
    private static partial Regex OpinionHeading();

    /// <summary>Масштаб в подписи: «млн сум», «тыс. сум», «млрд» (без «т» — в подписи это может быть и тонна, и сокращение).</summary>
    [GeneratedRegex(@"(?<![\p{L}])(?<scale>трлн|млрд|миллиард\p{L}*|млн|миллион\p{L}*|тыс(?:\.|\p{L}*)|mlrd|mln|ming)(?![\p{L}])", RegexOptions.IgnoreCase)]
    private static partial Regex ScaleWord();

    /// <summary>Подпись перед числом в строке: «Выручка, млн сум: », «Выручка (тыс. сум) — » — масштаб, затем только «сум», скобка и знак.</summary>
    [GeneratedRegex(@"(?<scale>трлн|млрд|миллиард\p{L}*|млн|миллион\p{L}*|тыс(?:\.|\p{L}*)|mlrd|mln|ming)[\s ]*(?:сум\p{L}*|so['ʻ’‘]?m)?[\s ]*\)?[\s ]*[:—–-][\s ]*$", RegexOptions.IgnoreCase)]
    private static partial Regex LabelScale();

    /// <summary>Все числа-утверждения текста (вне разделов рекомендаций и прогноза — см. <see cref="Checkable"/>).</summary>
    public static IReadOnlyList<NumberClaim> Find(string? text, bool keepSmall = false)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var masked = Mask(text);
        var list = new List<NumberClaim>();
        foreach (Match m in Number().Matches(masked))
        {
            var raw = m.Groups["num"].Value;
            var digits = raw.Replace(" ", "").Replace(" ", "").Replace(" ", "");
            var separator = digits.IndexOfAny(['.', ',']);
            var decimals = separator < 0 ? 0 : digits.Length - separator - 1;
            if (!decimal.TryParse(digits.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            {
                continue;
            }

            var scale = Scale(m.Groups["scale"].Value);
            var percent = m.Groups["pct"].Success;
            var marked = m.Groups["scale"].Success || percent || m.Groups["unit"].Success;
            if (!keepSmall && !marked)
            {
                // Год, номер, малое целое без единицы — не утверждение о данных.
                if (decimals == 0 && (value <= 10 || (value is >= 1990 and <= 2100 && !raw.Contains(' '))))
                {
                    continue;
                }
            }

            var tolerance = 0.5m * Pow10(-decimals) * scale;
            var context = m.Groups["scale"].Success || percent ? null : ContextScale(text, m.Index);
            list.Add(new NumberClaim(text.Substring(m.Index, m.Length).Trim(), value * scale, tolerance, percent, m.Index, m.Length, marked, context));
        }

        return list;
    }

    /// <summary>Числа, которые нужно подтвердить данными: всё, кроме разделов «Рекомендации» и «Прогноз».</summary>
    public static IReadOnlyList<NumberClaim> Checkable(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var skipped = OpinionRanges(text);
        return Find(text).Where(c => !skipped.Any(r => c.Index >= r.Start && c.Index < r.End)).ToList();
    }

    /// <summary>Предложение вокруг позиции (до точки с пробелом, «!», «?» или конца строки).</summary>
    public static string Sentence(string text, int index)
    {
        var start = index;
        while (start > 0 && !Boundary(text, start - 1))
        {
            start--;
        }

        var end = index;
        while (end < text.Length && !Boundary(text, end))
        {
            end++;
        }

        return text[start..end];
    }

    private static bool Boundary(string text, int i) =>
        text[i] is '\n' or '!' or '?' || (text[i] == '.' && (i + 1 >= text.Length || char.IsWhiteSpace(text[i + 1])));

    /// <summary>Разделы «Рекомендации», «Прогноз»: от такого заголовка до следующего заголовка.</summary>
    private static List<(int Start, int End)> OpinionRanges(string text)
    {
        var ranges = new List<(int Start, int End)>();
        var position = 0;
        int? open = null;
        foreach (var line in text.Split('\n'))
        {
            var heading = HeadingStart().IsMatch(line) && line.Trim().Length <= 80;
            if (heading)
            {
                if (open is { } s)
                {
                    ranges.Add((s, position));
                    open = null;
                }

                if (OpinionHeading().IsMatch(line))
                {
                    open = position;
                }
            }

            position += line.Length + 1;
        }

        if (open is { } last)
        {
            ranges.Add((last, text.Length));
        }

        return ranges;
    }

    /// <summary>Даты, время, «15-е», ссылки и номера пунктов заменяются пробелами (позиции чисел не сдвигаются).</summary>
    private static string Mask(string text)
    {
        var sb = new StringBuilder(text);
        void Blank(int index, int length)
        {
            for (var i = index; i < index + length && i < sb.Length; i++)
            {
                sb[i] = ' ';
            }
        }

        foreach (Match m in Masked().Matches(text))
        {
            Blank(m.Index, m.Length);
        }

        foreach (Match m in ListMarker().Matches(text))
        {
            Blank(m.Index, m.Length);
        }

        foreach (Match m in ShortDate().Matches(text))
        {
            var day = int.Parse(m.Groups["d"].Value, CultureInfo.InvariantCulture);
            var month = int.Parse(m.Groups["m"].Value, CultureInfo.InvariantCulture);
            var after = text.AsSpan(m.Index + m.Length).TrimStart("   ").ToString().ToLowerInvariant();
            // «1.2 млн», «0.25 %» — дробь, а не дата.
            var number = after.StartsWith('%') || after.StartsWith("млн") || after.StartsWith("млрд") || after.StartsWith("тыс") || after.StartsWith("mln")
                         || after.StartsWith("mlrd") || after.StartsWith("кг") || after.StartsWith("сум");
            if (!number && day is >= 1 and <= 31 && month is >= 1 and <= 12 && m.Groups["m"].Value.Length == 2)
            {
                Blank(m.Index, m.Length);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Масштаб из подписи числа: строка таблицы — подпись строки (первая ячейка) или заголовок столбца; иначе — подпись перед числом в той же
    /// строке («Выручка, млн сум: 13 457,8»). Нет подписи с масштабом — null.
    /// </summary>
    internal static decimal? ContextScale(string text, int index)
    {
        var lineStart = text.LastIndexOf('\n', Math.Max(0, index - 1)) + 1;
        if (index == 0)
        {
            lineStart = 0;
        }

        var lineEnd = text.IndexOf('\n', index);
        var line = text[lineStart..(lineEnd < 0 ? text.Length : lineEnd)];
        var at = index - lineStart;
        if (line.TrimStart().StartsWith('|'))
        {
            var cells = Cells(line);
            var column = cells.FindIndex(c => at >= c.Start && at < c.End);
            if (column > 0 && WordScale(cells[0].Text) is { } rowScale)
            {
                return rowScale;
            }

            if (column >= 0 && HeaderOf(text, lineStart) is { } header)
            {
                var headerCells = Cells(header);
                if (column < headerCells.Count && WordScale(headerCells[column].Text) is { } columnScale)
                {
                    return columnScale;
                }
            }

            return null;
        }

        var label = LabelScale().Match(line[..at]);
        return label.Success ? Scale(label.Groups["scale"].Value) : null;
    }

    private static decimal? WordScale(string cell) => ScaleWord().Match(cell) is { Success: true } m ? Scale(m.Groups["scale"].Value) : null;

    /// <summary>Ячейки строки таблицы Markdown с позициями внутри строки (без внешних «|»).</summary>
    private static List<(string Text, int Start, int End)> Cells(string line)
    {
        var cells = new List<(string, int, int)>();
        var bars = new List<int>();
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '|')
            {
                bars.Add(i);
            }
        }

        for (var k = 0; k + 1 < bars.Count; k++)
        {
            cells.Add((line[(bars[k] + 1)..bars[k + 1]], bars[k] + 1, bars[k + 1]));
        }

        return cells;
    }

    /// <summary>Заголовок таблицы: первая строка непрерывного блока строк с «|» над этой строкой (у заголовка под ним — строка «|---|»).</summary>
    private static string? HeaderOf(string text, int lineStart)
    {
        var lines = text[..lineStart].Split('\n');
        string? header = null;
        for (var i = lines.Length - 1; i >= 0; i--)
        {
            var l = lines[i].Trim();
            if (l.Length == 0 && i == lines.Length - 1)
            {
                continue;
            }

            if (!l.StartsWith('|'))
            {
                break;
            }

            header = lines[i];
        }

        return header is not null && !header.Replace("|", "").Replace("-", "").Replace(":", "").Trim().Equals(string.Empty) ? header : null;
    }

    private static decimal Scale(string word)
    {
        var w = word.ToLowerInvariant();
        return w switch
        {
            "" => 1m,
            _ when w.StartsWith("трлн") => 1_000_000_000_000m,
            _ when w.StartsWith("млрд") || w.StartsWith("миллиард") || w == "mlrd" => 1_000_000_000m,
            _ when w.StartsWith("млн") || w.StartsWith("миллион") || w == "mln" => 1_000_000m,
            _ => 1_000m, // тыс., тысяч, ming, т, тонн — тысячи (вес в данных — кг)
        };
    }

    private static decimal Pow10(int power)
    {
        var result = 1m;
        for (var i = 0; i < Math.Abs(power); i++)
        {
            result = power < 0 ? result / 10m : result * 10m;
        }

        return result;
    }
}
