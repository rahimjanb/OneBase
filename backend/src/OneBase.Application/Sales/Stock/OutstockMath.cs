namespace OneBase.Application.Sales.Stock;

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

    /// <summary>
    /// Остаток утром каждого дня. snapshotKg — остаток вечером последнего дня (снимок Linko); sold и received — по дням
    /// (индекс 0 — первый день). Вечер дня — это утро следующего. Минус (даты приёмки и проводки разошлись) → 0, и день помечается.
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
            if (m < 0)
            {
                negative[i] = true;
                m = 0;
            }

            morning[i] = m;
            evening = m;
        }

        return (morning, negative);
    }

    public static bool InStock(decimal morningKg) => morningKg > PresenceKg;

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
}
