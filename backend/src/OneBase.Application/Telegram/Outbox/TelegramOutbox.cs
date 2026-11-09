using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Telegram.Routing;
using OneBase.Domain.Telegram;

namespace OneBase.Application.Telegram.Outbox;

/// <summary>Что отправить: аккаунт, чат, подразделение, вид, текст, ключ идемпотентности (одно действие — одна отправка), автор, связанная запись.</summary>
public sealed record OutboxRequest(
    Guid AccountId,
    long PeerId,
    Guid? ConversationId,
    string Department,
    string Kind,
    string Text,
    string IdempotencyKey,
    string Author,
    Guid? CreatedById = null,
    int? ReplyToMessageId = null,
    string? RelatedType = null,
    Guid? RelatedId = null,
    DateTimeOffset? NotBefore = null,
    int TypingSeconds = 0);

/// <summary>После отправки или окончательной ошибки — подразделение обновляет свои записи (история переписки, публикация, исключение для HR).</summary>
public interface ITelegramOutboxObserver
{
    Task OnSentAsync(TelegramOutgoingMessage row, CancellationToken ct);

    Task OnFailedAsync(TelegramOutgoingMessage row, CancellationToken ct);
}

/// <summary>Поставить сообщение в очередь исходящих подразделения. Тот же ключ — та же строка (повтор действия дубля не даёт).</summary>
public sealed class TelegramOutbox(IAppDbContext db, TimeProvider clock)
{
    public async Task<TelegramOutgoingMessage> EnqueueAsync(OutboxRequest r, CancellationToken ct)
    {
        var existing = await db.TelegramOutgoingMessages.FirstOrDefaultAsync(m => m.IdempotencyKey == r.IdempotencyKey, ct);
        if (existing is not null)
        {
            return existing;
        }

        var text = r.Text.Trim();
        if (text.Length == 0)
        {
            throw new FieldValidationException("Пустое сообщение.");
        }

        if (text.Length > TelegramRules.MaxMessageLength)
        {
            throw new FieldValidationException($"Сообщение длиннее {TelegramRules.MaxMessageLength} символов.");
        }

        var row = new TelegramOutgoingMessage
        {
            AccountId = r.AccountId,
            PeerId = r.PeerId,
            ConversationId = r.ConversationId,
            Department = r.Department,
            Kind = r.Kind,
            Text = text,
            ReplyToMessageId = r.ReplyToMessageId,
            IdempotencyKey = r.IdempotencyKey,
            Author = r.Author,
            CreatedById = r.CreatedById,
            RelatedType = r.RelatedType,
            RelatedId = r.RelatedId,
            TypingSeconds = Math.Clamp(r.TypingSeconds, 0, TelegramOutboxRunner.MaxTypingSeconds),
            NextAttemptAt = r.NotBefore ?? clock.GetUtcNow(),
        };
        db.TelegramOutgoingMessages.Add(row);
        try
        {
            await db.SaveChangesAsync(ct);
            return row;
        }
        catch (DbUpdateException)
        {
            db.TelegramOutgoingMessages.Remove(row);
            return await db.TelegramOutgoingMessages.FirstAsync(m => m.IdempotencyKey == r.IdempotencyKey, ct);
        }
    }
}

