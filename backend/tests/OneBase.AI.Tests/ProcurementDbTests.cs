using System.Text.RegularExpressions;
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
/// Раздел «Заявки» на настоящем PostgreSQL (ONEBASE_TEST_POSTGRES, иначе пропускается): сотрудник видит только свои заявки, руководитель —
/// отправленные заявки своего отдела, снабжение — все отправленные, черновик — только инициатор; действия проверяются на сервере (передать
/// директору — только снабжение); номера без повторов при одновременном создании; повтор формы не создаёт вторую заявку; возврат на
/// доработку — с комментарием, правка пишет «было/стало» в историю; проверка полей, категорий и вложений.
/// </summary>
public class ProcurementDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly string[] EmployeePermissions = [Permissions.ProcurementRequest];
    private static readonly string[] SupplyPermissions = [Permissions.ProcurementRequest, Permissions.ProcurementRead, Permissions.ProcurementManage];
    private static readonly string[] DirectorPermissions = [Permissions.ProcurementRequest, Permissions.ProcurementRead, Permissions.ProcurementSettings];

    private sealed record Org(Department A, Department B, User Employee, User Colleague, User Other, User Head, User Supply, User Director, Guid Category, string Unit);

    private async Task<Org> SeedAsync()
    {
        var s = Guid.NewGuid().ToString("N")[..6];
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await DbSeeder.SeedProcurementAsync(db, CancellationToken.None);

        var deptA = new Department { Code = $"pr-a-{s}", Name = $"Производство {s}" };
        var deptB = new Department { Code = $"pr-b-{s}", Name = $"Маркетинг {s}" };
        var supplyRole = new Role { Name = $"Снабжение {s}" };
        supplyRole.Permissions.Add(new RolePermission { Code = Permissions.ProcurementManage });
        var directorRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == SystemRoles.Director) ?? new Role { Name = SystemRoles.Director };

        User Person(string login, string name, Department? department, Role? role = null)
        {
            var u = new User { Login = $"{login}-{s}", FullName = name, Department = department };
            if (role is not null)
            {
                u.Roles.Add(new UserRole { Role = role });
            }

            return u;
        }

        var employee = Person("pr-emp", "Азизова Нигора", deptA);
        var colleague = Person("pr-col", "Каримов Бахтиёр", deptA);
        var other = Person("pr-oth", "Юсупова Дилноза", deptB);
        var head = Person("pr-head", "Рахимов Шерзод", deptA);
        var supply = Person("pr-sup", "Ташкентов Улугбек", null, supplyRole);
        var director = Person("pr-dir", "Директор Компании", null, directorRole);
        db.Users.AddRange(employee, colleague, other, head, supply, director);
        db.WorkDepartmentHeads.Add(new WorkDepartmentHead { Department = deptA, UserId = head.Id });
        await db.SaveChangesAsync();
        // Маршрут: заявку сотрудника — руководителю отдела, затем финансисту (здесь им назначен директор), лимит 5 млн.
        var settings = await db.ProcurementSettings.FirstOrDefaultAsync() ?? db.ProcurementSettings.Add(new ProcurementSettings()).Entity;
        (settings.FinanceApproverId, settings.DirectorApproverId, settings.DirectorLimit) = (director.Id, null, ProcurementSettings.DefaultDirectorLimit);
        await db.SaveChangesAsync();

        var category = await db.ProcurementCategories.Where(c => c.Code == "stationery").Select(c => c.Id).SingleAsync();
        return new Org(deptA, deptB, employee, colleague, other, head, supply, director, category, "пачка");
    }

    private static async Task<T> AsAsync<T>(IServiceProvider root, User user, string[] permissions, Func<ProcurementRequestService, ProcurementContext, Task<T>> act)
    {
        using var scope = root.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ProcurementRequestService>();
        var ctx = await service.ContextAsync(user.Id, permissions.ToHashSet(), CancellationToken.None);
        return await act(service, ctx);
    }

    private static Task AsAsync(IServiceProvider root, User user, string[] permissions, Func<ProcurementRequestService, ProcurementContext, Task> act) =>
        AsAsync(root, user, permissions, async (s, c) =>
        {
            await act(s, c);
            return true;
        });

    private static ProcurementRequestInput Input(Org org, bool submit = false, Guid? key = null, params ProcurementItemInput[] items) => new(
        "Канцелярия на ноябрь", null, ProcurementPriority.High, FieldClock.Today.AddDays(7), "Закончилась бумага для отчётов", null,
        items.Length > 0 ? items : [new(null, "Бумага А4", org.Category, org.Unit, 20, 45_000), new(null, "Папки", org.Category, "шт", 30, 12_000)], submit, key);

    private async Task<List<UserNotification>> NotificationsAsync(User user, Guid requestId)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        return await db.UserNotifications.AsNoTracking().Where(n => n.RecipientId == user.Id && n.Link == $"/requests/{requestId}").OrderBy(n => n.CreatedAt).ToListAsync();
    }

    [PostgresFact]
    public async Task Requests_are_isolated_and_actions_are_checked_on_the_server()
    {
        var org = await SeedAsync();
        var sp = fixture.Services;

        // Без права «Заявки» раздел закрыт.
        await Assert.ThrowsAsync<FieldForbiddenException>(() => AsAsync(sp, org.Other, [], (s, c) => s.NavAsync(c, default)));

        var id = await AsAsync(sp, org.Employee, EmployeePermissions, (s, c) => s.CreateAsync(c, Input(org), default));
        var draft = await AsAsync(sp, org.Employee, EmployeePermissions, (s, c) => s.GetAsync(c, id, default));
        Assert.Equal(ProcurementRequestStatus.Draft, draft.Status);
        Assert.Matches(new Regex(@"^PR-\d{4}-\d{5}$"), draft.Number);
        Assert.Equal(org.A.Id, draft.DepartmentId); // отдел — из карточки пользователя
        Assert.Equal(1_260_000m, draft.EstimatedTotal); // 20 × 45 000 + 30 × 12 000
        Assert.True(draft.Can.Edit && draft.Can.Submit && draft.Can.Cancel && !draft.Can.Forward);

        // Черновик не видит никто, кроме инициатора.
        foreach (var (user, perms) in new[] { (org.Head, EmployeePermissions), (org.Supply, SupplyPermissions), (org.Other, EmployeePermissions) })
        {
            await Assert.ThrowsAsync<FieldForbiddenException>(() => AsAsync(sp, user, perms, (s, c) => s.GetAsync(c, id, default)));
        }

        var supplyDrafts = await AsAsync(sp, org.Supply, SupplyPermissions, (s, c) => s.SearchAsync(c, new ProcurementRequestSearch(PageSize: 200), default));
        Assert.DoesNotContain(supplyDrafts.Items, r => r.Id == id);

        await AsAsync(sp, org.Employee, EmployeePermissions, (s, c) => s.SubmitAsync(c, id, default));
        await AsAsync(sp, org.Employee, EmployeePermissions, (s, c) => s.SubmitAsync(c, id, default)); // двойное нажатие — без второго события

        // Отправленную видят руководитель отдела и снабжение; коллега и сотрудник другого отдела — нет.
        var headList = await AsAsync(sp, org.Head, EmployeePermissions, (s, c) => s.SearchAsync(c, new ProcurementRequestSearch("department"), default));
        Assert.Contains(headList.Items, r => r.Id == id);
        var supplyList = await AsAsync(sp, org.Supply, SupplyPermissions, (s, c) => s.SearchAsync(c, new ProcurementRequestSearch(Search: "бумага а4"), default));
        Assert.Contains(supplyList.Items, r => r.Id == id && r.ItemsCount == 2 && r.InitiatorName == "Азизова Нигора");
        var byNumber = await AsAsync(sp, org.Supply, SupplyPermissions, (s, c) => s.SearchAsync(c, new ProcurementRequestSearch(Search: draft.Number), default));
        Assert.Equal(id, Assert.Single(byNumber.Items).Id);
        foreach (var user in new[] { org.Colleague, org.Other })
        {
            var list = await AsAsync(sp, user, EmployeePermissions, (s, c) => s.SearchAsync(c, new ProcurementRequestSearch(PageSize: 200), default));
            Assert.DoesNotContain(list.Items, r => r.Id == id);
            await Assert.ThrowsAsync<FieldForbiddenException>(() => AsAsync(sp, user, EmployeePermissions, (s, c) => s.GetAsync(c, id, default)));
        }

        // Заявка сотрудника — у руководителя отдела. Его шаг решает только он: инициатор, снабжение и директор — нет (прямые вызовы API).
        Assert.Equal(ProcurementRequestStatus.HeadApproval, (await AsAsync(sp, org.Employee, EmployeePermissions, (s, c) => s.GetAsync(c, id, default))).Status);
        Assert.Contains((await AsAsync(sp, org.Head, EmployeePermissions, (s, c) => s.SearchAsync(c, new ProcurementRequestSearch("approvals"), default))).Items, r => r.Id == id);
        foreach (var (user, perms) in new[] { (org.Employee, EmployeePermissions), (org.Supply, SupplyPermissions), (org.Director, DirectorPermissions) })
        {
            await Assert.ThrowsAsync<FieldForbiddenException>(() => AsAsync(sp, user, perms, (s, c) => s.ApproveAsync(c, id, null, default)));
        }

        await AsAsync(sp, org.Head, EmployeePermissions, (s, c) => s.ApproveAsync(c, id, null, default));

        // Заявку на согласовании инициатор не правит.
        await Assert.ThrowsAsync<FieldConflictException>(() => AsAsync(sp, org.Employee, EmployeePermissions, (s, c) => s.UpdateAsync(c, id, Input(org), default)));

        var detail = await AsAsync(sp, org.Director, DirectorPermissions, (s, c) => s.GetAsync(c, id, default));
        Assert.Equal(ProcurementRequestStatus.FinanceApproval, detail.Status);
        Assert.Equal(["created", "submitted", "step_approved"], detail.History.Select(h => h.Action));
        Assert.Equal(nameof(ProcurementRequestStatus.HeadApproval), detail.History[2].FromStatus);
        Assert.Equal("Рахимов Шерзод", detail.History[2].ActorName);
        Assert.True(detail.Can.Approve && detail.Can.Return && detail.Can.Price); // финансист (здесь — директор) решает и может уточнить цены
        Assert.Equal([("head", "done"), ("finance", "current"), ("director", "skipped"), ("supply", "pending")], detail.Route!.Select(x => (x.Key, x.State)));
        Assert.False(detail.Can.Forward || detail.Can.Edit || detail.Can.Issue);

        // Обзор: у директора — заявка у него на согласовании; у сотрудника другого отдела её нет ни в одном числе.
        var overview = await AsAsync(sp, org.Director, DirectorPermissions, (s, c) => s.OverviewAsync(c, default));
        Assert.True(overview.ByStatus[nameof(ProcurementRequestStatus.FinanceApproval)] >= 1);
        Assert.Contains(overview.NeedsAction, r => r.Id == id);
        Assert.Contains(overview.Recent, r => r.Id == id);
        Assert.Contains(overview.MonthCategories, c => c.Name == "Канцелярия" && c.Total >= 900_000m);
        var otherOverview = await AsAsync(sp, org.Other, EmployeePermissions, (s, c) => s.OverviewAsync(c, default));
        Assert.DoesNotContain(otherOverview.Recent, r => r.Id == id);
        Assert.Equal(0, otherOverview.MonthRequests);
        var supplyNav = await AsAsync(sp, org.Supply, SupplyPermissions, (s, c) => s.NavAsync(c, default));
        Assert.True(supplyNav.Manages && supplyNav.ReadsAll && !supplyNav.Configures);

        Assert.Contains(await NotificationsAsync(org.Head, id), n => n.Kind == UserNotificationKind.ProcurementRequest && n.Title.Contains("ждёт вашего согласования"));
        Assert.Empty(await NotificationsAsync(org.Supply, id)); // снабжение узнаёт о заявке, когда её согласуют
        Assert.Contains(await NotificationsAsync(org.Director, id), n => n.Kind == UserNotificationKind.ProcurementRequest && n.Title.Contains("ждёт вашего согласования"));
        Assert.Contains(await NotificationsAsync(org.Employee, id), n => n.Kind == UserNotificationKind.ProcurementStatus);
        Assert.Empty(await NotificationsAsync(org.Colleague, id));
    }

    [PostgresFact]
    public async Task Route_goes_from_the_head_to_the_financier_and_above_the_limit_to_the_chosen_director()
    {
        var org = await SeedAsync();
        var sp = fixture.Services;
        var s = Guid.NewGuid().ToString("N")[..6];
        var financier = new User { Login = $"pr-fin-{s}", FullName = "Финансист Маршрута" };
        var chosen = new User { Login = $"pr-chd-{s}", FullName = "Директор Маршрута" };
        async Task Route(Guid? finance, Guid? director)
        {
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var settings = await db.ProcurementSettings.FirstAsync();
            (settings.FinanceApproverId, settings.DirectorApproverId, settings.DirectorLimit) = (finance, director, 5_000_000);
            await db.SaveChangesAsync();
        }

        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            db.Users.AddRange(financier, chosen);
            await db.SaveChangesAsync();
        }

        // Финансист ещё не выбран, директор маршрута — выбранный пользователь (роль «Директор» больше не решает).
        await Route(null, chosen.Id);
        ProcurementRequestInput Machine(decimal price) =>
            Input(org, submit: true, items: new ProcurementItemInput(null, "Станок упаковочный", org.Category, "шт", 1, price)) with { Title = "Станок" };

        // 1. Заявка сотрудника на 6 млн: руководитель отдела согласует — дальше финансист; он не выбран — заявка ждёт его.
        var id = await AsAsync(sp, org.Employee, EmployeePermissions, (x, c) => x.CreateAsync(c, Machine(6_000_000), default));
        await AsAsync(sp, org.Head, EmployeePermissions, (x, c) => x.ApproveAsync(c, id, "Нужен для новой линии", default));
        var waiting = await AsAsync(sp, org.Employee, EmployeePermissions, (x, c) => x.GetAsync(c, id, default));
        Assert.Equal(ProcurementRequestStatus.FinanceApproval, waiting.Status);
        Assert.Empty(waiting.Route![1].Who); // финансист не выбран
        Assert.Equal(("done", "Рахимов Шерзод", "Нужен для новой линии"), (waiting.Route[0].State, waiting.Route[0].Who.Single(), waiting.Route[0].Comment));

        // 2. Выбрали финансиста: он видит заявку в «Ждут моего согласования», снижает цену до 4,8 млн — директор не нужен.
        await Route(financier.Id, chosen.Id);
        var queue = await AsAsync(sp, financier, EmployeePermissions, (x, c) => x.SearchAsync(c, new ProcurementRequestSearch("approvals"), default));
        Assert.Contains(queue.Items, r => r.Id == id);
        var item = waiting.Items[0].Id;
        await Assert.ThrowsAsync<FieldValidationException>(() =>
            AsAsync(sp, financier, EmployeePermissions, (x, c) => x.ApproveAsync(c, id, null, default, [new ProcurementPriceInput(item, 0)])));
        await AsAsync(sp, financier, EmployeePermissions, (x, c) => x.ApproveAsync(c, id, null, default, [new ProcurementPriceInput(item, 4_800_000)]));
        var approved = await AsAsync(sp, org.Supply, SupplyPermissions, (x, c) => x.GetAsync(c, id, default));
        Assert.Equal((ProcurementRequestStatus.Approved, 4_800_000m), (approved.Status, approved.PurchaseTotal));
        Assert.Contains(approved.History, h => h.Action == "prices" && h.Text.Contains($"{ProcurementRules.Money(6_000_000)} → {ProcurementRules.Money(4_800_000)}"));
        Assert.Equal([("head", "done"), ("finance", "done"), ("director", "skipped"), ("supply", "current")], approved.Route!.Select(x => (x.Key, x.State)));
        Assert.Equal([ProcurementApprovalStages.Head, ProcurementApprovalStages.Finance], approved.Approvals!.Select(a => a.Stage));
        Assert.Contains(await NotificationsAsync(org.Supply, id), n => n.Title.StartsWith("Согласована к закупке"));
        Assert.Contains(await NotificationsAsync(org.Employee, id), n => n.Title.Contains("согласована — передана в снабжение"));

        // 3. Заявка самого руководителя на 7 млн: без шага руководителя — сразу финансисту, затем директору маршрута (больше лимита).
        var own = await AsAsync(sp, org.Head, EmployeePermissions, (x, c) => x.CreateAsync(c, Machine(7_000_000), default));
        var atFinance = await AsAsync(sp, financier, EmployeePermissions, (x, c) => x.GetAsync(c, own, default));
        Assert.Equal(ProcurementRequestStatus.FinanceApproval, atFinance.Status);
        Assert.Equal(("skipped", "initiatorHead"), (atFinance.Route![0].State, atFinance.Route[0].Reason));
        Assert.Equal("pending", atFinance.Route[2].State); // 7 млн больше лимита — директор впереди
        await AsAsync(sp, financier, EmployeePermissions, (x, c) => x.ApproveAsync(c, own, null, default));
        Assert.Equal(ProcurementRequestStatus.DirectorApproval, (await AsAsync(sp, org.Head, EmployeePermissions, (x, c) => x.GetAsync(c, own, default))).Status);
        // Директор по роли не решает — решает выбранный в настройках; администратор со всеми правами — тоже нет.
        await Assert.ThrowsAsync<FieldForbiddenException>(() => AsAsync(sp, org.Director, DirectorPermissions, (x, c) => x.ApproveAsync(c, own, null, default)));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => AsAsync(sp, org.Supply, [.. Permissions.All], (x, c) => x.ApproveAsync(c, own, null, default)));
        Assert.Contains(await NotificationsAsync(chosen, own), n => n.Title.Contains("ждёт вашего согласования"));
        Assert.DoesNotContain(await NotificationsAsync(org.Director, own), n => n.Title.Contains("ждёт вашего согласования"));
        await AsAsync(sp, chosen, [], (x, c) => x.ApproveAsync(c, own, "Согласовано", default));
        var done = await AsAsync(sp, org.Head, EmployeePermissions, (x, c) => x.GetAsync(c, own, default));
        Assert.Equal(ProcurementRequestStatus.Approved, done.Status);
        Assert.Equal([ProcurementApprovalStages.Finance, ProcurementApprovalStages.Director], done.Approvals!.Select(a => a.Stage));
        Assert.Contains(done.History, h => h.Text.Contains($"больше лимита {ProcurementRules.Money(5_000_000)} сум, передана директору"));
        Assert.Equal($"Утверждена директором: к закупке ≈ {ProcurementRules.Money(7_000_000)} сум — передана в снабжение", done.History[^1].Text);

        await Route(org.Director.Id, null); // общие настройки класса
    }

    [PostgresFact]
    public async Task Numbers_are_unique_under_concurrency_and_a_repeated_form_creates_one_request()
    {
        var org = await SeedAsync();
        var sp = fixture.Services;

        var ids = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ =>
            AsAsync(sp, org.Employee, EmployeePermissions, (s, c) => s.CreateAsync(c, Input(org), default))));
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var numbers = await db.ProcurementRequests.Where(r => ids.Contains(r.Id)).Select(r => r.Number).ToListAsync();
            Assert.Equal(12, numbers.Distinct().Count());
        }

        var key = Guid.NewGuid();
        var twice = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            AsAsync(sp, org.Employee, EmployeePermissions, (s, c) => s.CreateAsync(c, Input(org, submit: true, key: key), default))));
        Assert.Equal(twice[0], twice[1]);
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            Assert.Equal(1, await db.ProcurementRequests.CountAsync(r => r.ClientKey == key));
            Assert.Equal(1, await db.ProcurementAuditLogs.CountAsync(e => e.DocId == twice[0] && e.Action == "submitted"));
        }
    }

    [PostgresFact]
    public async Task Revision_needs_a_comment_and_edits_are_written_to_history()
    {
        var org = await SeedAsync();
        var sp = fixture.Services;
        var id = await AsAsync(sp, org.Employee, EmployeePermissions, (s, c) => s.CreateAsync(c, Input(org, submit: true), default));

        await Assert.ThrowsAsync<FieldValidationException>(() => AsAsync(sp, org.Head, EmployeePermissions, (s, c) => s.ReturnAsync(c, id, "  ", default)));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => AsAsync(sp, org.Supply, SupplyPermissions, (s, c) => s.ReturnAsync(c, id, "Не их шаг", default)));
        await AsAsync(sp, org.Head, EmployeePermissions, (s, c) => s.ReturnAsync(c, id, "Уточните формат бумаги", default));

        var returned = await AsAsync(sp, org.Employee, EmployeePermissions, (s, c) => s.GetAsync(c, id, default));
        Assert.Equal(ProcurementRequestStatus.RevisionRequired, returned.Status);
        Assert.Equal("Уточните формат бумаги", returned.RevisionComment);
        Assert.Contains(await NotificationsAsync(org.Employee, id), n => n.Body == "Уточните формат бумаги");

        var paper = returned.Items[0];
        var edited = Input(org, submit: true, items:
        [
            new(paper.Id, paper.Name, paper.CategoryId, paper.UnitCode, 25, paper.EstimatedPrice, null),
            new(null, "Картриджи", org.Category, "шт", 5, 300_000, null),
        ]) with { Version = returned.Version };
        await AsAsync(sp, org.Employee, EmployeePermissions, (s, c) => s.UpdateAsync(c, id, edited, default));

        var after = await AsAsync(sp, org.Supply, SupplyPermissions, (s, c) => s.GetAsync(c, id, default));
        Assert.Equal(ProcurementRequestStatus.HeadApproval, after.Status);
        Assert.Equal(["Бумага А4", "Картриджи"], after.Items.Select(i => i.Name));
        Assert.Equal(25 * 45_000m + 5 * 300_000m, after.EstimatedTotal);
        Assert.Equal(["created", "submitted", "returned", "updated", "resubmitted"], after.History.Select(h => h.Action));
        var update = after.History[3].Text;
        Assert.Contains("количество 20 пачка → 25 пачка", update);
        Assert.Contains("удалена позиция «Папки»", update);
        Assert.Contains("добавлена позиция «Картриджи»", update);
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var changes = await db.ProcurementAuditLogs.Where(e => e.DocId == id && e.Action == "updated").Select(e => e.Changes).SingleAsync();
            Assert.Contains("\"before\"", changes);
            Assert.Contains("Папки", changes);
        }

        // Правка по устаревшей версии (другая вкладка) — 409, а не тихая перезапись.
        var draftId = await AsAsync(sp, org.Employee, EmployeePermissions, (s, c) => s.CreateAsync(c, Input(org), default));
        var v1 = (await AsAsync(sp, org.Employee, EmployeePermissions, (s, c) => s.GetAsync(c, draftId, default))).Version;
        await AsAsync(sp, org.Employee, EmployeePermissions, (s, c) => s.UpdateAsync(c, draftId, Input(org) with { Title = "Новое название", Version = v1 }, default));
        await Assert.ThrowsAsync<FieldConflictException>(() =>
            AsAsync(sp, org.Employee, EmployeePermissions, (s, c) => s.UpdateAsync(c, draftId, Input(org) with { Version = v1 }, default)));
    }

    [PostgresFact]
    public async Task Fields_departments_and_categories_are_validated()
    {
        var org = await SeedAsync();
        var sp = fixture.Services;
        Task Create(ProcurementRequestInput input, User? user = null, string[]? perms = null) =>
            AsAsync(sp, user ?? org.Employee, perms ?? EmployeePermissions, (s, c) => s.CreateAsync(c, input, default));

        await Assert.ThrowsAsync<FieldValidationException>(() => Create(Input(org) with { Items = [] }));
        await Assert.ThrowsAsync<FieldValidationException>(() => Create(Input(org, items: new ProcurementItemInput(null, "Бумага", org.Category, org.Unit, 0, null, null))));
        await Assert.ThrowsAsync<FieldValidationException>(() => Create(Input(org, items: new ProcurementItemInput(null, "Бумага", org.Category, "ящик", 1, null, null))));
        await Assert.ThrowsAsync<FieldValidationException>(() => Create(Input(org) with { Title = " " }));
        await Assert.ThrowsAsync<FieldValidationException>(() => Create(Input(org, submit: true) with { Reason = null }));
        // Цена у позиции к закупке обязательна при отправке (черновик без неё сохраняется).
        var noPrice = new ProcurementItemInput(null, "Папки", org.Category, "шт", 30, null);
        var priceError = await Assert.ThrowsAsync<FieldValidationException>(() => Create(Input(org, submit: true, items: noPrice)));
        Assert.Contains("укажите примерную цену", priceError.Message);
        await Create(Input(org, items: noPrice));
        await Assert.ThrowsAsync<FieldValidationException>(() => Create(Input(org) with { DesiredDate = FieldClock.Today.AddDays(-1) }));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => Create(Input(org) with { DepartmentId = org.B.Id })); // чужой отдел

        // Руководитель подаёт за свой отдел, снабжение — за любой.
        await Create(Input(org) with { DepartmentId = org.A.Id }, org.Head);
        await Create(Input(org) with { DepartmentId = org.B.Id }, org.Supply, SupplyPermissions);

        // Категории настраивают директор и администратор; отключённую нельзя выбрать в новой позиции.
        var name = $"Упаковка {Guid.NewGuid():N}"[..16];
        await Assert.ThrowsAsync<FieldForbiddenException>(() =>
            AsAsync(sp, org.Employee, EmployeePermissions, (s, c) => s.CreateCategoryAsync(c, new ProcurementCategoryInput(name, null), default)));
        var category = await AsAsync(sp, org.Director, DirectorPermissions, (s, c) => s.CreateCategoryAsync(c, new ProcurementCategoryInput(name, null), default));
        await Assert.ThrowsAsync<FieldValidationException>(() =>
            AsAsync(sp, org.Director, DirectorPermissions, (s, c) => s.CreateCategoryAsync(c, new ProcurementCategoryInput(name.ToUpperInvariant(), null), default)));
        await AsAsync(sp, org.Director, DirectorPermissions, (s, c) => s.UpdateCategoryAsync(c, category, new ProcurementCategoryInput(name, null, false), default));
        await Assert.ThrowsAsync<FieldValidationException>(() => Create(Input(org, items: new ProcurementItemInput(null, "Коробки", category, "шт", 10, null, null))));
        var form = await AsAsync(sp, org.Employee, EmployeePermissions, (s, c) => s.FormAsync(c, default));
        Assert.DoesNotContain(form.Categories, c => c.Id == category);
        Assert.Equal(15, form.Categories.Count(c => c.Code is not null)); // категории из ТЗ
        Assert.Equal(org.A.Id, form.DefaultDepartmentId);
        Assert.Equal([org.A.Id], form.Departments.Select(d => d.Id));

        // Повторный засев справочников не дублирует категории и единицы.
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await DbSeeder.SeedProcurementAsync(db, CancellationToken.None);
        Assert.Equal(15, await db.ProcurementCategories.CountAsync(c => c.Code != null));
    }

    [PostgresFact]
    public async Task Attachments_are_checked_and_visible_only_with_the_request()
    {
        var org = await SeedAsync();
        var sp = fixture.Services;
        var storage = sp.GetRequiredService<IFileStorage>();
        var id = await AsAsync(sp, org.Employee, EmployeePermissions, (s, c) => s.CreateAsync(c, Input(org), default));

        ProcurementUpload File(string name) => new(name, 4, new MemoryStream([1, 2, 3, 4]));
        var doc = await AsAsync(sp, org.Employee, EmployeePermissions, (s, c) => s.AttachAsync(c, id, File("счёт.pdf"), storage, default));
        await Assert.ThrowsAsync<FieldValidationException>(() =>
            AsAsync(sp, org.Employee, EmployeePermissions, (s, c) => s.AttachAsync(c, id, File("page.html"), storage, default)));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => AsAsync(sp, org.Other, EmployeePermissions, (s, c) => s.DownloadAsync(c, doc, default)));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => AsAsync(sp, org.Supply, SupplyPermissions, (s, c) => s.DownloadAsync(c, doc, default))); // черновик

        var file = await AsAsync(sp, org.Employee, EmployeePermissions, (s, c) => s.DownloadAsync(c, doc, default));
        Assert.Equal("application/pdf", file.ContentType);
        var detail = await AsAsync(sp, org.Employee, EmployeePermissions, (s, c) => s.GetAsync(c, id, default));
        Assert.Equal("счёт.pdf", Assert.Single(detail.Documents).FileName);
        Assert.Contains(detail.History, h => h.Action == "attachment");

        await AsAsync(sp, org.Employee, EmployeePermissions, (s, c) => s.CancelAsync(c, id, "Купили сами", default));
        await Assert.ThrowsAsync<FieldForbiddenException>(() =>
            AsAsync(sp, org.Employee, EmployeePermissions, (s, c) => s.AttachAsync(c, id, File("акт.pdf"), storage, default)));
        var cancelled = await AsAsync(sp, org.Employee, EmployeePermissions, (s, c) => s.GetAsync(c, id, default));
        Assert.All(cancelled.Items, i => Assert.Equal(ProcurementItemStatus.Cancelled, i.Status));
        Assert.Equal("Купили сами", cancelled.History[^1].Comment);
    }
}
