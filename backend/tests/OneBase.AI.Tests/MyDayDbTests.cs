using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Work;
using OneBase.Api.Auth;
using OneBase.Api.Controllers;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Hr;
using OneBase.Application.Security;
using OneBase.Application.Work;
using OneBase.Domain.AI;
using OneBase.Domain.Files;
using OneBase.Domain.Hr;
using OneBase.Domain.Identity;
using OneBase.Domain.Work;
using OneBase.Infrastructure.Files;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// «Мой день» на настоящем PostgreSQL (ONEBASE_TEST_POSTGRES, иначе пропускается): каждый сотрудник видит только своё — рабочее время по своей
/// привязке к Verifix, назначенные ему задачи, свои события, вложения своих задач, свои уведомления и файлы своего отдела; план на день от AI
/// не вызывает модель повторно, «Обновить» — не чаще раза в 15 минут, без задач — «Всё в порядке» без модели; срок со временем; привязка
/// к Verifix при создании пользователя («Без привязки к Verifix» — галочкой).
/// </summary>
public class MyDayDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private sealed record Org(Department DeptA, Department DeptB, User A, User B, User Boss, HrEmployee HrA, HrEmployee HrB, HrEmployee Free);

    private static readonly IReadOnlySet<string> EmployeePermissions = new HashSet<string>
    {
        Permissions.WorkTasks, Permissions.FilesRead, Permissions.FilesWrite, Permissions.AgentsRun,
    };

    private static long _verifix = 7_000_000 + Random.Shared.Next(1_000_000);

    private async Task<Org> SeedAsync()
    {
        var s = Guid.NewGuid().ToString("N")[..6];
        var deptA = new Department { Code = $"md-a-{s}", Name = $"Продажи {s}" };
        var deptB = new Department { Code = $"md-b-{s}", Name = $"Финансы {s}" };
        var role = new Role { Name = $"Сотрудник {s}" };
        foreach (var p in EmployeePermissions)
        {
            role.Permissions.Add(new RolePermission { Code = p });
        }

        User Person(string login, string name, Department department)
        {
            var u = new User { Login = $"{login}-{s}", FirstName = name.Split(' ')[1], LastName = name.Split(' ')[0], FullName = name, Department = department };
            u.Roles.Add(new UserRole { Role = role });
            return u;
        }

        var a = Person("md-a", "Рахимова Азиза", deptA);
        var b = Person("md-b", "Каримов Бахтиёр", deptB);
        var boss = Person("md-boss", "Руководитель Отдела", deptA);
        HrEmployee Hr(string name, User? user) =>
            new() { VerifixId = Interlocked.Increment(ref _verifix), FirstName = name, FullName = name, Status = HrEmployeeStatus.Working, UserId = user?.Id };
        var hrA = Hr("RAHIMOVA AZIZA", a);
        var hrB = Hr("KARIMOV BAXTIYOR", b);
        var free = Hr("SVOBODNIY SOTRUDNIK", null);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        db.Users.AddRange(a, b, boss);
        db.HrEmployees.AddRange(hrA, hrB, free);
        await db.SaveChangesAsync();
        return new Org(deptA, deptB, a, b, boss, hrA, hrB, free);
    }

    private static Task<WorkContext> Ctx(IServiceProvider sp, User user) =>
        sp.GetRequiredService<WorkTaskService>().ContextAsync(user.Id, EmployeePermissions, CancellationToken.None);

    private static DateTime Local(DateOnly day, int h, int m) => day.ToDateTime(new TimeOnly(h, m));

    private static void Attendance(OneBaseDbContext db, HrEmployee e, DateOnly day, int h, int m)
    {
        var staff = e.VerifixId * 10;
        db.HrTimesheetDays.Add(new HrTimesheetDay
        {
            StaffId = staff, Date = day, VerifixEmployeeId = e.VerifixId, EmployeeId = e.Id, DayKind = "W",
            PlanBegin = Local(day, 9, 0), PlanEnd = Local(day, 18, 0), PlanMinutes = 540, FirstIn = Local(day, h, m),
        });
        db.HrAttendanceEvents.Add(new HrAttendanceEvent
        {
            VerifixTrackId = e.VerifixId * 100, VerifixEmployeeId = e.VerifixId, EmployeeId = e.Id, StaffId = staff, TrackType = "I",
            RawTime = $"{day:dd.MM.yyyy} {h:00}:{m:00}:00", LocalTime = Local(day, h, m), LocalDate = day,
            OccurredAt = new DateTimeOffset(Local(day, h, m), TimeSpan.FromHours(5)).ToUniversalTime(),
        });
    }

    [PostgresFact]
    public async Task Each_employee_sees_only_own_workday_tasks_calendar_documents_and_notifications()
    {
        var org = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var today = FieldClock.Today;

        Attendance(db, org.HrA, today, 8, 52);
        Attendance(db, org.HrB, today, 9, 30);
        WorkTask NewTask(User assignee, string title, DateOnly? due, WorkTaskStatus status = WorkTaskStatus.InProgress, Department? dept = null) => new()
        {
            DepartmentId = (dept ?? org.DeptA).Id, Title = title, AssigneeId = assignee.Id, CreatedById = org.Boss.Id, Status = status, DueDate = due,
        };
        var mine = NewTask(org.A, "Сверить дебиторку", today.AddDays(-1));
        var mineToday = NewTask(org.A, "Отчёт по продажам", today);
        var review = NewTask(org.A, "Обновить план визитов", today.AddDays(2), WorkTaskStatus.Review);
        var foreign = NewTask(org.B, "Чужая задача", today.AddDays(1), dept: org.DeptB);
        db.WorkTasks.AddRange(mine, mineToday, review, foreign);
        db.WorkTaskAttachments.AddRange(
            new WorkTaskAttachment { Task = mine, FileName = "План_продаж.xlsx", ContentType = "application/vnd.ms-excel", ObjectKey = "k1", UploadedById = org.Boss.Id },
            new WorkTaskAttachment { Task = mine, FileName = "Мой_черновик.docx", ContentType = "application/msword", ObjectKey = "k2", UploadedById = org.A.Id },
            new WorkTaskAttachment { Task = foreign, FileName = "Чужое.pdf", ContentType = "application/pdf", ObjectKey = "k3", UploadedById = org.Boss.Id });
        var at = new DateTimeOffset(today.AddDays(1).ToDateTime(new TimeOnly(10, 0)), TimeSpan.FromHours(5)).ToUniversalTime();
        var privateB = new CalendarEvent { Title = "Встреча 1:1 с Бахтиёром", StartsAt = at, CreatedById = org.Boss.Id };
        privateB.Participants.Add(new CalendarEventParticipant { UserId = org.B.Id });
        db.CalendarEvents.AddRange(privateB, new CalendarEvent { Title = "Аттестация сотрудников", Kind = CalendarEventKind.Event, StartsAt = at, ForEveryone = true });
        db.UserNotifications.AddRange(
            new UserNotification { RecipientId = org.A.Id, Kind = UserNotificationKind.TaskAssigned, Title = "Новая задача: Сверить дебиторку" },
            new UserNotification { RecipientId = org.B.Id, Kind = UserNotificationKind.TaskComment, Title = "Комментарий для Бахтиёра" });
        var rootA = new Folder { Name = org.DeptA.Name, DepartmentId = org.DeptA.Id };
        var reportsA = new Folder { Name = DepartmentFilesService.DefaultFolders[1], Parent = rootA, DepartmentId = org.DeptA.Id };
        var rootB = new Folder { Name = org.DeptB.Name, DepartmentId = org.DeptB.Id };
        db.Folders.AddRange(rootA, reportsA, rootB);
        db.Files.AddRange(
            new FileItem { Name = "КП_шаблон.docx", ContentType = "application/msword", Folder = rootA, DepartmentId = org.DeptA.Id, CreatedById = org.A.Id, CurrentVersion = 1 },
            new FileItem { Name = "Отчёт_нед43.pdf", ContentType = "application/pdf", Folder = reportsA, DepartmentId = org.DeptA.Id, CreatedById = org.Boss.Id, CurrentVersion = 1 },
            new FileItem { Name = "Бюджет_финансов.xlsx", ContentType = "application/vnd.ms-excel", Folder = rootB, DepartmentId = org.DeptB.Id, CreatedById = org.B.Id, CurrentVersion = 1 });
        await db.SaveChangesAsync();

        // Рабочий день — по своей привязке: приход A, а не B.
        var hr = sp.GetRequiredService<HrService>();
        var dayA = await hr.MyWorkdayAsync(org.A.Id, default);
        Assert.True(dayA.Available);
        Assert.Equal(Local(today, 8, 52), dayA.Today!.Arrival);
        Assert.Equal(Local(today, 8, 52), dayA.Week.Single(d => d.Date == today).Arrival);
        Assert.Equal(Local(today, 9, 30), (await hr.MyWorkdayAsync(org.B.Id, default)).Today!.Arrival);
        // Перерыв 13:00–14:00 по умолчанию, план 8 ч.
        Assert.Equal((Local(today, 13, 0), 60, false), (dayA.Today.Break!.Start, dayA.Today.Break.Minutes, dayA.Today.Break.FromSchedule));
        Assert.Equal(480, dayA.Today.PlannedMinutes);
        Assert.Equal("no_employee_link", (await hr.MyWorkdayAsync(org.Boss.Id, default)).UnavailableReason);

        // Задачи — только назначенные A.
        var tasks = sp.GetRequiredService<WorkTaskService>();
        var ctxA = await Ctx(sp, org.A);
        var my = await tasks.MyDayAsync(ctxA, default);
        Assert.Equal((3, 2, 1, 1), (my.Total, my.InProgress, my.Waiting, my.Overdue));
        Assert.Equal([mine.Id, mineToday.Id, review.Id], my.Upcoming.Select(t => t.Id));
        Assert.True(my.Upcoming[0].Overdue);
        await Assert.ThrowsAsync<FieldNotFoundException>(() => tasks.GetAsync(ctxA, foreign.Id, default));

        // Календарь: событие компании и сроки своих задач; приватная встреча B не видна.
        var calendar = sp.GetRequiredService<CalendarService>();
        var items = await calendar.ItemsAsync(ctxA, today.AddDays(-1), today.AddDays(7), default);
        Assert.Contains(items, i => i.Title == "Аттестация сотрудников");
        Assert.DoesNotContain(items, i => i.Title.Contains("Бахтиёр"));
        Assert.Equal(3, items.Count(i => i.Type == "task_due"));
        Assert.DoesNotContain(items, i => i.Title == "Чужая задача");
        await Assert.ThrowsAsync<FieldNotFoundException>(() => calendar.GetAsync(ctxA, privateB.Id, default));
        Assert.Contains(await calendar.ItemsAsync(await Ctx(sp, org.B), today, today.AddDays(7), default), i => i.Title.Contains("Бахтиёр"));

        // «Назначенные» — вложения к своим задачам от других; свои и чужие не попадают.
        Assert.Equal(["План_продаж.xlsx"], (await tasks.MyAttachmentsAsync(ctxA, 5, default)).Select(x => x.FileName));

        // Файлы — по правам «Файлов»: только отдел A; «Мои» — загруженные A; «Отчёты» — из папки «Отчёты».
        var files = sp.GetRequiredService<DepartmentFilesService>();
        var actor = (await files.UserActorAsync(org.A.Id, default))!;
        var recent = await files.MyDocumentsAsync(actor, "recent", 5, default);
        Assert.Equal(["КП_шаблон.docx", "Отчёт_нед43.pdf"], recent.Select(f => f.Name).Order());
        Assert.Equal(["КП_шаблон.docx"], (await files.MyDocumentsAsync(actor, "mine", 5, default)).Select(f => f.Name));
        Assert.Equal(["Отчёт_нед43.pdf"], (await files.MyDocumentsAsync(actor, "reports", 5, default)).Select(f => f.Name));

        // Уведомления — только адресованные A.
        var page = await sp.GetRequiredService<NotificationCenter>().ListAsync(org.A.Id, "all", null, 10, ctxA, default);
        Assert.Equal(["Новая задача: Сверить дебиторку"], page.Items.Select(n => n.Title));
    }

    [PostgresFact]
    public async Task Due_time_is_validated_and_makes_the_task_overdue_once_passed()
    {
        var org = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tasks = sp.GetRequiredService<WorkTaskService>();
        var ctx = await Ctx(sp, org.A);
        var today = FieldClock.Today;

        await Assert.ThrowsAsync<FieldValidationException>(() =>
            tasks.CreateAsync(ctx, org.DeptA.Code, new WorkTaskInput("Без даты", null, org.A.Id, DueTime: new TimeOnly(15, 0)), default));
        var id = await tasks.CreateAsync(ctx, org.DeptA.Code, new WorkTaskInput("Отчёт", null, org.A.Id, DueDate: today.AddDays(1), DueTime: new TimeOnly(15, 0)), default);
        var row = (await tasks.GetAsync(ctx, id, default)).Task;
        Assert.Equal((today.AddDays(1), new TimeOnly(15, 0), false), (row.DueDate!.Value, row.DueTime!.Value, row.Overdue));

        // Время срока сегодня уже прошло — задача просрочена в тот же день.
        var db = sp.GetRequiredService<OneBaseDbContext>();
        await db.WorkTasks.Where(t => t.Id == id).ExecuteUpdateAsync(x => x.SetProperty(t => t.DueDate, today).SetProperty(t => t.DueTime, new TimeOnly(0, 0)));
        Assert.True((await tasks.GetAsync(ctx, id, default)).Task.Overdue);
        Assert.Equal(1, (await tasks.MyDayAsync(ctx, default)).Overdue);
        Assert.Contains((await tasks.SearchAsync(ctx, new WorkTaskSearch("my", Status: "overdue"), default)).Items, t => t.Id == id);

        // Перенос срока сохраняет время и пишет его в историю.
        await tasks.UpdateAsync(ctx, id, new WorkTaskInput("Отчёт", null, org.A.Id, DueDate: today.AddDays(3), DueTime: new TimeOnly(11, 30)), default);
        var detail = await tasks.GetAsync(ctx, id, default);
        Assert.Equal(new TimeOnly(11, 30), detail.Task.DueTime);
        Assert.Contains(detail.History, h => h.Kind == "due" && h.Text.Contains("11:30"));
    }

    [PostgresFact]
    public async Task Daily_plan_is_cached_rate_limited_and_all_good_without_tasks()
    {
        var org = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var plans = sp.GetRequiredService<DailyPlanService>();
        var gateway = fixture.Gateway;
        var today = FieldClock.Today;
        var ctxB = await Ctx(sp, org.B);

        // Задач и событий нет — «Всё в порядке», модель не вызывается.
        var calls = gateway.Calls.Count;
        var empty = await plans.GetAsync(ctxB, default);
        Assert.Equal(("ready", DailyPlanService.AllGood), (empty.Status, empty.Items.Single().Text));
        Assert.Equal(calls, gateway.Calls.Count);

        var task = new WorkTask { DepartmentId = org.DeptA.Id, Title = "Отчёт по продажам", AssigneeId = org.A.Id, Status = WorkTaskStatus.InProgress, DueDate = today };
        db.WorkTasks.Add(task);
        await db.SaveChangesAsync();
        var ctx = await Ctx(sp, org.A);

        // Первое открытие — план готовится в фоне, ответ сразу.
        Assert.Equal("generating", (await plans.GetAsync(ctx, default)).Status);
        Assert.Equal(calls, gateway.Calls.Count);
        gateway.Script.Enqueue(m =>
        {
            // Во входе модели — только задачи A.
            Assert.Contains("Отчёт по продажам", m[^1].Content);
            Assert.DoesNotContain(org.B.FullName, m[^1].Content);
            return """{"items":[{"text":"Начните с отчёта по продажам — срок сегодня","tone":"warning","ref":"t1"}]}""";
        });
        await plans.GenerateAsync(org.A.Id, today, default);
        Assert.Equal(calls + 1, gateway.Calls.Count);
        Assert.Equal("me.daily_plan", gateway.Calls[^1].Options.Context.Purpose);

        // Повторное открытие — сохранённый план, без модели.
        var ready = await plans.GetAsync(ctx, default);
        Assert.Equal(("ready", $"/tasks/task/{task.Id}"), (ready.Status, ready.Items.Single().Href));
        await plans.GetAsync(ctx, default);
        Assert.Equal(calls + 1, gateway.Calls.Count);

        // «Обновить» сразу — отказ с временем, когда можно.
        var limited = await Assert.ThrowsAsync<DailyPlanRateLimitedException>(() => plans.RefreshAsync(ctx, default));
        Assert.NotNull(limited.RetryAt);
        Assert.Equal(calls + 1, gateway.Calls.Count);

        // Через 15 минут — можно.
        await db.AiDailyPlans.Where(p => p.UserId == org.A.Id).ExecuteUpdateAsync(x => x.SetProperty(p => p.GeneratedAt, DateTimeOffset.UtcNow.AddMinutes(-16)));
        Assert.Equal("generating", (await plans.RefreshAsync(ctx, default)).Status);
        gateway.Script.Enqueue(_ => """{"items":[{"text":"Закройте отчёт по продажам","tone":"warning","ref":"t1"}]}""");
        await plans.GenerateAsync(org.A.Id, today, default);
        Assert.Equal(calls + 2, gateway.Calls.Count);

        // Задачи изменились — план устарел; пересоставляется не раньше чем через 30 минут после прошлого.
        db.WorkTasks.Add(new WorkTask { DepartmentId = org.DeptA.Id, Title = "Новая задача", AssigneeId = org.A.Id, DueDate = today.AddDays(1) });
        await db.SaveChangesAsync();
        Assert.Equal("stale", (await plans.GetAsync(ctx, default)).Status);
        await db.AiDailyPlans.Where(p => p.UserId == org.A.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.GeneratedAt, DateTimeOffset.UtcNow.AddMinutes(-31)).SetProperty(p => p.RequestedAt, DateTimeOffset.UtcNow.AddMinutes(-31)));
        var regenerating = await plans.GetAsync(ctx, default);
        Assert.Equal("generating", regenerating.Status);
        Assert.NotEmpty(regenerating.Items);

        // Модель недоступна — прежний план, страница работает; повтора на каждое открытие нет.
        await plans.GenerateAsync(org.A.Id, today, default);
        Assert.Equal(calls + 3, gateway.Calls.Count);
        var failed = await plans.GetAsync(ctx, default);
        Assert.Equal("stale", failed.Status);
        Assert.NotEmpty(failed.Items);
        await plans.GetAsync(ctx, default);
        Assert.Equal(AiDailyPlanStatus.Failed, await db.AiDailyPlans.Where(p => p.UserId == org.A.Id).Select(p => p.Status).SingleAsync());
        Assert.Equal(calls + 3, gateway.Calls.Count);
    }

    private static UsersController Users(IServiceProvider sp, Guid adminId)
    {
        var claims = new List<Claim> { new(OneBaseClaims.UserId, adminId.ToString()) };
        claims.AddRange(Permissions.All.Select(p => new Claim(OneBaseClaims.Permission, p)));
        return new UsersController(sp.GetRequiredService<IAppDbContext>(), new PasswordHasher<User>(),
            new UserAccessCache(sp.GetRequiredService<IServiceScopeFactory>(), new MemoryCache(new MemoryCacheOptions())), sp.GetRequiredService<IAuditLogger>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) } },
        };
    }

    private static string? Error(IActionResult result) => (result as BadRequestObjectResult)?.Value?.GetType().GetProperty("error")?.GetValue(((BadRequestObjectResult)result).Value) as string;

    [PostgresFact]
    public async Task New_user_is_linked_to_verifix_or_marked_without_it()
    {
        var org = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == SystemRoles.EmployeeRole);
        if (role is null)
        {
            role = new Role { Name = SystemRoles.EmployeeRole };
            foreach (var p in EmployeePermissions)
            {
                role.Permissions.Add(new RolePermission { Code = p });
            }

            db.Roles.Add(role);
            await db.SaveChangesAsync();
        }

        var users = Users(sp, org.Boss.Id);
        var s = Guid.NewGuid().ToString("N")[..6];
        UsersController.UserInput Input(string login, Guid? hr = null, bool off = false, Guid? dept = null) =>
            new($"{login}-{s}", "Азиза", "Рахимова", null, null, "Менеджер по продажам", role.Id, "Secret-pass-1", DepartmentId: dept, HrEmployeeId: hr, NoVerifix: off);

        Assert.Equal("Выберите сотрудника в Verifix или отметьте «Без привязки к Verifix».", Error(await users.Create(Input("u1", dept: org.DeptA.Id), default)));
        Assert.Equal("Выберите отдел сотрудника.", Error(await users.Create(Input("u2", org.Free.Id), default)));
        Assert.Contains("уже привязан", Error(await users.Create(Input("u3", org.HrA.Id, dept: org.DeptA.Id), default)));

        Assert.IsType<OkObjectResult>(await users.Create(Input("u4", org.Free.Id, dept: org.DeptA.Id), default));
        var linked = await db.Users.AsNoTracking().SingleAsync(u => u.Login == $"u4-{s}");
        Assert.Equal(org.DeptA.Id, linked.DepartmentId);
        Assert.Equal(linked.Id, await db.HrEmployees.AsNoTracking().Where(e => e.Id == org.Free.Id).Select(e => e.UserId).SingleAsync());

        Assert.IsType<OkObjectResult>(await users.Create(Input("u5", off: true, dept: org.DeptB.Id), default));
        var contractor = await db.Users.AsNoTracking().SingleAsync(u => u.Login == $"u5-{s}");
        Assert.False(await db.HrEmployees.AnyAsync(e => e.UserId == contractor.Id));

        // «Без привязки» в карточке снимает привязку.
        Assert.IsType<OkObjectResult>(await users.Update(linked.Id, Input("u4", off: true, dept: org.DeptA.Id) with { Password = null }, default));
        Assert.Null(await db.HrEmployees.AsNoTracking().Where(e => e.Id == org.Free.Id).Select(e => e.UserId).SingleAsync());
    }
}
