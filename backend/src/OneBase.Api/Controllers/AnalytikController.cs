using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using OneBase.Api.Auth;
using OneBase.Application.Analytik;
using OneBase.Application.Analytik.Catalog;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Security;

namespace OneBase.Api.Controllers;

/// <summary>Ошибки Аналитика в ответ API: неверный план — 400, нет доступа — 403, не найдено — 404; текст — для пользователя.</summary>
public sealed class AnalytikErrorsAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        IActionResult? result = context.Exception switch
        {
            QueryPlanException { Code: QueryErrors.Denied } e => new ObjectResult(new { error = e.Message }) { StatusCode = StatusCodes.Status403Forbidden },
            QueryPlanException e => new BadRequestObjectResult(new { error = e.Message, code = e.Code }),
            UnauthorizedAccessException e => new ObjectResult(new { error = e.Message }) { StatusCode = StatusCodes.Status403Forbidden },
            KeyNotFoundException e => new NotFoundObjectResult(new { error = e.Message }),
            ArgumentException e => new BadRequestObjectResult(new { error = e.Message }),
            _ => null,
        };
        if (result is not null)
        {
            context.Result = result;
            context.ExceptionHandled = true;
        }
    }
}

/// <summary>Состояние раздела для пользователя: каталог, роль только на чтение, область продаж, может ли управлять.</summary>
public sealed record AnalytikStatus(DateTimeOffset? CatalogRefreshedAt, int Tables, int Relations, int ConfirmedRelations, int AvailableTables,
    bool DedicatedReadOnlyRole, bool Manager, string SalesScope);

public sealed record AnnotationInput(string Kind, string Target, string? Title, string? Description, string? Module, string? Access, string? RequiredPermission,
    string? Sensitivity);

public sealed record QueryLogRow(Guid Id, DateTimeOffset CreatedAt, Guid? UserId, string Origin, string? AgentCode, string Status, string? Tables, string? Scope,
    int RowCount, bool Truncated, long DurationMs, string? Error, string Plan, string? Sql);

/// <summary>
/// Раздел «Аналитик» (OneBase Intelligence): каталог базы (Data Intelligence Engine), аналитические запросы только на чтение, журнал запросов.
/// Право раздела — analytik.use; данные — по остальным правам пользователя и его области продаж (проверяются в движке на каждой таблице).
/// Управление каталогом и журнал всех пользователей — analytik.manage.
/// </summary>
[ApiController]
[Authorize]
[HasPermission(Permissions.AnalytikUse)]
[AnalytikErrors]
[Route("api/analytik")]
public sealed class AnalytikController(CatalogService catalog, QueryEngine engine, AnalytikAccess access, IAnalytikDb db, IAnalytikSql sql) : ControllerBase
{
    private Task<AnalytikPrincipal> PrincipalAsync(CancellationToken ct) => access.ResolveAsync(User.GetUserId(), ct);

    private QueryCaller Caller(string origin = "api") => new(User.GetUserId(), origin);

    [HttpGet("status")]
    public async Task<AnalytikStatus> Status(CancellationToken ct)
    {
        var principal = await PrincipalAsync(ct);
        var model = await catalog.ModelAsync(ct);
        return new AnalytikStatus(model.RefreshedAt, model.Tables.Count, model.Relations.Count, model.Relations.Count(r => r.Status == RelationStatus.Confirmed),
            model.Tables.Count(t => AnalytikAccess.Decide(t, principal).Allowed), sql.DedicatedReadOnlyRole, principal.Manager, principal.Sales.Description);
    }

    [HttpGet("catalog")]
    public async Task<IReadOnlyList<CatalogTableRow>> Catalog([FromQuery] string? module, [FromQuery] string? q, [FromQuery] bool available, CancellationToken ct) =>
        CatalogViews.Rows(await catalog.ModelAsync(ct), await PrincipalAsync(ct), string.IsNullOrWhiteSpace(module) ? null : module, q, available);

