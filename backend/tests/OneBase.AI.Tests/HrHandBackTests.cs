using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Llm;
using OneBase.AI.Recruitment;
using OneBase.Application.Recruitment;
using OneBase.Application.Recruitment.Telegram;
using OneBase.Domain.Recruitment;
using OneBase.Domain.Telegram;
using OneBase.Domain.Work;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>
/// Кандидат у HR: уведомление о передаче со счётчиком ждущих, напоминания HR раз в 15 минут, через час молчания HR диалог снова у AI (он сразу
/// отвечает на то, что кандидат написал, пока ждал), AI знает, что писал HR; отказ в принятом виде; «Вы» с большой буквы; обращение «Ака»/«Опа».
/// </summary>
public class HrHandBackTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private readonly HrHarness _hr = new(fixture, 9700);

    private async Task<(HrHarness.Chat Chat, Guid Conversation)> HandedOffAsync(OneBase.Infrastructure.Persistence.OneBaseDbContext db, int number)
    {
        var chat = _hr.NewChat();
        fixture.Gateway.Script.Enqueue(_ => "{\"action\":\"reply\",\"messages\":[\"Здравствуйте! Сколько лет вы водите грузовик?\"]}");
        await _hr.SayAsync(chat, $"Здравствуйте, я по вакансии #V{number}");
        await _hr.SayAsync(chat, "Можно поговорить с человеком?"); // без модели — сразу HR
        await _hr.SendQueuedAsync();
        return (chat, (await db.TelegramConversations.AsNoTracking().SingleAsync(c => c.PeerId == chat.Peer.PeerId)).Id);
    }

    private static async Task<Guid> RecruiterUserAsync(OneBase.Infrastructure.Persistence.OneBaseDbContext db)
    {
        var role = new OneBase.Domain.Identity.Role { Name = $"HR теста ожидания {Guid.NewGuid():N}" };
        role.Permissions.Add(new OneBase.Domain.Identity.RolePermission { Code = OneBase.Application.Security.Permissions.HrRecruitmentManage });
        var user = new OneBase.Domain.Identity.User { Login = $"hr-waiting-{Guid.NewGuid():N}", FullName = "Рекрутер Теста" };
        user.Roles.Add(new OneBase.Domain.Identity.UserRole { Role = role });
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    [PostgresFact]
    public async Task Hr_is_reminded_who_waits_and_an_hour_of_silence_returns_the_dialog_to_ai_which_knows_what_hr_wrote()
    {
        var (scope, db) = await _hr.StartAsync();
        using var _ = scope;
        var sp = scope.ServiceProvider;
        await db.UserNotifications.ExecuteDeleteAsync();
        var recruiter = await RecruiterUserAsync(db); // уведомления — тем, у кого право «Подбор персонала: управление»
        var vacancy = await _hr.OpenVacancyAsync(sp);
        var number = (await db.Vacancies.AsNoTracking().SingleAsync(v => v.Id == vacancy)).Number;
        var start = DateTimeOffset.UtcNow;
        fixture.Clock.Now = start;
        try
        {
            var (first, firstId) = await HandedOffAsync(db, number);
            Assert.Equal("Здравствуйте! Сколько лет Вы водите грузовик?", (await HrHarness.SentAsync(db, first))[0]); // «Вы» — с большой буквы
            var (second, secondId) = await HandedOffAsync(db, number);

            // Передача — уведомление HR со ссылкой на диалог и счётчиком: ждут двое.
            var handoff = await db.UserNotifications.AsNoTracking().Where(n => n.RecipientId == recruiter && n.Link == $"/hr/recruitment/telegram/{secondId}")
                .SingleAsync();
            Assert.Equal("Кандидат Тестов ждёт ответа HR · кандидат просит человека", handoff.Title);
            Assert.Contains("Сейчас ждут ответа HR: 2 кандидата. Не ответите в течение 60 минут — диалог вернётся к AI-рекрутеру.", handoff.Body);

            // Только что передали — без напоминания; через 20 минут — напоминание, через 25 — ещё рано (раз в 15 минут).
            var waiting = sp.GetRequiredService<HrWaiting>();
            fixture.Clock.Now = start.AddMinutes(10);
            Assert.Equal((0, 0), await waiting.RunAsync(default));
            fixture.Clock.Now = start.AddMinutes(20);
            Assert.True((await waiting.RunAsync(default)).Reminded > 0);
            var reminder = await db.UserNotifications.AsNoTracking().SingleAsync(n => n.RecipientId == recruiter && n.Link == HrWaiting.RemindersLink);
            Assert.Equal(("Ждут ответа HR в Telegram: 2 кандидата", UserNotificationKind.RecruitmentAlert), (reminder.Title, reminder.Kind));
            Assert.StartsWith("Дольше всех ждёт Кандидат Тестов — 20 мин.", reminder.Body);
            fixture.Clock.Now = start.AddMinutes(25);
            Assert.Equal(0, (await waiting.RunAsync(default)).Reminded);

            // HR ответил второму — он больше не ждёт: следующее напоминание — про одного, прежнее непрочитанное — прочитано.
            fixture.Clock.Now = start.AddMinutes(30);
            await sp.GetRequiredService<RecruitmentTelegramService>().SendAsync(_hr.Hr, secondId,
                new SendInput("Здравствуйте! Я из HR. Зарплату обсудим на собеседовании — приходите в пятницу.", Guid.NewGuid()), default);
            await _hr.SendQueuedAsync();
            fixture.Clock.Now = start.AddMinutes(40);
            await waiting.RunAsync(default);
            var reminders = await db.UserNotifications.AsNoTracking().Where(n => n.RecipientId == recruiter && n.Link == HrWaiting.RemindersLink)
                .OrderBy(n => n.CreatedAt).ToListAsync();
            Assert.Equal(["Ждут ответа HR в Telegram: 2 кандидата", "Ждут ответа HR в Telegram: 1 кандидат"], reminders.Select(r => r.Title));
            Assert.Equal((true, false), (reminders[0].ReadAt is not null, reminders[1].ReadAt is not null));

            // Первый кандидат пишет, пока ждёт; через 59 минут после передачи диалог ещё у HR, через 61 — снова у AI, и AI сразу отвечает.
            fixture.Clock.Now = start.AddMinutes(45);
            await _hr.SayAsync(first, "Мне кто-нибудь ответит? Какой график работы?");
            fixture.Clock.Now = start.AddMinutes(59);
            Assert.Equal(0, (await waiting.RunAsync(default)).Returned);
            fixture.Clock.Now = start.AddMinutes(61);
            fixture.Gateway.Script.Enqueue(m =>
            {
                Assert.Contains(RecruiterAgent.ResumeNote, m[^1].Content);
                Assert.Contains(m, x => x.Role == LlmRole.User && x.Content.Contains("Какой график работы?"));
                return "{\"action\":\"reply\",\"messages\":[\"Извините за ожидание! График — пн–сб, 9:00–18:00. вам подходит?\"]}";
            });
            Assert.Equal(1, (await waiting.RunAsync(default)).Returned);
            var conversations = await db.TelegramConversations.AsNoTracking().Where(c => c.Id == firstId || c.Id == secondId).ToDictionaryAsync(c => c.Id, c => c.Mode);
            Assert.Equal((TelegramConversationMode.Ai, TelegramConversationMode.Manual), (conversations[firstId], conversations[secondId]));
            Assert.Equal("Извините за ожидание! График — пн–сб, 9:00–18:00. Вам подходит?", (await HrHarness.SentAsync(db, first))[^1]);
            var resolved = await db.RecruitmentExceptions.AsNoTracking().SingleAsync(e => e.ConversationId == firstId);
            Assert.Equal((RecruitmentExceptionStatus.Resolved, (Guid?)null), (resolved.Status, resolved.ResolvedById));
            Assert.Equal("HR не ответил за 60 минут — диалог вернулся к AI-рекрутеру", resolved.Resolution);
            Assert.Contains((await sp.GetRequiredService<HrDialogService>().GetAsync(_hr.Hr, firstId, default)).Items,
                i => i.Event == "returned_to_ai" && i.Text == "Диалог вернулся к AI — HR не ответил за час");

            // Второй: час с ответа HR — снова у AI; кандидат после HR не писал — AI молчит до его сообщения.
            fixture.Clock.Now = start.AddMinutes(91);
            var calls = fixture.Gateway.Calls.Count;
            Assert.Equal(1, (await waiting.RunAsync(default)).Returned);
            Assert.Equal(calls, fixture.Gateway.Calls.Count);

            // Кандидат пишет — AI видит, что писал HR (пометка сотрудника, не реплика кандидата и не своя), и продолжает от этого.
            fixture.Gateway.Script.Enqueue(m =>
            {
                var hr = Assert.Single(m, x => x.Content.StartsWith(RecruiterAgent.HrMark, StringComparison.Ordinal));
                Assert.Equal(LlmRole.User, hr.Role);
                Assert.StartsWith("[Сотрудник HR Администратор написал кандидату", hr.Content);
                Assert.Contains("приходите в пятницу", hr.Content);
                Assert.Contains("пометка «[Сотрудник HR", m[0].Content);
                Assert.Contains("на «Вы» с большой буквы", m[1].Content);
                return "{\"action\":\"reply\",\"messages\":[\"Как и сказала коллега, ждём вас в пятницу в 10:00.\"]}";
            });
            await _hr.SayAsync(second, "А во сколько в пятницу?");
            Assert.Equal("Как и сказала коллега, ждём Вас в пятницу в 10:00.", (await HrHarness.SentAsync(db, second))[^1]);
        }
        finally
        {
            fixture.Clock.Now = null;
        }
    }

    [PostgresFact]
    public async Task Rejected_candidate_gets_the_company_rejection_wording_from_hr_template_and_from_ai()
    {
        var (scope, db) = await _hr.StartAsync();
        using var _ = scope;
        var sp = scope.ServiceProvider;
        var vacancy = await _hr.OpenVacancyAsync(sp);
        var number = (await db.Vacancies.AsNoTracking().SingleAsync(v => v.Id == vacancy)).Number;
        var chat = _hr.NewChat();
        fixture.Gateway.Script.Enqueue(_ => "{\"action\":\"reply\",\"messages\":[\"Здравствуйте! Сколько лет Вы водите грузовик?\"]}");
        await _hr.SayAsync(chat, $"Здравствуйте, я по вакансии #V{number}");
        var conversation = (await db.TelegramConversations.AsNoTracking().SingleAsync(c => c.PeerId == chat.Peer.PeerId)).Id;

        // Шаблон HR «Отказ — вежливо»: обращение «Ака» (фамилия на -ов), текст компании, «Вы» с большой буквы.
        var expected = "Кандидат Ака,\nБольшое спасибо за интерес к нашей компании! К сожалению, сейчас мы не готовы пригласить Вас на следующий этап. " +
                       "Ценим Ваше внимание и будем рады взаимодействию в будущем. Наше общение сохранится в нашей базе, и в случае потребности в Вашем опыте " +
                       "мы обязательно обратимся.";
        Assert.Equal(expected, (await sp.GetRequiredService<HrDialogService>().GetAsync(_hr.Hr, conversation, default)).Templates.Single(t => t.Key == "reject").Text);

        // Отказали — кандидат спрашивает о результате: AI знает об отказе и отвечает по образцу компании, без причин.
        var application = await db.CandidateApplications.AsNoTracking().SingleAsync();
        await sp.GetRequiredService<RecruitmentService>().ChangeStageAsync(_hr.Hr, application.Id, new StageInput(ApplicationStage.Rejected, null, "Нет категории C"), default);
        fixture.Gateway.Script.Enqueue(m =>
        {
            Assert.Contains("Отклик кандидата на вакансию «Водитель-экспедитор» закрыт", m[1].Content);
            Assert.Contains(expected, m[1].Content);
            Assert.DoesNotContain("Нет категории C", m[1].Content); // причина — для истории, кандидату её не называют
            return $"{{\"action\":\"reply\",\"messages\":[{System.Text.Json.JsonSerializer.Serialize(expected)}]}}";
        });
        await _hr.SayAsync(chat, "Есть новости по моей кандидатуре?");
        Assert.Equal(expected, (await HrHarness.SentAsync(db, chat))[^1]);
    }

    [Theory]
    [InlineData("Сардор Давлатов", "Сардор Ака")]
    [InlineData("Рахимова Дилноза", "Дилноза Опа")]
    [InlineData("Sardor Rahimov", "Sardor Aka")]
    [InlineData("Dilnoza", "Dilnoza Opa")]
    [InlineData("Нилуфар", "Нилуфар Опа")]
    [InlineData("Гулнора", "Гулнора Опа")]
    [InlineData("Karimov Jasur Akmalovich", "Jasur Aka")]
    [InlineData("Aliyeva Malika Rustam qizi", "Malika Opa")]
    [InlineData("shahzod", "Shahzod Aka")]
    [InlineData("Yahyo", "Yahyo Aka")]
    [InlineData("Bako", "Bako")] // пол по имени не понятен — только имя
    [InlineData("@sardor_d", null)]
    [InlineData("Telegram 12345", null)]
    [InlineData("Каримов", null)]
    public void Candidate_is_addressed_by_first_name_with_aka_or_opa(string fullName, string? expected) =>
        Assert.Equal(expected, CandidateNames.Address(fullName));

    [Fact]
    public void Rejection_follows_the_company_wording_in_every_language_and_greets_when_there_is_no_name()
    {
        Assert.StartsWith("Здравствуйте!\nБольшое спасибо за интерес к нашей компании!", RecruiterTexts.PoliteRejection(CandidateLang.Ru, null));
        Assert.StartsWith("Sardor Aka,\nKompaniyamizga qiziqish bildirganingiz uchun katta rahmat! Afsuski, hozircha Sizni keyingi bosqichga",
            RecruiterTexts.PoliteRejection(CandidateLang.UzLatn, "Sardor Aka"));
        Assert.StartsWith("Сардор Ака,\nКомпаниямизга қизиқиш", RecruiterTexts.PoliteRejection(CandidateLang.UzCyrl, "Сардор Ака"));
    }

    [Theory]
    [InlineData("Сколько лет вы работаете? Ваш опыт нам интересен, вам удобно с вами созвониться?",
        "Сколько лет Вы работаете? Ваш опыт нам интересен, Вам удобно с Вами созвониться?")]
    [InlineData("Передаю вашу анкету, ждём вас", "Передаю Вашу анкету, ждём Вас")]
    [InlineData("Вывоз товара и выходные — по графику", "Вывоз товара и выходные — по графику")]
    public void Polite_you_is_always_capitalized_in_russian(string text, string expected) => Assert.Equal(expected, RecruiterRules.PoliteYou(text));
}
