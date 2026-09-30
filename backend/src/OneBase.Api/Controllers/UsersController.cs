using System.Net.Mail;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Security;
using OneBase.Domain.Audit;
using OneBase.Domain.Identity;

namespace OneBase.Api.Controllers;

/// <summary>
/// «Настройки → Пользователи и роли». Удаления нет — сотрудника отключают (вход закрывается, история остаётся).
/// Назначать роль и менять пользователя можно, только если у вас есть все права этой роли (и текущей роли пользователя):
/// директор не выдаст себе или другим права администратора и не изменит администратора.
/// </summary>
[ApiController]
[Route("api/users")]
[HasPermission(Permissions.UsersManage)]
public sealed partial class UsersController(
    IAppDbContext db,
    IPasswordHasher<User> hasher,
    UserAccessCache access,
    IAuditLogger audit) : ControllerBase
{
    public const int MinPasswordLength = 8;

    public sealed record UserInput(
        string? Login,
        string? FirstName,
        string? LastName,
        string? Email,
        string? Phone,
        string? Position,
        Guid? RoleId,
        string? Password,
        bool IsActive = true);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var mine = MyPermissions();
        var roles = await RolesAsync(ct);
        var users = await db.Users.AsNoTracking()
            .Include(u => u.Roles)
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ThenBy(u => u.Login)
            .ToListAsync(ct);

        return Ok(new
        {
            Users = users.Select(u =>
            {
                var userRoles = roles.Where(r => u.Roles.Any(ur => ur.RoleId == r.Id)).ToList();
                return new
                {
                    u.Id,
                    u.Login,
                    u.FirstName,
                    u.LastName,
                    u.FullName,
                    u.Email,
                    u.Phone,
                    u.Position,
                    u.IsActive,
                    u.CreatedAt,
                    RoleId = userRoles.FirstOrDefault()?.Id,
                    RoleName = userRoles.Count == 0 ? null : string.Join(", ", userRoles.Select(r => r.Name)),
                    IsMe = u.Id == User.GetUserId(),
                    CanEdit = userRoles.All(r => r.Permissions.All(mine.Contains)),
                };
            }),
            Roles = roles.Select(r => new
            {
                r.Id,
                r.Name,
                r.Description,
                Department = r.DepartmentName,
                Permissions = r.Permissions.Select(p => new { Code = p, Label = SystemRoles.PermissionLabels.GetValueOrDefault(p, p) }),
                Users = users.Count(u => u.Roles.Any(ur => ur.RoleId == r.Id)),
                Assignable = r.Permissions.All(mine.Contains),
                OpensSettings = SystemRoles.SettingsPermissions.Any(r.Permissions.Contains),
            }),
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(UserInput input, CancellationToken ct)
    {
        var (values, error) = await ValidateAsync(input, null, ct);
        if (error is not null || values is null)
        {
            return BadRequest(new { error });
        }

        if (string.IsNullOrEmpty(input.Password) || input.Password.Length < MinPasswordLength)
        {
            return BadRequest(new { error = $"Пароль — не короче {MinPasswordLength} символов." });
        }

        var user = new User { Login = values.Login, FullName = string.Empty };
        Apply(user, values);
        user.PasswordHash = hasher.HashPassword(user, input.Password);
        user.Roles.Add(new UserRole { RoleId = values.Role.Id });
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        // В журнал — только идентификаторы и роль, без имени, телефона и почты.
        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "users.created", "user", user.Id.ToString(),
            new { role = values.Role.Name }, ct);
        return Ok(new { user.Id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UserInput input, CancellationToken ct)
    {
        var user = await db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
        {
            return NotFound();
        }

        var mine = MyPermissions();
        var roles = await RolesAsync(ct);
        var current = roles.Where(r => user.Roles.Any(ur => ur.RoleId == r.Id)).ToList();
        if (!current.All(r => r.Permissions.All(mine.Contains)))
        {
            return BadRequest(new { error = "У этого пользователя роль с правами, которых у вас нет, — изменить его может только администратор." });
        }

        var (validated, error) = await ValidateAsync(input, user, ct);
        if (error is not null || validated is null)
        {
            return BadRequest(new { error });
        }

        var values = validated;

        var isMe = user.Id == User.GetUserId();
        var roleChanged = current.Count != 1 || current[0].Id != values.Role.Id;
        if (isMe && roleChanged)
        {
            return BadRequest(new { error = "Свою роль изменить нельзя — иначе можно потерять доступ к этому разделу. Попросите другого администратора." });
        }

        if (isMe && !input.IsActive)
        {
            return BadRequest(new { error = "Нельзя отключить самого себя." });
        }

        var passwordChanged = !string.IsNullOrEmpty(input.Password);
        if (passwordChanged && input.Password!.Length < MinPasswordLength)
        {
            return BadRequest(new { error = $"Пароль — не короче {MinPasswordLength} символов." });
        }

        var wasActive = user.IsActive;
        Apply(user, values);
        user.IsActive = input.IsActive;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        if (passwordChanged)
        {
            user.PasswordHash = hasher.HashPassword(user, input.Password!);
        }

        if (roleChanged)
        {
            user.Roles.Clear();
            user.Roles.Add(new UserRole { UserId = user.Id, RoleId = values.Role.Id });
        }

        await db.SaveChangesAsync(ct);
        access.Invalidate(user.Id); // новая роль или отключение действуют сразу

        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "users.updated", "user", user.Id.ToString(),
            new
            {
                role = roleChanged ? values.Role.Name : null,
                active = wasActive != user.IsActive ? user.IsActive : (bool?)null,
                passwordChanged,
            }, ct);
        return Ok(new { user.Id });
    }

    private sealed record Values(string Login, string FirstName, string LastName, string? Email, string? Phone, string Position, RoleInfo Role);

    private sealed record RoleInfo(Guid Id, string Name, string? Description, Guid? DepartmentId, string? DepartmentName, IReadOnlyList<string> Permissions);

    private async Task<(Values? Values, string? Error)> ValidateAsync(UserInput input, User? existing, CancellationToken ct)
    {
        var login = (input.Login ?? string.Empty).Trim().ToLowerInvariant();
        var firstName = (input.FirstName ?? string.Empty).Trim();
        var lastName = (input.LastName ?? string.Empty).Trim();
        var position = (input.Position ?? string.Empty).Trim();
        var email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim().ToLowerInvariant();
        var phone = string.IsNullOrWhiteSpace(input.Phone) ? null : input.Phone.Trim();

        if (login.Length == 0 || firstName.Length == 0 || lastName.Length == 0 || position.Length == 0 || input.RoleId is null)
        {
            return (null, "Обязательные поля: логин, имя, фамилия, должность и роль.");
        }

        if (login != existing?.Login && !LoginPattern().IsMatch(login))
        {
            return (null, "Логин — от 3 до 64 символов: латинские буквы, цифры, точка, дефис, подчёркивание или @.");
        }

        if (firstName.Length > 100 || lastName.Length > 100 || position.Length > 150)
        {
            return (null, "Имя и фамилия — до 100 символов, должность — до 150.");
        }

        if (email is not null && (email.Length > 256 || !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email))
        {
            return (null, "Почта указана неверно.");
        }

        if (phone is not null && !PhonePattern().IsMatch(phone))
        {
            return (null, "Телефон — цифры, пробелы, скобки, дефисы и «+», от 7 до 32 символов.");
        }

        var roles = await RolesAsync(ct);
        var role = roles.FirstOrDefault(r => r.Id == input.RoleId);
        if (role is null)
        {
            return (null, "Такой роли нет.");
        }

        var mine = MyPermissions();
        if (!role.Permissions.All(mine.Contains))
        {
            return (null, $"Роль «{role.Name}» даёт права, которых у вас нет, — назначить её может только администратор.");
        }

        if (await db.Users.AnyAsync(u => u.Id != (existing == null ? Guid.Empty : existing.Id) && (u.Login == login || u.Email == login), ct))
        {
            return (null, $"Логин «{login}» уже занят.");
        }

        if (email is not null && await db.Users.AnyAsync(u => u.Id != (existing == null ? Guid.Empty : existing.Id) && (u.Email == email || u.Login == email), ct))
        {
            return (null, "Эта почта уже указана у другого пользователя.");
        }

        return (new Values(login, firstName, lastName, email, phone, position, role), null);
    }

    private static void Apply(User user, Values values)
    {
        user.Login = values.Login;
        user.FirstName = values.FirstName;
        user.LastName = values.LastName;
        user.FullName = Domain.Identity.User.FullNameOf(values.FirstName, values.LastName);
        user.Email = values.Email;
        user.Phone = values.Phone;
        user.Position = values.Position;
        user.DepartmentId = values.Role.DepartmentId; // отдел — по роли
    }

    private async Task<List<RoleInfo>> RolesAsync(CancellationToken ct) =>
        (await db.Roles.AsNoTracking()
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.Description,
                r.DepartmentId,
                DepartmentName = r.Department != null ? r.Department.Name : null,
                Permissions = r.Permissions.Select(p => p.Code).ToList(),
            })
            .ToListAsync(ct))
        .Select(r => new RoleInfo(r.Id, r.Name, r.Description, r.DepartmentId, r.DepartmentName,
            r.Permissions.OrderBy(p => Permissions.All.ToList().IndexOf(p)).ToList()))
        .OrderBy(r => SystemRoles.OrderOf(r.Name)).ThenBy(r => r.Name)
        .ToList();

    private HashSet<string> MyPermissions() => User.FindAll(OneBaseClaims.Permission).Select(c => c.Value).ToHashSet();

    [GeneratedRegex("^[a-z0-9._@-]{3,64}$")]
    private static partial Regex LoginPattern();

    [GeneratedRegex(@"^\+?[0-9 ()\-]{7,32}$")]
    private static partial Regex PhonePattern();
}
