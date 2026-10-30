using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Field;
using OneBase.Application.Security;
using OneBase.Application.Work;
using OneBase.Domain.Identity;
using OneBase.Domain.Work;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>Полоса этапов карточки задачи: когда задача последний раз пришла в каждый статус и кто её перевёл (по истории).</summary>
public class WorkTaskStagesTests
{
    private static WorkEventView E(string kind, string text, int minute, string actor = "Admin Admin") =>
        new(Guid.NewGuid(), kind, text, actor, new DateTimeOffset(2026, 10, 29, 16, 0, 0, TimeSpan.Zero).AddMinutes(minute));

    [Fact]
    public void Stages_take_the_latest_entry_into_each_status()
    {
        // Как у задачи из макета: создана, выполнена, возвращена в работу, снова отправлена на проверку (история — новые сверху).
        var history = new[]
        {
            E("review", "Отправлена на проверку", 40, "Исполнитель"),
            E("status", "Возвращена в работу", 30),
            E("checklist", "Пункт чек-листа: выписка", 25),
            E("accepted", "Задача выполнена", 20),
            E("status", "Статус: новая → в работе", 10),
            E("created", "Задача создана", 0),
        };

        var stages = WorkTaskService.StagesOf(history);

        Assert.Equal([WorkTaskStatus.New, WorkTaskStatus.Done, WorkTaskStatus.InProgress, WorkTaskStatus.Review], stages.Select(s => s.Status));
        Assert.Equal(30, stages.Single(s => s.Status == WorkTaskStatus.InProgress).At.Minute);
        Assert.Equal("Исполнитель", stages.Single(s => s.Status == WorkTaskStatus.Review).ActorName);
    }

    [Fact]
    public void Returned_and_cancelled_tasks_have_their_stages()
    {
        var stages = WorkTaskService.StagesOf([
            E("status", "Задача отменена: дубль", 50),
            E("returned", "Возвращена на доработку: нет выписки", 30),
            E("status", "Статус: в работе → на проверке", 20),
            E("created", "Задача создана", 0),
        ]);

        Assert.Equal([WorkTaskStatus.New, WorkTaskStatus.Review, WorkTaskStatus.InProgress, WorkTaskStatus.Cancelled], stages.Select(s => s.Status));
    }
}

