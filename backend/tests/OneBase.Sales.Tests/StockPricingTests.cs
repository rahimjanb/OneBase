using OneBase.Application.Sales.Stock;

namespace OneBase.Sales.Tests;

public class StockPricingTests
{
    // История цены товара 002 в прайсе «Дилерга кириш нарх»: 22 000 (без времени), 22 800 (28.08 и 31.08.2026), 23 490 (01.10.2026 11:42 UTC).
    private static readonly (decimal Price, decimal Tm)[] History = [(22000m, 0m), (22800m, 1787895783m), (22800m, 1788194653m), (23490m, 1790854923m)];

    [Fact]
    public void Latest_price_wins_by_time() => Assert.Equal(23490m, StockMath.LatestPrice(History));

    [Fact]
    public void Same_time_takes_higher_price() =>
        Assert.Equal(23925m, StockMath.LatestPrice([(22500m, 0m), (23925m, 0m)]));

    [Fact]
    public void No_rows_no_price() => Assert.Null(StockMath.LatestPrice([]));

    [Fact]
    public void Stock_is_valued_at_the_price_valid_at_the_start_of_the_month()
    {
        // Начало октября по Ташкенту — 30.09 19:00 UTC: повышение 01.10 в остаток ещё не входит.
        var octoberStart = StockMath.UnixSeconds(new DateOnly(2026, 10, 1), TimeSpan.FromHours(5));

        Assert.Equal(1790794800m, octoberStart);
        Assert.Equal(22800m, StockMath.PriceAsOf(History, octoberStart));
        Assert.Equal(23490m, StockMath.PriceAsOf(History, StockMath.UnixSeconds(new DateOnly(2026, 11, 1), TimeSpan.FromHours(5))));
        Assert.Equal(22000m, StockMath.PriceAsOf(History, 1787895783m)); // до первого повышения — строка без времени
    }

    [Fact]
    public void Product_priced_only_after_the_month_start_takes_its_latest_price() =>
        Assert.Equal(45000m, StockMath.PriceAsOf([(44000m, 1790900000m), (45000m, 1790950000m)], 1790794800m));

    [Fact]
    public void Value_is_pieces_times_price_without_weight()
    {
        Assert.Equal(87000m, StockMath.ValueSum(10, 8700m));
        Assert.Null(StockMath.ValueSum(10, null));
    }

    [Fact]
    public void Unit_weight_from_recent_orders_comes_first_then_the_year()
    {
        var pack = StockMath.ParsePack("484 Конфеты помадные, OFARIN, (8-шт по 0,420-кг) 3,36 - кг");

        Assert.Equal((0.42m, WeightSources.Orders), StockMath.UnitWeight(0.42m, 0.45m, pack));
        Assert.Equal((0.45m, WeightSources.OrdersYear), StockMath.UnitWeight(null, 0.45m, pack)); // свежих продаж нет — за год
        Assert.Equal((0.42m, WeightSources.Name), StockMath.UnitWeight(null, null, pack)); // продаж за год нет — из названия (≈)
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
