using ClosedXML.Excel;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Procurement;
using OneBase.Application.Security;
using OneBase.Domain.Identity;
using OneBase.Domain.Procurement;
using OneBase.Domain.Work;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// Поставщики, предложения и заказы на настоящем PostgreSQL (ONEBASE_TEST_POSTGRES, иначе пропускается): заказ — только из утверждённой
/// директором потребности и не больше части «к закупке»; в рамках заявки утверждается сам, дороже — решает директор (не снабжение и не
/// администратор); правка утверждённого (цена, поставщик) — повторное согласование; одна заявка — несколько заказов, заказ — позиции
/// нескольких заявок; отмена освобождает количество и закрывает задачу ответственного; сравнение предложений; поставщики (ИНН, импорт,
/// рейтинг); просрочки и фильтр по ответственному.
/// </summary>
public class ProcurementOrderDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly string[] Employee = [Permissions.ProcurementRequest];
    private static readonly string[] Supply = [Permissions.ProcurementRequest, Permissions.ProcurementRead, Permissions.ProcurementManage];
    private static readonly string[] Director = [Permissions.ProcurementRequest, Permissions.ProcurementRead, Permissions.ProcurementSettings];
    private static readonly string[] AllRights = [.. Permissions.All];

    private sealed record Org(User Employee, User Supply, User Supply2, User Director, User Admin, Guid Category);

    private async Task<Org> SeedAsync()
    {
        var s = Guid.NewGuid().ToString("N")[..6];
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await DbSeeder.SeedProcurementAsync(db, CancellationToken.None);
        if (!await db.Departments.AnyAsync(d => d.Code == "supply"))
        {
            db.Departments.Add(new Department { Code = "supply", Name = "Снабжение" });
        }

        var dept = new Department { Code = $"po-{s}", Name = $"Производство {s}" };
        var directorRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == SystemRoles.Director) ?? new Role { Name = SystemRoles.Director };
        var supplyRole = new Role { Name = $"Снабжение {s}" };
        supplyRole.Permissions.Add(new RolePermission { Code = Permissions.ProcurementManage });
        User Person(string login, string name, Role? role = null)
        {
            var u = new User { Login = $"{login}-{s}", FullName = name, Department = dept };
            if (role is not null)
            {
                u.Roles.Add(new UserRole { Role = role });
            }

            return u;
        }

        var org = new Org(Person("po-emp", "Инициатор"), Person("po-sup", "Снабженец Один", supplyRole), Person("po-sup2", "Снабженец Два", supplyRole),
            Person("po-dir", "Директор", directorRole), Person("po-adm", "Админ Все Права"), Guid.Empty);
        db.Users.AddRange(org.Employee, org.Supply, org.Supply2, org.Director, org.Admin);
        await db.SaveChangesAsync();
        // Маршрут: у отдела заявителя нет руководителя — заявка сразу к финансисту (здесь им назначен директор), директор маршрута — роль, лимит 5 млн.
        var route = await db.ProcurementSettings.FirstOrDefaultAsync() ?? db.ProcurementSettings.Add(new ProcurementSettings()).Entity;
        (route.FinanceApproverId, route.DirectorApproverId, route.DirectorLimit) = (org.Director.Id, null, ProcurementSettings.DefaultDirectorLimit);
        await db.SaveChangesAsync();
        return org with { Category = await db.ProcurementCategories.Where(c => c.Code == "stationery").Select(c => c.Id).SingleAsync() };
    }

    private async Task<T> As<T>(User user, string[] permissions, Func<IServiceProvider, ProcurementContext, Task<T>> act)
    {
        using var scope = fixture.Services.CreateScope();
        var requests = scope.ServiceProvider.GetRequiredService<ProcurementRequestService>();
        var ctx = await requests.ContextAsync(user.Id, permissions.ToHashSet(), CancellationToken.None);
        return await act(scope.ServiceProvider, ctx);
    }

    private Task As(User user, string[] permissions, Func<IServiceProvider, ProcurementContext, Task> act) => As(user, permissions, async (sp, c) =>
    {
        await act(sp, c);
        return true;
    });

    private static ProcurementRequestService R(IServiceProvider sp) => sp.GetRequiredService<ProcurementRequestService>();
    private static ProcurementOrderService O(IServiceProvider sp) => sp.GetRequiredService<ProcurementOrderService>();
    private static ProcurementSupplierService S(IServiceProvider sp) => sp.GetRequiredService<ProcurementSupplierService>();

    /// <summary>Заявка 100 пачек по 45 000 (без склада), отправлена и (approve) согласована финансистом (здесь — директор): 4,5 млн — не больше лимита.</summary>
    private async Task<(Guid Request, Guid Item)> RequestAsync(Org org, bool approve = true, decimal quantity = 100, decimal? price = 45_000)
    {
        var id = await As(org.Employee, Employee, (sp, c) => R(sp).CreateAsync(c, new ProcurementRequestInput("Бумага на квартал", null, ProcurementPriority.Normal, null,
            "Отчётность", null, [new(null, "Бумага А4", org.Category, "пачка", quantity, price)], Submit: true), default));
        if (approve)
        {
            await As(org.Director, Director, (sp, c) => R(sp).ApproveAsync(c, id, null, default));
        }

        var detail = await As(org.Supply, Supply, (sp, c) => R(sp).GetAsync(c, id, default));
        return (id, detail.Items[0].Id);
    }

    private Task<Guid> SupplierAsync(Org org, string name, string? inn = null) =>
        As(org.Supply, Supply, (sp, c) => S(sp).CreateAsync(c, new ProcurementSupplierInput(name, inn, "Менеджер", "+998 90 000 00 00", null, null, null, null, null,
            "Предоплата 50%", 5, null, [org.Category]), default));

    private static ProcurementOrderInput Order(Guid supplier, Guid item, decimal quantity, decimal price) =>
        new(supplier, null, null, FieldClock.Today.AddDays(10), null, 0, 0, true, 0, null, [new ProcurementOrderLineInput(item, quantity, price)]);

    [PostgresFact]
    public async Task Orders_need_an_approved_request_and_cannot_exceed_the_purchase_part()
    {
        var org = await SeedAsync();
        var supplier = await SupplierAsync(org, "ООО Бумага");
        var (pending, pendingItem) = await RequestAsync(org, approve: false);
        await Assert.ThrowsAsync<FieldConflictException>(() => // заявка ещё не утверждена директором
            As(org.Supply, Supply, (sp, c) => O(sp).CreateAsync(c, Order(supplier, pendingItem, 10, 40_000), default)));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => // заказы ведёт снабжение
            As(org.Employee, Employee, (sp, c) => O(sp).CreateAsync(c, Order(supplier, pendingItem, 10, 40_000), default)));

        var (request, item) = await RequestAsync(org);
        var a = await As(org.Supply, Supply, (sp, c) => O(sp).CreateAsync(c, Order(supplier, item, 60, 44_000) with { Submit = true }, default));
        var orderA = await As(org.Supply, Supply, (sp, c) => O(sp).GetAsync(c, a, default));
        Assert.Equal(ProcurementOrderStatus.Approved, orderA.Status); // дешевле утверждённых 45 000 — без директора
        Assert.Matches("^PO-\\d{4}-\\d{5}$", orderA.Number);
        Assert.Equal(0, orderA.ApprovalRound);

        // Вторая часть — другому поставщику; больше утверждённого не заказать.
        var supplier2 = await SupplierAsync(org, "ИП Канцтовары");
        await Assert.ThrowsAsync<FieldConflictException>(() =>
            As(org.Supply, Supply, (sp, c) => O(sp).CreateAsync(c, Order(supplier2, item, 41, 44_000), default)));
        var b = await As(org.Supply, Supply, (sp, c) => O(sp).CreateAsync(c, Order(supplier2, item, 40, 43_000), default));
        var req = await As(org.Supply, Supply, (sp, c) => R(sp).GetAsync(c, request, default));
        Assert.Equal((100m, ProcurementItemStatus.Ordered), (req.Items[0].OrderedQty, req.Items[0].Status));
        Assert.Equal(2, req.Orders!.Count);
        Assert.False(req.Can.Order); // всё заказано

        // Отмена освобождает количество и закрывает задачу ответственного.
        await Assert.ThrowsAsync<FieldValidationException>(() => As(org.Supply, Supply, (sp, c) => O(sp).CancelAsync(c, a, null, default))); // причина
        await As(org.Supply, Supply, (sp, c) => O(sp).CancelAsync(c, a, "Поставщик поднял цены", default));
        req = await As(org.Supply, Supply, (sp, c) => R(sp).GetAsync(c, request, default));
        Assert.Equal((40m, ProcurementItemStatus.ToPurchase), (req.Items[0].OrderedQty, req.Items[0].Status));
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var task = await db.WorkTasks.AsNoTracking().SingleAsync(t => t.Id == orderA.TaskId);
        Assert.Equal((WorkTaskStatus.Cancelled, $"/procurement/orders/{a}"), (task.Status, task.Link));
        Assert.Equal(org.Supply.Id, task.AssigneeId);
        Assert.NotNull((await As(org.Supply, Supply, (sp, c) => O(sp).GetAsync(c, b, default))).TaskId);
    }

    [PostgresFact]
    public async Task Order_above_the_approved_price_waits_for_the_director_and_changes_go_back_to_him()
    {
        var org = await SeedAsync();
        var supplier = await SupplierAsync(org, "ООО Дорогая Бумага");
        var (_, item) = await RequestAsync(org);
        var id = await As(org.Supply, Supply, (sp, c) => O(sp).CreateAsync(c, Order(supplier, item, 50, 48_000), default));
        await As(org.Supply, Supply, (sp, c) => O(sp).SubmitAsync(c, id, default));
        var order = await As(org.Director, Director, (sp, c) => O(sp).GetAsync(c, id, default));
        Assert.Equal((ProcurementOrderStatus.AwaitingApproval, 1), (order.Status, order.ApprovalRound));
        Assert.Contains("цена", order.ApprovalReasons);
        Assert.True(order.Can.Approve);
        await Assert.ThrowsAsync<FieldConflictException>(() => // ещё не утверждён — не отправить поставщику
            As(org.Supply, Supply, (sp, c) => O(sp).MarkOrderedAsync(c, id, null, default)));

        foreach (var (user, perms) in new[] { (org.Supply, Supply), (org.Admin, AllRights), (org.Employee, Employee) })
        {
            await Assert.ThrowsAsync<FieldForbiddenException>(() => As(user, perms, (sp, c) => O(sp).ApproveAsync(c, id, null, default)));
        }

        await Assert.ThrowsAsync<FieldValidationException>(() => As(org.Director, Director, (sp, c) => O(sp).RejectAsync(c, id, null, default)));
        await As(org.Director, Director, (sp, c) => O(sp).ApproveAsync(c, id, "Согласен, другой бумаги нет", default));
        await As(org.Director, Director, (sp, c) => O(sp).ApproveAsync(c, id, null, default)); // повторное нажатие
        await As(org.Supply, Supply, (sp, c) => O(sp).MarkOrderedAsync(c, id, null, default));
        order = await As(org.Supply, Supply, (sp, c) => O(sp).GetAsync(c, id, default));
        Assert.Equal((ProcurementOrderStatus.Ordered, FieldClock.Today), (order.Status, order.OrderDate));
        Assert.Single(order.Approvals);

        // Дешевле — без директора; дороже или другой поставщик — снова к директору (второй раунд).
        await As(org.Supply, Supply, (sp, c) => O(sp).UpdateAsync(c, id, Order(supplier, item, 50, 47_000) with { Version = order.Version }, default));
        order = await As(org.Supply, Supply, (sp, c) => O(sp).GetAsync(c, id, default));
        Assert.Equal(ProcurementOrderStatus.Ordered, order.Status);
        var other = await SupplierAsync(org, "ООО Другой Поставщик");
        await As(org.Supply, Supply, (sp, c) => O(sp).UpdateAsync(c, id, Order(other, item, 50, 47_000), default));
        order = await As(org.Director, Director, (sp, c) => O(sp).GetAsync(c, id, default));
        Assert.Equal((ProcurementOrderStatus.AwaitingApproval, 2), (order.Status, order.ApprovalRound));
        Assert.Contains(order.History, h => h.Action == "reapproval" && h.Comment!.Contains("сменился поставщик"));
        await As(org.Director, Director, (sp, c) => O(sp).ReturnAsync(c, id, "Почему сменили поставщика?", default));
        Assert.Equal(ProcurementOrderStatus.Draft, (await As(org.Supply, Supply, (sp, c) => O(sp).GetAsync(c, id, default))).Status);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        Assert.True(await db.UserNotifications.AnyAsync(n => n.RecipientId == org.Director.Id && n.Link == $"/procurement/orders/{id}" && n.Title.StartsWith("Повторное согласование")));
        Assert.True(await db.UserNotifications.AnyAsync(n => n.RecipientId == org.Employee.Id && n.Title.StartsWith("Заказано у поставщика")));
    }

    [PostgresFact]
    public async Task One_order_can_combine_requests_and_offers_are_compared()
    {
        var org = await SeedAsync();
        var cheap = await SupplierAsync(org, "ООО Дёшево");
        var fast = await SupplierAsync(org, "ООО Быстро");
        var (r1, i1) = await RequestAsync(org, quantity: 20);
        var (_, i2) = await RequestAsync(org, quantity: 30);

        ProcurementOfferInput Offer(Guid supplier, decimal price, int days, int quality) =>
            new(supplier, FieldClock.Today.AddDays(30), days, 100_000, 12, true, "Отсрочка 14 дней", "1 год", quality, null, [new(i1, price)]);
        var o1 = await As(org.Supply, Supply, (sp, c) => O(sp).CreateOfferAsync(c, r1, Offer(cheap, 40_000, 7, 3), default));
        var o2 = await As(org.Supply, Supply, (sp, c) => O(sp).CreateOfferAsync(c, r1, Offer(fast, 43_000, 2, 5), default));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Employee, Employee, (sp, c) => O(sp).CompareAsync(c, r1, S(sp), default)));
        var cmp = await As(org.Director, Director, (sp, c) => O(sp).CompareAsync(c, r1, S(sp), default));
        Assert.Equal(2, cmp.Offers.Count);
        var best = Assert.Single(cmp.Offers, o => o.BestTotal);
        Assert.Equal("ООО Дёшево", best.SupplierName);
        Assert.True(best.Lines[0].BestPrice);
        Assert.Equal("ООО Быстро", Assert.Single(cmp.Offers, o => o.Fastest).SupplierName);
        Assert.Equal(20 * 40_000m + 100_000m, best.Total);

        // Отклонённое предложение в сравнении не участвует: одно оставшееся — не «лучшее» и не «быстрее всех».
        await As(org.Supply, Supply, (sp, c) => O(sp).SetOfferStatusAsync(c, o1, true, default));
        cmp = await As(org.Supply, Supply, (sp, c) => O(sp).CompareAsync(c, r1, S(sp), default));
        Assert.DoesNotContain(cmp.Offers, o => o.BestTotal || o.Fastest || o.Lines.Any(l => l.BestPrice));
        await As(org.Supply, Supply, (sp, c) => O(sp).SetOfferStatusAsync(c, o1, false, default));
        Assert.Equal(o2, Assert.Single((await As(org.Supply, Supply, (sp, c) => O(sp).CompareAsync(c, r1, S(sp), default))).Offers, o => o.Fastest).Id);

        // Заказ из предложения: позиции двух заявок в одном заказе; предложение — выбрано.
        var id = await As(org.Supply, Supply, (sp, c) => O(sp).CreateAsync(c, new ProcurementOrderInput(cheap, o1, org.Supply2.Id, FieldClock.Today.AddDays(-2), null,
            0, 12, true, 100_000, null, [new(i1, 20, 40_000), new(i2, 30, 40_000)], Submit: true), default));
        var order = await As(org.Supply, Supply, (sp, c) => O(sp).GetAsync(c, id, default));
        Assert.Equal(2, order.Items.Select(i => i.RequestNumber).Distinct().Count());
        Assert.Equal(ProcurementOrderStatus.Approved, order.Status);
        Assert.Equal(org.Supply2.Id, order.ResponsibleId);
        await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Supply, Supply, (sp, c) => O(sp).SetOfferStatusAsync(c, o1, true, default)));
        cmp = await As(org.Supply, Supply, (sp, c) => O(sp).CompareAsync(c, r1, S(sp), default));
        Assert.Equal(ProcurementOfferStatus.Selected, cmp.Offers.Single(o => o.Id == o1).Status);

        // Контроль: просроченный заказ (ожидался позавчера), фильтр по ответственному, рейтинг поставщика.
        var overdue = await As(org.Supply, Supply, (sp, c) => O(sp).SearchAsync(c, new ProcurementOrderSearch("overdue"), default));
        Assert.Contains(overdue.Items, o => o.Id == id && o.Overdue);
        var mine = await As(org.Supply, Supply, (sp, c) => O(sp).SearchAsync(c, new ProcurementOrderSearch(ResponsibleId: org.Supply2.Id), default));
        Assert.Equal(id, Assert.Single(mine.Items).Id);
        var summary = await As(org.Supply, Supply, (sp, c) => O(sp).SummaryAsync(c, default));
        Assert.True(summary.Overdue >= 1);
        var card = await As(org.Supply, Supply, (sp, c) => S(sp).GetAsync(c, cheap, default));
        Assert.Equal((0m, 60m, 100m), (card.Rating.OnTime, card.Rating.Quality, card.Rating.Prices)); // просрочен; качество 3/5; цена как в предложении
        Assert.Equal(1, card.Rating.OverdueOrders);
        Assert.Equal(2, card.Prices.Count);
    }

    [PostgresFact]
    public async Task Suppliers_have_unique_inn_import_from_csv_and_documents_are_protected()
    {
        var org = await SeedAsync();
        var inn = $"30{Random.Shared.Next(1_000_000, 9_999_999)}";
        await SupplierAsync(org, "ООО Первый", inn);
        await Assert.ThrowsAsync<FieldValidationException>(() => SupplierAsync(org, "ООО Двойник", inn));
        await Assert.ThrowsAsync<FieldForbiddenException>(() =>
            As(org.Employee, Employee, (sp, c) => S(sp).SearchAsync(c, new ProcurementSupplierSearch(), default)));

        var suffix = Guid.NewGuid().ToString("N")[..5];
        var csv = $"Название;ИНН;Телефон;Email;Категории;Срок поставки\n" +
                  $"ООО Импорт {suffix};;+998 71 200 00 00;sale@import.uz;Канцелярия;7\n" +
                  $"ООО Первый (обновлён);{inn};+998 71 111 11 11;;;\n" +
                  $"ООО Ошибка {suffix};12;;;;\n";
        var result = await As(org.Supply, Supply, (sp, c) => S(sp).ImportAsync(c, new MemoryStream(Encoding.UTF8.GetBytes(csv)), "suppliers.csv", default));
        Assert.Equal((1, 1, 1), (result.Created, result.Updated, result.Skipped));
        Assert.Contains("ИНН", Assert.Single(result.Errors));
        var list = await As(org.Supply, Supply, (sp, c) => S(sp).SearchAsync(c, new ProcurementSupplierSearch($"Импорт {suffix}"), default));
        var imported = Assert.Single(list.Items);
        Assert.Equal(["Канцелярия"], imported.Categories);
        var updated = await As(org.Supply, Supply, (sp, c) => S(sp).SearchAsync(c, new ProcurementSupplierSearch(inn), default));
        Assert.Equal("ООО Первый (обновлён)", Assert.Single(updated.Items).Name);

        // Договор поставщика: прикрепляет снабжение, видит кладовщик; сотрудник — нет.
        var storage = fixture.Services.GetRequiredService<IFileStorage>();
        var doc = await As(org.Supply, Supply, (sp, c) => sp.GetRequiredService<ProcurementDocumentService>()
            .AttachAsync(c, ProcurementOrderRules.SupplierDoc, imported.Id, new ProcurementUpload("договор.pdf", 3, new MemoryStream([1, 2, 3])), "contract", storage, default));
        var keeperDocs = await As(org.Employee, [Permissions.ProcurementWarehouse], (sp, c) =>
            sp.GetRequiredService<ProcurementDocumentService>().ListAsync(c, ProcurementOrderRules.SupplierDoc, imported.Id, default));
        Assert.Equal(("договор.pdf", "contract"), (keeperDocs.Single().FileName, keeperDocs.Single().Kind));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Employee, Employee, (sp, c) =>
            sp.GetRequiredService<ProcurementDocumentService>().DownloadAsync(c, doc, default)));
    }

    /// <summary>
    /// Реальный вид таблицы снабжения: заголовки на узбекском и с опечаткой, поставщик на нескольких строках (ещё телефон, ещё сырьё), «Нет» вместо
    /// фирмы, «Йук» — не работаем, повтор по ИНН, столбцы без заголовка, строка с одним номером. Сначала проверка (ничего не сохраняется), потом импорт.
    /// </summary>
    [PostgresFact]
    public async Task Supplier_import_reads_real_world_sheet_with_continuation_rows_and_uzbek_headers()
    {
        var org = await SeedAsync();
        var s = Guid.NewGuid().ToString("N")[..5];
        var inn = $"30{Random.Shared.Next(1_000_000, 9_999_999)}";
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet("Sheet1");
        object[][] rows =
        [
            ["№", "Поставшик", "сирё", "Етказувчи Манзили", "Фирмаси", "ИНН", "Тел номер", "Ишловчи Ходим", "Олинадиган Хомашёлар", "Хозирда ишланади ёки ишланмаяпти"],
            [1, $"Арахис {s}", "сирё", "ТОШКЕНТ", $"OOO «Тест {s}»", inn, "90 000 00 01", "Анвар", "Арахис очищенный", "Ишлаймиз"],
            ["", "", "", "", "", "", "90 000 00 02", "", "Арахис жареный"],
            ["", "", "", "", "", "", "", "", $"Кунжут {s}", "", "Сверка учун", "99 000 00 03"],
            [2, $"Масло {s}", "сирё", "", "Нет", "", "91 000 00 00", "", "Масло подсолнечное", "Йук"],
            [3, $"Арахис {s} опт", "сирё", "", "", inn, "90 000 00 01", "", "Арахис очищенный"],
            [4],
            [5, $"Ошибка {s}", "", "", "", "12"],
        ];
        for (var r = 0; r < rows.Length; r++)
        {
            for (var c = 0; c < rows[r].Length; c++)
            {
                sheet.Cell(r + 1, c + 1).Value = rows[r][c] is int n ? n : (string)rows[r][c];
            }
        }

        var file = new MemoryStream();
        book.SaveAs(file);
        Task<ProcurementSupplierImportResult> Import(bool dryRun) => As(org.Supply, Supply, (sp, c) =>
            S(sp).ImportAsync(c, new MemoryStream(file.ToArray()), "Поставщики контакты.xlsx", default, dryRun));

        var check = await Import(true);
        Assert.True(check.DryRun);
        Assert.Equal((2, 0, 1), (check.Created, check.Updated, check.Skipped));
        Assert.Equal((2, 1, 1), (check.Joined, check.Duplicates, check.Inactive));
        Assert.Contains("Название ← «Поставшик»", check.Columns!);
        Assert.Contains("Фирма ← «Фирмаси»", check.Columns!);
        Assert.Contains("Что поставляет ← «Олинадиган Хомашёлар»", check.Columns!);
        Assert.Contains("Работаем ← «Хозирда ишланади ёки ишланмаяпти»", check.Columns!);
        Assert.Contains("ИНН", Assert.Single(check.Errors));
        Assert.Equal(3, check.Sample!.First(x => x.Name == $"Арахис {s}").Products);
        Assert.Empty((await As(org.Supply, Supply, (sp, c) => S(sp).SearchAsync(c, new ProcurementSupplierSearch(s, Active: null), default))).Items);

        var done = await Import(false);
        Assert.Equal((2, 0, 1), (done.Created, done.Updated, done.Skipped));
        var found = await As(org.Supply, Supply, (sp, c) => S(sp).SearchAsync(c, new ProcurementSupplierSearch($"кунжут {s}"), default));
        var card = await As(org.Supply, Supply, (sp, c) => S(sp).GetAsync(c, Assert.Single(found.Items).Id, default));
        Assert.Equal(($"Арахис {s}", $"OOO «Тест {s}»", inn, "Анвар", "ТОШКЕНТ"), (card.Name, card.LegalName, card.Inn, card.ContactPerson, card.Address));
        Assert.Equal("90 000 00 01, 90 000 00 02", card.Phone);
        Assert.Equal($"Арахис очищенный\nАрахис жареный\nКунжут {s}", card.Products);
        Assert.Equal("Сверка учун 99 000 00 03", card.Notes);
        Assert.Equal(["Сырьё"], card.Categories.Select(c => c.Name));
        Assert.True(card.IsActive);
        var oil = Assert.Single((await As(org.Supply, Supply, (sp, c) => S(sp).SearchAsync(c, new ProcurementSupplierSearch($"Масло {s}", Active: null), default))).Items);
        Assert.False(oil.IsActive);
        Assert.Null(oil.LegalName);

        // Повторный импорт того же файла: те же поставщики обновляются, телефоны и сырьё не задваиваются.
        var again = await Import(false);
        Assert.Equal((0, 2, 1), (again.Created, again.Updated, again.Skipped));
        card = await As(org.Supply, Supply, (sp, c) => S(sp).GetAsync(c, card.Id, default));
        Assert.Equal("90 000 00 01, 90 000 00 02", card.Phone);

        // CSV с заголовками-фразами: «Наименование поставщика», «ИНН поставщика», «Тел.».
        var csv = $"Наименование поставщика;ИНН поставщика;Тел.;Что поставляет\nООО Префикс {s};;+998 90 111 11 11;Бумага\n";
        var prefix = await As(org.Supply, Supply, (sp, c) => S(sp).ImportAsync(c, new MemoryStream(Encoding.UTF8.GetBytes(csv)), "suppliers.csv", default));
        Assert.Equal(1, prefix.Created);
        Assert.Contains("Название ← «Наименование поставщика»", prefix.Columns!);
        Assert.Contains("ИНН ← «ИНН поставщика»", prefix.Columns!);
        Assert.Contains("Телефон ← «Тел.»", prefix.Columns!);
    }
}
