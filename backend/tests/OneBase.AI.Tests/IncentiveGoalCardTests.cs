using OneBase.Application.Telegram.Battle;
using OneBase.Application.Telegram.Incentive;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Battle;
using OneBase.Domain.Incentive;
using OneBase.Infrastructure.Telegram.Reports;
using SkiaSharp;

namespace OneBase.AI.Tests;

/// <summary>
/// Семь карточек «Целей на маршрут» v2 без базы — на числах Excel-образца andijon 4 (24 ТТ понедельника) и итогах образцов 2–3: ТП 08:30, 13:00, 18:00,
/// неделя, месяц; СВР — итог дня команды; РМ — итог недели зоны; по-русски и по-узбекски; одна PNG 1080 px, отправка файлом; пустой маршрут.
/// ONEBASE_INCENTIVE_PNG=папка — сохранить картинки.
/// </summary>
public class IncentiveGoalCardTests
{
    private static readonly IncentiveGoalsPromo G = IncentivePromos.Goals2026;
    private static readonly DateOnly Monday = new(2026, 11, 23);
    private static readonly IncentiveAgent Me = new(582, "andijon 4 Мухаммаджон", "Андижон");

    private static IncentiveSettings Settings() => IncentiveTests.Twizos() with
    {
        StoreSets = IncentivePromos.NovemberSets, Promos = IncentivePromos.November2026 with { Goals = G },
    };

    // Точки образца: имя, средний чек, Могул (кг/мес, фасовка), обычная накладная Twizos.
    private static readonly (string Name, decimal Check, decimal? Mogul, string? Packing, decimal? Invoice)[] Points =
    [
        ("Asqarova Xurshida", 700_920, null, null, null), ("Qambarova Zulfiya", 1_104_212, null, null, 1_872_200), ("Nomoniv Shaxbozbek", 170_062, null, null, null),
        ("Anor Supermarket", 233_000, null, null, null), ("Muxammadjonov Sardorbek", 90_250, null, null, null), ("Komiljon Aka Abdullayev", 42_550, 2.1m, "420 г", null),
        ("Ergashova Azozaopa", 252_650, null, null, null), ("Toyga Toyona", 225_000, null, null, null), ("Ismoyil", 56_925, null, null, null),
        ("Holiqova Gulnora", 134_625, 2.52m, "420 г", null), ("Mamajonov Abdullo.Zarnigor Opa", 2_525_490, null, null, null), ("Axmadjonova Zuxra", 75_000, 7.04m, "420 г", null),
        ("Moydinova Shaxnoza", 849_659, null, null, 1_975_800), ("Moxigul (Mamajonova Nafisaxon)", 150_150, null, null, null), ("Ravshanbekaka", 115_500, 5.04m, "420 г", null),
        ("O'rmonova Shoira Opa", 141_600, null, null, null), ("Nazarova Shaxlo", 295_887, null, null, null), ("Mamajonov Raxmatillo", 150_000, 10.46m, "420 г", null),
        ("Shoxsanam Opa (Baxrom Aka) Dexqon Bozor Ichi", 141_600, null, null, null), ("Isaqjanova Maxliyo Ikromaka", 198_300, null, null, null),
        ("Qochqarova Asalxon", 218_100, 7.36m, "1 кг", null), ("Arzon Market", 77_700, null, null, null), ("Otajonov Nurbek", 131_960, null, null, null),
        ("Tojiddin", 245_067, null, null, 162_800),
    ];

    // Итог 18:00 по образцу 3: Могул / Twizos — T взял, P меньше цели, R отказ, - нет цели; O — без акции, заказ был.
    private static readonly string[] Evening =
    [
        "-T", "-T", "-R", "-T", "-T", "T-", "-T", "-R", "O", "RT", "-T", "TR", "-T", "-T", "PR", "-T", "-P", "TT", "-R", "-T", "RT", "-R", "-T", "-T",
    ];

