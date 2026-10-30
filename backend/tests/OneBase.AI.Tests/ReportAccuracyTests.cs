using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Field;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Telegram.Battle;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Battle;
using OneBase.Domain.Sales;
using OneBase.Domain.Telegram;
using OneBase.Infrastructure.Persistence;
using OneBase.Infrastructure.Telegram.Reports;
using SilentMarket = OneBase.Application.Telegram.Reports.SilentMarket;

namespace OneBase.AI.Tests;

/// <summary>
/// Точность автоотчётов по разбору рассылки 29.10.2026: ТП по номеру (ТП2 раньше ТП10), вакансии Linko названы, а не пропадают молча,
/// план без кг — без пустых колонок кг и средней цены, исключения «Поля боя» по ID Linko.
/// </summary>
public class ReportAccuracyTests
{
    private static readonly DateOnly Day = new(2026, 10, 29); // четверг
    private static readonly Guid Tashkent = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly int WorkDays = FieldKpi.WorkingDays(2026, 10);

    private static readonly IReadOnlyList<ReportRegionTeam> Teams =
    [
        new(Tashkent, "Тошкент", null, "Азиз Каримов", [new ReportPerson(71, "Азиз Каримов")],
            [new ReportPerson(61, "ТП1 Бахтиёр Тестов"), new ReportPerson(62, "ТП2 Дилшод Тестов")], null, null,
            [new ReportVacancy(536, "ТП3 вакант Тестовый", 276), new ReportVacancy(542, "ТП9 Вакант Тестовый", 200)]),
    ];

    private static ReportFacts Facts(bool withKg) => new()
    {
        Categories = [],
        CategoryPrice = new Dictionary<string, decimal>(),
        Plans = new Dictionary<(long, int, int), MonthPlan>
        {
            [(61, 2026, 10)] = new(30_000_000m * WorkDays, withKg ? 1_000m * WorkDays : 0, 120, new Dictionary<string, decimal>()),
            [(62, 2026, 10)] = new(20_000_000m * WorkDays, withKg ? 700m * WorkDays : 0, 90, new Dictionary<string, decimal>()),
        },
        Orders = [],
        Visits = [],
        OrdersAsOf = new DateTimeOffset(Day.ToDateTime(new TimeOnly(8, 0)), TimeSpan.FromHours(5)),
        VisitsAsOf = new DateTimeOffset(Day.ToDateTime(new TimeOnly(8, 0)), TimeSpan.FromHours(5)),
    };

    private static ReportDocument Build(string job, ReportScope scope, ReportFacts facts, string language = "ru", IReadOnlyDictionary<long, NoPurchaseAgent>? noPurchase = null)
    {
        var info = ReportCatalog.Find(job)!;
        var resolved = ReportScopes.Resolve(scope, Teams, [], ru => ReportTexts.Get(language, ru));
        var now = new DateTimeOffset(Day.ToDateTime(info.Time), TimeSpan.FromHours(5));
        var context = new ReportContext(info, resolved, Day, now, language, facts, new TimeOnly(13, 0), info.IsMidday ? info.Time : TimeOnly.MaxValue, 30, noPurchase, info.Time);
        return info.Report switch
        {
            ReportType.NoPurchase => NoPurchaseReport.Build(context),
            ReportType.NoOrders => NoOrdersReport.Build(context),
            ReportType.Weekly => WeeklyReport.Build(context),
            ReportType.Categories => CategoriesReport.Build(context),
            _ => DailyPlanReport.Build(context),
        };
    }

    private static readonly ReportScope Sv = new(TelegramReportRoles.Sv, RegionId: Tashkent, SupervisorId: 71);

    [Fact]
    public void Names_with_numbers_sort_by_value()
    {
        string[] names = ["ТП10 Азиз", "ТП2 Бобур", "ТП11", "тп3 Вали", "ТП1 Тимур", "ТП16", "Анвар", "ТП02 Ноль"];
        Assert.Equal(["Анвар", "ТП1 Тимур", "ТП2 Бобур", "ТП02 Ноль", "тп3 Вали", "ТП10 Азиз", "ТП11", "ТП16"], names.Order(NaturalNameComparer.Instance));
    }

