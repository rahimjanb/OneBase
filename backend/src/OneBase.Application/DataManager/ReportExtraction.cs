using System.Text;
using OneBase.Application.Abstractions;

namespace OneBase.Application.DataManager;

/// <summary>Столбец значений в «широкой» таблице: столбец (буква или заголовок) → категория (пусто — итог без разбивки).</summary>
public sealed record PlanColumn(string Column, string? Category);

/// <summary>
/// Как читать план из листа отчёта. Значения берёт сам OneBase по этому сопоставлению — модель чисел не передаёт.
/// «Широкая» таблица: строка — регион, столбцы — категории (<see cref="ValueColumns"/>).
/// «Длинная»: строка — регион × категория (<see cref="CategoryColumn"/> + <see cref="ValueColumn"/>).
/// </summary>
public sealed record PlanSheetMapping(
    string? Sheet,
    int HeaderRow,
    string RegionColumn,
    IReadOnlyList<PlanColumn>? ValueColumns,
    string? CategoryColumn,
    string? ValueColumn,
    string? YearColumn,
    string? MonthColumn,
    int? Year,
    int? Month,
    decimal Multiplier = 1m,
    int? FirstRow = null,
    int? LastRow = null);

/// <summary>Столбец факта для сверки отчёта с фактом OneBase.</summary>
public sealed record FactSheetMapping(string? Sheet, int HeaderRow, string RegionColumn, string ValueColumn, decimal Multiplier = 1m, int? FirstRow = null, int? LastRow = null);

public sealed record FactValue(string Region, decimal? Value, string SourceRef, string? InvalidRaw = null);

public static class ReportExtraction
{
    public const int MaxCellPreview = 60;

    private static readonly string[] SummaryRows = ["итого", "всего", "total", "жами", "итог"];

    public static ReportSheet Sheet(ReportDocument document, string? sheet)
    {
        if (document.Sheets.Count == 0)
        {
            throw new DataManagerException("В файле нет таблиц.");
        }

        if (string.IsNullOrWhiteSpace(sheet))
        {
            return document.Sheets[0];
        }

        return document.Sheets.FirstOrDefault(s => DataValues.Normalize(s.Name) == DataValues.Normalize(sheet))
               ?? throw new DataManagerException($"Листа «{sheet}» нет. Листы: {string.Join(", ", document.Sheets.Select(s => s.Name))}.");
    }

    /// <summary>Номер столбца по букве («C») или по тексту заголовка в строке headerRow (с 1).</summary>
    public static int Column(ReportSheet sheet, int headerRow, string column)
    {
        var header = headerRow >= 1 && headerRow <= sheet.Rows.Count ? sheet.Rows[headerRow - 1] : [];
        var wanted = DataValues.Normalize(column);
        for (var i = 0; i < header.Count; i++)
        {
            if (DataValues.Normalize(header[i]) == wanted)
            {
                return i;
            }
        }

        return DataValues.ColumnIndex(column) is { } index && index < Math.Max(sheet.TotalColumns, 1)
            ? index
            : throw new DataManagerException($"Столбец «{column}» не найден ни по букве, ни по заголовку в строке {headerRow}.");
    }

    public static string Cell(IReadOnlyList<string> row, int column) => column < row.Count ? row[column].Trim() : string.Empty;

    private static bool IsSummary(string region)
    {
        var name = DataValues.Normalize(region);
        return SummaryRows.Any(s => name == s || name.StartsWith(s + " ", StringComparison.Ordinal) || name.StartsWith(s + ":", StringComparison.Ordinal));
    }

