using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.AI.Consultant;
using OneBase.AI.Knowledge;
using OneBase.AI.Security;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Security;
using OneBase.Application.Work;
using OneBase.Domain.Field;
using OneBase.Domain.Work;

namespace OneBase.AI.Tools.Data;

/// <summary>Значение аргумента-перечисления: код (он же в enum схемы), подпись по-русски и синонимы, которыми модель может его назвать.</summary>
internal sealed record ArgChoice<T>(T Value, string Code, string Label, params string[] Aliases);

/// <summary>Общее для инструментов задач отделов и Sales Base: разбор аргументов, подписи статусов, даты, страницы сервисов.</summary>
internal static class WorkFieldText
{
    public const int PageSize = 200;

    private static readonly string[] MeAliases = ["me", "self", "я", "мне", "мои", "мой", "мою", "себе"];

    public static string Key(string value) => value.Trim().ToLowerInvariant().Replace('ё', 'е').Replace('_', ' ').Replace('-', ' ');

    /// <summary>«me», «я», «мои» — сам пользователь.</summary>
    public static bool IsMe(string value) => MeAliases.Contains(Key(value));

    public static string[] Codes<T>(IEnumerable<ArgChoice<T>> choices) => choices.Select(c => c.Code).ToArray();

    public static string CodeOf<T>(IEnumerable<ArgChoice<T>> choices, T value) => choices.First(c => EqualityComparer<T>.Default.Equals(c.Value, value)).Code;

    public static string LabelOf<T>(IEnumerable<ArgChoice<T>> choices, T value) =>
        choices.FirstOrDefault(c => EqualityComparer<T>.Default.Equals(c.Value, value))?.Label ?? $"{value}";

    /// <summary>Значение по коду, подписи или синониму; неизвестное — ошибка с перечнем допустимых.</summary>
    public static T Pick<T>(string? input, string arg, T fallback, IReadOnlyList<ArgChoice<T>> choices)
    {
        if (input is null)
        {
            return fallback;
        }

        var key = Key(input);
        foreach (var choice in choices)
        {
            if (Key(choice.Code) == key || Key(choice.Label) == key || choice.Aliases.Any(a => Key(a) == key))
            {
                return choice.Value;
            }
        }

        throw new ToolArgumentException($"Аргумент {arg}: значения «{input}» нет. Допустимо: {string.Join(", ", choices.Select(c => $"{c.Code} ({c.Label})"))}.");
    }

    /// <summary>
    /// Человек или команда по имени: точное совпадение, иначе все слова запроса входят в имя. Несколько — просим уточнить,
    /// ни одного — перечисляем, из кого можно выбрать (только доступных пользователю).
    /// </summary>
    public static T? Find<T>(IReadOnlyList<T> pool, string? query, Func<T, string> name, string arg, string available) where T : class
    {
        if (query is null)
        {
            return null;
        }

        var key = Key(query);
        var exact = pool.Where(p => Key(name(p)) == key).ToList();
        if (exact.Count == 1)
        {
            return exact[0];
        }

        var words = key.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var partial = exact.Count > 1 ? exact : pool.Where(p => Key(name(p)) is var n && words.All(w => n.Contains(w, StringComparison.Ordinal))).ToList();
        return partial.Count switch
        {
            1 => partial[0],
            > 1 => throw new ToolArgumentException($"Аргумент {arg}: «{query}» подходит нескольким — {List(partial.Select(name))}. Уточните имя."),
            _ => throw new ToolArgumentException($"Аргумент {arg}: «{query}» не найден. {available}: {List(pool.Select(name))}."),
        };
    }

    public static string List(IEnumerable<string> names, int max = 25)
    {
        var all = names.ToList();
        return all.Count == 0 ? "нет" : string.Join(", ", all.Take(max)) + (all.Count > max ? $" и ещё {all.Count - max}" : string.Empty);
    }

    public static string Day(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string? Day(DateOnly? day) => day is { } d ? Day(d) : null;

    /// <summary>Местная дата момента (Sales Base и задачи живут по времени Ташкента).</summary>
    public static string? LocalDay(DateTimeOffset? at) => at is { } a ? Day(FieldClock.LocalDate(a)) : null;

    public static string? LocalTime(DateTimeOffset? at) => at?.ToOffset(FieldClock.Offset).ToString("HH:mm", CultureInfo.InvariantCulture);

    public static string Ru(DateOnly day) => day.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    public static string RuPeriod(DateOnly from, DateOnly to) => from == to ? Ru(from) : $"{Ru(from)} — {Ru(to)}";

    /// <summary>На сколько дней просрочена открытая задача; не просрочена — null.</summary>
    public static int? OverdueDays(bool open, DateOnly? due, DateOnly today) => open && due is { } d && d < today ? today.DayNumber - d.DayNumber : null;

    public static string? Short(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var t = text.Trim();
        return t.Length <= max ? t : t[..(max - 1)].TrimEnd() + "…";
    }

    /// <summary>Дата yyyy-MM-dd (или «сегодня»/«вчера»); другой формат — ошибка аргумента.</summary>
    public static DateOnly? Date(ToolArgs args, string name)
    {
        var raw = args.Str(name);
        if (raw is null)
        {
            return null;
        }

        switch (Key(raw))
        {
            case "today" or "сегодня":
                return FieldClock.Today;
            case "yesterday" or "вчера":
                return FieldClock.Today.AddDays(-1);
        }

        return DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? day
            : throw new ToolArgumentException($"Аргумент {name} — дата в формате yyyy-MM-dd, например {Day(FieldClock.Today)}; передано «{raw}».");
    }

    /// <summary>Ссылка на страницу с параметрами (пустые пропускаются).</summary>
    public static string Link(string path, params (string Key, string? Value)[] query)
    {
        var parts = query.Where(q => !string.IsNullOrEmpty(q.Value)).Select(q => $"{q.Key}={Uri.EscapeDataString(q.Value!)}").ToList();
        return parts.Count == 0 ? path : $"{path}?{string.Join("&", parts)}";
    }

    /// <summary>Все страницы списка сервиса подряд, но не больше maxRows строк: (строки, всего по данным сервиса, обрезано ли).</summary>
    public static async Task<(List<T> Rows, int Total, bool Truncated)> AllPagesAsync<T>(Func<int, Task<PageResult<T>>> page, int maxRows)
    {
        var rows = new List<T>();
        for (var n = 1; ; n++)
        {
            var result = await page(n);
            rows.AddRange(result.Items);
            if (result.Items.Count == 0 || rows.Count >= result.Total)
            {
                return (rows, result.Total, false);
            }

            if (rows.Count >= maxRows)
            {
                return (rows, result.Total, true);
            }
        }
    }

    public static string Label(WorkTaskStatus status) => status switch
    {
        WorkTaskStatus.New => "новая",
        WorkTaskStatus.InProgress => "в работе",
        WorkTaskStatus.Done => "выполнена",
        WorkTaskStatus.Cancelled => "отменена",
        _ => status.ToString(),
    };

    public static string Label(WorkTaskPriority priority) => priority switch
    {
        WorkTaskPriority.Low => "низкий",
        WorkTaskPriority.Medium => "средний",
        WorkTaskPriority.High => "высокий",
        WorkTaskPriority.Urgent => "срочно",
        _ => priority.ToString(),
    };

    public static string Label(FieldPriority priority) => priority switch
    {
        FieldPriority.Low => "низкий",
        FieldPriority.Medium => "средний",
        FieldPriority.High => "высокий",
        FieldPriority.Urgent => "срочно",
        _ => priority.ToString(),
    };

    public static string Label(FieldTaskStatus status) => status switch
    {
        FieldTaskStatus.New => "новая",
        FieldTaskStatus.Accepted => "принята",
        FieldTaskStatus.InProgress => "в работе",
        FieldTaskStatus.Completed => "выполнена",
        FieldTaskStatus.Verified => "подтверждена",
        FieldTaskStatus.Cancelled => "отменена",
        FieldTaskStatus.Postponed => "отложена",
        _ => status.ToString(),
    };

    public static string Label(FieldActorType actor) => actor switch
    {
        FieldActorType.Rm => "РМ",
        FieldActorType.Supervisor => "супервайзер",
        FieldActorType.Agent => "агент",
        FieldActorType.Ai => "AI-планирование",
        FieldActorType.System => "система",
        FieldActorType.Office => "офис",
        _ => actor.ToString(),
    };

    public static string Label(FieldPointStatus status) => status switch
    {
        FieldPointStatus.Planned => "не посещена",
        FieldPointStatus.InProgress => "идёт визит",
        FieldPointStatus.Visited => "посещена",
        FieldPointStatus.Skipped => "пропущена",
        FieldPointStatus.Cancelled => "отменена",
        _ => status.ToString(),
    };

    public static string Label(FieldRouteStatus status) => status switch
    {
        FieldRouteStatus.Planned => "не начат",
        FieldRouteStatus.InProgress => "в работе",
        FieldRouteStatus.Completed => "завершён",
        FieldRouteStatus.Cancelled => "отменён",
        _ => status.ToString(),
    };

    public static string Label(FieldRouteSource source) => source switch
    {
        FieldRouteSource.Manual => "вручную",
        FieldRouteSource.Linko => "план визитов Linko",
        FieldRouteSource.Ai => "AI-планирование",
        _ => source.ToString(),
    };

    public static string Label(FieldVisitResult result) => result switch
    {
        FieldVisitResult.Order => "заказ",
        FieldVisitResult.Sale => "продажа",
        FieldVisitResult.Refusal => "отказ",
        FieldVisitResult.Revisit => "повторный визит",
        FieldVisitResult.Closed => "точка закрыта",
        FieldVisitResult.NoDecisionMaker => "нет ЛПР",
        FieldVisitResult.Other => "другое",
        _ => result.ToString(),
    };

    public static string Label(FieldVisitStatus status) => status switch
    {
        FieldVisitStatus.InProgress => "идёт",
        FieldVisitStatus.Completed => "завершён",
        FieldVisitStatus.Cancelled => "отменён",
        _ => status.ToString(),
    };

    public static string Label(FieldGeoStatus geo) => geo switch
    {
        FieldGeoStatus.Ok => "на месте",
        FieldGeoStatus.Far => "далеко от точки",
        FieldGeoStatus.NoGps => "нет GPS",
        FieldGeoStatus.NoTarget => "у точки нет координат",
        _ => geo.ToString(),
    };

    public static string Label(FieldRecommendationStatus status) => status switch
    {
        FieldRecommendationStatus.Pending => "ждёт решения",
        FieldRecommendationStatus.Approved => "принята",
        FieldRecommendationStatus.Rejected => "отклонена",
        FieldRecommendationStatus.Expired => "истекла",
        _ => status.ToString(),
    };
}

/// <summary>
/// Задачи отделов OneBase (раздел «Задачи»): сводка по отделам и список задач. Доступ — как на странице: WorkContext строится
/// по пользователю и его правам, отдел видят его сотрудники и руководство (право на пользователей). Чужой отдел — отказ, задачи не отдаются.
/// </summary>
internal sealed class GetDepartmentTasksTool(WorkTaskService tasks, IUserPermissions permissions, IAppDbContext db) : DataTool
{
    internal enum StatusFilter
    {
        Open,
        Overdue,
        Done,
        Cancelled,
        All,
    }

