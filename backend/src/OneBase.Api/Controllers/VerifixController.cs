using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Hr;
using OneBase.Application.Security;
using OneBase.Domain.Audit;
using OneBase.Domain.Hr;
using OneBase.Infrastructure.Hr;

namespace OneBase.Api.Controllers;

/// <summary>
/// «Настройки → Интеграции → HR → Verifix» и журнал синхронизации — только у права «Интеграции» (администратор).
/// client_secret наружу не отдаётся: только «••••» и последние символы; в журнал аудита — лишь признак «секрет изменён».
/// </summary>
[ApiController]
[Route("api/integrations/verifix")]
[HasPermission(Permissions.IntegrationsManage)]
public sealed class VerifixController(
    IAppDbContext db,
    VerifixSettingsStore settings,
    VerifixClient client,
    HrSyncCoordinator coordinator,
    HrService hr,
    IHrRules rules,
    TimeProvider time,
    IAuditLogger audit,
    ILogger<VerifixController> logger) : ControllerBase
{
    public const string Name = "Verifix";
    public const string Description = "Учёт рабочего времени: сотрудники, отметки турникетов и терминалов, графики и табель — источник данных раздела HR.";

    public sealed record SettingsInput(
        string? BaseUrl,
        string? ClientId,
        string? ClientSecret,
        long? FilialId,
        bool Enabled,
        bool AutoSync,
        int IntervalMinutes,
        int HistoryDays,
        int OverlapDays,
        int LateGraceMinutes,
        int EarlyLeaveGraceMinutes);

    public sealed record TestInput(string? BaseUrl, string? ClientId, string? ClientSecret, long? FilialId);

    public sealed record DepartmentInput(string? DepartmentCode);

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [HttpGet]
    public async Task<IActionResult> Details(CancellationToken ct) => Ok(await DetailsAsync(ct));

    /// <summary>Сохранить. ClientSecret: null — не менять, "" — удалить сохранённый в OneBase.</summary>
    [HttpPut]
    public async Task<IActionResult> Save(SettingsInput input, CancellationToken ct)
    {
        var baseUrl = input.BaseUrl?.Trim() ?? string.Empty;
        if (baseUrl.Length > 0 && !VerifixSettingsStore.IsValidUrl(baseUrl))
        {
            return BadRequest(new { error = "Адрес API должен начинаться с https:// — по документации Verifix это https://app.verifix.com" });
        }

        if (input.IntervalMinutes is < VerifixSettingsStore.MinInterval or > VerifixSettingsStore.MaxInterval)
        {
            return BadRequest(new { error = $"Интервал синхронизации — от {VerifixSettingsStore.MinInterval} минут до суток." });
        }

        await settings.SaveAsync(new VerifixSettingsInput(baseUrl, input.ClientId, input.ClientSecret, input.FilialId, input.Enabled, input.AutoSync,
            input.IntervalMinutes, input.HistoryDays, input.OverlapDays, input.LateGraceMinutes, input.EarlyLeaveGraceMinutes), User.GetUserId(), ct);
        client.Forget();
        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "integration.verifix.updated", "integration", VerifixSettingsStore.Code,
            new
            {
                baseUrl, input.Enabled, input.AutoSync, input.IntervalMinutes, input.HistoryDays, input.OverlapDays, input.LateGraceMinutes, input.EarlyLeaveGraceMinutes,
                input.FilialId, clientIdChanged = input.ClientId is not null, secretChanged = input.ClientSecret is not null,
            }, ct);
        return Ok(await DetailsAsync(ct));
    }

    /// <summary>Проверка подключения: токен и по странице каждого метода. Пустые поля — берутся сохранённые.</summary>
    [HttpPost("test")]
    public async Task<ActionResult<VerifixTestResult>> Test(TestInput input, CancellationToken ct)
    {
        var current = await settings.GetAsync(ct);
        var probe = current with
        {
            BaseUrl = string.IsNullOrWhiteSpace(input.BaseUrl) ? current.BaseUrl : VerifixSettingsStore.Normalize(input.BaseUrl),
            ClientId = string.IsNullOrWhiteSpace(input.ClientId) ? current.ClientId : input.ClientId.Trim(),
            ClientSecret = string.IsNullOrWhiteSpace(input.ClientSecret) ? current.ClientSecret : input.ClientSecret.Trim(),
            FilialId = input.FilialId is > 0 ? input.FilialId : current.FilialId,
        };
        var zone = (await rules.GetRulesAsync(ct)).Zone;
        var result = await client.TestAsync(probe, DateOnly.FromDateTime(VerifixFormats.ToLocal(time.GetUtcNow(), zone)), ct);
        await settings.SaveTestResultAsync(result.Ok, result.Message, ct);
        if (!result.Ok)
        {
            logger.LogWarning("Verifix: проверка подключения не прошла — {Message}", result.Message);
        }

        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "integration.verifix.tested", "integration", VerifixSettingsStore.Code,
            new { result.Ok, result.Message }, ct);
        return result;
    }

    /// <summary>Синхронизировать сейчас (в фоне). 409 — уже идёт.</summary>
    [HttpPost("sync")]
    public Task<IActionResult> Sync(CancellationToken ct) => StartAsync(HrSyncTrigger.Manual, null, ct);

    /// <summary>Журнал синхронизации, новые сверху.</summary>
    [HttpGet("runs")]
    public async Task<IActionResult> Runs([FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken ct = default)
    {
        var size = Math.Clamp(pageSize, 10, 100);
        var number = Math.Max(1, page);
        var total = await db.HrSyncRuns.CountAsync(ct);
        var runs = await db.HrSyncRuns.AsNoTracking().OrderByDescending(r => r.StartedAt).Skip((number - 1) * size).Take(size).ToListAsync(ct);
        var users = await UserNamesAsync(runs.Select(r => r.StartedById), ct);
        return Ok(new { Items = runs.Select(r => RunView(r, users)), Total = total, Page = number, PageSize = size });
    }

    [HttpGet("runs/{id:guid}")]
    public async Task<IActionResult> Run(Guid id, CancellationToken ct)
    {
        var run = await db.HrSyncRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        return run is null ? NotFound() : Ok(RunView(run, await UserNamesAsync([run.StartedById], ct)));
    }

    /// <summary>Повторить неудачную синхронизацию (загрузка идемпотентна: уже загруженное не задваивается).</summary>
    [HttpPost("runs/{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct)
    {
        var run = await db.HrSyncRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (run is null)
        {
            return NotFound();
        }

        if (run.Status is not (HrSyncStatus.Failed or HrSyncStatus.Partial))
        {
            return BadRequest(new { error = "Повторить можно только неудачную или частично выполненную синхронизацию." });
        }

        return await StartAsync(HrSyncTrigger.Retry, id, ct);
    }

    /// <summary>Привязать подразделение Verifix к отделу OneBase (локальное поле): по нему руководитель отдела видит своих сотрудников.</summary>
    [HttpPut("divisions/{id:long}/department")]
    public async Task<IActionResult> Department(long id, DepartmentInput input, CancellationToken ct)
    {
        var code = string.IsNullOrWhiteSpace(input.DepartmentCode) ? null : input.DepartmentCode.Trim();
        if (code is not null && !await db.Departments.AnyAsync(d => d.Code == code, ct))
        {
            return BadRequest(new { error = "Такого отдела OneBase нет." });
        }

        var changed = await db.HrReferences.Where(r => r.Kind == HrReferenceKinds.Division && r.VerifixId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.DepartmentCode, code), ct);
        if (changed == 0)
        {
            return NotFound(new { error = "Подразделение не найдено — сначала синхронизируйте справочники." });
        }

        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "integration.verifix.division_department", "hr_division", id.ToString(),
            new { departmentCode = code }, ct);
        return NoContent();
    }

    private async Task<IActionResult> StartAsync(string trigger, Guid? retryOf, CancellationToken ct)
    {
        var (result, runId) = await coordinator.StartInBackgroundAsync(trigger, User.GetUserId(), retryOf, ct);
        switch (result)
        {
            case HrSyncStartResult.NotConfigured:
                return BadRequest(new { error = "Сначала задайте client_id и client_secret и включите интеграцию." });
            case HrSyncStartResult.AlreadyRunning:
                return Conflict(new { error = "Синхронизация уже идёт — дождитесь окончания." });
        }

        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "integration.verifix.sync", "integration", VerifixSettingsStore.Code,
            new { trigger, retryOf, runId }, ct);
        return Accepted(new { runId });
    }

    /// <summary>not_configured | disabled | error | connected — для каталога интеграций.</summary>
    public static async Task<(string Status, DateTimeOffset? DataAsOf)> StatusAsync(IAppDbContext db, VerifixSettingsStore settings, CancellationToken ct)
    {
        var connection = await settings.GetAsync(ct);
        var last = await db.HrSyncRuns.AsNoTracking().Where(r => r.Status != HrSyncStatus.Running).OrderByDescending(r => r.StartedAt).FirstOrDefaultAsync(ct);
        var tracks = await db.HrSyncStates.AsNoTracking().Where(s => s.Entity == HrSyncService.TracksState).Select(s => s.CoveredThrough).FirstOrDefaultAsync(ct);
        var status = !connection.HasCredentials ? "not_configured"
            : !connection.Enabled ? "disabled"
            : last?.Status is HrSyncStatus.Failed or HrSyncStatus.Partial ? "error"
            : "connected";
        return (status, tracks);
    }

    private async Task<object> DetailsAsync(CancellationToken ct)
    {
        var connection = await settings.GetAsync(ct);
        var row = await settings.GetRowAsync(ct);
        var (status, dataAsOf) = await StatusAsync(db, settings, ct);
        var states = await db.HrSyncStates.AsNoTracking().OrderBy(s => s.Entity).ToListAsync(ct);
        var departments = await db.Departments.AsNoTracking().OrderBy(d => d.Name).Select(d => new { d.Code, d.Name }).ToListAsync(ct);
        var ctx = new HrContext(User.GetUserId(), true, true, null, []);
        var employees = await db.HrEmployees.AsNoTracking().GroupBy(e => e.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);
        var data = connection.Data;
        return new
        {
            Code = VerifixSettingsStore.Code,
            Name,
            Description,
            DepartmentCode = VerifixSettingsStore.Department,
            DepartmentName = departments.FirstOrDefault(d => d.Code == VerifixSettingsStore.Department)?.Name ?? "HR",
            Status = status,
            connection.BaseUrl,
            connection.ClientId,
            ClientIdFromEnvironment = string.IsNullOrWhiteSpace(data.ClientId) && !string.IsNullOrWhiteSpace(connection.ClientId),
            HasSecret = !string.IsNullOrEmpty(connection.ClientSecret),
            connection.SecretHint,
            SecretSource = connection.SecretSource.ToString(),
            connection.FilialId,
            connection.Enabled,
            data.AutoSync,
            data.IntervalMinutes,
            data.HistoryDays,
            data.OverlapDays,
            data.LateGraceMinutes,
            data.EarlyLeaveGraceMinutes,
            data.TimeZone,
            UpdatedAt = row?.UpdatedAt,
            LastTest = row?.LastTestAt is null ? null : new { At = row.LastTestAt, Ok = row.LastTestOk, Message = row.LastTestMessage },
            Sync = new
            {
                Running = coordinator.IsRunning || await db.HrSyncRuns.AnyAsync(r => r.Status == HrSyncStatus.Running, ct),
                DataAsOf = dataAsOf,
                Entities = states.Select(s => new { s.Entity, Title = HrSyncCoordinator.StepTitle(s.Entity), s.LastSuccessAt, s.LastRows, s.LastError, s.CoveredFrom, s.CoveredThrough }),
            },
            Employees = new
            {
                Working = employees.Where(e => e.Status == HrEmployeeStatus.Working).Sum(e => e.Count),
                Dismissed = employees.Where(e => e.Status == HrEmployeeStatus.Dismissed).Sum(e => e.Count),
                NotHired = employees.Where(e => e.Status == HrEmployeeStatus.NotHired).Sum(e => e.Count),
                Missing = await db.HrEmployees.CountAsync(e => e.MissingSince != null, ct),
            },
            Divisions = await hr.DivisionsAsync(ctx, ct),
            Departments = departments,
        };
    }

    private async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var list = ids.Where(i => i != null).Select(i => i!.Value).Distinct().ToList();
        return list.Count == 0 ? [] : await db.Users.AsNoTracking().Where(u => list.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName ?? u.Login, ct);
    }

    private static object RunView(HrSyncRun r, IReadOnlyDictionary<Guid, string> users)
    {
        List<HrSyncStep> steps;
        try
        {
            steps = JsonSerializer.Deserialize<List<HrSyncStep>>(r.StepsJson, Web) ?? [];
        }
        catch (JsonException)
        {
            steps = [];
        }

        return new
        {
            r.Id,
            r.Trigger,
            StartedBy = r.StartedById is { } u && users.TryGetValue(u, out var name) ? name : null,
            r.RetryOfId,
            r.StartedAt,
            r.FinishedAt,
            r.HeartbeatAt,
            r.Status,
            r.Created,
            r.Updated,
            r.Skipped,
            r.Errors,
            r.PeriodFrom,
            r.PeriodTo,
            r.Message,
            Steps = steps.Select(s => new { s.Step, Title = HrSyncCoordinator.StepTitle(s.Step), s.Status, s.Rows, s.Created, s.Updated, s.Skipped, s.Errors, s.Pages, s.Message }),
        };
    }
}
