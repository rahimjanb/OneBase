using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using OneBase.Application.Sales.Stock;

namespace OneBase.Infrastructure.Reports;

/// <summary>
/// «Акт приёма-передачи» автозаказа в Word — по бланку Linko: «Акт №____ / ПРИЕМА-ПЕРЕДАЧИ», «Завод → регион» и дата, абзац «Настоящий
/// акт составлен о том, что…», таблица «Наименование · Кор. · Ед. изм. · Кол-во · Цена за шт, SUM · Цена, SUM» с итогом, абзац о
/// материальной ответственности и подписи «Сдал» / «Принял». A4, книжная, текст бланка — по-русски, как в Linko.
/// </summary>
public sealed class TransferActWriter : ITransferActWriter
{
    private const string Font = "Calibri";
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    private static readonly string[] Months =
        ["Январь", "Февраль", "Март", "Апрель", "Май", "Июнь", "Июль", "Август", "Сентябрь", "Октябрь", "Ноябрь", "Декабрь"];

    // Ширины колонок таблицы, twips: A4 (11906) минус поля по 720 — 10 466.
    private static readonly int[] Widths = [4766, 800, 900, 1100, 1350, 1550];

    public byte[] Write(TransferAct act)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            document.PackageProperties.Title = $"Акт приёма-передачи {act.From} → {act.To} {act.Date:dd.MM.yyyy}";
            document.PackageProperties.Creator = "OneBase";
            document.PackageProperties.Created = DateTime.UtcNow;

            var main = document.AddMainDocumentPart();
            main.AddNewPart<StyleDefinitionsPart>().Styles = new Styles(
                new DocDefaults(
                    new RunPropertiesDefault(new RunPropertiesBaseStyle(
                        new RunFonts { Ascii = Font, HighAnsi = Font, ComplexScript = Font, EastAsia = Font },
                        new FontSize { Val = "22" },
                        new FontSizeComplexScript { Val = "22" },
                        new Languages { Val = "ru-RU" })),
                    new ParagraphPropertiesDefault(new ParagraphPropertiesBaseStyle(
                        new SpacingBetweenLines { After = "0", Line = "240", LineRule = LineSpacingRuleValues.Auto }))),
                new Style(new StyleName { Val = "Normal" }, new PrimaryStyle()) { Type = StyleValues.Paragraph, StyleId = "Normal", Default = true });

