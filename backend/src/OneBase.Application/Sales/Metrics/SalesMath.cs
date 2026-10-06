namespace OneBase.Application.Sales.Metrics;

public enum TargetLevel
{
    Good,
    Warning,
    Bad,
}

/// <summary>Группы ABC: A — SKU, дающие первые 80% веса, B — до 95%, C — хвост.</summary>
public static class AbcGroups
{
    public const string A = "A";
    public const string B = "B";
    public const string C = "C";
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
    /// АКБ — число уникальных ТТ (market.id), у которых чистый вес в наборе (продажи минус возвраты) больше нуля.
    /// Так считает «Полевой контроль»: точка, вернувшая всю покупку, в базу месяца не входит (сверено по всем
    /// регионам за август 2026 — совпадение до точки). Каждая ТТ считается один раз, даже если ей продавали несколько агентов.
    /// </summary>
    public static int Akb(IEnumerable<SaleLine> lines) => ActiveMarkets(lines).Count;

    /// <summary>ТТ с чистым весом больше нуля — те, что входят в АКБ набора.</summary>
    public static HashSet<long> ActiveMarkets(IEnumerable<SaleLine> lines) =>
        lines.Where(l => l.MarketId != null && !SalesOptions.IsChannelBranch(l.BranchId)) // каналы (базар, сети) в АКБ не входят
            .GroupBy(l => l.MarketId!.Value)
            .Where(g => g.Sum(l => l.Kg) > 0)
            .Select(g => g.Key)
            .ToHashSet();

    /// <summary>Строка положительна: чистые кг или чистая выручка больше нуля (возврат больше продажи — не продажа).</summary>
    public static bool Positive(decimal kg, decimal revenue) => kg > 0 || revenue > 0;

    /// <summary>
    /// ТТ артикула (АКБ SKU): уникальные ТТ, у которых чистая строка товара в наборе положительна — кг или выручка больше нуля
    /// (DOC §9.6 «точки с положительной строкой»). productLines — строки одного товара; строки ТТ складываются по всем её ТП,
    /// поэтому ТТ, купившая у двух ТП, считается один раз.
    /// </summary>
    public static HashSet<long> SkuMarkets(IEnumerable<SaleLine> productLines) =>
        productLines.Where(l => l.MarketId != null && !SalesOptions.IsChannelBranch(l.BranchId))
            .GroupBy(l => l.MarketId!.Value)
            .Where(g => Positive(g.Sum(l => l.Kg), g.Sum(l => l.Revenue)))
            .Select(g => g.Key)
            .ToHashSet();

    /// <summary>ТТ артикула — число ТТ с положительной строкой товара (см. SkuMarkets). Больше нуля — товар в наборе «продаётся».</summary>
    public static int SkuTt(IEnumerable<SaleLine> productLines) => SkuMarkets(productLines).Count;

