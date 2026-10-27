using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Security;

namespace OneBase.AI.Security;

/// <summary>
/// Права пользователя, от имени которого работает AI. AI видит только то, что видит сам пользователь:
/// инструмент или AI-сотрудник с правом, которого у пользователя нет, данных не получает. Права — роли с учётом отмеченных пользователю
/// разделов (UserSections): раздел снят — AI-сотрудник и инструменты этого раздела ему не отвечают.
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

        IReadOnlySet<string> set = await db.EffectivePermissionsAsync(userId, ct) ?? new HashSet<string>(StringComparer.Ordinal);
        _cache[userId] = set;
        return set;
    }
}
