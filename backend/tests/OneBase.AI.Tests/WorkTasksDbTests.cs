using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Work;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Security;
using OneBase.Application.Work;
using OneBase.Domain.AI;
using OneBase.Domain.Identity;
using OneBase.Domain.Work;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// Раздел «Задачи» на настоящем PostgreSQL (ONEBASE_TEST_POSTGRES, иначе пропускается): создание и правка, назначение, статусы с
/// проверкой и возвратом, архив, права руководства, руководителя отдела, PM и сотрудника, чек-лист и процент, комментарии, вложения,
/// напоминания без повторов, предложения AI-диспетчера — только через подтверждение и без дубликатов.
/// </summary>
public class WorkTasksDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private sealed record Org(
        Department Finance, Department Supply, User Director, User Head, User Employee, User Colleague, User Outsider, User Pm, User Member);

    /// <summary>Отделы с уникальными кодами и люди: директор (право на пользователей), руководитель финансов, два сотрудника финансов, сотрудник снабжения и два человека снабжения для проекта.</summary>
    private async Task<Org> SeedAsync()
    {
        var s = Guid.NewGuid().ToString("N")[..6];
        var finance = new Department { Code = $"wt-fin-{s}", Name = $"Финансы {s}" };
        var supply = new Department { Code = $"wt-sup-{s}", Name = $"Снабжение {s}" };
        var directorRole = new Role { Name = $"Директор {s}" };
        directorRole.Permissions.Add(new RolePermission { Code = Permissions.UsersManage });
        var employeeRole = new Role { Name = $"Сотрудник {s}" };
        employeeRole.Permissions.Add(new RolePermission { Code = Permissions.FilesRead });
        User Person(string login, string name, Department? department, Role role)
        {
            var u = new User { Login = $"{login}-{s}", FullName = name, Department = department };
            u.Roles.Add(new UserRole { Role = role });
            return u;
        }

        var org = new Org(finance, supply,
            Person("wt-dir", "Директор Компании", null, directorRole),
            Person("wt-head", "Руководитель Финансов", finance, employeeRole),
            Person("wt-emp", "Бухгалтер Первый", finance, employeeRole),
            Person("wt-col", "Бухгалтер Второй", finance, employeeRole),
            Person("wt-out", "Закупщик Чужой", supply, employeeRole),
            Person("wt-pm", "Руководитель Проекта", supply, employeeRole),
            Person("wt-mem", "Участник Проекта", supply, employeeRole));
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        db.Users.AddRange(org.Director, org.Head, org.Employee, org.Colleague, org.Outsider, org.Pm, org.Member);
        db.WorkDepartmentHeads.Add(new WorkDepartmentHead { Department = finance, User = org.Head });
        await db.SaveChangesAsync();
        return org;
    }

    private static readonly IReadOnlySet<string> EmployeePermissions = new HashSet<string> { Permissions.FilesRead };
    private static readonly IReadOnlySet<string> DirectorPermissions = new HashSet<string> { Permissions.UsersManage };

    private static Task<WorkContext> Ctx(IServiceProvider sp, User user, IReadOnlySet<string>? permissions = null) =>
        sp.GetRequiredService<WorkTaskService>().ContextAsync(user.Id, permissions ?? EmployeePermissions, CancellationToken.None);

    private static async Task<List<UserNotification>> NotesAsync(IServiceProvider sp, User user, Guid taskId) =>
        await sp.GetRequiredService<OneBaseDbContext>().UserNotifications.AsNoTracking()
            .Where(n => n.RecipientId == user.Id && n.Link == WorkTaskService.LinkOf(taskId)).OrderBy(n => n.CreatedAt).ToListAsync();

    [PostgresFact]
    public async Task Task_is_created_edited_assigned_and_rescheduled_with_history_and_notifications()
    {
        var org = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tasks = sp.GetRequiredService<WorkTaskService>();
        var employee = await Ctx(sp, org.Employee);
        var today = FieldClock.Today;

        var id = await tasks.CreateAsync(employee, org.Finance.Code,
            new WorkTaskInput("  Сверка оплат дилеров  ", "Сверить оплаты за октябрь", org.Colleague.Id, WorkTaskPriority.High, today.AddDays(5), today), default);

        var detail = await tasks.GetAsync(employee, id, default);
        Assert.Equal(("Сверка оплат дилеров", WorkTaskStatus.New, org.Colleague.Id, org.Employee.Id), (detail.Task.Title, detail.Task.Status, detail.Task.AssigneeId, detail.Task.CreatedById));
        Assert.Equal((org.Finance.Code, today, 0), (detail.Task.DepartmentCode, detail.Task.StartDate, detail.Task.Progress));
        Assert.Contains(detail.History, e => e.Kind == "created");
        Assert.Equal(UserNotificationKind.TaskAssigned, Assert.Single(await NotesAsync(sp, org.Colleague, id)).Kind);

        // Исполнитель — любой действующий пользователь: сотрудник другого отдела получает задачу, уведомление (кто поставил, отдел, срок)
        // и видит её в «Моих задачах». Сотрудники отдела в списке выбора — первыми.
        var foreign = await tasks.CreateAsync(employee, org.Finance.Code, new WorkTaskInput("Сверить остатки склада", null, org.Outsider.Id, DueDate: today.AddDays(2)), default);
        var assigned = Assert.Single(await NotesAsync(sp, org.Outsider, foreign));
        Assert.Equal(UserNotificationKind.TaskAssigned, assigned.Kind);
        Assert.Equal("Новая задача: Сверить остатки склада", assigned.Title);
        Assert.Equal($"Постановщик: Бухгалтер Первый; Отдел: {org.Finance.Name}; Срок: {today.AddDays(2):dd.MM.yyyy}", assigned.Body);
        var outsiderCtx = await Ctx(sp, org.Outsider);
        Assert.Contains((await tasks.SearchAsync(outsiderCtx, new WorkTaskSearch("my"), default)).Items, t => t.Id == foreign && t.CanWork);
        var people = await tasks.AssigneesAsync(employee, org.Finance.Code, default);
        Assert.Contains(people, p => p.Id == org.Outsider.Id && !p.InDepartment && p.Department == org.Supply.Name);
        Assert.True(people.FindIndex(p => p.Id == org.Colleague.Id) < people.FindIndex(p => p.Id == org.Outsider.Id));
        var disabled = new User { Login = $"wt-off-{Guid.NewGuid():N}"[..20], FullName = "Отключённый Сотрудник", IsActive = false };
        sp.GetRequiredService<OneBaseDbContext>().Users.Add(disabled);
        await sp.GetRequiredService<OneBaseDbContext>().SaveChangesAsync();
        Assert.DoesNotContain(await tasks.AssigneesAsync(employee, org.Finance.Code, default), p => p.Id == disabled.Id);
        await Assert.ThrowsAsync<FieldValidationException>(() => tasks.CreateAsync(employee, org.Finance.Code, new WorkTaskInput("Отключённому", null, disabled.Id), default));
        // Срок — не в прошлом и не раньше начала; пустое название — ошибка.
        await Assert.ThrowsAsync<FieldValidationException>(() => tasks.CreateAsync(employee, org.Finance.Code, new WorkTaskInput("Прошлое", null, null, DueDate: today.AddDays(-1)), default));
        await Assert.ThrowsAsync<FieldValidationException>(() => tasks.CreateAsync(employee, org.Finance.Code,
            new WorkTaskInput("Срок раньше начала", null, null, DueDate: today.AddDays(1), StartDate: today.AddDays(3)), default));
        await Assert.ThrowsAsync<FieldValidationException>(() => tasks.CreateAsync(employee, org.Finance.Code, new WorkTaskInput("   ", null, null), default));
        // Чужой отдел — ставить нельзя.
        await Assert.ThrowsAsync<FieldForbiddenException>(async () => await tasks.CreateAsync(await Ctx(sp, org.Outsider), org.Finance.Code, new WorkTaskInput("Х", null, null), default));

        // Исполнитель (не постановщик) задачу не правит; постановщик — правит и переносит срок.
        var colleague = await Ctx(sp, org.Colleague);
        await Assert.ThrowsAsync<FieldForbiddenException>(() => tasks.UpdateAsync(colleague, id, new WorkTaskInput("Другое", null, org.Colleague.Id), default));
        await tasks.UpdateAsync(employee, id, new WorkTaskInput("Сверка оплат дилеров", "Сверить оплаты за октябрь", org.Colleague.Id, WorkTaskPriority.Urgent, today.AddDays(9), today), default);
        var notes = await NotesAsync(sp, org.Colleague, id);
        Assert.Equal([UserNotificationKind.TaskAssigned, UserNotificationKind.TaskDeadlineChanged], notes.Select(n => n.Kind));
        detail = await tasks.GetAsync(employee, id, default);
        Assert.Equal(WorkTaskPriority.Urgent, detail.Task.Priority);
        Assert.Contains(detail.History, e => e.Kind == "due" && e.Text.Contains(today.AddDays(9).ToString("dd.MM.yyyy")));
        Assert.Contains(detail.History, e => e.Kind == "updated" && e.Text.Contains("критический"));

        // Переназначение — новому исполнителю «новая задача».
        await tasks.UpdateAsync(employee, id, new WorkTaskInput("Сверка оплат дилеров", "Сверить оплаты за октябрь", org.Head.Id, WorkTaskPriority.Urgent, today.AddDays(9), today), default);
        Assert.Equal(UserNotificationKind.TaskAssigned, Assert.Single(await NotesAsync(sp, org.Head, id)).Kind);
        Assert.Contains((await tasks.GetAsync(employee, id, default)).History, e => e.Kind == "assigned" && e.Text.Contains("Руководитель Финансов"));
    }

    [PostgresFact]
    public async Task Assignee_sends_to_review_manager_returns_with_a_reason_then_accepts_and_archives()
    {
        var org = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tasks = sp.GetRequiredService<WorkTaskService>();
        var employee = await Ctx(sp, org.Employee);
        var colleague = await Ctx(sp, org.Colleague);
        var head = await Ctx(sp, org.Head);
        var id = await tasks.CreateAsync(employee, org.Finance.Code, new WorkTaskInput("Закрыть акты сентября", null, org.Colleague.Id), default);

        await tasks.ChangeStatusAsync(colleague, id, new WorkTaskStatusInput(WorkTaskStatus.InProgress, null), default);
        await tasks.ChangeStatusAsync(colleague, id, new WorkTaskStatusInput(WorkTaskStatus.Review, "Акты подписаны"), default);
        Assert.Equal(UserNotificationKind.TaskReview, Assert.Single(await NotesAsync(sp, org.Employee, id)).Kind);
        // Исполнитель сам результат не принимает.
        await Assert.ThrowsAsync<FieldValidationException>(() => tasks.ChangeStatusAsync(colleague, id, new WorkTaskStatusInput(WorkTaskStatus.Done, null), default));

        // Возврат на доработку — только с причиной; причина — в комментариях и уведомлении.
        await Assert.ThrowsAsync<FieldValidationException>(() => tasks.ChangeStatusAsync(head, id, new WorkTaskStatusInput(WorkTaskStatus.InProgress, null), default));
        await tasks.ChangeStatusAsync(head, id, new WorkTaskStatusInput(WorkTaskStatus.InProgress, null, "Нет акта с дилером Б"), default);
        var returned = (await NotesAsync(sp, org.Colleague, id)).Last();
        Assert.Equal((UserNotificationKind.TaskReturned, "Нет акта с дилером Б"), (returned.Kind, returned.Body));
        var detail = await tasks.GetAsync(colleague, id, default);
        Assert.Equal(WorkTaskStatus.InProgress, detail.Task.Status);
        Assert.Contains(detail.Comments, c => c.Text == "Нет акта с дилером Б" && c.AuthorId == org.Head.Id);
        Assert.Contains(detail.History, e => e.Kind == "returned");

        await tasks.ChangeStatusAsync(colleague, id, new WorkTaskStatusInput(WorkTaskStatus.Review, null), default);
        await tasks.ChangeStatusAsync(head, id, new WorkTaskStatusInput(WorkTaskStatus.Done, null), default);
        detail = await tasks.GetAsync(colleague, id, default);
        Assert.Equal((WorkTaskStatus.Done, 100), (detail.Task.Status, detail.Task.Progress));
        Assert.NotNull(detail.Task.CompletedAt);
        Assert.Equal(UserNotificationKind.TaskAccepted, (await NotesAsync(sp, org.Colleague, id)).Last().Kind);

        // Архив: не удаляет; исполнитель не архивирует; руководитель — да; из списка «все» уходит, в «архиве» есть; возвращается.
        await Assert.ThrowsAsync<FieldForbiddenException>(() => tasks.ArchiveAsync(colleague, id, true, default));
        await tasks.ArchiveAsync(head, id, true, default);
        Assert.DoesNotContain((await tasks.SearchAsync(head, new WorkTaskSearch("all", org.Finance.Code), default)).Items, t => t.Id == id);
        Assert.Contains((await tasks.SearchAsync(head, new WorkTaskSearch("archive", org.Finance.Code), default)).Items, t => t.Id == id && t.ArchivedAt != null);
        await Assert.ThrowsAsync<FieldValidationException>(() => tasks.ChangeStatusAsync(head, id, new WorkTaskStatusInput(WorkTaskStatus.InProgress, null), default));
        await tasks.ArchiveAsync(head, id, false, default);
        Assert.Null((await tasks.GetAsync(head, id, default)).Task.ArchivedAt);

        // Открытую задачу в архив не отправить.
        var open = await tasks.CreateAsync(employee, org.Finance.Code, new WorkTaskInput("Открытая", null, null), default);
        await Assert.ThrowsAsync<FieldValidationException>(() => tasks.ArchiveAsync(head, open, true, default));

        // Блокировка — с причиной, видна в строке задачи.
        await Assert.ThrowsAsync<FieldValidationException>(() => tasks.SetBlockedAsync(employee, open, true, " ", default));
        await tasks.SetBlockedAsync(employee, open, true, "Ждём выписку банка", default);
        var row = Assert.Single((await tasks.SearchAsync(head, new WorkTaskSearch("all", org.Finance.Code, Status: "blocked"), default)).Items);
        Assert.Equal((open, true, "Ждём выписку банка"), (row.Id, row.IsBlocked, row.BlockedReason));
    }

    [PostgresFact]
    public async Task Director_head_pm_and_employee_see_and_manage_only_what_their_role_allows()
    {
        var org = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tasks = sp.GetRequiredService<WorkTaskService>();
        var employee = await Ctx(sp, org.Employee);
        var head = await Ctx(sp, org.Head);
        var director = await Ctx(sp, org.Director, DirectorPermissions);
        var outsider = await Ctx(sp, org.Outsider);
        var financeTask = await tasks.CreateAsync(employee, org.Finance.Code, new WorkTaskInput("Платёжный календарь", null, org.Colleague.Id), default);
        var supplyTask = await tasks.CreateAsync(outsider, org.Supply.Code, new WorkTaskInput("Заказать упаковку", null, null), default);

        // Сотрудник снабжения задачу финансов не видит (404, не 403 — по номеру не узнать, что она есть) и в списках её нет.
        await Assert.ThrowsAsync<FieldNotFoundException>(() => tasks.GetAsync(outsider, financeTask, default));
        Assert.DoesNotContain((await tasks.SearchAsync(outsider, new WorkTaskSearch("all"), default)).Items, t => t.Id == financeTask);
        await Assert.ThrowsAsync<FieldForbiddenException>(() => tasks.SearchAsync(outsider, new WorkTaskSearch("department", org.Finance.Code), default));
        await Assert.ThrowsAsync<FieldNotFoundException>(() => tasks.ChangeStatusAsync(outsider, financeTask, new WorkTaskStatusInput(WorkTaskStatus.InProgress, null), default));

        // Директор — вся компания; руководитель финансов — свой отдел (и управляет им), но не снабжение.
        var all = (await tasks.SearchAsync(director, new WorkTaskSearch("all", PageSize: 200), default)).Items.Select(t => t.Id).ToList();
        Assert.Contains(financeTask, all);
        Assert.Contains(supplyTask, all);
        Assert.True((await tasks.GetAsync(head, financeTask, default)).Task.CanManage);
        await Assert.ThrowsAsync<FieldNotFoundException>(() => tasks.GetAsync(head, supplyTask, default));
        Assert.False((await tasks.GetAsync(employee, financeTask, default)).Task.CanWork && false);

        // Проект финансов: создаёт руководитель отдела (сотрудник — нет), PM и участник — из снабжения.
        await Assert.ThrowsAsync<FieldForbiddenException>(() => tasks.CreateProjectAsync(employee, new WorkProjectInput("Проект", null, org.Finance.Code, []), default));
        var project = await tasks.CreateProjectAsync(head, new WorkProjectInput("Автоматизация сверок", "Сверки с дилерами", org.Finance.Code,
            [new WorkProjectMemberInput(org.Pm.Id, true), new WorkProjectMemberInput(org.Member.Id, false)]), default);
        var pm = await Ctx(sp, org.Pm);
        var member = await Ctx(sp, org.Member);

        // PM ставит задачу проекта участнику из другого отдела; участник видит и ведёт её, посторонний — нет.
        var projectTask = await tasks.CreateAsync(pm, org.Finance.Code, new WorkTaskInput("Выгрузка актов", null, org.Member.Id, ProjectId: project), default);
        Assert.Equal("Автоматизация сверок", (await tasks.GetAsync(member, projectTask, default)).Task.ProjectName);
        await tasks.ChangeStatusAsync(member, projectTask, new WorkTaskStatusInput(WorkTaskStatus.Review, null), default);
        await Assert.ThrowsAsync<FieldNotFoundException>(() => tasks.GetAsync(outsider, projectTask, default));
        // PM принимает задачу проекта, но задачу отдела вне проекта не видит и не правит.
        await tasks.ChangeStatusAsync(pm, projectTask, new WorkTaskStatusInput(WorkTaskStatus.Done, null), default);
        await Assert.ThrowsAsync<FieldNotFoundException>(() => tasks.UpdateAsync(pm, financeTask, new WorkTaskInput("Х", null, null), default));
        // Исполнитель задачи проекта — тоже любой действующий пользователь; участники проекта в списке выбора — первыми.
        Assert.Contains(await tasks.AssigneesAsync(pm, org.Finance.Code, default, project), p => p.Id == org.Member.Id && p.InDepartment);

        // Руководителей отделов назначает только руководство.
        await Assert.ThrowsAsync<FieldForbiddenException>(() => tasks.SetHeadsAsync(head, org.Supply.Code, [org.Head.Id], default));
        await tasks.SetHeadsAsync(director, org.Supply.Code, [org.Pm.Id], default);
        var pmAsHead = await Ctx(sp, org.Pm);
        Assert.True((await tasks.GetAsync(pmAsHead, supplyTask, default)).Task.CanManage);
    }

    [PostgresFact]
    public async Task Checklist_sets_progress_comments_notify_participants_and_attachments_follow_task_access()
    {
        var org = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tasks = sp.GetRequiredService<WorkTaskService>();
        var storage = sp.GetRequiredService<IFileStorage>();
        var employee = await Ctx(sp, org.Employee);
        var colleague = await Ctx(sp, org.Colleague);
        var outsider = await Ctx(sp, org.Outsider);
        var id = await tasks.CreateAsync(employee, org.Finance.Code, new WorkTaskInput("Отчёт по дебиторке", null, org.Colleague.Id), default);

        await tasks.SetProgressAsync(colleague, id, 30, default);
        Assert.Equal(30, (await tasks.GetAsync(colleague, id, default)).Task.Progress);
        var first = await tasks.AddChecklistItemAsync(colleague, id, "Выгрузить оплаты", default);
        await tasks.AddChecklistItemAsync(colleague, id, "Сверить с актами", default);
        await tasks.UpdateChecklistItemAsync(colleague, id, first, null, true, default);
        var detail = await tasks.GetAsync(colleague, id, default);
        Assert.Equal((50, 1, 2), (detail.Task.Progress, detail.Task.ChecklistDone, detail.Task.ChecklistTotal));
        // С чек-листом процент вручную не ставится; посторонний чек-лист не трогает.
        await Assert.ThrowsAsync<FieldValidationException>(() => tasks.SetProgressAsync(colleague, id, 90, default));
        await Assert.ThrowsAsync<FieldNotFoundException>(() => tasks.AddChecklistItemAsync(outsider, id, "Х", default));

        await tasks.AddCommentAsync(colleague, id, "Жду выписку", default);
        Assert.Contains(await NotesAsync(sp, org.Employee, id), n => n.Kind == UserNotificationKind.TaskComment && n.Body == "Жду выписку");
        Assert.DoesNotContain(await NotesAsync(sp, org.Colleague, id), n => n.Kind == UserNotificationKind.TaskComment); // себе — нет

        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("дебиторка"));
        var file = await tasks.AttachAsync(colleague, id, new WorkUpload("C:\\fakepath\\..\\отчёт<1>.txt", "text/plain", content.Length, content), storage, default);
        var attachment = Assert.Single((await tasks.GetAsync(employee, id, default)).Attachments);
        Assert.Equal(("отчёт1.txt", "text/plain"), (attachment.FileName, attachment.ContentType));
        Assert.Equal("text/plain", (await tasks.DownloadAsync(employee, file, default)).ContentType);
        await Assert.ThrowsAsync<FieldNotFoundException>(() => tasks.DownloadAsync(outsider, file, default));
        await Assert.ThrowsAsync<FieldValidationException>(() =>
            tasks.AttachAsync(colleague, id, new WorkUpload("big.bin", null, WorkTaskService.MaxAttachmentBytes + 1, Stream.Null), storage, default));
        await tasks.DeleteAttachmentAsync(colleague, file, storage, default);
        Assert.Empty((await tasks.GetAsync(employee, id, default)).Attachments);
        Assert.Equal("файл", WorkTaskService.SafeFileName("  "));
    }

    [PostgresFact]
    public async Task Due_soon_and_overdue_are_notified_once_per_due_date_even_when_the_job_runs_again()
    {
        var org = await SeedAsync();
        var today = FieldClock.Today;
        Guid soon, late;
        using (var seed = fixture.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var a = new WorkTask { DepartmentId = org.Finance.Id, Title = "Срок завтра", AssigneeId = org.Colleague.Id, CreatedById = org.Employee.Id, DueDate = today.AddDays(1) };
            var b = new WorkTask
            {
                DepartmentId = org.Finance.Id, Title = "Просрочка", AssigneeId = org.Colleague.Id, CreatedById = org.Employee.Id, DueDate = today.AddDays(-2),
                Status = WorkTaskStatus.InProgress,
            };
            db.WorkTasks.AddRange(a, b);
            await db.SaveChangesAsync();
            (soon, late) = (a.Id, b.Id);
        }

        for (var run = 0; run < 3; run++)
        {
            using var scope = fixture.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<WorkTaskService>().RemindAsync(today, default);
        }

        using (var check = fixture.Services.CreateScope())
        {
            var sp = check.ServiceProvider;
            Assert.Equal([UserNotificationKind.TaskDueSoon], (await NotesAsync(sp, org.Colleague, soon)).Select(n => n.Kind));
            Assert.Equal([UserNotificationKind.TaskOverdue], (await NotesAsync(sp, org.Colleague, late)).Select(n => n.Kind));
            Assert.Equal([UserNotificationKind.TaskOverdue], (await NotesAsync(sp, org.Employee, late)).Select(n => n.Kind));
            Assert.Contains((await sp.GetRequiredService<WorkTaskService>().GetAsync(await Ctx(sp, org.Employee), late, default)).History, e => e.Kind == "overdue");

            // Срок перенесли, и он снова прошёл — новое уведомление о новом сроке.
            await sp.GetRequiredService<OneBaseDbContext>().WorkTasks.Where(t => t.Id == late)
                .ExecuteUpdateAsync(u => u.SetProperty(t => t.DueDate, today.AddDays(-1)));
            await sp.GetRequiredService<WorkTaskService>().RemindAsync(today, default);
            Assert.Equal(2, (await NotesAsync(sp, org.Colleague, late)).Count(n => n.Kind == UserNotificationKind.TaskOverdue));
        }
    }

    [PostgresFact]
    public async Task Ai_dispatcher_proposes_from_real_data_only_after_confirmation_creates_tasks_and_never_duplicates()
    {
        var org = await SeedAsync();
        var key = Guid.NewGuid().ToString("N")[..8];
        using (var seed = fixture.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            db.AiAlerts.AddRange(
                new AiAlert
                {
                    Category = org.Finance.Code, Rule = "finance.test", Key = $"finance.test.{key}", Severity = AiAlertSeverity.Critical,
                    Title = "Оплаты дилеров упали на 12%", Message = "За октябрь поступило 880 млн сумов против 1 000 млн в сентябре.", Href = "/sales/payments",
                },
                // Находка с правом, которого у руководителя нет, — не предлагается и не показывается.
                new AiAlert
                {
                    Category = org.Finance.Code, Rule = "finance.secret", Key = $"finance.secret.{key}", Severity = AiAlertSeverity.Warning,
                    Title = "Скрытая находка", Message = "Данные HR.", RequiredPermission = Permissions.HrRead,
                });
            db.WorkTasks.Add(new WorkTask
            {
                DepartmentId = org.Finance.Id, Title = "Акт сверки с дилером", AssigneeId = org.Colleague.Id, DueDate = FieldClock.Today.AddDays(-4),
            });
            await db.SaveChangesAsync();
        }

        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var service = sp.GetRequiredService<TaskSuggestionService>();
        var tasks = sp.GetRequiredService<WorkTaskService>();
        var head = await Ctx(sp, org.Head);
        var employee = await Ctx(sp, org.Employee);

        // Сотрудник (не руководитель) предложений не получает и не видит.
        await Assert.ThrowsAsync<FieldForbiddenException>(() => service.GenerateAsync(employee, default));
        Assert.Empty(await service.ListAsync(employee, null, default));

        // Модель: первое — по фактам; второе — с придуманным числом (35) — заменяется текстом по правилу.
        var calls = fixture.Gateway.Calls.Count;
        fixture.Gateway.Script.Enqueue(_ => $$"""
            {"suggestions":[
              {"id":"c1","title":"Выяснить причину падения оплат на 12%","problem":"Поступления упали с 1 000 млн до 880 млн сумов.",
               "rationale":"Оплаты за октябрь — 880 млн сумов против 1 000 млн в сентябре.","assigneeId":"{{org.Colleague.Id}}","priority":"High","dueInDays":5,"insufficientData":false},
              {"id":"c2","title":"Разобрать 35 просроченных задач","problem":"Просрочено 35 задач.","rationale":"35 задач.","assigneeId":"{{org.Outsider.Id}}","priority":"Low","dueInDays":90}]}
            """);
        var run = await service.GenerateAsync(head, default);
        Assert.Equal((2, 2, true), (run.Candidates, run.Created, run.UsedModel));
        var (_, options) = fixture.Gateway.Calls[calls];
        Assert.Equal(("work.suggestions", true), (options.Context.Purpose, options.JsonOutput));
        Assert.DoesNotContain("Скрытая находка", fixture.Gateway.Calls[calls].Messages[1].Content);

        var list = await service.ListAsync(head, WorkSuggestionStatus.Pending, default);
        var alert = Assert.Single(list, s => s.SourceKind == "alert");
        Assert.Equal(("Выяснить причину падения оплат на 12%", org.Colleague.Id, WorkTaskPriority.High, FieldClock.Today.AddDays(5), "/sales/payments", "fake/mapper"),
            (alert.Title, alert.AssigneeId, alert.Priority, alert.DueDate, alert.SourceHref, alert.Model));
        var overdue = Assert.Single(list, s => s.SourceKind == "overdue");
        Assert.Null(overdue.Model); // формулировка модели не прошла проверку по фактам — текст по правилу
        Assert.DoesNotContain("35", overdue.Title);
        Assert.Null(overdue.AssigneeId);
        Assert.Null(overdue.DueDate);
        Assert.Contains("Акт сверки с дилером", overdue.Facts);
        Assert.DoesNotContain(list, s => s.Title.Contains("Скрытая"));

        // Повторный запуск — ничего нового (без вызова модели).
        var before = fixture.Gateway.Calls.Count;
        var again = await service.GenerateAsync(head, default);
        Assert.Equal((0, 2), (again.Created, again.Duplicates));
        Assert.Equal(before, fixture.Gateway.Calls.Count);

        // До подтверждения задачи нет; подтверждение (с правкой) создаёт одну задачу; второе подтверждение — ошибка.
        Assert.False(await sp.GetRequiredService<OneBaseDbContext>().WorkTasks.AnyAsync(t => t.SuggestionId == alert.Id));
        var taskId = await service.AcceptAsync(head, alert.Id, new TaskSuggestionEdit(Priority: WorkTaskPriority.Urgent), default);
        var created = await tasks.GetAsync(head, taskId, default);
        Assert.Equal(("Выяснить причину падения оплат на 12%", org.Colleague.Id, WorkTaskPriority.Urgent, org.Head.Id),
            (created.Task.Title, created.Task.AssigneeId, created.Task.Priority, created.Task.CreatedById));
        Assert.Contains("880 млн", created.Task.Description);
        Assert.Equal((WorkSuggestionStatus.Accepted, taskId), (await service.ListAsync(head, null, default)).Where(s => s.Id == alert.Id).Select(s => (s.Status, s.CreatedTaskId!.Value)).Single());
        await Assert.ThrowsAsync<FieldValidationException>(() => service.AcceptAsync(head, alert.Id, null, default));
        // Посторонний (сотрудник снабжения) чужое предложение не видит и не решает.
        await Assert.ThrowsAsync<FieldNotFoundException>(async () => await service.RejectAsync(await Ctx(sp, org.Outsider), overdue.Id, null, default));

        // Отклонение — с причиной; отклонённое не возвращается при новом запуске.
        await service.RejectAsync(head, overdue.Id, "Разберём на планёрке", default);
        Assert.Equal(WorkSuggestionStatus.Rejected, (await service.ListAsync(head, null, default)).Single(s => s.Id == overdue.Id).Status);
        Assert.Equal(0, (await service.GenerateAsync(head, default)).Created);
        Assert.Equal(1, await sp.GetRequiredService<OneBaseDbContext>().WorkTasks.CountAsync(t => t.SuggestionId != null && t.DepartmentId == org.Finance.Id));
    }

    [PostgresFact]
    public async Task Ai_dispatcher_without_a_model_proposes_by_rule_from_the_facts()
    {
        var org = await SeedAsync();
        using (var seed = fixture.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            db.WorkTasks.Add(new WorkTask { DepartmentId = org.Finance.Id, Title = "Платёжка поставщику", DueDate = FieldClock.Today.AddDays(-1) });
            await db.SaveChangesAsync();
        }

        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var service = sp.GetRequiredService<TaskSuggestionService>();
        fixture.Gateway.Script.Clear(); // шлюз без ответов — «ИИ не настроен»
        var run = await service.GenerateAsync(await Ctx(sp, org.Head), default);

        Assert.Equal((1, false), (run.Created, run.UsedModel));
        Assert.Contains("без модели", run.Note);
        var s = Assert.Single(await service.ListAsync(await Ctx(sp, org.Head), WorkSuggestionStatus.Pending, default));
        Assert.Equal(($"Разобрать просроченные задачи отдела «{org.Finance.Name}»", null, null), (s.Title, s.AssigneeId, s.DueDate));
        Assert.Contains("Платёжка поставщику", s.Rationale);
    }
}
