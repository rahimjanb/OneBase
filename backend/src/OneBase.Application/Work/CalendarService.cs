using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Security;
using OneBase.Domain.Audit;
using OneBase.Domain.Work;

namespace OneBase.Application.Work;

/// <summary>
/// Событие для создания и правки. Время — местное (Ташкент). Аудитория: участники (Participants), отдел (Department — код) или вся компания
/// (ForEveryone); автор видит своё событие всегда.
/// </summary>
public sealed record CalendarEventInput(
    string? Title,
    string? Description,
    CalendarEventKind Kind,
    DateOnly Date,
    TimeOnly? Start,
    TimeOnly? End,
    bool AllDay,
    string? Location,
    IReadOnlyList<Guid>? Participants,
    string? Department,
    bool ForEveryone);

/// <summary>
/// Строка календаря: событие или срок моей задачи. Type — meeting, report_due, event, task_due. Start — null у событий на весь день и сроков без
/// времени. Href — куда ведёт: карточка задачи или событие в календаре.
/// </summary>
public sealed record CalendarItem(
    string Id,
    string Type,
    string Title,
    DateOnly Date,
    TimeOnly? Start,
    TimeOnly? End,
    bool AllDay,
    string? Location,
    int Participants,
    string? Organizer,
    string Href,
    bool Overdue);

public sealed record CalendarPerson(Guid Id, string Name, string? Position, string? Department);

public sealed record CalendarEventView(
    Guid Id,
    string Title,
    string? Description,
    CalendarEventKind Kind,
    DateOnly Date,
    TimeOnly? Start,
    TimeOnly? End,
    bool AllDay,
    string? Location,
    string? DepartmentCode,
    string? DepartmentName,
    bool ForEveryone,
    CalendarPerson? Organizer,
    IReadOnlyList<CalendarPerson> Participants,
    bool CanEdit);

/// <summary>Что может пользователь в календаре: события отдела (его код) и всей компании.</summary>
public sealed record CalendarAbilities(IReadOnlyList<CalendarDepartmentOption> Departments, bool CanAddForEveryone);

public sealed record CalendarDepartmentOption(string Code, string Name);

/// <summary>
/// Календарь OneBase: встречи, сроки отчётов, важные события и сроки моих задач. Кто видит событие — автор, участники, сотрудники отдела
/// события, все (событие компании). Событие отдела создаёт руководитель отдела, HR или руководство; событие компании — HR или руководство.
/// Менять и удалять — автор или руководство. Участникам приходит уведомление о приглашении и о переносе.
/// </summary>
public sealed class CalendarService(IAppDbContext db, UserNotifier notifier, IAuditLogger audit)
{
    public const int MaxRangeDays = 62;
    public const int MaxParticipants = 100;

    private static TimeSpan Offset => FieldClock.Offset;

    // ---------- Правила (чистые функции) ----------

    public static bool CanAddForEveryone(WorkContext ctx) => ctx.SeesAll || ctx.Permissions.Contains(Permissions.HrRead);

    public static bool CanAddForDepartment(WorkContext ctx, Guid departmentId) => CanAddForEveryone(ctx) || ctx.Heads(departmentId);

    public static bool CanEdit(WorkContext ctx, CalendarEvent e) => ctx.SeesAll || e.CreatedById == ctx.UserId;

    /// <summary>События, которые видит пользователь (фильтр в базе).</summary>
    private IQueryable<CalendarEvent> Visible(WorkContext ctx)
    {
        var me = ctx.UserId;
        var department = ctx.DepartmentId;
        return db.CalendarEvents.AsNoTracking().Where(e => e.ForEveryone || e.CreatedById == me || e.Participants.Any(p => p.UserId == me)
                                                           || (department != null && e.DepartmentId == department));
    }

    // ---------- Чтение ----------

