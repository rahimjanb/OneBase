using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Security;
using OneBase.Domain.Audit;
using OneBase.Domain.Field;
using OneBase.Domain.Identity;

namespace OneBase.Api.Controllers;

/// <summary>Sales Base: профиль, «Сегодня», дашборды, KPI, агенты, состав и доступы, настройки, уведомления.</summary>
[Route("api/field")]
public sealed partial class FieldController(
    FieldAccess access,
    FieldDashboardService dashboards,
    FieldMemberService members,
    FieldSettingsStore settings,
    FieldDirectory directory,
    IAppDbContext db,
    IPasswordHasher<User> hasher,
    UserAccessCache userAccess,
    IAuditLogger audit) : FieldControllerBase(access)
{
    [HttpGet("me")]
    public async Task<FieldMeView> Me(CancellationToken ct) =>
        await dashboards.MeAsync(await ScopeAsync(ct), User.FindFirst(OneBaseClaims.Name)?.Value ?? "Пользователь", ct);

    [HttpGet("today")]
    public async Task<FieldTodayView> Today([FromQuery] Guid? agentId, [FromQuery] DateOnly? date, CancellationToken ct) =>
        await dashboards.TodayAsync(await ScopeAsync(ct), agentId, date, ct);

    [HttpGet("dashboard")]
    public async Task<FieldDashboardView> Dashboard([FromQuery] DateOnly? date, [FromQuery] Guid? teamId, [FromQuery] long? branchId, [FromQuery] Guid? agentId, CancellationToken ct) =>
        await dashboards.DashboardAsync(await ScopeAsync(ct), date, teamId, branchId, agentId, ct);

    [HttpGet("kpi")]
    public async Task<FieldKpiView> Kpi([FromQuery] int? year, [FromQuery] int? month, [FromQuery] Guid? teamId, [FromQuery] long? branchId, CancellationToken ct) =>
        await dashboards.KpiAsync(await ScopeAsync(ct), year, month, teamId, branchId, ct);

    public sealed record AgentOption(Guid Id, string Name, string? TeamName, FieldRole Role);

    /// <summary>Участники зоны для выбора исполнителя (агенты; для РМ — и супервайзеры).</summary>
    [HttpGet("agents")]
    public async Task<List<AgentOption>> Agents(CancellationToken ct)
    {
        var scope = await ScopeAsync(ct);
        var snapshot = await directory.GetAsync(ct);
        return snapshot.Members.Values
            .Where(m => m.IsActive && scope.CanSeeMember(m.Id) && m.Role != FieldRole.Rm)
            .Select(m => new AgentOption(m.Id, m.FullName, m.TeamId is { } t ? snapshot.Teams.GetValueOrDefault(t)?.Name : null, m.Role))
            .OrderBy(m => m.Role).ThenBy(m => m.TeamName).ThenBy(m => m.Name)
            .ToList();
    }

    [HttpGet("agents/{id:guid}")]
    public async Task<FieldAgentCard> Agent(Guid id, [FromQuery] DateOnly? date, CancellationToken ct) =>
        await dashboards.AgentAsync(await ScopeAsync(ct), id, date, ct);

    // ── Состав: участники, команды, импорт из Linko ──

    [HttpGet("structure")]
    public async Task<FieldStructureView> Structure(CancellationToken ct) => await members.GetAsync(await ScopeAsync(ct), ct);

    [HttpPost("structure/import")]
    public async Task<FieldImportResult> Import(CancellationToken ct) => await members.ImportAsync(await ScopeAsync(ct), ct);

    [HttpPost("members")]
    public async Task<IActionResult> CreateMember(FieldMemberInput input, CancellationToken ct) =>
        Ok(new { id = await members.SaveMemberAsync(await ScopeAsync(ct), null, input, ct) });

    [HttpPut("members/{id:guid}")]
    public async Task<IActionResult> UpdateMember(Guid id, FieldMemberInput input, CancellationToken ct) =>
        Ok(new { id = await members.SaveMemberAsync(await ScopeAsync(ct), id, input, ct) });

    [HttpPost("teams")]
    public async Task<IActionResult> CreateTeam(FieldTeamInput input, CancellationToken ct) =>
        Ok(new { id = await members.SaveTeamAsync(await ScopeAsync(ct), null, input, ct) });

    [HttpPut("teams/{id:guid}")]
    public async Task<IActionResult> UpdateTeam(Guid id, FieldTeamInput input, CancellationToken ct) =>
        Ok(new { id = await members.SaveTeamAsync(await ScopeAsync(ct), id, input, ct) });

    public sealed record AccessInput(string? Login, string? Password);

    /// <summary>
    /// Выдать участнику вход в Sales Base: пользователь OneBase с ролью по роли участника (Агент / Супервайзер / РМ).
    /// Пароль показывается один раз. Если вход уже есть — задаётся новый пароль. Роль можно выдать, только если у вас есть все её права.
    /// РМ филиалов — только агентам и супервайзерам своей зоны; вход РМ — только РМ всей организации. Пароль сбрасывается лишь у входа
    /// «только Sales Base» с ролью ниже вашей (иначе — администратор пользователей); отключённый вход не включается.
    /// </summary>
    [HttpPost("members/{id:guid}/access")]
    public async Task<IActionResult> GrantAccess(Guid id, AccessInput input, CancellationToken ct)
    {
        var scope = await ScopeAsync(ct);
        FieldMemberService.RequireManage(scope);
        var member = await db.FieldMembers.FirstOrDefaultAsync(m => m.Id == id, ct) ?? throw new FieldNotFoundException("Участник не найден.");
        if (scope.MemberId == member.Id || member.UserId == User.GetUserId())
        {
            throw new FieldForbiddenException("Свой пароль меняйте в профиле OneBase.");
        }

        if (!FieldMemberService.CanManageMember(scope, member.Id, member.Role))
        {
            throw new FieldNotFoundException("Участник не найден.");
        }

        var password = string.IsNullOrWhiteSpace(input.Password) ? GeneratePassword() : input.Password;
        if (password.Length < UsersController.MinPasswordLength)
        {
            throw new FieldValidationException($"Пароль — не короче {UsersController.MinPasswordLength} символов.");
        }

        if (member.UserId is { } existingId && await db.Users.FirstOrDefaultAsync(u => u.Id == existingId, ct) is { } existing)
        {
            var mine = MyPermissions();
            var currentPermissions = await db.Roles.Where(r => r.Users.Any(u => u.UserId == existing.Id)).SelectMany(r => r.Permissions.Select(p => p.Code)).ToListAsync(ct);
            if (!currentPermissions.All(mine.Contains))
            {
                throw new FieldForbiddenException("У этого пользователя права, которых у вас нет, — пароль меняет администратор.");
            }

            if (!mine.Contains(Permissions.UsersManage))
            {
                // Без права на пользователей — только вход «только Sales Base» агента или супервайзера: не чужая учётная запись OneBase и не РМ.
                var roleNames = await db.Roles.Where(r => r.Users.Any(u => u.UserId == existing.Id)).Select(r => r.Name).ToListAsync(ct);
                if (member.Role == FieldRole.Rm || roleNames.Any(r => r is not (SystemRoles.FieldAgent or SystemRoles.FieldSupervisor)))
                {
                    throw new FieldForbiddenException("Этот вход — не только для Sales Base: пароль меняет администратор пользователей.");
                }
            }

            if (!existing.IsActive)
            {
                throw new FieldForbiddenException("Вход отключён администратором — включает администратор пользователей.");
            }

            existing.PasswordHash = hasher.HashPassword(existing, password);
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            userAccess.Invalidate(existing.Id);
            await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "field.member.password_reset", "field_member", member.Id.ToString(), null, ct);
            return Ok(new { login = existing.Login, password });
        }

        var roleName = member.Role switch
        {
            FieldRole.Rm => SystemRoles.FieldRm,
            FieldRole.Supervisor => SystemRoles.FieldSupervisor,
            _ => SystemRoles.FieldAgent,
        };
        var role = await db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Name == roleName, ct)
                   ?? throw new FieldValidationException($"Роль «{roleName}» не найдена — перезапустите API, роли создаются при запуске.");
        if (!role.Permissions.Select(p => p.Code).All(MyPermissions().Contains))
        {
            throw new FieldForbiddenException($"Роль «{roleName}» даёт права, которых у вас нет.");
        }

        var linkoLogin = member.LinkoUserId is { } lid ? await db.LinkoUsers.Where(u => u.Id == lid).Select(u => u.Username).FirstOrDefaultAsync(ct) : null;
        var login = (input.Login ?? string.Empty).Trim().ToLowerInvariant();
        if (login.Length == 0)
        {
            login = SuggestLogin(linkoLogin, member);
        }

        if (!LoginPattern().IsMatch(login))
        {
            throw new FieldValidationException("Логин — от 3 до 64 символов: латинские буквы, цифры, точка, дефис, подчёркивание или @.");
        }

        if (await db.Users.AnyAsync(u => u.Login == login || u.Email == login, ct))
        {
            if (!string.IsNullOrEmpty(input.Login))
            {
                throw new FieldValidationException($"Логин «{login}» уже занят.");
            }

            login = $"{login}.{RandomNumberGenerator.GetInt32(100, 999)}";
        }

        var parts = member.FullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var user = new User
        {
            Login = login,
            FirstName = parts.ElementAtOrDefault(0) ?? member.FullName,
            LastName = parts.ElementAtOrDefault(1) ?? string.Empty,
            FullName = member.FullName,
            Phone = member.Phone,
            Position = roleName,
            DepartmentId = role.DepartmentId,
        };
        user.PasswordHash = hasher.HashPassword(user, password);
        user.Roles.Add(new UserRole { RoleId = role.Id });
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        await members.LinkUserAsync(scope, member.Id, user.Id, ct);
        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "field.member.access_granted", "field_member", member.Id.ToString(), new { role = roleName, userId = user.Id }, ct);
        return Ok(new { login, password });
    }

    private static string SuggestLogin(string? linkoLogin, FieldMember member)
    {
        var candidate = (linkoLogin ?? string.Empty).Trim().ToLowerInvariant();
        candidate = new string(candidate.Where(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_' or '-' or '@').ToArray());
        if (candidate.Length >= 3)
        {
            return candidate.Length <= 64 ? candidate : candidate[..64];
        }

        var prefix = member.Role switch { FieldRole.Rm => "rm", FieldRole.Supervisor => "sv", _ => "agent" };
        return member.LinkoUserId is { } id ? $"{prefix}{id}" : $"{prefix}{RandomNumberGenerator.GetInt32(1000, 9999)}";
    }

    /// <summary>Пароль из 10 символов без похожих (0/O, 1/l/I).</summary>
    private static string GeneratePassword()
    {
        const string alphabet = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return new string(Enumerable.Range(0, 10).Select(_ => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)]).ToArray());
    }

    [GeneratedRegex("^[a-z0-9._@-]{3,64}$")]
    private static partial Regex LoginPattern();

    // ── Настройки ──

    [HttpGet("settings")]
    public async Task<FieldSettings> GetSettings(CancellationToken ct)
    {
        await ScopeAsync(ct);
        return await settings.GetAsync(ct);
    }

    [HttpPut("settings")]
    public async Task<FieldSettings> PutSettings(FieldSettings input, CancellationToken ct)
    {
        var scope = await ScopeAsync(ct);
        FieldMemberService.RequireManageOrg(scope);
        if (input.GeoRadiusM is < 10 or > 5000 || input.GpsToleranceM is < 0 or > 2000 || input.MaxRoutePoints is < 1 or > 100 || input.VisitMinutes is < 1 or > 240
            || input.TravelSpeedKmh is < 5 or > 120 || input.NotVisitedDays is < 1 or > 180 || input.DeclinePct is < 5 or > 95 || input.DeclineMinSales < 0 || input.BehindPlanPct is < 1 or > 100)
        {
            throw new FieldValidationException("Проверьте значения: радиус 10–5000 м, допуск GPS 0–2000 м, точек 1–100, визит 1–240 мин, скорость 5–120 км/ч, давно не посещали 1–180 дн., падение 5–95%, отставание 1–100 п.п.");
        }

        var saved = await settings.UpdateAsync(s =>
        {
            s.GeoRadiusM = input.GeoRadiusM;
            s.GpsToleranceM = input.GpsToleranceM;
            s.MaxRoutePoints = input.MaxRoutePoints;
            s.VisitMinutes = input.VisitMinutes;
            s.TravelSpeedKmh = input.TravelSpeedKmh;
            s.DayStart = input.DayStart;
            s.NotVisitedDays = input.NotVisitedDays;
            s.DeclinePct = input.DeclinePct;
            s.DeclineMinSales = input.DeclineMinSales;
            s.BehindPlanPct = input.BehindPlanPct;
            s.AutoPlanning = input.AutoPlanning;
        }, ct);
        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "field.settings.updated", "field_settings", "1",
            new { saved.GeoRadiusM, saved.GpsToleranceM, saved.AutoPlanning, saved.MaxRoutePoints }, ct);
        return saved;
    }

    // ── Уведомления ──

    public sealed record NotificationRow(Guid Id, FieldNotificationKind Kind, string Title, string? Body, string? Link, DateTimeOffset CreatedAt, bool Read);

    [HttpGet("notifications")]
    public async Task<IActionResult> Notifications([FromQuery] bool unread = false, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        var scope = await ScopeAsync(ct);
        if (scope.MemberId is not { } me)
        {
            return Ok(new { items = Array.Empty<NotificationRow>(), unread = 0 });
        }

        var q = db.FieldNotifications.AsNoTracking().Where(n => n.RecipientId == me);
        if (unread)
        {
            q = q.Where(n => n.ReadAt == null);
        }

        var items = await q.OrderByDescending(n => n.CreatedAt).Take(Math.Clamp(take, 1, 200))
            .Select(n => new NotificationRow(n.Id, n.Kind, n.Title, n.Body, n.Link, n.CreatedAt, n.ReadAt != null))
            .ToListAsync(ct);
        var count = await db.FieldNotifications.CountAsync(n => n.RecipientId == me && n.ReadAt == null, ct);
        return Ok(new { items, unread = count });
    }

    [HttpPost("notifications/{id:guid}/read")]
    public async Task<IActionResult> Read(Guid id, CancellationToken ct)
    {
        var scope = await ScopeAsync(ct);
        await db.FieldNotifications.Where(n => n.Id == id && n.RecipientId == scope.MemberId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTimeOffset.UtcNow), ct);
        return NoContent();
    }

    [HttpPost("notifications/read-all")]
    public async Task<IActionResult> ReadAll(CancellationToken ct)
    {
        var scope = await ScopeAsync(ct);
        await db.FieldNotifications.Where(n => n.RecipientId == scope.MemberId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTimeOffset.UtcNow), ct);
        return NoContent();
    }

    /// <summary>Журнал действий Sales Base (для РМ): смены агентов, маршрутов, задач, решений по AI.</summary>
    [HttpGet("audit")]
    public async Task<IActionResult> Audit([FromQuery] string? entityType, [FromQuery] string? entityId, [FromQuery] int take = 100, CancellationToken ct = default)
    {
        var scope = await ScopeAsync(ct);
        FieldMemberService.RequireManageOrg(scope);
        var q = db.AuditLogs.AsNoTracking().Where(a => a.Action.StartsWith("field."));
        if (!string.IsNullOrEmpty(entityType))
        {
            q = q.Where(a => a.EntityType == entityType);
        }

        if (!string.IsNullOrEmpty(entityId))
        {
            q = q.Where(a => a.EntityId == entityId);
        }

        var rows = await q.OrderByDescending(a => a.Timestamp).Take(Math.Clamp(take, 1, 500))
            .Select(a => new { a.Id, a.Timestamp, a.ActorId, a.Action, a.EntityType, a.EntityId, a.Data })
            .ToListAsync(ct);
        var actorIds = rows.Select(r => Guid.TryParse(r.ActorId, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).Distinct().ToArray();
        var names = await db.Users.AsNoTracking().Where(u => actorIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id.ToString(), u => u.FullName, ct);
        return Ok(rows.Select(r => new { r.Id, r.Timestamp, Actor = names.GetValueOrDefault(r.ActorId, r.ActorId), r.Action, r.EntityType, r.EntityId, r.Data }));
    }
}
