using OneBase.AI.Work;
using OneBase.Application.Hr;
using OneBase.Application.Security;
using OneBase.Application.Work;
using OneBase.Domain.Work;

namespace OneBase.AI.Tests;

/// <summary>
/// «Мой день» — чистые правила: перерыв 13:00–14:00, когда в графике его нет; статус смены по последней отметке; отработано на текущий
/// момент; срок задачи со временем; личная эффективность; разбор плана от AI (только ссылки и числа из входа); роль «Сотрудник».
/// </summary>
public class MyDayTests
{
    private static readonly DateOnly Day = new(2026, 10, 30);
    private static DateTime At(int h, int m) => Day.ToDateTime(new TimeOnly(h, m));

    private static HrTimesheetFacts Sheet(DateTime? begin, DateTime? end, int? plan, DateTime? breakBegin = null, DateTime? breakEnd = null, string kind = "W") =>
        new(kind, begin, end, breakBegin, breakEnd, plan, null, null, []);

    [Fact]
    public void Default_break_is_13_to_14_when_the_schedule_has_none_and_the_plan_shrinks()
    {
        var day = HrMyDay.WithBreak(Sheet(At(9, 0), At(18, 0), 540), Day);
        Assert.Equal((At(13, 0), At(14, 0), 480), (day.BreakBegin!.Value, day.BreakEnd!.Value, day.PlanMinutes!.Value));

        // Свой перерыв из графика — без изменений.
        var own = Sheet(At(9, 0), At(18, 0), 480, At(12, 30), At(13, 30));
        Assert.Same(own, HrMyDay.WithBreak(own, Day));

        // Смена до обеда и выходной — без перерыва.
        Assert.Null(HrMyDay.WithBreak(Sheet(At(6, 0), At(12, 0), 360), Day).BreakBegin);
        Assert.Null(HrMyDay.WithBreak(Sheet(null, null, 0, kind: "R"), Day).BreakBegin);
    }

    private static HrDayResult Present(DateTime? firstIn, DateTime? lastOut = null, int? late = null, HrDayStatus status = HrDayStatus.Present) =>
        new(Day, status, "W", At(9, 0), At(18, 0), 480, firstIn, lastOut, 2, late is > 0, late, false, null,
            HrAttendance.Worked(firstIn, lastOut, At(13, 0), At(14, 0)), false, false, null, null, "events", null);

    [Fact]
    public void Shift_status_follows_the_last_mark()
    {
        var bb = At(13, 0);
        var be = At(14, 0);
        Assert.Equal("at_work", HrMyDay.Status(Present(At(8, 52)), new HrMark(At(8, 52), "I"), bb, be, At(10, 24)));
        Assert.Equal("on_break", HrMyDay.Status(Present(At(8, 52), At(13, 2)), new HrMark(At(13, 2), "O"), bb, be, At(13, 20)));
        Assert.Equal("on_break", HrMyDay.Status(Present(At(8, 52)), new HrMark(At(13, 2), "T"), bb, be, At(13, 20)));
        Assert.Equal("out", HrMyDay.Status(Present(At(8, 52), At(15, 0)), new HrMark(At(15, 0), "O"), bb, be, At(15, 30)));
        Assert.Equal("finished", HrMyDay.Status(Present(At(8, 52), At(18, 5)), new HrMark(At(18, 5), "O"), bb, be, At(18, 10)));
        Assert.Equal("at_work", HrMyDay.Status(Present(At(8, 52), At(13, 5)), new HrMark(At(14, 1), "I"), bb, be, At(15, 0)));
        Assert.Equal("not_started", HrMyDay.Status(Present(null, status: HrDayStatus.Expected), null, bb, be, At(8, 30)));
        Assert.Equal("day_off", HrMyDay.Status(Present(null, status: HrDayStatus.DayOff), null, bb, be, At(10, 0)));
        Assert.Equal("absent", HrMyDay.Status(Present(null, status: HrDayStatus.Absent), null, bb, be, At(11, 0)));
    }

