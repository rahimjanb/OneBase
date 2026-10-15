using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using OneBase.AI.Tools;
using OneBase.Application.Reports;

namespace OneBase.AI.Reports;

/// <summary>Ошибка в описании отчёта — текст уходит модели, чтобы она исправила вызов.</summary>
public sealed class ReportSpecException(string message) : Exception(message);

/// <summary>
/// Описание отчёта от AI-сотрудника → отчёт с данными. Лист с source: backend сам вызывает инструмент данных (с правами пользователя),
/// берёт массив по пути path и раскладывает поля по колонкам — цифры попадают в отчёт из данных OneBase, а не из текста модели.
/// Лист с rows — небольшая таблица, которую модель посчитала сама (итоги, выводы).
/// </summary>
public static partial class ReportSpec
{
    public const string ToolName = "create_report";
    public const string ToolTitle = "Формирую отчёт Excel";

    public const int MaxSheets = 10;
    public const int MaxColumns = 40;
    public const int MaxRows = 10_000;
    public const int MaxLiteralRows = 200;
    public const int MaxCells = 300_000;
    public const int MaxTitle = 120;

    public static readonly string Description =
        "Создаёт отчёт Excel (xlsx) с предпросмотром в чате. Вызывай ТОЛЬКО когда пользователь сам попросил отчёт, Excel или выгрузку, и ОДИН раз — " +
        "после того как получил данные инструментами. Лист с source: укажи инструмент данных и его аргументы (как при обычном вызове), path — путь к " +
        "массиву строк в JSON-результате инструмента (например «regions», «worst», «republic»; вложенный — через точку), columns — поля строки " +
        "(field, вложенные — через точку) с заголовком и форматом. Цифры backend возьмёт прямо из данных, при отчёте списки инструментов полные " +
        "(без топ-10). Лист с rows вместо source — только для небольшой таблицы, которую ты посчитал сам (итоги, сравнение), до 200 строк. " +
        "format: text, integer, number, kg, money (сумы), percent (значение в процентах: 27.8 = 27,8 %), date. total: true — итоговая строка с суммами.";

