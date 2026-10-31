using Microsoft.EntityFrameworkCore;
using OneBase.Application.Field;
using OneBase.Domain.Work;

namespace OneBase.Application.Work;

/// <summary>
/// Мои задачи для «Моего дня» — только назначенные мне (AssigneeId), без отменённых. Waiting — ждут другого человека: на проверке или
/// заблокированы. Done и Total — за текущий месяц: Total = открытые + выполненные в этом месяце.
/// </summary>
public sealed record WorkMyDay(
    int Total,
    int New,
    int InProgress,
    int Waiting,
    int Overdue,
    int Done,
    IReadOnlyList<WorkMyTask> Upcoming,
    WorkMyEfficiency Efficiency);

public sealed record WorkMyTask(Guid Id, string Title, WorkTaskStatus Status, bool IsBlocked, WorkTaskPriority Priority, DateOnly? DueDate, TimeOnly? DueTime,
    bool Overdue);

/// <summary>
/// Личная эффективность за 6 недель (текущая и пять прошлых). В срок — сдана (на проверку, а без проверки — выполнена) не позже срока; считается
/// только по выполненным задачам со сроком, без таких — OnTimePct = null. Weeks — по понедельникам, последняя — текущая.
/// </summary>
public sealed record WorkMyEfficiency(int? OnTimePct, int OnTime, int ClosedWithDue, int DoneThisWeek, int DoneLastWeek, IReadOnlyList<WorkMyWeek> Weeks);

public sealed record WorkMyWeek(DateOnly Monday, int Number, int Count);

public sealed partial class WorkTaskService
{
    public const int MyUpcomingLimit = 5;
    public const int EfficiencyWeeks = 6;

    public async Task<WorkMyDay> MyDayAsync(WorkContext ctx, CancellationToken ct)
    {
        var me = ctx.UserId;
        var today = Today;
        var now = NowTime;
        var monthStartUtc = FieldClock.DayStartUtc(new DateOnly(today.Year, today.Month, 1));
        var mine = db.WorkTasks.AsNoTracking().Where(t => t.AssigneeId == me);
        var open = mine.Where(t => t.ArchivedAt == null && Open.Contains(t.Status));

        var counts = await open
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Open = g.Count(),
                New = g.Count(t => t.Status == WorkTaskStatus.New),
                InProgress = g.Count(t => t.Status == WorkTaskStatus.InProgress),
                Waiting = g.Count(t => t.Status == WorkTaskStatus.Review || t.IsBlocked),
                Overdue = g.Count(t => t.DueDate != null && (t.DueDate < today || (t.DueDate == today && t.DueTime != null && t.DueTime <= now))),
            })
            .FirstOrDefaultAsync(ct);
        var doneThisMonth = await mine.CountAsync(t => t.Status == WorkTaskStatus.Done && t.CompletedAt >= monthStartUtc, ct);

        var upcoming = await open.Where(t => t.DueDate != null)
            .OrderBy(t => t.DueDate).ThenBy(t => t.DueTime == null).ThenBy(t => t.DueTime).ThenBy(t => t.CreatedAt)
            .Take(MyUpcomingLimit)
            .Select(t => new { t.Id, t.Title, t.Status, t.IsBlocked, t.Priority, t.DueDate, t.DueTime })
            .ToListAsync(ct);

        var thisMonday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var firstMonday = thisMonday.AddDays(-7 * (EfficiencyWeeks - 1));
        var since = FieldClock.DayStartUtc(firstMonday);
        var done = await mine.Where(t => t.Status == WorkTaskStatus.Done && t.CompletedAt >= since)
            .Select(t => new MyDone(t.CompletedAt!.Value, t.ReviewRequestedAt, t.DueDate, t.DueTime))
            .ToListAsync(ct);

        return new WorkMyDay(
            (counts?.Open ?? 0) + doneThisMonth,
            counts?.New ?? 0,
            counts?.InProgress ?? 0,
            counts?.Waiting ?? 0,
            counts?.Overdue ?? 0,
            doneThisMonth,
            upcoming.Select(t => new WorkMyTask(t.Id, t.Title, t.Status, t.IsBlocked, t.Priority, t.DueDate, t.DueTime,
                IsOverdue(t.Status, t.DueDate, t.DueTime, today, now))).ToList(),
            Efficiency(done, firstMonday));
    }

    public sealed record MyDone(DateTimeOffset CompletedAt, DateTimeOffset? ReviewRequestedAt, DateOnly? DueDate, TimeOnly? DueTime);

    /// <summary>Эффективность по выполненным задачам с первого понедельника периода (чистая функция — проверяется тестами).</summary>
    public static WorkMyEfficiency Efficiency(IReadOnlyList<MyDone> done, DateOnly firstMonday)
    {
        var weeks = Enumerable.Range(0, EfficiencyWeeks)
            .Select(i => firstMonday.AddDays(7 * i))
            .Select(monday => new WorkMyWeek(monday, System.Globalization.ISOWeek.GetWeekOfYear(monday.ToDateTime(TimeOnly.MinValue)),
                done.Count(d => FieldClock.LocalDate(d.CompletedAt) is var day && day >= monday && day < monday.AddDays(7))))
            .ToList();

        var withDue = done.Where(d => d.DueDate is not null).ToList();
        var onTime = withDue.Count(d =>
        {
            // Сдал на проверку — это и есть «сделал»; без проверки (или устаревшая отметка) — время выполнения.
            var at = d.ReviewRequestedAt is { } review && review <= d.CompletedAt ? review : d.CompletedAt;
            var local = at.ToOffset(FieldClock.Offset);
            var day = DateOnly.FromDateTime(local.DateTime);
            return day < d.DueDate || (day == d.DueDate && (d.DueTime is not { } time || TimeOnly.FromDateTime(local.DateTime) <= time));
        });
        return new WorkMyEfficiency(withDue.Count == 0 ? null : (int)Math.Round(100.0 * onTime / withDue.Count), onTime, withDue.Count,
            weeks[^1].Count, weeks[^2].Count, weeks);
    }
}

/// <summary>Вложение к моей задаче, добавленное другим человеком, — «Назначенные» в «Моих документах».</summary>
public sealed record WorkMyAttachment(Guid Id, string FileName, string ContentType, long SizeBytes, DateTimeOffset CreatedAt, string? By, Guid TaskId, string TaskTitle);

public sealed partial class WorkTaskService
{
    public async Task<List<WorkMyAttachment>> MyAttachmentsAsync(WorkContext ctx, int take, CancellationToken ct)
    {
        var me = ctx.UserId;
        return await db.WorkTaskAttachments.AsNoTracking()
            .Where(a => a.Task!.AssigneeId == me && a.Task.ArchivedAt == null && (a.UploadedById == null || a.UploadedById != me))
            .OrderByDescending(a => a.CreatedAt)
            .Take(take)
            .Select(a => new WorkMyAttachment(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.CreatedAt, a.UploadedBy == null ? null : a.UploadedBy.FullName, a.TaskId,
                a.Task!.Title))
            .ToListAsync(ct);
    }
}
