using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Security;

namespace OneBase.Api.Controllers;

/// <summary>«Настройки → AI → Использование и Журнал»: запросы, токены (вход, кэш, выход), стоимость, модели, пользователи, отделы, сотрудники, задачи.</summary>
[ApiController]
[Route("api/ai")]
[HasPermission(Permissions.AiSettingsManage)]
public sealed class AiUsageController(IAppDbContext db, OneBase.AI.Monitoring.IAiLimits limits) : ControllerBase
{
    /// <summary>Узбекистан — UTC+5: «сегодня» и дни считаются по Ташкенту.</summary>
    private static readonly TimeSpan Offset = TimeSpan.FromHours(5);

    private const string SystemUser = "система";
    private const string DeletedUser = "удалённый пользователь";
    private const string NoDepartment = "без отдела";

    /// <summary>Один вызов модели из ai.Usage. InputTokens — вход без кэша; PromptTokens — весь промпт.</summary>
    private sealed record UsageRow(
        DateTimeOffset CreatedAt,
        Guid? UserId,
        string? AgentCode,
        string Purpose,
        string Provider,
        string Model,
        int InputTokens,
        int OutputTokens,
        int CacheReadTokens,
        int CacheWriteTokens,
        decimal? CostUsd,
        long DurationMs,
        bool Success)
    {
        public int PromptTokens => InputTokens + CacheReadTokens + CacheWriteTokens;
    }

    private sealed record RequestRow(DateTimeOffset CreatedAt, Guid UserId, long DurationMs, string Status, bool FromCache);

