using OneBase.Application.Sales.Stock;

namespace OneBase.Sales.Tests;

public class OutstockMathTests
{
    [Fact]
    public void Morning_stock_is_rebuilt_backwards_from_snapshot()
    {
        // Вечером 3-го дня (снимок) — 10 кг. 3-й день: продали 4, привезли 0 → утром 14. 2-й: продали 6, привезли 20 → утром 0.
        // 1-й: продали 5, привезли 0 → утром 5.
        var (morning, negative) = OutstockMath.Reconstruct(10, [5, 6, 4], [0, 20, 0]);

        Assert.Equal([5m, 0m, 14m], morning);
        Assert.All(negative, n => Assert.False(n));
    }

    [Fact]
    public void Negative_stock_becomes_zero_and_is_flagged()
    {
        // Привезли больше, чем было с продажами: даты разошлись — утро = 0, день помечен.
        var (morning, negative) = OutstockMath.Reconstruct(1, [2, 1], [0, 30]);

        Assert.Equal([2m, 0m], morning);
        Assert.Equal([false, true], negative);
    }

    [Fact]
    public void Half_kilogram_is_not_in_stock()
    {
        Assert.False(OutstockMath.InStock(0.5m));
        Assert.True(OutstockMath.InStock(0.51m));
    }

    [Fact]
    public void Lost_kg_is_zero_days_times_average_daily_sales()
    {
        // Термез, KEKO декор: 10 485 кг за 30 дней, 4 дня в нуле → 1 398 кг.
        Assert.Equal(1398m, Math.Round(OutstockMath.LostKg(4, 10485, 30)));
        Assert.Equal(0m, OutstockMath.LostKg(0, 10485, 30));
    }

    [Fact]
    public void Core_is_the_top_pairs_that_make_eighty_percent()
    {
        // 50 + 30 = 80 из 100 → две пары; остальное — мелочь.
        Assert.Equal(2, OutstockMath.CoreCount([50m, 30m, 10m, 5m, 5m]));
        Assert.Equal(0, OutstockMath.CoreCount([0m, 0m]));
        Assert.Equal(1, OutstockMath.CoreCount([100m]));
    }

    [Fact]
    public void Chronic_is_half_of_the_period_or_more()
    {
        Assert.True(OutstockMath.IsChronic(15, 30));
        Assert.False(OutstockMath.IsChronic(14, 30));
        Assert.False(OutstockMath.IsChronic(0, 0));
    }
}
