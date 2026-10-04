using OneBase.Domain.Field;

namespace OneBase.Application.Field;

/// <summary>
/// Что видит текущий пользователь в Sales Base. Строится на backend из участника (field.Members) и команд,
/// никогда — из параметров запроса. РМ — организация или свои филиалы, супервайзер — агенты своих команд, агент — только себя.
/// </summary>
public sealed class FieldScope
{
    public required Guid UserId { get; init; }

    /// <summary>Участник Sales Base; null — администратор/РМ (field.manage) или руководитель (field.plan) без карточки участника.</summary>
    public Guid? MemberId { get; init; }

    public required FieldRole Role { get; init; }

    /// <summary>Управление составом и доступами: только РМ (карточка РМ или администратор без карточки) с правом field.manage.</summary>
    public required bool CanManage { get; init; }

    /// <summary>Вся организация, без ограничения по филиалам.</summary>
    public required bool OrgWide { get; init; }

    /// <summary>Филиалы РМ; пусто — вся организация (или не РМ).</summary>
    public IReadOnlyList<long> BranchIds { get; init; } = [];

    /// <summary>Агенты в области.</summary>
    public required IReadOnlySet<Guid> AgentIds { get; init; }

    /// <summary>Участники в области: агенты, супервайзеры их команд (для РМ), сам пользователь.</summary>
    public required IReadOnlySet<Guid> MemberIds { get; init; }

    /// <summary>Команды в области.</summary>
    public required IReadOnlySet<Guid> TeamIds { get; init; }

    /// <summary>Сотрудник Linko каждого участника в области.</summary>
    public required IReadOnlyDictionary<Guid, long> LinkoOf { get; init; }

    public bool IsAgent => Role == FieldRole.Agent;
    public bool IsSupervisor => Role == FieldRole.Supervisor;
    public bool IsRm => Role == FieldRole.Rm;

    /// <summary>
    /// Управление всей организацией: импорт из Linko, настройки, журнал, РМ-участники. РМ, ограниченный филиалами,
    /// управляет только командами и участниками своих филиалов.
    /// </summary>
    public bool CanManageOrg => CanManage && OrgWide;

    /// <summary>Планирует работу агентов (маршруты, задачи, назначения): РМ и супервайзер.</summary>
    public bool CanPlan => Role is FieldRole.Rm or FieldRole.Supervisor;

    public bool CanSeeAgent(Guid agentId) => AgentIds.Contains(agentId);
    public bool CanSeeMember(Guid memberId) => MemberIds.Contains(memberId);

    /// <summary>Менять маршрут, точки и задачи агента может РМ или супервайзер его команды.</summary>
    public bool CanPlanFor(Guid agentId) => CanPlan && AgentIds.Contains(agentId);

    /// <summary>Linko-сотрудники агентов в области (для запросов к продажам и визитам).</summary>
    public IReadOnlyCollection<long> AgentLinkoIds => AgentIds.Where(LinkoOf.ContainsKey).Select(a => LinkoOf[a]).ToList();
}

/// <summary>Нет доступа к Sales Base или к действию (403).</summary>
public sealed class FieldForbiddenException(string message) : Exception(message);

/// <summary>Объекта нет — или он вне области пользователя (404: о чужих данных не сообщаем).</summary>
public sealed class FieldNotFoundException(string message = "Не найдено.") : Exception(message);

/// <summary>Неверные данные запроса (400).</summary>
public sealed class FieldValidationException(string message) : Exception(message);

/// <summary>Конфликт состояния, например уже идёт другой визит (409).</summary>
public sealed class FieldConflictException(string message, object? details = null) : Exception(message)
{
    public object? Details { get; } = details;
}

/// <summary>Сборка области из участников и команд — чистая функция, проверяется тестами изоляции.</summary>
public static class FieldScopeBuilder
{
    public sealed record MemberRow(Guid Id, Guid? UserId, long? LinkoUserId, FieldRole Role, Guid? TeamId, IReadOnlyList<long> BranchIds, bool IsActive);

    public sealed record TeamRow(Guid Id, Guid? SupervisorId, long? BranchId, bool IsActive);

