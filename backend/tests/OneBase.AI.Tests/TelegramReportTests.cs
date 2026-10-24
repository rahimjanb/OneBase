using OneBase.Application.Field;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Telegram;
using OneBase.Infrastructure.Telegram.Reports;
using SkiaSharp;
using SilentMarket = OneBase.Application.Telegram.Reports.SilentMarket;

namespace OneBase.AI.Tests;

/// <summary>
/// Автоотчёты в Telegram без базы: каталог 13 запусков шести отчётов и роли, расписание (дни, пояс, догон, пропуск после простоя, «взвод»),
/// неделя пн–сб и пересчёт в понедельник, области получателей (СВ видит только свой регион, ТП — только себя), формулы и порог 80%,
/// построители всех отчётов на подставных данных (итоги = сумма строк, пустые данные, напоминание ТП с заказами — пропуск), картинки PNG 16:9.
/// </summary>
public class TelegramReportTests
{
    // Пятница, 23.10.2026; в октябре 2026 — рабочие дни пн–сб.
    private static readonly DateOnly Day = new(2026, 10, 23);
    private static readonly Guid West = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid Khorazm = Guid.Parse("00000000-0000-0000-0000-000000000005");
    private static readonly Guid Bukhara = Guid.Parse("00000000-0000-0000-0000-000000000007");
    private static readonly int WorkDays = FieldKpi.WorkingDays(2026, 10);

    private static DateTimeOffset At(DateOnly day, int hour, int minute = 0) => new(day.ToDateTime(new TimeOnly(hour, minute)), TimeSpan.FromHours(5));

    private static readonly IReadOnlyList<ReportRegionTeam> Teams =
    [
        new(Khorazm, "Хоразм", "Рустам Ахмедов", "Азиз Каримов", [new ReportPerson(71, "Азиз Каримов"), new ReportPerson(72, "Шавкат Олимов")],
            [new ReportPerson(61, "Бахтиёр Юлдашев"), new ReportPerson(62, "Дилшод Рахимов"), new ReportPerson(63, "Отабек Назаров")], West, "Запад"),
        new(Bukhara, "Бухоро", "Рустам Ахмедов", null, [], [new ReportPerson(64, "Жасур Хамидов")], West, "Запад"),
    ];

    private static readonly IReadOnlyList<ReportDirection> Directions = [new(West, "Запад", "Рустам Ахмедов")];

    /// <summary>План на день: 30 / 20 / 10 / 5 млн сум (месячный = дневной × рабочие дни), кг — 1 000 / 700 / 400 / 200.</summary>
    private static Dictionary<(long, int, int), MonthPlan> Plans() => new()
    {
        [(61, 2026, 10)] = new(30_000_000m * WorkDays, 1_000m * WorkDays, 120, new Dictionary<string, decimal> { ["Кекс"] = 600m * WorkDays, ["Бамбук"] = 400m * WorkDays }),
        [(62, 2026, 10)] = new(20_000_000m * WorkDays, 700m * WorkDays, 90, new Dictionary<string, decimal>()),
        [(63, 2026, 10)] = new(10_000_000m * WorkDays, 400m * WorkDays, 60, new Dictionary<string, decimal>()),
        [(64, 2026, 10)] = new(5_000_000m * WorkDays, 200m * WorkDays, 40, new Dictionary<string, decimal>()),
    };

    private static FactOrder Order(long id, long agent, int hour, decimal sum, decimal kg, DateOnly? day = null, long? market = null, string? promo = null) =>
        new(id, agent, market ?? 1000 + id, day ?? Day, new TimeOnly(hour, 0), sum, kg,
            new Dictionary<string, CategoryAmount> { ["Кекс"] = new(sum * 0.6m, kg * 0.6m), ["Бамбук"] = new(sum * 0.4m, kg * 0.4m) },
            promo is null ? [] : [promo]);

