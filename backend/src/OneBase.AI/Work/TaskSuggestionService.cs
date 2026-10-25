using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using OneBase.AI.Consultant;
using OneBase.AI.Gateway;
using OneBase.AI.Grounding;
using OneBase.AI.Llm;
using OneBase.AI.Providers;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Work;
using OneBase.Domain.AI;
using OneBase.Domain.Audit;
using OneBase.Domain.Work;

namespace OneBase.AI.Work;

public sealed record TaskSuggestionView(
    Guid Id,
    WorkSuggestionStatus Status,
    string SourceKind,
    string Title,
    string Problem,
    string Rationale,
    string Facts,
    bool InsufficientData,
    string DepartmentCode,
    string DepartmentName,
    Guid? AssigneeId,
    string? AssigneeName,
    WorkTaskPriority Priority,
    DateOnly? DueDate,
    string? SourceHref,
    string? Model,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DecidedAt,
    string? DecidedByName,
    string? DecisionNote,
    Guid? CreatedTaskId);

public sealed record TaskSuggestionRun(int Candidates, int Created, int Duplicates, bool UsedModel, string? Note);

/// <summary>Правка предложения перед подтверждением (или при подтверждении): что задано — заменяет предложенное.</summary>
public sealed record TaskSuggestionEdit(
    string? Title = null,
    string? Problem = null,
    string? Rationale = null,
    Guid? AssigneeId = null,
    bool ClearAssignee = false,
    WorkTaskPriority? Priority = null,
    DateOnly? DueDate = null,
    bool ClearDueDate = false);

/// <summary>Кандидат в предложение: факты источника — до модели.</summary>
internal sealed record SuggestionCandidate(
    string Id,
    string Fingerprint,
    string Kind,
    Guid DepartmentId,
    string DepartmentCode,
    string DepartmentName,
    string Title,
    string Problem,
    string Facts,
    string? Href,
    string? RequiredPermission,
    WorkTaskPriority Priority);

/// <summary>Предложение после модели (или по правилу) — до записи.</summary>
internal sealed record SuggestionDraft(string Title, string Problem, string Rationale, Guid? AssigneeId, WorkTaskPriority Priority, DateOnly? DueDate, bool InsufficientData);

