using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Domain.Audit;
using OneBase.Domain.Sales;

namespace OneBase.Application.Sales.Access;

/// <summary>
/// Охват пользователя в «Продажах» на этот запрос. Level = null — вся организация. Иначе — только своя часть оргструктуры: РМ — регионы
/// своих направлений, СВР — свои регионы, ТП — он сам. Строится на сервере из назначения (sales.UserScopes) и оргструктуры, никогда — из
/// параметров запроса; страницы и API проверяют по нему каждый id (чужой — 404, как будто его нет).
/// </summary>
public sealed record SalesScope(SalesScopeLevel? Level, IReadOnlySet<Guid> DirectionIds, IReadOnlySet<Guid> RegionIds, long? AgentId)
{
    public static readonly SalesScope Organization = new(null, new HashSet<Guid>(), new HashSet<Guid>(), null);

    /// <summary>Вся организация: страна, первичка, экспорт, P&L и т. п.</summary>
    public bool IsOrganization => Level is null;

    /// <summary>Видимость для расчётов списков (Metrics.SalesVisibility): вся организация, регионы или один ТП.</summary>
    public Metrics.SalesVisibility Visibility =>
        IsOrganization ? Metrics.SalesVisibility.All : IsAgent ? Metrics.SalesVisibility.OfAgent(AgentId ?? -1) : Metrics.SalesVisibility.OfRegions(RegionIds);

    public bool IsRm => Level == SalesScopeLevel.Rm;
    public bool IsSupervisor => Level == SalesScopeLevel.Supervisor;
    public bool IsAgent => Level == SalesScopeLevel.Agent;

    /// <summary>Страница направления: вся организация или РМ этого направления.</summary>
    public bool CanSeeDirection(Guid? directionId) => IsOrganization || (IsRm && directionId is { } d && DirectionIds.Contains(d));

    /// <summary>Регион: вся организация, РМ или СВР с этим регионом. ТП регион целиком не видит.</summary>
    public bool CanSeeRegion(Guid? regionId) => IsOrganization || (!IsAgent && regionId is { } r && RegionIds.Contains(r));

    /// <summary>ТП: вся организация; РМ и СВР — ТП своих регионов (регион ТП — как его считает раздел продаж в этом месяце); ТП — только себя.</summary>
    public bool CanSeeAgent(long agentId, Guid? regionId) => IsOrganization || (IsAgent ? AgentId == agentId : CanSeeRegion(regionId));
}

/// <summary>Охват пользователя: кэш на минуту на пользователя, сбрасывается при смене назначения и при любой правке оргструктуры продаж.</summary>
public sealed class SalesAccess(IAppDbContext db, IMemoryCache cache, SalesCacheSignal signal)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(1);

    private sealed record Entry(string Version, SalesScope Scope);

    public async Task<SalesScope> ScopeAsync(Guid userId, CancellationToken ct)
    {
        // Версия данных продаж в записи: регион перевели в другое направление (правка оргструктуры сбрасывает signal) — охват РМ пересчитан.
        if (cache.TryGetValue(Key(userId), out Entry? hit) && hit is not null && hit.Version == signal.Version)
        {
            return hit.Scope;
        }

        var version = signal.Version;
        var scope = await BuildAsync(userId, ct);
        cache.Set(Key(userId), new Entry(version, scope), Ttl);
        return scope;
    }

    /// <summary>Назначение пользователя изменилось — охват пересчитывается на следующем запросе, а не через минуту.</summary>
    public void Invalidate(Guid userId) => cache.Remove(Key(userId));

    private async Task<SalesScope> BuildAsync(Guid userId, CancellationToken ct)
    {
        var row = await db.SalesUserScopes.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (row is null)
        {
            return SalesScope.Organization;
        }

        switch (row.Level)
        {
            case SalesScopeLevel.Rm:
            {
                // РМ — свои регионы (роль «РМ», регионы в окне пользователя) и/или регионы своих направлений. Страница направления открыта, если
                // все его регионы у РМ: иначе через неё видны чужие регионы. Регион перевели в другое направление — пересчёт сразу (signal).
                var regions = await db.SalesRegions.AsNoTracking()
                    .Where(r => row.RegionIds.Contains(r.Id) || (r.DirectionId != null && row.DirectionIds.Contains(r.DirectionId.Value)))
                    .Select(r => r.Id).ToListAsync(ct);
                var set = regions.ToHashSet();
                var byDirection = await db.SalesRegions.AsNoTracking().Where(r => r.DirectionId != null).Select(r => new { r.Id, Direction = r.DirectionId!.Value }).ToListAsync(ct);
                var covered = byDirection.GroupBy(r => r.Direction).Where(g => g.All(r => set.Contains(r.Id))).Select(g => g.Key).ToHashSet();
                return new SalesScope(SalesScopeLevel.Rm, covered, set, null);
            }

            case SalesScopeLevel.Supervisor:
            {
                var regions = await db.SalesRegions.AsNoTracking().Where(r => row.RegionIds.Contains(r.Id)).Select(r => r.Id).ToListAsync(ct);
                return new SalesScope(SalesScopeLevel.Supervisor, new HashSet<Guid>(), regions.ToHashSet(), null);
            }

            default:
                // ТП без сотрудника Linko не видит никого — не всю организацию.
                return new SalesScope(SalesScopeLevel.Agent, new HashSet<Guid>(), new HashSet<Guid>(), row.AgentId ?? -1);
        }
    }

    private static string Key(Guid userId) => $"sales-scope:{userId}";
}

