using System.Globalization;
using System.Text.RegularExpressions;
using OneBase.Application.Recruitment.Documents;
using OneBase.Domain.Recruitment;

namespace OneBase.Application.Recruitment.Screening;

/// <summary>Критерий AI-скрининга: код, название, вес (максимум баллов).</summary>
public sealed record ScreeningCriterionDef(string Code, string Name, int Max, string Hint);

/// <summary>
/// Правила AI-скрининга — чистые функции без AI и базы (тест: ScreeningRulesTests): шесть критериев с весами, категории, рекомендация,
/// баллы критерия на сервере (не указано в резюме — нейтральная половина; без подтверждённой цитаты — не выше половины), статус скрининга из
/// этапа и отметки HR, скрытие защищённых признаков в тексте перед отправкой модели.
/// </summary>
public static partial class ScreeningRules
{
    /// <summary>Версия схемы анализа в CandidateScores.Version.</summary>
    public const int Version = 2;

    public static readonly IReadOnlyList<ScreeningCriterionDef> Criteria =
    [
        new("experience", "Опыт работы", 25, "релевантный опыт: должности, сферы, стаж, задачи"),
        new("skills", "Профессиональные навыки", 25, "навыки и инструменты, нужные вакансии"),
        new("requirements", "Соответствие требованиям вакансии", 20, "обязательные требования и критерии вакансии: выполнены ли"),
        new("achievements", "Достижения", 15, "измеримые результаты, рост, награды"),
        new("education", "Образование и квалификация", 10, "образование, курсы, сертификаты, нужные вакансии"),
        new("extras", "Дополнительные преимущества", 5, "языки, права, готовность к условиям, полезное сверх требований"),
    ];

    /// <summary>Статусы ответа модели по критерию: соответствует, частично, не соответствует, не указано в резюме.</summary>
    public static readonly string[] Statuses = ["met", "partial", "not_met", "not_stated"];

    public const string NotStated = "Не указано в резюме";

    /// <summary>Категория по итогу: 90–100 отличный, 80–89 очень хороший, 70–79 подходит, 50–69 частично, 0–49 низкое соответствие.</summary>
    public static string Category(int overall) => overall switch
    {
        >= 90 => "excellent",
        >= 80 => "very_good",
        >= 70 => "good",
        >= 50 => "partial",
        _ => "low",
    };

    public static string CategoryName(string category) => category switch
    {
        "excellent" => "Отличный кандидат",
        "very_good" => "Очень хороший кандидат",
        "good" => "Подходит",
        "partial" => "Частично подходит",
        _ => "Низкое соответствие",
    };

    /// <summary>Код рекомендации — из категории (единообразно на сервере, модель его не выбирает).</summary>
    public static string Recommendation(string category) => category switch
    {
        "excellent" => "strong_match",
        "very_good" => "good_match",
        "good" => "match",
        "partial" => "partial_match",
        _ => "low_match",
    };

    /// <summary>
    /// Баллы критерия на сервере. «Не указано в резюме» — половина веса: отсутствие сведений не означает плохого результата. Сказано, но нет
    /// подтверждённой цитаты — не выше половины: высокий балл без фактов из резюме невозможен. Возвращает баллы (с точностью 0,5) и урезан ли.
    /// </summary>
    public static (decimal Points, bool Capped) Points(int max, string status, decimal? aiPoints, bool verified)
    {
        var half = Half(max);
        if (status == "not_stated")
        {
            return (half, false);
        }

        var points = Math.Clamp(RoundHalf(aiPoints ?? 0), 0, max);
        return !verified && points > half ? (half, true) : (points, false);
    }

    public static decimal Half(int max) => RoundHalf(max / 2m);

    private static decimal RoundHalf(decimal x) => Math.Round(x * 2, MidpointRounding.AwayFromZero) / 2;

    /// <summary>Итог 0–100 — сумма баллов критериев, округлённая.</summary>
    public static int Overall(IEnumerable<decimal> points) => (int)Math.Clamp(Math.Round(points.Sum(), MidpointRounding.AwayFromZero), 0, 100);

