namespace OneBase.Application.Telegram.Reports;

/// <summary>
/// Выбор категорий «Карточек категорий» (отчёт «11. Итоги дня по категориям»): названия из «Настроек продаж» через запятую; второе название
/// категории в планах и у людей; день утреннего итога.
/// </summary>
public static class CategoryCardChoice
{
    /// <summary>Сколько категорий в одной отправке: альбом Telegram — до 10 картинок.</summary>
    public const int Max = 10;

    /// <summary>Как категорию называют в планах Linko и в разговоре: «Помадка» — «Могуль», «Придзел» — Twizos.</summary>
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Помадка"] = "Могуль",
        ["Придзел"] = "Twizos",
    };

    public static string? AliasOf(string name) => Aliases.GetValueOrDefault(name);

    /// <summary>«Придзел, Помадка» → [«Придзел», «Помадка»]: без пустых и повторов (регистр не важен), в заданном порядке.</summary>
    public static IReadOnlyList<string> Parse(string? text) =>
        (text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Список для хранения: пусто — null («как по умолчанию»).</summary>
    public static string? Format(IEnumerable<string>? names) =>
        names?.Select(n => n.Trim()).Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList() is { Count: > 0 } list ? string.Join(",", list) : null;

    /// <summary>Утренний итог — за прошлый рабочий день (пн–сб): в понедельник — за субботу.</summary>
    public static DateOnly PreviousWorkDay(DateOnly date)
    {
        var day = date.AddDays(-1);
        return day.DayOfWeek == DayOfWeek.Sunday ? day.AddDays(-1) : day;
    }
}
