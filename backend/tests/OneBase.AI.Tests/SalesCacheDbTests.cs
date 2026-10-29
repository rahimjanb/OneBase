using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Sales.SkuSales;
using OneBase.Domain.Sales;
using OneBase.Infrastructure.Persistence;
using OneBase.Infrastructure.Sales;

namespace OneBase.AI.Tests;

/// <summary>
/// Кэш отчётов продаж на настоящей PostgreSQL: один месяц, запрошенный одновременно, собирается один раз; после любого сброса
/// отчёт собирается заново и видит новые данные; АКБ давних месяцев считается один раз на месяц (соседние месяцы и динамика берут
/// посчитанное) и совпадает с расчётом за весь период; прогрев после сброса оставляет в кэше текущий месяц и месяцы динамики.
/// </summary>
public class SalesCacheDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private const long Base = DataManagerDb.FakeLinkoIds + 50_000;

    /// <summary>Продажи и возврат с мая по август 2026 в двух филиалах — у давних месяцев есть АКБ по категориям.</summary>
    private static async Task SeedAsync(OneBaseDbContext db)
    {
        await db.LinkoOrderReturnLines.Where(l => l.Id >= DataManagerDb.FakeLinkoIds).ExecuteDeleteAsync();
        await db.LinkoOrderReturns.Where(r => r.Id >= DataManagerDb.FakeLinkoIds).ExecuteDeleteAsync();
        var id = Base;
        foreach (var month in new[] { 5, 6, 7, 8 })
        {
            foreach (var (branch, name, market, product, kg) in new[]
                     {
                         (5L, "Хоразм", 2001L, 100L, 100m + month), (5L, "Хоразм", 2002L, 200L, 40m), (7L, "Бухоро", 2003L, 100L, 70m * month),
                     })
            {
                var day = new DateOnly(2026, month, 12);
                db.LinkoOrders.Add(new LinkoOrder
                {
                    Id = id, CreatedAt = day.ToDateTime(new TimeOnly(8, 0)), CreatedDate = day, AcceptedAt = day.ToDateTime(new TimeOnly(9, 0)),
                    AcceptedDate = day, Status = "delivered", MarketId = market, BranchId = branch, BranchName = name, AgentId = 51 + branch,
                    TotalWeight = kg, TotalPrice = kg * 30_000,
                });
                db.LinkoOrderLines.Add(new LinkoOrderLine
                {
                    Id = id, OrderId = id, ProductId = product, Amount = kg, Price = 30_000, TotalPrice = kg * 30_000, TotalWeight = kg,
                });
                id++;
            }
        }

        // Возврат июня обнуляет точку 2002 в июне: в АКБ июня её нет, в кг — есть минус.
        db.LinkoOrderReturns.Add(new LinkoOrderReturn
        {
            Id = Base, CreatedDate = new DateOnly(2026, 6, 20), Status = "delivered", MarketId = 2002, AgentId = 56, BranchId = 5, BranchName = "Хоразм",
            TotalWeight = 40, TotalPrice = 40 * 30_000,
        });
        db.LinkoOrderReturnLines.Add(new LinkoOrderReturnLine { Id = Base, ReturnId = Base, ProductId = 200, Amount = 40, Price = 30_000, TotalWeight = 40 });
        await db.SaveChangesAsync();
    }

    private async Task<IServiceScope> PrepareAsync()
    {
        await fixture.ResetAsync();
        var scope = fixture.Services.CreateScope();
        await SeedAsync(scope.ServiceProvider.GetRequiredService<OneBaseDbContext>());
        scope.ServiceProvider.GetRequiredService<SalesCacheSignal>().InvalidateHistory();
        return scope;
    }

    private async Task<SalesAnalytics> LoadInNewScopeAsync(int year, int month)
    {
        using var scope = fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SalesDataLoader>().LoadAsync(year, month);
    }

    [PostgresFact]
    public async Task Same_month_requested_at_once_is_built_once_and_every_reset_gives_fresh_figures()
    {
        using var scope = await PrepareAsync();
        var sp = scope.ServiceProvider;
        var builds = sp.GetRequiredService<SalesBuilds>();
        var signal = sp.GetRequiredService<SalesCacheSignal>();

        var before = builds.Builds;
        var all = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(() => LoadInNewScopeAsync(2026, 9))));
        Assert.Equal(1, builds.Builds - before);
        Assert.All(all, a => Assert.Same(all[0], a));
        var fact = all[0].CachedRepublic(null, null).Kpi.FactKg;

        // Разные месяцы одновременно — каждый по разу.
        before = builds.Builds;
        var months = await Task.WhenAll(new[] { 8, 7, 8, 7 }.Select(m => Task.Run(() => LoadInNewScopeAsync(2026, m))));
        Assert.Equal(2, builds.Builds - before);
        Assert.Same(months[0], months[2]);
        Assert.Same(months[1], months[3]);

        // Из кэша — пока данные не менялись.
        Assert.Same(all[0], await LoadInNewScopeAsync(2026, 9));

        // Новый заказ сентября и сброс (как после синхронизации или правки данных): отчёт новый и видит заказ.
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var day = new DateOnly(2026, 9, 15);
        db.LinkoOrders.Add(new LinkoOrder
        {
            Id = Base + 900, CreatedAt = day.ToDateTime(new TimeOnly(8, 0)), CreatedDate = day, AcceptedAt = day.ToDateTime(new TimeOnly(9, 0)),
            AcceptedDate = day, Status = "delivered", MarketId = 2001, BranchId = 5, BranchName = "Хоразм", AgentId = 56, TotalWeight = 25, TotalPrice = 25 * 30_000,
        });
        db.LinkoOrderLines.Add(new LinkoOrderLine { Id = Base + 900, OrderId = Base + 900, ProductId = 100, Amount = 25, Price = 30_000, TotalPrice = 25 * 30_000, TotalWeight = 25 });
        await db.SaveChangesAsync();
        Assert.Same(all[0], await LoadInNewScopeAsync(2026, 9)); // без сброса — прежний отчёт (кэш живёт до сброса)

        signal.Invalidate();
        var fresh = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Task.Run(() => LoadInNewScopeAsync(2026, 9))));
        Assert.NotSame(all[0], fresh[0]);
        Assert.All(fresh, a => Assert.Same(fresh[0], a));
        Assert.Equal(fact + 25, fresh[0].CachedRepublic(null, null).Kpi.FactKg);
        Assert.NotSame(months[0], await LoadInNewScopeAsync(2026, 8)); // сброс снимает все месяцы
    }

    [PostgresFact]
    public async Task Past_months_akb_is_counted_once_per_month_and_matches_counting_the_whole_period()
    {
        using var scope = await PrepareAsync();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<IAppDbContext>();
        var options = sp.GetRequiredService<SalesOptions>();
        var history = new CountingHistory(sp.GetRequiredService<ISalesHistoryReader>());
        var signal = new SalesCacheSignal();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var loader = new SalesDataLoader(db, options, cache, signal, history, new SalesBuilds(NullLogger<SalesBuilds>.Instance));

        // Октябрь: давние месяцы — январь…август, одним запросом.
        await loader.LoadAsync(2026, 10);
        Assert.Equal(new[] { (new DateOnly(2026, 1, 1), new DateOnly(2026, 8, 31)) }, history.Calls);

        // Сентябрь, июль: январь…июль и январь…май уже посчитаны — в БД не идём.
        var september = await loader.LoadAsync(2026, 9);
        await loader.LoadAsync(2026, 7);
        Assert.Single(history.Calls);

        // Те же цифры, что у сентября, посчитанного с пустым кэшем за весь период сразу.
        using var otherCache = new MemoryCache(new MemoryCacheOptions());
        var direct = await new SalesDataLoader(db, options, otherCache, new SalesCacheSignal(), sp.GetRequiredService<ISalesHistoryReader>(),
            new SalesBuilds(NullLogger<SalesBuilds>.Instance)).LoadAsync(2026, 9);
        var akb = september.CachedRepublic(null, null).AkbMonths;
        Assert.Equal(JsonSerializer.Serialize(direct.CachedRepublic(null, null).AkbMonths), JsonSerializer.Serialize(akb));
        Assert.Contains(akb.Total, v => v is > 0);

        // Обычный сброс (свежие месяцы) — отчёт новый, давние месяцы — из кэша; полная перезагрузка — давние месяцы заново.
        signal.Invalidate();
        Assert.NotSame(september, await loader.LoadAsync(2026, 9));
        Assert.Single(history.Calls);
        signal.InvalidateHistory();
        await loader.LoadAsync(2026, 9);
        Assert.Equal((new DateOnly(2026, 1, 1), new DateOnly(2026, 7, 31)), history.Calls[^1]);
        Assert.Equal(2, history.Calls.Count);
    }

    [PostgresFact]
    public async Task Warm_up_after_a_reset_leaves_the_current_month_and_trend_months_in_the_cache()
    {
        using var scope = await PrepareAsync();
        var sp = scope.ServiceProvider;
        var builds = sp.GetRequiredService<SalesBuilds>();
        var loader = sp.GetRequiredService<SalesDataLoader>();
        var signal = sp.GetRequiredService<SalesCacheSignal>();
        var warmer = new SalesCacheWarmer(fixture.Services.GetRequiredService<IServiceScopeFactory>(), signal, NullLogger<SalesCacheWarmer>.Instance)
        {
            StartupDelay = TimeSpan.Zero,
            Debounce = TimeSpan.FromMilliseconds(50),
        };

        using var stop = new CancellationTokenSource();
        await warmer.StartAsync(stop.Token);
        try
        {
            var months = (await loader.MonthsAsync()).Take(SalesCacheWarmer.TrendMonths).ToList();
            Assert.Equal(new[] { new SalesMonth(2026, 9), new SalesMonth(2026, 8), new SalesMonth(2026, 7), new SalesMonth(2026, 6), new SalesMonth(2026, 5) }, months);

            async Task WarmedAsync()
            {
                // Прогрев готов, когда все месяцы динамики отдаются из кэша, а сборок больше нет.
                for (var i = 0; i < 300; i++)
                {
                    var built = builds.Builds;
                    foreach (var m in months)
                    {
                        await LoadInNewScopeAsync(m.Year, m.Month);
                    }

                    if (builds.Builds == built)
                    {
                        return;
                    }

                    await Task.Delay(100);
                }

                Assert.Fail("Прогрев не закончился за 30 секунд");
            }

            await WarmedAsync();
            var september = await LoadInNewScopeAsync(2026, 9);

            // Сброс: прогрев сам пересобирает месяцы, следующий запрос — из кэша и уже новый отчёт.
            signal.Invalidate();
            var built = builds.Builds;
            for (var i = 0; i < 300 && builds.Builds - built < months.Count; i++)
            {
                await Task.Delay(100);
            }

            Assert.True(builds.Builds - built >= months.Count, $"после сброса прогрев собрал {builds.Builds - built} из {months.Count} месяцев");
            built = builds.Builds;
            var again = await LoadInNewScopeAsync(2026, 9);
            Assert.Equal(built, builds.Builds);
            Assert.NotSame(september, again);
        }
        finally
        {
            await stop.CancelAsync();
            await warmer.StopAsync(CancellationToken.None);
        }
    }

    private sealed class CountingHistory(ISalesHistoryReader inner) : ISalesHistoryReader
    {
        public List<(DateOnly From, DateOnly To)> Calls { get; } = [];

        public Task<IReadOnlyList<MonthlyAkb>> AkbByMonthAsync(DateOnly from, DateOnly to, IReadOnlyDictionary<long, long> categoryGroups, CancellationToken ct)
        {
            lock (Calls)
            {
                Calls.Add((from, to));
            }

            return inner.AkbByMonthAsync(from, to, categoryGroups, ct);
        }

        public Task<SkuSalesAggregate> SkuSalesAsync(DateOnly from, DateOnly to, IReadOnlyDictionary<long, Guid> branchRegions, CancellationToken ct) =>
            inner.SkuSalesAsync(from, to, branchRegions, ct);
    }
}
