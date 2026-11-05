using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Sales;
using OneBase.Application.Telegram.Reports;
using OneBase.Application.Telegram.Weekly;
using OneBase.Domain.Sales;
using OneBase.Domain.Telegram;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// Формулы еженедельного отчёта (формулы образцов «Полевого контроля», числа вымышленные; неделя 26–31.10.2026, данные по 29.10): рабочие дни,
/// прогноз, нужный темп, «добрать» КПУ и статусы, цели недели, корзины спящих.
/// </summary>
public class WeeklyReportMathTests
{
    private static readonly IReadOnlyDictionary<DateOnly, string> NoHolidays = new Dictionary<DateOnly, string>();

    [Fact]
    public void Week_is_monday_to_saturday_and_last_full_week_waits_for_saturday()
    {
        Assert.Equal((new DateOnly(2026, 10, 26), new DateOnly(2026, 10, 31)), WeeklyReportMath.Week(new DateOnly(2026, 10, 29)));
        Assert.Equal((new DateOnly(2026, 10, 26), new DateOnly(2026, 10, 31)), WeeklyReportMath.Week(new DateOnly(2026, 11, 1))); // воскресенье — прошедшая неделя
        Assert.Equal((new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 24)), WeeklyReportMath.LastFullWeek(new DateOnly(2026, 10, 29)));
        Assert.Equal((new DateOnly(2026, 10, 26), new DateOnly(2026, 10, 31)), WeeklyReportMath.LastFullWeek(new DateOnly(2026, 10, 31)));
    }

    [Fact]
    public void Calendar_counts_working_days_without_sundays_and_holidays()
    {
        var plain = WeeklyReportMath.Calendar(new DateOnly(2026, 10, 29), new DateOnly(2026, 10, 26), new DateOnly(2026, 10, 31), NoHolidays);
        Assert.Equal((27, 25, 2, 6, 4), (plain.MonthWorkDays, plain.MonthWorkDaysDone, plain.MonthWorkDaysLeft, plain.WeekWorkDays, plain.WeekWorkDaysDone));

        var holidays = new Dictionary<DateOnly, string> { [new DateOnly(2026, 10, 1)] = "День учителя" };
        var withHoliday = WeeklyReportMath.Calendar(new DateOnly(2026, 10, 29), new DateOnly(2026, 10, 26), new DateOnly(2026, 10, 31), holidays);
        Assert.Equal((26, 24, 2), (withHoliday.MonthWorkDays, withHoliday.MonthWorkDaysDone, withHoliday.MonthWorkDaysLeft));
    }

    [Fact]
    public void Forecast_and_pace_follow_working_days()
    {
        // 8 000 кг за 25 из 27 рабочих дней → прогноз 8 640; до 80% плана 11 000 — 800 кг, 400 в день; до 100% — 3 000, 1 500 в день.
        Assert.Equal(8_640m, Math.Round(WeeklyReportMath.Forecast(8_000m, 25, 27)!.Value));
        var to80 = WeeklyReportMath.Pace(11_000m, 0.8m, 8_000m, 2)!;
        Assert.Equal((800m, 400m), (to80.Remaining, to80.PerDay));
        var to100 = WeeklyReportMath.Pace(11_000m, 1m, 8_000m, 2)!;
        Assert.Equal((3_000m, 1_500m), (to100.Remaining, Math.Floor(to100.PerDay!.Value)));
        Assert.Null(WeeklyReportMath.Pace(null, 0.8m, 100, 2));
        Assert.Null(WeeklyReportMath.Pace(1000, 0.8m, 900, 2)!.PerDay); // цель взята
        Assert.Null(WeeklyReportMath.Forecast(100, 0, 27));
        Assert.Equal(0.25m, Math.Round(WeeklyReportMath.Change(1_500m, 1_200m)!.Value, 2));
        Assert.Null(WeeklyReportMath.Change(10, 0));
        // От базы меньше 50 кг и у ушедшей в возвраты недели процента нет: «+185%» с 15 кг и «−200%» ничего не значат.
        Assert.Null(WeeklyReportMath.Change(43, 15));
        Assert.Null(WeeklyReportMath.Change(-67, 67));
        Assert.Equal(3.88m, Math.Round(WeeklyReportMath.Change(6_236m, 1_278m)!.Value, 2)); // +388% от 1 278 кг — честный рост
    }

    [Fact]
    public void Region_plan_of_next_month_is_split_by_agents_plans_of_this_month()
    {
        // Ноябрьских планов ТП в Linko ещё нет, план РОП региона — 21 000 кг. Весовых планов октября нет ни у кого, в сумах — у двух ТП из трёх:
        // делим по ним (2 : 1), третий без плана — 0; по факту делили бы, только если сумм нет.
        IReadOnlyDictionary<long, decimal> kg = new Dictionary<long, decimal> { [1] = 0, [2] = 0, [3] = 0 };
        IReadOnlyDictionary<long, decimal> sum = new Dictionary<long, decimal> { [1] = 200_000_000, [2] = 100_000_000, [3] = 0 };
        IReadOnlyDictionary<long, decimal> fact = new Dictionary<long, decimal> { [1] = 500, [2] = 500, [3] = 500 };
        var bySum = WeeklyReportMath.SplitPlan(21_000m, [kg, sum, fact]);
        Assert.Equal((14_000m, 7_000m, 0m), (bySum[1], bySum[2], bySum[3]));
        Assert.Equal(21_000m, bySum.Values.Sum());

        // План в сумах только у одного из трёх (меньше половины) — делим по факту, поровну.
        IReadOnlyDictionary<long, decimal> few = new Dictionary<long, decimal> { [1] = 100_000_000, [2] = 0, [3] = 0 };
        Assert.Equal(7_000m, WeeklyReportMath.SplitPlan(21_000m, [kg, few, fact])[2]);
        Assert.Empty(WeeklyReportMath.SplitPlan(21_000m, [kg]));
    }

    [Fact]
    public void Dealer_money_adds_up_the_dealer_and_its_previous_names()
    {
        // Сменился дилер склада: у прежнего долг закрыт возвратом, у нового — отгрузки без оплат. Деньги региона — по обоим.
        static OneBase.Application.Sales.Settlements.DsoDealer Dealer(string name, decimal debt, decimal overdue, decimal paid, decimal shipped, DateOnly? lastPay,
            params (DateOnly Date, decimal Remaining)[] open) =>
            new(name.ToLowerInvariant(), name, "noterm", null, false, 20m, null, debt, overdue, paid, shipped, 0, lastPay, null, 0,
                open.Select(o => new OneBase.Application.Sales.Settlements.DsoOpen(o.Date, o.Remaining, o.Remaining, 0, null, o.Date, 0, 0, "ГП")).ToList());

        var old = Dealer("Прежний", 0, 0, -70m, 0, new DateOnly(2026, 9, 25));
        var current = Dealer("Новый", 550m, 0, 0, 550m, null, (new DateOnly(2026, 10, 20), 550m));
        var money = WeeklyReportBuilder.DealerMoney([old, current])!;

        Assert.Equal(("Новый + Прежний", 550m, -70m, 550m), (money.Name, money.Debt, money.PaidMonth, money.ShippedMonth));
        Assert.Equal(new DateOnly(2026, 9, 25), money.LastPay);
        Assert.Single(money.Open);
        Assert.Null(WeeklyReportBuilder.DealerMoney([]));
    }

    [Fact]
    public void Kpi_cell_tells_how_much_to_add_and_what_the_threshold_pays()
    {
        // Тотал 5 000 кг, факт 3 600 (72%), порог 80% — добрать 400 кг, 200 в день, «реально добрать», выход на порог — 800 тыс.
        var total = WeeklyReportMath.KpiCell("Тотал", "кг", 5_000m, 3_600m, 0.72m, 0.8m, 1_000m, 0m, 2, 10);
        Assert.Equal((400m, 200m, WeeklyKpiStatuses.Realistic, 800m), (total.Need, total.NeedPerDay, total.Status, total.Premium));

        // Могуль 50% при пороге 80% — «далеко»; бонус 1 250 × порог = 1 000.
        var mogul = WeeklyReportMath.KpiCell("Могуль", "кг", 2_000m, 1_000m, 0.5m, 0.8m, 1_250m, 0m, 2, 10);
        Assert.Equal((WeeklyKpiStatuses.Far, 1_000m), (mogul.Status, mogul.Premium));

        // Порог пройден — премия начисленная Linko, добирать нечего.
        var passed = WeeklyReportMath.KpiCell("Тотал", "кг", 5_000m, 4_600m, 0.92m, 0.8m, 1_000m, 920m, 2, 10);
        Assert.Equal((WeeklyKpiStatuses.Passed, (decimal?)null, 920m), (passed.Status, passed.Need, passed.Premium));

        var none = WeeklyReportMath.KpiCell("Печенье", "кг", 100m, 50m, 0.5m, null, null, null, 2, 10);
        Assert.Equal(WeeklyKpiStatuses.NoThreshold, none.Status);
    }

    [Fact]
    public void Weekly_goals_match_the_samples()
    {
        Assert.Equal([88, 77, 110], new[] { 80, 70, 100 }.Select(w => WeeklyReportMath.Goal(w, 1.1m)));
        Assert.Equal([36, 25, 60], new[] { 30, 15, 50 }.Select(w => WeeklyReportMath.Goal(w, 1.2m, 25)));
        Assert.Equal(1_111m, WeeklyReportMath.SalesGoal(5_000m, 27, 6));
        Assert.Null(WeeklyReportMath.SalesGoal(null, 27, 6));
        Assert.Equal([6, 5, 4], new[] { 30, 25, 20 }.Select(s => WeeklyReportMath.Return(s, 0.2m)));
        Assert.Equal(0, WeeklyReportMath.Return(0, 0.3m));
    }

    [Fact]
    public void Sleeping_buckets_by_last_purchase()
    {
        var day = new DateOnly(2026, 10, 29);
        Assert.Equal(WeeklySleepBuckets.Days15To30, WeeklyReportMath.SleepBucket(new DateOnly(2026, 10, 2), true, day, 15, 30)); // 27 дней
        Assert.Null(WeeklyReportMath.SleepBucket(new DateOnly(2026, 10, 20), true, day, 15, 30)); // 9 дней — не спит
        Assert.Equal(WeeklySleepBuckets.Days30Plus, WeeklyReportMath.SleepBucket(null, true, day, 15, 30)); // брала в прошлом месяце, в этом — нет
        Assert.Null(WeeklyReportMath.SleepBucket(null, false, day, 15, 30));
    }

    [Fact]
    public void Focus_note_starts_with_its_label_in_both_languages()
    {
        // PDF выделяет метку («Фокус» / «Diqqat») жирным и печатает остаток — перевод обязан начинаться с метки.
        var note = WeeklyNote.Of("focus", ("perDay", 524m), ("current", 426m));
        Assert.StartsWith("Фокус до конца месяца", WeeklyTexts.Note(note, "ru"));
        Assert.StartsWith("Diqqat — oy oxirigacha", WeeklyTexts.Note(note, "uz"));
    }
}

