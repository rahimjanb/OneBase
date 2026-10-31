namespace OneBase.Application.Hr;

/// <summary>
/// Рабочий день сотрудника для «Моего дня». Available = false — учёт недоступен: UnavailableReason not_connected (Verifix не подключён) или
/// no_employee_link (пользователь не привязан к сотруднику Verifix). Время — местное время организации.
/// </summary>
public sealed record HrMyWorkday(
    bool Available,
    string? UnavailableReason,
    DateTime Now,
    HrMyToday? Today,
    IReadOnlyList<HrMyWeekDay> Week,
    HrMyWeekTotals? Totals,
    DateTimeOffset? SyncedAt,
    DateTime? TracksThrough);

/// <summary>
/// Сегодня. Status: not_started, at_work, on_break, out (вышел среди дня), finished, day_off, on_leave, absent, no_schedule, unclear, not_employed.
/// Departure — только когда день закончен; WorkedMinutes — на текущий момент.
/// </summary>
public sealed record HrMyToday(
    string Status,
    DateTime? ShiftStart,
    DateTime? ShiftEnd,
    DateTime? Arrival,
    DateTime? Departure,
    DateTime? LastMarkAt,
    int? WorkedMinutes,
    int? PlannedMinutes,
    int? LateMinutes,
    int? EarlyLeaveMinutes,
    HrMyBreak? Break,
    string? LeaveName,
    string? Reason);

/// <summary>Перерыв по графику; FromSchedule = false — перерыв компании по умолчанию (13:00–14:00).</summary>
public sealed record HrMyBreak(DateTime Start, DateTime End, int Minutes, bool FromSchedule);

/// <summary>День недели. Status: ok, late, early_leave, absent, leave, day_off, not_started, no_schedule, unclear, not_employed, future.</summary>
public sealed record HrMyWeekDay(DateOnly Date, string Status, DateTime? Arrival, DateTime? Departure, int? WorkedMinutes, int? LateMinutes, DateTime? ShiftStart,
    DateTime? ShiftEnd);

public sealed record HrMyWeekTotals(int WorkedMinutes, int LateCount, int LateMinutes);

/// <summary>Последняя отметка дня: время и тип Verifix (I — вход, O — выход, T — ушёл на перерыв, N — вернулся).</summary>
public sealed record HrMark(DateTime Time, string Type);

/// <summary>Правила «Моего дня» поверх расчёта HrAttendance (чистые функции — проверяются тестами).</summary>
public static class HrMyDay
{
    /// <summary>Перерыв компании, когда в графике дня перерыв не задан.</summary>
    public static readonly TimeOnly DefaultBreakBegin = new(13, 0);

    public static readonly TimeOnly DefaultBreakEnd = new(14, 0);

    /// <summary>
    /// День табеля с перерывом: свой из графика, иначе 13:00–14:00 — если рабочая смена его захватывает; план дня уменьшается на перерыв
    /// (смена 09:00–18:00 без перерыва в графике — 8 ч).
    /// </summary>
    public static HrTimesheetFacts WithBreak(HrTimesheetFacts sheet, DateOnly date)
    {
        if (sheet.BreakBegin is not null && sheet.BreakEnd is not null)
        {
            return sheet;
        }

        if (sheet.DayKind != "W" || sheet.PlanBegin is not { } begin || sheet.PlanEnd is not { } end || end <= begin)
        {
            return sheet;
        }

        var breakBegin = date.ToDateTime(DefaultBreakBegin);
        var breakEnd = date.ToDateTime(DefaultBreakEnd);
        var overlap = Overlap(begin, end, breakBegin, breakEnd);
        if (overlap <= 0)
        {
            return sheet;
        }

        return sheet with
        {
            BreakBegin = breakBegin,
            BreakEnd = breakEnd,
            PlanMinutes = sheet.PlanMinutes is { } plan ? Math.Max(0, plan - overlap) : null,
        };
    }

    /// <summary>Что сейчас со сменой сотрудника — по итогу дня и последней отметке.</summary>
    public static string Status(HrDayResult day, HrMark? last, DateTime? breakBegin, DateTime? breakEnd, DateTime now)
    {
        switch (day.Status)
        {
            case HrDayStatus.Present:
                break;
            case HrDayStatus.DayOff:
                return "day_off";
            case HrDayStatus.OnLeave:
                return "on_leave";
            case HrDayStatus.Absent:
                return "absent";
            case HrDayStatus.Expected:
                return "not_started";
            case HrDayStatus.NoSchedule:
                return "no_schedule";
            case HrDayStatus.NotEmployed:
                return "not_employed";
            default:
                return last is null && day.PlanBegin is { } start && now < start ? "not_started" : "unclear";
        }

        var lastType = last?.Type;
        var exitAt = last is { Type: "O" or "T" } ? last.Time : (DateTime?)null;
        if (last is null)
        {
            // Только табель: без ухода — на работе.
            if (day.LastOut is not { } outAt)
            {
                return "at_work";
            }

            exitAt = outAt;
            lastType = "O";
        }

        if (lastType is not ("O" or "T") || exitAt is not { } exit)
        {
            return "at_work";
        }

        if (lastType == "T")
        {
            return "on_break";
        }

        if (breakBegin is { } bb && breakEnd is { } be && exit >= bb.AddMinutes(-15) && exit < be && now < be.AddMinutes(30))
        {
            return "on_break";
        }

        return day.PlanEnd is { } end && (exit >= end.AddMinutes(-60) || now >= end) ? "finished" : "out";
    }

    /// <summary>Отработано на текущий момент: от прихода до «сейчас» (на работе, на перерыве) или до последнего выхода — без перерыва по графику.</summary>
    public static int? WorkedSoFar(HrDayResult day, string status, HrMark? last, DateTime? breakBegin, DateTime? breakEnd, DateTime now)
    {
        if (day.FirstIn is not { } start)
        {
            return null;
        }

        DateTime? end = status switch
        {
            "at_work" or "on_break" => now,
            "out" => last?.Time ?? day.LastOut,
            _ => day.LastOut,
        };
        return end is { } e && e > start ? HrAttendance.Worked(start, e, breakBegin, breakEnd) : day.WorkedMinutes;
    }

    /// <summary>Итог дня недели для полосы «Эта неделя».</summary>
    public static string WeekStatus(HrDayResult day, DateOnly today)
    {
        if (day.Date > today)
        {
            return day.DayKind is { } kind && kind != "W" ? "day_off" : "future";
        }

        return day.Status switch
        {
            HrDayStatus.Present => day.Late ? "late" : day.EarlyLeave ? "early_leave" : "ok",
            HrDayStatus.Absent => "absent",
            HrDayStatus.OnLeave => "leave",
            HrDayStatus.DayOff => "day_off",
            HrDayStatus.Expected => "not_started",
            HrDayStatus.NoSchedule => "no_schedule",
            HrDayStatus.NotEmployed => "not_employed",
            _ => "unclear",
        };
    }

    /// <summary>Понедельник недели даты.</summary>
    public static DateOnly Monday(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    private static int Overlap(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd)
    {
        var start = aStart > bStart ? aStart : bStart;
        var end = aEnd < bEnd ? aEnd : bEnd;
        return end > start ? (int)Math.Round((end - start).TotalMinutes) : 0;
    }
}
