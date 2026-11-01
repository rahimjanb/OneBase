using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Field;
using OneBase.Application.Procurement;
using OneBase.Application.Security;
using OneBase.Domain.Identity;
using OneBase.Domain.Procurement;
using OneBase.Domain.Work;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// Приёмка товара по заказу на настоящем PostgreSQL (ONEBASE_TEST_POSTGRES, иначе пропускается): черновик не меняет склад, подтверждение
/// приходует принятое (пришло − брак) и резервирует под заявку; частичные поставки; лишнее сверх заказанного не принимается; повтор
/// формы и одновременное подтверждение не проводят приёмку дважды; брак и недостача — претензия (директору и руководителю снабжения),
/// закрытие претензии и заказа; закрытие недопоставленного остатка освобождает его для нового заказа; выдача выполняет заявку;
/// напоминания о поставках и остатках — по одному разу.
/// </summary>
public class ProcurementReceiptDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly string[] Employee = [Permissions.ProcurementRequest];
    private static readonly string[] Supply = [Permissions.ProcurementRequest, Permissions.ProcurementRead, Permissions.ProcurementManage];
    private static readonly string[] Director = [Permissions.ProcurementRequest, Permissions.ProcurementRead, Permissions.ProcurementSettings];
    private static readonly string[] Keeper = [Permissions.ProcurementRequest, Permissions.ProcurementWarehouse];

    private sealed record Org(User Employee, User Supply, User Director, User Keeper, User Keeper2, User SupplyHead, Guid Category);

    private async Task<Org> SeedAsync()
    {
        var s = Guid.NewGuid().ToString("N")[..6];
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await DbSeeder.SeedProcurementAsync(db, CancellationToken.None);
        var supplyDept = await db.Departments.FirstOrDefaultAsync(d => d.Code == "supply");
        if (supplyDept is null)
        {
            supplyDept = new Department { Code = "supply", Name = "Снабжение" };
            db.Departments.Add(supplyDept);
        }

        var dept = new Department { Code = $"rc-{s}", Name = $"Бухгалтерия {s}" };
        var directorRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == SystemRoles.Director) ?? new Role { Name = SystemRoles.Director };
        Role WithRights(string name, string code)
        {
            var role = new Role { Name = name };
            role.Permissions.Add(new RolePermission { Code = code });
            return role;
        }

        var supplyRole = WithRights($"Снабжение {s}", Permissions.ProcurementManage);
        var keeperRole = WithRights($"Кладовщик {s}", Permissions.ProcurementWarehouse);
        User Person(string login, string name, Role? role = null)
        {
            var u = new User { Login = $"{login}-{s}", FullName = name, Department = dept };
            if (role is not null)
            {
                u.Roles.Add(new UserRole { Role = role });
            }

            return u;
        }

        var org = new Org(Person("rc-emp", "Бухгалтер"), Person("rc-sup", "Снабженец", supplyRole), Person("rc-dir", "Директор", directorRole),
            Person("rc-kpr", "Кладовщик", keeperRole), Person("rc-kpr2", "Кладовщик Два", keeperRole), Person("rc-head", "Руководитель снабжения", supplyRole),
            Guid.Empty);
        db.Users.AddRange(org.Employee, org.Supply, org.Director, org.Keeper, org.Keeper2, org.SupplyHead);
        db.WorkDepartmentHeads.Add(new WorkDepartmentHead { Department = supplyDept, UserId = org.SupplyHead.Id });
        await db.SaveChangesAsync();
        return org with { Category = await db.ProcurementCategories.Where(c => c.Code == "stationery").Select(c => c.Id).SingleAsync() };
    }

    private async Task<T> As<T>(User user, string[] permissions, Func<IServiceProvider, ProcurementContext, Task<T>> act)
    {
        using var scope = fixture.Services.CreateScope();
        var ctx = await scope.ServiceProvider.GetRequiredService<ProcurementRequestService>().ContextAsync(user.Id, permissions.ToHashSet(), CancellationToken.None);
        return await act(scope.ServiceProvider, ctx);
    }

    private Task As(User user, string[] permissions, Func<IServiceProvider, ProcurementContext, Task> act) => As(user, permissions, async (sp, c) =>
    {
        await act(sp, c);
        return true;
    });

    private static ProcurementRequestService R(IServiceProvider sp) => sp.GetRequiredService<ProcurementRequestService>();
    private static ProcurementOrderService O(IServiceProvider sp) => sp.GetRequiredService<ProcurementOrderService>();
    private static ProcurementReceiptService Rc(IServiceProvider sp) => sp.GetRequiredService<ProcurementReceiptService>();
    private static ProcurementStockService St(IServiceProvider sp) => sp.GetRequiredService<ProcurementStockService>();

    /// <summary>Утверждённая директором заявка на 100 пачек (без карточки склада) и заказ на них — отмечен заказанным.</summary>
    private async Task<(Guid Request, Guid RequestItem, Guid Order, Guid OrderItem)> OrderedAsync(Org org, DateOnly? expected = null)
    {
        var request = await As(org.Employee, Employee, (sp, c) => R(sp).CreateAsync(c, new ProcurementRequestInput("Бумага для отчётов", null, ProcurementPriority.Normal, null,
            "Квартальная отчётность", null, [new(null, "Бумага А4", org.Category, "пачка", 100, 45_000)], Submit: true), default));
        await As(org.Supply, Supply, (sp, c) => R(sp).ForwardAsync(c, request, default));
        await As(org.Director, Director, (sp, c) => R(sp).ApproveAsync(c, request, null, default));
        var requestItem = (await As(org.Supply, Supply, (sp, c) => R(sp).GetAsync(c, request, default))).Items[0].Id;
        var supplier = await As(org.Supply, Supply, (sp, c) => sp.GetRequiredService<ProcurementSupplierService>().CreateAsync(c,
            new ProcurementSupplierInput($"ООО Бумага {Guid.NewGuid():N}"[..20], null, null, null, null, null, null, null, null, null, 3, null, [org.Category]), default));
        var order = await As(org.Supply, Supply, (sp, c) => O(sp).CreateAsync(c, new ProcurementOrderInput(supplier, null, org.Supply.Id,
            expected ?? FieldClock.Today.AddDays(5), null, 0, 0, true, 0, null, [new ProcurementOrderLineInput(requestItem, 100, 44_000)], Submit: true), default));
        await As(org.Supply, Supply, (sp, c) => O(sp).MarkOrderedAsync(c, order, null, default));
        var orderItem = (await As(org.Supply, Supply, (sp, c) => O(sp).GetAsync(c, order, default))).Items[0].Id;
        return (request, requestItem, order, orderItem);
    }

    private Task<Guid> CardAsync(Org org, string name, decimal minQty = 0) =>
        As(org.Keeper, Keeper, (sp, c) => St(sp).CreateItemAsync(c, new ProcurementCatalogInput($"RC-{Guid.NewGuid():N}"[..12], name, org.Category, "пачка", null, null, minQty), default));

    private async Task<(decimal OnHand, decimal Reserved)> BalanceAsync(Guid item)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var b = await db.ProcurementStockBalances.AsNoTracking().Where(x => x.ItemId == item).Select(x => new { x.OnHand, x.Reserved }).FirstOrDefaultAsync();
        return (b?.OnHand ?? 0, b?.Reserved ?? 0);
    }

    private async Task<List<string>> NotificationsAsync(User user)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        return await db.UserNotifications.AsNoTracking().Where(n => n.RecipientId == user.Id).OrderBy(n => n.CreatedAt).Select(n => n.Title).ToListAsync();
    }

    [PostgresFact]
    public async Task Partial_deliveries_change_stock_only_when_confirmed_and_end_with_a_fulfilled_request()
    {
        var org = await SeedAsync();
        var (request, _, order, orderItem) = await OrderedAsync(org);
        var card = await CardAsync(org, "Бумага А4 (склад)");

        // Черновик: склад и заказ не меняются; подтверждает только кладовщик.
        var draft = await As(org.Keeper, Keeper, (sp, c) => Rc(sp).CreateAsync(c,
            new ProcurementReceiptInput(order, "НАКЛ-17", null, [new(orderItem, 62, Defective: 2, ItemId: card)]), default));
        Assert.Equal((0m, 0m), await BalanceAsync(card));
        Assert.Equal(ProcurementOrderStatus.Ordered, (await As(org.Supply, Supply, (sp, c) => O(sp).GetAsync(c, order, default))).Status);
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Supply, Supply, (sp, c) => Rc(sp).PostAsync(c, draft, null, default)));
        await As(org.Keeper, Keeper, (sp, c) => Rc(sp).PostAsync(c, draft, null, default));

        // Принято 60 (62 − 2 брака): на складе и в резерве под заявку; заказ — поставлен частично; брак — претензия.
        Assert.Equal((60m, 60m), await BalanceAsync(card));
        var detail = await As(org.Supply, Supply, (sp, c) => O(sp).GetAsync(c, order, default));
        Assert.Equal((ProcurementOrderStatus.PartiallyDelivered, 60m), (detail.Status, detail.Items[0].ReceivedQty));
        Assert.Equal(ProcurementClaimStatus.Open, Assert.Single(detail.Claims!).Status);
        var req = await As(org.Supply, Supply, (sp, c) => R(sp).GetAsync(c, request, default));
        Assert.Equal((60m, card, ProcurementItemStatus.Ordered), (req.Items[0].ReceivedQty, req.Items[0].ItemId!.Value, req.Items[0].Status));
        Assert.Contains(await NotificationsAsync(org.Employee), t => t.StartsWith("Поступило на склад по заявке"));
        Assert.Contains(await NotificationsAsync(org.Director), t => t.StartsWith("Претензия к «"));
        Assert.Contains(await NotificationsAsync(org.SupplyHead), t => t.StartsWith("Претензия к «"));
        Assert.Contains(await NotificationsAsync(org.Keeper2), t => t.StartsWith("К выдаче: поступило по заказу"));

        // Лишнее не принимается: ждёт поставки 40 (брак остался за поставщиком).
        await Assert.ThrowsAsync<FieldValidationException>(() => As(org.Keeper, Keeper, (sp, c) => Rc(sp).CreateAsync(c,
            new ProcurementReceiptInput(order, null, null, [new(orderItem, 41)], Post: true), default)));

        // Вторая поставка сразу подтверждена; повтор формы с тем же ключом — та же приёмка, склад не удваивается.
        var key = Guid.NewGuid();
        var second = await As(org.Keeper, Keeper, (sp, c) => Rc(sp).CreateAsync(c, new ProcurementReceiptInput(order, "НАКЛ-18", null, [new(orderItem, 40)], Post: true, key), default));
        Assert.Equal(second, await As(org.Keeper, Keeper, (sp, c) => Rc(sp).CreateAsync(c, new ProcurementReceiptInput(order, "НАКЛ-18", null, [new(orderItem, 40)], Post: true, key), default)));
        Assert.Equal((100m, 100m), await BalanceAsync(card));
        detail = await As(org.Director, Director, (sp, c) => O(sp).GetAsync(c, order, default));
        Assert.Equal(ProcurementOrderStatus.Delivered, detail.Status);
        Assert.NotNull(detail.DeliveredAt);
        Assert.Equal(2, detail.Receipts!.Count(r => r.Status == ProcurementStockDocumentStatus.Posted));
        Assert.Contains(await NotificationsAsync(org.Director), t => t.StartsWith($"Заказ {detail.Number} поставлен полностью"));
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            Assert.Equal(WorkTaskStatus.Done, (await db.WorkTasks.AsNoTracking().SingleAsync(t => t.Id == detail.TaskId)).Status);
            Assert.Equal(2, await db.ProcurementStockMovements.CountAsync(m => m.ItemId == card && m.Kind == ProcurementMovementKind.Receipt && m.UnitCost == 44_000));
        }

        req = await As(org.Supply, Supply, (sp, c) => R(sp).GetAsync(c, request, default));
        Assert.Equal((100m, ProcurementItemStatus.Received), (req.Items[0].ReceivedQty, req.Items[0].Status));

        // Закрыть заказ можно после решения по претензии (только снабжение и с текстом решения).
        await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Supply, Supply, (sp, c) => O(sp).CompleteAsync(c, order, default)));
        var claim = detail.Claims!.Single().LineId;
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Keeper, Keeper, (sp, c) => Rc(sp).ResolveClaimAsync(c, claim, "Заменят", default)));
        await Assert.ThrowsAsync<FieldValidationException>(() => As(org.Supply, Supply, (sp, c) => Rc(sp).ResolveClaimAsync(c, claim, " ", default)));
        await As(org.Supply, Supply, (sp, c) => Rc(sp).ResolveClaimAsync(c, claim, "Поставщик заменит 2 пачки со следующей поставкой", default));
        await As(org.Supply, Supply, (sp, c) => O(sp).CompleteAsync(c, order, default));
        Assert.Equal(ProcurementOrderStatus.Completed, (await As(org.Supply, Supply, (sp, c) => O(sp).GetAsync(c, order, default))).Status);

        // Выдача инициатору из резерва — заявка выполнена.
        await As(org.Keeper, Keeper, (sp, c) => R(sp).IssueAsync(c, request, new([new(req.Items[0].Id, 100)], "Бухгалтер", null), default));
        Assert.Equal(ProcurementRequestStatus.Fulfilled, (await As(org.Employee, Employee, (sp, c) => R(sp).GetAsync(c, request, default))).Status);
        Assert.Equal((0m, 0m), await BalanceAsync(card));

        // Рейтинг поставщика: 1 из 2 строк приёмки с браком, поставлено всё заказанное.
        var rating = (await As(org.Supply, Supply, (sp, c) => sp.GetRequiredService<ProcurementSupplierService>().GetAsync(c, detail.SupplierId, default))).Rating;
        Assert.Equal((50m, 100m, 2, 1), (rating.Claims!.Value, rating.Completeness!.Value, rating.ReceiptLines, rating.ClaimLines));
    }

    [PostgresFact]
    public async Task A_receipt_is_never_posted_twice_and_closing_the_remainder_frees_it_for_a_new_order()
    {
        var org = await SeedAsync();
        var (request, requestItem, order, orderItem) = await OrderedAsync(org);
        var card = await CardAsync(org, "Бумага А4 (двойная приёмка)");
        var first = await As(org.Keeper, Keeper, (sp, c) => Rc(sp).CreateAsync(c, new ProcurementReceiptInput(order, null, null, [new(orderItem, 60, ItemId: card)]), default));
        var second = await As(org.Keeper2, Keeper, (sp, c) => Rc(sp).CreateAsync(c, new ProcurementReceiptInput(order, null, null, [new(orderItem, 60, ItemId: card)]), default));

        // Два кладовщика подтверждают одновременно: 60 + 60 > 100 — проходит только одна приёмка.
        var results = await Task.WhenAll(new[] { (org.Keeper, first), (org.Keeper2, second) }.Select(async x =>
        {
            try
            {
                await As(x.Item1, Keeper, (sp, c) => Rc(sp).PostAsync(c, x.Item2, null, default));
                return true;
            }
            catch (Exception e) when (e is FieldConflictException or DbUpdateConcurrencyException)
            {
                return false;
            }
        }));
        Assert.Equal(1, results.Count(ok => ok));
        Assert.Equal((60m, 60m), await BalanceAsync(card));
        var posted = results[0] ? first : second;
        var stale = results[0] ? second : first;
        await As(org.Keeper, Keeper, (sp, c) => Rc(sp).PostAsync(c, posted, null, default)); // повтор — ничего не меняет
        Assert.Equal((60m, 60m), await BalanceAsync(card));
        await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Keeper, Keeper, (sp, c) => Rc(sp).DeleteAsync(c, posted, default)));
        await As(org.Keeper, Keeper, (sp, c) => Rc(sp).DeleteAsync(c, stale, default));

        // Остаток 40 не довезут: закрывается с причиной, снова доступен для заказа; поставок по заказу больше не ждут.
        await Assert.ThrowsAsync<FieldValidationException>(() => As(org.Supply, Supply, (sp, c) => O(sp).CloseRemainderAsync(c, order, null, default)));
        await As(org.Supply, Supply, (sp, c) => O(sp).CloseRemainderAsync(c, order, "Поставщик снял позицию с производства", default));
        var detail = await As(org.Supply, Supply, (sp, c) => O(sp).GetAsync(c, order, default));
        Assert.Equal((ProcurementOrderStatus.Delivered, 40m), (detail.Status, detail.Items[0].ClosedQty));
        var req = await As(org.Supply, Supply, (sp, c) => R(sp).GetAsync(c, request, default));
        Assert.Equal((60m, 60m, ProcurementItemStatus.ToPurchase), (req.Items[0].OrderedQty, req.Items[0].ReceivedQty, req.Items[0].Status));
        var need = Assert.Single(await As(org.Supply, Supply, (sp, c) => O(sp).NeedsAsync(c, request, default)));
        Assert.Equal((requestItem, 40m), (need.RequestItemId, need.Remaining));
        Assert.Contains(await NotificationsAsync(org.Employee), t => t.StartsWith("Недопоставка по заявке"));
        Assert.Contains(await NotificationsAsync(org.Director), t => t.Contains("закрыт с недопоставкой"));
        await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Keeper, Keeper, (sp, c) => Rc(sp).CreateAsync(c,
            new ProcurementReceiptInput(order, null, null, [new(orderItem, 10, ItemId: card)]), default)));
    }

    [PostgresFact]
    public async Task Concurrent_receipts_that_fit_the_order_both_go_through_into_one_reserve()
    {
        var org = await SeedAsync();
        var (request, _, order, orderItem) = await OrderedAsync(org);
        var card = await CardAsync(org, "Бумага А4 (две приёмки)");
        var a = await As(org.Keeper, Keeper, (sp, c) => Rc(sp).CreateAsync(c, new ProcurementReceiptInput(order, null, null, [new(orderItem, 50, ItemId: card)]), default));
        var b = await As(org.Keeper2, Keeper, (sp, c) => Rc(sp).CreateAsync(c, new ProcurementReceiptInput(order, null, null, [new(orderItem, 30, ItemId: card)]), default));
        await Task.WhenAll(As(org.Keeper, Keeper, (sp, c) => Rc(sp).PostAsync(c, a, null, default)), As(org.Keeper2, Keeper, (sp, c) => Rc(sp).PostAsync(c, b, null, default)));

        Assert.Equal((80m, 80m), await BalanceAsync(card));
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var reserve = await db.ProcurementStockReservations.AsNoTracking().SingleAsync(x => x.RequestId == request && x.Status == ProcurementReservationStatus.Active);
        Assert.Equal(80m, reserve.Quantity);
        Assert.Equal((ProcurementOrderStatus.PartiallyDelivered, 80m),
            await db.ProcurementPurchaseOrders.Where(o => o.Id == order).Select(o => new ValueTuple<ProcurementOrderStatus, decimal>(o.Status, o.Items.Sum(i => i.ReceivedQty))).SingleAsync());
    }

    [PostgresFact]
    public async Task Positions_without_a_catalog_card_need_one_with_the_same_unit()
    {
        var org = await SeedAsync();
        var (_, _, order, orderItem) = await OrderedAsync(org);
        var kg = await As(org.Keeper, Keeper, (sp, c) => St(sp).CreateItemAsync(c, new ProcurementCatalogInput($"KG-{Guid.NewGuid():N}"[..12], "Бумага на вес", org.Category, "кг", null, null, 0), default));
        await Assert.ThrowsAsync<FieldValidationException>(() => As(org.Keeper, Keeper, (sp, c) => Rc(sp).CreateAsync(c,
            new ProcurementReceiptInput(order, null, null, [new(orderItem, 10, ItemId: kg)]), default)));
        var draft = await As(org.Keeper, Keeper, (sp, c) => Rc(sp).CreateAsync(c, new ProcurementReceiptInput(order, null, null, [new(orderItem, 10)]), default));
        await Assert.ThrowsAsync<FieldValidationException>(() => As(org.Keeper, Keeper, (sp, c) => Rc(sp).PostAsync(c, draft, null, default)));

        // Только недостача (ничего не принято) — карточка не нужна, претензия есть, заказ ждёт поставки дальше.
        var shortage = await As(org.Keeper, Keeper, (sp, c) => Rc(sp).CreateAsync(c, new ProcurementReceiptInput(order, "НАКЛ-5", null, [new(orderItem, 0, Shortage: 5)], Post: true), default));
        var receipt = await As(org.Director, Director, (sp, c) => Rc(sp).GetAsync(c, shortage, default));
        Assert.Equal((ProcurementStockDocumentStatus.Posted, ProcurementClaimStatus.Open, 0m), (receipt.Status, receipt.Lines[0].ClaimStatus, receipt.Lines[0].Accepted));
        Assert.Equal(ProcurementOrderStatus.Ordered, (await As(org.Supply, Supply, (sp, c) => O(sp).GetAsync(c, order, default))).Status);
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Employee, Employee, (sp, c) => Rc(sp).GetAsync(c, shortage, default)));
    }

    [PostgresFact]
    public async Task Reminders_about_deliveries_and_low_stock_are_sent_once()
    {
        var org = await SeedAsync();
        var today = FieldClock.Today;
        var (_, _, order, _) = await OrderedAsync(org, today.AddDays(1));
        var low = await CardAsync(org, $"Скрепки {Guid.NewGuid():N}"[..16], minQty: 10);
        using (var scope = fixture.Services.CreateScope())
        {
            var reminders = scope.ServiceProvider.GetRequiredService<ProcurementReminderService>();
            await reminders.RemindAsync(today, default);
            await reminders.RemindAsync(today, default);
        }

        var keeper = await NotificationsAsync(org.Keeper);
        Assert.Single(keeper, t => t.StartsWith("Завтра поставка по заказу"));
        Assert.Single(keeper, t => t.Contains("минимум") || t.StartsWith("Ниже минимального остатка"));
        Assert.Contains(await NotificationsAsync(org.Supply), t => t.Contains("минимум") || t.StartsWith("Ниже минимального остатка"));

        // Срок прошёл: директору и руководителю снабжения — один раз (ответственному напоминает его задача).
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            await db.ProcurementPurchaseOrders.Where(o => o.Id == order).ExecuteUpdateAsync(x => x.SetProperty(o => o.ExpectedDate, today.AddDays(-1)));
            var reminders = scope.ServiceProvider.GetRequiredService<ProcurementReminderService>();
            await reminders.RemindAsync(today, default);
            await reminders.RemindAsync(today, default);
        }

        Assert.Single(await NotificationsAsync(org.Director), t => t.StartsWith("Поставка просрочена"));
        Assert.Single(await NotificationsAsync(org.SupplyHead), t => t.StartsWith("Поставка просрочена"));
        Assert.DoesNotContain(await NotificationsAsync(org.Supply), t => t.StartsWith("Поставка просрочена"));

        // Остаток вернулся к минимуму — отметка снимается, следующее падение даст новое уведомление.
        await As(org.Keeper, Keeper, (sp, c) => St(sp).ReceiptAsync(c, new ProcurementStockOperationInput([new(low, 15, 1_000)], null, "НАКЛ-9"), default));
        using (var scope = fixture.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ProcurementReminderService>().RemindAsync(today, default);
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            Assert.Null(await db.ProcurementItems.Where(i => i.Id == low).Select(i => i.LowStockNotifiedAt).SingleAsync());
        }
    }
}
