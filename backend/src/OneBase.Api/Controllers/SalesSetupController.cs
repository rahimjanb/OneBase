using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales;
using OneBase.Application.Security;
using OneBase.Domain.Sales;

namespace OneBase.Api.Controllers;

/// <summary>«Настройки продаж»: справочник из Linko (только чтение) и цели. Планы и оргструктура — только из Linko.</summary>
[ApiController]
[Route("api/sales/setup")]
[HasPermission(Permissions.SalesRead)]
[InvalidateSalesCache]
public sealed class SalesSetupController(IAppDbContext db, SalesOptions salesOptions) : ControllerBase
{
    /// <summary>
    /// Регионы (филиалы Linko), справочник ТП из Linko (ТП — по должности, вакансия — «вакант» в имени или ID 0),
    /// категории и цели. Оргструктуры OneBase нет: в «Продажах» только данные Linko.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var sellingAgents = await db.LinkoOrders.Where(o => o.AgentId != null).Select(o => o.AgentId!.Value).Distinct().ToListAsync(ct);

        return Ok(new
        {
            Regions = await db.SalesRegions.AsNoTracking()
                .OrderBy(r => r.Name)
                .Select(r => new { r.Id, r.LinkoBranchId, r.Name })
                .ToListAsync(ct),
            Agents = (await db.LinkoUsers.AsNoTracking().ToListAsync(ct))
                .Select(u => new
                {
                    LinkoUserId = u.Id,
                    Name = u.DisplayName,
                    u.IsActive,
                    Job = u.JobName,
                    HasSales = sellingAgents.Contains(u.Id),
                    IsSalesRep = u.JobName is { } job && salesOptions.SalesRepJobs.Any(j => string.Equals(j.Trim(), job.Trim(), StringComparison.OrdinalIgnoreCase)),
                    IsVacancy = salesOptions.IsVacancy(u.Id, $"{u.DisplayName} {u.Username}"),
                })
                .OrderByDescending(a => a.IsSalesRep).ThenByDescending(a => a.HasSales).ThenBy(a => a.Name),
            Categories = await db.LinkoProductTypes.AsNoTracking()
                .OrderBy(t => t.Name)
                .Select(t => new { t.Id, t.Name, t.ParentId })
                .ToListAsync(ct),
            Targets = await db.SalesTargets.AsNoTracking().ToDictionaryAsync(t => t.Key, t => t.Value, ct),
        });
    }

    // ---------- Цели ----------

    [HttpPut("targets")]
    [HasPermission(Permissions.SalesManage)]
    public async Task<IActionResult> UpdateTargets(Dictionary<string, decimal> values, CancellationToken ct)
    {
        var unknown = values.Keys.Except(SalesTargetKeys.Defaults.Keys).ToList();
        if (unknown.Count > 0) return BadRequest(new { error = $"Неизвестные цели: {string.Join(", ", unknown)}" });

        var targets = await db.SalesTargets.ToDictionaryAsync(t => t.Key, ct);
        foreach (var (key, value) in values)
        {
            if (!targets.TryGetValue(key, out var target))
            {
                target = new SalesTarget { Key = key };
                db.SalesTargets.Add(target);
            }

            target.Value = value;
        }

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Планы из Linko (kpi_plans) — только для просмотра.</summary>
    [HttpGet("kpi-plans")]
    public async Task<IActionResult> KpiPlans([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        var plans = await db.LinkoKpiPlans.AsNoTracking().Where(p => p.Year == year && p.Month == month).ToListAsync(ct);
        var userIds = plans.Where(p => p.UserId != null).Select(p => p.UserId!.Value).Distinct().ToList();
        var users = await db.LinkoUsers.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);

        return Ok(plans
            .OrderBy(p => p.IndicatorName).ThenBy(p => p.UserId)
            .Select(p => new
            {
                p.Id,
                p.UserId,
                UserName = p.UserId is { } id && users.TryGetValue(id, out var u) ? u.DisplayName : null,
                p.IndicatorId,
                p.IndicatorName,
                p.Plan,
            }));
    }
}
