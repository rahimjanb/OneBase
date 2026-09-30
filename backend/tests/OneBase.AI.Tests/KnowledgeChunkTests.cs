using OneBase.AI.Knowledge;

namespace OneBase.AI.Tests;

public class KnowledgeChunkTests
{
    [Fact]
    public void Short_text_is_one_chunk()
    {
        var chunks = KnowledgeService.Chunk("Регламент отгрузки.\r\n\r\n\r\n\r\nОтгрузка — до 15:00.");

        Assert.Equal(["Регламент отгрузки.\nОтгрузка — до 15:00."], chunks);
    }

    [Fact]
    public void Long_text_is_split_by_paragraphs_within_size()
    {
        var paragraphs = Enumerable.Range(1, 30).Select(i => $"Абзац {i}. " + new string('а', 150));
        var chunks = KnowledgeService.Chunk(string.Join("\n\n", paragraphs), size: 500);

        Assert.True(chunks.Count > 5);
        Assert.All(chunks, c => Assert.True(c.Length <= 500, $"фрагмент {c.Length} символов"));
        Assert.StartsWith("Абзац 1.", chunks[0]);
        Assert.Contains("Абзац 30.", chunks[^1]);
    }

    [Fact]
    public void Very_long_line_is_cut_without_losing_text()
    {
        var sentence = "Продажи выросли в регионе. ";
        var text = string.Concat(Enumerable.Repeat(sentence, 200));
        var chunks = KnowledgeService.Chunk(text, size: 300);

        Assert.All(chunks, c => Assert.True(c.Length <= 300));
        Assert.Equal(200, chunks.Sum(c => CountOf(c, "Продажи")) - Overlap(chunks));
    }

    private static int CountOf(string text, string word) => (text.Length - text.Replace(word, string.Empty).Length) / word.Length;

    /// <summary>Короткий хвост фрагмента повторяется в начале следующего — вычитаем повторы.</summary>
    private static int Overlap(IReadOnlyList<string> chunks) =>
        chunks.Skip(1).Select((c, i) => chunks[i].Split('\n')[^1] is var tail && c.StartsWith(tail + "\n") ? CountOf(tail, "Продажи") : 0).Sum();
}
