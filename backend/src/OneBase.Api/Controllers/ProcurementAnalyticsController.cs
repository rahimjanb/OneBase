using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Procurement;
using OneBase.Application.Reports;

namespace OneBase.Api.Controllers;

/// <summary>
/// Снабжение — контроль: обзор по ролям (директор, руководитель снабжения, сотрудник снабжения, кладовщик), KPI, отчёты с фильтрами
/// (предпросмотр, Excel, PDF), журнал ежедневных сводок, заявка документом Word, акт приёмки PDF. Доступ решают сервисы по правам из
/// токена и роли «Директор» из базы.
/// </summary>
[ApiController]
[Authorize]
[FieldErrors]
[Route("api/procurement")]
public sealed class ProcurementAnalyticsController(
    ProcurementRequestService requests,
    ProcurementAnalyticsService analytics,
    ProcurementReportService reports,
    ProcurementReceiptService receipts,
    IReportWorkbookWriter excel,
    IReportPdfWriter pdf,
    IDocumentWriter word) : ControllerBase
{
    /// <summary>Строк на лист в предпросмотре отчёта (файл — полностью).</summary>
    private const int PreviewRows = 100;

    private Task<ProcurementContext> ContextAsync(CancellationToken ct) =>
        requests.ContextAsync(User.GetUserId(), User.FindAll(OneBaseClaims.Permission).Select(c => c.Value).ToHashSet(), ct);

    [HttpGet("dashboard")]
    public async Task<ProcurementDashboard> Dashboard([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        await analytics.DashboardAsync(await ContextAsync(ct), ProcurementFilter.Of(from, to), ct);

    /// <summary>Обзор снабжения: что требует действия, путь закупки, показатели и аналитика периода, сотрудники — по правам роли.</summary>
    [HttpGet("board")]
    public async Task<ProcurementBoard> Board([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        await analytics.BoardAsync(await ContextAsync(ct), ProcurementFilter.Of(from, to), ct);

    [HttpGet("reports")]
    public IReadOnlyList<ProcurementReportInfo> ReportKinds() => ProcurementReportService.Kinds;

    /// <summary>Предпросмотр отчёта: те же листы, что в файле, — первые 100 строк каждого (в листе — полное число строк).</summary>
    [HttpGet("reports/{kind}")]
    public async Task<ReportPreview> Report(string kind, [FromQuery] ReportQuery q, CancellationToken ct)
    {
        var report = await reports.BuildAsync(await ContextAsync(ct), kind, q.Filter(), ct);
        return new ReportPreview(report.Title, report.Sources,
            report.Sheets.Select(s => new ReportPreviewSheet(s.Name, s.Columns, s.Rows.Take(PreviewRows).ToList(), s.Rows.Count, s.Total, s.Note)).ToList());
    }

    [HttpGet("reports/{kind}/xlsx")]
    public async Task<IActionResult> ReportExcel(string kind, [FromQuery] ReportQuery q, CancellationToken ct)
    {
        var filter = q.Filter();
        var report = await reports.BuildAsync(await ContextAsync(ct), kind, filter, ct);
        return File(excel.Write(report, DateTimeOffset.UtcNow), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", FileName(kind, filter, "xlsx"));
    }

    [HttpGet("reports/{kind}/pdf")]
    public async Task<IActionResult> ReportPdf(string kind, [FromQuery] ReportQuery q, CancellationToken ct)
    {
        var filter = q.Filter();
        var report = await reports.BuildAsync(await ContextAsync(ct), kind, filter, ct);
        return File(pdf.Write(report, DateTimeOffset.UtcNow), "application/pdf", FileName(kind, filter, "pdf"));
    }

    [HttpGet("digests")]
    public async Task<PageResult<ProcurementDigestRow>> Digests([FromQuery] int page = 1, CancellationToken ct = default) =>
        await analytics.DigestPageAsync(await ContextAsync(ct), page, ct);

    [HttpGet("digests/{id:guid}")]
    public async Task<ProcurementDigestView> Digest(Guid id, CancellationToken ct) => await analytics.DigestAsync(await ContextAsync(ct), id, ct);

    /// <summary>Заявка документом Word — кому видна карточка заявки.</summary>
    [HttpGet("requests/{id:guid}/docx")]
    public async Task<IActionResult> RequestDocument(Guid id, CancellationToken ct)
    {
        var file = await requests.DocumentAsync(await ContextAsync(ct), id, word, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>Акт приёмки PDF — по подтверждённой приёмке.</summary>
    [HttpGet("receipts/{id:guid}/act")]
    public async Task<IActionResult> ReceiptAct(Guid id, CancellationToken ct)
    {
        var act = await receipts.ActAsync(await ContextAsync(ct), id, ct);
        return File(pdf.Write(act, DateTimeOffset.UtcNow), "application/pdf", $"{act.Title.Split(' ').Last()}.pdf");
    }

    private static string FileName(string kind, ProcurementFilter f, string ext) => $"procurement-{kind}-{f.From:yyyyMMdd}-{f.To:yyyyMMdd}.{ext}";

    public sealed record ReportQuery(DateOnly? From, DateOnly? To, Guid? Department, Guid? Category, Guid? Employee, Guid? Supplier)
    {
        public ProcurementFilter Filter() => ProcurementFilter.Of(From, To, Department, Category, Employee, Supplier);
    }

    public sealed record ReportPreviewSheet(string Name, IReadOnlyList<AiReportColumn> Columns, IReadOnlyList<IReadOnlyList<System.Text.Json.JsonElement>> Rows, int TotalRows,
        bool Total, string? Note);

    public sealed record ReportPreview(string Title, IReadOnlyList<string> Sources, IReadOnlyList<ReportPreviewSheet> Sheets);
}
