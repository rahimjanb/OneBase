using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OneBase.Domain.Identity;

namespace OneBase.Api.Auth;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = "onebase";
    public string Audience { get; set; } = "onebase";
    public string SigningKey { get; set; } = string.Empty;
    /// <summary>
    /// Срок сессии — рабочий день. Права и активность пользователя всё равно проверяются по БД на каждом запросе
    /// (UserAccessCache), поэтому отключённый пользователь теряет доступ сразу, а не через 12 часов.
    /// </summary>
    public int AccessTokenMinutes { get; set; } = 720;

    public SymmetricSecurityKey GetSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}

public static class OneBaseClaims
{
    public const string UserId = "sub";
    public const string Email = "email";
    public const string Name = "name";
    public const string Role = "role";
    public const string Permission = "perm";
    public const string Department = "dept";

    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(UserId) ?? throw new InvalidOperationException("В токене нет sub."));
}

public sealed class JwtTokenService(JwtOptions options)
{
    private readonly JsonWebTokenHandler _handler = new();

    public (string Token, DateTimeOffset ExpiresAt) Create(User user, IEnumerable<string> roles, IEnumerable<string> permissions)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(OneBaseClaims.UserId, user.Id.ToString()),
            new(OneBaseClaims.Email, user.Email),
            new(OneBaseClaims.Name, user.FullName),
        };
        if (user.DepartmentId is { } departmentId)
        {
            claims.Add(new Claim(OneBaseClaims.Department, departmentId.ToString()));
        }
        claims.AddRange(roles.Select(r => new Claim(OneBaseClaims.Role, r)));
        claims.AddRange(permissions.Distinct().Select(p => new Claim(OneBaseClaims.Permission, p)));

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = options.Issuer,
            Audience = options.Audience,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(options.GetSigningKey(), SecurityAlgorithms.HmacSha256),
        });

        return (token, expiresAt);
    }
}
