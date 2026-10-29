using System.Globalization;
using System.Text.Json;
using OneBase.AI.Consultant;
using OneBase.AI.Knowledge;
using OneBase.AI.Security;
using OneBase.Application.Field;
using OneBase.Application.Hr;
using OneBase.Application.Security;
using OneBase.Domain.Hr;

namespace OneBase.AI.Tools.Data;

/// <summary>
/// Инструменты раздела HR (данные Verifix): те же расчёты HrService, что и страницы «HR». Доступ — право «HR» (роль HR, директор,
/// администратор: все сотрудники); что именно видно, решает HrService по правам пользователя. Контакты сотрудников (телефон, почта)
/// модели не отдаются — для ответов о посещаемости они не нужны.
/// </summary>
internal abstract class HrTool(HrService hr, IUserPermissions permissions) : DataTool
{
    protected HrService Hr { get; } = hr;

    public override string? Source => KnowledgeSources.HrAttendance;
    public override string? RequiredPermission => Permissions.HrRead;

    public const int MaxPeriodDays = HrService.MaxPeriodDays;

    protected static readonly Dictionary<HrDayStatus, string> StatusText = new()
    {
        [HrDayStatus.Present] = "пришёл",
        [HrDayStatus.Absent] = "отсутствует",
        [HrDayStatus.OnLeave] = "по документу",
        [HrDayStatus.DayOff] = "выходной",
        [HrDayStatus.Expected] = "ожидается",
        [HrDayStatus.NoSchedule] = "нет графика",
        [HrDayStatus.Unknown] = "не ясно",
        [HrDayStatus.NotEmployed] = "не работал",
    };

    protected static string EmployeeStatus(string status) => status switch
    {
        HrEmployeeStatus.Working => "работает",
        HrEmployeeStatus.Dismissed => "уволен",
        HrEmployeeStatus.NotHired => "не принят",
        _ => status,
    };

    /// <summary>Что важно модели о расчёте: так она не назовёт «не ясно» прогулом.</summary>
    protected const string Rules =
        "«Отсутствует» — рабочий день по графику Verifix без прихода и без документа при полностью загруженных отметках; «не ясно» — данных " +
        "недостаточно (не прогул); «по документу» — отпуск, больничный, командировка, отгул; одна отметка без прихода — не рабочий день. " +
        "Опоздание — приход позже начала смены больше допуска; ранний уход — уход раньше конца смены.";

    protected async Task<HrContext> ContextAsync(ToolContext context, CancellationToken ct)
    {
        var userId = context.UserId ?? throw new ToolArgumentException("Данные HR отдаются только от имени пользователя.");
        var granted = await permissions.GetAsync(userId, ct);
        var ctx = await Hr.ContextAsync(userId, granted, ct);
        return ctx.HasAccess ? ctx : throw new ToolArgumentException("Нет доступа к данным HR.");
    }

    protected async Task<HrDivisionView?> DivisionAsync(HrContext ctx, string? query, CancellationToken ct)
    {
        if (query is null)
        {
            return null;
        }

        var all = await Hr.DivisionsAsync(ctx, ct);
        if (long.TryParse(query, out var id) && all.FirstOrDefault(d => d.Id == id) is { } byId)
        {
            return byId;
        }

        return WorkFieldText.Find(all.ToList(), query, d => d.Name, "division", "Подразделения");
    }

    /// <summary>Сотрудник по имени (часть имени) или ID Verifix: несколько — просим уточнить.</summary>
    protected async Task<HrEmployeeRow> EmployeeAsync(HrContext ctx, string query, CancellationToken ct)
    {
        var page = await Hr.EmployeesAsync(ctx, new HrEmployeeQuery(query, null, "all", null, 1, 20), ct);
        var key = WorkFieldText.Key(query);
        var exact = page.Items.Where(e => WorkFieldText.Key(e.FullName) == key || e.VerifixId.ToString(CultureInfo.InvariantCulture) == query.Trim()).ToList();
        if (exact.Count == 1)
        {
            return exact[0];
        }

        return page.Items.Count switch
        {
            1 => page.Items[0],
            0 => throw new ToolArgumentException($"Сотрудник «{query}» не найден в Verifix. Уточните фамилию или ID Verifix."),
            _ => throw new ToolArgumentException(
                $"«{query}» подходит нескольким: {WorkFieldText.List(page.Items.Select(e => $"{e.FullName} ({e.Division ?? "без подразделения"}, ID {e.VerifixId})"), 15)}. Уточните."),
        };
    }

