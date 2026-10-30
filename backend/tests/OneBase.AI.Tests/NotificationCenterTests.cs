using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Field;
using OneBase.Application.Security;
using OneBase.Application.Work;
using OneBase.Domain.Identity;
using OneBase.Domain.Work;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>Каталог видов уведомлений: у каждого вида есть значок-тип и раздел, разделы не пересекаются, важность — по смыслу.</summary>
public class NotificationCatalogTests
{
    [Fact]
    public void Every_kind_has_a_type_and_one_category()
    {
        string[] types = ["task_overdue", "task_due", "task_done", "task_comment", "task_assigned", "mention", "ai_insight", "ai_budget", "sales_alert", "system"];
        foreach (var kind in Enum.GetValues<UserNotificationKind>())
        {
            var (type, category) = NotificationCatalog.Of(kind);
            Assert.Contains(type, types);
            Assert.Contains(category, NotificationCatalog.Categories);
        }

        var all = NotificationCatalog.Categories.SelectMany(NotificationCatalog.KindsOf).ToList();
        Assert.Equal(Enum.GetValues<UserNotificationKind>().Length, all.Count);
        Assert.Equal(all.Count, all.Distinct().Count());
        Assert.Equal(("task_overdue", "tasks"), NotificationCatalog.Of(UserNotificationKind.TaskOverdue));
        Assert.Equal(("ai_insight", "ai"), NotificationCatalog.Of(UserNotificationKind.TaskSuggestion));
    }

    [Fact]
    public void Severity_follows_the_meaning()
    {
        Assert.Equal("critical", NotificationCatalog.Severity(UserNotificationKind.TaskOverdue, "Просрочена задача: X"));
        Assert.Equal("critical", NotificationCatalog.Severity(UserNotificationKind.DataSyncProblem, "Обмен с Linko не работает — данные в отчётах устарели"));
        Assert.Equal("info", NotificationCatalog.Severity(UserNotificationKind.DataSyncProblem, "Обмен с Linko восстановлен"));
        Assert.Equal("warning", NotificationCatalog.Severity(UserNotificationKind.TaskDueSoon, "Срок завтра: X"));
        Assert.Equal("info", NotificationCatalog.Severity(UserNotificationKind.TaskAssigned, "Новая задача"));
    }
}

