using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Domain.Work;

namespace OneBase.Application.Work;

/// <summary>
/// Виды уведомлений для экрана в одном месте: Type — значок и цвет на экране (ТЗ notifications: ai_insight, task_overdue, task_assigned,
/// system…), Category — раздел (вкладки «Задачи · AI · HR», настройка «Что присылать»). Новый вид уведомления — строка здесь.
/// </summary>
public static class NotificationCatalog
{
    public const string Tasks = "tasks";
    public const string Ai = "ai";
    public const string Hr = "hr";
    public const string Sales = "sales";
    public const string System = "system";

    /// <summary>Разделы, которые можно отключить в «Что присылать».</summary>
    public static readonly IReadOnlyList<string> Categories = [Tasks, Ai, Hr, Sales, System];

    public static (string Type, string Category) Of(UserNotificationKind kind) => kind switch
    {
        UserNotificationKind.TaskOverdue => ("task_overdue", Tasks),
        UserNotificationKind.TaskDueSoon or UserNotificationKind.FieldTaskPostponed => ("task_due", Tasks),
        UserNotificationKind.TaskDone or UserNotificationKind.FieldTaskDone or UserNotificationKind.TaskAccepted => ("task_done", Tasks),
        UserNotificationKind.TaskComment => ("task_comment", Tasks),
        UserNotificationKind.TaskAssigned or UserNotificationKind.TaskChanged or UserNotificationKind.TaskReview or UserNotificationKind.TaskReturned
            or UserNotificationKind.TaskDeadlineChanged => ("task_assigned", Tasks),
        UserNotificationKind.TaskSuggestion => ("ai_insight", Ai),
        UserNotificationKind.AiBudget => ("ai_budget", Ai),
        UserNotificationKind.MonthPlanMissing => ("sales_alert", Sales),
        UserNotificationKind.DataSyncProblem => ("system", System),
        _ => ("system", System),
    };

    /// <summary>Виды уведомлений раздела.</summary>
    public static IReadOnlyList<UserNotificationKind> KindsOf(string category) =>
        Enum.GetValues<UserNotificationKind>().Where(k => Of(k).Category == category).ToList();

    /// <summary>Важность: critical — просрочка, сбой обмена; warning — срок скоро, план не загружен, бюджет AI; info — остальное.</summary>
    public static string Severity(UserNotificationKind kind, string title) => kind switch
    {
        UserNotificationKind.TaskOverdue => "critical",
        UserNotificationKind.DataSyncProblem => title.Contains("восстановлен", StringComparison.OrdinalIgnoreCase) ? "info" : "critical",
        UserNotificationKind.TaskDueSoon or UserNotificationKind.MonthPlanMissing or UserNotificationKind.AiBudget or UserNotificationKind.FieldTaskPostponed => "warning",
        _ => "info",
    };
}

/// <summary>Действие уведомления: open — перейти, extend_due — продлить срок задачи без перехода. Kind — primary или secondary.</summary>
public sealed record NotificationAction(string Id, string Label, string Kind, string Action);

/// <summary>
/// Уведомление для экрана. Href — куда ведёт (то же, что Link: Link и Read оставлены для прежних экранов — лента «Обзора»).
/// </summary>
public sealed record NotificationItem(
    Guid Id,
    string Type,
    string Category,
    UserNotificationKind Kind,
    string Title,
    string? Body,
    string? Href,
    string? Link,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt,
    bool Read,
    string Severity,
    IReadOnlyList<NotificationAction> Actions);

public sealed record NotificationCounts(int All, int Unread, int Tasks, int Ai, int Hr);

/// <summary>Страница уведомлений: NextCursor — продолжение (null — дальше нет), Counts — для вкладок и колокольчика, Unread — как раньше.</summary>
public sealed record NotificationPage(IReadOnlyList<NotificationItem> Items, string? NextCursor, NotificationCounts Counts, int Unread);

/// <summary>
/// Центр уведомлений OneBase: список с фильтром (all, unread, tasks, ai, hr) и курсором, счётчики, прочитано / не прочитано, удаление,
/// действия без перехода («Продлить срок»), разделы, отключённые пользователем (их нет ни в списке, ни в счётчиках, ни в push).
/// Пользователь видит и меняет только свои уведомления.
/// </summary>
public sealed class NotificationCenter(IAppDbContext db, WorkTaskService tasks)
{
    public const int MaxPage = 50;