/// <summary>
/// Отправка очереди исходящих: по одному сообщению на аккаунт и подразделение за круг, с паузой и суточным лимитом подразделения на номере.
/// FLOOD_WAIT — ждём, сколько просит Telegram (попыткой не считается); временная ошибка — до 3 попыток; «неизвестно, дошло ли» — Unknown без
/// автоповтора; PEER_FLOOD — отправка подразделения с номера останавливается на сутки (продажи этим не останавливаются).
/// </summary>
public sealed class TelegramOutboxRunner(
    IAppDbContext db,
    ITelegramGateway telegram,
    IEnumerable<ITelegramOutboxObserver> observers,
    TimeProvider clock,
    ILogger<TelegramOutboxRunner> logger)
{
    public static readonly TimeSpan PeerFloodPause = TimeSpan.FromHours(24);

    /// <summary>«Печатает…» перед сообщением ИИ-агента — не дольше (очередь номера не должна стоять).</summary>
    public const int MaxTypingSeconds = 4;
    private static readonly TimeSpan Tashkent = TimeSpan.FromHours(5);

    /// <summary>После запуска сервера: «отправляется» — в Unknown (могло уйти), без автоповтора.</summary>
    public Task<int> RecoverAsync(CancellationToken ct) =>
        db.TelegramOutgoingMessages.Where(m => m.Status == TelegramOutboxStatus.Sending)
            .ExecuteUpdateAsync(u => u.SetProperty(m => m.Status, TelegramOutboxStatus.Unknown)
                .SetProperty(m => m.Error, "Сервер перезапустился во время отправки — неизвестно, дошло ли сообщение; проверьте чат."), ct);

    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var stale = now - TelegramRules.StaleSending;
        await db.TelegramOutgoingMessages.Where(m => m.Status == TelegramOutboxStatus.Sending && m.UpdatedAt < stale)
            .ExecuteUpdateAsync(u => u.SetProperty(m => m.Status, TelegramOutboxStatus.Unknown)
                .SetProperty(m => m.Error, "Telegram долго не подтверждал отправку — неизвестно, дошло ли сообщение; проверьте чат."), ct);

        var due = await db.TelegramOutgoingMessages.AsNoTracking().Where(m => m.Status == TelegramOutboxStatus.Pending && m.NextAttemptAt <= now)
            .OrderBy(m => m.NextAttemptAt).Take(200).ToListAsync(ct);
        var sent = 0;
        foreach (var group in due.GroupBy(m => (m.AccountId, m.Department)))
        {
            var settings = await db.TelegramAccountDepartments.AsNoTracking()
                .FirstOrDefaultAsync(d => d.AccountId == group.Key.AccountId && d.Department == group.Key.Department, ct);
            if (settings?.PausedUntil is { } paused && paused > now)
            {
                continue;
            }

            var interval = TimeSpan.FromSeconds(Math.Max(1, settings?.MinIntervalSeconds ?? 5));
            var last = await db.TelegramOutgoingMessages.Where(m => m.AccountId == group.Key.AccountId && m.Department == group.Key.Department && m.SentAt != null)
                .MaxAsync(m => m.SentAt, ct);
            if (last is { } l && now - l < interval)
            {
                continue;
            }

            var dayStart = new DateTimeOffset((now + Tashkent).Date, TimeSpan.Zero) - Tashkent;
            var today = await db.TelegramOutgoingMessages.CountAsync(m => m.AccountId == group.Key.AccountId && m.Department == group.Key.Department
                && m.Status == TelegramOutboxStatus.Sent && m.SentAt >= dayStart, ct);
            if (today >= (settings?.DailyLimit ?? 200))
            {
                continue;
            }

            if (await SendAsync(group.First().Id, now, ct))
            {
                sent++;
            }
        }

        return sent;
    }

    private async Task<bool> SendAsync(Guid id, DateTimeOffset now, CancellationToken ct)
    {
        var claimed = await db.TelegramOutgoingMessages.Where(m => m.Id == id && m.Status == TelegramOutboxStatus.Pending)
            .ExecuteUpdateAsync(u => u.SetProperty(m => m.Status, TelegramOutboxStatus.Sending).SetProperty(m => m.Attempts, m => m.Attempts + 1)
                .SetProperty(m => m.UpdatedAt, now), ct);
        if (claimed == 0)
        {
            return false;
        }

        var row = await db.TelegramOutgoingMessages.FirstAsync(m => m.Id == id, ct);
        var chat = await db.TelegramChats.AsNoTracking().FirstOrDefaultAsync(c => c.AccountId == row.AccountId && c.PeerId == row.PeerId, ct);
        if (chat is null)
        {
            await FailAsync(row, TelegramOutboxStatus.Failed, "Чат не найден у аккаунта — обновите список чатов в «Настройки → Telegram».", ct);
            return false;
        }

        var peer = new TelegramPeerRef(chat.PeerId, chat.Type, chat.IsChannelPeer, chat.AccessHash);
        if (row.TypingSeconds > 0)
        {
            // ИИ-агент пишет как человек: сначала «печатает…». Не вышло — не беда, сообщение всё равно уходит.
            try
            {
                await telegram.TypingAsync(row.AccountId, peer, ct);
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(row.TypingSeconds, MaxTypingSeconds)), ct);
            }
            catch (TelegramException)
            {
            }
        }

        try
        {
            var result = await telegram.SendAsync(row.AccountId, peer, new TelegramOutgoing(row.Text, false, [], row.ReplyToMessageId), ct);
            row.Status = TelegramOutboxStatus.Sent;
            row.SentAt = clock.GetUtcNow();
            row.UpdatedAt = row.SentAt;
            row.MessageIds = string.Join(',', result.MessageIds);
            row.Error = null;
            await db.SaveChangesAsync(ct);
            foreach (var observer in observers)
            {
                await observer.OnSentAsync(row, ct);
            }

            return true;
        }
        catch (TelegramException ex) when (ex.Kind == TelegramErrorKind.Flood)
        {
            row.Status = TelegramOutboxStatus.Pending;
            row.Attempts = Math.Max(0, row.Attempts - 1);
            row.NextAttemptAt = clock.GetUtcNow().AddSeconds(Math.Max(5, ex.WaitSeconds ?? 60));
            row.Error = ex.Message;
            await db.SaveChangesAsync(ct);
        }
        catch (TelegramException ex) when (ex.Kind == TelegramErrorKind.Transient && row.Attempts < TelegramRules.MaxAttempts)
        {
            row.Status = TelegramOutboxStatus.Pending;
            row.NextAttemptAt = clock.GetUtcNow() + TelegramRules.RetryDelay(row.Attempts);
            row.Error = ex.Message;
            await db.SaveChangesAsync(ct);
        }
        catch (TelegramException ex) when (ex.Message.Contains("PEER_FLOOD", StringComparison.Ordinal))
        {
            await db.TelegramAccountDepartments.Where(d => d.AccountId == row.AccountId && d.Department == row.Department)
                .ExecuteUpdateAsync(u => u.SetProperty(d => d.PausedUntil, clock.GetUtcNow() + PeerFloodPause).SetProperty(d => d.PauseReason, ex.Message), ct);
            logger.LogWarning("Telegram {Account}: PEER_FLOOD — отправка подразделения {Department} остановлена на сутки", row.AccountId, row.Department);
            await FailAsync(row, TelegramOutboxStatus.Failed, ex.Message, ct);
        }
        catch (TelegramException ex)
        {
            var unknown = ex.Message.Contains("неизвестно, дошло ли", StringComparison.Ordinal);
            await FailAsync(row, unknown ? TelegramOutboxStatus.Unknown : TelegramOutboxStatus.Failed, ex.Message, ct);
        }

        return false;
    }

    private async Task FailAsync(TelegramOutgoingMessage row, TelegramOutboxStatus status, string error, CancellationToken ct)
    {
        row.Status = status;
        row.Error = error.Length <= 1000 ? error : error[..1000];
        row.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        foreach (var observer in observers)
        {
            await observer.OnFailedAsync(row, ct);
        }
    }
}
