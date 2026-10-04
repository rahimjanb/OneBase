using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Domain.Audit;
using OneBase.Domain.Field;

namespace OneBase.Application.Field;

public sealed record FieldRecommendationRow(
    Guid Id,
    FieldRecommendationKind Kind,
    string Title,
    string Reason,
    decimal Confidence,
    FieldRecommendationStatus Status,
    Guid? AgentId,
    string? AgentName,
    Guid? SupervisorId,
    string? SupervisorName,
    long? MarketId,
    string? MarketName,
    FieldPriority Priority,
    DateOnly? DueDate,
    DateTimeOffset CreatedAt,
    string? DecidedBy,
    DateTimeOffset? DecidedAt,
    string? DecisionNote,
    Guid? TaskId,
    JsonElement SourceData,
    bool CanDecide);

public sealed record FieldRecommendationQuery(string? Status, FieldRecommendationKind? Kind, Guid? AgentId, int Page = 1, int PageSize = 50);

public sealed record FieldRecommendationDecision(string? Title, Guid? AgentId, FieldPriority? Priority, DateOnly? DueDate, string? Note, bool AddToRoute = true);

public sealed record FieldGenerateResult(int Created, int Skipped, int AutoApproved);

/// <summary>
/// AI Planning Engine, первый этап: правила FieldPlanningRules на витрине точек, планах и маршрутах. План сразу не меняется —
/// создаются рекомендации; РМ или супервайзер подтверждает (можно с правкой) или отклоняет. Подтверждённая создаёт задачу
/// (и ставит точку в маршрут на срок), для «точки без агента» — назначает агента. Автопланирование — настройка.
/// </summary>
public sealed class FieldRecommendationService(
    IAppDbContext db,
    FieldDirectory directory,
    FieldAssignments assignments,
    FieldSettingsStore settingsStore,
    ILinkoSalesProvider linko,
    FieldTaskService tasks,
    FieldRouteService routes,
    FieldCustomerService customers,
    FieldNotifier notifier,
    IAuditLogger audit)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Видимость — по текущей зоне (агенты области), а не по супервайзеру, записанному при создании: после перевода агента
    /// в другую команду его рекомендации видит и решает уже новый супервайзер. Агент рекомендации не видит.
    /// </summary>
    private IQueryable<FieldRecommendation> Visible(FieldScope scope)
    {
        var q = db.FieldRecommendations.AsNoTracking();
        if (!scope.CanPlan)
        {
            return q.Where(_ => false);
        }

        if (scope.OrgWide)
        {
            return q;
        }

        var agents = scope.AgentIds.ToArray();
        return q.Where(r => r.AgentId != null && agents.Contains(r.AgentId.Value));
    }

    /// <summary>Сколько рекомендаций ждут решения в зоне по выбранным агентам (для дашборда).</summary>
    public Task<int> PendingCountAsync(FieldScope scope, IReadOnlyCollection<Guid> agentIds, CancellationToken ct)
    {
        var ids = agentIds.ToArray();
        return Visible(scope).CountAsync(r => r.Status == FieldRecommendationStatus.Pending && r.AgentId != null && ids.Contains(r.AgentId.Value), ct);
    }

    public async Task<PageResult<FieldRecommendationRow>> ListAsync(FieldScope scope, FieldRecommendationQuery query, CancellationToken ct)
    {
        var q = Visible(scope);
        q = query.Status switch
        {
            null or "" or "pending" => q.Where(r => r.Status == FieldRecommendationStatus.Pending),
            "decided" => q.Where(r => r.Status != FieldRecommendationStatus.Pending),
            "all" => q,
            _ when Enum.TryParse<FieldRecommendationStatus>(query.Status, true, out var st) => q.Where(r => r.Status == st),
            _ => q,
        };

        if (query.Kind is { } kind)
        {
            q = q.Where(r => r.Kind == kind);
        }

        if (query.AgentId is { } agent)
        {
            q = q.Where(r => r.AgentId == agent);
        }

        var total = await q.CountAsync(ct);
        var pageSize = Math.Clamp(query.PageSize, 10, 200);
        var page = Math.Max(query.Page, 1);
        var rows = await q.OrderByDescending(r => r.Status == FieldRecommendationStatus.Pending)
            .ThenByDescending(r => (r.Priority == FieldPriority.Urgent ? 3 : r.Priority == FieldPriority.High ? 2 : r.Priority == FieldPriority.Medium ? 1 : 0)).ThenByDescending(r => r.Confidence).ThenByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PageResult<FieldRecommendationRow>(await RowsAsync(scope, rows, ct), total, page, pageSize);
    }

    public async Task<List<FieldRecommendationRow>> ForMarketAsync(FieldScope scope, long marketId, CancellationToken ct)
    {
        var rows = await Visible(scope).Where(r => r.MarketId == marketId).OrderByDescending(r => r.CreatedAt).Take(10).ToListAsync(ct);
        return await RowsAsync(scope, rows, ct);
    }

    public async Task<List<FieldRecommendationRow>> ForAgentAsync(FieldScope scope, Guid agentId, int take, CancellationToken ct)
    {
        var rows = await Visible(scope).Where(r => r.AgentId == agentId && r.Status == FieldRecommendationStatus.Pending)
            .OrderByDescending(r => (r.Priority == FieldPriority.Urgent ? 3 : r.Priority == FieldPriority.High ? 2 : r.Priority == FieldPriority.Medium ? 1 : 0)).ThenByDescending(r => r.Confidence).Take(take).ToListAsync(ct);
        return await RowsAsync(scope, rows, ct);
    }

    private async Task<List<FieldRecommendationRow>> RowsAsync(FieldScope scope, IReadOnlyList<FieldRecommendation> rows, CancellationToken ct)
    {
        var snapshot = await directory.GetAsync(ct);
        var markets = await linko.MarketsAsync(rows.Where(r => r.MarketId is not null).Select(r => r.MarketId!.Value).ToList(), ct);
        return rows.Select(r => new FieldRecommendationRow(r.Id, r.Kind, r.Title, r.Reason, r.Confidence, r.Status, r.AgentId, r.AgentId is { } a ? snapshot.NameOf(a) : null,
                r.SupervisorId, r.SupervisorId is { } s ? snapshot.NameOf(s) : null, r.MarketId, r.MarketId is { } m ? markets.GetValueOrDefault(m)?.Name : null, r.Priority, r.DueDate,
                r.CreatedAt, r.DecidedById is { } d ? snapshot.NameOf(d) : r.DecidedAt is not null && r.DecidedById is null ? "AI (авто)" : null, r.DecidedAt,
                scope.CanPlan ? r.DecisionNote : null, r.TaskId,
                JsonDocument.Parse(string.IsNullOrWhiteSpace(r.SourceData) ? "{}" : r.SourceData).RootElement.Clone(),
                r.Status == FieldRecommendationStatus.Pending && CanDecide(scope, r)))
            .ToList();
    }

    private static bool CanDecide(FieldScope scope, FieldRecommendation r) =>
        scope.CanPlan && (scope.OrgWide || (r.AgentId is { } a && scope.CanPlanFor(a)));

    /// <summary>
    /// Прогон правил. scope = null — вся организация (фоновая задача); иначе — зона РМ или супервайзера.
    /// Дубли не создаются: такая же ждёт решения, отклонена за 14 дней или подтверждена за 7.
    /// </summary>
    public async Task<FieldGenerateResult> GenerateAsync(FieldScope? scope, DateOnly today, CancellationToken ct)
    {
        if (scope is not null && !scope.CanPlan)
        {
            throw new FieldForbiddenException("Рекомендации обновляет супервайзер или РМ.");
        }

        var settings = await settingsStore.GetAsync(ct);
        var snapshot = await directory.GetAsync(ct);
        var agents = snapshot.Members.Values
            .Where(m => m.Role == FieldRole.Agent && m.IsActive && m.LinkoUserId is not null && (scope is null || scope.CanSeeAgent(m.Id)))
            .ToList();
        var agentScope = new FieldScope
        {
            UserId = scope?.UserId ?? Guid.Empty,
            MemberId = null,
            Role = FieldRole.Rm,
            CanManage = false,
            OrgWide = false,
            AgentIds = agents.Select(a => a.Id).ToHashSet(),
            MemberIds = agents.Select(a => a.Id).ToHashSet(),
            TeamIds = new HashSet<Guid>(),
            LinkoOf = agents.ToDictionary(a => a.Id, a => a.LinkoUserId!.Value),
        };

        var drafts = new List<FieldPlanningRules.Draft>();

        // 1. Точки агентов: падение, потерянные, давно не посещали.
        var rows = await (
                from m in assignments.MarketsInScope(agentScope)
                join s in db.FieldCustomerStats.AsNoTracking() on m.Id equals s.MarketId
                join c in db.FieldCustomers.AsNoTracking() on m.Id equals c.MarketId into cs
                from c in cs.DefaultIfEmpty()
                where (c == null || c.Status != FieldCustomerStatus.Inactive) && (s.Sales90 > 0 || s.SalesPrev30 > 0)
                select new { m.Id, m.Name, m.ResponsibleAgentId, s.Sales30, s.SalesPrev30, s.Sales90, s.LastOrderDate, s.LastVisitDate, Priority = c == null ? FieldPriority.Medium : c.Priority })
            .ToListAsync(ct);
        var effective = await assignments.EffectiveAgentsAsync(rows.Select(r => (r.Id, r.ResponsibleAgentId)), ct);
        drafts.AddRange(FieldPlanningRules.ForMarkets(
            rows.Select(r =>
            {
                var agent = effective.GetValueOrDefault(r.Id);
                return new FieldPlanningRules.MarketSnapshot(r.Id, r.Name, agent, agent is { } a ? snapshot.SupervisorOf(a) : null, r.Sales30, r.SalesPrev30, r.Sales90,
                    r.LastOrderDate, r.LastVisitDate, r.Priority);
            }),
            settings,
            today));

        // 2. Агенты, отстающие от темпа плана (кг, доставленные с начала месяца).
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var linkoIds = agents.Select(a => a.LinkoUserId!.Value).ToList();
        var plans = (await linko.AgentPlansAsync(linkoIds, today.Year, today.Month, ct)).ToDictionary(p => p.LinkoUserId);
        var facts = await linko.AgentTotalsAsync(linkoIds, monthStart, today, SalesBasis.Delivered, ct);
        drafts.AddRange(FieldPlanningRules.ForAgents(
            agents.Select(a => new FieldPlanningRules.AgentPace(a.Id, a.FullName, snapshot.SupervisorOf(a.Id),
                plans.GetValueOrDefault(a.LinkoUserId!.Value)?.PlanKg, facts.GetValueOrDefault(a.LinkoUserId!.Value)?.Kg ?? 0)),
            settings,
            today));

        // 3. Пропущенные точки маршрутов за два прошлых дня.
        var agentIds = agents.Select(a => a.Id).ToArray();
        var skipped = await db.FieldRoutePoints.AsNoTracking()
            .Where(p => p.Status == FieldPointStatus.Skipped && p.Route!.Date >= today.AddDays(-2) && p.Route.Date < today && agentIds.Contains(p.Route.AgentId))
            .Select(p => new { p.MarketId, p.Route!.AgentId, p.Route.Date, p.Note })
            .ToListAsync(ct);
        var skippedMarkets = await linko.MarketsAsync(skipped.Select(s => s.MarketId).ToList(), ct);
        drafts.AddRange(FieldPlanningRules.ForSkipped(
            skipped.Select(s => new FieldPlanningRules.SkippedPoint(s.MarketId, skippedMarkets.GetValueOrDefault(s.MarketId)?.Name ?? $"Точка {s.MarketId}", s.AgentId, snapshot.SupervisorOf(s.AgentId), s.Date, s.Note)),
            today));

        // 4. Точки без действующего агента в филиалах команд.
        drafts.AddRange(await OrphansAsync(scope, snapshot, today, ct));

        // Дедупликация и сохранение.
        var keys = drafts.Select(d => d.Key).Distinct().ToArray();
        var recent = await db.FieldRecommendations.AsNoTracking()
            .Where(r => keys.Contains(r.Key) && (r.Status == FieldRecommendationStatus.Pending
                                                 || (r.Status == FieldRecommendationStatus.Rejected && r.DecidedAt > DateTimeOffset.UtcNow.AddDays(-14))
                                                 || (r.Status == FieldRecommendationStatus.Approved && r.DecidedAt > DateTimeOffset.UtcNow.AddDays(-7))))
            .Select(r => r.Key)
            .ToListAsync(ct);
        var blocked = recent.ToHashSet();
        var created = new List<FieldRecommendation>();
        foreach (var d in drafts.DistinctBy(d => d.Key).Where(d => !blocked.Contains(d.Key)))
        {
            var rec = new FieldRecommendation
            {
                Kind = d.Kind,
                Key = d.Key,
                Title = d.Title.Length <= 300 ? d.Title : d.Title[..299] + "…",
                Reason = d.Reason.Length <= 2000 ? d.Reason : d.Reason[..1999] + "…",
                SourceData = JsonSerializer.Serialize(d.Source, Json),
                Confidence = d.Confidence,
                AgentId = d.AgentId,
                SupervisorId = d.SupervisorId,
                MarketId = d.MarketId,
                Priority = d.Priority,
                DueDate = d.DueDate,
            };
            db.FieldRecommendations.Add(rec);
            created.Add(rec);
        }

        // Супервайзерам — одно сводное уведомление о новых рекомендациях.
        foreach (var g in created.Where(r => r.SupervisorId is not null).GroupBy(r => r.SupervisorId))
        {
            notifier.Notify(g.Key, FieldNotificationKind.Recommendation, $"AI: {g.Count()} новых рекомендаций по команде", string.Join("; ", g.Take(3).Select(r => r.Title)), "/field/ai");
        }

        await db.SaveChangesAsync(ct);
        var auto = 0;
        if (settings.AutoPlanning)
        {
            foreach (var rec in created.Where(r => r.Kind != FieldRecommendationKind.Reassign))
            {
                await ApplyAsync(rec, null, new FieldRecommendationDecision(null, null, null, null, "Автопланирование"), ct);
                auto++;
            }

            await db.SaveChangesAsync(ct);
        }

        await audit.LogAsync(scope is null ? ActorType.System : ActorType.User, scope?.UserId.ToString() ?? "field-planning", "field.recommendations.generated", "field", null,
            new { created = created.Count, skipped = drafts.Count - created.Count, auto }, ct);
        return new FieldGenerateResult(created.Count, drafts.Count - created.Count, auto);
    }

    /// <summary>Точки, чей ответственный в Linko — не действующий агент Sales Base (вакансия, уволен, не добавлен), и их нет в оверлее.</summary>
    private async Task<List<FieldPlanningRules.Draft>> OrphansAsync(FieldScope? scope, FieldDirectory.Snapshot snapshot, DateOnly today, CancellationToken ct)
    {
        var teams = snapshot.Teams.Values.Where(t => t.IsActive && t.BranchId is not null && (scope is null || scope.OrgWide || scope.TeamIds.Contains(t.Id))).ToList();
        if (teams.Count == 0)
        {
            return [];
        }

        var branchIds = teams.Select(t => t.BranchId!.Value).Distinct().ToArray();
        var activeAgentLinko = snapshot.Members.Values.Where(m => m.Role == FieldRole.Agent && m.IsActive && m.LinkoUserId is not null).Select(m => m.LinkoUserId!.Value).ToArray();
        var orphans = await (
                from m in db.LinkoMarkets.AsNoTracking()
                join s in db.FieldCustomerStats.AsNoTracking() on m.Id equals s.MarketId
                where m.BranchId != null && branchIds.Contains(m.BranchId.Value) && s.Sales90 > 0
                      && (m.ResponsibleAgentId == null || !activeAgentLinko.Contains(m.ResponsibleAgentId.Value))
                      && !db.FieldCustomers.Any(c => c.MarketId == m.Id && c.AssignedAgentId != null)
                orderby s.Sales90 descending
                select new { m.Id, m.Name, m.BranchId, m.ResponsibleAgentId, s.Sales90 })
            .Take(30)
            .ToListAsync(ct);
        if (orphans.Count == 0)
        {
            return [];
        }

        var responsibleNames = await linko.UserNamesAsync(orphans.Where(o => o.ResponsibleAgentId is not null).Select(o => o.ResponsibleAgentId!.Value).Distinct().ToList(), ct);
        var load = await db.LinkoMarkets.AsNoTracking()
            .Where(m => m.ResponsibleAgentId != null && activeAgentLinko.Contains(m.ResponsibleAgentId.Value))
            .GroupBy(m => m.ResponsibleAgentId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var drafts = new List<FieldPlanningRules.OrphanMarket>();
        foreach (var o in orphans)
        {
            var team = teams.First(t => t.BranchId == o.BranchId);
            var candidate = snapshot.Members.Values
                .Where(m => m.Role == FieldRole.Agent && m.IsActive && m.TeamId == team.Id && m.LinkoUserId is not null)
                .OrderBy(m => load.GetValueOrDefault(m.LinkoUserId!.Value))
                .FirstOrDefault();
            if (candidate is null)
            {
                continue;
            }

            var reason = o.ResponsibleAgentId is null ? "В Linko у точки нет ответственного агента"
                : $"Ответственный в Linko — {responsibleNames.GetValueOrDefault(o.ResponsibleAgentId.Value, $"#{o.ResponsibleAgentId}")}, он не действующий агент Sales Base";
            drafts.Add(new FieldPlanningRules.OrphanMarket(o.Id, o.Name, reason, o.Sales90, candidate.Id, candidate.FullName, team.SupervisorId));
        }

        return FieldPlanningRules.ForOrphans(drafts, today);
    }

    public async Task<FieldRecommendationRow> ApproveAsync(FieldScope scope, Guid id, FieldRecommendationDecision decision, CancellationToken ct)
    {
        var rec = await db.FieldRecommendations.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rec is null || !await Visible(scope).AnyAsync(r => r.Id == id, ct))
        {
            throw new FieldNotFoundException("Рекомендация не найдена.");
        }

        if (rec.Status != FieldRecommendationStatus.Pending)
        {
            throw new FieldValidationException("По рекомендации уже принято решение.");
        }

        if (!CanDecide(scope, rec))
        {
            throw new FieldForbiddenException("Решение по рекомендации принимает супервайзер команды или РМ.");
        }

        if (decision.AgentId is { } agent && !scope.CanPlanFor(agent))
        {
            throw new FieldForbiddenException("Исполнитель — только агент вашей зоны.");
        }

        await ApplyAsync(rec, scope, decision, ct);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, scope.UserId.ToString(), "field.recommendation.approved", "field_recommendation", rec.Id.ToString(),
            new { kind = rec.Kind.ToString(), rec.TaskId, edited = decision.Title is not null || decision.AgentId is not null || decision.DueDate is not null || decision.Priority is not null }, ct);
        return (await RowsAsync(scope, [rec], ct))[0];
    }

    /// <summary>Подтверждение: задача (или переназначение точки), отметка решения, уведомление исполнителю.</summary>
    private async Task ApplyAsync(FieldRecommendation rec, FieldScope? scope, FieldRecommendationDecision decision, CancellationToken ct)
    {
        var snapshot = await directory.GetAsync(ct);
        rec.Status = FieldRecommendationStatus.Approved;
        rec.DecidedAt = DateTimeOffset.UtcNow;
        rec.DecidedById = scope?.MemberId;
        rec.DecisionNote = string.IsNullOrWhiteSpace(decision.Note) ? null : decision.Note.Trim()[..Math.Min(decision.Note.Trim().Length, 1000)];
        rec.UpdatedAt = rec.DecidedAt;

        if (rec.Kind == FieldRecommendationKind.Reassign && rec.MarketId is { } market && (decision.AgentId ?? rec.AgentId) is { } target && scope is not null)
        {
            await customers.ChangeAgentAsync(scope, market, target, ct, orphanAllowed: true);
            return;
        }

        var priority = decision.Priority ?? rec.Priority;
        var due = decision.DueDate ?? rec.DueDate;
        var title = string.IsNullOrWhiteSpace(decision.Title) ? rec.Title : decision.Title.Trim();
        var assignee = decision.AgentId ?? rec.AgentId;
        if (rec.Kind == FieldRecommendationKind.AgentBehindPlan && decision.AgentId is null && rec.AgentId is { } behind && snapshot.SupervisorOf(behind) is { } supervisor)
        {
            assignee = supervisor; // разбор плана — задача текущему супервайзеру агента
        }

        if (assignee is null)
        {
            return;
        }

        // Исполнитель — действующий участник зоны того, кто решает (агент мог уйти из команды после создания рекомендации).
        if (snapshot.Members.GetValueOrDefault(assignee.Value) is not { IsActive: true } || (scope is not null && !scope.CanSeeMember(assignee.Value)))
        {
            if (scope is null)
            {
                return; // автопланирование: исполнителя нет — только отметка решения
            }

            throw new FieldValidationException("Исполнитель больше не в вашей зоне или отключён — выберите другого агента.");
        }

        var task = tasks.CreateSystem(title, rec.Reason, assignee.Value, snapshot.SupervisorOf(assignee.Value), rec.MarketId, priority, due,
            FieldActorType.Ai, rec.Id, scope?.MemberId); // источник — AI и при ручном подтверждении; кто подтвердил — CreatedById
        rec.TaskId = task.Id;

        if (decision.AddToRoute && rec.MarketId is { } m && due is { } d && snapshot.Members.GetValueOrDefault(assignee.Value)?.Role == FieldRole.Agent)
        {
            await routes.InsertMarketAsync(assignee.Value, m, d, ct);
        }
    }

    public async Task<FieldRecommendationRow> RejectAsync(FieldScope scope, Guid id, string? note, CancellationToken ct)
    {
        var rec = await db.FieldRecommendations.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rec is null || !await Visible(scope).AnyAsync(r => r.Id == id, ct))
        {
            throw new FieldNotFoundException("Рекомендация не найдена.");
        }

        if (rec.Status != FieldRecommendationStatus.Pending)
        {
            throw new FieldValidationException("По рекомендации уже принято решение.");
        }

        if (!CanDecide(scope, rec))
        {
            throw new FieldForbiddenException("Решение по рекомендации принимает супервайзер команды или РМ.");
        }

        rec.Status = FieldRecommendationStatus.Rejected;
        rec.DecidedAt = DateTimeOffset.UtcNow;
        rec.DecidedById = scope.MemberId;
        rec.DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim()[..Math.Min(note.Trim().Length, 1000)];
        rec.UpdatedAt = rec.DecidedAt;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, scope.UserId.ToString(), "field.recommendation.rejected", "field_recommendation", rec.Id.ToString(),
            new { kind = rec.Kind.ToString(), note = rec.DecisionNote }, ct);
        return (await RowsAsync(scope, [rec], ct))[0];
    }

    /// <summary>Старые нерешённые рекомендации (срок прошёл больше 7 дней назад) — в «устарела».</summary>
    public async Task<int> ExpireAsync(DateOnly today, CancellationToken ct) =>
        await db.FieldRecommendations
            .Where(r => r.Status == FieldRecommendationStatus.Pending && r.DueDate != null && r.DueDate < today.AddDays(-7))
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, FieldRecommendationStatus.Expired).SetProperty(r => r.UpdatedAt, DateTimeOffset.UtcNow), ct);
}
