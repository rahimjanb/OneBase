using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Security;
using OneBase.Domain.Audit;
using OneBase.Domain.Sales;

namespace OneBase.Api.Controllers;

/// <summary>
/// «Настройки продаж»: оргструктура OneBase (направления — РМ и каналы; у региона — направление, СВР и дилер), справочник ТП из Linko
/// (только чтение) и цели. В Linko оргструктуры нет — она ведётся здесь; планы ТП — из Linko, планы РОП и «Завод» — sales."RegionPlans".
/// </summary>
[ApiController]
[Route("api/sales/setup")]
[HasPermission(Permissions.SalesRead)]
[InvalidateSalesCache]
public sealed class SalesSetupController(IAppDbContext db, SalesOptions salesOptions, IAuditLogger audit) : ControllerBase
{
    private const int NameLength = 200; // как в схеме sales: Name, ManagerName, SupervisorName, DealerName — varchar(200)

    public sealed record DirectionInput(string? Name, string? Kind, string? ManagerName, string? Description, int SortOrder);

    public sealed record RegionInput(Guid? DirectionId, string? SupervisorName, string? DealerName);

    /// <summary>
    /// Направления, регионы (филиалы Linko) с направлением, СВР и дилером, справочник ТП из Linko (ТП — по должности, вакансия — «вакан»
    /// в имени или ID 0), категории и цели. У региона вне вторички («Завод», «К К Мерч») и старого филиала — пояснение: их направление,
    /// СВР и дилер в отчётах не используются.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var sellingAgents = await db.LinkoOrders.Where(o => o.AgentId != null).Select(o => o.AgentId!.Value).Distinct().ToListAsync(ct);
        var regions = await db.SalesRegions.AsNoTracking().OrderBy(r => r.Name).ToListAsync(ct);
        var (_, aliases) = OldBranches.Merge(
            regions.Select(r => new RegionInfo(r.Id, r.LinkoBranchId, r.Name, null, null, null)).ToList(),
            salesOptions.OldBranchSuffix);

        string? NoteOf(SalesRegion r) =>
            salesOptions.IsExcludedBranch(r.Name) ? "экспорт и опт — не регион вторички"
            : salesOptions.IsIgnoredBranch(r.Name) ? "во вторичку не входит"
            : aliases.TryGetValue(r.LinkoBranchId, out var target) ? $"старый филиал — считается в регионе «{regions.First(x => x.Id == target).Name}»"
            : null;

        return Ok(new
        {
            Directions = await db.SalesDirections.AsNoTracking()
                .OrderBy(d => d.SortOrder).ThenBy(d => d.Name)
                .Select(d => new { d.Id, d.Name, d.Kind, d.ManagerName, d.Description, d.SortOrder })
                .ToListAsync(ct),
            Regions = regions.Select(r => new
            {
                r.Id,
                r.LinkoBranchId,
                r.Name,
                r.DirectionId,
                r.SupervisorName,
                r.DealerName,
                Note = NoteOf(r),
            }),
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

    // ---------- Направления ----------

    /// <summary>Новое направление: РМ (RegionalManager) или канал (Channel) — Базар, ключевые клиенты.</summary>
    [HttpPost("directions")]
    [HasPermission(Permissions.SalesManage)]
    public async Task<IActionResult> CreateDirection(DirectionInput input, CancellationToken ct)
    {
        if (await ValidateAsync(input, null, ct) is { } error) return BadRequest(new { error });

        var direction = new SalesDirection { Name = input.Name!.Trim() };
        Apply(direction, input);
        db.SalesDirections.Add(direction);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "sales.direction.created", "sales_direction", direction.Id.ToString(),
            new { direction.Name, Kind = direction.Kind.ToString(), direction.ManagerName }, ct);
        return Ok(new { direction.Id });
    }

    [HttpPut("directions/{id:guid}")]
    [HasPermission(Permissions.SalesManage)]
    public async Task<IActionResult> UpdateDirection(Guid id, DirectionInput input, CancellationToken ct)
    {
        var direction = await db.SalesDirections.FindAsync([id], ct);
        if (direction is null) return NotFound(new { error = "Направление не найдено." });
        if (await ValidateAsync(input, id, ct) is { } error) return BadRequest(new { error });

        Apply(direction, input);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "sales.direction.updated", "sales_direction", id.ToString(),
            new { direction.Name, Kind = direction.Kind.ToString(), direction.ManagerName, direction.SortOrder }, ct);
        return NoContent();
    }

