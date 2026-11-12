using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Api.Auth;
using OneBase.Api.Controllers;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Access;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Security;
using OneBase.Domain.Identity;
using OneBase.Domain.Sales;
using OneBase.Infrastructure.Persistence;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>
/// Изоляция данных «Продаж» в API по охвату пользователя (PostgreSQL, ONEBASE_TEST_POSTGRES). Сентябрь 2026 из общей базы теста: Хоразм —
/// ТП 51 (магазин 1001), Бухоро — ТП 52 (магазин 1002). Направление «Хорезм» у РМ 1, «Бухара» — у РМ 2; СВР — Бухоро; ТП — 51. Чужой регион,
/// направление, ТП и магазин — 404 даже по прямому id; списки — только свои люди; страницы страны — 403; выгрузки — по праву.
/// </summary>
public class SalesScopeApiDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly SalesController.PeriodQuery September = new(2026, 9);

    private sealed record Org(Guid DirKh, Guid DirBu, Guid Rm1, Guid Rm2, Guid Sv, Guid Tp, Guid Director, Guid Admin);

    private Org? _org;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private async Task<Org> OrgAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_org is not null)
            {
                return _org;
            }

            // xUnit создаёт класс заново на каждый тест: свои имена на каждую подготовку; регионы переходят к направлениям этого теста.
            var s = Guid.NewGuid().ToString("N")[..6];
            using var scope = fixture.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var kh = new SalesDirection { Name = $"Хорезм РМ {s}", ManagerName = "РМ 1" };
            var bu = new SalesDirection { Name = $"Бухара РМ {s}", ManagerName = "РМ 2" };
            db.SalesDirections.AddRange(kh, bu);
            (await db.SalesRegions.SingleAsync(r => r.Id == fixture.KhorazmId)).Direction = kh;
            (await db.SalesRegions.SingleAsync(r => r.Id == fixture.BukharaId)).Direction = bu;
            foreach (var (id, name) in new[] { (51L, "Азиз"), (52L, "Бобур") })
            {
                if (!await db.LinkoUsers.AnyAsync(u => u.Id == id))
                {
                    db.LinkoUsers.Add(new LinkoUser { Id = id, FirstName = name, SecondName = "ТП", IsActive = true, JobName = "Агент" });
                }
            }

            var users = new[] { "s-rm1", "s-rm2", "s-sv", "s-tp", "s-director", "s-admin" }.Select(l => new User { Login = $"{l}-{s}", FullName = $"{l} {s}" }).ToArray();
            // ТП 51 закреплён за одним пользователем: прежнее закрепление (подготовка другого теста) снимается.
            await db.SalesUserScopes.Where(x => x.AgentId == 51).ExecuteDeleteAsync();
            db.Users.AddRange(users);
            await db.SaveChangesAsync();
            scope.ServiceProvider.GetRequiredService<SalesCacheSignal>().Invalidate();
            _org = new Org(kh.Id, bu.Id, users[0].Id, users[1].Id, users[2].Id, users[3].Id, users[4].Id, users[5].Id);

            var admin = scope.ServiceProvider.GetRequiredService<SalesScopeAdmin>();
            await admin.SetAsync(_org.Admin, _org.Rm1, new SalesScopeInput(SalesScopeLevel.Rm, [kh.Id], null, null), default);
            await admin.SetAsync(_org.Admin, _org.Rm2, new SalesScopeInput(SalesScopeLevel.Rm, [bu.Id], null, null), default);
            await admin.SetAsync(_org.Admin, _org.Sv, new SalesScopeInput(SalesScopeLevel.Supervisor, null, [fixture.BukharaId], null), default);
            await admin.SetAsync(_org.Admin, _org.Tp, new SalesScopeInput(SalesScopeLevel.Agent, null, null, 51), default);
            return _org;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static ClaimsPrincipal Principal(Guid user, IEnumerable<string>? permissions = null)
    {
        var claims = new List<Claim> { new(OneBaseClaims.UserId, user.ToString()) };
        claims.AddRange((permissions ?? [Permissions.SalesRead]).Select(p => new Claim(OneBaseClaims.Permission, p)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    /// <summary>Контроллер от имени пользователя (свой экземпляр на вызов — охват кэшируется в контроллере на запрос).</summary>
    private async Task<T> As<T>(Guid user, Func<SalesController, Task<T>> act)
    {
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var controller = new SalesController(sp.GetRequiredService<SalesDataLoader>(), sp.GetRequiredService<SalesAccess>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Principal(user), RequestServices = sp } },
        };
        return await act(controller);
    }

    private static bool NotFound<T>(ActionResult<T> r) => r.Result is NotFoundResult or NotFoundObjectResult;
    private static T Ok<T>(ActionResult<T> r) => r.Value ?? throw new Xunit.Sdk.XunitException($"ожидался ответ, а не {r.Result?.GetType().Name}");

    [PostgresFact]
    public async Task Rm_sees_only_own_direction_regions_agents_and_stores_even_by_direct_id()
    {
        var org = await OrgAsync();
        Ok(await As(org.Rm1, c => c.Region(fixture.KhorazmId, September, null, null)));
        Assert.True(NotFound(await As(org.Rm1, c => c.Region(fixture.BukharaId, September, null, null))));      // регион РМ 2
        Ok(await As(org.Rm1, c => c.Direction(org.DirKh.ToString(), September, null, null)));
        Assert.True(NotFound(await As(org.Rm1, c => c.Direction(org.DirBu.ToString(), September, null, null))));   // направление РМ 2
        Assert.True(NotFound(await As(org.Rm2, c => c.Region(fixture.KhorazmId, September, null, null))));      // и наоборот

        var agents = await As(org.Rm1, c => c.Agents(September, default));
        Assert.Contains(agents.Agents, a => a.AgentId == 51);
        Assert.DoesNotContain(agents.Agents, a => a.AgentId == 52);
        Assert.All(agents.Agents, a => Assert.Equal(fixture.KhorazmId.ToString(), a.RegionId));
        Assert.DoesNotContain(agents.Regions, r => r.Id == fixture.BukharaId.ToString());
        Ok(await As(org.Rm1, c => c.Agent(51, September, default)));
        Assert.True(NotFound(await As(org.Rm1, c => c.Agent(52, September, default))));                          // ТП РМ 2

        Ok(await As(org.Rm1, c => c.Store(1001, September, null, default)));
        Assert.True(NotFound(await As(org.Rm1, c => c.Store(1002, September, null, default, c.HttpContext.RequestServices.GetRequiredService<OneBase.Application.Abstractions.IAppDbContext>()))));                  // магазин Бухоро

        // Ассортимент, категория, товар: своё — да; чужой регион, экспорт «Завода» — 404; без параметров — своё направление.
        var assortment = Ok(await As(org.Rm1, c => c.Assortment(September, null, null, default)));
        Assert.DoesNotContain(assortment.Regions, r => r.Id == fixture.BukharaId.ToString());
        Assert.True(NotFound(await As(org.Rm1, c => c.Assortment(September, null, fixture.BukharaId, default))));
        Assert.True(NotFound(await As(org.Rm1, c => c.Assortment(September, org.DirBu.ToString(), null, default))));
        Assert.True(NotFound(await As(org.Rm1, c => c.Product(100, September, null, fixture.BukharaId, null, false, default))));
        Assert.True(NotFound(await As(org.Rm1, c => c.Product(100, September, null, null, 52, false, default))));
        Assert.True(NotFound(await As(org.Rm1, c => c.Product(100, September, null, null, null, true, default))));
        var product = Ok(await As(org.Rm1, c => c.Product(100, September, null, null, null, false, default)));
        Assert.Equal(900, product.FactKg); // только Хоразм, без 500 кг Бухоро
    }

    [PostgresFact]
    public async Task Supervisor_sees_own_region_team_and_agent_only_himself()
    {
        var org = await OrgAsync();
        Ok(await As(org.Sv, c => c.Region(fixture.BukharaId, September, null, null)));
        Assert.True(NotFound(await As(org.Sv, c => c.Region(fixture.KhorazmId, September, null, null))));
        Assert.True(NotFound(await As(org.Sv, c => c.Direction(org.DirBu.ToString(), September, null, null)))); // направление — уровень РМ
        Ok(await As(org.Sv, c => c.Agent(52, September, default)));
        Assert.True(NotFound(await As(org.Sv, c => c.Agent(51, September, default))));                           // ТП другого СВР
        var team = await As(org.Sv, c => c.Agents(September, default));
        Assert.Equal(new[] { 52L }, team.Agents.Select(a => a.AgentId).ToArray());

        // ТП: только он — карточка, свой магазин (своими продажами); чужой ТП, чужой магазин, регион целиком — 404.
        Ok(await As(org.Tp, c => c.Agent(51, September, default)));
        Assert.True(NotFound(await As(org.Tp, c => c.Agent(52, September, default))));
        Assert.True(NotFound(await As(org.Tp, c => c.Region(fixture.KhorazmId, September, null, null))));
        var store = Ok(await As(org.Tp, c => c.Store(1001, September, 52, default, c.HttpContext.RequestServices.GetRequiredService<OneBase.Application.Abstractions.IAppDbContext>())));  // agent=52 в запросе не помогает — только свои продажи
        Assert.Equal(900, store.FactKg);
        Assert.True(NotFound(await As(org.Tp, c => c.Store(1002, September, null, default, c.HttpContext.RequestServices.GetRequiredService<OneBase.Application.Abstractions.IAppDbContext>()))));
        var own = await As(org.Tp, c => c.Agents(September, default));
        Assert.Equal(new[] { 51L }, own.Agents.Select(a => a.AgentId).ToArray());
        Assert.True(NotFound(await As(org.Tp, c => c.Assortment(September, null, fixture.KhorazmId, default))));
        Assert.Equal(900, Ok(await As(org.Tp, c => c.Product(100, September, null, null, null, false, default))).FactKg); // по себе
        Assert.True(NotFound(await As(org.Tp, c => c.Product(100, September, null, null, 52, false, default))));
    }

    /// <summary>
    /// «Мои регионы»: СВР с двумя регионами — оба одним экраном (своя страница), ассортимент, категория и товар без выбора — по обоим, по одному —
    /// по выбранному. СВР с одним регионом — его регион; вся организация и ТП — 404.
    /// </summary>
    [PostgresFact]
    public async Task Supervisor_with_several_regions_sees_all_of_them_on_my_regions()
    {
        var org = await OrgAsync();
        Guid sv2;
        using (var scope = fixture.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<OneBaseDbContext>();
            var user = db.Users.Add(new User { Login = $"s-sv2-{Guid.NewGuid():N}"[..20], FullName = "s-sv2" }).Entity;
            await db.SaveChangesAsync();
            sv2 = user.Id;
            var admin = sp.GetRequiredService<SalesScopeAdmin>();
            await admin.SetAsync(org.Admin, sv2, new SalesScopeInput(SalesScopeLevel.Supervisor, null, [fixture.KhorazmId, fixture.BukharaId], null), default);
            var access = sp.GetRequiredService<SalesAccess>();
            Assert.Equal("/sales/my", await admin.HomeAsync(await access.ScopeAsync(sv2, default), default));
            Assert.Equal($"/sales/regions/{fixture.BukharaId}", await admin.HomeAsync(await access.ScopeAsync(org.Sv, default), default));
        }

        var my = Ok(await As(sv2, c => c.MyRegions(September, null, null, "kg", null, default)));
        Assert.Equal(new[] { fixture.KhorazmId, fixture.BukharaId }.Select(x => x.ToString()).Order(), my.Cards.Select(x => x.Id).Order());
        var kh = Ok(await As(sv2, c => c.Region(fixture.KhorazmId, September, null, null)));
        var bu = Ok(await As(sv2, c => c.Region(fixture.BukharaId, September, null, null)));
        Assert.Equal(kh.Kpi.FactKg + bu.Kpi.FactKg, my.Kpi.FactKg);

        var assortment = Ok(await As(sv2, c => c.Assortment(September, null, null, default)));
        Assert.Equal("Мои регионы", assortment.ScopeName);
        Assert.Contains(assortment.Regions, r => r.Id == fixture.KhorazmId.ToString());
        Assert.Contains(assortment.Regions, r => r.Id == fixture.BukharaId.ToString());
        Assert.Equal(1400, Ok(await As(sv2, c => c.Product(100, September, null, null, null, false, default))).FactKg);              // оба региона
        Assert.Equal(900, Ok(await As(sv2, c => c.Product(100, September, null, fixture.KhorazmId, null, false, default))).FactKg); // один выбран
        Assert.True(NotFound(await As(sv2, c => c.Product(100, September, null, null, null, true, default))));                     // экспорт — нет
        Assert.True(NotFound(await As(sv2, c => c.Direction(org.DirKh.ToString(), September, null, null))));                       // направление — уровень РМ

        // Один регион — «Мои регионы» из одного; вся организация и ТП — 404 (у них своя страница).
        Assert.Equal(fixture.BukharaId.ToString(), Assert.Single(Ok(await As(org.Sv, c => c.MyRegions(September, null, null, "kg", null, default))).Cards).Id);
        Assert.Equal(500, Ok(await As(org.Sv, c => c.Product(100, September, null, null, null, false, default))).FactKg);
        Assert.True(NotFound(await As(org.Director, c => c.MyRegions(September, null, null, "kg", null, default))));
        Assert.True(NotFound(await As(org.Tp, c => c.MyRegions(September, null, null, "kg", null, default))));
    }

    [PostgresFact]
    public async Task Lists_and_totals_contain_only_people_of_the_scope_and_the_organization_keeps_everything()
    {
        var org = await OrgAsync();
        var problems = await As(org.Rm1, c => c.Problems(September, null, null, true, default));
        Assert.All(problems.Agents, a => Assert.Equal(fixture.KhorazmId.ToString(), a.RegionId));
        Assert.Null(problems.Fact); // факт республики — только всей организации
        var plans = await As(org.Rm1, c => c.Plans(September, default));
        Assert.All(plans.Agents, a => Assert.NotEqual(fixture.BukharaId.ToString(), a.RegionId));
        var kpi = await As(org.Rm1, c => c.Kpi(September, new OnlySales(), default));
        Assert.All(kpi.Regions, r => Assert.NotEqual(fixture.BukharaId.ToString(), r.Id));
        Assert.DoesNotContain(kpi.RegionOptions, r => r.Id == fixture.BukharaId.ToString());

        // Директор без назначения — вся организация: оба региона, оба ТП, факт республики.
        var all = await As(org.Director, c => c.Agents(September, default));
        Assert.Contains(all.Agents, a => a.AgentId == 51);
        Assert.Contains(all.Agents, a => a.AgentId == 52);
        Ok(await As(org.Director, c => c.Region(fixture.BukharaId, September, null, null)));
        Assert.NotNull((await As(org.Director, c => c.Problems(September, null, null, true, default))).Fact);
        Assert.Equal(1400, Ok(await As(org.Director, c => c.Product(100, September, null, null, null, false, default))).FactKg);
    }

    [PostgresFact]
    public async Task Transfer_moves_access_immediately()
    {
        var org = await OrgAsync();
        using (var scope = fixture.Services.CreateScope())
        {
            var admin = scope.ServiceProvider.GetRequiredService<SalesScopeAdmin>();
            var transferred = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>().Users.Add(new User { Login = $"s-moved-{Guid.NewGuid():N}"[..20], FullName = "s-moved" }).Entity;
            await scope.ServiceProvider.GetRequiredService<OneBaseDbContext>().SaveChangesAsync();
            await admin.SetAsync(org.Admin, transferred.Id, new SalesScopeInput(SalesScopeLevel.Supervisor, null, [fixture.KhorazmId], null), default);
            Ok(await As(transferred.Id, c => c.Region(fixture.KhorazmId, September, null, null)));
            await admin.SetAsync(org.Admin, transferred.Id, new SalesScopeInput(SalesScopeLevel.Supervisor, null, [fixture.BukharaId], null), default);
            Assert.True(NotFound(await As(transferred.Id, c => c.Region(fixture.KhorazmId, September, null, null)))); // прежняя команда закрыта сразу
            Ok(await As(transferred.Id, c => c.Region(fixture.BukharaId, September, null, null)));
        }
    }

    [PostgresFact]
    public async Task Country_pages_are_forbidden_to_a_limited_scope_by_the_filter()
    {
        var org = await OrgAsync();
        async Task<IActionResult?> Run(Guid user)
        {
            using var scope = fixture.Services.CreateScope();
            var http = new DefaultHttpContext { User = Principal(user), RequestServices = scope.ServiceProvider };
            var context = new ActionExecutingContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), [], new Dictionary<string, object?>(), null!);
            var called = false;
            await new SalesOrganizationOnlyAttribute().OnActionExecutionAsync(context, () =>
            {
                called = true;
                return Task.FromResult(new ActionExecutedContext(context, [], null!));
            });
            return called ? null : context.Result;
        }

        foreach (var limited in new[] { org.Rm1, org.Sv, org.Tp })
        {
            var result = Assert.IsType<ObjectResult>(await Run(limited));
            Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        }

        Assert.Null(await Run(org.Director)); // вся организация — действие выполняется
    }

    /// <summary>
    /// AI (консультант, AI-сотрудники, Telegram-агент от имени пользователя, оповещения, «Мой день», кэш ответов) считает по всей стране —
    /// от имени РМ / СВР / ТП он данных продаж и финансов не получает; у всей организации права прежние.
    /// </summary>
    [PostgresFact]
    public async Task Ai_acting_for_a_limited_scope_gets_no_sales_or_finance_data()
    {
        var org = await OrgAsync();
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var role = new Role { Name = $"Продажи-AI {Guid.NewGuid():N}"[..24] };
        foreach (var code in new[] { Permissions.SalesRead, Permissions.FinanceRead, Permissions.AgentsRun, Permissions.SalesExport })
        {
            role.Permissions.Add(new RolePermission { Code = code });
        }

        db.Roles.Add(role);
        foreach (var user in await db.Users.Include(u => u.Roles).Where(u => u.Id == org.Rm1 || u.Id == org.Tp || u.Id == org.Director).ToListAsync())
        {
            user.Roles.Add(new UserRole { Role = role });
        }

        await db.SaveChangesAsync();
        async Task<IReadOnlySet<string>> AiRights(Guid user)
        {
            using var s = fixture.Services.CreateScope();
            return await s.ServiceProvider.GetRequiredService<OneBase.AI.Security.IUserPermissions>().GetAsync(user);
        }

        foreach (var limited in new[] { org.Rm1, org.Tp })
        {
            var rights = await AiRights(limited);
            Assert.DoesNotContain(Permissions.SalesRead, rights);
            Assert.DoesNotContain(Permissions.FinanceRead, rights);
            Assert.DoesNotContain(Permissions.SalesExport, rights);
            Assert.Contains(Permissions.AgentsRun, rights); // консультант остаётся — без данных продаж
        }

        var director = await AiRights(org.Director);
        Assert.Contains(Permissions.SalesRead, director);
        Assert.Contains(Permissions.FinanceRead, director);
    }

    /// <summary>
    /// Ни одно действие «Продаж» не остаётся без решения об охвате: либо «только вся организация», либо проверяет охват само, либо без данных.
    /// Выгрузки Excel — только с правом «Выгрузки Excel» и только всей организации.
    /// </summary>
    [Fact]
    public void Every_sales_action_declares_its_scope_and_exports_need_the_export_right()
    {
        var controllers = new[] { typeof(SalesController), typeof(SalesSetupController), typeof(SalesPnlController), typeof(SalesSyncController) };
        var undecided = new List<string>();
        foreach (var type in controllers)
        {
            var classOrgOnly = type.GetCustomAttribute<SalesOrganizationOnlyAttribute>() is not null;
            foreach (var action in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                         .Where(m => m.GetCustomAttributes().Any(a => a is Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute)))
            {
                var decided = classOrgOnly || action.GetCustomAttribute<SalesOrganizationOnlyAttribute>() is not null
                    || action.GetCustomAttribute<SalesScopedAttribute>() is not null || action.GetCustomAttribute<SalesNoDataAttribute>() is not null;
                if (!decided)
                {
                    undecided.Add($"{type.Name}.{action.Name}");
                }

                var route = action.GetCustomAttributes().OfType<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>().First().Template ?? "";
                if (route.Contains("xlsx") || route.Contains("/act/"))
                {
                    Assert.Contains(action.GetCustomAttributes<HasPermissionAttribute>(), a => a.Policy == PermissionPolicyProvider.Prefix + Permissions.SalesExport);
                    Assert.NotNull(action.GetCustomAttribute<SalesOrganizationOnlyAttribute>());
                }
            }
        }

        Assert.Empty(undecided);
    }

    /// <summary>Права пользователя для «KPI»: только «Продажи», без «Финансов» (деньги скрыты).</summary>
    private sealed class OnlySales : OneBase.AI.Security.IUserPermissions
    {
        public Task<IReadOnlySet<string>> GetAsync(Guid userId, CancellationToken ct) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string> { Permissions.SalesRead });
    }
}
