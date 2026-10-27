using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneBase.Api.Auth;
using OneBase.Application.Field;
using OneBase.Application.Hr;
using OneBase.Infrastructure.Hr;

namespace OneBase.Api.Controllers;

/// <summary>
/// Раздел HR: обзор, сотрудники, посещаемость, опоздания и отсутствия, рабочее время и табели, отчёты. Данные — из Verifix (только чтение).
/// Кто что видит, решает HrService по правам и руководству отделом — никогда по параметрам запроса: чужой сотрудник — 404, нет доступа — 403.
/// </summary>
[ApiController]
[Authorize]
[FieldErrors]
[Route("api/hr")]
public sealed class HrController(HrService hr) : ControllerBase
{
    private Task<HrContext> ContextAsync(CancellationToken ct) =>
        hr.ContextAsync(User.GetUserId(), User.FindAll(OneBaseClaims.Permission).Select(c => c.Value).ToHashSet(), ct);

    public sealed record NoteInput(string? Note);

    /// <summary>Что доступно пользователю в разделе: все сотрудники или подразделения своего отдела, настройки интеграции.</summary>
    [HttpGet("context")]
    public async Task<IActionResult> Context(CancellationToken ct)
    {
        var ctx = await ContextAsync(ct);
        if (!ctx.HasAccess && !ctx.ManagesIntegration)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Нет доступа к HR." });
        }

        return Ok(new
        {
            ctx.HasAccess,
            ctx.SeesAll,
            ctx.ManagesIntegration,
            ctx.CanEditNotes,
            Departments = ctx.HeadOf,
            Divisions = await hr.DivisionsAsync(ctx, ct),
            Sync = await hr.SyncBriefAsync(ct),
        });
    }

    [HttpGet("overview")]
    public async Task<HrOverview> Overview(CancellationToken ct) => await hr.OverviewAsync(await ContextAsync(ct), ct);

    [HttpGet("employees")]
    public async Task<PageResult<HrEmployeeRow>> Employees([FromQuery] string? search, [FromQuery] long? divisionId, [FromQuery] string? status,
        [FromQuery] string? sort, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        await hr.EmployeesAsync(await ContextAsync(ct), new HrEmployeeQuery(search, divisionId, status, sort, page, pageSize), ct);

    [HttpGet("employees/{id:guid}")]
    public async Task<HrEmployeeCard> Employee(Guid id, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        await hr.EmployeeAsync(await ContextAsync(ct), id, from, to, ct);

    /// <summary>Заметка HR — локальное поле OneBase; данные Verifix здесь не меняются.</summary>
    [HttpPut("employees/{id:guid}/note")]
    public async Task<IActionResult> Note(Guid id, NoteInput input, CancellationToken ct)
    {
        await hr.SaveNoteAsync(await ContextAsync(ct), id, input.Note, ct);
        return NoContent();
    }

    /// <summary>Посещаемость за день: итог каждого сотрудника. status — present | late | absent | leave | expected | unknown | dayoff | noschedule.</summary>
    [HttpGet("attendance")]
    public async Task<IReadOnlyList<HrDayRow>> Attendance([FromQuery] DateOnly? date, [FromQuery] long? divisionId, [FromQuery] string? status,
        [FromQuery] string? search, CancellationToken ct) =>
        await hr.DayAsync(await ContextAsync(ct), date, divisionId, status, search, ct);

    /// <summary>Журнал отметок (проходов) за период.</summary>
    [HttpGet("events")]
    public async Task<PageResult<HrEventRow>> Events([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] Guid? employeeId,
        [FromQuery] long? divisionId, [FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken ct = default) =>
        await hr.EventsAsync(await ContextAsync(ct), from, to, employeeId, divisionId, page, pageSize, ct);

    /// <summary>Опоздания, ранние уходы, отсутствия и дни без ухода. kind — late | early | absent | noexit.</summary>
    [HttpGet("deviations")]
    public async Task<IReadOnlyList<HrDeviation>> Deviations([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? kind,
        [FromQuery] long? divisionId, CancellationToken ct) =>
        await hr.DeviationsAsync(await ContextAsync(ct), from, to, kind, divisionId, ct);

    [HttpGet("timesheet")]
    public async Task<HrTimesheet> Timesheet([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] long? divisionId, [FromQuery] string? search,
        CancellationToken ct) =>
        await hr.TimesheetAsync(await ContextAsync(ct), from, to, divisionId, search, ct);

    /// <summary>«Лидеры месяца» (главная): без единого опоздания за месяц — по отработанным часам.</summary>
    [HttpGet("leaders")]
    public async Task<HrLeaders> Leaders([FromQuery] int take = 7, CancellationToken ct = default) =>
        await hr.LeadersAsync(await ContextAsync(ct), take, ct);

    [HttpGet("reports")]
    public async Task<HrReport> Report([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] long? divisionId, CancellationToken ct) =>
        await hr.ReportAsync(await ContextAsync(ct), from, to, divisionId, ct);

    /// <summary>Отчёт в Excel: подразделения, табель сотрудников с буквами дней, отклонения.</summary>
    [HttpGet("reports/export")]
    public async Task<IActionResult> Export([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] long? divisionId, CancellationToken ct)
    {
        var ctx = await ContextAsync(ct);
        var report = await hr.ReportAsync(ctx, from, to, divisionId, ct);
        var sheet = await hr.TimesheetAsync(ctx, report.From, report.To, divisionId, null, ct);
        var deviations = await hr.DeviationsAsync(ctx, report.From, report.To, null, divisionId, ct);
        var bytes = HrReportWorkbook.Build(report, sheet, deviations);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"hr-{report.From:yyyy-MM-dd}-{report.To:yyyy-MM-dd}.xlsx");
    }
}
