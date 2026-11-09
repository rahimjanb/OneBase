using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.DialogAgents;
using OneBase.AI.Documents;
using OneBase.AI.Llm;
using OneBase.AI.Providers;
using OneBase.Application.Agents;
using OneBase.Application.Field;
using OneBase.Application.Recruitment;
using OneBase.Application.Security;
using OneBase.Application.Telegram;
using OneBase.Application.Telegram.Outbox;
using OneBase.Application.Telegram.Routing;
using OneBase.Domain.AI;
using OneBase.Domain.Identity;
using OneBase.Domain.Recruitment;
using OneBase.Domain.Telegram;
using OneBase.Domain.Work;
using OneBase.Infrastructure.Persistence;
using OneBase.Infrastructure.Telegram;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>Правила ИИ-агентов отделов без базы: ограничения ответа, деление на сообщения, рабочее время, разбор распознанного документа, тело запроса с фото.</summary>
public class DialogAgentRulesTests
{
    private static DialogAgent Agent() => DialogAgentService.Defaults("supply", DialogAgentKinds.Assistant, "Снабжение");

    [Theory]
    [InlineData("Скидка 20% только сегодня!", "скидка", "скидка")]
    [InlineData("Ещё вопрос по ЁЛКАМ", "елкам", "елкам")]
    [InlineData("Цена — по прайсу", "скидка\nбесплатно", null)]
    public void Forbidden_phrases_are_found_ignoring_case_and_yo(string reply, string phrases, string? expected) =>
        Assert.Equal(expected, DialogAgentRules.ForbiddenIn(reply, phrases));

    [Fact]
    public void Reply_is_split_into_at_most_three_short_plain_messages_within_the_limit()
    {
        var parts = DialogAgentRules.Parts(["**Здравствуйте!**\n\nСпасибо, что написали.\n\n## Условия\nГрафик 5/2.\n\nЗарплата от 6 млн."], split: true, maxChars: 700);
        Assert.Equal(3, parts.Count);
        Assert.Equal("Здравствуйте!", parts[0]);
        Assert.DoesNotContain(parts, p => p.Contains("**") || p.Contains("##"));
        Assert.Contains("Зарплата от 6 млн.", parts[2]);

        Assert.Single(DialogAgentRules.Parts(["Первое", "Второе"], split: false, maxChars: 700));
        var clipped = DialogAgentRules.Parts([string.Join(' ', Enumerable.Repeat("Длинное предложение без конца", 40)) + "."], split: false, maxChars: 120);
        Assert.True(clipped[0].Length <= 121);
    }

    [Fact]
    public void Off_hours_reply_waits_for_the_next_working_morning_and_working_hours_reply_after_a_human_pause()
    {
        var agent = Agent(); // 09:00–19:00, пн–сб
        var saturdayEvening = new DateTimeOffset(2026, 11, 14, 20, 30, 0, DialogAgentRules.Tashkent); // суббота
        Assert.False(DialogAgentRules.InWork(agent, saturdayEvening));
        var at = DialogAgentRules.SendAt(agent, saturdayEvening, new Random(1));
        var monday = new DateTimeOffset(2026, 11, 16, 9, 0, 0, DialogAgentRules.Tashkent);
        Assert.InRange(at, monday.AddMinutes(1), monday.AddMinutes(10)); // воскресенье — не рабочий день

        var tuesdayNoon = new DateTimeOffset(2026, 11, 17, 12, 0, 0, DialogAgentRules.Tashkent);
        Assert.InRange(DialogAgentRules.SendAt(agent, tuesdayNoon, new Random(2)), tuesdayNoon.AddSeconds(agent.ReplyDelayMinSeconds),
            tuesdayNoon.AddSeconds(agent.ReplyDelayMaxSeconds));

        agent.OffHours = "reply";
        Assert.True(DialogAgentRules.SendAt(agent, saturdayEvening, new Random(3)) < saturdayEvening.AddMinutes(5));
        (agent.WorkFrom, agent.WorkTo) = (new TimeOnly(22, 0), new TimeOnly(6, 0)); // ночная смена
        Assert.True(DialogAgentRules.InWork(agent, new DateTimeOffset(2026, 11, 17, 2, 0, 0, DialogAgentRules.Tashkent)));
    }

