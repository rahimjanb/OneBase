using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OneBase.AI.Telegram;

/// <summary>
/// Фоновая отправка рассылок: при запуске — разбор прерванных отправок, затем каждые 5 секунд — запланированные в очередь и по задаче на
/// каждый аккаунт, которому есть что отправлять (одна задача на аккаунт: он шлёт строго по очереди).
/// </summary>
internal sealed class TelegramCampaignWorker(TelegramCampaignRunner runner, Microsoft.Extensions.Configuration.IConfiguration config, ILogger<TelegramCampaignWorker> logger)
    : BackgroundService
{
    private readonly ConcurrentDictionary<Guid, Task> _running = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Microsoft.Extensions.Configuration.ConfigurationBinder.GetValue(config, "Telegram:Enabled", true))
        {
            // Telegram выключен в этой среде (локально): рассылки не отправляются и их статусы в базе не меняются.
            logger.LogInformation("Рассылки: Telegram выключен в этой среде (Telegram:Enabled=false)");
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            try
            {
                var failed = await runner.RecoverAsync(stoppingToken);
                if (failed > 0)
                {
                    logger.LogWarning("Рассылки: {Count} отправок прервано перезапуском — помечены ошибкой без автоповтора", failed);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Рассылки: прерванные отправки не проверены");
            }

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
            do
            {
                try
                {
                    foreach (var account in await runner.DueAccountsAsync(stoppingToken))
                    {
                        if (_running.TryGetValue(account, out var task) && !task.IsCompleted)
                        {
                            continue;
                        }

                        _running[account] = Task.Run(() => RunAsync(account, stoppingToken), CancellationToken.None);
                    }
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    // Обрыв сети или базы — не остановка сервера: в журнал, следующий круг через 5 с.
                    logger.LogWarning(ex, "Рассылки: круг очереди не выполнен");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // остановка сервера
        }
    }

    private async Task RunAsync(Guid account, CancellationToken ct)
    {
        try
        {
            await runner.RunAccountAsync(account, ct);
        }
        catch (OperationCanceledException)
        {
            // остановка сервера: строка «Отправляется» (если была) после запуска уйдёт в ошибку без автоповтора
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Рассылки: очередь аккаунта {Account} остановилась с ошибкой", account);
        }
    }
}
