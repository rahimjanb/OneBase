using OneBase.Application.Analytik.Catalog;
using OneBase.Application.Security;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>Правила каталога Аналитика без базы: доступ таблиц, чувствительность и роли колонок, вывод связей, отпечаток, поиск.</summary>
public sealed class AnalytikCatalogTests
{
    [Theory]
    [InlineData("linko", "Orders", "sales", Permissions.SalesRead, CatalogAccess.Open)]
    [InlineData("linko", "Payments", "finance", Permissions.FinanceRead, CatalogAccess.Open)]
    [InlineData("sales", "PnlValues", "finance", Permissions.FinanceRead, CatalogAccess.Open)]
    [InlineData("public", "Users", "system", Permissions.UsersManage, CatalogAccess.Open)]
    [InlineData("procurement", "StockBalances", "procurement", "procurement.read|procurement.manage", CatalogAccess.Open)]
    [InlineData("work", "Tasks", "work", Permissions.AnalytikManage, CatalogAccess.Open)]
    public void Policy_by_schema_and_table(string schema, string table, string module, string permission, string access)
    {
        var p = CatalogRules.Policy(schema, table);
        Assert.Equal(module, p.Module);
        Assert.Equal(permission, p.RequiredPermission);
        Assert.Equal(access, p.Access);
    }

    [Theory]
    [InlineData("ai", "Providers")]
    [InlineData("public", "UserSessions")]
    [InlineData("public", "DataProtectionKeys")]
    [InlineData("telegram", "Accounts")]
    [InlineData("ai", "Messages")]
    [InlineData("sales", "UserScopes")]
    public void Secrets_sessions_and_private_chats_are_closed(string schema, string table) =>
        Assert.True(CatalogRules.Policy(schema, table).Closed);

    [Fact]
    public void New_table_of_known_schema_inherits_schema_policy_and_unknown_schema_is_closed()
    {
        var added = CatalogRules.Policy("production", "CapacityLines");
        Assert.Equal("production", added.Module);
        Assert.Equal(CatalogAccess.Open, added.Access);

        var unknown = CatalogRules.Policy("finance_new", "Expenses");
        Assert.Equal("other", unknown.Module);
        Assert.True(unknown.Closed);
    }

    [Theory]
    [InlineData("public", "Users", "PasswordHash", Sensitivity.Secret)]
    [InlineData("ai", "Providers", "ProtectedApiKey", Sensitivity.Secret)]
    [InlineData("public", "FileLocks", "Token", Sensitivity.Secret)]
    [InlineData("recruitment", "HhConnection", "TokenExpiresAt", Sensitivity.None)]
    [InlineData("ai", "Usage", "InputTokens", Sensitivity.None)]
    [InlineData("hr", "Employees", "Phone", Sensitivity.Personal)]
    [InlineData("hr", "Employees", "Note", Sensitivity.Personal)]
    [InlineData("procurement", "Suppliers", "Email", Sensitivity.Personal)]
    [InlineData("linko", "Markets", "Lat", Sensitivity.None)]
    [InlineData("field", "Visits", "Lat", Sensitivity.Personal)]
    [InlineData("procurement", "Documents", "ObjectKey", Sensitivity.Internal)]
    [InlineData("linko", "Orders", "TotalPrice", Sensitivity.None)]
    public void Column_sensitivity(string schema, string table, string column, string expected) =>
        Assert.Equal(expected, CatalogRules.SensitivityOf(schema, table, column));

