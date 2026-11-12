using System.Text.RegularExpressions;
using OneBase.Application.Telegram.Battle;
using OneBase.Application.Telegram.Incentive;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Incentive;
using OneBase.Infrastructure.Telegram.Reports;
using SkiaSharp;

namespace OneBase.AI.Tests;

/// <summary>
/// Карточки накопительной акции без базы: на данных образцов ТЗ (Жахонгир, 12 магазинов маршрута, 515 кг на 16.11) — маршрут дня, полдень,
/// итог дня, неделя, месяц (и последний месяц акции), рейтинг, сводки СВ и РМ; PNG 1080 px, страницы, тест, несвежие данные, узбекский; подпись
/// под картинкой по шаблонам ТЗ (RU + UZ); у каждой надписи карточек есть узбекский перевод. ONEBASE_INCENTIVE_PNG=папка — сохранить картинки.
/// </summary>
public class IncentiveCardTests
{
    private static readonly IReadOnlyDictionary<DateOnly, string> Holidays = new Dictionary<DateOnly, string> { [new(2026, 12, 8)] = "День Конституции" };
    private static readonly DateOnly Day = new(2026, 11, 16);
    private static readonly IncentiveAgent Me = new(468, "Жахонгир CandyGold", "Самарканд");

    private static readonly string[] StoreNames =
    [
        "Шавкат Мадина Опт", "Kola 5 Комил Ака", "Муниса Опа Оптом", "Ойла Маркет Ташкент йули", "Sherzod aka krupski", "Шукрона Пушти Ок магазин",
        "Нормурот Ота Бегзод", "Сталин Чинигуль Опа", "Pokiza Plyus Ulug'bek", "Магазин Марокан Самарканд", "Амир Сталин", "Сардор Ака Ширин",
    ];

    private static readonly decimal[] Targets = [7.64m, 7.64m, 7.64m, 4.62m, 4.62m, 3.64m, 3.64m, 3.64m, 3.64m, 3.64m, 0.98m, 0.98m];

    private static IncentiveRoute Route(string slot, DateOnly date, int count = 12)
    {
        // Полдень: 1, 2, 4 взяты, 3 и 5 — отказ, 6 — частично; вечер — ещё 7 частично, 8 и 11 отказ, 9, 10, 12 взяты.
        (string Status, decimal Kg)[] midday = [(BattleStoreStatus.Taken, 9.1m), (BattleStoreStatus.Taken, 7.6m), (BattleStoreStatus.Refused, 0), (BattleStoreStatus.Taken, 6.9m),
            (BattleStoreStatus.Refused, 0), (BattleStoreStatus.Partial, 1.8m)];
        (string Status, decimal Kg)[] evening = [.. midday, (BattleStoreStatus.Partial, 1.8m), (BattleStoreStatus.Refused, 0), (BattleStoreStatus.Taken, 5.4m),
            (BattleStoreStatus.Taken, 4.3m), (BattleStoreStatus.Refused, 0), (BattleStoreStatus.Taken, 1.0m)];
        var stores = Enumerable.Range(0, count).Select(i =>
        {
            var set = IncentiveTests.Twizos().StoreSets.First(x => x.Kg == Targets[i % Targets.Length]);
            var (status, kg) = slot switch
            {
                IncentiveSlot.Midday when i < midday.Length => midday[i],
                IncentiveSlot.Evening when i < evening.Length => evening[i],
                _ => (BattleStoreStatus.Ahead, 0m),
            };
            return new IncentiveStore(9_000 + i, StoreNames[i % StoreNames.Length], i + 1, i is 3 or 11 ? IncentiveTargetSub.Reorder : IncentiveTargetSub.Connect, set.Contents,
                set.Kg, set.Prize, null, status, kg, status != BattleStoreStatus.Ahead);
        }).ToList();
        return new IncentiveRoute(Me.Id, date, "linko", stores, count + 2);
    }

    private static IncentiveStanding Standing(IncentiveAgent agent, decimal october, decimal november, decimal today, DateOnly date, bool wholesale = false)
    {
        var days = new Dictionary<DateOnly, decimal> { [new(2026, 10, 15)] = october, [new(2026, 11, 10)] = november };
        if (today > 0)
        {
            days[date] = today;
        }

        return new IncentiveStanding(agent, wholesale, october + november, 0, today, days);
    }

