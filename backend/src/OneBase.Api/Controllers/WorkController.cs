using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Work;

namespace OneBase.Api.Controllers;

/// <summary>
/// Раздел «Задачи» OneBase: отделы, задачи отделов и проектов (таблица, канбан, карточка), чек-листы, комментарии, вложения, история,
/// проекты, руководители отделов и предложения AI-диспетчера. Доступ решает WorkTaskService по отделу, руководству, проектам и правам
/// пользователя — никогда по параметрам запроса. Сам раздел — с правом «Задачи» (раздел пользователя, UserSections). Задачи полевой
/// команды продаж — в /api/field/tasks.
/// </summary>
[ApiController]
[Authorize]
[HasPermission(OneBase.Application.Security.Permissions.WorkTasks)]
[FieldErrors]
[Route("api/work")]
public sealed class WorkController(WorkTaskService tasks, OneBase.AI.Work.TaskSuggestionService suggestions, IFileStorage storage) : ControllerBase
{
    private Task<WorkContext> ContextAsync(CancellationToken ct) =>
        tasks.ContextAsync(User.GetUserId(), User.FindAll(OneBaseClaims.Permission).Select(c => c.Value).ToHashSet(), ct);

    [HttpGet("departments")]
    public async Task<List<DepartmentView>> Departments(CancellationToken ct) => await tasks.DepartmentsAsync(await ContextAsync(ct), ct);

    /// <summary>Для кнопок раздела в верхней панели: видна ли вкладка «Предложения AI» (руководитель) и сколько их ждёт решения.</summary>
    [HttpGet("nav")]
    public async Task<IActionResult> Nav(CancellationToken ct)
    {
        var ctx = await ContextAsync(ct);
        return Ok(new { Heads = ctx.HeadsAny, PendingSuggestions = await suggestions.PendingCountAsync(ctx, ct) });
    }

    [HttpGet("overview")]
    public async Task<WorkOverview> Overview(CancellationToken ct)
    {
        var ctx = await ContextAsync(ct);
        return await tasks.OverviewAsync(ctx, await suggestions.PendingCountAsync(ctx, ct), ct);
    }

    /// <summary>
    /// Список задач. С department без scope — прежний список отдела (страница отдела, AI). Иначе — поиск по всем доступным задачам:
    /// scope (all, my, department, archive, board), фильтры, сортировка и страницы.
    /// </summary>
    [HttpGet("tasks")]
    public async Task<PageResult<WorkTaskRow>> List(
        [FromQuery] string? department,
        [FromQuery] string? status,
        [FromQuery] string? scope = null,
        [FromQuery] bool mine = false,
        [FromQuery] bool createdByMe = false,
        [FromQuery] Guid? assigneeId = null,
        [FromQuery] Guid? projectId = null,
        [FromQuery] string? priority = null,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] string? dateField = null,
        [FromQuery] string? search = null,
        [FromQuery] string? sort = null,
        [FromQuery] bool desc = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var ctx = await ContextAsync(ct);
        if (scope is null && department is not null)
        {
            return await tasks.ListAsync(ctx, department, new WorkTaskQuery(status, mine, createdByMe, assigneeId, search, page, pageSize), ct);
        }

