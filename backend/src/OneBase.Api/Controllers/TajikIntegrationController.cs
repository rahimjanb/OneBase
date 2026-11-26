using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales.Tajik;
using OneBase.Application.Security;
using OneBase.Domain.Audit;
using OneBase.Infrastructure.Persistence;
using OneBase.Infrastructure.Tajik;

namespace OneBase.Api.Controllers;

/// <summary>
/// «Настройки → Интеграции → Продажи → 1С Таджикистан»: адрес API и ключ доступа (ключ наружу не отдаётся), включение, интервал, дата начала
/// истории, проверка, «Синхронизировать сейчас» и журнал запусков. 1С только читается.
/// </summary>
[ApiController]
[Route("api/integrations/tj1c")]
[HasPermission(Permissions.IntegrationsManage)]
public sealed class TajikIntegrationController(
    ITajikSettingsStore settings,
    TajikApiClient client,
    TajikSyncCoordinator coordinator,
    OneBaseDbContext db,
    IAuditLogger audit,
    TimeProvider time) : ControllerBase
{
    public const string Name = "1С Таджикистан";

    public const string Description = "Продажи дилера в Таджикистане (накладные: точки, ТП, товары, суммы в сомони) и остаток его склада — только чтение, раз в 30 минут. Показываются в «Продажи → Экспорт → Таджикистан».";

    public sealed record SaveInput(string? BaseUrl, string? Token, bool? Enabled, int? IntervalMinutes, DateOnly? HistoryFrom);

    public sealed record TestInput(string? BaseUrl, string? Token);

    public sealed record SyncInput(bool Full = false);

    public sealed record TestResult(bool Ok, string Message, int? SalesLines, int? StockRows);

    public sealed record DataStats(int Lines, int Documents, int Stores, DateOnly? From, DateOnly? Through, DateOnly? StockDate, int RemovedLines);

    [HttpGet]
    public async Task<IActionResult> Details(CancellationToken ct) => Ok(await DetailsAsync(ct));

    [HttpPut]
    public async Task<IActionResult> Save(SaveInput input, CancellationToken ct)
    {
        try
        {
            await settings.SaveAsync(new TajikSettingsChange(input.BaseUrl, input.Token, input.Enabled, input.IntervalMinutes, input.HistoryFrom), User.GetUserId(), ct);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { error = e.Message });
        }

        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "integration.tj1c.updated", "integration", TajikSettings.Code,
            new { input.BaseUrl, tokenChanged = input.Token is not null, input.Enabled, input.IntervalMinutes, input.HistoryFrom }, ct);
        return Ok(await DetailsAsync(ct));
    }

    /// <summary>Проверка: остаток склада и продажи за сегодня. Пустые адрес или ключ — берутся сохранённые.</summary>
    [HttpPost("test")]
    public async Task<TestResult> Test(TestInput input, CancellationToken ct)
    {
        var current = await settings.GetAsync(ct);
        var baseUrl = string.IsNullOrWhiteSpace(input.BaseUrl) ? current.BaseUrl : TajikSettingsStore.Normalize(input.BaseUrl);
        var token = string.IsNullOrWhiteSpace(input.Token) ? await settings.TokenAsync(ct) : input.Token.Trim();
        TestResult result;
        if (string.IsNullOrEmpty(baseUrl) || !TajikSettingsStore.IsValidUrl(baseUrl) || string.IsNullOrEmpty(token))
        {
            result = new TestResult(false, "Укажите адрес API (http:// или https://) и ключ доступа.", null, null);
        }
        else
        {
            try
            {
                var today = TajikText.Today(time.GetUtcNow());
                var stock = await client.StockAsync(baseUrl, token, ct);
                var sales = await client.SalesAsync(baseUrl, token, today, ct);
                var todayLines = sales.Items.Count(i => i.Date >= today);
                result = new TestResult(true,
                    $"Подключено: остаток — {stock.Items.Count} строк, продаж за {today:dd.MM.yyyy} — {todayLines} строк.", todayLines, stock.Items.Count);
            }
            catch (TajikApiException e)
            {
                result = new TestResult(false, e.Message, null, null);
            }
        }

        await settings.SaveTestResultAsync(result.Ok, result.Message, ct);
        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "integration.tj1c.tested", "integration", TajikSettings.Code,
            new { result.Ok, result.Message }, ct);
        return result;
    }

    /// <summary>Синхронизировать сейчас: окно с прошлого месяца, full=true — вся история с даты начала.</summary>
    [HttpPost("sync")]
    public async Task<IActionResult> Sync(SyncInput input, CancellationToken ct)
    {
        if (!(await settings.GetAsync(ct)).Configured)
        {
            return BadRequest(new { error = "Сначала задайте адрес API и ключ доступа." });
        }

        var run = await coordinator.RunAsync(input.Full ? TajikSyncService.Full : TajikSyncService.Manual, ct);
        if (run is null)
        {
            return Conflict(new { error = "Синхронизация уже идёт — дождитесь окончания." });
        }

        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "integration.tj1c.synced", "integration", TajikSettings.Code,
            new { run.Id, run.Status, input.Full }, ct);
        return Ok(await DetailsAsync(ct));
    }

    /// <summary>not_configured | disabled | error | connected — и время последней успешной загрузки.</summary>
    public static async Task<(string Status, DateTimeOffset? DataAsOf)> StatusAsync(ITajikSettingsStore settings, OneBaseDbContext db, CancellationToken ct)
    {
        var s = await settings.GetAsync(ct);
        var last = await db.TajikSyncRuns.AsNoTracking().Where(r => r.Status != "running").OrderByDescending(r => r.Id).FirstOrDefaultAsync(ct);
        var ok = await db.TajikSyncRuns.AsNoTracking().Where(r => r.Status == "ok" || r.Status == "partial").OrderByDescending(r => r.Id)
            .Select(r => r.FinishedAt).FirstOrDefaultAsync(ct);
        var status = !s.Configured ? "not_configured" : !s.Enabled ? "disabled" : last is { Status: "failed" or "partial" } ? "error" : "connected";
        return (status, ok);
    }

    private async Task<object> DetailsAsync(CancellationToken ct)
    {
        var s = await settings.GetAsync(ct);
        var (status, dataAsOf) = await StatusAsync(settings, db, ct);
        var runs = await db.TajikSyncRuns.AsNoTracking().OrderByDescending(r => r.Id).Take(20).ToListAsync(ct);
        var active = db.TajikSaleLines.AsNoTracking().Where(l => l.RemovedAt == null);
        var stats = new DataStats(
            await active.CountAsync(ct),
            await active.Select(l => new { l.DocNumber, l.DocDate }).Distinct().CountAsync(ct),
            await active.Select(l => l.Customer).Distinct().CountAsync(ct),
            await active.MinAsync(l => (DateOnly?)l.DocDate, ct),
            await active.MaxAsync(l => (DateOnly?)l.DocDate, ct),
            await db.TajikStockItems.AsNoTracking().MaxAsync(x => (DateOnly?)x.Date, ct),
            await db.TajikSaleLines.AsNoTracking().CountAsync(l => l.RemovedAt != null, ct));
        var department = await db.Departments.AsNoTracking().Where(d => d.Code == TajikSettings.Department).Select(d => d.Name).FirstOrDefaultAsync(ct);

        return new
        {
            Code = TajikSettings.Code,
            Name,
            Description,
            DepartmentCode = TajikSettings.Department,
            DepartmentName = department ?? "Продажи",
            Status = status,
            DataAsOf = dataAsOf,
            Settings = s,
            Running = coordinator.Running,
            Stats = stats,
            Runs = runs,
        };
    }
}
