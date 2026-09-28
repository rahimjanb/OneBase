using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace OneBase.Api.Auth;

public static class AuthSetup
{
    public static IServiceCollection AddJwtAuth(this IServiceCollection services, IConfiguration config)
    {
        var jwt = config.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
        if (jwt.SigningKey.Length < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey не задан или короче 32 символов.");
        }

        services.AddSingleton(jwt);
        services.AddSingleton<JwtTokenService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = jwt.GetSigningKey(),
                    NameClaimType = OneBaseClaims.Name,
                    RoleClaimType = OneBaseClaims.Role,
                    ClockSkew = TimeSpan.FromMinutes(1),
                };
                o.Events = new JwtBearerEvents { OnTokenValidated = RefreshAccessAsync };
            });

        services.AddSingleton<UserAccessCache>();
        services.AddAuthorization();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

        return services;
    }

    /// <summary>
    /// Роли и права берутся из БД, а не из токена: иначе новые права (или отзыв доступа)
    /// действовали бы только после повторного входа. Удалённый или отключённый пользователь получает 401.
    /// </summary>
    private static async Task RefreshAccessAsync(TokenValidatedContext context)
    {
        if (context.Principal?.Identity is not ClaimsIdentity identity
            || !Guid.TryParse(identity.FindFirst(OneBaseClaims.UserId)?.Value, out var userId))
        {
            context.Fail("В токене нет идентификатора пользователя.");
            return;
        }

        var access = await context.HttpContext.RequestServices.GetRequiredService<UserAccessCache>()
            .GetAsync(userId, context.HttpContext.RequestAborted);
        if (access is null)
        {
            context.Fail("Пользователь не найден или отключён.");
            return;
        }

        foreach (var claim in identity.FindAll(c => c.Type is OneBaseClaims.Role or OneBaseClaims.Permission).ToList())
        {
            identity.RemoveClaim(claim);
        }

        identity.AddClaims(access.Roles.Select(r => new Claim(OneBaseClaims.Role, r)));
        identity.AddClaims(access.Permissions.Select(p => new Claim(OneBaseClaims.Permission, p)));
    }
}
