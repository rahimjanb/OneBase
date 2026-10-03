using OneBase.Application.Sales.Stock;

namespace OneBase.Sales.Tests;

public class StockPricingTests
{
    [Fact]
    public void Latest_price_wins_by_time() =>
        Assert.Equal(23490m, StockMath.LatestPrice([(22800m, 1788194653m), (23490m, 1790854923m)]));

    [Fact]
    public void Same_time_takes_higher_price() =>
        Assert.Equal(23925m, StockMath.LatestPrice([(22500m, 0m), (23925m, 0m)]));

    [Fact]
    public void No_rows_no_price() => Assert.Null(StockMath.LatestPrice([]));

    [Fact]
    public void Value_is_pieces_times_price_without_weight()
    {
        Assert.Equal(87000m, StockMath.ValueSum(10, 8700m));
        Assert.Null(StockMath.ValueSum(10, null));
    }

    [Fact]
    public void Unit_weight_from_orders_comes_first()
    {
        var (kg, source) = StockMath.UnitWeight(0.42m, StockMath.ParsePack("484 Конфеты помадные, OFARIN, (8-шт по 0,420-кг) 3,36 - кг"));

        Assert.Equal(0.42m, kg);
        Assert.Equal(WeightSources.Orders, source);
    }

    [Fact]
    public void Unit_weight_falls_back_to_name_when_no_sales()
    {
        var (kg, source) = StockMath.UnitWeight(null, StockMath.ParsePack("101 Конфеты помадные, ZIMA, (8-шт по 0,5-кг) 4-кг"));

        Assert.Equal(0.5m, kg);
        Assert.Equal(WeightSources.Name, source);
    }

    [Fact]
    public void Box_weight_alone_does_not_give_unit_weight()
    {
        // «3-кг» — вес коробки; сколько в ней штук, из названия не видно.
        var (kg, source) = StockMath.UnitWeight(null, StockMath.ParsePack("001 Кукурузные подушечки, ROHAT XRUSTIK, 3-кг"));

        Assert.Null(kg);
        Assert.Equal(WeightSources.None, source);
    }
}
