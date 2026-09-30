using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneBase.AI.Gateway;

namespace OneBase.AI.Proactive;

public sealed class ProactiveOptions
{
    /// <summary>Как часто проверять данные, минут; 0 — не проверять по расписанию (только кнопкой).</summary>
    public int IntervalMinutes { get; init; } = 60;

    /// <summary>Первая проверка после старта — когда данные уже загружены из кэша/синхронизации.</summary>
    public int StartDelayMinutes { get; init; } = 2;
}

/// <summary>Проактивный анализ по расписанию. Выключенный в «Настройки → AI» AI не проверяет.</summary>
internal sealed class ProactiveAnalysisWorker(
    IServiceScopeFactory scopes,
    ProactiveOptions options,
    IAiSettingsSource settings,
    ILogger<ProactiveAnalysisWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.IntervalMinutes <= 0)
        {
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromMinutes(options.StartDelayMinutes), stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(options.IntervalMinutes));
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // остановка приложения
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            if (!(await settings.GetSettingsAsync(ct)).Enabled)
            {
                return;
            }

            using var scope = scopes.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<ProactiveAnalyzer>().RunAsync(ct);
            logger.LogInformation("Проактивный анализ: активных находок {Active}, новых {Created}, закрыто {Resolved}", result.Active, result.Created, result.Resolved);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Проактивный анализ не выполнен");
        }
    }
}
