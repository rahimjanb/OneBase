using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using OneBase.Application.Abstractions;
using OneBase.Application.Files;
using OneBase.Domain.Files;

namespace OneBase.Infrastructure.Files;

public sealed record PreviewSheet(string Name, IReadOnlyList<IReadOnlyList<string>> Rows, bool Truncated);

/// <summary>
/// Предпросмотр: kind = table (CSV, XLSX — листы, первые строки), text (DOCX — абзацы, TXT), media (картинки и PDF открываются по ссылке
/// скачивания inline), none — только скачать.
/// </summary>
public sealed record FilePreview(string Kind, IReadOnlyList<PreviewSheet> Sheets, IReadOnlyList<string> Paragraphs, bool Truncated, string? Note);

/// <summary>Предпросмотр на сервере (ClosedXML и OpenXML уже есть в OneBase): браузер получает готовую таблицу или текст, без новых библиотек.</summary>
public sealed class FilePreviewService(IFileStorage storage)
{
    public const int MaxRows = 200;
    public const int MaxColumns = 40;
    public const int MaxParagraphs = 400;
    private const long MaxBytes = 20L * 1024 * 1024;

    public async Task<FilePreview> PreviewAsync(FileItem file, FileVersion version, CancellationToken ct)
    {
        var ext = FileNames.Extension(file.Name);
        if (FileNames.IsInlineSafe(file.Name))
        {
            return new FilePreview("media", [], [], false, null);
        }

        if (ext is not ("csv" or "xlsx" or "docx" or "txt"))
        {
            return new FilePreview("none", [], [], false, "Предпросмотра для этого типа нет — скачайте файл.");
        }

        if (version.SizeBytes > MaxBytes)
        {
            return new FilePreview("none", [], [], false, "Файл больше 20 МБ — предпросмотр не строится, скачайте его.");
        }

        using var buffer = new MemoryStream();
        await storage.DownloadAsync(version.ObjectKey, buffer, ct);
        buffer.Position = 0;
        try
        {
            return ext switch
            {
                "csv" => Csv(buffer),
                "xlsx" => Xlsx(buffer),
                "docx" => Docx(buffer),
                _ => Text(buffer),
            };
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new FilePreview("none", [], [], false, "Файл не удалось прочитать для предпросмотра — скачайте его.");
        }
    }

    private static FilePreview Csv(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = reader.ReadToEnd();
        var firstLine = text.Split('\n', 2)[0];
        var delimiter = firstLine.Count(c => c == ';') > firstLine.Count(c => c == ',') ? ';' : ',';
        var rows = new List<IReadOnlyList<string>>();
        var truncated = false;
        foreach (var row in ParseCsv(text, delimiter))
        {
            if (rows.Count == MaxRows)
            {
                truncated = true;
                break;
            }

            rows.Add(row.Take(MaxColumns).ToList());
        }

        return new FilePreview("table", [new PreviewSheet("CSV", rows, truncated)], [], truncated, null);
    }

    /// <summary>CSV с кавычками: «"a;b"» — одна ячейка, «""» внутри кавычек — кавычка.</summary>
    public static IEnumerable<List<string>> ParseCsv(string text, char delimiter)
    {
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    cell.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    cell.Append(c);
                }
            }
            else if (c == '"')
            {
                quoted = true;
            }
            else if (c == delimiter)
            {
                row.Add(cell.ToString());
                cell.Clear();
            }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                row.Add(cell.ToString());
                cell.Clear();
                yield return row;
                row = [];
            }
            else
            {
                cell.Append(c);
            }
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            yield return row;
        }
    }

    private static FilePreview Xlsx(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var sheets = new List<PreviewSheet>();
        foreach (var sheet in workbook.Worksheets.Take(10))
        {
            var used = sheet.RangeUsed();
            if (used is null)
            {
                sheets.Add(new PreviewSheet(sheet.Name, [], false));
                continue;
            }

            var lastRow = Math.Min(used.LastRow().RowNumber(), used.FirstRow().RowNumber() + MaxRows - 1);
            var lastColumn = Math.Min(used.LastColumn().ColumnNumber(), used.FirstColumn().ColumnNumber() + MaxColumns - 1);
            var rows = new List<IReadOnlyList<string>>();
            for (var r = used.FirstRow().RowNumber(); r <= lastRow; r++)
            {
                var cells = new List<string>();
                for (var c = used.FirstColumn().ColumnNumber(); c <= lastColumn; c++)
                {
                    cells.Add(sheet.Cell(r, c).GetFormattedString());
                }

                rows.Add(cells);
            }

            sheets.Add(new PreviewSheet(sheet.Name, rows, used.LastRow().RowNumber() > lastRow || used.LastColumn().ColumnNumber() > lastColumn));
        }

        return new FilePreview("table", sheets, [], sheets.Any(s => s.Truncated), workbook.Worksheets.Count > 10 ? "Показаны первые 10 листов." : null);
    }

    private static FilePreview Docx(Stream stream)
    {
        using var document = WordprocessingDocument.Open(stream, false);
        var paragraphs = document.MainDocumentPart?.Document?.Body?
            .Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>()
            .Select(p => p.InnerText)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Take(MaxParagraphs + 1)
            .ToList() ?? [];
        var truncated = paragraphs.Count > MaxParagraphs;
        return new FilePreview("text", [], paragraphs.Take(MaxParagraphs).ToList(), truncated, paragraphs.Count == 0 ? "В документе нет текста." : null);
    }

    private static FilePreview Text(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var buffer = new char[64 * 1024];
        var read = reader.ReadBlock(buffer, 0, buffer.Length);
        var lines = new string(buffer, 0, read).Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        return new FilePreview("text", [], lines, !reader.EndOfStream, null);
    }
}
