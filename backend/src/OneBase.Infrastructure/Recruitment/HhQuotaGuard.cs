using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.Application.Recruitment.Hh;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.Recruitment;

/// <summary>Сводка Quota Guard для экрана: счётчики за 30 дней, предохранитель, каталог операций, последние записи журнала.</summary>
public sealed record HhGuardView(
    int FreeCalls30,
    int BlockedCalls30,
    int FailedCalls30,
    int CallsToday,
    int DailyCap,
    DateTimeOffset? GuardTrippedAt,
    string? GuardReason,
    DateTimeOffset? LastSyncAt,
    IReadOnlyList<HhOperation> Catalog,
    IReadOnlyList<HhCallView> Journal);

public sealed record HhCallView(DateTimeOffset At, string Operation, string Method, string Path, string Cost, string Trigger, bool Allowed, string? Reason, int? Status,
    string? ErrorType, int DurationMs);

/// <summary>
/// Quota Guard — перед каждым запросом к HH: (1) операция есть в каталоге и бесплатна (не расходует платные услуги и лимиты HH) — иначе блок
/// до отправки; (2) разрешена этим способом (автоматически или кнопкой HR); (3) не стоит предохранитель (HH ответил, что нужна оплата или
/// лимит исчерпан); (4) не превышен суточный предел OneBase. Неизвестная стоимость — блок. Каждая попытка — в журнал HhApiCalls (без тел
/// запросов). Ответ HH о деньгах или лимитах ставит предохранитель: автоматическая синхронизация останавливается до решения HR.
/// Журнал пишется отдельным INSERT, а предохранитель — ExecuteUpdate: незавершённые изменения HhService так не сохраняются раньше времени.
/// </summary>
public sealed class HhQuotaGuard(OneBaseDbContext db, TimeProvider clock, ILogger<HhQuotaGuard> logger)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static DateTimeOffset _last = DateTimeOffset.MinValue;

    public async Task<T> RunAsync<T>(string operation, string path, Func<Task<T>> call, CancellationToken ct)
    {
        var manual = HhCallScope.Manual;
        var (allowed, reason) = HhOperations.Check(operation, manual);
        if (allowed)
        {
            (allowed, reason) = await StateAsync(operation, manual, ct);
        }

        var op = HhOperations.Get(operation);
        if (!allowed)
        {
            await LogAsync(op, path, manual, false, reason, null, null, 0, ct);
            logger.LogInformation("HH: {Operation} заблокирована Quota Guard — {Reason}", operation, reason);
            throw new HhBlockedException(operation, reason!);
        }

        await PaceAsync(ct);
        var watch = Stopwatch.StartNew();
        try
        {
            var result = await call();
            await LogAsync(op, path, manual, true, null, 200, null, (int)watch.ElapsedMilliseconds, ct);
            return result;
        }
        catch (HhApiException ex)
        {
            var type = ex.Value is { } v ? $"{ex.Type}:{v}" : ex.Type;
            await LogAsync(op, path, manual, true, ex.Message, ex.Status, type, (int)watch.ElapsedMilliseconds, ct);
            if (HhOperations.IsBillingError(ex.Type, ex.Value))
            {
                await TripAsync($"{ex.Message} (операция {operation})", ct);
            }

            throw;
        }
    }

    /// <summary>Записать заблокированную попытку без вызова (например, резюме из отклика: по каталогу не загружается).</summary>
    public async Task<string> BlockedAsync(string operation, string path, CancellationToken ct)
    {
        var (_, reason) = HhOperations.Check(operation, HhCallScope.Manual);
        reason ??= "Заблокировано.";
        await LogAsync(HhOperations.Get(operation), path, HhCallScope.Manual, false, reason, null, null, 0, ct);
        return reason;
    }

    private async Task<(bool, string?)> StateAsync(string operation, bool manual, CancellationToken ct)
    {
        var row = await db.HhConnections.AsNoTracking().Where(c => c.Id == 1).Select(c => new { c.GuardTrippedAt, c.GuardReason }).FirstOrDefaultAsync(ct);
        if (row?.GuardTrippedAt is not null && !manual && operation is not (HhOperations.Token or HhOperations.Me))
        {
            return (false, $"Предохранитель: {row.GuardReason} Автоматические запросы остановлены, пока HR не нажмёт «Проверить снова».");
        }

        var since = clock.GetUtcNow().AddDays(-1);
        if (await db.HhApiCalls.CountAsync(c => c.Allowed && c.At >= since, ct) >= HhOperations.DailyCap)
        {
            return (false, $"Суточный предел запросов OneBase к HH ({HhOperations.DailyCap}) исчерпан — продолжим завтра.");
        }

        return (true, null);
    }

    /// <summary>Не чаще одного запроса в 250 мс на сервер (HH лимитов не публикует; лишних запросов не делаем).</summary>
    private static async Task PaceAsync(CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var wait = _last + HhOperations.MinInterval - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, ct);
            }

            _last = DateTimeOffset.UtcNow;
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task TripAsync(string reason, CancellationToken ct)
    {
        var text = reason.Length <= 500 ? reason : reason[..500];
        await db.HhConnections.Where(c => c.Id == 1 && c.GuardTrippedAt == null)
            .ExecuteUpdateAsync(u => u.SetProperty(c => c.GuardTrippedAt, clock.GetUtcNow()).SetProperty(c => c.GuardReason, text), ct);
        logger.LogWarning("HH: предохранитель расхода — {Reason}", text);
    }

    private async Task LogAsync(HhOperation op, string path, bool manual, bool allowed, string? reason, int? status, string? errorType, int durationMs,
        CancellationToken ct)
    {
        var at = clock.GetUtcNow();
        var clean = Cut(path.Split('?')[0], 300)!;
        var trigger = manual ? "manual" : "auto";
        var actor = HhCallScope.ActorId;
        var why = Cut(reason, 500);
        var error = Cut(errorType, 80);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO recruitment."HhApiCalls" ("At", "Operation", "Method", "Path", "Cost", "Trigger", "Allowed", "Reason", "Status", "ErrorType", "DurationMs", "ActorId")
            VALUES ({at}, {op.Code}, {Cut(op.Method, 8)}, {clean}, {op.Cost}, {trigger}, {allowed}, {why}, {status}, {error}, {durationMs}, {actor})
            """, ct);
    }

    private static string? Cut(string? s, int max) => s is null ? null : s.Length <= max ? s : s[..max];

    // ---------- Экран ----------

    public async Task<HhGuardView> ViewAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var month = now.AddDays(-30);
        var day = now.AddDays(-1);
        var counts = await db.HhApiCalls.AsNoTracking().Where(c => c.At >= month)
            .GroupBy(c => new { c.Allowed, Failed = c.Status != null && c.Status >= 400 })
            .Select(g => new { g.Key.Allowed, g.Key.Failed, Count = g.Count() }).ToListAsync(ct);
        var today = await db.HhApiCalls.CountAsync(c => c.Allowed && c.At >= day, ct);
        var row = await db.HhConnections.AsNoTracking().FirstOrDefaultAsync(c => c.Id == 1, ct);
        var journal = await db.HhApiCalls.AsNoTracking().OrderByDescending(c => c.Id).Take(50)
            .Select(c => new HhCallView(c.At, c.Operation, c.Method, c.Path, c.Cost, c.Trigger, c.Allowed, c.Reason, c.Status, c.ErrorType, c.DurationMs)).ToListAsync(ct);
        return new HhGuardView(counts.Where(c => c.Allowed && !c.Failed).Sum(c => c.Count), counts.Where(c => !c.Allowed).Sum(c => c.Count),
            counts.Where(c => c.Allowed && c.Failed).Sum(c => c.Count), today, HhOperations.DailyCap, row?.GuardTrippedAt, row?.GuardReason, row?.LastSyncAt,
            HhOperations.All, journal);
    }
}
