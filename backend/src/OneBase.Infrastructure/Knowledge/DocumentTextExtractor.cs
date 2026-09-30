using System.IO.Compression;
using System.Text;
using System.Xml;
using ClosedXML.Excel;
using OneBase.Application.Abstractions;

namespace OneBase.Infrastructure.Knowledge;

/// <summary>
/// Текст из документов для базы знаний: .txt, .md, .csv (UTF-8 или Windows-1251), .xlsx (строка таблицы — «заголовок: значение»),
/// .docx (абзацы из word/document.xml). PDF и старые .doc/.xls не читаются — нужна конвертация.
/// </summary>
internal sealed class DocumentTextExtractor : IDocumentTextExtractor
{
    private const int MaxRowsPerSheet = 20_000;

    static DocumentTextExtractor() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public IReadOnlyCollection<string> Extensions { get; } = [".txt", ".md", ".csv", ".xlsx", ".docx"];

    public async Task<string> ExtractAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        return extension switch
        {
            ".txt" or ".md" or ".csv" => Decode(buffer.ToArray()),
            ".xlsx" => Excel(buffer),
            ".docx" => Word(buffer),
            ".pdf" => throw new NotSupportedException("PDF пока не читается: сохраните документ как .docx или .txt и загрузите снова."),
            ".doc" or ".xls" => throw new NotSupportedException("Старый формат Office не читается: сохраните файл как .docx или .xlsx."),
            _ => throw new NotSupportedException($"Формат {extension} не поддерживается. Можно: {string.Join(", ", Extensions)}."),
        };
    }

    /// <summary>UTF-8 (с BOM или без); если байты не UTF-8 — Windows-1251 (выгрузки из 1С и Excel).</summary>
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

    private static string Excel(Stream stream)
    {
        using var book = new XLWorkbook(stream);
        var sb = new StringBuilder();
        foreach (var sheet in book.Worksheets)
        {
            var used = sheet.RangeUsed();
            if (used is null)
            {
                continue;
            }

            sb.AppendLine($"Лист «{sheet.Name}»");
            var rows = used.RowsUsed().ToList();
            var header = rows[0].Cells().Select(c => c.GetFormattedString().Trim()).ToList();
            foreach (var row in rows.Skip(1).Take(MaxRowsPerSheet))
            {
                var cells = row.Cells().Select(c => c.GetFormattedString().Trim()).ToList();
                var parts = cells
                    .Select((v, i) => (Name: i < header.Count && header[i].Length > 0 ? header[i] : $"Столбец {i + 1}", Value: v))
                    .Where(x => x.Value.Length > 0)
                    .Select(x => $"{x.Name}: {x.Value}");
                var line = string.Join("; ", parts);
                if (line.Length > 0)
                {
                    sb.AppendLine(line);
                }
            }

            if (rows.Count - 1 > MaxRowsPerSheet)
            {
                sb.AppendLine($"(лист обрезан: загружены первые {MaxRowsPerSheet} строк из {rows.Count - 1})");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string Word(Stream stream)
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        var entry = zip.GetEntry("word/document.xml") ?? throw new NotSupportedException("Файл .docx повреждён: нет word/document.xml.");
        using var xml = XmlReader.Create(entry.Open(), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var sb = new StringBuilder();
        while (xml.Read())
        {
            if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "t")
            {
                sb.Append(xml.ReadElementContentAsString());
                continue;
            }

            if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "tab")
            {
                sb.Append('\t');
            }
            else if (xml.NodeType == XmlNodeType.Element && xml.LocalName is "br" or "cr")
            {
                sb.Append('\n');
            }
            else if (xml.NodeType == XmlNodeType.EndElement && xml.LocalName == "p")
            {
                sb.Append('\n');
            }
            else if (xml.NodeType == XmlNodeType.EndElement && xml.LocalName == "tc")
            {
                sb.Append(" | ");
            }
        }

        return sb.ToString();
    }
}
