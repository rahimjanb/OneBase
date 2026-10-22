using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneBase.Application.Sales;
using OneBase.Domain.Sales;

namespace OneBase.Infrastructure.Linko;

/// <summary>Не даёт запускать синхронизацию параллельно (фон + кнопка «Обновить»).</summary>
public sealed class LinkoSyncCoordinator(
    IServiceScopeFactory scopes,
    SalesCacheSignal cacheSignal,
    LinkoSyncProgress progress,
    TimeProvider time,
    ILogger<LinkoSyncCoordinator> logger)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Отмена текущего запуска (кнопка «Отменить»); null — синхронизация не идёт.</summary>
    private CancellationTokenSource? _run;

    public bool IsRunning => _gate.CurrentCount == 0;

    /// <summary>Отмена запрошена и синхронизация ещё сворачивается.</summary>
    public bool IsCancelling => _run?.IsCancellationRequested == true;

    public LinkoSyncReport? LastReport { get; private set; }

    /// <summary>Запускает синхронизацию и ждёт её. null — если уже идёт другая.</summary>
    public async Task<LinkoSyncReport?> RunAsync(LinkoSyncMode mode, CancellationToken ct = default)
    {
        if (!await _gate.WaitAsync(0, ct))
        {
            return null;
        }

        using var run = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _run = run;
        try
        {
            progress.Start(mode);
            using var scope = scopes.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<LinkoSyncService>();
            LastReport = await service.SyncAsync(mode, run.Token);
            // Обычное обновление сбрасывает кэш свежих расчётов; если пришло изменение давнего месяца (заказ или возврат старше прошлого
            // месяца — поздняя приёмка, отмена) — и кэш давних месяцев: иначе АКБ с начала года жила бы со старыми цифрами часами.
            if (mode == LinkoSyncMode.Incremental && !LinkoSyncSchedule.TouchesHistory(service.EarliestChanged, time.GetUtcNow()))
            {
                cacheSignal.Invalidate();
            }
            else
            {
                cacheSignal.InvalidateHistory(); // полная загрузка, ежедневная сверка или изменение давних месяцев
            }

            progress.Step("Пересчёт отчётов", null);
            await WarmUpAsync(run.Token);
            return LastReport;
        }
        catch (OperationCanceledException) when (run.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            // Отменили кнопкой: шаги, которые успели, сохранены; прерванный шаг повторится со следующей синхронизацией.
            logger.LogWarning("Синхронизация Linko отменена (режим {Mode}): {Effect}", mode, mode == LinkoSyncMode.Reset
                ? "данные загружены не полностью — догрузятся следующей синхронизацией"
                : "незавершённый шаг повторится при следующей синхронизации");
            cacheSignal.Invalidate();
            return null;
        }
        finally
        {
            _run = null;
            progress.Finish();
            _gate.Release();
        }
    }

    /// <summary>Кнопка «Отменить»: прерывает текущую синхронизацию. false — синхронизация не идёт.</summary>
    public bool Cancel()
    {
        var run = _run;
        if (run is null || run.IsCancellationRequested)
        {
            return false;
        }

        progress.Step("Отмена…", null);
        run.Cancel();
        return true;
    }

    /// <summary>
    /// После обновления данных заранее считает текущий месяц и частые страницы, чтобы первый переход пользователя
    /// не ждал пересчёта. Ошибка прогрева не ломает синхронизацию — страница просто посчитается по запросу.
    /// </summary>
    private async Task WarmUpAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var analytics = await scope.ServiceProvider.GetRequiredService<SalesDataLoader>().LoadAsync(null, null, ct);
            analytics.CachedOverview();
            analytics.CachedRepublic(null, null);
            analytics.CachedProblems(null, null, false);
            analytics.CachedPlans();
            analytics.CachedAssortment(null, null); // «Товары и категории» — частый инструмент AI-консультанта
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Не удалось заранее пересчитать отчёты продаж");
        }
    }

    /// <summary>Запускает синхронизацию в фоне. false — если она уже идёт.</summary>
    public bool TryStartInBackground(LinkoSyncMode mode)
    {
        if (IsRunning)
        {
            return false;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await RunAsync(mode);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Фоновая синхронизация Linko упала");
            }
        });
        return true;
    }
}

