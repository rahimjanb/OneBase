using OneBase.Application.Sales;
using OneBase.Application.Sales.Primary;
using OneBase.Application.Sales.SkuSales;
using OneBase.Application.Sales.Stock;

namespace OneBase.AI.Tools.Data;

/// <summary>
/// Прогрев общих частей тяжёлых инструментов — тем же запросом, что делает инструмент без аргументов: снимок остатков и скорость продаж
/// (get_stock_reload), первичка текущего месяца (get_primary_shipments), агрегат окна и каталог «Продаж по SKU» за период по умолчанию
/// (get_sku_sales). Холодные они считаются 1,3–4,8 с; когда общие части в кэше, запрос с любым фильтром — около 0,15 с.
/// </summary>
internal sealed class SalesToolsWarmStep(StockService stock, PrimaryService primary, SkuSalesService sku, SalesDataLoader loader) : ISalesWarmStep
{
    public string Name => "инструменты продаж";

    public async Task WarmAsync(CancellationToken ct)
    {
        await stock.QueryAsync(new StockQuery(), ct);
        await primary.GetAsync(null, null, ct);
        if (await loader.LastDataAsync(ct) is { } last)
        {
            var (from, to) = GetSkuSalesTool.PeriodOf(null, null, null, last);
            await sku.GetAsync(MonthRange.Key(from), MonthRange.Key(to), null, null, null, null, ct);
        }
    }
}