    /// <summary>Период from–to (yyyy-MM-dd, «сегодня», «вчера»), по умолчанию — последние fallbackDays дней по сегодня; не больше 62 дней.</summary>
    protected static (DateOnly From, DateOnly To) Period(ToolArgs args, int fallbackDays)
    {
        var to = WorkFieldText.Date(args, "to") ?? FieldClock.Today;
        var from = WorkFieldText.Date(args, "from") ?? to.AddDays(1 - fallbackDays);
        if (to < from)
        {
            (from, to) = (to, from);
        }

        return to.DayNumber - from.DayNumber + 1 > MaxPeriodDays
            ? throw new ToolArgumentException($"Период — не больше {MaxPeriodDays} дней: разбейте запрос на части.")
            : (from, to);
    }

    protected static string? Clock(DateTime? at) => at?.ToString("HH:mm", CultureInfo.InvariantCulture);

    protected static decimal Hours(int minutes) => Math.Round(minutes / 60m, 1);

    protected static object SyncOf(HrSyncBrief s) => new
    {
        Configured = s.Configured,
        Enabled = s.Enabled,
        LastSync = s.LastSuccessAt?.ToOffset(FieldClock.Offset).ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture),
        TracksLoadedThrough = s.TracksThrough?.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture),
        TimesheetFrom = WorkFieldText.Day(s.TimesheetFrom),
        TimesheetTo = WorkFieldText.Day(s.TimesheetTo),
        LastRunStatus = s.LastStatus,
        LastRunMessage = s.LastStatus is HrSyncStatus.Failed or HrSyncStatus.Partial ? s.LastMessage : null,
    };

    protected static object SummaryOf(HrSummary s) => new
    {
        s.PlanDays,
        s.PresentDays,
        s.LateDays,
        s.LateMinutes,
        s.EarlyDays,
        s.EarlyMinutes,
        s.AbsentDays,
        LeaveDays = s.LeaveDays,
        UnknownDays = s.UnknownDays,
        NoExitDays = s.NoExitDays,
        WorkedHours = Hours(s.WorkedMinutes),
        PlanHours = Hours(s.PlanMinutes),
        AttendancePercent = Attendance(s),
        PunctualityPercent = Punctuality(s),
        WorkedOfPlanPercent = s.PlanMinutes > 0 ? Math.Round(100m * s.WorkedMinutes / s.PlanMinutes, 1) : (decimal?)null,
    };

    /// <summary>Явка — доля дней с приходом среди решённых рабочих (пришёл или отсутствовал); как в отчётах HR.</summary>
    protected static decimal? Attendance(HrSummary s) => s.PresentDays + s.AbsentDays is var n and > 0 ? Math.Round(100m * s.PresentDays / n, 1) : null;

    /// <summary>Пунктуальность — доля приходов без опоздания.</summary>
    protected static decimal? Punctuality(HrSummary s) => s.PresentDays > 0 ? Math.Round(100m * (s.PresentDays - s.LateDays) / s.PresentDays, 1) : null;

    protected static async Task<ToolResult> Guard(Func<Task<ToolResult>> run)
    {
        try
        {
            return await run();
        }
        catch (Exception ex) when (ex is FieldForbiddenException or FieldValidationException or FieldNotFoundException)
        {
            throw new ToolArgumentException(ex.Message);
        }
    }
}

