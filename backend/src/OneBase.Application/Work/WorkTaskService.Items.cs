using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Domain.Audit;
using OneBase.Domain.Work;

namespace OneBase.Application.Work;

/// <summary>Вложение к загрузке: имя, тип, размер и содержимое (поток читается один раз).</summary>
public sealed record WorkUpload(string FileName, string? ContentType, long SizeBytes, Stream Content);

/// <summary>Файл к скачиванию: тип отдаётся только безопасный (картинки, PDF, офис, текст), остальное — как двоичный файл.</summary>
public sealed record WorkDownload(string FileName, string ContentType, string ObjectKey);

public sealed record WorkOverview(
    int Open,
    int InProgress,
    int Review,
    int Overdue,
    int Blocked,
    int DueThisWeek,
    int DoneThisMonth,
    int MyOpen,
    int MyOverdue,
    int AwaitingMyReview,
    int PendingSuggestions,
    IReadOnlyList<WorkOverviewDepartment> Departments,
    IReadOnlyList<WorkTaskRow> MyUpcoming);

public sealed record WorkOverviewDepartment(string Code, string Name, int Open, int Overdue, int Review, int Blocked);

public sealed record WorkProjectView(
    Guid Id,
    string Name,
    string? Description,
    string DepartmentCode,
    string DepartmentName,
    bool Archived,
    bool CanManage,
    int OpenTasks,
    IReadOnlyList<WorkProjectMemberView> Members);

public sealed record WorkProjectMemberView(Guid UserId, string Name, bool IsManager);

public sealed record WorkProjectInput(string? Name, string? Description, string? Department, IReadOnlyList<WorkProjectMemberInput>? Members, bool Archived = false);

public sealed record WorkProjectMemberInput(Guid UserId, bool IsManager);

public sealed record WorkDepartmentHeadsView(string Code, string Name, IReadOnlyList<WorkAssignee> Heads);

public sealed record WorkUserOption(Guid Id, string Name, string? Position, string? DepartmentName);

public sealed record WorkReminderResult(int DueSoon, int Overdue);

public sealed partial class WorkTaskService
{
    /// <summary>Наибольшее вложение и число вложений задачи.</summary>
    public const long MaxAttachmentBytes = 20L * 1024 * 1024;

    public const int MaxAttachments = 30;

