using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Recruitment.Telegram;
using OneBase.Application.Security;
using OneBase.Application.Telegram.Outbox;
using OneBase.Application.Telegram.Routing;
using OneBase.Application.Work;
using OneBase.Domain.Audit;
using OneBase.Domain.Recruitment;
using OneBase.Domain.Work;

namespace OneBase.Application.Recruitment;

public sealed record SlotView(DateTimeOffset StartsAt, DateTimeOffset EndsAt);

public sealed record InterviewersInput(IReadOnlyList<Guid> UserIds);

public sealed record BookInput(Guid ApplicationId, DateTimeOffset StartsAt, string Format, string? Location);

public sealed record InterviewView(Guid Id, Guid ApplicationId, Guid CandidateId, string CandidateName, Guid VacancyId, string VacancyCode, string VacancyTitle,
    DateTimeOffset? StartsAt, DateTimeOffset? EndsAt, string Format, string? Location, string Status, IReadOnlyList<string> Interviewers,
    IReadOnlyList<SlotView> ProposedSlots, DateTimeOffset? ConfirmedAt);

/// <summary>
/// Собеседования через календарь OneBase (второго календаря нет): интервьюеры вакансии, их занятость — их события календаря; свободное время —
/// рабочие часы и дни из настроек подбора (Ташкент); кандидату предлагаются варианты, выбранное время перед записью проверяется заново (запись —
/// по одной: OneBase — один экземпляр backend). Подтверждённое собеседование — событие календаря с интервьюерами; напоминание кандидату — в
/// Telegram за сутки.
/// </summary>
public sealed class InterviewService(IAppDbContext db, UserNotifier notifier, RecruitmentExceptions exceptions, TelegramOutbox outbox, IAuditLogger audit, TimeProvider clock)
{
    private static readonly SemaphoreSlim Booking = new(1, 1);
    private static readonly TimeSpan Tashkent = TimeSpan.FromHours(5);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static readonly string[] Formats = ["office", "online", "phone"];

    /// <summary>Не предлагать время раньше, чем через столько от «сейчас».</summary>
    public static readonly TimeSpan Lead = TimeSpan.FromHours(2);

