using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Field;
using OneBase.Application.Recruitment;
using OneBase.Application.Recruitment.Documents;
using OneBase.Application.Recruitment.Jobs;
using OneBase.Application.Recruitment.Screening;
using OneBase.Domain.Identity;
using OneBase.Domain.Recruitment;
using OneBase.Infrastructure.Persistence;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>
/// AI-скрининг на настоящей базе (подставная модель): ручной импорт без дублей (тот же файл, тот же профиль HH), разбор → сводка и навыки →
/// анализ под вакансию в фоне, панель и фильтры, анализ по кнопке (резюме из API HH — модели только с разрешения), статусы HR (резерв — только
/// с согласием), очередь без приглашений, сравнение (данные кандидата удаляются вместе с ним), история рейтинга.
/// </summary>
public class RecruitmentScreeningTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private const string ResumeA =
        "Каримов Сардор Алишерович\nВодитель-экспедитор категории C\nСтаж 6 лет\nОпыт: 2019–2025 ООО «Логистика Плюс», водитель-экспедитор\n"
        + "Навыки: категория C, Excel, 1С\nОбразование: автомобильно-дорожный техникум, 2015\nТелефон: +998 90 123 45 67";

    private const string ResumeB =
        "Продавец-консультант, магазин одежды, 2 года.\nНавыки: Word, работа с кассой.\nХочу попробовать себя в логистике, готов учиться.";

    private static string Parse() => JsonSerializer.Serialize(new
    {
        fullName = new { value = "Каримов Сардор Алишерович", evidence = "Каримов Сардор Алишерович", basis = "stated" },
        professionalTitle = new { value = "Водитель-экспедитор", evidence = "Водитель-экспедитор категории C", basis = "stated" },
        experienceYears = new { value = "6", evidence = "Стаж 6 лет", basis = "stated" },
        workExperience = new[] { new { company = "ООО «Логистика Плюс»", position = "водитель-экспедитор", from = "2019", to = "2025", evidence = "ООО «Логистика Плюс»" } },
        education = new[] { new { institution = "автомобильно-дорожный техникум", year = "2015", evidence = "автомобильно-дорожный техникум" } },
        skills = new[]
        {
            new { value = "Excel", evidence = "Excel", basis = "stated" }, new { value = "1С", evidence = "1С", basis = "stated" },
            new { value = "Категория C", evidence = "категория C", basis = "stated" },
        },
        summary = "Водитель-экспедитор, 6 лет.",
    });

    private static string Screen(params (string Code, string Status, int Points, string? Quote)[] criteria) => JsonSerializer.Serialize(new
    {
        criteria = criteria.Select(c => new { code = c.Code, status = c.Status, points = c.Points, explanation = "—", quotes = c.Quote is null ? [] : new[] { c.Quote } }),
        summary = "Сводка",
        whyFits = "Почему",
        strengths = new[] { "Стаж" },
        weaknesses = new[] { "Нет достижений" },
        missingRequirements = Array.Empty<string>(),
        recommendation = "Пригласить",
        interviewQuestions = Enumerable.Range(1, 6).Select(i => new { question = $"Вопрос {i}", type = "experience", reason = "проверить" }),
        confidence = 0.8,
    });

    private async Task<(IServiceScope Scope, OneBaseDbContext Db, RecruitmentContext Hr, Guid Vacancy)> StartAsync()
    {
        await fixture.ResetAsync();
        var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await db.RecruitmentJobs.ExecuteDeleteAsync();
        await db.RecruitmentExceptions.ExecuteDeleteAsync();
        await db.HhConnections.ExecuteDeleteAsync();
        await db.HhApiCalls.ExecuteDeleteAsync();
        await db.Interviews.ExecuteDeleteAsync();
        await db.Candidates.ExecuteDeleteAsync();
        await db.Vacancies.ExecuteDeleteAsync();
        await db.RecruitmentSettings.ExecuteDeleteAsync();
        if (!await db.Departments.AnyAsync(d => d.Code == "sales"))
        {
            db.Departments.Add(new Department { Code = "sales", Name = "Продажи" });
            await db.SaveChangesAsync();
        }

        var hr = new RecruitmentContext(fixture.AdminId, true, true, []);
        var service = scope.ServiceProvider.GetRequiredService<RecruitmentService>();
        var vacancy = await service.CreateVacancyAsync(hr, new VacancyInput("Водитель-экспедитор", (await db.Departments.FirstAsync(d => d.Code == "sales")).Id,
            "Развозка товара по магазинам", "Права категории C, опыт от 2 лет", "Пн–сб", 6_000_000, 8_000_000, "Ташкент", "full", 1, null), default);
        await service.SetVacancyStatusAsync(hr, vacancy.Id, VacancyStatus.Open, default);
        fixture.Gateway.Script.Clear();
        return (scope, db, hr, vacancy.Id);
    }

    private static RecruitmentUpload Upload(string name, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return new RecruitmentUpload(name, "text/plain", bytes.Length, new MemoryStream(bytes));
    }

    private Task DrainAsync() => fixture.Services.GetRequiredService<RecruitmentJobRunner>().DrainAsync(default);

    [PostgresFact]
    public async Task Screening_imports_without_duplicates_analyzes_filters_marks_queues_and_compares()
    {
        var (scope, db, hr, vacancyId) = await StartAsync();
        using var _ = scope;
        var screening = scope.ServiceProvider.GetRequiredService<ScreeningService>();

        // Файл: разбор резюме (имя, должность, стаж, навыки) и анализ под вакансию — в фоне.
        fixture.Gateway.Script.Enqueue(_ => Parse());
        fixture.Gateway.Script.Enqueue(m =>
        {
            Assert.Contains("Права категории C, опыт от 2 лет", m[1].Content);
            return Screen(("experience", "met", 23, "Стаж 6 лет"), ("skills", "met", 22, "Excel, 1С"), ("requirements", "met", 18, "категории C"),
                ("achievements", "not_stated", 0, null), ("education", "met", 8, "автомобильно-дорожный техникум"), ("extras", "partial", 3, null));
        });
        var a = await screening.ImportFileAsync(hr, vacancyId, Upload("cv-karimov.txt", ResumeA), "written", default);
        Assert.True(a.Created);
        await DrainAsync();
        var karimov = await db.Candidates.AsNoTracking().Include(c => c.Skills).SingleAsync(c => c.Id == a.CandidateId);
        Assert.Equal("Каримов Сардор Алишерович", karimov.FullName); // имя-заглушка заменено именем из резюме
        Assert.Equal(("Водитель-экспедитор", 72), (karimov.DesiredPosition, karimov.ExperienceMonths));
        Assert.Equal("водитель-экспедитор — ООО «Логистика Плюс»", karimov.LastJob);
        Assert.Equal(["1с", "excel", "категория c"], karimov.Skills.Select(s => s.Normalized).Order());
        var scoreA = await db.CandidateScores.AsNoTracking().SingleAsync(s => s.CandidateId == a.CandidateId);
        Assert.Equal((2, 81), (scoreA.Version, scoreA.Overall)); // 23 + 22 + 18 + 7,5 + 8 + 2,5 (без цитаты — не выше половины)

        // Тот же файл ещё раз — тот же кандидат, без второй карточки.
        var again = await screening.ImportFileAsync(hr, vacancyId, Upload("копия.txt", ResumeA), null, default);
        Assert.Equal((a.CandidateId, false, "file"), (again.CandidateId, again.Created, again.MatchedBy));

        // Текст — второй кандидат (AI не ответил — разбор без модели, анализа нет: «ожидает»).
        var b = await screening.ImportTextAsync(hr, new ScreeningTextImport(vacancyId, ResumeB, "Бобур Алиев", null), default);
        await DrainAsync();
        Assert.True(b.Created);

        // Ссылка на профиль HH — кандидат с источником и ссылкой, без загрузки резюме; повтор — тот же.
        var hhUrl = "https://hh.uz/resume/0123456789abcdef0123456789abcdef0123";
        var c = await screening.ImportLinkAsync(hr, new ScreeningLinkImport(vacancyId, hhUrl, null, "hh"), default);
        Assert.True(c.Created);
        var c2 = await screening.ImportLinkAsync(hr, new ScreeningLinkImport(vacancyId, hhUrl, null, null), default);
        Assert.Equal((c.CandidateId, false, "source"), (c2.CandidateId, c2.Created, c2.MatchedBy));
        await Assert.ThrowsAsync<FieldValidationException>(() => screening.ImportLinkAsync(hr, new ScreeningLinkImport(vacancyId, "https://hh.uz/vacancy/1", null, null), default));
        Assert.Equal(3, await db.Candidates.CountAsync());

        // Панель и фильтры.
        var page = await screening.PageAsync(hr, new ScreeningQuery(vacancyId), default);
        Assert.Equal(new ScreeningKpis(3, 1, 1, 0, 1), page.Kpis);
        Assert.Equal(a.ApplicationId, page.Items[0].ApplicationId); // по оценке
        Assert.Equal((3, 1, 1), (page.Vacancies.Single(v => v.Id == vacancyId).Found, page.Vacancies.Single(v => v.Id == vacancyId).Analyzed,
            page.Vacancies.Single(v => v.Id == vacancyId).Suitable));
        Assert.True(page.Items.Single(r => r.CandidateId == b.CandidateId).Pending);
        var hhRow = page.Items.Single(r => r.CandidateId == c.CandidateId);
        Assert.Equal((hhUrl, false), (hhRow.HhUrl, hhRow.HasResume));
        Assert.Equal([a.CandidateId], (await screening.PageAsync(hr, new ScreeningQuery(vacancyId, Skill: "Excel"), default)).Items.Select(r => r.CandidateId));
        Assert.Equal([a.CandidateId], (await screening.PageAsync(hr, new ScreeningQuery(vacancyId, MinExperienceMonths: 60), default)).Items.Select(r => r.CandidateId));
        Assert.Equal([a.CandidateId], (await screening.PageAsync(hr, new ScreeningQuery(vacancyId, MinScore: 70), default)).Items.Select(r => r.CandidateId));
        Assert.Equal([a.CandidateId], (await screening.PageAsync(hr, new ScreeningQuery(vacancyId, Position: "экспедитор"), default)).Items.Select(r => r.CandidateId));
        Assert.Equal(3, (await screening.PageAsync(hr, new ScreeningQuery(vacancyId, Status: "new"), default)).Total); // анализ статус не меняет

        // Анализ по кнопке: резюме, полученное через API HH, модели не уходит (соглашение HH, п. 4.6), пока HR не разрешит.
        db.CandidateResumes.Add(new CandidateResume
        {
            CandidateId = b.CandidateId, FileName = "HH.txt", ContentType = "text/plain", ObjectKey = "test/hh", Sha256 = new string('b', 64), Source = "hh",
            Text = "ТАЙНА-HH: резюме из API", Status = DocumentStatus.Done, OcrStatus = DocumentStepStatus.Done, AnalysisStatus = DocumentStepStatus.Skipped,
        });
        await db.SaveChangesAsync();
        fixture.Gateway.Script.Enqueue(m =>
        {
            Assert.DoesNotContain("ТАЙНА-HH", m[1].Content);
            Assert.Contains("Продавец-консультант", m[1].Content);
            return Screen(("experience", "not_met", 5, "Продавец-консультант"), ("skills", "partial", 10, "Word"), ("requirements", "not_met", 4, "Продавец-консультант"));
        });
        var bApp = b.ApplicationId!.Value;
        var first = await screening.AnalyzeAsync(hr, bApp, default);
        Assert.Equal((34, "low", (int?)null), (first.Overall, first.Category, first.Delta)); // 5 + 10 + 4 + 7,5 + 5 + 2,5

        // Статусы HR: резерв — только с согласием; отметка — событием отклика; смена этапа отметку снимает.
        await Assert.ThrowsAsync<FieldValidationException>(() => screening.SetStatusAsync(hr, bApp, new ScreeningStatusInput("reserve", null, null), default));
        await Assert.ThrowsAsync<FieldValidationException>(() => screening.SetStatusAsync(hr, bApp, new ScreeningStatusInput("analyzing", null, null), default));
        await Assert.ThrowsAsync<FieldValidationException>(() => screening.SetStatusAsync(hr, bApp, new ScreeningStatusInput("rejected", null, null), default)); // нужна причина
        var aApp = a.ApplicationId!.Value;
        Assert.Equal("reserve", await screening.SetStatusAsync(hr, aApp, new ScreeningStatusInput("reserve", null, null), default));
        Assert.Equal([aApp], (await screening.PageAsync(hr, new ScreeningQuery(vacancyId, Status: "reserve"), default)).Items.Select(r => r.ApplicationId));
        Assert.Equal("recommended", await screening.SetStatusAsync(hr, aApp, new ScreeningStatusInput("recommended", "Сильный кандидат", null), default));
        Assert.Equal(1, (await screening.PageAsync(hr, new ScreeningQuery(vacancyId), default)).Kpis.Recommended);
        Assert.Equal("interview", await screening.SetStatusAsync(hr, aApp, new ScreeningStatusInput("interview", null, null), default));
        var application = await db.CandidateApplications.AsNoTracking().SingleAsync(x => x.Id == aApp);
        Assert.Equal((ApplicationStage.Interview, ApplicationMark.None), (application.Stage, application.Mark));
        Assert.Equal([ApplicationMark.Reserve, ApplicationMark.Recommended],
            await db.ApplicationEvents.AsNoTracking().Where(e => e.ApplicationId == aApp && e.Mark != null).OrderBy(e => e.Id).Select(e => e.Mark!.Value).ToListAsync());

        // Очередь: ожидающих нет; выбранный — в очередь без приглашения кандидату; второй раз — уже в очереди.
        Assert.Equal(0, (await screening.QueueAsync(hr, new ScreeningQueueInput(vacancyId, null), default)).Queued);
        Assert.Equal(1, (await screening.QueueAsync(hr, new ScreeningQueueInput(null, [bApp]), default)).Queued);
        Assert.Equal(CandidateScoringJob.NoInvite, (await db.RecruitmentJobs.AsNoTracking().SingleAsync(j => j.Queue == RecruitmentQueues.Scoring && j.RelatedId == bApp)).Payload);
        Assert.Equal("analyzing", (await screening.PageAsync(hr, new ScreeningQuery(vacancyId), default)).Items.Single(r => r.ApplicationId == bApp).Status);
        Assert.Equal(1, (await screening.QueueAsync(hr, new ScreeningQueueInput(null, [bApp]), default)).Skipped);
        await db.RecruitmentJobs.ExecuteDeleteAsync();

        // Сравнение: таблица — из анализов, рекомендация — модели; без анализа — нельзя; чужие ID из ответа не попадают.
        var comparer = scope.ServiceProvider.GetRequiredService<ICandidateComparer>();
        await Assert.ThrowsAsync<FieldValidationException>(() => comparer.CompareAsync(hr, new CompareInput(vacancyId, [aApp, c.ApplicationId!.Value]), default));
        fixture.Gateway.Script.Enqueue(m =>
        {
            Assert.Contains("Каримов Сардор Алишерович", m[1].Content);
            return JsonSerializer.Serialize(new { best = aApp, ranking = new[] { aApp, Guid.NewGuid(), bApp }, recommendation = "Каримов: стаж и категория C.",
                verdicts = new[] { new { id = bApp, verdict = "Мало опыта" } } });
        });
        var comparison = await comparer.CompareAsync(hr, new CompareInput(vacancyId, [bApp, aApp]), default);
        Assert.Equal((aApp, 2), (comparison.BestApplicationId, comparison.Candidates.Count));
        Assert.Equal([aApp, bApp], comparison.Ranking);
        Assert.Equal("6 г. · водитель-экспедитор — ООО «Логистика Плюс»", comparison.Candidates.Single(x => x.ApplicationId == aApp).Experience);
        Assert.Equal("Не указано в резюме", comparison.Candidates.Single(x => x.ApplicationId == bApp).Salary);
        Assert.Equal("Каримов Сардор Алишерович", Assert.Single(await screening.ComparisonsAsync(hr, vacancyId, default)).Best);
        Assert.Equal(comparison.Recommendation, (await screening.ComparisonAsync(hr, comparison.Id, default)).Recommendation);

        // Разрешили данные HH для AI — уходят; история рейтинга с изменением.
        db.HhConnections.Add(new HhConnection { AiOnHhData = true });
        await db.SaveChangesAsync();
        fixture.Gateway.Script.Enqueue(m =>
        {
            Assert.Contains("ТАЙНА-HH", m[1].Content);
            return Screen(("experience", "partial", 8, "Продавец-консультант"), ("skills", "partial", 10, "Word"), ("requirements", "not_met", 4, "Продавец-консультант"));
        });
        var second = await screening.AnalyzeAsync(hr, bApp, default);
        Assert.Equal((37, 3), (second.Overall, second.Delta));
        var history = Assert.Single(await screening.CandidateAsync(hr, b.CandidateId, default)).History;
        Assert.Equal([37, 34], history.Select(h => h.Overall));

        // Данные кандидата удалены — сравнения с ним тоже.
        await scope.ServiceProvider.GetRequiredService<RecruitmentService>().DeleteCandidateAsync(hr, a.CandidateId, default);
        Assert.False(await db.CandidateComparisons.AnyAsync());
        Assert.False(await db.CandidateSkills.AnyAsync(s => s.CandidateId == a.CandidateId));

        // Без права «управление» — только смотреть.
        var reader = new RecruitmentContext(fixture.AdminId, true, false, []);
        Assert.NotNull(await screening.PageAsync(reader, new ScreeningQuery(), default));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => screening.SetStatusAsync(reader, bApp, new ScreeningStatusInput("suitable", null, null), default));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => screening.QueueAsync(reader, new ScreeningQueueInput(vacancyId, null), default));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => screening.PageAsync(new RecruitmentContext(fixture.AdminId, false, false, []), new ScreeningQuery(), default));
    }
}
