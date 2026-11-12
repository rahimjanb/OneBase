using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;
using OneBase.Domain.Sales;

namespace OneBase.Application.Analytik.Semantic;

/// <summary>Строка плана месяца: регион (план регионов) или ТП (план Linko), группа (категория), кг, сумма, источник.</summary>
public sealed record PlanFactRow(DateOnly Month, Guid? Region, long? Agent, long? Group, decimal? Kg, decimal? Revenue, string Source, decimal? PlanFactKg = null);

/// <summary>Планы периода: строки, месяцы по источникам, заметки (что не разложить и почему).</summary>
public sealed record PlanFacts(IReadOnlyList<PlanFactRow> Rows, IReadOnlyList<DateOnly> RegionMonths, IReadOnlyList<DateOnly> AgentMonths,
    IReadOnlyList<DateOnly> EmptyMonths, IReadOnlyList<string> Notes);

/// <summary>
/// Планы — помесячные, по дням не делятся. Выбор источника по месяцу — как SalesPlans.Select «Продаж»: есть планы регионов выбранного вида
/// (Google Sheets / AI Data Manager → sales.RegionPlans) — они; иначе сумма планов ТП из Linko (staff_balance: «Тотал» или сумма показателей,
/// без командных планов СВР и исключённых должностей). Источники не складываются; настройка запроса planSource может выбрать один явно.
/// </summary>
public sealed class SemanticPlans(IAppDbContext db, SalesOptions options, SalesDataLoader loader)
{
    public const string RegionSource = "Google Sheets / AI Data Manager (sales.RegionPlans)";
    public const string AgentSource = "Linko: планы ТП (staff_balance)";

    /// <summary>
    /// regionLevel — срез по регионам (или без измерений): план и «факт для плана» каждого региона берутся из месяца «Продаж» (SalesAnalytics,
    /// тот же кэш и те же правила: планы регионов, «Завод» с раскладкой по РОП, а в месяцы без планов регионов — планы ТП Linko по региону ТП).
    /// </summary>
    public async Task<PlanFacts> LoadAsync(SemanticContext ctx, DateOnly from, DateOnly to, string source, PlanKind kind, bool byGroup,
        SalesRowScope scope, CancellationToken ct, bool regionLevel = false)
    {
        if (regionLevel && !byGroup && source != "agents")
        {
            return await RegionLevelAsync(from, to, source, kind, scope, ct);
        }

        var first = new DateOnly(from.Year, from.Month, 1);
        var last = new DateOnly(to.Year, to.Month, 1);
        var months = new List<DateOnly>();
        for (var m = first; m <= last; m = m.AddMonths(1))
        {
            months.Add(m);
        }

        var keys = months.Select(m => m.Year * 100 + m.Month).ToList();
        var regionPlans = await db.SalesRegionPlans.AsNoTracking()
            .Where(p => p.Kind == kind && keys.Contains(p.Year * 100 + p.Month))
            .Select(p => new { p.RegionId, p.Year, p.Month, p.CategoryId, p.PlanKg })
            .ToListAsync(ct);
        var notes = new List<string>();
        var rows = new List<PlanFactRow>();
        var regionMonths = new List<DateOnly>();
        var agentMonths = new List<DateOnly>();
        var empty = new List<DateOnly>();

        var staff = source == "regions" ? new List<SalesStaffPlan>() : await db.SalesStaffPlans.AsNoTracking()
            .Where(p => keys.Contains(p.Year * 100 + p.Month) && (p.PlanType == StaffPlanTypes.SalesWeight || p.PlanType == StaffPlanTypes.SalesSum))
            .ToListAsync(ct);
        var staffIds = staff.Select(s => s.LinkoUserId).Distinct().ToList();
        var excludedStaff = (await db.LinkoUsers.AsNoTracking().Where(u => staffIds.Contains(u.Id)).Select(u => new { u.Id, u.JobName }).ToListAsync(ct))
            .Where(u => u.JobName is { } job && options.StaffPlanExcludeJobs.Any(x => job.Contains(x, StringComparison.OrdinalIgnoreCase)))
            .Select(u => u.Id)
            .ToHashSet();
        var supervisorMonths = staff.Where(s => KpiMath.IsSupervisorIndicator(s.IndicatorName)).Select(s => (s.LinkoUserId, s.Year, s.Month)).ToHashSet();

        foreach (var month in months)
        {
            var monthRegion = regionPlans.Where(p => p.Year == month.Year && p.Month == month.Month).ToList();
            var useRegions = source switch
            {
                "regions" => true,
                "agents" => false,
                _ => monthRegion.Count > 0,
            };
            if (useRegions)
            {
                if (monthRegion.Count == 0)
                {
                    empty.Add(month);
                    continue;
                }

                regionMonths.Add(month);
                if (scope.Level == SalesRowScope.AgentLevel)
                {
                    continue; // у ТП нет доступа к планам регионов
                }

                // Регион старого филиала — в текущем; регион вне вторички («Завод») — не в планах вторички.
                var mapped = monthRegion
                    .Select(p => (Region: ctx.Structure.RegionOf(p.RegionId), p.CategoryId, p.PlanKg))
                    .Where(p => p.Region is not null && (scope.Level != SalesRowScope.RegionLevel || scope.RegionIds.Contains(p.Region.Value)))
                    .ToList();
                foreach (var region in mapped.GroupBy(p => p.Region!.Value))
                {
                    var total = region.Where(p => p.CategoryId is null).Sum(p => p.PlanKg);
                    var categories = region.Where(p => p.CategoryId is not null).ToList();
                    if (byGroup)
                    {
                        if (categories.Count == 0)
                        {
                            rows.Add(new PlanFactRow(month, region.Key, null, null, total, null, RegionSource));
                        }

                        foreach (var g in categories.GroupBy(p => ctx.GroupOf(p.CategoryId)))
                        {
                            rows.Add(new PlanFactRow(month, region.Key, null, g.Key, g.Sum(p => p.PlanKg), null, RegionSource));
                        }
                    }
                    else
                    {
                        // Итог региона — строка без категории, иначе сумма категорий (SalesMath.PlanTotal).
                        var kg = region.Any(p => p.CategoryId is null) ? total : categories.Sum(p => p.PlanKg);
                        rows.Add(new PlanFactRow(month, region.Key, null, null, kg, null, RegionSource));
                    }
                }

                continue;
            }

            var monthStaff = staff.Where(s => s.Year == month.Year && s.Month == month.Month
                                              && !excludedStaff.Contains(s.LinkoUserId) && !supervisorMonths.Contains((s.LinkoUserId, s.Year, s.Month))).ToList();
            if (monthStaff.Count == 0)
            {
                empty.Add(month);
                continue;
            }

            agentMonths.Add(month);
            if (scope.Level == SalesRowScope.RegionLevel)
            {
                continue; // регион ТП (по выручке) в планах Linko не задан — область регионов к ним не применить
            }

            foreach (var agent in monthStaff.GroupBy(s => s.LinkoUserId))
            {
                if (scope.Level == SalesRowScope.AgentLevel && agent.Key != scope.AgentId)
                {
                    continue;
                }

                decimal? Part(string type)
                {
                    var list = agent.Where(s => s.PlanType == type).ToList();
                    return list.Count == 0 ? null : KpiMath.PlanParts(list, s => s.IndicatorName).Sum(s => s.PlanAmount);
                }

                rows.Add(new PlanFactRow(month, null, agent.Key, null, Part(StaffPlanTypes.SalesWeight), Part(StaffPlanTypes.SalesSum), AgentSource));
            }
        }

        if (scope.Level == SalesRowScope.AgentLevel && regionMonths.Count > 0)
        {
            notes.Add("Планы регионов видны руководителям регионов; у ТП — только свой план из Linko.");
        }

        if (scope.Level == SalesRowScope.RegionLevel && agentMonths.Count > 0)
        {
            notes.Add("В месяцы без планов регионов план — сумма планов ТП Linko, а регион ТП в Linko не задан: по области регионов такой план не показан.");
        }

        return new PlanFacts(rows, regionMonths, agentMonths, empty, notes);
    }

