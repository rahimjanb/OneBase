using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OneBase.Infrastructure.Recruitment;

/// <summary>
/// Раз в 15 минут (<c>Recruitment:Hh:IntervalMinutes</c>) — отклики HH.uz по связанным вакансиям в единую базу кандидатов. Не подключён —
/// круг пустой. В копии для проверки не регистрируется (ходит во внешний API).
/// </summary>
internal sealed class HhSyncWorker(IServiceScopeFactory scopes, IConfiguration config, ILogger<HhSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(5, config.GetValue("Recruitment:Hh:IntervalMinutes", 15)));
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            using var timer = new PeriodicTimer(interval);
            do
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var result = await scope.ServiceProvider.GetRequiredService<HhService>().SyncAsync(null, stoppingToken);
                    if (result.Responses > 0 || result.Errors > 0)
                    {
                        logger.LogInformation("HH.uz: {Message}", result.Message);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "HH.uz: синхронизация откликов не выполнена");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // остановка сервера
        }
    }
}
