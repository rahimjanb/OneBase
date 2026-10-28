using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OneBase.Application.Abstractions;
using OneBase.Application.Files;
using OneBase.Domain.Audit;
using OneBase.Domain.Files;
using OneBase.Domain.Identity;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.Files;

public sealed record ConnectionView(
    string Status,
    string ConnectionType,
    string? Address,
    string? UncPath,
    string? Username,
    string Access,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? PasswordChangedAt,
    DateTimeOffset? RevokedAt,
    bool DomainConfigured,
    bool CanView,
    bool CanManage,
    IReadOnlyList<string> Guide,
    IReadOnlyList<string> Notes,
    // Адрес без пароля со скрытым кодом («https://files.1base.uz/k/••••••••/production»); сам адрес — по «Показать» (POST …/link).
    string? LinkAddress = null,
    DateTimeOffset? LinkExpiresAt = null,
    // Общие папки, выданные отделу: их открывает та же ссылка (адрес — с именем папки вместо кода отдела).
    IReadOnlyList<SharedLinkRef>? SharedFolders = null);

public sealed record SharedLinkRef(string Name, string Access, string? MaskedAddress);

/// <summary>Секретная ссылка: адрес для «Подключить сетевой диск», тот же адрес путём Windows и адреса общих папок по той же ссылке.</summary>
public sealed record ConnectionLink(string Address, string? UncPath, IReadOnlyList<SharedLinkAddress>? Shared = null);

public sealed record SharedLinkAddress(string Name, string Address, string? UncPath);

/// <summary>Личная ссылка Windows сотрудника: Status — none | active | expired. Folders — что видно на диске (папки отделов и общие папки).</summary>
public sealed record PersonalLinkView(
    string Status,
    string? MaskedAddress,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt,
    bool DomainConfigured,
    IReadOnlyList<string> Folders,
    IReadOnlyList<string> Guide,
    IReadOnlyList<string> Notes);

public sealed record ConnectionTestStep(string Name, bool Ok, string Detail);

public sealed record ConnectionTestResult(bool Ok, IReadOnlyList<ConnectionTestStep> Steps);

public enum DavAuthStatus
{
    Ok,
    Failed,
    Locked,
}

public sealed record DavAuthResult(DavAuthStatus Status, FileActor? Actor);

