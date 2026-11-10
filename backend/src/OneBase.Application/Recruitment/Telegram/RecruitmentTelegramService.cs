using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Telegram;
using OneBase.Application.Telegram.Outbox;
using OneBase.Application.Telegram.Routing;
using OneBase.Domain.Audit;
using OneBase.Domain.Recruitment;
using OneBase.Domain.Telegram;

namespace OneBase.Application.Recruitment.Telegram;

public sealed record HrTelegramAccount(Guid Id, string Name, string? Username, string Status, bool Online, DateTimeOffset? PausedUntil, string? PauseReason,
    int DailyLimit, int MinIntervalSeconds, int SentToday);

public sealed record HrConversationRow(Guid Id, Guid AccountId, long PeerId, Guid? CandidateId, string Name, string? Username, string Mode, string? LastText,
    DateTimeOffset? LastMessageAt, DateTimeOffset? LastInboundAt);

public sealed record HrMessage(long? Id, string Direction, string? Text, string? FileName, Guid? ResumeId, string Author, string? AuthorName, DateTimeOffset At,
    string Status, string? Error);

public sealed record HrConversation(Guid Id, Guid AccountId, string AccountName, long PeerId, Guid? CandidateId, string Name, string? Username, string Mode,
    IReadOnlyList<HrMessage> Messages);

/// <summary>Сообщение HR: текст и ключ нажатия (повтор не дублирует). Файл в запрос не входит — его кладёт в хранилище сам сервер.</summary>
public sealed record SendInput(string Text, Guid ClientKey);

public sealed record ModeInput(TelegramConversationMode Mode);

public sealed record PublicationChat(Guid Id, string Title, string? Username, string Type, bool CanSend, string? Reason);

public sealed record PublicationInput(Guid VacancyId, Guid AccountId, IReadOnlyList<Guid> ChatIds, string Text, DateTimeOffset? ScheduledAt);

public sealed record PublicationView(Guid Id, Guid VacancyId, string VacancyCode, string ChatTitle, string Status, DateTimeOffset ScheduledAt, string? Link,
    string? Error, string Text, DateTimeOffset CreatedAt);

public sealed record SourceInput(Guid AccountId, Guid ChatId, string Category, bool CollectLeads, string? Note);

public sealed record TelegramSourceView(Guid Id, Guid AccountId, string Title, string Category, bool CollectLeads, string? Note, int Leads);

public sealed record LeadView(Guid Id, string ChatTitle, string? FromName, string? FromUsername, string Excerpt, string Keywords, string Status, Guid? CandidateId,
    DateTimeOffset CreatedAt);

public sealed record LeadConvertInput(Guid? VacancyId);

