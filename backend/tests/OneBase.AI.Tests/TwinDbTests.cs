using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Field;
using OneBase.Application.Hr;
using OneBase.Application.Security;
using OneBase.Application.Twin;
using OneBase.Domain.Identity;
using OneBase.Domain.Procurement;
using OneBase.Domain.Work;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// Цифровой двойник на настоящем PostgreSQL (ONEBASE_TEST_POSTGRES, иначе пропускается): показатели отдела — те же, что у разделов «Задачи» и
/// «Заявки» (просрочка, на проверке, на согласовании, срочные, «Сырьё», просрочка закупки), черновики и закрытые заявки не считаются; без прав —
/// блоки noaccess, а не нули; привязки зданий меняет только администратор пользователей, с проверкой ключа, отделов и складов и записью в аудит.
/// </summary>
public class TwinDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly HashSet<string> Director =
        [Permissions.UsersManage, Permissions.WorkTasks, Permissions.ProcurementRequest, Permissions.ProcurementRead, Permissions.AgentsRun];

    private sealed record Org(Department Dept, User Employee, User Director);

    private async Task<Org> SeedAsync(string s)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await DbSeeder.SeedProcurementAsync(db, CancellationToken.None);
        var dept = new Department { Code = $"tw-{s}", Name = $"Цех двойника {s}" };
        var employee = new User { Login = $"tw-emp-{s}", FullName = "Сотрудник цеха", Department = dept };
        var director = new User { Login = $"tw-dir-{s}", FullName = "Директор двойника" };
        db.Users.AddRange(employee, director);
        await db.SaveChangesAsync();
        return new Org(dept, employee, director);
    }

    private async Task<T> Run<T>(Func<TwinService, Task<T>> action)
    {
        using var scope = fixture.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<TwinService>());
    }

    [PostgresFact]
    public async Task Department_metrics_match_tasks_and_requests()
    {
        var s = Guid.NewGuid().ToString("N")[..6];
        var org = await SeedAsync(s);
        var today = FieldClock.Today;

        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var raw = await db.ProcurementCategories.FirstAsync(c => c.Code == "raw_materials");
            var unit = await db.ProcurementUnits.Select(u => u.Code).FirstAsync();
            db.WorkTasks.AddRange(
                new WorkTask { DepartmentId = org.Dept.Id, Title = "Просроченная", DueDate = today.AddDays(-2), Status = WorkTaskStatus.InProgress },
                new WorkTask { DepartmentId = org.Dept.Id, Title = "В срок", DueDate = today.AddDays(3) },
                new WorkTask { DepartmentId = org.Dept.Id, Title = "На проверке", Status = WorkTaskStatus.Review, IsBlocked = true },
                new WorkTask { DepartmentId = org.Dept.Id, Title = "Готово", Status = WorkTaskStatus.Done, CompletedAt = DateTimeOffset.UtcNow });

            ProcurementRequest Request(string n, ProcurementRequestStatus status, ProcurementPriority priority = ProcurementPriority.Normal, DateOnly? desired = null) =>
                new()
                {
                    Number = $"TW-{s}-{n}", Title = $"Заявка {n}", DepartmentId = org.Dept.Id, InitiatorId = org.Employee.Id, Status = status, Priority = priority,
                    DesiredDate = desired, SubmittedAt = DateTimeOffset.UtcNow, PurchaseTotal = 1000,
                    Items = [new ProcurementRequestItem { LineNo = 1, Name = "Мука", CategoryId = raw.Id, UnitCode = unit, Quantity = 5 }],
                };

            db.ProcurementRequests.AddRange(
                Request("1", ProcurementRequestStatus.Approved, desired: today.AddDays(-1)),
                Request("2", ProcurementRequestStatus.HeadApproval, ProcurementPriority.Urgent),
                Request("3", ProcurementRequestStatus.Fulfilled),
                Request("4", ProcurementRequestStatus.Draft));
            await db.SaveChangesAsync();
        }

        var snapshot = await Run(t => t.SnapshotAsync(org.Director.Id, Director, default));
        var dept = snapshot.Departments.Single(d => d.Code == org.Dept.Code);

        Assert.Equal(TwinStates.Ok, dept.Tasks.State);
        Assert.Equal(new TwinDeptTasks(3, 1, 1, 1), dept.Tasks.Data);

        // Открыты утверждённая и на согласовании; закрытая и черновик не считаются. «Сырьё» — в обеих, просрочена закупка по первой.
        Assert.Equal(TwinStates.Ok, dept.Supply.State);
        Assert.Equal(new TwinDeptSupply(2, 1, 1, 0, 2, 1, 2000), dept.Supply.Data);
        Assert.True(snapshot.Supply.Data!.ReadsAll);
        Assert.True(snapshot.Supply.Data.OverdueToBuy >= 1);

        // Закупок по отделу в месяце нет — 0, а не «нет данных»; пользователи отдела — администратору пользователей.
        Assert.Equal(new TwinDeptSpend(0, 0), dept.Spend.Data);
        Assert.Equal(1, dept.Users);
        Assert.True(snapshot.CanEdit);
        Assert.Equal(TwinSite.Buildings.Select(b => b.Key), snapshot.Objects.Select(o => o.Key));
        Assert.Equal(TwinStates.Ok, snapshot.Alerts.State);
    }

    [PostgresFact]
    public async Task Without_permissions_blocks_are_noaccess()
    {
        var org = await SeedAsync(Guid.NewGuid().ToString("N")[..6]);

        var snapshot = await Run(t => t.SnapshotAsync(org.Employee.Id, new HashSet<string>(), default));

        Assert.False(snapshot.CanEdit);
        Assert.All(new[] { snapshot.Sales.State, snapshot.Primary.State, snapshot.Plant.State, snapshot.Finance.State, snapshot.Promo.State,
            snapshot.Alerts.State, snapshot.Tasks.State, snapshot.Hr.State, snapshot.Supply.State, snapshot.Stock.State }, s => Assert.Equal(TwinStates.NoAccess, s));
        var dept = snapshot.Departments.Single(d => d.Code == org.Dept.Code);
        Assert.Equal(TwinStates.NoAccess, dept.Tasks.State);
        Assert.Equal(TwinStates.NoAccess, dept.Supply.State);
        Assert.Equal(TwinStates.NoAccess, dept.Hr.State);
        Assert.Null(dept.Tasks.Data);
        Assert.Null(dept.Users);

        // Сотрудник с правом заявок видит только свои: заявки отдела без его участия в счёт не идут, склад и просрочка закупки скрыты.
        var own = await Run(t => t.SnapshotAsync(org.Employee.Id, new HashSet<string> { Permissions.ProcurementRequest }, default));
        Assert.Equal(TwinStates.Ok, own.Supply.State);
        Assert.False(own.Supply.Data!.ReadsAll);
        Assert.Null(own.Supply.Data.OverdueToBuy);
        Assert.Equal(TwinStates.NoAccess, own.Stock.State);
        Assert.Equal(TwinStates.NoAccess, own.Departments.Single(d => d.Code == org.Dept.Code).Spend.State);
    }

    [PostgresFact]
    public async Task Links_are_edited_by_users_admin_with_audit()
    {
        var org = await SeedAsync(Guid.NewGuid().ToString("N")[..6]);
        Guid warehouse;
        using (var scope = fixture.Services.CreateScope())
        {
            warehouse = await scope.ServiceProvider.GetRequiredService<OneBaseDbContext>().ProcurementWarehouses.Where(w => w.IsDefault).Select(w => w.Id).FirstAsync();
        }

        var input = new TwinObjectInput("  Склад сырья  ", [org.Dept.Id, org.Dept.Id], [warehouse], []);
        await Assert.ThrowsAsync<FieldForbiddenException>(() => Run(t => t.UpdateAsync(org.Employee.Id, new HashSet<string> { Permissions.WorkTasks }, "B", input, default)));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => Run(t => t.OptionsAsync(new HashSet<string>(), default)));
        await Assert.ThrowsAsync<FieldNotFoundException>(() => Run(t => t.UpdateAsync(org.Director.Id, Director, "Z", input, default)));
        await Assert.ThrowsAsync<FieldValidationException>(() =>
            Run(t => t.UpdateAsync(org.Director.Id, Director, "B", input with { DepartmentIds = [Guid.NewGuid()] }, default)));
        await Assert.ThrowsAsync<FieldValidationException>(() =>
            Run(t => t.UpdateAsync(org.Director.Id, Director, "B", input with { WarehouseIds = [Guid.NewGuid()] }, default)));
        await Assert.ThrowsAsync<FieldValidationException>(() => Run(t => t.UpdateAsync(org.Director.Id, Director, "B", input with { PlantIds = [-1] }, default)));
        await Assert.ThrowsAsync<FieldValidationException>(() =>
            Run(t => t.UpdateAsync(org.Director.Id, Director, "B", input with { Name = new string('я', TwinService.MaxNameLength + 1) }, default)));

        var options = await Run(t => t.OptionsAsync(Director, default));
        Assert.Contains(options.Departments, d => d.Id == org.Dept.Id.ToString() && d.Code == org.Dept.Code);
        Assert.Contains(options.Warehouses, w => w.Id == warehouse.ToString());

        var view = await Run(t => t.UpdateAsync(org.Director.Id, Director, "B", input, default));
        Assert.Equal("Склад сырья", view.Name);
        Assert.Equal("Северный корпус", view.DefaultName);
        Assert.Equal([(TwinLinkKinds.Department, org.Dept.Code), (TwinLinkKinds.Warehouse, (string?)null)], view.Links.Select(l => (l.Kind, l.Code)));

        // Замена целиком: отдел снят, склад остался; пустое название — из модели.
        view = await Run(t => t.UpdateAsync(org.Director.Id, Director, "B", new TwinObjectInput(" ", [], [warehouse], null), default));
        Assert.Equal("Северный корпус", view.Name);
        Assert.Equal([TwinLinkKinds.Warehouse], view.Links.Select(l => l.Kind));

        var snapshot = await Run(t => t.SnapshotAsync(org.Employee.Id, new HashSet<string>(), default));
        Assert.Equal([warehouse.ToString()], snapshot.Objects.Single(o => o.Key == "B").Links.Select(l => l.Id));
        Assert.Empty(snapshot.Objects.Single(o => o.Key == "A").Links);

        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            Assert.Equal(2, await db.AuditLogs.CountAsync(a => a.Action == "twin.object.updated" && a.EntityId == "B" && a.ActorId == org.Director.Id.ToString()));
            Assert.Equal(1, await db.TwinObjectLinks.CountAsync(l => l.Object!.Key == "B"));
        }
    }

    [Fact]
    public void Divisions_belong_to_nearest_linked_ancestor()
    {
        HrDivisionView D(long id, long? parent, string? code) => new(id, $"П{id}", parent, code, false, 0);
        var map = TwinService.DivisionDepartments([D(1, null, "production"), D(2, 1, null), D(3, 2, null), D(4, 2, "supply"), D(5, 4, null), D(6, null, null), D(7, 99, null)]);

        Assert.Equal(new Dictionary<long, string> { [1] = "production", [2] = "production", [3] = "production", [4] = "supply", [5] = "supply" }, map);
    }
}
