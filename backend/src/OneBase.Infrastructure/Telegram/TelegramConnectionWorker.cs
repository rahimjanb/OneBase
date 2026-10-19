using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneBase.Application.Telegram;
using OneBase.Domain.Telegram;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.Telegram;

/// <summary>
/// Подключённые аккаунты Telegram после запуска сервера: клиент поднимается по сохранённой сессии (с ним — приём входящих), затем раз в
/// 5 минут проверяется. Отозванная в Telegram сессия → «Отключён» с причиной; временные ошибки — повтор на следующем круге.
/// </summary>
internal sealed class TelegramConnectionWorker(IServiceScopeFactory scopes, ILogger<TelegramConnectionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // Фоновая служба не должна останавливать весь сервер: ошибка — в журнал, следующий круг через Interval.
                logger.LogWarning(ex, "Telegram: проверка подключений не завершилась");
            }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    internal async Task RunOnceAsync(CancellationToken ct)
    {
        List<Guid> ids;
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            ids = await db.TelegramAccounts.AsNoTracking()
                .Where(a => a.Status == TelegramAccountStatus.Connected)
                .Select(a => a.Id).ToListAsync(ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Telegram: список аккаунтов не прочитан");
            return;
        }

        foreach (var id in ids)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var accounts = scope.ServiceProvider.GetRequiredService<TelegramAccountService>();
                if (!await accounts.ReconnectAsync(id, ct))
                {
                    logger.LogWarning("Telegram {Account}: сессия недействительна — аккаунт отключён", id);
                    continue;
                }

                // Добавленные по username, пока аккаунт был не подключён, — найти их Telegram ID; чаты — раз в час свежие.
                await accounts.ResolvePendingAsync(id, ct);
                await accounts.SyncChatsIfStaleAsync(id, TimeSpan.FromHours(1), ct);
            }
            catch (TelegramException ex)
            {
                logger.LogWarning("Telegram {Account}: не подключён — {Error}", id, ex.Message);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                // В т.ч. отмена изнутри библиотеки при обрыве сети — это не остановка сервера.
                logger.LogWarning(ex, "Telegram {Account}: ошибка подключения", id);
            }
        }
    }
}
