namespace OneBase.Application.Hr;

/// <summary>Правила учёта времени из настроек интеграции: часовой пояс организации и допуски.</summary>
public sealed record HrRules(TimeZoneInfo Zone, int LateGraceMinutes, int EarlyLeaveGraceMinutes);

/// <summary>Состояние подключения к Verifix — без секретов: есть ли ключи, включено ли, расписание, адрес Verifix (для ссылок «Открыть в Verifix»).</summary>
public sealed record HrIntegrationState(bool Configured, bool Enabled, bool AutoSync, int IntervalMinutes, string? BaseUrl = null);

/// <summary>Правила и состояние интеграции из настроек Verifix (реализация — в Infrastructure, VerifixSettingsStore).</summary>
public interface IHrRules
{
    Task<HrRules> GetRulesAsync(CancellationToken ct = default);

    Task<HrIntegrationState> GetStateAsync(CancellationToken ct = default);
}

/// <summary>Итог дня сотрудника.</summary>
public enum HrDayStatus
{
    /// <summary>Есть подтверждённый приход (по табелю Verifix или отметке типа I).</summary>
    Present,

    /// <summary>Рабочий день по графику, нет прихода, нет отпуска или другого отсутствия по документу, данные за это время загружены полностью.</summary>
    Absent,

    /// <summary>Отпуск, больничный, командировка, отгул — по фактам табеля Verifix.</summary>
    OnLeave,

    /// <summary>Выходной, праздник или нерабочий день по графику.</summary>
    DayOff,

    /// <summary>Рабочий день ещё не начался (или не вышел допуск опоздания) — рано судить.</summary>
    Expected,

    /// <summary>На этот день у сотрудника нет графика в Verifix (табель его не прислал).</summary>
    NoSchedule,

    /// <summary>Нельзя решить: данные неполные (синхронизация не дошла до этого времени) или отметки без подтверждённого прихода.</summary>
    Unknown,

    /// <summary>Сотрудник в этот день не работал в компании (ещё не принят или уже уволен).</summary>
    NotEmployed,
}

/// <summary>Смысл вида времени Verifix (time_kind) — по буквенному коду и названию из справочника организации.</summary>
public enum HrTimeKindClass
{
    Other,
    Presence,
    Late,
    EarlyLeave,
    Absence,

    /// <summary>Отсутствие по документу на день: отпуск, больничный, командировка, отгул, неоплачиваемый отпуск.</summary>
    Leave,

    /// <summary>Почасовое отсутствие по документу — день остаётся рабочим.</summary>
    PartialLeave,

    DayOff,
    Overtime,
}

public static class HrTimeKinds
{
    /// <summary>
    /// По стандартным видам времени Verifix (time_kind$list: Я, ОП, РУ, ОТС, СВ, ПО, В, Б, О, К, ОТ, НО, СУ, ДЯ). Организация может переименовать
    /// вид — тогда запасной вариант по названию; незнакомый вид в расчёт не идёт.
    /// </summary>
    public static HrTimeKindClass Classify(string? letterCode, string? name)
    {
        switch ((letterCode ?? string.Empty).Trim().ToUpperInvariant())
        {
            case "Я":
            case "ДЯ":
                return HrTimeKindClass.Presence;
            case "ОП":
                return HrTimeKindClass.Late;
            case "РУ":
                return HrTimeKindClass.EarlyLeave;
            case "ОТС":
                return HrTimeKindClass.Absence;
            case "ОТ":
            case "Б":
            case "К":
            case "О":
            case "НО":
                return HrTimeKindClass.Leave;
            case "ПО":
                return HrTimeKindClass.PartialLeave;
            case "В":
                return HrTimeKindClass.DayOff;
            case "СУ":
                return HrTimeKindClass.Overtime;
        }

        var n = (name ?? string.Empty).Trim().ToLowerInvariant();
        return n switch
        {
            _ when n.StartsWith("почасов", StringComparison.Ordinal) => HrTimeKindClass.PartialLeave,
            _ when n.Contains("отпуск") || n.Contains("больнич") || n.Contains("командиров") || n.Contains("отгул") => HrTimeKindClass.Leave,
            _ when n.Contains("опоздан") => HrTimeKindClass.Late,
            _ when n.Contains("ранний уход") => HrTimeKindClass.EarlyLeave,
            _ when n.Contains("отсутств") => HrTimeKindClass.Absence,
            _ when n == "явка" => HrTimeKindClass.Presence,
            _ => HrTimeKindClass.Other,
        };
    }
}

/// <summary>Сотрудник для расчёта: статус и даты работы.</summary>
public sealed record HrEmployeeFacts(string Status, DateOnly? HiringDate, DateOnly? DismissalDate);

