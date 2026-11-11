using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Recruitment;
using OneBase.Application.Recruitment;
using OneBase.Application.Telegram;
using OneBase.Application.Telegram.Routing;
using OneBase.Domain.Identity;
using OneBase.Domain.Recruitment;
using OneBase.Domain.Telegram;
using OneBase.Infrastructure.Persistence;
using OneBase.Infrastructure.Telegram;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>Язык и графика кандидата: определение по сообщению, закрепление, явная смена (без базы).</summary>
public class CandidateLanguageTests
{
    private static readonly LanguageDetectionService Languages = new();

    [Theory]
    [InlineData("Assalomu alaykum, ish haqida ma'lumot bersangiz.", "uz-Latn")]
    [InlineData("Ассалому алайкум, иш ҳақида маълумот берсангиз.", "uz-Cyrl")]
    [InlineData("Здравствуйте, расскажите подробнее о вакансии.", "ru-Cyrl")]
    [InlineData("Salom, qachon suhbat bo'ladi?", "uz-Latn")]
    [InlineData("Салом, қачон суҳбат бўлади?", "uz-Cyrl")]
    [InlineData("Добрый день! Какой график работы?", "ru-Cyrl")]
    public void Examples_from_the_task_are_recognized_with_language_and_script(string text, string code)
    {
        var d = Languages.Detect(text);
        Assert.Equal(code, d.Code);
        Assert.True(d.Confidence >= 0.85, $"уверенность {d.Confidence}");
    }

    [Theory]
    [InlineData("ok")]
    [InlineData("1")]
    [InlineData("👍")]
    [InlineData("Sardor")]
    [InlineData("да")]
    [InlineData("ha")]
    [InlineData("ҳа")]
    public void Short_and_ambiguous_messages_are_not_confident(string text) =>
        Assert.True(Languages.Detect(text).Confidence < LanguageDetectionService.SteadyConfidence);

    [Theory]
    [InlineData("Здравствуйте, ish haqida ma'lumot bersangiz", "uz-Latn")] // приветствие по-русски, суть — узбекская латиница
    [InlineData("Ассалому алайкум, хочу узнать про вакансию водителя", "ru-Cyrl")] // приветствие по-узбекски, суть — русский
    [InlineData("Меня зовут Sardor Davlatov, работал водителем 5 лет", "ru-Cyrl")] // имя латиницей — не язык
    [InlineData("Знаю Excel, 1C и SQL, опыт 3 года", "ru-Cyrl")] // термины латиницей
    [InlineData("Salom, men Excel va 1C ni bilaman", "uz-Latn")]
    [InlineData("Мен Excel ва 1C ни биламан, тажрибам бор", "uz-Cyrl")]
    [InlineData("Privet, rabota eshe est?", "ru-Cyrl")] // русский транслитом — отвечаем по-русски
    public void Mixed_messages_names_and_terms_are_handled(string text, string code) => Assert.Equal(code, Languages.Detect(text).Code);

    [Theory]
    [InlineData("Iltimos, kirillda yozing", CandidateLang.UzCyrl)]
    [InlineData("Кириллда ёзинг илтимос", CandidateLang.UzCyrl)]
    [InlineData("Lotincha yozsangiz", CandidateLang.UzLatn)]
    [InlineData("Пишите, пожалуйста, по-русски", CandidateLang.Ru)]
    [InlineData("Ruscha yozing", CandidateLang.Ru)]
    [InlineData("Ўзбекча ёзинг", CandidateLang.UzCyrl)]
    [InlineData("O'zbekcha javob bering", CandidateLang.UzLatn)]
    public void Explicit_requests_change_the_language(string text, CandidateLang expected) =>
        Assert.Equal(expected, LanguageDetectionService.ExplicitRequest(text));

    [Theory]
    [InlineData("Я знаю русский и узбекский")]
    [InlineData("Резюме у меня на двух языках")]
    [InlineData("Rus va o'zbek tillarini bilaman")]
    public void Mentioning_a_language_is_not_a_request(string text) => Assert.Null(LanguageDetectionService.ExplicitRequest(text));

