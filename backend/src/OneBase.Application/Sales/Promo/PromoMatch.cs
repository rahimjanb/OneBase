using System.Text.RegularExpressions;

namespace OneBase.Application.Sales.Promo;

/// <summary>Часть скидки строки: акции (пусто — ручная скидка) и доля скидки строки.</summary>
public sealed record PromoPart(IReadOnlyList<long> Promos, decimal Share);

/// <summary>Акция Linko для сопоставления: тип, сроки, товары (conditional — со скидкой или условием, bonus — бонусный товар) и процент.</summary>
public sealed record PromoInfo(
    long Id,
    string Name,
    string Type,
    string? PromotionType,
    string? DiscountType,
    decimal? Discount,
    DateOnly? Begin,
    DateOnly? Till,
    bool ApplyAll,
    IReadOnlyDictionary<long, decimal?> Conditional,
    IReadOnlySet<long> Bonus);

/// <summary>Строка заказа со скидкой (доставленный заказ): даты, регион, агент, точка, товар, количество, цены и скидка строки.</summary>
public sealed record PromoLine(
    long OrderId,
    DateOnly Created,
    DateOnly Accepted,
    long? BranchId,
    string Region,
    long? AgentId,
    long? MarketId,
    long ProductId,
    decimal Amount,
    decimal ReturnAmount,
    decimal TotalPrice,
    decimal Discount,
    decimal Percent,
    bool FullReturn)
{
    /// <summary>Бонусный товар: цена 0, скидка — его стоимость (Linko ставит 100%).</summary>
    public bool IsBonus => Percent >= 100 || (TotalPrice == 0 && Discount > 0);

    /// <summary>Доля невозвращённого: полный возврат — 0, частичный — (кол-во − возвращено) ÷ кол-во. Возврат — на день приёмки заказа.</summary>
    public decimal Kept => FullReturn || Amount <= 0 ? 0 : Math.Clamp((Amount - ReturnAmount) / Amount, 0, 1);
}

/// <summary>
/// Акция строки заказа. Linko External API её не отдаёт (в отчёте «Промо продукты заказов» она есть), поэтому она восстанавливается: акция,
/// действующая в день создания заказа, где этот товар — бонусный (у бонусной строки) или со скидкой того же процента (у скидочной), с
/// учётом региона в названии акции («… Фергана», «Сырдарье …»). Не нашлось — «Без акции (ручная скидка)», Id = -1.
/// </summary>
public static partial class PromoMatch
{
    public const long Manual = -1;

    /// <summary>Дни после конца акции, когда заказ ещё считается её (заказ последнего дня принимают на следующий; «вне срока» — от 2 дней).</summary>
    public const int Grace = 2;

    [GeneratedRegex(@"(\d+(?:[.,]\d+)?)\s*%")]
    private static partial Regex PercentInName();

    /// <summary>
    /// Акции, к которым может относиться строка (по возрастанию id): одна — акция строки; несколько — одновременные акции с теми же товаром,
    /// процентом и охватом (в Linko их различают списки точек, которых нет в API: «Могул 8% "A"» и «8% "S"»); пусто — ручная скидка.
    /// </summary>
    public static IReadOnlyList<long> Match(PromoLine line, IReadOnlyList<PromoInfo> promos, Func<string, string, bool> regionInName, bool strict = false)
    {
        // Действует в день создания заказа или в день приёмки (заказ оформлен до старта, а принят уже в акции — Linko даёт скидку акции).
        var candidates = Candidates(line, promos, line.Created).Concat(Candidates(line, promos, line.Accepted)).DistinctBy(p => p.Id).ToList();
        if (candidates.Count == 0)
        {
            // Заказ создан чуть позже конца акции, а скидка в нём та же — тоже её (Linko применил действующую при оформлении).
            candidates = Candidates(line, promos, line.Created.AddDays(-Grace)).ToList();
        }

        // 1) Акция с чужим регионом в названии уступает остальным («Buxoro 2 %» не для Сырдарьи, где есть «Сырдарье Скидка 2%»); если других
        //    нет — остаётся: «Бамбук Коканд 002_10%» действует и на заказы филиала «Фаргона».
        //    Акция «на все товары» с чужим регионом не подходит никогда: «скидки 3% Ташкент» — не ручные 3% других регионов.
        //    Так же акция для агентов (promotion_agents) с чужим регионом: «Buxoro 2 %» — агентам Бухары, не Шахрисабзу.
        candidates = candidates.Where(p => !((p.ApplyAll || p.PromotionType == "promotion_agents") && MentionsOtherRegion(p, line.Region, regionInName))).ToList();
        //    strict — части составной скидки: акция с чужим регионом не берётся совсем.
        var local = candidates.Where(p => !MentionsOtherRegion(p, line.Region, regionInName)).ToList();
        if (local.Count > 0 || strict)
        {
            candidates = local;
        }

        // 2) Акция, где товар назван явно, сильнее акции «на все товары» с тем же процентом («Ручная скидка Ташкент 8%» уступает «Могул 8%»).
        var named = candidates.Where(p => p.Conditional.ContainsKey(line.ProductId) || p.Bonus.Contains(line.ProductId)).ToList();
        if (named.Count > 0)
        {
            candidates = named;
        }

        // 3) Из оставшихся «своя по региону» сильнее общей («Могул 10% Фергана» для Фаргоны, а не «Могул 10% "E"»).
        var regional = candidates.Where(p => regionInName(p.Name, line.Region)).ToList();
        return (regional.Count > 0 ? regional : candidates).Select(p => p.Id).Order().ToList();
    }

