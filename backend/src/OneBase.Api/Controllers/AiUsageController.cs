using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Security;

namespace OneBase.Api.Controllers;

/// <summary>«Настройки → AI → Использование и Журнал»: запросы, токены, стоимость, модели, пользователи, агенты.</summary>
[ApiController]
[Route("api/ai")]
[HasPermission(Permissions.AiSettingsManage)]
public sealed class AiUsageController(IAppDbContext db) : ControllerBase
{
    /// <summary>Узбекистан — UTC+5: «сегодня» и дни считаются по Ташкенту.</summary>
    private static readonly TimeSpan Offset = TimeSpan.FromHours(5);

    [HttpGet("usage")]
    public async Task<IActionResult> Usage([FromQuery] int days = 30, CancellationToken ct = default)
    {
        days = Math.Clamp(days, 1, 366);
        var todayStart = new DateTimeOffset(DateTimeOffset.UtcNow.ToOffset(Offset).Date, Offset);
        var from = todayStart.AddDays(-(days - 1));
        var fromUtc = from.ToUniversalTime(); // Npgsql пишет в timestamptz только UTC
        var rows = await db.AiUsage.AsNoTracking()
            .Where(u => u.CreatedAt >= fromUtc)
            .Select(u => new { u.CreatedAt, u.UserId, u.AgentCode, u.Purpose, u.Provider, u.Model, u.InputTokens, u.OutputTokens, u.CostUsd, u.DurationMs, u.Success })
            .ToListAsync(ct);
        var requests = await db.AiAuditLogs.AsNoTracking().Where(a => a.CreatedAt >= fromUtc)
            .Select(a => new { a.CreatedAt, a.UserId, a.DurationMs, a.Status }).ToListAsync(ct);
        var userIds = rows.Select(r => r.UserId).Concat(requests.Select(r => (Guid?)r.UserId)).OfType<Guid>().Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        object Totals<T>(IEnumerable<T> source, Func<T, int> input, Func<T, int> output, Func<T, decimal?> cost)
        {
            var list = source.ToList();
            return new
            {
                Calls = list.Count,
                InputTokens = list.Sum(input),
                OutputTokens = list.Sum(output),
                CostUsd = list.Sum(x => cost(x) ?? 0),
                CostIncomplete = list.Any(x => cost(x) is null),
            };
        }

        var today = rows.Where(r => r.CreatedAt >= todayStart).ToList();
        return Ok(new
        {
            From = from,
            Days = days,
            Today = new
            {
                Requests = requests.Count(r => r.CreatedAt >= todayStart),
                Usage = Totals(today, r => r.InputTokens, r => r.OutputTokens, r => r.CostUsd),
            },
            Period = new
            {
                Requests = requests.Count,
                FailedRequests = requests.Count(r => r.Status != "completed"),
                AvgResponseMs = requests.Count == 0 ? 0 : (long)requests.Average(r => r.DurationMs),
                FailedCalls = rows.Count(r => !r.Success),
                Usage = Totals(rows, r => r.InputTokens, r => r.OutputTokens, r => r.CostUsd),
            },
            ByDay = rows
                .GroupBy(r => r.CreatedAt.ToOffset(Offset).Date)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    Date = g.Key.ToString("yyyy-MM-dd"),
                    Requests = requests.Count(r => r.CreatedAt.ToOffset(Offset).Date == g.Key),
                    InputTokens = g.Sum(r => r.InputTokens),
                    OutputTokens = g.Sum(r => r.OutputTokens),
                    CostUsd = g.Sum(r => r.CostUsd ?? 0),
                }),
            ByModel = rows
                .GroupBy(r => $"{r.Provider}/{r.Model}")
                .OrderByDescending(g => g.Sum(r => r.InputTokens + r.OutputTokens))
                .Select(g => new
                {
                    Model = g.Key,
                    Calls = g.Count(),
                    Failed = g.Count(r => !r.Success),
                    InputTokens = g.Sum(r => r.InputTokens),
                    OutputTokens = g.Sum(r => r.OutputTokens),
                    CostUsd = g.Sum(r => r.CostUsd ?? 0),
                    PriceMissing = g.Any(r => r.CostUsd is null),
                    AvgMs = (long)g.Average(r => r.DurationMs),
                }),
            ByUser = rows
                .GroupBy(r => r.UserId)
                .OrderByDescending(g => g.Sum(r => r.CostUsd ?? 0)).ThenByDescending(g => g.Count())
                .Select(g => new
                {
                    User = g.Key is { } id ? users.GetValueOrDefault(id) ?? "удалённый пользователь" : "система",
                    Requests = requests.Count(r => r.UserId == g.Key),
                    Calls = g.Count(),
                    Tokens = g.Sum(r => r.InputTokens + r.OutputTokens),
                    CostUsd = g.Sum(r => r.CostUsd ?? 0),
                }),
            ByAgent = rows
                .GroupBy(r => r.AgentCode ?? "—")
                .OrderByDescending(g => g.Count())
                .Select(g => new { Agent = g.Key, Calls = g.Count(), Tokens = g.Sum(r => r.InputTokens + r.OutputTokens), CostUsd = g.Sum(r => r.CostUsd ?? 0) }),
            ByPurpose = rows
                .GroupBy(r => r.Purpose)
                .OrderByDescending(g => g.Count())
                .Select(g => new { Purpose = g.Key, Calls = g.Count(), Tokens = g.Sum(r => r.InputTokens + r.OutputTokens), CostUsd = g.Sum(r => r.CostUsd ?? 0) }),
        });
    }

    [HttpGet("logs")]
    public async Task<IActionResult> Logs([FromQuery] DateTimeOffset? before, [FromQuery] string? status, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);
        var query = db.AiAuditLogs.AsNoTracking().AsQueryable();
        if (before is { } b)
        {
            var beforeUtc = b.ToUniversalTime();
            query = query.Where(a => a.CreatedAt < beforeUtc);
        }

        if (status is "completed" or "failed")
        {
            query = query.Where(a => a.Status == status);
        }

        var rows = await query.OrderByDescending(a => a.CreatedAt).Take(take).ToListAsync(ct);
        var ids = rows.Select(r => r.UserId).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        return Ok(rows.Select(a => new
        {
            a.Id,
            a.CreatedAt,
            User = users.GetValueOrDefault(a.UserId) ?? "удалённый пользователь",
            a.Kind,
            a.ConversationId,
            a.Question,
            Agents = a.Agents?.Split(',') ?? [],
            a.Model,
            Tools = a.Tools?.Split(',') ?? [],
            Sources = a.Sources is null ? null : JsonDocument.Parse(a.Sources).RootElement.Clone() as JsonElement?,
            a.InputTokens,
            a.OutputTokens,
            a.CostUsd,
            a.DurationMs,
            a.Status,
            a.ResponsePreview,
            a.Error,
        }));
    }
}
