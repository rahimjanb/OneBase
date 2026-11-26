using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OneBase.Application.Abstractions;
using OneBase.Application.Finance;
using OneBase.Domain.Audit;
using OneBase.Finance.Integration.Contracts;

namespace OneBase.Api.Controllers;

/// <summary>
/// Внутренний API для службы интеграции 1С (контейнер onec-worker в сети docker). Вход — только по ключу службы (заголовок
/// X-OneBase-Integration-Token; в базе — его SHA-256). Снаружи закрыт: nginx отвечает 404 на /api/internal/, а запрос, прошедший через nginx
/// (с заголовком X-Real-IP), отклоняется и здесь. Права пользователей OneBase этот вход не даёт — только запись пакетов 1С и состояние службы.
/// </summary>
[ApiController]
[Route("api/internal/onec")]
[AllowAnonymous]
[ServiceFilter(typeof(OneCTokenFilter))]
public sealed class OneCInternalController(OneCSyncService sync, OneCImportService import) : ControllerBase
{
    [HttpPost("heartbeat")]
    public Task<HeartbeatResponse> Heartbeat(WorkerHeartbeat beat, CancellationToken ct) => sync.HeartbeatAsync(beat, ct);

    /// <summary>Подключение к 1С, заданное на странице «1С» (с секретами): служба забирает его, когда меняется версия подключения.</summary>
    [HttpGet("connection")]
    public Task<WorkerConnection> Connection(CancellationToken ct) => sync.ConnectionAsync(ct);

    [HttpPost("commands/{id:guid}/complete")]
    public Task<IActionResult> Complete(Guid id, CommandResult result, CancellationToken ct) =>
        Run(async () => { await sync.CompleteCommandAsync(id, result, ct); return NoContent(); });

    [HttpPost("runs")]
    public Task<StartRunResponse> Start(StartRunRequest request, CancellationToken ct) => sync.StartRunAsync(request, ct);

    [HttpPost("runs/{id:guid}/progress")]
    public Task<IActionResult> Progress(Guid id, RunProgress progress, CancellationToken ct) =>
        Run(async () => Ok(await sync.ProgressAsync(id, progress, ct)));

    [HttpPost("runs/{id:guid}/finish")]
    public Task<IActionResult> Finish(Guid id, FinishRunRequest request, CancellationToken ct) =>
        Run(async () => { await sync.FinishRunAsync(id, request, ct); return NoContent(); });

    /// <summary>Пакет записей. dryRun=true — проверка без записи (тестовая синхронизация).</summary>
    [HttpPost("batches")]
    [RequestSizeLimit(64 * 1024 * 1024)]
    public Task<IActionResult> Batch(ImportBatchRequest request, [FromQuery] bool dryRun, CancellationToken ct) =>
        Run(async () => Ok(await import.ImportAsync(request, dryRun, ct)));

    [HttpGet("totals")]
    public Task<IActionResult> Totals([FromQuery] string stream, [FromQuery] string? organizationId, [FromQuery] DateOnly from, [FromQuery] DateOnly to,
        [FromQuery] string? types, CancellationToken ct) =>
        Run(async () => Ok(await import.TotalsAsync(stream, organizationId ?? string.Empty, from, to, ct,
            types?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))));

    /// <summary>Записи 1С в OneBase для сверки версий: справочник целиком или документы организации за период.</summary>
    [HttpGet("versions")]
    public Task<IActionResult> Versions([FromQuery] string entityType, [FromQuery] string? organizationId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        CancellationToken ct) =>
        Run(async () => Ok(await import.StoredVersionsAsync(entityType, organizationId, from, to, ct)));

    /// <summary>Месяцы с документами организаций в OneBase — для ночной сверки версий.</summary>
    [HttpGet("versions/months")]
    public Task<IActionResult> Months(CancellationToken ct) => Run(async () => Ok(await import.StoredMonthsAsync(ct)));

    [HttpPost("failures")]
    public Task<IActionResult> Failure(FailureReport report, CancellationToken ct) =>
        Run(async () => Ok(new { id = await sync.ReportFailureAsync(report, ct) }));

    [HttpGet("failures/due")]
    public Task<List<FailureItem>> DueFailures([FromQuery] int limit, CancellationToken ct) => sync.DueFailuresAsync(Math.Clamp(limit, 1, 100), ct);

    [HttpPost("failures/{id:guid}/outcome")]
    public Task<IActionResult> Outcome(Guid id, FailureOutcome outcome, CancellationToken ct) =>
        Run(async () => { await sync.FailureOutcomeAsync(id, outcome, ct); return NoContent(); });

    [HttpGet("historical/{id:guid}")]
    public Task<IActionResult> Historical(Guid id, CancellationToken ct) => Run(async () => Ok(await sync.HistoricalAsync(id, ct)));

    [HttpPost("historical/{id:guid}/progress")]
    public Task<IActionResult> HistoricalProgress(Guid id, HistoricalProgressRequest request, CancellationToken ct) =>
        Run(async () => { await sync.HistoricalProgressAsync(id, request, ct); return NoContent(); });

    /// <summary>Ошибки протокола — 400, состояние не позволяет запись — 409; текст ошибки — для журнала службы.</summary>
    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (OneCImportException e)
        {
            return BadRequest(new { error = e.Message });
        }
        catch (OneCConflictException e)
        {
            return Conflict(new { error = e.Message });
        }
    }
}

/// <summary>
/// Проверка ключа службы интеграции 1С. Без ключа или с неверным — 401 (в журнал — без самого ключа); запрос через nginx — 404, как будто
/// адреса нет. Неверные ключи пишутся в аудит не чаще раза в минуту с адреса.
/// </summary>
public sealed class OneCTokenFilter(IOneCSettingsStore settings, IAuditLogger audit, ILogger<OneCTokenFilter> logger, TimeProvider clock) : IAsyncAuthorizationFilter
{
    private static readonly Dictionary<string, DateTimeOffset> LastAudit = [];

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var request = context.HttpContext.Request;
        if (request.Headers.ContainsKey("X-Real-IP") || request.Headers.ContainsKey("CF-Connecting-IP"))
        {
            context.Result = new NotFoundResult();
            return;
        }

        var token = request.Headers[IntegrationHeaders.Token].ToString();
        if (await settings.VerifyTokenAsync(token, context.HttpContext.RequestAborted))
        {
            return;
        }

        var ip = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "?";
        logger.LogWarning("1С: запрос службы интеграции с неверным или пустым ключом с адреса {Ip} ({Path})", ip, request.Path.Value);
        var now = clock.GetUtcNow();
        bool write;
        lock (LastAudit)
        {
            write = !LastAudit.TryGetValue(ip, out var at) || now - at > TimeSpan.FromMinutes(1);
            if (write)
            {
                LastAudit[ip] = now;
            }
        }

        if (write)
        {
            await audit.LogAsync(ActorType.Connection, OneCSettings.Code, "integration.onec.token_rejected", "integration", OneCSettings.Code,
                new { ip, path = request.Path.Value, empty = string.IsNullOrEmpty(token) }, context.HttpContext.RequestAborted);
        }

        context.Result = new UnauthorizedObjectResult(new { error = "Неверный ключ службы интеграции." });
    }
}
