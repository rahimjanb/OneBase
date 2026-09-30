using OneBase.Application.Security;

namespace OneBase.AI.Tests;

public class SystemRolesTests
{
    private static SystemRoles.Definition Role(string name) => SystemRoles.All.Single(r => r.Name == name);

    [Fact]
    public void Admin_has_every_permission()
    {
        Assert.Equal(Permissions.All.Order(), Role(SystemRoles.Admin).Permissions.Order());
    }

    [Fact]
    public void Director_sees_only_users_and_roles_in_settings_but_all_department_data()
    {
        var director = Role(SystemRoles.Director).Permissions;

        Assert.Equal([Permissions.UsersManage], SystemRoles.SettingsPermissions.Where(director.Contains));
        Assert.Contains(Permissions.SalesRead, director);
        Assert.Contains(Permissions.FinanceRead, director);
        Assert.Contains(Permissions.HrRead, director);
    }

    [Theory]
    [InlineData("sales")]
    [InlineData("finance")]
    [InlineData("hr")]
    [InlineData("production")]
    [InlineData("supply")]
    [InlineData("marketing")]
    public void Every_department_has_a_role_without_settings(string department)
    {
        var role = Assert.Single(SystemRoles.All, r => r.DepartmentCode == department);

        Assert.DoesNotContain(role.Permissions, SystemRoles.SettingsPermissions.Contains);
        Assert.Contains(Permissions.AgentsRun, role.Permissions);
    }
}
