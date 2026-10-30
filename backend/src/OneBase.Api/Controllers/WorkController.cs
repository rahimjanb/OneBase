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
    /// scope (all, my, department, archive, board), фильтры, сортировка и страницы. department, priority, status и assigneeId — одно значение
    /// или несколько через запятую (фильтры канбана и таблицы).
    /// </summary>
    [HttpGet("tasks")]
    public async Task<PageResult<WorkTaskRow>> List(
        [FromQuery] string? department,
        [FromQuery] string? status,
        [FromQuery] string? scope = null,
        [FromQuery] bool mine = false,
        [FromQuery] bool createdByMe = false,
        [FromQuery] string? assigneeId = null,
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
        var assignees = (assigneeId ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => Guid.TryParse(v, out var g) ? g : (Guid?)null).OfType<Guid>().Distinct().ToList();
        if (scope is null && department is not null)
        {
            return await tasks.ListAsync(ctx, department, new WorkTaskQuery(status, mine, createdByMe, assignees.Count > 0 ? assignees[0] : null, search, page, pageSize), ct);
        }

        return await tasks.SearchAsync(ctx, new WorkTaskSearch(scope ?? "all", department, projectId, status, priority, mine ? ctx.UserId : null, createdByMe,
            from, to, dateField, search, sort, desc, page, pageSize, assignees), ct);
    }

    [HttpGet("tasks/{id:guid}")]
    public async Task<WorkTaskDetail> Get(Guid id, CancellationToken ct) => await tasks.GetAsync(await ContextAsync(ct), id, ct);

    /// <summary>Кого можно упомянуть в комментарии через @: те, кто видит задачу.</summary>
    [HttpGet("tasks/{id:guid}/people")]
    public async Task<List<WorkAssignee>> People(Guid id, CancellationToken ct) => await tasks.PeopleAsync(await ContextAsync(ct), id, ct);

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

    public sealed record ChecklistOrderInput(IReadOnlyList<Guid>? Ids);

    [HttpPost("tasks/{id:guid}/checklist")]
    public async Task<IActionResult> AddChecklist(Guid id, ChecklistInput input, CancellationToken ct) =>
        Ok(new { id = await tasks.AddChecklistItemAsync(await ContextAsync(ct), id, input.Title, ct) });

    [HttpPut("tasks/{id:guid}/checklist/{itemId:guid}")]
    public async Task<IActionResult> UpdateChecklist(Guid id, Guid itemId, ChecklistInput input, CancellationToken ct)
    {
        await tasks.UpdateChecklistItemAsync(await ContextAsync(ct), id, itemId, input.Title, input.Done, ct);
        return NoContent();
    }

    /// <summary>Порядок пунктов чек-листа (перетаскивание): все пункты задачи в новом порядке.</summary>
    [HttpPost("tasks/{id:guid}/checklist/order")]
    public async Task<IActionResult> OrderChecklist(Guid id, ChecklistOrderInput input, CancellationToken ct)
    {
        await tasks.OrderChecklistAsync(await ContextAsync(ct), id, input.Ids ?? [], ct);
        return NoContent();
    }

    [HttpDelete("tasks/{id:guid}/checklist/{itemId:guid}")]
    public async Task<IActionResult> DeleteChecklist(Guid id, Guid itemId, CancellationToken ct)
    {
        await tasks.DeleteChecklistItemAsync(await ContextAsync(ct), id, itemId, ct);
        return NoContent();
    }

    /// <summary>Комментарий; Mentions — кого упомянули через @ (получат уведомление, если видят задачу).</summary>
    public sealed record CommentInput(string? Text, IReadOnlyList<Guid>? Mentions = null);

    [HttpPost("tasks/{id:guid}/comments")]
    public async Task<IActionResult> Comment(Guid id, CommentInput input, CancellationToken ct) =>
        Ok(new { id = await tasks.AddCommentAsync(await ContextAsync(ct), id, input.Text, ct, input.Mentions) });

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

/// <summary>
/// Уведомления пользователя OneBase: колокольчик в шапке, панель и экран «Уведомления». Список с фильтром (all, unread, tasks, ai, hr) и
/// курсором, счётчики для вкладок, прочитано / не прочитано, удаление, действия без перехода, разделы «Что присылать».
/// </summary>
[ApiController]
[Authorize]
[FieldErrors]
[Route("api/notifications")]
public sealed class NotificationsController(NotificationCenter center, WorkTaskService tasks) : ControllerBase
{
    public sealed record ActionInput(string Action, DateOnly? Due);

    public sealed record PreferencesInput(IReadOnlyList<string>? Muted);

    /// <summary>Контекст задач — только с правом «Задачи»: без него действий с задачами нет.</summary>
    private async Task<WorkContext?> WorkAsync(CancellationToken ct)
    {
        var permissions = User.FindAll(OneBaseClaims.Permission).Select(c => c.Value).ToHashSet();
        return permissions.Contains(OneBase.Application.Security.Permissions.WorkTasks) ? await tasks.ContextAsync(User.GetUserId(), permissions, ct) : null;
    }

    /// <summary>filter: all (по умолчанию), unread, tasks, ai, hr; cursor — nextCursor прошлой страницы; take — до 50 (по умолчанию 30).</summary>
    [HttpGet]
    public async Task<NotificationPage> List([FromQuery] string? filter, [FromQuery] string? cursor, [FromQuery] bool unread = false, [FromQuery] int take = 30,
        CancellationToken ct = default) =>
        await center.ListAsync(User.GetUserId(), unread ? "unread" : filter, cursor, take, await WorkAsync(ct), ct);

    /// <summary>Только счётчики — для колокольчика.</summary>
    [HttpGet("counts")]
    public Task<NotificationCounts> Counts(CancellationToken ct) => center.CountsAsync(User.GetUserId(), ct);

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> Read(Guid id, CancellationToken ct)
    {
        await center.ReadAsync(User.GetUserId(), id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/unread")]
    public async Task<IActionResult> Unread(Guid id, CancellationToken ct)
    {
        await center.UnreadAsync(User.GetUserId(), id, ct);
        return NoContent();
    }

    /// <summary>Прочитать все; category — только раздела (tasks, ai, hr…).</summary>
    [HttpPost("read-all")]
    public async Task<IActionResult> ReadAll([FromQuery] string? category, CancellationToken ct)
    {
        await center.ReadAllAsync(User.GetUserId(), category, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await center.DeleteAsync(User.GetUserId(), id, ct) > 0 ? NoContent() : NotFound(new { error = "Уведомление не найдено." });

    /// <summary>Действие без перехода: extend_due (due — новый срок задачи, yyyy-MM-dd).</summary>
    [HttpPost("{id:guid}/action")]
    public async Task<IActionResult> Act(Guid id, ActionInput input, CancellationToken ct)
    {
        await center.ActAsync(User.GetUserId(), id, input.Action, input.Due, await WorkAsync(ct), ct);
        return NoContent();
    }

    /// <summary>Что присылать: отключённые разделы (tasks, ai, hr, sales, system) и все разделы.</summary>
    [HttpGet("preferences")]
    public async Task<IActionResult> Preferences(CancellationToken ct) =>
        Ok(new { muted = await center.MutesAsync(User.GetUserId(), ct), categories = NotificationCatalog.Categories });

    [HttpPut("preferences")]
    public async Task<IActionResult> SavePreferences(PreferencesInput input, CancellationToken ct) =>
        Ok(new { muted = await center.SetMutesAsync(User.GetUserId(), input.Muted, ct), categories = NotificationCatalog.Categories });
}
