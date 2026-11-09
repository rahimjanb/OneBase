using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Telegram.Outbox;
using OneBase.Application.Telegram.Routing;
using OneBase.Domain.Telegram;

namespace OneBase.Application.Agents;

/// <summary>
/// Сообщение отдела с ИИ-агентом (не HR — у HR свой центр исключений) не ушло: диалог — сотруднику, тем, кто управляет агентом, — уведомление.
/// История отправленных сообщений записывается общим наблюдателем очереди.
/// </summary>
public sealed class DialogAgentOutboxObserver(IAppDbContext db, DialogAgentDelivery delivery, TimeProvider clock) : ITelegramOutboxObserver
{
    public Task OnSentAsync(TelegramOutgoingMessage row, CancellationToken ct) => Task.CompletedTask;

    public async Task OnFailedAsync(TelegramOutgoingMessage row, CancellationToken ct)
    {
        if (row.ConversationId is not { } conversationId || row.Department is TelegramDepartments.Hr or TelegramDepartments.Sales)
        {
            return;
        }

        var agent = await db.DialogAgents.AsNoTracking().Where(a => a.Department == row.Department).OrderByDescending(a => a.IsDefault).FirstOrDefaultAsync(ct);
        if (agent is null)
        {
            return;
        }

        await db.TelegramConversations.Where(c => c.Id == conversationId)
            .ExecuteUpdateAsync(u => u.SetProperty(c => c.Mode, TelegramConversationMode.Manual).SetProperty(c => c.UpdatedAt, clock.GetUtcNow()), ct);
        await delivery.NotifyAsync(agent, $"{agent.Name}: сообщение {(row.Status == TelegramOutboxStatus.Unknown ? "могло не дойти" : "не отправлено")}", row.Error, ct, "dialogs");
    }
}
