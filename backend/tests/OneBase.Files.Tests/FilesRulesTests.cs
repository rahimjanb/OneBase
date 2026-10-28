using System.Xml.Linq;
using OneBase.Application.Files;
using OneBase.Domain.Files;
using OneBase.Infrastructure.Files;

namespace OneBase.Files.Tests;

/// <summary>Правила «Файлов отделов»: имена, пути WebDAV (обход каталогов), доступ отделов, пароли подключения, XML WebDAV.</summary>
public class FilesRulesTests
{
    private static readonly Guid Production = Guid.NewGuid(), Finance = Guid.NewGuid(), Sales = Guid.NewGuid();

    private static FileActor User(Guid department, bool read = true, bool write = true, bool admin = false, params DepartmentGrant[] grants) =>
        new(Guid.NewGuid(), null, "Тест", read, write, admin, new HashSet<Guid> { department }, grants);

    // ---------- Доступ ----------

    [Fact]
    public void Own_department_is_read_write_and_other_departments_are_closed()
    {
        var production = User(Production);

        Assert.Equal(AccessLevel.Write, FileAccessRules.AccessTo(production, Production));
        Assert.Null(FileAccessRules.AccessTo(production, Finance));
        Assert.False(FileAccessRules.CanRead(production, Sales));
    }

    [Fact]
    public void Granted_department_gives_only_the_granted_level()
    {
        // Production: своё — чтение и запись, Sales — чтение (пример из задания), Finance — нет.
        var production = User(Production, grants: new DepartmentGrant(Sales, AccessLevel.Read));

        Assert.Equal(AccessLevel.Read, FileAccessRules.AccessTo(production, Sales));
        Assert.False(FileAccessRules.CanWrite(production, Sales));
        Assert.Null(FileAccessRules.AccessTo(production, Finance));
    }

    [Fact]
    public void Write_grant_is_capped_to_read_without_files_write_and_nothing_without_files_read()
    {
        var readOnly = User(Finance, write: false, grants: new DepartmentGrant(Production, AccessLevel.Write));
        var noFiles = User(Finance, read: false, write: false, grants: new DepartmentGrant(Production, AccessLevel.Write));

        Assert.Equal(AccessLevel.Read, FileAccessRules.AccessTo(readOnly, Finance));
        Assert.Equal(AccessLevel.Read, FileAccessRules.AccessTo(readOnly, Production));
        Assert.Null(FileAccessRules.AccessTo(noFiles, Finance));
        Assert.Null(FileAccessRules.AccessTo(noFiles, Production));
    }

    [Fact]
    public void Connection_sees_only_its_department_with_its_level()
    {
        var connection = FileActor.Connection(Guid.NewGuid(), "production_8f3a1", Production, AccessLevel.Write);
        var readOnly = FileActor.Connection(Guid.NewGuid(), "sales_00000", Sales, AccessLevel.Read);

        Assert.Equal(AccessLevel.Write, FileAccessRules.AccessTo(connection, Production));
        Assert.Null(FileAccessRules.AccessTo(connection, Finance));
        Assert.False(FileAccessRules.CanWrite(readOnly, Sales));
        Assert.False(FileAccessRules.CanSeeConnection(connection, Production)); // подключение не видит и не меняет учётные данные
        Assert.False(FileAccessRules.CanManageConnection(connection));
    }

    [Fact]
    public void Connection_credentials_are_shown_only_to_writers_and_managed_only_by_admins()
    {
        var reader = User(Finance, grants: new DepartmentGrant(Production, AccessLevel.Read));
        var writer = User(Production);
        var admin = User(Finance, admin: true);

        Assert.False(FileAccessRules.CanSeeConnection(reader, Production)); // логин отдела даёт запись — читателю его не показывают
        Assert.True(FileAccessRules.CanSeeConnection(writer, Production));
        Assert.False(FileAccessRules.CanManageConnection(writer));
        Assert.True(FileAccessRules.CanManageConnection(admin));
        Assert.Equal(AccessLevel.Manage, FileAccessRules.AccessTo(admin, Production));
    }

