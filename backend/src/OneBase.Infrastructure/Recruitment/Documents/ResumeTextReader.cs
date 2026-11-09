using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using OneBase.Application.Recruitment.Documents;

namespace OneBase.Infrastructure.Recruitment.Documents;

/// <summary>
/// Текст резюме без AI:
/// <list type="bullet">
/// <item>PDF — текстовый слой по страницам (PdfPig, порядок чтения), до MaxPages страниц; мало текста или у половины страниц его нет — нужен OCR.</item>
/// <item>DOCX — абзацы, таблицы по строкам («ячейка | ячейка»), надписи; колонтитулы — в начале (там часто имя и контакты).</item>
/// <item>DOC (Word 97–2003) — свой разбор контейнера OLE и таблицы кусков текста (<see cref="WordBinary"/>).</item>
/// <item>ODT — абзацы и таблицы из content.xml; RTF — текст без управляющих слов (\uN, \'hh по кодовой странице документа); TXT — UTF-8 или 1251.</item>
/// </list>
/// Макросы и встроенные объекты не выполняются; XML читается без DTD и внешних ссылок; распакованная часть ограничена MaxPartBytes.
/// </summary>
internal sealed partial class ResumeTextReader : IResumeTextReader
{
    internal const int MaxPages = 50;
    private const int MaxPartBytes = 30 * 1024 * 1024;

    /// <summary>Меньше стольких букв и цифр на весь PDF — это скан; на странице меньше MinPageLetters — страница-картинка.</summary>
    internal const int MinPdfLetters = 80;

    private const int MinPageLetters = 20;

    static ResumeTextReader() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public DocumentText Read(byte[] content, string type)
    {
        try
        {
            return type switch
            {
                "pdf" => Pdf(content),
                "docx" => Done(Docx(content)),
                "doc" => Done(WordBinary.Extract(content)),
                "odt" => Done(Odt(content)),
                "rtf" => Done(Rtf(Decode(content))),
                "txt" => Done(Decode(content)),
                "jpeg" or "png" or "webp" => new DocumentText(null, 1, null, true),
                _ => new DocumentText(null, null, "Этот формат не читается.", false),
            };
        }
        catch (NotSupportedException ex)
        {
            return new DocumentText(null, null, ex.Message, false);
        }
        catch (Exception ex) when (ex is InvalidDataException or XmlException or ArgumentException or IndexOutOfRangeException or EndOfStreamException)
        {
            // Подробность — только своя, русская («в DOCX нет word/document.xml»); системные английские сообщения HR не показываются.
            var detail = ex.Message.Any(c => c is >= 'а' and <= 'я') ? $" ({Short(ex.Message)})" : string.Empty;
            return new DocumentText(null, null, $"Файл повреждён — текст не прочитан{detail}. Оригинал сохранён.", false);
        }
    }

    private static string Short(string s) => s.Length <= 120 ? s : s[..120];

    private static DocumentText Done(string text)
    {
        var t = Tidy(text);
        return t.Length == 0 ? new DocumentText(null, null, "В файле нет текста.", false) : new DocumentText(t, null, null, false);
    }

    /// <summary>Лишние пробелы и пустые строки — убрать; переводы строк — оставить (структура резюме).</summary>
    private static string Tidy(string text) =>
        ManyBlankLines().Replace(string.Join('\n', text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Select(l => Spaces().Replace(l, " ").TrimEnd())), "\n\n").Trim();

    private static DocumentText Pdf(byte[] bytes)
    {
        try
        {
            using var document = UglyToad.PdfPig.PdfDocument.Open(bytes);
            var sb = new StringBuilder();
            var pages = document.NumberOfPages;
            var empty = 0;
            var read = 0;
            foreach (var page in document.GetPages())
            {
                if (++read > MaxPages)
                {
                    sb.AppendLine($"(прочитаны первые {MaxPages} страниц из {pages})");
                    break;
                }

                var text = UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor.ContentOrderTextExtractor.GetText(page);
                if (text.Count(char.IsLetterOrDigit) < MinPageLetters)
                {
                    empty++;
                }

                if (!string.IsNullOrWhiteSpace(text))
                {
                    sb.AppendLine(text.Trim());
                    sb.AppendLine();
                }
            }

            var result = Tidy(sb.ToString());
            var scan = result.Count(char.IsLetterOrDigit) < MinPdfLetters || empty * 2 >= Math.Min(read, MaxPages);
            return new DocumentText(result.Length == 0 ? null : result, pages, null, scan);
        }
        catch (UglyToad.PdfPig.Exceptions.PdfDocumentEncryptedException)
        {
            throw new NotSupportedException("PDF защищён паролем — попросите кандидата прислать без пароля. Оригинал сохранён.");
        }
        catch (Exception ex) when (ex is not NotSupportedException and not OutOfMemoryException)
        {
            throw new InvalidDataException("PDF не читается");
        }
    }

    // ---------- DOCX ----------

