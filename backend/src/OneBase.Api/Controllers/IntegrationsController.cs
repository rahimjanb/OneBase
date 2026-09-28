using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Security;
using OneBase.Domain.Audit;
using OneBase.Infrastructure.Linko;

namespace OneBase.Api.Controllers;

/// <summary>«Настройки → Интеграции»: интеграции по отделам и подключение к Linko. Токены наружу не отдаются.</summary>
[ApiController]
[Route("api/integrations")]
[HasPermission(Permissions.IntegrationsManage)]
public sealed class IntegrationsController(
    IAppDbContext db,
    LinkoSettingsStore linko,
    LinkoClient client,
    LinkoSyncCoordinator coordinator,
    LinkoSyncProgress progress,
    LinkoVerifier verifier,
    IAuditLogger audit) : ControllerBase
{
    private sealed record CatalogItem(string Code, string Department, string Name, string Description);

    /// <summary>Какие интеграции доступны в каком отделе.</summary>
    private static readonly CatalogItem[] Catalog =
    [
        new("linko", "sales", "Linko SFA",
            "Заказы, возвраты, визиты, торговые точки и торговые представители — источник данных раздела «Продажи»."),
    ];

    public sealed record IntegrationSummary(string Code, string Name, string Description, string Status, DateTimeOffset? DataAsOf);

    public sealed record LinkoInput(string? BaseUrl, string? Token, bool Enabled);

    public sealed record LinkoTestInput(string? BaseUrl, string? Token);

    [HttpGet]
    public async Task<IActionResult> Departments(CancellationToken ct)
    {
        var departments = await db.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync(ct);
        var result = new List<object>();
        foreach (var d in departments)
        {
            result.Add(new { d.Code, d.Name, Integrations = await SummariesAsync(d.Code, ct) });
        }

        return Ok(result);
    }

    [HttpGet("departments/{code}")]
    public async Task<IActionResult> Department(string code, CancellationToken ct)
    {
        var department = await db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Code == code, ct);
        return department is null
            ? NotFound()
            : Ok(new { department.Code, department.Name, Integrations = await SummariesAsync(code, ct) });
    }

    [HttpGet("linko")]
    public async Task<IActionResult> Linko(CancellationToken ct) => Ok(await LinkoDetailsAsync(ct));

    /// <summary>Сохранить адрес, токен и включение. Token: null — не менять, "" — удалить сохранённый в OneBase.</summary>
    [HttpPut("linko")]
    public async Task<IActionResult> SaveLinko(LinkoInput input, CancellationToken ct)
    {
        var baseUrl = input.BaseUrl?.Trim() ?? string.Empty;
        if (baseUrl.Length > 0 && !LinkoSettingsStore.IsValidUrl(baseUrl))
        {
            return BadRequest(new { error = "Адрес сервера должен начинаться с https:// — например https://имя.linko.uz" });
        }

        await linko.SaveAsync(baseUrl, input.Token, input.Enabled, User.GetUserId(), ct);
        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "integration.linko.updated", "integration", LinkoSettingsStore.Code,
            new { baseUrl, input.Enabled, tokenChanged = input.Token is not null }, ct);
        return Ok(await LinkoDetailsAsync(ct));
    }

    /// <summary>
    /// Проверка работоспособности. Можно проверить введённые, ещё не сохранённые значения:
    /// пустой адрес или токен — берётся сохранённый.
    /// </summary>
    [HttpPost("linko/test")]
    public async Task<ActionResult<LinkoTestResult>> TestLinko(LinkoTestInput input, CancellationToken ct)
    {
        var current = await linko.GetAsync(ct);
        var baseUrl = string.IsNullOrWhiteSpace(input.BaseUrl) ? current.BaseUrl : input.BaseUrl;
        var token = string.IsNullOrWhiteSpace(input.Token) ? current.Token : input.Token;

        var result = await client.TestAsync(baseUrl, token, ct);
        await linko.SaveTestResultAsync(result.Ok, result.Message, ct);
        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "integration.linko.tested", "integration", LinkoSettingsStore.Code,
            new { result.Ok, result.Message }, ct);
        return result;
    }

    /// <summary>
    /// Удалить все данные, загруженные из Linko (и связанные с ними регионы, агентов и планы OneBase),
    /// и загрузить заново с текущего сервера. Направления, цели и настройки подключения сохраняются.
    /// </summary>
    [HttpPost("linko/reset")]
    public async Task<IActionResult> ResetLinko(CancellationToken ct)
    {
        if (!(await linko.GetAsync(ct)).IsReady)
        {
            return BadRequest(new { error = "Сначала задайте адрес и токен и включите интеграцию." });
        }

        if (!coordinator.TryStartInBackground(LinkoSyncMode.Reset))
        {
            return Conflict(new { error = "Синхронизация уже идёт — дождитесь окончания." });
        }

        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "integration.linko.reset", "integration", LinkoSettingsStore.Code,
            new { (await linko.GetAsync(ct)).BaseUrl }, ct);
        return Accepted();
    }

    /// <summary>Сверка полноты загрузки: количество записей в Linko и в OneBase.</summary>
    [HttpPost("linko/verify")]
    public async Task<ActionResult<LinkoVerifyResult>> VerifyLinko(CancellationToken ct)
    {
        if (!(await linko.GetAsync(ct)).HasCredentials)
        {
            return BadRequest(new { error = "Сначала задайте адрес и токен." });
        }

        try
        {
            return await verifier.VerifyAsync(ct);
        }
        catch (LinkoApiException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private async Task<List<IntegrationSummary>> SummariesAsync(string department, CancellationToken ct)
    {
        var list = new List<IntegrationSummary>();
        foreach (var item in Catalog.Where(c => c.Department == department))
        {
            var (status, dataAsOf) = item.Code == LinkoSettingsStore.Code ? await LinkoStatusAsync(ct) : ("not_configured", null);
            list.Add(new IntegrationSummary(item.Code, item.Name, item.Description, status, dataAsOf));
        }

        return list;
    }

    /// <summary>not_configured | disabled | error | connected — и дата последних данных.</summary>
    private async Task<(string Status, DateTimeOffset? DataAsOf)> LinkoStatusAsync(CancellationToken ct)
    {
        var settings = await linko.GetAsync(ct);
        var states = await db.LinkoSyncStates.AsNoTracking().ToListAsync(ct);
        var documents = states.Where(s => s.Entity is "orders" or "order_returns" or "visits").ToList();
        DateTimeOffset? dataAsOf = documents.Count == 3 && documents.All(s => s.LastSuccessAt != null) ? documents.Min(s => s.LastSuccessAt) : null;

        var status = !settings.HasCredentials ? "not_configured"
            : !settings.Enabled ? "disabled"
            : states.Any(s => s.LastError != null) ? "error"
            : "connected";
        return (status, dataAsOf);
    }

    private async Task<object> LinkoDetailsAsync(CancellationToken ct)
    {
        var settings = await linko.GetAsync(ct);
        var row = await linko.GetRowAsync(ct);
        var (status, dataAsOf) = await LinkoStatusAsync(ct);
        var catalog = Catalog.Single(c => c.Code == LinkoSettingsStore.Code);
        var department = await db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Code == catalog.Department, ct);
        var states = await db.LinkoSyncStates.AsNoTracking().OrderBy(s => s.Entity).ToListAsync(ct);

        return new
        {
            catalog.Code,
            catalog.Name,
            catalog.Description,
            DepartmentCode = catalog.Department,
            DepartmentName = department?.Name ?? "Продажи",
            Status = status,
            settings.BaseUrl,
            BaseUrlFromEnvironment = string.IsNullOrWhiteSpace(row?.BaseUrl) && !string.IsNullOrWhiteSpace(settings.BaseUrl),
            settings.Enabled,
            HasToken = !string.IsNullOrEmpty(settings.Token),
            settings.TokenHint,
            TokenSource = settings.TokenSource.ToString(),
            UpdatedAt = row?.UpdatedAt,
            LastTest = row?.LastTestAt is null ? null : new { At = row.LastTestAt, Ok = row.LastTestOk, Message = row.LastTestMessage },
            Sync = new
            {
                coordinator.IsRunning,
                Progress = progress.Current,
                DataAsOf = dataAsOf,
                Entities = states.Select(s => new { s.Entity, s.LastSuccessAt, s.LastRows, s.LastError }),
            },
        };
    }
}