    internal static readonly IReadOnlyList<ArgChoice<StatusFilter>> Statuses =
    [
        new(StatusFilter.Open, "open", "открытые", "открыто", "открытая", "активные", "в работе", "new"),
        new(StatusFilter.Overdue, "overdue", "просроченные", "просрочено", "просроченная", "просрочка"),
        new(StatusFilter.Done, "done", "выполненные", "выполнено", "выполнена", "сделано", "completed", "closed", "закрытые"),
        new(StatusFilter.Cancelled, "cancelled", "отменённые", "отменено", "отменена", "canceled"),
        new(StatusFilter.All, "all", "все", "любые"),
    ];

    public override string Name => "get_department_tasks";
    public override string Title => "Задачи отделов";
    public override string Source => KnowledgeSources.WorkTasks;

    /// <summary>Отдельного права нет: какие отделы видны, решает WorkTaskService (сотрудники отдела и руководство).</summary>
    public override string? RequiredPermission => null;

    public override string Description =>
        "Задачи отделов OneBase (раздел «Задачи»). Возвращает: по каждому отделу, доступному пользователю, — сколько задач открыто, просрочено " +
        "и выполнено за последние days дней (по умолчанию 30); список задач — название, отдел, исполнитель, автор, приоритет, срок (yyyy-MM-dd), статус, " +
        "просрочка в днях, дата и итог выполнения или причина отмены. Аргументы: department — код (sales, production, supply, finance, marketing, hr) " +
        "или название отдела, без него — все доступные отделы; status — open (по умолчанию) / overdue / done / cancelled / all; assignee — исполнитель " +
        "(имя или «me»); days; limit — сколько задач в списке (20). Задачи отдела видят его сотрудники и руководство — к чужим отделам доступа нет. " +
        "Задачи торговых агентов и супервайзеров — не здесь, а в get_field_tasks.";

    public override string? UsageHint =>
        "Для вопросов о задачах отделов: что просрочено и на ком, сколько выполнено за месяц, «мои задачи» (assignee = me). " +
        "Задачи полевой команды продаж — get_field_tasks.";

    public override JsonElement InputSchema { get; } = Schema(
        ("department", new { type = "string", description = "Отдел: код (sales, production, supply, finance, marketing, hr) или название («Финансы»). Без отдела — все отделы, доступные пользователю." }),
        ("status", new
        {
            type = "string",
            @enum = WorkFieldText.Codes(Statuses),
            description = "open — открытые (по умолчанию), overdue — просроченные, done — выполненные за days дней, cancelled — отменённые за days дней, all — открытые и закрытые за days дней.",
        }),
        ("assignee", new { type = "string", description = "Исполнитель: имя или часть имени; «me» — сам пользователь. Без него — все исполнители." }),
        ("days", new { type = "integer", description = "Окно для выполненных и отменённых задач, дней: по умолчанию 30, до 365." }),
        ("limit", new { type = "integer", description = "Сколько задач вернуть в списке: по умолчанию 20, до 100." }));

    private sealed record Line(
        Guid DepartmentId,
        string Title,
        string? Description,
        WorkTaskPriority Priority,
        WorkTaskStatus Status,
        DateOnly? Due,
        Guid? AssigneeId,
        string? Assignee,
        string? Author,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ClosedAt,
        string? Result);

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        var status = WorkFieldText.Pick(args.Str("status"), "status", StatusFilter.Open, Statuses);
        var days = args.Int("days", 1, 365) ?? 30;
        var limit = args.Int("limit", 1, 100) ?? 20;
        var take = args.Report ? 1000 : limit;
        var userId = context.UserId ?? throw new ToolArgumentException("Задачи отделов отдаются только от имени пользователя.");
        var granted = await permissions.GetAsync(userId, ct);
        if (granted.Count == 0)
        {
            throw new ToolArgumentException("Пользователь отключён или не найден — задачи отделов недоступны.");
        }

        // Как WorkController: права пользователя + его отдел из базы; какие отделы видны — решает сервис.
        var work = await tasks.ContextAsync(userId, granted, ct);
        var all = await tasks.DepartmentsAsync(work, ct);
        var departmentArg = args.Str("department");
        var selected = SelectDepartments(all, departmentArg);
        var assignee = await AssigneeAsync(work, selected, args.Str("assignee"), userId, ct);

        var today = FieldClock.Today;
        var from = today.AddDays(1 - days);
        var since = FieldClock.DayStartUtc(from);
        var ids = selected.Select(d => d.Id).ToArray();
        var lines = new List<Line>();
        var found = 0;

        // Открытые и просроченные — сервисом, как на странице отдела: срочные выше, затем по сроку.
        if (status is StatusFilter.Open or StatusFilter.Overdue or StatusFilter.All)
        {
            var open = new List<Line>();
            foreach (var department in selected)
            {
                var page = await tasks.ListAsync(work, department.Code,
                    new WorkTaskQuery(status == StatusFilter.Overdue ? "overdue" : "open", false, false, assignee?.Id, null, 1, Math.Clamp(take, 10, WorkFieldText.PageSize)), ct);
                found += page.Total;
                open.AddRange(page.Items.Select(r => new Line(department.Id, r.Title, r.Description, r.Priority, r.Status, r.DueDate, r.AssigneeId, r.AssigneeName,
                    r.CreatedByName, r.CreatedAt, r.CompletedAt, r.Result)));
            }

            lines.AddRange(open
                .OrderByDescending(l => (int)l.Priority)
                .ThenBy(l => l.Due is null)
                .ThenBy(l => l.Due)
                .ThenByDescending(l => l.CreatedAt)
                .Take(take));
        }

        // Выполненные и отменённые за окно — свежие сверху; только отделы, которые сервис открыл пользователю.
        if (status is StatusFilter.Done or StatusFilter.Cancelled or StatusFilter.All)
        {
            var q = db.WorkTasks.AsNoTracking().Where(t => ids.Contains(t.DepartmentId));
            if (assignee is not null)
            {
                q = q.Where(t => t.AssigneeId == assignee.Id);
            }

            q = status switch
            {
                StatusFilter.Done => q.Where(t => t.Status == WorkTaskStatus.Done && t.CompletedAt >= since),
                StatusFilter.Cancelled => q.Where(t => t.Status == WorkTaskStatus.Cancelled && (t.UpdatedAt ?? t.CreatedAt) >= since),
                _ => q.Where(t => (t.Status == WorkTaskStatus.Done && t.CompletedAt >= since) || (t.Status == WorkTaskStatus.Cancelled && (t.UpdatedAt ?? t.CreatedAt) >= since)),
            };
            found += await q.CountAsync(ct);
            lines.AddRange(await q
                .OrderByDescending(t => t.CompletedAt ?? t.UpdatedAt ?? t.CreatedAt)
                .Take(take)
                .Select(t => new Line(t.DepartmentId, t.Title, t.Description, t.Priority, t.Status, t.DueDate, t.AssigneeId,
                    t.Assignee == null ? null : t.Assignee.FullName, t.CreatedBy == null ? null : t.CreatedBy.FullName, t.CreatedAt, t.CompletedAt ?? t.UpdatedAt, t.Result))
                .ToListAsync(ct));
        }

