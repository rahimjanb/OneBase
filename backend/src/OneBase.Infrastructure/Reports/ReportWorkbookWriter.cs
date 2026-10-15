using System.Globalization;
using System.Text.Json;
using ClosedXML.Excel;
using OneBase.Application.Reports;

namespace OneBase.Infrastructure.Reports;

/// <summary>
/// Отчёт консультанта → xlsx (ClosedXML). На каждом листе: название отчёта, когда сформирован и источники, шапка таблицы
/// (закреплена, с автофильтром), строки с форматами чисел, итоговая строка формулами СУММ, пояснение под таблицей.
/// Проценты хранятся в процентах (27,8) — в ячейку идёт доля 0,278 с форматом «0,0 %», чтобы Excel считал их числами.
/// </summary>
public sealed class ReportWorkbookWriter : IReportWorkbookWriter
{
    /// <summary>Узбекистан — UTC+5 без перехода на летнее время.</summary>
    private static readonly TimeSpan CompanyOffset = TimeSpan.FromHours(5);

    private const int HeaderRow = 4;
    private const int MaxCellText = 32_000;

    public byte[] Write(AiReportContent report, DateTimeOffset createdAt)
    {
        using var workbook = new XLWorkbook();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stamp = $"Сформировано в OneBase {createdAt.ToOffset(CompanyOffset):dd.MM.yyyy HH:mm} (Ташкент)"
                    + (report.Sources.Count > 0 ? $" · источники: {string.Join("; ", report.Sources)}" : "");
        foreach (var sheet in report.Sheets)
        {
            var ws = workbook.Worksheets.Add(SheetName(sheet.Name, names));
            WriteSheet(ws, report.Title, stamp, sheet);
        }

        if (report.Sheets.Count == 0)
        {
            workbook.Worksheets.Add("Отчёт").Cell(1, 1).Value = report.Title;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteSheet(IXLWorksheet ws, string title, string stamp, AiReportSheet sheet)
    {
        var columns = Math.Max(sheet.Columns.Count, 1);
        ws.Cell(1, 1).Value = title;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(2, 1).Value = stamp;
        ws.Cell(2, 1).Style.Font.Italic = true;
        ws.Cell(2, 1).Style.Font.FontColor = XLColor.Gray;

        for (var c = 0; c < sheet.Columns.Count; c++)
        {
            var cell = ws.Cell(HeaderRow, c + 1);
            cell.Value = sheet.Columns[c].Header;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EEF8");
            cell.Style.Alignment.WrapText = true;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }

        var row = HeaderRow + 1;
        foreach (var values in sheet.Rows)
        {
            for (var c = 0; c < sheet.Columns.Count; c++)
            {
                var value = c < values.Count ? values[c] : default;
                SetValue(ws.Cell(row, c + 1), value, sheet.Columns[c].Format);
            }

            row++;
        }

        var lastData = row - 1;
        if (sheet.Total && sheet.Rows.Count > 0)
        {
            var labelled = false;
            for (var c = 0; c < sheet.Columns.Count; c++)
            {
                var cell = ws.Cell(row, c + 1);
                var format = sheet.Columns[c].Format;
                if (ReportFormats.IsSummable(format))
                {
                    var letter = cell.WorksheetColumn().ColumnLetter();
                    cell.FormulaA1 = $"SUM({letter}{HeaderRow + 1}:{letter}{lastData})";
                    cell.Style.NumberFormat.Format = NumberFormat(format);
                }
                else if (!labelled)
                {
                    cell.Value = "Итого";
                    labelled = true;
                }

                cell.Style.Font.Bold = true;
                cell.Style.Border.TopBorder = XLBorderStyleValues.Thin;
            }

            row++;
        }

        if (sheet.Note is { Length: > 0 } note)
        {
            ws.Cell(row + 1, 1).Value = Clip(note);
            ws.Cell(row + 1, 1).Style.Font.Italic = true;
            ws.Cell(row + 1, 1).Style.Font.FontColor = XLColor.Gray;
        }

        if (sheet.Columns.Count > 0)
        {
            ws.SheetView.FreezeRows(HeaderRow);
            ws.Range(HeaderRow, 1, Math.Max(lastData, HeaderRow), columns).SetAutoFilter();
            // Ширина — по шапке и данным (без строк названия и пояснения: они длинные и растянули бы первую колонку).
            for (var c = 1; c <= columns; c++)
            {
                var column = ws.Column(c);
                column.AdjustToContents(HeaderRow, Math.Max(lastData, HeaderRow));
                column.Width = Math.Clamp(column.Width + 2, 10, 60);
            }
        }
    }

    private static void SetValue(IXLCell cell, JsonElement value, string format)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Number:
                var number = value.TryGetDecimal(out var d) ? d : (decimal)value.GetDouble();
                cell.Value = format == ReportFormats.Percent ? number / 100m : number;
                cell.Style.NumberFormat.Format = NumberFormat(ReportFormats.IsNumeric(format) ? format : ReportFormats.Number);
                break;
            case JsonValueKind.String:
                var text = value.GetString() ?? string.Empty;
                if (format == ReportFormats.Date && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date))
                {
                    cell.Value = date;
                    cell.Style.NumberFormat.Format = date.TimeOfDay == TimeSpan.Zero ? "dd.mm.yyyy" : "dd.mm.yyyy hh:mm";
                }
                else
                {
                    cell.Value = Clip(text);
                }

                break;
            case JsonValueKind.True or JsonValueKind.False:
                cell.Value = value.GetBoolean() ? "да" : "нет";
                break;
        }
    }

    private static string NumberFormat(string format) => format switch
    {
        ReportFormats.Integer or ReportFormats.Kg or ReportFormats.Money => "#,##0",
        ReportFormats.Percent => "0.0%",
        _ => "#,##0.##",
    };

    /// <summary>Имя листа Excel: до 31 знака, без []:*?/\, уникальное в книге.</summary>
    internal static string SheetName(string name, ISet<string> used)
    {
        var clean = new string((name ?? string.Empty).Where(ch => ch is not ('[' or ']' or ':' or '*' or '?' or '/' or '\\')).ToArray()).Trim().Trim('\'');
        if (clean.Length == 0)
        {
            clean = "Лист";
        }

        clean = clean.Length > 31 ? clean[..31] : clean;
        var result = clean;
        for (var i = 2; !used.Add(result); i++)
        {
            var suffix = $" ({i})";
            result = (clean.Length + suffix.Length > 31 ? clean[..(31 - suffix.Length)] : clean) + suffix;
        }

        return result;
    }

    private static string Clip(string text) => text.Length <= MaxCellText ? text : text[..MaxCellText] + "…";
}
