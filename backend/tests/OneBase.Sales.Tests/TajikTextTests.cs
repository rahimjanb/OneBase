using OneBase.Application.Sales.Tajik;
using Xunit;

namespace OneBase.Sales.Tests;

/// <summary>Разбор текстов 1С Таджикистана: регион по названию точки, код Linko и вес единицы по названию товара, выбор «коробка или штука» по цене.</summary>
public class TajikTextTests
{
    private static readonly (int Key, IReadOnlyList<string> Keywords)[] Regions =
    [
        (1, ["кулоб", "куляб"]),
        (2, ["чоми", "джоми"]),
        (3, ["ёвон"]),
        (4, ["панч"]),
        (5, ["шахритуз", "шахритус"]),
        (6, ["сарбанд", "левакант"]),
        (7, ["хочи шариф"]),
    ];

    [Theory]
    [InlineData("Муким  стоянка Шариф Кулоб", 1)]
    [InlineData("Ширин Кӯлоб", 1)]
    [InlineData("Ахмад магазин А.Чоми", 2)]
    [InlineData("Зарина Ҷоми", 2)]
    [InlineData("Дилшод -Ёвон", 3)]
    [InlineData("Дилшод Ëвон", 3)] // латинская Ë из 1С
    [InlineData("Дилафруз пеши Инобат навес бозор Шахритус", 5)]
    [InlineData("Маг Левакант ру ба руи военкамат", 6)]
    [InlineData("Нисо 2 праход Хочи Шариф", 7)]
    public void Region_comes_from_a_whole_word_in_the_store_name(string store, int expected) =>
        Assert.Equal(expected, TajikText.MatchRegion(store, Regions));

    [Fact]
    public void Part_of_a_word_is_not_a_region_and_the_last_place_in_the_name_wins()
    {
        // «Панчшер» — имя, не Пяндж; район в конце названия главнее слова в середине.
        Assert.Null(TajikText.MatchRegion("Панчшер кизилкшар рохи Чапаев", Regions));
        Assert.Equal(2, TajikText.MatchRegion("Кулоб ака магазин Чоми", Regions));
        Assert.Null(TajikText.MatchRegion("Розничная продажа", Regions));
        Assert.Null(TajikText.MatchRegion("", Regions));
    }

    [Fact]
    public void Normalize_maps_tajik_letters_and_latin_twins_inside_cyrillic_words()
    {
        Assert.Equal("кулоб чоми хочи", TajikText.Normalize("Кӯлоб, Ҷоми — Ҳочи"));
        // «Kулоб» с латинской K — та же точка; латинские слова не трогаются.
        Assert.Equal("кулоб twizo s", TajikText.Normalize("Kулоб TWIZO'S"));
    }

    [Theory]
    [InlineData("183 Азалия 1,5 кг чёрный", "183")]
    [InlineData("600.Снеки солёные «TWIZO'S» 200г*20шт", "600")]
    [InlineData("002.Rohat хруст-палочки молочный болшой 3кгКор.", "002")]
    [InlineData("475. Кекс Нилло брауни-1,5кг-Желтый", "475")]
    [InlineData("Помадка Ягода 4кг-Росип-105", null)]
    public void Linko_code_is_the_number_at_the_start_of_the_name(string name, string? code) => Assert.Equal(code, TajikText.LinkoCode(name));

    [Fact]
    public void Same_code_ignores_leading_zeros()
    {
        Assert.True(TajikText.SameCode("002", "002"));
        Assert.True(TajikText.SameCode("2", "002"));
        Assert.False(TajikText.SameCode("20", "002"));
        Assert.False(TajikText.SameCode(null, "002"));
    }

    [Theory]
    [InlineData("183 Азалия 1,5 кг чёрный", 1.5, null, null)]
    [InlineData("002.Rohat хруст-палочки молочный болшой 3кгКор.", 3.0, null, null)]
    [InlineData("346.Печенье сахарное Фрулли апельсин -2кг", 2.0, null, null)]
    [InlineData("476. Глазированные помадные кон-ы со вкусом апельсина Yamelly Зиима-420гр*8шт", null, 0.42, 8)]
    [InlineData("600.Снеки солёные «TWIZO'S» 200г*20шт", null, 0.2, 20)]
    [InlineData("494.Крупные палочки STIX Bamboo -400гр*10шт", null, 0.4, 10)]
    [InlineData("Без веса в названии", null, null, null)]
    public void Weight_from_the_name(string name, double? single, double? piece, int? count)
    {
        var (s, p, c) = TajikText.WeightOf(name);
        Assert.Equal(single is null ? null : (decimal)single.Value, s);
        Assert.Equal(piece is null ? null : (decimal)piece.Value, p);
        Assert.Equal(count, c);
    }

