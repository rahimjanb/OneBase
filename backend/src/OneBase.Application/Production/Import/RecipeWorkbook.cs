using System.Text.RegularExpressions;

namespace OneBase.Application.Production.Import;

/// <summary>Значение нормы в ячейке продукта: число больше нуля, ноль (не входит в состав), отрицательное, ошибка формулы, текст.</summary>
public enum RecipeCellKind
{
    Number,
    Zero,
    Negative,
    Error,
    Text,
}

/// <summary>
/// Компонент рецептуры в Excel: строка листа, ячейка нормы, артикул, название и единица сырья (как в файле), отметка «Вес» (входит в массу
/// сырья на тонну), что в ячейке и норма (число как в Excel; для нуля и отрицательного — тоже число).
/// </summary>
public sealed record RecipeSourceLine(int Row, string Cell, string? Article, string? Name, string? Unit, string? Weight, RecipeCellKind Kind, string? Raw,
    string? Formula, decimal? Value, string? Text);

/// <summary>
/// Рецептура в Excel — столбец продукта: лист, столбец, артикул и название продукта, группа (строка 1), база (количество и единица выхода, на
/// которое заданы нормы, и откуда она взята), итог листа «1 тонна учун кг» и потери (как в файле), компоненты — непустые ячейки столбца.
/// </summary>
public sealed record RecipeSource(string Sheet, int Col, string Column, int HeaderRow, string? Article, string Name, string? Group, decimal? BaseQuantity,
    string? BaseUnit, string? BaseLabel, XlsxCell? Total, XlsxCell? Loss, IReadOnlyList<RecipeSourceLine> Lines)
{
    /// <summary>Устойчивый ключ рецептуры в файле: лист и артикул продукта, без артикула — лист и название.</summary>
    public string SourceKey => $"{Sheet}|{(Article is { Length: > 0 } a ? a : "name:" + Name)}";

    public IEnumerable<RecipeSourceLine> Positive => Lines.Where(l => l.Kind == RecipeCellKind.Number);
}

/// <summary>Лист книги: рецептуры по столбцам («norma …») или другой (выгрузки остатков, продаж, пустые заголовки).</summary>
public sealed record RecipeSheet(string Name, bool Hidden, bool HasRecipes, int? HeaderRow, string? BaseLabel, IReadOnlyList<RecipeSource> Recipes,
    IReadOnlyList<string> Notes);

