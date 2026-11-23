namespace OneBase.Application.Sales.Metrics;

public sealed partial class SalesAnalytics
{
    /// <summary>
    /// «Из чего складывается объём» региона (как в «Полевом контроле», с v500): по категориям — доля в кг региона, план и факт в кг, выполнение,
    /// прогноз на конец месяца по темпу и прогноз к плану, план АКБ категории, АКБ категории и охват АКБ (АКБ категории ÷ АКБ региона).
    /// План категории — тот же, что в «Плане и факте по категориям» (РОП / «Завод» региона, без них — планы ТП из Linko); факт — все продажи
    /// категории в регионе. Планы Linko не на одну категорию (остаток «Тотала», «Могуль + Шоколад») — отдельными строками PlanGroups.
    /// Итог — регион целиком: весь факт и АКБ региона, план региона, сумма планов АКБ его ТП. null — в регионе ни продаж, ни плана.
    /// </summary>
    private VolumeTable? VolumeOf(IReadOnlySet<Guid> scope, IReadOnlyList<CategoryPlanFact> categoryPlans, KpiTiles kpi)
    {
        var lines = Lines(scope).ToList();
        if (lines.Count == 0 && kpi.PlanKg is null)
        {
            return null;
        }

        var totalKg = lines.Sum(l => l.Kg);
        var totalAkb = SalesMath.Akb(lines);
        var agents = AgentsIn(scope).ToHashSet();
        var akbPlans = _d.AkbPlans.Where(p => p.Group is not null && agents.Contains(p.AgentId))
            .GroupBy(p => p.Group!.Value).ToDictionary(g => g.Key, g => g.Sum(p => p.Plan));
        var plans = categoryPlans.Where(p => p.CategoryId is not null && p.PlanKg is not null)
            .GroupBy(p => p.CategoryId!.Value).ToDictionary(g => g.Key, g => g.Sum(p => p.PlanKg!.Value));
        var byGroup = lines.ToLookup(l => GroupOf(l.CategoryId));

        VolumeRow Row(long? id, string name, decimal fact, decimal? plan, int? akb, decimal? akbPlan)
        {
            var forecast = ForecastOf(fact);
            var execution = plan is > 0 ? SalesMath.Ratio(fact, plan) : null;
            var forecastExecution = plan is > 0 && forecast is { } f ? SalesMath.Ratio(f, plan) : null;
            return new VolumeRow(id, name, SalesMath.Ratio(fact, totalKg), plan, fact, execution, forecast, forecastExecution, akbPlan, akb ?? 0,
                akb is { } a && totalAkb > 0 ? (decimal)a / totalAkb : null, SalesMath.ExecutionLevelOf(execution), SalesMath.ForecastLevelOf(forecastExecution));
        }

        var rows = byGroup.Select(g => g.Key)
            .Concat(plans.Keys.Select(k => (long?)k))
            .Concat(akbPlans.Keys.Select(k => (long?)k))
            .Distinct()
            .Select(group =>
            {
                var categoryLines = byGroup[group].ToList();
                return Row(group, _cats.NameOf(group), categoryLines.Sum(l => l.Kg),
                    group is { } c && plans.TryGetValue(c, out var plan) ? plan : null,
                    SalesMath.Akb(categoryLines),
                    group is { } k && akbPlans.TryGetValue(k, out var akbPlan) ? akbPlan : null);
            })
            .Where(r => r.FactKg != 0 || r.PlanKg is not null || r.Akb > 0 || r.AkbPlan is not null)
            .OrderBy(r => r.CategoryId is null)
            .ThenByDescending(r => r.FactKg)
            .ThenByDescending(r => r.PlanKg ?? 0)
            .ToList();

        // Остаток «Тотала» и планы на несколько категорий: факт — все продажи этих категорий в регионе (ScopeFactKg), АКБ по ним не считается.
        var groups = categoryPlans.Where(p => p.CategoryId is null && p.PlanKg is not null)
            .Select(p => Row(null, p.Name, p.ScopeFactKg ?? p.FactKg, p.PlanKg, null, null))
            .ToList();

        var total = Row(null, "Итого", totalKg, kpi.PlanKg, totalAkb, kpi.AkbPlan?.Plan);
        return new VolumeTable(rows, groups, total, kpi.PlanSource);
    }
}
