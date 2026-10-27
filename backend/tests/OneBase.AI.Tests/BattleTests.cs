using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Telegram.Battle;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Battle;
using OneBase.Domain.Sales;
using OneBase.Domain.Telegram;
using OneBase.Infrastructure.Persistence;
using OneBase.Infrastructure.Telegram.Reports;
using SkiaSharp;

namespace OneBase.AI.Tests;

/// <summary>
/// «Поле боя» без базы: правила ТЗ v2 (наборы Твизос по закупу, сетка Могул, следующий порог, лестница бонуса и уровни, разбор акций
/// Linko, порядок точек), документ отчёта (подписи RU + UZ, пропуск без целей и без заказов), карточки PNG 1080 px для дня, полдня, итога
/// дня и недели (ТП и СВ, тест, узбекский, деление на страницы).
/// </summary>
public class BattleTests
{
    private static readonly BattleOptions O = new();
    private static readonly DateOnly Day = new(2026, 11, 2); // понедельник

    [Fact]
    public void Twizos_set_follows_average_purchase_and_next_level_after_taken_invoice()
    {
        Assert.Null(BattleRules.TwizosSet(O, 250_000));
        Assert.Equal("300", BattleRules.TwizosSet(O, 300_000)!.Level);
        Assert.Equal("300", BattleRules.TwizosSet(O, 999_999)!.Level);
        Assert.Equal("400", BattleRules.TwizosSet(O, 1_000_000)!.Level);
        Assert.Equal("600", BattleRules.TwizosSet(O, 3_000_000)!.Level);
        Assert.Equal("600", BattleRules.TwizosSet(O, 50_000_000)!.Level);

        // Уровень 400 уже брала одной накладной (392 200) — предлагаем 600; взяла и 600 — цели нет.
        Assert.Equal("600", BattleRules.TwizosSetToOffer(O, 1_500_000, 392_200)!.Level);
        Assert.Equal("400", BattleRules.TwizosSetToOffer(O, 1_500_000, 392_199)!.Level);
        Assert.Null(BattleRules.TwizosSetToOffer(O, 1_500_000, 584_600));
    }

    [Fact]
    public void Mogul_grid_gives_category_thresholds_and_volume_target()
    {
        Assert.Equal("H", BattleRules.MogulTier(O, 0).Letter);
        Assert.Equal("G", BattleRules.MogulTier(O, 150_000).Letter);
        Assert.Equal("E", BattleRules.MogulTier(O, 700_000).Letter);
        Assert.Equal("A", BattleRules.MogulTier(O, 12_000_000).Letter);

        var e = BattleRules.MogulTierByLetter(O, "e")!;
        Assert.Equal((1_500_000m, 5, 2_500_000m, 8), BattleRules.MogulVolumeTarget(O, e, 1_000_000)!.Value);
        Assert.Equal((2_500_000m, 8, (decimal?)null, (int?)null), BattleRules.MogulVolumeTarget(O, e, 1_500_000)!.Value);
        Assert.Null(BattleRules.MogulVolumeTarget(O, e, 2_500_000));
        var g = BattleRules.MogulTierByLetter(O, "G")!;
        Assert.Equal(10, BattleRules.MogulVolumeTarget(O, g, 400_000)!.Value.Percent);
    }

    [Fact]
    public void Bonus_ladder_levels_and_next_step()
    {
        Assert.Equal(0, BattleRules.Bonus(O, 29, 120));
        Assert.Equal(500_000, BattleRules.Bonus(O, 30, 120));
        Assert.Equal(1_000_000, BattleRules.Bonus(O, 52, 120));
        Assert.Equal(2_000_000, BattleRules.Bonus(O, 78, 120));
        Assert.Equal(0, BattleRules.Bonus(O, 0, 0));

        var next = BattleRules.NextStep(O, 12, 120)!.Value;
        Assert.Equal((25, 18), (next.Step.Percent, next.Goals));
        Assert.Null(BattleRules.NextStep(O, 120, 120));
        Assert.Equal(["Новобранец", "Воин", "Командир", "Легенда"], new[] { 10, 30, 55, 70 }.Select(t => BattleRules.Level(O, t, 100)));
    }

