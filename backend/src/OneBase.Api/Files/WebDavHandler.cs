using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Caching.Memory;
using OneBase.Application.Files;
using OneBase.Domain.Files;
using OneBase.Infrastructure.Files;

namespace OneBase.Api.Files;

/// <summary>
/// Подключение папки отдела к Windows: WebDAV (RFC 4918, класс 1 и 2) на /dav. Снаружи — https://{FILE_STORAGE_DOMAIN}/{код отдела}:
/// Cloudflare → nginx :82 → API /dav (nginx передаёт X-Dav-Root: «/», чтобы адреса в ответах были в пространстве клиента).
/// Вход — логин и пароль подключения отдела (Basic, только по HTTPS снаружи). Все операции идут через DepartmentFilesService —
/// те же права, версии, корзина и журнал, что и на сайте; содержимое — в том же MinIO, без второй копии.
/// </summary>
public static class WebDavHandler
{
    private const string Allow = "OPTIONS, PROPFIND, PROPPATCH, GET, HEAD, PUT, DELETE, MKCOL, MOVE, COPY, LOCK, UNLOCK";
    private const int MaxXmlBody = 64 * 1024;
    private const int LockSeconds = 3600;

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

        var actor = await AuthenticateAsync(http, connections, ct);
        if (actor is null)
        {
            return;
        }

        var publicBase = http.Request.Headers["X-Dav-Root"].FirstOrDefault() is { } root ? root.TrimEnd('/') : http.Request.PathBase.Value ?? "/dav";
        var segments = DavPath.Segments(http.Request.Path.Value);
        if (segments is null)
        {
            http.Response.StatusCode = StatusCodes.Status400BadRequest; // «.» или «..» в пути
            return;
        }

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
                await http.Response.WriteAsync(e.Message, ct);
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

    private static async Task<FileActor?> AuthenticateAsync(HttpContext http, FileConnectionsService connections, CancellationToken ct)
    {
        var header = http.Request.Headers.Authorization.FirstOrDefault();
        if (header is not null && header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
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
            if (colon > 0)
            {
                var result = await connections.AuthenticateAsync(decoded[..colon], decoded[(colon + 1)..],
                    http.Connection.RemoteIpAddress?.ToString() ?? "unknown", ct);
                if (result is { Status: DavAuthStatus.Ok, Actor: { } actor })
                {
                    await connections.TouchAsync(actor.ConnectionId!.Value, ct);
                    return actor;
                }

                if (result.Status == DavAuthStatus.Locked)
                {
                    http.Response.Headers.RetryAfter = "300";
                    http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    return null;
                }
            }
        }

        http.Response.Headers.WWWAuthenticate = "Basic realm=\"OneBase Files\", charset=\"UTF-8\"";
        http.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return null;
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
                    resources.Add(Document(segments, file));
                }
                else
                {
                    var folder = node.Folder!;
                    resources.Add(Collection(segments, folder.ParentId is null ? node.Department.Name : folder.Name, folder));
                    if (depthOne)
                    {
                        var (folders, children) = await files.ChildrenAsync(folder.Id, ct);
                        resources.AddRange(folders.Select(f => Collection([.. segments, f.Name], f.Name, f)));
                        resources.AddRange(children.Select(f => Document([.. segments, f.Name], f)));
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

            if (!DepartmentFilesService.CanWrite(actor, node.Department.Id))
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
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
            if (!cache.TryGetValue($"files-dav-download:{actor.ConnectionId}:{file.Id}:{version.Number}", out _))
            {
                cache.Set($"files-dav-download:{actor.ConnectionId}:{file.Id}:{version.Number}", true, TimeSpan.FromMinutes(10));
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

            await files.UploadAsync(actor, parent.Id, segments[^1], Request.Body, Request.ContentLength, ct);
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
        /// Блокировка для Office и Проводника: токен выдаётся, сами записи блокировкой не ограничиваются. LOCK несуществующего файла создаёт
        /// пустой (RFC 4918, Office так начинает сохранение).
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

            if (node is null || !DepartmentFilesService.CanWrite(actor, node.Department.Id))
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            // Продление: тело пустое, токен — в заголовке If.
            var token = string.IsNullOrWhiteSpace(body) && Request.Headers["If"].FirstOrDefault() is { } condition && condition.Contains("opaquelocktoken:")
                ? condition[condition.IndexOf("opaquelocktoken:", StringComparison.Ordinal)..].Split('>')[0]
                : $"opaquelocktoken:{Guid.NewGuid()}";
            var href = DavPath.Href(publicBase, segments, node.IsCollection);
            Response.Headers["Lock-Token"] = $"<{token}>";
            await WriteXmlAsync(created ? StatusCodes.Status201Created : StatusCodes.Status200OK,
                DavXml.LockResponse(token, href, body, LockSeconds, Request.Headers["Depth"].FirstOrDefault() is not "0" && node.IsCollection));
        }

        public Task UnlockAsync()
        {
            Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
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

        private DavResource Collection(IReadOnlyList<string> path, string name, Folder folder) =>
            new(DavPath.Href(publicBase, path, true), name, true, 0, "", folder.CreatedAt, folder.UpdatedAt ?? folder.CreatedAt, "");

        private DavResource Document(IReadOnlyList<string> path, FileItem file) =>
            new(DavPath.Href(publicBase, path, false), file.Name, false, file.SizeBytes, FileNames.ContentType(file.Name), file.CreatedAt,
                file.UpdatedAt ?? file.CreatedAt, ETag(file));

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
