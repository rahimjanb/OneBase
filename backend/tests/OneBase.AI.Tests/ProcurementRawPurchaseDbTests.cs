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
/// Закупка сырья на настоящем PostgreSQL (ONEBASE_TEST_POSTGRES, иначе пропускается): маршрут сырья «руководитель снабжения → директор» —
/// последовательно и независимо от суммы, пропустить шаг нельзя, остальные заявки — прежним маршрутом; поставка по тендеру — на склад
/// партиями на контроль, в единице закупки; «Куплено» позиции с партиями — на контроль, резерв под заявку после допуска, брак — снова к
/// закупке; единицы закупки на складе; история цен — заказы, тендеры и приходы без двойного учёта.
/// </summary>
public class ProcurementRawPurchaseDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly string[] Employee = [Permissions.ProcurementRequest];
    private static readonly string[] Supply = [Permissions.ProcurementRequest, Permissions.ProcurementRead, Permissions.ProcurementManage];
    private static readonly string[] Boss = [Permissions.ProcurementRequest, Permissions.ProcurementRead, Permissions.ProcurementSettings];
    private static readonly string[] Keeper = [Permissions.ProcurementRequest, Permissions.ProcurementWarehouse];
    private static readonly string[] Lab = [Permissions.ProcurementRequest, Permissions.QualityCheck];
    private static readonly string[] Admin = [.. Permissions.All];

    private sealed record Org(string S, Department Dept, User Employee, User Financier, User Director, User SupplyHead, User Buyer, User Keeper, User Lab, User Admin,
        Guid Raw, Guid Stationery, Guid Central);

    private async Task<Org> SeedAsync()
    {
        var s = Guid.NewGuid().ToString("N")[..6];
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await DbSeeder.SeedProcurementAsync(db, CancellationToken.None);
        var dept = new Department { Code = $"rp-{s}", Name = $"Цех {s}" };
        Role RoleWith(string name, params string[] codes)
        {
            var role = new Role { Name = name };
            foreach (var code in codes)
            {
                role.Permissions.Add(new RolePermission { Code = code });
            }

            return role;
        }

        User Person(string login, string name, Role? role = null)
        {
            var user = new User { Login = $"{login}-{s}", FullName = $"{name} {s}", Department = dept };
            if (role is not null)
            {
                user.Roles.Add(new UserRole { Role = role });
            }

            return user;
        }

        var org = new Org(s, dept, Person("rp-emp", "Технолог"), Person("rp-fin", "Финансист"), Person("rp-dir", "Директор"), Person("rp-sh", "Руководитель снабжения"),
            Person("rp-buy", "Снабженец", RoleWith($"Снабжение {s}", Permissions.ProcurementManage)), Person("rp-kpr", "Кладовщик", RoleWith($"Кладовщик {s}", Permissions.ProcurementWarehouse)),
            Person("rp-lab", "Завлаб", RoleWith($"Лаборатория {s}", Permissions.QualityCheck)), Person("rp-adm", "Админ"), Guid.Empty, Guid.Empty, Guid.Empty);
        db.Users.AddRange(org.Employee, org.Financier, org.Director, org.SupplyHead, org.Buyer, org.Keeper, org.Lab, org.Admin);
        var settings = await db.ProcurementSettings.FirstOrDefaultAsync() ?? db.ProcurementSettings.Add(new ProcurementSettings()).Entity;
        (settings.FinanceApproverId, settings.DirectorApproverId, settings.DirectorLimit, settings.RawRouteEnabled, settings.SupplyApproverId) =
            (org.Financier.Id, org.Director.Id, ProcurementSettings.DefaultDirectorLimit, false, null);
        await db.SaveChangesAsync();
        db.ProcurementQualityApprovers.RemoveRange(await db.ProcurementQualityApprovers.ToListAsync());
        db.ProcurementQualityApprovers.Add(new ProcurementQualityApprover { UserId = org.Lab.Id });
        await db.SaveChangesAsync();
        var raw = await db.ProcurementCategories.Where(c => c.Code == "raw_materials").Select(c => c.Id).SingleAsync();
        var stationery = await db.ProcurementCategories.Where(c => c.Code == "stationery").Select(c => c.Id).SingleAsync();
        var central = await db.ProcurementWarehouses.Where(w => w.IsDefault).Select(w => w.Id).SingleAsync();
        return org with { Raw = raw, Stationery = stationery, Central = central };
    }

    private async Task<T> As<T>(User user, string[] permissions, Func<IServiceProvider, ProcurementContext, Task<T>> act)
    {
        using var scope = fixture.Services.CreateScope();
        var ctx = await scope.ServiceProvider.GetRequiredService<ProcurementRequestService>().ContextAsync(user.Id, permissions.ToHashSet(), CancellationToken.None);
        return await act(scope.ServiceProvider, ctx);
    }

    private Task Do(User user, string[] permissions, Func<IServiceProvider, ProcurementContext, Task> act) => As(user, permissions, async (sp, c) =>
    {
        await act(sp, c);
        return true;
    });

    private static ProcurementRequestService R(IServiceProvider sp) => sp.GetRequiredService<ProcurementRequestService>();

    private Task RouteAsync(Org org, bool raw, Guid? supplyApprover) => Do(org.Director, Boss, async (sp, c) =>
    {
        var now = await R(sp).SettingsAsync(c, default);
        await R(sp).UpdateSettingsAsync(c, now with { RawRouteEnabled = raw, SupplyApproverId = supplyApprover }, default);
    });

    private Task<Guid> RequestAsync(Org org, User initiator, Guid category, decimal qty, decimal price, Guid? item = null, string name = "Мука") =>
        As(initiator, Employee, (sp, c) => R(sp).CreateAsync(c, new ProcurementRequestInput($"{name} для цеха {org.S}", null, ProcurementPriority.Normal, null, "Выпуск", null,
            [new(null, $"{name} {org.S}", category, "кг", qty, price, ItemId: item)], true), default));

    private Task<ProcurementRequestDetail> DetailAsync(Org org, Guid id) => As(org.Buyer, Supply, (sp, c) => R(sp).GetAsync(c, id, default));

    private async Task<bool> NotifiedAsync(User user, string title)
    {
        using var scope = fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OneBaseDbContext>().UserNotifications.AnyAsync(n => n.RecipientId == user.Id && n.Title.StartsWith(title));
    }

    private Task<Guid> ItemAsync(Org org, string name, bool batches, ProcurementQualityControl control = ProcurementQualityControl.None,
        IReadOnlyList<ProcurementItemUnitInput>? units = null) =>
        As(org.Keeper, Keeper, (sp, c) => sp.GetRequiredService<ProcurementStockService>().CreateItemAsync(c, new ProcurementCatalogInput($"RP-{Guid.NewGuid():N}"[..14],
            $"{name} {org.S}", org.Raw, "кг", null, null, 0, TracksBatches: batches, ShelfLifeDays: 180, QualityControl: control, Units: units), default));

    /// <summary>Утверждённая обычным маршрутом заявка (финансист, сумма в лимите).</summary>
    private async Task<Guid> ApprovedAsync(Org org, Guid category, decimal qty, decimal price, Guid? item = null, string name = "Мука")
    {
        var id = await RequestAsync(org, org.Employee, category, qty, price, item, name);
        await Do(org.Financier, Employee, (sp, c) => R(sp).ApproveAsync(c, id, null, default));
        Assert.Equal(ProcurementRequestStatus.Approved, (await DetailAsync(org, id)).Status);
        return id;
    }

    [PostgresFact]
    public async Task Raw_route_goes_supply_head_then_director_whatever_the_amount_and_other_requests_keep_their_route()
    {
        var org = await SeedAsync();

        // Маршрут сырья выключен — заявка на сырьё идёт прежним маршрутом (финансист).
        await RouteAsync(org, false, null);
        var before = await RequestAsync(org, org.Employee, org.Raw, 10, 1_000);
        Assert.Equal(ProcurementRequestStatus.FinanceApproval, (await DetailAsync(org, before)).Status);
        await Do(org.Employee, Employee, (sp, c) => R(sp).CancelAsync(c, before, null, default));

        // Включён: руководитель снабжения → директор, хотя сумма меньше лимита; директор, финансист и администратор шаг снабжения не решают.
        await RouteAsync(org, true, org.SupplyHead.Id);
        var id = await RequestAsync(org, org.Employee, org.Raw, 10, 1_000);
        var detail = await DetailAsync(org, id);
        Assert.Equal((ProcurementRequestStatus.SupplyApproval, true), (detail.Status, detail.RawRoute));
        Assert.Equal([ProcurementApprovalStages.SupplyHead, ProcurementApprovalStages.Director, "supply"], detail.Route!.Select(s => s.Key));
        Assert.Equal("current", detail.Route![0].State);
        Assert.True(await NotifiedAsync(org.SupplyHead, "Заявка ждёт вашего согласования"));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => Do(org.Director, Boss, (sp, c) => R(sp).ApproveAsync(c, id, null, default)));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => Do(org.Financier, Employee, (sp, c) => R(sp).ApproveAsync(c, id, null, default)));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => Do(org.Admin, Admin, (sp, c) => R(sp).ApproveAsync(c, id, null, default)));
        await Do(org.SupplyHead, Employee, (sp, c) => R(sp).ApproveAsync(c, id, "Нужно для выпуска", default));
        Assert.Equal(ProcurementRequestStatus.DirectorApproval, (await DetailAsync(org, id)).Status);
        await Do(org.Director, Boss, (sp, c) => R(sp).ApproveAsync(c, id, null, default));
        detail = await DetailAsync(org, id);
        Assert.Equal(ProcurementRequestStatus.Approved, detail.Status);
        Assert.Equal(["done", "done", "current"], detail.Route!.Select(s => s.State));
        Assert.True(await NotifiedAsync(org.Financier, "Утверждена закупка сырья")); // финансист — уведомление без шага (D4)

        // Заявка не на сырьё — прежним маршрутом; заявка самого руководителя снабжения — сразу директору.
        Assert.Equal(ProcurementRequestStatus.FinanceApproval, (await DetailAsync(org, await RequestAsync(org, org.Employee, org.Stationery, 5, 2_000, name: "Бумага"))).Status);
        Assert.Equal(ProcurementRequestStatus.DirectorApproval, (await DetailAsync(org, await RequestAsync(org, org.SupplyHead, org.Raw, 3, 1_000))).Status);

        // Руководитель снабжения не выбран — решают руководители отдела «Снабжение».
        await RouteAsync(org, true, null);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var supply = await db.Departments.FirstOrDefaultAsync(d => d.Code == "supply") ?? db.Departments.Add(new Department { Code = "supply", Name = "Снабжение" }).Entity;
            await db.SaveChangesAsync();
            db.WorkDepartmentHeads.Add(new WorkDepartmentHead { DepartmentId = supply.Id, UserId = org.Buyer.Id });
            await db.SaveChangesAsync();
        }

        var next = await RequestAsync(org, org.Employee, org.Raw, 4, 1_000);
        Assert.Equal(ProcurementRequestStatus.SupplyApproval, (await DetailAsync(org, next)).Status);
        await Assert.ThrowsAsync<FieldForbiddenException>(() => Do(org.SupplyHead, Employee, (sp, c) => R(sp).ApproveAsync(c, next, null, default)));
        await Do(org.Buyer, Supply, (sp, c) => R(sp).ReturnAsync(c, next, "Уточните объём", default));
        Assert.Equal(ProcurementRequestStatus.RevisionRequired, (await DetailAsync(org, next)).Status);
        await RouteAsync(org, false, null);
    }

    [PostgresFact]
    public async Task Tender_delivery_lands_in_batches_on_control_in_the_purchase_unit()
    {
        var org = await SeedAsync();
        await RouteAsync(org, false, null);
        var flour = await ItemAsync(org, "Мука", batches: true, ProcurementQualityControl.Documents, [new("упак", 25)]);
        var request = await ApprovedAsync(org, org.Raw, 100, 9_000, flour);
        var tenders = (Func<IServiceProvider, ProcurementTenderService>)(sp => sp.GetRequiredService<ProcurementTenderService>());
        ProcurementTenderInput Input(IReadOnlyList<ProcurementTenderLineInput> lines) => new($"Мука на квартал {org.S}", "T-1", FieldClock.Today.AddDays(-3), null,
            $"ООО Зерно {org.S}", org.Raw, "Д-1", 3, FieldClock.Today.AddDays(10), null, lines, RequestId: request, WarehouseId: org.Central);

        // Позиция с карточкой — в её единице закупки (упак = 25 кг); единица, которой нет у карточки, — ошибка.
        await Assert.ThrowsAsync<FieldValidationException>(() => As(org.Buyer, Supply, (sp, c) => tenders(sp).CreateAsync(c,
            Input([new($"Мука {org.S}", "мешок", 4, 250_000, flour)]), default)));
        var id = await As(org.Buyer, Supply, (sp, c) => tenders(sp).CreateAsync(c,
            Input([new($"Мука {org.S}", "упак", 4, 250_000, flour), new("Доставка", "шт", 1, 100_000)]), default));
        var detail = await As(org.Keeper, Keeper, (sp, c) => tenders(sp).GetAsync(c, id, default));
        Assert.True(detail.Can.Receive);
        Assert.Equal((request, 25m), (detail.RequestId!.Value, detail.Items[0].Factor!.Value));
        Assert.Contains((await DetailAsync(org, request)).Tenders!, t => t.Id == id);

        // Поставка: 2 упак одной партией — 50 кг на склад, партия на контроле, цена — 10 000 за кг.
        var line = detail.Items[0].Id;
        ProcurementTenderDeliveryInput Delivery(decimal qty, params ProcurementBatchInput[] batches) => new([new(line, qty, batches)], Reference: "Накладная 7");
        ProcurementBatchInput Batch(string no, decimal qty) => new(no, FieldClock.Today.AddDays(-5), null, qty);
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Buyer, Supply, (sp, c) => tenders(sp).DeliverAsync(c, id, Delivery(2, Batch("Z-1", 2)), default)));
        await Assert.ThrowsAsync<FieldValidationException>(() => As(org.Keeper, Keeper, (sp, c) => tenders(sp).DeliverAsync(c, id, Delivery(2, Batch("Z-1", 1)), default)));
        var receipt = await As(org.Keeper, Keeper, (sp, c) => tenders(sp).DeliverAsync(c, id, Delivery(2, Batch("Z-1", 2)), default));
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var batch = await db.ProcurementBatches.AsNoTracking().SingleAsync(b => b.ItemId == flour);
            Assert.Equal((ProcurementBatchQuality.Pending, 10_000m), (batch.QualityStatus, batch.UnitCost!.Value));
            Assert.Equal(50m, await db.ProcurementStockBalances.Where(b => b.ItemId == flour && b.WarehouseId == org.Central).Select(b => b.OnHand).SingleAsync());
            Assert.True(await db.ProcurementQualityChecks.AnyAsync(c => c.BatchId == batch.Id));
            Assert.Equal(id, (await db.ProcurementStockDocuments.AsNoTracking().SingleAsync(d => d.Id == receipt)).TenderId);
        }

        // Больше оставшегося — нельзя; поставка по тендеру не сторнируется; позиции после поставки не меняются.
        await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Keeper, Keeper, (sp, c) => tenders(sp).DeliverAsync(c, id, Delivery(3, Batch("Z-2", 3)), default)));
        await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Keeper, Keeper, (sp, c) =>
            sp.GetRequiredService<ProcurementStockService>().ReverseAsync(c, receipt, new ProcurementReversalInput("Ошибка"), default)));
        detail = await As(org.Buyer, Supply, (sp, c) => tenders(sp).GetAsync(c, id, default));
        await Assert.ThrowsAsync<FieldConflictException>(() => Do(org.Buyer, Supply, (sp, c) => tenders(sp).UpdateAsync(c, id,
            Input([new($"Мука {org.S}", "упак", 5, 250_000, flour), new("Доставка", "шт", 1, 100_000)]) with { Version = detail.Version }, default)));

        // Остаток поставлен — позиция «Доставка» без карточки на склад не идёт: тендер закрывает снабжение.
        await As(org.Keeper, Keeper, (sp, c) => tenders(sp).DeliverAsync(c, id, Delivery(2, Batch("Z-2", 2)), default));
        detail = await As(org.Buyer, Supply, (sp, c) => tenders(sp).GetAsync(c, id, default));
        Assert.Equal((4m, 0m, ProcurementTenderStatus.Contracted, 2), (detail.Items[0].ReceivedQty, detail.Items[0].Left, detail.Status, detail.Deliveries!.Count));
        await Do(org.Buyer, Supply, (sp, c) => tenders(sp).DeliveredAsync(c, id, null, default));

        // История цен позиции: тендер (в кг) и приход без заказа — поставка по тендеру отдельно не считается.
        await As(org.Keeper, Keeper, (sp, c) => sp.GetRequiredService<ProcurementStockService>().ReceiptAsync(c,
            new ProcurementStockOperationInput([new(flour, 10, 9_500, Batches: [new("R-1", FieldClock.Today.AddDays(-1), null, 10)])], "Докупили"), default));
        var prices = await As(org.Buyer, Supply, (sp, c) => sp.GetRequiredService<ProcurementInsightService>().ItemPricesAsync(c, flour, default));
        Assert.Equal([("tender", 100m, 10_000m), ("receipt", 10m, 9_500m)], prices.Points.Select(p => (p.Source, p.Quantity, p.Price)));
        Assert.Equal((9_500m, 10_000m), (prices.Min!.Value, prices.Max!.Value));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Keeper, Keeper, (sp, c) => sp.GetRequiredService<ProcurementInsightService>().ItemPricesAsync(c, flour, default)));
    }

    [PostgresFact]
    public async Task Bought_raw_material_waits_for_quality_then_is_reserved_or_goes_back_to_purchase()
    {
        var org = await SeedAsync();
        await RouteAsync(org, false, null);
        var cocoa = await ItemAsync(org, "Какао", batches: true, ProcurementQualityControl.Documents);
        var request = await ApprovedAsync(org, org.Raw, 20, 60_000, cocoa, "Какао");
        var line = (await DetailAsync(org, request)).Items[0].Id;
        var purchases = (Func<IServiceProvider, ProcurementPurchaseService>)(sp => sp.GetRequiredService<ProcurementPurchaseService>());
        ProcurementPurchaseInput Bought(params ProcurementBatchInput[] batches) => new([new(line, 20, 61_000, batches)], null, $"ООО Какао {org.S}", "Чек 5", null);

        // Позиция с партиями — «Куплено» только с партиями и сроком.
        await Assert.ThrowsAsync<FieldValidationException>(() => As(org.Buyer, Supply, (sp, c) => purchases(sp).PurchaseAsync(c, request, Bought(), default)));
        await As(org.Buyer, Supply, (sp, c) => purchases(sp).PurchaseAsync(c, request,
            Bought(new("K-1", null, FieldClock.Today.AddDays(200), 12), new("K-2", null, FieldClock.Today.AddDays(210), 8)), default));

        // Обе партии на контроле: получено 20, но под заявку ещё ничего не зарезервировано — выдать нечего.
        var item = (await DetailAsync(org, request)).Items[0];
        Assert.Equal((20m, 0m), (item.ReceivedQty, item.ReservedLeft));
        Guid BatchOf(OneBaseDbContext db, string no) => db.ProcurementBatches.AsNoTracking().Single(b => b.ItemId == cocoa && b.SupplierBatchNo == no).Id;
        Guid k1, k2, c1, c2;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            (k1, k2) = (BatchOf(db, "K-1"), BatchOf(db, "K-2"));
            c1 = await db.ProcurementQualityChecks.Where(c => c.BatchId == k1).Select(c => c.Id).SingleAsync();
            c2 = await db.ProcurementQualityChecks.Where(c => c.BatchId == k2).Select(c => c.Id).SingleAsync();
        }

        // Резерва нет — заявка кладовщику не видна: выдавать по ней нечего.
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Keeper, Keeper, (sp, c) => R(sp).IssueAsync(c, request, new([new(line, 1)], "Мастер", null), default)));

        // K-1 допущена — 12 под заявку, выдаётся; K-2 забракована — 8 снова к закупке.
        var quality = (Func<IServiceProvider, ProcurementQualityService>)(sp => sp.GetRequiredService<ProcurementQualityService>());
        foreach (var check in new[] { c1, c2 })
        {
            await Do(org.Lab, Lab, (sp, c) => quality(sp).DocumentsAsync(c, check, new([], true), default));
        }

        await Do(org.Lab, Lab, (sp, c) => quality(sp).DecideAsync(c, c1, new("approve", null), default));
        await Do(org.Lab, Lab, (sp, c) => quality(sp).DecideAsync(c, c2, new("reject", "Влажность выше нормы"), default));
        item = (await DetailAsync(org, request)).Items[0];
        Assert.Equal((12m, 12m, 12m), (item.ReceivedQty, item.OrderedQty, item.ReservedLeft));
        await As(org.Keeper, Keeper, (sp, c) => R(sp).IssueAsync(c, request, new([new(line, 12)], "Мастер", null), default));
        var toBuy = await As(org.Buyer, Supply, (sp, c) => purchases(sp).ToBuyAsync(c, default));
        var again = toBuy.Requests.Single(r => r.RequestId == request).Lines.Single();
        Assert.Equal((8m, true), (again.Left, again.TracksBatches));
        Assert.True(await NotifiedAsync(org.Buyer, "Снова к закупке"));
        var history = (await DetailAsync(org, request)).History;
        Assert.Contains(history, h => h.Action == "quality_approved");
        Assert.Contains(history, h => h.Action == "quality_rejected");
    }

    [PostgresFact]
    public async Task Stock_receipt_takes_a_purchase_unit_and_units_are_validated()
    {
        var org = await SeedAsync();
        var stock = (Func<IServiceProvider, ProcurementStockService>)(sp => sp.GetRequiredService<ProcurementStockService>());
        await Assert.ThrowsAsync<FieldValidationException>(() => ItemAsync(org, "Сахар", false, units: [new("упак", 0)]));
        await Assert.ThrowsAsync<FieldValidationException>(() => ItemAsync(org, "Сахар", false, units: [new("кг", 2)]));
        await Assert.ThrowsAsync<FieldValidationException>(() => ItemAsync(org, "Сахар", false, units: [new("упак", 25), new("упак", 50)]));
        var sugar = await ItemAsync(org, "Сахар", false, units: [new("упак", 25), new("коробка", 0.333m)]);

        // 3 упак по 250 000 — 75 кг по 10 000; 0,5 коробки — 0,1665 кг (больше трёх знаков) — ошибка; выдача — только в единице карточки.
        await As(org.Keeper, Keeper, (sp, c) => stock(sp).ReceiptAsync(c, new ProcurementStockOperationInput([new(sugar, 3, 250_000, UnitCode: "упак")], "Приход"), default));
        await Assert.ThrowsAsync<FieldValidationException>(() => As(org.Keeper, Keeper, (sp, c) => stock(sp).ReceiptAsync(c,
            new ProcurementStockOperationInput([new(sugar, 0.5m, UnitCode: "коробка")], "Приход"), default)));
        await Assert.ThrowsAsync<FieldValidationException>(() => As(org.Keeper, Keeper, (sp, c) => stock(sp).ReceiptAsync(c,
            new ProcurementStockOperationInput([new(sugar, 1, UnitCode: "л")], "Приход"), default)));
        await Assert.ThrowsAsync<FieldValidationException>(() => As(org.Keeper, Keeper, (sp, c) => stock(sp).IssueAsync(c,
            new ProcurementStockOperationInput([new(sugar, 1, UnitCode: "упак")], null, DepartmentId: org.Dept.Id), default)));
        var card = await As(org.Keeper, Keeper, (sp, c) => stock(sp).ItemAsync(c, sugar, default));
        Assert.Equal(75m, card.Item.OnHand);
        Assert.Equal((75m, 10_000m), (card.Movements.Single().Quantity, card.Movements.Single().UnitCost!.Value));
        Assert.Equal([("коробка", 0.333m), ("упак", 25m)], card.Units!.Select(u => (u.UnitCode, u.Factor)));
        var option = (await As(org.Keeper, Keeper, (sp, c) => stock(sp).CatalogSearchAsync(c, $"Сахар {org.S}", default))).Single();
        Assert.Equal(2, option.Units!.Count);
    }
}
