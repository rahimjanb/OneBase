using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.Application.Abstractions;
using OneBase.Application.Telegram;
using OneBase.Application.Telegram.Outbox;
using OneBase.Application.Telegram.Routing;
using OneBase.Domain.Recruitment;
using OneBase.Domain.Telegram;

namespace OneBase.Application.Recruitment.Telegram;

/// <summary>
/// Ход AI-рекрутера: новое сообщение кандидата в диалоге HR (агент — OneBase.AI). FileOutcome — что с вложением: Accepted (принято, обрабатывается),
/// Unsupported, TooBig, Limit; null — вложения нет или оно не документ.
/// </summary>
public sealed record RecruiterTurn(Guid AccountId, long PeerId, Guid ConversationId, Guid CandidateId, int MessageId, string Text, DateTimeOffset At,
    string? FileName = null, Guid? ResumeId = null, string? FileOutcome = null);

/// <summary>AI-рекрутер: отвечает кандидату через очередь исходящих или передаёт диалог человеку. Без регистрации — HR отвечает сам.</summary>
public interface IRecruiterAgent
{
    Task ReplyAsync(RecruiterTurn turn, CancellationToken ct);

    /// <summary>Резюме кандидата разобрано — продолжить разговор с учётом резюме (в диалоге, который ведёт AI).</summary>
    Task DocumentReadyAsync(Guid resumeId, CancellationToken ct);
}

/// <summary>Правила Telegram для подбора: код вакансии, слова поиска работы, ссылка на сообщение.</summary>
public static partial class RecruitmentTelegramRules
{
    /// <summary>Фразы поиска работы и резюме (ru, uz латиница и кириллица) — пост в группе-источнике становится лидом для HR.</summary>
    public static readonly string[] LeadPhrases =
    [
        "ищу работу", "в поиске работы", "ищу вакансию", "рассмотрю предложения", "резюме", "ищу подработку",
        "ish qidiryapman", "ish izlayapman", "ish kerak", "ishga kiraman", "rezyume", "ish qidiraman",
        "иш қидиряпман", "иш излаяпман", "иш керак", "резюме",
    ];

    /// <summary>Коды вакансий в тексте: #V12, V12 (и кириллическая «В»).</summary>
    public static IReadOnlyList<int> VacancyCodes(string? text) =>
        string.IsNullOrEmpty(text) ? [] : CodePattern().Matches(text).Select(m => int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)).Distinct().ToList();

    public static IReadOnlyList<string> LeadMatches(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var lower = text.ToLowerInvariant();
        return LeadPhrases.Where(p => lower.Contains(p, StringComparison.Ordinal)).Distinct().ToList();
    }

    /// <summary>Ссылка на сообщение: публичный чат — t.me/имя/номер, супергруппа или канал без имени — t.me/c/ID/номер; обычная группа — нет.</summary>
    public static string? MessageLink(TelegramChat chat, int messageId) =>
        !string.IsNullOrEmpty(chat.Username) ? $"https://t.me/{chat.Username}/{messageId}"
        : chat.IsChannelPeer ? $"https://t.me/c/{chat.PeerId}/{messageId}"
        : null;

    public static string Excerpt(string text, int max = 500)
    {
        var t = text.Trim();
        return t.Length <= max ? t : t[..max] + "…";
    }

    [GeneratedRegex(@"(?<![\p{L}\d])#?[VvВв]-?(\d{1,6})(?![\p{L}\d])")]
    private static partial Regex CodePattern();
}

/// <summary>
/// Признак HR в первом сообщении нового диалога (для номера, который работает и на продажи): код открытой вакансии (#V12) в личном
/// сообщении или ответ на публикацию вакансии.
/// </summary>
public sealed class RecruitmentRouteClaim(IAppDbContext db) : ITelegramRouteClaim
{
    public string Department => TelegramDepartments.Hr;

