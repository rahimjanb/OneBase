using OneBase.Application.Telegram.Incentive;
using OneBase.Domain.Incentive;

namespace OneBase.AI.Tests;

/// <summary>
/// Акции ноября без базы (ТЗ «Карточки ТК и расчёт целей по акциям ноября», 06.11.2026; файл «Расчёт целей ТП — утверждённые акции ноябрь.xlsx»):
/// подарок TW-02 по сумме накладной, наборы целей по шагам 1–4, порог YM-01 и кэшбек YM-02 на числах ТЗ, визиты к спящим и цель «разбудить» как
/// в Excel, TW-01 по группам, норма до приза для расчёта и пороги опта (TW-03).
/// </summary>
public class IncentivePromoTests
{
    private static readonly IncentivePromos November = IncentivePromos.November2026;
    private static readonly IncentiveTwizosPromo Tw = November.Twizos!;
    private static readonly IncentiveYamellyPromo Ym = November.Yamelly!;
    private static readonly IReadOnlyDictionary<DateOnly, string> Holidays = new Dictionary<DateOnly, string> { [new(2026, 12, 8)] = "День Конституции" };

    private static decimal KgOf(string code) => IncentivePromos.NovemberSets.First(x => x.Code == code).Kg;

    [Theory]
    [InlineData(288_599, null)]
    [InlineData(288_600, "mug")]
    [InlineData(392_199, "mug")]
    [InlineData(392_200, "thermos")]
    [InlineData(584_599, "thermos")]
    [InlineData(584_600, "kettle")]
    [InlineData(2_000_000, "kettle")]
    public void Gift_follows_the_invoice_sum_thresholds_of_tw02(int sum, string? gift) =>
        Assert.Equal(gift, IncentivePromoRules.Gift(Tw, sum)?.Gift);

    [Fact]
    public void Sets_of_the_spec_cost_and_weigh_as_in_the_spec()
    {
        // ТЗ 4.2: кружка (новый) 35 г × 3 — 310 800 / 2,94; кружка 140 г × 1 — 288 600 / 3,64; термос — 392 200 / 4,62; чайник — 599 400 / 6,58.
        Assert.Equal([("K35", 310_800m, 2.94m), ("K140", 288_600m, 3.64m), ("T", 392_200m, 4.62m), ("C", 599_400m, 6.58m)],
            IncentivePromos.NovemberSets.Select(x => (x.Code!, x.Sum, x.Kg)));
        // Подарок по сумме набора — тот, что в названии набора.
        Assert.All(IncentivePromos.NovemberSets, x => Assert.Equal(x.Gift, IncentivePromoRules.Gift(Tw, x.Sum)!.Gift));
        // Пары: кружка + термос 680 800, чайник + кружка 888 000, чайник + термос 991 600.
        decimal Sum(params string[] codes) => codes.Sum(c => IncentivePromos.NovemberSets.First(x => x.Code == c).Sum);
        Assert.Equal((680_800m, 888_000m, 991_600m), (Sum("T", "K140"), Sum("C", "K140"), Sum("C", "T")));
    }

    [Theory]
    [InlineData(500_000, true, "T+K140")]
    [InlineData(599_999, true, "T+K140")]
    [InlineData(600_000, true, "C+K140")]
    [InlineData(1_199_999, true, "C+K140")]
    [InlineData(1_200_000, true, "C+T")]
    [InlineData(59_999, false, null)]
    [InlineData(60_000, false, "K35")]
    [InlineData(249_999, false, "K35")]
    [InlineData(250_000, false, "K140")]
    public void Pair_for_buyers_and_start_mug_for_new_clients(int check, bool bought, string? sets)
    {
        var result = bought ? IncentivePromoRules.Pair(Tw, check) : IncentivePromoRules.Start(Tw, check);
        Assert.Equal(sets, result is null ? null : string.Join("+", result));
    }

