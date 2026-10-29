using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Caching.Memory;
using OneBase.Application.Files;
using OneBase.Domain.Files;
using OneBase.Infrastructure.Files;

namespace OneBase.Api.Files;

/// <summary>
/// Подключение папки отдела к Windows: WebDAV (RFC 4918, класс 1 и 2) на /dav. Снаружи — https://{FILE_STORAGE_DOMAIN}/{код отдела}:
/// Cloudflare → nginx :82 → API /dav (nginx передаёт X-Dav-Root: «/», чтобы адреса в ответах были в пространстве клиента).
/// Вход — секретная ссылка (/k/{код}/…, без пароля: ссылка отдела или личная ссылка сотрудника) или логин и пароль подключения отдела
/// (Basic, только по HTTPS снаружи). Первый сегмент пути — код отдела или имя общей папки («Общие файлы»).
/// Все операции идут через DepartmentFilesService — те же права, версии, корзина и журнал, что и на сайте; содержимое — в том же MinIO.
/// Блокировки настоящие: пока файл открыт в Word у одного, второй получает 423 и открывает его только для чтения.
/// </summary>
public static partial class WebDavHandler
{
    private const string Allow = "OPTIONS, PROPFIND, PROPPATCH, GET, HEAD, PUT, DELETE, MKCOL, MOVE, COPY, LOCK, UNLOCK";
    private const int MaxXmlBody = 64 * 1024;