/// <summary>Назначение охвата: уровень и его объекты. Level = null — вся организация (записи нет).</summary>
public sealed record SalesScopeInput(SalesScopeLevel? Level, IReadOnlyList<Guid>? DirectionIds, IReadOnlyList<Guid>? RegionIds, long? AgentId);

public sealed record SalesScopeOption(string Id, string Name, string? Hint);

/// <summary>Что можно назначить: направления РМ, регионы (с направлением и СВР), ТП Linko (действующие — сначала).</summary>
public sealed record SalesScopeOptions(IReadOnlyList<SalesScopeOption> Directions, IReadOnlyList<SalesScopeOption> Regions, IReadOnlyList<SalesScopeOption> Agents);

/// <summary>Назначение пользователя и кто его поставил.</summary>
public sealed record SalesScopeView(SalesScopeLevel? Level, IReadOnlyList<Guid> DirectionIds, IReadOnlyList<Guid> RegionIds, long? AgentId, string Title,
    DateTimeOffset? UpdatedAt);

/// <summary>
/// Назначение охвата пользователям (право «Пользователи и роли»). Проверяется, что направления, регионы и ТП существуют; ТП закрепляется
/// только за одним пользователем; свой охват не меняет никто сам себе. Каждое изменение — в журнал аудита («users.sales_scope»).
/// </summary>
public sealed class SalesScopeAdmin(IAppDbContext db, SalesAccess access, IAuditLogger audit, TimeProvider clock)
{
    public async Task<SalesScopeView> GetAsync(Guid userId, CancellationToken ct)
    {
        var row = await db.SalesUserScopes.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId, ct);
        return row is null
            ? new SalesScopeView(null, [], [], null, "Вся организация", null)
            : new SalesScopeView(row.Level, row.DirectionIds, row.RegionIds, row.AgentId, await TitleAsync(row.Level, row.DirectionIds, row.RegionIds, row.AgentId, ct),
                row.UpdatedAt);
    }

    public async Task<SalesScopeOptions> OptionsAsync(CancellationToken ct)
    {
        var directions = await db.SalesDirections.AsNoTracking().OrderBy(d => d.SortOrder).ThenBy(d => d.Name)
            .Select(d => new SalesScopeOption(d.Id.ToString(), d.Name, d.ManagerName)).ToListAsync(ct);
        var regions = await db.SalesRegions.AsNoTracking().OrderBy(r => r.Name)
            .Select(r => new SalesScopeOption(r.Id.ToString(), r.Name, r.Direction != null ? r.Direction.Name : null)).ToListAsync(ct);
        var agents = await db.LinkoUsers.AsNoTracking().OrderByDescending(u => u.IsActive).ThenBy(u => u.FirstName).ThenBy(u => u.SecondName)
            .Select(u => new { u.Id, u.FirstName, u.SecondName, u.JobName, u.IsActive }).ToListAsync(ct);
        return new SalesScopeOptions(directions, regions, agents.Select(a => new SalesScopeOption(a.Id.ToString(),
            string.Join(' ', new[] { a.FirstName, a.SecondName }.Where(s => !string.IsNullOrWhiteSpace(s))) is { Length: > 0 } n ? n : $"Linko {a.Id}",
            string.Join(" · ", new[] { a.JobName, a.IsActive ? null : "не работает" }.Where(s => !string.IsNullOrWhiteSpace(s))) is { Length: > 0 } h ? h : null)).ToList());
    }

    public async Task<SalesScopeView> SetAsync(Guid actorId, Guid userId, SalesScopeInput input, CancellationToken ct)
    {
        if (actorId == userId)
        {
            throw new FieldForbiddenException("Свой охват продаж не меняет никто сам себе — это делает другой администратор.");
        }

        if (!await db.Users.AnyAsync(u => u.Id == userId, ct))
        {
            throw new FieldNotFoundException("Пользователь не найден.");
        }

        var row = await db.SalesUserScopes.FirstOrDefaultAsync(s => s.UserId == userId, ct);
        var before = row is null ? null : new { Level = row.Level.ToString(), row.DirectionIds, row.RegionIds, row.AgentId };
        if (input.Level is null)
        {
            if (row is not null)
            {
                db.SalesUserScopes.Remove(row);
            }
        }
        else
        {
            var (directions, regions, agent) = await ValidateAsync(userId, input, ct);
            row ??= db.SalesUserScopes.Add(new SalesUserScope { UserId = userId }).Entity;
            row.Level = input.Level.Value;
            row.DirectionIds = directions;
            row.RegionIds = regions;
            row.AgentId = agent;
            row.UpdatedAt = clock.GetUtcNow();
            row.UpdatedById = actorId;
        }

        // Пользователь «изменился»: ключ кэша ответов AI-консультанта (по User.UpdatedAt) не отдаст ответ, собранный с прежним охватом.
        var user = await db.Users.FirstAsync(u => u.Id == userId, ct);
        user.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        access.Invalidate(userId);

        var after = input.Level is null ? null : new { Level = input.Level.Value.ToString(), row!.DirectionIds, row.RegionIds, row.AgentId };
        await audit.LogAsync(ActorType.User, actorId.ToString(), "users.sales_scope", "User", userId.ToString(), new { from = before, to = after }, ct);
        return await GetAsync(userId, ct);
    }

    private async Task<(List<Guid> Directions, List<Guid> Regions, long? Agent)> ValidateAsync(Guid userId, SalesScopeInput input, CancellationToken ct)
    {
        switch (input.Level)
        {
            case SalesScopeLevel.Rm:
            {
                var ids = (input.DirectionIds ?? []).Distinct().ToList();
                var regions = (input.RegionIds ?? []).Distinct().ToList();
                if (ids.Count == 0 && regions.Count == 0)
                {
                    throw new FieldValidationException("Выберите регионы РМ.");
                }

                if (await db.SalesDirections.CountAsync(d => ids.Contains(d.Id), ct) != ids.Count)
                {
                    throw new FieldValidationException("Направление не найдено — обновите страницу.");
                }

                if (await db.SalesRegions.CountAsync(r => regions.Contains(r.Id), ct) != regions.Count)
                {
                    throw new FieldValidationException("Регион не найден — обновите страницу.");
                }

                return (ids, regions, null);
            }

            case SalesScopeLevel.Supervisor:
            {
                var ids = (input.RegionIds ?? []).Distinct().ToList();
                if (ids.Count == 0)
                {
                    throw new FieldValidationException("Выберите регионы супервайзера.");
                }

                if (await db.SalesRegions.CountAsync(r => ids.Contains(r.Id), ct) != ids.Count)
                {
                    throw new FieldValidationException("Регион не найден — обновите страницу.");
                }

                return ([], ids, null);
            }

            default:
            {
                if (input.AgentId is not { } agent || !await db.LinkoUsers.AnyAsync(u => u.Id == agent, ct))
                {
                    throw new FieldValidationException("Выберите торгового представителя Linko.");
                }

                var taken = await db.SalesUserScopes.AsNoTracking().Where(s => s.AgentId == agent && s.UserId != userId).Select(s => s.UserId).FirstOrDefaultAsync(ct);
                if (taken != Guid.Empty)
                {
                    var name = await db.Users.Where(u => u.Id == taken).Select(u => u.FullName).FirstOrDefaultAsync(ct);
                    throw new FieldConflictException($"Этот ТП уже закреплён за пользователем «{name}».");
                }

                return ([], [], agent);
            }
        }
    }

    /// <summary>
    /// Домашняя страница раздела «Продажи» по охвату: вся организация — «/sales», РМ — его направление (первое по порядку оргструктуры),
    /// СВР — его регион (первый по названию), ТП — его карточка. Нет ни одного объекта — «/sales» (сервер всё равно отдаст 403/404).
    /// </summary>
    public async Task<string> HomeAsync(SalesScope scope, CancellationToken ct)
    {
        if (scope.IsAgent)
        {
            return $"/sales/agents/{scope.AgentId}";
        }

        if (scope.IsRm && await db.SalesDirections.AsNoTracking().Where(d => scope.DirectionIds.Contains(d.Id)).OrderBy(d => d.SortOrder).ThenBy(d => d.Name)
                .Select(d => (Guid?)d.Id).FirstOrDefaultAsync(ct) is { } direction)
        {
            return $"/sales/directions/{direction}";
        }

        if ((scope.IsSupervisor || scope.IsRm) && await db.SalesRegions.AsNoTracking().Where(r => scope.RegionIds.Contains(r.Id)).OrderBy(r => r.Name)
                .Select(r => (Guid?)r.Id).FirstOrDefaultAsync(ct) is { } region)
        {
            return $"/sales/regions/{region}";
        }

        return "/sales";
    }

    /// <summary>Уровень охвата по роли: «РМ» — РМ, «Супервайзер продаж» — супервайзер; другие роли охвата по регионам не дают.</summary>
    public static SalesScopeLevel? LevelOfRole(string? roleName) => roleName switch
    {
        Security.SystemRoles.SalesRm => SalesScopeLevel.Rm,
        Security.SystemRoles.SalesSupervisor => SalesScopeLevel.Supervisor,
        _ => null,
    };

    /// <summary>
    /// Регионы для роли из окна пользователя — до сохранения: РМ и супервайзеру продаж нужен хотя бы один существующий регион. Другим ролям
    /// регионы не нужны (передали — не ошибка, они не сохраняются).
    /// </summary>
    public async Task ValidateRoleAsync(string? roleName, IReadOnlyList<Guid>? regionIds, CancellationToken ct)
    {
        if (LevelOfRole(roleName) is null)
        {
            return;
        }

        var ids = (regionIds ?? []).Distinct().ToList();
        if (ids.Count == 0)
        {
            throw new FieldValidationException(roleName == Security.SystemRoles.SalesRm ? "Выберите регионы РМ." : "Выберите регионы супервайзера.");
        }

        if (await db.SalesRegions.CountAsync(r => ids.Contains(r.Id), ct) != ids.Count)
        {
            throw new FieldValidationException("Регион не найден — обновите страницу.");
        }
    }

    /// <summary>
    /// Охват по роли — после сохранения пользователя: роль «РМ» / «Супервайзер продаж» — эти регионы; роль сменили на другую — охват по
    /// регионам снимается (охват ТП, назначенный отдельно, остаётся). Без изменений — ничего не пишется. Изменение — в аудит, действует сразу.
    /// </summary>
    public async Task ApplyRoleAsync(Guid actorId, Guid userId, string? roleName, IReadOnlyList<Guid>? regionIds, CancellationToken ct)
    {
        var level = LevelOfRole(roleName);
        var row = await db.SalesUserScopes.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (level is null)
        {
            if (row is { Level: SalesScopeLevel.Rm or SalesScopeLevel.Supervisor })
            {
                await SetAsync(actorId, userId, new SalesScopeInput(null, null, null, null), ct);
            }

            return;
        }

        var regions = (regionIds ?? []).Distinct().Order().ToList();
        if (row is not null && row.Level == level && row.DirectionIds.Count == 0 && row.RegionIds.Order().SequenceEqual(regions))
        {
            return;
        }

        await SetAsync(actorId, userId, new SalesScopeInput(level, [], regions, null), ct);
    }

    /// <summary>Подпись охвата: «РМ: Ташкент, Восток», «СВР: Бухара», «ТП: Азиз Каримов», «Вся организация».</summary>
    public async Task<string> TitleAsync(SalesScopeLevel? level, IReadOnlyCollection<Guid> directions, IReadOnlyCollection<Guid> regions, long? agent, CancellationToken ct)
    {
        switch (level)
        {
            case null:
                return "Вся организация";
            case SalesScopeLevel.Rm:
                var d = await db.SalesRegions.AsNoTracking()
                    .Where(x => regions.Contains(x.Id) || (x.DirectionId != null && directions.Contains(x.DirectionId.Value))).OrderBy(x => x.Name).Select(x => x.Name).ToListAsync(ct);
                return $"РМ: {(d.Count > 0 ? string.Join(", ", d) : "—")}";
            case SalesScopeLevel.Supervisor:
                var r = await db.SalesRegions.AsNoTracking().Where(x => regions.Contains(x.Id)).OrderBy(x => x.Name).Select(x => x.Name).ToListAsync(ct);
                return $"СВР: {(r.Count > 0 ? string.Join(", ", r) : "—")}";
            default:
                var u = await db.LinkoUsers.AsNoTracking().Where(x => x.Id == agent).Select(x => new { x.FirstName, x.SecondName }).FirstOrDefaultAsync(ct);
                return $"ТП: {(u is null ? "—" : string.Join(' ', new[] { u.FirstName, u.SecondName }.Where(s => !string.IsNullOrWhiteSpace(s))))}";
        }
    }
}
