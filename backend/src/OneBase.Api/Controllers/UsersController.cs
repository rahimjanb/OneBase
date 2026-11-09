using System.Security.Claims;
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
/// директор не выдаст себе или другим права администратора и не изменит администратора. Разделы (UserSections) и права в разделах отделов
/// и настройках (UserRights) отмечаются галочками: роль подставляет свои, выдать можно только то, что есть у вас; совпали с ролью —
/// хранится «как у роли» (null). Свои разделы и права изменить нельзя.
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
        bool IsActive = true,
        IReadOnlyList<string>? Sections = null,
        Guid? DepartmentId = null,
        Guid? HrEmployeeId = null,
        bool NoVerifix = false,
        IReadOnlyList<string>? Rights = null);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var mine = MyPermissions();
        var roles = await RolesAsync(ct);
        var users = await db.Users.AsNoTracking()
            .Include(u => u.Roles)
            .AsSplitQuery()
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ThenBy(u => u.Login)
            .ToListAsync(ct);
        var links = await db.HrEmployees.AsNoTracking().Where(e => e.UserId != null)
            .Select(e => new { UserId = e.UserId!.Value, e.Id, e.FullName }).ToDictionaryAsync(e => e.UserId, ct);
        var departments = await db.Departments.AsNoTracking().OrderBy(d => d.Name).Select(d => new { d.Id, d.Code, d.Name }).ToListAsync(ct);
        // Последний вход — самый поздний сеанс входа (на любом устройстве); «в сети» — запрос за последние минуты (UserActivity).
        var logins = await db.UserSessions.AsNoTracking().GroupBy(s => s.UserId).Select(g => new { g.Key, Last = g.Max(s => s.CreatedAt) })
            .ToDictionaryAsync(x => x.Key, x => x.Last, ct);
        var onlineSince = DateTimeOffset.UtcNow - UserActivity.Online;

        return Ok(new
        {
            Users = users.Select(u =>
            {
                var userRoles = roles.Where(r => u.Roles.Any(ur => ur.RoleId == r.Id)).ToList();
                var effective = UserSections.Effective(userRoles.SelectMany(r => r.Permissions), u.Sections, u.Rights);
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
                    u.LastSeenAt,
                    Online = u.IsActive && u.LastSeenAt >= onlineSince,
                    LastLoginAt = logins.TryGetValue(u.Id, out var login) ? login : (DateTimeOffset?)null,
                    RoleId = userRoles.FirstOrDefault()?.Id,
                    RoleName = userRoles.Count == 0 ? null : string.Join(", ", userRoles.Select(r => r.Name)),
                    IsMe = u.Id == User.GetUserId(),
                    CanEdit = userRoles.All(r => r.Permissions.All(mine.Contains)) && effective.All(mine.Contains),
                    Sections = UserSections.Of(effective),
                    SectionsCustom = u.Sections is not null,
                    Rights = UserRights.Of(effective),
                    RightsCustom = u.Rights is not null,
                    u.DepartmentId,
                    HrEmployee = links.TryGetValue(u.Id, out var link) ? new { link.Id, Name = link.FullName } : null,
                };
            }),
            Roles = roles.Select(r => new
            {
                r.Id,
                r.Name,
                r.Description,
                Department = r.DepartmentName,
                r.DepartmentId,
                IsEmployee = r.Name == SystemRoles.EmployeeRole,
                Permissions = r.Permissions.Select(p => new { Code = p, Label = SystemRoles.PermissionLabels.GetValueOrDefault(p, p) }),
                Users = users.Count(u => u.Roles.Any(ur => ur.RoleId == r.Id)),
                Assignable = r.Permissions.All(mine.Contains),
                OpensSettings = SystemRoles.SettingsPermissions.Any(r.Permissions.Contains),
                Sections = UserSections.Of(r.Permissions),
                Rights = UserRights.Of(UserSections.Effective(r.Permissions, null)),
            }),
            // Каталог разделов; Grantable — можете ли вы выдать раздел (его права есть у вас).
            Sections = UserSections.All.Select(x => new { x.Key, x.Title, x.Description, Grantable = x.Grant.All(mine.Contains) }),
            // Права в разделах отделов и настройках по группам; Grantable — право (и то, что включается с ним) есть у вас.
            Rights = UserRights.All.Select(x => new
            {
                x.Key, x.Group, x.Title, x.Description, x.Requires, x.NeedsSection, Grantable = mine.Contains(x.Key) && x.Requires.All(mine.Contains),
            }),
            RightGroups = UserRights.Groups,
            Departments = departments,
            // Verifix подключён (сотрудники загружены) — при создании пользователя выбирается его сотрудник или «Без привязки к Verifix».
            VerifixAvailable = await db.HrEmployees.AnyAsync(ct),
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

        if (values.HrEmployee is null && !input.NoVerifix && await db.HrEmployees.AnyAsync(ct))
        {
            return BadRequest(new { error = "Выберите сотрудника в Verifix или отметьте «Без привязки к Verifix»." });
        }

        var user = new User { Login = values.Login, FullName = string.Empty };
        Apply(user, values);
        user.Sections = values.Sections;
        user.Rights = values.Rights;
        user.PasswordHash = hasher.HashPassword(user, input.Password);
        user.Roles.Add(new UserRole { RoleId = values.Role.Id });
        db.Users.Add(user);
        await LinkAsync(user.Id, values.HrEmployee, input.NoVerifix, ct);
        await db.SaveChangesAsync(ct);

        // В журнал — только идентификаторы и роль, без имени, телефона и почты.
        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "users.created", "user", user.Id.ToString(),
            new { role = values.Role.Name, sections = values.Sections, rights = values.Rights, verifix = values.HrEmployee }, ct);
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
        var currentEffective = UserSections.Effective(current.SelectMany(r => r.Permissions), user.Sections, user.Rights);
        if (!current.All(r => r.Permissions.All(mine.Contains)) || !currentEffective.All(mine.Contains))
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

        var sectionsChanged = !UserSections.Of(UserSections.Effective(values.Role.Permissions, values.Sections)).SequenceEqual(UserSections.Of(currentEffective));
        if (isMe && sectionsChanged)
        {
            return BadRequest(new { error = "Свои разделы изменить нельзя — попросите другого администратора." });
        }

        var newRights = UserRights.Of(UserSections.Effective(values.Role.Permissions, values.Sections, values.Rights));
        var rightsChanged = !newRights.SequenceEqual(UserRights.Of(currentEffective));
        if (isMe && rightsChanged)
        {
            return BadRequest(new { error = "Свои права изменить нельзя — попросите другого администратора." });
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
        user.Sections = values.Sections;
        user.Rights = values.Rights;
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

        var verifixChanged = await LinkAsync(user.Id, values.HrEmployee, input.NoVerifix, ct);
        await db.SaveChangesAsync(ct);
        access.Invalidate(user.Id); // новая роль, разделы или отключение действуют сразу
        if (passwordChanged)
        {
            // Новый пароль закрывает старые входы на всех устройствах; свой текущий вход администратор сохраняет.
            Guid? keep = Guid.TryParse(User.FindFirstValue(OneBaseClaims.Session), out var sid) && user.Id == User.GetUserId() ? sid : null;
            await db.RevokeAllAsync(user.Id, keep, ct);
        }
        if (wasActive && !user.IsActive)
        {
            await db.PushSubscriptions.Where(s => s.UserId == user.Id).ExecuteDeleteAsync(ct);
        }

        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "users.updated", "user", user.Id.ToString(),
            new
            {
                role = roleChanged ? values.Role.Name : null,
                sections = sectionsChanged ? UserSections.Of(UserSections.Effective(values.Role.Permissions, values.Sections)) : null,
                rights = rightsChanged ? newRights : null,
                active = wasActive != user.IsActive ? user.IsActive : (bool?)null,
                passwordChanged,
                verifix = verifixChanged ? values.HrEmployee : null,
            }, ct);
        return Ok(new { user.Id });
    }

    /// <summary>
    /// Sections и Rights — что сохранить: null — как у роли. DepartmentId — отдел роли, а у роли без отдела — выбранный. HrEmployee — сотрудник
    /// Verifix.
    /// </summary>
    private sealed record Values(string Login, string FirstName, string LastName, string? Email, string? Phone, string Position, RoleInfo Role, List<string>? Sections,
        Guid? DepartmentId, Guid? HrEmployee, List<string>? Rights = null);

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

        // Разделы: null — как у роли; выбранные совпали с разделами роли — тоже «как у роли» (пользователь пойдёт за ролью).
        List<string>? sections = null;
        if (input.Sections is not null)
        {
            var unknown = input.Sections.Where(k => UserSections.Find(k?.Trim()) is null).ToList();
            if (unknown.Count > 0)
            {
                return (null, $"Неизвестный раздел: {string.Join(", ", unknown)}.");
            }

            var chosen = UserSections.Normalize(input.Sections);
            sections = chosen.SequenceEqual(UserSections.Of(role.Permissions)) ? null : chosen;
            var foreign = UserSections.All.Where(x => chosen.Contains(x.Key) && UserSections.Effective(role.Permissions, chosen).Intersect(x.Permissions).Any(p => !mine.Contains(p))).ToList();
            if (foreign.Count > 0)
            {
                return (null, $"Раздел «{foreign[0].Title}» выдать нельзя: у вас самих нет к нему доступа.");
            }
        }

        // Права в разделах отделов и настройках: null — как у роли; с включаемыми вместе (закупки — и просмотр); совпали с ролью — «как у роли».
        List<string>? rights = null;
        if (input.Rights is not null)
        {
            var unknown = input.Rights.Where(k => UserRights.Find(k?.Trim()) is null).ToList();
            if (unknown.Count > 0)
            {
                return (null, $"Неизвестное право: {string.Join(", ", unknown)}.");
            }

            var chosen = UserRights.Normalize(input.Rights);
            if (UserRights.All.FirstOrDefault(x => chosen.Contains(x.Key) && !mine.Contains(x.Key)) is { } foreign)
            {
                return (null, $"Право «{foreign.Title}» выдать нельзя: у вас самих его нет.");
            }

            var bySections = UserSections.Effective(role.Permissions, sections);
            if (UserRights.All.FirstOrDefault(x => chosen.Contains(x.Key) && x.Needs is not null && !bySections.Contains(x.Needs)) is { } needs)
            {
                return (null, $"«{needs.Title}» действует только вместе с разделом «{UserSections.Find(needs.NeedsSection)?.Title}» — отметьте раздел.");
            }

            rights = chosen.SequenceEqual(UserRights.Of(bySections)) ? null : chosen;
        }

        if (await db.Users.AnyAsync(u => u.Id != (existing == null ? Guid.Empty : existing.Id) && (u.Login == login || u.Email == login), ct))
        {
            return (null, $"Логин «{login}» уже занят.");
        }

        if (email is not null && await db.Users.AnyAsync(u => u.Id != (existing == null ? Guid.Empty : existing.Id) && (u.Email == email || u.Login == email), ct))
        {
            return (null, "Эта почта уже указана у другого пользователя.");
        }

        // Отдел — по роли; у роли без отдела (директор, «Сотрудник») — выбранный в карточке. «Сотруднику» отдел обязателен: по нему видны задачи и файлы отдела.
        var department = role.DepartmentId;
        if (department is null && input.DepartmentId is { } picked)
        {
            if (!await db.Departments.AnyAsync(d => d.Id == picked, ct))
            {
                return (null, "Такого отдела нет.");
            }

            department = picked;
        }

        if (department is null && role.Name == SystemRoles.EmployeeRole)
        {
            return (null, "Выберите отдел сотрудника.");
        }

        // Сотрудник Verifix: работающий и не привязанный к другому пользователю (свою прежнюю привязку можно оставить).
        Guid? hrEmployee = null;
        if (!input.NoVerifix && input.HrEmployeeId is { } employeeId)
        {
            var employee = await db.HrEmployees.AsNoTracking().Where(e => e.Id == employeeId).Select(e => new { e.Status, e.UserId }).FirstOrDefaultAsync(ct);
            if (employee is null)
            {
                return (null, "Сотрудник Verifix не найден.");
            }

            var mineLink = existing is not null && employee.UserId == existing.Id;
            if (employee.UserId is { } other && !mineLink)
            {
                var name = await db.Users.AsNoTracking().Where(u => u.Id == other).Select(u => u.FullName).FirstOrDefaultAsync(ct);
                return (null, $"Этот сотрудник Verifix уже привязан к пользователю «{name}».");
            }

            if (!mineLink && employee.Status != Domain.Hr.HrEmployeeStatus.Working)
            {
                return (null, "Привязать можно только работающего сотрудника Verifix.");
            }

            hrEmployee = employeeId;
        }
        else if (!input.NoVerifix && existing is not null)
        {
            // Без выбора и без галочки — прежняя привязка остаётся.
            hrEmployee = await db.HrEmployees.AsNoTracking().Where(e => e.UserId == existing.Id).Select(e => (Guid?)e.Id).FirstOrDefaultAsync(ct);
        }

        return (new Values(login, firstName, lastName, email, phone, position, role, sections, department, hrEmployee, rights), null);
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
        user.DepartmentId = values.DepartmentId; // отдел — по роли, у роли без отдела — выбранный
    }

    /// <summary>Привязка к сотруднику Verifix: один пользователь — один сотрудник. Возвращает, изменилась ли привязка.</summary>
    private async Task<bool> LinkAsync(Guid userId, Guid? employeeId, bool noVerifix, CancellationToken ct)
    {
        var current = await db.HrEmployees.Where(e => e.UserId == userId).ToListAsync(ct);
        var target = noVerifix ? null : employeeId;
        if (current.Count == 1 && current[0].Id == target || current.Count == 0 && target is null)
        {
            return false;
        }

        foreach (var e in current)
        {
            e.UserId = null;
        }

        if (target is { } id && await db.HrEmployees.FirstOrDefaultAsync(e => e.Id == id, ct) is { } employee)
        {
            employee.UserId = userId;
        }

        return true;
    }

    /// <summary>
    /// Сотрудники Verifix для привязки: работающие, по ФИО или телефону (до 20). Для подстановки в форму — имя, фамилия, телефон, должность и
    /// отдел OneBase, к которому привязано подразделение Verifix; LinkedTo — уже привязан к другому пользователю.
    /// </summary>
    [HttpGet("hr-employees")]
    public async Task<IActionResult> HrEmployees([FromQuery] string? q, CancellationToken ct)
    {
        var query = db.HrEmployees.AsNoTracking().Where(e => e.Status == Domain.Hr.HrEmployeeStatus.Working);
        var text = (q ?? string.Empty).Trim();
        if (text.Length > 0)
        {
            var digits = new string(text.Where(char.IsDigit).ToArray());
            var lower = text.ToLower();
            var tail = digits.Length > 9 ? digits[^9..] : digits;
            query = digits.Length >= 4
                ? query.Where(e => e.Phone != null && e.Phone.Contains(tail))
                : query.Where(e => e.FullName.ToLower().Contains(lower));
        }

        var employees = await query.OrderBy(e => e.FullName).Take(20)
            .Select(e => new { e.Id, e.FullName, e.FirstName, e.LastName, e.Phone, e.JobId, e.DivisionId, e.UserId })
            .ToListAsync(ct);
        var refIds = employees.SelectMany(e => new[] { e.JobId, e.DivisionId }).OfType<long>().Distinct().ToList();
        var refs = await db.HrReferences.AsNoTracking()
            .Where(r => refIds.Contains(r.VerifixId) && (r.Kind == Domain.Hr.HrReferenceKinds.Job || r.Kind == Domain.Hr.HrReferenceKinds.Division))
            .Select(r => new { r.Kind, r.VerifixId, r.Name, r.DepartmentCode }).ToListAsync(ct);
        var departments = await db.Departments.AsNoTracking().ToDictionaryAsync(d => d.Code, d => d.Id, ct);
        var userIds = employees.Select(e => e.UserId).OfType<Guid>().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        return Ok(employees.Select(e =>
        {
            var job = refs.FirstOrDefault(r => r.Kind == Domain.Hr.HrReferenceKinds.Job && r.VerifixId == e.JobId);
            var division = refs.FirstOrDefault(r => r.Kind == Domain.Hr.HrReferenceKinds.Division && r.VerifixId == e.DivisionId);
            return new
            {
                e.Id,
                Name = e.FullName,
                e.FirstName,
                e.LastName,
                e.Phone,
                Job = job?.Name,
                Division = division?.Name,
                DepartmentId = division?.DepartmentCode is { } code && departments.TryGetValue(code, out var dep) ? dep : (Guid?)null,
                LinkedUserId = e.UserId,
                LinkedTo = e.UserId is { } u ? users.GetValueOrDefault(u) : null,
            };
        }));
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