    private static IncentiveAgentCard Card(IncentiveSettings s, string slot, DateOnly date, decimal today, IncentiveRoute? route = null, IncentiveWeekView? week = null,
        IncentiveMonthView? month = null, decimal november = 230)
    {
        var standing = Standing(Me, 285, november, today, date);
        var firstDay = slot is IncentiveSlot.Morning or IncentiveSlot.Midday ? date : date.AddDays(1);
        return new IncentiveAgentCard(Me, standing, IncentiveRules.Progress(s, standing.Total, firstDay, Holidays),
            IncentiveRules.Progress(s, standing.Total - today, date, Holidays), route, week, month, 1, 1, 102);
    }

    private static IncentiveTeam Team(IncentiveSettings s, string role, string slot)
    {
        IncentiveTeamRow Row(long id, string name, string region, decimal total, decimal today, decimal? route, bool wholesale = false) =>
            new(new IncentiveAgent(id, name, region), total, today, route, IncentiveRules.Progress(s, total, Day, Holidays), Math.Round(total / 39, 1), 1, wholesale);
        var samarkand = new IncentiveTeamRegion("Самарканд", "Азиз Каримов",
        [
            Row(468, "Жахонгир CandyGold", "Самарканд", 515, 38.2m, 52.3m), Row(478, "Жавохир CandyGold", "Самарканд", 442, 21, 40),
            Row(597, "Тимур CandyGold", "Самарканд", 375, 0, 38), Row(470, "Сухроб CandyGold", "Самарканд", 258, 12, 33),
            Row(479, "Фарход (Алам) CandyGold", "Самарканд", 1260, 30, 45, wholesale: true),
        ]);
        var regions = role == "rm"
            ? new List<IncentiveTeamRegion>
            {
                samarkand,
                new("Сырдарья", "Рустам Ахмедов", [Row(413, "Фотима Абдумаликова GOOD FOOD", "Сырдарья", 241, 9, 30), Row(411, "Абдуллох Рахимжонов GOOD", "Сырдарья", 169, 4, 28)]),
                new("Коканд", null, [Row(404, "Давлатжон Хусанов", "Коканд", 252, 11, 31)]),
            }
            : [samarkand];
        return new IncentiveTeam(role, role == "rm" ? "РМ Рустам Ахмедов · Восток" : "Самарканд · СВ Азиз Каримов", regions);
    }

