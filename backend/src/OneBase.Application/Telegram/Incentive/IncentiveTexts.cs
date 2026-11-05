using System.Globalization;
using OneBase.Application.Abstractions;
using OneBase.Application.Telegram.Reports;

namespace OneBase.Application.Telegram.Incentive;

/// <summary>
/// Тексты накопительной акции: килограммы, призы и наборы на языке карточки (названия — из настроек акции, по-узбекски — латиницей, кириллица —
/// переводом), месяцы, подпись под картинкой по шаблонам ТЗ (раздел 8) — всегда по-русски и по-узбекски.
/// </summary>
public static class IncentiveTexts
{
    private static readonly string[] MonthsRu = ["январь", "февраль", "март", "апрель", "май", "июнь", "июль", "август", "сентябрь", "октябрь", "ноябрь", "декабрь"];
    private static readonly string[] MonthsOfRu = ["января", "февраля", "марта", "апреля", "мая", "июня", "июля", "августа", "сентября", "октября", "ноября", "декабря"];
    private static readonly string[] MonthsInRu = ["январе", "феврале", "марте", "апреле", "мае", "июне", "июле", "августе", "сентябре", "октябре", "ноябре", "декабре"];
    private static readonly string[] MonthsUz = ["yanvar", "fevral", "mart", "aprel", "may", "iyun", "iyul", "avgust", "sentyabr", "oktabr", "noyabr", "dekabr"];

    private static string Upper(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private static bool Uz(string language) => language is UiLanguage.UzbekLatin or UiLanguage.UzbekCyrillicCode;

    private static string Latin(string language, string latin, string ru) => language == UiLanguage.UzbekCyrillicCode ? ReportTexts.Uzbek(language, latin, ru) : latin;

    /// <summary>Итоги в кг: от 100 — целые с разрядами («1 885»), меньше — с десятой без лишнего нуля («52», «7,6»).</summary>
    public static string Kg(decimal value)
    {
        if (Math.Abs(value) >= 100)
        {
            return ReportFormat.Number(value);
        }

        var rounded = Math.Round(value, 1, MidpointRounding.AwayFromZero);
        return (rounded == Math.Round(rounded) ? rounded.ToString("0", CultureInfo.InvariantCulture) : rounded.ToString("0.0", CultureInfo.InvariantCulture)).Replace('.', ',');
    }

    /// <summary>Вес магазина — всегда с десятой: «7,6», «1,0».</summary>
    public static string Kg1(decimal value) => Math.Round(value, 1, MidpointRounding.AwayFromZero).ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',');

    public static string Prize(IncentiveTier tier, string language) => Uz(language) && tier.PrizeUz is { } uz ? Latin(language, uz, tier.Prize) : tier.Prize;

    public static string Short(IncentiveTier tier, string language) => Uz(language) && tier.ShortUz is { } uz ? Latin(language, uz, tier.Short) : tier.Short;

    /// <summary>«до планшета» / «planshetgacha».</summary>
    public static string To(IncentiveTier tier, string language) =>
        Uz(language) ? Latin(language, $"{tier.ShortUz ?? tier.Short}gacha", $"до {tier.Of ?? tier.Short}") : $"до {tier.Of ?? tier.Short}";

    /// <summary>Набор магазина и приз клиенту на языке карточки: по набору акции с тем же составом (цели хранят русский состав).</summary>
    public static (string Contents, string? Prize, string? Level) StoreSet(IncentiveSettings s, string contents, string? prize, string language)
    {
        var set = s.StoreSets.FirstOrDefault(x => x.Contents == contents);
        if (set is null)
        {
            return (contents, prize, null);
        }

        return Uz(language)
            ? (set.ContentsUz is { } c ? Latin(language, c, set.Contents) : set.Contents, set.PrizeUz is { } p ? Latin(language, p, set.Prize ?? p) : set.Prize, set.Level)
            : (set.Contents, set.Prize, set.Level);
    }

    /// <summary>«ноябрь» / «noyabr».</summary>
    public static string MonthName(int month, string language) => Uz(language) ? Latin(language, MonthsUz[month - 1], MonthsRu[month - 1]) : MonthsRu[month - 1];