    [HttpGet("usage")]
    public async Task<IActionResult> Usage([FromQuery] int days = 30, CancellationToken ct = default)
    {
        days = Math.Clamp(days, 1, 366);
        var todayStart = new DateTimeOffset(DateTimeOffset.UtcNow.ToOffset(Offset).Date, Offset);
        var from = todayStart.AddDays(-(days - 1));
        var fromUtc = from.ToUniversalTime(); // Npgsql пишет в timestamptz только UTC
        var rows = await db.AiUsage.AsNoTracking()
            .Where(u => u.CreatedAt >= fromUtc)
            .Select(u => new UsageRow(u.CreatedAt, u.UserId, u.AgentCode, u.Purpose, u.Provider, u.Model, u.InputTokens, u.OutputTokens, u.CacheReadTokens,
                u.CacheWriteTokens, u.CostUsd, u.DurationMs, u.Success))
            .ToListAsync(ct);
        var requests = await db.AiAuditLogs.AsNoTracking().Where(a => a.CreatedAt >= fromUtc)
            .Select(a => new RequestRow(a.CreatedAt, a.UserId, a.DurationMs, a.Status, a.FromCache)).ToListAsync(ct);
        var userIds = rows.Select(r => r.UserId).Concat(requests.Select(r => (Guid?)r.UserId)).OfType<Guid>().Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.DepartmentId }).ToDictionaryAsync(u => u.Id, ct);
        var departmentIds = users.Values.Select(u => u.DepartmentId).OfType<Guid>().Distinct().ToList();
        var departments = await db.Departments.AsNoTracking().Where(d => departmentIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name, ct);

        string UserName(Guid? id) => id is { } u ? users.GetValueOrDefault(u)?.FullName ?? DeletedUser : SystemUser;
        string DepartmentName(Guid? id) =>
            id is null ? SystemUser
            : users.GetValueOrDefault(id.Value)?.DepartmentId is { } d ? departments.GetValueOrDefault(d) ?? NoDepartment
            : NoDepartment;

        object Totals(IEnumerable<UsageRow> source)
        {
            var list = source.ToList();
            return new
            {
                Calls = list.Count,
                InputTokens = list.Sum(x => (long)x.InputTokens),
                CacheReadTokens = list.Sum(x => (long)x.CacheReadTokens),
                CacheWriteTokens = list.Sum(x => (long)x.CacheWriteTokens),
                PromptTokens = list.Sum(x => (long)x.PromptTokens),
                OutputTokens = list.Sum(x => (long)x.OutputTokens),
                CostUsd = list.Sum(x => x.CostUsd ?? 0),
                CostIncomplete = list.Any(x => x.CostUsd is null),
            };
        }

        var today = rows.Where(r => r.CreatedAt >= todayStart).ToList();
        var budget = await limits.StatusAsync(ct);
        return Ok(new
        {
            From = from,
            Days = days,
            // Бюджет из «Настройки → AI → Общие»: траты сегодня и в этом месяце (по Ташкенту) — только вызовы с известной ценой.
            Budget = new
            {
                budget.DailyUsd,
                budget.MonthlyUsd,
                budget.WarnPercent,
                budget.SpentToday,
                budget.SpentMonth,
                budget.Exceeded,
                Warning = budget.DailyWarning || budget.MonthlyWarning,
            },
            Today = new
            {
                Requests = requests.Count(r => r.CreatedAt >= todayStart),
                FromCache = requests.Count(r => r.CreatedAt >= todayStart && r.FromCache),
                Usage = Totals(today),
            },
            Period = new
            {
                Requests = requests.Count,
                FailedRequests = requests.Count(r => r.Status != "completed"),
                // Ответы из кэша ответов консультанта — без вызовов модели.
                FromCache = requests.Count(r => r.FromCache),
                AvgResponseMs = requests.Count == 0 ? 0 : (long)requests.Average(r => r.DurationMs),
                FailedCalls = rows.Count(r => !r.Success),
                Usage = Totals(rows),
            },
            ByDay = rows
                .GroupBy(r => r.CreatedAt.ToOffset(Offset).Date)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    Date = g.Key.ToString("yyyy-MM-dd"),
                    Requests = requests.Count(r => r.CreatedAt.ToOffset(Offset).Date == g.Key),
                    InputTokens = g.Sum(r => (long)r.InputTokens),
                    CacheReadTokens = g.Sum(r => (long)r.CacheReadTokens),
                    CacheWriteTokens = g.Sum(r => (long)r.CacheWriteTokens),
                    OutputTokens = g.Sum(r => (long)r.OutputTokens),
                    CostUsd = g.Sum(r => r.CostUsd ?? 0),
                }),
            ByModel = rows
                .GroupBy(r => $"{r.Provider}/{r.Model}")
                .OrderByDescending(g => g.Sum(r => (long)r.PromptTokens + r.OutputTokens))
                .Select(g => new
                {
                    Model = g.Key,
                    Calls = g.Count(),
                    Failed = g.Count(r => !r.Success),
                    InputTokens = g.Sum(r => (long)r.InputTokens),
                    CacheReadTokens = g.Sum(r => (long)r.CacheReadTokens),
                    CacheWriteTokens = g.Sum(r => (long)r.CacheWriteTokens),
                    OutputTokens = g.Sum(r => (long)r.OutputTokens),
                    CostUsd = g.Sum(r => r.CostUsd ?? 0),
                    PriceMissing = g.Any(r => r.CostUsd is null),
                    AvgMs = (long)g.Average(r => r.DurationMs),
                }),
            ByUser = rows
                .GroupBy(r => r.UserId)
                .OrderByDescending(g => g.Sum(r => r.CostUsd ?? 0)).ThenByDescending(g => g.Count())
                .Select(g => new
                {
                    User = UserName(g.Key),
                    Requests = requests.Count(r => r.UserId == g.Key),
                    Calls = g.Count(),
                    Tokens = g.Sum(r => (long)r.PromptTokens + r.OutputTokens),
                    CacheReadTokens = g.Sum(r => (long)r.CacheReadTokens),
                    CostUsd = g.Sum(r => r.CostUsd ?? 0),
                }),
            // Отдел — по пользователю на момент отчёта; вызовы фоновых задач без пользователя — «система».
            ByDepartment = rows
                .GroupBy(r => DepartmentName(r.UserId))
                .OrderByDescending(g => g.Sum(r => r.CostUsd ?? 0)).ThenByDescending(g => g.Count())
                .Select(g => new
                {
                    Department = g.Key,
                    Requests = requests.Count(r => DepartmentName(r.UserId) == g.Key),
                    Calls = g.Count(),
                    Tokens = g.Sum(r => (long)r.PromptTokens + r.OutputTokens),
                    CacheReadTokens = g.Sum(r => (long)r.CacheReadTokens),
                    CostUsd = g.Sum(r => r.CostUsd ?? 0),
                }),
            ByAgent = rows
                .GroupBy(r => r.AgentCode ?? "—")
                .OrderByDescending(g => g.Count())
                .Select(g => new
                {
                    Agent = g.Key,
                    Calls = g.Count(),
                    Tokens = g.Sum(r => (long)r.PromptTokens + r.OutputTokens),
                    CacheReadTokens = g.Sum(r => (long)r.CacheReadTokens),
                    CostUsd = g.Sum(r => r.CostUsd ?? 0),
                }),
            ByPurpose = rows
                .GroupBy(r => r.Purpose)
                .OrderByDescending(g => g.Count())
                .Select(g => new
                {
                    Purpose = g.Key,
                    Calls = g.Count(),
                    Tokens = g.Sum(r => (long)r.PromptTokens + r.OutputTokens),
                    CacheReadTokens = g.Sum(r => (long)r.CacheReadTokens),
                    CostUsd = g.Sum(r => r.CostUsd ?? 0),
                }),
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
            User = users.GetValueOrDefault(a.UserId) ?? DeletedUser,
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
            Grounding = a.Grounding is null ? null : JsonDocument.Parse(a.Grounding).RootElement.Clone() as JsonElement?,
            a.CorrectionVerdict,
            a.CorrectionOfMessageId,
        }));
    }
}
