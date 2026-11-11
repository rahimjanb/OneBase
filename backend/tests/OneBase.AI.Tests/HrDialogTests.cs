using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Providers;
using OneBase.Application.Field;
using OneBase.Application.Recruitment;
using OneBase.Application.Recruitment.Telegram;
using OneBase.Application.Security;
using OneBase.Domain.Identity;
using OneBase.Domain.Recruitment;
using OneBase.Domain.Telegram;
using OneBase.Infrastructure.Persistence;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>
/// «Диалоги Telegram» (редизайн): режимы AI / HR / «Нужен HR» и порядок списка, непрочитанные у каждого сотрудника, системные события ленты,
/// карточка кандидата, шаблоны на языке кандидата, ответ HR снимает «Нужен HR», возврат AI, черновик AI не отправляется, файл от HR, «Повторить»,
/// просьба администратору, права.
/// </summary>
public class HrDialogTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private readonly HrHarness _hr = new(fixture, 9500);

    private async Task<(HrHarness.Chat Chat, Guid Conversation)> CandidateAsync(OneBaseDbContext db, int number, string hello = "Здравствуйте, я по вакансии")
    {
        var chat = _hr.NewChat();
        var uzbek = hello.StartsWith("Ассалому", StringComparison.Ordinal);
        var reply = uzbek ? "Раҳмат! Қанча йиллик тажрибангиз бор?" : "Спасибо! Сколько лет вы водите грузовик?";
        fixture.Gateway.Script.Enqueue(_ => $"{{\"action\":\"reply\",\"messages\":[\"{reply}\"]}}");
        await _hr.SayAsync(chat, $"{hello} #V{number}");
        await _hr.SendQueuedAsync();
        var conversation = await db.TelegramConversations.AsNoTracking().SingleAsync(c => c.PeerId == chat.Peer.PeerId);
        return (chat, conversation.Id);
    }

    [PostgresFact]
    public async Task Modes_order_unread_events_candidate_card_and_templates_follow_the_design_rules()
    {
        var (scope, db) = await _hr.StartAsync();
        using var _ = scope;
        var sp = scope.ServiceProvider;
        var vacancy = await _hr.OpenVacancyAsync(sp);
        var number = (await db.Vacancies.AsNoTracking().SingleAsync(v => v.Id == vacancy)).Number;
        await sp.GetRequiredService<InterviewService>().SaveSettingsAsync(_hr.Hr, vacancy, new InterviewSettingsInput(fixture.AdminId, [], 60, null, null, null, null, 2, 14,
            "office", "Ташкент, ул. Навои, 1", null, null, null, "Возьмите паспорт"), default);
        var dialogs = sp.GetRequiredService<HrDialogService>();

        var (ai, aiId) = await CandidateAsync(db, number);
        var (needHr, needHrId) = await CandidateAsync(db, number);
        var (_, uzId) = await CandidateAsync(db, number, "Ассалому алайкум, иш ҳақида маълумот берсангиз");
        await _hr.SayAsync(needHr, "Можно поговорить с человеком?"); // AI передаёт HR

        // Список: «Нужен HR» — первым; режимы; вакансия; автор последнего сообщения; непрочитанные — входящие.
        var list = await dialogs.ListAsync(_hr.Hr, null, default);
        Assert.Equal(needHrId, list[0].Id);
        Assert.Equal(("need_hr", "кандидат просит человека"), (list[0].Mode, list[0].HandoffReason));
        Assert.Equal("ai", list.Single(r => r.Id == aiId).Mode);
        Assert.Equal("Водитель-экспедитор", list.Single(r => r.Id == aiId).VacancyTitle);
        Assert.Equal("ai", list.Single(r => r.Id == aiId).LastAuthor);
        Assert.Equal(1, list.Single(r => r.Id == aiId).Unread); // одно сообщение кандидата — согласия больше не спрашивают
        var summary = await dialogs.SummaryAsync(_hr.Hr, false, default);
        Assert.Equal((3, 2, 1, 3), (summary.Counts.Total, summary.Counts.Ai, summary.Counts.NeedHr, summary.Counts.Unread));
        Assert.Equal(3, await dialogs.UnreadDialogsAsync(_hr.Hr, default)); // подпись кнопки «Диалоги Telegram» — то же число
        Assert.True(summary.Number!.Connected);

        // Открыл — прочитано у него; у другого сотрудника — по-прежнему непрочитанные.
        var dialog = await dialogs.GetAsync(_hr.Hr, needHrId, default);
        Assert.Equal(0, (await dialogs.ListAsync(_hr.Hr, null, default)).Single(r => r.Id == needHrId).Unread);
        Assert.Equal(2, (await dialogs.ListAsync(new RecruitmentContext(fixture.ViewerId, true, false, []), null, default)).Single(r => r.Id == needHrId).Unread);
        Assert.Equal((2, 3), (await dialogs.UnreadDialogsAsync(_hr.Hr, default), await dialogs.UnreadDialogsAsync(new RecruitmentContext(fixture.ViewerId, true, false, []), default)));
        Assert.Contains(dialog.Items, i => i is { Kind: "system", Event: "handoff_to_hr" } && i.Text!.Contains("кандидат просит человека"));
        Assert.Contains(dialog.Items, i => i.Kind == "candidate" && i.Text == "Можно поговорить с человеком?");
        Assert.Contains(dialog.Items, i => i.Kind == "ai");

        // Ответ HR: «Нужен HR» → «HR ведёт», то, что ждало человека, решено.
        await sp.GetRequiredService<RecruitmentTelegramService>().SendAsync(_hr.Hr, needHrId, new SendInput("Здравствуйте! Я Дилноза из HR, слушаю вас.", Guid.NewGuid()), default);
        Assert.Equal("hr", (await dialogs.ListAsync(_hr.Hr, null, default)).Single(r => r.Id == needHrId).Mode);
        Assert.False(await db.RecruitmentExceptions.AnyAsync(e => e.ConversationId == needHrId && e.Status == RecruitmentExceptionStatus.Open));
        var hrMessage = (await dialogs.GetAsync(_hr.Hr, needHrId, default)).Items.Single(i => i.Kind == "hr");
        Assert.Equal(("pending", true, "Администратор"), (hrMessage.Status, hrMessage.Self, hrMessage.Author));

        // Вернул AI — событие в ленте; взял диалог — тоже.
        var telegram = sp.GetRequiredService<RecruitmentTelegramService>();
        await telegram.SetModeAsync(_hr.Hr, needHrId, TelegramConversationMode.Ai, default);
        await telegram.SetModeAsync(_hr.Hr, aiId, TelegramConversationMode.Manual, default);
        Assert.Contains((await dialogs.GetAsync(_hr.Hr, needHrId, default)).Items, i => i.Event == "returned_to_ai" && i.Text!.StartsWith("Диалог возвращён AI"));
        Assert.Contains((await dialogs.GetAsync(_hr.Hr, aiId, default)).Items, i => i.Event == "hr_took_over");
        Assert.Equal("ai", (await dialogs.ListAsync(_hr.Hr, null, default)).Single(r => r.Id == needHrId).Mode);

        // Карточка кандидата: вакансия и место, этап, собеседование, оценка AI по критериям, анкета.
        var aiCandidate = (await HrHarness.CandidateAsync(db, ai)).Id;
        var application = await db.CandidateApplications.SingleAsync(a => a.CandidateId == aiCandidate);
        application.ScreeningAnswers = "{\"Опыт\":\"2 года, Mediapark\",\"Готов выйти\":\"с понедельника\"}";
        db.CandidateScores.Add(new CandidateScore { ApplicationId = application.Id, CandidateId = aiCandidate, Overall = 82, Summary = "Подходит",
            CriteriaJson = "[{\"name\":\"Опыт в продажах\",\"score\":8.5},{\"name\":\"Локация\",\"score\":9}]" });
        await db.SaveChangesAsync();
        var slots = await sp.GetRequiredService<InterviewService>().FreeSlotsAsync(vacancy, 1, 14, default);
        await sp.GetRequiredService<InterviewService>().BookAsync(application.Id, slots[0].StartsAt, null, null, null, default);
        var card = (await dialogs.GetAsync(_hr.Hr, aiId, default)).Candidate!;
        Assert.Equal(("Водитель-экспедитор", "Продажи · Ташкент", "Interview", slots[0].StartsAt), (card.VacancyTitle, card.VacancyPlace, card.Stage, card.InterviewAt!.Value));
        Assert.Equal(82, card.Score!.Total);
        Assert.Equal(("Локация", 90), (card.Score.Criteria[1].Label, card.Score.Criteria[1].Value));
        Assert.Contains(card.Form, f => f.Label == "Опыт" && f.Value == "2 года, Mediapark");
        Assert.Contains((await dialogs.GetAsync(_hr.Hr, aiId, default)).Items, i => i.Event == "interview_scheduled" && i.Text!.StartsWith("Собеседование назначено"));

        // Шаблоны — на языке и графике кандидата; адрес — из настроек собеседования вакансии.
        var templates = (await dialogs.GetAsync(_hr.Hr, uzId, default)).Templates;
        Assert.Equal(["address", "invite", "reject"], templates.Select(t => t.Key));
        Assert.Equal("Офисимиз манзили: Ташкент, ул. Навои, 1. Возьмите паспорт.", templates[0].Text);
        Assert.All(templates.Skip(1), t => Assert.Equal(CandidateLanguages.UzCyrl, new LanguageDetectionService().Detect(t.Text).Code));
        Assert.StartsWith("Приглашаем вас на собеседование на вакансию «Водитель-экспедитор»",
            (await dialogs.GetAsync(_hr.Hr, aiId, default)).Templates.Single(t => t.Key == "invite").Text);

        // Только просмотр — читать можно, писать и звать AI — нет.
        var viewer = new RecruitmentContext(fixture.ViewerId, true, false, []);
        Assert.NotNull(await dialogs.GetAsync(viewer, aiId, default));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => dialogs.DraftAsync(viewer, aiId, default));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => telegram.SendAsync(viewer, aiId, new SendInput("Привет", Guid.NewGuid()), default));
    }

    [PostgresFact]
    public async Task Ai_draft_is_not_sent_files_go_through_the_outbox_failed_messages_retry_and_admins_are_asked()
    {
        var (scope, db) = await _hr.StartAsync();
        using var _ = scope;
        var sp = scope.ServiceProvider;
        var vacancy = await _hr.OpenVacancyAsync(sp);
        var number = (await db.Vacancies.AsNoTracking().SingleAsync(v => v.Id == vacancy)).Number;
        var dialogs = sp.GetRequiredService<HrDialogService>();
        var (chat, id) = await CandidateAsync(db, number);

        // «Черновик AI»: текст по всей переписке, ничего не отправлено.
        var queued = await db.TelegramOutgoingMessages.CountAsync();
        fixture.Gateway.Script.Enqueue(m =>
        {
            Assert.Contains("черновик следующего сообщения", m[^1].Content);
            return "{\"action\":\"reply\",\"messages\":[\"Спасибо за ответ!\",\"Когда вам удобно прийти на собеседование?\"]}";
        });
        var draft = await dialogs.DraftAsync(_hr.Hr, id, default);
        Assert.Equal("Спасибо за ответ!\n\nКогда вам удобно прийти на собеседование?", draft);
        Assert.Equal(queued, await db.TelegramOutgoingMessages.CountAsync());
        Assert.Equal("hr.recruiter.draft", fixture.Gateway.Calls[^1].Options.Context.Purpose);

        // Провайдер не ответил (нет ключа, сбой) — понятная причина вместо «ошибки сервера», ничего не отправлено.
        fixture.Gateway.Script.Enqueue(_ => throw new AiProviderException("anthropic", 401, "Нет ключа API Anthropic — добавьте его в «Настройки → AI → Провайдеры».", false));
        var noKey = await Assert.ThrowsAsync<FieldValidationException>(() => dialogs.DraftAsync(_hr.Hr, id, default));
        Assert.Equal("Черновик недоступен: Нет ключа API Anthropic — добавьте его в «Настройки → AI → Провайдеры».", noKey.Message);
        fixture.Gateway.Script.Enqueue(_ => throw new AiProviderException("anthropic", 529, "Overloaded", true));
        var busy = await Assert.ThrowsAsync<FieldValidationException>(() => dialogs.DraftAsync(_hr.Hr, id, default));
        Assert.Equal("AI сейчас не отвечает — попробуйте ещё раз или напишите сами.", busy.Message);
        Assert.Equal(queued, await db.TelegramOutgoingMessages.CountAsync());

        // Файл от HR: в хранилище, через очередь, с подписью; в ленте — вложение; диалог — на человека.
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n% вакансия\n");
        await dialogs.SendFileAsync(_hr.Hr, id, new RecruitmentUpload("Вакансия.pdf", "application/pdf", pdf.Length, new MemoryStream(pdf)), "Описание вакансии",
            Guid.NewGuid(), default);
        var row = await db.TelegramOutgoingMessages.AsNoTracking().OrderByDescending(m => m.CreatedAt).FirstAsync();
        Assert.Equal(("Вакансия.pdf", "application/pdf", "Описание вакансии"), (row.FileName, row.FileContentType, row.Text));
        Assert.True(_hr.Stored(row.FileKey!));
        Assert.Equal(TelegramConversationMode.Manual, (await db.TelegramConversations.AsNoTracking().SingleAsync(c => c.Id == id)).Mode);
        await _hr.SendQueuedAsync();
        var sent = fixture.Telegram.Sent.Last(s => s.Peer.PeerId == chat.Peer.PeerId).Message;
        Assert.Equal(("Описание вакансии", "Вакансия.pdf"), (sent.Text, Assert.Single(sent.Files).FileName));
        Assert.Contains((await dialogs.GetAsync(_hr.Hr, id, default)).Items, i => i.Kind == "hr" && i.Attachment?.FileName == "Вакансия.pdf");
        await Assert.ThrowsAsync<FieldValidationException>(() => dialogs.SendFileAsync(_hr.Hr, id,
            new RecruitmentUpload("run.exe", null, 2, new MemoryStream([1, 2])), null, Guid.NewGuid(), default));

        // Не отправилось — «Повторить»: прежнее «отменено», то же сообщение снова в очереди.
        await sp.GetRequiredService<RecruitmentTelegramService>().SendAsync(_hr.Hr, id, new SendInput("Ждём вас завтра", Guid.NewGuid()), default);
        var failedRow = await db.TelegramOutgoingMessages.SingleAsync(m => m.Text == "Ждём вас завтра");
        failedRow.Status = TelegramOutboxStatus.Failed;
        failedRow.Error = "Нет связи с Telegram";
        await db.SaveChangesAsync();
        var item = (await dialogs.GetAsync(_hr.Hr, id, default)).Items.Single(i => i.Text == "Ждём вас завтра");
        Assert.Equal(("failed", failedRow.Id), (item.Status, item.OutgoingId!.Value));
        var after = await dialogs.RetryAsync(_hr.Hr, id, failedRow.Id, default);
        Assert.Equal("pending", after.Items.Single(i => i.Text == "Ждём вас завтра").Status);
        Assert.Equal(TelegramOutboxStatus.Cancelled, (await db.TelegramOutgoingMessages.AsNoTracking().SingleAsync(m => m.Id == failedRow.Id)).Status);
        await Assert.ThrowsAsync<FieldConflictException>(() => dialogs.RetryAsync(_hr.Hr, id, failedRow.Id, default));

        // «Попросить администратора»: тем, кто настраивает Telegram, — уведомление; второй раз за сутки — нет.
        var role = new Role { Name = "Telegram теста" };
        role.Permissions.Add(new RolePermission { Code = Permissions.TelegramManage });
        var admin = new User { Login = "tg-admin-" + Guid.NewGuid().ToString("N")[..6], FullName = "Админ Telegram" };
        admin.Roles.Add(new UserRole { Role = role });
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        Assert.Equal(1, await dialogs.AskAdminAsync(_hr.Hr, default));
        Assert.Contains(await db.UserNotifications.AsNoTracking().ToListAsync(), n => n.RecipientId == admin.Id && n.Title.Contains("подключить номер HR"));
        await Assert.ThrowsAsync<FieldConflictException>(() => dialogs.AskAdminAsync(_hr.Hr, default));
    }
}
