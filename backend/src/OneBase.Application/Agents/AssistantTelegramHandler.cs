using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Telegram;
using OneBase.Application.Telegram.Routing;
using OneBase.Domain.Telegram;

namespace OneBase.Application.Agents;

/// <summary>
/// Обработчик Telegram для отделов без своего (снабжение, финансы, маркетинг…): личный чат — история диалога (сообщения сохраняются, даже
/// когда диалог ведёт сотрудник), затем ход ИИ-агента отдела, если он включён и диалог ведёт AI. Группы не обрабатываются.
/// </summary>
public sealed class AssistantTelegramHandler(IAppDbContext db, IEnumerable<IAssistantAgent> agents, TimeProvider clock) : ITelegramDepartmentHandler
{
    public string Department => TelegramDepartments.Any;

    public async Task HandleAsync(TelegramIncoming m, TelegramRoute route, CancellationToken ct)
    {
        if (route.Department is not { } department || route.ConversationId is not { } conversationId || m.FromBot || m.Chat.Type != TelegramChatType.Private
            || m.FromUserId is null && !m.Outgoing)
        {
            return;
        }

        await EnsureChatAsync(m, ct);
        var file = m.File ?? m.Photo;
        if (m.Outgoing)
        {
            await SaveAsync(conversationId, m, TelegramMessageDirection.Out, "phone", file?.FileName, ct);
            return;
        }

        if (!await RegisterAsync(m, department, ct))
        {
            return; // повторная доставка
        }

        await SaveAsync(conversationId, m, TelegramMessageDirection.In, "contact", file?.FileName, ct);
        if (route.AutoReply && agents.FirstOrDefault() is { } agent)
        {
            await agent.ReplyAsync(new AssistantTurn(m.AccountId, m.Chat.PeerId, conversationId, department, m.MessageId, m.Text, file?.FileName,
                m.FromName ?? (m.FromUsername is { } u ? "@" + u : m.ChatTitle), m.Date), ct);
        }
    }

    /// <summary>Повторная доставка — по уникальному ключу аккаунт × чат × сообщение (та же таблица, что у агента продаж и HR).</summary>
    private async Task<bool> RegisterAsync(TelegramIncoming m, string department, CancellationToken ct)
    {
        if (await db.TelegramIncomingMessages.AnyAsync(x => x.AccountId == m.AccountId && x.PeerId == m.Chat.PeerId && x.MessageId == m.MessageId, ct))
        {
            return false;
        }

        var row = new TelegramIncomingMessage
        {
            AccountId = m.AccountId,
            PeerId = m.Chat.PeerId,
            MessageId = m.MessageId,
            FromUserId = m.FromUserId,
            SentAt = m.Date,
            Status = "processed",
            Reason = department,
        };
        db.TelegramIncomingMessages.Add(row);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            db.TelegramIncomingMessages.Remove(row);
            return false;
        }
    }

    private async Task EnsureChatAsync(TelegramIncoming m, CancellationToken ct)
    {
        if (await db.TelegramChats.AnyAsync(c => c.AccountId == m.AccountId && c.PeerId == m.Chat.PeerId, ct))
        {
            return;
        }

        var chat = new TelegramChat
        {
            AccountId = m.AccountId,
            PeerId = m.Chat.PeerId,
            Type = m.Chat.Type,
            IsChannelPeer = m.Chat.IsChannelPeer,
            AccessHash = m.Chat.AccessHash,
            Title = string.IsNullOrWhiteSpace(m.ChatTitle) ? m.Chat.PeerId.ToString(System.Globalization.CultureInfo.InvariantCulture) : m.ChatTitle,
            Username = m.FromUsername,
            SyncedAt = clock.GetUtcNow(),
        };
        db.TelegramChats.Add(chat);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.TelegramChats.Remove(chat);
        }
    }

    private async Task SaveAsync(Guid conversationId, TelegramIncoming m, TelegramMessageDirection direction, string author, string? fileName, CancellationToken ct)
    {
        var row = new TelegramConversationMessage
        {
            ConversationId = conversationId,
            AccountId = m.AccountId,
            PeerId = m.Chat.PeerId,
            MessageId = m.MessageId,
            Direction = direction,
            Text = string.IsNullOrEmpty(m.Text) ? null : m.Text.Length <= 8000 ? m.Text : m.Text[..8000],
            FileName = fileName,
            Author = author,
            SentAt = m.Date,
        };
        db.TelegramConversationMessages.Add(row);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.TelegramConversationMessages.Remove(row); // то же сообщение уже сохранено
        }
    }
}