    [Fact]
    public void Plan_adds_pairs_to_big_new_clients_until_norm_with_reserve_and_extra_covers_the_rest()
    {
        // Маршрут: брала Twizos (чек 700 тыс.) — пара; три новых (чек 1,5 млн, 400 тыс., 100 тыс.) — старт; норма 30 кг × 1,3 = 39 кг.
        IncentivePromoRules.Candidate[] route = [new(1, 700_000, true), new(2, 1_500_000, false), new(3, 400_000, false), new(4, 100_000, false), new(5, 50_000, false)];
        var goals = IncentivePromoRules.RouteGoals(Tw, KgOf, route, 30);
        // 1: чайник + кружка (10,22); 2 и 3 — «+ план» по убыванию чека: 1,5 млн — чайник + термос (11,2), 400 тыс. — термос + кружка (8,26);
        // 4 — старт 35 г × 3 (2,94); 5 — без цели (чек меньше 60 тыс.).
        Assert.Equal(["C+K140", "C+T", "T+K140", "K35"], goals.Select(g => string.Join("+", g.Sets)));
        Assert.Equal([false, true, true, false], goals.Select(g => g.PlanAdded));
        Assert.Equal([IncentiveTargetSub.Reorder, IncentiveTargetSub.Connect, IncentiveTargetSub.Connect, IncentiveTargetSub.Connect], goals.Select(g => g.Sub));
        var kg = goals.Sum(g => IncentivePromoRules.Kg(g.Sets, KgOf));
        Assert.Equal(32.62m, kg);
        Assert.Equal(6.38m, IncentivePromoRules.Gap(Tw, kg, 30));

        // Добор: ТТ с других дней с самым большим чеком — 5 млн, «чайник + кружка», 10,2 кг (пример ТЗ: Oyatlloh Otabekakaogli).
        IncentivePromoRules.Candidate[] others = [new(10, 300_000, true), new(11, 5_000_000, false), new(12, 40_000, false)];
        var extra = IncentivePromoRules.ExtraGoals(Tw, KgOf, others, 6.38m);
        Assert.Single(extra);
        Assert.Equal((11L, "C+K140", 10.22m), (extra[0].MarketId, string.Join("+", extra[0].Sets), IncentivePromoRules.Kg(extra[0].Sets, KgOf)));

        // Без нормы (все призы взяты) — шаг 3 не делается, добора нет.
        Assert.Equal(["C+K140", "K140", "K140", "K35"], IncentivePromoRules.RouteGoals(Tw, KgOf, route, null).Select(g => string.Join("+", g.Sets)));
        Assert.Equal(0, IncentivePromoRules.Gap(Tw, 0, null));
    }

    [Fact]
    public void Ym01_threshold_is_two_whole_boxes_of_one_packing()
    {
        Assert.True(IncentivePromoRules.Qualifies(Ym, [(0.42m, 8), (0.42m, 8)])); // 2 коробки 420 г разными вкусами
        Assert.False(IncentivePromoRules.Qualifies(Ym, [(0.42m, 8), (0.42m, 7)])); // 15 пачек — одна целая коробка
        Assert.True(IncentivePromoRules.Qualifies(Ym, [(1m, 8)])); // 2 коробки по 1 кг (4 пачки)
        Assert.False(IncentivePromoRules.Qualifies(Ym, [(0.42m, 8), (1m, 4)])); // по одной коробке разных фасовок
        Assert.False(IncentivePromoRules.Qualifies(Ym, [(0.23m, 40)])); // фасовки нет в настройках
        Assert.Equal(2, IncentivePromoRules.Boxes(16, 8));
        Assert.Equal(1, IncentivePromoRules.Boxes(15, 8));
    }

    [Fact]
    public void Cashback_is_ten_percent_after_seven_percent_discount_as_in_the_spec()
    {
        // ТЗ, раздел 7: 283 200 → 263 376 → 26 338; 320 000 → 297 600 → 29 760.
        Assert.Equal(263_376m, IncentivePromoRules.AfterDiscount(Ym, 283_200));
        Assert.Equal(26_338m, IncentivePromoRules.Cashback(Ym, 283_200));
        Assert.Equal(297_600m, IncentivePromoRules.AfterDiscount(Ym, 320_000));
        Assert.Equal(29_760m, IncentivePromoRules.Cashback(Ym, 320_000));
        Assert.Equal(26_338m, Ym.MinCashback);
    }

    [Theory]
    // Лист «Цели ТП»: спящих в списке → визитов в день (21 рабочий день 06–30.11) и цель «разбудить» (30%, вверх).
    [InlineData(46, 3, 14)] // andijon 4 Мухаммаджон
    [InlineData(51, 3, 16)] // andijon 1 Олимжон
    [InlineData(3, 1, 1)] // 599 andijon 6
    [InlineData(50, 3, 15)] // andijon 2 Исмоилжон
    [InlineData(0, 0, 0)]
    public void Sleeping_visits_per_day_and_wake_target_match_the_excel(int listed, int visits, int wake)
    {
        Assert.Equal(21, IncentiveRules.WorkDays(new(2026, 11, 6), new(2026, 11, 30), Holidays));
        Assert.Equal(visits, IncentivePromoRules.VisitsPerDay(listed, 21));
        Assert.Equal(wake, IncentivePromoRules.WakeTarget(listed, Ym.WakeShare));
    }

