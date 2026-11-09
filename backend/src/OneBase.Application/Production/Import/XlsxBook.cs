using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace OneBase.Application.Production.Import;

/// <summary>Вид ячейки Excel: число, текст, ошибка формулы (#REF!, #DIV/0!…), логическое значение.</summary>
public enum XlsxCellKind
{
    Number,
    Text,
    Error,
    Bool,
}

/// <summary>
/// Ячейка как в файле: адрес, вид, исходная запись значения (Raw — как в XML, без перевода в double), формула (null — значение введено; «shared» —
/// часть общей формулы без текста), число — <see cref="ExcelNumbers.Parse"/>, текст или код ошибки.
/// </summary>
public sealed record XlsxCell(string Ref, int Row, int Col, XlsxCellKind Kind, string? Raw, string? Formula, decimal? Number, string? Text)
{
    public bool IsFormula => Formula is not null;
}

public sealed record XlsxSheet(string Name, bool Hidden, IReadOnlyDictionary<(int Row, int Col), XlsxCell> Cells, int MaxRow, int MaxCol)
{
    public XlsxCell? this[int row, int col] => Cells.GetValueOrDefault((row, col));

    /// <summary>Текст ячейки без краёв (число — исходной записью), пусто — null.</summary>
    public string? Text(int row, int col) => this[row, col] switch
    {
        { Kind: XlsxCellKind.Text, Text: { } t } when t.Trim().Length > 0 => t.Trim(),
        { Kind: XlsxCellKind.Number, Number: { } n } => ExcelNumbers.Format(n),
        _ => null,
    };
}

public sealed class XlsxFormatException(string message) : Exception(message);

/// <summary>
/// Чтение книги .xlsx без сторонних библиотек: листы (с признаком скрытого), значения ячеек как в файле, формулы и их сохранённые значения,
/// ошибки. Даты не распознаются (рецептуры их не содержат). Защита от «zip-бомбы» — пределы размера частей и числа ячеек.
/// </summary>
public static class XlsxBook
{
    public const long MaxPartBytes = 200L * 1024 * 1024;
    public const int MaxCells = 3_000_000;

    private const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string PackageRels = "http://schemas.openxmlformats.org/package/2006/relationships";

    public static IReadOnlyList<XlsxSheet> Read(Stream stream)
    {
        ZipArchive zip;
        try
        {
            zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException)
        {
            throw new XlsxFormatException("Это не файл Excel .xlsx.");
        }

        using (zip)
        {
            var workbook = Part(zip, "xl/workbook.xml") ?? throw new XlsxFormatException("В файле нет книги Excel (xl/workbook.xml).");
            var shared = SharedStrings(zip);
            var targets = Relationships(zip);
            var sheets = new List<XlsxSheet>();
            var cells = 0;
            using var reader = XmlReader.Create(new MemoryStream(workbook), Settings());
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "sheet" || reader.NamespaceURI != Main)
                {
                    continue;
                }

                var name = reader.GetAttribute("name") ?? "";
                var hidden = reader.GetAttribute("state") is "hidden" or "veryHidden";
                var id = reader.GetAttribute("id", RelNs);
                if (id is null || !targets.TryGetValue(id, out var target))
                {
                    continue;
                }

                var data = Part(zip, target) ?? throw new XlsxFormatException($"Лист «{name}» не найден в файле.");
                var sheet = Sheet(name, hidden, data, shared, ref cells);
                sheets.Add(sheet);
            }

