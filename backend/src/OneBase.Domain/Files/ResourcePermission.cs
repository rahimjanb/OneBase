using OneBase.Domain.Common;

namespace OneBase.Domain.Files;

public enum PrincipalType
{
    User,
    Role,
    Department,
    Agent,
}

public enum AccessLevel
{
    Read = 1,
    Write = 2,
    Manage = 3,
}

/// <summary>Доступ субъекта к папке или файлу. Ровно одно из FolderId / FileItemId заполнено.</summary>
public class ResourcePermission : Entity
{
    public Guid? FolderId { get; set; }
    public Guid? FileItemId { get; set; }

    public PrincipalType PrincipalType { get; set; }
    public required string PrincipalId { get; set; }
    public AccessLevel Access { get; set; }
}
