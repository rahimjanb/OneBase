using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Files;
using OneBase.Application.Security;
using OneBase.Domain.Audit;
using OneBase.Domain.Files;
using OneBase.Domain.Identity;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.Files;

/// <summary>Ошибка операции с файлами: HTTP-статус и текст для пользователя.</summary>
public sealed class FilesException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;

    public static FilesException NotFound(string what = "Файл или папка не найдены.") => new(404, what);

    public static FilesException Forbidden() => new(403, "Нет прав на изменение файлов этого отдела.");

    public static FilesException Conflict(string name) => new(409, $"В папке уже есть «{name}».");

    public static FilesException Invalid(string message) => new(400, message);
}

public sealed record FileUserRef(string Name, bool ViaWindows);

public sealed record FileRow(
    Guid Id,
    Guid FolderId,
    string Name,
    string Extension,
    string ContentType,
    long SizeBytes,
    int Version,
    DateTimeOffset UpdatedAt,
    FileUserRef? UpdatedBy,
    bool Hidden);

public sealed record FolderRow(Guid Id, string Name, int Folders, int Files, DateTimeOffset UpdatedAt);

public sealed record FolderCrumb(Guid Id, string Name);

public sealed record DepartmentRef(Guid Id, string Code, string Name, string Access);

public sealed record FolderView(
    DepartmentRef Department,
    Guid Id,
    string Name,
    Guid? ParentId,
    IReadOnlyList<FolderCrumb> Path,
    IReadOnlyList<FolderRow> Folders,
    IReadOnlyList<FileRow> Files,
    bool CanWrite);

public sealed record ConnectionState(string Status, bool CanView, bool CanManage);

public sealed record DepartmentSummary(
    Guid Id,
    string Code,
    string Name,
    string Access,
    Guid RootFolderId,
    int Files,
    int Folders,
    long SizeBytes,
    DateTimeOffset? UpdatedAt,
    ConnectionState Connection);

public sealed record SearchRow(FileRow File, DepartmentRef Department, string FolderPath);

public sealed record TrashRow(Guid Id, bool IsFolder, string Name, long SizeBytes, string FolderPath, DateTimeOffset DeletedAt);

public sealed record DepartmentAccessRow(Guid Id, string Code, string Name, string Access);

public sealed record VersionRow(int Number, long SizeBytes, DateTimeOffset CreatedAt, FileUserRef? By, bool Current);

/// <summary>Узел дерева для WebDAV: папка (у корня отдела — его корневая папка) или файл.</summary>
public sealed record DavNode(Department Department, Folder? Folder, FileItem? File)
{
    public bool IsCollection => File is null;
}

/// <summary>
/// «Файлы отделов»: папки и файлы отдела в PostgreSQL, содержимое версий — в MinIO (ключ — id файла и версии, имя в ключ не входит).
/// Один сервис для сайта и для подключения Windows (WebDAV): права (FileAccessRules), версии, корзина и аудит одинаковые.
/// Удаление — в корзину (DeletedAt): содержимое остаётся, файл или папку можно восстановить.
/// </summary>
public sealed class DepartmentFilesService(OneBaseDbContext db, IFileStorage storage, IAuditLogger audit, FilesOptions options)
{
    /// <summary>Папки нового отдела: создаются вместе с корнем при первом открытии.</summary>
    public static readonly string[] DefaultFolders = ["Документы", "Отчёты", "Архив"];

    /// <summary>
    /// Пустая первая версия, которую Проводник создаёт перед записью (PUT 0 байт, затем PUT содержимого), не остаётся отдельной версией,
    /// если содержимое пришло от того же автора в течение этого времени.
    /// </summary>
    private static readonly TimeSpan EmptyPlaceholderWindow = TimeSpan.FromMinutes(2);

    // ---------- Кто работает ----------

