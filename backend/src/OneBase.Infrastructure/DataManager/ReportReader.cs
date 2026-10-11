using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using OneBase.Application.Abstractions;
using OneBase.Application.DataManager;
using OneBase.Infrastructure.Files;

namespace OneBase.Infrastructure.DataManager;

/// <summary>
/// Таблицы отчётов для AI Data Manager: .xlsx (ClosedXML, значения ячеек, а не их отображение — без потери знаков) и .csv / .txt
/// (UTF-8 или Windows-1251, разделитель — «;», «,» или табуляция).
/// </summary>
internal sealed class ReportReader(DataManagerOptions options) : IReportReader
{
    private const int MaxSheets = 20;

    static ReportReader() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public IReadOnlyCollection<string> Extensions { get; } = [".xlsx", ".xlsm", ".csv", ".txt"];

    public ReportDocument Read(Stream content, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        try
        {
            return extension switch
            {
                ".xlsx" or ".xlsm" => Excel(content),
                ".csv" or ".txt" => Csv(content),
                ".xls" => throw new NotSupportedException("Старый формат .xls не читается: сохраните файл в Excel как .xlsx."),
                _ => throw new NotSupportedException($"Формат {extension} не поддерживается. Можно: {string.Join(", ", Extensions)}."),
            };
        }
        catch (Exception ex) when (ex is not NotSupportedException)
        {
            throw new NotSupportedException("Файл не удалось прочитать: возможно, он повреждён или защищён паролем.");
        }
    }

    private ReportDocument Excel(Stream stream)
    {
        using var book = new XLWorkbook(stream);
        var sheets = new List<ReportSheet>();
        foreach (var sheet in book.Worksheets.Take(MaxSheets))
        {
            var used = sheet.RangeUsed(XLCellsUsedOptions.Contents);
            if (used is null)
            {
                sheets.Add(new ReportSheet(sheet.Name, [], 0, 0, false));
                continue;
            }

            var lastRow = used.LastRow().RowNumber();
            var lastColumn = used.LastColumn().ColumnNumber();
            var rowLimit = Math.Min(lastRow, options.MaxRowsPerSheet);
            var columnLimit = Math.Min(lastColumn, options.MaxColumns);
            var rows = new List<IReadOnlyList<string>>(rowLimit);
            for (var r = 1; r <= rowLimit; r++)
            {
                var cells = new string[columnLimit];
                for (var c = 1; c <= columnLimit; c++)
                {
                    cells[c - 1] = Value(sheet.Cell(r, c));
                }

                rows.Add(cells);
            }

            sheets.Add(new ReportSheet(sheet.Name, rows, lastRow, lastColumn, lastRow > rowLimit || lastColumn > columnLimit));
        }

        return new ReportDocument(sheets);
    }

    /// <summary>Значение ячейки: число — инвариантно и полностью (формат «0» в Excel скрывает дробь), дата — ГГГГ-ММ-ДД.</summary>
    private static string Value(IXLCell cell)
    {
        var value = cell.Value;
        if (value.IsBlank)
        {
            return string.Empty;
        }

        if (value.IsNumber)
        {
            return value.GetNumber().ToString("0.###########", CultureInfo.InvariantCulture);
        }

        if (value.IsDateTime)
        {
            return value.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (value.IsBoolean)
        {
            return value.GetBoolean() ? "да" : "нет";
        }

        if (value.IsError)
        {
            return "#ОШИБКА";
        }

        return value.ToString(CultureInfo.InvariantCulture).Trim();
    }

    private ReportDocument Csv(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var text = Decode(buffer.ToArray());
        var firstLine = text.Split('\n', 2)[0];
        var delimiter = new[] { ';', ',', '\t' }.OrderByDescending(d => firstLine.Count(c => c == d)).First();
        var rows = new List<IReadOnlyList<string>>();
        var total = 0;
        var columns = 0;
        foreach (var row in FilePreviewService.ParseCsv(text, delimiter))
        {
            total++;
            columns = Math.Max(columns, row.Count);
            if (rows.Count < options.MaxRowsPerSheet)
            {
                rows.Add(row.Take(options.MaxColumns).Select(c => c.Trim()).ToList());
            }
        }

        return new ReportDocument([new ReportSheet("CSV", rows, total, columns, total > rows.Count || columns > options.MaxColumns)]);
    }

    private static string Decode(byte[] bytes)
    {
        try
        {
            return new UTF8Encoding(false, true).GetString(bytes).TrimStart('﻿');
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding(1251).GetString(bytes);
        }
    }
}
