using System.Globalization;
using OneBase.Domain.Telegram;

namespace OneBase.Application.Telegram.Reports;

/// <summary>В чём план и факт: сумы (план sales_sum Linko, сумма заказов) или килограммы (product_sales_weight, вес заказов).</summary>
public enum ReportMetric
{
    Sum,
    Kg,
}

/// <summary>
/// Строка агента. null — показателя нет в источнике (плана нет в Linko, план визитов не загружен, утром факта ещё нет): на картинке —
/// «нет данных», а не ноль, и в итоги он не входит.
/// </summary>
public sealed record ReportAgentRow(
    string Name,
    decimal? Plan,
    decimal? Fact,
    int? VisitPlan,
    int? VisitFact,
    int Orders,
    string? Note = null)
{
    public decimal? Share => DailyReportMath.Share(Fact, Plan);
    public decimal? Deviation => DailyReportMath.Deviation(Fact, Plan);
}

/// <summary>Итог команды или региона: суммы только известных значений; AgentsWithoutPlan — сколько агентов в план не вошло.</summary>
public sealed record ReportTotals(decimal? Plan, decimal? Fact, int? VisitPlan, int? VisitFact, int Agents, int AgentsWithoutPlan)
{
    public decimal? Share => DailyReportMath.Share(Fact, Plan);
    public decimal? Deviation => DailyReportMath.Deviation(Fact, Plan);
}

public sealed record ReportTeam(string Supervisor, string? TeamName, IReadOnlyList<ReportAgentRow> Agents, ReportTotals Totals);

/// <summary>Источник показателя и свежесть его данных («Linko: данные на 23.10 11:02»).</summary>
public sealed record ReportSource(string What, string Source, DateTimeOffset? AsOf);

/// <summary>Лучший результат дня (вечер): только агенты с планом, по выполнению.</summary>
public sealed record ReportHighlight(string Name, string Team, decimal Share, decimal Fact);

/// <summary>Кто требует внимания (обед) или не выполнил план (вечер) — и зафиксированная в системе причина, если она есть.</summary>
public sealed record ReportAttention(string Name, string Team, string Reason);

/// <summary>
/// Отчёт для картинки: регион (или команда региона), команды супервайзеров с агентами, итоги, источники данных. Demo — демонстрационные
/// данные (на картинке «ТЕСТОВЫЕ ДАННЫЕ»), Check — проверочная отправка на реальных данных.
/// </summary>
public sealed record DailyReport(
    TelegramReportKind Kind,
    DateOnly Date,
    DateTimeOffset GeneratedAt,
    string Region,
    string? Manager,
    string? TeamScope,
    ReportMetric Metric,
    string Language,
    bool Demo,
    bool Check,
    IReadOnlyList<ReportTeam> Teams,
    ReportTotals Total,
    IReadOnlyList<ReportSource> Sources,
    IReadOnlyList<string> Notes,
    IReadOnlyList<ReportHighlight> Best,
    IReadOnlyList<ReportAttention> Attention,
    DateTimeOffset? DataAsOf = null)
{
    /// <summary>Утром факта нет: только цели дня.</summary>
    public bool HasFact => Kind != TelegramReportKind.Morning;

    public int Supervisors => Teams.Count;
}

/// <summary>
/// Формулы отчёта: выполнение = факт ÷ план × 100% (план 0 или неизвестен — выполнения нет, без деления на ноль), отклонение = факт − план.
/// Итог команды — сумма её агентов, итог региона — сумма команд (агент входит один раз); неизвестные значения в сумму не входят.
/// </summary>
public static class DailyReportMath
{
    /// <summary>Обед: ниже этой доли дневного плана — «требует внимания» (порог печатается на картинке).</summary>
    public const decimal MiddayAttentionShare = 0.5m;

    /// <summary>«Лучшие результаты» — только если агентов с планом не меньше: иначе рейтинг ничего не говорит.</summary>
    public const int MinAgentsForRating = 3;