/// <summary>
/// Обмен с Linko без людей — только по расписанию Sales:Sync:Times (по умолчанию 11:00 и 17:00 по Ташкенту): обновление изменений;
/// первый запуск дня после Sales:Sync:ReconcileHour — сверка (полная загрузка свежего окна и остатков). Между запусками данные свежее
/// делает только кнопка «Обновить». Сервер был выключен во время запуска (или запуск не прошёл) — после старта синхронизация догоняется
/// сразу. После каждого запуска — контроль обмена (LinkoSyncHealth): запуск не обновил данные — уведомление администраторам интеграций,
/// восстановилось — тоже. После синхронизации координатор сбрасывает кэши всех отчётов на данных Linko и пересчитывает частые страницы;
/// витрина Sales Base пересчитывается своей службой, увидев новую синхронизацию.
/// </summary>
internal sealed class LinkoSyncWorker(
    LinkoSyncCoordinator coordinator,
    LinkoSettingsStore settings,
    SalesOptions sales,
    IServiceScopeFactory scopes,
    TimeProvider time,
    ILogger<LinkoSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var times = LinkoSyncSchedule.ParseTimes(sales.Sync.Times);
        if (!sales.Sync.Enabled || times.Count == 0)
        {
            logger.LogInformation("Фоновая синхронизация Linko выключена (Sales:Sync:Enabled или пустое Sales:Sync:Times) — данные обновляет только кнопка «Обновить»");
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(15), time, stoppingToken);
        if (await MissedAsync(times, stoppingToken))
        {
            logger.LogInformation("Linko: запуск по расписанию пропущен (сервер был выключен или запуск не прошёл) — синхронизация сейчас");
            await RunOnceAsync(stoppingToken);
        }

        // Отсчёт от прошедшего запуска, а не только от часов: таймер может проснуться на миг раньше — без этого тот же запуск прошёл бы дважды.
        var previous = time.GetUtcNow();
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = time.GetUtcNow();
            var next = LinkoSyncSchedule.NextRun(now > previous ? now : previous, times);
            logger.LogInformation("Linko: следующая синхронизация — {Next:dd.MM.yyyy HH:mm} (Ташкент)", next.ToOffset(LinkoSyncSchedule.CompanyOffset));
            var wait = next - now;
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, time, stoppingToken);
            }

            previous = next;
            await RunOnceAsync(stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            // Настройки читаются перед каждым запуском: подключение можно включить или поменять без перезапуска.
            if (!(await settings.GetAsync(ct)).IsReady)
            {
                return;
            }

            var mode = await ReconcileDueAsync(ct) ? LinkoSyncMode.Reconcile : LinkoSyncMode.Incremental;
            if (mode == LinkoSyncMode.Reconcile)
            {
                logger.LogInformation("Linko: сверка (первый запуск дня)");
            }

            if (await coordinator.RunAsync(mode, ct) is not { } report)
            {
                logger.LogInformation("Linko: запуск по расписанию пропущен — уже идёт синхронизация кнопкой «Обновить»");
                return;
            }

            await CheckHealthAsync(report.StartedAt, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Синхронизация Linko упала");
        }
    }

    /// <summary>После последнего наступившего времени расписания заказы не обновлялись — ни по расписанию, ни кнопкой.</summary>
    private async Task<bool> MissedAsync(IReadOnlyList<TimeOnly> times, CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OneBase.Infrastructure.Persistence.OneBaseDbContext>();
            var last = await db.LinkoSyncStates.AsNoTracking().Where(s => s.Entity == "orders").Select(s => s.LastSuccessAt).FirstOrDefaultAsync(ct);
            return LinkoSyncSchedule.Missed(time.GetUtcNow(), last, times);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Linko: не удалось проверить пропущенный запуск");
            return false;
        }
    }

    private async Task<bool> ReconcileDueAsync(CancellationToken ct)
    {
        if (sales.Sync.ReconcileHour < 0)
        {
            return false;
        }

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBase.Infrastructure.Persistence.OneBaseDbContext>();
        var last = await db.LinkoSyncStates.AsNoTracking().Where(s => s.Entity == LinkoSyncSchedule.ReconcileState).Select(s => s.LastSuccessAt).FirstOrDefaultAsync(ct);
        return LinkoSyncSchedule.ReconcileDue(time.GetUtcNow(), last, sales.Sync.ReconcileHour);
    }

    /// <summary>Контроль обмена не должен ломать синхронизацию: ошибка — в журнал.</summary>
    private async Task CheckHealthAsync(DateTimeOffset since, CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<LinkoSyncHealth>().CheckAsync(since, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Linko: контроль обмена не выполнен");
        }
    }
}