    [Fact]
    public void Worked_so_far_counts_to_now_without_the_break()
    {
        var day = Present(At(8, 52));
        Assert.Equal(92, HrMyDay.WorkedSoFar(day, "at_work", new HrMark(At(8, 52), "I"), At(13, 0), At(14, 0), At(10, 24)));
        // Во время обеда счёт стоит: 08:52–13:00.
        Assert.Equal(248, HrMyDay.WorkedSoFar(day, "on_break", new HrMark(At(13, 0), "O"), At(13, 0), At(14, 0), At(13, 40)));
        // Ушёл в 18:05 — 9 ч 13 м минус обед.
        Assert.Equal(493, HrMyDay.WorkedSoFar(Present(At(8, 52), At(18, 5)), "finished", new HrMark(At(18, 5), "O"), At(13, 0), At(14, 0), At(19, 0)));
    }

    [Fact]
    public void Week_cell_status_and_monday()
    {
        Assert.Equal("late", HrMyDay.WeekStatus(Present(At(9, 12), At(18, 10), late: 12), Day));
        Assert.Equal("ok", HrMyDay.WeekStatus(Present(At(8, 47), At(18, 5)), Day));
        Assert.Equal("future", HrMyDay.WeekStatus(Present(null, status: HrDayStatus.Unknown), Day.AddDays(-1)));
        Assert.Equal(new DateOnly(2026, 10, 26), HrMyDay.Monday(new DateOnly(2026, 11, 1)));
        Assert.Equal(new DateOnly(2026, 10, 26), HrMyDay.Monday(new DateOnly(2026, 10, 26)));
    }

    [Fact]
    public void Due_time_makes_a_task_overdue_the_same_day()
    {
        var today = new DateOnly(2026, 10, 31);
        Assert.True(WorkTaskService.IsOverdue(WorkTaskStatus.InProgress, today, new TimeOnly(15, 0), today, new TimeOnly(15, 0)));
        Assert.False(WorkTaskService.IsOverdue(WorkTaskStatus.InProgress, today, new TimeOnly(15, 0), today, new TimeOnly(14, 59)));
        // Без времени — до конца дня.
        Assert.False(WorkTaskService.IsOverdue(WorkTaskStatus.New, today, null, today, new TimeOnly(23, 59)));
        Assert.True(WorkTaskService.IsOverdue(WorkTaskStatus.Review, today.AddDays(-1), null, today, new TimeOnly(0, 1)));
        Assert.False(WorkTaskService.IsOverdue(WorkTaskStatus.Done, today.AddDays(-3), null, today, new TimeOnly(12, 0)));
    }

    [Fact]
    public void Efficiency_counts_on_time_by_submission_and_weeks_by_completion()
    {
        var monday = new DateOnly(2026, 9, 21);
        DateTimeOffset Local(int month, int day, int hour) => new(2026, month, day, hour, 0, 0, TimeSpan.FromHours(5));
        var done = new List<WorkTaskService.MyDone>
        {
            // Сдал на проверку 30.10 в 14:00 при сроке «30.10 до 15:00», принят 31.10 — в срок.
            new(Local(10, 31, 10), Local(10, 30, 14), new DateOnly(2026, 10, 30), new TimeOnly(15, 0)),
            // Выполнен без проверки 29.10, срок 28.10 — не в срок.
            new(Local(10, 29, 12), null, new DateOnly(2026, 10, 28), null),
            // Без срока — в процент не входит.
            new(Local(10, 27, 12), null, null, null),
            new(Local(10, 20, 12), null, new DateOnly(2026, 10, 25), null),
        };

        var e = WorkTaskService.Efficiency(done, monday);

        Assert.Equal((2, 3, 67), (e.OnTime, e.ClosedWithDue, e.OnTimePct!.Value));
        Assert.Equal(6, e.Weeks.Count);
        Assert.Equal(3, e.DoneThisWeek);
        Assert.Equal(1, e.DoneLastWeek);
        Assert.Equal(44, e.Weeks[^1].Number);
        Assert.Null(WorkTaskService.Efficiency([new(Local(10, 27, 12), null, null, null)], monday).OnTimePct);
    }

