using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Api.Auth;

/// <summary>
/// «Был в сети»: время последнего запроса пользователя с действующим входом. Пока вкладка OneBase открыта на экране, колокольчик
/// спрашивает сервер раз в 30 секунд — поэтому «в сети» значит «OneBase открыт», а не «когда-то входил». Пишется не чаще раза в минуту на
/// пользователя (отметка в памяти сервера) одной командой UPDATE мимо отслеживания изменений: журнал правок пользователя и «изменён» не
/// трогаются. Сбой записи не мешает запросу — отметка просто повторится в следующую минуту.
/// </summary>
public sealed class UserActivity(IServiceScopeFactory scopes, IMemoryCache cache, ILogger<UserActivity> logger)
{
    /// <summary>Как часто обновляется отметка.</summary>
    public static readonly TimeSpan Every = TimeSpan.FromMinutes(1);

    /// <summary>«В сети» — последний запрос не раньше этого: опрос раз в 30 секунд плюс минута между отметками.</summary>
    public static readonly TimeSpan Online = TimeSpan.FromMinutes(3);

    public async Task TouchAsync(Guid userId, CancellationToken ct)
    {
        var key = $"user-seen:{userId}";
        if (cache.TryGetValue(key, out _))
        {
            return;
        }

        cache.Set(key, true, Every);
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var now = DateTimeOffset.UtcNow;
            await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(s => s.SetProperty(u => u.LastSeenAt, now), ct);
        }
        catch (OperationCanceledException)
        {
            cache.Remove(key);
        }
        catch (Exception ex)
        {
            cache.Remove(key);
            logger.LogWarning(ex, "Не записано «был в сети» пользователя {UserId}", userId);
        }
    }
}
