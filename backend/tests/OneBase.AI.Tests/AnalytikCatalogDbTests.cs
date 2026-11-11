using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OneBase.Application.Analytik;
using OneBase.Application.Analytik.Catalog;
using OneBase.Domain.Sales;
using OneBase.Infrastructure.Persistence;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>
/// Data Intelligence Engine на настоящем PostgreSQL (временная база): обнаружение всех схем и таблиц, внешних ключей, новых и изменённых
/// таблиц, проверка связей данными; выполнение только на чтение и лимит времени.
/// </summary>
public sealed class AnalytikCatalogDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private async Task<T> InScope<T>(Func<IServiceProvider, Task<T>> run)
    {
        using var scope = fixture.Services.CreateScope();
        return await run(scope.ServiceProvider);
    }

    private async Task ExecAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(Connection());
        await connection.OpenAsync();
        await new NpgsqlCommand(sql, connection).ExecuteNonQueryAsync();
    }

    private async Task<long> ScalarAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(Connection());
        await connection.OpenAsync();
        return Convert.ToInt64(await new NpgsqlCommand(sql, connection).ExecuteScalarAsync());
    }

    private string Connection()
    {
        using var scope = fixture.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<OneBaseDbContext>().Database.GetConnectionString()!;
    }

    [PostgresFact]
    public async Task Discovers_every_table_and_foreign_key_of_the_database()
    {
        var result = await InScope(sp => sp.GetRequiredService<CatalogService>().RefreshAsync("manual", null, true, CancellationToken.None));

        var tables = await ScalarAsync("""
            SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relkind IN ('r','p','v','m') AND NOT c.relispartition AND n.nspname NOT IN ('pg_catalog','information_schema')
              AND n.nspname NOT LIKE 'pg\_toast%' AND n.nspname NOT LIKE 'pg\_temp%'
            """);
        var fks = await ScalarAsync("SELECT count(*) FROM pg_constraint WHERE contype = 'f'");
        Assert.Equal(tables, result.Tables);
        Assert.True(tables > 200, $"в базе OneBase после миграций больше 200 таблиц, найдено {tables}");

        var model = await InScope(sp => sp.GetRequiredService<CatalogService>().ModelAsync(CancellationToken.None));
        Assert.Equal(tables, model.Tables.Count);
        Assert.Equal(fks, model.Relations.Count(r => r.Kind == RelationKinds.ForeignKey));
        foreach (var schema in new[] { "linko", "sales", "procurement", "production", "hr", "work", "ai", "public", "analytik" })
        {
            Assert.Contains(model.Tables, t => t.Schema == schema);
        }

        var orders = model.Find("linko.Orders")!;
        Assert.Equal("Заказы (вторичка)", orders.Title);
        Assert.Equal("AcceptedDate", orders.DateColumn);
        Assert.Equal("sales", orders.Module);
        Assert.Equal(ColumnRoles.Money, orders.Column("TotalPrice")!.Role);
        Assert.True(model.Find("ai.Providers")!.Closed);
        Assert.Equal(Sensitivity.Secret, model.Find("public.Users")!.Column("PasswordHash")!.Sensitivity);
        Assert.Equal(Sensitivity.Personal, model.Find("hr.Employees")!.Column("Phone")!.Sensitivity);
    }

    [PostgresFact]
    public async Task Detects_new_changed_and_removed_tables_without_programming()
    {
        await InScope(sp => sp.GetRequiredService<CatalogService>().RefreshAsync("manual", null, false, CancellationToken.None));
        var again = await InScope(sp => sp.GetRequiredService<CatalogService>().RefreshAsync("schedule", null, false, CancellationToken.None));
        Assert.True(again.Unchanged);
        Assert.Equal(0, again.Added);

        var schema = $"zz_new_{Guid.NewGuid():N}"[..14];
        try
        {
            await ExecAsync($"CREATE SCHEMA {schema}; CREATE TABLE {schema}.\"Expenses\" (\"Id\" uuid PRIMARY KEY, \"Amount\" numeric, \"SpentOn\" date);" +
                            $" INSERT INTO {schema}.\"Expenses\" VALUES (gen_random_uuid(), 100, '2026-11-01');");
            var added = await InScope(sp => sp.GetRequiredService<CatalogService>().RefreshAsync("schedule", null, false, CancellationToken.None));
            Assert.False(added.Unchanged);
            Assert.Equal(1, added.Added);
            Assert.Contains($"+ {schema}.Expenses", added.Changes);

            var model = await InScope(sp => sp.GetRequiredService<CatalogService>().ModelAsync(CancellationToken.None));
            var table = model.Find($"{schema}.Expenses")!;
            // Новая схема видна в каталоге, но закрыта для движка, пока администратор не опишет её доступ.
            Assert.Equal("other", table.Module);
            Assert.True(table.Closed);
            Assert.Equal("SpentOn", table.DateColumn);
            Assert.Equal(1, table.Rows);

            await ExecAsync($"ALTER TABLE {schema}.\"Expenses\" ADD COLUMN \"Kg\" numeric");
            var changed = await InScope(sp => sp.GetRequiredService<CatalogService>().RefreshAsync("schedule", null, false, CancellationToken.None));
            Assert.Equal(1, changed.Changed);
            Assert.Contains(changed.Changes, c => c.Contains("Kg", StringComparison.Ordinal));

            await ExecAsync($"DROP SCHEMA {schema} CASCADE");
            var removed = await InScope(sp => sp.GetRequiredService<CatalogService>().RefreshAsync("schedule", null, false, CancellationToken.None));
            Assert.Equal(1, removed.Removed);
            model = await InScope(sp => sp.GetRequiredService<CatalogService>().ModelAsync(CancellationToken.None));
            Assert.Null(model.Find($"{schema}.Expenses"));
        }
        finally
        {
            await ExecAsync($"DROP SCHEMA IF EXISTS {schema} CASCADE");
        }
    }

    [PostgresFact]
    public async Task Inferred_relations_are_verified_by_data()
    {
        // Заказы фикстуры ссылаются на точки 1001 и 1002 и агентов 51, 52; точек добавим, агентов в linko.Users нет.
        await InScope(async sp =>
        {
            var db = sp.GetRequiredService<OneBaseDbContext>();
            if (!await db.LinkoMarkets.AnyAsync(m => m.Id == 1001))
            {
                db.LinkoMarkets.AddRange(new LinkoMarket { Id = 1001, Name = "Точка 1" }, new LinkoMarket { Id = 1002, Name = "Точка 2" });
                await db.SaveChangesAsync();
            }

            return 0;
        });

        await InScope(sp => sp.GetRequiredService<CatalogService>().RefreshAsync("manual", null, true, CancellationToken.None));
        var model = await InScope(sp => sp.GetRequiredService<CatalogService>().ModelAsync(CancellationToken.None));
        CatalogRelationInfo Relation(string from) => model.Relations.Single(r => $"{r.From}.{r.FromColumn}" == from);

        var market = Relation("linko.Orders.MarketId");
        Assert.Equal(RelationKinds.Inferred, market.Kind);
        Assert.Equal(RelationStatus.Confirmed, market.Status);
        Assert.Equal(1m, market.MatchRatio);
        Assert.True(market.Joinable);

        var type = Relation("linko.Products.TypeId");
        Assert.Equal("linko.ProductTypes", type.To);
        Assert.Equal(RelationStatus.Confirmed, type.Status);

        var branch = Relation("linko.Orders.BranchId");
        Assert.Equal("sales.Regions", branch.To);
        Assert.Equal(RelationStatus.Confirmed, branch.Status);

        // Агентов 51 и 52 среди сотрудников Linko нет — связь не подтверждена данными и для соединений не используется.
        var agent = Relation("linko.Orders.AgentId");
        Assert.Equal(RelationStatus.Rejected, agent.Status);
        Assert.False(agent.Joinable);
    }

    [PostgresFact]
    public async Task Engine_connection_is_read_only_and_time_limited()
    {
        var sql = fixture.Services.GetRequiredService<IAnalytikSql>();
        var read = await sql.QueryAsync("SELECT count(*) FROM linko.\"Orders\"", [], new SqlLimits(10, 5), CancellationToken.None);
        Assert.Equal(2L, Convert.ToInt64(read.Rows[0][0]));

        // Каждая запись отклоняется самим PostgreSQL: транзакция READ ONLY (25006 read_only_sql_transaction).
        foreach (var write in new[]
                 {
                     "INSERT INTO linko.\"Currencies\" (\"Id\", \"Name\") VALUES (999, 'x')",
                     "UPDATE linko.\"Orders\" SET \"TotalPrice\" = 0",
                     "DELETE FROM linko.\"Orders\"",
                     "DROP TABLE linko.\"Orders\"",
                     "TRUNCATE linko.\"Orders\"",
                     "ALTER TABLE linko.\"Orders\" ADD COLUMN \"Hack\" int",
                     "CREATE TABLE public.zz_hack (id int)",
                 })
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => sql.QueryAsync(write, [], new SqlLimits(10, 5), CancellationToken.None));
            Assert.Equal("25006", error.SqlState);
        }

        // Несколько команд в одном тексте — данные всё равно не меняются.
        try
        {
            await sql.QueryAsync("SELECT 1; DELETE FROM linko.\"Orders\"", [], new SqlLimits(10, 5), CancellationToken.None);
        }
        catch (PostgresException)
        {
        }

        Assert.Equal(2L, await ScalarAsync("SELECT count(*) FROM linko.\"Orders\""));
        Assert.Equal(0L, await ScalarAsync("SELECT count(*) FROM linko.\"Currencies\" WHERE \"Id\" = 999"));
        Assert.Equal(0L, await ScalarAsync("SELECT count(*) FROM information_schema.tables WHERE table_name = 'zz_hack'"));
        await Assert.ThrowsAsync<AnalytikTimeoutException>(() => sql.QueryAsync("SELECT pg_sleep(3)", [], new SqlLimits(1, 1), CancellationToken.None));
    }
}
