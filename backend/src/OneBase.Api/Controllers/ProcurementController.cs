using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Procurement;

namespace OneBase.Api.Controllers;

/// <summary>
/// Снабжение: заявки на закупку (позиции, категории, приоритет, вложения, история, резерв, согласование директором, выдача) и склад
/// (остатки, каталог, движения, приход, списание, инвентаризация, выдача отделу). Доступ решают ProcurementRequestService и
/// ProcurementStockService по правам из токена, отделу, руководству и роли «Директор» из базы — никогда по параметрам запроса.
/// </summary>
[ApiController]
[Authorize]
[FieldErrors]
[Route("api/procurement")]
public sealed class ProcurementController(ProcurementRequestService requests, ProcurementStockService stock, ProcurementDocumentService documents, IFileStorage storage,
    ProcurementStockImportService stockImport)
    : ControllerBase
{
    private Task<ProcurementContext> ContextAsync(CancellationToken ct) =>
        requests.ContextAsync(User.GetUserId(), User.FindAll(OneBaseClaims.Permission).Select(c => c.Value).ToHashSet(), ct);

    [HttpGet("nav")]
    public async Task<ProcurementNav> Nav(CancellationToken ct) => await requests.NavAsync(await ContextAsync(ct), ct);

    [HttpGet("overview")]
    public async Task<ProcurementOverview> Overview(CancellationToken ct) => await requests.OverviewAsync(await ContextAsync(ct), ct);

    /// <summary>Отделы, категории и единицы для формы заявки.</summary>
    [HttpGet("form")]
    public async Task<ProcurementFormData> Form(CancellationToken ct) => await requests.FormAsync(await ContextAsync(ct), ct);

    /// <summary>
    /// Заявки: scope (all, my, department), status (open, closed, draft или статусы через запятую), priority и department — через запятую,
    /// category, from/to (дата создания), q — номер, название, позиция, инициатор; сортировка и страницы.
    /// </summary>
    [HttpGet("requests")]
    public async Task<PageResult<ProcurementRequestRow>> List(
        [FromQuery] string scope = "all",
        [FromQuery] string? status = null,
        [FromQuery] string? priority = null,
        [FromQuery] Guid? category = null,
        [FromQuery] string? department = null,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] string? q = null,
        [FromQuery] string? sort = null,
        [FromQuery] bool desc = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default) =>
        await requests.SearchAsync(await ContextAsync(ct), new ProcurementRequestSearch(scope, status, priority, category, department, from, to, q, sort, desc, page, pageSize), ct);

    [HttpGet("requests/{id:guid}")]
    public async Task<ProcurementRequestDetail> Get(Guid id, CancellationToken ct) => await requests.GetAsync(await ContextAsync(ct), id, ct);

    [HttpPost("requests")]
    public async Task<IActionResult> Create(ProcurementRequestInput input, CancellationToken ct) =>
        Ok(new { id = await requests.CreateAsync(await ContextAsync(ct), input, ct) });

    [HttpPut("requests/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, ProcurementRequestInput input, CancellationToken ct)
    {
        await requests.UpdateAsync(await ContextAsync(ct), id, input, ct);
        return NoContent();
    }

    [HttpPost("requests/{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
    {
        await requests.SubmitAsync(await ContextAsync(ct), id, ct);
        return NoContent();
    }

    [HttpPost("requests/{id:guid}/forward")]
    public async Task<IActionResult> Forward(Guid id, CancellationToken ct)
    {
        await requests.ForwardAsync(await ContextAsync(ct), id, ct);
        return NoContent();
    }

    [HttpPost("requests/{id:guid}/return")]
    public async Task<IActionResult> Return(Guid id, CommentBody body, CancellationToken ct)
    {
        await requests.ReturnAsync(await ContextAsync(ct), id, body.Comment, ct);
        return NoContent();
    }

    [HttpPost("requests/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CommentBody body, CancellationToken ct)
    {
        await requests.CancelAsync(await ContextAsync(ct), id, body.Comment, ct);
        return NoContent();
    }

    /// <summary>Согласующий текущего шага (руководитель отдела, финансист, директор маршрута).</summary>
    public sealed record ApproveBody(string? Comment, IReadOnlyList<ProcurementPriceInput>? Prices = null);

    /// <summary>
    /// Согласовать текущий шаг — его согласующий (руководитель отдела, финансист, директор маршрута; администратор не согласует). Финансист
    /// может уточнить цены позиций (Prices).
    /// </summary>
    [HttpPost("requests/{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, ApproveBody body, CancellationToken ct)
    {
        await requests.ApproveAsync(await ContextAsync(ct), id, body.Comment, ct, body.Prices);
        return NoContent();
    }

    /// <summary>Отклонить — согласующий текущего шага, комментарий обязателен.</summary>
    [HttpPost("requests/{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, CommentBody body, CancellationToken ct)
    {
        await requests.RejectAsync(await ContextAsync(ct), id, body.Comment, ct);
        return NoContent();
    }

    /// <summary>Выдать со склада по заявке (из резерва) — кладовщик.</summary>
    [HttpPost("requests/{id:guid}/issue")]
    public async Task<IActionResult> IssueForRequest(Guid id, ProcurementRequestIssueInput input, CancellationToken ct) =>
        Ok(new { id = await requests.IssueAsync(await ContextAsync(ct), id, input, ct) });

    [HttpPost("requests/{id:guid}/attachments")]
    [RequestSizeLimit(ProcurementRequestService.MaxAttachmentBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = ProcurementRequestService.MaxAttachmentBytes + 1024 * 1024)]
    public async Task<IActionResult> Attach(Guid id, IFormFile? file, CancellationToken ct)
    {
        if (file is null)
        {
            throw new FieldValidationException("Выберите файл.");
        }

        await using var stream = file.OpenReadStream();
        return Ok(new { id = await requests.AttachAsync(await ContextAsync(ct), id, new ProcurementUpload(file.FileName, file.Length, stream), storage, ct) });
    }

    /// <summary>Скачивание вложения — только тем, кто видит заявку; всегда как файл.</summary>
    [HttpGet("attachments/{id:guid}")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var ctx = await ContextAsync(ct);
        var file = await documents.DownloadAsync(ctx, id, ct) ?? await requests.DownloadAsync(ctx, id, ct);
        var buffer = new MemoryStream();
        await storage.DownloadAsync(file.ObjectKey, buffer, ct);
        buffer.Position = 0;
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "private, no-store";
        return File(buffer, file.ContentType, file.FileName);
    }

    [HttpDelete("attachments/{id:guid}")]
    public async Task<IActionResult> DeleteAttachment(Guid id, CancellationToken ct)
    {
        var ctx = await ContextAsync(ct);
        if (!await documents.DeleteAsync(ctx, id, storage, ct))
        {
            await requests.DeleteDocumentAsync(ctx, id, storage, ct);
        }

        return NoContent();
    }

    [HttpGet("categories")]
    public async Task<List<ProcurementCategoryView>> Categories([FromQuery] bool all = false, CancellationToken ct = default) =>
        await requests.CategoriesAsync(await ContextAsync(ct), all, ct);

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory(ProcurementCategoryInput input, CancellationToken ct) =>
        Ok(new { id = await requests.CreateCategoryAsync(await ContextAsync(ct), input, ct) });

    [HttpPut("categories/{id:guid}")]
    public async Task<IActionResult> UpdateCategory(Guid id, ProcurementCategoryInput input, CancellationToken ct)
    {
        await requests.UpdateCategoryAsync(await ContextAsync(ct), id, input, ct);
        return NoContent();
    }

    [HttpGet("settings")]
    public async Task<ProcurementSettingsView> Settings(CancellationToken ct) => await requests.SettingsAsync(await ContextAsync(ct), ct);

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings(ProcurementSettingsView input, CancellationToken ct)
    {
        await requests.UpdateSettingsAsync(await ContextAsync(ct), input, ct);
        return NoContent();
    }

    // ---------- Склад ----------

    [HttpGet("stock")]
    public async Task<PageResult<ProcurementStockRow>> Stock(
        [FromQuery] string? q = null,
        [FromQuery] Guid? category = null,
        [FromQuery] string? filter = null,
        [FromQuery] string? sort = null,
        [FromQuery] bool desc = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        await stock.StockAsync(await ContextAsync(ct), new ProcurementStockSearch(q, category, filter, sort, desc, page, pageSize), ct);

    [HttpGet("stock/summary")]
    public async Task<ProcurementStockSummary> StockSummary(CancellationToken ct) => await stock.SummaryAsync(await ContextAsync(ct), ct);

    /// <summary>Товары каталога для формы заявки — с доступным остатком.</summary>
    [HttpGet("catalog")]
    public async Task<List<ProcurementCatalogOption>> Catalog([FromQuery] string? q, CancellationToken ct) => await stock.CatalogSearchAsync(await ContextAsync(ct), q, ct);

    [HttpGet("items/{id:guid}")]
    public async Task<ProcurementItemCard> Item(Guid id, CancellationToken ct) => await stock.ItemAsync(await ContextAsync(ct), id, ct);

    [HttpPost("items")]
    public async Task<IActionResult> CreateItem(ProcurementCatalogInput input, CancellationToken ct) =>
        Ok(new { id = await stock.CreateItemAsync(await ContextAsync(ct), input, ct) });

    [HttpPut("items/{id:guid}")]
    public async Task<IActionResult> UpdateItem(Guid id, ProcurementCatalogInput input, CancellationToken ct)
    {
        await stock.UpdateItemAsync(await ContextAsync(ct), id, input, ct);
        return NoContent();
    }

    [HttpGet("movements")]
    public async Task<PageResult<ProcurementMovementRow>> Movements(
        [FromQuery] Guid? item = null,
        [FromQuery] string? kind = null,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] string? q = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        await stock.MovementsAsync(await ContextAsync(ct), new ProcurementMovementSearch(item, kind, from, to, q, page, pageSize), ct);

    [HttpGet("stock/departments")]
    public async Task<List<ProcurementDepartmentOption>> StockDepartments(CancellationToken ct) => await stock.DepartmentsAsync(await ContextAsync(ct), ct);

    [HttpGet("stock/to-issue")]
    public async Task<List<ProcurementToIssue>> ToIssue(CancellationToken ct) => await stock.ToIssueAsync(await ContextAsync(ct), ct);

    /// <summary>Складские операции (кладовщик): приход, списание, инвентаризация, выдача отделу без заявки.</summary>
    [HttpPost("stock/receipt")]
    public async Task<IActionResult> Receipt(ProcurementStockOperationInput input, CancellationToken ct) =>
        Ok(new { id = await stock.ReceiptAsync(await ContextAsync(ct), input, ct) });

    [HttpPost("stock/write-off")]
    public async Task<IActionResult> WriteOff(ProcurementStockOperationInput input, CancellationToken ct) =>
        Ok(new { id = await stock.WriteOffAsync(await ContextAsync(ct), input, ct) });

    [HttpPost("stock/inventory")]
    public async Task<IActionResult> Inventory(ProcurementStockOperationInput input, CancellationToken ct) =>
        Ok(new { id = await stock.InventoryAsync(await ContextAsync(ct), input, ct) });

    [HttpPost("stock/issue")]
    public async Task<IActionResult> Issue(ProcurementStockOperationInput input, CancellationToken ct) =>
        Ok(new { id = await stock.IssueAsync(await ContextAsync(ct), input, ct) });

    /// <summary>
    /// Загрузка движений склада из Excel (кладовщик): первая — начальные остатки по файлу, следующие — только новые строки. dryRun=true —
    /// проверка файла: что и куда попадёт, без сохранения.
    /// </summary>
    [HttpPost("stock/import")]
    [RequestSizeLimit(30 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 30 * 1024 * 1024)]
    public async Task<ProcurementStockImportResult> ImportStock(IFormFile? file, [FromQuery] bool dryRun, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            throw new FieldValidationException("Выберите файл Excel или CSV.");
        }

        await using var stream = file.OpenReadStream();
        return await stockImport.ImportAsync(await ContextAsync(ct), stream, file.FileName, ct, dryRun);
    }

    public sealed record CommentBody(string? Comment);
}
