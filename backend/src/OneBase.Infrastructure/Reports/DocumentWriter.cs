using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using OneBase.Application.Reports;

namespace OneBase.Infrastructure.Reports;

/// <summary>
/// Markdown от AI → docx (Open XML SDK): «# …» — заголовок документа, «## …»/«### …» — подзаголовки, списки «-»/«1.», таблицы «| … |»,
/// **жирный**, *курсив*, `код`, ссылки [текст](адрес) — текстом с адресом, «> …» — цитата, «```» — блок кода. Страница A4.
/// Отметка «сформировано в OneBase» — в свойствах файла, не в тексте: письмо можно переслать как есть.
/// </summary>
public sealed partial class DocumentWriter : IDocumentWriter
{
    private const string Font = "Calibri";
    private const string CodeFont = "Consolas";
    private const string HeadingColor = "1F3864";

    public byte[] Write(string title, string markdown, DateTimeOffset createdAt)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            document.PackageProperties.Title = Clean(title);
            document.PackageProperties.Creator = "OneBase";
            document.PackageProperties.Created = createdAt.UtcDateTime;

            var main = document.AddMainDocumentPart();
            main.AddNewPart<StyleDefinitionsPart>().Styles = Styles();
            var body = new Body();
            foreach (var element in Blocks(Clean(markdown)))
            {
                body.Append(element);
            }

