using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Security;
using OneBase.Domain.Hr;

namespace OneBase.Application.Hr;

/// <summary>
/// Кто что видит в разделе HR (проверяется здесь, интерфейс только показывает ответ API):
/// право «HR» (роль HR, директор, администратор) — все сотрудники; руководитель отдела (work.DepartmentHeads) — сотрудники подразделений
/// Verifix, привязанных к его отделу (с дочерними); остальные — ничего. Divisions = null — все подразделения.
/// </summary>
public sealed record HrContext(Guid UserId, bool SeesAll, bool ManagesIntegration, IReadOnlySet<long>? Divisions, IReadOnlyList<string> HeadOf)
{
    public bool HasAccess => SeesAll || Divisions is { Count: > 0 };

    /// <summary>Заметки HR в карточке — только у права «HR».</summary>
    public bool CanEditNotes => SeesAll;

    public bool CanSee(long? divisionId) => SeesAll || (divisionId is { } d && Divisions is not null && Divisions.Contains(d));
}

public sealed record HrDivisionView(long Id, string Name, long? ParentId, string? DepartmentCode, bool Missing, int Employees);

public sealed record HrSyncBrief(
    bool Configured,
    bool Enabled,
    bool Running,
    DateTimeOffset? LastSuccessAt,
    DateTimeOffset? LastRunAt,
    string? LastStatus,
    string? LastMessage,
    DateTime? TracksThrough,
    DateOnly? TimesheetFrom,
    DateOnly? TimesheetTo);

/// <summary>Scheduled — по графику на день: все, кроме выходного и дней без графика (с ними явку не считают).</summary>
public sealed record HrGroupStat(long? Id, string Name, int Active, int Scheduled, int Present, int Late, int Absent, int OnLeave, int Unknown, int Events);

public sealed record HrLocationStat(long? Id, string Name, int Employees, int Events);

/// <summary>
/// «Требует внимания»: отсутствует, опоздал или «не ясно». Kind — absent | late | unknown. FirstEvent — первая отметка дня
/// (для «не ясно»: отметки есть, подтверждённого прихода нет).
/// </summary>
public sealed record HrAttentionRow(Guid EmployeeId, string Employee, string? Division, string Kind, DateTime? PlanBegin, DateTime? FirstIn, int? LateMinutes,
    int Events, DateTime? FirstEvent);

/// <summary>
/// День для сравнения — предыдущий день, когда по графику работала хотя бы половина от сегодняшнего (после выходных — пятница).
/// Сегодня — на тот же час (кто пришёл до этого времени), прошлый день — за весь день. PresentPct — на месте от «по графику».
/// </summary>
public sealed record HrOverviewCompare(DateOnly Date, double PresentPct, TimeOnly? AvgArrival);

/// <summary>
/// Обзор HR за день (сегодня или прошлый). Шесть итогов дня — вовремя (Present − Late), опоздали, отсутствуют, по документу, ожидаются,
/// не ясно — в сумме дают Scheduled. AvgArrival — средний приход в рабочий день по графику; LateWeek и LateLastWeek — опоздания с понедельника
/// по этот день и за те же дни прошлой недели; PassesByHour — проходы по часам (24 значения).
/// </summary>
public sealed record HrOverview(
    DateOnly Date,
    DateTime Now,
    bool IsToday,
    int Active,
    int? ActiveDeltaMonth,
    int Scheduled,
    int Present,
    int Late,
    int Absent,
    int OnLeave,
    int Expected,
    int DayOff,
    int NoSchedule,
    int Unknown,
    int EventsToday,
    int MissingInVerifix,
    TimeOnly? AvgArrival,
    int? AvgLateMinutes,
    HrOverviewCompare? Compare,
    int LateWeek,
    int LateLastWeek,
    IReadOnlyList<int> PassesByHour,
    IReadOnlyList<HrAttentionRow> Attention,
    int AttentionTotal,
    IReadOnlyList<HrGroupStat> Divisions,
    IReadOnlyList<HrLocationStat> Locations,
    HrSyncBrief Sync,
    bool SeesAll);

public sealed record HrEmployeeRow(
    Guid Id,
    long VerifixId,
    string FullName,
    string? Phone,
    string Status,
    DateOnly? HiringDate,
    DateOnly? DismissalDate,
    long? DivisionId,
    string? Division,
    string? Job,
    string? Schedule,
    DateTimeOffset? MissingSince,
    DateTime? LastEventAt,
    HrDayStatus? Today,
    bool TodayLate);

public sealed record HrEmployeeQuery(string? Search, long? DivisionId, string? Status, string? Sort, int Page = 1, int PageSize = 50);

public sealed record HrEventRow(
    long Id,
    long TrackId,
    Guid? EmployeeId,
    string? Employee,
    string? Division,
    string? Location,
    string TrackType,
    string? MarkType,
    string RawTime,
    DateTime LocalTime,
    DateTimeOffset OccurredAt,
    DateTimeOffset ImportedAt,
    DateTimeOffset? MissingSince);

/// <summary>
/// Карточка сотрудника за период. Events — все отметки периода по времени (для раскрытия дня); NoteUpdatedBy — кто последним менял заметку;
/// VerifixUrl — адрес Verifix из настроек интеграции; Sync — свежесть данных Verifix.
/// </summary>
public sealed record HrEmployeeCard(
    HrEmployeeRow Employee,
    string? MiddleName,
    string? Email,
    string? OrgUnit,
    string? Note,
    DateTimeOffset? NoteUpdatedAt,
    string? NoteUpdatedBy,
    DateTimeOffset FirstSyncedAt,
    DateTimeOffset LastSyncedAt,
    bool CanEditNote,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<HrDayResult> Days,
    IReadOnlyList<HrEventRow> Events,
    HrSummary Summary,
    string? VerifixUrl,
    HrSyncBrief Sync);

