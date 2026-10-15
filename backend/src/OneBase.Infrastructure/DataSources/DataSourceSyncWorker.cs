using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OneBase.Infrastructure.DataSources;

/// <summary>
/// Синхронизация Google Sheets по расписанию: раз в минуту запускает привязки, у которых подошло время (NextRunAt). Раз в час — проверка
/// начала месяца: план был в прошлом месяце, а на текущий его нет — уведомление (CheckMonthPlansAsync, одно на вид плана и месяц).
/// </summary>
internal sealed class DataSourceSyncWorker(IServiceScopeFactory scopes, ILogger<DataSourceSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        var lastPlanCheck = DateTimeOffset.MinValue;
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<DataSourceService>();
                await service.RunDueAsync(stoppingToken);
                if (DateTimeOffset.UtcNow - lastPlanCheck >= TimeSpan.FromHours(1))
                {
                    lastPlanCheck = DateTimeOffset.UtcNow;
                    await service.CheckMonthPlansAsync(stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Источники данных: синхронизация по расписанию упала");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
