using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Recruitment.Jobs;
using OneBase.Application.Telegram.Outbox;
using OneBase.Application.Telegram.Routing;
using OneBase.Domain.Audit;
using OneBase.Domain.Recruitment;
using OneBase.Domain.Work;

namespace OneBase.Application.Recruitment;

public sealed record SlotView(DateTimeOffset StartsAt, DateTimeOffset EndsAt);

/// <summary>
/// Пожелание ко времени собеседования (кандидат в переписке — разбирает модель; HR — выбранный день): день или дни (Date…DateTo, пояс
/// собеседования), точное время (Time) или промежуток (From–To). Пустое поле — без условия.
/// </summary>
public sealed record SlotWish(DateOnly? Date = null, DateOnly? DateTo = null, TimeOnly? Time = null, TimeOnly? From = null, TimeOnly? To = null)
{
    public bool IsEmpty => Date is null && DateTo is null && Time is null && From is null && To is null;
}

/// <summary>Варианты по пожеланию: Exact — первым идёт ровно то время, что просили, и оно свободно.</summary>
public sealed record WishSlots(IReadOnlyList<SlotView> Slots, bool Exact);

/// <summary>
/// Отклик, на который HR может назначить собеседование (окно «Новое собеседование» в «Собеседованиях» и в календаре): кандидат, телефон,
/// вакансия, этап и действующее собеседование, если оно уже есть (тогда — перенос, а не новое).
/// </summary>
public sealed record BookableView(Guid ApplicationId, Guid CandidateId, string CandidateName, string? Phone, Guid VacancyId, string VacancyCode, string VacancyTitle,
    string Stage, Guid? InterviewId, string? InterviewStatus, DateTimeOffset? InterviewStartsAt, bool HasTelegram);

public sealed record InterviewersInput(IReadOnlyList<Guid> UserIds);

/// <summary>
/// HR назначает время: ConfirmedByCandidate — кандидат уже подтвердил (по телефону, лично) — сразу «подтверждено» и событие календаря; иначе —
/// «ждёт подтверждения кандидата»: время удерживается, кандидату — вопрос «вам удобно?».
/// </summary>
public sealed record BookInput(Guid ApplicationId, DateTimeOffset StartsAt, string? Format, string? Location, bool ConfirmedByCandidate = false, string? MeetingUrl = null);

public sealed record RescheduleInput(DateTimeOffset StartsAt);

public sealed record CancelInput(string? Reason);

public sealed record FeedbackInput(InterviewResult Result, int? Technical, int? Communication, int? Professional, string? Strengths, string? Weaknesses, string? Comments,
    string? Recommendation, ApplicationStage? NextStage);

public sealed record FeedbackView(Guid Id, Guid AuthorId, string Author, string Result, int? Technical, int? Communication, int? Professional, string? Strengths,
    string? Weaknesses, string? Comments, string? Recommendation, string? NextStage, DateTimeOffset At);

/// <summary>Настройки собеседования вакансии (null — общие настройки подбора) и что действует на самом деле (Effective…).</summary>
public sealed record InterviewSettingsView(Guid VacancyId, Guid? InterviewerId, IReadOnlyList<Guid> Participants, int? DurationMinutes, string? Days, string? DayStart,
    string? DayEnd, string? Breaks, int? MinNoticeHours, int? MaxAdvanceDays, string? Format, string? Address, string? MeetingUrl, string? TimeZone, int? InviteMinScore,
    string? Instructions, Guid? EffectiveInterviewerId, int EffectiveDuration, string EffectiveDays, string EffectiveDayStart, string EffectiveDayEnd, int EffectiveNotice,
    int EffectiveHorizon, string EffectiveFormat, string EffectiveTimeZone);

public sealed record InterviewSettingsInput(Guid? InterviewerId, IReadOnlyList<Guid>? Participants, int? DurationMinutes, string? Days, string? DayStart, string? DayEnd,
    string? Breaks, int? MinNoticeHours, int? MaxAdvanceDays, string? Format, string? Address, string? MeetingUrl, string? TimeZone, int? InviteMinScore, string? Instructions);

/// <summary>
/// Собеседование для HR: кандидат, резюме, вакансия, отдел, интервьюер и участники, время (часовой пояс), формат, место или ссылка, статус,
/// подтверждение кандидатом, удержание времени, прежнее время, отмена, оценка и сводка AI, событие календаря, итоги.
/// </summary>
public sealed record InterviewView(Guid Id, Guid ApplicationId, Guid CandidateId, string CandidateName, Guid VacancyId, string VacancyCode, string VacancyTitle,
    DateTimeOffset? StartsAt, DateTimeOffset? EndsAt, string Format, string? Location, string Status, IReadOnlyList<string> Interviewers,
    IReadOnlyList<SlotView> ProposedSlots, DateTimeOffset? ConfirmedAt, string CandidateConfirmation = "Pending", string? Interviewer = null, string? Department = null,
    string TimeZone = InterviewService.DefaultZone, string? MeetingUrl = null, DateTimeOffset? HoldUntil = null, DateTimeOffset? PreviousStartsAt = null,
    string? CancelledBy = null, string? CancelReason = null, Guid? ResumeId = null, int? AiScore = null, string? AiSummary = null, Guid? CalendarEventId = null,
    IReadOnlyList<FeedbackView>? Feedback = null);

/// <summary>Что действует для собеседований вакансии: ответственный, участники, длительность, дни, часы, перерывы, сроки, формат, место, пояс.</summary>
public sealed record InterviewConfig(Guid VacancyId, Guid? InterviewerId, IReadOnlyList<Guid> Users, int Minutes, IReadOnlySet<int> Days, TimeOnly DayStart, TimeOnly DayEnd,
    IReadOnlyList<(TimeOnly From, TimeOnly To)> Breaks, int NoticeHours, int HorizonDays, string Format, string? Address, string? MeetingUrl, string TimeZoneId,
    int? InviteMinScore, string? Instructions)
{
    public TimeZoneInfo Zone => InterviewService.Zone(TimeZoneId);
    public TimeSpan Length => TimeSpan.FromMinutes(Minutes);
}