    [Fact]
    public void Read_all_sees_every_department_read_only_but_keeps_own_write()
    {
        // Директор: files.read.all — все отделы на чтение; свой отдел (если есть) — как по ролям.
        var director = User(Production) with { CanReadAll = true };
        var viewer = new FileActor(Guid.NewGuid(), null, "Наблюдатель", false, false, false, new HashSet<Guid>(), [], CanReadAll: true);

        Assert.Equal(AccessLevel.Write, FileAccessRules.AccessTo(director, Production));
        Assert.Equal(AccessLevel.Read, FileAccessRules.AccessTo(director, Finance));
        Assert.False(FileAccessRules.CanWrite(director, Sales));
        Assert.False(FileAccessRules.CanSeeConnection(director, Finance)); // логин чужого отдела даёт запись — не показывается
        Assert.Equal(AccessLevel.Read, FileAccessRules.AccessTo(viewer, Sales));
        Assert.Null(FileAccessRules.AccessTo(User(Finance), Sales)); // без files.read.all чужие отделы закрыты
    }

    [Fact]
    public void Read_all_is_given_only_to_admin_and_director_roles()
    {
        var holders = OneBase.Application.Security.SystemRoles.All
            .Where(r => r.Permissions.Contains(OneBase.Application.Security.Permissions.FilesReadAll))
            .Select(r => r.Name)
            .ToList();

        Assert.Equal([OneBase.Application.Security.SystemRoles.Admin, OneBase.Application.Security.SystemRoles.Director], holders);
        Assert.Contains((OneBase.Application.Security.SystemRoles.Director, OneBase.Application.Security.Permissions.FilesReadAll),
            OneBase.Application.Security.SystemRoles.Upgrades); // у уже созданной роли директора право появится при запуске
    }

    // ---------- Имена ----------

    [Theory]
    [InlineData("отчёт.xlsx")]
    [InlineData("Отчёт #1 (итог) & план.txt")]
    [InlineData("2026")]
    public void Valid_names_pass(string name) => Assert.Null(FileNames.Error(name));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../secret.txt")]
    [InlineData("..\\secret.txt")]
    [InlineData("a/b")]
    [InlineData("a:b")]
    [InlineData("a*b")]
    [InlineData("name.")]
    [InlineData("CON")]
    [InlineData("com1.txt")]
    [InlineData("tab\tname")]
    public void Unsafe_or_windows_invalid_names_are_rejected(string name) => Assert.NotNull(FileNames.Error(name));

    [Fact]
    public void Too_long_name_is_rejected() => Assert.NotNull(FileNames.Error(new string('a', 256)));

    [Fact]
    public void Office_and_windows_service_files_are_hidden_on_the_site()
    {
        Assert.True(FileNames.IsHidden("~$отчёт.xlsx"));
        Assert.True(FileNames.IsHidden("desktop.ini"));
        Assert.True(FileNames.IsHidden("Thumbs.db"));
        Assert.False(FileNames.IsHidden("отчёт.xlsx"));
    }

    [Fact]
    public void Only_images_and_pdf_open_inline()
    {
        Assert.True(FileNames.IsInlineSafe("план.PDF"));
        Assert.True(FileNames.IsInlineSafe("фото.jpg"));
        Assert.False(FileNames.IsInlineSafe("страница.html"));
        Assert.False(FileNames.IsInlineSafe("рисунок.svg"));
        Assert.Equal("application/octet-stream", FileNames.ContentType("скрипт.html"));
    }

    // ---------- Пути WebDAV ----------

    [Fact]
    public void Path_segments_reject_dot_segments()
    {
        Assert.Equal(["production", "Отчёты", "a.xlsx"], DavPath.Segments("/production/Отчёты/a.xlsx"));
        Assert.Empty(DavPath.Segments("/")!);
        Assert.Null(DavPath.Segments("/production/../finance"));
        Assert.Null(DavPath.Segments("/production/./x"));
        Assert.Null(DavPath.Segments("/production/a\\..\\b"));
    }

    [Fact]
    public void Destination_is_parsed_in_the_client_url_space()
    {
        // За nginx адрес — от корня домена; напрямую к API — с /dav.
        Assert.Equal(["production", "Архив", "a b.txt"],
            DavPath.FromDestination("https://files.1base.uz/production/%D0%90%D1%80%D1%85%D0%B8%D0%B2/a%20b.txt", ""));
        Assert.Equal(["production", "x.txt"], DavPath.FromDestination("http://localhost:5080/dav/production/x.txt", "/dav"));
        Assert.Null(DavPath.FromDestination("http://localhost:5080/other/production/x.txt", "/dav")); // вне корня WebDAV
        Assert.Null(DavPath.FromDestination("https://files.1base.uz/production/%2E%2E/finance/x.txt", ""));
        Assert.Null(DavPath.FromDestination("ftp://files.1base.uz/production/x.txt", ""));
        Assert.Null(DavPath.FromDestination(null, ""));
    }

