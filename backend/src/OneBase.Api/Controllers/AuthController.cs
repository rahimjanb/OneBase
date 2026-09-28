using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Domain.Audit;
using OneBase.Domain.Identity;

namespace OneBase.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAppDbContext db,
    JwtTokenService tokens,
    IPasswordHasher<User> hasher,
    IAuditLogger audit) : ControllerBase
{
    public sealed record LoginRequest(string Email, string Password);

    public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt);

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users
            .Include(u => u.Roles).ThenInclude(ur => ur.Role).ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.Email == email, ct);

        var verification = user is { IsActive: true }
            ? hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password)
            : PasswordVerificationResult.Failed;

        if (user is null || verification == PasswordVerificationResult.Failed)
        {
            await audit.LogAsync(ActorType.System, "auth", "auth.login.failed", data: new { email }, cancellationToken: ct);
            return Unauthorized();
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, request.Password);
            await db.SaveChangesAsync(ct);
        }

        var roles = user.Roles.Select(ur => ur.Role.Name);
        var permissions = user.Roles.SelectMany(ur => ur.Role.Permissions).Select(p => p.Code);
        var (token, expiresAt) = tokens.Create(user, roles, permissions);

        await audit.LogAsync(ActorType.User, user.Id.ToString(), "auth.login", cancellationToken: ct);
        return new LoginResponse(token, expiresAt);
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me() => Ok(new
    {
        Id = User.GetUserId(),
        Email = User.FindFirst(OneBaseClaims.Email)?.Value,
        Name = User.Identity?.Name,
        Roles = User.FindAll(OneBaseClaims.Role).Select(c => c.Value),
        Permissions = User.FindAll(OneBaseClaims.Permission).Select(c => c.Value),
    });
}
