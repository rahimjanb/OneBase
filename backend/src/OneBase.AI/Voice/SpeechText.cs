using System.Text;
using System.Text.RegularExpressions;

namespace OneBase.AI.Voice;

/// <summary>
/// Ответ консультанта (Markdown) → текст для чтения вслух: без разметки и ссылок, таблица — по строкам «столбец: значение»,
/// и деление на части: первая короткая (звук начинается через секунду-две), дальше — крупнее.
/// </summary>
public static partial class SpeechText
{
    /// <summary>Первая часть — короткая, чтобы озвучка началась быстро.</summary>
    public const int FirstPartChars = 350;

    /// <summary>Остальные части — с запасом под лимит модели озвучки.</summary>
    public const int PartChars = 1500;

    /// <summary>
    /// Источники вслух не читаются: строки и предложения «Источник: …», список после заголовка «Источники», столбец «Источник» в таблице,
    /// «(источник: …)» и имена инструментов вроде get_sales — это для экрана, на слух они только мешают.
    /// </summary>
    public static string From(string markdown)
    {
        var lines = (markdown ?? string.Empty).Replace("\r", "").Split('\n');
        var output = new List<string>();
        string[]? header = null;
        var inCode = false;
        var inSources = false;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                inCode = !inCode; // код вслух не читается
                continue;
            }

            if (inCode || line.Length == 0 || HorizontalRule().IsMatch(line))
            {
                header = line.StartsWith('|') ? header : null;
                continue;
            }

            if (line.StartsWith('|'))
            {
                var cells = line.Trim('|').Split('|').Select(c => Inline(c.Trim())).ToArray();
                if (cells.All(c => TableSeparator().IsMatch(c) || c.Length == 0))
                {
                    continue; // |---|---|
                }

                if (header is null)
                {
                    header = cells;
                    continue;
                }

                var parts = cells.Select((c, i) => (Name: i < header.Length ? header[i] : "", Value: c))
                    .Where(p => p.Value.Length > 0 && p.Value != "—" && !SourceHeader().IsMatch(p.Name))
                    .Select((p, i) => i == 0 || p.Name.Length == 0 ? p.Value : $"{p.Name}: {p.Value}");
                var row = Clean(string.Join(", ", parts));
                if (row.Length > 0)
                {
                    output.Add(Sentence(row));
                }

                continue;
            }

            header = null;
            var bullet = Bullet().IsMatch(line);
            line = Heading().Replace(line, "");
            line = Bullet().Replace(line, "");
            line = Quote().Replace(line, "");
            var text = Inline(line);

            // «Источники:» — заголовок списка: его пункты пропускаются до первой строки не из списка.
            if (SourceHeader().IsMatch(text))
            {
                inSources = true;
                continue;
            }

            if (inSources && bullet)
            {
                continue;
            }

            inSources = false;
            text = Clean(string.Join(" ", Sentences().Split(text).Where(s => !SourceSentence().IsMatch(s))));
            if (text.Length > 0)
            {
                output.Add(Sentence(text));
            }
        }

        return Spaces().Replace(string.Join(" ", output), " ").Trim();
    }

    /// <summary>Части по границам предложений; слишком длинное предложение режется по пробелу.</summary>
    public static IReadOnlyList<string> Split(string text)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        foreach (var sentence in Sentences().Split(text).Where(s => s.Length > 0))
        {
            var limit = parts.Count == 0 ? FirstPartChars : PartChars;
            if (current.Length > 0 && current.Length + 1 + sentence.Length > limit)
            {
                parts.Add(current.ToString());
                current.Clear();
                limit = PartChars;
            }

            var rest = sentence;
            while (rest.Length > limit)
            {
                var cut = rest.LastIndexOf(' ', limit);
                cut = cut > limit / 2 ? cut : limit;
                if (current.Length > 0)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                }

                parts.Add(rest[..cut].Trim());
                rest = rest[cut..].Trim();
                limit = PartChars;
            }

            if (current.Length > 0)
            {
                current.Append(' ');
            }

            current.Append(rest);
        }

        if (current.Length > 0)
        {
            parts.Add(current.ToString());
        }

        return parts;
    }

    private static string Inline(string text)
    {
        text = Link().Replace(text, "$1");
        text = Url().Replace(text, "ссылка");
        text = Emphasis().Replace(text, "$2");
        text = text.Replace("`", "").Replace("→", " — ").Replace("≈", "примерно ").Replace("±", "плюс-минус ").Replace("&nbsp;", " ");
        return Spaces().Replace(text, " ").Trim();
    }

    private static string Sentence(string text) => text.Length > 0 && ".!?:;…".Contains(text[^1]) ? text : text + ".";

    /// <summary>Без «(источник: …)» и имён инструментов (get_sales); лишние пробелы и пробел перед знаком препинания убираются.</summary>
    private static string Clean(string text)
    {
        text = SourceParenthesis().Replace(text, "");
        text = ToolName().Replace(text, "");
        text = Spaces().Replace(text, " ");
        return SpaceBeforePunctuation().Replace(text, "$1").Trim();
    }

    [GeneratedRegex(@"^(источник|источники|source|sources)(\s+данных)?\s*[:.]?$", RegexOptions.IgnoreCase)]
    private static partial Regex SourceHeader();

    [GeneratedRegex(@"^\s*(источник|источники|source|sources)(\s+данных)?\s*[:—–-]", RegexOptions.IgnoreCase)]
    private static partial Regex SourceSentence();

    [GeneratedRegex(@"\s*\((источник|источники|source|sources)\b[^)]*\)", RegexOptions.IgnoreCase)]
    private static partial Regex SourceParenthesis();

    [GeneratedRegex(@"\b[a-z][a-z0-9]*(?:_[a-z0-9]+)+\b")]
    private static partial Regex ToolName();

    [GeneratedRegex(@"\s+([,.;:!?…])")]
    private static partial Regex SpaceBeforePunctuation();

    [GeneratedRegex(@"\[([^\]]+)\]\([^)]+\)")]
    private static partial Regex Link();

    [GeneratedRegex(@"https?://\S+")]
    private static partial Regex Url();

    [GeneratedRegex(@"(\*\*|__|\*|_)(\S(?:.*?\S)?)\1")]
    private static partial Regex Emphasis();

    [GeneratedRegex(@"^#{1,6}\s*")]
    private static partial Regex Heading();

    [GeneratedRegex(@"^([-*•+]|\d+[.)])\s+")]
    private static partial Regex Bullet();

    [GeneratedRegex(@"^>\s*")]
    private static partial Regex Quote();

    [GeneratedRegex(@"^(-{3,}|\*{3,}|_{3,})$")]
    private static partial Regex HorizontalRule();

    [GeneratedRegex(@"^:?-{2,}:?$")]
    private static partial Regex TableSeparator();

    [GeneratedRegex(@"(?<=[.!?…])\s+")]
    private static partial Regex Sentences();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
