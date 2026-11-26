using Microsoft.AspNetCore.Mvc;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Finance;
using OneBase.Application.Security;
using OneBase.Domain.Audit;
using OneBase.Finance.Integration.Contracts;

namespace OneBase.Api.Controllers;

/// <summary>
/// «Настройки → Интеграции → Финансы → 1С» — только у права «Интеграции». Подключение к 1С задаётся здесь (пароль и API Secret — только запись:
/// обратно не отдаются, в аудит не пишутся) или переменными службы; ключ службы показывается один раз при создании. Действия передаются службе
/// командами и пишутся в аудит (integration.onec.*).
/// </summary>
[ApiController]
[Route("api/integrations/onec")]
[HasPermission(Permissions.IntegrationsManage)]
public sealed class OneCController(OneCAdminService admin, IOneCSettingsStore settings, IAuditLogger audit) : ControllerBase
{
    public const string Name = "1С:Бухгалтерия";
    public const string Description = "Бухгалтерия для Узбекистана (VENKON GROUP): план счетов, проводки, остатки и обороты, контрагенты, документы, валюты — только чтение, 1С → OneBase.";

    /// <summary>Пароль и API Secret: null — не менять, "" — убрать. Адрес "" — убрать подключение со страницы (служба возьмёт своё).</summary>
    public sealed record SettingsInput(bool? Paused, int? IntervalMinutes, int? BatchSize, bool? AiEnabled, string? BaseUrl = null, string? Username = null,
        string? Password = null, string? ApiSecret = null, string? CaCertificatePem = null, List<string>? OrganizationIds = null, List<string>? Modules = null,
        DateOnly? HistoryFrom = null, bool ClearHistoryFrom = false);

    public sealed record HistoricalInput(DateOnly From, DateOnly To, int? BatchSize, bool Confirm);

    public sealed record RetryInput(List<Guid>? Ids);

    public sealed record ClearInput(string? Confirmation);

    /// <summary>Синхронизировать сейчас: modules — только эти модули (пусто — все включённые).</summary>
    public sealed record SyncInput(List<string>? Modules);

    [HttpGet]
    public Task<OneCStatusView> Status(CancellationToken ct) => admin.StatusAsync(ct);

    /// <summary>Для списка интеграций отдела: not_configured | disabled | error | connected и время последней успешной синхронизации.</summary>
    public static async Task<(string Status, DateTimeOffset? DataAsOf)> StatusAsync(OneCAdminService admin, CancellationToken ct)
    {
        var view = await admin.StatusAsync(ct);
        var status = view.State switch
        {
            "not_configured" or "waiting_worker" => "not_configured",
            "paused" => "disabled",
            "connected" => "connected",
            _ => "error",
        };
        return (status, view.LastSuccess?.FinishedAt);
    }

    [HttpPut("settings")]
    public async Task<IActionResult> Save(SettingsInput input, CancellationToken ct)
    {
        OneCSettings saved;
        try
        {
            saved = await settings.SaveAsync(new OneCSettingsChange(input.Paused, input.IntervalMinutes, input.BatchSize, input.AiEnabled, input.BaseUrl,
                input.Username, input.Password, input.ApiSecret, input.CaCertificatePem, input.OrganizationIds, input.Modules, input.HistoryFrom,
                input.ClearHistoryFrom), User.GetUserId(), ct);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { error = e.Message });
        }

