using OneBase.Domain.Hr;

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

/// <summary>Сотрудник рейтинга «Лидеры месяца»: место, отработано и план за период, дней с приходом (все — без опоздания).</summary>
public sealed record HrLeader(int Place, Guid EmployeeId, string Employee, string? Division, string? Job, int WorkedMinutes, int PlanMinutes, int PresentDays, int PlanDays);

/// <summary>«Лидеры месяца»: период, сколько работающих сотрудников и сколько из них ни разу не опоздали, первые места.</summary>
public sealed record HrLeaders(DateOnly From, DateOnly To, int Employees, int OnTime, IReadOnlyList<HrLeader> Leaders, HrSyncBrief Sync);

public sealed partial class HrService
{
    /// <summary>В первые дни месяца рейтинг — за прошлый месяц целиком: за день-два лидеров не выбрать.</summary>
    public const int LeadersPreviousMonthDays = 3;

    /// <summary>
    /// «Лидеры месяца» на главной: работающие сотрудники, которые за месяц ни разу не опоздали (хотя бы один день с приходом), — по
    /// отработанным часам (больше — выше), при равенстве — по дням с приходом. Период — с 1-го числа по сегодня; в первые
    /// <see cref="LeadersPreviousMonthDays"/> дня — прошлый месяц. Видимость — как везде в HR.
    /// </summary>
    public async Task<HrLeaders> LeadersAsync(HrContext ctx, int take, CancellationToken ct)
    {
        Require(ctx);
        var rules = await rulesSource.GetRulesAsync(ct);
        var today = DateOnly.FromDateTime(Now(rules));
        var (start, end) = today.Day <= LeadersPreviousMonthDays
            ? (new DateOnly(today.Year, today.Month, 1).AddMonths(-1), new DateOnly(today.Year, today.Month, 1).AddDays(-1))
            : (new DateOnly(today.Year, today.Month, 1), today);
        var period = await EvaluateAsync(ctx, start, end, null, null, null, rules, ct);
        var names = await NamesAsync(ct);

        var working = period.Rows.Where(r => r.Employee.Status == HrEmployeeStatus.Working)
            .Select(r => (r.Employee, Summary: HrSummary.Of(r.Days)))
            .ToList();
        var onTime = working.Where(x => x.Summary.PresentDays > 0 && x.Summary.LateDays == 0 && x.Summary.WorkedMinutes > 0)
            .OrderByDescending(x => x.Summary.WorkedMinutes).ThenByDescending(x => x.Summary.PresentDays).ThenBy(x => x.Employee.FullName, StringComparer.Ordinal)
            .ToList();
        var leaders = onTime.Take(Math.Clamp(take, 1, 50))
            .Select((x, i) => new HrLeader(i + 1, x.Employee.Id, x.Employee.FullName, Name(names.Divisions, x.Employee.DivisionId, null),
                Name(names.Jobs, x.Employee.JobId, null), x.Summary.WorkedMinutes, x.Summary.PlanMinutes, x.Summary.PresentDays, x.Summary.PlanDays))
            .ToList();
        return new HrLeaders(start, end, working.Count, onTime.Count, leaders, await SyncBriefAsync(rules, ct));
    }

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
    public async Task<HrTimesheet> TimesheetAsync(HrContext ctx, DateOnly? from, DateOnly? to, long? divisionId, string? search, Guid? employeeId, CancellationToken ct)
    {
        Require(ctx);
        var rules = await rulesSource.GetRulesAsync(ct);
        var today = DateOnly.FromDateTime(Now(rules));
        var (start, end) = Period(from ?? new DateOnly(today.Year, today.Month, 1), to ?? today);
        var period = await EvaluateAsync(ctx, start, end, divisionId, Only(employeeId), search, rules, ct);
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
    public async Task<HrReport> ReportAsync(HrContext ctx, DateOnly? from, DateOnly? to, long? divisionId, Guid? employeeId, CancellationToken ct)
    {
        Require(ctx);
        var rules = await rulesSource.GetRulesAsync(ct);
        var today = DateOnly.FromDateTime(Now(rules));
        var (start, end) = Period(from ?? new DateOnly(today.Year, today.Month, 1), to ?? today);
        var period = await EvaluateAsync(ctx, start, end, divisionId, Only(employeeId), null, rules, ct);
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

    /// <summary>Один сотрудник (выгрузка из его карточки) или все видимые.</summary>
    private static IReadOnlyCollection<Guid>? Only(Guid? employeeId) => employeeId is { } id ? [id] : null;

    private static double? Attendance(HrSummary s) => s.PresentDays + s.AbsentDays is var n and > 0 ? Math.Round(100.0 * s.PresentDays / n, 1) : null;

    private static double? Punctuality(HrSummary s) => s.PresentDays > 0 ? Math.Round(100.0 * (s.PresentDays - s.LateDays) / s.PresentDays, 1) : null;
}