        return await tasks.SearchAsync(ctx, new WorkTaskSearch(scope ?? "all", department, projectId, status, priority, mine ? ctx.UserId : assigneeId, createdByMe,
            from, to, dateField, search, sort, desc, page, pageSize), ct);
    }

    [HttpGet("tasks/{id:guid}")]
    public async Task<WorkTaskDetail> Get(Guid id, CancellationToken ct) => await tasks.GetAsync(await ContextAsync(ct), id, ct);

    [HttpGet("assignees")]
    public async Task<List<WorkAssignee>> Assignees([FromQuery] string department, [FromQuery] Guid? projectId, CancellationToken ct) =>
        await tasks.AssigneesAsync(await ContextAsync(ct), department, ct, projectId);

    public sealed record CreateInput(
        string Department,
        string? Title,
        string? Description,
        Guid? AssigneeId,
        Domain.Work.WorkTaskPriority Priority = Domain.Work.WorkTaskPriority.Medium,
        DateOnly? DueDate = null,
        DateOnly? StartDate = null,
        Guid? ProjectId = null);

    [HttpPost("tasks")]
    public async Task<IActionResult> Create(CreateInput input, CancellationToken ct) =>
        Ok(new
        {
            id = await tasks.CreateAsync(await ContextAsync(ct), input.Department,
                new WorkTaskInput(input.Title, input.Description, input.AssigneeId, input.Priority, input.DueDate, input.StartDate, input.ProjectId), ct),
        });

    [HttpPut("tasks/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, WorkTaskInput input, CancellationToken ct)
    {
        await tasks.UpdateAsync(await ContextAsync(ct), id, input, ct);
        return NoContent();
    }

    [HttpPost("tasks/{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, WorkTaskStatusInput input, CancellationToken ct)
    {
        await tasks.ChangeStatusAsync(await ContextAsync(ct), id, input, ct);
        return NoContent();
    }

    [HttpPost("tasks/{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        await tasks.ArchiveAsync(await ContextAsync(ct), id, true, ct);
        return NoContent();
    }

    [HttpPost("tasks/{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct)
    {
        await tasks.ArchiveAsync(await ContextAsync(ct), id, false, ct);
        return NoContent();
    }

    public sealed record BlockInput(bool Blocked, string? Reason);

    [HttpPost("tasks/{id:guid}/block")]
    public async Task<IActionResult> Block(Guid id, BlockInput input, CancellationToken ct)
    {
        await tasks.SetBlockedAsync(await ContextAsync(ct), id, input.Blocked, input.Reason, ct);
        return NoContent();
    }

    public sealed record ProgressInput(int Progress);

    [HttpPost("tasks/{id:guid}/progress")]
    public async Task<IActionResult> Progress(Guid id, ProgressInput input, CancellationToken ct)
    {
        await tasks.SetProgressAsync(await ContextAsync(ct), id, input.Progress, ct);
        return NoContent();
    }

    public sealed record ChecklistInput(string? Title, bool? Done);

    [HttpPost("tasks/{id:guid}/checklist")]
    public async Task<IActionResult> AddChecklist(Guid id, ChecklistInput input, CancellationToken ct) =>
        Ok(new { id = await tasks.AddChecklistItemAsync(await ContextAsync(ct), id, input.Title, ct) });

    [HttpPut("tasks/{id:guid}/checklist/{itemId:guid}")]
    public async Task<IActionResult> UpdateChecklist(Guid id, Guid itemId, ChecklistInput input, CancellationToken ct)
    {
        await tasks.UpdateChecklistItemAsync(await ContextAsync(ct), id, itemId, input.Title, input.Done, ct);
        return NoContent();
    }

    [HttpDelete("tasks/{id:guid}/checklist/{itemId:guid}")]
    public async Task<IActionResult> DeleteChecklist(Guid id, Guid itemId, CancellationToken ct)
    {
        await tasks.DeleteChecklistItemAsync(await ContextAsync(ct), id, itemId, ct);
        return NoContent();
    }

    public sealed record CommentInput(string? Text);

    [HttpPost("tasks/{id:guid}/comments")]
    public async Task<IActionResult> Comment(Guid id, CommentInput input, CancellationToken ct) =>
        Ok(new { id = await tasks.AddCommentAsync(await ContextAsync(ct), id, input.Text, ct) });

    [HttpPost("tasks/{id:guid}/attachments")]
    [RequestSizeLimit(WorkTaskService.MaxAttachmentBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = WorkTaskService.MaxAttachmentBytes + 1024 * 1024)]
    public async Task<IActionResult> Attach(Guid id, IFormFile? file, CancellationToken ct)
    {
        if (file is null)
        {
            throw new FieldValidationException("Выберите файл.");
        }

        await using var stream = file.OpenReadStream();
        return Ok(new { id = await tasks.AttachAsync(await ContextAsync(ct), id, new WorkUpload(file.FileName, file.ContentType, file.Length, stream), storage, ct) });
    }

    /// <summary>Скачивание вложения — только тем, кто видит задачу; всегда как файл (не открывается в браузере как страница).</summary>
    [HttpGet("attachments/{id:guid}")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var file = await tasks.DownloadAsync(await ContextAsync(ct), id, ct);
        var buffer = new MemoryStream();
        await storage.DownloadAsync(file.ObjectKey, buffer, ct);
        buffer.Position = 0;
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "private, no-store";
        return File(buffer, file.ContentType, file.FileName);
    }

    [HttpDelete("attachments/{id:guid}")]
    public async Task<IActionResult> DeleteAttachment(Guid id, CancellationToken ct)
    {
        await tasks.DeleteAttachmentAsync(await ContextAsync(ct), id, storage, ct);
        return NoContent();
    }

    // ---------- Проекты и руководители ----------

    [HttpGet("projects")]
    public async Task<List<WorkProjectView>> Projects([FromQuery] bool archived = false, CancellationToken ct = default) =>
        await tasks.ProjectsAsync(await ContextAsync(ct), archived, ct);

    [HttpPost("projects")]
    public async Task<IActionResult> CreateProject(WorkProjectInput input, CancellationToken ct) =>
        Ok(new { id = await tasks.CreateProjectAsync(await ContextAsync(ct), input, ct) });

    [HttpPut("projects/{id:guid}")]
    public async Task<IActionResult> UpdateProject(Guid id, WorkProjectInput input, CancellationToken ct)
    {
        await tasks.UpdateProjectAsync(await ContextAsync(ct), id, input, ct);
        return NoContent();
    }

    [HttpGet("heads")]
    public async Task<List<WorkDepartmentHeadsView>> Heads(CancellationToken ct) => await tasks.HeadsAsync(ct);

    public sealed record HeadsInput(IReadOnlyList<Guid> UserIds);

    [HttpPut("departments/{code}/heads")]
    public async Task<IActionResult> SetHeads(string code, HeadsInput input, CancellationToken ct)
    {
        await tasks.SetHeadsAsync(await ContextAsync(ct), code, input.UserIds ?? [], ct);
        return NoContent();
    }

    [HttpGet("users")]
    public async Task<List<WorkUserOption>> Users(CancellationToken ct) => await tasks.UsersAsync(await ContextAsync(ct), ct);

    // ---------- AI-диспетчер ----------

    [HttpGet("suggestions")]
    public async Task<List<OneBase.AI.Work.TaskSuggestionView>> Suggestions([FromQuery] Domain.Work.WorkSuggestionStatus? status, CancellationToken ct) =>
        await suggestions.ListAsync(await ContextAsync(ct), status, ct);

    /// <summary>Найти поводы в данных и предложить задачи (ничего не создаёт — предложения ждут подтверждения).</summary>
    [HttpPost("suggestions/generate")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("ai")]
    public async Task<OneBase.AI.Work.TaskSuggestionRun> Generate(CancellationToken ct) => await suggestions.GenerateAsync(await ContextAsync(ct), ct);

    [HttpPut("suggestions/{id:guid}")]
    public async Task<IActionResult> EditSuggestion(Guid id, OneBase.AI.Work.TaskSuggestionEdit input, CancellationToken ct)
    {
        await suggestions.EditAsync(await ContextAsync(ct), id, input, ct);
        return NoContent();
    }

    [HttpPost("suggestions/{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, OneBase.AI.Work.TaskSuggestionEdit? input, CancellationToken ct) =>
        Ok(new { taskId = await suggestions.AcceptAsync(await ContextAsync(ct), id, input, ct) });

    public sealed record RejectInput(string? Note);

    [HttpPost("suggestions/{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, RejectInput input, CancellationToken ct)
    {
        await suggestions.RejectAsync(await ContextAsync(ct), id, input.Note, ct);
        return NoContent();
    }
}

/// <summary>Уведомления пользователя OneBase: колокольчик в шапке.</summary>
[ApiController]
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController(IAppDbContext db) : ControllerBase
{
    public sealed record Row(Guid Id, Domain.Work.UserNotificationKind Kind, string Title, string? Body, string? Link, DateTimeOffset CreatedAt, bool Read);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool unread = false, [FromQuery] int take = 30, CancellationToken ct = default)
    {
        var me = User.GetUserId();
        var q = db.UserNotifications.AsNoTracking().Where(n => n.RecipientId == me);
        if (unread)
        {
            q = q.Where(n => n.ReadAt == null);
        }

        var items = await q.OrderByDescending(n => n.CreatedAt).Take(Math.Clamp(take, 1, 100))
            .Select(n => new Row(n.Id, n.Kind, n.Title, n.Body, n.Link, n.CreatedAt, n.ReadAt != null))
            .ToListAsync(ct);
        var count = await db.UserNotifications.CountAsync(n => n.RecipientId == me && n.ReadAt == null, ct);
        return Ok(new { items, unread = count });
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> Read(Guid id, CancellationToken ct)
    {
        var me = User.GetUserId();
        await db.UserNotifications.Where(n => n.Id == id && n.RecipientId == me && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTimeOffset.UtcNow), ct);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> ReadAll(CancellationToken ct)
    {
        var me = User.GetUserId();
        await db.UserNotifications.Where(n => n.RecipientId == me && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTimeOffset.UtcNow), ct);
        return NoContent();
    }
}
