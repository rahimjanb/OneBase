using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneBase.Application.Security;
using OneBase.Domain.AI;
using OneBase.Domain.Identity;
using OneBase.Domain.Sales;

namespace OneBase.Infrastructure.Persistence;

/// <summary>Применяет миграции и создаёт базовые данные: отделы, роли (SystemRoles), первого администратора, гранты агентов.</summary>
public static class DbSeeder
{
    public const string AdminRole = SystemRoles.Admin;

    private static readonly (string Code, string Name)[] Departments =
    [
        ("hr", "HR"),
        ("sales", "Продажи"),
        ("production", "Производство"),
        ("finance", "Финансы"),
        ("supply", "Снабжение"),
        ("marketing", "Маркетинг"),
    ];

    /// <summary>Коды агентов, которым выдаётся инструмент delegate_to_agent (Agent-to-Agent).</summary>
    private static readonly string[] AgentCodes = ["director", .. Departments.Select(d => d.Code)];

    public static async Task SeedAsync(IServiceProvider rootServices, CancellationToken cancellationToken = default)
    {
        using var scope = rootServices.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<OneBaseDbContext>();
        var config = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DbSeeder));

        await db.Database.MigrateAsync(cancellationToken);

        foreach (var (code, name) in Departments)
        {
            if (!await db.Departments.AnyAsync(d => d.Code == code, cancellationToken))
            {
                db.Departments.Add(new Department { Code = code, Name = name });
            }
        }

        await db.SaveChangesAsync(cancellationToken); // отделы нужны ролям ниже
        var departments = await db.Departments.ToDictionaryAsync(d => d.Code, d => d.Id, cancellationToken);

        // Роли: недостающие создаются с правами по умолчанию; у администратора всегда все права.
        // Права уже созданных ролей (кроме администратора) не перезаписываются.
        Role? admin = null;
        foreach (var definition in SystemRoles.All)
        {
            var isAdmin = definition.Name == SystemRoles.Admin;
            var role = await db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Name == definition.Name, cancellationToken);
            if (role is null && isAdmin)
            {
                role = await db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Name == SystemRoles.LegacyAdmin, cancellationToken);
                if (role is not null)
                {
                    role.Name = SystemRoles.Admin;
                }
            }

            var created = role is null;
            if (role is null)
            {
                role = new Role { Name = definition.Name };
                db.Roles.Add(role);
            }

            role.Description ??= definition.Description;
            if (isAdmin)
            {
                role.Description = definition.Description;
            }

            if (definition.DepartmentCode is { } code && role.DepartmentId is null && departments.TryGetValue(code, out var departmentId))
            {
                role.DepartmentId = departmentId;
            }

            if (created || isAdmin)
            {
                foreach (var permission in definition.Permissions.Except(role.Permissions.Select(p => p.Code)))
                {
                    role.Permissions.Add(new RolePermission { Code = permission });
                }
            }

            if (isAdmin)
            {
                admin = role;
            }
        }

        // Новые права существующих ролей (SystemRoles.Upgrades).
        foreach (var (roleName, permission) in SystemRoles.Upgrades)
        {
            var role = db.Roles.Local.FirstOrDefault(r => r.Name == roleName)
                       ?? await db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken);
            if (role is not null && role.Permissions.All(p => p.Code != permission))
            {
                role.Permissions.Add(new RolePermission { Code = permission });
            }
        }

        foreach (var agent in AgentCodes)
        {
            if (!await db.AgentToolGrants.AnyAsync(g => g.AgentCode == agent && g.ToolName == "delegate_to_agent", cancellationToken))
            {
                db.AgentToolGrants.Add(new AgentToolGrant { AgentCode = agent, ToolName = "delegate_to_agent" });
            }
        }

        var targetKeys = await db.SalesTargets.Select(t => t.Key).ToListAsync(cancellationToken);
        foreach (var (key, value) in SalesTargetKeys.Defaults.Where(t => !targetKeys.Contains(t.Key)))
        {
            db.SalesTargets.Add(new SalesTarget { Key = key, Value = value });
        }

        // Seed:AdminEmail — логин первого администратора (если похож на почту — и почта).
        var login = config["Seed:AdminEmail"]?.Trim().ToLowerInvariant();
        var password = config["Seed:AdminPassword"];
        if (!string.IsNullOrEmpty(login) && !string.IsNullOrEmpty(password)
            && !await db.Users.AnyAsync(u => u.Login == login || u.Email == login, cancellationToken))
        {
            var user = new User
            {
                Login = login,
                Email = login.Contains('@') ? login : null,
                FirstName = "Administrator",
                FullName = "Administrator",
                Position = SystemRoles.Admin,
            };
            user.PasswordHash = services.GetRequiredService<IPasswordHasher<User>>().HashPassword(user, password);
            user.Roles.Add(new UserRole { Role = admin! });
            db.Users.Add(user);
            logger.LogInformation("Создан первый администратор");
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
