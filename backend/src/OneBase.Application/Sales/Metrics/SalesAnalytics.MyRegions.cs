namespace OneBase.Application.Sales.Metrics;

/// <summary>
/// «Мои регионы» РМ и супервайзера: все регионы его охвата одним экраном — те же блоки, что у страницы направления (выполнение плана и KPI
/// по сумме регионов, карточки и сравнение регионов, календари, категории, «ещё не купили»). У СВР и РМ бывает несколько регионов; раньше
/// своей страницей был один регион (первый по имени) или одно направление.
/// </summary>
public sealed partial class SalesAnalytics
{
    public GroupView CachedMyRegions(IReadOnlyCollection<Guid> ids, int? from, int? to, string metric = "kg", long? category = null)
    {
        var set = ids.Where(id => id != NoRegionId && _regions.ContainsKey(id)).Order().ToList();
        var (f, t) = VisitRange(from, to);
        return View($"my-regions:{string.Join(",", set)}:{f}:{t}:{metric}:{category}", () => MyRegions(set, f, t, metric, category));
    }

    public GroupView MyRegions(IReadOnlyList<Guid> ids, DateOnly visitsFrom, DateOnly visitsTo, string calendarMetric = "kg", long? calendarCategory = null)
    {
        var cards = ids.Select(RegionUnit).OrderBy(c => c.Name).ToList();
        var subtitle = $"{ids.Count} {SalesFormat.Plural(ids.Count, "регион", "региона", "регионов")}";
        return Group("Мои регионы", subtitle, ids, cards, visitsFrom, visitsTo, calendarMetric, calendarCategory);
    }
}
