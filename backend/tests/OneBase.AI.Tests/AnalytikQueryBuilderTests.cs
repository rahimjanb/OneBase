using System.Text.Json;
using OneBase.Application.Analytik.Catalog;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Security;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>
/// Построитель SQL аналитического движка без базы: только каталожные имена и параметры, отказ на секреты, закрытые таблицы и чужие права,
/// защита от завышения сумм через «один ко многим», неоднозначные и «многие ко многим» связи, предикаты области продаж, пределы.
/// </summary>
public sealed class AnalytikQueryBuilderTests
{
    private static readonly QueryLimits Limits = new(500, 2000);

    private static readonly AnalytikPrincipal Director = Principal(Permissions.AnalytikUse, Permissions.SalesRead, Permissions.FinanceRead,
        Permissions.HrRead, Permissions.UsersManage, Permissions.ProcurementRead);

    private static AnalytikPrincipal Principal(params string[] permissions) =>
        new(Guid.NewGuid(), permissions.ToHashSet(), SalesRowScope.Organization);

    private static readonly SalesRowScope Khorazm = new(SalesRowScope.RegionLevel, [5L, 105L], [Guid.Parse("11111111-1111-1111-1111-111111111111")],
        [Guid.Parse("22222222-2222-2222-2222-222222222222")], null, "регионы: Хоразм");

    internal static CatalogModel Catalog()
    {
        CatalogTableInfo T(string full, params (string Name, string Type)[] columns)
        {
            var (schema, name) = (full.Split('.')[0], full.Split('.')[1]);
            var policy = CatalogRules.Policy(schema, name);
            var cols = columns.Select((c, i) =>
            {
                var sc = new SchemaColumn(c.Name, i + 1, c.Type, c.Name != "Id", null);
                var unique = c.Name == "Id" || (full == "sales.Regions" && c.Name == "LinkoBranchId");
                return new CatalogColumnInfo(c.Name, i + 1, c.Type, c.Name != "Id", c.Name == "Id", true, CatalogRules.RoleOf(sc, c.Name == "Id"), null, null,
                    CatalogRules.SensitivityOf(schema, name, c.Name), unique);
            }).ToList();
            return new CatalogTableInfo(schema, name, "table", policy.Module, name, null, "auto", 1000, true, 0, policy.Access, policy.RequiredPermission,
                policy.ScopeRule, columns.FirstOrDefault(c => c.Type == "date").Name, "Id", DateTimeOffset.UtcNow, null, cols);
        }

        var tables = new List<CatalogTableInfo>
        {
            T("linko.Orders", ("Id", "bigint"), ("MarketId", "bigint"), ("AgentId", "bigint"), ("BranchId", "bigint"), ("Status", "text"),
                ("AcceptedDate", "date"), ("TotalPrice", "numeric"), ("TotalWeight", "numeric")),
            T("linko.OrderLines", ("Id", "bigint"), ("OrderId", "bigint"), ("ProductId", "bigint"), ("Amount", "numeric"), ("TotalPrice", "numeric")),
            T("linko.Markets", ("Id", "bigint"), ("Name", "text"), ("BranchId", "bigint"), ("ResponsibleAgentId", "bigint")),
            T("linko.Products", ("Id", "bigint"), ("Name", "text")),
            T("linko.Users", ("Id", "bigint"), ("FirstName", "text"), ("Username", "text")),
            T("linko.Visits", ("Id", "bigint"), ("MarketId", "bigint"), ("UserId", "bigint"), ("Status", "text"), ("Day", "date")),
            T("linko.Stocks", ("Id", "bigint"), ("Name", "text")),
            T("linko.StockTransfers", ("Id", "bigint"), ("FromStockId", "bigint"), ("ToStockId", "bigint"), ("TotalWeight", "numeric")),
            T("linko.ProductBalances", ("Id", "bigint"), ("Balance", "numeric")),
            T("sales.Regions", ("Id", "uuid"), ("LinkoBranchId", "bigint"), ("Name", "text")),
            T("public.Users", ("Id", "uuid"), ("FullName", "text"), ("PasswordHash", "text")),
            T("hr.Employees", ("Id", "uuid"), ("FullName", "text"), ("Phone", "text")),
            T("ai.Providers", ("Id", "uuid"), ("Code", "text")),
        };

        CatalogRelationInfo R(string from, string to, string kind = RelationKinds.Inferred, string status = RelationStatus.Confirmed)
        {
            var f = from.Split('.');
            var t = to.Split('.');
            return new CatalogRelationInfo(f[0], f[1], f[2], t[0], t[1], t[2], kind, status, 1, 10, null);
        }

        return new CatalogModel(tables,
        [
            R("linko.OrderLines.OrderId", "linko.Orders.Id", RelationKinds.ForeignKey),
            R("linko.OrderLines.ProductId", "linko.Products.Id"),
            R("linko.Orders.MarketId", "linko.Markets.Id"),
            R("linko.Orders.BranchId", "sales.Regions.LinkoBranchId", RelationKinds.Manual),
            R("linko.Orders.AgentId", "linko.Users.Id", RelationKinds.Manual, RelationStatus.Rejected),
            R("linko.Visits.MarketId", "linko.Markets.Id"),
            R("linko.StockTransfers.FromStockId", "linko.Stocks.Id"),
            R("linko.StockTransfers.ToStockId", "linko.Stocks.Id"),
        ], DateTimeOffset.UtcNow);
    }

