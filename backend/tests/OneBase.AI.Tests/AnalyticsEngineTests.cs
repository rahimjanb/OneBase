using System.Text.Json;
using OneBase.AI.Analytik;
using OneBase.AI.Tools.Data;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Semantic;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>Универсальный движок без базы: реестр согласован, SQL только из белого списка и параметров, разбор аргументов инструмента.</summary>
public sealed class AnalyticsEngineTests
{
    [Fact]
    public void Catalog_is_consistent_every_calculated_metric_has_sources_and_every_dimension_a_fact()
    {
        var facts = new[] { SemanticCatalog.Sales, SemanticCatalog.Plan, SemanticCatalog.Payments, SemanticCatalog.Stock, SemanticCatalog.Visits };
        foreach (var m in SemanticCatalog.Metrics)
        {
            // У каждого показателя движка есть профиль паспорта (смысл, таблица, агрегация) — без «общего» запасного профиля.
            Assert.True(MetricPassports.Profiles.ContainsKey(m.Code), $"Нет профиля паспорта у {m.Code}");
            Assert.Contains(m.Fact, facts);
            Assert.False(string.IsNullOrWhiteSpace(m.Formula));
            foreach (var r in m.Requires ?? [])
            {
                Assert.NotNull(SemanticCatalog.Metric(r));
            }
        }

        foreach (var d in SemanticCatalog.Dimensions)
        {
            Assert.All(d.Facts, f => Assert.Contains(f, facts));
        }

        Assert.Equal("product_group", SemanticCatalog.Dimension("категория")!.Code);
        Assert.Equal("market", SemanticCatalog.Dimension("клиент")!.Code);
        Assert.Equal(SemanticCatalog.Metrics.Count, SemanticCatalog.Metrics.Select(m => m.Code).Distinct().Count());
        // Выручка, отгрузка и оплаты — разные показатели разных фактов.
        Assert.NotEqual(SemanticCatalog.Metric("revenue")!.Fact, SemanticCatalog.Metric("payments")!.Fact);
    }

    private static SemanticContext Context()
    {
        var region = new RegionInfo(Guid.NewGuid(), 5, "Хоразм", null, "СВР", "Дилер");
        var structure = new SalesStructureData([], [region], new Dictionary<long, Guid>(), new Dictionary<Guid, Guid>());
        return new SemanticContext(structure, new Dictionary<long, Guid> { [5] = region.Id }, new Dictionary<long, string> { [5] = "Хоразм" },
            new Dictionary<Guid, RegionInfo> { [region.Id] = region }, new Dictionary<Guid, DirectionInfo>(),
            SalesCategories.Build(new Dictionary<string, string> { ["Кекс"] = "10" }, new Dictionary<long, string> { [10] = "Кекс" }, new Dictionary<long, long?>()),
            new Dictionary<long, string> { [10] = "Кекс" }, [], ["завод"], ["к к мерч"], ["завод", "к к мерч"]);
    }

    [Fact]
    public void Sql_takes_names_from_the_whitelist_and_values_only_as_parameters()
    {
        var evil = "'; drop table linko.\"Orders\"; --";
        var built = new SemanticSql(Context(), new SalesOptions()).Build(SemanticCatalog.Sales, ["date", "region", "product_group"], ["revenue", "kg", "outlets"], "week",
            new DateOnly(2026, 8, 1), new DateOnly(2026, 11, 11), "secondary", SalesRowScope.Organization,
            [new ResolvedFilter("manager", false, [evil], false, "test")]);

        Assert.DoesNotContain("drop table", built.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(built.Parameters, p => p.Value is string[] a && a.Contains(evil));
        Assert.Contains("date_trunc('week', x.d)", built.Sql);
        Assert.Contains("count(*) filter (where y.mkt is not null and y.m_kg > 0 and not y.chan) m_outlets", built.Sql);
        Assert.Throws<AnalyticsQueryException>(() => SemanticSql.DimensionExpr(SemanticCatalog.Sales, "x.d; drop", "day"));
        Assert.Throws<AnalyticsQueryException>(() => new SemanticSql(Context(), new SalesOptions())
            .Build(SemanticCatalog.Sales, [], ["sum(1)"], "day", new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 2), "secondary", SalesRowScope.Organization, []));
    }

    [Fact]
    public void Region_scope_becomes_a_sql_predicate()
    {
        var scope = new SalesRowScope(SalesRowScope.RegionLevel, [5, 23], [], [], null, "регионы: Хоразм");
        var built = new SemanticSql(Context(), new SalesOptions()).Build(SemanticCatalog.Sales, ["region"], ["revenue"], "day",
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), "secondary", scope, []);
        Assert.Contains("o.\"BranchId\" = any(", built.Sql);
        Assert.Contains("r.\"BranchId\" = any(", built.Sql);
        Assert.Contains(built.Parameters, p => p.Value is long[] a && a.SequenceEqual(new long[] { 5, 23 }));
    }

    [Fact]
    public void Tool_arguments_become_a_query_periods_lists_and_limits()
    {
        var today = new DateOnly(2026, 11, 12);
        var args = new ToolArgs(JsonSerializer.SerializeToElement(new
        {
            metrics = new[] { "revenue", "kg" },
            dimensions = "date, product_group",
            period = "last_3_months",
            filters = new[] { new { dimension = "region", values = new[] { "Самарканд" } } },
            sort = new[] { new { by = "revenue", dir = "desc" } },
        }));
        var q = AnalyticsQueryTool.Parse(args, today);
        Assert.Equal(["revenue", "kg"], q.Metrics);
        Assert.Equal(["date", "product_group"], q.Dimensions);
        Assert.Equal(new DateOnly(2026, 8, 1), q.DateFrom);
        Assert.Equal(new DateOnly(2026, 10, 31), q.DateTo);
        Assert.Equal(AnalyticsQueryTool.ChatRows, q.Limit);
        Assert.Equal("Самарканд", q.Filters![0].Values![0]);

        var report = AnalyticsQueryTool.Parse(new ToolArgs(JsonSerializer.SerializeToElement(new { metrics = new[] { "revenue" } })) { Report = true }, today);
        Assert.Equal(SemanticEngine.MaxLimit, report.Limit);
        Assert.Equal((today.AddDays(-29), today), AnalyticsQueryTool.PeriodRange("last_30_days", today));
        Assert.StartsWith("/analytik/explore?q=", AnalyticsQueryTool.Link(q));
    }

    [Fact]
    public void Week_month_and_quarter_buckets_match_postgres_date_trunc()
    {
        Assert.Equal(new DateOnly(2026, 11, 9), SemanticEngine.Trunc(new DateOnly(2026, 11, 12), "week")); // понедельник
        Assert.Equal(new DateOnly(2026, 11, 9), SemanticEngine.Trunc(new DateOnly(2026, 11, 9), "week"));
        Assert.Equal(new DateOnly(2026, 11, 1), SemanticEngine.Trunc(new DateOnly(2026, 11, 12), "month"));
        Assert.Equal(new DateOnly(2026, 10, 1), SemanticEngine.Trunc(new DateOnly(2026, 11, 12), "quarter"));
        Assert.Equal(new DateOnly(2026, 1, 1), SemanticEngine.Trunc(new DateOnly(2026, 11, 12), "year"));
    }
}
