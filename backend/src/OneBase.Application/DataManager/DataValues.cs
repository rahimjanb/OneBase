using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OneBase.Application.DataManager;

/// <summary>Разбор значений из отчётов: числа в любой записи, названия без регистра и лишних пробелов, буквы столбцов.</summary>
public static partial class DataValues
{
    /// <summary>
    /// Число из ячейки: «1 234,5», «1234.5», «1,234.5», «40 505 кг», «−12». Пусто — null.
    /// Не число — null и <paramref name="ok"/> = false (пустая ячейка — ok = true).
    /// </summary>
    public static decimal? ParseNumber(string? text, out bool ok)
    {
        ok = true;
        var s = (text ?? string.Empty).Trim();
        if (s.Length == 0 || s is "-" or "—" or "–")
        {
            return null;
        }

        s = s.Replace('−', '-').Replace(' ', ' ').Replace(' ', ' ').Replace("'", "");
        s = UnitSuffix().Replace(s, "").Trim();
        s = s.Replace(" ", "");

        var comma = s.LastIndexOf(',');
        var dot = s.LastIndexOf('.');
        if (comma >= 0 && dot >= 0)
        {
            // последний разделитель — десятичный, другой — разряды
            s = comma > dot ? s.Replace(".", "").Replace(',', '.') : s.Replace(",", "");
        }
        else if (comma >= 0)
        {
            // «1,234,567» — разряды; «12,5» и «1,234» — дробь (в узбекских и русских отчётах запятая десятичная)
            s = s.Count(c => c == ',') > 1 ? s.Replace(",", "") : s.Replace(',', '.');
        }
        else if (s.Count(c => c == '.') > 1)
        {
            s = s.Replace(".", "");
        }

        if (decimal.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
                CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        ok = false;
        return null;
    }

    /// <summary>Название для сравнения: нижний регистр, ё → е, без кавычек и повторных пробелов.</summary>
    public static string Normalize(string? text)
    {
        var s = (text ?? string.Empty).Trim().ToLowerInvariant().Replace('ё', 'е');
        var sb = new StringBuilder(s.Length);
        var space = false;
        foreach (var c in s)
        {
            if (c is '"' or '«' or '»' or '\'' or '`')
            {
                continue;
            }

            if (char.IsWhiteSpace(c) || c is ' ')
            {
                space = sb.Length > 0;
                continue;
            }

            if (space)
            {
                sb.Append(' ');
                space = false;
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    /// <summary>Номер столбца (0 — A) по букве: «A», «c», «AB». Не буквы — null.</summary>
    public static int? ColumnIndex(string? letters)
    {
        var s = (letters ?? string.Empty).Trim().ToUpperInvariant();
        if (s.Length is 0 or > 3 || s.Any(c => c is < 'A' or > 'Z'))
        {
            return null;
        }

        var n = 0;
        foreach (var c in s)
        {
            n = n * 26 + (c - 'A' + 1);
        }

        return n - 1;
    }

    /// <summary>Буква столбца по номеру (0 — A).</summary>
    public static string ColumnLetter(int index)
    {
        var n = index + 1;
        var sb = new StringBuilder();
        while (n > 0)
        {
            var r = (n - 1) % 26;
            sb.Insert(0, (char)('A' + r));
            n = (n - 1) / 26;
        }

        return sb.ToString();
    }

    /// <summary>Значение для хранения в плане изменений: инвариантная запись без лишних нулей.</summary>
    public static string Format(decimal value) => value.ToString("0.############", CultureInfo.InvariantCulture);

    public static decimal? ParseStored(string? stored) =>
        stored is null ? null : decimal.Parse(stored, NumberStyles.Number, CultureInfo.InvariantCulture);

    /// <summary>Месяц из ячейки: «9», «09», «2026-09», «09.2026», «сентябрь».</summary>
    public static (int? Year, int? Month) ParseMonth(string? text)
    {
        var s = Normalize(text);
        if (s.Length == 0)
        {
            return (null, null);
        }

        if (int.TryParse(s, out var plain))
        {
            return plain is >= 1 and <= 12 ? (null, plain) : (null, null);
        }

        var iso = IsoMonth().Match(s);
        if (iso.Success)
        {
            return (int.Parse(iso.Groups[1].Value), int.Parse(iso.Groups[2].Value));
        }

        var ru = DottedMonth().Match(s);
        if (ru.Success)
        {
            return (int.Parse(ru.Groups[2].Value), int.Parse(ru.Groups[1].Value));
        }

        for (var i = 0; i < MonthStems.Length; i++)
        {
            if (s.StartsWith(MonthStems[i], StringComparison.Ordinal))
            {
                var year = YearIn().Match(s);
                return (year.Success ? int.Parse(year.Value) : null, i + 1);
            }
        }

        return (null, null);
    }

    public static readonly string[] MonthNames =
        ["январь", "февраль", "март", "апрель", "май", "июнь", "июль", "август", "сентябрь", "октябрь", "ноябрь", "декабрь"];

    private static readonly string[] MonthStems = ["янв", "фев", "мар", "апр", "май", "июн", "июл", "авг", "сен", "окт", "ноя", "дек"];

    [GeneratedRegex(@"\s*(кг|kg|т|тонн[а-я]*|сум|sum|uzs|шт)\.?$", RegexOptions.IgnoreCase)]
    private static partial Regex UnitSuffix();

    [GeneratedRegex(@"^(20\d{2})[-./](0?[1-9]|1[0-2])")]
    private static partial Regex IsoMonth();

    [GeneratedRegex(@"^(0?[1-9]|1[0-2])[./](20\d{2})$")]
    private static partial Regex DottedMonth();

    [GeneratedRegex(@"20\d{2}")]
    private static partial Regex YearIn();
}
