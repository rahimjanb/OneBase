using OneBase.Application.Telegram.Battle;
using OneBase.Application.Telegram.Incentive;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Incentive;
using OneBase.Infrastructure.Telegram.Reports;
using SkiaSharp;

namespace OneBase.AI.Tests;

/// <summary>
/// Карточки ТП с акциями ноября без базы — на данных образцов «Карточки ТК» (andijon 4, 24 магазина маршрута 09.11): 08:30, 13:00, 18:00,
/// неделя, месяц по-русски и по-узбекски; картинка не выше 2 560 px, длинный маршрут — следующими картинками; копии СВ без блоков предложений;
/// пустой маршрут. ONEBASE_INCENTIVE_PNG=папка — сохранить картинки.
/// </summary>
public class IncentivePromoCardTests
{
    private static readonly IReadOnlyDictionary<DateOnly, string> Holidays = new Dictionary<DateOnly, string> { [new(2026, 12, 8)] = "День Конституции" };
    private static readonly DateOnly Day = new(2026, 11, 9);
    private static readonly IncentiveAgent Me = new(582, "andijon 4 Мухаммаджон", "Андижон");

    internal static IncentiveSettings Settings() => IncentiveTests.Twizos() with { StoreSets = IncentivePromos.NovemberSets, Promos = IncentivePromos.November2026 };

    private static readonly (string Name, string Sub, string[] Sets, bool Plan, bool Sleeping, decimal Check)[] Specs =
    [
        ("Asqarova Xurshida", IncentiveTargetSub.Connect, ["C", "K140"], true, false, 950_000),
        ("Qambarova Zulfiya", IncentiveTargetSub.Reorder, ["C", "K140"], false, false, 880_000),
        ("Nomoniv Shaxbozbek", IncentiveTargetSub.Connect, ["K35"], false, false, 180_000),
        ("Anor Supermarket", IncentiveTargetSub.Connect, ["K35"], false, false, 210_000),
        ("Muxammadjonov Sardorbek", IncentiveTargetSub.Connect, ["K35"], false, false, 150_000),
        ("Komiljon Aka Abdullayev", IncentiveTargetSub.None, [], false, true, 120_000),
        ("Ergashova Azozaopa", IncentiveTargetSub.Connect, ["K140"], false, false, 260_000),
        ("Toyga Toyona", IncentiveTargetSub.Connect, ["K35"], false, false, 140_000),
        ("Ismoyil", IncentiveTargetSub.None, [], false, false, 57_000),
        ("Holiqova Gulnora", IncentiveTargetSub.Connect, ["K35"], false, true, 170_000),
        ("Mamajonov Abdullo Zafar", IncentiveTargetSub.Connect, ["C", "T"], true, false, 1_400_000),
        ("Axmadjonova Zuxra", IncentiveTargetSub.Connect, ["K35"], false, true, 200_000),
        ("Moydinova Shaxnoza", IncentiveTargetSub.Reorder, ["C", "K140"], false, false, 700_000),
        ("Moxigul (Mamajonova Dilnoza)", IncentiveTargetSub.Connect, ["K35"], false, false, 130_000),
        ("Ravshanbekaka", IncentiveTargetSub.Connect, ["K35"], false, true, 160_000),
        ("O'rmonova Shoira Opa", IncentiveTargetSub.Connect, ["K35"], false, false, 120_000),
        ("Nazarova Shaxlo", IncentiveTargetSub.Connect, ["K140"], false, false, 280_000),
        ("Mamajonov Raxmatillo", IncentiveTargetSub.Connect, ["K35"], false, true, 190_000),
        ("Shoxsanam Opa (Baxrom)", IncentiveTargetSub.Connect, ["K35"], false, false, 110_000),
        ("Isaqjanova Maxliyo Ikrom", IncentiveTargetSub.Connect, ["K35"], false, false, 100_000),
        ("Qochqarova Asalxon", IncentiveTargetSub.Connect, ["K35"], false, true, 140_000),
        ("Arzon Market", IncentiveTargetSub.Connect, ["K35"], false, false, 230_000),
        ("Otajonov Nurbek", IncentiveTargetSub.Connect, ["K35"], false, false, 90_000),
        ("Tojiddin", IncentiveTargetSub.Reorder, ["T", "K140"], false, false, 520_000),
    ];

