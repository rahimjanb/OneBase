using OneBase.Application.Hr;
using OneBase.Domain.Hr;
using OneBase.Infrastructure.Hr;

namespace OneBase.AI.Tests;

/// <summary>
/// Расчёт дня посещаемости: опоздание и ранний уход по графику табеля, отработанное время без перерыва, смена через полночь,
/// отсутствие — только в рабочий день, без документа и при полных данных; одна отметка — не рабочий день; часовой пояс организации.
/// </summary>
public class HrAttendanceTests
{
    private static readonly DateOnly Day = new(2026, 10, 21);
    private static readonly HrRules Rules = new(VerifixFormats.Tashkent, LateGraceMinutes: 5, EarlyLeaveGraceMinutes: 0);
    private static readonly HrEmployeeFacts Working = new(HrEmployeeStatus.Working, new DateOnly(2025, 1, 15), null);
    private static readonly HrCoverage Full = new(new DateTime(2026, 10, 22, 8, 0, 0), new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 22));

    private static DateTime T(int hour, int minute = 0, int day = 21) => new(2026, 10, day, hour, minute, 0);

    private static HrTimesheetFacts Workday(DateTime? firstIn = null, DateTime? lastOut = null, params HrFact[] facts) =>
        new("W", T(9), T(18), T(13), T(14), 480, firstIn, lastOut, facts);

    private static HrDayResult Eval(HrTimesheetFacts? sheet, HrEventSummary? events = null, HrCoverage? coverage = null, DateTime? now = null,
        HrEmployeeFacts? employee = null) =>
        HrAttendance.Evaluate(Day, employee ?? Working, sheet, events ?? HrEventSummary.None, coverage ?? Full, now ?? T(20), Rules);

    [Fact]
    public void Late_arrival_beyond_grace_counts_minutes_and_worked_time_excludes_the_break()
    {
        var day = Eval(Workday(T(9, 12), T(18, 5)));

        Assert.Equal(HrDayStatus.Present, day.Status);
        Assert.True(day.Late);
        Assert.Equal(12, day.LateMinutes);
        Assert.False(day.EarlyLeave);
        Assert.Equal(8 * 60 + 53 - 60, day.WorkedMinutes); // 09:12–18:05 минус час перерыва
        Assert.Equal("timesheet", day.Source);
    }

    [Fact]
    public void Arrival_within_grace_is_not_late()
    {
        var day = Eval(Workday(T(9, 4), T(18)));

        Assert.False(day.Late);
        Assert.Null(day.LateMinutes);
    }

    [Fact]
    public void Early_leave_only_after_the_shift_is_over()
    {
        var finished = Eval(Workday(T(8, 55), T(17, 20)), now: T(19));
        Assert.True(finished.EarlyLeave);
        Assert.Equal(40, finished.EarlyLeaveMinutes);

        // Смена ещё идёт: «последний уход» в 12:00 — обед, а не ранний уход.
        var running = Eval(Workday(T(8, 55), T(12)), now: T(12, 30));
        Assert.False(running.EarlyLeave);
        Assert.False(running.NoExit);
    }

    [Fact]
    public void Missing_exit_after_shift_is_flagged()
    {
        var day = Eval(Workday(T(8, 50)), now: T(19));

        Assert.Equal(HrDayStatus.Present, day.Status);
        Assert.True(day.NoExit);
        Assert.Null(day.WorkedMinutes);
    }

    [Fact]
    public void Absence_needs_a_workday_without_documents_and_complete_track_data()
    {
        Assert.Equal(HrDayStatus.Absent, Eval(Workday()).Status);

        // Данные отметок загружены только до 08:30 — рано говорить «отсутствует».
        var partial = Eval(Workday(), coverage: Full with { TracksThrough = T(8, 30) });
        Assert.Equal(HrDayStatus.Unknown, partial.Status);

        // Отметки ещё не загружались вовсе.
        Assert.Equal(HrDayStatus.Unknown, Eval(Workday(), coverage: Full with { TracksThrough = null }).Status);

        // Отпуск по табелю — не отсутствие.
        var leave = Eval(Workday(null, null, new HrFact(HrTimeKindClass.Leave, "ОТ", "Отпуск", 480)));
        Assert.Equal(HrDayStatus.OnLeave, leave.Status);
        Assert.Equal("ОТ", leave.LeaveCode);

        // Выходной по графику.
        Assert.Equal(HrDayStatus.DayOff, Eval(new HrTimesheetFacts("R", null, null, null, null, 0, null, null, [])).Status);
    }

    [Fact]
    public void Before_start_plus_grace_the_day_is_expected_not_absent()
    {
        var early = Eval(Workday(), now: T(9, 3), coverage: Full with { TracksThrough = T(9, 3) });
        Assert.Equal(HrDayStatus.Expected, early.Status);

        var later = Eval(Workday(), now: T(9, 30), coverage: Full with { TracksThrough = T(9, 29) });
        Assert.Equal(HrDayStatus.Absent, later.Status);
    }

    [Fact]
    public void A_single_event_without_confirmed_entry_is_not_a_workday()
    {
        // Отметка присутствия (тип C) без прихода — не «пришёл» и не «отсутствует».
        var day = Eval(Workday(), new HrEventSummary(1, null, null));

        Assert.Equal(HrDayStatus.Unknown, day.Status);
        Assert.Null(day.FirstIn);
    }

    [Fact]
    public void Entry_event_is_a_fallback_when_the_timesheet_has_no_input_yet()
    {
        var day = Eval(Workday(), new HrEventSummary(2, T(9, 20), T(13, 5)), now: T(13, 10));

        Assert.Equal(HrDayStatus.Present, day.Status);
        Assert.Equal("events", day.Source);
        Assert.Equal(20, day.LateMinutes);
    }

    [Fact]
    public void No_schedule_differs_from_timesheet_not_loaded()
    {
        Assert.Equal(HrDayStatus.NoSchedule, Eval(null).Status);
        Assert.Equal(HrDayStatus.Unknown, Eval(null, coverage: Full with { TimesheetFrom = null, TimesheetTo = null }).Status);

        // Пришёл без графика — присутствие есть, опоздание не считается.
        var present = Eval(null, new HrEventSummary(2, T(10), T(17)));
        Assert.Equal(HrDayStatus.Present, present.Status);
        Assert.False(present.Late);
    }

    [Fact]
    public void Night_shift_crossing_midnight_is_one_day()
    {
        var sheet = new HrTimesheetFacts("W", T(22), T(6, 0, 22), T(2, 0, 22), T(2, 30, 22), 450, T(22, 10), T(6, 5, 22), []);
        var day = Eval(sheet, now: T(7, 0, 22));

        Assert.Equal(HrDayStatus.Present, day.Status);
        Assert.Equal(10, day.LateMinutes);
        Assert.False(day.EarlyLeave);
        Assert.Equal(7 * 60 + 55 - 30, day.WorkedMinutes);
    }

    [Fact]
    public void Not_employed_days_are_excluded()
    {
        var hiredLater = new HrEmployeeFacts(HrEmployeeStatus.Working, new DateOnly(2026, 10, 25), null);
        Assert.Equal(HrDayStatus.NotEmployed, Eval(Workday(), employee: hiredLater).Status);

        var dismissed = new HrEmployeeFacts(HrEmployeeStatus.Dismissed, new DateOnly(2025, 1, 1), new DateOnly(2026, 10, 20));
        Assert.Equal(HrDayStatus.NotEmployed, Eval(Workday(), employee: dismissed).Status);

        // День увольнения — ещё рабочий.
        var lastDay = new HrEmployeeFacts(HrEmployeeStatus.Dismissed, new DateOnly(2025, 1, 1), Day);
        Assert.Equal(HrDayStatus.Absent, Eval(Workday(), employee: lastDay).Status);
    }

    [Fact]
    public void Day_codes_follow_the_timesheet_letters()
    {
        Assert.Equal("ОП", HrService.DayCode(Eval(Workday(T(9, 30), T(18)))));
        Assert.Equal("Я", HrService.DayCode(Eval(Workday(T(9), T(18)))));
        Assert.Equal("ОТС", HrService.DayCode(Eval(Workday())));
        Assert.Equal("Б", HrService.DayCode(Eval(Workday(null, null, new HrFact(HrTimeKindClass.Leave, "Б", "Больничный", 480)))));
        Assert.Equal("—", HrService.DayCode(Eval(null)));
    }

    [Theory]
    [InlineData("Я", "Явка", HrTimeKindClass.Presence)]
    [InlineData("ОП", "Опоздание", HrTimeKindClass.Late)]
    [InlineData("РУ", "Ранний Уход", HrTimeKindClass.EarlyLeave)]
    [InlineData("ОТС", "Отсутствие", HrTimeKindClass.Absence)]
    [InlineData("ОТ", "Отпуск", HrTimeKindClass.Leave)]
    [InlineData("Б", "Больничный", HrTimeKindClass.Leave)]
    [InlineData("К", "Командировка", HrTimeKindClass.Leave)]
    [InlineData("ПО", "Почасовой Отгул", HrTimeKindClass.PartialLeave)]
    [InlineData("X1", "Ежегодный отпуск", HrTimeKindClass.Leave)]
    [InlineData("X2", "Что-то своё", HrTimeKindClass.Other)]
    public void Time_kinds_are_classified_by_letter_code_then_name(string code, string name, HrTimeKindClass expected) =>
        Assert.Equal(expected, HrTimeKinds.Classify(code, name));

    [Fact]
    public void Verifix_times_are_parsed_as_organization_local_time_and_converted_to_utc()
    {
        var local = VerifixFormats.ParseLocal("21.10.2026 09:05:30");
        Assert.Equal(new DateTime(2026, 10, 21, 9, 5, 30), local);
        Assert.Equal(DateTimeKind.Unspecified, local!.Value.Kind);
        Assert.Equal(new DateTimeOffset(2026, 10, 21, 4, 5, 30, TimeSpan.Zero), VerifixFormats.ToUtc(local.Value, VerifixFormats.Tashkent));
        // Ночная отметка 00:30 по Ташкенту — ещё предыдущие сутки по UTC, но дата дня — местная.
        Assert.Equal(new DateTimeOffset(2026, 10, 20, 19, 30, 0, TimeSpan.Zero), VerifixFormats.ToUtc(new DateTime(2026, 10, 21, 0, 30, 0), VerifixFormats.Tashkent));

        Assert.Equal(new DateOnly(2025, 1, 15), VerifixFormats.ParseDate("15.01.2025"));
        Assert.Equal(new DateTime(2025, 1, 1), VerifixFormats.ParseLocal("01.01.2025")); // запрос на целый день — только дата
        Assert.Null(VerifixFormats.ParseLocal("2025/01/01 10:00"));
        Assert.Equal("21.10.2026 09:05:30", VerifixFormats.DateTime(new DateTime(2026, 10, 21, 9, 5, 30)));
    }

    [Fact]
    public void Schedule_retries_failed_runs_a_limited_number_of_times()
    {
        var now = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);
        HrSyncRun Run(string status, int minutesAgo) =>
            new() { Trigger = HrSyncTrigger.Auto, Status = status, StartedAt = now.AddMinutes(-minutesAgo), FinishedAt = now.AddMinutes(-minutesAgo + 1) };

        Assert.True(HrSyncSchedule.Due([], 60, now).Due);
        Assert.False(HrSyncSchedule.Due([Run(HrSyncStatus.Succeeded, 30)], 60, now).Due);
        Assert.True(HrSyncSchedule.Due([Run(HrSyncStatus.Succeeded, 61)], 60, now).Due);
        Assert.False(HrSyncSchedule.Due([Run(HrSyncStatus.Running, 120)], 60, now).Due);

        // Неудача — повтор через 5 минут с отметкой «повтор».
        var failed = Run(HrSyncStatus.Failed, 7);
        var (due, retryOf) = HrSyncSchedule.Due([failed], 60, now);
        Assert.True(due);
        Assert.Equal(failed.Id, retryOf);
        Assert.False(HrSyncSchedule.Due([Run(HrSyncStatus.Failed, 3)], 60, now).Due);

        // Четыре неудачи подряд — дальше обычный интервал, а не каждые 5 минут.
        List<HrSyncRun> many = [Run(HrSyncStatus.Failed, 10), Run(HrSyncStatus.Failed, 20), Run(HrSyncStatus.Failed, 30), Run(HrSyncStatus.Failed, 40)];
        Assert.False(HrSyncSchedule.Due(many, 60, now).Due);
        Assert.Null(HrSyncSchedule.Due(many, 60, now).RetryOf);
    }
}
