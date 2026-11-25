using OneBase.Application.Telegram.Incentive;
using OneBase.Domain.Incentive;

namespace OneBase.AI.Tests;

/// <summary>
/// «Цели на маршрут» v2 — правила на числах ТЗ v2 (24.11.2026) и Excel-образца «Пример_расчёта_целей_andijon4.xlsx»: все 24 точки маршрута
/// понедельника andijon 4 — коробки и фасовка Могул, скидка и кешбек, сегмент Twizos, набор, сумма, кг, подарок и кешбек (до сума); лестница,
/// квота стоек, сегмент кешбека на момент заказа, отметки ✓ / ½ / ✗.
/// </summary>
public class IncentiveGoalTests
{
    private static readonly IncentiveGoalsPromo G = IncentivePromos.Goals2026;
    private static readonly IncentiveTwizosPromo Tw = IncentivePromos.November2026.Twizos!;
    private static readonly IncentiveYamellyPromo Ym = IncentivePromos.November2026.Yamelly!;

    /// <summary>Строка Excel: ID ТТ, средний чек, Могул (кг/мес, фасовка) или null, обычная накладная Twizos (действующим) или null.</summary>
    private sealed record Row(long Id, string Name, decimal Check, decimal? MogulKg, string? Packing, decimal? Invoice,
        // Ожидание Могул: коробок, сумма, со скидкой, кешбек.
        int Boxes, decimal MSum, decimal MAfter, decimal MCashback,
        // Ожидание Twizos: сегмент (null — без цели), %, набор, сумма, кг, подарок, кешбек.
        string? Segment, decimal Pct, string? Set, decimal TSum, decimal TKg, string? Gift, decimal TCashback);

    private static readonly Row[] Excel =
    [
        new(60994, "Asqarova Xurshida", 700_920, null, null, null, 0, 0, 0, 0, "stand", 10, "35x1+140x1+200x1", 688_200, 8.62m, "kettle", 68_820),
        new(61030, "Qambarova Zulfiya", 1_104_212, null, null, 1_872_200, 0, 0, 0, 0, "existing", 5, "140x8+35x2", 2_516_000, 31.08m, "kettle", 125_800),
        new(61094, "Nomoniv Shaxbozbek", 170_062, null, null, null, 0, 0, 0, 0, "new", 8, "35x3", 310_800, 2.94m, "mug", 24_864),
        new(61106, "Anor Supermarket", 233_000, null, null, null, 0, 0, 0, 0, "new", 8, "35x3", 310_800, 2.94m, "mug", 24_864),
        new(61139, "Muxammadjonov Sardorbek", 90_250, null, null, null, 0, 0, 0, 0, "new", 8, "35x3", 310_800, 2.94m, "mug", 24_864),
        new(61166, "Komiljon Aka Abdullayev", 42_550, 2.1m, "420 г", null, 2, 283_200, 263_376, 26_338, null, 0, null, 0, 0, null, 0),
        new(61256, "Ergashova Azozaopa", 252_650, null, null, null, 0, 0, 0, 0, "new", 8, "140x1+35x1", 392_200, 4.62m, "thermos", 31_376),
        new(61257, "Toyga Toyona", 225_000, null, null, null, 0, 0, 0, 0, "new", 8, "35x3", 310_800, 2.94m, "mug", 24_864),
        new(61278, "Ismoyil", 56_925, null, null, null, 0, 0, 0, 0, null, 0, null, 0, 0, null, 0),
        new(61290, "Holiqova Gulnora", 134_625, 2.52m, "420 г", null, 2, 283_200, 263_376, 26_338, "new", 8, "35x3", 310_800, 2.94m, "mug", 24_864),
        new(61490, "Mamajonov Abdullo.Zarnigor Opa", 2_525_490, null, null, null, 0, 0, 0, 0, "stand", 10, "35x1+140x1+200x1", 688_200, 8.62m, "kettle", 68_820),
        new(61596, "Axmadjonova Zuxra", 75_000, 7.04m, "420 г", null, 3, 424_800, 395_064, 39_506, "new", 8, "35x3", 310_800, 2.94m, "mug", 24_864),
        new(61853, "Moydinova Shaxnoza", 849_659, null, null, 1_975_800, 0, 0, 0, 0, "existing", 5, "140x10+35x2", 3_093_200, 38.36m, "kettle", 154_660),
        new(61873, "Moxigul (Mamajonova Nafisaxon)", 150_150, null, null, null, 0, 0, 0, 0, "new", 8, "35x3", 310_800, 2.94m, "mug", 24_864),
        new(61943, "Ravshanbekaka", 115_500, 5.04m, "420 г", null, 2, 283_200, 263_376, 26_338, "new", 8, "35x3", 310_800, 2.94m, "mug", 24_864),
        new(61999, "O'rmonova Shoira Opa", 141_600, null, null, null, 0, 0, 0, 0, "new", 8, "35x3", 310_800, 2.94m, "mug", 24_864),
        new(62198, "Nazarova Shaxlo", 295_887, null, null, null, 0, 0, 0, 0, "new", 8, "140x1+35x1", 392_200, 4.62m, "thermos", 31_376),
        new(62199, "Mamajonov Raxmatillo", 150_000, 10.46m, "420 г", null, 4, 566_400, 526_752, 52_675, "new", 8, "35x3", 310_800, 2.94m, "mug", 24_864),
        new(62256, "Shoxsanam Opa (Baxrom Aka) Dexqon Bozor Ichi", 141_600, null, null, null, 0, 0, 0, 0, "new", 8, "35x3", 310_800, 2.94m, "mug", 24_864),
        new(62288, "Isaqjanova Maxliyo Ikromaka", 198_300, null, null, null, 0, 0, 0, 0, "new", 8, "35x3", 310_800, 2.94m, "mug", 24_864),
        new(62302, "Qochqarova Asalxon", 218_100, 7.36m, "1 кг", null, 3, 480_000, 446_400, 44_640, "new", 8, "35x3", 310_800, 2.94m, "mug", 24_864),
        new(62509, "Arzon Market", 77_700, null, null, null, 0, 0, 0, 0, "new", 8, "35x3", 310_800, 2.94m, "mug", 24_864),
        new(62664, "Otajonov Nurbek", 131_960, null, null, null, 0, 0, 0, 0, "new", 8, "35x3", 310_800, 2.94m, "mug", 24_864),
        new(62689, "Tojiddin", 245_067, null, null, 162_800, 0, 0, 0, 0, "existing", 5, "140x1", 288_600, 3.64m, "mug", 14_430),
    ];