    // Итоги образца: B — взяли всё, H — один подарок из пары, X — отказ, O — обычный заказ, Y — Yamelly спящей, h — Twizos взят, Yamelly нет.
    private const string Evening = "BHBXBYBXOhBBBXBBXhBBBXBH";

    private static IncentiveStore Store(IncentiveSettings s, int i, string slot)
    {
        var spec = Specs[i % Specs.Length];
        var sets = spec.Sets.Select(code => s.SetByCode(code)!).ToList();
        var target = sets.Sum(x => x.Kg);
        var outcome = slot switch
        {
            IncentiveSlot.Midday when i < 12 => Evening[i],
            IncentiveSlot.Evening when i < Evening.Length => Evening[i],
            _ => '-',
        };
        var twizos = sets.Count == 0 ? null : outcome switch
        {
            'B' or 'h' => BattleStoreStatus.Taken,
            'H' => BattleStoreStatus.Partial,
            'X' => BattleStoreStatus.Refused,
            _ => BattleStoreStatus.Ahead,
        };
        var yamelly = !spec.Sleeping ? null : outcome switch
        {
            'B' or 'Y' => BattleStoreStatus.Taken,
            'X' or 'h' => BattleStoreStatus.Refused,
            _ => BattleStoreStatus.Ahead,
        };
        var gifts = twizos switch
        {
            BattleStoreStatus.Taken => sets.Select(x => x.Gift!).ToList(),
            BattleStoreStatus.Partial => [sets[^1].Gift!],
            _ => [],
        };
        var kg = twizos == BattleStoreStatus.Taken ? target : twizos == BattleStoreStatus.Partial ? sets[^1].Kg : 0;
        var status = outcome switch
        {
            'B' or 'Y' or 'O' => BattleStoreStatus.Taken,
            'H' or 'h' => BattleStoreStatus.Partial,
            'X' => BattleStoreStatus.Refused,
            _ => BattleStoreStatus.Ahead,
        };
        return new IncentiveStore(70_000 + i, spec.Name, i + 1, spec.Sub, string.Join(" + ", sets.Select(x => x.Contents)), target, sets.FirstOrDefault()?.Prize, null, status,
            kg, status != BattleStoreStatus.Ahead, spec.Sets, spec.Plan, spec.Sleeping, IncentivePlacement.Route, spec.Check, gifts.Count, gifts, twizos, yamelly,
            yamelly == BattleStoreStatus.Taken ? 26_338 : 0, outcome == 'O' || status is BattleStoreStatus.Taken or BattleStoreStatus.Partial);
    }

    private static IncentiveRoute Route(IncentiveSettings s, string slot, DateOnly date, int count = 24)
    {
        var stores = Enumerable.Range(0, count).Select(i => Store(s, i, slot)).ToList();
        var extra = new IncentiveStore(80_001, "Oyatlloh Otabekakaogli", 1001, IncentiveTargetSub.Connect, "", 10.22m, "чайник", null, BattleStoreStatus.Ahead, 0, false, ["C", "K140"],
            Placement: IncentivePlacement.Extra, AvgCheck: 5_000_000, TwizosStatus: BattleStoreStatus.Ahead);
        IReadOnlyList<IncentiveOutOfRouteSale> sold = slot == IncentiveSlot.Morning ? [] : [new(80_002, "Navbahor do'kon", 6.58m, 599_400, ["kettle"], 0)];
        return new IncentiveRoute(Me.Id, date, "linko", stores, count + 16, [extra], sold);
    }

    private static IncentiveTopSku Sku(long id, string name, decimal unit, int akb, decimal kg) => new(id, name, null, unit, akb, kg);

    private static readonly IReadOnlyList<IncentiveTopSku> TopAkb =
    [
        Sku(303, "605  «Twizos» gazaklari Shashlik ta'mli o'rama tayoqchalar (35г / 28шт)", 0.035m, 140, 210),
        Sku(306, "614 «Twizos» gazaklari smetana va piyoz ta'mli o'rama tayoqchalar (35г / 28шт)", 0.035m, 120, 180),
    ];

