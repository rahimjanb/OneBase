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
        var permissions = user.Roles.SelectMany(ur => ur.Role.Permissions).Select(p => p.Code);
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
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var id = User.GetUserId();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
        {
            return Unauthorized();
        }

        var permissions = User.FindAll(OneBaseClaims.Permission).Select(c => c.Value).ToList();
        return Ok(new
        {
            user.Id,
            user.Login,
            user.Email,
            Name = user.FullName,
            user.FirstName,
            user.LastName,
            user.Position,
            Roles = User.FindAll(OneBaseClaims.Role).Select(c => c.Value).OrderBy(SystemRoles.OrderOf),
            Permissions = permissions,
            CanOpenSettings = SystemRoles.SettingsPermissions.Any(permissions.Contains),
        });
    }
}