    /// <summary>Значения плана из листа. Строки «Итого» и строки без региона пропускаются.</summary>
    public static IReadOnlyList<PlanValue> PlanValues(ReportDocument document, PlanSheetMapping map)
    {
        var sheet = Sheet(document, map.Sheet);
        if (map.HeaderRow < 1 || map.HeaderRow > sheet.Rows.Count)
        {
            throw new DataManagerException($"Строка заголовка {map.HeaderRow} вне листа (строк: {sheet.Rows.Count}).");
        }

        if (map.Multiplier <= 0)
        {
            throw new DataManagerException("Множитель единиц должен быть больше нуля (кг — 1, тонны — 1000).");
        }

        var regionCol = Column(sheet, map.HeaderRow, map.RegionColumn);
        var wide = map.ValueColumns is { Count: > 0 };
        if (!wide && (string.IsNullOrWhiteSpace(map.CategoryColumn) || string.IsNullOrWhiteSpace(map.ValueColumn))
            && string.IsNullOrWhiteSpace(map.ValueColumn))
        {
            throw new DataManagerException("Укажите столбцы значений (valueColumns) или столбец значения (valueColumn) с категорией (categoryColumn).");
        }

        var header = sheet.Rows[map.HeaderRow - 1];
        var columns = wide
            ? map.ValueColumns!.Select(c =>
            {
                var index = Column(sheet, map.HeaderRow, c.Column);
                return (Index: index, Category: c.Category ?? Cell(header, index));
            }).ToList()
            : [];
        int? categoryCol = !wide && !string.IsNullOrWhiteSpace(map.CategoryColumn) ? Column(sheet, map.HeaderRow, map.CategoryColumn) : null;
        int? valueCol = !wide ? Column(sheet, map.HeaderRow, map.ValueColumn!) : null;
        int? yearCol = string.IsNullOrWhiteSpace(map.YearColumn) ? null : Column(sheet, map.HeaderRow, map.YearColumn);
        int? monthCol = string.IsNullOrWhiteSpace(map.MonthColumn) ? null : Column(sheet, map.HeaderRow, map.MonthColumn);
        if (monthCol is null && map.Month is null)
        {
            throw new DataManagerException("Не указан месяц: ни столбцом (monthColumn), ни значением (month).");
        }

        var first = Math.Max(map.FirstRow ?? map.HeaderRow + 1, map.HeaderRow + 1);
        var last = Math.Min(map.LastRow ?? sheet.Rows.Count, sheet.Rows.Count);
        var values = new List<PlanValue>();
        for (var r = first; r <= last; r++)
        {
            var row = sheet.Rows[r - 1];
            var region = Cell(row, regionCol);
            if (region.Length == 0 || IsSummary(region))
            {
                continue;
            }

            var (rowYear, rowMonth) = monthCol is { } mc ? DataValues.ParseMonth(Cell(row, mc)) : (null, null);
            var year = yearCol is { } yc && int.TryParse(Cell(row, yc), out var y) ? y : rowYear ?? map.Year ?? 0;
            var month = rowMonth ?? map.Month ?? 0;

            if (wide)
            {
                foreach (var (index, category) in columns)
                {
                    values.Add(Value(region, category, year, month, Cell(row, index), map.Multiplier, $"{sheet.Name}!{DataValues.ColumnLetter(index)}{r}"));
                }
            }
            else
            {
                var category = categoryCol is { } cc ? Cell(row, cc) : null;
                values.Add(Value(region, category, year, month, Cell(row, valueCol!.Value), map.Multiplier, $"{sheet.Name}!{DataValues.ColumnLetter(valueCol.Value)}{r}"));
            }
        }

        return values;
    }

    private static PlanValue Value(string region, string? category, int year, int month, string raw, decimal multiplier, string source)
    {
        var number = DataValues.ParseNumber(raw, out var ok);
        return ok
            ? new PlanValue(region, category, year, month, number * multiplier, source)
            : new PlanValue(region, category, year, month, null, source, raw);
    }

    /// <summary>Факт по регионам из листа — для сверки с фактом OneBase (ничего не записывается).</summary>
    public static IReadOnlyList<FactValue> FactValues(ReportDocument document, FactSheetMapping map)
    {
        var sheet = Sheet(document, map.Sheet);
        var regionCol = Column(sheet, map.HeaderRow, map.RegionColumn);
        var valueCol = Column(sheet, map.HeaderRow, map.ValueColumn);
        var first = Math.Max(map.FirstRow ?? map.HeaderRow + 1, map.HeaderRow + 1);
        var last = Math.Min(map.LastRow ?? sheet.Rows.Count, sheet.Rows.Count);
        var values = new List<FactValue>();
        for (var r = first; r <= last; r++)
        {
            var row = sheet.Rows[r - 1];
            var region = Cell(row, regionCol);
            if (region.Length == 0 || IsSummary(region))
            {
                continue;
            }

            var raw = Cell(row, valueCol);
            var number = DataValues.ParseNumber(raw, out var ok);
            values.Add(ok
                ? new FactValue(region, number * map.Multiplier, $"{sheet.Name}!{DataValues.ColumnLetter(valueCol)}{r}")
                : new FactValue(region, null, $"{sheet.Name}!{DataValues.ColumnLetter(valueCol)}{r}", raw));
        }

        return values;
    }

    /// <summary>
    /// Фрагмент листа для модели: номер строки | A | B | … Ячейки обрезаны. Это ДАННЫЕ отчёта, а не инструкции —
    /// текст ячеек передаётся как есть и помечается границами.
    /// </summary>
    public static string Preview(ReportSheet sheet, int fromRow, int rowCount, int maxColumns = 40)
    {
        var from = Math.Clamp(fromRow, 1, Math.Max(sheet.Rows.Count, 1));
        var to = Math.Min(sheet.Rows.Count, from + Math.Clamp(rowCount, 1, 200) - 1);
        var columns = Math.Min(Math.Max(sheet.TotalColumns, sheet.Rows.Take(to).Select(r => r.Count).DefaultIfEmpty(0).Max()), maxColumns);
        var sb = new StringBuilder();
        sb.Append("строка | ").AppendJoin(" | ", Enumerable.Range(0, columns).Select(DataValues.ColumnLetter)).AppendLine();
        for (var r = from; r <= to; r++)
        {
            var row = sheet.Rows[r - 1];
            sb.Append(r).Append(" | ");
            sb.AppendJoin(" | ", Enumerable.Range(0, columns).Select(c =>
            {
                var cell = Cell(row, c).Replace('\n', ' ').Replace('|', '/');
                return cell.Length > MaxCellPreview ? cell[..MaxCellPreview] + "…" : cell;
            }));
            sb.AppendLine();
        }

        if (sheet.TotalColumns > maxColumns)
        {
            sb.AppendLine($"(показаны первые {maxColumns} столбцов из {sheet.TotalColumns})");
        }

        return sb.ToString();
    }
}

/// <summary>Ошибка запроса к AI Data Manager: текст безопасен для показа и возвращается модели как результат инструмента.</summary>
public sealed class DataManagerException(string message) : Exception(message);