/// <summary>Посещаемость за день: итоги, по подразделениям и локациям, списки отсутствующих, опоздавших, по документу и «не ясно».</summary>
internal sealed class GetHrDayTool(HrService hr, IUserPermissions permissions) : HrTool(hr, permissions)
{
    public override string Name => "get_hr_day";
    public override string Title => "HR: посещаемость за день";

    public override string Description =>
        "Посещаемость сотрудников за день по данным Verifix (раздел HR): сколько активных, пришли, опоздали, отсутствуют, по документу (отпуск, " +
        "больничный, командировка), ожидаются, выходной, нет графика, не ясно, отметок за день; то же по подразделениям; списки — кто отсутствует, " +
        "кто опоздал (на сколько минут, приход и начало смены), кто по документу и у кого «не ясно»; когда данные Verifix синхронизированы. " +
        "Аргументы: date — yyyy-MM-dd, «сегодня» (по умолчанию) или «вчера»; division — подразделение Verifix (название или ID). " + Rules;

    public override string? UsageHint => "Для вопросов «кто сегодня опоздал / не пришёл», «сколько людей на работе», посещаемость отдела за конкретный день.";

    public override JsonElement InputSchema { get; } = Schema(
        ("date", new { type = "string", description = "День: yyyy-MM-dd, «сегодня» (по умолчанию) или «вчера»." }),
        ("division", new { type = "string", description = "Подразделение Verifix: название или ID. Без него — все." }));

    protected override Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct) => Guard(async () =>
    {
        var ctx = await ContextAsync(context, ct);
        var asked = WorkFieldText.Date(args, "date");
        var division = await DivisionAsync(ctx, args.Str("division"), ct);
        var rows = await Hr.DayAsync(ctx, asked, division?.Id, null, null, ct);
        var date = rows.Count > 0 ? rows[0].Day.Date : asked ?? FieldClock.Today;
        var sync = await Hr.SyncBriefAsync(ct);
        int Count(Func<HrDayRow, bool> f) => rows.Count(f);
        var cap = args.Cap(40);

        return Data(new
        {
            Date = WorkFieldText.Day(date),
            Division = division?.Name,
            Totals = new
            {
                Active = rows.Count,
                Present = Count(r => r.Day.Status == HrDayStatus.Present),
                Late = Count(r => r.Day.Late),
                EarlyLeave = Count(r => r.Day.EarlyLeave),
                Absent = Count(r => r.Day.Status == HrDayStatus.Absent),
                OnLeave = Count(r => r.Day.Status == HrDayStatus.OnLeave),
                Expected = Count(r => r.Day.Status == HrDayStatus.Expected),
                DayOff = Count(r => r.Day.Status == HrDayStatus.DayOff),
                NoSchedule = Count(r => r.Day.Status == HrDayStatus.NoSchedule),
                Unknown = Count(r => r.Day.Status == HrDayStatus.Unknown),
                Events = rows.Sum(r => r.Day.Events),
            },
            ByDivision = rows.GroupBy(r => r.Division ?? "без подразделения")
                .Select(g => new
                {
                    Division = g.Key,
                    Active = g.Count(),
                    Present = g.Count(r => r.Day.Status == HrDayStatus.Present),
                    Late = g.Count(r => r.Day.Late),
                    Absent = g.Count(r => r.Day.Status == HrDayStatus.Absent),
                    OnLeave = g.Count(r => r.Day.Status == HrDayStatus.OnLeave),
                    Unknown = g.Count(r => r.Day.Status == HrDayStatus.Unknown),
                })
                .OrderByDescending(g => g.Active).Take(args.Cap(30)),
            AbsentList = rows.Where(r => r.Day.Status == HrDayStatus.Absent).Take(cap)
                .Select(r => new { r.Employee, r.Division, r.Job, PlanBegin = Clock(r.Day.PlanBegin), r.Day.Reason }),
            LateList = rows.Where(r => r.Day.Late).OrderByDescending(r => r.Day.LateMinutes).Take(cap)
                .Select(r => new { r.Employee, r.Division, r.Day.LateMinutes, FirstIn = Clock(r.Day.FirstIn), PlanBegin = Clock(r.Day.PlanBegin) }),
            OnLeaveList = rows.Where(r => r.Day.Status == HrDayStatus.OnLeave).Take(cap).Select(r => new { r.Employee, r.Division, Document = r.Day.LeaveName }),
            UnknownList = rows.Where(r => r.Day.Status == HrDayStatus.Unknown).Take(cap).Select(r => new { r.Employee, r.Division, r.Day.Reason }),
            Sync = SyncOf(sync),
        }, new DataSource("HR · Посещаемость", WorkFieldText.Ru(date), $"/hr/attendance?date={WorkFieldText.Day(date)}{(division is null ? "" : $"&divisionId={division.Id}")}"));
    });
}

