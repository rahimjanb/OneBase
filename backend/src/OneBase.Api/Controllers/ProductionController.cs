using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneBase.Api.Auth;
using OneBase.Application.Field;
using OneBase.Application.Procurement;
using OneBase.Application.Production;
using OneBase.Application.Reports;

namespace OneBase.Api.Controllers;

/// <summary>
/// «Производство»: рецептуры (версии, потребность), задания на выпуск (выпуск, выдача и возврат сырья по партиям, закрытие со сверкой нормы),
/// обзор сырья и производства, отчёты (предпросмотр, Excel, PDF). Доступ решают сервисы по правам из токена — никогда по параметрам запроса.
/// </summary>
[ApiController]
[Authorize]
[FieldErrors]
[Route("api/production")]
public sealed class ProductionController(
    ProcurementRequestService requests,
    ProductionRecipeService recipes,
    ProductionOrderService orders,
    ProductionAnalyticsService analytics,
    IReportWorkbookWriter excel,
    IReportPdfWriter pdf) : ControllerBase
{
    private const int PreviewRows = 100;

    private Task<ProcurementContext> ContextAsync(CancellationToken ct) =>
        requests.ContextAsync(User.GetUserId(), User.FindAll(OneBaseClaims.Permission).Select(c => c.Value).ToHashSet(), ct);

    [HttpGet("nav")]
    public async Task<ProductionNav> Nav(CancellationToken ct) => await analytics.NavAsync(await ContextAsync(ct), ct);

    // ---------- Рецептуры ----------

    [HttpGet("recipes")]
    public async Task<ProductionRecipeList> Recipes([FromQuery] string? q, [FromQuery] string? status, CancellationToken ct) =>
        await recipes.ListAsync(await ContextAsync(ct), q, status, ct);

    [HttpGet("recipes/options")]
    public async Task<IReadOnlyList<ProductionRecipeOption>> RecipeOptions(CancellationToken ct) => await recipes.OptionsAsync(await ContextAsync(ct), ct);

    [HttpGet("recipes/{id:guid}")]
    public async Task<ProductionRecipeDetail> Recipe(Guid id, CancellationToken ct) => await recipes.GetAsync(await ContextAsync(ct), id, ct);

    [HttpPost("recipes")]
    public async Task<IActionResult> CreateRecipe(ProductionRecipeInput input, CancellationToken ct) =>
        Ok(new { id = await recipes.CreateAsync(await ContextAsync(ct), input, ct) });

    [HttpPut("recipes/{id:guid}")]
    public async Task<IActionResult> UpdateRecipe(Guid id, ProductionRecipeInput input, CancellationToken ct)
    {
        await recipes.UpdateAsync(await ContextAsync(ct), id, input, ct);
        return NoContent();
    }

    [HttpPost("recipes/{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        await recipes.ActivateAsync(await ContextAsync(ct), id, ct);
        return NoContent();
    }

    [HttpPost("recipes/{id:guid}/new-version")]
    public async Task<IActionResult> NewVersion(Guid id, CancellationToken ct) => Ok(new { id = await recipes.NewVersionAsync(await ContextAsync(ct), id, ct) });

    [HttpPost("recipes/{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, ProcurementController.CommentBody body, CancellationToken ct)
    {
        await recipes.ArchiveAsync(await ContextAsync(ct), id, body.Comment, ct);
        return NoContent();
    }

    /// <summary>Потребность рецептуры на quantity выхода и доступно на складе warehouse (не указан — на всех складах).</summary>
    [HttpGet("recipes/{id:guid}/requirement")]
    public async Task<ProductionRequirement> Requirement(Guid id, [FromQuery] decimal quantity, [FromQuery] Guid? warehouse, CancellationToken ct) =>
        await recipes.RequirementAsync(await ContextAsync(ct), id, quantity, warehouse, ct);

    [HttpGet("products")]
    public async Task<IReadOnlyList<ProductionProductOption>> Products([FromQuery] string? q, CancellationToken ct) => await recipes.ProductsAsync(await ContextAsync(ct), q, ct);

    // ---------- Задания на выпуск ----------

    /// <summary>Журнал заданий: status — open, Draft, Released, InProgress, Completed, Cancelled; department, from, to (дата выпуска), q.</summary>
    [HttpGet("orders")]
    public async Task<ProductionOrderList> Orders([FromQuery] string? q, [FromQuery] string? status, [FromQuery] Guid? department, [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to, [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken ct = default) =>
        await orders.SearchAsync(await ContextAsync(ct), new ProductionOrderSearch(q, status, department, from, to, page, pageSize), ct);

    [HttpGet("orders/form")]
    public async Task<ProductionOrderForm> OrderForm(CancellationToken ct) => await orders.FormAsync(await ContextAsync(ct), ct);

    [HttpGet("orders/{id:guid}")]
    public async Task<ProductionOrderDetail> Order(Guid id, CancellationToken ct) => await orders.GetAsync(await ContextAsync(ct), id, ct);

    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder(ProductionOrderInput input, CancellationToken ct) => Ok(new { id = await orders.CreateAsync(await ContextAsync(ct), input, ct) });

    [HttpPut("orders/{id:guid}")]
    public async Task<IActionResult> UpdateOrder(Guid id, ProductionOrderInput input, CancellationToken ct)
    {
        await orders.UpdateAsync(await ContextAsync(ct), id, input, ct);
        return NoContent();
    }

    [HttpPost("orders/{id:guid}/release")]
    public async Task<IActionResult> Release(Guid id, CancellationToken ct)
    {
        await orders.ReleaseAsync(await ContextAsync(ct), id, ct);
        return NoContent();
    }

    [HttpPost("orders/{id:guid}/issue")]
    public async Task<ProductionMoveResult> Issue(Guid id, ProductionIssueInput input, CancellationToken ct) => await orders.IssueAsync(await ContextAsync(ct), id, input, ct);

    [HttpPost("orders/{id:guid}/return")]
    public async Task<ProductionMoveResult> Return(Guid id, ProductionReturnInput input, CancellationToken ct) => await orders.ReturnAsync(await ContextAsync(ct), id, input, ct);

    [HttpPost("orders/{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, ProductionCompleteInput input, CancellationToken ct)
    {
        await orders.CompleteAsync(await ContextAsync(ct), id, input, ct);
        return NoContent();
    }

    [HttpPost("orders/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, ProcurementController.CommentBody body, CancellationToken ct)
    {
        await orders.CancelAsync(await ContextAsync(ct), id, body.Comment, ct);
        return NoContent();
    }

    // ---------- Обзор и отчёты ----------

    [HttpGet("overview")]
    public async Task<ProductionOverview> Overview([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] Guid? warehouse, CancellationToken ct) =>
        await analytics.OverviewAsync(await ContextAsync(ct), ProcurementFilter.Of(from, to), warehouse, ct);

    [HttpGet("reports")]
    public IReadOnlyList<ProcurementReportInfo> ReportKinds() => ProductionAnalyticsService.Kinds;

    [HttpGet("reports/{kind}")]
    public async Task<ProcurementAnalyticsController.ReportPreview> Report(string kind, [FromQuery] ProductionReportQuery q, CancellationToken ct)
    {
        var report = await analytics.ReportAsync(await ContextAsync(ct), kind, q.Filter(), q.Warehouse, ct);
        return new ProcurementAnalyticsController.ReportPreview(report.Title, report.Sources,
            report.Sheets.Select(s => new ProcurementAnalyticsController.ReportPreviewSheet(s.Name, s.Columns, s.Rows.Take(PreviewRows).ToList(), s.Rows.Count, s.Total, s.Note))
                .ToList());
    }

    [HttpGet("reports/{kind}/xlsx")]
    public async Task<IActionResult> ReportExcel(string kind, [FromQuery] ProductionReportQuery q, CancellationToken ct)
    {
        var filter = q.Filter();
        var report = await analytics.ReportAsync(await ContextAsync(ct), kind, filter, q.Warehouse, ct);
        return File(excel.Write(report, DateTimeOffset.UtcNow), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", FileName(kind, filter, "xlsx"));
    }

    [HttpGet("reports/{kind}/pdf")]
    public async Task<IActionResult> ReportPdf(string kind, [FromQuery] ProductionReportQuery q, CancellationToken ct)
    {
        var filter = q.Filter();
        var report = await analytics.ReportAsync(await ContextAsync(ct), kind, filter, q.Warehouse, ct);
        return File(pdf.Write(report, DateTimeOffset.UtcNow), "application/pdf", FileName(kind, filter, "pdf"));
    }

    private static string FileName(string kind, ProcurementFilter f, string ext) =>
        $"onebase-production-{(kind.All(c => char.IsAsciiLetterOrDigit(c) || c == '_') ? kind : "report")}-{f.From:yyyy-MM-dd}-{f.To:yyyy-MM-dd}.{ext}";

    public sealed record ProductionReportQuery(DateOnly? From, DateOnly? To, Guid? Department, Guid? Warehouse)
    {
        public ProcurementFilter Filter() => ProcurementFilter.Of(From, To, Department);
    }
}
