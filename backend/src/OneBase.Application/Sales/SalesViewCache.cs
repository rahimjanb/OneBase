using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Caching.Memory;

namespace OneBase.Application.Sales;

/// <summary>
/// Кэш готовых страниц продаж (остатки, первичка, продажи по SKU, акции, расчёты): живёт до следующей синхронизации, но не дольше 30 минут.
/// Одна и та же страница, запрошенная одновременно (пользователи, AI-сотрудники, прогрев), считается один раз — остальные ждут этот расчёт;
/// запрос после сброса кэша к расчёту, начатому до сброса, не присоединяется (версия данных — часть ключа). Расчёт, который ждали, упал или
/// был отменён — ждавший считает сам.
/// </summary>
public static class SalesViewCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    /// <summary>Идущие расчёты — свои у каждого сигнала (процесс OneBase — один; тестовые контейнеры друг другу не мешают).</summary>
    private static readonly ConditionalWeakTable<SalesCacheSignal, ConcurrentDictionary<string, Task<object?>>> Running = new();

    public static async Task<T> GetAsync<T>(IMemoryCache cache, SalesCacheSignal signal, string key, Func<Task<T>> build)
    {
        if (cache.TryGetValue(key, out T? value) && value is not null)
        {
            return value;
        }

        var running = Running.GetValue(signal, _ => new ConcurrentDictionary<string, Task<object?>>(StringComparer.Ordinal));
        var flight = $"{key}@{signal.Version}";
        var mine = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var current = running.GetOrAdd(flight, mine.Task);
        if (current != mine.Task)
        {
            try
            {
                return (T)(await current)!;
            }
            catch (Exception)
            {
                return await BuildAsync(cache, signal, key, build); // чужой расчёт не удался — считаем сами
            }
        }

        try
        {
            value = await BuildAsync(cache, signal, key, build);
            mine.TrySetResult(value);
            return value;
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException)
            {
                mine.TrySetCanceled();
            }
            else
            {
                mine.TrySetException(ex);
            }

            _ = mine.Task.Exception; // ждавших может не быть
            throw;
        }
        finally
        {
            running.TryRemove(new KeyValuePair<string, Task<object?>>(flight, mine.Task));
        }
    }

    private static async Task<T> BuildAsync<T>(IMemoryCache cache, SalesCacheSignal signal, string key, Func<Task<T>> build)
    {
        var token = signal.Token; // до расчёта: если данные обновятся во время него, результат сразу устареет
        var value = await build();
        // Сброс пришёл во время расчёта — результат не кладём: запись с истёкшим токеном вытеснила бы свежую, посчитанную после сброса.
        if (!token.HasChanged)
        {
            cache.Set(key, value, new MemoryCacheEntryOptions().SetAbsoluteExpiration(Ttl).AddExpirationToken(token));
        }

        return value;
    }
}