    [Fact]
    public void Linko_mogul_promotions_are_recognised_by_name()
    {
        Assert.Equal(("A", 5), BattleRules.ParseMogulPromo(O, "Акция на Могул Октябрь 2026г. 5% \"A\"")!.Value);
        Assert.Equal(("G", 10), BattleRules.ParseMogulPromo(O, "Акция на Могул Ноябрь 2026г. 10% «g»")!.Value);
        Assert.Null(BattleRules.ParseMogulPromo(O, "twizos 3+1 Октябрь шашлык"));
        Assert.Null(BattleRules.ParseMogulPromo(O, "Могул распродажа 7%"));
    }

    [Fact]
    public void Route_order_is_nearest_neighbour_from_the_north_with_unlocated_last()
    {
        var points = new[] { (Id: 1, Lat: 40.0, Lon: 69.0), (Id: 2, Lat: 40.3, Lon: 69.0), (Id: 3, Lat: 40.1, Lon: 69.05), (Id: 4, Lat: (double?)null, Lon: (double?)null), (Id: 5, Lat: 40.29, Lon: 69.3) };
        var ordered = BattleRules.OrderByRoute(points.Select(p => (p.Id, Lat: (double?)p.Lat, p.Lon)).ToList(), p => (p.Lat, p.Lon));
        Assert.Equal([2, 3, 1, 5, 4], ordered.Select(p => p.Id));
        Assert.Equal(new DateOnly(2026, 10, 26), BattleRules.Monday(new DateOnly(2026, 10, 31)));
        Assert.Equal(new DateOnly(2026, 10, 26), BattleRules.Monday(new DateOnly(2026, 10, 26)));
    }

    // ---------- Документ и карточки на подставных данных ----------

    private static readonly Guid Region = Guid.Parse("00000000-0000-0000-0000-000000000005");
    private static readonly IReadOnlyList<ReportRegionTeam> Teams =
        [new(Region, "Сырдарья", "Рустам Ахмедов", "Азиз Каримов", [new ReportPerson(71, "Азиз Каримов")], [new ReportPerson(413, "Дилноза Тестова"), new ReportPerson(411, "Бахтиёр Тестов")])];

    private static BattleGoalView Goal(string kind, string sub, string level, decimal target, string status = BattleGoalStatus.Pending, decimal? taken = null, int? takenPercent = null) =>
        new(Guid.NewGuid(), kind, sub, level, target, kind == BattleGoalKind.Mogul ? target * 2 : null, kind == BattleGoalKind.Mogul ? 5 : 0, kind == BattleGoalKind.Mogul ? 8 : null,
            kind == BattleGoalKind.Twizos ? "604" : null, status, taken, takenPercent);

    private static BattleStore Store(int order, string name, string status, params BattleGoalView[] goals) =>
        new(1000 + order, name, order, goals, status, status != BattleStoreStatus.Ahead, goals.Sum(g => g.TakenSum ?? 0));

