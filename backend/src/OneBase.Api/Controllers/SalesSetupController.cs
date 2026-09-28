using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales;
using OneBase.Application.Security;
using OneBase.Domain.Sales;

namespace OneBase.Api.Controllers;

/// <summary>Оргструктура продаж, цели и планы — то, чего нет в Linko.</summary>
[ApiController]
[Route("api/sales/setup")]
[HasPermission(Permissions.SalesRead)]
[InvalidateSalesCache]
public sealed class SalesSetupController(IAppDbContext db, ISalesPlanImporter importer, SalesOptions salesOptions) : ControllerBase
{
    public sealed record DirectionInput(string Name, DirectionKind Kind, string? ManagerName, string? Description, int SortOrder);

    public sealed record RegionInput(string Name, Guid? DirectionId, string? SupervisorName, string? DealerName);

    public sealed record AgentInput(Guid? RegionId, bool IsVacancy, string? Note);

    public sealed record RegionPlanInput(Guid RegionId, PlanKind Kind, int Year, int Month, long? CategoryId, decimal? PlanKg);

    public sealed record AgentPlanInput(long LinkoUserId, PlanKind Kind, int Year, int Month, long? CategoryId, decimal? PlanKg);

    public sealed record AutoPlan(long LinkoUserId, decimal PlanKg, int Indicators);

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var profiles = await db.SalesAgentProfiles.AsNoTracking().ToDictionaryAsync(p => p.LinkoUserId, ct);
        var sellingAgents = await db.LinkoOrders.Where(o => o.AgentId != null).Select(o => o.AgentId!.Value).Distinct().ToListAsync(ct);