/// <summary>
/// Разбор книги технологических норм: листы, где есть строка-заголовок «Артикул | Наименование сырья | Ед.изм.», продукты — столбцы правее
/// (название — в строке заголовка, артикул — строкой выше, группа — в строке 1), сырьё — строки ниже. Служебные столбцы (Сум, Доллар, Курс,
/// НАХР, Вес) и «Итого» продуктами не считаются; продукт может стоять и левее «НАХР». База норм — подпись выше заголовка «1 тонна учун кг»
/// (на 1 тонну) — 1 000 кг; нет подписи — база не определена. Значения не меняются: ноль, отрицательное, текст и ошибки остаются как есть.
/// </summary>
public static partial class RecipeWorkbook
{
    private static readonly HashSet<string> ServiceHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "№", "Артикул", "Наименование сырья", "Наименование", "Ед.изм.", "Ед.изм", "Ед. изм.", "Сум", "Доллар", "Курс", "НАХР", "Вес", "Итого", "Всего",
    };

    public static IReadOnlyList<RecipeSheet> Parse(IReadOnlyList<XlsxSheet> book) => book.Select(Sheet).ToList();

    private static RecipeSheet Sheet(XlsxSheet sheet)
    {
        var header = HeaderRow(sheet);
        if (header is null)
        {
            return new RecipeSheet(sheet.Name, sheet.Hidden, false, null, null, [], ["Нет строки «Артикул | Наименование сырья» — не рецептуры."]);
        }

        var notes = new List<string>();
        int? totalRow = null, lossRow = null;
        decimal? baseQty = null;
        string? baseUnit = null, baseLabel = null;
        for (var r = 1; r < header; r++)
        {
            for (var c = 1; c <= Math.Min(9, sheet.MaxCol); c++)
            {
                if (sheet.Text(r, c) is not { } text)
                {
                    continue;
                }

                if (BasisLabel().Match(text) is { Success: true } m && totalRow is null)
                {
                    totalRow = r;
                    baseLabel = $"{text} ({XlsxBook.Ref(r, c)})";
                    (baseQty, baseUnit) = m.Groups["t"].Success ? (1000m, "кг") : (1m, "кг");
                }
                else if (text.Equals("Потери", StringComparison.OrdinalIgnoreCase) && lossRow is null)
                {
                    lossRow = r;
                }
            }
        }

        if (baseQty is null)
        {
            notes.Add("Не найдена подпись базы норм («1 тонна учун кг») — база рецептур не определена.");
        }

        var weightCol = Enumerable.Range(1, Math.Min(12, sheet.MaxCol)).Cast<int?>()
            .FirstOrDefault(c => string.Equals(sheet.Text(header.Value, c!.Value), "Вес", StringComparison.OrdinalIgnoreCase));
        var products = new List<(int Col, string Name)>();
        for (var c = 4; c <= sheet.MaxCol; c++)
        {
            if (sheet.Text(header.Value, c) is { } name && !ServiceHeaders.Contains(name))
            {
                products.Add((c, name));
            }
            else if (sheet.Text(header.Value, c) is { } service && (service.Equals("Итого", StringComparison.OrdinalIgnoreCase) || service.Equals("Всего", StringComparison.OrdinalIgnoreCase)))
            {
                notes.Add($"Столбец {XlsxBook.Column(c)} «{service}» — итог, не продукт.");
            }
        }

        var recipes = new List<RecipeSource>();
        foreach (var (col, name) in products)
        {
            var lines = new List<RecipeSourceLine>();
            for (var r = header.Value + 1; r <= sheet.MaxRow; r++)
            {
                if (sheet[r, col] is not { } cell || Line(sheet, r, col, cell, weightCol) is not { } line)
                {
                    continue;
                }

                lines.Add(line);
            }

            recipes.Add(new RecipeSource(sheet.Name, col, XlsxBook.Column(col), header.Value, sheet.Text(header.Value - 1, col), name, sheet.Text(1, col), baseQty,
                baseUnit, baseLabel, totalRow is { } tr ? sheet[tr, col] : null, lossRow is { } lr ? sheet[lr, col] : null, lines));
        }

        return new RecipeSheet(sheet.Name, sheet.Hidden, true, header, baseLabel, recipes, notes);
    }

    /// <summary>Строка-заголовок: «Артикул» в B и «Наименование…» в C (в первых 50 строках).</summary>
    private static int? HeaderRow(XlsxSheet sheet)
    {
        for (var r = 1; r <= Math.Min(50, sheet.MaxRow); r++)
        {
            if (string.Equals(sheet.Text(r, 2), "Артикул", StringComparison.OrdinalIgnoreCase)
                && sheet.Text(r, 3)?.StartsWith("Наименование", StringComparison.OrdinalIgnoreCase) == true)
            {
                return r;
            }
        }

        return null;
    }

    private static RecipeSourceLine? Line(XlsxSheet sheet, int row, int col, XlsxCell cell, int? weightCol)
    {
        RecipeCellKind kind;
        decimal? value = null;
        string? text = null;
        switch (cell.Kind)
        {
            case XlsxCellKind.Number:
                value = cell.Number;
                kind = value > 0 ? RecipeCellKind.Number : value < 0 ? RecipeCellKind.Negative : RecipeCellKind.Zero;
                break;
            case XlsxCellKind.Error:
                kind = RecipeCellKind.Error;
                text = cell.Text;
                break;
            case XlsxCellKind.Text when string.IsNullOrWhiteSpace(cell.Text) && cell.Formula is null:
                return null; // пустой текст — пустая ячейка
            case XlsxCellKind.Bool:
                kind = RecipeCellKind.Text;
                text = cell.Text == "TRUE" ? "ИСТИНА (логическое значение)" : "ЛОЖЬ (логическое значение)";
                break;
            default:
                kind = RecipeCellKind.Text;
                text = cell.Text;
                break;
        }

        return new RecipeSourceLine(row, cell.Ref, sheet.Text(row, 2), sheet.Text(row, 3), sheet.Text(row, 4), weightCol is { } w ? Weight(sheet[row, w]) : null, kind,
            cell.Raw, cell.Formula, value, text);
    }

    /// <summary>Отметка «Вес» (входит в массу сырья на тонну): в файлах бывает 1/0 числом или текстом и ИСТИНА/ЛОЖЬ — всё приводится к «1»/«0».</summary>
    private static string? Weight(XlsxCell? cell) => cell switch
    {
        { Kind: XlsxCellKind.Bool } => cell.Text == "TRUE" ? "1" : "0",
        { Kind: XlsxCellKind.Number, Number: { } n } => ExcelNumbers.Format(n),
        { Kind: XlsxCellKind.Text, Text: { } t } when t.Trim().Length > 0 => t.Trim(),
        _ => null,
    };

    // «1 тонна учун кг», «1 тонну на кг», «на 1 т кг» — нормы на 1 тонну; «1 кг учун кг» — на 1 кг.
    [GeneratedRegex(@"^(на\s+)?1\s*((?<t>тонн[ау]?|тн|т)|кг)\s*(учун|на)?\s*кг\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BasisLabel();
}