    public static async Task HandleAsync(HttpContext http)
    {
        var services = http.RequestServices;
        var files = services.GetRequiredService<DepartmentFilesService>();
        var connections = services.GetRequiredService<FileConnectionsService>();
        var options = services.GetRequiredService<FilesOptions>();
        var cache = services.GetRequiredService<IMemoryCache>();
        var ct = http.RequestAborted;

        http.Response.Headers["DAV"] = "1, 2";
        http.Response.Headers["MS-Author-Via"] = "DAV";
        http.Response.Headers.CacheControl = "no-store, private";

        var method = http.Request.Method.ToUpperInvariant();
        if (method == "OPTIONS")
        {
            // Без пароля: Windows сначала спрашивает возможности сервера и только потом входит.
            http.Response.Headers.Allow = Allow;
            http.Response.StatusCode = StatusCodes.Status200OK;
            return;
        }

        var publicBase = http.Request.Headers["X-Dav-Root"].FirstOrDefault() is { } root ? root.TrimEnd('/') : http.Request.PathBase.Value ?? "/dav";
        var segments = DavPath.Segments(http.Request.Path.Value);
        if (segments is null)
        {
            http.Response.StatusCode = StatusCodes.Status400BadRequest; // «.» или «..» в пути
            return;
        }

        var remote = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (segments.Count > 0 && segments[0].StartsWith('_'))
        {
            // Служебные адреса Office, SharePoint и FrontPage (/_api/…, /_vti_bin/…, /_layouts/…): таких папок нет (коды отделов и имена
            // общих папок с «_» не начинаются). Только 404 — ответ 401 с запросом пароля Microsoft 365 считает «небезопасным входом»
            // и блокирует файл, даже если сам файл открыт по ссылке без пароля.
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        FileActor? actor;
        if (segments.Count == 1 && segments[0].Equals("k", StringComparison.OrdinalIgnoreCase))
        {
            // «Шара» \\files.1base.uz@SSL\k: Проводник и мастер «Добавить сетевое расположение» проверяют путь от неё, и на 404 Windows
            // пишет «Папка недействительна». Здесь пустая папка без содержимого и без входа — ничего не раскрывает, дальше вход по коду.
            if (method == "PROPFIND")
            {
                var now = DateTimeOffset.UtcNow;
                http.Response.StatusCode = StatusCodes.Status207MultiStatus;
                http.Response.ContentType = "application/xml; charset=utf-8";
                await http.Response.WriteAsync(DavXml.MultiStatus([new DavResource(DavPath.Href(publicBase, ["k"], true), "k", true, 0, "", now, now, "")],
                    DavXml.ParsePropfind(null)), ct);
            }
            else
            {
                http.Response.StatusCode = method is "GET" or "HEAD" ? StatusCodes.Status200OK : StatusCodes.Status405MethodNotAllowed;
            }

            return;
        }

        if (segments.Count > 0 && segments[0].Equals("k", StringComparison.OrdinalIgnoreCase))
        {
            // Секретная ссылка: /k/{код}/… — без логина и пароля. Никакого 401: на запрос пароля (Basic) Word и Excel из Microsoft 365
            // отвечают блокировкой файла, а по ссылке открывают и сохраняют сразу. Неверный, истёкший или отозванный код — 404.
            var result = segments.Count > 1
                ? await connections.AuthenticateLinkAsync(segments[1], remote, ct)
                : new DavAuthResult(DavAuthStatus.Failed, null);
            if (result.Status == DavAuthStatus.Locked)
            {
                http.Response.Headers.RetryAfter = "300";
                http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                return;
            }

            if (result.Actor is null)
            {
                http.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            actor = result.Actor;
            publicBase = $"{publicBase}/k/{segments[1]}";
            segments = segments.Skip(2).ToList();
        }
        else if (segments.Count > 0 && segments[0].Equals("my", StringComparison.OrdinalIgnoreCase))
        {
            // «Мои файлы» по логину и паролю OneBase: https://files.1base.uz/my — без входа 401 (Windows спросит логин), после входа —
            // папки отделов сотрудника и общие папки, выданные ему, его роли или отделу. Права — его, из базы при каждом запросе.
            actor = await AuthenticateAsync(http, connections, remote, ct);
            if (actor is null)
            {
                return;
            }

            publicBase = $"{publicBase}/my";
            segments = segments.Skip(1).ToList();
        }
        else if (OfficeProbe(http, segments))
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        else if (segments.Count == 0)
        {
            // Корень сервера никогда не просит пароль. Адрес вида https://files.1base.uz/k/{код}/отдел Windows проверяет через корень
            // (DavWWWRoot), а логин отдела, введённый раньше в этом сеансе, Windows шлёт сюда сама: 401 здесь — окно пароля и «Папка
            // недействительна» для адреса без пароля. С верным логином — папки подключения; без него или с неверным — пустая папка
            // (PROPFIND) или текст (GET, HEAD). Логин отдела Проводник спрашивает у папки отдела (/production → 401).
            var login = await LoginAsync(http, connections, remote, ct);
            if (login.Status == DavAuthStatus.Locked)
            {
                return;
            }

            if (login.Actor is null)
            {
                await AnonymousRootAsync(http, method, publicBase, ct);
                return;
            }

            actor = login.Actor;
        }
        else
        {
            actor = await AuthenticateAsync(http, connections, remote, ct);
            if (actor is null)
            {
                return;
            }
        }

        actor = actor with { Ip = remote, LockTokens = SubmittedTokens(http.Request) };
        var dav = new Context(http, files, actor, publicBase, segments, cache, options, ct);
        try
        {
            await (method switch
            {
                "PROPFIND" => dav.PropFindAsync(),
                "PROPPATCH" => dav.PropPatchAsync(),
                "GET" => dav.GetAsync(body: true),
                "HEAD" => dav.GetAsync(body: false),
                "PUT" => dav.PutAsync(),
                "DELETE" => dav.DeleteAsync(),
                "MKCOL" => dav.MkColAsync(),
                "MOVE" => dav.MoveOrCopyAsync(move: true),
                "COPY" => dav.MoveOrCopyAsync(move: false),
                "LOCK" => dav.LockAsync(),
                "UNLOCK" => dav.UnlockAsync(),
                _ => dav.NotAllowedAsync(),
            });
        }
        catch (FilesException e)
        {
            if (!http.Response.HasStarted)
            {
                http.Response.StatusCode = e.Status;
                if (e.Status == StatusCodes.Status423Locked)
                {
                    // Файл открыт на запись у другого: правка без его токена не проходит (RFC 4918, 9.10.6).
                    http.Response.ContentType = "application/xml; charset=utf-8";
                    await http.Response.WriteAsync(DavXml.Error("lock-token-submitted", DavPath.Href(publicBase, segments, false)), ct);
                }
                else
                {
                    await http.Response.WriteAsync(e.Message, ct);
                }
            }
        }
        catch (BadHttpRequestException e) when (e.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            if (!http.Response.HasStarted)
            {
                http.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            }
        }
    }

    /// <summary>Токены блокировок, которые предъявил клиент: заголовок If («(&lt;opaquelocktoken:…&gt;)») и Lock-Token (UNLOCK).</summary>
    public static IReadOnlySet<string> SubmittedTokens(HttpRequest request)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        foreach (var header in request.Headers["If"].Concat(request.Headers["Lock-Token"]))
        {
            foreach (Match match in TokenPattern().Matches(header ?? ""))
            {
                tokens.Add(match.Value);
            }
        }

        return tokens;
    }

    [GeneratedRegex(@"opaquelocktoken:[^>\s)\]]+")]
    private static partial Regex TokenPattern();

    /// <summary>
    /// Служебные запросы без входа к корню сервера: так Word и Excel выясняют, что за сервер, открывая файл по ссылке. Ответ 401 с
    /// запросом пароля Microsoft 365 считает «небезопасным входом» и блокирует файл — поэтому здесь 404. Проводнику корень без входа
    /// отвечает пустой папкой, логин отдела он вводит у папки отдела (401 там). Адреса «/_…» (FrontPage, SharePoint) — 404 ещё раньше, всем.
    /// </summary>
    private static bool OfficeProbe(HttpContext http, IReadOnlyList<string> segments)
    {
        if (http.Request.Headers.Authorization.Count > 0 || segments.Count > 0)
        {
            return false;
        }

        var agent = http.Request.Headers.UserAgent.ToString();
        return agent.Contains("Microsoft Office", StringComparison.OrdinalIgnoreCase) || agent.Contains("MSOffice", StringComparison.OrdinalIgnoreCase)
               || agent.Contains("Microsoft.Office", StringComparison.OrdinalIgnoreCase) || agent.Contains("FrontPage", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Вход по логину и паролю отдела (Basic), без ответа 401: Locked — пауза после неверных паролей (ответ 429 уже записан).</summary>
    private static async Task<DavAuthResult> LoginAsync(HttpContext http, FileConnectionsService connections, string remote, CancellationToken ct)
    {
        var header = http.Request.Headers.Authorization.FirstOrDefault();
        if (header is null || !header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return new DavAuthResult(DavAuthStatus.Failed, null);
        }

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header[6..].Trim()));
        }
        catch (FormatException)
        {
            decoded = string.Empty;
        }

        var colon = decoded.IndexOf(':');
        if (colon <= 0)
        {
            return new DavAuthResult(DavAuthStatus.Failed, null);
        }

        var result = await connections.AuthenticateAsync(decoded[..colon], decoded[(colon + 1)..], remote, ct);
        if (result is { Status: DavAuthStatus.Ok, Actor: { ConnectionId: { } connectionId } })
        {
            await connections.TouchAsync(connectionId, ct); // у личного входа (логин OneBase) подключения нет
        }
        else if (result.Status == DavAuthStatus.Locked)
        {
            http.Response.Headers.RetryAfter = "300";
            http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        }

        return result;
    }