            return sheets;
        }
    }

    private static XmlReaderSettings Settings() => new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreWhitespace = false };

    private static byte[]? Part(ZipArchive zip, string path)
    {
        var entry = zip.GetEntry(path) ?? zip.Entries.FirstOrDefault(e => string.Equals(e.FullName, path, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return null;
        }

        if (entry.Length > MaxPartBytes)
        {
            throw new XlsxFormatException("Файл Excel слишком большой.");
        }

        using var s = entry.Open();
        using var ms = new MemoryStream();
        var buffer = new byte[81920];
        long total = 0;
        int n;
        while ((n = s.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += n;
            if (total > MaxPartBytes)
            {
                throw new XlsxFormatException("Файл Excel слишком большой.");
            }

            ms.Write(buffer, 0, n);
        }

        return ms.ToArray();
    }

    /// <summary>Связи книги: r:id листа → путь части в архиве.</summary>
    private static Dictionary<string, string> Relationships(ZipArchive zip)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (Part(zip, "xl/_rels/workbook.xml.rels") is not { } rels)
        {
            return result;
        }

        using var reader = XmlReader.Create(new MemoryStream(rels), Settings());
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "Relationship" && reader.NamespaceURI == PackageRels
                && reader.GetAttribute("Id") is { } id && reader.GetAttribute("Target") is { } target)
            {
                target = target.Replace('\\', '/');
                result[id] = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
            }
        }

        return result;
    }

    /// <summary>Общие строки: текст &lt;t&gt; и фрагментов &lt;r&gt;&lt;t&gt;, без фонетических подсказок &lt;rPh&gt;.</summary>
    private static List<string> SharedStrings(ZipArchive zip)
    {
        var result = new List<string>();
        if (Part(zip, "xl/sharedStrings.xml") is not { } data)
        {
            return result;
        }

        using var reader = XmlReader.Create(new MemoryStream(data), Settings());
        StringBuilder? current = null;
        var phonetic = 0;
        reader.Read();
        while (!reader.EOF)
        {
            if (reader.NodeType == XmlNodeType.Element && reader.NamespaceURI == Main)
            {
                switch (reader.LocalName)
                {
                    case "si" when reader.IsEmptyElement:
                        result.Add("");
                        break;
                    case "si":
                        current = new StringBuilder();
                        break;
                    case "rPh" when !reader.IsEmptyElement:
                        phonetic++;
                        break;
                    case "t" when phonetic == 0 && current is not null:
                        // ReadElementContentAsString уже переводит на следующий узел — без лишнего Read().
                        current.Append(reader.ReadElementContentAsString());
                        continue;
                }
            }
            else if (reader.NodeType == XmlNodeType.EndElement && reader.NamespaceURI == Main)
            {
                if (reader.LocalName == "rPh")
                {
                    phonetic--;
                }
                else if (reader.LocalName == "si" && current is not null)
                {
                    result.Add(current.ToString());
                    current = null;
                }
            }

            reader.Read();
        }

        return result;
    }

    private static XlsxSheet Sheet(string name, bool hidden, byte[] data, List<string> shared, ref int total)
    {
        var cells = new Dictionary<(int, int), XlsxCell>();
        int maxRow = 0, maxCol = 0, row = 0, col = 0;
        using var reader = XmlReader.Create(new MemoryStream(data), Settings());
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.NamespaceURI != Main)
            {
                continue;
            }

            if (reader.LocalName == "row")
            {
                row = int.TryParse(reader.GetAttribute("r"), NumberStyles.None, CultureInfo.InvariantCulture, out var r) ? r : row + 1;
                col = 0;
                continue;
            }

            if (reader.LocalName != "c")
            {
                continue;
            }

            var reference = reader.GetAttribute("r");
            if (reference is not null && Address(reference) is { } address)
            {
                (row, col) = address;
            }
            else
            {
                col++;
                reference = Ref(row, col);
            }

            var type = reader.GetAttribute("t");
            string? formula = null, value = null, inline = null;
            if (!reader.IsEmptyElement)
            {
                var depth = reader.Depth;
                reader.Read();
                while (!reader.EOF && !(reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth))
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.NamespaceURI == Main)
                    {
                        switch (reader.LocalName)
                        {
                            case "f":
                                var sharedFormula = reader.GetAttribute("t") == "shared";
                                var text = reader.ReadElementContentAsString();
                                formula = text.Length > 0 ? text : sharedFormula ? "shared" : "";
                                continue;
                            case "v":
                                value = reader.ReadElementContentAsString();
                                continue;
                            case "is":
                                inline = InlineText(reader);
                                continue;
                        }
                    }

                    reader.Read();
                }
            }

            var cell = Cell(reference, row, col, type, value, inline, formula, shared);
            if (cell is null)
            {
                continue;
            }

            if (++total > MaxCells)
            {
                throw new XlsxFormatException("В файле Excel слишком много ячеек.");
            }

            cells[(row, col)] = cell;
            maxRow = Math.Max(maxRow, row);
            maxCol = Math.Max(maxCol, col);
        }

        return new XlsxSheet(name, hidden, cells, maxRow, maxCol);
    }

    /// <summary>Текст &lt;is&gt; без фонетических подсказок; читатель — на узле после &lt;/is&gt;.</summary>
    private static string InlineText(XmlReader reader)
    {
        var sb = new StringBuilder();
        if (reader.IsEmptyElement)
        {
            reader.Read();
            return "";
        }

        var depth = reader.Depth;
        var phonetic = 0;
        reader.Read();
        while (!reader.EOF && !(reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth))
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "rPh" && !reader.IsEmptyElement)
            {
                phonetic++;
            }
            else if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "rPh")
            {
                phonetic--;
            }
            else if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "t" && phonetic == 0)
            {
                sb.Append(reader.ReadElementContentAsString());
                continue;
            }

            reader.Read();
        }

        reader.Read(); // за </is>
        return sb.ToString();
    }

    private static XlsxCell? Cell(string reference, int row, int col, string? type, string? value, string? inline, string? formula, List<string> shared)
    {
        switch (type)
        {
            case "s":
                return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var i) && i >= 0 && i < shared.Count
                    ? new XlsxCell(reference, row, col, XlsxCellKind.Text, value, formula, null, shared[i])
                    : null;
            case "inlineStr":
                return new XlsxCell(reference, row, col, XlsxCellKind.Text, inline, formula, null, inline ?? "");
            case "str":
                return value is null && formula is null ? null : new XlsxCell(reference, row, col, XlsxCellKind.Text, value, formula, null, value ?? "");
            case "e":
                return new XlsxCell(reference, row, col, XlsxCellKind.Error, value, formula, null, value);
            case "b":
                return new XlsxCell(reference, row, col, XlsxCellKind.Bool, value, formula, null, value == "1" ? "TRUE" : "FALSE");
            default:
                if (value is null)
                {
                    // Формула без сохранённого значения — пусто, но формула есть: видно в проверке.
                    return formula is null ? null : new XlsxCell(reference, row, col, XlsxCellKind.Text, null, formula, null, "");
                }

                return ExcelNumbers.Parse(value) is { } number
                    ? new XlsxCell(reference, row, col, XlsxCellKind.Number, value, formula, number, null)
                    : new XlsxCell(reference, row, col, XlsxCellKind.Text, value, formula, null, value);
        }
    }

    /// <summary>«AB12» → (12, 28); не адрес — null.</summary>
    public static (int Row, int Col)? Address(string reference)
    {
        var col = 0;
        var i = 0;
        while (i < reference.Length && reference[i] is >= 'A' and <= 'Z')
        {
            col = col * 26 + reference[i] - 'A' + 1;
            i++;
        }

        return i > 0 && i < reference.Length && int.TryParse(reference.AsSpan(i), NumberStyles.None, CultureInfo.InvariantCulture, out var row) && row > 0
            ? (row, col)
            : null;
    }

    public static string Column(int col)
    {
        var s = "";
        while (col > 0)
        {
            var r = (col - 1) % 26;
            s = (char)('A' + r) + s;
            col = (col - 1) / 26;
        }

        return s;
    }

    public static string Ref(int row, int col) => Column(col) + row.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Числа Excel без ошибок округления: Excel хранит double, а показывает не больше 15 значащих цифр. Значение из файла («0.32400000000000001»)