    private static readonly IReadOnlyList<IncentiveTopSku> TopKg =
    [
        Sku(305, "613 «Twizos» gazaklari smetana va piyoz ta'mli o'rama tayoqchalar (140г / 26шт)", 0.14m, 80, 320),
        Sku(302, "604  «Twizos» gazaklari Shashlik ta'mli o'rama tayoqchalar (140г / 26шт)", 0.14m, 75, 300),
    ];

    private static IncentiveAgentCard DayCard(IncentiveSettings s, string slot, IncentiveRoute route)
    {
        var today = slot switch { IncentiveSlot.Midday => 47m, IncentiveSlot.Evening => 81.5m, _ => 0m };
        var standing = new IncentiveStanding(Me, false, 105.8m, 0, today, new Dictionary<DateOnly, decimal> { [new(2026, 10, 15)] = 105.8m, [Day] = today });
        var firstDay = slot == IncentiveSlot.Evening ? Day.AddDays(1) : Day;
        var progress = IncentiveRules.Progress(s, standing.Total, firstDay, Holidays);
        var dayStart = IncentiveRules.Progress(s, standing.Total - today, Day, Holidays);
        var woken = route.SleepingWoken;
        var balance = new IncentiveCashbackBalance(Me.Id, 46, woken, woken * 26_338m, woken * 26_338m, 0, 0, 0, 0, (46 - woken) * 26_338m,
            [new(new DateOnly(2026, 11, 9), woken, woken * 26_338m, 0, 0)]);
        var stores = route.Stores;
        var goals = new IncentiveGoalsSummary(stores.Count, stores.Count(x => x.Sub == IncentiveTargetSub.Reorder), stores.Count(x => x.Sub == IncentiveTargetSub.Connect && !x.PlanAdded),
            stores.Count(x => x.PlanAdded), stores.Count(x => x.Sleeping), stores.Count(x => x.Plain), stores.Sum(x => x.TargetKg), dayStart.Norm,
            dayStart.Norm is { } n ? Math.Round(n * 1.3m, 1) : null, 1, 10.22m);
        var cashbackToday = stores.Sum(x => x.Cashback);
        var promo = new IncentivePromoView(new IncentiveSleepingView(46, woken, 14, 3), balance, cashbackToday,
            new IncentiveBestView(today, today >= 6 ? 1 : null, false, false, 6, Me.Name, today, 0, 500_000), TopAkb, TopKg, goals, [], route.GiftsTaken.Values.Sum(),
            route.GiftsTaken, null);
        return new IncentiveAgentCard(Me, standing, progress, dayStart, route, null, null, 3, 3, 120, promo);
    }

    private static IncentiveAgentCard WeekCard(IncentiveSettings s)
    {
        var monday = Day;
        decimal[] kg = [82, 61, 78, 44, 92, 40];
        int[] counts = [24, 16, 21, 22, 27, 14];
        (int Woken, int Planned)[] sleeping = [(4, 6), (2, 3), (3, 4), (1, 3), (3, 5), (1, 3)];
        int[] sets = [21, 13, 16, 9, 19, 8];
        var days = Enumerable.Range(0, 6).Select(i => new IncentiveWeekDay(monday.AddDays(i), kg[i], Route(s, IncentiveSlot.Evening, monday.AddDays(i), counts[i]))).ToList();
        var promoDays = Enumerable.Range(0, 6).Select(i => new IncentivePromoDay(monday.AddDays(i), kg[i], sets[i], sleeping[i].Planned, sleeping[i].Woken, sleeping[i].Woken * 26_338m,
            i == 4, 85)).ToList();
        var total = kg.Sum();
        var saturday = monday.AddDays(5);
        var standing = new IncentiveStanding(Me, false, 105.8m + total, 0, 0, new Dictionary<DateOnly, decimal> { [new(2026, 10, 15)] = 105.8m, [monday] = total });
        var balance = new IncentiveCashbackBalance(Me.Id, 46, 14, 368_732, 52_676, 316_056, 0, 0, 0, 32 * 26_338m,
            [new(new DateOnly(2026, 11, 2), 0, 0, 0, 0), new(monday, 14, 368_732, 316_056, 0)]);
        var promo = new IncentivePromoView(new IncentiveSleepingView(46, 14, 14, 3), balance, 0, new IncentiveBestView(40, 2, false, false, 6, Me.Name, 44, 1, 500_000), TopAkb, TopKg,
            null, promoDays, sets.Sum(), new Dictionary<string, int> { ["mug"] = 60, ["thermos"] = 14, ["kettle"] = 12 }, new IncentiveEarnings(368_732, 0, 1, 500_000, 0));
        return new IncentiveAgentCard(Me, standing, IncentiveRules.Progress(s, standing.Total, saturday.AddDays(2), Holidays),
            IncentiveRules.Progress(s, standing.Total, saturday, Holidays), null, new IncentiveWeekView(monday, saturday, days, 85, 508, []), null, 3, 4, 120, promo);
    }