    /// <summary>Вход по логину отдела для папок отделов и общих папок: нет или неверный — 401 с запросом пароля (Проводник его покажет).</summary>
    private static async Task<FileActor?> AuthenticateAsync(HttpContext http, FileConnectionsService connections, string remote, CancellationToken ct)
    {
        var result = await LoginAsync(http, connections, remote, ct);
        if (result.Actor is not null || result.Status == DavAuthStatus.Locked)
        {
            return result.Actor;
        }

        http.Response.Headers.WWWAuthenticate = "Basic realm=\"OneBase Files\", charset=\"UTF-8\"";
        http.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return null;
    }

    /// <summary>Корень без входа: пустая папка (PROPFIND), текст (GET, HEAD), остальное — 405. Ничего не раскрывает.</summary>
    private static async Task AnonymousRootAsync(HttpContext http, string method, string publicBase, CancellationToken ct)
    {
        if (method == "PROPFIND")
        {
            var now = DateTimeOffset.UtcNow;
            http.Response.StatusCode = StatusCodes.Status207MultiStatus;
            http.Response.ContentType = "application/xml; charset=utf-8";
            await http.Response.WriteAsync(DavXml.MultiStatus([new DavResource(DavPath.Href(publicBase, [], true), "OneBase", true, 0, "", now, now, "")],
                DavXml.ParsePropfind(null)), ct);
        }
        else if (method is "GET" or "HEAD")
        {
            http.Response.StatusCode = StatusCodes.Status200OK;
            http.Response.ContentType = "text/plain; charset=utf-8";
            if (method == "GET")
            {
                await http.Response.WriteAsync("OneBase — файлы отделов. Подключите папку в Проводнике по адресу из OneBase («Подключить к Windows»).", ct);
            }
        }
        else
        {
            http.Response.Headers.Allow = "OPTIONS, PROPFIND, GET, HEAD";
            http.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
        }
    }

