using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Domain.Audit;
using OneBase.Domain.Telegram;

namespace OneBase.Application.Telegram;

public enum TelegramLinkOutcome
{
    Linked,
    Invalid,
    Expired,

    /// <summary>Администратор указал для этого Telegram другого пользователя OneBase.</summary>
    OtherUser,
}

/// <summary>
/// Код привязки, присланный аккаунту в Telegram: подтверждает, что этот Telegram (по ID из сообщения, не по username) принадлежит
/// пользователю OneBase, выпустившему код. Код одноразовый и действует 15 минут; в базе — только хэш.
/// </summary>
public sealed class TelegramLinkService(IAppDbContext db, IAuditLogger audit, TimeProvider clock)
{
    /// <summary>
    /// Код верный и не истёк → строка пользователя в списке аккаунта подтверждается. Если администратор указал другого пользователя OneBase —
    /// отказ. Если строки нет — создаётся в режиме «Только отправленные данные»: полный доступ включает администратор.
    /// </summary>
    public async Task<(TelegramLinkOutcome Outcome, TelegramUserPermission? Permission)> RedeemAsync(Guid accountId, long telegramUserId,
        string? username, string? name, string code, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var hash = TelegramRules.HashLinkCode(code);
        var row = await db.TelegramLinkCodes.FirstOrDefaultAsync(c => c.CodeHash == hash, ct);
        if (row is null || row.UsedAt is not null)
        {
            return (TelegramLinkOutcome.Invalid, null);
        }

        if (row.ExpiresAt <= now)
        {
            return (TelegramLinkOutcome.Expired, null);
        }

        var cleanUsername = CleanUsername(username);
        var permission = await db.TelegramUserPermissions.FirstOrDefaultAsync(p => p.AccountId == accountId && p.TelegramUserId == telegramUserId, ct)
                         ?? (cleanUsername is null
                             ? null
                             : await db.TelegramUserPermissions.FirstOrDefaultAsync(
                                 p => p.AccountId == accountId && p.TelegramUserId == null && p.Username == cleanUsername, ct));
        if (permission is not null && permission.UserId is { } assigned && assigned != row.UserId)
        {
            return (TelegramLinkOutcome.OtherUser, null);
        }

        if (permission is null)
        {
            permission = new TelegramUserPermission
            {
                AccountId = accountId,
                Mode = TelegramAccessMode.SentOnly,
                Title = Short(name, 200),
                Username = cleanUsername,
                CreatedById = row.UserId,
            };
            db.TelegramUserPermissions.Add(permission);
        }

        if (permission.UserId != row.UserId)
        {
            permission.ConversationId = null;
        }

        permission.TelegramUserId = telegramUserId;
        permission.Username ??= cleanUsername;
        permission.Title ??= Short(name, 200);
        permission.UserId = row.UserId;
        permission.VerifiedAt = now;
        permission.UpdatedAt = now;
        row.UsedAt = now;
        row.AccountId = accountId;
        row.TelegramUserId = telegramUserId;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, row.UserId.ToString(), "telegram.link.verified", nameof(TelegramUserPermission), permission.Id.ToString(),
            new { accountId, telegramUserId, Mode = permission.Mode.ToString() }, ct);
        return (TelegramLinkOutcome.Linked, permission);
    }

    /// <summary>Код увидели не в личном чате (например, в группе): его могли прочитать другие — гасим, пусть пользователь возьмёт новый.</summary>
    public async Task<bool> BurnAsync(string code, CancellationToken ct = default)
    {
        var hash = TelegramRules.HashLinkCode(code);
        var now = clock.GetUtcNow();
        var row = await db.TelegramLinkCodes.FirstOrDefaultAsync(c => c.CodeHash == hash && c.UsedAt == null && c.ExpiresAt > now, ct);
        if (row is null)
        {
            return false;
        }

        row.ExpiresAt = now;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static string? CleanUsername(string? username)
    {
        var clean = username?.Trim().TrimStart('@').ToLowerInvariant();
        return string.IsNullOrEmpty(clean) ? null : clean.Length > 64 ? clean[..64] : clean;
    }

    private static string? Short(string? value, int max)
    {
        var t = value?.Trim();
        return string.IsNullOrEmpty(t) ? null : t.Length > max ? t[..max] : t;
    }
}