/// <summary>
/// HR → «Подбор» → Telegram: номера HR и их состояние, диалоги с кандидатами (история, ручной ответ через очередь, AI или человек),
/// публикации вакансий в группы и каналы, источники поиска и лиды. Подключение номеров — только в «Настройки → Telegram».
/// </summary>
public sealed class RecruitmentTelegramService(IAppDbContext db, TelegramOutbox outbox, TelegramConversationService conversations, ITelegramGateway gateway,
    IAuditLogger audit, TimeProvider clock, OneBase.Application.Agents.DialogAgentDelivery delivery, RecruitmentExceptions exceptions)
{
    private static readonly string[] Categories = ["jobs", "professional", "channel", "other"];

    private static void Read(RecruitmentContext ctx)
    {
        if (!ctx.SeesCandidates)
        {
            throw new FieldForbiddenException("Нет доступа к подбору: нужно право «Подбор персонала: просмотр».");
        }
    }

    private static void Manage(RecruitmentContext ctx)
    {
        if (!ctx.CanManage)
        {
            throw new FieldForbiddenException("Писать кандидатам и публиковать вакансии может только HR (право «Подбор персонала: управление»).");
        }
    }

    private Task AuditAsync(RecruitmentContext ctx, string action, string type, Guid id, object? data, CancellationToken ct) =>
        audit.LogAsync(ActorType.User, ctx.UserId.ToString(), action, type, id.ToString(), data, ct);

    private async Task EnsureHrAccountAsync(Guid accountId, CancellationToken ct)
    {
        if (!await db.ServesAsync(accountId, TelegramDepartments.Hr, ct))
        {
            throw new FieldValidationException("Этот номер не работает для HR — в «Настройки → Telegram» выберите у него «Подразделения: HR».");
        }
    }

    // ---------- Номера и диалоги ----------

    public async Task<IReadOnlyList<HrTelegramAccount>> AccountsAsync(RecruitmentContext ctx, CancellationToken ct)
    {
        Read(ctx);
        var accounts = await db.AccountsServing(TelegramDepartments.Hr).AsNoTracking().OrderBy(a => a.CreatedAt).ToListAsync(ct);
        var ids = accounts.Select(a => a.Id).ToList();
        var settings = await db.TelegramAccountDepartments.AsNoTracking().Where(d => ids.Contains(d.AccountId) && d.Department == TelegramDepartments.Hr).ToListAsync(ct);
        var dayStart = clock.GetUtcNow().AddHours(-24);
        var sent = await db.TelegramOutgoingMessages.AsNoTracking().Where(m => ids.Contains(m.AccountId) && m.Department == TelegramDepartments.Hr && m.SentAt >= dayStart)
            .GroupBy(m => m.AccountId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return accounts.Select(a =>
        {
            var s = settings.FirstOrDefault(x => x.AccountId == a.Id);
            return new HrTelegramAccount(a.Id, a.Name, a.Username, a.Status.ToString(), gateway.LiveStatus(a.Id)?.Online == true, s?.PausedUntil, s?.PauseReason,
                s?.DailyLimit ?? 200, s?.MinIntervalSeconds ?? 5, sent.GetValueOrDefault(a.Id));
        }).ToList();
    }

    public async Task<IReadOnlyList<HrConversationRow>> ConversationsAsync(RecruitmentContext ctx, Guid? accountId, string? search, CancellationToken ct)
    {
        Read(ctx);
        var query = db.TelegramConversations.AsNoTracking().Where(c => c.Department == TelegramDepartments.Hr && c.ChatType == TelegramChatType.Private);
        if (accountId is { } a)
        {
            query = query.Where(c => c.AccountId == a);
        }

        var rows = await (from c in query
                          join candidate in db.Candidates.AsNoTracking() on c.RelatedId equals (Guid?)candidate.Id into candidates
                          from candidate in candidates.DefaultIfEmpty()
                          join chat in db.TelegramChats.AsNoTracking() on new { c.AccountId, c.PeerId } equals new { chat.AccountId, chat.PeerId } into chats
                          from chat in chats.DefaultIfEmpty()
                          orderby c.LastMessageAt descending
                          select new { c, Name = candidate != null ? candidate.FullName : chat != null ? chat.Title : null, Username = chat != null ? chat.Username : null })
            .Take(300).ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var lower = search.Trim().ToLowerInvariant();
            rows = rows.Where(r => (r.Name ?? string.Empty).ToLowerInvariant().Contains(lower) || (r.Username ?? string.Empty).Contains(lower.TrimStart('@'))).ToList();
        }

        var ids = rows.Select(r => r.c.Id).ToList();
        var last = await db.TelegramConversationMessages.AsNoTracking().Where(m => ids.Contains(m.ConversationId))
            .GroupBy(m => m.ConversationId).Select(g => g.OrderByDescending(m => m.SentAt).Select(m => new { m.ConversationId, m.Text }).First()).ToListAsync(ct);
        return rows.Select(r => new HrConversationRow(r.c.Id, r.c.AccountId, r.c.PeerId, r.c.RelatedType == RecruitmentService.CandidateRelation ? r.c.RelatedId : null,
            r.Name ?? r.c.PeerId.ToString(System.Globalization.CultureInfo.InvariantCulture), r.Username, r.c.Mode.ToString(),
            last.FirstOrDefault(x => x.ConversationId == r.c.Id)?.Text, r.c.LastMessageAt, r.c.LastInboundAt)).ToList();
    }

    private async Task<TelegramConversation> HrConversationAsync(Guid id, CancellationToken ct) =>
        await db.TelegramConversations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && c.Department == TelegramDepartments.Hr, ct)
        ?? throw new FieldNotFoundException("Диалог не найден.");

    public async Task<HrConversation> ConversationAsync(RecruitmentContext ctx, Guid id, CancellationToken ct)
    {
        Read(ctx);
        var c = await HrConversationAsync(id, ct);
        var account = await db.TelegramAccounts.AsNoTracking().Where(a => a.Id == c.AccountId).Select(a => a.Name).FirstAsync(ct);
        var candidate = c.RelatedType == RecruitmentService.CandidateRelation && c.RelatedId is { } cid
            ? await db.Candidates.AsNoTracking().Where(x => x.Id == cid).Select(x => new { x.Id, x.FullName }).FirstOrDefaultAsync(ct)
            : null;
        var chat = await db.TelegramChats.AsNoTracking().FirstOrDefaultAsync(x => x.AccountId == c.AccountId && x.PeerId == c.PeerId, ct);
        var messages = await db.TelegramConversationMessages.AsNoTracking().Where(m => m.ConversationId == id).OrderByDescending(m => m.SentAt).Take(300).ToListAsync(ct);
        var pending = await db.TelegramOutgoingMessages.AsNoTracking()
            .Where(m => m.ConversationId == id && m.Status != TelegramOutboxStatus.Sent).OrderByDescending(m => m.CreatedAt).Take(50).ToListAsync(ct);
        var names = await db.Users.AsNoTracking().Where(u => messages.Select(m => m.AuthorUserId).Concat(pending.Select(p => p.CreatedById)).Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        var list = messages.Select(m => new HrMessage(m.Id, m.Direction.ToString(), m.Text, m.FileName, m.ResumeId, m.Author,
                m.AuthorUserId is { } u ? names.GetValueOrDefault(u) : null, m.SentAt, "Sent", null))
            .Concat(pending.Select(p => new HrMessage(null, "Out", p.Text, null, null, p.Author, p.CreatedById is { } u ? names.GetValueOrDefault(u) : null,
                p.CreatedAt, p.Status.ToString(), p.Error)))
            .OrderBy(m => m.At).ToList();
        return new HrConversation(c.Id, c.AccountId, account, c.PeerId, candidate?.Id, candidate?.FullName ?? chat?.Title ?? c.PeerId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            chat?.Username, c.Mode.ToString(), list);
    }

    /// <summary>
    /// Ответ сотрудника из OneBase — через очередь исходящих HR (пауза и лимит номера). Сотрудник взял диалог на себя: AI в диалоге
    /// выключается (вернуть — «Вернуть AI»). Повтор того же нажатия (clientKey) второго сообщения не даёт.
    /// </summary>
    public Task<HrConversation> SendAsync(RecruitmentContext ctx, Guid id, SendInput input, CancellationToken ct) => SendAsync(ctx, id, input, null, ct);

    /// <summary>Сообщение с файлом: файл уже сохранён сервером (<see cref="HrDialogService.SendFileAsync"/>), текст — подпись.</summary>
    public async Task<HrConversation> SendAsync(RecruitmentContext ctx, Guid id, SendInput input, OutboxFile? file, CancellationToken ct)
    {
        Manage(ctx);
        var c = await HrConversationAsync(id, ct);
        await delivery.SupersedeAsync(c.Id, "Ответил сотрудник HR", ct); // неотправленный ответ AI больше не нужен
        await outbox.EnqueueAsync(new OutboxRequest(c.AccountId, c.PeerId, c.Id, TelegramDepartments.Hr, "manual", input.Text ?? string.Empty,
            $"manual:{input.ClientKey:N}", "user", ctx.UserId, File: file), ct);
        if (c.Mode == TelegramConversationMode.Ai)
        {
            await conversations.SetModeAsync(c.AccountId, c.PeerId, TelegramConversationMode.Manual, ctx.UserId, ct);
        }

        // «Нужен HR» → «HR ведёт»: человек ответил — то, что ждало его по диалогу, решено.
        await exceptions.ResolveConversationAsync(c.Id, ctx.UserId, "HR ответил кандидату в диалоге", ct);

        return await ConversationAsync(ctx, id, ct);
    }

    public async Task<HrConversation> SetModeAsync(RecruitmentContext ctx, Guid id, TelegramConversationMode mode, CancellationToken ct)
    {
        Manage(ctx);
        var c = await HrConversationAsync(id, ct);
        if (mode == TelegramConversationMode.Manual)
        {
            await delivery.SupersedeAsync(c.Id, "Диалог взял сотрудник HR", ct);
        }
        else
        {
            // Вернули AI — по диалогу больше никто не ждёт человека.
            await exceptions.ResolveConversationAsync(c.Id, ctx.UserId, "Диалог возвращён AI", ct);
        }

        await conversations.SetModeAsync(c.AccountId, c.PeerId, mode, ctx.UserId, ct);
        return await ConversationAsync(ctx, id, ct);
    }

    // ---------- Публикации вакансий ----------

    public async Task<IReadOnlyList<PublicationChat>> PublicationChatsAsync(RecruitmentContext ctx, Guid accountId, CancellationToken ct)
    {
        Read(ctx);
        await EnsureHrAccountAsync(accountId, ct);
        return await db.TelegramChats.AsNoTracking().Where(c => c.AccountId == accountId && c.Type != TelegramChatType.Private).OrderBy(c => c.Title)
            .Select(c => new PublicationChat(c.Id, c.Title, c.Username, c.Type.ToString(), c.CanSend, c.CannotSendReason)).ToListAsync(ct);
    }

    /// <summary>Текст публикации по вакансии: должность, зарплата, город, условия, требования и как откликнуться (код вакансии — для маршрутизации ответа).</summary>
    public async Task<string> TemplateAsync(RecruitmentContext ctx, Guid vacancyId, Guid accountId, CancellationToken ct)
    {
        Read(ctx);
        var v = await db.Vacancies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == vacancyId, ct) ?? throw new FieldNotFoundException("Вакансия не найдена.");
        var username = await db.TelegramAccounts.AsNoTracking().Where(a => a.Id == accountId).Select(a => a.Username).FirstOrDefaultAsync(ct);
        return Template(v, username);
    }

    public static string Template(Vacancy v, string? username)
    {
        var lines = new List<string> { $"Вакансия: {v.Title}" };
        if (v.SalaryFrom is not null || v.SalaryTo is not null)
        {
            var f = v.SalaryFrom is { } from ? from.ToString("#,0", System.Globalization.CultureInfo.GetCultureInfo("ru-RU")) : null;
            var t = v.SalaryTo is { } to ? to.ToString("#,0", System.Globalization.CultureInfo.GetCultureInfo("ru-RU")) : null;
            lines.Add($"Зарплата: {(f != null && t != null ? $"{f} – {t}" : f != null ? $"от {f}" : $"до {t}")} сум");
        }

        if (!string.IsNullOrWhiteSpace(v.City))
        {
            lines.Add($"Город: {v.City}");
        }

        if (!string.IsNullOrWhiteSpace(v.Conditions))
        {
            lines.Add($"\nУсловия:\n{v.Conditions}");
        }

        if (!string.IsNullOrWhiteSpace(v.Requirements))
        {
            lines.Add($"\nТребования:\n{v.Requirements}");
        }

        lines.Add(username is null
            ? $"\nОткликнуться: напишите в личные сообщения код #{v.Code}"
            : $"\nОткликнуться: напишите @{username} код #{v.Code}");
        return string.Join('\n', lines);
    }

    /// <summary>Опубликовать вакансию в выбранные группы и каналы номера HR — сейчас или по расписанию; публикует очередь исходящих.</summary>
    public async Task<IReadOnlyList<PublicationView>> PublishAsync(RecruitmentContext ctx, PublicationInput input, CancellationToken ct)
    {
        Manage(ctx);
        await EnsureHrAccountAsync(input.AccountId, ct);
        var vacancy = await db.Vacancies.AsNoTracking().FirstOrDefaultAsync(v => v.Id == input.VacancyId, ct) ?? throw new FieldNotFoundException("Вакансия не найдена.");
        if (vacancy.Status != VacancyStatus.Open)
        {
            throw new FieldValidationException($"Вакансия {vacancy.Code} не открыта — публиковать можно только открытую.");
        }

        if (input.ChatIds.Count == 0)
        {
            throw new FieldValidationException("Выберите хотя бы одну группу или канал.");
        }

        var chats = await db.TelegramChats.AsNoTracking().Where(c => c.AccountId == input.AccountId && input.ChatIds.Contains(c.Id)).ToListAsync(ct);
        if (chats.FirstOrDefault(c => c.Type == TelegramChatType.Private || !c.CanSend) is { } bad)
        {
            throw new FieldValidationException($"В «{bad.Title}» публиковать нельзя: {bad.CannotSendReason ?? "это личный чат"}.");
        }

        var when = input.ScheduledAt is { } at && at > clock.GetUtcNow() ? at : clock.GetUtcNow();
        foreach (var chat in chats)
        {
            var publication = new VacancyPublication
            {
                VacancyId = vacancy.Id,
                AccountId = input.AccountId,
                ChatId = chat.Id,
                PeerId = chat.PeerId,
                ChatTitle = chat.Title,
                ChatUsername = chat.Username,
                Text = input.Text?.Trim() ?? string.Empty,
                ScheduledAt = when,
                CreatedById = ctx.UserId,
            };
            db.VacancyPublications.Add(publication);
            var row = await outbox.EnqueueAsync(new OutboxRequest(input.AccountId, chat.PeerId, null, TelegramDepartments.Hr, "publication", publication.Text,
                $"publication:{publication.Id:N}", "user", ctx.UserId, null, RecruitmentOutboxObserver.PublicationRelation, publication.Id, when), ct);
            publication.OutgoingId = row.Id;
            await db.SaveChangesAsync(ct);
        }

        await AuditAsync(ctx, "recruitment.vacancy.published", nameof(Vacancy), vacancy.Id, new { vacancy.Code, chats = chats.Count, scheduled = when }, ct);
        return await PublicationsAsync(ctx, vacancy.Id, ct);
    }

    public async Task<IReadOnlyList<PublicationView>> PublicationsAsync(RecruitmentContext ctx, Guid? vacancyId, CancellationToken ct)
    {
        Read(ctx);
        var query = db.VacancyPublications.AsNoTracking();
        if (vacancyId is { } v)
        {
            query = query.Where(p => p.VacancyId == v);
        }

        return await query.OrderByDescending(p => p.CreatedAt).Take(300)
            .Join(db.Vacancies, p => p.VacancyId, v => v.Id, (p, v) => new PublicationView(p.Id, p.VacancyId, "V" + v.Number, p.ChatTitle, p.Status.ToString(),
                p.ScheduledAt, p.Link, p.Error, p.Text, p.CreatedAt)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PublicationView>> CancelPublicationAsync(RecruitmentContext ctx, Guid id, CancellationToken ct)
    {
        Manage(ctx);
        var publication = await db.VacancyPublications.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new FieldNotFoundException("Публикация не найдена.");
        var cancelled = publication.OutgoingId is { } outgoing
            ? await db.TelegramOutgoingMessages.Where(m => m.Id == outgoing && m.Status == TelegramOutboxStatus.Pending)
                .ExecuteUpdateAsync(u => u.SetProperty(m => m.Status, TelegramOutboxStatus.Cancelled), ct)
            : 0;
        if (cancelled == 0)
        {
            throw new FieldConflictException("Публикация уже отправляется или отправлена — отменить нельзя.");
        }

        publication.Status = PublicationStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        return await PublicationsAsync(ctx, publication.VacancyId, ct);
    }

    // ---------- Источники и лиды ----------

    public async Task<IReadOnlyList<TelegramSourceView>> SourcesAsync(RecruitmentContext ctx, CancellationToken ct)
    {
        Read(ctx);
        var sources = await db.RecruitmentSources.AsNoTracking().OrderBy(s => s.Title).ToListAsync(ct);
        var leads = await db.TelegramLeads.AsNoTracking().GroupBy(l => new { l.AccountId, l.ChatPeerId }).Select(g => new { g.Key.AccountId, g.Key.ChatPeerId, Count = g.Count() })
            .ToListAsync(ct);
        return sources.Select(s => new TelegramSourceView(s.Id, s.AccountId, s.Title, s.Category, s.CollectLeads, s.Note,
            leads.FirstOrDefault(l => l.AccountId == s.AccountId && l.ChatPeerId == s.PeerId)?.Count ?? 0)).ToList();
    }

    public async Task<IReadOnlyList<TelegramSourceView>> AddSourceAsync(RecruitmentContext ctx, SourceInput input, CancellationToken ct)
    {
        Manage(ctx);
        await EnsureHrAccountAsync(input.AccountId, ct);
        if (!Categories.Contains(input.Category))
        {
            throw new FieldValidationException("Категория: вакансии и поиск работы, профессиональное сообщество, профильный канал или другое.");
        }

        var chat = await db.TelegramChats.AsNoTracking().FirstOrDefaultAsync(c => c.Id == input.ChatId && c.AccountId == input.AccountId && c.Type != TelegramChatType.Private, ct)
                   ?? throw new FieldValidationException("Выберите группу или канал, где состоит номер HR.");
        if (await db.RecruitmentSources.AnyAsync(s => s.AccountId == input.AccountId && s.PeerId == chat.PeerId, ct))
        {
            throw new FieldConflictException("Этот чат уже в источниках.");
        }

        db.RecruitmentSources.Add(new RecruitmentSource
        {
            AccountId = input.AccountId,
            ChatId = chat.Id,
            PeerId = chat.PeerId,
            Title = chat.Title,
            Category = input.Category,
            CollectLeads = input.CollectLeads,
            Note = input.Note?.Trim() is { Length: > 0 } n ? (n.Length <= 500 ? n : n[..500]) : null,
            CreatedById = ctx.UserId,
        });
        await db.SaveChangesAsync(ct);
        return await SourcesAsync(ctx, ct);
    }

    public async Task<IReadOnlyList<TelegramSourceView>> RemoveSourceAsync(RecruitmentContext ctx, Guid id, CancellationToken ct)
    {
        Manage(ctx);
        await db.RecruitmentSources.Where(s => s.Id == id).ExecuteDeleteAsync(ct);
        return await SourcesAsync(ctx, ct);
    }

    public async Task<IReadOnlyList<LeadView>> LeadsAsync(RecruitmentContext ctx, LeadStatus? status, CancellationToken ct)
    {
        Read(ctx);
        var query = db.TelegramLeads.AsNoTracking();
        if (status is { } s)
        {
            query = query.Where(l => l.Status == s);
        }

        return await query.OrderByDescending(l => l.CreatedAt).Take(300)
            .Select(l => new LeadView(l.Id, l.ChatTitle, l.FromName, l.FromUsername, l.Excerpt, l.Keywords, l.Status.ToString(), l.CandidateId, l.CreatedAt)).ToListAsync(ct);
    }

    /// <summary>HR решил связаться: кандидат заводится (Telegram ID и username — от Telegram). Написать ему — решение и действие человека.</summary>
    public async Task<Guid> ConvertLeadAsync(RecruitmentContext ctx, Guid id, LeadConvertInput input, RecruitmentService recruitment, CancellationToken ct)
    {
        Manage(ctx);
        var lead = await db.TelegramLeads.FirstOrDefaultAsync(l => l.Id == id, ct) ?? throw new FieldNotFoundException("Лид не найден.");
        if (lead.Status != LeadStatus.New || lead.FromUserId is null)
        {
            throw new FieldConflictException("По этому лиду уже приняли решение или у поста нет автора.");
        }

        var contacts = new List<IntakeContact> { new(CandidateContactKind.Telegram, lead.FromUserId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), true) };
        if (RecruitmentRules.Normalize(CandidateContactKind.TelegramUsername, lead.FromUsername) is not null)
        {
            contacts.Add(new IntakeContact(CandidateContactKind.TelegramUsername, lead.FromUsername!, true));
        }

        var result = await recruitment.IntakeAsync(new CandidateIntake(lead.FromName ?? (lead.FromUsername is { } u ? "@" + u : "Кандидат из Telegram"), null, null,
            $"Пост в «{lead.ChatTitle}»: {lead.Excerpt}", contacts, "telegram", lead.FromUserId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            $"Лид из группы «{lead.ChatTitle}»", input.VacancyId, null), ctx.UserId, ct);
        lead.Status = LeadStatus.Converted;
        lead.CandidateId = result.CandidateId;
        lead.DecidedById = ctx.UserId;
        await db.SaveChangesAsync(ct);
        return result.CandidateId;
    }

    public async Task<IReadOnlyList<LeadView>> IgnoreLeadAsync(RecruitmentContext ctx, Guid id, CancellationToken ct)
    {
        Manage(ctx);
        await db.TelegramLeads.Where(l => l.Id == id && l.Status == LeadStatus.New)
            .ExecuteUpdateAsync(u => u.SetProperty(l => l.Status, LeadStatus.Ignored).SetProperty(l => l.DecidedById, ctx.UserId), ct);
        return await LeadsAsync(ctx, LeadStatus.New, ct);
    }
}