    /// <summary>Карточки для тестов: 16 магазинов и 20 целей (как в образце ТЗ), статусы — по слоту.</summary>
    public static BattleCards SampleCards(string slot, string language, IReadOnlyList<(long Id, string Name)> agents, DateOnly date)
    {
        var evaluated = slot != "morning";
        var stores = new List<BattleStore>();
        for (var i = 1; i <= 16; i++)
        {
            var goals = new List<BattleGoalView>();
            if (i <= 10)
            {
                var taken = evaluated && i % 3 != 0;
                goals.Add(Goal(BattleGoalKind.Twizos, i <= 6 ? BattleGoalSub.Connect : BattleGoalSub.Prize, i <= 4 ? "400" : "300", i <= 4 ? 392_200 : 288_600,
                    taken ? BattleGoalStatus.Taken : evaluated ? BattleGoalStatus.Refused : BattleGoalStatus.Pending, taken ? 470_640 : null));
            }

            if (i is 2 or 5 or 8 or 10 or >= 11)
            {
                var taken = evaluated && i % 4 != 0;
                goals.Add(Goal(BattleGoalKind.Mogul, i >= 11 ? BattleGoalSub.Connect : BattleGoalSub.Volume, i >= 11 ? "G" : "E", i >= 11 ? 400_000 : 1_500_000,
                    taken ? BattleGoalStatus.Taken : evaluated ? BattleGoalStatus.Refused : BattleGoalStatus.Pending, taken ? 1_500_000 : null, taken ? 5 : null));
            }

            var takenCount = goals.Count(g => g.IsTaken);
            var status = !evaluated ? BattleStoreStatus.Ahead : takenCount == goals.Count ? BattleStoreStatus.Taken : takenCount > 0 ? BattleStoreStatus.Partial : BattleStoreStatus.Refused;
            stores.Add(Store(i, $"Savdo nuqtasi {i} juda uzun nomi bilan", status, goals.ToArray()));
        }

        var all = stores.SelectMany(s => s.Goals).ToList();
        var progress = new BattleWeekProgress(all.Count(g => g.IsTaken), 120, all.Count(g => g.IsNew && g.IsTaken), 60, 0, O.Bonus[0], 30 - all.Count(g => g.IsTaken), true);
        var days = agents.Select(a => new BattleDay(a.Id, a.Name, "Сырдарья", date, BattleRouteSource.Linko, stores, true, progress, new DateTimeOffset(date.ToDateTime(new TimeOnly(12, 45)), TimeSpan.FromHours(5)))).ToList();
        var monday = BattleRules.Monday(date);
        var weeks = agents.Select(a => new BattleWeek(a.Id, a.Name, "Сырдарья", monday, monday.AddDays(5),
            Enumerable.Range(0, 6).Select(i => new BattleWeekDay(monday.AddDays(i), i < 5 ? stores : [], i < 5 ? 10 + i : 0, i < 5 ? 20 : 0)).ToList(),
            new BattleWeekProgress(60, 100, 20, 40, 1_500_000, O.Bonus[3], 5, true), 2, 6, null)).ToList();
        var prizes = O.TwizosSets.Select(s => new BattlePrizeTile(s.Level, s.Sum, s.Contents, s.Prize)).ToList();
        return new BattleCards(slot, slot == "saturday" ? [] : days, slot == "saturday" ? weeks : [], prizes, "Могул — одной накладной · фрукты от 70 000 сум → ещё −5%", O.Bonus, language);
    }

    private static ReportDocument Build(string job, ReportScope scope, string language = "ru", BattleCards? cards = null, DateTimeOffset? ordersAsOf = null)
    {
        var info = ReportCatalog.Find(job)!;
        var resolved = ReportScopes.Resolve(scope, Teams, [], ru => ReportTexts.Get(language, ru));
        var at = new DateTimeOffset(Day.ToDateTime(info.Time), TimeSpan.FromHours(5));
        var facts = new ReportFacts
        {
            Categories = [], CategoryPrice = new Dictionary<string, decimal>(), Plans = new Dictionary<(long, int, int), MonthPlan>(), Orders = [], Visits = [],
            OrdersAsOf = ordersAsOf ?? at.AddMinutes(-10),
        };
        var context = new ReportContext(info, resolved, Day, at, language, facts, new TimeOnly(13, 0), info.IsMidday ? TimeOnly.FromDateTime(at.DateTime) : TimeOnly.MaxValue, 30, null, info.Time);
        return BattleReport.Build(context, cards ?? SampleCards(info.Slot, language, resolved.Agents.Select(a => (a.Id, a.Name)).ToList(), Day));
    }

    private static readonly ReportScope Tp = new(TelegramReportRoles.Tp, AgentId: 413);
    private static readonly ReportScope Sv = new(TelegramReportRoles.Sv, RegionId: Region, SupervisorId: 71);

