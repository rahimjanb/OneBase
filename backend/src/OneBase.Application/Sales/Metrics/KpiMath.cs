using System.Text.Json;
using System.Text.RegularExpressions;
using OneBase.Domain.Sales;

namespace OneBase.Application.Sales.Metrics;

/// <summary>Уровень премии KPI-показателя Linko: выполнение в процентах [From; To), бонус и способ расчёта.</summary>
public sealed record KpiLevel(decimal From, decimal? To, decimal Bonus, string Type);

/// <summary>
/// Формулы раздела «KPI» — как в Linko («Зарплаты») и в «Полевом контроле»: метка колонки по названию показателя, единица по типу плана,
/// порог и «Бонус 100%» — первый уровень с бонусом, премия — бонус уровня × выполнение (не выше 100%), выполнение — до тысячных (к чётному).
/// </summary>
public static partial class KpiMath
{
    public const string Kg = "кг";
    public const string Sum = "сум";
    public const string Pieces = "шт";
    public const string Akb = "АКБ";

    /// <summary>Порядок колонок региона: сначала эти метки, затем остальные по алфавиту (ординально, как в «Полевом контроле»).</summary>
    public static readonly IReadOnlyList<string> Canonical = ["Тотал", "Могуль", "Twizos", "АКБ"];

    public static string UnitOf(string planType) => planType switch
    {
        StaffPlanTypes.SalesWeight => Kg,
        StaffPlanTypes.SalesSum => Sum,
        StaffPlanTypes.SalesPieces => Pieces,
        StaffPlanTypes.ActiveClients => Akb,
        _ => planType,
    };

    // Сначала длинные: «Бамбук + Шоколад» раньше «Бамбук», «Кекс 2.5 кг» раньше «Кекс».
    private static readonly (Regex Pattern, string Label)[] Labels =
    [
        (new Regex(@"бамбук\s*\+\s*шоколад", RegexOptions.IgnoreCase), "Бамбук + Шоколад"),
        (new Regex(@"трубочка\s*\+\s*печен", RegexOptions.IgnoreCase), "Трубочка + Печенье"),
        (new Regex(@"кекс\s*2[.,]5\s*кг", RegexOptions.IgnoreCase), "Кекс 2.5 кг"),
        (new Regex(@"кекс\s*1\s*кг", RegexOptions.IgnoreCase), "Кекс 1 кг"),
        (new Regex(@"помадка\s*1\s*кг", RegexOptions.IgnoreCase), "Помадка 1 кг"),
        (new Regex(@"помадка\s*230\s*гр", RegexOptions.IgnoreCase), "Помадка 230 гр"),
        (new Regex(@"помадка\s*420\s*гр", RegexOptions.IgnoreCase), "Помадка 420 Гр"),
        (new Regex(@"акб", RegexOptions.IgnoreCase), "АКБ"),
        (new Regex(@"тотал", RegexOptions.IgnoreCase), "Тотал"),
        (new Regex(@"могуль", RegexOptions.IgnoreCase), "Могуль"),
        (new Regex(@"twizos", RegexOptions.IgnoreCase), "Twizos"),
        (new Regex(@"бамбук", RegexOptions.IgnoreCase), "Бамбук"),
        (new Regex(@"шоколад", RegexOptions.IgnoreCase), "Шоколад"),
        (new Regex(@"фрулли", RegexOptions.IgnoreCase), "Фрулли"),
        (new Regex(@"трубочк", RegexOptions.IgnoreCase), "Трубочка"),
        (new Regex(@"печен", RegexOptions.IgnoreCase), "Печенье"),
        (new Regex(@"кекс", RegexOptions.IgnoreCase), "Кекс"),
    ];

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"^(Январь|Февраль|Март|Апрель|Май|Июнь|Июль|Август|Сентябрь|Октябрь|Ноябрь|Декабрь)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex Month();

    [GeneratedRegex(@"\s(Вес|Сумма|Термез|Денов|Хоразм|Навои|Наманган|Ташкент|Сырдаре|Сырдарья|\d+([.,]\d+)?\s*млн|Супер|0)(\s|$).*$", RegexOptions.IgnoreCase)]
    private static partial Regex Stop();

