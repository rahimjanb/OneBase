using OneBase.Domain.Hr;

namespace OneBase.Application.Hr;

public sealed record HrTimesheetRow(Guid EmployeeId, string Employee, string? Division, string? Job, HrSummary Summary, IReadOnlyList<string> Codes);

public sealed record HrTimesheet(DateOnly From, DateOnly To, IReadOnlyList<DateOnly> Dates, IReadOnlyList<HrTimesheetRow> Rows, HrSummary Total, HrSyncBrief Sync);

/// <summary>Итог подразделения. AttendanceRate — доля дней с приходом среди решённых рабочих (пришёл или отсутствовал); Punctuality — доля приходов без опоздания.</summary>
public sealed record HrReportGroup(long? DivisionId, string Division, int Employees, HrSummary Summary, double? AttendanceRate, double? Punctuality);

/// <summary>
/// Отчёт за период. TopLate — 10 чаще всех опаздывавших (по разам), TopLateTime — 10 с наибольшим временем опозданий; TopAbsent — 10 с наибольшим
/// числом отсутствий. AbsentWholePeriod — сколько сотрудников, работавших весь период, ни в один рабочий день не пришли (см. <see cref="HrService.AbsentWholePeriod"/>).
/// </summary>
public sealed record HrReport(
    DateOnly From,
    DateOnly To,
    int Employees,
    HrSummary Total,
    double? AttendanceRate,
    double? Punctuality,
    IReadOnlyList<HrReportGroup> Divisions,
    IReadOnlyList<HrTimesheetRow> TopLate,
    IReadOnlyList<HrTimesheetRow> TopLateTime,
    IReadOnlyList<HrTimesheetRow> TopAbsent,
    int AbsentWholePeriod,
    HrSyncBrief Sync);

