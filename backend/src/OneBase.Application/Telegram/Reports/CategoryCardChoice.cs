using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace OneBase.Application.Telegram.Reports;

/// <summary>
/// Карточка в выборе отчёта «11. Итоги дня по категориям»: категория из «Настроек продаж» и её товары (id товаров Linko), по которым считаются
/// цифры карточки; Products = null — вся категория (и новые товары в ней тоже). Строка превращается в карточку всей категории.
/// </summary>
public sealed record CategoryCardPick(string Category, IReadOnlyList<long>? Products = null)
{
    /// <summary>Вся категория — товары не выбраны.</summary>
    [JsonIgnore]
    public bool Whole => Products is not { Count: > 0 };

    public static implicit operator CategoryCardPick(string category) => new(category);
}

/// <summary>
/// Выбор категорий «Карточек категорий» (отчёт «11. Итоги дня по категориям»): категории из «Настроек продаж» и, если нужно, их товары;
/// второе название категории в планах и у людей; день утреннего итога.
/// </summary>
public static class CategoryCardChoice
{
    /// <summary>Сколько категорий в одной отправке: альбом Telegram — до 10 картинок.</summary>
    public const int Max = 10;

    /// <summary>Длина хранимого выбора (колонки CategoryCards и Categories).</summary>
    public const int StoredMax = 4000;

    /// <summary>Как категорию называют в планах Linko и в разговоре: «Помадка» — «Могуль», «Придзел» — Twizos.</summary>
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Помадка"] = "Могуль",
        ["Придзел"] = "Twizos",
    };

    // Названия категорий — кириллицей как есть, а не \uXXXX: запись читается в базе и короче (колонка — StoredMax символов).
    private static readonly JsonSerializerOptions Json = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Cyrillic),
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string? AliasOf(string name) => Aliases.GetValueOrDefault(name);

    /// <summary>
    /// Хранимый выбор → карточки. Только категории — названия через запятую («Придзел, Помадка», как раньше); с товарами — JSON
    /// ([{"category":"Придзел","products":[300,302]},{"category":"Помадка"}]). Без пустых и повторов категорий (регистр не важен), в заданном
    /// порядке; испорченный JSON — пустой выбор (уйдёт выбор по умолчанию).
    /// </summary>
    public static IReadOnlyList<CategoryCardPick> Parse(string? text)
    {
        var value = (text ?? "").Trim();
        if (!value.StartsWith('['))
        {
            return Normalize(value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(n => new CategoryCardPick(n)));
        }

        try
        {
            return Normalize(JsonSerializer.Deserialize<List<CategoryCardPick?>>(value, Json)?.OfType<CategoryCardPick>() ?? []);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Выбор для хранения: пусто — null («как по умолчанию»); без товаров — названия через запятую, с товарами — JSON.</summary>
    public static string? Format(IEnumerable<CategoryCardPick>? picks)
    {
        var list = Normalize(picks ?? []);
        return list.Count == 0 ? null
            : list.All(p => p.Whole) ? string.Join(",", list.Select(p => p.Category))
            : JsonSerializer.Serialize(list, Json);
    }

    /// <summary>Без пустых категорий и повторов (первое вхождение); товары — без повторов и нулей, по возрастанию id; пусто — вся категория.</summary>
    public static IReadOnlyList<CategoryCardPick> Normalize(IEnumerable<CategoryCardPick> picks) =>
        picks
            .Where(p => !string.IsNullOrWhiteSpace(p.Category))
            .Select(p => new CategoryCardPick(p.Category.Trim(), p.Products?.Where(id => id > 0).Distinct().Order().ToList() is { Count: > 0 } ids ? ids : null))
            .DistinctBy(p => p.Category, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Утренний итог — за прошлый рабочий день (пн–сб): в понедельник — за субботу.</summary>
    public static DateOnly PreviousWorkDay(DateOnly date)
    {
        var day = date.AddDays(-1);
        return day.DayOfWeek == DayOfWeek.Sunday ? day.AddDays(-1) : day;
    }
}