    /// <summary>Сотрудник: права и отделы — из БД на момент запроса (роли могли смениться после входа); null — отключён или не найден.</summary>
    public async Task<FileActor?> UserActorAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking()
            .Include(u => u.Roles).ThenInclude(r => r.Role).ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is not { IsActive: true })
        {
            return null;
        }

        var permissions = user.Roles.SelectMany(r => r.Role.Permissions).Select(p => p.Code).ToHashSet();
        var own = user.Roles.Select(r => r.Role.DepartmentId).Append(user.DepartmentId).OfType<Guid>().ToHashSet();
        var userKey = user.Id.ToString();
        var roleKeys = user.Roles.Select(r => r.RoleId.ToString()).ToList();
        var departmentKeys = own.Select(d => d.ToString()).ToList();

        // Доступ к чужим отделам — выдачи на корневой папке отдела: сотруднику, его роли или его отделу.
        var grants = await (
                from p in db.ResourcePermissions.AsNoTracking()
                join f in db.Folders.AsNoTracking() on p.FolderId equals f.Id
                where f.ParentId == null && f.DepartmentId != null
                      && ((p.PrincipalType == PrincipalType.User && p.PrincipalId == userKey)
                          || (p.PrincipalType == PrincipalType.Role && roleKeys.Contains(p.PrincipalId))
                          || (p.PrincipalType == PrincipalType.Department && departmentKeys.Contains(p.PrincipalId)))
                select new DepartmentGrant(f.DepartmentId!.Value, p.Access))
            .ToListAsync(ct);

        return new FileActor(user.Id, null, user.FullName,
            permissions.Contains(Permissions.FilesRead), permissions.Contains(Permissions.FilesWrite), permissions.Contains(Permissions.FilesManage),
            own, grants, CanReadAll: permissions.Contains(Permissions.FilesReadAll));
    }

    // ---------- Отделы ----------

    public async Task<IReadOnlyList<DepartmentSummary>> DepartmentsAsync(FileActor actor, CancellationToken ct)
    {
        var departments = (await db.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync(ct))
            .Where(d => FileAccessRules.CanRead(actor, d.Id))
            .ToList();
        var ids = departments.Select(d => d.Id).ToList();

        var files = await db.Files.AsNoTracking()
            .Where(f => f.DepartmentId != null && ids.Contains(f.DepartmentId.Value) && f.DeletedAt == null)
            .GroupBy(f => f.DepartmentId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count(), Size = g.Sum(f => f.SizeBytes), Updated = g.Max(f => f.UpdatedAt ?? f.CreatedAt) })
            .ToDictionaryAsync(x => x.Id, ct);
        var folders = await db.Folders.AsNoTracking()
            .Where(f => f.DepartmentId != null && ids.Contains(f.DepartmentId.Value) && f.ParentId != null && f.DeletedAt == null)
            .GroupBy(f => f.DepartmentId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        var connections = await db.FileConnections.AsNoTracking()
            .Where(c => ids.Contains(c.DepartmentId))
            .GroupBy(c => c.DepartmentId)
            .Select(g => new { Id = g.Key, Active = g.Any(c => c.RevokedAt == null) })
            .ToDictionaryAsync(x => x.Id, x => x.Active, ct);

        var result = new List<DepartmentSummary>();
        foreach (var d in departments)
        {
            var root = await RootAsync(d, ct);
            var stats = files.GetValueOrDefault(d.Id);
            var status = connections.TryGetValue(d.Id, out var active) ? active ? "active" : "revoked" : "none";
            result.Add(new DepartmentSummary(d.Id, d.Code, d.Name, AccessName(FileAccessRules.AccessTo(actor, d.Id)), root.Id,
                stats?.Count ?? 0, folders.GetValueOrDefault(d.Id), stats?.Size ?? 0, stats?.Updated,
                new ConnectionState(status, FileAccessRules.CanSeeConnection(actor, d.Id), FileAccessRules.CanManageConnection(actor))));
        }

        return result;
    }

    public async Task<Department> DepartmentAsync(FileActor actor, string code, CancellationToken ct)
    {
        var department = await db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Code == code.ToLower(), ct);
        return department is not null && FileAccessRules.CanRead(actor, department.Id) ? department : throw FilesException.NotFound("Отдел не найден.");
    }

    /// <summary>Корневая папка отдела; при первом обращении создаётся вместе с папками по умолчанию.</summary>
    public async Task<Folder> RootAsync(Department department, CancellationToken ct)
    {
        var root = await db.Folders.FirstOrDefaultAsync(f => f.ParentId == null && f.DepartmentId == department.Id, ct);
        if (root is not null)
        {
            return root;
        }

        root = new Folder { Name = department.Name, DepartmentId = department.Id };
        db.Folders.Add(root);
        foreach (var name in DefaultFolders)
        {
            db.Folders.Add(new Folder { Name = name, Parent = root, DepartmentId = department.Id });
        }

        try
        {
            await db.SaveChangesAsync(ct);
            return root;
        }
        catch (DbUpdateException)
        {
            // Корень одновременно создал другой запрос (уникальный индекс IX_Folders_DepartmentRoot) — берём его.
            db.ChangeTracker.Clear();
            return await db.Folders.FirstAsync(f => f.ParentId == null && f.DepartmentId == department.Id, ct);
        }
    }

    // ---------- Папки ----------

    public async Task<FolderView> FolderAsync(FileActor actor, Guid folderId, CancellationToken ct)
    {
        var folder = await ReadableFolderAsync(actor, folderId, ct);
        var department = await db.Departments.AsNoTracking().FirstAsync(d => d.Id == folder.DepartmentId, ct);
        var access = FileAccessRules.AccessTo(actor, department.Id);

        var children = await db.Folders.AsNoTracking()
            .Where(f => f.ParentId == folder.Id && f.DeletedAt == null)
            .Select(f => new
            {
                f.Id,
                f.Name,
                Folders = f.Children.Count(c => c.DeletedAt == null),
                Files = f.Files.Count(x => x.DeletedAt == null),
                Updated = f.UpdatedAt ?? f.CreatedAt,
            })
            .OrderBy(f => f.Name)
            .ToListAsync(ct);
        var files = await db.Files.AsNoTracking().Where(f => f.FolderId == folder.Id && f.DeletedAt == null).OrderBy(f => f.Name).ToListAsync(ct);

        return new FolderView(
            new DepartmentRef(department.Id, department.Code, department.Name, AccessName(access)),
            folder.Id,
            folder.ParentId is null ? department.Name : folder.Name,
            folder.ParentId,
            await PathAsync(folder, department, ct),
            children.Select(c => new FolderRow(c.Id, c.Name, c.Folders, c.Files, c.Updated)).ToList(),
            await RowsAsync(files, ct),
            access >= AccessLevel.Write);
    }

    public async Task<Folder> CreateFolderAsync(FileActor actor, Guid parentId, string name, CancellationToken ct)
    {
        var parent = await WritableFolderAsync(actor, parentId, ct);
        name = ValidName(name);
        await EnsureFreeAsync(parent.Id, name, null, ct);

        var folder = new Folder { Name = name, ParentId = parent.Id, DepartmentId = parent.DepartmentId, CreatedById = actor.UserId };
        db.Folders.Add(folder);
        Touch(parent);
        await db.SaveChangesAsync(ct);
        await AuditAsync(actor, "files.folder.created", "Folder", folder.Id, new { folder.Name, parentId = parent.Id, departmentId = folder.DepartmentId }, ct);
        return folder;
    }

    public async Task RenameFolderAsync(FileActor actor, Guid folderId, string name, CancellationToken ct)
    {
        var folder = await WritableFolderAsync(actor, folderId, ct);
        if (folder.ParentId is not { } parentId)
        {
            throw FilesException.Invalid("Корневую папку отдела нельзя переименовать.");
        }

        name = ValidName(name);
        if (name == folder.Name)
        {
            return;
        }

        await EnsureFreeAsync(parentId, name, folder.Id, ct);
        var old = folder.Name;
        folder.Name = name;
        Touch(folder);
        await db.SaveChangesAsync(ct);
        await AuditAsync(actor, "files.folder.renamed", "Folder", folder.Id, new { from = old, to = name }, ct);
    }

    /// <summary>Перенос папки в другую папку того же отдела (не в саму себя и не во вложенную).</summary>
    public async Task MoveFolderAsync(FileActor actor, Guid folderId, Guid targetId, string? newName, CancellationToken ct)
    {
        var folder = await WritableFolderAsync(actor, folderId, ct);
        if (folder.ParentId is null)
        {
            throw FilesException.Invalid("Корневую папку отдела нельзя перенести.");
        }

        var target = await WritableFolderAsync(actor, targetId, ct);
        if (target.DepartmentId != folder.DepartmentId)
        {
            throw FilesException.Invalid("Папку можно перенести только внутри отдела. В другой отдел — скачайте и загрузите заново.");
        }

        for (Folder? cursor = target; cursor is not null; cursor = cursor.ParentId is { } up ? await db.Folders.FirstOrDefaultAsync(f => f.Id == up, ct) : null)
        {
            if (cursor.Id == folder.Id)
            {
                throw FilesException.Invalid("Папку нельзя перенести в саму себя или во вложенную папку.");
            }
        }

        var name = newName is null ? folder.Name : ValidName(newName);
        if (target.Id == folder.ParentId && name == folder.Name)
        {
            return;
        }

        await EnsureFreeAsync(target.Id, name, folder.Id, ct);
        var from = folder.ParentId;
        folder.ParentId = target.Id;
        folder.Name = name;
        Touch(folder);
        Touch(target);
        await db.SaveChangesAsync(ct);
        await AuditAsync(actor, "files.folder.moved", "Folder", folder.Id, new { from, to = target.Id, name }, ct);
    }

    /// <summary>Папка со всем содержимым — в корзину (одна отметка времени у всего поддерева: по ней же и восстановление).</summary>
    public async Task DeleteFolderAsync(FileActor actor, Guid folderId, CancellationToken ct)
    {
        var folder = await WritableFolderAsync(actor, folderId, ct);
        if (folder.ParentId is null)
        {
            throw FilesException.Invalid("Корневую папку отдела нельзя удалить.");
        }

        var now = DateTimeOffset.UtcNow;
        var (folderIds, _) = await SubtreeAsync(folder.Id, includeDeleted: false, ct);
        await db.Folders.Where(f => folderIds.Contains(f.Id) && f.DeletedAt == null).ExecuteUpdateAsync(s => s.SetProperty(f => f.DeletedAt, now), ct);
        await db.Files.Where(f => folderIds.Contains(f.FolderId) && f.DeletedAt == null).ExecuteUpdateAsync(s => s.SetProperty(f => f.DeletedAt, now), ct);
        await AuditAsync(actor, "files.folder.deleted", "Folder", folder.Id, new { folder.Name, folders = folderIds.Count }, ct);
    }

    // ---------- Файлы ----------

    /// <summary>
    /// Загрузка: новое имя — новый файл (версия 1); имя уже есть — новая версия того же файла (перезапись). length — размер тела,
    /// если известен; иначе тело сначала пишется во временный файл.
    /// </summary>
    public async Task<FileItem> UploadAsync(FileActor actor, Guid folderId, string name, Stream content, long? length, CancellationToken ct)
    {
        var folder = await WritableFolderAsync(actor, folderId, ct);
        name = ValidName(name);
        var limit = actor.IsConnection ? options.MaxDavFileBytes : options.MaxUploadBytes;
        if (length > limit)
        {
            throw new FilesException(413, $"Файл больше {limit / 1024 / 1024} МБ.");
        }

        if (await db.Folders.AnyAsync(f => f.ParentId == folder.Id && f.DeletedAt == null && f.Name.ToLower() == name.ToLower(), ct))
        {
            throw FilesException.Conflict(name);
        }

        var existing = await db.Files.Include(f => f.Versions)
            .FirstOrDefaultAsync(f => f.FolderId == folder.Id && f.DeletedAt == null && f.Name.ToLower() == name.ToLower(), ct);
        var file = existing ?? new FileItem { Name = name, ContentType = FileNames.ContentType(name), FolderId = folder.Id, DepartmentId = folder.DepartmentId, CreatedById = actor.UserId };

        var (key, size, sha) = await StoreAsync(file.Id, content, length, limit, FileNames.ContentType(name), ct);
        var now = DateTimeOffset.UtcNow;
        var current = existing?.Versions.FirstOrDefault(v => v.Number == existing.CurrentVersion);
        string action;
        if (existing is not null && current is not null && current.SizeBytes == 0 && now - current.CreatedAt < EmptyPlaceholderWindow
            && current.UploadedById == actor.UserId && current.UploadedByConnectionId == actor.ConnectionId)
        {
            // Пустая заготовка Проводника: содержимое заменяет её, отдельной версии не остаётся.
            var oldKey = current.ObjectKey;
            current.ObjectKey = key;
            current.SizeBytes = size;
            current.Sha256 = sha;
            await TryDeleteObjectAsync(oldKey, ct);
            action = existing.CurrentVersion == 1 ? "files.uploaded" : "files.updated";
        }
        else
        {
            var number = (existing?.CurrentVersion ?? 0) + 1;
            var added = new FileVersion
            {
                FileItemId = file.Id,
                Number = number,
                ObjectKey = key,
                SizeBytes = size,
                Sha256 = sha,
                UploadedById = actor.UserId,
                UploadedByConnectionId = actor.ConnectionId,
            };
            if (existing is null)
            {
                file.Versions.Add(added);
            }
            else
            {
                // Id версии задан в коде: через навигацию загруженного файла EF принял бы её за существующую строку (UPDATE вместо INSERT).
                db.FileVersions.Add(added);
            }

            file.CurrentVersion = number;
            action = existing is null ? "files.uploaded" : "files.updated";
        }

        if (existing is null)
        {
            db.Files.Add(file);
        }

        file.Name = existing is null ? name : file.Name;
        file.ContentType = FileNames.ContentType(file.Name);
        file.SizeBytes = size;
        file.UpdatedById = actor.UserId;
        file.UpdatedByConnectionId = actor.ConnectionId;
        file.UpdatedAt = now;
        Touch(folder);
        await db.SaveChangesAsync(ct);
        await AuditAsync(actor, action, "File", file.Id, new { file.Name, version = file.CurrentVersion, size, folderId = folder.Id }, ct);
        return file;
    }

    /// <summary>Файл и его текущая версия для скачивания (с проверкой доступа).</summary>
    public async Task<(FileItem File, FileVersion Version)> OpenAsync(FileActor actor, Guid fileId, int? version, CancellationToken ct)
    {
        var file = await ReadableFileAsync(actor, fileId, ct);
        var number = version ?? file.CurrentVersion;
        var v = await db.FileVersions.AsNoTracking().FirstOrDefaultAsync(x => x.FileItemId == file.Id && x.Number == number, ct)
                ?? throw FilesException.NotFound("Версия файла не найдена.");
        return (file, v);
    }

    public Task CopyContentAsync(FileVersion version, Stream destination, CancellationToken ct) =>
        storage.DownloadAsync(version.ObjectKey, destination, ct);

    public Task AuditDownloadAsync(FileActor actor, FileItem file, int version, CancellationToken ct) =>
        AuditAsync(actor, "files.downloaded", "File", file.Id, new { file.Name, version }, ct);

    public async Task RenameFileAsync(FileActor actor, Guid fileId, string name, CancellationToken ct)
    {
        var file = await WritableFileAsync(actor, fileId, ct);
        name = ValidName(name);
        if (name == file.Name)
        {
            return;
        }

        await EnsureFreeAsync(file.FolderId, name, file.Id, ct);
        var old = file.Name;
        file.Name = name;
        file.ContentType = FileNames.ContentType(name);
        file.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await AuditAsync(actor, "files.renamed", "File", file.Id, new { from = old, to = name }, ct);
    }

    /// <summary>Перенос файла в другую папку (в том числе другого отдела — нужна запись в обоих); replace — заменить файл с тем же именем.</summary>
    public async Task MoveFileAsync(FileActor actor, Guid fileId, Guid targetFolderId, string? newName, bool replace, CancellationToken ct)
    {
        var file = await WritableFileAsync(actor, fileId, ct);
        var target = await WritableFolderAsync(actor, targetFolderId, ct);
        var name = newName is null ? file.Name : ValidName(newName);
        if (target.Id == file.FolderId && name == file.Name)
        {
            return;
        }

        var clash = await db.Files.FirstOrDefaultAsync(f => f.FolderId == target.Id && f.DeletedAt == null && f.Id != file.Id && f.Name.ToLower() == name.ToLower(), ct);
        if (clash is not null)
        {
            if (!replace)
            {
                throw FilesException.Conflict(name);
            }

            clash.DeletedAt = DateTimeOffset.UtcNow; // заменяемый файл — в корзину, его можно восстановить
        }

        if (await db.Folders.AnyAsync(f => f.ParentId == target.Id && f.DeletedAt == null && f.Name.ToLower() == name.ToLower(), ct))
        {
            throw FilesException.Conflict(name);
        }

        var from = file.FolderId;
        file.FolderId = target.Id;
        file.DepartmentId = target.DepartmentId;
        file.Name = name;
        file.ContentType = FileNames.ContentType(name);
        file.UpdatedAt = DateTimeOffset.UtcNow;
        Touch(target);
        await db.SaveChangesAsync(ct);
        await AuditAsync(actor, "files.moved", "File", file.Id, new { from, to = target.Id, name, replaced = clash?.Id }, ct);
    }

    /// <summary>Копия файла (WebDAV COPY): содержимое текущей версии записывается заново — у копии своя история.</summary>
    public async Task<FileItem> CopyFileAsync(FileActor actor, Guid fileId, Guid targetFolderId, string name, CancellationToken ct)
    {
        var (file, version) = await OpenAsync(actor, fileId, null, ct);
        await using var buffer = TempStream();
        await storage.DownloadAsync(version.ObjectKey, buffer, ct);
        buffer.Position = 0;
        var copy = await UploadAsync(actor, targetFolderId, name, buffer, buffer.Length, ct);
        await AuditAsync(actor, "files.copied", "File", copy.Id, new { source = file.Id, copy.Name }, ct);
        return copy;
    }

    public async Task DeleteFileAsync(FileActor actor, Guid fileId, CancellationToken ct)
    {
        var file = await WritableFileAsync(actor, fileId, ct);
        file.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await AuditAsync(actor, "files.deleted", "File", file.Id, new { file.Name, folderId = file.FolderId }, ct);
    }

    public async Task<IReadOnlyList<VersionRow>> VersionsAsync(FileActor actor, Guid fileId, CancellationToken ct)
    {
        var file = await ReadableFileAsync(actor, fileId, ct);
        var versions = await db.FileVersions.AsNoTracking().Where(v => v.FileItemId == file.Id).OrderByDescending(v => v.Number).ToListAsync(ct);
        var people = await PeopleAsync(versions.Select(v => v.UploadedById), versions.Select(v => v.UploadedByConnectionId), ct);
        return versions.Select(v => new VersionRow(v.Number, v.SizeBytes, v.CreatedAt, Person(people, v.UploadedById, v.UploadedByConnectionId), v.Number == file.CurrentVersion)).ToList();
    }

    // ---------- Корзина ----------

    public async Task<IReadOnlyList<TrashRow>> TrashAsync(FileActor actor, string departmentCode, CancellationToken ct)
    {
        var department = await DepartmentAsync(actor, departmentCode, ct);
        var deletedFolders = await db.Folders.AsNoTracking().Where(f => f.DepartmentId == department.Id && f.DeletedAt != null).ToListAsync(ct);
        var deletedFolderIds = deletedFolders.Select(f => f.Id).ToHashSet();
        // В списке — то, что удаляли по отдельности: папка, чей родитель не удалён тем же действием, и файлы вне удалённых папок.
        var topFolders = deletedFolders.Where(f => f.ParentId is not { } p || !deletedFolders.Any(x => x.Id == p && x.DeletedAt == f.DeletedAt)).ToList();
        var files = await db.Files.AsNoTracking().Where(f => f.DepartmentId == department.Id && f.DeletedAt != null).ToListAsync(ct);
        var topFiles = files.Where(f => !deletedFolderIds.Contains(f.FolderId) || deletedFolders.First(x => x.Id == f.FolderId).DeletedAt != f.DeletedAt).ToList();

        var rows = new List<TrashRow>();
        foreach (var f in topFolders)
        {
            rows.Add(new TrashRow(f.Id, true, f.Name, 0, await PathTextAsync(f.ParentId, ct), f.DeletedAt!.Value));
        }

        foreach (var f in topFiles)
        {
            rows.Add(new TrashRow(f.Id, false, f.Name, f.SizeBytes, await PathTextAsync(f.FolderId, ct), f.DeletedAt!.Value));
        }

        return rows.OrderByDescending(r => r.DeletedAt).ToList();
    }

    public async Task RestoreFileAsync(FileActor actor, Guid fileId, CancellationToken ct)
    {
        var file = await db.Files.FirstOrDefaultAsync(f => f.Id == fileId && f.DeletedAt != null, ct) ?? throw FilesException.NotFound();
        RequireWrite(actor, file.DepartmentId);
        var folder = await db.Folders.FirstAsync(f => f.Id == file.FolderId, ct);
        if (folder.DeletedAt is not null)
        {
            throw FilesException.Invalid($"Папка «{folder.Name}» в корзине — восстановите её.");
        }

        file.Name = await FreeNameAsync(folder.Id, file.Name, ct);
        file.DeletedAt = null;
        file.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await AuditAsync(actor, "files.restored", "File", file.Id, new { file.Name }, ct);
    }

    public async Task RestoreFolderAsync(FileActor actor, Guid folderId, CancellationToken ct)
    {
        var folder = await db.Folders.FirstOrDefaultAsync(f => f.Id == folderId && f.DeletedAt != null, ct) ?? throw FilesException.NotFound();
        RequireWrite(actor, folder.DepartmentId);
        var parent = await db.Folders.FirstAsync(f => f.Id == folder.ParentId, ct);
        if (parent.DeletedAt is not null)
        {
            throw FilesException.Invalid($"Папка «{parent.Name}» в корзине — восстановите её.");
        }

        var stamp = folder.DeletedAt;
        var (folderIds, _) = await SubtreeAsync(folder.Id, includeDeleted: true, ct);
        folder.Name = await FreeNameAsync(parent.Id, folder.Name, ct);
        folder.DeletedAt = null;
        await db.SaveChangesAsync(ct);
        await db.Folders.Where(f => folderIds.Contains(f.Id) && f.DeletedAt == stamp).ExecuteUpdateAsync(s => s.SetProperty(f => f.DeletedAt, (DateTimeOffset?)null), ct);
        await db.Files.Where(f => folderIds.Contains(f.FolderId) && f.DeletedAt == stamp).ExecuteUpdateAsync(s => s.SetProperty(f => f.DeletedAt, (DateTimeOffset?)null), ct);
        await AuditAsync(actor, "files.folder.restored", "Folder", folder.Id, new { folder.Name }, ct);
    }

    // ---------- Поиск ----------

    /// <summary>Поиск по имени, расширению, папке, отделу и автору — в отделах, доступных на чтение (по содержимому — нет).</summary>
    public async Task<IReadOnlyList<SearchRow>> SearchAsync(FileActor actor, string query, string? departmentCode, CancellationToken ct)
    {
        var q = query.Trim();
        if (q.Length < 2)
        {
            return [];
        }

        var departments = (await db.Departments.AsNoTracking().ToListAsync(ct))
            .Where(d => FileAccessRules.CanRead(actor, d.Id) && (departmentCode is null || d.Code == departmentCode.ToLower()))
            .ToDictionary(d => d.Id);
        var ids = departments.Keys.ToList();
        var pattern = "%" + q.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
        var matchingPeople = await db.Users.AsNoTracking().Where(u => EF.Functions.ILike(u.FullName, pattern)).Select(u => (Guid?)u.Id).ToListAsync(ct);
        var matchingDepartments = departments.Values.Where(d => d.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).Select(d => (Guid?)d.Id).ToList();

        var files = await db.Files.AsNoTracking()
            .Where(f => f.DepartmentId != null && ids.Contains(f.DepartmentId.Value) && f.DeletedAt == null && f.Folder.DeletedAt == null
                        && (EF.Functions.ILike(f.Name, pattern)
                            || EF.Functions.ILike(f.Folder.Name, pattern)
                            || matchingPeople.Contains(f.UpdatedById)
                            || matchingPeople.Contains(f.CreatedById)
                            || matchingDepartments.Contains(f.DepartmentId)))
            .OrderByDescending(f => f.UpdatedAt ?? f.CreatedAt)
            .Take(200)
            .ToListAsync(ct);

        var rows = await RowsAsync(files, ct);
        var result = new List<SearchRow>();
        foreach (var row in rows.Where(r => !r.Hidden))
        {
            var file = files.First(f => f.Id == row.Id);
            var d = departments[file.DepartmentId!.Value];
            result.Add(new SearchRow(row, new DepartmentRef(d.Id, d.Code, d.Name, AccessName(FileAccessRules.AccessTo(actor, d.Id))), await PathTextAsync(file.FolderId, ct)));
        }

        return result;
    }

    // ---------- Доступ других отделов ----------

    /// <summary>
    /// Доступ других отделов к папке отдела (выдачи на корневой папке, субъект — отдел): нет, чтение или запись. Свой отдел — по ролям
    /// (files.read / files.write), его здесь не меняют. Только администратор.
    /// </summary>
    public async Task<IReadOnlyList<DepartmentAccessRow>> DepartmentAccessAsync(FileActor actor, Department department, CancellationToken ct)
    {
        RequireManage(actor);
        var root = await RootAsync(department, ct);
        var grants = await db.ResourcePermissions.AsNoTracking()
            .Where(p => p.FolderId == root.Id && p.PrincipalType == PrincipalType.Department)
            .ToDictionaryAsync(p => p.PrincipalId, p => p.Access, ct);
        return (await db.Departments.AsNoTracking().Where(d => d.Id != department.Id).OrderBy(d => d.Name).ToListAsync(ct))
            .Select(d => new DepartmentAccessRow(d.Id, d.Code, d.Name, AccessName(grants.TryGetValue(d.Id.ToString(), out var a) ? a : null)))
            .ToList();
    }

    public async Task SetDepartmentAccessAsync(FileActor actor, Department department, Guid otherDepartmentId, AccessLevel? access, CancellationToken ct)
    {
        RequireManage(actor);
        if (otherDepartmentId == department.Id || !await db.Departments.AnyAsync(d => d.Id == otherDepartmentId, ct))
        {
            throw FilesException.Invalid("Выберите другой отдел.");
        }

        if (access is AccessLevel.Manage)
        {
            throw FilesException.Invalid("Другому отделу можно дать чтение или запись.");
        }

        var root = await RootAsync(department, ct);
        var key = otherDepartmentId.ToString();
        var grant = await db.ResourcePermissions.FirstOrDefaultAsync(p => p.FolderId == root.Id && p.PrincipalType == PrincipalType.Department && p.PrincipalId == key, ct);
        if (access is null)
        {
            if (grant is not null)
            {
                db.ResourcePermissions.Remove(grant);
            }
        }
        else if (grant is null)
        {
            db.ResourcePermissions.Add(new ResourcePermission { FolderId = root.Id, PrincipalType = PrincipalType.Department, PrincipalId = key, Access = access.Value });
        }
        else
        {
            grant.Access = access.Value;
            grant.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        await AuditAsync(actor, "files.access.changed", "Department", department.Id, new { department = department.Code, to = otherDepartmentId, access = AccessName(access) }, ct);
    }

    private static void RequireManage(FileActor actor)
    {
        if (!FileAccessRules.CanManageConnection(actor))
        {
            throw new FilesException(403, "Доступ к папкам отделов настраивает администратор.");
        }
    }

    // ---------- WebDAV ----------

    /// <summary>
    /// Узел по пути WebDAV: первый сегмент — код отдела, дальше имена папок и в конце папка или файл (без учёта регистра, как в Windows).
    /// null — не найден или нет доступа на чтение (для чужого отдела ответ такой же, как для несуществующего).
    /// </summary>
    public async Task<DavNode?> ResolveAsync(FileActor actor, IReadOnlyList<string> segments, CancellationToken ct)
    {
        if (segments.Count == 0)
        {
            return null;
        }

        var code = segments[0].ToLowerInvariant();
        var department = await db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Code == code, ct);
        if (department is null || !FileAccessRules.CanRead(actor, department.Id))
        {
            return null;
        }

        var folder = await RootAsync(department, ct);
        for (var i = 1; i < segments.Count; i++)
        {
            var name = segments[i].ToLower();
            var child = await db.Folders.FirstOrDefaultAsync(f => f.ParentId == folder.Id && f.DeletedAt == null && f.Name.ToLower() == name, ct);
            if (child is not null)
            {
                folder = child;
                continue;
            }

            if (i == segments.Count - 1)
            {
                var file = await db.Files.FirstOrDefaultAsync(f => f.FolderId == folder.Id && f.DeletedAt == null && f.Name.ToLower() == name, ct);
                return file is null ? null : new DavNode(department, folder, file);
            }

            return null;
        }

        return new DavNode(department, folder, null);
    }

    /// <summary>Отделы, доступные на чтение (корень WebDAV): у подключения — только его отдел.</summary>
    public async Task<IReadOnlyList<Department>> ReadableDepartmentsAsync(FileActor actor, CancellationToken ct) =>
        (await db.Departments.AsNoTracking().OrderBy(d => d.Code).ToListAsync(ct)).Where(d => FileAccessRules.CanRead(actor, d.Id)).ToList();

    public async Task<(IReadOnlyList<Folder> Folders, IReadOnlyList<FileItem> Files)> ChildrenAsync(Guid folderId, CancellationToken ct) =>
        (await db.Folders.AsNoTracking().Where(f => f.ParentId == folderId && f.DeletedAt == null).OrderBy(f => f.Name).ToListAsync(ct),
         await db.Files.AsNoTracking().Where(f => f.FolderId == folderId && f.DeletedAt == null).OrderBy(f => f.Name).ToListAsync(ct));

    public static bool CanWrite(FileActor actor, Guid? departmentId) => departmentId is { } d && FileAccessRules.CanWrite(actor, d);

    // ---------- Внутреннее ----------

    private async Task<(string Key, long Size, string Sha256)> StoreAsync(Guid fileId, Stream content, long? length, long limit, string contentType, CancellationToken ct)
    {
        var key = $"files/{fileId:N}/{Guid.CreateVersion7():N}";
        Stream source = content;
        FileStream? temp = null;
        try
        {
            if (length is null)
            {
                // Размер неизвестен (chunked) — MinIO нужен размер заранее: сначала во временный файл.
                temp = TempStream();
                await CopyLimitedAsync(content, temp, limit, ct);
                temp.Position = 0;
                source = temp;
                length = temp.Length;
            }

            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using var hashing = new HashingReadStream(source, hash, length.Value);
            await storage.PutAsync(key, hashing, length.Value, contentType, ct);
            if (hashing.BytesRead != length.Value)
            {
                await TryDeleteObjectAsync(key, ct);
                throw FilesException.Invalid("Файл пришёл не полностью — загрузите ещё раз.");
            }

            return (key, length.Value, Convert.ToHexStringLower(hash.GetHashAndReset()));
        }
        finally
        {
            if (temp is not null)
            {
                await temp.DisposeAsync();
            }
        }
    }

    private static async Task CopyLimitedAsync(Stream source, Stream destination, long limit, CancellationToken ct)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            total += read;
            if (total > limit)
            {
                throw new FilesException(413, $"Файл больше {limit / 1024 / 1024} МБ.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), ct);
        }
    }

    private static FileStream TempStream() =>
        new(Path.Combine(Path.GetTempPath(), $"onebase-file-{Guid.NewGuid():N}"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920,
            FileOptions.DeleteOnClose | FileOptions.Asynchronous);

    private async Task TryDeleteObjectAsync(string key, CancellationToken ct)
    {
        try
        {
            await storage.DeleteAsync(key, ct);
        }
        catch (Exception)
        {
            // Лишний объект в MinIO не ломает файлы: ссылки на него в базе уже нет.
        }
    }

    private async Task<Folder> ReadableFolderAsync(FileActor actor, Guid folderId, CancellationToken ct)
    {
        var folder = await db.Folders.FirstOrDefaultAsync(f => f.Id == folderId && f.DeletedAt == null, ct);
        return folder is { DepartmentId: { } d } && FileAccessRules.CanRead(actor, d) ? folder : throw FilesException.NotFound("Папка не найдена.");
    }

    private async Task<Folder> WritableFolderAsync(FileActor actor, Guid folderId, CancellationToken ct)
    {
        var folder = await ReadableFolderAsync(actor, folderId, ct);
        RequireWrite(actor, folder.DepartmentId);
        return folder;
    }

    private async Task<FileItem> ReadableFileAsync(FileActor actor, Guid fileId, CancellationToken ct)
    {
        var file = await db.Files.FirstOrDefaultAsync(f => f.Id == fileId && f.DeletedAt == null, ct);
        return file is { DepartmentId: { } d } && FileAccessRules.CanRead(actor, d) ? file : throw FilesException.NotFound("Файл не найден.");
    }

    private async Task<FileItem> WritableFileAsync(FileActor actor, Guid fileId, CancellationToken ct)
    {
        var file = await ReadableFileAsync(actor, fileId, ct);
        RequireWrite(actor, file.DepartmentId);
        return file;
    }

    private static void RequireWrite(FileActor actor, Guid? departmentId)
    {
        if (departmentId is not { } d || !FileAccessRules.CanRead(actor, d))
        {
            throw FilesException.NotFound();
        }

        if (!FileAccessRules.CanWrite(actor, d))
        {
            throw FilesException.Forbidden();
        }
    }

    private static string ValidName(string name)
    {
        var value = FileNames.Normalize(name);
        return FileNames.Error(value) is { } error ? throw FilesException.Invalid(error) : value;
    }

    /// <summary>В папке нет ни файла, ни папки с таким именем (без учёта регистра); except — сам переименовываемый объект.</summary>
    private async Task EnsureFreeAsync(Guid folderId, string name, Guid? except, CancellationToken ct)
    {
        var lower = name.ToLower();
        if (await db.Folders.AnyAsync(f => f.ParentId == folderId && f.DeletedAt == null && f.Id != except && f.Name.ToLower() == lower, ct)
            || await db.Files.AnyAsync(f => f.FolderId == folderId && f.DeletedAt == null && f.Id != except && f.Name.ToLower() == lower, ct))
        {
            throw FilesException.Conflict(name);
        }
    }

    /// <summary>Свободное имя для восстановления: «отчёт.xlsx» занято — «отчёт (2).xlsx».</summary>
    private async Task<string> FreeNameAsync(Guid folderId, string name, CancellationToken ct)
    {
        var dot = name.LastIndexOf('.');
        var (stem, ext) = dot > 0 ? (name[..dot], name[dot..]) : (name, string.Empty);
        for (var i = 1; i < 1000; i++)
        {
            var candidate = i == 1 ? name : $"{stem} ({i}){ext}";
            var lower = candidate.ToLower();
            if (!await db.Folders.AnyAsync(f => f.ParentId == folderId && f.DeletedAt == null && f.Name.ToLower() == lower, ct)
                && !await db.Files.AnyAsync(f => f.FolderId == folderId && f.DeletedAt == null && f.Name.ToLower() == lower, ct))
            {
                return candidate;
            }
        }

        throw FilesException.Conflict(name);
    }

    private async Task<(List<Guid> Folders, int Depth)> SubtreeAsync(Guid rootId, bool includeDeleted, CancellationToken ct)
    {
        var all = new List<Guid> { rootId };
        var level = new List<Guid> { rootId };
        var depth = 0;
        while (level.Count > 0 && depth < 100)
        {
            level = await db.Folders.Where(f => f.ParentId != null && level.Contains(f.ParentId.Value) && (includeDeleted || f.DeletedAt == null))
                .Select(f => f.Id).ToListAsync(ct);
            all.AddRange(level);
            depth++;
        }

        return (all, depth);
    }

    private async Task<IReadOnlyList<FolderCrumb>> PathAsync(Folder folder, Department department, CancellationToken ct)
    {
        var path = new List<FolderCrumb>();
        for (Folder? cursor = folder; cursor is not null; cursor = cursor.ParentId is { } up ? await db.Folders.AsNoTracking().FirstOrDefaultAsync(f => f.Id == up, ct) : null)
        {
            path.Add(new FolderCrumb(cursor.Id, cursor.ParentId is null ? department.Name : cursor.Name));
        }

        path.Reverse();
        return path;
    }

    private async Task<string> PathTextAsync(Guid? folderId, CancellationToken ct)
    {
        var names = new List<string>();
        for (var id = folderId; id is { } current;)
        {
            var folder = await db.Folders.AsNoTracking().Where(f => f.Id == current).Select(f => new { f.Name, f.ParentId }).FirstOrDefaultAsync(ct);
            if (folder is null)
            {
                break;
            }

            names.Add(folder.Name);
            id = folder.ParentId;
        }

        names.Reverse();
        return string.Join(" / ", names);
    }

    private async Task<IReadOnlyList<FileRow>> RowsAsync(IReadOnlyList<FileItem> files, CancellationToken ct)
    {
        var people = await PeopleAsync(files.Select(f => f.UpdatedById ?? f.CreatedById), files.Select(f => f.UpdatedByConnectionId), ct);
        return files.Select(f => new FileRow(f.Id, f.FolderId, f.Name, FileNames.Extension(f.Name), f.ContentType, f.SizeBytes, f.CurrentVersion,
                f.UpdatedAt ?? f.CreatedAt, Person(people, f.UpdatedById ?? f.CreatedById, f.UpdatedByConnectionId), FileNames.IsHidden(f.Name)))
            .ToList();
    }

    private async Task<Dictionary<Guid, FileUserRef>> PeopleAsync(IEnumerable<Guid?> users, IEnumerable<Guid?> connections, CancellationToken ct)
    {
        var userIds = users.OfType<Guid>().Distinct().ToList();
        var connectionIds = connections.OfType<Guid>().Distinct().ToList();
        var result = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => new FileUserRef(u.FullName, false), ct);
        foreach (var c in await db.FileConnections.AsNoTracking().Where(c => connectionIds.Contains(c.Id)).Select(c => new { c.Id, c.Username }).ToListAsync(ct))
        {
            result[c.Id] = new FileUserRef($"Windows · {c.Username}", true);
        }

        return result;
    }

    private static FileUserRef? Person(Dictionary<Guid, FileUserRef> people, Guid? user, Guid? connection) =>
        connection is { } c && people.TryGetValue(c, out var viaWindows) ? viaWindows
        : user is { } u && people.TryGetValue(u, out var person) ? person
        : null;

    private static void Touch(Folder folder) => folder.UpdatedAt = DateTimeOffset.UtcNow;

    private static string AccessName(AccessLevel? access) => access switch
    {
        AccessLevel.Manage => "manage",
        AccessLevel.Write => "write",
        AccessLevel.Read => "read",
        _ => "none",
    };

    private Task AuditAsync(FileActor actor, string action, string entityType, Guid entityId, object data, CancellationToken ct) =>
        actor.ConnectionId is { } connection
            ? audit.LogAsync(ActorType.Connection, connection.ToString(), action, entityType, entityId.ToString(), new { connection = actor.Name, details = data }, ct)
            : audit.LogAsync(ActorType.User, actor.UserId?.ToString() ?? "unknown", action, entityType, entityId.ToString(), data, ct);
}

/// <summary>Поток чтения, который по пути считает SHA-256 и число прочитанных байт (хэш версии без второго прохода по файлу).</summary>
internal sealed class HashingReadStream(Stream inner, IncrementalHash hash, long length) : Stream
{
    public long BytesRead { get; private set; }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => length;

    public override long Position
    {
        get => BytesRead;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        var read = inner.Read(buffer);
        Track(buffer[..read]);
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = await inner.ReadAsync(buffer, cancellationToken);
        Track(buffer.Span[..read]);
        return read;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    private void Track(ReadOnlySpan<byte> data)
    {
        hash.AppendData(data);
        BytesRead += data.Length;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