/// <summary>Итог периода по сотруднику или группе.</summary>
public sealed record HrSummary(
    int PlanDays,
    int PresentDays,
    int LateDays,
    int LateMinutes,
    int EarlyDays,
    int EarlyMinutes,
    int AbsentDays,
    int LeaveDays,
    int UnknownDays,
    int NoExitDays,
    int WorkedMinutes,
    int PlanMinutes)
{
    public static HrSummary Of(IEnumerable<HrDayResult> days)
    {
        int plan = 0, present = 0, late = 0, lateMin = 0, early = 0, earlyMin = 0, absent = 0, leave = 0, unknown = 0, noExit = 0, worked = 0, planMin = 0;
        foreach (var d in days)
        {
            if (d.Status == HrDayStatus.NotEmployed)
            {
                continue;
            }

            var workday = d.DayKind == "W";
            if (workday)
            {
                plan++;
                planMin += d.PlanMinutes ?? 0;
            }

            switch (d.Status)
            {
                case HrDayStatus.Present:
                    present++;
                    break;
                case HrDayStatus.Absent:
                    absent++;
                    break;
                case HrDayStatus.OnLeave:
                    leave++;
                    break;
                case HrDayStatus.Unknown:
                    unknown++;
                    break;
            }

            if (d.Late)
            {
                late++;
                lateMin += d.LateMinutes ?? 0;
            }

            if (d.EarlyLeave)
            {
                early++;
                earlyMin += d.EarlyLeaveMinutes ?? 0;
            }

            if (d.NoExit)
            {
                noExit++;
            }

            worked += d.WorkedMinutes ?? 0;
        }

        return new HrSummary(plan, present, late, lateMin, early, earlyMin, absent, leave, unknown, noExit, worked, planMin);
    }
}

/// <summary>Отклонение: опоздание, ранний уход, отсутствие, нет ухода.</summary>
public sealed record HrDeviation(
    DateOnly Date,
    Guid EmployeeId,
    string Employee,
    string? Division,
    string Kind,
    int? Minutes,
    DateTime? PlanBegin,
    DateTime? PlanEnd,
    DateTime? FirstIn,
    DateTime? LastOut,
    string? Reason);

public sealed record HrDayRow(Guid EmployeeId, string Employee, string? Division, string? Job, HrDayResult Day);

/// <summary>
/// Раздел HR: обзор, сотрудники, посещаемость, опоздания и отсутствия, рабочее время и табели, отчёты. Только чтение данных Verifix;
/// единственное, что меняется здесь, — локальная заметка HR в карточке сотрудника.
/// </summary>
public sealed partial class HrService(IAppDbContext db, IHrRules rulesSource, TimeProvider time)
{
    public const int MaxPeriodDays = 62;

    /// <summary>Предел периода «Отчётов» и выгрузки в Excel — квартал.</summary>
    public const int MaxReportDays = 92;

    public async Task<HrContext> ContextAsync(Guid userId, IReadOnlySet<string> permissions, CancellationToken ct)
    {
        var manages = permissions.Contains(Permissions.IntegrationsManage);
        if (permissions.Contains(Permissions.HrRead))
        {
            return new HrContext(userId, true, manages, null, []);
        }

        var heads = await db.WorkDepartmentHeads.AsNoTracking().Where(h => h.UserId == userId).Select(h => h.Department!.Code).ToListAsync(ct);
        if (heads.Count == 0)
        {
            return new HrContext(userId, false, manages, new HashSet<long>(), []);
        }

        var divisions = await db.HrReferences.AsNoTracking().Where(r => r.Kind == HrReferenceKinds.Division)
            .Select(r => new { r.VerifixId, r.ParentId, r.DepartmentCode }).ToListAsync(ct);
        var children = divisions.Where(d => d.ParentId != null).ToLookup(d => d.ParentId!.Value, d => d.VerifixId);
        var visible = new HashSet<long>();
        var queue = new Queue<long>(divisions.Where(d => d.DepartmentCode != null && heads.Contains(d.DepartmentCode)).Select(d => d.VerifixId));
        while (queue.TryDequeue(out var id))
        {
            if (visible.Add(id))
            {
                foreach (var child in children[id])
                {
                    queue.Enqueue(child);
                }
            }
        }

        return new HrContext(userId, false, manages, visible, heads);
    }

    /// <summary>Видит ли пользователь раздел HR (для меню).</summary>
    public async Task<bool> HasAccessAsync(Guid userId, IReadOnlySet<string> permissions, CancellationToken ct) =>
        permissions.Contains(Permissions.HrRead) || permissions.Contains(Permissions.IntegrationsManage) || (await ContextAsync(userId, permissions, ct)).HasAccess;

    /// <summary>Отметок в карточке сотрудника — с запасом на 62 дня по нескольку проходов в день.</summary>
    private const int MaxCardEvents = 5000;

    // ---------- Обзор ----------

    /// <summary>Сколько строк «Требует внимания» отдаёт обзор (на экране — сколько помещается; остальные — в «Опозданиях и отсутствиях»).</summary>
    public const int AttentionTake = 10;