    /// <summary>Покрытие фактами: доля веса критериев с подтверждённой цитатой (0–1). Мало фактов (меньше 0,4) — в карточке «мало данных».</summary>
    public static decimal Coverage(IEnumerable<(int Max, bool Verified)> criteria) =>
        Math.Round(criteria.Where(c => c.Verified).Sum(c => c.Max) / 100m, 2);

    public const decimal LowCoverage = 0.4m;

    // ---------- Статус скрининга ----------

    /// <summary>Статусы скрининга: новый, анализируется, подходит, рекомендован, на собеседовании, отказ, принят на работу, в резерве.</summary>
    public static readonly string[] StatusCodes = ["new", "analyzing", "suitable", "recommended", "interview", "rejected", "hired", "reserve"];

    public static string StatusName(string status) => status switch
    {
        "analyzing" => "Анализируется",
        "suitable" => "Подходит",
        "recommended" => "Рекомендован",
        "interview" => "На собеседовании",
        "rejected" => "Отказ",
        "hired" => "Принят на работу",
        "reserve" => "В резерве",
        _ => "Новый",
    };

    /// <summary>
    /// Статус из этапа и отметки HR: закрытый отклик — принят или отказ; отметка HR (подходит, рекомендован, в резерве) — она (любая смена этапа
    /// её снимает, так что это последнее действие); собеседование или оффер — «на собеседовании»; идёт анализ — «анализируется»; иначе — «новый».
    /// </summary>
    public static string Status(ApplicationStage stage, ApplicationMark mark, bool analyzing) => stage switch
    {
        ApplicationStage.Hired => "hired",
        ApplicationStage.Rejected or ApplicationStage.Withdrawn => "rejected",
        _ when mark == ApplicationMark.Reserve => "reserve",
        _ when mark == ApplicationMark.Recommended => "recommended",
        _ when mark == ApplicationMark.Suitable => "suitable",
        ApplicationStage.Interview or ApplicationStage.Offer => "interview",
        _ when analyzing => "analyzing",
        _ => "new",
    };

    /// <summary>
    /// Что сделать при выборе статуса HR: этап (null — не менять) и отметка. «Подходит», «рекомендован», «в резерве» — отметка; новый или
    /// закрытый отклик при этом — в «Отбор» (на собеседовании и оффер остаются). «Анализируется» — не выбирается (его ставит очередь).
    /// </summary>
    public static (ApplicationStage? Stage, ApplicationMark Mark) Apply(string status, ApplicationStage current) => status switch
    {
        "new" => (current == ApplicationStage.New ? null : ApplicationStage.New, ApplicationMark.None),
        "suitable" or "recommended" or "reserve" => (current is ApplicationStage.Interview or ApplicationStage.Offer or ApplicationStage.Screening ? null : ApplicationStage.Screening,
            status switch { "suitable" => ApplicationMark.Suitable, "recommended" => ApplicationMark.Recommended, _ => ApplicationMark.Reserve }),
        "interview" => (current is ApplicationStage.Interview or ApplicationStage.Offer ? null : ApplicationStage.Interview, ApplicationMark.None),
        "rejected" => (current == ApplicationStage.Rejected ? null : ApplicationStage.Rejected, ApplicationMark.None),
        "hired" => (current == ApplicationStage.Hired ? null : ApplicationStage.Hired, ApplicationMark.None),
        _ => throw new ArgumentException($"Статус «{status}» не выбирается.", nameof(status)),
    };

    // ---------- Защищённые признаки ----------