    private static IncentiveAgentCard MonthCard(IncentiveSettings s)
    {
        var date = new DateOnly(2026, 11, 30);
        var standing = new IncentiveStanding(Me, false, 717.8m, 0, 0, new Dictionary<DateOnly, decimal> { [new(2026, 10, 15)] = 105.8m, [new(2026, 11, 20)] = 612m });
        var weeks = new List<IncentiveMonthWeek> { new(1, new(2026, 11, 2), new(2026, 11, 8), 40, 120), new(2, Day, new(2026, 11, 15), 396, 120) };
        var promo = new IncentivePromoView(new IncentiveSleepingView(46, 21, 14, 0), null, 0, new IncentiveBestView(0, null, false, false, 6, null, 0, 3, 500_000), TopAkb, TopKg,
            null, [], 104, new Dictionary<string, int>(), new IncentiveEarnings(612_400, 560_086, 3, 1_500_000, 1_500_000));
        return new IncentiveAgentCard(Me, standing, IncentiveRules.Progress(s, standing.Total, date.AddDays(1), Holidays), IncentiveRules.Progress(s, standing.Total, date, Holidays),
            null, null, new IncentiveMonthView(2026, 11, 612, 105.8m, weeks, s.Tiers[0], 525, false), 2, 2, 120, promo);
    }

    internal static IncentiveCards Sample(string slot, string language = "ru", int stores = 24)
    {
        var s = Settings();
        var date = slot switch { IncentiveSlot.Week => Day.AddDays(5), IncentiveSlot.Month => new DateOnly(2026, 11, 30), _ => Day };
        var card = slot switch
        {
            IncentiveSlot.Week => WeekCard(s),
            IncentiveSlot.Month => MonthCard(s),
            _ => DayCard(s, slot, Route(s, slot, Day, stores)),
        };
        return new IncentiveCards(slot, date, s, language, [card], null, null, IncentiveRules.MiddayShare(new(8, 30), new(13, 0), new(18, 0)),
            new DateTimeOffset(2026, 11, 9, 3, 15, 0, TimeSpan.Zero), false);
    }

    private static readonly SkiaReportRenderer Renderer = new();

    private static IReadOnlyList<byte[]> Render(IncentiveCards cards, string name, int maxHeight = 2560)
    {
        var (caption, summary) = IncentiveTexts.Caption(cards);
        var document = new ReportDocument("incentive." + cards.Slot, ReportType.Battle, "tp", "9. Накопительная акция", "ТП andijon 4 Мухаммаджон · Андижон", null, cards.Date,
            cards.Date.ToString("dd.MM.yyyy"), DateTimeOffset.UtcNow, cards.DataAsOf, cards.Language, [], [], [], [], caption, summary) { Incentive = cards };
        var pages = Renderer.Render(document);
        if (Environment.GetEnvironmentVariable("ONEBASE_INCENTIVE_PNG") is { Length: > 0 } dir)
        {
            Directory.CreateDirectory(dir);
            for (var i = 0; i < pages.Count; i++)
            {
                File.WriteAllBytes(Path.Combine(dir, $"promo-{name}-{i + 1}.png"), pages[i]);
            }
        }

        foreach (var png in pages)
        {
            using var bitmap = SKBitmap.Decode(png);
            Assert.Equal(1080, bitmap.Width);
            Assert.InRange(bitmap.Height, 400, maxHeight);
        }

        return pages;
    }