    /// <summary>Обзор за день: date — прошлый день или сегодня (по умолчанию и для будущих дат).</summary>
    public async Task<HrOverview> OverviewAsync(HrContext ctx, DateOnly? date, CancellationToken ct)
    {
        Require(ctx);
        var rules = await rulesSource.GetRulesAsync(ct);
        var now = Now(rules);
        var today = DateOnly.FromDateTime(now);
        var day = date is { } d && d < today ? d : today;
        var isToday = day == today;

        // С понедельника прошлой недели: опоздания этой недели к тем же дням прошлой и день для сравнения — одним расчётом.
        var monday = day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
        var from = monday.AddDays(-7);
        var period = await EvaluateAsync(ctx, from, day, null, null, null, rules, ct);
        var names = await NamesAsync(ct);
        int Index(DateOnly x) => x.DayNumber - from.DayNumber;

        var rows = period.Rows.Select(r => (r.Employee, Day: r.Days[Index(day)])).Where(r => r.Day.Status != HrDayStatus.NotEmployed).ToList();
        var events = await VisibleEvents(ctx).Where(e => e.LocalDate == day && e.MissingSince == null)
            .GroupBy(e => e.LocationId)
            .Select(g => new { LocationId = g.Key, Events = g.Count(), Employees = g.Select(e => e.VerifixEmployeeId).Distinct().Count() })
            .ToListAsync(ct);
        var hours = await VisibleEvents(ctx).Where(e => e.LocalDate == day && e.MissingSince == null)
            .GroupBy(e => e.LocalTime.Hour).Select(g => new { Hour = g.Key, Count = g.Count() }).ToListAsync(ct);
        var passesByHour = new int[24];
        foreach (var h in hours)
        {
            passesByHour[h.Hour] = h.Count;
        }

        var divisions = rows.GroupBy(r => r.Employee.DivisionId)
            .Select(g => new HrGroupStat(g.Key, Name(names.Divisions, g.Key, "Без подразделения")!, g.Count(), g.Count(r => Scheduled(r.Day)),
                g.Count(r => r.Day.Status == HrDayStatus.Present), g.Count(r => r.Day.Late), g.Count(r => r.Day.Status == HrDayStatus.Absent),
                g.Count(r => r.Day.Status == HrDayStatus.OnLeave), g.Count(r => r.Day.Status == HrDayStatus.Unknown), g.Sum(r => r.Day.Events)))
            .OrderByDescending(g => g.Active).ThenBy(g => g.Name).ToList();
        var locations = events
            .Select(e => new HrLocationStat(e.LocationId, Name(names.Locations, e.LocationId, "Локация не указана")!, e.Employees, e.Events))
            .OrderByDescending(l => l.Events).ThenBy(l => l.Name).ToList();
        var missing = await Visible(ctx).CountAsync(e => e.MissingSince != null, ct);

        // Сравнение: сегодня — на тот же час, прошлый день — целиком.
        var scheduled = rows.Count(r => Scheduled(r.Day));
        HrOverviewCompare? compare = null;
        for (var x = day.AddDays(-1); x >= from && compare is null; x = x.AddDays(-1))
        {
            var days = period.Rows.Select(r => r.Days[Index(x)]).Where(Scheduled).ToList();
            if (days.Count == 0 || days.Count * 2 < scheduled)
            {
                continue;
            }

            var limit = isToday ? x.ToDateTime(TimeOnly.FromDateTime(now)) : (DateTime?)null;
            var present = days.Where(v => v.Status == HrDayStatus.Present && (limit is null || v.FirstIn <= limit)).ToList();
            compare = new HrOverviewCompare(x, Math.Round(100.0 * present.Count / days.Count, 1), AvgArrival(present));
        }

        int LateBetween(DateOnly a, DateOnly b) => period.Rows.Sum(r => r.Days.Count(v => v.Date >= a && v.Date <= b && v.Late));
        var late = rows.Where(r => r.Day.Late).ToList();

        // Штат месяц назад — по датам приёма и увольнения; без дат приёма в Verifix изменение не посчитать.
        var staff = await Visible(ctx).Where(e => e.Status != HrEmployeeStatus.NotHired)
            .Select(e => new { e.Status, e.HiringDate, e.DismissalDate }).ToListAsync(ct);
        int EmployedOn(DateOnly x) => staff.Count(s => (s.HiringDate is not { } hired || hired <= x)
                                                        && (s.DismissalDate is { } dismissed ? dismissed >= x : s.Status != HrEmployeeStatus.Dismissed));
        int? activeDelta = staff.Any(s => s.HiringDate != null) ? EmployedOn(day) - EmployedOn(day.AddMonths(-1)) : null;

        // «Требует внимания»: отсутствующие, затем опоздавшие (сильнее — выше), затем «не ясно».
        var attention = rows.Where(r => r.Day.Status is HrDayStatus.Absent or HrDayStatus.Unknown || r.Day.Late)
            .OrderBy(r => r.Day.Status == HrDayStatus.Absent ? 0 : r.Day.Late ? 1 : 2)
            .ThenByDescending(r => r.Day.LateMinutes ?? 0).ThenBy(r => r.Employee.FullName)
            .ToList();
        var top = attention.Take(AttentionTake).ToList();
        var unclear = top.Where(r => r.Day.Status == HrDayStatus.Unknown && r.Day.Events > 0).Select(r => r.Employee.Id).ToList();
        var firstEvents = unclear.Count == 0
            ? new Dictionary<Guid, DateTime>()
            : await db.HrAttendanceEvents.AsNoTracking()
                .Where(e => e.EmployeeId != null && unclear.Contains(e.EmployeeId.Value) && e.LocalDate == day && e.MissingSince == null)
                .GroupBy(e => e.EmployeeId!.Value).Select(g => new { g.Key, First = g.Min(e => e.LocalTime) })
                .ToDictionaryAsync(g => g.Key, g => g.First, ct);
        var attentionRows = top.Select(r => new HrAttentionRow(r.Employee.Id, r.Employee.FullName, Name(names.Divisions, r.Employee.DivisionId, null),
            r.Day.Status == HrDayStatus.Absent ? "absent" : r.Day.Late ? "late" : "unknown", r.Day.PlanBegin, r.Day.FirstIn, r.Day.LateMinutes, r.Day.Events,
            firstEvents.TryGetValue(r.Employee.Id, out var first) ? first : null)).ToList();

        return new HrOverview(day, now, isToday, rows.Count, activeDelta, scheduled,
            rows.Count(r => r.Day.Status == HrDayStatus.Present), late.Count, rows.Count(r => r.Day.Status == HrDayStatus.Absent),
            rows.Count(r => r.Day.Status == HrDayStatus.OnLeave), rows.Count(r => r.Day.Status == HrDayStatus.Expected),
            rows.Count(r => r.Day.Status == HrDayStatus.DayOff), rows.Count(r => r.Day.Status == HrDayStatus.NoSchedule),
            rows.Count(r => r.Day.Status == HrDayStatus.Unknown), events.Sum(e => e.Events), missing,
            AvgArrival(rows.Select(r => r.Day)), late.Count == 0 ? null : (int)Math.Round(late.Average(r => r.Day.LateMinutes ?? 0)), compare,
            LateBetween(monday, day), LateBetween(monday.AddDays(-7), day.AddDays(-7)), passesByHour, attentionRows, attention.Count,
            divisions, locations, await SyncBriefAsync(rules, ct), ctx.SeesAll);
    }

