using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Semantic;
using OneBase.Application.Reports;
using OneBase.Application.Security;
using OneBase.Domain.Audit;

namespace OneBase.Api.Controllers;

/// <summary>
/// «Аналитик → Срезы»: реестр возможностей, срез (показатели × измерения × шаг × период × фильтры) и Excel. Тот же движок, что у AI
/// (analytics_query): права, область продаж и журнал analytik.Queries — на сервере; бизнес-данные только читаются.
/// </summary>
[ApiController]
[Authorize]
[HasPermission(Permissions.AnalytikUse)]
[AnalytikErrors]
[Route("api/analytik/explore")]
public sealed class AnalytikExploreController(SemanticEngine engine, CapabilityRegistry registry, IReportWorkbookWriter writer, IAuditLogger audit, TimeProvider clock)
    : ControllerBase
{
    /// <summary>Строк в выгрузке Excel.</summary>
    public const int ExportRows = 50_000;

    private QueryCaller Caller => new(User.GetUserId(), "api");

    [HttpGet("capabilities")]
    public async Task<CapabilityView> Capabilities(CancellationToken ct) => await registry.ViewAsync(Caller, ct);

    /// <summary>Паспорта показателей: смысл, формула, таблица и поле, тип, агрегация, шаги, измерения, совместимые факты, диапазон данных, свежесть, доступ.</summary>
    [HttpGet("passports")]
    public async Task<IReadOnlyList<MetricPassport>> Passports(CancellationToken ct) => await registry.PassportsAsync(Caller, ct);

    /// <summary>Карта показателей отделов: где считается каждый (движок, расчёт, инструмент) и чего в OneBase нет, с живым наличием данных.</summary>
    [HttpGet("business")]
    public async Task<IReadOnlyList<BusinessMetricState>> Business(CancellationToken ct) => await registry.BusinessAsync(Caller, ct);

    /// <summary>Совместимость пары показателей (Data Compatibility Engine): класс, пункты проверки, совет. dims — через запятую.</summary>
    [HttpGet("compatibility")]
    public async Task<CompatibilityResult> Compatibility([FromQuery] string a, [FromQuery] string b, [FromQuery] string? dims, [FromQuery] string? grain,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        await registry.CompatibilityAsync(Caller, a, b, dims?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), grain, from, to, ct);

    [HttpPost("query")]
    public async Task<AnalyticsResult> Query([FromBody] AnalyticsQuery query, CancellationToken ct) => await engine.RunAsync(query, Caller, ct);

    /// <summary>Срез целиком в Excel: лист данных (с итогом), лист «Проверка и источники».</summary>
    [HttpPost("export")]
    public async Task<IActionResult> Export([FromBody] AnalyticsQuery query, CancellationToken ct)
    {
        var r = await engine.RunAsync(query with { Limit = ExportRows, Offset = 0 }, Caller, ct);
        var columns = r.Columns.Select(c => new AiReportColumn(c.Title, c.Format, c.Kind is "metric" or "compare" or "delta" && c.Format is not "percent"
            && c.Code is not ("outlets" or "orders" or "avg_check" or "avg_price_kg" or "cover_days"))).ToList();
        var rows = r.Rows.Select(row => (IReadOnlyList<JsonElement>)row.Select(v => JsonSerializer.SerializeToElement(v)).ToList()).ToList();
        var metricIndex = r.Columns.Select((c, i) => (c, i)).Where(x => x.c.Kind == "metric").Select(x => x.i).Take(2).ToList();
        var labelIndex = r.Columns.ToList().FindIndex(c => c.Kind == "dimension" && c.Code != "date");
        var data = new AiReportSheet("Срез", columns, rows, Total: true,
            Note: $"Период {r.From:dd.MM.yyyy} — {r.To:dd.MM.yyyy}" + (r.CompareFrom is { } cf ? $"; сравнение {cf:dd.MM.yyyy} — {r.CompareTo:dd.MM.yyyy}" : "") +
                  (r.Scope is null ? "" : $"; область: {r.Scope}"),
            Chart: labelIndex >= 0 && metricIndex.Count > 0 ? new AiReportChart(labelIndex, metricIndex) : null);
        var checks = new AiReportSheet("Проверка и источники",
            [new AiReportColumn("Что", ReportFormats.Text), new AiReportColumn("Результат", ReportFormats.Text), new AiReportColumn("Подробно", ReportFormats.Text)],
            r.Checks.Select(c => (IReadOnlyList<JsonElement>)[Json("Проверка"), Json(c.Ok ? "да" : "нет"), Json(c.Text)])
                .Concat(r.Sources.Select(s => (IReadOnlyList<JsonElement>)[Json("Источник"), Json(s.Title),
                    Json($"{s.Tables}. {s.Rules}. Загружено: {s.SyncedAt:dd.MM.yyyy HH:mm}; данные по {s.DataThrough:dd.MM.yyyy}")]))
                .Concat(r.Notes.Select(n => (IReadOnlyList<JsonElement>)[Json("Заметка"), Json(""), Json(n)]))
                .ToList());
        var title = "Срез: " + string.Join(", ", r.Columns.Where(c => c.Kind == "metric").Select(c => c.Title).Take(3));
        var bytes = writer.Write(new AiReportContent(title, [data, checks], r.Sources.Select(s => s.Title).ToList()), clock.GetUtcNow());
        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "analytik.explore.export", "AnalytikQuery", r.QueryId.ToString(),
            new { r.TotalRows, r.From, r.To }, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"srez-{r.From:yyyyMMdd}-{r.To:yyyyMMdd}.xlsx");
    }

    private static JsonElement Json(string value) => JsonSerializer.SerializeToElement(value);
}