    /// <summary>
    /// Строки резюме с защищёнными признаками (дата рождения, возраст, пол, семейное положение, дети, национальность, религия) — значение
    /// заменяется на «[скрыто]» до отправки модели: оценка не должна на них опираться. Возвращает текст и сколько строк скрыто.
    /// </summary>
    public static (string Text, int Hidden) HideProtected(string text)
    {
        var hidden = 0;
        var result = ProtectedLine().Replace(text, m =>
        {
            hidden++;
            return $"{m.Groups["label"].Value}{m.Groups["sep"].Value} [скрыто]";
        });
        result = AgeMention().Replace(result, m =>
        {
            hidden++;
            return "[возраст скрыт]";
        });
        return (result, hidden);
    }

    [GeneratedRegex(@"(?im)^(?<label>[ \t\-•*]*(?:дата\s+рождения|год\s+рождения|возраст|пол|семейное\s+положение|дети|национальность|вероисповедание|религия|"
                    + @"tug['ʻ’`]?ilgan\s+(?:sana|yil)|yosh[i]?|jinsi|oilaviy\s+(?:holati|ahvoli)|farzand(?:lar)?(?:i)?|millati|dini|date\s+of\s+birth|"
                    + @"birth\s*date|age|gender|sex|marital\s+status|children|nationality|religion))(?<sep>\s*[:\-–—])[^\n]*$")]
    private static partial Regex ProtectedLine();

    /// <summary>«Мужчина, 34 года», «Женщина, 28 лет, родилась 1 мая 1996» — шапка резюме HH и похожих.</summary>
    [GeneratedRegex(@"(?i)\b(?:мужчина|женщина|erkak|ayol|male|female)\s*,\s*\d{2}\s*(?:года|год|лет|yosh|years?)(?:\s*,\s*родил(?:ся|ась)\s+[^\n,]*)?")]
    private static partial Regex AgeMention();

    // ---------- Цитаты ----------

    /// <summary>Источник для проверки цитат: нижний регистр, без пробелов, кавычек и знаков (как у разбора резюме).</summary>
    public static string NormalizeSource(string text) => ResumeProfiles.Normalize(text);

    /// <summary>Цитата подтверждена, если она есть в источнике (без учёта регистра, пробелов, кавычек и знаков) и не короче 3 значимых символов.</summary>
    public static bool Found(string normalizedSource, string? quote)
    {
        var needle = ResumeProfiles.Normalize(quote ?? string.Empty);
        return needle.Length >= 3 && normalizedSource.Contains(needle, StringComparison.Ordinal);
    }

    // ---------- Сводка кандидата из разбора резюме ----------

    /// <summary>Стаж в месяцах из поля резюме («6», «6 лет», «3 г. 4 мес.», «2,5»); не понять — null.</summary>
    public static int? ExperienceMonths(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var years = YearsPart().Match(value);
        var months = MonthsPart().Match(value);
        decimal total = 0;
        if (years.Success && decimal.TryParse(years.Groups[1].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var y))
        {
            total += y * 12;
        }

        if (months.Success && int.TryParse(months.Groups[1].Value, out var m))
        {
            total += m;
        }

        if (!years.Success && !months.Success && decimal.TryParse(value.Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var plain))
        {
            total = plain * 12;
        }

        return total is > 0 and < 12 * 70 ? (int)Math.Round(total) : null;
    }

    [GeneratedRegex(@"(\d+(?:[.,]\d+)?)\+?\s*(?:г(?:од|ода|\.)?|лет|yil|years?|y\b)", RegexOptions.IgnoreCase)]
    private static partial Regex YearsPart();

    [GeneratedRegex(@"(\d+)\s*(?:мес|oy|months?|m\b)", RegexOptions.IgnoreCase)]
    private static partial Regex MonthsPart();

    /// <summary>Навык для фильтра: нижний регистр, одинарные пробелы, без точек и кавычек по краям; длиннее 100 — обрезан.</summary>
    public static string? NormalizeSkill(string? skill)
    {
        var s = Spaces().Replace((skill ?? string.Empty).Trim().Trim('.', ',', ';', '"', '«', '»', '\'').ToLowerInvariant().Replace('ё', 'е'), " ");
        return s.Length is >= 1 and <= 100 ? s : s.Length > 100 ? s[..100] : null;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
