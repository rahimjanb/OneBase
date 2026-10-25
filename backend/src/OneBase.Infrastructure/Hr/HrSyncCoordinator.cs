using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneBase.Domain.Hr;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.Hr;

public enum HrSyncStartResult
{
    Started,
    AlreadyRunning,
    NotConfigured,
}

/// <summary>
/// Запуск синхронизации Verifix без параллельных прогонов: в процессе — семафор, между экземплярами API — строка журнала со статусом
/// Running (уникальный индекс: вторая такая строка не вставится). Запуск, который перестал отмечаться дольше StaleAfter (сервер
/// перезапустили посреди загрузки), закрывается как прерванный — иначе новые запуски ждали бы его вечно.
/// </summary>
public sealed class HrSyncCoordinator(IServiceScopeFactory scopes, VerifixSettingsStore settings, TimeProvider time, ILogger<HrSyncCoordinator> logger)
{
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(15);

    private static readonly JsonSerializerOptions StepsJson = new(JsonSerializerDefaults.Web);

    private readonly SemaphoreSlim _gate = new(1, 1);

    public bool IsRunning => _gate.CurrentCount == 0;

    /// <summary>Запускает и ждёт окончания (фон по расписанию, тесты). Возвращает запуск или причину, по которой он не начат.</summary>
    public async Task<(HrSyncStartResult Result, Guid? RunId)> RunAsync(string trigger, Guid? userId, Guid? retryOf, CancellationToken ct)
    {
        var (result, runId) = await BeginAsync(trigger, userId, retryOf, ct);
        if (result != HrSyncStartResult.Started)
        {
            return (result, null);
        }

        await ExecuteAsync(runId!.Value, ct);
        return (result, runId);
    }

    /// <summary>Кнопка «Синхронизировать» и «Повторить»: запуск в фоне, ответ сразу.</summary>
    public async Task<(HrSyncStartResult Result, Guid? RunId)> StartInBackgroundAsync(string trigger, Guid? userId, Guid? retryOf, CancellationToken ct)
    {
        var (result, runId) = await BeginAsync(trigger, userId, retryOf, ct);
        if (result == HrSyncStartResult.Started)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await ExecuteAsync(runId!.Value, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Фоновая синхронизация Verifix упала");
                }
            }, CancellationToken.None);
        }

        return (result, runId);
    }

    private async Task<(HrSyncStartResult, Guid?)> BeginAsync(string trigger, Guid? userId, Guid? retryOf, CancellationToken ct)
    {
        if (!(await settings.GetAsync(ct)).IsReady)
        {
            return (HrSyncStartResult.NotConfigured, null);
        }

        if (!await _gate.WaitAsync(0, ct))
        {
            return (HrSyncStartResult.AlreadyRunning, null);
        }

        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var now = time.GetUtcNow();
            var stale = now - StaleAfter;
            var closed = await db.HrSyncRuns.Where(r => r.Status == HrSyncStatus.Running && r.HeartbeatAt < stale)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, HrSyncStatus.Failed)
                    .SetProperty(r => r.FinishedAt, now)
                    .SetProperty(r => r.Message, "Прервана: сервер перезапустился или запуск перестал отвечать. Загруженное сохранено."), ct);
            if (closed > 0)
            {
                logger.LogWarning("Verifix: закрыт зависший запуск синхронизации ({Count})", closed);
            }

            var run = new HrSyncRun { Trigger = trigger, StartedById = userId, RetryOfId = retryOf, StartedAt = now, HeartbeatAt = now };
            db.HrSyncRuns.Add(run);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Другой экземпляр API уже синхронизирует.
                _gate.Release();
                return (HrSyncStartResult.AlreadyRunning, null);
            }

            return (HrSyncStartResult.Started, run.Id);
        }
        catch
        {
            _gate.Release();
            throw;
        }
    }

    private async Task ExecuteAsync(Guid runId, CancellationToken ct)
    {
        string status;
        string? message;
        HrSyncOutcome? outcome = null;
        try
        {
            using var scope = scopes.CreateScope();
            outcome = await scope.ServiceProvider.GetRequiredService<HrSyncService>().RunAsync(runId, ct);
            var failed = outcome.Steps.Count(s => s.Status == "failed");
            var ok = outcome.Steps.Count(s => s.Status == "ok");
            status = failed == 0 ? HrSyncStatus.Succeeded : ok > 0 && !outcome.AuthFailed ? HrSyncStatus.Partial : HrSyncStatus.Failed;
            message = failed == 0
                ? "Синхронизация завершена."
                : string.Join("; ", outcome.Steps.Where(s => s.Status == "failed").Select(s => $"{StepTitle(s.Step)}: {s.Message}"));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            status = HrSyncStatus.Failed;
            message = "Остановлена: сервер OneBase завершает работу. Загруженное сохранено.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Синхронизация Verifix упала");
            status = HrSyncStatus.Failed;
            message = "Ошибка OneBase при синхронизации: " + VerifixLog.Safe(ex.Message);
        }

        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var run = await db.HrSyncRuns.FirstAsync(r => r.Id == runId, CancellationToken.None);
            run.Status = status;
            run.FinishedAt = time.GetUtcNow();
            run.HeartbeatAt = run.FinishedAt.Value;
            run.Message = message is { Length: > 1000 } ? message[..1000] : message;
            if (outcome is not null)
            {
                run.Created = outcome.Steps.Sum(s => s.Created);
                run.Updated = outcome.Steps.Sum(s => s.Updated);
                run.Skipped = outcome.Steps.Sum(s => s.Skipped);
                run.Errors = outcome.Steps.Sum(s => s.Errors);
                run.PeriodFrom = outcome.PeriodFrom;
                run.PeriodTo = outcome.PeriodTo;
                run.StepsJson = JsonSerializer.Serialize(outcome.Steps, StepsJson);
            }
            else
            {
                run.Errors = Math.Max(1, run.Errors);
            }

            await db.SaveChangesAsync(CancellationToken.None);
        }
        finally
        {
            _gate.Release();
        }

        if (status != HrSyncStatus.Succeeded)
        {
            logger.LogWarning("Verifix: синхронизация {Status} — {Message}", status, message);
        }
    }

    public static string StepTitle(string step) => step switch
    {
        HrReferenceKinds.Division => "подразделения",
        HrReferenceKinds.Job => "должности",
        HrReferenceKinds.Schedule => "графики",
        HrReferenceKinds.Location => "локации",
        HrReferenceKinds.TimeKind => "виды времени",
        HrSyncService.EmployeesState => "сотрудники",
        HrSyncService.TracksState => "отметки",
        HrSyncService.TimesheetState => "табель",
        "links" => "привязка",
        _ => step,
    };
}

