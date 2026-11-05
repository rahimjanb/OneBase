using OneBase.Application.Sales.Metrics;
using OneBase.Domain.Sales;

namespace OneBase.Application.Sales;

/// <summary>Строка sales."RegionPlans": план региона, кг за месяц; CategoryId — тип товара Linko (null — план без разбивки по категориям).</summary>
public sealed record RegionPlanRow(Guid RegionId, PlanKind Kind, int Year, int Month, long? CategoryId, decimal PlanKg);

/// <summary>Строка sales."FactoryPlans": план «Завод» на всю страну — кг категории отчёта за месяц (CategoryId — как в RegionPlans).</summary>
public sealed record FactoryTotalRow(int Year, int Month, long CategoryId, decimal PlanKg);

/// <summary>
/// План «Завод» месяца по регионам из итогов страны. Explicit — на месяц план «Завод» уже заведён по регионам (таблица, Data Manager): он
/// важнее итогов, раскладки нет. BaseYear/BaseMonth — месяц плана РОП, по долям которого разложено (свой месяц, а без него — последний
/// прошлый с планом РОП); null — раскладывать не по чему. Rows — планы регионов вторички вида «Завод».
/// </summary>
public sealed record FactoryCascade(int Year, int Month, bool Explicit, int? BaseYear, int? BaseMonth, IReadOnlyList<RegionPlanRow> Rows);

/// <summary>
/// План вторички за месяц. Source — откуда план подразделений (PlanSources). Plans — план месяца: планы регионов выбранного вида (если есть)
/// и планы ТП из Linko (по ним карточка ТП, команда региона и состав месяца); YearRegionPlans — планы регионов выбранного вида на весь год
/// («План и факт по месяцам»); NextRegionPlans — на следующий месяц. AltSource и AltPlans — второй план регионов того же месяца (РОП при
/// выбранном «Заводе» и наоборот): стоит рядом с выбранным («план РОП: 82,4% из 400 000 кг»), в расчёт выполнения не входит.
/// </summary>
public sealed record MonthPlans(
    string Source,
    IReadOnlyList<PlanRow> Plans,
    IReadOnlyList<PlanRow> YearRegionPlans,
    IReadOnlyList<PlanRow> NextRegionPlans,
    string? AltSource = null,
    IReadOnlyList<PlanRow>? AltPlans = null);

/// <summary>
/// План вторички — «План РОП» (по умолчанию) или «План «Завод»»: планы регионов × категорий × месяцев из sales."RegionPlans"
/// (переключатель plan=rop|factory). Если на месяц планов регионов выбранного вида нет — план подразделений из планов ТП в Linko
/// (staff_balance), и ответ это показывает (PlanSources.Linko). Планы двух источников в одном месяце не смешиваются.
/// План «Завод» завод задаёт итогом страны по категориям (sales."FactoryPlans", «Настройки продаж»): регионам он раскладывается по их доле в
/// плане РОП той же категории (WithFactory) — так, как его считает «Полевой контроль».
/// </summary>
public static class SalesPlans
{
    /// <summary>
    /// Планы регионов вместе с планом «Завод», разложенным из итогов страны, — на каждый месяц итогов, у которого плана «Завод» по регионам нет.
    /// Остальные строки не меняются; дальше их берут Select и Available как обычные строки «Завода».
    /// </summary>
    public static IReadOnlyList<RegionPlanRow> WithFactory(
        IReadOnlyList<RegionPlanRow> regionPlans, IReadOnlyList<FactoryTotalRow> totals, Func<Guid, Guid?> regionOf)
    {
        if (totals.Count == 0)
        {
            return regionPlans;
        }

        var result = regionPlans.ToList();
        foreach (var (year, month) in totals.Select(t => (t.Year, t.Month)).Distinct())
        {
            result.AddRange(Cascade(year, month, totals, regionPlans, regionOf).Rows);
        }

        return result;
    }