    /// <summary>
    /// Части скидки строки: обычно одна (акция или ручная), но скидки складываются — у строки 7% = «Фруктовые Могули 5%» + «Сырдарье Скидка
    /// 2%», 12% = «Могул 10%» + ручные 2%. Тогда строка делится по процентам: акция товара получает свой процент, остаток — акция с таким
    /// процентом или ручная скидка (как в отчёте Linko «Промо продукты заказов»: строки акций всегда со своим процентом). Share — доля скидки.
    /// </summary>
    public static IReadOnlyList<PromoPart> Allocate(PromoLine line, IReadOnlyList<PromoInfo> promos, Func<string, string, bool> regionInName)
    {
        var exact = Match(line, promos, regionInName);
        if (exact.Count > 0 || line.IsBonus || line.Percent <= 0)
        {
            return [new PromoPart(exact, 1)];
        }

        // Проценты акций, где товар назван явно, которые действуют для строки и не названы другим регионом, — от большего к меньшему.
        var percents = promos
            .Where(p => p.Type == "discount" && p.DiscountType != "manual" && Active(p, line) && !MentionsOtherRegion(p, line.Region, regionInName))
            .Select(p => p.Conditional.TryGetValue(line.ProductId, out var amount) ? (amount is > 0 ? amount : PercentOf(p)) : null)
            .Where(x => x is { } v && v > 0 && v < line.Percent)
            .Select(x => x!.Value)
            .Distinct()
            .OrderByDescending(x => x)
            .ToList();
        foreach (var main in percents)
        {
            var mainIds = Match(line with { Percent = main }, promos, regionInName, strict: true);
            if (mainIds.Count == 0)
            {
                continue;
            }

            // Остаток — своя акция (обычно «на все товары» региона: «Сырдарье Скидка 2%») или ручная надбавка агента.
            var rest = line.Percent - main;
            var restIds = Match(line with { Percent = rest }, promos, regionInName, strict: true);
            return [new PromoPart(mainIds, main / line.Percent), new PromoPart(restIds, rest / line.Percent)];
        }

        return [new PromoPart([], 1)];
    }

    /// <summary>
    /// Части скидки всех строк сразу: если в заказе тот же процент стоит и на товаре, которого нет ни в одной акции, — это ручная скидка
    /// агента на заказ, и все строки заказа с этим процентом ручные (так их показывает отчёт Linko), даже если часть товаров совпала с акцией.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<PromoPart>> AllocateAll(IReadOnlyList<PromoLine> lines, IReadOnlyList<PromoInfo> promos, Func<string, string, bool> regionInName)
    {
        var parts = lines.Select(l => Allocate(l, promos, regionInName)).ToArray();
        foreach (var group in Enumerable.Range(0, lines.Count).Where(i => !lines[i].IsBonus).GroupBy(i => (lines[i].OrderId, lines[i].Percent)))
        {
            var uncovered = group.Any(i => parts[i].All(p => p.Promos.Count == 0));
            if (uncovered)
            {
                foreach (var i in group)
                {
                    parts[i] = [new PromoPart([], 1)];
                }
            }
        }

        return parts;
    }

    private static bool Active(PromoInfo p, PromoLine line) =>
        In(p, line.Created) || In(p, line.Accepted) || In(p, line.Created.AddDays(-Grace));

    private static bool In(PromoInfo p, DateOnly day) => (p.Begin is not { } b || day >= b) && (p.Till is not { } t || day <= t);

    private static bool MentionsOtherRegion(PromoInfo promo, string region, Func<string, string, bool> regionInName) =>
        regionInName(promo.Name, "*") && !regionInName(promo.Name, region);

