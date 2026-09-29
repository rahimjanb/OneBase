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
        (await Load(q, ct)).CachedOverview();

    /// <summary>from/to — диапазон дней для календаря визитов (по умолчанию с 1-го по последний день данных).</summary>
    [HttpGet("republic")]
    public async Task<GroupView> Republic([FromQuery] PeriodQuery q, [FromQuery] int? from, [FromQuery] int? to, CancellationToken ct) =>
        (await Load(q, ct)).CachedRepublic(from, to);

    [HttpGet("directions/{id}")]
    public async Task<ActionResult<GroupView>> Direction(string id, [FromQuery] PeriodQuery q, [FromQuery] int? from, [FromQuery] int? to, CancellationToken ct)
    {
        var analytics = await Load(q, ct);
        return analytics.HasDirection(id) ? analytics.CachedDirection(id, from, to) : NotFound();
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
        metric = metric is "sum" or "akb" ? metric : "kg";
        return analytics.HasRegion(id) ? analytics.CachedRegion(id, from, to, metric, metric == "akb" ? category : null) : NotFound();
    }

    [HttpGet("agents/{id:long}")]
    public async Task<ActionResult<AgentView>> Agent(long id, [FromQuery] PeriodQuery q, CancellationToken ct)
    {
        var analytics = await Load(q, ct);
        return analytics.HasAgent(id) ? analytics.CachedAgent(id) : NotFound();
    }

    /// <summary>Все планы из Linko (staff_balance) за месяц: агенты по регионам и планы команд (супервайзеров).</summary>
    [HttpGet("plans")]
    public async Task<PlansView> Plans([FromQuery] PeriodQuery q, CancellationToken ct) =>
        (await Load(q with { Plan = PlanKind.Rop }, ct)).CachedPlans();

    [HttpGet("problems")]
    public async Task<ProblemsView> Problems(
        [FromQuery] PeriodQuery q,
        [FromQuery] string? direction,
        [FromQuery] FlagKind? criterion,
        [FromQuery] bool vacancies = false,
        CancellationToken ct = default) =>
        (await Load(q, ct)).CachedProblems(string.IsNullOrEmpty(direction) ? null : direction, criterion, vacancies);

    private Task<SalesAnalytics> Load(PeriodQuery q, CancellationToken ct) => loader.LoadAsync(q.Year, q.Month, q.Plan, ct);
}
