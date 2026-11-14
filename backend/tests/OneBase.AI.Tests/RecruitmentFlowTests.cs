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

    /// <summary>HH отвечает на список откликов «нужна оплата» (api_access_payment).</summary>
    public bool PaymentRequired;

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
            case ("GET", "/negotiations/response") when PaymentRequired:
                return Json(new { errors = new[] { new { type = "api_access_payment", value = "action_must_be_payed" } } }, HttpStatusCode.Forbidden);
            case ("GET", "/negotiations/response"):
                return Json(new
                {
                    items = new object[]
                    {
                        new
                        {
                            id = "n1", state = new { id = "response" }, updated_at = "2026-11-08T10:00:00+0500",
                            resume = new
                            {
                                id = "r1", first_name = "Иван", last_name = "Иванов", title = "Водитель", url = "https://api.hh.ru/resumes/r1?topic_id=n1",
                                alternate_url = "https://hh.uz/resume/r1", area = new { name = "Ташкент" }, salary = new { amount = 7000000, currency = "UZS" },
                                total_experience = new { months = 70 }, age = 33, gender = new { id = "male" },
                            },
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
/// HH, обновление токена), AI-рекрутер (выключатель, ответ без вопроса о согласии, просьба о человеке, ответ и выбор времени, неверный формат — человеку), оценка с
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
        await db.HhApiCalls.ExecuteDeleteAsync();
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
        var db = sp.GetRequiredService<OneBaseDbContext>();
        return new HhService(db, api, new HhQuotaGuard(db, fixture.Clock, NullLogger<HhQuotaGuard>.Instance), sp.GetRequiredService<IDataProtectionProvider>(),
            sp.GetRequiredService<RecruitmentService>(),
            sp.GetRequiredService<RecruitmentExceptions>(), sp.GetRequiredService<IAuditLogger>(), fixture.Clock, NullLogger<HhService>.Instance);
    }

    // ---------- HH.uz ----------

    [PostgresFact]
    public async Task Hh_connects_by_state_syncs_free_responses_without_spending_limits_and_guard_blocks_paid_operations()
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

        // Синхронизация: кандидаты — по бесплатному списку откликов (имя, должность, город, зарплата, стаж, ссылка на hh.uz); резюме через API не
        // загружается — просмотр расходует лимит HH: в журнале Quota Guard — заблокировано, запроса к /resumes нет. Пол и возраст не берутся.
        var result = await hh.SyncAsync(null, default);
        Assert.Equal((2, 0), (result.Responses, result.Errors));
        await fixture.Services.GetRequiredService<OneBase.Application.Recruitment.Jobs.RecruitmentJobRunner>().DrainAsync(default);
        var ivan = await db.Candidates.AsNoTracking().Include(c => c.Contacts).Include(c => c.Resumes).Include(c => c.Applications).Include(c => c.Sources)
            .SingleAsync(c => c.FullName == "Иванов Иван");
        Assert.Empty(ivan.Resumes);
        Assert.Empty(ivan.Contacts);
        Assert.Equal(("Водитель", "Ташкент", "7000000 UZS", 70), (ivan.DesiredPosition, ivan.City, ivan.SalaryExpectation, ivan.ExperienceMonths));
        Assert.Equal("https://hh.uz/resume/r1", Assert.Single(ivan.Sources).Note);
        Assert.DoesNotContain("33", ivan.Notes ?? string.Empty);
        Assert.Equal(vacancyId, Assert.Single(ivan.Applications).VacancyId);
        Assert.DoesNotContain(fake.Requests, r => r.Url.Contains("/resumes/", StringComparison.Ordinal));
        var calls = await db.HhApiCalls.AsNoTracking().ToListAsync();
        Assert.Equal(2, calls.Count(c => c.Operation == HhOperations.ResumeView && !c.Allowed && c.Cost == HhOperations.Limit));
        Assert.All(calls.Where(c => c.Allowed), c => Assert.Equal(HhOperations.Free, c.Cost));
        Assert.Contains("лимит просмотров", (await db.HhNegotiations.AsNoTracking().FirstAsync(n => n.NegotiationId == "n1")).LastError);

        // Повтор — без дублей и без новых попыток просмотра резюме.
        await hh.SyncAsync(null, default);
        Assert.Equal(1, await db.Candidates.CountAsync(c => c.FullName == "Иванов Иван"));
        Assert.Equal(2, await db.HhApiCalls.CountAsync(c => c.Operation == HhOperations.ResumeView));

        // Каждый запрос — с HH-User-Agent и почтой, host=hh.uz, к api.hh.ru; запросы с токеном — Bearer.
        Assert.All(fake.Requests, r => Assert.Equal("OneBase/1.0 (dev@example.com)", r.UserAgent));
        Assert.All(fake.Requests, r => Assert.StartsWith("https://api.hh.ru/", r.Url));
        Assert.All(fake.Requests.Where(r => !r.Url.Contains("/token", StringComparison.Ordinal)), r => Assert.StartsWith("Bearer access-", r.Auth));

        // Действие — только из списка HH и кнопкой HR; с текстом сообщения — заблокировано до отправки (сообщения через HH запрещены).
        var negotiation = Assert.Single(await hh.NegotiationsAsync(hr, ivan.Id, default));
        await Assert.ThrowsAsync<FieldValidationException>(() => hh.ActionAsync(hr, negotiation.Id, "discard", null, default));
        var before = fake.Requests.Count;
        await Assert.ThrowsAsync<FieldValidationException>(() => hh.ActionAsync(hr, negotiation.Id, "interview", "Приглашаем на собеседование", default));
        Assert.Equal(before, fake.Requests.Count);
        Assert.True(await db.HhApiCalls.AnyAsync(c => c.Operation == HhOperations.ActionMessage && !c.Allowed && c.Trigger == "manual"));
        await hh.ActionAsync(hr, negotiation.Id, "interview", null, default);
        Assert.Contains(fake.Requests, r => r.Method == HttpMethod.Put && r.Url.StartsWith("https://api.hh.ru/negotiations/interview/n1", StringComparison.Ordinal)
                                            && !r.Body.Contains("message=", StringComparison.Ordinal));

        // Токен истёк — обновляется (refresh), затем запрос идёт с новым.
        await db.HhConnections.ExecuteUpdateAsync(u => u.SetProperty(c => c.TokenExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
        db.ChangeTracker.Clear(); // в работе у каждого запроса свой контекст; здесь — тот же, что у сервиса
        await hh.SyncAsync(null, default);
        Assert.Equal("refresh", fake.LastGrant);
        Assert.Equal("Bearer access-2", fake.Requests[^1].Auth);

        // Предохранитель: HH ответил «нужна оплата» — ничего не списано, но синхронизация по расписанию в HH больше не ходит, пока HR не снимет.
        fake.PaymentRequired = true;
        var paid = await hh.SyncAsync(null, default);
        Assert.True(paid.Errors > 0);
        Assert.NotNull((await db.HhConnections.AsNoTracking().SingleAsync()).GuardTrippedAt);
        db.ChangeTracker.Clear();
        var sent = fake.Requests.Count;
        var stopped = await hh.SyncAsync(null, default);
        Assert.Equal(sent, fake.Requests.Count);
        Assert.Contains("предохранител", stopped.Message, StringComparison.OrdinalIgnoreCase);
        var guard = await hh.GuardAsync(hr, default);
        Assert.True(guard.FreeCalls30 > 0);
        Assert.True(guard.BlockedCalls30 >= 3);
        Assert.Contains(guard.Catalog, o => o.Code == HhOperations.ResumeSearch && !o.Auto && !o.Manual);
        Assert.Null((await hh.ResetGuardAsync(hr, default)).GuardTrippedAt);
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

        // Включили ИИ-агента «Рекрутер» («Настройки → ИИ-агенты») — отвечает сразу.
        await EnableRecruiterAsync(sp);

        // Согласия на обработку данных не спрашивает: на первое же сообщение отвечает модель (JSON: reply).
        fixture.Gateway.Script.Enqueue(m =>
        {
            Assert.Contains("Права категории C", m[1].Content); // вакансия — в меняющейся части промпта
            Assert.Contains("Есть ли у вас опыт", m[1].Content); // вопросы отбора агента
            Assert.Contains("не выдавай", m[0].Content);
            Assert.Contains("Согласия на обработку данных не спрашивай", m[0].Content);
            return "{\"action\":\"reply\",\"message\":\"Здравствуйте! Сколько лет вы работаете водителем?\",\"notes\":\"\"}";
        });
        await DeliverAsync(Msg(account, 2, "Расскажите про вакансию"));
        var candidateId = (await db.Candidates.AsNoTracking().SingleAsync()).Id;
        Assert.False(await db.CandidateConsents.AnyAsync(c => c.CandidateId == candidateId));
        Assert.Equal("Здравствуйте! Сколько лет Вы работаете водителем?", (await QueuedAsync(db))[^1]); // «Вы» — с большой буквы
        Assert.DoesNotContain(await QueuedAsync(db), t => t.Contains("Согласен"));

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

    [PostgresFact]
    public async Task Recruiter_knows_every_open_vacancy_of_onebase_and_signs_the_candidate_up_for_the_chosen_one()
    {
        var (scope, db, hr, sales) = await StartAsync();
        using var _ = scope;
        var sp = scope.ServiceProvider;
        var account = await HrAccountAsync(sp);
        var service = sp.GetRequiredService<RecruitmentService>();
        var hrDepartment = (await db.Departments.FirstAsync(d => d.Code == "hr")).Id;
        // Вакансии рекрутеров — без «Миссии AI-рекрутера»; черновик агент не знает.
        var driver = await OpenVacancyAsync(sp, hr, sales);
        var operatorVacancy = await service.CreateVacancyAsync(hr, new VacancyInput("Оператор call-центра", hrDepartment, "Приём звонков от клиентов",
            "Грамотная речь, русский и узбекский", "График 2/2 с 9:00 до 21:00, обучение", 4_500_000, null, "Ташкент", "shift", 2, null), default);
        await service.SetVacancyStatusAsync(hr, operatorVacancy.Id, VacancyStatus.Open, default);
        await service.CreateVacancyAsync(hr, new VacancyInput("Главный бухгалтер", hrDepartment, null, null, null, null, null, null, null, 1, null), default);
        var numbers = await db.Vacancies.AsNoTracking().ToDictionaryAsync(v => v.Id, v => v.Number);
        await EnableRecruiterAsync(sp);

        // С первого сообщения модель видит все открытые вакансии OneBase с отделом, занятостью, требованиями и условиями и вопросы отбора агента.
        fixture.Gateway.Script.Enqueue(m =>
        {
            var context = m[1].Content;
            Assert.Contains("Есть ли у вас опыт", context);
            Assert.Contains($"V{numbers[driver]} «Водитель-экспедитор» — отдел «Продажи», Ташкент, полная занятость", context);
            Assert.Contains("Права категории C, опыт от 2 лет", context);
            Assert.Contains($"V{numbers[operatorVacancy.Id]} «Оператор call-центра» — отдел «HR», Ташкент, сменный график, зарплата от 4500000 сум", context);
            Assert.Contains("Грамотная речь, русский и узбекский", context);
            Assert.Contains("График 2/2", context);
            Assert.DoesNotContain("Главный бухгалтер", context);
            Assert.Contains("не придумывай вакансий", m[0].Content);
            // Вакансии не перечисляет: спрашивает, по какой вакансии пишет кандидат; «какие есть?» — сначала опыт, по нему — подходящая.
            Assert.Contains("спроси, по какой вакансии он пишет", m[0].Content);
            Assert.Contains("спроси, в чём у него есть опыт", m[0].Content);
            Assert.Contains("Зарплату сам не называй", m[0].Content);
            Assert.Contains("не называй себя виртуальным", m[0].Content);
            Assert.Contains("не для перечисления", context);
            return "{\"action\":\"reply\",\"messages\":[\"Здравствуйте! Я рекрутер компании. Расскажите, в чём у вас есть опыт? Подберу подходящую вакансию.\"]}";
        });
        await DeliverAsync(Msg(account, 2, "Здравствуйте, какие у вас есть вакансии?"));
        Assert.Contains("в чём у Вас есть опыт", (await QueuedAsync(db))[^1]);

        // По опыту — подходящая вакансия (без зарплаты); кандидат пока ни на что не записан.
        fixture.Gateway.Script.Enqueue(_ => "{\"action\":\"reply\",\"messages\":[\"Вам подойдёт оператор call-центра в Ташкенте — приём звонков от клиентов. Интересно?\"]}");
        await DeliverAsync(Msg(account, 3, "2 года продавцом в магазине, много общался с клиентами"));
        Assert.Contains("оператор call-центра", (await QueuedAsync(db))[^1]);
        Assert.False(await db.CandidateApplications.AnyAsync());

        // Кандидат выбрал — модель вернула код вакансии, кандидат записан на неё.
        fixture.Gateway.Script.Enqueue(_ =>
            $"{{\"action\":\"reply\",\"messages\":[\"Отлично! Какой график вам удобен?\"],\"vacancy\":\"V{numbers[operatorVacancy.Id]}\"}}");
        await DeliverAsync(Msg(account, 4, "Да, хочу оператором"));
        Assert.Equal(operatorVacancy.Id, (await db.CandidateApplications.AsNoTracking().SingleAsync()).VacancyId);

        // Дальше — его вакансия целиком, остальные открытые — тоже с подробностями.
        fixture.Gateway.Script.Enqueue(m =>
        {
            Assert.Contains($"Вакансия кандидата V{numbers[operatorVacancy.Id]}: Оператор call-центра", m[1].Content);
            Assert.Contains("Занятость: сменный график", m[1].Content);
            Assert.Contains($"Другие открытые вакансии компании", m[1].Content);
            Assert.Contains($"V{numbers[driver]} «Водитель-экспедитор»", m[1].Content);
            Assert.Contains("Права категории C, опыт от 2 лет", m[1].Content);
            return "{\"action\":\"reply\",\"messages\":[\"Понял, спасибо!\"]}";
        });
        await DeliverAsync(Msg(account, 5, "2/2 подходит"));
        Assert.Equal("Понял, спасибо!", (await QueuedAsync(db))[^1]);
    }

    /// <summary>Рекрутер HR по умолчанию — включён, круглосуточно (ответы уходят сразу всегда).</summary>
    internal static async Task<DialogAgent> EnableRecruiterAsync(IServiceProvider sp, Action<DialogAgent>? configure = null)
    {
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var id = (await sp.GetRequiredService<DialogAgentService>().EnsureRecruiterAsync(default)).Id;
        var agent = await db.DialogAgents.SingleAsync(a => a.Id == id);
        (agent.Enabled, agent.WorkFrom, agent.WorkTo, agent.WorkDays, agent.Intro) = (true, null, null, "1,2,3,4,5,6,7", "рекрутер компании");
        configure?.Invoke(agent);
        await db.SaveChangesAsync();
        return agent;
    }

    [Theory]
    [InlineData("Можно поговорить с человеком?", true)]
    [InlineData("Позовите менеджера", true)]
    [InlineData("Работал менеджером по продажам 3 года", false)]
    [InlineData("Был оператором колл-центра", false)]
    [InlineData("Inson bilan gaplashsam bo'ladimi?", true)]
    public void Only_explicit_requests_for_a_human_hand_off(string text, bool expected) => Assert.Equal(expected, RecruiterRules.AsksForHuman(text));

    [Theory]
    [InlineData("Можно перенести на другой день?", false)]
    [InlineData("другое время", false)]
    [InlineData("Перенесите на четверг после обеда", true)]
    [InlineData("Можно в 15:00?", true)]
    [InlineData("Ertaga kelsam bo'ladimi?", true)]
    [InlineData("Эртага эрталаб бўладими?", true)]
    public void Named_day_or_time_goes_to_the_model_as_a_wish(string text, bool expected) => Assert.Equal(expected, RecruiterRules.MentionsWhen(text));

    [Fact]
    public void Time_wish_is_read_from_the_model_json_and_junk_is_dropped()
    {
        Assert.Equal(new SlotWish(new DateOnly(2026, 11, 19), null, new TimeOnly(15, 0)),
            RecruiterAgent.Parse("{\"action\":\"other_time\",\"messages\":[],\"wish\":{\"date\":\"2026-11-19\",\"time\":\"15:00\",\"from\":\"\",\"to\":\"\"}}")!.Wish);
        Assert.Equal(new SlotWish(new DateOnly(2026, 11, 23), new DateOnly(2026, 11, 27), null, new TimeOnly(9, 0), new TimeOnly(12, 0)),
            RecruiterAgent.Parse("{\"action\":\"other_time\",\"wish\":{\"date\":\"2026-11-23\",\"date_to\":\"2026-11-27\",\"from\":\"9:00\",\"to\":\"12:00\"}}")!.Wish);
        Assert.Null(RecruiterAgent.Parse("{\"action\":\"other_time\",\"wish\":{\"date\":\"в четверг\",\"time\":\"утром\"}}")!.Wish);
        Assert.Null(RecruiterAgent.Parse("{\"action\":\"reply\",\"messages\":[\"Здравствуйте\"]}")!.Wish);
    }

    // ---------- Оценка ----------

    [PostgresFact]
    public async Task Screening_scores_six_weighted_criteria_on_the_server_hides_protected_data_and_caps_unverified_points()
    {
        var (scope, db, hr, sales) = await StartAsync();
        using var _ = scope;
        var sp = scope.ServiceProvider;
        var vacancyId = await OpenVacancyAsync(sp, hr, sales);
        var service = sp.GetRequiredService<RecruitmentService>();
        var candidate = await service.CreateCandidateAsync(hr, new CandidateInput("Ботир Каримов", null, null, null, [], "manual", null, vacancyId, "written"), default);
        await service.UploadResumeAsync(hr, candidate.CandidateId, new RecruitmentUpload("cv.txt", "text/plain", 0,
            new MemoryStream(Encoding.UTF8.GetBytes("Водитель категории C, стаж 6 лет. Работал в Логистика Плюс.\nДата рождения: 01.01.1990"))) with { SizeBytes = 120 }, default);
        await fixture.Services.GetRequiredService<OneBase.Application.Recruitment.Jobs.RecruitmentJobRunner>().DrainAsync(default); // текст резюме — в фоне
        var applicationId = (await db.CandidateApplications.AsNoTracking().SingleAsync()).Id;

        fixture.Gateway.Script.Enqueue(m =>
        {
            Assert.Contains("<resume>", m[1].Content);
            Assert.Contains("Дата рождения: [скрыто]", m[1].Content); // защищённые признаки — не модели
            Assert.DoesNotContain("1990", m[1].Content);
            return JsonSerializer.Serialize(new
            {
                criteria = new object[]
                {
                    new { code = "experience", status = "met", points = 23, explanation = "Шесть лет за рулём", quotes = new[] { "стаж 6 лет" } },
                    new { code = "skills", status = "met", points = 24, explanation = "Знает город", quotes = new[] { "отлично знает весь Ташкент" } },
                    new { code = "requirements", status = "met", points = 18, explanation = "Категория C есть", quotes = new[] { "Водитель категории C" } },
                    new { code = "achievements", status = "not_stated", points = 0, explanation = "", quotes = Array.Empty<string>() },
                    new { code = "extras", status = "partial", points = 2, explanation = "Работал в логистике", quotes = Array.Empty<string>() },
                },
                summary = "Опытный водитель",
                whyFits = "Стаж и категория C",
                strengths = new[] { "Стаж 6 лет" },
                weaknesses = new[] { "Нет данных о маршрутах" },
                missingRequirements = new[] { "Медицинская книжка" },
                recommendation = "Пригласить на собеседование",
                interviewQuestions = Enumerable.Range(1, 12).Select(i => new { question = $"Вопрос {i}", type = i % 2 == 0 ? "technical" : "bogus", reason = "пробел" }),
                confidence = 1.7,
            });
        });
        var score = await sp.GetRequiredService<ICandidateScorer>().ScoreAsync(hr, applicationId, default);
        Assert.True(score.Criteria.Single(c => c.Name == "Опыт работы").Verified);
        Assert.False(score.Criteria.Single(c => c.Name == "Профессиональные навыки").Verified); // цитаты нет в резюме
        // Баллы на сервере: опыт 23 (подтверждён), навыки 24 → 12,5 (без подтверждения — не выше половины), требования 18, достижения не указаны → 7,5,
        // образование модель не оценила → 5 (нейтрально), преимущества 2.
        Assert.Equal(68, score.Overall);
        var row = await db.CandidateScores.AsNoTracking().SingleAsync();
        var analysis = OneBase.Application.Recruitment.Screening.ScreeningJson.View(row);
        Assert.Equal((2, "partial", "partial_match"), (analysis.Version, analysis.Category, analysis.Recommendation));
        Assert.Equal(12.5m, analysis.Criteria.Single(c => c.Code == "skills").Points);
        Assert.True(analysis.Criteria.Single(c => c.Code == "skills").Capped);
        Assert.Equal((7.5m, "Не указано в резюме"), (analysis.Criteria.Single(c => c.Code == "achievements").Points, analysis.Criteria.Single(c => c.Code == "achievements").Explanation));
        Assert.Equal(10, analysis.Questions.Count);
        Assert.All(analysis.Questions, q => Assert.Contains(q.Type, new[] { "technical", "experience" }));
        Assert.Equal((1m, 0.45m), (analysis.Confidence!.Value, analysis.Coverage!.Value)); // уверенность модели — отдельно и не больше 1
        Assert.Equal("Медицинская книжка", Assert.Single(analysis.Missing));
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