/// <summary>Сотрудники из Verifix: поиск, подразделение, должность, график, статус, даты приёма и увольнения, итог сегодняшнего дня.</summary>
internal sealed class GetHrEmployeesTool(HrService hr, IUserPermissions permissions) : HrTool(hr, permissions)
{
    internal static readonly IReadOnlyList<ArgChoice<string>> Statuses =
    [
        new("working", "working", "работают", "работает", "действующие", "активные"),
        new("dismissed", "dismissed", "уволены", "уволенные", "уволен"),
        new("not_hired", "not_hired", "не приняты", "кандидаты"),
        new("missing", "missing", "нет в Verifix", "удалённые"),
        new("all", "all", "все"),
    ];

    public override string Name => "get_hr_employees";
    public override string Title => "HR: сотрудники";

    public override string Description =>
        "Сотрудники из Verifix: ФИО, ID Verifix, подразделение, должность, график, статус (работает, уволен, не принят), дата приёма и увольнения, " +
        "последняя отметка, итог сегодняшнего дня; сколько человек по подразделениям. Аргументы: search — часть ФИО или ID Verifix; division — " +
        "подразделение (название или ID); status — working (по умолчанию) / dismissed / not_hired / missing (пропали из Verifix) / all; sort — " +
        "name (по умолчанию) / hired (сначала новые) / division; limit — сколько строк (50, до 200). Контакты (телефон, почта) не отдаются.";

    public override string? UsageHint => "Для вопросов о составе: сколько сотрудников, кто в подразделении, кого приняли или уволили, найти человека.";

    public override JsonElement InputSchema { get; } = Schema(
        ("search", new { type = "string", description = "Часть ФИО или ID Verifix." }),
        ("division", new { type = "string", description = "Подразделение Verifix: название или ID." }),
        ("status", new { type = "string", @enum = WorkFieldText.Codes(Statuses), description = "working — работают (по умолчанию), dismissed — уволены, not_hired — не приняты, missing — пропали из Verifix, all — все." }),
        ("sort", new { type = "string", @enum = new[] { "name", "hired", "division" }, description = "name — по ФИО (по умолчанию), hired — сначала недавно принятые, division — по подразделению." }),
        ("limit", new { type = "integer", description = "Сколько сотрудников вернуть: 50 по умолчанию, до 200." }));

    protected override Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct) => Guard(async () =>
    {
        var ctx = await ContextAsync(context, ct);
        var division = await DivisionAsync(ctx, args.Str("division"), ct);
        var status = WorkFieldText.Pick(args.Str("status"), "status", "working", Statuses);
        var sort = args.Str("sort") is { } s && s is "hired" or "division" ? s : null;
        var limit = args.Report ? 200 : args.Int("limit", 1, 200) ?? 50;
        var page = await Hr.EmployeesAsync(ctx, new HrEmployeeQuery(args.Str("search"), division?.Id, status == "working" ? null : status, sort, 1, Math.Max(10, limit)), ct);
        var divisions = await Hr.DivisionsAsync(ctx, ct);

        return Data(new
        {
            Total = page.Total,
            Shown = Math.Min(limit, page.Items.Count),
            Status = status,
            Division = division?.Name,
            Employees = page.Items.Take(limit).Select(e => new
            {
                e.FullName,
                e.VerifixId,
                e.Division,
                e.Job,
                e.Schedule,
                Status = EmployeeStatus(e.Status),
                Hired = WorkFieldText.Day(e.HiringDate),
                Dismissed = WorkFieldText.Day(e.DismissalDate),
                MissingInVerifixSince = e.MissingSince is { } m ? WorkFieldText.LocalDay(m) : null,
                LastEvent = e.LastEventAt?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                Today = e.Today is { } t ? StatusText[t] + (e.TodayLate ? ", опоздал" : "") : null,
            }),
            WorkingByDivision = divisions.Where(d => d.Employees > 0).OrderByDescending(d => d.Employees).Take(args.Cap(40)).Select(d => new { Division = d.Name, Working = d.Employees }),
        }, new DataSource("HR · Сотрудники", null, "/hr/employees"));
    });
}

