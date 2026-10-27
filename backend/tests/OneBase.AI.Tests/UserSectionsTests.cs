using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Knowledge;
using OneBase.AI.Security;
using OneBase.AI.Tools;
using OneBase.Application.Security;
using OneBase.Domain.AI;
using OneBase.Domain.Identity;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

public class UserSectionsTests
{
    private static readonly string[] SalesRole =
    [
        Permissions.SalesRead, Permissions.SalesManage, Permissions.FilesRead, Permissions.FilesWrite, Permissions.AgentsRun, Permissions.WorkTasks,
        Permissions.UsersManage,
    ];

    [Fact]
    public void Without_sections_the_role_permissions_stay_as_they_are()
    {
        Assert.Equal(SalesRole.Order(), UserSections.Effective(SalesRole, null).Order());
        Assert.Equal([UserSections.Sales, UserSections.Tasks, UserSections.Files, UserSections.Consultant], UserSections.Of(SalesRole));
    }

    [Fact]
    public void Chosen_sections_replace_the_role_sections_and_keep_role_only_rights()
    {
        var effective = UserSections.Effective(SalesRole, [UserSections.Hr, UserSections.Consultant]);

        Assert.Contains(Permissions.HrRead, effective); // у роли HR нет — выдаётся право раздела
        Assert.Contains(Permissions.AgentsRun, effective);
        Assert.Contains(Permissions.UsersManage, effective); // право управления — от роли, не раздел
        foreach (var removed in new[] { Permissions.SalesRead, Permissions.SalesManage, Permissions.FilesRead, Permissions.FilesWrite, Permissions.WorkTasks })
        {
            Assert.DoesNotContain(removed, effective);
        }

        Assert.Equal([UserSections.Hr, UserSections.Consultant], UserSections.Of(effective));
        Assert.Empty(UserSections.Of(UserSections.Effective(SalesRole, [])));
    }

    [Fact]
    public void A_section_the_role_already_has_keeps_the_role_rights_in_it()
    {
        string[] admin = [Permissions.FilesRead, Permissions.FilesWrite, Permissions.FilesReadAll, Permissions.FilesManage, Permissions.FieldManage, Permissions.FieldUse];

        var effective = UserSections.Effective(admin, [UserSections.Files, UserSections.Field]);
        Assert.Contains(Permissions.FilesReadAll, effective); // все отделы, а не только «свой»
        Assert.Contains(Permissions.FilesManage, effective);
        Assert.Contains(Permissions.FieldManage, effective);
        Assert.DoesNotContain(Permissions.FieldPlan, effective); // своих прав раздела у роли хватает — Grant не добавляется

        var granted = UserSections.Effective([], [UserSections.Files, UserSections.Field]);
        Assert.Equal(new[] { Permissions.FieldPlan, Permissions.FilesRead, Permissions.FilesWrite }.Order(), granted.Order());
    }

    [Fact]
    public void Normalize_keeps_known_keys_once_in_catalog_order()
    {
        Assert.Equal([UserSections.Sales, UserSections.Hr], UserSections.Normalize([" hr", "sales", "sales", "bogus"]));
    }

    [Fact]
    public void Catalog_uses_known_permissions_and_grants_only_its_own()
    {
        Assert.Equal(UserSections.All.Count, UserSections.All.Select(s => s.Key).Distinct().Count());
        foreach (var section in UserSections.All)
        {
            Assert.NotEmpty(section.Grant);
            Assert.All(section.Permissions, p => Assert.Contains(p, Permissions.All));
            Assert.All(section.Grant, p => Assert.Contains(p, section.Permissions));
        }

        // Право принадлежит одному разделу — иначе снятая галочка одного раздела отнимала бы право у другого.
        Assert.Equal(UserSections.SectionPermissions.Count, UserSections.All.Sum(s => s.Permissions.Count));
    }
}