    [Fact]
    public void Preference_is_kept_through_ambiguous_messages_and_changes_only_on_clear_evidence()
    {
        // Первое понятное сообщение задаёт язык.
        var first = Languages.Decide(null, null, null, null, "Ассалому алайкум, иш ҳақида маълумот берсангиз.");
        Assert.Equal((CandidateLang.UzCyrl, "auto", true), (first.Preferred, first.Source, first.Changed));

        // Короткие и неоднозначные — не меняют: «ok», «1», «да», имя латиницей.
        foreach (var text in new[] { "ok", "1", "да", "ha", "Sardor", "👍" })
        {
            var d = Languages.Decide(CandidateLang.UzCyrl, "auto", "uz-Cyrl", 0.99, text);
            Assert.Equal((CandidateLang.UzCyrl, false), (d.Preferred, d.Changed));
        }

        // Одно среднее сообщение на другом языке — не меняет; второе подряд на нём же — меняет.
        var once = Languages.Decide(CandidateLang.Ru, "auto", "ru-Cyrl", 0.9, "Ha, bor");
        Assert.False(once.Changed);
        var twice = Languages.Decide(CandidateLang.Ru, "auto", once.Detected.Code, once.Detected.Confidence, "Ha, tajribam bor");
        Assert.Equal((CandidateLang.UzLatn, true), (twice.Preferred, twice.Changed));

        // Длинное уверенное сообщение — меняет сразу.
        var strong = Languages.Decide(CandidateLang.Ru, "auto", "ru-Cyrl", 0.9, "Assalomu alaykum, ish haqida ma'lumot bersangiz, iltimos");
        Assert.Equal(CandidateLang.UzLatn, strong.Preferred);

        // Язык выбрал HR или сам кандидат — меняет только явная просьба.
        Assert.Equal(CandidateLang.Ru, Languages.Decide(CandidateLang.Ru, "hr", "ru-Cyrl", 0.9, "Assalomu alaykum, ish haqida ma'lumot bersangiz, iltimos").Preferred);
        var asked = Languages.Decide(CandidateLang.UzLatn, "hr", "uz-Latn", 0.9, "Iltimos, kirillda yozing");
        Assert.Equal((CandidateLang.UzCyrl, "candidate", true), (asked.Preferred, asked.Source, asked.Changed));

        // «Пишите кириллицей» от того, с кем говорим по-русски, — русский остаётся.
        Assert.Equal(CandidateLang.Ru, Languages.Decide(CandidateLang.Ru, "auto", "ru-Cyrl", 0.9, "Пишите, пожалуйста, кириллицей").Preferred);
    }

    [Fact]
    public void Templates_have_three_variants_and_slots_use_local_day_names()
    {
        var monday = new DateTimeOffset(2026, 11, 16, 10, 0, 0, TimeSpan.FromHours(5));
        Assert.Equal("1) пн 16.11, 10:00", RecruiterTexts.Slots(CandidateLang.Ru, [monday]));
        Assert.Equal("1) Du 16.11, 10:00", RecruiterTexts.Slots(CandidateLang.UzLatn, [monday]));
        Assert.Equal("1) Ду 16.11, 10:00", RecruiterTexts.Slots(CandidateLang.UzCyrl, [monday]));
        foreach (var lang in Enum.GetValues<CandidateLang>())
        {
            var text = string.Join(' ', RecruiterTexts.Invitation(lang), RecruiterTexts.ResumeReceived(lang), RecruiterTexts.Handoff(lang),
                RecruiterTexts.Booked(lang, "16.11 10:00", null), RecruiterTexts.Reminder(lang, "16.11 10:00", null), RecruiterTexts.FollowUp(lang, "Сардор", "Водитель"));
            Assert.Equal(CandidateLanguages.Code(lang), Languages.Detect(text).Code);
        }

        Assert.Equal("Резюмейингизни олдим. Ҳужжатни кўриб чиқяпман. Сизга тез орада жавоб бераман.", RecruiterTexts.ResumeReceived(CandidateLang.UzCyrl));
        Assert.Equal("Rezyumeyingizni oldim. Hujjatni ko'rib chiqyapman. Sizga tez orada javob beraman.", RecruiterTexts.ResumeReceived(CandidateLang.UzLatn));
    }