    private static JsonElement J(object value) => JsonSerializer.SerializeToElement(value);

    private static BuiltQuery Build(QueryPlan plan, AnalytikPrincipal? who = null) => QueryBuilder.Build(plan, Catalog(), who ?? Director, Limits);

    private static QueryPlanException Fails(QueryPlan plan, AnalytikPrincipal? who = null) =>
        Assert.Throws<QueryPlanException>(() => Build(plan, who));

    [Fact]
    public void Revenue_by_region_and_month_joins_by_confirmed_relation_with_parameters()
    {
        var q = Build(new QueryPlan
        {
            From = "linko.Orders",
            GroupBy = ["Regions.Name"],
            Bucket = new PlanBucket("AcceptedDate", "month", "month"),
            Measures = [new PlanMeasure("sum", "TotalPrice", "revenue"), new PlanMeasure("count", null, "orders")],
            Filters = [new PlanFilter("Status", "in", J(new[] { "delivered", "given" })), new PlanFilter("AcceptedDate", "between", J(new[] { "2026-09-01", "2026-10-31" }))],
        });

        Assert.Contains("LEFT JOIN \"sales\".\"Regions\" t1 ON t1.\"LinkoBranchId\" = t0.\"BranchId\"", q.Sql);
        Assert.Contains("date_trunc('month', t0.\"AcceptedDate\")::date", q.Sql);
        Assert.Contains("GROUP BY 1, 2", q.Sql);
        Assert.DoesNotContain("delivered", q.Sql);
        Assert.DoesNotContain("2026-09-01", q.Sql);
        Assert.Contains(q.Parameters, p => p.Value is string[] s && s.SequenceEqual(["delivered", "given"]));
        Assert.Contains(q.Parameters, p => p.Value is DateOnly d && d == new DateOnly(2026, 9, 1));
        Assert.Equal(["Name", "month", "revenue", "orders"], q.Columns.Select(c => c.Name));
        Assert.True(q.Aggregate);
        Assert.Empty(q.Notes);
    }

    [Theory]
    [InlineData("linko.Orders; DROP TABLE linko.\"Orders\"")]
    [InlineData("pg_catalog.pg_authid")]
    [InlineData("linko.Orders\" --")]
    public void Unknown_tables_including_injection_are_rejected(string from) =>
        Assert.Equal(QueryErrors.Invalid, Fails(new QueryPlan { From = from }).Code);