    /// <summary>
    /// 61: 12 млн к 13:00 и ещё 20 млн после (40% → 107%); 62: 17 млн до 13:00 (85%); 63 — без заказов (0%); 64 (Бухоро): 6 млн с акцией.
    /// </summary>
    private static ReportFacts Facts(DateTimeOffset? ordersAsOf = null, bool empty = false) => new()
    {
        Categories = ["Бамбук", "Кекс"],
        CategoryPrice = new Dictionary<string, decimal> { ["Кекс"] = 30_000, ["Бамбук"] = 28_000 },
        Plans = empty ? new Dictionary<(long, int, int), MonthPlan>() : Plans(),
        Orders = empty
            ? []
            :
            [
                Order(1, 61, 9, 12_000_000, 400, promo: "Осень −5%"), Order(2, 61, 14, 20_000_000, 650), Order(3, 62, 10, 17_000_000, 600),
                Order(4, 64, 11, 6_000_000, 210, promo: "Ручная скидка"),
            ],
        Visits = empty
            ? []
            :
            [
                new(61, 1001, Day, new TimeOnly(9, 0), true, true), new(61, 1002, Day, new TimeOnly(15, 0), true, true), new(61, 1003, Day, new TimeOnly(0, 0), true, false),
                new(62, 1004, Day, new TimeOnly(10, 0), true, true), new(63, 1005, Day, new TimeOnly(0, 0), true, false),
            ],
        OrdersAsOf = ordersAsOf ?? At(Day, 17, 55),
        VisitsAsOf = ordersAsOf ?? At(Day, 17, 55),
    };

    private static ReportDocument Build(string job, ReportScope scope, DateTimeOffset now, ReportFacts? facts = null, string language = "ru",
        IReadOnlyDictionary<long, NoPurchaseAgent>? noPurchase = null, DateOnly? day = null)
    {
        var info = ReportCatalog.Find(job)!;
        var resolved = ReportScopes.Resolve(scope, Teams, Directions, ru => ReportTexts.Get(language, ru));
        var date = day ?? Day;
        var cutoff = info.IsMidday ? TimeOnly.FromDateTime(now.DateTime) : TimeOnly.MaxValue;
        var peers = info.Report == ReportType.Weekly && scope.Role == TelegramReportRoles.Tp ? Teams.First(t => t.Id == resolved.Regions[0].Id).Agents : null;
        var context = new ReportContext(info, resolved, date, now, language, facts ?? Facts(), new TimeOnly(13, 0), cutoff, 30, noPurchase, info.Time, peers);
        return info.Report switch
        {
            ReportType.Daily => DailyPlanReport.Build(context),
            ReportType.NoPurchase => NoPurchaseReport.Build(context),
            ReportType.NoOrders => NoOrdersReport.Build(context),
            ReportType.Promo => PromoReport.Build(context),
            ReportType.Categories => CategoriesReport.Build(context),
            _ => WeeklyReport.Build(context),
        };
    }

    private static readonly ReportScope Rm = new(TelegramReportRoles.Rm, West);
    private static readonly ReportScope Sv = new(TelegramReportRoles.Sv, RegionId: Khorazm, SupervisorId: 71);
    private static ReportScope Tp(long agent) => new(TelegramReportRoles.Tp, AgentId: agent);

    // ---------- Каталог ----------

