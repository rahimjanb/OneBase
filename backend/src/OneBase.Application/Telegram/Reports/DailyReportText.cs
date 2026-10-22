using System.Text;

namespace OneBase.Application.Telegram.Reports;

/// <summary>Картинки отчёта: PNG по страницам (на телефоне читаются без увеличения).</summary>
public interface IDailyReportRenderer
{
    IReadOnlyList<byte[]> Render(DailyReport report);
}

/// <summary>
/// Текст отчёта: подпись к картинкам (коротко) и материал чата для Telegram-агента (полностью) — чтобы на «что в отчёте?» агент отвечал по
/// тому, что ушло в этот чат.
/// </summary>
public static class DailyReportText
{
    /// <summary>«OneBase · План на день · Ташкентский регион · 23.10.2026 — план 300 млн сум, выполнение 40%».</summary>
    public static string Caption(DailyReport r)
    {
        var lang = r.Language;
        var supervisor = r.Teams.Count == 1 ? $" · {r.Teams[0].Supervisor}" : "";
        var head = $"OneBase · {DailyReportTexts.Name(r.Kind, lang)} · {r.Region}{supervisor} · {r.Date:dd.MM.yyyy}";
        if (r.Demo)
        {
            head = $"{DailyReportTexts.Get(lang, "ТЕСТОВЫЕ ДАННЫЕ")} · {head}";
        }

        var parts = new List<string>();
        if (r.Total.Plan is { } plan)
        {
            parts.Add($"{DailyReportTexts.Get(lang, "План")} {ReportFormat.Short(plan, r.Metric, lang)}");
        }

        if (r.HasFact && r.Total.Fact is { } fact)
        {
            parts.Add($"{DailyReportTexts.Get(lang, "Факт")} {ReportFormat.Short(fact, r.Metric, lang)}");
            if (r.Total.Share is { } share)
            {
                parts.Add($"{DailyReportTexts.Get(lang, "Выполнение")} {ReportFormat.Percent(share)}");
            }
        }

        return parts.Count == 0 ? head : $"{head}\n{string.Join(" · ", parts)}";
    }

    /// <summary>Кратко для журнала: «План 300 млн сум · факт 120 млн сум (40%)».</summary>
    public static string Summary(DailyReport r)
    {
        var lang = r.Language;
        var plan = r.Total.Plan is { } p ? ReportFormat.Short(p, r.Metric, lang) : DailyReportTexts.Get(lang, "нет данных");
        if (!r.HasFact)
        {
            return $"{DailyReportTexts.Get(lang, "План")} {plan} · {r.Supervisors} / {r.Total.Agents}";
        }

        var fact = r.Total.Fact is { } f ? ReportFormat.Short(f, r.Metric, lang) : DailyReportTexts.Get(lang, "нет данных");
        return $"{DailyReportTexts.Get(lang, "План")} {plan} · {DailyReportTexts.Get(lang, "Факт")} {fact} ({ReportFormat.Percent(r.Total.Share)})";
    }

    /// <summary>Весь отчёт обычным текстом: команды, агенты, итоги, внимание, источники.</summary>
    public static string Plain(DailyReport r)
    {
        var lang = r.Language;
        string T(string ru) => DailyReportTexts.Get(lang, ru);
        string A(decimal? v) => v is { } x ? ReportFormat.Amount(x, r.Metric) : T("нет данных");
        string V(int? v) => v?.ToString() ?? T("нет данных");
        var unit = DailyReportTexts.Unit(r.Metric, lang);
        var sb = new StringBuilder();
        sb.AppendLine(Caption(r));
        if (r.Manager is { } manager)
        {
            sb.AppendLine($"{T("РМ")}: {manager}");
        }

        sb.AppendLine($"{T("Супервайзеров")}: {r.Supervisors}; {T("Агентов")}: {r.Total.Agents}");
        sb.AppendLine($"{T("План продаж")}: {A(r.Total.Plan)} {unit}; {T("План визитов")}: {V(r.Total.VisitPlan)}");
        if (r.HasFact)
        {
            sb.AppendLine($"{T("Факт продаж")}: {A(r.Total.Fact)} {unit}; {T("Выполнение")}: {ReportFormat.Percent(r.Total.Share)}; {T("Отклонение")}: " +
                          $"{(r.Total.Deviation is { } d ? ReportFormat.Signed(d, r.Metric) : T("нет данных"))}; {T("Визиты: факт / план")}: {V(r.Total.VisitFact)} / {V(r.Total.VisitPlan)}");
        }

        foreach (var team in r.Teams)
        {
            sb.AppendLine();
            sb.AppendLine($"{T("Супервайзер")}: {team.Supervisor}{(team.TeamName is { } name && !team.Supervisor.Contains(name, StringComparison.Ordinal) ? $" ({name})" : "")}");
            foreach (var a in team.Agents)
            {
                sb.Append($"- {a.Name}: {T("План")} {A(a.Plan)}");
                if (r.HasFact)
                {
                    sb.Append($", {T("Факт")} {A(a.Fact)}, {ReportFormat.Percent(a.Share)}");
                    sb.Append($", {T("Визиты")} {V(a.VisitFact)}/{V(a.VisitPlan)}");
                }
                else
                {
                    sb.Append($", {T("План визитов")} {V(a.VisitPlan)}");
                }

                if (a.Note is { } note)
                {
                    sb.Append($" — {note}");
                }

                sb.AppendLine();
            }

            sb.AppendLine($"{T("Итого по команде")}: {T("План")} {A(team.Totals.Plan)}" +
                          (r.HasFact ? $", {T("Факт")} {A(team.Totals.Fact)}, {ReportFormat.Percent(team.Totals.Share)}" : "") +
                          $", {T("Визиты")} {(r.HasFact ? $"{V(team.Totals.VisitFact)}/" : "")}{V(team.Totals.VisitPlan)}");
        }

        if (r.Best.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"{T("Лучшие результаты")}: " + string.Join("; ", r.Best.Select(b => $"{b.Name} {ReportFormat.Percent(b.Share)}")));
        }

        if (r.Attention.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine((r.Kind == OneBase.Domain.Telegram.TelegramReportKind.Evening ? T("Не выполнили план") : T("Требуют внимания")) + ":");
            foreach (var a in r.Attention)
            {
                sb.AppendLine($"- {a.Name} ({a.Team}): {a.Reason}");
            }
        }

        foreach (var note in r.Notes)
        {
            sb.AppendLine(note);
        }

        return sb.ToString();
    }
}
