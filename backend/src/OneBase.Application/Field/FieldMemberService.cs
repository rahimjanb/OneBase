using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales;
using OneBase.Domain.Audit;
using OneBase.Domain.Field;

namespace OneBase.Application.Field;

public sealed record FieldMemberView(
    Guid Id,
    string FullName,
    FieldRole Role,
    string? Phone,
    Guid? TeamId,
    string? TeamName,
    Guid? SupervisorId,
    string? SupervisorName,
    long? LinkoUserId,
    string? LinkoJob,
    string? LinkoPosition,
    Guid? UserId,
    string? Login,
    bool IsActive,
    IReadOnlyList<long> BranchIds,
    int Markets,
    bool Editable);

public sealed record FieldTeamView(Guid Id, string Name, Guid? SupervisorId, string? SupervisorName, long? BranchId, string? BranchName, bool IsActive, int Agents);

public sealed record FieldCandidate(long LinkoUserId, string Name, string? Job, string? Position, FieldRole SuggestedRole, long? BranchId, string? BranchName, int Markets);

public sealed record FieldStructureView(
    IReadOnlyList<FieldMemberView> Members,
    IReadOnlyList<FieldTeamView> Teams,
    IReadOnlyList<FieldCandidate> Candidates,
    IReadOnlyList<FieldBranch> Branches,
    bool CanManage,
    bool CanManageOrg);

public sealed record FieldBranch(long Id, string Name);

public sealed record FieldMemberInput(string? FullName, FieldRole Role, string? Phone, Guid? TeamId, long? LinkoUserId, List<long>? BranchIds, bool IsActive = true);

public sealed record FieldTeamInput(string? Name, Guid? SupervisorId, long? BranchId, bool IsActive = true);

public sealed record FieldImportResult(int Agents, int Supervisors, int TeamsCreated, int SupervisorsLinked);