    [Theory]
    [InlineData("TotalPrice", "numeric", ColumnRoles.Money)]
    [InlineData("TotalWeight", "numeric", ColumnRoles.Weight)]
    [InlineData("DiscountPercent", "numeric", ColumnRoles.Percent)]
    [InlineData("OnHand", "numeric(18,3)", ColumnRoles.Quantity)]
    [InlineData("Status", "text", ColumnRoles.Status)]
    [InlineData("Name", "character varying(200)", ColumnRoles.Name)]
    [InlineData("Sku", "character varying(64)", ColumnRoles.Code)]
    [InlineData("CreatedDate", "date", ColumnRoles.Date)]
    [InlineData("CreatedAt", "timestamp with time zone", ColumnRoles.DateTime)]
    [InlineData("MarketId", "bigint", ColumnRoles.Reference)]
    [InlineData("IsDelete", "boolean", ColumnRoles.Flag)]
    [InlineData("LeadTimeDays", "integer", ColumnRoles.Count)]
    public void Column_roles(string name, string type, string role) =>
        Assert.Equal(role, CatalogRules.RoleOf(new SchemaColumn(name, 1, type, true, null), false));

    [Fact]
    public void Date_column_prefers_business_dates_over_service_timestamps()
    {
        var t = Table("linko", "Orders", [("Id", "bigint"), ("CreatedAt", "timestamp without time zone"), ("CreatedDate", "date"), ("AcceptedDate", "date")]);
        Assert.Equal("CreatedDate", CatalogRules.DateColumnOf(t));
        var service = Table("x", "Y", [("Id", "uuid"), ("UpdatedAt", "timestamp with time zone"), ("StartedAt", "timestamp with time zone")]);
        Assert.Equal("StartedAt", CatalogRules.DateColumnOf(service));
    }

    [Fact]
    public void Relations_from_foreign_keys_knowledge_and_names_with_type_check_and_no_guessing()
    {
        var tables = new List<SchemaTable>
        {
            Table("linko", "Orders", [("Id", "bigint"), ("MarketId", "bigint"), ("AgentId", "bigint"), ("BranchId", "bigint")]),
            Table("linko", "OrderLines", [("Id", "bigint"), ("OrderId", "bigint"), ("ProductId", "bigint")],
                [new SchemaForeignKey("FK_OrderLines_Orders", ["OrderId"], "linko", "Orders", ["Id"])]),
            Table("linko", "Markets", [("Id", "bigint"), ("Name", "text")]),
            Table("linko", "Users", [("Id", "bigint"), ("FirstName", "text")]),
            Table("linko", "Visits", [("Id", "bigint"), ("UserId", "bigint"), ("MarketId", "bigint")]),
            Table("linko", "Products", [("Id", "bigint"), ("TypeId", "bigint")]),
            Table("linko", "ProductTypes", [("Id", "bigint"), ("ParentId", "bigint")]),
            Table("linko", "Stocks", [("Id", "bigint")]),
            Table("linko", "StockTransfers", [("Id", "bigint"), ("FromStockId", "bigint"), ("ToStockId", "bigint")]),
            Table("sales", "Regions", [("Id", "uuid"), ("LinkoBranchId", "bigint")]),
            Table("public", "Users", [("Id", "uuid"), ("Login", "text")]),
            Table("hr", "Employees", [("Id", "uuid"), ("UserId", "uuid"), ("NoteUpdatedById", "uuid")]),
            Table("procurement", "Items", [("Id", "uuid")]),
            Table("procurement", "Warehouses", [("Id", "uuid")]),
            Table("procurement", "StockMovements", [("Id", "uuid"), ("ItemId", "uuid"), ("WarehouseId", "uuid"), ("ActorId", "uuid"), ("TaskId", "uuid")]),
            Table("field", "RoutePoints", [("Id", "uuid"), ("MarketId", "bigint")]),
            Table("work", "Tasks", [("Id", "uuid"), ("SuggestionId", "uuid")]),
            Table("work", "TaskSuggestions", [("Id", "uuid")]),
            Table("field", "Tasks", [("Id", "uuid")]),
        };

        var relations = RelationInference.Build(tables);
        string? Target(string from) => relations.FirstOrDefault(r => $"{r.FromSchema}.{r.FromTable}.{r.FromColumn}" == from) is { } r
            ? $"{r.ToSchema}.{r.ToTable}.{r.ToColumn}:{r.Kind}"
            : null;

        Assert.Equal("linko.Orders.Id:fk", Target("linko.OrderLines.OrderId"));
        Assert.Equal("linko.Markets.Id:inferred", Target("linko.Orders.MarketId"));
        Assert.Equal("linko.Users.Id:manual", Target("linko.Orders.AgentId"));
        Assert.Equal("sales.Regions.LinkoBranchId:manual", Target("linko.Orders.BranchId"));
        // Пользователь Linko (bigint), а не OneBase (uuid).
        Assert.Equal("linko.Users.Id:manual", Target("linko.Visits.UserId"));
        Assert.Equal("public.Users.Id:inferred", Target("hr.Employees.UserId"));
        Assert.Equal("public.Users.Id:inferred", Target("hr.Employees.NoteUpdatedById"));
        Assert.Equal("public.Users.Id:inferred", Target("procurement.StockMovements.ActorId"));
        Assert.Equal("linko.ProductTypes.Id:inferred", Target("linko.Products.TypeId"));
        Assert.Equal("linko.ProductTypes.Id:inferred", Target("linko.ProductTypes.ParentId"));
        Assert.Equal("linko.Stocks.Id:inferred", Target("linko.StockTransfers.FromStockId"));
        Assert.Equal("procurement.Warehouses.Id:inferred", Target("procurement.StockMovements.WarehouseId"));
        Assert.Equal("linko.Markets.Id:inferred", Target("field.RoutePoints.MarketId"));
        Assert.Equal("work.TaskSuggestions.Id:inferred", Target("work.Tasks.SuggestionId"));
        // Две равноправные таблицы задач в других схемах — связь не угадывается.
        Assert.Null(Target("procurement.StockMovements.TaskId"));
    }

