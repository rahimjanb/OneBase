using OneBase.AI.Tools.Data;
using OneBase.Application.Sales.Metrics;

namespace OneBase.AI.Tests;

/// <summary>Ответ find_products по регионам: регион без покупок за месяц — «нет данных», а не «нет продаж» и не «не возят».</summary>
public class FindProductsToolTests
{
    private static ProductBreakdownRow Row(string status, decimal kg = 0, decimal prevKg = 0, bool noData = false) =>
        new("r", "Жиззах", null, status, kg, 0, 0, 0, null, prevKg, noData);

    [Fact]
    public void Region_without_purchases_is_reported_as_no_data_not_as_no_sales()
    {
        // В разбивке товара: статус региона с покупками — статус артикула в нём; без покупок (NoData) — «нет данных», каким бы ни был статус.
        Assert.Equal("нет продаж", FindProductsTool.RegionStatusName(Row(SkuStatuses.Silent)));
        Assert.Equal("не возят", FindProductsTool.RegionStatusName(Row(SkuStatuses.Elsewhere)));
        Assert.Equal("пропал", FindProductsTool.RegionStatusName(Row(SkuStatuses.Lost, prevKg: 5)));
        Assert.Equal("нет данных", FindProductsTool.RegionStatusName(Row(SkuStatuses.Silent, noData: true)));
        Assert.Equal("нет данных", FindProductsTool.RegionStatusName(Row(SkuStatuses.Elsewhere, noData: true)));

        // В разбивке категории — то же: «нет данных» или «есть покупки».
        Assert.Equal("нет данных", FindProductsTool.RegionStatusName(new AssortmentRegionRow("r", "Жиззах", 0, 0, 0, 0, 0, 0, NoData: true)));
        Assert.Equal("есть покупки", FindProductsTool.RegionStatusName(new AssortmentRegionRow("r", "Север", 10, 100, 3, 1, 0, 5)));
    }

    [Fact]
    public void Region_rows_shown_are_those_with_sales_now_or_last_month_and_those_without_data()
    {
        Assert.True(FindProductsTool.ShowRegion(Row(SkuStatuses.Selling, kg: 3)));
        Assert.True(FindProductsTool.ShowRegion(Row(SkuStatuses.Lost, prevKg: 2)));
        Assert.True(FindProductsTool.ShowRegion(Row(SkuStatuses.Silent, noData: true))); // регион без покупок остаётся — с ответом «нет данных»
        Assert.False(FindProductsTool.ShowRegion(Row(SkuStatuses.Elsewhere))); // нет ни продаж, ни прошлого месяца — строки нет
    }

    [Fact]
    public void Sku_statuses_have_russian_names()
    {
        Assert.Equal("продаётся", FindProductsTool.StatusName(SkuStatuses.Selling));
        Assert.Equal("пропал", FindProductsTool.StatusName(SkuStatuses.Lost));
        Assert.Equal("не возят", FindProductsTool.StatusName(SkuStatuses.Elsewhere));
        Assert.Equal("нет продаж", FindProductsTool.StatusName(SkuStatuses.Silent));
    }
}