        return Ok(new
        {
            Directions = await db.SalesDirections.AsNoTracking()
                .OrderBy(d => d.SortOrder).ThenBy(d => d.Name)
                .Select(d => new { d.Id, d.Name, d.Kind, d.ManagerName, d.Description, d.SortOrder })
                .ToListAsync(ct),
            Regions = await db.SalesRegions.AsNoTracking()
                .OrderBy(r => r.Name)
                .Select(r => new { r.Id, r.LinkoBranchId, r.Name, r.DirectionId, r.SupervisorName, r.DealerName })
                .ToListAsync(ct),
            Agents = (await db.LinkoUsers.AsNoTracking().ToListAsync(ct))
                .Select(u => new
                {
                    LinkoUserId = u.Id,
                    Name = u.DisplayName,
                    u.IsActive,
                    Job = u.JobName,
                    HasSales = sellingAgents.Contains(u.Id),
                    InDirectory = profiles.ContainsKey(u.Id),
                    profiles.GetValueOrDefault(u.Id)?.RegionId,
                    IsVacancy = profiles.GetValueOrDefault(u.Id)?.IsVacancy ?? false,
                    profiles.GetValueOrDefault(u.Id)?.Note,
                })
                .OrderByDescending(a => a.HasSales).ThenBy(a => a.Name),
            Categories = await db.LinkoProductTypes.AsNoTracking()
                .OrderBy(t => t.Name)
                .Select(t => new { t.Id, t.Name, t.ParentId })
                .ToListAsync(ct),
            Targets = await db.SalesTargets.AsNoTracking().ToDictionaryAsync(t => t.Key, t => t.Value, ct),
        });
    }

    // ---------- Направления ----------

    [HttpPost("directions")]
    [HasPermission(Permissions.SalesManage)]
    public async Task<IActionResult> CreateDirection(DirectionInput input, CancellationToken ct)
    {
        var direction = new SalesDirection { Name = input.Name.Trim() };
        Apply(direction, input);
        db.SalesDirections.Add(direction);
        await db.SaveChangesAsync(ct);
        return Ok(new { direction.Id });
    }

    [HttpPut("directions/{id:guid}")]
    [HasPermission(Permissions.SalesManage)]
    public async Task<IActionResult> UpdateDirection(Guid id, DirectionInput input, CancellationToken ct)
    {
        var direction = await db.SalesDirections.FindAsync([id], ct);
        if (direction is null) return NotFound();
        Apply(direction, input);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("directions/{id:guid}")]
    [HasPermission(Permissions.SalesManage)]
    public async Task<IActionResult> DeleteDirection(Guid id, CancellationToken ct)
    {
        // Регионы направления не удаляются — у них просто сбрасывается направление (ON DELETE SET NULL).
        var deleted = await db.SalesDirections.Where(d => d.Id == id).ExecuteDeleteAsync(ct);
        return deleted == 0 ? NotFound() : NoContent();
    }

    private static void Apply(SalesDirection d, DirectionInput input)
    {
        d.Name = input.Name.Trim();
        d.Kind = input.Kind;
        d.ManagerName = input.ManagerName?.Trim();
        d.Description = input.Description?.Trim();
        d.SortOrder = input.SortOrder;
        d.UpdatedAt = DateTimeOffset.UtcNow;
    }

    // ---------- Регионы и агенты ----------

    [HttpPut("regions/{id:guid}")]
    [HasPermission(Permissions.SalesManage)]
    public async Task<IActionResult> UpdateRegion(Guid id, RegionInput input, CancellationToken ct)
    {
        var region = await db.SalesRegions.FindAsync([id], ct);
        if (region is null) return NotFound();
        region.Name = input.Name.Trim();
        region.DirectionId = input.DirectionId;
        region.SupervisorName = input.SupervisorName?.Trim();
        region.DealerName = input.DealerName?.Trim();
        region.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPut("agents/{linkoUserId:long}")]
    [HasPermission(Permissions.SalesManage)]
    public async Task<IActionResult> UpsertAgent(long linkoUserId, AgentInput input, CancellationToken ct)
    {
        if (!await db.LinkoUsers.AnyAsync(u => u.Id == linkoUserId, ct)) return NotFound();

        var profile = await db.SalesAgentProfiles.FindAsync([linkoUserId], ct);
        if (profile is null)
        {
            profile = new SalesAgentProfile { LinkoUserId = linkoUserId };
            db.SalesAgentProfiles.Add(profile);
        }

        profile.RegionId = input.RegionId;
        profile.IsVacancy = input.IsVacancy;
        profile.Note = input.Note?.Trim();
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("agents/{linkoUserId:long}")]
    [HasPermission(Permissions.SalesManage)]
    public async Task<IActionResult> RemoveAgent(long linkoUserId, CancellationToken ct)
    {
        var deleted = await db.SalesAgentProfiles.Where(p => p.LinkoUserId == linkoUserId).ExecuteDeleteAsync(ct);
        return deleted == 0 ? NotFound() : NoContent();
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

    // ---------- Планы ----------

    [HttpGet("plans")]
    public async Task<IActionResult> GetPlans([FromQuery] int year, [FromQuery] int month, [FromQuery] PlanKind kind = PlanKind.Rop, CancellationToken ct = default)
    {
        return Ok(new
        {
            RegionPlans = await db.SalesRegionPlans.AsNoTracking()
                .Where(p => p.Year == year && p.Month == month && p.Kind == kind)
                .Select(p => new { p.Id, p.RegionId, p.CategoryId, p.PlanKg })
                .ToListAsync(ct),
            AgentPlans = await db.SalesAgentPlans.AsNoTracking()
                .Where(p => p.Year == year && p.Month == month && p.Kind == kind)
                .Select(p => new { p.Id, p.LinkoUserId, p.CategoryId, p.PlanKg })
                .ToListAsync(ct),
            // Планы агентов из Linko (staff_balance, кг) — действуют, если в OneBase не задан ручной.
            AutoAgentPlans = kind != PlanKind.Rop ? [] : await AutoAgentPlansAsync(year, month, ct),
        });
    }

    /// <summary>Автопланы ТП из Linko без супервайзеров (их план — план команды, см. Sales:StaffPlanExcludeJobs).</summary>
    private async Task<List<AutoPlan>> AutoAgentPlansAsync(int year, int month, CancellationToken ct)
    {
        var plans = await db.SalesStaffPlans.AsNoTracking()
            .Where(p => p.Year == year && p.Month == month && p.PlanType == StaffPlanTypes.SalesWeight)
            .GroupBy(p => p.LinkoUserId)
            .Select(g => new AutoPlan(g.Key, g.Sum(p => p.PlanAmount), g.Count()))
            .ToListAsync(ct);

        var ids = plans.Select(p => p.LinkoUserId).ToList();
        var excluded = (await db.LinkoUsers.AsNoTracking().Where(u => ids.Contains(u.Id)).Select(u => new { u.Id, u.JobName }).ToListAsync(ct))
            .Where(u => u.JobName is { } job && salesOptions.StaffPlanExcludeJobs.Any(x => job.Contains(x, StringComparison.OrdinalIgnoreCase)))
            .Select(u => u.Id)
            .ToHashSet();
        return plans.Where(p => !excluded.Contains(p.LinkoUserId)).ToList();
    }

    [HttpPut("plans/region")]
    [HasPermission(Permissions.SalesManage)]
    public async Task<IActionResult> SetRegionPlan(RegionPlanInput input, CancellationToken ct)
    {
        var plan = await db.SalesRegionPlans.FirstOrDefaultAsync(p => p.RegionId == input.RegionId && p.Kind == input.Kind
            && p.Year == input.Year && p.Month == input.Month && p.CategoryId == input.CategoryId, ct);

        if (input.PlanKg is null)
        {
            if (plan is not null) db.SalesRegionPlans.Remove(plan);
        }
        else
        {
            if (plan is null)
            {
                plan = new SalesRegionPlan { RegionId = input.RegionId, Kind = input.Kind, Year = input.Year, Month = input.Month, CategoryId = input.CategoryId };
                db.SalesRegionPlans.Add(plan);
            }

            plan.PlanKg = input.PlanKg.Value;
            plan.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPut("plans/agent")]
    [HasPermission(Permissions.SalesManage)]
    public async Task<IActionResult> SetAgentPlan(AgentPlanInput input, CancellationToken ct)
    {
        var plan = await db.SalesAgentPlans.FirstOrDefaultAsync(p => p.LinkoUserId == input.LinkoUserId && p.Kind == input.Kind
            && p.Year == input.Year && p.Month == input.Month && p.CategoryId == input.CategoryId, ct);

        if (input.PlanKg is null)
        {
            if (plan is not null) db.SalesAgentPlans.Remove(plan);
        }
        else
        {
            if (plan is null)
            {
                plan = new SalesAgentPlan { LinkoUserId = input.LinkoUserId, Kind = input.Kind, Year = input.Year, Month = input.Month, CategoryId = input.CategoryId };
                db.SalesAgentPlans.Add(plan);
            }

            plan.PlanKg = input.PlanKg.Value;
            plan.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("plans/import")]
    [HasPermission(Permissions.SalesManage)]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<PlanImportResult>> Import(IFormFile file, [FromQuery] PlanImportTarget target, [FromQuery] PlanKind kind = PlanKind.Rop, CancellationToken ct = default)
    {
        await using var stream = file.OpenReadStream();
        return await importer.ImportAsync(stream, file.FileName, target, kind, ct);
    }

    [HttpGet("plans/template")]
    [HasPermission(Permissions.SalesManage)]
    public async Task<IActionResult> Template([FromQuery] PlanImportTarget target, CancellationToken ct)
    {
        var bytes = await importer.BuildTemplateAsync(target, ct);
        var name = target == PlanImportTarget.Region ? "plan-regions.xlsx" : "plan-agents.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
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
