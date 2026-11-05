using OneBase.Application.Sales.Dmp;

namespace OneBase.Sales.Tests;

public class DmpTests
{
    private static readonly DateOnly AsOf = new(2026, 10, 31);
    private static DateOnly Oct(int day) => new(2026, 10, day);

    private static DmpView Build(
        IReadOnlyList<DmpShelfLine> shelves,
        IReadOnlyList<DmpSale>? sales = null,
        IReadOnlyList<DmpReceipt>? receipts = null,
        IReadOnlyList<DmpBalance>? balances = null) =>
        DmpCompute.Build(AsOf, ["Стеллаж"], "Придзел", receipts ?? [], balances ?? [], shelves, sales ?? [],
            new Dictionary<long, string> { [1] = "Магазин 1", [2] = "Магазин 2", [3] = "Магазин 3" },
            new Dictionary<long, string> { [7] = "ТП 7" });

    [Fact]
    public void Shop_orders_are_compared_in_equal_windows_before_and_after_installation_without_the_fill_order()
    {
        // Стеллаж привезли заказом 100 22.10: до даты данных 9 дней — окна 13–21.10 и 23–31.10.
        var view = Build(
            [new DmpShelfLine(100, DmpMove.Installed, Oct(22), 1, 7, "Север", 1)],
            [
                new DmpSale(1, 100, false, Oct(22), 16.3m, 9.6m), // наполнение
                new DmpSale(1, 90, false, Oct(12), 50, 0), // раньше окна «до»
                new DmpSale(1, 91, false, Oct(15), 4, 1),
                new DmpSale(1, 101, false, Oct(29), 5, 2),
                new DmpSale(1, 5, true, Oct(30), -1.5m, -0.5m), // возврат вычитается, заказом не считается
                new DmpSale(1, 102, false, Oct(5), 10, 0),
            ]);

        var shop = Assert.Single(view.ShopRows);
        Assert.Equal(("Магазин 1", "Север", "ТП 7", Oct(22), 1m, 9), (shop.Market, shop.Region, shop.Agent, shop.Date, shop.Qty, shop.Days));
        Assert.Equal(new DmpOrders(1, 16.3m, 9.6m), shop.Fill);
        Assert.Equal(new DmpOrders(1, 4, 1), shop.Before);
        Assert.Equal(new DmpOrders(1, 3.5m, 1.5m), shop.After);
        Assert.Equal((Oct(29), 2), (shop.LastOrder, shop.IdleDays));
        Assert.Equal([(2026, 7), (2026, 8), (2026, 9), (2026, 10)], shop.Months.Select(m => (m.Year, m.Month)));
        Assert.Equal(new DmpMonthKg(2026, 10, 83.8m, 12.1m), shop.Months[^1]);
    }

    [Fact]
    public void Regions_count_received_left_installed_issued_and_shops_and_factory_stock_stays_apart()
    {
        var view = Build(
            [
                new DmpShelfLine(1, DmpMove.Installed, Oct(20), 1, 7, "Север", 2),
                new DmpShelfLine(2, DmpMove.Returned, Oct(25), 1, 7, "Север", 1), // магазин вернул один из двух
                new DmpShelfLine(3, DmpMove.Issued, Oct(30), 2, 7, "Север", 1), // выдан, не подтверждён
                new DmpShelfLine(4, DmpMove.Installed, Oct(21), 3, null, "Юг", 1),
                new DmpShelfLine(5, DmpMove.Installed, new DateOnly(2026, 11, 1), 3, null, "Юг", 5), // после даты данных
            ],
            receipts: [new DmpReceipt("Север", Oct(15), 20), new DmpReceipt("Юг", Oct(15), 10), new DmpReceipt("Юг", Oct(29), 5)],
            balances: [new DmpBalance("Север", false, 17), new DmpBalance("Юг", false, 14), new DmpBalance("Завод", true, 900)]);

        Assert.Equal([("Север", 20m, 17m, 1m, 1m, 1), ("Юг", 15m, 14m, 1m, 0m, 1)],
            view.Regions.Select(r => (r.Region, r.Received, r.Left, r.Installed, r.Issued, r.Shops)));
        Assert.Equal([Oct(15), Oct(29)], view.Regions[1].ReceivedDates);
        Assert.Equal((35m, 31m, 900m, 2m, 1m, 2), (view.Received, view.Left, view.FactoryLeft, view.Installed, view.Issued, view.Shops));

        var issued = view.ShopRows.Single(s => s.MarketId == 2);
        Assert.Equal((0m, 1m, Oct(30), 1), (issued.Qty, issued.Issued, issued.Date, issued.Days));
    }

    [Fact]
    public void Shop_without_orders_after_installation_has_empty_window_and_idle_days_from_the_fill_order()
    {
        var view = Build([new DmpShelfLine(10, DmpMove.Installed, Oct(27), 1, 7, "Север", 1)], [new DmpSale(1, 10, false, Oct(27), 35.5m, 23.8m)]);

        var shop = Assert.Single(view.ShopRows);
        Assert.Equal((4, DmpOrders.Empty, DmpOrders.Empty, Oct(27), 4), (shop.Days, shop.Before, shop.After, shop.LastOrder, shop.IdleDays));
    }
}
