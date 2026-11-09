using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Recruitment.Documents;
using OneBase.Application.Recruitment.Jobs;
using OneBase.Application.Security;
using OneBase.Application.Telegram.Outbox;
using OneBase.Application.Telegram.Routing;
using OneBase.Application.Work;
using OneBase.Domain.Recruitment;
using OneBase.Domain.Telegram;
using OneBase.Domain.Work;

namespace OneBase.Application.Recruitment;

/// <summary>
/// hr.interview.notifications — уведомления о собеседовании после того, как запись зафиксирована: интервьюеру и участникам (колокольчик и push,
/// «приглашение в календарь»), HR и руководителю отдела вакансии; кандидату в Telegram на его языке — если изменение сделал HR (свои действия
/// кандидат уже видел в ответе рекрутера). Повтор задания не шлёт сообщение кандидату дважды (ключ очереди исходящих).
/// </summary>
public sealed class InterviewNotificationsJob(IAppDbContext db, UserNotifier notifier, TelegramOutbox outbox, InterviewService interviews) : IRecruitmentJobHandler
{
    public string Queue => RecruitmentQueues.Notifications;

    public async Task RunAsync(RecruitmentJob job, CancellationToken ct)
    {
        var interview = await db.Interviews.AsNoTracking().FirstOrDefaultAsync(i => i.Id == job.RelatedId, ct);
        if (interview is null)
        {
            return;
        }

        using var payload = JsonDocument.Parse(job.Payload ?? "{}");
        var kind = payload.RootElement.TryGetProperty("kind", out var k) ? k.GetString() ?? "confirmed" : "confirmed";
        var by = payload.RootElement.TryGetProperty("by", out var b) ? b.GetString() ?? "hr" : "hr";
        var candidate = await db.Candidates.AsNoTracking().FirstOrDefaultAsync(c => c.Id == interview.CandidateId, ct);
        var vacancy = await db.Vacancies.AsNoTracking().FirstOrDefaultAsync(v => v.Id == interview.VacancyId, ct);
        if (candidate is null || vacancy is null)
        {
            return;
        }

        var at = interview.StartsAt is { } s ? RecruiterTexts.At(InterviewService.Local(s, interview.TimeZone)) : "—";
        var (title, calendarKind) = kind switch
        {
            "confirmed" => ($"Собеседование {at}: {candidate.FullName}", true),
            "awaiting" => ($"Предложено время собеседования {at}: {candidate.FullName} — ждём подтверждения", false),
            "rescheduled" => ($"Собеседование перенесено на {at}: {candidate.FullName}", true),
            "candidate_confirmed" => ($"Кандидат подтвердил собеседование {at}: {candidate.FullName}", false),
            "declined" => ($"Кандидат отказался от собеседования: {candidate.FullName}", false),
            "expired" => ($"Кандидат не подтвердил время {at} — время освобождено: {candidate.FullName}", false),
            _ => ($"Собеседование {at} отменено: {candidate.FullName}", true),
        };
        var body = $"{vacancy.Title} ({vacancy.Code})" + (interview.CancelReason is { } reason && kind == "cancelled" ? $". Причина: {reason}" : string.Empty);
        var participants = InterviewService.ParticipantsOf(interview.Participants).ToHashSet();
        if (interview.InterviewerId is { } i)
        {
            participants.Add(i);
        }

        // Участникам — только то, что меняет их календарь; HR и руководителю отдела — всё.
        if (calendarKind)
        {
            foreach (var user in participants)
            {
                notifier.Notify(user, UserNotificationKind.CalendarInvite, title, body, "/calendar");
            }
        }

        var hr = await db.ActiveUsersWithAsync(Permissions.HrRecruitmentManage, ct);
        var heads = await db.WorkDepartmentHeads.AsNoTracking().Where(h => h.DepartmentId == vacancy.DepartmentId).Select(h => h.UserId).ToListAsync(ct);
        foreach (var user in hr.Concat(heads).Distinct().Where(u => !calendarKind || !participants.Contains(u)))
        {
            notifier.Notify(user, UserNotificationKind.RecruitmentAlert, title, body, "/hr/recruitment/interviews");
        }

        await db.SaveChangesAsync(ct);

        // Кандидату — об изменениях, которые сделал не он сам.
        if (by != "candidate" && kind is "awaiting" or "rescheduled" or "cancelled" or "confirmed" && await interviews.ConversationAsync(candidate.Id, ct) is { } conversation)
        {
            var lang = CandidateLanguages.Of(candidate.Language, candidate.PreferredScript) ?? CandidateLang.Ru;
            var where = RecruiterTexts.Where(lang, interview.Format, interview.Location, interview.MeetingUrl);
            var text = kind switch
            {
                "awaiting" => RecruiterTexts.ConfirmTime(lang, at, where),
                "rescheduled" => RecruiterTexts.Rescheduled(lang, at, where),
                "confirmed" => RecruiterTexts.Booked(lang, at, where),
                _ => RecruiterTexts.Cancelled(lang, at),
            };
            await outbox.EnqueueAsync(new OutboxRequest(conversation.AccountId, conversation.PeerId, conversation.Id, TelegramDepartments.Hr, "interview", text,
                $"interview:{interview.Id:N}:{kind}:{interview.StartsAt:yyyyMMddHHmm}", "system"), ct);
        }
    }

