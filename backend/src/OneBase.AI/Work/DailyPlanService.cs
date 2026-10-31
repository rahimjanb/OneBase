using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneBase.AI.Consultant;
using OneBase.AI.Gateway;
using OneBase.AI.Llm;
using OneBase.AI.Security;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Hr;
using OneBase.Application.Security;
using OneBase.Application.Work;
using OneBase.Domain.AI;
using OneBase.Domain.Work;

namespace OneBase.AI.Work;

/// <summary>Рекомендация плана. Tone: danger — просрочено, warning — сегодня, info — остальное, ok — всё в порядке. Href строит код, не модель.</summary>
public sealed record DailyPlanItem(string Text, string Tone, string? Href);

/// <summary>
/// План на экране. Status: ready; stale — данные изменились, план прежний (обновится по «Обновить» или при следующем открытии); generating —
/// готовится (Items — прежний план, если был); unavailable — AI недоступен. RefreshAt — когда можно нажать «Обновить».
/// </summary>
public sealed record DailyPlanView(string Status, DateTimeOffset? GeneratedAt, IReadOnlyList<DailyPlanItem> Items, DateTimeOffset? RefreshAt, string? Message);

/// <summary>«Обновить» раньше, чем через 15 минут после прошлого плана, или после дневного потолка генераций.</summary>
public sealed class DailyPlanRateLimitedException(string message, DateTimeOffset? retryAt) : Exception(message)
{
    public DateTimeOffset? RetryAt { get; } = retryAt;
}

/// <summary>Очередь фоновой генерации планов (одна на процесс).</summary>
public sealed class DailyPlanQueue
{
    private readonly Channel<(Guid UserId, DateOnly Date)> _channel = Channel.CreateBounded<(Guid, DateOnly)>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest });

    public void Enqueue(Guid userId, DateOnly date) => _channel.Writer.TryWrite((userId, date));

    public IAsyncEnumerable<(Guid UserId, DateOnly Date)> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

/// <summary>Составляет планы из очереди по одному — запрос страницы генерацию не ждёт.</summary>
public sealed class DailyPlanWorker(DailyPlanQueue queue, IServiceScopeFactory scopes, ILogger<DailyPlanWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var (userId, date) in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<DailyPlanService>().GenerateAsync(userId, date, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "План на день: не составлен");
            }
        }
    }
}

