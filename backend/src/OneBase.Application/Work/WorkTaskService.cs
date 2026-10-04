using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Domain.Audit;
using OneBase.Domain.Identity;
using OneBase.Domain.Work;

namespace OneBase.Application.Work;

/// <summary>Кто спрашивает: пользователь, его права (из запроса, обновляются из базы) и отдел (из базы).</summary>
public sealed record WorkContext(Guid UserId, IReadOnlySet<string> Permissions, Guid? DepartmentId)
{
    /// <summary>Руководство (администратор, директор — право на пользователей): все отделы.</summary>
    public bool SeesAll => Permissions.Contains(Security.Permissions.UsersManage);

    /// <summary>Доступ к Sales Base: задачи и маршруты полевой команды в отделе «Продажи».</summary>
    public bool HasField =>
        Permissions.Contains(Security.Permissions.FieldUse) || Permissions.Contains(Security.Permissions.FieldManage) || Permissions.Contains(Security.Permissions.FieldPlan);
}

public sealed record DepartmentView(
    Guid Id,
    string Code,
    string Name,
    bool CanOpen,
    bool IsMine,
    bool HasTasks,
    bool HasField,
    int OpenTasks,
    int MyTasks,
    int OverdueTasks);

public sealed record WorkTaskRow(
    Guid Id,
    string Title,
    string? Description,
    WorkTaskPriority Priority,
    WorkTaskStatus Status,
    DateOnly? DueDate,
    bool Overdue,
    Guid? AssigneeId,
    string? AssigneeName,
    Guid? CreatedById,
    string? CreatedByName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? Result,
    bool CanEdit,
    IReadOnlyList<WorkTaskStatus> NextStatuses);

public sealed record WorkTaskQuery(string? Status, bool Mine, bool CreatedByMe, Guid? AssigneeId, string? Search, int Page = 1, int PageSize = 50);

public sealed record WorkTaskInput(string? Title, string? Description, Guid? AssigneeId, WorkTaskPriority Priority = WorkTaskPriority.Medium, DateOnly? DueDate = null);

public sealed record WorkTaskStatusInput(WorkTaskStatus Status, string? Result);

public sealed record WorkAssignee(Guid Id, string Name, string? Position);

/// <summary>
/// Раздел «Задачи» OneBase: задачи отделов. Отдел видят его сотрудники (отдел пользователя — из роли) и руководство
/// (право на пользователей). Ставит задачу любой, кто видит отдел; исполнитель — сотрудник этого отдела.
/// Исполнитель ведёт задачу до «выполнена»; автор и руководство правят, отменяют, возвращают в работу.
/// Задачи агентов и супервайзеров — в Sales Base (FieldTaskService); в отделе «Продажи» они показываются отдельно.
/// </summary>
public sealed class WorkTaskService(IAppDbContext db, UserNotifier notifier, IAuditLogger audit)
{
    /// <summary>Порядок отделов на экране «Задачи»: продажи первыми — там полевая команда.</summary>
    public static readonly string[] Order = ["sales", "production", "supply", "finance", "marketing", "hr"];

    public const string SalesCode = "sales";

    private static readonly WorkTaskStatus[] Open = [WorkTaskStatus.New, WorkTaskStatus.InProgress];
    private static readonly WorkTaskStatus[] AllStatuses = Enum.GetValues<WorkTaskStatus>();

