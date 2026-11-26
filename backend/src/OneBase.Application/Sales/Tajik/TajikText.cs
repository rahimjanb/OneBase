using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace OneBase.Application.Sales.Tajik;

/// <summary>
/// Разбор текстов 1С Таджикистана: названия точек (регион по словам), названия товаров (код Linko в начале, вес единицы), даты API.
/// Чистые функции — без БД.
/// </summary>
public static partial class TajikText
{
    /// <summary>Часовой пояс Душанбе — UTC+5, как Ташкент.</summary>
    public static readonly TimeSpan Offset = TimeSpan.FromHours(5);

    public static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(now.ToOffset(Offset).DateTime);

    /// <summary>Дата API — ДД-ММ-ГГГГ.</summary>
    public static string ApiDate(DateOnly date) => date.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

    public static bool TryParseApiDate(string? text, out DateOnly date) =>
        DateOnly.TryParseExact(text?.Trim(), ["dd-MM-yyyy", "dd.MM.yyyy", "yyyy-MM-dd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    // Таджикские буквы и латинские двойники кириллицы (в словах с кириллицей) → базовая кириллица.
    private static readonly Dictionary<char, char> Letters = new()
    {
        ['ҷ'] = 'ч', ['ӯ'] = 'у', ['ҳ'] = 'х', ['қ'] = 'к', ['ғ'] = 'г', ['ӣ'] = 'и', ['ё'] = 'е', ['ë'] = 'е', ['ӑ'] = 'а', ['ӗ'] = 'е',
    };

    private static readonly Dictionary<char, char> LatinTwins = new()
    {
        ['a'] = 'а', ['e'] = 'е', ['o'] = 'о', ['p'] = 'р', ['c'] = 'с', ['x'] = 'х', ['k'] = 'к', ['m'] = 'м', ['t'] = 'т', ['y'] = 'у', ['h'] = 'н',
        ['b'] = 'в',
    };

    /// <summary>
    /// Текст для сравнения: нижний регистр, ҷ → ч, ӯ → у, ҳ → х, қ → к, ғ → г, ӣ → и, ё → е; в словах с кириллицей латинские двойники
    /// (a, e, o, p, c, x, k…) → кириллица; всё, кроме букв и цифр, — пробел.
    /// </summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var words = new List<string>();
        var current = new StringBuilder();
        void Flush()
        {
            if (current.Length == 0) return;
            var word = current.ToString();
            if (word.Any(IsCyrillic))
            {
                word = new string(word.Select(c => LatinTwins.TryGetValue(c, out var r) ? r : c).ToArray());
            }

            words.Add(word);
            current.Clear();
        }

        foreach (var raw in text.ToLowerInvariant())
        {
            var c = Letters.TryGetValue(raw, out var mapped) ? mapped : raw;
            if (char.IsLetterOrDigit(c))
            {
                current.Append(c);
            }
            else
            {
                Flush();
            }
        }

        Flush();
        return string.Join(' ', words);
    }

    private static bool IsCyrillic(char c) => c is >= 'а' and <= 'я' or 'ъ' or 'ь';

    /// <summary>
    /// Регион по словам в названии точки: ключевое слово (или несколько слов) должно стоять в названии целыми словами. Совпало несколько —
    /// побеждает то, что ближе к концу названия (город или район обычно в конце: «Муким стоянка Шариф Кулоб»), затем более длинное.
    /// </summary>
    public static TKey? MatchRegion<TKey>(string? storeName, IEnumerable<(TKey Key, IReadOnlyList<string> Keywords)> regions)
        where TKey : struct
    {
        var name = " " + Normalize(storeName) + " ";
        if (name.Length <= 2)
        {
            return null;
        }

        TKey? best = null;
        var bestEnd = -1;
        var bestLength = -1;
        foreach (var (key, keywords) in regions)
        {
            foreach (var keyword in keywords)
            {
                var word = Normalize(keyword);
                if (word.Length == 0)
                {
                    continue;
                }

                var needle = " " + word + " ";
                var at = name.LastIndexOf(needle, StringComparison.Ordinal);
                if (at < 0)
                {
                    continue;
                }

                var end = at + needle.Length;
                if (end > bestEnd || (end == bestEnd && word.Length > bestLength))
                {
                    best = key;
                    bestEnd = end;
                    bestLength = word.Length;
                }
            }
        }

        return best;
    }

    /// <summary>Номер в начале названия товара — код товара Linko («183 Азалия 1,5 кг», «600.Снеки…», «002.Rohat…»); нет — null.</summary>
    public static string? LinkoCode(string? productName)
    {
        var m = LeadingCode().Match(productName ?? string.Empty);
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>Совпадает ли код Linko с номером из названия: точно или как число («002» = «2»).</summary>
    public static bool SameCode(string? linkoCode, string code)
    {
        if (string.IsNullOrWhiteSpace(linkoCode))
        {
            return false;
        }

        var trimmed = linkoCode.Trim();
        return trimmed == code
            || (long.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var a)
                && long.TryParse(code, NumberStyles.None, CultureInfo.InvariantCulture, out var b) && a == b);
    }

    /// <summary>
    /// Вес из названия: «1,5 кг», «4кг», «3кгКор.» — один вариант (Single); «420гр*8шт», «200г*20шт» — штука (Piece) и коробка (Piece × Count):
    /// какая из них — единица 1С, решает цена (<see cref="TajikWeights"/>).
    /// </summary>
    public static (decimal? Single, decimal? Piece, int? Count) WeightOf(string? productName)
    {
        var text = (productName ?? string.Empty).ToLowerInvariant().Replace(',', '.');
        var box = BoxWeight().Match(text);
        if (box.Success)
        {
            var piece = Kg(box.Groups[1].Value, box.Groups[2].Value);
            var count = int.Parse(box.Groups[3].Value, CultureInfo.InvariantCulture);
            if (piece is > 0 && count > 0)
            {
                return (null, piece, count);
            }
        }

        var kg = KgWeight().Match(text);
        if (kg.Success && Kg(kg.Groups[1].Value, "кг") is { } k and > 0)
        {
            return (k, null, null);
        }

        var grams = GramWeight().Match(text);
        if (grams.Success && Kg(grams.Groups[1].Value, "г") is { } g and > 0)
        {
            return (g, null, null);
        }

        return (null, null, null);
    }

    private static decimal? Kg(string number, string unit)
    {
        if (!decimal.TryParse(number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }

        return unit is "кг" or "kg" ? value : value / 1000m;
    }

    /// <summary>Короткий постоянный идентификатор точки для адреса страницы (по названию из 1С).</summary>
    public static string StoreId(string customer) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(customer.Trim())))[..16];

    [GeneratedRegex(@"^\s*(\d{1,5})(?=[\s.\-)]|$)")]
    private static partial Regex LeadingCode();

    [GeneratedRegex(@"(\d+(?:\.\d+)?)\s*(кг|kg|гр|г|gr|g)\.?\s*[*xх×]\s*(\d+)\s*(?:шт|pcs)?")]
    private static partial Regex BoxWeight();

    [GeneratedRegex(@"(\d+(?:\.\d+)?)\s*(?:кг|kg)")]
    private static partial Regex KgWeight();

    [GeneratedRegex(@"(\d+(?:\.\d+)?)\s*(?:гр|г|gr|g)(?![a-zа-я])")]
    private static partial Regex GramWeight();
}
