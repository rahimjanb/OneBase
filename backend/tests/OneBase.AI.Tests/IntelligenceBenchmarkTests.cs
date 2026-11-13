using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Analytik.Intelligence;
using OneBase.Application.Analytik.Compute;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Semantic;
using OneBase.Application.Security;
using Xunit;
using Xunit.Abstractions;

namespace OneBase.AI.Tests;

/// <summary>
/// Complex Question Benchmark — регрессия на PostgreSQL: все 104 сценария 14 категорий выполняются эталонными операциями на тестовом мире
/// (IntelligenceWorld); статус каждой — как ожидается, а числа сценариев с данными сверяются с независимым расчётом по строкам мира (LINQ, без
/// SQL движка). Падение любого сценария после изменения системы — регрессия.
/// </summary>
public sealed class IntelligenceBenchmarkTests(DataManagerDb fixture, ITestOutputHelper output) : IClassFixture<DataManagerDb>
{
    private static readonly DateOnly Sep1 = new(2026, 9, 1), Sep30 = new(2026, 9, 30), Oct1 = new(2026, 10, 1), Oct30 = new(2026, 10, 30);

    private static BenchmarkContext Context(IntelligenceWorld w) =>
        new(w.Regions, [IntelligenceWorld.GroupA, IntelligenceWorld.GroupB], Oct1, Oct30, Sep1, Sep30, IntelligenceWorld.Event, new DateOnly(2026, 10, 15),
            new DateOnly(2026, 7, 1));

    private static AnalytikPrincipal Principal() =>
        new(Guid.NewGuid(), new HashSet<string> { Permissions.SalesRead, Permissions.FinanceRead, Permissions.AnalytikUse }, SalesRowScope.Organization);

    [Fact]
    public void Catalog_has_over_a_hundred_cases_in_all_fourteen_categories()
    {
        var cases = BenchmarkCatalog.Cases;
        Assert.True(cases.Count >= 100, $"сценариев {cases.Count}");
        Assert.Equal(cases.Count, cases.Select(c => c.Id).Distinct().Count());
        Assert.Equal(14, BenchmarkCatalog.Categories.Count);
        Assert.All(BenchmarkCatalog.Categories.Keys, cat => Assert.True(cases.Count(c => c.Category == cat) >= 6, cat));
        Assert.All(cases, c => Assert.NotNull(c.Golden));
    }

    [PostgresFact]
    public async Task Every_case_of_the_benchmark_gives_the_expected_result_and_matches_the_reference()
    {
        var w = await IntelligenceWorld.SeedAsync(fixture);
        var ctx = Context(w);
        var p = Principal();
        var runner = fixture.Services.GetRequiredService<BenchmarkRunner>();
        var failures = new List<string>();
        var outcomes = new Dictionary<string, GoldenOutcome>();
        foreach (var c in BenchmarkCatalog.Cases)
        {
            var g = await runner.GoldenAsync(c, ctx, p, default);
            outcomes[c.Id] = g;
            if (g.Status != c.Check.Status)
            {
                failures.Add($"{c.Id}: ожидалось {c.Check.Status}, получено {g.Status} — {g.Detail}");
            }
        }

        foreach (var (id, check) in Reference(w))
        {
            try
            {
                check(outcomes[id]);
            }
            catch (Exception ex)
            {
                failures.Add($"{id}: эталон не совпал — {ex.Message.Split('\n')[0]}");
            }
        }

        foreach (var f in failures)
        {
            output.WriteLine(f);
        }

        Assert.True(failures.Count == 0, $"не прошли {failures.Count}:\n{string.Join("\n", failures)}");
    }

    // ---------- эталон: независимый расчёт по строкам мира ----------

    private static AnalyticsResult Slice(GoldenOutcome g) => Assert.IsType<AnalyticsResult>(g.Value);

    private static JsonElement Result(GoldenOutcome g) =>
        JsonSerializer.SerializeToElement(Assert.IsType<ComputeOutcome>(g.Value).Result, ComputeService.JsonOptions);

    private static double Total(AnalyticsResult r, string metric) => (double)r.Totals[metric]!.Value;

    private static int Col(AnalyticsResult r, string code) => r.Columns.ToList().FindIndex(c => c.Code == code);

    private static double Num(object? v) => Convert.ToDouble(v, CultureInfo.InvariantCulture);

    private static void Near(double expected, double actual, double tolerance = 0.5) =>
        Assert.True(Math.Abs(expected - actual) <= tolerance, $"ожидалось {expected}, получено {actual}");