    /// <summary>
    /// Раскладка плана «Завод» месяца по регионам. Итог категории делится между регионами вторички пропорционально их плану РОП в этой
    /// категории (у категории без плана РОП — пропорционально всему плану РОП региона), в целых кг методом наибольшего остатка: сумма
    /// регионов равна итогу завода. План РОП — того же месяца, а пока его нет — последнего прошлого месяца, где он есть (среди regionPlans).
    /// Регион без плана РОП плана «Завод» не получает (его факт идёт в республику без плана, как у РОП). Сверено с «Полевым контролем»:
    /// октябрь 2026 — все 133 клетки регион × категория.
    /// </summary>
    public static FactoryCascade Cascade(
        int year, int month, IReadOnlyList<FactoryTotalRow> totals, IReadOnlyList<RegionPlanRow> regionPlans, Func<Guid, Guid?> regionOf)
    {
        var secondary = regionPlans
            .Select(p => (Plan: p, Region: regionOf(p.RegionId)))
            .Where(x => x.Region is not null)
            .ToList();
        if (secondary.Any(x => x.Plan.Kind == PlanKind.Factory && x.Plan.Year == year && x.Plan.Month == month))
        {
            return new FactoryCascade(year, month, true, null, null, []);
        }

        var monthTotals = totals.Where(t => t.Year == year && t.Month == month && t.PlanKg > 0).ToList();
        var key = year * 12 + month;
        var baseKey = secondary
            .Where(x => x.Plan.Kind == PlanKind.Rop && x.Plan.PlanKg > 0 && x.Plan.Year * 12 + x.Plan.Month <= key)
            .Select(x => (int?)(x.Plan.Year * 12 + x.Plan.Month))
            .Max();
        if (monthTotals.Count == 0 || baseKey is not { } b)
        {
            return new FactoryCascade(year, month, false, null, null, []);
        }

        var (baseYear, baseMonth) = ((b - 1) / 12, (b - 1) % 12 + 1);
        var rop = secondary
            .Where(x => x.Plan.Kind == PlanKind.Rop && x.Plan.Year == baseYear && x.Plan.Month == baseMonth)
            .Select(x => (Region: x.Region!.Value, x.Plan.CategoryId, x.Plan.PlanKg))
            .ToList();
        var regions = rop.Select(x => x.Region).Distinct().OrderBy(r => r).ToList();
        var regionTotals = regions.Select(r => rop.Where(x => x.Region == r).Sum(x => x.PlanKg)).ToList();

        var rows = new List<RegionPlanRow>();
        foreach (var total in monthTotals.GroupBy(t => t.CategoryId))
        {
            var weights = regions.Select(r => rop.Where(x => x.Region == r && x.CategoryId == total.Key).Sum(x => x.PlanKg)).ToList();
            if (weights.Sum() <= 0)
            {
                weights = regionTotals;
            }

            var shares = Allocate(total.Sum(t => t.PlanKg), weights);
            rows.AddRange(regions.Select((r, i) => new RegionPlanRow(r, PlanKind.Factory, year, month, total.Key, shares[i])).Where(p => p.PlanKg > 0));
        }

        return new FactoryCascade(year, month, false, baseYear, baseMonth, rows);
    }

    /// <summary>
    /// Делит total пропорционально весам в целых кг: каждому — целая часть доли, оставшиеся кг — по одному тем, у кого дробная часть больше
    /// (при равной — больший вес). Дробный остаток итога (итог не в целых кг) — региону с наибольшим весом. Сумма долей равна total.
    /// </summary>
    public static IReadOnlyList<decimal> Allocate(decimal total, IReadOnlyList<decimal> weights)
    {
        var sum = weights.Sum();
        if (sum <= 0)
        {
            return weights.Select(_ => 0m).ToList();
        }

        var raw = weights.Select(w => total * w / sum).ToList();
        var shares = raw.Select(Math.Floor).ToList();
        var left = total - shares.Sum();
        foreach (var i in Enumerable.Range(0, raw.Count).OrderByDescending(i => raw[i] - shares[i]).ThenByDescending(i => weights[i]).ThenBy(i => i))
        {
            if (left < 1)
            {
                break;
            }

            shares[i] += 1;
            left -= 1;
        }

        if (left > 0)
        {
            var top = Enumerable.Range(0, weights.Count).OrderByDescending(i => weights[i]).First();
            shares[top] += left;
        }

        return shares;
    }

