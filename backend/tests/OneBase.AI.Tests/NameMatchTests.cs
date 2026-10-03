using OneBase.AI.Tools.Data;

namespace OneBase.AI.Tests;

public class NameMatchTests
{
    [Theory]
    [InlineData("Помадка", "pomadka")]
    [InlineData("Помадка 0,5 кг", "pomadka05kg")]
    [InlineData("Yamelly Orange", "yamellyorange")]
    [InlineData("Трубочки", "trubochki")]
    public void Key_transliterates_and_strips(string text, string expected) => Assert.Equal(expected, NameMatch.Key(text));

    [Fact]
    public void Russian_query_finds_latin_product_name()
    {
        Assert.Equal(3, NameMatch.Score("130 Конфеты помадные со вкусом фруктовый микс, POMADKA, (MIX) 230-гр", "помадка"));
    }

    [Fact]
    public void Stem_matches_other_word_forms()
    {
        // «помадка» → «помадные»: целого слова в названии нет, основа есть.
        Assert.Equal(1, NameMatch.Score("407 Конфеты помадные со вкусом кофе, KOFFEO, 2-кг", "помадка"));
    }

    [Fact]
    public void Latin_query_finds_cyrillic_type()
    {
        Assert.Equal(3, NameMatch.Score("Помадка 1 кг", "POMADKA"));
    }

    [Fact]
    public void All_words_must_match()
    {
        Assert.Equal(2, NameMatch.Score("107 Конфеты помадные \"Апельсин\" Yamelly Orange (8 шт по 0,5-кг) 4кг", "yamelly апельсин"));
        Assert.Equal(0, NameMatch.Score("107 Конфеты помадные \"Апельсин\" Yamelly Orange (8 шт по 0,5-кг) 4кг", "yamelly вишня"));
    }

    [Fact]
    public void Unrelated_names_do_not_match()
    {
        Assert.Equal(0, NameMatch.Score("Бамбук", "помадка"));
        Assert.Equal(0, NameMatch.Score("Кекс", "м"));
    }
}