/// <summary>Один сотрудник: данные Verifix и все дни за период — график, приход, уход, опоздание, ранний уход, отработано, документ.</summary>
internal sealed class GetHrEmployeeTool(HrService hr, IUserPermissions permissions) : HrTool(hr, permissions)
{
    public override string Name => "get_hr_employee";
    public override string Title => "HR: карточка сотрудника";

    public override string Description =>
        "Карточка сотрудника по данным Verifix: подразделение, должность, график, статус, приём и увольнение, заметка HR; итоги за период (рабочих " +
        "дней, с приходом, опозданий и минут, ранних уходов, отсутствий, дней по документам, отработано и план в часах) и каждый день — итог, начало и " +
        "конец смены, приход, уход, опоздание, ранний уход, отработано, документ, пояснение. Аргументы: employee — ФИО (часть) или ID Verifix " +
        "(обязательно); from, to — yyyy-MM-dd, по умолчанию последние 14 дней, не больше 62. " + Rules;

    public override string? UsageHint => "Для вопросов об одном человеке: как работает, когда опаздывал, сколько отработал за месяц.";

    public override JsonElement InputSchema { get; } = Schema(
        ("employee", new { type = "string", description = "ФИО (можно часть) или ID Verifix. Обязательно." }),
        ("from", new { type = "string", description = "Начало периода: yyyy-MM-dd." }),
        ("to", new { type = "string", description = "Конец периода: yyyy-MM-dd (по умолчанию сегодня)." }));

    protected override Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct) => Guard(async () =>
    {
        var ctx = await ContextAsync(context, ct);
        var query = args.Str("employee") ?? throw new ToolArgumentException("Укажите employee — ФИО или ID Verifix.");
        var from = WorkFieldText.Date(args, "from");
        var to = WorkFieldText.Date(args, "to");
        var found = await EmployeeAsync(ctx, query, ct);
        // Период по умолчанию (14 дней по сегодня) и предел 62 дня проверяет HrService.
        var card = await Hr.EmployeeAsync(ctx, found.Id, from, to, ct);
        var e = card.Employee;

        return Data(new
        {
            e.FullName,
            e.VerifixId,
            e.Division,
            e.Job,
            e.Schedule,
            Status = EmployeeStatus(e.Status),
            Hired = WorkFieldText.Day(e.HiringDate),
            Dismissed = WorkFieldText.Day(e.DismissalDate),
            HrNote = card.Note,
            Period = new { From = WorkFieldText.Day(card.From), To = WorkFieldText.Day(card.To) },
            Summary = SummaryOf(card.Summary),
            Days = card.Days.Where(d => d.Status != HrDayStatus.NotEmployed).OrderBy(d => d.Date).Select(d => new
            {
                Date = WorkFieldText.Day(d.Date),
                Status = StatusText[d.Status],
                Shift = d.PlanBegin is { } b ? $"{Clock(b)}–{Clock(d.PlanEnd)}" : null,
                FirstIn = Clock(d.FirstIn),
                LastOut = Clock(d.LastOut),
                LateMinutes = d.Late ? d.LateMinutes : null,
                EarlyLeaveMinutes = d.EarlyLeave ? d.EarlyLeaveMinutes : null,
                WorkedHours = d.WorkedMinutes is { } w ? Hours(w) : (decimal?)null,
                NoExit = d.NoExit ? true : (bool?)null,
                Document = d.LeaveName,
                d.Reason,
            }),
            Events = card.Events.Count,
        }, new DataSource($"HR · {e.FullName}", WorkFieldText.RuPeriod(card.From, card.To), $"/hr/employees/{e.Id}?from={WorkFieldText.Day(card.From)}&to={WorkFieldText.Day(card.To)}"));
    });
}

