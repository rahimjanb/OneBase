using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.Application.Field;
using OneBase.Domain.Audit;
using OneBase.Domain.Recruitment;

namespace OneBase.Application.Recruitment;

public sealed partial class RecruitmentService
{
    private static readonly HashSet<string> SafeTypes = new(StringComparer.OrdinalIgnoreCase)
        { "application/pdf", "image/png", "image/jpeg", "text/plain" };

    // ---------- Приём кандидата (вручную, HH, Telegram) ----------

    /// <summary>
    /// Принять кандидата с дедупликацией: тот же ID в источнике или тот же подтверждённый контакт — тот же кандидат (дополняется);
    /// совпадение неподтверждённого контакта — подозрение на дубль для HR. По имени кандидаты не объединяются никогда.
    /// actorId = null — приём системой (HH, Telegram).
    /// </summary>
    public async Task<IntakeResult> IntakeAsync(CandidateIntake intake, Guid? actorId, CancellationToken ct)
    {
        var name = Required(intake.FullName, 200, "имя кандидата");
        var source = Clean(intake.Source, 16) ?? "manual";
        if (!RecruitmentRules.Sources.Contains(source))
        {
            throw new FieldValidationException("Источник: вручную, HH, Telegram, рекомендация или другое.");
        }

        var externalId = Clean(intake.ExternalId, 100);
        var language = Clean(intake.Language, 8);
        if (language is not null && !RecruitmentRules.Languages.Contains(language))
        {
            throw new FieldValidationException("Язык общения — ru или uz.");
        }

        var consent = Clean(intake.ConsentChannel, 16);
        if (consent is not null && !RecruitmentRules.ConsentChannels.Contains(consent))
        {
            throw new FieldValidationException("Канал согласия: письменно, Telegram, HH, e-mail или устно.");
        }

        var contacts = new List<IntakeContact>();
        foreach (var c in intake.Contacts)
        {
            var value = RecruitmentRules.Normalize(c.Kind, c.Value)
                        ?? throw new FieldValidationException($"Не похоже на {RecruitmentRules.KindLabel(c.Kind)}: «{c.Value?.Trim()}».");
            var same = contacts.FindIndex(x => x.Kind == c.Kind && x.Value == value);
            if (same < 0)
            {
                contacts.Add(c with { Value = value });
            }
            else if (c.Verified)
            {
                contacts[same] = contacts[same] with { Verified = true };
            }
        }

        if (intake.VacancyId is { } vacancyId)
        {
            await OpenVacancyAsync(vacancyId, ct);
        }

        // Тот же человек: ID в источнике, затем подтверждённые контакты.
        Guid? existingId = null;
        string? matched = null;
        if (externalId is not null)
        {
            existingId = await db.CandidateSources.AsNoTracking().Where(s => s.Source == source && s.ExternalId == externalId)
                .Select(s => (Guid?)s.CandidateId).FirstOrDefaultAsync(ct);
            matched = existingId is null ? null : "source";
        }

        foreach (var c in contacts.Where(c => c.Verified))
        {
            if (existingId is not null)
            {
                break;
            }

            existingId = await db.CandidateContacts.AsNoTracking().Where(x => x.Kind == c.Kind && x.Value == c.Value && x.Verified)
                .Select(x => (Guid?)x.CandidateId).FirstOrDefaultAsync(ct);
            matched = existingId is null ? null : "contact";
        }

        var now = clock.GetUtcNow();
        Candidate candidate;
        if (existingId is { } id)
        {
            candidate = await db.Candidates.Include(c => c.Contacts).Include(c => c.Sources).FirstAsync(c => c.Id == id, ct);
            candidate.City ??= Clean(intake.City, 100);
            candidate.Language ??= language;
            if (Clean(intake.Notes, 2000) is { } notes && !(candidate.Notes ?? string.Empty).Contains(notes, StringComparison.Ordinal))
            {
                candidate.Notes = Clean(string.IsNullOrEmpty(candidate.Notes) ? notes : $"{candidate.Notes}\n{notes}", 4000);
            }
        }
        else
        {
            candidate = new Candidate
            {
                FullName = name,
                City = Clean(intake.City, 100),
                Language = language,
                Notes = Clean(intake.Notes, 4000),
                LastActivityAt = now,
                CreatedById = actorId ?? Guid.Empty,
            };
            db.Candidates.Add(candidate);
        }

        foreach (var c in contacts)
        {
            // Подтверждённый контакт другого кандидата не «переезжает»: у этого он остаётся неподтверждённым — и это подозрение на дубль.
            var verified = c.Verified && !await db.CandidateContacts.AnyAsync(x => x.Kind == c.Kind && x.Value == c.Value && x.Verified && x.CandidateId != candidate.Id, ct);
            var have = candidate.Contacts.FirstOrDefault(x => x.Kind == c.Kind && x.Value == c.Value);
            if (have is null)
            {
                db.CandidateContacts.Add(new CandidateContact
                {
                    CandidateId = candidate.Id,
                    Kind = c.Kind,
                    Value = c.Value,
                    Verified = verified,
                    VerifiedAt = verified ? now : null,
                    Source = source,
                });
            }
            else if (verified && !have.Verified)
            {
                have.Verified = true;
                have.VerifiedAt = now;
            }
        }

        if (!candidate.Sources.Any(s => s.Source == source && s.ExternalId == externalId))
        {
            db.CandidateSources.Add(new CandidateSource { CandidateId = candidate.Id, Source = source, ExternalId = externalId, Note = Clean(intake.SourceNote, 500) });
        }

        candidate.LastActivityAt = now;
        await db.SaveChangesAsync(ct);

        if (intake.VacancyId is { } vid && !await db.CandidateApplications.AnyAsync(a => a.CandidateId == candidate.Id && a.VacancyId == vid, ct))
        {
            AddApplication(candidate.Id, vid, source, actorId, now);
        }

        if (consent is not null)
        {
            await AddConsentAsync(candidate.Id, consent, actorId, ct);
        }

        await db.SaveChangesAsync(ct);
        var duplicates = await DetectDuplicatesAsync(candidate.Id, ct);
        if (duplicates > 0 && actorId is null)
        {
            // Пришёл сам (HH, Telegram) и похож на другого кандидата — решает HR.
            await exceptions.RaiseAsync("duplicate", "Кандидат похож на другого: совпал контакт. Объедините карточки или отметьте, что это разные люди.",
                candidate.Id, intake.VacancyId, dedupKey: $"dup:{candidate.Id:N}", ct: ct);
        }

        await RefreshRetentionAsync(candidate.Id, touch: false, ct);
        await audit.LogAsync(actorId is null ? ActorType.System : ActorType.User, actorId?.ToString() ?? "recruitment",
            existingId is null ? "recruitment.candidate.created" : "recruitment.candidate.matched", nameof(Candidate), candidate.Id.ToString(),
            new { source, matched, contacts = contacts.Count, vacancy = intake.VacancyId, duplicates }, ct);
        return new IntakeResult(candidate.Id, existingId is null, matched, duplicates);
    }

