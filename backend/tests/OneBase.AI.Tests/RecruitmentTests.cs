using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Recruitment;
using OneBase.Domain.Identity;
using OneBase.Domain.Recruitment;
using OneBase.Domain.Work;
using OneBase.Infrastructure.Persistence;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>Правила подбора без базы: нормализация контактов, срок хранения.</summary>
public class RecruitmentRulesTests
{
    [Theory]
    [InlineData(CandidateContactKind.Phone, "+998 90 123-45-67", "+998901234567")]
    [InlineData(CandidateContactKind.Phone, "90 123 45 67", "+998901234567")]
    [InlineData(CandidateContactKind.Phone, "998901234567", "+998901234567")]
    [InlineData(CandidateContactKind.Phone, "+7 (999) 111-22-33", "+79991112233")]
    [InlineData(CandidateContactKind.Phone, "123", null)]
    [InlineData(CandidateContactKind.Email, " Ivan.Petrov@Mail.COM ", "ivan.petrov@mail.com")]
    [InlineData(CandidateContactKind.Email, "ivan@", null)]
    [InlineData(CandidateContactKind.TelegramUsername, "@Ivan_Petrov", "ivan_petrov")]
    [InlineData(CandidateContactKind.TelegramUsername, "https://t.me/ivan_petrov/", "ivan_petrov")]
    [InlineData(CandidateContactKind.TelegramUsername, "@abc", null)]
    [InlineData(CandidateContactKind.Telegram, "123456789", "123456789")]
    [InlineData(CandidateContactKind.Telegram, "-5", null)]
    public void Contacts_are_normalized(CandidateContactKind kind, string raw, string? expected) =>
        Assert.Equal(expected, RecruitmentRules.Normalize(kind, raw));

    [Fact]
    public void Retention_starts_only_when_every_application_is_closed()
    {
        var activity = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);
        var closed = activity.AddDays(2);
        Assert.Equal(activity, RecruitmentRules.RetentionFrom([], activity));
        Assert.Null(RecruitmentRules.RetentionFrom([(ApplicationStage.Rejected, closed), (ApplicationStage.Interview, null)], activity));
        Assert.Equal(closed, RecruitmentRules.RetentionFrom([(ApplicationStage.Rejected, closed), (ApplicationStage.Hired, activity.AddDays(1))], activity));
        Assert.Equal(closed.AddDays(7), RecruitmentRules.DeleteAt(closed, 7));
        Assert.Null(RecruitmentRules.DeleteAt(null, 7));
    }
}