    [HttpGet("catalog/graph")]
    public async Task<CatalogGraph> Graph(CancellationToken ct) => CatalogViews.Graph(await catalog.ModelAsync(ct), await PrincipalAsync(ct));

    [HttpGet("catalog/table/{schema}/{name}")]
    public async Task<ActionResult<CatalogTableDetail>> Table(string schema, string name, CancellationToken ct) =>
        CatalogViews.Detail(await catalog.ModelAsync(ct), $"{schema}.{name}", await PrincipalAsync(ct)) is { } detail
            ? detail
            : NotFound(new { error = $"Таблицы {schema}.{name} нет в каталоге." });

    [HttpGet("catalog/metrics")]
    public IReadOnlyList<MetricKnowledge> Metrics() => CatalogKnowledge.Metrics;

    [HttpPost("catalog/refresh")]
    [HasPermission(Permissions.AnalytikManage)]
    public Task<CatalogRefreshResult> Refresh([FromQuery] bool force, CancellationToken ct) => catalog.RefreshAsync("manual", User.GetUserId(), force, ct);

    [HttpGet("catalog/runs")]
    [HasPermission(Permissions.AnalytikManage)]
    public Task<IReadOnlyList<Domain.Analytik.CatalogRun>> Runs(CancellationToken ct) => catalog.RunsAsync(30, ct);

    [HttpPut("catalog/annotations")]
    [HasPermission(Permissions.AnalytikManage)]
    public async Task<IActionResult> Annotate(AnnotationInput input, CancellationToken ct)
    {
        await catalog.AnnotateAsync(input.Kind, input.Target, input.Title, input.Description, input.Module, input.Access, input.RequiredPermission,
            input.Sensitivity, User.GetUserId(), ct);
        // Описание применяется к каталогу при обновлении — сразу, без смены схемы.
        await catalog.RefreshAsync("manual", User.GetUserId(), false, ct);
        return NoContent();
    }

    /// <summary>Аналитический запрос по типизированному плану (snake_case: from, select, measures, filters, group_by, bucket, order_by, limit).</summary>
    [HttpPost("query")]
    public async Task<ActionResult<QueryOutcome>> Query([FromBody] JsonElement body, CancellationToken ct)
    {
        var outcome = await engine.RunAsync(QueryPlan.Parse(body), Caller(), ct);
        return outcome.Status switch
        {
            "ok" => outcome,
            "denied" => StatusCode(StatusCodes.Status403Forbidden, outcome),
            "error" => StatusCode(StatusCodes.Status500InternalServerError, outcome),
            _ => BadRequest(outcome),
        };
    }

    [HttpGet("profile")]
    public Task<ColumnProfile> Profile([FromQuery] string table, [FromQuery] string column, CancellationToken ct) =>
        engine.ProfileAsync(table, column, Caller(), ct);

    [HttpGet("entity")]
    public Task<EntityView> Entity([FromQuery] string table, [FromQuery] string key, CancellationToken ct) => engine.EntityAsync(table, key, Caller(), ct);

    /// <summary>Журнал запросов: свои; управляющий Аналитиком — всех (all=true).</summary>
    [HttpGet("queries")]
    public async Task<IReadOnlyList<QueryLogRow>> Queries([FromQuery] bool all, [FromQuery] int take, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var manager = User.FindAll(OneBaseClaims.Permission).Any(c => c.Value == Permissions.AnalytikManage);
        var rows = db.Queries.AsNoTracking();
        if (!(all && manager))
        {
            rows = rows.Where(q => q.UserId == userId);
        }

        return await rows.OrderByDescending(q => q.CreatedAt).Take(Math.Clamp(take == 0 ? 50 : take, 1, 500))
            .Select(q => new QueryLogRow(q.Id, q.CreatedAt, q.UserId, q.Origin, q.AgentCode, q.Status, q.Tables, q.Scope, q.RowCount, q.Truncated, q.DurationMs,
                q.Error, q.Plan, q.Sql))
            .ToListAsync(ct);
    }
}