    /// <summary>Вид плана вторички: «Завод» или РОП. Плана первички во вторичке нет — вместо него РОП.</summary>
    public static PlanKind Secondary(PlanKind kind) => kind == PlanKind.Factory ? PlanKind.Factory : PlanKind.Rop;

    /// <summary>
    /// Вид плана из параметра адреса (plan=…): «factory» без учёта регистра — «Завод», всё остальное (rop, пусто, неизвестное значение) —
    /// РОП, по умолчанию. Неизвестное значение не ошибка: ссылка со старым или испорченным параметром открывает план РОП.
    /// </summary>
    public static PlanKind Parse(string? plan) =>
        string.Equals(plan?.Trim(), PlanSources.Factory, StringComparison.OrdinalIgnoreCase) ? PlanKind.Factory : PlanKind.Rop;

    /// <summary>Ключ кэша расчёта месяца: вид плана — часть ключа, РОП и «Завод» считаются и хранятся отдельно.</summary>
    public static string CacheKey(int year, int month, PlanKind kind) => $"sales:{year}-{month}:{Secondary(kind)}";

    /// <summary>Вид плана для ответа API: rop или factory.</summary>
    public static string SourceOf(PlanKind kind) => Secondary(kind) == PlanKind.Factory ? PlanSources.Factory : PlanSources.Rop;

    /// <summary>
    /// Планы месяца. regionPlans — строки sales."RegionPlans" за год месяца и следующий месяц (строки другого вида отбрасываются);
    /// regionOf — регион вторички, в котором считается строка (старый филиал — в текущем регионе; null — регион вне вторички:
    /// «Завод», «К К Мерч»); agentPlans — планы ТП из Linko на месяц.
    /// </summary>
    public static MonthPlans Select(
        int year,
        int month,
        PlanKind kind,
        IEnumerable<RegionPlanRow> regionPlans,
        Func<Guid, Guid?> regionOf,
        IReadOnlyList<PlanRow> agentPlans)
    {
        var selected = Secondary(kind);
        var next = new DateOnly(year, month, 1).AddMonths(1);
        var rows = regionPlans
            .Where(p => p.Kind == selected)
            .Select(p => (Plan: p, Region: regionOf(p.RegionId)))
            .Where(x => x.Region is not null)
            .Select(x => (x.Plan.Year, Row: new PlanRow(x.Region, null, x.Plan.Month, x.Plan.CategoryId, x.Plan.PlanKg)))
            .ToList();

        var yearPlans = rows.Where(x => x.Year == year).Select(x => x.Row).ToList();
        var nextPlans = rows.Where(x => x.Year == next.Year && x.Row.Month == next.Month).Select(x => x.Row).ToList();
        var monthPlans = yearPlans.Where(p => p.Month == month).ToList();

        var other = selected == PlanKind.Factory ? PlanKind.Rop : PlanKind.Factory;
        var altPlans = regionPlans
            .Where(p => p.Kind == other && p.Year == year && p.Month == month)
            .Select(p => (Plan: p, Region: regionOf(p.RegionId)))
            .Where(x => x.Region is not null)
            .Select(x => new PlanRow(x.Region, null, month, x.Plan.CategoryId, x.Plan.PlanKg))
            .ToList();
        var altSource = altPlans.Count > 0 ? SourceOf(other) : null;

        return monthPlans.Count > 0
            ? new MonthPlans(SourceOf(selected), monthPlans.Concat(agentPlans).ToList(), yearPlans, nextPlans, altSource, altPlans)
            : new MonthPlans(PlanSources.Linko, agentPlans, yearPlans, nextPlans, altSource, altPlans);
    }

