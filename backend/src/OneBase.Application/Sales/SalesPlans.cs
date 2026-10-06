using OneBase.Application.Sales.Metrics;
using OneBase.Domain.Sales;

namespace OneBase.Application.Sales;

/// <summary>Строка sales."RegionPlans": план региона, кг за месяц; CategoryId — тип товара Linko (null — план без разбивки по категориям).</summary>
public sealed record RegionPlanRow(Guid RegionId, PlanKind Kind, int Year, int Month, long? CategoryId, decimal PlanKg);

/// <summary>
/// План вторички за месяц. Source — откуда план подразделений (PlanSources). Plans — план месяца: планы регионов выбранного вида (если есть)
/// и планы ТП из Linko (по ним карточка ТП, команда региона и состав месяца); YearRegionPlans — планы регионов выбранного вида на весь год
/// («План и факт по месяцам»); NextRegionPlans — на следующий месяц.
/// </summary>
public sealed record MonthPlans(string Source, IReadOnlyList<PlanRow> Plans, IReadOnlyList<PlanRow> YearRegionPlans, IReadOnlyList<PlanRow> NextRegionPlans);

/// <summary>
/// План вторички — «План РОП» (по умолчанию) или «План «Завод»»: планы регионов × категорий × месяцев из sales."RegionPlans"
/// (переключатель plan=rop|factory). Если на месяц планов регионов выбранного вида нет — план подразделений из планов ТП в Linko
/// (staff_balance), и ответ это показывает (PlanSources.Linko). Планы двух источников в одном месяце не смешиваются.
/// </summary>
public static class SalesPlans
{
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

        return monthPlans.Count > 0
            ? new MonthPlans(SourceOf(selected), monthPlans.Concat(agentPlans).ToList(), yearPlans, nextPlans)
            : new MonthPlans(PlanSources.Linko, agentPlans, yearPlans, nextPlans);
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
