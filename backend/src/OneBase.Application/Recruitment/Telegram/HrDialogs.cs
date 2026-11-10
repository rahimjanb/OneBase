using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Security;
using OneBase.Application.Telegram;
using OneBase.Application.Telegram.Outbox;
using OneBase.Application.Telegram.Routing;
using OneBase.Application.Work;
using OneBase.Domain.Audit;
using OneBase.Domain.Recruitment;
using OneBase.Domain.Telegram;
using OneBase.Domain.Work;

namespace OneBase.Application.Recruitment.Telegram;

/// <summary>Номер HR: аккаунт, название, последние цифры номера (полный номер хранится зашифрованным), подключён ли, в сети ли.</summary>
/// <summary>Номер HR для плашки: подключён ли, запущен ли клиент, и до какого времени Telegram остановил отправку (PEER_FLOOD), если остановил.</summary>
public sealed record HrNumberView(Guid AccountId, string Name, string? PhoneHint, string? Username, bool Connected, bool Online, DateTimeOffset? PausedUntil);

public sealed record HrDialogCounts(int Total, int Ai, int NeedHr, int Unread);

/// <summary>Сводка страницы: номер HR (null — не подключён к HR), может ли сотрудник настраивать Telegram, счётчики фильтров.</summary>
public sealed record HrDialogSummary(HrNumberView? Number, bool CanConfigure, HrDialogCounts Counts);

/// <summary>
/// Строка списка: Mode — ai (ведёт AI), hr (ведёт человек), need_hr (AI передал диалог человеку и он ещё не ответил); последнее сообщение и кто
/// его написал (candidate, ai, hr); непрочитанные входящие — у этого сотрудника.
/// </summary>
public sealed record HrDialogRow(Guid Id, Guid? CandidateId, string Name, string? Username, string? VacancyTitle, string Mode, string? LastText, string? LastAuthor,
    DateTimeOffset? LastAt, int Unread, string? HandoffReason);

public sealed record HrDialogAttachment(string FileName, long? Size, Guid? ResumeId, bool Image, bool CanPreview);

/// <summary>
/// Элемент ленты: Kind — candidate, ai, auto (автосообщение OneBase), hr, system. У сообщений — статус (sent, pending, failed, unknown), у
/// неотправленного HR — OutgoingId для «Повторить»; у системного — Event: handoff_to_hr, hr_took_over, returned_to_ai, interview_scheduled.
/// </summary>
public sealed record HrDialogItem(string Kind, string? Text, DateTimeOffset At, string? Author, bool Self, string Status, string? Error, Guid? OutgoingId,
    HrDialogAttachment? Attachment, string? Event);

public sealed record HrDialogScoreItem(string Label, int Value);

public sealed record HrDialogScore(int Total, IReadOnlyList<HrDialogScoreItem> Criteria);

public sealed record HrDialogField(string Label, string Value);

/// <summary>Карточка кандидата в диалоге: контакты, вакансия и этап, собеседование, оценка AI, анкета (ответы на вопросы отбора, собрал AI).</summary>
public sealed record HrDialogCandidate(Guid Id, string Name, string? City, string? Username, string? Phone, Guid? ApplicationId, Guid? VacancyId, string? VacancyTitle,
    string? VacancyPlace, string? Source, string? Stage, DateTimeOffset? InterviewAt, string? InterviewTimeZone, HrDialogScore? Score, IReadOnlyList<HrDialogField> Form,
    string? Language);

public sealed record HrDialogTemplate(string Key, string Label, string Text);

public sealed record HrDialog(Guid Id, Guid AccountId, string Name, string? Username, string Mode, string? HandoffReason, DateTimeOffset? LastInboundAt,
    IReadOnlyList<HrDialogItem> Items, HrDialogCandidate? Candidate, IReadOnlyList<HrDialogTemplate> Templates, bool CanManage);

