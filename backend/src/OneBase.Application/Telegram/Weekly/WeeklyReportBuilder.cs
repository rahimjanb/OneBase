using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Sales.Promo;
using OneBase.Application.Sales.Settlements;
using OneBase.Application.Sales.Stock;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Telegram;

namespace OneBase.Application.Telegram.Weekly;

/// <summary>
/// Собирает еженедельный отчёт области (РОП — вся страна, РМ — направление, СВ — регион, ТП — один ТП) из расчётов OneBase: «Продажи»
/// (неделя по строкам факта — те же правила, что у месяца: доставленные заказы − возвраты по дате приёмки, без «Завода»; месяц, план,
/// категории, клиенты), KPI Linko, «Акции», «Остатки», «Аутсток», DSO. Ничего не выдумывает: нет данных — null и замечание.
/// Команда СВ — все ТП региона (решение 31.10.2026).
/// </summary>
public sealed class WeeklyReportBuilder(
    IAppDbContext db,
    SalesDataLoader loader,
    ILinkoSalesProvider linko,
    PromoService promo,
    StockService stock,
    OutstockService outstock,
    SettlementsService settlements,
    WeeklyReportOptions defaults,
    TimeProvider time)
{
    private readonly Dictionary<(int, int), SalesAnalytics> _months = [];

    /// <summary>Формулы отчёта: appsettings с настройками «Отчёты PDF» поверх — читаются при каждой сборке.</summary>
    private WeeklyReportOptions options = defaults;

    /// <summary>
    /// Отчёт за неделю, в которую входит through (по умолчанию — отчётный день «Продаж»; воскресенье — прошедшая неделя пн–сб).
    /// money — показывать деньги КПУ и DSO (право finance.read).
    /// </summary>
    public async Task<WeeklySupervisorReport> BuildAsync(ReportScope scope, DateOnly? through, bool money, CancellationToken ct)
    {
        options = defaults.With(await db.TelegramWeeklyPdfSettings.AsNoTracking().FirstOrDefaultAsync(ct));
        var latest = await MonthAsync(through, ct);
        var day = through is { } t && t < latest.Period.DataThrough ? t : latest.Period.DataThrough;
        var (weekFrom, weekTo) = WeeklyReportMath.Week(day);
        if (day > weekTo)
        {
            // Воскресенье (и позже) — неделя пн–сб уже прошла: отчёт по её субботу, месяц — месяц субботы (в воскресенье 1-го — ещё прошлый месяц).
            day = weekTo;
        }

        var current = await MonthAsync(day, ct);
        var dataThrough = day < current.Period.DataThrough ? day : current.Period.DataThrough;
        var holidays = await HolidaysAsync(weekFrom.AddDays(-45), weekTo.AddDays(45), ct);
        var calendar = WeeklyReportMath.Calendar(dataThrough, weekFrom, weekTo, holidays);
        var weekEnd = dataThrough < weekTo ? dataThrough : weekTo; // воскресенье в неделю пн–сб не входит

        var teams = current.CachedReportTeams();
        var resolved = ReportScopes.Resolve(scope, teams, current.ReportDirections(), s => s);
        var role = scope.Role == TelegramReportRoles.Rm && scope.DirectionId is null ? "rop" : scope.Role;
        var regionIds = resolved.Regions.Select(r => r.Id).ToList();
        IReadOnlyList<ReportPerson> agents = resolved.Agent is { } single ? [single] : resolved.Agents;
        var agentIds = agents.Select(a => a.Id).ToHashSet();

        var warnings = new List<WeeklyNote>();
        if (dataThrough < weekTo)
        {
            warnings.Add(WeeklyNote.Of("week-partial", ("done", calendar.WeekWorkDaysDone), ("total", calendar.WeekWorkDays), ("through", dataThrough.ToString("dd.MM"))));
        }

        // Строки факта: неделя, те же дни прошлой недели, месяц, прошлый месяц — по регионам области (у ТП — только его строки).
        var weekLines = await LinesAsync(weekFrom, weekEnd, regionIds, resolved.Agent?.Id, ct);
        var prevWeekLines = await LinesAsync(weekFrom.AddDays(-7), weekEnd.AddDays(-7), regionIds, resolved.Agent?.Id, ct);
        var monthStart = new DateOnly(dataThrough.Year, dataThrough.Month, 1);
        var monthLines = await LinesAsync(monthStart, dataThrough, regionIds, resolved.Agent?.Id, ct);
        var prevStart = monthStart.AddMonths(-1);
        var prevLines = await LinesAsync(prevStart, monthStart.AddDays(-1), regionIds, resolved.Agent?.Id, ct);
        var prevSameDay = SameWorkDay(prevStart, calendar.MonthWorkDaysDone, holidays);
        var prevSameLines = prevSameDay is { } psd ? prevLines.Where(l => l.Date <= psd).ToList() : null;
        var (fullFrom, fullTo) = WeeklyReportMath.LastFullWeek(dataThrough);
        var fullWeekLines = fullTo == weekTo && dataThrough >= weekTo ? weekLines : await LinesAsync(fullFrom, fullTo, regionIds, resolved.Agent?.Id, ct);

        var plans = (await linko.AgentPlansAsync(agentIds, dataThrough.Year, dataThrough.Month, ct)).ToDictionary(p => p.LinkoUserId);
        var regionViews = regionIds.Where(current.HasRegion).ToDictionary(id => id, id => current.CachedRegion(id, null, null, "kg", null));

        // Продажи области: месяц — плитки «Продаж» (РОП — республика, РМ — направление, СВ — регион, ТП — карточка ТП).
        var unit = MonthUnit(current, role, scope, resolved).Through(monthLines);
        var sales = Sales(weekLines, prevWeekLines, prevSameLines, unit, plans.Values.Where(p => agentIds.Contains(p.LinkoUserId)).Select(p => p.PlanAkb),
            weekFrom, weekEnd, calendar, holidays);

        var kpiView = current.CachedKpi(money);

        // Строки регионов (РОП и РМ; у СВ — его регион): у каждого свои строки факта, месяц — карточка региона «Продаж».
        var regionRows = new List<WeeklyRegionMetrics>();
        foreach (var r in resolved.Agent is null ? resolved.Regions.Where(r => regionViews.ContainsKey(r.Id)) : [])
        {
            var view = regionViews[r.Id];
            var ids = r.Agents.Select(a => a.Id).ToHashSet();
            var regionUnit = new MonthUnitData(view.Kpi.FactKg, view.Kpi.Revenue, view.Kpi.PlanKg, view.Kpi.PlanSource, view.Kpi.Akb, view.Months)
                .Through(await LinesAsync(monthStart, dataThrough, [r.Id], null, ct));
            var own = await LinesAsync(weekFrom, weekEnd, [r.Id], null, ct);
            var ownPrev = await LinesAsync(weekFrom.AddDays(-7), weekEnd.AddDays(-7), [r.Id], null, ct);
            var ownPrevSame = prevSameDay is { } sd ? await LinesAsync(prevStart, sd, [r.Id], null, ct) : null;
            var rs = Sales(own, ownPrev, ownPrevSame, regionUnit, plans.Values.Where(p => ids.Contains(p.LinkoUserId)).Select(p => p.PlanAkb), weekFrom, weekEnd, calendar,
                holidays);
            var kpiSv = kpiView.Regions.FirstOrDefault(k => k.Id == r.Id.ToString())?.People.FirstOrDefault(p => p.Role == "СВР")?.Name;
            regionRows.Add(new WeeklyRegionMetrics(r.Id, r.Name, view.DirectionName, r.Supervisor ?? view.Supervisor ?? kpiSv, view.Dealer, r.Agents.Count, rs));
        }

        // KPI Linko: люди области (ТП и СВР регионов).
        var kpiPeople = kpiView.Regions.Where(r => regionIds.Any(id => id.ToString() == r.Id)).SelectMany(r => r.People)
            .Where(p => resolved.Agent is null || p.Id == resolved.Agent.Id)
            .ToList();
        var kpi = new WeeklyKpiMetrics(kpiPeople.Count > 0, money, kpiView.LoadedAt, kpiPeople.Select(p => ToKpiPerson(p, calendar.MonthWorkDaysLeft)).ToList());
        if (kpiPeople.Count == 0)
        {
            warnings.Add(WeeklyNote.Of("kpi-missing"));
        }

        var team = regionViews.Values.SelectMany(v => v.Team).GroupBy(t => t.AgentId).ToDictionary(g => g.Key, g => g.First());
        var routes = await RouteRowsAsync(weekFrom, weekEnd, regionIds, ct);
        var employees = agents.Select(a => Employee(a, weekLines, prevWeekLines, monthLines, team.GetValueOrDefault(a.Id), plans.GetValueOrDefault(a.Id),
                routes.GetValueOrDefault(a.Id), kpi.People.FirstOrDefault(p => p.Id == a.Id), calendar))
            .OrderByDescending(e => e.Execution ?? -1).ThenByDescending(e => e.MonthKg)
            .ToList();

        var clients = await ClientsAsync(monthLines, prevLines, agents, dataThrough, ct);
        var products = Products(regionViews.Values, monthLines, agents, kpi, current);
        var promoMetrics = await PromoAsync(resolved, regionViews, fullFrom, fullTo, weekFrom, weekEnd, monthStart, dataThrough, ct);
        var (dealers, dealerSources) = await DealersAsync(regionViews, money, dataThrough, warnings, ct);
        warnings.Add(WeeklyNote.Of("dmp-missing"));
        var goals = await GoalsAsync(agents, team, plans, fullWeekLines, promoMetrics, clients, weekTo, dataThrough, holidays, ct);

        var sources = await SourcesAsync(kpiView.LoadedAt, promoMetrics.AsOf, dealerSources, ct);
        var stale = sources.FirstOrDefault(s => s.Key == WeeklySources.Orders);
        if (stale is { State: WeeklySourceStates.Stale })
        {
            warnings.Add(WeeklyNote.Of("orders-stale", ("asOf", stale.AsOf?.ToOffset(TimeSpan.FromHours(5)).ToString("dd.MM HH:mm"))));
        }

        var report = new WeeklySupervisorReport(
            $"weekly:{scope.Key}:{weekFrom:yyyy-MM-dd}",
            role,
            scope.Key,
            resolved.Agent?.Name ?? (role == "rop" ? "Республика" : resolved.Title),
            role is TelegramReportRoles.Sv or TelegramReportRoles.Tp
                ? resolved.Regions.FirstOrDefault()?.Supervisor ?? regionViews.Values.FirstOrDefault()?.Supervisor ?? kpi.People.FirstOrDefault(p => p.Role == TelegramReportRoles.Sv)?.Name
                  ?? kpiView.Regions.FirstOrDefault(k => regionIds.Any(id => id.ToString() == k.Id))?.People.FirstOrDefault(p => p.Role == "СВР")?.Name
                : null,
            weekFrom,
            weekTo,
            dataThrough,
            time.GetUtcNow(),
            sources.Where(s => s.Key is WeeklySources.Orders or WeeklySources.Visits).Select(s => s.AsOf).Where(a => a is not null).DefaultIfEmpty(null).Min(),
            calendar,
            sources,
            sales,
            regionRows,
            employees,
            clients,
            products,
            promoMetrics.Metrics,
            new WeeklyDmpMetrics(false),
            dealers,
            kpi,
            goals,
            Recommend(role, sales, regionRows, employees),
            warnings,
            resolved.Regions.Count == 1 ? resolved.Regions[0].Name : null);
        return report;
    }

    // ---------- Продажи ----------

    private sealed record MonthUnitData(decimal FactKg, decimal Revenue, decimal? PlanKg, string PlanSource, int Akb, IReadOnlyList<MonthPlanFact> Months)
    {
        /// <summary>Факт месяца по день отчёта (строки факта с 1-го по отчётный день): карточки «Продаж» считают по сегодняшний день.</summary>
        public MonthUnitData Through(IReadOnlyList<SaleLine> lines) => this with { FactKg = lines.Sum(l => l.Kg), Revenue = lines.Sum(l => l.Revenue), Akb = SalesMath.Akb(lines) };
    }

    private static MonthUnitData MonthUnit(SalesAnalytics current, string role, ReportScope scope, ResolvedScope resolved)
    {
        if (resolved.Agent is { } agent)
        {
            var view = current.CachedAgent(agent.Id);
            return new MonthUnitData(view.FactKg, view.Revenue, view.PlanKg, PlanSources.Linko, view.Akb, []);
        }

        if (role == TelegramReportRoles.Sv && resolved.Regions.Count == 1 && current.HasRegion(resolved.Regions[0].Id))
        {
            var view = current.CachedRegion(resolved.Regions[0].Id, null, null, "kg", null);
            return new MonthUnitData(view.Kpi.FactKg, view.Kpi.Revenue, view.Kpi.PlanKg, view.Kpi.PlanSource, view.Kpi.Akb, view.Months);
        }

        var group = scope.DirectionId is { } direction && current.HasDirection(direction.ToString())
            ? current.CachedDirection(direction.ToString(), null, null)
            : current.CachedRepublic(null, null);
        return new MonthUnitData(group.Kpi.FactKg, group.Kpi.Revenue, group.Kpi.PlanKg, group.Kpi.PlanSource, group.Kpi.Akb, []);
    }

    private static WeeklySalesMetrics Sales(IReadOnlyList<SaleLine> week, IReadOnlyList<SaleLine> prevWeek, IReadOnlyList<SaleLine>? prevSame, MonthUnitData month,
        IEnumerable<decimal?> akbPlans, DateOnly weekFrom, DateOnly weekEnd, WeeklyCalendar calendar, IReadOnlyDictionary<DateOnly, string> holidays)
    {
        var weekKg = week.Sum(l => l.Kg);
        var prevKg = prevWeek.Count == 0 ? (decimal?)null : prevWeek.Sum(l => l.Kg);
        var byDay = week.ToLookup(l => l.Date);
        var days = Enumerable.Range(0, 6).Select(i => weekFrom.AddDays(i)).Select(d => d > weekEnd
                ? new WeeklyDay(d, null, null, null, DailyPlanMath.IsWorkingDay(d, holidays))
                : new WeeklyDay(d, byDay[d].Sum(l => l.Kg), byDay[d].Sum(l => l.Revenue), SalesMath.OrderCount(byDay[d]), DailyPlanMath.IsWorkingDay(d, holidays)))
            .ToList();
        var akbPlanValues = akbPlans.Where(p => p is > 0).Select(p => p!.Value).ToList();
        decimal? akbPlan = akbPlanValues.Count == 0 ? null : akbPlanValues.Sum();
        var forecast = WeeklyReportMath.Forecast(month.FactKg, calendar.MonthWorkDaysDone, calendar.MonthWorkDays);
        var prevSameKg = prevSame?.Sum(l => l.Kg);
        return new WeeklySalesMetrics(
            weekKg,
            week.Sum(l => l.Revenue),
            SalesMath.OrderCount(week),
            SalesMath.Akb(week),
            prevKg,
            WeeklyReportMath.Change(weekKg, prevKg),
            days,
            month.PlanKg is > 0 && calendar.MonthWorkDays > 0 ? month.PlanKg / calendar.MonthWorkDays : null,
            month.FactKg,
            month.Revenue,
            month.PlanKg,
            WeeklyReportMath.Ratio(month.FactKg, month.PlanKg),
            forecast,
            WeeklyReportMath.Ratio(forecast, month.PlanKg),
            month.PlanSource,
            month.Akb,
            akbPlan,
            WeeklyReportMath.Ratio(month.Akb, akbPlan),
            month.Akb > 0 ? month.Revenue / month.Akb : null,
            prevSameKg,
            prevSameKg is { } p ? WeeklyReportMath.Change(month.FactKg, p) : null,
            calendar.MonthWorkDaysDone > 0 ? month.FactKg / calendar.MonthWorkDaysDone : null,
            WeeklyReportMath.Pace(month.PlanKg, 0.8m, month.FactKg, calendar.MonthWorkDaysLeft),
            WeeklyReportMath.Pace(month.PlanKg, 1m, month.FactKg, calendar.MonthWorkDaysLeft),
            month.Months);
    }

    // ---------- ТП ----------

    private sealed record RouteAgentWeek(int Plan, int DoneInPlan, int Done, int WithOrder);

    private async Task<Dictionary<long, RouteAgentWeek>> RouteRowsAsync(DateOnly from, DateOnly to, IReadOnlyList<Guid> regions, CancellationToken ct)
    {
        var result = new Dictionary<long, RouteAgentWeek>();
        foreach (var (partFrom, partTo) in Parts(from, to))
        {
            var analytics = await MonthAsync(partFrom, ct);
            foreach (var region in regions.Where(analytics.HasRegion))
            {
                foreach (var row in analytics.RouteSplit(partFrom, partTo, region).Regions.SelectMany(r => r.Agents))
                {
                    if (!long.TryParse(row.Id, out var id))
                    {
                        continue;
                    }

                    var was = result.GetValueOrDefault(id) ?? new RouteAgentWeek(0, 0, 0, 0);
                    result[id] = new RouteAgentWeek(was.Plan + row.Plan, was.DoneInPlan + row.DoneInPlan, was.Done + row.DoneInPlan + row.DoneOffPlan,
                        was.WithOrder + row.VisitsWithOrderInPlan + row.VisitsWithOrderOffPlan);
                }
            }
        }

        return result;
    }

    private static WeeklyEmployeeMetrics Employee(ReportPerson agent, IReadOnlyList<SaleLine> week, IReadOnlyList<SaleLine> prevWeek, IReadOnlyList<SaleLine> month,
        TeamRow? team, AgentMonthPlan? plan,
        RouteAgentWeek? route, WeeklyKpiPerson? kpi, WeeklyCalendar calendar)
    {
        var own = week.Where(l => l.AgentId == agent.Id).ToList();
        var prev = prevWeek.Where(l => l.AgentId == agent.Id).ToList();
        var weekKg = own.Sum(l => l.Kg);
        var prevKg = prev.Count == 0 ? (decimal?)null : prev.Sum(l => l.Kg);
        var orders = SalesMath.OrderCount(own);
        var visits = route?.Done ?? 0;
        var ownMonth = month.Where(l => l.AgentId == agent.Id).ToList();
        var monthKg = ownMonth.Sum(l => l.Kg);
        var planKg = team?.PlanKg ?? plan?.PlanKg;
        // «% плана» — из Linko: показатель «Тотал» KPI (кг, без него — сумма); прогноз — его факт по рабочим дням.
        var total = kpi?.Cells.FirstOrDefault(c => c.Label == "Тотал") ?? kpi?.Cells.FirstOrDefault(c => c.Label == "Тотал (сумма)");
        var forecast = total is null ? null : WeeklyReportMath.Forecast(total.Fact, calendar.MonthWorkDaysDone, calendar.MonthWorkDays);
        return new WeeklyEmployeeMetrics(agent.Id, agent.Name, weekKg, own.Sum(l => l.Revenue), prevKg, WeeklyReportMath.Change(weekKg, prevKg), orders, SalesMath.Akb(own),
            visits, visits > 0 ? (decimal)route!.WithOrder / visits : null, route?.Plan ?? 0, route?.DoneInPlan ?? 0,
            route is { Plan: > 0 } r ? (decimal)r.DoneInPlan / r.Plan : null, monthKg, planKg, total?.Pct,
            total is { Plan: > 0 } ? WeeklyReportMath.Ratio(forecast, total.Plan) : null, SalesMath.Akb(ownMonth), plan?.PlanAkb, kpi?.PremiumNow, kpi?.PremiumMax,
            total?.Plan, total?.Fact, total?.Unit);
    }

    // ---------- KPI ----------

    private WeeklyKpiPerson ToKpiPerson(KpiPerson p, int daysLeft)
    {
        var cells = p.Cells
            .Select(c => WeeklyReportMath.KpiCell(KpiLabel(c.Name, c.Unit), c.Unit, c.Plan, c.Fact, c.Pct, c.Threshold, c.Bonus100, c.PremiumNow, daysLeft,
                options.KpiRealisticGapPp))
            .OrderBy(c => KpiOrder(c.Label)).ThenBy(c => c.Label, StringComparer.Ordinal)
            .ToList();
        var max = p.Cells.Any(c => c.Bonus100 is not null) ? p.Cells.Sum(c => c.Bonus100 ?? 0) : (decimal?)null;
        return new WeeklyKpiPerson(p.Id, p.Name, p.Role == "СВР" ? TelegramReportRoles.Sv : TelegramReportRoles.Tp, p.PremiumNow, max, cells);
    }

    /// <summary>Метка показателя: «Тотал» в сумах — «Тотал (сумма)», остальные — как в разделе «KPI» (KpiMath.Label).</summary>
    private static string KpiLabel(string name, string unit) => KpiMath.Label(name) is var label && label == "Тотал" && unit == KpiMath.Sum ? "Тотал (сумма)" : KpiMath.Label(name);

    private static int KpiOrder(string label) => label switch { "Тотал (сумма)" => 0, "Тотал" => 1, "Могуль" => 2, "Twizos" => 3, "АКБ" => 4, _ => 5 };

    // ---------- Клиенты ----------

    private async Task<WeeklyClientMetrics> ClientsAsync(IReadOnlyList<SaleLine> month, IReadOnlyList<SaleLine> prev, IReadOnlyList<ReportPerson> agents, DateOnly day,
        CancellationToken ct)
    {
        static Dictionary<long, List<SaleLine>> ByMarket(IEnumerable<SaleLine> lines) =>
            lines.Where(l => l.MarketId != null && !SalesOptions.IsChannelBranch(l.BranchId)).GroupBy(l => l.MarketId!.Value).ToDictionary(g => g.Key, g => g.ToList());

        var now = ByMarket(month);
        var before = ByMarket(prev);
        var boughtNow = now.Where(kv => kv.Value.Sum(l => l.Kg) > 0).Select(kv => kv.Key).ToHashSet();
        var boughtBefore = before.Where(kv => kv.Value.Sum(l => l.Kg) > 0).Select(kv => kv.Key).ToHashSet();
        var names = agents.ToDictionary(a => a.Id, a => a.Name);

        static long? MainAgent(List<SaleLine> lines) =>
            lines.Where(l => l.AgentId != null).GroupBy(l => l.AgentId!.Value).OrderByDescending(g => g.Sum(l => l.Kg)).Select(g => (long?)g.Key).FirstOrDefault();

        var sleeping = new List<(long Market, long? Agent, DateOnly Last, int Days, decimal Kg, decimal Revenue, string Bucket)>();
        foreach (var market in boughtNow.Union(boughtBefore))
        {
            // Последняя покупка в этом месяце — последний день с чистым весом больше нуля (возврат в другой день покупку не отменяет).
            DateOnly? last = now.TryGetValue(market, out var lines)
                ? lines.GroupBy(l => l.Date).Where(g => g.Sum(l => l.Kg) > 0).Select(g => (DateOnly?)g.Key).DefaultIfEmpty(null).Max()
                : null;
            var bucket = WeeklyReportMath.SleepBucket(boughtNow.Contains(market) ? last : null, boughtBefore.Contains(market), day, options.SleepFromDays, options.SleepDays);
            if (bucket is null)
            {
                continue;
            }

            var source = bucket == WeeklySleepBuckets.Days15To30 ? lines! : before[market];
            var lastDay = bucket == WeeklySleepBuckets.Days15To30 ? last!.Value
                : source.GroupBy(l => l.Date).Where(g => g.Sum(l => l.Kg) > 0).Select(g => g.Key).DefaultIfEmpty(source.Max(l => l.Date)).Max();
            sleeping.Add((market, MainAgent(source), lastDay, day.DayNumber - lastDay.DayNumber, source.Sum(l => l.Kg), source.Sum(l => l.Revenue), bucket));
        }

        var ids = sleeping.Select(s => s.Market).ToArray();
        var marketNames = ids.Length == 0
            ? new Dictionary<long, string>()
            : await db.LinkoMarkets.AsNoTracking().Where(m => ids.Contains(m.Id)).Select(m => new { m.Id, m.Name }).ToDictionaryAsync(m => m.Id, m => m.Name, ct);
        var list = sleeping
            .Select(s => new WeeklySleepingMarket(s.Market, marketNames.GetValueOrDefault(s.Market) ?? $"#{s.Market}", s.Agent,
                s.Agent is { } a ? names.GetValueOrDefault(a) : null, s.Last, s.Days, Math.Round(s.Kg, 1), s.Bucket))
            .OrderByDescending(s => s.Kg)
            .ToList();
        var newMarkets = boughtNow.Where(m => !boughtBefore.Contains(m)).ToList();

        var rows = agents.Select(a =>
        {
            var baseOf = boughtBefore.Where(m => MainAgent(before[m]) == a.Id).ToHashSet();
            var bought = baseOf.Count(boughtNow.Contains);
            return new WeeklyClientRow(a.Id, a.Name, baseOf.Count, bought, baseOf.Count == 0 ? null : (decimal)bought / baseOf.Count,
                list.Count(s => s.AgentId == a.Id && s.Bucket == WeeklySleepBuckets.Days15To30), list.Count(s => s.AgentId == a.Id && s.Bucket == WeeklySleepBuckets.Days30Plus),
                newMarkets.Count(m => MainAgent(now[m]) == a.Id));
        }).ToList();

        return new WeeklyClientMetrics(boughtBefore.Count, boughtBefore.Count(boughtNow.Contains),
            list.Count(s => s.Bucket == WeeklySleepBuckets.Days15To30), list.Where(s => s.Bucket == WeeklySleepBuckets.Days15To30).Sum(s => s.Kg),
            list.Count(s => s.Bucket == WeeklySleepBuckets.Days30Plus),
            sleeping.Where(s => s.Bucket == WeeklySleepBuckets.Days30Plus).Sum(s => s.Revenue),
            newMarkets.Count, newMarkets.Sum(m => now[m].Sum(l => l.Revenue)), rows, list);
    }

    // ---------- Товарные линии ----------

    /// <summary>
    /// Линии: план категорий — из карточек «Продаж» области, факт — по строкам месяца по день отчёта; категория строки — категория отчёта
    /// её товара (SalesAnalytics.ReportCategoryOf), так же, как в автоотчётах. ТП × категория — кг и ТТ с чистым весом категории.
    /// </summary>
    private static WeeklyProductMetrics Products(IEnumerable<RegionView> regions, IReadOnlyList<SaleLine> month, IReadOnlyList<ReportPerson> agents, WeeklyKpiMetrics kpi,
        SalesAnalytics current)
    {
        var views = regions.ToList();
        var named = month.Where(l => l.ProductId is not null).Select(l => (Line: l, Category: current.ReportCategoryOf(l.ProductId!.Value))).Where(x => x.Category is not null).ToList();
        var factByCategory = named.GroupBy(x => x.Category!).ToDictionary(g => g.Key, g => g.Sum(x => x.Line.Kg));
        var categories = views.SelectMany(v => v.CategoryPlans)
            .Where(c => c.CategoryId is not null)
            .GroupBy(c => (c.CategoryId, c.Name))
            .Select(g =>
            {
                var plan = g.Any(c => c.PlanKg is not null) ? g.Sum(c => c.PlanKg ?? 0) : (decimal?)null;
                var fact = factByCategory.GetValueOrDefault(g.Key.Name);
                return new WeeklyCategoryRow(g.Key.CategoryId, g.Key.Name, plan, fact, WeeklyReportMath.Ratio(fact, plan));
            })
            .OrderByDescending(c => c.PlanKg ?? 0).ThenByDescending(c => c.FactKg)
            .ToList();
        var order = categories.Select(c => c.Name).ToList();
        var byAgent = agents.Select(a =>
        {
            var cats = named.Where(x => x.Line.AgentId == a.Id).GroupBy(x => x.Category!)
                .Select(g => new WeeklyAgentCategory(g.Key, Math.Round(g.Sum(x => x.Line.Kg), 1), SalesMath.Akb(g.Select(x => x.Line))))
                .Where(c => c.Kg != 0)
                .OrderBy(c => order.IndexOf(c.Name) is var i && i >= 0 ? i : int.MaxValue).ThenByDescending(c => c.Kg)
                .ToList();
            var person = kpi.People.FirstOrDefault(p => p.Id == a.Id);
            var priority = person?.Cells.Where(c => c.Unit == KpiMath.Kg && c.Label is "Могуль" or "Twizos")
                .Select(c => new WeeklyPriorityLine(c.Label, c.Plan, c.Fact, c.Plan > 0 ? c.Fact / c.Plan : null)).ToList() ?? [];
            return new WeeklyAgentProducts(a.Id, a.Name, cats, priority);
        }).ToList();
        return new WeeklyProductMetrics(categories, byAgent);
    }

    // ---------- Акции ----------

    private sealed record PromoResult(WeeklyPromotionMetrics Metrics, DateTimeOffset? AsOf, IReadOnlyDictionary<long, int> PrevWeekByAgent);

    private async Task<PromoResult> PromoAsync(ResolvedScope scope, IReadOnlyDictionary<Guid, RegionView> regions, DateOnly prevFrom, DateOnly prevTo, DateOnly weekFrom,
        DateOnly weekEnd, DateOnly monthStart, DateOnly through, CancellationToken ct)
    {
        var data = await promo.BaseAsync(ct);
        var regionNames = scope.Regions.Select(r => r.Name).Concat(regions.Values.Select(v => v.Name)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var agentIds = scope.Agent is { } one ? new HashSet<long> { one.Id } : scope.Agents.Select(a => a.Id).ToHashSet();
        var rows = data.Months
            .SelectMany(m => m.Rows.Select(r => (Date: new DateOnly(m.Year, m.Month, r.Day), Row: r)))
            .Where(x => regionNames.Contains(x.Row.Region) && (scope.Agent is null || x.Row.Agent == scope.Agent.Id))
            .ToList();

        static int Outlets(IEnumerable<PromoRow> r) => r.Where(x => x.Market != null && x.Sale > 0).Select(x => x.Market).Distinct().Count();

        var prevWeek = rows.Where(x => x.Date >= prevFrom && x.Date <= prevTo).Select(x => x.Row).ToList();
        var week = rows.Where(x => x.Date >= weekFrom && x.Date <= weekEnd).Select(x => x.Row).ToList();
        var month = rows.Where(x => x.Date >= monthStart && x.Date <= through).Select(x => x.Row).ToList();
        var sale = month.Sum(r => r.Sale);
        var cost = month.Sum(r => r.Discount);
        var agentNames = scope.Agents.ToDictionary(a => a.Id, a => a.Name);
        var agentRows = month.Select(r => r.Agent).Concat(prevWeek.Select(r => r.Agent)).Distinct()
            .Where(a => a is { } id && agentIds.Contains(id))
            .Select(a =>
            {
                var own = month.Where(r => r.Agent == a).ToList();
                var s = own.Sum(r => r.Sale);
                var c = own.Sum(r => r.Discount);
                return new WeeklyPromoAgent(a, agentNames.GetValueOrDefault(a!.Value) ?? data.Agents.GetValueOrDefault(a.Value) ?? $"#{a}",
                    Outlets(prevWeek.Where(r => r.Agent == a)), Outlets(week.Where(r => r.Agent == a)), Outlets(own), s, c, s > 0 ? c / s : null);
            })
            .OrderByDescending(a => a.OutletsMonth)
            .ToList();
        var kinds = month.GroupBy(r => r.Promo < 0 ? -1 : r.Promo)
            .Select(g => new WeeklyPromoKind(g.Key, g.Key < 0 ? "Ручная скидка" : data.Promos.TryGetValue(g.Key, out var p) ? p.Name : $"#{g.Key}", Outlets(g),
                g.Sum(r => r.Sale), g.Sum(r => r.Discount)))
            .OrderByDescending(k => k.Sale)
            .ToList();
        var metrics = new WeeklyPromotionMetrics(prevFrom, prevTo, Outlets(prevWeek), Outlets(week), Outlets(month), sale, cost, sale > 0 ? cost / sale : null, agentRows, kinds);
        var byAgent = agentIds.ToDictionary(id => id, id => Outlets(prevWeek.Where(r => r.Agent == id)));
        return new PromoResult(metrics, data.AsOf, byAgent);
    }

    // ---------- Дилеры ----------

    private async Task<(List<WeeklyDealerMetrics> Dealers, (DateTimeOffset? Stock, DateOnly? Dso))> DealersAsync(IReadOnlyDictionary<Guid, RegionView> regions, bool money,
        DateOnly dataThrough, List<WeeklyNote> warnings, CancellationToken ct)
    {
        var result = new List<WeeklyDealerMetrics>();
        if (regions.Count == 0)
        {
            return (result, (null, null));
        }

        var stockView = await stock.QueryAsync(new StockQuery(), ct);
        OutstockView? lost = null;
        try
        {
            lost = await outstock.GetAsync(new OutstockQuery(dataThrough.Year, dataThrough.Month), ct);
        }
        catch (InvalidOperationException)
        {
            // Аутсток не считается без снимка остатков — страница «упущено» пишет «нет данных».
        }

        DsoView? dso = null;
        if (money)
        {
            dso = DsoCompute.Build(await settlements.BaseAsync(ct));
        }

        foreach (var (id, view) in regions)
        {
            var key = id.ToString();
            var items = stockView.Items.Where(i => i.Regions.ContainsKey(key)).Select(i => (Item: i, Cell: i.Regions[key])).ToList();
            var withSales = items.Where(x => x.Cell.KgPerDay is >= 1).ToList();
            WeeklyDealerSku Sku((StockItem Item, StockCell Cell) x) =>
                new(x.Item.ProductId, x.Item.Name, x.Item.Code, x.Item.Category, x.Cell.Kg, x.Cell.KgPerDay, x.Cell.DaysOfCover, x.Cell.OrderKg, x.Item.Top);
            var deficit = withSales.Where(x => x.Cell.DaysOfCover is not { } d || d < options.DeficitDays)
                .OrderBy(x => x.Cell.DaysOfCover ?? -1).ThenByDescending(x => x.Cell.KgPerDay).Take(options.DealerRows).Select(Sku).ToList();
            var ship = withSales.Where(x => x.Cell.OrderKg > 0).OrderByDescending(x => x.Cell.OrderKg).Take(options.DealerRows).Select(Sku).ToList();
            var pairs = lost?.Pairs.Where(p => p.RegionId == key).ToList();
            var dealerMoney = dso?.Dealers.FirstOrDefault(d => SameDealer(d.Name, view.Dealer) || SameDealer(d.Key, view.Dealer));
            if (money && dso is not null && dealerMoney is null && !string.IsNullOrWhiteSpace(view.Dealer))
            {
                warnings.Add(WeeklyNote.Of("dso-dealer-missing", ("region", view.Name), ("dealer", view.Dealer)));
            }

            result.Add(new WeeklyDealerMetrics(id, view.Name, view.Dealer, stockView.SnapshotDate, withSales.Count(x => x.Cell.Kg is not > 0), withSales.Sum(x => x.Cell.OrderKg),
                deficit, ship, lost is null ? null : lost.Month, pairs?.Sum(p => p.LostKg), pairs?.Sum(p => p.LostSum),
                pairs?.Where(p => p.LostKg > 0).OrderByDescending(p => p.LostSum).Take(5).Select(p => new WeeklyLostSku(p.Product, p.ZeroDays, p.LostKg, p.LostSum)).ToList() ?? [],
                dealerMoney is null ? null : new WeeklyDealerMoney(dealerMoney.Name, dealerMoney.Debt, dealerMoney.Overdue, dealerMoney.Fact, dealerMoney.Norm, dealerMoney.Status, dealerMoney.PaidMonth,
                    dealerMoney.ShippedMonth, dealerMoney.LastPay, dealerMoney.Open.Select(o => new WeeklyDealerOpen(o.Date, o.Remaining, o.Due)).ToList())));
        }

        return (result, (stockView.SyncedAt, dso?.AsOf));
    }

    /// <summary>Дилер DSO и дилер региона — одно и то же: без регистра, лишних пробелов и «ё».</summary>
    public static bool SameDealer(string? a, string? b)
    {
        static string Norm(string s) => string.Join(' ', s.ToLowerInvariant().Replace('ё', 'е').Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return a is { Length: > 0 } && b is { Length: > 0 } && Norm(a) == Norm(b);
    }

    // ---------- Цели ----------

    private async Task<WeeklyGoals> GoalsAsync(IReadOnlyList<ReportPerson> agents, IReadOnlyDictionary<long, TeamRow> team, IReadOnlyDictionary<long, AgentMonthPlan> plans,
        IReadOnlyList<SaleLine> fullWeek, PromoResult promoResult, WeeklyClientMetrics clients, DateOnly weekTo, DateOnly dataThrough,
        IReadOnlyDictionary<DateOnly, string> holidays, CancellationToken ct)
    {
        var nextFrom = weekTo.AddDays(2);
        var nextTo = nextFrom.AddDays(5);
        var nextMonth = nextFrom.Month != dataThrough.Month || nextFrom.Year != dataThrough.Year;
        var planMonth = nextMonth ? new DateOnly(nextFrom.Year, nextFrom.Month, 1) : new DateOnly(dataThrough.Year, dataThrough.Month, 1);
        var nextPlans = nextMonth
            ? (await linko.AgentPlansAsync(agents.Select(a => a.Id).ToList(), planMonth.Year, planMonth.Month, ct)).ToDictionary(p => p.LinkoUserId)
            : new Dictionary<long, AgentMonthPlan>();
        var ofNextMonth = nextMonth && nextPlans.Values.Any(p => p.PlanKg is > 0);
        if (!ofNextMonth)
        {
            planMonth = new DateOnly(dataThrough.Year, dataThrough.Month, 1);
        }

        var monthDays = WeeklyReportMath.WorkDays(planMonth, planMonth.AddMonths(1).AddDays(-1), holidays);
        var weekDays = WeeklyReportMath.WorkDays(nextFrom, nextTo, holidays);

        var rows = agents.Select(a =>
        {
            decimal? plan = ofNextMonth ? nextPlans.GetValueOrDefault(a.Id)?.PlanKg : team.GetValueOrDefault(a.Id)?.PlanKg ?? plans.GetValueOrDefault(a.Id)?.PlanKg;
            var akbWas = SalesMath.Akb(fullWeek.Where(l => l.AgentId == a.Id));
            var promoWas = promoResult.PrevWeekByAgent.GetValueOrDefault(a.Id);
            var mine = clients.Sleeping.Where(s => s.AgentId == a.Id).ToList();
            var s15 = mine.Where(s => s.Bucket == WeeklySleepBuckets.Days15To30).ToList();
            var s30 = mine.Where(s => s.Bucket == WeeklySleepBuckets.Days30Plus).ToList();
            return new WeeklyAgentGoal(a.Id, a.Name, WeeklyReportMath.SalesGoal(plan, monthDays, weekDays), akbWas, WeeklyReportMath.Goal(akbWas, options.GoalAkbFactor),
                promoWas, WeeklyReportMath.Goal(promoWas, options.GoalPromoFactor, options.GoalPromoMin),
                WeeklyReportMath.Return(s15.Count, options.ReturnShare15To30), s15.Count, WeeklyReportMath.Return(s30.Count, options.ReturnShare30Plus), s30.Count,
                s15.Take(options.ReturnList15To30).ToList(), s30.Take(options.ReturnList30Plus).ToList());
        }).ToList();

        var sales = rows.Where(r => r.SalesKg is not null).Select(r => r.SalesKg!.Value).ToList();
        return new WeeklyGoals(nextFrom, nextTo, weekDays, ofNextMonth, sales.Count == 0 ? null : sales.Sum(), rows.Sum(r => r.Akb), rows.Sum(r => r.Promo),
            rows.Sum(r => r.Return15To30), rows.Sum(r => r.Return30Plus), false, rows);
    }

    // ---------- Выводы ----------

    /// <summary>
    /// Три вывода, как в образцах: «хорошо» — у кого неделя выросла сильнее всех к прошлой и лучший прогноз; «плохо» — прогноз месяца и три
    /// худших по прогнозу; «фокус» — сколько нужно в день до 80% плана. Строки — регионы (РОП, РМ) или ТП (СВ).
    /// </summary>
    private static IReadOnlyList<WeeklyNote> Recommend(string role, WeeklySalesMetrics total, IReadOnlyList<WeeklyRegionMetrics> regions,
        IReadOnlyList<WeeklyEmployeeMetrics> employees)
    {
        var units = role is "rop" or TelegramReportRoles.Rm
            ? regions.Select(r => (r.Name, r.Sales.WeekVsPrev, r.Sales.ForecastExecution)).ToList()
            : employees.Select(e => (e.Name, e.WeekVsPrev, e.ForecastExecution)).ToList();
        var notes = new List<WeeklyNote>();
        var grew = units.Where(u => u.WeekVsPrev is > 0).OrderByDescending(u => u.WeekVsPrev).FirstOrDefault();
        var best = units.Where(u => u.ForecastExecution is not null).OrderByDescending(u => u.ForecastExecution).FirstOrDefault();
        if (grew.Name is not null || best.Name is not null)
        {
            notes.Add(WeeklyNote.Of("good", ("grew", grew.Name), ("growth", grew.WeekVsPrev), ("best", best.Name), ("bestForecast", best.ForecastExecution)));
        }

        var worst = units.Where(u => u.ForecastExecution is not null).OrderBy(u => u.ForecastExecution).Take(3).ToList();
        if (total.ForecastExecution is not null)
        {
            notes.Add(WeeklyNote.Of("bad", ("forecast", total.ForecastExecution),
                ("worst", string.Join("; ", worst.Select(w => $"{w.Name}|{(w.ForecastExecution ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)}")))));
        }

        if (total.To80 is { PerDay: { } perDay })
        {
            notes.Add(WeeklyNote.Of("focus", ("perDay", Math.Round(perDay, 0)), ("current", total.CurrentPerDayKg is { } c ? Math.Round(c, 0) : null)));
        }

        return notes;
    }

    // ---------- Источники ----------

    private async Task<IReadOnlyList<WeeklySourceStatus>> SourcesAsync(DateTimeOffset? kpiAt, DateTimeOffset? promoAt, (DateTimeOffset? Stock, DateOnly? Dso) dealers,
        CancellationToken ct)
    {
        var states = await db.LinkoSyncStates.AsNoTracking().Where(s => s.Entity == "orders" || s.Entity == "visits")
            .Select(s => new { s.Entity, s.LastSuccessAt }).ToListAsync(ct);
        var now = time.GetUtcNow();
        WeeklySourceStatus Linko(string key, string entity)
        {
            var at = states.FirstOrDefault(s => s.Entity == entity)?.LastSuccessAt;
            return new WeeklySourceStatus(key, at is null ? WeeklySourceStates.Missing : now - at > TimeSpan.FromHours(6) ? WeeklySourceStates.Stale : WeeklySourceStates.Ok, at);
        }

        return
        [
            Linko(WeeklySources.Orders, "orders"),
            Linko(WeeklySources.Visits, "visits"),
            new(WeeklySources.Kpi, kpiAt is null ? WeeklySourceStates.Missing : WeeklySourceStates.Ok, kpiAt),
            new(WeeklySources.Promo, promoAt is null ? WeeklySourceStates.Missing : WeeklySourceStates.Ok, promoAt),
            new(WeeklySources.Stock, dealers.Stock is null ? WeeklySourceStates.Missing : WeeklySourceStates.Ok, dealers.Stock),
            new(WeeklySources.Dso, dealers.Dso is null ? WeeklySourceStates.Missing : WeeklySourceStates.Ok,
                dealers.Dso is { } d ? new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(5)) : null),
            new(WeeklySources.Dmp, WeeklySourceStates.Missing, null, "inventory-not-synced"),
        ];
    }

    // ---------- Общее ----------

    /// <summary>Аналитика месяца дня (без дня — последний месяц с данными); один месяц — один раз за отчёт.</summary>
    private async Task<SalesAnalytics> MonthAsync(DateOnly? day, CancellationToken ct)
    {
        var key = day is { } d ? (d.Year, d.Month) : (0, 0);
        if (_months.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var analytics = await loader.LoadAsync(day?.Year, day?.Month, ct);
        _months[key] = analytics;
        if (day is null)
        {
            _months[(analytics.Period.Year, analytics.Period.Month)] = analytics;
        }

        return analytics;
    }

    /// <summary>Отрезок по месяцам: неделя на стыке месяцев — две части.</summary>
    private static IEnumerable<(DateOnly From, DateOnly To)> Parts(DateOnly from, DateOnly to)
    {
        for (var start = from; start <= to;)
        {
            var monthEnd = new DateOnly(start.Year, start.Month, 1).AddMonths(1).AddDays(-1);
            var end = monthEnd < to ? monthEnd : to;
            yield return (start, end);
            start = end.AddDays(1);
        }
    }

    private async Task<IReadOnlyList<SaleLine>> LinesAsync(DateOnly from, DateOnly to, IReadOnlyList<Guid> regions, long? agent, CancellationToken ct)
    {
        var lines = new List<SaleLine>();
        foreach (var (partFrom, partTo) in Parts(from, to))
        {
            var analytics = await MonthAsync(partFrom, ct);
            if (agent is { } one)
            {
                lines.AddRange(analytics.ScopeLines(partFrom, partTo, agent: one));
                continue;
            }

            foreach (var region in regions.Where(analytics.HasRegion))
            {
                lines.AddRange(analytics.ScopeLines(partFrom, partTo, region));
            }
        }

        return lines;
    }

    /// <summary>День прошлого месяца, когда в нём прошло столько же рабочих дней (для «к прошлому месяцу за то же число рабочих дней»).</summary>
    private static DateOnly? SameWorkDay(DateOnly monthStart, int workDays, IReadOnlyDictionary<DateOnly, string> holidays)
    {
        if (workDays <= 0)
        {
            return null;
        }

        var n = 0;
        for (var d = monthStart; d.Month == monthStart.Month; d = d.AddDays(1))
        {
            n += DailyPlanMath.IsWorkingDay(d, holidays) ? 1 : 0;
            if (n == workDays)
            {
                return d;
            }
        }

        return monthStart.AddMonths(1).AddDays(-1);
    }

    private async Task<IReadOnlyDictionary<DateOnly, string>> HolidaysAsync(DateOnly from, DateOnly to, CancellationToken ct) =>
        await db.SalesHolidays.AsNoTracking().Where(h => h.Date >= from && h.Date <= to).ToDictionaryAsync(h => h.Date, h => h.Name, ct);
}