    internal static IncentiveCards Sample(string slot, string language = "ru", string? team = null, bool stale = false, bool final = false)
    {
        var s = IncentiveTests.Twizos(479);
        var date = slot == IncentiveSlot.Month ? final ? new DateOnly(2026, 12, 31) : new DateOnly(2026, 11, 30) : slot is IncentiveSlot.Week or IncentiveSlot.Rating ? new DateOnly(2026, 11, 21) : Day;
        var today = slot switch { IncentiveSlot.Midday => 25.4m, IncentiveSlot.Evening => 38.2m, IncentiveSlot.Week => 35m, _ => 0m };
        var agents = new List<IncentiveAgentCard>();
        IncentiveRating? rating = null;
        switch (slot)
        {
            case IncentiveSlot.Morning or IncentiveSlot.Midday or IncentiveSlot.Evening:
                agents.Add(Card(s, slot, date, today, Route(slot, date)));
                break;
            case IncentiveSlot.Week:
            {
                var monday = BattleRules.Monday(date);
                decimal[] kg = [38, 31, 22, 41, 27, 35];
                int[] stores = [12, 12, 12, 20, 16, 16];
                var days = Enumerable.Range(0, 6).Select(i => new IncentiveWeekDay(monday.AddDays(i), kg[i], Route(IncentiveSlot.Evening, monday.AddDays(i), stores[i]))).ToList();
                IncentiveTopMarket[] best =
                [
                    new(1, "Супермаркет Тимур Бек Савдо Ма", 32, IncentiveRules.WeekTarget(32 * 60 / 31m)), new(2, "Qaxramon ota magazin", 32, 5),
                    new(3, "Коффе Тайм", 26, 5), new(4, "Бобур Ака Кизил Кулок Кш", 23, 5), new(5, "Камаридин 888 Оптовик", 22, 5),
                ];
                agents.Add(Card(s, slot, date, today, week: new IncentiveWeekView(monday, monday.AddDays(5), days, 26, 156, best), november: 424));
                break;
            }

            case IncentiveSlot.Month:
            {
                decimal[] kg = [118, 149, 174, 199, 0];
                var weeks = new List<IncentiveMonthWeek>();
                for (var (from, i) = (new DateOnly(date.Year, date.Month, 1), 0); from.Month == date.Month; i++)
                {
                    var to = BattleRules.Monday(from).AddDays(6);
                    if (to.Month != date.Month)
                    {
                        to = new DateOnly(date.Year, date.Month, 1).AddMonths(1).AddDays(-1);
                    }

                    if (IncentiveRules.WorkDays(from, to, Holidays) > 0)
                    {
                        weeks.Add(new IncentiveMonthWeek(weeks.Count + 1, from, to, kg[Math.Min(weeks.Count, kg.Length - 1)], 120));
                    }

                    from = to.AddDays(1);
                }

                agents.Add(Card(s, slot, date, 0, month: new IncentiveMonthView(date.Year, date.Month, 640, 285, weeks, s.Tiers[0], 525, final), november: 640));
                break;
            }

            case IncentiveSlot.Rating:
            {
                (string Name, string Region, decimal Kg)[] top =
                [
                    ("Жахонгир CandyGold", "Самарканд", 1515), ("Жавохир CandyGold", "Самарканд", 442), ("Тимур CandyGold", "Самарканд", 375), ("Сухроб CandyGold", "Самарканд", 258),
                    ("Давлатжон Хусанов", "Коканд", 252), ("Фотима Абдумаликова GOOD FOOD", "Сырдарья", 241), ("Зилолахон Рахмонова", "Фаргона", 224),
                    ("Озода Иброхимова GOOD FOOD", "Сырдарья", 200), ("Абдуллох Рахимжонов GOOD FOOD", "Сырдарья", 169), ("Азизхужа Назирхужаев", "Коканд", 164),
                ];
                IncentiveRatingRow Row((string Name, string Region, decimal Kg) a, int i) => new(i + 1, new IncentiveAgent(i + 1, a.Name, a.Region), a.Kg,
                    IncentiveRules.Reached(s.Tiers, a.Kg), IncentiveRules.Next(s.Tiers, a.Kg), IncentiveRules.Next(s.Tiers, a.Kg) is { } n ? n.Kg - a.Kg : null);
                rating = new IncentiveRating(null, top.Select(Row).ToList(), [Row(("534 ТП1 Дилмурод осдо", "Ташкент", 310), 0), Row(("Салохиддин Кодиров", "Коканд", 186), 1)], 34);
                break;
            }
        }

        var teamCards = team is null ? null : Team(s, team, slot);
        if (team is not null && IncentiveSlot.IsDay(slot) && team == "sv")
        {
            agents.Add(Card(s, slot, date, today, Route(slot, date)) with { Agent = new IncentiveAgent(478, "Жавохир CandyGold", "Самарканд") });
        }

        return new IncentiveCards(slot, date, s, language, agents, teamCards, rating, IncentiveRules.MiddayShare(new(8, 30), new(13, 0), new(18, 0)),
            new DateTimeOffset(2026, 11, 16, 7, 45, 0, TimeSpan.Zero), stale);
    }

    private static ReportDocument Document(IncentiveCards cards, bool test = false)
    {
        var (caption, summary) = IncentiveTexts.Caption(cards);
        return new ReportDocument("incentive." + cards.Slot, ReportType.Battle, "tp", "9. Накопительная акция", "ТП Жахонгир CandyGold · Самарканд", null, cards.Date,
            cards.Date.ToString("dd.MM.yyyy"), DateTimeOffset.UtcNow, cards.DataAsOf, cards.Language, [], [], [], [], caption, summary) { Incentive = cards, Test = test };
    }

    private static readonly SkiaReportRenderer Renderer = new();

    private static IReadOnlyList<byte[]> Render(IncentiveCards cards, string name, bool test = false)
    {
        var pages = Renderer.Render(Document(cards, test));
        if (Environment.GetEnvironmentVariable("ONEBASE_INCENTIVE_PNG") is { Length: > 0 } dir)
        {
            Directory.CreateDirectory(dir);
            for (var i = 0; i < pages.Count; i++)
            {
                File.WriteAllBytes(Path.Combine(dir, $"{name}-{i + 1}.png"), pages[i]);
            }
        }

        foreach (var png in pages)
        {
            using var bitmap = SKBitmap.Decode(png);
            Assert.Equal(1080, bitmap.Width);
            Assert.InRange(bitmap.Height, 400, 3200);
            Assert.True(png.Length < 5 * 1024 * 1024);
        }

        return pages;
    }

