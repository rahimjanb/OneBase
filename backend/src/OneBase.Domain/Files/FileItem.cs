using OneBase.Domain.Common;

namespace OneBase.Domain.Files;

/// <summary>Метаданные файла. Содержимое каждой версии лежит в MinIO по ключу <see cref="FileVersion.ObjectKey"/>.</summary>
public class FileItem : Entity
{
    public required string Name { get; set; }
    public required string ContentType { get; set; }

    public Guid FolderId { get; set; }
    public Folder Folder { get; set; } = null!;

    public int CurrentVersion { get; set; }
    public ICollection<FileVersion> Versions { get; set; } = [];
    public ICollection<ResourcePermission> Permissions { get; set; } = [];
}

public class FileVersion : Entity
{
    public Guid FileItemId { get; set; }
    public FileItem FileItem { get; set; } = null!;

    public int Number { get; set; }
    public required string ObjectKey { get; set; }
    public long SizeBytes { get; set; }
    public required string Sha256 { get; set; }
    public Guid UploadedById { get; set; }
}