    [Fact]
    public void Recognized_id_document_keeps_no_text_and_resume_keeps_its_text()
    {
        var passport = DocumentVisionReader.Parse("{\"document_type\":\"id_document\",\"text\":\"AA1234567 Каримов\"}")!;
        Assert.Null(passport.Text);
        Assert.Contains("удостоверяющий личность", passport.Error);
        var resume = DocumentVisionReader.Parse("```json\n{\"document_type\":\"resume\",\"text\":\"Водитель категории C, 6 лет\"}\n```")!;
        Assert.Equal(("resume", "Водитель категории C, 6 лет", (string?)null), (resume.DocumentType, resume.Text, resume.Error));
        Assert.Null(DocumentVisionReader.Parse("не JSON"));
    }

    [Fact]
    public void Photo_and_pdf_go_to_claude_as_base64_blocks_before_the_text()
    {
        var request = new AiCompletionRequest("claude-opus-5-5",
        [
            new LlmMessage(LlmRole.System, "Распознай"),
            new LlmMessage(LlmRole.User, "Что на фото?", Attachments: [new LlmAttachment("image/jpeg", [1, 2, 3], "cv.jpg"), new LlmAttachment(LlmAttachment.Pdf, [4, 5], "scan.pdf")]),
        ], [], null, 1000);
        var content = AnthropicProvider.BuildBody(request)["messages"]![0]!["content"]!.AsArray();
        Assert.Equal(("image", "image/jpeg", Convert.ToBase64String([1, 2, 3])),
            ((string?)content[0]!["type"], (string?)content[0]!["source"]!["media_type"], (string?)content[0]!["source"]!["data"]));
        Assert.Equal(("document", "application/pdf"), ((string?)content[1]!["type"], (string?)content[1]!["source"]!["media_type"]));
        Assert.Equal("Что на фото?", (string?)content[2]!["text"]);

        var openAi = OpenAiProvider.BuildBody(request)["messages"]![1]!["content"]!.AsArray();
        Assert.StartsWith("data:image/jpeg;base64,", (string?)openAi[0]!["image_url"]!["url"]);
        Assert.Equal("scan.pdf", (string?)openAi[1]!["file"]!["filename"]);
    }
}