    private async Task<RecruitmentSettings> SettingsAsync(CancellationToken ct) =>
        await db.RecruitmentSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1, ct) ?? new RecruitmentSettings();

    public async Task<IReadOnlyList<Guid>> InterviewersAsync(Guid vacancyId, CancellationToken ct)
    {
        var users = await db.VacancyInterviewers.AsNoTracking().Where(i => i.VacancyId == vacancyId).Select(i => i.UserId).ToListAsync(ct);
        if (users.Count > 0)
        {
            return users;
        }

        var recruiter = await db.Vacancies.AsNoTracking().Where(v => v.Id == vacancyId).Select(v => v.RecruiterId).FirstOrDefaultAsync(ct);
        return recruiter is { } r ? [r] : [];
    }

    public async Task<IReadOnlyList<Guid>> SetInterviewersAsync(RecruitmentContext ctx, Guid vacancyId, InterviewersInput input, CancellationToken ct)
    {
        if (!ctx.CanManage)
        {
            throw new FieldForbiddenException("Интервьюеров назначает HR.");
        }

        if (!await db.Vacancies.AnyAsync(v => v.Id == vacancyId, ct))
        {
            throw new FieldNotFoundException("Вакансия не найдена.");
        }

        var ids = input.UserIds.Distinct().ToList();
        if (await db.Users.CountAsync(u => ids.Contains(u.Id) && u.IsActive, ct) != ids.Count)
        {
            throw new FieldValidationException("Интервьюеры — действующие пользователи OneBase.");
        }

        await db.VacancyInterviewers.Where(i => i.VacancyId == vacancyId).ExecuteDeleteAsync(ct);
        db.VacancyInterviewers.AddRange(ids.Select(u => new VacancyInterviewer { VacancyId = vacancyId, UserId = u }));
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "recruitment.vacancy.interviewers", nameof(Vacancy), vacancyId.ToString(), new { count = ids.Count }, ct);
        return ids;
    }

    /// <summary>Занят ли кто-то из интервьюеров в [from, to): события календаря, где он автор или участник (событие на весь день — весь день).</summary>
    private async Task<bool> BusyAsync(IReadOnlyList<Guid> users, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        if (users.Count == 0)
        {
            return false;
        }

        var dayFrom = new DateTimeOffset((from + Tashkent).Date, TimeSpan.Zero) - Tashkent;
        var events = await db.CalendarEvents.AsNoTracking()
            .Where(e => e.StartsAt < to && (e.EndsAt ?? e.StartsAt.AddHours(1)) > dayFrom
                        && ((e.CreatedById != null && users.Contains(e.CreatedById.Value)) || e.Participants.Any(p => users.Contains(p.UserId))))
            .Select(e => new { e.StartsAt, e.EndsAt, e.AllDay }).ToListAsync(ct);
        return events.Any(e => e.AllDay
            ? (e.StartsAt + Tashkent).Date <= (from + Tashkent).Date && ((e.EndsAt ?? e.StartsAt) + Tashkent).Date >= (from + Tashkent).Date
            : e.StartsAt < to && (e.EndsAt ?? e.StartsAt.AddHours(1)) > from);
    }

    /// <summary>Свободные слоты интервьюеров вакансии на ближайшие дни: не больше одного в день (чтобы кандидат выбирал из разных дней), до count.</summary>
    public async Task<IReadOnlyList<SlotView>> FreeSlotsAsync(Guid vacancyId, int count, int days, CancellationToken ct)
    {
        var settings = await SettingsAsync(ct);
        var users = await InterviewersAsync(vacancyId, ct);
        var workDays = settings.InterviewDays.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(d => int.TryParse(d, out var n) ? n : 0).Where(n => n is >= 1 and <= 7).ToHashSet();
        var length = TimeSpan.FromMinutes(Math.Clamp(settings.InterviewSlotMinutes, 15, 240));
        var earliest = clock.GetUtcNow() + Lead;
        var result = new List<SlotView>();
        var today = (clock.GetUtcNow() + Tashkent).Date;
        for (var d = 0; d < days && result.Count < count; d++)
        {
            var date = today.AddDays(d);
            var iso = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
            if (!workDays.Contains(iso))
            {
                continue;
            }

            for (var start = date + settings.InterviewDayStart.ToTimeSpan(); start + length <= date + settings.InterviewDayEnd.ToTimeSpan(); start += length)
            {
                var from = new DateTimeOffset(start, TimeSpan.Zero) - Tashkent;
                if (from < earliest || await BusyAsync(users, from, from + length, ct))
                {
                    continue;
                }

                result.Add(new SlotView(from, from + length));
                break;
            }
        }

        return result;
    }

    private async Task<CandidateApplication> ApplicationAsync(Guid applicationId, CancellationToken ct) =>
        await db.CandidateApplications.Include(a => a.Vacancy).Include(a => a.Candidate).FirstOrDefaultAsync(a => a.Id == applicationId, ct)
        ?? throw new FieldNotFoundException("Отклик не найден.");

    /// <summary>Предложить кандидату варианты (AI-рекрутер или HR): собеседование «предложено» со слотами. Слотов нет — исключение «расписание».</summary>
    public async Task<(Interview Interview, IReadOnlyList<SlotView> Slots)> ProposeAsync(Guid applicationId, Guid? actorId, CancellationToken ct)
    {
        var application = await ApplicationAsync(applicationId, ct);
        var slots = await FreeSlotsAsync(application.VacancyId, 3, 10, ct);
        var interview = await db.Interviews.FirstOrDefaultAsync(i => i.ApplicationId == applicationId && i.Status == InterviewStatus.Proposed, ct);
        if (interview is null)
        {
            interview = new Interview { ApplicationId = applicationId, CandidateId = application.CandidateId, VacancyId = application.VacancyId, Format = "office", CreatedById = actorId };
            db.Interviews.Add(interview);
        }

        interview.ProposedSlotsJson = JsonSerializer.Serialize(slots, Json);
        await db.SaveChangesAsync(ct);
        if (slots.Count == 0)
        {
            await exceptions.RaiseAsync("schedule", $"Нет свободного времени у интервьюеров вакансии {application.Vacancy?.Code} на ближайшие 10 дней.",
                application.CandidateId, application.VacancyId, dedupKey: $"schedule:{applicationId:N}", ct: ct);
        }

        return (interview, slots);
    }

    public static IReadOnlyList<SlotView> Slots(Interview interview) =>
        string.IsNullOrEmpty(interview.ProposedSlotsJson) ? [] : JsonSerializer.Deserialize<List<SlotView>>(interview.ProposedSlotsJson, Json) ?? [];

    /// <summary>Варианты для кандидата текстом: «1) пн 10.11 в 10:00» (время Ташкента).</summary>
    public static string SlotsText(IReadOnlyList<SlotView> slots, bool uzbek)
    {
        var culture = CultureInfo.GetCultureInfo(uzbek ? "uz-Latn-UZ" : "ru-RU");
        return string.Join('\n', slots.Select((s, i) => $"{i + 1}) {(s.StartsAt + Tashkent).ToString("ddd dd.MM, HH:mm", culture)}"));
    }

    /// <summary>
    /// Записать на время (выбор кандидата или HR): повторная проверка занятости под замком; занято — конфликт. Событие календаря с интервьюерами,
    /// этап отклика — «собеседование», уведомления интервьюерам и HR.
    /// </summary>
    public async Task<Interview> BookAsync(Guid applicationId, DateTimeOffset startsAt, string format, string? location, Guid? actorId, CancellationToken ct)
    {
        if (!Formats.Contains(format))
        {
            throw new FieldValidationException("Формат: в офисе, онлайн или по телефону.");
        }

        var application = await ApplicationAsync(applicationId, ct);
        if (RecruitmentRules.IsClosed(application.Stage))
        {
            throw new FieldValidationException("Отклик закрыт — собеседование не назначается.");
        }

        var settings = await SettingsAsync(ct);
        var length = TimeSpan.FromMinutes(Math.Clamp(settings.InterviewSlotMinutes, 15, 240));
        var users = await InterviewersAsync(application.VacancyId, ct);
        await Booking.WaitAsync(ct);
        try
        {
            if (startsAt < clock.GetUtcNow())
            {
                throw new FieldValidationException("Это время уже прошло.");
            }

            if (await BusyAsync(users, startsAt, startsAt + length, ct))
            {
                throw new FieldConflictException("Это время у интервьюера уже занято — выберите другое.");
            }

            var now = clock.GetUtcNow();
            var calendar = new CalendarEvent
            {
                Title = $"Собеседование: {application.Candidate?.FullName} — {application.Vacancy?.Title} ({application.Vacancy?.Code})",
                Description = "Подбор персонала (HR → «Подбор»).",
                Kind = CalendarEventKind.Meeting,
                StartsAt = startsAt,
                EndsAt = startsAt + length,
                Location = location?.Trim() is { Length: > 0 } l ? (l.Length <= 300 ? l : l[..300]) : null,
                DepartmentId = application.Vacancy?.DepartmentId,
                CreatedById = actorId ?? application.Vacancy?.RecruiterId ?? users.FirstOrDefault(),
            };
            foreach (var user in users)
            {
                calendar.Participants.Add(new CalendarEventParticipant { EventId = calendar.Id, UserId = user });
            }

            db.CalendarEvents.Add(calendar);
            var interview = await db.Interviews.FirstOrDefaultAsync(i => i.ApplicationId == applicationId && i.Status == InterviewStatus.Proposed, ct);
            if (interview is null)
            {
                interview = new Interview { ApplicationId = applicationId, CandidateId = application.CandidateId, VacancyId = application.VacancyId, Format = format, CreatedById = actorId };
                db.Interviews.Add(interview);
            }

            interview.StartsAt = startsAt;
            interview.EndsAt = startsAt + length;
            interview.Format = format;
            interview.Location = calendar.Location;
            interview.Status = InterviewStatus.Confirmed;
            interview.ConfirmedAt = now;
            interview.CalendarEventId = calendar.Id;
            if (application.Stage is ApplicationStage.New or ApplicationStage.Screening)
            {
                db.ApplicationEvents.Add(new ApplicationEvent { ApplicationId = application.Id, FromStage = application.Stage, ToStage = ApplicationStage.Interview, ActorId = actorId, At = now, Comment = "Назначено собеседование" });
                application.Stage = ApplicationStage.Interview;
                application.StageChangedAt = now;
            }

            var when = (startsAt + Tashkent).ToString("dd.MM HH:mm", CultureInfo.InvariantCulture);
            foreach (var user in users)
            {
                notifier.Notify(user, UserNotificationKind.CalendarInvite, $"Собеседование {when}: {application.Candidate?.FullName}",
                    $"{application.Vacancy?.Title} ({application.Vacancy?.Code})", "/calendar");
            }

            foreach (var user in (await db.ActiveUsersWithAsync(Permissions.HrRecruitmentManage, ct)).Except(users))
            {
                notifier.Notify(user, UserNotificationKind.RecruitmentAlert, $"Собеседование назначено: {application.Candidate?.FullName}, {when}",
                    $"{application.Vacancy?.Title} ({application.Vacancy?.Code})", "/hr/recruitment/interviews");
            }

            await db.SaveChangesAsync(ct);
            await audit.LogAsync(actorId is null ? ActorType.Agent : ActorType.User, actorId?.ToString() ?? "recruiter", "recruitment.interview.booked",
                nameof(Interview), interview.Id.ToString(), new { application = applicationId, startsAt, format }, ct);
            return interview;
        }
        finally
        {
            Booking.Release();
        }
    }

    public async Task<IReadOnlyList<InterviewView>> ListAsync(RecruitmentContext ctx, bool upcoming, CancellationToken ct)
    {
        if (!ctx.SeesCandidates)
        {
            throw new FieldForbiddenException("Нет доступа к подбору.");
        }

        var now = clock.GetUtcNow();
        var query = db.Interviews.AsNoTracking();
        query = upcoming
            ? query.Where(i => i.Status == InterviewStatus.Proposed || (i.Status == InterviewStatus.Confirmed && i.EndsAt >= now))
            : query.Where(i => i.Status != InterviewStatus.Proposed);
        var rows = await query.OrderBy(i => i.StartsAt).Take(300).ToListAsync(ct);
        return await ViewsAsync(rows, ct);
    }

    public async Task<IReadOnlyList<InterviewView>> ForCandidateAsync(RecruitmentContext ctx, Guid candidateId, CancellationToken ct)
    {
        if (!ctx.SeesCandidates)
        {
            throw new FieldForbiddenException("Нет доступа к подбору.");
        }

        return await ViewsAsync(await db.Interviews.AsNoTracking().Where(i => i.CandidateId == candidateId).OrderByDescending(i => i.CreatedAt).ToListAsync(ct), ct);
    }

    private async Task<IReadOnlyList<InterviewView>> ViewsAsync(List<Interview> rows, CancellationToken ct)
    {
        var candidateIds = rows.Select(r => r.CandidateId).Distinct().ToList();
        var names = await db.Candidates.AsNoTracking().Where(c => candidateIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.FullName, ct);
        var vacancyIds = rows.Select(r => r.VacancyId).Distinct().ToList();
        var vacancies = await db.Vacancies.AsNoTracking().Where(v => vacancyIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, ct);
        var eventIds = rows.Where(r => r.CalendarEventId != null).Select(r => r.CalendarEventId!.Value).ToList();
        var participants = await db.CalendarEvents.AsNoTracking().Where(e => eventIds.Contains(e.Id))
            .SelectMany(e => e.Participants.Select(p => new { e.Id, p.User!.FullName })).ToListAsync(ct);
        return rows.Select(i => new InterviewView(i.Id, i.ApplicationId, i.CandidateId, names.GetValueOrDefault(i.CandidateId) ?? "—", i.VacancyId,
            vacancies.TryGetValue(i.VacancyId, out var v) ? v.Code : "—", vacancies.TryGetValue(i.VacancyId, out var v2) ? v2.Title : "—", i.StartsAt, i.EndsAt,
            i.Format, i.Location, i.Status.ToString(), participants.Where(p => p.Id == i.CalendarEventId).Select(p => p.FullName).ToList(), Slots(i), i.ConfirmedAt)).ToList();
    }

    /// <summary>HR назначает время вручную (из свободных или любое свободное).</summary>
    public async Task<IReadOnlyList<InterviewView>> BookByHrAsync(RecruitmentContext ctx, BookInput input, CancellationToken ct)
    {
        if (!ctx.CanManage)
        {
            throw new FieldForbiddenException("Собеседование назначает HR.");
        }

        var interview = await BookAsync(input.ApplicationId, input.StartsAt, input.Format, input.Location, ctx.UserId, ct);
        return await ForCandidateAsync(ctx, interview.CandidateId, ct);
    }

    /// <summary>Отменить, отметить «прошло» или «не пришёл». Отмена убирает событие из календаря.</summary>
    public async Task<IReadOnlyList<InterviewView>> SetStatusAsync(RecruitmentContext ctx, Guid id, InterviewStatus status, CancellationToken ct)
    {
        if (!ctx.CanManage)
        {
            throw new FieldForbiddenException("Собеседования меняет HR.");
        }

        if (status is InterviewStatus.Proposed or InterviewStatus.Confirmed)
        {
            throw new FieldValidationException("Статус: прошло, не пришёл или отменено.");
        }

        var interview = await db.Interviews.FirstOrDefaultAsync(i => i.Id == id, ct) ?? throw new FieldNotFoundException("Собеседование не найдено.");
        interview.Status = status;
        interview.UpdatedAt = clock.GetUtcNow();
        if (status == InterviewStatus.Cancelled && interview.CalendarEventId is { } e)
        {
            await db.CalendarEvents.Where(x => x.Id == e).ExecuteDeleteAsync(ct);
            interview.CalendarEventId = null;
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "recruitment.interview.status", nameof(Interview), id.ToString(), new { status = status.ToString() }, ct);
        return await ForCandidateAsync(ctx, interview.CandidateId, ct);
    }

    /// <summary>
    /// Напоминание кандидату в Telegram за сутки до собеседования (одно на собеседование): если с ним есть диалог HR. Возвращает число
    /// поставленных в очередь.
    /// </summary>
    public async Task<int> RemindAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var due = await db.Interviews.Where(i => i.Status == InterviewStatus.Confirmed && i.RemindedAt == null && i.StartsAt > now && i.StartsAt <= now.AddHours(24))
            .ToListAsync(ct);
        var queued = 0;
        foreach (var interview in due)
        {
            var conversation = await db.TelegramConversations.AsNoTracking()
                .Where(c => c.Department == TelegramDepartments.Hr && c.RelatedType == RecruitmentService.CandidateRelation && c.RelatedId == interview.CandidateId)
                .OrderByDescending(c => c.LastMessageAt).FirstOrDefaultAsync(ct);
            interview.RemindedAt = now;
            if (conversation is null)
            {
                continue;
            }

            var uzbek = await db.Candidates.AsNoTracking().Where(c => c.Id == interview.CandidateId).Select(c => c.Language).FirstOrDefaultAsync(ct) == "uz";
            var at = (interview.StartsAt!.Value + Tashkent).ToString("dd.MM HH:mm", CultureInfo.InvariantCulture);
            var text = uzbek
                ? $"Eslatma: suhbat {at} da (Toshkent vaqti){(interview.Location is { } l ? $", manzil: {l}" : string.Empty)}. Vaqtni o'zgartirish kerak bo'lsa, shu yerga yozing."
                : $"Напоминаем: собеседование {at} (время Ташкента){(interview.Location is { } l2 ? $", адрес: {l2}" : string.Empty)}. Если нужно перенести — напишите сюда.";
            await outbox.EnqueueAsync(new OutboxRequest(conversation.AccountId, conversation.PeerId, conversation.Id, TelegramDepartments.Hr, "reminder", text,
                $"reminder:{interview.Id:N}", "system"), ct);
            queued++;
        }

        await db.SaveChangesAsync(ct);
        return queued;
    }
}
