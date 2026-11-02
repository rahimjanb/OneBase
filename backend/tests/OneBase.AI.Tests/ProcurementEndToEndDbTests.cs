using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Security;
using OneBase.AI.Tools;
using OneBase.Application.Field;
using OneBase.Application.Procurement;
using OneBase.Application.Security;
using OneBase.Domain.Identity;
using OneBase.Domain.Procurement;
using OneBase.Domain.Work;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// Финальный аудит снабжения — сквозной сценарий на настоящем PostgreSQL (ONEBASE_TEST_POSTGRES, иначе пропускается), права из ролей в базе:
/// групповая заявка (две позиции) → проверка остатков и резерв → только директор утверждает → поставщики и предложения → заказы → цена
/// выросла после утверждения — снова директор → частичная поставка с браком и недостачей → полная поставка → выдача отделу; на каждом шаге —
/// запреты ТЗ: двойной резерв, двойное оприходование, отрицательный остаток, обход директора, лишняя приёмка и выдача; в конце — история,
/// движения склада, уведомления, обзор, KPI и AI видят ту же закупку.
/// </summary>
public class ProcurementEndToEndDbTests(ConsultantDb fixture) : IClassFixture<ConsultantDb>
{
    private sealed record Org(User Employee, User Other, User Supply, User SupplyHead, User Director, User Keeper, User Admin, Guid Raw, string Tag);

    private static T Get<T>(IServiceProvider sp) where T : notnull => sp.GetRequiredService<T>();

    private static ProcurementRequestService R(IServiceProvider sp) => Get<ProcurementRequestService>(sp);

    private static ProcurementOrderService O(IServiceProvider sp) => Get<ProcurementOrderService>(sp);

    private static ProcurementReceiptService Rc(IServiceProvider sp) => Get<ProcurementReceiptService>(sp);

    private static ProcurementStockService S(IServiceProvider sp) => Get<ProcurementStockService>(sp);

    private async Task<T> As<T>(User user, Func<IServiceProvider, ProcurementContext, Task<T>> act)
    {
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var granted = await Get<IUserPermissions>(sp).GetAsync(user.Id);
        return await act(sp, await R(sp).ContextAsync(user.Id, granted, CancellationToken.None));
    }

    private Task As(User user, Func<IServiceProvider, ProcurementContext, Task> act) => As(user, async (sp, c) =>
    {
        await act(sp, c);
        return true;
    });

    private async Task<(decimal OnHand, decimal Reserved)> BalanceAsync(Guid item)
    {
        using var scope = fixture.Services.CreateScope();
        var b = await Get<OneBaseDbContext>(scope.ServiceProvider).ProcurementStockBalances.AsNoTracking().SingleAsync(x => x.ItemId == item);
        return (b.OnHand, b.Reserved);
    }

    private async Task<List<string>> NotificationsAsync(User user)
    {
        using var scope = fixture.Services.CreateScope();
        return await Get<OneBaseDbContext>(scope.ServiceProvider).UserNotifications.AsNoTracking().Where(n => n.RecipientId == user.Id).Select(n => n.Title).ToListAsync();
    }

    private async Task<Org> SeedAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        using var scope = fixture.Services.CreateScope();
        var db = Get<OneBaseDbContext>(scope.ServiceProvider);
        await DbSeeder.SeedProcurementAsync(db, CancellationToken.None);
        var supplyDept = await db.Departments.FirstOrDefaultAsync(d => d.Code == "supply");
        if (supplyDept is null)
        {
            supplyDept = new Department { Code = "supply", Name = "Снабжение" };
            db.Departments.Add(supplyDept);
        }

        var dept = new Department { Code = $"e2e-{tag}", Name = $"Цех {tag}" };
        Role Role(string name, params string[] codes)
        {
            var role = new Role { Name = name };
            foreach (var code in codes)
            {
                role.Permissions.Add(new RolePermission { Code = code });
            }

            return role;
        }

