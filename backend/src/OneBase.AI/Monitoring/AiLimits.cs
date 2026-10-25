using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneBase.AI.Gateway;
using OneBase.AI.Llm;
using OneBase.Application.Abstractions;
using OneBase.Application.Security;
using OneBase.Domain.Work;

namespace OneBase.AI.Monitoring;

/// <summary>Траты на AI за сегодня и месяц (по Ташкенту) против бюджета из «Настройки → AI → Общие». Бюджет null — без ограничения.</summary>
public sealed record AiBudgetStatus(decimal SpentToday, decimal SpentMonth, decimal? DailyUsd, decimal? MonthlyUsd, int WarnPercent)
{
    public bool DailyExceeded => DailyUsd is { } d && SpentToday >= d;
    public bool MonthlyExceeded => MonthlyUsd is { } m && SpentMonth >= m;
    public bool Exceeded => DailyExceeded || MonthlyExceeded;
    public bool DailyWarning => DailyUsd is { } d && d > 0 && SpentToday >= d * WarnPercent / 100m;
    public bool MonthlyWarning => MonthlyUsd is { } m && m > 0 && SpentMonth >= m * WarnPercent / 100m;

    /// <summary>Почему новые вызовы моделей не выполняются — для пользователя; null — бюджет не исчерпан.</summary>
    public string? BlockMessage =>
        DailyExceeded ? $"Дневной бюджет на AI исчерпан (${SpentToday:0.##} из ${DailyUsd:0.##}). Запросы к AI возобновятся завтра или после увеличения бюджета в «Настройки → AI → Общие»."
        : MonthlyExceeded ? $"Месячный бюджет на AI исчерпан (${SpentMonth:0.##} из ${MonthlyUsd:0.##}). Запросы к AI возобновятся в следующем месяце или после увеличения бюджета в «Настройки → AI → Общие»."
        : null;
}

/// <summary>Ограничения вызовов моделей: бюджет в USD (день и месяц) и число одновременных вызовов. Реализация — <see cref="AiLimits"/>.</summary>
public interface IAiLimits
{
    /// <summary>Бросает LlmNotConfiguredException с понятным текстом, если бюджет исчерпан.</summary>
    Task EnsureBudgetAsync(CancellationToken ct = default);

    /// <summary>Место для одного вызова модели: если одновременных вызовов уже столько, сколько разрешено, — ждёт освобождения.</summary>
    Task<IDisposable> EnterAsync(CancellationToken ct = default);

    /// <summary>Учесть стоимость записанного вызова: обновить траты и при переходе порога предупреждения или бюджета уведомить администраторов AI.</summary>
    Task SpentAsync(decimal costUsd, CancellationToken ct = default);

    Task<AiBudgetStatus> StatusAsync(CancellationToken ct = default);
}

public sealed class AiLimits(IServiceScopeFactory scopes, IAiSettingsSource settings, TimeProvider time, ILogger<AiLimits> logger) : IAiLimits
{
    /// <summary>Узбекистан — UTC+5: день и месяц бюджета — по Ташкенту.</summary>
    private static readonly TimeSpan Offset = TimeSpan.FromHours(5);

    /// <summary>Как долго траты берутся из памяти: между перечитываниями они обновляются стоимостью каждого записанного вызова.</summary>
    private static readonly TimeSpan Refresh = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim _load = new(1, 1);
    private readonly object _gate = new();
    private Spent? _spent;
    private (int Limit, SemaphoreSlim Slots)? _slots;

    private sealed record Spent(DateTimeOffset Day, DateTimeOffset Month, decimal Today, decimal ThisMonth, DateTimeOffset LoadedAt);

    public async Task EnsureBudgetAsync(CancellationToken ct = default)
    {
        var status = await StatusAsync(ct);
        if (status.BlockMessage is { } message)
        {
            throw new LlmNotConfiguredException(message);
        }
    }

    public async Task<IDisposable> EnterAsync(CancellationToken ct = default)
    {
        var limit = (await settings.GetSettingsAsync(ct)).MaxConcurrentCalls;
        if (limit <= 0)
        {
            return NoLease.Instance;
        }

        SemaphoreSlim slots;
        lock (_gate)
        {
            // Лимит поменяли в настройках — новые вызовы идут через новый семафор; уже начатые освобождают свой.
            if (_slots is not { } current || current.Limit != limit)
            {
                _slots = (limit, new SemaphoreSlim(limit, limit));
            }

            slots = _slots.Value.Slots;
        }

        await slots.WaitAsync(ct);
        return new Lease(slots);
    }

    public async Task<AiBudgetStatus> StatusAsync(CancellationToken ct = default)
    {
        var s = await settings.GetSettingsAsync(ct);
        var spent = await SpentNowAsync(ct);
        return new AiBudgetStatus(spent.Today, spent.ThisMonth, s.DailyBudgetUsd, s.MonthlyBudgetUsd, s.BudgetWarnPercent);
    }

