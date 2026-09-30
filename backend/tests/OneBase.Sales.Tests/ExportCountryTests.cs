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
}
