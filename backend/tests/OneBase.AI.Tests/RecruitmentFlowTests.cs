using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OneBase.AI.Recruitment;
using OneBase.Application.Abstractions;
using OneBase.Application.Agents;
using OneBase.Application.Field;
using OneBase.Application.Recruitment;
using OneBase.Application.Recruitment.Hh;
using OneBase.Application.Recruitment.Telegram;
using OneBase.Application.Telegram;
using OneBase.Application.Telegram.Outbox;
using OneBase.Application.Telegram.Routing;
using OneBase.Domain.AI;
using OneBase.Domain.Identity;
using OneBase.Domain.Recruitment;
using OneBase.Domain.Telegram;
using OneBase.Domain.Work;
using OneBase.Infrastructure.Persistence;
using OneBase.Infrastructure.Recruitment;
using OneBase.Infrastructure.Telegram;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>
/// Подставной HH (api.hh.ru по спецификации): токен, /me, опубликованные вакансии, отклики «response», резюме (одно — платное: 403), действие по
/// отклику. Каждый запрос проверяется: HH-User-Agent с почтой, host=hh.uz, Bearer.
/// </summary>
public sealed class FakeHh : HttpMessageHandler
{
    public List<(HttpMethod Method, string Url, string? UserAgent, string? Auth, string Body)> Requests { get; } = [];
    public int TokenCalls;
    public string? LastGrant;

    private static HttpResponseMessage Json(object body, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct);
        Requests.Add((request.Method, request.RequestUri!.ToString(), request.Headers.TryGetValues("HH-User-Agent", out var ua) ? ua.First() : null,
            request.Headers.Authorization?.ToString(), body));
        var path = request.RequestUri!.AbsolutePath;
        if (!request.RequestUri.Query.Contains("host=hh.uz", StringComparison.Ordinal))
        {
            return Json(new { errors = new[] { new { type = "bad_argument", value = "host" } } }, HttpStatusCode.BadRequest);
        }

        switch (request.Method.Method, path)
        {
            case ("POST", "/token"):
                TokenCalls++;
                LastGrant = body.Contains("grant_type=refresh_token", StringComparison.Ordinal) ? "refresh" : "code";
                return Json(new { access_token = $"access-{TokenCalls}", token_type = "bearer", expires_in = 1209600, refresh_token = $"refresh-{TokenCalls}" });
            case ("GET", "/me"):
                return Json(new { id = "m1", employer = new { id = "777", name = "Тестовый работодатель" } });
            case ("GET", "/employers/777/vacancies/active"):
                return Json(new { items = new[] { new { id = "9001", name = "Водитель-экспедитор", area = new { name = "Ташкент" } } }, page = 0, pages = 1 });
            case ("GET", "/negotiations/response"):
                return Json(new
                {
                    items = new object[]
                    {
                        new
                        {
                            id = "n1", state = new { id = "response" }, updated_at = "2026-11-08T10:00:00+0500",
                            resume = new { id = "r1", first_name = "Иван", last_name = "Иванов", title = "Водитель", url = "https://api.hh.ru/resumes/r1?topic_id=n1" },
                            actions = new[] { new { id = "interview", name = "Интервью", method = "PUT", url = "https://api.hh.ru/negotiations/interview/n1" } },
                        },
                        new
                        {
                            id = "n2", state = new { id = "response" }, updated_at = "2026-11-08T11:00:00+0500",
                            resume = new { id = "r2", first_name = "Пётр", last_name = "Петров", title = "Экспедитор", url = "https://api.hh.ru/resumes/r2?topic_id=n2" },
                            actions = Array.Empty<object>(),
                        },
                    },
                    page = 0,
                    pages = 1,
                });
            case ("GET", "/resumes/r1"):
                return Json(new
                {
                    id = "r1", first_name = "Иван", last_name = "Иванов", title = "Водитель", area = new { name = "Ташкент" },
                    contact = new object[]
                    {
                        new { kind = "email", contact_value = "ivan.hh@example.com", verified = true, type = new { id = "email" } },
                        new { kind = "phone", contact_value = "+998901112233", verified = false, type = new { id = "cell" } },
                    },
                    experience = new[] { new { company = "Логистика", position = "Водитель категории C", start = "2019-01-01", end = (string?)null, description = "Развозка по Ташкенту" } },
                    skill_set = new[] { "Категория C", "Знание города" },
                });
            case ("GET", "/resumes/r2"):
                return Json(new { errors = new[] { new { type = "api_access_payment", value = "action_must_be_payed" } } }, HttpStatusCode.Forbidden);
            case ("PUT", "/negotiations/interview/n1"):
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            default:
                return Json(new { errors = new[] { new { type = "not_found" } } }, HttpStatusCode.NotFound);
        }
    }
}