/// <summary>
/// Центр уведомлений на PostgreSQL: фильтры и счётчики вкладок, курсор без повторов и пропусков, прочитано / не прочитано, удаление,
/// «Прочитать все» по разделу, отключённые разделы (нет в списке и счётчиках), «Продлить срок» — только у того, кто может менять задачу.
/// </summary>
public class NotificationCenterDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private async Task<(User Author, User Assignee, Department Department)> SeedAsync()
    {
        var s = Guid.NewGuid().ToString("N")[..6];
        var department = new Department { Code = $"nc-{s}", Name = $"Отдел {s}" };
        var role = new Role { Name = $"Сотрудник уведомлений {s}" };
        role.Permissions.Add(new RolePermission { Code = Permissions.WorkTasks });
        User Person(string login, string name)
        {
            var u = new User { Login = $"{login}-{s}", FullName = name, Department = department };
            u.Roles.Add(new UserRole { Role = role });
            return u;
        }

        var author = Person("nc-author", "Постановщик Задач");
        var assignee = Person("nc-assignee", "Исполнитель Задач");
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        db.Users.AddRange(author, assignee);
        await db.SaveChangesAsync();
        return (author, assignee, department);
    }

    private static UserNotification Note(Guid user, UserNotificationKind kind, int minute, bool read = false, string? link = null) => new()
    {
        RecipientId = user, Kind = kind, Title = $"{kind} {minute}", Link = link,
        CreatedAt = new DateTimeOffset(2026, 10, 30, 9, minute, 0, TimeSpan.Zero), ReadAt = read ? DateTimeOffset.UtcNow : null,
    };

    [PostgresFact]
    public async Task List_filters_counts_pages_without_duplicates_and_hides_muted_sections()
    {
        var (user, other, _) = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var center = sp.GetRequiredService<NotificationCenter>();
        db.UserNotifications.AddRange(
            Note(user.Id, UserNotificationKind.TaskAssigned, 1, read: true),
            Note(user.Id, UserNotificationKind.TaskOverdue, 2),
            Note(user.Id, UserNotificationKind.TaskComment, 3),
            Note(user.Id, UserNotificationKind.TaskSuggestion, 4),
            Note(user.Id, UserNotificationKind.AiBudget, 5, read: true),
            Note(user.Id, UserNotificationKind.DataSyncProblem, 6),
            Note(user.Id, UserNotificationKind.MonthPlanMissing, 7),
            Note(other.Id, UserNotificationKind.TaskAssigned, 8));
        await db.SaveChangesAsync();
        var ct = CancellationToken.None;

        // Страницы по 3: все 7 подряд, новые сверху, без повторов; последняя — без продолжения.
        var seen = new List<string>();
        string? cursor = null;
        var pages = 0;
        do
        {
            var page = await center.ListAsync(user.Id, "all", cursor, 3, null, ct);
            seen.AddRange(page.Items.Select(i => i.Title));
            cursor = page.NextCursor;
            pages++;
        }
        while (cursor is not null && pages < 10);

        Assert.Equal(3, pages);
        Assert.Equal(Enumerable.Range(1, 7).Reverse().Select(m => m), seen.Select(t => int.Parse(t.Split(' ')[1])));

        var all = await center.ListAsync(user.Id, null, null, 30, null, ct);
        Assert.Equal(new NotificationCounts(7, 5, 3, 2, 0), all.Counts);
        Assert.Equal(5, all.Unread);
        var overdue = all.Items.Single(i => i.Kind == UserNotificationKind.TaskOverdue);
        Assert.Equal(("task_overdue", "tasks", "critical"), (overdue.Type, overdue.Category, overdue.Severity));
        Assert.Equal(5, (await center.ListAsync(user.Id, "unread", null, 30, null, ct)).Items.Count);
        Assert.Equal(3, (await center.ListAsync(user.Id, "tasks", null, 30, null, ct)).Items.Count);
        Assert.Equal(2, (await center.ListAsync(user.Id, "ai", null, 30, null, ct)).Items.Count);
        Assert.Empty((await center.ListAsync(user.Id, "hr", null, 30, null, ct)).Items);

        // Прочитано / не прочитано, удаление — только своего; «Прочитать все» по разделу.
        var comment = all.Items.Single(i => i.Kind == UserNotificationKind.TaskComment).Id;
        await center.ReadAsync(user.Id, comment, ct);
        Assert.Equal(4, (await center.CountsAsync(user.Id, ct)).Unread);
        await center.UnreadAsync(user.Id, comment, ct);
        Assert.Equal(5, (await center.CountsAsync(user.Id, ct)).Unread);
        var foreign = await db.UserNotifications.AsNoTracking().Where(n => n.RecipientId == other.Id).Select(n => n.Id).SingleAsync();
        Assert.Equal(0, await center.DeleteAsync(user.Id, foreign, ct));
        Assert.Equal(1, await center.DeleteAsync(user.Id, all.Items.Single(i => i.Kind == UserNotificationKind.MonthPlanMissing).Id, ct));
        await center.ReadAllAsync(user.Id, NotificationCatalog.Tasks, ct);
        Assert.Equal(new NotificationCounts(6, 2, 3, 2, 0), await center.CountsAsync(user.Id, ct)); // непрочитаны AI-предложение и сбой обмена

        // Отключённый раздел: нет ни в списке, ни в счётчиках; неизвестные коды не сохраняются.
        Assert.Equal(["ai"], await center.SetMutesAsync(user.Id, ["ai", "premium", "ai"], ct));
        var muted = await center.ListAsync(user.Id, "all", null, 30, null, ct);
        Assert.DoesNotContain(muted.Items, i => i.Category == "ai");
        Assert.Equal(new NotificationCounts(4, 1, 3, 0, 0), muted.Counts);
        Assert.Empty(await center.SetMutesAsync(user.Id, [], ct));
        Assert.Equal(6, (await center.CountsAsync(user.Id, ct)).All);
    }

    [PostgresFact]
    public async Task Extend_due_is_offered_and_works_only_for_who_can_change_the_task()
    {
        var (author, assignee, department) = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var tasks = sp.GetRequiredService<WorkTaskService>();
        var center = sp.GetRequiredService<NotificationCenter>();
        var ct = CancellationToken.None;
        var permissions = new HashSet<string> { Permissions.WorkTasks };
        var authorCtx = await tasks.ContextAsync(author.Id, permissions, ct);
        var assigneeCtx = await tasks.ContextAsync(assignee.Id, permissions, ct);
        var today = FieldClock.Today;

        var taskId = await tasks.CreateAsync(authorCtx, department.Code, new WorkTaskInput("Сверка остатков склада ГП", null, assignee.Id, WorkTaskPriority.Medium, today.AddDays(1)), ct);
        await db.WorkTasks.Where(t => t.Id == taskId).ExecuteUpdateAsync(u => u.SetProperty(t => t.DueDate, today.AddDays(-1)), ct);
        var link = WorkTaskService.LinkOf(taskId);
        db.UserNotifications.AddRange(Note(author.Id, UserNotificationKind.TaskOverdue, 10, link: link), Note(assignee.Id, UserNotificationKind.TaskOverdue, 10, link: link));
        await db.SaveChangesAsync(ct);

        var forAuthor = (await center.ListAsync(author.Id, "all", null, 30, authorCtx, ct)).Items.Single(i => i.Kind == UserNotificationKind.TaskOverdue);
        Assert.Equal([("open", "primary"), ("extend_due", "secondary")], forAuthor.Actions.Select(a => (a.Action, a.Kind)));
        var forAssignee = (await center.ListAsync(assignee.Id, "all", null, 30, assigneeCtx, ct)).Items.Single(i => i.Kind == UserNotificationKind.TaskOverdue);
        Assert.Equal(["open"], forAssignee.Actions.Select(a => a.Action)); // исполнитель срок не меняет
        // Без права «Задачи» — тоже без действия.
        Assert.Equal(["open"], (await center.ListAsync(author.Id, "all", null, 30, null, ct)).Items.Single(i => i.Kind == UserNotificationKind.TaskOverdue).Actions.Select(a => a.Action));

        await center.ActAsync(author.Id, forAuthor.Id, "extend_due", today.AddDays(3), authorCtx, ct);
        Assert.Equal(today.AddDays(3), await db.WorkTasks.AsNoTracking().Where(t => t.Id == taskId).Select(t => t.DueDate).SingleAsync(ct));
        Assert.NotNull(await db.UserNotifications.AsNoTracking().Where(n => n.Id == forAuthor.Id).Select(n => n.ReadAt).SingleAsync(ct));

        await Assert.ThrowsAsync<FieldForbiddenException>(() => center.ActAsync(assignee.Id, forAssignee.Id, "extend_due", today.AddDays(5), assigneeCtx, ct));
        await Assert.ThrowsAsync<FieldValidationException>(() => center.ActAsync(author.Id, forAuthor.Id, "accept", null, authorCtx, ct));
        await Assert.ThrowsAsync<FieldNotFoundException>(() => center.ActAsync(assignee.Id, forAuthor.Id, "extend_due", today, assigneeCtx, ct));
    }
}