    [Fact]
    public void Fingerprint_changes_when_structure_changes()
    {
        var before = Table("x", "T", [("Id", "uuid"), ("Name", "text")]);
        var after = Table("x", "T", [("Id", "uuid"), ("Name", "text"), ("Kg", "numeric")]);
        Assert.Equal(CatalogRules.Fingerprint(before), CatalogRules.Fingerprint(Table("x", "T", [("Id", "uuid"), ("Name", "text")])));
        Assert.NotEqual(CatalogRules.Fingerprint(before), CatalogRules.Fingerprint(after));
        Assert.NotEqual(CatalogRules.Fingerprint([before]), CatalogRules.Fingerprint([after]));
    }

    [Theory]
    [InlineData("сколько сырья на складе", "procurement.StockBalances")]
    [InlineData("продажи по регионам за месяц", "linko.Orders")]
    [InlineData("xodimlar kechikishi", "hr.TimesheetDays")]
    [InlineData("какие поставщики срывают сроки", "procurement.Suppliers")]
    [InlineData("техкарты и нормы расхода", "production.Recipes")]
    public void Search_finds_tables_by_business_words(string question, string expected)
    {
        var model = new CatalogModel(CatalogKnowledge.Tables.Select(k =>
        {
            var (schema, name) = (k.Target.Split('.')[0], k.Target.Split('.')[1]);
            var policy = CatalogRules.Policy(schema, name);
            return new CatalogTableInfo(schema, name, "table", policy.Module, k.Title, k.Description, "knowledge", 100, true, 0, policy.Access,
                policy.RequiredPermission, policy.ScopeRule, null, "Id", DateTimeOffset.UtcNow, null, []);
        }).ToList(), [], DateTimeOffset.UtcNow);

        var hits = CatalogSearch.Search(model, question, limit: 3);
        Assert.Contains(hits, h => h.Table.FullName == expected);
    }

    internal static SchemaTable Table(string schema, string name, (string Name, string Type)[] columns, SchemaForeignKey[]? fks = null) =>
        new(schema, name, "table", 10, 8192, null,
            columns.Select((c, i) => new SchemaColumn(c.Name, i + 1, c.Type, c.Name != "Id", null)).ToList(),
            columns.Any(c => c.Name == "Id") ? ["Id"] : [], fks ?? [], new HashSet<string> { "Id" });
}
