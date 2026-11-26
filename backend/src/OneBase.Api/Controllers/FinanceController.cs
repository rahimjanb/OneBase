using Microsoft.AspNetCore.Mvc;
using OneBase.Api.Auth;
using OneBase.Application.Finance;
using OneBase.Application.Security;

namespace OneBase.Api.Controllers;

/// <summary>
/// Раздел «Финансы» по данным 1С:Бухгалтерии — только чтение (право finance.read). Цифры — факт 1С, без пересчёта; в каждом ответе — источник,
/// период и время синхронизации. Организация: id организации 1С или пусто — все организации сводно. Период: from–to (по Ташкенту), по умолчанию —
/// текущий месяц. Отборы во всех отчётах (необязательно): counterparty, item, warehouse, department — id 1С.
/// </summary>
[ApiController]
[Route("api/finance")]
[HasPermission(Permissions.FinanceRead)]
public sealed class FinanceController(FinanceReportService reports, TimeProvider clock) : ControllerBase
{
    public const int MaxDays = 3 * 366;

    private FinanceFilter? Filter(string? organization, DateOnly? from, DateOnly? to, out IActionResult? error)
    {
        var today = OneCFormat.LocalDate(clock.GetUtcNow());
        var end = to ?? today;
        var start = from ?? new DateOnly(end.Year, end.Month, 1);
        error = null;
        if (start > end)
        {
            error = BadRequest(new { error = "Начало периода позже конца." });
            return null;
        }

        if (end.DayNumber - start.DayNumber > MaxDays)
        {
            error = BadRequest(new { error = "Период — не больше трёх лет." });
            return null;
        }

        string? Query(string key) => Request.Query[key].ToString() is { Length: > 0 and <= 100 } value ? value.Trim() : null;
        return new FinanceFilter(string.IsNullOrWhiteSpace(organization) ? null : organization.Trim(), start, end)
        {
            CounterpartyId = Query("counterparty"), ItemId = Query("item"), WarehouseId = Query("warehouse"), DepartmentId = Query("department"),
        };
    }

    /// <summary>Значения для отборов: kind — counterparty, item, warehouse, department; q — часть названия, код или ИНН.</summary>
    [HttpGet("dimensions")]
    public async Task<IActionResult> Dimensions(string kind, string? q, int limit = 20, CancellationToken ct = default) =>
        Ok(await reports.DimensionOptionsAsync(kind, q, limit, ct));

    private async Task<IActionResult> Run<T>(string? organization, DateOnly? from, DateOnly? to, Func<FinanceFilter, Task<T>> query)
    {
        var filter = Filter(organization, from, to, out var error);
        return filter is null ? error! : Ok(await query(filter));
    }

    [HttpGet("organizations")]
    public async Task<IActionResult> Organizations(CancellationToken ct) => Ok(await reports.OrganizationsAsync(ct));

    [HttpGet("overview")]
    public Task<IActionResult> Overview(string? organization, DateOnly? from, DateOnly? to, CancellationToken ct) =>
        Run(organization, from, to, f => reports.OverviewAsync(f, ct));

    [HttpGet("pnl")]
    public Task<IActionResult> Pnl(string? organization, DateOnly? from, DateOnly? to, CancellationToken ct) =>
        Run(organization, from, to, f => reports.PnlAsync(f, ct));

    [HttpGet("cash")]
    public Task<IActionResult> Cash(string? organization, DateOnly? from, DateOnly? to, CancellationToken ct) =>
        Run(organization, from, to, f => reports.CashAsync(f, ct));

    /// <summary>kind: receivables (дебиторка) или payables (кредиторка).</summary>
    [HttpGet("settlements")]
    public Task<IActionResult> Settlements(string? organization, DateOnly? from, DateOnly? to, string? kind, int top = 100, CancellationToken ct = default) =>
        Run(organization, from, to, f => reports.SettlementsAsync(f, kind == "payables" ? "payables" : "receivables", top, ct));

    [HttpGet("accounts")]
    public Task<IActionResult> Accounts(string? organization, DateOnly? from, DateOnly? to, CancellationToken ct) =>
        Run(organization, from, to, f => reports.TrialBalanceAsync(f, ct));

    [HttpGet("accounts/{code}")]
    public async Task<IActionResult> Account(string code, string? organization, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var filter = Filter(organization, from, to, out var error);
        if (filter is null)
        {
            return error!;
        }

        return await reports.AccountAsync(filter, code, ct) is { } account ? Ok(account) : NotFound(new { error = $"Счёта {code} нет в плане счетов 1С." });
    }

    [HttpGet("entries")]
    public Task<IActionResult> Entries(string? organization, DateOnly? from, DateOnly? to, string? account, string? counterparty, string? registrar,
        int page = 1, int pageSize = 50, CancellationToken ct = default) =>
        Run(organization, from, to, f => reports.EntriesAsync(f, account, counterparty, registrar, page, pageSize, ct));

    [HttpGet("documents/{id:guid}")]
    public async Task<IActionResult> Document(Guid id, CancellationToken ct) =>
        await reports.DocumentAsync(id, ct) is { } doc ? Ok(doc) : NotFound();

    [HttpGet("operations")]
    public Task<IActionResult> Operations(string? organization, DateOnly? from, DateOnly? to, CancellationToken ct) =>
        Run(organization, from, to, f => reports.OperationsAsync(f, ct));

    [HttpGet("anomalies")]
    public Task<IActionResult> Anomalies(string? organization, DateOnly? from, DateOnly? to, CancellationToken ct) =>
        Run(organization, from, to, f => reports.AnomaliesAsync(f, ct));
}
