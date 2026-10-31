using Microsoft.EntityFrameworkCore;
using OneBase.Application.Files;

namespace OneBase.Infrastructure.Files;

/// <summary>
/// Файл в «Моих документах». Relation: uploaded — загружен мной, report — из папки «Отчёты», shared — из доступной мне папки, assigned — вложение
/// к моей задаче от другого человека (Source = attachment, TaskId — задача).
/// </summary>
public sealed record MyDocument(
    Guid Id,
    string Source,
    string Name,
    string ContentType,
    long SizeBytes,
    DateTimeOffset UpdatedAt,
    string Relation,
    string? By,
    string? Place,
    Guid? TaskId = null);

public sealed partial class DepartmentFilesService
{
    /// <summary>Вкладки «Моих документов» из «Файлов» (назначенные — вложения задач, их собирает WorkTaskService).</summary>
    public static readonly string[] MyTabs = ["recent", "mine", "reports"];

    /// <summary>
    /// recent — последние изменённые файлы в моих отделах и доступных папках; mine — загруженные мной (первая или любая версия); reports — из папок
    /// «Отчёты» (системная папка отдела). Доступ — те же правила, что у «Файлов» (FileAccessRules): чужие отделы без выдачи не видны.
    /// </summary>
    public async Task<IReadOnlyList<MyDocument>> MyDocumentsAsync(FileActor actor, string tab, int take, CancellationToken ct)
    {
        if (!actor.CanRead || actor.UserId is not { } me)
        {
            return [];
        }

        var departments = (await db.Departments.AsNoTracking().ToListAsync(ct)).Where(d => FileAccessRules.CanRead(actor, d.Id)).ToDictionary(d => d.Id);
        var shared = (await ReadableSharedAsync(actor, ct)).ToDictionary(r => r.Id);
        var ids = departments.Keys.ToList();
        var sharedIds = shared.Keys.ToList();
        var q = db.Files.AsNoTracking()
            .Where(f => ((f.DepartmentId != null && ids.Contains(f.DepartmentId.Value)) || (f.SharedRootId != null && sharedIds.Contains(f.SharedRootId.Value)))
                        && f.DeletedAt == null && f.Folder.DeletedAt == null);
        var reportsFolder = DefaultFolders[1];
        q = tab switch
        {
            "mine" => q.Where(f => f.CreatedById == me || db.FileVersions.Any(v => v.FileItemId == f.Id && v.UploadedById == me)),
            "reports" => q.Where(f => f.Folder.Name == reportsFolder),
            _ => q,
        };

        // С запасом: скрытые служебные файлы (~$, .tmp) отбрасываются после выборки.
        var files = await q.OrderByDescending(f => f.UpdatedAt ?? f.CreatedAt).Take(take * 3).Include(f => f.Folder).ToListAsync(ct);
        var rows = await RowsAsync(files, ct);
        return rows.Where(r => !r.Hidden).Take(take).Select(r =>
        {
            var file = files.First(f => f.Id == r.Id);
            var place = file.DepartmentId is { } d ? departments[d].Name : shared.TryGetValue(file.SharedRootId!.Value, out var root) ? root.Name : null;
            var relation = file.CreatedById == me || tab == "mine" ? "uploaded" : file.Folder.Name == reportsFolder ? "report" : "shared";
            return new MyDocument(r.Id, "file", r.Name, r.ContentType, r.SizeBytes, r.UpdatedAt, relation, r.UpdatedBy?.Name, place);
        }).ToList();
    }
}