/// <summary>
/// Подбор на PostgreSQL: заявки на сотрудника и права руководителей, вакансии и этапы, единая база кандидатов с дедупликацией (ID источника,
/// подтверждённые контакты, подозрения на дубль и объединение), резюме с текстом из PDF, согласия, срок хранения и удаление данных.
/// </summary>
public class RecruitmentTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private sealed record Setup(Guid Hr, Guid Head, Guid Stranger, Guid Sales, Guid Production);

    private async Task<(IServiceScope Scope, RecruitmentService Service, OneBaseDbContext Db, Setup S)> StartAsync()
    {
        await fixture.ResetAsync();
        var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await db.CandidateDuplicates.ExecuteDeleteAsync();
        await db.Candidates.ExecuteDeleteAsync();
        await db.Vacancies.ExecuteDeleteAsync();
        await db.HiringRequests.ExecuteDeleteAsync();
        await db.RecruitmentSettings.ExecuteDeleteAsync();
        await db.RecruitmentConsentTexts.ExecuteDeleteAsync();
        await db.WorkDepartmentHeads.ExecuteDeleteAsync();

        Guid Department(string code, string name)
        {
            var existing = db.Departments.FirstOrDefault(d => d.Code == code);
            if (existing is not null)
            {
                return existing.Id;
            }

            var department = new Department { Code = code, Name = name };
            db.Departments.Add(department);
            db.SaveChanges();
            return department.Id;
        }

        var sales = Department("sales", "Продажи");
        var production = Department("production", "Производство");
        Guid User(string login, string name)
        {
            var user = new User { Login = $"{login}-{Guid.NewGuid():N}"[..20], FullName = name };
            db.Users.Add(user);
            db.SaveChanges();
            return user.Id;
        }

        var hr = User("hr", "Рекрутер");
        var head = User("head", "Руководитель продаж");
        var stranger = User("emp", "Сотрудник");
        db.WorkDepartmentHeads.Add(new WorkDepartmentHead { DepartmentId = sales, UserId = head });
        await db.SaveChangesAsync();
        return (scope, scope.ServiceProvider.GetRequiredService<RecruitmentService>(), db, new Setup(hr, head, stranger, sales, production));
    }

    private static RecruitmentContext Hr(Setup s) => new(s.Hr, true, true, []);
    private static RecruitmentContext Head(Setup s) => new(s.Head, false, false, [s.Sales]);

    private static VacancyInput VacancyOf(Setup s, string title = "Торговый представитель") =>
        new(title, s.Sales, "Работа с магазинами", "Опыт продаж от 1 года", "Пн–сб, служебный транспорт", 5_000_000, 8_000_000, "Ташкент", "full", 2, null);

    private static async Task<Guid> OpenVacancyAsync(RecruitmentService service, Setup s, string title = "Торговый представитель")
    {
        var vacancy = await service.CreateVacancyAsync(Hr(s), VacancyOf(s, title), default);
        await service.SetVacancyStatusAsync(Hr(s), vacancy.Id, VacancyStatus.Open, default);
        return vacancy.Id;
    }

    // ---------- заявки ----------

    [PostgresFact]
    public async Task Department_head_requests_people_for_own_department_and_hr_decides()
    {
        var (scope, service, db, s) = await StartAsync();
        using var _ = scope;
        var input = new HiringRequestInput(s.Sales, "Торговый представитель", 2, "Новый маршрут", "Опыт продаж", 5_000_000, 8_000_000, null);

        // Руководитель — только за свой отдел; сотрудник без руководства — никак.
        await Assert.ThrowsAsync<FieldForbiddenException>(() => service.CreateRequestAsync(Head(s), input with { DepartmentId = s.Production }, default));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => service.CreateRequestAsync(new RecruitmentContext(s.Stranger, false, false, []), input, default));
        await Assert.ThrowsAsync<FieldValidationException>(() => service.CreateRequestAsync(Head(s), input with { SalaryFrom = 9_000_000 }, default));
        var request = await service.CreateRequestAsync(Head(s), input, default);
        Assert.Equal(("Submitted", true, false), (request.Status, request.CanCancel, request.CanDecide));

        // HR видит все заявки, руководитель другого отдела — нет.
        Assert.Single(await service.RequestsAsync(Hr(s), null, default));
        Assert.Empty(await service.RequestsAsync(new RecruitmentContext(s.Stranger, false, false, [s.Production]), null, default));

        // Отказ — только с причиной; одобрение открывает черновик вакансии с данными заявки и уведомляет автора.
        await Assert.ThrowsAsync<FieldValidationException>(() => service.RejectRequestAsync(Hr(s), request.Id, new DecisionInput(null), default));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => service.ApproveRequestAsync(Head(s), request.Id, new DecisionInput(null), default));
        var approved = await service.ApproveRequestAsync(Hr(s), request.Id, new DecisionInput("Открываем"), default);
        Assert.Equal("Approved", approved.Status);
        var vacancy = await db.Vacancies.AsNoTracking().SingleAsync(v => v.Id == approved.VacancyId);
        Assert.Equal(("Торговый представитель", VacancyStatus.Draft, 2, 5_000_000m, s.Sales), (vacancy.Title, vacancy.Status, vacancy.Headcount, vacancy.SalaryFrom!.Value, vacancy.DepartmentId));
        Assert.Equal($"V{vacancy.Number}", approved.VacancyCode);
        Assert.True(await db.UserNotifications.AnyAsync(n => n.RecipientId == s.Head && n.Kind == UserNotificationKind.HiringRequestStatus));
        await Assert.ThrowsAsync<FieldConflictException>(() => service.CancelRequestAsync(Head(s), request.Id, default));
    }

    // ---------- вакансии, этапы, закрытие ----------

    [PostgresFact]
    public async Task Stages_keep_history_and_closing_a_vacancy_closes_open_applications()
    {
        var (scope, service, db, s) = await StartAsync();
        using var _ = scope;
        var draft = await service.CreateVacancyAsync(Hr(s), VacancyOf(s), default);
        var candidate = await service.CreateCandidateAsync(Hr(s), new CandidateInput("Алишер Каримов", "Ташкент", "uz", null,
            [new ContactInput(CandidateContactKind.Phone, "90 123 45 67")], "manual", null, null, "written"), default);

        // В черновик кандидатов не добавляют; после открытия — да, один раз.
        await Assert.ThrowsAsync<FieldValidationException>(() => service.AddApplicationAsync(Hr(s), candidate.CandidateId, new ApplicationInput(draft.Id), default));
        await service.SetVacancyStatusAsync(Hr(s), draft.Id, VacancyStatus.Open, default);
        var card = await service.AddApplicationAsync(Hr(s), candidate.CandidateId, new ApplicationInput(draft.Id), default);
        await Assert.ThrowsAsync<FieldConflictException>(() => service.AddApplicationAsync(Hr(s), candidate.CandidateId, new ApplicationInput(draft.Id), default));
        var application = Assert.Single(card.Applications);
        Assert.Null(card.DeleteAt);

        // Этапы с историей; отказ — с причиной; читатель менять не может.
        await Assert.ThrowsAsync<FieldForbiddenException>(() => service.ChangeStageAsync(new RecruitmentContext(s.Stranger, true, false, []), application.Id,
            new StageInput(ApplicationStage.Screening, null, null), default));
        await service.ChangeStageAsync(Hr(s), application.Id, new StageInput(ApplicationStage.Screening, "Позвонили", null), default);
        await Assert.ThrowsAsync<FieldValidationException>(() => service.ChangeStageAsync(Hr(s), application.Id, new StageInput(ApplicationStage.Rejected, null, null), default));
        card = await service.ChangeStageAsync(Hr(s), application.Id, new StageInput(ApplicationStage.Interview, null, null), default);
        Assert.Equal(["Interview", "Screening", "New"], card.Applications[0].History.Select(h => h.To));
        var vacancyView = await service.VacancyAsync(Hr(s), draft.Id, default);
        Assert.Equal(1, vacancyView.Stages["Interview"]);
        Assert.Equal("+998901234567", vacancyView.Applications[0].Phone);

        // Закрыли вакансию — открытый отклик получает отказ «вакансия закрыта», у кандидата пошёл срок хранения.
        await service.SetVacancyStatusAsync(Hr(s), draft.Id, VacancyStatus.Closed, default);
        card = await service.CandidateAsync(Hr(s), candidate.CandidateId, default);
        Assert.Equal(("Rejected", "Вакансия закрыта"), (card.Applications[0].Stage, card.Applications[0].RejectReason));
        Assert.NotNull(card.DeleteAt);
        Assert.Equal(7, card.RetentionDays);
        Assert.Contains(await db.AuditLogs.AsNoTracking().Where(a => a.Action == "recruitment.vacancy.status").Select(a => a.Data!).ToListAsync(), d => d.Contains("\"closed\": 1") || d.Contains("\"closed\":1"));
    }

    // ---------- дедупликация ----------

    [PostgresFact]
    public async Task Same_person_is_found_by_source_id_or_verified_contact_never_by_name()
    {
        var (scope, service, db, s) = await StartAsync();
        using var _ = scope;
        var vacancy = await OpenVacancyAsync(service, s);

        // Отклик HH пришёл дважды — один кандидат, второй раз — «тот же ID в источнике».
        var hh = new CandidateIntake("Иван Петров", null, null, null, [new IntakeContact(CandidateContactKind.Email, "ivan@example.com", true)], "hh", "resume-1", null, vacancy, null);
        var first = await service.IntakeAsync(hh, null, default);
        var again = await service.IntakeAsync(hh, null, default);
        Assert.Equal((true, (string?)null), (first.Created, first.MatchedBy));
        Assert.Equal((false, "source", first.CandidateId), (again.Created, again.MatchedBy, again.CandidateId));

        // Написал в Telegram с подтверждённым e-mail — тот же кандидат, добавился Telegram ID.
        var telegram = await service.IntakeAsync(new CandidateIntake("Ваня", null, null, null,
            [new IntakeContact(CandidateContactKind.Email, "IVAN@example.com", true), new IntakeContact(CandidateContactKind.Telegram, "555001", true)],
            "telegram", "555001", null, null, null), null, default);
        Assert.Equal((false, "contact", first.CandidateId), (telegram.Created, telegram.MatchedBy, telegram.CandidateId));
        var card = await service.CandidateAsync(Hr(s), first.CandidateId, default);
        Assert.Equal(["Email", "Telegram"], card.Contacts.Select(c => c.Kind).Order());
        Assert.Equal(["hh", "telegram"], card.Sources.Select(x => x.Source).Order());
        Assert.Single(card.Applications);

        // Тёзка с другими контактами — другой человек, без подозрений.
        var namesake = await service.CreateCandidateAsync(Hr(s), new CandidateInput("Иван Петров", null, null, null,
            [new ContactInput(CandidateContactKind.Phone, "+998 93 000 00 01")], "manual", null, null, null), default);
        Assert.True(namesake.Created);
        Assert.Equal(0, namesake.Duplicates);

        // Тот же телефон, введённый вручную, — не объединяется сам: подозрение на дубль для HR.
        var twin = await service.CreateCandidateAsync(Hr(s), new CandidateInput("Иван П.", null, null, null,
            [new ContactInput(CandidateContactKind.Phone, "93 000 00 01")], "referral", "От Алишера", null, null), default);
        Assert.Equal((true, 1), (twin.Created, twin.Duplicates));
        Assert.Equal(2, (await service.CandidatesAsync(Hr(s), new CandidateQuery(null, null, null, true), default)).Total);
        var duplicate = Assert.Single((await service.CandidateAsync(Hr(s), twin.CandidateId, default)).Duplicates);
        Assert.Equal((namesake.CandidateId, "Phone", "+998930000001"), (duplicate.OtherCandidateId, duplicate.Kind, duplicate.Value));

        // Поиск по имени и по цифрам телефона.
        Assert.Equal(3, (await service.CandidatesAsync(Hr(s), new CandidateQuery("иван", null, null, false), default)).Total);
        Assert.Equal(2, (await service.CandidatesAsync(Hr(s), new CandidateQuery("000 00 01", null, null, false), default)).Total);
    }

    [PostgresFact]
    public async Task Merging_a_duplicate_moves_everything_and_removes_the_second_card()
    {
        var (scope, service, db, s) = await StartAsync();
        using var _ = scope;
        var vacancy = await OpenVacancyAsync(service, s);
        var other = await OpenVacancyAsync(service, s, "Супервайзер");
        var keep = await service.CreateCandidateAsync(Hr(s), new CandidateInput("Дилноза Юсупова", "Самарканд", null, "Сильная",
            [new ContactInput(CandidateContactKind.Phone, "+998 91 111 11 11")], "manual", null, vacancy, "written"), default);
        var drop = await service.CreateCandidateAsync(Hr(s), new CandidateInput("Дилноза Ю.", null, "uz", "Хочет в Ташкент",
            [new ContactInput(CandidateContactKind.Phone, "911111111"), new ContactInput(CandidateContactKind.Email, "dilnoza@example.com")], "referral", null, vacancy, null), default);
        await service.AddApplicationAsync(Hr(s), drop.CandidateId, new ApplicationInput(other), default);
        await service.UploadResumeAsync(Hr(s), drop.CandidateId, Upload("cv.txt", "Резюме: продажи 3 года"), default);
        var duplicate = Assert.Single((await service.CandidateAsync(Hr(s), drop.CandidateId, default)).Duplicates);

        await Assert.ThrowsAsync<FieldValidationException>(() => service.MergeDuplicateAsync(Hr(s), duplicate.Id, Guid.NewGuid(), default));
        var merged = await service.MergeDuplicateAsync(Hr(s), duplicate.Id, keep.CandidateId, default);

        Assert.Equal(("Дилноза Юсупова", "Самарканд", "uz"), (merged.FullName, merged.City, merged.Language));
        Assert.Contains("Хочет в Ташкент", merged.Notes);
        Assert.Equal(["Email", "Phone"], merged.Contacts.Select(c => c.Kind).Order());
        Assert.Equal(2, merged.Applications.Count);              // своя вакансия и «Супервайзер»; общая — один раз
        Assert.Single(merged.Resumes);
        Assert.Single(merged.Consents);
        Assert.Empty(merged.Duplicates);
        Assert.False(await db.Candidates.AnyAsync(c => c.Id == drop.CandidateId));
        await Assert.ThrowsAsync<FieldNotFoundException>(() => service.CandidateAsync(Hr(s), drop.CandidateId, default));
    }

    // ---------- резюме ----------

    private static RecruitmentUpload Upload(string name, string text, string type = "text/plain") => Upload(name, Encoding.UTF8.GetBytes(text), type);

    private static RecruitmentUpload Upload(string name, byte[] bytes, string type) => new(name, type, bytes.Length, new MemoryStream(bytes));

    /// <summary>Настоящий PDF с текстовым слоем (одна страница, Helvetica) — как резюме, сохранённое из Word.</summary>
    internal static byte[] Pdf(string text)
    {
        var content = $"BT /F1 12 Tf 72 720 Td ({text}) Tj ET";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
        };
        var sb = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(sb.ToString()));
            sb.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = Encoding.ASCII.GetByteCount(sb.ToString());
        sb.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            sb.Append($"{offset:D10} 00000 n \n");
        }

        sb.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    [PostgresFact]
    public async Task Resume_text_is_read_from_pdf_files_are_checked_and_downloads_are_audited()
    {
        var (scope, service, db, s) = await StartAsync();
        using var _ = scope;
        var candidate = await service.CreateCandidateAsync(Hr(s), new CandidateInput("Сардор Алиев", null, null, null, [], "manual", null, null, null), default);

        await service.UploadResumeAsync(Hr(s), candidate.CandidateId, Upload("Aliev CV.pdf", Pdf("Sales representative, 5 years in FMCG"), "application/pdf"), default);
        await scope.ServiceProvider.GetRequiredService<OneBase.Application.Recruitment.Jobs.RecruitmentJobRunner>().DrainAsync(default); // текст — в фоне
        var card = await service.CandidateAsync(Hr(s), candidate.CandidateId, default);
        var resume = Assert.Single(card.Resumes);
        Assert.True(resume.HasText);
        Assert.Contains("Sales representative, 5 years in FMCG", resume.TextPreview);

        // Тот же файл ещё раз — нет; фото — сохраняется без текста; исполняемый файл — нет.
        await Assert.ThrowsAsync<FieldConflictException>(() => service.UploadResumeAsync(Hr(s), candidate.CandidateId,
            Upload("copy.pdf", Pdf("Sales representative, 5 years in FMCG"), "application/pdf"), default));
        await service.UploadResumeAsync(Hr(s), candidate.CandidateId, Upload("photo.jpg", [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3], "image/jpeg"), default);
        await scope.ServiceProvider.GetRequiredService<OneBase.Application.Recruitment.Jobs.RecruitmentJobRunner>().DrainAsync(default);
        card = await service.CandidateAsync(Hr(s), candidate.CandidateId, default);
        Assert.Contains(card.Resumes, r => r.FileName == "photo.jpg" && !r.HasText && r.TextError!.Contains("Фото"));
        await Assert.ThrowsAsync<FieldValidationException>(() => service.UploadResumeAsync(Hr(s), candidate.CandidateId, Upload("cv.exe", "MZ"), default));

        // Скачивание: тип PDF, ключ хранилища кандидата; в аудите — без имени файла и кандидата.
        var download = await service.DownloadResumeAsync(new RecruitmentContext(s.Stranger, true, false, []), resume.Id, default);
        Assert.Equal(("application/pdf", "Aliev CV.pdf"), (download.ContentType, download.FileName));
        Assert.StartsWith($"recruitment/resumes/{candidate.CandidateId:N}/", download.ObjectKey);
        await Assert.ThrowsAsync<FieldForbiddenException>(() => service.DownloadResumeAsync(Head(s), resume.Id, default));
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "recruitment.resume.downloaded"));
    }

    // ---------- согласие, срок хранения, удаление ----------

    [PostgresFact]
    public async Task Data_is_kept_while_hiring_goes_on_and_deleted_a_week_after_it_ends()
    {
        var (scope, service, db, s) = await StartAsync();
        using var _ = scope;
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        var retention = scope.ServiceProvider.GetRequiredService<RecruitmentRetentionService>();
        var vacancy = await OpenVacancyAsync(service, s);
        var start = new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
        fixture.Clock.Now = start;

        var active = await service.CreateCandidateAsync(Hr(s), new CandidateInput("В работе", null, null, null, [], "manual", null, vacancy, "written"), default);
        var finished = await service.CreateCandidateAsync(Hr(s), new CandidateInput("Отказ", null, null, null, [new ContactInput(CandidateContactKind.Email, "no@example.com")], "manual", null, vacancy, "written"), default);
        await service.UploadResumeAsync(Hr(s), finished.CandidateId, Upload("cv.txt", "Резюме"), default);
        var application = (await service.CandidateAsync(Hr(s), finished.CandidateId, default)).Applications[0];
        await service.ChangeStageAsync(Hr(s), application.Id, new StageInput(ApplicationStage.Rejected, null, "Нет опыта"), default);
        var deleteAt = (await service.CandidateAsync(Hr(s), finished.CandidateId, default)).DeleteAt;
        Assert.Equal(start.AddDays(7), deleteAt);

        // Через 6 дней — ещё хранится; через 8 — удалён целиком вместе с файлом; кандидат в работе — никогда.
        fixture.Clock.Now = start.AddDays(6);
        Assert.Equal(0, await retention.RunAsync(default));
        fixture.Clock.Now = start.AddDays(8);
        Assert.Equal(1, await retention.RunAsync(default));
        Assert.False(await db.Candidates.AnyAsync(c => c.Id == finished.CandidateId));
        Assert.False(await db.CandidateContacts.AnyAsync(c => c.Value == "no@example.com"));
        Assert.DoesNotContain(await storage.ListKeysAsync().ToListAsync(), k => k.Contains(finished.CandidateId.ToString("N")));
        Assert.True(await db.Candidates.AnyAsync(c => c.Id == active.CandidateId));
        var deleted = await db.AuditLogs.AsNoTracking().Where(a => a.Action == "recruitment.candidate.deleted").Select(a => a.Data!).ToListAsync();
        Assert.Contains(deleted, d => d.Contains("retention"));
        Assert.DoesNotContain(deleted, d => d.Contains("Отказ") || d.Contains("no@example.com"));

        // Срок — настройка: 1–365 дней; новый текст согласия — новая версия.
        await Assert.ThrowsAsync<FieldValidationException>(() => service.SaveSettingsAsync(Hr(s), new RecruitmentSettingsInput(0, "Текст"), default));
        var settings = await service.SaveSettingsAsync(Hr(s), new RecruitmentSettingsInput(14, "Согласен на обработку данных."), default);
        Assert.Equal((14, 2), (settings.RetentionDays, settings.ConsentVersion));

        // Отзыв согласия — данные удаляются сразу.
        var consent = (await service.CandidateAsync(Hr(s), active.CandidateId, default)).Consents[0];
        await service.WithdrawConsentAsync(Hr(s), consent.Id, default);
        Assert.False(await db.Candidates.AnyAsync(c => c.Id == active.CandidateId));
        Assert.Contains(await db.AuditLogs.AsNoTracking().Where(a => a.Action == "recruitment.candidate.deleted").Select(a => a.Data!).ToListAsync(),
            d => d.Contains("consent_withdrawn"));
        fixture.Clock.Now = null;
    }

    [PostgresFact]
    public async Task Candidates_are_visible_only_with_the_recruitment_right()
    {
        var (scope, service, _, s) = await StartAsync();
        using var __ = scope;
        await Assert.ThrowsAsync<FieldForbiddenException>(() => service.CandidatesAsync(Head(s), new CandidateQuery(null, null, null, false), default));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => service.VacanciesAsync(Head(s), null, null, default));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => service.CreateCandidateAsync(new RecruitmentContext(s.Hr, true, false, []),
            new CandidateInput("Кто-то", null, null, null, [], "manual", null, null, null), default));
        await Assert.ThrowsAsync<FieldValidationException>(() => service.CreateCandidateAsync(Hr(s),
            new CandidateInput("Кто-то", null, null, null, [], "hh", null, null, null), default));
        var view = await service.ContextViewAsync(Head(s), default);
        Assert.Equal((false, false, true), (view.CanRead, view.CanManage, view.CanRequest));
        Assert.Equal([s.Sales], view.RequestDepartments.Select(d => d.Id));
        Assert.Empty(view.Recruiters);
        await Assert.ThrowsAsync<FieldForbiddenException>(() => service.ContextViewAsync(new RecruitmentContext(s.Stranger, false, false, []), default));
    }
}