    [Fact]
    public void Catalog_has_thirteen_runs_of_six_reports_at_the_specified_times()
    {
        Assert.Equal(13, ReportCatalog.Jobs.Count);
        Assert.Equal(ReportCatalog.Jobs.Count, ReportCatalog.Jobs.Select(j => j.Code).Distinct().Count());
        Assert.Equal(6, ReportCatalog.Jobs.Select(j => j.Report).Distinct().Count());
        string Times(ReportType t) => string.Join(" ", ReportCatalog.Jobs.Where(j => j.Report == t).Select(j => j.Time.ToString("HH:mm")));
        Assert.Equal("08:30 13:00 18:00", Times(ReportType.Daily));
        Assert.Equal("08:30", Times(ReportType.NoPurchase));
        Assert.Equal("13:00 17:00 18:00", Times(ReportType.NoOrders));
        Assert.Equal("13:00 18:00", Times(ReportType.Promo));
        Assert.Equal("13:00 18:00", Times(ReportType.Categories));
        Assert.Equal("18:00 08:30", Times(ReportType.Weekly));

        // Напоминание 17:00 — только ТП; неделя — суббота 18:00 и понедельник 8:30; остальное — пн–сб.
        Assert.Equal([TelegramReportRoles.Tp], ReportCatalog.Find("no-orders.reminder")!.Roles);
        Assert.Equal(ReportSchedules.Saturday, ReportCatalog.Find("weekly.saturday")!.Days);
        Assert.Equal(ReportSchedules.Monday, ReportCatalog.Find("weekly.monday")!.Days);
        Assert.All(ReportCatalog.Jobs.Where(j => j.Report != ReportType.Weekly), j => Assert.Equal(ReportSchedules.WorkDays, j.Days));

        // Отчёты получателя: неизвестные коды и чужие роли отбрасываются.
        var codes = ReportCatalog.Parse("daily.morning, no-orders.reminder,unknown,daily.morning");
        Assert.Equal(["daily.morning", "no-orders.reminder"], codes);
        Assert.Equal(["daily.morning"], ReportCatalog.ForRole(codes, TelegramReportRoles.Sv));
        Assert.Equal(["daily.morning", "no-orders.reminder"], ReportCatalog.ForRole(codes, TelegramReportRoles.Tp));

        var job = ReportCatalog.Default(ReportCatalog.Find("daily.midday")!);
        Assert.Equal((false, new TimeOnly(13, 0), "Asia/Tashkent", 1), (job.Enabled, job.Time, job.TimeZone, job.Version)); // по умолчанию выключено
    }

    // ---------- Расписание ----------

    private static TelegramReportJob Job(string code, DateTimeOffset armed, int? catchUp = null)
    {
        var job = ReportCatalog.Default(ReportCatalog.Find(code)!);
        job.Enabled = true;
        job.ArmedAt = armed;
        job.CatchUpMinutes = catchUp ?? job.CatchUpMinutes;
        return job;
    }

    [Fact]
    public void Run_is_due_after_its_time_within_catch_up_only_once_armed_and_missed_after_downtime()
    {
        var job = Job("daily.midday", At(Day, 9));
        Assert.Null(ReportSchedules.Due(job, At(Day, 12, 59)));
        Assert.Equal(new ReportSlot(Day, At(Day, 13)), ReportSchedules.Due(job, At(Day, 13)));
        Assert.Equal(Day, ReportSchedules.Due(job, At(Day, 14, 59))!.Period); // догон 120 мин
        Assert.Null(ReportSchedules.Due(job, At(Day, 15, 1)));

        // Сервер не работал дольше догона — запуск не отправляется, а помечается пропущенным.
        Assert.Null(ReportSchedules.Missed(job, At(Day, 14)));
        Assert.Equal(new ReportSlot(Day, At(Day, 13)), ReportSchedules.Missed(job, At(Day, 15, 1)));

        // Включили в 13:30 — сегодняшние 13:00 уже не догоняются и не считаются пропущенными.
        var armedLate = Job("daily.midday", At(Day, 13, 30));
        Assert.Null(ReportSchedules.Due(armedLate, At(Day, 13, 31)));
        Assert.Null(ReportSchedules.Missed(armedLate, At(Day, 18)));
        Assert.Equal(At(Day.AddDays(1), 13), ReportSchedules.Next(armedLate, At(Day, 13, 31)));

        // Воскресенья нет в днях: следующий после субботы — понедельник.
        Assert.Equal(At(new DateOnly(2026, 10, 26), 13), ReportSchedules.Next(job, At(new DateOnly(2026, 10, 24), 14)));
        Assert.Null(ReportSchedules.Due(job, At(new DateOnly(2026, 10, 25), 13, 5)));

        // Выключенное расписание не срабатывает и не имеет следующего запуска.
        job.Enabled = false;
        Assert.Null(ReportSchedules.Due(job, At(Day, 13, 5)));
        Assert.Null(ReportSchedules.Next(job, At(Day, 13, 5)));

        // Часовой пояс: 13:00 Ташкента — 08:00 UTC.
        Assert.Equal(new DateTimeOffset(2026, 10, 23, 8, 0, 0, TimeSpan.Zero), ReportSchedules.At(Day, new TimeOnly(13, 0), "Asia/Tashkent").ToUniversalTime());
        Assert.Equal([1, 2, 3, 4, 5, 6], ReportSchedules.IsoDays(ReportSchedules.WorkDays));
        Assert.Equal(ReportSchedules.WorkDays, ReportSchedules.DaysOf([6, 5, 4, 3, 2, 1, 1, 9]));
        Assert.Equal(new TimeOnly(8, 30), ReportSchedules.ParseTime("8:30"));
        Assert.Null(ReportSchedules.ParseTime("25:00"));
    }