/// <summary>Опоздания, ранние уходы, отсутствия и дни без ухода за период — по видам, по сотрудникам и списком.</summary>
internal sealed class GetHrDeviationsTool(HrService hr, IUserPermissions permissions) : HrTool(hr, permissions)
{
    internal static readonly IReadOnlyList<ArgChoice<string?>> Kinds =
    [
        new("late", "late", "опоздания", "опоздание", "опоздал"),
        new("early", "early", "ранние уходы", "ранний уход"),
        new("absent", "absent", "отсутствия", "отсутствие", "прогулы", "прогул", "не пришёл"),
        new("noexit", "noexit", "без ухода", "нет ухода"),
        new(null, "all", "все", "любые"),
    ];

    public override string Name => "get_hr_deviations";
    public override string Title => "HR: опоздания и отсутствия";

    public override string Description =>
        "Нарушения дисциплины за период по графикам Verifix: опоздания (минуты), ранние уходы, отсутствия, дни без отметки ухода. Возвращает " +
        "сколько каждого вида, по сотрудникам (сколько раз и минут — от самых частых) и список случаев (дата, сотрудник, подразделение, вид, " +
        "минуты, смена, приход, уход, пояснение). Аргументы: from, to — yyyy-MM-dd (по умолчанию последние 7 дней, не больше 62); kind — late / " +
        "early / absent / noexit / all (по умолчанию); division — подразделение; limit — сколько случаев в списке (50). " + Rules;

    public override string? UsageHint => "Для вопросов «кто чаще всего опаздывает», «сколько прогулов за месяц», «опоздания отдела за неделю».";

    public override JsonElement InputSchema { get; } = Schema(
        ("from", new { type = "string", description = "Начало периода: yyyy-MM-dd." }),
        ("to", new { type = "string", description = "Конец периода: yyyy-MM-dd (по умолчанию сегодня)." }),
        ("kind", new { type = "string", @enum = WorkFieldText.Codes(Kinds), description = "late — опоздания, early — ранние уходы, absent — отсутствия, noexit — нет ухода, all — все (по умолчанию)." }),
        ("division", new { type = "string", description = "Подразделение Verifix: название или ID." }),
        ("limit", new { type = "integer", description = "Сколько случаев в списке: 50 по умолчанию, до 300." }));