    public Task OnFailedAsync(RecruitmentJob job, string error, CancellationToken ct) => Task.CompletedTask;
}

/// <summary>
/// hr.interview.scheduling — «invite»: пригласить кандидата (варианты времени — ему в Telegram), «calendar»: восстановить событие календаря
/// действующего собеседования.
/// </summary>
public sealed class InterviewSchedulingJob(InterviewService interviews) : IRecruitmentJobHandler
{
    public string Queue => RecruitmentQueues.Scheduling;

    public async Task RunAsync(RecruitmentJob job, CancellationToken ct)
    {
        if (job.Key.StartsWith("calendar:", StringComparison.Ordinal))
        {
            await interviews.RepairCalendarAsync(job.RelatedId, ct);
            return;
        }

        await interviews.InviteAsync(job.RelatedId, null, ct);
    }

    public Task OnFailedAsync(RecruitmentJob job, string error, CancellationToken ct) => Task.CompletedTask;
}

/// <summary>
/// Сверка собеседований (раз в минуту): удержанное время, которое кандидат не подтвердил, — освобождается (собеседование «отменено системой», HR —
/// уведомление); у действующего собеседования пропало событие календаря — задание восстановить.
/// </summary>
public sealed class InterviewSweeper(IAppDbContext db, InterviewService interviews, RecruitmentJobQueue jobs, TimeProvider clock) : IRecruitmentSweeper
{
    public async Task<int> SweepAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var expired = await db.Interviews.AsNoTracking().Where(i => i.Status == InterviewStatus.Awaiting && i.HoldUntil < now).Select(i => i.Id).Take(50).ToListAsync(ct);
        foreach (var id in expired)
        {
            await interviews.CancelAsync(id, "system", "Кандидат не подтвердил время — время освобождено.", null, ct);
        }

        var missing = await db.Interviews.AsNoTracking()
            .Where(i => (i.Status == InterviewStatus.Confirmed || i.Status == InterviewStatus.Rescheduled) && i.EndsAt > now
                        && (i.CalendarEventId == null || !db.CalendarEvents.Any(e => e.Id == i.CalendarEventId)))
            .Select(i => i.Id).Take(50).ToListAsync(ct);
        var queued = 0;
        foreach (var id in missing)
        {
            if (await jobs.EnqueueAsync(RecruitmentQueues.Scheduling, $"calendar:{id:N}", id, ct: ct))
            {
                queued++;
            }
        }

        return expired.Count + queued;
    }
}

/// <summary>
/// После оценки AI под вакансию: у вакансии задан порог приглашения, оценка не ниже, собеседования ещё нет, диалог с кандидатом ведёт AI-рекрутер,
/// которому можно назначать собеседования, — задание пригласить (варианты времени из календаря). Ниже порога — кандидат не приглашается
/// автоматически (решает HR по оценке в карточке).
/// </summary>
public sealed class InterviewInviter(IAppDbContext db, InterviewService interviews, Agents.DialogAgentService agents, RecruitmentJobQueue jobs) : IInterviewInviter
{
    public async Task AfterScoringAsync(Guid applicationId, CancellationToken ct)
    {
        var application = await db.CandidateApplications.AsNoTracking().Include(a => a.Vacancy).FirstOrDefaultAsync(a => a.Id == applicationId, ct);
        if (application is null || application.ClosedAt is not null || application.Vacancy is null)
        {
            return;
        }

        var cfg = await interviews.ConfigAsync(application.VacancyId, ct);
        var score = await db.CandidateScores.AsNoTracking().Where(s => s.ApplicationId == applicationId).OrderByDescending(s => s.CreatedAt)
            .Select(s => (int?)s.Overall).FirstOrDefaultAsync(ct);
        if (cfg.InviteMinScore is not { } min || score is not { } value || value < min
            || await db.Interviews.AnyAsync(i => i.ApplicationId == applicationId && i.Status != InterviewStatus.Cancelled, ct))
        {
            return;
        }

        var conversation = await interviews.ConversationAsync(application.CandidateId, ct);
        if (conversation is not { Mode: TelegramConversationMode.Ai })
        {
            return;
        }

        var agent = await agents.RecruiterForAsync(application.Vacancy.AgentId, conversation.AccountId, ct);
        if (agent is { Enabled: true, CanScheduleInterviews: true })
        {
            await jobs.EnqueueAsync(RecruitmentQueues.Scheduling, $"invite:{applicationId:N}", applicationId, ct: ct);
        }
    }
}