        // В аудит — что изменилось, без пароля и секрета (только «задан / убран»).
        await Audit("integration.onec.updated", new
        {
            input.Paused, input.IntervalMinutes, input.BatchSize, input.AiEnabled, input.BaseUrl, input.Username,
            Password = input.Password is null ? null : input.Password.Length == 0 ? "убран" : "задан",
            ApiSecret = input.ApiSecret is null ? null : input.ApiSecret.Length == 0 ? "убран" : "задан",
            Certificate = input.CaCertificatePem is null ? null : input.CaCertificatePem.Length == 0 ? "убран" : "задан",
            input.OrganizationIds, input.Modules, input.HistoryFrom, input.ClearHistoryFrom,
        }, ct);
        return Ok(saved);
    }

    /// <summary>Новый ключ службы интеграции — показывается один раз; прежний перестаёт действовать сразу.</summary>
    [HttpPost("token")]
    public async Task<IActionResult> Token(CancellationToken ct)
    {
        var token = await settings.CreateTokenAsync(User.GetUserId(), ct);
        await Audit("integration.onec.token_created", new { hint = "••••" + token[^4..] }, ct);
        return Ok(new { token });
    }

    [HttpPost("test")]
    public Task<IActionResult> Test(CancellationToken ct) => Command(WorkerCommandKinds.TestConnection, "integration.onec.tested", ct);

    /// <summary>Тестовая синхронизация: по странице каждого справочника и изменений — проверка без записи.</summary>
    [HttpPost("test-sync")]
    public Task<IActionResult> TestSync(CancellationToken ct) => Command(WorkerCommandKinds.TestSync, "integration.onec.test_sync", ct);

    [HttpPost("sync")]
    public async Task<IActionResult> Sync([FromBody] SyncInput? input, CancellationToken ct)
    {
        var modules = input?.Modules is { Count: > 0 } list ? OneCModules.Normalize(list).ToList() : null;
        var command = await admin.RequestAsync(WorkerCommandKinds.SyncNow, modules is null ? null : new { modules }, User.GetUserId(), ct);
        await Audit("integration.onec.sync", new { command.Id, modules }, ct);
        return Accepted(command);
    }

    [HttpPost("pause")]
    public Task<OneCSettings> Pause(CancellationToken ct) => SetPausedAsync(true, ct);

    [HttpPost("resume")]
    public Task<OneCSettings> Resume(CancellationToken ct) => SetPausedAsync(false, ct);

    /// <summary>Историческая загрузка за период — только с подтверждением (Confirm=true после окна подтверждения).</summary>
    [HttpPost("historical")]
    public async Task<IActionResult> Historical(HistoricalInput input, CancellationToken ct)
    {
        if (!input.Confirm)
        {
            return BadRequest(new { error = "Подтвердите историческую загрузку." });
        }

        try
        {
            var job = await admin.StartHistoricalAsync(input.From, input.To, input.BatchSize, User.GetUserId(), ct);
            await Audit("integration.onec.historical_started", new { input.From, input.To, job.BatchSize, job.Id }, ct);
            return Ok(job);
        }
        catch (OneCImportException e)
        {
            return BadRequest(new { error = e.Message });
        }
        catch (OneCConflictException e)
        {
            return Conflict(new { error = e.Message });
        }
    }

    [HttpPost("historical/cancel")]
    public async Task<IActionResult> CancelHistorical(CancellationToken ct)
    {
        if (!await admin.CancelHistoricalAsync(ct))
        {
            return Conflict(new { error = "Историческая загрузка не идёт." });
        }

        await Audit("integration.onec.historical_cancelled", new { }, ct);
        return NoContent();
    }

    [HttpGet("runs")]
    public Task<OneCPage<OneCRunView>> Runs([FromQuery] int page = 1, [FromQuery] int pageSize = 30, [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null,
        [FromQuery] string? status = null, [FromQuery] string? kind = null, CancellationToken ct = default) =>
        admin.RunsAsync(page, pageSize, ct, from, to, status, kind);

    [HttpGet("runs/{id:guid}")]
    public async Task<IActionResult> Run(Guid id, CancellationToken ct) => await admin.RunAsync(id, ct) is { } run ? Ok(run) : NotFound();

    [HttpGet("failures")]
    public Task<OneCPage<OneCFailureView>> Failures([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 30, [FromQuery] string? stream = null,
        [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null, CancellationToken ct = default) =>
        admin.FailuresAsync(status, page, pageSize, ct, stream, from, to);

    [HttpPost("failures/retry")]
    public async Task<IActionResult> Retry(RetryInput input, CancellationToken ct)
    {
        var count = await admin.RetryFailuresAsync(input.Ids, User.GetUserId(), ct);
        await Audit("integration.onec.failures_retried", new { count }, ct);
        return Ok(new { count });
    }

    [HttpPost("failures/{id:guid}/ignore")]
    public async Task<IActionResult> Ignore(Guid id, CancellationToken ct)
    {
        if (!await admin.IgnoreFailureAsync(id, ct))
        {
            return NotFound();
        }

        await Audit("integration.onec.failure_ignored", new { id }, ct);
        return NoContent();
    }

    [HttpGet("changes")]
    public Task<OneCPage<OneCChangeView>> Changes([FromQuery] string? entityType, [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken ct = default) =>
        admin.ChangesAsync(entityType, page, pageSize, ct);

    /// <summary>
    /// Подключить другую базу 1С: удалить загруженные данные прежней базы (журнал запусков остаётся). Только с подтверждением словом «ОЧИСТИТЬ».
    /// </summary>
    [HttpPost("clear")]
    public async Task<IActionResult> Clear(ClearInput input, CancellationToken ct)
    {
        if (!string.Equals(input.Confirmation?.Trim(), "ОЧИСТИТЬ", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Для подтверждения введите слово ОЧИСТИТЬ." });
        }

        try
        {
            var before = await settings.GetAsync(ct);
            var removed = await admin.ClearDataAsync(ct);
            await Audit("integration.onec.data_cleared", new { removed, infobase = before.AcceptedInfobaseName ?? before.AcceptedInfobaseId }, ct);
            return Ok(new { removed });
        }
        catch (OneCConflictException e)
        {
            return Conflict(new { error = e.Message });
        }
    }

    private async Task<IActionResult> Command(string kind, string action, CancellationToken ct)
    {
        var command = await admin.RequestAsync(kind, null, User.GetUserId(), ct);
        await Audit(action, new { command.Id }, ct);
        return Accepted(command);
    }

    private async Task<OneCSettings> SetPausedAsync(bool paused, CancellationToken ct)
    {
        var saved = await settings.SaveAsync(new OneCSettingsChange(Paused: paused), User.GetUserId(), ct);
        await Audit(paused ? "integration.onec.paused" : "integration.onec.resumed", new { }, ct);
        return saved;
    }

    private Task Audit(string action, object data, CancellationToken ct) =>
        audit.LogAsync(ActorType.User, User.GetUserId().ToString(), action, "integration", OneCSettings.Code, data, ct);
}