/// <summary>
/// Собеседования — одна запись о собеседовании (Interview) и её отражение в календаре OneBase (событие, одно на собеседование; второго календаря нет).
/// <list type="bullet">
/// <item>Свободное время — по календарю ответственного интервьюера и участников (события, где они автор или участник, и удержанное время других
/// кандидатов), в рабочие дни и часы вакансии, без перерывов, не раньше минимального срока и не дальше горизонта (часовой пояс вакансии).</item>
/// <item>Запись — в транзакции под блокировкой PostgreSQL на каждого участника: одновременный выбор одного времени двумя кандидатами — одна
/// запись, второму — «занято». Событие календаря создаётся в той же транзакции: или оба, или ничего.</item>
/// <item>Предложенное время — не собеседование. Подтверждённое — только после выбора и «да» кандидата (или HR отметил, что кандидат подтвердил).</item>
/// <item>Перенос и отмена — в собеседовании и в календаре вместе; уведомления — задание hr.interview.notifications (интервьюеру, участникам, HR,
/// руководителю отдела вакансии; кандидату — если изменил HR).</item>
/// </list>
/// </summary>
public sealed class InterviewService(IAppDbContext db, RecruitmentExceptions exceptions, TelegramOutbox outbox, RecruitmentJobQueue jobs, IAuditLogger audit,
    TimeProvider clock)
{
    public const string DefaultZone = "Asia/Tashkent";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly TimeZoneInfo Tashkent = TimeZoneInfo.CreateCustomTimeZone(DefaultZone, TimeSpan.FromHours(5), DefaultZone, DefaultZone);
    private static readonly InterviewStatus[] Active = [InterviewStatus.Awaiting, InterviewStatus.Confirmed, InterviewStatus.Rescheduled];
    public static readonly string[] Formats = ["office", "online", "phone"];

    /// <summary>Сколько кандидат думает над временем, которое предложил HR: потом время освобождается.</summary>
    public static readonly TimeSpan Hold = TimeSpan.FromHours(24);

    /// <summary>Минимальный срок записи по умолчанию.</summary>
    public static readonly TimeSpan Lead = TimeSpan.FromHours(2);

    public static TimeZoneInfo Zone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || id == DefaultZone)
        {
            return Tashkent;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return Tashkent;
        }
    }

    public static DateTimeOffset Local(DateTimeOffset utc, string? zone) => TimeZoneInfo.ConvertTime(utc, Zone(zone));

    public static bool IsActive(Interview i) => Active.Contains(i.Status);

    private async Task<RecruitmentSettings> SettingsAsync(CancellationToken ct) =>
        await db.RecruitmentSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1, ct) ?? new RecruitmentSettings();

    // ---------- Настройки собеседования ----------

    public async Task<IReadOnlyList<Guid>> InterviewersAsync(Guid vacancyId, CancellationToken ct) => (await ConfigAsync(vacancyId, ct)).Users;

    public async Task<IReadOnlyList<Guid>> SetInterviewersAsync(RecruitmentContext ctx, Guid vacancyId, InterviewersInput input, CancellationToken ct)
    {
        Manage(ctx);
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
        await SaveAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "recruitment.vacancy.interviewers", nameof(Vacancy), vacancyId.ToString(), new { count = ids.Count }, ct);
        return ids;
    }

    /// <summary>
    /// Что действует: настройки вакансии, иначе общие настройки подбора. Ответственный — выбранный у вакансии, иначе первый интервьюер, иначе
    /// рекрутер вакансии; участники — ответственный и интервьюеры вакансии.
    /// </summary>
    public async Task<InterviewConfig> ConfigAsync(Guid vacancyId, CancellationToken ct)
    {
        var settings = await SettingsAsync(ct);
        var own = await db.VacancyInterviewSettings.AsNoTracking().FirstOrDefaultAsync(s => s.VacancyId == vacancyId, ct);
        var interviewers = await db.VacancyInterviewers.AsNoTracking().Where(i => i.VacancyId == vacancyId).Select(i => i.UserId).ToListAsync(ct);
        var recruiter = await db.Vacancies.AsNoTracking().Where(v => v.Id == vacancyId).Select(v => v.RecruiterId).FirstOrDefaultAsync(ct);
        var responsible = own?.InterviewerId ?? (interviewers.Count > 0 ? interviewers[0] : recruiter);
        var users = new List<Guid>();
        if (responsible is { } r)
        {
            users.Add(r);
        }

        users.AddRange(interviewers.Where(u => !users.Contains(u)));
        return new InterviewConfig(vacancyId, responsible, users, Math.Clamp(own?.DurationMinutes ?? settings.InterviewSlotMinutes, 15, 240),
            ParseDays(own?.Days ?? settings.InterviewDays), own?.DayStart ?? settings.InterviewDayStart, own?.DayEnd ?? settings.InterviewDayEnd, ParseBreaks(own?.Breaks),
            Math.Clamp(own?.MinNoticeHours ?? (int)Lead.TotalHours, 0, 168), Math.Clamp(own?.MaxAdvanceDays ?? 14, 1, 90), own?.Format ?? "office", own?.Address,
            own?.MeetingUrl, own?.TimeZone ?? DefaultZone, own?.InviteMinScore, own?.Instructions);
    }

    public async Task<InterviewSettingsView> SettingsViewAsync(RecruitmentContext ctx, Guid vacancyId, CancellationToken ct)
    {
        Read(ctx);
        var own = await db.VacancyInterviewSettings.AsNoTracking().FirstOrDefaultAsync(s => s.VacancyId == vacancyId, ct);
        var cfg = await ConfigAsync(vacancyId, ct);
        var participants = await db.VacancyInterviewers.AsNoTracking().Where(i => i.VacancyId == vacancyId).Select(i => i.UserId).ToListAsync(ct);
        static string? T(TimeOnly? t) => t?.ToString("HH:mm", CultureInfo.InvariantCulture);
        return new InterviewSettingsView(vacancyId, own?.InterviewerId, participants, own?.DurationMinutes, own?.Days, T(own?.DayStart), T(own?.DayEnd), own?.Breaks,
            own?.MinNoticeHours, own?.MaxAdvanceDays, own?.Format, own?.Address, own?.MeetingUrl, own?.TimeZone, own?.InviteMinScore, own?.Instructions,
            cfg.InterviewerId, cfg.Minutes, string.Join(',', cfg.Days.Order()), T(cfg.DayStart)!, T(cfg.DayEnd)!, cfg.NoticeHours, cfg.HorizonDays, cfg.Format, cfg.TimeZoneId);
    }

    public async Task<InterviewSettingsView> SaveSettingsAsync(RecruitmentContext ctx, Guid vacancyId, InterviewSettingsInput input, CancellationToken ct)
    {
        Manage(ctx);
        if (!await db.Vacancies.AnyAsync(v => v.Id == vacancyId, ct))
        {
            throw new FieldNotFoundException("Вакансия не найдена.");
        }

        var users = (input.Participants ?? []).Concat(input.InterviewerId is { } i ? [i] : []).Distinct().ToList();
        if (await db.Users.CountAsync(u => users.Contains(u.Id) && u.IsActive, ct) != users.Count)
        {
            throw new FieldValidationException("Интервьюер и участники — действующие пользователи OneBase.");
        }

        static TimeOnly? Time(string? s, string what) => string.IsNullOrWhiteSpace(s) ? null
            : TimeOnly.TryParse(s, CultureInfo.InvariantCulture, out var t) ? t : throw new FieldValidationException($"{what}: время в формате 10:00.");
        var start = Time(input.DayStart, "Начало дня");
        var end = Time(input.DayEnd, "Конец дня");
        if (start is { } a && end is { } b && b <= a)
        {
            throw new FieldValidationException("Рабочие часы собеседований: начало раньше конца.");
        }

        if (input.DurationMinutes is < 15 or > 240)
        {
            throw new FieldValidationException("Длительность собеседования — 15–240 минут.");
        }

        if (input.Format is { Length: > 0 } f && !Formats.Contains(f))
        {
            throw new FieldValidationException("Формат: в офисе, онлайн или по телефону.");
        }

        if (input.InviteMinScore is < 0 or > 100)
        {
            throw new FieldValidationException("Порог оценки — от 0 до 100.");
        }

        if (input.MeetingUrl is { Length: > 0 } url && !(Uri.TryCreate(url.Trim(), UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps))
        {
            throw new FieldValidationException("Ссылка на онлайн-встречу — полный адрес https://…");
        }

        if (input.TimeZone is { Length: > 0 } tz && tz != DefaultZone && Zone(tz) == Tashkent)
        {
            throw new FieldValidationException("Неизвестный часовой пояс: укажите как Asia/Tashkent.");
        }

        if (!string.IsNullOrWhiteSpace(input.Breaks) && ParseBreaks(input.Breaks).Count == 0)
        {
            throw new FieldValidationException("Перерывы: «13:00-14:00», несколько — через запятую.");
        }

        var days = string.IsNullOrWhiteSpace(input.Days) ? null : string.Join(',', ParseDays(input.Days).Order());
        if (input.Days is { Length: > 0 } && string.IsNullOrEmpty(days))
        {
            throw new FieldValidationException("Дни недели: 1 — пн … 7 — вс, через запятую.");
        }

        var row = await db.VacancyInterviewSettings.FirstOrDefaultAsync(s => s.VacancyId == vacancyId, ct);
        if (row is null)
        {
            row = new VacancyInterviewSettings { VacancyId = vacancyId };
            db.VacancyInterviewSettings.Add(row);
        }

        row.InterviewerId = input.InterviewerId;
        row.DurationMinutes = input.DurationMinutes;
        row.Days = days;
        (row.DayStart, row.DayEnd) = (start, end);
        row.Breaks = Clean(input.Breaks, 200);
        row.MinNoticeHours = input.MinNoticeHours is { } n ? Math.Clamp(n, 0, 168) : null;
        row.MaxAdvanceDays = input.MaxAdvanceDays is { } h ? Math.Clamp(h, 1, 90) : null;
        row.Format = Clean(input.Format, 16);
        row.Address = Clean(input.Address, 300);
        row.MeetingUrl = Clean(input.MeetingUrl, 500);
        row.TimeZone = Clean(input.TimeZone, 40);
        row.InviteMinScore = input.InviteMinScore;
        row.Instructions = Clean(input.Instructions, 1000);
        row.UpdatedAt = clock.GetUtcNow();
        row.UpdatedById = ctx.UserId;
        if (input.Participants is not null)
        {
            await db.VacancyInterviewers.Where(x => x.VacancyId == vacancyId).ExecuteDeleteAsync(ct);
            db.VacancyInterviewers.AddRange(input.Participants.Distinct().Select(p => new VacancyInterviewer { VacancyId = vacancyId, UserId = p }));
        }

        await SaveAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "recruitment.vacancy.interview_settings", nameof(Vacancy), vacancyId.ToString(),
            new { input.DurationMinutes, input.Format, input.InviteMinScore, participants = input.Participants?.Count }, ct);
        return await SettingsViewAsync(ctx, vacancyId, ct);
    }

    private static HashSet<int> ParseDays(string? days) =>
        (days ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(d => int.TryParse(d, out var n) ? n : 0).Where(n => n is >= 1 and <= 7).ToHashSet();

    private static List<(TimeOnly From, TimeOnly To)> ParseBreaks(string? breaks)
    {
        var result = new List<(TimeOnly, TimeOnly)>();
        foreach (var part in (breaks ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var range = part.Split(['-', '–'], StringSplitOptions.TrimEntries);
            if (range.Length == 2 && TimeOnly.TryParse(range[0], CultureInfo.InvariantCulture, out var from) && TimeOnly.TryParse(range[1], CultureInfo.InvariantCulture, out var to)
                && to > from)
            {
                result.Add((from, to));
            }
        }

        return result;
    }

    // ---------- Свободное время ----------

    private static long LockKey(Guid user) => BitConverter.ToInt64(user.ToByteArray(), 0);

    /// <summary>
    /// Занятость участников около [from, to): события календаря (автор или участник; весь день — весь день в поясе собеседования) и время, удержанное
    /// за другими кандидатами (ждут подтверждения). Своё событие и своё удержание — не занятость (перенос).
    /// </summary>
    private async Task<List<(DateTimeOffset From, DateTimeOffset To)>> BusyAsync(IReadOnlyList<Guid> users, DateTimeOffset from, DateTimeOffset to, TimeZoneInfo zone,
        Guid? exceptInterview, CancellationToken ct)
    {
        var busy = new List<(DateTimeOffset, DateTimeOffset)>();
        if (users.Count == 0)
        {
            return busy;
        }

        (from, to) = (from.ToUniversalTime(), to.ToUniversalTime()); // PostgreSQL принимает только UTC

        var exceptEvent = exceptInterview is { } own
            ? await db.Interviews.AsNoTracking().Where(i => i.Id == own).Select(i => i.CalendarEventId).FirstOrDefaultAsync(ct)
            : null;
        var lower = from.AddDays(-1);
        var events = await db.CalendarEvents.AsNoTracking()
            .Where(e => e.StartsAt < to && (e.EndsAt ?? e.StartsAt.AddHours(1)) > lower && e.Id != exceptEvent
                        && ((e.CreatedById != null && users.Contains(e.CreatedById.Value)) || e.Participants.Any(p => users.Contains(p.UserId))))
            .Select(e => new { e.StartsAt, e.EndsAt, e.AllDay }).ToListAsync(ct);
        foreach (var e in events)
        {
            if (e.AllDay)
            {
                var first = TimeZoneInfo.ConvertTime(e.StartsAt, zone).Date;
                var last = TimeZoneInfo.ConvertTime(e.EndsAt ?? e.StartsAt, zone).Date.AddDays(1);
                busy.Add((new DateTimeOffset(first, zone.GetUtcOffset(first)), new DateTimeOffset(last, zone.GetUtcOffset(last))));
            }
            else
            {
                busy.Add((e.StartsAt, e.EndsAt ?? e.StartsAt.AddHours(1)));
            }
        }

        var now = clock.GetUtcNow();
        var holds = await db.Interviews.AsNoTracking()
            .Where(i => i.Status == InterviewStatus.Awaiting && i.HoldUntil > now && i.StartsAt < to && i.EndsAt > from && i.Id != exceptInterview)
            .Select(i => new { i.StartsAt, i.EndsAt, i.InterviewerId, i.Participants }).ToListAsync(ct);
        foreach (var h in holds.Where(h => (h.InterviewerId is { } x && users.Contains(x)) || ParticipantsOf(h.Participants).Any(users.Contains)))
        {
            busy.Add((h.StartsAt!.Value, h.EndsAt!.Value));
        }

        return busy;
    }

    private static bool Overlaps(IEnumerable<(DateTimeOffset From, DateTimeOffset To)> busy, DateTimeOffset from, DateTimeOffset to) =>
        busy.Any(b => b.From < to && b.To > from);

    /// <summary>
    /// Свободные слоты: рабочие дни и часы вакансии, без перерывов, не раньше минимального срока, не дальше горизонта, все участники свободны;
    /// onePerDay — не больше одного в день (кандидат выбирает из разных дней); after — только позже этого времени («другое время»).
    /// </summary>
    public async Task<IReadOnlyList<SlotView>> FreeSlotsAsync(Guid vacancyId, int count, int days, CancellationToken ct, DateTimeOffset? after = null, bool onePerDay = true)
    {
        var cfg = await ConfigAsync(vacancyId, ct);
        var today = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), cfg.Zone).Date;
        return await ScanAsync(cfg, count, today, today.AddDays(Math.Min(days, cfg.HorizonDays)), after, onePerDay, null, null, null, null, ct);
    }

    /// <summary>
    /// Перебор сетки вакансии (с начала рабочего дня шагом длительности) в днях [first, last] в пределах горизонта: рабочий день, без перерывов,
    /// не раньше срока записи и after, внутри промежутка from–to (если задан), все участники свободны (своё собеседование exceptInterview — нет),
    /// кроме времени skip (текущее время собеседования при переносе).
    /// </summary>
    private async Task<List<SlotView>> ScanAsync(InterviewConfig cfg, int count, DateTime first, DateTime last, DateTimeOffset? after, bool onePerDay,
        TimeOnly? from, TimeOnly? to, Guid? exceptInterview, DateTimeOffset? skip, CancellationToken ct)
    {
        var zone = cfg.Zone;
        var now = clock.GetUtcNow();
        var earliest = now + TimeSpan.FromHours(cfg.NoticeHours);
        if (after is { } a && a > earliest)
        {
            earliest = a;
        }

        var today = TimeZoneInfo.ConvertTime(now, zone).Date;
        first = first < today ? today : first.Date;
        last = last > today.AddDays(cfg.HorizonDays) ? today.AddDays(cfg.HorizonDays) : last.Date;
        var result = new List<SlotView>();
        if (first > last || count <= 0)
        {
            return result;
        }

        var busy = await BusyAsync(cfg.Users, Moment(first, zone), Moment(last.AddDays(2), zone), zone, exceptInterview, ct);
        for (var date = first; date <= last && result.Count < count; date = date.AddDays(1))
        {
            if (!WorkDay(cfg, date))
            {
                continue;
            }

            for (var start = cfg.DayStart.ToTimeSpan(); start + cfg.Length <= cfg.DayEnd.ToTimeSpan() && result.Count < count; start += cfg.Length)
            {
                var (lf, lt) = (TimeOnly.FromTimeSpan(start), TimeOnly.FromTimeSpan(start + cfg.Length));
                if (from is { } wf && lf < wf || to is { } wt && lt > wt)
                {
                    continue;
                }

                var at = Moment(date + start, zone);
                var end = at + cfg.Length;
                if (at < earliest || cfg.Breaks.Any(b => b.From < lt && b.To > lf) || Overlaps(busy, at, end) || skip is { } x && x == at)
                {
                    continue;
                }

                result.Add(new SlotView(at.ToUniversalTime(), end.ToUniversalTime()));
                if (onePerDay)
                {
                    break;
                }
            }
        }

        return result;
    }

    private static DateTimeOffset Moment(DateTime local, TimeZoneInfo zone) => new(local, zone.GetUtcOffset(local));

    private static bool WorkDay(InterviewConfig cfg, DateTime date) => cfg.Days.Contains(date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek);

    /// <summary>
    /// Точное время (не обязательно по сетке): рабочий день и часы вакансии, без перерывов, не раньше срока записи, в пределах горизонта, все
    /// участники свободны (своё собеседование exceptInterview — не помеха). Свободно — слот, иначе null.
    /// </summary>
    private async Task<SlotView?> ExactAsync(InterviewConfig cfg, DateTime local, Guid? exceptInterview, CancellationToken ct)
    {
        var zone = cfg.Zone;
        var now = clock.GetUtcNow();
        var at = Moment(local, zone);
        var end = at + cfg.Length;
        var (lf, lt) = (TimeOnly.FromDateTime(local), TimeOnly.FromDateTime(local + cfg.Length));
        var today = TimeZoneInfo.ConvertTime(now, zone).Date;
        if (!WorkDay(cfg, local.Date) || lf < cfg.DayStart || lt > cfg.DayEnd || lt < lf || at < now + TimeSpan.FromHours(cfg.NoticeHours)
            || local.Date > today.AddDays(cfg.HorizonDays) || cfg.Breaks.Any(b => b.From < lt && b.To > lf))
        {
            return null;
        }

        return Overlaps(await BusyAsync(cfg.Users, at, end, zone, exceptInterview, ct), at, end) ? null : new SlotView(at.ToUniversalTime(), end.ToUniversalTime());
    }

    /// <summary>
    /// Варианты по пожеланию (свободное время интервьюера и участников вакансии): точное время свободно — оно первым (Exact), дальше — соседние
    /// в тот же день; иначе — свободное время в нужные дни и часы, ближайшее к пожеланию (несколько в день); там ничего — ближайшие свободные
    /// с нужного дня (по одному в день). Время notAt (текущее собеседование) не предлагается; exceptInterview — своё собеседование не занимает время.
    /// </summary>
    public async Task<WishSlots> WishSlotsAsync(Guid vacancyId, SlotWish wish, int count, Guid? exceptInterview, CancellationToken ct, DateTimeOffset? notAt = null)
    {
        var cfg = await ConfigAsync(vacancyId, ct);
        var today = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), cfg.Zone).Date;
        var first = wish.Date?.ToDateTime(TimeOnly.MinValue) ?? today;
        var last = wish.DateTo?.ToDateTime(TimeOnly.MinValue) ?? wish.Date?.ToDateTime(TimeOnly.MinValue) ?? today.AddDays(cfg.HorizonDays);
        if (last < first)
        {
            (first, last) = (last, first);
        }

        bool Other(SlotView s) => notAt is not { } n || s.StartsAt != n.ToUniversalTime();
        var skip = notAt?.ToUniversalTime();
        if (wish.Time is { } time)
        {
            // Точное время — в названный день (сразу запись — только если день один и время свободно); без дня — ближайший день, где оно
            // свободно, но выбирает кандидат.
            var single = wish.Date is { } day && (wish.DateTo is null || wish.DateTo == day);
            for (var date = first < today ? today : first; date <= last && date <= today.AddDays(cfg.HorizonDays); date = date.AddDays(1))
            {
                if (await ExactAsync(cfg, date + time.ToTimeSpan(), exceptInterview, ct) is { } exact && Other(exact))
                {
                    var near = (await ScanAsync(cfg, 48, date, date, null, false, null, null, exceptInterview, skip, ct))
                        .Where(s => !(s.StartsAt < exact.EndsAt && s.EndsAt > exact.StartsAt))
                        .OrderBy(s => Math.Abs((s.StartsAt - exact.StartsAt).TotalMinutes)).Take(count - 1);
                    return new WishSlots([exact, .. near.OrderBy(s => s.StartsAt)], single);
                }

                if (single)
                {
                    break;
                }
            }
        }

        // Промежуток: точное время ± 3 часа, иначе — from–to пожелания; в каждом дне — ближайшие к пожеланию, дни — по порядку.
        var (from, to) = wish.Time is { } t
            ? (wish.From ?? (t.Hour >= 3 ? t.AddHours(-3) : TimeOnly.MinValue), wish.To ?? (t.Hour <= 20 ? t.AddHours(3) : TimeOnly.MaxValue))
            : (wish.From, wish.To);
        var inWindow = await ScanAsync(cfg, 200, first, last, null, false, from, to, exceptInterview, skip, ct);
        if (inWindow.Count > 0)
        {
            var target = (wish.Time ?? from)?.ToTimeSpan();
            var pick = inWindow
                .GroupBy(s => Local(s.StartsAt, cfg.TimeZoneId).Date)
                .SelectMany(g => g.OrderBy(s => target is { } x ? Math.Abs((Local(s.StartsAt, cfg.TimeZoneId).TimeOfDay - x).TotalMinutes) : 0).Select((s, i) => (s, i)))
                .OrderBy(x => x.i).ThenBy(x => x.s.StartsAt)
                .Take(count).Select(x => x.s).OrderBy(s => s.StartsAt).ToList();
            return new WishSlots(pick, false);
        }

        return new WishSlots(await ScanAsync(cfg, count, first, today.AddDays(cfg.HorizonDays), null, true, null, null, exceptInterview, skip, ct), false);
    }

    // ---------- Приглашение ----------

    private async Task<CandidateApplication> ApplicationAsync(Guid applicationId, CancellationToken ct) =>
        await db.CandidateApplications.Include(a => a.Vacancy).Include(a => a.Candidate).FirstOrDefaultAsync(a => a.Id == applicationId, ct)
        ?? throw new FieldNotFoundException("Отклик не найден.");

    /// <summary>Действующее собеседование отклика (ждёт подтверждения, подтверждено, перенесено) или предложение вариантов.</summary>
    public Task<Interview?> CurrentAsync(Guid applicationId, CancellationToken ct) =>
        db.Interviews.Where(i => i.ApplicationId == applicationId && (Active.Contains(i.Status) || i.Status == InterviewStatus.Proposed))
            .OrderByDescending(i => i.UpdatedAt ?? i.CreatedAt).FirstOrDefaultAsync(ct);

    /// <summary>
    /// Варианты времени кандидату (AI-рекрутер или HR): три свободных слота в разные дни; after — «другое время» (позже предложенных). У действующего
    /// собеседования варианты — для переноса (собеседование остаётся в силе, пока кандидат не выбрал). offered — уже подобранные варианты (по
    /// пожеланию кандидата, <see cref="WishSlotsAsync"/>). Слотов нет — исключение «расписание».
    /// </summary>
    public async Task<(Interview Interview, IReadOnlyList<SlotView> Slots)> ProposeAsync(Guid applicationId, Guid? actorId, CancellationToken ct, DateTimeOffset? after = null,
        IReadOnlyList<SlotView>? offered = null)
    {
        var application = await ApplicationAsync(applicationId, ct);
        if (RecruitmentRules.IsClosed(application.Stage))
        {
            throw new FieldValidationException("Отклик закрыт — собеседование не назначается.");
        }

        var cfg = await ConfigAsync(application.VacancyId, ct);
        var slots = offered ?? await FreeSlotsAsync(application.VacancyId, 3, cfg.HorizonDays, ct, after);
        var interview = await CurrentAsync(applicationId, ct);
        if (interview is null)
        {
            interview = new Interview
            {
                ApplicationId = applicationId,
                CandidateId = application.CandidateId,
                VacancyId = application.VacancyId,
                Format = cfg.Format,
                CreatedById = actorId,
                InterviewerId = cfg.InterviewerId,
                Participants = JsonSerializer.Serialize(cfg.Users, Json),
                TimeZone = cfg.TimeZoneId,
                Location = cfg.Format == "office" ? cfg.Address : null,
                MeetingUrl = cfg.Format == "online" ? cfg.MeetingUrl : null,
            };
            db.Interviews.Add(interview);
        }

        interview.ProposedSlotsJson = JsonSerializer.Serialize(slots, Json);
        interview.UpdatedAt = clock.GetUtcNow();
        await SaveAsync(ct);
        if (slots.Count == 0)
        {
            await exceptions.RaiseAsync("schedule", $"Нет свободного времени у интервьюеров вакансии {application.Vacancy?.Code} на ближайшие {cfg.HorizonDays} дн.",
                application.CandidateId, application.VacancyId, dedupKey: $"schedule:{applicationId:N}", ct: ct);
        }

        return (interview, slots);
    }

    public static IReadOnlyList<SlotView> Slots(Interview interview) =>
        string.IsNullOrEmpty(interview.ProposedSlotsJson) ? [] : JsonSerializer.Deserialize<List<SlotView>>(interview.ProposedSlotsJson, Json) ?? [];

    public static IReadOnlyList<Guid> ParticipantsOf(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json, Json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Варианты для кандидата текстом: «1) пн 10.11, 10:00» (пояс собеседования), день недели — на языке кандидата.</summary>
    public static string SlotsText(IReadOnlyList<SlotView> slots, CandidateLang lang, string? zone = null) =>
        RecruiterTexts.Slots(lang, slots.Select(s => Local(s.StartsAt, zone)));

    /// <summary>Язык переписки кандидата (ru, если ещё не понятен).</summary>
    public async Task<CandidateLang> LanguageAsync(Guid candidateId, CancellationToken ct)
    {
        var c = await db.Candidates.AsNoTracking().Where(x => x.Id == candidateId).Select(x => new { x.Language, x.PreferredScript }).FirstOrDefaultAsync(ct);
        return CandidateLanguages.Of(c?.Language, c?.PreferredScript) ?? CandidateLang.Ru;
    }

    /// <summary>
    /// Пригласить (HR или по оценке AI): варианты времени — кандидату в его диалог HR на его языке. Нет диалога — HR связывается сам (варианты видны в
    /// карточке). Возвращает собеседование-предложение и варианты.
    /// </summary>
    public async Task<(Interview Interview, IReadOnlyList<SlotView> Slots)> InviteAsync(Guid applicationId, Guid? actorId, CancellationToken ct)
    {
        var (interview, slots) = await ProposeAsync(applicationId, actorId, ct);
        var conversation = await ConversationAsync(interview.CandidateId, ct);
        if (conversation is not null && slots.Count > 0)
        {
            var lang = await LanguageAsync(interview.CandidateId, ct);
            var text = $"{RecruiterTexts.Invitation(lang)}\n{SlotsText(slots, lang, interview.TimeZone)}\n{RecruiterTexts.ChooseNumber(lang)} {RecruiterTexts.SlotOptions(lang)}";
            await outbox.EnqueueAsync(new OutboxRequest(conversation.AccountId, conversation.PeerId, conversation.Id, TelegramDepartments.Hr, "interview", text,
                $"invite:{interview.Id:N}:{slots[0].StartsAt:yyyyMMddHHmm}", actorId is null ? "ai" : "user", actorId), ct);
        }

        await audit.LogAsync(actorId is null ? ActorType.Agent : ActorType.User, actorId?.ToString() ?? "recruiter", "recruitment.interview.invited", nameof(Interview),
            interview.Id.ToString(), new { application = applicationId, slots = slots.Count }, ct);
        return (interview, slots);
    }

    public Task<OneBase.Domain.Telegram.TelegramConversation?> ConversationAsync(Guid candidateId, CancellationToken ct) =>
        db.TelegramConversations.AsNoTracking()
            .Where(c => c.Department == TelegramDepartments.Hr && c.RelatedType == RecruitmentService.CandidateRelation && c.RelatedId == candidateId)
            .OrderByDescending(c => c.LastMessageAt).FirstOrDefaultAsync(ct);

    // ---------- Запись, подтверждение, перенос, отмена ----------

    /// <summary>
    /// Записать на время. confirmed — кандидат выбрал сам (actorId = null) или HR отметил, что кандидат подтвердил: «подтверждено» и событие
    /// календаря в той же транзакции. Иначе (HR предложил одно время) — «ждёт подтверждения»: время удерживается Hold, календаря ещё нет.
    /// Под блокировкой участников время перепроверяется; занято — конфликт. Уведомления — после фиксации (задание).
    /// </summary>
    public async Task<Interview> BookAsync(Guid applicationId, DateTimeOffset startsAt, string? format, string? location, Guid? actorId, CancellationToken ct,
        bool confirmed = true, string? meetingUrl = null)
    {
        startsAt = startsAt.ToUniversalTime();
        var application = await ApplicationAsync(applicationId, ct);
        if (RecruitmentRules.IsClosed(application.Stage))
        {
            throw new FieldValidationException("Отклик закрыт — собеседование не назначается.");
        }

        var cfg = await ConfigAsync(application.VacancyId, ct);
        format = string.IsNullOrWhiteSpace(format) ? cfg.Format : format;
        if (!Formats.Contains(format))
        {
            throw new FieldValidationException("Формат: в офисе, онлайн или по телефону.");
        }

        var current = await CurrentAsync(applicationId, ct);
        if (current is not null && IsActive(current))
        {
            throw new FieldConflictException("У кандидата уже назначено собеседование по этой вакансии — перенесите или отмените его.");
        }

        var interview = current ?? new Interview { ApplicationId = applicationId, CandidateId = application.CandidateId, VacancyId = application.VacancyId, Format = format, CreatedById = actorId };
        await using (var tx = await db.LockedTransactionAsync(cfg.Users.Select(LockKey).ToList(), ct))
        {
            var now = clock.GetUtcNow();
            if (startsAt < now)
            {
                throw new FieldValidationException("Это время уже прошло.");
            }

            var end = startsAt + cfg.Length;
            if (Overlaps(await BusyAsync(cfg.Users, startsAt, end, cfg.Zone, current?.Id, ct), startsAt, end))
            {
                throw new FieldConflictException("Это время у интервьюера уже занято — выберите другое.");
            }

            if (current is null)
            {
                db.Interviews.Add(interview);
            }

            interview.StartsAt = startsAt;
            interview.EndsAt = end;
            interview.Format = format;
            interview.InterviewerId = cfg.InterviewerId;
            interview.Participants = JsonSerializer.Serialize(cfg.Users, Json);
            interview.TimeZone = cfg.TimeZoneId;
            interview.Location = Clean(location, 300) ?? (format == "office" ? cfg.Address : null);
            interview.MeetingUrl = Clean(meetingUrl, 500) ?? (format == "online" ? cfg.MeetingUrl : null);
            interview.ProposedSlotsJson = null;
            interview.UpdatedAt = now;
            if (confirmed)
            {
                interview.Status = InterviewStatus.Confirmed;
                interview.CandidateConfirmation = actorId is null ? CandidateConfirmation.Confirmed : CandidateConfirmation.ByHr;
                interview.ConfirmedAt = now;
                interview.HoldUntil = null;
                await EnsureCalendarAsync(interview, application, cfg, actorId, ct);
                MoveToInterviewStage(application, actorId, now);
            }
            else
            {
                interview.Status = InterviewStatus.Awaiting;
                interview.CandidateConfirmation = CandidateConfirmation.Pending;
                interview.HoldUntil = now + Hold;
            }

            await SaveAsync(ct);
            await tx.CommitAsync(ct);
        }

        await NotifyLaterAsync(interview, confirmed ? "confirmed" : "awaiting", actorId is null ? "candidate" : "hr", ct);
        await audit.LogAsync(actorId is null ? ActorType.Agent : ActorType.User, actorId?.ToString() ?? "recruiter", "recruitment.interview.booked",
            nameof(Interview), interview.Id.ToString(), new { application = applicationId, startsAt, format, status = interview.Status.ToString() }, ct);
        return interview;
    }

    /// <summary>
    /// «Да» кандидата (или отметка HR «подтвердил»): ждущее подтверждения — перепроверка времени под блокировкой, «подтверждено», событие календаря;
    /// перенесённое — подтверждение нового времени. Повтор — без изменений.
    /// </summary>
    public async Task<Interview> ConfirmAsync(Guid interviewId, Guid? actorId, CancellationToken ct)
    {
        var interview = await db.Interviews.FirstOrDefaultAsync(i => i.Id == interviewId, ct) ?? throw new FieldNotFoundException("Собеседование не найдено.");
        var confirmation = actorId is null ? CandidateConfirmation.Confirmed : CandidateConfirmation.ByHr;
        if (interview.Status is InterviewStatus.Confirmed or InterviewStatus.Rescheduled)
        {
            if (interview.CandidateConfirmation == CandidateConfirmation.Pending)
            {
                interview.CandidateConfirmation = confirmation;
                interview.UpdatedAt = clock.GetUtcNow();
                await SaveAsync(ct);
                await NotifyLaterAsync(interview, "candidate_confirmed", actorId is null ? "candidate" : "hr", ct);
            }

            return interview;
        }

        if (interview.Status != InterviewStatus.Awaiting || interview.StartsAt is not { } startsAt || interview.EndsAt is not { } endsAt)
        {
            throw new FieldConflictException("Подтверждать нечего: время ещё не выбрано или собеседование отменено.");
        }

        var application = await ApplicationAsync(interview.ApplicationId, ct);
        var cfg = await ConfigAsync(interview.VacancyId, ct);
        var users = UsersOf(interview, cfg);
        await using (var tx = await db.LockedTransactionAsync(users.Select(LockKey).ToList(), ct))
        {
            var now = clock.GetUtcNow();
            if (startsAt < now)
            {
                throw new FieldValidationException("Это время уже прошло — выберите другое.");
            }

            if (Overlaps(await BusyAsync(users, startsAt, endsAt, cfg.Zone, interview.Id, ct), startsAt, endsAt))
            {
                throw new FieldConflictException("Это время у интервьюера уже занято — выберите другое.");
            }

            interview.Status = InterviewStatus.Confirmed;
            interview.CandidateConfirmation = confirmation;
            interview.ConfirmedAt = now;
            interview.HoldUntil = null;
            interview.UpdatedAt = now;
            await EnsureCalendarAsync(interview, application, cfg, actorId, ct);
            MoveToInterviewStage(application, actorId, now);
            await SaveAsync(ct);
            await tx.CommitAsync(ct);
        }

        await NotifyLaterAsync(interview, "confirmed", actorId is null ? "candidate" : "hr", ct);
        await audit.LogAsync(actorId is null ? ActorType.Agent : ActorType.User, actorId?.ToString() ?? "candidate", "recruitment.interview.confirmed",
            nameof(Interview), interview.Id.ToString(), new { interview.StartsAt }, ct);
        return interview;
    }

    /// <summary>
    /// Перенести на новое время (HR или выбор кандидата из новых вариантов): перепроверка под блокировкой (своё событие — не помеха), событие
    /// календаря переносится тем же событием. Перенёс HR — новое время ждёт «да» кандидата; выбрал кандидат — сразу подтверждено.
    /// </summary>
    public async Task<Interview> RescheduleAsync(Guid interviewId, DateTimeOffset startsAt, Guid? actorId, CancellationToken ct)
    {
        startsAt = startsAt.ToUniversalTime();
        var interview = await db.Interviews.FirstOrDefaultAsync(i => i.Id == interviewId, ct) ?? throw new FieldNotFoundException("Собеседование не найдено.");
        if (!IsActive(interview))
        {
            throw new FieldConflictException("Переносить можно только назначенное собеседование.");
        }

        var application = await ApplicationAsync(interview.ApplicationId, ct);
        var cfg = await ConfigAsync(interview.VacancyId, ct);
        var users = UsersOf(interview, cfg);
        var length = (interview.EndsAt - interview.StartsAt) ?? cfg.Length;
        var wasAwaiting = interview.Status == InterviewStatus.Awaiting;
        await using (var tx = await db.LockedTransactionAsync(users.Select(LockKey).ToList(), ct))
        {
            var now = clock.GetUtcNow();
            if (startsAt < now)
            {
                throw new FieldValidationException("Это время уже прошло.");
            }

            if (Overlaps(await BusyAsync(users, startsAt, startsAt + length, cfg.Zone, interview.Id, ct), startsAt, startsAt + length))
            {
                throw new FieldConflictException("Это время у интервьюера уже занято — выберите другое.");
            }

            interview.PreviousStartsAt = wasAwaiting ? interview.PreviousStartsAt : interview.StartsAt;
            interview.StartsAt = startsAt;
            interview.EndsAt = startsAt + length;
            interview.ProposedSlotsJson = null;
            interview.RemindedAt = null;
            interview.UpdatedAt = now;
            if (wasAwaiting && actorId is null)
            {
                // Кандидат сам выбрал другое время вместо предложенного — это и есть подтверждение.
                interview.Status = InterviewStatus.Confirmed;
                interview.CandidateConfirmation = CandidateConfirmation.Confirmed;
                interview.ConfirmedAt = now;
                interview.HoldUntil = null;
                await EnsureCalendarAsync(interview, application, cfg, null, ct);
                MoveToInterviewStage(application, null, now);
            }
            else if (wasAwaiting)
            {
                interview.HoldUntil = now + Hold; // HR предложил другое время — снова ждём «да»
            }
            else
            {
                interview.Status = InterviewStatus.Rescheduled;
                interview.CandidateConfirmation = actorId is null ? CandidateConfirmation.Confirmed : CandidateConfirmation.Pending;
                await EnsureCalendarAsync(interview, application, cfg, actorId, ct);
            }

            await SaveAsync(ct);
            await tx.CommitAsync(ct);
        }

        var kind = wasAwaiting ? (interview.Status == InterviewStatus.Confirmed ? "confirmed" : "awaiting") : "rescheduled";
        await NotifyLaterAsync(interview, kind, actorId is null ? "candidate" : "hr", ct);
        await audit.LogAsync(actorId is null ? ActorType.Agent : ActorType.User, actorId?.ToString() ?? "candidate", "recruitment.interview.rescheduled",
            nameof(Interview), interview.Id.ToString(), new { from = interview.PreviousStartsAt, to = startsAt }, ct);
        return interview;
    }

    /// <summary>Отменить (HR, кандидат, система): «отменено», событие календаря удаляется; уведомления — участникам, HR, кандидату (если отменил не он).</summary>
    public async Task<Interview> CancelAsync(Guid interviewId, string by, string? reason, Guid? actorId, CancellationToken ct)
    {
        var interview = await db.Interviews.FirstOrDefaultAsync(i => i.Id == interviewId, ct) ?? throw new FieldNotFoundException("Собеседование не найдено.");
        if (interview.Status is InterviewStatus.Cancelled or InterviewStatus.Completed or InterviewStatus.NoShow)
        {
            return interview;
        }

        var wasProposal = interview.Status == InterviewStatus.Proposed;
        var now = clock.GetUtcNow();
        interview.Status = InterviewStatus.Cancelled;
        interview.CancelledAt = now;
        interview.CancelledBy = by;
        interview.CancelReason = Clean(reason, 500);
        interview.HoldUntil = null;
        interview.ProposedSlotsJson = null;
        interview.UpdatedAt = now;
        if (by == "candidate")
        {
            interview.CandidateConfirmation = CandidateConfirmation.Declined;
        }

        if (interview.CalendarEventId is { } e)
        {
            await db.CalendarEvents.Where(x => x.Id == e).ExecuteDeleteAsync(ct);
            interview.CalendarEventId = null;
        }

        await SaveAsync(ct);
        await NotifyLaterAsync(interview, wasProposal ? "declined" : "cancelled", by, ct);
        await audit.LogAsync(actorId is null ? ActorType.System : ActorType.User, actorId?.ToString() ?? by, "recruitment.interview.cancelled", nameof(Interview),
            interview.Id.ToString(), new { by, wasProposal }, ct);
        return interview;
    }

    private static IReadOnlyList<Guid> UsersOf(Interview interview, InterviewConfig cfg)
    {
        var users = ParticipantsOf(interview.Participants);
        return users.Count > 0 ? users : cfg.Users;
    }

    private void MoveToInterviewStage(CandidateApplication application, Guid? actorId, DateTimeOffset now)
    {
        if (application.Stage is ApplicationStage.New or ApplicationStage.Screening)
        {
            db.ApplicationEvents.Add(new ApplicationEvent
            {
                ApplicationId = application.Id, FromStage = application.Stage, ToStage = ApplicationStage.Interview, ActorId = actorId, At = now,
                Comment = "Назначено собеседование",
            });
            application.Stage = ApplicationStage.Interview;
            application.StageChangedAt = now;
        }
    }

    /// <summary>
    /// Событие календаря собеседования — одно: нет — создаётся, есть — переносится и обновляется (участники, место, описание). Кандидат в календаре
    /// OneBase не пользователь — он в описании (имя, телефон, Telegram). Добавляется в контекст — сохраняет вызывающий в своей транзакции.
    /// </summary>
    private async Task EnsureCalendarAsync(Interview interview, CandidateApplication application, InterviewConfig cfg, Guid? actorId, CancellationToken ct)
    {
        var users = UsersOf(interview, cfg);
        var calendar = interview.CalendarEventId is { } id ? await db.CalendarEvents.Include(e => e.Participants).FirstOrDefaultAsync(e => e.Id == id, ct) : null;
        if (calendar is null)
        {
            calendar = new CalendarEvent { Title = string.Empty, Kind = CalendarEventKind.Meeting, CreatedById = interview.InterviewerId ?? actorId ?? users.FirstOrDefault() };
            db.CalendarEvents.Add(calendar);
            interview.CalendarEventId = calendar.Id;
        }

        var candidate = application.Candidate!;
        var vacancy = application.Vacancy!;
        var contacts = await db.CandidateContacts.AsNoTracking().Where(c => c.CandidateId == candidate.Id
                && (c.Kind == CandidateContactKind.Phone || c.Kind == CandidateContactKind.TelegramUsername))
            .OrderByDescending(c => c.Verified).Select(c => new { c.Kind, c.Value }).ToListAsync(ct);
        var description = new StringBuilder();
        description.AppendLine($"Кандидат: {candidate.FullName}" + (contacts.Count > 0
            ? " (" + string.Join(", ", contacts.Take(2).Select(c => c.Kind == CandidateContactKind.TelegramUsername ? "Telegram @" + c.Value : c.Value)) + ")"
            : string.Empty));
        description.AppendLine($"Вакансия: {vacancy.Title} ({vacancy.Code})");
        description.AppendLine("Формат: " + interview.Format switch { "online" => "онлайн", "phone" => "по телефону", _ => "в офисе" }
                               + (interview.MeetingUrl is { } url ? $", ссылка: {url}" : string.Empty));
        if (cfg.Instructions is { Length: > 0 } instructions)
        {
            description.AppendLine($"Для кандидата: {instructions}");
        }

        calendar.Title = Cut($"Собеседование: {candidate.FullName} — {vacancy.Title} ({vacancy.Code})", 200);
        calendar.Description = description.ToString().TrimEnd();
        calendar.StartsAt = interview.StartsAt!.Value;
        calendar.EndsAt = interview.EndsAt;
        calendar.AllDay = false;
        calendar.Location = interview.Format == "online" ? Clean(interview.MeetingUrl, 300) : interview.Location;
        calendar.DepartmentId = vacancy.DepartmentId;
        calendar.UpdatedAt = clock.GetUtcNow();
        foreach (var removed in calendar.Participants.Where(p => !users.Contains(p.UserId)).ToList())
        {
            calendar.Participants.Remove(removed);
        }

        foreach (var user in users.Where(u => calendar.Participants.All(p => p.UserId != u)))
        {
            calendar.Participants.Add(new CalendarEventParticipant { EventId = calendar.Id, UserId = user });
        }
    }

    /// <summary>Сверка: у действующего собеседования нет события календаря (удалили мимо OneBase) — восстановить. Возвращает, восстановлено ли.</summary>
    public async Task<bool> RepairCalendarAsync(Guid interviewId, CancellationToken ct)
    {
        var interview = await db.Interviews.FirstOrDefaultAsync(i => i.Id == interviewId, ct);
        if (interview is null || interview.Status is not (InterviewStatus.Confirmed or InterviewStatus.Rescheduled) || interview.StartsAt is null
            || interview.CalendarEventId is { } e && await db.CalendarEvents.AnyAsync(x => x.Id == e, ct))
        {
            return false;
        }

        interview.CalendarEventId = null;
        var application = await ApplicationAsync(interview.ApplicationId, ct);
        await EnsureCalendarAsync(interview, application, await ConfigAsync(interview.VacancyId, ct), null, ct);
        await SaveAsync(ct);
        await audit.LogAsync(ActorType.System, "recruitment", "recruitment.interview.calendar_restored", nameof(Interview), interview.Id.ToString(), null, ct);
        return true;
    }

    /// <summary>Уведомления — заданием после фиксации (одно задание на событие: повтор не шлёт дважды).</summary>
    private Task NotifyLaterAsync(Interview interview, string kind, string by, CancellationToken ct) =>
        jobs.EnqueueAsync(RecruitmentQueues.Notifications, $"notify:{interview.Id:N}:{kind}:{interview.StartsAt:yyyyMMddHHmm}:{interview.UpdatedAt?.UtcTicks}",
            interview.Id, JsonSerializer.Serialize(new { kind, by }, Json), ct: ct);

    // ---------- Для HR ----------

    /// <summary>Сколько действующих собеседований сегодня (день — по времени Ташкента, как вид «today») — для кнопки «Собеседования».</summary>
    public async Task<int> TodayCountAsync(RecruitmentContext ctx, CancellationToken ct)
    {
        Read(ctx);
        var today = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Tashkent).Date;
        var from = new DateTimeOffset(today, TimeSpan.FromHours(5)).ToUniversalTime();
        var to = from.AddDays(1);
        return await db.Interviews.CountAsync(i => Active.Contains(i.Status) && i.StartsAt >= from && i.StartsAt < to, ct);
    }

    /// <summary>
    /// Список собеседований по виду: today, tomorrow, week, upcoming (действующие впереди и предложения), completed (прошло и не пришёл), cancelled,
    /// rescheduled, past (всё, кроме предложений); дни — по времени Ташкента.
    /// </summary>
    public async Task<IReadOnlyList<InterviewView>> ListAsync(RecruitmentContext ctx, string? view, CancellationToken ct)
    {
        Read(ctx);
        var now = clock.GetUtcNow();
        var today = TimeZoneInfo.ConvertTime(now, Tashkent).Date;
        static DateTimeOffset Day(DateTime d) => new DateTimeOffset(d, TimeSpan.FromHours(5)).ToUniversalTime();
        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var (d0, d1, d2, w0, w1) = (Day(today), Day(today.AddDays(1)), Day(today.AddDays(2)), Day(weekStart), Day(weekStart.AddDays(7)));
        var query = db.Interviews.AsNoTracking();
        query = (view ?? "upcoming") switch
        {
            "today" => query.Where(i => Active.Contains(i.Status) && i.StartsAt >= d0 && i.StartsAt < d1),
            "tomorrow" => query.Where(i => Active.Contains(i.Status) && i.StartsAt >= d1 && i.StartsAt < d2),
            "week" => query.Where(i => Active.Contains(i.Status) && i.StartsAt >= w0 && i.StartsAt < w1),
            "completed" => query.Where(i => i.Status == InterviewStatus.Completed || i.Status == InterviewStatus.NoShow),
            "cancelled" => query.Where(i => i.Status == InterviewStatus.Cancelled),
            "rescheduled" => query.Where(i => i.Status == InterviewStatus.Rescheduled),
            "past" => query.Where(i => i.Status != InterviewStatus.Proposed),
            _ => query.Where(i => i.Status == InterviewStatus.Proposed || (Active.Contains(i.Status) && i.EndsAt >= now)),
        };
        var rows = await (view is "completed" or "cancelled" ? query.OrderByDescending(i => i.StartsAt) : query.OrderBy(i => i.StartsAt)).Take(300).ToListAsync(ct);
        return await ViewsAsync(rows, ct);
    }

    /// <summary>Прежний вызов: true — предстоящие, false — все, кроме предложений.</summary>
    public Task<IReadOnlyList<InterviewView>> ListAsync(RecruitmentContext ctx, bool upcoming, CancellationToken ct) => ListAsync(ctx, upcoming ? "upcoming" : "past", ct);

    public async Task<IReadOnlyList<InterviewView>> ForCandidateAsync(RecruitmentContext ctx, Guid candidateId, CancellationToken ct)
    {
        Read(ctx);
        return await ViewsAsync(await db.Interviews.AsNoTracking().Where(i => i.CandidateId == candidateId).OrderByDescending(i => i.CreatedAt).ToListAsync(ct), ct);
    }

    public async Task<InterviewView> GetAsync(RecruitmentContext ctx, Guid id, CancellationToken ct)
    {
        Read(ctx);
        var row = await db.Interviews.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct) ?? throw new FieldNotFoundException("Собеседование не найдено.");
        return (await ViewsAsync([row], ct))[0];
    }

    private async Task<IReadOnlyList<InterviewView>> ViewsAsync(List<Interview> rows, CancellationToken ct)
    {
        var candidateIds = rows.Select(r => r.CandidateId).Distinct().ToList();
        var names = await db.Candidates.AsNoTracking().Where(c => candidateIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.FullName, ct);
        var vacancyIds = rows.Select(r => r.VacancyId).Distinct().ToList();
        var vacancies = await db.Vacancies.AsNoTracking().Include(v => v.Department).Where(v => vacancyIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, ct);
        var eventIds = rows.Where(r => r.CalendarEventId != null).Select(r => r.CalendarEventId!.Value).ToList();
        var eventUsers = await db.CalendarEvents.AsNoTracking().Where(e => eventIds.Contains(e.Id)).SelectMany(e => e.Participants.Select(p => new { e.Id, p.UserId }))
            .ToListAsync(ct);
        var userIds = rows.SelectMany(r => ParticipantsOf(r.Participants)).Concat(rows.Where(r => r.InterviewerId != null).Select(r => r.InterviewerId!.Value))
            .Concat(eventUsers.Select(e => e.UserId)).Distinct().ToList();
        var interviewIds = rows.Select(r => r.Id).ToList();
        var feedback = await db.InterviewFeedback.AsNoTracking().Where(f => interviewIds.Contains(f.InterviewId)).OrderBy(f => f.CreatedAt).ToListAsync(ct);
        userIds.AddRange(feedback.Select(f => f.AuthorId));
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        var resumes = await db.CandidateResumes.AsNoTracking().Where(r => candidateIds.Contains(r.CandidateId) && r.Sha256 != null)
            .OrderByDescending(r => r.CreatedAt).Select(r => new { r.CandidateId, r.Id, r.Summary, r.DocumentType }).ToListAsync(ct);
        var applicationIds = rows.Select(r => r.ApplicationId).Distinct().ToList();
        var scores = await db.CandidateScores.AsNoTracking().Where(s => applicationIds.Contains(s.ApplicationId)).OrderByDescending(s => s.CreatedAt)
            .Select(s => new { s.ApplicationId, s.Overall, s.Summary }).ToListAsync(ct);
        return rows.Select(i =>
        {
            vacancies.TryGetValue(i.VacancyId, out var v);
            var participants = i.CalendarEventId is { } e ? eventUsers.Where(x => x.Id == e).Select(x => x.UserId).ToList() : ParticipantsOf(i.Participants).ToList();
            var resume = resumes.FirstOrDefault(r => r.CandidateId == i.CandidateId && r.DocumentType is null or "resume")
                         ?? resumes.FirstOrDefault(r => r.CandidateId == i.CandidateId);
            var score = scores.FirstOrDefault(s => s.ApplicationId == i.ApplicationId);
            return new InterviewView(i.Id, i.ApplicationId, i.CandidateId, names.GetValueOrDefault(i.CandidateId) ?? "—", i.VacancyId, v?.Code ?? "—", v?.Title ?? "—",
                i.StartsAt, i.EndsAt, i.Format, i.Location, i.Status.ToString(), participants.Select(u => users.GetValueOrDefault(u) ?? "—").ToList(), Slots(i), i.ConfirmedAt,
                i.CandidateConfirmation.ToString(), i.InterviewerId is { } x ? users.GetValueOrDefault(x) : null, v?.Department?.Name, i.TimeZone, i.MeetingUrl, i.HoldUntil,
                i.PreviousStartsAt, i.CancelledBy, i.CancelReason, resume?.Id, score?.Overall, score?.Summary ?? resume?.Summary, i.CalendarEventId,
                feedback.Where(f => f.InterviewId == i.Id).Select(f => new FeedbackView(f.Id, f.AuthorId, users.GetValueOrDefault(f.AuthorId) ?? "—", f.Result.ToString(),
                    f.Technical, f.Communication, f.Professional, f.Strengths, f.Weaknesses, f.Comments, f.Recommendation, f.NextStage?.ToString(),
                    f.UpdatedAt ?? f.CreatedAt)).ToList());
        }).ToList();
    }

    /// <summary>
    /// Отклики для окна «Новое собеседование»: открытые (не закрытые этапом), поиск по имени, телефону или Telegram кандидата, коду или названию
    /// вакансии; свежие первыми. У каждого — действующее собеседование, если есть, и есть ли диалог в Telegram (куда придут варианты и вопрос).
    /// </summary>
    public async Task<IReadOnlyList<BookableView>> BookableAsync(RecruitmentContext ctx, string? search, CancellationToken ct)
    {
        Manage(ctx);
        var q = db.CandidateApplications.AsNoTracking().Where(a => a.ClosedAt == null && a.Stage != ApplicationStage.Hired && a.Stage != ApplicationStage.Rejected
                                                                   && a.Stage != ApplicationStage.Withdrawn);
        if (Clean(search, 100) is { } s)
        {
            var lower = s.ToLower().TrimStart('#', '@');
            var digits = new string(s.Where(char.IsAsciiDigit).ToArray());
            var number = (lower.StartsWith('v') || lower.StartsWith('в')) && int.TryParse(lower[1..], out var n) ? n : (int?)null;
            q = q.Where(a => a.Candidate!.FullName.ToLower().Contains(lower)
                             || a.Vacancy!.Title.ToLower().Contains(lower) || (number != null && a.Vacancy!.Number == number)
                             || db.CandidateContacts.Any(x => x.CandidateId == a.CandidateId && (x.Value.ToLower().Contains(lower) || (digits.Length >= 4 && x.Value.Contains(digits)))));
        }

        var rows = await q.OrderByDescending(a => a.StageChangedAt).Take(20)
            .Select(a => new { a.Id, a.CandidateId, a.Candidate!.FullName, a.VacancyId, a.Vacancy!.Number, a.Vacancy.Title, a.Stage }).ToListAsync(ct);
        var candidateIds = rows.Select(r => r.CandidateId).Distinct().ToList();
        var applicationIds = rows.Select(r => r.Id).ToList();
        var phones = await db.CandidateContacts.AsNoTracking().Where(c => candidateIds.Contains(c.CandidateId) && c.Kind == CandidateContactKind.Phone)
            .OrderByDescending(c => c.Verified).Select(c => new { c.CandidateId, c.Value }).ToListAsync(ct);
        var current = await db.Interviews.AsNoTracking()
            .Where(i => applicationIds.Contains(i.ApplicationId) && (Active.Contains(i.Status) || i.Status == InterviewStatus.Proposed))
            .OrderByDescending(i => i.UpdatedAt ?? i.CreatedAt).Select(i => new { i.ApplicationId, i.Id, i.Status, i.StartsAt }).ToListAsync(ct);
        var telegram = await db.TelegramConversations.AsNoTracking()
            .Where(c => c.Department == TelegramDepartments.Hr && c.RelatedType == RecruitmentService.CandidateRelation && c.RelatedId != null && candidateIds.Contains(c.RelatedId.Value))
            .Select(c => c.RelatedId!.Value).Distinct().ToListAsync(ct);
        return rows.Select(r =>
        {
            var i = current.FirstOrDefault(x => x.ApplicationId == r.Id);
            return new BookableView(r.Id, r.CandidateId, r.FullName, phones.FirstOrDefault(p => p.CandidateId == r.CandidateId)?.Value, r.VacancyId, "V" + r.Number,
                r.Title, r.Stage.ToString(), i?.Id, i?.Status.ToString(), i?.StartsAt, telegram.Contains(r.CandidateId));
        }).ToList();
    }

    /// <summary>Свободное время интервьюера и участников вакансии на день (все варианты по сетке вакансии) — для выбора времени HR.</summary>
    public async Task<IReadOnlyList<SlotView>> DaySlotsAsync(RecruitmentContext ctx, Guid vacancyId, DateOnly date, CancellationToken ct)
    {
        Read(ctx);
        var cfg = await ConfigAsync(vacancyId, ct);
        var day = date.ToDateTime(TimeOnly.MinValue);
        return await ScanAsync(cfg, 64, day, day, null, false, null, null, null, null, ct);
    }

    /// <summary>HR назначает время: кандидат подтвердил (по телефону) — сразу подтверждено; иначе — ждёт его «да» в Telegram.</summary>
    public async Task<IReadOnlyList<InterviewView>> BookByHrAsync(RecruitmentContext ctx, BookInput input, CancellationToken ct)
    {
        Manage(ctx);
        var interview = await BookAsync(input.ApplicationId, input.StartsAt, input.Format, input.Location, ctx.UserId, ct, input.ConfirmedByCandidate, input.MeetingUrl);
        return await ForCandidateAsync(ctx, interview.CandidateId, ct);
    }

    public async Task<InterviewView> RescheduleByHrAsync(RecruitmentContext ctx, Guid id, RescheduleInput input, CancellationToken ct)
    {
        Manage(ctx);
        await RescheduleAsync(id, input.StartsAt, ctx.UserId, ct);
        return await GetAsync(ctx, id, ct);
    }

    public async Task<InterviewView> CancelByHrAsync(RecruitmentContext ctx, Guid id, CancelInput input, CancellationToken ct)
    {
        Manage(ctx);
        await CancelAsync(id, "hr", input.Reason, ctx.UserId, ct);
        return await GetAsync(ctx, id, ct);
    }

    public async Task<InterviewView> ConfirmByHrAsync(RecruitmentContext ctx, Guid id, CancellationToken ct)
    {
        Manage(ctx);
        await ConfirmAsync(id, ctx.UserId, ct);
        return await GetAsync(ctx, id, ct);
    }

    private static void Read(RecruitmentContext ctx)
    {
        if (!ctx.SeesCandidates)
        {
            throw new FieldForbiddenException("Нет доступа к подбору.");
        }
    }

    private static void Manage(RecruitmentContext ctx)
    {
        if (!ctx.CanManage)
        {
            throw new FieldForbiddenException("Собеседования меняет HR.");
        }
    }

    /// <summary>Прошло, не пришёл или отменено (HR). Отмена убирает событие из календаря и уведомляет.</summary>
    public async Task<IReadOnlyList<InterviewView>> SetStatusAsync(RecruitmentContext ctx, Guid id, InterviewStatus status, CancellationToken ct)
    {
        Manage(ctx);
        if (status is not (InterviewStatus.Completed or InterviewStatus.NoShow or InterviewStatus.Cancelled))
        {
            throw new FieldValidationException("Статус: прошло, не пришёл или отменено.");
        }

        var interview = await db.Interviews.FirstOrDefaultAsync(i => i.Id == id, ct) ?? throw new FieldNotFoundException("Собеседование не найдено.");
        if (status == InterviewStatus.Cancelled)
        {
            await CancelAsync(id, "hr", null, ctx.UserId, ct);
        }
        else
        {
            if (!IsActive(interview) || interview.Status == InterviewStatus.Awaiting)
            {
                throw new FieldValidationException("«Прошло» и «не пришёл» — для назначенного собеседования.");
            }

            interview.Status = status;
            interview.CompletedAt = clock.GetUtcNow();
            interview.UpdatedAt = clock.GetUtcNow();
            await SaveAsync(ct);
            await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "recruitment.interview.status", nameof(Interview), id.ToString(), new { status = status.ToString() }, ct);
        }

        return await ForCandidateAsync(ctx, interview.CandidateId, ct);
    }

    /// <summary>
    /// Итоги собеседования от сотрудника HR (одни на сотрудника, правятся): результат, оценки 1–5, сильные и слабые стороны, комментарий, рекомендация,
    /// следующий этап. Собеседование с итогами — «прошло». Этап отклика меняет HR через RecruitmentService (история, срок хранения).
    /// </summary>
    public async Task<InterviewView> SaveFeedbackAsync(RecruitmentContext ctx, Guid id, FeedbackInput input, RecruitmentService recruitment, CancellationToken ct)
    {
        Manage(ctx);
        var interview = await db.Interviews.FirstOrDefaultAsync(i => i.Id == id, ct) ?? throw new FieldNotFoundException("Собеседование не найдено.");
        if (interview.Status is InterviewStatus.Proposed or InterviewStatus.Awaiting or InterviewStatus.Cancelled)
        {
            throw new FieldValidationException("Итоги вносятся по назначенному или прошедшему собеседованию.");
        }

        foreach (var (score, what) in new[] { (input.Technical, "Техническая оценка"), (input.Communication, "Общение"), (input.Professional, "Профессиональные качества") })
        {
            if (score is < 1 or > 5)
            {
                throw new FieldValidationException($"{what} — от 1 до 5.");
            }
        }

        var row = await db.InterviewFeedback.FirstOrDefaultAsync(f => f.InterviewId == id && f.AuthorId == ctx.UserId, ct);
        if (row is null)
        {
            row = new InterviewFeedback { InterviewId = id, ApplicationId = interview.ApplicationId, AuthorId = ctx.UserId };
            db.InterviewFeedback.Add(row);
        }

        row.Result = input.Result;
        (row.Technical, row.Communication, row.Professional) = (input.Technical, input.Communication, input.Professional);
        row.Strengths = Clean(input.Strengths, 2000);
        row.Weaknesses = Clean(input.Weaknesses, 2000);
        row.Comments = Clean(input.Comments, 4000);
        row.Recommendation = Clean(input.Recommendation, 2000);
        row.NextStage = input.NextStage;
        row.UpdatedAt = clock.GetUtcNow();
        if (interview.Status is InterviewStatus.Confirmed or InterviewStatus.Rescheduled)
        {
            interview.Status = InterviewStatus.Completed;
            interview.CompletedAt = clock.GetUtcNow();
            interview.UpdatedAt = clock.GetUtcNow();
        }

        await SaveAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "recruitment.interview.feedback", nameof(Interview), id.ToString(),
            new { result = input.Result.ToString(), next = input.NextStage?.ToString() }, ct);
        if (input.NextStage is { } next
            && await db.CandidateApplications.AsNoTracking().Where(a => a.Id == interview.ApplicationId).Select(a => a.Stage).FirstAsync(ct) != next)
        {
            await recruitment.ChangeStageAsync(ctx, interview.ApplicationId, new StageInput(next, "По итогам собеседования",
                next == ApplicationStage.Rejected ? Clean(input.Comments, 1000) ?? "По итогам собеседования" : null), ct);
        }

        return await GetAsync(ctx, id, ct);
    }

    /// <summary>
    /// Напоминание кандидату в Telegram за сутки до собеседования (одно на время собеседования): если с ним есть диалог HR. Возвращает число
    /// поставленных в очередь.
    /// </summary>
    public async Task<int> RemindAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var due = await db.Interviews
            .Where(i => (i.Status == InterviewStatus.Confirmed || i.Status == InterviewStatus.Rescheduled) && i.RemindedAt == null && i.StartsAt > now
                        && i.StartsAt <= now.AddHours(24))
            .ToListAsync(ct);
        var queued = 0;
        foreach (var interview in due)
        {
            var conversation = await ConversationAsync(interview.CandidateId, ct);
            interview.RemindedAt = now;
            if (conversation is null)
            {
                continue;
            }

            var lang = await LanguageAsync(interview.CandidateId, ct);
            var text = RecruiterTexts.Reminder(lang, RecruiterTexts.At(Local(interview.StartsAt!.Value, interview.TimeZone)),
                RecruiterTexts.Where(lang, interview.Format, interview.Location, interview.MeetingUrl));
            await outbox.EnqueueAsync(new OutboxRequest(conversation.AccountId, conversation.PeerId, conversation.Id, TelegramDepartments.Hr, "reminder", text,
                $"reminder:{interview.Id:N}:{interview.StartsAt:yyyyMMddHHmm}", "system"), ct);
            queued++;
        }

        await SaveAsync(ct);
        return queued;
    }

    /// <summary>Сохранить; собеседование одновременно изменил кто-то ещё (версия строки) — понятный конфликт вместо ошибки сервера.</summary>
    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new FieldConflictException("Собеседование только что изменилось — обновите страницу и повторите.");
        }
    }

    private static string? Clean(string? s, int max) => s?.Trim() is { Length: > 0 } t ? (t.Length <= max ? t : t[..max]) : null;

    private static string Cut(string s, int max) => s.Length <= max ? s : s[..max];
}