/// <summary>
/// Синхронизация Verifix по расписанию: раз в IntervalMinutes из настроек интеграции (включается флажком «Автосинхронизация»). Запуск
/// не прошёл — повтор через RetryDelay, не больше MaxRetries раз подряд, затем обычный интервал. Настройки читаются перед каждой проверкой —
/// включить, выключить и поменять интервал можно без перезапуска. Verifix:WorkerEnabled = false — только кнопка.
/// </summary>
internal sealed class HrSyncWorker(
    HrSyncCoordinator coordinator,
    VerifixSettingsStore settings,
    VerifixOptions options,
    IServiceScopeFactory scopes,
    TimeProvider time,
    ILogger<HrSyncWorker> logger) : BackgroundService
{
    public static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(5);
    public const int MaxRetries = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.WorkerEnabled)
        {
            logger.LogInformation("Фоновая синхронизация Verifix выключена (Verifix:WorkerEnabled) — данные обновляет только кнопка");
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(20), time, stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var connection = await settings.GetAsync(stoppingToken);
                if (connection.IsReady && connection.Data.AutoSync && !coordinator.IsRunning)
                {
                    var (due, retryOf) = await DueAsync(connection.Data.IntervalMinutes, stoppingToken);
                    if (due)
                    {
                        await coordinator.RunAsync(retryOf is null ? HrSyncTrigger.Auto : HrSyncTrigger.Retry, null, retryOf, stoppingToken);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Verifix: проверка расписания синхронизации не выполнена");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), time, stoppingToken);
        }
    }

    /// <summary>Пора ли запускать: интервал прошёл; после неудачи — короткая пауза и повтор (не больше MaxRetries подряд).</summary>
    private async Task<(bool Due, Guid? RetryOf)> DueAsync(int intervalMinutes, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var recent = await db.HrSyncRuns.AsNoTracking().OrderByDescending(r => r.StartedAt).Take(MaxRetries + 1).ToListAsync(ct);
        return HrSyncSchedule.Due(recent, intervalMinutes, time.GetUtcNow());
    }
}

/// <summary>Расписание фоновой синхронизации — чистая функция над последними запусками (новые первыми).</summary>
public static class HrSyncSchedule
{
    public static (bool Due, Guid? RetryOf) Due(IReadOnlyList<HrSyncRun> recent, int intervalMinutes, DateTimeOffset now)
    {
        var last = recent.FirstOrDefault();
        if (last is null)
        {
            return (true, null);
        }

        if (last.Status == HrSyncStatus.Running)
        {
            return (false, null);
        }

        // Подряд неудачные запуски: первые MaxRetries — повтор через RetryDelay, дальше — обычный интервал (Verifix может лежать долго).
        var failures = recent.TakeWhile(r => r.Status == HrSyncStatus.Failed).Count();
        if (failures is > 0 and <= HrSyncWorker.MaxRetries)
        {
            return (now >= (last.FinishedAt ?? last.StartedAt) + HrSyncWorker.RetryDelay, last.Id);
        }

        var interval = TimeSpan.FromMinutes(Math.Clamp(intervalMinutes, VerifixSettingsStore.MinInterval, VerifixSettingsStore.MaxInterval));
        return (now >= last.StartedAt + interval, null);
    }
}
