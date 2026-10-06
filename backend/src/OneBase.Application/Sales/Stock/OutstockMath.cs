namespace OneBase.Application.Sales.Stock;

/// <summary>Чья потеря в день без товара у дилера.</summary>
public enum LossOwner
{
    /// <summary>На заводе товар был — недовоз или поздний заказ дилера.</summary>
    Dealer,

    /// <summary>На заводе тоже ноль — отгружать было нечего.</summary>
    Factory,

    /// <summary>По товару нет данных склада завода.</summary>
    Unknown,
}

/// <summary>
/// Аутсток — дни, когда у дилера на складе нет товара, который он обычно продаёт. Linko помнит остаток только «на сейчас»,
/// поэтому остаток по дням восстанавливается назад от снимка: утро = вечер + продано за день − привезено за день.
/// Чистая арифметика без базы — чтобы правила проверялись тестами.
/// </summary>
public static class OutstockMath
{
    /// <summary>Товар «есть», если утром на складе больше этого веса.</summary>
    public const decimal PresenceKg = 0.5m;

    /// <summary>Ядро потерь — пары, которые набирают эту долю всех потерь.</summary>
    public const decimal CoreShare = 0.8m;

    /// <summary>«Хуже всего по доле» выбирается среди регионов, продавших за месяц больше этого веса: у крошечного региона доля не показательна.</summary>
    public const decimal WorstRegionMinKg = 1000m;

    /// <summary>
    /// Остаток утром каждого дня. snapshotKg — остаток вечером последнего дня (снимок Linko); sold и received — по дням
    /// (индекс 0 — первый день). Вечер дня — это утро следующего. Минус (даты приёмки и проводки разошлись) показывается нулём
    /// и день помечается, но сам ход назад продолжается от отрицательного остатка — иначе один сдвиг проводки стирал бы весь
    /// предшествующий аутсток («Полевой контроль»: минус → 0 только на экране).
    /// </summary>
    public static (decimal[] Morning, bool[] Negative) Reconstruct(decimal snapshotKg, IReadOnlyList<decimal> sold, IReadOnlyList<decimal> received)
    {
        if (sold.Count != received.Count)
        {
            throw new ArgumentException("У продаж и прихода должно быть одинаковое число дней.");
        }

        var n = sold.Count;
        var morning = new decimal[n];
        var negative = new bool[n];
        var evening = snapshotKg;
        for (var i = n - 1; i >= 0; i--)
        {
            var m = evening + sold[i] - received[i];
            negative[i] = m < 0;
            morning[i] = Math.Max(0, m);
            evening = m;
        }

        return (morning, negative);
    }

    public static bool InStock(decimal morningKg) => morningKg > PresenceKg;

    /// <summary>
    /// Приход дилеру считается, только если перемещение принято не позже момента снимка: Linko прибавляет товар к остатку дилера
    /// при приёмке, и непринятое перемещение в снимке ещё не отражено — вычитать его назад нельзя.
    /// </summary>
    public static bool ReceiptCounts(DateTime? acceptedAt, DateTime snapshotLocal) => acceptedAt is { } at && at <= snapshotLocal;

    /// <summary>Чья потеря в день без товара: на заводе было больше 0,5 кг — дилера; не больше — завода; нет данных по заводу — неизвестно.</summary>
    public static LossOwner Blame(decimal? factoryMorningKg) =>
        factoryMorningKg is not { } f ? LossOwner.Unknown : InStock(f) ? LossOwner.Dealer : LossOwner.Factory;

    /// <summary>Упущено, кг = дней в нуле × (продажи за период ÷ дней периода).</summary>
    public static decimal LostKg(int zeroDays, decimal periodKg, int periodDays) =>
        periodDays > 0 && zeroDays > 0 ? zeroDays * periodKg / periodDays : 0;

    /// <summary>Сколько первых пар из списка, отсортированного по убыванию потерь, набирают долю share всех потерь.</summary>
    public static int CoreCount(IReadOnlyList<decimal> lossesDescending, decimal share = CoreShare)
    {
        var total = lossesDescending.Sum();
        if (total <= 0)
        {
            return 0;
        }

        decimal acc = 0;
        for (var i = 0; i < lossesDescending.Count; i++)
        {
            acc += lossesDescending[i];
            if (acc >= total * share)
            {
                return i + 1;
            }
        }

        return lossesDescending.Count;
    }

    /// <summary>Хронический аутсток — в нуле половину периода и больше.</summary>
    public static bool IsChronic(int zeroDays, int periodDays) => periodDays > 0 && zeroDays * 2 >= periodDays;

    /// <summary>«Каждый N-й килограмм могли продать, но не продали»: N = продано ÷ упущено, не меньше 2; null — потерь нет.</summary>
    public static int? EveryNthKg(decimal soldKg, decimal lostKg) =>
        lostKg > 0 && soldKg > 0 ? Math.Max(2, (int)Math.Round(soldKg / lostKg, MidpointRounding.AwayFromZero)) : null;
}