    private async Task<PlanFacts> RegionLevelAsync(DateOnly from, DateOnly to, string source, PlanKind kind, SalesRowScope scope, CancellationToken ct)
    {
        var rows = new List<PlanFactRow>();
        var regionMonths = new List<DateOnly>();
        var agentMonths = new List<DateOnly>();
        var empty = new List<DateOnly>();
        var notes = new List<string>();
        for (var m = new DateOnly(from.Year, from.Month, 1); m <= to; m = m.AddMonths(1))
        {
            var analytics = await loader.LoadAsync(m.Year, m.Month, kind, ct);
            var facts = analytics.RegionPlanFacts().Where(f => f.PlanKg is not null).ToList();
            var linko = facts.Count > 0 && facts.All(f => f.PlanSource == PlanSources.Linko);
            if (facts.Count == 0 || (source == "regions" && linko))
            {
                empty.Add(m);
                continue;
            }

            (linko ? agentMonths : regionMonths).Add(m);
            if (scope.Level == SalesRowScope.AgentLevel)
            {
                continue;
            }

            foreach (var f in facts.Where(f => scope.Level != SalesRowScope.RegionLevel || scope.RegionIds.Contains(f.RegionId)))
            {
                rows.Add(new PlanFactRow(m, f.RegionId, null, null, f.PlanKg, null, linko ? AgentRegionSource : RegionSource, f.PlanFactKg));
            }
        }

        if (agentMonths.Count > 0)
        {
            notes.Add("В месяцы без планов регионов план региона — сумма планов ТП Linko по региону ТП (где у ТП больше выручки), а «факт для плана» — " +
                      "продажи этих ТП, как в «Продажах».");
        }

        if (scope.Level == SalesRowScope.AgentLevel)
        {
            notes.Add("Планы регионов видны руководителям регионов; у ТП — только свой план из Linko.");
        }

        return new PlanFacts(rows, regionMonths, agentMonths, empty, notes);
    }

    public const string AgentRegionSource = "Linko: планы ТП по региону ТП (staff_balance)";
}
