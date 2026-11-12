using OneBase.Domain.Incentive;

namespace OneBase.Application.Telegram.Incentive;

/// <summary>
/// Правила акций ноября (ТЗ «Карточки ТК и расчёт целей по акциям ноября», 06.11.2026), чистые функции: подарок TW-02 по сумме накладной, наборы
/// целей по точкам маршрута (шаги 1–4 раздела 4.2), порог YM-01 и кэшбек YM-02, визиты к спящим и цель «разбудить» (4.3), лучший агент дня TW-01.
/// </summary>
public static class IncentivePromoRules
{
    /// <summary>Подарок за заказ (TW-02): самый крупный, до порога которого дотягивает сумма Twizos накладной по прайсу; меньше кружки — null.</summary>
    public static IncentiveGiftTier? Gift(IncentiveTwizosPromo p, decimal twizosSum) => p.GiftTiers.Where(g => twizosSum >= g.MinSum).MaxBy(g => g.MinSum);

    /// <summary>Шаг 1: ТТ брала Twizos — пара подарков по среднему чеку (&lt; 600 тыс. — термос + кружка, до 1,2 млн — чайник + кружка, выше — чайник + термос).</summary>
    public static IReadOnlyList<string>? Pair(IncentiveTwizosPromo p, decimal check) =>
        p.PairRules.FirstOrDefault(r => r.MaxCheck is not { } max || check < max)?.Sets;

    /// <summary>Шаг 2: ТТ не брала Twizos — старт «кружка»: чек меньше 250 тыс. — 35 г × 3 кор., иначе 140 г × 1 кор.; меньше 60 тыс. — без цели.</summary>
    public static IReadOnlyList<string>? Start(IncentiveTwizosPromo p, decimal check) =>
        check < p.MinCheck ? null : check < p.SmallCheck ? [p.SmallStartSet] : [p.StartSet];

    /// <summary>Точка для цели: средний чек и брала ли Twizos.</summary>
    public sealed record Candidate(long MarketId, decimal Check, bool Bought);

    /// <summary>Цель точки: наборы по кодам (пара — два заказа), подключить или дозаказ, «+ план».</summary>
    public sealed record Goal(long MarketId, IReadOnlyList<string> Sets, string Sub, bool PlanAdded);

    /// <summary>
    /// Шаги 1–3 на точках маршрута (в порядке маршрута): пара — брали, старт — не брали; если кг целей меньше нормы дня × запас (1,3) — новым
    /// клиентам с чеком от 300 тыс. по убыванию чека сразу пара («+ план»), пока не хватит. norm = null — без шага 3 (нормы нет: призы взяты).
    /// </summary>
    public static List<Goal> RouteGoals(IncentiveTwizosPromo p, Func<string, decimal> kgOf, IReadOnlyList<Candidate> stores, decimal? norm)
    {
        var goals = new Dictionary<long, Goal>();
        foreach (var c in stores)
        {
            var sets = c.Bought ? Pair(p, c.Check) : Start(p, c.Check);
            if (sets is { Count: > 0 })
            {
                goals[c.MarketId] = new Goal(c.MarketId, sets, c.Bought ? IncentiveTargetSub.Reorder : IncentiveTargetSub.Connect, false);
            }
        }

        if (norm is { } n)
        {
            var target = n * p.Reserve;
            decimal Total() => goals.Values.Sum(g => g.Sets.Sum(kgOf));
            foreach (var c in stores.Where(c => !c.Bought && c.Check >= p.PairNewMinCheck).OrderByDescending(c => c.Check).ThenBy(c => c.MarketId))
            {
                if (Total() >= target)
                {
                    break;
                }

                if (Pair(p, c.Check) is { Count: > 0 } pair)
                {
                    goals[c.MarketId] = new Goal(c.MarketId, pair, IncentiveTargetSub.Connect, true);
                }
            }
        }

        return stores.Where(c => goals.ContainsKey(c.MarketId)).Select(c => goals[c.MarketId]).ToList();
    }

    /// <summary>Сколько кг не хватает целям маршрута до нормы дня с запасом (0 — хватает; нормы нет — 0).</summary>
    public static decimal Gap(IncentiveTwizosPromo p, decimal goalsKg, decimal? norm) => norm is { } n ? Math.Max(0, n * p.Reserve - goalsKg) : 0;

    /// <summary>
    /// Шаг 4 «Добери до нормы — клиенты вне маршрута»: точки ТП с других дней с самым большим чеком (от 60 тыс.), пара подарков (крупный чек —
    /// чайник + кружка, иначе термос + кружка — как на образце), пока не закроется недостача, не больше ExtraMax.
    /// </summary>
    public static List<Goal> ExtraGoals(IncentiveTwizosPromo p, Func<string, decimal> kgOf, IReadOnlyList<Candidate> others, decimal gap)
    {
        var list = new List<Goal>();
        foreach (var c in others.Where(c => c.Check >= p.MinCheck).OrderByDescending(c => c.Check).ThenBy(c => c.MarketId))
        {
            if (gap <= 0 || list.Count >= p.ExtraMax)
            {
                break;
            }

            var sets = ExtraPair(p, c.Check);
            if (sets.Count == 0)
            {
                continue;
            }

            list.Add(new Goal(c.MarketId, sets, c.Bought ? IncentiveTargetSub.Reorder : IncentiveTargetSub.Connect, false));
            gap -= sets.Sum(kgOf);
        }

        return list;
    }

