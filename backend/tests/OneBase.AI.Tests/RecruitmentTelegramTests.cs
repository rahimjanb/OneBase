using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Recruitment;
using OneBase.Application.Recruitment.Telegram;
using OneBase.Application.Telegram;
using OneBase.Application.Telegram.Outbox;
using OneBase.Application.Telegram.Routing;
using OneBase.Domain.Identity;
using OneBase.Domain.Recruitment;
using OneBase.Domain.Telegram;
using OneBase.Infrastructure.Persistence;
using OneBase.Infrastructure.Telegram;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>
/// Telegram для HR (этап 3) на подставном Telegram: диалог с кандидатом (кандидат по подтверждённому Telegram ID, история, резюме из
/// вложения, код вакансии), очередь исходящих (пауза, лимит, FLOOD_WAIT, PEER_FLOOD, «неизвестно», перезапуск), публикации со ссылкой,
/// ответ на публикацию и лиды из групп-источников, удаление переписки вместе с данными кандидата. Ничего не уходит в настоящий Telegram.
/// </summary>
public class RecruitmentTelegramTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly TelegramPeerRef Candidate = new(7001, TelegramChatType.Private, false, 71);
    private static readonly TelegramPeerRef Jobs = new(9101, TelegramChatType.Group, true, 91);

    private TelegramIncoming Msg(Guid account, TelegramPeerRef chat, int id, long from, string text, TelegramIncomingFile? file = null, int? replyTo = null,
        bool outgoing = false) =>
        new(account, chat, chat == Jobs ? "Работа в Ташкенте" : "Алишер", id, from, from == 7001 ? "alisher_k" : "dilshod_t", from == 7001 ? "Алишер Каримов" : "Дилшод",
            text, fixture.Clock.GetUtcNow(), outgoing, false, file, false, replyTo);

    private async Task<(IServiceScope Scope, OneBaseDbContext Db, Guid Account, RecruitmentContext Hr)> StartAsync()
    {
        await fixture.ResetAsync();
        var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        await db.Candidates.ExecuteDeleteAsync();
        await db.Vacancies.ExecuteDeleteAsync();
        await db.TelegramLeads.ExecuteDeleteAsync();
        await db.RecruitmentSources.ExecuteDeleteAsync();
        foreach (var (code, name) in new[] { ("sales", "Продажи"), ("hr", "HR") })
        {
            if (!await db.Departments.AnyAsync(d => d.Code == code))
            {
                db.Departments.Add(new Department { Code = code, Name = name });
            }
        }

        await db.SaveChangesAsync();
        fixture.Telegram.Dialogs.AddRange([new TelegramDialog(Jobs, "Работа в Ташкенте", "rabota_tashkent", true)]);
        fixture.Telegram.Me = new TelegramMe(777_000_222, "onebase_hr", "HR");
        var accounts = sp.GetRequiredService<TelegramAccountService>();
        await accounts.SaveCredentialsAsync(123456, "0123456789abcdef0123456789abcdef", fixture.AdminId);
        var account = await accounts.CreateAccountAsync("HR", fixture.AdminId);
        await accounts.StartLoginAsync(account.Id, "+998 90 000 00 02", fixture.AdminId);
        await accounts.SubmitCodeAsync(account.Id, fixture.Telegram.Code, fixture.AdminId);
        await sp.GetRequiredService<TelegramDepartmentService>().SaveAsync(account.Id, new TelegramDepartmentsInput(["hr"], "hr"), fixture.AdminId);
        await db.TelegramAccountDepartments.Where(d => d.AccountId == account.Id).ExecuteUpdateAsync(u => u.SetProperty(d => d.MinIntervalSeconds, 5));
        return (scope, db, account.Id, new RecruitmentContext(fixture.AdminId, true, true, []));
    }

    /// <summary>Как в работе: маршрутизатор → обработчик подразделения (каждое сообщение — своя область DI).</summary>
    private async Task DeliverAsync(TelegramIncoming m)
    {
        using var scope = fixture.Services.CreateScope();
        var route = await scope.ServiceProvider.GetRequiredService<TelegramMessageRouter>().RouteAsync(m, default);
        var handler = scope.ServiceProvider.GetServices<ITelegramDepartmentHandler>().Single(h => h.Department == route.Department);
        await handler.HandleAsync(m, route, default);
    }

    private async Task<int> RunOutboxAsync()
    {
        using var scope = fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<TelegramOutboxRunner>().RunOnceAsync(default);
    }

    private async Task<Guid> OpenVacancyAsync(IServiceProvider sp, RecruitmentContext hr)
    {
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var sales = await db.Departments.FirstAsync(d => d.Code == "sales");
        var service = sp.GetRequiredService<RecruitmentService>();
        var vacancy = await service.CreateVacancyAsync(hr, new VacancyInput("Водитель-экспедитор", sales.Id, null, "Права категории C", "Пн–сб", 6_000_000, null, "Ташкент",
            "full", 1, null), default);
        await service.SetVacancyStatusAsync(hr, vacancy.Id, VacancyStatus.Open, default);
        return vacancy.Id;
    }

    [PostgresFact]
    public async Task Candidate_writing_to_the_hr_number_gets_a_card_history_resume_and_vacancy()
    {
        var (scope, db, account, hr) = await StartAsync();
        using var _ = scope;
        var vacancyId = await OpenVacancyAsync(scope.ServiceProvider, hr);
        var number = (await db.Vacancies.AsNoTracking().SingleAsync(v => v.Id == vacancyId)).Number;

        await DeliverAsync(Msg(account, Candidate, 1, 7001, $"Здравствуйте, я по вакансии #V{number}"));
        await DeliverAsync(Msg(account, Candidate, 1, 7001, $"Здравствуйте, я по вакансии #V{number}")); // повторная доставка
        fixture.Telegram.Files[(Candidate.PeerId, 2)] = RecruitmentTests.Pdf("Driver, category C, 7 years");
        await DeliverAsync(Msg(account, Candidate, 2, 7001, "Моё резюме", new TelegramIncomingFile("cv.pdf", "application/pdf", 500)));
        await DeliverAsync(Msg(account, Candidate, 3, 777_000_222, "Ответил с телефона", outgoing: true));

        var candidate = await db.Candidates.AsNoTracking().Include(c => c.Contacts).Include(c => c.Applications).Include(c => c.Resumes).SingleAsync();
        Assert.Equal("Алишер Каримов", candidate.FullName);
        Assert.Contains(candidate.Contacts, c => c.Kind == CandidateContactKind.Telegram && c.Value == "7001" && c.Verified);
        Assert.Contains(candidate.Contacts, c => c.Kind == CandidateContactKind.TelegramUsername && c.Value == "alisher_k" && c.Verified);
        Assert.Equal(vacancyId, Assert.Single(candidate.Applications).VacancyId);
        Assert.Contains("Driver, category C", Assert.Single(candidate.Resumes).Text);

        var conversation = await db.TelegramConversations.AsNoTracking().SingleAsync(c => c.AccountId == account && c.PeerId == Candidate.PeerId);
        Assert.Equal(("hr", "candidate", candidate.Id), (conversation.Department, conversation.RelatedType, conversation.RelatedId!.Value));
        var messages = await db.TelegramConversationMessages.AsNoTracking().Where(m => m.ConversationId == conversation.Id).OrderBy(m => m.MessageId).ToListAsync();
        Assert.Equal([("candidate", 1), ("candidate", 2), ("phone", 3)], messages.Select(m => (m.Author, m.MessageId!.Value)));
        Assert.NotNull(messages[1].ResumeId);
        Assert.Empty(fixture.Telegram.Sent); // AI-рекрутера в этом тесте нет — сам никто не отвечает

        // Удаление данных кандидата удаляет и переписку.
        await scope.ServiceProvider.GetRequiredService<RecruitmentService>().DeleteCandidateAsync(hr, candidate.Id, default);
        Assert.False(await db.TelegramConversationMessages.AnyAsync(m => m.ConversationId == conversation.Id));
        Assert.False(await db.TelegramConversations.AnyAsync(c => c.Id == conversation.Id));
    }

    [PostgresFact]
    public async Task Outbox_paces_the_hr_number_and_handles_flood_peer_flood_and_unknown()
    {
        var (scope, db, account, hr) = await StartAsync();
        using var _ = scope;
        var service = scope.ServiceProvider.GetRequiredService<RecruitmentTelegramService>();
        await DeliverAsync(Msg(account, Candidate, 1, 7001, "Добрый день, есть вакансии?"));
        var conversation = (await service.ConversationsAsync(hr, account, null, default)).Single();
        var start = new DateTimeOffset(2026, 11, 9, 6, 0, 0, TimeSpan.Zero);
        fixture.Clock.Now = start;

        // Ручной ответ: повтор нажатия — одно сообщение; AI в диалоге выключается (ведёт человек).
        var key = Guid.NewGuid();
        await service.SendAsync(hr, conversation.Id, new SendInput("Здравствуйте! Да, есть.", key), default);
        var view = await service.SendAsync(hr, conversation.Id, new SendInput("Здравствуйте! Да, есть.", key), default);
        Assert.Equal("Manual", view.Mode);
        Assert.Equal(1, await RunOutboxAsync());
        Assert.Equal("Здравствуйте! Да, есть.", Assert.Single(fixture.Telegram.Sent).Message.Text);
        view = await service.ConversationAsync(hr, conversation.Id, default);
        Assert.Contains(view.Messages, m => m.Direction == "Out" && m.Author == "user" && m.Status == "Sent");

        // Пауза номера: следующее — не раньше чем через 5 с.
        await service.SendAsync(hr, conversation.Id, new SendInput("Пришлите резюме, пожалуйста.", Guid.NewGuid()), default);
        Assert.Equal(0, await RunOutboxAsync());
        fixture.Clock.Now = start.AddSeconds(6);
        Assert.Equal(1, await RunOutboxAsync());

        // FLOOD_WAIT — ждём, попыткой не считается; затем «неизвестно, дошло ли» — без автоповтора.
        var attempt = 0;
        fixture.Telegram.Fail = (_, _) => ++attempt == 1
            ? new TelegramException("FLOOD_WAIT 30", TelegramErrorKind.Flood, 30)
            : new TelegramException("Telegram не подтвердил отправку за 30 с — неизвестно, дошло ли сообщение; проверьте чат.", TelegramErrorKind.Permanent);
        await service.SendAsync(hr, conversation.Id, new SendInput("Когда удобно созвониться?", Guid.NewGuid()), default);
        fixture.Clock.Now = start.AddSeconds(20);
        Assert.Equal(0, await RunOutboxAsync());
        var row = await db.TelegramOutgoingMessages.AsNoTracking().SingleAsync(m => m.Text == "Когда удобно созвониться?");
        Assert.Equal((TelegramOutboxStatus.Pending, 0), (row.Status, row.Attempts));
        fixture.Clock.Now = start.AddSeconds(60);
        await RunOutboxAsync();
        Assert.Equal(TelegramOutboxStatus.Unknown, (await db.TelegramOutgoingMessages.AsNoTracking().SingleAsync(m => m.Id == row.Id)).Status);

        // PEER_FLOOD — отправка HR с номера остановлена на сутки.
        fixture.Telegram.Fail = (_, _) => TelegramRules.Classify(400, "PEER_FLOOD", 0);
        await service.SendAsync(hr, conversation.Id, new SendInput("Ещё вопрос", Guid.NewGuid()), default);
        fixture.Clock.Now = start.AddSeconds(120);
        await RunOutboxAsync();
        var settings = await db.TelegramAccountDepartments.AsNoTracking().SingleAsync(d => d.AccountId == account);
        Assert.Equal(start.AddSeconds(120) + TelegramOutboxRunner.PeerFloodPause, settings.PausedUntil);
        fixture.Telegram.Fail = null;
        await service.SendAsync(hr, conversation.Id, new SendInput("После паузы", Guid.NewGuid()), default);
        fixture.Clock.Now = start.AddSeconds(200);
        Assert.Equal(0, await RunOutboxAsync());
        Assert.True((await service.AccountsAsync(hr, default)).Single().PausedUntil > start);

        // Перезапуск посреди отправки — «неизвестно», без автоповтора.
        await db.TelegramOutgoingMessages.Where(m => m.Text == "После паузы").ExecuteUpdateAsync(u => u.SetProperty(m => m.Status, TelegramOutboxStatus.Sending));
        await scope.ServiceProvider.GetRequiredService<TelegramOutboxRunner>().RecoverAsync(default);
        Assert.Equal(TelegramOutboxStatus.Unknown, (await db.TelegramOutgoingMessages.AsNoTracking().SingleAsync(m => m.Text == "После паузы")).Status);
        fixture.Clock.Now = null;
    }

    [PostgresFact]
    public async Task Vacancy_is_published_with_a_link_and_replies_and_job_posts_become_leads()
    {
        var (scope, db, account, hr) = await StartAsync();
        using var _ = scope;
        var vacancyId = await OpenVacancyAsync(scope.ServiceProvider, hr);
        var service = scope.ServiceProvider.GetRequiredService<RecruitmentTelegramService>();
        var chats = await service.PublicationChatsAsync(hr, account, default);
        var group = chats.Single(c => c.Title == "Работа в Ташкенте");
        var text = await service.TemplateAsync(hr, vacancyId, account, default);
        Assert.Contains("Водитель-экспедитор", text);
        Assert.Contains("@onebase_hr", text);
        Assert.Contains("#V", text);

        // Публикация — через очередь; ссылка — по имени группы.
        await service.PublishAsync(hr, new PublicationInput(vacancyId, account, [group.Id], text, null), default);
        Assert.Equal(1, await RunOutboxAsync());
        var publication = Assert.Single(await service.PublicationsAsync(hr, vacancyId, default));
        Assert.Equal("Sent", publication.Status);
        var messageId = (await db.VacancyPublications.AsNoTracking().SingleAsync()).MessageId!.Value;
        Assert.Equal($"https://t.me/rabota_tashkent/{messageId}", publication.Link);

        // Ответ на публикацию в группе — лид «ответ на вакансию»; человеку никто сам не пишет.
        await DeliverAsync(Msg(account, Jobs, 501, 7002, "Интересно, какой график?", replyTo: messageId));
        // Пост «ищу работу» — лид, только если группа отмечена источником.
        await DeliverAsync(Msg(account, Jobs, 502, 7003, "Ищу работу водителем, стаж 10 лет"));
        Assert.Single(await service.LeadsAsync(hr, LeadStatus.New, default));
        await service.AddSourceAsync(hr, new SourceInput(account, group.Id, "jobs", true, null), default);
        await DeliverAsync(Msg(account, Jobs, 503, 7003, "Ищу работу водителем, стаж 10 лет"));
        await DeliverAsync(Msg(account, Jobs, 504, 7004, "Продаю диван"));
        var leads = await service.LeadsAsync(hr, LeadStatus.New, default);
        Assert.Equal(2, leads.Count);
        Assert.Contains(leads, l => l.Keywords.Contains("ответ на вакансию"));
        Assert.Contains(leads, l => l.Keywords.Contains("ищу работу"));
        Assert.Single(fixture.Telegram.Sent); // только сама публикация

        // HR решил связаться — кандидат с подтверждённым Telegram ID и откликом на вакансию.
        var lead = leads.Single(l => l.Keywords.Contains("ищу работу"));
        var candidateId = await service.ConvertLeadAsync(hr, lead.Id, new LeadConvertInput(vacancyId), scope.ServiceProvider.GetRequiredService<RecruitmentService>(), default);
        var card = await scope.ServiceProvider.GetRequiredService<RecruitmentService>().CandidateAsync(hr, candidateId, default);
        Assert.Contains(card.Contacts, c => c.Kind == "Telegram" && c.Value == "7003" && c.Verified);
        Assert.Single(card.Applications);
    }

    [PostgresFact]
    public async Task On_a_shared_number_a_vacancy_code_claims_the_dialog_for_hr()
    {
        var (scope, db, account, hr) = await StartAsync();
        using var _ = scope;
        await scope.ServiceProvider.GetRequiredService<TelegramDepartmentService>().SaveAsync(account, new TelegramDepartmentsInput(["sales", "hr"], "sales"), fixture.AdminId);
        var vacancyId = await OpenVacancyAsync(scope.ServiceProvider, hr);
        var number = (await db.Vacancies.AsNoTracking().SingleAsync(v => v.Id == vacancyId)).Number;
        var router = scope.ServiceProvider.GetRequiredService<TelegramMessageRouter>();

        Assert.Equal(("hr", "claim"), Pair(await router.RouteAsync(Msg(account, Candidate, 1, 7001, $"Хочу на V{number}"), default)));
        var other = new TelegramPeerRef(7005, TelegramChatType.Private, false, 75);
        Assert.Equal(("sales", "default"), Pair(await router.RouteAsync(Msg(account, other, 1, 7005, "Есть ли скидки на товар?"), default)));
        var closed = new TelegramPeerRef(7006, TelegramChatType.Private, false, 76);
        Assert.Equal(("sales", "default"), Pair(await router.RouteAsync(Msg(account, closed, 1, 7006, "Хочу на V999999"), default)));
    }

    private static (string? Department, string Reason) Pair(TelegramRoute route) => (route.Department, route.Reason);

    [Theory]
    [InlineData("Я по вакансии #V12", new[] { 12 })]
    [InlineData("v7 и В8", new[] { 7, 8 })]
    [InlineData("модель IV12X и COVID19", new int[0])]
    public void Vacancy_codes_are_found_in_text(string text, int[] expected) => Assert.Equal(expected, RecruitmentTelegramRules.VacancyCodes(text));

    [Fact]
    public void Job_search_phrases_are_matched_in_russian_and_uzbek()
    {
        Assert.Contains("ищу работу", RecruitmentTelegramRules.LeadMatches("Добрый день! Ищу работу продавцом"));
        Assert.Contains("ish qidiryapman", RecruitmentTelegramRules.LeadMatches("Assalomu alaykum, ish qidiryapman"));
        Assert.Empty(RecruitmentTelegramRules.LeadMatches("Продаю диван"));
    }
}