    /// <summary>По графику на день: явку считают от них — все, кроме выходного, дней без графика и не работавших в компании.</summary>
    private static bool Scheduled(HrDayResult d) => d.Status is not (HrDayStatus.DayOff or HrDayStatus.NoSchedule or HrDayStatus.NotEmployed);

    /// <summary>Средний приход в рабочий день по графику (работа в выходной и без графика не в счёт).</summary>
    private static TimeOnly? AvgArrival(IEnumerable<HrDayResult> days)
    {
        var minutes = days.Where(d => d.Status == HrDayStatus.Present && d.DayKind == "W" && d.FirstIn is not null)
            .Select(d => d.FirstIn!.Value.TimeOfDay.TotalMinutes).ToList();
        return minutes.Count == 0 ? null : TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(Math.Round(minutes.Average())));
    }

    public async Task<HrSyncBrief> SyncBriefAsync(CancellationToken ct) => await SyncBriefAsync(await rulesSource.GetRulesAsync(ct), ct);

    private async Task<HrSyncBrief> SyncBriefAsync(HrRules rules, CancellationToken ct)
    {
        var state = await rulesSource.GetStateAsync(ct);
        var last = await db.HrSyncRuns.AsNoTracking().OrderByDescending(r => r.StartedAt).FirstOrDefaultAsync(ct);
        var lastOk = await db.HrSyncRuns.AsNoTracking().Where(r => r.Status == HrSyncStatus.Succeeded || r.Status == HrSyncStatus.Partial)
            .OrderByDescending(r => r.StartedAt).Select(r => r.FinishedAt).FirstOrDefaultAsync(ct);
        var coverage = await CoverageAsync(rules, ct);
        return new HrSyncBrief(state.Configured, state.Enabled, last?.Status == HrSyncStatus.Running, lastOk, last?.StartedAt, last?.Status, last?.Message,
            coverage.TracksThrough, coverage.TimesheetFrom, coverage.TimesheetTo);
    }

    // ---------- Сотрудники ----------

    public async Task<PageResult<HrEmployeeRow>> EmployeesAsync(HrContext ctx, HrEmployeeQuery q, CancellationToken ct)
    {
        Require(ctx);
        var query = Visible(ctx);
        if (q.DivisionId is { } division)
        {
            var ids = await SubtreeAsync(division, ct);
            query = query.Where(e => e.DivisionId != null && ids.Contains(e.DivisionId.Value));
        }

        query = q.Status switch
        {
            "dismissed" => query.Where(e => e.Status == HrEmployeeStatus.Dismissed),
            "not_hired" => query.Where(e => e.Status == HrEmployeeStatus.NotHired),
            "missing" => query.Where(e => e.MissingSince != null),
            "all" => query,
            _ => query.Where(e => e.Status == HrEmployeeStatus.Working),
        };
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            var lower = s.ToLower();
            query = long.TryParse(s, out var number)
                ? query.Where(e => e.VerifixId == number || (e.Phone ?? "").Contains(s))
                : query.Where(e => e.FullName.ToLower().Contains(lower) || (e.Phone ?? "").Contains(s));
        }

        var total = await query.CountAsync(ct);
        query = q.Sort switch
        {
            "hired" => query.OrderByDescending(e => e.HiringDate).ThenBy(e => e.FullName),
            "division" => query.OrderBy(e => e.DivisionId).ThenBy(e => e.FullName),
            _ => query.OrderBy(e => e.FullName),
        };
        var size = Math.Clamp(q.PageSize, 10, 200);
        var page = Math.Max(1, q.Page);
        var employees = await query.Skip((page - 1) * size).Take(size).AsNoTracking().ToListAsync(ct);
        return new PageResult<HrEmployeeRow>(await RowsAsync(ctx, employees, ct), total, page, size);
    }

    /// <summary>Карточка сотрудника за период. maxDays — предел периода: страницы и API — 62 дня, AI — до квартала (MaxReportDays).</summary>
    public async Task<HrEmployeeCard> EmployeeAsync(HrContext ctx, Guid id, DateOnly? from, DateOnly? to, CancellationToken ct, int maxDays = MaxPeriodDays)
    {
        Require(ctx);
        var employee = await Visible(ctx).AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct) ?? throw new FieldNotFoundException("Сотрудник не найден.");
        var rules = await rulesSource.GetRulesAsync(ct);
        var today = DateOnly.FromDateTime(Now(rules));
        // Уволенный — по умолчанию период заканчивается днём увольнения.
        var last = employee.DismissalDate is { } dismissed && dismissed < today ? dismissed : today;
        var (start, end) = Period(from ?? (to ?? last).AddDays(-13), to ?? last, Math.Min(maxDays, MaxReportDays));
        var period = await EvaluateAsync(ctx, start, end, null, [id], null, rules, ct, includeAll: true);
        var days = period.Rows.FirstOrDefault()?.Days ?? [];
        var names = await NamesAsync(ct);
        var events = await EventRowsAsync(VisibleEvents(ctx).Where(e => e.EmployeeId == id && e.LocalDate >= start && e.LocalDate <= end)
            .OrderBy(e => e.LocalTime).ThenBy(e => e.Id), names, MaxCardEvents, ct);
        var row = (await RowsAsync(ctx, [employee], ct))[0];
        var noteBy = employee.NoteUpdatedById is { } author
            ? await db.Users.AsNoTracking().Where(u => u.Id == author).Select(u => u.FullName).FirstOrDefaultAsync(ct)
            : null;
        var state = await rulesSource.GetStateAsync(ct);
        return new HrEmployeeCard(row, employee.MiddleName, employee.Email, Name(names.Divisions, employee.OrgUnitId, null), employee.Note, employee.NoteUpdatedAt,
            noteBy, employee.FirstSyncedAt, employee.LastSyncedAt, ctx.CanEditNotes, start, end, days.OrderByDescending(d => d.Date).ToList(), events,
            HrSummary.Of(days), state.Configured ? state.BaseUrl : null, await SyncBriefAsync(rules, ct));
    }

    /// <summary>Локальная заметка HR: единственное поле сотрудника, которое правится в OneBase. Синхронизация её не трогает.</summary>
    public async Task SaveNoteAsync(HrContext ctx, Guid id, string? note, CancellationToken ct)
    {
        Require(ctx);
        if (!ctx.CanEditNotes)
        {
            throw new FieldForbiddenException("Заметки ведёт HR.");
        }

        var text = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (text is { Length: > 2000 })
        {
            throw new FieldValidationException("Заметка — не длиннее 2000 символов.");
        }

        var now = time.GetUtcNow();
        var changed = await db.HrEmployees.Where(e => e.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.Note, text).SetProperty(e => e.NoteUpdatedAt, now).SetProperty(e => e.NoteUpdatedById, ctx.UserId), ct);
        if (changed == 0)
        {
            throw new FieldNotFoundException("Сотрудник не найден.");
        }
    }

    public async Task<IReadOnlyList<HrDivisionView>> DivisionsAsync(HrContext ctx, CancellationToken ct)
    {
        if (!ctx.HasAccess && !ctx.ManagesIntegration)
        {
            throw new FieldForbiddenException("Нет доступа к HR.");
        }

        var counts = await db.HrEmployees.AsNoTracking().Where(e => e.Status == HrEmployeeStatus.Working && e.DivisionId != null)
            .GroupBy(e => e.DivisionId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, ct);
        var all = await db.HrReferences.AsNoTracking().Where(r => r.Kind == HrReferenceKinds.Division).OrderBy(r => r.Name).ToListAsync(ct);
        return all.Where(d => ctx.SeesAll || ctx.ManagesIntegration || ctx.CanSee(d.VerifixId))
            .Select(d => new HrDivisionView(d.VerifixId, d.Name, d.ParentId, d.DepartmentCode, d.MissingSince != null, counts.GetValueOrDefault(d.VerifixId)))
            .ToList();
    }

    // ---------- Посещаемость ----------

    /// <summary>День: каждый видимый сотрудник с итогом дня. status — present | ontime | late | absent | leave | expected | unknown | dayoff | noschedule.</summary>
    public async Task<IReadOnlyList<HrDayRow>> DayAsync(HrContext ctx, DateOnly? date, long? divisionId, string? status, string? search, CancellationToken ct)
    {
        Require(ctx);
        var rules = await rulesSource.GetRulesAsync(ct);
        var day = date ?? DateOnly.FromDateTime(Now(rules));
        var period = await EvaluateAsync(ctx, day, day, Divisions(divisionId), null, search, rules, ct);
        var names = await NamesAsync(ct);
        return period.Rows
            .Select(r => new HrDayRow(r.Employee.Id, r.Employee.FullName, Name(names.Divisions, r.Employee.DivisionId, null), Name(names.Jobs, r.Employee.JobId, null), r.Days[0]))
            .Where(r => r.Day.Status != HrDayStatus.NotEmployed && Matches(r.Day, status))
            .OrderBy(r => Order(r.Day)).ThenBy(r => r.Employee)
            .ToList();
    }

    public async Task<PageResult<HrEventRow>> EventsAsync(HrContext ctx, DateOnly? from, DateOnly? to, Guid? employeeId, long? divisionId, int page, int pageSize, CancellationToken ct)
    {
        Require(ctx);
        var rules = await rulesSource.GetRulesAsync(ct);
        var today = DateOnly.FromDateTime(Now(rules));
        var (start, end) = Period(from ?? today, to ?? today);
        var query = VisibleEvents(ctx).Where(e => e.LocalDate >= start && e.LocalDate <= end);
        if (employeeId is { } employee)
        {
            query = query.Where(e => e.EmployeeId == employee);
        }

        if (divisionId is { } division)
        {
            var ids = await SubtreeAsync(division, ct);
            query = query.Where(e => e.DivisionId != null && ids.Contains(e.DivisionId.Value));
        }

        var size = Math.Clamp(pageSize, 20, 500);
        var number = Math.Max(1, page);
        var total = await query.CountAsync(ct);
        var names = await NamesAsync(ct);
        var rows = await EventRowsAsync(query.OrderByDescending(e => e.LocalTime).ThenByDescending(e => e.Id).Skip((number - 1) * size), names, size, ct);
        return new PageResult<HrEventRow>(rows, total, number, size);
    }

    /// <summary>
    /// Опоздания, ранние уходы, отсутствия и дни без ухода за период. kind — late | early | absent | noexit | null (все). maxDays — предел
    /// периода: страницы и API — 62 дня, AI — до квартала (MaxReportDays).
    /// </summary>
    public async Task<IReadOnlyList<HrDeviation>> DeviationsAsync(HrContext ctx, DateOnly? from, DateOnly? to, string? kind, long? divisionId, Guid? employeeId, CancellationToken ct,
        int maxDays = MaxPeriodDays)
    {
        Require(ctx);
        var rules = await rulesSource.GetRulesAsync(ct);
        var today = DateOnly.FromDateTime(Now(rules));
        var (start, end) = Period(from ?? today.AddDays(-6), to ?? today, Math.Min(maxDays, MaxReportDays));
        var period = await EvaluateAsync(ctx, start, end, Divisions(divisionId), Only(employeeId), null, rules, ct);
        return DeviationsOf(period, await NamesAsync(ct), kind);
    }

    private static List<HrDeviation> DeviationsOf(PeriodResult period, Names names, string? kind)
    {
        var list = new List<HrDeviation>();
        foreach (var row in period.Rows)
        {
            var division = Name(names.Divisions, row.Employee.DivisionId, null);
            foreach (var d in row.Days)
            {
                void Add(string k, int? minutes) =>
                    list.Add(new HrDeviation(d.Date, row.Employee.Id, row.Employee.FullName, division, k, minutes, d.PlanBegin, d.PlanEnd, d.FirstIn, d.LastOut, d.Reason));

                if (d.Late && kind is null or "late")
                {
                    Add("late", d.LateMinutes);
                }

                if (d.EarlyLeave && kind is null or "early")
                {
                    Add("early", d.EarlyLeaveMinutes);
                }

                if (d.Status == HrDayStatus.Absent && kind is null or "absent")
                {
                    Add("absent", d.PlanMinutes);
                }

                if (d.NoExit && kind is null or "noexit")
                {
                    Add("noexit", null);
                }
            }
        }

        return list.OrderByDescending(x => x.Date).ThenBy(x => x.Employee).ToList();
    }

    // ---------- Общее ----------

    private sealed record EmployeeLite(Guid Id, long VerifixId, string FullName, string Status, DateOnly? HiringDate, DateOnly? DismissalDate, long? DivisionId, long? JobId);

    private sealed record PeriodRow(EmployeeLite Employee, IReadOnlyList<HrDayResult> Days);

    private sealed record PeriodResult(DateOnly From, DateOnly To, IReadOnlyList<PeriodRow> Rows);

    private sealed record Names(
        IReadOnlyDictionary<long, string> Divisions,
        IReadOnlyDictionary<long, string> Jobs,
        IReadOnlyDictionary<long, string> Schedules,
        IReadOnlyDictionary<long, string> Locations,
        IReadOnlyDictionary<long, (string Name, string? Code)> TimeKinds);

    /// <summary>Одно подразделение (с дочерними) или все.</summary>
    private static IReadOnlyCollection<long>? Divisions(long? divisionId) => divisionId is { } id ? [id] : null;

    private static void Require(HrContext ctx)
    {
        if (!ctx.HasAccess)
        {
            throw new FieldForbiddenException("Нет доступа к HR.");
        }
    }

    private DateTime Now(HrRules rules) => DateTime.SpecifyKind(TimeZoneInfo.ConvertTime(time.GetUtcNow(), rules.Zone).DateTime, DateTimeKind.Unspecified);

    private static (DateOnly From, DateOnly To) Period(DateOnly from, DateOnly to, int maxDays = MaxPeriodDays)
    {
        if (to < from)
        {
            (from, to) = (to, from);
        }

        if (to.DayNumber - from.DayNumber + 1 > maxDays)
        {
            throw new FieldValidationException($"Период — не больше {maxDays} дней.");
        }

        return (from, to);
    }

    private IQueryable<HrEmployee> Visible(HrContext ctx)
    {
        var query = db.HrEmployees.AsNoTracking();
        if (ctx.SeesAll)
        {
            return query;
        }

        var divisions = (ctx.Divisions ?? new HashSet<long>()).ToList();
        return query.Where(e => e.DivisionId != null && divisions.Contains(e.DivisionId.Value));
    }

    private IQueryable<HrAttendanceEvent> VisibleEvents(HrContext ctx)
    {
        var query = db.HrAttendanceEvents.AsNoTracking();
        if (ctx.SeesAll)
        {
            return query;
        }

        var visible = Visible(ctx).Select(e => (Guid?)e.Id);
        return query.Where(e => visible.Contains(e.EmployeeId));
    }

    /// <summary>Подразделение с дочерними.</summary>
    private Task<List<long>> SubtreeAsync(long division, CancellationToken ct) => SubtreeAsync([division], ct);

    /// <summary>Несколько подразделений с дочерними (без повторов).</summary>
    private async Task<List<long>> SubtreeAsync(IEnumerable<long> divisions, CancellationToken ct)
    {
        var all = await db.HrReferences.AsNoTracking().Where(r => r.Kind == HrReferenceKinds.Division).Select(r => new { r.VerifixId, r.ParentId }).ToListAsync(ct);
        var children = all.Where(d => d.ParentId != null).ToLookup(d => d.ParentId!.Value, d => d.VerifixId);
        var result = new List<long>();
        var seen = new HashSet<long>();
        var queue = new Queue<long>(divisions);
        while (queue.TryDequeue(out var id))
        {
            if (seen.Add(id))
            {
                result.Add(id);
                foreach (var child in children[id])
                {
                    queue.Enqueue(child);
                }
            }
        }

        return result;
    }

    private async Task<Names> NamesAsync(CancellationToken ct)
    {
        var refs = await db.HrReferences.AsNoTracking().Select(r => new { r.Kind, r.VerifixId, r.Name, r.Code }).ToListAsync(ct);
        IReadOnlyDictionary<long, string> Of(string kind) => refs.Where(r => r.Kind == kind).ToDictionary(r => r.VerifixId, r => r.Name);
        return new Names(Of(HrReferenceKinds.Division), Of(HrReferenceKinds.Job), Of(HrReferenceKinds.Schedule), Of(HrReferenceKinds.Location),
            refs.Where(r => r.Kind == HrReferenceKinds.TimeKind).ToDictionary(r => r.VerifixId, r => (r.Name, r.Code)));
    }

    private static string? Name(IReadOnlyDictionary<long, string> names, long? id, string? fallback) =>
        id is { } v ? names.TryGetValue(v, out var name) ? name : $"#{v}" : fallback;

    private async Task<HrCoverage> CoverageAsync(HrRules rules, CancellationToken ct)
    {
        var states = await db.HrSyncStates.AsNoTracking().Where(s => s.Entity == "tracks" || s.Entity == "timesheet").ToListAsync(ct);
        var tracks = states.FirstOrDefault(s => s.Entity == "tracks");
        var sheet = states.FirstOrDefault(s => s.Entity == "timesheet");
        DateTime? Local(DateTimeOffset? at) => at is { } a ? DateTime.SpecifyKind(TimeZoneInfo.ConvertTime(a, rules.Zone).DateTime, DateTimeKind.Unspecified) : null;
        var sheetTo = Local(sheet?.CoveredThrough);
        return new HrCoverage(tracks?.CoveredFrom is null ? null : Local(tracks.CoveredThrough), sheet?.CoveredFrom,
            sheetTo is { } t ? DateOnly.FromDateTime(t) : null);
    }

    /// <summary>
    /// Итоги дней за период по видимым сотрудникам: табель Verifix + сводка отметок → HrAttendance. includeAll — и уволенные, и не принятые
    /// (для карточки); иначе — только те, кто работал в периоде.
    /// </summary>
    private async Task<PeriodResult> EvaluateAsync(HrContext ctx, DateOnly from, DateOnly to, IReadOnlyCollection<long>? divisions, IReadOnlyCollection<Guid>? only, string? search,
        HrRules rules, CancellationToken ct, bool includeAll = false)
    {
        var query = Visible(ctx);
        if (only is not null)
        {
            var onlyIds = only.ToList();
            query = query.Where(e => onlyIds.Contains(e.Id));
        }

        if (divisions is { Count: > 0 })
        {
            var ids = await SubtreeAsync(divisions, ct);
            query = query.Where(e => e.DivisionId != null && ids.Contains(e.DivisionId.Value));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lower = search.Trim().ToLower();
            query = query.Where(e => e.FullName.ToLower().Contains(lower));
        }

        if (!includeAll)
        {
            query = query.Where(e => e.Status != HrEmployeeStatus.NotHired && (e.HiringDate == null || e.HiringDate <= to)
                                     && (e.Status == HrEmployeeStatus.Working || (e.DismissalDate != null && e.DismissalDate >= from)));
        }

        var employees = await query.Select(e => new EmployeeLite(e.Id, e.VerifixId, e.FullName, e.Status, e.HiringDate, e.DismissalDate, e.DivisionId, e.JobId))
            .ToListAsync(ct);
        var employeeIds = employees.Select(e => e.Id).ToList();

        var sheets = await db.HrTimesheetDays.AsNoTracking()
            .Where(d => d.EmployeeId != null && employeeIds.Contains(d.EmployeeId.Value) && d.Date >= from && d.Date <= to && d.MissingSince == null)
            .ToListAsync(ct);
        var events = await db.HrAttendanceEvents.AsNoTracking()
            .Where(e => e.EmployeeId != null && employeeIds.Contains(e.EmployeeId.Value) && e.LocalDate >= from && e.LocalDate <= to && e.MissingSince == null)
            .GroupBy(e => new { e.EmployeeId, e.LocalDate })
            .Select(g => new
            {
                g.Key.EmployeeId,
                g.Key.LocalDate,
                Count = g.Count(),
                FirstIn = g.Min(e => e.TrackType == "I" ? (DateTime?)e.LocalTime : null),
                LastOut = g.Max(e => e.TrackType == "O" ? (DateTime?)e.LocalTime : null),
            })
            .ToListAsync(ct);

        var kinds = await db.HrReferences.AsNoTracking().Where(r => r.Kind == HrReferenceKinds.TimeKind)
            .ToDictionaryAsync(r => r.VerifixId, r => (r.Name, r.Code), ct);
        var coverage = await CoverageAsync(rules, ct);
        var now = Now(rules);

        // Повторный приём в периоде — два staff_id на один день: берётся рабочий день, затем свежий.
        var sheetIndex = sheets.GroupBy(d => (d.EmployeeId!.Value, d.Date))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.DayKind == "W").ThenByDescending(d => d.SyncedAt).First());
        var eventIndex = events.ToDictionary(e => (e.EmployeeId!.Value, e.LocalDate), e => new HrEventSummary(e.Count, e.FirstIn, e.LastOut));

        var rows = new List<PeriodRow>(employees.Count);
        foreach (var e in employees)
        {
            var facts = new HrEmployeeFacts(e.Status, e.HiringDate, e.DismissalDate);
            var days = new List<HrDayResult>(to.DayNumber - from.DayNumber + 1);
            for (var date = from; date <= to; date = date.AddDays(1))
            {
                var sheet = sheetIndex.TryGetValue((e.Id, date), out var s) ? ToFacts(s, kinds) : null;
                var summary = eventIndex.TryGetValue((e.Id, date), out var ev) ? ev : HrEventSummary.None;
                days.Add(HrAttendance.Evaluate(date, facts, sheet, summary, coverage, now, rules));
            }

            rows.Add(new PeriodRow(e, days));
        }

        return new PeriodResult(from, to, rows);
    }

    private sealed record FactJson(long K, long V);

    private static HrTimesheetFacts ToFacts(HrTimesheetDay d, IReadOnlyDictionary<long, (string Name, string? Code)> kinds)
    {
        List<FactJson> raw;
        try
        {
            raw = JsonSerializer.Deserialize<List<FactJson>>(d.FactsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        }
        catch (JsonException)
        {
            raw = [];
        }

        var facts = raw.Select(f =>
        {
            var (name, code) = kinds.TryGetValue(f.K, out var kind) ? kind : ($"вид времени #{f.K}", null);
            return new HrFact(HrTimeKinds.Classify(code, name), code, name, f.V);
        }).ToList();
        return new HrTimesheetFacts(d.DayKind, d.PlanBegin, d.PlanEnd, d.BreakBegin, d.BreakEnd, d.PlanMinutes, d.FirstIn, d.LastOut, facts);
    }

    private async Task<IReadOnlyList<HrEmployeeRow>> RowsAsync(HrContext ctx, IReadOnlyList<HrEmployee> employees, CancellationToken ct)
    {
        if (employees.Count == 0)
        {
            return [];
        }

        var names = await NamesAsync(ct);
        var rules = await rulesSource.GetRulesAsync(ct);
        var today = DateOnly.FromDateTime(Now(rules));
        var ids = employees.Select(e => e.Id).ToList();
        var last = await db.HrAttendanceEvents.AsNoTracking().Where(e => e.EmployeeId != null && ids.Contains(e.EmployeeId.Value) && e.MissingSince == null)
            .GroupBy(e => e.EmployeeId!.Value).Select(g => new { g.Key, Last = g.Max(e => e.LocalTime) }).ToDictionaryAsync(g => g.Key, g => g.Last, ct);
        var todayRows = (await EvaluateAsync(ctx, today, today, null, ids, null, rules, ct, includeAll: true)).Rows
            .ToDictionary(r => r.Employee.Id, r => r.Days[0]);

        return employees.Select(e =>
        {
            var day = todayRows.GetValueOrDefault(e.Id);
            return new HrEmployeeRow(e.Id, e.VerifixId, e.FullName, e.Phone, e.Status, e.HiringDate, e.DismissalDate, e.DivisionId,
                Name(names.Divisions, e.DivisionId, null), Name(names.Jobs, e.JobId, null), Name(names.Schedules, e.ScheduleId, null), e.MissingSince,
                last.TryGetValue(e.Id, out var at) ? at : null, day?.Status, day?.Late ?? false);
        }).ToList();
    }

    private async Task<IReadOnlyList<HrEventRow>> EventRowsAsync(IQueryable<HrAttendanceEvent> query, Names names, int take, CancellationToken ct)
    {
        var rows = await query.Take(take).ToListAsync(ct);
        var ids = rows.Where(r => r.EmployeeId != null).Select(r => r.EmployeeId!.Value).Distinct().ToList();
        var people = await db.HrEmployees.AsNoTracking().Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.FullName, ct);
        return rows.Select(r => new HrEventRow(r.Id, r.VerifixTrackId, r.EmployeeId, r.EmployeeId is { } id && people.TryGetValue(id, out var name) ? name : null,
            Name(names.Divisions, r.DivisionId, null), Name(names.Locations, r.LocationId, null), r.TrackType, r.MarkType, r.RawTime, r.LocalTime, r.OccurredAt,
            r.ImportedAt, r.MissingSince)).ToList();
    }

    private static bool Matches(HrDayResult d, string? status) => status switch
    {
        "present" => d.Status == HrDayStatus.Present,
        "ontime" => d.Status == HrDayStatus.Present && !d.Late,
        "late" => d.Late,
        "absent" => d.Status == HrDayStatus.Absent,
        "leave" => d.Status == HrDayStatus.OnLeave,
        "expected" => d.Status == HrDayStatus.Expected,
        "unknown" => d.Status == HrDayStatus.Unknown,
        "dayoff" => d.Status == HrDayStatus.DayOff,
        "noschedule" => d.Status == HrDayStatus.NoSchedule,
        _ => true,
    };

    private static int Order(HrDayResult d) => d.Status switch
    {
        HrDayStatus.Absent => 0,
        HrDayStatus.Present when d.Late => 1,
        HrDayStatus.Unknown => 2,
        HrDayStatus.Present => 3,
        HrDayStatus.Expected => 4,
        HrDayStatus.OnLeave => 5,
        _ => 6,
    };
}
