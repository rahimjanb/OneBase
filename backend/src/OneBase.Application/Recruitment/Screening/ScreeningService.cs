using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Recruitment.Documents;
using OneBase.Application.Recruitment.Hh;
using OneBase.Application.Recruitment.Jobs;
using OneBase.Domain.Audit;
using OneBase.Domain.Recruitment;

namespace OneBase.Application.Recruitment.Screening;

/// <summary>
/// HR → «Подбор» → «AI-скрининг»: панель (кандидаты, проанализировано, подходящие, рекомендованные, ожидают анализа), вакансии со статистикой,
/// список откликов с оценкой и фильтрами, статус скрининга (отметка HR + этап), анализ (сразу или очередью — платные вызовы AI только по
/// действию HR), ручной импорт резюме (файл, текст, ссылка на профиль — с дедупликацией), анализы кандидата с историей рейтинга, сохранённые
/// сравнения. Права: смотреть — «Подбор: просмотр», менять и запускать AI — «Подбор: управление».
/// </summary>
public sealed class ScreeningService(
    IAppDbContext db,
    RecruitmentService recruitment,
    RecruitmentJobQueue jobs,
    IEnumerable<ICandidateScorer> scorers,
    IAuditLogger audit,
    TimeProvider clock)
{
    /// <summary>Строк в списке — не больше (фильтры сужают).</summary>
    public const int MaxRows = 300;

    /// <summary>Анализов в очередь за одно нажатие — не больше (каждый — платный вызов AI).</summary>
    public const int MaxQueue = 50;

    public const int Suitable = 70;

    private static void Read(RecruitmentContext ctx)
    {
        if (!ctx.SeesCandidates)
        {
            throw new FieldForbiddenException("Нет доступа к подбору.");
        }
    }

    private static void Manage(RecruitmentContext ctx)
    {
        if (!ctx.CanManage)
        {
            throw new FieldForbiddenException("Это делает HR (право «Подбор персонала: управление»).");
        }
    }

    private sealed record AppRow(Guid Id, Guid CandidateId, Guid VacancyId, ApplicationStage Stage, ApplicationMark Mark, string Source, DateTimeOffset CreatedAt,
        DateTimeOffset? ClosedAt);

    private sealed record ScoreRow(Guid ApplicationId, int Overall, int Version, decimal? Confidence, DateTimeOffset CreatedAt, string Summary);

    private sealed record State(
        List<AppRow> Apps,
        Dictionary<Guid, ScoreRow> Latest,
        HashSet<Guid> Analyzed,
        Dictionary<Guid, DateTimeOffset> ResumeAt,
        HashSet<Guid> Analyzing,
        Dictionary<Guid, string> HhUrls,
        Dictionary<Guid, string> Failed);

    /// <summary>Отклики и всё, что нужно для статуса, оценки и «ожидает анализа» (одним набором запросов).</summary>
    private async Task<State> StateAsync(IReadOnlyCollection<Guid> vacancyIds, CancellationToken ct)
    {
        var apps = await db.CandidateApplications.AsNoTracking().Where(a => vacancyIds.Contains(a.VacancyId))
            .Select(a => new AppRow(a.Id, a.CandidateId, a.VacancyId, a.Stage, a.Mark, a.Source, a.CreatedAt, a.ClosedAt)).ToListAsync(ct);
        var appIds = apps.Select(a => a.Id).ToList();
        var candIds = apps.Select(a => a.CandidateId).Distinct().ToList();
        var scores = await db.CandidateScores.AsNoTracking().Where(s => appIds.Contains(s.ApplicationId))
            .Select(s => new ScoreRow(s.ApplicationId, s.Overall, s.Version, s.Confidence, s.CreatedAt, s.Summary)).ToListAsync(ct);
        var latest = scores.GroupBy(s => s.ApplicationId).ToDictionary(g => g.Key, g => g.MaxBy(s => s.CreatedAt)!);
        var hhAllowed = await db.HhConnections.AsNoTracking().Where(c => c.Id == 1).Select(c => c.AiOnHhData).FirstOrDefaultAsync(ct);
        var resumeAt = (await db.CandidateResumes.AsNoTracking()
                .Where(r => candIds.Contains(r.CandidateId) && r.Text != null && r.DocumentType != "id_document" && (hhAllowed || r.Source != "hh"))
                .GroupBy(r => r.CandidateId).Select(g => new { g.Key, At = g.Max(r => r.CreatedAt) }).ToListAsync(ct))
            .ToDictionary(x => x.Key, x => x.At);
        var queued = await db.RecruitmentJobs.AsNoTracking()
            .Where(j => j.Queue == RecruitmentQueues.Scoring && (j.Status == RecruitmentJobStatus.Pending || j.Status == RecruitmentJobStatus.Running) && appIds.Contains(j.RelatedId))
            .Select(j => j.RelatedId).ToListAsync(ct);
        var processing = await db.CandidateResumes.AsNoTracking()
            .Where(r => candIds.Contains(r.CandidateId) && (r.Status == DocumentStatus.Received || r.Status == DocumentStatus.Stored || r.Status == DocumentStatus.Processing))
            .Select(r => r.CandidateId).Distinct().ToListAsync(ct);
        var analyzing = queued.Concat(apps.Where(a => processing.Contains(a.CandidateId) && a.ClosedAt == null).Select(a => a.Id)).ToHashSet();
        var failed = (await db.RecruitmentJobs.AsNoTracking()
                .Where(j => j.Queue == RecruitmentQueues.Scoring && j.Status == RecruitmentJobStatus.Failed && appIds.Contains(j.RelatedId))
                .Select(j => new { j.RelatedId, j.FinishedAt, j.LastError }).ToListAsync(ct))
            .Where(j => !latest.TryGetValue(j.RelatedId, out var s) || j.FinishedAt > s.CreatedAt)
            .GroupBy(j => j.RelatedId).ToDictionary(g => g.Key, g => g.MaxBy(j => j.FinishedAt)!.LastError ?? "Анализ не выполнен.");
        var hh = await db.CandidateSources.AsNoTracking().Where(s => candIds.Contains(s.CandidateId) && s.Source == "hh")
            .Select(s => new { s.CandidateId, s.ExternalId, s.Note }).ToListAsync(ct);
        var urls = hh.GroupBy(s => s.CandidateId).ToDictionary(g => g.Key, g =>
        {
            var s = g.First();
            return HhLinks.IsSite(s.Note) ? s.Note! : HhLinks.Resume(s.ExternalId ?? string.Empty);
        });
        return new State(apps, latest, scores.Select(s => s.ApplicationId).ToHashSet(), resumeAt, analyzing, urls, failed);
    }

    /// <summary>Ждёт анализа: подбор идёт, есть текст резюме, анализа нет или резюме новее; сейчас не анализируется.</summary>
    private static bool IsPending(State s, AppRow a) =>
        a.ClosedAt == null && !s.Analyzing.Contains(a.Id) && s.ResumeAt.TryGetValue(a.CandidateId, out var at)
        && (!s.Latest.TryGetValue(a.Id, out var score) || score.CreatedAt < at);

    public async Task<ScreeningPage> PageAsync(RecruitmentContext ctx, ScreeningQuery q, CancellationToken ct)
    {
        Read(ctx);
        var vacancies = await db.Vacancies.AsNoTracking().Where(v => v.Status != VacancyStatus.Closed || v.Id == q.VacancyId)
            .Select(v => new { v.Id, v.Number, v.Title, v.Status, v.Requirements, v.Criteria, v.HhVacancyId, v.CreatedAt }).ToListAsync(ct);
        vacancies = vacancies.OrderBy(v => v.Status switch { VacancyStatus.Open => 0, VacancyStatus.Paused => 1, VacancyStatus.Draft => 2, _ => 3 })
            .ThenByDescending(v => v.CreatedAt).ToList();
        var state = await StateAsync(vacancies.Select(v => v.Id).ToList(), ct);
        var hhRow = await db.HhConnections.AsNoTracking().FirstOrDefaultAsync(c => c.Id == 1, ct);

        var panel = vacancies.Select(v =>
        {
            var apps = state.Apps.Where(a => a.VacancyId == v.Id).ToList();
            var scored = apps.Where(a => state.Latest.ContainsKey(a.Id)).Select(a => state.Latest[a.Id]).ToList();
            return new ScreeningVacancy(v.Id, $"V{v.Number}", v.Title, v.Status.ToString(), Cut(v.Requirements, 400), Cut(v.Criteria, 400), apps.Count, scored.Count,
                scored.Count(s => s.Overall >= Suitable), scored.Count == 0 ? null : Math.Round((decimal)scored.Average(s => s.Overall), 1),
                v.HhVacancyId is not null ? hhRow?.LastSyncAt : null, scored.Count == 0 ? null : scored.Max(s => s.CreatedAt), v.HhVacancyId is not null);
        }).ToList();

        var scope = q.VacancyId is { } vid ? state.Apps.Where(a => a.VacancyId == vid).ToList() : state.Apps;
        var kpis = new ScreeningKpis(scope.Select(a => a.CandidateId).Distinct().Count(), scope.Count(a => state.Analyzed.Contains(a.Id)),
            scope.Count(a => state.Latest.TryGetValue(a.Id, out var s) && s.Overall >= Suitable), scope.Count(a => a.Mark == ApplicationMark.Recommended && a.ClosedAt == null),
            scope.Count(a => IsPending(state, a)));

        var candIds = scope.Select(a => a.CandidateId).Distinct().ToList();
        var candidates = await db.Candidates.AsNoTracking().Where(c => candIds.Contains(c.Id))
            .Select(c => new { c.Id, c.FullName, c.City, c.DesiredPosition, c.ExperienceMonths, c.LastJob, c.SalaryExpectation, c.Education })
            .ToDictionaryAsync(c => c.Id, ct);
        var skills = (await db.CandidateSkills.AsNoTracking().Where(s => candIds.Contains(s.CandidateId)).OrderBy(s => s.Id)
                .Select(s => new { s.CandidateId, s.Name, s.Normalized }).ToListAsync(ct))
            .GroupBy(s => s.CandidateId).ToDictionary(g => g.Key, g => g.ToList());
        var codes = vacancies.ToDictionary(v => v.Id, v => (Code: $"V{v.Number}", v.Title));

        var rows = scope.Where(a => candidates.ContainsKey(a.CandidateId)).Select(a =>
        {
            var c = candidates[a.CandidateId];
            state.Latest.TryGetValue(a.Id, out var s);
            var analyzing = state.Analyzing.Contains(a.Id);
            return new
            {
                Row = new ScreeningRow(a.Id, a.CandidateId, c.FullName, c.DesiredPosition, c.City, c.ExperienceMonths, c.LastJob,
                    skills.GetValueOrDefault(a.CandidateId)?.Select(x => x.Name).Take(12).ToList() ?? [], c.SalaryExpectation, c.Education, a.VacancyId,
                    codes[a.VacancyId].Code, codes[a.VacancyId].Title, ScreeningRules.Status(a.Stage, a.Mark, analyzing), a.Stage.ToString(), a.Source, s?.Overall,
                    s is null ? null : ScreeningRules.Category(s.Overall), s?.Version, s?.Confidence, s?.CreatedAt, analyzing, IsPending(state, a),
                    s is null ? null : Cut(s.Summary, 400), state.HhUrls.GetValueOrDefault(a.CandidateId), a.CreatedAt, state.ResumeAt.ContainsKey(a.CandidateId),
                    state.Failed.GetValueOrDefault(a.Id)),
                Skills = skills.GetValueOrDefault(a.CandidateId)?.Select(x => x.Normalized).ToList() ?? [],
            };
        }).ToList();

        var cities = rows.Select(r => r.Row.City).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList();
        var filtered = rows.Where(r => q.Status is not { Length: > 0 } st || r.Row.Status == st)
            .Where(r => q.MinScore is not { } min || r.Row.Overall >= min)
            .Where(r => q.City is not { Length: > 0 } city || string.Equals(r.Row.City, city, StringComparison.OrdinalIgnoreCase))
            .Where(r => q.MinExperienceMonths is not { } exp || r.Row.ExperienceMonths >= exp)
            .Where(r => ScreeningRules.NormalizeSkill(q.Skill) is not { } skill || r.Skills.Any(x => x.Contains(skill, StringComparison.Ordinal)))
            .Where(r => q.Position?.Trim() is not { Length: > 0 } pos || Has(r.Row.DesiredPosition, pos) || Has(r.Row.LastJob, pos))
            .Where(r => q.Search?.Trim() is not { Length: > 0 } search || Has(r.Row.FullName, search))
            .Select(r => r.Row);
        filtered = q.Sort switch
        {
            "recent" => filtered.OrderByDescending(r => r.AppliedAt),
            "name" => filtered.OrderBy(r => r.FullName, StringComparer.CurrentCultureIgnoreCase),
            "experience" => filtered.OrderByDescending(r => r.ExperienceMonths ?? -1).ThenByDescending(r => r.Overall ?? -1),
            _ => filtered.OrderByDescending(r => r.Overall ?? -1).ThenByDescending(r => r.AppliedAt),
        };
        var list = filtered.ToList();

        var since = clock.GetUtcNow().AddDays(-30);
        var calls = await db.HhApiCalls.AsNoTracking().Where(c => c.At >= since).GroupBy(c => c.Allowed).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var lastBlock = await db.HhApiCalls.AsNoTracking().Where(c => !c.Allowed).OrderByDescending(c => c.Id).Select(c => c.Reason).FirstOrDefaultAsync(ct);
        var hh = new HhGuardBrief(hhRow?.ProtectedAccessToken is not null, hhRow?.Enabled ?? false, hhRow?.LastSyncAt, hhRow?.LastSyncOk,
            calls.Where(c => c.Key).Sum(c => c.Count), calls.Where(c => !c.Key).Sum(c => c.Count), hhRow?.GuardReason, hhRow?.GuardTrippedAt, lastBlock);
        return new ScreeningPage(kpis, panel, list.Take(MaxRows).ToList(), list.Count, cities, hh);
    }

    private static bool Has(string? text, string part) => text?.Contains(part, StringComparison.CurrentCultureIgnoreCase) == true;

    // ---------- Анализ ----------

    /// <summary>Анализы кандидата по откликам — последний и история рейтинга (с изменением к предыдущему).</summary>
    public async Task<IReadOnlyList<CandidateScreeningView>> CandidateAsync(RecruitmentContext ctx, Guid candidateId, CancellationToken ct)
    {
        Read(ctx);
        var vacancyIds = await db.CandidateApplications.AsNoTracking().Where(a => a.CandidateId == candidateId).Select(a => a.VacancyId).ToListAsync(ct);
        var state = await StateAsync(vacancyIds, ct);
        var apps = state.Apps.Where(a => a.CandidateId == candidateId).OrderBy(a => a.ClosedAt != null).ThenByDescending(a => a.CreatedAt).ToList();
        var appIds = apps.Select(a => a.Id).ToList();
        var rows = await db.CandidateScores.AsNoTracking().Where(s => appIds.Contains(s.ApplicationId)).OrderBy(s => s.CreatedAt).ToListAsync(ct);
        var vacancies = await db.Vacancies.AsNoTracking().Where(v => vacancyIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, ct);
        return apps.Select(a =>
        {
            var history = new List<ScreeningAnalysisView>();
            int? previous = null;
            foreach (var s in rows.Where(r => r.ApplicationId == a.Id))
            {
                history.Add(ScreeningJson.View(s, previous));
                previous = s.Overall;
            }

            history.Reverse();
            var v = vacancies[a.VacancyId];
            return new CandidateScreeningView(a.Id, a.VacancyId, v.Code, v.Title, ScreeningRules.Status(a.Stage, a.Mark, state.Analyzing.Contains(a.Id)),
                state.Analyzing.Contains(a.Id), IsPending(state, a), state.HhUrls.GetValueOrDefault(candidateId), history);
        }).ToList();
    }

    /// <summary>Проанализировать сейчас (кнопка HR; платный вызов AI) — результат сразу.</summary>
    public async Task<ScreeningAnalysisView> AnalyzeAsync(RecruitmentContext ctx, Guid applicationId, CancellationToken ct)
    {
        Manage(ctx);
        var scorer = scorers.FirstOrDefault() ?? throw new FieldValidationException("AI не подключён — анализ недоступен («Настройки → AI»).");
        var score = await scorer.ScoreAsync(ctx, applicationId, ct);
        var previous = await db.CandidateScores.AsNoTracking().Where(s => s.ApplicationId == applicationId && s.Id != score.Id).OrderByDescending(s => s.CreatedAt)
            .Select(s => (int?)s.Overall).FirstOrDefaultAsync(ct);
        var row = await db.CandidateScores.AsNoTracking().FirstAsync(s => s.Id == score.Id, ct);
        return ScreeningJson.View(row, previous);
    }

    /// <summary>
    /// В очередь: выбранные отклики или все ожидающие вакансии — не больше 50 за раз; без резюме и уже в очереди — пропускаются. Анализ из
    /// очереди кандидату ничего не отправляет (без приглашения на собеседование).
    /// </summary>
    public async Task<ScreeningQueueResult> QueueAsync(RecruitmentContext ctx, ScreeningQueueInput input, CancellationToken ct)
    {
        Manage(ctx);
        if (!scorers.Any())
        {
            throw new FieldValidationException("AI не подключён — анализ недоступен («Настройки → AI»).");
        }

        var ids = input.ApplicationIds?.Distinct().ToList() ?? [];
        var vacancyIds = ids.Count > 0
            ? await db.CandidateApplications.AsNoTracking().Where(a => ids.Contains(a.Id)).Select(a => a.VacancyId).Distinct().ToListAsync(ct)
            : input.VacancyId is { } v ? [v] : throw new FieldValidationException("Выберите кандидатов или вакансию.");
        var state = await StateAsync(vacancyIds, ct);
        var targets = ids.Count > 0 ? state.Apps.Where(a => ids.Contains(a.Id)).ToList() : state.Apps.Where(a => IsPending(state, a)).ToList();
        int queued = 0, skipped = 0;
        foreach (var a in targets)
        {
            if (queued >= MaxQueue || !state.ResumeAt.TryGetValue(a.CandidateId, out var at) || state.Analyzing.Contains(a.Id))
            {
                skipped++;
                continue;
            }

            if (await jobs.EnqueueAsync(RecruitmentQueues.Scoring, $"screen:{a.Id:N}:{at.UtcTicks}", a.Id, Documents.CandidateScoringJob.NoInvite, ct: ct))
            {
                queued++;
            }
            else
            {
                skipped++;
            }
        }

        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "recruitment.screening.queued", nameof(Vacancy), (input.VacancyId ?? Guid.Empty).ToString(),
            new { queued, skipped }, ct);
        var message = queued == 0
            ? "Нечего ставить в очередь: у выбранных нет текста резюме, они уже анализируются или проанализированы по последнему резюме."
            : $"В очереди на анализ: {queued}." + (skipped > 0 ? $" Пропущено: {skipped} (нет резюме, уже в очереди или предел {MaxQueue} за раз)." : string.Empty);
        return new ScreeningQueueResult(queued, skipped, message);
    }

    // ---------- Статус ----------

    /// <summary>
    /// Статус скрининга, выбранный HR: этап — через обычную смену этапа (история, срок хранения), отметка — событием отклика. «В резерве» —
    /// только с действующим согласием кандидата на обработку данных: кандидат в резерве хранится, пока его не уберут из резерва.
    /// </summary>
    public async Task<string> SetStatusAsync(RecruitmentContext ctx, Guid applicationId, ScreeningStatusInput input, CancellationToken ct)
    {
        Manage(ctx);
        var status = input.Status?.Trim().ToLowerInvariant() ?? string.Empty;
        if (status == "analyzing" || !ScreeningRules.StatusCodes.Contains(status))
        {
            throw new FieldValidationException("Статус: новый, подходит, рекомендован, на собеседовании, отказ, принят на работу или в резерве.");
        }

        var application = await db.CandidateApplications.AsNoTracking().FirstOrDefaultAsync(a => a.Id == applicationId, ct)
                          ?? throw new FieldNotFoundException("Отклик не найден.");
        if (status == "reserve" && !await db.CandidateConsents.AnyAsync(c => c.CandidateId == application.CandidateId && c.WithdrawnAt == null, ct))
        {
            throw new FieldValidationException("Для резерва нужно согласие кандидата на обработку данных — отметьте его в карточке кандидата.");
        }

        var (stage, mark) = ScreeningRules.Apply(status, application.Stage);
        if (stage is { } to)
        {
            await recruitment.ChangeStageAsync(ctx, applicationId, new StageInput(to, Clean(input.Comment, 1000) ?? $"AI-скрининг: {ScreeningRules.StatusName(status)}",
                input.RejectReason), ct);
        }

        var row = await db.CandidateApplications.FirstAsync(a => a.Id == applicationId, ct);
        if (row.Mark != mark)
        {
            var now = clock.GetUtcNow();
            db.ApplicationEvents.Add(new ApplicationEvent
            {
                ApplicationId = row.Id,
                FromStage = row.Stage,
                ToStage = row.Stage,
                Mark = mark,
                Comment = Clean(input.Comment, 1000) ?? (mark == ApplicationMark.None ? "Отметка снята" : $"AI-скрининг: {ScreeningRules.StatusName(status)}"),
                ActorId = ctx.UserId,
                At = now,
            });
            row.Mark = mark;
            row.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
            await recruitment.TouchCandidateAsync(row.CandidateId, ct);
        }

        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "recruitment.screening.status", nameof(CandidateApplication), applicationId.ToString(), new { status }, ct);
        return ScreeningRules.Status(row.Stage, row.Mark, false);
    }

    // ---------- Ручной импорт ----------

    /// <summary>
    /// Резюме файлом (PDF, DOCX…) на вакансию: тот же файл уже есть у кандидата — он и добавляется к вакансии (без второй карточки); иначе —
    /// новый кандидат (имя возьмётся из резюме после разбора) и документ в обработку: текст, разбор, анализ под вакансию — в фоне.
    /// </summary>
    public async Task<ScreeningImportResult> ImportFileAsync(RecruitmentContext ctx, Guid vacancyId, RecruitmentUpload upload, string? consent, CancellationToken ct)
    {
        Manage(ctx);
        if (upload.SizeBytes <= 0 || upload.SizeBytes > RecruitmentRules.MaxResumeBytes)
        {
            throw new FieldValidationException($"Файл пустой или больше {RecruitmentRules.MaxResumeBytes / 1024 / 1024} МБ.");
        }

        var name = ResumeDocumentProcessor.SafeFileName(upload.FileName);
        if (!RecruitmentRules.ResumeExtensions.Contains(Path.GetExtension(name)))
        {
            throw new FieldValidationException("Резюме — PDF, Word (DOCX, DOC), RTF, ODT, TXT или фото (JPG, PNG, WEBP).");
        }

        using var buffer = new MemoryStream();
        await upload.Content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        var title = Path.GetFileNameWithoutExtension(name);
        return await ImportAsync(ctx, vacancyId, bytes, name, upload.ContentType, $"Кандидат из файла {Cut(title, 150)}", consent, "file", ct);
    }

    /// <summary>Резюме текстом (вставленный законно полученный текст): как файл .txt.</summary>
    public async Task<ScreeningImportResult> ImportTextAsync(RecruitmentContext ctx, ScreeningTextImport input, CancellationToken ct)
    {
        Manage(ctx);
        var text = input.Text?.Trim() ?? string.Empty;
        if (text.Length < 50)
        {
            throw new FieldValidationException("Вставьте текст резюме (хотя бы несколько строк).");
        }

        var bytes = Encoding.UTF8.GetBytes(text.Length <= 100_000 ? text : text[..100_000]);
        var name = Clean(input.FullName, 200);
        return await ImportAsync(ctx, input.VacancyId, bytes, "Резюме (текст).txt", "text/plain", name ?? "Кандидат из текста резюме", input.ConsentChannel, "text", ct);
    }

    private async Task<ScreeningImportResult> ImportAsync(RecruitmentContext ctx, Guid vacancyId, byte[] bytes, string fileName, string? contentType, string fullName,
        string? consent, string kind, CancellationToken ct)
    {
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var existing = await db.CandidateResumes.AsNoTracking().Where(r => r.Sha256 == sha).Select(r => (Guid?)r.CandidateId).FirstOrDefaultAsync(ct);
        if (existing is { } known)
        {
            var attached = await recruitment.AttachVacancyAsync(known, vacancyId, "manual", ctx.UserId, ct);
            var app = await ApplicationIdAsync(known, vacancyId, ct);
            await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "recruitment.screening.imported", nameof(Candidate), known.ToString(), new { kind, matched = "file", attached }, ct);
            return new ScreeningImportResult(known, app, false, "file", attached
                ? "Это резюме уже есть в базе — кандидат добавлен к вакансии без второй карточки."
                : "Это резюме уже есть в базе, кандидат уже рассматривается на эту вакансию (или она закрыта).");
        }

        var intake = await recruitment.IntakeAsync(new CandidateIntake(fullName, null, null, null, [], "manual", null, "Импорт резюме (AI-скрининг)", vacancyId, consent),
            ctx.UserId, ct);
        await recruitment.AddResumeAsync(intake.CandidateId, new RecruitmentUpload(fileName, contentType, bytes.Length, new MemoryStream(bytes)), "manual", ctx.UserId, ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "recruitment.screening.imported", nameof(Candidate), intake.CandidateId.ToString(),
            new { kind, intake.Created, intake.MatchedBy }, ct);
        return new ScreeningImportResult(intake.CandidateId, await ApplicationIdAsync(intake.CandidateId, vacancyId, ct), intake.Created, intake.MatchedBy,
            "Резюме загружено: текст, разбор и анализ под вакансию — в фоне (платные вызовы AI), обычно минута-две.");
    }

    /// <summary>
    /// Кандидат по ссылке на профиль: ссылка HH — источник hh с ID резюме (тот же ID уже есть — тот же кандидат), другая ссылка — источник
    /// other. Резюме по ссылке не скачивается: через API HH просмотр расходует лимит, со сторонних сайтов — не разбираем страницы. HR загружает файл.
    /// </summary>
    public async Task<ScreeningImportResult> ImportLinkAsync(RecruitmentContext ctx, ScreeningLinkImport input, CancellationToken ct)
    {
        Manage(ctx);
        var url = input.Url?.Trim() ?? string.Empty;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new FieldValidationException("Ссылка на профиль — адрес https (например, https://hh.uz/resume/…).");
        }

        var resumeId = HhLinks.ResumeId(url);
        if (HhLinks.IsSite(url) && resumeId is null)
        {
            throw new FieldValidationException("Нужна ссылка на резюме HH — вида https://hh.uz/resume/…");
        }

        var name = Clean(input.FullName, 200);
        if (resumeId is null && name is null)
        {
            throw new FieldValidationException("Укажите имя кандидата.");
        }

        var intake = await recruitment.IntakeAsync(new CandidateIntake(name ?? $"Кандидат HH {resumeId![..Math.Min(8, resumeId!.Length)]}", null, null, null, [],
            resumeId is null ? "other" : "hh", resumeId, Cut(url, 500), input.VacancyId, input.ConsentChannel), ctx.UserId, ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "recruitment.screening.imported", nameof(Candidate), intake.CandidateId.ToString(),
            new { kind = "link", hh = resumeId is not null, intake.Created, intake.MatchedBy }, ct);
        var app = await ApplicationIdAsync(intake.CandidateId, input.VacancyId, ct);
        return new ScreeningImportResult(intake.CandidateId, app, intake.Created, intake.MatchedBy, intake.Created
            ? "Кандидат добавлен. Резюме по ссылке не загружается автоматически: откройте профиль, скачайте резюме и загрузите файлом — затем AI его проанализирует."
            : "Этот профиль уже есть в базе — открыта существующая карточка.");
    }

    private Task<Guid?> ApplicationIdAsync(Guid candidateId, Guid vacancyId, CancellationToken ct) =>
        db.CandidateApplications.AsNoTracking().Where(a => a.CandidateId == candidateId && a.VacancyId == vacancyId).Select(a => (Guid?)a.Id).FirstOrDefaultAsync(ct);

    // ---------- Сравнения ----------

    public async Task<IReadOnlyList<ComparisonBrief>> ComparisonsAsync(RecruitmentContext ctx, Guid vacancyId, CancellationToken ct)
    {
        Read(ctx);
        var rows = await db.CandidateComparisons.AsNoTracking().Where(c => c.VacancyId == vacancyId).OrderByDescending(c => c.CreatedAt).Take(20).ToListAsync(ct);
        return rows.Select(r => ComparisonView(r)).OfType<ComparisonView>().Select(v => new ComparisonBrief(v.Id, v.CreatedAt,
            v.Candidates.Select(c => c.FullName).ToList(), v.Candidates.FirstOrDefault(c => c.ApplicationId == v.BestApplicationId)?.FullName)).ToList();
    }

    public async Task<ComparisonView> ComparisonAsync(RecruitmentContext ctx, Guid id, CancellationToken ct)
    {
        Read(ctx);
        var row = await db.CandidateComparisons.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new FieldNotFoundException("Сравнение не найдено.");
        return ComparisonView(row) ?? throw new FieldNotFoundException("Сравнение повреждено.");
    }

    private static ComparisonView? ComparisonView(CandidateComparison row)
    {
        try
        {
            return JsonSerializer.Deserialize<ComparisonView>(row.ResultJson, ScreeningJson.Options) is { } v ? v with { Id = row.Id, CreatedAt = row.CreatedAt } : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Clean(string? s, int max) => s?.Trim() is { Length: > 0 } t ? (t.Length <= max ? t : t[..max]) : null;

    private static string? Cut(string? s, int max) => s is null ? null : s.Length <= max ? s : s[..max];
}
