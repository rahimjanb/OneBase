using Microsoft.AspNetCore.Mvc;
using OneBase.Api.Auth;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Security;
using OneBase.Domain.Sales;

namespace OneBase.Api.Controllers;

/// <summary>
/// Аналитика продаж по уровням drill-down: старт → республика → РМ → регион → агент.
/// Все данные — из нашей БД (синхронизированной с Linko).
/// Общие параметры: year, month (по умолчанию — месяц последних данных), plan=Rop|Factory.
/// </summary>
[ApiController]
[Route("api/sales")]
[HasPermission(Permissions.SalesRead)]
public sealed class SalesController(SalesDataLoader loader) : ControllerBase
{
    public sealed record PeriodQuery(int? Year, int? Month, PlanKind Plan = PlanKind.Rop);

    [HttpGet("months")]
    public Task<IReadOnlyList<SalesMonth>> Months(CancellationToken ct) => loader.MonthsAsync(ct);

    [HttpGet("overview")]
    public async Task<OverviewView> Overview([FromQuery] PeriodQuery q, CancellationToken ct) =>
        (await Load(q, ct)).Overview();

    /// <summary>from/to — диапазон дней для календаря визитов (по умолчанию с 1-го по последний день данных).</summary>
    [HttpGet("republic")]
    public async Task<GroupView> Republic([FromQuery] PeriodQuery q, [FromQuery] int? from, [FromQuery] int? to, CancellationToken ct)
    {
        var analytics = await Load(q, ct);
        var (f, t) = Range(analytics.Period, from, to);
        return analytics.Republic(f, t);
    }

    [HttpGet("directions/{id}")]
    public async Task<ActionResult<GroupView>> Direction(string id, [FromQuery] PeriodQuery q, [FromQuery] int? from, [FromQuery] int? to, CancellationToken ct)
    {
        var analytics = await Load(q, ct);
        if (!analytics.HasDirection(id)) return NotFound();
        var (f, t) = Range(analytics.Period, from, to);
        return analytics.Direction(id, f, t);
    }

    /// <summary>metric — календарь месяца: kg | sum | akb; category — для АКБ по категории.</summary>
    [HttpGet("regions/{id:guid}")]
    public async Task<ActionResult<RegionView>> Region(
        Guid id,
        [FromQuery] PeriodQuery q,
        [FromQuery] int? from,
        [FromQuery] int? to,
        [FromQuery] string metric = "kg",
        [FromQuery] long? category = null,
        CancellationToken ct = default)
    {
        var analytics = await Load(q, ct);
        if (!analytics.HasRegion(id)) return NotFound();
        var (f, t) = Range(analytics.Period, from, to);
        return analytics.Region(id, f, t, metric, category);
    }

    [HttpGet("agents/{id:long}")]
    public async Task<ActionResult<AgentView>> Agent(long id, [FromQuery] PeriodQuery q, CancellationToken ct)
    {
        var analytics = await Load(q, ct);
        return analytics.HasAgent(id) ? analytics.Agent(id) : NotFound();
    }

    [HttpGet("problems")]
    public async Task<ProblemsView> Problems(
        [FromQuery] PeriodQuery q,
        [FromQuery] string? direction,
        [FromQuery] FlagKind? criterion,
        [FromQuery] bool vacancies = false,
        CancellationToken ct = default) =>
        (await Load(q, ct)).Problems(string.IsNullOrEmpty(direction) ? null : direction, criterion, vacancies);

    private Task<SalesAnalytics> Load(PeriodQuery q, CancellationToken ct) => loader.LoadAsync(q.Year, q.Month, q.Plan, ct);

    private static (DateOnly From, DateOnly To) Range(PeriodInfo period, int? from, int? to)
    {
        var last = Math.Max(1, period.WorkedDays);
        var f = Math.Clamp(from ?? 1, 1, period.DaysInMonth);
        var t = Math.Clamp(to ?? last, f, period.DaysInMonth);
        return (new DateOnly(period.Year, period.Month, f), new DateOnly(period.Year, period.Month, t));
    }
}
