using OneBase.Domain.Common;

namespace OneBase.Domain.Identity;

public class User : Entity
{
    public required string Email { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public required string FullName { get; set; }
    public bool IsActive { get; set; } = true;

    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public ICollection<UserRole> Roles { get; set; } = [];
}
