using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Api.Files;
using OneBase.Application.Files;
using OneBase.Application.Security;
using OneBase.Domain.Files;
using OneBase.Domain.Identity;
using OneBase.Infrastructure.Files;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// Файлы на настоящем WebDAV (WebDavHandler, PostgreSQL): служебные запросы Office без запроса пароля, блокировки двух редакторов,
/// сохранение через временный файл, условная запись, общие папки (доступ сотрудникам и отделам), личная ссылка, срок и отзыв ссылок,
/// лимит неверных ссылок и ошибочные запросы. Последовательность запросов — как у Word, Excel и Проводника в журнале проверки
/// на Windows 11 с Office 2021 (16.0.14332).
/// </summary>
public class FileSharingTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private const string Word = "Microsoft Office Word 2014 (16.0.14332) Windows NT 10.0";
    private const string Explorer = "Microsoft-WebDAV-MiniRedir/10.0.26200";

    private static string LockBody(string owner) =>
        $"""<?xml version="1.0" encoding="utf-8"?><D:lockinfo xmlns:D="DAV:"><D:lockscope><D:exclusive/></D:lockscope><D:locktype><D:write/></D:locktype><D:owner><D:href>{owner}</D:href></D:owner></D:lockinfo>""";

    [Fact]
    public void Lock_timeout_and_submitted_tokens_are_read_like_office_and_explorer_send_them()
    {
        Assert.Equal(600, WebDavHandler.LockSeconds("Second-600"));
        Assert.Equal(3600, WebDavHandler.LockSeconds("Infinite, Second-4100000000"));
        Assert.Equal(3600, WebDavHandler.LockSeconds(null));
        Assert.Equal(60, WebDavHandler.LockSeconds("Second-5"));

        var http = new DefaultHttpContext();
        http.Request.Headers["If"] = "<https://files.1base.uz/k/x/a.docx> (<opaquelocktoken:aa-1>) (Not <opaquelocktoken:bb-2> [\"etag\"])";
        http.Request.Headers["Lock-Token"] = "<opaquelocktoken:cc-3>";
        Assert.Equal(["opaquelocktoken:aa-1", "opaquelocktoken:bb-2", "opaquelocktoken:cc-3"], WebDavHandler.SubmittedTokens(http.Request).Order());
    }

    [PostgresFact]
    public async Task Office_service_requests_get_404_and_never_a_password_prompt()
    {
        using var scope = fixture.Services.CreateScope();
        // SharePoint API (Word 2021+ спрашивает «сведения о совместном доступе» к файлу, открытому по ссылке), FrontPage и _layouts —
        // и без входа, и с заголовком Authorization (Windows шлёт NTLM на «локальные» адреса) — 404 без WWW-Authenticate.
        foreach (var (method, path) in new[]
                 {
                     ("POST", "/_api/web/GetFileByUrl(@v)/ListItemAllFields/GetSharingInformation"), ("GET", "/_vti_inf.html"),
                     ("POST", "/_vti_bin/shtml.dll/_vti_rpc"), ("GET", "/_layouts/15/download.aspx"), ("PROPFIND", "/_windows/default.aspx"),
                 })
        {
            foreach (var auth in new string?[] { null, "Negotiate TlRMTVNTUAABAAAA" })
            {
                var answer = await DavAsync(scope.ServiceProvider, method, path, agent: "Microsoft Office/16.0 (Windows NT 10.0; Microsoft Word 16.0.14332; Pro)",
                    headers: auth is null ? null : new() { ["Authorization"] = auth });
                Assert.Equal(404, answer.Status);
                Assert.False(answer.Headers.ContainsKey("WWW-Authenticate"), $"{method} {path} просит пароль");
            }
        }

        // Проводник у корня по-прежнему входит по логину и паролю.
        var explorer = await DavAsync(scope.ServiceProvider, "PROPFIND", "/", agent: Explorer);
        Assert.Equal(401, explorer.Status);
    }

    [PostgresFact]
    public async Task Windows_shell_walks_the_link_path_from_the_k_share_without_a_password()
    {
        // Мастер «Добавить сетевое расположение» и адресная строка Проводника проверяют \\files.1base.uz@SSL\k\{код}\отдел от «шары» \k:
        // на 404 Windows пишет «Папка недействительна». /k — пустая папка без входа и без содержимого, дальше — вход по коду.
        var setup = await SetupAsync("fwk");
        var sp = setup.Scope.ServiceProvider;
        foreach (var depth in new[] { "0", "1" })
        {
            var share = await DavAsync(sp, "PROPFIND", "/k", agent: Explorer, headers: new() { ["Depth"] = depth });
            Assert.Equal(207, share.Status);
            Assert.False(share.Headers.ContainsKey("WWW-Authenticate"));
            Assert.Single(System.Xml.Linq.XDocument.Parse(share.Body).Descendants(DavXml.D + "response")); // только сама «шара», ничего внутри
            Assert.DoesNotContain("fwk", share.Body);
        }

        Assert.Equal(207, (await DavAsync(sp, "PROPFIND", $"/k/{setup.Link}", agent: Explorer, headers: new() { ["Depth"] = "0" })).Status);
        Assert.Contains("Документы", (await DavAsync(sp, "PROPFIND", $"/k/{setup.Link}/fwk", agent: Explorer, headers: new() { ["Depth"] = "1" })).Body);
        Assert.Equal(405, (await DavAsync(sp, "PUT", "/k", "x", Explorer)).Status);
        Assert.Equal(404, (await DavAsync(sp, "PROPFIND", $"/k/{ConnectionCredentials.NewLinkToken()}", agent: Explorer)).Status);
    }

    [PostgresFact]
    public async Task Second_editor_is_locked_out_and_cannot_overwrite_the_first()
    {
        var setup = await SetupAsync("fla");
        var sp = setup.Scope.ServiceProvider;
        var path = $"/k/{setup.Link}/fla/Документы/Отчёт.docx";
        Assert.Equal(201, (await DavAsync(sp, "PUT", path, "версия 1", Word)).Status);

        // Word на компьютере A открыл файл на запись.
        var first = await DavAsync(sp, "LOCK", path, LockBody(@"PC-A\Иван"), Word, new() { ["Timeout"] = "Second-600" });
        Assert.Equal(200, first.Status);
        var token = first.Headers["Lock-Token"].ToString().Trim('<', '>');
        Assert.StartsWith("opaquelocktoken:", token);
        Assert.Contains("Second-600", first.Body);

        // Компьютер B: свой LOCK — 423 (Word откроет только для чтения), запись, удаление и перенос без токена — 423.
        var second = await DavAsync(sp, "LOCK", path, LockBody(@"PC-B\Мария"), Word);
        Assert.Equal(423, second.Status);
        Assert.Contains("no-conflicting-lock", second.Body);
        Assert.Equal(423, (await DavAsync(sp, "PUT", path, "перезапись B", Word)).Status);
        Assert.Equal(423, (await DavAsync(sp, "DELETE", path, agent: Explorer)).Status);
        Assert.Equal(423, (await DavAsync(sp, "MOVE", path, agent: Explorer,
            headers: new() { ["Destination"] = $"https://files.1base.uz/k/{setup.Link}/fla/Документы/Другое.docx" })).Status);

        // Кем открыт — видно в PROPFIND (без чужого токена) и на сайте; сайт тоже не перезапишет файл.
        var props = await DavAsync(sp, "PROPFIND", path, headers: new() { ["Depth"] = "0" });
        Assert.Contains(@"PC-A\Иван", props.Body);
        Assert.Contains("activelock", props.Body);
        Assert.DoesNotContain(token, props.Body);
        var files = sp.GetRequiredService<DepartmentFilesService>();
        var user = (await files.UserActorAsync(setup.WriterId, default))!;
        var docs = await FolderByNameAsync(files, user, setup.RootId, "Документы");
        Assert.Equal(@"PC-A\Иван", Assert.Single(docs.Files).Lock?.Owner);
        var site = await Assert.ThrowsAsync<FilesException>(() => files.UploadAsync(user, docs.Id, "Отчёт.docx", new MemoryStream("с сайта"u8.ToArray()), 12, default));
        Assert.Equal(423, site.Status);
        Assert.Contains(@"PC-A\Иван", site.Message);

        // Сам Word A: продлевает блокировку, сохраняет со своим токеном (новая версия) и закрывает файл.
        var refresh = await DavAsync(sp, "LOCK", path, agent: Word, headers: new() { ["If"] = $"(<{token}>)", ["Timeout"] = "Second-3600" });
        Assert.Equal(200, refresh.Status);
        Assert.Equal($"<{token}>", refresh.Headers["Lock-Token"].ToString());
        Assert.Equal(204, (await DavAsync(sp, "PUT", path, "версия 2 от A", Word, new() { ["If"] = $"(<{token}>)" })).Status);
        Assert.Equal((200, "версия 2 от A"), Pick(await DavAsync(sp, "GET", path, agent: Word)));
        Assert.Equal(204, (await DavAsync(sp, "UNLOCK", path, agent: Word, headers: new() { ["Lock-Token"] = $"<{token}>" })).Status);

        // Файл закрыт — B сохраняет; версии все на месте.
        Assert.Equal(204, (await DavAsync(sp, "PUT", path, "версия 3 от B", Word)).Status);
        var file = await setup.Db.Files.AsNoTracking().SingleAsync(f => f.Name == "Отчёт.docx");
        Assert.Equal(3, file.CurrentVersion);
        Assert.Equal([1, 2, 3], await setup.Db.FileVersions.AsNoTracking().Where(v => v.FileItemId == file.Id).OrderBy(v => v.Number).Select(v => v.Number).ToListAsync());

        // Упавший Word: истёкшая блокировка не мешает; администратор снимает зависшую, сотрудник без управления — нет.
        var stale = await DavAsync(sp, "LOCK", path, LockBody(@"PC-A\Иван"), Word);
        await setup.Db.FileLocks.ExecuteUpdateAsync(s => s.SetProperty(l => l.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
        Assert.Equal(204, (await DavAsync(sp, "PUT", path, "версия 4", Word)).Status);
        Assert.Equal(200, (await DavAsync(sp, "LOCK", path, LockBody(@"PC-A\Иван"), Word)).Status);
        Assert.Equal(403, (await Assert.ThrowsAsync<FilesException>(() => files.ForceUnlockAsync(user, file.Id, default))).Status);
        await files.ForceUnlockAsync(Admin(), file.Id, default);
        Assert.Equal(204, (await DavAsync(sp, "PUT", path, "версия 5", Word)).Status);
        Assert.Contains(await setup.Db.AuditLogs.AsNoTracking().Select(a => a.Action).ToListAsync(), a => a == "files.unlocked.forced");
        Assert.Equal(200, stale.Status);
    }

    [PostgresFact]
    public async Task Saving_through_a_temporary_file_keeps_the_document_and_its_history()
    {
        var setup = await SetupAsync("flb");
        var sp = setup.Scope.ServiceProvider;
        var folder = $"/k/{setup.Link}/flb/Документы";
        Assert.Equal(201, (await DavAsync(sp, "PUT", $"{folder}/Смета.xlsx", "старое", Explorer)).Status);
        var original = await setup.Db.Files.AsNoTracking().SingleAsync(f => f.Name == "Смета.xlsx");

        // Проводник/приложение пишет временный файл и переносит его поверх документа.
        Assert.Equal(201, (await DavAsync(sp, "PUT", $"{folder}/~WRD0001.tmp", "новое", Explorer)).Status);
        var moved = await DavAsync(sp, "MOVE", $"{folder}/~WRD0001.tmp", agent: Explorer,
            headers: new() { ["Destination"] = $"http://files.1base.uz/k/{setup.Link}/flb/%D0%94%D0%BE%D0%BA%D1%83%D0%BC%D0%B5%D0%BD%D1%82%D1%8B/%D0%A1%D0%BC%D0%B5%D1%82%D0%B0.xlsx", ["Overwrite"] = "T" });
        Assert.Equal(204, moved.Status);
        var after = await setup.Db.Files.AsNoTracking().SingleAsync(f => f.Name == "Смета.xlsx" && f.DeletedAt == null);
        Assert.Equal((original.Id, 2), (after.Id, after.CurrentVersion)); // тот же документ, новая версия
        Assert.Equal((200, "новое"), Pick(await DavAsync(sp, "GET", $"{folder}/Смета.xlsx")));
        Assert.Equal(404, (await DavAsync(sp, "HEAD", $"{folder}/~WRD0001.tmp")).Status);

        // Условная запись: устаревшая версия — 412, текущая — 204; «только новый файл» поверх существующего — 412.
        var etag = (await DavAsync(sp, "HEAD", $"{folder}/Смета.xlsx")).Headers.ETag.ToString();
        Assert.Equal(412, (await DavAsync(sp, "PUT", $"{folder}/Смета.xlsx", "x", Word, new() { ["If-Match"] = $"\"{original.Id:N}-1\"" })).Status);
        Assert.Equal(204, (await DavAsync(sp, "PUT", $"{folder}/Смета.xlsx", "третья", Word, new() { ["If-Match"] = etag })).Status);
        Assert.Equal(412, (await DavAsync(sp, "PUT", $"{folder}/Смета.xlsx", "x", Word, new() { ["If-None-Match"] = "*" })).Status);

        // Корзина и восстановление через сайт.
        var files = sp.GetRequiredService<DepartmentFilesService>();
        var user = (await files.UserActorAsync(setup.WriterId, default))!;
        Assert.Equal(204, (await DavAsync(sp, "DELETE", $"{folder}/Смета.xlsx", agent: Explorer)).Status);
        var trash = await files.TrashAsync(user, "flb", default);
        Assert.Contains(trash, r => r.Name == "Смета.xlsx");
        await files.RestoreFileAsync(user, original.Id, default);
        Assert.Equal((200, "третья"), Pick(await DavAsync(sp, "GET", $"{folder}/Смета.xlsx")));
    }

    [PostgresFact]
    public async Task Shared_folder_is_visible_only_to_granted_people_and_departments()
    {
        var a = await SetupAsync("fsa");
        var b = await SetupAsync("fsb", reuse: a);
        var sp = a.Scope.ServiceProvider;
        var files = sp.GetRequiredService<DepartmentFilesService>();
        var admin = Admin();

        // Имя: не код отдела, не служебное, без повторов.
        Assert.Equal(400, (await Assert.ThrowsAsync<FilesException>(() => files.CreateSharedAsync(admin, "fsa", default))).Status);
        Assert.Equal(400, (await Assert.ThrowsAsync<FilesException>(() => files.CreateSharedAsync(admin, "_api", default))).Status);
        var shared = await files.CreateSharedAsync(admin, "Общие файлы", default);
        Assert.Equal(409, (await Assert.ThrowsAsync<FilesException>(() => files.CreateSharedAsync(admin, "общие ФАЙЛЫ", default))).Status);
        var writer = (await files.UserActorAsync(a.WriterId, default))!;
        Assert.Equal(403, (await Assert.ThrowsAsync<FilesException>(() => files.CreateSharedAsync(writer, "Своя", default))).Status);

        // Без выдачи не видно никому, кроме администратора, — и директору с просмотром всех отделов тоже.
        Assert.Empty(await files.SharedFoldersAsync(writer, default));
        Assert.Equal(404, (await Assert.ThrowsAsync<FilesException>(() => files.FolderAsync(writer, shared.Id, default))).Status);
        Assert.Empty(await files.SharedFoldersAsync((await files.UserActorAsync(a.DirectorId, default))!, default));
        Assert.Single(await files.SharedFoldersAsync(admin, default));

        // Просмотр: видно, писать нельзя; редактирование: загрузка, версии, корзина.
        await files.SetSharedAccessAsync(admin, shared.Id, "user", a.WriterId.ToString(), AccessLevel.Read, default);
        writer = (await files.UserActorAsync(a.WriterId, default))!;
        Assert.Equal("read", Assert.Single(await files.SharedFoldersAsync(writer, default)).Access);
        Assert.Equal(403, (await Assert.ThrowsAsync<FilesException>(() => Upload(files, writer, shared.Id, "план.txt", "1"))).Status);
        await files.SetSharedAccessAsync(admin, shared.Id, "user", a.WriterId.ToString(), AccessLevel.Write, default);
        writer = (await files.UserActorAsync(a.WriterId, default))!;
        var plan = await Upload(files, writer, shared.Id, "план.txt", "1");
        Assert.Equal(2, (await Upload(files, writer, shared.Id, "план.txt", "2")).CurrentVersion);
        Assert.Equal((null, shared.Id), (plan.DepartmentId, plan.SharedRootId));
        await files.DeleteFileAsync(writer, plan.Id, default);
        Assert.Contains(await files.SharedTrashAsync(writer, shared.Id, default), r => r.Name == "план.txt");
        await files.RestoreFileAsync(writer, plan.Id, default);
        var view = await files.FolderAsync(writer, shared.Id, default);
        Assert.True(view.Department.Shared);
        Assert.Equal(("Общие файлы", "write"), (view.Department.Name, view.Department.Access));

        // Отдел B получил запись: его подключение Windows видит общую папку рядом со своей и пишет в неё; отдел A — нет.
        await files.SetSharedAccessAsync(admin, shared.Id, "department", b.DepartmentId.ToString(), AccessLevel.Write, default);
        var root = await DavAsync(sp, "PROPFIND", $"/k/{b.Link}/", headers: new() { ["Depth"] = "1" });
        Assert.Contains($"/k/{b.Link}/fsb/", root.Body);
        Assert.Contains("Общие файлы", root.Body);
        Assert.Equal(201, (await DavAsync(sp, "PUT", $"/k/{b.Link}/Общие файлы/от отдела B.txt", "B", Explorer)).Status);
        Assert.Equal(404, (await DavAsync(sp, "PROPFIND", $"/k/{a.Link}/Общие файлы/")).Status);
        Assert.DoesNotContain("Общие файлы", (await DavAsync(sp, "PROPFIND", $"/k/{a.Link}/", headers: new() { ["Depth"] = "1" })).Body);

        // Поиск и перенос из отдела в общую папку.
        var bWriter = (await files.UserActorAsync(b.WriterId, default))!;
        var found = await files.SearchAsync(bWriter, "от отдела", null, default);
        Assert.True(Assert.Single(found).Department.Shared);
        var own = await Upload(files, bWriter, b.RootId, "в общую.txt", "x");
        await files.MoveFileAsync(bWriter, own.Id, shared.Id, null, false, default);
        var movedFile = await a.Db.Files.AsNoTracking().SingleAsync(f => f.Id == own.Id);
        Assert.Equal((null, shared.Id), (movedFile.DepartmentId, movedFile.SharedRootId));

        // Управление: без него доступ не настроить; с ним — можно; забранный доступ пропадает сразу.
        Assert.Equal(403, (await Assert.ThrowsAsync<FilesException>(() =>
            files.SetSharedAccessAsync(writer, shared.Id, "user", b.WriterId.ToString(), AccessLevel.Read, default))).Status);
        await files.SetSharedAccessAsync(admin, shared.Id, "user", a.WriterId.ToString(), AccessLevel.Manage, default);
        writer = (await files.UserActorAsync(a.WriterId, default))!;
        await files.SetSharedAccessAsync(writer, shared.Id, "user", b.WriterId.ToString(), AccessLevel.Read, default);
        var grants = (await files.SharedAccessAsync(writer, shared.Id, default)).Grants;
        Assert.Equal(["department:write", "user:manage", "user:read"], grants.Select(g => $"{g.Type}:{g.Access}").Order());
        await files.SetSharedAccessAsync(admin, shared.Id, "user", a.WriterId.ToString(), null, default);
        Assert.Empty(await files.SharedFoldersAsync((await files.UserActorAsync(a.WriterId, default))!, default));

        // Непустую общую папку не удалить — данные не теряются; журнал помнит выдачи.
        Assert.Equal(409, (await Assert.ThrowsAsync<FilesException>(() => files.DeleteSharedAsync(admin, shared.Id, default))).Status);
        Assert.Contains(await a.Db.AuditLogs.AsNoTracking().Select(x => x.Action).ToListAsync(), x => x == "files.shared.access.changed");
    }

    [PostgresFact]
    public async Task Personal_link_carries_the_users_rights_and_stops_on_revoke_expiry_or_deactivation()
    {
        var setup = await SetupAsync("fpa");
        var sp = setup.Scope.ServiceProvider;
        var files = sp.GetRequiredService<DepartmentFilesService>();
        var connections = sp.GetRequiredService<FileConnectionsService>();
        var shared = await files.CreateSharedAsync(Admin(), "Проекты", default);

        var user = (await files.UserActorAsync(setup.WriterId, default))!;
        Assert.Equal("none", (await connections.MyLinkAsync(user, default)).Status);
        var issued = await connections.IssueMyLinkAsync(user, default);
        Assert.Equal("active", issued.Status);
        Assert.Equal("https://files.1base.uz/k/••••••••", issued.MaskedAddress);
        Assert.InRange(issued.ExpiresAt!.Value, DateTimeOffset.UtcNow.AddDays(364), DateTimeOffset.UtcNow.AddDays(366));
        var token = (await connections.RevealMyLinkAsync(user, default)).Address.Split('/')[4];

        // Диск — ровно права сотрудника: свой отдел; общая папка — только после выдачи, на чтение — без записи.
        var root = await DavAsync(sp, "PROPFIND", $"/k/{token}/", headers: new() { ["Depth"] = "1" });
        Assert.Contains($"/k/{token}/fpa/", root.Body);
        Assert.DoesNotContain("Проекты", root.Body);
        await files.SetSharedAccessAsync(Admin(), shared.Id, "user", setup.WriterId.ToString(), AccessLevel.Read, default);
        Assert.Contains("Проекты", (await DavAsync(sp, "PROPFIND", $"/k/{token}/", headers: new() { ["Depth"] = "1" })).Body);
        Assert.Equal(403, (await DavAsync(sp, "PUT", $"/k/{token}/Проекты/план.docx", "x", Word)).Status);
        Assert.Equal(201, (await DavAsync(sp, "PUT", $"/k/{token}/fpa/Документы/моё.docx", "x", Word, ip: "10.1.2.3")).Status);

        // В журнале — сотрудник, источник «личная ссылка» и IP.
        var entry = await setup.Db.AuditLogs.AsNoTracking().Where(x => x.Action == "files.uploaded" && x.ActorId == setup.WriterId.ToString())
            .OrderByDescending(x => x.Id).FirstAsync();
        using (var data = System.Text.Json.JsonDocument.Parse(entry.Data!))
        {
            Assert.Equal(("windows-personal", "10.1.2.3"), (data.RootElement.GetProperty("source").GetString(), data.RootElement.GetProperty("ip").GetString()));
        }

        // Отключили сотрудника — диск не открывается; включили — снова.
        await setup.Db.Users.Where(u => u.Id == setup.WriterId).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));
        Assert.Equal(404, (await DavAsync(sp, "PROPFIND", $"/k/{token}/")).Status);
        await setup.Db.Users.Where(u => u.Id == setup.WriterId).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, true));
        Assert.Equal(207, (await DavAsync(sp, "PROPFIND", $"/k/{token}/")).Status);

        // Новая ссылка отменяет прежнюю сразу; отзыв — и новую.
        await connections.IssueMyLinkAsync(user, default);
        Assert.Equal(404, (await DavAsync(sp, "PROPFIND", $"/k/{token}/")).Status);
        var fresh = (await connections.RevealMyLinkAsync(user, default)).Address.Split('/')[4];
        Assert.Equal(207, (await DavAsync(sp, "PROPFIND", $"/k/{fresh}/")).Status);
        await connections.RevokeMyLinkAsync(user, default);
        Assert.Equal(404, (await DavAsync(sp, "PROPFIND", $"/k/{fresh}/")).Status);

        // Истёкшая ссылка — как неверная: и личная, и ссылка отдела.
        var expired = ConnectionCredentials.NewLinkToken();
        setup.Db.FileUserLinks.Add(new FileUserLink
        {
            UserId = setup.WriterId, LinkTokenHash = ConnectionCredentials.LinkHash(expired)!, ProtectedLinkToken = "-", ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        });
        await setup.Db.SaveChangesAsync();
        Assert.Equal(404, (await DavAsync(sp, "PROPFIND", $"/k/{expired}/")).Status);

        var other = await SetupAsync("fpb", reuse: setup, link: false);
        var admin = Admin();
        var department = await setup.Db.Departments.AsNoTracking().SingleAsync(d => d.Code == "fpb");
        await connections.CreateAsync(admin, department, default);
        await setup.Db.FileConnections.Where(c => c.DepartmentId == department.Id).ExecuteUpdateAsync(s => s.SetProperty(c => c.LinkExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
        var stale = (await connections.RevealLinkAsync(admin, department, default)).Address.Split('/')[4];
        Assert.Equal(404, (await DavAsync(sp, "PROPFIND", $"/k/{stale}/fpb/")).Status);
        Assert.NotEqual(Guid.Empty, other.DepartmentId);
    }

    [PostgresFact]
    public async Task Wrong_links_are_rate_limited_and_bad_requests_are_rejected()
    {
        var setup = await SetupAsync("fbr");
        var sp = setup.Scope.ServiceProvider;
        for (var i = 0; i < FileConnectionsService.MaxLinkMisses; i++)
        {
            Assert.Equal(404, (await DavAsync(sp, "PROPFIND", $"/k/{ConnectionCredentials.NewLinkToken()}/fbr/", ip: "10.9.9.9")).Status);
        }

        var limited = await DavAsync(sp, "PROPFIND", $"/k/{ConnectionCredentials.NewLinkToken()}/fbr/", ip: "10.9.9.9");
        Assert.Equal((429, "300"), (limited.Status, limited.Headers.RetryAfter.ToString()));
        Assert.Equal(207, (await DavAsync(sp, "PROPFIND", $"/k/{setup.Link}/fbr/", ip: "10.8.8.8")).Status); // другой адрес — как обычно

        var folder = $"/k/{setup.Link}/fbr";
        Assert.Equal(400, (await DavAsync(sp, "PROPFIND", $"{folder}/../fla/")).Status);
        Assert.Equal(415, (await DavAsync(sp, "MKCOL", $"{folder}/Новая", "<x/>")).Status);
        Assert.Equal(409, (await DavAsync(sp, "PUT", $"{folder}/Нет такой/файл.txt", "x")).Status);
        Assert.Equal(405, (await DavAsync(sp, "PUT", $"/k/{setup.Link}/файл.txt", "x")).Status);
        Assert.Equal(201, (await DavAsync(sp, "PUT", $"{folder}/Документы/a.txt", "x")).Status);
        Assert.Equal(400, (await DavAsync(sp, "LOCK", $"{folder}/Документы/a.txt")).Status); // продление без токена
        Assert.Equal(412, (await DavAsync(sp, "LOCK", $"{folder}/Документы/a.txt", agent: Word,
            headers: new() { ["If"] = "(<opaquelocktoken:00000000-0000-0000-0000-000000000000>)" })).Status); // продление чужого или истёкшего

        // Большой файл через Windows: больше предела — 413, ничего не записано.
        var options = sp.GetRequiredService<FilesOptions>();
        var limit = options.MaxDavFileBytes;
        options.MaxDavFileBytes = 1024;
        try
        {
            Assert.Equal(413, (await DavAsync(sp, "PUT", $"{folder}/Документы/big.bin", new string('x', 2048))).Status);
            Assert.False(await setup.Db.Files.AnyAsync(f => f.Name == "big.bin"));
        }
        finally
        {
            options.MaxDavFileBytes = limit;
        }
    }

    // ---------- Подготовка ----------

    private sealed record Setup(IServiceScope Scope, OneBaseDbContext Db, Guid DepartmentId, Guid RootId, Guid WriterId, Guid DirectorId, string Link);

    /// <summary>
    /// Отдел с кодом code, сотрудник с чтением и записью файлов в нём, директор с просмотром всех отделов (без отдела) и ссылка отдела
    /// (link = false — без подключения). reuse — тот же scope (второй отдел в одном тесте).
    /// </summary>
    private async Task<Setup> SetupAsync(string code, Setup? reuse = null, bool link = true)
    {
        if (reuse is null)
        {
            await fixture.ResetAsync();
        }

        var scope = reuse?.Scope ?? fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        if (reuse is null)
        {
            // База — своя у этого класса тестов: файлы прошлых тестов не должны попадать в списки («администратор видит одну общую папку»).
            await db.Database.ExecuteSqlRawAsync(
                "TRUNCATE \"FileLocks\", \"FileUserLinks\", \"ResourcePermissions\", \"FileVersions\", \"Files\", \"Folders\", \"FileConnections\" CASCADE");
        }

        sp.GetRequiredService<FilesOptions>().PublicDomain = "files.1base.uz";
        var department = new Department { Code = code, Name = $"Отдел {code}" };
        db.Departments.Add(department);
        var employee = new Role { Name = $"Сотрудник файлов {code}" };
        employee.Permissions.Add(new RolePermission { Code = Permissions.FilesRead });
        employee.Permissions.Add(new RolePermission { Code = Permissions.FilesWrite });
        var writer = new User { Login = $"files-{code}", FullName = $"Сотрудник {code}", DepartmentId = department.Id };
        writer.Roles.Add(new UserRole { Role = employee });
        var directorRole = new Role { Name = $"Директор файлов {code}" };
        directorRole.Permissions.Add(new RolePermission { Code = Permissions.FilesReadAll });
        var director = new User { Login = $"files-dir-{code}", FullName = $"Директор {code}" };
        director.Roles.Add(new UserRole { Role = directorRole });
        db.Users.AddRange(writer, director);
        await db.SaveChangesAsync();

        var files = sp.GetRequiredService<DepartmentFilesService>();
        var root = await files.RootAsync(department, default);
        var token = "";
        if (link)
        {
            var connections = sp.GetRequiredService<FileConnectionsService>();
            await connections.CreateAsync(Admin(), department, default);
            token = (await connections.RevealLinkAsync(Admin(), department, default)).Address.Split('/')[4];
        }

        return new Setup(scope, db, department.Id, root.Id, writer.Id, director.Id, token);
    }

    private FileActor Admin() => new(fixture.AdminId, null, "Админ", true, true, true, new HashSet<Guid>(), [], CanReadAll: true);

    private static Task<FileItem> Upload(DepartmentFilesService files, FileActor actor, Guid folderId, string name, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return files.UploadAsync(actor, folderId, name, new MemoryStream(bytes), bytes.Length, default);
    }

    private static async Task<FolderView> FolderByNameAsync(DepartmentFilesService files, FileActor actor, Guid rootId, string name)
    {
        var root = await files.FolderAsync(actor, rootId, default);
        return await files.FolderAsync(actor, root.Folders.Single(f => f.Name == name).Id, default);
    }

    private static (int, string) Pick((int Status, string Body, IHeaderDictionary Headers) answer) => (answer.Status, answer.Body);

    private static async Task<(int Status, string Body, IHeaderDictionary Headers)> DavAsync(IServiceProvider sp, string method, string path, string? body = null,
        string? agent = null, Dictionary<string, string>? headers = null, string ip = "10.0.0.5")
    {
        using var scope = sp.CreateScope();
        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        http.Connection.RemoteIpAddress = IPAddress.Parse(ip);
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
        else
        {
            http.Request.ContentLength = 0;
        }

        http.Response.Body = new MemoryStream();
        await WebDavHandler.HandleAsync(http);
        http.Response.Body.Position = 0;
        return (http.Response.StatusCode, await new StreamReader(http.Response.Body).ReadToEndAsync(), http.Response.Headers);
    }
}
