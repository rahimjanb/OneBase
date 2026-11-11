using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Api.Auth;
using OneBase.Api.Controllers;
using OneBase.Application.Abstractions;
using OneBase.Application.Security;
using OneBase.Domain.Identity;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// Права в разделах отделов и настройках (UserRights): как они меняют итоговые права (зависимости, «как у роли», настройки продаж без
/// раздела «Продажи», совместимость с сохранёнными разделами) и как их сохраняет «Настройки → Пользователи» на настоящем PostgreSQL: выдать
/// можно только своё, свои права не меняются, неизвестный код — ошибка; совпали с ролью — «как у роли»; по ним — уведомления «по праву».
/// </summary>
public class UserRightsTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly string[] KeeperRole =
        [Permissions.ProcurementRequest, Permissions.ProcurementWarehouse, Permissions.AgentsRun, Permissions.FilesRead, Permissions.FilesWrite, Permissions.WorkTasks];

    [Fact]
    public void Without_rights_the_role_rights_stay_and_chosen_rights_replace_only_the_catalog_ones()
    {
        Assert.Equal(KeeperRole.Order(), UserSections.Effective(KeeperRole, null, null).Order());
        Assert.Equal([Permissions.ProcurementRequest, Permissions.ProcurementWarehouse], UserRights.Of(KeeperRole));

        // Кладовщику отметили качество сырья и сняли склад и свои заявки: файлы, задачи и консультант — как были (это разделы).
        var effective = UserSections.Effective(KeeperRole, null, [Permissions.QualityCheck]);
        Assert.Contains(Permissions.QualityCheck, effective);
        Assert.DoesNotContain(Permissions.ProcurementWarehouse, effective);
        Assert.DoesNotContain(Permissions.ProcurementRequest, effective);
        Assert.Contains(Permissions.FilesRead, effective);
        Assert.Contains(Permissions.AgentsRun, effective);
        Assert.Empty(UserRights.Of(UserSections.Effective(KeeperRole, null, [])));
    }

    [Fact]
    public void Actions_bring_their_viewing_right_and_settings_of_sales_need_the_sales_section()
    {
        Assert.Equal([Permissions.ProcurementRead, Permissions.ProcurementManage], UserRights.Normalize([Permissions.ProcurementManage]));
        Assert.Equal([Permissions.ProductionRead, Permissions.ProductionOperate], UserRights.Normalize([" production.operate ", "unknown", Permissions.ProductionOperate]));
        Assert.Contains(Permissions.ProductionRead, UserSections.Effective([], null, [Permissions.ProductionRecipes]));

        // Настройки продаж без раздела «Продажи» не действуют — ни отмеченные, ни от роли, если раздел снят.
        Assert.DoesNotContain(Permissions.SalesManage, UserSections.Effective([], null, [Permissions.SalesManage]));
        Assert.Contains(Permissions.SalesManage, UserSections.Effective([Permissions.SalesRead], null, [Permissions.SalesManage]));
        Assert.DoesNotContain(Permissions.SalesManage, UserSections.Effective([Permissions.SalesRead, Permissions.SalesManage], [UserSections.Tasks], null));
    }

    [Fact]
    public void Saved_sections_do_not_take_away_rights_of_departments_and_settings()
    {
        // Разделы отмечены раньше (задачи и консультант) — права склада и настроек роли остаются, пока права не отмечены.
        string[] role = [.. KeeperRole, Permissions.UsersManage, Permissions.ProcurementSettings];
        var effective = UserSections.Effective(role, [UserSections.Tasks, UserSections.Consultant], null);
        Assert.Contains(Permissions.ProcurementWarehouse, effective);
        Assert.Contains(Permissions.UsersManage, effective);
        Assert.Contains(Permissions.ProcurementSettings, effective);
        Assert.DoesNotContain(Permissions.FilesRead, effective);
    }

    private sealed record Org(string S, Guid KeeperRole, Guid Director, Guid Department);

    private async Task<Org> SeedAsync()
    {
        var s = Guid.NewGuid().ToString("N")[..6];
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var keeper = new Role { Name = $"Кладовщик {s}" };
        foreach (var code in KeeperRole)
        {
            keeper.Permissions.Add(new RolePermission { Code = code });
        }

        var department = new Department { Code = $"r-{s}", Name = $"Склад {s}" };
        var director = new User { Login = $"r-dir-{s}", FullName = $"Директор {s}", Department = department };
        db.AddRange(keeper, department, director);
        await db.SaveChangesAsync();
        return new Org(s, keeper.Id, director.Id, department.Id);
    }

    /// <summary>«Настройки → Пользователи» от имени того, у кого эти права (по умолчанию — все).</summary>
    private static UsersController Users(IServiceProvider sp, Guid actor, IEnumerable<string>? permissions = null)
    {
        var claims = new List<Claim> { new(OneBaseClaims.UserId, actor.ToString()) };
        claims.AddRange((permissions ?? Permissions.All).Select(p => new Claim(OneBaseClaims.Permission, p)));
        return new UsersController(sp.GetRequiredService<IAppDbContext>(), new PasswordHasher<User>(),
            new UserAccessCache(sp.GetRequiredService<IServiceScopeFactory>(), new MemoryCache(new MemoryCacheOptions())), sp.GetRequiredService<IAuditLogger>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) } },
        };
    }

    private static UsersController.UserInput Input(Org org, string login, IReadOnlyList<string>? rights, IReadOnlyList<string>? sections = null) =>
        new(login, "Акмаль", "Хамидов", null, null, "Кладовщик", org.KeeperRole, "Pass-12345", Sections: sections, DepartmentId: org.Department, NoVerifix: true,
            Rights: rights);

    private static string? Error(IActionResult result) =>
        result is BadRequestObjectResult bad ? JsonSerializer.SerializeToElement(bad.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web)).GetProperty("error").GetString() : null;

    private static Guid Id(IActionResult result) =>
        JsonSerializer.SerializeToElement(((OkObjectResult)result).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web)).GetProperty("id").GetGuid();

    private async Task<(List<string>? Stored, HashSet<string> Effective)> StoredAsync(Guid id)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var stored = await db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => u.Rights).SingleAsync();
        return (stored, (await db.EffectivePermissionsAsync(id, CancellationToken.None))!);
    }

    [PostgresFact]
    public async Task Users_screen_saves_rights_only_within_the_editor_own_rights()
    {
        var org = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;

        // Директору без права «Качество сырья» его не выдать; неизвестный код и настройки продаж без раздела — ошибки.
        string[] director = [Permissions.UsersManage, .. KeeperRole, Permissions.ProcurementRead, Permissions.ProcurementSettings];
        Assert.Contains("выдать нельзя", Error(await Users(sp, org.Director, director).Create(Input(org, $"r-k1-{org.S}", [Permissions.QualityCheck]), default)));
        Assert.Contains("Неизвестное право", Error(await Users(sp, org.Director, director).Create(Input(org, $"r-k1-{org.S}", ["warehouse.all"]), default)));
        Assert.Contains("только вместе с разделом «Продажи»",
            Error(await Users(sp, org.Director, Permissions.All).Create(Input(org, $"r-k1-{org.S}", [Permissions.SalesManage]), default)));

        // Директор выдаёт то, что есть у него: кладовщик ещё и смотрит все заявки и настраивает снабжение.
        var keeper = Id(await Users(sp, org.Director, director).Create(Input(org, $"r-k1-{org.S}",
            [Permissions.ProcurementRequest, Permissions.ProcurementWarehouse, Permissions.ProcurementRead, Permissions.ProcurementSettings]), default));
        var (stored, effective) = await StoredAsync(keeper);
        Assert.Equal([Permissions.ProcurementRequest, Permissions.ProcurementRead, Permissions.ProcurementWarehouse, Permissions.ProcurementSettings], stored);
        Assert.Contains(Permissions.ProcurementSettings, effective);
        Assert.True(SystemRoles.SettingsPermissions.Any(effective.Contains)); // «Настройки» откроются

        // Отметили как у роли — хранится «как у роли».
        var asRole = Id(await Users(sp, org.Director, director).Create(Input(org, $"r-k2-{org.S}", [Permissions.ProcurementWarehouse, Permissions.ProcurementRequest]), default));
        Assert.Null((await StoredAsync(asRole)).Stored);

        // Администратор переводит кладовщика в лабораторию: качество сырья вместо склада; уведомления «по праву» — по новым правам.
        var admin = Guid.Empty;
        using (var s2 = fixture.Services.CreateScope())
        {
            var db = s2.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var user = new User { Login = $"r-adm-{org.S}", FullName = $"Админ {org.S}" };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            admin = user.Id;
        }

        Assert.Null(Error(await Users(sp, admin).Update(keeper, Input(org, $"r-k1-{org.S}", [Permissions.QualityCheck, Permissions.ProcurementRequest]) with { Password = null },
            default)));
        (stored, effective) = await StoredAsync(keeper);
        Assert.Equal([Permissions.ProcurementRequest, Permissions.QualityCheck], stored);
        Assert.DoesNotContain(Permissions.ProcurementWarehouse, effective);
        var db2 = sp.GetRequiredService<OneBaseDbContext>();
        Assert.Contains(keeper, await db2.ActiveUsersWithAsync(Permissions.QualityCheck, default, withoutAdmin: true));
        Assert.DoesNotContain(keeper, await db2.ActiveUsersWithAsync(Permissions.ProcurementWarehouse, default, withoutAdmin: true));
        Assert.Contains(asRole, await db2.ActiveUsersWithAsync(Permissions.ProcurementWarehouse, default, withoutAdmin: true));

        // Список: права и признак «вручную», каталог по группам; свои права изменить нельзя.
        var list = JsonSerializer.SerializeToElement(((OkObjectResult)await Users(sp, admin).List(default)).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var row = list.GetProperty("users").EnumerateArray().Single(u => u.GetProperty("id").GetGuid() == keeper);
        Assert.Equal((true, 2), (row.GetProperty("rightsCustom").GetBoolean(), row.GetProperty("rights").GetArrayLength()));
        Assert.Equal(UserRights.All.Count, list.GetProperty("rights").GetArrayLength());
        // Группы: снабжение и склад, производство, продажи (выгрузки Excel), настройки.
        Assert.Equal([UserRights.Supply, UserRights.Production, UserRights.Sales, UserRights.Settings],
            list.GetProperty("rightGroups").EnumerateArray().Select(g => g.GetProperty("key").GetString()).ToArray());
        Assert.Contains("Свои права изменить нельзя",
            Error(await Users(sp, keeper, [Permissions.UsersManage, .. KeeperRole, Permissions.QualityCheck]).Update(keeper,
                Input(org, $"r-k1-{org.S}", [Permissions.QualityCheck]) with { Password = null }, default)));

        // Права доходят до входа: кэш прав пользователя — с отмеченными.
        var access = await new UserAccessCache(sp.GetRequiredService<IServiceScopeFactory>(), new MemoryCache(new MemoryCacheOptions())).GetAsync(keeper, default);
        Assert.Contains(Permissions.QualityCheck, access!.Permissions);
        Assert.DoesNotContain(Permissions.ProcurementWarehouse, access.Permissions);
    }
}
