using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.Application.Abstractions;
using OneBase.Domain.Telegram;

namespace OneBase.Application.Telegram.Routing;

/// <summary>Коды подразделений, у которых есть работа в Telegram (совпадают с <c>public.Departments.Code</c>).</summary>
public static class TelegramDepartments
{
    public const string Sales = "sales";
    public const string Hr = "hr";

    /// <summary>Обработчик для любого отдела без своего (ИИ-агенты отделов: снабжение, финансы…); продажи и HR — свои обработчики.</summary>
    public const string Any = "*";

    /// <summary>
    /// Аккаунты, которые работают для подразделения. Аккаунт без настроенных подразделений — у продаж (как до маршрутизации), поэтому
    /// для продаж он подходит, для остальных — нет.
    /// </summary>
    public static IQueryable<TelegramAccount> AccountsServing(this IAppDbContext db, string department) =>
        department == Sales
            ? db.TelegramAccounts.Where(a => !db.TelegramAccountDepartments.Any(d => d.AccountId == a.Id)
                                             || db.TelegramAccountDepartments.Any(d => d.AccountId == a.Id && d.Department == department))
            : db.TelegramAccounts.Where(a => db.TelegramAccountDepartments.Any(d => d.AccountId == a.Id && d.Department == department));

    public static Task<bool> ServesAsync(this IAppDbContext db, Guid accountId, string department, CancellationToken ct = default) =>
        db.AccountsServing(department).AnyAsync(a => a.Id == accountId, ct);
}

/// <summary>
/// Куда маршрутизатор отправил сообщение: подразделение (null — «не распределён», автоматически не отвечает никто), причина, диалог
/// (null — у аккаунта не настроены подразделения: всё у продаж без записи диалога) и его режим.
/// </summary>
public sealed record TelegramRoute(
    string? Department,
    string Reason,
    Guid? ConversationId = null,
    TelegramConversationMode Mode = TelegramConversationMode.Ai,
    TelegramConversationStatus Status = TelegramConversationStatus.Active)
{
    /// <summary>Аккаунт без подразделений или маршрутизация выключена: всё — агенту продаж, как до маршрутизации.</summary>
    public static TelegramRoute Legacy { get; } = new(TelegramDepartments.Sales, "legacy");

    /// <summary>AI подразделения отвечает: диалог распределён, не передан человеку и не закрыт.</summary>
    public bool AutoReply => Department is not null && Mode == TelegramConversationMode.Ai && Status == TelegramConversationStatus.Active;
}

/// <summary>
/// Обработчик входящих сообщений подразделения (продажи — AI-агент продаж; HR — рекрутер). Получает только диалоги своего
/// подразделения; сообщения одного чата приходят строго по очереди.
/// </summary>
public interface ITelegramDepartmentHandler
{
    string Department { get; }

    Task HandleAsync(TelegramIncoming message, TelegramRoute route, CancellationToken ct);
}

/// <summary>
/// Признак подразделения в первом сообщении нового диалога (например, у HR — ответ на пост вакансии или код вакансии). Учитывается,
/// только если подразделение включено у аккаунта.
/// </summary>
public interface ITelegramRouteClaim
{
    string Department { get; }

    ValueTask<bool> ClaimsAsync(TelegramIncoming message, CancellationToken ct);
}

/// <summary>
/// Маршрутизатор входящих Telegram: решает, какое подразделение отвечает в диалоге. Правила (без AI):
/// <list type="number">
/// <item>у аккаунта нет подразделений — продажи, как до маршрутизации (диалог не записывается);</item>
/// <item>диалог уже закреплён — его хозяин (подразделение выключено у аккаунта — «не распределён»);</item>
/// <item>новый диалог: признак ровно одного включённого подразделения — ему; признаки двух — «не распределён»; признаков нет —
///   подразделение аккаунта по умолчанию (нет такого — «не распределён»);</item>
/// <item>человек из списка пользователей агента продаж на номере, который работает и на продажи, — продажам (другое подразделение его не забирает).</item>
/// </list>
/// Сменить хозяина закреплённого диалога может только человек (<see cref="TelegramConversationService"/>).
/// </summary>
public sealed class TelegramMessageRouter(IAppDbContext db, IEnumerable<ITelegramRouteClaim> claims, ILogger<TelegramMessageRouter> logger)
{
    public async Task<TelegramRoute> RouteAsync(TelegramIncoming m, CancellationToken ct)
    {
        var departments = await db.TelegramAccountDepartments.AsNoTracking().Where(d => d.AccountId == m.AccountId).ToListAsync(ct);
        if (departments.Count == 0)
        {
            return TelegramRoute.Legacy;
        }

        var enabled = departments.Select(d => d.Department).ToHashSet(StringComparer.Ordinal);
        var fallback = departments.FirstOrDefault(d => d.IsDefault)?.Department;
        var conversation = await db.TelegramConversations.AsNoTracking()
            .FirstOrDefaultAsync(c => c.AccountId == m.AccountId && c.PeerId == m.Chat.PeerId, ct);
        if (conversation is null)
        {
            // Свои исходящие и сообщения ботов диалог не открывают: их видит подразделение по умолчанию (агент такие пропускает сам).
            if (m.Outgoing || m.FromBot || m.FromUserId is null)
            {
                return new TelegramRoute(fallback, "service");
            }

            conversation = await OpenAsync(m, enabled, fallback, ct);
        }
        else
        {
            await TouchAsync(conversation.Id, m, ct);
        }

        var department = conversation.Department is { } d && enabled.Contains(d) ? d : null;
        var reason = department is null && conversation.Department is not null ? "department_disabled" : conversation.RouteReason;
        return new TelegramRoute(department, reason, conversation.Id, conversation.Mode, conversation.Status);
    }