/// <summary>
/// Сборка еженедельного отчёта СВ на PostgreSQL: неделя и те же дни прошлой недели по строкам «Продаж», маршрут, месяц и план ТП, спящие и
/// новые ТТ, цели следующей недели, «нет данных» вместо нулей (ДМП, DSO, KPI).
/// </summary>
public class WeeklyReportDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private const long First = 940_001, Second = 940_002;

    private async Task SeedAsync()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        db.LinkoUsers.AddRange(
            new Domain.Sales.LinkoUser { Id = First, FirstName = "ТП Первый", SecondName = "Тестов", IsActive = true, JobName = "Агент" },
            new Domain.Sales.LinkoUser { Id = Second, FirstName = "ТП Второй", SecondName = "Тестов", IsActive = true, JobName = "Агент" });
        foreach (var (id, name) in new[] { (950_001L, "Точка А"), (950_002L, "Точка Б"), (950_003L, "Точка В"), (950_004L, "Точка Г"), (950_005L, "Точка Д") })
        {
            db.LinkoMarkets.Add(new LinkoMarket { Id = id, Name = name, BranchId = 5, BranchName = "Хоразм", ResponsibleAgentId = id == 950_004 ? Second : First });
        }

        var n = 960_000L;
        void Sale(long agent, long market, DateOnly day, decimal kg)
        {
            var id = ++n;
            var at = day.ToDateTime(new TimeOnly(11, 0));
            db.LinkoOrders.Add(new LinkoOrder
            {
                Id = id, CreatedAt = at, CreatedDate = day, AcceptedAt = at, AcceptedDate = day, Status = "delivered", MarketId = market, BranchId = 5, BranchName = "Хоразм",
                AgentId = agent, TotalPrice = kg * 40_000, TotalWeight = kg,
            });
            db.LinkoOrderLines.Add(new LinkoOrderLine { Id = id, OrderId = id, ProductId = 100, Amount = kg, Price = 40_000, TotalPrice = kg * 40_000, TotalWeight = kg });
        }

        // Сентябрь: первый — А, Б, В; второй — Г. Октябрь: первый — А 02.10 (спит 15–30), Д 27.10 (новая); Б и В в октябре не брали (спят 30+);
        // второй — Г 20.10 и 28.10.
        Sale(First, 950_001, new DateOnly(2026, 9, 10), 10);
        Sale(First, 950_002, new DateOnly(2026, 9, 11), 20);
        Sale(First, 950_003, new DateOnly(2026, 9, 12), 5);
        Sale(Second, 950_004, new DateOnly(2026, 9, 14), 8);
        Sale(First, 950_001, new DateOnly(2026, 10, 2), 12);
        Sale(Second, 950_004, new DateOnly(2026, 10, 20), 6);
        Sale(First, 950_005, new DateOnly(2026, 10, 27), 30);
        Sale(Second, 950_004, new DateOnly(2026, 10, 28), 7);
        Sale(Second, 950_004, new DateOnly(2026, 10, 29), 1);

        // Маршрут недели первого: 27.10 Д — выполнен по плану, 28.10 Б — не посещён.
        db.LinkoVisits.AddRange(
            new LinkoVisit { Id = 970_001, Date = new DateTime(2026, 10, 27, 10, 0, 0), Day = new DateOnly(2026, 10, 27), Status = "done", IsInPlan = true, MarketId = 950_005, UserId = First },
            new LinkoVisit { Id = 970_002, Date = new DateTime(2026, 10, 28, 0, 0, 0), Day = new DateOnly(2026, 10, 28), Status = "pending", IsInPlan = true, MarketId = 950_002, UserId = First });

        // План октября первого — 200 кг и АКБ 10 (staff_balance Linko).
        db.SalesStaffPlans.AddRange(
            new SalesStaffPlan { Year = 2026, Month = 10, LinkoUserId = First, IndicatorId = 1, IndicatorName = "Октябрь Тотал Вес", PlanType = StaffPlanTypes.SalesWeight, PlanAmount = 200, FactAmount = 42, LoadedAt = DateTimeOffset.UtcNow },
            new SalesStaffPlan { Year = 2026, Month = 10, LinkoUserId = First, IndicatorId = 2, IndicatorName = "Октябрь АКБ", PlanType = StaffPlanTypes.ActiveClients, PlanAmount = 10, LoadedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        scope.ServiceProvider.GetRequiredService<SalesCacheSignal>().Invalidate();
    }

    [PostgresFact]
    public async Task Supervisor_report_is_built_from_sales_rules_with_no_data_instead_of_zeros()
    {
        fixture.Clock.Now = new DateTimeOffset(2026, 10, 30, 3, 0, 0, TimeSpan.Zero); // пятница 08:00 Ташкент — данные по 29.10
        await SeedAsync();

        WeeklySupervisorReport report;
        using (var scope = fixture.Services.CreateScope())
        {
            report = await scope.ServiceProvider.GetRequiredService<WeeklyReportBuilder>()
                .BuildAsync(new ReportScope(TelegramReportRoles.Sv, RegionId: fixture.KhorazmId), new DateOnly(2026, 10, 29), money: true, CancellationToken.None);
        }

        Assert.Equal((new DateOnly(2026, 10, 26), new DateOnly(2026, 10, 31), new DateOnly(2026, 10, 29)), (report.PeriodStart, report.PeriodEnd, report.DataThrough));
        Assert.Equal("sv", report.Role);
        Assert.Contains(report.DataWarnings, w => w.Code == "week-partial");
        Assert.Contains(report.DataWarnings, w => w.Code == "dmp-missing");
        Assert.False(report.DmpMetrics.Available);
        Assert.Contains(report.SourceStatuses, s => s.Key == WeeklySources.Dmp && s.State == WeeklySourceStates.Missing);

        // Неделя первого: 30 кг (Д 27.10), прошлой недели нет — «к прошлой» нет (не 0), маршрут 1 из 2.
        var first = report.EmployeeMetrics.Single(e => e.AgentId == First);
        Assert.Equal((30m, (decimal?)null, (decimal?)null, 1), (first.WeekKg, first.PrevWeekKg, first.WeekVsPrev, first.WeekAkb));
        Assert.Equal((2, 1, 0.5m), (first.WeekRoutePlan, first.WeekRouteDone, first.WeekRouteShare));
        // «% плана» — из KPI Linko (факт Linko 42 из 200 кг), месяц в кг — по правилам «Продаж».
        Assert.Equal((42m, 200m, 0.21m, 10m), (first.MonthKg, first.PlanKg, first.Execution, first.AkbPlan));
        Assert.Equal((200m, 42m, "кг"), (first.LinkoPlan, first.LinkoFact, first.LinkoUnit));

        // Второй: 8 кг против 6 в те же дни прошлой недели — база меньше 50 кг, процент («+33%») ничего не значит: null; плана в Linko нет — null, а не 0.
        var second = report.EmployeeMetrics.Single(e => e.AgentId == Second);
        Assert.Equal((8m, 6m), (second.WeekKg, second.PrevWeekKg));
        Assert.Null(second.WeekVsPrev);
        Assert.Null(second.PlanKg);
        Assert.Null(second.Execution);

        // Дни недели: 26–29.10 — факт, 30–31.10 — впереди (null).
        Assert.Equal(6, report.SalesMetrics.Days.Count);
        Assert.Null(report.SalesMetrics.Days[4].Kg);
        Assert.True(report.SalesMetrics.Days[1].Kg >= 30);

        // Клиенты: А — спит 15–30 (последняя покупка 02.10), Б и В — 30+ (брали в сентябре), Д — новая.
        var sleeping = report.ClientMetrics.Sleeping.ToDictionary(s => s.MarketId);
        Assert.Equal(WeeklySleepBuckets.Days15To30, sleeping[950_001].Bucket);
        Assert.Equal(27, sleeping[950_001].Days);
        Assert.Equal((WeeklySleepBuckets.Days30Plus, 20m, First), (sleeping[950_002].Bucket, sleeping[950_002].Kg, sleeping[950_002].AgentId));
        Assert.Equal(WeeklySleepBuckets.Days30Plus, sleeping[950_003].Bucket);
        Assert.False(sleeping.ContainsKey(950_004));
        var firstClients = report.ClientMetrics.Agents.Single(a => a.AgentId == First);
        Assert.Equal((3, 1, 1, 2, 1), (firstClients.Base, firstClients.Bought, firstClients.Sleeping15To30, firstClients.Sleeping30Plus, firstClients.New));

        // Цели 02–07.11: продажи — план октября ÷ рабочие дни октября × 6 (плана ноября нет); АКБ прошлой полной недели (19–24.10): у второго 1 ТТ;
        // акции — не меньше 25.
        var goals = report.WeeklyGoals;
        Assert.Equal((new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 7), false), (goals.WeekFrom, goals.WeekTo, goals.PlanOfNextMonth));
        var g1 = goals.Agents.Single(a => a.AgentId == First);
        Assert.Equal(Math.Round(200m / report.Calendar.MonthWorkDays * goals.WeekWorkDays, 0, MidpointRounding.AwayFromZero), g1.SalesKg);
        Assert.Equal((0, 25, 1, 2), (g1.AkbWas, g1.Promo, g1.Sleeping15To30, g1.Sleeping30Plus));
        Assert.Equal(["Точка Б", "Точка В"], g1.ReturnList30Plus.Select(s => s.Name));
        var g2 = goals.Agents.Single(a => a.AgentId == Second);
        Assert.Equal((1, 1, (decimal?)null), (g2.AkbWas, g2.Akb, g2.SalesKg));
        Assert.False(goals.DmpAvailable);

        // Регион — одна строка, продажи недели области не меньше суммы ТП.
        var region = Assert.Single(report.Regions);
        Assert.Equal(fixture.KhorazmId, region.RegionId);
        Assert.True(report.SalesMetrics.WeekKg >= 38);

        // PDF СВ (QuestPDF): 10 страниц, русский и узбекский; страницы картинками — для предпросмотра.
        var renderer = fixture.Services.GetRequiredService<IWeeklyPdfRenderer>();
        var pdf = renderer.Render(report, "ru");
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.Equal(10, renderer.RenderImages(report, "ru", 40).Count);
        Assert.Equal(10, renderer.RenderImages(report, "uz", 40).Count);
        Assert.Equal(10, renderer.RenderImages(report, "uz-Cyrl", 40).Count); // узбекская кириллица

        // РОП (rm без направления): строки регионов + сводка; PDF — 3 страницы (главное, ДМП, дилеры).
        WeeklySupervisorReport rop;
        using (var scope = fixture.Services.CreateScope())
        {
            rop = await scope.ServiceProvider.GetRequiredService<WeeklyReportBuilder>()
                .BuildAsync(new ReportScope(TelegramReportRoles.Rm), new DateOnly(2026, 10, 29), money: true, CancellationToken.None);
        }

        Assert.Equal("rop", rop.Role);
        Assert.Contains(rop.Regions, g => g.RegionId == fixture.KhorazmId && g.Sales.WeekKg >= 38);
        Assert.Equal(3, renderer.RenderImages(rop, "ru", 40).Count);

        // Карточка ТП: отчёт ТП — только его цифры (ни строк, ни имён второго ТП), 2 страницы на ТП; у СВ — карточки всей команды.
        using (var scope = fixture.Services.CreateScope())
        {
            var tp = await scope.ServiceProvider.GetRequiredService<WeeklyReportBuilder>()
                .BuildAsync(new ReportScope(TelegramReportRoles.Tp, AgentId: First), new DateOnly(2026, 10, 29), money: true, CancellationToken.None);
            Assert.Equal(("tp", "Хоразм"), (tp.Role, tp.RegionName));
            Assert.Equal(First, Assert.Single(tp.EmployeeMetrics).AgentId);
            Assert.All(tp.WeeklyGoals.Agents, g => Assert.Equal(First, g.AgentId));
            Assert.All(tp.ClientMetrics.Agents, c => Assert.Equal(First, c.AgentId));
            Assert.All(tp.ClientMetrics.Sleeping, m => Assert.Equal(First, m.AgentId));
            Assert.All(tp.KpiMetrics.People, k => Assert.Equal(First, k.Id));
            Assert.All(tp.PromotionMetrics.Agents, x => Assert.True(x.AgentId is null || x.AgentId == First));
            Assert.Empty(tp.Regions);
            Assert.DoesNotContain("ТП Второй", System.Text.Json.JsonSerializer.Serialize(tp));
            Assert.Equal(2, renderer.RenderCardImages(tp, "ru", 40).Count);
            Assert.Equal(2, renderer.RenderCardImages(tp, "uz", 40).Count);
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(renderer.RenderCards(tp, "ru"), 0, 4));
        }

        Assert.Equal(2 * report.EmployeeMetrics.Count, renderer.RenderCardImages(report, "ru", 40).Count);

        // Полнота: заказы и визиты Linko ни разу не обновлялись — данные неполные (PDF «только при полных данных» не уйдёт); KPI есть.
        Assert.Equal([WeeklySources.Orders, WeeklySources.Visits], report.Incomplete);

        // Заказы и визиты обновлены час назад; формула АКБ из «Отчётов PDF» — × 1,5: у второго 1 ТТ прошлой недели → цель 2 (было 1).
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            foreach (var entity in new[] { "orders", "visits" })
            {
                var state = await db.LinkoSyncStates.FirstOrDefaultAsync(x => x.Entity == entity);
                if (state is null)
                {
                    db.LinkoSyncStates.Add(state = new LinkoSyncState { Entity = entity });
                }

                state.LastSuccessAt = new DateTimeOffset(2026, 10, 30, 2, 0, 0, TimeSpan.Zero); // час назад
            }

            db.TelegramWeeklyPdfSettings.Add(new TelegramWeeklyPdfSettings { GoalAkbFactor = 1.5m });
            await db.SaveChangesAsync();
            var again = await scope.ServiceProvider.GetRequiredService<WeeklyReportBuilder>()
                .BuildAsync(new ReportScope(TelegramReportRoles.Sv, RegionId: fixture.KhorazmId), new DateOnly(2026, 10, 29), money: true, CancellationToken.None);
            Assert.Empty(again.Incomplete);
            Assert.Equal((1, 2), (again.WeeklyGoals.Agents.Single(a => a.AgentId == Second).AkbWas, again.WeeklyGoals.Agents.Single(a => a.AgentId == Second).Akb));
            await db.TelegramWeeklyPdfSettings.ExecuteDeleteAsync();

            // План РОП региона на ноябрь есть, а планов ТП на ноябрь в Linko нет — цели 02–07.11 из плана РОП, разложенного по ТП по их планам
            // октября: у первого 200 кг, у второго плана нет — весь план первому (вес есть у половины ТП).
            db.SalesRegionPlans.Add(new Domain.Sales.SalesRegionPlan { RegionId = fixture.KhorazmId, Kind = Domain.Sales.PlanKind.Rop, Year = 2026, Month = 11, PlanKg = 600 });
            await db.SaveChangesAsync();
            scope.ServiceProvider.GetRequiredService<SalesCacheSignal>().Invalidate();
            var byRop = await fixture.Services.CreateScope().ServiceProvider.GetRequiredService<WeeklyReportBuilder>()
                .BuildAsync(new ReportScope(TelegramReportRoles.Sv, RegionId: fixture.KhorazmId), new DateOnly(2026, 10, 29), money: true, CancellationToken.None);
            var ropGoals = byRop.WeeklyGoals;
            Assert.True(ropGoals.PlanOfNextMonth && ropGoals.FromRegionPlan);
            Assert.Equal(Math.Round(600m / ropGoals.MonthWorkDays * ropGoals.WeekWorkDays, 0, MidpointRounding.AwayFromZero), ropGoals.Agents.Single(x => x.AgentId == First).SalesKg);
            Assert.Null(ropGoals.Agents.Single(x => x.AgentId == Second).SalesKg);
            Assert.Equal(ropGoals.Agents.Single(x => x.AgentId == First).SalesKg, ropGoals.SalesKg);
            await db.SalesRegionPlans.Where(x => x.RegionId == fixture.KhorazmId && x.Year == 2026 && x.Month == 11).ExecuteDeleteAsync();
            scope.ServiceProvider.GetRequiredService<SalesCacheSignal>().Invalidate();
        }

        // Воскресенье 1 ноября (есть продажа этого дня): неделя — прошедшая 26–31.10, месяц — октябрь, а не ноябрь с одним днём.
        fixture.Clock.Now = new DateTimeOffset(2026, 11, 1, 7, 0, 0, TimeSpan.Zero);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var at = new DateTime(2026, 11, 1, 11, 0, 0);
            db.LinkoOrders.Add(new LinkoOrder
            {
                Id = 960_100, CreatedAt = at, CreatedDate = new DateOnly(2026, 11, 1), AcceptedAt = at, AcceptedDate = new DateOnly(2026, 11, 1), Status = "delivered",
                MarketId = 950_001, BranchId = 5, BranchName = "Хоразм", AgentId = First, TotalPrice = 4 * 40_000, TotalWeight = 4,
            });
            db.LinkoOrderLines.Add(new LinkoOrderLine { Id = 960_100, OrderId = 960_100, ProductId = 100, Amount = 4, Price = 40_000, TotalPrice = 4 * 40_000, TotalWeight = 4 });
            await db.SaveChangesAsync();
            scope.ServiceProvider.GetRequiredService<SalesCacheSignal>().Invalidate();
            var sunday = await scope.ServiceProvider.GetRequiredService<WeeklyReportBuilder>()
                .BuildAsync(new ReportScope(TelegramReportRoles.Sv, RegionId: fixture.KhorazmId), null, money: true, CancellationToken.None);
            Assert.Equal((new DateOnly(2026, 10, 26), new DateOnly(2026, 10, 31)), (sunday.PeriodStart, sunday.DataThrough)); // неделя прошла — по субботу
            var firstOct = sunday.EmployeeMetrics.Single(e => e.AgentId == First);
            Assert.Equal((42m, 200m), (firstOct.MonthKg, firstOct.PlanKg)); // план и факт октября из KPI Linko
        }
    }
}
