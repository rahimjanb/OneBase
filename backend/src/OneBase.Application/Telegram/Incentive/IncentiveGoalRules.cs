using System.Globalization;
using OneBase.Domain.Incentive;

namespace OneBase.Application.Telegram.Incentive;

/// <summary>
/// Правила «Целей на маршрут» v2 (ТЗ Улугбека v2 от 24.11.2026, Excel-образец andijon 4), чистые функции: цель Могул спящей точке, сегмент и набор
/// Twizos, лестница наборов, квота стоек, сегмент кешбека Twizos на момент заказа, отметки ✓ / ½ / ✗ и цвет точки.
/// </summary>
public static class IncentiveGoalRules
{
    // ---------- Наборы ----------

    /// <summary>Набор «140x8+35x2» → коробки по порядку записи; пустые и нулевые части пропускаются.</summary>
    public static IReadOnlyList<IncentiveBoxCount> ParseSet(string? set)
    {
        var list = new List<IncentiveBoxCount>();
        foreach (var part in (set ?? "").Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var bits = part.Split(['x', 'х', '×', '*'], 2, StringSplitOptions.TrimEntries);
            if (bits.Length == 2 && bits[0].Length > 0 && int.TryParse(bits[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0)
            {
                list.Add(new IncentiveBoxCount(bits[0], n));
            }
        }

        return list;
    }

    /// <summary>Коробки → строка набора («140x8+35x2»).</summary>
    public static string FormatSet(IEnumerable<IncentiveBoxCount> boxes) => string.Join("+", boxes.Where(b => b.Count > 0).Select(b => $"{b.Box}x{b.Count}"));

    /// <summary>Сумма набора по прайсу коробок; коробка без цены в настройках — null (набор не годится).</summary>
    public static decimal? Sum(IncentiveGoalsPromo p, string set)
    {
        decimal total = 0;
        foreach (var b in ParseSet(set))
        {
            if (p.Box(b.Box) is not { } box)
            {
                return null;
            }

            total += box.Price * b.Count;
        }

        return total;
    }

    /// <summary>Кг набора.</summary>
    public static decimal Kg(IncentiveGoalsPromo p, string set) => ParseSet(set).Sum(b => (p.Box(b.Box)?.Kg ?? 0) * b.Count);

    /// <summary>
    /// Набор действующему клиенту по цели (ТЗ v2, 4.3, лестница). first — первая ступень Ladder не меньше цели, дальше — 140 г × N + LadderTail (N —
    /// вверх, чтобы хватило), как в коде Excel-образца; minimal — самый дешёвый набор не меньше цели среди ступеней, 140 г × 1 + 35 г × 0–3 и
/// 140 г × N + 35 г × 1–3.
    /// </summary>
    public static string Ladder(IncentiveGoalsPromo p, decimal target)
    {
        var steps = (p.Ladder ?? []).Select(x => (Set: x, Sum: Sum(p, x))).Where(x => x.Sum is not null).Select(x => (x.Set, Sum: x.Sum!.Value)).ToList();
        var big = p.Box("140");
        if (p.LadderMode == IncentiveLadderMode.Minimal && big is not null)
        {
            var small = p.Box("35");
            var candidates = new List<(string Set, decimal Sum, int Boxes)>(steps.Select(x => (x.Set, x.Sum, ParseSet(x.Set).Sum(b => b.Count))));
            var maxN = (int)Math.Ceiling(target / big.Price) + 1;
            for (var n = 1; n <= Math.Max(1, maxN); n++)
            {
                // ТЗ 4.3: 140 г × 1 — отдельная ступень, дальше — 140 г × N + 35 г × 1–3.
                for (var k = n == 1 || small is null ? 0 : 1; k <= (small is null ? 0 : 3); k++)
                {
                    var set = k == 0 ? $"140x{n}" : $"140x{n}+35x{k}";
                    candidates.Add((set, n * big.Price + k * (small?.Price ?? 0), n + k));
                }
            }

            return candidates.Where(c => c.Sum >= target).OrderBy(c => c.Sum).ThenBy(c => c.Boxes).Select(c => c.Set).FirstOrDefault() ?? $"140x{Math.Max(1, maxN)}";
        }

        foreach (var (set, sum) in steps)
        {
            if (sum >= target)
            {
                return set;
            }
        }

        // Продолжение лестницы: 140 г × N + хвост (35 г × 2), N — вверх, не меньше 1.
        var tail = Sum(p, p.LadderTail) ?? 0;
        var price = big?.Price ?? 0;
        var count = price <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling((target - tail) / price));
        return string.IsNullOrWhiteSpace(p.LadderTail) ? $"140x{count}" : $"140x{count}+{p.LadderTail}";
    }

    // ---------- Могул ----------

    /// <summary>
    /// Фасовка точки (ТЗ v2, 4.2): та, которую точка брала больше по кг (при равенстве — первая в настройках, 420 г); покупок нет — первая.
    /// kgByUnit — кг Могула по весу пачки (0,42 / 1).
    /// </summary>
    public static IncentiveMogulBox? MogulBox(IncentiveGoalsPromo p, IReadOnlyDictionary<decimal, decimal> kgByUnit)
    {
        var boxes = p.MogulBoxes ?? [];
        if (boxes.Count == 0)
        {
            return null;
        }

        decimal KgOf(IncentiveMogulBox b) => kgByUnit.Where(kv => Math.Abs(kv.Key - b.UnitKg) <= 0.005m).Sum(kv => kv.Value);
        var best = boxes[0];
        foreach (var b in boxes.Skip(1))
        {
            if (KgOf(b) > KgOf(best))
            {
                best = b;
            }
        }

        return best;
    }

    /// <summary>
    /// Цель Могул спящей точке (ТЗ v2, 4.2): цель кг = средний закуп × (1 + 20%); коробок = вверх(цель ÷ кг коробки), не меньше порога YM-01 (2);
    /// сумма = коробок × цена коробки; со скидкой 7%; кешбек ТП 10% от суммы со скидкой. Пример: 7,04 кг → 8,45 кг → 3 кор. 420 г → 424 800 → 395 064 → 39 506.
    /// </summary>
    public static IncentiveMogulGoal MogulGoal(IncentiveGoalsPromo p, IncentiveYamellyPromo ym, decimal avgKg, int months, IncentiveMogulBox box)
    {
        var target = avgKg * (1 + p.MogulGrowth);
        var boxes = box.Kg <= 0 ? ym.MinBoxes : Math.Max(ym.MinBoxes, (int)Math.Ceiling(target / box.Kg - 0.000_000_1m));
        var sum = boxes * box.Price;
        var after = IncentivePromoRules.AfterDiscount(ym, sum);
        var cashback = Math.Round(after * ym.CashbackPercent / 100, 0, MidpointRounding.AwayFromZero);
        return new IncentiveMogulGoal(Math.Round(avgKg, 3), months, Math.Round(target, 3), box.Name, box.Kg, boxes, boxes * box.Kg, sum, after, cashback, box.NameUz);
    }

    // ---------- Twizos ----------

    /// <summary>Кешбек от суммы: % × сумма, до сума.</summary>
    public static decimal Cashback(decimal sum, decimal percent) => Math.Round(sum * percent / 100, 0, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Цель действующему клиенту: max(средняя накладная × (1 + 30%); порог следующего подарка TW-02 выше средней накладной). Пример: 162 800 →
    /// max(211 640; 288 600) = 288 600; 1 872 200 → 2 433 860.
    /// </summary>
    public static decimal ExistingTarget(IncentiveGoalsPromo p, IncentiveTwizosPromo tw, decimal avgInvoice)
    {
        var next = tw.GiftTiers.Select(g => g.MinSum).Where(m => m > avgInvoice).Order().Cast<decimal?>().FirstOrDefault() ?? 0;
        return Math.Max(avgInvoice * (1 + p.TwizosGrowth), next);
    }

    /// <summary>Новый клиент с чеком от порога стойки — кандидат на стойку (квоту раздаёт <see cref="StandOffers"/>).</summary>
    public static bool StandCandidate(IncentiveGoalsPromo p, bool existing, decimal avgCheck) => !existing && avgCheck >= p.StandCheck;

    /// <summary>
    /// Кому предложить стойку (ТЗ v2, 4.3.1): новые клиенты с чеком от 600 тыс. — по убыванию чека (при равенстве — по ID), пока не кончится квота.
    /// </summary>
    public static HashSet<long> StandOffers(IEnumerable<(long MarketId, decimal AvgCheck)> candidates, int quota) =>
        candidates.OrderByDescending(c => c.AvgCheck).ThenBy(c => c.MarketId).Take(Math.Max(0, quota)).Select(c => c.MarketId).ToHashSet();

    /// <summary>
    /// Цель Twizos (ТЗ v2, 4.3). Действующий — по лестнице от цели, 5%. Новый: чек меньше 60 тыс. — без цели; до 250 тыс. — 3 кор. 35 г (кружка);
    /// до 600 тыс. — 140 г + 35 г (термос); от 600 тыс. в квоте стоек — стойка + 35 + 140 + 200 г (чайник, 10%), вне квоты — 140 г + 35 г × 3
    /// (чайник); новым — 8%. Подарок — по сумме набора (TW-02).
    /// </summary>
    public static IncentiveTwizosGoal? TwizosGoal(IncentiveGoalsPromo p, IncentiveTwizosPromo tw, bool existing, decimal avgInvoice, decimal avgCheck, bool standOffer)
    {
        string set;
        string segment;
        decimal percent;
        if (existing)
        {
            set = Ladder(p, ExistingTarget(p, tw, avgInvoice));
            (segment, percent) = (IncentiveTwizosSegment.Existing, p.ExistingPercent);
        }
        else if (avgCheck < p.MinCheck)
        {
            return null;
        }
        else if (avgCheck >= p.StandCheck)
        {
            (set, segment, percent) = standOffer
                ? (p.StandSet, IncentiveTwizosSegment.Stand, p.StandPercent)
                : (p.BigSet, IncentiveTwizosSegment.New, p.NewPercent);
        }
        else
        {
            set = avgCheck >= p.ThermosCheck ? p.MediumSet : p.SmallSet;
            (segment, percent) = (IncentiveTwizosSegment.New, p.NewPercent);
        }

        if (Sum(p, set) is not { } sum)
        {
            return null;
        }

        return new IncentiveTwizosGoal(segment, percent, existing ? Math.Round(avgInvoice, 0) : null, Math.Round(avgCheck, 0), set, sum, Kg(p, set),
            IncentivePromoRules.Gift(tw, sum)?.Gift, Cashback(sum, percent), segment == IncentiveTwizosSegment.Stand);
    }

    /// <summary>
    /// Сегмент кешбека Twizos на момент заказа (ТЗ v2, 5): точка уже брала Twizos — действующий 5%; не брала — новый 8%, а со стойкой в заказе и
    /// суммой Twizos от 600 000 — 10%.
    /// </summary>
    public static (string Segment, decimal Percent) CashbackSegment(IncentiveGoalsPromo p, bool boughtBefore, bool stand, decimal twizosSum) =>
        boughtBefore ? (IncentiveTwizosSegment.Existing, p.ExistingPercent)
        : stand && twizosSum >= p.StandCheck ? (IncentiveTwizosSegment.Stand, p.StandPercent)
        : (IncentiveTwizosSegment.New, p.NewPercent);

    // ---------- Стойки ----------

    /// <summary>Цель региона по стойкам: остаток на складе; 0 или склада нет — StandZeroTarget (40).</summary>
    public static int StandTarget(IncentiveGoalsPromo p, int? stock) => stock is > 0 ? stock.Value : p.StandZeroTarget;

    /// <summary>Квота ТП: вверх(цель региона ÷ ТП региона); 14 ÷ 6 → 3.</summary>
    public static int StandQuota(int target, int agents) => target <= 0 ? 0 : agents <= 0 ? target : (int)Math.Ceiling((decimal)target / agents);

    // ---------- Итоги ----------

    /// <summary>
    /// Отметка Могул (ТЗ v2, 4.6): накладная по YM-01 с коробками не меньше цели — ✓; YM-01 выполнен (от 2 коробок), но меньше цели — ½; визит или
    /// заказ без YM-01 — ✗; иначе впереди. orders — заказы дня точки: выполнен ли порог (и не отменён) и целых коробок.
    /// </summary>
    public static string MogulStatus(IncentiveMogulGoal goal, IEnumerable<(bool Qualifies, int Boxes)> orders, bool visited)
    {
        var done = orders.Where(o => o.Qualifies).ToList();
        return done.Count == 0 ? visited ? IncentiveGoalStatus.Refused : IncentiveGoalStatus.Ahead
            : done.Max(o => o.Boxes) >= goal.Boxes ? IncentiveGoalStatus.Taken
            : IncentiveGoalStatus.Partial;
    }

    /// <summary>
    /// Отметка Twizos (ТЗ v2, 4.6): сумма Twizos в накладной не меньше рекомендованной — ✓; заказ Twizos есть, но меньше — ½; визит или заказ без
    /// Twizos — ✗; иначе впереди. sums — суммы Twizos по прайсу в накладных дня точки.
    /// </summary>
    public static string TwizosStatus(IncentiveTwizosGoal goal, IReadOnlyCollection<decimal> sums, bool visited) =>
        sums.Count == 0 || sums.All(s => s <= 0) ? visited ? IncentiveGoalStatus.Refused : IncentiveGoalStatus.Ahead
        : sums.Max() >= goal.Sum ? IncentiveGoalStatus.Taken
        : IncentiveGoalStatus.Partial;

    /// <summary>
    /// Цвет навеса точки: все цели взяты — зелёный (taken); часть — оранжевый (partial); ничего, но визит или заказ был — красный (refused);
    /// иначе впереди. Без целей — по заказу: был — taken, визит без заказа — refused.
    /// </summary>
    public static string StoreStatus(IReadOnlyCollection<string> offers, bool hasOrder, bool visited) =>
        offers.Count == 0 ? hasOrder ? IncentiveGoalStatus.Taken : visited ? IncentiveGoalStatus.Refused : IncentiveGoalStatus.Ahead
        : offers.All(x => x == IncentiveGoalStatus.Taken) ? IncentiveGoalStatus.Taken
        : offers.Any(x => x is IncentiveGoalStatus.Taken or IncentiveGoalStatus.Partial) ? IncentiveGoalStatus.Partial
        : offers.Any(x => x == IncentiveGoalStatus.Refused) || visited ? IncentiveGoalStatus.Refused
        : IncentiveGoalStatus.Ahead;
}