        var done = await db.WorkTasks.AsNoTracking()
            .Where(t => ids.Contains(t.DepartmentId) && t.Status == WorkTaskStatus.Done && t.CompletedAt >= since)
            .GroupBy(t => t.DepartmentId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var names = selected.ToDictionary(d => d.Id, d => d.Name);
        var shown = lines.Take(take).ToList();
        var noAccess = departmentArg is null ? all.Where(d => !d.HasTasks).Select(d => d.Name).ToList() : [];
        return Data(new
        {
            Date = WorkFieldText.Day(today),
            Departments = selected.Select(d => new
            {
                d.Code,
                d.Name,
                Mine = d.IsMine ? true : (bool?)null,
                Open = d.OpenTasks,
                Overdue = d.OverdueTasks,
                Done = done.GetValueOrDefault(d.Id),
            }),
            DonePeriod = $"{WorkFieldText.Day(from)} — {WorkFieldText.Day(today)} ({days} дн.)",
            NoAccess = noAccess.Count > 0 ? noAccess : null,
            Filter = new { Status = WorkFieldText.CodeOf(Statuses, status), Assignee = assignee?.Name },
            Found = found,
            Shown = shown.Count,
            Tasks = shown.Select(l => new
            {
                Department = selected.Count > 1 ? names.GetValueOrDefault(l.DepartmentId) : null,
                l.Title,
                Assignee = l.Assignee ?? (l.AssigneeId is null ? "без исполнителя" : null),
                Mine = l.AssigneeId == userId ? true : (bool?)null,
                l.Author,
                Priority = WorkFieldText.Label(l.Priority),
                Status = WorkFieldText.Label(l.Status),
                Due = WorkFieldText.Day(l.Due),
                OverdueDays = WorkFieldText.OverdueDays(l.Status is WorkTaskStatus.New or WorkTaskStatus.InProgress, l.Due, today),
                Closed = l.Status is WorkTaskStatus.Done or WorkTaskStatus.Cancelled ? WorkFieldText.LocalDay(l.ClosedAt) : null,
                Result = WorkFieldText.Short(l.Result, 200),
                Description = WorkFieldText.Short(l.Description, 160),
            }),
            FieldTasks = all.Any(d => d.HasField) ? "Задачи агентов и супервайзеров полевой команды продаж — инструмент get_field_tasks." : null,
            Note = "Open и Overdue — счётчики раздела «Задачи» по отделу целиком (просрочена: срок раньше сегодняшнего дня, задача не выполнена и не отменена). " +
                   "Done — выполненные за DonePeriod по дате выполнения. Found — сколько задач подходит под фильтр, Shown — сколько в списке.",
        }, selected.Select(d => new DataSource($"OneBase → Задачи → {d.Name}", $"на {WorkFieldText.Ru(today)}", $"/tasks/{d.Code}")).ToArray());
    }

    /// <summary>
    /// Отделы для ответа: без аргумента — все, чьи задачи видит пользователь; с аргументом — отдел по коду, названию или началу названия.
    /// Отдел есть, но задачи его не видны пользователю — ошибка (задачи не отдаются).
    /// </summary>
    internal static IReadOnlyList<DepartmentView> SelectDepartments(IReadOnlyList<DepartmentView> all, string? query)
    {
        var fieldHint = all.Any(d => d.HasField) ? " Задачи агентов и супервайзеров полевой команды продаж — инструмент get_field_tasks." : string.Empty;
        var visible = all.Where(d => d.HasTasks).ToList();
        if (query is null)
        {
            return visible.Count > 0
                ? visible
                : throw new ToolArgumentException("У пользователя нет доступа к задачам ни одного отдела: задачи отдела видят его сотрудники и руководство." + fieldHint);
        }

        var key = WorkFieldText.Key(query);
        if (key.StartsWith("отдел ", StringComparison.Ordinal))
        {
            key = key["отдел ".Length..].Trim();
        }

        var matches = all.Where(d => WorkFieldText.Key(d.Code) == key || WorkFieldText.Key(d.Name) == key).ToList();
        if (matches.Count == 0 && key.Length >= 3)
        {
            // «финансов», «продаж», «fin» — по началу названия или кода.
            var stem = key.Length > 5 ? key[..5] : key;
            matches = all.Where(d => WorkFieldText.Key(d.Name).StartsWith(stem, StringComparison.Ordinal) || WorkFieldText.Key(d.Code).StartsWith(stem, StringComparison.Ordinal)).ToList();
        }

        static string Names(IEnumerable<DepartmentView> departments) => string.Join(", ", departments.Select(d => $"{d.Code} ({d.Name})"));

        if (matches.Count == 0)
        {
            throw new ToolArgumentException($"Аргумент department: отдела «{query}» нет. Отделы: {Names(all)}.");
        }

        if (matches.Count > 1)
        {
            throw new ToolArgumentException($"Аргумент department: «{query}» подходит нескольким отделам — {Names(matches)}. Укажите код.");
        }

        var department = matches[0];
        if (!department.HasTasks)
        {
            var available = visible.Count > 0 ? $" Доступные отделы: {Names(visible)}." : " Доступных отделов нет.";
            throw new ToolArgumentException(
                $"Нет доступа к задачам отдела «{department.Name}»: их видят сотрудники отдела и руководство.{available}{(department.HasField ? fieldHint : string.Empty)}");
        }

        return [department];
    }

    private async Task<WorkAssignee?> AssigneeAsync(WorkContext work, IReadOnlyList<DepartmentView> departments, string? query, Guid userId, CancellationToken ct)
    {
        if (query is null)
        {
            return null;
        }

        if (WorkFieldText.IsMe(query))
        {
            return new WorkAssignee(userId, "сам пользователь", null);
        }

        // Исполнитель задачи — любой действующий пользователь OneBase: список один для всех доступных отделов.
        var people = departments.Count > 0 ? await tasks.AssigneesAsync(work, departments[0].Code, ct) : [];
        return WorkFieldText.Find(people, query, p => p.Name, "assignee", "Пользователи OneBase");
    }
}

/// <summary>Область пользователя в Sales Base для инструментов — как FieldControllerBase: участник, команды и права, никогда не аргументы модели.</summary>
internal static class FieldToolScope
{
    public static async Task<FieldScope> ResolveAsync(FieldAccess access, IUserPermissions permissions, ToolContext context, CancellationToken ct)
    {
        var userId = context.UserId ?? throw new ToolArgumentException("Данные Sales Base отдаются только от имени пользователя.");
        var granted = await permissions.GetAsync(userId, ct);
        try
        {
            return await access.ResolveAsync(userId, granted.Contains(Permissions.FieldUse), granted.Contains(Permissions.FieldManage), ct, granted.Contains(Permissions.FieldPlan));
        }
        catch (FieldForbiddenException e)
        {
            throw new ToolArgumentException(e.Message);
        }
    }

    /// <summary>Участник по имени — только из области пользователя (агент найдёт только себя). agentsOnly — только агенты.</summary>
    public static FieldMemberInfo? Member(FieldScope scope, FieldDirectory.Snapshot snapshot, string? query, bool agentsOnly)
    {
        if (query is null)
        {
            return null;
        }

        if (WorkFieldText.IsMe(query) && scope.MemberId is { } me && snapshot.Members.TryGetValue(me, out var self) && (!agentsOnly || scope.CanSeeAgent(me)))
        {
            return self;
        }

        var pool = snapshot.Members.Values
            .Where(m => m.IsActive && (agentsOnly ? scope.CanSeeAgent(m.Id) : scope.CanSeeMember(m.Id)))
            .OrderBy(m => m.FullName)
            .ToList();
        return WorkFieldText.Find(pool, query, m => m.FullName, "agent", agentsOnly ? "Агенты в зоне пользователя" : "Участники в зоне пользователя");
    }

    public static FieldTeamInfo? Team(FieldScope scope, FieldDirectory.Snapshot snapshot, string? query)
    {
        if (query is null)
        {
            return null;
        }

        var pool = snapshot.Teams.Values.Where(t => t.IsActive && scope.TeamIds.Contains(t.Id)).OrderBy(t => t.Name).ToList();
        return WorkFieldText.Find(pool, query, t => t.Name, "team", "Команды в зоне пользователя");
    }

