using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace OneBase.Api.Auth;

/// <summary>[HasPermission(Permissions.X)] — доступ только при наличии claim "perm" с этим кодом.</summary>
public sealed class HasPermissionAttribute(string permission)
    : AuthorizeAttribute(PermissionPolicyProvider.Prefix + permission);

public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public const string Prefix = "perm:";

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return await base.GetPolicyAsync(policyName);
        }

        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireClaim(OneBaseClaims.Permission, policyName[Prefix.Length..])
            .Build();
    }
}
