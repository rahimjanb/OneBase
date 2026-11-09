using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Security;
using OneBase.Application.Work;
using OneBase.Domain.Audit;
using OneBase.Domain.Recruitment;
using OneBase.Domain.Work;

namespace OneBase.Application.Recruitment;

/// <summary>
/// Подбор персонала: заявки на сотрудников, вакансии, единая база кандидатов (RecruitmentService.Candidates.cs), настройки и срок
/// хранения. Права проверяются здесь (<see cref="RecruitmentContext"/>): нет права — 403, чужое или удалённое — 404. Изменения — в аудит
/// (<c>recruitment.*</c>, без персональных данных кандидата).
/// </summary>
public sealed partial class RecruitmentService(
    IAppDbContext db,
    IFileStorage storage,
    IDocumentTextExtractor extractor,
    IEnumerable<IDocumentVisionReader> vision,
    UserNotifier notifier,
    RecruitmentExceptions exceptions,
    IAuditLogger audit,
    TimeProvider clock,
    ILogger<RecruitmentService> logger)
{
    public async Task<RecruitmentContext> ContextAsync(Guid userId, IReadOnlySet<string> permissions, CancellationToken ct)
    {
        var heads = await db.WorkDepartmentHeads.AsNoTracking().Where(h => h.UserId == userId).Select(h => h.DepartmentId).ToListAsync(ct);
        return new RecruitmentContext(userId, permissions.Contains(Permissions.HrRecruitmentRead), permissions.Contains(Permissions.HrRecruitmentManage), heads);
    }

    public async Task<RecruitmentContextView> ContextViewAsync(RecruitmentContext ctx, CancellationToken ct)
    {
        if (!ctx.SeesRequests)
        {
            throw new FieldForbiddenException("Нет доступа к подбору персонала.");
        }

        var departments = await db.Departments.AsNoTracking().OrderBy(d => d.Name).Select(d => new RecruitmentOption(d.Id, d.Name, d.Code)).ToListAsync(ct);
        var recruiters = ctx.SeesCandidates ? await RecruitersAsync(ct) : [];
        return new RecruitmentContextView(ctx.CanRead || ctx.CanManage, ctx.CanManage, ctx.CanRequest,
            ctx.CanManage ? departments : departments.Where(d => ctx.HeadOf.Contains(d.Id)).ToList(),
            ctx.SeesCandidates ? departments : [], recruiters, (await SettingsRowAsync(ct)).RetentionDays);
    }

    private async Task<List<RecruitmentOption>> RecruitersAsync(CancellationToken ct)
    {
        var ids = await db.ActiveUsersWithAsync(Permissions.HrRecruitmentManage, ct);
        return await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).OrderBy(u => u.FullName)
            .Select(u => new RecruitmentOption(u.Id, u.FullName)).ToListAsync(ct);
    }

    private static void RequireRead(RecruitmentContext ctx)
    {
        if (!ctx.SeesCandidates)
        {
            throw new FieldForbiddenException("Нет доступа к подбору: нужно право «Подбор персонала: просмотр».");
        }
    }

    private static void RequireManage(RecruitmentContext ctx)
    {
        if (!ctx.CanManage)
        {
            throw new FieldForbiddenException("Менять данные подбора может только HR (право «Подбор персонала: управление»).");
        }
    }

    private async Task<Dictionary<Guid, string>> NamesAsync(IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var set = ids.Where(i => i is not null).Select(i => i!.Value).Distinct().ToList();
        return set.Count == 0 ? [] : await db.Users.AsNoTracking().Where(u => set.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    private static string? Clean(string? value, int max)
    {
        var v = value?.Trim();
        return string.IsNullOrEmpty(v) ? null : v.Length <= max ? v : v[..max];
    }

    private static string Required(string? value, int max, string what) =>
        Clean(value, max) ?? throw new FieldValidationException($"Укажите {what}.");

    private static (decimal? From, decimal? To) Salary(decimal? from, decimal? to)
    {
        if (from is < 0 || to is < 0)
        {
            throw new FieldValidationException("Зарплата не может быть отрицательной.");
        }

        if (from is { } f && to is { } t && f > t)
        {
            throw new FieldValidationException("Зарплата «от» больше, чем «до».");
        }

        return (from, to);
    }

    private static int Headcount(int value) =>
        value is >= 1 and <= 100 ? value : throw new FieldValidationException("Количество — от 1 до 100 человек.");

    private Task AuditAsync(RecruitmentContext ctx, string action, string entityType, Guid id, object? data, CancellationToken ct) =>
        audit.LogAsync(ActorType.User, ctx.UserId.ToString(), action, entityType, id.ToString(), data, ct);

    // ---------- Настройки и согласие ----------

    private async Task<RecruitmentSettings> SettingsRowAsync(CancellationToken ct)
    {
        var settings = await db.RecruitmentSettings.FirstOrDefaultAsync(s => s.Id == 1, ct);
        if (settings is null)
        {
            settings = new RecruitmentSettings();
            db.RecruitmentSettings.Add(settings);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                db.RecruitmentSettings.Remove(settings);
                settings = await db.RecruitmentSettings.FirstAsync(s => s.Id == 1, ct);
            }
        }

        return settings;
    }

    /// <summary>Действующий текст согласия (последняя версия); нет ни одной — создаётся версия 1 по умолчанию.</summary>
    private async Task<RecruitmentConsentText> ConsentTextAsync(CancellationToken ct)
    {
        var text = await db.RecruitmentConsentTexts.OrderByDescending(t => t.Version).FirstOrDefaultAsync(ct);
        if (text is not null)
        {
            return text;
        }

        text = new RecruitmentConsentText { Version = 1, Text = RecruitmentRules.DefaultConsentText, CreatedAt = clock.GetUtcNow() };
        db.RecruitmentConsentTexts.Add(text);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.RecruitmentConsentTexts.Remove(text);
            text = await db.RecruitmentConsentTexts.OrderByDescending(t => t.Version).FirstAsync(ct);
        }

        return text;
    }

    public async Task<int> RetentionDaysAsync(CancellationToken ct) => (await SettingsRowAsync(ct)).RetentionDays;

    public async Task<RecruitmentSettingsView> SettingsAsync(RecruitmentContext ctx, CancellationToken ct)
    {
        RequireRead(ctx);
        var settings = await SettingsRowAsync(ct);
        var text = await ConsentTextAsync(ct);
        return new RecruitmentSettingsView(settings.RetentionDays, text.Version, text.Text, settings.UpdatedAt);
    }

    /// <summary>Срок хранения и текст согласия. Новый текст — новая версия (у данных согласий остаётся версия, на которую согласились).</summary>
    public async Task<RecruitmentSettingsView> SaveSettingsAsync(RecruitmentContext ctx, RecruitmentSettingsInput input, CancellationToken ct)
    {
        RequireManage(ctx);
        if (input.RetentionDays is < RecruitmentRules.MinRetentionDays or > RecruitmentRules.MaxRetentionDays)
        {
            throw new FieldValidationException($"Срок хранения — от {RecruitmentRules.MinRetentionDays} до {RecruitmentRules.MaxRetentionDays} дней.");
        }

        var consent = Required(input.ConsentText, 8000, "текст согласия");
        var settings = await SettingsRowAsync(ct);
        var current = await ConsentTextAsync(ct);
        var before = settings.RetentionDays;
        settings.RetentionDays = input.RetentionDays;
        settings.UpdatedAt = clock.GetUtcNow();
        settings.UpdatedById = ctx.UserId;
        var newVersion = consent != current.Text;
        if (newVersion)
        {
            db.RecruitmentConsentTexts.Add(new RecruitmentConsentText { Version = current.Version + 1, Text = consent, CreatedAt = clock.GetUtcNow(), CreatedById = ctx.UserId });
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "recruitment.settings.saved", nameof(RecruitmentSettings), "1",
            new { retentionBefore = before, retentionAfter = input.RetentionDays, consentVersion = newVersion ? current.Version + 1 : current.Version }, ct);
        return await SettingsAsync(ctx, ct);
    }

    // ---------- Заявки на сотрудника ----------

    public async Task<IReadOnlyList<HiringRequestView>> RequestsAsync(RecruitmentContext ctx, HiringRequestStatus? status, CancellationToken ct)
    {
        if (!ctx.SeesRequests)
        {
            throw new FieldForbiddenException("Нет доступа к заявкам на сотрудников.");
        }

        var query = db.HiringRequests.AsNoTracking().Include(r => r.Department).AsQueryable();
        if (!ctx.SeesCandidates)
        {
            query = query.Where(r => ctx.HeadOf.Contains(r.DepartmentId) || r.CreatedById == ctx.UserId);
        }

        if (status is { } s)
        {
            query = query.Where(r => r.Status == s);
        }

        var rows = await query.OrderByDescending(r => r.CreatedAt).Take(500).ToListAsync(ct);
        return await RequestViewsAsync(ctx, rows, ct);
    }

    private async Task<List<HiringRequestView>> RequestViewsAsync(RecruitmentContext ctx, List<HiringRequest> rows, CancellationToken ct)
    {
        var names = await NamesAsync(rows.SelectMany(r => new[] { (Guid?)r.CreatedById, r.DecidedById }), ct);
        var vacancyIds = rows.Where(r => r.VacancyId != null).Select(r => r.VacancyId!.Value).ToList();
        var codes = vacancyIds.Count == 0 ? [] : await db.Vacancies.AsNoTracking().Where(v => vacancyIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => $"V{v.Number}", ct);
        return rows.Select(r => new HiringRequestView(r.Id, r.Number, r.DepartmentId, r.Department?.Name ?? "—", r.Position, r.Headcount, r.Reason,
            r.Requirements, r.SalaryFrom, r.SalaryTo, r.DesiredStart, r.Status.ToString(), names.GetValueOrDefault(r.CreatedById), r.CreatedAt,
            r.DecidedById is { } d ? names.GetValueOrDefault(d) : null, r.DecidedAt, r.DecisionComment, r.VacancyId,
            r.VacancyId is { } v ? codes.GetValueOrDefault(v) : null,
            r.Status == HiringRequestStatus.Submitted && (r.CreatedById == ctx.UserId || ctx.CanManage),
            r.Status == HiringRequestStatus.Submitted && ctx.CanManage)).ToList();
    }

    private async Task<HiringRequest> RequestAsync(RecruitmentContext ctx, Guid id, CancellationToken ct)
    {
        var request = await db.HiringRequests.Include(r => r.Department).FirstOrDefaultAsync(r => r.Id == id, ct)
                      ?? throw new FieldNotFoundException("Заявка не найдена.");
        if (!ctx.SeesCandidates && !ctx.HeadOf.Contains(request.DepartmentId) && request.CreatedById != ctx.UserId)
        {
            throw new FieldNotFoundException("Заявка не найдена.");
        }

        return request;
    }

    /// <summary>Подать заявку: руководитель — за свой отдел, HR — за любой. HR получает уведомление.</summary>
    public async Task<HiringRequestView> CreateRequestAsync(RecruitmentContext ctx, HiringRequestInput input, CancellationToken ct)
    {
        if (!ctx.CanRequest)
        {
            throw new FieldForbiddenException("Заявку на сотрудника подаёт руководитель отдела или HR.");
        }

        if (!ctx.CanManage && !ctx.HeadOf.Contains(input.DepartmentId))
        {
            throw new FieldForbiddenException("Заявку можно подать только за свой отдел.");
        }

        var department = await db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == input.DepartmentId, ct)
                         ?? throw new FieldValidationException("Выберите отдел.");
        var (from, to) = Salary(input.SalaryFrom, input.SalaryTo);
        var request = new HiringRequest
        {
            DepartmentId = department.Id,
            Position = Required(input.Position, 200, "должность"),
            Headcount = Headcount(input.Headcount),
            Reason = Clean(input.Reason, 1000),
            Requirements = Clean(input.Requirements, 4000),
            SalaryFrom = from,
            SalaryTo = to,
            DesiredStart = input.DesiredStart,
            CreatedById = ctx.UserId,
        };
        db.HiringRequests.Add(request);
        await db.SaveChangesAsync(ct);

        foreach (var recipient in (await db.ActiveUsersWithAsync(Permissions.HrRecruitmentManage, ct)).Where(u => u != ctx.UserId))
        {
            notifier.Notify(recipient, UserNotificationKind.HiringRequest, $"Заявка на сотрудника № {request.Number}: {request.Position}",
                $"{department.Name}, {request.Headcount} чел.", "/hr/recruitment/requests");
        }

        await db.SaveChangesAsync(ct);
        await AuditAsync(ctx, "recruitment.request.created", nameof(HiringRequest), request.Id,
            new { request.Number, department = department.Code, request.Position, request.Headcount }, ct);
        return (await RequestViewsAsync(ctx, [request], ct))[0];
    }

    public async Task<HiringRequestView> CancelRequestAsync(RecruitmentContext ctx, Guid id, CancellationToken ct)
    {
        var request = await RequestAsync(ctx, id, ct);
        if (request.CreatedById != ctx.UserId && !ctx.CanManage)
        {
            throw new FieldForbiddenException("Отозвать заявку может её автор или HR.");
        }

        EnsureSubmitted(request);
        request.Status = HiringRequestStatus.Cancelled;
        request.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await AuditAsync(ctx, "recruitment.request.cancelled", nameof(HiringRequest), request.Id, new { request.Number }, ct);
        return (await RequestViewsAsync(ctx, [request], ct))[0];
    }

    /// <summary>Одобрить: открывается черновик вакансии с данными заявки (HR дополняет и публикует), автор получает уведомление.</summary>
    public async Task<HiringRequestView> ApproveRequestAsync(RecruitmentContext ctx, Guid id, DecisionInput input, CancellationToken ct)
    {
        RequireManage(ctx);
        var request = await RequestAsync(ctx, id, ct);
        EnsureSubmitted(request);
        var vacancy = new Vacancy
        {
            Title = request.Position,
            DepartmentId = request.DepartmentId,
            HiringRequestId = request.Id,
            Requirements = request.Requirements,
            SalaryFrom = request.SalaryFrom,
            SalaryTo = request.SalaryTo,
            Headcount = request.Headcount,
            RecruiterId = ctx.UserId,
            CreatedById = ctx.UserId,
        };
        db.Vacancies.Add(vacancy);
        request.Status = HiringRequestStatus.Approved;
        request.DecidedById = ctx.UserId;
        request.DecidedAt = clock.GetUtcNow();
        request.DecisionComment = Clean(input.Comment, 1000);
        request.VacancyId = vacancy.Id;
        await db.SaveChangesAsync(ct);
        notifier.Notify(request.CreatedById, UserNotificationKind.HiringRequestStatus, $"Заявка № {request.Number} одобрена: {request.Position}",
            $"Открыта вакансия V{vacancy.Number}." + (request.DecisionComment is { } c ? $" {c}" : string.Empty), "/hr/recruitment/requests");
        await db.SaveChangesAsync(ct);
        await AuditAsync(ctx, "recruitment.request.approved", nameof(HiringRequest), request.Id, new { request.Number, vacancy = vacancy.Code }, ct);
        return (await RequestViewsAsync(ctx, [request], ct))[0];
    }

    public async Task<HiringRequestView> RejectRequestAsync(RecruitmentContext ctx, Guid id, DecisionInput input, CancellationToken ct)
    {
        RequireManage(ctx);
        var comment = Required(input.Comment, 1000, "причину отказа");
        var request = await RequestAsync(ctx, id, ct);
        EnsureSubmitted(request);
        request.Status = HiringRequestStatus.Rejected;
        request.DecidedById = ctx.UserId;
        request.DecidedAt = clock.GetUtcNow();
        request.DecisionComment = comment;
        await db.SaveChangesAsync(ct);
        notifier.Notify(request.CreatedById, UserNotificationKind.HiringRequestStatus, $"Заявка № {request.Number} отклонена: {request.Position}",
            request.DecisionComment, "/hr/recruitment/requests");
        await db.SaveChangesAsync(ct);
        await AuditAsync(ctx, "recruitment.request.rejected", nameof(HiringRequest), request.Id, new { request.Number }, ct);
        return (await RequestViewsAsync(ctx, [request], ct))[0];
    }

    private static void EnsureSubmitted(HiringRequest request)
    {
        if (request.Status != HiringRequestStatus.Submitted)
        {
            throw new FieldConflictException("По заявке уже принято решение или она отозвана — обновите страницу.");
        }
    }

    // ---------- Вакансии ----------

    public async Task<IReadOnlyList<VacancyRow>> VacanciesAsync(RecruitmentContext ctx, VacancyStatus? status, string? search, CancellationToken ct)
    {
        RequireRead(ctx);
        var query = db.Vacancies.AsNoTracking().Include(v => v.Department).AsQueryable();
        if (status is { } s)
        {
            query = query.Where(v => v.Status == s);
        }

        if (Clean(search, 100) is { } q)
        {
            var lower = q.ToLower();
            query = query.Where(v => v.Title.ToLower().Contains(lower) || (v.City != null && v.City.ToLower().Contains(lower)));
        }

        var rows = await query.OrderBy(v => v.Status == VacancyStatus.Open ? 0 : v.Status == VacancyStatus.Draft ? 1 : v.Status == VacancyStatus.Paused ? 2 : 3)
            .ThenByDescending(v => v.CreatedAt).Take(500).ToListAsync(ct);
        var ids = rows.Select(v => v.Id).ToList();
        var stages = await db.CandidateApplications.AsNoTracking().Where(a => ids.Contains(a.VacancyId))
            .GroupBy(a => new { a.VacancyId, a.Stage }).Select(g => new { g.Key.VacancyId, g.Key.Stage, Count = g.Count() }).ToListAsync(ct);
        var names = await NamesAsync(rows.Select(v => v.RecruiterId), ct);
        return rows.Select(v => new VacancyRow(v.Id, v.Code, v.Title, v.Department?.Name ?? "—", v.Status.ToString(), v.Headcount, v.SalaryFrom, v.SalaryTo, v.City,
            v.RecruiterId is { } r ? names.GetValueOrDefault(r) : null,
            stages.Where(x => x.VacancyId == v.Id && !RecruitmentRules.IsClosed(x.Stage)).Sum(x => x.Count),
            stages.Where(x => x.VacancyId == v.Id && x.Stage == ApplicationStage.Hired).Sum(x => x.Count),
            v.CreatedAt, v.OpenedAt)).ToList();
    }

    public async Task<VacancyView> VacancyAsync(RecruitmentContext ctx, Guid id, CancellationToken ct)
    {
        RequireRead(ctx);
        var v = await db.Vacancies.AsNoTracking().Include(x => x.Department).FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new FieldNotFoundException("Вакансия не найдена.");
        var applications = await db.CandidateApplications.AsNoTracking().Where(a => a.VacancyId == id).Include(a => a.Candidate)
            .OrderBy(a => a.Stage).ThenByDescending(a => a.StageChangedAt).ToListAsync(ct);
        var candidateIds = applications.Select(a => a.CandidateId).ToList();
        var phones = await db.CandidateContacts.AsNoTracking().Where(c => candidateIds.Contains(c.CandidateId) && c.Kind == CandidateContactKind.Phone)
            .GroupBy(c => c.CandidateId).Select(g => new { g.Key, Value = g.OrderByDescending(c => c.Verified).Select(c => c.Value).First() })
            .ToDictionaryAsync(x => x.Key, x => x.Value, ct);
        var names = await NamesAsync([v.RecruiterId], ct);
        var request = v.HiringRequestId is { } rid ? await db.HiringRequests.AsNoTracking().Where(r => r.Id == rid).Select(r => (int?)r.Number).FirstOrDefaultAsync(ct) : null;
        return new VacancyView(v.Id, v.Code, v.Title, v.DepartmentId, v.Department?.Name ?? "—", v.Description, v.Requirements, v.Conditions, v.SalaryFrom,
            v.SalaryTo, v.City, v.EmploymentType, v.Headcount, v.Status.ToString(), v.RecruiterId, v.RecruiterId is { } r ? names.GetValueOrDefault(r) : null,
            v.HiringRequestId, request, v.CreatedAt, v.OpenedAt, v.ClosedAt,
            Enum.GetValues<ApplicationStage>().ToDictionary(s => s.ToString(), s => applications.Count(a => a.Stage == s)),
            applications.Select(a => new VacancyApplicationRow(a.Id, a.CandidateId, a.Candidate?.FullName ?? "—", a.Stage.ToString(), a.Source, a.StageChangedAt,
                phones.GetValueOrDefault(a.CandidateId))).ToList(), v.AiRecruiter, v.Criteria, v.HhVacancyId, v.AgentId,
            v.ScreeningQuestions);
    }

    public async Task<VacancyView> CreateVacancyAsync(RecruitmentContext ctx, VacancyInput input, CancellationToken ct)
    {
        RequireManage(ctx);
        var vacancy = new Vacancy { Title = "—", DepartmentId = input.DepartmentId, CreatedById = ctx.UserId };
        await ApplyAsync(vacancy, input, ct);
        db.Vacancies.Add(vacancy);
        await db.SaveChangesAsync(ct);
        await AuditAsync(ctx, "recruitment.vacancy.created", nameof(Vacancy), vacancy.Id, new { vacancy.Code, vacancy.Title }, ct);
        return await VacancyAsync(ctx, vacancy.Id, ct);
    }

    public async Task<VacancyView> UpdateVacancyAsync(RecruitmentContext ctx, Guid id, VacancyInput input, CancellationToken ct)
    {
        RequireManage(ctx);
        var vacancy = await db.Vacancies.FirstOrDefaultAsync(v => v.Id == id, ct) ?? throw new FieldNotFoundException("Вакансия не найдена.");
        await ApplyAsync(vacancy, input, ct);
        vacancy.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await AuditAsync(ctx, "recruitment.vacancy.updated", nameof(Vacancy), vacancy.Id, new { vacancy.Code, vacancy.Title }, ct);
        return await VacancyAsync(ctx, vacancy.Id, ct);
    }

    private async Task ApplyAsync(Vacancy vacancy, VacancyInput input, CancellationToken ct)
    {
        if (!await db.Departments.AnyAsync(d => d.Id == input.DepartmentId, ct))
        {
            throw new FieldValidationException("Выберите отдел.");
        }

        var employment = Clean(input.EmploymentType, 16);
        if (employment is not null && !RecruitmentRules.EmploymentTypes.Contains(employment))
        {
            throw new FieldValidationException("Тип занятости: полная, частичная, сменная, удалённая или временная.");
        }

        if (input.RecruiterId is { } recruiter && !(await db.ActiveUsersWithAsync(Permissions.HrRecruitmentManage, ct)).Contains(recruiter))
        {
            throw new FieldValidationException("Рекрутер — сотрудник с правом «Подбор персонала: управление».");
        }

        var (from, to) = Salary(input.SalaryFrom, input.SalaryTo);
        vacancy.Title = Required(input.Title, 200, "название вакансии");
        vacancy.DepartmentId = input.DepartmentId;
        vacancy.Description = Clean(input.Description, 8000);
        vacancy.Requirements = Clean(input.Requirements, 4000);
        vacancy.Conditions = Clean(input.Conditions, 4000);
        vacancy.SalaryFrom = from;
        vacancy.SalaryTo = to;
        vacancy.City = Clean(input.City, 100);
        vacancy.EmploymentType = employment;
        vacancy.Headcount = Headcount(input.Headcount);
        vacancy.RecruiterId = input.RecruiterId;
    }

    /// <summary>
    /// Открыть, приостановить, закрыть вакансию. Закрытие: кандидаты, у которых подбор по ней ещё идёт, получают отказ «вакансия закрыта» —
    /// иначе их данные хранились бы бессрочно.
    /// </summary>
    public async Task<VacancyView> SetVacancyStatusAsync(RecruitmentContext ctx, Guid id, VacancyStatus status, CancellationToken ct)
    {
        RequireManage(ctx);
        var vacancy = await db.Vacancies.FirstOrDefaultAsync(v => v.Id == id, ct) ?? throw new FieldNotFoundException("Вакансия не найдена.");
        if (vacancy.Status == status)
        {
            return await VacancyAsync(ctx, id, ct);
        }

        var now = clock.GetUtcNow();
        var before = vacancy.Status;
        vacancy.Status = status;
        vacancy.UpdatedAt = now;
        if (status == VacancyStatus.Open)
        {
            vacancy.OpenedAt ??= now;
            vacancy.ClosedAt = null;
        }

        var closed = 0;
        if (status == VacancyStatus.Closed)
        {
            vacancy.ClosedAt = now;
            var open = await db.CandidateApplications.Where(a => a.VacancyId == id).ToListAsync(ct);
            foreach (var application in open.Where(a => !RecruitmentRules.IsClosed(a.Stage)))
            {
                Stage(application, ApplicationStage.Rejected, "Вакансия закрыта", "Вакансия закрыта", ctx.UserId, now);
                closed++;
            }
        }

        await db.SaveChangesAsync(ct);
        foreach (var candidateId in await db.CandidateApplications.AsNoTracking().Where(a => a.VacancyId == id).Select(a => a.CandidateId).Distinct().ToListAsync(ct))
        {
            await RefreshRetentionAsync(candidateId, touch: false, ct);
        }

        await AuditAsync(ctx, "recruitment.vacancy.status", nameof(Vacancy), vacancy.Id, new { vacancy.Code, from = before.ToString(), to = status.ToString(), closed }, ct);
        return await VacancyAsync(ctx, id, ct);
    }
}
