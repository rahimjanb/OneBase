using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Analytik;
using OneBase.AI.Tools;
using OneBase.Application.Analytik;
using OneBase.Application.Analytik.Catalog;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Security;
using OneBase.Domain.Identity;
using OneBase.Domain.Sales;
using OneBase.Infrastructure.Persistence;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>Текст свободного запроса до PostgreSQL: один SELECT/WITH, комментарии, строки, запрещённые функции и команды.</summary>
public class SqlGuardTests
{
    [Theory]
    [InlineData("select 1;", "select 1")]
    [InlineData("-- выручка\nSELECT 1 -- хвост\n;  ", "SELECT 1")]
    [InlineData("/* вложенный /* комментарий */ */ with x as (select 1 a) select a from x", "with x as (select 1 a) select a from x")]
    [InlineData("select 'drop table; -- не команда' as t", "select 'drop table; -- не команда' as t")]
    [InlineData("select \"DeletedAt\", \"UpdatedAt\" from linko.\"Orders\"", "select \"DeletedAt\", \"UpdatedAt\" from linko.\"Orders\"")]
    [InlineData("(select 1) union all (select 2)", "(select 1) union all (select 2)")]
    public void Accepts_one_read_query_and_strips_comments_and_trailing_semicolon(string sql, string expected) =>
        Assert.Equal(expected, SqlGuard.Clean(sql).Trim());

    [Theory]
    [InlineData("", QueryErrors.Invalid)]
    [InlineData("select 1; select 2", QueryErrors.Invalid)]
    [InlineData("update linko.\"Orders\" set \"Status\" = 'x'", QueryErrors.Invalid)]
    [InlineData("select * into t from linko.\"Orders\"", QueryErrors.Denied)]
    [InlineData("select * from linko.\"Orders\" for update", QueryErrors.Denied)]
    [InlineData("with d as (delete from linko.\"Orders\" returning 1) select * from d", QueryErrors.Denied)]
    [InlineData("select query_to_xml('select * from ai.\"Providers\"', true, false, '')", QueryErrors.Denied)]
    [InlineData("select \"query_to_xml\"('select 1', true, false, '')", QueryErrors.Denied)]
    [InlineData("select pg_catalog.pg_read_file('/etc/passwd')", QueryErrors.Denied)]
    [InlineData("select table_to_xml('ai.\"Providers\"', true, false, '')", QueryErrors.Denied)]
    [InlineData("select current_setting('data_directory')", QueryErrors.Denied)]
    [InlineData("select pg_sleep(100)", QueryErrors.Denied)]
    [InlineData("select $1", QueryErrors.Invalid)]
    [InlineData("select $$x$$", QueryErrors.Invalid)]
    [InlineData("select E'\\' ; select 1'", QueryErrors.Invalid)]
    [InlineData("select U&\"\\0070g_sleep\"(1)", QueryErrors.Invalid)]
    [InlineData("select 1 /* не закрыт", QueryErrors.Invalid)]
    [InlineData("select 'не закрыта", QueryErrors.Invalid)]
    public void Rejects_writes_several_statements_escapes_and_functions_that_read_files_settings_or_other_queries(string sql, string code)
    {
        var ex = Assert.Throws<QueryPlanException>(() => SqlGuard.Clean(sql));
        Assert.Equal(code, ex.Code);
    }

    [Fact]
    public void Plan_text_yields_column_references_with_and_without_alias_and_whole_rows()
    {
        var refs = SqlPlanInspector.ColumnRefs("(to_json(o.*)) AND (\"Orders\".\"MarketId\" = m.\"Id\") AND (\"Id\" = 5) AND (o.\"Name\" = 'u.\"Secret\"')").ToList();
        Assert.Contains((("o", "*")), refs);
        Assert.Contains(("Orders", "MarketId"), refs);
        Assert.Contains(("m", "Id"), refs);
        Assert.Contains(((string?)null, "Id"), refs);
        Assert.DoesNotContain(("u", "Secret"), refs); // внутри строки — не колонка
    }
}