/// <summary>
/// ИИ-агенты отделов на PostgreSQL: распознавание фото и сканов резюме (подставная модель), черновики «С проверкой», запрещённые фразы, помощник
/// отдела в Telegram (подставной Telegram), передача человеку, права по отделам. Ни одного настоящего сообщения и платного вызова.
/// </summary>
public class DialogAgentTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly TelegramPeerRef Client = new(8101, TelegramChatType.Private, false, 81);

    private async Task<(IServiceScope Scope, OneBaseDbContext Db)> StartAsync()
    {
        await fixture.ResetAsync();
        var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await db.RecruitmentExceptions.ExecuteDeleteAsync();
        await db.Candidates.ExecuteDeleteAsync();
        await db.Vacancies.ExecuteDeleteAsync();
        await db.RecruitmentSettings.ExecuteDeleteAsync();
        await db.WorkDepartmentHeads.ExecuteDeleteAsync();
        foreach (var (code, name) in new[] { ("sales", "Продажи"), ("hr", "HR"), ("supply", "Снабжение") })
        {
            if (!await db.Departments.AnyAsync(d => d.Code == code))
            {
                db.Departments.Add(new Department { Code = code, Name = name });
            }
        }

        await db.SaveChangesAsync();
        return (scope, db);
    }

    private AgentAccess Admin => new(fixture.AdminId, new HashSet<string> { "sales", "hr", "supply" }, true);

    private async Task<Guid> AccountAsync(IServiceProvider sp, string department)
    {
        fixture.Telegram.Me = new TelegramMe(777_000_444, "onebase_supply", "Снабжение");
        var accounts = sp.GetRequiredService<TelegramAccountService>();
        await accounts.SaveCredentialsAsync(123456, "0123456789abcdef0123456789abcdef", fixture.AdminId);
        var account = await accounts.CreateAccountAsync("Снабжение", fixture.AdminId);
        await accounts.StartLoginAsync(account.Id, "+998 90 000 00 04", fixture.AdminId);
        await accounts.SubmitCodeAsync(account.Id, fixture.Telegram.Code, fixture.AdminId);
        await sp.GetRequiredService<TelegramDepartmentService>().SaveAsync(account.Id, new TelegramDepartmentsInput([department], department), fixture.AdminId);
        return account.Id;
    }

    private TelegramIncoming Msg(Guid account, int id, string text) =>
        new(account, Client, "Поставщик", id, 9101, "supplier_uz", "Бахтиёр", text, fixture.Clock.GetUtcNow(), false, false);

    private async Task DeliverAsync(TelegramIncoming m)
    {
        using var scope = fixture.Services.CreateScope();
        var route = await scope.ServiceProvider.GetRequiredService<TelegramMessageRouter>().RouteAsync(m, default);
        var handlers = scope.ServiceProvider.GetServices<ITelegramDepartmentHandler>().ToList();
        var handler = handlers.FirstOrDefault(h => h.Department == route.Department) ?? handlers.Single(h => h.Department == TelegramDepartments.Any);
        await handler.HandleAsync(m, route, default);
    }

    private static async Task<DialogAgent> SupplyAgentAsync(IServiceProvider sp, AgentAccess access, Action<DialogAgent>? configure = null)
    {
        var view = await sp.GetRequiredService<DialogAgentService>().CreateAsync(access, new AgentCreateInput("supply", DialogAgentKinds.Assistant, "Снабжение"), default);
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var agent = await db.DialogAgents.SingleAsync(a => a.Id == view.Id);
        (agent.Enabled, agent.ReplyDelayMinSeconds, agent.ReplyDelayMaxSeconds, agent.Typing, agent.WorkFrom, agent.WorkTo, agent.WorkDays) =
            (true, 0, 0, false, null, null, "1,2,3,4,5,6,7");
        agent.DisplayName = "Азиза";
        agent.Intro = "менеджер отдела снабжения";
        configure?.Invoke(agent);
        await db.SaveChangesAsync();
        return agent;
    }

    // ---------- Фото и сканы резюме ----------

    [PostgresFact]
    public async Task Photo_and_scanned_pdf_resumes_are_read_by_the_model_and_can_be_switched_off()
    {
        var (scope, db) = await StartAsync();
        using var _ = scope;
        var sp = scope.ServiceProvider;
        var service = sp.GetRequiredService<RecruitmentService>();
        var hr = new RecruitmentContext(fixture.AdminId, true, true, []);
        var candidate = await service.CreateCandidateAsync(hr, new CandidateInput("Нодира Алиева", null, null, null, [], "manual", null, null, "written"), default);

        // Фото резюме — модель читает изображение (блок image/jpeg), текст сохраняется с пометкой «распознал AI».
        fixture.Gateway.Script.Enqueue(m =>
        {
            var file = Assert.Single(m[^1].Attachments!);
            Assert.Equal("image/jpeg", file.MediaType);
            return "{\"document_type\":\"resume\",\"text\":\"Нодира Алиева. Бухгалтер, 7 лет. 1С, Excel.\"}";
        });
        var card = await service.UploadResumeAsync(hr, candidate.CandidateId, Upload("cv.jpg", [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3], "image/jpeg"), default);
        var photo = card.Resumes.Single(r => r.FileName == "cv.jpg");
        Assert.True(photo.HasText && photo.TextByAi);
        Assert.Contains("Бухгалтер, 7 лет", photo.TextPreview);
        Assert.Equal("hr.resume.read", fixture.Gateway.Calls[^1].Options.Context.Purpose);

        // PDF с текстовым слоем — без модели; скан (PDF без текста) — модель читает документ application/pdf.
        var calls = fixture.Gateway.Calls.Count;
        await service.UploadResumeAsync(hr, candidate.CandidateId, Upload("cv.pdf", RecruitmentTests.Pdf("Accountant with 7 years of experience in 1C and Excel, IFRS reports, tax audits and payroll for FMCG distributors"), "application/pdf"), default);
        Assert.Equal(calls, fixture.Gateway.Calls.Count);
        fixture.Gateway.Script.Enqueue(m =>
        {
            Assert.Equal(LlmAttachment.Pdf, Assert.Single(m[^1].Attachments!).MediaType);
            return "{\"document_type\":\"certificate\",\"text\":\"Диплом. Ташкентский финансовый институт, 2015.\"}";
        });
        card = await service.UploadResumeAsync(hr, candidate.CandidateId, Upload("diploma.pdf", RecruitmentTests.Pdf(" "), "application/pdf"), default);
        Assert.Contains("финансовый институт", card.Resumes.Single(r => r.FileName == "diploma.pdf").TextPreview);

        // Паспорт — текст не сохраняется.
        fixture.Gateway.Script.Enqueue(_ => "{\"document_type\":\"id_document\",\"text\":\"AA1234567\"}");
        card = await service.UploadResumeAsync(hr, candidate.CandidateId, Upload("passport.png", [0x89, 0x50, 0x4E, 0x47, 9], "image/png"), default);
        var passport = card.Resumes.Single(r => r.FileName == "passport.png");
        Assert.False(passport.HasText);
        Assert.Equal("id_document", passport.DocumentType);

        // Распознавание выключено в настройках подбора — модель не вызывается, причина видна HR.
        await db.RecruitmentSettings.ExecuteUpdateAsync(u => u.SetProperty(s => s.ReadScans, false));
        calls = fixture.Gateway.Calls.Count;
        card = await service.UploadResumeAsync(hr, candidate.CandidateId, Upload("cv2.jpg", [0xFF, 0xD8, 0xFF, 0xE0, 7], "image/jpeg"), default);
        Assert.Equal(calls, fixture.Gateway.Calls.Count);
        Assert.Contains("Распознавать фото и сканы", card.Resumes.Single(r => r.FileName == "cv2.jpg").TextError);
    }

    private static RecruitmentUpload Upload(string name, byte[] content, string type) => new(name, type, content.Length, new MemoryStream(content));

    // ---------- Помощник отдела ----------

    [PostgresFact]
    public async Task Department_assistant_answers_with_faq_in_short_messages_and_hands_off_complaints_to_a_human()
    {
        var (scope, db) = await StartAsync();
        using var _ = scope;
        var sp = scope.ServiceProvider;
        var account = await AccountAsync(sp, "supply");
        var agents = sp.GetRequiredService<DialogAgentService>();
        var agent = await SupplyAgentAsync(sp, Admin, a => a.ForbiddenPhrases = "бесплатно");
        // Руководитель снабжения получает уведомления агента своего отдела.
        db.WorkDepartmentHeads.Add(new WorkDepartmentHead { DepartmentId = (await db.Departments.SingleAsync(d => d.Code == "supply")).Id, UserId = fixture.AdminId });
        await db.SaveChangesAsync();
        await agents.AddAnswerAsync(Admin, agent.Id, new AgentAnswerInput("Куда привозить товар?", "Склад: Ташкент, ул. Складская 7, приём 9:00–17:00."), default);

        // Вопрос — модель с готовым ответом в промпте; ответ двумя сообщениями, назначение — агенты отдела.
        fixture.Gateway.Script.Enqueue(m =>
        {
            Assert.Contains("Складская 7", m[0].Content);
            Assert.Contains("Тебя зовут Азиза", m[0].Content);
            Assert.Contains("не выдавай", m[0].Content);
            Assert.Contains("<message>Здравствуйте, куда привезти поставку?</message>", m[^1].Content);
            return "{\"action\":\"reply\",\"messages\":[\"Здравствуйте, Бахтиёр!\",\"Привозите на склад: ул. Складская 7, приём с 9:00 до 17:00.\"]}";
        });
        await DeliverAsync(Msg(account, 1, "Здравствуйте, куда привезти поставку?"));
        var queued = await db.TelegramOutgoingMessages.AsNoTracking().OrderBy(m => m.NextAttemptAt).ToListAsync();
        Assert.Equal(["Здравствуйте, Бахтиёр!", "Привозите на склад: ул. Складская 7, приём с 9:00 до 17:00."], queued.Select(m => m.Text));
        Assert.All(queued, m => Assert.Equal(("supply", "ai", DialogAgentDelivery.RelatedType), (m.Department, m.Author, m.RelatedType)));
        Assert.True(queued[1].NextAttemptAt > queued[0].NextAttemptAt); // второе сообщение — через паузу, как пишет человек
        Assert.Equal("agents.supply.reply", fixture.Gateway.Calls[^1].Options.Context.Purpose);
        Assert.Equal(DialogReplyStatus.Sent, (await db.DialogAgentReplies.AsNoTracking().SingleAsync()).Status);

        // Собеседник написал ещё, пока ответ не ушёл, — ответ отменяется и заменяется новым (один на оба сообщения).
        fixture.Gateway.Script.Enqueue(_ => "{\"action\":\"reply\",\"messages\":[\"Да, в субботу тоже принимаем до 14:00.\"]}");
        await DeliverAsync(Msg(account, 2, "А в субботу можно?"));
        Assert.All(await db.TelegramOutgoingMessages.AsNoTracking().Where(m => m.Text.Contains("Складская")).ToListAsync(),
            m => Assert.Equal(TelegramOutboxStatus.Cancelled, m.Status));
        Assert.Contains(await db.DialogAgentReplies.AsNoTracking().ToListAsync(), r => r.Status == DialogReplyStatus.Superseded);

        // Запрещённая фраза — ответ не уходит, диалог — сотруднику, руководителям — уведомление.
        fixture.Gateway.Script.Enqueue(_ => "{\"action\":\"reply\",\"messages\":[\"Доставку сделаем бесплатно!\"]}");
        await DeliverAsync(Msg(account, 3, "А доставка платная?"));
        var blocked = await db.DialogAgentReplies.AsNoTracking().SingleAsync(r => r.Status == DialogReplyStatus.Blocked);
        Assert.Contains("бесплатно", blocked.BlockReason);
        Assert.False(await db.TelegramOutgoingMessages.AnyAsync(m => m.Text.Contains("бесплатно")));
        Assert.Equal(TelegramConversationMode.Manual, (await db.TelegramConversations.AsNoTracking().SingleAsync()).Mode);
        Assert.True(await db.UserNotifications.AnyAsync(n => n.Kind == UserNotificationKind.AgentAlert && n.RecipientId == fixture.AdminId));

        // Диалог ведёт человек — сообщения сохраняются, AI молчит; вернули AI — жалоба сразу к человеку, без модели.
        var calls = fixture.Gateway.Calls.Count;
        await DeliverAsync(Msg(account, 4, "Жду ответа"));
        Assert.Equal(calls, fixture.Gateway.Calls.Count);
        var conversation = await db.TelegramConversations.AsNoTracking().SingleAsync();
        Assert.Contains("Жду ответа", (await agents.ConversationAsync(Admin, agent.Id, conversation.Id, default)).Messages.Select(m => m.Text));
        await agents.SetModeAsync(Admin, agent.Id, conversation.Id, TelegramConversationMode.Ai, default);
        await DeliverAsync(Msg(account, 5, "У меня жалоба на ваших кладовщиков"));
        Assert.Equal(calls, fixture.Gateway.Calls.Count);
        Assert.Equal(TelegramConversationMode.Manual, (await db.TelegramConversations.AsNoTracking().SingleAsync()).Mode);
        Assert.Contains(await db.TelegramOutgoingMessages.AsNoTracking().ToListAsync(), m => m.Text.Contains("ответственному сотруднику"));

        // Исправленный ответ — в готовые ответы агента.
        await agents.ApproveAsync(Admin, blocked.Id, new ReplyReviewInput("Доставка по Ташкенту — 50 000 сум."), default);
        Assert.Contains(await db.TelegramOutgoingMessages.AsNoTracking().ToListAsync(), m => m.Text == "Доставка по Ташкенту — 50 000 сум.");
        await agents.RateAsync(Admin, blocked.Id, new ReplyRateInput("bad", "Доставка по Ташкенту — 50 000 сум.", true), default);
        Assert.Equal(2, await db.DialogAgentAnswers.CountAsync(a => a.AgentId == agent.Id));
    }

    [PostgresFact]
    public async Task Approval_mode_keeps_drafts_until_a_human_approves_or_rejects_them()
    {
        var (scope, db) = await StartAsync();
        using var _ = scope;
        var sp = scope.ServiceProvider;
        var account = await AccountAsync(sp, "supply");
        var agents = sp.GetRequiredService<DialogAgentService>();
        var agent = await SupplyAgentAsync(sp, Admin, a => a.Mode = DialogAgentMode.Approval);

        fixture.Gateway.Script.Enqueue(_ => "{\"action\":\"reply\",\"messages\":[\"Здравствуйте! Прайс пришлю сегодня.\"]}");
        await DeliverAsync(Msg(account, 1, "Пришлите прайс"));
        Assert.Empty(await db.TelegramOutgoingMessages.AsNoTracking().ToListAsync());
        var page = await agents.RepliesAsync(Admin, agent.Id, "drafts", default);
        var draft = Assert.Single(page.Items);
        Assert.Equal((1, "Draft"), (page.Drafts, draft.Status));

        // Одобрили с правкой — уходит исправленный текст, правка запоминается.
        await agents.ApproveAsync(Admin, draft.Id, new ReplyReviewInput("Здравствуйте! Прайс пришлю до 18:00."), default);
        Assert.Equal("Здравствуйте! Прайс пришлю до 18:00.", (await db.TelegramOutgoingMessages.AsNoTracking().SingleAsync()).Text);
        var approved = await db.DialogAgentReplies.AsNoTracking().SingleAsync();
        Assert.Equal((DialogReplyStatus.Approved, "Здравствуйте! Прайс пришлю до 18:00."), (approved.Status, approved.Correction));
        await Assert.ThrowsAsync<FieldConflictException>(() => agents.ApproveAsync(Admin, draft.Id, new ReplyReviewInput(null), default));

        // Следующий черновик отклонили — ничего не уходит.
        fixture.Gateway.Script.Enqueue(_ => "{\"action\":\"reply\",\"messages\":[\"Скидку дадим 30%.\"]}");
        await DeliverAsync(Msg(account, 2, "А скидка будет?"));
        var second = (await agents.RepliesAsync(Admin, agent.Id, "drafts", default)).Items.Single();
        await agents.RejectAsync(Admin, second.Id, default);
        Assert.Single(await db.TelegramOutgoingMessages.AsNoTracking().ToListAsync());
    }

    // ---------- Права и настройки ----------

    [PostgresFact]
    public async Task Department_heads_manage_only_their_department_agents_and_settings_are_validated()
    {
        var (scope, db) = await StartAsync();
        using var _ = scope;
        var sp = scope.ServiceProvider;
        var agents = sp.GetRequiredService<DialogAgentService>();
        var supply = await db.Departments.SingleAsync(d => d.Code == "supply");
        db.WorkDepartmentHeads.Add(new WorkDepartmentHead { DepartmentId = supply.Id, UserId = fixture.ViewerId });
        await db.SaveChangesAsync();

        var head = await agents.AccessAsync(fixture.ViewerId, new HashSet<string>(), default);
        Assert.Equal(["supply"], head.Departments);
        var hrManager = await agents.AccessAsync(fixture.ViewerId, new HashSet<string> { Permissions.HrRecruitmentManage }, default);
        Assert.Contains("hr", hrManager.Departments);
        var admin = await agents.AccessAsync(fixture.AdminId, new HashSet<string> { Permissions.AiSettingsManage }, default);
        Assert.Superset(new HashSet<string> { "sales", "hr", "supply" }, admin.Departments.ToHashSet());

        // Руководитель снабжения создаёт агента своего отдела; к HR доступа нет; в продажах агентов не создают (агент продаж — у номера).
        var created = await agents.CreateAsync(head, new AgentCreateInput("supply", DialogAgentKinds.Assistant, "Снабжение"), default);
        Assert.True(created.IsDefault);
        Assert.False(created.Enabled); // новый агент выключен
        await Assert.ThrowsAsync<FieldForbiddenException>(() => agents.CreateAsync(head, new AgentCreateInput("hr", DialogAgentKinds.Recruiter, "Рекрутер"), default));
        await Assert.ThrowsAsync<FieldValidationException>(() => agents.CreateAsync(admin, new AgentCreateInput("sales", DialogAgentKinds.Assistant, "Продажи"), default));
        Assert.Contains((await agents.DepartmentsAsync(admin, default)).Single(d => d.Code == "hr").Agents, a => a.Kind == DialogAgentKinds.Recruiter && a.IsDefault);

        var input = Input(created) with { MaxReplyChars = 50 };
        await Assert.ThrowsAsync<FieldValidationException>(() => agents.SaveAsync(head, created.Id, input, default));
        await Assert.ThrowsAsync<FieldValidationException>(() => agents.SaveAsync(head, created.Id, Input(created) with { WorkFrom = "25:00", WorkTo = "18:00" }, default));
        var saved = await agents.SaveAsync(head, created.Id, Input(created) with { Enabled = true, Mode = "Approval", Tone = "formal", ForbiddenPhrases = "скидка" }, default);
        Assert.Equal((true, "Approval", "formal"), (saved.Enabled, saved.Mode, saved.Tone));
    }

    private static AgentInput Input(AgentView v) => new(v.Name, v.Enabled, v.Mode, v.DisplayName, v.Intro, v.Tone, v.Language, v.Length, v.Emoji, v.SplitMessages,
        v.Instructions, v.ScreeningQuestions, v.AllowedTopics, v.ForbiddenTopics, v.ForbiddenPhrases, v.HandoffPhrases, v.MaxReplyChars, v.MaxRepliesPerHour,
        v.MaxRepliesPerDay, v.CanDiscussSalary, v.CanScheduleInterviews, v.HandoffReply, v.WorkFrom, v.WorkTo, v.WorkDays, v.OffHours, v.ReplyDelayMinSeconds,
        v.ReplyDelayMaxSeconds, v.Typing, v.FollowUpHours, v.ModelProvider, v.Model);

    [PostgresFact]
    public async Task Test_tab_shows_what_the_agent_would_answer_without_sending_anything()
    {
        var (scope, db) = await StartAsync();
        using var _ = scope;
        var sp = scope.ServiceProvider;
        var agent = await SupplyAgentAsync(sp, Admin, a => a.ForbiddenPhrases = "гарантирую");
        fixture.Gateway.Script.Enqueue(m =>
        {
            Assert.Contains("<message>Когда оплата?</message>", m[^1].Content);
            return "{\"action\":\"reply\",\"messages\":[\"Оплату гарантирую завтра.\"]}";
        });
        var result = await sp.GetRequiredService<DialogAgentTester>().TestAsync(agent, new AgentTestInput([new AgentTestMessage("user", "Когда оплата?")], null),
            fixture.AdminId, default);
        Assert.Equal("reply", result.Action);
        Assert.Contains("гарантирую", result.Blocked);
        Assert.Equal("agents.test", fixture.Gateway.Calls[^1].Options.Context.Purpose);
        Assert.Empty(await db.TelegramOutgoingMessages.AsNoTracking().ToListAsync());
        Assert.Empty(await db.DialogAgentReplies.AsNoTracking().ToListAsync());

        // Фраза «сразу к человеку» — без модели.
        var calls = fixture.Gateway.Calls.Count;
        var handoff = await sp.GetRequiredService<DialogAgentTester>().TestAsync(agent, new AgentTestInput([new AgentTestMessage("user", "Это жалоба")], null),
            fixture.AdminId, default);
        Assert.Equal("handoff", handoff.Action);
        Assert.Equal(calls, fixture.Gateway.Calls.Count);
    }
}