/// <summary>
/// AI-диспетчер задач. Находит поводы для задач в данных OneBase — находки проактивного анализа (снижение продаж, риск невыполнения плана,
/// дефицит остатков, проблемные агенты; повторяющиеся — с числом дней), просроченные задачи отделов, задачи, зависшие на проверке, — и
/// предлагает руководителю задачу: название, проблему, обоснование, отдел, исполнителя, приоритет, срок, ссылку на данные.
/// <list type="bullet">
/// <item>Задачи сам не создаёт: предложение «ожидает подтверждения»; задача появляется, только когда руководитель его подтвердит (можно с правкой).</item>
/// <item>Ничего не придумывает: формулирует модель через AI Gateway (учёт токенов и бюджета), но числа обоснования сверяются с фактами
/// источника; исполнитель — только из сотрудников отдела; не прошло проверку — текст по правилу из самих фактов, без модели.</item>
/// <item>Повторный запуск дубликатов не создаёт: уникальный отпечаток источника за период (находка — её ключ, просрочки — отдел и неделя).</item>
/// <item>Не обходит права: видит и решает только руководитель отдела предложения (или руководство), находка с правом — только при этом праве.</item>
/// </list>
/// </summary>
public sealed class TaskSuggestionService(
    IAppDbContext db,
    WorkTaskService tasks,
    IAiGateway gateway,
    UserNotifier notifier,
    IAuditLogger audit,
    TimeProvider clock,
    ILogger<TaskSuggestionService> logger)
{
    /// <summary>Сколько кандидатов формулирует один запуск (остальные — в следующий).</summary>
    public const int MaxPerRun = 15;

    /// <summary>Задача на проверке дольше — повод напомнить руководителю.</summary>
    public static readonly TimeSpan StaleReview = TimeSpan.FromDays(3);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    internal const string Prompt = """
        Ты — AI-диспетчер задач компании в OneBase (Узбекистан, кондитерская компания: продажи, производство, снабжение, финансы, маркетинг, HR).
        На входе — кандидаты: поводы для задач, найденные в данных OneBase, с фактами (facts), и сотрудники отделов (assignees).
        Для каждого кандидата предложи руководителю задачу.

        Правила:
        1. Только факты из facts этого кандидата. Не добавляй чисел, процентов, имён, дат и причин, которых там нет. Не обещай результат.
        2. assigneeId — только id из assignees того же отдела. Если в фактах нет основания выбрать конкретного человека — null.
        3. priority — Low, Medium, High или Urgent; ориентир — defaultPriority.
        4. dueInDays — целое от 1 до 30: срок на выполнение от сегодняшнего дня по приоритету (Urgent 1–3, High 3–7, Medium 7–14, Low 14–30).
           Если срок нельзя обосновать — null.
        5. Если фактов недостаточно, чтобы ставить задачу, — insufficientData: true, а в problem прямо скажи, каких данных не хватает.
        6. title — конкретное действие, до 120 символов; problem — 1–2 предложения; rationale — 1–3 предложения: какие факты из данных
           показывают проблему (с числами из facts).
        Ответ — только JSON, без текста вокруг:
        {"suggestions":[{"id":"c1","title":"…","problem":"…","rationale":"…","assigneeId":null,"priority":"High","dueInDays":5,"insufficientData":false}]}
        """;

    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(5)).DateTime);

    // ---------- Просмотр ----------

    /// <summary>Предложения, которые пользователь может решать: его отделы (руководство — все), находки с правом — только при этом праве.</summary>
    public async Task<List<TaskSuggestionView>> ListAsync(WorkContext ctx, WorkSuggestionStatus? status, CancellationToken ct)
    {
        var q = Scoped(ctx);
        if (status is { } st)
        {
            q = q.Where(s => s.Status == st);
        }

        var rows = await q.OrderBy(s => s.Status == WorkSuggestionStatus.Pending ? 0 : 1).ThenByDescending(s => s.CreatedAt).Take(200)
            .Select(s => new
            {
                S = s, Code = s.Department!.Code, Department = s.Department.Name, Assignee = s.Assignee == null ? null : s.Assignee.FullName,
            })
            .ToListAsync(ct);
        var deciders = rows.Select(r => r.S.DecidedById).OfType<Guid>().Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => deciders.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        return rows.Select(r => new TaskSuggestionView(r.S.Id, r.S.Status, r.S.SourceKind, r.S.Title, r.S.Problem, r.S.Rationale, r.S.Facts, r.S.InsufficientData,
            r.Code, r.Department, r.S.AssigneeId, r.Assignee, r.S.Priority, r.S.DueDate, r.S.SourceHref, r.S.Model, r.S.CreatedAt, r.S.DecidedAt,
            r.S.DecidedById is { } d ? names.GetValueOrDefault(d) : null, r.S.DecisionNote, r.S.CreatedTaskId)).ToList();
    }

    public async Task<int> PendingCountAsync(WorkContext ctx, CancellationToken ct) =>
        ctx.HeadsAny ? await Scoped(ctx).CountAsync(s => s.Status == WorkSuggestionStatus.Pending, ct) : 0;

    private IQueryable<WorkTaskSuggestion> Scoped(WorkContext ctx)
    {
        var permissions = ctx.Permissions.ToArray();
        var q = db.WorkTaskSuggestions.AsNoTracking().Where(s => s.RequiredPermission == null || permissions.Contains(s.RequiredPermission));
        if (ctx.SeesAll)
        {
            return q;
        }

        var heads = (ctx.HeadOf ?? new HashSet<Guid>()).ToArray();
        return q.Where(s => heads.Contains(s.DepartmentId));
    }

    // ---------- Запуск ----------

    /// <summary>
    /// Найти поводы и предложить задачи по отделам, которыми руководит пользователь. Повторный запуск предлагает только новое: отпечаток
    /// источника уникален. Модель недоступна (не настроена, бюджет, сбой) — предложения по правилу из фактов, без модели.
    /// </summary>
    public async Task<TaskSuggestionRun> GenerateAsync(WorkContext ctx, CancellationToken ct)
    {
        if (!ctx.HeadsAny)
        {
            throw new FieldForbiddenException("Предложения AI-диспетчера получают руководители отделов и руководство.");
        }

        var all = await db.Departments.AsNoTracking().ToListAsync(ct);
        var managed = all.Where(d => ctx.Heads(d.Id)).ToList();
        var candidates = await CandidatesAsync(ctx, managed, ct);
        var fingerprints = candidates.Select(c => c.Fingerprint).ToArray();
        var known = (await db.WorkTaskSuggestions.AsNoTracking().Where(s => fingerprints.Contains(s.Fingerprint)).Select(s => s.Fingerprint).ToListAsync(ct)).ToHashSet();
        var fresh = candidates.Where(c => !known.Contains(c.Fingerprint))
            .OrderByDescending(c => PriorityRank(c.Priority))
            .Take(MaxPerRun)
            .Select((c, i) => c with { Id = $"c{i + 1}" })
            .ToList();
        if (fresh.Count == 0)
        {
            return new TaskSuggestionRun(candidates.Count, 0, candidates.Count, false,
                candidates.Count == 0 ? "В данных OneBase нет новых поводов для задач по вашим отделам." : "Все поводы уже предложены — новых нет.");
        }

        var assignees = await AssigneesAsync(fresh.Select(c => c.DepartmentId).Distinct().ToList(), ct);
        var (drafts, model, note) = await FormulateAsync(ctx, fresh, assignees, ct);
        var created = 0;
        var duplicates = known.Count;
        var newByDepartment = new Dictionary<Guid, int>();
        foreach (var c in fresh)
        {
            var d = drafts.GetValueOrDefault(c.Id) ?? Template(c);
            var suggestion = new WorkTaskSuggestion
            {
                Fingerprint = c.Fingerprint,
                SourceKind = c.Kind,
                Title = Cut(d.Title, 300),
                Problem = Cut(d.Problem, 2000),
                Rationale = Cut(d.Rationale, 4000),
                Facts = Cut(c.Facts, 4000),
                InsufficientData = d.InsufficientData,
                DepartmentId = c.DepartmentId,
                AssigneeId = d.AssigneeId,
                Priority = d.Priority,
                DueDate = d.DueDate,
                SourceHref = c.Href,
                RequiredPermission = c.RequiredPermission,
                Model = drafts.ContainsKey(c.Id) ? model : null,
                GeneratedById = ctx.UserId,
            };
            db.WorkTaskSuggestions.Add(suggestion);
            try
            {
                await db.SaveChangesAsync(ct);
                created++;
                newByDepartment[c.DepartmentId] = newByDepartment.GetValueOrDefault(c.DepartmentId) + 1;
            }
            catch (DbUpdateException)
            {
                // Тот же повод одновременно предложил другой запуск (уникальный отпечаток) — второе предложение не нужно.
                foreach (var entry in db.ChangeTracker.Entries<WorkTaskSuggestion>().Where(e => e.Entity == suggestion).ToList())
                {
                    entry.State = EntityState.Detached;
                }

                if (!await db.WorkTaskSuggestions.AsNoTracking().AnyAsync(x => x.Fingerprint == c.Fingerprint, ct))
                {
                    throw;
                }

                duplicates++;
            }
        }

        await NotifyHeadsAsync(ctx, newByDepartment, all, ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "work.suggestions.generated", "work_suggestion", null,
            new { candidates = candidates.Count, created, duplicates, model }, ct);
        return new TaskSuggestionRun(candidates.Count, created, duplicates, model is not null, note);
    }

    /// <summary>Поводы из данных: находки проактивного анализа, просрочки и зависшие проверки по отделам пользователя.</summary>
    internal async Task<List<SuggestionCandidate>> CandidatesAsync(WorkContext ctx, IReadOnlyList<Domain.Identity.Department> managed, CancellationToken ct)
    {
        var result = new List<SuggestionCandidate>();
        var byCode = managed.ToDictionary(d => d.Code);
        var ids = managed.Select(d => d.Id).ToArray();
        var codes = byCode.Keys.ToArray();
        var today = Today;
        var week = $"{ISOWeek.GetYear(today.ToDateTime(TimeOnly.MinValue))}-W{ISOWeek.GetWeekOfYear(today.ToDateTime(TimeOnly.MinValue)):00}";

        // 1. Находки проактивного анализа (правила по данным: продажи, план, остатки, первичка) — только при праве на их данные.
        var alerts = await db.AiAlerts.AsNoTracking().Where(a => a.ResolvedAt == null && codes.Contains(a.Category)).ToListAsync(ct);
        foreach (var a in alerts.Where(a => a.RequiredPermission is null || ctx.Permissions.Contains(a.RequiredPermission)))
        {
            var department = byCode[a.Category];
            var days = (int)(a.LastSeenAt - a.DetectedAt).TotalDays;
            var facts = $"{a.Title}. {a.Message}"
                        + (a.Recommendation is { Length: > 0 } r ? $" Рекомендация анализа: {r}" : "")
                        + (days >= 7 ? $" Находка повторяется: держится {days} дн., впервые — {a.DetectedAt.ToOffset(TimeSpan.FromHours(5)):dd.MM.yyyy}." : "");
            var priority = a.Severity switch
            {
                AiAlertSeverity.Critical => days >= 7 ? WorkTaskPriority.Urgent : WorkTaskPriority.High,
                AiAlertSeverity.Warning => days >= 7 ? WorkTaskPriority.High : WorkTaskPriority.Medium,
                _ => WorkTaskPriority.Low,
            };
            result.Add(new SuggestionCandidate("", $"alert:{a.Key}", "alert", department.Id, department.Code, department.Name, a.Title, a.Message, facts, a.Href,
                a.RequiredPermission, priority));
        }

        // 2. Просроченные задачи — по отделу за неделю: руководителю — разобрать их.
        var overdue = await db.WorkTasks.AsNoTracking()
            .Where(t => ids.Contains(t.DepartmentId) && t.ArchivedAt == null
                        && (t.Status == WorkTaskStatus.New || t.Status == WorkTaskStatus.InProgress || t.Status == WorkTaskStatus.Review)
                        && t.DueDate != null && t.DueDate < today)
            .Select(t => new { t.DepartmentId, t.Title, t.DueDate, Assignee = t.Assignee == null ? null : t.Assignee.FullName })
            .ToListAsync(ct);
        foreach (var g in overdue.GroupBy(t => t.DepartmentId))
        {
            var department = managed.First(d => d.Id == g.Key);
            var list = g.OrderBy(t => t.DueDate).ToList();
            var maxDays = list.Max(t => today.DayNumber - t.DueDate!.Value.DayNumber);
            var lines = list.Take(10).Select(t =>
                $"«{t.Title}» — исполнитель {t.Assignee ?? "не назначен"}, срок {t.DueDate:dd.MM.yyyy}, просрочка {today.DayNumber - t.DueDate!.Value.DayNumber} дн.");
            var facts = $"Просроченных открытых задач в отделе «{department.Name}»: {list.Count}. " + string.Join("; ", lines) + (list.Count > 10 ? "; …" : ".");
            result.Add(new SuggestionCandidate("", $"overdue:{department.Code}:{week}", "overdue", department.Id, department.Code, department.Name,
                $"Разобрать просроченные задачи отдела «{department.Name}»", $"В отделе {list.Count} просроченных задач.", facts,
                $"/tasks/all?department={department.Code}&status=overdue", null, list.Count >= 5 || maxDays >= 14 ? WorkTaskPriority.High : WorkTaskPriority.Medium));
        }

        // 3. Задачи на проверке дольше трёх дней — результат ждёт приёмки.
        var staleBefore = clock.GetUtcNow() - StaleReview;
        var stale = await db.WorkTasks.AsNoTracking()
            .Where(t => ids.Contains(t.DepartmentId) && t.ArchivedAt == null && t.Status == WorkTaskStatus.Review && t.ReviewRequestedAt != null
                        && t.ReviewRequestedAt < staleBefore)
            .Select(t => new { t.DepartmentId, t.Title, t.ReviewRequestedAt, Author = t.CreatedBy == null ? null : t.CreatedBy.FullName })
            .ToListAsync(ct);
        foreach (var g in stale.GroupBy(t => t.DepartmentId))
        {
            var department = managed.First(d => d.Id == g.Key);
            var list = g.OrderBy(t => t.ReviewRequestedAt).ToList();
            var lines = list.Take(10).Select(t =>
                $"«{t.Title}» — на проверке с {t.ReviewRequestedAt!.Value.ToOffset(TimeSpan.FromHours(5)):dd.MM.yyyy}, постановщик {t.Author ?? "не указан"}");
            var facts = $"Задач, ждущих приёмки больше {StaleReview.Days} дн., в отделе «{department.Name}»: {list.Count}. " + string.Join("; ", lines) + ".";
            result.Add(new SuggestionCandidate("", $"review:{department.Code}:{week}", "review", department.Id, department.Code, department.Name,
                $"Принять или вернуть задачи на проверке в отделе «{department.Name}»", $"{list.Count} задач ждут проверки больше {StaleReview.Days} дн.", facts,
                $"/tasks/all?department={department.Code}&status=Review", null, WorkTaskPriority.Medium));
        }

        return result;
    }

    private async Task<Dictionary<Guid, List<(Guid Id, string Name, string? Position, int Open)>>> AssigneesAsync(IReadOnlyList<Guid> departments, CancellationToken ct)
    {
        var users = await db.Users.AsNoTracking().Where(u => u.IsActive && u.DepartmentId != null && departments.Contains(u.DepartmentId.Value))
            .Select(u => new { u.Id, u.FullName, u.Position, Department = u.DepartmentId!.Value }).ToListAsync(ct);
        var ids = users.Select(u => u.Id).ToArray();
        var open = await db.WorkTasks.AsNoTracking()
            .Where(t => t.AssigneeId != null && ids.Contains(t.AssigneeId.Value) && t.ArchivedAt == null
                        && (t.Status == WorkTaskStatus.New || t.Status == WorkTaskStatus.InProgress || t.Status == WorkTaskStatus.Review))
            .GroupBy(t => t.AssigneeId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return users.GroupBy(u => u.Department).ToDictionary(g => g.Key,
            g => g.OrderBy(u => u.FullName).Take(40).Select(u => (u.Id, u.FullName, u.Position, open.GetValueOrDefault(u.Id))).ToList());
    }

    /// <summary>
    /// Формулировки модели — одним запросом на запуск. Ответ проверяется: исполнитель — из списка отдела, приоритет — из допустимых, срок —
    /// 1–30 дней; числа в названии, проблеме и обосновании — только из фактов кандидата. Не прошло — у кандидата формулировка по правилу.
    /// </summary>
    internal async Task<(Dictionary<string, SuggestionDraft> Drafts, string? Model, string? Note)> FormulateAsync(WorkContext ctx,
        IReadOnlyList<SuggestionCandidate> candidates, Dictionary<Guid, List<(Guid Id, string Name, string? Position, int Open)>> assignees, CancellationToken ct)
    {
        var request = JsonSerializer.Serialize(new
        {
            today = Today.ToString("yyyy-MM-dd"),
            candidates = candidates.Select(c => new { id = c.Id, kind = c.Kind, department = c.DepartmentCode, defaultPriority = c.Priority.ToString(), facts = c.Facts }),
            assignees = candidates.Select(c => c.DepartmentId).Distinct().ToDictionary(
                id => candidates.First(c => c.DepartmentId == id).DepartmentCode,
                id => assignees.GetValueOrDefault(id)?.Select(a => new { id = a.Id, name = a.Name, position = a.Position, openTasks = a.Open }).Cast<object>().ToList() ?? []),
        }, Json);

        AiCallResult result;
        try
        {
            result = await gateway.CompleteAsync(
                [new LlmMessage(LlmRole.System, Prompt), new LlmMessage(LlmRole.User, request)],
                [],
                new AiCallOptions
                {
                    JsonOutput = true, MaxOutputTokens = 8000, ReasoningEffort = AiReasoning.Low, CacheConversation = false,
                    Context = new AiCallContext(ctx.UserId, null, null, "work.suggestions"),
                },
                ct);
        }
        catch (Exception ex) when (ex is LlmNotConfiguredException or AiProviderException)
        {
            logger.LogInformation("AI-диспетчер: модель недоступна ({Error}) — предложения по правилу", ex.Message);
            return ([], null, $"ИИ недоступен ({ex.Message}) — предложения составлены по правилу из данных, без модели.");
        }

        var json = JsonText.ExtractObject(result.Text);
        if (json is null || !json.Value.TryGetProperty("suggestions", out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return ([], null, "Модель ответила не по формату — предложения составлены по правилу из данных.");
        }

        var drafts = new Dictionary<string, SuggestionDraft>();
        var rejected = 0;
        foreach (var item in list.EnumerateArray().Where(i => i.ValueKind == JsonValueKind.Object))
        {
            var id = JsonText.Str(item, "id");
            var c = candidates.FirstOrDefault(x => x.Id == id);
            if (c is null || drafts.ContainsKey(c.Id))
            {
                continue;
            }

            var draft = Validate(c, item, assignees.GetValueOrDefault(c.DepartmentId) ?? []);
            if (draft is null)
            {
                rejected++;
                continue;
            }

            drafts[c.Id] = draft;
        }

        return (drafts, result.Model.ToString(), rejected > 0 ? $"Формулировки {rejected} предложений не прошли проверку по данным — для них текст по правилу." : null);
    }

    /// <summary>Проверка формулировки модели; null — не годится (тогда — по правилу).</summary>
    internal SuggestionDraft? Validate(SuggestionCandidate c, JsonElement item, IReadOnlyList<(Guid Id, string Name, string? Position, int Open)> assignees)
    {
        var title = JsonText.Str(item, "title")?.Trim();
        var problem = JsonText.Str(item, "problem")?.Trim();
        var rationale = JsonText.Str(item, "rationale")?.Trim();
        if (title is not { Length: >= 5 and <= 200 } || problem is not { Length: > 0 } || rationale is not { Length: > 0 })
        {
            return null;
        }

        // Числа формулировки — только из фактов источника (EvidenceCheck: с учётом округления).
        var evidence = new EvidenceSet();
        evidence.AddText(c.Facts);
        evidence.AddText(c.Title);
        evidence.AddText(c.Problem);
        if (!EvidenceCheck.Run($"{title}\n{problem}\n{rationale}", evidence).Grounded)
        {
            return null;
        }

        Guid? assignee = Guid.TryParse(JsonText.Str(item, "assigneeId"), out var a) && assignees.Any(x => x.Id == a) ? a : null;
        var priority = Enum.TryParse<WorkTaskPriority>(JsonText.Str(item, "priority"), true, out var p) && Enum.IsDefined(p) ? p : c.Priority;
        DateOnly? due = item.TryGetProperty("dueInDays", out var d) && d.ValueKind == JsonValueKind.Number && d.TryGetInt32(out var days) && days is >= 1 and <= 30
            ? Today.AddDays(days)
            : null;
        var insufficient = item.TryGetProperty("insufficientData", out var i) && i.ValueKind == JsonValueKind.True;
        return new SuggestionDraft(title, problem, rationale, assignee, priority, due, insufficient);
    }

    /// <summary>Формулировка без модели — из самих фактов: срок и исполнитель не предлагаются (их задаст руководитель).</summary>
    internal static SuggestionDraft Template(SuggestionCandidate c) => c.Kind switch
    {
        "alert" => new SuggestionDraft($"Разобрать: {c.Title}", c.Problem, c.Facts, null, c.Priority, null, false),
        _ => new SuggestionDraft(c.Title, c.Problem, c.Facts, null, c.Priority, null, false),
    };

    private async Task NotifyHeadsAsync(WorkContext ctx, Dictionary<Guid, int> created, IReadOnlyList<Domain.Identity.Department> departments, CancellationToken ct)
    {
        if (created.Count == 0)
        {
            return;
        }

        var ids = created.Keys.ToArray();
        var heads = await db.WorkDepartmentHeads.AsNoTracking().Where(h => ids.Contains(h.DepartmentId) && h.User!.IsActive).ToListAsync(ct);
        foreach (var g in heads.Where(h => h.UserId != ctx.UserId).GroupBy(h => h.UserId))
        {
            var count = g.Sum(h => created.GetValueOrDefault(h.DepartmentId));
            var names = string.Join(", ", g.Select(h => departments.First(d => d.Id == h.DepartmentId).Name));
            notifier.Notify(g.Key, UserNotificationKind.TaskSuggestion, $"AI-диспетчер предложил задач: {count}", $"Отделы: {names}. Ждут вашего подтверждения.",
                "/tasks/suggestions");
        }

        await db.SaveChangesAsync(ct);
    }

    // ---------- Решения ----------

    private async Task<WorkTaskSuggestion> RequirePendingAsync(WorkContext ctx, Guid id, CancellationToken ct)
    {
        var s = await db.WorkTaskSuggestions.Include(x => x.Department).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s?.Department is null || !ctx.Heads(s.DepartmentId) || (s.RequiredPermission is { } p && !ctx.Permissions.Contains(p)))
        {
            throw new FieldNotFoundException("Предложение не найдено.");
        }

        if (s.Status != WorkSuggestionStatus.Pending)
        {
            throw new FieldValidationException(s.Status == WorkSuggestionStatus.Accepted ? "Предложение уже подтверждено — задача создана." : "Предложение уже отклонено.");
        }

        return s;
    }

    /// <summary>Правка предложения, пока оно ожидает решения.</summary>
    public async Task EditAsync(WorkContext ctx, Guid id, TaskSuggestionEdit edit, CancellationToken ct)
    {
        var s = await RequirePendingAsync(ctx, id, ct);
        await ApplyAsync(s, edit, ct);
        s.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Подтвердить: создаётся настоящая задача от имени руководителя (с его правами — WorkTaskService проверяет отдел, исполнителя и срок).
    /// Решение и задача записываются вместе: второй руководитель, подтвердивший одновременно, получит «уже подтверждено».
    /// </summary>
    public async Task<Guid> AcceptAsync(WorkContext ctx, Guid id, TaskSuggestionEdit? edit, CancellationToken ct)
    {
        var s = await RequirePendingAsync(ctx, id, ct);
        if (edit is not null)
        {
            await ApplyAsync(s, edit, ct);
            await db.SaveChangesAsync(ct);
        }

        // Сначала решение (условно — только из «ожидает»), потом задача: так одна задача на одно предложение.
        var claimed = await db.WorkTaskSuggestions.Where(x => x.Id == id && x.Status == WorkSuggestionStatus.Pending)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.Status, WorkSuggestionStatus.Accepted).SetProperty(x => x.DecidedById, ctx.UserId)
                .SetProperty(x => x.DecidedAt, DateTimeOffset.UtcNow), ct);
        if (claimed != 1)
        {
            throw new FieldValidationException("Предложение уже решено другим руководителем.");
        }

        Guid taskId;
        try
        {
            var description = $"{s.Problem}\n\nОбоснование (данные OneBase): {s.Rationale}" + (s.SourceHref is { } href ? $"\nИсточник: {href}" : "");
            taskId = await tasks.CreateAsync(ctx, s.Department!.Code,
                new WorkTaskInput(s.Title, description.Length <= 4000 ? description : description[..4000], s.AssigneeId, s.Priority, s.DueDate), ct, s.Id);
        }
        catch
        {
            // Задача не создалась (например, срок уже в прошлом) — предложение снова ждёт решения.
            await db.WorkTaskSuggestions.Where(x => x.Id == id)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.Status, WorkSuggestionStatus.Pending).SetProperty(x => x.DecidedById, (Guid?)null)
                    .SetProperty(x => x.DecidedAt, (DateTimeOffset?)null), CancellationToken.None);
            throw;
        }

        await db.WorkTaskSuggestions.Where(x => x.Id == id).ExecuteUpdateAsync(u => u.SetProperty(x => x.CreatedTaskId, taskId), ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "work.suggestion.accepted", "work_suggestion", id.ToString(), new { taskId }, ct);
        return taskId;
    }

    public async Task RejectAsync(WorkContext ctx, Guid id, string? note, CancellationToken ct)
    {
        var s = await RequirePendingAsync(ctx, id, ct);
        var text = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (text is { Length: > 1000 })
        {
            throw new FieldValidationException("Причина — до 1000 символов.");
        }

        s.Status = WorkSuggestionStatus.Rejected;
        s.DecidedById = ctx.UserId;
        s.DecidedAt = DateTimeOffset.UtcNow;
        s.DecisionNote = text;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "work.suggestion.rejected", "work_suggestion", id.ToString(), new { note = text }, ct);
    }

    private async Task ApplyAsync(WorkTaskSuggestion s, TaskSuggestionEdit edit, CancellationToken ct)
    {
        if (edit.Title is { } title)
        {
            var t = title.Trim();
            s.Title = t.Length is >= 1 and <= 300 ? t : throw new FieldValidationException("Название задачи — от 1 до 300 символов.");
        }

        if (edit.Problem is { } problem)
        {
            var p = problem.Trim();
            s.Problem = p.Length is >= 1 and <= 2000 ? p : throw new FieldValidationException("Описание проблемы — от 1 до 2000 символов.");
        }

        if (edit.Rationale is { } rationale)
        {
            var r = rationale.Trim();
            s.Rationale = r.Length is >= 1 and <= 4000 ? r : throw new FieldValidationException("Обоснование — от 1 до 4000 символов.");
        }

        if (edit.ClearAssignee)
        {
            s.AssigneeId = null;
        }
        else if (edit.AssigneeId is { } a)
        {
            if (!await db.Users.AnyAsync(u => u.Id == a && u.IsActive && u.DepartmentId == s.DepartmentId, ct))
            {
                throw new FieldValidationException("Исполнитель — действующий сотрудник отдела предложения.");
            }

            s.AssigneeId = a;
        }

        if (edit.Priority is { } priority)
        {
            s.Priority = Enum.IsDefined(priority) ? priority : throw new FieldValidationException("Приоритет — низкий, обычный, высокий или критический.");
        }

        if (edit.ClearDueDate)
        {
            s.DueDate = null;
        }
        else if (edit.DueDate is { } due)
        {
            s.DueDate = due >= Today ? due : throw new FieldValidationException("Срок не может быть в прошлом.");
        }
    }

    private static int PriorityRank(WorkTaskPriority p) => p switch
    {
        WorkTaskPriority.Urgent => 3,
        WorkTaskPriority.High => 2,
        WorkTaskPriority.Medium => 1,
        _ => 0,
    };

    private static string Cut(string text, int max) => text.Length <= max ? text : text[..max];
}
