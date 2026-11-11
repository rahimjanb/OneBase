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
    public void Director_sees_only_users_and_departments_in_settings_but_all_department_data()
    {
        var director = Role(SystemRoles.Director).Permissions;

        // В «Настройках» директору — «Пользователи и роли» и «Отделы» (маршрут заявок снабжения: финансист, директор, лимит).
        Assert.Equal([Permissions.UsersManage, Permissions.ProcurementSettings], SystemRoles.SettingsPermissions.Where(director.Contains));
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
        // У отдела продаж ролей несколько: «Продажи», «РМ» и «Супервайзер продаж» (свои регионы) — у всех нет настроек и есть консультант.
        var roles = SystemRoles.All.Where(r => r.DepartmentCode == department && r.Name != SystemRoles.DataAdmin).ToList();
        Assert.NotEmpty(roles);
        Assert.All(roles, role =>
        {
            Assert.DoesNotContain(role.Permissions, SystemRoles.SettingsPermissions.Contains);
            Assert.Contains(Permissions.AgentsRun, role.Permissions);
        });
    }

    [Fact]
    public void DataAdmin_has_sales_files_and_only_data_manager_in_settings()
    {
        var role = Role(SystemRoles.DataAdmin);

        Assert.Equal("sales", role.DepartmentCode); // «Файлы» — только папка отдела продаж
        Assert.Equal([Permissions.DataManage, Permissions.FilesRead, Permissions.FilesWrite], role.Permissions.Order());
        Assert.Equal([Permissions.DataManage], SystemRoles.SettingsPermissions.Where(role.Permissions.Contains));
        Assert.DoesNotContain(Permissions.FilesReadAll, role.Permissions);
    }
}