    public async Task SpentAsync(decimal costUsd, CancellationToken ct = default)
    {
        if (costUsd <= 0)
        {
            return;
        }

        var before = await StatusAsync(ct);
        lock (_gate)
        {
            if (_spent is { } s)
            {
                _spent = s with { Today = s.Today + costUsd, ThisMonth = s.ThisMonth + costUsd };
            }
        }

        var after = await StatusAsync(ct);
        var crossed = new List<(string Title, string Body)>();
        if (after.DailyExceeded && !before.DailyExceeded)
        {
            crossed.Add(("Дневной бюджет на AI исчерпан", $"Потрачено ${after.SpentToday:0.##} из ${after.DailyUsd:0.##}. Новые запросы к AI до конца дня не выполняются."));
        }
        else if (after.DailyWarning && !before.DailyWarning && !after.DailyExceeded)
        {
            crossed.Add(($"Потрачено {after.WarnPercent}% дневного бюджета на AI", $"${after.SpentToday:0.##} из ${after.DailyUsd:0.##}."));
        }

        if (after.MonthlyExceeded && !before.MonthlyExceeded)
        {
            crossed.Add(("Месячный бюджет на AI исчерпан", $"Потрачено ${after.SpentMonth:0.##} из ${after.MonthlyUsd:0.##}. Новые запросы к AI до конца месяца не выполняются."));
        }
        else if (after.MonthlyWarning && !before.MonthlyWarning && !after.MonthlyExceeded)
        {
            crossed.Add(($"Потрачено {after.WarnPercent}% месячного бюджета на AI", $"${after.SpentMonth:0.##} из ${after.MonthlyUsd:0.##}."));
        }

        foreach (var (title, body) in crossed)
        {
            logger.LogWarning("AI: {Title}. {Body}", title, body);
            await NotifyAsync(title, body, ct);
        }
    }

    private async Task<Spent> SpentNowAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var local = now.ToOffset(Offset);
        var day = new DateTimeOffset(local.Date, Offset);
        var month = new DateTimeOffset(local.Year, local.Month, 1, 0, 0, 0, Offset);
        if (_spent is { } cached && cached.Day == day && now - cached.LoadedAt < Refresh)
        {
            return cached;
        }

        await _load.WaitAsync(ct);
        try
        {
            if (_spent is { } fresh && fresh.Day == day && now - fresh.LoadedAt < Refresh)
            {
                return fresh;
            }

            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var monthUtc = month.ToUniversalTime();
            var dayUtc = day.ToUniversalTime();
            var rows = await db.AiUsage.AsNoTracking()
                .Where(u => u.CreatedAt >= monthUtc && u.CostUsd != null)
                .GroupBy(u => u.CreatedAt >= dayUtc)
                .Select(g => new { Today = g.Key, Cost = g.Sum(u => u.CostUsd ?? 0) })
                .ToListAsync(ct);
            var today = rows.Where(r => r.Today).Sum(r => r.Cost);
            var spent = new Spent(day, month, today, rows.Sum(r => r.Cost), now);
            lock (_gate)
            {
                _spent = spent;
            }

            return spent;
        }
        finally
        {
            _load.Release();
        }
    }

    /// <summary>Уведомление (колокольчик) тем, кто управляет AI; одно и то же — не чаще раза за день.</summary>
    private async Task NotifyAsync(string title, string body, CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var dayUtc = new DateTimeOffset(time.GetUtcNow().ToOffset(Offset).Date, Offset).ToUniversalTime();
            if (await db.UserNotifications.AnyAsync(n => n.Kind == UserNotificationKind.AiBudget && n.Title == title && n.CreatedAt >= dayUtc, ct))
            {
                return;
            }

            var recipients = await db.Users.AsNoTracking()
                .Where(u => u.IsActive && u.Roles.Any(r => r.Role.Permissions.Any(p => p.Code == Permissions.AiSettingsManage)))
                .Select(u => u.Id)
                .ToListAsync(ct);
            foreach (var recipient in recipients)
            {
                db.UserNotifications.Add(new UserNotification
                {
                    RecipientId = recipient, Kind = UserNotificationKind.AiBudget, Title = title, Body = body, Link = "/settings/ai/usage",
                });
            }

            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "AI: не удалось уведомить о бюджете");
        }
    }

    private sealed class Lease(SemaphoreSlim slots) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                slots.Release();
            }
        }
    }

    private sealed class NoLease : IDisposable
    {
        public static readonly NoLease Instance = new();

        public void Dispose()
        {
        }
    }
}

/// <summary>Без ограничений — для тестов и инструментов, где бюджет не нужен.</summary>
public sealed class NoAiLimits : IAiLimits
{
    public static readonly NoAiLimits Instance = new();

    public Task EnsureBudgetAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task<IDisposable> EnterAsync(CancellationToken ct = default) => Task.FromResult<IDisposable>(new Empty());

    public Task SpentAsync(decimal costUsd, CancellationToken ct = default) => Task.CompletedTask;

    public Task<AiBudgetStatus> StatusAsync(CancellationToken ct = default) => Task.FromResult(new AiBudgetStatus(0, 0, null, null, 80));

    private sealed class Empty : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