    [Theory]
    [InlineData(IncentiveSlot.Morning, "ru")]
    [InlineData(IncentiveSlot.Midday, "ru")]
    [InlineData(IncentiveSlot.Evening, "ru")]
    [InlineData(IncentiveSlot.Week, "ru")]
    [InlineData(IncentiveSlot.Month, "ru")]
    [InlineData(IncentiveSlot.Morning, "uz")]
    [InlineData(IncentiveSlot.Midday, "uz")]
    [InlineData(IncentiveSlot.Evening, "uz")]
    [InlineData(IncentiveSlot.Week, "uz")]
    [InlineData(IncentiveSlot.Month, "uz")]
    public void November_cards_render_within_telegram_photo_limits(string slot, string language)
    {
        var pages = Render(Sample(slot, language), $"{slot}-{language}");
        // 24 магазина утром — две картинки (над полем — плитки, дорога, предложения, как собраны цели); днём и вечером — тоже не больше трёх.
        Assert.InRange(pages.Count, 1, IncentiveSlot.IsDay(slot) ? 3 : 1);
    }

    [Fact]
    public void Long_routes_continue_on_next_images_and_empty_routes_still_render()
    {
        var long40 = Render(Sample(IncentiveSlot.Evening, stores: 40), "evening-40");
        Assert.InRange(long40.Count, 2, 4);
        var cards = Sample(IncentiveSlot.Morning);
        var empty = cards with { Agents = [cards.Agents[0] with { Route = new IncentiveRoute(Me.Id, Day, null, [], 0) }] };
        Assert.Single(Render(empty, "morning-empty"));
        // Копии СВ (несколько ТП) — без блоков «Что предлагаем» и «Как собраны цели»: короче, чем у ТП.
        var copies = cards with { Agents = [cards.Agents[0], cards.Agents[0] with { Agent = new IncentiveAgent(583, "andijon 1 Олимжон", "Андижон") }] };
        var own = Render(cards, "morning-own");
        var copy = Render(copies, "morning-copies");
        Assert.True(copy.Count / 2 <= own.Count);
        using var a = SKBitmap.Decode(own[0]);
        using var b = SKBitmap.Decode(copy[0]);
        Assert.NotEqual(Convert.ToBase64String(own[0]), Convert.ToBase64String(copy[0]));
    }

    private static IncentiveCards OnePhoto(IncentiveCards cards) => cards with { Program = cards.Program with { OnePhoto = true } };

    [Fact]
    public void One_photo_setting_keeps_each_card_on_a_single_image()
    {
        // Без настройки длинный маршрут и сводка по стране продолжаются следующей картинкой; с ней — одна высокая картинка со всем.
        Assert.True(Render(Sample(IncentiveSlot.Morning), "split-morning").Count > 1);
        Assert.True(Render(Sample(IncentiveSlot.Evening, stores: 40), "split-evening-40").Count > 1);
        foreach (var (cards, name) in new[]
                 {
                     (Sample(IncentiveSlot.Morning), "one-morning"), (Sample(IncentiveSlot.Midday), "one-midday"), (Sample(IncentiveSlot.Evening, stores: 40), "one-evening-40"),
                     (Sample(IncentiveSlot.Morning, "uz"), "one-morning-uz"), (Managers(IncentiveSlot.Summary, "ru", 20), "one-summary-20"),
                 })
        {
            var pages = Render(OnePhoto(cards), name, 8_920);
            Assert.Single(pages);
            using var bitmap = SKBitmap.Decode(pages[0]);
            Assert.True(bitmap.Height > 2560 || name.StartsWith("one-summary", StringComparison.Ordinal), $"{name}: {bitmap.Height}");
            // Фото Telegram — до 10 МБ.
            Assert.True(pages[0].Length < 10_000_000, $"{name}: {pages[0].Length} байт");
        }

        // Очень длинный маршрут — всё равно одна картинка: уменьшенная так, чтобы ширина + высота ≤ 10 000 px (предел фото Telegram).
        var huge = Renderer.Render(Document(OnePhoto(Sample(IncentiveSlot.Morning, stores: 120))));
        Assert.Single(huge);
        using var big = SKBitmap.Decode(huge[0]);
        Assert.True(big.Width + big.Height <= 10_000, $"{big.Width}×{big.Height}");
    }

