using OneBase.Domain.Files;

namespace OneBase.Application.Files;

/// <summary>Выданный доступ к папке отдела другому отделу, роли или сотруднику (ResourcePermission на корневой папке отдела).</summary>
public sealed record DepartmentGrant(Guid DepartmentId, AccessLevel Access);

/// <summary>
/// Кто работает с файлами: сотрудник OneBase (права и отделы — из БД на момент запроса, не из токена) или подключение Windows
/// (один логин на отдел).
/// </summary>
public sealed record FileActor(
    Guid? UserId,
    Guid? ConnectionId,
    string Name,
    bool CanRead,
    bool CanWrite,
    bool IsAdmin,
    IReadOnlySet<Guid> OwnDepartments,
    IReadOnlyList<DepartmentGrant> Grants,
    Guid? ConnectionDepartment = null,
    AccessLevel ConnectionAccess = AccessLevel.Read)
{
    public bool IsConnection => ConnectionId is not null;

    public static FileActor Connection(Guid connectionId, string username, Guid departmentId, AccessLevel access) =>
        new(null, connectionId, username, true, access >= AccessLevel.Write, false, new HashSet<Guid>(), [], departmentId, access);
}

/// <summary>
/// Доступ к папке отдела — одно правило для сайта и для подключения Windows:
/// подключение — только свой отдел и только с уровнем подключения; администратор (files.manage) — всё; сотрудник — свой отдел
/// (чтение при files.read, запись при files.write) и отделы, к которым ему, его роли или отделу выдан доступ; запись по выдаче —
/// только при files.write. Без files.read и files.write — ничего.
/// </summary>
public static class FileAccessRules
{
    public static AccessLevel? AccessTo(FileActor actor, Guid departmentId)
    {
        if (actor.IsConnection)
        {
            return actor.ConnectionDepartment == departmentId ? actor.ConnectionAccess : null;
        }

        if (actor.IsAdmin)
        {
            return AccessLevel.Manage;
        }

        if (!actor.CanRead && !actor.CanWrite)
        {
            return null;
        }

        AccessLevel? best = actor.OwnDepartments.Contains(departmentId) ? actor.CanWrite ? AccessLevel.Write : AccessLevel.Read : null;
        foreach (var grant in actor.Grants.Where(g => g.DepartmentId == departmentId))
        {
            var level = grant.Access >= AccessLevel.Write && !actor.CanWrite ? AccessLevel.Read : grant.Access;
            if (best is null || level > best)
            {
                best = level;
            }
        }

        return best;
    }

    public static bool CanRead(FileActor actor, Guid departmentId) => AccessTo(actor, departmentId) is not null;

    public static bool CanWrite(FileActor actor, Guid departmentId) => AccessTo(actor, departmentId) >= AccessLevel.Write;

    /// <summary>
    /// Логин и пароль подключения отдела видят те, кто может в нём писать (подключение даёт запись): иначе сотрудник с правом только на
    /// чтение получил бы через Windows запись. Создают, меняют и отзывают — администраторы.
    /// </summary>
    public static bool CanSeeConnection(FileActor actor, Guid departmentId) => !actor.IsConnection && CanWrite(actor, departmentId);

    public static bool CanManageConnection(FileActor actor) => !actor.IsConnection && actor.IsAdmin;
}
