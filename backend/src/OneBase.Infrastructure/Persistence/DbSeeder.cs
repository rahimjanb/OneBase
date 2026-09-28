using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneBase.Application.Security;
using OneBase.Domain.AI;
using OneBase.Domain.Identity;

namespace OneBase.Infrastructure.Persistence;

/// <summary>Применяет миграции и создаёт базовые данные: отделы, роль Admin, первого администратора, гранты агентов.</summary>
public static class DbSeeder
{
    public const string AdminRole = "Admin";

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

        var admin = await db.Roles.Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Name == AdminRole, cancellationToken);
        if (admin is null)
        {
            admin = new Role { Name = AdminRole, Description = "Полный доступ" };
            db.Roles.Add(admin);
        }

        foreach (var code in Permissions.All.Except(admin.Permissions.Select(p => p.Code)))
        {
            admin.Permissions.Add(new RolePermission { Code = code });
        }

        foreach (var agent in AgentCodes)
        {
            if (!await db.AgentToolGrants.AnyAsync(g => g.AgentCode == agent && g.ToolName == "delegate_to_agent", cancellationToken))
            {
                db.AgentToolGrants.Add(new AgentToolGrant { AgentCode = agent, ToolName = "delegate_to_agent" });
            }
        }

        var email = config["Seed:AdminEmail"]?.Trim().ToLowerInvariant();
        var password = config["Seed:AdminPassword"];
        if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(password)
            && !await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            var user = new User { Email = email, FullName = "Administrator" };
            user.PasswordHash = services.GetRequiredService<IPasswordHasher<User>>().HashPassword(user, password);
            user.Roles.Add(new UserRole { Role = admin });
            db.Users.Add(user);
            logger.LogInformation("Создан администратор {Email}", email);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
