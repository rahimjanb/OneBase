using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;

namespace OneBase.AI.Security;

/// <summary>
/// Права пользователя, от имени которого работает AI. AI видит только то, что видит сам пользователь:
/// инструмент или AI-сотрудник с правом, которого у пользователя нет, данных не получает.
/// </summary>
public interface IUserPermissions
{
    /// <summary>Права активного пользователя; отключённый или несуществующий — пустой набор.</summary>
    Task<IReadOnlySet<string>> GetAsync(Guid userId, CancellationToken ct = default);
}

internal sealed class UserPermissions(IAppDbContext db) : IUserPermissions
{
    private readonly Dictionary<Guid, IReadOnlySet<string>> _cache = [];

    public async Task<IReadOnlySet<string>> GetAsync(Guid userId, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(userId, out var cached))
        {
            return cached;
        }

        var codes = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId && u.IsActive)
            .SelectMany(u => u.Roles.SelectMany(r => r.Role.Permissions.Select(p => p.Code)))
            .Distinct()
            .ToListAsync(ct);

        var set = codes.ToHashSet(StringComparer.Ordinal);
        _cache[userId] = set;
        return set;
    }
}
