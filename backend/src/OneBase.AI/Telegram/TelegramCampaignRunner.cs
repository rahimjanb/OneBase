using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneBase.Application.Abstractions;
using OneBase.Application.Telegram;
using OneBase.Domain.Telegram;

namespace OneBase.AI.Telegram;

/// <summary>
/// Очередь рассылок (в PostgreSQL — строки получателей). Один аккаунт отправляет строго по одному сообщению с паузой рассылки;
/// разные аккаунты — параллельно. Перед отправкой строка получателя помечается «Отправляется» — если сервер упал посреди отправки,
/// после запуска она уходит в ошибку «неизвестно, ушло ли» без автоповтора (лучше пропуск, чем дубль). FLOOD_WAIT — ждём сколько
/// просит Telegram, не считая попыткой; PEER_FLOOD и отозванная сессия — пауза всей рассылки; временные ошибки — до 3 попыток.
/// </summary>
public sealed class TelegramCampaignRunner(IServiceScopeFactory scopes, TimeProvider clock, ILogger<TelegramCampaignRunner> logger)
{
    /// <summary>FLOOD_WAIT до стольких секунд ждём на месте; дольше — получатель откладывается, очередь аккаунта отпускается.</summary>
    public const int InlineFloodWaitSeconds = 120;

    /// <summary>Пауза между отправками и ожидание FLOOD_WAIT (в тестах — без ожидания).</summary>
    public Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = Task.Delay;