    /// <summary>Короткая метка колонки по названию показателя Linko: «Октябрь Могуль Наманган Супер» → «Могуль».</summary>
    public static string Label(string name)
    {
        var text = Spaces().Replace(name, " ").Trim();
        foreach (var (pattern, label) in Labels)
        {
            if (pattern.IsMatch(text))
            {
                return label;
            }
        }

        // Неизвестная группа: без месяца, региона, «Вес/Сумма», плана в млн и «Супер».
        text = Stop().Replace(Month().Replace(text, ""), "").Replace("«", "").Replace("»", "").Replace("\"", "").Replace(" gazaklari", "").Trim();
        return text.Length == 0 ? Spaces().Replace(name, " ").Trim() : text.Replace("Печенья", "Печенье");
    }

    /// <summary>Порядок колонок: канонические метки, затем остальные ординально.</summary>
    public static IReadOnlyList<string> Order(IEnumerable<string> labels)
    {
        var set = labels.Distinct().ToList();
        return Canonical.Where(set.Contains).Concat(set.Where(l => !Canonical.Contains(l)).Order(StringComparer.Ordinal)).ToList();
    }

    /// <summary>СВР — у человека есть показатель «… Супер» (по должности Linko не определить: бывают СВР с должностью «Агент»).</summary>
    public static bool IsSupervisorIndicator(string name) => name.Contains("супер", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<KpiLevel> ParseLevels(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.EnumerateArray()
                .Select(e => new KpiLevel(
                    e.GetProperty("from").GetDecimal(),
                    e.TryGetProperty("to", out var to) && to.ValueKind == JsonValueKind.Number ? to.GetDecimal() : null,
                    e.TryGetProperty("bonus", out var b) && b.ValueKind == JsonValueKind.Number ? b.GetDecimal() : 0,
                    e.TryGetProperty("type", out var t) ? t.GetString() ?? "" : ""))
                .OrderBy(l => l.From)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Порог премии (доля) и «Бонус 100%» — первый уровень с ненулевым бонусом; без него — (null, 0).</summary>
    public static (decimal? Threshold, decimal Bonus) BonusLevel(IReadOnlyList<KpiLevel> levels) =>
        levels.Where(l => l.Bonus > 0).OrderBy(l => l.From).FirstOrDefault() is { } level ? (level.From / 100, level.Bonus) : (null, 0);

    /// <summary>Выполнение до тысячных: 121/400 → 0,302 (к чётному, как в Linko и «Полевом контроле»).</summary>
    public static decimal Pct(decimal fact, decimal plan) => plan == 0 ? 0 : Math.Round(fact / plan, 3, MidpointRounding.ToEven);

    /// <summary>
    /// Премия по уровням Linko: уровень, в который попало выполнение (в процентах, [от; до), выше последнего — последний); «процент от бонуса»
    /// — бонус × выполнение, не выше 100%; «бонус» — сумма уровня. Сверено с начисленной премией Linko: 560 из 560 строк (21.10.2026).
    /// </summary>
    public static decimal Premium(IReadOnlyList<KpiLevel> levels, decimal ratio)
    {
        if (levels.Count == 0)
        {
            return 0;
        }

        var percent = ratio * 100;
        var level = levels.FirstOrDefault(l => percent >= l.From && (l.To is not { } to || percent < to))
                    ?? (percent >= levels.Max(l => l.To ?? l.From) ? levels.MaxBy(l => l.To ?? l.From) : null);
        if (level is null || level.Bonus <= 0)
        {
            return 0;
        }

        return level.Type switch
        {
            "bonus" => level.Bonus,
            _ when level.Type.StartsWith("percent_from_bonus_amount", StringComparison.Ordinal) => level.Bonus * Math.Min(ratio, 1),
            _ => 0, // проценты от продаж и фонда в октябрьских KPI не встречаются — не выдумываем
        };
    }

    /// <summary>
    /// Доля прошедшего месяца для прогноза: прошедшие дни (с долей неполного сегодняшнего — SalesMath.ElapsedDays) ÷ дней в месяце;
    /// прошедших дней нет — 1. Идёт ли месяц, решает не доля, а закрыт ли он: последний день месяца, пока он идёт, — ещё не итог.
    /// </summary>
    public static decimal MonthShare(decimal elapsedDays, int daysInMonth) =>
        daysInMonth <= 0 || elapsedDays <= 0 ? 1 : Math.Min(1, elapsedDays / daysInMonth);

    /// <summary>Цвет выполнения: от 100% — зелёный, от порога премии — оранжевый, ниже — красный (без бонуса всё ниже 100% красное).</summary>
    public static string Tone(decimal ratio, decimal? threshold) => ratio >= 1 ? "ok" : threshold is { } t && ratio >= t ? "warn" : "bad";
}
