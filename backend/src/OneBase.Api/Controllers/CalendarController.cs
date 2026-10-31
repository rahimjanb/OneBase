using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneBase.Api.Auth;
using OneBase.Application.Field;
using OneBase.Application.Work;

namespace OneBase.Api.Controllers;

/// <summary>
/// Календарь: встречи, сроки отчётов, важные события и сроки моих задач. Кто что видит и меняет — решает CalendarService по пользователю из
/// токена.
/// </summary>
[ApiController]
[Authorize]
[FieldErrors]
[Route("api/calendar")]
public sealed class CalendarController(CalendarService calendar, WorkTaskService tasks) : ControllerBase
{
    private Task<WorkContext> ContextAsync(CancellationToken ct) =>
        tasks.ContextAsync(User.GetUserId(), User.FindAll(OneBaseClaims.Permission).Select(c => c.Value).ToHashSet(), ct);

    /// <summary>События и сроки задач за период (по умолчанию — неделя с сегодняшнего дня), не больше 62 дней.</summary>
    [HttpGet]
    public async Task<List<CalendarItem>> Items([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var start = from ?? FieldClock.Today;
        return await calendar.ItemsAsync(await ContextAsync(ct), start, to ?? start.AddDays(6), ct);
    }

    /// <summary>Для формы: отделы, для которых можно создать событие, и можно ли событие для всей компании.</summary>
    [HttpGet("abilities")]
    public async Task<CalendarAbilities> Abilities(CancellationToken ct) => await calendar.AbilitiesAsync(await ContextAsync(ct), ct);

    [HttpGet("people")]
    public async Task<List<CalendarPerson>> People([FromQuery] string? q, CancellationToken ct) => await calendar.PeopleAsync(q, ct);

    [HttpGet("events/{id:guid}")]
    public async Task<CalendarEventView> Get(Guid id, CancellationToken ct) => await calendar.GetAsync(await ContextAsync(ct), id, ct);

    [HttpPost("events")]
    public async Task<IActionResult> Create(CalendarEventInput input, CancellationToken ct) =>
        Ok(new { id = await calendar.CreateAsync(await ContextAsync(ct), input, ct) });

    [HttpPut("events/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, CalendarEventInput input, CancellationToken ct)
    {
        await calendar.UpdateAsync(await ContextAsync(ct), id, input, ct);
        return NoContent();
    }

    [HttpDelete("events/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await calendar.DeleteAsync(await ContextAsync(ct), id, ct);
        return NoContent();
    }
}