    [Fact]
    public void Model_reply_in_another_script_is_caught()
    {
        Assert.True(RecruiterRules.WrongLanguage(Languages, CandidateLang.UzCyrl, ["Rahmat! Qancha yillik tajribangiz bor?"]));
        Assert.True(RecruiterRules.WrongLanguage(Languages, CandidateLang.UzCyrl, ["Спасибо! Сколько лет вы работаете водителем?"]));
        Assert.False(RecruiterRules.WrongLanguage(Languages, CandidateLang.UzCyrl, ["Раҳмат! Қанча йиллик тажрибангиз бор?"]));
        Assert.False(RecruiterRules.WrongLanguage(Languages, CandidateLang.UzLatn, ["Ok"]));
        Assert.False(RecruiterRules.WrongLanguage(Languages, CandidateLang.Ru, ["Отлично! Вакансия «Driver» в Ташкенте, график 5/2."]));
    }
}

/// <summary>
/// AI-рекрутер отвечает на языке и графике кандидата (сценарии 1–5): узбекский латиницей, узбекский кириллицей, русский; неоднозначное
/// сообщение язык не меняет; смешанное — по сути; явная просьба меняет; ответ модели не той графикой — переписывается или уходит HR.
/// </summary>
public class RecruitmentLanguageDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private long _peer = 8200;

    private async Task<(IServiceScope Scope, OneBaseDbContext Db, Guid Account)> StartAsync()
    {
        await fixture.ResetAsync();
        var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await db.RecruitmentExceptions.ExecuteDeleteAsync();
        await db.Candidates.ExecuteDeleteAsync();
        await db.Vacancies.ExecuteDeleteAsync();
        await db.RecruitmentSettings.ExecuteDeleteAsync();
        foreach (var (code, name) in new[] { ("sales", "Продажи"), ("hr", "HR") })
        {
            if (!await db.Departments.AnyAsync(d => d.Code == code))
            {
                db.Departments.Add(new Department { Code = code, Name = name });
            }
        }

        await db.SaveChangesAsync();
        var account = await HrAccountAsync(scope.ServiceProvider);
        await RecruitmentFlowTests.EnableRecruiterAsync(scope.ServiceProvider);
        return (scope, db, account);
    }

    private async Task<Guid> HrAccountAsync(IServiceProvider sp)
    {
        fixture.Telegram.Me = new TelegramMe(777_000_444, "onebase_hr_lang", "HR");
        var accounts = sp.GetRequiredService<TelegramAccountService>();
        await accounts.SaveCredentialsAsync(123456, "0123456789abcdef0123456789abcdef", fixture.AdminId);
        var account = await accounts.CreateAccountAsync("HR", fixture.AdminId);
        await accounts.StartLoginAsync(account.Id, "+998 90 000 00 04", fixture.AdminId);
        await accounts.SubmitCodeAsync(account.Id, fixture.Telegram.Code, fixture.AdminId);
        await sp.GetRequiredService<TelegramDepartmentService>().SaveAsync(account.Id, new TelegramDepartmentsInput(["hr"], "hr"), fixture.AdminId);
        return account.Id;
    }

    private sealed record Chat(Guid Account, TelegramPeerRef Peer, long User)
    {
        public int Next;
    }

    private Chat NewChat(Guid account)
    {
        var peer = ++_peer;
        return new Chat(account, new TelegramPeerRef(peer, TelegramChatType.Private, false, 81), peer);
    }

    private async Task SayAsync(Chat chat, string text)
    {
        var m = new TelegramIncoming(chat.Account, chat.Peer, "Кандидат", ++chat.Next, chat.User, null, "Кандидат Тестов", text, fixture.Clock.GetUtcNow(), false, false);
        using var scope = fixture.Services.CreateScope();
        var route = await scope.ServiceProvider.GetRequiredService<TelegramMessageRouter>().RouteAsync(m, default);
        await scope.ServiceProvider.GetServices<ITelegramDepartmentHandler>().Single(h => h.Department == "hr").HandleAsync(m, route, default);
    }

    /// <summary>Отправить очередь исходящих (в историю диалога сообщение попадает после отправки — рекрутер видит его в переписке).</summary>
    private async Task SendQueuedAsync()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await db.TelegramAccountDepartments.ExecuteUpdateAsync(u => u.SetProperty(d => d.MinIntervalSeconds, 1));
        for (var i = 0; i < 20 && await db.TelegramOutgoingMessages.AnyAsync(m => m.Status == TelegramOutboxStatus.Pending); i++)
        {
            await db.TelegramOutgoingMessages.ExecuteUpdateAsync(u => u.SetProperty(m => m.SentAt, (DateTimeOffset?)null));
            await scope.ServiceProvider.GetRequiredService<OneBase.Application.Telegram.Outbox.TelegramOutboxRunner>().RunOnceAsync(default);
        }
    }

    private static async Task<string> LastAsync(OneBaseDbContext db, Chat chat) =>
        await db.TelegramOutgoingMessages.AsNoTracking().Where(m => m.PeerId == chat.Peer.PeerId).OrderByDescending(m => m.CreatedAt).Select(m => m.Text).FirstAsync();

    private static async Task<Candidate> CandidateAsync(OneBaseDbContext db, Chat chat) =>
        await db.Candidates.AsNoTracking().SingleAsync(c => c.Contacts.Any(x => x.Kind == CandidateContactKind.Telegram && x.Value == chat.User.ToString()));

    [PostgresFact]
    public async Task Uzbek_latin_russian_and_uzbek_cyrillic_candidates_get_answers_in_their_own_language_and_script()
    {
        var (scope, db, account) = await StartAsync();
        using var _ = scope;

        // 1. Узбекский латиницей — согласия не спрашивает: сразу ответ модели, ей велено писать латиницей.
        var latin = NewChat(account);
        fixture.Gateway.Script.Enqueue(m =>
        {
            Assert.Contains("ЛАТИНИЦЕЙ", m[1].Content);
            return "{\"action\":\"reply\",\"messages\":[\"Assalomu alaykum! Hozir haydovchi va operator vakansiyalari bor. Qaysi biri sizga qiziq?\"]}";
        });
        await SayAsync(latin, "Assalomu alaykum, ish haqida ma'lumot bersangiz.");
        Assert.Equal("Assalomu alaykum! Hozir haydovchi va operator vakansiyalari bor. Qaysi biri sizga qiziq?", await LastAsync(db, latin));
        Assert.Equal(("uz", "Latn", "auto"), ((await CandidateAsync(db, latin)).Language, (await CandidateAsync(db, latin)).PreferredScript, (await CandidateAsync(db, latin)).LanguageSource));
        await SendQueuedAsync();

        // 2. Узбекский кириллицей — модель ответила латиницей — переписывает, уходит кириллица.
        var cyrillic = NewChat(account);
        var calls = fixture.Gateway.Calls.Count;
        fixture.Gateway.Script.Enqueue(m =>
        {
            Assert.Contains("КИРИЛЛИЦЕЙ", m[1].Content);
            return "{\"action\":\"reply\",\"messages\":[\"Rahmat! Qaysi vakansiya sizga qiziq?\"]}";
        });
        fixture.Gateway.Script.Enqueue(m =>
        {
            Assert.Contains("не на языке кандидата", m[^1].Content);
            return "{\"action\":\"reply\",\"messages\":[\"Раҳмат! Қайси вакансия сизга қизиқ?\"]}";
        });
        await SayAsync(cyrillic, "Ассалому алайкум, иш ҳақида маълумот берсангиз.");
        Assert.Equal("Раҳмат! Қайси вакансия сизга қизиқ?", await LastAsync(db, cyrillic));
        Assert.Equal(("uz", "Cyrl"), ((await CandidateAsync(db, cyrillic)).Language, (await CandidateAsync(db, cyrillic)).PreferredScript));
        Assert.Equal(calls + 2, fixture.Gateway.Calls.Count);
        Assert.Equal("hr.recruiter.language", fixture.Gateway.Calls[^1].Options.Context.Purpose);
        Assert.DoesNotContain(await db.TelegramOutgoingMessages.AsNoTracking().Where(m => m.PeerId == cyrillic.Peer.PeerId).Select(m => m.Text).ToListAsync(),
            t => t.Contains("Qaysi", StringComparison.Ordinal));
        await SendQueuedAsync();

        // 3. Русский — по-русски.
        var russian = NewChat(account);
        fixture.Gateway.Script.Enqueue(m =>
        {
            Assert.Contains("пиши по-русски", m[1].Content);
            return "{\"action\":\"reply\",\"messages\":[\"Здравствуйте! Какая работа вам интересна?\"]}";
        });
        await SayAsync(russian, "Здравствуйте, расскажите подробнее о вакансии.");
        Assert.Equal("Здравствуйте! Какая работа Вам интересна?", await LastAsync(db, russian));
        Assert.Equal(("ru", "Cyrl"), ((await CandidateAsync(db, russian)).Language, (await CandidateAsync(db, russian)).PreferredScript));

        // 4. Неоднозначные короткие сообщения не меняют язык: кириллица остаётся.
        foreach (var text in new[] { "ok", "1", "да" })
        {
            fixture.Gateway.Script.Enqueue(m =>
            {
                Assert.Contains("КИРИЛЛИЦЕЙ", m[1].Content);
                return "{\"action\":\"reply\",\"messages\":[\"Тушунарли, раҳмат!\"]}";
            });
            await SayAsync(cyrillic, text);
        }

        var kept = await CandidateAsync(db, cyrillic);
        Assert.Equal(("uz", "Cyrl"), (kept.Language, kept.PreferredScript));

        // Явная просьба — меняет: латинский кандидат попросил кириллицу.
        fixture.Gateway.Script.Enqueue(m =>
        {
            Assert.Contains("КИРИЛЛИЦЕЙ", m[1].Content);
            return "{\"action\":\"reply\",\"messages\":[\"Майли, энди кириллда ёзаман.\"]}";
        });
        await SayAsync(latin, "Iltimos, kirillda yozing");
        var switched = await CandidateAsync(db, latin);
        Assert.Equal(("uz", "Cyrl", "candidate"), (switched.Language, switched.PreferredScript, switched.LanguageSource));

        // 5. Смешанное: приветствие по-русски, суть — узбекской латиницей → латиница.
        var mixed = NewChat(account);
        fixture.Gateway.Script.Enqueue(m =>
        {
            Assert.Contains("ЛАТИНИЦЕЙ", m[1].Content);
            return "{\"action\":\"reply\",\"messages\":[\"Assalomu alaykum! Qaysi ish sizga qiziq?\"]}";
        });
        await SayAsync(mixed, "Здравствуйте! Ish haqida ma'lumot bersangiz");
        Assert.StartsWith("Assalomu alaykum!", await LastAsync(db, mixed));
        Assert.Equal(("uz", "Latn"), ((await CandidateAsync(db, mixed)).Language, (await CandidateAsync(db, mixed)).PreferredScript));
    }

    [PostgresFact]
    public async Task Reply_that_stays_in_the_wrong_script_is_not_sent_and_goes_to_hr()
    {
        var (scope, db, account) = await StartAsync();
        using var _ = scope;
        var chat = NewChat(account);
        fixture.Gateway.Script.Enqueue(_ => "{\"action\":\"reply\",\"messages\":[\"Rahmat! Qaysi vakansiya sizga qiziq?\"]}");
        fixture.Gateway.Script.Enqueue(_ => "{\"action\":\"reply\",\"messages\":[\"Rahmat! Qaysi ish sizga qiziq?\"]}");
        await SayAsync(chat, "Ассалому алайкум, иш ҳақида маълумот берсангиз.");

        var sent = await db.TelegramOutgoingMessages.AsNoTracking().Where(m => m.PeerId == chat.Peer.PeerId).Select(m => m.Text).ToListAsync();
        Assert.DoesNotContain(sent, t => t.Contains("Qaysi", StringComparison.Ordinal));
        Assert.Equal(RecruiterTexts.Handoff(CandidateLang.UzCyrl), await LastAsync(db, chat));
        Assert.Contains(await db.RecruitmentExceptions.AsNoTracking().ToListAsync(), e => e.Message.Contains("не на языке кандидата"));
    }
}