    private static Dictionary<string, Action<GoldenOutcome>> Reference(IntelligenceWorld w)
    {
        var oct = w.Between(Oct1, Oct30).ToList();
        var sep = w.Between(Sep1, Sep30).ToList();
        double Rev(IEnumerable<WorldLine> lines) => (double)lines.Sum(l => l.Revenue);
        double Kg(IEnumerable<WorldLine> lines) => (double)lines.Sum(l => l.Kg);
        return new Dictionary<string, Action<GoldenOutcome>>
        {
            ["simple.revenue_month"] = g => Near(Rev(oct), Total(Slice(g), "revenue")),
            ["simple.kg_month"] = g => Near(Kg(oct), Total(Slice(g), "kg"), 0.01),
            ["simple.orders_month"] = g => Near(oct.Count, Total(Slice(g), "orders"), 0),
            ["simple.outlets_month"] = g => Near(14, Total(Slice(g), "outlets"), 0), // 15 точек, точка 5 «Хорезма» в октябре не покупала
            ["simple.region_revenue"] = g => Near(Rev(oct.Where(l => l.Region == 1)), Total(Slice(g), "revenue")),
            ["simple.region_kg"] = g => Near(Kg(oct.Where(l => l.Region == 2)), Total(Slice(g), "kg"), 0.01),
            ["simple.avg_price"] = g => Near(Rev(oct) / Kg(oct), Total(Slice(g), "avg_price_kg"), 1),
            ["simple.avg_check"] = g => Near(Rev(oct) / oct.Count, Total(Slice(g), "avg_check"), 1),
            ["simple.prev_month"] = g => Near(Rev(sep), Total(Slice(g), "revenue")),
            ["multi.region_group"] = g =>
            {
                var r = Slice(g);
                Assert.Equal(6, r.TotalRows);
                var row = r.Rows.Single(x => (string)x[0]! == w.Regions[0] && (string)x[1]! == IntelligenceWorld.GroupB);
                Near(Rev(oct.Where(l => l.Region == 0 && l.Product == 1)), Num(row[Col(r, "revenue")]));
            },
            ["multi.day_group"] = g => Assert.Equal(60, Slice(g).TotalRows),
            ["multi.day_region_group"] = g => Assert.Equal(180, Slice(g).TotalRows),
            ["multi.agent_group"] = g => Assert.Equal(6, Slice(g).TotalRows),
            ["multi.week_region"] = g => Assert.Equal(15, Slice(g).TotalRows),
            ["multi.top_markets"] = g =>
            {
                var r = Slice(g);
                Assert.Equal(5, r.Rows.Count);
                var best = oct.GroupBy(l => l.Market).Max(x => (double)x.Sum(l => l.Revenue));
                Near(best, Num(r.Rows[0][Col(r, "revenue")]));
            },
            ["multi.region_compare"] = g =>
            {
                var r = Slice(g);
                var row = r.Rows.Single(x => (string)x[0]! == w.Regions[0]);
                Near(Rev(sep.Where(l => l.Region == 0)), Num(row[Col(r, "revenue__prev")]));
            },
            ["multi.group_share"] = g =>
            {
                var r = Slice(g);
                Near(100, r.Rows.Sum(x => Num(x[Col(r, "revenue_share")])), 0.1);
            },
            ["multi.month_region_group"] = g => Assert.Equal(24, Slice(g).TotalRows),
            ["multi.product_region"] = g => Assert.Equal(2, Slice(g).TotalRows),
            ["fin.pvm"] = g =>
            {
                var pvm = Result(g).GetProperty("pvm");
                Near(Kg(oct.Where(l => l.Product == 1)) * 3_000, pvm.GetProperty("price").GetDouble(), 1);
                Near(Rev(oct) - Rev(sep), pvm.GetProperty("delta").GetDouble(), 1);
            },
            ["fin.drivers_region"] = g => Assert.Equal(w.Regions[0], Result(g).GetProperty("items")[0].GetProperty("key").GetString()),
            ["fin.drivers_group"] = g =>
            {
                var b = Result(g).GetProperty("items").EnumerateArray().Single(i => i.GetProperty("key").GetString() == IntelligenceWorld.GroupB);
                Near(Rev(oct.Where(l => l.Product == 1)) - Rev(sep.Where(l => l.Product == 1)), b.GetProperty("delta").GetDouble(), 1);
            },
            ["fin.unit"] = g =>
            {
                var r0 = Result(g).GetProperty("items").EnumerateArray().Single(i => i.GetProperty("key").GetString() == w.Regions[0]);
                Near(Rev(oct.Where(l => l.Region == 0)) / 4, r0.GetProperty("revenuePerOutlet").GetDouble(), 1);
            },
            ["fin.price_kg_region"] = g =>
            {
                var r = Slice(g);
                var row = r.Rows.Single(x => (string)x[0]! == w.Regions[1]);
                var region = oct.Where(l => l.Region == 1).ToList();
                Near(Rev(region) / Kg(region), Num(row[Col(r, "avg_price_kg")]), 1);
            },
            ["cause.region"] = g => Assert.Equal(w.Regions[0], Result(g).GetProperty("items")[0].GetProperty("key").GetString()),
            ["cause.did"] = g => Near(w.DidKg(Sep1, Oct30), Result(g).GetProperty("result").GetProperty("effect").GetDouble(), 0.1),
            ["cause.before_after"] = g =>
            {
                var daily = w.DailyKg([0], Sep1, Oct30);
                Near(daily.Skip(30).Average() - daily.Take(30).Average(), Result(g).GetProperty("result").GetProperty("test").GetProperty("diff").GetDouble(), 0.1);
            },
            ["cause.churn"] = g =>
            {
                var r = Result(g);
                Assert.Equal(1, r.GetProperty("lost").GetInt32());
                Near(Rev(sep.Where(l => l.Market == IntelligenceWorld.Market(0, 5))), r.GetProperty("revenueAtRisk").GetDouble(), 1);
            },
            ["cause.market"] = g =>
            {
                var items = Result(g).GetProperty("items").EnumerateArray().ToList();
                var lostMarketKg = Kg(sep.Where(l => l.Market == IntelligenceWorld.Market(0, 5)));
                Assert.Contains(items, i => Math.Abs(i.GetProperty("delta").GetDouble() + lostMarketKg) < 0.01);
            },
            ["long.weekday"] = g =>
            {
                var idx = Result(g).GetProperty("indices").EnumerateArray().ToList();
                Assert.Equal(7, idx.MinBy(i => i.GetProperty("index").GetDouble()).GetProperty("position").GetInt32()); // воскресенье — самый слабый
            },
            ["long.months"] = g =>
            {
                var r = Slice(g);
                Assert.Equal(4, r.Rows.Count);
                Near(Rev(w.Between(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31))), Num(r.Rows.Single(x => (string)x[0]! == "2026-08-01")[1]));
            },
            ["opt.abc"] = g => Assert.Equal(2, Result(g).GetProperty("items").GetArrayLength()),
            ["multi_step.waves"] = g => Assert.Equal(3, ((IReadOnlyList<IReadOnlyList<InvestigationStep>>)g.Value!).Count),
            ["whatif.goal"] = g => Assert.Contains("= 12", g.Detail),
        };
    }
}