/// <summary>
/// «План на день от AI» в «Моём дне». Модель вызывается не при каждом открытии: план хранится в ai.DailyPlans (пользователь + день Ташкента).
/// Генерация — при первом открытии за день, по «Обновить» (не чаще раза в 15 минут) и при следующем открытии, если задачи или события
/// изменились (не раньше чем через 30 минут после прошлого плана); не больше 4 генераций на человека в день. Вход модели — только данные
/// этого сотрудника: его открытые задачи, события календаря на неделю и рабочий день. Нет задач и событий — «Всё в порядке», без модели.
/// </summary>
public sealed partial class DailyPlanService(
    IAppDbContext db,
    WorkTaskService tasks,
    CalendarService calendar,
    HrService hr,
    IAiGateway gateway,
    IUserPermissions permissions,
    DailyPlanQueue queue,
    ILogger<DailyPlanService> logger)
{
    public const int MaxGenerationsPerDay = 4;
    public const int MaxItems = 3;
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan StaleInterval = TimeSpan.FromMinutes(30);

    /// <summary>Генерация «зависла» (процесс перезапустился, очередь потеряна) — после этого её можно начать снова.</summary>
    public static readonly TimeSpan StuckAfter = TimeSpan.FromMinutes(3);

    public const string AllGood = "Всё в порядке — задач пока нет.";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<DailyPlanView> GetAsync(WorkContext ctx, CancellationToken ct)
    {
        if (!ctx.Permissions.Contains(Permissions.AgentsRun))
        {
            return Unavailable("AI-помощник недоступен для вашей роли.");
        }

        var input = await InputAsync(ctx, ct);
        if (input.IsEmpty)
        {
            return new DailyPlanView("ready", DateTimeOffset.UtcNow, [new DailyPlanItem(AllGood, "ok", null)], null, null);
        }

        if (!(await gateway.GetReadinessAsync(ct)).Ready)
        {
            return Unavailable("Рекомендации временно недоступны.");
        }

        var today = FieldClock.Today;
        var now = DateTimeOffset.UtcNow;
        var plan = await db.AiDailyPlans.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == ctx.UserId && p.Date == today, ct);
        if (plan is null)
        {
            db.AiDailyPlans.Add(new AiDailyPlan { UserId = ctx.UserId, Date = today, RequestedAt = now });
            try
            {
                await db.SaveChangesAsync(ct);
                queue.Enqueue(ctx.UserId, today);
            }
            catch (DbUpdateException)
            {
                // Второй запрос того же сотрудника успел создать план — генерация уже идёт.
            }

            return new DailyPlanView("generating", null, [], null, null);
        }

        var items = Items(plan);
        var refreshAt = RefreshAt(plan);
        if (plan.Status == AiDailyPlanStatus.Generating)
        {
            if (now - plan.RequestedAt > StuckAfter && await ClaimAsync(plan.Id, AiDailyPlanStatus.Generating, plan.RequestedAt, ct))
            {
                queue.Enqueue(ctx.UserId, today);
            }

            return new DailyPlanView("generating", plan.GeneratedAt, items, refreshAt, null);
        }

        var changed = plan.InputHash != input.Hash;
        // Не раньше чем через 30 минут после прошлой попытки (RequestedAt): и устаревший план, и повтор после сбоя модели — не на каждое открытие.
        if (changed && plan.Generations < MaxGenerationsPerDay && now - plan.RequestedAt >= StaleInterval
            && await ClaimAsync(plan.Id, plan.Status, plan.RequestedAt, ct))
        {
            queue.Enqueue(ctx.UserId, today);
            return new DailyPlanView("generating", plan.GeneratedAt, items, refreshAt, null);
        }

        if (plan.Status == AiDailyPlanStatus.Failed)
        {
            return items.Count > 0
                ? new DailyPlanView("stale", plan.GeneratedAt, items, refreshAt, "Не удалось обновить план — показан прежний.")
                : new DailyPlanView("unavailable", null, [], refreshAt, "Рекомендации временно недоступны.");
        }

        return new DailyPlanView(changed ? "stale" : "ready", plan.GeneratedAt, items, refreshAt, null);
    }

    /// <summary>«Обновить»: не чаще раза в 15 минут и не больше 4 планов в день.</summary>
    public async Task<DailyPlanView> RefreshAsync(WorkContext ctx, CancellationToken ct)
    {
        if (!ctx.Permissions.Contains(Permissions.AgentsRun))
        {
            return Unavailable("AI-помощник недоступен для вашей роли.");
        }

        var today = FieldClock.Today;
        var plan = await db.AiDailyPlans.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == ctx.UserId && p.Date == today, ct);
        if (plan is null)
        {
            return await GetAsync(ctx, ct);
        }

        if (plan.Status == AiDailyPlanStatus.Generating)
        {
            return new DailyPlanView("generating", plan.GeneratedAt, Items(plan), RefreshAt(plan), null);
        }

        if (plan.Generations >= MaxGenerationsPerDay)
        {
            throw new DailyPlanRateLimitedException("План на сегодня уже обновлялся несколько раз — следующий будет завтра.", null);
        }

        if (RefreshAt(plan) is { } at && at > DateTimeOffset.UtcNow)
        {
            throw new DailyPlanRateLimitedException($"Обновить план можно не чаще раза в {RefreshInterval.TotalMinutes:0} минут.", at);
        }

        var input = await InputAsync(ctx, ct);
        if (input.IsEmpty)
        {
            return new DailyPlanView("ready", DateTimeOffset.UtcNow, [new DailyPlanItem(AllGood, "ok", null)], null, null);
        }

        if (await ClaimAsync(plan.Id, plan.Status, plan.RequestedAt, ct))
        {
            queue.Enqueue(ctx.UserId, today);
        }

        return new DailyPlanView("generating", plan.GeneratedAt, Items(plan), null, null);
    }

    /// <summary>Составить план (фоновая очередь). Ничего не делает, если план не ждёт генерации.</summary>
    public async Task GenerateAsync(Guid userId, DateOnly date, CancellationToken ct)
    {
        var plan = await db.AiDailyPlans.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId && p.Date == date && p.Status == AiDailyPlanStatus.Generating, ct);
        if (plan is null)
        {
            return;
        }

        var rights = await permissions.GetAsync(userId, ct);
        var ctx = await tasks.ContextAsync(userId, rights, ct);
        var input = await InputAsync(ctx, ct);
        if (input.IsEmpty)
        {
            await FinishAsync(plan.Id, AiDailyPlanStatus.Ready, [new DailyPlanItem(AllGood, "ok", null)], input.Hash, null, generated: false, ct);
            return;
        }

        try
        {
            var answer = await gateway.CompleteAsync(
                [new LlmMessage(LlmRole.System, Prompt), new LlmMessage(LlmRole.User, input.Json)],
                [],
                new AiCallOptions
                {
                    JsonOutput = true, MaxOutputTokens = 1500, ReasoningEffort = AiReasoning.Low, CacheConversation = false,
                    Context = new AiCallContext(userId, null, null, "me.daily_plan"),
                },
                ct);
            var items = Parse(answer.Text, input);
            await (items.Count > 0
                ? FinishAsync(plan.Id, AiDailyPlanStatus.Ready, items, input.Hash, null, generated: true, ct)
                : FinishAsync(plan.Id, AiDailyPlanStatus.Failed, null, null, "модель ответила не по формату", generated: true, ct));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogInformation("План на день: модель недоступна ({Error})", ex.Message);
            await FinishAsync(plan.Id, AiDailyPlanStatus.Failed, null, null, ex.Message, generated: false, ct);
        }
    }

    /// <summary>
    /// Итог генерации — условным обновлением строки «готовится» (без отслеживания сущности): план другого запроса не перезаписывается.
    /// Без рекомендаций (сбой) прежний план, его отпечаток и время остаются.
    /// </summary>
    private async Task FinishAsync(Guid id, AiDailyPlanStatus status, IReadOnlyList<DailyPlanItem>? items, string? hash, string? error, bool generated,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var text = error is null ? null : error.Length <= 1000 ? error : error[..1000];
        var add = generated ? 1 : 0;
        var row = db.AiDailyPlans.Where(p => p.Id == id && p.Status == AiDailyPlanStatus.Generating);
        if (items is null)
        {
            await row.ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, status).SetProperty(p => p.Error, text).SetProperty(p => p.UpdatedAt, now)
                .SetProperty(p => p.Generations, p => p.Generations + add), ct);
            return;
        }

        var json = JsonSerializer.Serialize(items, Json);
        await row.ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, status).SetProperty(p => p.ItemsJson, json).SetProperty(p => p.InputHash, hash)
            .SetProperty(p => p.GeneratedAt, now).SetProperty(p => p.Error, text).SetProperty(p => p.UpdatedAt, now)
            .SetProperty(p => p.Generations, p => p.Generations + add), ct);
    }

    /// <summary>Перевести план в «готовится» условным обновлением: из двух одновременных запросов генерацию запускает один.</summary>
    private async Task<bool> ClaimAsync(Guid id, AiDailyPlanStatus status, DateTimeOffset requestedAt, CancellationToken ct) =>
        await db.AiDailyPlans.Where(p => p.Id == id && p.Status == status && p.RequestedAt == requestedAt)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, AiDailyPlanStatus.Generating).SetProperty(p => p.RequestedAt, DateTimeOffset.UtcNow), ct) == 1;

    private static DateTimeOffset? RefreshAt(AiDailyPlan plan) =>
        plan.Generations >= MaxGenerationsPerDay ? null : plan.GeneratedAt is { } at && plan.Generations > 0 ? at + RefreshInterval : null;

    private static IReadOnlyList<DailyPlanItem> Items(AiDailyPlan plan)
    {
        if (string.IsNullOrEmpty(plan.ItemsJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<DailyPlanItem>>(plan.ItemsJson, Json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static DailyPlanView Unavailable(string message) => new("unavailable", null, [], null, message);

    // ---------- Вход модели ----------

    /// <summary>Данные плана: JSON для модели, ссылки (t1 → карточка задачи), допустимые числа и отпечаток (задачи и события).</summary>
    public sealed record PlanInput(string Json, IReadOnlyDictionary<string, string> Refs, IReadOnlySet<string> Numbers, string Hash, bool IsEmpty);

    public async Task<PlanInput> InputAsync(WorkContext ctx, CancellationToken ct)
    {
        var today = FieldClock.Today;
        var nowTime = FieldClock.TimeOfDay;
        var me = ctx.UserId;
        var open = await db.WorkTasks.AsNoTracking()
            .Where(t => t.AssigneeId == me && t.ArchivedAt == null && (t.Status == WorkTaskStatus.New || t.Status == WorkTaskStatus.InProgress || t.Status == WorkTaskStatus.Review))
            .OrderBy(t => t.DueDate == null).ThenBy(t => t.DueDate).ThenBy(t => t.DueTime).ThenByDescending(t => t.Priority)
            .Take(30)
            .Select(t => new { t.Id, t.Title, t.Status, t.Priority, t.IsBlocked, t.DueDate, t.DueTime })
            .ToListAsync(ct);
        var events = (await calendar.ItemsAsync(ctx, today, today.AddDays(7), ct)).Where(i => i.Type != "task_due").Take(20).ToList();
        var workday = await hr.MyWorkdayAsync(me, ct);

        var refs = new Dictionary<string, string>();
        var taskRows = open.Select((t, i) =>
        {
            refs[$"t{i + 1}"] = WorkTaskService.LinkOf(t.Id);
            return new
            {
                @ref = $"t{i + 1}",
                title = t.Title,
                status = WorkTaskService.StatusText(t.Status),
                priority = WorkTaskService.PriorityText(t.Priority),
                blocked = t.IsBlocked,
                due = t.DueDate is { } d ? Day(d, today) : null,
                dueTime = t.DueTime?.ToString("HH:mm", CultureInfo.InvariantCulture),
                overdueDays = t.DueDate is { } due && WorkTaskService.IsOverdue(t.Status, t.DueDate, t.DueTime, today, nowTime) ? today.DayNumber - due.DayNumber : (int?)null,
            };
        }).ToList();
        var eventRows = events.Select((e, i) =>
        {
            refs[$"e{i + 1}"] = e.Href;
            return new
            {
                @ref = $"e{i + 1}",
                title = e.Title,
                type = e.Type switch { "report_due" => "срок отчёта", "event" => "событие", _ => "встреча" },
                day = Day(e.Date, today),
                start = e.Start?.ToString("HH:mm", CultureInfo.InvariantCulture),
                end = e.End?.ToString("HH:mm", CultureInfo.InvariantCulture),
            };
        }).ToList();

        var payload = new
        {
            today = $"{today:yyyy-MM-dd}, {CultureInfo.GetCultureInfo("ru-RU").DateTimeFormat.GetDayName(today.DayOfWeek)}",
            now = nowTime.ToString("HH:mm", CultureInfo.InvariantCulture),
            workday = workday.Today is { } w
                ? new { status = w.Status, shift = w.ShiftStart is { } s && w.ShiftEnd is { } f ? $"{s:HH:mm}–{f:HH:mm}" : null, lateMinutes = w.LateMinutes }
                : null,
            counts = new
            {
                open = open.Count,
                inProgress = open.Count(t => t.Status == WorkTaskStatus.InProgress),
                review = open.Count(t => t.Status == WorkTaskStatus.Review),
                overdue = taskRows.Count(t => t.overdueDays is not null),
            },
            tasks = taskRows,
            events = eventRows,
        };
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web) { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        var numbers = NumberPattern().Matches(json).Select(m => m.Value).ToHashSet();
        // Отпечаток — задачи и события (без времени суток и рабочего дня): план пересоставляется, когда они изменились.
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            tasks = open.Select(t => new { t.Id, t.Status, t.IsBlocked, t.Priority, t.DueDate, t.DueTime }),
            events = events.Select(e => new { e.Id, e.Date, e.Start }),
        }))));
        return new PlanInput(json, refs, numbers, hash, open.Count == 0 && events.Count == 0);
    }

    private static string Day(DateOnly date, DateOnly today) => (date.DayNumber - today.DayNumber) switch
    {
        0 => "сегодня",
        1 => "завтра",
        -1 => "вчера",
        _ => date.ToString("d MMMM, dddd", CultureInfo.GetCultureInfo("ru-RU")),
    };

    // ---------- Ответ модели ----------

    /// <summary>
    /// Рекомендации из ответа: до трёх, текст до 220 символов, тон — из допустимых, ссылка — только на объект из входа. Рекомендация с числом,
    /// которого нет во входе, отбрасывается (модель не придумывает сроков и количеств).
    /// </summary>
    public static IReadOnlyList<DailyPlanItem> Parse(string? text, PlanInput input)
    {
        var json = JsonText.ExtractObject(text);
        if (json is null || !json.Value.TryGetProperty("items", out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var items = new List<DailyPlanItem>();
        foreach (var item in list.EnumerateArray().Where(i => i.ValueKind == JsonValueKind.Object))
        {
            var body = JsonText.Str(item, "text")?.Trim();
            if (string.IsNullOrEmpty(body) || body.Length > 220)
            {
                continue;
            }

            if (NumberPattern().Matches(body).Any(m => !input.Numbers.Contains(m.Value) && !DayOfMonth(body, m)))
            {
                continue;
            }

            var tone = JsonText.Str(item, "tone") is "danger" or "warning" or "info" or "ok" ? JsonText.Str(item, "tone")! : "info";
            var href = JsonText.Str(item, "ref") is { } r && input.Refs.TryGetValue(r, out var link) ? link : null;
            items.Add(new DailyPlanItem(body, tone, href));
            if (items.Count == MaxItems)
            {
                break;
            }
        }

        return items;
    }

    /// <summary>Число — день месяца перед названием месяца («2 ноября»): во входе даты словами, модель может написать их иначе.</summary>
    private static bool DayOfMonth(string text, Match number) =>
        int.TryParse(number.Value, out var n) && n is >= 1 and <= 31 && MonthPattern().IsMatch(text[(number.Index + number.Length)..]);

    [GeneratedRegex(@"\d+")]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"^\s*(январ|феврал|март|апрел|ма[яй]|июн|июл|август|сентябр|октябр|ноябр|декабр)", RegexOptions.IgnoreCase)]
    private static partial Regex MonthPattern();

    private const string Prompt = """
        Ты — помощник сотрудника в OneBase. По данным ниже составь план на день: от 1 до 3 коротких рекомендаций (до 160 символов), что сделать
        в первую очередь. Используй только эти данные: задачи сотрудника, их сроки, события календаря на неделю, рабочий день. Не придумывай
        задач, сроков, событий, людей и чисел.

        Порядок: просроченные задачи; задачи и события со сроком сегодня; задачи на проверке или заблокированные (кого ждут); ближайшие сроки.
        Каждая рекомендация ссылается на один объект из данных полем ref (ref задачи или события) — или null, если ссылаться не на что.
        tone: danger — просрочено; warning — срок или событие сегодня; info — остальное; ok — срочного нет.

        Если срочного и важного нет, верни одну рекомендацию с tone ok: «Всё в порядке — срочных задач нет.» — без советов и без придуманных дел.

        Пиши по-русски, обращайся на «вы», без вступлений. Формат ответа — только JSON:
        {"items":[{"text":"…","tone":"danger|warning|info|ok","ref":"t1"}]}
        """;
}
