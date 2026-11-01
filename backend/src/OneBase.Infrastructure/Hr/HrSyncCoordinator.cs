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
/// Running (уникальный индекс: вторая такая строка не вставится). Два вида запуска: полный (справочники, сотрудники, отметки, табель) и
/// быстрый — только отметки (проходы, триггер HrSyncTrigger.Tracks). Запуск, который перестал отмечаться дольше StaleAfter (сервер
/// перезапустили посреди загрузки), закрывается как прерванный — не только при новом старте, но и проверкой расписания (CloseStaleAsync):
/// иначе «идёт» висело часами — кнопка заблокирована, расписание ждёт окончания, а закрыть мог только новый старт. Свой запуск, который
/// завис (не отмечается дольше StaleAfter) или идёт дольше MaxRunMinutes, останавливается; кнопка «Отменить» (CancelAsync) — тоже.
/// </summary>
public sealed class HrSyncCoordinator(IServiceScopeFactory scopes, VerifixSettingsStore settings, VerifixOptions options, TimeProvider time,
    ILogger<HrSyncCoordinator> logger)
{
    public static readonly TimeSpan StaleAfter = HrSyncRun.StaleAfter;

    /// <summary>Кнопка ждёт окончания быстрого запуска проходов (секунды), а не отвечает «уже идёт».</summary>
    private static readonly TimeSpan WaitForQuick = TimeSpan.FromSeconds(30);

    /// <summary>Сколько хранить успешные быстрые запуски в журнале: их 720 в сутки, нужны только последние.</summary>
    private static readonly TimeSpan KeepQuickRuns = TimeSpan.FromDays(2);

    private static readonly JsonSerializerOptions StepsJson = new(JsonSerializerDefaults.Web);

    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Вид запуска, который держит семафор (пока идёт).</summary>
    private volatile string? _runningTrigger;

    /// <summary>Запуск этого процесса (пока идёт): его отменяют CancelAsync и CloseStaleAsync; причина — в _stopReason.</summary>
    private CancellationTokenSource? _current;
    private Guid _currentId;
    private string? _stopReason;
    private bool _cancelledByUser;

    public bool IsRunning => _gate.CurrentCount == 0;

    /// <summary>Сейчас идёт быстрый запуск (только проходы, секунды).</summary>
    public bool IsQuickRunning => IsRunning && _runningTrigger == HrSyncTrigger.Tracks;

    private TimeSpan MaxRun => TimeSpan.FromMinutes(Math.Clamp(options.MaxRunMinutes, 15, 24 * 60));

    /// <summary>Строка Running, которая отмечалась недавно: синхронизация действительно идёт (здесь или в другом экземпляре API).</summary>
    public static bool IsAlive(HrSyncRun run, DateTimeOffset now) => run.IsAlive(now);

    /// <summary>
    /// Зависшие запуски: строки Running, которые не отмечались дольше StaleAfter, закрываются как прерванные; свой запуск, который перестал
    /// отмечаться или идёт дольше MaxRunMinutes, отменяется (строку он закроет сам). Вызывают расписание (каждые полминуты) и каждый старт.
    /// </summary>
    public async Task CloseStaleAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        await CloseStaleAsync(scope.ServiceProvider.GetRequiredService<OneBaseDbContext>(), ct);
    }

    private async Task CloseStaleAsync(OneBaseDbContext db, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var stale = now - StaleAfter;
        var current = _current;
        var currentId = current is null ? Guid.Empty : _currentId;
        if (current is not null)
        {
            var row = await db.HrSyncRuns.AsNoTracking().Where(r => r.Id == currentId).Select(r => new { r.StartedAt, r.HeartbeatAt }).FirstOrDefaultAsync(ct);
            var reason = row is null ? null
                : row.HeartbeatAt < stale ? $"Остановлена: запуск не отвечал больше {StaleAfter.TotalMinutes:0} минут. Загруженное сохранено."
                : now - row.StartedAt > MaxRun ? $"Остановлена: шла дольше {MaxRun.TotalMinutes:0} минут. Загруженное сохранено — следующий запуск продолжит."
                : null;
            if (reason is not null && !current.IsCancellationRequested)
            {
                _stopReason = reason;
                logger.LogWarning("Verifix: {Reason}", reason);
                await current.CancelAsync();
            }
        }

        var closed = await db.HrSyncRuns.Where(r => r.Status == HrSyncStatus.Running && r.HeartbeatAt < stale && r.Id != currentId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, HrSyncStatus.Failed)
                .SetProperty(r => r.FinishedAt, now)
                .SetProperty(r => r.Message, "Прервана: сервер перезапустился или запуск перестал отвечать. Загруженное сохранено."), ct);
        if (closed > 0)
        {
            logger.LogWarning("Verifix: закрыт зависший запуск синхронизации ({Count})", closed);
        }
    }

    /// <summary>
    /// Кнопка «Отменить»: останавливает идущий запуск этого сервера (загруженное сохраняется — отметки по дням, табель по неделям; итог —
    /// «Отменена»). Строку «идёт» без живого запуска здесь (зависла, другой экземпляр API) закрывает сразу. false — отменять нечего.
    /// </summary>
    public async Task<bool> CancelAsync(CancellationToken ct)
    {
        var current = _current;
        if (current is not null && !current.IsCancellationRequested)
        {
            _stopReason = "Отменена пользователем. Загруженное сохранено.";
            _cancelledByUser = true;
            logger.LogInformation("Verifix: синхронизация отменена пользователем");
            await current.CancelAsync();
            return true;
        }

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var now = time.GetUtcNow();
        var currentId = current is null ? Guid.Empty : _currentId;
        var closed = await db.HrSyncRuns.Where(r => r.Status == HrSyncStatus.Running && r.Id != currentId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, HrSyncStatus.Cancelled)
                .SetProperty(r => r.FinishedAt, now)
                .SetProperty(r => r.Message, "Отменена пользователем: запуск на этом сервере не шёл — строка закрыта. Загруженное сохранено."), ct);
        return closed > 0 || current is not null;
    }

    /// <summary>Запускает и ждёт окончания (фон по расписанию, тесты). Возвращает запуск или причину, по которой он не начат.</summary>
    public async Task<(HrSyncStartResult Result, Guid? RunId)> RunAsync(string trigger, Guid? userId, Guid? retryOf, CancellationToken ct)
    {
        var (result, runId) = await BeginAsync(trigger, userId, retryOf, ct);
        if (result != HrSyncStartResult.Started)
        {
            return (result, null);
        }

        await ExecuteAsync(runId!.Value, trigger, ct);
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
                    await ExecuteAsync(runId!.Value, trigger, CancellationToken.None);
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

        // Полный запуск (кнопка, расписание) ждёт окончания быстрого — он идёт секунды; быстрый никого не ждёт.
        var wait = trigger != HrSyncTrigger.Tracks && _runningTrigger == HrSyncTrigger.Tracks ? WaitForQuick : TimeSpan.Zero;
        if (!await _gate.WaitAsync(wait, ct))
        {
            return (HrSyncStartResult.AlreadyRunning, null);
        }

        _runningTrigger = trigger;
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            await CloseStaleAsync(db, ct);

            var now = time.GetUtcNow();
            var run = new HrSyncRun { Trigger = trigger, StartedById = userId, RetryOfId = retryOf, StartedAt = now, HeartbeatAt = now };
            db.HrSyncRuns.Add(run);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Другой экземпляр API уже синхронизирует.
                Release();
                return (HrSyncStartResult.AlreadyRunning, null);
            }

            return (HrSyncStartResult.Started, run.Id);
        }
        catch
        {
            Release();
            throw;
        }
    }

    private void Release()
    {
        _runningTrigger = null;
        _gate.Release();
    }

    private async Task ExecuteAsync(Guid runId, string trigger, CancellationToken ct)
    {
        string status;
        string? message;
        HrSyncOutcome? outcome = null;
        // Свой токен запуска: его отменяют завершение сервера (ct), предел MaxRunMinutes, «Отменить» и проверка «завис» (CloseStaleAsync).
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        stop.CancelAfter(MaxRun);
        _stopReason = null;
        _cancelledByUser = false;
        _currentId = runId;
        _current = stop;
        try
        {
            using var scope = scopes.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<HrSyncService>();
            outcome = trigger == HrSyncTrigger.Tracks ? await service.RunTracksAsync(runId, stop.Token) : await service.RunAsync(runId, stop.Token);
            var failed = outcome.Steps.Count(s => s.Status == "failed");
            var ok = outcome.Steps.Count(s => s.Status == "ok");
            status = failed == 0 ? HrSyncStatus.Succeeded : ok > 0 && !outcome.AuthFailed ? HrSyncStatus.Partial : HrSyncStatus.Failed;
            message = failed == 0
                ? outcome.Steps.FirstOrDefault(s => s.Status == "skipped")?.Message ?? "Синхронизация завершена."
                : string.Join("; ", outcome.Steps.Where(s => s.Status == "failed").Select(s => $"{StepTitle(s.Step)}: {s.Message}"));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            status = HrSyncStatus.Failed;
            message = "Остановлена: сервер OneBase завершает работу. Загруженное сохранено.";
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            status = _cancelledByUser ? HrSyncStatus.Cancelled : HrSyncStatus.Failed;
            message = _stopReason ?? $"Остановлена: шла дольше {MaxRun.TotalMinutes:0} минут. Загруженное сохранено — следующий запуск продолжит.";
            logger.LogWarning("Verifix: {Message}", message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Синхронизация Verifix упала");
            status = HrSyncStatus.Failed;
            message = "Ошибка OneBase при синхронизации: " + VerifixLog.Safe(ex.Message);
        }
        finally
        {
            _current = null;
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
            else if (status != HrSyncStatus.Cancelled)
            {
                run.Errors = Math.Max(1, run.Errors);
            }

            await db.SaveChangesAsync(CancellationToken.None);

            // Успешные быстрые запуски нужны журналу только последние: старше двух суток — удаляются.
            if (trigger == HrSyncTrigger.Tracks && status == HrSyncStatus.Succeeded)
            {
                var before = run.StartedAt - KeepQuickRuns;
                await db.HrSyncRuns.Where(r => r.Trigger == HrSyncTrigger.Tracks && r.Status == HrSyncStatus.Succeeded && r.StartedAt < before)
                    .ExecuteDeleteAsync(CancellationToken.None);
            }
        }
        finally
        {
            Release();
        }

        if (status is not (HrSyncStatus.Succeeded or HrSyncStatus.Cancelled))
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
/// Синхронизация Verifix по расписанию (включается флажком «Автосинхронизация»): проходы (отметки) — каждые TracksIntervalMinutes
/// (по умолчанию 2), полная синхронизация — каждые IntervalMinutes (по умолчанию 10). Полный запуск не прошёл — повтор через RetryDelay,
/// не больше MaxRetries раз подряд, затем обычный интервал. Настройки читаются перед каждой проверкой — включить, выключить и поменять
/// интервалы можно без перезапуска. Каждые полминуты — проверка зависших запусков (и без автосинхронизации). Verifix:WorkerEnabled =
/// false — только кнопка.
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

    /// <summary>Шаг проверки расписания: при проходах раз в 2 минуты — не реже раза в полминуты.</summary>
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(30);

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
                if (connection.IsReady)
                {
                    await coordinator.CloseStaleAsync(stoppingToken);
                }

                if (connection.IsReady && connection.Data.AutoSync && !coordinator.IsRunning)
                {
                    var (plan, retryOf) = await NextAsync(connection.Data, stoppingToken);
                    if (plan != HrSyncPlan.None)
                    {
                        var trigger = plan == HrSyncPlan.Tracks ? HrSyncTrigger.Tracks : retryOf is null ? HrSyncTrigger.Auto : HrSyncTrigger.Retry;
                        await coordinator.RunAsync(trigger, null, retryOf, stoppingToken);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Verifix: проверка расписания синхронизации не выполнена");
            }

            await Task.Delay(Tick, time, stoppingToken);
        }
    }

    /// <summary>Что запускать: полную синхронизацию (интервал прошёл или повтор после неудачи), проходы или ничего.</summary>
    private async Task<(HrSyncPlan Plan, Guid? RetryOf)> NextAsync(VerifixSettingsData data, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var full = await db.HrSyncRuns.AsNoTracking().Where(r => r.Trigger != HrSyncTrigger.Tracks)
            .OrderByDescending(r => r.StartedAt).Take(MaxRetries + 1).ToListAsync(ct);
        // Отметки загружает и полный запуск: проходы считаются от последнего запуска любого вида.
        var lastAny = await db.HrSyncRuns.AsNoTracking().OrderByDescending(r => r.StartedAt).Select(r => (DateTimeOffset?)r.StartedAt).FirstOrDefaultAsync(ct);
        var tracksReady = await db.HrSyncStates.AsNoTracking().AnyAsync(s => s.Entity == HrSyncService.TracksState && s.CoveredThrough != null, ct);
        return HrSyncSchedule.Next(full, lastAny, data.IntervalMinutes, data.TracksIntervalMinutes ?? VerifixSettingsStore.DefaultTracksInterval, tracksReady,
            time.GetUtcNow());
    }
}