    public async ValueTask<bool> ClaimsAsync(TelegramIncoming message, CancellationToken ct)
    {
        if (message.ReplyToMessageId is { } reply
            && await db.VacancyPublications.AnyAsync(p => p.AccountId == message.AccountId && p.PeerId == message.Chat.PeerId && p.MessageId == reply, ct))
        {
            return true;
        }

        var codes = RecruitmentTelegramRules.VacancyCodes(message.Text);
        return message.Chat.Type == TelegramChatType.Private && codes.Count > 0
               && await db.Vacancies.AnyAsync(v => codes.Contains(v.Number) && (v.Status == VacancyStatus.Open || v.Status == VacancyStatus.Paused), ct);
    }
}

/// <summary>
/// Обработчик подразделения HR: личный чат — диалог с кандидатом (кандидат заводится или находится по подтверждённому Telegram ID, сообщение
/// и вложение-резюме сохраняются сразу, код вакансии — отклик на неё), затем ход AI-рекрутера, если в диалоге отвечает AI. Группа —
/// только лиды: пост «ищу работу» в группе-источнике или ответ на публикацию вакансии (незнакомым OneBase сам не пишет).
/// </summary>
public sealed class RecruitmentTelegramHandler(
    IAppDbContext db,
    RecruitmentService recruitment,
    Documents.ResumeDocumentProcessor documents,
    IEnumerable<IRecruiterAgent> agents,
    TimeProvider clock,
    ILogger<RecruitmentTelegramHandler> logger) : ITelegramDepartmentHandler
{
    public string Department => TelegramDepartments.Hr;

    public async Task HandleAsync(TelegramIncoming m, TelegramRoute route, CancellationToken ct)
    {
        if (m.FromBot || m.FromUserId is null && !m.Outgoing)
        {
            return;
        }

        if (m.Chat.Type != TelegramChatType.Private)
        {
            if (!m.Outgoing)
            {
                await GroupAsync(m, ct);
            }

            return;
        }

        if (route.ConversationId is not { } conversationId)
        {
            return; // свои исходящие в ещё не открытом диалоге — нечего сохранять
        }

        await EnsureChatAsync(m, ct);
        if (m.Outgoing)
        {
            await SaveMessageAsync(conversationId, m, TelegramMessageDirection.Out, "phone", null, null, ct);
            return;
        }

        if (!await RegisterAsync(m, ct))
        {
            return; // повторная доставка
        }

        var candidateId = await CandidateAsync(conversationId, m, ct);
        await recruitment.NoteLanguageAsync(candidateId, m.Text, ct); // язык и графика переписки — до ответа
        foreach (var number in RecruitmentTelegramRules.VacancyCodes(m.Text))
        {
            var vacancyId = await db.Vacancies.AsNoTracking().Where(v => v.Number == number).Select(v => (Guid?)v.Id).FirstOrDefaultAsync(ct);
            if (vacancyId is { } vid)
            {
                await recruitment.AttachVacancyAsync(candidateId, vid, "telegram", null, ct);
            }
        }

        // Вложение — только запись «получен» и задание скачать: ответ кандидату не ждёт ни скачивания, ни распознавания.
        Guid? resumeId = null;
        string? fileOutcome = null;
        var attachment = m.File ?? m.Photo; // документ или фото (фото резюме читает AI)
        if (attachment is { } file)
        {
            try
            {
                var received = await documents.ReceiveTelegramAsync(candidateId, new Documents.TelegramDocument(m.AccountId, m.Chat.PeerId, m.MessageId, file.FileName,
                    file.MimeType, file.Size), ct);
                resumeId = received.ResumeId;
                fileOutcome = received.Outcome == Documents.ReceiveOutcome.Ignored ? null : received.Outcome.ToString();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Подбор: вложение из Telegram {Message} не записано", m.MessageId);
            }
        }

        await SaveMessageAsync(conversationId, m, TelegramMessageDirection.In, "candidate", attachment?.FileName, resumeId, ct);
        await recruitment.TouchCandidateAsync(candidateId, ct);
        await db.TelegramIncomingMessages.Where(x => x.AccountId == m.AccountId && x.PeerId == m.Chat.PeerId && x.MessageId == m.MessageId)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.Status, "processed").SetProperty(x => x.Reason, "hr"), ct);

        if (route.AutoReply && agents.FirstOrDefault() is { } agent)
        {
            await agent.ReplyAsync(new RecruiterTurn(m.AccountId, m.Chat.PeerId, conversationId, candidateId, m.MessageId, m.Text, m.Date, attachment?.FileName, resumeId,
                fileOutcome), ct);
        }
    }

    /// <summary>Повторная доставка — по уникальному ключу аккаунт × чат × сообщение (та же таблица, что у агента продаж).</summary>
    private async Task<bool> RegisterAsync(TelegramIncoming m, CancellationToken ct)
    {
        if (await db.TelegramIncomingMessages.AnyAsync(x => x.AccountId == m.AccountId && x.PeerId == m.Chat.PeerId && x.MessageId == m.MessageId, ct))
        {
            return false;
        }

        var row = new TelegramIncomingMessage { AccountId = m.AccountId, PeerId = m.Chat.PeerId, MessageId = m.MessageId, FromUserId = m.FromUserId, SentAt = m.Date };
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

    /// <summary>Чат — в список чатов аккаунта (access_hash нужен для ответа; синхронизация чатов раз в час могла его ещё не взять).</summary>
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

    /// <summary>Кандидат диалога: уже связан — он; иначе приём из Telegram (Telegram ID и username — подтверждены самим сообщением).</summary>
    private async Task<Guid> CandidateAsync(Guid conversationId, TelegramIncoming m, CancellationToken ct)
    {
        var linked = await db.TelegramConversations.AsNoTracking().Where(c => c.Id == conversationId && c.RelatedType == RecruitmentService.CandidateRelation)
            .Select(c => c.RelatedId).FirstOrDefaultAsync(ct);
        if (linked is { } id && await db.Candidates.AnyAsync(c => c.Id == id, ct))
        {
            return id;
        }

        var from = m.FromUserId!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var contacts = new List<IntakeContact> { new(Domain.Recruitment.CandidateContactKind.Telegram, from, true) };
        if (RecruitmentRules.Normalize(Domain.Recruitment.CandidateContactKind.TelegramUsername, m.FromUsername) is not null)
        {
            contacts.Add(new IntakeContact(Domain.Recruitment.CandidateContactKind.TelegramUsername, m.FromUsername!, true));
        }

        var name = !string.IsNullOrWhiteSpace(m.FromName) ? m.FromName! : !string.IsNullOrWhiteSpace(m.FromUsername) ? "@" + m.FromUsername : $"Telegram {from}";
        var result = await recruitment.IntakeAsync(new CandidateIntake(name, null, null, null, contacts, "telegram", from, "Написал в Telegram", null, null), null, ct);
        await db.TelegramConversations.Where(c => c.Id == conversationId).ExecuteUpdateAsync(u => u
            .SetProperty(c => c.RelatedType, RecruitmentService.CandidateRelation)
            .SetProperty(c => c.RelatedId, result.CandidateId)
            .SetProperty(c => c.Kind, "candidate")
            .SetProperty(c => c.TelegramUserId, m.FromUserId), ct);
        return result.CandidateId;
    }

    private async Task SaveMessageAsync(Guid conversationId, TelegramIncoming m, TelegramMessageDirection direction, string author, string? fileName, Guid? resumeId, CancellationToken ct)
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
            ResumeId = resumeId,
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

    /// <summary>Группа: ответ на публикацию вакансии или пост «ищу работу» в группе-источнике — лид для HR (без автоматического сообщения человеку).</summary>
    private async Task GroupAsync(TelegramIncoming m, CancellationToken ct)
    {
        var keywords = new List<string>();
        if (m.ReplyToMessageId is { } reply)
        {
            var vacancy = await db.VacancyPublications.AsNoTracking().Where(p => p.AccountId == m.AccountId && p.PeerId == m.Chat.PeerId && p.MessageId == reply)
                .Join(db.Vacancies, p => p.VacancyId, v => v.Id, (p, v) => (int?)v.Number).FirstOrDefaultAsync(ct);
            if (vacancy is { } number)
            {
                keywords.Add($"ответ на вакансию V{number}");
            }
        }

        if (keywords.Count == 0)
        {
            var source = await db.RecruitmentSources.AsNoTracking().AnyAsync(s => s.AccountId == m.AccountId && s.PeerId == m.Chat.PeerId && s.CollectLeads, ct);
            if (!source)
            {
                return;
            }

            keywords.AddRange(RecruitmentTelegramRules.LeadMatches(m.Text));
            if (keywords.Count == 0)
            {
                return;
            }
        }

        var lead = new TelegramLead
        {
            AccountId = m.AccountId,
            ChatPeerId = m.Chat.PeerId,
            ChatTitle = m.ChatTitle.Length <= 200 ? m.ChatTitle : m.ChatTitle[..200],
            MessageId = m.MessageId,
            FromUserId = m.FromUserId,
            FromUsername = m.FromUsername,
            FromName = m.FromName,
            Excerpt = RecruitmentTelegramRules.Excerpt(m.Text),
            Keywords = string.Join(", ", keywords),
        };
        db.TelegramLeads.Add(lead);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.TelegramLeads.Remove(lead);
        }
    }
}

