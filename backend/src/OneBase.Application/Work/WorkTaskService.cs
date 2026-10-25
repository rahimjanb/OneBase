using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Domain.Audit;
using OneBase.Domain.Identity;
using OneBase.Domain.Work;

namespace OneBase.Application.Work;

/// <summary>
/// Кто спрашивает: пользователь, его права (из запроса), отдел (из базы), отделы, которыми он руководит в «Задачах», и проекты, где он
/// участник (значение — руководитель проекта).
/// </summary>
public sealed record WorkContext(
    Guid UserId,
    IReadOnlySet<string> Permissions,
    Guid? DepartmentId,
    IReadOnlySet<Guid>? HeadOf = null,
    IReadOnlyDictionary<Guid, bool>? Projects = null)
{
    /// <summary>Руководство (администратор, директор — право на пользователей): задачи всей компании.</summary>
    public bool SeesAll => Permissions.Contains(Security.Permissions.UsersManage);

    /// <summary>Доступ к Sales Base: задачи и маршруты полевой команды в отделе «Продажи».</summary>
    public bool HasField =>
        Permissions.Contains(Security.Permissions.FieldUse) || Permissions.Contains(Security.Permissions.FieldManage) || Permissions.Contains(Security.Permissions.FieldPlan);

    /// <summary>Управляет задачами отдела: руководство или руководитель этого отдела.</summary>
    public bool Heads(Guid departmentId) => SeesAll || (HeadOf?.Contains(departmentId) ?? false);

    /// <summary>Руководит хотя бы одним отделом (или всей компанией).</summary>
    public bool HeadsAny => SeesAll || HeadOf is { Count: > 0 };

    public bool InProject(Guid? projectId) => projectId is { } id && Projects?.ContainsKey(id) == true;

    public bool ManagesProject(Guid? projectId) => projectId is { } id && Projects?.TryGetValue(id, out var manager) == true && manager;
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
    int OverdueTasks,
    bool IsHead = false);

/// <summary>Строка задачи для таблицы, канбана и инструментов AI. CanEdit — то же, что CanManage (прежнее имя для совместимости).</summary>
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
    IReadOnlyList<WorkTaskStatus> NextStatuses,
    Guid DepartmentId,
    string DepartmentCode,
    string DepartmentName,
    Guid? ProjectId,
    string? ProjectName,
    DateOnly? StartDate,
    int Progress,
    bool IsBlocked,
    string? BlockedReason,
    DateTimeOffset? ArchivedAt,
    int ChecklistDone,
    int ChecklistTotal,
    bool CanManage,
    bool CanWork);

/// <summary>Прежний запрос списка задач одного отдела (страница отдела и инструмент AI).</summary>
public sealed record WorkTaskQuery(string? Status, bool Mine, bool CreatedByMe, Guid? AssigneeId, string? Search, int Page = 1, int PageSize = 50);

/// <summary>
/// Поиск по всем доступным задачам. Scope: all — все доступные (без архива); my — назначенные мне; department — один отдел; archive —
/// архив и завершённые (выполненные, отменённые); board — для канбана (без архива и отменённых). Status — группа (open, overdue, review,
/// blocked, closed) или статусы через запятую; Priority — через запятую. From/To — по дате создания или сроку (DateField = due).
/// </summary>
public sealed record WorkTaskSearch(
    string Scope = "all",
    string? Department = null,
    Guid? ProjectId = null,
    string? Status = null,
    string? Priority = null,
    Guid? AssigneeId = null,
    bool CreatedByMe = false,
    DateOnly? From = null,
    DateOnly? To = null,
    string? DateField = null,
    string? Search = null,
    string? Sort = null,
    bool Desc = false,
    int Page = 1,
    int PageSize = 25);

public sealed record WorkTaskInput(
    string? Title,
    string? Description,
    Guid? AssigneeId,
    WorkTaskPriority Priority = WorkTaskPriority.Medium,
    DateOnly? DueDate = null,
    DateOnly? StartDate = null,
    Guid? ProjectId = null);

/// <summary>Смена статуса; Comment — итог, причина возврата на доработку или отмены (для возврата обязателен).</summary>
public sealed record WorkTaskStatusInput(WorkTaskStatus Status, string? Result, string? Comment = null);

/// <summary>Кого можно назначить исполнителем. InDepartment — сотрудник отдела задачи или участник её проекта (в списке — первыми).</summary>
public sealed record WorkAssignee(Guid Id, string Name, string? Position, string? Department = null, bool InDepartment = false);

public sealed record WorkChecklistView(Guid Id, string Title, bool IsDone, int SortOrder, DateTimeOffset? DoneAt);

public sealed record WorkCommentView(Guid Id, Guid? AuthorId, string AuthorName, string Text, DateTimeOffset CreatedAt);

public sealed record WorkAttachmentView(Guid Id, string FileName, string ContentType, long SizeBytes, string? UploadedByName, DateTimeOffset CreatedAt, bool CanDelete);

public sealed record WorkEventView(Guid Id, string Kind, string Text, string ActorName, DateTimeOffset CreatedAt);

public sealed record WorkTaskDetail(
    WorkTaskRow Task,
    IReadOnlyList<WorkChecklistView> Checklist,
    IReadOnlyList<WorkCommentView> Comments,
    IReadOnlyList<WorkAttachmentView> Attachments,
    IReadOnlyList<WorkEventView> History,
    bool CanArchive,
    bool CanRestore,
    bool CanComment,
    bool CanAttach);