            var body = new Body(
                Text("Акт №____", bold: true, size: 28, align: JustificationValues.Center),
                Text("ПРИЕМА-ПЕРЕДАЧИ", bold: true, size: 28, align: JustificationValues.Center),
                Empty(),
                Heading(act),
                Empty(),
                Text($"Настоящий акт составлен о том, что Начальник отдела продажи передает {act.Sender} а Менеджер по продажам (Продавец) " +
                     "________________________ принимает следующие товары (продукции)", bold: true, align: JustificationValues.Center),
                Empty(),
                Items(act),
                Empty(),
                Empty(),
                // Абзац об ответственности и подписи — одним блоком: на новую страницу переносятся вместе, подписи одни не остаются.
                KeepNext(Text("Менеджер по продажам (Продавец) осведомлён о том, что он принимает на себя полную материальную ответственность за ущерб и " +
                     "недостачу вверенного ему вышеуказанные имущества. При осмотре выявлено, что товары (продукции) находится в хорошем " +
                     "состоянии, отсутствуют признаки дефектов, объективных физических воздействий.", bold: true, align: JustificationValues.Center)),
                KeepNext(Empty()),
                Signatures(),
                new SectionProperties(
                    new PageSize { Width = 11906U, Height = 16838U },
                    new PageMargin { Top = 851, Right = 720U, Bottom = 851, Left = 720U, Header = 425U, Footer = 425U, Gutter = 0U }));
            main.Document = new Document(body);
        }

        return stream.ToArray();
    }

    /// <summary>«Завод → Денов» слева и «22 Октябрь 2026» справа — строкой без рамок, как в бланке.</summary>
    private static Table Heading(TransferAct act) =>
        Grid([5233, 5233], false,
            new TableRow(
                Cell(5233, Text($"{act.From} → {act.To}", bold: true)),
                Cell(5233, Text($"{act.Date.Day} {Months[act.Date.Month - 1]} {act.Date.Year}", bold: true, align: JustificationValues.Right))));

    private static Table Items(TransferAct act)
    {
        var header = new TableRow(new TableRowProperties(new TableHeader()));
        string[] titles = ["Наименование", "Кор.", "Ед. изм.", "Кол-во", "Цена за шт, SUM", "Цена, SUM"];
        for (var c = 0; c < titles.Length; c++)
        {
            header.Append(Cell(Widths[c], Text(titles[c], bold: true, align: JustificationValues.Center), center: true));
        }

        var table = Grid(Widths, true, header);
        foreach (var row in act.Rows)
        {
            table.Append(new TableRow(
                Cell(Widths[0], Text(row.Name)),
                Cell(Widths[1], Text(row.Boxes.ToString("#,##0", Ru), align: JustificationValues.Center)),
                Cell(Widths[2], Text(row.Unit, align: JustificationValues.Center)),
                Cell(Widths[3], Text(row.Quantity.ToString("#,##0.###", Ru), align: JustificationValues.Right)),
                Cell(Widths[4], Text(row.Price is { } p ? p.ToString("#,##0", Ru) : "", align: JustificationValues.Right)),
                Cell(Widths[5], Text(row.Sum is { } s ? s.ToString("#,##0", Ru) : "", align: JustificationValues.Right))));
        }

        var total = new TableCell(
            new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = Widths[..5].Sum().ToString() }, new GridSpan { Val = 5 },
                new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }),
            Text("Итого:", bold: true));
        table.Append(new TableRow(total, Cell(Widths[5], Text(act.Total.ToString("#,##0", Ru), bold: true, align: JustificationValues.Right))));
        return table;
    }

    /// <summary>«Сдал» и «Принял», линии для подписи и «(подпись)» мелко под ними.</summary>
    private static Table Signatures()
    {
        int[] widths = [4000, 2466, 4000];
        var line = new TableCellBorders(new BottomBorder { Val = BorderValues.Single, Size = 6U, Color = "000000" });
        var table = Grid(widths, false,
            new TableRow(
                Cell(widths[0], Text("Сдал", bold: true, align: JustificationValues.Center)),
                Cell(widths[1], Empty()),
                Cell(widths[2], Text("Принял", bold: true, align: JustificationValues.Center))),
            new TableRow(
                new TableRowProperties(new TableRowHeight { Val = 560U, HeightType = HeightRuleValues.AtLeast }),
                Cell(widths[0], Empty(), borders: (TableCellBorders)line.CloneNode(true)),
                Cell(widths[1], Empty()),
                Cell(widths[2], Empty(), borders: (TableCellBorders)line.CloneNode(true))),
            new TableRow(
                Cell(widths[0], Text("(подпись)", italic: true, size: 18, align: JustificationValues.Center)),
                Cell(widths[1], Empty()),
                Cell(widths[2], Text("(подпись)", italic: true, size: 18, align: JustificationValues.Center))));
        foreach (var paragraph in table.Elements<TableRow>().SkipLast(1).SelectMany(r => r.Descendants<Paragraph>()))
        {
            KeepNext(paragraph);
        }

        return table;
    }

    private static Paragraph KeepNext(Paragraph paragraph)
    {
        (paragraph.ParagraphProperties ?? paragraph.PrependChild(new ParagraphProperties())).PrependChild(new KeepNext());
        return paragraph;
    }

    private static Table Grid(int[] widths, bool borders, params TableRow[] rows)
    {
        var properties = new TableProperties(
            new TableWidth { Type = TableWidthUnitValues.Dxa, Width = widths.Sum().ToString() },
            new TableLayout { Type = TableLayoutValues.Fixed },
            new TableCellMarginDefault(
                new TopMargin { Width = "30", Type = TableWidthUnitValues.Dxa },
                new TableCellLeftMargin { Width = 70, Type = TableWidthValues.Dxa },
                new BottomMargin { Width = "30", Type = TableWidthUnitValues.Dxa },
                new TableCellRightMargin { Width = 70, Type = TableWidthValues.Dxa }));
        if (borders)
        {
            properties.InsertAfter(new TableBorders(
                Border<TopBorder>(), Border<LeftBorder>(), Border<BottomBorder>(), Border<RightBorder>(),
                Border<InsideHorizontalBorder>(), Border<InsideVerticalBorder>()), properties.GetFirstChild<TableWidth>());
        }

        var table = new Table(properties);
        var grid = new TableGrid();
        foreach (var w in widths)
        {
            grid.Append(new GridColumn { Width = w.ToString() });
        }

        table.Append(grid);
        foreach (var row in rows)
        {
            table.Append(row);
        }

        return table;
    }

    private static T Border<T>() where T : BorderType, new() => new() { Val = BorderValues.Single, Size = 4U, Color = "000000" };

    private static TableCell Cell(int width, Paragraph content, bool center = false, TableCellBorders? borders = null)
    {
        var properties = new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = width.ToString() });
        if (borders is not null)
        {
            properties.Append(borders);
        }

        properties.Append(new TableCellVerticalAlignment { Val = center ? TableVerticalAlignmentValues.Center : TableVerticalAlignmentValues.Top });
        return new TableCell(properties, content);
    }

    private static Paragraph Empty() => new();

    private static Paragraph Text(string text, bool bold = false, bool italic = false, int? size = null, JustificationValues? align = null)
    {
        var paragraph = new Paragraph();
        if (align is { } a)
        {
            paragraph.Append(new ParagraphProperties(new Justification { Val = a }));
        }

        var run = new Run();
        if (bold || italic || size is not null)
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

            if (size is { } s)
            {
                properties.Append(new FontSize { Val = s.ToString() });
            }

            run.Append(properties);
        }

        run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        paragraph.Append(run);
        return paragraph;
    }
}