    public async Task<NotificationPage> ListAsync(Guid userId, string? filter, string? cursor, int take, WorkContext? work, CancellationToken ct)
    {
        var muted = await MutedKindsAsync(userId, ct);
        var mine = db.UserNotifications.AsNoTracking().Where(n => n.RecipientId == userId && !muted.Contains(n.Kind));

        var section = filter is not null && NotificationCatalog.Categories.Contains(filter) ? NotificationCatalog.KindsOf(filter) : null;
        var q = filter == "unread" ? mine.Where(n => n.ReadAt == null)
            : section is not null ? mine.Where(n => section.Contains(n.Kind))
            : mine;

        if (Cursor(cursor) is { } after)
        {
            q = q.Where(n => n.CreatedAt < after.At || (n.CreatedAt == after.At && n.Id.CompareTo(after.Id) < 0));
        }

        var size = Math.Clamp(take, 1, MaxPage);
        var rows = await q.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).Take(size + 1).ToListAsync(ct);
        var page = rows.Take(size).ToList();
        var next = rows.Count > size ? CursorOf(page[^1]) : null;

        // «Продлить срок» — только где пользователь может менять задачу (постановщик, руководство), а задача ещё открыта.
        var taskIds = page.Where(n => n.Kind is UserNotificationKind.TaskOverdue or UserNotificationKind.TaskDueSoon)
            .Select(n => WorkTaskService.TaskOfLink(n.Link)).OfType<Guid>().Distinct().ToList();
        var manageable = work is null ? [] : await tasks.ManageableAsync(work, taskIds, ct);

