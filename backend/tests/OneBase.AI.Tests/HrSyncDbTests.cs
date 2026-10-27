using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Tools;
using OneBase.AI.Tools.Data;
using OneBase.Application.Field;
using OneBase.Application.Hr;
using OneBase.Application.Security;
using OneBase.Domain.Hr;
using OneBase.Domain.Identity;
using OneBase.Domain.Work;
using OneBase.Infrastructure.Hr;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// HR и Verifix на настоящем PostgreSQL (ONEBASE_TEST_POSTGRES, иначе пропускается) и подставном Verifix (FakeVerifix — без сети):
/// загрузка справочников, сотрудников, отметок и табеля; повторная загрузка без дубликатов; локальные поля не перезаписываются;
/// пропавший сотрудник не удаляется; часовой пояс; посещаемость с учётом графика, отпуска и полноты данных; права HR, руководителя отдела
/// и постороннего; защита от параллельного запуска; ошибки авторизации, лимиты, сбои шага и повтор неудачной синхронизации.
/// </summary>
public class HrSyncDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    /// <summary>Среда 21.10.2026, 10:30 по Ташкенту.</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 5, 30, 0, TimeSpan.Zero);

    private static readonly DateOnly Today = new(2026, 10, 21);
    private static readonly DateOnly Yesterday = Today.AddDays(-1);
    private static readonly IReadOnlySet<string> HrPermissions = new HashSet<string> { Permissions.HrRead };
    private static readonly IReadOnlySet<string> Employee = new HashSet<string> { Permissions.FilesRead };

    private FakeVerifix Verifix => fixture.Verifix;

    /// <summary>
    /// Пустые таблицы HR, настройки подключения и стандартный набор: 101 — пришёл вовремя, 102 (дочернее подразделение) — нет прихода,
    /// 103 — в отпуске, 104 — уволен в начале месяца, 105 — опоздал на 40 минут.
    /// </summary>
    private async Task SeedAsync(string secret = FakeVerifix.ClientSecret, bool enabled = true)
    {
        fixture.Clock.Now = Now;
        Verifix.Clear();
        Verifix.SeedReferences();
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                "TRUNCATE hr.\"AttendanceEvents\", hr.\"TimesheetDays\", hr.\"Employees\", hr.\"References\", hr.\"SyncRuns\", hr.\"SyncStates\" RESTART IDENTITY CASCADE");
            await scope.ServiceProvider.GetRequiredService<VerifixSettingsStore>().SaveAsync(
                new VerifixSettingsInput(FakeVerifix.BaseUrl, FakeVerifix.ClientId, secret, null, enabled, true, 60, 7, 3, 5, 0), fixture.AdminId);
        }

        Verifix.Employees.AddRange([
            FakeVerifix.Employee(101, "Бобур", "Алиев", 901),
            FakeVerifix.Employee(102, "Дилноза", "Каримова", 902),
            FakeVerifix.Employee(103, "Сардор", "Юсупов", 903),
            FakeVerifix.Employee(104, "Пётр", "Уволенный", 901, "D", "15.01.2025", "01.10.2026"),
            FakeVerifix.Employee(105, "Тимур", "Расулов", 901),
        ]);
        Verifix.Tracks.AddRange([
            FakeVerifix.Track(5001, 101, "21.10.2026 08:55:00"),
            FakeVerifix.Track(5002, 105, "21.10.2026 09:40:00"),
            FakeVerifix.Track(5003, 101, "21.10.2026 10:00:00", "C"),
            FakeVerifix.Track(5004, 101, "20.10.2026 09:20:00"),
            FakeVerifix.Track(5005, 101, "20.10.2026 18:00:00", "O"),
        ]);
        Verifix.Timesheet.AddRange([
            FakeVerifix.Staff(101, 901, FakeVerifix.Day(Yesterday, input: "09:20:00", output: "18:00:00"), FakeVerifix.Day(Today, input: "08:55:00")),
            FakeVerifix.Staff(102, 902, FakeVerifix.Day(Yesterday, input: "08:58:00", output: "18:02:00"), FakeVerifix.Day(Today)),
            FakeVerifix.Staff(103, 903, FakeVerifix.Day(Yesterday, facts: (91, 480)), FakeVerifix.Day(Today, facts: (91, 480))),
            FakeVerifix.Staff(105, 901, FakeVerifix.Day(Yesterday, input: "09:00:00", output: "18:00:00"), FakeVerifix.Day(Today, input: "09:40:00")),
        ]);
    }

    private async Task<HrSyncRun> SyncAsync(string trigger = HrSyncTrigger.Manual, Guid? retryOf = null)
    {
        var (result, runId) = await fixture.Services.GetRequiredService<HrSyncCoordinator>().RunAsync(trigger, fixture.AdminId, retryOf, CancellationToken.None);
        Assert.Equal(HrSyncStartResult.Started, result);
        using var scope = fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OneBaseDbContext>().HrSyncRuns.AsNoTracking().FirstAsync(r => r.Id == runId);
    }

    private async Task<T> WithAsync<T>(Func<IServiceProvider, Task<T>> read)
    {
        using var scope = fixture.Services.CreateScope();
        return await read(scope.ServiceProvider);
    }

    private Task<HrContext> HrContextAsync(IServiceProvider sp, Guid? user = null, IReadOnlySet<string>? permissions = null) =>
        sp.GetRequiredService<HrService>().ContextAsync(user ?? fixture.AdminId, permissions ?? HrPermissions, CancellationToken.None);

    [PostgresFact]
    public async Task Sync_imports_references_employees_tracks_and_timesheet_with_local_and_utc_times()
    {
        await SeedAsync();
        var run = await SyncAsync();

        Assert.Equal(HrSyncStatus.Succeeded, run.Status);
        Assert.NotNull(run.FinishedAt);
        Assert.True(run.Created > 0);
        Assert.Equal(0, run.Errors);
        Assert.Equal(new DateOnly(2026, 10, 15), run.PeriodFrom); // HistoryDays = 7
        Assert.Equal(Today, run.PeriodTo);

        await WithAsync(async sp =>
        {
            var db = sp.GetRequiredService<OneBaseDbContext>();
            Assert.Equal(5, await db.HrEmployees.CountAsync());
            var bobur = await db.HrEmployees.SingleAsync(e => e.VerifixId == 101);
            Assert.Equal("Алиев Бобур", bobur.FullName);
            Assert.Equal("998900000101", bobur.Phone);
            Assert.Equal(HrEmployeeStatus.Working, bobur.Status);
            Assert.Equal(new DateOnly(2025, 1, 15), bobur.HiringDate);
            Assert.Equal(901, bobur.DivisionId);
            Assert.Equal(721, bobur.JobId);
            Assert.Equal(20101, bobur.StaffId);
            Assert.Null(bobur.MissingSince);

            var track = await db.HrAttendanceEvents.SingleAsync(e => e.VerifixTrackId == 5001);
            Assert.Equal(bobur.Id, track.EmployeeId);
            Assert.Equal("21.10.2026 08:55:00", track.RawTime);
            Assert.Equal(new DateTime(2026, 10, 21, 8, 55, 0), track.LocalTime);
            Assert.Equal(new DateTimeOffset(2026, 10, 21, 3, 55, 0, TimeSpan.Zero), track.OccurredAt);
            Assert.Equal(Today, track.LocalDate);
            Assert.Equal("I", track.TrackType);
            Assert.Equal(5, await db.HrAttendanceEvents.CountAsync());

            var day = await db.HrTimesheetDays.SingleAsync(d => d.VerifixEmployeeId == 103 && d.Date == Today);
            Assert.Equal("W", day.DayKind);
            Assert.Equal(new DateTime(2026, 10, 21, 9, 0, 0), day.PlanBegin);
            Assert.Equal(480, day.PlanMinutes);
            Assert.Contains("91", day.FactsJson);

            Assert.Equal(3, await db.HrReferences.CountAsync(r => r.Kind == HrReferenceKinds.Division));
            Assert.Equal("ОТ", (await db.HrReferences.SingleAsync(r => r.Kind == HrReferenceKinds.TimeKind && r.VerifixId == 91)).Code);
            var tracks = await db.HrSyncStates.SingleAsync(s => s.Entity == HrSyncService.TracksState);
            Assert.Equal(new DateOnly(2026, 10, 15), tracks.CoveredFrom);
            Assert.Equal(Now, tracks.CoveredThrough);
            return 0;
        });

        // Отметки — по дню (7 запросов), табель — одним периодом; тело — по документации.
        var trackCalls = Verifix.CallsTo("core/track$list");
        Assert.Equal(7, trackCalls.Count);
        Assert.Contains(trackCalls, c => c.Body.Contains("\"begin_datetime\":\"15.10.2026 00:00:00\"") && c.Body.Contains("\"end_datetime\":\"15.10.2026 23:59:59\""));
        Assert.Contains(trackCalls, c => c.Body.Contains("\"end_datetime\":\"21.10.2026 10:30:00\""));
        Assert.Single(Verifix.CallsTo("core/timesheet$export"));

        // Чувствительные поля Verifix (ПИНФЛ, ИНН, паспорт, фото, номер карты, дата рождения) в модели OneBase не хранятся.
        var columns = typeof(HrEmployee).GetProperties().Select(p => p.Name.ToLowerInvariant()).ToList();
        Assert.DoesNotContain(columns, c => c.Contains("npin") || c.Contains("tin") || c.Contains("passport") || c.Contains("photo") || c.Contains("card")
                                            || c.Contains("birth") || c.Contains("gender"));
    }

    [PostgresFact]
    public async Task Reimport_does_not_duplicate_and_keeps_local_fields()
    {
        await SeedAsync();
        await SyncAsync();
        var bobur = await WithAsync(sp => sp.GetRequiredService<OneBaseDbContext>().HrEmployees.AsNoTracking().SingleAsync(e => e.VerifixId == 101));
        await WithAsync(async sp =>
        {
            await sp.GetRequiredService<HrService>().SaveNoteAsync(await HrContextAsync(sp), bobur.Id, "Пропуск выдан 01.10", CancellationToken.None);
            await sp.GetRequiredService<OneBaseDbContext>().HrReferences.Where(r => r.Kind == HrReferenceKinds.Division && r.VerifixId == 901)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.DepartmentCode, "sales"));
            return 0;
        });

        // В Verifix: новый телефон, тип отметки изменился (C → M), появилась новая отметка.
        Verifix.Employees[0]["phone_number"] = "998901112233";
        Verifix.Tracks[2]["track_type"] = "M";
        Verifix.Tracks.Add(FakeVerifix.Track(5006, 102, "21.10.2026 10:15:00"));
        fixture.Clock.Now = Now.AddMinutes(30);
        var second = await SyncAsync();

        Assert.Equal(HrSyncStatus.Succeeded, second.Status);
        Assert.True(second.Skipped > 0);
        await WithAsync(async sp =>
        {
            var db = sp.GetRequiredService<OneBaseDbContext>();
            Assert.Equal(6, await db.HrAttendanceEvents.CountAsync());
            Assert.Equal(1, await db.HrAttendanceEvents.CountAsync(e => e.VerifixTrackId == 5003));
            var changed = await db.HrAttendanceEvents.SingleAsync(e => e.VerifixTrackId == 5003);
            Assert.Equal("M", changed.TrackType);
            Assert.NotNull(changed.ChangedAt);
            Assert.Equal(5, await db.HrEmployees.CountAsync());
            var again = await db.HrEmployees.SingleAsync(e => e.VerifixId == 101);
            Assert.Equal(bobur.Id, again.Id);
            Assert.Equal("998901112233", again.Phone);
            Assert.Equal("Пропуск выдан 01.10", again.Note); // локальное поле OneBase не перезаписано
            Assert.Equal("sales", (await db.HrReferences.SingleAsync(r => r.Kind == HrReferenceKinds.Division && r.VerifixId == 901)).DepartmentCode);
            Assert.Equal(await db.HrTimesheetDays.CountAsync(), await db.HrTimesheetDays.Select(d => new { d.StaffId, d.Date }).Distinct().CountAsync());
            return 0;
        });

        var steps = await WithAsync(async sp => (await sp.GetRequiredService<OneBaseDbContext>().HrSyncRuns.AsNoTracking().SingleAsync(r => r.Id == second.Id)).StepsJson);
        Assert.Contains(System.Text.Json.JsonDocument.Parse(steps).RootElement.EnumerateArray(), s => s.GetProperty("step").GetString() == "employees" && s.GetProperty("updated").GetInt32() == 1);
    }

    [PostgresFact]
    public async Task Missing_employee_is_marked_not_deleted_and_dismissal_is_a_status()
    {
        await SeedAsync();
        await SyncAsync();

        var karimova = Verifix.Employees[1];
        Verifix.Employees.Remove(karimova);
        Verifix.Employees[3]["status"] = "D"; // 105 уволен сегодня
        Verifix.Employees[3]["dismissal_date"] = "21.10.2026";
        Verifix.Tracks.RemoveAll(t => (string?)t["track_id"] == "5003"); // отметку удалили в Verifix
        fixture.Clock.Now = Now.AddMinutes(10);
        var run = await SyncAsync();
        Assert.Equal(HrSyncStatus.Succeeded, run.Status);

        await WithAsync(async sp =>
        {
            var db = sp.GetRequiredService<OneBaseDbContext>();
            var missing = await db.HrEmployees.SingleAsync(e => e.VerifixId == 102);
            Assert.NotNull(missing.MissingSince);
            Assert.Equal(HrEmployeeStatus.Working, missing.Status);
            Assert.Equal(1, await db.HrAttendanceEvents.CountAsync(e => e.VerifixTrackId == 5003 && e.MissingSince != null)); // не удалена
            var dismissed = await db.HrEmployees.SingleAsync(e => e.VerifixId == 105);
            Assert.Equal(HrEmployeeStatus.Dismissed, dismissed.Status);
            Assert.Equal(Today, dismissed.DismissalDate);
            Assert.Equal(5, await db.HrEmployees.CountAsync());
            return 0;
        });

        // Вернулась в Verifix — отметка «нет в Verifix» снимается.
        Verifix.Employees.Add(karimova);
        fixture.Clock.Now = Now.AddMinutes(20);
        await SyncAsync();
        Assert.Null(await WithAsync(sp => sp.GetRequiredService<OneBaseDbContext>().HrEmployees.Where(e => e.VerifixId == 102).Select(e => e.MissingSince).SingleAsync()));
    }

    [PostgresFact]
    public async Task Overview_attendance_and_deviations_respect_schedule_leave_and_data_completeness()
    {
        await SeedAsync();
        await SyncAsync();

        var overview = await WithAsync(async sp => await sp.GetRequiredService<HrService>().OverviewAsync(await HrContextAsync(sp), CancellationToken.None));
        Assert.Equal(Today, overview.Date);
        Assert.Equal(4, overview.Active); // уволенный 01.10 не считается
        Assert.Equal(2, overview.Present);
        Assert.Equal(1, overview.Late);
        Assert.Equal(1, overview.Absent);
        Assert.Equal(1, overview.OnLeave);
        Assert.Equal(3, overview.EventsToday);
        Assert.True(overview.Sync.Configured);
        Assert.Contains(overview.Divisions, d => d.Id == 901 && d.Active == 2 && d.Late == 1);
        Assert.Contains(overview.Locations, l => l.Name == "Главный офис" && l.Employees == 2);

        var day = await WithAsync(async sp => await sp.GetRequiredService<HrService>().DayAsync(await HrContextAsync(sp), Today, null, null, null, CancellationToken.None));
        Assert.Equal(HrDayStatus.Absent, day.Single(r => r.Employee == "Каримова Дилноза").Day.Status);
        Assert.Equal(40, day.Single(r => r.Employee == "Расулов Тимур").Day.LateMinutes);
        Assert.Equal("ОТ", day.Single(r => r.Employee == "Юсупов Сардор").Day.LeaveCode);

        var deviations = await WithAsync(async sp =>
            await sp.GetRequiredService<HrService>().DeviationsAsync(await HrContextAsync(sp), Yesterday, Today, null, null, CancellationToken.None));
        Assert.Contains(deviations, d => d.Kind == "late" && d.Employee == "Алиев Бобур" && d.Date == Yesterday && d.Minutes == 20);
        Assert.Contains(deviations, d => d.Kind == "absent" && d.Employee == "Каримова Дилноза" && d.Date == Today);
        Assert.DoesNotContain(deviations, d => d.Employee == "Юсупов Сардор");

        var sheet = await WithAsync(async sp =>
            await sp.GetRequiredService<HrService>().TimesheetAsync(await HrContextAsync(sp), Yesterday, Today, null, null, CancellationToken.None));
        Assert.Equal(new[] { "ОП", "Я" }, sheet.Rows.Single(r => r.Employee == "Алиев Бобур").Codes);
        Assert.Equal(new[] { "ОТ", "ОТ" }, sheet.Rows.Single(r => r.Employee == "Юсупов Сардор").Codes);

        // Отметки загружены только до 08:00 — без прихода сотрудник «не ясно», а не «отсутствует».
        await WithAsync(sp => sp.GetRequiredService<OneBaseDbContext>().HrSyncStates.Where(s => s.Entity == HrSyncService.TracksState)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CoveredThrough, new DateTimeOffset(2026, 10, 21, 3, 0, 0, TimeSpan.Zero))));
        var stale = await WithAsync(async sp => await sp.GetRequiredService<HrService>().OverviewAsync(await HrContextAsync(sp), CancellationToken.None));
        Assert.Equal(0, stale.Absent);
        Assert.Equal(1, stale.Unknown);

        var report = await WithAsync(async sp => await sp.GetRequiredService<HrService>().ReportAsync(await HrContextAsync(sp), Yesterday, Today, null, CancellationToken.None));
        var bytes = HrReportWorkbook.Build(report, sheet, deviations);
        Assert.True(bytes.Length > 1000);
    }

    [PostgresFact]
    public async Task Department_head_sees_only_mapped_divisions_and_others_see_nothing()
    {
        await SeedAsync();
        await SyncAsync();
        var s = Guid.NewGuid().ToString("N")[..6];
        var (head, outsider, departmentCode) = await WithAsync(async sp =>
        {
            var db = sp.GetRequiredService<OneBaseDbContext>();
            var department = new Department { Code = $"hr-sales-{s}", Name = $"Продажи {s}" };
            var role = new Role { Name = $"Сотрудник HR-теста {s}" };
            role.Permissions.Add(new RolePermission { Code = Permissions.FilesRead });
            var headUser = new User { Login = $"hr-head-{s}", FullName = "Руководитель продаж", Department = department };
            headUser.Roles.Add(new UserRole { Role = role });
            var other = new User { Login = $"hr-other-{s}", FullName = "Сотрудник", Department = department };
            other.Roles.Add(new UserRole { Role = role });
            db.Users.AddRange(headUser, other);
            db.WorkDepartmentHeads.Add(new WorkDepartmentHead { Department = department, User = headUser });
            await db.SaveChangesAsync();
            await db.HrReferences.Where(r => r.Kind == HrReferenceKinds.Division && r.VerifixId == 901).ExecuteUpdateAsync(x => x.SetProperty(r => r.DepartmentCode, department.Code));
            return (headUser.Id, other.Id, department.Code);
        });

        await WithAsync(async sp =>
        {
            var hr = sp.GetRequiredService<HrService>();
            var ctx = await HrContextAsync(sp, head, Employee);
            Assert.True(ctx.HasAccess);
            Assert.False(ctx.SeesAll);
            Assert.Equal(new long[] { 901, 902 }, ctx.Divisions!.Order());
            Assert.Contains(departmentCode, ctx.HeadOf);

            var employees = await hr.EmployeesAsync(ctx, new HrEmployeeQuery(null, null, null, null), CancellationToken.None);
            Assert.Equal(new[] { "Алиев Бобур", "Каримова Дилноза", "Расулов Тимур" }, employees.Items.Select(e => e.FullName));

            var foreign = await sp.GetRequiredService<OneBaseDbContext>().HrEmployees.Where(e => e.VerifixId == 103).Select(e => e.Id).SingleAsync();
            await Assert.ThrowsAsync<FieldNotFoundException>(() => hr.EmployeeAsync(ctx, foreign, null, null, CancellationToken.None));
            var own = employees.Items[0].Id;
            await Assert.ThrowsAsync<FieldForbiddenException>(() => hr.SaveNoteAsync(ctx, own, "заметка", CancellationToken.None));

            var events = await hr.EventsAsync(ctx, Yesterday, Today, null, null, 1, 100, CancellationToken.None);
            Assert.All(events.Items, e => Assert.NotEqual("Юсупов Сардор", e.Employee));
            var overview = await hr.OverviewAsync(ctx, CancellationToken.None);
            Assert.Equal(3, overview.Active);
            Assert.True(await hr.HasAccessAsync(head, Employee, CancellationToken.None));

            var stranger = await HrContextAsync(sp, outsider, Employee);
            Assert.False(stranger.HasAccess);
            Assert.False(await hr.HasAccessAsync(outsider, Employee, CancellationToken.None));
            await Assert.ThrowsAsync<FieldForbiddenException>(() => hr.OverviewAsync(stranger, CancellationToken.None));
            await Assert.ThrowsAsync<FieldForbiddenException>(() => hr.EmployeesAsync(stranger, new HrEmployeeQuery(null, null, null, null), CancellationToken.None));
            return 0;
        });
    }

    [PostgresFact]
    public async Task Parallel_runs_are_refused_and_a_stale_run_is_closed()
    {
        await SeedAsync();
        var coordinator = fixture.Services.GetRequiredService<HrSyncCoordinator>();
        Verifix.Delay = TimeSpan.FromMilliseconds(150);
        var first = coordinator.RunAsync(HrSyncTrigger.Manual, fixture.AdminId, null, CancellationToken.None);
        await Task.Delay(50);
        var second = await coordinator.RunAsync(HrSyncTrigger.Auto, null, null, CancellationToken.None);
        Assert.Equal(HrSyncStartResult.AlreadyRunning, second.Result);
        Assert.Equal(HrSyncStartResult.Started, (await first).Result);
        Verifix.Delay = TimeSpan.Zero;

        // Другой экземпляр API синхронизирует (строка Running свежая) — этот не начинает.
        var other = new HrSyncRun { Trigger = HrSyncTrigger.Auto, StartedAt = Now, HeartbeatAt = Now };
        await WithAsync(async sp =>
        {
            var db = sp.GetRequiredService<OneBaseDbContext>();
            db.HrSyncRuns.Add(other);
            await db.SaveChangesAsync();
            return 0;
        });
        Assert.Equal(HrSyncStartResult.AlreadyRunning, (await coordinator.RunAsync(HrSyncTrigger.Manual, fixture.AdminId, null, CancellationToken.None)).Result);

        // Перестал отмечаться больше 15 минут — закрывается как прерванный, новый запуск идёт.
        fixture.Clock.Now = Now.AddMinutes(20);
        var run = await SyncAsync();
        Assert.Equal(HrSyncStatus.Succeeded, run.Status);
        var closed = await WithAsync(sp => sp.GetRequiredService<OneBaseDbContext>().HrSyncRuns.AsNoTracking().SingleAsync(r => r.Id == other.Id));
        Assert.Equal(HrSyncStatus.Failed, closed.Status);
        Assert.Contains("Прервана", closed.Message);
    }

    [PostgresFact]
    public async Task Auth_errors_fail_the_run_transient_errors_are_retried_and_failed_runs_can_be_retried()
    {
        await SeedAsync(secret: "wrong-secret-0000000000");
        var denied = await SyncAsync();
        Assert.Equal(HrSyncStatus.Failed, denied.Status);
        Assert.Contains("client_id", denied.Message);
        Assert.DoesNotContain("wrong-secret", denied.Message);
        Assert.Contains(System.Text.Json.JsonDocument.Parse(denied.StepsJson).RootElement.EnumerateArray(), s => s.GetProperty("status").GetString() == "skipped");
        Assert.Equal(0, await WithAsync(sp => sp.GetRequiredService<OneBaseDbContext>().HrEmployees.CountAsync()));

        await SeedAsync();
        // 429 и 502 — повтор внутри клиента, синхронизация проходит.
        Verifix.Fail("core/track$list", HttpStatusCode.TooManyRequests, HttpStatusCode.BadGateway);
        Assert.Equal(HrSyncStatus.Succeeded, (await SyncAsync()).Status);

        // Отметки недоступны три попытки подряд — шаг не выполнен, остальное загружено: «частично».
        Verifix.Fail("core/track$list", HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable);
        fixture.Clock.Now = Now.AddMinutes(30);
        var partial = await SyncAsync();
        Assert.Equal(HrSyncStatus.Partial, partial.Status);
        Assert.True(partial.Errors >= 1);
        Assert.Contains("отметки", partial.Message);
        var state = await WithAsync(sp => sp.GetRequiredService<OneBaseDbContext>().HrSyncStates.AsNoTracking().SingleAsync(s => s.Entity == HrSyncService.TracksState));
        Assert.NotNull(state.LastError);
        Assert.Equal(Now, state.CoveredThrough); // полнота — по последней удачной загрузке

        // Ответ не JSON — ошибка шага, а не падение фоновой задачи; следующий запуск не блокируется.
        Verifix.Garbage.Add("core/employee$list");
        var broken = await SyncAsync();
        Assert.Equal(HrSyncStatus.Partial, broken.Status);
        Assert.Contains("не в формате", broken.Message);
        Verifix.Garbage.Clear();

        var retry = await SyncAsync(HrSyncTrigger.Retry, partial.Id);
        Assert.Equal(HrSyncStatus.Succeeded, retry.Status);
        Assert.Equal(partial.Id, retry.RetryOfId);
        Assert.Equal(HrSyncTrigger.Retry, retry.Trigger);
        Assert.Equal(5, await WithAsync(sp => sp.GetRequiredService<OneBaseDbContext>().HrAttendanceEvents.CountAsync()));
    }

    [PostgresFact]
    public async Task Access_denied_methods_do_not_stop_the_rest_of_the_sync()
    {
        await SeedAsync();
        const string reason = "Пользователь не прикреплен ни к одному филиалу или проекту";
        Verifix.Denied["core/employee$list"] = reason;
        Verifix.Denied["core/track$list"] = reason;

        var run = await SyncAsync();

        // Справочники, табель загружены; сотрудники и отметки — ошибка шага с причиной от Verifix, не «неверные ключи».
        Assert.Equal(HrSyncStatus.Partial, run.Status);
        Assert.Contains(reason, run.Message);
        Assert.DoesNotContain("client_secret", run.Message);
        var steps = System.Text.Json.JsonDocument.Parse(run.StepsJson).RootElement.EnumerateArray()
            .ToDictionary(s => s.GetProperty("step").GetString()!, s => s.GetProperty("status").GetString());
        Assert.Equal("failed", steps["employees"]);
        Assert.Equal("failed", steps["tracks"]);
        Assert.Equal("ok", steps["location"]);
        Assert.Equal("ok", steps["timesheet"]);
        Assert.Equal(3, await WithAsync(sp => sp.GetRequiredService<OneBaseDbContext>().HrReferences.CountAsync(r => r.Kind == HrReferenceKinds.Division)));
    }

    [PostgresFact]
    public async Task Disabled_integration_does_not_start_and_secret_is_stored_encrypted()
    {
        await SeedAsync(enabled: false);
        var (result, _) = await fixture.Services.GetRequiredService<HrSyncCoordinator>().RunAsync(HrSyncTrigger.Auto, null, null, CancellationToken.None);
        Assert.Equal(HrSyncStartResult.NotConfigured, result);

        var row = await WithAsync(sp => sp.GetRequiredService<OneBaseDbContext>().Integrations.AsNoTracking().SingleAsync(i => i.Code == VerifixSettingsStore.Code));
        Assert.Equal("hr", row.DepartmentCode);
        Assert.NotNull(row.ProtectedSecret);
        Assert.DoesNotContain(FakeVerifix.ClientSecret, row.ProtectedSecret);
        Assert.DoesNotContain(FakeVerifix.ClientSecret, row.SettingsJson);
        Assert.Equal("••••cdef", row.SecretHint);
        var connection = await fixture.Services.GetRequiredService<VerifixSettingsStore>().GetAsync();
        Assert.Equal(FakeVerifix.ClientSecret, connection.ClientSecret);
        Assert.False(connection.IsReady);
        Assert.Empty(Verifix.Calls);
    }

    /// <summary>«Лидеры месяца»: только работающие и ни разу не опоздавшие за месяц, по отработанным часам; в первые дни — прошлый месяц.</summary>
    [PostgresFact]
    public async Task Leaders_of_the_month_are_on_time_employees_by_worked_hours()
    {
        await SeedAsync();
        await SyncAsync();

        var leaders = await WithAsync(async sp => await sp.GetRequiredService<HrService>().LeadersAsync(await HrContextAsync(sp), 7, CancellationToken.None));
        Assert.Equal((new DateOnly(2026, 10, 1), Today), (leaders.From, leaders.To));
        Assert.Equal(4, leaders.Employees); // уволенный 01.10 не считается
        // Алиев опоздал вчера, Расулов — сегодня, Юсупов в отпуске (нет приходов) — в рейтинг попадает только Каримова.
        var only = Assert.Single(leaders.Leaders);
        Assert.Equal(1, leaders.OnTime);
        Assert.Equal((1, "Каримова Дилноза", "Команда Ташкент", 1), (only.Place, only.Employee, only.Division, only.PresentDays));
        Assert.True(only.WorkedMinutes > 6 * 60, $"отработано {only.WorkedMinutes} мин"); // 08:58–18:02

        // 2 ноября — ещё прошлый месяц целиком.
        fixture.Clock.Now = new DateTimeOffset(2026, 11, 2, 5, 0, 0, TimeSpan.Zero);
        var early = await WithAsync(async sp => await sp.GetRequiredService<HrService>().LeadersAsync(await HrContextAsync(sp), 7, CancellationToken.None));
        Assert.Equal((new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)), (early.From, early.To));
        fixture.Clock.Now = Now;

        var outsider = Guid.NewGuid();
        await WithAsync(async sp => await Assert.ThrowsAsync<FieldForbiddenException>(async () =>
            await sp.GetRequiredService<HrService>().LeadersAsync(await HrContextAsync(sp, outsider, Employee), 7, CancellationToken.None)));
    }

    /// <summary>Инструменты HR AI-сотрудника: те же цифры, что страницы HR; без телефонов; без права «HR» — отказ.</summary>
    [PostgresFact]
    public async Task Ai_hr_tools_answer_from_verifix_data_and_refuse_without_hr_access()
    {
        await SeedAsync();
        await SyncAsync();
        var s = Guid.NewGuid().ToString("N")[..6];
        var (hrUser, outsider) = await WithAsync(async sp =>
        {
            var db = sp.GetRequiredService<OneBaseDbContext>();
            var hrRole = new Role { Name = $"HR AI-теста {s}" };
            hrRole.Permissions.Add(new RolePermission { Code = Permissions.HrRead });
            var plain = new Role { Name = $"Сотрудник AI-теста {s}" };
            plain.Permissions.Add(new RolePermission { Code = Permissions.FilesRead });
            var hr = new User { Login = $"hr-ai-{s}", FullName = "HR Тестовый" };
            hr.Roles.Add(new UserRole { Role = hrRole });
            var other = new User { Login = $"hr-ai-other-{s}", FullName = "Сотрудник Тестовый" };
            other.Roles.Add(new UserRole { Role = plain });
            db.Users.AddRange(hr, other);
            await db.SaveChangesAsync();
            return (hr.Id, other.Id);
        });

        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        async Task<(ToolResult Result, JsonElement Json)> Ask<T>(Guid user, object args) where T : ITool
        {
            var result = await ActivatorUtilities.CreateInstance<T>(sp).ExecuteAsync(new ToolContext("hr", user, 0), JsonSerializer.SerializeToElement(args));
            Assert.True(result.Success, result.Content);
            Assert.DoesNotContain("998900000", result.Content); // телефоны модели не отдаются
            return (result, JsonDocument.Parse(result.Content).RootElement.Clone());
        }

        // Сегодня: двое пришли (один опоздал на 40 минут), одна отсутствует, один в отпуске; уволенный не считается.
        var (day, d) = await Ask<GetHrDayTool>(hrUser, new { });
        Assert.Equal("2026-10-21", d.GetProperty("date").GetString());
        var totals = d.GetProperty("totals");
        Assert.Equal(4, totals.GetProperty("active").GetInt32());
        Assert.Equal(2, totals.GetProperty("present").GetInt32());
        Assert.Equal(1, totals.GetProperty("late").GetInt32());
        Assert.Equal(1, totals.GetProperty("absent").GetInt32());
        Assert.Equal(1, totals.GetProperty("onLeave").GetInt32());
        var late = Assert.Single(d.GetProperty("lateList").EnumerateArray());
        Assert.Equal("Расулов Тимур", late.GetProperty("employee").GetString());
        Assert.Equal(40, late.GetProperty("lateMinutes").GetInt32());
        Assert.Equal("09:40", late.GetProperty("firstIn").GetString());
        Assert.Equal("Каримова Дилноза", Assert.Single(d.GetProperty("absentList").EnumerateArray()).GetProperty("employee").GetString());
        Assert.True(d.GetProperty("sync").GetProperty("configured").GetBoolean());
        Assert.NotNull(d.GetProperty("sync").GetProperty("lastSync").GetString());
        Assert.Contains("/hr/attendance?date=2026-10-21", JsonSerializer.Serialize(day.Sources));

        // Подразделение по части названия — с дочерними; уволенные — отдельным статусом.
        var (_, team) = await Ask<GetHrEmployeesTool>(hrUser, new { division = "продаж" });
        Assert.Equal("Отдел продаж", team.GetProperty("division").GetString());
        Assert.Equal(["Алиев Бобур", "Каримова Дилноза", "Расулов Тимур"], team.GetProperty("employees").EnumerateArray().Select(e => e.GetProperty("fullName").GetString()));
        var (_, dismissed) = await Ask<GetHrEmployeesTool>(hrUser, new { status = "уволенные" });
        var gone = Assert.Single(dismissed.GetProperty("employees").EnumerateArray());
        Assert.Equal("Уволенный Пётр", gone.GetProperty("fullName").GetString());
        Assert.Equal("уволен", gone.GetProperty("status").GetString());
        Assert.Equal("2026-10-01", gone.GetProperty("dismissed").GetString());

        // Карточка по фамилии: итоги периода и каждый день.
        var (_, card) = await Ask<GetHrEmployeeTool>(hrUser, new { employee = "алиев", from = "2026-10-20", to = "2026-10-21" });
        Assert.Equal(101, card.GetProperty("verifixId").GetInt64());
        Assert.Equal(1, card.GetProperty("summary").GetProperty("lateDays").GetInt32());
        Assert.Equal(20, card.GetProperty("summary").GetProperty("lateMinutes").GetInt32());
        Assert.Equal(2, card.GetProperty("summary").GetProperty("presentDays").GetInt32());
        var days = card.GetProperty("days").EnumerateArray().ToList();
        Assert.Equal(["2026-10-20", "2026-10-21"], days.Select(x => x.GetProperty("date").GetString()));
        Assert.Equal("09:20", days[0].GetProperty("firstIn").GetString());
        Assert.Equal(20, days[0].GetProperty("lateMinutes").GetInt32());

        // Нарушения за два дня: отпуск — не нарушение.
        var (_, dev) = await Ask<GetHrDeviationsTool>(hrUser, new { from = "2026-10-20", to = "2026-10-21" });
        Assert.Equal(2, dev.GetProperty("counts").GetProperty("late").GetInt32());
        Assert.Equal(60, dev.GetProperty("counts").GetProperty("lateMinutes").GetInt32());
        Assert.Equal(1, dev.GetProperty("counts").GetProperty("absent").GetInt32());
        Assert.DoesNotContain("Юсупов", dev.ToString());
        var (_, lateOnly) = await Ask<GetHrDeviationsTool>(hrUser, new { from = "2026-10-20", to = "2026-10-21", kind = "опоздания" });
        Assert.All(lateOnly.GetProperty("cases").EnumerateArray(), c => Assert.Equal("опоздание", c.GetProperty("kind").GetString()));

        // Табель: по подразделениям и сотрудникам; буквы дней — когда сотрудников немного.
        var (_, sheet) = await Ask<GetHrTimesheetTool>(hrUser, new { from = "2026-10-20", to = "2026-10-21", search = "Алиев" });
        var bobur = Assert.Single(sheet.GetProperty("employeesList").EnumerateArray());
        Assert.Equal("20:ОП 21:Я", bobur.GetProperty("days").GetString());
        var (_, all) = await Ask<GetHrTimesheetTool>(hrUser, new { from = "2026-10-20", to = "2026-10-21" });
        Assert.Equal("Расулов Тимур", all.GetProperty("employeesList")[0].GetProperty("employee").GetString()); // больше минут опоздания сверху
        Assert.Contains(all.GetProperty("byDivision").EnumerateArray(), g => g.GetProperty("division").GetString() == "Бухгалтерия" && g.GetProperty("leaveDays").GetInt32() == 2);

        // Неверные аргументы и чужие — понятная ошибка, без данных.
        var tool = ActivatorUtilities.CreateInstance<GetHrDayTool>(sp);
        var denied = await tool.ExecuteAsync(new ToolContext("hr", outsider, 0), JsonSerializer.SerializeToElement(new { }));
        Assert.False(denied.Success);
        Assert.Contains("Нет доступа", denied.Content);
        Assert.DoesNotContain("Каримова", denied.Content);
        var tooLong = await ActivatorUtilities.CreateInstance<GetHrDeviationsTool>(sp)
            .ExecuteAsync(new ToolContext("hr", hrUser, 0), JsonSerializer.SerializeToElement(new { from = "2026-01-01", to = "2026-10-21" }));
        Assert.False(tooLong.Success);
        Assert.Contains("62", tooLong.Content);
        var unknown = await ActivatorUtilities.CreateInstance<GetHrEmployeeTool>(sp)
            .ExecuteAsync(new ToolContext("hr", hrUser, 0), JsonSerializer.SerializeToElement(new { employee = "Несуществующий" }));
        Assert.False(unknown.Success);
        Assert.Contains("не найден", unknown.Content);
    }
}