    [Fact]
    public void Daily_plan_without_kg_hides_kg_columns_and_names_vacancies()
    {
        var plain = Build("daily.morning", Sv, Facts(withKg: false));
        var summary = plain.Tables[0];
        Assert.Equal("1. Общий план продаж на день — сум", summary.Title);
        Assert.DoesNotContain(summary.Columns, col => col.Title is "План, кг" or "Ср. цена, сум/кг");
        Assert.All(summary.Rows, r => Assert.Equal(summary.Columns.Count, r.Count));
        Assert.Contains(plain.Notes, n => n.Contains("нет плана в кг"));
        Assert.Contains(plain.Notes, n => n == "Вакансии без ТП (не входят в план, базу и маршрут): ТП3 вакант Тестовый — 276 точек, ТП9 Вакант Тестовый — 200 точек");
        Assert.DoesNotContain(" кг", plain.Caption.Split('\n')[^1]);
        Assert.Null(plain.Kpis.First(k => k.Label == "План дня, сум").Hint);

        var kg = Build("daily.morning", Sv, Facts(withKg: true));
        Assert.Equal("1. Общий план продаж на день — сум и кг", kg.Tables[0].Title);
        Assert.Contains(kg.Tables[0].Columns, col => col.Title == "План, кг");
        Assert.DoesNotContain(kg.Notes, n => n.Contains("нет плана в кг"));
        Assert.Contains("кг", kg.Caption);

        // ТП — без строки кг утром и без «/ — кг» в 13:00; вакансий у ТП нет: его отчёт только о нём.
        var tp = new ReportScope(TelegramReportRoles.Tp, AgentId: 61);
        var morning = Build("daily.morning", tp, Facts(withKg: false));
        Assert.DoesNotContain(morning.Tables[0].Rows, r => r[1].Text == "Продажи, кг");
        Assert.DoesNotContain("кг", morning.Caption);
        Assert.DoesNotContain(morning.Notes, n => n.Contains("Вакансии"));
        var midday = Build("daily.midday", tp, Facts(withKg: false));
        Assert.DoesNotContain("сум / ", midday.Caption);
        Assert.Contains(midday.Tables[0].Rows, r => r[1].Text == "Продажи, кг"); // факт в кг днём есть

        // Узбекский — все новые строки переведены.
        static bool Cyrillic(string s) => s.Any(ch => ch is >= 'А' and <= 'я' or 'Ё' or 'ё');
        string[] data = ["Тошкент", "Азиз Каримов", "ТП1 Бахтиёр Тестов", "ТП2 Дилшод Тестов", "ТП3 вакант Тестовый", "ТП9 Вакант Тестовый"];
        foreach (var doc in new[] { Build("daily.morning", Sv, Facts(false), "uz"), Build("daily.midday", tp, Facts(false), "uz"), Build("daily.morning", tp, Facts(false), "uz") })
        {
            var texts = new[] { doc.Title, doc.Caption, doc.Summary }.Concat(doc.Notes).Concat(doc.Tables.Select(t => t.Title)).Concat(doc.Tables.SelectMany(t => t.Columns.Select(col => col.Title)));
            var missing = texts.Select(t => data.Aggregate(t, (acc, d) => acc.Replace(d, ""))).Where(Cyrillic).ToList();
            Assert.True(missing.Count == 0, string.Join(Environment.NewLine, missing));
        }
    }

    /// <summary>Как у большинства регионов в Linko: план только в кг (61 — 1 000 кг/день, 62 — 700), заказы дня 61 — 800 кг, 62 — 210 кг.</summary>
    private static ReportFacts KgOnly() => new()
    {
        Categories = [],
        CategoryPrice = new Dictionary<string, decimal>(),
        Plans = new Dictionary<(long, int, int), MonthPlan>
        {
            [(61, 2026, 10)] = new(null, 1_000m * WorkDays, 120, new Dictionary<string, decimal>()),
            [(62, 2026, 10)] = new(null, 700m * WorkDays, 90, new Dictionary<string, decimal>()),
        },
        Orders =
        [
            new(1, 61, 9001, Day, new TimeOnly(10, 0), 24_000_000, 800, new Dictionary<string, CategoryAmount>(), []),
            new(2, 62, 9002, Day, new TimeOnly(15, 0), 6_300_000, 210, new Dictionary<string, CategoryAmount>(), []),
        ],
        Visits = [],
        OrdersAsOf = new DateTimeOffset(Day.ToDateTime(new TimeOnly(17, 55)), TimeSpan.FromHours(5)),
        VisitsAsOf = new DateTimeOffset(Day.ToDateTime(new TimeOnly(17, 55)), TimeSpan.FromHours(5)),
    };

