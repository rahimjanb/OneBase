using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Analytik;
using OneBase.AI.Tools;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Semantic;
using OneBase.Application.Sales;
using OneBase.Application.Security;
using OneBase.Domain.Identity;
using OneBase.Domain.Sales;
using OneBase.Infrastructure.Linko;
using OneBase.Infrastructure.Persistence;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>
/// Универсальный аналитический движок на настоящем PostgreSQL: обязательные сценарии ТЗ (1–14) на строках Linko — дни × товарные группы,
/// регион × группа, день × регион × группа, сравнение, топ, падение, план из Google Sheets с фактом Linko, повторная синхронизация, отмена
/// и исправление, права и компания, помесячный источник, несопоставленные регионы и товары, дни без средних, «нет инструмента ≠ нет данных»;
/// плюс сверка с «Продажами» и история загрузок Linko. Данные теста — в своих филиалах (≥ 900) и типах товаров: чужие тесты не мешают.
/// </summary>
public sealed class AnalyticsEngineDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private const long BranchA = 961, BranchB = 962, BranchUnknown = 963;
    private const long TypeA = 9_100_001, TypeB = 9_100_002;
    private const long ProductA = 9_100_011, ProductB = 9_100_012, ProductNoType = 9_100_013;
    private const long AgentA = 9_100_101, AgentB = 9_100_102;
    private const long Ids = DataManagerDb.FakeLinkoIds + 500_000;
    private const decimal Price = 20_000;
    private static readonly DateOnly From = new(2026, 8, 1), To = new(2026, 11, 11);

    private static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Permissions.SalesRead, Permissions.FinanceRead, Permissions.AnalytikUse,
    };

    private sealed record World(string S, Guid RegionA, Guid RegionB, string NameA, string NameB, string GroupA, string GroupB);

    private long _next;

    private static T Service<T>(IServiceScope scope) where T : notnull => scope.ServiceProvider.GetRequiredService<T>();

    private static AnalytikPrincipal Principal(IReadOnlySet<string>? permissions = null, SalesRowScope? scope = null) =>
        new(Guid.NewGuid(), permissions ?? All, scope ?? SalesRowScope.Organization);

    private async Task<AnalyticsResult> RunAsync(AnalyticsQuery q, AnalytikPrincipal? p = null)
    {
        using var scope = fixture.Services.CreateScope();
        p ??= Principal();
        return await Service<SemanticEngine>(scope).RunAsync(q, new QueryCaller(p.UserId, "api"), default, p);
    }

    /// <summary>
    /// Регионы «Альфа» (филиал 961) и «Бета» (962); группы — два своих типа Linko вне категорий «Продаж» (группа = тип). Дни 1–10 августа:
    /// Альфа — товар А по (10 + день) кг, Бета — товар Б по 5 × день кг; 20 000 сум/кг. Отмена, возврат, заказ в долларах, филиал без
    /// региона и товар без типа — для проверок. Сентябрь и октябрь: А 100 → 80 кг, Б 50 → 90 кг.
    /// </summary>
    private async Task<World> SeedAsync()
    {
        await fixture.ResetAsync();
        _next = Ids;
        var s = Guid.NewGuid().ToString("N")[..5];
        using var scope = fixture.Services.CreateScope();
        var db = Service<OneBaseDbContext>(scope);
        await db.LinkoOrderReturnLines.Where(l => l.ReturnId >= DataManagerDb.FakeLinkoIds).ExecuteDeleteAsync();
        await db.LinkoOrderReturns.Where(r => r.Id >= DataManagerDb.FakeLinkoIds).ExecuteDeleteAsync();
        foreach (var (id, name) in new[] { (TypeA, "Тест-группа А"), (TypeB, "Тест-группа Б") })
        {
            if (!await db.LinkoProductTypes.AnyAsync(t => t.Id == id))
            {
                db.LinkoProductTypes.Add(new LinkoProductType { Id = id, Name = name });
            }
        }

        foreach (var (id, name, type) in new[] { (ProductA, "Тест товар А", (long?)TypeA), (ProductB, "Тест товар Б", TypeB), (ProductNoType, "Тест товар без типа", null) })
        {
            if (!await db.LinkoProducts.AnyAsync(p => p.Id == id))
            {
                db.LinkoProducts.Add(new LinkoProduct { Id = id, Name = name, Code = $"T{id}", TypeId = type });
            }
        }

        var a = new SalesRegion { LinkoBranchId = BranchA, Name = $"Альфа {s}", SupervisorName = "СВР Альфа" };
        var b = new SalesRegion { LinkoBranchId = BranchB, Name = $"Бета {s}" };
        db.SalesRegions.AddRange(a, b);
        await db.SaveChangesAsync();

        for (var day = 1; day <= 10; day++)
        {
            var date = new DateOnly(2026, 8, day);
            Order(db, BranchA, a.Name, AgentA, ProductA, 10 + day, date);
            Order(db, BranchB, b.Name, AgentB, ProductB, 5 * day, date);
        }

        Order(db, BranchA, a.Name, AgentA, ProductA, 1000, new DateOnly(2026, 8, 5), status: "cancelled");
        Order(db, BranchUnknown, $"Неизвестный {s}", AgentA, ProductA, 7, new DateOnly(2026, 8, 7));
        Order(db, BranchB, b.Name, AgentB, ProductNoType, 4, new DateOnly(2026, 8, 8));
        Order(db, BranchA, a.Name, AgentA, ProductA, 2, new DateOnly(2026, 8, 9), currency: "USD");
        Order(db, BranchA, a.Name, AgentA, ProductA, 100, new DateOnly(2026, 9, 15));
        Order(db, BranchB, b.Name, AgentB, ProductB, 50, new DateOnly(2026, 9, 15));
        Order(db, BranchA, a.Name, AgentA, ProductA, 80, new DateOnly(2026, 10, 15));
        Order(db, BranchB, b.Name, AgentB, ProductB, 90, new DateOnly(2026, 10, 15));

        // Возврат 6 августа: 3 кг товара А в «Альфе».
        var returnId = _next++;
        db.LinkoOrderReturns.Add(new LinkoOrderReturn
        {
            Id = returnId, CreatedDate = new DateOnly(2026, 8, 6), Status = "delivered", MarketId = 9_100_300, AgentId = AgentA, BranchId = BranchA, BranchName = a.Name,
            TotalPrice = 3 * Price, TotalWeight = 3,
        });
        db.LinkoOrderReturnLines.Add(new LinkoOrderReturnLine { Id = returnId, ReturnId = returnId, OrderId = Ids, ProductId = ProductA, Amount = 3, Price = Price, TotalWeight = 3 });
        await db.SaveChangesAsync();
        Service<SalesCacheSignal>(scope).InvalidateHistory();
        return new World(s, a.Id, b.Id, a.Name, b.Name, "Тест-группа А", "Тест-группа Б");
    }

    private void Order(OneBaseDbContext db, long branch, string branchName, long agent, long product, decimal kg, DateOnly date, string status = "delivered",
        string? currency = null)
    {
        var id = _next++;
        db.LinkoOrders.Add(new LinkoOrder
        {
            Id = id, CreatedAt = date.ToDateTime(new TimeOnly(8, 0)), CreatedDate = date, AcceptedAt = date.ToDateTime(new TimeOnly(9, 0)), AcceptedDate = date,
            Status = status, MarketId = 9_100_200 + id % 50, BranchId = branch, BranchName = branchName, AgentId = agent, TotalWeight = kg, TotalPrice = kg * Price,
            Currency = currency,
        });
        db.LinkoOrderLines.Add(new LinkoOrderLine { Id = id, OrderId = id, ProductId = product, Amount = kg, Price = Price, TotalPrice = kg * Price, TotalWeight = kg });
    }

    private static AnalyticsFilter Regions(World w) => new("region", "in", [w.NameA, w.NameB]);

    private static int Col(AnalyticsResult r, string code) => r.Columns.ToList().FindIndex(c => c.Code == code);

    private static decimal Num(object? v) => v is decimal d ? d : Convert.ToDecimal(v);

    // ---------- 1–3: дни × группы, регион × группа, день × регион × группа ----------

    [PostgresFact]
    public async Task T01_daily_revenue_by_product_group_is_the_real_day_fact()
    {
        var w = await SeedAsync();
        var r = await RunAsync(new AnalyticsQuery
        {
            Metrics = ["revenue", "kg"], Dimensions = ["date", "product_group"], Grain = "day", DateFrom = From, DateTo = To, Filters = [Regions(w)], Limit = 5000,
        });

        var rows = r.Rows.Where(x => (string)x[1]! == w.GroupA).ToDictionary(x => (string)x[0]!, x => Num(x[Col(r, "revenue")]));
        Assert.Equal((10 + 3) * Price, rows["2026-08-03"]);
        Assert.Equal((10 + 6 - 3) * Price, rows["2026-08-06"]); // возврат в свой день
        Assert.Equal((10 + 5) * Price, rows["2026-08-05"]); // отменённый заказ не входит
        Assert.Equal((10 + 9) * Price, rows["2026-08-09"]); // заказ в долларах — выручка 0, вес учтён
        var groupB = r.Rows.Single(x => (string)x[0]! == "2026-08-04" && (string)x[1]! == w.GroupB);
        Assert.Equal(20 * Price, Num(groupB[Col(r, "revenue")]));
        Assert.Equal(r.Totals["revenue"], r.Rows.Sum(x => Num(x[Col(r, "revenue")])));
        Assert.Contains(r.Checks, c => c.Code == "grain" && c.Ok && c.Text.Contains("день"));
        Assert.Contains(r.Checks, c => c.Code == "totals" && c.Ok);
    }

    [PostgresFact]
    public async Task T02_region_by_product_group_with_share()
    {
        var w = await SeedAsync();
        var r = await RunAsync(new AnalyticsQuery
        {
            Metrics = ["revenue", "kg", "revenue_share"], Dimensions = ["region", "product_group"], DateFrom = From, DateTo = To, Filters = [Regions(w)],
        });

        var alpha = r.Rows.Single(x => (string)x[0]! == w.NameA && (string)x[1]! == w.GroupA);
        // 1–10 августа: Σ(10 + d) = 155 кг, +2 кг в долларах, −3 кг возврата; сентябрь 100, октябрь 80.
        Assert.Equal(155 + 2 - 3 + 100 + 80, Num(alpha[Col(r, "kg")]));
        Assert.Equal((155 - 3 + 100 + 80) * Price, Num(alpha[Col(r, "revenue")]));
        Assert.Equal(100m, Math.Round(r.Rows.Sum(x => Num(x[Col(r, "revenue_share")])), 0));
        Assert.Contains(r.Checks, c => c.Code == "totals" && c.Ok);
        // Товар без типа в «Бете» (8 августа) — строка «Без группы» и честное «не всё сопоставлено» (подробно — T12).
        Assert.Contains(r.Rows, x => (string)x[1]! == "Без группы");
    }

    [PostgresFact]
    public async Task T03_date_by_region_by_product_group()
    {
        var w = await SeedAsync();
        var r = await RunAsync(new AnalyticsQuery
        {
            Metrics = ["revenue", "orders", "outlets"], Dimensions = ["date", "region", "product_group"], Grain = "day", DateFrom = From, DateTo = To,
            Filters = [Regions(w)], Limit = 5000,
        });

        var cell = r.Rows.Single(x => (string)x[0]! == "2026-08-10" && (string)x[1]! == w.NameB && (string)x[2]! == w.GroupB);
        Assert.Equal(50 * Price, Num(cell[Col(r, "revenue")]));
        Assert.Equal(1m, Num(cell[Col(r, "orders")]));
        Assert.Equal(1m, Num(cell[Col(r, "outlets")]));
        Assert.Equal(r.TotalRows, r.Rows.Count);
    }

    // ---------- 4–6: сравнение месяцев, топ, падение ----------

    [PostgresFact]
    public async Task T04_T06_compare_two_months_and_find_declining_groups()
    {
        var w = await SeedAsync();
        var r = await RunAsync(new AnalyticsQuery
        {
            Metrics = ["revenue"], Dimensions = ["product_group"], DateFrom = new DateOnly(2026, 10, 1), DateTo = new DateOnly(2026, 10, 31), Compare = "previous",
            Filters = [Regions(w)], Sort = [new AnalyticsSort("revenue__pct", "asc")],
        });

        Assert.Equal(new DateOnly(2026, 9, 1), r.CompareFrom);
        Assert.Equal(new DateOnly(2026, 9, 30), r.CompareTo);
        var first = r.Rows[0];
        Assert.Equal(w.GroupA, first[0]);
        Assert.Equal(80 * Price, Num(first[Col(r, "revenue")]));
        Assert.Equal(100 * Price, Num(first[Col(r, "revenue__prev")]));
        Assert.Equal(-20m, Num(first[Col(r, "revenue__pct")]));
        var declining = r.Rows.Where(x => Num(x[Col(r, "revenue__pct")]) < 0).Select(x => (string)x[0]!).ToList();
        Assert.Equal([w.GroupA], declining);
        Assert.Equal(80m, Num(r.Rows.Single(x => (string)x[0]! == w.GroupB)[Col(r, "revenue__pct")]));
    }

    [PostgresFact]
    public async Task T05_top_groups_by_revenue()
    {
        var w = await SeedAsync();
        var r = await RunAsync(new AnalyticsQuery
        {
            Metrics = ["revenue"], Dimensions = ["product_group"], DateFrom = From, DateTo = To, Filters = [Regions(w)],
            Sort = [new AnalyticsSort("revenue", "desc")], Limit = 1,
        });

        Assert.Single(r.Rows);
        Assert.Equal(w.GroupB, r.Rows[0][0]); // Б: 275 + 50 + 90 = 415 кг > А: 155 − 3 + 100 + 80 = 332 кг (по 20 000 сум)
        Assert.True(r.HasMore);
        Assert.Contains(r.Checks, c => c.Code == "limit");
    }

    // ---------- 7: план из Google Sheets + факт Linko ----------

    [PostgresFact]
    public async Task T07_region_plan_from_sheets_with_linko_fact()
    {
        var w = await SeedAsync();
        using (var scope = fixture.Services.CreateScope())
        {
            var db = Service<OneBaseDbContext>(scope);
            db.SalesRegionPlans.AddRange(
                new SalesRegionPlan { RegionId = w.RegionA, Kind = PlanKind.Rop, Year = 2026, Month = 10, PlanKg = 100 },
                new SalesRegionPlan { RegionId = w.RegionB, Kind = PlanKind.Rop, Year = 2026, Month = 10, PlanKg = 50 });
            await db.SaveChangesAsync();
            Service<SalesCacheSignal>(scope).InvalidateHistory();
        }

        var r = await RunAsync(new AnalyticsQuery
        {
            Metrics = ["kg", "plan_kg", "plan_execution", "plan_deviation_kg"], Dimensions = ["region"], Grain = "month",
            DateFrom = new DateOnly(2026, 10, 1), DateTo = new DateOnly(2026, 10, 31), Filters = [Regions(w)],
        });

        var alpha = r.Rows.Single(x => (string)x[0]! == w.NameA);
        Assert.Equal(80m, Num(alpha[Col(r, "kg")]));
        Assert.Equal(100m, Num(alpha[Col(r, "plan_kg")]));
        Assert.Equal(80m, Num(alpha[Col(r, "plan_execution")]));
        Assert.Equal(-20m, Num(alpha[Col(r, "plan_deviation_kg")]));
        var beta = r.Rows.Single(x => (string)x[0]! == w.NameB);
        Assert.Equal(180m, Num(beta[Col(r, "plan_execution")]));
        Assert.Contains(r.Sources, s => s.System == "google_sheets");
        Assert.Contains(r.Sources, s => s.System == "linko");
    }

    // ---------- 8–9: повторная синхронизация, отмена и исправление ----------

    private static object LinkoOrder(long id, long branch, string branchName, decimal kg, DateTime date, string status = "delivered") => new
    {
        id,
        created_date = date,
        accepted_time = date,
        status,
        market = new { id = 5100 + id % 100, name = "Магазин" },
        branch = new { id = branch, name = branchName },
        agent = new { id = 61, name = "Агент" },
        total_price = kg * 30_000,
        total_weight = kg,
        products = new[] { new { id, product = new { id = 100, name = "Кекс 1 кг" }, amount = kg, price = 30_000, total_price = kg * 30_000, total_weight = kg } },
        tm = 1,
    };

    [PostgresFact]
    public async Task T08_T09_resync_does_not_duplicate_and_cancel_or_correction_updates_the_fact()
    {
        await fixture.ResetAsync();
        var s = Guid.NewGuid().ToString("N")[..5];
        var region = $"Сверка {s}";
        var day = DateTime.Today.AddDays(-2);
        var (o1, o2, o3) = (DataManagerDb.FakeLinkoIds + 1, DataManagerDb.FakeLinkoIds + 2, DataManagerDb.FakeLinkoIds + 3);
        fixture.Linko.Orders.AddRange([LinkoOrder(o1, 971, region, 10, day), LinkoOrder(o2, 971, region, 20, day), LinkoOrder(o3, 971, region, 30, day)]);
        var coordinator = fixture.Services.GetRequiredService<LinkoSyncCoordinator>();
        var date = DateOnly.FromDateTime(day);
        var query = new AnalyticsQuery { Metrics = ["kg", "revenue", "orders"], Dimensions = ["region"], DateFrom = date, DateTo = date, Filters = [new("region", "in", [region])] };

        await coordinator.RunAsync(LinkoSyncMode.Incremental);
        var first = await RunAsync(query);
        await coordinator.RunAsync(LinkoSyncMode.Incremental); // та же выгрузка ещё раз
        var second = await RunAsync(query);

        Assert.Equal(60m, first.Totals["kg"]);
        Assert.Equal(first.Totals["kg"], second.Totals["kg"]);
        Assert.Equal(first.Totals["revenue"], second.Totals["revenue"]);
        Assert.Equal(3m, second.Totals["orders"]);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = Service<OneBaseDbContext>(scope);
            Assert.Equal(3, await db.LinkoOrders.CountAsync(o => o.Id >= o1 && o.Id <= o3));
            Assert.Equal(3, await db.LinkoOrderLines.CountAsync(l => l.OrderId >= o1 && l.OrderId <= o3));
            // История загрузок: оба запуска записаны, со строками по шагам.
            var runs = await db.LinkoSyncRuns.OrderByDescending(r => r.StartedAt).Take(2).ToListAsync();
            Assert.Equal(2, runs.Count);
            Assert.All(runs, r => Assert.Contains("\"orders\"", r.Entities));
            Assert.All(runs, r => Assert.Equal(DateOnly.FromDateTime(day), r.EarliestChanged));
        }

        // Linko отменил второй заказ и исправил третий (35 кг вместо 30).
        fixture.Linko.Orders.Clear();
        fixture.Linko.Orders.AddRange([LinkoOrder(o1, 971, region, 10, day), LinkoOrder(o2, 971, region, 20, day, "cancelled"), LinkoOrder(o3, 971, region, 35, day)]);
        await coordinator.RunAsync(LinkoSyncMode.Incremental);
        var after = await RunAsync(query);

        Assert.Equal(45m, after.Totals["kg"]);
        Assert.Equal(2m, after.Totals["orders"]);
        Assert.Equal(45m * 30_000, after.Totals["revenue"]);
    }

    // ---------- 10: права и компания ----------

    [PostgresFact]
    public async Task T10_rbac_scope_and_tenant_isolation()
    {
        var w = await SeedAsync();
        var q = new AnalyticsQuery { Metrics = ["revenue"], Dimensions = ["region"], DateFrom = From, DateTo = To, Filters = [Regions(w)] };

        var regional = Principal(scope: new SalesRowScope(SalesRowScope.RegionLevel, [BranchA], [w.RegionA], [], null, "регионы: Альфа"));
        var onlyAlpha = await RunAsync(q, regional);
        Assert.Equal([w.NameA], onlyAlpha.Rows.Select(x => (string)x[0]!).ToList());
        Assert.Contains(onlyAlpha.Checks, c => c.Code == "scope");

        var agent = Principal(scope: new SalesRowScope(SalesRowScope.AgentLevel, [], [], [], AgentB, "только свои продажи"));
        var onlyB = await RunAsync(q, agent);
        Assert.Equal([w.NameB], onlyB.Rows.Select(x => (string)x[0]!).ToList());

        var noSales = await Assert.ThrowsAsync<AnalyticsQueryException>(() => RunAsync(q, Principal(new HashSet<string> { Permissions.AnalytikUse })));
        Assert.Equal("denied", noSales.Code);
        var noFinance = await Assert.ThrowsAsync<AnalyticsQueryException>(() =>
            RunAsync(q with { Metrics = ["payments"] }, Principal(new HashSet<string> { Permissions.SalesRead })));
        Assert.Equal("denied", noFinance.Code);
        var stock = await Assert.ThrowsAsync<AnalyticsQueryException>(() => RunAsync(new AnalyticsQuery { Metrics = ["stock_units"] }, regional));
        Assert.Equal("denied", stock.Code);
        var tenant = await Assert.ThrowsAsync<AnalyticsQueryException>(() => RunAsync(q with { Tenant = "another-company" }));
        Assert.Equal("denied", tenant.Code);

        // Отказ и запрос — в журнале analytik.Queries.
        using var scope = fixture.Services.CreateScope();
        var log = Service<OneBase.Application.Analytik.IAnalytikDb>(scope).Queries;
        Assert.True(await log.AnyAsync(x => x.Origin == "api:semantic" && x.Status == "denied"));
        Assert.True(await log.AnyAsync(x => x.Origin == "api:semantic" && x.Status == "ok" && x.Scope != null));
    }

    // ---------- 11, 13: только помесячный источник; дни — без средних ----------

    [PostgresFact]
    public async Task T11_monthly_only_plan_is_never_split_into_days()
    {
        var w = await SeedAsync();
        var ex = await Assert.ThrowsAsync<AnalyticsQueryException>(() => RunAsync(new AnalyticsQuery
        {
            Metrics = ["kg", "plan_kg"], Dimensions = ["date"], Grain = "day", DateFrom = new DateOnly(2026, 10, 1), DateTo = new DateOnly(2026, 10, 31), Filters = [Regions(w)],
        }));
        Assert.Equal("unsupported", ex.Code);
        Assert.Contains("помесячный", ex.Message);

        var monthly = await RunAsync(new AnalyticsQuery
        {
            Metrics = ["kg", "plan_kg"], Dimensions = ["date"], Grain = "month", DateFrom = new DateOnly(2026, 10, 1), DateTo = new DateOnly(2026, 10, 31), Filters = [Regions(w)],
        });
        Assert.Single(monthly.Rows);
        Assert.Equal("2026-10-01", monthly.Rows[0][0]);
    }

    [PostgresFact]
    public async Task T13_days_are_facts_not_monthly_averages_and_empty_days_are_zero()
    {
        var w = await SeedAsync();
        var r = await RunAsync(new AnalyticsQuery
        {
            Metrics = ["kg"], Dimensions = ["date"], Grain = "day", DateFrom = new DateOnly(2026, 8, 1), DateTo = new DateOnly(2026, 8, 15),
            Filters = [new("region", "in", [w.NameA])], FillZeros = true,
        });

        var byDay = r.Rows.ToDictionary(x => (string)x[0]!, x => Num(x[1]));
        Assert.Equal(15, byDay.Count);
        Assert.Equal(11m, byDay["2026-08-01"]);
        Assert.Equal(20m, byDay["2026-08-10"]);
        Assert.Equal(0m, byDay["2026-08-12"]); // продаж нет — ноль, а не среднее
        var average = r.Totals["kg"]!.Value / 15;
        Assert.DoesNotContain(byDay.Values, v => Math.Abs(v - average) < 0.001m);
        Assert.Equal(10, byDay.Values.Count(v => v > 0));
        Assert.Equal(13m, byDay["2026-08-06"]); // 16 кг − возврат 3 кг в тот же день
        Assert.Contains(r.Notes, n => n.Contains("нулём"));
    }

    // ---------- 12: несопоставленные регионы и товары ----------

    [PostgresFact]
    public async Task T12_unmapped_region_and_product_are_reported_before_the_answer()
    {
        var w = await SeedAsync();
        var byRegion = await RunAsync(new AnalyticsQuery
        {
            Metrics = ["kg"], Dimensions = ["region"], DateFrom = new DateOnly(2026, 8, 1), DateTo = new DateOnly(2026, 8, 10),
            Filters = [new("product", "in", [ProductA.ToString()])],
        });
        Assert.Equal(7m, Num(byRegion.Rows.Single(x => (string)x[0]! == "Без региона")[1]));
        var regionCheck = Assert.Single(byRegion.Checks, c => c.Code == "region_mapping");
        Assert.False(regionCheck.Ok);
        Assert.False(byRegion.Complete);

        var byGroup = await RunAsync(new AnalyticsQuery
        {
            Metrics = ["kg"], Dimensions = ["product_group"], DateFrom = new DateOnly(2026, 8, 1), DateTo = new DateOnly(2026, 8, 10), Filters = [new("region", "in", [w.NameB])],
        });
        Assert.Equal(4m, Num(byGroup.Rows.Single(x => (string)x[0]! == "Без группы")[1]));
        Assert.False(Assert.Single(byGroup.Checks, c => c.Code == "group_mapping").Ok);
    }

    // ---------- 14: нет готового инструмента ≠ нет данных ----------

    [PostgresFact]
    public async Task T14_slice_without_a_dedicated_tool_is_answered_by_the_engine()
    {
        var w = await SeedAsync();
        Guid userId;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = Service<OneBaseDbContext>(scope);
            var role = new Role { Name = $"Срезы {w.S}" };
            foreach (var code in All)
            {
                role.Permissions.Add(new RolePermission { Code = code });
            }

            var user = new User { Login = $"slices-{w.S}", FullName = "Директор" };
            user.Roles.Add(new UserRole { Role = role });
            db.Users.Add(user);
            await db.SaveChangesAsync();
            userId = user.Id;
        }

        using var s2 = fixture.Services.CreateScope();
        // Реестр: продажи раскладываются по дню, региону и товарной группе вместе — инструмент для этого есть, хотя готового отчёта нет.
        var capabilities = await Service<CapabilityRegistry>(s2).ViewAsync(new QueryCaller(userId, "chat"), default);
        var sales = capabilities.Facts.Single(f => f.Code == SemanticCatalog.Sales);
        Assert.True(sales.Allowed);
        Assert.Contains("day", sales.Grains);
        Assert.Contains("product_group", sales.Dimensions);
        Assert.Contains("region", sales.Dimensions);

        // Инструмент AI — как его вызывает агент: названия вместо id, ответ — строки с данными, а не «данных нет».
        var tool = s2.ServiceProvider.GetServices<ITool>().Single(t => t.Name == AnalyticsQueryTool.ToolName);
        var args = JsonSerializer.SerializeToElement(new
        {
            metrics = new[] { "revenue" },
            dimensions = new[] { "date", "region", "product_group" },
            grain = "day",
            date_from = "2026-08-01",
            date_to = "2026-08-10",
            filters = new[] { new { dimension = "region", op = "in", values = new[] { w.NameA, w.NameB } } },
        });
        var result = await tool.ExecuteAsync(new ToolContext("analyst.sales", userId, 0), args);
        Assert.True(result.Success, result.Content);
        using var doc = JsonDocument.Parse(result.Content);
        Assert.Equal(21, doc.RootElement.GetProperty("totalRows").GetInt32()); // 10 дней × 2 региона + товар без типа в «Бете» 8 августа
        Assert.StartsWith("/analytik/explore?q=", doc.RootElement.GetProperty("link").GetString());

        // Источник движка есть у консультанта (все источники) и у аналитика продаж.
        Assert.Contains(OneBase.AI.Knowledge.KnowledgeSources.SalesSlices, OneBase.AI.Agents.AgentDefaults.All.Single(a => a.Code == "sales").Sources);
        Assert.Contains(OneBase.AI.Knowledge.KnowledgeSources.SalesSlices, AnalytikAgents.Analysts.Single(a => a.Code == "analyst.sales").Sources);
    }

    // ---------- сверка с «Продажами» ----------

    [PostgresFact]
    public async Task Region_revenue_and_kg_match_the_sales_month_report()
    {
        var w = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var analytics = await Service<SalesDataLoader>(scope).LoadAsync(2026, 8);
        var page = analytics.Republic(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)).Regions.Where(x => x.Name == w.NameA || x.Name == w.NameB).ToList();
        var r = await RunAsync(new AnalyticsQuery
        {
            Metrics = ["revenue", "kg", "outlets"], Dimensions = ["region"], DateFrom = new DateOnly(2026, 8, 1), DateTo = new DateOnly(2026, 8, 31), Filters = [Regions(w)],
        });

        Assert.Equal(2, page.Count);
        foreach (var row in page)
        {
            var e = r.Rows.Single(x => (string)x[0]! == row.Name);
            Assert.Equal(row.Revenue, Num(e[Col(r, "revenue")]));
            Assert.Equal(row.FactKg, Num(e[Col(r, "kg")]));
            Assert.Equal(row.Akb, (int)Num(e[Col(r, "outlets")]));
        }
    }
}
