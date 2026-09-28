using OneBase.Domain.Common;

namespace OneBase.Domain.Identity;

public class Role : Entity
{
    public required string Name { get; set; }
    public string? Description { get; set; }

    public ICollection<RolePermission> Permissions { get; set; } = [];
    public ICollection<UserRole> Users { get; set; } = [];
}

public class UserRole
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;
}

/// <summary>Код разрешения, выданный роли (например, "files.write"). Список кодов — в Application.Security.Permissions.</summary>
public class RolePermission
{
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public required string Code { get; set; }
}
