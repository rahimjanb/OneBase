using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Agents;
using OneBase.Api.Auth;
using OneBase.Api.Controllers;
using OneBase.Application.Security;

namespace OneBase.AI.Tests;

/// <summary>
/// «AI Dashboard» (GET /api/ai/agents): агентов отделов, к данным которых у пользователя нет права, в списке нет совсем — а не «нет доступа»
/// (PostgreSQL, ONEBASE_TEST_POSTGRES).
/// </summary>
public class AiAgentsListDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private sealed class Fixed(params string[] rights) : OneBase.AI.Security.IUserPermissions
    {
        public Task<IReadOnlySet<string>> GetAsync(Guid userId, CancellationToken ct) => Task.FromResult<IReadOnlySet<string>>(rights.ToHashSet());
    }

    private async Task<List<(string Code, bool Available)>> ListAsync(params string[] rights)
    {
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var controller = ActivatorUtilities.CreateInstance<AiAgentsController>(sp, new Fixed(rights));
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(OneBaseClaims.UserId, Guid.NewGuid().ToString())], "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user, RequestServices = sp } };
        var ok = Assert.IsType<OkObjectResult>(await controller.List(null, default));
        return ((IEnumerable<object>)ok.Value!)
            .Select(x => ((string)x.GetType().GetProperty("Code")!.GetValue(x)!, (bool)x.GetType().GetProperty("Available")!.GetValue(x)!))
            .ToList();
    }

    [PostgresFact]
    public async Task Agents_list_has_only_agents_whose_data_the_user_may_read()
    {
        IReadOnlyList<AgentConfig> all;
        using (var scope = fixture.Services.CreateScope())
        {
            all = await scope.ServiceProvider.GetRequiredService<AiAgentStore>().ListAsync(default);
        }

        // Со всеми правами — полный список; с правом только на HR — те же агенты без тех, кому нужно другое право.
        var required = all.ToDictionary(a => a.Code, a => a.RequiredPermission);
        var full = await ListAsync([.. Permissions.All]);
        var hrOnly = await ListAsync(Permissions.AgentsRun, Permissions.HrRead);
        Assert.Equal(
            full.Where(a => required[a.Code] is null || required[a.Code] == Permissions.HrRead).Select(a => a.Code).Order().ToList(),
            hrOnly.Select(a => a.Code).Order().ToList());
        var sales = all.First(a => a.RequiredPermission == Permissions.SalesRead && full.Any(f => f.Code == a.Code));
        Assert.DoesNotContain(hrOnly, a => a.Code == sales.Code);

        // С правом «Продажи» агент продаж есть; «доступен» — значит включён.
        var withSales = await ListAsync(Permissions.AgentsRun, Permissions.SalesRead);
        Assert.Contains(withSales, a => a.Code == sales.Code && a.Available == sales.Enabled);
    }

    /// <summary>Сводка находок (GET /api/ai/alerts): отделов, данные которых закрыты, в разбивке по отделам нет — не «HR · 0».</summary>
    [PostgresFact]
    public async Task Alerts_summary_lists_only_departments_whose_data_the_user_may_read()
    {
        async Task<List<string>> CategoriesAsync(params string[] rights)
        {
            using var scope = fixture.Services.CreateScope();
            var sp = scope.ServiceProvider;
            var controller = ActivatorUtilities.CreateInstance<AiAlertsController>(sp, new Fixed(rights));
            var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(OneBaseClaims.UserId, Guid.NewGuid().ToString())], "test"));
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user, RequestServices = sp } };
            var ok = Assert.IsType<OkObjectResult>(await controller.List(true, default));
            var summary = ok.Value!.GetType().GetProperty("Summary")!.GetValue(ok.Value)!;
            var byCategory = (IEnumerable<object>)summary.GetType().GetProperty("ByCategory")!.GetValue(summary)!;
            return byCategory.Select(x => (string)x.GetType().GetProperty("Category")!.GetValue(x)!).ToList();
        }

        IReadOnlyList<AgentConfig> agents;
        using (var scope = fixture.Services.CreateScope())
        {
            agents = await scope.ServiceProvider.GetRequiredService<AiAgentStore>().ListAsync(default);
        }

        var everything = await CategoriesAsync([.. Permissions.All]);
        Assert.Equal(["finance", "sales", "marketing", "hr", "production", "supply"], everything);
        var hrOnly = await CategoriesAsync(Permissions.AgentsRun, Permissions.HrRead);
        Assert.Contains("hr", hrOnly);
        // AI-сотрудник отдела — тот, чей код совпадает с кодом отдела; его право решает, виден ли отдел в сводке.
        foreach (var closed in agents.Where(a => a.Code == a.DepartmentCode && a.RequiredPermission is { } p && p != Permissions.HrRead && p != Permissions.AgentsRun))
        {
            Assert.DoesNotContain(closed.DepartmentCode!, hrOnly);
        }
    }
}