    /// <summary>Участник команды: её агент или супервайзер.</summary>
    public static bool InTeam(FieldDirectory.Snapshot snapshot, FieldTeamInfo team, Guid memberId) =>
        team.SupervisorId == memberId || (snapshot.Members.TryGetValue(memberId, out var m) && m.TeamId == team.Id);

    public static string? TeamName(FieldDirectory.Snapshot snapshot, Guid memberId) =>
        snapshot.Members.TryGetValue(memberId, out var m) && m.TeamId is { } t ? snapshot.Teams.GetValueOrDefault(t)?.Name : null;

    public static string ScopeText(FieldScope scope) => scope.Role switch
    {
        FieldRole.Agent => "агент — только свои данные",
        FieldRole.Supervisor => "супервайзер — агенты своих команд",
        _ => scope.OrgWide ? "РМ — вся организация" : "РМ — свои филиалы",
    };
}

/// <summary>
/// Задачи полевой команды Sales Base: сводка, разбивка по исполнителям и список с причинами, сроками и итогами.
/// Открытые — сервисом FieldTaskService (как страница «Задачи»), выполненные и отменённые за окно — в пределах той же области.
/// </summary>
internal sealed class GetFieldTasksTool(
    FieldAccess access,
    FieldDirectory directory,
    FieldTaskService tasks,
    ILinkoSalesProvider linko,
    IAppDbContext db,
    IUserPermissions permissions) : DataTool
{
    private const int MaxOpen = 3000;

    internal enum StatusFilter
    {
        Open,
        Overdue,
        Today,
        Postponed,
        Done,
        Cancelled,
    }

    internal static readonly IReadOnlyList<ArgChoice<StatusFilter>> Statuses =
    [
        new(StatusFilter.Open, "open", "открытые", "открыто", "открытая", "активные", "в работе"),
        new(StatusFilter.Overdue, "overdue", "просроченные", "просрочено", "просрочка"),
        new(StatusFilter.Today, "today", "на сегодня", "сегодня", "due today"),
        new(StatusFilter.Postponed, "postponed", "отложенные", "отложено", "перенесённые"),
        new(StatusFilter.Done, "done", "выполненные", "выполнено", "completed", "verified", "подтверждённые", "closed", "закрытые"),
        new(StatusFilter.Cancelled, "cancelled", "отменённые", "отменено", "canceled"),
    ];

    public override string Name => "get_field_tasks";
    public override string Title => "Sales Base: задачи полевой команды";
    public override string Source => KnowledgeSources.FieldSales;
    public override string? RequiredPermission => Permissions.FieldUse;

    public override IReadOnlyList<string>? AlternativePermissions => [Permissions.FieldManage, Permissions.FieldPlan];

    public override string Description =>
        "Задачи полевой команды продаж (Sales Base) — агентов и супервайзеров. Возвращает: сводку — открыто, просрочено, на сегодня, отложено, " +
        "поставлено AI, выполнено за days дней; разбивку по исполнителям (открыто, просрочено, выполнено); список задач — название, исполнитель и команда, " +
        "торговая точка, приоритет, срок (yyyy-MM-dd), статус, просрочка в днях, кто поставил (AI-планирование, супервайзер, РМ, система, офис), " +
        "причина постановки (у задач AI — причина рекомендации), итог исполнителя или причина переноса/отмены. Аргументы: status — open (по умолчанию) / overdue / " +
        "today / postponed / done / cancelled; agent — исполнитель (имя или «me»); team — команда; days — окно для выполненных и отменённых (30); limit — строк (20). " +
        "Агент видит свои задачи, супервайзер — своей команды, РМ — организации или своих филиалов.";

    public override string? UsageHint =>
        "Для вопросов о задачах агентов: кто не выполняет задачи, что просрочено и почему, что поставил AI, что сделано за неделю. Задачи офиса по отделам — get_department_tasks.";

    public override JsonElement InputSchema { get; } = Schema(
        ("status", new
        {
            type = "string",
            @enum = WorkFieldText.Codes(Statuses),
            description = "open — открытые (по умолчанию), overdue — просроченные, today — срок сегодня, postponed — отложенные, done — выполненные за days дней, cancelled — отменённые за days дней.",
        }),
        ("agent", new { type = "string", description = "Исполнитель: агент или супервайзер, имя или часть имени; «me» — сам пользователь. Без него — вся зона пользователя." }),
        ("team", new { type = "string", description = "Команда: название (например «Ташкент»). Без команды — вся зона пользователя." }),
        ("days", new { type = "integer", description = "Окно для выполненных и отменённых задач, дней: по умолчанию 30, до 180." }),
        ("limit", new { type = "integer", description = "Сколько задач вернуть в списке: по умолчанию 20, до 100." }));

    private sealed record Line(
        string Title,
        string? Description,
        Guid AssignedToId,
        string? Market,
        FieldPriority Priority,
        FieldTaskStatus Status,
        DateOnly? Due,
        FieldActorType By,
        string? Author,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ClosedAt,
        string? Result,
        bool FromAi);

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        var status = WorkFieldText.Pick(args.Str("status"), "status", StatusFilter.Open, Statuses);
        var days = args.Int("days", 1, 180) ?? 30;
        var limit = args.Int("limit", 1, 100) ?? 20;
        var take = args.Report ? 1000 : limit;
        var scope = await FieldToolScope.ResolveAsync(access, permissions, context, ct);
        var snapshot = await directory.GetAsync(ct);
        var who = FieldToolScope.Member(scope, snapshot, args.Str("agent"), agentsOnly: false);
        var team = FieldToolScope.Team(scope, snapshot, args.Str("team"));
        var members = scope.MemberIds.Where(id => (who is null || id == who.Id) && (team is null || FieldToolScope.InTeam(snapshot, team, id))).ToHashSet();
        var ids = members.ToArray();
        var today = FieldClock.Today;
        var from = today.AddDays(1 - days);
        var since = FieldClock.DayStartUtc(from);

        // Открытые — сервисом Sales Base, тем же порядком, что на странице: просроченные выше, затем приоритет и срок.
        var (openAll, _, openCut) = await WorkFieldText.AllPagesAsync(
            n => tasks.ListAsync(scope, new FieldTaskQuery("open", who?.Id, null, null, null, null, null, n, WorkFieldText.PageSize), ct), MaxOpen);
        var open = openAll.Where(t => members.Contains(t.AssignedToId)).ToList();

        // Выполненные (и подтверждённые) за окно — по исполнителям области.
        var done = await db.FieldTasks.AsNoTracking()
            .Where(t => ids.Contains(t.AssignedToId) && (t.Status == FieldTaskStatus.Completed || t.Status == FieldTaskStatus.Verified) && t.CompletedAt >= since)
            .GroupBy(t => t.AssignedToId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        List<Line> lines;
        int found;
        if (status is StatusFilter.Done or StatusFilter.Cancelled)
        {
            var q = db.FieldTasks.AsNoTracking().Where(t => ids.Contains(t.AssignedToId));
            q = status == StatusFilter.Done
                ? q.Where(t => (t.Status == FieldTaskStatus.Completed || t.Status == FieldTaskStatus.Verified) && t.CompletedAt >= since)
                : q.Where(t => t.Status == FieldTaskStatus.Cancelled && (t.UpdatedAt ?? t.CreatedAt) >= since);
            found = await q.CountAsync(ct);
            var closed = await q.OrderByDescending(t => t.CompletedAt ?? t.UpdatedAt ?? t.CreatedAt).Take(take).ToListAsync(ct);
            var markets = await linko.MarketsAsync(closed.Where(t => t.MarketId != null).Select(t => t.MarketId!.Value).Distinct().ToList(), ct);
            lines = closed.Select(t => new Line(t.Title, t.Description, t.AssignedToId, t.MarketId is { } m ? markets.GetValueOrDefault(m)?.Name ?? $"Точка {m}" : null,
                t.Priority, t.Status, t.DueDate, t.CreatedByType, AuthorOf(t, snapshot), t.CreatedAt, t.CompletedAt ?? t.UpdatedAt, t.Result, t.RecommendationId is not null)).ToList();
        }
        else
        {
            var picked = (status switch
            {
                StatusFilter.Overdue => open.Where(t => t.Overdue),
                StatusFilter.Today => open.Where(t => t.DueDate == today),
                StatusFilter.Postponed => open.Where(t => t.Status == FieldTaskStatus.Postponed),
                _ => open,
            }).ToList();
            found = picked.Count;
            lines = picked.Take(take).Select(t => new Line(t.Title, t.Description, t.AssignedToId, t.MarketName, t.Priority, t.Status, t.DueDate, t.CreatedByType,
                t.CreatedBy, t.CreatedAt, t.CompletedAt, t.Result, t.RecommendationId is not null)).ToList();
        }

        var openBy = open.ToLookup(t => t.AssignedToId);
        var people = open.Select(t => t.AssignedToId).Concat(done.Keys).Distinct()
            .Select(id => new
            {
                Name = snapshot.NameOf(id),
                Team = FieldToolScope.TeamName(snapshot, id),
                Open = openBy[id].Count(),
                Overdue = openBy[id].Count(t => t.Overdue),
                Postponed = openBy[id].Count(t => t.Status == FieldTaskStatus.Postponed),
                Done = done.GetValueOrDefault(id),
            })
            .OrderByDescending(x => x.Overdue).ThenByDescending(x => x.Open).ThenBy(x => x.Name)
            .ToList();
        var closedList = status is StatusFilter.Done or StatusFilter.Cancelled;

        return Data(new
        {
            Date = WorkFieldText.Day(today),
            Scope = FieldToolScope.ScopeText(scope),
            Filter = new { Status = WorkFieldText.CodeOf(Statuses, status), Agent = who?.FullName, Team = team?.Name },
            DonePeriod = $"{WorkFieldText.Day(from)} — {WorkFieldText.Day(today)} ({days} дн.)",
            Summary = new
            {
                Open = open.Count,
                Overdue = open.Count(t => t.Overdue),
                DueToday = open.Count(t => t.DueDate == today),
                Postponed = open.Count(t => t.Status == FieldTaskStatus.Postponed),
                FromAi = open.Count(t => t.CreatedByType == FieldActorType.Ai),
                Done = done.Values.Sum(),
            },
            ByAssignee = who is null && !scope.IsAgent ? people.Take(args.Cap(30)) : null,
            Found = found,
            Shown = lines.Count,
            Tasks = lines.Select(l => new
            {
                l.Title,
                Assignee = snapshot.NameOf(l.AssignedToId),
                Team = team is null && !scope.IsAgent ? FieldToolScope.TeamName(snapshot, l.AssignedToId) : null,
                l.Market,
                Priority = WorkFieldText.Label(l.Priority),
                Status = WorkFieldText.Label(l.Status),
                Due = WorkFieldText.Day(l.Due),
                OverdueDays = WorkFieldText.OverdueDays(FieldTaskRules.IsOpen(l.Status), l.Due, today),
                SetBy = l.By is FieldActorType.Ai or FieldActorType.System || l.Author is null ? WorkFieldText.Label(l.By) : $"{WorkFieldText.Label(l.By)} {l.Author}",
                Created = WorkFieldText.LocalDay(l.CreatedAt),
                Closed = closedList ? WorkFieldText.LocalDay(l.ClosedAt) : null,
                Reason = WorkFieldText.Short(l.Description, 300),
                Result = WorkFieldText.Short(l.Result, 300),
                FromAiRecommendation = l.FromAi ? true : (bool?)null,
            }),
            Note = "Открытые — новая, принята, в работе, отложена; просрочена — срок раньше сегодняшнего дня. Done — выполненные и подтверждённые супервайзером " +
                   "по дате выполнения за DonePeriod. Reason — описание задачи (у задач AI — причина рекомендации), Result — итог исполнителя или причина переноса/отмены." +
                   (openCut ? $" Открытых задач больше {MaxOpen} — сводка по первым {MaxOpen}." : string.Empty),
        }, new DataSource("Sales Base → Задачи", $"на {WorkFieldText.Ru(today)}", WorkFieldText.Link("/field/tasks",
            ("view", status switch
            {
                StatusFilter.Overdue => "overdue",
                StatusFilter.Today => "today",
                StatusFilter.Done or StatusFilter.Cancelled => "closed",
                _ => null,
            }),
            ("agent", who?.Id.ToString()))));
    }

    private static string? AuthorOf(FieldTask task, FieldDirectory.Snapshot snapshot) => task.CreatedByType switch
    {
        FieldActorType.Ai or FieldActorType.System => null,
        _ => task.CreatedById is { } member ? snapshot.NameOf(member) : null,
    };
}

/// <summary>
/// Маршруты и визиты Sales Base за день или период: по агенту — точки плана по порядку с итогом каждой, по команде — сводка по агентам
/// и причины пропусков. Данные — FieldRouteService и FieldVisitService в области пользователя, заказы — зеркало Linko.
/// </summary>
internal sealed class GetFieldRoutesTool(
    FieldAccess access,
    FieldDirectory directory,
    FieldRouteService routes,
    FieldVisitService visits,
    ILinkoSalesProvider linko,
    IUserPermissions permissions) : DataTool
{
    internal const int MaxDays = 31;
    private const int MaxVisits = 3000;
    private const int MaxRouteViews = 40;

    public override string Name => "get_field_routes";
    public override string Title => "Sales Base: маршруты и визиты";
    public override string Source => KnowledgeSources.FieldSales;
    public override string? RequiredPermission => Permissions.FieldUse;

    public override IReadOnlyList<string>? AlternativePermissions => [Permissions.FieldManage, Permissions.FieldPlan];

    public override string Description =>
        "Маршруты и визиты полевой команды продаж (Sales Base) за день или период до 31 дня. С агентом (agent) — по дням: статус и источник маршрута, " +
        "точки плана по порядку со статусом (посещена, пропущена с причиной, не посещена), время прибытия и ухода, результат визита (заказ, отказ, нет ЛПР…), " +
        "сумма и комментарий агента, визиты вне маршрута, заказы агента в Linko за день; за период — пропущенные точки с причинами. Без агента — по команде " +
        "или всей зоне: по каждому агенту точки плана, посещено, пропущено, не посещено, охват %, визиты и визиты с заказом, заказы Linko; агенты без маршрута; " +
        "частые причины пропуска и пропущенные точки; отказы на визитах с комментариями. Аргументы: date (yyyy-MM-dd, по умолчанию сегодня) или from и to " +
        "(yyyy-MM-dd); agent; team; limit — строк в списках (20). Агент видит только себя, супервайзер — команду, РМ — организацию или свои филиалы.";

    public override string? UsageHint =>
        "Для вопросов «как агент прошёл маршрут», «почему пропущены точки», «кто хуже выполняет маршрут», «сколько заказов с визитов». " +
        "За прошлый день передай date, за неделю — from и to. Продажи команды против плана — get_field_team.";

    public override JsonElement InputSchema { get; } = Schema(
        ("date", new { type = "string", description = "День yyyy-MM-dd. Без даты (и без from/to) — сегодня." }),
        ("from", new { type = "string", description = "Начало периода yyyy-MM-dd (вместо date). Период — до 31 дня; без to — по сегодня." }),
        ("to", new { type = "string", description = "Конец периода yyyy-MM-dd (вместо date)." }),
        ("agent", new { type = "string", description = "Агент: имя или часть имени. С агентом — его маршрут по точкам и визиты. Агент всегда видит только себя." }),
        ("team", new { type = "string", description = "Команда: название. Без агента и команды — вся зона пользователя." }),
        ("limit", new { type = "integer", description = "Сколько строк в списках (пропущенные точки, визиты, отказы): по умолчанию 20, до 100." }));

    private sealed record AgentLine(string Name, string? Team, int Routes, int Points, int Visited, int Skipped, int Visits, int WithOrder, decimal OrderAmount,
        int LinkoOrders, decimal LinkoSum, decimal LinkoKg)
    {
        public decimal? Coverage => Points > 0 ? (decimal)Visited / Points : null;
    }

    private sealed record SkipLine(DateOnly Date, string Agent, string Market, string? Reason);

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        var (from, to) = Period(args);
        var limit = args.Int("limit", 1, 100) ?? 20;
        var take = args.Report ? 1000 : limit;
        var scope = await FieldToolScope.ResolveAsync(access, permissions, context, ct);
        var snapshot = await directory.GetAsync(ct);
        var agent = FieldToolScope.Member(scope, snapshot, args.Str("agent"), agentsOnly: true)
                    ?? (scope.IsAgent && scope.MemberId is { } me ? snapshot.Members.GetValueOrDefault(me) : null);
        if (agent is not null)
        {
            return await AgentAsync(scope, snapshot, agent, from, to, take, ct);
        }

        if (scope.IsAgent)
        {
            throw new ToolArgumentException("Агент видит только свой маршрут.");
        }

        return await TeamAsync(scope, snapshot, FieldToolScope.Team(scope, snapshot, args.Str("team")), from, to, take, args, ct);
    }

