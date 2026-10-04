using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Domain.Field;

namespace OneBase.Application.Field;

public sealed record FieldAmount(decimal Sum, decimal Kg, int Orders);

public sealed record FieldPlanFact(decimal? PlanKg, decimal? PlanSum, decimal FactKg, decimal FactSum, decimal? ShareKg, decimal? ShareSum, decimal Expected, decimal? ForecastKg, decimal? ForecastSum);

public sealed record FieldDayPoint(DateOnly Date, decimal Sum, decimal Kg, int Orders);

public sealed record FieldMeView(
    Guid UserId,
    Guid? MemberId,
    string Name,
    FieldRole Role,
    bool CanPlan,
    bool CanManage,
    bool CanManageOrg,
    Guid? TeamId,
    string? TeamName,
    Guid? SupervisorId,
    string? SupervisorName,
    int UnreadNotifications,
    int GeoRadiusM,
    int GpsToleranceM,
    DateOnly Today,
    DateOnly? DataAsOf,
    bool HasLinko,
    FieldVisitRow? ActiveVisit);

public sealed record FieldTodayView(
    DateOnly Date,
    Guid AgentId,
    string AgentName,
    FieldAmount Today,
    decimal? DailyPlanKg,
    decimal? DailyPlanSum,
    decimal? DayShareKg,
    decimal? DayShareSum,
    FieldPlanFact Month,
    int VisitsToday,
    int LinkoVisitsDone,
    int LinkoVisitsPlanned,
    int TasksOpen,
    int TasksDueToday,
    int TasksDoneToday,
    int TasksOverdue,
    FieldRouteView? Route,
    FieldVisitRow? ActiveVisit,
    IReadOnlyList<FieldTaskRow> Tasks,
    IReadOnlyList<FieldRecommendationRow> Recommendations,
    IReadOnlyList<FieldDayPoint> Days,
    DateOnly? DataAsOf);

public sealed record FieldAgentDayRow(
    Guid AgentId,
    string Name,
    Guid? TeamId,
    string? TeamName,
    decimal SumToday,
    decimal KgToday,
    int OrdersToday,
    decimal? DailyPlanKg,
    decimal? DayShareKg,
    decimal MonthKg,
    decimal MonthSum,
    decimal? PlanKg,
    decimal? MonthShareKg,
    decimal? BehindPp,
    int Visits,
    int RoutePlanned,
    int RouteVisited,
    int RouteSkipped,
    int TasksOpen,
    int TasksOverdue,
    int TasksDoneToday,
    int FarVisits,
    FieldKpi.AgentDayStatus Status,
    string? StatusNote);

public sealed record FieldTeamRow(Guid TeamId, string Name, string? Supervisor, int Agents, decimal SumToday, decimal MonthKg, decimal? PlanKg, decimal? ShareKg, int Visits, int TasksOverdue, int Problems);

public sealed record FieldAttention(Guid AgentId, string Title, IReadOnlyList<string> Details, string Severity, string Link);

public sealed record FieldDashboardView(
    DateOnly Date,
    FieldRole Role,
    int Agents,
    int Supervisors,
    int ActiveAgents,
    FieldAmount Today,
    decimal? DailyPlanKg,
    decimal? DayShareKg,
    FieldPlanFact Month,
    int Visits,
    int PlannedPoints,
    int VisitedPoints,
    decimal? Coverage,
    int TasksOpen,
    int TasksDoneToday,
    int TasksOverdue,
    decimal? TaskCompletion,
    int ActiveRoutes,
    int PendingRecommendations,
    IReadOnlyList<FieldAgentDayRow> AgentRows,
    IReadOnlyList<FieldTeamRow> Teams,
    IReadOnlyList<FieldAttention> Attention,
    IReadOnlyList<FieldDayPoint> Days,
    DateOnly? DataAsOf);

public sealed record FieldAgentKpi(
    Guid AgentId,
    string Name,
    Guid? TeamId,
    string? TeamName,
    decimal Sum,
    decimal Kg,
    decimal? PlanKg,
    decimal? PlanSum,
    decimal? ShareKg,
    decimal? ShareSum,
    decimal? ForecastShare,
    int Orders,
    int ActiveMarkets,
    decimal? PlanAkb,
    int AssignedMarkets,
    int VisitsDone,
    int VisitsPlanned,
    decimal? VisitPlanShare,
    int SalesBaseVisits,
    int TasksTotal,
    int TasksDone,
    decimal? TaskShare,
    int RoutePoints,
    int RouteVisited,
    decimal? RouteCoverage,
    decimal? MarketCoverage,
    decimal? SumPerVisit);

public sealed record FieldTeamKpi(Guid TeamId, string Name, string? Supervisor, int Agents, decimal Sum, decimal Kg, decimal? PlanKg, decimal? ShareKg, decimal? AvgAgentShare,
    int VisitsDone, int TasksDone, int TasksTotal, decimal? RouteCoverage, decimal? MarketCoverage, decimal? SumPerVisit);