    [Fact]
    public void All_24_points_of_andijon_4_match_the_excel_sample()
    {
        // Андижон: 14 стоек на складе ÷ 6 ТП → квота 3; новые клиенты с чеком от 600 тыс. — Asqarova и Mamajonov — обоим стойка.
        var quota = IncentiveGoalRules.StandQuota(IncentiveGoalRules.StandTarget(G, 14), 6);
        Assert.Equal(3, quota);
        var stands = IncentiveGoalRules.StandOffers(
            Excel.Where(r => IncentiveGoalRules.StandCandidate(G, r.Invoice is not null, r.Check)).Select(r => (r.Id, r.Check)), quota);
        Assert.Equal([60994L, 61490L], stands.Order());

        foreach (var r in Excel)
        {
            if (r.MogulKg is { } kg)
            {
                var box = G.MogulBoxes!.Single(b => b.Name == r.Packing);
                var m = IncentiveGoalRules.MogulGoal(G, Ym, kg, 1, box);
                Assert.True((r.Boxes, r.MSum, r.MAfter, r.MCashback) == (m.Boxes, m.Sum, m.AfterDiscount, m.Cashback),
                    $"{r.Name}: Могул {m.Boxes} кор. {m.Box} {m.Sum} → {m.AfterDiscount} → {m.Cashback}");
                Assert.Equal(r.Packing, m.Box);
                Assert.Equal(Math.Round(kg * 1.2m, 3), m.TargetKg);
            }

            var t = IncentiveGoalRules.TwizosGoal(G, Tw, r.Invoice is not null, r.Invoice ?? 0, r.Check, stands.Contains(r.Id));
            if (r.Segment is null)
            {
                Assert.True(t is null, $"{r.Name}: без цели Twizos");
                continue;
            }

            Assert.NotNull(t);
            Assert.True((r.Segment, r.Pct, r.Set, r.TSum, r.TKg, r.Gift, r.TCashback) == (t.Segment, t.Percent, t.Set, t.Sum, t.Kg, t.Gift, t.Cashback),
                $"{r.Name}: Twizos {t.Segment} {t.Percent}% {t.Set} {t.Sum} {t.Kg} {t.Gift} {t.Cashback}");
        }

        // Итого по образцу: 6 спящих Могул (кешбек до 215 835), 22 цели Twizos (кешбек до 868 242), 1 ТТ без акции.
        Assert.Equal(215_835m, Excel.Where(r => r.MogulKg is not null).Sum(r => r.MCashback));
        Assert.Equal(22, Excel.Count(r => r.Segment is not null));
        Assert.Equal(868_242m, Excel.Sum(r => r.TCashback));
        Assert.Single(Excel, r => r.Segment is null && r.MogulKg is null);
    }

