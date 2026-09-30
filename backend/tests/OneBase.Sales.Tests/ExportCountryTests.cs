using OneBase.Application.Sales;

namespace OneBase.Sales.Tests;

public class ExportCountryTests
{
    private static readonly SalesOptions Options = new();

    [Theory]
    [InlineData("Daler Tojikiston", "Dushanbe", "Таджикистан")]
    [InlineData("Abdumavlon Tojikiston", "Hujand", "Таджикистан")]
    [InlineData("Adamium Armenia", "Armenia", "Армения")]
    [InlineData("DOS LTD Kyrgyzstan", "Kyrgyzstan", "Киргизия")]
    [InlineData("ARAZ AZERBAIJAN", "AZERBAIJAN", "Азербайджан")]
    [InlineData("Orgil Conditer Mongolia", null, "Монголия")]
    [InlineData("Казахстан - Актобе", null, "Казахстан")]
    [InlineData("Dagestan", "Ставропольский край, г. Михайловск", "Россия")]
    public void Country_is_found_in_market_name_or_address(string name, string? address, string country) =>
        Assert.Equal(country, Options.ExportCountryOf(name, address));

    [Fact]
    public void Unknown_market_has_no_country() =>
        Assert.Null(Options.ExportCountryOf("Murod D/63", "Toshkent"));

    [Fact]
    public void Only_configured_market_types_are_export() =>
        Assert.True(Options.IsExportMarketType(" export ") && !Options.IsExportMarketType("Оптовая торговля"));

    [Theory]
    [InlineData("Сентябрь Бамбук Бухоро", "Бамбук")]
    [InlineData("Сентябрь Могуль + Шоколад Шахрисабз Самарканд", "Помадка,Шоколад")]
    [InlineData("Сентябрь Трубочка + Печение Шахрисабз Самарканд", "Трубочки,Печенье")]
    [InlineData("Сентябрь план Самарканд", "")]
    public void Plan_indicator_is_split_into_report_categories(string indicator, string categories) =>
        Assert.Equal(categories.Split(',', StringSplitOptions.RemoveEmptyEntries), Options.PlanCategoriesOf(indicator));

    [Theory]
    [InlineData(0, "Иван", true)]
    [InlineData(15, "Вакант Самарканд", true)]
    [InlineData(16, "Агент Ташкент", false)]
    public void Vacancy_is_marked_by_name_or_zero_id(long id, string name, bool vacancy) =>
        Assert.Equal(vacancy, Options.IsVacancy(id, name));
}
