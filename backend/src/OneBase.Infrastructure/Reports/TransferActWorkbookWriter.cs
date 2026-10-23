using ClosedXML.Excel;
using OneBase.Application.Sales.Stock;

namespace OneBase.Infrastructure.Reports;

/// <summary>
/// «Акт приёма-передачи» автозаказа в Excel — по бланку Linko: «Акт №____ / ПРИЕМА-ПЕРЕДАЧИ», «Завод → регион» и дата, абзац «Настоящий
/// акт составлен о том, что…», таблица «Наименование · Кор. · Ед. изм. · Кол-во · Цена за шт, SUM · Цена, SUM» с итогом, абзац о
/// материальной ответственности и подписи «Сдал» / «Принял». Числа — числами (их можно править и считать), печать — A4 книжная в ширину
/// страницы. Текст бланка — по-русски, как в Linko.
/// </summary>
public sealed class TransferActWorkbookWriter : ITransferActWriter
{
    private const int Columns = 6;
    private const string Integer = "#,##0";
    private const string Quantity = "#,##0.###";

    private static readonly string[] Months =
        ["Январь", "Февраль", "Март", "Апрель", "Май", "Июнь", "Июль", "Август", "Сентябрь", "Октябрь", "Ноябрь", "Декабрь"];

    private static readonly double[] Widths = [52, 8, 9, 11, 15, 16];

    public byte[] Write(TransferAct act)
    {
        using var workbook = new XLWorkbook();
        workbook.Properties.Title = $"Акт приёма-передачи {act.From} → {act.To} {act.Date:dd.MM.yyyy}";
        workbook.Properties.Author = "OneBase";
        var ws = workbook.Worksheets.Add("Акт");
        ws.Style.Font.FontName = "Calibri";
        ws.Style.Font.FontSize = 11;
        for (var c = 0; c < Columns; c++)
        {
            ws.Column(c + 1).Width = Widths[c];
        }

        var row = 1;
        Merged(ws, row++, "Акт №____", bold: true, size: 14, center: true);
        Merged(ws, row++, "ПРИЕМА-ПЕРЕДАЧИ", bold: true, size: 14, center: true);
        row++;

        Put(ws.Range(row, 1, row, 3), $"{act.From} → {act.To}");
        Put(ws.Range(row, 4, row, Columns), $"{act.Date.Day} {Months[act.Date.Month - 1]} {act.Date.Year}");
        ws.Range(row, 1, row, Columns).Style.Font.Bold = true;
        ws.Range(row, 4, row, Columns).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        row += 2;

        Merged(ws, row++, $"Настоящий акт составлен о том, что Начальник отдела продажи передает {act.Sender} а Менеджер по продажам (Продавец) " +
                           "________________________ принимает следующие товары (продукции)", bold: true, center: true, lines: 3);
        row++;

        var top = row;
        string[] titles = ["Наименование", "Кор.", "Ед. изм.", "Кол-во", "Цена за шт, SUM", "Цена, SUM"];
        for (var c = 0; c < Columns; c++)
        {
            ws.Cell(row, c + 1).Value = titles[c];
        }

        var header = ws.Range(row, 1, row, Columns);
        header.Style.Font.Bold = true;
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        header.Style.Alignment.WrapText = true;
        row++;

        foreach (var item in act.Rows)
        {
            ws.Cell(row, 1).Value = item.Name;
            ws.Cell(row, 1).Style.Alignment.WrapText = true;
            ws.Cell(row, 2).Value = item.Boxes;
            ws.Cell(row, 3).Value = item.Unit;
            ws.Cell(row, 4).Value = item.Quantity;
            if (item.Price is { } price)
            {
                ws.Cell(row, 5).Value = price;
            }

            if (item.Sum is { } sum)
            {
                ws.Cell(row, 6).Value = sum;
            }

            row++;
        }

        Put(ws.Range(row, 1, row, Columns - 1), "Итого:");
        ws.Cell(row, Columns).Value = act.Total;
        ws.Range(row, 1, row, Columns).Style.Font.Bold = true;

        var table = ws.Range(top, 1, row, Columns);
        table.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        table.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        table.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ws.Range(top + 1, 2, row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range(top + 1, 4, row, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Range(top + 1, 2, row, 2).Style.NumberFormat.Format = Integer;
        ws.Range(top + 1, 4, row, 4).Style.NumberFormat.Format = Quantity;
        ws.Range(top + 1, 5, row, 6).Style.NumberFormat.Format = Integer;
        row += 3;

        Merged(ws, row++, "Менеджер по продажам (Продавец) осведомлён о том, что он принимает на себя полную материальную ответственность за ущерб и " +
                           "недостачу вверенного ему вышеуказанные имущества. При осмотре выявлено, что товары (продукции) находится в хорошем " +
                           "состоянии, отсутствуют признаки дефектов, объективных физических воздействий.", bold: true, center: true, lines: 4);
        row++;

        // «Сдал» — слева, «Принял» — справа; под ними линия для подписи и «(подпись)».
        ws.Cell(row, 1).Value = "Сдал";
        Put(ws.Range(row, 4, row, Columns), "Принял");
        ws.Range(row, 1, row, Columns).Style.Font.Bold = true;
        ws.Range(row, 1, row, Columns).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        row++;
        ws.Row(row).Height = 28;
        ws.Cell(row, 1).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        ws.Range(row, 4, row, Columns).Merge().Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        row++;
        ws.Cell(row, 1).Value = "(подпись)";
        Put(ws.Range(row, 4, row, Columns), "(подпись)");
        ws.Range(row, 1, row, Columns).Style.Font.Italic = true;
        ws.Range(row, 1, row, Columns).Style.Font.FontSize = 9;
        ws.Range(row, 1, row, Columns).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
        ws.PageSetup.PageOrientation = XLPageOrientation.Portrait;
        ws.PageSetup.FitToPages(1, 0);
        ws.PageSetup.Margins.Left = ws.PageSetup.Margins.Right = 0.5;
        ws.PageSetup.Margins.Top = ws.PageSetup.Margins.Bottom = 0.6;
        ws.PageSetup.PrintAreas.Add(1, 1, row, Columns);
        ws.PageSetup.SetRowsToRepeatAtTop(top, top);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>Объединить ячейки и записать текст в первую (остальные ячейки объединения пустые).</summary>
    private static IXLRange Put(IXLRange range, string text)
    {
        range.Merge();
        range.FirstCell().Value = text;
        return range;
    }

    /// <summary>Строка текста на всю ширину акта; lines — сколько строк высоты занять под перенос.</summary>
    private static void Merged(IXLWorksheet ws, int row, string text, bool bold = false, double size = 11, bool center = false, int lines = 1)
    {
        var range = Put(ws.Range(row, 1, row, Columns), text);
        range.Style.Font.Bold = bold;
        range.Style.Font.FontSize = size;
        range.Style.Alignment.WrapText = true;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        if (center)
        {
            range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        if (lines > 1)
        {
            ws.Row(row).Height = 15 * lines;
        }
    }
}