    [Fact]
    public void Href_escapes_each_segment_and_marks_collections()
    {
        Assert.Equal("/production/%D0%9E%D1%82%D1%87%D1%91%D1%82%20%231.txt", DavPath.Href("", ["production", "Отчёт #1.txt"], false));
        Assert.Equal("/dav/production/", DavPath.Href("/dav", ["production"], true));
        Assert.Equal("/", DavPath.Href("", [], true));
    }

    // ---------- Пароли и адрес подключения ----------

    [Fact]
    public void Password_is_random_long_and_verified_only_by_its_hash()
    {
        var password = ConnectionCredentials.NewPassword();
        var hash = ConnectionCredentials.Hash(password);

        Assert.Equal(ConnectionCredentials.PasswordLength, password.Length);
        Assert.NotEqual(password, ConnectionCredentials.NewPassword());
        Assert.DoesNotContain(password, hash);
        Assert.True(ConnectionCredentials.Verify(hash, password));
        Assert.False(ConnectionCredentials.Verify(hash, password + "x"));
        Assert.False(ConnectionCredentials.Verify("plain-text", password));
        Assert.NotEqual(hash, ConnectionCredentials.Hash(password)); // соль у каждого хэша своя
    }

    [Fact]
    public void Username_is_department_code_and_random_suffix()
    {
        var username = ConnectionCredentials.NewUsername("Production");

        Assert.Matches("^production_[0-9a-f]{5}$", username);
        Assert.Matches("^dept_[0-9a-f]{5}$", ConnectionCredentials.NewUsername("Отдел"));
    }

    [Fact]
    public void Address_comes_from_configuration_and_is_absent_without_domain()
    {
        var configured = new FilesOptions { PublicDomain = "files.1base.uz" };
        var local = new FilesOptions { PublicDomain = "localhost:8443" };

        Assert.Equal("https://files.1base.uz/production", configured.FolderUrl("production"));
        Assert.Equal(@"\\files.1base.uz@SSL\production", configured.UncPath("production"));
        Assert.Equal(@"\\localhost@SSL@8443\production", local.UncPath("production"));
        Assert.Null(new FilesOptions().FolderUrl("production"));
        Assert.Null(new FilesOptions().UncPath("production"));
    }

    /// <summary>
    /// Секретная ссылка отдела (адрес без пароля): код из 32 строчных знаков без похожих, хэш не зависит от регистра (Windows может менять
    /// регистр в адресе), чужая форма — не ищется. Адрес и путь Windows — с префиксом /k/{код}, Destination переноса — внутри той же ссылки.
    /// </summary>
    [Fact]
    public void Department_link_token_address_and_paths()
    {
        var token = ConnectionCredentials.NewLinkToken();
        Assert.Matches("^[a-km-np-z2-9]{32}$", token);
        Assert.NotEqual(token, ConnectionCredentials.NewLinkToken());
        Assert.Equal(ConnectionCredentials.LinkHash(token), ConnectionCredentials.LinkHash(" " + token.ToUpperInvariant()));
        Assert.Equal(64, ConnectionCredentials.LinkHash(token)!.Length);
        Assert.Null(ConnectionCredentials.LinkHash(token[..31]));
        Assert.Null(ConnectionCredentials.LinkHash(token[..31] + "0"));
        Assert.Null(ConnectionCredentials.LinkHash(null));

        var options = new FilesOptions { PublicDomain = "files.1base.uz" };
        Assert.Equal($"https://files.1base.uz/k/{token}/supply", options.LinkUrl(token, "supply"));
        Assert.Equal($@"\\files.1base.uz@SSL\k\{token}\supply", options.LinkUncPath(token, "supply"));
        Assert.Null(new FilesOptions().LinkUrl(token, "supply"));

        var root = $"/k/{token}";
        Assert.Equal(["supply", "Документы", "заявка.docx"],
            DavPath.FromDestination($"https://files.1base.uz/k/{token}/supply/%D0%94%D0%BE%D0%BA%D1%83%D0%BC%D0%B5%D0%BD%D1%82%D1%8B/%D0%B7%D0%B0%D1%8F%D0%B2%D0%BA%D0%B0.docx", root));
        Assert.Null(DavPath.FromDestination("https://files.1base.uz/supply/a.docx", root)); // вне своей ссылки — отказ
        Assert.Equal($"/k/{token}/supply/", DavPath.Href(root, ["supply"], true));
    }