    /// <summary>Положительные строки набора: ТТ → товары, у которых чистая строка этой ТТ положительна (кг или выручка больше нуля).</summary>
    public static Dictionary<long, HashSet<long>> PositiveSkus(IEnumerable<SaleLine> lines) =>
        lines.Where(l => l.MarketId != null && l.ProductId != null && !SalesOptions.IsChannelBranch(l.BranchId))
            .GroupBy(l => (Market: l.MarketId!.Value, Product: l.ProductId!.Value))
            .Where(g => Positive(g.Sum(l => l.Kg), g.Sum(l => l.Revenue)))
            .GroupBy(g => g.Key.Market)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Key.Product).ToHashSet());

    /// <summary>
    /// «Только он» (DOC §9.2): моно-точка — ТТ с положительной строкой ровно по одному товару набора. Товар → сколько моно-точек
    /// держатся только на нём; сумма значений — число моно-точек набора.
    /// </summary>
    public static Dictionary<long, int> SoloByProduct(IEnumerable<SaleLine> lines) =>
        PositiveSkus(lines).Values
            .Where(skus => skus.Count == 1)
            .GroupBy(skus => skus.First())
            .ToDictionary(g => g.Key, g => g.Count());

    /// <summary>
    /// Цвет клетки матрицы «товар × регион»: товара в регионе нет (ни одной ТТ) — красный, дистрибуция меньше доли lowShare
    /// от средней дистрибуции товара по охвату — оранжевый, иначе — обычный.
    /// </summary>
    public static string MatrixLevelOf(int tt, decimal? distribution, decimal? average, decimal lowShare) =>
        tt <= 0 ? MatrixLevels.None
        : average is { } a && distribution is { } d && d < a * lowShare ? MatrixLevels.Low
        : MatrixLevels.Ok;

    /// <summary>Группа ABC по накопленной доле веса (SKU по убыванию кг): A — до 80% включительно, B — до 95%, остальные — C.</summary>
    public static string AbcOf(decimal? cumulativeShare) =>
        cumulativeShare is not { } c ? AbcGroups.C : c <= 0.80m ? AbcGroups.A : c <= 0.95m ? AbcGroups.B : AbcGroups.C;

    /// <summary>Число заказов (возвраты не считаются).</summary>
    public static int OrderCount(IEnumerable<SaleLine> lines) =>
        lines.Where(l => l.OrderId != null).Select(l => l.OrderId).Distinct().Count();

    /// <summary>Число различных категорий отчёта, по которым были заказы. group — тип товара → категория отчёта.</summary>
    public static int CategoryCount(IEnumerable<SaleLine> lines, Func<long?, long?> group) =>
        lines.Where(l => l.OrderId != null && l.CategoryId != null)
            .Select(l => group(l.CategoryId))
            .Distinct()
            .Count();

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

    /// <summary>Уровень плашки «X% от цели», как в «Полевом контроле»: ≥95% — зелёная, 75–94% — оранжевая, &lt;75% — красная.</summary>
    public static TargetLevel? TargetLevelOf(decimal? value, decimal target)
    {
        var ratio = Ratio(value ?? 0, target);
        if (value is null || ratio is null)
        {
            return null;
        }

        return ratio >= 0.95m ? TargetLevel.Good : ratio >= 0.75m ? TargetLevel.Warning : TargetLevel.Bad;
    }

    /// <summary>Цвет прогноза к плану по категориям, как в «Полевом контроле»: от 100% — зелёный, от 90% — оранжевый, ниже — красный.</summary>
    public static TargetLevel? ForecastLevelOf(decimal? forecastExecution) =>
        forecastExecution is not { } v ? null : v >= 1m ? TargetLevel.Good : v >= 0.9m ? TargetLevel.Warning : TargetLevel.Bad;

    /// <summary>
    /// Цвет выполнения (план, % плана визитов), как execLevel в «Полевом контроле»: от 90% — зелёный, от 60% — оранжевый, ниже — красный;
    /// без значения — нет цвета. Интерфейс только раскрашивает по уровню.
    /// </summary>
    public static TargetLevel? ExecutionLevelOf(decimal? execution) =>
        execution is not { } v ? null : v >= 0.9m ? TargetLevel.Good : v >= 0.6m ? TargetLevel.Warning : TargetLevel.Bad;

    /// <summary>Среднее за месяц по месяцам с данными (идущий месяц тоже в счёт, как в «Полевом контроле»); null — данных нет.</summary>
    public static decimal? Average(IEnumerable<decimal?> values)
    {
        var known = values.OfType<decimal>().ToList();
        return known.Count == 0 ? null : known.Average();
    }

    /// <summary>
    /// Тяжесть «Проблемного агента» (порядок в списке, как в «Полевом контроле»): 10 за критичное замечание, 4 за риск, плюс
    /// (1 − min(страйк, 1)) × 3 у оцениваемых агентов (визитов не меньше minVisits и страйк есть).
    /// </summary>
    public static decimal ProblemScore(int critical, int risk, decimal? conversion, int visits, int minVisits) =>
        10 * critical + 4 * risk + (visits >= minVisits && conversion is { } c ? (1 - Math.Min(c, 1)) * 3 : 0);

    /// <summary>Осталось до плана: план − факт, не меньше нуля.</summary>
    public static decimal Remaining(decimal plan, decimal fact) => Math.Max(0, plan - fact);

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