    [Fact]
    public void Plan_only_in_kg_is_a_plan_and_performance_is_counted_in_kg()
    {
        var morning = Build("daily.morning", Sv, KgOnly());
        Assert.Equal("1. Общий план продаж на день — кг", morning.Tables[0].Title);
        Assert.DoesNotContain(morning.Tables[0].Columns, col => col.Title is "План, сум" or "Ср. цена, сум/кг");
        Assert.Contains("План дня: 1 700 кг", morning.Caption);
        Assert.DoesNotContain(morning.Notes, n => n.StartsWith("Нет плана на день")); // план есть — в кг
        Assert.Contains(morning.Notes, n => n == "В Linko план только в кг — выполнение считается по кг");
        Assert.Contains(morning.Kpis, k => k.Label == "План дня, кг" && k.Value == "1 700");

        // Вечер: выполнение по кг (1 010 из 1 700 = 59%), ниже 80% — 62 (210 из 700 = 30%).
        var evening = Build("daily.evening", Sv, KgOnly());
        Assert.Contains("Итог дня: 1 010 кг (59%), 30 300 000 сум", evening.Caption);
        Assert.Contains(evening.Kpis, k => k.Label == "Выполнение" && k.Value == "59%");
        Assert.Contains(evening.Problems, p => p.StartsWith("Ниже 80% плана: ТП2 Дилшод Тестов 30%"));
        var tp = Build("daily.evening", new ReportScope(TelegramReportRoles.Tp, AgentId: 61), KgOnly());
        Assert.Contains("Итог дня: 800 кг (80%), 24 000 000 сум", tp.Caption);

        // «ТП без заказов» считает план дня в сумах: у кого план только в кг — не «нет плана», а другая мера.
        var noOrders = Build("no-orders.evening", Sv, KgOnly(), noPurchase: null);
        Assert.DoesNotContain(noOrders.Notes, n => n.StartsWith("Нет плана месяца в Linko"));
        Assert.Contains(noOrders.Notes, n => n.StartsWith("План месяца в Linko только в кг:"));
        // Напоминание 13:00 ТП (у 62 первый заказ в 15:00): план дня в кг, а не «— сум».
        var reminder = Build("no-orders.midday", new ReportScope(TelegramReportRoles.Tp, AgentId: 62), KgOnly());
        Assert.Contains("План на день: 700 кг.", reminder.Caption);
        Assert.DoesNotContain("— сум", reminder.Caption);

        // Плана в Linko нет совсем — так и написано, без «— сум» и без «план только в сумах».
        var none = new ReportFacts
        {
            Categories = [], CategoryPrice = new Dictionary<string, decimal>(), Plans = new Dictionary<(long, int, int), MonthPlan>(), Orders = KgOnly().Orders, Visits = [],
            OrdersAsOf = KgOnly().OrdersAsOf, VisitsAsOf = KgOnly().VisitsAsOf,
        };
        var empty = Build("daily.morning", Sv, none);
        Assert.Contains("Плана на день в Linko нет", empty.Caption);
        Assert.DoesNotContain(empty.Notes, n => n.Contains("план только в"));
        Assert.Contains(empty.Notes, n => n.StartsWith("Нет плана на день в Linko: ТП1 Бахтиёр Тестов, ТП2 Дилшод Тестов"));
        Assert.Contains("(плана в Linko нет)", Build("daily.evening", new ReportScope(TelegramReportRoles.Tp, AgentId: 61), none).Caption);
        Assert.Contains("(плана в Linko нет)", Build("daily.midday", Sv, none, "uz").Caption.Replace("(Linko'da reja yo'q)", "(плана в Linko нет)"));

        // Узбекский — все новые строки переведены.
        static bool Cyrillic(string s) => s.Any(ch => ch is >= 'А' and <= 'я' or 'Ё' or 'ё');
        string[] data = ["Тошкент", "Азиз Каримов", "ТП1 Бахтиёр Тестов", "ТП2 Дилшод Тестов", "ТП3 вакант Тестовый", "ТП9 Вакант Тестовый"];
        foreach (var job in new[] { "daily.morning", "daily.midday", "daily.evening", "no-orders.evening", "weekly.saturday", "categories.evening" })
        {
            foreach (var scope in new[] { Sv, new ReportScope(TelegramReportRoles.Tp, AgentId: 61) })
            {
                var doc = Build(job, scope, KgOnly(), "uz");
                var texts = new[] { doc.Title, doc.Caption, doc.Summary }.Concat(doc.Notes).Concat(doc.Problems).Concat(doc.Kpis.SelectMany(k => new[] { k.Label, k.Value, k.Hint ?? "" }))
                    .Concat(doc.Tables.SelectMany(t => new[] { t.Title, t.Hint ?? "" }.Concat(t.Columns.Select(col => col.Title))));
                var missing = texts.Select(t => data.Aggregate(t, (acc, d) => acc.Replace(d, ""))).Where(Cyrillic).ToList();
                Assert.True(missing.Count == 0, job + ": " + string.Join(Environment.NewLine, missing));
            }
        }
    }