    [Fact]
    public void Injection_through_values_aliases_units_functions_and_order_is_impossible()
    {
        var q = Build(new QueryPlan { From = "linko.Orders", Filters = [new PlanFilter("Status", "eq", J("x'); DROP TABLE linko.\"Orders\"; --"))] });
        Assert.DoesNotContain("DROP", q.Sql);
        Assert.Contains(q.Parameters, p => (p.Value as string)?.Contains("DROP") == true);

        Assert.Equal(QueryErrors.Invalid, Fails(new QueryPlan { From = "linko.Orders", Select = [new PlanField("Status\"; DROP TABLE x; --")] }).Code);
        Assert.Equal(QueryErrors.Invalid, Fails(new QueryPlan { From = "linko.Orders", Select = [new PlanField("Status", "a\" FROM pg_shadow --")] }).Code);
        Assert.Equal(QueryErrors.Invalid, Fails(new QueryPlan
        {
            From = "linko.Orders", Measures = [new PlanMeasure("count")], Bucket = new PlanBucket("AcceptedDate", "month', now()) --"),
        }).Code);
        Assert.Equal(QueryErrors.Invalid, Fails(new QueryPlan { From = "linko.Orders", Measures = [new PlanMeasure("sum(1)); DROP TABLE x; --", "TotalPrice")] }).Code);
        Assert.Equal(QueryErrors.Invalid, Fails(new QueryPlan { From = "linko.Orders", OrderBy = [new PlanOrder("Status", "desc; DROP TABLE x")] }).Code);
        Assert.Equal(QueryErrors.Invalid, Fails(new QueryPlan { From = "linko.Orders", Filters = [new PlanFilter("Status", "eq'); DROP", J("x"))] }).Code);
        Assert.Equal(QueryErrors.Invalid, Fails(new QueryPlan { From = "linko.Orders", Filters = [new PlanFilter("TotalPrice", "gt", J("1 OR 1=1"))] }).Code);
    }

    [Fact]
    public void Secret_and_personal_columns_closed_tables_and_missing_rights_are_denied()
    {
        Assert.Equal(QueryErrors.Denied, Fails(new QueryPlan { From = "public.Users", Select = [new PlanField("PasswordHash")] }).Code);
        Assert.Equal(QueryErrors.Denied, Fails(new QueryPlan { From = "hr.Employees", Filters = [new PlanFilter("Phone", "starts_with", J("+998"))] }).Code);
        Assert.Equal(QueryErrors.Denied, Fails(new QueryPlan { From = "linko.Users", Measures = [new PlanMeasure("count")], GroupBy = ["Username"] }).Code);
        Assert.Equal(QueryErrors.Denied, Fails(new QueryPlan { From = "ai.Providers" }).Code);

        var hrOnly = Principal(Permissions.AnalytikUse, Permissions.HrRead);
        Assert.Equal(QueryErrors.Denied, Fails(new QueryPlan { From = "linko.Orders" }, hrOnly).Code);
        Assert.Equal(QueryErrors.Denied, Fails(new QueryPlan { From = "hr.Employees" }, Principal(Permissions.HrRead)).Code);

        // Без колонок по умолчанию — только видимые: пароль и телефон в результат не попадают.
        var users = Build(new QueryPlan { From = "public.Users" });
        Assert.DoesNotContain(users.Columns, c => c.Name == "PasswordHash");
        Assert.DoesNotContain("PasswordHash", users.Sql);
    }

    [Fact]
    public void Sums_through_one_to_many_joins_are_refused_but_detail_base_works()
    {
        var error = Fails(new QueryPlan { From = "linko.Markets", GroupBy = ["Name"], Measures = [new PlanMeasure("sum", "Orders.TotalPrice")] });
        Assert.Contains("один ко многим", error.Message);

        var distinct = Build(new QueryPlan { From = "linko.Markets", GroupBy = ["Name"], Measures = [new PlanMeasure("count_distinct", "Orders.Id", "orders")] });
        Assert.Contains("LEFT JOIN \"linko\".\"Orders\" t1 ON t1.\"MarketId\" = t0.\"Id\"", distinct.Sql);

        var lines = Build(new QueryPlan { From = "linko.OrderLines", GroupBy = ["Markets.Name"], Measures = [new PlanMeasure("sum", "TotalPrice", "revenue")] });
        Assert.Contains("JOIN \"linko\".\"Orders\" t1", lines.Sql);
        Assert.Contains("JOIN \"linko\".\"Markets\" t2", lines.Sql);
    }

    [Fact]
    public void Ambiguous_many_to_many_and_unconfirmed_relations_are_not_guessed()
    {
        var ambiguous = Fails(new QueryPlan { From = "linko.StockTransfers", Select = [new PlanField("Stocks.Name")] });
        Assert.Contains("неоднозначна", ambiguous.Message);
        var explicitJoin = Build(new QueryPlan { From = "linko.StockTransfers", Join = ["linko.Stocks via ToStockId"], Select = [new PlanField("Stocks.Name", "to_stock")] });
        Assert.Contains("t1.\"Id\" = t0.\"ToStockId\"", explicitJoin.Sql);

        // Заказы и визиты — соседи через точку: «многие ко многим» не строится.
        Assert.Equal(QueryErrors.Invalid, Fails(new QueryPlan { From = "linko.Orders", Select = [new PlanField("Visits.Status")] }).Code);
        // Связь агента отклонена данными — по ней не соединяем.
        Assert.Equal(QueryErrors.Invalid, Fails(new QueryPlan { From = "linko.Orders", Select = [new PlanField("linko.Users.FirstName")] }).Code);
    }

