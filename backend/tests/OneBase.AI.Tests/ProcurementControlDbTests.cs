using System.IO.Compression;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Procurement;
using OneBase.Application.Reports;
using OneBase.Application.Security;
using OneBase.Domain.Identity;
using OneBase.Domain.Procurement;
using OneBase.Domain.Work;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// Контроль снабжения на настоящем PostgreSQL (ONEBASE_TEST_POSTGRES, иначе пропускается): обзор по ролям и KPI из данных одной закупки
/// (экономия, поставка в срок, выполненная заявка, претензия), права на обзор, отчёты и файлы (Excel, PDF, Word, акт приёмки), ежедневная
/// сводка — одна на день и в журнале, эскалация просроченной задачи снабжения — руководителю, потом директору, без повторов.
/// </summary>
public class ProcurementControlDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly string[] Employee = [Permissions.ProcurementRequest];
    private static readonly string[] Supply = [Permissions.ProcurementRequest, Permissions.ProcurementRead, Permissions.ProcurementManage];
    private static readonly string[] Director = [Permissions.ProcurementRequest, Permissions.ProcurementRead, Permissions.ProcurementSettings];
    private static readonly string[] Keeper = [Permissions.ProcurementRequest, Permissions.ProcurementWarehouse];

    private sealed record Org(User Employee, User Other, User Supply, User Director, User Keeper, User SupplyHead, Guid Category, Guid Department);

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

        var dept = new Department { Code = $"ct-{s}", Name = $"Маркетинг {s}" };
        var directorRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == SystemRoles.Director) ?? new Role { Name = SystemRoles.Director };
        Role WithRights(string name, string code)
        {
            var role = new Role { Name = name };
            role.Permissions.Add(new RolePermission { Code = code });
            return role;
        }

        var supplyRole = WithRights($"Снабжение {s}", Permissions.ProcurementManage);
        var keeperRole = WithRights($"Кладовщик {s}", Permissions.ProcurementWarehouse);
        User Person(string login, string name, Department department, Role? role = null)
        {
            var u = new User { Login = $"{login}-{s}", FullName = name, Department = department };
            if (role is not null)
            {
                u.Roles.Add(new UserRole { Role = role });
            }

            return u;
        }

        var org = new Org(Person("ct-emp", "Маркетолог", dept), Person("ct-oth", "Чужой сотрудник", supplyDept), Person("ct-sup", "Снабженец", supplyDept, supplyRole),
            Person("ct-dir", "Директор", dept, directorRole), Person("ct-kpr", "Кладовщик", supplyDept, keeperRole), Person("ct-head", "Руководитель снабжения", supplyDept, supplyRole),
            Guid.Empty, dept.Id);
        db.Users.AddRange(org.Employee, org.Other, org.Supply, org.Director, org.Keeper, org.SupplyHead);
        db.WorkDepartmentHeads.Add(new WorkDepartmentHead { Department = supplyDept, UserId = org.SupplyHead.Id });
        await db.SaveChangesAsync();
        return org with { Category = await db.ProcurementCategories.Where(c => c.Code == "stationery").Select(c => c.Id).SingleAsync(), Department = dept.Id };
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

    private static T Get<T>(IServiceProvider sp) where T : notnull => sp.GetRequiredService<T>();

    /// <summary>
    /// Полная закупка: заявка 100 пачек по 45 000 → директор утвердил → заказ по 44 000 (цены с НДС) → заказан → принято 100 (+2 брака:
    /// претензия) → выдано инициатору (заявка выполнена). Экономия — 100 × 1 000 = 100 000.
    /// </summary>
    private async Task<(Guid Request, Guid Order, Guid Receipt, Guid Supplier)> PurchaseAsync(Org org)
    {
        var request = await As(org.Employee, Employee, (sp, c) => Get<ProcurementRequestService>(sp).CreateAsync(c, new ProcurementRequestInput("Бумага для буклетов", null,
            ProcurementPriority.High, FieldClock.Today.AddDays(10), "Выставка", "Нужна белая", [new(null, "Бумага А4 | 80 г", org.Category, "пачка", 100, 45_000)], Submit: true), default));
        await As(org.Supply, Supply, (sp, c) => Get<ProcurementRequestService>(sp).ForwardAsync(c, request, default));
        await As(org.Director, Director, (sp, c) => Get<ProcurementRequestService>(sp).ApproveAsync(c, request, "Согласовано", default));
        var item = (await As(org.Supply, Supply, (sp, c) => Get<ProcurementRequestService>(sp).GetAsync(c, request, default))).Items[0].Id;
        var supplier = await As(org.Supply, Supply, (sp, c) => Get<ProcurementSupplierService>(sp).CreateAsync(c,
            new ProcurementSupplierInput($"ООО Контроль {Guid.NewGuid():N}"[..20], null, null, null, null, null, null, null, null, null, 2, null, [org.Category]), default));
        var order = await As(org.Supply, Supply, (sp, c) => Get<ProcurementOrderService>(sp).CreateAsync(c, new ProcurementOrderInput(supplier, null, org.Supply.Id,
            FieldClock.Today.AddDays(3), null, 0, 12, true, 0, null, [new ProcurementOrderLineInput(item, 100, 44_000)], Submit: true), default));
        await As(org.Supply, Supply, (sp, c) => Get<ProcurementOrderService>(sp).MarkOrderedAsync(c, order, null, default));
        var orderItem = (await As(org.Supply, Supply, (sp, c) => Get<ProcurementOrderService>(sp).GetAsync(c, order, default))).Items[0].Id;
        var card = await As(org.Keeper, Keeper, (sp, c) => Get<ProcurementStockService>(sp).CreateItemAsync(c,
            new ProcurementCatalogInput($"CT-{Guid.NewGuid():N}"[..12], "Бумага А4 80 г", org.Category, "пачка", null, null, 0), default));
        var receipt = await As(org.Keeper, Keeper, (sp, c) => Get<ProcurementReceiptService>(sp).CreateAsync(c,
            new ProcurementReceiptInput(order, "НАКЛ-1", null, [new(orderItem, 102, Defective: 2, ItemId: card)], Post: true), default));
        await As(org.Keeper, Keeper, (sp, c) => Get<ProcurementRequestService>(sp).IssueAsync(c, request, new([new(item, 100)], "Маркетолог", null), default));
        return (request, order, receipt, supplier);
    }

    [PostgresFact]
    public async Task Dashboard_and_kpis_come_from_real_purchases_and_follow_roles()
    {
        var org = await SeedAsync();
        var (request, order, _, supplier) = await PurchaseAsync(org);
        var f = ProcurementFilter.Of(FieldClock.Today.AddDays(-1), FieldClock.Today, department: org.Department);

        var kpis = await As(org.Director, Director, (sp, _) => Get<ProcurementAnalyticsService>(sp).KpisAsync(f, default));
        Assert.Equal((100_000m, 1, 1, 1), (kpis.Savings, kpis.SavingsLines, kpis.FulfilledRequests, kpis.Claims));
        Assert.Equal(100m, kpis.OnTimePercent);
        Assert.Equal(1, kpis.PurchasesMeasured);
        Assert.True(kpis.AvgPurchaseDays >= 0);
        Assert.Equal(Math.Round(100m * 100_000 / 4_500_000, 1), kpis.SavingsPercent);

        var director = await As(org.Director, Director, (sp, c) => Get<ProcurementAnalyticsService>(sp).DashboardAsync(c, f, default));
        Assert.NotNull(director.Kpis);
        Assert.NotNull(director.Head);
        Assert.Null(director.Buyer);
        Assert.Null(director.Warehouse);
        var board = director.Director!;
        Assert.Contains(board.ByDepartment, d => d.Key == org.Department.ToString() && d.Amount == 4_400_000m);
        Assert.Contains(board.ByCategory, c => c.Key == org.Category.ToString() && c.Amount >= 4_400_000m);
        Assert.Equal(12, board.ByMonth.Count);
        Assert.True(board.ReceivedTotal >= 4_400_000m);
        Assert.Contains(board.Best.Concat(board.Worst), s => s.Id == supplier);
        Assert.NotNull(director.Digests);

        var buyer = await As(org.Supply, Supply, (sp, c) => Get<ProcurementAnalyticsService>(sp).DashboardAsync(c, f, default));
        Assert.Null(buyer.Director);
        Assert.Null(buyer.Head); // снабженец без руководства отдела
        Assert.NotNull(buyer.Buyer);

        var head = await As(org.SupplyHead, Supply, (sp, c) => Get<ProcurementAnalyticsService>(sp).DashboardAsync(c, f, default));
        var staff = Assert.Single(head.Head!.Staff, s => s.UserId == org.Supply.Id);
        Assert.Equal(1, staff.Delivered);
        Assert.NotNull(head.Digests);

        var keeper = await As(org.Keeper, Keeper, (sp, c) => Get<ProcurementAnalyticsService>(sp).DashboardAsync(c, f, default));
        Assert.NotNull(keeper.Warehouse);
        Assert.Null(keeper.Kpis);
        Assert.Contains(keeper.Warehouse!.Recent, r => r.OrderId == order);
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Employee, Employee, (sp, c) => Get<ProcurementAnalyticsService>(sp).DashboardAsync(c, f, default)));
        _ = request;
    }

    [PostgresFact]
    public async Task Reports_export_to_excel_and_pdf_and_requests_download_as_word()
    {
        var org = await SeedAsync();
        var (request, _, receipt, supplier) = await PurchaseAsync(org);
        var f = ProcurementFilter.Of(FieldClock.Today.AddDays(-1), FieldClock.Today, supplier: supplier);
        foreach (var kind in ProcurementReportService.Kinds.Select(k => k.Key))
        {
            var report = await As(org.Director, Director, (sp, c) => Get<ProcurementReportService>(sp).BuildAsync(c, kind, f, default));
            Assert.NotEmpty(report.Sheets);
            using var scope = fixture.Services.CreateScope();
            Assert.StartsWith("PK", Encoding.ASCII.GetString(scope.ServiceProvider.GetRequiredService<IReportWorkbookWriter>().Write(report, DateTimeOffset.UtcNow)[..2]));
            Assert.StartsWith("%PDF", Encoding.ASCII.GetString(scope.ServiceProvider.GetRequiredService<IReportPdfWriter>().Write(report, DateTimeOffset.UtcNow)[..4]));
        }

        var lines = await As(org.Supply, Supply, (sp, c) => Get<ProcurementReportService>(sp).BuildAsync(c, "lines", f, default));
        var row = Assert.Single(lines.Sheets[0].Rows);
        Assert.Equal(100_000m, row[^1].GetDecimal()); // экономия по позиции
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Keeper, Keeper, (sp, c) => Get<ProcurementReportService>(sp).BuildAsync(c, "orders", f, default)));
        await Assert.ThrowsAsync<FieldNotFoundException>(() => As(org.Director, Director, (sp, c) => Get<ProcurementReportService>(sp).BuildAsync(c, "salaries", f, default)));

        // Word: инициатор скачивает свою заявку; в документе — номер, позиция (вертикальная черта не ломает таблицу), причина, решение директора.
        var file = await As(org.Employee, Employee, (sp, c) => Get<ProcurementRequestService>(sp).DocumentAsync(c, request, Get<IDocumentWriter>(sp), default));
        Assert.EndsWith(".docx", file.FileName);
        using (var zip = new ZipArchive(new MemoryStream(file.Content)))
        {
            using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
            var xml = await reader.ReadToEndAsync();
            Assert.Contains(file.FileName[..^5], xml);
            Assert.Contains("Бумага А4 / 80 г", xml);
            Assert.Contains("Выставка", xml);
            Assert.Contains("Согласовано", xml);
        }

        await Assert.ThrowsAsync<FieldForbiddenException>(() =>
            As(org.Other, Employee, (sp, c) => Get<ProcurementRequestService>(sp).DocumentAsync(c, request, Get<IDocumentWriter>(sp), default)));

        var act = await As(org.Director, Director, (sp, c) => Get<ProcurementReceiptService>(sp).ActAsync(c, receipt, default));
        Assert.Equal(102m, act.Sheets[0].Rows[0][5].GetDecimal()); // пришло
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(fixture.Services.GetRequiredService<IReportPdfWriter>().Write(act, DateTimeOffset.UtcNow)[..4]));

        // Узбекский кириллицей (кнопка «ЎЗ»): подписи отчёта, акта и Word — кириллицей, данные (названия, номера) — как есть.
        UiLanguage.Set("uz-Cyrl");
        try
        {
            var requests = await As(org.Director, Director, (sp, c) => Get<ProcurementReportService>(sp).BuildAsync(c, "requests", f, default));
            Assert.Contains(requests.Sheets[0].Columns, c => c.Header == "Ҳолат");
            Assert.StartsWith("Қабул далолатномаси", (await As(org.Director, Director, (sp, c) => Get<ProcurementReceiptService>(sp).ActAsync(c, receipt, default))).Title);
            var cyrillic = await As(org.Employee, Employee, (sp, c) => Get<ProcurementRequestService>(sp).DocumentAsync(c, request, Get<IDocumentWriter>(sp), default));
            using var zip = new ZipArchive(new MemoryStream(cyrillic.Content));
            using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
            var xml = await reader.ReadToEndAsync();
            Assert.Contains("Харид аризаси", xml);
            Assert.Contains("Бумага А4 / 80 г", xml);
            Assert.DoesNotContain("Xarid arizasi", xml);
        }
        finally
        {
            UiLanguage.Set("ru");
        }
    }

    [PostgresFact]
    public async Task Daily_digest_is_stored_once_and_overdue_supply_tasks_escalate_once_per_level()
    {
        var org = await SeedAsync();
        var today = FieldClock.Today;
        var waiting = await As(org.Employee, Employee, (sp, c) => Get<ProcurementRequestService>(sp).CreateAsync(c, new ProcurementRequestInput("Ждёт директора", null,
            ProcurementPriority.Normal, null, "Нужно", null, [new(null, "Маркеры", org.Category, "шт", 10, 15_000)], Submit: true), default));
        await As(org.Supply, Supply, (sp, c) => Get<ProcurementRequestService>(sp).ForwardAsync(c, waiting, default));
        Guid task;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var supplyDept = await db.Departments.SingleAsync(d => d.Code == "supply");
            var t = new WorkTask { DepartmentId = supplyDept.Id, Title = "Сверить счёт поставщика", AssigneeId = org.Supply.Id, CreatedById = org.SupplyHead.Id, DueDate = today.AddDays(-1) };
            db.WorkTasks.Add(t);
            await db.SaveChangesAsync();
            task = t.Id;
        }

        using (var scope = fixture.Services.CreateScope())
        {
            var reminders = scope.ServiceProvider.GetRequiredService<ProcurementReminderService>();
            Assert.True(await reminders.DigestAsync(today, default));
            Assert.False(await reminders.DigestAsync(today, default));
            Assert.Equal(1, await reminders.EscalateAsync(today, default)); // 1 день — руководителю
            Assert.Equal(0, await reminders.EscalateAsync(today, default));
        }

        var digests = await As(org.Director, Director, (sp, c) => Get<ProcurementAnalyticsService>(sp).DigestPageAsync(c, 1, default));
        var row = Assert.Single(digests.Items, d => d.Day == today);
        var digest = await As(org.SupplyHead, Supply, (sp, c) => Get<ProcurementAnalyticsService>(sp).DigestAsync(c, row.Id, default));
        Assert.Contains(digest.Sections[0].Items, i => i.Link == $"/requests/{waiting}");
        Assert.Contains(digest.Sections, s => s.Title == "Просроченные задачи снабжения" && s.Items.Any(i => i.Link == $"/tasks/task/{task}"));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Supply, Supply, (sp, c) => Get<ProcurementAnalyticsService>(sp).DigestPageAsync(c, 1, default)));

        // Ещё 2 дня просрочки (всего 3) — директору; срок перенесли — эскалация начнётся заново.
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            await db.WorkTasks.Where(t => t.Id == task).ExecuteUpdateAsync(x => x.SetProperty(t => t.DueDate, today.AddDays(-3)));
            var reminders = scope.ServiceProvider.GetRequiredService<ProcurementReminderService>();
            Assert.Equal(2, await reminders.EscalateAsync(today, default)); // новый срок: руководителю и директору
            Assert.Equal(0, await reminders.EscalateAsync(today, default));
            Assert.Equal(3, await db.ProcurementEscalations.CountAsync(e => e.TaskId == task));
            Assert.Equal(3, await db.WorkTaskEvents.CountAsync(e => e.TaskId == task && e.Kind == "escalated"));
            var titles = await db.UserNotifications.Where(n => n.Link == $"/tasks/task/{task}").Select(n => new { n.RecipientId, n.Title }).ToListAsync();
            Assert.Equal(2, titles.Count(n => n.RecipientId == org.SupplyHead.Id));
            Assert.Single(titles, n => n.RecipientId == org.Director.Id);
            Assert.DoesNotContain(titles, n => n.RecipientId == org.Supply.Id); // исполнителю напоминают «Задачи»
            Assert.True(await db.UserNotifications.AnyAsync(n => n.RecipientId == org.Director.Id && n.Link == $"/procurement/digests/{row.Id}"));
        }
    }
}