    /// <summary>Timeout из LOCK («Second-600», «Infinite, Second-4100000000»): секунды, не больше часа; нет или Infinite — час.</summary>
    public static int LockSeconds(string? header)
    {
        foreach (var part in (header ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.StartsWith("Second-", StringComparison.OrdinalIgnoreCase) && long.TryParse(part[7..], NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
            {
                return (int)Math.Clamp(seconds, 60, DepartmentFilesService.MaxLockSeconds);
            }
        }

        return DepartmentFilesService.MaxLockSeconds;
    }

    private sealed class Context(
        HttpContext http,
        DepartmentFilesService files,
        FileActor actor,
        string publicBase,
        IReadOnlyList<string> segments,
        IMemoryCache cache,
        FilesOptions options,
        CancellationToken ct)
    {
        private HttpRequest Request => http.Request;
        private HttpResponse Response => http.Response;

        public async Task PropFindAsync()
        {
            var request = DavXml.ParsePropfind(await ReadXmlAsync());
            var depthOne = Request.Headers["Depth"].FirstOrDefault() is not "0"; // «infinity» отвечаем как «1»
            var resources = new List<DavResource>();

            if (segments.Count == 0)
            {
                var now = DateTimeOffset.UtcNow;
                resources.Add(new DavResource(DavPath.Href(publicBase, [], true), "OneBase", true, 0, "", now, now, ""));
                if (depthOne)
                {
                    foreach (var department in await files.ReadableDepartmentsAsync(actor, ct))
                    {
                        var root = await files.RootAsync(department, ct);
                        resources.Add(Collection([department.Code], department.Name, root));
                    }

                    // Общие папки — рядом с папками отделов, отдельными папками («Общие файлы»), а не внутри отдела.
                    foreach (var shared in await files.ReadableSharedAsync(actor, ct))
                    {
                        resources.Add(Collection([shared.Name], shared.Name, shared));
                    }
                }
            }
            else
            {
                var node = await files.ResolveAsync(actor, segments, ct);
                if (node is null)
                {
                    Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                if (node.File is { } file)
                {
                    resources.Add(Document(segments, file, await LockOfAsync(file.Id)));
                }
                else
                {
                    var folder = node.Folder!;
                    resources.Add(Collection(segments, folder.ParentId is null ? node.Space.Name : folder.Name, folder));
                    if (depthOne)
                    {
                        var (folders, children) = await files.ChildrenAsync(folder.Id, ct);
                        resources.AddRange(folders.Select(f => Collection([.. segments, f.Name], f.Name, f)));
                        foreach (var child in children)
                        {
                            resources.Add(Document([.. segments, child.Name], child, await LockOfAsync(child.Id)));
                        }
                    }
                }
            }

            await WriteXmlAsync(StatusCodes.Status207MultiStatus, DavXml.MultiStatus(resources, request));
        }

        public async Task PropPatchAsync()
        {
            var node = await files.ResolveAsync(actor, segments, ct);
            if (node is null)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            if (!DepartmentFilesService.CanWrite(actor, node.Space))
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            if (node.File is { } file)
            {
                await files.EnsureUnlockedAsync(actor, file, ct);
            }

            // Windows пишет время создания и изменения файла — принимаем без хранения (время ведёт OneBase).
            await WriteXmlAsync(StatusCodes.Status207MultiStatus, DavXml.PropPatchResponse(DavPath.Href(publicBase, segments, node.IsCollection), await ReadXmlAsync()));
        }

        public async Task GetAsync(bool body)
        {
            var node = segments.Count == 0 ? null : await files.ResolveAsync(actor, segments, ct);
            if (segments.Count > 0 && node is null)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            if (node?.File is not { } file)
            {
                Response.ContentType = "text/plain; charset=utf-8";
                if (body)
                {
                    await Response.WriteAsync("OneBase — файлы отдела. Откройте этот адрес в Проводнике Windows («Подключить сетевой диск»).", ct);
                }

                return;
            }

            var (_, version) = await files.OpenAsync(actor, file.Id, null, ct);
            var etag = ETag(file);
            Response.Headers.ETag = etag;
            Response.Headers.LastModified = (file.UpdatedAt ?? file.CreatedAt).ToString("R", CultureInfo.InvariantCulture);
            if (Request.Headers.IfNoneMatch.FirstOrDefault() == etag)
            {
                Response.StatusCode = StatusCodes.Status304NotModified;
                return;
            }

            Response.ContentType = FileNames.ContentType(file.Name);
            Response.ContentLength = version.SizeBytes;
            if (!body)
            {
                return;
            }

            // Проводник читает файлы и для эскизов — в журнал одна запись на подключение, файл и версию за 10 минут.
            var key = $"files-dav-download:{actor.ConnectionId ?? actor.UserId}:{file.Id}:{version.Number}";
            if (!cache.TryGetValue(key, out _))
            {
                cache.Set(key, true, TimeSpan.FromMinutes(10));
                await files.AuditDownloadAsync(actor, file, version.Number, ct);
            }

            await files.CopyContentAsync(version, Response.Body, ct);
        }

        public async Task PutAsync()
        {
            if (segments.Count < 2)
            {
                Response.StatusCode = StatusCodes.Status405MethodNotAllowed; // в корень и вместо папки отдела файл не кладётся
                return;
            }

            var parent = await ParentFolderAsync(segments);
            if (parent is null)
            {
                Response.StatusCode = StatusCodes.Status409Conflict;
                return;
            }

            if (http.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
            {
                limit.MaxRequestBodySize = options.MaxDavFileBytes;
            }

            var existed = await files.ResolveAsync(actor, segments, ct);
            if (existed is { IsCollection: true })
            {
                Response.StatusCode = StatusCodes.Status409Conflict;
                return;
            }

            // Условная запись (RFC 9110): If-Match — только если на сервере та же версия, что у клиента; If-None-Match: * — только новый файл.
            // Так клиент, который следит за версией, не затрёт чужое сохранение, сделанное после его чтения.
            var ifMatch = Request.Headers.IfMatch.ToString();
            if (ifMatch.Length > 0 && ifMatch != "*" && (existed?.File is not { } current || !ifMatch.Split(',').Select(v => v.Trim()).Contains(ETag(current))))
            {
                Response.StatusCode = StatusCodes.Status412PreconditionFailed;
                return;
            }

            if (Request.Headers.IfNoneMatch.ToString() == "*" && existed is not null)
            {
                Response.StatusCode = StatusCodes.Status412PreconditionFailed;
                return;
            }

            var saved = await files.UploadAsync(actor, parent.Id, segments[^1], Request.Body, Request.ContentLength, ct);
            Response.Headers.ETag = ETag(saved);
            Response.StatusCode = existed is null ? StatusCodes.Status201Created : StatusCodes.Status204NoContent;
        }

        public async Task DeleteAsync()
        {
            var node = segments.Count < 2 ? null : await files.ResolveAsync(actor, segments, ct);
            if (node is null)
            {
                Response.StatusCode = segments.Count < 2 ? StatusCodes.Status403Forbidden : StatusCodes.Status404NotFound;
                return;
            }

            if (node.File is { } file)
            {
                await files.DeleteFileAsync(actor, file.Id, ct);
            }
            else
            {
                await files.DeleteFolderAsync(actor, node.Folder!.Id, ct);
            }

            Response.StatusCode = StatusCodes.Status204NoContent;
        }

        public async Task MkColAsync()
        {
            if (Request.ContentLength > 0)
            {
                Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
                return;
            }

            if (segments.Count < 2 || await files.ResolveAsync(actor, segments, ct) is not null)
            {
                Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                return;
            }

            var parent = await ParentFolderAsync(segments);
            if (parent is null)
            {
                Response.StatusCode = StatusCodes.Status409Conflict;
                return;
            }

            await files.CreateFolderAsync(actor, parent.Id, segments[^1], ct);
            Response.StatusCode = StatusCodes.Status201Created;
        }

        public async Task MoveOrCopyAsync(bool move)
        {
            var source = segments.Count < 2 ? null : await files.ResolveAsync(actor, segments, ct);
            if (source is null)
            {
                Response.StatusCode = segments.Count < 2 ? StatusCodes.Status403Forbidden : StatusCodes.Status404NotFound;
                return;
            }

            var target = DavPath.FromDestination(Request.Headers["Destination"].FirstOrDefault(), publicBase);
            if (target is null || target.Count < 2)
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            var parent = await ParentFolderAsync(target);
            if (parent is null)
            {
                Response.StatusCode = StatusCodes.Status409Conflict;
                return;
            }

            var overwrite = Request.Headers["Overwrite"].FirstOrDefault() is not "F";
            var existing = await files.ResolveAsync(actor, target, ct);
            var sameNode = existing is not null && (existing.File?.Id ?? existing.Folder?.Id) == (source.File?.Id ?? source.Folder?.Id);
            if (existing is not null && !sameNode && !overwrite)
            {
                Response.StatusCode = StatusCodes.Status412PreconditionFailed;
                return;
            }

            var name = target[^1];
            if (source.File is { } file)
            {
                if (move && existing?.File is { } replaced && !sameNode)
                {
                    // Сохранение «через временный файл» (~WRD0001.tmp → отчёт.docx): содержимое — новой версией того же документа,
                    // его история и прежнее содержимое не уходят в корзину вместе с ним.
                    await files.ReplaceContentAsync(actor, file.Id, replaced.Id, ct);
                    Response.StatusCode = StatusCodes.Status204NoContent;
                    return;
                }

                if (existing is { IsCollection: true } && !sameNode)
                {
                    await files.DeleteFolderAsync(actor, existing.Folder!.Id, ct);
                }

                if (move)
                {
                    await files.MoveFileAsync(actor, file.Id, parent.Id, name, replace: true, ct);
                }
                else
                {
                    await files.CopyFileAsync(actor, file.Id, parent.Id, name, ct);
                }
            }
            else
            {
                if (existing is not null && !sameNode)
                {
                    await (existing.File is { } f ? files.DeleteFileAsync(actor, f.Id, ct) : files.DeleteFolderAsync(actor, existing.Folder!.Id, ct));
                }

                if (move)
                {
                    await files.MoveFolderAsync(actor, source.Folder!.Id, parent.Id, name, ct);
                }
                else
                {
                    await CopyFolderAsync(source.Folder!.Id, parent.Id, name, Request.Headers["Depth"].FirstOrDefault() is not "0");
                }
            }

            Response.StatusCode = existing is null || sameNode ? StatusCodes.Status201Created : StatusCodes.Status204NoContent;
        }

        /// <summary>
        /// Блокировка (RFC 4918, 9.10). Файлы — настоящая: хранится в базе, пока действует, менять файл можно только с её токеном
        /// (заголовок If); чужой LOCK получает 423 — второй Word открывает файл только для чтения и пишет, кем он открыт. Продление —
        /// LOCK без тела с токеном в If. LOCK несуществующего файла создаёт пустой (Office так начинает сохранение нового файла).
        /// Папки: токен выдаётся без хранения — Проводник блокирует папки редко, а файлы внутри защищают их собственные блокировки.
        /// </summary>
        public async Task LockAsync()
        {
            var body = await ReadXmlAsync();
            var node = segments.Count < 2 ? null : await files.ResolveAsync(actor, segments, ct);
            var created = false;
            if (node is null)
            {
                var parent = segments.Count < 2 ? null : await ParentFolderAsync(segments);
                if (parent is null)
                {
                    Response.StatusCode = StatusCodes.Status409Conflict;
                    return;
                }

                await files.UploadAsync(actor, parent.Id, segments[^1], Stream.Null, 0, ct);
                node = await files.ResolveAsync(actor, segments, ct);
                created = true;
            }

            if (node is null || !DepartmentFilesService.CanWrite(actor, node.Space))
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            var seconds = LockSeconds(Request.Headers["Timeout"].FirstOrDefault());
            var refresh = string.IsNullOrWhiteSpace(body) ? actor.LockTokens.FirstOrDefault() : null;
            var href = DavPath.Href(publicBase, segments, node.IsCollection);
            if (node.File is not { } file)
            {
                var token = refresh ?? $"opaquelocktoken:{Guid.NewGuid()}";
                Response.Headers["Lock-Token"] = $"<{token}>";
                await WriteXmlAsync(StatusCodes.Status200OK,
                    DavXml.LockResponse(token, href, body, seconds, Request.Headers["Depth"].FirstOrDefault() is not "0"));
                return;
            }

            if (string.IsNullOrWhiteSpace(body) && refresh is null)
            {
                Response.StatusCode = StatusCodes.Status400BadRequest; // продление без токена
                return;
            }

            FileLock fileLock;
            try
            {
                fileLock = await files.LockAsync(actor, file.Id, refresh, DavXml.LockOwner(body), seconds, ct);
            }
            catch (FilesException e) when (e.Status == StatusCodes.Status423Locked)
            {
                await WriteXmlAsync(StatusCodes.Status423Locked, DavXml.Error("no-conflicting-lock", href));
                return;
            }

            var left = (int)Math.Max(1, Math.Ceiling((fileLock.ExpiresAt - DateTimeOffset.UtcNow).TotalSeconds));
            Response.Headers["Lock-Token"] = $"<{fileLock.Token}>";
            await WriteXmlAsync(created ? StatusCodes.Status201Created : StatusCodes.Status200OK,
                DavXml.LockResponse(fileLock.Token, href, body, left, false, fileLock.Owner));
        }

        /// <summary>UNLOCK: снимает свою блокировку по Lock-Token. Неизвестный токен (истекла, сняли) — тоже 204: клиент закрывает файл.</summary>
        public async Task UnlockAsync()
        {
            var node = segments.Count < 2 ? null : await files.ResolveAsync(actor, segments, ct);
            if (node is null)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var token = TokenPattern().Match(Request.Headers["Lock-Token"].ToString());
            if (node.File is { } file && token.Success)
            {
                await files.UnlockAsync(actor, file.Id, token.Value, ct);
            }

            Response.StatusCode = StatusCodes.Status204NoContent;
        }

        public Task NotAllowedAsync()
        {
            Response.Headers.Allow = Allow;
            Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return Task.CompletedTask;
        }

        private async Task CopyFolderAsync(Guid sourceId, Guid parentId, string name, bool deep)
        {
            var copy = await files.CreateFolderAsync(actor, parentId, name, ct);
            if (!deep)
            {
                return;
            }

            var (folders, children) = await files.ChildrenAsync(sourceId, ct);
            foreach (var file in children)
            {
                await files.CopyFileAsync(actor, file.Id, copy.Id, file.Name, ct);
            }

            foreach (var folder in folders)
            {
                await CopyFolderAsync(folder.Id, copy.Id, folder.Name, deep);
            }
        }

        /// <summary>Папка, в которой лежит (или будет лежать) последний сегмент пути; null — её нет.</summary>
        private async Task<Folder?> ParentFolderAsync(IReadOnlyList<string> path)
        {
            var parent = await files.ResolveAsync(actor, path.Take(path.Count - 1).ToList(), ct);
            return parent is { IsCollection: true } ? parent.Folder : null;
        }

        private async Task<DavActiveLock?> LockOfAsync(Guid fileId) =>
            await files.ActiveLockAsync(fileId, ct) is { } l
                ? new DavActiveLock(l.Owner, (int)Math.Max(1, Math.Ceiling((l.ExpiresAt - DateTimeOffset.UtcNow).TotalSeconds)))
                : null;

        private DavResource Collection(IReadOnlyList<string> path, string name, Folder folder) =>
            new(DavPath.Href(publicBase, path, true), name, true, 0, "", folder.CreatedAt, folder.UpdatedAt ?? folder.CreatedAt, "");

        private DavResource Document(IReadOnlyList<string> path, FileItem file, DavActiveLock? fileLock) =>
            new(DavPath.Href(publicBase, path, false), file.Name, false, file.SizeBytes, FileNames.ContentType(file.Name), file.CreatedAt,
                file.UpdatedAt ?? file.CreatedAt, ETag(file), fileLock);

        private static string ETag(FileItem file) => $"\"{file.Id:N}-{file.CurrentVersion}\"";

        private async Task<string?> ReadXmlAsync()
        {
            if (Request.ContentLength is 0)
            {
                return null;
            }

            if (Request.ContentLength > MaxXmlBody)
            {
                throw new FilesException(StatusCodes.Status413PayloadTooLarge, "Слишком большой запрос.");
            }

            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var buffer = new char[MaxXmlBody + 1];
            var read = await reader.ReadBlockAsync(buffer, ct);
            return read > MaxXmlBody ? throw new FilesException(StatusCodes.Status413PayloadTooLarge, "Слишком большой запрос.") : new string(buffer, 0, read);
        }

        private async Task WriteXmlAsync(int status, string xml)
        {
            Response.StatusCode = status;
            Response.ContentType = "application/xml; charset=utf-8";
            await Response.WriteAsync(xml, ct);
        }
    }
}