    public static decimal? Share(decimal? fact, decimal? plan) => fact is { } f && plan is > 0 ? f / plan.Value : null;

    public static decimal? Deviation(decimal? fact, decimal? plan) => fact is { } f && plan is { } p ? f - p : null;

    public static ReportTotals Totals(IReadOnlyCollection<ReportAgentRow> agents) => new(
        SumKnown(agents.Select(a => a.Plan)),
        SumKnown(agents.Select(a => a.Fact)),
        SumKnown(agents.Select(a => a.VisitPlan)),
        SumKnown(agents.Select(a => a.VisitFact)),
        agents.Count,
        agents.Count(a => a.Plan is not > 0));

    /// <summary>Итог региона — из итогов команд, а не повторным проходом по агентам: так видно, что он равен сумме команд.</summary>
    public static ReportTotals Combine(IReadOnlyCollection<ReportTotals> teams) => new(
        SumKnown(teams.Select(t => t.Plan)),
        SumKnown(teams.Select(t => t.Fact)),
        SumKnown(teams.Select(t => t.VisitPlan)),
        SumKnown(teams.Select(t => t.VisitFact)),
        teams.Sum(t => t.Agents),
        teams.Sum(t => t.AgentsWithoutPlan));

    private static decimal? SumKnown(IEnumerable<decimal?> values)
    {
        var known = values.Where(v => v is not null).Select(v => v!.Value).ToList();
        return known.Count == 0 ? null : known.Sum();
    }

    private static int? SumKnown(IEnumerable<int?> values)
    {
        var known = values.Where(v => v is not null).Select(v => v!.Value).ToList();
        return known.Count == 0 ? null : known.Sum();
    }

    /// <summary>
    /// Сверка перед отправкой: итог каждой команды = сумме её агентов, итог региона = сумме команд и сумме всех агентов. Пусто — сходится;
    /// иначе отчёт не отправляется (ошибка в журнале).
    /// </summary>
    public static IReadOnlyList<string> Verify(DailyReport report)
    {
        var problems = new List<string>();
        foreach (var team in report.Teams)
        {
            if (Totals(team.Agents) != team.Totals)
            {
                problems.Add($"Итог команды «{team.Supervisor}» не равен сумме её агентов.");
            }
        }

        if (Combine(report.Teams.Select(t => t.Totals).ToList()) != report.Total)
        {
            problems.Add("Итог региона не равен сумме команд.");
        }

        if (Totals(report.Teams.SelectMany(t => t.Agents).ToList()) != report.Total)
        {
            problems.Add("Итог региона не равен сумме агентов.");
        }

        return problems;
    }

    /// <summary>Лучшие по выполнению дневного плана (вечер): агенты с планом; при равенстве — больший факт.</summary>
    public static IReadOnlyList<ReportHighlight> Best(IReadOnlyList<ReportTeam> teams, int take = 3)
    {
        var rated = teams.SelectMany(t => t.Agents.Select(a => (Team: t, Agent: a))).Where(x => x.Agent.Share is not null).ToList();
        if (rated.Count < MinAgentsForRating)
        {
            return [];
        }

        return rated.OrderByDescending(x => x.Agent.Share).ThenByDescending(x => x.Agent.Fact).Take(take)
            .Select(x => new ReportHighlight(x.Agent.Name, x.Team.Supervisor, x.Agent.Share!.Value, x.Agent.Fact ?? 0))
            .ToList();
    }