    /// <summary>После запуска сервера: всё, что было «Отправляется», — в ошибку без автоповтора.</summary>
    public async Task<int> RecoverAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        return await FailSendingAsync(db, null, ct);
    }

    /// <summary>Наступило время запланированных → в очередь; зависшие «Отправляется» → в ошибку. Возвращает аккаунты, которым есть что отправлять.</summary>
    public async Task<IReadOnlyList<Guid>> DueAccountsAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var now = clock.GetUtcNow();
        await db.TelegramCampaigns.Where(c => c.Status == TelegramCampaignStatus.Scheduled && c.ScheduledAt <= now)
            .ExecuteUpdateAsync(u => u.SetProperty(c => c.Status, TelegramCampaignStatus.Queued), ct);
        await FailSendingAsync(db, now - TelegramRules.StaleSending, ct);
        var active = await db.TelegramCampaigns.AsNoTracking()
            .Where(c => c.Status == TelegramCampaignStatus.Queued || c.Status == TelegramCampaignStatus.Sending)
            .Select(c => new { c.Id, c.AccountId }).ToListAsync(ct);
        foreach (var campaign in active)
        {
            await FinishIfDoneAsync(db, campaign.Id, ct);
        }

        return await db.TelegramCampaigns.AsNoTracking()
            .Where(c => (c.Status == TelegramCampaignStatus.Queued || c.Status == TelegramCampaignStatus.Sending)
                        && db.TelegramCampaignRecipients.Any(r => r.CampaignId == c.Id && r.Status == TelegramRecipientStatus.Pending
                                                                  && (r.NextAttemptAt == null || r.NextAttemptAt <= now)))
            .Select(c => c.AccountId).Distinct().ToListAsync(ct);
    }

    /// <summary>Для тестов и ручного запуска: все готовые к отправке аккаунты по очереди.</summary>
    public async Task RunAllAsync(CancellationToken ct)
    {
        foreach (var account in await DueAccountsAsync(ct))
        {
            await RunAccountAsync(account, ct);
        }
    }

    /// <summary>Отправлять, пока у аккаунта есть готовые получатели (рассылки — в порядке создания).</summary>
    public async Task RunAccountAsync(Guid accountId, CancellationToken ct)
    {
        var cache = new Dictionary<Guid, (IReadOnlyList<TelegramCampaignService.LoadedAttachment> Files, IReadOnlyDictionary<Guid, string?> Texts)>();
        while (!ct.IsCancellationRequested)
        {
            using var scope = scopes.CreateScope();
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<IAppDbContext>();
            var now = clock.GetUtcNow();
            var next = await db.TelegramCampaignRecipients
                .Where(r => r.Status == TelegramRecipientStatus.Pending && (r.NextAttemptAt == null || r.NextAttemptAt <= now)
                            && r.Campaign!.AccountId == accountId
                            && (r.Campaign.Status == TelegramCampaignStatus.Queued || r.Campaign.Status == TelegramCampaignStatus.Sending))
                .OrderBy(r => r.Campaign!.CreatedAt).ThenBy(r => r.CreatedAt)
                .Select(r => new { r.Id, r.CampaignId })
                .FirstOrDefaultAsync(ct);
            if (next is null)
            {
                return;
            }

            var campaign = await db.TelegramCampaigns.AsNoTracking().FirstAsync(c => c.Id == next.CampaignId, ct);
            var account = await db.TelegramAccounts.AsNoTracking().FirstAsync(a => a.Id == accountId, ct);
            if (account.Status != TelegramAccountStatus.Connected)
            {
                await PauseAsync(db, campaign.Id, "Рассылка приостановлена: аккаунт Telegram не подключён. Подключите его и нажмите «Продолжить».", ct);
                continue;
            }

            // Захват строки: Pending → Sending (если её только что отменили — пропускаем).
            var claimed = await db.TelegramCampaignRecipients.Where(r => r.Id == next.Id && r.Status == TelegramRecipientStatus.Pending)
                .ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, TelegramRecipientStatus.Sending)
                    .SetProperty(r => r.Attempts, r => r.Attempts + 1)
                    .SetProperty(r => r.LastAttemptAt, now), ct);
            if (claimed == 0)
            {
                continue;
            }

            await db.TelegramCampaigns.Where(c => c.Id == campaign.Id && c.Status == TelegramCampaignStatus.Queued)
                .ExecuteUpdateAsync(u => u.SetProperty(c => c.Status, TelegramCampaignStatus.Sending).SetProperty(c => c.StartedAt, c => c.StartedAt ?? now), ct);

            var recipient = await db.TelegramCampaignRecipients.FirstAsync(r => r.Id == next.Id, ct);
            var materials = sp.GetRequiredService<TelegramMaterialService>();
            var stop = false;
            try
            {
                var chat = await db.TelegramChats.AsNoTracking().FirstOrDefaultAsync(c => c.Id == recipient.ChatId && c.AccountId == accountId, ct)
                           ?? throw new TelegramException("Чат больше не найден у аккаунта.", TelegramErrorKind.Permanent);
                if (!cache.TryGetValue(campaign.Id, out var files))
                {
                    var service = sp.GetRequiredService<TelegramCampaignService>();
                    var loaded = await service.LoadAttachmentsAsync(campaign.Id, ct);
                    files = (loaded, await TelegramCampaignService.ExtractAllAsync(materials, loaded, ct));
                    cache[campaign.Id] = files;
                }

                var sent = await sp.GetRequiredService<ITelegramGateway>()
                    .SendAsync(accountId, TelegramCampaignService.PeerOf(chat), TelegramCampaignService.MessageOf(campaign, files.Files), ct);
                recipient.Status = TelegramRecipientStatus.Sent;
                recipient.SentAt = clock.GetUtcNow();
                recipient.Error = null;
                recipient.NextAttemptAt = null;
                recipient.MessageIds = string.Join(',', sent.MessageIds);
                await db.SaveChangesAsync(CancellationToken.None);
                try
                {
                    await TelegramCampaignService.RecordMaterialsAsync(materials, campaign, recipient.PeerId, files.Files, files.Texts, campaign.CreatedById, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    // Сообщение ушло; без материала агент просто не ответит по нему — рассылку не останавливаем.
                    logger.LogWarning(ex, "Рассылка {Campaign}: материал для чата {Peer} не записан", campaign.Id, recipient.PeerId);
                }
            }
            catch (TelegramException ex) when (ex.Kind == TelegramErrorKind.Flood)
            {
                // Telegram просит подождать: это не вина получателя — попытка не считается.
                var wait = ex.WaitSeconds ?? 30;
                recipient.Status = TelegramRecipientStatus.Pending;
                recipient.Attempts = Math.Max(0, recipient.Attempts - 1);
                recipient.NextAttemptAt = clock.GetUtcNow().AddSeconds(wait);
                recipient.Error = ex.Message;
                await db.SaveChangesAsync(CancellationToken.None);
                await NoteAsync(db, campaign.Id, ex.Message, ct);
                if (wait > InlineFloodWaitSeconds)
                {
                    return; // очередь аккаунта отпускаем; продолжит следующий круг, когда истечёт ожидание
                }

                await Delay(TimeSpan.FromSeconds(wait), ct);
                continue;
            }
            catch (TelegramException ex) when (ex.Kind is TelegramErrorKind.Auth or TelegramErrorKind.Config || ex.Message.Contains("PEER_FLOOD", StringComparison.Ordinal))
            {
                // Сессия отозвана, ключи не те или Telegram ограничил аккаунт за спам — дальше слать нельзя: пауза рассылки, получатель ждёт.
                recipient.Status = TelegramRecipientStatus.Pending;
                recipient.Attempts = Math.Max(0, recipient.Attempts - 1);
                recipient.Error = ex.Message;
                await db.SaveChangesAsync(CancellationToken.None);
                await PauseAsync(db, campaign.Id, "Рассылка приостановлена: " + ex.Message, ct);
                stop = true;
            }
            catch (TelegramException ex) when (ex.Kind == TelegramErrorKind.Transient && recipient.Attempts < TelegramRules.MaxAttempts)
            {
                recipient.Status = TelegramRecipientStatus.Pending;
                recipient.NextAttemptAt = clock.GetUtcNow() + TelegramRules.RetryDelay(recipient.Attempts);
                recipient.Error = ex.Message;
                await db.SaveChangesAsync(CancellationToken.None);
            }
            catch (TelegramException ex)
            {
                recipient.Status = TelegramRecipientStatus.Failed;
                recipient.Error = ex.Message;
                recipient.NextAttemptAt = null;
                await db.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Рассылка {Campaign}: ошибка отправки в чат {Peer}", campaign.Id, recipient.PeerId);
                recipient.Status = recipient.Attempts < TelegramRules.MaxAttempts ? TelegramRecipientStatus.Pending : TelegramRecipientStatus.Failed;
                recipient.NextAttemptAt = recipient.Status == TelegramRecipientStatus.Pending ? clock.GetUtcNow() + TelegramRules.RetryDelay(recipient.Attempts) : null;
                recipient.Error = "Внутренняя ошибка OneBase — подробности в журнале сервера.";
                await db.SaveChangesAsync(CancellationToken.None);
            }

            await FinishIfDoneAsync(db, campaign.Id, ct);
            if (stop)
            {
                return;
            }

            await Delay(TimeSpan.FromSeconds(Math.Max(campaign.DelaySeconds, 1)), ct);
        }
    }

    /// <summary>Нет ожидающих и отправляемых → итог: «Завершена», «Завершена с ошибками» или «Отменена».</summary>
    private async Task FinishIfDoneAsync(IAppDbContext db, Guid campaignId, CancellationToken ct)
    {
        var statuses = await db.TelegramCampaignRecipients.AsNoTracking().Where(r => r.CampaignId == campaignId).Select(r => r.Status).ToListAsync(ct);
        if (TelegramRules.Outcome(statuses) is not { } outcome)
        {
            return;
        }

        var now = clock.GetUtcNow();
        await db.TelegramCampaigns.Where(c => c.Id == campaignId && (c.Status == TelegramCampaignStatus.Queued || c.Status == TelegramCampaignStatus.Sending))
            .ExecuteUpdateAsync(u => u.SetProperty(c => c.Status, outcome).SetProperty(c => c.FinishedAt, now), ct);
    }

    private static async Task PauseAsync(IAppDbContext db, Guid campaignId, string reason, CancellationToken ct) =>
        await db.TelegramCampaigns.Where(c => c.Id == campaignId && (c.Status == TelegramCampaignStatus.Queued || c.Status == TelegramCampaignStatus.Sending))
            .ExecuteUpdateAsync(u => u.SetProperty(c => c.Status, TelegramCampaignStatus.Paused).SetProperty(c => c.LastError, reason.Length > 1000 ? reason[..1000] : reason), ct);

    private static async Task NoteAsync(IAppDbContext db, Guid campaignId, string note, CancellationToken ct) =>
        await db.TelegramCampaigns.Where(c => c.Id == campaignId).ExecuteUpdateAsync(u => u.SetProperty(c => c.LastError, note), ct);

    /// <summary>«Отправляется» дольше порога (или все — после запуска сервера) → ошибка без автоповтора: неизвестно, дошло ли.</summary>
    private static async Task<int> FailSendingAsync(IAppDbContext db, DateTimeOffset? olderThan, CancellationToken ct) =>
        await db.TelegramCampaignRecipients
            .Where(r => r.Status == TelegramRecipientStatus.Sending && (olderThan == null || r.LastAttemptAt < olderThan))
            .ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, TelegramRecipientStatus.Failed)
                .SetProperty(r => r.Error, "Неизвестно, дошло ли сообщение: сервер прервал отправку. Автоповтора нет, чтобы не отправить дважды — проверьте чат и при необходимости нажмите «Повторить ошибки»."), ct);
}