    /// <summary>Пара для добора: правила ExtraPairs (по умолчанию — правила шага 1).</summary>
    public static IReadOnlyList<string> ExtraPair(IncentiveTwizosPromo p, decimal check) =>
        p.ExtraPairRules.FirstOrDefault(r => r.MaxCheck is not { } max || check < max)?.Sets ?? [];

    /// <summary>Кг целей точки: сумма весов наборов (пара — два заказа).</summary>
    public static decimal Kg(IEnumerable<string> sets, Func<string, decimal> kgOf) => sets.Sum(kgOf);

    // ---------- Yamelly: YM-01 / YM-02 ----------

    /// <summary>Фасовка строки по весу пачки (кг строки ÷ количество): 0,42 → «420 г», 1 → «1 кг»; другая — null (порог не считает).</summary>
    public static IncentivePacking? Packing(IncentiveYamellyPromo p, decimal unitKg) =>
        (p.Packings ?? []).FirstOrDefault(x => Math.Abs(x.UnitKg - unitKg) <= 0.005m);

    /// <summary>Целых коробок: пачек ÷ в коробке, вниз.</summary>
    public static int Boxes(decimal units, int perBox) => perBox <= 0 || units <= 0 ? 0 : (int)Math.Floor(units / perBox);

    /// <summary>Порог YM-01: в накладной не меньше MinBoxes целых коробок одной фасовки (разные вкусы одной фасовки складываются).</summary>
    public static bool Qualifies(IncentiveYamellyPromo p, IEnumerable<(decimal UnitKg, decimal Units)> lines) =>
        lines.Select(l => (Packing: Packing(p, l.UnitKg), l.Units))
            .Where(x => x.Packing is not null)
            .GroupBy(x => x.Packing!)
            .Any(g => Boxes(g.Sum(x => x.Units), g.Key.PerBox) >= p.MinBoxes);

    /// <summary>Сумма Yamelly после скидки YM-01 (по прайсу): 283 200 → 263 376.</summary>
    public static decimal AfterDiscount(IncentiveYamellyPromo p, decimal listSum) =>
        Math.Round(listSum * (100 - p.DiscountPercent) / 100, 0, MidpointRounding.AwayFromZero);

    /// <summary>Кэшбек YM-02: % от суммы Yamelly после скидки, до сума (283 200 → 263 376 → 26 338; 320 000 → 297 600 → 29 760).</summary>
    public static decimal Cashback(IncentiveYamellyPromo p, decimal listSum) =>
        Math.Round(listSum * (100 - p.DiscountPercent) / 100 * p.CashbackPercent / 100, 0, MidpointRounding.AwayFromZero);

    /// <summary>Визитов к спящим в день: вверх (осталось спящих ÷ осталось рабочих дней акции); дней нет — все оставшиеся.</summary>
    public static int VisitsPerDay(int remaining, int daysLeft) =>
        remaining <= 0 ? 0 : daysLeft <= 0 ? remaining : (int)Math.Ceiling((decimal)remaining / daysLeft);

    /// <summary>Цель «разбудить» за акцию: доля списка, вверх (46 × 30% = 13,8 → 14).</summary>
    public static int WakeTarget(int listed, decimal share) => listed <= 0 ? 0 : (int)Math.Ceiling(listed * share);

    /// <summary>
    /// Приоритет спящей точки (ТЗ 4.3): 1 — в маршруте ТП и берёт другие категории; 2 — последний Yamelly за три месяца до месяца акции
    /// (авг–сен); 3 — раньше.
    /// </summary>
    public static int SleepingPriority(bool inRoute, bool buysOther, DateOnly? lastYamelly, DateOnly promoStart) =>
        inRoute && buysOther ? 1
        : lastYamelly is { } d && d >= new DateOnly(promoStart.Year, promoStart.Month, 1).AddMonths(-3) ? 2
        : 3;

    // ---------- TW-01 ----------

    /// <summary>Строка рейтинга дня TW-01: место в своей группе (null — меньше порога) и победа.</summary>
    public sealed record BestRow(long AgentId, bool Wholesale, decimal Kg, int? Place, bool Winner);

    /// <summary>
    /// Лучший агент дня (TW-01): в каждой группе — по кг Twizos за день (при равенстве — по ID), место есть у тех, кто взял порог группы (розница
    /// 6 кг, рынок и опт 30 кг); победитель — первое место.
    /// </summary>
    public static IReadOnlyList<BestRow> BestOfDay(IncentiveBestPromo p, IEnumerable<(long Agent, bool Wholesale, decimal Kg)> rows)
    {
        var result = new List<BestRow>();
        foreach (var group in rows.GroupBy(r => r.Wholesale))
        {
            var place = 0;
            foreach (var r in group.OrderByDescending(r => r.Kg).ThenBy(r => r.Agent))
            {
                int? at = r.Kg > 0 && r.Kg >= p.MinKg(group.Key) ? ++place : null;
                result.Add(new BestRow(r.Agent, group.Key, r.Kg, at, at == 1));
            }
        }

        return result;
    }
}