    /// <summary>День (date) или период (from, to) не длиннее MaxDays; без аргументов — сегодня.</summary>
    internal static (DateOnly From, DateOnly To) Period(ToolArgs args)
    {
        var date = WorkFieldText.Date(args, "date");
        var from = WorkFieldText.Date(args, "from");
        var to = WorkFieldText.Date(args, "to");
        if (date is { } day)
        {
            return from is null && to is null ? (day, day) : throw new ToolArgumentException("Укажите либо date (один день), либо from и to (период), но не всё сразу.");
        }

        var today = FieldClock.Today;
        var start = from ?? to ?? today;
        var end = to ?? (from is { } f && f < today ? today : start);
        if (end < start)
        {
            throw new ToolArgumentException($"Аргумент to ({WorkFieldText.Day(end)}) раньше from ({WorkFieldText.Day(start)}).");
        }

        var length = end.DayNumber - start.DayNumber + 1;
        return length <= MaxDays
            ? (start, end)
            : throw new ToolArgumentException($"Период — не больше {MaxDays} дней, а {WorkFieldText.Day(start)} — {WorkFieldText.Day(end)} это {length} дн. Сузьте from/to.");
    }

    private async Task<(List<FieldVisitRow> Rows, bool Truncated)> VisitsAsync(FieldScope scope, Guid? agentId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var (rows, _, cut) = await WorkFieldText.AllPagesAsync(
            n => visits.ListAsync(scope, new FieldVisitQuery(agentId, null, from, to, null, null, n, WorkFieldText.PageSize), ct), MaxVisits);
        return (rows.Where(v => v.Status != FieldVisitStatus.Cancelled).ToList(), cut);
    }