/// <summary>
/// Свободный запрос (analytik_sql) на настоящем PostgreSQL: оконные функции и соединения по продажам фикстуры (Хоразм 900 кг — заказ 1,
/// ТП 51; Бухоро 500 кг — заказ 2, ТП 52), отказы — закрытая и системная таблицы, скрытая колонка, целая строка таблицы со скрытыми
/// колонками, запрещённая функция, данные продаж при области региона; ошибка PostgreSQL — текстом для исправления; журнал; инструмент.
/// </summary>
public sealed class AnalytikSqlDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>, IAsyncLifetime
{
    private Guid _directorId;
    private Guid _supervisorId;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(DataManagerDb.Variable)))
        {
            return;
        }

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        if (!await db.LinkoMarkets.AnyAsync(m => m.Id == 1001))
        {
            db.LinkoMarkets.AddRange(new LinkoMarket { Id = 1001, Name = "Точка Хоразм", BranchId = 5 }, new LinkoMarket { Id = 1002, Name = "Точка Бухоро", BranchId = 7 });
        }

        Role RoleOf(string name, params string[] codes)
        {
            var role = new Role { Name = name };
            foreach (var code in codes)
            {
                role.Permissions.Add(new RolePermission { Code = code });
            }

            db.Roles.Add(role);
            return role;
        }

        var director = await db.Users.FirstOrDefaultAsync(u => u.Login == "sql-director");
        var supervisor = await db.Users.FirstOrDefaultAsync(u => u.Login == "sql-sv");
        if (director is null || supervisor is null)
        {
            director = AddUser(db, RoleOf("Директор (тест SQL)", Permissions.AnalytikUse, Permissions.SalesRead, Permissions.UsersManage), "sql-director");
            supervisor = AddUser(db, RoleOf("СВ (тест SQL)", Permissions.AnalytikUse, Permissions.SalesRead), "sql-sv");
            await db.SaveChangesAsync();
            db.SalesUserScopes.Add(new SalesUserScope { UserId = supervisor.Id, Level = SalesScopeLevel.Supervisor, RegionIds = [fixture.KhorazmId] });
        }

        await db.SaveChangesAsync();
        _directorId = director.Id;
        _supervisorId = supervisor.Id;
        await scope.ServiceProvider.GetRequiredService<CatalogService>().RefreshAsync("manual", null, true, CancellationToken.None);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static User AddUser(OneBaseDbContext db, Role role, string login)
    {
        var user = new User { Login = login, FullName = login };
        user.Roles.Add(new UserRole { Role = role });
        db.Users.Add(user);
        return user;
    }

    private async Task<SqlOutcome> Run(Guid userId, string sql, int? rows = null)
    {
        using var scope = fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SqlQueryService>().RunAsync(sql, rows, new QueryCaller(userId, "api"), CancellationToken.None);
    }

    [PostgresFact]
    public async Task Director_runs_window_functions_joins_and_aggregates_over_sales()
    {
        var kg = await Run(_directorId, """
            -- кг по ТП и доля в итоге
            with o as (
              select "AgentId", sum("TotalWeight") kg from linko."Orders"
              where "Status" in ('delivered', 'given') and lower(coalesce("BranchName", '')) not in ('завод', 'к к мерч')
              group by 1)
            select "AgentId", kg, round(kg / sum(kg) over () * 100, 1) as share_pct,
                   rank() over (order by kg desc) as place
            from o order by place;
            """);
        Assert.True(kg.Ok, kg.Error);
        Assert.Equal(["AgentId", "kg", "share_pct", "place"], kg.Result!.Columns.Select(c => c.Name));
        Assert.Equal(2, kg.Result.Rows.Count);
        Assert.Equal((51L, 900m, 64.3m, 1L), (Convert.ToInt64(kg.Result.Rows[0][0]), Convert.ToDecimal(kg.Result.Rows[0][1]),
            Convert.ToDecimal(kg.Result.Rows[0][2]), Convert.ToInt64(kg.Result.Rows[0][3])));
        Assert.Contains("linko.Orders", kg.Result.Tables);

        var chain = await Run(_directorId, """
            select o."Id", m."Name", r."Name" as region, o."CreatedDate" - lag(o."CreatedDate") over (order by o."Id") as days_since_previous
            from linko."Orders" o
            join linko."Markets" m on m."Id" = o."MarketId"
            join sales."Regions" r on r."LinkoBranchId" = m."BranchId"
            order by o."Id"
            """);
        Assert.True(chain.Ok, chain.Error);
        Assert.Equal(["Точка Хоразм", "Точка Бухоро"], chain.Result!.Rows.Select(r => (string)r[1]!));
        Assert.Equal(["Хоразм", "Бухоро"], chain.Result.Rows.Select(r => (string)r[2]!));
        Assert.Null(chain.Result.Rows[0][3]);
        Assert.Equal(0, Convert.ToInt32(chain.Result.Rows[1][3]));

        // Предел строк: has_more, а не молча обрезанный ответ.
        var limited = await Run(_directorId, "select \"Id\" from linko.\"Orders\" order by 1", rows: 1);
        Assert.True(limited.Ok, limited.Error);
        Assert.True(limited.Result!.HasMore);
        Assert.Single(limited.Result.Rows);

        using var scope = fixture.Services.CreateScope();
        var log = await scope.ServiceProvider.GetRequiredService<OneBaseDbContext>().Set<OneBase.Domain.Analytik.AnalytikQuery>()
            .Where(q => q.UserId == _directorId && q.Status == "ok").OrderByDescending(q => q.CreatedAt).FirstAsync();
        Assert.Contains("onebase_sql", log.Sql);
        Assert.Contains("linko.Orders", log.Tables);
    }

    [PostgresFact]
    public async Task Closed_and_system_tables_hidden_columns_whole_rows_and_unknown_functions_are_refused()
    {
        async Task Refused(string sql, string fragment, bool access = false)
        {
            var outcome = await Run(_directorId, sql);
            Assert.False(outcome.Ok, sql);
            Assert.Equal(QueryErrors.Denied, outcome.Status);
            Assert.Contains(fragment, outcome.Error);
            // По правам — только закрытая таблица; скрытая колонка, системная таблица, функция исправляются в самом запросе.
            Assert.Equal(access, outcome.Access);
        }

        await Refused("select * from ai.\"Providers\"", "закрыта", access: true);
        await Refused("select rolname from pg_catalog.pg_authid", "не открыта");
        await Refused("select \"PasswordHash\" from public.\"Users\"", "PasswordHash");
        await Refused("select \"Login\" from public.\"Users\" where \"PasswordHash\" like 'A%'", "PasswordHash");
        await Refused("select to_json(u) from public.\"Users\" u", "Целая строка");
        await Refused("select x.* from (select * from public.\"Users\") x", "скрыта");
        await Refused("select \"Username\" from linko.\"Users\"", "Username");
        await Refused("select md5(\"Username\") from linko.\"Users\"", "Username");
        // Отказ по колонке подсказывает, чем её заменить: видимые колонки таблицы.
        await Refused("select \"Id\", \"Username\" from linko.\"Users\"", "Видимые колонки linko.Users:");
        Assert.Contains("FirstName", (await Run(_directorId, "select \"Username\" from linko.\"Users\"")).Error);
        await Refused("select query_to_xml('select 1', true, false, '')", "query_to_xml");
        await Refused("select inet_server_port()", "inet_server_port");

        // Без скрытых колонок — можно: те же таблицы, видимые колонки.
        var users = await Run(_directorId, "select count(*) as users, count(distinct \"Login\") as logins from public.\"Users\"");
        Assert.True(users.Ok, users.Error);

        // Ошибка PostgreSQL — текстом для исправления запроса (без данных).
        var typo = await Run(_directorId, "select \"CreatedDat\" from linko.\"Orders\"");
        Assert.Equal(QueryErrors.Invalid, typo.Status);
        Assert.Contains("CreatedDat", typo.Error);
    }

    [PostgresFact]
    public async Task Supervisor_cannot_read_sales_rows_freely_but_can_read_reference_tables()
    {
        var orders = await Run(_supervisorId, "select count(*) from linko.\"Orders\"");
        Assert.Equal(QueryErrors.Denied, orders.Status);
        Assert.Contains("analytik_query", orders.Error);
        Assert.True(orders.Access);

        var lines = await Run(_supervisorId, "select sum(l.\"TotalWeight\") from linko.\"OrderLines\" l");
        Assert.Equal(QueryErrors.Denied, lines.Status);

        var products = await Run(_supervisorId, "select \"Name\" from linko.\"Products\" order by \"Id\"");
        Assert.True(products.Ok, products.Error);
        Assert.Contains("Кекс 1 кг", products.Result!.Rows.Select(r => (string)r[0]!));
    }

    [PostgresFact]
    public async Task Tool_runs_query_describes_tables_and_tells_access_refusals_from_fixable_mistakes()
    {
        using var scope = fixture.Services.CreateScope();
        var tool = scope.ServiceProvider.GetServices<ITool>().Single(t => t.Name == SqlTool.ToolName);
        Assert.Equal("sales.stats", tool.Source);
        var context = new ToolContext("sales", _directorId, 0);

        var ok = await tool.ExecuteAsync(context, JsonSerializer.SerializeToElement(new { sql = "select \"AgentId\", sum(\"TotalWeight\") kg from linko.\"Orders\" group by 1 order by 1" }));
        Assert.True(ok.Success, ok.Content);
        using (var json = JsonDocument.Parse(ok.Content))
        {
            Assert.Equal(2, json.RootElement.GetProperty("rowCount").GetInt32());
            Assert.Equal(900m, json.RootElement.GetProperty("rows")[0][1].GetDecimal());
        }

        var describe = await tool.ExecuteAsync(context, JsonSerializer.SerializeToElement(new { describe = new[] { "linko.Users", "ai.Providers" } }));
        Assert.True(describe.Success, describe.Content);
        Assert.Contains("FirstName", describe.Content);
        Assert.Contains("Username", describe.Content); // в списке скрытых, а не в колонках
        Assert.Contains("закрыта", describe.Content);

        var denied = await tool.ExecuteAsync(context, JsonSerializer.SerializeToElement(new { sql = "select * from ai.\"Providers\"" }));
        Assert.False(denied.Success);
        Assert.Contains("закрыта", denied.Content);
        Assert.Equal(ToolFailures.Forbidden, denied.ErrorKind);

        // Скрытая колонка — не «нет прав»: ошибка аргументов, которую сотрудник исправит (в ограничения ответа она не попадает).
        var hidden = await tool.ExecuteAsync(context, JsonSerializer.SerializeToElement(new { sql = "select \"Username\" from linko.\"Users\"" }));
        Assert.False(hidden.Success);
        Assert.Equal(ToolFailures.InvalidArguments, hidden.ErrorKind);
        Assert.Contains("FirstName", hidden.Content);
        Assert.Equal(6, tool.MaxFailures);
    }

    [PostgresFact]
    public async Task Long_free_query_is_limited_by_its_own_timeout_not_by_the_connection_default()
    {
        // Свободный запрос ждёт до SqlTimeoutSeconds (30 с) при QueryTimeoutSeconds 15: клиентский предел соединения Npgsql раньше обрывал его
        // на QueryTimeoutSeconds + 10 — «сбой базы» вместо результата. Здесь предел соединения 10 с, запросу разрешено 15 с, он идёт 11 с.
        var type = Type.GetType("OneBase.Infrastructure.Analytik.AnalytikSqlRunner, OneBase.Infrastructure", throwOnError: true)!;
        var logger = typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger<>).MakeGenericType(type).GetField("Instance")!.GetValue(null);
        var runner = (IAnalytikSql)Activator.CreateInstance(type,
            fixture.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>(),
            new AnalytikOptions { QueryTimeoutSeconds = 0, MaxConcurrentQueries = 1 }, logger)!;
        try
        {
            var slow = await runner.QueryAsync("SELECT pg_sleep(11), 1 AS done", [], new SqlLimits(1, 15), CancellationToken.None);
            Assert.Equal(1, Convert.ToInt32(slow.Rows[0][1]));
            await Assert.ThrowsAsync<AnalytikTimeoutException>(() => runner.QueryAsync("SELECT pg_sleep(5)", [], new SqlLimits(1, 1), CancellationToken.None));
        }
        finally
        {
            (runner as IDisposable)?.Dispose();
        }
    }
}