/// <summary>Выгрузка в Excel: итоги, табель по дням и отклонения — из одного расчёта периода.</summary>
public sealed record HrReportExport(HrReport Report, HrTimesheet Timesheet, IReadOnlyList<HrDeviation> Deviations);

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

    /// <summary>
    /// «Рабочее время и табели»: по сотруднику — итоги периода и буква каждого дня. maxDays — предел периода: страницы и API — 62 дня, AI —
    /// до квартала (MaxReportDays).
    /// </summary>
    public async Task<HrTimesheet> TimesheetAsync(HrContext ctx, DateOnly? from, DateOnly? to, long? divisionId, string? search, Guid? employeeId, CancellationToken ct,
        int maxDays = MaxPeriodDays)
    {
        Require(ctx);
        var rules = await rulesSource.GetRulesAsync(ct);
        var today = DateOnly.FromDateTime(Now(rules));
        var (start, end) = Period(from ?? new DateOnly(today.Year, today.Month, 1), to ?? today, Math.Min(maxDays, MaxReportDays));
        var period = await EvaluateAsync(ctx, start, end, Divisions(divisionId), Only(employeeId), search, rules, ct);
        return TimesheetOf(period, await NamesAsync(ct), await SyncBriefAsync(rules, ct));
    }

    /// <summary>«Отчёты»: итоги по подразделениям, частые опоздания и отсутствия за период (до квартала). divisions — несколько подразделений с дочерними.</summary>
    public async Task<HrReport> ReportAsync(HrContext ctx, DateOnly? from, DateOnly? to, IReadOnlyCollection<long>? divisions, Guid? employeeId, CancellationToken ct)
    {
        Require(ctx);
        var rules = await rulesSource.GetRulesAsync(ct);
        var today = DateOnly.FromDateTime(Now(rules));
        var (start, end) = Period(from ?? new DateOnly(today.Year, today.Month, 1), to ?? today, MaxReportDays);
        var period = await EvaluateAsync(ctx, start, end, divisions, Only(employeeId), null, rules, ct);
        return ReportOf(period, await NamesAsync(ct), await SyncBriefAsync(rules, ct));
    }

    /// <summary>Выгрузка в Excel за период (до квартала): отчёт, табель и отклонения считаются один раз.</summary>
    public async Task<HrReportExport> ExportAsync(HrContext ctx, DateOnly? from, DateOnly? to, IReadOnlyCollection<long>? divisions, Guid? employeeId, CancellationToken ct)
    {
        Require(ctx);
        var rules = await rulesSource.GetRulesAsync(ct);
        var today = DateOnly.FromDateTime(Now(rules));
        var (start, end) = Period(from ?? new DateOnly(today.Year, today.Month, 1), to ?? today, MaxReportDays);
        var period = await EvaluateAsync(ctx, start, end, divisions, Only(employeeId), null, rules, ct);
        var names = await NamesAsync(ct);
        var sync = await SyncBriefAsync(rules, ct);
        return new HrReportExport(ReportOf(period, names, sync), TimesheetOf(period, names, sync), DeviationsOf(period, names, null));
    }

    private static HrTimesheetRow PersonOf(PeriodRow r, Names names) =>
        new(r.Employee.Id, r.Employee.FullName, Name(names.Divisions, r.Employee.DivisionId, null), Name(names.Jobs, r.Employee.JobId, null),
            HrSummary.Of(r.Days), r.Days.Select(DayCode).ToList());

    private static HrTimesheet TimesheetOf(PeriodResult period, Names names, HrSyncBrief sync)
    {
        var rows = period.Rows.Select(r => PersonOf(r, names)).OrderBy(r => r.Division).ThenBy(r => r.Employee).ToList();
        var dates = Enumerable.Range(0, period.To.DayNumber - period.From.DayNumber + 1).Select(period.From.AddDays).ToList();
        return new HrTimesheet(period.From, period.To, dates, rows, HrSummary.Of(period.Rows.SelectMany(r => r.Days)), sync);
    }

    private static HrReport ReportOf(PeriodResult period, Names names, HrSyncBrief sync)
    {
        var people = period.Rows.Select(r => PersonOf(r, names)).ToList();
        var groups = period.Rows.GroupBy(r => r.Employee.DivisionId)
            .Select(g =>
            {
                var summary = HrSummary.Of(g.SelectMany(r => r.Days));
                return new HrReportGroup(g.Key, Name(names.Divisions, g.Key, "Без подразделения")!, g.Count(), summary, Attendance(summary), Punctuality(summary));
            })
            .OrderBy(g => g.Division).ToList();
        var total = HrSummary.Of(period.Rows.SelectMany(r => r.Days));
        var late = people.Where(p => p.Summary.LateDays > 0).ToList();
        return new HrReport(period.From, period.To, period.Rows.Count, total, Attendance(total), Punctuality(total), groups,
            late.OrderByDescending(p => p.Summary.LateDays).ThenByDescending(p => p.Summary.LateMinutes).Take(10).ToList(),
            late.OrderByDescending(p => p.Summary.LateMinutes).ThenByDescending(p => p.Summary.LateDays).Take(10).ToList(),
            people.Where(p => p.Summary.AbsentDays > 0).OrderByDescending(p => p.Summary.AbsentDays).Take(10).ToList(),
            period.Rows.Count(r => AbsentWholePeriod(r.Days)),
            sync);
    }

    /// <summary>
    /// Не приходил весь период: работал в компании все дни периода и ни в один свой рабочий день не пришёл — только отсутствия (день,
    /// который ещё не начался, не мешает), без документов и без «не ясно». Кто принят или уволен внутри периода — не в счёт.
    /// </summary>
    public static bool AbsentWholePeriod(IReadOnlyList<HrDayResult> days) =>
        days.Any(d => d.Status == HrDayStatus.Absent)
        && days.All(d => d.Status is HrDayStatus.Absent or HrDayStatus.Expected or HrDayStatus.DayOff or HrDayStatus.NoSchedule);

    /// <summary>Один сотрудник (выгрузка из его карточки) или все видимые.</summary>
    private static IReadOnlyCollection<Guid>? Only(Guid? employeeId) => employeeId is { } id ? [id] : null;

    private static double? Attendance(HrSummary s) => s.PresentDays + s.AbsentDays is var n and > 0 ? Math.Round(100.0 * s.PresentDays / n, 1) : null;

    private static double? Punctuality(HrSummary s) => s.PresentDays > 0 ? Math.Round(100.0 * (s.PresentDays - s.LateDays) / s.PresentDays, 1) : null;
}
