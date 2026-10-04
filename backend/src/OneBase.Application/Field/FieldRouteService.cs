using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Domain.Audit;
using OneBase.Domain.Field;

namespace OneBase.Application.Field;

public sealed record FieldRoutePointView(
    Guid Id,
    int Sequence,
    long MarketId,
    string Name,
    string? Address,
    double? Lat,
    double? Lon,
    TimeOnly? PlannedTime,
    FieldPointStatus Status,
    bool LinkoDone,
    Guid? VisitId,
    DateTimeOffset? Arrival,
    DateTimeOffset? Departure,
    string? Note,
    FieldPriority Priority,
    int OpenTasks);

public sealed record FieldRouteView(
    Guid Id,
    DateOnly Date,
    Guid AgentId,
    string AgentName,
    FieldRouteStatus Status,
    FieldRouteSource Source,
    decimal DistanceKm,
    int EstimatedMinutes,
    int Planned,
    int Visited,
    int Skipped,
    int Remaining,
    Guid? NextPointId,
    bool CanEdit,
    IReadOnlyList<FieldRoutePointView> Points);

public sealed record FieldRouteSummary(Guid Id, DateOnly Date, Guid AgentId, string AgentName, string? TeamName, FieldRouteStatus Status, FieldRouteSource Source,
    int Planned, int Visited, int Skipped, decimal DistanceKm, int EstimatedMinutes);

