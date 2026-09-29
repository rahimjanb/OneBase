using Microsoft.Extensions.Caching.Memory;

namespace OneBase.Application.Sales;

/// <summary>Кэш готовых страниц продаж (остатки, первичка): живёт до следующей синхронизации, но не дольше 30 минут.</summary>
public static class SalesViewCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    public static async Task<T> GetAsync<T>(IMemoryCache cache, SalesCacheSignal signal, string key, Func<Task<T>> build)
    {
        if (cache.TryGetValue(key, out T? value) && value is not null)
        {
            return value;
        }

        var token = signal.Token; // до расчёта: если данные обновятся во время него, результат сразу устареет
        value = await build();
        cache.Set(key, value, new MemoryCacheEntryOptions().SetAbsoluteExpiration(Ttl).AddExpirationToken(token));
        return value;
    }
}
