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
/// Склад и согласование директором на настоящем PostgreSQL (ONEBASE_TEST_POSTGRES, иначе пропускается): проверка склада при отправке
/// (100 запрошено, 40 доступно — 40 в резерв, 60 к закупке), резерв без двойного и отрицательного остатка при одновременных заявках,
/// утверждение только ролью «Директор» (администратор со всеми правами и снабжение — 403), отказ и возврат с комментарием снимают
/// резерв, заявка только со склада утверждается сама, выдача из резерва, повторное согласование после существенной правки, приход,
/// списание, инвентаризация, выдача отделу, ключ повтора документа.
/// </summary>
public class ProcurementStockDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly string[] Employee = [Permissions.ProcurementRequest];
    private static readonly string[] Supply = [Permissions.ProcurementRequest, Permissions.ProcurementRead, Permissions.ProcurementManage];
    private static readonly string[] Director = [Permissions.ProcurementRequest, Permissions.ProcurementRead, Permissions.ProcurementSettings];
    private static readonly string[] Keeper = [Permissions.ProcurementRequest, Permissions.ProcurementWarehouse];
    private static readonly string[] AllRights = [.. Permissions.All];

    private sealed record Org(Department Dept, User Employee, User Supply, User Director, User Keeper, User Admin, Guid Category);

    private async Task<Org> SeedAsync()
    {
        var s = Guid.NewGuid().ToString("N")[..6];
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await DbSeeder.SeedProcurementAsync(db, CancellationToken.None);
        var dept = new Department { Code = $"st-{s}", Name = $"Производство {s}" };
        var directorRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == SystemRoles.Director) ?? new Role { Name = SystemRoles.Director };
        // Права ролей в базе — по ним уведомления находят снабжение и кладовщика; администратор (все права, не «Директор») их не получает.
        Role WithRights(string name, params string[] codes)
        {
            var role = new Role { Name = name };
            foreach (var code in codes)
            {
                role.Permissions.Add(new RolePermission { Code = code });
            }

            return role;
        }

        var adminRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == SystemRoles.Admin)
                        ?? WithRights(SystemRoles.Admin, Permissions.ProcurementManage, Permissions.ProcurementWarehouse);
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

        var org = new Org(dept, Person("st-emp", "Сотрудник Производства"), Person("st-sup", "Снабженец", supplyRole), Person("st-dir", "Директор", directorRole),
            Person("st-kpr", "Кладовщик", keeperRole), Person("st-adm", "Администратор", adminRole), Guid.Empty);
        db.Users.AddRange(org.Employee, org.Supply, org.Director, org.Keeper, org.Admin);
        await db.SaveChangesAsync();
        var category = await db.ProcurementCategories.Where(c => c.Code == "production_materials").Select(c => c.Id).SingleAsync();
        return org with { Category = category };
    }

    private Task<T> As<T>(User user, string[] permissions, Func<ProcurementRequestService, ProcurementStockService, ProcurementContext, Task<T>> act) =>
        Run(user, permissions, act);

    private Task As(User user, string[] permissions, Func<ProcurementRequestService, ProcurementStockService, ProcurementContext, Task> act) =>
        Run(user, permissions, async (r, s, c) =>
        {
            await act(r, s, c);
            return true;
        });

    private async Task<T> Run<T>(User user, string[] permissions, Func<ProcurementRequestService, ProcurementStockService, ProcurementContext, Task<T>> act)
    {
        using var scope = fixture.Services.CreateScope();
        var requests = scope.ServiceProvider.GetRequiredService<ProcurementRequestService>();
        var stock = scope.ServiceProvider.GetRequiredService<ProcurementStockService>();
        var ctx = await requests.ContextAsync(user.Id, permissions.ToHashSet(), CancellationToken.None);
        return await act(requests, stock, ctx);
    }

    /// <summary>Позиция каталога с приходом qty на центральный склад (кладовщик).</summary>
    private async Task<Guid> ItemAsync(Org org, string name, decimal qty, decimal minQty = 0)
    {
        var sku = $"SKU-{Guid.NewGuid():N}"[..14];
        var id = await As(org.Keeper, Keeper, (_, s, c) => s.CreateItemAsync(c, new ProcurementCatalogInput(sku, name, org.Category, "кг", null, null, minQty), default));
        if (qty > 0)
        {
            await As(org.Keeper, Keeper, (_, s, c) => s.ReceiptAsync(c, new ProcurementStockOperationInput([new(id, qty, 12_000)], "Начальный остаток", "НАКЛ-1"), default));
        }

        return id;
    }

    private async Task<ProcurementStockBalance> BalanceAsync(Guid itemId)
    {
        using var scope = fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OneBaseDbContext>().ProcurementStockBalances.AsNoTracking().SingleAsync(b => b.ItemId == itemId);
    }

    private static ProcurementRequestInput Request(Guid itemId, decimal qty, decimal fromStock, bool submit = true, string? purchaseReason = null) => new(
        "Материал для цеха", null, ProcurementPriority.High, null, "План производства на ноябрь", null,
        [new(null, null, null, null, qty, 15_000, null, itemId, fromStock, purchaseReason)], submit);

    [PostgresFact]
    public async Task Stock_is_checked_reserved_and_only_the_director_approves_the_purchase()
    {
        var org = await SeedAsync();
        var item = await ItemAsync(org, "Гранулы ПЭТ", 40);

        // На складе есть, а просят купить всё — нужно обоснование.
        await Assert.ThrowsAsync<FieldValidationException>(() => As(org.Employee, Employee, (r, _, c) => r.CreateAsync(c, Request(item, 100, 0), default)));

        // Сценарий ТЗ: запрошено 100, доступно 40 → 40 в резерв, 60 к закупке.
        var id = await As(org.Employee, Employee, (r, _, c) => r.CreateAsync(c, Request(item, 100, 40), default));
        var balance = await BalanceAsync(item);
        Assert.Equal((40m, 40m), (balance.OnHand, balance.Reserved));
        var submitted = await As(org.Supply, Supply, (r, _, c) => r.GetAsync(c, id, default));
        Assert.Equal(ProcurementRequestStatus.Submitted, submitted.Status);
        var line = Assert.Single(submitted.Items);
        Assert.Equal((40m, 60m, 40m, ProcurementItemStatus.ToPurchase), (line.FromStock, line.ToPurchase, line.ReservedLeft, line.Status));
        Assert.Equal(60 * 15_000m, submitted.PurchaseTotal);

        // Остаток уже в резерве — вторая заявка его не получит.
        await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Employee, Employee, (r, _, c) => r.CreateAsync(c, Request(item, 10, 10), default)));

        await As(org.Supply, Supply, (r, _, c) => r.ForwardAsync(c, id, default));
        // Утверждает только директор: не снабжение, не инициатор и не администратор со всеми правами (прямые вызовы API).
        foreach (var (user, perms) in new[] { (org.Supply, Supply), (org.Employee, Employee), (org.Admin, AllRights) })
        {
            await Assert.ThrowsAsync<FieldForbiddenException>(() => As(user, perms, (r, _, c) => r.ApproveAsync(c, id, null, default)));
        }

        await As(org.Director, Director, (r, _, c) => r.ApproveAsync(c, id, "Согласовано", default));
        await As(org.Director, Director, (r, _, c) => r.ApproveAsync(c, id, null, default)); // повторное нажатие — без второго решения

        // Кладовщик выдаёт 40 из резерва: остаток 0, резерв 0, заявка частично выполнена (60 ждут закупки).
        await Assert.ThrowsAsync<FieldForbiddenException>(() =>
            As(org.Supply, Supply, (r, _, c) => r.IssueAsync(c, id, new([new(line.Id, 40)], "Бригадир", null), default)));
        await Assert.ThrowsAsync<FieldConflictException>(() =>
            As(org.Keeper, Keeper, (r, _, c) => r.IssueAsync(c, id, new([new(line.Id, 41)], "Бригадир", null), default)));
        var key = Guid.NewGuid();
        var doc1 = await As(org.Keeper, Keeper, (r, _, c) => r.IssueAsync(c, id, new([new(line.Id, 40)], "Бригадир цеха", null, key), default));
        var doc2 = await As(org.Keeper, Keeper, (r, _, c) => r.IssueAsync(c, id, new([new(line.Id, 40)], "Бригадир цеха", null, key), default));
        Assert.Equal(doc1, doc2);

        balance = await BalanceAsync(item);
        Assert.Equal((0m, 0m), (balance.OnHand, balance.Reserved));
        var detail = await As(org.Director, Director, (r, _, c) => r.GetAsync(c, id, default));
        Assert.Equal(ProcurementRequestStatus.PartiallyFulfilled, detail.Status);
        Assert.Equal((40m, ProcurementItemStatus.PartiallyIssued), (detail.Items[0].IssuedQty, detail.Items[0].Status));
        var approval = Assert.Single(detail.Approvals!);
        Assert.Equal((1, ProcurementApprovalDecision.Approved, "Согласовано", "Директор"), (approval.Round, approval.Decision, approval.Comment, approval.DeciderName));
        Assert.Equal(["created", "submitted", "forwarded", "approved", "issued"], detail.History.Select(h => h.Action));

        var movements = await As(org.Keeper, Keeper, (_, s, c) => s.MovementsAsync(c, new ProcurementMovementSearch(item), default));
        Assert.Equal([-40m, 40m], movements.Items.Select(m => m.Quantity));
        Assert.Equal((40m, 0m), (movements.Items[0].BalanceBefore, movements.Items[0].BalanceAfter));
        Assert.Equal(detail.Number, movements.Items[0].RequestNumber);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        Assert.True(await db.UserNotifications.AnyAsync(n => n.RecipientId == org.Director.Id && n.Link == $"/requests/{id}" && n.Title.Contains("согласования")));
        Assert.True(await db.UserNotifications.AnyAsync(n => n.RecipientId == org.Keeper.Id && n.Title.StartsWith("К выдаче со склада")));
        Assert.False(await db.UserNotifications.AnyAsync(n => n.RecipientId == org.Admin.Id && n.Link == $"/requests/{id}")); // технический администратор
    }

    [PostgresFact]
    public async Task Stock_only_request_is_approved_without_the_director_and_fulfilled_by_issue()
    {
        var org = await SeedAsync();
        var item = await ItemAsync(org, "Перчатки рабочие", 50);
        var id = await As(org.Employee, Employee, (r, _, c) => r.CreateAsync(c, Request(item, 20, 20), default));
        var detail = await As(org.Keeper, Keeper, (r, _, c) => r.GetAsync(c, id, default));
        Assert.Equal(ProcurementRequestStatus.Approved, detail.Status);
        Assert.Equal(0m, detail.PurchaseTotal);
        Assert.Contains(detail.History, h => h.Action == "auto_approved");
        Assert.True(detail.Can.Issue);
        await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Supply, Supply, (r, _, c) => r.ForwardAsync(c, id, default))); // нечего согласовывать

        await As(org.Keeper, Keeper, (r, _, c) => r.IssueAsync(c, id, new([new(detail.Items[0].Id, 20)], "Мастер смены", null), default));
        detail = await As(org.Employee, Employee, (r, _, c) => r.GetAsync(c, id, default));
        Assert.Equal(ProcurementRequestStatus.Fulfilled, detail.Status);
        Assert.Equal(ProcurementItemStatus.Issued, detail.Items[0].Status);
        Assert.Equal((30m, 0m), ((await BalanceAsync(item)).OnHand, (await BalanceAsync(item)).Reserved));
    }

    [PostgresFact]
    public async Task Concurrent_requests_never_reserve_more_than_is_on_hand()
    {
        var org = await SeedAsync();
        var item = await ItemAsync(org, "Краска эмаль", 40);
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ =>
        {
            try
            {
                await As(org.Employee, Employee, (r, _, c) => r.CreateAsync(c, Request(item, 15, 15), default));
                return true;
            }
            catch (Exception e) when (e is FieldConflictException or DbUpdateConcurrencyException)
            {
                return false;
            }
        }));

        var balance = await BalanceAsync(item);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var reserved = await db.ProcurementStockReservations.Where(x => x.ItemId == item && x.Status == ProcurementReservationStatus.Active).SumAsync(x => x.Quantity);
        Assert.InRange(results.Count(ok => ok), 1, 2); // 15 + 15 ≤ 40, третья не помещается
        Assert.Equal(results.Count(ok => ok) * 15m, reserved);
        Assert.Equal(reserved, balance.Reserved);
        Assert.True(balance.Reserved <= balance.OnHand);
    }

    [PostgresFact]
    public async Task Reject_and_return_need_a_comment_and_release_the_reserve()
    {
        var org = await SeedAsync();
        var item = await ItemAsync(org, "Лента упаковочная", 30);
        var id = await As(org.Employee, Employee, (r, _, c) => r.CreateAsync(c, Request(item, 50, 30), default));
        await As(org.Supply, Supply, (r, _, c) => r.ForwardAsync(c, id, default));
        Assert.Equal(30m, (await BalanceAsync(item)).Reserved);

        await Assert.ThrowsAsync<FieldValidationException>(() => As(org.Director, Director, (r, _, c) => r.ReturnAsync(c, id, " ", default)));
        await As(org.Director, Director, (r, _, c) => r.ReturnAsync(c, id, "Уменьшите объём — склад скоро пополнится", default));
        Assert.Equal(0m, (await BalanceAsync(item)).Reserved);
        var returned = await As(org.Employee, Employee, (r, _, c) => r.GetAsync(c, id, default));
        Assert.Equal(ProcurementRequestStatus.RevisionRequired, returned.Status);
        Assert.Equal("Уменьшите объём — склад скоро пополнится", returned.RevisionComment);
        Assert.Equal(ProcurementApprovalDecision.Revision, Assert.Single(returned.Approvals!).Decision);

        // Исправленная заявка снова резервирует и идёт на второй раунд; отказ — с комментарием, резерв снимается.
        await As(org.Employee, Employee, (r, _, c) => r.SubmitAsync(c, id, default));
        Assert.Equal(30m, (await BalanceAsync(item)).Reserved);
        await As(org.Supply, Supply, (r, _, c) => r.ForwardAsync(c, id, default));
        await Assert.ThrowsAsync<FieldValidationException>(() => As(org.Director, Director, (r, _, c) => r.RejectAsync(c, id, null, default)));
        await As(org.Director, Director, (r, _, c) => r.RejectAsync(c, id, "Нет бюджета в этом месяце", default));
        var rejected = await As(org.Employee, Employee, (r, _, c) => r.GetAsync(c, id, default));
        Assert.Equal(ProcurementRequestStatus.Rejected, rejected.Status);
        Assert.Equal([1, 2], rejected.Approvals!.Select(a => a.Round));
        Assert.All(rejected.Items, i => Assert.Equal(ProcurementItemStatus.Cancelled, i.Status));
        Assert.Equal((30m, 0m), ((await BalanceAsync(item)).OnHand, (await BalanceAsync(item)).Reserved));
    }

    [PostgresFact]
    public async Task Material_change_of_an_approved_request_goes_back_to_the_director()
    {
        var org = await SeedAsync();
        var id = await As(org.Employee, Employee, (r, _, c) => r.CreateAsync(c, new ProcurementRequestInput("Оснастка", null, ProcurementPriority.Normal, null,
            "Износ оснастки", null, [new(null, "Пресс-форма", org.Category, "шт", 2, 1_000_000)], Submit: true), default));
        await As(org.Supply, Supply, (r, _, c) => r.ForwardAsync(c, id, default));
        await As(org.Director, Director, (r, _, c) => r.ApproveAsync(c, id, null, default));
        var approved = await As(org.Supply, Supply, (r, _, c) => r.GetAsync(c, id, default));
        Assert.True(approved.Can.Edit && approved.Can.ApprovedEdit);
        await Assert.ThrowsAsync<FieldConflictException>(() => // инициатор утверждённую не правит
            As(org.Employee, Employee, (r, _, c) => r.UpdateAsync(c, id, Edit(approved, 2, 900_000), default)));

        // Цена ниже — без повторного согласования, только в историю.
        await As(org.Supply, Supply, (r, _, c) => r.UpdateAsync(c, id, Edit(approved, 2, 900_000), default));
        var cheaper = await As(org.Supply, Supply, (r, _, c) => r.GetAsync(c, id, default));
        Assert.Equal(ProcurementRequestStatus.Approved, cheaper.Status);
        Assert.Contains("без повторного согласования", cheaper.History[^1].Text);

        // Цена выше утверждённой (допуск 0 %) — снова к директору, второй раунд.
        await As(org.Supply, Supply, (r, _, c) => r.UpdateAsync(c, id, Edit(cheaper, 2, 1_100_000), default));
        var again = await As(org.Director, Director, (r, _, c) => r.GetAsync(c, id, default));
        Assert.Equal((ProcurementRequestStatus.DirectorApproval, 2), (again.Status, again.ApprovalRound));
        Assert.Contains("повторное согласование", again.History[^1].Text);
        Assert.True(again.Can.Approve);

        // Допуск 15 %: рост цены на 10 % после второго утверждения — без повторного согласования.
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Supply, Supply, (r, _, c) => r.UpdateSettingsAsync(c, new(15), default)));
        await As(org.Director, Director, (r, _, c) => r.UpdateSettingsAsync(c, new(15), default));
        await As(org.Director, Director, (r, _, c) => r.ApproveAsync(c, id, null, default));
        var second = await As(org.Supply, Supply, (r, _, c) => r.GetAsync(c, id, default));
        await As(org.Supply, Supply, (r, _, c) => r.UpdateAsync(c, id, Edit(second, 2, 1_210_000), default));
        Assert.Equal(ProcurementRequestStatus.Approved, (await As(org.Supply, Supply, (r, _, c) => r.GetAsync(c, id, default))).Status);
        await As(org.Director, Director, (r, _, c) => r.UpdateSettingsAsync(c, new(0), default));
    }

    private static ProcurementRequestInput Edit(ProcurementRequestDetail d, decimal qty, decimal price) => new(d.Title, d.DepartmentId, d.Priority, d.DesiredDate,
        d.Reason, d.Comment, [new(d.Items[0].Id, d.Items[0].Name, d.Items[0].CategoryId, d.Items[0].UnitCode, qty, price)], Version: d.Version);

    [PostgresFact]
    public async Task Warehouse_operations_never_go_negative_and_are_journaled()
    {
        var org = await SeedAsync();
        var item = await ItemAsync(org, "Смазка техническая", 10, minQty: 5);

        await Assert.ThrowsAsync<FieldForbiddenException>(() => // приход проводит кладовщик
            As(org.Supply, Supply, (_, s, c) => s.ReceiptAsync(c, new([new(item, 5)], null), default)));
        await Assert.ThrowsAsync<FieldValidationException>(() => // списание — с причиной
            As(org.Keeper, Keeper, (_, s, c) => s.WriteOffAsync(c, new([new(item, 2)], null), default)));
        await Assert.ThrowsAsync<FieldConflictException>(() =>
            As(org.Keeper, Keeper, (_, s, c) => s.WriteOffAsync(c, new([new(item, 11)], "Пролито"), default)));
        await As(org.Keeper, Keeper, (_, s, c) => s.WriteOffAsync(c, new([new(item, 2)], "Пролито при переливе"), default));

        // Резерв 6 по заявке: свободно 2 — выдать отделу без заявки больше нельзя, инвентаризация ниже резерва — нельзя.
        var id = await As(org.Employee, Employee, (r, _, c) => r.CreateAsync(c, Request(item, 6, 6), default));
        await Assert.ThrowsAsync<FieldConflictException>(() =>
            As(org.Keeper, Keeper, (_, s, c) => s.IssueAsync(c, new([new(item, 3)], "На участок", DepartmentId: org.Dept.Id), default)));
        await Assert.ThrowsAsync<FieldValidationException>(() => // выдача отделу — с отделом
            As(org.Keeper, Keeper, (_, s, c) => s.IssueAsync(c, new([new(item, 1)], null), default)));
        await Assert.ThrowsAsync<FieldConflictException>(() =>
            As(org.Keeper, Keeper, (_, s, c) => s.InventoryAsync(c, new([new(item, 5)], "Пересчёт"), default)));

        var key = Guid.NewGuid();
        var inv1 = await As(org.Keeper, Keeper, (_, s, c) => s.InventoryAsync(c, new([new(item, 7)], "Пересчёт", ClientKey: key), default));
        var inv2 = await As(org.Keeper, Keeper, (_, s, c) => s.InventoryAsync(c, new([new(item, 7)], "Пересчёт", ClientKey: key), default));
        Assert.Equal(inv1, inv2);
        await As(org.Keeper, Keeper, (_, s, c) => s.IssueAsync(c, new([new(item, 1)], "На участок", DepartmentId: org.Dept.Id, Receiver: "Мастер"), default));

        var balance = await BalanceAsync(item);
        Assert.Equal((6m, 6m), (balance.OnHand, balance.Reserved));
        var stock = await As(org.Supply, Supply, (_, s, c) => s.StockAsync(c, new ProcurementStockSearch(Filter: "low"), default));
        Assert.Contains(stock.Items, r => r.ItemId == item && r.Low && r.Available == 0); // доступно 0 < минимум 5
        var journal = await As(org.Supply, Supply, (_, s, c) => s.MovementsAsync(c, new ProcurementMovementSearch(item), default));
        Assert.Equal([ProcurementMovementKind.Issue, ProcurementMovementKind.Adjustment, ProcurementMovementKind.WriteOff, ProcurementMovementKind.Receipt],
            journal.Items.Select(m => m.Kind));
        Assert.Equal([-1m, -1m, -2m, 10m], journal.Items.Select(m => m.Quantity)); // 8 → 7 по инвентаризации
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Employee, Employee, (_, s, c) => s.StockAsync(c, new(), default)));

        var options = await As(org.Employee, Employee, (_, s, c) => s.CatalogSearchAsync(c, "Смазка", default));
        Assert.Equal(0m, Assert.Single(options, o => o.Id == item).Available);
        var toIssue = await As(org.Keeper, Keeper, (_, s, c) => s.ToIssueAsync(c, default));
        Assert.Equal(6m, Assert.Single(toIssue, t => t.RequestId == id).Lines.Single().Left);
    }
}
