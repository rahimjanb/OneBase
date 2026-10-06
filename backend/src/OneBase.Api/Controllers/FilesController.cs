using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;
using OneBase.Api.Auth;
using OneBase.Application.Files;
using OneBase.Application.Security;
using OneBase.Infrastructure.Files;

namespace OneBase.Api.Controllers;

/// <summary>
/// «Файлы отделов» на сайте: отделы, папки, загрузка (несколько файлов сразу; то же имя — новая версия), скачивание, предпросмотр,
/// переименование, перенос, корзина, поиск и подключение папки отдела к Windows. Права проверяются на каждом запросе по данным из БД
/// (FileAccessRules) — те же, что у подключения Windows (WebDAV, /dav).
/// </summary>
[ApiController]
[Route("api/files")]
[HasPermission(Permissions.FilesRead)]
[FilesErrors]
public sealed class FilesController(DepartmentFilesService files, FileConnectionsService connections, FilePreviewService previews, FilesOptions options)
    : ControllerBase
{
    public sealed record NameInput(string? Name);

    public sealed record FolderInput(Guid ParentId, string? Name);

    public sealed record MoveInput(Guid TargetFolderId, string? Name, bool Replace);

    [HttpGet("departments")]
    public async Task<IActionResult> Departments(CancellationToken ct) =>
        Ok(await files.DepartmentsAsync(await ActorAsync(ct), ct));

    /// <summary>Корневая папка отдела по коду (production, finance …).</summary>
    [HttpGet("departments/{code}")]
    public async Task<IActionResult> Department(string code, CancellationToken ct)
    {
        var actor = await ActorAsync(ct);
        var department = await files.DepartmentAsync(actor, code, ct);
        var root = await files.RootAsync(department, ct);
        return Ok(await files.FolderAsync(actor, root.Id, ct));
    }

    [HttpGet("folders/{id:guid}")]
    public async Task<IActionResult> Folder(Guid id, CancellationToken ct) => Ok(await files.FolderAsync(await ActorAsync(ct), id, ct));

    [HttpPost("folders")]
    public async Task<IActionResult> CreateFolder(FolderInput input, CancellationToken ct)
    {
        var folder = await files.CreateFolderAsync(await ActorAsync(ct), input.ParentId, input.Name ?? string.Empty, ct);
        return Ok(new { folder.Id, folder.Name });
    }

    [HttpPut("folders/{id:guid}")]
    public async Task<IActionResult> RenameFolder(Guid id, NameInput input, CancellationToken ct)
    {
        await files.RenameFolderAsync(await ActorAsync(ct), id, input.Name ?? string.Empty, ct);
        return NoContent();
    }

    [HttpPost("folders/{id:guid}/move")]
    public async Task<IActionResult> MoveFolder(Guid id, MoveInput input, CancellationToken ct)
    {
        await files.MoveFolderAsync(await ActorAsync(ct), id, input.TargetFolderId, input.Name, ct);
        return NoContent();
    }

    [HttpDelete("folders/{id:guid}")]
    public async Task<IActionResult> DeleteFolder(Guid id, CancellationToken ct)
    {
        await files.DeleteFolderAsync(await ActorAsync(ct), id, ct);
        return NoContent();
    }

    [HttpPost("folders/{id:guid}/restore")]
    public async Task<IActionResult> RestoreFolder(Guid id, CancellationToken ct)
    {
        await files.RestoreFolderAsync(await ActorAsync(ct), id, ct);
        return NoContent();
    }

    /// <summary>Загрузка одного или нескольких файлов (multipart, поле files). До Files:MaxUploadBytes на запрос — как nginx основного сайта.</summary>
    [HttpPost("folders/{id:guid}/upload")]
    [RequestSizeLimit(210L * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 210L * 1024 * 1024)]
    public async Task<IActionResult> Upload(Guid id, [FromForm(Name = "files")] List<IFormFile> uploads, CancellationToken ct)
    {
        if (uploads.Count == 0)
        {
            return BadRequest(new { error = "Выберите файлы для загрузки." });
        }

        var actor = await ActorAsync(ct);
        var uploaded = new List<object>();
        foreach (var file in uploads)
        {
            await using var stream = file.OpenReadStream();
            var item = await files.UploadAsync(actor, id, file.FileName, stream, file.Length, ct);
            uploaded.Add(new { item.Id, item.Name, Version = item.CurrentVersion, item.SizeBytes });
        }

        return Ok(uploaded);
    }

    /// <summary>
    /// Скачивание (version — номер версии, иначе текущая). inline=true — открыть в браузере: только картинки и PDF, остальное всегда
    /// вложением (HTML или SVG из файла не выполнятся на сайте). Ответ не кэшируется.
    /// </summary>
    [HttpGet("{id:guid}/download")]
    public async Task Download(Guid id, [FromQuery] int? version, [FromQuery] bool inline, CancellationToken ct)
    {
        var actor = await ActorAsync(ct);
        var (file, v) = await files.OpenAsync(actor, id, version, ct);
        var showInline = inline && FileNames.IsInlineSafe(file.Name);
        Response.ContentType = showInline ? FileNames.ContentType(file.Name) : "application/octet-stream";
        Response.ContentLength = v.SizeBytes;
        Response.Headers.CacheControl = "no-store, private";
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentDisposition = new ContentDispositionHeaderValue(showInline ? "inline" : "attachment")
        {
            FileNameStar = file.Name,
            FileName = Ascii(file.Name),
        }.ToString();
        if (!showInline)
        {
            await files.AuditDownloadAsync(actor, file, v.Number, ct);
        }

        await files.CopyContentAsync(v, Response.Body, ct);
    }

    [HttpGet("{id:guid}/preview")]
    public async Task<IActionResult> Preview(Guid id, CancellationToken ct)
    {
        var (file, version) = await files.OpenAsync(await ActorAsync(ct), id, null, ct);
        return Ok(await previews.PreviewAsync(file, version, ct));
    }

    [HttpGet("{id:guid}/versions")]
    public async Task<IActionResult> Versions(Guid id, CancellationToken ct) => Ok(await files.VersionsAsync(await ActorAsync(ct), id, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Rename(Guid id, NameInput input, CancellationToken ct)
    {
        await files.RenameFileAsync(await ActorAsync(ct), id, input.Name ?? string.Empty, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/move")]
    public async Task<IActionResult> Move(Guid id, MoveInput input, CancellationToken ct)
    {
        await files.MoveFileAsync(await ActorAsync(ct), id, input.TargetFolderId, input.Name, input.Replace, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await files.DeleteFileAsync(await ActorAsync(ct), id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct)
    {
        await files.RestoreFileAsync(await ActorAsync(ct), id, ct);
        return NoContent();
    }

    [HttpGet("departments/{code}/trash")]
    public async Task<IActionResult> Trash(string code, CancellationToken ct) => Ok(await files.TrashAsync(await ActorAsync(ct), code, ct));

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] string? department, CancellationToken ct) =>
        Ok(await files.SearchAsync(await ActorAsync(ct), q ?? string.Empty, string.IsNullOrWhiteSpace(department) ? null : department, ct));

    // ---------- Подключение к Windows ----------

    [HttpGet("departments/{code}/connection")]
    public async Task<IActionResult> Connection(string code, CancellationToken ct)
    {
        var actor = await ActorAsync(ct);
        return Ok(await connections.GetAsync(actor, await files.DepartmentAsync(actor, code, ct), ct));
    }

    [HttpPost("departments/{code}/connection")]
    public async Task<IActionResult> CreateConnection(string code, CancellationToken ct)
    {
        var actor = await ActorAsync(ct);
        return Ok(await connections.CreateAsync(actor, await files.DepartmentAsync(actor, code, ct), ct));
    }

    [HttpPost("departments/{code}/connection/regenerate")]
    public async Task<IActionResult> RegenerateConnection(string code, CancellationToken ct)
    {
        var actor = await ActorAsync(ct);
        return Ok(await connections.RegenerateAsync(actor, await files.DepartmentAsync(actor, code, ct), ct));
    }

    [HttpPost("departments/{code}/connection/revoke")]
    public async Task<IActionResult> RevokeConnection(string code, CancellationToken ct)
    {
        var actor = await ActorAsync(ct);
        return Ok(await connections.RevokeAsync(actor, await files.DepartmentAsync(actor, code, ct), ct));
    }

    /// <summary>Пароль подключения — отдельным запросом по нажатию «Показать»/«Скопировать» (POST: не кэшируется и не попадает в адрес).</summary>
    [HttpPost("departments/{code}/connection/password")]
    public async Task<IActionResult> ConnectionPassword(string code, CancellationToken ct)
    {
        var actor = await ActorAsync(ct);
        Response.Headers.CacheControl = "no-store";
        return Ok(new { password = await connections.RevealAsync(actor, await files.DepartmentAsync(actor, code, ct), ct) });
    }

    [HttpPost("departments/{code}/connection/test")]
    public async Task<IActionResult> TestConnection(string code, CancellationToken ct)
    {
        var actor = await ActorAsync(ct);
        return Ok(await connections.TestAsync(actor, await files.DepartmentAsync(actor, code, ct), ct));
    }

    public sealed record AccessInput(Guid DepartmentId, string? Access);

    /// <summary>Доступ других отделов к папке отдела (администратор): access = none | read | write.</summary>
    [HttpGet("departments/{code}/access")]
    public async Task<IActionResult> DepartmentAccess(string code, CancellationToken ct)
    {
        var actor = await ActorAsync(ct);
        return Ok(await files.DepartmentAccessAsync(actor, await files.DepartmentAsync(actor, code, ct), ct));
    }

    [HttpPut("departments/{code}/access")]
    public async Task<IActionResult> SetDepartmentAccess(string code, AccessInput input, CancellationToken ct)
    {
        var actor = await ActorAsync(ct);
        var level = input.Access switch
        {
            "read" => (Domain.Files.AccessLevel?)Domain.Files.AccessLevel.Read,
            "write" => Domain.Files.AccessLevel.Write,
            "none" or null => null,
            _ => throw new FilesException(400, "Доступ — none, read или write."),
        };
        await files.SetDepartmentAccessAsync(actor, await files.DepartmentAsync(actor, code, ct), input.DepartmentId, level, ct);
        return NoContent();
    }

    [HttpGet("settings")]
    public IActionResult Settings() =>
        Ok(new { maxUploadBytes = options.MaxUploadBytes, domainConfigured = options.PublicRoot() is not null });

    private async Task<FileActor> ActorAsync(CancellationToken ct) =>
        await files.UserActorAsync(User.GetUserId(), ct) ?? throw new FilesException(401, "Сессия закончилась — войдите заново.");

    /// <summary>ASCII-имя для старых клиентов (filename=); настоящее имя — в filename* (UTF-8).</summary>
    private static string Ascii(string name)
    {
        var ascii = new string(name.Select(c => c is >= ' ' and < (char)127 and not '"' and not '\\' ? c : '_').ToArray());
        return ascii.Trim('_').Length == 0 ? "file" + Path.GetExtension(name) : ascii;
    }
}

/// <summary>FilesException → { error } с её статусом (400, 403, 404, 409, 413).</summary>
public sealed class FilesErrorsAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is FilesException e)
        {
            context.Result = new ObjectResult(new { error = e.Message }) { StatusCode = e.Status };
            context.ExceptionHandled = true;
        }
    }
}
