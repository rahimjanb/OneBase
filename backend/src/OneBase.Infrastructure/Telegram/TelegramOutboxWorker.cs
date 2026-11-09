using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneBase.Application.Telegram.Outbox;

namespace OneBase.Infrastructure.Telegram;

/// <summary>
/// Очередь исходящих подразделений (HR): после запуска «отправляется» — в «неизвестно» (без автоповтора), затем раз в 3 с — круг отправки.
/// При <c>Telegram:Enabled=false</c> не отправляет ничего (строки ждут). В копии для проверки не регистрируется вовсе.
/// </summary>
internal sealed class TelegramOutboxWorker(IServiceScopeFactory scopes, IConfiguration config, ILogger<TelegramOutboxWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!WTelegramGateway.Enabled(config))
        {
            logger.LogInformation("Очередь исходящих Telegram не запущена: Telegram в этой среде выключен");
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            using (var scope = scopes.CreateScope())
            {
                var recovered = await scope.ServiceProvider.GetRequiredService<TelegramOutboxRunner>().RecoverAsync(stoppingToken);
                if (recovered > 0)
                {
                    logger.LogWarning("Очередь исходящих Telegram: {Count} сообщений «отправлялись» при остановке — помечены «неизвестно, дошло ли»", recovered);
                }
            }

            using var timer = new PeriodicTimer(Interval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<TelegramOutboxRunner>().RunOnceAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Очередь исходящих Telegram: круг отправки не выполнен");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // остановка сервера
        }
    }
}
