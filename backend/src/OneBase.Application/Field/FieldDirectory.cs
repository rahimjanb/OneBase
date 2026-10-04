using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OneBase.Application.Abstractions;
using OneBase.Domain.Field;

namespace OneBase.Application.Field;

public sealed record FieldMemberInfo(Guid Id, Guid? UserId, long? LinkoUserId, FieldRole Role, string FullName, string? Phone, Guid? TeamId, IReadOnlyList<long> BranchIds, bool IsActive);

public sealed record FieldTeamInfo(Guid Id, string Name, Guid? SupervisorId, long? BranchId, bool IsActive);

/// <summary>Участники и команды Sales Base — маленькие таблицы, кэш на 30 секунд, сброс после изменений состава.</summary>
public sealed class FieldDirectory(IAppDbContext db, IMemoryCache cache)
{
    private const string Key = "field:directory";

    public sealed record Snapshot(IReadOnlyDictionary<Guid, FieldMemberInfo> Members, IReadOnlyDictionary<Guid, FieldTeamInfo> Teams)
    {
        private Dictionary<long, FieldMemberInfo>? _byLinko;

        /// <summary>Участник по сотруднику Linko (активные важнее отключённых).</summary>
        public IReadOnlyDictionary<long, FieldMemberInfo> ByLinko => _byLinko ??= Members.Values
            .Where(m => m.LinkoUserId is not null)
            .GroupBy(m => m.LinkoUserId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.IsActive).First());

        /// <summary>Супервайзер агента — через команду.</summary>
        public Guid? SupervisorOf(Guid agentId) =>
            Members.TryGetValue(agentId, out var a) && a.TeamId is { } t && Teams.TryGetValue(t, out var team) ? team.SupervisorId : null;

        public string NameOf(Guid? memberId) => memberId is { } id && Members.TryGetValue(id, out var m) ? m.FullName : "—";
    }

    public async Task<Snapshot> GetAsync(CancellationToken ct) =>
        await cache.GetOrCreateAsync(Key, async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
            var members = await db.FieldMembers.AsNoTracking()
                .Select(m => new FieldMemberInfo(m.Id, m.UserId, m.LinkoUserId, m.Role, m.FullName, m.Phone, m.TeamId, m.BranchIds, m.IsActive))
                .ToListAsync(ct);
            var teams = await db.FieldTeams.AsNoTracking()
                .Select(t => new FieldTeamInfo(t.Id, t.Name, t.SupervisorId, t.BranchId, t.IsActive))
                .ToListAsync(ct);
            return new Snapshot(members.ToDictionary(m => m.Id), teams.ToDictionary(t => t.Id));
        }) ?? throw new InvalidOperationException("Справочник Sales Base не загрузился.");

    public void Invalidate() => cache.Remove(Key);
}

/// <summary>Область текущего пользователя (см. FieldScopeBuilder).</summary>
public sealed class FieldAccess(FieldDirectory directory)
{
    public async Task<FieldScope> ResolveAsync(Guid userId, bool canUse, bool canManage, CancellationToken ct, bool canPlan = false)
    {
        var snapshot = await directory.GetAsync(ct);
        return FieldScopeBuilder.Build(
            userId,
            canUse,
            canManage,
            snapshot.Members.Values.Select(m => new FieldScopeBuilder.MemberRow(m.Id, m.UserId, m.LinkoUserId, m.Role, m.TeamId, m.BranchIds, m.IsActive)).ToList(),
            snapshot.Teams.Values.Select(t => new FieldScopeBuilder.TeamRow(t.Id, t.SupervisorId, t.BranchId, t.IsActive)).ToList(),
            canPlan);
    }
}

/// <summary>
/// Кто ведёт точку: агент, назначенный в Sales Base, иначе участник, который в Linko ответственный за точку.
/// </summary>
public sealed class FieldAssignments(IAppDbContext db, FieldDirectory directory, OneBase.Application.Sales.SalesOptions salesOptions)
{
    public async Task<Dictionary<long, Guid?>> EffectiveAgentsAsync(IEnumerable<(long MarketId, long? ResponsibleAgentId)> markets, CancellationToken ct)
    {
        var list = markets.DistinctBy(m => m.MarketId).ToList();
        var ids = list.Select(m => m.MarketId).ToArray();
        var overrides = ids.Length == 0
            ? []
            : await db.FieldCustomers.AsNoTracking()
                .Where(c => ids.Contains(c.MarketId) && c.AssignedAgentId != null)
                .ToDictionaryAsync(c => c.MarketId, c => c.AssignedAgentId, ct);
        var snapshot = await directory.GetAsync(ct);
        return list.ToDictionary(
            m => m.MarketId,
            m => overrides.TryGetValue(m.MarketId, out var assigned)
                ? assigned
                : m.ResponsibleAgentId is { } linko && snapshot.ByLinko.TryGetValue(linko, out var member) && member.Role == FieldRole.Agent ? member.Id : (Guid?)null);
    }