    /// <param name="canPlan">field.plan: руководитель планирует работу всей организации (задачи, маршруты) без управления составом.</param>
    public static FieldScope Build(Guid userId, bool canUse, bool canManage, IReadOnlyList<MemberRow> members, IReadOnlyList<TeamRow> teams, bool canPlan = false)
    {
        var me = members.FirstOrDefault(m => m.UserId == userId);
        var active = members.Where(m => m.IsActive).ToList();
        var activeTeams = teams.Where(t => t.IsActive).ToList();

        if (me is null)
        {
            if (!canManage && !canPlan)
            {
                throw new FieldForbiddenException("Вас ещё не добавили в Sales Base. Обратитесь к РМ или администратору.");
            }

            // Без карточки участника: администратор (field.manage) или руководитель (field.plan) — вся организация.
            // Управление составом, доступами и настройками — только с field.manage.
            return Scope(userId, null, FieldRole.Rm, canManage, orgWide: true, [],
                active.Where(m => m.Role == FieldRole.Agent).Select(m => m.Id), active.Select(m => m.Id), activeTeams.Select(t => t.Id), active);
        }

        // Отключённая карточка закрывает Sales Base даже при праве field.manage — иначе отключение РМ открывало бы ему всю организацию.
        if (!me.IsActive)
        {
            throw new FieldForbiddenException("Ваш доступ к Sales Base отключён.");
        }

        if (!canUse && !canManage && !canPlan)
        {
            throw new FieldForbiddenException("У вашей роли нет доступа к Sales Base.");
        }

        switch (me.Role)
        {
            case FieldRole.Rm:
            {
                if (me.BranchIds.Count == 0)
                {
                    return Scope(userId, me.Id, FieldRole.Rm, canManage, orgWide: true, [],
                        active.Where(m => m.Role == FieldRole.Agent).Select(m => m.Id), active.Select(m => m.Id), activeTeams.Select(t => t.Id), active);
                }

                var rmTeams = activeTeams.Where(t => t.BranchId is { } b && me.BranchIds.Contains(b)).ToList();
                var teamIds = rmTeams.Select(t => t.Id).ToHashSet();
                var agents = active.Where(m => m.Role == FieldRole.Agent && m.TeamId is { } t && teamIds.Contains(t)).Select(m => m.Id).ToList();

                // Супервайзер — в области, только если все его команды в филиалах РМ: иначе через него видны чужие филиалы.
                var supervisors = rmTeams.Where(t => t.SupervisorId is not null).Select(t => t.SupervisorId!.Value).Distinct()
                    .Where(s => activeTeams.Where(t => t.SupervisorId == s).All(t => teamIds.Contains(t.Id)));
                return Scope(userId, me.Id, FieldRole.Rm, canManage, orgWide: false, me.BranchIds, agents, agents.Concat(supervisors).Append(me.Id), teamIds, active);
            }

            case FieldRole.Supervisor:
            {
                // Управление составом — у РМ; супервайзер с правом field.manage управляет только планами своей команды.
                var teamIds = activeTeams.Where(t => t.SupervisorId == me.Id).Select(t => t.Id).ToHashSet();
                var agents = active.Where(m => m.Role == FieldRole.Agent && m.TeamId is { } t && teamIds.Contains(t)).Select(m => m.Id).ToList();
                return Scope(userId, me.Id, FieldRole.Supervisor, canManage: false, orgWide: false, [], agents, agents.Append(me.Id), teamIds, active);
            }

            default:
                return Scope(userId, me.Id, FieldRole.Agent, canManage: false, orgWide: false, [], [me.Id], [me.Id],
                    me.TeamId is { } team ? [team] : [], active);
        }
    }

    private static FieldScope Scope(Guid userId, Guid? memberId, FieldRole role, bool canManage, bool orgWide, IReadOnlyList<long> branchIds,
        IEnumerable<Guid> agents, IEnumerable<Guid> members, IEnumerable<Guid> teams, IReadOnlyList<MemberRow> all)
    {
        var memberSet = members.ToHashSet();
        return new FieldScope
        {
            UserId = userId,
            MemberId = memberId,
            Role = role,
            CanManage = canManage,
            OrgWide = orgWide,
            BranchIds = branchIds,
            AgentIds = agents.ToHashSet(),
            MemberIds = memberSet,
            TeamIds = teams.ToHashSet(),
            LinkoOf = all.Where(m => memberSet.Contains(m.Id) && m.LinkoUserId is not null).ToDictionary(m => m.Id, m => m.LinkoUserId!.Value),
        };
    }
}
