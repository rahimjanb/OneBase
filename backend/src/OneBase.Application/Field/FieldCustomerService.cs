using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Domain.Audit;
using OneBase.Domain.Field;

namespace OneBase.Application.Field;

public sealed record FieldCustomerRow(
    long MarketId,
    string Name,
    string? Address,
    string? Branch,
    string? Type,
    double? Lat,
    double? Lon,
    Guid? AgentId,
    string? AgentName,
    FieldPriority Priority,
    FieldCustomerStatus Status,
    DateOnly? LastVisitDate,
    DateOnly? LastOrderDate,
    decimal Sales7,
    decimal Sales30,
    decimal Sales90,
    decimal? TrendPct,
    decimal? MonthlyTarget);

public sealed record FieldCustomerQuery(
    string? Search,
    Guid? AgentId,
    Guid? TeamId,
    long? BranchId,
    FieldCustomerStatus? Status,
    FieldPriority? Priority,
    string? Attention,
    string? Sort,
    int Page = 1,
    int PageSize = 50);

public sealed record FieldVisitHistoryRow(string Source, DateTimeOffset At, string? Agent, string Status, string? Result, string? Comment, double? DistanceM, Guid? VisitId);

public sealed record FieldOrderRow(long Id, DateOnly Date, DateOnly? AcceptedDate, string Status, string? Agent, decimal Sum, decimal Kg);

public sealed record FieldMonthSales(int Year, int Month, decimal Sum, decimal Kg, int Orders);

public sealed record FieldCustomerCard(
    FieldCustomerRow Customer,
    long? LinkoResponsibleId,
    string? LinkoResponsibleName,
    Guid? SupervisorId,
    string? SupervisorName,
    string? ContactPerson,
    string? Phone,
    string? Note,
    decimal MonthToDate,
    decimal? MonthShare,
    int Orders30,
    int Visits30,
    IReadOnlyList<FieldMonthSales> Months,
    IReadOnlyList<FieldOrderRow> Orders,
    IReadOnlyList<FieldVisitHistoryRow> Visits,
    IReadOnlyList<FieldTaskRow> Tasks,
    IReadOnlyList<FieldRecommendationRow> Recommendations,
    bool CanEdit,
    bool CanChangeAgent,
    IReadOnlyList<FieldAgentOption> AgentOptions);

public sealed record FieldAgentOption(Guid Id, string Name, string? TeamName);

public sealed record FieldCustomerInput(FieldPriority? Priority, FieldCustomerStatus? Status, decimal? MonthlyTarget, string? ContactPerson, string? Phone, string? Note);

public sealed record FieldMapPoint(long MarketId, string Name, double Lat, double Lon, string State, Guid? AgentId, int? Sequence, FieldPriority Priority, decimal Sales30);

public sealed record FieldMapAgent(Guid AgentId, string Name, double Lat, double Lon, DateTimeOffset At);

public sealed record FieldMapRoute(Guid RouteId, Guid AgentId, string AgentName, IReadOnlyList<long> MarketIds);

public sealed record FieldMapView(DateOnly Date, IReadOnlyList<FieldMapPoint> Points, IReadOnlyList<FieldMapRoute> Routes, IReadOnlyList<FieldMapAgent> Agents, bool Truncated, int Total);