/// <summary>
/// Этапы 4–7: HH.uz (вход по state, синхронизация откликов в единую базу без дублей, платное резюме — исключение, действия только из ответа
/// HH, обновление токена), AI-рекрутер (выключатель, согласие, просьба о человеке, ответ и выбор времени, неверный формат — человеку), оценка с
/// проверкой цитат, собеседования (занятость по календарю, запись, напоминание), исключения, напоминание не ответившему, аналитика.
/// </summary>
public class RecruitmentFlowTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly TelegramPeerRef Candidate = new(7101, TelegramChatType.Private, false, 81);

    private async Task<(IServiceScope Scope, OneBaseDbContext Db, RecruitmentContext Hr, Guid Sales)> StartAsync()
    {
        await fixture.ResetAsync();
        var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await db.RecruitmentExceptions.ExecuteDeleteAsync();
        await db.HhNegotiations.ExecuteDeleteAsync();
        await db.HhConnections.ExecuteDeleteAsync();
        await db.Candidates.ExecuteDeleteAsync();
        await db.Vacancies.ExecuteDeleteAsync();
        await db.RecruitmentSettings.ExecuteDeleteAsync();
        await db.CalendarEvents.ExecuteDeleteAsync();
        foreach (var (code, name) in new[] { ("sales", "Продажи"), ("hr", "HR") })
        {
            if (!await db.Departments.AnyAsync(d => d.Code == code))
            {
                db.Departments.Add(new Department { Code = code, Name = name });
            }
        }

        await db.SaveChangesAsync();
        var sales = (await db.Departments.FirstAsync(d => d.Code == "sales")).Id;
        return (scope, db, new RecruitmentContext(fixture.AdminId, true, true, []), sales);
    }

    private static async Task<Guid> OpenVacancyAsync(IServiceProvider sp, RecruitmentContext hr, Guid department, bool ai = false)
    {
        var service = sp.GetRequiredService<RecruitmentService>();
        var vacancy = await service.CreateVacancyAsync(hr, new VacancyInput("Водитель-экспедитор", department, "Развозка товара по магазинам", "Права категории C, опыт от 2 лет",
            "Пн–сб, 9:00–18:00, служебная машина", 6_000_000, 8_000_000, "Ташкент", "full", 1, null), default);
        await service.SetVacancyStatusAsync(hr, vacancy.Id, VacancyStatus.Open, default);
        if (ai)
        {
            await sp.GetRequiredService<RecruitmentAutomation>().SetVacancyAiAsync(hr, vacancy.Id, new VacancyAiInput(true, null), default);
        }

        return vacancy.Id;
    }

    private HhService Hh(IServiceProvider sp, FakeHh fake)
    {
        var type = typeof(TelegramAccountService).Assembly.GetType("OneBase.Infrastructure.Recruitment.HhClient")!;
        var api = (IHhApi)Activator.CreateInstance(type, new HttpClient(fake))!;
        return new HhService(sp.GetRequiredService<OneBaseDbContext>(), api, sp.GetRequiredService<IDataProtectionProvider>(), sp.GetRequiredService<RecruitmentService>(),
            sp.GetRequiredService<RecruitmentExceptions>(), sp.GetRequiredService<IAuditLogger>(), fixture.Clock, NullLogger<HhService>.Instance);
    }

    // ---------- HH.uz ----------

    [PostgresFact]
    public async Task Hh_connects_by_state_syncs_responses_once_and_only_runs_actions_hh_offered()
    {
        var (scope, db, hr, sales) = await StartAsync();
        using var _ = scope;
        var fake = new FakeHh();
        var hh = Hh(scope.ServiceProvider, fake);

        await Assert.ThrowsAsync<FieldValidationException>(() => hh.SaveSettingsAsync(hr, new HhSettingsInput("client", "secret", "http://insecure", "dev@example.com", true), default));
        await hh.SaveSettingsAsync(hr, new HhSettingsInput("client-1", "secret-1", "https://onebase.example.com/hr/recruitment/hh/callback", "dev@example.com", true), default);
        var url = await hh.StartConnectAsync(hr, default);
        Assert.StartsWith("https://hh.ru/oauth/authorize?response_type=code&client_id=client-1&state=", url);
        var state = Uri.UnescapeDataString(url.Split("state=")[1].Split('&')[0]);
        await Assert.ThrowsAsync<FieldValidationException>(() => hh.CompleteConnectAsync(hr, "code", "forged-state", default));
        var status = await hh.CompleteConnectAsync(hr, "code-1", state, default);
        Assert.Equal((true, "Тестовый работодатель"), (status.Connected, status.EmployerName));
        Assert.DoesNotContain("secret-1", JsonSerializer.Serialize(status));
        Assert.DoesNotContain("access-1", (await db.HhConnections.AsNoTracking().SingleAsync()).ProtectedAccessToken);

        // Вакансии работодателя и связь с вакансией OneBase.
        var vacancyId = await OpenVacancyAsync(scope.ServiceProvider, hr, sales);
        var hhVacancy = Assert.Single(await hh.VacanciesAsync(hr, default));
        await hh.LinkAsync(hr, vacancyId, hhVacancy.HhVacancyId, default);

        // Синхронизация: кандидат с подтверждённым e-mail и резюме; платное резюме — исключение HR, остальное идёт.
        var result = await hh.SyncAsync(null, default);
        Assert.Equal((2, 1), (result.Responses, result.Errors));
        var ivan = await db.Candidates.AsNoTracking().Include(c => c.Contacts).Include(c => c.Resumes).Include(c => c.Applications).SingleAsync(c => c.FullName == "Иванов Иван");
        Assert.Contains(ivan.Contacts, c => c.Kind == CandidateContactKind.Email && c.Value == "ivan.hh@example.com" && c.Verified);
        Assert.Contains(ivan.Contacts, c => c.Kind == CandidateContactKind.Phone && c.Value == "+998901112233" && !c.Verified);
        Assert.Contains("Водитель категории C", Assert.Single(ivan.Resumes).Text);
        Assert.Equal(vacancyId, Assert.Single(ivan.Applications).VacancyId);
        Assert.Contains(await db.RecruitmentExceptions.AsNoTracking().ToListAsync(), e => e.Kind == "hh" && e.Message.Contains("платный доступ"));

        // Повтор — без дублей и без второго платного просмотра резюме.
        var resumeViews = fake.Requests.Count(r => r.Url.Contains("/resumes/r1", StringComparison.Ordinal));
        await hh.SyncAsync(null, default);
        Assert.Equal(1, await db.Candidates.CountAsync(c => c.FullName == "Иванов Иван"));
        Assert.Equal(resumeViews, fake.Requests.Count(r => r.Url.Contains("/resumes/r1", StringComparison.Ordinal)));

        // Каждый запрос — с HH-User-Agent и почтой, host=hh.uz, к api.hh.ru; запросы с токеном — Bearer.
        Assert.All(fake.Requests, r => Assert.Equal("OneBase/1.0 (dev@example.com)", r.UserAgent));
        Assert.All(fake.Requests, r => Assert.StartsWith("https://api.hh.ru/", r.Url));
        Assert.All(fake.Requests.Where(r => !r.Url.Contains("/token", StringComparison.Ordinal)), r => Assert.StartsWith("Bearer access-", r.Auth));

        // Действие — только из списка HH; чужое — отказ без запроса.
        var negotiation = Assert.Single(await hh.NegotiationsAsync(hr, ivan.Id, default));
        await Assert.ThrowsAsync<FieldValidationException>(() => hh.ActionAsync(hr, negotiation.Id, "discard", null, default));
        await hh.ActionAsync(hr, negotiation.Id, "interview", "Приглашаем на собеседование", default);
        Assert.Contains(fake.Requests, r => r.Method == HttpMethod.Put && r.Url.StartsWith("https://api.hh.ru/negotiations/interview/n1", StringComparison.Ordinal)
                                            && r.Body.Contains("message=", StringComparison.Ordinal));

        // Токен истёк — обновляется (refresh), затем запрос идёт с новым.
        await db.HhConnections.ExecuteUpdateAsync(u => u.SetProperty(c => c.TokenExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
        db.ChangeTracker.Clear(); // в работе у каждого запроса свой контекст; здесь — тот же, что у сервиса
        await hh.SyncAsync(null, default);
        Assert.Equal("refresh", fake.LastGrant);
        Assert.Equal("Bearer access-2", fake.Requests[^1].Auth);
    }

    // ---------- AI-рекрутер ----------

    private TelegramIncoming Msg(Guid account, int id, string text) =>
        new(account, Candidate, "Сардор", id, 7101, "sardor_d", "Сардор Давлатов", text, fixture.Clock.GetUtcNow(), false, false);

    private async Task<Guid> HrAccountAsync(IServiceProvider sp)
    {
        fixture.Telegram.Me = new TelegramMe(777_000_333, "onebase_hr2", "HR");
        var accounts = sp.GetRequiredService<TelegramAccountService>();
        await accounts.SaveCredentialsAsync(123456, "0123456789abcdef0123456789abcdef", fixture.AdminId);
        var account = await accounts.CreateAccountAsync("HR", fixture.AdminId);
        await accounts.StartLoginAsync(account.Id, "+998 90 000 00 03", fixture.AdminId);
        await accounts.SubmitCodeAsync(account.Id, fixture.Telegram.Code, fixture.AdminId);
        await sp.GetRequiredService<TelegramDepartmentService>().SaveAsync(account.Id, new TelegramDepartmentsInput(["hr"], "hr"), fixture.AdminId);
        return account.Id;
    }

    private async Task DeliverAsync(TelegramIncoming m)
    {
        using var scope = fixture.Services.CreateScope();
        var route = await scope.ServiceProvider.GetRequiredService<TelegramMessageRouter>().RouteAsync(m, default);
        await scope.ServiceProvider.GetServices<ITelegramDepartmentHandler>().Single(h => h.Department == "hr").HandleAsync(m, route, default);
    }

    private async Task<List<string>> QueuedAsync(OneBaseDbContext db) =>
        await db.TelegramOutgoingMessages.AsNoTracking().OrderBy(m => m.CreatedAt).Select(m => m.Text).ToListAsync();

    [PostgresFact]
    public async Task Recruiter_asks_consent_first_answers_by_json_hands_off_and_books_from_the_calendar()
    {
        var (scope, db, hr, sales) = await StartAsync();
        using var _ = scope;
        var sp = scope.ServiceProvider;
        var account = await HrAccountAsync(sp);
        var vacancyId = await OpenVacancyAsync(sp, hr, sales, ai: true);
        var number = (await db.Vacancies.AsNoTracking().SingleAsync(v => v.Id == vacancyId)).Number;
        await sp.GetRequiredService<InterviewService>().SetInterviewersAsync(hr, vacancyId, new InterviewersInput([fixture.AdminId]), default);
        var automation = sp.GetRequiredService<RecruitmentAutomation>();

        // AI выключен (по умолчанию) — никто не отвечает, модель не вызывается.
        await DeliverAsync(Msg(account, 1, $"Здравствуйте, я по вакансии #V{number}"));
        Assert.Empty(await QueuedAsync(db));
        Assert.Empty(fixture.Gateway.Calls);

        // Включили ИИ-агента «Рекрутер» («Настройки → ИИ-агенты»): без паузы и «печатает…», круглосуточно — чтобы тест не ждал.
        await EnableRecruiterAsync(sp);

        // Без согласия — только текст согласия, без вызова модели.
        await DeliverAsync(Msg(account, 2, "Расскажите про вакансию"));
        Assert.Contains("ответьте «Согласен»", (await QueuedAsync(db))[^1]);
        Assert.Empty(fixture.Gateway.Calls);
        await SendQueuedAsync();

        // «Согласен» — согласие записано, дальше отвечает модель (JSON: reply).
        fixture.Gateway.Script.Enqueue(m =>
        {
            Assert.Contains("Права категории C", m[1].Content); // вакансия — в меняющейся части промпта
            Assert.Contains("Есть ли у вас опыт", m[1].Content); // вопросы отбора агента
            Assert.Contains("не выдавай", m[0].Content);
            return "{\"action\":\"reply\",\"message\":\"Спасибо! Сколько лет вы работаете водителем?\",\"notes\":\"\"}";
        });
        await DeliverAsync(Msg(account, 3, "Согласен"));
        var candidateId = (await db.Candidates.AsNoTracking().SingleAsync()).Id;
        Assert.True(await db.CandidateConsents.AnyAsync(c => c.CandidateId == candidateId && c.Channel == "telegram"));
        Assert.Equal("Спасибо! Сколько лет вы работаете водителем?", (await QueuedAsync(db))[^1]);

        // Модель решила пригласить — варианты времени из календаря (а не от модели); выбор «1» — запись и событие календаря.
        fixture.Gateway.Script.Enqueue(_ => "{\"action\":\"propose_interview\",\"message\":\"Вы нам подходите!\",\"notes\":\"Опыт 5 лет, ждёт 7 млн\"}");
        await DeliverAsync(Msg(account, 4, "5 лет, категория C, хочу 7 млн"));
        Assert.Contains("1) ", (await QueuedAsync(db))[^1]);
        Assert.Contains("AI: Опыт 5 лет", (await db.Candidates.AsNoTracking().SingleAsync()).Notes);
        var callsBefore = fixture.Gateway.Calls.Count;
        await DeliverAsync(Msg(account, 5, "1"));
        Assert.Equal(callsBefore, fixture.Gateway.Calls.Count); // выбор времени — без модели
        var interview = await db.Interviews.AsNoTracking().SingleAsync();
        Assert.Equal(InterviewStatus.Confirmed, interview.Status);
        var calendar = await db.CalendarEvents.AsNoTracking().Include(e => e.Participants).SingleAsync(e => e.Id == interview.CalendarEventId);
        Assert.Contains(calendar.Participants, p => p.UserId == fixture.AdminId);
        Assert.Contains("собеседование назначено", (await QueuedAsync(db))[^1]);
        Assert.Equal(ApplicationStage.Interview, (await db.CandidateApplications.AsNoTracking().SingleAsync()).Stage);

        // Просьба о человеке — HR, диалог на человека; ответ модели неверного формата — тоже к человеку.
        await DeliverAsync(Msg(account, 6, "Можно поговорить с человеком?"));
        Assert.Contains(await db.RecruitmentExceptions.AsNoTracking().ToListAsync(), e => e.Kind == "human_requested");
        Assert.Equal(TelegramConversationMode.Manual, (await db.TelegramConversations.AsNoTracking().SingleAsync()).Mode);
        var exception = (await sp.GetRequiredService<RecruitmentExceptions>().ListAsync(hr, RecruitmentExceptionStatus.Open, default)).Single(e => e.Kind == "human_requested");
        await sp.GetRequiredService<RecruitmentExceptions>().ResolveAsync(hr, exception.Id, new ResolveInput("Поговорили", true), default);
        Assert.Equal(TelegramConversationMode.Ai, (await db.TelegramConversations.AsNoTracking().SingleAsync()).Mode);
        fixture.Gateway.Script.Enqueue(_ => "не JSON");
        await DeliverAsync(Msg(account, 7, "А что по оформлению?"));
        Assert.Contains(await db.RecruitmentExceptions.AsNoTracking().ToListAsync(), e => e.Kind == "unclear" && e.Status == RecruitmentExceptionStatus.Open);

        // Каждый вызов модели — с назначением «hr.recruiter.*» для учёта расходов.
        Assert.All(fixture.Gateway.Calls, c => Assert.StartsWith("hr.recruiter.", c.Options.Context.Purpose));
    }

    /// <summary>Рекрутер HR по умолчанию — включён, отвечает сразу (без паузы и «печатает…»), круглосуточно.</summary>
    internal static async Task<DialogAgent> EnableRecruiterAsync(IServiceProvider sp, Action<DialogAgent>? configure = null)
    {
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var id = (await sp.GetRequiredService<DialogAgentService>().EnsureRecruiterAsync(default)).Id;
        var agent = await db.DialogAgents.SingleAsync(a => a.Id == id);
        (agent.Enabled, agent.ReplyDelayMinSeconds, agent.ReplyDelayMaxSeconds, agent.Typing) = (true, 0, 0, false);
        (agent.WorkFrom, agent.WorkTo, agent.WorkDays, agent.Intro) = (null, null, "1,2,3,4,5,6,7", "виртуальный помощник HR");
        configure?.Invoke(agent);
        await db.SaveChangesAsync();
        return agent;
    }

    private async Task SendQueuedAsync()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await db.TelegramAccountDepartments.ExecuteUpdateAsync(u => u.SetProperty(d => d.MinIntervalSeconds, 1));
        await db.TelegramOutgoingMessages.ExecuteUpdateAsync(u => u.SetProperty(m => m.SentAt, (DateTimeOffset?)null));
        await scope.ServiceProvider.GetRequiredService<TelegramOutboxRunner>().RunOnceAsync(default);
    }

    [Theory]
    [InlineData("Можно поговорить с человеком?", true)]
    [InlineData("Позовите менеджера", true)]
    [InlineData("Работал менеджером по продажам 3 года", false)]
    [InlineData("Был оператором колл-центра", false)]
    [InlineData("Inson bilan gaplashsam bo'ladimi?", true)]
    public void Only_explicit_requests_for_a_human_hand_off(string text, bool expected) => Assert.Equal(expected, RecruiterRules.AsksForHuman(text));

    [Theory]
    [InlineData("Согласен", true)]
    [InlineData("roziman", true)]
    [InlineData("Не согласен", false)]
    [InlineData("Согласен, но сначала расскажите о зарплате и графике работы подробнее, пожалуйста, и об оформлении", false)]
    public void Consent_is_recognized_only_from_a_short_positive_answer(string text, bool expected) => Assert.Equal(expected, RecruiterRules.GivesConsent(text));

    // ---------- Оценка ----------

    [PostgresFact]
    public async Task Scoring_marks_evidence_not_found_in_the_resume_as_unverified()
    {
        var (scope, db, hr, sales) = await StartAsync();
        using var _ = scope;
        var sp = scope.ServiceProvider;
        var vacancyId = await OpenVacancyAsync(sp, hr, sales);
        var service = sp.GetRequiredService<RecruitmentService>();
        var candidate = await service.CreateCandidateAsync(hr, new CandidateInput("Ботир Каримов", null, null, null, [], "manual", null, vacancyId, "written"), default);
        await service.UploadResumeAsync(hr, candidate.CandidateId, new RecruitmentUpload("cv.txt", "text/plain", 0,
            new MemoryStream(Encoding.UTF8.GetBytes("Водитель категории C, стаж 6 лет. Работал в Логистика Плюс."))) with { SizeBytes = 90 }, default);
        var applicationId = (await db.CandidateApplications.AsNoTracking().SingleAsync()).Id;

        fixture.Gateway.Script.Enqueue(m =>
        {
            Assert.Contains("<resume>", m[1].Content);
            return JsonSerializer.Serialize(new
            {
                criteria = new object[]
                {
                    new { name = "Опыт", score = 9, explanation = "Шесть лет за рулём", evidence = "стаж 6 лет" },
                    new { name = "Навыки", score = 10, explanation = "Знает город", evidence = "отлично знает весь Ташкент" },
                },
                summary = "Опытный водитель",
                risks = "Уточнить график",
            });
        });
        var score = await sp.GetRequiredService<ICandidateScorer>().ScoreAsync(hr, applicationId, default);
        Assert.True(score.Criteria.Single(c => c.Name == "Опыт").Verified);
        Assert.False(score.Criteria.Single(c => c.Name == "Навыки").Verified);
        Assert.Equal(0, score.Criteria.Single(c => c.Name == "Образование").Score); // модель не оценила — 0, а не выдумка
        Assert.Equal((int)Math.Round(new[] { 9, 10, 0, 0, 0, 0 }.Average() * 10), score.Overall);
        Assert.Equal(ApplicationStage.New, (await db.CandidateApplications.AsNoTracking().SingleAsync()).Stage); // оценка этап не меняет
        await Assert.ThrowsAsync<FieldForbiddenException>(() => sp.GetRequiredService<ICandidateScorer>().ScoreAsync(new RecruitmentContext(fixture.AdminId, true, false, []), applicationId, default));
    }

    // ---------- Собеседования, напоминания, аналитика ----------

    [PostgresFact]
    public async Task Free_slots_skip_busy_calendar_time_booking_rechecks_and_reminders_follow_ups_and_analytics_work()
    {
        var (scope, db, hr, sales) = await StartAsync();
        using var _ = scope;
        var sp = scope.ServiceProvider;
        var interviews = sp.GetRequiredService<InterviewService>();
        var vacancyId = await OpenVacancyAsync(sp, hr, sales, ai: true);
        await interviews.SetInterviewersAsync(hr, vacancyId, new InterviewersInput([fixture.AdminId]), default);
        await sp.GetRequiredService<RecruitmentAutomation>().SaveAiSettingsAsync(hr, new AiSettingsInput("10:00", "12:00", 60, "1,2,3,4,5,6,7", false), default);
        await EnableRecruiterAsync(sp);
        // Сейчас — 9:00 по Ташкенту; первый возможный слот — 11:00 (не раньше чем через 2 часа).
        var morning = new DateTimeOffset(2026, 11, 10, 4, 0, 0, TimeSpan.Zero);
        fixture.Clock.Now = morning;

        // Интервьюер занят в 11:00 — первый слот дня не предлагается, переходит на следующий день.
        db.CalendarEvents.Add(new CalendarEvent { Title = "Совещание", StartsAt = morning.AddHours(2), EndsAt = morning.AddHours(3), CreatedById = fixture.AdminId });
        await db.SaveChangesAsync();
        var slots = await interviews.FreeSlotsAsync(vacancyId, 3, 5, default);
        Assert.Equal(morning.AddDays(1).AddHours(1), slots[0].StartsAt); // завтра 10:00 Ташкента (05:00 UTC)
        Assert.Equal(3, slots.Select(s => s.StartsAt.Date).Distinct().Count());

        var service = sp.GetRequiredService<RecruitmentService>();
        var candidate = await service.CreateCandidateAsync(hr, new CandidateInput("Нодира Алиева", null, "uz", null, [], "manual", null, vacancyId, "written"), default);
        var applicationId = (await db.CandidateApplications.AsNoTracking().SingleAsync()).Id;
        await interviews.BookAsync(applicationId, slots[0].StartsAt, "office", "Офис, 2 этаж", fixture.AdminId, default);
        await Assert.ThrowsAsync<FieldConflictException>(() => interviews.BookAsync(applicationId, slots[0].StartsAt.AddMinutes(30), "office", null, fixture.AdminId, default));

        // Напоминание за сутки — только если есть диалог с кандидатом; без диалога отмечается, но не уходит.
        fixture.Clock.Now = slots[0].StartsAt.AddHours(-20);
        Assert.Equal(0, await interviews.RemindAsync(default));
        Assert.NotNull((await db.Interviews.AsNoTracking().SingleAsync()).RemindedAt);

        var analytics = await sp.GetRequiredService<RecruitmentAutomation>().AnalyticsAsync(hr, default);
        var row = analytics.Vacancies.Single(v => v.VacancyId == vacancyId);
        Assert.Equal((1, 1, 1), (row.Total, row.Stages["Interview"], row.Interviews));
        Assert.Contains(analytics.Sources, s => s.Source == "manual" && s.Candidates == 1);
        fixture.Clock.Now = null;
    }
}