    private static readonly HashSet<string> SafeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/gif", "image/webp", "application/pdf", "text/plain", "text/csv",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "application/msword",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation", "application/zip",
    };

    // ---------- Чек-лист ----------

    public async Task<Guid> AddChecklistItemAsync(WorkContext ctx, Guid taskId, string? title, CancellationToken ct)
    {
        var (task, _) = await RequireTaskAsync(ctx, taskId, ct);
        RequireWork(ctx, task);
        var text = ValidItem(title);
        var count = await db.WorkTaskChecklist.CountAsync(c => c.TaskId == taskId, ct);
        if (count >= 100)
        {
            throw new FieldValidationException("В чек-листе — до 100 пунктов.");
        }

        var item = new WorkTaskChecklistItem { TaskId = taskId, Title = text, SortOrder = count };
        db.WorkTaskChecklist.Add(item);
        Event(taskId, ctx.UserId, "checklist", $"Пункт чек-листа: {text}");
        await db.SaveChangesAsync(ct);
        await RecountAsync(task, ct);
        return item.Id;
    }

    public async Task UpdateChecklistItemAsync(WorkContext ctx, Guid taskId, Guid itemId, string? title, bool? done, CancellationToken ct)
    {
        var (task, _) = await RequireTaskAsync(ctx, taskId, ct);
        RequireWork(ctx, task);
        var item = await db.WorkTaskChecklist.FirstOrDefaultAsync(c => c.Id == itemId && c.TaskId == taskId, ct) ?? throw new FieldNotFoundException("Пункт не найден.");
        if (title is not null)
        {
            item.Title = ValidItem(title);
        }

        if (done is { } d && d != item.IsDone)
        {
            item.IsDone = d;
            item.DoneAt = d ? DateTimeOffset.UtcNow : null;
            item.DoneById = d ? ctx.UserId : null;
            Event(taskId, ctx.UserId, "checklist", $"{(d ? "Выполнен пункт" : "Снята отметка")}: {item.Title}");
        }

        item.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await RecountAsync(task, ct);
    }

    /// <summary>Новый порядок пунктов (перетаскивание): список — все пункты задачи, каждый один раз.</summary>
    public async Task OrderChecklistAsync(WorkContext ctx, Guid taskId, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var (task, _) = await RequireTaskAsync(ctx, taskId, ct);
        RequireWork(ctx, task);
        var items = await db.WorkTaskChecklist.Where(c => c.TaskId == taskId).ToListAsync(ct);
        if (ids.Count != items.Count || ids.Distinct().Count() != ids.Count || items.Any(i => !ids.Contains(i.Id)))
        {
            throw new FieldValidationException("Чек-лист изменился — обновите страницу и повторите.");
        }

        for (var i = 0; i < ids.Count; i++)
        {
            var item = items.First(x => x.Id == ids[i]);
            if (item.SortOrder != i)
            {
                item.SortOrder = i;
                item.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteChecklistItemAsync(WorkContext ctx, Guid taskId, Guid itemId, CancellationToken ct)
    {
        var (task, _) = await RequireTaskAsync(ctx, taskId, ct);
        RequireWork(ctx, task);
        var item = await db.WorkTaskChecklist.FirstOrDefaultAsync(c => c.Id == itemId && c.TaskId == taskId, ct) ?? throw new FieldNotFoundException("Пункт не найден.");
        db.WorkTaskChecklist.Remove(item);
        Event(taskId, ctx.UserId, "checklist", $"Удалён пункт: {item.Title}");
        await db.SaveChangesAsync(ct);
        await RecountAsync(task, ct);
    }

    /// <summary>С чек-листом процент — доля выполненных пунктов (у выполненной задачи — 100).</summary>
    private async Task RecountAsync(WorkTask task, CancellationToken ct)
    {
        var items = await db.WorkTaskChecklist.Where(c => c.TaskId == task.Id).Select(c => c.IsDone).ToListAsync(ct);
        var progress = task.Status == WorkTaskStatus.Done ? 100 : items.Count == 0 ? task.Progress : (int)Math.Round(100.0 * items.Count(d => d) / items.Count);
        if (progress != task.Progress)
        {
            task.Progress = progress;
            task.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    private static string ValidItem(string? title)
    {
        var text = (title ?? string.Empty).Trim();
        if (text.Length is 0 or > 300)
        {
            throw new FieldValidationException("Пункт чек-листа — от 1 до 300 символов.");
        }

        return text;
    }

    // ---------- Комментарии ----------

    /// <summary>
    /// Комментарий. Упомянутые через @ (mentions) получают уведомление «Упоминание» — только те, кто видит задачу (PeopleAsync): по @ не
    /// узнать о чужой задаче. Исполнитель и постановщик получают «Комментарий», если их не упомянули (одно уведомление на человека).
    /// </summary>
    public async Task<Guid> AddCommentAsync(WorkContext ctx, Guid taskId, string? text, CancellationToken ct, IReadOnlyList<Guid>? mentions = null)
    {
        var (task, _) = await RequireTaskAsync(ctx, taskId, ct);
        var body = (text ?? string.Empty).Trim();
        if (body.Length is 0 or > 4000)
        {
            throw new FieldValidationException("Комментарий — от 1 до 4000 символов.");
        }

        var comment = new WorkTaskComment { TaskId = taskId, AuthorId = ctx.UserId, Text = body };
        db.WorkTaskComments.Add(comment);
        var preview = body.Length <= 300 ? body : body[..300] + "…";
        var mentioned = new HashSet<Guid>();
        if (mentions is { Count: > 0 })
        {
            var wanted = mentions.Where(m => m != ctx.UserId).Distinct().Take(20).ToHashSet();
            if (wanted.Count > 0)
            {
                var author = await db.Users.AsNoTracking().Where(u => u.Id == ctx.UserId).Select(u => u.FullName).FirstOrDefaultAsync(ct);
                foreach (var person in (await PeopleOfAsync(task, ct)).Where(p => wanted.Contains(p.Id)))
                {
                    mentioned.Add(person.Id);
                    notifier.Notify(person.Id, UserNotificationKind.TaskMention, $"Упоминание: {task.Title}",
                        string.IsNullOrWhiteSpace(author) ? preview : $"{author}: {preview}", LinkOf(taskId));
                }
            }
        }

        foreach (var recipient in new[] { task.AssigneeId, task.CreatedById }.OfType<Guid>().Distinct().Where(r => r != ctx.UserId && !mentioned.Contains(r)))
        {
            notifier.Notify(recipient, UserNotificationKind.TaskComment, $"Комментарий: {task.Title}", preview, LinkOf(taskId));
        }

        await db.SaveChangesAsync(ct);
        return comment.Id;
    }

    /// <summary>Кого можно упомянуть в комментарии к задаче: те, кто её видит (для подсказки после @).</summary>
    public async Task<List<WorkAssignee>> PeopleAsync(WorkContext ctx, Guid taskId, CancellationToken ct)
    {
        var (task, _) = await RequireTaskAsync(ctx, taskId, ct);
        return (await PeopleOfAsync(task, ct)).Where(p => p.Id != ctx.UserId).ToList();
    }

    /// <summary>
    /// Действующие пользователи с разделом «Задачи», которые видят задачу (те же правила, что CanView): руководство, сотрудники и
    /// руководители отдела задачи, участники её проекта, исполнитель и постановщик. InDepartment — сотрудник отдела задачи.
    /// </summary>
    private async Task<List<WorkAssignee>> PeopleOfAsync(WorkTask task, CancellationToken ct)
    {
        var heads = await db.WorkDepartmentHeads.AsNoTracking().Where(h => h.DepartmentId == task.DepartmentId).Select(h => h.UserId).ToListAsync(ct);
        var members = task.ProjectId is { } project
            ? await db.WorkProjectMembers.AsNoTracking().Where(m => m.ProjectId == project && m.Project!.ArchivedAt == null).Select(m => m.UserId).ToListAsync(ct)
            : [];
        var users = await db.Users.AsNoTracking()
            .Where(u => u.IsActive)
            .Select(u => new
            {
                u.Id, u.FullName, u.Login, u.Position, u.DepartmentId, Department = u.Department != null ? u.Department.Name : null, u.Sections,
                Permissions = u.Roles.SelectMany(r => r.Role.Permissions.Select(p => p.Code)).ToList(),
            })
            .ToListAsync(ct);
        return users
            .Where(u => OneBase.Application.Security.UserSections.Effective(u.Permissions, u.Sections).Contains(OneBase.Application.Security.Permissions.WorkTasks))
            .Where(u => u.Permissions.Contains(OneBase.Application.Security.Permissions.UsersManage) || u.DepartmentId == task.DepartmentId || heads.Contains(u.Id)
                        || members.Contains(u.Id) || u.Id == task.AssigneeId || u.Id == task.CreatedById)
            .Select(u => new WorkAssignee(u.Id, string.IsNullOrWhiteSpace(u.FullName) ? u.Login : u.FullName, u.Position, u.Department, u.DepartmentId == task.DepartmentId))
            .OrderByDescending(a => a.InDepartment)
            .ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    // ---------- Вложения ----------

    /// <summary>Вложение в хранилище файлов (MinIO, ключ tasks/{задача}/{вложение}); описание — в базе. Загружает тот, кто ведёт задачу.</summary>
    public async Task<Guid> AttachAsync(WorkContext ctx, Guid taskId, WorkUpload upload, IFileStorage storage, CancellationToken ct)
    {
        var (task, _) = await RequireTaskAsync(ctx, taskId, ct);
        RequireWork(ctx, task);
        if (upload.SizeBytes <= 0)
        {
            throw new FieldValidationException("Файл пустой.");
        }

        if (upload.SizeBytes > MaxAttachmentBytes)
        {
            throw new FieldValidationException($"Файл больше {MaxAttachmentBytes / 1024 / 1024} МБ.");
        }

        if (await db.WorkTaskAttachments.CountAsync(a => a.TaskId == taskId, ct) >= MaxAttachments)
        {
            throw new FieldValidationException($"К задаче — до {MaxAttachments} вложений.");
        }

        var name = SafeFileName(upload.FileName);
        var contentType = string.IsNullOrWhiteSpace(upload.ContentType) ? "application/octet-stream" : upload.ContentType.Trim();
        var attachment = new WorkTaskAttachment
        {
            TaskId = taskId,
            FileName = name,
            ContentType = contentType.Length <= 200 ? contentType : "application/octet-stream",
            SizeBytes = upload.SizeBytes,
            ObjectKey = string.Empty,
            UploadedById = ctx.UserId,
        };
        attachment.ObjectKey = $"tasks/{taskId:N}/{attachment.Id:N}";
        await storage.PutAsync(attachment.ObjectKey, upload.Content, upload.SizeBytes, attachment.ContentType, ct);
        db.WorkTaskAttachments.Add(attachment);
        Event(taskId, ctx.UserId, "attachment", $"Вложение: {name}");
        await db.SaveChangesAsync(ct);
        return attachment.Id;
    }

    public async Task<WorkDownload> DownloadAsync(WorkContext ctx, Guid attachmentId, CancellationToken ct)
    {
        var a = await db.WorkTaskAttachments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == attachmentId, ct) ?? throw new FieldNotFoundException("Файл не найден.");
        await RequireTaskAsync(ctx, a.TaskId, ct);
        return new WorkDownload(a.FileName, SafeTypes.Contains(a.ContentType) ? a.ContentType : "application/octet-stream", a.ObjectKey);
    }

    public async Task DeleteAttachmentAsync(WorkContext ctx, Guid attachmentId, IFileStorage storage, CancellationToken ct)
    {
        var a = await db.WorkTaskAttachments.FirstOrDefaultAsync(x => x.Id == attachmentId, ct) ?? throw new FieldNotFoundException("Файл не найден.");
        var (task, _) = await RequireTaskAsync(ctx, a.TaskId, ct);
        if (a.UploadedById != ctx.UserId && !CanManage(ctx, task))
        {
            throw new FieldForbiddenException("Удалить вложение может тот, кто его загрузил, или постановщик и руководитель.");
        }

        db.WorkTaskAttachments.Remove(a);
        Event(task.Id, ctx.UserId, "attachment", $"Удалено вложение: {a.FileName}");
        await db.SaveChangesAsync(ct);
        await storage.DeleteAsync(a.ObjectKey, ct);
    }

    /// <summary>Имя файла без пути и управляющих символов, до 255 символов.</summary>
    public static string SafeFileName(string? name)
    {
        var raw = Path.GetFileName((name ?? string.Empty).Replace('\\', '/').Split('/').Last());
        var clean = new string(raw.Where(c => !char.IsControl(c) && c is not ('"' or '<' or '>' or '|' or '*' or '?' or ':')).ToArray()).Trim();
        if (clean.Length == 0)
        {
            clean = "файл";
        }

        if (clean.Length > 255)
        {
            var ext = Path.GetExtension(clean);
            clean = clean[..(255 - Math.Min(ext.Length, 20))] + ext[..Math.Min(ext.Length, 20)];
        }

        return clean;
    }

    // ---------- Обзор ----------

    public async Task<WorkOverview> OverviewAsync(WorkContext ctx, int pendingSuggestions, CancellationToken ct)
    {
        var today = Today;
        var weekEnd = today.AddDays(7);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthStartUtc = new DateTimeOffset(monthStart.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(5)).ToUniversalTime();
        var active = Visible(ctx).Where(t => t.ArchivedAt == null);
        var open = active.Where(t => Open.Contains(t.Status));
        var me = ctx.UserId;
        var byDepartment = await open
            .GroupBy(t => new { t.Department!.Code, t.Department.Name })
            .Select(g => new WorkOverviewDepartment(g.Key.Code, g.Key.Name, g.Count(), g.Count(t => t.DueDate != null && t.DueDate < today),
                g.Count(t => t.Status == WorkTaskStatus.Review), g.Count(t => t.IsBlocked)))
            .ToListAsync(ct);
        var reviews = await open.Where(t => t.Status == WorkTaskStatus.Review).Select(t => new { t.DepartmentId, t.ProjectId, t.CreatedById }).ToListAsync(ct);
        var upcoming = await SearchAsync(ctx, new WorkTaskSearch("my", Status: "open", Sort: "due", PageSize: 8), ct);
        return new WorkOverview(
            await open.CountAsync(ct),
            await open.CountAsync(t => t.Status == WorkTaskStatus.InProgress, ct),
            reviews.Count,
            await open.CountAsync(t => t.DueDate != null && t.DueDate < today, ct),
            await open.CountAsync(t => t.IsBlocked, ct),
            await open.CountAsync(t => t.DueDate != null && t.DueDate >= today && t.DueDate <= weekEnd, ct),
            await Visible(ctx).CountAsync(t => t.Status == WorkTaskStatus.Done && t.CompletedAt >= monthStartUtc, ct),
            await open.CountAsync(t => t.AssigneeId == me, ct),
            await open.CountAsync(t => t.AssigneeId == me && t.DueDate != null && t.DueDate < today, ct),
            reviews.Count(r => r.CreatedById == me || ctx.Heads(r.DepartmentId) || ctx.ManagesProject(r.ProjectId)),
            pendingSuggestions,
            byDepartment.OrderBy(d => Array.IndexOf(Order, d.Code) is var i and >= 0 ? i : Order.Length).ToList(),
            upcoming.Items.Where(r => r.DueDate is not null).ToList());
    }

    // ---------- Проекты ----------

    public async Task<List<WorkProjectView>> ProjectsAsync(WorkContext ctx, bool includeArchived, CancellationToken ct)
    {
        var mine = (ctx.Projects ?? new Dictionary<Guid, bool>()).Keys.ToArray();
        var heads = (ctx.HeadOf ?? new HashSet<Guid>()).ToArray();
        var department = ctx.DepartmentId;
        var q = db.WorkProjects.AsNoTracking();
        if (!ctx.SeesAll)
        {
            q = q.Where(p => mine.Contains(p.Id) || heads.Contains(p.DepartmentId) || (department != null && p.DepartmentId == department));
        }

        if (!includeArchived)
        {
            q = q.Where(p => p.ArchivedAt == null);
        }

        var projects = await q.OrderBy(p => p.Name)
            .Select(p => new
            {
                p.Id, p.Name, p.Description, p.DepartmentId, Code = p.Department!.Code, Department = p.Department.Name, p.ArchivedAt,
                Members = p.Members.Select(m => new WorkProjectMemberView(m.UserId, m.User!.FullName, m.IsManager)).ToList(),
            })
            .ToListAsync(ct);
        var ids = projects.Select(p => p.Id).ToArray();
        var open = await db.WorkTasks.AsNoTracking().Where(t => t.ProjectId != null && ids.Contains(t.ProjectId.Value) && Open.Contains(t.Status) && t.ArchivedAt == null)
            .GroupBy(t => t.ProjectId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return projects.Select(p => new WorkProjectView(p.Id, p.Name, p.Description, p.Code, p.Department, p.ArchivedAt is not null,
            ctx.Heads(p.DepartmentId) || ctx.ManagesProject(p.Id), open.GetValueOrDefault(p.Id),
            p.Members.OrderByDescending(m => m.IsManager).ThenBy(m => m.Name).ToList())).ToList();
    }

    /// <summary>Создать проект: руководство или руководитель отдела-владельца.</summary>
    public async Task<Guid> CreateProjectAsync(WorkContext ctx, WorkProjectInput input, CancellationToken ct)
    {
        var department = await db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Code == input.Department, ct) ?? throw new FieldValidationException("Выберите отдел проекта.");
        if (!ctx.Heads(department.Id))
        {
            throw new FieldForbiddenException("Проект создаёт руководитель отдела или руководство.");
        }

        var project = new WorkProject { Name = ValidName(input.Name), Description = ValidDescription(input.Description), DepartmentId = department.Id, CreatedById = ctx.UserId };
        db.WorkProjects.Add(project);
        await SetMembersAsync(project, input.Members ?? [], ct);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "work.project.created", "work_project", project.Id.ToString(), new { department = department.Code }, ct);
        return project.Id;
    }

    /// <summary>Изменить проект и участников: руководство, руководитель отдела-владельца, руководитель проекта.</summary>
    public async Task UpdateProjectAsync(WorkContext ctx, Guid id, WorkProjectInput input, CancellationToken ct)
    {
        var project = await db.WorkProjects.Include(p => p.Members).FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new FieldNotFoundException("Проект не найден.");
        if (!ctx.Heads(project.DepartmentId) && !ctx.ManagesProject(project.Id))
        {
            throw new FieldForbiddenException("Проект меняет его руководитель, руководитель отдела или руководство.");
        }

        project.Name = ValidName(input.Name);
        project.Description = ValidDescription(input.Description);
        project.ArchivedAt = input.Archived ? project.ArchivedAt ?? DateTimeOffset.UtcNow : null;
        project.UpdatedAt = DateTimeOffset.UtcNow;
        if (input.Members is { } members)
        {
            // Руководитель проекта не может снять с себя руководство (иначе проект останется без управления) — это делает руководитель отдела.
            if (!ctx.Heads(project.DepartmentId) && !members.Any(m => m.UserId == ctx.UserId && m.IsManager))
            {
                throw new FieldValidationException("Нельзя снять себя с руководства проектом — это делает руководитель отдела.");
            }

            db.WorkProjectMembers.RemoveRange(project.Members);
            project.Members.Clear();
            await SetMembersAsync(project, members, ct);
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "work.project.updated", "work_project", project.Id.ToString(), new { input.Archived }, ct);
    }

    private async Task SetMembersAsync(WorkProject project, IReadOnlyList<WorkProjectMemberInput> members, CancellationToken ct)
    {
        var ids = members.Select(m => m.UserId).Distinct().ToList();
        if (ids.Count > 200)
        {
            throw new FieldValidationException("В проекте — до 200 участников.");
        }

        var active = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id) && u.IsActive).Select(u => u.Id).ToListAsync(ct);
        if (active.Count != ids.Count)
        {
            throw new FieldValidationException("Участники проекта — действующие пользователи OneBase.");
        }

        foreach (var m in members.GroupBy(m => m.UserId).Select(g => g.First()))
        {
            project.Members.Add(new WorkProjectMember { ProjectId = project.Id, UserId = m.UserId, IsManager = m.IsManager });
        }
    }

    private static string ValidName(string? name)
    {
        var text = (name ?? string.Empty).Trim();
        return text.Length is 0 or > 200 ? throw new FieldValidationException("Название проекта — от 1 до 200 символов.") : text;
    }

    private static string? ValidDescription(string? description)
    {
        var text = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        return text is { Length: > 2000 } ? throw new FieldValidationException("Описание проекта — до 2000 символов.") : text;
    }

    // ---------- Руководители отделов ----------

    public async Task<List<WorkDepartmentHeadsView>> HeadsAsync(CancellationToken ct)
    {
        var departments = await db.Departments.AsNoTracking().ToListAsync(ct);
        var heads = await db.WorkDepartmentHeads.AsNoTracking()
            .Select(h => new { h.DepartmentId, h.UserId, h.User!.FullName, h.User.Position }).ToListAsync(ct);
        return departments
            .OrderBy(d => Array.IndexOf(Order, d.Code) is var i and >= 0 ? i : Order.Length).ThenBy(d => d.Name)
            .Select(d => new WorkDepartmentHeadsView(d.Code, d.Name,
                heads.Where(h => h.DepartmentId == d.Id).OrderBy(h => h.FullName).Select(h => new WorkAssignee(h.UserId, h.FullName, h.Position)).ToList()))
            .ToList();
    }

    /// <summary>Назначить руководителей отдела — только руководство (право на пользователей).</summary>
    public async Task SetHeadsAsync(WorkContext ctx, string code, IReadOnlyList<Guid> userIds, CancellationToken ct)
    {
        if (!ctx.SeesAll)
        {
            throw new FieldForbiddenException("Руководителей отделов назначает руководство.");
        }

        var department = await db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Code == code, ct) ?? throw new FieldNotFoundException("Отдел не найден.");
        var ids = userIds.Distinct().ToList();
        if (await db.Users.CountAsync(u => ids.Contains(u.Id) && u.IsActive, ct) != ids.Count)
        {
            throw new FieldValidationException("Руководитель — действующий пользователь OneBase.");
        }

        var current = await db.WorkDepartmentHeads.Where(h => h.DepartmentId == department.Id).ToListAsync(ct);
        db.WorkDepartmentHeads.RemoveRange(current.Where(h => !ids.Contains(h.UserId)));
        foreach (var id in ids.Where(id => current.All(h => h.UserId != id)))
        {
            db.WorkDepartmentHeads.Add(new WorkDepartmentHead { DepartmentId = department.Id, UserId = id });
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "work.heads.updated", "department", department.Code, new { heads = ids }, ct);
    }

    /// <summary>Пользователи для выбора участников проекта и руководителей (действующие).</summary>
    public async Task<List<WorkUserOption>> UsersAsync(WorkContext ctx, CancellationToken ct)
    {
        if (!ctx.HeadsAny && ctx.Projects?.Values.Any(v => v) != true)
        {
            throw new FieldForbiddenException("Список сотрудников для проектов — у руководителей.");
        }

        return await db.Users.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.FullName)
            .Select(u => new WorkUserOption(u.Id, u.FullName, u.Position, u.Department == null ? null : u.Department.Name)).ToListAsync(ct);
    }

    // ---------- Напоминания о сроках ----------

    /// <summary>
    /// Напоминания: срок завтра — исполнителю; срок прошёл — исполнителю и постановщику. Одно уведомление на срок: отметка
    /// (DueSoonNotifiedFor, OverdueNotifiedFor) ставится условным обновлением в базе, поэтому повторный запуск, второй экземпляр API
    /// или обновление страницы второго уведомления не дают. Перенос срока — новое напоминание к новому сроку.
    /// </summary>
    public async Task<WorkReminderResult> RemindAsync(DateOnly today, CancellationToken ct)
    {
        var tomorrow = today.AddDays(1);
        var dueSoon = await db.WorkTasks.AsNoTracking()
            .Where(t => t.ArchivedAt == null && (t.Status == WorkTaskStatus.New || t.Status == WorkTaskStatus.InProgress) && t.AssigneeId != null
                        && t.DueDate == tomorrow && (t.DueSoonNotifiedFor == null || t.DueSoonNotifiedFor != t.DueDate))
            .Select(t => new { t.Id, t.Title, t.AssigneeId, t.DueDate })
            .Take(500)
            .ToListAsync(ct);
        var sentSoon = 0;
        foreach (var t in dueSoon)
        {
            var claimed = await db.WorkTasks.Where(x => x.Id == t.Id && x.DueDate == t.DueDate && (x.DueSoonNotifiedFor == null || x.DueSoonNotifiedFor != t.DueDate))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.DueSoonNotifiedFor, t.DueDate), ct);
            if (claimed == 1)
            {
                notifier.Notify(t.AssigneeId, UserNotificationKind.TaskDueSoon, $"Срок завтра: {t.Title}", DueText(t.DueDate), LinkOf(t.Id));
                sentSoon++;
            }
        }

        var overdue = await db.WorkTasks.AsNoTracking()
            .Where(t => t.ArchivedAt == null && Open.Contains(t.Status) && t.DueDate != null && t.DueDate < today
                        && (t.OverdueNotifiedFor == null || t.OverdueNotifiedFor != t.DueDate))
            .Select(t => new { t.Id, t.Title, t.AssigneeId, t.CreatedById, t.DueDate })
            .Take(500)
            .ToListAsync(ct);
        var sentOverdue = 0;
        foreach (var t in overdue)
        {
            var claimed = await db.WorkTasks.Where(x => x.Id == t.Id && x.DueDate == t.DueDate && (x.OverdueNotifiedFor == null || x.OverdueNotifiedFor != t.DueDate))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.OverdueNotifiedFor, t.DueDate), ct);
            if (claimed != 1)
            {
                continue;
            }

            foreach (var recipient in new[] { t.AssigneeId, t.CreatedById }.OfType<Guid>().Distinct())
            {
                notifier.Notify(recipient, UserNotificationKind.TaskOverdue, $"Просрочена задача: {t.Title}", DueText(t.DueDate), LinkOf(t.Id));
            }

            db.WorkTaskEvents.Add(new WorkTaskEvent { TaskId = t.Id, Kind = "overdue", Text = $"Срок {t.DueDate:dd.MM.yyyy} прошёл" });
            sentOverdue++;
        }

        await db.SaveChangesAsync(ct);
        return new WorkReminderResult(sentSoon, sentOverdue);
    }
}
