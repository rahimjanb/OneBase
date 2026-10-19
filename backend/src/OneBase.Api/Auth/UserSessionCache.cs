using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OneBase.Application.Abstractions;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Api.Auth;

/// <summary>
/// Действует ли сеанс входа (кэш на минуту). Выход закрывает свой сеанс сразу (<see cref="Invalidate"/>),
/// смена пароля администратором закрывает все сеансы пользователя — не позже чем через минуту.
/// </summary>
public sealed class UserSessionCache(IServiceScopeFactory scopes, IMemoryCache cache)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(1);

    public async Task<bool> IsActiveAsync(Guid sessionId, Guid userId, CancellationToken ct)
    {
        if (cache.TryGetValue(Key(sessionId), out bool cached))
        {
            return cached;
        }

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var active = await db.UserSessions.AsNoTracking().AnyAsync(s => s.Id == sessionId && s.UserId == userId && s.RevokedAt == null, ct);
        cache.Set(Key(sessionId), active, Ttl);
        return active;
    }

    public void Invalidate(Guid sessionId) => cache.Remove(Key(sessionId));

    private static string Key(Guid sessionId) => $"user-session:{sessionId}";
}

public static class UserSessions
{
    /// <summary>
    /// Закрывает сеансы пользователя (смена пароля администратором): старые входы на всех устройствах перестают действовать.
    /// keep — сеанс, который не трогаем (пользователь меняет пароль сам себе и остаётся в системе).
    /// </summary>
    public static Task<int> RevokeAllAsync(this IAppDbContext db, Guid userId, Guid? keep, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        return db.UserSessions
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.Id != keep)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), ct);
    }
}
