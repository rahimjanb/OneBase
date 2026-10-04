using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Domain.Audit;
using OneBase.Domain.Field;

namespace OneBase.Application.Field;

public sealed record FieldTaskRow(
    Guid Id,
    string Title,
    string? Description,
    FieldPriority Priority,
    FieldTaskStatus Status,
    DateOnly? DueDate,
    bool Overdue,
    Guid AssignedToId,
    string AssignedTo,
    FieldActorType CreatedByType,
    string? CreatedBy,
    long? MarketId,
    string? MarketName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? Result,
    Guid? RecommendationId,
    bool CanEdit,
    IReadOnlyList<FieldTaskStatus> NextStatuses);

public sealed record FieldTaskQuery(string? Status, Guid? AssignedToId, FieldPriority? Priority, string? Due, long? MarketId, FieldActorType? CreatedByType, string? Search, int Page = 1, int PageSize = 50);

public sealed record FieldTaskInput(string? Title, string? Description, Guid? AssignedToId, long? MarketId, FieldPriority Priority = FieldPriority.Medium, DateOnly? DueDate = null);

public sealed record FieldTaskStatusInput(FieldTaskStatus Status, string? Result, DateOnly? PostponeTo);

/// <summary>
/// Задачи. Ставят РМ и супервайзер (агенту своей зоны), AI — через подтверждённую рекомендацию, система — по событиям;
/// агент может завести задачу себе. Исполнитель ведёт задачу до «выполнена», супервайзер подтверждает или возвращает.
/// </summary>
public sealed class FieldTaskService(
    IAppDbContext db,
    FieldDirectory directory,
    FieldAssignments assignments,
    ILinkoSalesProvider linko,
    FieldNotifier notifier,
    IAuditLogger audit)
{
    private static readonly FieldTaskStatus[] AllStatuses = Enum.GetValues<FieldTaskStatus>();

    private IQueryable<FieldTask> Visible(FieldScope scope)
    {
        var members = scope.MemberIds.ToArray();
        return db.FieldTasks.AsNoTracking().Where(t => members.Contains(t.AssignedToId));
    }

    public async Task<PageResult<FieldTaskRow>> ListAsync(FieldScope scope, FieldTaskQuery query, CancellationToken ct)
    {
        var today = FieldClock.Today;
        var q = Visible(scope);
        q = query.Status switch
        {
            null or "" or "open" => q.Where(t => FieldTaskRules.Open.Contains(t.Status)),
            "closed" => q.Where(t => !FieldTaskRules.Open.Contains(t.Status)),
            "all" => q,
            _ when Enum.TryParse<FieldTaskStatus>(query.Status, true, out var st) => q.Where(t => t.Status == st),
            _ => q,
        };

        if (query.AssignedToId is { } assignee)
        {
            q = q.Where(t => t.AssignedToId == assignee);
        }

        if (query.Priority is { } priority)
        {
            q = q.Where(t => t.Priority == priority);
        }

        if (query.MarketId is { } market)
        {
            q = q.Where(t => t.MarketId == market);
        }

        if (query.CreatedByType is { } by)
        {
            q = q.Where(t => t.CreatedByType == by);
        }

        q = query.Due switch
        {
            "today" => q.Where(t => t.DueDate == today),
            "overdue" => q.Where(t => t.DueDate < today && FieldTaskRules.Open.Contains(t.Status)),
            "week" => q.Where(t => t.DueDate >= today && t.DueDate <= today.AddDays(7)),
            _ => q,
        };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var text = query.Search.Trim().ToLower();
            q = q.Where(t => t.Title.ToLower().Contains(text) || (t.Description != null && t.Description.ToLower().Contains(text)));
        }

        var total = await q.CountAsync(ct);
        var pageSize = Math.Clamp(query.PageSize, 10, 200);
        var page = Math.Max(query.Page, 1);
        var rows = await q
            .OrderBy(t => FieldTaskRules.Open.Contains(t.Status) && t.DueDate < today ? 0 : 1)
            .ThenByDescending(t => (t.Priority == FieldPriority.Urgent ? 3 : t.Priority == FieldPriority.High ? 2 : t.Priority == FieldPriority.Medium ? 1 : 0)) // приоритет хранится строкой — сортируем по смыслу, не по алфавиту
            .ThenBy(t => t.DueDate == null ? 1 : 0).ThenBy(t => t.DueDate)
            .ThenByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
        return new PageResult<FieldTaskRow>(await RowsAsync(scope, rows, ct), total, page, pageSize);
    }

    public async Task<List<FieldTaskRow>> ForMarketAsync(FieldScope scope, long marketId, CancellationToken ct)
    {
        var rows = await Visible(scope).Where(t => t.MarketId == marketId).OrderByDescending(t => t.CreatedAt).Take(20).ToListAsync(ct);
        return await RowsAsync(scope, rows, ct);
    }

    public async Task<FieldTaskRow> GetAsync(FieldScope scope, Guid id, CancellationToken ct)
    {
        var task = await Visible(scope).FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new FieldNotFoundException("Задача не найдена.");
        return (await RowsAsync(scope, [task], ct))[0];
    }

    private async Task<List<FieldTaskRow>> RowsAsync(FieldScope scope, IReadOnlyList<FieldTask> tasks, CancellationToken ct)
    {
        var snapshot = await directory.GetAsync(ct);
        var markets = await linko.MarketsAsync(tasks.Where(t => t.MarketId is not null).Select(t => t.MarketId!.Value).ToList(), ct);
        var today = FieldClock.Today;
        return tasks.Select(t =>
        {
            var isAssignee = scope.MemberId == t.AssignedToId;
            var canPlan = scope.CanPlan && scope.CanSeeMember(t.AssignedToId) && !isAssignee || scope.CanPlanFor(t.AssignedToId);
            return new FieldTaskRow(t.Id, t.Title, t.Description, t.Priority, t.Status, t.DueDate, FieldTaskRules.IsOpen(t.Status) && t.DueDate < today,
                t.AssignedToId, snapshot.NameOf(t.AssignedToId), t.CreatedByType,
                t.CreatedByType == FieldActorType.Ai ? "AI" : t.CreatedByType == FieldActorType.System ? "Система" : snapshot.NameOf(t.CreatedById),
                t.MarketId, t.MarketId is { } m ? markets.GetValueOrDefault(m)?.Name : null, t.CreatedAt, t.CompletedAt, t.Result, t.RecommendationId,
                canPlan, AllStatuses.Where(s => FieldTaskRules.CanTransition(t.Status, s, isAssignee, canPlan)).ToList());
        }).ToList();
    }

    public async Task<Guid> CreateAsync(FieldScope scope, FieldTaskInput input, CancellationToken ct)
    {
        var (title, description) = Validate(input);
        var assignee = input.AssignedToId ?? scope.MemberId ?? throw new FieldValidationException("Укажите исполнителя.");
        var self = assignee == scope.MemberId;
        if (!self && !(scope.CanPlan && scope.CanSeeMember(assignee)))
        {
            throw new FieldForbiddenException("Поставить задачу можно агенту своей команды.");
        }

        if (input.MarketId is { } marketId)
        {
            await assignments.RequireMarketAsync(scope, marketId, ct);
        }

        var snapshot = await directory.GetAsync(ct);
        var task = new FieldTask
        {
            Title = title,
            Description = description,
            AssignedToId = assignee,
            SupervisorId = snapshot.SupervisorOf(assignee),
            MarketId = input.MarketId,
            Priority = input.Priority,
            DueDate = input.DueDate,
            CreatedById = scope.MemberId,
            CreatedByType = self && scope.IsAgent ? FieldActorType.Agent : scope.IsRm ? FieldActorType.Rm : FieldActorType.Supervisor,
        };
        db.FieldTasks.Add(task);
        if (!self)
        {
            notifier.Notify(assignee, FieldNotificationKind.TaskAssigned, $"Новая задача: {title}", DueText(task.DueDate), $"/field/tasks/{task.Id}");
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, scope.UserId.ToString(), "field.task.created", "field_task", task.Id.ToString(),
            new { assignee, task.MarketId, priority = task.Priority.ToString(), task.DueDate }, ct);
        return task.Id;
    }

    /// <summary>Задача от AI или системы (без проверки области — вызывается из доверенного кода).</summary>
    public FieldTask CreateSystem(string title, string? description, Guid assignee, Guid? supervisor, long? marketId, FieldPriority priority, DateOnly? due, FieldActorType by, Guid? recommendationId, Guid? createdById)
    {
        var task = new FieldTask
        {
            Title = title.Length <= 300 ? title : title[..299] + "…",
            Description = description,
            AssignedToId = assignee,
            SupervisorId = supervisor,
            MarketId = marketId,
            Priority = priority,
            DueDate = due,
            CreatedByType = by,
            CreatedById = createdById,
            RecommendationId = recommendationId,
        };
        db.FieldTasks.Add(task);
        notifier.Notify(assignee, FieldNotificationKind.TaskAssigned, $"Новая задача: {task.Title}", DueText(due), $"/field/tasks/{task.Id}");
        return task;
    }

    public async Task UpdateAsync(FieldScope scope, Guid id, FieldTaskInput input, CancellationToken ct)
    {
        var task = await db.FieldTasks.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (task is null || !scope.CanSeeMember(task.AssignedToId))
        {
            throw new FieldNotFoundException("Задача не найдена.");
        }

        var isAssignee = scope.MemberId == task.AssignedToId;
        var canPlan = scope.CanPlan && !isAssignee || scope.CanPlanFor(task.AssignedToId);
        var ownPersonal = isAssignee && task.CreatedById == scope.MemberId;
        if (!canPlan && !ownPersonal)
        {
            throw new FieldForbiddenException("Менять задачу может тот, кто её поставил, супервайзер или РМ.");
        }

        var (title, description) = Validate(input, task.DueDate);
        var oldAssignee = task.AssignedToId;
        if (input.AssignedToId is { } assignee && assignee != task.AssignedToId)
        {
            if (!canPlan || !scope.CanSeeMember(assignee))
            {
                throw new FieldForbiddenException("Передать задачу можно только участнику своей зоны.");
            }

            task.AssignedToId = assignee;
            task.SupervisorId = (await directory.GetAsync(ct)).SupervisorOf(assignee);
        }

        if (input.MarketId is { } marketId && marketId != task.MarketId)
        {
            await assignments.RequireMarketAsync(scope, marketId, ct);
        }

        var changedDue = task.DueDate != input.DueDate;
        task.Title = title;
        task.Description = description;
        task.MarketId = input.MarketId;
        task.Priority = input.Priority;
        task.DueDate = input.DueDate;
        task.UpdatedAt = DateTimeOffset.UtcNow;

        if (oldAssignee != task.AssignedToId)
        {
            notifier.Notify(task.AssignedToId, FieldNotificationKind.TaskAssigned, $"Вам передана задача: {title}", DueText(task.DueDate), $"/field/tasks/{task.Id}");
            notifier.Notify(oldAssignee, FieldNotificationKind.TaskChanged, $"Задача «{title}» передана другому исполнителю", null, "/field/tasks");
        }
        else if (!isAssignee)
        {
            notifier.Notify(task.AssignedToId, FieldNotificationKind.TaskChanged, $"Изменена задача: {title}", changedDue ? DueText(task.DueDate) : null, $"/field/tasks/{task.Id}");
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, scope.UserId.ToString(), "field.task.updated", "field_task", task.Id.ToString(),
            new { assignee = task.AssignedToId, previous = oldAssignee == task.AssignedToId ? (Guid?)null : oldAssignee, priority = task.Priority.ToString(), task.DueDate }, ct);
    }

    public async Task ChangeStatusAsync(FieldScope scope, Guid id, FieldTaskStatusInput input, CancellationToken ct)
    {
        var task = await db.FieldTasks.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (task is null || !scope.CanSeeMember(task.AssignedToId))
        {
            throw new FieldNotFoundException("Задача не найдена.");
        }

        var isAssignee = scope.MemberId == task.AssignedToId;
        var canPlan = scope.CanPlan && !isAssignee || scope.CanPlanFor(task.AssignedToId);
        if (!FieldTaskRules.CanTransition(task.Status, input.Status, isAssignee, canPlan))
        {
            throw new FieldValidationException($"Нельзя перевести задачу из «{task.Status}» в «{input.Status}».");
        }

        var from = task.Status;
        task.Status = input.Status;
        task.UpdatedAt = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(input.Result))
        {
            task.Result = input.Result.Trim().Length <= 2000 ? input.Result.Trim() : input.Result.Trim()[..2000];
        }

        switch (input.Status)
        {
            case FieldTaskStatus.Completed:
                task.CompletedAt = DateTimeOffset.UtcNow;
                notifier.Notify(task.SupervisorId ?? task.CreatedById, FieldNotificationKind.TaskChanged, $"Выполнена задача: {task.Title}", task.Result, $"/field/tasks/{task.Id}");
                break;
            case FieldTaskStatus.Verified:
                task.VerifiedAt = DateTimeOffset.UtcNow;
                notifier.Notify(task.AssignedToId, FieldNotificationKind.TaskVerified, $"Задача подтверждена: {task.Title}", null, $"/field/tasks/{task.Id}");
                break;
            case FieldTaskStatus.Postponed:
                if (input.PostponeTo is { } to)
                {
                    if (to < FieldClock.Today)
                    {
                        throw new FieldValidationException("Перенести можно на сегодня или позже.");
                    }

                    task.DueDate = to;
                }

                if (!isAssignee)
                {
                    notifier.Notify(task.AssignedToId, FieldNotificationKind.TaskChanged, $"Задача перенесена: {task.Title}", DueText(task.DueDate), $"/field/tasks/{task.Id}");
                }
                else
                {
                    notifier.Notify(task.SupervisorId, FieldNotificationKind.TaskChanged, $"Агент перенёс задачу: {task.Title}", DueText(task.DueDate), $"/field/tasks/{task.Id}");
                }

                break;
            case FieldTaskStatus.Cancelled:
                notifier.Notify(task.AssignedToId, FieldNotificationKind.TaskChanged, $"Задача отменена: {task.Title}", null, "/field/tasks");
                break;
            case FieldTaskStatus.InProgress when from == FieldTaskStatus.Completed:
                task.CompletedAt = null;
                notifier.Notify(task.AssignedToId, FieldNotificationKind.TaskChanged, $"Задача возвращена в работу: {task.Title}", task.Result, $"/field/tasks/{task.Id}");
                break;
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, scope.UserId.ToString(), "field.task.status", "field_task", task.Id.ToString(),
            new { from = from.ToString(), to = task.Status.ToString(), task.DueDate }, ct);
    }

    /// <summary>Раз в день: напомнить исполнителю и супервайзеру о просроченных задачах.</summary>
    public async Task<int> NotifyOverdueAsync(DateOnly today, CancellationToken ct)
    {
        var overdue = await db.FieldTasks.AsNoTracking()
            .Where(t => FieldTaskRules.Open.Contains(t.Status) && t.DueDate < today)
            .Select(t => new { t.Id, t.Title, t.AssignedToId, t.SupervisorId, t.DueDate })
            .ToListAsync(ct);
        if (overdue.Count == 0)
        {
            return 0;
        }

        var since = FieldClock.DayStartUtc(today);
        var already = await db.FieldNotifications.AsNoTracking()
            .Where(n => n.Kind == FieldNotificationKind.TaskOverdue && n.CreatedAt >= since && n.Link != null)
            .Select(n => n.Link!)
            .ToListAsync(ct);
        var sent = 0;
        foreach (var t in overdue.Where(t => !already.Contains($"/field/tasks/{t.Id}")))
        {
            notifier.Notify([t.AssignedToId, t.SupervisorId], FieldNotificationKind.TaskOverdue, $"Просрочена задача: {t.Title}", $"Срок был {t.DueDate:dd.MM.yyyy}.", $"/field/tasks/{t.Id}");
            sent++;
        }

        await db.SaveChangesAsync(ct);
        return sent;
    }

    /// <param name="currentDue">Срок задачи до правки: прежний срок, даже прошедший, можно оставить (правка просроченной задачи).</param>
    private static (string Title, string? Description) Validate(FieldTaskInput input, DateOnly? currentDue = null)
    {
        var title = (input.Title ?? string.Empty).Trim();
        if (title.Length is 0 or > 300)
        {
            throw new FieldValidationException("Название задачи — от 1 до 300 символов.");
        }

        var description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        if (description is { Length: > 4000 })
        {
            throw new FieldValidationException("Описание — до 4000 символов.");
        }

        if (input.DueDate is { } due && due != currentDue && due < FieldClock.Today.AddDays(-1))
        {
            throw new FieldValidationException("Срок не может быть в прошлом.");
        }

        return (title, description);
    }

    private static string? DueText(DateOnly? due) => due is { } d ? $"Срок: {d:dd.MM.yyyy}" : null;
}