    [Fact]
    public void Sleeping_priority_route_and_other_categories_then_recent_then_old()
    {
        var start = new DateOnly(2026, 11, 6);
        Assert.Equal(1, IncentivePromoRules.SleepingPriority(true, true, new DateOnly(2026, 3, 1), start));
        Assert.Equal(2, IncentivePromoRules.SleepingPriority(true, false, new DateOnly(2026, 8, 15), start)); // в маршруте, но другое не берёт
        Assert.Equal(2, IncentivePromoRules.SleepingPriority(false, true, new DateOnly(2026, 9, 30), start));
        Assert.Equal(3, IncentivePromoRules.SleepingPriority(false, true, new DateOnly(2026, 7, 31), start));
        Assert.Equal(3, IncentivePromoRules.SleepingPriority(false, false, null, start));
    }

    [Fact]
    public void Best_agent_of_the_day_per_group_with_thresholds()
    {
        var rows = IncentivePromoRules.BestOfDay(November.Best!,
            [(1, false, 7m), (2, false, 5.9m), (3, false, 12m), (4, true, 29m), (5, true, 31m), (6, false, 12m)]);
        var by = rows.ToDictionary(r => r.AgentId);
        // Розница: 12 кг у двоих — первым по ID; 5,9 кг меньше порога 6 — без места. Опт: 31 — победитель, 29 меньше порога 30.
        Assert.Equal((1, true), (by[3].Place!.Value, by[3].Winner));
        Assert.Equal((2, false), (by[6].Place!.Value, by[6].Winner));
        Assert.Equal(3, by[1].Place);
        Assert.Null(by[2].Place);
        Assert.Equal((1, true), (by[5].Place!.Value, by[5].Winner));
        Assert.Null(by[4].Place);
        Assert.Equal(2, rows.Count(r => r.Winner));
    }

    [Fact]
    public void Norm_goes_to_the_prize_for_the_calculation_and_each_prize_has_kg_per_day()
    {
        var s = IncentiveTests.Twizos() with
        {
            Tiers = IncentiveTests.Twizos().Tiers.Select(t => t with { WholesaleKg = t.Kg + 100 }).ToList(),
            Promos = November,
        };
        // andijon 4 Мухаммаджон на 06.11: накоплено 105,8 (октябрь, как в Excel). Рабочих дней 06.11–31.12 без 08.12 — 47 (в Excel 48: праздник
        // не вычтен), норма до телефона — 3 894,2 ÷ 47 = 82,9 → 83 кг (в Excel 81,1).
        var p = IncentiveRules.Progress(s, 105.8m, new DateOnly(2026, 11, 6), Holidays);
        Assert.Equal(47, p.WorkDaysLeft);
        Assert.Equal("phone", p.Target!.Image);
        Assert.Equal(83m, p.Norm);
        Assert.Equal([1394.2m, 2294.2m, 3894.2m], p.Tiers.Select(t => t.Left));
        Assert.Equal([29.7m, 48.8m, 82.9m], p.Tiers.Select(t => t.PerDay!.Value));
        Assert.Equal("tablet", p.Next!.Image); // ближайшая ступень — по-прежнему планшет
        // Без праздника — как в Excel: 3 894,2 ÷ 48 = 81,1.
        Assert.Equal(81.1m, IncentiveRules.Progress(s, 105.8m, new DateOnly(2026, 11, 6), new Dictionary<DateOnly, string>()).Tiers[2].PerDay);

        // Опт: свои пороги (+100 кг в тесте), норма — до своего телефона.
        var w = IncentiveRules.Progress(s, 0, new DateOnly(2026, 11, 6), Holidays, wholesale: true);
        Assert.Equal([1600m, 2500m, 4100m], w.Tiers.Select(t => t.Tier.Kg));
        Assert.Equal(Math.Ceiling(4100m / 47), w.Norm);

        // Телефон взят — норма до следующей ступени нет: призы кончились; без приза для расчёта — до ближайшей невзятой.
        Assert.Null(IncentiveRules.Progress(s, 4000, new DateOnly(2026, 11, 6), Holidays).Norm);
        var old = IncentiveRules.Progress(IncentiveTests.Twizos(), 105.8m, new DateOnly(2026, 11, 6), Holidays);
        Assert.Null(old.Target);
        Assert.Equal(Math.Ceiling(1394.2m / 47), old.Norm);
    }

    [Fact]
    public void November_promos_survive_json_round_trip()
    {
        var json = November.ToJson();
        Assert.Equal(json, IncentivePromos.Parse(json).ToJson());
        Assert.Same(IncentivePromos.None, IncentivePromos.Parse("  "));
        Assert.Null(IncentivePromos.Parse("{}").Twizos);
    }
}
