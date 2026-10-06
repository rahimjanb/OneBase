using OneBase.Domain.Common;
using OneBase.Domain.Identity;

namespace OneBase.Domain.Files;

public class Folder : Entity
{
    public required string Name { get; set; }

    public Guid? ParentId { get; set; }
    public Folder? Parent { get; set; }
    public ICollection<Folder> Children { get; set; } = [];

    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public Guid? CreatedById { get; set; }

    /// <summary>В корзине с этого момента (вместе со всем содержимым).</summary>
    public DateTimeOffset? DeletedAt { get; set; }

    public ICollection<FileItem> Files { get; set; } = [];
    public ICollection<ResourcePermission> Permissions { get; set; } = [];
}