/// переводится в кратчайшую запись того же double («0.324») и, если в ней больше 15 значащих цифр, — в 15 цифр, как в Excel. Дальше — только
/// decimal.
/// </summary>
public static class ExcelNumbers
{
    public const int SignificantDigits = 15;

    public static decimal? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || !double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) || !double.IsFinite(d))
        {
            return null;
        }

        var shortest = d.ToString("R", CultureInfo.InvariantCulture);
        if (Significant(shortest) > SignificantDigits)
        {
            shortest = d.ToString("G" + SignificantDigits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }

        return decimal.TryParse(shortest, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? Normalize(value) : null;
    }

    /// <summary>Без лишних нулей в конце: 641.650 → 641.65.</summary>
    public static decimal Normalize(decimal value) => value / 1.000000000000000000000000000000000m;

    /// <summary>Знаков после запятой у значения (без хвостовых нулей).</summary>
    public static int Scale(decimal value) => (decimal.GetBits(Normalize(value))[3] >> 16) & 0xFF;

    /// <summary>Запись числа для людей и сравнения: инвариантная, без хвостовых нулей и без экспоненты.</summary>
    public static string Format(decimal value) => Normalize(value).ToString(CultureInfo.InvariantCulture);

    private static int Significant(string s)
    {
        var mantissa = s.Split('E', 'e')[0].TrimStart('-').Replace(".", "", StringComparison.Ordinal).TrimStart('0');
        return mantissa.Length;
    }
}