    [Fact]
    public void Weekly_report_ranks_by_kg_when_plan_is_only_in_kg()
    {
        var info = ReportCatalog.Find("weekly.saturday")!;
        var saturday = new DateOnly(2026, 10, 31);
        var facts = KgOnly();
        var resolved = ReportScopes.Resolve(Sv, Teams, [], s => s);
        var context = new ReportContext(info, resolved, saturday, new DateTimeOffset(saturday.ToDateTime(info.Time), TimeSpan.FromHours(5)), "ru",
            new ReportFacts
            {
                Categories = facts.Categories, CategoryPrice = facts.CategoryPrice, Plans = facts.Plans, Orders = facts.Orders, Visits = facts.Visits,
                OrdersAsOf = new DateTimeOffset(saturday.ToDateTime(new TimeOnly(17, 55)), TimeSpan.FromHours(5)),
            }, new TimeOnly(13, 0), TimeOnly.MaxValue, 30, null, info.Time);
        var week = WeeklyReport.Build(context);
        var rating = week.Tables[0];
        Assert.Equal(["ТП1 Бахтиёр Тестов", "ТП2 Дилшод Тестов"], rating.Rows.Select(r => r[1].Text)); // 800 из 6 000 кг впереди 210 из 4 200
        Assert.Equal("Отсортировано по % выполнения плана (кг)", rating.Hint);
        Assert.Contains("Факт 1 010 кг", week.Caption);
        Assert.Contains(week.Kpis, k => k.Label == "Факт недели, кг");
        Assert.Contains(week.Tables, t => t.Title == "Продажи по дням недели, кг");
    }

    [Fact]
    public void No_purchase_report_names_its_basis_and_vacancies()
    {
        var silent = new Dictionary<long, NoPurchaseAgent>
        {
            [61] = new(61, 40, 1, 5, 1, 900_000, [new SilentMarket(2001, "Магазин «Нур»", 61, Day.AddDays(-41), 900_000, 41, 120, [], true)]),
            [62] = new(62, 30, 1, 4, 0, 0, [new SilentMarket(2002, "Магазин «Разовый»", 62, Day.AddDays(-35), 5_000_000, 35, 15, [], false)]),
        };
        var doc = Build("no-purchase.morning", Sv, Facts(false), noPurchase: silent);
        // Крупнейшие — по среднему закупу (120 кг/мес), а не по одному большому последнему заказу (5 млн при 15 кг/мес).
        var biggest = doc.Tables.Single(t => t.Title.StartsWith("2. Крупнейшие"));
        Assert.Equal(["Магазин «Нур»", "Магазин «Разовый»"], biggest.Rows.Select(r => r[1].Text));
        Assert.Contains(doc.Notes, n => n.Contains("возвращённый целиком, покупкой не считается"));
        Assert.Contains(doc.Notes, n => n.StartsWith("Вакансии без ТП"));
        Assert.DoesNotContain("Активная база", doc.Plain());
        Assert.Contains("База: закуп за 12 мес., точек", doc.Plain());

        // Картинка с длинными заголовками колонок рисуется (заголовок — до трёх строк).
        var pages = new SkiaReportRenderer().Render(doc);
        Assert.NotEmpty(pages);
    }