    /// <summary>«ноября» (неделя 3 ноября) / «noyabr».</summary>
    public static string MonthOf(int month, string language) => Uz(language) ? Latin(language, MonthsUz[month - 1], MonthsOfRu[month - 1]) : MonthsOfRu[month - 1];

    /// <summary>«октябрь 285 + ноябрь 230»: кг по месяцам акции с начала по месяц дня.</summary>
    public static string MonthsLine(IncentiveSettings s, IncentiveStanding standing, DateOnly date, string language)
    {
        var parts = new List<string>();
        for (var m = new DateOnly(s.Start.Year, s.Start.Month, 1); m <= date && m <= s.End; m = m.AddMonths(1))
        {
            parts.Add($"{MonthName(m.Month, language)} {Kg(standing.Month(m.Year, m.Month))}");
        }

        return string.Join(" + ", parts);
    }

    /// <summary>Подпись под картинкой (по-русски и по-узбекски) и кратко для журнала.</summary>
    public static (string Caption, string Summary) Caption(IncentiveCards cards)
    {
        var s = cards.Program;
        var product = s.Product;
        if (cards.Slot == IncentiveSlot.Rating && cards.Rating is { } rating)
        {
            var leader = rating.Retail.FirstOrDefault();
            if (leader is null)
            {
                return ($"Рейтинг акции {product}: накоплений пока нет.\n{product} aksiyasi reytingi: hozircha yig‘ilganlar yo‘q.", $"Рейтинг {product}: пусто");
            }

            var toRu = leader.Next is { } n ? $" До {n.Of ?? n.Short} у лидера — {Kg(leader.ToNext ?? 0)} кг." : "";
            var toUz = leader.Next is { } nu ? $" Yetakchiga {nu.ShortUz ?? nu.Short}gacha — {Kg(leader.ToNext ?? 0)} kg." : "";
            return ($"Рейтинг акции {product} на {cards.Date:dd.MM}: 1 — {leader.Agent.Name}, {Kg(leader.Kg)} кг.{toRu}\n" +
                    $"{product} aksiyasi reytingi {cards.Date:dd.MM}: 1-o‘rin — {leader.Agent.Name}, {Kg(leader.Kg)} kg.{toUz}",
                $"Рейтинг: 1 — {leader.Agent.Name}, {Kg(leader.Kg)} кг");
        }

        // Сводка команды (СВ, РМ) — подпись о команде, даже если с ней одна копия карточки ТП.
        if (cards.Team is not null || cards.Agents.Count != 1)
        {
            var team = cards.Team;
            var total = team?.Total ?? cards.Agents.Sum(a => a.Total);
            var today = team?.Today ?? cards.Agents.Sum(a => a.Today);
            var count = team?.Agents.Count ?? cards.Agents.Count;
            var behind = team?.Behind ?? 0;
            var todayRu = cards.Slot == IncentiveSlot.Morning ? "" : $", сегодня {Kg(today)} кг";
            var todayUz = cards.Slot == IncentiveSlot.Morning ? "" : $", bugun {Kg(today)} kg";
            return ($"{product} · {count} ТП: накоплено {Kg(total)} кг{todayRu}. Темп ниже нормы — у {behind}.\n" +
                    $"{product} · {count} ta TP: yig‘ildi {Kg(total)} kg{todayUz}. Sur’ati me’yordan past — {behind} ta.",
                $"{count} ТП: накоплено {Kg(total)} кг{todayRu}");
        }

        var me = cards.Agents[0];
        var next = me.Progress.Next;
        var x = Kg(me.Total);
        var y = Kg(me.Progress.ToNext ?? 0);
        var norm = me.Progress.Norm is { } nn ? Kg(nn) : "—";
        string ru, uz, summary;
        switch (cards.Slot)
        {
            case IncentiveSlot.Morning or IncentiveSlot.Midday:
            {
                var route = me.Route;
                var stores = route?.Stores.Count ?? 0;
                var target = Kg(route?.TargetKg ?? 0);
                var tailRu = next is null ? "Все призы акции взяты." : $"Накоплено {x} кг, до {next.Of ?? next.Short} — {y} кг.";
                var tailUz = next is null ? "Aksiyaning barcha sovg‘alari olindi." : $"Yig‘ildi {x} kg, {next.ShortUz ?? next.Short}gacha — {y} kg.";
                if (cards.Slot == IncentiveSlot.Morning)
                {
                    ru = $"Маршрут: {stores} магазинов, цель {target} кг {product}. {tailRu}";
                    uz = $"Marshrut: {stores} ta do‘kon, maqsad {target} kg {product}. {tailUz}";
                    summary = $"Маршрут: {stores} магазинов, цель {target} кг · накоплено {x} кг";
                }
                else
                {
                    ru = $"Полдень: сегодня {Kg(me.Today)} из {target} кг {product}. {tailRu}";
                    uz = $"Tush: bugun {target} kg dan {Kg(me.Today)} kg {product}. {tailUz}";
                    summary = $"Полдень: сегодня {Kg(me.Today)} из {target} кг · накоплено {x} кг";
                }

                break;
            }

            case IncentiveSlot.Week:
            {
                var w = Kg(me.Week?.Kg ?? 0);
                var rank = me.Rank?.ToString(CultureInfo.InvariantCulture) ?? "—";
                ru = $"За неделю {w} кг, всего {x} кг. Место — {rank}.";
                uz = $"Haftada {w} kg, jami {x} kg. O‘rin — {rank}.";
                summary = $"Неделя: {w} кг, всего {x} кг, место {rank}";
                break;
            }

            case IncentiveSlot.Month when me.Month is { } month:
            {
                if (month.Final || me.Progress.WorkDaysLeft == 0)
                {
                    var won = me.Progress.Reached;
                    ru = won is null ? $"Итог акции: {x} кг. Приз не взят — {(next is null ? "" : $"до {next.Of ?? next.Short} не хватило {y} кг")}."
                        : $"Итог акции: {x} кг. Приз — {won.Prize} (по доставленным накладным после 31.12).";
                    uz = won is null ? $"Aksiya yakuni: {x} kg. Sovg‘a olinmadi{(next is null ? "" : $" — {next.ShortUz ?? next.Short}gacha {y} kg yetmadi")}."
                        : $"Aksiya yakuni: {x} kg. Sovg‘a — {won.PrizeUz ?? won.Prize} (31.12 dan keyin yetkazilgan yuklar bo‘yicha).";
                    summary = $"Итог акции: {x} кг";
                    break;
                }

                var nextMonth = new DateOnly(month.Year, month.Month, 1).AddMonths(1).Month;
                var monthKg = Kg(month.Kg);
                ru = next is null ? $"Итог месяца: {monthKg} кг. Все призы акции взяты."
                    : $"Итог месяца: {monthKg} кг. В {MonthsInRu[nextMonth - 1]} нужно {y} кг до {next.Of ?? next.Short}.";
                uz = next is null ? $"Oy yakuni: {monthKg} kg. Aksiyaning barcha sovg‘alari olindi."
                    : $"Oy yakuni: {monthKg} kg. {Upper(MonthsUz[nextMonth - 1])}da {next.ShortUz ?? next.Short}gacha {y} kg kerak.";
                summary = $"Месяц: {monthKg} кг, накоплено {x} кг";
                break;
            }

            default:
            {
                ru = next is null ? $"Накоплено {x} кг. Все призы акции взяты." : $"Накоплено {x} кг. До {next.Of ?? next.Short} — {y} кг, норма {norm} кг в день.";
                uz = next is null ? $"Yig‘ildi {x} kg. Aksiyaning barcha sovg‘alari olindi." : $"Yig‘ildi {x} kg. {Upper(next.ShortUz ?? next.Short)}gacha — {y} kg, kuniga {norm} kg.";
                summary = $"Итог дня: сегодня {Kg(me.Today)} кг · накоплено {x} кг";
                break;
            }
        }

        return ($"{ru}\n{uz}", summary);
    }
}