    private static DailyPlanService.PlanInput Input() => new(
        """{"tasks":[{"ref":"t1","title":"Отчёт","dueTime":"15:00"}],"counts":{"open":4}}""",
        new Dictionary<string, string> { ["t1"] = "/tasks/task/1", ["e1"] = "/calendar?date=2026-11-02&event=2" },
        new HashSet<string> { "15", "00", "4" },
        "hash",
        false);

    [Fact]
    public void Plan_keeps_only_grounded_items_with_links_from_the_input()
    {
        const string answer = """
            ```json
            {"items":[
              {"text":"Начните с отчёта — срок сегодня в 15:00","tone":"warning","ref":"t1"},
              {"text":"Обновите статусы 4 задач","tone":"info","ref":"zz"},
              {"text":"Закройте 7 задач до вечера","tone":"danger","ref":"t1"},
              {"text":"Встреча 2 ноября","tone":"loud","ref":"e1"},
              {"text":"Четвёртая","tone":"info","ref":null}
            ]}
            ```
            """;

        var items = DailyPlanService.Parse(answer, Input());

        Assert.Equal(3, items.Count);
        Assert.Equal(new DailyPlanItem("Начните с отчёта — срок сегодня в 15:00", "warning", "/tasks/task/1"), items[0]);
        // Ссылки не на объект входа — без ссылки; число, которого нет во входе (7), — рекомендация отброшена; тон не из списка — info.
        Assert.Null(items[1].Href);
        Assert.Equal(("info", "/calendar?date=2026-11-02&event=2"), (items[2].Tone, items[2].Href));
        Assert.Empty(DailyPlanService.Parse("Просто текст", Input()));
    }

    [Fact]
    public void Employee_role_has_tasks_files_and_consultant_without_management()
    {
        var role = SystemRoles.All.Single(r => r.Name == SystemRoles.EmployeeRole);
        Assert.Null(role.DepartmentCode);
        // Свои заявки на закупку (раздел «Заявки») — у всех сотрудников; все заявки компании — нет.
        Assert.Equal(new[] { Permissions.AgentsRun, Permissions.FilesRead, Permissions.FilesWrite, Permissions.WorkTasks, Permissions.ProcurementRequest }.Order(),
            role.Permissions.Order());
        Assert.DoesNotContain(Permissions.ProcurementRead, role.Permissions);
        Assert.DoesNotContain(Permissions.UsersManage, role.Permissions);
        Assert.DoesNotContain(Permissions.HrRead, role.Permissions);
    }

    [Fact]
    public void Calendar_audience_rules()
    {
        var employee = new WorkContext(Guid.NewGuid(), new HashSet<string> { Permissions.WorkTasks }, Guid.NewGuid());
        var head = employee with { HeadOf = new HashSet<Guid> { employee.DepartmentId!.Value } };
        var hr = employee with { Permissions = new HashSet<string> { Permissions.HrRead } };
        Assert.False(CalendarService.CanAddForEveryone(employee));
        Assert.False(CalendarService.CanAddForDepartment(employee, employee.DepartmentId!.Value));
        Assert.True(CalendarService.CanAddForDepartment(head, employee.DepartmentId!.Value));
        Assert.True(CalendarService.CanAddForEveryone(hr));
        var e = new CalendarEvent { Title = "x", CreatedById = employee.UserId };
        Assert.True(CalendarService.CanEdit(employee, e));
        Assert.False(CalendarService.CanEdit(head with { UserId = Guid.NewGuid() }, e));
    }
}
