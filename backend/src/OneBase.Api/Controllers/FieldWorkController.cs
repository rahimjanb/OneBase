using Microsoft.AspNetCore.Mvc;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Domain.Field;

namespace OneBase.Api.Controllers;

/// <summary>Sales Base: торговые точки и карта.</summary>
[Route("api/field")]
public sealed class FieldCustomersController(
    FieldAccess access,
    FieldCustomerService customers,
    FieldTaskService tasks,
    FieldRecommendationService recommendations) : FieldControllerBase(access)
{
    [HttpGet("customers")]
    public async Task<PageResult<FieldCustomerRow>> List(
        [FromQuery] string? search,
        [FromQuery] Guid? agentId,
        [FromQuery] Guid? teamId,
        [FromQuery] long? branchId,
        [FromQuery] FieldCustomerStatus? status,
        [FromQuery] FieldPriority? priority,
        [FromQuery] string? attention,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        await customers.ListAsync(await ScopeAsync(ct), new FieldCustomerQuery(search, agentId, teamId, branchId, status, priority, attention, sort, page, pageSize), ct);

    [HttpGet("customers/{id:long}")]
    public async Task<FieldCustomerCard> Get(long id, CancellationToken ct) =>
        await customers.GetAsync(await ScopeAsync(ct), id, tasks, recommendations, ct);

    [HttpPut("customers/{id:long}")]
    public async Task<IActionResult> Update(long id, FieldCustomerInput input, CancellationToken ct)
    {
        await customers.UpdateAsync(await ScopeAsync(ct), id, input, ct);
        return NoContent();
    }

    public sealed record AgentInput(Guid AgentId);

    [HttpPost("customers/{id:long}/agent")]
    public async Task<FieldCustomerService.ChangeAgentResult> ChangeAgent(long id, AgentInput input, CancellationToken ct) =>
        await customers.ChangeAgentAsync(await ScopeAsync(ct), id, input.AgentId, ct);

    [HttpGet("map")]
    public async Task<FieldMapView> Map(
        [FromQuery] DateOnly? date,
        [FromQuery] Guid? agentId,
        [FromQuery] Guid? teamId,
        [FromQuery] double? south,
        [FromQuery] double? west,
        [FromQuery] double? north,
        [FromQuery] double? east,
        CancellationToken ct)
    {
        (double, double, double, double)? box = south is { } s && west is { } w && north is { } n && east is { } e ? (s, w, n, e) : null;
        return await customers.MapAsync(await ScopeAsync(ct), date ?? FieldClock.Today, agentId, teamId, box, ct);
    }
}

/// <summary>Sales Base: маршруты.</summary>
[Route("api/field/routes")]
public sealed class FieldRoutesController(FieldAccess access, FieldRouteService routes) : FieldControllerBase(access)
{
    [HttpGet]
    public async Task<List<FieldRouteSummary>> List([FromQuery] DateOnly? date, [FromQuery] Guid? agentId, [FromQuery] Guid? teamId, CancellationToken ct) =>
        await routes.ListAsync(await ScopeAsync(ct), date ?? FieldClock.Today, agentId, teamId, ct);

    [HttpGet("{id:guid}")]
    public async Task<FieldRouteView> Get(Guid id, CancellationToken ct) => await routes.GetAsync(await ScopeAsync(ct), id, ct);

    /// <summary>Маршрут агента на день (агент — свой, без agentId). 204 — маршрута ещё нет.</summary>
    [HttpGet("for")]
    public async Task<IActionResult> For([FromQuery] Guid? agentId, [FromQuery] DateOnly? date, CancellationToken ct)
    {
        var scope = await ScopeAsync(ct);
        var agent = agentId ?? scope.MemberId ?? throw new FieldValidationException("Укажите агента.");
        var route = await routes.GetForAsync(scope, agent, date ?? FieldClock.Today, ct);
        return route is null ? NoContent() : Ok(route);
    }

    public sealed record BuildInput(Guid? AgentId, DateOnly? Date, double? Latitude, double? Longitude);

    [HttpPost("build")]
    public async Task<FieldRouteView> Build(BuildInput input, CancellationToken ct)
    {
        var scope = await ScopeAsync(ct);
        var agent = input.AgentId ?? scope.MemberId ?? throw new FieldValidationException("Укажите агента.");
        return await routes.BuildAsync(scope, agent, input.Date ?? FieldClock.Today, input.Latitude, input.Longitude, ct);
    }

    public sealed record PointsInput(List<long> MarketIds);

    [HttpPut("{id:guid}/points")]
    public async Task<FieldRouteView> Points(Guid id, PointsInput input, CancellationToken ct) =>
        await routes.UpdatePointsAsync(await ScopeAsync(ct), id, input.MarketIds ?? [], ct);

    public sealed record OptimizeInput(double? Latitude, double? Longitude);

    [HttpPost("{id:guid}/optimize")]
    public async Task<FieldRouteView> Optimize(Guid id, OptimizeInput input, CancellationToken ct) =>
        await routes.OptimizeAsync(await ScopeAsync(ct), id, input.Latitude, input.Longitude, ct);

    public sealed record SkipInput(string? Note);

    [HttpPost("points/{pointId:guid}/skip")]
    public async Task<FieldRouteView> Skip(Guid pointId, SkipInput input, CancellationToken ct) =>
        await routes.SkipPointAsync(await ScopeAsync(ct), pointId, input.Note, ct);
}

/// <summary>Sales Base: визиты, фото, совместные выезды.</summary>
[Route("api/field/visits")]
public sealed class FieldVisitsController(FieldAccess access, FieldVisitService visits, IFileStorage storage) : FieldControllerBase(access)
{
    [HttpGet]
    public async Task<PageResult<FieldVisitRow>> List(
        [FromQuery] Guid? agentId,
        [FromQuery] long? marketId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] FieldVisitResult? result,
        [FromQuery] FieldGeoStatus? geo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        await visits.ListAsync(await ScopeAsync(ct), new FieldVisitQuery(agentId, marketId, from, to, result, geo, page, pageSize), ct);

    [HttpGet("{id:guid}")]
    public async Task<FieldVisitRow> Get(Guid id, CancellationToken ct) => await visits.GetAsync(await ScopeAsync(ct), id, ct);

    [HttpGet("active")]
    public async Task<IActionResult> Active(CancellationToken ct) =>
        await visits.ActiveAsync(await ScopeAsync(ct), ct) is { } visit ? Ok(visit) : NoContent();

    [HttpPost("start")]
    public async Task<FieldVisitRow> Start(FieldVisitStartInput input, CancellationToken ct) => await visits.StartAsync(await ScopeAsync(ct), input, ct);

    [HttpPost("{id:guid}/finish")]
    public async Task<FieldVisitRow> Finish(Guid id, FieldVisitFinishInput input, CancellationToken ct) => await visits.FinishAsync(await ScopeAsync(ct), id, input, ct);

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        await visits.CancelAsync(await ScopeAsync(ct), id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/photo")]
    [RequestSizeLimit(FieldVisitService.MaxPhotoBytes + 64 * 1024)]
    public async Task<IActionResult> Photo(Guid id, IFormFile? file, CancellationToken ct)
    {
        if (file is null)
        {
            throw new FieldValidationException("Выберите фото.");
        }

        await using var stream = file.OpenReadStream();
        await visits.SavePhotoAsync(await ScopeAsync(ct), id, stream, file.Length, file.ContentType, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/photo")]
    public async Task<IActionResult> GetPhoto(Guid id, CancellationToken ct)
    {
        var (key, contentType) = await visits.PhotoAsync(await ScopeAsync(ct), id, ct);
        var buffer = new MemoryStream();
        await storage.DownloadAsync(key, buffer, ct);
        buffer.Position = 0;
        Response.Headers.CacheControl = "private, max-age=3600";
        return File(buffer, contentType);
    }

    [HttpGet("joint")]
    public async Task<List<FieldJointVisitRow>> Joint([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var start = from ?? FieldClock.Today.AddDays(-7);
        return await visits.JointListAsync(await ScopeAsync(ct), start, to ?? start.AddDays(37), ct);
    }

    [HttpPost("joint")]
    public async Task<IActionResult> CreateJoint(FieldJointVisitInput input, CancellationToken ct) =>
        Ok(new { id = await visits.JointCreateAsync(await ScopeAsync(ct), input, ct) });

    [HttpPut("joint/{id:guid}")]
    public async Task<IActionResult> UpdateJoint(Guid id, FieldJointVisitUpdate input, CancellationToken ct)
    {
        await visits.JointUpdateAsync(await ScopeAsync(ct), id, input, ct);
        return NoContent();
    }
}

/// <summary>Sales Base: задачи.</summary>
[Route("api/field/tasks")]
public sealed class FieldTasksController(FieldAccess access, FieldTaskService tasks) : FieldControllerBase(access)
{
    [HttpGet]
    public async Task<PageResult<FieldTaskRow>> List(
        [FromQuery] string? status,
        [FromQuery] Guid? assignedToId,
        [FromQuery] FieldPriority? priority,
        [FromQuery] string? due,
        [FromQuery] long? marketId,
        [FromQuery] FieldActorType? createdBy,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        await tasks.ListAsync(await ScopeAsync(ct), new FieldTaskQuery(status, assignedToId, priority, due, marketId, createdBy, search, page, pageSize), ct);

    [HttpGet("{id:guid}")]
    public async Task<FieldTaskRow> Get(Guid id, CancellationToken ct) => await tasks.GetAsync(await ScopeAsync(ct), id, ct);

    [HttpPost]
    public async Task<IActionResult> Create(FieldTaskInput input, CancellationToken ct) => Ok(new { id = await tasks.CreateAsync(await ScopeAsync(ct), input, ct) });

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, FieldTaskInput input, CancellationToken ct)
    {
        await tasks.UpdateAsync(await ScopeAsync(ct), id, input, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, FieldTaskStatusInput input, CancellationToken ct)
    {
        await tasks.ChangeStatusAsync(await ScopeAsync(ct), id, input, ct);
        return NoContent();
    }
}

/// <summary>Sales Base: рекомендации AI и решения по ним.</summary>
[Route("api/field/ai")]
public sealed class FieldAiController(FieldAccess access, FieldRecommendationService recommendations) : FieldControllerBase(access)
{
    [HttpGet("recommendations")]
    public async Task<PageResult<FieldRecommendationRow>> List([FromQuery] string? status, [FromQuery] FieldRecommendationKind? kind, [FromQuery] Guid? agentId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        await recommendations.ListAsync(await ScopeAsync(ct), new FieldRecommendationQuery(status, kind, agentId, page, pageSize), ct);

    [HttpPost("recommendations/generate")]
    public async Task<FieldGenerateResult> Generate(CancellationToken ct) =>
        await recommendations.GenerateAsync(await ScopeAsync(ct), FieldClock.Today, ct);

    [HttpPost("recommendations/{id:guid}/approve")]
    public async Task<FieldRecommendationRow> Approve(Guid id, FieldRecommendationDecision decision, CancellationToken ct) =>
        await recommendations.ApproveAsync(await ScopeAsync(ct), id, decision, ct);

    public sealed record RejectInput(string? Note);

    [HttpPost("recommendations/{id:guid}/reject")]
    public async Task<FieldRecommendationRow> Reject(Guid id, RejectInput input, CancellationToken ct) =>
        await recommendations.RejectAsync(await ScopeAsync(ct), id, input.Note, ct);
}
