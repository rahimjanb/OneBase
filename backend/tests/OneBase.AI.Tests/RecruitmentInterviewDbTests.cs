using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Field;
using OneBase.Application.Recruitment;
using OneBase.Application.Recruitment.Jobs;
using OneBase.Application.Security;
using OneBase.Application.Work;
using OneBase.Domain.Recruitment;
using OneBase.Domain.Work;
using OneBase.Infrastructure.Persistence;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>
/// Собеседования (сценарии 17–27, 30): приглашение подходящего кандидата (порог оценки вакансии), выбор времени, одна запись — одно событие
/// календаря, «предложено» и «ждёт подтверждения» — не собеседование, конфликт времени и одновременный выбор, перенос и отмена в обоих местах,
/// защита события в общем календаре, уведомления (участники, руководитель отдела, кандидат), итоги, задания переживают перезапуск.
/// </summary>
public class RecruitmentInterviewDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private readonly HrHarness _hr = new(fixture, 9300);

    /// <summary>Понедельник 16.11.2026, 08:00 Ташкента — слоты с 10:00 (минимальный срок 2 ч).</summary>
    private static readonly DateTimeOffset Monday = new(2026, 11, 16, 3, 0, 0, TimeSpan.Zero);

    /// <summary>Время Ташкента (в UTC — так его хранит PostgreSQL).</summary>
    private static DateTimeOffset Local(int day, int hour) => new DateTimeOffset(2026, 11, day, hour, 0, 0, TimeSpan.FromHours(5)).ToUniversalTime();

    private async Task<(IServiceScope Scope, OneBaseDbContext Db, Guid Vacancy)> StartAsync()
    {
        var (scope, db) = await _hr.StartAsync();
        fixture.Clock.Now = Monday;
        await db.WorkDepartmentHeads.ExecuteDeleteAsync();
        var vacancy = await _hr.OpenVacancyAsync(scope.ServiceProvider, v => v.Criteria = "Опыт\nКатегория C");
        // Настройки собеседования вакансии: ответственный — админ, участник — наблюдатель, часы 10–13 с перерывом 12–13, час, пн–пт, порог 70.
        await scope.ServiceProvider.GetRequiredService<InterviewService>().SaveSettingsAsync(_hr.Hr, vacancy, new InterviewSettingsInput(fixture.AdminId,
            [fixture.ViewerId], 60, "1,2,3,4,5", "10:00", "13:00", "12:00-13:00", 2, 14, "office", "Ташкент, ул. Навои, 1", null, null, 70,
            "Возьмите паспорт и диплом"), default);
        // Руководитель отдела вакансии (продажи).
        db.WorkDepartmentHeads.Add(new WorkDepartmentHead { DepartmentId = _hr.Sales, UserId = fixture.DataAdminId });
        await db.SaveChangesAsync();
        return (scope, db, vacancy);
    }

    /// <summary>Кандидат с откликом на вакансию: написал — рекрутер сразу ответил.</summary>
    private async Task<HrHarness.Chat> CandidateAsync(OneBaseDbContext db, Guid vacancy)
    {
        var number = (await db.Vacancies.AsNoTracking().SingleAsync(v => v.Id == vacancy)).Number;
        var chat = _hr.NewChat();
        fixture.Gateway.Script.Enqueue(_ => "{\"action\":\"reply\",\"messages\":[\"Спасибо! Сколько лет вы водите грузовик?\"]}");
        await _hr.SayAsync(chat, $"Здравствуйте, я по вакансии #V{number}");
        return chat;
    }

    private static string Score(int experience, int category) => JsonSerializer.Serialize(new
    {
        criteria = new[]
        {
            new { name = "Опыт", score = experience, explanation = "стаж", evidence = "5 лет" },
            new { name = "Категория C", score = category, explanation = "права", evidence = "категория C" },
        },
        summary = "Опытный водитель категории C",
        risks = "",
    });

    private static async Task<Application> ApplicationOf(OneBaseDbContext db, HrHarness.Chat chat)
    {
        var candidate = await HrHarness.CandidateAsync(db, chat);
        return new Application((await db.CandidateApplications.AsNoTracking().SingleAsync(a => a.CandidateId == candidate.Id)).Id, candidate.Id);
    }

    private sealed record Application(Guid Id, Guid CandidateId);

    /// <summary>Действие HR — в своей области DI, как отдельный запрос к API (кандидат тем временем мог изменить собеседование).</summary>
    private async Task<T> HrAsync<T>(Func<InterviewService, Task<T>> action)
    {
        using var scope = fixture.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<InterviewService>());
    }

    [PostgresFact]
    public async Task Qualified_candidate_is_invited_picks_a_time_and_the_interview_is_in_hr_and_calendar_once()
    {
        var (scope, db, vacancy) = await StartAsync();
        using var _ = scope;
        var chat = await CandidateAsync(db, vacancy);

        // 17. Модель решила пригласить; оценки нет — система оценивает (90 ≥ 70) и предлагает время из календаря, а не модель.
        fixture.Gateway.Script.Enqueue(_ => "{\"action\":\"propose_interview\",\"messages\":[\"Ваш опыт нам подходит!\"]}");
        fixture.Gateway.Script.Enqueue(_ => Score(9, 9));
        await _hr.SayAsync(chat, "5 лет вожу грузовик, категория C");
        var invitation = (await HrHarness.SentAsync(db, chat))[^1];
        Assert.Contains("Ваш опыт нам подходит!", invitation);
        Assert.Contains("1) пн 16.11, 10:00\n2) вт 17.11, 10:00\n3) ср 18.11, 10:00", invitation);
        Assert.Contains("другое время", invitation);
        var app = await ApplicationOf(db, chat);
        var proposed = await db.Interviews.AsNoTracking().SingleAsync(i => i.ApplicationId == app.Id);
        // 20. Предложено — не собеседование: события календаря нет, время не занято.
        Assert.Equal((InterviewStatus.Proposed, (Guid?)null), (proposed.Status, proposed.CalendarEventId));
        Assert.Empty(await db.CalendarEvents.AsNoTracking().ToListAsync());

        // 18. Выбор «2» — без модели: запись, событие календаря, этап «Собеседование».
        var calls = fixture.Gateway.Calls.Count;
        await _hr.SayAsync(chat, "2");
        Assert.Equal(calls, fixture.Gateway.Calls.Count);
        Assert.Contains("собеседование назначено на 17.11 10:00", (await HrHarness.SentAsync(db, chat))[^1]);
        Assert.Contains("ул. Навои, 1", (await HrHarness.SentAsync(db, chat))[^1]);
        var interview = await db.Interviews.AsNoTracking().SingleAsync(i => i.ApplicationId == app.Id);
        Assert.Equal((InterviewStatus.Confirmed, CandidateConfirmation.Confirmed, Local(17, 10)), (interview.Status, interview.CandidateConfirmation, interview.StartsAt));
        Assert.Equal(fixture.AdminId, interview.InterviewerId);
        Assert.Equal(ApplicationStage.Interview, (await db.CandidateApplications.AsNoTracking().SingleAsync(a => a.Id == app.Id)).Stage);

        // 22. В календаре OneBase: событие с интервьюером и участником, кандидатом и вакансией в названии, инструкцией и адресом.
        var calendar = await db.CalendarEvents.AsNoTracking().Include(e => e.Participants).SingleAsync();
        Assert.Equal(interview.CalendarEventId, calendar.Id);
        Assert.Equal(new[] { fixture.AdminId, fixture.ViewerId }.Order(), calendar.Participants.Select(p => p.UserId).Order());
        Assert.Contains("Водитель-экспедитор", calendar.Title);
        Assert.Contains("Возьмите паспорт и диплом", calendar.Description);
        Assert.Equal(("Ташкент, ул. Навои, 1", Local(17, 10), Local(17, 11)), (calendar.Location, calendar.StartsAt, calendar.EndsAt));

        // 21. В HR → «Собеседования»: вид «завтра» и «неделя»; кандидат, вакансия, отдел, интервьюер, формат, подтверждение, оценка AI.
        var interviews = scope.ServiceProvider.GetRequiredService<InterviewService>();
        Assert.Empty(await interviews.ListAsync(_hr.Hr, "today", default));
        var row = Assert.Single(await interviews.ListAsync(_hr.Hr, "tomorrow", default));
        Assert.Single(await interviews.ListAsync(_hr.Hr, "week", default));
        Assert.Equal(("Confirmed", "Confirmed", "Продажи", "Администратор", "office", 90), (row.Status, row.CandidateConfirmation, row.Department, row.Interviewer, row.Format, row.AiScore));
        Assert.Contains("Опытный водитель", row.AiSummary);

        // 26. Уведомления — после записи, заданием: участникам — в календарь, руководителю отдела — о подборе; кандидату — нет (он выбрал сам).
        await _hr.DrainAsync();
        var notes = await db.UserNotifications.AsNoTracking().ToListAsync();
        Assert.Contains(notes, n => n.RecipientId == fixture.AdminId && n.Kind == UserNotificationKind.CalendarInvite && n.Title.Contains("17.11 10:00"));
        Assert.Contains(notes, n => n.RecipientId == fixture.ViewerId && n.Kind == UserNotificationKind.CalendarInvite);
        Assert.Contains(notes, n => n.RecipientId == fixture.DataAdminId && n.Kind == UserNotificationKind.RecruitmentAlert);
        Assert.DoesNotContain(await HrHarness.SentAsync(db, chat), t => t.Contains("Предлагаем собеседование", StringComparison.Ordinal));

        // 25. Повторы не создают второго события: «да» ещё раз, восстановление календаря, сверка.
        await _hr.SayAsync(chat, "да");
        Assert.False(await interviews.RepairCalendarAsync(interview.Id, default));
        await scope.ServiceProvider.GetServices<IRecruitmentSweeper>().Single(s => s is InterviewSweeper).SweepAsync(default);
        await _hr.DrainAsync();
        Assert.Equal(1, await db.CalendarEvents.CountAsync());
        // Событие удалили мимо OneBase — сверка восстанавливает одно событие.
        await db.CalendarEvents.ExecuteDeleteAsync();
        await scope.ServiceProvider.GetServices<IRecruitmentSweeper>().Single(s => s is InterviewSweeper).SweepAsync(default);
        await _hr.DrainAsync();
        Assert.Equal(1, await db.CalendarEvents.CountAsync());
        Assert.Equal((await db.CalendarEvents.AsNoTracking().SingleAsync()).Id, (await db.Interviews.AsNoTracking().SingleAsync()).CalendarEventId);
    }

    [PostgresFact]
    public async Task Low_score_is_not_invited_automatically_and_goes_to_hr()
    {
        var (scope, db, vacancy) = await StartAsync();
        using var _ = scope;
        var chat = await CandidateAsync(db, vacancy);
        fixture.Gateway.Script.Enqueue(_ => "{\"action\":\"propose_interview\",\"messages\":[\"Приглашаем!\"]}");
        fixture.Gateway.Script.Enqueue(_ => Score(3, 4));
        await _hr.SayAsync(chat, "Опыта почти нет, права категории B");
        Assert.Equal(RecruiterTexts.PassToColleagues(CandidateLang.Ru), (await HrHarness.SentAsync(db, chat))[^1]);
        Assert.False(await db.Interviews.AnyAsync());
        Assert.Contains(await db.RecruitmentExceptions.AsNoTracking().ToListAsync(), e => e.Kind == "review" && e.Message.Contains("ниже порога вакансии 70"));
    }

    [PostgresFact]
    public async Task Hr_time_waits_for_the_candidates_yes_conflicts_are_detected_and_only_one_of_two_simultaneous_picks_wins()
    {
        var (scope, db, vacancy) = await StartAsync();
        using var _ = scope;
        var interviews = scope.ServiceProvider.GetRequiredService<InterviewService>();
        var first = await CandidateAsync(db, vacancy);
        var second = await CandidateAsync(db, vacancy);
        var a = await ApplicationOf(db, first);
        var b = await ApplicationOf(db, second);

        // 19. У интервьюера встреча во вторник 10:00 — это время не предлагается, а записать на него нельзя.
        db.CalendarEvents.Add(new CalendarEvent { Title = "Планёрка", StartsAt = Local(17, 10), EndsAt = Local(17, 11), CreatedById = fixture.AdminId });
        await db.SaveChangesAsync();
        var free = await interviews.FreeSlotsAsync(vacancy, 3, 14, default);
        Assert.Equal([Local(16, 10), Local(17, 11), Local(18, 10)], free.Select(s => s.StartsAt)); // вторник — 11:00, после планёрки
        await Assert.ThrowsAsync<FieldConflictException>(() => interviews.BookAsync(a.Id, Local(17, 10), null, null, fixture.AdminId, default));
        // Перерыв 12–13 — не предлагается и в «плотном» режиме (все слоты дня).
        Assert.DoesNotContain(await interviews.FreeSlotsAsync(vacancy, 20, 3, default, onePerDay: false), s => s.StartsAt == Local(16, 12));

        // HR предложил время, кандидат ещё не ответил: «ждёт подтверждения», события нет, время удержано (второму кандидату — занято).
        await interviews.BookByHrAsync(_hr.Hr, new BookInput(a.Id, Local(18, 11), null, null), default);
        var waiting = await db.Interviews.AsNoTracking().SingleAsync(i => i.ApplicationId == a.Id);
        Assert.Equal((InterviewStatus.Awaiting, (Guid?)null), (waiting.Status, waiting.CalendarEventId));
        Assert.Equal(1, await db.CalendarEvents.CountAsync()); // только планёрка
        await Assert.ThrowsAsync<FieldConflictException>(() => interviews.BookAsync(b.Id, Local(18, 11), null, null, null, default));
        await _hr.DrainAsync();
        Assert.Contains("Предлагаем собеседование 18.11 11:00", (await HrHarness.SentAsync(db, first))[^1]);

        // «Да» кандидата — подтверждено, событие календаря появилось.
        await _hr.SayAsync(first, "Да, удобно");
        var confirmed = await db.Interviews.AsNoTracking().SingleAsync(i => i.ApplicationId == a.Id);
        Assert.Equal((InterviewStatus.Confirmed, CandidateConfirmation.Confirmed), (confirmed.Status, confirmed.CandidateConfirmation));
        Assert.NotNull(confirmed.CalendarEventId);
        Assert.Contains("собеседование назначено на 18.11 11:00", (await HrHarness.SentAsync(db, first))[^1]);

        // Два кандидата одновременно выбирают одно и то же время (пятница 10:00): записан один, второму — «занято».
        var thirdChat = await CandidateAsync(db, vacancy);
        var c = await ApplicationOf(db, thirdChat);
        var results = await Task.WhenAll(new[] { b.Id, c.Id }.Select(async id =>
        {
            using var s = fixture.Services.CreateScope();
            try
            {
                await s.ServiceProvider.GetRequiredService<InterviewService>().BookAsync(id, Local(20, 10), null, null, null, default);
                return "ok";
            }
            catch (FieldConflictException)
            {
                return "busy";
            }
        }));
        Assert.Equal(["busy", "ok"], results.Order());
        Assert.Equal(1, await db.Interviews.CountAsync(i => i.StartsAt == Local(20, 10) && i.Status == InterviewStatus.Confirmed));
        Assert.Equal(1, await db.CalendarEvents.CountAsync(e => e.StartsAt == Local(20, 10)));
    }

    [PostgresFact]
    public async Task Rescheduling_and_cancelling_change_both_views_the_calendar_event_is_guarded_and_everyone_is_told()
    {
        var (scope, db, vacancy) = await StartAsync();
        using var _ = scope;
        var interviews = scope.ServiceProvider.GetRequiredService<InterviewService>();
        var chat = await CandidateAsync(db, vacancy);
        var app = await ApplicationOf(db, chat);
        var booked = await interviews.BookAsync(app.Id, Local(17, 10), null, null, null, default);
        var eventId = booked.CalendarEventId!.Value;
        await _hr.DrainAsync();

        // Событие собеседования в общем календаре не меняется и не удаляется в обход собеседования.
        var calendar = scope.ServiceProvider.GetRequiredService<CalendarService>();
        var boss = new WorkContext(fixture.AdminId, new HashSet<string> { Permissions.UsersManage }, null);
        Assert.Equal(("recruitment", false), ((await calendar.GetAsync(boss, eventId, default)).ManagedBy, (await calendar.GetAsync(boss, eventId, default)).CanEdit));
        await Assert.ThrowsAsync<FieldConflictException>(() => calendar.DeleteAsync(boss, eventId, default));
        await Assert.ThrowsAsync<FieldConflictException>(() => calendar.UpdateAsync(boss, eventId, new CalendarEventInput("Сдвинул", null, CalendarEventKind.Meeting,
            new DateOnly(2026, 11, 17), new TimeOnly(15, 0), new TimeOnly(16, 0), false, null, [], null, false), default));

        // 23. HR переносит: то же событие на новое время, статус «перенесено», кандидат должен подтвердить; участникам и кандидату — сообщение.
        var moved = await HrAsync(i => i.RescheduleByHrAsync(_hr.Hr, booked.Id, new RescheduleInput(Local(19, 11)), default));
        Assert.Equal(("Rescheduled", "Pending", Local(17, 10), eventId), (moved.Status, moved.CandidateConfirmation, moved.PreviousStartsAt, moved.CalendarEventId!.Value));
        Assert.Equal(Local(19, 11), (await db.CalendarEvents.AsNoTracking().SingleAsync(e => e.Id == eventId)).StartsAt);
        Assert.Single(await interviews.ListAsync(_hr.Hr, "rescheduled", default));
        await _hr.DrainAsync();
        Assert.Contains("Собеседование перенесено на 19.11 11:00", (await HrHarness.SentAsync(db, chat))[^1]);
        Assert.Contains(await db.UserNotifications.AsNoTracking().ToListAsync(), n => n.RecipientId == fixture.ViewerId && n.Title.Contains("перенесено на 19.11 11:00"));
        await _hr.SayAsync(chat, "да");
        Assert.Equal(CandidateConfirmation.Confirmed, (await db.Interviews.AsNoTracking().SingleAsync()).CandidateConfirmation);

        // Кандидат просит перенести — новые варианты (позже назначенного); выбор — перенос того же события, сразу подтверждено.
        await _hr.SayAsync(chat, "Можно перенести на другой день?");
        Assert.Contains("1) пт 20.11, 10:00", (await HrHarness.SentAsync(db, chat))[^1]);
        await _hr.SayAsync(chat, "1");
        var again = await db.Interviews.AsNoTracking().SingleAsync();
        Assert.Equal((InterviewStatus.Rescheduled, CandidateConfirmation.Confirmed, Local(20, 10), eventId), (again.Status, again.CandidateConfirmation, again.StartsAt, again.CalendarEventId!.Value));
        Assert.Equal(Local(20, 10), (await db.CalendarEvents.AsNoTracking().SingleAsync(e => e.Id == eventId)).StartsAt);

        // 24. Отмена HR: «отменено» в HR, событие удалено из календаря, кандидату и участникам — сообщение.
        await HrAsync(i => i.CancelByHrAsync(_hr.Hr, again.Id, new CancelInput("Вакансия закрывается"), default));
        Assert.Empty(await db.CalendarEvents.AsNoTracking().ToListAsync());
        var cancelled = Assert.Single(await interviews.ListAsync(_hr.Hr, "cancelled", default));
        Assert.Equal(("hr", "Вакансия закрывается"), (cancelled.CancelledBy, cancelled.CancelReason));
        await _hr.DrainAsync();
        Assert.Contains("Собеседование 20.11 10:00 отменено", (await HrHarness.SentAsync(db, chat))[^1]);
        Assert.Contains(await db.UserNotifications.AsNoTracking().ToListAsync(), n => n.RecipientId == fixture.AdminId && n.Title.Contains("отменено"));

        // Кандидат на узбекской кириллице отказывается от нового приглашения — без модели, ответ кириллицей.
        var uz = _hr.NewChat();
        var number = (await db.Vacancies.AsNoTracking().SingleAsync()).Number;
        fixture.Gateway.Script.Enqueue(_ => "{\"action\":\"reply\",\"messages\":[\"Раҳмат! Тажрибангиз қанча?\"]}");
        await _hr.SayAsync(uz, $"Ассалому алайкум, иш ҳақида маълумот берсангиз #V{number}");
        await _hr.SendQueuedAsync();
        var uzApp = await ApplicationOf(db, uz);
        await HrAsync(i => i.InviteAsync(uzApp.Id, fixture.AdminId, default));
        Assert.Contains("1) Ду 16.11, 10:00", (await HrHarness.SentAsync(db, uz))[^1]);
        await _hr.SayAsync(uz, "Кечирасиз, келолмайман");
        Assert.Equal(RecruiterTexts.Declined(CandidateLang.UzCyrl), (await HrHarness.SentAsync(db, uz))[^1]);
        var declined = await db.Interviews.AsNoTracking().SingleAsync(i => i.ApplicationId == uzApp.Id);
        Assert.Equal((InterviewStatus.Cancelled, "candidate"), (declined.Status, declined.CancelledBy));
    }

    [PostgresFact]
    public async Task Candidate_names_a_day_and_time_and_the_recruiter_checks_the_interviewers_calendar_moves_or_offers_the_nearest_free_time()
    {
        var (scope, db, vacancy) = await StartAsync();
        using var _ = scope;
        var chat = await CandidateAsync(db, vacancy);
        var app = await ApplicationOf(db, chat);
        // У интервьюера в четверг 11:00 планёрка.
        db.CalendarEvents.Add(new CalendarEvent { Title = "Планёрка", StartsAt = Local(19, 11), EndsAt = Local(19, 12), CreatedById = fixture.AdminId });
        await db.SaveChangesAsync();

        // Кандидат подходит и сам называет время — оно свободно: запись сразу, без вариантов (оценка 90 ≥ порога 70).
        fixture.Gateway.Script.Enqueue(m =>
        {
            Assert.Contains("заполни wish", m[0].Content);
            return "{\"action\":\"propose_interview\",\"messages\":[\"Отлично, приглашаем!\"],\"wish\":{\"date\":\"2026-11-17\",\"time\":\"10:00\"}}";
        });
        fixture.Gateway.Script.Enqueue(_ => Score(9, 9));
        await _hr.SayAsync(chat, "5 лет, категория C. Можно во вторник в 10?");
        var booked = await db.Interviews.AsNoTracking().SingleAsync(i => i.ApplicationId == app.Id);
        Assert.Equal((InterviewStatus.Confirmed, Local(17, 10)), (booked.Status, booked.StartsAt));
        Assert.Contains("собеседование назначено на 17.11 10:00", (await HrHarness.SentAsync(db, chat))[^1]);
        var eventId = booked.CalendarEventId!.Value;

        // «Перенесите на четверг в 10:00» — названо время: не «следующие варианты» без модели, а пожелание; свободно — перенос того же события.
        fixture.Gateway.Script.Enqueue(_ => "{\"action\":\"other_time\",\"messages\":[\"Сейчас посмотрю\"],\"wish\":{\"date\":\"2026-11-19\",\"time\":\"10:00\"}}");
        await _hr.SayAsync(chat, "Можно перенести на четверг в 10:00?");
        var moved = await db.Interviews.AsNoTracking().SingleAsync();
        Assert.Equal((InterviewStatus.Rescheduled, CandidateConfirmation.Confirmed, Local(19, 10)), (moved.Status, moved.CandidateConfirmation, moved.StartsAt));
        Assert.Equal(Local(19, 10), (await db.CalendarEvents.AsNoTracking().SingleAsync(e => e.Id == eventId)).StartsAt);
        Assert.Contains("собеседование назначено на 19.11 10:00", (await HrHarness.SentAsync(db, chat))[^1]);

        // Четверг 11:00 занят планёркой — ближайшее свободное (текущее время не предлагается); назначенное в силе, пока кандидат не выбрал.
        fixture.Gateway.Script.Enqueue(_ => "{\"action\":\"other_time\",\"messages\":[],\"wish\":{\"date\":\"2026-11-19\",\"time\":\"11:00\"}}");
        await _hr.SayAsync(chat, "А в четверг в 11:00 получится?");
        var offer = (await HrHarness.SentAsync(db, chat))[^1];
        Assert.StartsWith(RecruiterTexts.WishBusy(CandidateLang.Ru), offer);
        Assert.Contains("1) пт 20.11, 10:00\n2) пн 23.11, 10:00\n3) вт 24.11, 10:00", offer);
        Assert.Equal(Local(19, 10), (await db.Interviews.AsNoTracking().SingleAsync()).StartsAt);
        await _hr.SayAsync(chat, "2");
        Assert.Equal(Local(23, 10), (await db.Interviews.AsNoTracking().SingleAsync()).StartsAt);
        Assert.Equal(Local(23, 10), (await db.CalendarEvents.AsNoTracking().SingleAsync(e => e.Id == eventId)).StartsAt);

        // Промежуток («в пятницу ближе к обеду») — свободное время в нём (перерыв 12–13 не предлагается).
        fixture.Gateway.Script.Enqueue(_ => "{\"action\":\"other_time\",\"messages\":[],\"wish\":{\"date\":\"2026-11-20\",\"from\":\"11:00\",\"to\":\"13:00\"}}");
        await _hr.SayAsync(chat, "А в пятницу ближе к обеду?");
        Assert.Equal($"{RecruiterTexts.WishSlots(CandidateLang.Ru)}\n1) пт 20.11, 11:00", (await HrHarness.SentAsync(db, chat))[^1].Split("\n" + RecruiterTexts.ChooseNumber(CandidateLang.Ru))[0]);
        await _hr.SayAsync(chat, "1");
        Assert.Equal(Local(20, 11), (await db.Interviews.AsNoTracking().SingleAsync()).StartsAt);
        Assert.Equal(1, await db.CalendarEvents.CountAsync(e => e.Id == eventId));

        // HR: «Новое собеседование» — поиск отклика по вакансии (с действующим собеседованием), свободное время дня, запись второго кандидата.
        var interviews = scope.ServiceProvider.GetRequiredService<InterviewService>();
        var number = (await db.Vacancies.AsNoTracking().SingleAsync()).Number;
        var second = await CandidateAsync(db, vacancy);
        var b = await ApplicationOf(db, second);
        var found = await interviews.BookableAsync(_hr.Hr, $"V{number}", default);
        Assert.Equal(("Rescheduled", Local(20, 11)), (found.Single(r => r.ApplicationId == app.Id).InterviewStatus, found.Single(r => r.ApplicationId == app.Id).InterviewStartsAt));
        Assert.True(found.Single(r => r.ApplicationId == b.Id) is { InterviewId: null, HasTelegram: true });
        Assert.Empty(await interviews.BookableAsync(_hr.Hr, "Нет такого кандидата", default));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => interviews.BookableAsync(new RecruitmentContext(fixture.AdminId, true, false, []), null, default));
        Assert.Equal([Local(20, 10)], (await interviews.DaySlotsAsync(_hr.Hr, vacancy, new DateOnly(2026, 11, 20), default)).Select(x => x.StartsAt));
        await interviews.BookByHrAsync(_hr.Hr, new BookInput(b.Id, Local(20, 10), "online", null, true, "https://meet.example/x"), default);
        var hrBooked = await db.Interviews.AsNoTracking().SingleAsync(i => i.ApplicationId == b.Id);
        Assert.Equal((InterviewStatus.Confirmed, CandidateConfirmation.ByHr, "https://meet.example/x"), (hrBooked.Status, hrBooked.CandidateConfirmation, hrBooked.MeetingUrl));
        Assert.NotNull(hrBooked.CalendarEventId);
        Assert.Empty(await interviews.DaySlotsAsync(_hr.Hr, vacancy, new DateOnly(2026, 11, 20), default));
    }

    [PostgresFact]
    public async Task Feedback_is_stored_and_moves_the_stage_only_when_hr_chooses()
    {
        var (scope, db, vacancy) = await StartAsync();
        using var _ = scope;
        var interviews = scope.ServiceProvider.GetRequiredService<InterviewService>();
        var recruitment = scope.ServiceProvider.GetRequiredService<RecruitmentService>();
        var chat = await CandidateAsync(db, vacancy);
        var app = await ApplicationOf(db, chat);
        var booked = await interviews.BookAsync(app.Id, Local(16, 10), null, null, null, default);

        await Assert.ThrowsAsync<FieldValidationException>(() =>
            interviews.SaveFeedbackAsync(_hr.Hr, booked.Id, new FeedbackInput(InterviewResult.Passed, 6, 4, 4, null, null, null, null, null), recruitment, default));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => interviews.SaveFeedbackAsync(new RecruitmentContext(fixture.AdminId, true, false, []), booked.Id,
            new FeedbackInput(InterviewResult.Passed, 5, 4, 4, null, null, null, null, null), recruitment, default));

        // 27. Итоги: оценки, сильные и слабые стороны, рекомендация; без следующего этапа — этап не меняется.
        var view = await interviews.SaveFeedbackAsync(_hr.Hr, booked.Id, new FeedbackInput(InterviewResult.WaitingDecision, 5, 4, 4, "Знает город", "Нет опыта с 1С",
            "Спокойный, пунктуальный", "Рассмотреть", null), recruitment, default);
        Assert.Equal("Completed", view.Status);
        var feedback = Assert.Single(view.Feedback!);
        Assert.Equal(("WaitingDecision", 5, "Знает город", "Нет опыта с 1С"), (feedback.Result, feedback.Technical, feedback.Strengths, feedback.Weaknesses));
        Assert.Equal(ApplicationStage.Interview, (await db.CandidateApplications.AsNoTracking().SingleAsync(a => a.Id == app.Id)).Stage);

        // Правка тех же итогов (не вторая запись) и решение HR о следующем этапе.
        view = await interviews.SaveFeedbackAsync(_hr.Hr, booked.Id, new FeedbackInput(InterviewResult.Passed, 5, 5, 4, "Знает город", null, null, "Сделать оффер",
            ApplicationStage.Offer), recruitment, default);
        Assert.Equal(("Passed", "Offer"), (Assert.Single(view.Feedback!).Result, Assert.Single(view.Feedback!).NextStage));
        Assert.Equal(1, await db.InterviewFeedback.CountAsync());
        Assert.Equal(ApplicationStage.Offer, (await db.CandidateApplications.AsNoTracking().SingleAsync(a => a.Id == app.Id)).Stage);
        Assert.Single(await interviews.ListAsync(_hr.Hr, "completed", default));
    }

    [PostgresFact]
    public async Task Interview_jobs_survive_a_restart_and_unconfirmed_holds_are_released()
    {
        var (scope, db, vacancy) = await StartAsync();
        using var _ = scope;
        var interviews = scope.ServiceProvider.GetRequiredService<InterviewService>();
        var queue = scope.ServiceProvider.GetRequiredService<RecruitmentJobQueue>();
        var chat = await CandidateAsync(db, vacancy);
        var app = await ApplicationOf(db, chat);

        // 30. Уведомление о записи «взято и сервер упал» — после аренды задание выполняется, уведомления доходят один раз.
        await interviews.BookAsync(app.Id, Local(16, 10), null, null, null, default);
        var job = Assert.Single(await queue.ClaimAsync(RecruitmentQueues.Notifications, 5, default));
        await db.RecruitmentJobs.Where(j => j.Id == job.Id).ExecuteUpdateAsync(u => u.SetProperty(j => j.LockedUntil, DateTimeOffset.UtcNow.AddMinutes(-1)));
        await _hr.DrainAsync();
        Assert.Equal(1, await db.UserNotifications.CountAsync(n => n.RecipientId == fixture.ViewerId));
        var done = await db.RecruitmentJobs.AsNoTracking().SingleAsync(j => j.Id == job.Id);
        Assert.Equal((RecruitmentJobStatus.Done, 2), (done.Status, done.Attempts));

        // Время, предложенное HR и не подтверждённое за сутки, освобождается: «отменено системой», время снова свободно.
        var second = await CandidateAsync(db, vacancy);
        var b = await ApplicationOf(db, second);
        await interviews.BookByHrAsync(_hr.Hr, new BookInput(b.Id, Local(17, 11), null, null), default);
        Assert.DoesNotContain(await interviews.FreeSlotsAsync(vacancy, 20, 3, default, onePerDay: false), s => s.StartsAt == Local(17, 11));
        fixture.Clock.Now = Monday.AddHours(25);
        await scope.ServiceProvider.GetServices<IRecruitmentSweeper>().Single(s => s is InterviewSweeper).SweepAsync(default);
        var released = await db.Interviews.AsNoTracking().SingleAsync(i => i.ApplicationId == b.Id);
        Assert.Equal((InterviewStatus.Cancelled, "system"), (released.Status, released.CancelledBy));
        Assert.Contains(await interviews.FreeSlotsAsync(vacancy, 20, 3, default, onePerDay: false), s => s.StartsAt == Local(17, 11));
    }
}
