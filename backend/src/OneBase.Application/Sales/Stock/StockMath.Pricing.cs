namespace OneBase.Application.Sales.Stock;

public static partial class StockMath
{
    /// <summary>
    /// Актуальная цена из прайса Linko: у товара в прайсе лежит история строк с отметкой времени (Tm) — берётся последняя,
    /// при одинаковом времени — большая. Пустой список — цены нет.
    /// </summary>
    public static decimal? LatestPrice(IEnumerable<(decimal Price, decimal Tm)> items)
    {
        (decimal Price, decimal Tm)? best = null;
        foreach (var item in items)
        {
            if (best is null || item.Tm > best.Value.Tm || (item.Tm == best.Value.Tm && item.Price > best.Value.Price))
            {
                best = item;
            }
        }

        return best?.Price;
    }

    /// <summary>
    /// Стоимость остатка — штуки × цена за единицу учёта. Остаток и цена в Linko хранятся в одной единице (кг у весового товара,
    /// штука или шоубокс у штучного), поэтому вес единицы здесь не участвует и ошибка «цена за кг вместо цены за штуку» невозможна.
    /// </summary>
    public static decimal? ValueSum(decimal pieces, decimal? price) => price is { } p ? pieces * p : null;

    /// <summary>
    /// Вес единицы учёта: по строкам заказов (Σ веса ÷ Σ количества), а если товар за год не продавался — из названия
    /// («(8-шт по 0,5-кг)» → 0,5 кг) с пометкой, что это оценка. Одного веса коробки («…, 3-кг») мало: сколько в ней штук, не видно.
    /// </summary>
    public static (decimal? UnitKg, string Source) UnitWeight(decimal? fromOrders, PackInfo? pack) =>
        fromOrders is { } orders ? (orders, WeightSources.Orders)
        : pack?.PieceKg is { } piece ? (piece, WeightSources.Name)
        : (null, WeightSources.None);
}
