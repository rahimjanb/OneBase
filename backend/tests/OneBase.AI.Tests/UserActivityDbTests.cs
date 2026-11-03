using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OneBase.Api.Auth;
using OneBase.Api.Controllers;
using OneBase.Application.Abstractions;
using OneBase.Application.Security;
using OneBase.Domain.Identity;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// «Был в сети» на настоящем PostgreSQL (ONEBASE_TEST_POSTGRES, иначе пропускается): запрос отмечает время, повторные в ту же минуту —
/// нет (одна запись в минуту), отметка не меняет «изменён»; в «Пользователи и роли» — был в сети, в сети (последние минуты, только у
/// включённых) и последний вход (самый поздний сеанс).
/// </summary>
public class UserActivityDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private UserActivity Activity() =>
        new(fixture.Services.GetRequiredService<IServiceScopeFactory>(), new MemoryCache(new MemoryCacheOptions()), NullLogger<UserActivity>.Instance);

    private static UsersController Users(IServiceProvider sp, Guid adminId)
    {
        var claims = new List<Claim> { new(OneBaseClaims.UserId, adminId.ToString()) };
        claims.AddRange(Permissions.All.Select(p => new Claim(OneBaseClaims.Permission, p)));
        return new UsersController(sp.GetRequiredService<IAppDbContext>(), new PasswordHasher<User>(),
            new UserAccessCache(sp.GetRequiredService<IServiceScopeFactory>(), new MemoryCache(new MemoryCacheOptions())), sp.GetRequiredService<IAuditLogger>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) } },
        };
    }

    private async Task<User> UserAsync(Func<OneBaseDbContext, User, Task>? prepare = null)
    {
        var s = Guid.NewGuid().ToString("N")[..6];
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var user = new User { Login = $"seen-{s}", FullName = $"Сотрудник {s}" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        if (prepare is not null)
        {
            await prepare(db, user);
            await db.SaveChangesAsync();
        }

        return user;
    }

    private async Task<(DateTimeOffset? Seen, DateTimeOffset? Updated)> SeenAsync(Guid id)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var u = await db.Users.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.LastSeenAt, x.UpdatedAt }).SingleAsync();
        return (u.LastSeenAt, u.UpdatedAt);
    }

    [PostgresFact]
    public async Task Requests_mark_the_user_online_at_most_once_a_minute()
    {
        var user = await UserAsync();
        Assert.Null((await SeenAsync(user.Id)).Seen);

        var activity = Activity();
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        await activity.TouchAsync(user.Id, default);
        var (seen, updated) = await SeenAsync(user.Id);
        Assert.NotNull(seen);
        Assert.True(seen >= before);
        Assert.Null(updated);

        // Ещё запросы в ту же минуту — без записи (проверяем: старое значение не перезаписано).
        var old = DateTimeOffset.UtcNow.AddHours(-2);
        using (var scope = fixture.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<OneBaseDbContext>().Users.Where(u => u.Id == user.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastSeenAt, old));
        }

        await activity.TouchAsync(user.Id, default);
        Assert.Equal(old.ToUnixTimeSeconds(), (await SeenAsync(user.Id)).Seen!.Value.ToUnixTimeSeconds());

        // Минута прошла (на другом экземпляре сервера отметки ещё нет) — запись снова.
        await Activity().TouchAsync(user.Id, default);
        Assert.True((await SeenAsync(user.Id)).Seen >= before);
    }

    [PostgresFact]
    public async Task Users_list_shows_online_last_seen_and_last_login()
    {
        var now = DateTimeOffset.UtcNow;
        var online = await UserAsync((db, u) =>
        {
            u.LastSeenAt = now.AddMinutes(-1);
            db.UserSessions.Add(new UserSession { UserId = u.Id, CreatedAt = now.AddDays(-3) });
            db.UserSessions.Add(new UserSession { UserId = u.Id, CreatedAt = now.AddHours(-5), RevokedAt = now.AddHours(-4) });
            return Task.CompletedTask;
        });
        var away = await UserAsync((_, u) =>
        {
            u.LastSeenAt = now.AddMinutes(-10);
            return Task.CompletedTask;
        });
        var disabled = await UserAsync((_, u) =>
        {
            u.LastSeenAt = now.AddSeconds(-30);
            u.IsActive = false;
            return Task.CompletedTask;
        });
        var never = await UserAsync();

        using var scope = fixture.Services.CreateScope();
        var result = await Users(scope.ServiceProvider, fixture.AdminId).List(default);
        var json = JsonSerializer.SerializeToElement(((OkObjectResult)result).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        JsonElement Row(Guid id) => json.GetProperty("users").EnumerateArray().Single(r => r.GetProperty("id").GetGuid() == id);

        Assert.True(Row(online.Id).GetProperty("online").GetBoolean());
        Assert.Equal(now.AddHours(-5).ToUnixTimeSeconds(), Row(online.Id).GetProperty("lastLoginAt").GetDateTimeOffset().ToUnixTimeSeconds());
        Assert.False(Row(away.Id).GetProperty("online").GetBoolean());
        Assert.Equal(now.AddMinutes(-10).ToUnixTimeSeconds(), Row(away.Id).GetProperty("lastSeenAt").GetDateTimeOffset().ToUnixTimeSeconds());
        Assert.False(Row(disabled.Id).GetProperty("online").GetBoolean());
        Assert.Equal(JsonValueKind.Null, Row(never.Id).GetProperty("lastSeenAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, Row(never.Id).GetProperty("lastLoginAt").ValueKind);
    }
}
