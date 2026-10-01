using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Security;
using OneBase.Domain.Audit;

namespace OneBase.Api.Controllers;

/// <summary>«Настройки → Журнал ошибок»: ошибки интеграции Linko, синхронизации и сервера. Только администратор.</summary>
[ApiController]
[Route("api/logs")]
[HasPermission(Permissions.LogsRead)]
public sealed class LogsController(IAppDbContext db) : ControllerBase
{
    private const int PageSize = 100;

    private static readonly string[] Sources = [SystemLogSources.Linko, SystemLogSources.Sync, SystemLogSources.System];

    /// <summary>
    /// Записи за последние days дней, новые сверху. source — linko | sync | system; level — warning (предупреждения)
    /// или error (ошибки и критические). beforeId — следующая страница.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? source, [FromQuery] string? level, [FromQuery] int days = 7,
        [FromQuery] long? beforeId = null, CancellationToken ct = default)
    {
        days = Math.Clamp(days, 1, 90);
        var since = DateTimeOffset.UtcNow.AddDays(-days);
        var period = db.SystemLogs.AsNoTracking().Where(l => l.Timestamp >= since);

        var counts = await period
            .GroupBy(l => new { l.Source, Error = l.Level != "Warning" })
            .Select(g => new { g.Key.Source, g.Key.Error, Count = g.Count() })
            .ToListAsync(ct);

        var query = period;
        if (source is not null && Sources.Contains(source))
        {
            query = query.Where(l => l.Source == source);
        }

        query = level switch
        {
            "warning" => query.Where(l => l.Level == "Warning"),
            "error" => query.Where(l => l.Level != "Warning"),
            _ => query,
        };

        if (beforeId is { } before)
        {
            query = query.Where(l => l.Id < before);
        }

        var items = await query.OrderByDescending(l => l.Id).Take(PageSize + 1).ToListAsync(ct);
        return Ok(new
        {
            Days = days,
            Items = items.Take(PageSize).Select(l => new { l.Id, l.Timestamp, l.Level, l.Source, l.Category, l.Message, l.Exception, l.TraceId }),
            HasMore = items.Count > PageSize,
            Counts = Sources.ToDictionary(s => s, s => new
            {
                Errors = counts.Where(c => c.Source == s && c.Error).Sum(c => c.Count),
                Warnings = counts.Where(c => c.Source == s && !c.Error).Sum(c => c.Count),
            }),
        });
    }
}
