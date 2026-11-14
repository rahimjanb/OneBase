using System.Text;
using OneBase.Application.Analytik.Compute;

namespace OneBase.Application.Analytik.Research;

/// <summary>
/// Текст исследования по структуре ТЗ: краткий вывод, показатели, закономерности, корреляции, значимость, причины и гипотезы, различия, риски,
/// рекомендации, ограничения, источники. У каждого вывода — его уровень доказательности словами. Это и опора для модели (числа ответа — только
/// отсюда), и ответ без модели, когда она недоступна.
/// </summary>
public static class ResearchBrief
{
    private static readonly (string Section, string Title)[] Sections =
    [
        ("summary", "Краткий вывод"),
        ("metrics", "Основные показатели"),
        ("patterns", "Найденные закономерности"),
        ("correlations", "Корреляции"),
        ("significance", "Статистическая значимость"),
        ("causes", "Причины и гипотезы"),
        ("hypotheses", "Исследовательские гипотезы"),
        ("differences", "Региональные, товарные и другие различия"),
        ("risks", "Риски"),
        ("recommendations", "Рекомендации"),
    ];

    public static string Tag(string level) => level switch
    {
        EvidenceLevels.Fact => "факт",
        EvidenceLevels.Calculated => "расчёт",
        EvidenceLevels.Statistical => "статистически подтверждено",
        EvidenceLevels.Correlation => "корреляция",
        EvidenceLevels.Hypothesis => "гипотеза",
        EvidenceLevels.Forecast => "прогноз",
        _ => "не проверено",
    };

    public static string HypothesisStatus(string status) => status switch
    {
        "confirmed" => "подтверждена",
        "partially_confirmed" => "подтверждена частично",
        "rejected" => "не подтверждена",
        _ => "не ясно",
    };

    public static string Render(ResearchResult r, string? link = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"**{r.Title}** — {r.Plan.From:dd.MM.yyyy} — {r.Plan.To:dd.MM.yyyy}" + (r.Status is "partial" ? " (часть данных не получена)" : r.Status is "failed" ? " (данные не получены)" : ""));
        sb.AppendLine();
        foreach (var (section, title) in Sections)
        {
            var items = r.Findings.Where(f => f.Section == section).ToList();
            var hypotheses = section == "causes" ? r.Hypotheses : [];
            if (items.Count == 0 && hypotheses.Count == 0)
            {
                continue;
            }

            sb.AppendLine($"### {title}");
            foreach (var f in items)
            {
                sb.AppendLine($"- {f.Text} *({Tag(f.Level)})*");
            }

            foreach (var h in hypotheses)
            {
                sb.AppendLine($"- Гипотеза «{h.Statement}» — {HypothesisStatus(h.Status)}: {h.Basis} *({Tag(h.Level)})*");
            }

            sb.AppendLine();
        }

        var limits = r.Findings.Where(f => f.Section == "limitations").Select(f => f.Text).Concat(r.Limitations).Distinct().ToList();
        if (limits.Count > 0)
        {
            sb.AppendLine("### Ограничения данных");
            foreach (var l in limits)
            {
                sb.AppendLine($"- {l}");
            }

            sb.AppendLine();
        }

        if (r.Sources.Count > 0)
        {
            sb.AppendLine("### Источники");
            foreach (var s in r.Sources)
            {
                sb.AppendLine($"- {s.Title}{(s.DataThrough is { } d ? $", данные по {d:dd.MM.yyyy}" : "")}{(s.SyncedAt is { } at ? $", загружено {at.ToOffset(TimeSpan.FromHours(5)):dd.MM.yyyy HH:mm}" : "")}");
            }

            sb.AppendLine();
        }

        if (link is not null)
        {
            sb.AppendLine($"Подробности исследования (план, операции, таблицы): {link}");
        }

        return sb.ToString().TrimEnd();
    }
}