/// <summary>
/// Подключение папки отдела к Windows (WebDAV по HTTPS, один логин на отдел): создание, смена пароля, отзыв, повторный показ пароля,
/// проверка входа и настоящая проверка подключения (хранилище, база, публичный адрес WebDAV с логином и паролем).
/// Пароль в журнал, логи и ответы, кроме явного «Показать пароль», не попадает.
/// Главный способ — секретная ссылка (адрес без пароля /k/{код}/{отдел}): Word и Excel (Microsoft 365) блокируют вход по паролю (Basic)
/// на любой сервер, пока это не разрешено на компьютере, а по ссылке открывают и сохраняют файлы без вопросов. Ссылка — как пароль:
/// в базе хэш и шифр, показывается по «Показать» (в журнал — факт показа), новая ссылка отменяет старую сразу.
/// </summary>
public sealed class FileConnectionsService(
    OneBaseDbContext db,
    IDataProtectionProvider protection,
    IMemoryCache cache,
    IAuditLogger audit,
    FilesOptions options,
    IFileStorage storage,
    IHttpClientFactory httpClients,
    DepartmentFilesService files)
{
    public const string ConnectionType = "WebDAV (HTTPS)";

    /// <summary>Неверных входов с одного адреса на один логин до блокировки на <see cref="LockWindow"/>.</summary>
    public const int MaxFailures = 10;

    private static readonly TimeSpan LockWindow = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan AuthCacheTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan TouchEvery = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan LinkMissTtl = TimeSpan.FromMinutes(1);
    private const string Masked = "••••••••";

    /// <summary>Неверных кодов ссылки с одного адреса за <see cref="LockWindow"/> до паузы (429): подбор кода (160 бит) и так невозможен.</summary>
    public const int MaxLinkMisses = 30;

    private readonly IDataProtector _protector = protection.CreateProtector("OneBase.Files.ConnectionPassword");
    private readonly IDataProtector _links = protection.CreateProtector("OneBase.Files.ConnectionLink");

    private sealed record AuthEntry(Guid ConnectionId, string Username, Guid DepartmentId, AccessLevel Access, string Fingerprint);

    /// <summary>Найденная ссылка: подключение отдела (DepartmentId) или личная ссылка сотрудника (UserId); ExpiresAt — срок.</summary>
    private sealed record LinkEntry(Guid Id, Guid? DepartmentId, string? Username, AccessLevel Access, Guid? UserId, DateTimeOffset? ExpiresAt);

    public async Task<ConnectionView> GetAsync(FileActor actor, Department department, CancellationToken ct)
    {
        var connection = await LatestAsync(department.Id, ct);
        var canView = FileAccessRules.CanSeeConnection(actor, department.Id);
        var canManage = FileAccessRules.CanManageConnection(actor);
        var status = connection is null ? "none" : connection.RevokedAt is null ? "active" : "revoked";
        var address = options.FolderUrl(department.Code);
        var active = status == "active" && canView;
        var username = active ? connection!.Username : null;
        if (status == "active" && connection!.LinkTokenHash is null)
        {
            // Подключения, созданные до ссылок, получают её при первом открытии окна.
            await IssueLinkAsync(connection.Id, onlyIfMissing: true, ct);
            await audit.LogAsync(ActorType.User, actor.UserId?.ToString() ?? "unknown", "files.connection.link.created", "FileConnection", connection.Id.ToString(),
                new { department = department.Code, connection.Username }, ct);
        }

        return new ConnectionView(
            status,
            ConnectionType,
            address,
            options.UncPath(department.Code),
            username,
            connection?.Access == AccessLevel.Read ? "read" : "write",
            connection?.CreatedAt,
            canView ? connection?.LastUsedAt : null,
            canView ? connection?.PasswordChangedAt : null,
            connection?.RevokedAt,
            address is not null,
            canView,
            canManage,
            active && address is not null ? Guide(department) : [],
            Notes(department, address, username),
            active ? options.LinkUrl(Masked, department.Code) : null,
            active ? connection!.LinkExpiresAt : null,
            await SharedOfDepartmentAsync(department, active ? Masked : null, ct));
    }

    /// <summary>Общие папки, выданные отделу (уровень — не выше записи), с адресом по ссылке отдела (token = null — без адреса).</summary>
    private async Task<IReadOnlyList<SharedLinkRef>> SharedOfDepartmentAsync(Department department, string? token, CancellationToken ct)
    {
        var grants = await files.DepartmentSharedGrantsAsync(department.Id, ct);
        var ids = grants.Select(g => g.RootId).ToList();
        var names = await db.Folders.AsNoTracking().Where(f => ids.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.Name, ct);
        return grants.GroupBy(g => g.RootId).Where(g => names.ContainsKey(g.Key))
            .Select(g => new SharedLinkRef(names[g.Key], g.Max(x => x.Access) >= AccessLevel.Write ? "write" : "read",
                token is null ? null : options.LinkUrl(token, names[g.Key])))
            .OrderBy(r => r.Name)
            .ToList();
    }

    public async Task<ConnectionView> CreateAsync(FileActor actor, Department department, CancellationToken ct)
    {
        RequireManage(actor);
        if (await db.FileConnections.AnyAsync(c => c.DepartmentId == department.Id && c.RevokedAt == null, ct))
        {
            throw new FilesException(409, "У отдела уже есть подключение — смените пароль или отзовите его.");
        }

        var password = ConnectionCredentials.NewPassword();
        var token = ConnectionCredentials.NewLinkToken();
        var connection = new FileConnection
        {
            DepartmentId = department.Id,
            Username = await FreeUsernameAsync(department.Code, ct),
            PasswordHash = ConnectionCredentials.Hash(password),
            ProtectedPassword = _protector.Protect(password),
            LinkTokenHash = ConnectionCredentials.LinkHash(token),
            ProtectedLinkToken = _links.Protect(token),
            LinkChangedAt = DateTimeOffset.UtcNow,
            LinkExpiresAt = options.LinkExpiry(DateTimeOffset.UtcNow),
            CreatedById = actor.UserId,
        };
        db.FileConnections.Add(connection);
        await db.SaveChangesAsync(ct);
        await files.RootAsync(department, ct);
        await audit.LogAsync(ActorType.User, actor.UserId?.ToString() ?? "unknown", "files.connection.created", "FileConnection", connection.Id.ToString(),
            new { department = department.Code, connection.Username }, ct);
        return await GetAsync(actor, department, ct);
    }

    public async Task<ConnectionView> RegenerateAsync(FileActor actor, Department department, CancellationToken ct)
    {
        RequireManage(actor);
        var connection = await ActiveAsync(department.Id, ct) ?? throw FilesException.NotFound("У отдела нет действующего подключения.");
        var password = ConnectionCredentials.NewPassword();
        connection.PasswordHash = ConnectionCredentials.Hash(password);
        connection.ProtectedPassword = _protector.Protect(password);
        connection.PasswordChangedAt = DateTimeOffset.UtcNow;
        connection.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        Forget(connection.Username); // старый пароль перестаёт работать сразу, без ожидания кэша
        await audit.LogAsync(ActorType.User, actor.UserId?.ToString() ?? "unknown", "files.connection.regenerated", "FileConnection", connection.Id.ToString(),
            new { department = department.Code, connection.Username }, ct);
        return await GetAsync(actor, department, ct);
    }

    public async Task<ConnectionView> RevokeAsync(FileActor actor, Department department, CancellationToken ct)
    {
        RequireManage(actor);
        var connection = await ActiveAsync(department.Id, ct) ?? throw FilesException.NotFound("У отдела нет действующего подключения.");
        var link = await CurrentLinkHashAsync(connection.Id, ct);
        connection.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        Forget(connection.Username);
        ForgetLink(link);
        await audit.LogAsync(ActorType.User, actor.UserId?.ToString() ?? "unknown", "files.connection.revoked", "FileConnection", connection.Id.ToString(),
            new { department = department.Code, connection.Username }, ct);
        return await GetAsync(actor, department, ct);
    }

    /// <summary>Пароль для «Показать» и «Скопировать»: сотрудникам с записью в отделе и администраторам; факт показа — в журнал (без пароля).</summary>
    public async Task<string> RevealAsync(FileActor actor, Department department, CancellationToken ct)
    {
        if (!FileAccessRules.CanSeeConnection(actor, department.Id))
        {
            throw FilesException.Forbidden();
        }

        var connection = await ActiveAsync(department.Id, ct) ?? throw FilesException.NotFound("У отдела нет действующего подключения.");
        var password = _protector.Unprotect(connection.ProtectedPassword);
        await audit.LogAsync(ActorType.User, actor.UserId?.ToString() ?? "unknown", "files.connection.revealed", "FileConnection", connection.Id.ToString(),
            new { department = department.Code, connection.Username }, ct);
        return password;
    }

    /// <summary>Секретная ссылка для «Показать» и «Скопировать»: тем же, кому виден пароль; факт показа — в журнал (без кода).</summary>
    public async Task<ConnectionLink> RevealLinkAsync(FileActor actor, Department department, CancellationToken ct)
    {
        if (!FileAccessRules.CanSeeConnection(actor, department.Id))
        {
            throw FilesException.Forbidden();
        }

        // Без отслеживания: ссылку меняет ExecuteUpdate, и запись из кэша контекста показала бы старую.
        var connection = await db.FileConnections.AsNoTracking().FirstOrDefaultAsync(c => c.DepartmentId == department.Id && c.RevokedAt == null, ct)
                         ?? throw FilesException.NotFound("У отдела нет действующего подключения.");
        var token = connection.ProtectedLinkToken is { } stored ? _links.Unprotect(stored) : await IssueLinkAsync(connection.Id, onlyIfMissing: false, ct);
        var address = options.LinkUrl(token, department.Code)
                      ?? throw new FilesException(409, "Адрес подключения не настроен: администратору нужно задать FILE_STORAGE_DOMAIN.");
        await audit.LogAsync(ActorType.User, actor.UserId?.ToString() ?? "unknown", "files.connection.link.revealed", "FileConnection", connection.Id.ToString(),
            new { department = department.Code, connection.Username }, ct);
        var shared = (await SharedOfDepartmentAsync(department, null, ct))
            .Select(r => new SharedLinkAddress(r.Name, options.LinkUrl(token, r.Name)!, options.LinkUncPath(token, r.Name))).ToList();
        return new ConnectionLink(address, options.LinkUncPath(token, department.Code), shared);
    }

    /// <summary>Новая ссылка: старая перестаёт работать сразу (кэш входа сбрасывается), компьютеры подключают диск по новой.</summary>
    public async Task<ConnectionView> RegenerateLinkAsync(FileActor actor, Department department, CancellationToken ct)
    {
        RequireManage(actor);
        var connection = await ActiveAsync(department.Id, ct) ?? throw FilesException.NotFound("У отдела нет действующего подключения.");
        var old = await CurrentLinkHashAsync(connection.Id, ct);
        await IssueLinkAsync(connection.Id, onlyIfMissing: false, ct);
        ForgetLink(old);
        await audit.LogAsync(ActorType.User, actor.UserId?.ToString() ?? "unknown", "files.connection.link.regenerated", "FileConnection", connection.Id.ToString(),
            new { department = department.Code, connection.Username }, ct);
        return await GetAsync(actor, department, ct);
    }

    /// <summary>
    /// Вход WebDAV по секретной ссылке: подключение отдела по хэшу кода. Найденное и ненайденное ненадолго кэшируются — Проводник и Office
    /// делают десятки запросов в секунду, а компьютер со старой ссылкой повторяет их без конца. Подобрать код (160 бит) нельзя.
    /// </summary>
    /// <remarks>
    /// Ссылка отдела — подключение отдела (его папка и выданные отделу общие папки). Личная ссылка — сам сотрудник: его права читаются
    /// из базы при каждом запросе (отключили, сменили роль, забрали доступ — действует сразу). Истёкшая или отозванная ссылка — как
    /// неверная. Неверных кодов с одного адреса больше <see cref="MaxLinkMisses"/> за 5 минут — пауза (Locked → 429).
    /// </remarks>
    public async Task<DavAuthResult> AuthenticateLinkAsync(string token, string remote, CancellationToken ct)
    {
        var missKey = $"files-dav-linkmiss:{remote}";
        if (cache.TryGetValue(missKey, out int misses) && misses >= MaxLinkMisses)
        {
            return new DavAuthResult(DavAuthStatus.Locked, null);
        }

        var hash = ConnectionCredentials.LinkHash(token);
        LinkEntry? entry = null;
        if (hash is not null && !cache.TryGetValue(LinkKey(hash), out entry))
        {
            entry = await FindLinkAsync(hash, ct);
            cache.Set(LinkKey(hash), entry, entry is null ? LinkMissTtl : AuthCacheTtl);
        }

        if (entry is null || entry.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            cache.Set(missKey, misses + 1, LockWindow);
            return new DavAuthResult(DavAuthStatus.Failed, null);
        }

        if (entry.UserId is { } userId)
        {
            var user = await files.UserActorAsync(userId, ct);
            if (user is null || !FileAccessRules.CanHavePersonalLink(user))
            {
                return new DavAuthResult(DavAuthStatus.Failed, null);
            }

            await TouchPersonalAsync(entry.Id, ct);
            return new DavAuthResult(DavAuthStatus.Ok, user with { Source = FileSources.WindowsPersonal, PersonalLinkId = entry.Id });
        }

        await TouchAsync(entry.Id, ct);
        var shared = await files.DepartmentSharedGrantsAsync(entry.DepartmentId!.Value, ct);
        return new DavAuthResult(DavAuthStatus.Ok,
            FileActor.Connection(entry.Id, entry.Username!, entry.DepartmentId.Value, entry.Access, shared) with { Source = FileSources.WindowsLink });
    }

    private async Task<LinkEntry?> FindLinkAsync(string hash, CancellationToken ct)
    {
        var connection = await db.FileConnections.AsNoTracking().FirstOrDefaultAsync(c => c.LinkTokenHash == hash && c.RevokedAt == null, ct);
        if (connection is not null)
        {
            return new LinkEntry(connection.Id, connection.DepartmentId, connection.Username, connection.Access, null, connection.LinkExpiresAt);
        }

        var personal = await db.FileUserLinks.AsNoTracking().FirstOrDefaultAsync(l => l.LinkTokenHash == hash && l.RevokedAt == null, ct);
        return personal is null ? null : new LinkEntry(personal.Id, null, null, AccessLevel.Write, personal.UserId, personal.ExpiresAt);
    }

    /// <summary>Вход WebDAV по логину и паролю (Basic). Неверный пароль — счётчик по адресу и логину; после 10 ошибок — пауза 5 минут.</summary>
    public async Task<DavAuthResult> AuthenticateAsync(string username, string password, string remote, CancellationToken ct)
    {
        var login = username.Trim().ToLowerInvariant();
        var failKey = $"files-dav-fail:{remote}:{login}";
        if (cache.TryGetValue(failKey, out int failures) && failures >= MaxFailures)
        {
            return new DavAuthResult(DavAuthStatus.Locked, null);
        }

        var fingerprint = ConnectionCredentials.Fingerprint(login, password);
        if (cache.TryGetValue(AuthKey(login), out AuthEntry? entry) && entry is not null && entry.Fingerprint == fingerprint)
        {
            return new DavAuthResult(DavAuthStatus.Ok, await LoginActorAsync(entry.ConnectionId, entry.Username, entry.DepartmentId, entry.Access, ct));
        }

        var connection = await db.FileConnections.AsNoTracking().FirstOrDefaultAsync(c => c.Username == login && c.RevokedAt == null, ct);
        if (connection is null || !ConnectionCredentials.Verify(connection.PasswordHash, password))
        {
            cache.Set(failKey, failures + 1, LockWindow);
            return new DavAuthResult(DavAuthStatus.Failed, null);
        }

        cache.Remove(failKey);
        cache.Set(AuthKey(login), new AuthEntry(connection.Id, connection.Username, connection.DepartmentId, connection.Access, fingerprint), AuthCacheTtl);
        return new DavAuthResult(DavAuthStatus.Ok, await LoginActorAsync(connection.Id, connection.Username, connection.DepartmentId, connection.Access, ct));
    }

    /// <summary>Подключение по логину: папка отдела и общие папки, выданные отделу (выдачи читаются при каждом запросе).</summary>
    private async Task<FileActor> LoginActorAsync(Guid connectionId, string username, Guid departmentId, AccessLevel access, CancellationToken ct) =>
        FileActor.Connection(connectionId, username, departmentId, access, await files.DepartmentSharedGrantsAsync(departmentId, ct)) with
        {
            Source = FileSources.WindowsLogin,
        };

    /// <summary>«Последняя активность» личной ссылки — не чаще раза в минуту.</summary>
    private async Task TouchPersonalAsync(Guid linkId, CancellationToken ct)
    {
        if (cache.TryGetValue($"files-dav-touch:{linkId}", out _))
        {
            return;
        }

        cache.Set($"files-dav-touch:{linkId}", true, TouchEvery);
        var now = DateTimeOffset.UtcNow;
        await db.FileUserLinks.Where(l => l.Id == linkId).ExecuteUpdateAsync(s => s.SetProperty(l => l.LastUsedAt, now), ct);
    }

    // ---------- Личная ссылка Windows ----------

    public async Task<PersonalLinkView> MyLinkAsync(FileActor actor, CancellationToken ct)
    {
        RequirePersonal(actor);
        var link = await db.FileUserLinks.AsNoTracking().Where(l => l.UserId == actor.UserId && l.RevokedAt == null).FirstOrDefaultAsync(ct);
        var folders = (await files.ReadableDepartmentsAsync(actor, ct)).Select(d => d.Code)
            .Concat((await files.ReadableSharedAsync(actor, ct)).Select(r => r.Name)).ToList();
        var configured = options.PublicRoot() is not null;
        var status = link is null ? "none" : link.ExpiresAt <= DateTimeOffset.UtcNow ? "expired" : "active";
        return new PersonalLinkView(status, status == "active" ? options.LinkUrl(Masked, "") : null, link?.CreatedAt, link?.ExpiresAt, link?.LastUsedAt,
            configured, folders, status == "active" && configured ? PersonalGuide() : [], PersonalNotes(configured));
    }

    /// <summary>Выпустить личную ссылку (или новую вместо прежней — прежняя перестаёт работать сразу).</summary>
    public async Task<PersonalLinkView> IssueMyLinkAsync(FileActor actor, CancellationToken ct)
    {
        RequirePersonal(actor);
        var userId = actor.UserId!.Value;
        var now = DateTimeOffset.UtcNow;
        var old = await db.FileUserLinks.Where(l => l.UserId == userId && l.RevokedAt == null).ToListAsync(ct);
        foreach (var link in old)
        {
            link.RevokedAt = now;
            ForgetLink(link.LinkTokenHash);
        }

        await db.SaveChangesAsync(ct);
        var token = ConnectionCredentials.NewLinkToken();
        var created = new FileUserLink
        {
            UserId = userId,
            LinkTokenHash = ConnectionCredentials.LinkHash(token)!,
            ProtectedLinkToken = _links.Protect(token),
            ExpiresAt = options.LinkExpiry(now),
        };
        db.FileUserLinks.Add(created);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), old.Count > 0 ? "files.personal_link.regenerated" : "files.personal_link.created",
            "FileUserLink", created.Id.ToString(), new { until = created.ExpiresAt, source = actor.Source, ip = actor.Ip }, ct);
        return await MyLinkAsync(actor, ct);
    }

    /// <summary>Личная ссылка для «Показать» и «Скопировать» — только самому сотруднику; факт показа — в журнал (без кода).</summary>
    public async Task<ConnectionLink> RevealMyLinkAsync(FileActor actor, CancellationToken ct)
    {
        RequirePersonal(actor);
        var link = await db.FileUserLinks.AsNoTracking().FirstOrDefaultAsync(l => l.UserId == actor.UserId && l.RevokedAt == null, ct)
                   ?? throw FilesException.NotFound("Личной ссылки нет — выпустите её.");
        if (link.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new FilesException(410, "Срок личной ссылки истёк — выпустите новую.");
        }

        var token = _links.Unprotect(link.ProtectedLinkToken);
        var address = options.LinkUrl(token, "")
                      ?? throw new FilesException(409, "Адрес подключения не настроен: администратору нужно задать FILE_STORAGE_DOMAIN.");
        await audit.LogAsync(ActorType.User, actor.UserId!.Value.ToString(), "files.personal_link.revealed", "FileUserLink", link.Id.ToString(),
            new { source = actor.Source, ip = actor.Ip }, ct);
        var shared = (await files.ReadableSharedAsync(actor, ct))
            .Select(r => new SharedLinkAddress(r.Name, options.LinkUrl(token, r.Name)!, options.LinkUncPath(token, r.Name))).ToList();
        return new ConnectionLink(address, options.LinkUncPath(token, ""), shared);
    }

    public async Task<PersonalLinkView> RevokeMyLinkAsync(FileActor actor, CancellationToken ct)
    {
        RequirePersonal(actor);
        var now = DateTimeOffset.UtcNow;
        foreach (var link in await db.FileUserLinks.Where(l => l.UserId == actor.UserId && l.RevokedAt == null).ToListAsync(ct))
        {
            link.RevokedAt = now;
            ForgetLink(link.LinkTokenHash);
            await audit.LogAsync(ActorType.User, actor.UserId!.Value.ToString(), "files.personal_link.revoked", "FileUserLink", link.Id.ToString(),
                new { source = actor.Source, ip = actor.Ip }, ct);
        }

        await db.SaveChangesAsync(ct);
        return await MyLinkAsync(actor, ct);
    }

    private static void RequirePersonal(FileActor actor)
    {
        if (!FileAccessRules.CanHavePersonalLink(actor) || actor.UserId is null)
        {
            throw new FilesException(403, "Личная ссылка Windows — для сотрудников с доступом к файлам.");
        }
    }

    private static IReadOnlyList<string> PersonalGuide() =>
    [
        "Откройте Проводник и выберите «Этот компьютер».",
        "Нажмите «Подключить сетевой диск» (вверху окна; в Windows 11 — в меню «…»).",
        "Выберите свободную букву диска, например Y:.",
        "В поле «Папка» вставьте личный адрес: «Скопировать» у поля «Личный адрес».",
        "Отметьте «Восстанавливать подключение при входе в систему» и нажмите «Готово» — логин и пароль не нужны.",
        "На диске — папки ваших отделов и общие папки, к которым вам выдан доступ; Word и Excel открывают и сохраняют файлы прямо с диска.",
    ];

    private static IReadOnlyList<string> PersonalNotes(bool configured) =>
    [
        "Личный адрес работает как пароль и даёт ровно ваши права: не пересылайте его. Попал не туда — нажмите «Новая ссылка», прежняя перестанет работать сразу.",
        "Права проверяются при каждом обращении: если вас отключат или заберут доступ к папке, диск сразу покажет меньше или перестанет открываться.",
        "Пока файл открыт в Word или Excel, у других он открывается только для чтения — правки двух людей не перезапишут друг друга.",
        .. configured ? Array.Empty<string>() : ["Адрес подключения не настроен: администратору нужно задать FILE_STORAGE_DOMAIN."],
    ];

    /// <summary>«Последняя активность» — не чаще раза в минуту на подключение (WebDAV делает десятки запросов в секунду).</summary>
    public async Task TouchAsync(Guid connectionId, CancellationToken ct)
    {
        if (cache.TryGetValue($"files-dav-touch:{connectionId}", out _))
        {
            return;
        }

        cache.Set($"files-dav-touch:{connectionId}", true, TouchEvery);
        var now = DateTimeOffset.UtcNow;
        await db.FileConnections.Where(c => c.Id == connectionId).ExecuteUpdateAsync(s => s.SetProperty(c => c.LastUsedAt, now), ct);
    }

    /// <summary>
    /// Проверка подключения: адрес настроен, подключение действует, хранилище пишет и читает, папка отдела есть в базе и — главное —
    /// публичный адрес WebDAV отвечает на PROPFIND с логином и паролем отдела (тот же путь, что у Windows: Cloudflare → nginx → API).
    /// </summary>
    public async Task<ConnectionTestResult> TestAsync(FileActor actor, Department department, CancellationToken ct)
    {
        if (!FileAccessRules.CanSeeConnection(actor, department.Id))
        {
            throw FilesException.Forbidden();
        }

        var steps = new List<ConnectionTestStep>();
        var url = options.FolderUrl(department.Code);
        steps.Add(url is null
            ? new ConnectionTestStep("Адрес подключения", false, "Не задан домен (переменная FILE_STORAGE_DOMAIN → Files:PublicDomain) — Windows подключать не к чему.")
            : new ConnectionTestStep("Адрес подключения", true, url));

        var connection = await ActiveAsync(department.Id, ct);
        steps.Add(connection is null
            ? new ConnectionTestStep("Логин отдела", false, "Подключение не создано или отозвано.")
            : new ConnectionTestStep("Логин отдела", true, connection.Username));

        steps.Add(await StorageStepAsync(ct));

        var root = await files.RootAsync(department, ct);
        var folders = await db.Folders.CountAsync(f => f.DepartmentId == department.Id && f.DeletedAt == null, ct);
        steps.Add(new ConnectionTestStep("База данных", true, $"Папка отдела «{root.Name}»: папок — {folders}."));

        if (url is not null && connection is not null)
        {
            steps.Add(await WebDavStepAsync(url, connection, ct));
            var token = connection.ProtectedLinkToken is { } stored ? _links.Unprotect(stored) : await IssueLinkAsync(connection.Id, onlyIfMissing: false, ct);
            steps.Add(await LinkStepAsync(options.LinkUrl(token, department.Code)!, department, ct));
        }
        else
        {
            steps.Add(new ConnectionTestStep("WebDAV по адресу", false, "Не проверялся: нет адреса или логина."));
        }

        await audit.LogAsync(ActorType.User, actor.UserId?.ToString() ?? "unknown", "files.connection.tested", "Department", department.Id.ToString(),
            new { department = department.Code, ok = steps.All(s => s.Ok) }, ct);
        return new ConnectionTestResult(steps.All(s => s.Ok), steps);
    }

    private async Task<ConnectionTestStep> StorageStepAsync(CancellationToken ct)
    {
        var key = $"health/files-test-{Guid.NewGuid():N}";
        var payload = Encoding.UTF8.GetBytes("onebase-files-test");
        try
        {
            await storage.PutAsync(key, new MemoryStream(payload), payload.Length, "text/plain", ct);
            using var read = new MemoryStream();
            await storage.DownloadAsync(key, read, ct);
            await storage.DeleteAsync(key, ct);
            return read.ToArray().SequenceEqual(payload)
                ? new ConnectionTestStep("Хранилище (MinIO)", true, "Запись, чтение и удаление проверочного объекта прошли.")
                : new ConnectionTestStep("Хранилище (MinIO)", false, "Прочитанное не совпало с записанным.");
        }
        catch (Exception e)
        {
            return new ConnectionTestStep("Хранилище (MinIO)", false, $"Ошибка хранилища: {e.GetType().Name}.");
        }
    }

    private async Task<ConnectionTestStep> WebDavStepAsync(string url, FileConnection connection, CancellationToken ct)
    {
        try
        {
            var password = _protector.Unprotect(connection.ProtectedPassword);
            using var client = httpClients.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);
            using var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), url.TrimEnd('/') + "/");
            request.Headers.Add("Depth", "0");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{connection.Username}:{password}")));
            using var response = await client.SendAsync(request, ct);
            return response.StatusCode == HttpStatusCode.MultiStatus
                ? new ConnectionTestStep("WebDAV по адресу", true, $"{url} ответил 207: папка отдела открывается с логином {connection.Username}.")
                : new ConnectionTestStep("WebDAV по адресу", false, $"{url} ответил {(int)response.StatusCode} {response.ReasonPhrase} вместо 207 — проверьте маршрут Cloudflare → nginx:82.");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return new ConnectionTestStep("WebDAV по адресу", false, $"{url} не отвечает ({e.GetType().Name}) — проверьте домен в Cloudflare и nginx:82.");
        }
    }

    private async Task<ConnectionTestStep> LinkStepAsync(string url, Department department, CancellationToken ct)
    {
        try
        {
            using var client = httpClients.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);
            using var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), url.TrimEnd('/') + "/");
            request.Headers.Add("Depth", "0");
            using var response = await client.SendAsync(request, ct);
            return response.StatusCode == HttpStatusCode.MultiStatus
                ? new ConnectionTestStep("Адрес без пароля", true, $"Ссылка отдела «{department.Name}» открывается без логина и пароля (207) — Word и Excel откроют файлы с диска.")
                : new ConnectionTestStep("Адрес без пароля", false, $"Ссылка ответила {(int)response.StatusCode} {response.ReasonPhrase} вместо 207 — проверьте маршрут Cloudflare → nginx:82.");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return new ConnectionTestStep("Адрес без пароля", false, $"Ссылка не отвечает ({e.GetType().Name}) — проверьте домен в Cloudflare и nginx:82.");
        }
    }

    /// <summary>Как подключить диск по ссылке — без логина и пароля. Сама ссылка в инструкцию не пишется: она выдаётся по «Скопировать».</summary>
    private static IReadOnlyList<string> Guide(Department department) =>
    [
        "Откройте Проводник и выберите «Этот компьютер».",
        "Нажмите «Подключить сетевой диск» (вверху окна; в Windows 11 — в меню «…»).",
        "Выберите свободную букву диска, например Z:.",
        "В поле «Папка» вставьте адрес без пароля: в окне OneBase «Подключить к Windows» — «Скопировать» у поля «Адрес без пароля».",
        "Отметьте «Восстанавливать подключение при входе в систему» и нажмите «Готово» — логин и пароль не нужны.",
        $"Диск Z: с папкой отдела «{department.Name}» появится в «Этом компьютере». Файлы, положенные туда, сразу видны в OneBase; Word и Excel открывают и сохраняют их прямо с диска.",
    ];

    private IReadOnlyList<string> Notes(Department department, string? address, string? username) =>
    [
        "Адрес без пароля работает как пароль: не пересылайте его посторонним. Если он попал не туда, администратор нажимает «Новая ссылка» — старая перестаёт работать сразу, а диск на компьютерах отдела подключают по новой.",
        "Если Windows пишет «Указанное имя папки недействительно», запустите службу «Веб-клиент» (WebClient): Win+R → services.msc → «Веб-клиент» → «Запустить», тип запуска — «Автоматически».",
        .. address is not null && username is not null
            ? [$"Вход по логину {username} и паролю ({address}) тоже работает, но Word и Excel из Microsoft 365 по нему файлы не откроют («заблокировал доступ… метод входа, который может быть небезопасным») — подключайте диск по адресу без пароля."]
            : Array.Empty<string>(),
        .. address is null ? ["Адрес подключения не настроен: администратору нужно задать FILE_STORAGE_DOMAIN."] : Array.Empty<string>(),
        @"Windows по умолчанию открывает через сетевую папку файлы до 50 МБ. Чтобы поднять предел, выполните в PowerShell от имени администратора: reg add HKLM\SYSTEM\CurrentControlSet\Services\WebClient\Parameters /v FileSizeLimitInBytes /t REG_DWORD /d 4294967295 /f; Restart-Service WebClient",
        "Через сетевую папку проходит файл до 100 МБ (ограничение Cloudflare) — большие файлы загружайте на сайте OneBase.",
        "Файлы, загруженные на сайте, появятся в Проводнике после обновления папки (F5).",
    ];

    private Task<FileConnection?> ActiveAsync(Guid departmentId, CancellationToken ct) =>
        db.FileConnections.FirstOrDefaultAsync(c => c.DepartmentId == departmentId && c.RevokedAt == null, ct);

    private Task<FileConnection?> LatestAsync(Guid departmentId, CancellationToken ct) =>
        db.FileConnections.AsNoTracking().Where(c => c.DepartmentId == departmentId)
            .OrderBy(c => c.RevokedAt == null ? 0 : 1).ThenByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);

    private async Task<string> FreeUsernameAsync(string code, CancellationToken ct)
    {
        for (var i = 0; i < 20; i++)
        {
            var candidate = ConnectionCredentials.NewUsername(code);
            if (!await db.FileConnections.AnyAsync(c => c.Username == candidate, ct))
            {
                return candidate;
            }
        }

        throw new FilesException(500, "Не удалось подобрать свободный логин — попробуйте ещё раз.");
    }

    private static void RequireManage(FileActor actor)
    {
        if (!FileAccessRules.CanManageConnection(actor))
        {
            throw new FilesException(403, "Подключения к Windows создаёт и меняет администратор.");
        }
    }

    private void Forget(string username) => cache.Remove(AuthKey(username.ToLowerInvariant()));

    private static string AuthKey(string login) => $"files-dav-auth:{login}";

    private void ForgetLink(string? hash)
    {
        if (hash is not null)
        {
            cache.Remove(LinkKey(hash));
        }
    }

    private static string LinkKey(string hash) => $"files-dav-link:{hash}";

    /// <summary>Хэш ссылки прямо из базы: ссылку меняет ExecuteUpdate, и запись из кэша контекста могла бы быть старой.</summary>
    private Task<string?> CurrentLinkHashAsync(Guid connectionId, CancellationToken ct) =>
        db.FileConnections.AsNoTracking().Where(c => c.Id == connectionId).Select(c => c.LinkTokenHash).FirstOrDefaultAsync(ct);

    /// <summary>Выпускает код ссылки (onlyIfMissing — только если ссылки ещё нет: два одновременных открытия окна не перебьют друг друга).</summary>
    private async Task<string> IssueLinkAsync(Guid connectionId, bool onlyIfMissing, CancellationToken ct)
    {
        var token = ConnectionCredentials.NewLinkToken();
        var hash = ConnectionCredentials.LinkHash(token)!;
        var protectedToken = _links.Protect(token);
        var now = DateTimeOffset.UtcNow;
        var query = db.FileConnections.Where(c => c.Id == connectionId);
        if (onlyIfMissing)
        {
            query = query.Where(c => c.LinkTokenHash == null);
        }

        var expires = options.LinkExpiry(now);
        await query.ExecuteUpdateAsync(s => s
            .SetProperty(c => c.LinkTokenHash, hash)
            .SetProperty(c => c.ProtectedLinkToken, protectedToken)
            .SetProperty(c => c.LinkChangedAt, now)
            .SetProperty(c => c.LinkExpiresAt, expires), ct);
        return token;
    }
}