/// <summary>Что запустить по расписанию.</summary>
public enum HrSyncPlan
{
    None,
    Full,
    Tracks,
}

/// <summary>Расписание фоновой синхронизации — чистые функции над последними запусками (новые первыми).</summary>
public static class HrSyncSchedule
{
    /// <summary>
    /// Полная синхронизация важнее: её срок прошёл (или пора повторить неудачную) — она. Иначе проходы — если с последнего запуска любого
    /// вида прошло TracksIntervalMinutes и отметки уже загружались (первую загрузку истории делает полная). Полная идёт — ничего.
    /// </summary>
    public static (HrSyncPlan Plan, Guid? RetryOf) Next(IReadOnlyList<HrSyncRun> recentFull, DateTimeOffset? lastAnyStart, int intervalMinutes,
        int tracksIntervalMinutes, bool tracksReady, DateTimeOffset now)
    {
        var (due, retryOf) = Due(recentFull, intervalMinutes, now);
        if (due)
        {
            return (HrSyncPlan.Full, retryOf);
        }

        if (recentFull.FirstOrDefault() is { } last && last.IsAlive(now))
        {
            return (HrSyncPlan.None, null);
        }

        var every = TimeSpan.FromMinutes(Math.Clamp(tracksIntervalMinutes, VerifixSettingsStore.MinTracksInterval, VerifixSettingsStore.MaxTracksInterval));
        return tracksReady && (lastAnyStart is not { } at || now >= at + every) ? (HrSyncPlan.Tracks, null) : (HrSyncPlan.None, null);
    }

