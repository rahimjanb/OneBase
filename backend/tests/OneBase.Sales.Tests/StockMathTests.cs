using System.Text.Json;
using OneBase.Application.Sales.Stock;
using OneBase.Infrastructure.Linko;

namespace OneBase.Sales.Tests;

public class StockMathTests
{
    [Fact]
    public void Balance_string_of_zeros_is_zero_not_truthy()
    {
        // Linko отдаёт balance строкой: непустая строка «истинна», но это ноль.
        var dto = JsonSerializer.Deserialize<LinkoProductBalanceDto>(
            """{"product":{"id":4021},"stock":{"id":8},"balance":"0.000000000","tm":"1741091650.749378"}""", LinkoClient.Json)!;

        Assert.Equal(0m, dto.Balance);
        Assert.Equal(0m, StockMath.ParseQuantity("0.000000000"));
        Assert.Equal(13m, StockMath.ParseQuantity("13.000000000"));
        Assert.Null(StockMath.ParseQuantity("abc"));
    }

    [Fact]
    public void Pieces_convert_to_kg_by_piece_weight_and_then_to_boxes_by_box_weight()
    {
        // Помадка: 0,5 кг штука, 8 штук в коробке 4 кг. Остаток 80 штук = 40 кг = 10 коробок.
        var unitKg = StockMath.UnitKg(totalWeight: 250m, amount: 500m); // продали 500 штук на 250 кг
        var kg = StockMath.Kg(80, unitKg);

        Assert.Equal(0.5m, unitKg);
        Assert.Equal(40m, kg);
        Assert.Equal(10m, StockMath.Boxes(kg, 4m));
        Assert.NotEqual(80m * 4m, kg); // неверно: штуки × вес коробки завысили бы остаток в 8 раз
    }

    [Theory]
    [InlineData("038 Шоколад LILYA бежовый, молоко (4-шт по 0,5-кг) 2-кг", 2.0, 4, 0.5)]
    [InlineData("006 Хрустящие подушечки «Hrustik», (24шт по 40-гр ) 0,96-кг", 0.96, 24, 0.04)]
    [InlineData("254 Печенье «Frully», (10-шт по 300гр), 3-кг", 3.0, 10, 0.3)]
    [InlineData("342 Печенье «Frully», (10-шт по 180гр)", 1.8, 10, 0.18)]
    [InlineData("001 Кукурузные подушечки ROHAT XRUSTIK, 3-кг", 3.0, null, null)]
    [InlineData("059 Печенье с чёрной глазурью, SVETOCHKA, 2,5-кг", 2.5, null, null)]
    [InlineData("004 Глазированные конфеты «Kartofelki» , 3 кг", 3.0, null, null)]
    public void Pack_is_parsed_from_the_product_name(string name, double boxKg, int? pieces, double? pieceKg)
    {
        var pack = StockMath.ParsePack(name)!;

        Assert.Equal((decimal)boxKg, pack.BoxKg);
        Assert.Equal(pieces, pack.PiecesPerBox);
        Assert.Equal(pieceKg is null ? null : (decimal)pieceKg.Value, pack.PieceKg);
    }

    [Fact]
    public void Name_without_weight_gives_no_pack() =>
        Assert.Null(StockMath.ParsePack("050 Печенье сахарное глазированное “Candy Gold”"));

    [Theory]
    [InlineData(4.0, 0.5, true)] // 8 штук
    [InlineData(3.0, 3.0, true)] // коробка — сама единица учёта
    [InlineData(0.96, 0.04, true)]
    [InlineData(3.0, 0.42, false)] // 7,14 штуки — название не про ту единицу
    [InlineData(2.0, 3.0, false)] // коробка легче штуки
    public void Box_from_name_is_accepted_only_if_it_holds_a_whole_number_of_units(double boxKg, double unitKg, bool ok) =>
        Assert.Equal(ok, StockMath.BoxConsistent((decimal)boxKg, (decimal)unitKg));
}
