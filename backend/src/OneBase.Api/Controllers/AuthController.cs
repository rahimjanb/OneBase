using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Security;
using OneBase.Domain.Audit;
using OneBase.Domain.Identity;

namespace OneBase.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAppDbContext db,
    JwtTokenService tokens,
    UserSessionCache sessions,
    IPasswordHasher<User> hasher,
    IAuditLogger audit) : ControllerBase
{
    /// <summary>Login — логин или почта; Email — прежнее имя поля, принимается так же.</summary>
    public sealed record LoginRequest(string? Login, string? Email, string Password);

    public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt);

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var login = (request.Login ?? request.Email ?? string.Empty).Trim().ToLowerInvariant();
        var user = login.Length == 0
            ? null
            : await db.Users
                .Include(u => u.Roles).ThenInclude(ur => ur.Role).ThenInclude(r => r.Permissions)
                .FirstOrDefaultAsync(u => u.Login == login || u.Email == login, ct);

        var verification = user is { IsActive: true }
            ? hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password)
            : PasswordVerificationResult.Failed;

        if (user is null || verification == PasswordVerificationResult.Failed)
        {
            await audit.LogAsync(ActorType.System, "auth", "auth.login.failed", data: new { login }, cancellationToken: ct);
            return Unauthorized();
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, request.Password);
            await db.SaveChangesAsync(ct);
        }

        // Сеанс длится до выхода: токен живёт долго, а закрывается на сервере (выход, смена пароля).
        var session = new UserSession
        {
            UserId = user.Id,
            UserAgent = Short(Request.Headers.UserAgent.ToString(), 300),
            Ip = Short(HttpContext.Connection.RemoteIpAddress?.ToString(), 64),
        };
        db.UserSessions.Add(session);
        await db.SaveChangesAsync(ct);

        var roles = user.Roles.Select(ur => ur.Role.Name);
        var permissions = UserSections.Effective(user.Roles.SelectMany(ur => ur.Role.Permissions).Select(p => p.Code), user.Sections, user.Rights);
        var (token, expiresAt) = tokens.Create(user, session.Id, roles, permissions);

        await audit.LogAsync(ActorType.User, user.Id.ToString(), "auth.login", cancellationToken: ct);
        return new LoginResponse(token, expiresAt);
    }

    /// <summary>
    /// Выход: сеанс закрывается на сервере — токен этого входа больше не действует, даже если его скопировали. Другие входы
    /// пользователя (телефон, другой браузер) остаются.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (Guid.TryParse(User.FindFirstValue(OneBaseClaims.Session), out var sessionId))
        {
            var now = DateTimeOffset.UtcNow;
            await db.UserSessions.Where(s => s.Id == sessionId && s.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), ct);
            sessions.Invalidate(sessionId);
        }

        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "auth.logout", cancellationToken: ct);
        return NoContent();
    }

    private static string? Short(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Length <= max ? value : value[..max];

    /// <summary>Текущий пользователь: профиль — из БД (правки видны сразу), роли и права — актуальные (см. AuthSetup).</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me([FromServices] OneBase.Application.Hr.HrService hr, [FromServices] OneBase.Application.Recruitment.RecruitmentService recruitment,
        [FromServices] OneBase.Application.Agents.DialogAgentService agents,
        [FromServices] OneBase.Application.Sales.Access.SalesAccess salesAccess,
        [FromServices] OneBase.Application.Sales.Access.SalesScopeAdmin salesScopes,
        [FromServices] OneBase.Application.Work.WorkTaskService work,
        CancellationToken ct)
    {
        var id = User.GetUserId();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
        {
            return Unauthorized();
        }

        var permissions = User.FindAll(OneBaseClaims.Permission).Select(c => c.Value).ToList();
        var roles = User.FindAll(OneBaseClaims.Role).Select(c => c.Value).OrderBy(SystemRoles.OrderOf).ToList();
        var agentDepartments = (await agents.AccessAsync(id, permissions.ToHashSet(), ct)).Departments.Order(StringComparer.Ordinal).ToList();
        var department = user.DepartmentId is { } departmentId
            ? await db.Departments.AsNoTracking().Where(d => d.Id == departmentId).Select(d => d.Name).FirstOrDefaultAsync(ct)
            : null;
        return Ok(new
        {
            user.Id,
            user.Login,
            user.Email,
            Name = user.FullName,
            user.FirstName,
            user.LastName,
            user.Position,
            DepartmentName = department,
            Roles = roles,
            // Главная: у роли «Сотрудник» — «Мой день»; у кладовщика, лаборатории, снабжения и аналитика — «Рабочий стол» (поставки, приёмка,
            // качество, согласования); у остальных — «Обзор» компании.
            Home = roles.Contains(SystemRoles.EmployeeRole) ? "my_day" : WorksInSupply(roles, permissions) ? "work" : "overview",
            Permissions = permissions,
            CanOpenSettings = SystemRoles.SettingsPermissions.Any(permissions.Contains) || agentDepartments.Count > 0,
            // «Настройки → ИИ-агенты»: отделы, агентами которых управляет пользователь (администратор AI, HR, Telegram, руководитель отдела).
            AgentDepartments = agentDepartments,
            // Раздел HR: право «HR», интеграции или руководитель отдела, к которому привязаны подразделения Verifix.
            HrAccess = await hr.HasAccessAsync(id, permissions.ToHashSet(), ct),
            // Заявки на сотрудника (HR → «Подбор»): руководитель отдела или тот, кто ведёт подбор.
            HiringRequests = (await recruitment.ContextAsync(id, permissions.ToHashSet(), ct)).SeesRequests,
            // Охват «Продаж»: уровень (Organization | Rm | Supervisor | Agent) и своя страница — вкладки раздела и вход в него. Проверяет каждый
            // запрос всё равно сервер; без раздела «Продажи» — null.
            SalesScope = permissions.Contains(Permissions.SalesRead) ? await SalesScopeOf(salesAccess, salesScopes, id, ct) : null,
            // Отделы, задачи которых открываются: свой, те, которыми руководит, у руководства — все (в «Продажах» — и полевая команда Sales Base).
            // Без раздела «Задачи» — пусто. Вместе со своими разделами отделов решает, какие отделы видны в «Отделах» и на главной.
            Departments = permissions.Contains(Permissions.WorkTasks)
                ? await work.OpenDepartmentCodesAsync(await work.ContextAsync(id, permissions.ToHashSet(), ct), ct)
                : [],
        });
    }

    private static async Task<object> SalesScopeOf(OneBase.Application.Sales.Access.SalesAccess access, OneBase.Application.Sales.Access.SalesScopeAdmin admin, Guid id,
        CancellationToken ct)
    {
        var scope = await access.ScopeAsync(id, ct);
        return new { Level = scope.Level?.ToString() ?? "Organization", Home = await admin.HomeAsync(scope, ct) };
    }

    /// <summary>
    /// Рабочий стол снабжения и склада — главная кладовщика, лаборатории, снабжения и аналитика. Директору и администратору (у него все права)
    /// — «Обзор» компании.
    /// </summary>
    private static bool WorksInSupply(IReadOnlyCollection<string> roles, IReadOnlyCollection<string> permissions) =>
        !roles.Contains(SystemRoles.Admin) && !roles.Contains(SystemRoles.Director)
        && (roles.Contains(SystemRoles.AnalystRole) || permissions.Contains(Permissions.ProcurementWarehouse) || permissions.Contains(Permissions.QualityCheck)
            || permissions.Contains(Permissions.ProcurementManage));
}