/// <summary>
/// Раздел «Задачи» OneBase: задачи отделов и проектов. Права проверяются здесь, на сервере, — никогда по параметрам запроса.
/// <list type="bullet">
/// <item>Руководство (право на пользователей: администратор, директор) — все задачи компании.</item>
/// <item>Руководитель отдела (work.DepartmentHeads) — все задачи своих отделов: ставит, правит, назначает, переносит сроки, принимает.</item>
/// <item>Руководитель проекта (PM, участник проекта с IsManager) — то же для задач проекта.</item>
/// <item>Сотрудник — задачи своего отдела, назначенные ему, поставленные им и задачи проектов, где он участник.</item>
/// </list>
/// Постановщик управляет своей задачей. Исполнитель ведёт задачу: берёт в работу, отмечает чек-лист и процент, отправляет на проверку;
/// принимает результат или возвращает на доработку постановщик или руководитель. Задачи полевой команды — в Sales Base (FieldTaskService).
/// </summary>
public sealed partial class WorkTaskService(IAppDbContext db, UserNotifier notifier, IAuditLogger audit)
{
    /// <summary>Порядок отделов на экране «Задачи»: продажи первыми — там полевая команда.</summary>
    public static readonly string[] Order = ["sales", "production", "supply", "finance", "marketing", "hr"];

    public const string SalesCode = "sales";

    /// <summary>Открытые: ещё не выполнены и не отменены (в том числе на проверке).</summary>
    internal static readonly WorkTaskStatus[] Open = [WorkTaskStatus.New, WorkTaskStatus.InProgress, WorkTaskStatus.Review];

    private static readonly WorkTaskStatus[] Closed = [WorkTaskStatus.Done, WorkTaskStatus.Cancelled];
    private static readonly WorkTaskStatus[] AllStatuses = [WorkTaskStatus.New, WorkTaskStatus.InProgress, WorkTaskStatus.Review, WorkTaskStatus.Done, WorkTaskStatus.Cancelled];

