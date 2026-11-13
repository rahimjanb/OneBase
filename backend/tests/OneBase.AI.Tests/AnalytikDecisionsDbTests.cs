using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Analytik;
using OneBase.AI.Tools;
using OneBase.Application.Analytik;
using OneBase.Application.Analytik.Compute;
using OneBase.Application.Analytik.Decisions;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Semantic;
using OneBase.Application.Security;
using OneBase.Domain.Identity;
using OneBase.Infrastructure.Persistence;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>
/// Память решений на PostgreSQL (мир IntelligenceWorld): исходный показатель — срез до решения, факт — после, вердикт и значимость «до / после»;
/// эталон — строки мира. Плюс права (чужое решение не видно) и черновик решения от AI без сохранения.
/// </summary>
public sealed class AnalytikDecisionsDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly DateOnly Sep1 = new(2026, 9, 1), Sep30 = new(2026, 9, 30), Oct1 = new(2026, 10, 1), Oct30 = new(2026, 10, 30);

    private async Task<AnalytikPrincipal> AuthorAsync(string suffix)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var role = new Role { Name = $"Директор решений {suffix}" };
        foreach (var code in new[] { Permissions.SalesRead, Permissions.AnalytikUse })
        {
            role.Permissions.Add(new RolePermission { Code = code });
        }

        var user = new User { Login = $"decide-{suffix}", FullName = "Директор" };
        user.Roles.Add(new UserRole { Role = role });
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return await scope.ServiceProvider.GetRequiredService<AnalytikAccess>().ResolveAsync(user.Id, default);
    }

    private static DecisionInput Input(IntelligenceWorld w, int region, string title, decimal expected) =>
        new(title, null, "Итог исследования", Oct1, new DecisionMetric("revenue", [new AnalyticsFilter("region", "in", [w.Regions[region]])]), "up", expected, Oct30);

    [PostgresFact]
    public async Task Decision_keeps_its_baseline_and_is_evaluated_against_the_fact_when_due()
    {
        var w = await IntelligenceWorld.SeedAsync(fixture);
        using (var clean = fixture.Services.CreateScope())
        {
            await clean.ServiceProvider.GetRequiredService<IAnalytikDb>().Decisions.ExecuteDeleteAsync();
        }

        fixture.Clock.Now = new DateTimeOffset(2026, 11, 5, 6, 0, 0, TimeSpan.Zero);
        var author = await AuthorAsync(w.Suffix);
        DecisionView khorezm, samarkand;
        using (var scope = fixture.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<DecisionService>();
            khorezm = await service.CreateAsync(author, Input(w, 0, "Новый агент в Хорезме", 10), default);
            samarkand = await service.CreateAsync(author, Input(w, 1, "Повысить цены группы Б", 50), default);
        }

        Assert.Equal(Sep1, khorezm.BaselineFrom);
        Assert.Equal(Sep30, khorezm.BaselineTo);
        Assert.Equal(w.Revenue(0, Sep1, Sep30), khorezm.BaselineValue);
        Assert.True(khorezm.Due);

        using (var scope = fixture.Services.CreateScope())
        {
            Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<DecisionService>().EvaluateDueAsync(default));
        }

        using (var scope = fixture.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<DecisionService>();
            var k = await service.GetAsync(author, khorezm.Id, default);
            Assert.Equal("evaluated", k.Status);
            Assert.Equal("missed", k.Outcome);
            Assert.Equal(w.Revenue(0, Oct1, Oct30), k.ActualValue);
            Assert.Equal(EvidenceClasses.Statistical, k.Actual!.Value.GetProperty("evidenceClass").GetString());
            Assert.Contains("не доказывает", string.Join(" ", k.Actual.Value.GetProperty("notes").EnumerateArray().Select(n => n.GetString())));

            var s = await service.GetAsync(author, samarkand.Id, default);
            var change = (double)((w.Revenue(1, Oct1, Oct30) / 30 - w.Revenue(1, Sep1, Sep30) / 30) / (w.Revenue(1, Sep1, Sep30) / 30) * 100);
            Assert.Equal(change, s.Actual!.Value.GetProperty("changePct").GetDouble(), 1);
            Assert.Equal("partial", s.Outcome); // рост есть (подорожание Б), но меньше ожидаемых 50 %
        }
    }

    [PostgresFact]
    public async Task Other_users_do_not_see_a_decision_and_ai_only_prepares_a_draft()
    {
        var w = await IntelligenceWorld.SeedAsync(fixture);
        var author = await AuthorAsync(w.Suffix);
        var stranger = new AnalytikPrincipal(Guid.NewGuid(), new HashSet<string> { Permissions.SalesRead, Permissions.AnalytikUse }, SalesRowScope.Organization);
        using var scope = fixture.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<DecisionService>();
        var d = await service.CreateAsync(author, Input(w, 0, "Решение", 5), default);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetAsync(stranger, d.Id, default));
        Assert.Empty(await service.ListAsync(stranger, null, null, default));

        var draft = scope.ServiceProvider.GetServices<ITool>().Single(t => t.Name == "analytik_decision_draft");
        Assert.Equal(ToolLevels.Prepare, draft.Level);
        var before = await scope.ServiceProvider.GetRequiredService<IAnalytikDb>().Decisions.CountAsync();
        var result = await draft.ExecuteAsync(new ToolContext("analyst.sales", author.UserId, 0), JsonSerializer.SerializeToElement(new
        {
            title = "Вернуть точку 5 в Хорезме", metric = "revenue", expected_direction = "up", expected_change_pct = 15,
            filters = new[] { new { dimension = "region", op = "in", values = new[] { w.Treated } } },
        }));
        Assert.True(result.Success, result.Content);
        using var doc = JsonDocument.Parse(result.Content);
        Assert.StartsWith("/analytik/decisions?draft=", doc.RootElement.GetProperty("link").GetString());
        Assert.False(doc.RootElement.GetProperty("saved").GetBoolean());
        Assert.Equal(before, await scope.ServiceProvider.GetRequiredService<IAnalytikDb>().Decisions.CountAsync());
    }
}
