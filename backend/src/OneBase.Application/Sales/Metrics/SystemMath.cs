namespace OneBase.Application.Sales.Metrics;

/// <summary>Корзина старения долга: дней от отгрузки [From; To] (To = null — без верхней границы) и доля резерва — сколько из неё, скорее всего, не вернётся.</summary>
public sealed record SystemBucket(string Label, int From, int? To, decimal ReserveShare);

/// <summary>
/// Правила «Системы» — как вкладка «Система» «Полевого контроля» (по книгам Дорощука и Герасименко): не сколько продали, а как работает
/// конвейер — маршрут, качество точки, деньги и запас дилера. Чистые функции.
/// </summary>
public static class SystemMath
{
    /// <summary>Сколько SKU ядра нужно точке, чтобы считаться качественной.</summary>
    public const int CoreMin = 3;

    /// <summary>Ядро — SKU, которые вместе дают эту долю веса своей категории за окно (Парето).</summary>
    public const decimal CoreShare = 0.8m;

    /// <summary>Месяцев в окне ядра и регулярности верхних точек (выбранный и два прошлых).</summary>
    public const int CoreMonths = 3;

    /// <summary>Верхние точки — эта доля АКБ с наибольшим весом (весовая дистрибуция).</summary>
    public const decimal TopShare = 0.2m;

    /// <summary>
    /// Корзины старения и резерв по ним — пример шкалы из книги Герасименко («Бизнес, финотчётность и не только», гл. 20): своей истории
    /// невозврата нет, это ориентир, а не прогноз.
    /// </summary>
    public static readonly IReadOnlyList<SystemBucket> Buckets =
    [
        new("0–30", 0, 30, 0.008m),
        new("31–60", 31, 60, 0.02m),
        new("61–120", 61, 120, 0.05m),
        new("121–240", 121, 240, 0.2m),
        new("241–360", 241, 360, 0.35m),
        new(">360", 361, null, 0.5m),
    ];

    /// <summary>Меньше стольких прошедших дней месяца запас считается с 1-го прошлого месяца: за день-два скорость продаж не видна.</summary>
    public const int MinStockDays = 7;

    /// <summary>Первичка больше вторички на эту долю и запас в днях вырос — «затоваривание?»: дилера грузят быстрее, чем он продаёт.</summary>
    public const decimal OverstockRatio = 1.15m;

    /// <summary>
    /// Ядро ассортимента: в каждой категории отчёта — SKU по убыванию веса за окно, пока вместе не наберут CoreShare веса категории
    /// (SKU, на котором доля набирается, входит). Товары вне категорий отчёта (category = null) и с весом не больше нуля не входят.
    /// </summary>
    public static IReadOnlyList<long> Core(IReadOnlyDictionary<long, decimal> productKg, Func<long, long?> category)
    {
        var core = new List<long>();
        foreach (var group in productKg.Where(p => p.Value > 0).Select(p => (Id: p.Key, Kg: p.Value, Category: category(p.Key)))
                     .Where(p => p.Category is not null).GroupBy(p => p.Category))
        {
            var total = group.Sum(p => p.Kg);
            var sum = 0m;
            foreach (var p in group.OrderByDescending(p => p.Kg).ThenBy(p => p.Id))
            {
                core.Add(p.Id);
                sum += p.Kg;
                if (sum >= CoreShare * total)
                {
                    break;
                }
            }
        }

        return core;
    }