/// <summary>
/// Маршруты: на агента и день. Строятся из открытых задач с точкой, плана визитов Linko на этот день и (если плана нет)
/// точек, которые пора посетить; порядок — ближайший сосед + 2-opt, время — дорога со средней скоростью + визит.
/// Посещённая в Linko точка показывается посещённой.
/// </summary>
public sealed class FieldRouteService(
    IAppDbContext db,
    FieldDirectory directory,
    FieldAssignments assignments,
    FieldSettingsStore settingsStore,
    ILinkoSalesProvider linko,
    FieldNotifier notifier,
    IAuditLogger audit)
{
    public async Task<List<FieldRouteSummary>> ListAsync(FieldScope scope, DateOnly date, Guid? agentId, Guid? teamId, CancellationToken ct)
    {
        var snapshot = await directory.GetAsync(ct);
        var agents = scope.AgentIds.Where(a => (agentId is null || a == agentId) && (teamId is null || snapshot.Members.GetValueOrDefault(a)?.TeamId == teamId)).ToArray();
        var routes = await db.FieldRoutes.AsNoTracking()
            .Where(r => r.Date == date && agents.Contains(r.AgentId))
            .Select(r => new { r.Id, r.Date, r.AgentId, r.Status, r.Source, r.DistanceKm, r.EstimatedMinutes, Points = r.Points.Select(p => new { p.MarketId, p.Status }).ToList() })
            .ToListAsync(ct);
        var linkoDone = await LinkoDoneAsync(snapshot, routes.Select(r => r.AgentId), date, ct);
        return routes
            .Select(r =>
            {
                var done = linkoDone.GetValueOrDefault(r.AgentId) ?? [];
                var visited = r.Points.Count(p => p.Status == FieldPointStatus.Visited || (p.Status == FieldPointStatus.Planned && done.Contains(p.MarketId)));
                var member = snapshot.Members.GetValueOrDefault(r.AgentId);
                var team = member?.TeamId is { } t ? snapshot.Teams.GetValueOrDefault(t)?.Name : null;
                return new FieldRouteSummary(r.Id, r.Date, r.AgentId, member?.FullName ?? "—", team, r.Status, r.Source,
                    r.Points.Count(p => p.Status != FieldPointStatus.Cancelled), visited, r.Points.Count(p => p.Status == FieldPointStatus.Skipped), r.DistanceKm, r.EstimatedMinutes);
            })
            .OrderBy(r => r.TeamName).ThenBy(r => r.AgentName)
            .ToList();
    }

    public async Task<FieldRouteView> GetAsync(FieldScope scope, Guid routeId, CancellationToken ct)
    {
        var route = await db.FieldRoutes.AsNoTracking().Include(r => r.Points).FirstOrDefaultAsync(r => r.Id == routeId, ct);
        if (route is null || !scope.CanSeeAgent(route.AgentId))
        {
            throw new FieldNotFoundException("Маршрут не найден.");
        }

        return await ViewAsync(scope, route, ct);
    }

    /// <summary>Маршрут агента на день или null, если его ещё нет.</summary>
    public async Task<FieldRouteView?> GetForAsync(FieldScope scope, Guid agentId, DateOnly date, CancellationToken ct)
    {
        RequireAgent(scope, agentId);
        var route = await db.FieldRoutes.AsNoTracking().Include(r => r.Points).FirstOrDefaultAsync(r => r.AgentId == agentId && r.Date == date, ct);
        return route is null ? null : await ViewAsync(scope, route, ct);
    }

    private async Task<FieldRouteView> ViewAsync(FieldScope scope, FieldRoute route, CancellationToken ct)
    {
        var snapshot = await directory.GetAsync(ct);
        var points = route.Points.Where(p => p.Status != FieldPointStatus.Cancelled).OrderBy(p => p.Sequence).ToList();
        var markets = await linko.MarketsAsync(points.Select(p => p.MarketId).ToList(), ct);
        var marketIds = points.Select(p => p.MarketId).ToArray();
        var priorities = await db.FieldCustomers.AsNoTracking().Where(c => marketIds.Contains(c.MarketId)).ToDictionaryAsync(c => c.MarketId, c => c.Priority, ct);
        var tasks = await db.FieldTasks.AsNoTracking()
            .Where(t => t.AssignedToId == route.AgentId && t.MarketId != null && marketIds.Contains(t.MarketId.Value) && FieldTaskRules.Open.Contains(t.Status))
            .GroupBy(t => t.MarketId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var done = (await LinkoDoneAsync(snapshot, [route.AgentId], route.Date, ct)).GetValueOrDefault(route.AgentId) ?? [];

        var views = points.Select(p =>
        {
            var m = markets.GetValueOrDefault(p.MarketId);
            var linkoDone = done.Contains(p.MarketId);
            var status = p.Status == FieldPointStatus.Planned && linkoDone ? FieldPointStatus.Visited : p.Status;
            return new FieldRoutePointView(p.Id, p.Sequence, p.MarketId, m?.Name ?? $"Точка {p.MarketId}", m?.Address, m?.Lat, m?.Lon, p.PlannedTime, status, linkoDone,
                p.VisitId, p.ActualArrival, p.ActualDeparture, p.Note, priorities.GetValueOrDefault(p.MarketId, FieldPriority.Medium), tasks.GetValueOrDefault(p.MarketId));
        }).ToList();

        var visited = views.Count(v => v.Status == FieldPointStatus.Visited);
        var skipped = views.Count(v => v.Status == FieldPointStatus.Skipped);
        var next = views.FirstOrDefault(v => v.Status == FieldPointStatus.InProgress) ?? views.FirstOrDefault(v => v.Status == FieldPointStatus.Planned);
        return new FieldRouteView(route.Id, route.Date, route.AgentId, snapshot.NameOf(route.AgentId), route.Status, route.Source, route.DistanceKm, route.EstimatedMinutes,
            views.Count, visited, skipped, views.Count - visited - skipped, next?.Id, scope.CanPlanFor(route.AgentId) || scope.MemberId == route.AgentId, views);
    }

    /// <summary>Точки, где у агента в этот день выполненный визит Linko.</summary>
    private async Task<Dictionary<Guid, HashSet<long>>> LinkoDoneAsync(FieldDirectory.Snapshot snapshot, IEnumerable<Guid> agentIds, DateOnly date, CancellationToken ct)
    {
        var byLinko = agentIds.Distinct()
            .Select(a => (Agent: a, Linko: snapshot.Members.GetValueOrDefault(a)?.LinkoUserId))
            .Where(x => x.Linko is not null)
            .ToDictionary(x => x.Linko!.Value, x => x.Agent);
        if (byLinko.Count == 0)
        {
            return [];
        }

        var visits = await linko.VisitsAsync(byLinko.Keys.ToList(), date, date, ct);
        return visits.Where(v => v.Done)
            .GroupBy(v => byLinko[v.LinkoUserId])
            .ToDictionary(g => g.Key, g => g.Select(v => v.MarketId).ToHashSet());
    }

    /// <summary>
    /// Построить маршрут на день: задачи с точкой → план визитов Linko → точки, которые пора посетить. Уже посещённые
    /// и начатые точки существующего маршрута сохраняются, остальные заменяются.
    /// </summary>
    public async Task<FieldRouteView> BuildAsync(FieldScope scope, Guid agentId, DateOnly date, double? startLat, double? startLon, CancellationToken ct)
    {
        RequirePlan(scope, agentId);
        var settings = await settingsStore.GetAsync(ct);
        var snapshot = await directory.GetAsync(ct);
        var agent = snapshot.Members.GetValueOrDefault(agentId) ?? throw new FieldNotFoundException("Агент не найден.");

        var chosen = new List<long>();
        var source = FieldRouteSource.Manual;

        // 1. Открытые задачи агента с точкой и сроком до этого дня.
        var taskMarkets = await db.FieldTasks.AsNoTracking()
            .Where(t => t.AssignedToId == agentId && t.MarketId != null && FieldTaskRules.Open.Contains(t.Status) && (t.DueDate == null || t.DueDate <= date))
            .OrderByDescending(t => (t.Priority == FieldPriority.Urgent ? 3 : t.Priority == FieldPriority.High ? 2 : t.Priority == FieldPriority.Medium ? 1 : 0)).ThenBy(t => t.DueDate)
            .Select(t => t.MarketId!.Value)
            .ToListAsync(ct);
        chosen.AddRange(taskMarkets);

        // 2. План визитов Linko на этот день.
        if (agent.LinkoUserId is { } linkoId)
        {
            var plan = (await linko.VisitsAsync([linkoId], date, date, ct)).Where(v => v.InPlan || v.Status == "pending").OrderBy(v => v.Date).Select(v => v.MarketId).ToList();
            if (plan.Count > 0)
            {
                source = FieldRouteSource.Linko;
                chosen.AddRange(plan);
            }
        }

        // 3. Плана нет — точки агента, которые пора посетить: давно без визита, с продажами, высокий приоритет.
        if (source != FieldRouteSource.Linko && chosen.Distinct().Count() < settings.MaxRoutePoints)
        {
            var due = await DueMarketsAsync(scope, agentId, date, settings.MaxRoutePoints - chosen.Distinct().Count(), chosen, ct);
            if (due.Count > 0)
            {
                source = FieldRouteSource.Ai;
                chosen.AddRange(due);
            }
        }

        var unique = chosen.Distinct().Take(Math.Max(settings.MaxRoutePoints, taskMarkets.Distinct().Count())).ToList();
        var route = await db.FieldRoutes.Include(r => r.Points).FirstOrDefaultAsync(r => r.AgentId == agentId && r.Date == date, ct);
        var isNew = route is null;
        // Агент строит маршрут себе, только если его ещё нет: перестроить готовый (в том числе от супервайзера) может только планирующий.
        if (route is not null && !scope.CanPlanFor(agentId) && route.Points.Any(p => p.Status != FieldPointStatus.Cancelled))
        {
            throw new FieldConflictException("Маршрут на этот день уже есть — перестроить его может супервайзер или РМ.");
        }

        route ??= new FieldRoute { AgentId = agentId, Date = date, SupervisorId = snapshot.SupervisorOf(agentId), CreatedById = scope.MemberId };
        if (isNew)
        {
            db.FieldRoutes.Add(route);
        }

        var kept = route.Points.Where(p => p.Status is FieldPointStatus.Visited or FieldPointStatus.InProgress or FieldPointStatus.Skipped).ToList();
        foreach (var p in route.Points.Except(kept).ToList())
        {
            route.Points.Remove(p);
            db.FieldRoutePoints.Remove(p);
        }

        foreach (var marketId in unique.Where(m => kept.All(k => k.MarketId != m)))
        {
            AddPoint(route, new FieldRoutePoint { MarketId = marketId, RouteId = route.Id });
        }

        route.Source = source;
        route.Status = route.Points.Any(p => p.Status is FieldPointStatus.Visited or FieldPointStatus.InProgress) ? FieldRouteStatus.InProgress : FieldRouteStatus.Planned;
        await RecomputeAsync(route, settings, startLat, startLon, reorder: true, ct);
        route.UpdatedAt = DateTimeOffset.UtcNow;
        if (scope.MemberId != agentId)
        {
            notifier.Notify(agentId, FieldNotificationKind.RouteChanged, $"Маршрут на {date:dd.MM}: {route.Points.Count} точек", null, $"/field/routes/{route.Id}");
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, scope.UserId.ToString(), "field.route.built", "field_route", route.Id.ToString(),
            new { agentId, date, points = route.Points.Count, source = source.ToString() }, ct);
        return await GetAsync(scope, route.Id, ct);
    }

    /// <summary>Точки агента, которые пора посетить: давно без визита Linko, с продажами, сначала приоритетные и крупные.</summary>
    private async Task<List<long>> DueMarketsAsync(FieldScope scope, Guid agentId, DateOnly date, int take, IReadOnlyCollection<long> exclude, CancellationToken ct)
    {
        if (take <= 0)
        {
            return [];
        }

        var agentScope = new FieldScope
        {
            UserId = scope.UserId,
            MemberId = agentId,
            Role = FieldRole.Agent,
            CanManage = false,
            OrgWide = false,
            AgentIds = new HashSet<Guid> { agentId },
            MemberIds = new HashSet<Guid> { agentId },
            TeamIds = new HashSet<Guid>(),
            LinkoOf = scope.LinkoOf.Where(kv => kv.Key == agentId).ToDictionary(kv => kv.Key, kv => kv.Value),
        };
        var recent = date.AddDays(-3);
        var candidates = await (
                from m in assignments.MarketsInScope(agentScope)
                join s in db.FieldCustomerStats on m.Id equals s.MarketId into ss
                from s in ss.DefaultIfEmpty()
                join c in db.FieldCustomers on m.Id equals c.MarketId into cs
                from c in cs.DefaultIfEmpty()
                where (s == null || s.LastVisitDate == null || s.LastVisitDate < recent) && (c == null || c.Status != FieldCustomerStatus.Inactive)
                select new { m.Id, m.Lat, m.Lon, LastVisit = s == null ? null : s.LastVisitDate, Sales90 = s == null ? 0 : s.Sales90, Priority = c == null ? FieldPriority.Medium : c.Priority })
            .ToListAsync(ct);

        return candidates
            .Where(c => !exclude.Contains(c.Id) && FieldGeo.HasCoordinates(c.Lat, c.Lon))
            .Select(c => new
            {
                c.Id,
                Score = (c.LastVisit is { } lv ? Math.Min(date.DayNumber - lv.DayNumber, 60) : 45) * 2
                        + (double)Math.Min(c.Sales90 / 1_000_000m, 30)
                        + (int)c.Priority * 15,
            })
            .OrderByDescending(c => c.Score)
            .Take(take)
            .Select(c => c.Id)
            .ToList();
    }

    /// <summary>
    /// Изменить состав и порядок точек: marketIds — новый порядок непосещённых точек. Посещённые и начатые остаются на местах.
    /// </summary>
    public async Task<FieldRouteView> UpdatePointsAsync(FieldScope scope, Guid routeId, IReadOnlyList<long> marketIds, CancellationToken ct)
    {
        var route = await db.FieldRoutes.Include(r => r.Points).FirstOrDefaultAsync(r => r.Id == routeId, ct);
        if (route is null || !scope.CanSeeAgent(route.AgentId))
        {
            throw new FieldNotFoundException("Маршрут не найден.");
        }

        if (!scope.CanPlanFor(route.AgentId))
        {
            throw new FieldForbiddenException("Состав маршрута меняет супервайзер команды или РМ.");
        }

        if (marketIds.Count > 200)
        {
            throw new FieldValidationException("В маршруте — не больше 200 точек.");
        }

        // Точки, которые уже стоят в маршруте (например, из плана визитов Linko вне зоны), можно оставить и переставить; новые — только из зоны.
        var allowed = await assignments.MarketsInScope(scope).Where(m => marketIds.Contains(m.Id)).Select(m => m.Id).ToListAsync(ct);
        allowed.AddRange(route.Points.Where(p => p.Status != FieldPointStatus.Cancelled).Select(p => p.MarketId));
        if (marketIds.Except(allowed).Any())
        {
            throw new FieldValidationException("Часть точек вне вашей зоны.");
        }

        var settings = await settingsStore.GetAsync(ct);
        var locked = route.Points.Where(p => p.Status is FieldPointStatus.Visited or FieldPointStatus.InProgress or FieldPointStatus.Skipped).ToList();
        var open = route.Points.Except(locked).ToList();
        foreach (var p in open.Where(p => !marketIds.Contains(p.MarketId)))
        {
            route.Points.Remove(p);
            db.FieldRoutePoints.Remove(p);
        }

        var seq = locked.Count == 0 ? 0 : locked.Max(p => p.Sequence);
        foreach (var marketId in marketIds.Distinct().Where(m => locked.All(l => l.MarketId != m)))
        {
            var point = route.Points.FirstOrDefault(p => p.MarketId == marketId && !locked.Contains(p));
            if (point is null)
            {
                point = new FieldRoutePoint { MarketId = marketId, RouteId = route.Id };
                AddPoint(route, point);
            }

            point.Sequence = ++seq;
        }

        await RecomputeAsync(route, settings, null, null, reorder: false, ct);
        route.UpdatedAt = DateTimeOffset.UtcNow;
        if (scope.MemberId != route.AgentId)
        {
            notifier.Notify(route.AgentId, FieldNotificationKind.RouteChanged, $"Супервайзер изменил маршрут на {route.Date:dd.MM}", null, $"/field/routes/{route.Id}");
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, scope.UserId.ToString(), "field.route.changed", "field_route", route.Id.ToString(),
            new { points = route.Points.Count }, ct);
        return await GetAsync(scope, route.Id, ct);
    }

    /// <summary>Переставить непосещённые точки в оптимальном порядке (от текущей позиции агента, если она известна).</summary>
    public async Task<FieldRouteView> OptimizeAsync(FieldScope scope, Guid routeId, double? startLat, double? startLon, CancellationToken ct)
    {
        var route = await db.FieldRoutes.Include(r => r.Points).FirstOrDefaultAsync(r => r.Id == routeId, ct);
        if (route is null || !scope.CanSeeAgent(route.AgentId) || !(scope.CanPlanFor(route.AgentId) || scope.MemberId == route.AgentId))
        {
            throw new FieldNotFoundException("Маршрут не найден.");
        }

        await RecomputeAsync(route, await settingsStore.GetAsync(ct), startLat, startLon, reorder: true, ct);
        route.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetAsync(scope, route.Id, ct);
    }

    public async Task<FieldRouteView> SkipPointAsync(FieldScope scope, Guid pointId, string? note, CancellationToken ct)
    {
        var point = await db.FieldRoutePoints.Include(p => p.Route).FirstOrDefaultAsync(p => p.Id == pointId, ct);
        if (point?.Route is null || !scope.CanSeeAgent(point.Route.AgentId) || !(scope.CanPlanFor(point.Route.AgentId) || scope.MemberId == point.Route.AgentId))
        {
            throw new FieldNotFoundException("Точка маршрута не найдена.");
        }

        if (point.Status is FieldPointStatus.Visited or FieldPointStatus.InProgress)
        {
            throw new FieldValidationException("Точка уже посещена или визит идёт.");
        }

        point.Status = FieldPointStatus.Skipped;
        point.Note = string.IsNullOrWhiteSpace(note) ? point.Note : note.Trim()[..Math.Min(note.Trim().Length, 1000)];
        await CloseIfDoneAsync(point.RouteId, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(scope, point.RouteId, ct);
    }

    /// <summary>Маршрут завершён, когда не осталось запланированных точек.</summary>
    public async Task CloseIfDoneAsync(Guid routeId, CancellationToken ct)
    {
        var route = await db.FieldRoutes.Include(r => r.Points).FirstOrDefaultAsync(r => r.Id == routeId, ct);
        if (route is null)
        {
            return;
        }

        var active = route.Points.Where(p => p.Status != FieldPointStatus.Cancelled).ToList();
        if (active.Count > 0 && active.All(p => p.Status is FieldPointStatus.Visited or FieldPointStatus.Skipped))
        {
            route.Status = FieldRouteStatus.Completed;
        }
        else if (active.Any(p => p.Status is FieldPointStatus.Visited or FieldPointStatus.InProgress or FieldPointStatus.Skipped))
        {
            route.Status = FieldRouteStatus.InProgress;
        }
    }

    /// <summary>Убрать точку из будущих (и сегодняшних) непосещённых маршрутов агента — при смене агента.</summary>
    public async Task<int> RemoveMarketAsync(Guid agentId, long marketId, DateOnly fromDate, CancellationToken ct)
    {
        var routes = await db.FieldRoutes.Include(r => r.Points)
            .Where(r => r.AgentId == agentId && r.Date >= fromDate && r.Points.Any(p => p.MarketId == marketId && p.Status == FieldPointStatus.Planned))
            .ToListAsync(ct);
        var settings = await settingsStore.GetAsync(ct);
        foreach (var route in routes)
        {
            foreach (var p in route.Points.Where(p => p.MarketId == marketId && p.Status == FieldPointStatus.Planned).ToList())
            {
                route.Points.Remove(p);
                db.FieldRoutePoints.Remove(p);
            }

            await RecomputeAsync(route, settings, null, null, reorder: false, ct);
            route.UpdatedAt = DateTimeOffset.UtcNow;
        }

        return routes.Count;
    }

    /// <summary>Добавить точку в маршрут агента на день (если он есть) — в место с наименьшим удлинением пути.</summary>
    public async Task<bool> InsertMarketAsync(Guid agentId, long marketId, DateOnly date, CancellationToken ct)
    {
        var route = await db.FieldRoutes.Include(r => r.Points).FirstOrDefaultAsync(r => r.AgentId == agentId && r.Date == date, ct);
        if (route is null || route.Points.Any(p => p.MarketId == marketId && p.Status != FieldPointStatus.Cancelled))
        {
            return false;
        }

        var open = route.Points.Where(p => p.Status == FieldPointStatus.Planned).OrderBy(p => p.Sequence).ToList();
        var markets = await linko.MarketsAsync(open.Select(p => p.MarketId).Append(marketId).ToList(), ct);
        FieldRouting.Stop Stop(long id) => markets.TryGetValue(id, out var m) ? new FieldRouting.Stop(id, m.Lat, m.Lon) : new FieldRouting.Stop(id, null, null);
        var index = FieldRouting.InsertionIndex(open.Select(p => Stop(p.MarketId)).ToList(), Stop(marketId));
        var point = new FieldRoutePoint { MarketId = marketId, RouteId = route.Id };
        open.Insert(index, point);
        AddPoint(route, point);
        var seq = route.Points.Where(p => p.Status != FieldPointStatus.Planned).Select(p => p.Sequence).DefaultIfEmpty(0).Max();
        foreach (var p in open)
        {
            p.Sequence = ++seq;
        }

        await RecomputeAsync(route, await settingsStore.GetAsync(ct), null, null, reorder: false, ct);
        route.UpdatedAt = DateTimeOffset.UtcNow;
        return true;
    }

    /// <summary>
    /// Порядок, номера, плановое время, длина и длительность. Посещённые и начатые точки идут первыми в своём порядке,
    /// запланированные — после (переупорядочиваются, если reorder).
    /// </summary>
    private async Task RecomputeAsync(FieldRoute route, FieldSettings settings, double? startLat, double? startLon, bool reorder, CancellationToken ct)
    {
        var active = route.Points.Where(p => p.Status != FieldPointStatus.Cancelled).ToList();
        var markets = await linko.MarketsAsync(active.Select(p => p.MarketId).ToList(), ct);
        FieldRouting.Stop Stop(FieldRoutePoint p) => markets.TryGetValue(p.MarketId, out var m) ? new FieldRouting.Stop(p.MarketId, m.Lat, m.Lon) : new FieldRouting.Stop(p.MarketId, null, null);

        var done = active.Where(p => p.Status != FieldPointStatus.Planned).OrderBy(p => p.Sequence).ToList();
        var planned = active.Where(p => p.Status == FieldPointStatus.Planned).OrderBy(p => p.Sequence).ToList();
        if (reorder && planned.Count > 1)
        {
            var last = done.LastOrDefault(p => p.Status is FieldPointStatus.Visited or FieldPointStatus.InProgress);
            var (sLat, sLon) = FieldGeo.HasCoordinates(startLat, startLon) ? (startLat, startLon)
                : last is not null && markets.TryGetValue(last.MarketId, out var lm) ? (lm.Lat, lm.Lon) : ((double?)null, (double?)null);
            var ordered = FieldRouting.Order(planned.Select(Stop).ToList(), sLat, sLon);
            planned = ordered.Select(s => planned.First(p => p.MarketId == s.MarketId)).ToList();
        }

        var all = done.Concat(planned).ToList();
        for (var i = 0; i < all.Count; i++)
        {
            all[i].Sequence = i + 1;
        }

        var stops = all.Select(Stop).ToList();
        var (times, minutes) = FieldRouting.Schedule(stops, settings.DayStart, settings.VisitMinutes, settings.TravelSpeedKmh);
        for (var i = 0; i < all.Count; i++)
        {
            if (all[i].Status == FieldPointStatus.Planned)
            {
                all[i].PlannedTime = times[i];
            }
        }

        route.DistanceKm = FieldRouting.LengthKm(stops);
        route.EstimatedMinutes = minutes;
    }

    /// <summary>
    /// Новая точка — явно как добавляемая: ключ (Guid v7) задан при создании, и через коллекцию уже сохранённого маршрута
    /// EF принял бы её за существующую запись (UPDATE вместо INSERT).
    /// </summary>
    private void AddPoint(FieldRoute route, FieldRoutePoint point)
    {
        route.Points.Add(point);
        db.FieldRoutePoints.Add(point);
    }

    private static void RequireAgent(FieldScope scope, Guid agentId)
    {
        if (!scope.CanSeeAgent(agentId))
        {
            throw new FieldNotFoundException("Агент не найден.");
        }
    }

    private static void RequirePlan(FieldScope scope, Guid agentId)
    {
        RequireAgent(scope, agentId);
        if (!scope.CanPlanFor(agentId) && scope.MemberId != agentId)
        {
            throw new FieldForbiddenException("Менять маршрут может супервайзер команды или РМ.");
        }
    }
}