    // ---------- XML WebDAV ----------

    [Fact]
    public void Propfind_lists_requested_properties_and_reports_unknown_as_404()
    {
        var request = DavXml.ParsePropfind("""
            <?xml version="1.0"?><D:propfind xmlns:D="DAV:" xmlns:Z="urn:x"><D:prop><D:getcontentlength/><Z:custom/></D:prop></D:propfind>
            """);
        var now = DateTimeOffset.UtcNow;
        var xml = XDocument.Parse(DavXml.MultiStatus([new DavResource("/production/a.txt", "a.txt", false, 42, "text/plain", now, now, "\"e\"")], request));
        var d = DavXml.D;

        Assert.Equal(PropfindMode.Prop, request.Mode);
        var propstats = xml.Descendants(d + "propstat").ToList();
        Assert.Equal("42", propstats[0].Descendants(d + "getcontentlength").Single().Value);
        Assert.Contains("404", propstats[1].Element(d + "status")!.Value);
    }

    [Fact]
    public void Allprop_marks_folders_as_collections_without_file_properties()
    {
        var now = DateTimeOffset.UtcNow;
        var xml = XDocument.Parse(DavXml.MultiStatus(
            [new DavResource("/production/", "Производство", true, 0, "", now, now, "")], DavXml.ParsePropfind(null)));
        var d = DavXml.D;

        Assert.NotNull(xml.Descendants(d + "collection").SingleOrDefault());
        Assert.Empty(xml.Descendants(d + "getcontentlength"));
        Assert.Equal("Производство", xml.Descendants(d + "displayname").Single().Value);
    }

    [Fact]
    public void Xml_with_dtd_is_not_parsed()
    {
        // Внешние сущности (XXE) не разбираются: такой запрос читается как allprop.
        var request = DavXml.ParsePropfind("""<?xml version="1.0"?><!DOCTYPE x [<!ENTITY e SYSTEM "file:///etc/passwd">]><D:propfind xmlns:D="DAV:"><D:prop>&e;</D:prop></D:propfind>""");

        Assert.Equal(PropfindMode.AllProp, request.Mode);
    }

    [Fact]
    public void Lock_response_carries_token_owner_and_timeout()
    {
        var xml = XDocument.Parse(DavXml.LockResponse("opaquelocktoken:1", "/production/a.docx",
            """<D:lockinfo xmlns:D="DAV:"><D:owner>Рахим</D:owner></D:lockinfo>""", 3600, false));
        var d = DavXml.D;

        Assert.Equal("opaquelocktoken:1", xml.Descendants(d + "locktoken").Single().Value);
        Assert.Equal("Рахим", xml.Descendants(d + "owner").Single().Value);
        Assert.Equal("Second-3600", xml.Descendants(d + "timeout").Single().Value);
    }

    // ---------- Общие папки ----------

