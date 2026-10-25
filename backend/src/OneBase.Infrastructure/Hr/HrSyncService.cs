using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.Domain.Hr;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.Hr;

/// <summary>Шаг синхронизации для журнала.</summary>
public sealed class HrSyncStep
{
    public required string Step { get; init; }
    public string Status { get; set; } = "ok";
    public int Rows { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Errors { get; set; }
    public int Pages { get; set; }
    public string? Message { get; set; }
}

public sealed record HrSyncOutcome(IReadOnlyList<HrSyncStep> Steps, DateOnly PeriodFrom, DateOnly PeriodTo, bool AuthFailed);

/// <summary>
/// Одна синхронизация Verifix → OneBase. Порядок: справочники (подразделения, должности, графики, локации, виды времени), сотрудники
/// (полная выгрузка — по ней видно, кто пропал), отметки по дням (первая загрузка — HistoryDays дней, дальше — от последней полной загрузки
/// с перекрытием OverlapDays: терминал мог передать отметки позже), табель за то же окно (не меньше 7 дней: документы задним числом).
/// Сопоставление — только по ID Verifix: повторная загрузка обновляет строку, а не добавляет. Ничего не удаляется: пропавшее из полной
/// выгрузки получает MissingSince. Поля OneBase (заметка HR, привязка подразделения к отделу) не меняются.
/// Ошибка одного шага не останавливает остальные (кроме отказа в авторизации); итог шага — в журнал.
/// </summary>
public sealed class HrSyncService(OneBaseDbContext db, VerifixClient client, VerifixSettingsStore settings, TimeProvider time, ILogger<HrSyncService> logger)
{
    public const string TracksState = "tracks";
    public const string TimesheetState = "timesheet";
    public const string EmployeesState = "employees";

    private const int EmployeePage = 500;
    private const int TrackPage = 1000;
    private const int TimesheetPage = 100;
    private const int ReferencePage = 500;

    private Guid _runId;
    private DateTimeOffset _started;
    private Dictionary<long, Guid> _employees = [];

    public async Task<HrSyncOutcome> RunAsync(Guid runId, CancellationToken ct)
    {
        _runId = runId;
        var connection = await settings.GetAsync(ct);
        var zone = VerifixFormats.Zone(connection.Data.TimeZone);
        _started = time.GetUtcNow();
        var nowLocal = VerifixFormats.ToLocal(_started, zone);
        var today = DateOnly.FromDateTime(nowLocal);
        var historyStart = today.AddDays(-(Math.Clamp(connection.Data.HistoryDays, 1, VerifixSettingsStore.MaxHistoryDays) - 1));
        var overlap = Math.Clamp(connection.Data.OverlapDays, 1, 14);

        var steps = new List<HrSyncStep>();
        var authFailed = false;

        async Task Run(string name, Func<HrSyncStep, Task> body)
        {
            var step = new HrSyncStep { Step = name };
            steps.Add(step);
            if (authFailed)
            {
                step.Status = "skipped";
                step.Message = "пропущено: Verifix не принял авторизацию";
                return;
            }

            try
            {
                await body(step);
            }
            catch (VerifixApiException ex)
            {
                step.Status = "failed";
                step.Errors++;
                step.Message = VerifixClient.Human(ex);
                authFailed |= ex.Kind is VerifixErrorKind.Auth or VerifixErrorKind.NotConfigured;
                logger.LogWarning("Verifix: шаг {Step} не выполнен — {Message}", name, VerifixLog.Safe(ex.Message));
                await SaveStateErrorAsync(name, step.Message, ct);
            }

            await HeartbeatAsync(ct);
        }

        foreach (var kind in HrReferenceKinds.All)
        {
            await Run(kind, step => ReferencesAsync(connection, kind, step, ct));
        }

        await Run(EmployeesState, step => EmployeesAsync(connection, step, ct));
        _employees = await db.HrEmployees.AsNoTracking().ToDictionaryAsync(e => e.VerifixId, e => e.Id, ct);

        var tracksFrom = await WindowStartAsync(TracksState, historyStart, overlap, zone, ct);
        await Run(TracksState, step => TracksAsync(connection, tracksFrom, today, nowLocal, historyStart, zone, step, ct));

        var sheetFrom = await WindowStartAsync(TimesheetState, historyStart, Math.Max(overlap, 7), zone, ct);
        await Run(TimesheetState, step => TimesheetAsync(connection, sheetFrom, today, historyStart, step, ct));

        await Run("links", step => LinkAsync(step, ct));
        return new HrSyncOutcome(steps, tracksFrom < sheetFrom ? tracksFrom : sheetFrom, today, authFailed);
    }