/// <summary>Живой прогон набора — механика без платных вызовов: модель по назначению, подтверждение и права, бюджет, оценка ответа.</summary>
public sealed class IntelligenceBenchmarkLiveTests(IntelligenceDb fixture) : IClassFixture<IntelligenceDb>
{
    private AnalytikPrincipal Manager() =>
        new(fixture.UserId, new HashSet<string> { Permissions.SalesRead, Permissions.FinanceRead, Permissions.AnalytikUse, Permissions.AnalytikManage },
            SalesRowScope.Organization);

    [PostgresFact]
    public async Task Live_run_requires_confirmation_and_scores_the_answer_against_the_golden_slice()
    {
        await fixture.ResetAsync();
        var w = await IntelligenceWorld.SeedAsync(fixture.Services, fixture.ResetAsync);
        using var scope = fixture.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<BenchmarkRunner>();
        await Assert.ThrowsAsync<ArgumentException>(() => runner.LiveAsync(Manager(), ["simple.revenue_month"], 1, false, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => runner.LiveAsync(Manager() with { Permissions = new HashSet<string> { Permissions.AnalytikUse } },
            ["simple.revenue_month"], 1, true, default));

        // Ответ называет итог октября: эталон — тот же срез (три крупнейших региона прошлого месяца — все три региона мира).
        var total = w.Between(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)).Sum(l => l.Revenue);
        fixture.Model
            .Text("consultant.route", """{"agents":["analyst.sales"],"tasks":{"analyst.sales":"Выручка за октябрь"},"reason":"продажи"}""")
            .On("agent", messages => messages.Any(m => m.Role == OneBase.AI.Llm.LlmRole.Tool)
                ? PurposeModel.Result("""{"agent":"analyst.sales","status":"completed","summary":"Выручка октября посчитана.","findings":[],"metrics":[],"problems":[],"recommendations":[],"data_sources":[]}""")
                : PurposeModel.Result(null, new OneBase.AI.Llm.LlmToolCall("q1", OneBase.AI.Analytik.AnalyticsQueryTool.ToolName, JsonSerializer.SerializeToElement(new
                {
                    metrics = new[] { "revenue" }, date_from = "2026-10-01", date_to = "2026-10-31",
                    filters = new[] { new { dimension = "region", op = "in", values = w.Regions } },
                }))))
            .Text("analytik.answer", $"Выручка за октябрь — {total.ToString("N0", CultureInfo.GetCultureInfo("ru-RU"))} сум.");

        var run = await runner.LiveAsync(Manager(), ["simple.revenue_month", "simple.kg_month"], 0.5m, true, default);
        Assert.Equal("live", run.Mode);
        Assert.Equal(2, run.Total);
        using var results = JsonDocument.Parse(run.Results!);
        var first = results.RootElement.EnumerateArray().First(r => r.GetProperty("id").GetString() == "simple.revenue_month");
        Assert.True(first.GetProperty("passed").GetBoolean(), first.ToString());
        Assert.Equal(0, fixture.Model.Count("analytik.plan")); // простой вопрос — без исследования
    }
}