    protected override Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct) => Guard(async () =>
    {
        var ctx = await ContextAsync(context, ct);
        var (from, to) = Period(args, 7);
        var kind = WorkFieldText.Pick(args.Str("kind"), "kind", null, Kinds);
        var division = await DivisionAsync(ctx, args.Str("division"), ct);
        var limit = args.Report ? 5000 : args.Int("limit", 1, 300) ?? 50;
        var list = await Hr.DeviationsAsync(ctx, from, to, kind, division?.Id, null, ct);

        return Data(new
        {
            Period = new { From = WorkFieldText.Day(from), To = WorkFieldText.Day(to) },
            Division = division?.Name,
            Counts = new
            {
                Late = list.Count(d => d.Kind == "late"),
                LateMinutes = list.Where(d => d.Kind == "late").Sum(d => d.Minutes ?? 0),
                EarlyLeave = list.Count(d => d.Kind == "early"),
                Absent = list.Count(d => d.Kind == "absent"),
                NoExit = list.Count(d => d.Kind == "noexit"),
            },
            ByEmployee = list.GroupBy(d => (d.EmployeeId, d.Employee, d.Division))
                .Select(g => new
                {
                    g.Key.Employee,
                    g.Key.Division,
                    Late = g.Count(d => d.Kind == "late"),
                    LateMinutes = g.Where(d => d.Kind == "late").Sum(d => d.Minutes ?? 0),
                    EarlyLeave = g.Count(d => d.Kind == "early"),
                    Absent = g.Count(d => d.Kind == "absent"),
                    NoExit = g.Count(d => d.Kind == "noexit"),
                })
                .OrderByDescending(x => x.Absent + x.Late + x.EarlyLeave).ThenByDescending(x => x.LateMinutes)
                .Take(args.Cap(30)),
            Cases = list.Take(limit).Select(d => new
            {
                Date = WorkFieldText.Day(d.Date),
                d.Employee,
                d.Division,
                Kind = d.Kind switch { "late" => "опоздание", "early" => "ранний уход", "absent" => "отсутствие", _ => "нет ухода" },
                d.Minutes,
                Shift = d.PlanBegin is { } b ? $"{Clock(b)}–{Clock(d.PlanEnd)}" : null,
                FirstIn = Clock(d.FirstIn),
                LastOut = Clock(d.LastOut),
                d.Reason,
            }),
            TotalCases = list.Count,
        }, new DataSource("HR · Опоздания и отсутствия", WorkFieldText.RuPeriod(from, to), $"/hr/deviations?from={WorkFieldText.Day(from)}&to={WorkFieldText.Day(to)}"));
    });
}

/// <summary>Табель и отчёт за период: итоги, по подразделениям (явка, пунктуальность), по сотрудникам — отработано и план, нарушения.</summary>
internal sealed class GetHrTimesheetTool(HrService hr, IUserPermissions permissions) : HrTool(hr, permissions)
{
    public override string Name => "get_hr_timesheet";
    public override string Title => "HR: табель и отчёт";

    public override string Description =>
        "Табель и отчёт по рабочему времени за период по данным Verifix: итоги (сотрудников, рабочих дней по графику, с приходом, опозданий и " +
        "минут, ранних уходов, отсутствий, дней по документам, «не ясно», отработано и план в часах, явка % — доля дней с приходом среди " +
        "решённых рабочих, без опозданий % — доля приходов без опоздания); по подразделениям — то же; по сотрудникам — итоги периода. " +
        "Аргументы: from, to — yyyy-MM-dd (по умолчанию текущий месяц, не больше 62 дней); division — подразделение; search — часть ФИО; sort — " +
        "late / absent / worked / attendance / name (по умолчанию late — больше опозданий сверху); limit — сколько сотрудников (30). " +
        "Для 1–5 сотрудников добавляются буквы дней табеля (Я — явка, ОП — опоздание, РУ — ранний уход, РВ — работа в выходной, ОТС — " +
        "отсутствие, ОТ/Б/К/О/НО — документ, В — выходной, П — праздник, ? — не ясно).";

    public override string? UsageHint => "Для вопросов о часах и дисциплине за месяц: сколько отработали, какой отдел хуже по явке, у кого недоработка.";

    public override JsonElement InputSchema { get; } = Schema(
        ("from", new { type = "string", description = "Начало периода: yyyy-MM-dd (по умолчанию 1-е число текущего месяца)." }),
        ("to", new { type = "string", description = "Конец периода: yyyy-MM-dd (по умолчанию сегодня)." }),
        ("division", new { type = "string", description = "Подразделение Verifix: название или ID." }),
        ("search", new { type = "string", description = "Часть ФИО сотрудника." }),
        ("sort", new { type = "string", @enum = new[] { "late", "absent", "worked", "attendance", "name" }, description = "Порядок сотрудников." }),
        ("limit", new { type = "integer", description = "Сколько сотрудников вернуть: 30 по умолчанию, до 300." }));

