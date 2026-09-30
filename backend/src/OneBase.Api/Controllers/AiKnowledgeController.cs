using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneBase.AI.Knowledge;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Security;
using OneBase.Domain.AI;
using OneBase.Domain.Audit;

namespace OneBase.Api.Controllers;

/// <summary>База знаний AI: загрузка документов (администратор), просмотр источника и поиск (пользователь с доступом).</summary>
[ApiController]
[Route("api/ai/knowledge")]
public sealed class AiKnowledgeController(IAppDbContext db, KnowledgeService knowledge, IFileStorage files, IAuditLogger audit) : ControllerBase
{
    public sealed record DocumentInput(string Title, string? DepartmentCode, string? RequiredPermission);

    [HttpGet("documents")]
    [HasPermission(Permissions.AiSettingsManage)]
    public async Task<IActionResult> Documents(CancellationToken ct)
    {
        var docs = await db.AiKnowledgeDocuments.AsNoTracking()
            .Where(d => d.ArchivedAt == null)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);
        var departments = await db.Departments.AsNoTracking().OrderBy(d => d.Name).Select(d => new { d.Code, d.Name }).ToListAsync(ct);
        return Ok(new
        {
            Extensions = knowledge.Extensions,
            MaxBytes = KnowledgeService.MaxBytes,
            Departments = departments,
            Permissions = new[] { Permissions.FinanceRead, Permissions.HrRead, Permissions.SalesRead },
            Documents = docs.Select(View),
        });
    }

    [HttpPost("documents")]
    [HasPermission(Permissions.AiSettingsManage)]
    [RequestSizeLimit(KnowledgeService.MaxBytes + 1024 * 1024)]
    public async Task<IActionResult> Upload(
        IFormFile file,
        [FromForm] string? title,
        [FromForm] string? departmentCode,
        [FromForm] string? requiredPermission,
        CancellationToken ct)
    {
        if (await Invalid(departmentCode, requiredPermission, ct) is { } error)
        {
            return BadRequest(new { error });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var document = await knowledge.UploadAsync(stream, file.FileName, file.ContentType, title, departmentCode, requiredPermission, User.GetUserId(), ct);
            await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "ai.knowledge.uploaded", "ai_knowledge_document", document.Id.ToString(),
                new { document.FileName, document.SizeBytes, document.Chunks, document.DepartmentCode, document.RequiredPermission }, ct);
            return Ok(View(document));
        }
        catch (KnowledgeException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("documents/{id:guid}")]
    [HasPermission(Permissions.AiSettingsManage)]
    public async Task<IActionResult> Update(Guid id, DocumentInput input, CancellationToken ct)
    {
        var document = await db.AiKnowledgeDocuments.FirstOrDefaultAsync(d => d.Id == id && d.ArchivedAt == null, ct);
        if (document is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Length > 300)
        {
            return BadRequest(new { error = "Название — от 1 до 300 символов." });
        }

        if (await Invalid(input.DepartmentCode, input.RequiredPermission, ct) is { } error)
        {
            return BadRequest(new { error });
        }

        document.Title = input.Title.Trim();
        document.DepartmentCode = string.IsNullOrWhiteSpace(input.DepartmentCode) ? null : input.DepartmentCode;
        document.RequiredPermission = string.IsNullOrWhiteSpace(input.RequiredPermission) ? null : input.RequiredPermission;
        document.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "ai.knowledge.updated", "ai_knowledge_document", id.ToString(),
            new { document.DepartmentCode, document.RequiredPermission }, ct);
        return Ok(View(document));
    }

    /// <summary>Убрать документ из базы знаний: в поиске больше не участвует; файл и текст сохраняются.</summary>
    [HttpDelete("documents/{id:guid}")]
    [HasPermission(Permissions.AiSettingsManage)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        var document = await db.AiKnowledgeDocuments.FirstOrDefaultAsync(d => d.Id == id && d.ArchivedAt == null, ct);
        if (document is null)
        {
            return NotFound();
        }

        document.ArchivedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "ai.knowledge.archived", "ai_knowledge_document", id.ToString(), null, ct);
        return NoContent();
    }

    /// <summary>Построить эмбеддинги для документов без них (после выбора модели эмбеддингов).</summary>
    [HttpPost("reindex")]
    [HasPermission(Permissions.AiSettingsManage)]
    public async Task<IActionResult> Reindex(CancellationToken ct)
    {
        try
        {
            var count = await knowledge.EmbedPendingAsync(User.GetUserId(), ct);
            await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "ai.knowledge.reindexed", "ai_knowledge", "all", new { count }, ct);
            return Ok(new { count });
        }
        catch (KnowledgeException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Текст документа — страница источника в ответе консультанта. Только если документ доступен пользователю.</summary>
    [HttpGet("documents/{id:guid}/content")]
    [HasPermission(Permissions.FilesRead)]
    public async Task<IActionResult> Content(Guid id, CancellationToken ct)
    {
        var content = await knowledge.GetForUserAsync(User.GetUserId(), id, ct);
        if (content is null)
        {
            return NotFound();
        }

        var d = content.Document;
        return Ok(new { d.Id, d.Title, d.FileName, d.DepartmentCode, d.CreatedAt, d.SizeBytes, content.Chunks });
    }

    [HttpGet("documents/{id:guid}/file")]
    [HasPermission(Permissions.FilesRead)]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var content = await knowledge.GetForUserAsync(User.GetUserId(), id, ct);
        if (content is null)
        {
            return NotFound();
        }

        var buffer = new MemoryStream();
        await files.DownloadAsync(content.Document.ObjectKey, buffer, ct);
        buffer.Position = 0;
        return File(buffer, content.Document.ContentType, content.Document.FileName);
    }

    [HttpGet("search")]
    [HasPermission(Permissions.FilesRead)]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct) =>
        Ok(await knowledge.SearchAsync(User.GetUserId(), q ?? string.Empty, 8, ct));

    private async Task<string?> Invalid(string? department, string? permission, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(department) && !await db.Departments.AnyAsync(d => d.Code == department, ct))
        {
            return "Неизвестный отдел.";
        }

        return !string.IsNullOrWhiteSpace(permission) && !Permissions.All.Contains(permission) ? "Неизвестное право OneBase." : null;
    }

    private static object View(AiKnowledgeDocument d) => new
    {
        d.Id,
        d.Title,
        d.FileName,
        d.SizeBytes,
        d.DepartmentCode,
        d.RequiredPermission,
        Status = d.Status.ToString(),
        d.Error,
        d.Chunks,
        d.Characters,
        d.EmbeddingModel,
        d.CreatedAt,
    };
}
