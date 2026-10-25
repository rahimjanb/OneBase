using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneBase.Application.Abstractions;
using StackExchange.Redis;

namespace OneBase.Infrastructure.Caching;

/// <summary>
/// Redis, когда он подключён; иначе — IMemoryCache. Подключение к Redis — в фоне при первом обращении: пока его нет (или Redis недоступен),
/// значения живут в памяти процесса, запрос не ждёт таймаута подключения. Значения в Redis — с префиксом onebase:, без секретов.
/// </summary>
public sealed class SharedCache(IServiceProvider services, IMemoryCache memory, ILogger<SharedCache> logger) : ISharedCache
{
    private const string Prefix = "onebase:";

    private readonly Lazy<Task<IConnectionMultiplexer?>> _redis = new(() => Task.Run(() =>
    {
        try
        {
            return (IConnectionMultiplexer?)services.GetRequiredService<IConnectionMultiplexer>();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Кэш: Redis недоступен — значения хранятся в памяти процесса");
            return null;
        }
    }));

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        if (Redis() is { } db)
        {
            try
            {
                var value = await db.StringGetAsync(Prefix + key);
                return value.HasValue ? value.ToString() : null;
            }
            catch (Exception ex) when (ex is RedisException or TimeoutException)
            {
                logger.LogDebug(ex, "Кэш: Redis не ответил — читаю из памяти");
            }
        }

        return memory.TryGetValue(Prefix + key, out string? cached) ? cached : null;
    }

    public async Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken ct = default)
    {
        if (Redis() is { } db)
        {
            try
            {
                await db.StringSetAsync(Prefix + key, value, ttl);
                return;
            }
            catch (Exception ex) when (ex is RedisException or TimeoutException)
            {
                logger.LogDebug(ex, "Кэш: Redis не ответил — сохраняю в памяти");
            }
        }

        memory.Set(Prefix + key, value, ttl);
    }

    /// <summary>База Redis, если подключение готово и активно; иначе null (подключение идёт в фоне).</summary>
    private IDatabase? Redis()
    {
        var connecting = _redis.Value;
        return connecting.IsCompletedSuccessfully && connecting.Result is { IsConnected: true } redis ? redis.GetDatabase() : null;
    }
}
