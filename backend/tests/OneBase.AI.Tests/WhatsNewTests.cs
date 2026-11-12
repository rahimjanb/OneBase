using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Api.Auth;
using OneBase.Api.Controllers;
using OneBase.Application.Abstractions;
using OneBase.Application.Security;
using OneBase.Domain.Identity;
using OneBase.Infrastructure.Persistence;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>
/// Окно «Что нового»: версия — дата релиза; закрытое окно записывается в профиль (не появится и на другом устройстве), более ранняя версия
/// отметку не откатывает.
/// </summary>
public class WhatsNewTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    [Fact]
    public void Version_is_a_release_date_and_only_a_later_one_moves_the_mark()
    {
        Assert.Equal("2026-11-13", WhatsNew.Normalize(" 2026-11-13 "));
        Assert.Equal("2026-11-13.2", WhatsNew.Normalize("2026-11-13.2"));
        foreach (var bad in new[] { null, "", "3.0", "2026-11-13; drop", "2026/11/13", "2026-11-13.123", new string('1', 40) })
        {
            Assert.Null(WhatsNew.Normalize(bad));
        }

        Assert.Equal("2026-11-13", WhatsNew.Next(null, "2026-11-13"));
        Assert.Equal("2026-12-01", WhatsNew.Next("2026-11-13", "2026-12-01"));
        Assert.Equal("2026-12-01", WhatsNew.Next("2026-12-01", "2026-11-13")); // старое окно из профиля не откатывает
        Assert.Equal("2026-11-13.2", WhatsNew.Next("2026-11-13", "2026-11-13.2"));
        Assert.Equal("2026-11-13.10", WhatsNew.Next("2026-11-13.9", "2026-11-13.10"));
        Assert.Equal("2026-11-13", WhatsNew.Next("2026-11-13", "2026-11-13"));
    }

    [PostgresFact]
    public async Task Closing_the_window_is_saved_in_the_profile_and_an_older_version_does_not_roll_it_back()
    {
        Guid userId;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var user = db.Users.Add(new User { Login = $"wn-{Guid.NewGuid():N}"[..20], FullName = "Что нового" }).Entity;
            await db.SaveChangesAsync();
            userId = user.Id;
        }

        async Task<IActionResult> Close(string? version)
        {
            using var scope = fixture.Services.CreateScope();
            var sp = scope.ServiceProvider;
            // Отметке нужны только база и пользователь: вход, токены и пароли в ней не участвуют.
            var controller = new AuthController(sp.GetRequiredService<IAppDbContext>(), null!, null!, null!, sp.GetRequiredService<IAuditLogger>())
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(OneBaseClaims.UserId, userId.ToString())], "test")) },
                },
            };
            return await controller.WhatsNewSeen(new AuthController.WhatsNewSeenRequest(version), default);
        }

        async Task<string?> Seen()
        {
            using var scope = fixture.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<OneBaseDbContext>().Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.WhatsNewSeen).SingleAsync();
        }

        Assert.Null(await Seen());
        Assert.IsType<NoContentResult>(await Close("2026-11-13"));
        Assert.Equal("2026-11-13", await Seen());
        Assert.IsType<NoContentResult>(await Close("2026-10-01"));                 // окно старого релиза — отметка не откатывается
        Assert.Equal("2026-11-13", await Seen());
        Assert.IsType<BadRequestObjectResult>(await Close("3.0"));
        Assert.IsType<BadRequestObjectResult>(await Close(null));
        Assert.Equal("2026-11-13", await Seen());
    }
}
