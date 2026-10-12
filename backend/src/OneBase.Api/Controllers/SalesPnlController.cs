using Microsoft.AspNetCore.Mvc;
using OneBase.Api.Auth;
using OneBase.Application.Sales.Pnl;
using OneBase.Application.Security;

namespace OneBase.Api.Controllers;

/// <summary>
/// «Продажи → P&L дилеров»: прибыль завода на дилере по месяцам. Финансовые данные — нужны права «Продажи» и «Финансы».
/// Данные загружаются только через AI Data Manager (sales.PnlValues); здесь — только чтение и расчёт по формулам OneBase.
/// </summary>
[ApiController]
[Route("api/sales/pnl")]
[HasPermission(Permissions.SalesRead)]
[HasPermission(Permissions.FinanceRead)]
public sealed class SalesPnlController(PnlService pnl) : ControllerBase
{
    /// <summary>P&L месяца по дилерам и группам РМ; без year/month — последний месяц с продажами и расходами.</summary>
    [HttpGet]
    public Task<PnlView> Get([FromQuery] int? year, [FromQuery] int? month, CancellationToken ct) => pnl.GetAsync(year, month, ct);

    /// <summary>Карточка дилера: месяцы года, итог и темп роста month к предыдущему месяцу.</summary>
    [HttpGet("dealer")]
    public async Task<ActionResult<PnlDealerCard>> Dealer([FromQuery] string key, [FromQuery] int year, [FromQuery] int? month, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 200 || year is < 2020 or > 2100)
        {
            return BadRequest();
        }

        return await pnl.DealerAsync(key, year, month, ct) is { } card ? card : NotFound();
    }
}
