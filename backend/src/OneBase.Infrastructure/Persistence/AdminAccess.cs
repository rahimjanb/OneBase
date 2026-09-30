using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Security;
using OneBase.Domain.Audit;
using OneBase.Domain.Identity;

namespace OneBase.Infrastructure.Persistence;

/// <summary>
/// Аварийный доступ администратора: `dotnet OneBase.Api.dll reset-admin ЛОГИН`, новый пароль — первой строкой stdin
/// (в аргументах его не видно в списке процессов и истории команд). Для того, у кого есть доступ к серверу.
/// Пользователь есть — новый пароль, включается, получает роль «Администратор»; нет — создаётся администратором.
/// Заодно показывает, восстановлены ли данные: пустая база обычно значит, что дамп не восстановился.
/// </summary>
public static class AdminAccess
{
    public const int MinPasswordLength = 8;

    public static async Task<int> ResetAsync(IServiceProvider services, string? loginArg, TextReader input)
    {
        var login = (loginArg ?? string.Empty).Trim().ToLowerInvariant();
        if (login.Length is < 1 or > 64)
        {
            Console.Error.WriteLine("Укажите логин: reset-admin ЛОГИН (пароль — в стандартный ввод).");
            return 1;
        }

        var password = input.ReadLine() ?? string.Empty;
        if (password.Length < MinPasswordLength)
        {
            Console.Error.WriteLine($"Пароль — не короче {MinPasswordLength} символов. Ничего не изменено.");
            return 1;
        }

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        var admin = await db.Roles.FirstOrDefaultAsync(r => r.Name == SystemRoles.Admin);
        if (admin is null)
        {
            Console.Error.WriteLine("Роли «Администратор» нет — база не подготовлена. Запустите API (docker compose up -d) и повторите.");
            return 1;
        }

        var user = await db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Login == login || u.Email == login);
        var created = user is null;
        if (user is null)
        {
            user = new User
            {
                Login = login,
                Email = login.Contains('@') ? login : null,
                FirstName = SystemRoles.Admin,
                FullName = SystemRoles.Admin,
                Position = SystemRoles.Admin,
            };
            db.Users.Add(user);
        }

        user.PasswordHash = hasher.HashPassword(user, password);
        user.IsActive = true;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        if (user.Roles.All(r => r.RoleId != admin.Id))
        {
            user.Roles.Add(new UserRole { UserId = user.Id, RoleId = admin.Id });
        }

        db.AuditLogs.Add(new AuditLog
        {
            ActorType = ActorType.System,
            ActorId = "reset-admin",
            Action = created ? "users.admin.created" : "users.admin.reset",
            EntityType = "user",
            EntityId = user.Id.ToString(),
        });
        await db.SaveChangesAsync();

        Console.WriteLine(created
            ? $"Создан администратор «{user.Login}». Войдите с этим логином и новым паролем."
            : $"Пользователь «{user.Login}»: пароль изменён, вход включён, роль «Администратор». Войдите с новым паролем.");

        var users = await db.Users.CountAsync();
        var orders = await db.LinkoOrders.CountAsync();
        Console.WriteLine($"В базе: пользователей {users}, заказов Linko {orders:N0}.");
        if (orders == 0)
        {
            Console.WriteLine("Заказов Linko нет — похоже, дамп базы не восстановлен. Порядок восстановления — docs/deploy.md.");
        }

        return 0;
    }
}