    [Fact]
    public void Ladder_goes_up_to_the_next_gift_or_plus_30_percent_and_can_switch_to_the_cheapest_set()
    {
        // ТЗ 4.3: Tojiddin — обычно 162 800 → следующий подарок 288 600 → 1 кор. 140 г; Qambarova — 1 872 200 × 1,3 = 2 433 860 → 8 × 140 + 2 × 35.
        Assert.Equal(288_600m, IncentiveGoalRules.ExistingTarget(G, Tw, 162_800));
        Assert.Equal(2_433_860m, IncentiveGoalRules.ExistingTarget(G, Tw, 1_872_200));
        Assert.Equal(392_200m, IncentiveGoalRules.ExistingTarget(G, Tw, 300_000)); // 390 000 < термоса 392 200
        Assert.Equal("140x1+35x1", IncentiveGoalRules.Ladder(G, 392_200));
        Assert.Equal("140x1+35x3", IncentiveGoalRules.Ladder(G, 584_600));
        Assert.Equal("140x8+35x2", IncentiveGoalRules.Ladder(G, 2_433_860));

        // Moydinova: цель 2 568 540 — по лестнице образца 10 × 140 + 2 × 35 = 3 093 200; самый дешёвый набор — 8 × 140 + 3 × 35 = 2 619 600.
        Assert.Equal("140x10+35x2", IncentiveGoalRules.Ladder(G, 1_975_800m * 1.3m));
        var minimal = G with { LadderMode = IncentiveLadderMode.Minimal };
        Assert.Equal("140x8+35x3", IncentiveGoalRules.Ladder(minimal, 1_975_800m * 1.3m));
        Assert.Equal(2_619_600m, IncentiveGoalRules.Sum(minimal, "140x8+35x3"));
        Assert.Equal("140x1", IncentiveGoalRules.Ladder(minimal, 250_000));

        // Дальше лестницы: 140 г × N + 35 г × 2, N — вверх.
        Assert.Equal("140x14+35x2", IncentiveGoalRules.Ladder(G, 4_000_000));
        Assert.True(IncentiveGoalRules.Sum(G, "140x14+35x2") >= 4_000_000);
        Assert.Equal([new IncentiveBoxCount("140", 8), new IncentiveBoxCount("35", 2)], IncentiveGoalRules.ParseSet("140x8 + 35x2"));
        Assert.Equal("35x1+140x1+200x1", IncentiveGoalRules.FormatSet(IncentiveGoalRules.ParseSet(G.StandSet)));
    }

    [Fact]
    public void New_big_client_without_quota_gets_the_kettle_set_at_8_percent_and_quota_runs_out_by_check()
    {
        var big = IncentiveGoalRules.TwizosGoal(G, Tw, false, 0, 700_000, false)!;
        Assert.Equal((IncentiveTwizosSegment.New, 8m, "140x1+35x3", 599_400m, "kettle", 47_952m, false),
            (big.Segment, big.Percent, big.Set, big.Sum, big.Gift, big.Cashback, big.Stand));

        // Квота 3 — стойку предлагаем трём самым крупным; четвёртый — набор без стойки.
        var offers = IncentiveGoalRules.StandOffers([(1, 650_000), (2, 2_000_000), (3, 900_000), (4, 900_000)], 3);
        Assert.Equal([2L, 3L, 4L], offers.Order());
        Assert.Empty(IncentiveGoalRules.StandOffers([(1, 650_000)], 0));

        // Действующему со средним чеком меньше 60 тыс. цель всё равно ставится (ТЗ: без цели — только если Twizos не брала).
        Assert.NotNull(IncentiveGoalRules.TwizosGoal(G, Tw, true, 50_000, 40_000, false));
        Assert.Null(IncentiveGoalRules.TwizosGoal(G, Tw, false, 0, 59_999, false));

        // Цель региона: на складе 0 (Коканд) — 40 стоек; склада нет — тоже 40; Денов 8 ÷ 3 ТП → 3.
        Assert.Equal(40, IncentiveGoalRules.StandTarget(G, 0));
        Assert.Equal(40, IncentiveGoalRules.StandTarget(G, null));
        Assert.Equal(3, IncentiveGoalRules.StandQuota(8, 3));
        Assert.Equal(0, IncentiveGoalRules.StandQuota(0, 3));
    }