    [Theory]
    [InlineData(IncentiveSlot.Morning, "ru", 1)]
    [InlineData(IncentiveSlot.Midday, "ru", 1)]
    [InlineData(IncentiveSlot.Evening, "ru", 1)]
    [InlineData(IncentiveSlot.Week, "ru", 1)]
    [InlineData(IncentiveSlot.Month, "ru", 1)]
    [InlineData(IncentiveSlot.Rating, "ru", 1)]
    [InlineData(IncentiveSlot.Morning, "uz", 1)]
    [InlineData(IncentiveSlot.Evening, "uz", 1)]
    [InlineData(IncentiveSlot.Week, "uz", 1)]
    [InlineData(IncentiveSlot.Month, "uz", 1)]
    [InlineData(IncentiveSlot.Rating, "uz", 1)]
    public void Agent_cards_render_as_1080_px_png(string slot, string language, int expected)
    {
        var pages = Render(Sample(slot, language), $"{slot}-{language}");
        Assert.Equal(expected, pages.Count);
        var test = Renderer.Render(Document(Sample(slot, language), test: true));
        Assert.NotEqual(Convert.ToBase64String(pages[0]), Convert.ToBase64String(test[0]));
    }

    [Fact]
    public void Team_summaries_stale_data_final_month_and_long_routes()
    {
        // СВ: сводка команды и копии карточек двух ТП (каждая одной картинкой); РМ: регионы и лучшие ТП.
        Assert.Equal(3, Render(Sample(IncentiveSlot.Morning, team: "sv"), "morning-sv").Count);
        Assert.Equal(3, Render(Sample(IncentiveSlot.Evening, team: "sv"), "evening-sv").Count);
        Assert.Single(Render(Sample(IncentiveSlot.Midday, team: "rm") with { Agents = [] }, "midday-rm"));
        Assert.Single(Render(Sample(IncentiveSlot.Week, team: "rm") with { Agents = [] }, "week-rm"));
        // Данные Linko не обновились — та же карточка с отметкой «данные на …» (выше на полосу).
        var fresh = Render(Sample(IncentiveSlot.Evening), "evening-fresh");
        var stale = Render(Sample(IncentiveSlot.Evening, stale: true), "evening-stale");
        using (var a = SKBitmap.Decode(fresh[0]))
        using (var b = SKBitmap.Decode(stale[0]))
        {
            Assert.Equal(a.Height + 60, b.Height);
        }

        Assert.Single(Render(Sample(IncentiveSlot.Month, final: true), "month-final"));
        // 26 магазинов у ТП — три картинки по 12; у копии СВ — одна (до 20).
        var cards = Sample(IncentiveSlot.Morning);
        var longRoute = cards with { Agents = [cards.Agents[0] with { Route = Route(IncentiveSlot.Morning, Day, 26) }] };
        Assert.Equal(3, Render(longRoute, "morning-long").Count);
        // Нет маршрута — карточка с пояснением, не пустая.
        Assert.Single(Render(cards with { Agents = [cards.Agents[0] with { Route = new IncentiveRoute(Me.Id, Day, null, [], 0) }] }, "morning-empty"));
    }

    [Fact]
    public void Captions_follow_the_spec_templates_in_russian_and_uzbek()
    {
        var morning = IncentiveTexts.Caption(Sample(IncentiveSlot.Morning)).Caption;
        Assert.Equal("Маршрут: 12 магазинов, цель 52,3 кг Twizos. Накоплено 515 кг, до планшета — 985 кг.\n" +
                     "Marshrut: 12 ta do‘kon, maqsad 52,3 kg Twizos. Yig‘ildi 515 kg, planshetgacha — 985 kg.", morning);
        var evening = IncentiveTexts.Caption(Sample(IncentiveSlot.Evening)).Caption;
        // 553 кг на вечер 16.11: до планшета 947 кг на 38 рабочих дней с 17.11 → 25 кг в день.
        Assert.Equal("Накоплено 553 кг. До планшета — 947 кг, норма 25 кг в день.\nYig‘ildi 553 kg. Planshetgacha — 947 kg, kuniga 25 kg.", evening);
        var week = IncentiveTexts.Caption(Sample(IncentiveSlot.Week)).Caption;
        Assert.Equal("За неделю 194 кг, всего 744 кг. Место — 1.\nHaftada 194 kg, jami 744 kg. O‘rin — 1.", week);
        var month = IncentiveTexts.Caption(Sample(IncentiveSlot.Month)).Caption;
        Assert.Equal("Итог месяца: 640 кг. В декабре нужно 575 кг до планшета.\nOy yakuni: 640 kg. Dekabrda planshetgacha 575 kg kerak.", month);
        Assert.StartsWith("Итог акции: 925 кг. Приз не взят", IncentiveTexts.Caption(Sample(IncentiveSlot.Month, final: true)).Caption);
        var rating = IncentiveTexts.Caption(Sample(IncentiveSlot.Rating)).Caption;
        var leader = ReportFormat.Number(1515);
        Assert.Equal($"Рейтинг акции Twizos на 21.11: 1 — Жахонгир CandyGold, {leader} кг. До телевизора у лидера — 885 кг.\n" +
                     $"Twizos aksiyasi reytingi 21.11: 1-o‘rin — Жахонгир CandyGold, {leader} kg. Yetakchiga televizorgacha — 885 kg.", rating);
        Assert.Contains("5 ТП: накоплено", IncentiveTexts.Caption(Sample(IncentiveSlot.Evening, team: "sv")).Caption);
    }

