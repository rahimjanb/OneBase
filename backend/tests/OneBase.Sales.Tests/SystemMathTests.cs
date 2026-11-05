using OneBase.Application.Sales.Metrics;

namespace OneBase.Sales.Tests;

public class SystemMathTests
{
    private static SaleLine Line(long market, long product, decimal kg, long type = 1) =>
        new(new DateOnly(2026, 10, 5), 7, market, 100, type, product, kg, kg * 10, 1);

    [Fact]
    public void Core_takes_skus_of_each_category_by_weight_until_they_give_80_percent_of_it()
    {
        // Категория 1: 50 + 30 = 80% — ядро из двух; категория 2: 70 < 80%, 70 + 20 = 90% — тоже двух; товар вне категорий — нет.
        var kg = new Dictionary<long, decimal> { [1] = 50, [2] = 30, [3] = 20, [4] = 70, [5] = 20, [6] = 10, [7] = 500, [8] = -5 };
        long? Category(long p) => p switch { <= 3 => -1, <= 6 => -2, _ => null };

        Assert.Equal([1, 2, 4, 5], SystemMath.Core(kg, Category).Order());
    }

    [Fact]
    public void Quality_counts_akb_skus_core_categories_one_sku_and_regular_top_outlets()
    {
        long? Category(long? product) => product is <= 3 ? -1 : -2;
        IReadOnlySet<long> core = new HashSet<long> { 1, 2, 3 };
        var lines = new List<SaleLine>
        {
            // Точка 10: три SKU ядра одной категории — качественная.
            Line(10, 1, 40), Line(10, 2, 30), Line(10, 3, 30),
            // Точка 11: один SKU (второй вернули целиком) — «с 1 SKU».
            Line(11, 4, 5), Line(11, 5, 3), Line(11, 5, -3),
            // Точка 12: два SKU двух категорий.
            Line(12, 1, 10), Line(12, 6, 10),
            // Точка 13: возврат больше продажи — не АКБ.
            Line(13, 1, 2), Line(13, 1, -4),
            Line(14, 6, 1), Line(15, 6, 1), Line(16, 6, 1),
        };

        var row = SystemMath.Quality("r", "Север", lines, core, [new HashSet<long> { 10, 11 }, new HashSet<long> { 10 }], Category, []);

        Assert.Equal(6, row.Akb); // 10, 11, 12, 14, 15, 16
        Assert.Equal(126m, row.Kg);
        Assert.Equal(1.5m, row.SkuAvg); // (3 + 1 + 2 + 1 + 1 + 1) / 6
        Assert.Equal((4, 1, 1), (row.OneSku, row.Quality, row.TwoCategories));
        // Верхние 20% от 6 точек — одна (точка 10, 100 кг из 128): она покупала в обоих прошлых месяцах.
        Assert.Equal((1, 1), (row.TopCount, row.TopRegular));
        Assert.Equal(100m / 128m, row.TopShare);
    }

    [Fact]
    public void Aging_puts_open_shipments_into_buckets_by_age_and_reserve_follows_the_scale()
    {
        var aging = SystemMath.Aging([(0, 100m), (30, 50m), (31, 1000m), (120, 10m), (361, 2000m)]);

        Assert.Equal([150m, 1000m, 10m, 0m, 0m, 2000m], aging);
        Assert.Equal(Math.Round(150 * 0.008m + 1000 * 0.02m + 10 * 0.05m + 2000 * 0.5m), SystemMath.Reserve(aging));
    }

    [Fact]
    public void Stock_on_the_first_is_restored_back_from_now_by_sales_and_shipments_and_overstock_needs_both_signals()
    {
        // Сейчас 600 кг; с 1-го продано 1 000 за 10 дней (100 в день), завод отгрузил 1 300: на 1-е было 300 кг.
        var (stock1, daysNow, days1, overstock) = SystemMath.Stock(600, 1300, 1000, 10);
        Assert.Equal((300m, 6m, 3m, true), (stock1, daysNow, days1, overstock));

        // Отгрузили не больше вторички на 15% — сигнала нет, хоть запас и вырос.
        Assert.False(SystemMath.Stock(600, 1100, 1000, 10).Overstock);

        // Вторички нет — дней запаса нет.
        Assert.Equal((null, null), (SystemMath.Stock(600, 100, 0, 10).DaysNow, SystemMath.Stock(null, 100, 50, 10).Stock1));
    }
}