    private static string Docx(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes, writable: false), ZipArchiveMode.Read);
        var body = zip.GetEntry("word/document.xml") ?? throw new InvalidDataException("в DOCX нет word/document.xml");
        var sb = new StringBuilder();
        foreach (var part in zip.Entries.Where(e => HeaderPart().IsMatch(e.FullName)).OrderBy(e => e.FullName, StringComparer.Ordinal))
        {
            sb.AppendLine(WordXml(part));
        }

        sb.AppendLine(WordXml(body));
        return sb.ToString();
    }

    /// <summary>Текст WordprocessingML: абзац — строка; в таблице ячейки строки — через « | », абзацы ячейки — через пробел.</summary>
    private static string WordXml(ZipArchiveEntry entry)
    {
        using var xml = XmlReader.Create(Limited(entry), Safe);
        var sb = new StringBuilder();
        var cellDepth = 0;
        var cellHasText = false;
        xml.Read();
        while (!xml.EOF)
        {
            if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "t" && !xml.IsEmptyElement)
            {
                // ReadElementContentAsString сам переходит на следующий узел — его надо разобрать, а не пропустить.
                var text = xml.ReadElementContentAsString();
                sb.Append(text);
                cellHasText |= text.Length > 0;
                continue;
            }

            if (xml.NodeType == XmlNodeType.Element)
            {
                switch (xml.LocalName)
                {
                    case "tab":
                        sb.Append('\t');
                        break;
                    case "br" or "cr":
                        sb.Append(cellDepth > 0 ? ' ' : '\n');
                        break;
                    case "tc" when !xml.IsEmptyElement:
                        cellDepth++;
                        cellHasText = false;
                        break;
                }
            }
            else if (xml.NodeType == XmlNodeType.EndElement)
            {
                Close(xml.LocalName);
            }

            xml.Read();
        }

        return sb.ToString();

        void Close(string name)
        {
            switch (name)
            {
                case "p":
                    sb.Append(cellDepth > 0 ? (cellHasText ? " " : string.Empty) : "\n");
                    break;
                case "tc":
                    cellDepth = Math.Max(0, cellDepth - 1);
                    TrimEnd(sb);
                    sb.Append(" | ");
                    break;
                case "tr":
                    TrimCellSeparator(sb);
                    sb.Append('\n');
                    break;
            }
        }
    }

    // ---------- ODT ----------

    private static string Odt(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes, writable: false), ZipArchiveMode.Read);
        var content = zip.GetEntry("content.xml") ?? throw new InvalidDataException("в ODT нет content.xml");
        using var xml = XmlReader.Create(Limited(content), Safe);
        var sb = new StringBuilder();
        var cellDepth = 0;
        while (xml.Read())
        {
            if (xml.NodeType == XmlNodeType.Text || xml.NodeType == XmlNodeType.SignificantWhitespace)
            {
                sb.Append(xml.Value);
            }
            else if (xml.NodeType == XmlNodeType.Element)
            {
                switch (xml.LocalName)
                {
                    case "s":
                        var count = int.TryParse(xml.GetAttribute("c", "urn:oasis:names:tc:opendocument:xmlns:text:1.0"), out var c) ? Math.Clamp(c, 1, 100) : 1;
                        sb.Append(' ', count);
                        break;
                    case "tab":
                        sb.Append('\t');
                        break;
                    case "line-break":
                        sb.Append(cellDepth > 0 ? ' ' : '\n');
                        break;
                    case "table-cell" when !xml.IsEmptyElement:
                        cellDepth++;
                        break;
                }
            }
            else if (xml.NodeType == XmlNodeType.EndElement)
            {
                switch (xml.LocalName)
                {
                    case "p" or "h":
                        sb.Append(cellDepth > 0 ? " " : "\n");
                        break;
                    case "table-cell":
                        cellDepth = Math.Max(0, cellDepth - 1);
                        TrimEnd(sb);
                        sb.Append(" | ");
                        break;
                    case "table-row":
                        TrimCellSeparator(sb);
                        sb.Append('\n');
                        break;
                }
            }
        }

        return sb.ToString();
    }

    // ---------- RTF ----------

    /// <summary>
    /// Текст RTF: управляющие слова убираются, \par и \line — перевод строки, \cell — « | », \row — строка; \uN — символ Unicode (следующие
    /// \ucN символов-замен пропускаются), \'hh — байт в кодовой странице документа (\ansicpgN, по умолчанию 1251); группы \*, шрифты, цвета,
    /// стили, картинки и сведения о файле — пропускаются.
    /// </summary>
    internal static string Rtf(string rtf)
    {
        var sb = new StringBuilder();
        var skipStack = new Stack<(bool Skip, int Uc)>();
        var skip = false;
        var uc = 1;
        var pendingSkip = 0;
        var codePage = 1251;
        var bytes = new List<byte>();
        var i = 0;

        void Flush()
        {
            if (bytes.Count > 0)
            {
                Encoding encoding;
                try
                {
                    encoding = Encoding.GetEncoding(codePage);
                }
                catch (Exception)
                {
                    encoding = Encoding.GetEncoding(1251);
                }

                sb.Append(encoding.GetString(bytes.ToArray()));
                bytes.Clear();
            }
        }

        while (i < rtf.Length)
        {
            var c = rtf[i];
            if (c == '{')
            {
                Flush();
                skipStack.Push((skip, uc));
                i++;
                continue;
            }

            if (c == '}')
            {
                Flush();
                (skip, uc) = skipStack.Count > 0 ? skipStack.Pop() : (false, 1);
                i++;
                continue;
            }

            if (c == '\\')
            {
                i++;
                if (i >= rtf.Length)
                {
                    break;
                }

                var n = rtf[i];
                if (n == '\'')
                {
                    if (i + 2 < rtf.Length && byte.TryParse(rtf.AsSpan(i + 1, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
                    {
                        if (pendingSkip > 0)
                        {
                            pendingSkip--;
                        }
                        else if (!skip)
                        {
                            bytes.Add(b);
                        }
                    }

                    i += 3;
                    continue;
                }

                if (n is '\\' or '{' or '}')
                {
                    Flush();
                    if (!skip)
                    {
                        sb.Append(n);
                    }

                    i++;
                    continue;
                }

                if (n == '*')
                {
                    skip = true;
                    i++;
                    continue;
                }

                if (!char.IsLetter(n))
                {
                    if (n == '~' && !skip)
                    {
                        sb.Append(' ');
                    }

                    i++;
                    continue;
                }

                var start = i;
                while (i < rtf.Length && char.IsLetter(rtf[i]))
                {
                    i++;
                }

                var word = rtf[start..i];
                var numStart = i;
                if (i < rtf.Length && (rtf[i] == '-' || char.IsDigit(rtf[i])))
                {
                    i++;
                    while (i < rtf.Length && char.IsDigit(rtf[i]))
                    {
                        i++;
                    }
                }

                int? param = i > numStart && int.TryParse(rtf.AsSpan(numStart, i - numStart), out var p) ? p : null;
                if (i < rtf.Length && rtf[i] == ' ')
                {
                    i++;
                }

                Flush();
                switch (word)
                {
                    case "ansicpg" when param is { } cp:
                        codePage = cp;
                        break;
                    case "uc" when param is { } u:
                        uc = Math.Clamp(u, 0, 10);
                        break;
                    case "u" when param is { } code:
                        if (!skip)
                        {
                            sb.Append((char)(code < 0 ? code + 65536 : code));
                        }

                        pendingSkip = uc;
                        break;
                    case "par" or "line" or "sect" or "page":
                        if (!skip)
                        {
                            sb.Append('\n');
                        }

                        break;
                    case "tab":
                        if (!skip)
                        {
                            sb.Append('\t');
                        }

                        break;
                    case "cell":
                        if (!skip)
                        {
                            sb.Append(" | ");
                        }

                        break;
                    case "row":
                        if (!skip)
                        {
                            TrimCellSeparator(sb);
                            sb.Append('\n');
                        }

                        break;
                    case "fonttbl" or "colortbl" or "stylesheet" or "info" or "pict" or "object" or "header" or "footer" or "listtable" or "listoverridetable"
                        or "themedata" or "datastore" or "latentstyles" or "generator" or "xmlnstbl" or "rsidtbl":
                        skip = true;
                        break;
                }

                continue;
            }

            if (c is '\r' or '\n')
            {
                i++;
                continue;
            }

            if (pendingSkip > 0)
            {
                pendingSkip--;
                i++;
                continue;
            }

            Flush();
            if (!skip)
            {
                sb.Append(c);
            }

            i++;
        }

        Flush();
        return sb.ToString();
    }

    // ---------- Общее ----------

    private static readonly XmlReaderSettings Safe = new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreComments = true };

    /// <summary>Распакованная часть — не больше MaxPartBytes (защита от «zip-бомбы»).</summary>
    private static Stream Limited(ZipArchiveEntry entry)
    {
        if (entry.Length > MaxPartBytes)
        {
            throw new InvalidDataException("слишком большая часть документа");
        }

        var buffer = new MemoryStream();
        using var source = entry.Open();
        var chunk = new byte[81920];
        int read;
        while ((read = source.Read(chunk, 0, chunk.Length)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxPartBytes)
            {
                throw new InvalidDataException("слишком большая часть документа");
            }
        }

        buffer.Position = 0;
        return buffer;
    }

    private static void TrimEnd(StringBuilder sb)
    {
        while (sb.Length > 0 && sb[^1] == ' ')
        {
            sb.Length--;
        }
    }

    private static void TrimCellSeparator(StringBuilder sb)
    {
        TrimEnd(sb);
        if (sb.Length >= 2 && sb[^1] == '|' && sb[^2] == ' ')
        {
            sb.Length -= 2;
        }

        TrimEnd(sb);
    }

    /// <summary>UTF-8 (с BOM или без); не UTF-8 — Windows-1251.</summary>
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

    [GeneratedRegex(@"^word/(header|footer)\d*\.xml$")]
    private static partial Regex HeaderPart();

    [GeneratedRegex(@"[ \t ]{2,}")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ManyBlankLines();
}
