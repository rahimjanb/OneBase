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
    /// Цена, действовавшая на момент cutoffTm (unix-секунды): последняя строка с Tm раньше этого момента. Строк до него нет
    /// (товар заведён позже) — последняя по времени. Так остаток оценивается по цене на начало текущего месяца, а не по
    /// повышению, которое ввели уже в этом месяце.
    /// </summary>
    public static decimal? PriceAsOf(IReadOnlyCollection<(decimal Price, decimal Tm)> items, decimal cutoffTm) =>
        LatestPrice(items.Where(i => i.Tm < cutoffTm)) ?? LatestPrice(items);

    /// <summary>Unix-секунды начала дня по местному времени компании — момент, с которого действуют цены месяца.</summary>
    public static decimal UnixSeconds(DateOnly day, TimeSpan offset) =>
        new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), offset).ToUnixTimeSeconds();

    /// <summary>
    /// Стоимость остатка — штуки × цена за единицу учёта. Остаток и цена в Linko хранятся в одной единице (кг у весового товара,
    /// штука или шоубокс у штучного), поэтому вес единицы здесь не участвует и ошибка «цена за кг вместо цены за штуку» невозможна.
    /// </summary>
    public static decimal? ValueSum(decimal pieces, decimal? price) => price is { } p ? pieces * p : null;

    /// <summary>
    /// Вес единицы учёта: по строкам заказов с 1-го числа последнего закрытого месяца (Σ веса ÷ Σ количества), без свежих продаж —
    /// по строкам за год, а если товар за год не продавался — из названия («(8-шт по 0,5-кг)» → 0,5 кг) с пометкой, что это оценка.
    /// Одного веса коробки («…, 3-кг») мало: сколько в ней штук, не видно.
    /// </summary>
    public static (decimal? UnitKg, string Source) UnitWeight(decimal? fromRecentOrders, decimal? fromYearOrders, PackInfo? pack) =>
        fromRecentOrders is { } recent ? (recent, WeightSources.Orders)
        : fromYearOrders is { } year ? (year, WeightSources.OrdersYear)
        : pack?.PieceKg is { } piece ? (piece, WeightSources.Name)
        : (null, WeightSources.None);

    public static (decimal? UnitKg, string Source) UnitWeight(decimal? fromOrders, PackInfo? pack) => UnitWeight(fromOrders, null, pack);
}