    // ---------- Справочники ----------

    private sealed record RefItem(long Id, string Name, string? Code, long? ParentId, string? State, string? Address, DateOnly? Closed);

    private async Task ReferencesAsync(VerifixConnection connection, string kind, HrSyncStep step, CancellationToken ct)
    {
        var (method, body) = kind switch
        {
            HrReferenceKinds.Division => ("core/division$list", (object)new { division_ids = Array.Empty<long>() }),
            HrReferenceKinds.Job => ("core/job$list", new { job_ids = Array.Empty<long>() }),
            HrReferenceKinds.Schedule => ("core/schedule$list", new { schedule_ids = Array.Empty<long>() }),
            HrReferenceKinds.Location => ("core/location$list", new { location_ids = Array.Empty<long>() }),
            _ => ("core/time_kind$list", new { time_kind_ids = Array.Empty<long>() }),
        };

        async Task Save(IEnumerable<RefItem> items)
        {
            step.Pages++;
            var list = items.Where(i => i.Id > 0).GroupBy(i => i.Id).Select(g => g.Last()).ToList();
            var ids = list.Select(i => i.Id).ToList();
            var existing = await db.HrReferences.Where(r => r.Kind == kind && ids.Contains(r.VerifixId)).ToDictionaryAsync(r => r.VerifixId, ct);
            foreach (var item in list)
            {
                step.Rows++;
                var name = string.IsNullOrWhiteSpace(item.Name) ? $"#{item.Id}" : Trim(item.Name, 300)!;
                if (!existing.TryGetValue(item.Id, out var row))
                {
                    db.HrReferences.Add(new HrReference
                    {
                        Kind = kind, VerifixId = item.Id, Name = name, Code = Trim(item.Code, 50), ParentId = item.ParentId, State = Trim(item.State, 4),
                        Address = Trim(item.Address, 500), ClosedDate = item.Closed, LastSyncedAt = _started,
                    });
                    step.Created++;
                    continue;
                }

                var changed = row.Name != name || row.Code != Trim(item.Code, 50) || row.ParentId != item.ParentId || row.State != Trim(item.State, 4)
                              || row.Address != Trim(item.Address, 500) || row.ClosedDate != item.Closed || row.MissingSince != null;
                // Только поля Verifix; DepartmentCode — привязка OneBase — не трогается.
                row.Name = name;
                row.Code = Trim(item.Code, 50);
                row.ParentId = item.ParentId;
                row.State = Trim(item.State, 4);
                row.Address = Trim(item.Address, 500);
                row.ClosedDate = item.Closed;
                row.LastSyncedAt = _started;
                row.MissingSince = null;
                if (changed)
                {
                    step.Updated++;
                }
                else
                {
                    step.Skipped++;
                }
            }

            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
        }

        switch (kind)
        {
            case HrReferenceKinds.Division:
                await client.ListAsync<VerifixDivisionDto>(connection, method, body, ReferencePage,
                    page => Save(page.Select(d => new RefItem(d.DivisionId, d.Name ?? "", null, d.ParentId, d.State, null, VerifixFormats.ParseDate(d.ClosedDate)))), ct);
                break;
            case HrReferenceKinds.Job:
                await client.ListAsync<VerifixJobDto>(connection, method, body, ReferencePage,
                    page => Save(page.Select(d => new RefItem(d.JobId, d.Name ?? "", d.Code, null, d.State, null, null))), ct);
                break;
            case HrReferenceKinds.Schedule:
                await client.ListAsync<VerifixScheduleDto>(connection, method, body, ReferencePage,
                    page => Save(page.Select(d => new RefItem(d.ScheduleId, d.Name ?? "", d.Kind, null, d.State, null, null))), ct);
                break;
            case HrReferenceKinds.Location:
                await client.ListAsync<VerifixLocationDto>(connection, method, body, ReferencePage,
                    page => Save(page.Select(d => new RefItem(d.LocationId, d.Name ?? "", null, null, d.State, d.Address, null))), ct);
                break;
            default:
                await client.ListAsync<VerifixTimeKindDto>(connection, method, body, ReferencePage,
                    page => Save(page.Select(d => new RefItem(d.TimeKindId, d.Name ?? "", d.LetterCode, null, null, null, null))), ct);
                break;
        }

        // Выгрузка полная — чего в ней не было, помечается (не удаляется).
        await db.HrReferences.Where(r => r.Kind == kind && r.LastSyncedAt < _started && r.MissingSince == null)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.MissingSince, _started), ct);
        await SaveStateAsync(kind, step.Rows, null, null, ct);
    }

    // ---------- Сотрудники ----------

    private async Task EmployeesAsync(VerifixConnection connection, HrSyncStep step, CancellationToken ct)
    {
        // Без фильтров — все сотрудники, включая уволенных и не принятых (так в документации employee$list).
        var body = new { employee_ids = Array.Empty<long>(), statuses = Array.Empty<string>() };
        await client.ListAsync<VerifixEmployeeDto>(connection, "core/employee$list", body, EmployeePage, async page =>
        {
            step.Pages++;
            var list = page.Where(e => e.EmployeeId > 0).GroupBy(e => e.EmployeeId).Select(g => g.Last()).ToList();
            step.Skipped += page.Count - list.Count;
            var ids = list.Select(e => e.EmployeeId).ToList();
            var existing = await db.HrEmployees.Where(e => ids.Contains(e.VerifixId)).ToDictionaryAsync(e => e.VerifixId, ct);
            var unchanged = new List<long>();
            foreach (var dto in list)
            {
                step.Rows++;
                var hash = Hash(dto);
                if (!existing.TryGetValue(dto.EmployeeId, out var row))
                {
                    row = new HrEmployee { VerifixId = dto.EmployeeId, FirstName = "", FullName = "", Status = "", FirstSyncedAt = _started };
                    Apply(row, dto, hash);
                    db.HrEmployees.Add(row);
                    step.Created++;
                }
                else if (row.SourceHash != hash || row.MissingSince != null)
                {
                    Apply(row, dto, hash);
                    step.Updated++;
                }
                else
                {
                    unchanged.Add(dto.EmployeeId);
                    step.Skipped++;
                    db.Entry(row).State = EntityState.Detached;
                }
            }

            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            if (unchanged.Count > 0)
            {
                await db.HrEmployees.Where(e => unchanged.Contains(e.VerifixId)).ExecuteUpdateAsync(s => s.SetProperty(e => e.LastSyncedAt, _started), ct);
            }

            await HeartbeatAsync(ct);
        }, ct);

        // Полная выгрузка прошла: кого в ней нет — «нет в Verifix с …». Запись и история посещений остаются.
        var missing = await db.HrEmployees.Where(e => e.LastSyncedAt < _started && e.MissingSince == null)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.MissingSince, _started), ct);
        if (missing > 0)
        {
            step.Message = $"не пришли из Verifix: {missing} — отмечены, не удалены";
        }

        await SaveStateAsync(EmployeesState, step.Rows, null, null, ct);
    }

    private void Apply(HrEmployee row, VerifixEmployeeDto dto, string hash)
    {
        // Только поля Verifix. Note и прочие поля OneBase не трогаются.
        var first = Trim(dto.FirstName, 200);
        var last = Trim(dto.LastName, 200);
        var middle = Trim(dto.MiddleName, 200);
        row.FirstName = first ?? last ?? $"Сотрудник {dto.EmployeeId}";
        row.LastName = last;
        row.MiddleName = middle;
        row.FullName = Trim(string.Join(' ', new[] { last, first, middle }.Where(x => !string.IsNullOrWhiteSpace(x))), 600) is { Length: > 0 } full
            ? full
            : $"Сотрудник {dto.EmployeeId}";
        row.Phone = Trim(dto.PhoneNumber, 50);
        row.Email = Trim(dto.Email, 200);
        row.Status = Trim(dto.Status, 4) ?? HrEmployeeStatus.Working;
        row.State = Trim(dto.State, 4);
        row.HiringDate = VerifixFormats.ParseDate(dto.HiringDate);
        row.DismissalDate = VerifixFormats.ParseDate(dto.DismissalDate);
        row.JobId = dto.JobId;
        row.DivisionId = dto.DivisionId;
        row.ScheduleId = dto.ScheduleId;
        row.OrgUnitId = dto.OrgUnitId;
        row.SourceHash = hash;
        row.LastSyncedAt = _started;
        row.MissingSince = null;
    }

    private static string Hash(VerifixEmployeeDto d) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', d.FirstName, d.LastName, d.MiddleName, d.PhoneNumber, d.Email, d.State, d.Status,
            d.HiringDate, d.DismissalDate, d.JobId, d.DivisionId, d.ScheduleId, d.OrgUnitId))))[..32];

    // ---------- Отметки ----------

    /// <summary>
    /// Начало окна: первая загрузка или увеличенная глубина истории — с historyStart; иначе — от даты последней полной загрузки назад на overlap дней.
    /// </summary>
    private async Task<DateOnly> WindowStartAsync(string entity, DateOnly historyStart, int overlap, TimeZoneInfo zone, CancellationToken ct)
    {
        var state = await db.HrSyncStates.AsNoTracking().FirstOrDefaultAsync(s => s.Entity == entity, ct);
        if (state?.CoveredFrom is not { } from || state.CoveredThrough is not { } through || from > historyStart)
        {
            return historyStart;
        }

        var start = DateOnly.FromDateTime(VerifixFormats.ToLocal(through, zone)).AddDays(-(overlap - 1));
        return start < historyStart ? historyStart : start;
    }

    /// <summary>
    /// Отметки по одному дню: так полнота загрузки точна (день перечитан целиком — пропавшие из Verifix помечаются) и ошибка не портит другие дни.
    /// Дни идут по порядку; на первом сбое шаг останавливается, CoveredThrough — до последнего целиком загруженного дня.
    /// </summary>
    private async Task TracksAsync(VerifixConnection connection, DateOnly from, DateOnly today, DateTime nowLocal, DateOnly historyStart, TimeZoneInfo zone,
        HrSyncStep step, CancellationToken ct)
    {
        for (var day = from; day <= today; day = day.AddDays(1))
        {
            var begin = day.ToDateTime(TimeOnly.MinValue);
            var end = day == today ? nowLocal : day.ToDateTime(new TimeOnly(23, 59, 59));
            var body = new { begin_datetime = VerifixFormats.DateTime(begin), end_datetime = VerifixFormats.DateTime(end) };
            await client.ListAsync<VerifixTrackDto>(connection, "core/track$list", body, TrackPage, page => SaveTracksAsync(page, zone, step, ct), ct);

            // День перечитан целиком: отметки этого дня (до момента запроса), которых в ответе не было, — «нет в Verifix».
            var missing = await db.HrAttendanceEvents.Where(e => e.LocalDate == day && e.LocalTime <= end && e.LastSeenAt < _started && e.MissingSince == null)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.MissingSince, _started), ct);
            if (missing > 0)
            {
                step.Message = $"пропали из Verifix: {missing} отметок — исключены из расчёта, не удалены";
            }

            var covered = day == today ? _started : VerifixFormats.ToUtc(day.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);
            await SaveStateAsync(TracksState, step.Rows, covered, day == from ? from : null, ct, historyStart);
            await HeartbeatAsync(ct);
        }
    }

    private async Task SaveTracksAsync(IReadOnlyList<VerifixTrackDto> page, TimeZoneInfo zone, HrSyncStep step, CancellationToken ct)
    {
        step.Pages++;
        var list = page.Where(t => t.TrackId > 0).GroupBy(t => t.TrackId).Select(g => g.Last()).ToList();
        step.Skipped += page.Count - list.Count;
        var ids = list.Select(t => t.TrackId).ToList();
        var existing = await db.HrAttendanceEvents.Where(e => ids.Contains(e.VerifixTrackId)).ToDictionaryAsync(e => e.VerifixTrackId, ct);
        var unchanged = new List<long>();
        foreach (var dto in list)
        {
            step.Rows++;
            if (VerifixFormats.ParseLocal(dto.TrackDatetime) is not { } local)
            {
                step.Skipped++;
                step.Errors++;
                continue;
            }

            var employeeId = _employees.TryGetValue(dto.EmployeeId, out var id) ? id : (Guid?)null;
            var raw = Trim(dto.TrackDatetime, 40)!;
            var type = Trim(dto.TrackType, 4) ?? "";
            var mark = Trim(dto.MarkType, 4);
            if (!existing.TryGetValue(dto.TrackId, out var row))
            {
                db.HrAttendanceEvents.Add(new HrAttendanceEvent
                {
                    VerifixTrackId = dto.TrackId, VerifixEmployeeId = dto.EmployeeId, EmployeeId = employeeId, StaffId = dto.StaffId, DivisionId = dto.DivisionId,
                    LocationId = dto.LocationId, ScheduleId = dto.ScheduleId, TrackType = type, MarkType = mark, RawTime = raw, LocalTime = local,
                    LocalDate = DateOnly.FromDateTime(local), OccurredAt = VerifixFormats.ToUtc(local, zone), ImportedAt = _started, LastSeenAt = _started,
                });
                step.Created++;
                continue;
            }

            // Вид отметки в Verifix — «на момент отправки запроса» и может измениться: обновляем ту же строку.
            var changed = row.VerifixEmployeeId != dto.EmployeeId || row.TrackType != type || row.MarkType != mark || row.RawTime != raw
                          || row.LocationId != dto.LocationId || row.DivisionId != dto.DivisionId || row.ScheduleId != dto.ScheduleId
                          || row.StaffId != dto.StaffId || row.MissingSince != null || (row.EmployeeId is null && employeeId is not null);
            if (!changed)
            {
                unchanged.Add(dto.TrackId);
                step.Skipped++;
                db.Entry(row).State = EntityState.Detached;
                continue;
            }

            row.VerifixEmployeeId = dto.EmployeeId;
            row.EmployeeId = employeeId ?? row.EmployeeId;
            row.StaffId = dto.StaffId;
            row.DivisionId = dto.DivisionId;
            row.LocationId = dto.LocationId;
            row.ScheduleId = dto.ScheduleId;
            row.TrackType = type;
            row.MarkType = mark;
            row.RawTime = raw;
            row.LocalTime = local;
            row.LocalDate = DateOnly.FromDateTime(local);
            row.OccurredAt = VerifixFormats.ToUtc(local, zone);
            row.ChangedAt = _started;
            row.LastSeenAt = _started;
            row.MissingSince = null;
            step.Updated++;
        }

        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        if (unchanged.Count > 0)
        {
            await db.HrAttendanceEvents.Where(e => unchanged.Contains(e.VerifixTrackId)).ExecuteUpdateAsync(s => s.SetProperty(e => e.LastSeenAt, _started), ct);
        }

        await HeartbeatAsync(ct);
    }

    // ---------- Табель ----------

    private async Task TimesheetAsync(VerifixConnection connection, DateOnly from, DateOnly today, DateOnly historyStart, HrSyncStep step, CancellationToken ct)
    {
        for (var start = from; start <= today; start = start.AddDays(7))
        {
            var end = start.AddDays(6) < today ? start.AddDays(6) : today;
            var body = new
            {
                period_begin_date = VerifixFormats.Date(start), period_end_date = VerifixFormats.Date(end),
                division_ids = Array.Empty<long>(), employee_ids = Array.Empty<long>(),
            };
            await client.ListAsync<VerifixTimesheetDto>(connection, "core/timesheet$export", body, TimesheetPage, page => SaveTimesheetAsync(page, start, end, step, ct), ct);

            // Период перечитан целиком: дни, которых больше нет в табеле (сняли график), помечаются.
            await db.HrTimesheetDays.Where(d => d.Date >= start && d.Date <= end && d.SyncedAt < _started && d.MissingSince == null)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.MissingSince, _started), ct);
            await SaveStateAsync(TimesheetState, step.Rows, end == today ? _started : null, start == from ? from : null, ct, historyStart);
        }
    }

    private async Task SaveTimesheetAsync(IReadOnlyList<VerifixTimesheetDto> page, DateOnly from, DateOnly to, HrSyncStep step, CancellationToken ct)
    {
        step.Pages++;
        var days = new List<(VerifixTimesheetDto Staff, VerifixTimesheetDayDto Day, DateOnly Date)>();
        foreach (var staff in page.Where(s => s.StaffId > 0))
        {
            foreach (var day in staff.Days ?? [])
            {
                if (VerifixFormats.ParseDate(day.Date) is { } date && date >= from && date <= to && !string.IsNullOrWhiteSpace(day.DayKind))
                {
                    days.Add((staff, day, date));
                }
                else
                {
                    step.Skipped++;
                }
            }
        }

        var staffIds = days.Select(d => d.Staff.StaffId).Distinct().ToList();
        var existing = (await db.HrTimesheetDays.Where(d => staffIds.Contains(d.StaffId) && d.Date >= from && d.Date <= to).ToListAsync(ct))
            .ToDictionary(d => (d.StaffId, d.Date));
        var unchanged = new List<long>();
        foreach (var group in days.GroupBy(d => (d.Staff.StaffId, d.Date)))
        {
            var (staff, day, date) = group.Last();
            step.Rows++;
            var facts = JsonSerializer.Serialize((day.Facts ?? []).Where(f => f.TimeKindId > 0).OrderBy(f => f.TimeKindId)
                .Select(f => new { k = f.TimeKindId, v = f.FactValue ?? 0 }));
            var employeeId = _employees.TryGetValue(staff.EmployeeId, out var id) ? id : (Guid?)null;
            var next = new HrTimesheetDay
            {
                StaffId = staff.StaffId, Date = date, VerifixEmployeeId = staff.EmployeeId, EmployeeId = employeeId, DivisionId = staff.DivisionId, JobId = staff.JobId,
                ScheduleId = staff.ScheduleId, DayKind = Trim(day.DayKind, 4)!, PlanBegin = VerifixFormats.ParseLocal(day.BeginTime),
                PlanEnd = VerifixFormats.ParseLocal(day.EndTime), BreakBegin = VerifixFormats.ParseLocal(day.BreakBeginTime),
                BreakEnd = VerifixFormats.ParseLocal(day.BreakEndTime), PlanMinutes = day.PlanTime, FirstIn = VerifixFormats.ParseLocal(day.InputTime),
                LastOut = VerifixFormats.ParseLocal(day.OutputTime), InputLocationId = day.InputLocationId, OutputLocationId = day.OutputLocationId,
                DoneMarks = day.DoneMarks, PlannedMarks = day.PlannedMarks, FactsJson = facts, SyncedAt = _started,
            };

            if (!existing.TryGetValue((staff.StaffId, date), out var row))
            {
                db.HrTimesheetDays.Add(next);
                step.Created++;
                continue;
            }

            var same = row.VerifixEmployeeId == next.VerifixEmployeeId && row.DivisionId == next.DivisionId && row.JobId == next.JobId && row.ScheduleId == next.ScheduleId
                       && row.DayKind == next.DayKind && row.PlanBegin == next.PlanBegin && row.PlanEnd == next.PlanEnd && row.BreakBegin == next.BreakBegin
                       && row.BreakEnd == next.BreakEnd && row.PlanMinutes == next.PlanMinutes && row.FirstIn == next.FirstIn && row.LastOut == next.LastOut
                       && row.InputLocationId == next.InputLocationId && row.OutputLocationId == next.OutputLocationId && row.DoneMarks == next.DoneMarks
                       && row.PlannedMarks == next.PlannedMarks && SameJson(row.FactsJson, facts) && row.MissingSince == null
                       && (row.EmployeeId is not null || employeeId is null);
            if (same)
            {
                unchanged.Add(row.Id);
                step.Skipped++;
                db.Entry(row).State = EntityState.Detached;
                continue;
            }

            next.Id = row.Id;
            next.EmployeeId ??= row.EmployeeId;
            db.Entry(row).CurrentValues.SetValues(next);
            row.MissingSince = null;
            step.Updated++;
        }

        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        if (unchanged.Count > 0)
        {
            await db.HrTimesheetDays.Where(d => unchanged.Contains(d.Id)).ExecuteUpdateAsync(s => s.SetProperty(d => d.SyncedAt, _started), ct);
        }

        // Последний рабочий цикл сотрудника (staff_id меняется при повторном приёме).
        foreach (var (employee, staffId) in page.Where(s => s.StaffId > 0 && _employees.ContainsKey(s.EmployeeId)).GroupBy(s => s.EmployeeId).Select(g => (g.Key, g.Max(s => s.StaffId))))
        {
            await db.HrEmployees.Where(e => e.VerifixId == employee && (e.StaffId == null || e.StaffId < staffId))
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.StaffId, staffId), ct);
        }

        await HeartbeatAsync(ct);
    }

    private static bool SameJson(string stored, string fresh)
    {
        try
        {
            return JsonSerializer.Serialize(JsonDocument.Parse(stored).RootElement) == JsonSerializer.Serialize(JsonDocument.Parse(fresh).RootElement);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    // ---------- Связи ----------

    /// <summary>Отметки и дни табеля, пришедшие раньше сотрудника, привязываются, когда сотрудник появился.</summary>
    private async Task LinkAsync(HrSyncStep step, CancellationToken ct)
    {
        var orphans = await db.HrAttendanceEvents.AsNoTracking().Where(e => e.EmployeeId == null).Select(e => e.VerifixEmployeeId).Distinct().Take(2000).ToListAsync(ct);
        orphans.AddRange(await db.HrTimesheetDays.AsNoTracking().Where(d => d.EmployeeId == null).Select(d => d.VerifixEmployeeId).Distinct().Take(2000).ToListAsync(ct));
        foreach (var verifixId in orphans.Distinct())
        {
            if (!_employees.TryGetValue(verifixId, out var id))
            {
                continue;
            }

            step.Updated += await db.HrAttendanceEvents.Where(e => e.EmployeeId == null && e.VerifixEmployeeId == verifixId)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.EmployeeId, id), ct);
            step.Updated += await db.HrTimesheetDays.Where(d => d.EmployeeId == null && d.VerifixEmployeeId == verifixId)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.EmployeeId, id), ct);
        }

        var left = await db.HrAttendanceEvents.AsNoTracking().CountAsync(e => e.EmployeeId == null, ct);
        if (left > 0)
        {
            step.Skipped = left;
            step.Message = $"отметок без сотрудника в списке Verifix: {left}";
        }
    }

    // ---------- Состояние и журнал ----------

    private async Task SaveStateAsync(string entity, int rows, DateTimeOffset? coveredThrough, DateOnly? coveredFrom, CancellationToken ct, DateOnly? historyStart = null)
    {
        var state = await db.HrSyncStates.FirstOrDefaultAsync(s => s.Entity == entity, ct);
        if (state is null)
        {
            state = new HrSyncState { Entity = entity };
            db.HrSyncStates.Add(state);
        }

        state.LastSuccessAt = time.GetUtcNow();
        state.LastRows = rows;
        state.LastError = null;
        state.LastErrorAt = null;
        if (coveredThrough is { } through && (state.CoveredThrough is null || through > state.CoveredThrough))
        {
            state.CoveredThrough = through;
        }

        // Начало истории: окно начиналось с historyStart (первая загрузка) или раньше прежнего начала.
        if (coveredFrom is { } from && (state.CoveredFrom is null || from < state.CoveredFrom || (historyStart is { } h && from <= h)))
        {
            state.CoveredFrom = from;
        }

        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }

    private async Task SaveStateErrorAsync(string entity, string message, CancellationToken ct)
    {
        try
        {
            db.ChangeTracker.Clear();
            var state = await db.HrSyncStates.FirstOrDefaultAsync(s => s.Entity == entity, ct);
            if (state is null)
            {
                state = new HrSyncState { Entity = entity };
                db.HrSyncStates.Add(state);
            }

            state.LastError = message.Length > 1000 ? message[..1000] : message;
            state.LastErrorAt = time.GetUtcNow();
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Verifix: не удалось записать состояние {Entity}", entity);
        }
    }

    private Task HeartbeatAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        return db.HrSyncRuns.Where(r => r.Id == _runId).ExecuteUpdateAsync(s => s.SetProperty(r => r.HeartbeatAt, now), ct);
    }

    private static string? Trim(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();
        return text.Length <= max ? text : text[..max];
    }
}