    [Fact]
    public void Week_is_monday_to_saturday_and_monday_run_recomputes_the_finished_week()
    {
        var saturday = new DateOnly(2026, 10, 24);
        var monday = new DateOnly(2026, 10, 26);
        Assert.Equal((new DateOnly(2026, 10, 19), saturday), WeeklyReport.Period(ReportCatalog.Find("weekly.saturday")!, saturday));
        Assert.Equal((new DateOnly(2026, 10, 19), saturday), WeeklyReport.Period(ReportCatalog.Find("weekly.monday")!, monday));
        Assert.Equal(6, ReportSchedules.WorkingDays(new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 25)).Count());

        var job = Job("weekly.saturday", At(new DateOnly(2026, 10, 1), 9));
        Assert.Null(ReportSchedules.Due(job, At(Day, 18, 5))); // пятница
        Assert.Equal(saturday, ReportSchedules.Due(job, At(saturday, 18, 5))!.Period);
        Assert.Equal(monday, ReportSchedules.Due(Job("weekly.monday", At(new DateOnly(2026, 10, 1), 9)), At(monday, 8, 35))!.Period);

        // Понедельник пересчитывает неделю заново: поздний заказ субботы учтён, уже посчитанное не прибавляется второй раз.
        var facts = Facts();
        var late = new ReportFacts
        {
            Categories = facts.Categories, CategoryPrice = facts.CategoryPrice, Plans = facts.Plans, Visits = facts.Visits, OrdersAsOf = At(monday, 8),
            Orders = [.. facts.Orders, Order(9, 61, 19, 5_000_000, 100, saturday)],
        };
        var sat = Build("weekly.saturday", Sv, At(saturday, 18), facts, day: saturday);
        var mon = Build("weekly.monday", Sv, At(monday, 8, 30), late, day: monday);
        string Totals(ReportDocument d) => string.Join(" / ", d.Tables.Select(t => t.Title + ": " + string.Join(" | ", t.Total?.Select(c => c.Text) ?? [])));
        Assert.True(sat.Tables.Any(t => t.Total?.Any(c => c.Text == "49 000 000") == true), Totals(sat)); // 12 + 20 + 17 млн
        Assert.True(mon.Tables.Any(t => t.Total?.Any(c => c.Text == "54 000 000") == true), Totals(mon)); // + 5 млн субботы
        Assert.Equal("19.10–24.10.2026", mon.Period);
    }

    // ---------- Области и права ----------

    [Fact]
    public void Scope_keys_round_trip_and_each_role_sees_only_its_own_data()
    {
        foreach (var scope in new[] { Rm, new ReportScope(TelegramReportRoles.Rm), Sv, new ReportScope(TelegramReportRoles.Sv, RegionId: Khorazm), Tp(61) })
        {
            Assert.Equal(scope, ReportScope.Parse(scope.Key));
        }

        Assert.Null(ReportScope.Parse("legacy:abc"));
        Assert.Null(ReportScope.Parse(null));

        // РМ — регионы направления; СВ — только ТП своего региона; ТП — только он сам.
        Assert.Equal([61L, 62, 63, 64], ReportScopes.Resolve(Rm, Teams, Directions, s => s).Agents.Select(a => a.Id));
        var sv = ReportScopes.Resolve(Sv, Teams, Directions, s => s);
        Assert.Equal([61L, 62, 63], sv.Agents.Select(a => a.Id));
        Assert.Equal("Хоразм · СВ Азиз Каримов", sv.Title);
        Assert.Equal([62L], ReportScopes.Resolve(Tp(62), Teams, Directions, s => s).Agents.Select(a => a.Id));

        // Супервайзер другого региона, ТП вне команд, неизвестное направление — область не строится.
        Assert.Throws<KeyNotFoundException>(() => ReportScopes.Resolve(Sv with { RegionId = Bukhara }, Teams, Directions, s => s));
        Assert.Throws<KeyNotFoundException>(() => ReportScopes.Resolve(Tp(999), Teams, Directions, s => s));
        Assert.Throws<KeyNotFoundException>(() => ReportScopes.Resolve(Rm with { DirectionId = Guid.NewGuid() }, Teams, Directions, s => s));

        // В отчёт ТП не попадают цифры коллег — даже в недельном рейтинге (только место).
        var weekly = Build("weekly.saturday", Tp(62), At(new DateOnly(2026, 10, 24), 18), day: new DateOnly(2026, 10, 24));
        var text = weekly.Plain();
        Assert.DoesNotContain("Бахтиёр", text);
        Assert.DoesNotContain("Отабек", text);
        Assert.Contains("Дилшод Рахимов", weekly.Scope);
    }

    // ---------- Формулы и порог 80% ----------

    [Fact]
    public void Numbers_percents_and_the_80_percent_threshold()
    {
        Assert.Equal("300 000 000", ReportFormat.Number(300_000_000m));
        Assert.Equal("−3 000", ReportFormat.Number(-3_000m));
        Assert.Equal("—", ReportFormat.Number((decimal?)null));
        Assert.Equal("24,7", ReportFormat.Kg(24.66m));
        Assert.Equal("+12 500", ReportFormat.Signed(12_500m));
        Assert.Equal("73%", ReportFormat.Percent(0.7349m));
        Assert.Null(ReportFormat.Share(10m, 0m)); // план 0 — выполнение неизвестно, а не бесконечность
        Assert.Null(ReportFormat.Share(10m, null));
        Assert.Equal(ReportTone.Bad, ReportFormat.Tone(0.799m));
        Assert.Equal(ReportTone.Warn, ReportFormat.Tone(0.8m));
        Assert.Equal(ReportTone.Good, ReportFormat.Tone(1m));
        Assert.Equal(ReportTone.Muted, ReportFormat.Tone(null));
        Assert.Equal(10_000_000m, ReportFacts.Daily(10_000_000m * WorkDays, Day));
        Assert.Null(ReportFacts.Daily(10_000_000m, new DateOnly(2026, 10, 25))); // воскресенье — без плана
    }

    // ---------- Построители ----------

    [Fact]
    public void Daily_plan_totals_add_up_and_values_below_80_percent_are_red()
    {
        var morning = Build("daily.morning", Sv, At(Day, 8, 30));
        var plan = morning.Tables[0];
        Assert.Equal(3, plan.Rows.Count);
        Assert.Contains(plan.Total!, c => c.Text == "60 000 000"); // 30 + 20 + 10 млн
        Assert.Equal(["1. Общий план продаж на день — сум и кг", "2. План по категориям товаров на день, сум", "3. План по категориям товаров на день, кг"],
            morning.Tables.Select(t => t.Title));
        Assert.DoesNotContain(morning.Kpis, k => k.Label.StartsWith("Факт", StringComparison.Ordinal)); // утром факта нет

        // 13:00: факт — заказы до 13:00 (заказ 14:00 не входит).
        var midday = Build("daily.midday", Sv, At(Day, 13));
        var table = midday.Tables.Single(t => t.Columns.Any(col => col.Title == "Факт 13:00, сум"));
        Assert.Contains(table.Total!, c => c.Text == "29 000 000"); // 12 + 17 млн
        // 18:00: оба среза; 61 — 107% (зелёный), 62 — 85% (жёлтый), 63 — 0% (красный).
        var evening = Build("daily.evening", Sv, At(Day, 18));
        var result = evening.Tables.Single(t => t.Columns.Any(col => col.Title == "% 18:00"));
        var share = result.Columns.ToList().FindIndex(col => col.Title == "% 18:00");
        Assert.True(share > 0, string.Join(" | ", result.Columns.Select(col => col.Title)));
        Assert.Equal(["107%", "85%", "0%"], result.Rows.Select(r => r[share].Text));
        Assert.Equal([ReportTone.Good, ReportTone.Warn, ReportTone.Bad], result.Rows.Select(r => r[share].Tone));
        Assert.Contains(result.Total!, c => c.Text == "49 000 000");
        Assert.NotEmpty(evening.Problems);
        Assert.Contains(evening.Problems, p => p.Contains("Отабек Назаров"));

        // РМ — по регионам направления; итог = сумма регионов.
        var rm = Build("daily.evening", Rm, At(Day, 18));
        var regions = rm.Tables.Single(t => t.Columns.Any(col => col.Title == "% 18:00"));
        Assert.Equal(["Хоразм", "Бухоро"], regions.Rows.Select(r => r[1].Text));
        Assert.Contains(regions.Total!, c => c.Text == "55 000 000");

        // ТП — личные цифры: 32 млн из 30 млн.
        var tp = Build("daily.evening", Tp(61), At(Day, 18));
        Assert.Contains(tp.Kpis, k => k.Value == "32 000 000" && k.Tone == ReportTone.Good);
    }

    [Fact]
    public void Tp_reminder_is_skipped_when_the_agent_already_has_orders()
    {
        var withOrders = Build("no-orders.reminder", Tp(61), At(Day, 17));
        Assert.False(withOrders.Applies);
        Assert.Contains("есть заказы", withOrders.SkipReason);

        var without = Build("no-orders.reminder", Tp(63), At(Day, 17));
        Assert.True(without.Applies);

        // СВ в 13:00 и 18:00 — список ТП без заказов: только Отабек.
        var sv = Build("no-orders.evening", Sv, At(Day, 18));
        Assert.True(sv.Applies);
        Assert.Contains("Отабек Назаров", sv.Plain());
        var status = sv.Tables[0].Rows.ToDictionary(r => r[1].Text, r => r[^2]);
        Assert.Equal(("ОК", ReportTone.Good), (status["Бахтиёр Юлдашев"].Text, status["Бахтиёр Юлдашев"].Tone));
        Assert.Equal(("Нет заказов", ReportTone.Bad), (status["Отабек Назаров"].Text, status["Отабек Назаров"].Tone));
        Assert.Contains(sv.Problems, p => p.Contains("Отабек Назаров") && !p.Contains("Бахтиёр"));

        // Напоминание 17:00 — только ТП.
        Assert.Throws<KeyNotFoundException>(() => ReportScopes.Resolve(Tp(999), Teams, Directions, s => s));
        Assert.DoesNotContain(TelegramReportRoles.Sv, ReportCatalog.Find("no-orders.reminder")!.Roles);
    }

    [Fact]
    public void Promo_categories_and_no_purchase_reports_use_only_scope_data()
    {
        var promo = Build("promo.evening", Sv, At(Day, 18));
        var text = promo.Plain();
        Assert.Contains("Осень −5%", text);
        Assert.DoesNotContain("Ручная скидка", text); // заказ с ручной скидкой — в Бухоро, не в области СВ Хоразма

        var categories = Build("categories.evening", Sv, At(Day, 18));
        Assert.Contains("Кекс", categories.Plain());
        Assert.Contains("Бамбук", categories.Plain());

        var silent = new Dictionary<long, NoPurchaseAgent>
        {
            [61] = new(61, 40, 2, 5, 1, 3_500_000,
                [new SilentMarket(2001, "Магазин «Нур»", 61, Day.AddDays(-41), 3_500_000, 41, 120, ["Кекс"], true),
                 new SilentMarket(2002, "Супермаркет «Бахор»", 61, Day.AddDays(-33), 1_200_000, 33, 40, ["Бамбук"], false)]),
            [62] = new(62, 30, 0, 4, 0, 0, []),
            [63] = new(63, 25, 0, 3, 0, 0, []),
        };
        var noPurchase = Build("no-purchase.morning", Sv, At(Day, 8, 30), noPurchase: silent);
        Assert.Contains("Магазин «Нур»", noPurchase.Plain());
        Assert.True(noPurchase.Tables.Count > 0);
    }

    [Fact]
    public void Every_report_builds_for_every_role_on_empty_data_and_in_uzbek()
    {
        foreach (var job in ReportCatalog.Jobs)
        {
            foreach (var scope in new[] { Rm, Sv, Tp(63) }.Where(s => job.Roles.Contains(s.Role)))
            {
                var now = At(Day, job.Time.Hour, job.Time.Minute);
                var empty = Build(job.Code, scope, now, Facts(empty: true),
                    noPurchase: job.Report == ReportType.NoPurchase ? new Dictionary<long, NoPurchaseAgent>() : null);
                Assert.False(string.IsNullOrWhiteSpace(empty.Title));
                Assert.DoesNotContain("NaN", empty.Plain());

                var uz = Build(job.Code, scope, now, language: "uz",
                    noPurchase: job.Report == ReportType.NoPurchase ? new Dictionary<long, NoPurchaseAgent>() : null);
                Assert.Equal("uz", uz.Language);
            }
        }

        // Заказы Linko за день не загружены — отчёт с фактом помечен неполным.
        var stale = Build("daily.evening", Sv, At(Day, 18), Facts(At(Day.AddDays(-1), 18)));
        Assert.True(stale.Incomplete);
        Assert.Contains(stale.Notes, n => n.Contains("не загружены"));

        // Визитов выполнено много, а заказов нет ни у кого — у СВ и РМ замечание «проверьте обновление заказов Linko» (нули — не факт);
        // у ТП его нет (один человек без заказов — для этого есть «ТП без заказов»), утром факта ещё нет.
        var full = Facts();
        var noOrders = new ReportFacts
        {
            Categories = full.Categories, CategoryPrice = full.CategoryPrice, Plans = full.Plans, Orders = [], OrdersAsOf = full.OrdersAsOf, VisitsAsOf = full.VisitsAsOf,
            Visits = Enumerable.Range(0, 12).Select(i => new FactVisit(61, 2000 + i, Day, new TimeOnly(9 + i % 8, 0), true, true)).ToList(),
        };
        Assert.Contains(Build("daily.evening", Sv, At(Day, 18), noOrders).Notes, n => n.Contains("проверьте обновление заказов"));
        Assert.Contains(Build("no-orders.evening", Rm, At(Day, 18), noOrders).Notes, n => n.Contains("Визитов выполнено 12"));
        Assert.DoesNotContain(Build("daily.evening", Tp(61), At(Day, 18), noOrders).Notes, n => n.Contains("проверьте обновление заказов"));
        Assert.DoesNotContain(Build("daily.morning", Sv, At(Day, 8, 30), noOrders).Notes, n => n.Contains("проверьте обновление заказов"));
        Assert.DoesNotContain(Build("daily.evening", Sv, At(Day, 18)).Notes, n => n.Contains("проверьте обновление заказов"));
    }

    [Fact]
    public void Uzbek_texts_cover_every_builder_and_renderer_string()
    {
        // Имена людей, регионов, категорий и акций — данные, они не переводятся.
        string[] data =
        [
            .. Teams.SelectMany(t => new[] { t.Name, t.Manager, t.SupervisorName }.Concat(t.Supervisors.Select(p => p.Name)).Concat(t.Agents.Select(a => a.Name))).OfType<string>(),
            .. Directions.Select(d => d.Name), "Кекс", "Бамбук", "Осень −5%",
        ];
        static bool Cyrillic(string s) => s.Any(ch => ch is >= 'А' and <= 'я' or 'Ё' or 'ё');
        var missing = new List<string>();
        foreach (var job in ReportCatalog.Jobs)
        {
            foreach (var scope in new[] { Rm, Sv, Tp(61), Tp(63) }.Where(s => job.Roles.Contains(s.Role)))
            {
                var report = Build(job.Code, scope, At(Day, job.Time.Hour, job.Time.Minute), language: "uz",
                    noPurchase: job.Report == ReportType.NoPurchase ? new Dictionary<long, NoPurchaseAgent>() : null);
                var texts = new[] { report.Title, report.Caption, report.Summary, report.Scope }
                    .Concat(report.Kpis.SelectMany(k => new[] { k.Label, k.Value, k.Hint ?? "" }))
                    .Concat(report.Tables.SelectMany(t => new[] { t.Title, t.Hint ?? "" }.Concat(t.Columns.Select(col => col.Title))
                        .Concat(t.Rows.SelectMany(r => r.Select(cell => cell.Text))).Concat(t.Total?.Select(cell => cell.Text) ?? [])))
                    .Concat(report.Problems).Concat(report.Notes).Append(report.SkipReason ?? "");
                missing.AddRange(texts.Select(t => data.Aggregate(t, (acc, d) => acc.Replace(d, ""))).Where(Cyrillic).Select(t => $"{job.Code}/{scope.Role}: {t}"));
            }
        }

        Assert.True(missing.Count == 0, string.Join(Environment.NewLine, missing.Distinct().Take(80)));
    }

    // ---------- Картинки ----------

    private static readonly SkiaReportRenderer Renderer = new();

    [Fact]
    public void Every_report_renders_as_16_by_9_png_pages_within_telegram_limits()
    {
        foreach (var job in ReportCatalog.Jobs)
        {
            foreach (var scope in new[] { Rm, Sv, Tp(61) }.Where(s => job.Roles.Contains(s.Role)))
            {
                var report = Build(job.Code, scope, At(Day, job.Time.Hour, job.Time.Minute),
                    noPurchase: job.Report == ReportType.NoPurchase ? new Dictionary<long, NoPurchaseAgent>() : null);
                var pages = Renderer.Render(report);
                Assert.NotEmpty(pages);
                foreach (var png in pages)
                {
                    using var bitmap = SKBitmap.Decode(png);
                    Assert.Equal((1920, 1080), (bitmap.Width, bitmap.Height));
                    Assert.True(png.Length < 10 * 1024 * 1024);
                }
            }
        }
    }

    [Fact]
    public void Many_rows_split_into_pages_and_test_sends_are_marked()
    {
        var agents = Enumerable.Range(0, 45).Select(i => new ReportPerson(500 + i, $"Агент с очень длинным именем и фамилией номер {i + 1}")).ToList();
        var team = Teams[0] with { Agents = agents };
        var plans = agents.ToDictionary(a => (a.Id, 2026, 10), _ => new MonthPlan(1_000_000m * WorkDays, 50m * WorkDays, 10, new Dictionary<string, decimal>()));
        var facts = new ReportFacts
        {
            Categories = [], CategoryPrice = new Dictionary<string, decimal>(), Plans = plans, Visits = [], OrdersAsOf = At(Day, 17, 55),
            Orders = agents.Select((a, i) => new FactOrder(9000 + i, a.Id, 3000 + i, Day, new TimeOnly(10, 0), 500_000 + i * 30_000, 20, new Dictionary<string, CategoryAmount>(), [])).ToList(),
        };
        var info = ReportCatalog.Find("daily.evening")!;
        var resolved = ReportScopes.Resolve(Sv, [team], Directions, s => s);
        var report = DailyPlanReport.Build(new ReportContext(info, resolved, Day, At(Day, 18), "ru", facts, new TimeOnly(13, 0), TimeOnly.MaxValue));
        var pages = Renderer.Render(report);
        Assert.True(pages.Count >= 2, $"страниц: {pages.Count}");
        Assert.True(pages.Count <= 10); // альбом Telegram — до 10 фото

        var test = Renderer.Render(report with { Test = true });
        Assert.NotEqual(Convert.ToBase64String(pages[0]), Convert.ToBase64String(test[0])); // полоса «ТЕСТОВАЯ ОТПРАВКА»

        // Резерв без картинки — текст со всеми ТП и итогом.
        var plain = report.Plain();
        Assert.Contains("номер 45", plain);
        Assert.Contains("Итого по региону", plain);
    }
}
