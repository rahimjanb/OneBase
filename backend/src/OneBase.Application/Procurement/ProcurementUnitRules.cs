namespace OneBase.Application.Procurement;

/// <summary>Единица закупки из формы: код единицы и сколько единиц карточки в одной (мешок = 50 кг).</summary>
public sealed record ProcurementItemUnitInput(string? UnitCode, decimal Factor);

/// <summary>Единица закупки позиции для форм.</summary>
public sealed record ProcurementItemUnitView(string UnitCode, decimal Factor);

/// <summary>
/// Единицы закупки (D10, чистые функции — проверяются тестами): приход ведут в единице карточки или в её единице закупки; количество
/// пересчитывается в единицу карточки — до трёх знаков, иначе ошибка (не округляем молча); цена за единицу карточки — до двух знаков.
/// </summary>
public static class ProcurementUnitRules
{
    public const decimal MaxFactor = 1_000_000m;

    public const int MaxUnits = 10;

    /// <summary>Коэффициент единицы: единица карточки — 1, единица закупки — её коэффициент, другая — null (так не принять).</summary>
    public static decimal? Factor(string itemUnit, string? unit, IEnumerable<(string UnitCode, decimal Factor)> units) =>
        string.IsNullOrWhiteSpace(unit) || unit == itemUnit ? 1m : units.Where(u => u.UnitCode == unit).Select(u => (decimal?)u.Factor).FirstOrDefault();

    /// <summary>Количество в единице карточки; больше трёх знаков после пересчёта — null.</summary>
    public static decimal? ToBase(decimal quantity, decimal factor)
    {
        var value = quantity * factor;
        return decimal.Round(value, 3) == value ? value : null;
    }

    /// <summary>Цена за единицу карточки: цена единицы закупки ÷ коэффициент, до двух знаков.</summary>
    public static decimal? CostToBase(decimal? cost, decimal factor) => cost is { } c ? decimal.Round(c / factor, 2) : null;

    /// <summary>Проверка единиц закупки карточки: единица есть в справочнике, не единица карточки, не повторяется, коэффициент больше нуля, до шести знаков.</summary>
    public static string? Check(string itemUnit, IReadOnlyList<ProcurementItemUnitInput> units, IReadOnlyCollection<string> known)
    {
        if (units.Count > MaxUnits)
        {
            return $"Единиц закупки — до {MaxUnits}.";
        }

        var seen = new HashSet<string>();
        foreach (var u in units)
        {
            var code = u.UnitCode?.Trim();
            if (string.IsNullOrEmpty(code) || !known.Contains(code))
            {
                return "Единица закупки: выберите единицу из справочника.";
            }

            if (code == itemUnit)
            {
                return $"«{code}» — единица карточки: единица закупки должна быть другой.";
            }

            if (!seen.Add(code))
            {
                return $"Единица закупки «{code}» повторяется.";
            }

            if (u.Factor <= 0 || u.Factor > MaxFactor || decimal.Round(u.Factor, 6) != u.Factor)
            {
                return $"«{code}»: сколько {itemUnit} в одной — больше нуля, не больше шести знаков после запятой.";
            }
        }

        return null;
    }
}
