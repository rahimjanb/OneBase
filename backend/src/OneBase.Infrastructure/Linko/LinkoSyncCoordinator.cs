using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneBase.Application.Sales;
using OneBase.Domain.Sales;

namespace OneBase.Infrastructure.Linko;

/// <summary>Не даёт запускать синхронизацию параллельно (фон + кнопка «Обновить»).</summary>
public sealed class LinkoSyncCoordinator(IServiceScopeFactory scopes, SalesCacheSignal cacheSignal, LinkoSyncProgress progress, ILogger<LinkoSyncCoordinator> logger)
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
            if (mode == LinkoSyncMode.Incremental)
            {
                cacheSignal.Invalidate();
            }
            else
            {
                cacheSignal.InvalidateHistory(); // полная загрузка могла поменять и давние месяцы
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

/// <summary>Периодическая синхронизация Linko (Sales:Sync:IntervalMinutes).</summary>
internal sealed class LinkoSyncWorker(
    LinkoSyncCoordinator coordinator,
    LinkoSettingsStore settings,
    SalesOptions sales,
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
                    await coordinator.RunAsync(LinkoSyncMode.Incremental, stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Синхронизация Linko упала");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