    private static readonly Guid Region = Guid.Parse("00000000-0000-0000-0000-000000000005");

    private static readonly IReadOnlyList<OneBase.Application.Sales.Metrics.ReportRegionTeam> Teams =
    [
        new(Region, "Самарканд", "Рустам Ахмедов", "Азиз Каримов", [new OneBase.Application.Sales.Metrics.ReportPerson(71, "Азиз Каримов")],
            [new OneBase.Application.Sales.Metrics.ReportPerson(468, "Жахонгир CandyGold"), new OneBase.Application.Sales.Metrics.ReportPerson(478, "Жавохир CandyGold")]),
    ];

    private static readonly ReportScope TpScope = new(OneBase.Domain.Telegram.TelegramReportRoles.Tp, AgentId: 468);
    private static readonly ReportScope SvScope = new(OneBase.Domain.Telegram.TelegramReportRoles.Sv, RegionId: Region, SupervisorId: 71);
    private static readonly ReportScope RmScope = new(OneBase.Domain.Telegram.TelegramReportRoles.Rm);

    private static ReportDocument Doc(IncentiveCards cards, ReportScope scope)
    {
        var info = ReportCatalog.Find("incentive." + cards.Slot)!;
        var resolved = ReportScopes.Resolve(scope, Teams, [], ru => ReportTexts.Get(cards.Language, ru));
        var at = new DateTimeOffset(cards.Date.ToDateTime(info.Time), TimeSpan.FromHours(5));
        var facts = new ReportFacts
        {
            Categories = [], CategoryPrice = new Dictionary<string, decimal>(), Plans = new Dictionary<(long, int, int), MonthPlan>(), Orders = [], Visits = [],
            OrdersAsOf = at.AddMinutes(-10),
        };
        var context = new ReportContext(info, resolved, cards.Date, at, cards.Language, facts, new TimeOnly(13, 0),
            info.IsMidday ? TimeOnly.FromDateTime(at.DateTime) : TimeOnly.MaxValue, 30, null, info.Time);
        return IncentiveReport.Build(context, cards);
    }

    [Fact]
    public void Documents_carry_cards_bilingual_captions_and_a_text_fallback()
    {
        var morning = Doc(Sample(IncentiveSlot.Morning), TpScope);
        Assert.Equal("9. Накопительная акция · 08:30 — маршрут дня", morning.Title);
        Assert.StartsWith("9. Накопительная акция · ТП Жахонгир CandyGold · Самарканд · 16.11.2026\nМаршрут: 12 магазинов, цель 52,3 кг Twizos.", morning.Caption);
        Assert.Contains("Marshrut: 12 ta do‘kon", morning.Caption);
        Assert.NotNull(morning.Incentive);
        Assert.True(morning.Applies);
        Assert.Contains(morning.Kpis, k => k.Label == "Накоплено" && k.Value == "515 кг");
        Assert.Contains("Шавкат Мадина Опт", morning.Plain()); // без картинки — таблица маршрута текстом
        Assert.Single(Renderer.Render(morning));

        var stale = Doc(Sample(IncentiveSlot.Evening, stale: true), TpScope);
        Assert.True(stale.Incomplete);
        Assert.Contains(stale.Notes, n => n.Contains("не обновились к отправке"));
        var sv = Doc(Sample(IncentiveSlot.Evening, team: "sv"), SvScope);
        Assert.Contains("5 ТП: накоплено", sv.Caption);
        Assert.Equal(3, Renderer.Render(sv).Count);
        Assert.Contains("Рейтинг акции Twizos", Doc(Sample(IncentiveSlot.Rating), RmScope).Caption);
        var nothing = Doc(Sample(IncentiveSlot.Evening) with { Agents = [] }, TpScope);
        Assert.False(nothing.Applies);
        Assert.Contains("нет ТП акции", nothing.SkipReason);
    }