    private static bool IsOrder(FieldVisitRow v) => v.Result is FieldVisitResult.Order or FieldVisitResult.Sale;

    private static object VisitSummary(IReadOnlyList<FieldVisitRow> rows, bool truncated) => new
    {
        Total = rows.Count,
        WithOrder = rows.Count(IsOrder),
        OrderAmount = R(rows.Where(IsOrder).Sum(v => v.Amount ?? 0)),
        ByResult = rows.Where(v => v.Result is not null).GroupBy(v => v.Result!.Value).OrderByDescending(g => g.Count())
            .ToDictionary(g => WorkFieldText.Label(g.Key), g => g.Count()),
        InProgress = rows.Count(v => v.Status == FieldVisitStatus.InProgress),
        FarFromPoint = rows.Count(v => v.GeoStatus == FieldGeoStatus.Far),
        Note = truncated ? $"Визитов больше {MaxVisits} — посчитаны последние {MaxVisits}." : null,
    };

    private static object VisitLine(FieldVisitRow v, bool withAgent) => new
    {
        Date = WorkFieldText.LocalDay(v.StartedAt),
        Time = WorkFieldText.LocalTime(v.StartedAt),
        Agent = withAgent ? v.AgentName : null,
        Market = v.MarketName,
        Status = v.Status == FieldVisitStatus.Completed ? null : WorkFieldText.Label(v.Status),
        Result = v.Result is { } r ? WorkFieldText.Label(r) : null,
        Amount = R(v.Amount),
        v.Minutes,
        Comment = WorkFieldText.Short(v.Comment, 200),
        Geo = v.GeoStatus == FieldGeoStatus.Ok ? null : WorkFieldText.Label(v.GeoStatus),
        OutsideRoute = v.RouteId is null ? true : (bool?)null,
    };

    private async Task<ToolResult> AgentAsync(FieldScope scope, FieldDirectory.Snapshot snapshot, FieldMemberInfo agent, DateOnly from, DateOnly to, int take, CancellationToken ct)
    {
        var single = from == to;
        var linkoIds = agent.LinkoUserId is { } l ? new List<long> { l } : [];
        var sales = (await linko.AgentDaySalesAsync(linkoIds, from, to, SalesBasis.Taken, ct))
            .GroupBy(s => s.Date)
            .ToDictionary(g => g.Key, g => (Orders: g.Sum(x => x.Orders), Sum: g.Sum(x => x.Sum), Kg: g.Sum(x => x.Kg)));
        var (visitRows, visitsCut) = await VisitsAsync(scope, agent.Id, from, to, ct);
        var visitById = visitRows.ToDictionary(v => v.Id);
        var inPoints = new HashSet<Guid>();
        var dayLines = new List<object>();
        var skipped = new List<SkipLine>();
        Guid? lastRoute = null;
        int routesCount = 0, planned = 0, visited = 0, skippedCount = 0;

        object Point(FieldRoutePointView p)
        {
            var visit = p.VisitId is { } id ? visitById.GetValueOrDefault(id) : null;
            if (visit is not null)
            {
                inPoints.Add(visit.Id);
            }

            return new
            {
                p.Sequence,
                Market = p.Name,
                p.Address,
                PlannedTime = p.PlannedTime?.ToString("HH:mm", CultureInfo.InvariantCulture),
                Status = WorkFieldText.Label(p.Status),
                VisitInLinko = p.LinkoDone ? true : (bool?)null,
                Arrival = WorkFieldText.LocalTime(p.Arrival),
                Departure = WorkFieldText.LocalTime(p.Departure),
                SkipReason = p.Status == FieldPointStatus.Skipped ? p.Note ?? "причина не указана" : null,
                Note = p.Status == FieldPointStatus.Skipped ? null : WorkFieldText.Short(p.Note, 200),
                Visit = visit is null
                    ? null
                    : new
                    {
                        Result = visit.Result is { } r ? WorkFieldText.Label(r) : WorkFieldText.Label(visit.Status),
                        Amount = R(visit.Amount),
                        visit.Minutes,
                        Comment = WorkFieldText.Short(visit.Comment, 200),
                        Geo = visit.GeoStatus == FieldGeoStatus.Ok ? null : WorkFieldText.Label(visit.GeoStatus),
                    },
                OpenTasks = p.OpenTasks > 0 ? p.OpenTasks : (int?)null,
                Priority = p.Priority is FieldPriority.High or FieldPriority.Urgent ? WorkFieldText.Label(p.Priority) : null,
            };
        }

        for (var day = from; day <= to; day = day.AddDays(1))
        {
            FieldRouteView? view;
            FieldRouteSummary? summary = null;
            if (single)
            {
                view = await routes.GetForAsync(scope, agent.Id, day, ct);
            }
            else
            {
                summary = (await routes.ListAsync(scope, day, agent.Id, null, ct)).FirstOrDefault();
                view = summary is { Skipped: > 0 } ? await routes.GetAsync(scope, summary.Id, ct) : null;
            }

            var dayVisits = visitRows.Count(v => FieldClock.LocalDate(v.StartedAt) == day);
            var sale = sales.GetValueOrDefault(day);
            if (view is null && summary is null)
            {
                if (single || dayVisits > 0 || sale.Orders > 0)
                {
                    dayLines.Add(new { Date = WorkFieldText.Day(day), Route = "маршрута нет", Visits = dayVisits, LinkoOrders = sale.Orders, LinkoSum = R(sale.Sum), LinkoKg = R(sale.Kg) });
                }

                continue;
            }

            var points = view?.Planned ?? summary!.Planned;
            var done = view?.Visited ?? summary!.Visited;
            var skip = view?.Skipped ?? summary!.Skipped;
            routesCount++;
            planned += points;
            visited += done;
            skippedCount += skip;
            lastRoute = view?.Id ?? summary!.Id;
            if (view is not null)
            {
                skipped.AddRange(view.Points.Where(p => p.Status == FieldPointStatus.Skipped).Select(p => new SkipLine(day, view.AgentName, p.Name, WorkFieldText.Short(p.Note, 200))));
            }

            dayLines.Add(new
            {
                Date = WorkFieldText.Day(day),
                Status = WorkFieldText.Label(view?.Status ?? summary!.Status),
                Source = WorkFieldText.Label(view?.Source ?? summary!.Source),
                Planned = points,
                Visited = done,
                Skipped = skip,
                NotVisited = points - done - skip,
                DistanceKm = R(view?.DistanceKm ?? summary!.DistanceKm, 1),
                EstimatedMinutes = view?.EstimatedMinutes ?? summary!.EstimatedMinutes,
                Visits = dayVisits,
                LinkoOrders = sale.Orders,
                LinkoSum = R(sale.Sum),
                LinkoKg = R(sale.Kg),
                Points = single ? view!.Points.Select(Point).ToList() : null,
            });
        }

        // За день визиты маршрута уже в точках — отдельно только визиты вне маршрута; за период — последние визиты.
        var visitList = visitRows.Where(v => !single || !inPoints.Contains(v.Id)).Take(take).Select(v => VisitLine(v, withAgent: false)).ToList();
        var period = WorkFieldText.RuPeriod(from, to);
        var routeLink = single && lastRoute is { } routeId
            ? $"/field/routes/{routeId}"
            : WorkFieldText.Link("/field/routes", ("date", WorkFieldText.Day(to)), ("agent", agent.Id.ToString()));
        return Data(new
        {
            Period = new { From = WorkFieldText.Day(from), To = WorkFieldText.Day(to) },
            Agent = agent.FullName,
            Team = FieldToolScope.TeamName(snapshot, agent.Id),
            Totals = new
            {
                Routes = routesCount,
                Points = planned,
                Visited = visited,
                Skipped = skippedCount,
                NotVisited = planned - visited - skippedCount,
                CoveragePct = planned > 0 ? Pct((decimal)visited / planned) : null,
                LinkoOrders = sales.Values.Sum(s => s.Orders),
                LinkoSum = R(sales.Values.Sum(s => s.Sum)),
                LinkoKg = R(sales.Values.Sum(s => s.Kg)),
            },
            Visits = VisitSummary(visitRows, visitsCut),
            Days = dayLines,
            SkippedPoints = single ? null : skipped.Take(take).Select(s => new { Date = WorkFieldText.Day(s.Date), s.Market, Reason = s.Reason ?? "причина не указана" }),
            VisitList = visitList.Count > 0 ? visitList : null,
            Note = "Посещена — визит Sales Base или выполненный визит Linko в этот день; пропущена — агент отметил пропуск (с причиной); не посещена — точка плана " +
                   "без визита и без отметки (за сегодня — ещё впереди). Заказы Linko — принятые агентом за день (по дате создания, без отменённых). " +
                   "Сумма визита — со слов агента при завершении визита." + (agent.LinkoUserId is null ? " У агента нет сотрудника Linko — заказов и визитов Linko нет." : string.Empty),
        },
            new DataSource($"Sales Base → Маршруты → {agent.FullName}", period, routeLink),
            new DataSource("Sales Base → Визиты", period, WorkFieldText.Link("/field/visits", ("agent", agent.Id.ToString()))));
    }