/// <summary>
/// HR → «Подбор» → «Диалоги Telegram»: номер HR, список диалогов (кто ведёт — AI, человек или «нужен HR»; непрочитанные — свои у каждого
/// сотрудника), лента (сообщения и системные события: AI передал HR, HR взял диалог, диалог возвращён AI, собеседование назначено), карточка
/// кандидата, шаблоны ответов на языке кандидата, файл от HR, «Повторить» неотправленное, черновик AI (не отправляется), просьба администратору
/// подключить номер. Отправка, режим и права — те же, что у <see cref="RecruitmentTelegramService"/>.
/// </summary>
public sealed class HrDialogService(
    IAppDbContext db,
    RecruitmentTelegramService telegram,
    InterviewService interviews,
    ITelegramGateway gateway,
    IFileStorage storage,
    TelegramOutbox outbox,
    UserNotifier notifier,
    IEnumerable<IRecruiterAgent> agents,
    IAuditLogger audit,
    TimeProvider clock)
{
    public const long MaxFileBytes = 20L * 1024 * 1024;

    private static readonly HashSet<string> FileExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".jpg", ".jpeg", ".png" };

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
            throw new FieldForbiddenException("Писать кандидатам может только HR (право «Подбор персонала: управление»).");
        }
    }

    // ---------- Сводка и список ----------

    public async Task<HrDialogSummary> SummaryAsync(RecruitmentContext ctx, bool canConfigure, CancellationToken ct)
    {
        Read(ctx);
        var account = await db.AccountsServing(TelegramDepartments.Hr).AsNoTracking().OrderByDescending(a => a.Status == TelegramAccountStatus.Connected)
            .ThenBy(a => a.CreatedAt).FirstOrDefaultAsync(ct);
        var pausedUntil = account is null ? null
            : await db.TelegramAccountDepartments.AsNoTracking().Where(d => d.AccountId == account.Id && d.Department == TelegramDepartments.Hr)
                .Select(d => d.PausedUntil).FirstOrDefaultAsync(ct);
        var number = account is null ? null
            : new HrNumberView(account.Id, account.Name, account.PhoneHint, account.Username, account.Status == TelegramAccountStatus.Connected,
                gateway.LiveStatus(account.Id)?.Online == true, pausedUntil > clock.GetUtcNow() ? pausedUntil : null);
        var rows = await ListAsync(ctx, null, ct);
        return new HrDialogSummary(number, canConfigure, new HrDialogCounts(rows.Count, rows.Count(r => r.Mode == "ai"), rows.Count(r => r.Mode == "need_hr"),
            rows.Count(r => r.Unread > 0)));
    }

    /// <summary>Диалоги HR: сначала «нужен HR», затем по последнему сообщению; поиск — по имени и @username.</summary>
    public async Task<IReadOnlyList<HrDialogRow>> ListAsync(RecruitmentContext ctx, string? search, CancellationToken ct)
    {
        Read(ctx);
        var rows = await (from c in db.TelegramConversations.AsNoTracking().Where(c => c.Department == TelegramDepartments.Hr && c.ChatType == TelegramChatType.Private)
                          join candidate in db.Candidates.AsNoTracking() on c.RelatedId equals (Guid?)candidate.Id into candidates
                          from candidate in candidates.DefaultIfEmpty()
                          join chat in db.TelegramChats.AsNoTracking() on new { c.AccountId, c.PeerId } equals new { chat.AccountId, chat.PeerId } into chats
                          from chat in chats.DefaultIfEmpty()
                          orderby c.LastMessageAt descending
                          select new
                          {
                              c.Id, c.Mode, c.LastMessageAt, CandidateId = candidate != null ? (Guid?)candidate.Id : null,
                              Name = candidate != null ? candidate.FullName : chat != null ? chat.Title : null, Username = chat != null ? chat.Username : null,
                          })
            .Take(300).ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var lower = search.Trim().ToLowerInvariant().TrimStart('@');
            rows = rows.Where(r => (r.Name ?? string.Empty).ToLowerInvariant().Contains(lower) || (r.Username ?? string.Empty).ToLowerInvariant().Contains(lower)).ToList();
        }

        var ids = rows.Select(r => r.Id).ToList();
        var last = await db.TelegramConversationMessages.AsNoTracking().Where(m => ids.Contains(m.ConversationId))
            .GroupBy(m => m.ConversationId)
            .Select(g => g.OrderByDescending(m => m.SentAt).Select(m => new { m.ConversationId, m.Text, m.FileName, m.Direction, m.Author, m.SentAt }).First())
            .ToListAsync(ct);
        var unread = await UnreadAsync(ids, ctx.UserId, ct);
        var handoffs = await OpenHandoffsAsync(ids, ct);
        var candidateIds = rows.Where(r => r.CandidateId != null).Select(r => r.CandidateId!.Value).ToList();
        var vacancies = await db.CandidateApplications.AsNoTracking().Where(a => candidateIds.Contains(a.CandidateId))
            .OrderBy(a => a.ClosedAt != null).ThenByDescending(a => a.StageChangedAt).Select(a => new { a.CandidateId, a.Vacancy!.Title }).ToListAsync(ct);
        var list = rows.Select(r =>
        {
            var m = last.FirstOrDefault(x => x.ConversationId == r.Id);
            var mode = Mode(r.Mode, handoffs.ContainsKey(r.Id));
            return new HrDialogRow(r.Id, r.CandidateId, r.Name ?? "Кандидат", r.Username, vacancies.FirstOrDefault(v => v.CandidateId == r.CandidateId)?.Title, mode,
                m is null ? null : m.Text ?? (m.FileName is { } f ? $"📎 {f}" : null), m is null ? null : AuthorKind(m.Direction, m.Author), m?.SentAt ?? r.LastMessageAt,
                unread.GetValueOrDefault(r.Id), handoffs.GetValueOrDefault(r.Id));
        });
        return list.OrderByDescending(r => r.Mode == "need_hr").ThenByDescending(r => r.LastAt).ToList();
    }

    private static string Mode(TelegramConversationMode mode, bool waiting) =>
        mode == TelegramConversationMode.Ai ? "ai" : waiting ? "need_hr" : "hr";

    private static string AuthorKind(TelegramMessageDirection direction, string author) =>
        direction == TelegramMessageDirection.In ? "candidate" : author switch { "ai" => "ai", "system" => "auto", _ => "hr" };

    /// <summary>Непрочитанные входящие у сотрудника: после его отметки «прочитано» (нет отметки — все входящие).</summary>
    private async Task<Dictionary<Guid, int>> UnreadAsync(IReadOnlyCollection<Guid> ids, Guid userId, CancellationToken ct) =>
        await (from m in db.TelegramConversationMessages.AsNoTracking().Where(m => ids.Contains(m.ConversationId) && m.Direction == TelegramMessageDirection.In)
               join r in db.DialogReads.AsNoTracking().Where(r => r.UserId == userId) on m.ConversationId equals r.ConversationId into reads
               from r in reads.DefaultIfEmpty()
               where r == null || m.SentAt > r.ReadAt
               group m by m.ConversationId into g
               select new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);

    /// <summary>Диалоги, где AI передал человеку и тот ещё не ответил (открытое исключение диалога), — с причиной последнего.</summary>
    private async Task<Dictionary<Guid, string>> OpenHandoffsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var open = await db.RecruitmentExceptions.AsNoTracking()
            .Where(e => e.Status == RecruitmentExceptionStatus.Open && e.ConversationId != null && ids.Contains(e.ConversationId.Value))
            .OrderByDescending(e => e.CreatedAt).Select(e => new { Conversation = e.ConversationId!.Value, e.Kind }).ToListAsync(ct);
        return open.GroupBy(e => e.Conversation).ToDictionary(g => g.Key, g => Reason(g.First().Kind));
    }

    /// <summary>Причина передачи человеку — для пилюли «AI передал диалог HR · …».</summary>
    public static string Reason(string kind) => kind switch
    {
        "salary" => "вопрос о зарплате",
        "conditions" => "спорные условия",
        "human_requested" => "кандидат просит человека",
        "unclear" => "AI не понял вопрос",
        "review" => "нужно решение HR",
        "blocked" => "ответ AI остановлен ограничением",
        "schedule" => "не нашлось времени для собеседования",
        "technical" => "техническая ошибка AI",
        "send_failed" => "сообщение не отправлено",
        "telegram" => "проблема Telegram",
        "duplicate" => "возможный дубль кандидата",
        _ => RecruitmentExceptions.Kinds.TryGetValue(kind, out var label) ? label.ToLowerInvariant() : kind,
    };

    // ---------- Диалог ----------

    /// <summary>Диалог: лента, карточка кандидата, шаблоны. Открытие отмечает его прочитанным у этого сотрудника.</summary>
    public async Task<HrDialog> GetAsync(RecruitmentContext ctx, Guid id, CancellationToken ct)
    {
        Read(ctx);
        var c = await db.TelegramConversations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.Department == TelegramDepartments.Hr, ct)
                ?? throw new FieldNotFoundException("Диалог не найден.");
        var chat = await db.TelegramChats.AsNoTracking().FirstOrDefaultAsync(x => x.AccountId == c.AccountId && x.PeerId == c.PeerId, ct);
        var candidateId = c.RelatedType == RecruitmentService.CandidateRelation ? c.RelatedId : null;
        var candidate = candidateId is { } cid ? await db.Candidates.AsNoTracking().Include(x => x.Contacts).FirstOrDefaultAsync(x => x.Id == cid, ct) : null;

        var messages = await db.TelegramConversationMessages.AsNoTracking().Where(m => m.ConversationId == id).OrderByDescending(m => m.SentAt).Take(300).ToListAsync(ct);
        var pending = await db.TelegramOutgoingMessages.AsNoTracking()
            .Where(m => m.ConversationId == id && m.Status != TelegramOutboxStatus.Sent && m.Status != TelegramOutboxStatus.Cancelled)
            .OrderByDescending(m => m.CreatedAt).Take(50).ToListAsync(ct);
        var exceptions = await db.RecruitmentExceptions.AsNoTracking().Where(e => e.ConversationId == id).OrderBy(e => e.CreatedAt).ToListAsync(ct);
        var idText = id.ToString();
        var modes = await db.AuditLogs.AsNoTracking().Where(a => a.Action == "telegram.conversation.mode" && a.EntityId == idText).OrderBy(a => a.Timestamp)
            .Select(a => new { a.Timestamp, a.ActorId, a.Data }).Take(100).ToListAsync(ct);
        var interviewRows = candidateId is { } ic
            ? await db.Interviews.AsNoTracking().Where(i => i.CandidateId == ic && i.ConfirmedAt != null && i.StartsAt != null).ToListAsync(ct)
            : [];
        var resumeIds = messages.Where(m => m.ResumeId != null).Select(m => m.ResumeId!.Value).ToList();
        var resumes = await db.CandidateResumes.AsNoTracking().Where(r => resumeIds.Contains(r.Id))
            .Select(r => new { r.Id, r.SizeBytes, r.ContentType, r.Sha256 }).ToDictionaryAsync(r => r.Id, ct);
        var userIds = messages.Where(m => m.AuthorUserId != null).Select(m => m.AuthorUserId!.Value).Concat(pending.Where(p => p.CreatedById != null).Select(p => p.CreatedById!.Value))
            .Concat(modes.Select(m => Guid.TryParse(m.ActorId, out var g) ? g : Guid.Empty)).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        string? Name(Guid? user) => user is { } u && names.TryGetValue(u, out var n) ? n : null;

        var items = new List<HrDialogItem>();
        foreach (var m in messages)
        {
            HrDialogAttachment? attachment = null;
            if (m.FileName is { } file)
            {
                var r = m.ResumeId is { } rid && resumes.TryGetValue(rid, out var x) ? x : null;
                var type = r?.ContentType ?? string.Empty;
                attachment = new HrDialogAttachment(file, r?.SizeBytes, m.ResumeId, type.StartsWith("image/", StringComparison.Ordinal) || IsImage(file),
                    r is { Sha256: not null } && type is "application/pdf" or "image/png" or "image/jpeg" or "image/webp");
            }

            var kind = AuthorKind(m.Direction, m.Author);
            items.Add(new HrDialogItem(kind, m.Text, m.SentAt, kind == "hr" ? Name(m.AuthorUserId) ?? (m.Author == "phone" ? "с телефона" : null) : null,
                m.AuthorUserId == ctx.UserId, "sent", null, null, attachment, null));
        }

        foreach (var p in pending)
        {
            var kind = p.Author switch { "ai" => "ai", "system" => "auto", _ => "hr" };
            items.Add(new HrDialogItem(kind, string.IsNullOrEmpty(p.Text) ? null : p.Text, p.CreatedAt, kind == "hr" ? Name(p.CreatedById) : null, p.CreatedById == ctx.UserId,
                p.Status switch { TelegramOutboxStatus.Failed => "failed", TelegramOutboxStatus.Unknown => "unknown", _ => "pending" }, p.Error,
                p.Status is TelegramOutboxStatus.Failed or TelegramOutboxStatus.Unknown && kind == "hr" ? p.Id : null,
                p.FileName is { } f ? new HrDialogAttachment(f, p.FileSize, null, IsImage(f), false) : null, null));
        }

        foreach (var e in exceptions.Where(e => e.Kind is not ("duplicate" or "hh" or "telegram")))
        {
            items.Add(new HrDialogItem("system", $"AI передал диалог HR · {Reason(e.Kind)}", e.CreatedAt, null, false, "sent", null, null, null, "handoff_to_hr"));
        }

        foreach (var m in modes)
        {
            var to = ModeTo(m.Data);
            var who = Guid.TryParse(m.ActorId, out var actor) ? Name(actor) : null;
            items.Add(to == "Ai"
                ? new HrDialogItem("system", "Диалог возвращён AI" + (who is null ? string.Empty : $" · {who}"), m.Timestamp, who, false, "sent", null, null, null, "returned_to_ai")
                : new HrDialogItem("system", "HR взял диалог" + (who is null ? string.Empty : $" · {who}"), m.Timestamp, who, false, "sent", null, null, null, "hr_took_over"));
        }

        foreach (var i in interviewRows)
        {
            var at = InterviewService.Local(i.StartsAt!.Value, i.TimeZone).ToString("ddd, d MMMM, HH:mm", System.Globalization.CultureInfo.GetCultureInfo("ru-RU"));
            items.Add(new HrDialogItem("system", $"Собеседование назначено · {at}", i.ConfirmedAt!.Value, null, false, "sent", null, null, null, "interview_scheduled"));
        }

        await MarkReadAsync(id, ctx.UserId, ct);
        var waiting = exceptions.LastOrDefault(e => e.Status == RecruitmentExceptionStatus.Open);
        return new HrDialog(c.Id, c.AccountId, candidate?.FullName ?? chat?.Title ?? "Кандидат", chat?.Username, Mode(c.Mode, waiting is not null),
            waiting is null ? null : Reason(waiting.Kind), c.LastInboundAt, items.OrderBy(x => x.At).ToList(), candidate is null ? null : await CandidateAsync(candidate, ct),
            candidate is null ? [] : await TemplatesAsync(candidate, ct), ctx.CanManage);
    }

    /// <summary>Куда переключили режим (данные аудита — JSON: {"to": "Ai"}).</summary>
    private static string ModeTo(string? data)
    {
        try
        {
            using var json = JsonDocument.Parse(data ?? "{}");
            return json.RootElement.TryGetProperty("to", out var to) && to.GetString() == "Ai" ? "Ai" : "Manual";
        }
        catch (JsonException)
        {
            return "Manual";
        }
    }

    private static bool IsImage(string file) => Path.GetExtension(file).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".webp";

    private async Task MarkReadAsync(Guid conversationId, Guid userId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        if (await db.DialogReads.Where(r => r.ConversationId == conversationId && r.UserId == userId).ExecuteUpdateAsync(u => u.SetProperty(r => r.ReadAt, now), ct) > 0)
        {
            return;
        }

        var row = new DialogRead { ConversationId = conversationId, UserId = userId, ReadAt = now };
        db.DialogReads.Add(row);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.DialogReads.Remove(row); // открыли в двух вкладках одновременно — отметка уже есть
        }
    }

    private async Task<HrDialogCandidate> CandidateAsync(Candidate candidate, CancellationToken ct)
    {
        var application = await db.CandidateApplications.AsNoTracking().Include(a => a.Vacancy).ThenInclude(v => v!.Department)
            .Where(a => a.CandidateId == candidate.Id).OrderBy(a => a.ClosedAt != null).ThenByDescending(a => a.StageChangedAt).FirstOrDefaultAsync(ct);
        Interview? interview = null;
        HrDialogScore? score = null;
        if (application is not null)
        {
            interview = await db.Interviews.AsNoTracking()
                .Where(i => i.ApplicationId == application.Id && (i.Status == InterviewStatus.Confirmed || i.Status == InterviewStatus.Rescheduled || i.Status == InterviewStatus.Awaiting))
                .OrderByDescending(i => i.UpdatedAt ?? i.CreatedAt).FirstOrDefaultAsync(ct);
            var row = await db.CandidateScores.AsNoTracking().Where(s => s.ApplicationId == application.Id).OrderByDescending(s => s.CreatedAt).FirstOrDefaultAsync(ct);
            if (row is not null)
            {
                var criteria = new List<HrDialogScoreItem>();
                try
                {
                    using var json = JsonDocument.Parse(row.CriteriaJson);
                    foreach (var c in json.RootElement.EnumerateArray())
                    {
                        if (c.TryGetProperty("name", out var n) && c.TryGetProperty("score", out var s) && s.ValueKind == JsonValueKind.Number && s.TryGetDouble(out var value))
                        {
                            criteria.Add(new HrDialogScoreItem(n.GetString() ?? "—", (int)Math.Clamp(Math.Round(value * 10), 0, 100))); // 0–10 → 0–100
                        }
                    }
                }
                catch (JsonException)
                {
                }

                score = new HrDialogScore(row.Overall, criteria);
            }
        }

        var vacancy = application?.Vacancy;
        var place = vacancy is null ? null : string.Join(" · ", new[] { vacancy.Department?.Name, vacancy.City }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var form = RecruitmentService.ScreeningAnswers(application?.ScreeningAnswers).Select(a => new HrDialogField(a.Key, a.Value)).ToList();
        var lang = CandidateLanguages.Of(candidate.Language, candidate.PreferredScript);
        return new HrDialogCandidate(candidate.Id, candidate.FullName, candidate.City,
            candidate.Contacts.Where(x => x.Kind == CandidateContactKind.TelegramUsername).Select(x => x.Value).FirstOrDefault(),
            candidate.Contacts.Where(x => x.Kind == CandidateContactKind.Phone).OrderByDescending(x => x.Verified).Select(x => x.Value).FirstOrDefault(),
            application?.Id, application?.VacancyId, vacancy?.Title, string.IsNullOrEmpty(place) ? null : place, application?.Source, application?.Stage.ToString(),
            interview?.StartsAt, interview?.TimeZone, score, form, lang is { } l ? CandidateLanguages.Code(l) : null);
    }

    /// <summary>Шаблоны ответа на языке и графике кандидата: адрес офиса (если указан у вакансии), приглашение на собеседование, вежливый отказ.</summary>
    private async Task<IReadOnlyList<HrDialogTemplate>> TemplatesAsync(Candidate candidate, CancellationToken ct)
    {
        var lang = CandidateLanguages.Of(candidate.Language, candidate.PreferredScript) ?? CandidateLang.Ru;
        var application = await db.CandidateApplications.AsNoTracking().Include(a => a.Vacancy).Where(a => a.CandidateId == candidate.Id && a.ClosedAt == null)
            .OrderByDescending(a => a.StageChangedAt).FirstOrDefaultAsync(ct);
        var list = new List<HrDialogTemplate>();
        if (application?.Vacancy is { } vacancy && (await interviews.ConfigAsync(vacancy.Id, ct)) is { Address: { Length: > 0 } address } cfg)
        {
            list.Add(new HrDialogTemplate("address", "Адрес офиса", RecruiterTexts.OfficeAddress(lang, address, cfg.Instructions)));
        }

        var title = application?.Vacancy?.Title;
        list.Add(new HrDialogTemplate("invite", "Пригласить на собеседование", RecruiterTexts.InviteByHr(lang, title)));
        list.Add(new HrDialogTemplate("reject", "Отказ — вежливо", RecruiterTexts.PoliteRejection(lang, title)));
        return list;
    }

    // ---------- Действия HR ----------

    /// <summary>
    /// Файл кандидату от HR (PDF, Word, Excel, TXT, фото до 20 МБ): сохраняется в хранилище OneBase (удаляется вместе с перепиской кандидата), уходит
    /// через очередь исходящих с подписью; диалог — на человека, как при любом ответе HR.
    /// </summary>
    public async Task<HrConversation> SendFileAsync(RecruitmentContext ctx, Guid id, RecruitmentUpload upload, string? caption, Guid clientKey, CancellationToken ct)
    {
        Manage(ctx);
        var c = await db.TelegramConversations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.Department == TelegramDepartments.Hr, ct)
                ?? throw new FieldNotFoundException("Диалог не найден.");
        var name = Documents.ResumeDocumentProcessor.SafeFileName(upload.FileName);
        var extension = Path.GetExtension(name);
        if (!FileExtensions.Contains(extension))
        {
            throw new FieldValidationException("Можно отправить PDF, Word, Excel, TXT или фото (JPG, PNG).");
        }

        if (upload.SizeBytes <= 0 || upload.SizeBytes > MaxFileBytes)
        {
            throw new FieldValidationException($"Файл — до {MaxFileBytes / 1024 / 1024} МБ.");
        }

        using var buffer = new MemoryStream();
        await upload.Content.CopyToAsync(buffer, ct);
        if (buffer.Length > MaxFileBytes || buffer.Length == 0)
        {
            throw new FieldValidationException($"Файл — до {MaxFileBytes / 1024 / 1024} МБ.");
        }

        var type = extension.ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".txt" => "text/plain",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            _ => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        };
        var key = $"recruitment/dialogs/{c.Id:N}/{clientKey:N}{extension.ToLowerInvariant()}";
        buffer.Position = 0;
        await storage.PutAsync(key, buffer, buffer.Length, type, ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "recruitment.dialog.file", "TelegramConversation", id.ToString(), new { size = buffer.Length }, ct);
        return await telegram.SendAsync(ctx, id, new SendInput(caption ?? string.Empty, clientKey), new OutboxFile(key, name, type, buffer.Length), ct);
    }

    /// <summary>«Повторить» неотправленное сообщение HR: прежнее — «отменено», то же сообщение (и файл) — снова в очередь.</summary>
    public async Task<HrDialog> RetryAsync(RecruitmentContext ctx, Guid id, Guid outgoingId, CancellationToken ct)
    {
        Manage(ctx);
        var row = await db.TelegramOutgoingMessages.FirstOrDefaultAsync(m => m.Id == outgoingId && m.ConversationId == id, ct)
                  ?? throw new FieldNotFoundException("Сообщение не найдено.");
        if (row.Status is not (TelegramOutboxStatus.Failed or TelegramOutboxStatus.Unknown))
        {
            throw new FieldConflictException("Сообщение уже отправлено или отправляется.");
        }

        row.Status = TelegramOutboxStatus.Cancelled;
        row.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await outbox.EnqueueAsync(new OutboxRequest(row.AccountId, row.PeerId, row.ConversationId, row.Department, row.Kind, row.Text, $"retry:{row.Id:N}", "user",
            ctx.UserId, File: row.FileKey is { } key ? new OutboxFile(key, row.FileName ?? "file", row.FileContentType ?? "application/octet-stream", row.FileSize ?? 0) : null), ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "recruitment.dialog.retry", "TelegramConversation", id.ToString(), null, ct);
        return await GetAsync(ctx, id, ct);
    }

    /// <summary>«Черновик AI»: AI-рекрутер пишет ответ по всей переписке, но не отправляет — текст уходит в поле ввода HR (платный вызов по кнопке).</summary>
    public async Task<string> DraftAsync(RecruitmentContext ctx, Guid id, CancellationToken ct)
    {
        Manage(ctx);
        if (!await db.TelegramConversations.AnyAsync(x => x.Id == id && x.Department == TelegramDepartments.Hr, ct))
        {
            throw new FieldNotFoundException("Диалог не найден.");
        }

        var agent = agents.FirstOrDefault() ?? throw new FieldValidationException("AI-рекрутер не подключён.");
        var text = await agent.DraftAsync(id, ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "recruitment.dialog.draft", "TelegramConversation", id.ToString(), null, ct);
        return text;
    }

    /// <summary>
    /// «Попросить администратора»: тем, кто настраивает Telegram, — уведомление подключить номер HR (не чаще раза в сутки от одного сотрудника).
    /// Возвращает, скольким отправлено.
    /// </summary>
    public async Task<int> AskAdminAsync(RecruitmentContext ctx, CancellationToken ct)
    {
        Read(ctx);
        var since = clock.GetUtcNow().AddDays(-1);
        var who = ctx.UserId.ToString();
        if (await db.AuditLogs.AnyAsync(a => a.Action == "recruitment.dialog.ask_admin" && a.ActorId == who && a.Timestamp > since, ct))
        {
            throw new FieldConflictException("Вы уже просили администратора сегодня — уведомление у него есть.");
        }

        var name = await db.Users.AsNoTracking().Where(u => u.Id == ctx.UserId).Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? "Сотрудник HR";
        var admins = (await db.ActiveUsersWithAsync(Permissions.TelegramManage, ct)).Where(u => u != ctx.UserId).ToList();
        foreach (var admin in admins)
        {
            notifier.Notify(admin, UserNotificationKind.RecruitmentAlert, $"{name} просит подключить номер HR к Telegram",
                "«Настройки → Telegram»: подключите номер, войдите по коду и выберите «Подразделения: HR» — тогда кандидаты смогут писать в OneBase.", "/settings/telegram");
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, who, "recruitment.dialog.ask_admin", "TelegramAccount", null, new { admins = admins.Count }, ct);
        return admins.Count;
    }
}