    [Fact]
    public void Document_has_bilingual_caption_counters_and_skip_rules()
    {
        var morning = Build("battle.morning", Tp);
        Assert.StartsWith("7. Поле боя · ТП Дилноза Тестова · Сырдарья · 02.11.2026", morning.Caption);
        Assert.Contains("Доброе утро! Маршрут: 16 магазинов, 20 целей.", morning.Caption);
        Assert.Contains("Xayrli tong! Marshrut: 16 ta do‘kon, 20 ta maqsad.", morning.Caption);
        Assert.Equal("7. Поле боя · 08:30 — маршрут дня", morning.Title);
        Assert.Contains(morning.Kpis, k => k.Label == "Твизос — взято" && k.Value == "0 / 10");
        Assert.Contains(morning.Kpis, k => k.Label == "Точек пройдено" && k.Value == "0 / 16");
        Assert.NotNull(morning.Battle);
        Assert.True(morning.Applies);
        Assert.Contains("Твизос · подключить", morning.Plain());

        var evening = Build("battle.evening", Tp);
        // Вечером взято 7 Твизос и 7 Могул из 20; до первой ступени (30 из 120 за неделю) — ещё 16.
        Assert.Contains("Итог дня: 14 из 20. До бонуса 500 000 сум — ещё 16 целей.", evening.Caption);
        Assert.Contains("Kun yakuni: 20 tadan 14 ta. 500 000 so‘m bonusgacha yana 16 ta maqsad.", evening.Caption);

        // Без целей — пропуск; заказы Linko за день не загружены — полдень и вечер не отправляются.
        var empty = Build("battle.morning", Tp, cards: new BattleCards("morning", [], [], [], "", O.Bonus, "ru"));
        Assert.False(empty.Applies);
        Assert.Contains("Целей на этот день нет", empty.SkipReason);
        var stale = Build("battle.midday", Tp, ordersAsOf: new DateTimeOffset(Day.AddDays(-1).ToDateTime(new TimeOnly(18, 0)), TimeSpan.FromHours(5)));
        Assert.False(stale.Applies);
        Assert.Contains("не обновились", stale.SkipReason);
        Assert.True(Build("battle.morning", Tp, ordersAsOf: new DateTimeOffset(Day.AddDays(-1).ToDateTime(new TimeOnly(18, 0)), TimeSpan.FromHours(5))).Applies);

        var week = Build("battle.saturday", Tp);
        Assert.Contains("Итог недели: 60 из 100 (60%) — бонус 1 500 000 сум.", week.Caption);
        Assert.Contains("Hafta yakuni: 100 tadan 60 ta (60%) — bonus 1 500 000 so‘m.", week.Caption);

        var sv = Build("battle.morning", Sv);
        Assert.Equal(2, sv.Battle!.Days.Count);
        Assert.Contains("2 ТП: 32 магазинов, 40 целей", sv.Caption);
    }

    private static readonly SkiaReportRenderer Renderer = new();

    [Theory]
    [InlineData("battle.morning", "ru")]
    [InlineData("battle.midday", "uz")]
    [InlineData("battle.evening", "ru")]
    [InlineData("battle.saturday", "uz")]
    public void Cards_render_as_1080_px_png(string job, string language)
    {
        var report = Build(job, Tp, language);
        var pages = Renderer.Render(report);
        // 16 магазинов — две картинки дня (по 10 на страницу); неделя — одна.
        Assert.Equal(job == "battle.saturday" ? 1 : 2, pages.Count);
        foreach (var png in pages)
        {
            using var bitmap = SKBitmap.Decode(png);
            Assert.Equal(1080, bitmap.Width);
            Assert.InRange(bitmap.Height, 800, 2600);
            Assert.True(png.Length < 4 * 1024 * 1024);
        }

        var test = Renderer.Render(report with { Test = true });
        Assert.NotEqual(Convert.ToBase64String(pages[0]), Convert.ToBase64String(test[0]));
        Assert.Equal(2, Renderer.Render(Build(job, Sv, language)).Count); // у СВ — по одной картинке на ТП (двое в регионе)
    }

