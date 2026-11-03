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
/// «Куплено» по согласованной заявке на настоящем PostgreSQL (ONEBASE_TEST_POSTGRES, иначе пропускается): заявки к закупке; покупка сразу
/// приходуется на склад по цене покупки и резервируется под заявку (к выдаче), частично и до конца; карточка склада — та же по названию и
/// единице или новая; поставщик — из справочника по названию или новый (без дублей); дороже согласованного — уведомление финансисту; лишнее
/// сверх «к закупке», повтор формы и покупка до согласования не проходят; выдача выполняет заявку; покупка считается в закупках периода.
/// </summary>
public class ProcurementPurchaseDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly string[] Employee = [Permissions.ProcurementRequest];
    private static readonly string[] Supply = [Permissions.ProcurementRequest, Permissions.ProcurementRead, Permissions.ProcurementManage];
    private static readonly string[] Director = [Permissions.ProcurementRequest, Permissions.ProcurementRead, Permissions.ProcurementSettings];
    private static readonly string[] Keeper = [Permissions.ProcurementRequest, Permissions.ProcurementWarehouse];

    private sealed record Org(User Employee, User Supply, User Financier, User Keeper, Guid Category, string Suffix);

    private async Task<Org> SeedAsync()
    {
        var s = Guid.NewGuid().ToString("N")[..6];
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await DbSeeder.SeedProcurementAsync(db, CancellationToken.None);
        var dept = new Department { Code = $"pu-{s}", Name = $"Маркетинг {s}" };
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

        var org = new Org(Person("pu-emp", "Маркетолог"), Person("pu-sup", "Снабженец", supplyRole), Person("pu-fin", "Финансист"), Person("pu-kpr", "Кладовщик", keeperRole),
            Guid.Empty, s);
        db.Users.AddRange(org.Employee, org.Supply, org.Financier, org.Keeper);
        await db.SaveChangesAsync();
        // Маршрут: у отдела нет руководителя — заявка сразу к финансисту; лимит 5 млн, допуск роста цены 0 %.
        var route = await db.ProcurementSettings.FirstOrDefaultAsync() ?? db.ProcurementSettings.Add(new ProcurementSettings()).Entity;
        (route.FinanceApproverId, route.DirectorApproverId, route.DirectorLimit, route.ReapprovalTolerancePercent) =
            (org.Financier.Id, null, ProcurementSettings.DefaultDirectorLimit, 0m);
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
    private static ProcurementPurchaseService P(IServiceProvider sp) => sp.GetRequiredService<ProcurementPurchaseService>();

    /// <summary>Заявка сотрудника, согласованная финансистом (до лимита — сразу в снабжение).</summary>
    private async Task<Guid> ApprovedAsync(Org org, params ProcurementItemInput[] items)
    {
        var request = await As(org.Employee, Employee, (sp, c) => R(sp).CreateAsync(c, new ProcurementRequestInput($"Для выставки {org.Suffix}", null, ProcurementPriority.High,
            FieldClock.Today.AddDays(3), "Стенд на выставке", null, items, Submit: true), default));
        await As(org.Financier, Employee, (sp, c) => R(sp).ApproveAsync(c, request, null, default));
        Assert.Equal(ProcurementRequestStatus.Approved, (await As(org.Supply, Supply, (sp, c) => R(sp).GetAsync(c, request, default))).Status);
        return request;
    }

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
    public async Task Bought_goods_go_to_the_warehouse_for_the_request_and_issuing_fulfills_it()
    {
        var org = await SeedAsync();
        var shop = $"Texnomart {org.Suffix}";
        var request = await ApprovedAsync(org,
            new ProcurementItemInput(null, $"Папка-регистратор {org.Suffix}", org.Category, "шт", 10, 12_000),
            new ProcurementItemInput(null, $"Ручка шариковая {org.Suffix}", org.Category, "шт", 50, 3_000));
        var detail = await As(org.Supply, Supply, (sp, c) => R(sp).GetAsync(c, request, default));
        Assert.True(detail.Can.Purchase);
        Assert.False((await As(org.Employee, Employee, (sp, c) => R(sp).GetAsync(c, request, default))).Can.Purchase);
        var (folder, pen) = (detail.Items[0].Id, detail.Items[1].Id);

        // К закупке: обе позиции целиком, по согласованным ценам; сотруднику список не виден.
        var toBuy = (await As(org.Supply, Supply, (sp, c) => P(sp).ToBuyAsync(c, default))).Requests.Single(x => x.RequestId == request);
        Assert.Equal([10m, 50m], toBuy.Lines.Select(l => l.Left));
        Assert.Equal(270_000m, toBuy.Total);
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Employee, Employee, (sp, c) => P(sp).ToBuyAsync(c, default)));

        // Отмечает покупку снабжение — не кладовщик и не сотрудник.
        var input = new ProcurementPurchaseInput([new(folder, 10, 11_500), new(pen, 20, 3_000)], null, shop, "ЧЕК-1", "Купили в выходные", Guid.NewGuid());
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Keeper, Keeper, (sp, c) => P(sp).PurchaseAsync(c, request, input, default)));
        var first = await As(org.Supply, Supply, (sp, c) => P(sp).PurchaseAsync(c, request, input, default));
        Assert.StartsWith("PO-", first.OrderNumber);
        Assert.StartsWith("RC-", first.ReceiptNumber);
        Assert.Equal(175_000m, first.Total);

        // Повтор той же формы — та же покупка, второй раз не приходуется.
        var again = await As(org.Supply, Supply, (sp, c) => P(sp).PurchaseAsync(c, request, input, default));
        Assert.Equal(first.OrderId, again.OrderId);

        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var order = await db.ProcurementPurchaseOrders.AsNoTracking().Include(o => o.Items).Include(o => o.Supplier).SingleAsync(o => o.Id == first.OrderId);
            Assert.True(order.Direct);
            Assert.Equal(ProcurementOrderStatus.Completed, order.Status);
            Assert.Equal(shop, order.Supplier!.Name);
            Assert.Equal(org.Supply.Id, order.ResponsibleId);
            Assert.All(order.Items, i => Assert.Equal(i.Quantity, i.ReceivedQty));
            var receipt = await db.ProcurementStockDocuments.AsNoTracking().Include(d => d.ReceiptLines).SingleAsync(d => d.Id == first.ReceiptId);
            Assert.Equal((ProcurementStockDocumentStatus.Posted, "ЧЕК-1", request), (receipt.Status, receipt.Reference, receipt.RequestId));
            Assert.Equal([10m, 20m], receipt.ReceiptLines.OrderBy(l => l.LineNo).Select(l => l.Accepted));
            var costs = await db.ProcurementStockMovements.AsNoTracking().Where(m => m.DocumentId == receipt.Id).OrderBy(m => m.Quantity).Select(m => m.UnitCost).ToListAsync();
            Assert.Equal([11_500m, 3_000m], costs);
        }

        // Позиции без карточки — новые карточки склада с артикулом SKU-; товар на складе и в резерве под заявку.
        detail = await As(org.Supply, Supply, (sp, c) => R(sp).GetAsync(c, request, default));
        Assert.All(detail.Items, i => Assert.StartsWith("SKU-", i.Sku));
        Assert.Equal((10m, 10m), await BalanceAsync(detail.Items[0].ItemId!.Value));
        Assert.Equal((20m, 20m), await BalanceAsync(detail.Items[1].ItemId!.Value));
        Assert.Equal([ProcurementItemStatus.Received, ProcurementItemStatus.ToPurchase], detail.Items.Select(i => i.Status));
        Assert.Equal(ProcurementRequestStatus.Approved, detail.Status);
        Assert.Contains(detail.Orders!, o => o.Direct && o.Id == first.OrderId && o.ResponsibleName == "Снабженец");
        Assert.Contains(detail.History, h => h.Action == "purchased" && h.Text.Contains($"Куплено у «{shop}»") && h.Text.Contains("чек ЧЕК-1"));
        Assert.Contains($"Куплено по заявке {detail.Number} — можно получить на складе", await NotificationsAsync(org.Employee));
        Assert.Contains($"К выдаче: куплено по заявке {detail.Number}", await NotificationsAsync(org.Keeper));
        Assert.DoesNotContain(await NotificationsAsync(org.Financier), t => t.StartsWith("Куплено дороже"));

        // Осталось купить 30 ручек; больше не оприходовать.
        toBuy = (await As(org.Supply, Supply, (sp, c) => P(sp).ToBuyAsync(c, default))).Requests.Single(x => x.RequestId == request);
        Assert.Equal(pen, toBuy.Lines.Single().RequestItemId);
        Assert.Equal(30m, toBuy.Lines.Single().Left);
        var tooMuch = await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Supply, Supply, (sp, c) =>
            P(sp).PurchaseAsync(c, request, new ProcurementPurchaseInput([new(pen, 31, 3_000)], null, shop, null, null), default)));
        Assert.Contains("осталось купить 30 шт", tooMuch.Message);

        // Остаток — дороже согласованного, в том же магазине (название другим регистром — тот же поставщик): финансисту уведомление.
        await As(org.Supply, Supply, (sp, c) =>
            P(sp).PurchaseAsync(c, request, new ProcurementPurchaseInput([new(pen, 30, 3_500)], null, shop.ToUpperInvariant(), null, null), default));
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            Assert.Equal(1, await db.ProcurementSuppliers.CountAsync(x => x.Name.ToLower() == shop.ToLower()));
        }

        Assert.Contains($"Куплено дороже согласованного: заявка {detail.Number}", await NotificationsAsync(org.Financier));
        detail = await As(org.Supply, Supply, (sp, c) => R(sp).GetAsync(c, request, default));
        Assert.Contains(detail.History, h => h.Action == "purchased" && h.Text.Contains("Дороже согласованного: Ручка шариковая") && h.Text.Contains($"{ProcurementRules.Money(3_500)} вместо {ProcurementRules.Money(3_000)}"));
        Assert.All(detail.Items, i => Assert.Equal(ProcurementItemStatus.Received, i.Status));
        Assert.False(detail.Can.Purchase);
        Assert.DoesNotContain((await As(org.Supply, Supply, (sp, c) => P(sp).ToBuyAsync(c, default))).Requests, x => x.RequestId == request);
        Assert.Equal((50m, 50m), await BalanceAsync(detail.Items[1].ItemId!.Value));

        // Покупки — в закупках периода, как заказы поставщикам: срок от утверждения до поставки, экономия против согласованной цены
        // (папки 10 × 500 дешевле, ручки 30 × 500 дороже).
        using (var scope = fixture.Services.CreateScope())
        {
            var kpis = await scope.ServiceProvider.GetRequiredService<ProcurementAnalyticsService>()
                .KpisAsync(ProcurementFilter.Of(FieldClock.Today, FieldClock.Today, detail.DepartmentId), default);
            Assert.Equal(2, kpis.PurchasesMeasured);
            Assert.Equal(-10_000m, kpis.Savings);
            Assert.Equal(0, kpis.DeliveriesMeasured);
        }

        // Кладовщик выдаёт всё — заявка выполнена, резерв снят.
        await As(org.Keeper, Keeper, (sp, c) => R(sp).IssueAsync(c, request, new([new(folder, 10), new(pen, 50)], "Маркетолог", null), default));
        detail = await As(org.Supply, Supply, (sp, c) => R(sp).GetAsync(c, request, default));
        Assert.Equal(ProcurementRequestStatus.Fulfilled, detail.Status);
        Assert.Equal((0m, 0m), await BalanceAsync(detail.Items[1].ItemId!.Value));
    }

    [PostgresFact]
    public async Task Purchase_goes_to_the_linked_card_or_to_a_card_with_the_same_name_and_unit()
    {
        var org = await SeedAsync();
        var name = $"Степлер {org.Suffix}";
        var stapler = await As(org.Keeper, Keeper, (sp, c) => sp.GetRequiredService<ProcurementStockService>().CreateItemAsync(c,
            new ProcurementCatalogInput($"PU-{Guid.NewGuid():N}"[..12], name, org.Category, "шт", null, null, 0), default));
        var tape = await As(org.Keeper, Keeper, (sp, c) => sp.GetRequiredService<ProcurementStockService>().CreateItemAsync(c,
            new ProcurementCatalogInput($"PU-{Guid.NewGuid():N}"[..12], $"Скотч {org.Suffix}", org.Category, "шт", null, null, 0), default));
        var request = await ApprovedAsync(org,
            new ProcurementItemInput(null, $"  {name.ToUpperInvariant()} ", org.Category, "шт", 2, 40_000),
            new ProcurementItemInput(null, $"Скотч {org.Suffix}", org.Category, "шт", 5, 8_000, ItemId: tape));
        var items = (await As(org.Supply, Supply, (sp, c) => R(sp).GetAsync(c, request, default))).Items;

        await As(org.Supply, Supply, (sp, c) => P(sp).PurchaseAsync(c, request,
            new ProcurementPurchaseInput([new(items[0].Id, 2, 39_000), new(items[1].Id, 5, 8_000)], null, $"Канцтовары {org.Suffix}", null, null), default));
        items = (await As(org.Supply, Supply, (sp, c) => R(sp).GetAsync(c, request, default))).Items;
        Assert.Equal([stapler, tape], items.Select(i => i.ItemId!.Value));
        Assert.Equal((2m, 2m), await BalanceAsync(stapler));
        Assert.Equal((5m, 5m), await BalanceAsync(tape));
    }

    [PostgresFact]
    public async Task Buying_waits_for_approval_and_needs_a_price_and_where_it_was_bought()
    {
        var org = await SeedAsync();
        var request = await As(org.Employee, Employee, (sp, c) => R(sp).CreateAsync(c, new ProcurementRequestInput($"Флешки {org.Suffix}", null, ProcurementPriority.Normal, null,
            "Для презентаций", null, [new(null, $"Флешка 64 ГБ {org.Suffix}", org.Category, "шт", 3, 90_000)], Submit: true), default));
        var item = (await As(org.Supply, Supply, (sp, c) => R(sp).GetAsync(c, request, default))).Items[0].Id;
        var early = await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Supply, Supply, (sp, c) =>
            P(sp).PurchaseAsync(c, request, new ProcurementPurchaseInput([new(item, 3, 90_000)], null, "Магазин", null, null), default)));
        Assert.Contains("купить можно после согласования", early.Message);

        await As(org.Financier, Employee, (sp, c) => R(sp).ApproveAsync(c, request, null, default));
        await Assert.ThrowsAsync<FieldValidationException>(() => As(org.Supply, Supply, (sp, c) =>
            P(sp).PurchaseAsync(c, request, new ProcurementPurchaseInput([new(item, 3, 0)], null, "Магазин", null, null), default)));
        var nowhere = await Assert.ThrowsAsync<FieldValidationException>(() => As(org.Supply, Supply, (sp, c) =>
            P(sp).PurchaseAsync(c, request, new ProcurementPurchaseInput([new(item, 3, 90_000)], null, "  ", null, null), default)));
        Assert.Contains("где купили", nowhere.Message);
        await Assert.ThrowsAsync<FieldValidationException>(() => As(org.Supply, Supply, (sp, c) =>
            P(sp).PurchaseAsync(c, request, new ProcurementPurchaseInput([], null, "Магазин", null, null), default)));
    }
}