    [Fact]
    public void Cashback_segment_is_taken_at_order_time_and_examples_of_section_5_match()
    {
        // Раздел 5: 283 200 → 263 376 → 26 338; 424 800 → 395 064 → 39 506.
        Assert.Equal(26_338m, IncentiveGoalRules.MogulGoal(G, Ym, 0, 0, G.MogulBoxes![0]).Cashback);
        Assert.Equal(39_506m, IncentiveGoalRules.MogulGoal(G, Ym, 7.04m, 2, G.MogulBoxes[0]).Cashback);

        // Новый со стойкой от 600 000 — 10%; без стойки — 8%; стойка, но закуп меньше 600 000 — 8%; уже брал — 5% (577 200 × 5% = 28 860).
        Assert.Equal((IncentiveTwizosSegment.Stand, 10m), IncentiveGoalRules.CashbackSegment(G, false, true, 688_200));
        Assert.Equal(68_820m, IncentiveGoalRules.Cashback(688_200, 10));
        Assert.Equal((IncentiveTwizosSegment.New, 8m), IncentiveGoalRules.CashbackSegment(G, false, false, 392_200));
        Assert.Equal(31_376m, IncentiveGoalRules.Cashback(392_200, 8));
        Assert.Equal((IncentiveTwizosSegment.New, 8m), IncentiveGoalRules.CashbackSegment(G, false, true, 599_400));
        Assert.Equal((IncentiveTwizosSegment.Existing, 5m), IncentiveGoalRules.CashbackSegment(G, true, true, 688_200));
        Assert.Equal(28_860m, IncentiveGoalRules.Cashback(577_200, 5));
    }

    [Fact]
    public void Marks_follow_table_4_6_and_the_store_colour_combines_both_goals()
    {
        var mogul = IncentiveGoalRules.MogulGoal(G, Ym, 7.04m, 2, G.MogulBoxes![0]); // 3 коробки
        Assert.Equal(IncentiveGoalStatus.Taken, IncentiveGoalRules.MogulStatus(mogul, [(true, 3)], true));
        Assert.Equal(IncentiveGoalStatus.Partial, IncentiveGoalRules.MogulStatus(mogul, [(true, 2)], true));
        Assert.Equal(IncentiveGoalStatus.Refused, IncentiveGoalRules.MogulStatus(mogul, [(false, 1)], true)); // 1 коробка — YM-01 нет
        Assert.Equal(IncentiveGoalStatus.Ahead, IncentiveGoalRules.MogulStatus(mogul, [], false));

        var twizos = IncentiveGoalRules.TwizosGoal(G, Tw, false, 0, 150_000, false)!; // 310 800
        Assert.Equal(IncentiveGoalStatus.Taken, IncentiveGoalRules.TwizosStatus(twizos, [310_800], true));
        Assert.Equal(IncentiveGoalStatus.Partial, IncentiveGoalRules.TwizosStatus(twizos, [103_600], true));
        Assert.Equal(IncentiveGoalStatus.Refused, IncentiveGoalRules.TwizosStatus(twizos, [], true));
        Assert.Equal(IncentiveGoalStatus.Ahead, IncentiveGoalRules.TwizosStatus(twizos, [], false));

        Assert.Equal(IncentiveGoalStatus.Taken, IncentiveGoalRules.StoreStatus([IncentiveGoalStatus.Taken, IncentiveGoalStatus.Taken], true, true));
        Assert.Equal(IncentiveGoalStatus.Partial, IncentiveGoalRules.StoreStatus([IncentiveGoalStatus.Taken, IncentiveGoalStatus.Refused], true, true));
        Assert.Equal(IncentiveGoalStatus.Refused, IncentiveGoalRules.StoreStatus([IncentiveGoalStatus.Refused, IncentiveGoalStatus.Refused], true, true));
        Assert.Equal(IncentiveGoalStatus.Ahead, IncentiveGoalRules.StoreStatus([IncentiveGoalStatus.Ahead], false, false));
        Assert.Equal(IncentiveGoalStatus.Taken, IncentiveGoalRules.StoreStatus([], true, true)); // без акции — «✓ заказ»
    }

    [Fact]
    public void Mogul_packing_is_the_one_bought_more_by_kg_and_excluded_regions_are_recognised()
    {
        Assert.Equal("1 кг", IncentiveGoalRules.MogulBox(G, new Dictionary<decimal, decimal> { [0.42m] = 4.2m, [1m] = 10.5m })!.Name);
        Assert.Equal("420 г", IncentiveGoalRules.MogulBox(G, new Dictionary<decimal, decimal> { [0.42m] = 10.92m, [1m] = 10m })!.Name);
        Assert.Equal("420 г", IncentiveGoalRules.MogulBox(G, new Dictionary<decimal, decimal>())!.Name);
        Assert.True(G.MogulExcluded("Самарканд"));
        Assert.True(G.MogulExcluded(" aaa "));
        Assert.False(G.MogulExcluded("Андижон"));
        Assert.Equal("Олмалик", G.StockRegion("AAA"));
        Assert.Null(G.StockRegion("Андижон"));
    }
}
