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

    private readonly IDataProtector _protector = protection.CreateProtector("OneBase.Files.ConnectionPassword");

    private sealed record AuthEntry(Guid ConnectionId, string Username, Guid DepartmentId, AccessLevel Access, string Fingerprint);

    public async Task<ConnectionView> GetAsync(FileActor actor, Department department, CancellationToken ct)
    {
        var connection = await LatestAsync(department.Id, ct);
        var canView = FileAccessRules.CanSeeConnection(actor, department.Id);
        var canManage = FileAccessRules.CanManageConnection(actor);
        var status = connection is null ? "none" : connection.RevokedAt is null ? "active" : "revoked";
        var address = options.FolderUrl(department.Code);
        var active = status == "active" && canView;
        var username = active ? connection!.Username : null;

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
            active && address is not null ? Guide(department, address, username!) : [],
            Notes(department));
    }

    public async Task<ConnectionView> CreateAsync(FileActor actor, Department department, CancellationToken ct)
    {
        RequireManage(actor);
        if (await db.FileConnections.AnyAsync(c => c.DepartmentId == department.Id && c.RevokedAt == null, ct))
        {
            throw new FilesException(409, "У отдела уже есть подключение — смените пароль или отзовите его.");
        }

        var password = ConnectionCredentials.NewPassword();
        var connection = new FileConnection
        {
            DepartmentId = department.Id,
            Username = await FreeUsernameAsync(department.Code, ct),
            PasswordHash = ConnectionCredentials.Hash(password),
            ProtectedPassword = _protector.Protect(password),
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
        connection.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        Forget(connection.Username);
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
            return new DavAuthResult(DavAuthStatus.Ok, FileActor.Connection(entry.ConnectionId, entry.Username, entry.DepartmentId, entry.Access));
        }

        var connection = await db.FileConnections.AsNoTracking().FirstOrDefaultAsync(c => c.Username == login && c.RevokedAt == null, ct);
        if (connection is null || !ConnectionCredentials.Verify(connection.PasswordHash, password))
        {
            cache.Set(failKey, failures + 1, LockWindow);
            return new DavAuthResult(DavAuthStatus.Failed, null);
        }

        cache.Remove(failKey);
        cache.Set(AuthKey(login), new AuthEntry(connection.Id, connection.Username, connection.DepartmentId, connection.Access, fingerprint), AuthCacheTtl);
        return new DavAuthResult(DavAuthStatus.Ok, FileActor.Connection(connection.Id, connection.Username, connection.DepartmentId, connection.Access));
    }

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

    private static IReadOnlyList<string> Guide(Department department, string address, string username) =>
    [
        "Откройте Проводник и выберите «Этот компьютер».",
        "Нажмите «Подключить сетевой диск» (вверху окна; в Windows 11 — в меню «…»).",
        "Выберите свободную букву диска, например Z:.",
        $"В поле «Папка» вставьте адрес: {address}",
        "Отметьте «Восстанавливать подключение при входе в систему» и «Использовать другие учётные данные», нажмите «Готово».",
        $"Введите логин {username} и пароль подключения, отметьте «Запомнить учётные данные» и нажмите «ОК».",
        $"Диск Z: с папкой отдела «{department.Name}» появится в «Этом компьютере». Файлы, положенные туда, сразу видны в OneBase.",
    ];

    private IReadOnlyList<string> Notes(Department department) =>
    [
        options.UncPath(department.Code) is { } unc ? $"Тот же адрес в виде пути Windows: {unc}" : "Адрес подключения не настроен: администратору нужно задать FILE_STORAGE_DOMAIN.",
        "Если Windows пишет «Указанное имя папки недействительно», запустите службу «Веб-клиент» (WebClient): Win+R → services.msc → «Веб-клиент» → «Запустить», тип запуска — «Автоматически».",
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
}