/// <summary>
/// Состав Sales Base: участники и команды. Управляет РМ (field.manage); супервайзер видит свою команду.
/// Импорт из Linko: агенты и супервайзеры, команды по филиалам (филиал агента — где большинство его точек).
/// </summary>
public sealed class FieldMemberService(
    IAppDbContext db,
    FieldDirectory directory,
    ILinkoSalesProvider linko,
    SalesOptions salesOptions,
    IAuditLogger audit)
{
    private static readonly string[] SupervisorJobs = ["Супервайзер", "Supervisor"];

    public async Task<FieldStructureView> GetAsync(FieldScope scope, CancellationToken ct)
    {
        var snapshot = await directory.GetAsync(ct);
        var branches = (await linko.BranchesAsync(ct)).Select(b => new FieldBranch(b.Id, b.Name)).ToList();
        var branchName = branches.ToDictionary(b => b.Id, b => b.Name);
        if (!scope.OrgWide)
        {
            branches = branches.Where(b => scope.BranchIds.Contains(b.Id)).ToList();
        }

        // Всю организацию видит только управляющий всей организацией; РМ филиалов — свою зону и свободных супервайзеров (чтобы поставить во главе команды).
        var ledTeams = snapshot.Teams.Values.Where(t => t.IsActive && t.SupervisorId is not null).Select(t => t.SupervisorId!.Value).ToHashSet();
        var visible = snapshot.Members.Values
            .Where(m => scope.CanManageOrg || scope.CanSeeMember(m.Id) || (scope.CanManage && m.Role == FieldRole.Supervisor && m.IsActive && !ledTeams.Contains(m.Id)))
            .ToList();
        var linkoIds = visible.Where(m => m.LinkoUserId is not null).Select(m => m.LinkoUserId!.Value).ToArray();
        var linkoUsers = await db.LinkoUsers.AsNoTracking().Where(u => linkoIds.Contains(u.Id)).Select(u => new { u.Id, u.JobName, u.PositionName }).ToDictionaryAsync(u => u.Id, ct);
        var userIds = visible.Where(m => m.UserId is not null).Select(m => m.UserId!.Value).ToArray();
        var logins = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Login, ct);
        var marketCounts = await db.LinkoMarkets.AsNoTracking()
            .Where(m => m.ResponsibleAgentId != null && linkoIds.Contains(m.ResponsibleAgentId.Value))
            .GroupBy(m => m.ResponsibleAgentId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var members = visible
            .Select(m =>
            {
                var team = m.TeamId is { } t && snapshot.Teams.TryGetValue(t, out var tm) ? tm : null;
                var lu = m.LinkoUserId is { } l && linkoUsers.TryGetValue(l, out var u) ? u : null;
                return new FieldMemberView(m.Id, m.FullName, m.Role, m.Phone, m.TeamId, team?.Name, team?.SupervisorId, team?.SupervisorId is { } s ? snapshot.NameOf(s) : null,
                    m.LinkoUserId, lu?.JobName, lu?.PositionName, m.UserId, m.UserId is { } uid && logins.TryGetValue(uid, out var login) ? login : null,
                    m.IsActive, m.BranchIds, m.LinkoUserId is { } li && marketCounts.TryGetValue(li, out var c) ? c : 0,
                    m.Id != scope.MemberId && CanManageMember(scope, m.Id, m.Role));
            })
            .OrderBy(m => m.Role).ThenBy(m => m.TeamName).ThenBy(m => m.FullName)
            .ToList();

        var teams = snapshot.Teams.Values
            .Where(t => scope.CanManageOrg || scope.TeamIds.Contains(t.Id))
            .Select(t => new FieldTeamView(t.Id, t.Name, t.SupervisorId, t.SupervisorId is { } s ? snapshot.NameOf(s) : null, t.BranchId,
                t.BranchId is { } b && branchName.TryGetValue(b, out var n) ? n : null, t.IsActive,
                snapshot.Members.Values.Count(m => m.TeamId == t.Id && m.IsActive && m.Role == FieldRole.Agent)))
            .OrderBy(t => t.Name)
            .ToList();

        var candidates = scope.CanManageOrg ? await CandidatesAsync(snapshot, branchName, ct) : [];
        return new FieldStructureView(members, teams, candidates, branches, scope.CanManage, scope.CanManageOrg);
    }

    /// <summary>Сотрудники Linko (агенты и супервайзеры), которых ещё нет в Sales Base.</summary>
    private async Task<List<FieldCandidate>> CandidatesAsync(FieldDirectory.Snapshot snapshot, IReadOnlyDictionary<long, string> branchName, CancellationToken ct)
    {
        var known = snapshot.Members.Values.Where(m => m.LinkoUserId is not null).Select(m => m.LinkoUserId!.Value).ToHashSet();
        var users = await LinkoStaffAsync(ct);
        var fresh = users.Where(u => !known.Contains(u.Id)).ToList();
        var branches = await AgentBranchesAsync(fresh.Select(u => u.Id).ToArray(), ct);
        return fresh
            .Select(u =>
            {
                var b = branches.GetValueOrDefault(u.Id);
                return new FieldCandidate(u.Id, u.Name, u.Job, u.Position, u.Role, b.BranchId, b.BranchId is { } id && branchName.TryGetValue(id, out var n) ? n : null, b.Markets);
            })
            .OrderBy(c => c.SuggestedRole).ThenBy(c => c.BranchName).ThenBy(c => c.Name)
            .ToList();
    }

    private sealed record StaffRow(long Id, string Name, string? Job, string? Position, FieldRole Role);

    private async Task<List<StaffRow>> LinkoStaffAsync(CancellationToken ct)
    {
        var repJobs = salesOptions.SalesRepJobs;
        var rows = await db.LinkoUsers.AsNoTracking()
            .Where(u => u.IsActive && u.JobName != null)
            .Select(u => new { u.Id, u.FirstName, u.SecondName, u.Username, u.JobName, u.PositionName })
            .ToListAsync(ct);
        return rows
            .Select(u =>
            {
                var name = string.Join(' ', new[] { u.FirstName, u.SecondName }.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (name.Length == 0)
                {
                    name = u.Username ?? $"#{u.Id}";
                }

                FieldRole? role = repJobs.Contains(u.JobName!) ? FieldRole.Agent
                    : SupervisorJobs.Any(j => u.JobName!.Contains(j, StringComparison.OrdinalIgnoreCase)) ? FieldRole.Supervisor
                    : null;
                return role is null || salesOptions.IsFieldVacancy(u.Id, name) ? null : new StaffRow(u.Id, name, u.JobName, u.PositionName, role.Value);
            })
            .Where(u => u is not null)
            .Select(u => u!)
            .ToList();
    }

    /// <summary>Филиал агента — где больше всего его точек (Linko: ответственный агент).</summary>
    private async Task<Dictionary<long, (long? BranchId, int Markets)>> AgentBranchesAsync(long[] linkoIds, CancellationToken ct)
    {
        if (linkoIds.Length == 0)
        {
            return [];
        }

        var rows = await db.LinkoMarkets.AsNoTracking()
            .Where(m => m.ResponsibleAgentId != null && linkoIds.Contains(m.ResponsibleAgentId.Value) && m.BranchId != null)
            .GroupBy(m => new { Agent = m.ResponsibleAgentId!.Value, m.BranchId })
            .Select(g => new { g.Key.Agent, g.Key.BranchId, Count = g.Count() })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.Agent).ToDictionary(g => g.Key, g => ((long?)g.OrderByDescending(r => r.Count).First().BranchId, g.Sum(r => r.Count)));
    }

    /// <summary>Филиал супервайзера — по точкам, где он бывал с визитами, иначе по названию филиала в должности.</summary>
    private async Task<Dictionary<long, long>> SupervisorBranchesAsync(IReadOnlyList<StaffRow> supervisors, IReadOnlyList<FieldBranch> branches, CancellationToken ct)
    {
        var ids = supervisors.Select(s => s.Id).ToArray();
        var visited = await (
                from v in db.LinkoVisits.AsNoTracking()
                join m in db.LinkoMarkets.AsNoTracking() on v.MarketId equals (long?)m.Id
                where v.UserId != null && ids.Contains(v.UserId.Value) && m.BranchId != null
                group m by new { User = v.UserId!.Value, m.BranchId } into g
                select new { g.Key.User, g.Key.BranchId, Count = g.Count() })
            .ToListAsync(ct);
        var result = visited.GroupBy(v => v.User).ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.Count).First().BranchId!.Value);
        foreach (var s in supervisors.Where(s => !result.ContainsKey(s.Id)))
        {
            var text = $"{s.Name} {s.Position}";
            if (branches.FirstOrDefault(b => text.Contains(b.Name, StringComparison.OrdinalIgnoreCase)) is { } match)
            {
                result[s.Id] = match.Id;
            }
        }

        return result;
    }

    /// <summary>
    /// Импорт из Linko: недостающие агенты и супервайзеры, команда на каждый филиал с агентами; супервайзер ставится во главе
    /// команды своего филиала, если у неё ещё нет супервайзера. Существующих участников и команды не трогает.
    /// </summary>
    public async Task<FieldImportResult> ImportAsync(FieldScope scope, CancellationToken ct)
    {
        RequireManageOrg(scope);
        var staff = await LinkoStaffAsync(ct);
        var existing = await db.FieldMembers.Where(m => m.LinkoUserId != null).Select(m => m.LinkoUserId!.Value).ToListAsync(ct);
        var fresh = staff.Where(s => !existing.Contains(s.Id)).ToList();
        var branches = (await linko.BranchesAsync(ct)).Select(b => new FieldBranch(b.Id, b.Name)).ToList();
        // Названия всех филиалов (и исключённых из отчётов) — чтобы команда называлась «Завод», а не «Филиал 4».
        var branchName = (await db.LinkoMarkets.AsNoTracking().Where(m => m.BranchId != null && m.BranchName != null)
                .Select(m => new { m.BranchId, m.BranchName }).Distinct().ToListAsync(ct))
            .GroupBy(b => b.BranchId!.Value).ToDictionary(g => g.Key, g => g.First().BranchName!);

        var agentBranches = await AgentBranchesAsync(fresh.Where(s => s.Role == FieldRole.Agent).Select(s => s.Id).ToArray(), ct);
        var teams = await db.FieldTeams.ToListAsync(ct);
        var teamsCreated = 0;
        FieldTeam TeamFor(long branchId)
        {
            var team = teams.FirstOrDefault(t => t.BranchId == branchId && t.IsActive);
            if (team is null)
            {
                team = new FieldTeam { Name = branchName.GetValueOrDefault(branchId, $"Филиал {branchId}"), BranchId = branchId };
                db.FieldTeams.Add(team);
                teams.Add(team);
                teamsCreated++;
            }

            return team;
        }

        var agents = 0;
        foreach (var s in fresh.Where(s => s.Role == FieldRole.Agent))
        {
            var branch = agentBranches.GetValueOrDefault(s.Id).BranchId;
            db.FieldMembers.Add(new FieldMember { FullName = s.Name, Role = FieldRole.Agent, LinkoUserId = s.Id, TeamId = branch is { } b ? TeamFor(b).Id : null });
            agents++;
        }

        var supervisors = fresh.Where(s => s.Role == FieldRole.Supervisor).ToList();
        var supervisorBranches = await SupervisorBranchesAsync(supervisors, branches, ct);
        var linked = 0;
        foreach (var s in supervisors)
        {
            var member = new FieldMember { FullName = s.Name, Role = FieldRole.Supervisor, LinkoUserId = s.Id };
            db.FieldMembers.Add(member);
            if (supervisorBranches.TryGetValue(s.Id, out var branch) && teams.FirstOrDefault(t => t.BranchId == branch && t.IsActive && t.SupervisorId == null) is { } team)
            {
                team.SupervisorId = member.Id;
                linked++;
            }
        }

        await db.SaveChangesAsync(ct);
        directory.Invalidate();
        await audit.LogAsync(ActorType.User, scope.UserId.ToString(), "field.members.imported", "field", null,
            new { agents, supervisors = supervisors.Count, teamsCreated, linked }, ct);
        return new FieldImportResult(agents, supervisors.Count, teamsCreated, linked);
    }

    public async Task<Guid> SaveMemberAsync(FieldScope scope, Guid? id, FieldMemberInput input, CancellationToken ct)
    {
        RequireManage(scope);
        var name = (input.FullName ?? string.Empty).Trim();
        if (name.Length is 0 or > 200)
        {
            throw new FieldValidationException("Укажите имя участника (до 200 символов).");
        }

        if (input.Phone is { Length: > 32 })
        {
            throw new FieldValidationException("Телефон — до 32 символов.");
        }

        if (input.TeamId is { } teamId && !await db.FieldTeams.AnyAsync(t => t.Id == teamId, ct))
        {
            throw new FieldValidationException("Такой команды нет.");
        }

        var member = id is null ? null : await db.FieldMembers.FirstOrDefaultAsync(m => m.Id == id, ct) ?? throw new FieldNotFoundException("Участник не найден.");
        var isSelf = member is not null && scope.MemberId == member.Id;
        if (isSelf)
        {
            var sameBranches = input.Role != FieldRole.Rm
                || (input.BranchIds ?? []).Distinct().Order().SequenceEqual(member!.BranchIds.Order());
            if (!input.IsActive || input.Role != member!.Role || input.LinkoUserId != member.LinkoUserId || input.TeamId != member.TeamId || !sameBranches)
            {
                throw new FieldValidationException("Свою роль, доступ, филиалы, команду и привязку к Linko изменить нельзя — попросите другого РМ или администратора.");
            }
        }
        else
        {
            if (member is not null && !CanManageMember(scope, member.Id, member.Role))
            {
                throw new FieldNotFoundException("Участник не найден.");
            }

            if (input.Role == FieldRole.Rm && !scope.CanManageOrg)
            {
                throw new FieldForbiddenException("РМ добавляет и меняет управляющий всей организацией.");
            }

            // РМ филиалов: агент — только в команду своего филиала, сотрудник Linko — только своего филиала.
            if (!scope.CanManageOrg)
            {
                if (input.Role == FieldRole.Agent && (input.TeamId is not { } team || !scope.TeamIds.Contains(team)))
                {
                    throw new FieldValidationException("Выберите команду своего филиала.");
                }

                if (input.LinkoUserId is { } lid && lid != member?.LinkoUserId
                    && await db.LinkoMarkets.AnyAsync(m => m.ResponsibleAgentId == lid && (m.BranchId == null || !scope.BranchIds.Contains(m.BranchId.Value)), ct))
                {
                    throw new FieldValidationException("Этот сотрудник Linko работает в другом филиале.");
                }
            }
        }

        if (input.LinkoUserId is { } linkoId)
        {
            if (!await db.LinkoUsers.AnyAsync(u => u.Id == linkoId, ct))
            {
                throw new FieldValidationException("Сотрудник Linko не найден.");
            }

            if (await db.FieldMembers.AnyAsync(m => m.LinkoUserId == linkoId && m.Id != id, ct))
            {
                throw new FieldValidationException("Этот сотрудник Linko уже есть в Sales Base.");
            }
        }

        var isNew = member is null;
        member ??= new FieldMember { FullName = name };
        var wasActiveAgent = !isNew && member.Role == FieldRole.Agent && member.IsActive;

        member.FullName = name;
        member.Role = input.Role;
        member.Phone = string.IsNullOrWhiteSpace(input.Phone) ? null : input.Phone.Trim();
        member.TeamId = input.Role == FieldRole.Agent ? input.TeamId : null;
        member.LinkoUserId = input.LinkoUserId;
        member.BranchIds = input.Role == FieldRole.Rm ? input.BranchIds?.Distinct().ToList() ?? [] : [];
        member.IsActive = input.IsActive;
        member.UpdatedAt = DateTimeOffset.UtcNow;
        if (isNew)
        {
            db.FieldMembers.Add(member);
        }

        // Агент отключён или стал не агентом — точки, назначенные ему в Sales Base, освобождаются: иначе они пропадают из всех зон
        // и не попадают в рекомендации «точка без агента». Дальше точку ведёт ответственный из Linko или её назначают заново.
        var released = 0;
        if (wasActiveAgent && !(member.Role == FieldRole.Agent && member.IsActive))
        {
            var assigned = await db.FieldCustomers.Where(c => c.AssignedAgentId == member.Id).ToListAsync(ct);
            foreach (var customer in assigned)
            {
                customer.AssignedAgentId = null;
                customer.UpdatedAt = DateTimeOffset.UtcNow;
            }

            released = assigned.Count;
        }

        await db.SaveChangesAsync(ct);
        directory.Invalidate();
        await audit.LogAsync(ActorType.User, scope.UserId.ToString(), isNew ? "field.member.created" : "field.member.updated", "field_member", member.Id.ToString(),
            new { role = member.Role.ToString(), member.TeamId, member.IsActive, member.LinkoUserId, releasedMarkets = released }, ct);
        return member.Id;
    }

    public async Task<Guid> SaveTeamAsync(FieldScope scope, Guid? id, FieldTeamInput input, CancellationToken ct)
    {
        RequireManage(scope);
        var name = (input.Name ?? string.Empty).Trim();
        if (name.Length is 0 or > 200)
        {
            throw new FieldValidationException("Укажите название команды (до 200 символов).");
        }

        var team = id is null ? null : await db.FieldTeams.FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new FieldNotFoundException("Команда не найдена.");
        if (!scope.CanManageOrg)
        {
            if (team is not null && !scope.TeamIds.Contains(team.Id))
            {
                throw new FieldNotFoundException("Команда не найдена.");
            }

            if (input.BranchId is not { } branch || !scope.BranchIds.Contains(branch))
            {
                throw new FieldValidationException("Выберите свой филиал.");
            }
        }

        if (input.SupervisorId is { } sid)
        {
            var supervisor = await db.FieldMembers.AsNoTracking().FirstOrDefaultAsync(m => m.Id == sid && m.Role == FieldRole.Supervisor, ct)
                             ?? throw new FieldValidationException("Во главе команды может быть только супервайзер.");
            if (!scope.CanManageOrg)
            {
                // Супервайзер своей зоны или свободный: иначе через новую команду РМ получил бы его команды в других филиалах.
                var otherTeams = await db.FieldTeams.Where(t => t.SupervisorId == sid && t.IsActive && t.Id != id).Select(t => t.Id).ToListAsync(ct);
                if (!supervisor.IsActive || otherTeams.Any(t => !scope.TeamIds.Contains(t)))
                {
                    throw new FieldValidationException("Этот супервайзер ведёт команды другого филиала — назначает РМ всей организации.");
                }
            }
        }
        var isNew = team is null;
        team ??= new FieldTeam { Name = name };
        team.Name = name;
        team.SupervisorId = input.SupervisorId;
        team.BranchId = input.BranchId;
        team.IsActive = input.IsActive;
        team.UpdatedAt = DateTimeOffset.UtcNow;
        if (isNew)
        {
            db.FieldTeams.Add(team);
        }

        await db.SaveChangesAsync(ct);
        directory.Invalidate();
        await audit.LogAsync(ActorType.User, scope.UserId.ToString(), isNew ? "field.team.created" : "field.team.updated", "field_team", team.Id.ToString(),
            new { team.SupervisorId, team.BranchId, team.IsActive }, ct);
        return team.Id;
    }

    /// <summary>Привязать созданного пользователя OneBase к участнику (после выдачи доступа в контроллере).</summary>
    public async Task LinkUserAsync(FieldScope scope, Guid memberId, Guid userId, CancellationToken ct)
    {
        RequireManage(scope);
        var member = await db.FieldMembers.FirstOrDefaultAsync(m => m.Id == memberId, ct) ?? throw new FieldNotFoundException("Участник не найден.");
        if (!CanManageMember(scope, member.Id, member.Role))
        {
            throw new FieldNotFoundException("Участник не найден.");
        }

        if (await db.FieldMembers.AnyAsync(m => m.UserId == userId && m.Id != memberId, ct))
        {
            throw new FieldValidationException("Этот пользователь уже привязан к другому участнику.");
        }

        member.UserId = userId;
        member.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        directory.Invalidate();
    }

    public static void RequireManage(FieldScope scope)
    {
        if (!scope.CanManage)
        {
            throw new FieldForbiddenException("Состав Sales Base меняет РМ или администратор.");
        }
    }

    /// <summary>Импорт, настройки, журнал и РМ — только РМ всей организации или администратор.</summary>
    public static void RequireManageOrg(FieldScope scope)
    {
        if (!scope.CanManageOrg)
        {
            throw new FieldForbiddenException("Это делает РМ всей организации или администратор.");
        }
    }

    /// <summary>
    /// Можно ли менять участника (карточку, доступ, пароль): РМ всей организации — любого, кроме себя (свои поля проверяются отдельно);
    /// РМ филиалов — агентов и супервайзеров своей зоны. Свободные супервайзеры видны РМ филиала только для назначения в команду.
    /// </summary>
    public static bool CanManageMember(FieldScope scope, Guid memberId, FieldRole role) =>
        scope.CanManageOrg || (scope.CanManage && role != FieldRole.Rm && scope.CanSeeMember(memberId));
}