/// <summary>
/// Канбан и карточка задачи на настоящем PostgreSQL (ONEBASE_TEST_POSTGRES, иначе пропускается): фильтр по нескольким отделам и
/// исполнителям, @-упоминания в комментариях только для тех, кто видит задачу, порядок пунктов чек-листа, этапы в карточке.
/// </summary>
public class WorkTaskBoardDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private sealed record Org(Department Finance, Department Supply, User Director, User Head, User Employee, User Colleague, User Outsider);

    private async Task<Org> SeedAsync()
    {
        var s = Guid.NewGuid().ToString("N")[..6];
        var finance = new Department { Code = $"wb-fin-{s}", Name = $"Финансы {s}" };
        var supply = new Department { Code = $"wb-sup-{s}", Name = $"Снабжение {s}" };
        var directorRole = new Role { Name = $"Директор {s}" };
        directorRole.Permissions.Add(new RolePermission { Code = Permissions.UsersManage });
        directorRole.Permissions.Add(new RolePermission { Code = Permissions.WorkTasks });
        var employeeRole = new Role { Name = $"Сотрудник {s}" };
        employeeRole.Permissions.Add(new RolePermission { Code = Permissions.WorkTasks });
        User Person(string login, string name, Department? department, Role role)
        {
            var u = new User { Login = $"{login}-{s}", FullName = name, Department = department };
            u.Roles.Add(new UserRole { Role = role });
            return u;
        }

        var org = new Org(finance, supply,
            Person("wb-dir", "Директор Компании", null, directorRole),
            Person("wb-head", "Руководитель Финансов", finance, employeeRole),
            Person("wb-emp", "Бухгалтер Первый", finance, employeeRole),
            Person("wb-col", "Бухгалтер Второй", finance, employeeRole),
            Person("wb-out", "Закупщик Чужой", supply, employeeRole));
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        db.Users.AddRange(org.Director, org.Head, org.Employee, org.Colleague, org.Outsider);
        db.WorkDepartmentHeads.Add(new WorkDepartmentHead { Department = finance, User = org.Head });
        await db.SaveChangesAsync();
        return org;
    }

    private static Task<WorkContext> Ctx(IServiceProvider sp, User user, bool director = false) =>
        sp.GetRequiredService<WorkTaskService>().ContextAsync(user.Id,
            director ? new HashSet<string> { Permissions.UsersManage } : new HashSet<string>(), CancellationToken.None);

    private static async Task<List<UserNotification>> NotesAsync(IServiceProvider sp, User user, Guid taskId) =>
        await sp.GetRequiredService<OneBaseDbContext>().UserNotifications.AsNoTracking()
            .Where(n => n.RecipientId == user.Id && n.Link == WorkTaskService.LinkOf(taskId)).OrderBy(n => n.CreatedAt).ToListAsync();

    [PostgresFact]
    public async Task Board_filters_by_several_departments_and_assignees()
    {
        var org = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tasks = sp.GetRequiredService<WorkTaskService>();
        var director = await Ctx(sp, org.Director, director: true);

        var finance = await tasks.CreateAsync(director, org.Finance.Code, new WorkTaskInput("Сверка", null, org.Employee.Id), default);
        var finance2 = await tasks.CreateAsync(director, org.Finance.Code, new WorkTaskInput("Отчёт", null, org.Colleague.Id), default);
        var supply = await tasks.CreateAsync(director, org.Supply.Code, new WorkTaskInput("Закупка", null, org.Outsider.Id), default);

        var both = await tasks.SearchAsync(director, new WorkTaskSearch("board", $"{org.Finance.Code},{org.Supply.Code}", PageSize: 500), default);
        Assert.Equal(new[] { finance, finance2, supply }.Order(), both.Items.Select(t => t.Id).Order());

        var people = await tasks.SearchAsync(director, new WorkTaskSearch("board", $"{org.Finance.Code},{org.Supply.Code}", AssigneeIds: [org.Employee.Id, org.Outsider.Id]), default);
        Assert.Equal(new[] { finance, supply }.Order(), people.Items.Select(t => t.Id).Order());

        // Отдел без доступа в списке — отказ, как и для одного отдела.
        var employee = await Ctx(sp, org.Employee);
        await Assert.ThrowsAsync<FieldForbiddenException>(() => tasks.SearchAsync(employee, new WorkTaskSearch("board", $"{org.Finance.Code},{org.Supply.Code}"), default));
    }

    [PostgresFact]
    public async Task Mentions_notify_only_people_who_see_the_task_and_replace_the_comment_notice()
    {
        var org = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tasks = sp.GetRequiredService<WorkTaskService>();
        var employee = await Ctx(sp, org.Employee);
        var id = await tasks.CreateAsync(employee, org.Finance.Code, new WorkTaskInput("Сверка оплат", null, org.Colleague.Id), default);

        // Подсказка после @: отдел задачи, его руководитель и руководство; чужой отдел — нет, сам автор — нет.
        var people = (await tasks.PeopleAsync(employee, id, default)).Select(p => p.Id).ToList();
        Assert.Contains(org.Colleague.Id, people);
        Assert.Contains(org.Head.Id, people);
        Assert.Contains(org.Director.Id, people);
        Assert.DoesNotContain(org.Outsider.Id, people);
        Assert.DoesNotContain(org.Employee.Id, people);

        await tasks.AddCommentAsync(employee, id, "@Бухгалтер Второй @Руководитель Финансов @Закупщик Чужой посмотрите", default,
            [org.Colleague.Id, org.Head.Id, org.Outsider.Id, org.Employee.Id]);

        // Исполнитель упомянут — одно уведомление «Упоминание» (без второго «Комментарий»); руководитель — тоже; чужому и себе — ничего.
        var colleague = await NotesAsync(sp, org.Colleague, id);
        Assert.Equal([UserNotificationKind.TaskAssigned, UserNotificationKind.TaskMention], colleague.Select(n => n.Kind));
        Assert.Equal("Упоминание: Сверка оплат", colleague[^1].Title);
        Assert.StartsWith("Бухгалтер Первый: @Бухгалтер Второй", colleague[^1].Body);
        Assert.Equal(UserNotificationKind.TaskMention, Assert.Single(await NotesAsync(sp, org.Head, id)).Kind);
        Assert.Empty(await NotesAsync(sp, org.Outsider, id));
        Assert.Empty(await NotesAsync(sp, org.Employee, id));
        Assert.Equal(("mention", "tasks"), NotificationCatalog.Of(UserNotificationKind.TaskMention));

        // Без упоминаний — как раньше: исполнителю «Комментарий».
        await tasks.AddCommentAsync(employee, id, "Жду выписку", default);
        Assert.Equal(UserNotificationKind.TaskComment, (await NotesAsync(sp, org.Colleague, id))[^1].Kind);
    }

    [PostgresFact]
    public async Task Checklist_order_is_saved_and_stages_come_with_the_card()
    {
        var org = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tasks = sp.GetRequiredService<WorkTaskService>();
        var employee = await Ctx(sp, org.Employee);
        var colleague = await Ctx(sp, org.Colleague);
        var id = await tasks.CreateAsync(employee, org.Finance.Code, new WorkTaskInput("Закрытие месяца", null, org.Colleague.Id, DueDate: FieldClock.Today.AddDays(3)), default);
        var a = await tasks.AddChecklistItemAsync(colleague, id, "Выписка", default);
        var b = await tasks.AddChecklistItemAsync(colleague, id, "Акт сверки", default);
        var c = await tasks.AddChecklistItemAsync(colleague, id, "Отчёт", default);

        await tasks.OrderChecklistAsync(colleague, id, [c, a, b], default);
        Assert.Equal([c, a, b], (await tasks.GetAsync(colleague, id, default)).Checklist.Select(i => i.Id));
        await Assert.ThrowsAsync<FieldValidationException>(() => tasks.OrderChecklistAsync(colleague, id, [c, a], default));
        var outsider = await Ctx(sp, org.Outsider);
        await Assert.ThrowsAsync<FieldNotFoundException>(() => tasks.OrderChecklistAsync(outsider, id, [a, b, c], default));

        await tasks.ChangeStatusAsync(colleague, id, new WorkTaskStatusInput(WorkTaskStatus.InProgress, null), default);
        await tasks.ChangeStatusAsync(colleague, id, new WorkTaskStatusInput(WorkTaskStatus.Review, null), default);
        var detail = await tasks.GetAsync(employee, id, default);
        Assert.Equal([WorkTaskStatus.New, WorkTaskStatus.InProgress, WorkTaskStatus.Review], detail.Stages.Select(s => s.Status));
        Assert.Equal("Бухгалтер Второй", detail.Stages[^1].ActorName);
    }
}
