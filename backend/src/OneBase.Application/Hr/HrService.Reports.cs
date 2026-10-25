namespace OneBase.Application.Hr;

public sealed record HrTimesheetRow(Guid EmployeeId, string Employee, string? Division, string? Job, HrSummary Summary, IReadOnlyList<string> Codes);

public sealed record HrTimesheet(DateOnly From, DateOnly To, IReadOnlyList<DateOnly> Dates, IReadOnlyList<HrTimesheetRow> Rows, HrSummary Total, HrSyncBrief Sync);

/// <summary>Итог подразделения. AttendanceRate — доля дней с приходом среди решённых рабочих (пришёл или отсутствовал); Punctuality — доля приходов без опоздания.</summary>
public sealed record HrReportGroup(long? DivisionId, string Division, int Employees, HrSummary Summary, double? AttendanceRate, double? Punctuality);

public sealed record HrReport(
    DateOnly From,
    DateOnly To,
    int Employees,
    HrSummary Total,
    double? AttendanceRate,
    double? Punctuality,
    IReadOnlyList<HrReportGroup> Divisions,
    IReadOnlyList<HrTimesheetRow> TopLate,
    IReadOnlyList<HrTimesheetRow> TopAbsent,
    HrSyncBrief Sync);

public sealed partial class HrService
{
    /// <summary>
    /// Буква дня в табеле: Я — явка, ОП — опоздание, РУ — ранний уход, РВ — работа в нерабочий день, ОТС — отсутствие, буква документа
    /// (ОТ, Б, К, О, НО) — отпуск и т. п., В — выходной, П — праздник, · — день ещё не начался, — — нет графика, ? — данных недостаточно.
    /// </summary>
    public static string DayCode(HrDayResult d) => d.Status switch
    {
        HrDayStatus.NotEmployed => "",
        HrDayStatus.Present when d.WorkedOnDayOff => "РВ",
        HrDayStatus.Present when d.Late => "ОП",
        HrDayStatus.Present when d.EarlyLeave => "РУ",
        HrDayStatus.Present => "Я",
        HrDayStatus.Absent => "ОТС",
        HrDayStatus.OnLeave => string.IsNullOrWhiteSpace(d.LeaveCode) ? "ОТ" : d.LeaveCode!,
        HrDayStatus.DayOff => d.DayKind == "H" ? "П" : "В",
        HrDayStatus.Expected => "·",
        HrDayStatus.NoSchedule => "—",
        _ => "?",
    };

    /// <summary>«Рабочее время и табели»: по сотруднику — итоги периода и буква каждого дня.</summary>
    public async Task<HrTimesheet> TimesheetAsync(HrContext ctx, DateOnly? from, DateOnly? to, long? divisionId, string? search, CancellationToken ct)
    {
        Require(ctx);
        var rules = await rulesSource.GetRulesAsync(ct);
        var today = DateOnly.FromDateTime(Now(rules));
        var (start, end) = Period(from ?? new DateOnly(today.Year, today.Month, 1), to ?? today);
        var period = await EvaluateAsync(ctx, start, end, divisionId, null, search, rules, ct);
        var names = await NamesAsync(ct);
        var rows = period.Rows
            .Select(r => new HrTimesheetRow(r.Employee.Id, r.Employee.FullName, Name(names.Divisions, r.Employee.DivisionId, null), Name(names.Jobs, r.Employee.JobId, null),
                HrSummary.Of(r.Days), r.Days.Select(DayCode).ToList()))
            .OrderBy(r => r.Division).ThenBy(r => r.Employee)
            .ToList();
        var dates = Enumerable.Range(0, end.DayNumber - start.DayNumber + 1).Select(start.AddDays).ToList();
        return new HrTimesheet(start, end, dates, rows, HrSummary.Of(period.Rows.SelectMany(r => r.Days)), await SyncBriefAsync(rules, ct));
    }

    /// <summary>«Отчёты»: итоги по подразделениям, частые опоздания и отсутствия за период.</summary>
    public async Task<HrReport> ReportAsync(HrContext ctx, DateOnly? from, DateOnly? to, long? divisionId, CancellationToken ct)
    {
        Require(ctx);
        var rules = await rulesSource.GetRulesAsync(ct);
        var today = DateOnly.FromDateTime(Now(rules));
        var (start, end) = Period(from ?? new DateOnly(today.Year, today.Month, 1), to ?? today);
        var period = await EvaluateAsync(ctx, start, end, divisionId, null, null, rules, ct);
        var names = await NamesAsync(ct);

        var people = period.Rows
            .Select(r => new HrTimesheetRow(r.Employee.Id, r.Employee.FullName, Name(names.Divisions, r.Employee.DivisionId, null), Name(names.Jobs, r.Employee.JobId, null),
                HrSummary.Of(r.Days), r.Days.Select(DayCode).ToList()))
            .ToList();
        var groups = period.Rows.GroupBy(r => r.Employee.DivisionId)
            .Select(g =>
            {
                var summary = HrSummary.Of(g.SelectMany(r => r.Days));
                return new HrReportGroup(g.Key, Name(names.Divisions, g.Key, "Без подразделения")!, g.Count(), summary, Attendance(summary), Punctuality(summary));
            })
            .OrderBy(g => g.Division).ToList();
        var total = HrSummary.Of(period.Rows.SelectMany(r => r.Days));
        return new HrReport(start, end, period.Rows.Count, total, Attendance(total), Punctuality(total), groups,
            people.Where(p => p.Summary.LateDays > 0).OrderByDescending(p => p.Summary.LateDays).ThenByDescending(p => p.Summary.LateMinutes).Take(10).ToList(),
            people.Where(p => p.Summary.AbsentDays > 0).OrderByDescending(p => p.Summary.AbsentDays).Take(10).ToList(),
            await SyncBriefAsync(rules, ct));
    }

    private static double? Attendance(HrSummary s) => s.PresentDays + s.AbsentDays is var n and > 0 ? Math.Round(100.0 * s.PresentDays / n, 1) : null;

    private static double? Punctuality(HrSummary s) => s.PresentDays > 0 ? Math.Round(100.0 * (s.PresentDays - s.LateDays) / s.PresentDays, 1) : null;
}