    [Fact]
    public void Shared_folder_is_open_only_by_grant_and_write_needs_the_files_write_right()
    {
        var root = Guid.NewGuid();
        var stranger = User(Production);
        Assert.Null(FileAccessRules.AccessToShared(stranger, root));
        Assert.Null(FileAccessRules.AccessToShared(stranger with { CanReadAll = true }, root)); // «просмотр всех отделов» общих папок не открывает
        Assert.Equal(AccessLevel.Manage, FileAccessRules.AccessToShared(User(Production, admin: true), root));

        var editor = User(Production) with { SharedGrants = [new SharedGrant(root, AccessLevel.Write)] };
        Assert.Equal(AccessLevel.Write, FileAccessRules.AccessToShared(editor, root));
        var readOnly = User(Production, write: false) with { SharedGrants = [new SharedGrant(root, AccessLevel.Manage)] };
        Assert.Equal(AccessLevel.Read, FileAccessRules.AccessToShared(readOnly, root)); // без files.write — только просмотр
        var noFiles = User(Production, read: false, write: false) with { SharedGrants = [new SharedGrant(root, AccessLevel.Write)] };
        Assert.Null(FileAccessRules.AccessToShared(noFiles, root)); // раздел «Файлы» снят — общих папок тоже нет

        // Подключение отдела: выданное отделу, не выше своего уровня и без управления.
        var connection = FileActor.Connection(Guid.NewGuid(), "production_1", Production, AccessLevel.Write, [new SharedGrant(root, AccessLevel.Manage)]);
        Assert.Equal(AccessLevel.Write, FileAccessRules.AccessToShared(connection, root));
        var readConnection = FileActor.Connection(Guid.NewGuid(), "production_2", Production, AccessLevel.Read, [new SharedGrant(root, AccessLevel.Write)]);
        Assert.Equal(AccessLevel.Read, FileAccessRules.AccessToShared(readConnection, root));
        Assert.Null(FileAccessRules.AccessToItem(connection, null, Guid.NewGuid()));
        Assert.Null(FileAccessRules.AccessToItem(editor, null, null)); // старые папки без отдела и общей папки не видны никому
    }

    [Fact]
    public void Personal_and_shared_link_addresses_escape_names_and_keep_windows_paths()
    {
        var options = new FilesOptions { PublicDomain = "files.1base.uz" };
        Assert.Equal("https://files.1base.uz/k/abc", options.LinkUrl("abc", ""));
        Assert.Equal(@"\\files.1base.uz@SSL\k\abc", options.LinkUncPath("abc", ""));
        Assert.Equal("https://files.1base.uz/k/abc/%D0%9E%D0%B1%D1%89%D0%B8%D0%B5%20%D1%84%D0%B0%D0%B9%D0%BB%D1%8B", options.LinkUrl("abc", "Общие файлы"));
        Assert.Equal(@"\\files.1base.uz@SSL\k\abc\Общие файлы", options.LinkUncPath("abc", "Общие файлы"));
        var issued = new DateTimeOffset(2026, 10, 28, 0, 0, 0, TimeSpan.Zero);
        Assert.Equal(issued.AddDays(365), options.LinkExpiry(issued));
        Assert.Equal(issued.AddDays(3650), new FilesOptions { LinkLifetimeDays = 100_000 }.LinkExpiry(issued));
    }

    [Fact]
    public void Lock_discovery_tells_who_holds_the_file_without_giving_away_the_token()
    {
        var d = DavXml.D;
        var resource = new DavResource("/production/a.docx", "a.docx", false, 10, "application/octet-stream", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "\"1\"",
            new DavActiveLock(@"PC-A\Иван", 540));
        var xml = XDocument.Parse(DavXml.MultiStatus([resource], DavXml.ParsePropfind(null)));
        var active = xml.Descendants(d + "activelock").Single();
        Assert.Equal(@"PC-A\Иван", active.Element(d + "owner")!.Value);
        Assert.Equal("Second-540", active.Element(d + "timeout")!.Value);
        Assert.Null(active.Element(d + "locktoken"));

        var owner = DavXml.LockOwner("""<D:lockinfo xmlns:D="DAV:"><D:owner><D:href>DESKTOP\Admin</D:href></D:owner></D:lockinfo>""");
        Assert.Equal(@"DESKTOP\Admin", owner);
        Assert.Null(DavXml.LockOwner(null));
        Assert.Contains("no-conflicting-lock", DavXml.Error("no-conflicting-lock", "/a"));
        var refreshed = XDocument.Parse(DavXml.LockResponse("opaquelocktoken:1", "/a", null, 3600, false, "PC-A"));
        Assert.Equal(("PC-A", "opaquelocktoken:1"), (refreshed.Descendants(d + "owner").Single().Value, refreshed.Descendants(d + "locktoken").Single().Value));
    }

    // ---------- Предпросмотр CSV ----------

    [Fact]
    public void Csv_parser_handles_quotes_delimiters_and_line_endings()
    {
        var rows = FilePreviewService.ParseCsv("a;\"b;c\";\"d \"\"e\"\"\"\r\n1;2;3\n", ';').ToList();

        Assert.Equal(2, rows.Count);
        Assert.Equal(["a", "b;c", "d \"e\""], rows[0]);
        Assert.Equal(["1", "2", "3"], rows[1]);
    }
}
