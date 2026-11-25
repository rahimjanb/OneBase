using Microsoft.AspNetCore.Mvc;
using OneBase.Api.Auth;
using OneBase.Application.Sales.Access;
using OneBase.Application.Security;
using OneBase.Application.Telegram.Incentive;
using OneBase.Domain.Incentive;
using OneBase.Infrastructure.Telegram.Reports;

namespace OneBase.Api.Controllers;

/// <summary>
/// «Продажи → Поле боя» — веб-модуль «Целей на маршрут» v2 (ТЗ v2 от 24.11.2026): обзор, маршрут ТП с целями Могул и Twizos, итоги команды,
/// журнал кешбека (два баланса), квоты стоек, просмотр и выгрузка карточек PNG. Охват — как во всех «Продажах»: ТП — только он, СВР — свои регионы,
/// РМ — регионы направлений, без охвата — вся организация; ТП вне охвата — 404. Только чтение, кроме обновления журнала (вся организация).
/// </summary>
[ApiController]
[Route("api/sales/battle")]
[HasPermission(Permissions.SalesRead)]
public sealed class SalesBattleController(IncentiveWebService battle, SalesAccess access, SalesScopeAdmin scopes) : ControllerBase
{
    private async Task<SalesScope> ScopeAsync(CancellationToken ct) => await access.ScopeAsync(User.GetUserId(), ct);

    [SalesScoped]
    [HttpGet]
    public async Task<ActionResult<BattleOverview>> Overview([FromQuery] DateOnly? date, CancellationToken ct)
    {
        var scope = await ScopeAsync(ct);
        var title = (await scopes.GetAsync(User.GetUserId(), ct)).Title;
        var view = await battle.OverviewAsync(scope, title, date ?? battle.Today(), ct);
        return view is null ? NotFound(new { error = "Акции с целями на маршрут (v2) нет — включите раздел «Цели на маршрут v2» во вкладке «Акции»." }) : view;
    }

    [SalesScoped]
    [HttpGet("agents/{id:long}/route")]
    public async Task<ActionResult<BattleRouteView>> Route(long id, [FromQuery] DateOnly? date, CancellationToken ct) =>
        await battle.RouteAsync(await ScopeAsync(ct), id, date ?? battle.Today(), ct) is { } view ? view : NotFound();

    [SalesScoped]
    [HttpGet("team")]
    public async Task<IReadOnlyList<BattleTeamRow>> Team([FromQuery] string? region, [FromQuery] DateOnly? date, CancellationToken ct) =>
        await battle.TeamAsync(await ScopeAsync(ct), string.IsNullOrWhiteSpace(region) ? null : region.Trim(), date ?? battle.Today(), ct);

    [SalesScoped]
    [HttpGet("ledger")]
    public async Task<IReadOnlyList<BattleLedgerRow>> Ledger([FromQuery] long? agent, [FromQuery] string? kind, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        var end = to ?? battle.Today();
        return await battle.LedgerAsync(await ScopeAsync(ct), agent, kind is IncentiveCashbackKind.Mogul or IncentiveCashbackKind.Twizos ? kind : null, from ?? end.AddDays(-30),
            end, ct);
    }

    [SalesScoped]
    [HttpGet("stands")]
    public async Task<IReadOnlyList<IncentiveStandQuota>> Stands([FromQuery] DateOnly? date, CancellationToken ct) =>
        await battle.StandsAsync(await ScopeAsync(ct), date ?? battle.Today(), ct);

    /// <summary>Карточка PNG (1080 px): slot — morning, midday, evening, week, month, team, zone; agent — ТП для карточек ТП; region — для team и zone.</summary>
    [SalesScoped]
    [HttpGet("card")]
    public async Task<IActionResult> Card([FromQuery] string slot, [FromQuery] long? agent, [FromQuery] string? region, [FromQuery] DateOnly? date,
        [FromQuery] string? lang, CancellationToken ct)
    {
        string[] slots = [IncentiveSlot.Morning, IncentiveSlot.Midday, IncentiveSlot.Evening, IncentiveSlot.Week, IncentiveSlot.Month, IncentiveSlot.Team, IncentiveSlot.Zone];
        if (!slots.Contains(slot))
        {
            return BadRequest(new { error = "Карточка: morning, midday, evening, week, month, team или zone." });
        }

        var png = await battle.CardAsync(await ScopeAsync(ct), slot, agent, string.IsNullOrWhiteSpace(region) ? null : region.Trim(), date ?? battle.Today(),
            lang is "uz" or "uz-Cyrl" ? lang : "ru", ct);
        return png is null ? NotFound(new { error = "Карточки на этот день нет: день вне целей v2 или ТП вне вашего охвата." }) : File(png, "image/png", $"pole-boya-{slot}.png");
    }

    [SalesOrganizationOnly]
    [HttpPost("ledger/refresh")]
    public async Task<ActionResult<IncentiveLedgerRefresh>> Refresh(CancellationToken ct) =>
        await battle.RefreshAsync(ct) is { } result ? result : NotFound();
}