public sealed record FieldKpiView(int Year, int Month, DateOnly AsOf, decimal ExpectedShare, FieldAgentKpi Total, IReadOnlyList<FieldAgentKpi> Agents, IReadOnlyList<FieldTeamKpi> Teams);

public sealed record FieldAgentCard(FieldMemberInfo Agent, string? TeamName, string? SupervisorName, FieldTodayView Today, FieldAgentKpi Kpi, IReadOnlyList<FieldVisitRow> Visits, bool CanPlan);

/// <summary>Дашборды и KPI: агент — свой день; супервайзер — команда; РМ — организация. Факт продаж — Linko, визиты — Linko + Sales Base.</summary>
public sealed class FieldDashboardService(
    IAppDbContext db,
    FieldDirectory directory,
    FieldSettingsStore settingsStore,
    ILinkoSalesProvider linko,
    FieldRouteService routes,
    FieldTaskService tasks,
    FieldVisitService visits,
    FieldRecommendationService recommendations)
{
    public async Task<FieldMeView> MeAsync(FieldScope scope, string fallbackName, CancellationToken ct)
    {
        var snapshot = await directory.GetAsync(ct);
        var settings = await settingsStore.GetAsync(ct);
        var me = scope.MemberId is { } id ? snapshot.Members.GetValueOrDefault(id) : null;
        var team = me?.TeamId is { } t ? snapshot.Teams.GetValueOrDefault(t) : null;
        var supervisor = me is null ? null : me.Role == FieldRole.Agent ? snapshot.SupervisorOf(me.Id) : null;
        var unread = scope.MemberId is { } mid ? await db.FieldNotifications.CountAsync(n => n.RecipientId == mid && n.ReadAt == null, ct) : 0;
        return new FieldMeView(scope.UserId, scope.MemberId, me?.FullName ?? fallbackName, scope.Role, scope.CanPlan, scope.CanManage, scope.CanManageOrg, team?.Id, team?.Name, supervisor,
            supervisor is { } s ? snapshot.NameOf(s) : null, unread, settings.GeoRadiusM, settings.GpsToleranceM, FieldClock.Today,
            await linko.LastDataDateAsync(ct), me?.LinkoUserId is not null, await visits.ActiveAsync(scope, ct));
    }

    /// <summary>День агента: заказы за день против дневного плана, месяц, визиты, задачи, маршрут, рекомендации.</summary>
    public async Task<FieldTodayView> TodayAsync(FieldScope scope, Guid? agentId, DateOnly? date, CancellationToken ct)
    {
        // Без agentId — свой день, только для агента; супервайзер и РМ указывают агента своей зоны.
        var agent = agentId ?? (scope.IsAgent ? scope.MemberId : null) ?? throw new FieldValidationException("Укажите агента.");
        if (!scope.CanSeeAgent(agent))
        {
            throw new FieldNotFoundException("Агент не найден.");
        }

        var snapshot = await directory.GetAsync(ct);
        var member = snapshot.Members.GetValueOrDefault(agent) ?? throw new FieldNotFoundException("Агент не найден.");
        var day = date ?? FieldClock.Today;
        var monthStart = new DateOnly(day.Year, day.Month, 1);
        var linkoIds = member.LinkoUserId is { } l ? new[] { l } : [];

        var taken = (await linko.AgentDaySalesAsync(linkoIds, day, day, SalesBasis.Taken, ct)).FirstOrDefault();
        var delivered = await linko.AgentDaySalesAsync(linkoIds, monthStart, day, SalesBasis.Delivered, ct);
        var plan = (await linko.AgentPlansAsync(linkoIds, day.Year, day.Month, ct)).FirstOrDefault();
        var month = PlanFact(plan?.PlanKg, plan?.PlanSum, delivered.Sum(d => d.Kg), delivered.Sum(d => d.Sum), day);
        var dailyKg = FieldKpi.DailyPlan(plan?.PlanKg, day.Year, day.Month);
        var dailySum = FieldKpi.DailyPlan(plan?.PlanSum, day.Year, day.Month);

        var linkoVisits = await linko.VisitsAsync(linkoIds, day, day, ct);
        var dayStart = FieldClock.DayStartUtc(day);
        var ownVisits = await db.FieldVisits.AsNoTracking()
            .Where(v => v.AgentId == agent && v.StartedAt >= dayStart && v.StartedAt < dayStart.AddDays(1) && v.Status == FieldVisitStatus.Completed)
            .Select(v => v.MarketId)
            .ToListAsync(ct);
        var visitedMarkets = ownVisits.Concat(linkoVisits.Where(v => v.Done).Select(v => v.MarketId)).Distinct().Count();

        // Нужны только открытые задачи и выполненные в этот день — не вся история.
        var openStatuses = FieldTaskRules.Open;
        var taskDayStart = FieldClock.DayStartUtc(day);
        var taskDayEnd = taskDayStart.AddDays(1);
        var taskRows = await db.FieldTasks.AsNoTracking()
            .Where(t => t.AssignedToId == agent && (openStatuses.Contains(t.Status) || (t.CompletedAt >= taskDayStart && t.CompletedAt < taskDayEnd)))
            .Select(t => new { t.Status, t.DueDate, t.CompletedAt })
            .ToListAsync(ct);
        var open = taskRows.Where(t => FieldTaskRules.IsOpen(t.Status)).ToList();

        var topTasks = await tasks.ListAsync(scope, new FieldTaskQuery("open", agent, null, null, null, null, null, 1, 10), ct);
        var days = delivered.GroupBy(d => d.Date).OrderBy(g => g.Key).Select(g => new FieldDayPoint(g.Key, g.Sum(x => x.Sum), g.Sum(x => x.Kg), g.Sum(x => x.Orders))).ToList();

        return new FieldTodayView(
            day,
            agent,
            member.FullName,
            new FieldAmount(taken?.Sum ?? 0, taken?.Kg ?? 0, taken?.Orders ?? 0),
            dailyKg,
            dailySum,
            FieldKpi.Share(taken?.Kg ?? 0, dailyKg),
            FieldKpi.Share(taken?.Sum ?? 0, dailySum),
            month,
            visitedMarkets,
            linkoVisits.Count(v => v.Done),
            linkoVisits.Count(v => v.InPlan),
            open.Count,
            open.Count(t => t.DueDate == day),
            taskRows.Count(t => t.CompletedAt is { } c && FieldClock.LocalDate(c) == day),
            open.Count(t => t.DueDate < day),
            await routes.GetForAsync(scope.CanSeeAgent(agent) ? scope : scope, agent, day, ct),
            scope.MemberId == agent ? await visits.ActiveAsync(scope, ct) : null,
            topTasks.Items,
            await recommendations.ForAgentAsync(scope, agent, 5, ct),
            days,
            await linko.LastDataDateAsync(ct));
    }

    private static FieldPlanFact PlanFact(decimal? planKg, decimal? planSum, decimal factKg, decimal factSum, DateOnly day) => new(
        planKg,
        planSum,
        factKg,
        factSum,
        FieldKpi.Share(factKg, planKg),
        FieldKpi.Share(factSum, planSum),
        FieldKpi.ExpectedShare(day),
        planKg is > 0 ? FieldKpi.Forecast(factKg, day) : null,
        planSum is > 0 ? FieldKpi.Forecast(factSum, day) : null);

    /// <summary>Дашборд супервайзера (команда) или РМ (организация, фильтры: команда, филиал, агент).</summary>
    public async Task<FieldDashboardView> DashboardAsync(FieldScope scope, DateOnly? date, Guid? teamId, long? branchId, Guid? agentId, CancellationToken ct)
    {
        if (scope.IsAgent)
        {
            throw new FieldForbiddenException("Дашборд команды — для супервайзера и РМ.");
        }

        var snapshot = await directory.GetAsync(ct);
        var settings = await settingsStore.GetAsync(ct);
        var day = date ?? FieldClock.Today;
        var monthStart = new DateOnly(day.Year, day.Month, 1);
        var agents = AgentsFor(scope, snapshot, teamId, branchId, agentId);
        var linkoOf = agents.Where(a => a.LinkoUserId is not null).ToDictionary(a => a.Id, a => a.LinkoUserId!.Value);
        var linkoIds = linkoOf.Values.ToList();

        var taken = (await linko.AgentDaySalesAsync(linkoIds, day, day, SalesBasis.Taken, ct)).ToDictionary(d => d.LinkoUserId);
        var monthDays = await linko.AgentDaySalesAsync(linkoIds, monthStart, day, SalesBasis.Delivered, ct);
        var monthBy = monthDays.GroupBy(d => d.LinkoUserId).ToDictionary(g => g.Key, g => (Kg: g.Sum(x => x.Kg), Sum: g.Sum(x => x.Sum)));
        var plans = (await linko.AgentPlansAsync(linkoIds, day.Year, day.Month, ct)).ToDictionary(p => p.LinkoUserId);
        var linkoVisits = await linko.VisitsAsync(linkoIds, day, day, ct);
        var visitsBy = linkoVisits.Where(v => v.Done).GroupBy(v => v.LinkoUserId).ToDictionary(g => g.Key, g => g.Select(v => v.MarketId).ToHashSet());

        var agentIds = agents.Select(a => a.Id).ToArray();
        var dayStart = FieldClock.DayStartUtc(day);
        var own = await db.FieldVisits.AsNoTracking()
            .Where(v => agentIds.Contains(v.AgentId) && v.StartedAt >= dayStart && v.StartedAt < dayStart.AddDays(1) && v.Status != FieldVisitStatus.Cancelled)
            .Select(v => new { v.AgentId, v.MarketId, v.Status, v.GeoStatus })
            .ToListAsync(ct);
        var dayRoutes = await db.FieldRoutes.AsNoTracking()
            .Where(r => r.Date == day && agentIds.Contains(r.AgentId))
            .Select(r => new { r.AgentId, r.Status, Points = r.Points.Where(p => p.Status != FieldPointStatus.Cancelled).Select(p => new { p.MarketId, p.Status }).ToList() })
            .ToListAsync(ct);
        // Только открытые задачи и выполненные в этот день — не вся история.
        var openStatuses = FieldTaskRules.Open;
        var dayEnd = dayStart.AddDays(1);
        var taskRows = await db.FieldTasks.AsNoTracking()
            .Where(t => agentIds.Contains(t.AssignedToId) && (openStatuses.Contains(t.Status) || (t.CompletedAt >= dayStart && t.CompletedAt < dayEnd)))
            .Select(t => new { t.AssignedToId, t.Status, t.DueDate, t.CompletedAt })
            .ToListAsync(ct);
        var pending = await recommendations.PendingCountAsync(scope, agentIds, ct);

        var isPast = day < FieldClock.Today;
        var hour = FieldClock.Now.Hour;
        var rows = agents.Select(a =>
        {
            var linkoId = linkoOf.GetValueOrDefault(a.Id);
            var t = linkoId != 0 ? taken.GetValueOrDefault(linkoId) : null;
            var m = linkoId != 0 ? monthBy.GetValueOrDefault(linkoId) : default;
            var plan = linkoId != 0 ? plans.GetValueOrDefault(linkoId) : null;
            var done = (linkoId != 0 ? visitsBy.GetValueOrDefault(linkoId) : null) ?? [];
            var ownDone = own.Where(v => v.AgentId == a.Id && v.Status == FieldVisitStatus.Completed).Select(v => v.MarketId);
            var route = dayRoutes.FirstOrDefault(r => r.AgentId == a.Id);
            var planned = route?.Points.Count ?? 0;
            var visited = route?.Points.Count(p => p.Status == FieldPointStatus.Visited || (p.Status == FieldPointStatus.Planned && done.Contains(p.MarketId))) ?? 0;
            var skipped = route?.Points.Count(p => p.Status == FieldPointStatus.Skipped) ?? 0;
            var visits = done.Concat(ownDone).Distinct().Count();
            var agentTasks = taskRows.Where(x => x.AssignedToId == a.Id).ToList();
            var openTasks = agentTasks.Count(x => FieldTaskRules.IsOpen(x.Status));
            var overdue = agentTasks.Count(x => FieldTaskRules.IsOpen(x.Status) && x.DueDate < day);
            var doneToday = agentTasks.Count(x => x.CompletedAt is { } c && FieldClock.LocalDate(c) == day);
            var far = own.Count(v => v.AgentId == a.Id && v.GeoStatus == FieldGeoStatus.Far);
            var inVisit = own.Any(v => v.AgentId == a.Id && v.Status == FieldVisitStatus.InProgress);
            var dailyKg = FieldKpi.DailyPlan(plan?.PlanKg, day.Year, day.Month);
            var behind = FieldKpi.BehindPace(m.Kg, plan?.PlanKg, day);
            // Будущий день ещё не начался; воскресенье — выходной.
            var status = FieldKpi.DayStatus(inVisit, planned, visited, skipped, t?.Orders ?? 0, visits, isPast, isPast ? 23 : day > FieldClock.Today ? 0 : hour,
                restDay: day.DayOfWeek == DayOfWeek.Sunday);
            var notes = new List<string>();
            if (skipped > 0)
            {
                notes.Add($"пропущено точек: {skipped}");
            }

            if (overdue > 0)
            {
                notes.Add($"просрочено задач: {overdue}");
            }

            if (behind is >= 0 && behind >= settings.BehindPlanPct)
            {
                notes.Add($"отставание от темпа {Math.Round(behind.Value)} п.п.");
            }

            if (far > 0)
            {
                notes.Add($"визитов далеко от точки: {far}");
            }

            var team = a.TeamId is { } tid ? snapshot.Teams.GetValueOrDefault(tid) : null;
            return new FieldAgentDayRow(a.Id, a.FullName, a.TeamId, team?.Name, t?.Sum ?? 0, t?.Kg ?? 0, t?.Orders ?? 0, dailyKg, FieldKpi.Share(t?.Kg ?? 0, dailyKg),
                m.Kg, m.Sum, plan?.PlanKg, FieldKpi.Share(m.Kg, plan?.PlanKg), behind, visits, planned, visited, skipped, openTasks, overdue, doneToday, far, status,
                notes.Count == 0 ? null : string.Join(" · ", notes));
        }).ToList();

        var attention = rows
            .Where(r => r.Status == FieldKpi.AgentDayStatus.Problem || r.TasksOverdue > 0 || r.RouteSkipped > 0 || r.BehindPp >= settings.BehindPlanPct || r.FarVisits > 0)
            .Select(r =>
            {
                var details = new List<string>();
                if (r.MonthShareKg is { } share)
                {
                    details.Add($"Выполнение {Math.Round(share * 100)}%");
                }

                if (r.RoutePlanned > 0 && r.RoutePlanned - r.RouteVisited > 0)
                {
                    details.Add($"{r.RoutePlanned - r.RouteVisited} точки не посещены");
                }

                if (r.TasksOverdue > 0)
                {
                    details.Add($"{r.TasksOverdue} задачи просрочены");
                }

                if (r.Status == FieldKpi.AgentDayStatus.Problem && r.Visits == 0 && r.OrdersToday == 0)
                {
                    details.Add("нет визитов и заказов");
                }

                if (r.FarVisits > 0)
                {
                    details.Add($"{r.FarVisits} визит(а) далеко от точки");
                }

                var severity = r.BehindPp >= settings.BehindPlanPct * 1.75m || r.Status == FieldKpi.AgentDayStatus.Problem ? "bad" : "warn";
                return new FieldAttention(r.AgentId, $"Агент {r.Name}", details, severity, $"/field/agents/{r.AgentId}");
            })
            .OrderBy(a => a.Severity == "bad" ? 0 : 1).ThenByDescending(a => a.Details.Count)
            .Take(12)
            .ToList();

        var teams = rows.Where(r => r.TeamId is not null).GroupBy(r => r.TeamId!.Value)
            .Select(g =>
            {
                var team = snapshot.Teams.GetValueOrDefault(g.Key);
                var planKg = g.Sum(r => r.PlanKg ?? 0);
                return new FieldTeamRow(g.Key, team?.Name ?? "—", team?.SupervisorId is { } s ? snapshot.NameOf(s) : null, g.Count(), g.Sum(r => r.SumToday), g.Sum(r => r.MonthKg),
                    planKg > 0 ? planKg : null, FieldKpi.Share(g.Sum(r => r.MonthKg), planKg > 0 ? planKg : null), g.Sum(r => r.Visits), g.Sum(r => r.TasksOverdue),
                    g.Count(r => r.Status == FieldKpi.AgentDayStatus.Problem));
            })
            .OrderBy(t => t.Name)
            .ToList();

        var planKgTotal = rows.Sum(r => r.PlanKg ?? 0);
        var planSumTotal = plans.Values.Sum(p => p.PlanSum ?? 0);
        var dailyPlanTotal = FieldKpi.DailyPlan(planKgTotal > 0 ? planKgTotal : null, day.Year, day.Month);
        var plannedPoints = rows.Sum(r => r.RoutePlanned);
        var visitedPoints = rows.Sum(r => r.RouteVisited);
        var tasksOpen = rows.Sum(r => r.TasksOpen);
        var tasksDone = rows.Sum(r => r.TasksDoneToday);
        var days = monthDays.GroupBy(d => d.Date).OrderBy(g => g.Key).Select(g => new FieldDayPoint(g.Key, g.Sum(x => x.Sum), g.Sum(x => x.Kg), g.Sum(x => x.Orders))).ToList();
        var supervisors = agents.Select(a => snapshot.SupervisorOf(a.Id)).Where(s => s is not null).Distinct().Count();

        return new FieldDashboardView(
            day,
            scope.Role,
            rows.Count,
            supervisors,
            rows.Count(r => r.OrdersToday > 0 || r.Visits > 0),
            new FieldAmount(rows.Sum(r => r.SumToday), rows.Sum(r => r.KgToday), rows.Sum(r => r.OrdersToday)),
            dailyPlanTotal,
            FieldKpi.Share(rows.Sum(r => r.KgToday), dailyPlanTotal),
            PlanFact(planKgTotal > 0 ? planKgTotal : null, planSumTotal > 0 ? planSumTotal : null, rows.Sum(r => r.MonthKg), rows.Sum(r => r.MonthSum), day),
            rows.Sum(r => r.Visits),
            plannedPoints,
            visitedPoints,
            plannedPoints > 0 ? (decimal)visitedPoints / plannedPoints : null,
            tasksOpen,
            tasksDone,
            rows.Sum(r => r.TasksOverdue),
            tasksOpen + tasksDone > 0 ? (decimal)tasksDone / (tasksOpen + tasksDone) : null,
            dayRoutes.Count(r => r.Status == FieldRouteStatus.InProgress),
            pending,
            rows.OrderBy(r => r.TeamName).ThenBy(r => r.Name).ToList(),
            teams,
            attention,
            days,
            await linko.LastDataDateAsync(ct));
    }

    private static List<FieldMemberInfo> AgentsFor(FieldScope scope, FieldDirectory.Snapshot snapshot, Guid? teamId, long? branchId, Guid? agentId) =>
        scope.AgentIds
            .Select(a => snapshot.Members.GetValueOrDefault(a))
            .Where(m => m is { IsActive: true })
            .Select(m => m!)
            .Where(m => (agentId is null || m.Id == agentId)
                        && (teamId is null || m.TeamId == teamId)
                        && (branchId is null || (m.TeamId is { } t && snapshot.Teams.GetValueOrDefault(t)?.BranchId == branchId)))
            .ToList();

    /// <summary>KPI за месяц: агенты, команды, итог. Продажи — доставленные (как во вторичке), визиты — выполненные в Linko.</summary>
    public async Task<FieldKpiView> KpiAsync(FieldScope scope, int? year, int? month, Guid? teamId, long? branchId, CancellationToken ct, Guid? agentId = null)
    {
        var today = FieldClock.Today;
        if (year is < 2000 or > 2100)
        {
            throw new FieldValidationException("Год — от 2000 до 2100.");
        }

        var y = year ?? today.Year;
        var mo = month is >= 1 and <= 12 ? month.Value : today.Month;
        var first = new DateOnly(y, mo, 1);
        var end = first.AddMonths(1).AddDays(-1);
        var asOf = end < today ? end : today;

        var snapshot = await directory.GetAsync(ct);
        var agents = scope.IsAgent && scope.MemberId is { } me
            ? [snapshot.Members[me]]
            : AgentsFor(scope, snapshot, teamId, branchId, agentId);
        var linkoOf = agents.Where(a => a.LinkoUserId is not null).ToDictionary(a => a.Id, a => a.LinkoUserId!.Value);
        var linkoIds = linkoOf.Values.ToList();
        var agentIds = agents.Select(a => a.Id).ToArray();

        var totals = await linko.AgentTotalsAsync(linkoIds, first, asOf, SalesBasis.Delivered, ct);
        var plans = (await linko.AgentPlansAsync(linkoIds, y, mo, ct)).ToDictionary(p => p.LinkoUserId);
        var visitStats = await linko.AgentVisitStatsAsync(linkoIds, first, asOf, ct);
        var assigned = await AssignedCountsAsync(agents, ct);

        var monthStart = FieldClock.DayStartUtc(first);
        var monthEnd = FieldClock.DayStartUtc(end.AddDays(1));
        var sbVisits = await db.FieldVisits.AsNoTracking()
            .Where(v => agentIds.Contains(v.AgentId) && v.StartedAt >= monthStart && v.StartedAt < monthEnd && v.Status == FieldVisitStatus.Completed)
            .GroupBy(v => v.AgentId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var taskStats = await db.FieldTasks.AsNoTracking()
            .Where(t => agentIds.Contains(t.AssignedToId) && ((t.DueDate >= first && t.DueDate <= end) || (t.DueDate == null && t.CreatedAt >= monthStart && t.CreatedAt < monthEnd)))
            .GroupBy(t => t.AssignedToId)
            .Select(g => new { g.Key, Total = g.Count(t => t.Status != FieldTaskStatus.Cancelled), Done = g.Count(t => t.Status == FieldTaskStatus.Completed || t.Status == FieldTaskStatus.Verified) })
            .ToDictionaryAsync(x => x.Key, x => (x.Total, x.Done), ct);
        // Точка маршрута посещена: визит Sales Base или выполненный визит Linko в тот же день. Визиты Linko — одним запросом
        // за месяц и сверка в памяти: подзапрос на каждую точку маршрута на всей организации слишком медленный.
        var routePoints = await (
                from p in db.FieldRoutePoints.AsNoTracking()
                join r in db.FieldRoutes.AsNoTracking() on p.RouteId equals r.Id
                where r.Date >= first && r.Date <= asOf && agentIds.Contains(r.AgentId) && p.Status != FieldPointStatus.Cancelled
                select new { r.AgentId, r.Date, p.MarketId, p.Status })
            .ToListAsync(ct);
        var linkoDone = routePoints.Any(p => p.Status == FieldPointStatus.Planned)
            ? (await db.LinkoVisits.AsNoTracking()
                    .Where(v => v.UserId != null && linkoIds.Contains(v.UserId.Value) && v.Day >= first && v.Day <= asOf && v.Status == "done" && v.MarketId != null)
                    .Select(v => new { User = v.UserId!.Value, v.Day, Market = v.MarketId!.Value })
                    .ToListAsync(ct))
                .Select(v => (v.User, v.Day, v.Market)).ToHashSet()
            : [];
        var routeBy = routePoints.GroupBy(p => p.AgentId).ToDictionary(g => g.Key, g => (
            Total: g.Count(),
            Visited: g.Count(p => p.Status == FieldPointStatus.Visited
                                  || (p.Status == FieldPointStatus.Planned && linkoOf.TryGetValue(p.AgentId, out var lu) && linkoDone.Contains((lu, p.Date, p.MarketId))))));
        var forecastFactor = FieldKpi.ElapsedWorkingDays(asOf) is var elapsed and > 0 ? (decimal)FieldKpi.WorkingDays(y, mo) / elapsed : (decimal?)null;

        FieldAgentKpi Row(FieldMemberInfo a)
        {
            var l = linkoOf.GetValueOrDefault(a.Id);
            var t = l != 0 ? totals.GetValueOrDefault(l) : null;
            var p = l != 0 ? plans.GetValueOrDefault(l) : null;
            var v = l != 0 ? visitStats.GetValueOrDefault(l) : null;
            var (taskTotal, taskDone) = taskStats.GetValueOrDefault(a.Id);
            var (routeTotal, routeVisited) = routeBy.GetValueOrDefault(a.Id);
            var assignedCount = assigned.GetValueOrDefault(a.Id);
            var shareKg = FieldKpi.Share(t?.Kg ?? 0, p?.PlanKg);
            var team = a.TeamId is { } tid ? snapshot.Teams.GetValueOrDefault(tid)?.Name : null;
            return new FieldAgentKpi(a.Id, a.FullName, a.TeamId, team, t?.Sum ?? 0, t?.Kg ?? 0, p?.PlanKg, p?.PlanSum, shareKg, FieldKpi.Share(t?.Sum ?? 0, p?.PlanSum),
                shareKg * forecastFactor, t?.Orders ?? 0, t?.Markets ?? 0, p?.PlanAkb, assignedCount, v?.Done ?? 0, v?.Planned ?? 0,
                v is { Planned: > 0 } ? (decimal)v.DoneInPlan / v.Planned : null, sbVisits.GetValueOrDefault(a.Id), taskTotal, taskDone,
                taskTotal > 0 ? (decimal)taskDone / taskTotal : null, routeTotal, routeVisited, routeTotal > 0 ? (decimal)routeVisited / routeTotal : null,
                assignedCount > 0 ? Math.Min(1, (decimal)(v?.Markets ?? 0) / assignedCount) : null, v is { Done: > 0 } ? (t?.Sum ?? 0) / v.Done : null);
        }

        var rows = agents.Select(Row).OrderBy(r => r.TeamName).ThenByDescending(r => r.ShareKg ?? -1).ThenBy(r => r.Name).ToList();
        var teams = rows.Where(r => r.TeamId is not null).GroupBy(r => r.TeamId!.Value)
            .Select(g =>
            {
                var team = snapshot.Teams.GetValueOrDefault(g.Key);
                var plan = g.Sum(r => r.PlanKg ?? 0);
                var shares = g.Where(r => r.ShareKg is not null).Select(r => r.ShareKg!.Value).ToList();
                var visitsDone = g.Sum(r => r.VisitsDone);
                var routeTotal = g.Sum(r => r.RoutePoints);
                var assignedCount = g.Sum(r => r.AssignedMarkets);
                return new FieldTeamKpi(g.Key, team?.Name ?? "—", team?.SupervisorId is { } s ? snapshot.NameOf(s) : null, g.Count(), g.Sum(r => r.Sum), g.Sum(r => r.Kg),
                    plan > 0 ? plan : null, FieldKpi.Share(g.Sum(r => r.Kg), plan > 0 ? plan : null), shares.Count > 0 ? shares.Average() : null, visitsDone,
                    g.Sum(r => r.TasksDone), g.Sum(r => r.TasksTotal), routeTotal > 0 ? (decimal)g.Sum(r => r.RouteVisited) / routeTotal : null,
                    assignedCount > 0 ? Math.Min(1, (decimal)g.Sum(r => (r.MarketCoverage ?? 0) * r.AssignedMarkets) / assignedCount) : null,
                    visitsDone > 0 ? g.Sum(r => r.Sum) / visitsDone : null);
            })
            .OrderByDescending(t => t.ShareKg ?? -1)
            .ToList();

        var planKg = rows.Sum(r => r.PlanKg ?? 0);
        var planSum = rows.Sum(r => r.PlanSum ?? 0);
        var planAkb = rows.Sum(r => r.PlanAkb ?? 0);
        var allVisits = rows.Sum(r => r.VisitsDone);
        var allPlanned = rows.Sum(r => r.VisitsPlanned);
        var totalTasks = rows.Sum(r => r.TasksTotal);
        var routeAll = rows.Sum(r => r.RoutePoints);
        var assignedAll = rows.Sum(r => r.AssignedMarkets);
        var total = new FieldAgentKpi(Guid.Empty, "Итого", null, null, rows.Sum(r => r.Sum), rows.Sum(r => r.Kg), planKg > 0 ? planKg : null, planSum > 0 ? planSum : null,
            FieldKpi.Share(rows.Sum(r => r.Kg), planKg > 0 ? planKg : null), FieldKpi.Share(rows.Sum(r => r.Sum), planSum > 0 ? planSum : null),
            FieldKpi.Share(rows.Sum(r => r.Kg), planKg > 0 ? planKg : null) * forecastFactor, rows.Sum(r => r.Orders), rows.Sum(r => r.ActiveMarkets), planAkb > 0 ? planAkb : null,
            assignedAll, allVisits, allPlanned, null, rows.Sum(r => r.SalesBaseVisits), totalTasks, rows.Sum(r => r.TasksDone),
            totalTasks > 0 ? (decimal)rows.Sum(r => r.TasksDone) / totalTasks : null, routeAll, rows.Sum(r => r.RouteVisited), routeAll > 0 ? (decimal)rows.Sum(r => r.RouteVisited) / routeAll : null,
            assignedAll > 0 ? Math.Min(1, rows.Sum(r => (r.MarketCoverage ?? 0) * r.AssignedMarkets) / assignedAll) : null, allVisits > 0 ? rows.Sum(r => r.Sum) / allVisits : null);
        return new FieldKpiView(y, mo, asOf, FieldKpi.ExpectedShare(asOf), total, rows, teams);
    }

    /// <summary>Точек за агентом: ответственный в Linko, с поправкой на переназначения в Sales Base.</summary>
    private async Task<Dictionary<Guid, int>> AssignedCountsAsync(IReadOnlyList<FieldMemberInfo> agents, CancellationToken ct)
    {
        var linkoOf = agents.Where(a => a.LinkoUserId is not null).ToDictionary(a => a.LinkoUserId!.Value, a => a.Id);
        var linkoIds = linkoOf.Keys.ToArray();
        var agentIds = agents.Select(a => a.Id).ToArray();
        var linkoCounts = await db.LinkoMarkets.AsNoTracking()
            .Where(m => m.ResponsibleAgentId != null && linkoIds.Contains(m.ResponsibleAgentId.Value))
            .GroupBy(m => m.ResponsibleAgentId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var overrides = await (
                from c in db.FieldCustomers.AsNoTracking()
                join m in db.LinkoMarkets.AsNoTracking() on c.MarketId equals m.Id
                where c.AssignedAgentId != null && ((m.ResponsibleAgentId != null && linkoIds.Contains(m.ResponsibleAgentId.Value)) || agentIds.Contains(c.AssignedAgentId.Value))
                select new { Assigned = c.AssignedAgentId!.Value, m.ResponsibleAgentId })
            .ToListAsync(ct);

        var result = linkoCounts.Where(kv => linkoOf.ContainsKey(kv.Key)).ToDictionary(kv => linkoOf[kv.Key], kv => kv.Value);
        foreach (var o in overrides)
        {
            var from = o.ResponsibleAgentId is { } r && linkoOf.TryGetValue(r, out var f) ? f : (Guid?)null;
            if (from == o.Assigned)
            {
                continue;
            }

            if (from is { } fromAgent)
            {
                result[fromAgent] = result.GetValueOrDefault(fromAgent) - 1;
            }

            if (agentIds.Contains(o.Assigned))
            {
                result[o.Assigned] = result.GetValueOrDefault(o.Assigned) + 1;
            }
        }

        return result;
    }

    public async Task<FieldAgentCard> AgentAsync(FieldScope scope, Guid agentId, DateOnly? date, CancellationToken ct)
    {
        if (!scope.CanSeeAgent(agentId))
        {
            throw new FieldNotFoundException("Агент не найден.");
        }

        var snapshot = await directory.GetAsync(ct);
        var agent = snapshot.Members[agentId];
        var day = date ?? FieldClock.Today;
        var today = await TodayAsync(scope, agentId, day, ct);
        var kpi = await KpiAsync(scope, day.Year, day.Month, null, null, ct, agentId); // только этот агент, а не вся зона
        var recent = await visits.ListAsync(scope, new FieldVisitQuery(agentId, null, day.AddDays(-14), day, null, null, 1, 20), ct);
        return new FieldAgentCard(agent, agent.TeamId is { } t ? snapshot.Teams.GetValueOrDefault(t)?.Name : null,
            snapshot.SupervisorOf(agentId) is { } s ? snapshot.NameOf(s) : null, today,
            kpi.Agents.FirstOrDefault(a => a.AgentId == agentId) ?? kpi.Total, recent.Items, scope.CanPlanFor(agentId));
    }
}
