using OneBase.Application.Telegram.Battle;
using OneBase.Application.Telegram.Reports;

namespace OneBase.Application.Telegram.Incentive;

/// <summary>
/// Правила накопительной акции (ТЗ «Проект 3», раздел 6), чистые функции: ступени и приз, рабочие дни и норма в день, ориентир месяца, цель
/// магазина по среднему закупу, статус магазина, рейтинг.
/// </summary>
public static class IncentiveRules
{
    /// <summary>Типы точек без цели на маршруте: экспорт, офисы, персонал, тестовые (опт и супермаркеты — с целью, как на образце).</summary>
    public static readonly string[] ExcludedMarketTypeWords = ["EXPORT", "Office", "Персонал", "тест", "test"];

    /// <summary>Магазинов с целью на карточке дня по умолчанию (на образце ТЗ — 12); в настройках акции — от 1 до 30.</summary>
    public const int MaxStores = 12;

    public const int MaxStoresLimit = 30;

    /// <summary>Высшая взятая ступень: один ТП — один приз (призы не суммируются).</summary>
    public static IncentiveTier? Reached(IReadOnlyList<IncentiveTier> tiers, decimal kg) => tiers.Where(t => kg >= t.Kg).MaxBy(t => t.Kg);

    /// <summary>Ближайшая невзятая ступень; null — взяты все.</summary>
    public static IncentiveTier? Next(IReadOnlyList<IncentiveTier> tiers, decimal kg) => tiers.Where(t => kg < t.Kg).MinBy(t => t.Kg);

    /// <summary>Рабочие дни (пн–сб без праздников) с from по to включительно.</summary>
    public static int WorkDays(DateOnly from, DateOnly to, IReadOnlyDictionary<DateOnly, string> holidays)
    {
        var count = 0;
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            if (DailyPlanMath.IsWorkingDay(d, holidays))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Ход к призам на момент: firstDay — первый день, который ещё можно работать (утром — сегодня, вечером — завтра). Норма — остаток до
    /// ближайшей невзятой ступени ÷ рабочие дни до конца акции, вверх до целых кг.
    /// </summary>
    public static IncentiveProgress Progress(IncentiveSettings s, decimal total, DateOnly firstDay, IReadOnlyDictionary<DateOnly, string> holidays)
    {
        var next = Next(s.Tiers, total);
        var days = firstDay > s.End ? 0 : WorkDays(firstDay < s.Start ? s.Start : firstDay, s.End, holidays);
        decimal? norm = next is null || days == 0 ? null : Math.Ceiling((next.Kg - total) / days);
        return new IncentiveProgress(total, Reached(s.Tiers, total), next, s.Tiers.Select(t => new IncentiveTierLeft(t, Math.Max(0, t.Kg - total))).ToList(), days, norm);
    }

    /// <summary>Ориентир месяца для ступени: порог × доля месяца (нет доли — null).</summary>
    public static decimal? MonthGuide(IncentiveSettings s, IncentiveTier tier, int year, int month) =>
        s.Months.FirstOrDefault(m => m.Year == year && m.Month == month) is { } share ? Math.Round(tier.Kg * share.Share, 1) : null;

    /// <summary>Набор по среднему закупу ТТ: самый крупный, до которого ТТ дотягивает (от 5 млн — 140 + 200 г … меньше 0,5 млн — 35 г).</summary>
    public static IncentiveStoreSet? StoreSet(IReadOnlyList<IncentiveStoreSet> sets, decimal avgMonth) =>
        sets.Where(x => avgMonth >= x.MinAvg).MaxBy(x => x.MinAvg);

    /// <summary>Набор того же уровня, что Твизос-цель «Поля боя» (300 → 140 г, 400 → 140 + 35 г, 600 → 140 + 200 г).</summary>
    public static IncentiveStoreSet? SetForLevel(IReadOnlyList<IncentiveStoreSet> sets, string? level) =>
        level is null ? null : sets.FirstOrDefault(x => x.Level == level);

    /// <summary>Статус магазина: кг товара акции в заказах дня ≥ цели — взят; меньше — частично; визит или заказ без товара — отказ; иначе впереди.</summary>
    public static string StoreStatus(decimal kg, decimal target, bool visited) =>
        kg > 0 && kg >= target - 0.0005m ? BattleStoreStatus.Taken
        : kg > 0 ? BattleStoreStatus.Partial
        : visited ? BattleStoreStatus.Refused
        : BattleStoreStatus.Ahead;

    /// <summary>Доля дневной цели, которую нужно взять к полудню: прошедшая часть рабочего дня (08:30 → 18:00, 13:00 — 47%).</summary>
    public static decimal MiddayShare(TimeOnly start, TimeOnly midday, TimeOnly end)
    {
        var day = (end - start).TotalMinutes;
        return day <= 0 ? 0 : (decimal)Math.Clamp((midday - start).TotalMinutes / day, 0, 1);
    }

    /// <summary>Лучшие клиенты для дозаказа (ТЗ, раздел 6): ТОП-5 ТТ ТП по кг товара акции за 60 дней.</summary>
    public const int TopMarkets = 5;

    public const int TopDays = 60;

    /// <summary>
    /// Цель ТТ на неделю: ≈ 60% её среднего недельного закупа за 60 дней, до ближайших 5 кг, не меньше 5 кг («Дозакажи Twizos»).
    /// </summary>
    public static decimal WeekTarget(decimal kgPerPeriod, int days = TopDays)
    {
        var weekly = days <= 0 ? 0 : kgPerPeriod * 7 / days;
        return Math.Max(5, Math.Round(weekly * 0.6m / 5, MidpointRounding.AwayFromZero) * 5);
    }

    /// <summary>Рейтинг: по накопленному, при равенстве — по имени и ID.</summary>
    public static IReadOnlyList<IncentiveStanding> Rating(IEnumerable<IncentiveStanding> standings) =>
        standings.OrderByDescending(a => a.Total).ThenBy(a => a.Agent.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(a => a.Agent.Id).ToList();
}