    [Fact]
    public void Box_or_piece_is_chosen_by_price_against_linko()
    {
        // Цены — из ответа 1С за ноябрь 2026 (сомони за единицу) и Linko (сум за кг).
        var weights = TajikWeights.Resolve(
        [
            new("НФ-358", "183 Азалия 1,5 кг чёрный", 69.35m, 45_282m, 1m),
            new("НФ-1487", "462. Помадка Зима 4кг", 152m, 36_184m, 1m),
            new("НФ-1748", "346.Печенье сахарное Фрулли апельсин -2кг", 69.5m, 36_919m, 1m),
            // Помадка 8 × 420 г за 128 сомони — коробка; Twizos 200 г за 15,1 — пачка; STIX 400 г за 11 — пачка.
            new("НФ-1771", "476. Глазированные помадные кон-ы Yamelly Зиима-420гр*8шт", 128m, 16_861m / 0.42m, 0.42m),
            new("НФ-1969", "600.Снеки солёные «TWIZO'S» 200г*20шт", 15.1m, 13_556m / 0.2m, 0.2m),
            new("НФ-1846", "494.Крупные палочки STIX Bamboo -400гр*10шт", 11m, 10_195m / 0.4m, 0.4m),
            // Без товара Linko: однозначный вес — из названия, двоякий — штука с пометкой.
            new("НФ-1022", "Помадка Ягода 4кг-Росип-105", 134.4m, null, null),
            new("X-1", "Новинка 50г*12шт", 5m, null, null),
            new("X-2", "Без веса", 5m, 20_000m, 0.25m),
            new("X-3", "Без веса и без Linko", 5m, null, null),
        ]);

        Assert.Equal(new TajikUnitWeight(1.5m, TajikWeights.FromName), weights["НФ-358"]);
        Assert.Equal(new TajikUnitWeight(3.36m, TajikWeights.Box), weights["НФ-1771"]);
        Assert.Equal(new TajikUnitWeight(0.2m, TajikWeights.Piece), weights["НФ-1969"]);
        Assert.Equal(new TajikUnitWeight(0.4m, TajikWeights.Piece), weights["НФ-1846"]);
        Assert.Equal(new TajikUnitWeight(4m, TajikWeights.FromName), weights["НФ-1022"]);
        Assert.Equal(new TajikUnitWeight(0.05m, TajikWeights.Ambiguous), weights["X-1"]);
        Assert.Equal(new TajikUnitWeight(0.25m, TajikWeights.FromLinko), weights["X-2"]);
        Assert.Equal(new TajikUnitWeight(null, TajikWeights.Unknown), weights["X-3"]);
    }

    [Theory]
    [InlineData("18-11-2026", 2026, 11, 18)]
    [InlineData("2026-11-18", 2026, 11, 18)]
    public void Api_dates(string text, int y, int m, int d)
    {
        Assert.True(TajikText.TryParseApiDate(text, out var date));
        Assert.Equal(new DateOnly(y, m, d), date);
        Assert.Equal("18-11-2026", TajikText.ApiDate(new DateOnly(2026, 11, 18)));
        Assert.False(TajikText.TryParseApiDate("xx", out _));
    }

    [Fact]
    public void Store_id_is_stable_and_ignores_outer_spaces()
    {
        Assert.Equal(TajikText.StoreId("Муким  стоянка Шариф Кулоб"), TajikText.StoreId(" Муким  стоянка Шариф Кулоб "));
        Assert.NotEqual(TajikText.StoreId("Муким стоянка Шариф Кулоб"), TajikText.StoreId("Муким  стоянка Шариф Кулоб"));
        Assert.Equal(16, TajikText.StoreId("a").Length);
    }
}