    [Fact]
    public void Region_scope_adds_row_predicates_and_refuses_unscopable_tables()
    {
        var rm = new AnalytikPrincipal(Guid.NewGuid(), new HashSet<string> { Permissions.AnalytikUse, Permissions.SalesRead }, Khorazm);

        var orders = Build(new QueryPlan { From = "linko.Orders", Measures = [new PlanMeasure("sum", "TotalWeight", "kg")] }, rm);
        Assert.Contains("t0.\"BranchId\" = ANY(@scope_branches)", orders.Sql);
        Assert.Contains(orders.Parameters, p => p.Name == "scope_branches" && p.Value is long[] b && b.SequenceEqual([5L, 105L]));
        Assert.Equal("регионы: Хоразм", orders.Scope);

        var lines = Build(new QueryPlan { From = "linko.OrderLines", Measures = [new PlanMeasure("sum", "TotalPrice")] }, rm);
        Assert.Contains("EXISTS (SELECT 1 FROM \"linko\".\"Orders\" sp0 WHERE sp0.\"Id\" = t0.\"OrderId\" AND sp0.\"BranchId\" = ANY(@scope_branches))", lines.Sql);

        var visits = Build(new QueryPlan { From = "linko.Visits", Measures = [new PlanMeasure("count")] }, rm);
        Assert.Contains("EXISTS (SELECT 1 FROM \"linko\".\"Markets\" sm0 WHERE sm0.\"Id\" = t0.\"MarketId\"", visits.Sql);

        // Справочник товаров — без ограничения; остатки дилеров ограничить регионом нельзя — отказ.
        Assert.DoesNotContain("scope", Build(new QueryPlan { From = "linko.Products" }, rm).Sql);
        Assert.Equal(QueryErrors.Denied, Fails(new QueryPlan { From = "linko.ProductBalances" }, rm).Code);

        // Присоединённая таблица продаж ограничивается в условии соединения.
        var withRegion = Build(new QueryPlan { From = "linko.OrderLines", GroupBy = ["Regions.Name"], Measures = [new PlanMeasure("sum", "TotalPrice")] }, rm);
        Assert.Contains("t2.\"Id\" = ANY(@scope_regions)", withRegion.Sql);
    }

    [Fact]
    public void Agent_scope_sees_only_own_rows()
    {
        var agent = new AnalytikPrincipal(Guid.NewGuid(), new HashSet<string> { Permissions.AnalytikUse, Permissions.SalesRead },
            new SalesRowScope(SalesRowScope.AgentLevel, [], [], [], 51, "только свои продажи (ТП 51)"));
        var q = Build(new QueryPlan { From = "linko.Orders", Measures = [new PlanMeasure("sum", "TotalWeight")] }, agent);
        Assert.Contains("t0.\"AgentId\" = @scope_agent", q.Sql);
        Assert.Contains(q.Parameters, p => p.Name == "scope_agent" && p.Value is long a && a == 51);
        var visits = Build(new QueryPlan { From = "linko.Visits", Measures = [new PlanMeasure("count")] }, agent);
        Assert.Contains("t0.\"UserId\" = @scope_agent", visits.Sql);
        Assert.Equal(QueryErrors.Denied, Fails(new QueryPlan { From = "sales.Regions" }, agent).Code);
    }

    [Fact]
    public void Limits_are_capped_and_one_extra_row_detects_more()
    {
        var q = Build(new QueryPlan { From = "linko.Orders", Limit = 1_000_000 });
        Assert.Equal(500, q.Limit);
        Assert.EndsWith("LIMIT 501 OFFSET 0", q.Sql);
        Assert.StartsWith("SELECT ", q.Sql);
        Assert.Equal(QueryErrors.Invalid, Fails(new QueryPlan { From = "linko.Orders", Bucket = new PlanBucket("AcceptedDate", "month") }).Code);
    }
}