/// <summary>После отправки из очереди: сообщение — в историю диалога, публикация — «опубликована» со ссылкой (или ошибка).</summary>
public sealed class RecruitmentOutboxObserver(IAppDbContext db, RecruitmentExceptions exceptions, TimeProvider clock) : ITelegramOutboxObserver
{
    public const string PublicationRelation = "publication";

    public async Task OnSentAsync(TelegramOutgoingMessage row, CancellationToken ct)
    {
        var first = row.MessageIds?.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).FirstOrDefault();
        if (row.ConversationId is { } conversationId && await db.TelegramConversations.AnyAsync(c => c.Id == conversationId, ct))
        {
            db.TelegramConversationMessages.Add(new TelegramConversationMessage
            {
                ConversationId = conversationId,
                AccountId = row.AccountId,
                PeerId = row.PeerId,
                MessageId = first,
                Direction = TelegramMessageDirection.Out,
                Text = row.Text,
                Author = row.Author,
                AuthorUserId = row.CreatedById,
                SentAt = row.SentAt ?? clock.GetUtcNow(),
            });
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear(); // то же сообщение уже пришло обратно как исходящее
            }
        }

        if (row.RelatedType == PublicationRelation && row.RelatedId is { } publicationId)
        {
            var chat = await db.TelegramChats.AsNoTracking().FirstOrDefaultAsync(c => c.AccountId == row.AccountId && c.PeerId == row.PeerId, ct);
            await db.VacancyPublications.Where(p => p.Id == publicationId).ExecuteUpdateAsync(u => u
                .SetProperty(p => p.Status, PublicationStatus.Sent)
                .SetProperty(p => p.MessageId, first)
                .SetProperty(p => p.Link, chat != null && first != null ? RecruitmentTelegramRules.MessageLink(chat, first.Value) : null)
                .SetProperty(p => p.UpdatedAt, clock.GetUtcNow()), ct);
        }
    }

    public async Task OnFailedAsync(TelegramOutgoingMessage row, CancellationToken ct)
    {
        if (row.RelatedType == PublicationRelation && row.RelatedId is { } publicationId)
        {
            await db.VacancyPublications.Where(p => p.Id == publicationId).ExecuteUpdateAsync(u => u
                .SetProperty(p => p.Status, PublicationStatus.Failed).SetProperty(p => p.Error, row.Error).SetProperty(p => p.UpdatedAt, clock.GetUtcNow()), ct);
            await exceptions.RaiseAsync("send_failed", $"Публикация вакансии не отправлена: {row.Error}", dedupKey: $"send:{row.Id:N}", ct: ct);
            return;
        }

        if (row.ConversationId is { } conversationId && row.Department == TelegramDepartments.Hr)
        {
            var candidate = await db.TelegramConversations.AsNoTracking().Where(c => c.Id == conversationId && c.RelatedType == RecruitmentService.CandidateRelation)
                .Select(c => c.RelatedId).FirstOrDefaultAsync(ct);
            await exceptions.RaiseAsync(row.Error?.Contains("PEER_FLOOD", StringComparison.Ordinal) == true ? "telegram" : "send_failed",
                $"Сообщение кандидату {(row.Status == TelegramOutboxStatus.Unknown ? "могло не дойти" : "не отправлено")}: {row.Error}",
                candidate, null, conversationId, $"send:{row.Id:N}", ct);
        }
    }
}
