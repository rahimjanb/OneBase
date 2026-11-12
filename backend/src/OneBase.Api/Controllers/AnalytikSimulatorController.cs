using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneBase.AI.Analytik.Simulation;
using OneBase.Api.Auth;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Simulation;
using OneBase.Application.Security;

namespace OneBase.Api.Controllers;

public sealed record SimulatorCatalog(IReadOnlyList<ScenarioKindView> Kinds, IReadOnlyList<ParameterDefinition> Parameters, IReadOnlyList<string> Presets,
    IReadOnlyList<GraphNode> Nodes, int MinHorizonDays, int MaxHorizonDays, int MaxCompare, string FormulaVersion);

public sealed record BaselineRequest(DateOnly? From, DateOnly? To);

public sealed record CopyRequest(string? Name);

public sealed record AiText(string Text);

/// <summary>
/// «Аналитик → Симулятор» (What-If): каталог параметров, снимок исходных данных, черновой расчёт, сценарии (сохранить, открыть, копия, правка,
/// архив, пересчёт на актуальных данных), сравнение, набор базовый / оптимистичный / пессимистичный. Права — analytik.use и права разделов
/// (ScenarioService и ScenarioBaselineBuilder); бизнес-данные только читаются.
/// </summary>
[ApiController]
[Authorize]
[HasPermission(Permissions.AnalytikUse)]
[AnalytikErrors]
[Route("api/analytik/simulator")]
public sealed class AnalytikSimulatorController(AnalytikAccess access, ScenarioService scenarios, ScenarioAnalyst analyst, ScenarioExport export)
    : ControllerBase
{
    private Task<AnalytikPrincipal> PrincipalAsync(CancellationToken ct) => access.ResolveAsync(User.GetUserId(), ct);

    [HttpGet("catalog")]
    public SimulatorCatalog Catalog() => new(ScenarioCatalog.Kinds, ScenarioCatalog.Parameters, ScenarioPresets.All, DependencyGraph.Nodes,
        ScenarioCatalog.MinHorizonDays, ScenarioCatalog.MaxHorizonDays, ScenarioService.MaxCompare, ScenarioEngine.FormulaVersion);

    /// <summary>Снимок исходных данных за базовый период (по умолчанию — последние 3 полных месяца).</summary>
    [HttpPost("baseline")]
    public async Task<BaselineView> Baseline([FromBody] BaselineRequest request, CancellationToken ct) =>
        await scenarios.BaselineAsync(await PrincipalAsync(ct), request.From, request.To, ct);

    [HttpGet("baseline/{id:guid}")]
    public async Task<BaselineView> BaselineById(Guid id, CancellationToken ct) => await scenarios.BaselineByIdAsync(await PrincipalAsync(ct), id, ct);

    /// <summary>Черновой расчёт без сохранения.</summary>
    [HttpPost("run")]
    public async Task<SimulationResult> Run([FromBody] RunInput input, CancellationToken ct) => await scenarios.RunAsync(await PrincipalAsync(ct), input, ct);

    [HttpGet("scenarios")]
    public async Task<IReadOnlyList<ScenarioRow>> List([FromQuery] bool archived, CancellationToken ct) =>
        await scenarios.ListAsync(await PrincipalAsync(ct), archived, ct);

    [HttpGet("scenarios/{id:guid}")]
    public async Task<ScenarioView> Get(Guid id, CancellationToken ct) => await scenarios.GetAsync(await PrincipalAsync(ct), id, ct);

    [HttpPost("scenarios")]
    public async Task<ScenarioView> Create([FromBody] ScenarioInput input, CancellationToken ct) =>
        await scenarios.SaveAsync(await PrincipalAsync(ct), null, input, ct);

    [HttpPut("scenarios/{id:guid}")]
    public async Task<ScenarioView> Update(Guid id, [FromBody] ScenarioInput input, CancellationToken ct) =>
        await scenarios.SaveAsync(await PrincipalAsync(ct), id, input, ct);

    [HttpPost("scenarios/{id:guid}/copy")]
    public async Task<ScenarioView> Copy(Guid id, [FromBody] CopyRequest? request, CancellationToken ct) =>
        await scenarios.CopyAsync(await PrincipalAsync(ct), id, request?.Name, ct);

    [HttpPost("scenarios/{id:guid}/archive")]
    public async Task<ScenarioView> Archive(Guid id, CancellationToken ct) => await scenarios.ArchiveAsync(await PrincipalAsync(ct), id, true, ct);

    [HttpPost("scenarios/{id:guid}/restore")]
    public async Task<ScenarioView> Restore(Guid id, CancellationToken ct) => await scenarios.ArchiveAsync(await PrincipalAsync(ct), id, false, ct);

    /// <summary>«Пересчитать на актуальных данных»: новый снимок, прежний расчёт — в историю.</summary>
    [HttpPost("scenarios/{id:guid}/recalculate")]
    public async Task<ScenarioView> Recalculate(Guid id, CancellationToken ct) => await scenarios.RecalculateAsync(await PrincipalAsync(ct), id, ct);

    /// <summary>Набор «базовый / оптимистичный / пессимистичный» по сценарию.</summary>
    [HttpPost("scenarios/{id:guid}/set")]
    public async Task<IReadOnlyList<ScenarioView>> Set(Guid id, CancellationToken ct) => await scenarios.CreateSetAsync(await PrincipalAsync(ct), id, ct);

    /// <summary>AI-анализ сохранённого сценария (по кнопке; автору — сохраняется в сценарии).</summary>
    [HttpPost("scenarios/{id:guid}/ai")]
    public async Task<ActionResult<AiText>> Ai(Guid id, CancellationToken ct)
    {
        try
        {
            return new AiText(await analyst.ExplainAsync(await PrincipalAsync(ct), id, ct));
        }
        catch (AiUnavailableException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    /// <summary>AI-анализ несохранённого черновика (не сохраняется).</summary>
    [HttpPost("ai")]
    public async Task<ActionResult<AiText>> AiDraft([FromBody] RunInput input, CancellationToken ct)
    {
        try
        {
            return new AiText(await analyst.ExplainDraftAsync(await PrincipalAsync(ct), input, ct));
        }
        catch (AiUnavailableException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpGet("compare")]
    public async Task<ScenarioComparison> Compare([FromQuery] string ids, CancellationToken ct) =>
        await scenarios.CompareAsync(await PrincipalAsync(ct), Ids(ids), ct);

    /// <summary>Экспорт сценария: xlsx или pdf; compare — id других сценариев для листа сравнения.</summary>
    [HttpGet("scenarios/{id:guid}/export/{format}")]
    public async Task<IActionResult> Export(Guid id, string format, [FromQuery] string? compare, CancellationToken ct)
    {
        var file = await export.FileAsync(await PrincipalAsync(ct), id, format, Ids(compare), ct);
        return File(file.Content, file.ContentType, file.FileName);
    }

    private static List<Guid> Ids(string? ids) => (ids ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(x => Guid.TryParse(x, out var g) ? g : throw new ArgumentException("Неверный id сценария.")).ToList();
}
