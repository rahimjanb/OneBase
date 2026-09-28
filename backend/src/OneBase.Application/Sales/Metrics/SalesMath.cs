namespace OneBase.Application.Sales.Metrics;

public enum TargetLevel
{
    Good,
    Warning,
    Bad,
}

/// <summary>Формулы метрик продаж. Все функции чистые.</summary>
public static class SalesMath
{
    /// <summary>num / den; null, если знаменатель 0 (показывается как «—»).</summary>
    public static decimal? Ratio(decimal num, decimal den) => den == 0 ? null : num / den;

    public static decimal? Ratio(decimal num, decimal? den) => den is null or 0 ? null : num / den.Value;

    /// <summary>Отработано дней: с 1-го числа по дату последних данных, не больше длины месяца.</summary>
    public static int WorkedDays(DateOnly monthStart, DateOnly dataThrough)
    {
        if (dataThrough < monthStart)
        {
            return 0;
        }

        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        return dataThrough > monthEnd ? monthEnd.Day : dataThrough.Day;
    }

    /// <summary>Прогноз = факт / отработанных дней × дней в месяце.</summary>
    public static decimal? Forecast(decimal fact, int workedDays, int daysInMonth) =>
        workedDays <= 0 ? null : fact / workedDays * daysInMonth;

    /// <summary>
    /// АКБ — число уникальных ТТ с продажей (чистая выручка &gt; 0) в наборе строк.
    /// Каждая ТТ считается один раз, даже если ей продавали несколько агентов.
    /// </summary>
    public static int Akb(IEnumerable<SaleLine> lines) => ActiveMarkets(lines).Count;

    public static HashSet<long> ActiveMarkets(IEnumerable<SaleLine> lines) =>
        lines.Where(l => l.MarketId != null)
            .GroupBy(l => l.MarketId!.Value)
            .Where(g => g.Sum(l => l.Revenue) > 0)
            .Select(g => g.Key)
            .ToHashSet();

    /// <summary>Число заказов (возвраты не считаются).</summary>
    public static int OrderCount(IEnumerable<SaleLine> lines) =>
        lines.Where(l => l.OrderId != null).Select(l => l.OrderId).Distinct().Count();

    /// <summary>Число различных категорий с чистой продажей &gt; 0.</summary>
    public static int CategoryCount(IEnumerable<SaleLine> lines) =>
        lines.Where(l => l.CategoryId != null)
            .GroupBy(l => l.CategoryId)
            .Count(g => g.Sum(l => l.Revenue) > 0);

    /// <summary>
    /// Группы категорий для карточек: подтип «Помадка 0,5 кг» объединяется с категорией «Помадка»
    /// (имя начинается с имени другой категории и пробела). Возвращает категория → категория-группа.
    /// </summary>
    public static Dictionary<long, long> CategoryGroups(IReadOnlyDictionary<long, string> categories)
    {
        var names = categories.ToDictionary(c => c.Key, c => c.Value.Trim());
        return names.ToDictionary(c => c.Key, c => names
            .Where(p => p.Key != c.Key && p.Value.Length > 0 && c.Value.StartsWith(p.Value + " ", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Value.Length)
            .Select(p => (long?)p.Key)
            .FirstOrDefault() ?? c.Key);
    }

    /// <summary>Медиана; null для пустого набора.</summary>
    public static decimal? Median(IEnumerable<decimal> values)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        if (sorted.Length == 0)
        {
            return null;
        }

        var mid = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }

    /// <summary>
    /// «Те же дни» прошлого месяца: по номеру дня (1–N против 1–N).
    /// Возвращает последний включаемый день прошлого месяца.
    /// </summary>
    public static DateOnly SameDaysCutoff(DateOnly previousMonthStart, int workedDays)
    {
        var days = DateTime.DaysInMonth(previousMonthStart.Year, previousMonthStart.Month);
        return previousMonthStart.AddDays(Math.Clamp(workedDays, 1, days) - 1);
    }

    /// <summary>Изменение в долях: (стало − было) / было; null, если было 0.</summary>
    public static decimal? Delta(decimal before, decimal now) => before == 0 ? null : (now - before) / before;

    /// <summary>
    /// Темп к своему среднему = прогноз месяца / средний факт прошлых месяцев.
    /// Учитываются только месяцы, где у агента были продажи.
    /// </summary>
    public static decimal? TempoToOwnAverage(decimal fact, int workedDays, int daysInMonth, IEnumerable<decimal> previousMonths)
    {
        var history = previousMonths.Where(v => v > 0).ToList();
        var forecast = Forecast(fact, workedDays, daysInMonth);
        return forecast is null || history.Count == 0 ? null : forecast / history.Average();
    }

    /// <summary>Уровень плашки «X% от цели»: ≥100% — зелёная, 70–99% — оранжевая, &lt;70% — красная.</summary>
    public static TargetLevel? TargetLevelOf(decimal? value, decimal target)
    {
        var ratio = Ratio(value ?? 0, target);
        if (value is null || ratio is null)
        {
            return null;
        }

        return ratio >= 1 ? TargetLevel.Good : ratio >= 0.7m ? TargetLevel.Warning : TargetLevel.Bad;
    }

    /// <summary>План по набору строк плана: если есть строка без категории — это итог, иначе сумма категорий.</summary>
    public static decimal? PlanTotal(IEnumerable<PlanRow> rows)
    {
        var list = rows.ToList();
        if (list.Count == 0)
        {
            return null;
        }

        var total = list.Where(r => r.CategoryId == null).ToList();
        return total.Count > 0 ? total.Sum(r => r.PlanKg) : list.Sum(r => r.PlanKg);
    }
}
