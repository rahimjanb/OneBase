using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OneBase.Application.Abstractions;
using OneBase.Application.Files;
using OneBase.Domain.AI;

namespace OneBase.Application.DataManager;

/// <summary>
/// Отчёты AI Data Manager: проверка формата и размера, отпечаток SHA-256, разбор таблиц, хранение в MinIO.
/// Файл видит только тот, кто его загрузил.
/// </summary>
public sealed partial class DataFileService(
    IAppDbContext db,
    IFileStorage storage,
    IReportReader reader,
    IMemoryCache cache,
    DataManagerOptions options)
{
    public IReadOnlyCollection<string> Extensions => reader.Extensions;

    public async Task<DataFileView> UploadAsync(Guid userId, Guid? conversationId, string fileName, Stream content, CancellationToken ct = default)
    {
        var name = FileNames.Normalize(Path.GetFileName(fileName));
        var extension = Path.GetExtension(name).ToLowerInvariant();
        if (name.Length is 0 or > 255)
        {
            throw new DataManagerException("Имя файла — от 1 до 255 символов.");
        }

        if (!reader.Extensions.Contains(extension))
        {
            throw new DataManagerException($"Формат {extension} не поддерживается. Можно: {string.Join(", ", reader.Extensions)}. "
                                           + "Старый .xls сохраните в Excel как .xlsx.");
        }

        using var buffer = new MemoryStream();
        await CopyLimitedAsync(content, buffer, options.MaxFileBytes, ct);
        if (buffer.Length == 0)
        {
            throw new DataManagerException("Файл пустой.");
        }

        var bytes = buffer.ToArray();
        var document = Parse(bytes, name); // повреждённый или неподходящий файл не сохраняется
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));

        if (conversationId is { } cid && !await db.DataConversations.AnyAsync(c => c.Id == cid && c.UserId == userId, ct))
        {
            throw new DataManagerException("Чат не найден.");
        }

        var file = new DataFile
        {
            UserId = userId,
            ConversationId = conversationId,
            FileName = name,
            ContentType = FileNames.ContentType(name),
            SizeBytes = bytes.Length,
            Sha256 = sha,
            ObjectKey = string.Empty,
        };
        file.ObjectKey = $"data-manager/{file.Id:N}/{SafeName().Replace(name, "_")}";
        file.Summary = JsonSerializer.Serialize(Sheets(document), DataTargets.Json);

        await storage.PutAsync(file.ObjectKey, new MemoryStream(bytes), bytes.Length, file.ContentType, ct);
        db.DataFiles.Add(file);
        await db.SaveChangesAsync(ct);
        cache.Set(CacheKey(file.Id), document, TimeSpan.FromMinutes(20));

        return await ViewAsync(file, ct);
    }

    /// <summary>Файл пользователя и его таблицы; чужой или несуществующий — ошибка «не найден».</summary>
    public async Task<(DataFile File, ReportDocument Document)> LoadAsync(Guid fileId, Guid userId, CancellationToken ct = default)
    {
        var file = await db.DataFiles.AsNoTracking().FirstOrDefaultAsync(f => f.Id == fileId && f.UserId == userId, ct)
                   ?? throw new DataManagerException($"Файл {fileId} не найден среди ваших загрузок.");
        if (cache.TryGetValue(CacheKey(fileId), out ReportDocument? cached) && cached is not null)
        {
            return (file, cached);
        }

        using var buffer = new MemoryStream();
        await storage.DownloadAsync(file.ObjectKey, buffer, ct);
        var document = Parse(buffer.ToArray(), file.FileName);
        cache.Set(CacheKey(fileId), document, TimeSpan.FromMinutes(20));
        return (file, document);
    }

    public async Task<IReadOnlyList<DataFileView>> ListAsync(Guid userId, Guid conversationId, CancellationToken ct = default)
    {
        var files = await db.DataFiles.AsNoTracking()
            .Where(f => f.UserId == userId && f.ConversationId == conversationId)
            .OrderBy(f => f.CreatedAt)
            .ToListAsync(ct);
        var views = new List<DataFileView>();
        foreach (var file in files)
        {
            views.Add(await ViewAsync(file, ct));
        }

        return views;
    }

    public async Task<DataFileView?> GetAsync(Guid fileId, Guid userId, CancellationToken ct = default) =>
        await db.DataFiles.AsNoTracking().FirstOrDefaultAsync(f => f.Id == fileId && f.UserId == userId, ct) is { } file
            ? await ViewAsync(file, ct)
            : null;

    private async Task<DataFileView> ViewAsync(DataFile file, CancellationToken ct)
    {
        var earlier = await db.DataFiles.AsNoTracking()
            .Where(f => f.Sha256 == file.Sha256 && f.Id != file.Id && f.CreatedAt <= file.CreatedAt)
            .OrderByDescending(f => f.CreatedAt)
            .Take(10)
            .Select(f => new { f.Id, f.FileName, f.CreatedAt })
            .ToListAsync(ct);
        var earlierIds = earlier.Select(f => f.Id).ToList();
        var applied = await db.DataChangeSets.AsNoTracking()
            .Where(s => s.FileId != null && earlierIds.Contains(s.FileId.Value)
                        && (s.Status == DataChangeSetStatus.Applied || s.Status == DataChangeSetStatus.PartiallyRolledBack))
            .Select(s => new { s.Id, FileId = s.FileId!.Value })
            .ToListAsync(ct);

        var sheets = string.IsNullOrEmpty(file.Summary)
            ? []
            : JsonSerializer.Deserialize<List<DataSheetInfo>>(file.Summary, DataTargets.Json) ?? [];
        return new DataFileView(file.Id, file.FileName, file.SizeBytes, file.Sha256, file.CreatedAt, sheets,
            earlier.Select(f => new DataFileDuplicate(f.Id, f.FileName, f.CreatedAt, applied.Where(a => a.FileId == f.Id).Select(a => a.Id).ToList())).ToList());
    }

    private ReportDocument Parse(byte[] bytes, string name)
    {
        ReportDocument document;
        try
        {
            document = reader.Read(new MemoryStream(bytes), name);
        }
        catch (NotSupportedException ex)
        {
            throw new DataManagerException(ex.Message);
        }

        if (document.Sheets.Count == 0 || document.Sheets.All(s => s.Rows.Count == 0))
        {
            throw new DataManagerException("В файле нет данных: все листы пустые.");
        }

        return document;
    }

    private static List<DataSheetInfo> Sheets(ReportDocument document) =>
        document.Sheets.Select(s => new DataSheetInfo(s.Name, s.TotalRows, s.TotalColumns, s.Truncated)).ToList();

    private static async Task CopyLimitedAsync(Stream source, Stream target, long limit, CancellationToken ct)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            total += read;
            if (total > limit)
            {
                throw new DataManagerException($"Файл больше {limit / 1024 / 1024} МБ.");
            }

            await target.WriteAsync(buffer.AsMemory(0, read), ct);
        }
    }

    private static string CacheKey(Guid fileId) => $"data-manager:file:{fileId:N}";

    [GeneratedRegex(@"[^\p{L}\p{N}._-]+")]
    private static partial Regex SafeName();
}
