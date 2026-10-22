using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Telegram;
using OneBase.Infrastructure.Telegram.Reports;
using SkiaSharp;

namespace OneBase.AI.Tests;

/// <summary>
/// Автоотчёты в Telegram без базы: формулы (выполнение, отклонение, план 0 и неизвестный), итоги команд и региона и их сверка, демонстрационные
/// данные, расписание (время, дни, часовой пояс, догон после простоя, «взвод» при включении), картинки PNG 16:9
/// (размер, страницы, кириллица и узбекская латиница, длинные имена).
/// </summary>
public class TelegramReportTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 8, 0, 0, TimeSpan.FromHours(5));

    private static ReportAgentRow Agent(string name, decimal? plan, decimal? fact = null, int? visitPlan = 10, int? visitFact = null, int orders = 0) =>
        new(name, plan, fact, visitPlan, visitFact, orders);

    private static DailyReport Report(TelegramReportKind kind, params ReportTeam[] teams) =>
        new(kind, new DateOnly(2026, 10, 23), Now, "Ташкентский регион", "Иванов", null, ReportMetric.Sum, "ru", false, false, teams,
            DailyReportMath.Combine(teams.Select(t => t.Totals).ToList()), [], [], [], []);

    private static ReportTeam Team(string supervisor, params ReportAgentRow[] agents) => new(supervisor, null, agents, DailyReportMath.Totals(agents));

    // ---------- Формулы ----------

    [Fact]
    public void Share_and_deviation_never_divide_by_zero_or_invent_missing_values()
    {
        Assert.Equal(0.4m, DailyReportMath.Share(120, 300));
        Assert.Null(DailyReportMath.Share(120, 0));      // план 0 — выполнения нет
        Assert.Null(DailyReportMath.Share(120, null));   // плана нет в источнике
        Assert.Null(DailyReportMath.Share(null, 300));   // факта ещё нет
        Assert.Equal(-180, DailyReportMath.Deviation(120, 300));
        Assert.Null(DailyReportMath.Deviation(null, 300));
        Assert.Equal(40, DailyReportMath.Percent(0.4m));
        Assert.Equal(13, DailyReportMath.Percent(0.125m)); // половина — вверх
    }

    [Fact]
    public void Team_and_region_totals_add_up_and_skip_unknown_values()
    {
        var aziz = Team("Азиз", Agent("Агент 1", 35_000_000, 30_000_000, 10, 8), Agent("Агент 2", null, 5_000_000, null, 4), Agent("Агент 3", 30_000_000, 0, 10, 0));
        var bek = Team("Бек", Agent("Агент 4", 35_000_000, 40_000_000, 12, 12));
        Assert.Equal(65_000_000, aziz.Totals.Plan);       // агент без плана в план команды не входит…
        Assert.Equal(35_000_000, aziz.Totals.Fact);       // …а его факт — входит
        Assert.Equal(20, aziz.Totals.VisitPlan);
        Assert.Equal(1, aziz.Totals.AgentsWithoutPlan);
        Assert.Equal(3, aziz.Totals.Agents);

        var report = Report(TelegramReportKind.Evening, aziz, bek);
        Assert.Equal(100_000_000, report.Total.Plan);      // регион = сумма команд
        Assert.Equal(75_000_000, report.Total.Fact);
        Assert.Equal(-25_000_000, report.Total.Deviation);
        Assert.Equal(0.75m, report.Total.Share);
        Assert.Equal(4, report.Total.Agents);
        Assert.Empty(DailyReportMath.Verify(report));

        // Итог команды, не равный сумме агентов, — отчёт не уходит.
        var broken = report with { Teams = [aziz with { Totals = aziz.Totals with { Plan = 70_000_000 } }, bek] };
        Assert.Contains(DailyReportMath.Verify(broken), p => p.Contains("Азиз"));
        Assert.NotEmpty(DailyReportMath.Verify(report with { Total = report.Total with { Fact = 1 } }));
    }

    [Fact]
    public void Morning_report_has_no_fact_and_unknown_plans_stay_unknown()
    {
        var team = Team("Азиз", Agent("Агент 1", null), Agent("Агент 2", null));
        Assert.Null(team.Totals.Plan); // ни одного плана — «нет данных», а не 0
        Assert.Null(team.Totals.Fact);
        Assert.False(Report(TelegramReportKind.Morning, team).HasFact);
    }

    [Fact]
    public void Attention_and_best_follow_stated_rules()
    {
        string T(string ru) => DailyReportTexts.Get("ru", ru);
        var team = Team("Азиз",
            Agent("Ноль", 30_000_000, 0, 10, 0, orders: 0),
            Agent("Мало", 30_000_000, 9_000_000, 10, 3, orders: 2),
            Agent("Норма", 30_000_000, 20_000_000, 10, 6, orders: 5),
            Agent("Без плана", null, 1_000_000, null, 1, orders: 1));
        var midday = DailyReportMath.Attention(TelegramReportKind.Midday, [team], T);
        Assert.Equal(["Ноль", "Мало"], midday.Select(a => a.Name));
        Assert.Contains("нет заказов и визитов", midday[0].Reason);
        Assert.Contains("30%", midday[1].Reason);

        var evening = DailyReportMath.Attention(TelegramReportKind.Evening, [Team("Азиз", Agent("А", 10, 9) with { Note = "пропущено точек: 2" }, Agent("Б", 10, 5), Agent("В", 10, 12))], T);
        Assert.Equal(["Б", "А"], evening.Select(a => a.Name)); // по выполнению, выполнившие план не попадают
        Assert.Equal("50% · −5", evening[0].Reason); // причин в данных нет — только выполнение и отклонение, без выдуманных причин
        Assert.Contains("пропущено точек: 2", evening[1].Reason);

        // Рейтинг — только когда агентов с планом хватает.
        Assert.Empty(DailyReportMath.Best([Team("Т", Agent("А", 10, 12), Agent("Б", 10, 5))]));
        var best = DailyReportMath.Best([Team("Т", Agent("А", 10, 12), Agent("Б", 10, 5), Agent("В", 10, 15), Agent("Г", null, 99))]);
        Assert.Equal(["В", "А", "Б"], best.Select(b => b.Name));
    }

    [Fact]
    public void Numbers_are_formatted_in_sums_with_group_separators()
    {
        Assert.Equal("300 000 000", ReportFormat.Number(300_000_000));
        Assert.Equal("−180 000 000", ReportFormat.Number(-180_000_000));
        Assert.Equal("+12 500 000", ReportFormat.Signed(12_500_000, ReportMetric.Sum));
        Assert.Equal("−3 000", ReportFormat.Signed(-3_000, ReportMetric.Sum));
        Assert.Equal("4,5", ReportFormat.Amount(4.54m, ReportMetric.Kg));
        Assert.Equal("35,4 млн сум", ReportFormat.Short(35_400_000, ReportMetric.Sum, "ru"));
        Assert.Equal("1,2 mlrd so'm", ReportFormat.Short(1_200_000_000, ReportMetric.Sum, "uz"));
        Assert.Equal("—", ReportFormat.Percent(null));
    }

    // ---------- Демонстрационные данные ----------

    [Fact]
    public void Demo_report_is_marked_deterministic_and_consistent_across_the_day()
    {
        var date = new DateOnly(2026, 10, 23);
        var input = new DemoReportInput(null, 3, 5);
        var morning = DailyReportDemo.Build(TelegramReportKind.Morning, input, ReportMetric.Sum, "ru", date, Now);
        var evening = DailyReportDemo.Build(TelegramReportKind.Evening, input, ReportMetric.Sum, "ru", date, Now);
        var again = DailyReportDemo.Build(TelegramReportKind.Evening, input, ReportMetric.Sum, "ru", date, Now);

        Assert.True(morning.Demo);
        Assert.Equal("Ташкентский регион", morning.Region);
        Assert.Equal(3, morning.Supervisors);
        Assert.Equal(15, morning.Total.Agents);
        Assert.All(morning.Teams.SelectMany(t => t.Agents), a => Assert.Null(a.Fact)); // утром — без факта
        Assert.All(morning.Teams.SelectMany(t => t.Agents), a => Assert.StartsWith("Агент ", a.Name));
        Assert.Equal(morning.Teams.SelectMany(t => t.Agents).Select(a => a.Plan), evening.Teams.SelectMany(t => t.Agents).Select(a => a.Plan));
        Assert.Equal(evening.Total, again.Total);
        Assert.Empty(DailyReportMath.Verify(evening));
        Assert.Contains("ТЕСТОВЫЕ ДАННЫЕ", DailyReportText.Caption(evening));
    }

    // ---------- Расписание ----------

    private static TelegramReportSchedule Schedule(TimeOnly time, DateTimeOffset armed, int days = DailyReportSchedules.WorkDays) => new()
    {
        Kind = TelegramReportKind.Morning, Enabled = true, Time = time, TimeZone = "Asia/Tashkent", Days = days, ArmedAt = armed,
    };

    private static DateTimeOffset Tashkent(int day, int hour, int minute = 0) => new(2026, 10, day, hour, minute, 0, TimeSpan.FromHours(5));

    [Fact]
    public void Defaults_are_8_13_18_in_Tashkent_monday_to_saturday_and_off()
    {
        var morning = DailyReportSchedules.Default(TelegramReportKind.Morning);
        Assert.False(morning.Enabled); // сохранение настроек ничего не отправляет: включают явно
        Assert.Equal(new TimeOnly(8, 0), morning.Time);
        Assert.Equal(new TimeOnly(13, 0), DailyReportSchedules.Default(TelegramReportKind.Midday).Time);
        Assert.Equal(new TimeOnly(18, 0), DailyReportSchedules.Default(TelegramReportKind.Evening).Time);
        Assert.Equal("Asia/Tashkent", morning.TimeZone);
        Assert.Equal([1, 2, 3, 4, 5, 6], DailyReportSchedules.IsoDays(morning.Days));
        Assert.Equal(DailyReportSchedules.AllDays, DailyReportSchedules.DaysOf([1, 2, 3, 4, 5, 6, 7, 7, 9]));
    }

    [Fact]
    public void Slot_is_due_after_its_time_within_catch_up_and_only_once_armed()
    {
        var schedule = Schedule(new TimeOnly(8, 0), Tashkent(20, 12));
        Assert.Null(DailyReportSchedules.Due(schedule, Tashkent(23, 7, 59)));
        Assert.Equal(new ReportSlot(new DateOnly(2026, 10, 23), Tashkent(23, 8)), DailyReportSchedules.Due(schedule, Tashkent(23, 8)));
        Assert.NotNull(DailyReportSchedules.Due(schedule, Tashkent(23, 9, 30)));  // сервер был выключен в 8:00 — догоняет
        Assert.Null(DailyReportSchedules.Due(schedule, Tashkent(23, 10, 1)));     // позже двух часов — уже не нужен
        Assert.Null(DailyReportSchedules.Due(schedule, Tashkent(25, 8, 5)));      // 25.10.2026 — воскресенье, не в днях
        Assert.NotNull(DailyReportSchedules.Due(Schedule(new TimeOnly(8, 0), Tashkent(20, 12), DailyReportSchedules.AllDays), Tashkent(25, 8, 5)));

        // Включили в 08:30 — сегодняшние 08:00 не догоняются, следующий запуск — завтра.
        var armedLate = Schedule(new TimeOnly(8, 0), Tashkent(23, 8, 30));
        Assert.Null(DailyReportSchedules.Due(armedLate, Tashkent(23, 8, 31)));
        Assert.Equal(Tashkent(24, 8), DailyReportSchedules.Next(armedLate, Tashkent(23, 8, 31)));
        armedLate.Enabled = false;
        Assert.Null(DailyReportSchedules.Due(armedLate, Tashkent(24, 8)));
        Assert.Null(DailyReportSchedules.Next(armedLate, Tashkent(24, 8)));

        // Сервер в UTC: 03:00 UTC — это 08:00 в Ташкенте.
        Assert.NotNull(DailyReportSchedules.Due(schedule, new DateTimeOffset(2026, 10, 23, 3, 0, 30, TimeSpan.Zero)));
        Assert.Equal(new TimeOnly(8, 30), DailyReportSchedules.ParseTime("8:30"));
        Assert.Null(DailyReportSchedules.ParseTime("25:00"));
        Assert.Null(DailyReportSchedules.Zone("Mars/Olympus"));
    }

    // ---------- Картинки ----------

    private static readonly SkiaDailyReportRenderer Renderer = new();

    [Fact]
    public void Every_page_is_a_16_by_9_png_within_telegram_photo_limits()
    {
        foreach (var kind in Enum.GetValues<TelegramReportKind>())
        {
            var report = DailyReportDemo.Build(kind, new DemoReportInput(null, 1, 8), ReportMetric.Sum, "ru", new DateOnly(2026, 10, 23), Now);
            var pages = Renderer.Render(report);
            Assert.NotEmpty(pages);
            foreach (var page in pages)
            {
                Assert.Equal([0x89, 0x50, 0x4E, 0x47], page.Take(4)); // PNG
                using var bitmap = SKBitmap.Decode(page);
                Assert.Equal((1920, 1080), (bitmap.Width, bitmap.Height)); // 16:9, каждая страница одного размера
                Assert.True(page.Length < 1_500_000, $"{kind}: страница {page.Length / 1024} КБ");
            }
        }
    }

    [Fact]
    public void Many_agents_split_into_numbered_16_by_9_pages_and_long_names_do_not_break_the_layout()
    {
        var big = DailyReportDemo.Build(TelegramReportKind.Evening, new DemoReportInput("Сурхондарё вилояти — очень длинное название региона для проверки", 1, 25),
            ReportMetric.Sum, "uz", new DateOnly(2026, 10, 23), Now);
        var longNames = big with
        {
            Teams = big.Teams.Select(t => t with
            {
                Supervisor = "Абдурахмонов Шахзодбек Ғайратжонович (супервайзер)",
                Agents = t.Agents.Select(a => a with { Name = $"{a.Name} Ўрозбоев Қаҳрамонжон Ҳусанович 😀" }).ToList(),
            }).ToList(),
        };
        var pages = Renderer.Render(longNames);
        Assert.True(pages.Count >= 3, $"страниц: {pages.Count}");
        foreach (var page in pages)
        {
            using var bitmap = SKBitmap.Decode(page);
            Assert.Equal((1920, 1080), (bitmap.Width, bitmap.Height));
        }

        // Несколько команд (демо) — тоже 16:9.
        var teams = DailyReportDemo.Build(TelegramReportKind.Midday, new DemoReportInput(null, 3, 6), ReportMetric.Kg, "ru", new DateOnly(2026, 10, 23), Now);
        Assert.All(Renderer.Render(teams), page => Assert.Equal(1920, SKBitmap.Decode(page).Width));
    }

    [Fact]
    public void Plain_text_lists_every_agent_and_the_totals()
    {
        var report = DailyReportDemo.Build(TelegramReportKind.Midday, new DemoReportInput("Хоразм", 2, 3), ReportMetric.Kg, "ru", new DateOnly(2026, 10, 23), Now);
        var text = DailyReportText.Plain(report);
        foreach (var agent in report.Teams.SelectMany(t => t.Agents))
        {
            Assert.Contains(agent.Name, text);
        }

        Assert.Contains("Итого по команде", text);
        Assert.Contains("кг", text);
        Assert.Contains("Хоразм", DailyReportText.Caption(report));
    }
}
