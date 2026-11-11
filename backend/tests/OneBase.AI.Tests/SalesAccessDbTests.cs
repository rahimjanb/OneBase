using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Field;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Access;
using OneBase.Domain.Identity;
using OneBase.Domain.Sales;
using OneBase.Infrastructure.Persistence;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>
/// Охват пользователя в «Продажах» на настоящем PostgreSQL (ONEBASE_TEST_POSTGRES): РМ — регионы своих направлений (регион, переведённый в
/// другое направление, уходит сразу), СВР — свои регионы, ТП — только он, без назначения — вся организация. Назначает администратор:
/// проверка данных, ТП — за одним пользователем, свой охват не меняется, каждое изменение — в аудит и действует сразу.
/// </summary>
public class SalesAccessDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private sealed record Org(string S, Guid DirA, Guid DirB, Guid A1, Guid A2, Guid B1, long Agent1, long Agent2, Guid RmA, Guid RmB, Guid SvA, Guid Tp1, Guid Director,
        Guid Admin);

    private static long _linko = 880_000_000;

    private async Task<Org> SeedAsync()
    {
        var s = Guid.NewGuid().ToString("N")[..6];
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var dirA = new SalesDirection { Name = $"Восток {s}", ManagerName = "РМ Восток" };
        var dirB = new SalesDirection { Name = $"Запад {s}", ManagerName = "РМ Запад" };
        db.SalesDirections.AddRange(dirA, dirB);
        var a1 = new SalesRegion { LinkoBranchId = Interlocked.Increment(ref _linko), Name = $"Самарканд {s}", Direction = dirA };
        var a2 = new SalesRegion { LinkoBranchId = Interlocked.Increment(ref _linko), Name = $"Джизак {s}", Direction = dirA };
        var b1 = new SalesRegion { LinkoBranchId = Interlocked.Increment(ref _linko), Name = $"Бухара {s}", Direction = dirB };
        db.SalesRegions.AddRange(a1, a2, b1);
        var (agent1, agent2) = (Interlocked.Increment(ref _linko), Interlocked.Increment(ref _linko));
        db.LinkoUsers.AddRange(new LinkoUser { Id = agent1, FirstName = "Азиз", SecondName = $"Каримов {s}", IsActive = true, JobName = "Агент" },
            new LinkoUser { Id = agent2, FirstName = "Бобур", SecondName = $"Алиев {s}", IsActive = true, JobName = "Агент" });
        User Person(string login) => new() { Login = $"{login}-{s}", FullName = $"{login} {s}" };
        var users = new[] { Person("rm-a"), Person("rm-b"), Person("sv-a"), Person("tp-1"), Person("director"), Person("admin") };
        db.Users.AddRange(users);
        await db.SaveChangesAsync();
        return new Org(s, dirA.Id, dirB.Id, a1.Id, a2.Id, b1.Id, agent1, agent2, users[0].Id, users[1].Id, users[2].Id, users[3].Id, users[4].Id, users[5].Id);
    }

    private async Task<T> With<T>(Func<IServiceProvider, Task<T>> act)
    {
        using var scope = fixture.Services.CreateScope();
        return await act(scope.ServiceProvider);
    }

    private Task<SalesScope> ScopeOf(Guid user) => With(sp => sp.GetRequiredService<SalesAccess>().ScopeAsync(user, default));

    private Task<SalesScopeView> Assign(Guid actor, Guid user, SalesScopeInput input) =>
        With(sp => sp.GetRequiredService<SalesScopeAdmin>().SetAsync(actor, user, input, default));

    [Fact]
    public void Sales_roles_open_sales_by_regions_and_sales_base_rm_does_not_open_sales()
    {
        var all = Application.Security.SystemRoles.All.ToDictionary(r => r.Name);
        Assert.Contains(Application.Security.Permissions.SalesRead, all[Application.Security.SystemRoles.SalesRm].Permissions);
        Assert.Contains(Application.Security.Permissions.SalesRead, all[Application.Security.SystemRoles.SalesSupervisor].Permissions);
        Assert.DoesNotContain(Application.Security.Permissions.SalesRead, all[Application.Security.SystemRoles.FieldRm].Permissions);
        Assert.DoesNotContain(Application.Security.Permissions.SalesExport, all[Application.Security.SystemRoles.FieldRm].Permissions);
        Assert.Equal(SalesScopeLevel.Rm, SalesScopeAdmin.LevelOfRole("РМ"));
        Assert.Equal(SalesScopeLevel.Supervisor, SalesScopeAdmin.LevelOfRole("Супервайзер продаж"));
        Assert.Null(SalesScopeAdmin.LevelOfRole("Супервайзер")); // Sales Base
        Assert.Null(SalesScopeAdmin.LevelOfRole("РМ (Sales Base)"));
    }

    [Fact]
    public void Scope_checks_follow_the_hierarchy()
    {
        var (d1, d2, r1, r2) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var rm = new SalesScope(SalesScopeLevel.Rm, new HashSet<Guid> { d1 }, new HashSet<Guid> { r1 }, null);
        Assert.True(rm.CanSeeDirection(d1));
        Assert.False(rm.CanSeeDirection(d2));
        Assert.True(rm.CanSeeRegion(r1));
        Assert.False(rm.CanSeeRegion(r2));
        Assert.False(rm.CanSeeRegion(null));
        Assert.True(rm.CanSeeAgent(5, r1));
        Assert.False(rm.CanSeeAgent(5, r2));
        Assert.False(rm.CanSeeAgent(5, null)); // ТП без региона РМ не виден

        var sv = new SalesScope(SalesScopeLevel.Supervisor, new HashSet<Guid>(), new HashSet<Guid> { r2 }, null);
        Assert.False(sv.CanSeeDirection(d1));
        Assert.True(sv.CanSeeRegion(r2));
        Assert.True(sv.CanSeeAgent(9, r2));
        Assert.False(sv.CanSeeAgent(9, r1));

        var tp = new SalesScope(SalesScopeLevel.Agent, new HashSet<Guid>(), new HashSet<Guid>(), 7);
        Assert.True(tp.CanSeeAgent(7, r1));
        Assert.False(tp.CanSeeAgent(8, r1));
        Assert.False(tp.CanSeeRegion(r1));
        Assert.False(tp.CanSeeDirection(d1));
        Assert.False(tp.IsOrganization);

        Assert.True(SalesScope.Organization.CanSeeRegion(r2));
        Assert.True(SalesScope.Organization.CanSeeDirection(d2));
        Assert.True(SalesScope.Organization.CanSeeAgent(1, null));
    }

    [PostgresFact]
    public async Task Rm_sees_regions_of_own_directions_only_and_a_moved_region_leaves_at_once()
    {
        var org = await SeedAsync();
        await Assign(org.Admin, org.RmA, new SalesScopeInput(SalesScopeLevel.Rm, [org.DirA], null, null));
        await Assign(org.Admin, org.RmB, new SalesScopeInput(SalesScopeLevel.Rm, [org.DirB], null, null));

        var a = await ScopeOf(org.RmA);
        Assert.Equal(new HashSet<Guid> { org.A1, org.A2 }, a.RegionIds.ToHashSet());
        Assert.False(a.CanSeeRegion(org.B1));
        Assert.False(a.CanSeeDirection(org.DirB));
        var b = await ScopeOf(org.RmB);
        Assert.Equal(new HashSet<Guid> { org.B1 }, b.RegionIds.ToHashSet());
        Assert.False(b.CanSeeRegion(org.A1));

        // Регион передали другому РМ в оргструктуре (правка сбрасывает данные продаж) — у прежнего РМ его больше нет.
        await With(async sp =>
        {
            var db = sp.GetRequiredService<OneBaseDbContext>();
            (await db.SalesRegions.SingleAsync(r => r.Id == org.A2)).DirectionId = org.DirB;
            await db.SaveChangesAsync();
            sp.GetRequiredService<SalesCacheSignal>().Invalidate();
            return true;
        });
        Assert.False((await ScopeOf(org.RmA)).CanSeeRegion(org.A2));
        Assert.True((await ScopeOf(org.RmB)).CanSeeRegion(org.A2));
    }

    [PostgresFact]
    public async Task Supervisor_agent_and_unassigned_users_get_their_scopes_and_transfer_or_revoke_works_immediately()
    {
        var org = await SeedAsync();
        Assert.True((await ScopeOf(org.Director)).IsOrganization); // без назначения — вся организация, как было

        await Assign(org.Admin, org.SvA, new SalesScopeInput(SalesScopeLevel.Supervisor, null, [org.A1], null));
        var sv = await ScopeOf(org.SvA);
        Assert.True(sv.CanSeeRegion(org.A1));
        Assert.False(sv.CanSeeRegion(org.A2));
        Assert.False(sv.CanSeeDirection(org.DirA));

        await Assign(org.Admin, org.Tp1, new SalesScopeInput(SalesScopeLevel.Agent, null, null, org.Agent1));
        var tp = await ScopeOf(org.Tp1);
        Assert.True(tp.CanSeeAgent(org.Agent1, org.A1));
        Assert.False(tp.CanSeeAgent(org.Agent2, org.A1));
        Assert.False(tp.CanSeeRegion(org.A1));

        // Перевод супервайзера в другой регион: прежний регион закрыт сразу (охват в кэше сброшен назначением).
        await Assign(org.Admin, org.SvA, new SalesScopeInput(SalesScopeLevel.Supervisor, null, [org.B1], null));
        var moved = await ScopeOf(org.SvA);
        Assert.False(moved.CanSeeRegion(org.A1));
        Assert.True(moved.CanSeeRegion(org.B1));

        // Охват снят — снова вся организация; повышение ТП до РМ — сразу.
        await Assign(org.Admin, org.Tp1, new SalesScopeInput(SalesScopeLevel.Rm, [org.DirA], null, null));
        Assert.True((await ScopeOf(org.Tp1)).CanSeeRegion(org.A2));
        await Assign(org.Admin, org.Tp1, new SalesScopeInput(null, null, null, null));
        Assert.True((await ScopeOf(org.Tp1)).IsOrganization);
    }

    /// <summary>
    /// Роль «РМ» / «Супервайзер продаж»: регионы — прямо в окне пользователя (обязательно); РМ видит свои регионы, страница направления открыта,
    /// только если у него все регионы направления; роль сменили на другую — охват по регионам снят; ТП, назначенный отдельно, не трогается.
    /// </summary>
    [PostgresFact]
    public async Task Rm_and_supervisor_roles_carry_their_regions_and_lose_them_with_the_role()
    {
        var org = await SeedAsync();
        var admin = (Func<IServiceProvider, SalesScopeAdmin>)(sp => sp.GetRequiredService<SalesScopeAdmin>());
        await Assert.ThrowsAsync<FieldValidationException>(() => With(sp => admin(sp).ValidateRoleAsync(Application.Security.SystemRoles.SalesRm, [], default).ContinueWith(t => { t.GetAwaiter().GetResult(); return true; })));
        await Assert.ThrowsAsync<FieldValidationException>(() => With(sp => admin(sp).ValidateRoleAsync(Application.Security.SystemRoles.SalesSupervisor, [Guid.NewGuid()], default).ContinueWith(t => { t.GetAwaiter().GetResult(); return true; })));
        await With(async sp => { await admin(sp).ValidateRoleAsync("Продажи", null, default); return true; }); // другим ролям регионы не нужны

        // РМ с одним регионом направления «Восток» (A1): регион виден, страница направления — нет (там ещё A2).
        await With(async sp => { await admin(sp).ApplyRoleAsync(org.Admin, org.RmA, Application.Security.SystemRoles.SalesRm, [org.A1], default); return true; });
        var one = await ScopeOf(org.RmA);
        Assert.True(one.IsRm);
        Assert.True(one.CanSeeRegion(org.A1));
        Assert.False(one.CanSeeRegion(org.A2));
        Assert.False(one.CanSeeDirection(org.DirA));
        Assert.Equal($"/sales/regions/{org.A1}", await With(sp => admin(sp).HomeAsync(one, default)));

        // Оба региона «Востока» — страница направления открыта и становится своей страницей.
        await With(async sp => { await admin(sp).ApplyRoleAsync(org.Admin, org.RmA, Application.Security.SystemRoles.SalesRm, [org.A1, org.A2], default); return true; });
        var both = await ScopeOf(org.RmA);
        Assert.True(both.CanSeeDirection(org.DirA));
        Assert.False(both.CanSeeDirection(org.DirB));
        Assert.Equal($"/sales/directions/{org.DirA}", await With(sp => admin(sp).HomeAsync(both, default)));
        Assert.Equal($"РМ: Джизак {org.S}, Самарканд {org.S}", (await With(sp => admin(sp).GetAsync(org.RmA, default))).Title);

        // Повтор тех же регионов — без записи в аудит; смена роли на «Продажи» — охват по регионам снят (вся организация).
        await With(async sp => { await admin(sp).ApplyRoleAsync(org.Admin, org.RmA, Application.Security.SystemRoles.SalesRm, [org.A2, org.A1], default); return true; });
        await With(async sp => { await admin(sp).ApplyRoleAsync(org.Admin, org.RmA, "Продажи", null, default); return true; });
        Assert.True((await ScopeOf(org.RmA)).IsOrganization);
        await With(async sp =>
        {
            var logs = await sp.GetRequiredService<OneBaseDbContext>().AuditLogs.AsNoTracking().CountAsync(l => l.Action == "users.sales_scope" && l.EntityId == org.RmA.ToString());
            Assert.Equal(3, logs); // один регион → два региона → снят
            return true;
        });

        // Супервайзер продаж — его регион; охват ТП (не от роли) сменой роли не снимается.
        await With(async sp => { await admin(sp).ApplyRoleAsync(org.Admin, org.SvA, Application.Security.SystemRoles.SalesSupervisor, [org.B1], default); return true; });
        Assert.True((await ScopeOf(org.SvA)).CanSeeRegion(org.B1));
        await Assign(org.Admin, org.Tp1, new SalesScopeInput(SalesScopeLevel.Agent, null, null, org.Agent1));
        await With(async sp => { await admin(sp).ApplyRoleAsync(org.Admin, org.Tp1, "Продажи", null, default); return true; });
        Assert.True((await ScopeOf(org.Tp1)).IsAgent);
    }

    /// <summary>Окно пользователя: роль «РМ» без регионов не сохраняется; с регионами — охват создаётся вместе с пользователем, правка ролью снимает.</summary>
    [PostgresFact]
    public async Task Users_screen_saves_rm_regions_with_the_user()
    {
        var org = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var rmRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == Application.Security.SystemRoles.SalesRm)
                     ?? db.Roles.Add(new Role { Name = Application.Security.SystemRoles.SalesRm }).Entity;
        var other = db.Roles.Add(new Role { Name = $"Продажи {org.S}" }).Entity;
        foreach (var r in new[] { rmRole, other })
        {
            if (!r.Permissions.Any(p => p.Code == Application.Security.Permissions.SalesRead))
            {
                r.Permissions.Add(new RolePermission { Code = Application.Security.Permissions.SalesRead });
            }
        }

        await db.SaveChangesAsync();
        Api.Controllers.UsersController Users()
        {
            var claims = new List<System.Security.Claims.Claim> { new(Api.Auth.OneBaseClaims.UserId, org.Admin.ToString()) };
            claims.AddRange(Application.Security.Permissions.All.Select(p => new System.Security.Claims.Claim(Api.Auth.OneBaseClaims.Permission, p)));
            return new Api.Controllers.UsersController(sp.GetRequiredService<Application.Abstractions.IAppDbContext>(), new Microsoft.AspNetCore.Identity.PasswordHasher<User>(),
                new Api.Auth.UserAccessCache(sp.GetRequiredService<IServiceScopeFactory>(), new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions())),
                sp.GetRequiredService<Application.Abstractions.IAuditLogger>(), sp.GetRequiredService<SalesScopeAdmin>())
            {
                ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
                {
                    HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(claims, "test")) },
                },
            };
        }

        Api.Controllers.UsersController.UserInput Input(Guid role, IReadOnlyList<Guid>? regions) =>
            new($"rm-new-{org.S}", "Рустам", "Каримов", null, null, "РМ", role, "Pass-12345", NoVerifix: true, SalesRegionIds: regions);

        var rejected = await Users().Create(Input(rmRole.Id, []), default);
        var error = ((Microsoft.AspNetCore.Mvc.ObjectResult)rejected).Value!;
        Assert.Equal("Выберите регионы РМ.", error.GetType().GetProperty("error")!.GetValue(error));
        Assert.False(await db.Users.AnyAsync(u => u.Login == $"rm-new-{org.S}")); // без регионов пользователь не создан

        var created = (Microsoft.AspNetCore.Mvc.OkObjectResult)await Users().Create(Input(rmRole.Id, [org.A1]), default);
        var id = (Guid)created.Value!.GetType().GetProperty("Id")!.GetValue(created.Value)!;
        var rm = await ScopeOf(id);
        Assert.True(rm.IsRm);
        Assert.True(rm.CanSeeRegion(org.A1));
        Assert.False(rm.CanSeeRegion(org.B1));

        await Users().Update(id, Input(rmRole.Id, [org.A1, org.B1]) with { Password = null }, default);
        Assert.True((await ScopeOf(id)).CanSeeRegion(org.B1)); // правка регионов — сразу

        await Users().Update(id, Input(other.Id, null) with { Password = null }, default);
        Assert.True((await ScopeOf(id)).IsOrganization); // роль «Продажи» — охват по регионам снят
    }

    [PostgresFact]
    public async Task Assignment_is_validated_unique_for_an_agent_not_self_made_and_audited()
    {
        var org = await SeedAsync();
        await Assert.ThrowsAsync<FieldForbiddenException>(() => Assign(org.RmA, org.RmA, new SalesScopeInput(null, null, null, null)));
        await Assert.ThrowsAsync<FieldValidationException>(() => Assign(org.Admin, org.RmA, new SalesScopeInput(SalesScopeLevel.Rm, [], null, null)));
        await Assert.ThrowsAsync<FieldValidationException>(() => Assign(org.Admin, org.RmA, new SalesScopeInput(SalesScopeLevel.Rm, [Guid.NewGuid()], null, null)));
        await Assert.ThrowsAsync<FieldValidationException>(() => Assign(org.Admin, org.SvA, new SalesScopeInput(SalesScopeLevel.Supervisor, null, [Guid.NewGuid()], null)));
        await Assert.ThrowsAsync<FieldValidationException>(() => Assign(org.Admin, org.Tp1, new SalesScopeInput(SalesScopeLevel.Agent, null, null, -5)));
        await Assert.ThrowsAsync<FieldNotFoundException>(() => Assign(org.Admin, Guid.NewGuid(), new SalesScopeInput(null, null, null, null)));

        var view = await Assign(org.Admin, org.Tp1, new SalesScopeInput(SalesScopeLevel.Agent, null, null, org.Agent1));
        Assert.Equal($"ТП: Азиз Каримов {org.S}", view.Title);
        var conflict = await Assert.ThrowsAsync<FieldConflictException>(() => Assign(org.Admin, org.SvA, new SalesScopeInput(SalesScopeLevel.Agent, null, null, org.Agent1)));
        Assert.Contains($"tp-1 {org.S}", conflict.Message);

        var rm = await Assign(org.Admin, org.RmA, new SalesScopeInput(SalesScopeLevel.Rm, [org.DirA, org.DirA], null, null));
        Assert.Equal([org.DirA], rm.DirectionIds);
        Assert.Equal($"РМ: Джизак {org.S}, Самарканд {org.S}", rm.Title); // подпись РМ — его регионы (здесь — регионы направления)

        await With(async sp =>
        {
            var db = sp.GetRequiredService<OneBaseDbContext>();
            var logs = await db.AuditLogs.AsNoTracking().Where(l => l.Action == "users.sales_scope" && l.EntityId == org.RmA.ToString()).ToListAsync();
            Assert.Single(logs);
            Assert.Equal(org.Admin.ToString(), logs[0].ActorId);
            Assert.Contains("Rm", logs[0].Data);
            return true;
        });
    }
}
