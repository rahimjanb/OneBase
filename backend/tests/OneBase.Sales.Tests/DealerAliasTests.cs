using OneBase.Application.DataManager;
using OneBase.Domain.Sales;

namespace OneBase.Sales.Tests;

/// <summary>Другие имена дилера региона: смена дилера сохраняет прежнее имя, сопоставление строк отчёта находит регион и по ним.</summary>
public class DealerAliasTests
{
    [Fact]
    public void Changing_the_dealer_keeps_the_previous_name_as_an_alias()
    {
        var region = new SalesRegion { Name = "Ташкент" };
        region.ChangeDealer("Прежний шахар");
        Assert.Null(region.DealerAliases); // прежнего не было

        region.ChangeDealer("Новый Шахар");
        Assert.Equal("Новый Шахар", region.DealerName);
        Assert.Equal(["Прежний шахар"], region.DealerAliases);

        region.ChangeDealer("Прежний Шахар"); // вернулся прежний (регистр не важен) — из других имён он уходит, сменившийся — остаётся
        Assert.Equal(["Новый Шахар"], region.DealerAliases);

        region.SetDealerAliases([" Новый  Шахар ", "новый шахар", "", "прежний шахар", "Второй склад"]);
        Assert.Equal(["Новый Шахар", "Второй склад"], region.DealerAliases); // без повторов, пустых и самого дилера
        region.SetDealerAliases([]);
        Assert.Null(region.DealerAliases);
    }

    [Fact]
    public void Report_rows_find_the_region_by_its_name_dealer_or_another_dealer_name()
    {
        var tashkent = new DataRegionRef(Guid.NewGuid(), "Ташкент", "Прежний шахар", 10, Aliases: ["Новый Шахар"]);
        var khorazm = new DataRegionRef(Guid.NewGuid(), "Хоразм", "Дилер Хоразм", 11);
        var lookup = new DataMatcher([tashkent, khorazm], []);

        Assert.Equal(tashkent.Id, lookup.Region("Прежний Шахар").Region!.Id);
        Assert.Equal(tashkent.Id, lookup.Region("Новый Шахар").Region!.Id); // другое имя дилера
        Assert.Equal(tashkent.Id, lookup.Region("Ташкент").Region!.Id);
        Assert.Equal(khorazm.Id, lookup.Region("Дилер Хоразм").Region!.Id);
        Assert.Contains("не найден", lookup.Region("Умид шахар").Error);
    }
}