    [HttpDelete("directions/{id:guid}")]
    [HasPermission(Permissions.SalesManage)]
    public async Task<IActionResult> DeleteDirection(Guid id, CancellationToken ct)
    {
        // Регионы направления не удаляются — у них просто сбрасывается направление (ON DELETE SET NULL).
        var name = await db.SalesDirections.Where(d => d.Id == id).Select(d => d.Name).FirstOrDefaultAsync(ct);
        if (name is null) return NotFound(new { error = "Направление не найдено." });

        await db.SalesDirections.Where(d => d.Id == id).ExecuteDeleteAsync(ct);
        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "sales.direction.deleted", "sales_direction", id.ToString(), new { name }, ct);
        return NoContent();
    }

    /// <summary>Проверка направления; null — всё в порядке, иначе текст ошибки для пользователя.</summary>
    private async Task<string?> ValidateAsync(DirectionInput input, Guid? id, CancellationToken ct)
    {
        var name = input.Name?.Trim();
        if (string.IsNullOrEmpty(name)) return "Укажите название направления.";
        if (name.Length > NameLength) return $"Название направления — не длиннее {NameLength} символов.";
        if (KindOf(input.Kind) is null) return "Тип направления — «РМ» (RegionalManager) или «Канал» (Channel).";
        if (input.ManagerName?.Trim().Length > NameLength) return $"Имя руководителя — не длиннее {NameLength} символов.";

        var lower = name.ToLower();
        if (await db.SalesDirections.AnyAsync(d => d.Id != id && d.Name.ToLower() == lower, ct))
        {
            return $"Направление «{name}» уже есть.";
        }

        return null;
    }

    private static DirectionKind? KindOf(string? kind) =>
        Enum.TryParse<DirectionKind>(kind?.Trim(), ignoreCase: true, out var value) && Enum.IsDefined(value) ? value : null;

    private static void Apply(SalesDirection d, DirectionInput input)
    {
        d.Name = input.Name!.Trim();
        d.Kind = KindOf(input.Kind)!.Value;
        d.ManagerName = Clean(input.ManagerName);
        d.Description = Clean(input.Description);
        d.SortOrder = input.SortOrder;
        d.UpdatedAt = DateTimeOffset.UtcNow;
    }

    // ---------- Регионы ----------

    /// <summary>
    /// Направление (РМ или канал), СВР и дилер региона. Название региона — название филиала в Linko: по нему склеиваются старые филиалы
    /// и склады, поэтому здесь оно не меняется — его обновляет синхронизация по последнему заказу филиала.
    /// </summary>
    [HttpPut("regions/{id:guid}")]
    [HasPermission(Permissions.SalesManage)]
    public async Task<IActionResult> UpdateRegion(Guid id, RegionInput input, CancellationToken ct)
    {
        var region = await db.SalesRegions.FindAsync([id], ct);
        if (region is null) return NotFound(new { error = "Регион не найден." });
        if (input.DirectionId is { } directionId && !await db.SalesDirections.AnyAsync(d => d.Id == directionId, ct))
        {
            return BadRequest(new { error = "Такого направления нет — обновите страницу." });
        }

        if (input.SupervisorName?.Trim().Length > NameLength) return BadRequest(new { error = $"Имя СВР — не длиннее {NameLength} символов." });
        if (input.DealerName?.Trim().Length > NameLength) return BadRequest(new { error = $"Название дилера — не длиннее {NameLength} символов." });

        region.DirectionId = input.DirectionId;
        region.SupervisorName = Clean(input.SupervisorName);
        region.DealerName = Clean(input.DealerName);
        region.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "sales.region.updated", "sales_region", id.ToString(),
            new { region.Name, region.DirectionId, region.SupervisorName, region.DealerName }, ct);
        return NoContent();
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
