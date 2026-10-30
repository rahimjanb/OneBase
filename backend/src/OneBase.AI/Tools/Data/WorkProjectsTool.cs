using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.AI.Consultant;
using OneBase.AI.Knowledge;
using OneBase.AI.Security;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Security;
using OneBase.Application.Work;
using OneBase.Domain.Work;

namespace OneBase.AI.Tools.Data;

/// <summary>
/// Проекты раздела «Задачи» (вкладка «Проекты»): отдел, руководитель проекта и участники, задачи проекта — открыто, просрочено, на проверке,
/// выполнено. Какие проекты видны, решает WorkTaskService.ProjectsAsync, как на странице.
/// </summary>
internal sealed class GetWorkProjectsTool(WorkTaskService tasks, IUserPermissions permissions, IAppDbContext db) : DataTool
{
    internal const int DefaultLimit = 20;
    private const int DoneWindowDays = 30;

    public override string Name => "get_work_projects";
    public override string Title => "Проекты отделов";
    public override string Source => KnowledgeSources.WorkTasks;
    public override string? RequiredPermission => Permissions.WorkTasks;

    public override string Description =>
        "Проекты раздела «Задачи» OneBase: название, отдел, описание, руководитель проекта (PM) и участники, задачи проекта — открыто, просрочено, " +
        "на проверке, заблокировано, выполнено за 30 дней, ближайший срок. Только проекты, которые видит пользователь (проекты его отделов и те, где " +
        "он участник; руководство — все). Вопросы: «какие у нас проекты», «кто ведёт проект …», «где в проектах просрочки». Задачи по отделам — get_department_tasks.";

    public override JsonElement InputSchema { get; } = Schema(
        ("query", new { type = "string", description = "Часть названия проекта или имени участника. Без него — все видимые проекты." }),
        ("department", new { type = "string", description = "Отдел проекта: код (sales, finance…) или название. Без него — все отделы." }),
        ("archived", new { type = "boolean", description = "true — вместе с архивными проектами. По умолчанию — только действующие." }),
        ("limit", new { type = "integer", description = $"Сколько проектов: по умолчанию {DefaultLimit}, до 50." }));

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        var limit = args.Int("limit", 1, 50) ?? DefaultLimit;
        var userId = context.UserId ?? throw new ToolArgumentException("Проекты отдаются только от имени пользователя.");
        var granted = await permissions.GetAsync(userId, ct);
        if (granted.Count == 0)
        {
            throw new ToolArgumentException("Пользователь отключён или не найден — проекты недоступны.");
        }

        var work = await tasks.ContextAsync(userId, granted, ct);
        var projects = await tasks.ProjectsAsync(work, args.Bool("archived") == true, ct);
        if (args.Str("department") is { } department)
        {
            var key = WorkFieldText.Key(department);
            projects = projects.Where(p => string.Equals(p.DepartmentCode, department, StringComparison.OrdinalIgnoreCase)
                                           || WorkFieldText.Key(p.DepartmentName).Contains(key, StringComparison.Ordinal)).ToList();
        }

        if (args.Str("query") is { } query)
        {
            var key = WorkFieldText.Key(query);
            projects = projects.Where(p => WorkFieldText.Key(p.Name).Contains(key, StringComparison.Ordinal)
                                           || p.Members.Any(m => WorkFieldText.Key(m.Name).Contains(key, StringComparison.Ordinal))).ToList();
        }

        var ids = projects.Select(p => p.Id).ToList();
        var today = FieldClock.Today;
        var since = FieldClock.DayStartUtc(today.AddDays(1 - DoneWindowDays));
        var rows = await db.WorkTasks.AsNoTracking()
            .Where(t => t.ProjectId != null && ids.Contains(t.ProjectId.Value) && t.ArchivedAt == null)
            .Select(t => new { Project = t.ProjectId!.Value, t.Status, t.DueDate, t.IsBlocked, t.CompletedAt })
            .ToListAsync(ct);
        var byProject = rows.ToLookup(r => r.Project);
        bool Open(WorkTaskStatus s) => s is not (WorkTaskStatus.Done or WorkTaskStatus.Cancelled);

        var shown = projects.TakeTop(args, limit, "projects").ToList();
        return Data(new
        {
            Total = projects.Count,
            Shown = shown.Count,
            Projects = shown.Select(p =>
            {
                var t = byProject[p.Id].ToList();
                var open = t.Where(x => Open(x.Status)).ToList();
                return new
                {
                    p.Name,
                    Department = p.DepartmentName,
                    p.Description,
                    p.Archived,
                    Managers = p.Members.Where(m => m.IsManager).Select(m => m.Name),
                    Members = p.Members.Where(m => !m.IsManager).Select(m => m.Name),
                    Tasks = new
                    {
                        Open = open.Count,
                        Overdue = open.Count(x => x.DueDate < today),
                        OnReview = open.Count(x => x.Status == WorkTaskStatus.Review),
                        Blocked = open.Count(x => x.IsBlocked),
                        DoneLast30Days = t.Count(x => x.Status == WorkTaskStatus.Done && x.CompletedAt >= since),
                        NextDue = open.Where(x => x.DueDate != null).Select(x => x.DueDate).Min() is { } due ? WorkFieldText.Day(due) : null,
                    },
                };
            }),
            Note = "Задачи проекта без архивных. Overdue — срок прошёл, задача не выполнена и не отменена. Сами задачи — get_department_tasks.",
        }, new DataSource("Задачи · Проекты", null, "/tasks/projects"));
    }
}
