using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Security;
using OneBase.Domain.Audit;
using OneBase.Domain.Work;

namespace OneBase.Application.Work;

/// <summary>Сотрудник в настройках отделов: руководитель отдела или кандидат в согласующие.</summary>
public sealed record DepartmentPerson(Guid Id, string Name, string? Position, string? Department);

/// <summary>Отдел в «Настройки → Отделы»: сколько в нём пользователей и кто руководит.</summary>
public sealed record DepartmentCardView(string Code, string Name, int Users, IReadOnlyList<DepartmentPerson> Heads);

/// <summary>Отделы компании и что можно менять: руководителей (право «Пользователи») и маршрут заявок снабжения (право «Настройки снабжения»).</summary>
public sealed record DepartmentsSettingsView(IReadOnlyList<DepartmentCardView> Departments, bool CanEditHeads, bool CanEditSupply);

/// <summary>
/// «Настройки → Отделы»: шесть отделов компании, их руководители и свои настройки каждого отдела (у снабжения — маршрут согласования заявок,
/// он хранится в настройках снабжения). Руководители — те же, что в «Задачах»: руководитель согласует заявки сотрудников своего отдела и
/// управляет задачами отдела. Открывают раздел те, у кого есть право «Пользователи» или «Настройки снабжения».
/// </summary>
public sealed class DepartmentSettingsService(IAppDbContext db, IAuditLogger audit)
{
    public static bool Opens(IReadOnlySet<string> permissions) =>
        permissions.Contains(Permissions.UsersManage) || permissions.Contains(Permissions.ProcurementSettings);

    private static void RequireOpen(IReadOnlySet<string> permissions)
    {
        if (!Opens(permissions))
        {
            throw new FieldForbiddenException("Настройки отделов — у администратора пользователей и того, кто настраивает снабжение.");
        }
    }

    public async Task<DepartmentsSettingsView> ListAsync(IReadOnlySet<string> permissions, CancellationToken ct)
    {
        RequireOpen(permissions);
        var departments = await db.Departments.AsNoTracking().Select(d => new { d.Id, d.Code, d.Name, Users = d.Users.Count(u => u.IsActive) }).ToListAsync(ct);
        var heads = await db.WorkDepartmentHeads.AsNoTracking()
            .Select(h => new { h.DepartmentId, h.UserId, h.User!.FullName, h.User.Position }).ToListAsync(ct);
        var cards = departments
            .OrderBy(d => Array.IndexOf(WorkTaskService.Order, d.Code) is var i and >= 0 ? i : WorkTaskService.Order.Length).ThenBy(d => d.Name)
            .Select(d => new DepartmentCardView(d.Code, d.Name, d.Users,
                heads.Where(h => h.DepartmentId == d.Id).OrderBy(h => h.FullName).Select(h => new DepartmentPerson(h.UserId, h.FullName, h.Position, d.Name)).ToList()))
            .ToList();
        return new DepartmentsSettingsView(cards, permissions.Contains(Permissions.UsersManage), permissions.Contains(Permissions.ProcurementSettings));
    }

    /// <summary>Действующие пользователи — для выбора руководителей, финансиста и директора.</summary>
    public async Task<List<DepartmentPerson>> UsersAsync(IReadOnlySet<string> permissions, CancellationToken ct)
    {
        RequireOpen(permissions);
        return await db.Users.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.FullName)
            .Select(u => new DepartmentPerson(u.Id, u.FullName, u.Position, u.Department == null ? null : u.Department.Name)).ToListAsync(ct);
    }

    /// <summary>Руководители отдела из настроек — право «Пользователи».</summary>
    public async Task SetHeadsAsync(Guid actorId, IReadOnlySet<string> permissions, string code, IReadOnlyList<Guid> userIds, CancellationToken ct)
    {
        if (!permissions.Contains(Permissions.UsersManage))
        {
            throw new FieldForbiddenException("Руководителей отделов назначает администратор пользователей.");
        }

        await ReplaceHeadsAsync(db, audit, actorId, code, userIds, ct);
    }

    /// <summary>
    /// Заменить руководителей отдела (права проверяет вызывающий: «Задачи» — руководство, «Настройки → Отделы» — право «Пользователи»).
    /// Руководитель — действующий пользователь; список пуст — руководителя нет. Было и стало — в журнал аудита.
    /// </summary>
    public static async Task ReplaceHeadsAsync(IAppDbContext db, IAuditLogger audit, Guid actorId, string code, IReadOnlyList<Guid> userIds, CancellationToken ct)
    {
        var department = await db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Code == code, ct) ?? throw new FieldNotFoundException("Отдел не найден.");
        var ids = userIds.Distinct().ToList();
        if (await db.Users.CountAsync(u => ids.Contains(u.Id) && u.IsActive, ct) != ids.Count)
        {
            throw new FieldValidationException("Руководитель — действующий пользователь OneBase.");
        }

        var current = await db.WorkDepartmentHeads.Where(h => h.DepartmentId == department.Id).ToListAsync(ct);
        var before = current.Select(h => h.UserId).ToList();
        db.WorkDepartmentHeads.RemoveRange(current.Where(h => !ids.Contains(h.UserId)));
        foreach (var id in ids.Where(id => current.All(h => h.UserId != id)))
        {
            db.WorkDepartmentHeads.Add(new WorkDepartmentHead { DepartmentId = department.Id, UserId = id });
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, actorId.ToString(), "work.heads.updated", "department", department.Code, new { before, heads = ids }, ct);
    }
}