    private static IncentiveStore Store(int i, string slot)
    {
        var p = Points[i];
        var stands = new HashSet<int> { 0, 10 };
        var mogul = p.Mogul is { } kg ? IncentiveGoalRules.MogulGoal(G, IncentivePromos.November2026.Yamelly!, kg, 2, G.MogulBoxes!.Single(b => b.Name == p.Packing)) : null;
        var twizos = IncentiveGoalRules.TwizosGoal(G, IncentivePromos.November2026.Twizos!, p.Invoice is not null, p.Invoice ?? 0, p.Check, stands.Contains(i));
        var goal = new IncentiveRouteGoal(mogul, twizos);
        var evaluated = slot != IncentiveSlot.Morning && (slot != IncentiveSlot.Midday || i < 12);
        var outcome = evaluated ? Evening[i] : "--";
        string? Mark(char c, bool has) => !has ? null : c switch
        {
            'T' => BattleStoreStatus.Taken,
            'P' => BattleStoreStatus.Partial,
            'R' => BattleStoreStatus.Refused,
            _ => BattleStoreStatus.Ahead,
        };
        var ms = Mark(outcome[0], mogul is not null);
        var ts = Mark(outcome.Length > 1 ? outcome[1] : '-', twizos is not null);
        var twizosKg = ts switch { BattleStoreStatus.Taken => twizos!.Kg, BattleStoreStatus.Partial => 3.64m, _ => 0 };
        var twizosSum = ts switch { BattleStoreStatus.Taken => twizos!.Sum, BattleStoreStatus.Partial => 288_600m, _ => 0 };
        var fact = new IncentiveGoalFact(ms, ms == BattleStoreStatus.Taken ? mogul!.Boxes : ms == BattleStoreStatus.Partial ? 2 : 0,
            ms == BattleStoreStatus.Taken ? mogul!.Cashback : ms == BattleStoreStatus.Partial ? 26_338 : 0, ts, twizosSum, twizosKg,
            twizos is null ? 0 : IncentiveGoalRules.Cashback(twizosSum, twizos.Percent), twizosSum > 0 ? twizos!.Segment : null,
            ts is BattleStoreStatus.Taken or BattleStoreStatus.Partial && twizos!.Gift is { } g ? [ts == BattleStoreStatus.Partial ? "mug" : g] : [], twizos?.Stand == true && ts == BattleStoreStatus.Taken);
        var offers = new[] { ms, ts }.Where(x => x is not null).Select(x => x!).ToList();
        var hasOrder = outcome == "O" || offers.Any(x => x is BattleStoreStatus.Taken or BattleStoreStatus.Partial);
        var visited = evaluated && outcome != "--";
        var status = IncentiveGoalRules.StoreStatus(offers, hasOrder, visited && (hasOrder || offers.Any(x => x == BattleStoreStatus.Refused)));
        return new IncentiveStore(61000 + i, p.Name, i + 1, twizos is null ? IncentiveTargetSub.None : IncentiveTargetSub.Connect, twizos?.Set ?? "", twizos?.Kg ?? 0,
            twizos?.Gift, null, status, twizosKg, visited, null, false, mogul is not null, IncentivePlacement.Route, p.Check, fact.Gifts.Count, fact.Gifts, ts, ms,
            fact.MogulCashback, hasOrder, goal, fact);
    }

    private static IncentiveRoute Route(string slot, DateOnly date, int count = 24)
    {
        var stores = Enumerable.Range(0, count).Select(i => Store(i % Points.Length, slot) with { Order = i + 1, MarketId = 61000 + i }).ToList();
        IReadOnlyList<IncentiveOutOfRouteSale> outside = slot == IncentiveSlot.Morning ? [] :
        [
            new(62999, "Navbahor do'kon", 4.62m, 392_200, ["thermos"], 0,
                new IncentiveGoalFact(null, 0, 0, BattleStoreStatus.Taken, 392_200, 4.62m, 31_376, IncentiveTwizosSegment.New, ["thermos"], false)),
        ];
        return new IncentiveRoute(Me.Id, date, BattleRouteSource.Linko, stores, stores.Count, [], outside);
    }

    private static readonly IReadOnlyList<IncentiveTopSku> Top35 = [new(1, "605 «Twizos» Shashlik (35г / 28шт)", "605", 0.035m, 40, 30), new(2, "614 «Twizos» smetana va piyoz (35г / 28шт)", "614", 0.035m, 30, 20)];
    private static readonly IReadOnlyList<IncentiveTopSku> Top140 = [new(3, "613 «Twizos» smetana va piyoz (140г / 26шт)", "613", 0.14m, 20, 60), new(4, "604 «Twizos» Shashlik (140г / 26шт)", "604", 0.14m, 18, 50)];

    private static IncentiveAgentCard Card(IncentiveSettings s, IncentiveGoalAgent goals)
    {
        var standing = new IncentiveStanding(Me, false, 0, 0, 0, new Dictionary<DateOnly, decimal>());
        var progress = IncentiveRules.Progress(s, 0, Monday, new Dictionary<DateOnly, string>());
        return new IncentiveAgentCard(Me, standing, progress, progress, goals.Day?.Route, null, null, null, null, 0, null, goals);
    }

    private static IncentiveGoalBalances Balances(decimal mogulIssued, decimal twizosIssued, decimal? mogulCan, decimal? twizosCan) =>
        new(new IncentiveGoalBalance(mogulIssued, 79_014 + mogulIssued, 131_690, 0, 131_690, mogulCan), new IncentiveGoalBalance(twizosIssued, twizosIssued, 0, 0, 0, twizosCan));

