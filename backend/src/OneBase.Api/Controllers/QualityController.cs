using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Procurement;

namespace OneBase.Api.Controllers;

/// <summary>
/// Контроль качества сырья: очередь и архив проверок партий, проверка документов, лаборатория (показатели, пробы), решение «допустить /
/// забраковать», блокировка партий, спецификации материалов, кто решает по качеству. Доступ решает ProcurementQualityService по правам из
/// токена и назначению в настройках качества — никогда по параметрам запроса.
/// </summary>
[ApiController]
[Authorize]
[FieldErrors]
[Route("api/quality")]
public sealed class QualityController(ProcurementRequestService requests, ProcurementQualityService quality) : ControllerBase
{
    private Task<ProcurementContext> ContextAsync(CancellationToken ct) =>
        requests.ContextAsync(User.GetUserId(), User.FindAll(OneBaseClaims.Permission).Select(c => c.Value).ToHashSet(), ct);

    [HttpGet("nav")]
    public async Task<ProcurementQualityNav> Nav(CancellationToken ct) => await quality.NavAsync(await ContextAsync(ct), ct);

    /// <summary>Проверки: status — open (по умолчанию), documents, lab, decision, decided, rejected, cancelled, all; warehouse, item, batch, q.</summary>
    [HttpGet("checks")]
    public async Task<PageResult<ProcurementQualityCheckRow>> Checks(
        [FromQuery] string? status = "open",
        [FromQuery] Guid? warehouse = null,
        [FromQuery] Guid? item = null,
        [FromQuery] Guid? batch = null,
        [FromQuery] string? q = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        await quality.ListAsync(await ContextAsync(ct), new ProcurementQualitySearch(status, warehouse, item, batch, q, page, pageSize), ct);

    [HttpGet("checks/{id:guid}")]
    public async Task<ProcurementQualityCheckCard> Check(Guid id, CancellationToken ct) => await quality.CardAsync(await ContextAsync(ct), id, ct);

    /// <summary>Проверка документов — лаборатория.</summary>
    [HttpPut("checks/{id:guid}/documents")]
    public async Task<IActionResult> Documents(Guid id, ProcurementQualityDocumentsInput input, CancellationToken ct)
    {
        await quality.DocumentsAsync(await ContextAsync(ct), id, input, ct);
        return NoContent();
    }

    /// <summary>Показатели и проба — лаборатория (после документов в порядке).</summary>
    [HttpPut("checks/{id:guid}/lab")]
    public async Task<IActionResult> Lab(Guid id, ProcurementQualityLabInput input, CancellationToken ct)
    {
        await quality.LabAsync(await ContextAsync(ct), id, input, ct);
        return NoContent();
    }

    /// <summary>Решение по партии — назначенный в настройках качества (администратор не решает).</summary>
    [HttpPost("checks/{id:guid}/decide")]
    public async Task<IActionResult> Decide(Guid id, ProcurementQualityDecisionInput input, CancellationToken ct)
    {
        await quality.DecideAsync(await ContextAsync(ct), id, input, ct);
        return NoContent();
    }

    [HttpPost("batches/{id:guid}/block")]
    public async Task<IActionResult> Block(Guid id, ProcurementBlockInput input, CancellationToken ct)
    {
        await quality.BlockAsync(await ContextAsync(ct), id, input, ct);
        return NoContent();
    }

    [HttpPost("batches/{id:guid}/unblock")]
    public async Task<IActionResult> Unblock(Guid id, ProcurementBlockInput input, CancellationToken ct)
    {
        await quality.UnblockAsync(await ContextAsync(ct), id, input, ct);
        return NoContent();
    }

    [HttpGet("specs/{itemId:guid}")]
    public async Task<ProcurementQualitySpecsView> Specs(Guid itemId, CancellationToken ct) => await quality.SpecsAsync(await ContextAsync(ct), itemId, ct);

    [HttpPut("specs/{itemId:guid}")]
    public async Task<IActionResult> UpdateSpecs(Guid itemId, ProcurementQualitySpecsInput input, CancellationToken ct)
    {
        await quality.UpdateSpecsAsync(await ContextAsync(ct), itemId, input, ct);
        return NoContent();
    }

    [HttpGet("settings")]
    public async Task<ProcurementQualitySettingsView> Settings(CancellationToken ct) => await quality.SettingsAsync(await ContextAsync(ct), ct);

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings(ProcurementQualitySettingsInput input, CancellationToken ct)
    {
        await quality.UpdateSettingsAsync(await ContextAsync(ct), input, ct);
        return NoContent();
    }
}
