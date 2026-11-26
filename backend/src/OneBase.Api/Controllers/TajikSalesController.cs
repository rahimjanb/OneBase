using Microsoft.AspNetCore.Mvc;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales.Tajik;
using OneBase.Application.Security;
using OneBase.Domain.Audit;

namespace OneBase.Api.Controllers;

/// <summary>
/// «Продажи → Экспорт → Таджикистан»: продажи дилера по его 1С — все регионы, регион (точки и категории), точка (накладные), остаток склада.
/// Как и экспорт «Завода» — только для охвата «вся организация». year, month — месяц (по умолчанию — последний месяц с данными).
/// Настройка регионов — с правом «Продажи: управление» (данные 1С не меняются, только отнесение точек к регионам).
/// </summary>
[ApiController]
[Route("api/sales/tajik")]
[HasPermission(Permissions.SalesRead)]
[SalesOrganizationOnly]
public sealed class TajikSalesController(TajikSalesService sales, TajikSetupService setup, IAuditLogger audit) : ControllerBase
{
    [HttpGet]
    public Task<TajikOverview> Overview([FromQuery] int? year, [FromQuery] int? month, CancellationToken ct) => sales.OverviewAsync(year, month, ct);

    /// <summary>Регион: id — Guid региона или none (точки без региона); category — название категории отчёта (фильтр точек и товаров).</summary>
    [HttpGet("regions/{id}")]
    public async Task<ActionResult<TajikRegionView>> Region(string id, [FromQuery] int? year, [FromQuery] int? month, [FromQuery] string? category, CancellationToken ct) =>
        await sales.RegionAsync(id, year, month, category, ct) is { } view ? view : NotFound();

    /// <summary>Точка: id — из списка точек (по названию в 1С).</summary>
    [HttpGet("stores/{id}")]
    public async Task<ActionResult<TajikStoreView>> Store(string id, [FromQuery] int? year, [FromQuery] int? month, CancellationToken ct) =>
        await sales.StoreAsync(id, year, month, ct) is { } view ? view : NotFound();

    [HttpGet("setup")]
    public Task<TajikSetupView> Setup(CancellationToken ct) => setup.GetAsync(ct);

    /// <summary>Список регионов целиком: без Id — новый, которых нет — удаляются вместе с закреплениями.</summary>
    [HttpPut("setup/regions")]
    [HasPermission(Permissions.SalesManage)]
    public async Task<IActionResult> SaveRegions([FromBody] List<TajikRegionInput> input, CancellationToken ct)
    {
        try
        {
            await setup.SaveRegionsAsync(input, User.GetUserId(), ct);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { error = e.Message });
        }

        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "sales.tajik.regions.updated", "integration", TajikSettings.Code,
            new { regions = input.Select(r => r.Name).ToList() }, ct);
        return Ok(await setup.GetAsync(ct));
    }

    /// <summary>Закрепить ТП (kind=agent, name) или точку (kind=store, storeId) за регионом; regionId пусто — снять.</summary>
    [HttpPut("setup/assignment")]
    [HasPermission(Permissions.SalesManage)]
    public async Task<IActionResult> SaveAssignment([FromBody] TajikAssignmentInput input, CancellationToken ct)
    {
        try
        {
            await setup.SaveAssignmentAsync(input, User.GetUserId(), ct);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { error = e.Message });
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new { error = e.Message });
        }

        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "sales.tajik.assignment.updated", "integration", TajikSettings.Code,
            new { input.Kind, input.Name, input.StoreId, input.RegionId }, ct);
        return Ok(await setup.GetAsync(ct));
    }
}