    /// <summary>
    /// Какие планы регионов заведены на месяц: rop и factory (в этом порядке), если у вида есть строка региона вторички за этот месяц —
    /// те же строки, что взял бы Select. Переключатель плана не предлагает вид, которого на месяц нет: выбрав его, получили бы планы ТП из Linko.
    /// </summary>
    public static IReadOnlyList<string> Available(int year, int month, IEnumerable<RegionPlanRow> regionPlans, Func<Guid, Guid?> regionOf)
    {
        var kinds = regionPlans
            .Where(p => p.Year == year && p.Month == month && (p.Kind is PlanKind.Rop or PlanKind.Factory) && regionOf(p.RegionId) is not null)
            .Select(p => p.Kind)
            .ToHashSet();
        return new[] { PlanKind.Rop, PlanKind.Factory }.Where(kinds.Contains).Select(SourceOf).ToList();
    }
}

/// <summary>Регион в плане «Завод» месяца: план «Завод» (разложенный или заведённый по регионам) и план РОП по категориям, кг.</summary>
public sealed record FactoryRegionRow(Guid Id, string Name, IReadOnlyDictionary<long, decimal> Factory, IReadOnlyDictionary<long, decimal> Rop);

/// <summary>
/// План «Завод» месяца для «Настроек продаж»: Totals — итоги страны по категориям (как задал завод); RopTotals — план РОП месяца по
/// категориям; Explicit — план «Завод» на месяц заведён по регионам (таблица, Data Manager) и итоги не используются; BaseYear/BaseMonth —
/// месяц плана РОП, по которому разложено (null — раскладывать не по чему); Regions — регионы вторички с планом.
/// </summary>
public sealed record FactoryPlanView(
    int Year,
    int Month,
    IReadOnlyDictionary<long, decimal> Totals,
    IReadOnlyDictionary<long, decimal> RopTotals,
    bool Explicit,
    int? BaseYear,
    int? BaseMonth,
    IReadOnlyList<FactoryRegionRow> Regions)
{
    public static FactoryPlanView Of(FactoryCascade cascade, IReadOnlyList<FactoryTotalRow> totals, IReadOnlyList<RegionPlanRow> rows, SalesStructureData structure)
    {
        var (year, month) = (cascade.Year, cascade.Month);
        var mapped = rows
            .Where(p => p.Year == year && p.Month == month && p.CategoryId is not null)
            .Select(p => (Plan: p, Region: structure.RegionOf(p.RegionId)))
            .Where(x => x.Region is not null)
            .Select(x => x.Plan with { RegionId = x.Region!.Value })
            .ToList();
        var rop = mapped.Where(p => p.Kind == PlanKind.Rop).ToList();
        var factory = cascade.Explicit ? mapped.Where(p => p.Kind == PlanKind.Factory).ToList() : cascade.Rows.ToList();

        static IReadOnlyDictionary<long, decimal> ByCategory(IEnumerable<RegionPlanRow> list) =>
            list.GroupBy(p => p.CategoryId!.Value).ToDictionary(g => g.Key, g => g.Sum(p => p.PlanKg));

        var regions = rop.Concat(factory).Select(p => p.RegionId).Distinct()
            .Select(id => new FactoryRegionRow(
                id,
                structure.Regions.FirstOrDefault(r => r.Id == id)?.Name ?? "—",
                ByCategory(factory.Where(p => p.RegionId == id)),
                ByCategory(rop.Where(p => p.RegionId == id))))
            .OrderBy(r => r.Name, StringComparer.CurrentCulture)
            .ToList();

        return new FactoryPlanView(
            year,
            month,
            totals.Where(t => t.Year == year && t.Month == month).GroupBy(t => t.CategoryId).ToDictionary(g => g.Key, g => g.Sum(t => t.PlanKg)),
            ByCategory(rop),
            cascade.Explicit,
            cascade.BaseYear,
            cascade.BaseMonth,
            regions);
    }
}