    /// <summary>RelatedType диалога Telegram, который ведётся с кандидатом (RelatedId — ID кандидата).</summary>
    public const string CandidateRelation = "candidate";

    /// <summary>Согласие, данное кандидатом в переписке (ответил «согласен» на текст согласия) — записывает система, не сотрудник.</summary>
    public async Task RecordConsentByCandidateAsync(Guid candidateId, string channel, CancellationToken ct)
    {
        if (await db.CandidateConsents.AnyAsync(c => c.CandidateId == candidateId && c.WithdrawnAt == null, ct))
        {
            return;
        }

        await AddConsentAsync(candidateId, channel, null, ct);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.Agent, "recruiter", "recruitment.consent.recorded", nameof(Candidate), candidateId.ToString(), new { channel, byCandidate = true }, ct);
    }

    public Task<bool> HasConsentAsync(Guid candidateId, CancellationToken ct) =>
        db.CandidateConsents.AnyAsync(c => c.CandidateId == candidateId && c.WithdrawnAt == null, ct);

    /// <summary>Заметка к кандидату (что выяснил AI-рекрутер: опыт, ожидания, когда может выйти) — без повторов.</summary>
    public async Task AppendNotesAsync(Guid candidateId, string text, CancellationToken ct)
    {
        var note = Clean(text, 1000);
        var candidate = await db.Candidates.FirstOrDefaultAsync(c => c.Id == candidateId, ct);
        if (note is null || candidate is null || (candidate.Notes ?? string.Empty).Contains(note, StringComparison.Ordinal))
        {
            return;
        }

        candidate.Notes = Clean(string.IsNullOrEmpty(candidate.Notes) ? note : $"{candidate.Notes}\n{note}", 4000);
        await db.SaveChangesAsync(ct);
    }

    public const int MaxScreeningAnswers = 30;

    /// <summary>Ответы на вопросы отбора, которые AI-рекрутер узнал в переписке: дописываются к прежним (тот же вопрос — новый ответ).</summary>
    public async Task SaveScreeningAnswersAsync(Guid applicationId, IReadOnlyDictionary<string, string> answers, CancellationToken ct)
    {
        var clean = answers.Select(a => (Q: Clean(a.Key, 200), A: Clean(a.Value, 300))).Where(a => a.Q is not null && a.A is not null).ToList();
        var application = await db.CandidateApplications.FirstOrDefaultAsync(a => a.Id == applicationId, ct);
        if (clean.Count == 0 || application is null)
        {
            return;
        }

        var all = ScreeningAnswers(application.ScreeningAnswers);
        foreach (var (q, a) in clean)
        {
            all[q!] = a!;
        }

        application.ScreeningAnswers = System.Text.Json.JsonSerializer.Serialize(all.Take(MaxScreeningAnswers).ToDictionary());
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Ответы на вопросы отбора из JSON (повреждённый — пусто).</summary>
    public static Dictionary<string, string> ScreeningAnswers(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json) is { } d
                ? new Dictionary<string, string>(d, StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (System.Text.Json.JsonException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    /// <summary>Отметить активность кандидата (сообщение, ответ) — срок хранения считается заново.</summary>
    public Task TouchCandidateAsync(Guid candidateId, CancellationToken ct) => RefreshRetentionAsync(candidateId, touch: true, ct);

    /// <summary>Кандидат рассматривается на вакансию (из Telegram по коду V12, из HH по отклику): нет отклика и вакансия открыта — создаётся.</summary>
    public async Task<bool> AttachVacancyAsync(Guid candidateId, Guid vacancyId, string source, Guid? actorId, CancellationToken ct)
    {
        var vacancy = await db.Vacancies.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vacancyId, ct);
        if (vacancy is not { Status: VacancyStatus.Open or VacancyStatus.Paused }
            || await db.CandidateApplications.AnyAsync(a => a.CandidateId == candidateId && a.VacancyId == vacancyId, ct))
        {
            return false;
        }

        AddApplication(candidateId, vacancyId, source, actorId, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        await RefreshRetentionAsync(candidateId, touch: true, ct);
        return true;
    }

    private async Task<Vacancy> OpenVacancyAsync(Guid vacancyId, CancellationToken ct)
    {
        var vacancy = await db.Vacancies.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vacancyId, ct) ?? throw new FieldValidationException("Вакансия не найдена.");
        return vacancy.Status is VacancyStatus.Open or VacancyStatus.Paused
            ? vacancy
            : throw new FieldValidationException(vacancy.Status == VacancyStatus.Draft
                ? $"Вакансия {vacancy.Code} ещё черновик — откройте её, чтобы добавлять кандидатов."
                : $"Вакансия {vacancy.Code} закрыта.");
    }

    private void AddApplication(Guid candidateId, Guid vacancyId, string source, Guid? actorId, DateTimeOffset now)
    {
        var application = new CandidateApplication
        {
            CandidateId = candidateId,
            VacancyId = vacancyId,
            Source = source,
            StageChangedAt = now,
            CreatedById = actorId,
        };
        db.CandidateApplications.Add(application);
        db.ApplicationEvents.Add(new ApplicationEvent { ApplicationId = application.Id, ToStage = ApplicationStage.New, ActorId = actorId, At = now });
    }

    private async Task AddConsentAsync(Guid candidateId, string channel, Guid? actorId, CancellationToken ct)
    {
        var text = await ConsentTextAsync(ct);
        db.CandidateConsents.Add(new CandidateConsent
        {
            CandidateId = candidateId,
            Purpose = RecruitmentRules.PurposeRecruitment,
            Channel = channel,
            TextVersion = text.Version,
            GivenAt = clock.GetUtcNow(),
            RecordedById = actorId,
        });
    }

    /// <summary>Подозрения на дубль: контакт кандидата есть у другого кандидата. Возвращает, сколько открытых подозрений у кандидата.</summary>
    private async Task<int> DetectDuplicatesAsync(Guid candidateId, CancellationToken ct)
    {
        var contacts = await db.CandidateContacts.AsNoTracking().Where(c => c.CandidateId == candidateId).Select(c => new { c.Kind, c.Value }).ToListAsync(ct);
        foreach (var c in contacts)
        {
            var others = await db.CandidateContacts.AsNoTracking().Where(x => x.Kind == c.Kind && x.Value == c.Value && x.CandidateId != candidateId)
                .Select(x => x.CandidateId).Distinct().ToListAsync(ct);
            foreach (var other in others)
            {
                var known = await db.CandidateDuplicates.AnyAsync(d => d.Kind == c.Kind && d.Value == c.Value
                    && ((d.CandidateId == candidateId && d.OtherCandidateId == other) || (d.CandidateId == other && d.OtherCandidateId == candidateId)), ct);
                if (!known)
                {
                    db.CandidateDuplicates.Add(new CandidateDuplicate { CandidateId = candidateId, OtherCandidateId = other, Kind = c.Kind, Value = c.Value });
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return await db.CandidateDuplicates.CountAsync(d => d.Status == CandidateDuplicateStatus.Open && (d.CandidateId == candidateId || d.OtherCandidateId == candidateId), ct);
    }

    /// <summary>
    /// Срок хранения: touch — отметить активность сейчас; затем «с какого момента идёт срок» по откликам (открыт хоть один — null, подбор
    /// идёт).
    /// </summary>
    private async Task RefreshRetentionAsync(Guid candidateId, bool touch, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        if (touch)
        {
            await db.Candidates.Where(c => c.Id == candidateId).ExecuteUpdateAsync(u => u.SetProperty(c => c.LastActivityAt, now), ct);
        }

        var last = await db.Candidates.AsNoTracking().Where(c => c.Id == candidateId).Select(c => (DateTimeOffset?)c.LastActivityAt).FirstOrDefaultAsync(ct);
        if (last is null)
        {
            return;
        }

        var applications = await db.CandidateApplications.AsNoTracking().Where(a => a.CandidateId == candidateId).Select(a => new { a.Stage, a.ClosedAt }).ToListAsync(ct);
        var from = RecruitmentRules.RetentionFrom(applications.Select(a => (a.Stage, a.ClosedAt)), last.Value);
        await db.Candidates.Where(c => c.Id == candidateId).ExecuteUpdateAsync(u => u.SetProperty(c => c.RetentionFrom, from), ct);
    }

    private async Task<Candidate> CandidateRowAsync(Guid id, CancellationToken ct) =>
        await db.Candidates.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new FieldNotFoundException("Кандидат не найден (возможно, его данные уже удалены).");

    // ---------- Кандидаты: список, карточка, правка ----------

    public async Task<CandidatePage> CandidatesAsync(RecruitmentContext ctx, CandidateQuery query, CancellationToken ct)
    {
        RequireRead(ctx);
        var q = db.Candidates.AsNoTracking();
        if (Clean(query.Search, 100) is { } search)
        {
            var lower = search.ToLower();
            var digits = new string(search.Where(char.IsAsciiDigit).ToArray());
            q = q.Where(c => c.FullName.ToLower().Contains(lower)
                             || db.CandidateContacts.Any(x => x.CandidateId == c.Id && (x.Value.Contains(lower) || (digits.Length >= 4 && x.Value.Contains(digits)))));
        }

        if (query.VacancyId is { } vacancy)
        {
            q = q.Where(c => db.CandidateApplications.Any(a => a.CandidateId == c.Id && a.VacancyId == vacancy));
        }

        if (query.Stage is { } stage)
        {
            q = q.Where(c => db.CandidateApplications.Any(a => a.CandidateId == c.Id && a.Stage == stage && (query.VacancyId == null || a.VacancyId == query.VacancyId)));
        }

        if (query.DuplicatesOnly)
        {
            q = q.Where(c => db.CandidateDuplicates.Any(d => d.Status == CandidateDuplicateStatus.Open && (d.CandidateId == c.Id || d.OtherCandidateId == c.Id)));
        }

        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 200);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(c => c.LastActivityAt).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        var ids = rows.Select(c => c.Id).ToList();
        var phones = await db.CandidateContacts.AsNoTracking().Where(c => ids.Contains(c.CandidateId) && c.Kind == CandidateContactKind.Phone)
            .Select(c => new { c.CandidateId, c.Value, c.Verified }).ToListAsync(ct);
        var applications = await db.CandidateApplications.AsNoTracking().Where(a => ids.Contains(a.CandidateId)).Include(a => a.Vacancy)
            .OrderByDescending(a => a.StageChangedAt).ToListAsync(ct);
        var consents = await db.CandidateConsents.AsNoTracking().Where(c => ids.Contains(c.CandidateId) && c.WithdrawnAt == null).Select(c => c.CandidateId).ToListAsync(ct);
        var duplicates = await db.CandidateDuplicates.AsNoTracking().Where(d => d.Status == CandidateDuplicateStatus.Open && (ids.Contains(d.CandidateId) || ids.Contains(d.OtherCandidateId)))
            .Select(d => new { d.CandidateId, d.OtherCandidateId }).ToListAsync(ct);
        var resumes = await db.CandidateResumes.AsNoTracking().Where(r => ids.Contains(r.CandidateId)).GroupBy(r => r.CandidateId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var days = await RetentionDaysAsync(ct);
        return new CandidatePage(rows.Select(c => new CandidateRow(c.Id, c.FullName, c.City,
            phones.Where(p => p.CandidateId == c.Id).OrderByDescending(p => p.Verified).Select(p => p.Value).FirstOrDefault(),
            applications.Where(a => a.CandidateId == c.Id).Select(a => new CandidateApplicationBrief(a.VacancyId, a.Vacancy?.Code ?? "—", a.Vacancy?.Title ?? "—", a.Stage.ToString())).ToList(),
            c.LastActivityAt, RecruitmentRules.DeleteAt(c.RetentionFrom, days), consents.Contains(c.Id),
            duplicates.Count(d => d.CandidateId == c.Id || d.OtherCandidateId == c.Id), resumes.GetValueOrDefault(c.Id))).ToList(), total, page, size);
    }

    public async Task<CandidateCard> CandidateAsync(RecruitmentContext ctx, Guid id, CancellationToken ct)
    {
        RequireRead(ctx);
        var c = await db.Candidates.AsNoTracking().Include(x => x.Contacts).Include(x => x.Sources).Include(x => x.Resumes).Include(x => x.Consents)
                    .FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new FieldNotFoundException("Кандидат не найден (возможно, его данные уже удалены).");
        var applications = await db.CandidateApplications.AsNoTracking().Where(a => a.CandidateId == id).Include(a => a.Vacancy)
            .OrderByDescending(a => a.StageChangedAt).ToListAsync(ct);
        var applicationIds = applications.Select(a => a.Id).ToList();
        var events = await db.ApplicationEvents.AsNoTracking().Where(e => applicationIds.Contains(e.ApplicationId)).OrderByDescending(e => e.At).ToListAsync(ct);
        var duplicates = await db.CandidateDuplicates.AsNoTracking().Where(d => d.Status == CandidateDuplicateStatus.Open && (d.CandidateId == id || d.OtherCandidateId == id))
            .ToListAsync(ct);
        var otherIds = duplicates.Select(d => d.CandidateId == id ? d.OtherCandidateId : d.CandidateId).ToList();
        var others = await db.Candidates.AsNoTracking().Where(x => otherIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        var names = await NamesAsync(events.Select(e => e.ActorId).Concat(c.Consents.Select(x => x.RecordedById)), ct);
        var days = await RetentionDaysAsync(ct);
        return new CandidateCard(c.Id, c.FullName, c.City, c.Language, c.Notes, c.CreatedAt, c.LastActivityAt, RecruitmentRules.DeleteAt(c.RetentionFrom, days), days,
            c.Consents.Any(x => x.WithdrawnAt == null),
            c.Contacts.OrderBy(x => x.Kind).ThenByDescending(x => x.Verified).Select(x => new ContactView(x.Id, x.Kind.ToString(), x.Value, x.Verified, x.Source)).ToList(),
            c.Sources.OrderBy(x => x.CreatedAt).Select(x => new SourceView(x.Source, x.ExternalId, x.Note, x.CreatedAt)).ToList(),
            c.Resumes.OrderByDescending(x => x.CreatedAt).Select(x => new ResumeView(x.Id, x.FileName, x.SizeBytes, x.Source, x.CreatedAt, !string.IsNullOrEmpty(x.Text),
                x.TextError, x.Text is { Length: > 0 } t ? (t.Length <= 1500 ? t : t[..1500] + "…") : null, x.TextByAi, x.DocumentType,
                vision.FirstOrDefault() is { } reader && reader.Supports(Path.GetExtension(x.FileName)))).ToList(),
            applications.Select(a => new ApplicationView(a.Id, a.VacancyId, a.Vacancy?.Code ?? "—", a.Vacancy?.Title ?? "—", a.Vacancy?.Status.ToString() ?? "—",
                a.Stage.ToString(), a.Source, a.RejectReason, a.StageChangedAt,
                events.Where(e => e.ApplicationId == a.Id).Select(e => new ApplicationEventView(e.FromStage?.ToString(), e.ToStage.ToString(), e.Comment,
                    e.ActorId is { } actor ? names.GetValueOrDefault(actor) : null, e.At)).ToList(), ScreeningAnswers(a.ScreeningAnswers))).ToList(),
            c.Consents.OrderByDescending(x => x.GivenAt).Select(x => new ConsentView(x.Id, x.Channel, x.TextVersion, x.GivenAt, x.WithdrawnAt,
                x.RecordedById is { } r ? names.GetValueOrDefault(r) : null)).ToList(),
            duplicates.Select(d =>
            {
                var other = d.CandidateId == id ? d.OtherCandidateId : d.CandidateId;
                return new DuplicateView(d.Id, other, others.GetValueOrDefault(other) ?? "—", d.Kind.ToString(), d.Value);
            }).ToList());
    }

    public async Task<IntakeResult> CreateCandidateAsync(RecruitmentContext ctx, CandidateInput input, CancellationToken ct)
    {
        RequireManage(ctx);
        var source = Clean(input.Source, 16) ?? "manual";
        if (source is "hh" or "telegram")
        {
            throw new FieldValidationException("Кандидаты из HH и Telegram приходят сами; вручную — «вручную», «рекомендация» или «другое».");
        }

        return await IntakeAsync(new CandidateIntake(input.FullName, input.City, input.Language, input.Notes,
            (input.Contacts ?? []).Select(c => new IntakeContact(c.Kind, c.Value, false)).ToList(), source, null, input.SourceNote, input.VacancyId,
            input.ConsentChannel), ctx.UserId, ct);
    }

    public async Task<CandidateCard> UpdateCandidateAsync(RecruitmentContext ctx, Guid id, CandidateUpdateInput input, CancellationToken ct)
    {
        RequireManage(ctx);
        var candidate = await CandidateRowAsync(id, ct);
        var language = Clean(input.Language, 8);
        if (language is not null && !RecruitmentRules.Languages.Contains(language))
        {
            throw new FieldValidationException("Язык общения — ru или uz.");
        }

        candidate.FullName = Required(input.FullName, 200, "имя кандидата");
        candidate.City = Clean(input.City, 100);
        candidate.Language = language;
        candidate.Notes = Clean(input.Notes, 4000);
        candidate.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await RefreshRetentionAsync(id, touch: true, ct);
        await AuditAsync(ctx, "recruitment.candidate.updated", nameof(Candidate), id, null, ct);
        return await CandidateAsync(ctx, id, ct);
    }

    public async Task<CandidateCard> AddContactAsync(RecruitmentContext ctx, Guid id, ContactInput input, CancellationToken ct)
    {
        RequireManage(ctx);
        await CandidateRowAsync(id, ct);
        var value = RecruitmentRules.Normalize(input.Kind, input.Value)
                    ?? throw new FieldValidationException($"Не похоже на {RecruitmentRules.KindLabel(input.Kind)}: «{input.Value?.Trim()}».");
        if (await db.CandidateContacts.AnyAsync(c => c.CandidateId == id && c.Kind == input.Kind && c.Value == value, ct))
        {
            throw new FieldConflictException("Такой контакт у кандидата уже есть.");
        }

        db.CandidateContacts.Add(new CandidateContact { CandidateId = id, Kind = input.Kind, Value = value, Source = "manual" });
        await db.SaveChangesAsync(ct);
        await DetectDuplicatesAsync(id, ct);
        await RefreshRetentionAsync(id, touch: true, ct);
        await AuditAsync(ctx, "recruitment.contact.added", nameof(Candidate), id, new { kind = input.Kind.ToString() }, ct);
        return await CandidateAsync(ctx, id, ct);
    }

    public async Task<CandidateCard> RemoveContactAsync(RecruitmentContext ctx, Guid id, Guid contactId, CancellationToken ct)
    {
        RequireManage(ctx);
        var contact = await db.CandidateContacts.FirstOrDefaultAsync(c => c.Id == contactId && c.CandidateId == id, ct)
                      ?? throw new FieldNotFoundException("Контакт не найден.");
        db.CandidateContacts.Remove(contact);
        await db.CandidateDuplicates.Where(d => d.Status == CandidateDuplicateStatus.Open && d.Kind == contact.Kind && d.Value == contact.Value
            && (d.CandidateId == id || d.OtherCandidateId == id)).ExecuteDeleteAsync(ct);
        await db.SaveChangesAsync(ct);
        await RefreshRetentionAsync(id, touch: true, ct);
        await AuditAsync(ctx, "recruitment.contact.removed", nameof(Candidate), id, new { kind = contact.Kind.ToString() }, ct);
        return await CandidateAsync(ctx, id, ct);
    }

    // ---------- Резюме ----------

    public async Task<CandidateCard> UploadResumeAsync(RecruitmentContext ctx, Guid id, RecruitmentUpload upload, CancellationToken ct)
    {
        RequireManage(ctx);
        await AddResumeAsync(id, upload, "manual", ctx.UserId, ct);
        await AuditAsync(ctx, "recruitment.resume.uploaded", nameof(Candidate), id, new { upload.SizeBytes }, ct);
        return await CandidateAsync(ctx, id, ct);
    }

    /// <summary>Резюме кандидату: файл — в хранилище, текст — извлекается сразу (PDF, DOCX, TXT); не извлёкся — причина у резюме, файл остаётся.</summary>
    public async Task<Guid> AddResumeAsync(Guid candidateId, RecruitmentUpload upload, string source, Guid? actorId, CancellationToken ct)
    {
        await CandidateRowAsync(candidateId, ct);
        if (upload.SizeBytes <= 0)
        {
            throw new FieldValidationException("Файл пустой.");
        }

        if (upload.SizeBytes > RecruitmentRules.MaxResumeBytes)
        {
            throw new FieldValidationException($"Резюме больше {RecruitmentRules.MaxResumeBytes / 1024 / 1024} МБ.");
        }

        var fileName = SafeFileName(upload.FileName);
        var extension = Path.GetExtension(fileName);
        if (!RecruitmentRules.ResumeExtensions.Contains(extension))
        {
            throw new FieldValidationException("Резюме — PDF, Word (DOCX, DOC), RTF, ODT, TXT или фото (JPG, PNG).");
        }

        if (await db.CandidateResumes.CountAsync(r => r.CandidateId == candidateId, ct) >= RecruitmentRules.MaxResumesPerCandidate)
        {
            throw new FieldValidationException($"У кандидата — до {RecruitmentRules.MaxResumesPerCandidate} резюме. Удалите старые.");
        }

        using var buffer = new MemoryStream();
        await upload.Content.CopyToAsync(buffer, ct);
        if (buffer.Length > RecruitmentRules.MaxResumeBytes)
        {
            throw new FieldValidationException($"Резюме больше {RecruitmentRules.MaxResumeBytes / 1024 / 1024} МБ.");
        }

        var sha = Convert.ToHexStringLower(SHA256.HashData(buffer.ToArray()));
        if (await db.CandidateResumes.AnyAsync(r => r.CandidateId == candidateId && r.Sha256 == sha, ct))
        {
            throw new FieldConflictException("Это резюме у кандидата уже есть.");
        }

        var reading = await ResumeTextAsync(buffer.ToArray(), fileName, candidateId, forceAi: false, ct);

        var contentType = string.IsNullOrWhiteSpace(upload.ContentType) || upload.ContentType.Length > 200 ? "application/octet-stream" : upload.ContentType.Trim();
        var resume = new CandidateResume
        {
            CandidateId = candidateId,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = buffer.Length,
            ObjectKey = string.Empty,
            Sha256 = sha,
            Text = reading.Text,
            TextError = reading.Error,
            TextByAi = reading.ByAi,
            DocumentType = reading.DocumentType,
            Source = source,
            UploadedById = actorId,
        };
        resume.ObjectKey = $"recruitment/resumes/{candidateId:N}/{resume.Id:N}";
        buffer.Position = 0;
        await storage.PutAsync(resume.ObjectKey, buffer, buffer.Length, contentType, ct);
        db.CandidateResumes.Add(resume);
        await db.SaveChangesAsync(ct);
        await RefreshRetentionAsync(candidateId, touch: true, ct);
        return resume.Id;
    }

    private sealed record ResumeReading(string? Text, string? Error, bool ByAi, string? DocumentType);

    /// <summary>Меньше стольких букв и цифр в текстовом слое PDF — это скан (подпись, номер страницы): его читает AI.</summary>
    private const int MinPdfLetters = 80;

    /// <summary>
    /// Текст резюме: текстовый слой (PDF, DOCX, TXT); фото и PDF-скан без текста — AI (Claude), если в настройках подбора включено
    /// «Распознавать фото и сканы». forceAi — «Распознать снова»: AI читает и PDF с текстовым слоем.
    /// </summary>
    private async Task<ResumeReading> ResumeTextAsync(byte[] bytes, string fileName, Guid candidateId, bool forceAi, CancellationToken ct)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        string? text = null;
        string? error = null;
        if (!forceAi && extractor.Extensions.Contains(extension))
        {
            try
            {
                text = (await extractor.ExtractAsync(new MemoryStream(bytes, writable: false), fileName, ct)).Trim();
                if (text.Length == 0)
                {
                    (text, error) = (null, "В файле нет текста.");
                }
            }
            catch (NotSupportedException ex)
            {
                error = ex.Message;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Подбор: текст резюме {Candidate} не извлечён", candidateId);
                error = "Текст не извлёкся — файл сохранён, его можно открыть.";
            }
        }
        else if (!forceAi)
        {
            error = "Текст из этого формата не извлекается — файл сохранён.";
        }

        var scan = text is null || extension == ".pdf" && text.Count(char.IsLetterOrDigit) < MinPdfLetters;
        if ((forceAi || scan) && vision.FirstOrDefault() is { } reader && reader.Supports(extension))
        {
            var settings = await db.RecruitmentSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1, ct) ?? new RecruitmentSettings();
            if (!settings.ReadScans)
            {
                return new ResumeReading(Clip(text), text is null ? "Фото и сканы не распознаются: включите «Распознавать фото и сканы» в настройках подбора." : null,
                    false, null);
            }

            var read = await reader.ReadAsync(bytes, fileName, "hr.resume.read", ct);
            if (read.Text is { Length: > 0 } recognized)
            {
                return new ResumeReading(Clip(recognized), null, true, read.DocumentType);
            }

            return text is not null && !forceAi
                ? new ResumeReading(Clip(text), null, false, read.DocumentType)
                : new ResumeReading(null, $"{(extension == ".pdf" ? "Скан PDF" : "Фото")} не распознан{(extension == ".pdf" ? string.Empty : "о")}: {read.Error ?? "AI не нашёл в файле текста."}", false,
                    read.DocumentType);
        }

        if (text is null && extension is ".jpg" or ".jpeg" or ".png" or ".webp")
        {
            error = "Фото — распознавание недоступно (AI не подключён).";
        }

        return new ResumeReading(Clip(text), text is null ? error : null, false, null);
    }

    private static string? Clip(string? text) =>
        text is null ? null : text.Length > RecruitmentRules.MaxResumeText ? text[..RecruitmentRules.MaxResumeText] : text;

    /// <summary>«Распознать снова»: файл из хранилища читает AI (фото, скан, PDF с плохим текстовым слоем). Платный вызов — только по кнопке HR.</summary>
    public async Task<CandidateCard> ReadResumeAgainAsync(RecruitmentContext ctx, Guid resumeId, CancellationToken ct)
    {
        RequireManage(ctx);
        var r = await db.CandidateResumes.FirstOrDefaultAsync(x => x.Id == resumeId, ct) ?? throw new FieldNotFoundException("Резюме не найдено.");
        if (vision.FirstOrDefault() is not { } reader || !reader.Supports(Path.GetExtension(r.FileName)))
        {
            throw new FieldValidationException("AI распознаёт только фото (JPG, PNG, WEBP) и PDF.");
        }

        using var buffer = new MemoryStream();
        await storage.DownloadAsync(r.ObjectKey, buffer, ct);
        var reading = await ResumeTextAsync(buffer.ToArray(), r.FileName, r.CandidateId, forceAi: true, ct);
        if (reading.Text is null)
        {
            r.TextError = reading.Error;
            r.DocumentType = reading.DocumentType ?? r.DocumentType;
            await db.SaveChangesAsync(ct);
            throw new FieldValidationException(reading.Error ?? "Распознать не удалось.");
        }

        r.Text = reading.Text;
        r.TextError = null;
        r.TextByAi = reading.ByAi;
        r.DocumentType = reading.DocumentType;
        r.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await AuditAsync(ctx, "recruitment.resume.read", nameof(Candidate), r.CandidateId, new { reading.ByAi }, ct);
        return await CandidateAsync(ctx, r.CandidateId, ct);
    }

    private static string SafeFileName(string? name)
    {
        var file = Path.GetFileName(name?.Trim() ?? string.Empty);
        foreach (var bad in Path.GetInvalidFileNameChars())
        {
            file = file.Replace(bad, '_');
        }

        file = file.Length <= 200 ? file : file[^200..];
        return string.IsNullOrWhiteSpace(file) ? "resume" : file;
    }

    public async Task<RecruitmentDownload> DownloadResumeAsync(RecruitmentContext ctx, Guid resumeId, CancellationToken ct)
    {
        RequireRead(ctx);
        var r = await db.CandidateResumes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == resumeId, ct) ?? throw new FieldNotFoundException("Резюме не найдено.");
        await AuditAsync(ctx, "recruitment.resume.downloaded", nameof(Candidate), r.CandidateId, null, ct);
        return new RecruitmentDownload(r.FileName, SafeTypes.Contains(r.ContentType) ? r.ContentType : "application/octet-stream", r.ObjectKey);
    }

    public async Task<CandidateCard> DeleteResumeAsync(RecruitmentContext ctx, Guid resumeId, CancellationToken ct)
    {
        RequireManage(ctx);
        var r = await db.CandidateResumes.FirstOrDefaultAsync(x => x.Id == resumeId, ct) ?? throw new FieldNotFoundException("Резюме не найдено.");
        await storage.DeleteAsync(r.ObjectKey, ct);
        db.CandidateResumes.Remove(r);
        await db.SaveChangesAsync(ct);
        await RefreshRetentionAsync(r.CandidateId, touch: true, ct);
        await AuditAsync(ctx, "recruitment.resume.deleted", nameof(Candidate), r.CandidateId, null, ct);
        return await CandidateAsync(ctx, r.CandidateId, ct);
    }

    // ---------- Отклики и этапы ----------

    public async Task<CandidateCard> AddApplicationAsync(RecruitmentContext ctx, Guid id, ApplicationInput input, CancellationToken ct)
    {
        RequireManage(ctx);
        await CandidateRowAsync(id, ct);
        var vacancy = await OpenVacancyAsync(input.VacancyId, ct);
        if (await db.CandidateApplications.AnyAsync(a => a.CandidateId == id && a.VacancyId == vacancy.Id, ct))
        {
            throw new FieldConflictException($"Кандидат уже рассматривается на вакансию {vacancy.Code}.");
        }

        AddApplication(id, vacancy.Id, "manual", ctx.UserId, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        await RefreshRetentionAsync(id, touch: true, ct);
        await AuditAsync(ctx, "recruitment.application.created", nameof(Candidate), id, new { vacancy = vacancy.Code }, ct);
        return await CandidateAsync(ctx, id, ct);
    }

    /// <summary>Этап найма. Отказ — с причиной; закрытый отклик можно вернуть в работу. Решение «принят» — за человеком (AI его не ставит).</summary>
    public async Task<CandidateCard> ChangeStageAsync(RecruitmentContext ctx, Guid applicationId, StageInput input, CancellationToken ct)
    {
        RequireManage(ctx);
        var application = await db.CandidateApplications.FirstOrDefaultAsync(a => a.Id == applicationId, ct) ?? throw new FieldNotFoundException("Отклик не найден.");
        if (application.Stage == input.Stage)
        {
            return await CandidateAsync(ctx, application.CandidateId, ct);
        }

        if (input.Stage == ApplicationStage.Rejected && Clean(input.RejectReason, 1000) is null)
        {
            throw new FieldValidationException("Укажите причину отказа.");
        }

        var from = application.Stage;
        Stage(application, input.Stage, input.Comment, input.RejectReason, ctx.UserId, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        await RefreshRetentionAsync(application.CandidateId, touch: true, ct);
        await AuditAsync(ctx, "recruitment.application.stage", nameof(Candidate), application.CandidateId,
            new { application = application.Id, from = from.ToString(), to = input.Stage.ToString() }, ct);
        return await CandidateAsync(ctx, application.CandidateId, ct);
    }

    private void Stage(CandidateApplication application, ApplicationStage to, string? comment, string? rejectReason, Guid? actorId, DateTimeOffset now)
    {
        db.ApplicationEvents.Add(new ApplicationEvent
        {
            ApplicationId = application.Id,
            FromStage = application.Stage,
            ToStage = to,
            Comment = Clean(comment, 1000),
            ActorId = actorId,
            At = now,
        });
        application.Stage = to;
        application.StageChangedAt = now;
        application.UpdatedAt = now;
        application.ClosedAt = RecruitmentRules.IsClosed(to) ? now : null;
        application.RejectReason = to == ApplicationStage.Rejected ? Clean(rejectReason, 1000) : null;
    }

    // ---------- Согласие ----------

    public async Task<CandidateCard> RecordConsentAsync(RecruitmentContext ctx, Guid id, ConsentInput input, CancellationToken ct)
    {
        RequireManage(ctx);
        await CandidateRowAsync(id, ct);
        var channel = Clean(input.Channel, 16);
        if (channel is null || !RecruitmentRules.ConsentChannels.Contains(channel))
        {
            throw new FieldValidationException("Канал согласия: письменно, Telegram, HH, e-mail или устно.");
        }

        await AddConsentAsync(id, channel, ctx.UserId, ct);
        await db.SaveChangesAsync(ct);
        await RefreshRetentionAsync(id, touch: true, ct);
        await AuditAsync(ctx, "recruitment.consent.recorded", nameof(Candidate), id, new { channel }, ct);
        return await CandidateAsync(ctx, id, ct);
    }

    /// <summary>Отзыв согласия: данные кандидата удаляются сразу — вместе с резюме, контактами, откликами.</summary>
    public async Task WithdrawConsentAsync(RecruitmentContext ctx, Guid consentId, CancellationToken ct)
    {
        RequireManage(ctx);
        var consent = await db.CandidateConsents.FirstOrDefaultAsync(c => c.Id == consentId, ct) ?? throw new FieldNotFoundException("Согласие не найдено.");
        consent.WithdrawnAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await DeleteCandidateDataAsync(consent.CandidateId, "consent_withdrawn", ctx.UserId, ct);
    }

    // ---------- Удаление данных и дубли ----------

    public Task DeleteCandidateAsync(RecruitmentContext ctx, Guid id, CancellationToken ct)
    {
        RequireManage(ctx);
        return DeleteCandidateDataAsync(id, "manual", ctx.UserId, ct);
    }

    /// <summary>
    /// Удалить все данные кандидата: файлы резюме из хранилища, затем кандидата с контактами, источниками, откликами, историей, согласиями и
    /// подозрениями на дубль. Хранилище недоступно — ничего не удаляется (повтор позже), чтобы не остался файл без хозяина. В аудите — только
    /// причина и количество, без персональных данных.
    /// </summary>
    public async Task DeleteCandidateDataAsync(Guid id, string reason, Guid? actorId, CancellationToken ct)
    {
        var exists = await db.Candidates.AnyAsync(c => c.Id == id, ct);
        if (!exists)
        {
            throw new FieldNotFoundException("Кандидат не найден (возможно, его данные уже удалены).");
        }

        var keys = await db.CandidateResumes.AsNoTracking().Where(r => r.CandidateId == id).Select(r => r.ObjectKey).ToListAsync(ct);
        foreach (var key in keys)
        {
            await storage.DeleteAsync(key, ct);
        }

        var applications = await db.CandidateApplications.CountAsync(a => a.CandidateId == id, ct);

        // Переписка с кандидатом в Telegram (история, неотправленные сообщения), лиды и записи собеседований — тоже его данные.
        var conversations = await db.TelegramConversations.Where(c => c.RelatedType == CandidateRelation && c.RelatedId == id).Select(c => c.Id).ToListAsync(ct);
        if (conversations.Count > 0)
        {
            await db.TelegramOutgoingMessages.Where(m => m.ConversationId != null && conversations.Contains(m.ConversationId.Value)).ExecuteDeleteAsync(ct);
            await db.TelegramConversations.Where(c => conversations.Contains(c.Id)).ExecuteDeleteAsync(ct);
        }

        await db.TelegramLeads.Where(l => l.CandidateId == id).ExecuteDeleteAsync(ct);
        // Журнал ответов ИИ-агента (вопросы кандидата и ответы ему) — тоже его переписка.
        await db.DialogAgentReplies.Where(r => r.CandidateId == id || conversations.Contains(r.ConversationId)).ExecuteDeleteAsync(ct);
        await db.RecruitmentExceptions.Where(e => e.CandidateId == id).ExecuteDeleteAsync(ct);
        // События календаря собеседований — с именем кандидата в названии; удаляются вместе с его данными.
        var events = await db.Interviews.Where(i => i.CandidateId == id && i.CalendarEventId != null).Select(i => i.CalendarEventId!.Value).ToListAsync(ct);
        if (events.Count > 0)
        {
            await db.CalendarEvents.Where(e => events.Contains(e.Id)).ExecuteDeleteAsync(ct);
        }

        await db.Candidates.Where(c => c.Id == id).ExecuteDeleteAsync(ct);
        await audit.LogAsync(actorId is null ? ActorType.System : ActorType.User, actorId?.ToString() ?? "recruitment", "recruitment.candidate.deleted",
            nameof(Candidate), id.ToString(), new { reason, resumes = keys.Count, applications }, ct);
    }

    /// <summary>Не дубль: подозрение закрывается, кандидаты остаются разными.</summary>
    public async Task<CandidateCard> DismissDuplicateAsync(RecruitmentContext ctx, Guid duplicateId, Guid viewFor, CancellationToken ct)
    {
        RequireManage(ctx);
        var duplicate = await OpenDuplicateAsync(duplicateId, ct);
        duplicate.Status = CandidateDuplicateStatus.Dismissed;
        duplicate.ResolvedById = ctx.UserId;
        duplicate.ResolvedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await AuditAsync(ctx, "recruitment.duplicate.dismissed", nameof(Candidate), duplicate.CandidateId, new { other = duplicate.OtherCandidateId }, ct);
        return await CandidateAsync(ctx, viewFor, ct);
    }

    /// <summary>
    /// Объединить: кандидат <paramref name="keepId"/> остаётся, второй переносится в него — контакты (подтверждённость сохраняется), источники,
    /// резюме (одинаковые файлы — один раз), отклики (на ту же вакансию — остаётся отклик оставшегося), согласия, заметки; затем второй удаляется.
    /// </summary>
    public async Task<CandidateCard> MergeDuplicateAsync(RecruitmentContext ctx, Guid duplicateId, Guid keepId, CancellationToken ct)
    {
        RequireManage(ctx);
        var duplicate = await OpenDuplicateAsync(duplicateId, ct);
        if (keepId != duplicate.CandidateId && keepId != duplicate.OtherCandidateId)
        {
            throw new FieldValidationException("Оставить можно одного из двух кандидатов этого дубля.");
        }

        var dropId = keepId == duplicate.CandidateId ? duplicate.OtherCandidateId : duplicate.CandidateId;
        var keep = await db.Candidates.Include(c => c.Contacts).Include(c => c.Resumes).Include(c => c.Applications).FirstAsync(c => c.Id == keepId, ct);
        var drop = await db.Candidates.Include(c => c.Contacts).Include(c => c.Resumes).Include(c => c.Applications).FirstAsync(c => c.Id == dropId, ct);

        // 1. Убрать у второго то, что у оставшегося уже есть (иначе уникальные индексы не дадут перенести).
        var orphanKeys = new List<string>();
        foreach (var contact in drop.Contacts.ToList())
        {
            if (keep.Contacts.FirstOrDefault(x => x.Kind == contact.Kind && x.Value == contact.Value) is { } twin)
            {
                if (contact.Verified && !twin.Verified)
                {
                    twin.Verified = true;
                    twin.VerifiedAt = contact.VerifiedAt;
                }

                db.CandidateContacts.Remove(contact);
            }
        }

        foreach (var resume in drop.Resumes.Where(r => keep.Resumes.Any(k => k.Sha256 == r.Sha256)).ToList())
        {
            orphanKeys.Add(resume.ObjectKey);
            db.CandidateResumes.Remove(resume);
        }

        foreach (var application in drop.Applications.Where(a => keep.Applications.Any(k => k.VacancyId == a.VacancyId)).ToList())
        {
            db.CandidateApplications.Remove(application);
        }

        keep.City ??= drop.City;
        keep.Language ??= drop.Language;
        if (!string.IsNullOrEmpty(drop.Notes) && !(keep.Notes ?? string.Empty).Contains(drop.Notes, StringComparison.Ordinal))
        {
            keep.Notes = Clean(string.IsNullOrEmpty(keep.Notes) ? drop.Notes : $"{keep.Notes}\n{drop.Notes}", 4000);
        }

        keep.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);

        // 2. Перенести остальное и удалить второго (подозрения на дубль с ним — тоже; у оставшегося они найдутся заново).
        await db.CandidateDuplicates.Where(d => d.CandidateId == dropId || d.OtherCandidateId == dropId).ExecuteDeleteAsync(ct);
        await db.CandidateContacts.Where(x => x.CandidateId == dropId).ExecuteUpdateAsync(u => u.SetProperty(x => x.CandidateId, keepId), ct);
        await db.CandidateSources.Where(x => x.CandidateId == dropId).ExecuteUpdateAsync(u => u.SetProperty(x => x.CandidateId, keepId), ct);
        await db.CandidateResumes.Where(x => x.CandidateId == dropId).ExecuteUpdateAsync(u => u.SetProperty(x => x.CandidateId, keepId), ct);
        await db.CandidateApplications.Where(x => x.CandidateId == dropId).ExecuteUpdateAsync(u => u.SetProperty(x => x.CandidateId, keepId), ct);
        await db.CandidateConsents.Where(x => x.CandidateId == dropId).ExecuteUpdateAsync(u => u.SetProperty(x => x.CandidateId, keepId), ct);
        await db.Candidates.Where(c => c.Id == dropId).ExecuteDeleteAsync(ct);
        foreach (var key in orphanKeys)
        {
            try
            {
                await storage.DeleteAsync(key, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Подбор: файл одинакового резюме после объединения не удалён из хранилища");
            }
        }

        await DetectDuplicatesAsync(keepId, ct);
        await RefreshRetentionAsync(keepId, touch: true, ct);
        await AuditAsync(ctx, "recruitment.duplicate.merged", nameof(Candidate), keepId, new { merged = dropId }, ct);
        return await CandidateAsync(ctx, keepId, ct);
    }

    private async Task<CandidateDuplicate> OpenDuplicateAsync(Guid id, CancellationToken ct)
    {
        var duplicate = await db.CandidateDuplicates.FirstOrDefaultAsync(d => d.Id == id, ct) ?? throw new FieldNotFoundException("Подозрение на дубль не найдено.");
        return duplicate.Status == CandidateDuplicateStatus.Open
            ? duplicate
            : throw new FieldConflictException("По этому дублю уже приняли решение — обновите страницу.");
    }
}