    [Fact]
    public void Uzbek_texts_cover_incentive_documents()
    {
        static bool Cyrillic(string s) => s.Any(ch => ch is >= 'А' and <= 'я' or 'Ё' or 'ё');
        string[] data =
        [
            .. StoreNames, "Жахонгир CandyGold", "Жавохир CandyGold", "Тимур CandyGold", "Сухроб CandyGold", "Фарход (Алам) CandyGold", "Фотима Абдумаликова GOOD FOOD",
            "Абдуллох Рахимжонов GOOD FOOD", "Абдуллох Рахимжонов GOOD", "Давлатжон Хусанов", "Зилолахон Рахмонова", "Озода Иброхимова GOOD FOOD", "Азизхужа Назирхужаев",
            "534 ТП1 Дилмурод осдо", "Салохиддин Кодиров", "Супермаркет Тимур Бек Савдо Ма", "Коффе Тайм", "Бобур Ака Кизил Кулок Кш", "Камаридин 888 Оптовик", "Самарканд · СВ Азиз Каримов", "РМ Рустам Ахмедов · Восток", "Самарканд", "Сырдарья", "Коканд", "Фаргона", "Ташкент", "Рустам Ахмедов", "Азиз Каримов", "Восток",
        ];
        var missing = new List<string>();
        foreach (var (slot, scope, team) in new (string, ReportScope, string?)[]
                 {
                     (IncentiveSlot.Morning, TpScope, null), (IncentiveSlot.Midday, TpScope, null), (IncentiveSlot.Evening, TpScope, null), (IncentiveSlot.Week, TpScope, null),
                     (IncentiveSlot.Month, TpScope, null), (IncentiveSlot.Evening, SvScope, "sv"), (IncentiveSlot.Week, RmScope, "rm"), (IncentiveSlot.Rating, RmScope, null),
                 })
        {
            var report = Doc(Sample(slot, "uz", team, stale: true), scope);
            // Подпись под картинкой — всегда RU + UZ (ТЗ, раздел 8), её русская строка — не пропуск перевода.
            var texts = new[] { report.Title, report.Summary is { Length: > 0 } ? "" : "" }.Concat(report.Kpis.SelectMany(k => new[] { k.Label, k.Value, k.Hint ?? "" }))
                .Concat(report.Tables.SelectMany(t => new[] { t.Title }.Concat(t.Columns.Select(c => c.Title)).Concat(t.Rows.SelectMany(r => r.Select(cell => cell.Text)))))
                .Concat(report.Notes).Append(report.SkipReason ?? "");
            missing.AddRange(texts.Select(t => data.Aggregate(t, (acc, d) => acc.Replace(d, ""))).Where(Cyrillic).Select(t => $"{slot}/{scope.Role}: {t}"));
        }

        Assert.True(missing.Count == 0, string.Join(Environment.NewLine, missing.Distinct().Take(60)));
    }

    [Fact]
    public void Every_card_label_has_an_uzbek_translation()
    {
        var root = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(root, "src", "OneBase.Infrastructure")))
        {
            root = Path.GetDirectoryName(root)!;
        }

        var folder = Path.Combine(root, "src", "OneBase.Infrastructure", "Telegram", "Reports");
        var source = string.Concat(Directory.GetFiles(folder, "IncentiveCardRenderer*.cs").Order().Select(File.ReadAllText));
        var keys = Regex.Matches(source, @"\b[TF]\(""((?:[^""\\]|\\.)*)""").Select(m => Regex.Unescape(m.Groups[1].Value)).Distinct().ToList();
        Assert.NotEmpty(keys);
        var known = ReportTexts.Keys.ToHashSet();
        var missing = keys.Where(k => !known.Contains(k)).ToList();
        Assert.True(missing.Count == 0, "Нет перевода:" + Environment.NewLine + string.Join(Environment.NewLine, missing));
    }
}
