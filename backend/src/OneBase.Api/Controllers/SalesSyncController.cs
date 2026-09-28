using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Security;
using OneBase.Domain.Audit;
using OneBase.Infrastructure.Linko;

namespace OneBase.Api.Controllers;

[ApiController]
[Route("api/sales")]
[HasPermission(Permissions.SalesRead)]
public sealed class SalesSyncController(IAppDbContext db, LinkoSyncCoordinator coordinator, LinkoOptions linko, IAuditLogger audit) : ControllerBase
{
    /// <summary>Когда данные Linko последний раз успешно загружены и нет ли ошибок.</summary>
    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var states = await db.LinkoSyncStates.AsNoTracking().OrderBy(s => s.Entity).ToListAsync(ct);
        var documents = states.Where(s => s.Entity is "orders" or "order_returns" or "visits").ToList();

        return Ok(new
        {
            Configured = linko.IsConfigured,
            IsRunning = coordinator.IsRunning,
            DataAsOf = documents.Count == 3 && documents.All(s => s.LastSuccessAt != null)
                ? documents.Min(s => s.LastSuccessAt)
                : null,
            HasErrors = states.Any(s => s.LastError != null),
            Entities = states.Select(s => new { s.Entity, s.LastRunAt, s.LastSuccessAt, s.LastRows, s.LastError }),
        });
    }

    /// <summary>Кнопка «Обновить»: запускает синхронизацию в фоне.</summary>
    [HttpPost("sync")]
    [HasPermission(Permissions.SalesManage)]
    public async Task<IActionResult> Sync([FromQuery] bool full = false, CancellationToken ct = default)
    {
        if (!linko.IsConfigured)
        {
            return Problem("Linko не настроен: задайте LINKO_BASE_URL и LINKO_TOKEN в .env.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (!coordinator.TryStartInBackground(full))
        {
            return Conflict(new { error = "Синхронизация уже идёт." });
        }

        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "sales.sync.started", data: new { full }, cancellationToken: ct);
        return Accepted();
    }
}