    /// <summary>
    /// Качество дистрибуции по строкам области: АКБ — точки с чистым весом больше нуля; у каждой — SKU с чистым весом больше нуля, из них
    /// SKU ядра и категории отчёта. Верхние 20% — точки АКБ с наибольшим весом (округление, не меньше одной); TopRegular — сколько из них
    /// покупали в каждом из прошлых месяцев окна (otherMonths — точки-покупатели месяца).
    /// </summary>
    public static SystemQualityRow Quality(
        string id,
        string name,
        IReadOnlyList<SaleLine> lines,
        IReadOnlySet<long> core,
        IReadOnlyList<IReadOnlySet<long>> otherMonths,
        Func<long?, long?> category,
        IReadOnlyList<SystemQualityRow> agents)
    {
        var marketKg = SalesMath.MarketKg(lines);
        var akb = marketKg.Where(m => m.Value > 0).Select(m => m.Key).ToHashSet();
        var skus = lines
            .Where(l => l.MarketId is { } m && akb.Contains(m) && l.ProductId != null && !SalesOptions.IsChannelBranch(l.BranchId))
            .GroupBy(l => (Market: l.MarketId!.Value, Product: l.ProductId!.Value))
            .Where(g => g.Sum(l => l.Kg) > 0)
            .GroupBy(g => g.Key.Market, g => g.Key.Product)
            .ToDictionary(g => g.Key, g => g.ToList());
        var counts = akb.Select(m => skus.GetValueOrDefault(m) ?? []).ToList();

        var top = akb.OrderByDescending(m => marketKg[m]).ThenBy(m => m).Take(akb.Count == 0 ? 0 : Math.Max(1, (int)Math.Round(akb.Count * TopShare, MidpointRounding.AwayFromZero))).ToList();
        var akbKg = akb.Sum(m => marketKg[m]);

        return new SystemQualityRow(
            id,
            name,
            akb.Count,
            lines.Sum(l => l.Kg),
            akb.Count == 0 ? null : Math.Round((decimal)counts.Sum(c => c.Count) / akb.Count, 1),
            counts.Count(c => c.Count == 1),
            counts.Count(c => c.Count(core.Contains) >= CoreMin),
            counts.Count(c => c.Select(p => category(p)).OfType<long>().Distinct().Count() >= 2),
            akbKg > 0 ? top.Sum(m => marketKg[m]) / akbKg : null,
            top.Count,
            top.Count(m => otherMonths.All(month => month.Contains(m))),
            agents);
    }

    /// <summary>Долг по корзинам старения: остаток неоплаченной отгрузки — в корзину её возраста (дней от отгрузки до даты данных).</summary>
    public static IReadOnlyList<decimal> Aging(IEnumerable<(int Age, decimal Remaining)> open)
    {
        var list = open.ToList();
        return Buckets.Select(b => list.Where(o => o.Age >= b.From && (b.To is not { } to || o.Age <= to)).Sum(o => o.Remaining)).ToList();
    }

    /// <summary>Резерв по шкале: сумма корзин × доля резерва корзины.</summary>
    public static decimal Reserve(IReadOnlyList<decimal> aging) => Math.Round(aging.Select((v, i) => v * Buckets[i].ReserveShare).Sum());

    /// <summary>
    /// Запас дилера в днях его продаж: остаток (кг) ÷ вторичка региона за прошедшие дни месяца на день. Остаток на 1-е — остаток сейчас плюс
    /// проданное с 1-го минус отгруженное заводом с 1-го (первичка нетто): Linko истории остатков не отдаёт, это восстановление назад,
    /// как в «Аутстоке».
    /// </summary>
    public static (decimal? Stock1, decimal? DaysNow, decimal? Days1, bool Overstock) Stock(decimal? stockNow, decimal? primaryKg, decimal? secondaryKg, decimal elapsedDays)
    {
        if (stockNow is not { } now || secondaryKg is not { } sec)
        {
            return (null, null, null, false);
        }

        var stock1 = primaryKg is { } prim ? Math.Max(0, now + sec - prim) : (decimal?)null;
        var perDay = elapsedDays > 0 && sec > 0 ? sec / elapsedDays : 0;
        decimal? Days(decimal? kg) => kg is { } k && perDay > 0 ? Math.Round(k / perDay, 1) : null;
        var (daysNow, days1) = (Days(now), Days(stock1));
        var overstock = primaryKg is { } p && sec > 0 && p > OverstockRatio * sec && daysNow is { } dn && days1 is { } d1 && dn > d1;
        return (stock1, daysNow, days1, overstock);
    }
}