    protected override Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct) => Guard(async () =>
    {
        var ctx = await ContextAsync(context, ct);
        var division = await DivisionAsync(ctx, args.Str("division"), ct);
        var limit = args.Report ? 5000 : args.Int("limit", 1, 300) ?? 30;
        // Период по умолчанию (с 1-го числа по сегодня) и предел 62 дня проверяет HrService.
        var sheet = await Hr.TimesheetAsync(ctx, WorkFieldText.Date(args, "from"), WorkFieldText.Date(args, "to"), division?.Id, args.Str("search"), null, ct);

        var rows = (args.Str("sort") ?? "late") switch
        {
            "absent" => sheet.Rows.OrderByDescending(r => r.Summary.AbsentDays).ThenByDescending(r => r.Summary.LateDays),
            "worked" => sheet.Rows.OrderByDescending(r => r.Summary.WorkedMinutes),
            "attendance" => sheet.Rows.OrderBy(r => Attendance(r.Summary) ?? 101),
            "name" => sheet.Rows.OrderBy(r => r.Employee, StringComparer.CurrentCultureIgnoreCase),
            _ => sheet.Rows.OrderByDescending(r => r.Summary.LateDays).ThenByDescending(r => r.Summary.LateMinutes),
        };
        var withCodes = sheet.Rows.Count <= 5;

        return Data(new
        {
            Period = new { From = WorkFieldText.Day(sheet.From), To = WorkFieldText.Day(sheet.To) },
            Division = division?.Name,
            Employees = sheet.Rows.Count,
            Totals = SummaryOf(sheet.Total),
            ByDivision = sheet.Rows.GroupBy(r => r.Division ?? "без подразделения")
                .Select(g => (Division: g.Key, Employees: g.Count(), Summary: Sum(g.Select(r => r.Summary))))
                .OrderByDescending(g => g.Employees).Take(args.Cap(40))
                .Select(g => new
                {
                    g.Division,
                    g.Employees,
                    g.Summary.PlanDays,
                    g.Summary.PresentDays,
                    g.Summary.LateDays,
                    g.Summary.AbsentDays,
                    g.Summary.LeaveDays,
                    g.Summary.UnknownDays,
                    WorkedHours = Hours(g.Summary.WorkedMinutes),
                    PlanHours = Hours(g.Summary.PlanMinutes),
                    AttendancePercent = Attendance(g.Summary),
                    PunctualityPercent = Punctuality(g.Summary),
                }),
            EmployeesList = rows.Take(limit).Select(r => new
            {
                r.Employee,
                r.Division,
                r.Job,
                r.Summary.PlanDays,
                r.Summary.PresentDays,
                r.Summary.LateDays,
                r.Summary.LateMinutes,
                r.Summary.EarlyDays,
                r.Summary.AbsentDays,
                r.Summary.LeaveDays,
                r.Summary.UnknownDays,
                WorkedHours = Hours(r.Summary.WorkedMinutes),
                PlanHours = Hours(r.Summary.PlanMinutes),
                Days = withCodes ? string.Join(" ", sheet.Dates.Select((d, i) => $"{d.Day:00}:{(r.Codes[i] is { Length: > 0 } c ? c : "-")}")) : null,
            }),
            Sync = SyncOf(sheet.Sync),
        }, new DataSource("HR · Рабочее время и табели", WorkFieldText.RuPeriod(sheet.From, sheet.To), $"/hr/timesheets?from={WorkFieldText.Day(sheet.From)}&to={WorkFieldText.Day(sheet.To)}"));
    });

    private static HrSummary Sum(IEnumerable<HrSummary> all) => all.Aggregate(new HrSummary(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), (a, s) => new HrSummary(
        a.PlanDays + s.PlanDays, a.PresentDays + s.PresentDays, a.LateDays + s.LateDays, a.LateMinutes + s.LateMinutes, a.EarlyDays + s.EarlyDays,
        a.EarlyMinutes + s.EarlyMinutes, a.AbsentDays + s.AbsentDays, a.LeaveDays + s.LeaveDays, a.UnknownDays + s.UnknownDays, a.NoExitDays + s.NoExitDays,
        a.WorkedMinutes + s.WorkedMinutes, a.PlanMinutes + s.PlanMinutes));
}
