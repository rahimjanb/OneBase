using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OneBase.AI.Telegram;

/// <summary>
/// Фоновая отправка рассылок: при запуске — разбор прерванных отправок, затем каждые 5 секунд — запланированные в очередь и по задаче на
/// каждый аккаунт, которому есть что отправлять (одна задача на аккаунт: он шлёт строго по очереди).
/// </summary>
internal sealed class TelegramCampaignWorker(TelegramCampaignRunner runner, ILogger<TelegramCampaignWorker> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<Guid, Task> _running = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            var failed = await runner.RecoverAsync(stoppingToken);
            if (failed > 0)
            {
                logger.LogWarning("Рассылки: {Count} отправок прервано перезапуском — помечены ошибкой без автоповтора", failed);
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
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
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