    internal static IncentiveCards Sample(string slot, string language = "ru", int stores = 24)
    {
        var s = Settings();
        var saturday = Monday.AddDays(5);
        switch (slot)
        {
            case IncentiveSlot.Week:
            {
                var days = Enumerable.Range(0, 6).Select(i =>
                {
                    var route = Route(IncentiveSlot.Evening, Monday.AddDays(i), 14 + i * 2);
                    return new IncentiveGoalWeekDay(Monday.AddDays(i), route, IncentiveGoalTotals.Of(route));
                }).ToList();
                var week = new IncentiveGoalWeek(Monday, saturday, days, Balances(412_870, 692_864, 0, 0),
                    [new(new(2026, 11, 16), new(2026, 11, 22), 131_690, 0, 131_690), new(Monday, Monday.AddDays(6), 360_194, 618_634, 0)],
                    412_870, 692_864, days.Sum(d => d.Totals.Woken), days.Sum(d => d.Totals.MogulGoals), days.Sum(d => d.Totals.TwizosClients),
                    days.Sum(d => d.Totals.TwizosGoals), 31, 2, days.Sum(d => d.Totals.TwizosKg), 4, true);
                return new IncentiveCards(slot, saturday, s, language, [Card(s, new IncentiveGoalAgent(null, week, null))], null, null, 0.5m, null, false);
            }

            case IncentiveSlot.Month:
            {
                var month = new IncentiveGoalMonth(46, 21, 0.3m, Monday, 61, 34, 3, 224,
                    [new(new(2026, 11, 6), new(2026, 11, 8), 1, 26_338), new(new(2026, 11, 9), new(2026, 11, 15), 5, 164_210), new(new(2026, 11, 16), new(2026, 11, 22), 4, 131_690),
                        new(Monday, new(2026, 11, 29), 8, 412_870), new(new(2026, 11, 30), new(2026, 11, 30), 3, 98_452)],
                    new IncentiveGoalEarn(833_560, 322_238), new IncentiveGoalEarn(884_900, 0), new IncentiveGoalEarn(1_500_000, 1_500_000), 3, new DateOnly(2026, 11, 21));
                return new IncentiveCards(slot, new DateOnly(2026, 11, 30), s, language, [Card(s, new IncentiveGoalAgent(null, null, month))], null, null, 0.5m, null, false);
            }

            case IncentiveSlot.Team:
            {
                var names = new[] { "andijon 4 Мухаммаджон", "andijon 1 Олимжон", "andijon 3 Достон", "599 andijon 6", "andijon 2 Исмоилжон", "andijon 5 Нуриддин" };
                var rows = names.Select((n, i) =>
                {
                    var route = Route(IncentiveSlot.Evening, Monday, 24 - i * 3);
                    return new IncentiveGoalTeamRow(new IncentiveAgent(580 + i, n, "Андижон"), IncentiveGoalTotals.Of(route));
                }).OrderByDescending(r => r.Totals.Cashback).ToList();
                var team = new IncentiveGoalTeam("СВР · Андижон", "Андижон", rows,
                    [new("mug", 14, 76), new("thermos", 6, 64), new("kettle", 4, 26), new("stand", 2, 12, 14)], Balances(250_209, 1_091_934, 120_000, 300_000));
                return new IncentiveCards(slot, Monday, s, language, [], null, null, 0.5m, null, false, GoalTeam: team);
            }

            case IncentiveSlot.Zone:
            {
                IncentiveGoalZoneRegion Region(string name, int listed, int woken, int clients, int fresh, int total, int week, int gifts, int mug, int thermos, int kettle) =>
                    new(name, 6, listed, woken, clients, fresh, clients * 3.3m, total, week, gifts,
                        new Dictionary<string, int> { ["mug"] = mug, ["thermos"] = thermos, ["kettle"] = kettle }, Balances(4_000_000, 6_000_000, 0, 0));
                var zone = new IncentiveGoalZone("РМ 2", Monday, saturday,
                [
                    Region("Андижон", 821, 312, 412, 210, 14, 6, 31, 90, 70, 30), Region("Коканд", 636, 280, 356, 180, 11, 4, 52, 60, 55, 21),
                    Region("Наманган", 655, 190, 198, 90, 5, 1, 24, 95, 80, 35), Region("Фаргона", 686, 240, 287, 162, 9, 3, 40, 70, 60, 8),
                ], Balances(16_120_400, 27_300_100, 0, 0), true, 0.3m);
                return new IncentiveCards(slot, saturday, s, language, [], null, null, 0.5m, null, false, GoalZone: zone);
            }

            default:
            {
                var route = Route(slot, Monday, stores);
                var totals = IncentiveGoalTotals.Of(route);
                var morning = slot == IncentiveSlot.Morning;
                var day = new IncentiveGoalDay(route, totals,
                    Balances(totals.MogulCashback, totals.TwizosCashback, morning ? totals.MogulGoalCashback : totals.MogulLeft, morning ? totals.TwizosGoalCashback : totals.TwizosLeft),
                    Top35, Top140, "Андижон");
                return new IncentiveCards(slot, Monday, s, language, [Card(s, new IncentiveGoalAgent(day, null, null))], null, null, 0.5m, null, false);
            }
        }
    }

