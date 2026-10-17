using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using OneBase.AI.Reports;
using OneBase.Application.Abstractions;
using OneBase.Application.Telegram;
using OneBase.Domain.Audit;
using OneBase.Domain.Telegram;

namespace OneBase.AI.Telegram;

public sealed record CampaignInput(Guid AccountId, string Name, string? Description, string? Text, bool Formatted, DateTimeOffset? ScheduledAt, int DelaySeconds);

public sealed record CampaignCounts(int Total, int Pending, int Sending, int Sent, int Failed, int Cancelled);

public sealed record CampaignListItem(
    Guid Id,
    string Name,
    Guid AccountId,
    string AccountName,
    string Status,
    DateTimeOffset? ScheduledAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    string? CreatedBy,
    CampaignCounts Counts,
    string? TextPreview = null,
    int Files = 0);

public sealed record CampaignRecipientView(
    Guid Id,
    Guid ChatId,
    long PeerId,
    string Title,
    string? Username,
    string Type,
    string Status,
    int Attempts,
    DateTimeOffset? NextAttemptAt,
    DateTimeOffset? SentAt,
    string? Error);

public sealed record CampaignAttachmentView(Guid Id, string Kind, string FileName, string ContentType, long SizeBytes, Guid? ReportId);

public sealed record CampaignView(
    Guid Id,
    Guid AccountId,
    string AccountName,
    string AccountStatus,
    string Name,
    string? Description,
    string Status,
    string? Text,
    bool Formatted,
    DateTimeOffset? ScheduledAt,
    string TimeZone,
    int DelaySeconds,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    string? LastError,
    string? CreatedBy,
    CampaignCounts Counts,
    IReadOnlyList<CampaignAttachmentView> Attachments,
    IReadOnlyList<CampaignRecipientView> Recipients);

public sealed record CampaignAccount(Guid Id, string Name, string? Username, string Status);

/// <summary>Чат аккаунта для выбора получателей; CanSend=false — Reason объясняет, почему писать нельзя.</summary>
public sealed record CampaignChat(Guid Id, long PeerId, string Title, string? Username, string Type, bool CanSend, bool Added, string? Reason = null);

public sealed record CampaignFile(string FileName, string ContentType, byte[] Content);

/// <summary>Итог добавления получателей: сколько добавлено и почему остальные — нет (чат недоступен, уже в списке).</summary>
public sealed record RecipientsResult(int Added, IReadOnlyList<SkippedRecipient> Skipped);

public sealed record SkippedRecipient(string Title, string Reason);

public sealed record CampaignPreview(string? Html, int Parts, bool CaptionWithFile, IReadOnlyList<string> Warnings, int Recipients, int Unavailable);

public sealed record UserReport(Guid Id, string Title, DateTimeOffset CreatedAt);