    [Theory]
    [InlineData("andijon 1 Олимжон", "ru", "Андижон 1 Олимжон")]
    [InlineData("andijon 1 Олимжон", "uz-Cyrl", "Андижон 1 Олимжон")]
    [InlineData("andijon 1 Олимжон", "uz", "andijon 1 Олимжон")]
    [InlineData("Санжар Сатторов (Romitan)", "ru", "Санжар Сатторов (Ромитан)")]
    [InlineData("Озода Иброхимова  GOOD FOOD", "ru", "Озода Иброхимова  GOOD FOOD")]
    [InlineData("Abdurahmon Sharifov", "ru", "Abdurahmon Sharifov")]
    [InlineData("ТП4 Сардор Яшнабод 1", "ru", "ТП4 Сардор Яшнабод 1")]
    public void Agent_name_is_written_in_one_script_on_the_card(string name, string language, string expected) =>
        Assert.Equal(expected, IncentiveTexts.Person(name, language));

    private static ReportDocument Document(IncentiveCards cards) =>
        new("incentive." + cards.Slot, ReportType.Battle, "tp", "9. Накопительная акция", "ТП", null, cards.Date, cards.Date.ToString("dd.MM.yyyy"), DateTimeOffset.UtcNow,
            cards.DataAsOf, cards.Language, [], [], [], [], "", "") { Incentive = cards };

    private static IncentiveCards Managers(string slot, string language = "ru", int regions = 4)
    {
        var s = Settings();
        var holidays = Holidays;
        IncentiveProgress Progress(decimal total) => IncentiveRules.Progress(s, total, Day.AddDays(1), holidays);
        if (slot == IncentiveSlot.Team)
        {
            (string Name, int Planned, int Woken, decimal Kg, int Sets, decimal Total, int? Place)[] people =
            [
                ("andijon 4 Мухаммаджон", 3, 2, 42.9m, 8, 148.7m, 1), ("andijon 1 Олимжон", 3, 2, 18m, 4, 50.9m, 3), ("andijon 3 Достон", 3, 1, 12m, 3, 13.2m, 5),
                ("599 andijon 6", 1, 1, 9m, 2, 19.4m, 8), ("andijon 2 Исмоилжон", 3, 0, 4m, 1, 5.9m, null), ("andijon 5 Нуриддин", 1, 0, 0, 0, 0, null),
            ];
            var rows = people.Select((p, i) => new IncentiveTeamDayRow(new IncentiveAgent(580 + i, p.Name, "Андижон"), p.Planned, p.Woken, p.Kg, 45, p.Sets, p.Woken * 26_338m,
                p.Kg > 0 ? 1 : 3, Progress(p.Total), p.Total, p.Place, p.Place == 1, false)).ToList();
            var team = new IncentiveTeamDay("Андижон · СВ Азиз Каримов", rows, new Dictionary<string, int> { ["kettle"] = 5, ["thermos"] = 6, ["mug"] = 7 });
            return new IncentiveCards(slot, Day, s, language, [], null, null, 0.47m, new DateTimeOffset(2026, 11, 9, 14, 0, 0, TimeSpan.Zero), false, team);
        }

        string[] names = ["Андижон", "Коканд", "Наманган", "Фаргона", "Самарканд", "Бухоро", "Хоразм", "Ташкент", "Жиззах", "Карши", "Навои", "Нукус", "Термез", "Денов",
            "Сырдарья", "Бекобод", "Кибрай", "Шахрисабз", "Чирчик", "Янгийул"];
        var list = Enumerable.Range(0, regions).Select(i => new IncentiveZoneRegion(names[i % names.Length], "СВ " + (i + 1), 6, 820 - i * 20, Math.Max(12, 312 - i * 14 + (i == 2 ? -150 : 0)),
            Math.Max(80, 1140 - i * 50), 31 + i, new Dictionary<string, int> { ["mug"] = 20 + i, ["thermos"] = 8, ["kettle"] = 3 + i },
            new Dictionary<string, decimal> { ["mug"] = Math.Max(40, 90 - i * 3), ["thermos"] = 70, ["kettle"] = i == 1 ? 2 : 30 }, i % 2, 1200, 2400)).ToList();
        var summary = slot == IncentiveSlot.Summary;
        var from = summary ? Day.AddDays(-7) : Day;
        var territories = summary
            ? new List<IncentiveTerritoryState> { new("Самарканд", 22_500, 9_800, new Dictionary<long, decimal>()), new("Кибрай", 600, 540, new Dictionary<long, decimal>()),
                new("Олмалык", 730, 350, new Dictionary<long, decimal>()), new("Пскент", 600, 610, new Dictionary<long, decimal>()) }
            : null;
        var budget = summary
            ? new IncentiveBudgetView(26_900_000, 21_000_000, 60_000_000, 4_500_000, 3_000_000, 21_000_000, new Dictionary<string, int> { ["mug"] = 640, ["thermos"] = 210, ["kettle"] = 95 },
                new Dictionary<string, int> { ["mug"] = 2000, ["thermos"] = 2000, ["kettle"] = 1000 })
            : null;
        var zone = new IncentiveZone(summary ? "Вся страна" : "РМ 2 · Андижон · Коканд · Наманган · Фергана", from, from.AddDays(5), list, 26_900_000, 4_500_000,
            new IncentiveAgent(582, "andijon 4 Мухаммаджон", "Андижон"), 292, Progress(292), territories, budget, 9_400);
        return new IncentiveCards(slot, summary ? Day.AddDays(7) : Day.AddDays(5), s, language, [], null, null, 0.47m, new DateTimeOffset(2026, 11, 14, 14, 0, 0, TimeSpan.Zero),
            false, null, zone);
    }

