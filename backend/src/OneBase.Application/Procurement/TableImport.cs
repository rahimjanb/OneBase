using OneBase.Application.Abstractions;

namespace OneBase.Application.Procurement;

/// <summary>
/// Общее для импорта таблиц снабжения (поставщики, движения склада): заголовки и значения сравниваются без регистра и знаков препинания,
/// «ё» = «е»; строка заголовков ищется по синонимам — русским, узбекским (кириллица и латиница) и частым опечаткам; «нет», «-» — пусто.
/// </summary>
internal static class TableImport
{
    /// <summary>Поле импорта: код, подпись для «какой столбец куда пошёл» и названия столбца (уже приведённые Norm).</summary>
    public sealed record Column(string Field, string Label, string[] Names);

    public static Column[] Columns(params (string Field, string Label, string[] Names)[] list) =>
        list.Select(h => new Column(h.Field, h.Label, h.Names.Select(Norm).ToArray())).ToArray();

    /// <summary>«Пусто» в таблицах: прочерк, «нет», «йўқ»…</summary>
    public static readonly HashSet<string> Blank = ["нет", "йук", "йўқ", "yoq", "yo q", "н д", "n a", "не указан", "не указано"];

    /// <summary>Для сравнения: нижний регистр, «ё» → «е», знаки препинания — пробелы (кроме «№»), лишние пробелы убраны.</summary>
    public static string Norm(string? text)
    {
        var chars = (text ?? "").ToLowerInvariant().Replace('ё', 'е').Select(c => char.IsLetterOrDigit(c) || c == '№' ? c : ' ').ToArray();
        return string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Узбекское написание русских слов: «сирё» = «Сырьё» (без «ь», «ъ», «ы» = «и»).</summary>
    public static string Loose(string text) => Norm(text).Replace("ь", "").Replace("ъ", "").Replace('ы', 'и');

    /// <summary>
    /// Столбцы строки заголовков: поле → номер столбца, каждый столбец — одному полю. Сначала точное совпадение, затем начало заголовка
    /// («Наименование поставщика», «ИНН поставщика»).
    /// </summary>
    public static Dictionary<string, int> Match(IReadOnlyList<string> row, Column[] columns)
    {
        var cells = row.Select(Norm).ToList();
        var found = new Dictionary<string, int>();
        foreach (var exact in new[] { true, false })
        {
            foreach (var (field, _, names) in columns)
            {
                if (found.ContainsKey(field))
                {
                    continue;
                }

                for (var i = 0; i < cells.Count; i++)
                {
                    var cell = cells[i];
                    if (cell.Length > 0 && !found.ContainsValue(i) && names.Any(n => cell == n || (!exact && cell.StartsWith(n + " ", StringComparison.Ordinal))))
                    {
                        found[field] = i;
                        break;
                    }
                }
            }
        }

        return found;
    }

    /// <summary>Таблица файла: лист и строка заголовков (в первых 20 строках любого листа), где столбцы подходят (accept).</summary>
    public sealed record Table(ReportSheet Sheet, int Header, Dictionary<string, int> Columns)
    {
        public IReadOnlyList<IReadOnlyList<string>> Rows => Sheet.Rows;

        public string Title(int column) => column < Sheet.Rows[Header].Count ? Sheet.Rows[Header][column].Trim() : "";
    }

    public static Table? Find(ReportDocument document, Column[] columns, Func<Dictionary<string, int>, bool> accept)
    {
        foreach (var sheet in document.Sheets)
        {
            for (var r = 0; r < Math.Min(sheet.Rows.Count, 20); r++)
            {
                var found = Match(sheet.Rows[r], columns);
                if (accept(found))
                {
                    return new Table(sheet, r, found);
                }
            }
        }

        return null;
    }

    /// <summary>Какой столбец файла куда пошёл: «Название ← «Поставшик»» (служебные поля, вроде «№», — не показываются).</summary>
    public static List<string> Mapped(Column[] columns, Table table, params string[] hidden) =>
        columns.Where(c => !hidden.Contains(c.Field) && table.Columns.ContainsKey(c.Field)).Select(c => $"{c.Label} ← «{table.Title(table.Columns[c.Field])}»").ToList();
}