    /// <summary>События и сроки моих открытых задач за период (включительно), по времени.</summary>
    public async Task<List<CalendarItem>> ItemsAsync(WorkContext ctx, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (to < from)
        {
            (from, to) = (to, from);
        }

        if (to.DayNumber - from.DayNumber + 1 > MaxRangeDays)
        {
            throw new FieldValidationException($"Период календаря — не больше {MaxRangeDays} дней.");
        }

        var fromUtc = FieldClock.DayStartUtc(from);
        var toUtc = FieldClock.DayStartUtc(to.AddDays(1));
        var events = await Visible(ctx).Where(e => e.StartsAt >= fromUtc && e.StartsAt < toUtc)
            .Select(e => new
            {
                e.Id, e.Title, e.Kind, e.StartsAt, e.EndsAt, e.AllDay, e.Location,
                Participants = e.Participants.Count,
                Organizer = e.CreatedBy == null ? null : e.CreatedBy.FullName,
            })
            .ToListAsync(ct);

        var me = ctx.UserId;
        var tasks = await db.WorkTasks.AsNoTracking()
            .Where(t => t.AssigneeId == me && t.ArchivedAt == null && WorkTaskService.Open.Contains(t.Status) && t.DueDate != null && t.DueDate >= from && t.DueDate <= to)
            .Select(t => new { t.Id, t.Title, t.Status, t.DueDate, t.DueTime })
            .ToListAsync(ct);

        var today = FieldClock.Today;
        var now = FieldClock.TimeOfDay;
        var items = events.Select(e =>
        {
            var start = e.StartsAt.ToOffset(Offset);
            var date = DateOnly.FromDateTime(start.DateTime);
            return new CalendarItem(e.Id.ToString(), TypeOf(e.Kind), e.Title, date, e.AllDay ? null : TimeOnly.FromDateTime(start.DateTime),
                e.AllDay || e.EndsAt is null ? null : TimeOnly.FromDateTime(e.EndsAt.Value.ToOffset(Offset).DateTime), e.AllDay, e.Location, e.Participants,
                e.Organizer, $"/calendar?date={date:yyyy-MM-dd}&event={e.Id}", false);
        }).Concat(tasks.Select(t => new CalendarItem(t.Id.ToString(), "task_due", t.Title, t.DueDate!.Value, t.DueTime, null, t.DueTime is null, null, 0, null,
            WorkTaskService.LinkOf(t.Id), WorkTaskService.IsOverdue(t.Status, t.DueDate, t.DueTime, today, now))));

        // Весь день — первым в дне, затем по времени.
        return items.OrderBy(i => i.Date).ThenBy(i => i.AllDay ? 0 : 1).ThenBy(i => i.Start).ThenBy(i => i.Title).ToList();
    }

    public async Task<CalendarEventView> GetAsync(WorkContext ctx, Guid id, CancellationToken ct)
    {
        var e = await Visible(ctx).Include(x => x.Participants).ThenInclude(p => p.User).ThenInclude(u => u!.Department)
                    .Include(x => x.Department).Include(x => x.CreatedBy).ThenInclude(u => u!.Department)
                    .FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new FieldNotFoundException("Событие не найдено.");
        var start = e.StartsAt.ToOffset(Offset);
        CalendarPerson? Person(Domain.Identity.User? u) => u is null ? null : new CalendarPerson(u.Id, u.FullName, u.Position, u.Department?.Name);
        return new CalendarEventView(e.Id, e.Title, e.Description, e.Kind, DateOnly.FromDateTime(start.DateTime), e.AllDay ? null : TimeOnly.FromDateTime(start.DateTime),
            e.AllDay || e.EndsAt is null ? null : TimeOnly.FromDateTime(e.EndsAt.Value.ToOffset(Offset).DateTime), e.AllDay, e.Location, e.Department?.Code,
            e.Department?.Name, e.ForEveryone, Person(e.CreatedBy),
            e.Participants.Select(p => Person(p.User)).OfType<CalendarPerson>().OrderBy(p => p.Name).ToList(), CanEdit(ctx, e));
    }

    /// <summary>Кого можно пригласить: действующие пользователи по имени (до 30).</summary>
    public async Task<List<CalendarPerson>> PeopleAsync(string? search, CancellationToken ct)
    {
        var q = db.Users.AsNoTracking().Where(u => u.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim().ToLower();
            q = q.Where(u => u.FullName.ToLower().Contains(text) || (u.Position != null && u.Position.ToLower().Contains(text)));
        }

        return await q.OrderBy(u => u.FullName).Take(30)
            .Select(u => new CalendarPerson(u.Id, u.FullName, u.Position, u.Department == null ? null : u.Department.Name)).ToListAsync(ct);
    }