        var counts = await CountsAsync(mine, ct);
        return new NotificationPage(page.Select(n => Item(n, manageable)).ToList(), next, counts, counts.Unread);
    }

    public async Task<NotificationCounts> CountsAsync(Guid userId, CancellationToken ct)
    {
        var muted = await MutedKindsAsync(userId, ct);
        return await CountsAsync(db.UserNotifications.AsNoTracking().Where(n => n.RecipientId == userId && !muted.Contains(n.Kind)), ct);
    }

    public Task<int> ReadAsync(Guid userId, Guid id, CancellationToken ct) =>
        db.UserNotifications.Where(n => n.Id == id && n.RecipientId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTimeOffset.UtcNow), ct);

    public Task<int> UnreadAsync(Guid userId, Guid id, CancellationToken ct) =>
        db.UserNotifications.Where(n => n.Id == id && n.RecipientId == userId && n.ReadAt != null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, (DateTimeOffset?)null), ct);

    /// <summary>Прочитать все: все или только раздела (вкладка «Задачи» — только задачи).</summary>
    public Task<int> ReadAllAsync(Guid userId, string? category, CancellationToken ct)
    {
        var q = db.UserNotifications.Where(n => n.RecipientId == userId && n.ReadAt == null);
        if (category is not null && NotificationCatalog.Categories.Contains(category))
        {
            var kinds = NotificationCatalog.KindsOf(category);
            q = q.Where(n => kinds.Contains(n.Kind));
        }

        return q.ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTimeOffset.UtcNow), ct);
    }

    /// <summary>Удалить своё уведомление (жест «Удалить» на телефоне; отмена — на экране, до запроса).</summary>
    public Task<int> DeleteAsync(Guid userId, Guid id, CancellationToken ct) =>
        db.UserNotifications.Where(n => n.Id == id && n.RecipientId == userId).ExecuteDeleteAsync(ct);

    /// <summary>
    /// Действие уведомления без перехода. extend_due — новый срок задачи из уведомления о сроке (права и проверки — как в карточке задачи);
    /// уведомление становится прочитанным. Неизвестное действие или чужое уведомление — FieldValidationException / FieldNotFoundException.
    /// </summary>
    public async Task ActAsync(Guid userId, Guid id, string action, DateOnly? due, WorkContext? work, CancellationToken ct)
    {
        var n = await db.UserNotifications.FirstOrDefaultAsync(x => x.Id == id && x.RecipientId == userId, ct)
            ?? throw new FieldNotFoundException("Уведомление не найдено.");
        switch (action)
        {
            case "extend_due":
                if (n.Kind is not (UserNotificationKind.TaskOverdue or UserNotificationKind.TaskDueSoon) || WorkTaskService.TaskOfLink(n.Link) is not { } taskId)
                {
                    throw new FieldValidationException("У этого уведомления нет задачи со сроком.");
                }

                if (work is null)
                {
                    throw new FieldForbiddenException("Нет доступа к задачам.");
                }

                if (due is null)
                {
                    throw new FieldValidationException("Укажите новый срок.");
                }

                await tasks.ExtendDueAsync(work, taskId, due.Value, ct);
                break;
            default:
                throw new FieldValidationException($"Действие «{action}» не поддерживается.");
        }

        n.ReadAt ??= DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Отключённые разделы пользователя.</summary>
    public async Task<IReadOnlyList<string>> MutesAsync(Guid userId, CancellationToken ct) =>
        await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.NotificationMutes).FirstOrDefaultAsync(ct) ?? [];

    /// <summary>Сохранить отключённые разделы; неизвестные коды отбрасываются.</summary>
    public async Task<IReadOnlyList<string>> SetMutesAsync(Guid userId, IEnumerable<string>? muted, CancellationToken ct)
    {
        var list = (muted ?? []).Where(NotificationCatalog.Categories.Contains).Distinct().OrderBy(c => c, StringComparer.Ordinal).ToList();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw new FieldNotFoundException("Пользователь не найден.");
        user.NotificationMutes = list.Count == 0 ? null : list;
        await db.SaveChangesAsync(ct);
        return list;
    }

    private async Task<List<UserNotificationKind>> MutedKindsAsync(Guid userId, CancellationToken ct) =>
        (await MutesAsync(userId, ct)).SelectMany(NotificationCatalog.KindsOf).ToList();

    private static async Task<NotificationCounts> CountsAsync(IQueryable<Domain.Work.UserNotification> mine, CancellationToken ct)
    {
        var groups = await mine.GroupBy(n => new { n.Kind, Unread = n.ReadAt == null })
            .Select(g => new { g.Key.Kind, g.Key.Unread, Count = g.Count() })
            .ToListAsync(ct);
        int Of(string category) => groups.Where(g => NotificationCatalog.Of(g.Kind).Category == category).Sum(g => g.Count);
        return new NotificationCounts(groups.Sum(g => g.Count), groups.Where(g => g.Unread).Sum(g => g.Count), Of(NotificationCatalog.Tasks),
            Of(NotificationCatalog.Ai), Of(NotificationCatalog.Hr));
    }

    private static NotificationItem Item(Domain.Work.UserNotification n, HashSet<Guid> manageable)
    {
        var (type, category) = NotificationCatalog.Of(n.Kind);
        var actions = new List<NotificationAction>();
        if (n.Link is not null && n.Kind is UserNotificationKind.TaskOverdue or UserNotificationKind.TaskSuggestion
                || n.Link is not null && n.Kind == UserNotificationKind.DataSyncProblem && NotificationCatalog.Severity(n.Kind, n.Title) == "critical")
        {
            actions.Add(new NotificationAction("open", "Открыть", "primary", "open"));
        }

        if (WorkTaskService.TaskOfLink(n.Link) is { } task && manageable.Contains(task))
        {
            actions.Add(new NotificationAction("extend_due", "Продлить срок", actions.Count == 0 ? "primary" : "secondary", "extend_due"));
        }

        return new NotificationItem(n.Id, type, category, n.Kind, n.Title, n.Body, n.Link, n.Link, n.CreatedAt, n.ReadAt, n.ReadAt is not null,
            NotificationCatalog.Severity(n.Kind, n.Title), actions);
    }

    private static string CursorOf(Domain.Work.UserNotification n) => $"{n.CreatedAt.UtcTicks.ToString(CultureInfo.InvariantCulture)}_{n.Id:N}";

    private static (DateTimeOffset At, Guid Id)? Cursor(string? cursor)
    {
        var parts = cursor?.Split('_');
        return parts is { Length: 2 } && long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) && Guid.TryParseExact(parts[1], "N", out var id)
               && ticks is > 0 && ticks <= DateTimeOffset.MaxValue.UtcTicks
            ? (new DateTimeOffset(ticks, TimeSpan.Zero), id)
            : null;
    }
}
