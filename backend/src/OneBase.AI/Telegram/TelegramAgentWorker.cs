using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneBase.Application.Telegram;

namespace OneBase.AI.Telegram;

/// <summary>
/// Входящие Telegram → AI-агент. Одновременно — до 4 сообщений (ответ консультанта идёт десятки секунд), но в одном чате — строго по
/// очереди: второй вопрос ждёт ответа на первый. Каждое сообщение — в своей области DI (свой DbContext).
/// </summary>
internal sealed class TelegramAgentWorker(TelegramInbox inbox, IServiceScopeFactory scopes, ILogger<TelegramAgentWorker> logger) : BackgroundService
{
    private const int Parallelism = 4;

    private readonly SemaphoreSlim _slots = new(Parallelism, Parallelism);
    private readonly ConcurrentDictionary<(Guid, long), SemaphoreSlim> _chats = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in inbox.Reader.ReadAllAsync(stoppingToken))
            {
                await _slots.WaitAsync(stoppingToken);
                _ = Task.Run(() => HandleAsync(message, stoppingToken), CancellationToken.None);
            }
        }
        catch (OperationCanceledException)
        {
            // остановка сервера
        }
    }

    private async Task HandleAsync(TelegramIncoming message, CancellationToken ct)
    {
        var chat = _chats.GetOrAdd((message.AccountId, message.Chat.PeerId), _ => new SemaphoreSlim(1, 1));
        try
        {
            await chat.WaitAsync(ct);
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<TelegramAgentService>().ProcessAsync(message, ct);
            }
            finally
            {
                chat.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // остановка сервера: ответ не отправлен, входящее остаётся в базе со статусом received
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Telegram-агент: сообщение {Message} в чате {Chat} не обработано", message.MessageId, message.Chat.PeerId);
        }
        finally
        {
            _slots.Release();
        }
    }
}