/// <summary>
/// Торговые точки = точки Linko + оверлей Sales Base. Списки фильтруются и сортируются на сервере по витрине
/// field.CustomerStats; карта отдаёт не больше MapLimit точек в видимой области.
/// </summary>
public sealed class FieldCustomerService(
    IAppDbContext db,
    FieldDirectory directory,
    FieldAssignments assignments,
    FieldRouteService routes,
    ILinkoSalesProvider linko,
    FieldNotifier notifier,
    IAuditLogger audit)
{
    public const int MapLimit = 1500;

    /// <summary>Точка + витрина + оверлей. Класс с init-свойствами: EF умеет продолжать запрос по member-init, а не по конструктору.</summary>
    private sealed class Joined
    {
        public required Domain.Sales.LinkoMarket M { get; init; }
        public FieldCustomerStats? S { get; init; }
        public FieldCustomer? C { get; init; }
    }

    private IQueryable<Joined> Query(FieldScope scope) => Query(assignments.MarketsInScope(scope));

    private IQueryable<Joined> Query(IQueryable<Domain.Sales.LinkoMarket> markets) =>
        from m in markets
        join s in db.FieldCustomerStats.AsNoTracking() on m.Id equals s.MarketId into ss
        from s in ss.DefaultIfEmpty()
        join c in db.FieldCustomers.AsNoTracking() on m.Id equals c.MarketId into cs
        from c in cs.DefaultIfEmpty()
        select new Joined { M = m, S = s, C = c };

    public async Task<PageResult<FieldCustomerRow>> ListAsync(FieldScope scope, FieldCustomerQuery query, CancellationToken ct)
    {
        var snapshot = await directory.GetAsync(ct);
        var q = Query(scope);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var text = query.Search.Trim().ToLower();
            q = long.TryParse(text, out var id)
                ? q.Where(x => x.M.Id == id || x.M.Name.ToLower().Contains(text))
                : q.Where(x => x.M.Name.ToLower().Contains(text) || (x.M.Address != null && x.M.Address.ToLower().Contains(text)));
        }

        var agentFilter = query.AgentId is { } aid ? new[] { aid }
            : query.TeamId is { } tid ? snapshot.Members.Values.Where(m => m.TeamId == tid && scope.CanSeeAgent(m.Id)).Select(m => m.Id).ToArray()
            : null;
        if (agentFilter is not null)
        {
            var linkoIds = agentFilter.Where(scope.LinkoOf.ContainsKey).Select(a => scope.LinkoOf[a]).ToArray();
            q = q.Where(x => (x.C != null && x.C.AssignedAgentId != null && agentFilter.Contains(x.C.AssignedAgentId.Value))
                             || ((x.C == null || x.C.AssignedAgentId == null) && x.M.ResponsibleAgentId != null && linkoIds.Contains(x.M.ResponsibleAgentId.Value)));
        }

        if (query.BranchId is { } branch)
        {
            q = q.Where(x => x.M.BranchId == branch);
        }

        if (query.Status is { } status)
        {
            q = status == FieldCustomerStatus.Active ? q.Where(x => x.C == null || x.C.Status == FieldCustomerStatus.Active) : q.Where(x => x.C != null && x.C.Status == status);
        }

        if (query.Priority is { } priority)
        {
            q = priority == FieldPriority.Medium ? q.Where(x => x.C == null || x.C.Priority == FieldPriority.Medium) : q.Where(x => x.C != null && x.C.Priority == priority);
        }

        var today = FieldClock.Today;
        q = query.Attention switch
        {
            "decline" => q.Where(x => x.S != null && x.S.SalesPrev30 > 0 && x.S.Sales30 < x.S.SalesPrev30 * 0.7m),
            "lost" => q.Where(x => x.S != null && x.S.Sales30 == 0 && x.S.SalesPrev30 > 0),
            "notvisited" => q.Where(x => x.S != null && x.S.Sales90 > 0 && (x.S.LastVisitDate == null || x.S.LastVisitDate < today.AddDays(-14))),
            "new" => q.Where(x => x.S == null || x.S.LastOrderDate == null),
            _ => q,
        };

        var total = await q.CountAsync(ct);
        q = query.Sort switch
        {
            "name" => q.OrderBy(x => x.M.Name),
            "lastvisit" => q.OrderBy(x => x.S == null ? null : x.S.LastVisitDate).ThenBy(x => x.M.Name),
            "trend" => q.OrderBy(x => x.S == null || x.S.SalesPrev30 == 0 ? 0 : (x.S.Sales30 - x.S.SalesPrev30) / x.S.SalesPrev30).ThenBy(x => x.M.Name),
            "sales90" => q.OrderByDescending(x => x.S == null ? 0 : x.S.Sales90).ThenBy(x => x.M.Name),
            _ => q.OrderByDescending(x => x.S == null ? 0 : x.S.Sales30).ThenBy(x => x.M.Name),
        };

        var pageSize = Math.Clamp(query.PageSize, 10, 200);
        var page = Math.Max(query.Page, 1);
        var rows = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var agents = await assignments.EffectiveAgentsAsync(rows.Select(r => (r.M.Id, r.M.ResponsibleAgentId)), ct);
        return new PageResult<FieldCustomerRow>(rows.Select(r => Row(r, agents.GetValueOrDefault(r.M.Id), snapshot)).ToList(), total, page, pageSize);
    }

    private static FieldCustomerRow Row(Joined r, Guid? agentId, FieldDirectory.Snapshot snapshot) => new(
        r.M.Id,
        r.M.Name,
        r.M.Address,
        r.M.BranchName,
        r.M.MarketTypeName,
        r.M.Lat,
        r.M.Lon,
        agentId,
        agentId is { } a ? snapshot.NameOf(a) : null,
        r.C?.Priority ?? FieldPriority.Medium,
        r.C?.Status ?? FieldCustomerStatus.Active,
        r.S?.LastVisitDate,
        r.S?.LastOrderDate,
        r.S?.Sales7 ?? 0,
        r.S?.Sales30 ?? 0,
        r.S?.Sales90 ?? 0,
        r.S is { SalesPrev30: > 0 } s ? Math.Round((s.Sales30 - s.SalesPrev30) / s.SalesPrev30 * 100, 1) : null,
        r.C?.MonthlyTarget);

    public async Task<FieldCustomerCard> GetAsync(FieldScope scope, long marketId, FieldTaskService tasks, FieldRecommendationService recommendations, CancellationToken ct)
    {
        var snapshot = await directory.GetAsync(ct);
        var row = await Query(scope).FirstOrDefaultAsync(x => x.M.Id == marketId, ct)
                  ?? (await assignments.IsRoutePointAsync(scope, marketId, ct) ? await Query(db.LinkoMarkets.AsNoTracking()).FirstOrDefaultAsync(x => x.M.Id == marketId, ct) : null)
                  ?? throw new FieldNotFoundException("Точка не найдена.");
        var agentId = (await assignments.EffectiveAgentsAsync([(row.M.Id, row.M.ResponsibleAgentId)], ct)).GetValueOrDefault(row.M.Id);
        var supervisorId = agentId is { } a ? snapshot.SupervisorOf(a) : null;
        var today = FieldClock.Today;
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var orders = await linko.MarketOrdersAsync(marketId, today.AddMonths(-6).AddDays(1 - today.Day), ct);
        var userNames = await linko.UserNamesAsync(orders.Where(o => o.AgentId is not null).Select(o => o.AgentId!.Value).Distinct().ToList(), ct);
        var delivered = orders.Where(o => o.Status == "delivered" && o.AcceptedDate is not null).ToList();
        var months = Enumerable.Range(0, 6)
            .Select(i => monthStart.AddMonths(-i))
            .Select(d => new FieldMonthSales(d.Year, d.Month,
                delivered.Where(o => o.AcceptedDate!.Value.Year == d.Year && o.AcceptedDate.Value.Month == d.Month).Sum(o => o.Sum),
                delivered.Where(o => o.AcceptedDate!.Value.Year == d.Year && o.AcceptedDate.Value.Month == d.Month).Sum(o => o.Kg),
                delivered.Count(o => o.AcceptedDate!.Value.Year == d.Year && o.AcceptedDate.Value.Month == d.Month)))
            .Reverse()
            .ToList();
        var monthToDate = months[^1].Sum;

        // Визиты Sales Base — только участников своей зоны: заметки и результаты визитов других команд не показываем.
        var visitors = scope.MemberIds.ToArray();
        var ownVisits = await db.FieldVisits.AsNoTracking().Where(v => v.MarketId == marketId && (scope.OrgWide || visitors.Contains(v.AgentId)))
            .OrderByDescending(v => v.StartedAt).Take(20).ToListAsync(ct);
        var linkoVisits = await linko.MarketVisitsAsync(marketId, 20, ct);
        var visitNames = await linko.UserNamesAsync(linkoVisits.Select(v => v.LinkoUserId).Distinct().ToList(), ct);
        var visits = ownVisits
            .Select(v => new FieldVisitHistoryRow("Sales Base", v.StartedAt, snapshot.NameOf(v.AgentId), v.Status.ToString(), v.Result?.ToString(), v.Comment, v.DistanceM, v.Id))
            .Concat(linkoVisits.Select(v => new FieldVisitHistoryRow("Linko", new DateTimeOffset(v.Date, FieldClock.Offset), visitNames.GetValueOrDefault(v.LinkoUserId),
                v.Done ? "Выполнен" : v.Status == "pending" ? "Запланирован" : "Не выполнен", v.InPlan ? "в плане" : "вне плана", null, null, null)))
            .OrderByDescending(v => v.At)
            .Take(30)
            .ToList();

        var canEdit = agentId is { } ag ? scope.CanPlanFor(ag) || scope.MemberId == ag : scope.CanPlan;
        var options = scope.CanPlan
            ? scope.AgentIds.Select(id => snapshot.Members.GetValueOrDefault(id)).Where(m => m is { IsActive: true })
                .Select(m => new FieldAgentOption(m!.Id, m.FullName, m.TeamId is { } t ? snapshot.Teams.GetValueOrDefault(t)?.Name : null))
                .OrderBy(o => o.TeamName).ThenBy(o => o.Name).ToList()
            : [];

        return new FieldCustomerCard(
            Row(row, agentId, snapshot),
            row.M.ResponsibleAgentId,
            row.M.ResponsibleAgentId is { } r ? (await linko.UserNamesAsync([r], ct)).GetValueOrDefault(r) : null,
            supervisorId,
            supervisorId is { } s ? snapshot.NameOf(s) : null,
            row.C?.ContactPerson,
            row.C?.Phone,
            row.C?.Note,
            monthToDate,
            row.C?.MonthlyTarget is > 0 ? monthToDate / row.C.MonthlyTarget.Value : null,
            row.S?.Orders30 ?? 0,
            row.S?.Visits30 ?? 0,
            months,
            orders.Take(30).Select(o => new FieldOrderRow(o.Id, o.CreatedDate, o.AcceptedDate, o.Status, o.AgentId is { } oa ? userNames.GetValueOrDefault(oa) : null, o.Sum, o.Kg)).ToList(),
            visits,
            await tasks.ForMarketAsync(scope, marketId, ct),
            await recommendations.ForMarketAsync(scope, marketId, ct),
            canEdit,
            scope.CanPlan && (agentId is null || scope.CanPlanFor(agentId.Value)),
            options);
    }

    /// <summary>Приоритет, статус и план меняет супервайзер или РМ; контакт, телефон и заметку — и агент точки.</summary>
    public async Task UpdateAsync(FieldScope scope, long marketId, FieldCustomerInput input, CancellationToken ct)
    {
        var market = await assignments.RequireMarketAsync(scope, marketId, ct);
        var agentId = (await assignments.EffectiveAgentsAsync([(market.Id, market.ResponsibleAgentId)], ct)).GetValueOrDefault(market.Id);
        var planner = agentId is { } a ? scope.CanPlanFor(a) : scope.CanPlan;
        var owner = agentId is not null && scope.MemberId == agentId;
        if (!planner && !owner)
        {
            throw new FieldForbiddenException("Менять карточку точки может её агент, супервайзер или РМ.");
        }

        if ((input.Priority is not null || input.Status is not null || input.MonthlyTarget is not null) && !planner)
        {
            throw new FieldForbiddenException("Приоритет, статус и план точки меняет супервайзер или РМ.");
        }

        if (input.MonthlyTarget is < 0 or > 100_000_000_000)
        {
            throw new FieldValidationException("План точки — от 0 до 100 млрд сум.");
        }

        var customer = await db.FieldCustomers.FirstOrDefaultAsync(c => c.MarketId == marketId, ct);
        if (customer is null)
        {
            customer = new FieldCustomer { MarketId = marketId };
            db.FieldCustomers.Add(customer);
        }

        if (input.Priority is { } p)
        {
            customer.Priority = p;
        }

        if (input.Status is { } st)
        {
            customer.Status = st;
        }

        if (input.MonthlyTarget is not null)
        {
            customer.MonthlyTarget = input.MonthlyTarget == 0 ? null : input.MonthlyTarget;
        }

        customer.ContactPerson = Clean(input.ContactPerson, 200) ?? customer.ContactPerson;
        customer.Phone = Clean(input.Phone, 32) ?? customer.Phone;
        customer.Note = input.Note is null ? customer.Note : Clean(input.Note, 2000);
        customer.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, scope.UserId.ToString(), "field.customer.updated", "field_customer", marketId.ToString(),
            new { priority = input.Priority?.ToString(), status = input.Status?.ToString(), input.MonthlyTarget }, ct);
    }

    public sealed record ChangeAgentResult(Guid? FromAgentId, string? FromAgent, Guid ToAgentId, string ToAgent, int RoutesUpdated, bool AddedToRoute);

    /// <summary>
    /// Сменить агента точки: назначение в Sales Base, точка уходит из непосещённых маршрутов прежнего агента
    /// (сегодня и дальше) и встаёт в сегодняшний маршрут нового, если он есть; уведомления обоим и запись в журнал.
    /// В Linko ответственный агент не меняется — интеграция только читает.
    /// </summary>
    /// <param name="orphanAllowed">По рекомендации «точка без агента»: точку без действующего агента в филиале команд можно назначить, хоть она и вне области.</param>
    public async Task<ChangeAgentResult> ChangeAgentAsync(FieldScope scope, long marketId, Guid newAgentId, CancellationToken ct, bool orphanAllowed = false)
    {
        var market = await assignments.MarketsInScope(scope).FirstOrDefaultAsync(m => m.Id == marketId, ct);
        var orphan = false;
        if (market is null && orphanAllowed)
        {
            market = await assignments.OrphanInScopeAsync(scope, marketId, ct);
            orphan = market is not null;
        }

        if (market is null)
        {
            throw new FieldNotFoundException("Точка не найдена.");
        }
        if (!scope.CanPlanFor(newAgentId))
        {
            throw new FieldForbiddenException("Назначить точку можно только агенту своей команды.");
        }

        var snapshot = await directory.GetAsync(ct);
        if (snapshot.Members.GetValueOrDefault(newAgentId) is not { IsActive: true, Role: FieldRole.Agent } newAgent)
        {
            throw new FieldValidationException("Новый агент не найден или отключён.");
        }

        var oldAgentId = (await assignments.EffectiveAgentsAsync([(market.Id, market.ResponsibleAgentId)], ct)).GetValueOrDefault(market.Id);
        if (oldAgentId is { } old && !scope.CanPlanFor(old) && !scope.OrgWide && !orphan)
        {
            throw new FieldForbiddenException("Точку ведёт агент другой команды.");
        }

        if (oldAgentId == newAgentId)
        {
            throw new FieldValidationException("Точку уже ведёт этот агент.");
        }

        var customer = await db.FieldCustomers.FirstOrDefaultAsync(c => c.MarketId == marketId, ct);
        if (customer is null)
        {
            customer = new FieldCustomer { MarketId = marketId };
            db.FieldCustomers.Add(customer);
        }

        customer.AssignedAgentId = newAgentId;
        customer.UpdatedAt = DateTimeOffset.UtcNow;

        var today = FieldClock.Today;
        var updated = oldAgentId is { } from ? await routes.RemoveMarketAsync(from, marketId, today, ct) : 0;
        var added = await routes.InsertMarketAsync(newAgentId, marketId, today, ct);

        // Открытые задачи по точке переходят к новому агенту.
        var openTasks = await db.FieldTasks.Where(t => t.MarketId == marketId && oldAgentId != null && t.AssignedToId == oldAgentId && FieldTaskRules.Open.Contains(t.Status)).ToListAsync(ct);
        foreach (var t in openTasks)
        {
            t.AssignedToId = newAgentId;
            t.SupervisorId = snapshot.SupervisorOf(newAgentId);
            t.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var actor = scope.MemberId is { } me ? snapshot.NameOf(me) : "Администратор";
        notifier.Notify(newAgentId, FieldNotificationKind.AgentChanged, $"Вам назначена точка «{market.Name}»", $"Назначил: {actor}.{(added ? " Точка добавлена в сегодняшний маршрут." : string.Empty)}", $"/field/customers/{marketId}");
        notifier.Notify(oldAgentId, FieldNotificationKind.AgentChanged, $"Точка «{market.Name}» передана агенту {newAgent.FullName}", $"Изменил: {actor}.", "/field/customers");
        var newSupervisor = snapshot.SupervisorOf(newAgentId);
        if (newSupervisor != scope.MemberId)
        {
            notifier.Notify(newSupervisor, FieldNotificationKind.AgentChanged, $"Точка «{market.Name}» передана агенту {newAgent.FullName}", $"Изменил: {actor}.", $"/field/customers/{marketId}");
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, scope.UserId.ToString(), "field.customer.agent_changed", "field_customer", marketId.ToString(),
            new { customer = market.Name, from = oldAgentId, fromName = oldAgentId is { } f ? snapshot.NameOf(f) : null, to = newAgentId, toName = newAgent.FullName, routes = updated, tasks = openTasks.Count }, ct);
        return new ChangeAgentResult(oldAgentId, oldAgentId is { } o ? snapshot.NameOf(o) : null, newAgentId, newAgent.FullName, updated, added);
    }

    /// <summary>
    /// Карта: точки маршрутов на день (с номерами и статусами), точки в видимой области (не больше MapLimit, крупные первыми),
    /// последние известные позиции агентов (координаты визитов Sales Base за день).
    /// </summary>
    public async Task<FieldMapView> MapAsync(FieldScope scope, DateOnly date, Guid? agentId, Guid? teamId, (double South, double West, double North, double East)? bbox, CancellationToken ct)
    {
        var snapshot = await directory.GetAsync(ct);
        var agents = scope.AgentIds.Where(a => (agentId is null || a == agentId) && (teamId is null || snapshot.Members.GetValueOrDefault(a)?.TeamId == teamId)).ToArray();

        var dayRoutes = await db.FieldRoutes.AsNoTracking()
            .Where(r => r.Date == date && agents.Contains(r.AgentId))
            .Select(r => new { r.Id, r.AgentId, Points = r.Points.Where(p => p.Status != FieldPointStatus.Cancelled).OrderBy(p => p.Sequence).Select(p => new { p.MarketId, p.Sequence, p.Status }).ToList() })
            .ToListAsync(ct);
        var pointState = new Dictionary<long, (string State, int Seq, Guid Agent)>();
        var linkoAgents = agents.Where(scope.LinkoOf.ContainsKey).ToDictionary(a => scope.LinkoOf[a], a => a);
        var done = (await linko.VisitsAsync(linkoAgents.Keys.ToList(), date, date, ct)).Where(v => v.Done).Select(v => v.MarketId).ToHashSet();
        foreach (var r in dayRoutes)
        {
            foreach (var p in r.Points)
            {
                var state = p.Status switch
                {
                    FieldPointStatus.Visited => "visited",
                    FieldPointStatus.InProgress => "visited",
                    FieldPointStatus.Skipped => "skipped",
                    _ => done.Contains(p.MarketId) ? "visited" : "planned",
                };
                pointState[p.MarketId] = (state, p.Sequence, r.AgentId);
            }
        }

        var q = Query(scope);
        if (agentId is not null || teamId is not null)
        {
            var linkoIds = agents.Where(scope.LinkoOf.ContainsKey).Select(a => scope.LinkoOf[a]).ToArray();
            q = q.Where(x => (x.C != null && x.C.AssignedAgentId != null && agents.Contains(x.C.AssignedAgentId.Value))
                             || ((x.C == null || x.C.AssignedAgentId == null) && x.M.ResponsibleAgentId != null && linkoIds.Contains(x.M.ResponsibleAgentId.Value)));
        }

        if (bbox is { } box)
        {
            q = q.Where(x => x.M.Lat >= box.South && x.M.Lat <= box.North && x.M.Lon >= box.West && x.M.Lon <= box.East);
        }

        q = q.Where(x => x.M.Lat != null && x.M.Lon != null && x.M.Lat != 0);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(x => x.S == null ? 0 : x.S.Sales30).Take(MapLimit)
            .Select(x => new
            {
                x.M.Id,
                x.M.Name,
                Lat = x.M.Lat!.Value,
                Lon = x.M.Lon!.Value,
                x.M.ResponsibleAgentId,
                Priority = x.C == null ? FieldPriority.Medium : x.C.Priority,
                Status = x.C == null ? FieldCustomerStatus.Active : x.C.Status,
                Sales30 = x.S == null ? 0 : x.S.Sales30,
                HasOrders = x.S != null && x.S.LastOrderDate != null,
            })
            .ToListAsync(ct);

        // Точки маршрутов показываем всегда, даже если они за пределами лимита.
        var missing = pointState.Keys.Except(rows.Select(r => r.Id)).ToList();
        var extra = await linko.MarketsAsync(missing, ct);
        var effective = await assignments.EffectiveAgentsAsync(rows.Select(r => (r.Id, r.ResponsibleAgentId)).Concat(extra.Values.Select(m => (m.Id, m.ResponsibleAgentId))), ct);

        string StateOf(long id, FieldCustomerStatus status, FieldPriority priority, bool hasOrders) =>
            pointState.TryGetValue(id, out var ps) ? ps.State
            : status == FieldCustomerStatus.Problem ? "problem"
            : priority is FieldPriority.High or FieldPriority.Urgent ? "priority"
            : !hasOrders ? "new"
            : "normal";

        var points = rows.Select(r => new FieldMapPoint(r.Id, r.Name, r.Lat, r.Lon, StateOf(r.Id, r.Status, r.Priority, r.HasOrders), effective.GetValueOrDefault(r.Id),
                pointState.TryGetValue(r.Id, out var ps) ? ps.Seq : null, r.Priority, r.Sales30))
            .Concat(extra.Values.Where(m => FieldGeo.HasCoordinates(m.Lat, m.Lon)).Select(m => new FieldMapPoint(m.Id, m.Name, m.Lat!.Value, m.Lon!.Value, pointState[m.Id].State,
                effective.GetValueOrDefault(m.Id), pointState[m.Id].Seq, FieldPriority.Medium, 0)))
            .ToList();

        var dayStart = FieldClock.DayStartUtc(date);
        var positions = await db.FieldVisits.AsNoTracking()
            .Where(v => agents.Contains(v.AgentId) && v.StartedAt >= dayStart && v.StartedAt < dayStart.AddDays(1) && v.Latitude != null && v.Longitude != null)
            .GroupBy(v => v.AgentId)
            .Select(g => g.OrderByDescending(v => v.StartedAt).Select(v => new { v.AgentId, v.Latitude, v.Longitude, v.StartedAt }).First())
            .ToListAsync(ct);

        return new FieldMapView(
            date,
            points,
            dayRoutes.Select(r => new FieldMapRoute(r.Id, r.AgentId, snapshot.NameOf(r.AgentId), r.Points.Select(p => p.MarketId).ToList())).ToList(),
            positions.Select(p => new FieldMapAgent(p.AgentId, snapshot.NameOf(p.AgentId), p.Latitude!.Value, p.Longitude!.Value, p.StartedAt)).ToList(),
            total > MapLimit,
            total);
    }

    private static string? Clean(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().Length <= max ? s.Trim() : s.Trim()[..max];
}