            if (!body.HasChildren)
            {
                body.Append(new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "Heading1" }), TextRun(Clean(title))));
            }

            body.Append(new SectionProperties(
                new PageSize { Width = 11906U, Height = 16838U },
                new PageMargin { Top = 1134, Right = 850U, Bottom = 1134, Left = 1418U, Header = 708U, Footer = 708U, Gutter = 0U }));
            main.Document = new Document(body);
        }

        return stream.ToArray();
    }

    // ---------- Блоки ----------

    private static IEnumerable<OpenXmlElement> Blocks(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var paragraph = new List<string>();
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd();
            var trimmed = line.TrimStart();

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                foreach (var p in Flush(paragraph))
                {
                    yield return p;
                }

                for (i++; i < lines.Length && !lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal); i++)
                {
                    yield return new Paragraph(
                        new ParagraphProperties(new SpacingBetweenLines { After = "0" }),
                        new Run(new RunProperties(new RunFonts { Ascii = CodeFont, HighAnsi = CodeFont, ComplexScript = CodeFont }, new FontSize { Val = "19" }),
                            new Text(lines[i].TrimEnd()) { Space = SpaceProcessingModeValues.Preserve }));
                }

                continue;
            }

            if (trimmed.Length == 0)
            {
                foreach (var p in Flush(paragraph))
                {
                    yield return p;
                }

                continue;
            }

            if (Heading().Match(trimmed) is { Success: true } heading)
            {
                foreach (var p in Flush(paragraph))
                {
                    yield return p;
                }

                var level = Math.Min(heading.Groups[1].Value.Length, 3);
                yield return new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = $"Heading{level}" }), TextRun(Plain(heading.Groups[2].Value)));
                continue;
            }

            if (Rule().IsMatch(trimmed))
            {
                foreach (var p in Flush(paragraph))
                {
                    yield return p;
                }

                continue;
            }

            if (trimmed.StartsWith('|') && i + 1 < lines.Length && Separator().IsMatch(lines[i + 1].Trim()))
            {
                foreach (var p in Flush(paragraph))
                {
                    yield return p;
                }

                var header = Cells(trimmed);
                var rows = new List<IReadOnlyList<string>>();
                for (i += 2; i < lines.Length && lines[i].TrimStart().StartsWith('|'); i++)
                {
                    rows.Add(Cells(lines[i].Trim()));
                }

                i--;
                yield return Table(header, rows);
                yield return new Paragraph(new ParagraphProperties(new SpacingBetweenLines { After = "0" }));
                continue;
            }

            if (Bullet().Match(line) is { Success: true } bullet)
            {
                foreach (var p in Flush(paragraph))
                {
                    yield return p;
                }

                var depth = bullet.Groups[1].Value.Replace("\t", "    ").Length / 2;
                yield return ListItem("•", bullet.Groups[2].Value, depth);
                continue;
            }

            if (Numbered().Match(line) is { Success: true } numbered)
            {
                foreach (var p in Flush(paragraph))
                {
                    yield return p;
                }

                var depth = numbered.Groups[1].Value.Replace("\t", "    ").Length / 2;
                yield return ListItem(numbered.Groups[2].Value + ".", numbered.Groups[3].Value, depth);
                continue;
            }

            if (trimmed.StartsWith('>'))
            {
                foreach (var p in Flush(paragraph))
                {
                    yield return p;
                }

                var quote = new Paragraph(new ParagraphProperties(
                    new ParagraphBorders(new LeftBorder { Val = BorderValues.Single, Size = 12U, Space = 8U, Color = "B4C6E7" }),
                    new Indentation { Left = "284" }));
                foreach (var run in Runs(trimmed.TrimStart('>').Trim(), italic: true))
                {
                    quote.Append(run);
                }

                yield return quote;
                continue;
            }

            paragraph.Add(trimmed);
        }

        foreach (var p in Flush(paragraph))
        {
            yield return p;
        }
    }

    /// <summary>Строки подряд — один абзац (как в Markdown); перенос строки внутри сохраняется.</summary>
    private static IEnumerable<Paragraph> Flush(List<string> lines)
    {
        if (lines.Count == 0)
        {
            yield break;
        }

        var paragraph = new Paragraph();
        for (var i = 0; i < lines.Count; i++)
        {
            if (i > 0)
            {
                paragraph.Append(new Run(new Break()));
            }

            foreach (var run in Runs(lines[i]))
            {
                paragraph.Append(run);
            }
        }

        lines.Clear();
        yield return paragraph;
    }

    private static Paragraph ListItem(string marker, string text, int depth)
    {
        var left = 360 + depth * 360;
        var item = new Paragraph(new ParagraphProperties(
            new SpacingBetweenLines { After = "60" },
            new Indentation { Left = left.ToString(), Hanging = "360" }));
        item.Append(TextRun(marker + "\t"));
        foreach (var run in Runs(text))
        {
            item.Append(run);
        }

        return item;
    }

    private static Table Table(IReadOnlyList<string> header, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var columns = Math.Max(header.Count, rows.Count == 0 ? 1 : rows.Max(r => r.Count));
        var table = new Table(new TableProperties(
            new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" },
            new TableBorders(
                Border<TopBorder>(), Border<LeftBorder>(), Border<BottomBorder>(), Border<RightBorder>(),
                Border<InsideHorizontalBorder>(), Border<InsideVerticalBorder>()),
            new TableCellMarginDefault(
                new TopMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
                new TableCellLeftMargin { Width = 85, Type = TableWidthValues.Dxa },
                new BottomMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
                new TableCellRightMargin { Width = 85, Type = TableWidthValues.Dxa })));
        var grid = new TableGrid();
        for (var c = 0; c < columns; c++)
        {
            grid.Append(new GridColumn { Width = (9638 / columns).ToString() });
        }

        table.Append(grid);
        table.Append(Row(header, columns, true));
        foreach (var row in rows)
        {
            table.Append(Row(row, columns, false));
        }

        return table;
    }

    private static T Border<T>() where T : BorderType, new() => new() { Val = BorderValues.Single, Size = 4U, Color = "BFBFBF" };

    private static TableRow Row(IReadOnlyList<string> cells, int columns, bool header)
    {
        var row = new TableRow();
        if (header)
        {
            row.Append(new TableRowProperties(new TableHeader()));
        }

        for (var c = 0; c < columns; c++)
        {
            var text = c < cells.Count ? cells[c] : string.Empty;
            var properties = new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = (5000 / columns).ToString() });
            if (header)
            {
                properties.Append(new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = "E8EEF8" });
            }

            var paragraph = new Paragraph(new ParagraphProperties(new SpacingBetweenLines { After = "0" }));
            foreach (var run in Runs(text, bold: header))
            {
                paragraph.Append(run);
            }

            row.Append(new TableCell(properties, paragraph));
        }

        return row;
    }

    private static IReadOnlyList<string> Cells(string line)
    {
        var inner = line.Trim();
        inner = inner.StartsWith('|') ? inner[1..] : inner;
        inner = inner.EndsWith('|') ? inner[..^1] : inner;
        return inner.Split('|').Select(c => c.Trim()).ToList();
    }

    // ---------- Текст внутри строки ----------

    /// <summary>**жирный**, __жирный__, *курсив*, _курсив_, `код`, [текст](адрес) → куски текста с оформлением.</summary>
    private static IEnumerable<Run> Runs(string text, bool bold = false, bool italic = false)
    {
        var position = 0;
        foreach (Match m in Inline().Matches(text))
        {
            if (m.Index > position)
            {
                yield return TextRun(text[position..m.Index], bold, italic);
            }

            if (m.Groups["b"].Success || m.Groups["b2"].Success)
            {
                foreach (var run in Runs(m.Groups["b"].Success ? m.Groups["b"].Value : m.Groups["b2"].Value, true, italic))
                {
                    yield return run;
                }
            }
            else if (m.Groups["i"].Success || m.Groups["i2"].Success)
            {
                foreach (var run in Runs(m.Groups["i"].Success ? m.Groups["i"].Value : m.Groups["i2"].Value, bold, true))
                {
                    yield return run;
                }
            }
            else if (m.Groups["code"].Success)
            {
                yield return new Run(new RunProperties(new RunFonts { Ascii = CodeFont, HighAnsi = CodeFont, ComplexScript = CodeFont }),
                    new Text(m.Groups["code"].Value) { Space = SpaceProcessingModeValues.Preserve });
            }
            else
            {
                var label = m.Groups["label"].Value;
                var url = m.Groups["url"].Value;
                yield return TextRun(label == url ? url : $"{label} ({url})", bold, italic);
            }

            position = m.Index + m.Length;
        }

        if (position < text.Length)
        {
            yield return TextRun(text[position..], bold, italic);
        }
    }

    private static Run TextRun(string text, bool bold = false, bool italic = false)
    {
        var run = new Run();
        if (bold || italic)
        {
            var properties = new RunProperties();
            if (bold)
            {
                properties.Append(new Bold());
            }

            if (italic)
            {
                properties.Append(new Italic());
            }

            run.Append(properties);
        }

        run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return run;
    }

    /// <summary>Заголовок без разметки: «**Итог**» → «Итог».</summary>
    private static string Plain(string text) => Inline().Replace(text, m =>
        m.Groups["b"].Success ? m.Groups["b"].Value :
        m.Groups["b2"].Success ? m.Groups["b2"].Value :
        m.Groups["i"].Success ? m.Groups["i"].Value :
        m.Groups["i2"].Success ? m.Groups["i2"].Value :
        m.Groups["code"].Success ? m.Groups["code"].Value : m.Groups["label"].Value).Trim();

    /// <summary>Без управляющих символов (XML их не допускает), кроме табуляции и переводов строки.</summary>
    private static string Clean(string? text)
    {
        var sb = new StringBuilder((text ?? string.Empty).Length);
        foreach (var c in text ?? string.Empty)
        {
            if (c is '\t' or '\n' or '\r' || (c >= ' ' && c != '￾' && c != '￿'))
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    // ---------- Стили ----------

    private static Styles Styles() => new(
        new DocDefaults(
            new RunPropertiesDefault(new RunPropertiesBaseStyle(
                new RunFonts { Ascii = Font, HighAnsi = Font, ComplexScript = Font, EastAsia = Font },
                new FontSize { Val = "22" },
                new FontSizeComplexScript { Val = "22" },
                new Languages { Val = "ru-RU" })),
            new ParagraphPropertiesDefault(new ParagraphPropertiesBaseStyle(
                new SpacingBetweenLines { After = "120", Line = "276", LineRule = LineSpacingRuleValues.Auto }))),
        new Style(new StyleName { Val = "Normal" }, new PrimaryStyle()) { Type = StyleValues.Paragraph, StyleId = "Normal", Default = true },
        HeadingStyle(1, 32, "240"),
        HeadingStyle(2, 26, "200"),
        HeadingStyle(3, 23, "160"));

    private static Style HeadingStyle(int level, int halfPoints, string before) => new(
        new StyleName { Val = $"heading {level}" },
        new BasedOn { Val = "Normal" },
        new NextParagraphStyle { Val = "Normal" },
        new PrimaryStyle(),
        new StyleParagraphProperties(
            new KeepNext(),
            new SpacingBetweenLines { Before = before, After = "120" },
            new OutlineLevel { Val = level - 1 }),
        new StyleRunProperties(
            new Bold(),
            new Color { Val = HeadingColor },
            new FontSize { Val = halfPoints.ToString() }))
    {
        Type = StyleValues.Paragraph,
        StyleId = $"Heading{level}",
    };

    [GeneratedRegex(@"^(#{1,6})\s+(.+?)\s*#*$")]
    private static partial Regex Heading();

    [GeneratedRegex(@"^(-{3,}|\*{3,}|_{3,})$")]
    private static partial Regex Rule();

    [GeneratedRegex(@"^\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)*\|?$")]
    private static partial Regex Separator();

    [GeneratedRegex(@"^(\s*)[-*•]\s+(.+)$")]
    private static partial Regex Bullet();

    [GeneratedRegex(@"^(\s*)(\d{1,3})[.)]\s+(.+)$")]
    private static partial Regex Numbered();

    [GeneratedRegex(@"\*\*(?<b>.+?)\*\*|(?<!\w)__(?<b2>.+?)__(?!\w)|(?<![\*\w])\*(?<i>[^\s*](?:.*?[^\s*])?)\*(?![\*\w])|(?<!\w)_(?<i2>[^\s_](?:.*?[^\s_])?)_(?!\w)|`(?<code>[^`]+)`|\[(?<label>[^\]]+)\]\((?<url>[^)\s]+)\)")]
    private static partial Regex Inline();
}
