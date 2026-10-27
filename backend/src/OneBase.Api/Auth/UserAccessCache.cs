using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OneBase.Application.Security;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Api.Auth;

public sealed record UserAccess(IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);

/// <summary>
/// Актуальные роли и права пользователя из БД (кэш на минуту): права ролей с учётом разделов пользователя (UserSections).
/// Права в JWT могут устареть — новые права и смена ролей действуют без повторного входа.
/// </summary>
public sealed class UserAccessCache(IServiceScopeFactory scopes, IMemoryCache cache)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(1);

    /// <summary>null — пользователя нет или он отключён.</summary>
    public async Task<UserAccess?> GetAsync(Guid userId, CancellationToken ct)
    {
        if (cache.TryGetValue(Key(userId), out UserAccess? cached))
        {
            return cached;
        }

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var user = await db.Users.AsNoTracking()
            .Include(u => u.Roles).ThenInclude(ur => ur.Role).ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        UserAccess? access = user is { IsActive: true }
            ? new UserAccess(
                user.Roles.Select(r => r.Role.Name).Distinct().ToList(),
                UserSections.Effective(user.Roles.SelectMany(r => r.Role.Permissions).Select(p => p.Code), user.Sections).ToList())
            : null;

        cache.Set(Key(userId), access, Ttl);
        return access;
    }

    /// <summary>Роли, разделы или активность пользователя изменились — права перечитываются сразу, а не через минуту.</summary>
    public void Invalidate(Guid userId) => cache.Remove(Key(userId));

    private static string Key(Guid userId) => $"user-access:{userId}";
}
