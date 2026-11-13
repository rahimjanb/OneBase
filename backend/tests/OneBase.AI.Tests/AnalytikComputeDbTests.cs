using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Analytik;
using OneBase.Application.Analytik;
using OneBase.Application.Analytik.Compute;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Semantic;
using OneBase.Application.Security;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>
/// Вычислительный движок на PostgreSQL: данные — строки Linko тестового мира (IntelligenceWorld), ответы сверяются с эталоном, посчитанным по
/// тем же строкам в памяти, без SQL. Плюс журнал расчётов (вход, алгоритм, версия), повтор расчёта по записи и права.
/// </summary>
public sealed class AnalytikComputeDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly DateOnly Sep1 = new(2026, 9, 1), Sep30 = new(2026, 9, 30), Oct1 = new(2026, 10, 1), Oct30 = new(2026, 10, 30);

    private static AnalytikPrincipal Principal(params string[] permissions) =>
        new(Guid.NewGuid(), permissions.Length == 0 ? new HashSet<string> { Permissions.SalesRead, Permissions.AnalytikUse } : permissions.ToHashSet(),
            SalesRowScope.Organization);

    private async Task<ComputeOutcome> RunAsync(string method, ComputeData data, object? parameters = null, AnalytikPrincipal? p = null)
    {
        using var scope = fixture.Services.CreateScope();
        p ??= Principal();
        var request = new ComputeRequest
        {
            Method = method, Data = data,
            Params = parameters is null ? null : JsonSerializer.SerializeToElement(parameters).EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone()),
        };
        return await scope.ServiceProvider.GetRequiredService<ComputeService>().RunAsync(request, new ComputeContext(p.UserId, "api"), default, p);
    }

    private static JsonElement Result(ComputeOutcome o) => JsonSerializer.SerializeToElement(o.Result, ComputeService.JsonOptions);

    private static AnalyticsFilter Only(IntelligenceWorld w, params int[] regions) => new("region", "in", regions.Select(r => w.Regions[r]).ToList());

    [PostgresFact]
    public async Task Drivers_find_the_region_that_pulled_revenue_down_and_match_the_reference()
    {
        var w = await IntelligenceWorld.SeedAsync(fixture);
        var o = await RunAsync("drivers", new ComputeData
        {
            Metrics = ["revenue"], Dimension = "region", From = Oct1, To = Oct30, CompareFrom = Sep1, CompareTo = Sep30, Filters = [Only(w, 0, 1, 2)],
        });
        Assert.Equal("ok", o.Status);
        var r = Result(o);
        var items = r.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(w.Treated, items[0].GetProperty("key").GetString());
        for (var i = 0; i < 3; i++)
        {
            var item = items.Single(x => x.GetProperty("key").GetString() == w.Regions[i]);
            Assert.Equal((double)w.Revenue(i, Sep1, Sep30), item.GetProperty("a").GetDouble(), 0);
            Assert.Equal((double)w.Revenue(i, Oct1, Oct30), item.GetProperty("b").GetDouble(), 0);
        }

        Assert.True(items[0].GetProperty("delta").GetDouble() < 0);
        Assert.Equal(EvidenceClasses.Observed, o.EvidenceClass);
        Assert.Contains("Вниз тянут", o.Summary);
    }

    [PostgresFact]
    public async Task Price_volume_mix_isolates_the_october_price_increase()
    {
        var w = await IntelligenceWorld.SeedAsync(fixture);
        var o = await RunAsync("pvm", new ComputeData { From = Oct1, To = Oct30, CompareFrom = Sep1, CompareTo = Sep30, Filters = [Only(w, 0, 1, 2)] });
        var pvm = Result(o).GetProperty("pvm");
        var kgB = (double)w.Between(Oct1, Oct30).Where(l => l.Product == 1).Sum(l => l.Kg);
        Assert.Equal(kgB * 3_000, pvm.GetProperty("price").GetDouble(), 0);
        var parts = new[] { "volume", "mix", "price", "newItems", "lostItems" }.Sum(k => pvm.GetProperty(k).GetDouble());
        Assert.Equal(pvm.GetProperty("delta").GetDouble(), parts, 0);
        var expectedDelta = (double)(w.Between(Oct1, Oct30).Sum(l => l.Revenue) - w.Between(Sep1, Sep30).Sum(l => l.Revenue));
        Assert.Equal(expectedDelta, pvm.GetProperty("delta").GetDouble(), 0);
    }

    [PostgresFact]
    public async Task Difference_in_differences_confirms_the_drop_against_control_regions()
    {
        var w = await IntelligenceWorld.SeedAsync(fixture);
        var o = await RunAsync("did", new ComputeData { Metrics = ["kg"], Dimension = "region", From = Sep1, To = Oct30, Filters = [Only(w, 0, 1, 2)] },
            new { change_date = "2026-10-01", treatment = new[] { w.Treated } });
        Assert.Equal("ok", o.Status);
        var r = Result(o).GetProperty("result");
        Assert.Equal(w.DidKg(Sep1, Oct30), r.GetProperty("effect").GetDouble(), 1);
        Assert.Equal(EvidenceClasses.TestedCausal, o.EvidenceClass);

        var ba = await RunAsync("before_after", new ComputeData { Metrics = ["kg"], From = Sep1, To = Oct30, Filters = [Only(w, 0)] },
            new { change_date = "2026-10-01" });
        var test = Result(ba).GetProperty("result").GetProperty("test");
        var daily = w.DailyKg([0], Sep1, Oct30);
        Assert.Equal(daily.Skip(30).Average() - daily.Take(30).Average(), test.GetProperty("diff").GetDouble(), 1);
        Assert.Equal(EvidenceClasses.Statistical, ba.EvidenceClass);
    }

    [PostgresFact]
    public async Task Churn_finds_the_market_that_stopped_buying_and_revenue_at_risk()
    {
        var w = await IntelligenceWorld.SeedAsync(fixture);
        var o = await RunAsync("churn", new ComputeData { From = Oct1, To = Oct30, CompareFrom = Sep1, CompareTo = Sep30, Filters = [Only(w, 0, 1, 2)] },
            new { by = "region" });
        var r = Result(o);
        Assert.Equal(15, r.GetProperty("baseCustomers").GetInt32());
        Assert.Equal(1, r.GetProperty("lost").GetInt32());
        var lostMarket = IntelligenceWorld.Market(0, 5);
        var atRisk = (double)w.Between(Sep1, Sep30).Where(l => l.Market == lostMarket).Sum(l => l.Revenue);
        Assert.Equal(atRisk, r.GetProperty("revenueAtRisk").GetDouble(), 0);
        Assert.Equal(w.Treated, r.GetProperty("groups")[0].GetProperty("group").GetString());
    }

    [PostgresFact]
    public async Task Abc_xyz_by_market_matches_the_reference_classification()
    {
        var w = await IntelligenceWorld.SeedAsync(fixture);
        var from = new DateOnly(2026, 7, 1);
        var o = await RunAsync("abc_xyz", new ComputeData { Metrics = ["revenue"], Dimension = "market", Grain = "month", From = from, To = Oct30, Filters = [Only(w, 0, 1, 2)] });
        Assert.Equal("ok", o.Status);
        var items = Result(o).GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(15, items.Count);
        // Эталон: те же суммы по месяцам июль–сентябрь (октябрь неполный — 30 дней из 31, движок его не берёт).
        var months = new[] { 7, 8, 9 };
        var reference = SegmentMath.Abc(w.Lines.Where(l => months.Contains(l.Date.Month)).GroupBy(l => l.Market)
            .Select(g => (g.Key.ToString(), (double)g.Sum(l => l.Revenue))).ToList());
        Assert.Equal(reference.Count(x => x.Class == "A"), items.Count(i => i.GetProperty("abc").GetString() == "A"));
        // Точка 5 «Хорезма» в октябре не покупала — но октябрь не вошёл, поэтому все точки стабильны (X).
        Assert.All(items, i => Assert.Equal("X", i.GetProperty("xyz").GetString()));
    }

    [PostgresFact]
    public async Task Markets_with_the_same_name_stay_separate_participants()
    {
        var w = await IntelligenceWorld.SeedAsync(fixture);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBase.Infrastructure.Persistence.OneBaseDbContext>();
            foreach (var id in new[] { IntelligenceWorld.Market(1, 1), IntelligenceWorld.Market(2, 1) })
            {
                db.LinkoMarkets.RemoveRange(db.LinkoMarkets.Where(m => m.Id == id));
                db.LinkoMarkets.Add(new OneBase.Domain.Sales.LinkoMarket { Id = id, Name = "Азамат" });
            }

            await db.SaveChangesAsync();
        }

        var o = await RunAsync("abc_xyz", new ComputeData
        {
            Metrics = ["revenue"], Dimension = "market", Grain = "month", From = new DateOnly(2026, 7, 1), To = Oct30, Filters = [Only(w, 0, 1, 2)],
        });
        Assert.Equal("ok", o.Status);
        var keys = Result(o).GetProperty("items").EnumerateArray().Select(i => i.GetProperty("key").GetString()).ToList();
        Assert.Equal(15, keys.Distinct().Count());
        Assert.Contains($"Азамат ({IntelligenceWorld.Market(1, 1)})", keys);
    }

    [PostgresFact]
    public async Task Every_computation_is_journaled_and_replays_to_the_same_result()
    {
        var w = await IntelligenceWorld.SeedAsync(fixture);
        var p = Principal();
        var o = await RunAsync("trend", new ComputeData { Metrics = ["kg"], Grain = "week", From = new DateOnly(2026, 7, 6), To = new DateOnly(2026, 9, 27), Filters = [Only(w, 1)] },
            null, p);
        Assert.Equal("ok", o.Status);
        Assert.NotEmpty(o.QueryIds);

        using var scope = fixture.Services.CreateScope();
        var adb = scope.ServiceProvider.GetRequiredService<IAnalytikDb>();
        var row = await adb.Computations.AsNoTracking().SingleAsync(c => c.Id == o.Id);
        Assert.Equal("trend", row.Method);
        Assert.Equal(ComputeCatalog.Version, row.Version);
        Assert.Contains("МНК", row.Algorithm);
        Assert.Contains(o.QueryIds[0].ToString(), row.Input);
        Assert.NotNull(row.Result);
        Assert.Equal(new DateOnly(2026, 7, 6), row.PeriodFrom);

        var (again, same, changed) = await scope.ServiceProvider.GetRequiredService<ComputeService>().ReplayAsync(o.Id, new ComputeContext(p.UserId, "api"), default, p);
        Assert.True(same);
        Assert.False(changed);
        Assert.NotEqual(o.Id, again.Id);
    }

    [PostgresFact]
    public async Task Without_sales_rights_the_engine_refuses_instead_of_computing()
    {
        var w = await IntelligenceWorld.SeedAsync(fixture);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => RunAsync("describe",
            new ComputeData { Metrics = ["revenue"], Dimension = "region", From = Sep1, To = Sep30, Filters = [Only(w, 0)] }, null, Principal(Permissions.AnalytikUse)));
    }

    [PostgresFact]
    public async Task Missing_inputs_give_an_explained_insufficient_result_not_numbers()
    {
        var w = await IntelligenceWorld.SeedAsync(fixture);
        var o = await RunAsync("before_after", new ComputeData { Metrics = ["kg"], From = Sep1, To = Oct30, Filters = [Only(w, 0)] });
        Assert.Equal("insufficient", o.Status);
        Assert.Contains("change_date", o.Error);
        Assert.Null(o.Result);

        var tool = ComputeTool.Present(o, ComputeTool.ToolName);
        Assert.True(tool.Success);
        Assert.Contains("не подставляй числа", tool.Content);
    }
}
