using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneBase.Application.Sales;

namespace OneBase.Infrastructure.Linko;

/// <summary>Не даёт запускать синхронизацию параллельно (фон + кнопка «Обновить»).</summary>
public sealed class LinkoSyncCoordinator(IServiceScopeFactory scopes, SalesCacheSignal cacheSignal, ILogger<LinkoSyncCoordinator> logger)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public bool IsRunning => _gate.CurrentCount == 0;

    public LinkoSyncReport? LastReport { get; private set; }

    /// <summary>Запускает синхронизацию и ждёт её. null — если уже идёт другая.</summary>
    public async Task<LinkoSyncReport?> RunAsync(bool full, CancellationToken ct = default)
    {
        if (!await _gate.WaitAsync(0, ct))
        {
            return null;
        }

        try
        {
            using var scope = scopes.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<LinkoSyncService>();
            LastReport = await service.SyncAsync(full, ct);
            cacheSignal.Invalidate();
            return LastReport;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Запускает синхронизацию в фоне. false — если она уже идёт.</summary>
    public bool TryStartInBackground(bool full)
    {
        if (IsRunning)
        {
            return false;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await RunAsync(full);
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
                    await coordinator.RunAsync(full: false, stoppingToken);
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