    [Fact]
    public void Battle_exclusions_match_linko_id_and_name()
    {
        // «534» — ID ТП в Linko: имя из отчёта без номера («ТП1 Тестовый») не мешает, чужой ID с теми же цифрами — не совпадает.
        Assert.True(BattleRules.MentionsAgent(534, "", ["534"]));
        Assert.False(BattleRules.MentionsAgent(1534, "1534 ТП", ["534"]));
        Assert.True(BattleRules.MentionsAgent(77, "77 ТП OPTOM bozor", ["OPTOM", "BOZOR"]));
        Assert.False(BattleRules.MentionsAgent(77, "77 ТП Чилонзор", ["OPTOM", "BOZOR", "534"]));
    }
}

/// <summary>
/// На PostgreSQL: «Клиенты без закупа» вычитают возвраты (целиком возвращённый заказ — не покупка) и ставят крупных первыми; «Поле боя» не
/// ставит целей задним числом и не ставит Могул ТП 534 (ТЗ), даже когда в отчёт пришло имя без номера.
/// </summary>
public class ReportAccuracyDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    [PostgresFact]
    public async Task Month_plan_is_the_total_indicator_not_the_sum_with_its_parts()
    {
        // Ташкент: «Тотал» + Twizos + Могуль (части «Тотала») в сумах; кг — «Тотал Вес 6 млн» + Twizos + Могуль. Термез: только категории.
        const long tashkent = 900_881, termez = 900_882;
        SalesStaffPlan Row(long user, string type, string name, decimal plan, decimal fact) => new()
        {
            Year = 2026, Month = 10, LinkoUserId = user, IndicatorId = name.GetHashCode() & 0xFFFF, IndicatorName = name, PlanType = type, PlanAmount = plan, FactAmount = fact,
            LoadedAt = DateTimeOffset.UtcNow,
        };
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            db.SalesStaffPlans.AddRange(
                Row(tashkent, StaffPlanTypes.SalesSum, "Октябрь Тотал Ташкент", 100_000_000, 40_000_000),
                Row(tashkent, StaffPlanTypes.SalesSum, "Октябрь Twizos Ташкент", 12_000_000, 5_000_000),
                Row(tashkent, StaffPlanTypes.SalesSum, "Октябрь Могуль Ташкент", 30_000_000, 2_000_000),
                Row(tashkent, StaffPlanTypes.SalesWeight, "Октябрь Тотал Вес 6 млн", 5_000, 3_000),
                Row(tashkent, StaffPlanTypes.SalesWeight, "Октябрь Twizos Вес 6 млн", 200, 10),
                Row(tashkent, StaffPlanTypes.SalesWeight, "Октябрь Могуль Вес 6 млн", 1_000, 400),
                Row(tashkent, StaffPlanTypes.ActiveClients, "Октябрь АКБ Ташкент", 100, 50),
                Row(termez, StaffPlanTypes.SalesWeight, "Октябрь Бамбук Термез 600", 600, 300),
                Row(termez, StaffPlanTypes.SalesWeight, "Октябрь Кекс 1 кг Термез 500", 500, 200));
            await db.SaveChangesAsync();
        }

        Dictionary<long, AgentMonthPlan> plans;
        using (var scope = fixture.Services.CreateScope())
        {
            plans = (await scope.ServiceProvider.GetRequiredService<ILinkoSalesProvider>().AgentPlansAsync([tashkent, termez], 2026, 10, CancellationToken.None))
                .ToDictionary(p => p.LinkoUserId);
        }

        Assert.Equal((100_000_000m, 5_000m, 100m), (plans[tashkent].PlanSum, plans[tashkent].PlanKg, plans[tashkent].PlanAkb));
        Assert.Equal((40_000_000m, 3_000m), (plans[tashkent].LinkoFactSum, plans[tashkent].LinkoFactKg));
        Assert.Equal((1_100m, (decimal?)null), (plans[termez].PlanKg, plans[termez].PlanSum)); // без «Тотала» — сумма категорий
        Assert.True(KpiMath.IsTotalIndicator("Октябрь Тотал Сумма Сырдаре"));
        Assert.False(KpiMath.IsTotalIndicator("Октябрь Могуль Ташкент Супер"));
    }

    [PostgresFact]
    public async Task No_purchase_nets_returns_and_sorts_by_average_purchase()
    {
        var day = new DateOnly(2026, 10, 29);
        const long agent = 900_771;
        long[] ids = [9_700_001, 9_700_002, 9_700_003];
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            db.LinkoMarkets.AddRange(ids.Select((id, i) => new LinkoMarket { Id = id, Name = $"Магазин {i + 1}", MarketTypeName = "Розница", ResponsibleAgentId = agent }));
            LinkoOrder Order(long id, long market, int daysAgo, decimal sum, decimal kg)
            {
                var d = day.AddDays(-daysAgo);
                return new LinkoOrder
                {
                    Id = id, MarketId = market, AgentId = agent, Status = "delivered", CreatedAt = d.ToDateTime(new TimeOnly(10, 0)), CreatedDate = d,
                    AcceptedAt = d.ToDateTime(new TimeOnly(12, 0)), AcceptedDate = d, TotalPrice = sum, TotalWeight = kg,
                };
            }

            // Магазин 1: 70 дней назад — 1 000 000 (10 кг); 40 дней назад — 4 200 000, возвращён целиком → последняя покупка — 70 дней назад.
            // Магазин 2: 50 дней назад — 2 000 000 (100 кг), возврат 500 000 (20 кг) → последняя покупка 1 500 000, 80 кг.
            // Магазин 3: покупка 10 дней назад — не «без закупа».
            db.LinkoOrders.AddRange(Order(9_710_001, ids[0], 70, 1_000_000, 10), Order(9_710_002, ids[0], 40, 4_200_000, 50), Order(9_710_003, ids[1], 50, 2_000_000, 100),
                Order(9_710_004, ids[2], 10, 700_000, 7));
            db.LinkoOrderReturns.AddRange(
                new LinkoOrderReturn
                {
                    Id = 9_720_001, CreatedDate = day.AddDays(-35), Status = "delivered", MarketId = ids[0], AgentId = agent, TotalPrice = 4_200_000, TotalWeight = 50,
                    Lines = [new LinkoOrderReturnLine { Id = 9_721_001, OrderId = 9_710_002, Amount = 1, Price = 4_200_000, TotalWeight = 50 }],
                },
                new LinkoOrderReturn
                {
                    Id = 9_720_002, CreatedDate = day.AddDays(-45), Status = "delivered", MarketId = ids[1], AgentId = agent, TotalPrice = 500_000, TotalWeight = 20,
                    Lines = [new LinkoOrderReturnLine { Id = 9_721_002, OrderId = 9_710_003, Amount = 10, Price = 50_000, TotalWeight = 20 }],
                });
            await db.SaveChangesAsync();
        }

        NoPurchaseAgent result;
        using (var scope = fixture.Services.CreateScope())
        {
            result = (await scope.ServiceProvider.GetRequiredService<ReportDataService>().NoPurchaseAsync([agent], day, 30, [], CancellationToken.None))[agent];
        }

        Assert.Equal(3, result.Base);
        Assert.Equal(["Магазин 2", "Магазин 1"], result.Markets.Select(m => m.Name)); // 80 кг крупнее 10 кг
        var shop1 = result.Markets.Single(m => m.Name == "Магазин 1");
        Assert.Equal((day.AddDays(-70), 1_000_000m, 70), (shop1.LastPurchase, shop1.LastSum, shop1.Days));
        var shop2 = result.Markets.Single(m => m.Name == "Магазин 2");
        Assert.Equal((1_500_000m, Math.Round(80m / 3, 1)), (shop2.LastSum, shop2.KgPerMonth));
    }

    [PostgresFact]
    public async Task Battle_goals_start_from_the_card_day_and_skip_mogul_for_agent_534()
    {
        var thursday = new DateOnly(2026, 11, 5);
        var monday = BattleRules.Monday(thursday);
        fixture.Clock.Now = new DateTimeOffset(2026, 11, 5, 3, 30, 0, TimeSpan.Zero); // четверг 08:30 Ташкент
        const long agent = 534;
        long[] ids = [9_800_001, 9_800_002, 9_800_003, 9_800_004];
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            db.LinkoUsers.Add(new LinkoUser { Id = agent, FirstName = "534 ТП1 Тестовый", SecondName = "Тестов", Username = "tp-test", IsActive = true });
            db.LinkoPromotions.Add(new LinkoPromotion { Id = 9_800_151, Name = "Акция на Могул Ноябрь 2026г. 5% \"H\"", Type = "discount", BeginDate = monday, TillDate = monday.AddDays(30) });
            for (var i = 0; i < ids.Length; i++)
            {
                db.LinkoMarkets.Add(new LinkoMarket { Id = ids[i], Name = $"Точка {i + 1}", MarketTypeName = "Розница", ResponsibleAgentId = agent, Lat = 41.3 + i * 0.01, Lon = 69.2 });
                var monthly = i == 3 ? 20_000m : 1_200_000m; // четвёртая — ниже активной базы
                foreach (var m in new[] { 1, 2, 3 })
                {
                    var past = monday.AddMonths(-m);
                    db.LinkoOrders.Add(new LinkoOrder
                    {
                        Id = 9_810_000 + i * 10 + m, MarketId = ids[i], AgentId = agent, Status = "delivered", CreatedAt = past.ToDateTime(new TimeOnly(10, 0)), CreatedDate = past,
                        AcceptedAt = past.ToDateTime(new TimeOnly(12, 0)), AcceptedDate = past, TotalPrice = monthly * 4 / 3, TotalWeight = 10,
                    });
                }

                // План Linko: все точки и в понедельник (уже прошёл), и в четверг.
                foreach (var d in new[] { monday, thursday })
                {
                    db.LinkoVisits.Add(new LinkoVisit { Id = 9_820_000 + i * 10 + d.Day, UserId = agent, MarketId = ids[i], Status = "pending", IsInPlan = true, Date = d.ToDateTime(TimeOnly.MinValue), Day = d });
                }
            }

            await db.SaveChangesAsync();
        }

        BattleDay day;
        using (var scope = fixture.Services.CreateScope())
        {
            // В отчёт имя приходит без номера Linko — исключение должно сработать по ID.
            day = await scope.ServiceProvider.GetRequiredService<BattleGoalService>().DayAsync(agent, "ТП1 Тестовый Тестов", "Тошкент", thursday, null, false, CancellationToken.None);
        }

        Assert.Equal(3, day.Stores.Count);
        Assert.Equal(4, day.RoutePoints);
        Assert.Equal(3, day.TwizosTotal);
        Assert.Equal(0, day.MogulTotal); // ТЗ: цели Могул не ставятся ТП 534 (своя механика)

        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            Assert.False(await db.BattleGoals.AnyAsync(g => g.AgentId == agent && g.Date < thursday));
            Assert.False(await db.BattleGoals.AnyAsync(g => g.AgentId == agent && g.Kind == BattleGoalKind.Mogul));
            // Понедельник задним числом не поставлен — в долю бонуса недели не лёг: в ней только цели с четверга.
            Assert.Equal(await db.BattleGoals.CountAsync(g => g.AgentId == agent && g.Week == monday), day.Week.Total);
        }

        var card = new SkiaReportRenderer();
        var info = ReportCatalog.Find("battle.morning")!;
        var team = new ReportRegionTeam(Guid.NewGuid(), "Тошкент", null, null, [], [new ReportPerson(agent, "ТП1 Тестовый Тестов")]);
        var resolved = ReportScopes.Resolve(new ReportScope(TelegramReportRoles.Tp, AgentId: agent), [team], [], s => s);
        var facts = new ReportFacts
        {
            Categories = [], CategoryPrice = new Dictionary<string, decimal>(), Plans = new Dictionary<(long, int, int), MonthPlan>(), Orders = [], Visits = [],
            OrdersAsOf = fixture.Clock.Now, VisitsAsOf = fixture.Clock.Now,
        };
        var context = new ReportContext(info, resolved, thursday, fixture.Clock.Now!.Value, "ru", facts, new TimeOnly(13, 0), TimeOnly.MaxValue, 30, null, info.Time);
        var options = new BattleOptions();
        var doc = BattleReport.Build(context, new BattleCards("morning", [day], [], options.TwizosSets.Select(s => new BattlePrizeTile(s.Level, s.Sum, s.Contents, s.Prize)).ToList(),
            "Могул", options.Bonus, "ru"));
        Assert.Contains("В маршруте 4 точек, цели — в 3 магазинах: 3 целей.", doc.Caption);
        Assert.Contains(doc.Kpis, k => k.Label == "Магазины с целями — пройдено" && k.Value == "0 / 3" && k.Hint == "в маршруте 4 точек");
        Assert.NotEmpty(card.Render(doc));
    }
}
