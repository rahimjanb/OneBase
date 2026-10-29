using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Api.Files;
using OneBase.Application.Files;
using OneBase.Domain.Identity;
using OneBase.Infrastructure.Files;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// Секретная ссылка отдела на настоящем WebDAV (WebDavHandler, база PostgreSQL): папка открывается и пишется без логина и пароля, ни один
/// ответ не просит пароль (иначе Word и Excel из Microsoft 365 блокируют файл), служебные запросы Office к корню — 404, Проводник у корня
/// по-прежнему входит по паролю; новая ссылка отменяет старую, отзыв — обе; старые подключения получают ссылку при открытии окна.
/// </summary>
public class FileLinkTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private const string Word = "Microsoft Office Word 2014";

    [PostgresFact]
    public async Task Department_link_opens_the_folder_without_any_password_prompt()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var connections = sp.GetRequiredService<FileConnectionsService>();
        var options = sp.GetRequiredService<FilesOptions>();
        var domain = options.PublicDomain;
        options.PublicDomain = "files.1base.uz";
        try
        {
            var department = await db.Departments.FirstOrDefaultAsync(d => d.Code == "supply");
            if (department is null)
            {
                department = new Department { Code = "supply", Name = "Снабжение" };
                db.Departments.Add(department);
                await db.SaveChangesAsync();
            }

            var admin = new FileActor(fixture.AdminId, null, "Админ", true, true, true, new HashSet<Guid>(), [], CanReadAll: true);
            var view = await connections.CreateAsync(admin, department, default);
            Assert.Equal("https://files.1base.uz/k/••••••••/supply", view.LinkAddress);
            Assert.DoesNotContain(view.Guide, s => s.Contains("/k/") && !s.Contains("••"));

            var link = await connections.RevealLinkAsync(admin, department, default);
            var token = link.Address.Split('/')[4];
            Assert.Equal($"https://files.1base.uz/k/{token}/supply", link.Address);
            Assert.Equal($@"\\files.1base.uz@SSL\k\{token}\supply", link.UncPath);

            // Проводник по ссылке: папка отдела видна без логина, адреса в ответе — внутри той же ссылки.
            var list = await DavAsync(sp, "PROPFIND", $"/k/{token}/supply/", headers: new() { ["Depth"] = "1" });
            Assert.Equal(207, list.Status);
            Assert.Contains($"/k/{token}/supply/", list.Body);
            var root = await DavAsync(sp, "PROPFIND", $"/k/{token}/", headers: new() { ["Depth"] = "1" });
            Assert.Equal(207, root.Status);
            Assert.Contains($"/k/{token}/supply/", root.Body);

            // Word по ссылке: сохраняет и открывает файл, пароль не спрашивается.
            var put = await DavAsync(sp, "PUT", $"/k/{token}/supply/Заявка.docx", "содержимое заявки", Word);
            Assert.InRange(put.Status, 200, 204);
            var get = await DavAsync(sp, "GET", $"/k/{token}/supply/Заявка.docx", agent: Word);
            Assert.Equal((200, "содержимое заявки"), (get.Status, get.Body));
            var moved = await DavAsync(sp, "MOVE", $"/k/{token}/supply/Заявка.docx", agent: Word,
                headers: new() { ["Destination"] = $"https://files.1base.uz/k/{token}/supply/Заявка%202.docx" });
            Assert.InRange(moved.Status, 201, 204);
            Assert.Equal(200, (await DavAsync(sp, "HEAD", $"/k/{token}/supply/Заявка 2.docx", agent: Word)).Status);

            // Служебные запросы Office к корню и FrontPage — 404 без запроса пароля; Проводник у корня — 401 с паролем, как раньше.
            foreach (var probe in new[] { ("PROPFIND", "/"), ("GET", "/_vti_inf.html"), ("POST", "/_vti_bin/shtml.dll/_vti_rpc") })
            {
                var answer = await DavAsync(sp, probe.Item1, probe.Item2, agent: probe.Item2 == "/" ? Word : "MSFrontPage/16.0");
                Assert.Equal(404, answer.Status);
                Assert.False(answer.Headers.ContainsKey("WWW-Authenticate"));
            }

            // Корень без входа — пустая папка (по нему Windows проверяет адрес https://…/k/{код}/отдел); логин отдела — у папки отдела.
            var rootAnonymous = await DavAsync(sp, "PROPFIND", "/", agent: "Microsoft-WebDAV-MiniRedir/10.0.26200");
            Assert.Equal(207, rootAnonymous.Status);
            Assert.DoesNotContain("supply", rootAnonymous.Body);
            var explorer = await DavAsync(sp, "PROPFIND", "/supply/", agent: "Microsoft-WebDAV-MiniRedir/10.0.26200");
            Assert.Equal(401, explorer.Status);
            Assert.StartsWith("Basic", explorer.Headers.WWWAuthenticate.ToString());

            // Неверная ссылка — 404, тоже без запроса пароля.
            var wrong = await DavAsync(sp, "PROPFIND", "/k/abcdefghijkmnpqrstuvwxyz23456789/supply/", agent: Word);
            Assert.Equal(404, wrong.Status);
            Assert.False(wrong.Headers.ContainsKey("WWW-Authenticate"));

            // Новая ссылка: старая перестаёт работать сразу, новая открывает ту же папку.
            await connections.RegenerateLinkAsync(admin, department, default);
            Assert.Equal(404, (await DavAsync(sp, "PROPFIND", $"/k/{token}/supply/")).Status);
            var fresh = (await connections.RevealLinkAsync(admin, department, default)).Address.Split('/')[4];
            Assert.NotEqual(token, fresh);
            Assert.Equal(207, (await DavAsync(sp, "PROPFIND", $"/k/{fresh}/supply/")).Status);

            // Подключение, созданное до ссылок, получает её при первом открытии окна.
            await db.FileConnections.ExecuteUpdateAsync(s => s.SetProperty(c => c.LinkTokenHash, (string?)null).SetProperty(c => c.ProtectedLinkToken, (string?)null));
            await connections.GetAsync(admin, department, default);
            Assert.NotNull(await db.FileConnections.AsNoTracking().Select(c => c.LinkTokenHash).SingleAsync());
            var issued = (await connections.RevealLinkAsync(admin, department, default)).Address.Split('/')[4];
            Assert.Equal(207, (await DavAsync(sp, "PROPFIND", $"/k/{issued}/supply/")).Status);

            // Отзыв подключения — ссылка больше не работает.
            await connections.RevokeAsync(admin, department, default);
            Assert.Equal(404, (await DavAsync(sp, "PROPFIND", $"/k/{issued}/supply/")).Status);
            Assert.Contains(await db.AuditLogs.AsNoTracking().Select(a => a.Action).ToListAsync(), a => a == "files.connection.link.revealed");
        }
        finally
        {
            options.PublicDomain = domain;
        }
    }

    private static async Task<(int Status, string Body, IHeaderDictionary Headers)> DavAsync(IServiceProvider sp, string method, string path, string? body = null,
        string? agent = null, Dictionary<string, string>? headers = null)
    {
        using var scope = sp.CreateScope();
        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        http.Request.Method = method;
        http.Request.Path = path;
        http.Request.Headers["X-Dav-Root"] = "/";
        if (agent is not null)
        {
            http.Request.Headers.UserAgent = agent;
        }

        foreach (var (name, value) in headers ?? [])
        {
            http.Request.Headers[name] = value;
        }

        if (body is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            http.Request.Body = new MemoryStream(bytes);
            http.Request.ContentLength = bytes.Length;
        }

        http.Response.Body = new MemoryStream();
        await WebDavHandler.HandleAsync(http);
        http.Response.Body.Position = 0;
        return (http.Response.StatusCode, await new StreamReader(http.Response.Body).ReadToEndAsync(), http.Response.Headers);
    }
}
