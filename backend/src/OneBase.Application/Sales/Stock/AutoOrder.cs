namespace OneBase.Application.Sales.Stock;

/// <summary>
/// Товар автозаказа региона: единица Linko («кг» или «шт»), сколько единиц в коробке, вес коробки, цена за единицу (прайс
/// Sales:StockPriceList — «Дилерга кириш нарх», по нему Linko печатает передачи) и рекомендация в коробках — рекомендуемый заказ
/// «Рек. остатка» (до запаса на 15 дней, вверх до коробки; ТОП-товару без остатка и продаж — 10 коробок).
/// </summary>
public sealed record AutoOrderItem(
    long ProductId,
    string? Code,
    string Name,
    string Category,
    bool Top,
    string Unit,
    decimal UnitsPerBox,
    decimal? BoxKg,
    decimal? Price,
    int RecBoxes,
    decimal StockKg,
    decimal? KgPerDay);

/// <summary>Автозаказ региона: склады дилеров для выбора, товары с известной коробкой; WithoutBox — сколько товаров без коробки (в автозаказ не попали).</summary>
public sealed record AutoOrderView(
    string RegionId,
    string RegionName,
    IReadOnlyList<StockRegion> Regions,
    DateOnly SnapshotDate,
    string? PriceList,
    int CoverDays,
    IReadOnlyList<AutoOrderItem> Items,
    int WithoutBox);

public sealed record AutoOrderActLine(long ProductId, int Boxes);

/// <summary>Акт из автозаказа: регион, дата акта и коробки по товарам (с клиента — только коробки; названия, единицы и цены — с сервера).</summary>
public sealed record AutoOrderActInput(string? RegionId, DateOnly? Date, IReadOnlyList<AutoOrderActLine>? Lines);

public sealed record TransferActRow(string Name, int Boxes, string Unit, decimal Quantity, decimal? Price, decimal? Sum);

/// <summary>Акт приёма-передачи (бланк Linko): откуда → куда, дата, кто передаёт, строки и итог.</summary>
public sealed record TransferAct(string From, string To, DateOnly Date, string Sender, IReadOnlyList<TransferActRow> Rows, decimal Total);

/// <summary>Документ Word «Акт приёма-передачи» по бланку Linko.</summary>
public interface ITransferActWriter
{
    byte[] Write(TransferAct act);
}

public static class AutoOrderMath
{
    public const string Kg = "кг";
    public const string Pieces = "шт";

    /// <summary>
    /// Единиц учёта Linko в коробке: у весового («кг») — вес коробки; у штучного — штук в коробке из названия («35г / 28шт»), иначе
    /// вес коробки ÷ вес штуки. null — коробка неизвестна, товар в автозаказ не попадает.
    /// </summary>
    public static decimal? UnitsPerBox(string? unit, PackInfo? pack, decimal? unitKg, decimal? boxKg)
    {
        if (string.Equals(unit, Kg, StringComparison.OrdinalIgnoreCase))
        {
            return boxKg is > 0 ? boxKg : null;
        }

        if (pack?.PiecesPerBox is { } pieces && pieces > 0)
        {
            return pieces;
        }

        if (boxKg is > 0 && unitKg is > 0)
        {
            var n = Math.Round(boxKg.Value / unitKg.Value);
            return n > 0 ? n : null;
        }

        return null;
    }

    /// <summary>Единица Linko для акта: «кг» или «шт» (без единицы в справочнике — по весу штуки: 1 кг — «кг»).</summary>
    public static string UnitOf(string? measurement, decimal? unitKg)
    {
        var unit = measurement?.Trim().ToLowerInvariant();
        return unit is Kg or Pieces ? unit : unitKg is { } u && StockMath.IsByWeight(u) ? Kg : Pieces;
    }

    /// <summary>Строка акта: количество = коробки × единиц в коробке (до граммов), сумма = количество × цена за единицу, целыми сумами.</summary>
    public static TransferActRow Row(AutoOrderItem item, int boxes)
    {
        var quantity = Math.Round(boxes * item.UnitsPerBox, 3);
        return new TransferActRow(item.Name, boxes, item.Unit, quantity, item.Price, item.Price is { } p ? Math.Round(quantity * p) : null);
    }
}
