using OneBase.Domain.Common;

namespace OneBase.Domain.Identity;

public class Department : Entity
{
    public required string Code { get; set; }
    public required string Name { get; set; }

    public ICollection<User> Users { get; set; } = [];
}
