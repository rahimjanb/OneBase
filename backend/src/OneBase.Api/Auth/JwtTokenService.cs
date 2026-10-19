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
    /// Срок токена входа: сеанс длится, пока пользователь сам не выйдет, поэтому срок долгий (10 лет). Отзыв — на сервере:
    /// токен несёт сеанс (UserSession), его проверяют на каждом запросе вместе с правами и активностью пользователя (UserAccessCache).
    /// </summary>
    public int SessionDays { get; set; } = 3650;

    public SymmetricSecurityKey GetSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}

public static class OneBaseClaims
{
    public const string UserId = "sub";
    public const string Email = "email";
    public const string Login = "login";
    public const string Name = "name";
    public const string Role = "role";
    public const string Permission = "perm";
    public const string Department = "dept";
    /// <summary>Сеанс входа (UserSession.Id).</summary>
    public const string Session = "sid";

    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(UserId) ?? throw new InvalidOperationException("В токене нет sub."));
}

public sealed class JwtTokenService(JwtOptions options)
{
    private readonly JsonWebTokenHandler _handler = new();

    public (string Token, DateTimeOffset ExpiresAt) Create(User user, Guid sessionId, IEnumerable<string> roles, IEnumerable<string> permissions)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddDays(options.SessionDays);

        var claims = new List<Claim>
        {
            new(OneBaseClaims.UserId, user.Id.ToString()),
            new(OneBaseClaims.Session, sessionId.ToString()),
            new(OneBaseClaims.Login, user.Login),
            new(OneBaseClaims.Name, user.FullName),
        };
        if (user.Email is { } email)
        {
            claims.Add(new Claim(OneBaseClaims.Email, email));
        }

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
