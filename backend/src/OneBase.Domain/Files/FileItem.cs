using OneBase.Domain.Common;

namespace OneBase.Domain.Files;

/// <summary>Метаданные файла. Содержимое каждой версии лежит в MinIO по ключу <see cref="FileVersion.ObjectKey"/>.</summary>
public class FileItem : Entity
{
    public required string Name { get; set; }
    public required string ContentType { get; set; }

    public Guid FolderId { get; set; }
    public Folder Folder { get; set; } = null!;

    /// <summary>Отдел (как у папки) — для поиска и проверки доступа без обхода дерева папок.</summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>Размер текущей версии — для списков без чтения версий.</summary>
    public long SizeBytes { get; set; }

    public Guid? CreatedById { get; set; }
    public Guid? UpdatedById { get; set; }

    /// <summary>Подключение Windows (WebDAV), через которое файл последний раз изменили; null — изменён в OneBase.</summary>
    public Guid? UpdatedByConnectionId { get; set; }

    /// <summary>В корзине с этого момента; содержимое в MinIO остаётся до восстановления или очистки.</summary>
    public DateTimeOffset? DeletedAt { get; set; }

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

    /// <summary>Кто загрузил в OneBase; null — версия пришла через подключение Windows (<see cref="UploadedByConnectionId"/>).</summary>
    public Guid? UploadedById { get; set; }

    public Guid? UploadedByConnectionId { get; set; }
}
