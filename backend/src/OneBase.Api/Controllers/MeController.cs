using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneBase.AI.Work;
using OneBase.Api.Auth;
using OneBase.Application.Field;
using OneBase.Application.Hr;
using OneBase.Application.Security;
using OneBase.Application.Work;
using OneBase.Infrastructure.Files;

namespace OneBase.Api.Controllers;

/// <summary>
/// «Мой день»: данные только текущего пользователя — он берётся из токена, параметров userId или employeeId нет. Каждый блок — отдельный
/// запрос (страница грузит их параллельно, ошибка одного не ломает остальные); блок без права раздела отвечает available: false.
/// </summary>
[ApiController]
[Authorize]
[FieldErrors]
[Route("api/me")]
public sealed class MeController(HrService hr, WorkTaskService tasks, CalendarService calendar, DepartmentFilesService files, DailyPlanService plans)
    : ControllerBase
{
    public const int DocumentsTake = 5;

    /// <summary>Сегодня и 7 дней вперёд — календарь «Моего дня».</summary>
    public const int CalendarDays = 7;

    private HashSet<string> MyPermissions() => User.FindAll(OneBaseClaims.Permission).Select(c => c.Value).ToHashSet();

    private Task<WorkContext> ContextAsync(CancellationToken ct) => tasks.ContextAsync(User.GetUserId(), MyPermissions(), ct);

    /// <summary>Рабочий день и неделя из Verifix — по привязке пользователя к сотруднику.</summary>
    [HttpGet("workday")]
    public async Task<HrMyWorkday> Workday(CancellationToken ct) => await hr.MyWorkdayAsync(User.GetUserId(), ct);

    /// <summary>Мои задачи: счётчики, ближайшие сроки, эффективность за 6 недель.</summary>
    [HttpGet("tasks")]
    public async Task<IActionResult> Tasks(CancellationToken ct)
    {
        if (!MyPermissions().Contains(Permissions.WorkTasks))
        {
            return Ok(new { available = false });
        }

        var day = await tasks.MyDayAsync(await ContextAsync(ct), ct);
        return Ok(new { available = true, day.Total, day.New, day.InProgress, day.Waiting, day.Overdue, day.Done, day.Upcoming, day.Efficiency });
    }

    /// <summary>События и сроки моих задач на сегодня и неделю вперёд.</summary>
    [HttpGet("calendar")]
    public async Task<List<CalendarItem>> Calendar(CancellationToken ct)
    {
        var today = FieldClock.Today;
        return await calendar.ItemsAsync(await ContextAsync(ct), today, today.AddDays(CalendarDays), ct);
    }

    /// <summary>Мои документы: recent, mine, reports — из «Файлов» с их правами; assigned — вложения к моим задачам от других.</summary>
    [HttpGet("documents")]
    public async Task<IActionResult> Documents([FromQuery] string? tab, CancellationToken ct)
    {
        var kind = tab is "mine" or "reports" or "assigned" ? tab : "recent";
        var permissions = MyPermissions();
        if (kind == "assigned")
        {
            if (!permissions.Contains(Permissions.WorkTasks))
            {
                return Ok(new { available = false, items = Array.Empty<MyDocument>() });
            }

            var attachments = await tasks.MyAttachmentsAsync(await ContextAsync(ct), DocumentsTake, ct);
            return Ok(new
            {
                available = true,
                items = attachments.Select(a => new MyDocument(a.Id, "attachment", a.FileName, a.ContentType, a.SizeBytes, a.CreatedAt, "assigned", a.By, a.TaskTitle, a.TaskId)),
            });
        }

        if (!permissions.Contains(Permissions.FilesRead) || await files.UserActorAsync(User.GetUserId(), ct) is not { } actor)
        {
            return Ok(new { available = false, items = Array.Empty<MyDocument>() });
        }

        return Ok(new { available = true, items = await files.MyDocumentsAsync(actor, kind, DocumentsTake, ct) });
    }

    /// <summary>План на день от AI: сохранённый план без вызова модели; первый за день — готовится в фоне (status generating).</summary>
    [HttpGet("ai-plan")]
    public async Task<DailyPlanView> AiPlan(CancellationToken ct) => await plans.GetAsync(await ContextAsync(ct), ct);

    /// <summary>«Обновить» план: не чаще раза в 15 минут — иначе 429 с временем, когда можно.</summary>
    [HttpPost("ai-plan/refresh")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("ai")]
    public async Task<IActionResult> RefreshAiPlan(CancellationToken ct)
    {
        try
        {
            return Ok(await plans.RefreshAsync(await ContextAsync(ct), ct));
        }
        catch (DailyPlanRateLimitedException e)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { error = e.Message, retryAt = e.RetryAt });
        }
    }
}