    /// <summary>
    /// Точки в области: назначенные в Sales Base агентам области, плюс точки, где ответственный в Linko — агент области
    /// и в Sales Base точку никому другому не переназначили. РМ всей организации — все точки.
    /// </summary>
    public IQueryable<Domain.Sales.LinkoMarket> MarketsInScope(FieldScope scope)
    {
        var markets = db.LinkoMarkets.AsNoTracking();
        if (scope.OrgWide)
        {
            // Вся организация — без экспорта и опта филиалов Sales:ExcludedBranches («Завод»): это не полевые продажи.
            var excluded = salesOptions.ExcludedBranches.Select(b => b.Trim().ToLowerInvariant()).ToArray();
            return markets.Where(m => m.BranchName == null || !excluded.Contains(m.BranchName.ToLower()));
        }

        var agentIds = scope.AgentIds.ToArray();
        var linkoIds = scope.AgentLinkoIds.ToArray();
        var overrides = db.FieldCustomers.Where(c => c.AssignedAgentId != null);
        return markets.Where(m =>
            overrides.Any(c => c.MarketId == m.Id && agentIds.Contains(c.AssignedAgentId!.Value))
            || (m.ResponsibleAgentId != null && linkoIds.Contains(m.ResponsibleAgentId.Value) && !overrides.Any(c => c.MarketId == m.Id)));
    }

    /// <summary>
    /// Точка без действующего агента (ответственный в Linko — вакансия, уволен или не в Sales Base, переназначения нет) в филиале команд области.
    /// Такую точку можно назначить агенту области по рекомендации «точка без агента».
    /// </summary>
    public async Task<Domain.Sales.LinkoMarket?> OrphanInScopeAsync(FieldScope scope, long marketId, CancellationToken ct)
    {
        var snapshot = await directory.GetAsync(ct);
        var branchIds = snapshot.Teams.Values.Where(t => t.IsActive && t.BranchId is not null && (scope.OrgWide || scope.TeamIds.Contains(t.Id)))
            .Select(t => t.BranchId!.Value).Distinct().ToArray();
        var activeAgentLinko = snapshot.Members.Values.Where(m => m.Role == FieldRole.Agent && m.IsActive && m.LinkoUserId is not null).Select(m => m.LinkoUserId!.Value).ToArray();
        return await db.LinkoMarkets.AsNoTracking().FirstOrDefaultAsync(m => m.Id == marketId && m.BranchId != null && branchIds.Contains(m.BranchId.Value)
            && (m.ResponsibleAgentId == null || !activeAgentLinko.Contains(m.ResponsibleAgentId.Value))
            && !db.FieldCustomers.Any(c => c.MarketId == m.Id && c.AssignedAgentId != null), ct);
    }

    /// <summary>
    /// Точка в маршруте агента области на неделю назад или вперёд (супервайзер мог поставить в маршрут точку из задачи или плана Linko,
    /// которую агент не ведёт): по ней можно открыть карточку и начать визит.
    /// </summary>
    public Task<bool> IsRoutePointAsync(FieldScope scope, long marketId, CancellationToken ct)
    {
        var agents = scope.AgentIds.ToArray();
        var from = FieldClock.Today.AddDays(-7);
        var to = FieldClock.Today.AddDays(7);
        return db.FieldRoutePoints.AsNoTracking().AnyAsync(p => p.MarketId == marketId && p.Status != FieldPointStatus.Cancelled
            && agents.Contains(p.Route!.AgentId) && p.Route.Date >= from && p.Route.Date <= to, ct);
    }

    /// <summary>Точка в области (или, с allowRoutePoints, в маршруте агента области)? Чужая — как будто её нет (404).</summary>
    public async Task<Domain.Sales.LinkoMarket> RequireMarketAsync(FieldScope scope, long marketId, CancellationToken ct, bool allowRoutePoints = false)
    {
        var market = await MarketsInScope(scope).FirstOrDefaultAsync(m => m.Id == marketId, ct);
        if (market is null && allowRoutePoints && await IsRoutePointAsync(scope, marketId, ct))
        {
            market = await db.LinkoMarkets.AsNoTracking().FirstOrDefaultAsync(m => m.Id == marketId, ct);
        }

        return market ?? throw new FieldNotFoundException("Точка не найдена.");
    }
}

/// <summary>Уведомления внутри приложения. Добавляет записи в контекст — сохраняет вызывающий сервис вместе со своими изменениями.</summary>
public sealed class FieldNotifier(IAppDbContext db)
{
    public void Notify(Guid? recipientId, FieldNotificationKind kind, string title, string? body = null, string? link = null)
    {
        if (recipientId is null)
        {
            return;
        }

        db.FieldNotifications.Add(new FieldNotification { RecipientId = recipientId.Value, Kind = kind, Title = Trim(title, 300)!, Body = Trim(body, 2000), Link = link });
    }

    public void Notify(IEnumerable<Guid?> recipients, FieldNotificationKind kind, string title, string? body = null, string? link = null)
    {
        foreach (var r in recipients.Where(r => r is not null).Distinct())
        {
            Notify(r, kind, title, body, link);
        }
    }

    private static string? Trim(string? s, int max) => s is null ? null : s.Length <= max ? s : s[..(max - 1)] + "…";
}

/// <summary>Настройки Sales Base: строка Id = 1, создаётся со значениями по умолчанию.</summary>
public sealed class FieldSettingsStore(IAppDbContext db, IMemoryCache cache)
{
    private const string Key = "field:settings";

    public async Task<FieldSettings> GetAsync(CancellationToken ct) =>
        await cache.GetOrCreateAsync(Key, async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return await db.FieldSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1, ct) ?? new FieldSettings();
        }) ?? new FieldSettings();

    public async Task<FieldSettings> UpdateAsync(Action<FieldSettings> apply, CancellationToken ct)
    {
        var settings = await db.FieldSettings.FirstOrDefaultAsync(s => s.Id == 1, ct);
        if (settings is null)
        {
            settings = new FieldSettings();
            db.FieldSettings.Add(settings);
        }

        apply(settings);
        settings.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        cache.Remove(Key);
        return settings;
    }
}

public sealed record PageResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