    [Fact]
    public void Uzbek_texts_cover_battle_documents()
    {
        static bool Cyrillic(string s) => s.Any(ch => ch is >= 'А' and <= 'я' or 'Ё' or 'ё');
        string[] data = ["Дилноза Тестова", "Бахтиёр Тестов", "Азиз Каримов", "Сырдарья", "Могул — одной накладной · фрукты от 70 000 сум → ещё −5%"];
        var missing = new List<string>();
        foreach (var job in ReportCatalog.Jobs.Where(j => j.Report == ReportType.Battle))
        {
            foreach (var scope in new[] { Tp, Sv })
            {
                var report = Build(job.Code, scope, "uz");
                var texts = new[] { report.Title, report.Caption, report.Summary, report.Scope }.Concat(report.Kpis.SelectMany(k => new[] { k.Label, k.Value, k.Hint ?? "" }))
                    .Concat(report.Tables.SelectMany(t => new[] { t.Title }.Concat(t.Columns.Select(col => col.Title)).Concat(t.Rows.SelectMany(r => r.Select(cell => cell.Text)))))
                    .Concat(report.Notes).Append(report.SkipReason ?? "");
                missing.AddRange(texts.Select(t => data.Aggregate(t, (acc, d) => acc.Replace(d, ""))).Where(Cyrillic).Select(t => $"{job.Code}/{scope.Role}: {t}"));
            }
        }

        Assert.True(missing.Count == 0, string.Join(Environment.NewLine, missing.Distinct().Take(40)));
    }
}