/// <summary>Факт табеля, уже с понятным смыслом вида времени. Value — как прислал Verifix (единица не согласована — используется только «больше нуля»).</summary>
public sealed record HrFact(HrTimeKindClass Class, string? Code, string Name, long Value);

/// <summary>День табеля Verifix (всё время — локальное время организации).</summary>
public sealed record HrTimesheetFacts(
    string DayKind,
    DateTime? PlanBegin,
    DateTime? PlanEnd,
    DateTime? BreakBegin,
    DateTime? BreakEnd,
    int? PlanMinutes,
    DateTime? FirstIn,
    DateTime? LastOut,
    IReadOnlyList<HrFact> Facts);

/// <summary>Отметки дня одним итогом: сколько, первый приход (тип I), последний уход (тип O).</summary>
public sealed record HrEventSummary(int Count, DateTime? FirstIn, DateTime? LastOut)
{
    public static readonly HrEventSummary None = new(0, null, null);
}

/// <summary>
/// Насколько данные полные. TracksThrough — до какого локального момента отметки загружены целиком (последняя успешная выгрузка отметок);
/// TimesheetFrom/TimesheetTo — даты, за которые табель загружен полностью.
/// </summary>
public sealed record HrCoverage(DateTime? TracksThrough, DateOnly? TimesheetFrom, DateOnly? TimesheetTo)
{
    public bool TimesheetCovers(DateOnly date) => TimesheetFrom is { } from && TimesheetTo is { } to && date >= from && date <= to;
}

public sealed record HrDayResult(
    DateOnly Date,
    HrDayStatus Status,
    string? DayKind,
    DateTime? PlanBegin,
    DateTime? PlanEnd,
    int? PlanMinutes,
    DateTime? FirstIn,
    DateTime? LastOut,
    int Events,
    bool Late,
    int? LateMinutes,
    bool EarlyLeave,
    int? EarlyLeaveMinutes,
    int? WorkedMinutes,
    bool NoExit,
    bool WorkedOnDayOff,
    string? LeaveName,
    string? LeaveCode,
    string Source,
    string? Reason);