    public static readonly JsonElement InputSchema = JsonSerializer.SerializeToElement(new
    {
        type = "object",
        required = new[] { "title", "sheets" },
        properties = new
        {
            title = new { type = "string", description = "Название отчёта: что и за какой период, например «Продажи по регионам, октябрь 2026»." },
            sheets = new
            {
                type = "array",
                description = "Листы отчёта (до 10).",
                items = new
                {
                    type = "object",
                    required = new[] { "name", "columns" },
                    properties = new
                    {
                        name = new { type = "string", description = "Название листа (до 31 знака)." },
                        source = new
                        {
                            type = "object",
                            description = "Откуда строки: инструмент данных, его аргументы и путь к массиву в результате.",
                            required = new[] { "tool", "path" },
                            properties = new
                            {
                                tool = new { type = "string" },
                                input = new { type = "object" },
                                path = new { type = "string", description = "Путь к массиву (или объекту — одна строка) в JSON-результате; пусто — весь результат." },
                            },
                        },
                        columns = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "object",
                                required = new[] { "header" },
                                properties = new
                                {
                                    field = new { type = "string", description = "Поле строки из source (через точку для вложенных). Для rows не нужно." },
                                    header = new { type = "string" },
                                    format = new { type = "string", @enum = ReportFormats.All },
                                },
                            },
                        },
                        rows = new
                        {
                            type = "array",
                            description = "Строки таблицы, если нет source: массивы значений по колонкам (числа, строки, null).",
                            items = new { type = "array", items = new { } },
                        },
                        total = new { type = "boolean", description = "Итоговая строка с суммами числовых колонок (не процентов)." },
                        note = new { type = "string", description = "Пояснение под таблицей: источник, период, оговорки." },
                    },
                },
            },
        },
    });

    /// <summary>Результат вызова инструмента данных для листа: успех, JSON-текст, источники.</summary>
    public delegate Task<ToolResult> RunTool(string tool, JsonElement input, CancellationToken ct);

    public static async Task<AiReportContent> BuildAsync(JsonElement args, RunTool run, CancellationToken ct)
    {
        if (args.ValueKind != JsonValueKind.Object)
        {
            throw new ReportSpecException("Нужен объект с title и sheets.");
        }

        var title = Str(args, "title")?.Trim();
        if (string.IsNullOrEmpty(title))
        {
            throw new ReportSpecException("Не указано название отчёта (title).");
        }

        title = title.Length > MaxTitle ? title[..MaxTitle].TrimEnd() : title;
        if (!args.TryGetProperty("sheets", out var sheetsArg) || sheetsArg.ValueKind != JsonValueKind.Array || sheetsArg.GetArrayLength() == 0)
        {
            throw new ReportSpecException("Нужен хотя бы один лист (sheets).");
        }

        if (sheetsArg.GetArrayLength() > MaxSheets)
        {
            throw new ReportSpecException($"Листов — не больше {MaxSheets}.");
        }

        var cache = new Dictionary<string, ToolResult>(StringComparer.Ordinal);
        var sources = new List<string>();
        var sheets = new List<AiReportSheet>();
        var cells = 0;
        var index = 0;
        foreach (var s in sheetsArg.EnumerateArray())
        {
            index++;
            var name = Str(s, "name")?.Trim() is { Length: > 0 } n ? n : $"Лист {index}";
            var sheet = s.TryGetProperty("source", out var source) && source.ValueKind == JsonValueKind.Object
                ? await FromSourceAsync(name, s, source, run, cache, sources, ct)
                : FromRows(name, s);
            cells += sheet.Rows.Count * Math.Max(sheet.Columns.Count, 1);
            if (cells > MaxCells)
            {
                throw new ReportSpecException($"Отчёт слишком большой: больше {MaxCells:N0} ячеек. Сузьте период или фильтры.");
            }

            sheets.Add(sheet);
        }

        return new AiReportContent(title, sheets, sources);
    }

    private static async Task<AiReportSheet> FromSourceAsync(string name, JsonElement sheet, JsonElement source, RunTool run,
        Dictionary<string, ToolResult> cache, List<string> sources, CancellationToken ct)
    {
        var tool = Str(source, "tool")?.Trim();
        if (string.IsNullOrEmpty(tool))
        {
            throw new ReportSpecException($"Лист «{name}»: в source не указан инструмент (tool).");
        }

        if (tool == ToolName)
        {
            throw new ReportSpecException($"Лист «{name}»: источник — инструмент данных, а не {ToolName}.");
        }

        var input = source.TryGetProperty("input", out var i) && i.ValueKind == JsonValueKind.Object ? i : JsonSerializer.SerializeToElement(new { });
        var key = tool + "|" + input.GetRawText();
        if (!cache.TryGetValue(key, out var result))
        {
            result = await run(tool, input, ct);
            cache[key] = result;
        }

        if (!result.Success)
        {
            throw new ReportSpecException($"Лист «{name}»: инструмент {tool} вернул ошибку: {result.Content}");
        }

        foreach (var s in result.Sources ?? [])
        {
            var text = s.Period is null ? s.Title : $"{s.Title} ({s.Period})";
            if (!sources.Contains(text))
            {
                sources.Add(text);
            }
        }

        JsonElement root;
        try
        {
            root = JsonDocument.Parse(result.Content).RootElement.Clone();
        }
        catch (JsonException)
        {
            throw new ReportSpecException($"Лист «{name}»: инструмент {tool} вернул не таблицу, а текст — его в отчёт не взять.");
        }

        var path = Str(source, "path")?.Trim() ?? string.Empty;
        var target = Navigate(root, path)
                     ?? throw new ReportSpecException($"Лист «{name}»: в результате {tool} нет «{path}». Есть: {string.Join(", ", Paths(root))}.");
        var items = target.ValueKind switch
        {
            JsonValueKind.Array => target.EnumerateArray().ToList(),
            JsonValueKind.Object => [target],
            _ => throw new ReportSpecException($"Лист «{name}»: «{path}» — не таблица и не объект. Есть: {string.Join(", ", Paths(root))}."),
        };

        if (items.Count > MaxRows)
        {
            throw new ReportSpecException($"Лист «{name}»: {items.Count:N0} строк — больше {MaxRows:N0}. Сузьте период или фильтры.");
        }

        var specs = ColumnSpecs(name, sheet, items);
        var rows = items
            .Select(item => (IReadOnlyList<JsonElement>)specs.Select(c => Scalar(item.ValueKind == JsonValueKind.Object ? Navigate(item, c.Field) : item)).ToList())
            .ToList();
        var columns = specs.Select((c, k) => new AiReportColumn(c.Header, c.Format ?? Infer(c.Field, rows.Select(r => r[k])))).ToList();
        return new AiReportSheet(Name(name), columns, rows, Bool(sheet, "total"), Note(sheet));
    }

    private static AiReportSheet FromRows(string name, JsonElement sheet)
    {
        if (!sheet.TryGetProperty("rows", out var rowsArg) || rowsArg.ValueKind != JsonValueKind.Array)
        {
            throw new ReportSpecException($"Лист «{name}»: нужен source (инструмент данных) или rows (свои строки).");
        }

        if (rowsArg.GetArrayLength() > MaxLiteralRows)
        {
            throw new ReportSpecException($"Лист «{name}»: своих строк — не больше {MaxLiteralRows}. Большие таблицы — через source, из данных.");
        }

        var specs = ColumnSpecs(name, sheet, []);
        var rows = new List<IReadOnlyList<JsonElement>>();
        foreach (var r in rowsArg.EnumerateArray())
        {
            var values = r.ValueKind == JsonValueKind.Array ? r.EnumerateArray().Select(v => Scalar(v)).ToList() : [Scalar(r)];
            if (values.Count > specs.Count)
            {
                throw new ReportSpecException($"Лист «{name}»: в строке {values.Count} значений, а колонок {specs.Count}.");
            }

            while (values.Count < specs.Count)
            {
                values.Add(Null);
            }

            rows.Add(values);
        }

        var columns = specs.Select((c, k) => new AiReportColumn(c.Header, c.Format ?? Infer(c.Header, rows.Select(r => r[k])))).ToList();
        return new AiReportSheet(Name(name), columns, rows, Bool(sheet, "total"), Note(sheet));
    }

    private sealed record ColumnSpec(string Field, string Header, string? Format);

    /// <summary>Колонки листа; у source без columns — все простые поля первой строки (до 40).</summary>
    private static List<ColumnSpec> ColumnSpecs(string name, JsonElement sheet, IReadOnlyList<JsonElement> items)
    {
        var specs = new List<ColumnSpec>();
        if (sheet.TryGetProperty("columns", out var cols) && cols.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in cols.EnumerateArray())
            {
                var field = Str(c, "field")?.Trim() ?? string.Empty;
                var header = Str(c, "header")?.Trim() is { Length: > 0 } h ? h : field;
                var format = Str(c, "format")?.Trim().ToLowerInvariant();
                if (format is not null && !ReportFormats.All.Contains(format))
                {
                    throw new ReportSpecException($"Лист «{name}»: формат «{format}» неизвестен. Можно: {string.Join(", ", ReportFormats.All)}.");
                }

                specs.Add(new ColumnSpec(field, header.Length > 0 ? header : $"Колонка {specs.Count + 1}", format));
            }
        }

        if (specs.Count == 0 && items.Count > 0 && items[0].ValueKind == JsonValueKind.Object)
        {
            specs = items[0].EnumerateObject()
                .Where(p => p.Value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
                .Select(p => new ColumnSpec(p.Name, p.Name, null))
                .ToList();
        }
        else if (specs.Count == 0 && items.Count > 0)
        {
            specs = [new ColumnSpec(string.Empty, "Значение", null)];
        }

        if (specs.Count == 0)
        {
            throw new ReportSpecException($"Лист «{name}»: не указаны колонки (columns).");
        }

        if (specs.Count > MaxColumns)
        {
            throw new ReportSpecException($"Лист «{name}»: колонок — не больше {MaxColumns}.");
        }

        // Поле, которого нет ни в одной строке, — опечатка в имени: модели — список настоящих полей.
        if (items.Count > 0 && items[0].ValueKind == JsonValueKind.Object)
        {
            var missing = specs.Where(c => c.Field.Length > 0 && items.All(i => Navigate(i, c.Field) is null)).Select(c => c.Field).ToList();
            if (missing.Count > 0)
            {
                throw new ReportSpecException($"Лист «{name}»: в строках нет полей {string.Join(", ", missing)}. Есть: {string.Join(", ", Fields(items[0]))}.");
            }
        }

        return specs;
    }

    /// <summary>Узел по пути «a.b.0.c» (имена без учёта регистра, числа — индексы массива); пустой путь — сам узел; null — нет такого.</summary>
    public static JsonElement? Navigate(JsonElement root, string path)
    {
        var current = root;
        foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current.ValueKind == JsonValueKind.Object)
            {
                var found = current.EnumerateObject().FirstOrDefault(p => string.Equals(p.Name, part, StringComparison.OrdinalIgnoreCase));
                if (found.Value.ValueKind == JsonValueKind.Undefined)
                {
                    return null;
                }

                current = found.Value;
            }
            else if (current.ValueKind == JsonValueKind.Array && int.TryParse(part, out var i) && i >= 0 && i < current.GetArrayLength())
            {
                current = current[i];
            }
            else
            {
                return null;
            }
        }

        return current;
    }

    /// <summary>Пути к таблицам и объектам в результате инструмента (для подсказки модели).</summary>
    private static IEnumerable<string> Paths(JsonElement root, string prefix = "", int depth = 0)
    {
        if (root.ValueKind != JsonValueKind.Object || depth > 2)
        {
            yield break;
        }

        foreach (var p in root.EnumerateObject())
        {
            var path = prefix.Length == 0 ? p.Name : $"{prefix}.{p.Name}";
            if (p.Value.ValueKind == JsonValueKind.Array)
            {
                yield return $"{path} (строк: {p.Value.GetArrayLength()})";
            }
            else if (p.Value.ValueKind == JsonValueKind.Object)
            {
                yield return path;
                foreach (var nested in Paths(p.Value, path, depth + 1))
                {
                    yield return nested;
                }
            }
        }
    }

    /// <summary>Поля строки с вложенными объектами через точку (для подсказки модели).</summary>
    private static IEnumerable<string> Fields(JsonElement item, string prefix = "")
    {
        foreach (var p in item.EnumerateObject())
        {
            var path = prefix.Length == 0 ? p.Name : $"{prefix}.{p.Name}";
            if (p.Value.ValueKind == JsonValueKind.Object && prefix.Length == 0)
            {
                foreach (var nested in Fields(p.Value, path))
                {
                    yield return nested;
                }
            }
            else if (p.Value.ValueKind != JsonValueKind.Array)
            {
                yield return path;
            }
        }
    }

    /// <summary>Пустая ячейка — JSON null (не default: «неопределённый» JsonElement не сериализуется).</summary>
    private static readonly JsonElement Null = JsonDocument.Parse("null").RootElement.Clone();

    /// <summary>Ячейка: число, строка или null; массив и объект — текстом (JSON), булево — «да»/«нет» в Excel.</summary>
    private static JsonElement Scalar(JsonElement? value) => value switch
    {
        null => Null,
        { ValueKind: JsonValueKind.Number or JsonValueKind.String or JsonValueKind.True or JsonValueKind.False } v => v,
        { ValueKind: JsonValueKind.Array or JsonValueKind.Object } v => JsonSerializer.SerializeToElement(v.GetRawText()),
        _ => Null,
    };

    /// <summary>
    /// Формат по значениям и имени поля: числа — проценты («executionPct»: инструменты отдают проценты с суффиксом Pct; доли 0..1 без него —
    /// не проценты), кг, сумы («sum», «revenue»), целые; строки-даты — дата.
    /// </summary>
    public static string Infer(string field, IEnumerable<JsonElement> values)
    {
        var present = values.Where(v => v.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null)).ToList();
        if (present.Count > 0 && present.All(v => v.ValueKind == JsonValueKind.Number))
        {
            var f = field.ToLowerInvariant();
            if (PercentField().IsMatch(f))
            {
                return ReportFormats.Percent;
            }

            if (KgField().IsMatch(f))
            {
                return ReportFormats.Kg;
            }

            if (MoneyField().IsMatch(f))
            {
                return ReportFormats.Money;
            }

            return present.All(v => v.TryGetInt64(out _)) ? ReportFormats.Integer : ReportFormats.Number;
        }

        return present.Count > 0 && present.All(v => v.ValueKind == JsonValueKind.String && IsoDate().IsMatch(v.GetString() ?? string.Empty))
            ? ReportFormats.Date
            : ReportFormats.Text;
    }

    private static string Name(string name) => name.Length > 31 ? name[..31] : name;

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool Bool(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    private static string? Note(JsonElement sheet) => Str(sheet, "note")?.Trim() is { Length: > 0 } n ? (n.Length > 1000 ? n[..1000] : n) : null;

    /// <summary>Короткая сводка для модели: что попало в отчёт (листы, строки, колонки, первая строка).</summary>
    public static string Summary(Guid id, AiReportContent report)
    {
        var lines = new List<string>
        {
            $"Отчёт «{report.Title}» создан (id {id}): листов {report.Sheets.Count}, строк {report.RowCount.ToString("N0", CultureInfo.GetCultureInfo("ru-RU"))}.",
            "Пользователь увидит его под ответом: предпросмотр и кнопка «Скачать Excel». Не пересказывай таблицу целиком — назови главное.",
        };
        foreach (var s in report.Sheets)
        {
            lines.Add($"Лист «{s.Name}»: {s.Rows.Count} строк; колонки: {string.Join(", ", s.Columns.Select(c => $"{c.Header} ({c.Format})"))}.");
        }

        return string.Join('\n', lines);
    }

    [GeneratedRegex(@"pct|percent|процент")]
    private static partial Regex PercentField();

    [GeneratedRegex(@"kg|кг|weight|вес")]
    private static partial Regex KgField();

    [GeneratedRegex(@"sum|revenue|amount|money|price|сумм|выручк|оплат|payment")]
    private static partial Regex MoneyField();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}")]
    private static partial Regex IsoDate();
}