    private static IEnumerable<PromoInfo> Candidates(PromoLine line, IReadOnlyList<PromoInfo> promos, DateOnly day)
    {
        foreach (var p in promos)
        {
            if ((p.Begin is { } b && day < b) || (p.Till is { } t && day > t))
            {
                continue;
            }

            if (line.IsBonus)
            {
                if (p.Type == "bonus" && (p.Bonus.Contains(line.ProductId) || p.ApplyAll))
                {
                    yield return p;
                }

                continue;
            }

            if (p.Type != "discount")
            {
                continue;
            }

            if (p.DiscountType == "manual")
            {
                // Ручная акция: процент ставит агент; у акции «Ручная скидка Ташкент 8%» — только 8%, без процента — любой.
                if ((p.ApplyAll || p.Conditional.ContainsKey(line.ProductId)) && (PercentOf(p) is not { } manual || manual == line.Percent))
                {
                    yield return p;
                }

                continue;
            }

            if (p.Conditional.TryGetValue(line.ProductId, out var amount))
            {
                var percent = amount is > 0 ? amount.Value : PercentOf(p);
                if (percent is { } x && x == line.Percent)
                {
                    yield return p;
                }
            }
            else if (p.ApplyAll && PercentOf(p) is { } x && x == line.Percent)
            {
                yield return p;
            }
        }
    }

    /// <summary>Процент акции: поле discount, а если там 0 — число с «%» в названии («Могул 10% "E"»).</summary>
    public static decimal? PercentOf(PromoInfo p)
    {
        if (p.Discount is > 0)
        {
            return p.Discount;
        }

        var m = PercentInName().Match(p.Name);
        return m.Success && decimal.TryParse(m.Groups[1].Value.Replace(',', '.'), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var v)
            ? v
            : null;
    }

    /// <summary>
    /// Упоминает ли текст регион (region = "*" — любой из regions): сравнение слов по согласным с латиницей в кириллице — «Фергана» и
    /// «Фаргона», «Хорезм» и «Хоразм», «Сырдарье» и «Сырдарья», «Buxoro» и «Бухоро» совпадают.
    /// </summary>
    public static bool RegionMentioned(string text, string region, IReadOnlyCollection<string> regions)
    {
        var words = Words().Matches(text).Select(m => Skeleton(m.Value)).Where(w => w.Length >= 2).ToHashSet();
        if (words.Count == 0)
        {
            return false;
        }

        return region == "*" ? regions.Any(r => words.Contains(Skeleton(r))) : words.Contains(Skeleton(region));
    }

    [GeneratedRegex(@"[\p{L}']+")]
    private static partial Regex Words();

    private static readonly (string Latin, string Cyrillic)[] Latin =
    [
        ("sh", "ш"), ("ch", "ч"), ("o'", "о"), ("g'", "г"), ("b", "б"), ("v", "в"), ("g", "г"), ("d", "д"), ("j", "ж"), ("z", "з"),
        ("k", "к"), ("l", "л"), ("m", "м"), ("n", "н"), ("p", "п"), ("r", "р"), ("s", "с"), ("t", "т"), ("f", "ф"), ("x", "х"),
        ("h", "х"), ("q", "к"), ("c", "к"),
    ];

    /// <summary>Согласные слова: «Фаргона» → «фргн», «Андижан» → «нджн» («дж» — как «ж»: «Джиззах» = «Жиззах»).</summary>
    public static string Skeleton(string word)
    {
        var w = word.ToLowerInvariant();
        foreach (var (latin, cyrillic) in Latin)
        {
            w = w.Replace(latin, cyrillic);
        }

        w = w.Replace("дж", "ж").Replace('ё', 'е');
        return new string(w.Where(ch => "бвгджзклмнпрстфхцчшщ".Contains(ch)).ToArray());
    }

    [GeneratedRegex(@"(\d+)\s*\+\s*(\d+)")]
    private static partial Regex NPlusM();

    /// <summary>
    /// «Продажа по акции» бонусной строки — как в «Полевом контроле»: стоимость бонуса × N/M для «N+M» («5+1» — ×5, «10+2» — ×5); у остальных
    /// бонусов (100%) — 0. У скидочной строки — сумма строки после скидки.
    /// </summary>
    public static decimal BonusSaleFactor(string promoName) =>
        NPlusM().Match(promoName) is { Success: true } m && int.Parse(m.Groups[2].Value) is > 0 and var bonus ? decimal.Parse(m.Groups[1].Value) / bonus : 0;
}