    /// <summary>
    /// Обед: нет ни заказов, ни визитов — или выполнение ниже <see cref="MiddayAttentionShare"/>. Вечер: план не выполнен (с причиной из
    /// системы, если она есть). Агенты без плана сюда не попадают по выполнению — только по отсутствию работы.
    /// </summary>
    public static IReadOnlyList<ReportAttention> Attention(TelegramReportKind kind, IReadOnlyList<ReportTeam> teams, Func<string, string> t, ReportMetric metric = ReportMetric.Sum)
    {
        var list = new List<(decimal Order, ReportAttention Row)>();
        foreach (var team in teams)
        {
            foreach (var a in team.Agents)
            {
                if (kind == TelegramReportKind.Midday)
                {
                    if (a.Orders == 0 && (a.Fact ?? 0) == 0 && a.VisitFact is null or 0)
                    {
                        list.Add((-1, new ReportAttention(a.Name, team.Supervisor, t("нет заказов и визитов"))));
                    }
                    else if (a.Share is { } s && s < MiddayAttentionShare)
                    {
                        list.Add((s, new ReportAttention(a.Name, team.Supervisor, string.Format(CultureInfo.InvariantCulture, t("{0}% дневного плана"), Percent(s)))));
                    }
                }
                else if (kind == TelegramReportKind.Evening && a.Share is { } s && s < 1)
                {
                    // Причин невыполнения в данных нет — только цифры (и пометка, если она есть); причины не придумываются.
                    var text = $"{Percent(s)}% · {ReportFormat.Signed(a.Deviation ?? 0, metric)}";
                    list.Add((s, new ReportAttention(a.Name, team.Supervisor, string.IsNullOrWhiteSpace(a.Note) ? text : $"{text} · {a.Note}")));
                }
            }
        }

        return list.OrderBy(x => x.Order).Select(x => x.Row).ToList();
    }

    public static int Percent(decimal share) => (int)Math.Round(share * 100, MidpointRounding.AwayFromZero);
}

/// <summary>Числа на картинке: сумы с пробелами между разрядами, проценты, отклонение со знаком.</summary>
public static class ReportFormat
{
    private const char Group = ' ';

    /// <summary>300 000 000 — целые, неразрывные пробелы между разрядами.</summary>
    public static string Number(decimal value)
    {
        var rounded = Math.Round(value, 0, MidpointRounding.AwayFromZero);
        var text = Math.Abs(rounded).ToString("#,0", CultureInfo.InvariantCulture).Replace(',', Group);
        return rounded < 0 ? "−" + text : text;
    }

    /// <summary>Значение показателя: сумы — целые, кг — целые (меньше 10 кг — с десятой).</summary>
    public static string Amount(decimal value, ReportMetric metric) =>
        metric == ReportMetric.Kg && Math.Abs(value) is > 0 and < 10
            ? Math.Round(value, 1, MidpointRounding.AwayFromZero).ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',')
            : Number(value);

    /// <summary>+12 500 000 / −3 000 000.</summary>
    public static string Signed(decimal value, ReportMetric metric)
    {
        var text = Amount(Math.Abs(value), metric);
        return Math.Round(value, metric == ReportMetric.Kg ? 1 : 0) switch
        {
            > 0 => "+" + text,
            < 0 => "−" + text,
            _ => text,
        };
    }

    public static string Percent(decimal? share) => share is { } s ? $"{DailyReportMath.Percent(s)}%" : "—";

    /// <summary>Коротко для подписи и журнала: 35,4 млн / 1,2 млрд / 540 тыс.</summary>
    public static string Short(decimal value, ReportMetric metric, string language)
    {
        var uz = language == "uz";
        if (metric == ReportMetric.Kg)
        {
            return Number(value) + (uz ? " kg" : " кг");
        }

        var abs = Math.Abs(value);
        var (div, unit) = abs >= 1_000_000_000m ? (1_000_000_000m, uz ? "mlrd" : "млрд")
            : abs >= 1_000_000m ? (1_000_000m, uz ? "mln" : "млн")
            : abs >= 1_000m ? (1_000m, uz ? "ming" : "тыс.")
            : (1m, "");
        var number = Math.Round(value / div, div == 1 ? 0 : 1, MidpointRounding.AwayFromZero).ToString(div == 1 ? "0" : "0.#", CultureInfo.InvariantCulture).Replace('.', ',');
        return unit.Length == 0 ? number + (uz ? " so'm" : " сум") : $"{number} {unit} {(uz ? "so'm" : "сум")}";
    }
}
