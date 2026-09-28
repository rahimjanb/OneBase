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
            });

        services.AddAuthorization();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

        return services;
    }
}
