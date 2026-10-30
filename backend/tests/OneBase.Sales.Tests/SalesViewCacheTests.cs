using Microsoft.Extensions.Caching.Memory;
using OneBase.Application.Sales;

namespace OneBase.Sales.Tests;

/// <summary>
/// Кэш страниц продаж: одна страница, запрошенная одновременно, считается один раз; запрос после сброса к расчёту, начатому до сброса, не
/// присоединяется, а результат того расчёта в кэш не попадает; расчёт, который ждали, упал — ждавший считает сам.
/// </summary>
public class SalesViewCacheTests
{
    [Fact]
    public async Task Same_page_requested_at_once_is_built_once()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var signal = new SalesCacheSignal();
        var builds = 0;
        var gate = new TaskCompletionSource();

        var requests = Enumerable.Range(0, 8).Select(_ => SalesViewCache.GetAsync(cache, signal, "page", async () =>
        {
            Interlocked.Increment(ref builds);
            await gate.Task;
            return new object();
        })).ToList();
        gate.SetResult();
        var results = await Task.WhenAll(requests);

        Assert.Equal(1, builds);
        Assert.All(results, r => Assert.Same(results[0], r));
        Assert.Same(results[0], await SalesViewCache.GetAsync(cache, signal, "page", () => Task.FromResult(new object())));
    }

    [Fact]
    public async Task Request_after_a_reset_does_not_join_the_old_build_and_the_old_result_is_not_cached()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var signal = new SalesCacheSignal();
        var oldGate = new TaskCompletionSource();
        var old = SalesViewCache.GetAsync(cache, signal, "page", async () =>
        {
            await oldGate.Task;
            return "до сброса";
        });

        signal.Invalidate(); // данные изменились, пока страница считалась
        var fresh = await SalesViewCache.GetAsync(cache, signal, "page", () => Task.FromResult("после сброса"));
        oldGate.SetResult();

        Assert.Equal("после сброса", fresh);
        Assert.Equal("до сброса", await old); // начатый до сброса запрос получает своё
        Assert.Equal("после сброса", await SalesViewCache.GetAsync(cache, signal, "page", () => Task.FromResult("лишний расчёт")));
    }

    [Fact]
    public async Task When_the_awaited_build_fails_the_waiter_builds_itself()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var signal = new SalesCacheSignal();
        var gate = new TaskCompletionSource();
        var failing = SalesViewCache.GetAsync<string>(cache, signal, "page", async () =>
        {
            await gate.Task;
            throw new InvalidOperationException("сбой БД");
        });
        var waiter = SalesViewCache.GetAsync(cache, signal, "page", () => Task.FromResult("свой расчёт"));
        gate.SetResult();

        await Assert.ThrowsAsync<InvalidOperationException>(() => failing);
        Assert.Equal("свой расчёт", await waiter);
    }
}
