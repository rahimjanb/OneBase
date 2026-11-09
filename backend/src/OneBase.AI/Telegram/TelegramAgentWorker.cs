using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneBase.Application.Telegram;
using OneBase.Application.Telegram.Routing;

namespace OneBase.AI.Telegram;

/// <summary>
/// Входящие Telegram → маршрутизатор → обработчик подразделения (продажи — AI-агент продаж, HR — рекрутер). Сообщения одного чата —
/// строго по очереди и по порядку прихода, общей для всех подразделений (один чат никогда не обрабатывают двое сразу). Одновременно
/// работают слоты подразделения: продажи — 4 (ответ консультанта идёт десятки секунд), остальные — по 2, чтобы долгие ответы одного
/// подразделения не задерживали другое. Сообщение занимает слот, только когда дошла его очередь в чате.
/// <para>
/// <c>Telegram:Routing:Enabled=false</c> — маршрутизатор не спрашивается: всё агенту продаж, как до маршрутизации.
/// </para>
/// </summary>
internal sealed class TelegramAgentWorker(TelegramInbox inbox, IServiceScopeFactory scopes, IConfiguration config, ILogger<TelegramAgentWorker> logger)
    : BackgroundService
{
    public const string RoutingKey = "Telegram:Routing:Enabled";
    internal const int SalesSlots = 4;
    internal const int OtherSlots = 2;

    /// <summary>Сообщений в очередях чатов — не больше; дальше чтение очереди ждёт (как раньше ждало свободного слота).</summary>
    internal const int MaxPending = 1000;

    private readonly SemaphoreSlim _pending = new(MaxPending, MaxPending);
    private readonly Lock _gate = new();
    private readonly Dictionary<(Guid Account, long Peer), Queue<TelegramIncoming>> _chats = [];
    private readonly Dictionary<string, SemaphoreSlim> _slots = new(StringComparer.Ordinal);
    private readonly bool _routing = config.GetValue(RoutingKey, true);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in inbox.Reader.ReadAllAsync(stoppingToken))
            {
                await EnqueueAsync(message, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // остановка сервера
        }
    }

    /// <summary>В очередь чата (ждёт, если в очередях уже MaxPending сообщений); у чата без очереди — запустить её разбор.</summary>
    internal async Task EnqueueAsync(TelegramIncoming message, CancellationToken ct)
    {
        await _pending.WaitAsync(ct);
        var key = (message.AccountId, message.Chat.PeerId);
        bool start;
        lock (_gate)
        {
            start = !_chats.TryGetValue(key, out var queue);
            if (start)
            {
                queue = new Queue<TelegramIncoming>();
                _chats[key] = queue;
            }

            queue!.Enqueue(message);
        }

        if (start)
        {
            _ = Task.Run(() => DrainAsync(key, ct), CancellationToken.None);
        }
    }

    /// <summary>Сообщения в обработке и в очередях чатов (для тестов и диагностики).</summary>
    internal int Pending => MaxPending - _pending.CurrentCount;

    private async Task DrainAsync((Guid Account, long Peer) key, CancellationToken ct)
    {
        while (true)
        {
            TelegramIncoming message;
            lock (_gate)
            {
                var queue = _chats[key];
                if (queue.Count == 0 || ct.IsCancellationRequested)
                {
                    _chats.Remove(key);
                    return;
                }

                message = queue.Dequeue();
            }

            try
            {
                await ProcessAsync(message, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // остановка сервера: ответ не отправлен, входящее остаётся в базе со статусом received
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Telegram: сообщение {Message} в чате {Chat} не обработано", message.MessageId, message.Chat.PeerId);
            }
            finally
            {
                _pending.Release();
            }
        }
    }

    private async Task ProcessAsync(TelegramIncoming message, CancellationToken ct)
    {
        var route = TelegramRoute.Legacy;
        if (_routing)
        {
            using var routing = scopes.CreateScope();
            route = await routing.ServiceProvider.GetRequiredService<TelegramMessageRouter>().RouteAsync(message, ct);
        }

        using var scope = scopes.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<ITelegramDepartmentHandler>().ToList();
        // Свой обработчик подразделения; у остальных отделов (кроме продаж) — общий обработчик ИИ-агентов отделов.
        var handler = handlers.FirstOrDefault(h => h.Department == route.Department)
                      ?? (route.Department is { } d && d != TelegramDepartments.Sales ? handlers.FirstOrDefault(h => h.Department == TelegramDepartments.Any) : null);

        if (!route.AutoReply)
        {
            if (!message.Outgoing)
            {
                var why = route.Department is null ? $"не распределён ({route.Reason})"
                    : route.Mode == OneBase.Domain.Telegram.TelegramConversationMode.Manual ? $"{route.Department}: ведёт человек"
                    : $"{route.Department}: диалог закрыт";
                logger.LogInformation("Telegram {Account}: сообщение {Message} в чате {Peer} — {Why}; AI не отвечает",
                    message.AccountId, message.MessageId, message.Chat.PeerId, why);
            }

            // Диалог ведёт человек: у подразделений со своей историей переписки (HR, отделы с ИИ-агентами) сообщение всё равно сохраняется —
            // сотрудник видит его в OneBase; обработчик сам не отвечает (route.AutoReply = false). Продажи — как раньше, без обработчика.
            if (route.Department is null || route.Department == TelegramDepartments.Sales || route.ConversationId is null || handler is null)
            {
                return;
            }
        }

        if (handler is null)
        {
            if (!message.Outgoing)
            {
                logger.LogInformation("Telegram {Account}: сообщение {Message} в чате {Peer} — подразделение {Department} без AI-обработчика, ждёт человека",
                    message.AccountId, message.MessageId, message.Chat.PeerId, route.Department);
            }

            return;
        }

        var slot = Slot(route.Department!);
        await slot.WaitAsync(ct);
        try
        {
            await handler.HandleAsync(message, route, ct);
        }
        finally
        {
            slot.Release();
        }
    }

    private SemaphoreSlim Slot(string department)
    {
        lock (_gate)
        {
            if (!_slots.TryGetValue(department, out var slot))
            {
                var size = department == TelegramDepartments.Sales ? SalesSlots : OtherSlots;
                slot = new SemaphoreSlim(size, size);
                _slots[department] = slot;
            }

            return slot;
        }
    }
}

/// <summary>Обработчик подразделения «Продажи» — AI-агент продаж без изменений его правил (<see cref="TelegramAgentService"/>).</summary>
internal sealed class SalesTelegramHandler(TelegramAgentService agent) : ITelegramDepartmentHandler
{
    public string Department => TelegramDepartments.Sales;

    public Task HandleAsync(TelegramIncoming message, TelegramRoute route, CancellationToken ct) => agent.ProcessAsync(message, ct);
}