    public async Task<CalendarAbilities> AbilitiesAsync(WorkContext ctx, CancellationToken ct)
    {
        var departments = await db.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync(ct);
        return new CalendarAbilities(departments.Where(d => CanAddForDepartment(ctx, d.Id)).Select(d => new CalendarDepartmentOption(d.Code, d.Name)).ToList(),
            CanAddForEveryone(ctx));
    }

    // ---------- Изменение ----------

    public async Task<Guid> CreateAsync(WorkContext ctx, CalendarEventInput input, CancellationToken ct)
    {
        var e = new CalendarEvent { Title = string.Empty, CreatedById = ctx.UserId };
        var participants = await ApplyAsync(ctx, e, input, ct);
        db.CalendarEvents.Add(e);
        foreach (var user in participants)
        {
            e.Participants.Add(new CalendarEventParticipant { EventId = e.Id, UserId = user });
        }

        await NotifyAsync(ctx, e, participants.Where(p => p != ctx.UserId), "Приглашение", ct);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "calendar.event.created", "calendar_event", e.Id.ToString(),
            new { kind = e.Kind.ToString(), participants = participants.Count, department = e.DepartmentId, e.ForEveryone }, ct);
        return e.Id;
    }

    public async Task UpdateAsync(WorkContext ctx, Guid id, CalendarEventInput input, CancellationToken ct)
    {
        var e = await db.CalendarEvents.Include(x => x.Participants).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new FieldNotFoundException("Событие не найдено.");
        if (!await Visible(ctx).AnyAsync(x => x.Id == id, ct))
        {
            throw new FieldNotFoundException("Событие не найдено.");
        }

        if (!CanEdit(ctx, e))
        {
            throw new FieldForbiddenException("Менять событие может его автор или руководство.");
        }

        var (startsAt, endsAt, allDay) = (e.StartsAt, e.EndsAt, e.AllDay);
        var participants = await ApplyAsync(ctx, e, input, ct);
        var before = e.Participants.Select(p => p.UserId).ToHashSet();
        foreach (var removed in e.Participants.Where(p => !participants.Contains(p.UserId)).ToList())
        {
            e.Participants.Remove(removed);
        }

        var added = participants.Where(p => !before.Contains(p)).ToList();
        foreach (var user in added)
        {
            e.Participants.Add(new CalendarEventParticipant { EventId = e.Id, UserId = user });
        }

        e.UpdatedAt = DateTimeOffset.UtcNow;
        await NotifyAsync(ctx, e, added.Where(p => p != ctx.UserId), "Приглашение", ct);
        if (startsAt != e.StartsAt || endsAt != e.EndsAt || allDay != e.AllDay)
        {
            await NotifyAsync(ctx, e, participants.Where(p => before.Contains(p) && p != ctx.UserId), "Перенесено", ct);
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "calendar.event.updated", "calendar_event", e.Id.ToString(),
            new { kind = e.Kind.ToString(), participants = participants.Count }, ct);
    }

    public async Task DeleteAsync(WorkContext ctx, Guid id, CancellationToken ct)
    {
        var e = await db.CalendarEvents.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new FieldNotFoundException("Событие не найдено.");
        if (!await Visible(ctx).AnyAsync(x => x.Id == id, ct))
        {
            throw new FieldNotFoundException("Событие не найдено.");
        }

        if (!CanEdit(ctx, e))
        {
            throw new FieldForbiddenException("Удалить событие может его автор или руководство.");
        }

        db.CalendarEvents.Remove(e);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "calendar.event.deleted", "calendar_event", id.ToString(), null, ct);
    }

    /// <summary>Проверяет ввод и заполняет событие; возвращает участников (без повторов, действующие пользователи).</summary>
    private async Task<List<Guid>> ApplyAsync(WorkContext ctx, CalendarEvent e, CalendarEventInput input, CancellationToken ct)
    {
        var title = (input.Title ?? string.Empty).Trim();
        if (title.Length is 0 or > 300)
        {
            throw new FieldValidationException("Название события — от 1 до 300 символов.");
        }

        var description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        if (description is { Length: > 4000 })
        {
            throw new FieldValidationException("Описание — до 4000 символов.");
        }

        var location = string.IsNullOrWhiteSpace(input.Location) ? null : input.Location.Trim();
        if (location is { Length: > 300 })
        {
            throw new FieldValidationException("Место — до 300 символов.");
        }

        if (!Enum.IsDefined(input.Kind))
        {
            throw new FieldValidationException("Вид события — встреча, срок отчёта или событие.");
        }

        if (!input.AllDay && input.Start is null)
        {
            throw new FieldValidationException("Укажите время начала или отметьте «Весь день».");
        }

        if (!input.AllDay && input.End is { } end && end <= input.Start)
        {
            throw new FieldValidationException("Окончание — позже начала.");
        }

        Guid? department = null;
        if (!string.IsNullOrWhiteSpace(input.Department))
        {
            var found = await db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Code == input.Department, ct) ?? throw new FieldValidationException("Отдел не найден.");
            // Прежний отдел события можно оставить; новый — только свой (руководитель), HR или руководство.
            if (found.Id != e.DepartmentId && !CanAddForDepartment(ctx, found.Id))
            {
                throw new FieldForbiddenException("Событие для отдела создаёт руководитель отдела, HR или руководство.");
            }

            department = found.Id;
        }

        if (input.ForEveryone && !e.ForEveryone && !CanAddForEveryone(ctx))
        {
            throw new FieldForbiddenException("Событие для всей компании создаёт HR или руководство.");
        }

        var ids = (input.Participants ?? []).Distinct().ToList();
        if (ids.Count > MaxParticipants)
        {
            throw new FieldValidationException($"Участников — до {MaxParticipants}.");
        }

        var known = e.Participants.Select(p => p.UserId).ToHashSet();
        var active = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id) && u.IsActive).Select(u => u.Id).ToListAsync(ct);
        // Отключённого участника можно оставить в событии, нового — только действующего.
        if (ids.Any(id => !active.Contains(id) && !known.Contains(id)))
        {
            throw new FieldValidationException("Участники — действующие пользователи OneBase.");
        }

        e.Title = title;
        e.Description = description;
        e.Location = location;
        e.Kind = input.Kind;
        e.AllDay = input.AllDay;
        e.StartsAt = new DateTimeOffset(input.Date.ToDateTime(input.AllDay ? TimeOnly.MinValue : input.Start!.Value), Offset).ToUniversalTime();
        e.EndsAt = !input.AllDay && input.End is { } finish ? new DateTimeOffset(input.Date.ToDateTime(finish), Offset).ToUniversalTime() : null;
        e.DepartmentId = department;
        e.ForEveryone = input.ForEveryone;
        return ids;
    }

    private async Task NotifyAsync(WorkContext ctx, CalendarEvent e, IEnumerable<Guid> recipients, string what, CancellationToken ct)
    {
        var list = recipients.ToList();
        if (list.Count == 0)
        {
            return;
        }

        var author = await db.Users.AsNoTracking().Where(u => u.Id == ctx.UserId).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        var start = e.StartsAt.ToOffset(Offset);
        var when = start.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)
                   + (e.AllDay ? ", весь день" : " " + start.ToString("HH:mm", CultureInfo.InvariantCulture)
                                                    + (e.EndsAt is { } end ? "–" + end.ToOffset(Offset).ToString("HH:mm", CultureInfo.InvariantCulture) : ""));
        var body = string.Join("; ", new[] { when, string.IsNullOrWhiteSpace(author) ? null : $"Организатор: {author}" }.OfType<string>());
        var link = $"/calendar?date={start:yyyy-MM-dd}&event={e.Id}";
        foreach (var user in list)
        {
            notifier.Notify(user, UserNotificationKind.CalendarInvite, $"{what}: {e.Title}", body, link);
        }
    }

    public static string TypeOf(CalendarEventKind kind) => kind switch
    {
        CalendarEventKind.ReportDue => "report_due",
        CalendarEventKind.Event => "event",
        _ => "meeting",
    };
}
