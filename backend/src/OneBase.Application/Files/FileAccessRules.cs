using OneBase.Domain.Files;

namespace OneBase.Application.Files;

/// <summary>Выданный доступ к папке отдела другому отделу, роли или сотруднику (ResourcePermission на корневой папке отдела).</summary>
public sealed record DepartmentGrant(Guid DepartmentId, AccessLevel Access);

/// <summary>Выданный доступ к общей папке («Общие файлы»): сотруднику, его роли или отделу (ResourcePermission на корне общей папки).</summary>
public sealed record SharedGrant(Guid RootId, AccessLevel Access);

/// <summary>Откуда пришёл запрос — пишется в журнал действий вместе с IP.</summary>
public static class FileSources
{
    public const string Web = "web";
    public const string WindowsLink = "windows-link";
    public const string WindowsLogin = "windows-login";
    public const string WindowsPersonal = "windows-personal";
}

/// <summary>
/// Кто работает с файлами: сотрудник OneBase (права и отделы — из БД на момент запроса, не из токена) или подключение Windows
/// (один логин на отдел). Source и Ip — откуда запрос (для журнала); LockTokens — токены блокировок WebDAV из заголовка If.
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
    AccessLevel ConnectionAccess = AccessLevel.Read,
    bool CanReadAll = false,
    IReadOnlyList<SharedGrant>? SharedGrants = null)
{
    public bool IsConnection => ConnectionId is not null;

    public IReadOnlyList<SharedGrant> Shared => SharedGrants ?? [];

    public string Source { get; init; } = FileSources.Web;

    public string? Ip { get; init; }

    /// <summary>Личная ссылка Windows, через которую пришёл сотрудник (для журнала и «последней активности»).</summary>
    public Guid? PersonalLinkId { get; init; }

    public IReadOnlySet<string> LockTokens { get; init; } = new HashSet<string>();

    public static FileActor Connection(Guid connectionId, string username, Guid departmentId, AccessLevel access, IReadOnlyList<SharedGrant>? shared = null) =>
        new(null, connectionId, username, true, access >= AccessLevel.Write, false, new HashSet<Guid>(), [], departmentId, access, SharedGrants: shared);
}

/// <summary>
/// Доступ к папке отдела — одно правило для сайта и для подключения Windows:
/// подключение — только свой отдел и только с уровнем подключения; администратор (files.manage) — всё; files.read.all (администратор
/// и директор) — любой отдел не меньше чем на чтение; сотрудник — свой отдел (чтение при files.read, запись при files.write) и отделы,
/// к которым ему, его роли или отделу выдан доступ; запись по выдаче — только при files.write. Без прав на файлы — ничего.
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
            return actor.CanReadAll ? AccessLevel.Read : null;
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

        return best ?? (actor.CanReadAll ? AccessLevel.Read : null);
    }

    /// <summary>
    /// Общая папка видна только тем, кому её выдали (сотруднику, его роли или отделу), и администратору (files.manage — управление
    /// всеми). Просмотр всех отделов (files.read.all) общих папок не открывает. Редактирование и управление по выдаче — только при
    /// files.write, иначе просмотр. Подключение отдела видит общие папки, выданные его отделу, не выше своего уровня и без управления.
    /// </summary>
    public static AccessLevel? AccessToShared(FileActor actor, Guid rootId)
    {
        AccessLevel? granted = null;
        foreach (var grant in actor.Shared.Where(g => g.RootId == rootId))
        {
            granted = granted is null || grant.Access > granted ? grant.Access : granted;
        }

        if (actor.IsConnection)
        {
            if (granted is not { } level)
            {
                return null;
            }

            var capped = level > AccessLevel.Write ? AccessLevel.Write : level;
            return capped > actor.ConnectionAccess ? actor.ConnectionAccess : capped;
        }

        if (actor.IsAdmin)
        {
            return AccessLevel.Manage;
        }

        if ((!actor.CanRead && !actor.CanWrite) || granted is not { } access)
        {
            return null;
        }

        return access >= AccessLevel.Write && !actor.CanWrite ? AccessLevel.Read : access;
    }

    /// <summary>Доступ к папке или файлу по её пространству: отдел или общая папка (у старых папок без того и другого — никакого).</summary>
    public static AccessLevel? AccessToItem(FileActor actor, Guid? departmentId, Guid? sharedRootId) =>
        departmentId is { } d ? AccessTo(actor, d) : sharedRootId is { } r ? AccessToShared(actor, r) : null;

    public static bool CanRead(FileActor actor, Guid departmentId) => AccessTo(actor, departmentId) is not null;

    public static bool CanWrite(FileActor actor, Guid departmentId) => AccessTo(actor, departmentId) >= AccessLevel.Write;

    /// <summary>
    /// Логин и пароль подключения отдела видят те, кто может в нём писать (подключение даёт запись): иначе сотрудник с правом только на
    /// чтение получил бы через Windows запись. Создают, меняют и отзывают — администраторы.
    /// </summary>
    public static bool CanSeeConnection(FileActor actor, Guid departmentId) => !actor.IsConnection && CanWrite(actor, departmentId);

    public static bool CanManageConnection(FileActor actor) => !actor.IsConnection && actor.IsAdmin;

    /// <summary>Личная ссылка Windows — сотруднику с любым правом на файлы (через неё он получает ровно свои права, не больше).</summary>
    public static bool CanHavePersonalLink(FileActor actor) => !actor.IsConnection && (actor.CanRead || actor.CanWrite || actor.IsAdmin || actor.CanReadAll);
}
