using OneBase.Domain.Telegram;

namespace OneBase.Application.Telegram.Reports;

/// <summary>Демонстрационный отчёт: название региона, сколько супервайзеров и агентов в команде.</summary>
public sealed record DemoReportInput(string? Region, int Supervisors, int AgentsPerTeam);

/// <summary>
/// Демонстрационные данные для предпросмотра и тестовой отправки: «Супервайзер 1», «Агент 1»… и правдоподобные, но вымышленные цифры.
/// Реальных имён и показателей в них нет; на картинке — «ТЕСТОВЫЕ ДАННЫЕ». Одинаковый вход — одинаковые цифры (утро и вечер сходятся).
/// </summary>
public static class DailyReportDemo
{
    public const int MaxSupervisors = 8;
    public const int MaxAgentsPerTeam = 25;

    public static DailyReport Build(TelegramReportKind kind, DemoReportInput input, ReportMetric metric, string language, DateOnly date, DateTimeOffset now)
    {
        var supervisors = Math.Clamp(input.Supervisors, 1, MaxSupervisors);
        var perTeam = Math.Clamp(input.AgentsPerTeam, 1, MaxAgentsPerTeam);
        var region = string.IsNullOrWhiteSpace(input.Region) ? (language == "uz" ? "Toshkent hududi" : "Ташкентский регион") : input.Region.Trim();
        if (region.Length > 80)
        {
            region = region[..80];
        }

        var random = new Random(Seed(region, supervisors, perTeam, metric, date));
        var teams = new List<ReportTeam>();
        var agentNo = 1;
        for (var s = 1; s <= supervisors; s++)
        {
            var agents = new List<ReportAgentRow>();
            for (var a = 0; a < perTeam; a++, agentNo++)
            {
                // Все случайные числа берутся всегда, в одном порядке: утро, обед и вечер одного дня — с одинаковыми планами.
                // Сумы: 25–40 млн с шагом 500 тыс.; кг: 300–900 с шагом 10.
                decimal plan = metric == ReportMetric.Kg ? random.Next(30, 91) * 10 : random.Next(50, 81) * 500_000;
                var visitPlan = random.Next(8, 15);
                var midday = random.Next(20, 75) / 100m;
                var evening = random.Next(60, 130) / 100m;
                var skipped = random.Next(1, 4);
                var lessOrders = random.Next(0, 3);

                // Один агент в демо — без плана: видно, как отчёт показывает недостающие данные; второй — без заказов («требует внимания»).
                var noPlan = s == supervisors && a == perTeam - 1 && agentNo > 3;
                var progress = agentNo == 2 ? 0 : kind switch
                {
                    TelegramReportKind.Midday => midday,
                    TelegramReportKind.Evening => evening,
                    _ => 0m,
                };

                var step = metric == ReportMetric.Kg ? 1m : 1000m;
                decimal? fact = kind == TelegramReportKind.Morning ? null : Math.Round(plan * progress / step) * step;
                int? visitFact = kind == TelegramReportKind.Morning ? null : progress == 0 ? 0 : Math.Min(visitPlan + 2, (int)Math.Round(visitPlan * Math.Min(progress + 0.15m, 1.1m)));
                var orders = fact is > 0 ? Math.Max(1, (visitFact ?? 0) - lessOrders) : 0;
                _ = skipped; // число берётся всегда: порядок случайных чисел не меняется
                agents.Add(new ReportAgentRow($"{DailyReportTexts.Get(language, "Агент")} {agentNo}", noPlan ? null : plan, fact, visitPlan, visitFact, orders));
            }

            teams.Add(new ReportTeam($"{DailyReportTexts.Get(language, "Супервайзер")} {s}", null, agents, DailyReportMath.Totals(agents)));
        }

        var total = DailyReportMath.Combine(teams.Select(t => t.Totals).ToList());
        string T(string ru) => DailyReportTexts.Get(language, ru);
        var notes = teams.SelectMany(t => t.Agents).Where(a => a.Plan is null).Select(a => a.Name).ToList() is { Count: > 0 } missing
            ? new[] { DailyReportTexts.Format(language, "Нет плана у агентов: {0}", string.Join(", ", missing)) }
            : [];
        return new DailyReport(kind, date, now, region, $"{T("Региональный менеджер")} 1", null, metric, language, Demo: true, Check: false, teams, total,
            [
                new ReportSource(T("Планы — Linko (staff_balance), месячный ÷ рабочие дни (пн–сб)"), "Linko", now),
                new ReportSource(T("Продажи — заказы, принятые агентом за день (Linko)"), "Linko", now),
            ],
            notes,
            kind == TelegramReportKind.Evening ? DailyReportMath.Best(teams) : [],
            DailyReportMath.Attention(kind, teams, T, metric),
            kind == TelegramReportKind.Morning ? null : now);
    }

    /// <summary>Свой хэш, а не HashCode: тот меняется от запуска к запуску процесса.</summary>
    private static int Seed(string region, int supervisors, int perTeam, ReportMetric metric, DateOnly date)
    {
        unchecked
        {
            var h = 17;
            foreach (var c in region)
            {
                h = h * 31 + c;
            }

            return ((h * 31 + supervisors) * 31 + perTeam) * 31 + (int)metric + date.DayNumber;
        }
    }
}