/// <summary>
/// Постановка и оценка целей на PostgreSQL: план визитов Linko → цели по правилам (активная база, Твизос без закупа, набор по уровню,
/// сетка Могул по акции Linko), повторная карточка целей не меняет, заказ дня закрывает цель, визит без заказа — отказ, неделя — ход и бонус.
/// </summary>
public class BattleDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly DateOnly Monday = new(2026, 11, 2);
    private static readonly DateOnly Wednesday = Monday.AddDays(2);

    [PostgresFact]
    public async Task Goals_follow_linko_plan_and_orders_close_them()
    {
        fixture.Clock.Now = new DateTimeOffset(2026, 11, 4, 3, 30, 0, TimeSpan.Zero); // среда 04.11, 08:30 Ташкент
        var agent = 900_413;
        var ids = Enumerable.Range(1, 8).Select(i => 9_000_000L + i).ToArray();
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            await db.BattleGoals.Where(g => g.AgentId == agent).ExecuteDeleteAsync();
            foreach (var id in ids)
            {
                await db.LinkoVisits.Where(v => v.MarketId == id).ExecuteDeleteAsync();
                await db.LinkoOrders.Where(o => o.MarketId == id).ExecuteDeleteAsync();
                await db.LinkoMarkets.Where(m => m.Id == id).ExecuteDeleteAsync();
            }

            await db.LinkoProductTypes.Where(t => t.Id == 22 || t.Id == 20).ExecuteDeleteAsync();
            await db.LinkoProducts.Where(p => p.Id == 9_000_604 || p.Id == 9_000_480).ExecuteDeleteAsync();
            await db.LinkoPromotions.Where(p => p.Id == 9_000_151).ExecuteDeleteAsync();
            db.LinkoProductTypes.AddRange(new LinkoProductType { Id = 22, Name = "Снек" }, new LinkoProductType { Id = 20, Name = "Помадка 0,420 кг" });
            db.LinkoProducts.AddRange(new LinkoProduct { Id = 9_000_604, Name = "Twizos Kabob 140 г", Code = "604", TypeId = 22 }, new LinkoProduct { Id = 9_000_480, Name = "Yamelly Arbuz 420 г", Code = "480", TypeId = 20 });
            db.LinkoPromotions.Add(new LinkoPromotion { Id = 9_000_151, Name = "Акция на Могул Ноябрь 2026г. 5% \"E\"", Type = "discount", BeginDate = Monday, TillDate = Monday.AddDays(30) });
            // 8 точек ТП: 1–6 — активная база (без Твизос; 2 и 4 берут Могул в ноябре), 7 — мелкая (ниже 50 000), 8 — экспорт.
            for (var i = 0; i < ids.Length; i++)
            {
                db.LinkoMarkets.Add(new LinkoMarket { Id = ids[i], Name = $"Точка {i + 1}", MarketTypeName = i == 7 ? "EXPORT" : "Розница", ResponsibleAgentId = agent, Lat = 40.5 + i * 0.01, Lon = 68.6 });
                var monthly = i == 6 ? 20_000m : 1_200_000m + i * 400_000m;
                foreach (var m in new[] { 1, 2, 3 })
                {
                    var past = Monday.AddMonths(-m);
                    db.LinkoOrders.Add(new LinkoOrder
                    {
                        Id = 9_100_000 + i * 10 + m, MarketId = ids[i], AgentId = agent, Status = "delivered", CreatedAt = past.ToDateTime(new TimeOnly(10, 0)), CreatedDate = past,
                        AcceptedAt = past.ToDateTime(new TimeOnly(12, 0)), AcceptedDate = past, TotalPrice = monthly * 4 / 3, TotalWeight = 10,
                    });
                }

                if (i is 1 or 3)
                {
                    var eve = Monday.AddDays(-1); // 1 ноября — Могул этого месяца, накладная 900 000 (категория E по 700 000/мес)
                    db.LinkoOrders.Add(new LinkoOrder
                    {
                        Id = 9_200_000 + i, MarketId = ids[i], AgentId = agent, Status = "delivered", CreatedAt = eve.ToDateTime(new TimeOnly(9, 0)), CreatedDate = eve,
                        AcceptedAt = eve.ToDateTime(new TimeOnly(15, 0)), AcceptedDate = eve, TotalPrice = 900_000, TotalWeight = 5,
                        Lines = [new LinkoOrderLine { Id = 9_200_100 + i, ProductId = 9_000_480, Amount = 20, Price = 45_000, TotalPrice = 900_000, TotalWeight = 5 }],
                    });
                    foreach (var m in new[] { 1, 2, 3 })
                    {
                        db.LinkoOrderLines.Add(new LinkoOrderLine { Id = 9_300_000 + i * 10 + m, OrderId = 9_100_000 + i * 10 + m, ProductId = 9_000_480, Amount = 20, Price = 35_000, TotalPrice = 700_000, TotalWeight = 4 });
                    }
                }

                // План визитов Linko: все 8 точек в среду.
                db.LinkoVisits.Add(new LinkoVisit { Id = 9_400_000 + i, UserId = agent, MarketId = ids[i], Status = "pending", IsInPlan = true, Date = Wednesday.ToDateTime(TimeOnly.MinValue), Day = Wednesday });
            }

            await db.SaveChangesAsync();
        }

        BattleDay day;
        using (var scope = fixture.Services.CreateScope())
        {
            day = await scope.ServiceProvider.GetRequiredService<BattleGoalService>().DayAsync(agent, "413 Тест", "Сырдарья", Wednesday, null, false, CancellationToken.None);
        }

        Assert.Equal(BattleRouteSource.Linko, day.RouteSource);
        Assert.Equal(6, day.Stores.Count); // без мелкой и экспортной
        Assert.Equal(6, day.TwizosTotal); // все шесть без Твизос — «подключить»
        Assert.All(day.Goals.Where(g => g.Kind == BattleGoalKind.Twizos), g => Assert.Equal(BattleGoalSub.Connect, g.Sub));
        Assert.Equal("400", day.Stores.First(s => s.Name == "Точка 1").Goals[0].Level); // 1,2 млн/мес → набор 400
        Assert.Equal("600", day.Stores.First(s => s.Name == "Точка 6").Goals[0].Level); // 3,2 млн → 600
        Assert.True(day.MogulActive);
        var mogul = day.Goals.Where(g => g.Kind == BattleGoalKind.Mogul).ToList();
        Assert.Equal(4, mogul.Count(g => g.Sub == BattleGoalSub.Connect)); // точки без Могул в ноябре: 1, 3, 5, 6 (H — новые)
        Assert.Equal(2, mogul.Count(g => g.Sub == BattleGoalSub.Volume)); // 2 и 4: накладная 900 000 < порога E 1 500 000 → цель 1 500 000 → 5%
        var volume = day.Stores.First(s => s.Name == "Точка 2").Goals.Single(g => g.Kind == BattleGoalKind.Mogul);
        Assert.Equal(("E", 1_500_000m, 5, 2_500_000m, 8), (volume.Level, volume.TargetSum, volume.Percent, volume.SecondSum!.Value, volume.SecondPercent!.Value));
        Assert.Equal(12, day.Week.Total);
        Assert.Equal(0, day.Week.Taken);
        Assert.All(day.Stores, s => Assert.Equal(BattleStoreStatus.Ahead, s.Status));

        // Заказы среды: точка 1 — Твизос на 400 000 до скидки (цель 392 200 взята), точка 2 — Могул 2 600 000 (второй порог, 8%); точка 3 — визит без заказа.
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            db.LinkoOrders.Add(new LinkoOrder
            {
                Id = 9_500_001, MarketId = ids[0], AgentId = agent, Status = "not_delivered", CreatedAt = Wednesday.ToDateTime(new TimeOnly(10, 15)), CreatedDate = Wednesday, TotalPrice = 380_000, TotalWeight = 3,
                Lines = [new LinkoOrderLine { Id = 9_500_101, ProductId = 9_000_604, Amount = 26, Price = 14_615, TotalPrice = 380_000, TotalWeight = 3, OriginPrice = 15_385, TotalDiscount = 20_000, DiscountPercent = 5 }],
            });
            db.LinkoOrders.Add(new LinkoOrder
            {
                Id = 9_500_002, MarketId = ids[1], AgentId = agent, Status = "not_delivered", CreatedAt = Wednesday.ToDateTime(new TimeOnly(11, 0)), CreatedDate = Wednesday, TotalPrice = 2_392_000, TotalWeight = 20,
                Lines = [new LinkoOrderLine { Id = 9_500_102, ProductId = 9_000_480, Amount = 60, Price = 39_867, TotalPrice = 2_392_000, TotalWeight = 20, OriginPrice = 43_333, TotalDiscount = 208_000, DiscountPercent = 8 }],
            });
            db.LinkoVisits.Add(new LinkoVisit { Id = 9_400_100, UserId = agent, MarketId = ids[2], Status = "done", IsInPlan = true, Date = Wednesday.ToDateTime(new TimeOnly(11, 30)), Day = Wednesday });
            await db.SaveChangesAsync();
        }

        BattleDay midday, evening;
        BattleWeek week;
        using (var scope = fixture.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<BattleGoalService>();
            midday = await service.DayAsync(agent, "413 Тест", "Сырдарья", Wednesday, new TimeOnly(10, 30), false, CancellationToken.None);
            evening = await service.DayAsync(agent, "413 Тест", "Сырдарья", Wednesday, null, false, CancellationToken.None);
            week = await service.WeekAsync(agent, "413 Тест", "Сырдарья", Monday, Monday.AddDays(5), null, false, [agent, 900_411], CancellationToken.None);
        }

        // Цели те же (повторное построение ничего не переставило), к 10:30 взята только Твизос точки 1.
        Assert.Equal(day.Goals.Select(g => g.Id).Order(), midday.Goals.Select(g => g.Id).Order());
        Assert.Equal(1, midday.Taken);
        Assert.Equal(BattleStoreStatus.Partial, midday.Stores.First(s => s.Name == "Точка 1").Status); // Твизос взят, Могул «подключить» — ещё нет
        Assert.Equal(BattleStoreStatus.Ahead, midday.Stores.First(s => s.Name == "Точка 2").Status);

        var p1 = evening.Stores.First(s => s.Name == "Точка 1");
        Assert.Equal((BattleStoreStatus.Partial, 400_000m), (p1.Status, p1.Goals[0].TakenSum)); // Твизос взят до скидки 400 000, Могул «подключить» — нет
        var p2 = evening.Stores.First(s => s.Name == "Точка 2").Goals.Single(g => g.Kind == BattleGoalKind.Mogul);
        Assert.Equal((BattleGoalStatus.Taken, 2_600_000m, 8), (p2.Status, p2.TakenSum, p2.TakenPercent));
        Assert.Equal(BattleStoreStatus.Refused, evening.Stores.First(s => s.Name == "Точка 3").Status); // визит есть, заказа нет
        Assert.Equal(BattleStoreStatus.Ahead, evening.Stores.First(s => s.Name == "Точка 5").Status);
        Assert.Equal(2, evening.Week.Taken);
        Assert.Equal(12, evening.Week.Total);

        Assert.Equal(2, week.Progress.Taken);
        Assert.Equal((1, 1), (week.Rank, week.Participants)); // у 411 целей нет — он не участник
        Assert.Equal(6, week.Days.Count);
        Assert.Equal(6, week.Days[2].Stores.Count);
        Assert.Empty(week.Days[0].Stores); // в понедельник плана не было
    }
}
