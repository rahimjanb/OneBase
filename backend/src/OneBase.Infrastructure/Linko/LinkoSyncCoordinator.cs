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
                cacheSignal.InvalidateHistory(); // полная загрузка, ночная сверка или изменение давних месяцев
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
/// Обмен с Linko без людей: обновление изменений каждые Sales:Sync:IntervalMinutes, раз в сутки после Sales:Sync:ReconcileHour (Ташкент) —
/// ночная сверка (полная загрузка свежего окна и остатков); после каждого запуска — контроль обмена (LinkoSyncHealth): данные устарели —
/// уведомление администраторам интеграций, восстановилось — тоже. После синхронизации координатор сбрасывает кэши всех отчётов на данных
/// Linko и пересчитывает частые страницы; витрина Sales Base пересчитывается своей службой, увидев новую синхронизацию.
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
        if (!sales.Sync.Enabled)
        {
            logger.LogInformation("Фоновая синхронизация Linko выключена (Sales:Sync:Enabled=false)");
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, sales.Sync.IntervalMinutes)));
        do
        {
            try
            {
                // Настройки читаются на каждом цикле: подключение можно включить или поменять без перезапуска.
                if ((await settings.GetAsync(stoppingToken)).IsReady)
                {
                    var mode = await ReconcileDueAsync(stoppingToken) ? LinkoSyncMode.Reconcile : LinkoSyncMode.Incremental;
                    if (mode == LinkoSyncMode.Reconcile)
                    {
                        logger.LogInformation("Linko: ночная сверка");
                    }

                    await coordinator.RunAsync(mode, stoppingToken);
                    await CheckHealthAsync(stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Синхронизация Linko упала");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
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
    private async Task CheckHealthAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<LinkoSyncHealth>().CheckAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Linko: контроль обмена не выполнен");
        }
    }
}