    private static readonly SkiaReportRenderer Renderer = new();

    private static IReadOnlyList<byte[]> Render(IncentiveCards cards, string name)
    {
        var (caption, summary) = IncentiveTexts.Caption(cards);
        var document = new ReportDocument("incentive." + cards.Slot, ReportType.Incentive, "tp", "9. Накопительная акция", "ТП andijon 4 Мухаммаджон · Андижон", null, cards.Date,
            cards.Date.ToString("dd.MM.yyyy"), DateTimeOffset.UtcNow, cards.DataAsOf, cards.Language, [], [], [], [], caption, summary) { Incentive = cards, AsFiles = cards.V2 };
        var pages = Renderer.Render(document);
        if (Environment.GetEnvironmentVariable("ONEBASE_INCENTIVE_PNG") is { Length: > 0 } dir)
        {
            Directory.CreateDirectory(dir);
            for (var i = 0; i < pages.Count; i++)
            {
                File.WriteAllBytes(Path.Combine(dir, $"goals-{name}-{i + 1}.png"), pages[i]);
            }
        }

        foreach (var png in pages)
        {
            using var bitmap = SKBitmap.Decode(png);
            Assert.Equal(1080, bitmap.Width);
            Assert.InRange(bitmap.Height, 400, 9000);
        }

        return pages;
    }

    [Theory]
    [InlineData(IncentiveSlot.Morning, "ru")]
    [InlineData(IncentiveSlot.Midday, "ru")]
    [InlineData(IncentiveSlot.Evening, "ru")]
    [InlineData(IncentiveSlot.Week, "ru")]
    [InlineData(IncentiveSlot.Month, "ru")]
    [InlineData(IncentiveSlot.Team, "ru")]
    [InlineData(IncentiveSlot.Zone, "ru")]
    [InlineData(IncentiveSlot.Morning, "uz")]
    [InlineData(IncentiveSlot.Evening, "uz")]
    [InlineData(IncentiveSlot.Week, "uz")]
    [InlineData(IncentiveSlot.Month, "uz")]
    [InlineData(IncentiveSlot.Team, "uz")]
    [InlineData(IncentiveSlot.Zone, "uz")]
    [InlineData(IncentiveSlot.Evening, "uz-Cyrl")]
    public void Seven_cards_render_as_one_long_png_each(string slot, string language)
    {
        var cards = Sample(slot, language);
        Assert.True(cards.V2);
        var pages = Render(cards, $"{slot}-{language}");
        Assert.Single(pages);
    }

    [Fact]
    public void Morning_card_of_the_sample_route_is_about_4300_px_and_long_routes_stay_in_one_file()
    {
        using var bitmap = SKBitmap.Decode(Render(Sample(IncentiveSlot.Morning), "morning-height")[0]);
        Assert.InRange(bitmap.Height, 3500, 6500);
        Assert.Single(Render(Sample(IncentiveSlot.Evening, stores: 40), "evening-40"));
        Assert.Single(Render(Sample(IncentiveSlot.Morning, stores: 0), "morning-empty"));
    }

    [Fact]
    public void Totals_of_the_sample_route_match_the_excel_and_the_evening_sample()
    {
        var morning = IncentiveGoalTotals.Of(Route(IncentiveSlot.Morning, Monday));
        Assert.Equal((24, 6, 22, 1), (morning.Stores, morning.MogulGoals, morning.TwizosGoals, morning.Plain));
        Assert.Equal((215_835m, 868_242m), (morning.MogulGoalCashback, morning.TwizosGoalCashback));
        Assert.Equal(5, morning.Both); // Holiqova, Axmadjonova, Ravshanbekaka, Raxmatillo, Qochqarova — один визит, два предложения
        Assert.Equal((3, 17, 2), (morning.Segments[IncentiveTwizosSegment.Existing], morning.Segments[IncentiveTwizosSegment.New], morning.Segments[IncentiveTwizosSegment.Stand]));

        var evening = IncentiveGoalTotals.Of(Route(IncentiveSlot.Evening, Monday));
        Assert.Equal(4, evening.Woken); // Komiljon, Axmadjonova, Ravshanbekaka (½), Raxmatillo
        Assert.Equal([3, 8, 19, 22], evening.Refused); // «на мушке» образца 3
    }
}