/// <summary>
/// Рассылки Telegram: черновик (аккаунт, текст, файлы, получатели) → проверка и тест → запуск сразу или по расписанию → очередь
/// (TelegramCampaignRunner) с паузой, продолжением и отменой. Получатели — только чаты аккаунта, куда он может писать; один чат —
/// один раз. Файлы и отчёты фиксируются при добавлении (отчёт — файлом Excel на момент добавления) и в ходе рассылки не меняются.
/// </summary>
public sealed class TelegramCampaignService(
    IAppDbContext db,
    ITelegramGateway telegram,
    IFileStorage storage,
    ReportService reports,
    TelegramMaterialService materials,
    IAuditLogger audit,
    TimeProvider clock)
{
    public const int MaxAttachments = 10;
    public const int MaxRecipients = 5000;

    private static readonly TelegramCampaignStatus[] Editable = [TelegramCampaignStatus.Draft, TelegramCampaignStatus.Scheduled];
    private static readonly TelegramCampaignStatus[] Final =
        [TelegramCampaignStatus.Completed, TelegramCampaignStatus.CompletedWithErrors, TelegramCampaignStatus.Cancelled, TelegramCampaignStatus.Failed];

    // ---------- Справочники мастера ----------

    public async Task<IReadOnlyList<CampaignAccount>> AccountsAsync(CancellationToken ct = default) =>
        await db.TelegramAccounts.AsNoTracking().OrderBy(a => a.CreatedAt)
            .Select(a => new CampaignAccount(a.Id, a.Name, a.Username, a.Status.ToString())).ToListAsync(ct);

    /// <summary>Чаты аккаунта для выбора получателей: поиск, тип; Added — уже в этой рассылке.</summary>
    public async Task<IReadOnlyList<CampaignChat>> ChatsAsync(Guid campaignId, string? search, string? type, CancellationToken ct = default)
    {
        var campaign = await CampaignAsync(campaignId, ct);
        var added = await db.TelegramCampaignRecipients.AsNoTracking().Where(r => r.CampaignId == campaignId).Select(r => r.PeerId).ToListAsync(ct);
        var query = db.TelegramChats.AsNoTracking().Where(c => c.AccountId == campaign.AccountId);
        if (Enum.TryParse<TelegramChatType>(type, true, out var t))
        {
            query = query.Where(c => c.Type == t);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = "%" + search.Trim().TrimStart('@').ToLowerInvariant().Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(c => EF.Functions.Like(c.Title.ToLower(), s) || (c.Username != null && EF.Functions.Like(c.Username.ToLower(), s)));
        }

        var rows = await query.OrderByDescending(c => c.CanSend).ThenBy(c => c.Title).Take(2000).ToListAsync(ct);
        var set = added.ToHashSet();
        return rows.Select(c => new CampaignChat(c.Id, c.PeerId, c.Title, c.Username, c.Type.ToString(), c.CanSend, set.Contains(c.PeerId),
            c.CanSend ? null : c.CannotSendReason ?? "аккаунт не может писать в этот чат")).ToList();
    }

    /// <summary>Обновить чаты аккаунта из Telegram (для выбора получателей).</summary>
    public async Task<int> SyncChatsAsync(Guid campaignId, CancellationToken ct = default)
    {
        var campaign = await CampaignAsync(campaignId, ct);
        return await TelegramChatSync.SyncAsync(db, telegram, campaign.AccountId, clock.GetUtcNow(), ct);
    }

    /// <summary>Отчёты пользователя (сформированные консультантом) — можно приложить к рассылке файлом.</summary>
    public async Task<IReadOnlyList<UserReport>> ReportsAsync(Guid userId, CancellationToken ct = default) =>
        await db.AiReports.AsNoTracking().Where(r => r.UserId == userId).OrderByDescending(r => r.CreatedAt).Take(50)
            .Select(r => new UserReport(r.Id, r.Title, r.CreatedAt)).ToListAsync(ct);

    // ---------- Рассылки ----------

    public async Task<IReadOnlyList<CampaignListItem>> ListAsync(string? status, CancellationToken ct = default)
    {
        var query = db.TelegramCampaigns.AsNoTracking();
        if (Enum.TryParse<TelegramCampaignStatus>(status, true, out var s))
        {
            query = query.Where(c => c.Status == s);
        }

        var rows = await query.OrderByDescending(c => c.CreatedAt).Take(300)
            .Select(c => new
            {
                c.Id, c.Name, c.AccountId, Account = c.Account!.Name, c.Status, c.ScheduledAt, c.CreatedAt, c.StartedAt, c.FinishedAt, c.CreatedById,
                Preview = c.Text == null ? null : c.Text.Substring(0, Math.Min(c.Text.Length, 160)),
                Files = c.Attachments.Count,
            })
            .ToListAsync(ct);
        var ids = rows.Select(r => r.Id).ToList();
        var counts = await CountsAsync(ids, ct);
        var users = await UserNamesAsync(rows.Select(r => r.CreatedById), ct);
        return rows.Select(r => new CampaignListItem(r.Id, r.Name, r.AccountId, r.Account, r.Status.ToString(), r.ScheduledAt, r.CreatedAt, r.StartedAt,
            r.FinishedAt, users.GetValueOrDefault(r.CreatedById), counts.GetValueOrDefault(r.Id) ?? new CampaignCounts(0, 0, 0, 0, 0, 0), r.Preview, r.Files)).ToList();
    }

    /// <summary>Файл рассылки (как он был отправлен) — скачать.</summary>
    public async Task<CampaignFile> AttachmentAsync(Guid id, Guid attachmentId, CancellationToken ct = default)
    {
        var a = await db.TelegramCampaignAttachments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == attachmentId && x.CampaignId == id, ct)
                ?? throw new KeyNotFoundException();
        using var buffer = new MemoryStream();
        await storage.DownloadAsync(a.ObjectKey, buffer, ct);
        return new CampaignFile(a.FileName, a.ContentType, buffer.ToArray());
    }

    /// <summary>
    /// Повторить как новую: черновик с тем же текстом, файлами (копии — у каждой рассылки свои) и получателями, которым ещё можно писать.
    /// Отправленную рассылку не изменить — Telegram не правит у получателей уже доставленное.
    /// </summary>
    public async Task<CampaignView> DuplicateAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var source = await db.TelegramCampaigns.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new KeyNotFoundException();
        var copy = new TelegramCampaign
        {
            AccountId = source.AccountId,
            Name = (source.Name.Length > 190 ? source.Name[..190] : source.Name) + " (копия)",
            Description = source.Description,
            Text = source.Text,
            Formatted = source.Formatted,
            DelaySeconds = source.DelaySeconds,
            TimeZone = source.TimeZone,
            CreatedById = userId,
        };
        db.TelegramCampaigns.Add(copy);
        await db.SaveChangesAsync(ct);

        var attachments = await db.TelegramCampaignAttachments.AsNoTracking().Where(a => a.CampaignId == id).OrderBy(a => a.Position).ToListAsync(ct);
        foreach (var a in attachments)
        {
            using var buffer = new MemoryStream();
            await storage.DownloadAsync(a.ObjectKey, buffer, ct);
            await StoreAsync(copy, a.Kind, a.FileName, a.ContentType, buffer.ToArray(), a.ReportId, userId, ct);
        }

        var chatIds = await db.TelegramCampaignRecipients.AsNoTracking().Where(r => r.CampaignId == id).Select(r => r.ChatId).ToListAsync(ct);
        await AddRecipientsAsync(copy.Id, chatIds, ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.campaign.duplicated", nameof(TelegramCampaign), copy.Id.ToString(), new { from = id }, ct);
        return await GetAsync(copy.Id, ct);
    }

    public async Task<CampaignView> GetAsync(Guid id, CancellationToken ct = default)
    {
        var c = await db.TelegramCampaigns.AsNoTracking().Include(x => x.Account).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException();
        var attachments = await db.TelegramCampaignAttachments.AsNoTracking().Where(a => a.CampaignId == id).OrderBy(a => a.Position)
            .Select(a => new CampaignAttachmentView(a.Id, a.Kind, a.FileName, a.ContentType, a.SizeBytes, a.ReportId)).ToListAsync(ct);
        var recipients = await db.TelegramCampaignRecipients.AsNoTracking().Where(r => r.CampaignId == id).OrderBy(r => r.CreatedAt).Take(MaxRecipients)
            .Select(r => new CampaignRecipientView(r.Id, r.ChatId, r.PeerId, r.Title, r.Username, r.Type.ToString(), r.Status.ToString(), r.Attempts,
                r.NextAttemptAt, r.SentAt, r.Error)).ToListAsync(ct);
        var users = await UserNamesAsync([c.CreatedById], ct);
        return new CampaignView(c.Id, c.AccountId, c.Account?.Name ?? "—", c.Account?.Status.ToString() ?? "—", c.Name, c.Description, c.Status.ToString(), c.Text,
            c.Formatted, c.ScheduledAt, c.TimeZone, c.DelaySeconds, c.CreatedAt, c.StartedAt, c.FinishedAt, c.LastError, users.GetValueOrDefault(c.CreatedById),
            Count(recipients.Select(r => r.Status)), attachments, recipients);
    }

    public async Task<CampaignView> CreateAsync(CampaignInput input, Guid userId, CancellationToken ct = default)
    {
        if (!await db.TelegramAccounts.AnyAsync(a => a.Id == input.AccountId, ct))
        {
            throw new TelegramException("Выберите аккаунт Telegram.", TelegramErrorKind.Config);
        }

        var campaign = new TelegramCampaign { AccountId = input.AccountId, Name = "—", CreatedById = userId };
        Apply(campaign, input);
        db.TelegramCampaigns.Add(campaign);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.campaign.created", nameof(TelegramCampaign), campaign.Id.ToString(),
            new { campaign.Name, campaign.AccountId }, ct);
        return await GetAsync(campaign.Id, ct);
    }

    /// <summary>Изменить черновик (или запланированную, пока не началась). Смена аккаунта очищает получателей — у другого аккаунта другие чаты.</summary>
    public async Task<CampaignView> UpdateAsync(Guid id, CampaignInput input, Guid userId, CancellationToken ct = default)
    {
        var campaign = await CampaignAsync(id, ct);
        EnsureEditable(campaign);
        if (campaign.AccountId != input.AccountId)
        {
            if (!await db.TelegramAccounts.AnyAsync(a => a.Id == input.AccountId, ct))
            {
                throw new TelegramException("Выберите аккаунт Telegram.", TelegramErrorKind.Config);
            }

            await db.TelegramCampaignRecipients.Where(r => r.CampaignId == id).ExecuteDeleteAsync(ct);
            campaign.AccountId = input.AccountId;
        }

        Apply(campaign, input);
        if (campaign.Status == TelegramCampaignStatus.Scheduled && campaign.ScheduledAt is null)
        {
            campaign.Status = TelegramCampaignStatus.Draft;
        }

        campaign.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.campaign.updated", nameof(TelegramCampaign), id.ToString(), new { campaign.Name }, ct);
        return await GetAsync(id, ct);
    }

    private void Apply(TelegramCampaign campaign, CampaignInput input)
    {
        var name = input.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            throw new TelegramException("Укажите название рассылки.", TelegramErrorKind.Config);
        }

        var text = input.Text?.Trim();
        if (text is { Length: > TelegramRules.MaxMessageLength })
        {
            throw new TelegramException($"Текст длиннее {TelegramRules.MaxMessageLength} символов — столько Telegram не примет одним сообщением.", TelegramErrorKind.Config);
        }

        if (input.ScheduledAt is { } at && at < clock.GetUtcNow().AddMinutes(-1))
        {
            throw new TelegramException("Время отправки уже прошло.", TelegramErrorKind.Config);
        }

        campaign.Name = name.Length > 200 ? name[..200] : name;
        campaign.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim()[..Math.Min(2000, input.Description.Trim().Length)];
        campaign.Text = string.IsNullOrEmpty(text) ? null : text;
        campaign.Formatted = input.Formatted;
        campaign.ScheduledAt = input.ScheduledAt;
        campaign.DelaySeconds = Math.Clamp(input.DelaySeconds, 2, 600);
    }

    public async Task DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var campaign = await CampaignAsync(id, ct);
        if (campaign.Status is TelegramCampaignStatus.Queued or TelegramCampaignStatus.Sending)
        {
            throw new TelegramException("Рассылка идёт — сначала отмените её.", TelegramErrorKind.Config);
        }

        if (await db.TelegramCampaignRecipients.AnyAsync(r => r.CampaignId == id && r.Status == TelegramRecipientStatus.Sent, ct))
        {
            throw new TelegramException("Рассылка уже отправлялась — её журнал сохраняется. Удалить можно только неотправленную.", TelegramErrorKind.Config);
        }

        var keys = await db.TelegramCampaignAttachments.AsNoTracking().Where(a => a.CampaignId == id).Select(a => a.ObjectKey).ToListAsync(ct);
        db.TelegramCampaigns.Remove(campaign);
        await db.SaveChangesAsync(ct);
        foreach (var key in keys)
        {
            await storage.DeleteAsync(key, ct);
        }

        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.campaign.deleted", nameof(TelegramCampaign), id.ToString(), new { campaign.Name }, ct);
    }

    // ---------- Получатели ----------

    /// <summary>Добавить чаты: только чаты аккаунта рассылки, куда он может писать; повторно один чат не добавляется.</summary>
    public async Task<RecipientsResult> AddRecipientsAsync(Guid id, IReadOnlyList<Guid> chatIds, CancellationToken ct = default)
    {
        var campaign = await CampaignAsync(id, ct);
        EnsureEditable(campaign);
        var ids = chatIds.Distinct().ToList();
        var chats = await db.TelegramChats.AsNoTracking().Where(c => ids.Contains(c.Id)).ToListAsync(ct);
        var existing = (await db.TelegramCampaignRecipients.AsNoTracking().Where(r => r.CampaignId == id).Select(r => r.PeerId).ToListAsync(ct)).ToHashSet();
        var skipped = new List<SkippedRecipient>();
        var added = 0;
        foreach (var chatId in ids)
        {
            var chat = chats.FirstOrDefault(c => c.Id == chatId);
            if (chat is null || chat.AccountId != campaign.AccountId)
            {
                skipped.Add(new SkippedRecipient(chat?.Title ?? chatId.ToString(), "чат не принадлежит аккаунту рассылки"));
                continue;
            }

            if (!chat.CanSend)
            {
                skipped.Add(new SkippedRecipient(chat.Title, "аккаунт не может писать в этот чат"));
                continue;
            }

            if (!existing.Add(chat.PeerId))
            {
                skipped.Add(new SkippedRecipient(chat.Title, "уже в списке"));
                continue;
            }

            if (existing.Count > MaxRecipients)
            {
                skipped.Add(new SkippedRecipient(chat.Title, $"больше {MaxRecipients} получателей в одной рассылке"));
                continue;
            }

            db.TelegramCampaignRecipients.Add(new TelegramCampaignRecipient
            {
                CampaignId = id,
                ChatId = chat.Id,
                PeerId = chat.PeerId,
                Title = chat.Title,
                Username = chat.Username,
                Type = chat.Type,
            });
            added++;
        }

        await db.SaveChangesAsync(ct);
        return new RecipientsResult(added, skipped);
    }

    public async Task RemoveRecipientAsync(Guid id, Guid recipientId, CancellationToken ct = default)
    {
        var campaign = await CampaignAsync(id, ct);
        EnsureEditable(campaign);
        await db.TelegramCampaignRecipients.Where(r => r.Id == recipientId && r.CampaignId == id).ExecuteDeleteAsync(ct);
    }

    public async Task ClearRecipientsAsync(Guid id, CancellationToken ct = default)
    {
        var campaign = await CampaignAsync(id, ct);
        EnsureEditable(campaign);
        await db.TelegramCampaignRecipients.Where(r => r.CampaignId == id).ExecuteDeleteAsync(ct);
    }

    // ---------- Файлы ----------

    public async Task<CampaignAttachmentView> AddFileAsync(Guid id, string fileName, string? contentType, Stream content, long size, Guid userId,
        CancellationToken ct = default)
    {
        var campaign = await CampaignAsync(id, ct);
        EnsureEditable(campaign);
        if (size <= 0 || size > TelegramRules.MaxAttachmentBytes)
        {
            throw new TelegramException($"Файл пустой или больше {TelegramRules.MaxAttachmentBytes / 1024 / 1024} МБ.", TelegramErrorKind.Config);
        }

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        return await StoreAsync(campaign, "file", SafeName(fileName), string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            buffer.ToArray(), null, userId, ct);
    }

    /// <summary>Отчёт консультанта — файлом Excel на момент добавления: дальше рассылка отправляет именно этот снимок.</summary>
    public async Task<CampaignAttachmentView> AddReportAsync(Guid id, Guid reportId, Guid userId, CancellationToken ct = default)
    {
        var campaign = await CampaignAsync(id, ct);
        EnsureEditable(campaign);
        var workbook = await reports.WorkbookAsync(userId, reportId, ct)
                       ?? throw new TelegramException("Отчёт не найден среди ваших отчётов.", TelegramErrorKind.Config);
        return await StoreAsync(campaign, "report", workbook.FileName, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            workbook.Content, reportId, userId, ct);
    }

    private async Task<CampaignAttachmentView> StoreAsync(TelegramCampaign campaign, string kind, string fileName, string contentType, byte[] bytes, Guid? reportId,
        Guid userId, CancellationToken ct)
    {
        var count = await db.TelegramCampaignAttachments.CountAsync(a => a.CampaignId == campaign.Id, ct);
        if (count >= MaxAttachments)
        {
            throw new TelegramException($"Не больше {MaxAttachments} файлов в рассылке.", TelegramErrorKind.Config);
        }

        var attachment = new TelegramCampaignAttachment
        {
            CampaignId = campaign.Id,
            Kind = kind,
            FileName = fileName,
            ContentType = contentType.Length > 200 ? "application/octet-stream" : contentType,
            SizeBytes = bytes.LongLength,
            ObjectKey = string.Empty,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
            ReportId = reportId,
            Position = count,
        };
        attachment.ObjectKey = $"telegram/campaigns/{campaign.Id:N}/{attachment.Id:N}";
        using (var stream = new MemoryStream(bytes, writable: false))
        {
            await storage.PutAsync(attachment.ObjectKey, stream, bytes.LongLength, attachment.ContentType, ct);
        }

        db.TelegramCampaignAttachments.Add(attachment);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.campaign.attachment_added", nameof(TelegramCampaign), campaign.Id.ToString(),
            new { kind, fileName, attachment.SizeBytes, attachment.Sha256 }, ct);
        return new CampaignAttachmentView(attachment.Id, kind, fileName, attachment.ContentType, attachment.SizeBytes, reportId);
    }

    public async Task RemoveAttachmentAsync(Guid id, Guid attachmentId, CancellationToken ct = default)
    {
        var campaign = await CampaignAsync(id, ct);
        EnsureEditable(campaign);
        var attachment = await db.TelegramCampaignAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.CampaignId == id, ct) ?? throw new KeyNotFoundException();
        db.TelegramCampaignAttachments.Remove(attachment);
        await db.SaveChangesAsync(ct);
        await storage.DeleteAsync(attachment.ObjectKey, ct);
    }

    // ---------- Проверка, тест, запуск ----------

    public async Task<CampaignPreview> PreviewAsync(Guid id, CancellationToken ct = default)
    {
        var campaign = await CampaignAsync(id, ct);
        var attachments = await db.TelegramCampaignAttachments.AsNoTracking().Where(a => a.CampaignId == id).ToListAsync(ct);
        var recipients = await db.TelegramCampaignRecipients.AsNoTracking().Where(r => r.CampaignId == id).Select(r => r.ChatId).ToListAsync(ct);
        var unavailable = await db.TelegramChats.AsNoTracking().CountAsync(c => recipients.Contains(c.Id) && !c.CanSend, ct);
        var warnings = new List<string>();
        var html = campaign.Text is null ? null : campaign.Formatted ? TelegramRules.MarkdownToTelegramHtml(campaign.Text) : TelegramRules.Escape(campaign.Text);
        var caption = attachments.Count == 1 && campaign.Text is { Length: > 0 and <= TelegramRules.MaxCaptionLength };
        if (attachments.Count == 1 && campaign.Text is { Length: > TelegramRules.MaxCaptionLength })
        {
            warnings.Add($"Текст длиннее {TelegramRules.MaxCaptionLength} символов — уйдёт отдельным сообщением перед файлом.");
        }

        if (campaign.Text is null && attachments.Count == 0)
        {
            warnings.Add("Нет ни текста, ни файлов.");
        }

        if (recipients.Count == 0)
        {
            warnings.Add("Не выбраны получатели.");
        }

        if (unavailable > 0)
        {
            warnings.Add($"В {unavailable} чатов аккаунт больше не может писать — им отправка не удастся.");
        }

        var minutes = Math.Ceiling(recipients.Count * campaign.DelaySeconds / 60.0);
        if (recipients.Count > 1)
        {
            warnings.Add($"Отправка займёт не меньше ~{minutes} мин (пауза {campaign.DelaySeconds} с между чатами).");
        }

        var parts = (campaign.Text is null ? 0 : caption ? 0 : 1) + attachments.Count;
        return new CampaignPreview(html, parts, caption, warnings, recipients.Count, unavailable);
    }

    /// <summary>Тестовая отправка в один чат аккаунта (например, себе или коллеге) — статусы рассылки не меняются.</summary>
    public async Task<string> TestAsync(Guid id, Guid chatId, Guid userId, CancellationToken ct = default)
    {
        var campaign = await CampaignAsync(id, ct);
        var chat = await db.TelegramChats.AsNoTracking().FirstOrDefaultAsync(c => c.Id == chatId && c.AccountId == campaign.AccountId, ct)
                   ?? throw new TelegramException("Выберите чат этого аккаунта.", TelegramErrorKind.Config);
        if (!chat.CanSend)
        {
            throw new TelegramException("Аккаунт не может писать в этот чат.", TelegramErrorKind.Permanent);
        }

        var attachments = await LoadAttachmentsAsync(id, ct);
        if (campaign.Text is null && attachments.Count == 0)
        {
            throw new TelegramException("Нечего отправлять: нет ни текста, ни файлов.", TelegramErrorKind.Config);
        }

        await telegram.SendAsync(campaign.AccountId, PeerOf(chat), MessageOf(campaign, attachments), ct);
        await RecordMaterialsAsync(materials, campaign, chat.PeerId, attachments, await ExtractAllAsync(materials, attachments, ct), userId, ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.campaign.test_sent", nameof(TelegramCampaign), id.ToString(), new { chat.Title }, ct);
        return chat.Title;
    }

    /// <summary>Запустить: сразу (в очередь) или по расписанию. Проверяется всё, что можно проверить до отправки.</summary>
    public async Task<CampaignView> LaunchAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var campaign = await CampaignAsync(id, ct);
        if (campaign.Status != TelegramCampaignStatus.Draft && campaign.Status != TelegramCampaignStatus.Scheduled)
        {
            throw new TelegramException("Запустить можно черновик.", TelegramErrorKind.Config);
        }

        var account = await db.TelegramAccounts.AsNoTracking().FirstAsync(a => a.Id == campaign.AccountId, ct);
        if (account.Status != TelegramAccountStatus.Connected)
        {
            throw new TelegramException("Аккаунт Telegram не подключён.", TelegramErrorKind.Config);
        }

        var hasFiles = await db.TelegramCampaignAttachments.AnyAsync(a => a.CampaignId == id, ct);
        if (campaign.Text is null && !hasFiles)
        {
            throw new TelegramException("Нечего отправлять: нет ни текста, ни файлов.", TelegramErrorKind.Config);
        }

        if (!await db.TelegramCampaignRecipients.AnyAsync(r => r.CampaignId == id, ct))
        {
            throw new TelegramException("Не выбраны получатели.", TelegramErrorKind.Config);
        }

        var now = clock.GetUtcNow();
        campaign.Status = campaign.ScheduledAt is { } at && at > now ? TelegramCampaignStatus.Scheduled : TelegramCampaignStatus.Queued;
        campaign.LastError = null;
        campaign.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.campaign.launched", nameof(TelegramCampaign), id.ToString(),
            new { Status = campaign.Status.ToString(), campaign.ScheduledAt }, ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Пауза: текущая отправка (если идёт) завершается, следующие ждут «Продолжить».</summary>
    public Task<CampaignView> PauseAsync(Guid id, Guid userId, CancellationToken ct = default) =>
        ChangeAsync(id, userId, "paused", [TelegramCampaignStatus.Queued, TelegramCampaignStatus.Sending, TelegramCampaignStatus.Scheduled],
            c => c.Status = TelegramCampaignStatus.Paused, ct);

    public Task<CampaignView> ResumeAsync(Guid id, Guid userId, CancellationToken ct = default) =>
        ChangeAsync(id, userId, "resumed", [TelegramCampaignStatus.Paused], c =>
        {
            c.Status = c.ScheduledAt is { } at && at > clock.GetUtcNow() && c.StartedAt is null ? TelegramCampaignStatus.Scheduled : TelegramCampaignStatus.Queued;
            c.LastError = null;
        }, ct);

    /// <summary>Отменить: ещё не отправленные — «Отменено»; уже отправленные остаются (Telegram не даёт «отозвать» у всех).</summary>
    public async Task<CampaignView> CancelAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var view = await ChangeAsync(id, userId, "cancelled",
            [TelegramCampaignStatus.Draft, TelegramCampaignStatus.Scheduled, TelegramCampaignStatus.Queued, TelegramCampaignStatus.Sending, TelegramCampaignStatus.Paused],
            c =>
            {
                c.Status = TelegramCampaignStatus.Cancelled;
                c.FinishedAt = clock.GetUtcNow();
            }, ct);
        await db.TelegramCampaignRecipients.Where(r => r.CampaignId == id && r.Status == TelegramRecipientStatus.Pending)
            .ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, TelegramRecipientStatus.Cancelled), ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Повторить ошибки: только получатели с ошибкой — снова в очередь; отправленным повторно не уходит.</summary>
    public async Task<CampaignView> RetryFailedAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var campaign = await CampaignAsync(id, ct);
        if (campaign.Status is not (TelegramCampaignStatus.CompletedWithErrors or TelegramCampaignStatus.Failed or TelegramCampaignStatus.Paused))
        {
            throw new TelegramException("Повторить можно завершённую с ошибками или приостановленную рассылку.", TelegramErrorKind.Config);
        }

        var count = await db.TelegramCampaignRecipients.Where(r => r.CampaignId == id && r.Status == TelegramRecipientStatus.Failed)
            .ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, TelegramRecipientStatus.Pending).SetProperty(r => r.Attempts, 0)
                .SetProperty(r => r.NextAttemptAt, (DateTimeOffset?)null), ct);
        if (count == 0)
        {
            throw new TelegramException("Ошибочных получателей нет.", TelegramErrorKind.Config);
        }

        campaign.Status = TelegramCampaignStatus.Queued;
        campaign.FinishedAt = null;
        campaign.LastError = null;
        campaign.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.campaign.retry", nameof(TelegramCampaign), id.ToString(), new { count }, ct);
        return await GetAsync(id, ct);
    }

    private async Task<CampaignView> ChangeAsync(Guid id, Guid userId, string action, TelegramCampaignStatus[] from, Action<TelegramCampaign> change,
        CancellationToken ct)
    {
        var campaign = await CampaignAsync(id, ct);
        if (!from.Contains(campaign.Status))
        {
            throw new TelegramException($"Сейчас это недоступно (статус рассылки — {campaign.Status}).", TelegramErrorKind.Config);
        }

        change(campaign);
        campaign.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.campaign." + action, nameof(TelegramCampaign), id.ToString(), null, ct);
        return await GetAsync(id, ct);
    }

    // ---------- Общее с очередью ----------

    internal sealed record LoadedAttachment(TelegramCampaignAttachment Row, byte[] Content);

    internal async Task<IReadOnlyList<LoadedAttachment>> LoadAttachmentsAsync(Guid campaignId, CancellationToken ct)
    {
        var rows = await db.TelegramCampaignAttachments.AsNoTracking().Where(a => a.CampaignId == campaignId).OrderBy(a => a.Position).ToListAsync(ct);
        var result = new List<LoadedAttachment>();
        foreach (var row in rows)
        {
            using var buffer = new MemoryStream();
            await storage.DownloadAsync(row.ObjectKey, buffer, ct);
            var bytes = buffer.ToArray();
            if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != row.Sha256)
            {
                throw new TelegramException($"Файл «{row.FileName}» в хранилище изменился или повреждён — рассылка его не отправит.", TelegramErrorKind.Permanent);
            }

            result.Add(new LoadedAttachment(row, bytes));
        }

        return result;
    }

    internal static TelegramOutgoing MessageOf(TelegramCampaign campaign, IReadOnlyList<LoadedAttachment> attachments)
    {
        var text = campaign.Text is null ? null : campaign.Formatted ? TelegramRules.MarkdownToTelegramHtml(campaign.Text) : campaign.Text;
        return new TelegramOutgoing(text, campaign.Formatted, attachments.Select(a => new TelegramFile(a.Row.FileName, a.Row.ContentType, a.Content)).ToList());
    }

    internal static TelegramPeerRef PeerOf(TelegramChat chat) => new(chat.PeerId, chat.Type, chat.IsChannelPeer, chat.AccessHash);

    internal static async Task<IReadOnlyDictionary<Guid, string?>> ExtractAllAsync(TelegramMaterialService materials, IReadOnlyList<LoadedAttachment> attachments,
        CancellationToken ct)
    {
        var texts = new Dictionary<Guid, string?>();
        foreach (var a in attachments)
        {
            texts[a.Row.Id] = await materials.ExtractAsync(a.Row.FileName, a.Content, ct);
        }

        return texts;
    }

    /// <summary>Отправленное в чат — материалы этого чата (для ответов агента в режиме «Только отправленные данные»).</summary>
    internal static async Task RecordMaterialsAsync(TelegramMaterialService materials, TelegramCampaign campaign, long peerId, IReadOnlyList<LoadedAttachment> attachments,
        IReadOnlyDictionary<Guid, string?> texts, Guid? userId, CancellationToken ct)
    {
        if (campaign.Text is { Length: > 0 } text)
        {
            await materials.RecordTextAsync(campaign.AccountId, peerId, "message", campaign.Name, text, campaign.Id, userId, ct);
        }

        foreach (var a in attachments)
        {
            await materials.RecordAsync(campaign.AccountId, peerId, a.Row.Kind, a.Row.FileName, a.Row.FileName, a.Row.ObjectKey, a.Row.SizeBytes,
                texts.GetValueOrDefault(a.Row.Id), campaign.Id, userId, ct);
        }
    }

    internal static CampaignCounts Count(IEnumerable<string> statuses)
    {
        var list = statuses.ToList();
        return new CampaignCounts(list.Count, list.Count(s => s == nameof(TelegramRecipientStatus.Pending)), list.Count(s => s == nameof(TelegramRecipientStatus.Sending)),
            list.Count(s => s == nameof(TelegramRecipientStatus.Sent)), list.Count(s => s == nameof(TelegramRecipientStatus.Failed)),
            list.Count(s => s == nameof(TelegramRecipientStatus.Cancelled)));
    }

    private async Task<Dictionary<Guid, CampaignCounts>> CountsAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var rows = await db.TelegramCampaignRecipients.AsNoTracking().Where(r => ids.Contains(r.CampaignId))
            .GroupBy(r => new { r.CampaignId, r.Status }).Select(g => new { g.Key.CampaignId, g.Key.Status, Count = g.Count() }).ToListAsync(ct);
        return rows.GroupBy(r => r.CampaignId).ToDictionary(g => g.Key, g =>
        {
            int Of(TelegramRecipientStatus s) => g.Where(x => x.Status == s).Sum(x => x.Count);
            return new CampaignCounts(g.Sum(x => x.Count), Of(TelegramRecipientStatus.Pending), Of(TelegramRecipientStatus.Sending), Of(TelegramRecipientStatus.Sent),
                Of(TelegramRecipientStatus.Failed), Of(TelegramRecipientStatus.Cancelled));
        });
    }

    private async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return await db.Users.AsNoTracking().Where(u => list.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    private async Task<TelegramCampaign> CampaignAsync(Guid id, CancellationToken ct) =>
        await db.TelegramCampaigns.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new KeyNotFoundException();

    private static void EnsureEditable(TelegramCampaign campaign)
    {
        if (!Editable.Contains(campaign.Status))
        {
            throw new TelegramException(Final.Contains(campaign.Status)
                ? "Рассылка завершена — изменить её нельзя. Создайте новую."
                : "Рассылка уже идёт — сначала поставьте на паузу или отмените.", TelegramErrorKind.Config);
        }
    }

    private static string SafeName(string fileName)
    {
        var name = Path.GetFileName(fileName ?? "file").Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return name.Length == 0 ? "file" : name.Length > 200 ? name[^200..] : name;
    }
}