/// <summary>
/// Посещаемость дня по данным Verifix. Главный источник — табель Verifix (timesheet$export): он сам применяет график сотрудника
/// (в том числе индивидуальный и многосменный), выходные, праздники, отпуска и действительность отметок, а первый приход и последний уход
/// берёт только из действительных интервалов. Отметки (track$list) — запасной источник прихода и ухода, когда табель за день ещё не обновился.
/// Правила:
/// <list type="bullet">
/// <item>одна отметка — не рабочий день: присутствие — только подтверждённый приход (табель или отметка типа I), отметки других типов дают «не ясно»;</item>
/// <item>отсутствие — только если день рабочий по графику, нет отпуска, больничного, командировки или отгула, прошёл допуск опоздания
/// и отметки до этого момента загружены целиком; иначе — «ожидается», «нет графика» или «не ясно»;</item>
/// <item>опоздание — приход позже начала по графику больше чем на допуск; ранний уход — последний уход раньше конца по графику больше чем на
/// допуск (только когда смена закончилась); отработано — от прихода до ухода без пересечения с перерывом;</item>
/// <item>смена через полночь: начало и конец графика в табеле — полные дата и время, поэтому смена 22:00–06:00 считается одним днём.</item>
/// </list>
/// </summary>
public static class HrAttendance
{
    public static HrDayResult Evaluate(
        DateOnly date,
        HrEmployeeFacts employee,
        HrTimesheetFacts? timesheet,
        HrEventSummary events,
        HrCoverage coverage,
        DateTime nowLocal,
        HrRules rules)
    {
        var employed = employee.Status != "U"
                       && (employee.HiringDate is not { } hired || hired <= date)
                       && (employee.DismissalDate is not { } dismissed ? employee.Status != "D" : dismissed >= date);
        var firstIn = timesheet?.FirstIn ?? events.FirstIn;
        var lastOut = timesheet?.LastOut ?? (events.LastOut is { } o && (firstIn is null || o > firstIn) ? o : null);
        var source = timesheet?.FirstIn is not null ? "timesheet" : events.FirstIn is not null ? "events" : timesheet is not null ? "timesheet" : "events";

        HrDayResult Result(HrDayStatus status, string? reason = null, HrFact? leave = null, bool late = false, int? lateMinutes = null,
            bool early = false, int? earlyMinutes = null, int? worked = null, bool noExit = false, bool dayOffWork = false) =>
            new(date, status, timesheet?.DayKind, timesheet?.PlanBegin, timesheet?.PlanEnd, timesheet?.PlanMinutes, firstIn, lastOut,
                events.Count, late, lateMinutes, early, earlyMinutes, worked, noExit, dayOffWork, leave?.Name, leave?.Code, source, reason);

        if (!employed && firstIn is null)
        {
            return Result(HrDayStatus.NotEmployed, employee.Status == "U" ? "не принят" : employee.HiringDate > date ? "ещё не принят" : "уволен");
        }

        var leaveFact = timesheet?.Facts.FirstOrDefault(f => f.Class == HrTimeKindClass.Leave && f.Value > 0);
        var worked = Worked(firstIn, lastOut, timesheet?.BreakBegin, timesheet?.BreakEnd);

        if (timesheet is null)
        {
            if (firstIn is not null)
            {
                return Result(HrDayStatus.Present, "нет графика в Verifix — опоздание не считается", worked: worked, noExit: lastOut is null && date < DateOnly.FromDateTime(nowLocal));
            }

            if (events.Count > 0)
            {
                return Result(HrDayStatus.Unknown, "есть отметки без подтверждённого прихода");
            }

            return coverage.TimesheetCovers(date)
                ? Result(HrDayStatus.NoSchedule, "на этот день у сотрудника нет графика в Verifix")
                : Result(HrDayStatus.Unknown, "табель за этот день ещё не загружен");
        }

        if (timesheet.DayKind != "W")
        {
            return firstIn is not null
                ? Result(HrDayStatus.Present, "работа в нерабочий день", worked: worked, dayOffWork: true)
                : Result(HrDayStatus.DayOff);
        }

        if (firstIn is not null)
        {
            int? lateMinutes = null;
            if (timesheet.PlanBegin is { } begin && firstIn > begin)
            {
                var minutes = (int)Math.Ceiling((firstIn.Value - begin).TotalMinutes);
                lateMinutes = minutes > rules.LateGraceMinutes ? minutes : null;
            }

            int? earlyMinutes = null;
            var shiftOver = timesheet.PlanEnd is { } end ? nowLocal >= end : date < DateOnly.FromDateTime(nowLocal);
            if (shiftOver && lastOut is { } exit && timesheet.PlanEnd is { } planEnd && exit < planEnd)
            {
                var minutes = (int)Math.Ceiling((planEnd - exit).TotalMinutes);
                earlyMinutes = minutes > rules.EarlyLeaveGraceMinutes ? minutes : null;
            }

            return Result(HrDayStatus.Present, leaveFact is not null ? $"при документе «{leaveFact.Name}»" : null, leaveFact,
                late: lateMinutes is not null, lateMinutes: lateMinutes, early: earlyMinutes is not null, earlyMinutes: earlyMinutes,
                worked: worked, noExit: shiftOver && lastOut is null);
        }

        if (leaveFact is not null)
        {
            return Result(HrDayStatus.OnLeave, leave: leaveFact);
        }

        if (events.Count > 0)
        {
            return Result(HrDayStatus.Unknown, "есть отметки без подтверждённого прихода — проверьте в Verifix");
        }

        // Когда уже можно судить: начало по графику плюс допуск; без времени начала (гибкий или почасовой график) — после конца дня.
        var deadline = timesheet.PlanBegin is { } start
            ? start.AddMinutes(rules.LateGraceMinutes)
            : date.AddDays(1).ToDateTime(TimeOnly.MinValue);
        if (nowLocal < deadline)
        {
            return Result(HrDayStatus.Expected, timesheet.PlanBegin is { } s ? $"начало по графику в {s:HH:mm}" : "день ещё идёт");
        }

        if (coverage.TracksThrough is not { } through || through < deadline)
        {
            return Result(HrDayStatus.Unknown, coverage.TracksThrough is { } t
                ? $"отметки загружены до {t:dd.MM HH:mm} — данных за это время ещё нет"
                : "отметки ещё не загружались");
        }

        if (timesheet.PlanMinutes is 0 && timesheet.PlanBegin is null)
        {
            return Result(HrDayStatus.DayOff, "план на день — 0 минут");
        }

        var verifixAbsence = timesheet.Facts.Any(f => f.Class == HrTimeKindClass.Absence && f.Value > 0);
        return Result(HrDayStatus.Absent, verifixAbsence ? "нет прихода; Verifix: отсутствие" : "нет прихода в рабочий день");
    }

    /// <summary>Минуты от прихода до ухода без пересечения с перерывом по графику.</summary>
    public static int? Worked(DateTime? firstIn, DateTime? lastOut, DateTime? breakBegin, DateTime? breakEnd)
    {
        if (firstIn is not { } start || lastOut is not { } end || end <= start)
        {
            return null;
        }

        var total = end - start;
        if (breakBegin is { } bb && breakEnd is { } be && be > bb)
        {
            var overlapStart = bb > start ? bb : start;
            var overlapEnd = be < end ? be : end;
            if (overlapEnd > overlapStart)
            {
                total -= overlapEnd - overlapStart;
            }
        }

        return (int)Math.Round(total.TotalMinutes);
    }
}