/// <summary>Разделы пользователя на базе: права AI (IUserPermissions), инструменты агента и «кто может» считают отмеченные разделы.</summary>
public class UserSectionsDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private const string Agent = "sections-test";

    [PostgresFact]
    public async Task Ai_answers_only_by_the_sections_ticked_for_the_user()
    {
        Guid custom, asRole, inactive;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var role = new Role { Name = $"Продажи разделов {Guid.NewGuid():N}"[..30] };
            foreach (var code in new[] { Permissions.SalesRead, Permissions.SalesManage, Permissions.FilesRead, Permissions.FilesWrite, Permissions.AgentsRun, Permissions.WorkTasks })
            {
                role.Permissions.Add(new RolePermission { Code = code });
            }

            var customUser = new User { Login = $"sec-hr-{Guid.NewGuid():N}"[..20], FullName = "Раздел HR", Sections = [UserSections.Hr, UserSections.Consultant] };
            customUser.Roles.Add(new UserRole { Role = role });
            var roleUser = new User { Login = $"sec-role-{Guid.NewGuid():N}"[..20], FullName = "Как у роли" };
            roleUser.Roles.Add(new UserRole { Role = role });
            var offUser = new User { Login = $"sec-off-{Guid.NewGuid():N}"[..20], FullName = "Отключён", IsActive = false, Sections = [UserSections.Hr] };
            offUser.Roles.Add(new UserRole { Role = role });
            db.Users.AddRange(customUser, roleUser, offUser);

            await db.AgentToolGrants.Where(g => g.AgentCode == Agent).ExecuteDeleteAsync();
            await db.AiAgentKnowledgeSources.Where(s => s.AgentCode == Agent).ExecuteDeleteAsync();
            foreach (var tool in new[] { "get_sales", "get_hr_employees", "get_department_tasks" })
            {
                db.AgentToolGrants.Add(new AgentToolGrant { AgentCode = Agent, ToolName = tool });
            }

            foreach (var source in new[] { KnowledgeSources.SalesSecondary, KnowledgeSources.HrAttendance, KnowledgeSources.WorkTasks })
            {
                db.AiAgentKnowledgeSources.Add(new AiAgentKnowledgeSource { AgentCode = Agent, SourceCode = source });
            }

            await db.SaveChangesAsync();
            (custom, asRole, inactive) = (customUser.Id, roleUser.Id, offUser.Id);
        }

        using (var scope = fixture.Services.CreateScope())
        {
            var permissions = scope.ServiceProvider.GetRequiredService<IUserPermissions>();
            var mine = await permissions.GetAsync(custom);
            Assert.Contains(Permissions.HrRead, mine);
            Assert.Contains(Permissions.AgentsRun, mine);
            Assert.DoesNotContain(Permissions.SalesRead, mine);
            Assert.DoesNotContain(Permissions.WorkTasks, mine);
            Assert.Contains(Permissions.SalesRead, await permissions.GetAsync(asRole));
            Assert.Empty(await permissions.GetAsync(inactive));

            // Инструменты агента: HR — да, продажи и задачи — нет (разделы сняты); у «как у роли» — наоборот.
            var tools = scope.ServiceProvider.GetRequiredService<ToolExecutor>();
            Assert.Equal(["get_hr_employees"], (await tools.GetAllowedToolsAsync(Agent, custom)).Select(t => t.Name));
            Assert.Equal(["get_department_tasks", "get_sales"], (await tools.GetAllowedToolsAsync(Agent, asRole)).Select(t => t.Name).Order());

            using var args = JsonDocument.Parse("{}");
            var denied = await tools.ExecuteAsync(new ToolContext(Agent, custom, 0), "get_sales", args.RootElement);
            Assert.False(denied.Success, denied.Content);

            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var hr = await db.ActiveUsersWithAsync(Permissions.HrRead, CancellationToken.None);
            Assert.Contains(custom, hr);
            Assert.DoesNotContain(asRole, hr);
            Assert.DoesNotContain(inactive, hr);
            Assert.Contains(asRole, await db.ActiveUsersWithAsync(Permissions.SalesRead, CancellationToken.None));
            Assert.DoesNotContain(custom, await db.ActiveUsersWithAsync(Permissions.SalesRead, CancellationToken.None));
        }
    }
}