    /// <summary>Пора ли полная синхронизация: интервал прошёл; после неудачи — короткая пауза и повтор (не больше MaxRetries подряд).</summary>
    public static (bool Due, Guid? RetryOf) Due(IReadOnlyList<HrSyncRun> recent, int intervalMinutes, DateTimeOffset now)
    {
        var last = recent.FirstOrDefault();
        if (last is null)
        {
            return (true, null);
        }

        // Идёт — ждём; «идёт», но давно не отмечается — зависшая строка: старт её закроет и начнёт заново.
        if (last.Status == HrSyncStatus.Running)
        {
            return (!last.IsAlive(now), null);
        }

        // Подряд неудачные запуски: первые MaxRetries — повтор через RetryDelay, дальше — обычный интервал (Verifix может лежать долго).
        // Отменённый пользователем — не неудача: следующий — по интервалу.
        var failures = recent.TakeWhile(r => r.Status == HrSyncStatus.Failed).Count();
        if (failures is > 0 and <= HrSyncWorker.MaxRetries)
        {
            return (now >= (last.FinishedAt ?? last.StartedAt) + HrSyncWorker.RetryDelay, last.Id);
        }

        var interval = TimeSpan.FromMinutes(Math.Clamp(intervalMinutes, VerifixSettingsStore.MinInterval, VerifixSettingsStore.MaxInterval));
        return (now >= last.StartedAt + interval, null);
    }
}
