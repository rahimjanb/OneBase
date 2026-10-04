using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Work;

namespace OneBase.Api.Controllers;

/// <summary>
/// Раздел «Задачи» OneBase: отделы (кнопки) и задачи отделов. Доступ решает сервис по отделу пользователя и правам —
/// никогда не по параметрам запроса. Задачи полевой команды продаж — в /api/field/tasks.
/// </summary>
[ApiController]
[Authorize]
[FieldErrors]
[Route("api/work")]
public sealed class WorkController(WorkTaskService tasks) : ControllerBase
{
    private Task<WorkContext> ContextAsync(CancellationToken ct) =>
        tasks.ContextAsync(User.GetUserId(), User.FindAll(OneBaseClaims.Permission).Select(c => c.Value).ToHashSet(), ct);

    [HttpGet("departments")]
    public async Task<List<DepartmentView>> Departments(CancellationToken ct) => await tasks.DepartmentsAsync(await ContextAsync(ct), ct);

    [HttpGet("tasks")]
    public async Task<PageResult<WorkTaskRow>> List(
        [FromQuery] string department,
        [FromQuery] string? status,
        [FromQuery] bool mine = false,
        [FromQuery] bool createdByMe = false,
        [FromQuery] Guid? assigneeId = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        await tasks.ListAsync(await ContextAsync(ct), department, new WorkTaskQuery(status, mine, createdByMe, assigneeId, search, page, pageSize), ct);

    [HttpGet("assignees")]
    public async Task<List<WorkAssignee>> Assignees([FromQuery] string department, CancellationToken ct) =>
        await tasks.AssigneesAsync(await ContextAsync(ct), department, ct);

    public sealed record CreateInput(string Department, string? Title, string? Description, Guid? AssigneeId, Domain.Work.WorkTaskPriority Priority = Domain.Work.WorkTaskPriority.Medium, DateOnly? DueDate = null);

    [HttpPost("tasks")]
    public async Task<IActionResult> Create(CreateInput input, CancellationToken ct) =>
        Ok(new { id = await tasks.CreateAsync(await ContextAsync(ct), input.Department, new WorkTaskInput(input.Title, input.Description, input.AssigneeId, input.Priority, input.DueDate), ct) });

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