    private async Task<ToolResult> TeamAsync(FieldScope scope, FieldDirectory.Snapshot snapshot, FieldTeamInfo? team, DateOnly from, DateOnly to, int take, ToolArgs args, CancellationToken ct)
    {
        var agents = scope.AgentIds
            .Select(id => snapshot.Members.GetValueOrDefault(id))
            .OfType<FieldMemberInfo>()
            .Where(m => m.IsActive && (team is null || m.TeamId == team.Id))
            .ToList();
        var agentIds = agents.Select(a => a.Id).ToHashSet();
        var summaries = new List<FieldRouteSummary>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            summaries.AddRange((await routes.ListAsync(scope, day, null, team?.Id, ct)).Where(s => agentIds.Contains(s.AgentId)));
        }

        var linkoIds = agents.Where(a => a.LinkoUserId is not null).Select(a => a.LinkoUserId!.Value).Distinct().ToList();
        var sales = (await linko.AgentDaySalesAsync(linkoIds, from, to, SalesBasis.Taken, ct))
            .GroupBy(s => s.LinkoUserId)
            .ToDictionary(g => g.Key, g => (Orders: g.Sum(x => x.Orders), Sum: g.Sum(x => x.Sum), Kg: g.Sum(x => x.Kg)));
        var (allVisits, visitsCut) = await VisitsAsync(scope, null, from, to, ct);
        var teamVisits = allVisits.Where(v => agentIds.Contains(v.AgentId)).ToList();

        // Причины пропуска — в точках маршрута: свежие маршруты с пропусками, не больше MaxRouteViews.
        var skippedRoutes = summaries.Where(s => s.Skipped > 0).OrderByDescending(s => s.Date).ThenByDescending(s => s.Skipped).ToList();
        var skipped = new List<SkipLine>();
        foreach (var s in skippedRoutes.Take(MaxRouteViews))
        {
            var view = await routes.GetAsync(scope, s.Id, ct);
            skipped.AddRange(view.Points.Where(p => p.Status == FieldPointStatus.Skipped).Select(p => new SkipLine(s.Date, s.AgentName, p.Name, WorkFieldText.Short(p.Note, 200))));
        }

        var rows = agents.Select(a =>
        {
            var own = summaries.Where(s => s.AgentId == a.Id).ToList();
            var visitsOf = teamVisits.Where(v => v.AgentId == a.Id).ToList();
            var sale = a.LinkoUserId is { } lu ? sales.GetValueOrDefault(lu) : default;
            return new AgentLine(a.FullName, FieldToolScope.TeamName(snapshot, a.Id), own.Count, own.Sum(s => s.Planned), own.Sum(s => s.Visited), own.Sum(s => s.Skipped),
                visitsOf.Count, visitsOf.Count(IsOrder), visitsOf.Where(IsOrder).Sum(v => v.Amount ?? 0), sale.Orders, sale.Sum, sale.Kg);
        }).ToList();
        var withRoute = rows.Where(r => r.Routes > 0).OrderBy(r => r.Coverage ?? 2).ThenByDescending(r => r.Skipped).ThenBy(r => r.Name).ToList();
        var points = summaries.Sum(s => s.Planned);
        var visited = summaries.Sum(s => s.Visited);
        var skippedCount = summaries.Sum(s => s.Skipped);
        var refusals = teamVisits.Where(v => v.Result is FieldVisitResult.Refusal or FieldVisitResult.Closed or FieldVisitResult.NoDecisionMaker).Take(take)
            .Select(v => VisitLine(v, withAgent: true)).ToList();
        var period = WorkFieldText.RuPeriod(from, to);

        return Data(new
        {
            Period = new { From = WorkFieldText.Day(from), To = WorkFieldText.Day(to) },
            Scope = FieldToolScope.ScopeText(scope),
            Team = team?.Name,
            Totals = new
            {
                Agents = agents.Count,
                AgentsWithRoute = withRoute.Count,
                Routes = summaries.Count,
                Points = points,
                Visited = visited,
                Skipped = skippedCount,
                NotVisited = points - visited - skippedCount,
                CoveragePct = points > 0 ? Pct((decimal)visited / points) : null,
                LinkoOrders = rows.Sum(r => r.LinkoOrders),
                LinkoSum = R(rows.Sum(r => r.LinkoSum)),
                LinkoKg = R(rows.Sum(r => r.LinkoKg)),
            },
            Visits = VisitSummary(teamVisits, visitsCut),
            Agents = withRoute.Take(args.Cap(40)).Select(r => new
            {
                r.Name,
                Team = team is null ? r.Team : null,
                r.Routes,
                r.Points,
                r.Visited,
                r.Skipped,
                NotVisited = r.Points - r.Visited - r.Skipped,
                CoveragePct = Pct(r.Coverage),
                r.Visits,
                VisitsWithOrder = r.WithOrder,
                VisitOrderAmount = R(r.OrderAmount),
                r.LinkoOrders,
                LinkoSum = R(r.LinkoSum),
                LinkoKg = R(r.LinkoKg),
            }),
            AgentsWithoutRoute = rows.Where(r => r.Routes == 0).Select(r => r.Name).Take(args.Cap(40)).ToList() is { Count: > 0 } none ? none : null,
            SkipReasons = skipped.GroupBy(s => s.Reason ?? "причина не указана").OrderByDescending(g => g.Count()).Take(10)
                .Select(g => new { Reason = g.Key, Count = g.Count() }),
            SkippedPoints = skipped.Take(take).Select(s => new { Date = WorkFieldText.Day(s.Date), s.Agent, s.Market, Reason = s.Reason ?? "причина не указана" }),
            Refusals = refusals.Count > 0 ? refusals : null,
            Note = "Агенты — от худшего охвата маршрута (посещено / точек плана). Посещена — визит Sales Base или выполненный визит Linko; пропущена — агент отметил " +
                   "пропуск; не посещена — без визита и отметки (за сегодня — ещё впереди). Заказы Linko — принятые агентами за период (без отменённых); " +
                   "VisitOrderAmount — суммы, которые агенты указали на визитах с заказом." +
                   (skippedRoutes.Count > MaxRouteViews ? $" Причины пропуска — по {MaxRouteViews} последним маршрутам с пропусками из {skippedRoutes.Count}." : string.Empty),
        },
            new DataSource(team is null ? "Sales Base → Маршруты" : $"Sales Base → Маршруты → {team.Name}", period, WorkFieldText.Link("/field/routes", ("date", WorkFieldText.Day(to)))),
            new DataSource("Sales Base → Визиты", period, "/field/visits"));
    }
}