    private async Task<TelegramConversation> OpenAsync(TelegramIncoming m, HashSet<string> enabled, string? fallback, CancellationToken ct)
    {
        var claimed = new List<string>();
        foreach (var claim in claims.Where(c => enabled.Contains(c.Department)))
        {
            if (!claimed.Contains(claim.Department) && await claim.ClaimsAsync(m, ct))
            {
                claimed.Add(claim.Department);
            }
        }

        var (department, reason) = claimed.Count switch
        {
            1 => (claimed[0], "claim"),
            > 1 => ((string?)null, "conflict"),
            _ => (fallback, fallback is null ? "no_default" : "default"),
        };

        // Человек из списка агента продаж на номере, который работает и на продажи, — продажам, как до маршрутизации: другое подразделение
        // (признак или «по умолчанию») не забирает у агента продаж его людей.
        if (department != TelegramDepartments.Sales && enabled.Contains(TelegramDepartments.Sales) && await IsSalesUserAsync(m, ct))
        {
            (department, reason) = (TelegramDepartments.Sales, "sales_user");
        }

        var row = new TelegramConversation
        {
            AccountId = m.AccountId,
            PeerId = m.Chat.PeerId,
            ChatType = m.Chat.Type,
            TelegramUserId = m.Chat.Type == TelegramChatType.Private ? m.FromUserId : null,
            Department = department,
            RouteReason = reason,
            LastMessageAt = m.Date,
            LastInboundAt = m.Date,
        };
        db.TelegramConversations.Add(row);
        try
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Telegram {Account}: диалог {Peer} → {Department} ({Reason})", m.AccountId, m.Chat.PeerId,
                department ?? "не распределён", reason);
            return row;
        }
        catch (DbUpdateException)
        {
            // Диалог уже открыло другое сообщение — берём его хозяина.
            db.TelegramConversations.Remove(row);
            return await db.TelegramConversations.AsNoTracking().FirstAsync(c => c.AccountId == m.AccountId && c.PeerId == m.Chat.PeerId, ct);
        }
    }

    /// <summary>Человек есть в списке пользователей агента продаж этого аккаунта (по Telegram ID или ожидающий — по username).</summary>
    private async Task<bool> IsSalesUserAsync(TelegramIncoming m, CancellationToken ct)
    {
        var from = m.FromUserId!.Value;
        var username = string.IsNullOrWhiteSpace(m.FromUsername) ? null : m.FromUsername.Trim().TrimStart('@').ToLowerInvariant();
        return await db.TelegramUserPermissions.AnyAsync(p => p.AccountId == m.AccountId
            && (p.TelegramUserId == from || (username != null && p.TelegramUserId == null && p.Username == username)), ct);
    }

    private async Task TouchAsync(Guid id, TelegramIncoming m, CancellationToken ct)
    {
        await db.TelegramConversations.Where(c => c.Id == id && (c.LastMessageAt == null || c.LastMessageAt < m.Date))
            .ExecuteUpdateAsync(u => u.SetProperty(c => c.LastMessageAt, m.Date), ct);
        if (!m.Outgoing)
        {
            await db.TelegramConversations.Where(c => c.Id == id && (c.LastInboundAt == null || c.LastInboundAt < m.Date))
                .ExecuteUpdateAsync(u => u.SetProperty(c => c.LastInboundAt, m.Date), ct);
        }
    }
}