    public async Task<WorkContext> ContextAsync(Guid userId, IReadOnlySet<string> permissions, CancellationToken ct)
    {
        var department = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.DepartmentId).FirstOrDefaultAsync(ct);
        var heads = await db.WorkDepartmentHeads.AsNoTracking().Where(h => h.UserId == userId).Select(h => h.DepartmentId).ToListAsync(ct);
        var projects = await db.WorkProjectMembers.AsNoTracking()
            .Where(m => m.UserId == userId && m.Project!.ArchivedAt == null)
            .Select(m => new { m.ProjectId, m.IsManager })
            .ToListAsync(ct);
        return new WorkContext(userId, permissions, department, heads.ToHashSet(), projects.ToDictionary(p => p.ProjectId, p => p.IsManager));
    }

    // ---------- Правила доступа (чистые функции — проверяются тестами) ----------

    /// <summary>Видит все задачи отдела: руководство, руководитель отдела, сотрудник отдела.</summary>
    public static bool CanSeeDepartment(WorkContext ctx, Guid departmentId) => ctx.Heads(departmentId) || ctx.DepartmentId == departmentId;

    public static bool CanView(WorkContext ctx, WorkTask t) =>
        CanSeeDepartment(ctx, t.DepartmentId) || t.AssigneeId == ctx.UserId || t.CreatedById == ctx.UserId || ctx.InProject(t.ProjectId);

    /// <summary>Управлять задачей (править, назначать, переносить срок, принимать, возвращать, отменять, архивировать).</summary>
    public static bool CanManage(WorkContext ctx, WorkTask t) => ctx.Heads(t.DepartmentId) || ctx.ManagesProject(t.ProjectId) || t.CreatedById == ctx.UserId;

    /// <summary>Вести задачу: управляющий, исполнитель; задачу без исполнителя — сотрудник отдела или участник проекта (он и станет исполнителем).</summary>
    public static bool CanWork(WorkContext ctx, WorkTask t) =>
        CanManage(ctx, t) || t.AssigneeId == ctx.UserId || (t.AssigneeId is null && (ctx.DepartmentId == t.DepartmentId || ctx.InProject(t.ProjectId)));

    /// <summary>
    /// Переходы статусов. Ведущий (исполнитель): «Новая» → «В работе» → «На проверке». Управляющий: принять («Выполнена») или вернуть на доработку
    /// («В работе»), закрыть сразу, отменить, вернуть выполненную или отменённую в работу.
    /// </summary>
    public static bool CanTransition(WorkTaskStatus from, WorkTaskStatus to, bool canWork, bool canManage) => (from, to) switch
    {
        _ when from == to => false,
        (WorkTaskStatus.New, WorkTaskStatus.InProgress) => canWork,
        (WorkTaskStatus.New or WorkTaskStatus.InProgress, WorkTaskStatus.Review) => canWork,
        (WorkTaskStatus.Review, WorkTaskStatus.Done) => canManage,
        (WorkTaskStatus.Review, WorkTaskStatus.InProgress) => canManage,
        (WorkTaskStatus.New or WorkTaskStatus.InProgress, WorkTaskStatus.Done) => canManage,
        (WorkTaskStatus.New or WorkTaskStatus.InProgress or WorkTaskStatus.Review, WorkTaskStatus.Cancelled) => canManage,
        (WorkTaskStatus.Done or WorkTaskStatus.Cancelled, WorkTaskStatus.InProgress) => canManage,
        _ => false,
    };

    /// <summary>Задачи, которые видит пользователь (фильтр в базе).</summary>
    private IQueryable<WorkTask> Visible(WorkContext ctx)
    {
        var q = db.WorkTasks.AsNoTracking();
        if (ctx.SeesAll)
        {
            return q;
        }

        var me = ctx.UserId;
        var department = ctx.DepartmentId;
        var heads = (ctx.HeadOf ?? new HashSet<Guid>()).ToArray();
        var projects = (ctx.Projects ?? new Dictionary<Guid, bool>()).Keys.ToArray();
        return q.Where(t => (department != null && t.DepartmentId == department) || heads.Contains(t.DepartmentId) || t.AssigneeId == me || t.CreatedById == me
                            || (t.ProjectId != null && projects.Contains(t.ProjectId.Value)));
    }

    private static DateOnly Today => FieldClock.Today;

    // ---------- Отделы ----------

    /// <summary>Все отделы (кнопки раздела) с доступом и счётчиками задач отдела.</summary>
    public async Task<List<DepartmentView>> DepartmentsAsync(WorkContext ctx, CancellationToken ct)
    {
        var departments = await db.Departments.AsNoTracking().ToListAsync(ct);
        var visibleIds = departments.Where(d => CanSeeDepartment(ctx, d.Id)).Select(d => d.Id).ToArray();
        var today = Today;
        var counts = await db.WorkTasks.AsNoTracking()
            .Where(t => visibleIds.Contains(t.DepartmentId) && Open.Contains(t.Status) && t.ArchivedAt == null)
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
                var sees = CanSeeDepartment(ctx, d.Id);
                return new DepartmentView(d.Id, d.Code, d.Name, sees || (d.Code == SalesCode && ctx.HasField), ctx.DepartmentId == d.Id, sees,
                    d.Code == SalesCode && ctx.HasField, c?.Open ?? 0, c?.Mine ?? 0, c?.Overdue ?? 0, ctx.Heads(d.Id));
            })
            .ToList();
    }

    /// <summary>Отдел по коду; нет такого — 404, нет доступа к задачам отдела — 403.</summary>
    private async Task<Department> RequireDepartmentAsync(WorkContext ctx, string code, CancellationToken ct)
    {
        var department = await db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Code == code, ct) ?? throw new FieldNotFoundException("Отдел не найден.");
        if (!CanSeeDepartment(ctx, department.Id))
        {
            throw new FieldForbiddenException("Задачи этого отдела видят его сотрудники, руководитель отдела и руководство.");
        }

        return department;
    }

    // ---------- Списки ----------

    /// <summary>Задачи одного отдела (страница отдела и инструмент AI): открытые по умолчанию, без архива.</summary>
    public async Task<PageResult<WorkTaskRow>> ListAsync(WorkContext ctx, string code, WorkTaskQuery query, CancellationToken ct)
    {
        var department = await RequireDepartmentAsync(ctx, code, ct);
        var status = query.Status is null or "" ? "open" : query.Status;
        return await SearchAsync(ctx, new WorkTaskSearch(
            status is "closed" or "all" ? "all" : "department", department.Code, null, status == "all" ? null : status, null,
            query.Mine ? ctx.UserId : query.AssigneeId, query.CreatedByMe, Search: query.Search, Page: query.Page, PageSize: query.PageSize), ct, department);
    }

    public async Task<PageResult<WorkTaskRow>> SearchAsync(WorkContext ctx, WorkTaskSearch s, CancellationToken ct, Department? department = null)
    {
        var q = Visible(ctx);
        var today = Today;
        switch (s.Scope)
        {
            case "my":
                q = q.Where(t => t.AssigneeId == ctx.UserId && t.ArchivedAt == null);
                break;
            case "department":
                department ??= await RequireDepartmentAsync(ctx, s.Department ?? throw new FieldValidationException("Укажите отдел."), ct);
                q = q.Where(t => t.DepartmentId == department.Id && t.ArchivedAt == null);
                break;
            case "archive":
                q = q.Where(t => t.ArchivedAt != null || t.Status == WorkTaskStatus.Done || t.Status == WorkTaskStatus.Cancelled);
                break;
            case "board":
                q = q.Where(t => t.ArchivedAt == null && t.Status != WorkTaskStatus.Cancelled);
                break;
            default:
                q = q.Where(t => t.ArchivedAt == null);
                break;
        }

        if (s.Scope != "department" && !string.IsNullOrWhiteSpace(s.Department))
        {
            var d = await RequireDepartmentAsync(ctx, s.Department, ct);
            q = q.Where(t => t.DepartmentId == d.Id);
            department ??= d;
        }

        if (s.ProjectId is { } project)
        {
            q = q.Where(t => t.ProjectId == project);
        }

        q = StatusFilter(q, s.Status, today);
        var priorities = Parse<WorkTaskPriority>(s.Priority);
        if (priorities.Count > 0)
        {
            q = q.Where(t => priorities.Contains(t.Priority));
        }

        if (s.AssigneeId is { } assignee)
        {
            q = q.Where(t => t.AssigneeId == assignee);
        }

        if (s.CreatedByMe)
        {
            q = q.Where(t => t.CreatedById == ctx.UserId);
        }

        if (s.DateField == "due")
        {
            if (s.From is { } from)
            {
                q = q.Where(t => t.DueDate >= from);
            }

            if (s.To is { } to)
            {
                q = q.Where(t => t.DueDate <= to);
            }
        }
        else
        {
            // Дата создания — по Ташкенту (UTC+5): граница дня — полночь Ташкента.
            if (s.From is { } from)
            {
                var fromUtc = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(5)).ToUniversalTime();
                q = q.Where(t => t.CreatedAt >= fromUtc);
            }

            if (s.To is { } to)
            {
                var toUtc = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(5)).ToUniversalTime();
                q = q.Where(t => t.CreatedAt < toUtc);
            }
        }

        if (!string.IsNullOrWhiteSpace(s.Search))
        {
            var text = s.Search.Trim().ToLower();
            q = q.Where(t => t.Title.ToLower().Contains(text) || (t.Description != null && t.Description.ToLower().Contains(text))
                             || (t.Assignee != null && t.Assignee.FullName.ToLower().Contains(text)));
        }

        var total = await q.CountAsync(ct);
        var pageSize = Math.Clamp(s.PageSize, 5, s.Scope == "board" ? 500 : 200);
        var page = Math.Max(s.Page, 1);
        var rows = await Sorted(q, s.Sort, s.Desc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(t => new RowData(t, t.Assignee == null ? null : t.Assignee.FullName, t.CreatedBy == null ? null : t.CreatedBy.FullName, t.Department!.Code,
                t.Department.Name, t.Project == null ? null : t.Project.Name, t.Checklist.Count(c => c.IsDone), t.Checklist.Count))
            .ToListAsync(ct);
        return new PageResult<WorkTaskRow>(rows.Select(r => Row(ctx, r, today)).ToList(), total, page, pageSize);
    }

    private sealed record RowData(WorkTask Task, string? Assignee, string? Author, string DepartmentCode, string DepartmentName, string? Project, int Done, int Total);

    private static IQueryable<WorkTask> StatusFilter(IQueryable<WorkTask> q, string? status, DateOnly today) => status switch
    {
        null or "" or "any" => q,
        "open" => q.Where(t => Open.Contains(t.Status)),
        "overdue" => q.Where(t => Open.Contains(t.Status) && t.DueDate != null && t.DueDate < today),
        "blocked" => q.Where(t => Open.Contains(t.Status) && t.IsBlocked),
        "closed" => q.Where(t => Closed.Contains(t.Status)),
        _ => Parse<WorkTaskStatus>(status) is { Count: > 0 } list ? q.Where(t => list.Contains(t.Status)) : q,
    };

    private static List<T> Parse<T>(string? csv) where T : struct, Enum =>
        string.IsNullOrWhiteSpace(csv) ? [] : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => Enum.TryParse<T>(v, true, out var e) ? (T?)e : null).OfType<T>().Distinct().ToList();

    /// <summary>Сортировка таблицы; без неё — открытые сначала, затем критичность, срок, свежесть.</summary>
    private static IQueryable<WorkTask> Sorted(IQueryable<WorkTask> q, string? sort, bool desc)
    {
        return sort switch
        {
            "title" => desc ? q.OrderByDescending(t => t.Title) : q.OrderBy(t => t.Title),
            "created" => desc ? q.OrderByDescending(t => t.CreatedAt) : q.OrderBy(t => t.CreatedAt),
            "due" => desc ? q.OrderBy(t => t.DueDate == null).ThenByDescending(t => t.DueDate) : q.OrderBy(t => t.DueDate == null).ThenBy(t => t.DueDate),
            "progress" => desc ? q.OrderByDescending(t => t.Progress) : q.OrderBy(t => t.Progress),
            "status" => desc ? q.OrderByDescending(t => t.Status) : q.OrderBy(t => t.Status),
            "priority" => desc
                ? q.OrderByDescending(t => t.Priority == WorkTaskPriority.Urgent ? 3 : t.Priority == WorkTaskPriority.High ? 2 : t.Priority == WorkTaskPriority.Medium ? 1 : 0)
                : q.OrderBy(t => t.Priority == WorkTaskPriority.Urgent ? 3 : t.Priority == WorkTaskPriority.High ? 2 : t.Priority == WorkTaskPriority.Medium ? 1 : 0),
            "assignee" => desc ? q.OrderByDescending(t => t.Assignee!.FullName) : q.OrderBy(t => t.Assignee!.FullName),
            "department" => desc ? q.OrderByDescending(t => t.Department!.Name) : q.OrderBy(t => t.Department!.Name),
            _ => q
                .OrderBy(t => t.Status == WorkTaskStatus.New || t.Status == WorkTaskStatus.InProgress || t.Status == WorkTaskStatus.Review ? 0 : 1)
                .ThenByDescending(t => t.Priority == WorkTaskPriority.Urgent ? 3 : t.Priority == WorkTaskPriority.High ? 2 : t.Priority == WorkTaskPriority.Medium ? 1 : 0)
                .ThenBy(t => t.DueDate == null)
                .ThenBy(t => t.DueDate)
                .ThenByDescending(t => t.CreatedAt),
        };
    }

    private static WorkTaskRow Row(WorkContext ctx, RowData r, DateOnly today)
    {
        var t = r.Task;
        var manage = CanManage(ctx, t);
        var work = CanWork(ctx, t);
        return new WorkTaskRow(t.Id, t.Title, t.Description, t.Priority, t.Status, t.DueDate, Open.Contains(t.Status) && t.DueDate < today,
            t.AssigneeId, r.Assignee, t.CreatedById, r.Author, t.CreatedAt, t.CompletedAt, t.Result, manage,
            t.ArchivedAt is null ? AllStatuses.Where(s => CanTransition(t.Status, s, work, manage)).ToList() : [],
            t.DepartmentId, r.DepartmentCode, r.DepartmentName, t.ProjectId, r.Project, t.StartDate, t.Progress, t.IsBlocked, t.BlockedReason, t.ArchivedAt,
            r.Done, r.Total, manage, work);
    }

    // ---------- Карточка ----------

    public async Task<WorkTaskDetail> GetAsync(WorkContext ctx, Guid id, CancellationToken ct)
    {
        var data = await Visible(ctx).Where(t => t.Id == id)
            .Select(t => new RowData(t, t.Assignee == null ? null : t.Assignee.FullName, t.CreatedBy == null ? null : t.CreatedBy.FullName, t.Department!.Code,
                t.Department.Name, t.Project == null ? null : t.Project.Name, t.Checklist.Count(c => c.IsDone), t.Checklist.Count))
            .FirstOrDefaultAsync(ct) ?? throw new FieldNotFoundException("Задача не найдена.");
        var row = Row(ctx, data, Today);
        var checklist = await db.WorkTaskChecklist.AsNoTracking().Where(c => c.TaskId == id).OrderBy(c => c.SortOrder).ThenBy(c => c.CreatedAt)
            .Select(c => new WorkChecklistView(c.Id, c.Title, c.IsDone, c.SortOrder, c.DoneAt)).ToListAsync(ct);
        var comments = await db.WorkTaskComments.AsNoTracking().Where(c => c.TaskId == id).OrderBy(c => c.CreatedAt)
            .Select(c => new WorkCommentView(c.Id, c.AuthorId, c.Author == null ? "удалённый пользователь" : c.Author.FullName, c.Text, c.CreatedAt)).ToListAsync(ct);
        var attachments = await db.WorkTaskAttachments.AsNoTracking().Where(a => a.TaskId == id).OrderBy(a => a.CreatedAt)
            .Select(a => new { a.Id, a.FileName, a.ContentType, a.SizeBytes, a.UploadedById, Name = a.UploadedBy == null ? null : a.UploadedBy.FullName, a.CreatedAt })
            .ToListAsync(ct);
        var history = await db.WorkTaskEvents.AsNoTracking().Where(e => e.TaskId == id).OrderByDescending(e => e.CreatedAt).Take(200)
            .Select(e => new WorkEventView(e.Id, e.Kind, e.Text, e.Actor == null ? "система" : e.Actor.FullName, e.CreatedAt)).ToListAsync(ct);
        var task = data.Task;
        return new WorkTaskDetail(row, checklist, comments,
            attachments.Select(a => new WorkAttachmentView(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.Name, a.CreatedAt, row.CanManage || a.UploadedById == ctx.UserId)).ToList(),
            history,
            row.CanManage && task.ArchivedAt is null && Closed.Contains(task.Status),
            row.CanManage && task.ArchivedAt is not null,
            true,
            row.CanWork || row.CanManage);
    }

    private async Task<(WorkTask Task, Department Department)> RequireTaskAsync(WorkContext ctx, Guid id, CancellationToken ct)
    {
        var task = await db.WorkTasks.Include(t => t.Department).FirstOrDefaultAsync(t => t.Id == id, ct);
        // Нет доступа — как будто задачи нет (404): по номеру не узнать, что она существует.
        if (task?.Department is null || !CanView(ctx, task))
        {
            throw new FieldNotFoundException("Задача не найдена.");
        }

        return (task, task.Department);
    }

    // ---------- Исполнители ----------

    /// <summary>
    /// Кому можно поставить задачу: любой действующий пользователь OneBase. Сначала — сотрудники отдела задачи и участники её проекта,
    /// затем остальные (с отделом и должностью — чтобы не перепутать однофамильцев).
    /// </summary>
    public async Task<List<WorkAssignee>> AssigneesAsync(WorkContext ctx, string code, CancellationToken ct, Guid? projectId = null)
    {
        var department = await db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Code == code, ct) ?? throw new FieldNotFoundException("Отдел не найден.");
        if (!CanSeeDepartment(ctx, department.Id) && !ctx.InProject(projectId))
        {
            throw new FieldForbiddenException("Задачи этого отдела видят его сотрудники, руководитель отдела и руководство.");
        }

        var members = projectId is { } p
            ? await db.WorkProjectMembers.AsNoTracking().Where(m => m.ProjectId == p).Select(m => m.UserId).ToListAsync(ct)
            : [];
        var users = await db.Users.AsNoTracking()
            .Where(u => u.IsActive)
            .Select(u => new { u.Id, u.FullName, u.Login, u.Position, u.DepartmentId, Department = u.Department != null ? u.Department.Name : null })
            .ToListAsync(ct);
        return users
            .Select(u => new WorkAssignee(u.Id, string.IsNullOrWhiteSpace(u.FullName) ? u.Login : u.FullName, u.Position, u.Department,
                u.DepartmentId == department.Id || members.Contains(u.Id)))
            .OrderByDescending(a => a.InDepartment)
            .ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>Исполнитель — любой действующий пользователь OneBase (задачу он видит как назначенную ему, в «Моих задачах»).</summary>
    private async Task<bool> IsValidAssigneeAsync(Guid userId, CancellationToken ct) =>
        await db.Users.AnyAsync(u => u.Id == userId && u.IsActive, ct);

    private async Task<Guid?> ValidAssigneeAsync(Guid? assigneeId, CancellationToken ct)
    {
        if (assigneeId is not { } id)
        {
            return null;
        }

        if (!await IsValidAssigneeAsync(id, ct))
        {
            throw new FieldValidationException("Исполнитель — действующий пользователь OneBase.");
        }

        return id;
    }

    /// <summary>Проект задачи: существует, не в архиве, того же отдела; null — без проекта.</summary>
    private async Task<WorkProject?> ValidProjectAsync(Guid departmentId, Guid? projectId, CancellationToken ct)
    {
        if (projectId is not { } id)
        {
            return null;
        }

        var project = await db.WorkProjects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new FieldValidationException("Проект не найден.");
        if (project.ArchivedAt is not null)
        {
            throw new FieldValidationException("Проект в архиве — новые задачи в нём не ставятся.");
        }

        if (project.DepartmentId != departmentId)
        {
            throw new FieldValidationException("Проект относится к другому отделу.");
        }

        return project;
    }

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

        if (input.StartDate is { } start && input.DueDate is { } end && end < start)
        {
            throw new FieldValidationException("Срок не может быть раньше даты начала.");
        }

        if (!Enum.IsDefined(input.Priority))
        {
            throw new FieldValidationException("Приоритет — низкий, обычный, высокий или критический.");
        }

        return (title, description);
    }

    // ---------- Создание и правка ----------

    /// <summary>
    /// Новая задача в отделе (и проекте). Ставит тот, кто видит отдел, руководит им или участвует в проекте. suggestionId — задача из
    /// предложения AI-диспетчера, подтверждённого руководителем.
    /// </summary>
    public async Task<Guid> CreateAsync(WorkContext ctx, string code, WorkTaskInput input, CancellationToken ct, Guid? suggestionId = null)
    {
        var department = await db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Code == code, ct) ?? throw new FieldNotFoundException("Отдел не найден.");
        var project = await ValidProjectAsync(department.Id, input.ProjectId, ct);
        if (!CanSeeDepartment(ctx, department.Id) && !(project is not null && ctx.InProject(project.Id)))
        {
            throw new FieldForbiddenException("Ставить задачи в этом отделе могут его сотрудники, руководитель отдела и руководство.");
        }

        var (title, description) = Validate(input);
        var assignee = await ValidAssigneeAsync(input.AssigneeId, ct);
        var task = new WorkTask
        {
            DepartmentId = department.Id,
            ProjectId = project?.Id,
            Title = title,
            Description = description,
            AssigneeId = assignee,
            CreatedById = ctx.UserId,
            Priority = input.Priority,
            StartDate = input.StartDate,
            DueDate = input.DueDate,
            SuggestionId = suggestionId,
        };
        db.WorkTasks.Add(task);
        Event(task.Id, ctx.UserId, "created", suggestionId is null ? "Задача создана" : "Задача создана из предложения AI-диспетчера");
        if (assignee is { } a && a != ctx.UserId)
        {
            notifier.Notify(a, UserNotificationKind.TaskAssigned, $"Новая задача: {title}", await AssignedTextAsync(ctx.UserId, department.Name, task.DueDate, ct), LinkOf(task.Id));
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "work.task.created", "work_task", task.Id.ToString(),
            new { department = department.Code, project = project?.Id, assignee, priority = task.Priority.ToString(), task.DueDate, suggestionId }, ct);
        return task.Id;
    }

    public async Task UpdateAsync(WorkContext ctx, Guid id, WorkTaskInput input, CancellationToken ct)
    {
        var (task, department) = await RequireTaskAsync(ctx, id, ct);
        if (!CanManage(ctx, task))
        {
            throw new FieldForbiddenException("Менять задачу может постановщик, руководитель отдела или проекта и руководство.");
        }

        if (task.ArchivedAt is not null)
        {
            throw new FieldValidationException("Задача в архиве — сначала верните её из архива.");
        }

        var (title, description) = Validate(input, task.DueDate);
        var project = input.ProjectId == task.ProjectId ? task.ProjectId : (await ValidProjectAsync(department.Id, input.ProjectId, ct))?.Id;
        // Прежнего исполнителя можно оставить, даже если он уже отключён; новый — действующий пользователь OneBase.
        var assignee = input.AssigneeId == task.AssigneeId ? task.AssigneeId : await ValidAssigneeAsync(input.AssigneeId, ct);
        var reassigned = assignee != task.AssigneeId;
        var dueChanged = input.DueDate != task.DueDate;
        var changes = new List<string>();
        if (title != task.Title)
        {
            changes.Add("название");
        }

        if (description != task.Description)
        {
            changes.Add("описание");
        }

        if (input.Priority != task.Priority)
        {
            changes.Add($"приоритет: {PriorityText(task.Priority)} → {PriorityText(input.Priority)}");
        }

        if (input.StartDate != task.StartDate)
        {
            changes.Add($"начало: {DateText(task.StartDate)} → {DateText(input.StartDate)}");
        }

        if (project != task.ProjectId)
        {
            changes.Add("проект");
        }

        var previousAssignee = task.AssigneeId;
        var previousDue = task.DueDate;
        task.Title = title;
        task.Description = description;
        task.Priority = input.Priority;
        task.StartDate = input.StartDate;
        task.DueDate = input.DueDate;
        task.ProjectId = project;
        task.AssigneeId = assignee;
        task.UpdatedAt = DateTimeOffset.UtcNow;

        if (changes.Count > 0)
        {
            Event(task.Id, ctx.UserId, "updated", "Изменено: " + string.Join("; ", changes));
        }

        if (dueChanged)
        {
            Event(task.Id, ctx.UserId, "due", $"Срок: {DateText(previousDue)} → {DateText(task.DueDate)}");
        }

        if (reassigned)
        {
            var names = await db.Users.AsNoTracking().Where(u => u.Id == previousAssignee || u.Id == assignee).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
            Event(task.Id, ctx.UserId, "assigned",
                $"Исполнитель: {(previousAssignee is { } p ? names.GetValueOrDefault(p) ?? "—" : "любой сотрудник")} → {(assignee is { } n ? names.GetValueOrDefault(n) ?? "—" : "любой сотрудник")}");
        }

        // Отключённому исполнителю не пишем.
        if (assignee is { } a && a != ctx.UserId)
        {
            if (reassigned)
            {
                notifier.Notify(a, UserNotificationKind.TaskAssigned, $"Новая задача: {title}", await AssignedTextAsync(ctx.UserId, department.Name, task.DueDate, ct), LinkOf(task.Id));
            }
            else if (dueChanged)
            {
                notifier.Notify(a, UserNotificationKind.TaskDeadlineChanged, $"Перенесён срок: {title}", $"Срок: {DateText(previousDue)} → {DateText(task.DueDate)}", LinkOf(task.Id));
            }
            else if (changes.Count > 0 && await IsValidAssigneeAsync(a, ct))
            {
                notifier.Notify(a, UserNotificationKind.TaskChanged, $"Задача изменена: {title}", DueText(task.DueDate), LinkOf(task.Id));
            }
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "work.task.updated", "work_task", task.Id.ToString(),
            new { assignee, priority = task.Priority.ToString(), task.DueDate, task.StartDate, project }, ct);
    }

    // ---------- Статусы ----------

    public async Task ChangeStatusAsync(WorkContext ctx, Guid id, WorkTaskStatusInput input, CancellationToken ct)
    {
        var (task, _) = await RequireTaskAsync(ctx, id, ct);
        if (task.ArchivedAt is not null)
        {
            throw new FieldValidationException("Задача в архиве — сначала верните её из архива.");
        }

        var manage = CanManage(ctx, task);
        var work = CanWork(ctx, task);
        if (!CanTransition(task.Status, input.Status, work, manage))
        {
            throw new FieldValidationException(!work && !manage
                ? "Вести задачу может исполнитель, а принимать — постановщик или руководитель."
                : "Так изменить статус задачи нельзя.");
        }

        var comment = string.IsNullOrWhiteSpace(input.Comment) ? null : input.Comment.Trim();
        var returning = task.Status == WorkTaskStatus.Review && input.Status == WorkTaskStatus.InProgress;
        if (returning && comment is null)
        {
            throw new FieldValidationException("Напишите, что доработать, — исполнитель увидит это в задаче и в уведомлении.");
        }

        if (comment is { Length: > 4000 })
        {
            throw new FieldValidationException("Комментарий — до 4000 символов.");
        }

        var from = task.Status;
        task.Status = input.Status;
        task.UpdatedAt = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(input.Result))
        {
            var result = input.Result.Trim();
            task.Result = result.Length <= 2000 ? result : result[..2000];
        }

        // Задача без исполнителя: кто взял в работу или отправил на проверку, тот и исполнитель.
        if (task.AssigneeId is null && input.Status is WorkTaskStatus.InProgress or WorkTaskStatus.Review && !manage)
        {
            task.AssigneeId = ctx.UserId;
        }

        if (comment is not null)
        {
            db.WorkTaskComments.Add(new WorkTaskComment { TaskId = task.Id, AuthorId = ctx.UserId, Text = comment });
        }

        var link = LinkOf(task.Id);
        switch (input.Status)
        {
            case WorkTaskStatus.Review:
                task.ReviewRequestedAt = DateTimeOffset.UtcNow;
                Event(task.Id, ctx.UserId, "review", "Отправлена на проверку");
                if (task.CreatedById is { } author && author != ctx.UserId)
                {
                    notifier.Notify(author, UserNotificationKind.TaskReview, $"На проверку: {task.Title}", comment ?? task.Result, link);
                }

                break;
            case WorkTaskStatus.Done:
                task.CompletedAt = DateTimeOffset.UtcNow;
                task.Progress = 100;
                task.IsBlocked = false;
                task.BlockedReason = null;
                Event(task.Id, ctx.UserId, "accepted", from == WorkTaskStatus.Review ? "Результат принят, задача выполнена" : "Задача выполнена");
                if (task.AssigneeId is { } doer && doer != ctx.UserId)
                {
                    notifier.Notify(doer, UserNotificationKind.TaskAccepted, $"Результат принят: {task.Title}", comment ?? task.Result, link);
                }

                if (task.CreatedById is { } creator && creator != ctx.UserId && creator != task.AssigneeId)
                {
                    notifier.Notify(creator, UserNotificationKind.TaskDone, $"Выполнена задача: {task.Title}", task.Result, link);
                }

                break;
            case WorkTaskStatus.Cancelled:
                Event(task.Id, ctx.UserId, "status", "Задача отменена" + (comment is null ? "" : $": {comment}"));
                if (task.AssigneeId is { } a && a != ctx.UserId)
                {
                    notifier.Notify(a, UserNotificationKind.TaskChanged, $"Задача отменена: {task.Title}", comment ?? task.Result, link);
                }

                break;
            case WorkTaskStatus.InProgress when returning:
                Event(task.Id, ctx.UserId, "returned", $"Возвращена на доработку: {comment}");
                if (task.AssigneeId is { } back && back != ctx.UserId)
                {
                    notifier.Notify(back, UserNotificationKind.TaskReturned, $"На доработку: {task.Title}", comment, link);
                }

                break;
            case WorkTaskStatus.InProgress when from is WorkTaskStatus.Done or WorkTaskStatus.Cancelled:
                task.CompletedAt = null;
                Event(task.Id, ctx.UserId, "status", "Возвращена в работу");
                if (task.AssigneeId is { } again && again != ctx.UserId)
                {
                    notifier.Notify(again, UserNotificationKind.TaskChanged, $"Задача возвращена в работу: {task.Title}", comment ?? task.Result, link);
                }

                break;
            default:
                Event(task.Id, ctx.UserId, "status", $"Статус: {StatusText(from)} → {StatusText(task.Status)}");
                break;
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "work.task.status", "work_task", task.Id.ToString(),
            new { from = from.ToString(), to = task.Status.ToString() }, ct);
    }

    /// <summary>В архив — только выполненная или отменённая; удаления нет, из архива задача возвращается.</summary>
    public async Task ArchiveAsync(WorkContext ctx, Guid id, bool archive, CancellationToken ct)
    {
        var (task, _) = await RequireTaskAsync(ctx, id, ct);
        if (!CanManage(ctx, task))
        {
            throw new FieldForbiddenException("Архивировать задачу может постановщик, руководитель отдела или проекта и руководство.");
        }

        if (archive)
        {
            if (task.ArchivedAt is not null)
            {
                return;
            }

            if (!Closed.Contains(task.Status))
            {
                throw new FieldValidationException("В архив отправляется выполненная или отменённая задача.");
            }

            task.ArchivedAt = DateTimeOffset.UtcNow;
            task.ArchivedById = ctx.UserId;
            Event(task.Id, ctx.UserId, "archived", "Отправлена в архив");
        }
        else
        {
            if (task.ArchivedAt is null)
            {
                return;
            }

            task.ArchivedAt = null;
            task.ArchivedById = null;
            Event(task.Id, ctx.UserId, "restored", "Возвращена из архива");
        }

        task.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), archive ? "work.task.archived" : "work.task.restored", "work_task", task.Id.ToString(), null, ct);
    }

    /// <summary>Блокировка — признак «работа стоит» с причиной; снимается так же. Видна на доске и в таблице.</summary>
    public async Task SetBlockedAsync(WorkContext ctx, Guid id, bool blocked, string? reason, CancellationToken ct)
    {
        var (task, _) = await RequireTaskAsync(ctx, id, ct);
        RequireWork(ctx, task);
        var text = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (blocked && text is null)
        {
            throw new FieldValidationException("Укажите, что мешает работе: кого или что ждёт задача.");
        }

        if (text is { Length: > 500 })
        {
            throw new FieldValidationException("Причина — до 500 символов.");
        }

        if (blocked && !Open.Contains(task.Status))
        {
            throw new FieldValidationException("Заблокировать можно только открытую задачу.");
        }

        task.IsBlocked = blocked;
        task.BlockedReason = blocked ? text : null;
        task.UpdatedAt = DateTimeOffset.UtcNow;
        Event(task.Id, ctx.UserId, "blocked", blocked ? $"Работа заблокирована: {text}" : "Блокировка снята");
        if (blocked && task.CreatedById is { } author && author != ctx.UserId)
        {
            notifier.Notify(author, UserNotificationKind.TaskChanged, $"Задача заблокирована: {task.Title}", text, LinkOf(task.Id));
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Процент выполнения вручную — только без чек-листа (с чек-листом он считается по пунктам).</summary>
    public async Task SetProgressAsync(WorkContext ctx, Guid id, int progress, CancellationToken ct)
    {
        var (task, _) = await RequireTaskAsync(ctx, id, ct);
        RequireWork(ctx, task);
        if (progress is < 0 or > 100)
        {
            throw new FieldValidationException("Процент выполнения — от 0 до 100.");
        }

        if (await db.WorkTaskChecklist.AnyAsync(c => c.TaskId == id, ct))
        {
            throw new FieldValidationException("У задачи есть чек-лист — процент считается по выполненным пунктам.");
        }

        if (!Open.Contains(task.Status))
        {
            throw new FieldValidationException("Процент меняется у открытой задачи.");
        }

        if (task.Progress == progress)
        {
            return;
        }

        Event(task.Id, ctx.UserId, "progress", $"Выполнение: {task.Progress}% → {progress}%");
        task.Progress = progress;
        task.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static void RequireWork(WorkContext ctx, WorkTask task)
    {
        if (task.ArchivedAt is not null)
        {
            throw new FieldValidationException("Задача в архиве — сначала верните её из архива.");
        }

        if (!CanWork(ctx, task))
        {
            throw new FieldForbiddenException("Вести задачу может исполнитель, постановщик и руководитель.");
        }
    }

    // ---------- Тексты и история ----------

    private void Event(Guid taskId, Guid? actorId, string kind, string text) =>
        db.WorkTaskEvents.Add(new WorkTaskEvent { TaskId = taskId, ActorId = actorId, Kind = kind, Text = text.Length <= 1000 ? text : text[..1000] });

    public static string LinkOf(Guid taskId) => $"/tasks/task/{taskId}";

    private static string? DueText(DateOnly? due) => due is { } d ? $"Срок: {d:dd.MM.yyyy}" : null;

    /// <summary>Текст уведомления о назначении: кто поставил, отдел, срок — частями через «; » (так их переводит интерфейс).</summary>
    private async Task<string> AssignedTextAsync(Guid authorId, string department, DateOnly? due, CancellationToken ct)
    {
        var author = await db.Users.AsNoTracking().Where(u => u.Id == authorId).Select(u => string.IsNullOrWhiteSpace(u.FullName) ? u.Login : u.FullName).FirstOrDefaultAsync(ct);
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(author))
        {
            parts.Add($"Постановщик: {author}");
        }

        parts.Add($"Отдел: {department}");
        if (DueText(due) is { } dueText)
        {
            parts.Add(dueText);
        }

        return string.Join("; ", parts);
    }

    private static string DateText(DateOnly? date) => date is { } d ? d.ToString("dd.MM.yyyy") : "без даты";

    public static string StatusText(WorkTaskStatus status) => status switch
    {
        WorkTaskStatus.New => "новая",
        WorkTaskStatus.InProgress => "в работе",
        WorkTaskStatus.Review => "на проверке",
        WorkTaskStatus.Done => "выполнена",
        _ => "отменена",
    };

    public static string PriorityText(WorkTaskPriority priority) => priority switch
    {
        WorkTaskPriority.Low => "низкий",
        WorkTaskPriority.Medium => "обычный",
        WorkTaskPriority.High => "высокий",
        _ => "критический",
    };
}
