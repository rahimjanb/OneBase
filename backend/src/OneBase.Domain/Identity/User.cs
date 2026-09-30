using OneBase.Domain.Common;

namespace OneBase.Domain.Identity;

public class User : Entity
{
    /// <summary>Логин для входа: строчными буквами, уникален.</summary>
    public required string Login { get; set; }

    /// <summary>Почта — необязательна; если задана, уникальна и тоже подходит для входа.</summary>
    public string? Email { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    /// <summary>«Имя Фамилия» — для подписи в интерфейсе, журнале и токене.</summary>
    public required string FullName { get; set; }

    public string? Phone { get; set; }

    /// <summary>Должность.</summary>
    public string? Position { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public ICollection<UserRole> Roles { get; set; } = [];

    public static string FullNameOf(string firstName, string lastName) => $"{firstName.Trim()} {lastName.Trim()}".Trim();
}
