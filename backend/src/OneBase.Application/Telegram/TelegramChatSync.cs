using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Domain.Telegram;

namespace OneBase.Application.Telegram;

/// <summary>Чаты аккаунта из Telegram → таблица чатов: новые добавляются, известные обновляются, пропавшие помечаются «писать нельзя».</summary>
public static class TelegramChatSync
{
    public static async Task<int> SyncAsync(IAppDbContext db, ITelegramGateway gateway, Guid accountId, DateTimeOffset now, CancellationToken ct)
    {
        var dialogs = await gateway.DialogsAsync(accountId, ct);
        var existing = await db.TelegramChats.Where(c => c.AccountId == accountId).ToDictionaryAsync(c => c.PeerId, ct);
        var seen = new HashSet<long>();
        foreach (var d in dialogs)
        {
            if (!seen.Add(d.Peer.PeerId))
            {
                continue;
            }

            if (!existing.TryGetValue(d.Peer.PeerId, out var chat))
            {
                chat = new TelegramChat { AccountId = accountId, PeerId = d.Peer.PeerId, Title = d.Title };
                db.TelegramChats.Add(chat);
            }

            chat.Type = d.Peer.Type;
            chat.IsChannelPeer = d.Peer.IsChannelPeer;
            // access_hash 0 у недоступного чата не затирает известный.
            if (d.Peer.AccessHash != 0 || (!d.Peer.IsChannelPeer && d.Peer.Type != TelegramChatType.Private))
            {
                chat.AccessHash = d.Peer.AccessHash;
            }

            chat.Title = Short(d.Title, 255) ?? d.Peer.PeerId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            chat.Username = Short(d.Username, 64);
            chat.CanSend = d.CanSend;
            chat.CannotSendReason = d.CanSend ? null : Short(d.Reason, 200) ?? "аккаунт не может писать в этот чат";
            chat.SyncedAt = now;
        }

        foreach (var gone in existing.Values.Where(c => !seen.Contains(c.PeerId)))
        {
            gone.CanSend = false;
            gone.CannotSendReason = "чата больше нет в диалогах аккаунта";
        }

        await db.SaveChangesAsync(ct);
        return seen.Count;
    }

    private static string? Short(string? value, int max)
    {
        var t = value?.Trim();
        return string.IsNullOrEmpty(t) ? null : t.Length > max ? t[..max] : t;
    }
}