    public async Task<WorkContext> ContextAsync(Guid userId, IReadOnlySet<string> permissions, CancellationToken ct)
    {
        var department = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.DepartmentId).FirstOrDefaultAsync(ct);
        return new WorkContext(userId, permissions, department);
    }

    private static bool CanSeeTasks(WorkContext ctx, Department d) => ctx.SeesAll || ctx.DepartmentId == d.Id;

    private static bool CanOpen(WorkContext ctx, Department d) => CanSeeTasks(ctx, d) || (d.Code == SalesCode && ctx.HasField);

    /// <summary>Все отделы (кнопки раздела) с доступом и счётчиками задач отдела.</summary>
    public async Task<List<DepartmentView>> DepartmentsAsync(WorkContext ctx, CancellationToken ct)
    {
        var departments = await db.Departments.AsNoTracking().ToListAsync(ct);
        var visibleIds = departments.Where(d => CanSeeTasks(ctx, d)).Select(d => d.Id).ToArray();
        var today = FieldClock.Today;
        var counts = await db.WorkTasks.AsNoTracking()
            .Where(t => visibleIds.Contains(t.DepartmentId) && Open.Contains(t.Status))
            .GroupBy(t => t.DepartmentId)
            .Select(g => new
            {
                g.Key,
                Open = g.Count(),
                Mine = g.Count(t => t.AssigneeId == ctx.UserId),
                Overdue = g.Count(t => t.DueDate != null && t.DueDate < today),
            })
            .ToDictionaryAsync(x => x.Key, ct);
        return departments
            .OrderBy(d => Array.IndexOf(Order, d.Code) is var i and >= 0 ? i : Order.Length).ThenBy(d => d.Name)
            .Select(d =>
            {
                var c = counts.GetValueOrDefault(d.Id);
                return new DepartmentView(d.Id, d.Code, d.Name, CanOpen(ctx, d), ctx.DepartmentId == d.Id, CanSeeTasks(ctx, d), d.Code == SalesCode && ctx.HasField,
                    c?.Open ?? 0, c?.Mine ?? 0, c?.Overdue ?? 0);
            })
            .ToList();
    }

    /// <summary>Отдел по коду; нет такого — 404, нет доступа к задачам отдела — 403.</summary>
    private async Task<Department> RequireDepartmentAsync(WorkContext ctx, string code, CancellationToken ct)
    {
        var department = await db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Code == code, ct) ?? throw new FieldNotFoundException("Отдел не найден.");
        if (!CanSeeTasks(ctx, department))
        {
            throw new FieldForbiddenException("Задачи этого отдела видят его сотрудники и руководство.");
        }

        return department;
    }

    public async Task<PageResult<WorkTaskRow>> ListAsync(WorkContext ctx, string code, WorkTaskQuery query, CancellationToken ct)
    {
        var department = await RequireDepartmentAsync(ctx, code, ct);
        var today = Today;
        var q = db.WorkTasks.AsNoTracking().Where(t => t.DepartmentId == department.Id);
        q = query.Status switch
        {
            null or "" or "open" => q.Where(t => Open.Contains(t.Status)),
            "overdue" => q.Where(t => Open.Contains(t.Status) && t.DueDate != null && t.DueDate < today),
            "closed" => q.Where(t => !Open.Contains(t.Status)),
            "all" => q,
            _ when Enum.TryParse<WorkTaskStatus>(query.Status, true, out var st) => q.Where(t => t.Status == st),
            _ => q,
        };

        if (query.Mine)
        {
            q = q.Where(t => t.AssigneeId == ctx.UserId);
        }

        if (query.CreatedByMe)
        {
            q = q.Where(t => t.CreatedById == ctx.UserId);
        }

        if (query.AssigneeId is { } assignee)
        {
            q = q.Where(t => t.AssigneeId == assignee);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var text = query.Search.Trim().ToLower();
            q = q.Where(t => t.Title.ToLower().Contains(text) || (t.Description != null && t.Description.ToLower().Contains(text)));
        }

        var total = await q.CountAsync(ct);
        var pageSize = Math.Clamp(query.PageSize, 10, 200);
        var page = Math.Max(query.Page, 1);
        // Открытые сначала: просроченные и срочные выше, потом по сроку; закрытые — свежие сверху.
        var rows = await q
            .OrderBy(t => t.Status == WorkTaskStatus.New || t.Status == WorkTaskStatus.InProgress ? 0 : 1)
            .ThenByDescending(t => t.Priority == WorkTaskPriority.Urgent ? 3 : t.Priority == WorkTaskPriority.High ? 2 : t.Priority == WorkTaskPriority.Medium ? 1 : 0)
            .ThenBy(t => t.DueDate == null)
            .ThenBy(t => t.DueDate)
            .ThenByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(t => new
            {
                Task = t,
                Assignee = t.Assignee == null ? null : t.Assignee.FullName,
                Author = t.CreatedBy == null ? null : t.CreatedBy.FullName,
            })
            .ToListAsync(ct);
        return new PageResult<WorkTaskRow>(rows.Select(r => Row(ctx, r.Task, r.Assignee, r.Author, today)).ToList(), total, page, pageSize);
    }

    private static DateOnly Today => FieldClock.Today;

    private static WorkTaskRow Row(WorkContext ctx, WorkTask t, string? assignee, string? author, DateOnly today)
    {
        var isAssignee = t.AssigneeId == ctx.UserId;
        var canEdit = CanEdit(ctx, t);
        return new WorkTaskRow(t.Id, t.Title, t.Description, t.Priority, t.Status, t.DueDate, Open.Contains(t.Status) && t.DueDate < today,
            t.AssigneeId, assignee, t.CreatedById, author, t.CreatedAt, t.CompletedAt, t.Result, canEdit,
            AllStatuses.Where(s => CanTransition(t.Status, s, isAssignee || (t.AssigneeId is null && !canEdit), canEdit)).ToList());
    }

    /// <summary>Править, отменять и возвращать задачу: автор и руководство.</summary>
    private static bool CanEdit(WorkContext ctx, WorkTask t) => ctx.SeesAll || t.CreatedById == ctx.UserId;

    /// <summary>
    /// Переходы: исполнитель (или любой сотрудник отдела, если исполнителя нет) — в работу и «выполнена»;
    /// автор и руководство — то же, плюс отмена и возврат выполненной или отменённой в работу.
    /// </summary>
    public static bool CanTransition(WorkTaskStatus from, WorkTaskStatus to, bool isAssignee, bool canEdit)
    {
        if (from == to)
        {
            return false;
        }

        var worker = isAssignee || canEdit;
        return (from, to) switch
        {
            (WorkTaskStatus.New, WorkTaskStatus.InProgress) => worker,
            (WorkTaskStatus.New or WorkTaskStatus.InProgress, WorkTaskStatus.Done) => worker,
            (WorkTaskStatus.New or WorkTaskStatus.InProgress, WorkTaskStatus.Cancelled) => canEdit,
            (WorkTaskStatus.Done or WorkTaskStatus.Cancelled, WorkTaskStatus.InProgress) => canEdit,
            _ => false,
        };
    }

    /// <summary>Сотрудники отдела — кому можно поставить задачу.</summary>
    public async Task<List<WorkAssignee>> AssigneesAsync(WorkContext ctx, string code, CancellationToken ct)
    {
        var department = await RequireDepartmentAsync(ctx, code, ct);
        return await db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.DepartmentId == department.Id)
            .OrderBy(u => u.FullName)
            .Select(u => new WorkAssignee(u.Id, u.FullName, u.Position))
            .ToListAsync(ct);
    }

    private async Task<Guid?> ValidAssigneeAsync(Department department, Guid? assigneeId, CancellationToken ct)
    {
        if (assigneeId is not { } id)
        {
            return null;
        }

        if (!await IsMemberAsync(department, id, ct))
        {
            throw new FieldValidationException("Исполнитель — действующий сотрудник этого отдела.");
        }

        return id;
    }

    private Task<bool> IsMemberAsync(Department department, Guid userId, CancellationToken ct) =>
        db.Users.AnyAsync(u => u.Id == userId && u.IsActive && u.DepartmentId == department.Id, ct);

    private static (string Title, string? Description) Validate(WorkTaskInput input, DateOnly? currentDue = null)
    {
        var title = (input.Title ?? string.Empty).Trim();
        if (title.Length is 0 or > 300)
        {
            throw new FieldValidationException("Название задачи — от 1 до 300 символов.");
        }

        var description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        if (description is { Length: > 4000 })
        {
            throw new FieldValidationException("Описание — до 4000 символов.");
        }

        if (input.DueDate is { } due && due != currentDue && due < Today)
        {
            throw new FieldValidationException("Срок не может быть в прошлом.");
        }

        return (title, description);
    }

    public async Task<Guid> CreateAsync(WorkContext ctx, string code, WorkTaskInput input, CancellationToken ct)
    {
        var department = await RequireDepartmentAsync(ctx, code, ct);
        var (title, description) = Validate(input);
        var assignee = await ValidAssigneeAsync(department, input.AssigneeId, ct);
        var task = new WorkTask
        {
            DepartmentId = department.Id,
            Title = title,
            Description = description,
            AssigneeId = assignee,
            CreatedById = ctx.UserId,
            Priority = input.Priority,
            DueDate = input.DueDate,
        };
        db.WorkTasks.Add(task);
        if (assignee is { } a && a != ctx.UserId)
        {
            notifier.Notify(a, UserNotificationKind.TaskAssigned, $"Новая задача: {title}", DueText(task.DueDate), LinkOf(department));
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "work.task.created", "work_task", task.Id.ToString(),
            new { department = department.Code, assignee, priority = task.Priority.ToString(), task.DueDate }, ct);
        return task.Id;
    }

    private async Task<(WorkTask Task, Department Department)> RequireTaskAsync(WorkContext ctx, Guid id, CancellationToken ct)
    {
        var task = await db.WorkTasks.Include(t => t.Department).FirstOrDefaultAsync(t => t.Id == id, ct);
        // Чужой отдел — как будто задачи нет (404): по номеру не узнать, что она существует.
        if (task?.Department is null || !CanSeeTasks(ctx, task.Department))
        {
            throw new FieldNotFoundException("Задача не найдена.");
        }

        return (task, task.Department);
    }

    public async Task UpdateAsync(WorkContext ctx, Guid id, WorkTaskInput input, CancellationToken ct)
    {
        var (task, department) = await RequireTaskAsync(ctx, id, ct);
        if (!CanEdit(ctx, task))
        {
            throw new FieldForbiddenException("Менять задачу может её автор или руководство.");
        }

        var (title, description) = Validate(input, task.DueDate);
        // Прежнего исполнителя можно оставить, даже если он уже не в отделе; новый — только сотрудник отдела.
        var assignee = input.AssigneeId == task.AssigneeId ? task.AssigneeId : await ValidAssigneeAsync(department, input.AssigneeId, ct);
        var reassigned = assignee != task.AssigneeId;
        task.Title = title;
        task.Description = description;
        task.Priority = input.Priority;
        task.DueDate = input.DueDate;
        task.AssigneeId = assignee;
        task.UpdatedAt = DateTimeOffset.UtcNow;
        // Прежний исполнитель, который уже не в отделе, задачу не откроет — ему не пишем.
        if (assignee is { } a && a != ctx.UserId && (reassigned || await IsMemberAsync(department, a, ct)))
        {
            notifier.Notify(a, reassigned ? UserNotificationKind.TaskAssigned : UserNotificationKind.TaskChanged,
                reassigned ? $"Новая задача: {title}" : $"Задача изменена: {title}", DueText(task.DueDate), LinkOf(department));
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "work.task.updated", "work_task", task.Id.ToString(),
            new { assignee, priority = task.Priority.ToString(), task.DueDate }, ct);
    }

    public async Task ChangeStatusAsync(WorkContext ctx, Guid id, WorkTaskStatusInput input, CancellationToken ct)
    {
        var (task, department) = await RequireTaskAsync(ctx, id, ct);
        var canEdit = CanEdit(ctx, task);
        var isAssignee = task.AssigneeId == ctx.UserId || (task.AssigneeId is null && !canEdit);
        if (!CanTransition(task.Status, input.Status, isAssignee, canEdit))
        {
            throw new FieldValidationException("Так изменить статус задачи нельзя.");
        }

        var from = task.Status;
        task.Status = input.Status;
        task.UpdatedAt = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(input.Result))
        {
            var result = input.Result.Trim();
            task.Result = result.Length <= 2000 ? result : result[..2000];
        }

        // Задача отдела без исполнителя: кто взял в работу или выполнил, тот и исполнитель.
        if (task.AssigneeId is null && input.Status is WorkTaskStatus.InProgress or WorkTaskStatus.Done && !canEdit)
        {
            task.AssigneeId = ctx.UserId;
        }

        var link = LinkOf(department);
        switch (input.Status)
        {
            case WorkTaskStatus.Done:
                task.CompletedAt = DateTimeOffset.UtcNow;
                if (task.CreatedById is { } author && author != ctx.UserId)
                {
                    notifier.Notify(author, UserNotificationKind.TaskDone, $"Выполнена задача: {task.Title}", task.Result, link);
                }

                break;
            case WorkTaskStatus.Cancelled:
                if (task.AssigneeId is { } a && a != ctx.UserId)
                {
                    notifier.Notify(a, UserNotificationKind.TaskChanged, $"Задача отменена: {task.Title}", task.Result, link);
                }

                break;
            case WorkTaskStatus.InProgress when from is WorkTaskStatus.Done or WorkTaskStatus.Cancelled:
                task.CompletedAt = null;
                if (task.AssigneeId is { } back && back != ctx.UserId)
                {
                    notifier.Notify(back, UserNotificationKind.TaskChanged, $"Задача возвращена в работу: {task.Title}", task.Result, link);
                }

                break;
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "work.task.status", "work_task", task.Id.ToString(),
            new { from = from.ToString(), to = task.Status.ToString() }, ct);
    }

    private static string LinkOf(Department department) => $"/tasks/{department.Code}";

    private static string? DueText(DateOnly? due) => due is { } d ? $"Срок: {d:dd.MM.yyyy}" : null;
}
