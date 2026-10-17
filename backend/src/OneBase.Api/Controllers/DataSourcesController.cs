using Microsoft.AspNetCore.Mvc;
using OneBase.Api.Auth;
using OneBase.Application.DataSources;
using OneBase.Application.Security;
using OneBase.Infrastructure.DataSources;

namespace OneBase.Api.Controllers;

/// <summary>
/// «Настройки → Источники данных»: подключения Google Sheets, привязки разделов продаж к листам (какие поля обновлять и как часто)
/// и синхронизация. Запись данных — планом изменений AI Data Manager, поэтому привязки и запуск требуют ещё и его права.
/// Журнал конфликтов источников: строки таблиц, которые не записаны, и попытки изменить чужое поле.
/// </summary>
[ApiController]
[Route("api/settings/data-sources")]
[HasPermission(Permissions.IntegrationsManage)]
public sealed class DataSourcesController(DataSourceService sources, DataSourceOwnershipService ownership, DataSourceAutoMapService autoMap) : ControllerBase
{
    public sealed record PreviewRequest(Guid ConnectionId, string Sheet);

    [HttpGet]
    public async Task<IActionResult> Overview(CancellationToken ct) => Ok(await sources.OverviewAsync(User.GetUserId(), ct));

    [HttpPost("connections")]
    public Task<IActionResult> CreateConnection(ConnectionInput input, CancellationToken ct) =>
        Run(async () => Ok(await sources.CreateConnectionAsync(input, User.GetUserId(), ct)));

    [HttpPut("connections/{id:guid}")]
    public Task<IActionResult> UpdateConnection(Guid id, ConnectionInput input, CancellationToken ct) =>
        Run(async () => Ok(await sources.UpdateConnectionAsync(id, input, User.GetUserId(), ct)));

    [HttpDelete("connections/{id:guid}")]
    public Task<IActionResult> DeleteConnection(Guid id, CancellationToken ct) =>
        Run(async () =>
        {
            await sources.DeleteConnectionAsync(id, User.GetUserId(), ct);
            return NoContent();
        });

    /// <summary>Проверить таблицу: читается ли, какие листы.</summary>
    [HttpPost("connections/{id:guid}/test")]
    public Task<IActionResult> TestConnection(Guid id, CancellationToken ct) => Run(async () => Ok(await sources.TestConnectionAsync(id, ct)));

    /// <summary>Первые строки листа — для выбора столбцов привязки.</summary>
    [HttpPost("preview")]
    public Task<IActionResult> Preview(PreviewRequest request, CancellationToken ct) =>
        Run(async () => Ok(await sources.PreviewAsync(request.ConnectionId, request.Sheet, ct)));

    [HttpPost("bindings")]
    public Task<IActionResult> CreateBinding(BindingInput input, CancellationToken ct) =>
        Run(async () => Ok(await sources.SaveBindingAsync(null, input, User.GetUserId(), ct)));

    [HttpPut("bindings/{id:guid}")]
    public Task<IActionResult> UpdateBinding(Guid id, BindingInput input, CancellationToken ct) =>
        Run(async () => Ok(await sources.SaveBindingAsync(id, input, User.GetUserId(), ct)));

    [HttpDelete("bindings/{id:guid}")]
    public Task<IActionResult> DeleteBinding(Guid id, CancellationToken ct) =>
        Run(async () =>
        {
            await sources.DeleteBindingAsync(id, User.GetUserId(), ct);
            return NoContent();
        });

    /// <summary>Синхронизировать сейчас — итог по каждому полю.</summary>
    [HttpPost("bindings/{id:guid}/sync")]
    public Task<IActionResult> Sync(Guid id, CancellationToken ct) =>
        Run(async () => Ok(await sources.RunAsync(id, "manual", User.GetUserId(), ct)));

    /// <summary>Журнал синхронизаций привязки (последние 30).</summary>
    [HttpGet("bindings/{id:guid}/runs")]
    public async Task<IActionResult> Runs(Guid id, CancellationToken ct) => Ok(await sources.RunsAsync(id, ct));

    /// <summary>
    /// ИИ-сопоставление: лист → предложения «столбец → поле OneBase» с уверенностью, причиной и предупреждениями (для существующей
    /// привязки — BindingId в теле). Ничего не записывает.
    /// </summary>
    [HttpPost("auto-map")]
    public Task<IActionResult> AutoMap(AutoMapInput input, CancellationToken ct) =>
        Run(async () => Ok(await autoMap.ProposeAsync(input, User.GetUserId(), ct)));

    /// <summary>Подтверждение сопоставления: принятые соответствия → привязка (DryRun — только собрать поля для ручного редактора).</summary>
    [HttpPost("auto-map/confirm")]
    public Task<IActionResult> ConfirmAutoMap(AutoMapConfirmInput input, CancellationToken ct) =>
        Run(async () => Ok(await autoMap.ConfirmAsync(input, User.GetUserId(), ct)));

    /// <summary>Журнал конфликтов: открытые и закрытые за 30 дней.</summary>
    [HttpGet("conflicts")]
    public async Task<IActionResult> Conflicts(CancellationToken ct) => Ok(await ownership.ConflictsAsync(ct));

    /// <summary>Закрыть конфликт вручную; если строка всё ещё с ошибкой, следующая синхронизация откроет его снова.</summary>
    [HttpPost("conflicts/{id:guid}/close")]
    public Task<IActionResult> CloseConflict(Guid id, CancellationToken ct) =>
        Run(async () =>
        {
            await ownership.CloseAsync(id, User.GetUserId(), ct);
            return NoContent();
        });

    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (DataSourceException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Не найдено — обновите страницу." });
        }
    }
}
