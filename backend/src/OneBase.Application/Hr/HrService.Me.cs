using Microsoft.EntityFrameworkCore;

namespace OneBase.Application.Hr;

/// <summary>«Мой день»: рабочее время самого сотрудника — без прав HR, только по своей привязке к сотруднику Verifix (HrEmployee.UserId).</summary>
public sealed partial class HrService
{
    public async Task<HrMyWorkday> MyWorkdayAsync(Guid userId, CancellationToken ct)
    {
        var rules = await rulesSource.GetRulesAsync(ct);
        var now = Now(rules);
        var today = DateOnly.FromDateTime(now);
        var employee = await db.HrEmployees.AsNoTracking().Where(e => e.UserId == userId).Select(e => (Guid?)e.Id).FirstOrDefaultAsync(ct);
        if (employee is not { } id)
        {
            var connected = (await rulesSource.GetStateAsync(ct)).Configured || await db.HrEmployees.AnyAsync(ct);
            return new HrMyWorkday(false, connected ? "no_employee_link" : "not_connected", now, null, [], null, null, null);
        }

        // Контекст «видит всех» ограничен единственным своим сотрудником (only): чужие данные в расчёт не попадают.
        var self = new HrContext(userId, true, false, null, []);
        var monday = HrMyDay.Monday(today);
        var period = await EvaluateAsync(self, monday, monday.AddDays(6), null, [id], null, rules, ct, includeAll: true, adjust: HrMyDay.WithBreak);
        var days = period.Rows.FirstOrDefault()?.Days ?? [];
        var day = days.FirstOrDefault(d => d.Date == today);

        var sheet = await db.HrTimesheetDays.AsNoTracking()
            .Where(d => d.EmployeeId == id && d.Date == today && d.MissingSince == null)
            .OrderByDescending(d => d.DayKind == "W").ThenByDescending(d => d.SyncedAt)
            .Select(d => new HrTimesheetFacts(d.DayKind, d.PlanBegin, d.PlanEnd, d.BreakBegin, d.BreakEnd, d.PlanMinutes, d.FirstIn, d.LastOut, new List<HrFact>()))
            .FirstOrDefaultAsync(ct);
        var withBreak = sheet is null ? null : HrMyDay.WithBreak(sheet, today);
        var last = await db.HrAttendanceEvents.AsNoTracking()
            .Where(e => e.EmployeeId == id && e.LocalDate == today && e.MissingSince == null)
            .OrderByDescending(e => e.LocalTime)
            .Select(e => new HrMark(e.LocalTime, e.TrackType))
            .FirstOrDefaultAsync(ct);

        HrMyToday? todayView = null;
        int? workedToday = null;
        if (day is not null)
        {
            var status = HrMyDay.Status(day, last, withBreak?.BreakBegin, withBreak?.BreakEnd, now);
            workedToday = HrMyDay.WorkedSoFar(day, status, last, withBreak?.BreakBegin, withBreak?.BreakEnd, now);
            HrMyBreak? pause = withBreak is { BreakBegin: { } bb, BreakEnd: { } be } && be > bb
                ? new HrMyBreak(bb, be, (int)Math.Round((be - bb).TotalMinutes), sheet!.BreakBegin is not null)
                : null;
            todayView = new HrMyToday(status, day.PlanBegin, day.PlanEnd, day.FirstIn, status == "finished" ? day.LastOut : null,
                last?.Time ?? day.LastOut ?? day.FirstIn, workedToday, day.PlanMinutes, day.LateMinutes, day.EarlyLeaveMinutes, pause, day.LeaveName, day.Reason);
        }

        var week = days.Select(d => new HrMyWeekDay(d.Date, HrMyDay.WeekStatus(d, today), d.FirstIn, d.LastOut,
                d.Date == today ? workedToday : d.Date < today ? d.WorkedMinutes : null, d.LateMinutes, d.PlanBegin, d.PlanEnd))
            .ToList();
        var past = week.Where(d => d.Date <= today).ToList();
        var totals = new HrMyWeekTotals(past.Sum(d => d.WorkedMinutes ?? 0), past.Count(d => d.Status == "late"), past.Sum(d => d.Status == "late" ? d.LateMinutes ?? 0 : 0));
        var sync = await SyncBriefAsync(rules, ct);
        return new HrMyWorkday(true, null, now, todayView, week, totals, sync.LastSuccessAt, sync.TracksThrough);
    }
}
