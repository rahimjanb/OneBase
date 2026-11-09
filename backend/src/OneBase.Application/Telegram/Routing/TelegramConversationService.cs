using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Domain.Audit;
using OneBase.Domain.Telegram;

namespace OneBase.Application.Telegram.Routing;

public sealed record TelegramConversationView(
    Guid Id,
    long PeerId,
    string ChatType,
    string? Title,
    string? Username,
    long? TelegramUserId,
    string? Department,
    string RouteReason,
    string Mode,
    string Status,
    Guid? AssignedUserId,
    DateTimeOffset? LastMessageAt,
    DateTimeOffset? LastInboundAt,
    DateTimeOffset CreatedAt);

/// <summary>
/// Диалоги аккаунта и их хозяева. Хозяина закреплённого диалога меняет только человек: передать другому подразделению, оставить
/// «не распределённым», остановить автоответы AI или вернуть их. Каждое действие — в журнал аудита (<c>telegram.conversation.*</c>).
/// </summary>
public sealed class TelegramConversationService(IAppDbContext db, IAuditLogger audit, TimeProvider clock)
{
    /// <summary>Фильтр «не распределённые» в <see cref="ListAsync"/>.</summary>
    public const string Unassigned = "unassigned";

    private const int MaxRows = 200;

    /// <summary>Диалоги аккаунта, новые сверху. department: код подразделения, <see cref="Unassigned"/> или пусто — все.</summary>
    public async Task<IReadOnlyList<TelegramConversationView>> ListAsync(Guid accountId, string? department, CancellationToken ct = default)
    {
        var query = db.TelegramConversations.AsNoTracking().Where(c => c.AccountId == accountId);
        query = department switch
        {
            null or "" => query,
            Unassigned => query.Where(c => c.Department == null),
            _ => query.Where(c => c.Department == department),
        };

        var rows = await (from c in query
                          join chat in db.TelegramChats.AsNoTracking() on new { c.AccountId, c.PeerId } equals new { chat.AccountId, chat.PeerId } into chats
                          from chat in chats.DefaultIfEmpty()
                          orderby c.LastMessageAt descending
                          select new { c, Title = chat == null ? null : chat.Title, Username = chat == null ? null : chat.Username })
            .Take(MaxRows).ToListAsync(ct);
        return rows.Select(r => View(r.c, r.Title, r.Username)).ToList();
    }

    /// <summary>Передать диалог подразделению (null — «не распределён»). Подразделение должно быть включено у аккаунта.</summary>
    public async Task<TelegramConversationView> AssignAsync(Guid accountId, long peerId, string? department, Guid userId, CancellationToken ct = default)
    {
        var target = string.IsNullOrWhiteSpace(department) ? null : department.Trim();
        var enabled = await db.TelegramAccountDepartments.AsNoTracking().Where(d => d.AccountId == accountId).Select(d => d.Department).ToListAsync(ct);
        if (enabled.Count == 0)
        {
            throw new TelegramException("У аккаунта не заданы подразделения — все его диалоги у продаж. Сначала задайте подразделения аккаунта.",
                TelegramErrorKind.Config);
        }

        if (target is not null && !enabled.Contains(target))
        {
            throw new TelegramException($"Подразделение «{target}» не работает с этим аккаунтом.", TelegramErrorKind.Config);
        }

        var conversation = await OpenAsync(accountId, peerId, ct);
        var from = conversation.Department;
        conversation.Department = target;
        conversation.RouteReason = "manual";
        await SaveAsync(conversation, userId, ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.conversation.assigned", nameof(TelegramConversation), conversation.Id.ToString(),
            new { accountId, peerId, from, to = target }, ct);
        return await ViewAsync(conversation, ct);
    }

    /// <summary>Остановить автоответы AI в диалоге (ведёт человек) или вернуть их.</summary>
    public async Task<TelegramConversationView> SetModeAsync(Guid accountId, long peerId, TelegramConversationMode mode, Guid userId, CancellationToken ct = default)
    {
        var conversation = await db.TelegramConversations.FirstOrDefaultAsync(c => c.AccountId == accountId && c.PeerId == peerId, ct)
                           ?? throw new KeyNotFoundException("Диалог не найден.");
        var from = conversation.Mode;
        conversation.Mode = mode;
        await SaveAsync(conversation, userId, ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.conversation.mode", nameof(TelegramConversation), conversation.Id.ToString(),
            new { accountId, peerId, from = from.ToString(), to = mode.ToString() }, ct);
        return await ViewAsync(conversation, ct);
    }

    /// <summary>Диалог чата: существующий или новый (чат должен быть в списке чатов аккаунта).</summary>
    private async Task<TelegramConversation> OpenAsync(Guid accountId, long peerId, CancellationToken ct)
    {
        var conversation = await db.TelegramConversations.FirstOrDefaultAsync(c => c.AccountId == accountId && c.PeerId == peerId, ct);
        if (conversation is not null)
        {
            return conversation;
        }

        var chat = await db.TelegramChats.AsNoTracking().FirstOrDefaultAsync(c => c.AccountId == accountId && c.PeerId == peerId, ct)
                   ?? throw new KeyNotFoundException("Чат не найден у аккаунта — обновите список чатов.");
        conversation = new TelegramConversation
        {
            AccountId = accountId,
            PeerId = peerId,
            ChatType = chat.Type,
            TelegramUserId = chat.Type == TelegramChatType.Private ? chat.PeerId : null,
            RouteReason = "manual",
        };
        db.TelegramConversations.Add(conversation);
        return conversation;
    }

    private async Task SaveAsync(TelegramConversation conversation, Guid userId, CancellationToken ct)
    {
        conversation.UpdatedAt = clock.GetUtcNow();
        conversation.UpdatedById = userId;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new TelegramException("Диалог только что открылся по новому сообщению — обновите страницу и повторите.", TelegramErrorKind.Config);
        }
    }

    private async Task<TelegramConversationView> ViewAsync(TelegramConversation c, CancellationToken ct)
    {
        var chat = await db.TelegramChats.AsNoTracking().Where(x => x.AccountId == c.AccountId && x.PeerId == c.PeerId)
            .Select(x => new { x.Title, x.Username }).FirstOrDefaultAsync(ct);
        return View(c, chat?.Title, chat?.Username);
    }

    private static TelegramConversationView View(TelegramConversation c, string? title, string? username) =>
        new(c.Id, c.PeerId, c.ChatType.ToString(), title, username, c.TelegramUserId, c.Department, c.RouteReason, c.Mode.ToString(), c.Status.ToString(),
            c.AssignedUserId, c.LastMessageAt, c.LastInboundAt, c.CreatedAt);
}
