using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Analytik;
using OneBase.Application.Analytik.Catalog;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Security;
using OneBase.Domain.Identity;
using OneBase.Domain.Sales;
using OneBase.Infrastructure.Persistence;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>
/// Аналитический движок на настоящем PostgreSQL: суммы по регионам, область продаж супервайзера и агента (строки чужих регионов не
/// попадают), отказ без права, журнал запросов, профиль колонки и «всё о записи». Данные фикстуры: сентябрь 2026 — Хоразм 900 кг
/// (заказ 1, агент 51, точка 1001), Бухоро 500 кг (заказ 2, агент 52, точка 1002).
/// </summary>
public sealed class AnalytikQueryDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>, IAsyncLifetime
{
    private Guid _directorId;
    private Guid _supervisorId;
    private Guid _agentId;

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

        var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == "Аналитик продаж (тест)");
        if (role is null)
        {
            role = new Role { Name = "Аналитик продаж (тест)" };
            role.Permissions.Add(new RolePermission { Code = Permissions.AnalytikUse });
            role.Permissions.Add(new RolePermission { Code = Permissions.SalesRead });
            db.Roles.Add(role);
        }

        var directorRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "Директор (тест Аналитика)");
        if (directorRole is null)
        {
            directorRole = new Role { Name = "Директор (тест Аналитика)" };
            foreach (var code in new[] { Permissions.AnalytikUse, Permissions.SalesRead, Permissions.FinanceRead, Permissions.UsersManage })
            {
                directorRole.Permissions.Add(new RolePermission { Code = code });
            }

            db.Roles.Add(directorRole);
        }

        var director = await db.Users.FirstOrDefaultAsync(u => u.Login == "analytik-director") ?? AddUser(db, directorRole, "analytik-director");
        var supervisor = await db.Users.FirstOrDefaultAsync(u => u.Login == "analytik-sv") ?? AddUser(db, role, "analytik-sv");
        var agent = await db.Users.FirstOrDefaultAsync(u => u.Login == "analytik-agent") ?? AddUser(db, role, "analytik-agent");
        await db.SaveChangesAsync();
        _directorId = director.Id;
        _supervisorId = supervisor.Id;
        _agentId = agent.Id;

        if (!await db.SalesUserScopes.AnyAsync(s => s.UserId == _supervisorId))
        {
            db.SalesUserScopes.Add(new SalesUserScope { UserId = _supervisorId, Level = SalesScopeLevel.Supervisor, RegionIds = [fixture.KhorazmId] });
            db.SalesUserScopes.Add(new SalesUserScope { UserId = _agentId, Level = SalesScopeLevel.Agent, AgentId = 51 });
            await db.SaveChangesAsync();
        }

        await scope.ServiceProvider.GetRequiredService<CatalogService>().RefreshAsync("manual", null, true, CancellationToken.None);
    }

    private static User AddUser(OneBaseDbContext db, Role role, string login)
    {
        var user = new User { Login = login, FullName = login };
        user.Roles.Add(new UserRole { Role = role });
        db.Users.Add(user);
        return user;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<QueryOutcome> Run(Guid userId, QueryPlan plan)
    {
        using var scope = fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<QueryEngine>().RunAsync(plan, new QueryCaller(userId, "api"), CancellationToken.None);
    }

    private static readonly QueryPlan KgByRegion = new()
    {
        From = "linko.Orders",
        GroupBy = ["sales.Regions.Name"],
        Measures = [new PlanMeasure("sum", "TotalWeight", "kg")],
        Filters = [new PlanFilter("Status", "in", JsonSerializer.SerializeToElement(new[] { "delivered", "given" }))],
        OrderBy = [new PlanOrder("kg", "desc")],
    };

    private static Dictionary<string, decimal> Kg(QueryOutcome o) =>
        o.Result!.Rows.ToDictionary(r => (string)r[0]!, r => Convert.ToDecimal(r[1]));

    [PostgresFact]
    public async Task Director_sees_all_regions_supervisor_only_own_and_agent_only_own_orders()
    {
        var all = await Run(_directorId, KgByRegion);
        Assert.True(all.Ok, all.Error);
        Assert.Equal(new Dictionary<string, decimal> { ["Хоразм"] = 900m, ["Бухоро"] = 500m }, Kg(all));
        Assert.Null(all.Result!.Scope);

        var supervisor = await Run(_supervisorId, KgByRegion);
        Assert.True(supervisor.Ok, supervisor.Error);
        Assert.Equal(new Dictionary<string, decimal> { ["Хоразм"] = 900m }, Kg(supervisor));
        Assert.Contains("Хоразм", supervisor.Result!.Scope);

        // Сумма без группировки — тоже только свой регион (предикат на базовой таблице, а не фильтр результата).
        var total = await Run(_supervisorId, new QueryPlan { From = "linko.OrderLines", Measures = [new PlanMeasure("sum", "TotalWeight", "kg")] });
        Assert.True(total.Ok, total.Error);
        Assert.Equal(900m, Convert.ToDecimal(total.Result!.Rows[0][0]));

        var agent = await Run(_agentId, new QueryPlan { From = "linko.Orders", Select = [new PlanField("Id"), new PlanField("TotalWeight")] });
        Assert.True(agent.Ok, agent.Error);
        Assert.Single(agent.Result!.Rows);
        Assert.Equal(1L, Convert.ToInt64(agent.Result.Rows[0][0]));

        var regions = await Run(_agentId, new QueryPlan { From = "sales.Regions" });
        Assert.Equal("denied", regions.Status);
    }

    [PostgresFact]
    public async Task Without_analytik_right_nothing_is_returned_and_every_query_is_logged()
    {
        var denied = await Run(fixture.ViewerId, KgByRegion);
        Assert.Equal("denied", denied.Status);
        Assert.Null(denied.Result);

        var secret = await Run(_directorId, new QueryPlan { From = "public.Users", Select = [new PlanField("PasswordHash")] });
        Assert.Equal("denied", secret.Status);

        var ok = await Run(_directorId, KgByRegion);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAnalytikDb>();
        var log = await db.Queries.AsNoTracking().Where(q => q.Id == denied.QueryId || q.Id == secret.QueryId || q.Id == ok.QueryId).ToListAsync();
        Assert.Equal(3, log.Count);
        Assert.Contains(log, q => q.Id == denied.QueryId && q.Status == "denied" && q.UserId == fixture.ViewerId && q.Sql == null);
        var okLog = log.Single(q => q.Id == ok.QueryId);
        Assert.Equal("ok", okLog.Status);
        Assert.Equal(2, okLog.RowCount);
        Assert.Contains("linko.Orders", okLog.Tables);
        Assert.Contains("sales.Regions", okLog.Tables);
        Assert.StartsWith("SELECT", okLog.Sql);
        Assert.Contains("delivered", okLog.Parameters);
        Assert.Equal("onebase", okLog.Tenant);
    }

    [PostgresFact]
    public async Task Profile_and_entity_360_respect_rights()
    {
        using var scope = fixture.Services.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<QueryEngine>();
        var profile = await engine.ProfileAsync("linko.Orders", "Status", new QueryCaller(_directorId, "api"), CancellationToken.None);
        Assert.Equal(2, profile.Rows);
        Assert.Equal(1, profile.Distinct);
        Assert.Equal("delivered", profile.Top.Single().Value);

        var market = await engine.EntityAsync("linko.Markets", "1001", new QueryCaller(_directorId, "api"), CancellationToken.None);
        Assert.Equal("Точка Хоразм", market.Record!["Name"]);
        var orders = market.Related.Single(r => r.Table == "linko.Orders");
        Assert.Equal(1, orders.Count);

        // Точка другого региона для супервайзера Хоразма не существует.
        var foreign = await engine.EntityAsync("linko.Markets", "1002", new QueryCaller(_supervisorId, "api"), CancellationToken.None);
        Assert.Null(foreign.Record);
    }
}