    [Theory]
    [InlineData(IncentiveSlot.Team, "ru", 4)]
    [InlineData(IncentiveSlot.Zone, "ru", 4)]
    [InlineData(IncentiveSlot.Summary, "ru", 20)]
    [InlineData(IncentiveSlot.Team, "uz", 4)]
    [InlineData(IncentiveSlot.Zone, "uz", 4)]
    [InlineData(IncentiveSlot.Summary, "uz", 20)]
    public void Manager_cards_render_for_supervisor_manager_and_summary(string slot, string language, int regions)
    {
        var pages = Render(Managers(slot, language, regions), $"{slot}-{language}");
        Assert.InRange(pages.Count, 1, 2);
        var (caption, _) = IncentiveTexts.Caption(Managers(slot, language, regions));
        Assert.StartsWith(slot switch { IncentiveSlot.Team => "Итог дня команды", IncentiveSlot.Zone => "Итог недели зоны", _ => "Сводка акций" }, caption);
    }

    [Fact]
    public void Afternoon_card_is_the_16_00_interim_cut()
    {
        var cards = Sample(IncentiveSlot.Midday) with { Slot = IncentiveSlot.Afternoon };
        Assert.NotEmpty(Render(cards, "afternoon-ru"));
        Assert.StartsWith("16:00: сегодня", IncentiveTexts.Caption(cards).Caption);
    }

    [Fact]
    public void Programs_without_november_promos_keep_the_previous_cards()
    {
        var cards = IncentiveCardTests.Sample(IncentiveSlot.Morning);
        Assert.Null(cards.Agents[0].Promo);
        var pages = Renderer.Render(new ReportDocument("incentive.morning", ReportType.Battle, "tp", "9", "ТП", null, cards.Date, "", DateTimeOffset.UtcNow, cards.DataAsOf,
            "ru", [], [], [], [], "", "") { Incentive = cards });
        Assert.Single(pages);
    }
}