/// <summary>
/// Рекомендации AI-планирования Sales Base: что предложено, почему, по каким точкам и агентам, что решили супервайзер и РМ.
/// Видят супервайзер (по своей команде) и РМ — как страница «AI-планирование»; агенту рекомендации не показываются.
/// </summary>
internal sealed class GetFieldRecommendationsTool(
    FieldAccess access,
    FieldDirectory directory,
    FieldRecommendationService recommendations,
    IUserPermissions permissions) : DataTool
{
    private const int MaxRows = 2000;

    internal enum StatusFilter
    {
        Pending,
        Approved,
        Rejected,
        Expired,
        Decided,
        All,
    }

    internal static readonly IReadOnlyList<ArgChoice<StatusFilter>> Statuses =
    [
        new(StatusFilter.Pending, "pending", "ждут решения", "новые", "new", "ожидают", "ожидающие", "на рассмотрении"),
        new(StatusFilter.Approved, "approved", "принятые", "принято", "подтверждённые", "одобренные", "accepted"),
        new(StatusFilter.Rejected, "rejected", "отклонённые", "отклонено", "отклонены"),
        new(StatusFilter.Expired, "expired", "истёкшие", "истекли", "устаревшие"),
        new(StatusFilter.Decided, "decided", "с решением", "решённые", "закрытые"),
        new(StatusFilter.All, "all", "все"),
    ];

    internal static readonly IReadOnlyList<ArgChoice<FieldRecommendationKind>> Kinds =
    [
        new(FieldRecommendationKind.SalesDecline, "sales_decline", "падение продаж", "SalesDecline"),
        new(FieldRecommendationKind.NotVisited, "not_visited", "давно не посещали", "NotVisited", "не посещали"),
        new(FieldRecommendationKind.LostCustomer, "lost_customer", "клиент перестал покупать", "LostCustomer", "потерянный клиент"),
        new(FieldRecommendationKind.AgentBehindPlan, "agent_behind_plan", "отставание от плана", "AgentBehindPlan"),
        new(FieldRecommendationKind.SkippedPoint, "skipped_point", "пропущенная точка", "SkippedPoint"),
        new(FieldRecommendationKind.HighPotential, "high_potential", "высокий потенциал", "HighPotential"),
        new(FieldRecommendationKind.Reassign, "reassign", "точка без агента", "Reassign"),
    ];

    public override string Name => "get_field_recommendations";
    public override string Title => "Sales Base: рекомендации AI-планирования";
    public override string Source => KnowledgeSources.FieldSales;
    public override string? RequiredPermission => Permissions.FieldUse;

    public override IReadOnlyList<string>? AlternativePermissions => [Permissions.FieldManage, Permissions.FieldPlan];

    public override string Description =>
        "Рекомендации AI-планирования Sales Base: что предложено по торговым точкам и агентам и что решили супервайзер или РМ. Возвращает: сколько рекомендаций " +
        "ждут решения, принято, отклонено, истекло; разбивку по видам и агентам; список — вид (падение продаж, давно не посещали, клиент перестал покупать, " +
        "отставание от плана, пропущенная точка, высокий потенциал, точка без агента), заголовок, причина с цифрами, статус, агент, супервайзер, точка, приоритет, " +
        "срок (yyyy-MM-dd), уверенность %, кто и когда решил, комментарий решения, создана ли задача. Аргументы: status — pending (по умолчанию) / approved / " +
        "rejected / expired / decided / all; kind; agent; team; limit (20). Видят супервайзер (своя команда) и РМ; агенту рекомендации не показываются.";

    public override string? UsageHint =>
        "Для вопросов «что предлагает AI-планирование», «какие точки требуют визита», «что отклонили супервайзеры и почему». Принятые рекомендации становятся задачами агентов — их выполнение смотри в get_field_tasks.";

    public override JsonElement InputSchema { get; } = Schema(
        ("status", new
        {
            type = "string",
            @enum = WorkFieldText.Codes(Statuses),
            description = "pending — ждут решения (по умолчанию), approved — принятые, rejected — отклонённые, expired — истёкшие, decided — все с решением, all — все.",
        }),
        ("kind", new
        {
            type = "string",
            @enum = WorkFieldText.Codes(Kinds),
            description = "Вид: sales_decline — падение продаж, not_visited — давно не посещали, lost_customer — клиент перестал покупать, agent_behind_plan — отставание от плана, " +
                          "skipped_point — пропущенная точка, high_potential — высокий потенциал, reassign — точка без агента. Без вида — все.",
        }),
        ("agent", new { type = "string", description = "Агент: имя или часть имени. Без агента — вся зона пользователя." }),
        ("team", new { type = "string", description = "Команда: название. Без команды — вся зона пользователя." }),
        ("limit", new { type = "integer", description = "Сколько рекомендаций вернуть в списке: по умолчанию 20, до 100." }));

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        var status = WorkFieldText.Pick(args.Str("status"), "status", StatusFilter.Pending, Statuses);
        FieldRecommendationKind? kind = args.Str("kind") is { } k ? WorkFieldText.Pick(k, "kind", default(FieldRecommendationKind), Kinds) : null;
        var limit = args.Int("limit", 1, 100) ?? 20;
        var take = args.Report ? 1000 : limit;
        var scope = await FieldToolScope.ResolveAsync(access, permissions, context, ct);
        if (!scope.CanPlan)
        {
            throw new ToolArgumentException("Рекомендации AI-планирования видят супервайзер и РМ. Агенту принятые рекомендации приходят задачами — смотри get_field_tasks.");
        }

        var snapshot = await directory.GetAsync(ct);
        var agent = FieldToolScope.Member(scope, snapshot, args.Str("agent"), agentsOnly: true);
        var team = FieldToolScope.Team(scope, snapshot, args.Str("team"));
        var serviceStatus = ServiceStatus(status);

        var (rows, total, cut) = await WorkFieldText.AllPagesAsync(
            n => recommendations.ListAsync(scope, new FieldRecommendationQuery(serviceStatus, kind, agent?.Id, n, WorkFieldText.PageSize), ct), MaxRows);
        if (team is not null)
        {
            rows = rows.Where(r => r.AgentId is { } a && snapshot.Members.GetValueOrDefault(a)?.TeamId == team.Id).ToList();
        }

        // Счётчики по статусам — итоги сервиса с теми же фильтрами (без команды: её сервис не фильтрует).
        object? counts = null;
        if (team is null)
        {
            async Task<int> Count(string s) => (await recommendations.ListAsync(scope, new FieldRecommendationQuery(s, kind, agent?.Id, 1, 10), ct)).Total;
            counts = new
            {
                Pending = await Count(ServiceStatus(StatusFilter.Pending)),
                Approved = await Count(ServiceStatus(StatusFilter.Approved)),
                Rejected = await Count(ServiceStatus(StatusFilter.Rejected)),
                Expired = await Count(ServiceStatus(StatusFilter.Expired)),
            };
        }

        var ordered = rows.Where(r => r.Status == FieldRecommendationStatus.Pending)
            .Concat(rows.Where(r => r.Status != FieldRecommendationStatus.Pending).OrderByDescending(r => r.DecidedAt ?? r.CreatedAt))
            .Take(take)
            .ToList();
        var today = FieldClock.Today;

        return Data(new
        {
            Date = WorkFieldText.Day(today),
            Scope = FieldToolScope.ScopeText(scope),
            Filter = new
            {
                Status = WorkFieldText.CodeOf(Statuses, status),
                Kind = kind is { } fk ? WorkFieldText.LabelOf(Kinds, fk) : null,
                Agent = agent?.FullName,
                Team = team?.Name,
            },
            StatusCounts = counts,
            Found = team is null ? total : rows.Count,
            Shown = ordered.Count,
            ByKind = rows.GroupBy(r => r.Kind).OrderByDescending(g => g.Count()).Select(g => new { Kind = WorkFieldText.LabelOf(Kinds, g.Key), Count = g.Count() }),
            ByAgent = agent is null
                ? rows.Where(r => r.AgentId is not null).GroupBy(r => r.AgentId!.Value).OrderByDescending(g => g.Count()).Take(15)
                    .Select(g => new { Agent = snapshot.NameOf(g.Key), Team = FieldToolScope.TeamName(snapshot, g.Key), Count = g.Count(), Markets = g.Where(r => r.MarketId is not null).Select(r => r.MarketId).Distinct().Count() })
                : null,
            Items = ordered.Select(r => new
            {
                Kind = WorkFieldText.LabelOf(Kinds, r.Kind),
                r.Title,
                Reason = WorkFieldText.Short(r.Reason, 400),
                Status = WorkFieldText.Label(r.Status),
                Agent = r.AgentName,
                Supervisor = r.SupervisorName,
                Market = r.MarketName,
                Priority = WorkFieldText.Label(r.Priority),
                Due = WorkFieldText.Day(r.DueDate),
                ConfidencePct = Math.Round(r.Confidence * 100),
                Created = WorkFieldText.LocalDay(r.CreatedAt),
                r.DecidedBy,
                Decided = WorkFieldText.LocalDay(r.DecidedAt),
                DecisionNote = WorkFieldText.Short(r.DecisionNote, 200),
                TaskCreated = r.TaskId is not null ? true : (bool?)null,
            }),
            Note = "Рекомендация план сразу не меняет: супервайзер или РМ подтверждает её (агенту ставится задача, точка — в маршрут) или отклоняет. " +
                   "Уверенность — оценка правила 0–100%. Ждущие решения — первыми, решённые — от свежих." +
                   (cut ? $" Рекомендаций больше {MaxRows} — разбивки посчитаны по первым {MaxRows}." : string.Empty),
        }, new DataSource("Sales Base → AI-планирование", $"на {WorkFieldText.Ru(today)}", WorkFieldText.Link("/field/ai",
            ("status", status == StatusFilter.Pending ? null : serviceStatus),
            ("kind", kind?.ToString()))));
    }

    /// <summary>Статус в запросе сервиса (и на странице /field/ai): pending, decided, all или имя статуса.</summary>
    private static string ServiceStatus(StatusFilter status) => status switch
    {
        StatusFilter.Pending => "pending",
        StatusFilter.Decided => "decided",
        StatusFilter.All => "all",
        StatusFilter.Approved => nameof(FieldRecommendationStatus.Approved),
        StatusFilter.Rejected => nameof(FieldRecommendationStatus.Rejected),
        _ => nameof(FieldRecommendationStatus.Expired),
    };
}