        var director = await db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Name == SystemRoles.Director);
        if (director is null)
        {
            director = new Role { Name = SystemRoles.Director };
            db.Roles.Add(director);
        }

        foreach (var code in new[] { Permissions.ProcurementRequest, Permissions.ProcurementRead, Permissions.ProcurementSettings }.Where(c => director.Permissions.All(p => p.Code != c)))
        {
            director.Permissions.Add(new RolePermission { Code = code });
        }

        var employee = Role($"Сотрудник {tag}", Permissions.ProcurementRequest);
        var supply = Role($"Снабжение {tag}", Permissions.ProcurementRequest, Permissions.ProcurementRead, Permissions.ProcurementManage);
        var keeper = Role($"Кладовщик {tag}", Permissions.ProcurementRequest, Permissions.ProcurementWarehouse);
        // Технический администратор (системная роль «Администратор»): все права снабжения, но не «Директор» — утверждать не может и рабочих
        // уведомлений снабжения не получает.
        var admin = await db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Name == SystemRoles.Admin);
        if (admin is null)
        {
            admin = new Role { Name = SystemRoles.Admin };
            db.Roles.Add(admin);
        }

        foreach (var code in new[] { Permissions.ProcurementRequest, Permissions.ProcurementRead, Permissions.ProcurementManage, Permissions.ProcurementSettings, Permissions.ProcurementWarehouse }
                     .Where(c => admin.Permissions.All(p => p.Code != c)))
        {
            admin.Permissions.Add(new RolePermission { Code = code });
        }
        User Person(string login, string name, Department department, Role role)
        {
            var user = new User { Login = $"{login}-{tag}", FullName = name, Department = department };
            user.Roles.Add(new UserRole { Role = role });
            return user;
        }

        var org = new Org(Person("e2e-emp", $"Технолог {tag}", dept, employee), Person("e2e-oth", $"Чужой {tag}", supplyDept, employee),
            Person("e2e-sup", $"Снабженец {tag}", supplyDept, supply), Person("e2e-head", $"Руководитель снабжения {tag}", supplyDept, supply),
            Person("e2e-dir", $"Директор {tag}", dept, director), Person("e2e-kpr", $"Кладовщик {tag}", supplyDept, keeper),
            Person("e2e-adm", $"Администратор {tag}", supplyDept, admin), Guid.Empty, tag);
        db.Users.AddRange(org.Employee, org.Other, org.Supply, org.SupplyHead, org.Director, org.Keeper, org.Admin);
        db.WorkDepartmentHeads.Add(new WorkDepartmentHead { Department = supplyDept, UserId = org.SupplyHead.Id });
        await db.SaveChangesAsync();
        return org with { Raw = await db.ProcurementCategories.Where(c => c.Code == "raw_materials").Select(c => c.Id).SingleAsync() };
    }

    [PostgresFact]
    public async Task Main_scenario_from_group_request_to_issue_keeps_stock_rights_and_history_consistent()
    {
        var org = await SeedAsync();
        var today = FieldClock.Today;
        // Тесты класса делят одну временную базу — обзор и KPI сравниваются с состоянием до сценария.
        var start = await As(org.Director, (sp, c) => Get<ProcurementAnalyticsService>(sp).DashboardAsync(c, ProcurementFilter.Of(today, today), default));

        // Склад: гранулы — 30 кг на складе, краска — карточка без остатка.
        var granules = await As(org.Keeper, (sp, c) => S(sp).CreateItemAsync(c, new ProcurementCatalogInput($"G-{org.Tag}", $"Гранулы {org.Tag}", org.Raw, "кг", null, null, 0), default));
        var paint = await As(org.Keeper, (sp, c) => S(sp).CreateItemAsync(c, new ProcurementCatalogInput($"P-{org.Tag}", $"Краска {org.Tag}", org.Raw, "кг", null, null, 5), default));
        await As(org.Keeper, (sp, c) => S(sp).ReceiptAsync(c, new ProcurementStockOperationInput([new(granules, 30, 12_000)], "Начальный остаток", "НАКЛ-0"), default));

        // 1–4. Групповая заявка из двух позиций: гранулы 100 кг (30 со склада — в резерв, 70 к закупке) и краска 10 кг (вся к закупке).
        var request = await As(org.Employee, (sp, c) => R(sp).CreateAsync(c, new ProcurementRequestInput("Материалы на ноябрь", null, ProcurementPriority.High,
            today.AddDays(14), "План производства", null,
            [new(null, null, null, null, 100, 15_000, null, granules, 30), new(null, null, null, null, 10, 50_000, null, paint, 0)], Submit: true), default));
        Assert.Equal((30m, 30m), await BalanceAsync(granules));
        var submitted = await As(org.Employee, (sp, c) => R(sp).GetAsync(c, request, default));
        Assert.Equal(70 * 15_000m + 10 * 50_000m, submitted.PurchaseTotal);
        var (gLine, pLine) = (submitted.Items[0].Id, submitted.Items[1].Id);

        // Двойной резерв невозможен; чужой сотрудник заявку не видит.
        await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Other, (sp, c) => R(sp).CreateAsync(c, new ProcurementRequestInput("Ещё гранулы", null,
            ProcurementPriority.Normal, null, "Нужно", null, [new(null, null, null, null, 5, 15_000, null, granules, 5)], Submit: true), default)));
        var hidden = await Record.ExceptionAsync(() => As(org.Other, (sp, c) => R(sp).GetAsync(c, request, default)));
        Assert.True(hidden is FieldNotFoundException or FieldForbiddenException, hidden?.GetType().Name);

        // 5–6. Снабжение передаёт директору. Утверждает только директор: не инициатор, не снабжение, не администратор со всеми правами.
        await As(org.Supply, (sp, c) => R(sp).ForwardAsync(c, request, default));
        foreach (var user in new[] { org.Employee, org.Supply, org.Admin })
        {
            await Assert.ThrowsAsync<FieldForbiddenException>(() => As(user, (sp, c) => R(sp).ApproveAsync(c, request, null, default)));
        }

        // Пока директор не утвердил — заказать нельзя.
        var supplierA = await As(org.Supply, (sp, c) => Get<ProcurementSupplierService>(sp).CreateAsync(c,
            new ProcurementSupplierInput($"ООО Полимер {org.Tag}", null, null, null, null, null, null, null, null, null, 3, null, [org.Raw]), default));
        var supplierB = await As(org.Supply, (sp, c) => Get<ProcurementSupplierService>(sp).CreateAsync(c,
            new ProcurementSupplierInput($"ООО Колор {org.Tag}", null, null, null, null, null, null, null, null, null, 3, null, [org.Raw]), default));
        ProcurementOrderInput Order(Guid supplier, Guid line, decimal qty, decimal price) =>
            new(supplier, null, org.Supply.Id, today.AddDays(5), null, 0, 12, true, 0, null, [new ProcurementOrderLineInput(line, qty, price)], Submit: true);
        Assert.NotNull(await Record.ExceptionAsync(() => As(org.Supply, (sp, c) => O(sp).CreateAsync(c, Order(supplierA, gLine, 70, 14_000), default))));
        await As(org.Director, (sp, c) => R(sp).ApproveAsync(c, request, "Согласовано", default));

        // 9–10. Предложения двух поставщиков: гранулы дешевле у «Полимера», краска — у «Колора».
        await As(org.Supply, (sp, c) => O(sp).CreateOfferAsync(c, request, new ProcurementOfferInput(supplierA, null, 3, 0, 12, true, null, null, null, null,
            [new(gLine, 14_000), new(pLine, 52_000)]), default));
        await As(org.Supply, (sp, c) => O(sp).CreateOfferAsync(c, request, new ProcurementOfferInput(supplierB, null, 5, 0, 12, true, null, null, null, null,
            [new(gLine, 14_500), new(pLine, 48_000)]), default));
        var comparison = await As(org.Director, (sp, c) => O(sp).CompareAsync(c, request, Get<ProcurementSupplierService>(sp), default));
        Assert.True(comparison.Offers.Single(o => o.SupplierId == supplierA).Lines.Single(l => l.RequestItemId == gLine).BestPrice);
        Assert.True(comparison.Offers.Single(o => o.SupplierId == supplierB).Lines.Single(l => l.RequestItemId == pLine).BestPrice);

        // 11. Заказы в пределах утверждённого — без директора; больше утверждённого заказать нельзя.
        var order1 = await As(org.Supply, (sp, c) => O(sp).CreateAsync(c, Order(supplierA, gLine, 70, 14_000), default));
        var order2 = await As(org.Supply, (sp, c) => O(sp).CreateAsync(c, Order(supplierB, pLine, 10, 48_000), default));
        Assert.Equal(ProcurementOrderStatus.Approved, (await As(org.Supply, (sp, c) => O(sp).GetAsync(c, order1, default))).Status);
        Assert.NotNull(await Record.ExceptionAsync(() => As(org.Supply, (sp, c) => O(sp).CreateAsync(c, Order(supplierA, gLine, 1, 14_000), default))));

        // 12. Цена выросла после утверждения (14 000 → 16 500, выше цены заявки) — заказ снова у директора, поставщику не отправить.
        var before = await As(org.Supply, (sp, c) => O(sp).GetAsync(c, order1, default));
        await As(org.Supply, (sp, c) => O(sp).UpdateAsync(c, order1, Order(supplierA, gLine, 70, 16_500) with { Version = before.Version }, default));
        Assert.Equal(ProcurementOrderStatus.AwaitingApproval, (await As(org.Supply, (sp, c) => O(sp).GetAsync(c, order1, default))).Status);
        await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Supply, (sp, c) => O(sp).MarkOrderedAsync(c, order1, null, default)));
        foreach (var user in new[] { org.Supply, org.Admin })
        {
            await Assert.ThrowsAsync<FieldForbiddenException>(() => As(user, (sp, c) => O(sp).ApproveAsync(c, order1, null, default)));
        }

        await As(org.Director, (sp, c) => O(sp).ApproveAsync(c, order1, "Цена выросла — согласовано", default));
        await As(org.Supply, (sp, c) => O(sp).MarkOrderedAsync(c, order1, null, default));
        await As(org.Supply, (sp, c) => O(sp).MarkOrderedAsync(c, order2, null, default));
        var o1 = await As(org.Supply, (sp, c) => O(sp).GetAsync(c, order1, default));
        var o2 = await As(org.Supply, (sp, c) => O(sp).GetAsync(c, order2, default));
        Assert.Equal((ProcurementOrderStatus.Ordered, 16_500m), (o1.Status, o1.Items[0].UnitPrice));

        // 13, 15, 16. Частичная поставка гранул: пришло 42, брак 2, по накладной не хватает 3 → принято 40, претензия.
        var receipt1 = await As(org.Keeper, (sp, c) => Rc(sp).CreateAsync(c, new ProcurementReceiptInput(o1.Id, "НАКЛ-1", null, [new(o1.Items[0].Id, 42, Defective: 2, Shortage: 3)]), default));
        Assert.Equal((30m, 30m), await BalanceAsync(granules)); // черновик склад не меняет
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Supply, (sp, c) => Rc(sp).PostAsync(c, receipt1, null, default)));
        await As(org.Keeper, (sp, c) => Rc(sp).PostAsync(c, receipt1, null, default));
        // Дважды оприходовать нельзя: повтор не меняет склад.
        var repost = await Record.ExceptionAsync(() => As(org.Keeper, (sp, c) => Rc(sp).PostAsync(c, receipt1, null, default)));
        Assert.True(repost is null or FieldConflictException, repost?.GetType().Name);
        Assert.Equal((70m, 70m), await BalanceAsync(granules));
        o1 = await As(org.Supply, (sp, c) => O(sp).GetAsync(c, order1, default));
        Assert.Equal((ProcurementOrderStatus.PartiallyDelivered, 40m), (o1.Status, o1.Items[0].ReceivedQty));
        var claim = Assert.Single(o1.Claims!);
        Assert.Equal((2m, 3m, ProcurementClaimStatus.Open), (claim.Defective, claim.Shortage, claim.Status));

        // 14. Остаток 30 приходит второй поставкой — заказ поставлен; лишнее не принимается.
        await As(org.Keeper, (sp, c) => Rc(sp).CreateAsync(c, new ProcurementReceiptInput(o1.Id, "НАКЛ-2", null, [new(o1.Items[0].Id, 30)], Post: true), default));
        await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Keeper, (sp, c) => Rc(sp).CreateAsync(c,
            new ProcurementReceiptInput(o1.Id, "НАКЛ-3", null, [new(o1.Items[0].Id, 1)], Post: true), default)));
        await As(org.Keeper, (sp, c) => Rc(sp).CreateAsync(c, new ProcurementReceiptInput(o2.Id, "НАКЛ-4", null, [new(o2.Items[0].Id, 10)], Post: true), default));
        Assert.Equal(ProcurementOrderStatus.Delivered, (await As(org.Supply, (sp, c) => O(sp).GetAsync(c, order1, default))).Status);
        Assert.Equal((100m, 100m), await BalanceAsync(granules));
        Assert.Equal((10m, 10m), await BalanceAsync(paint));

        // 19–20. Выдача цеху: больше остатка нельзя; повтор формы с тем же ключом — одна выдача; склад пуст, заявка выполнена.
        await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Keeper, (sp, c) => R(sp).IssueAsync(c, request, new([new(gLine, 101)], "Цех", null), default)));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Supply, (sp, c) => R(sp).IssueAsync(c, request, new([new(gLine, 100)], "Цех", null), default)));
        var key = Guid.NewGuid();
        var issue = await As(org.Keeper, (sp, c) => R(sp).IssueAsync(c, request, new([new(gLine, 100), new(pLine, 10)], "Начальник цеха", null, key), default));
        Assert.Equal(issue, await As(org.Keeper, (sp, c) => R(sp).IssueAsync(c, request, new([new(gLine, 100), new(pLine, 10)], "Начальник цеха", null, key), default)));
        Assert.Equal((0m, 0m), await BalanceAsync(granules));
        Assert.Equal((0m, 0m), await BalanceAsync(paint));

        // Отрицательный остаток невозможен: списать с пустого склада нельзя.
        Assert.NotNull(await Record.ExceptionAsync(() => As(org.Keeper, (sp, c) => S(sp).WriteOffAsync(c, new ProcurementStockOperationInput([new(granules, 1)], "Порча"), default))));
        Assert.Equal((0m, 0m), await BalanceAsync(granules));

        // История не теряется: заявка, заказ, движения склада с остатками до и после.
        var done = await As(org.Director, (sp, c) => R(sp).GetAsync(c, request, default));
        Assert.Equal(ProcurementRequestStatus.Fulfilled, done.Status);
        var actions = done.History.Select(h => h.Action).ToList();
        Assert.Equal(["created", "submitted", "forwarded", "approved"], actions.Take(4));
        Assert.Contains("issued", actions);
        o1 = await As(org.Supply, (sp, c) => O(sp).GetAsync(c, order1, default));
        Assert.True(o1.History.Count >= 5, string.Join(", ", o1.History.Select(h => h.Action)));
        Assert.Contains(o1.Approvals, a => a.Decision == ProcurementApprovalDecision.Approved && a.Comment == "Цена выросла — согласовано");
        var movements = (await As(org.Keeper, (sp, c) => S(sp).MovementsAsync(c, new ProcurementMovementSearch(granules), default))).Items;
        Assert.Equal(0m, movements.Sum(m => m.Quantity));
        Assert.All(movements, m => Assert.Equal(m.BalanceBefore + m.Quantity, m.BalanceAfter));
        Assert.All(movements, m => Assert.True(m.BalanceAfter >= 0));
        Assert.Equal([30m, 40m, 30m, -100m], movements.OrderBy(m => m.CreatedAt).Select(m => m.Quantity));

        // 23. Уведомления участникам: директору — на согласование и претензия, руководителю снабжения — претензия, кладовщику — к выдаче,
        // инициатору — поступление; технический администратор их не получает.
        Assert.Contains(await NotificationsAsync(org.Director), t => t.Contains("согласования"));
        Assert.Contains(await NotificationsAsync(org.Director), t => t.StartsWith("Претензия к «"));
        Assert.Contains(await NotificationsAsync(org.SupplyHead), t => t.StartsWith("Претензия к «"));
        Assert.Contains(await NotificationsAsync(org.Keeper), t => t.StartsWith("К выдаче"));
        Assert.Contains(await NotificationsAsync(org.Employee), t => t.StartsWith("Поступило на склад по заявке"));
        using (var scope = fixture.Services.CreateScope())
        {
            var db = Get<OneBaseDbContext>(scope.ServiceProvider);
            Assert.False(await db.UserNotifications.AnyAsync(n => n.RecipientId == org.Admin.Id && n.Link == $"/requests/{request}"));
            Assert.Equal(WorkTaskStatus.Done, (await db.WorkTasks.AsNoTracking().SingleAsync(t => t.Id == o1.TaskId)).Status);
        }

        // 21–22. Обзор и KPI директора — из этой закупки: заказано 70 × 16 500 + 10 × 48 000, одна претензия, заявка выполнена, поставки в срок.
        var dashboard = await As(org.Director, (sp, c) => Get<ProcurementAnalyticsService>(sp).DashboardAsync(c, ProcurementFilter.Of(today, today), default));
        Assert.Equal(start.Director!.OrderedTotal + 70 * 16_500m + 10 * 48_000m, dashboard.Director!.OrderedTotal);
        Assert.Equal((start.Kpis!.Claims + 1, start.Kpis.FulfilledRequests + 1), (dashboard.Kpis!.Claims, dashboard.Kpis.FulfilledRequests));
        Assert.Equal(100m, dashboard.Kpis.OnTimePercent);
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Employee, (sp, c) => Get<ProcurementAnalyticsService>(sp).DashboardAsync(c, ProcurementFilter.Of(today, today), default)));

        // 24. AI видит ту же закупку с правами пользователя: директору — цена гранул выросла к прошлой покупке, сотруднику — только своё.
        using (var scope = fixture.Services.CreateScope())
        {
            var executor = Get<ToolExecutor>(scope.ServiceProvider);
            var prices = await executor.ExecuteAsync(new ToolContext("supply", org.Director.Id, 0), "get_purchase_price_history",
                JsonSerializer.SerializeToElement(new { search = org.Tag }));
            Assert.True(prices.Success, prices.Content);
            var items = JsonDocument.Parse(prices.Content).RootElement.GetProperty("items").EnumerateArray().ToList();
            Assert.Contains(items, i => i.GetProperty("lastPrice").GetDecimal() == 16_500m);
            var denied = await executor.ExecuteAsync(new ToolContext("supply", org.Employee.Id, 0), "get_purchase_price_history", JsonSerializer.SerializeToElement(new { }));
            Assert.Equal(ToolFailures.Forbidden, denied.ErrorKind);
        }
    }

    [PostgresFact]
    public async Task Settings_categories_and_catalog_changes_are_written_to_the_audit_log()
    {
        var org = await SeedAsync();

        // Правила согласования и эскалации меняет директор (право настроек), сотрудник — нет.
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Employee, (sp, c) => R(sp).UpdateSettingsAsync(c, new ProcurementSettingsView(7, 2, 4), default)));
        await As(org.Director, (sp, c) => R(sp).UpdateSettingsAsync(c, new ProcurementSettingsView(7, 2, 4), default));
        var category = await As(org.Director, (sp, c) => R(sp).CreateCategoryAsync(c, new ProcurementCategoryInput($"Упаковка {org.Tag}", null), default));
        await As(org.Director, (sp, c) => R(sp).UpdateCategoryAsync(c, category, new ProcurementCategoryInput($"Упаковка и тара {org.Tag}", null), default));
        var item = await As(org.Keeper, (sp, c) => S(sp).CreateItemAsync(c, new ProcurementCatalogInput($"A-{org.Tag}", $"Плёнка {org.Tag}", org.Raw, "кг", null, null, 5), default));
        await As(org.Keeper, (sp, c) => S(sp).UpdateItemAsync(c, item, new ProcurementCatalogInput($"A-{org.Tag}", $"Плёнка {org.Tag}", org.Raw, "кг", null, null, 20), default));

        using var scope = fixture.Services.CreateScope();
        var logs = await Get<OneBaseDbContext>(scope.ServiceProvider).AuditLogs.AsNoTracking()
            .Where(a => a.ActorId == org.Director.Id.ToString() || a.ActorId == org.Keeper.Id.ToString())
            .OrderBy(a => a.Id).ToListAsync();
        Assert.Equal(["procurement.settings.updated", "procurement.category.created", "procurement.category.updated", "procurement.item.created", "procurement.item.updated"],
            logs.Where(a => a.Action.StartsWith("procurement.")).Select(a => a.Action));
        var settings = JsonDocument.Parse(logs.Single(a => a.Action == "procurement.settings.updated").Data!).RootElement;
        Assert.Equal(7m, settings.GetProperty("after").GetProperty("ReapprovalTolerancePercent").GetDecimal());
        var card = JsonDocument.Parse(logs.Single(a => a.Action == "procurement.item.updated").Data!).RootElement;
        Assert.Equal((5m, 20m), (card.GetProperty("before").GetProperty("MinQty").GetDecimal(), card.GetProperty("after").GetProperty("MinQty").GetDecimal()));
    }

    [PostgresFact]
    public async Task Overview_board_shows_actions_and_pipeline_by_role()
    {
        var org = await SeedAsync();
        var today = FieldClock.Today;
        async Task<ProcurementBoard> Board(User user) =>
            await As(user, (sp, c) => Get<ProcurementAnalyticsService>(sp).BoardAsync(c, ProcurementFilter.Of(today, today), default));

        var before = await Board(org.Director);
        var request = await As(org.Employee, (sp, c) => R(sp).CreateAsync(c, new ProcurementRequestInput($"Плёнка {org.Tag}", null, ProcurementPriority.Normal,
            null, "Упаковка", null, [new(null, $"Плёнка {org.Tag}", org.Raw, "кг", 10, 20_000)], Submit: true), default));
        await As(org.Supply, (sp, c) => R(sp).ForwardAsync(c, request, default));

        // Директор: заявка ждёт его решения — в итоге, на этапе «Согласование» и в карточке с суммой к закупке.
        var director = await Board(org.Director);
        Assert.True(director.Access.CanApprove);
        Assert.Equal(before.Pipeline.OnApprovalCount + 1, director.Pipeline.OnApprovalCount);
        Assert.Equal(before.Summary.PendingApprovalAmount + 200_000m, director.Summary.PendingApprovalAmount);
        var card = Assert.Single(director.Approvals!, a => a.Id == request);
        Assert.Equal((200_000m, $"Технолог {org.Tag}"), (card.Amount, card.Initiator));
        Assert.NotNull(director.Metrics);
        Assert.NotNull(director.Analytics);
        Assert.Empty(director.Errors);

        // Снабженец: без права согласования, команда — только он сам; руководитель снабжения видит всех с ролями.
        var buyer = await Board(org.Supply);
        Assert.False(buyer.Access.CanApprove);
        Assert.Null(buyer.Approvals);
        Assert.All(buyer.Team!, t => Assert.Equal(org.Supply.Id, t.UserId));
        var lead = await Board(org.SupplyHead);
        Assert.Contains(lead.Team!, t => t.UserId == org.SupplyHead.Id && t.Role == "Руководитель снабжения");
        Assert.Contains(lead.Team!, t => t.UserId == org.Supply.Id && t.Role == "Закупщик");

        // Кладовщик: этапы заказов и просрочки — да; заявки, показатели, аналитика и команда — нет. Сотруднику обзор закрыт.
        var keeper = await Board(org.Keeper);
        Assert.Null(keeper.Pipeline.RequestsAwaitingOrder);
        Assert.Null(keeper.Pipeline.OnApprovalCount);
        Assert.Null(keeper.Metrics);
        Assert.Null(keeper.Analytics);
        Assert.Null(keeper.Team);
        Assert.NotNull(keeper.Summary.OverdueDeliveries);
        await Assert.ThrowsAsync<FieldForbiddenException>(() => Board(org.Employee));

        // Путь: утверждена — ждёт заказа; заказ в работе; оформлен — в пути; срок наступил — ждёт приёмки (этапы не пересекаются).
        await As(org.Director, (sp, c) => R(sp).ApproveAsync(c, request, null, default));
        var approved = await Board(org.Director);
        Assert.Equal(director.Pipeline.RequestsAwaitingOrder + 1, approved.Pipeline.RequestsAwaitingOrder);
        var supplier = await As(org.Supply, (sp, c) => Get<ProcurementSupplierService>(sp).CreateAsync(c,
            new ProcurementSupplierInput($"ООО Плёнка {org.Tag}", null, null, null, null, null, null, null, null, null, 3, null, [org.Raw]), default));
        var line = (await As(org.Supply, (sp, c) => R(sp).GetAsync(c, request, default))).Items[0].Id;
        var order = await As(org.Supply, (sp, c) => O(sp).CreateAsync(c, new ProcurementOrderInput(supplier, null, org.Supply.Id, today.AddDays(3), null, 0, 12, true, 0, null,
            [new ProcurementOrderLineInput(line, 10, 19_000)], Submit: true), default));
        var ordered = await Board(org.Director);
        Assert.Equal((approved.Pipeline.OrdersInProgress + 1, approved.Pipeline.RequestsAwaitingOrder - 1), (ordered.Pipeline.OrdersInProgress, ordered.Pipeline.RequestsAwaitingOrder));
        await As(org.Supply, (sp, c) => O(sp).MarkOrderedAsync(c, order, null, default));
        var sent = await Board(org.Director);
        Assert.Equal((ordered.Pipeline.OrdersInProgress - 1, ordered.Pipeline.InTransit + 1, ordered.Pipeline.AwaitingReceipt), (sent.Pipeline.OrdersInProgress, sent.Pipeline.InTransit, sent.Pipeline.AwaitingReceipt));
        Assert.Contains(sent.Upcoming!, u => u.Id == order);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = Get<OneBaseDbContext>(scope.ServiceProvider);
            (await db.ProcurementPurchaseOrders.SingleAsync(o => o.Id == order)).ExpectedDate = today;
            await db.SaveChangesAsync();
        }

        var due = await Board(org.Keeper);
        Assert.Equal((sent.Pipeline.InTransit - 1, sent.Pipeline.AwaitingReceipt + 1), (due.Pipeline.InTransit, due.Pipeline.AwaitingReceipt));
    }
}
