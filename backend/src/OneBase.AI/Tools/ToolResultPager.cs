using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using OneBase.AI.Llm;

namespace OneBase.AI.Tools;

/// <summary>Урезанный список результата: путь в JSON, сколько строк всего, сколько показано, с какой строки и есть ли ещё.</summary>
public sealed record ListPage(string Path, int TotalCount, int ReturnedCount, int Offset, bool HasMore);

/// <summary>
/// Результаты инструментов для модели в пределах её контекста — без обрезки посередине JSON. Результат больше бюджета остаётся валидным
/// JSON: у самых больших списков убираются строки с конца, а в «_pagination» записано, какие списки урезаны (total_count, returned_count,
/// offset, has_more). Полный результат хранится на время работы сотрудника под result_id: следующие строки отдаёт инструмент
/// get_more_rows — из того же снимка, без нового запроса к данным, подряд по offset (без повторов и пропусков). Повтор того же вызова
/// с теми же аргументами не запрашивает данные заново. Один экземпляр — на один запуск AI-сотрудника.
/// </summary>
public sealed partial class ToolResultPager
{
    public const string ToolName = "get_more_rows";

    /// <summary>Сколько символов — запас под «_pagination» поверх урезанного результата.</summary>
    private const int MetaReserve = 2_400;

    /// <summary>Сколько урезанных списков перечисляется в «_pagination» (остальные — числом lists_truncated).</summary>
    private const int MaxListedPages = 12;

    public static readonly LlmToolDefinition Definition = new(
        ToolName,
        "Следующие строки списка из уже полученного результата инструмента (result_id и path — из его «_pagination»): из того же снимка данных, " +
        "без нового запроса, подряд с offset. Нужен, когда в результате has_more = true, а для ответа нужны все строки, а не первые. Если нужны " +
        "только сумма, среднее, количество или сравнение — бери итоги результата, а не перебирай строки.",
        JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                result_id = new { type = "string", description = "result_id из «_pagination» результата" },
                path = new { type = "string", description = "Путь списка из «_pagination.lists», например Agents или Regions[0].Agents" },
                offset = new { type = "integer", description = "С какой строки (next_offset прошлой страницы)" },
                limit = new { type = "integer", description = "Сколько строк (по умолчанию — сколько поместится)" },
            },
            required = new[] { "result_id", "path", "offset" },
        }));

    private static readonly JsonSerializerOptions Compact = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly Dictionary<string, JsonNode> _results = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string Content, string? ResultId)> _calls = new(StringComparer.Ordinal);
    private int _next;

    /// <summary>Сохранённых полных результатов (для тестов и журнала).</summary>
    public int Stored => _results.Count;

    /// <summary>
    /// Результат для модели не длиннее budget символов. Влезает — как есть. JSON — урезается по строкам списков с «_pagination» и
    /// сохраняется целиком; не JSON — сокращается по строкам текста с пометкой, сколько показано.
    /// </summary>
    public string Fit(string content, int budget, out IReadOnlyList<ListPage> pages)
    {
        pages = [];
        if (content.Length <= budget)
        {
            return content;
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(content);
        }
        catch (JsonException)
        {
            root = null;
        }

        if (root is null)
        {
            return FitText(content, budget);
        }

        // Длинным его делало только экранирование (\uXXXX вместо кириллицы): компактная запись помещается целиком.
        var compact = root.ToJsonString(Compact);
        if (compact.Length <= budget)
        {
            return compact;
        }

        var id = $"r{++_next}";
        _results[id] = root.DeepClone();
        var copy = root is JsonArray ? new JsonObject { ["items"] = root.DeepClone() } : root.DeepClone();
        var lists = Shrink(copy, budget - MetaReserve);
        pages = lists;
        // В «_pagination» — до 12 самых больших урезанных списков: метаданные сами не должны съедать бюджет.
        var shown = lists.OrderByDescending(p => p.TotalCount).Take(MaxListedPages).ToList();
        var meta = new JsonObject
        {
            ["result_id"] = id,
            ["truncated"] = lists.Count > 0,
            ["lists_truncated"] = lists.Count,
            ["lists"] = new JsonArray(shown.Select(p => (JsonNode)new JsonObject
            {
                ["path"] = root is JsonArray ? p.Path["items".Length..].TrimStart('.') is { Length: > 0 } rest ? rest : "" : p.Path,
                ["total_count"] = p.TotalCount,
                ["returned_count"] = p.ReturnedCount,
                ["offset"] = p.Offset,
                ["has_more"] = p.HasMore,
                ["next_offset"] = p.Offset + p.ReturnedCount,
            }).ToArray()),
            ["next"] = $"Показаны первые строки списков. Остальные — {ToolName}(result_id, path, offset = next_offset) из того же снимка, без нового запроса. " +
                       "Итоги и суммы результата посчитаны по всем строкам — для суммы, количества и сравнения перебирать страницы не нужно.",
        };
        if (copy is JsonObject obj)
        {
            obj["_pagination"] = meta;
        }

        var text = copy.ToJsonString(Compact);
        return text.Length <= budget ? text : new JsonObject { ["_pagination"] = meta, ["message"] = "Результат слишком большой даже после сокращения списков: сузьте фильтры (период, регион) или запросите строки страницами." }.ToJsonString(Compact);
    }

    /// <summary>Повтор того же вызова в этом запуске: ключ — инструмент и аргументы без порядка свойств и пробелов.</summary>
    public static string CallKey(string tool, JsonElement arguments) => $"{tool}:{Canonical(arguments)}";

    /// <summary>Запомнить результат вызова; повтор отдаст ссылку на него.</summary>
    public void Remember(string key, string content, string? resultId) => _calls[key] = (content, resultId);

    /// <summary>Тот же вызов уже был — его результат (полный) и result_id, если он сохранён страницами.</summary>
    public bool TryRepeat(string key, out string content, out string? resultId)
    {
        if (_calls.TryGetValue(key, out var hit))
        {
            (content, resultId) = hit;
            return true;
        }

        content = string.Empty;
        resultId = null;
        return false;
    }

    /// <summary>Последний сохранённый result_id (для связи повтора вызова с его страницами).</summary>
    public string? LastResultId => _next == 0 ? null : $"r{_next}";

    /// <summary>Страница строк списка сохранённого результата: подряд с offset, не длиннее budget символов.</summary>
    public ToolResult Page(JsonElement arguments, int budget)
    {
        string? Str(string name) => arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        int? Int(string name) => arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty(name, out var v) && v.TryGetInt32(out var n) ? n : null;

        var id = Str("result_id");
        if (id is null || !_results.TryGetValue(id, out var root))
        {
            return ToolResult.Failure(ToolName, ToolFailures.NotFound, $"Результата «{id}» в этом анализе нет — вызови инструмент данных заново.");
        }

        var path = Str("path") ?? "";
        if (Resolve(root, path) is not JsonArray list)
        {
            return ToolResult.Failure(ToolName, ToolFailures.InvalidArguments, $"Списка «{path}» в результате {id} нет — путь берётся из «_pagination.lists».");
        }

        var offset = Math.Max(0, Int("offset") ?? 0);
        var limit = Math.Max(1, Int("limit") ?? list.Count);
        var rows = new JsonArray();
        var size = 0;
        for (var i = offset; i < list.Count && rows.Count < limit; i++)
        {
            var row = list[i]?.DeepClone();
            var length = row?.ToJsonString(Compact).Length ?? 4;
            // Хотя бы одна строка — даже большая; дальше — пока помещается.
            if (rows.Count > 0 && size + length > budget - MetaReserve)
            {
                break;
            }

            rows.Add(row);
            size += length + 1;
        }

        var returned = rows.Count;
        var page = new JsonObject
        {
            ["result_id"] = id,
            ["path"] = path,
            ["offset"] = offset,
            ["returned_count"] = returned,
            ["total_count"] = list.Count,
            ["has_more"] = offset + returned < list.Count,
            ["next_offset"] = offset + returned,
            ["rows"] = rows,
        };
        return ToolResult.Ok(page.ToJsonString(Compact));
    }

    /// <summary>
    /// Урезать списки (строки — с конца), пока JSON не станет не длиннее budget. Пути — от корня: Agents, Regions[0].Agents. Урезается список
    /// с наибольшим собственным весом — без вложенных списков: у регионов с длинными списками ТП урезаются ТП внутри регионов, а строки
    /// регионов (с их итогами) остаются; у длинного списка с короткими вложенными — сам список.
    /// </summary>
    internal static IReadOnlyList<ListPage> Shrink(JsonNode root, int budget)
    {
        var totals = new Dictionary<string, int>(StringComparer.Ordinal);
        var arrays = new List<(string Path, JsonArray Array, int Parent)>();
        Collect(root, "", -1, arrays);
        foreach (var (path, array, _) in arrays)
        {
            totals[path] = array.Count;
        }

        for (var guard = 0; guard < 10_000; guard++)
        {
            var size = root.ToJsonString(Compact).Length;
            if (size <= budget)
            {
                break;
            }

            // Длина каждого списка дерева (не из уже убранных строк) и собственный вес — без ближайших вложенных списков.
            var lengths = new Dictionary<int, int>();
            var nested = new Dictionary<int, int>();
            for (var i = 0; i < arrays.Count; i++)
            {
                if (ReferenceEquals(arrays[i].Array.Root, root))
                {
                    lengths[i] = arrays[i].Array.ToJsonString(Compact).Length;
                    if (arrays[i].Parent >= 0)
                    {
                        nested[arrays[i].Parent] = nested.GetValueOrDefault(arrays[i].Parent) + lengths[i];
                    }
                }
            }

            // Список, у которого ещё есть что убрать (остаётся хотя бы одна строка), с наибольшим собственным весом.
            var pick = lengths.Keys
                .Where(i => arrays[i].Array.Count > 1)
                .OrderByDescending(i => lengths[i] - nested.GetValueOrDefault(i))
                .ThenByDescending(i => lengths[i])
                .DefaultIfEmpty(-1)
                .First();
            if (pick < 0)
            {
                break;
            }

            var (array, length) = (arrays[pick].Array, lengths[pick]);
            var average = Math.Max(1, length / array.Count);
            var remove = Math.Clamp((size - budget) / average + 1, 1, array.Count - 1);
            for (var i = 0; i < remove; i++)
            {
                array.RemoveAt(array.Count - 1);
            }
        }

        return arrays
            .Where(a => a.Array.Count < totals[a.Path] && ReferenceEquals(a.Array.Root, root))
            .Select(a => new ListPage(a.Path, totals[a.Path], a.Array.Count, 0, true))
            .ToList();
    }

    /// <summary>Все списки дерева с путями; parent — индекс ближайшего списка, внутри которого этот (−1 — нет).</summary>
    private static void Collect(JsonNode? node, string path, int parent, List<(string, JsonArray, int)> arrays)
    {
        switch (node)
        {
            case JsonArray array:
                var self = arrays.Count;
                arrays.Add((path, array, parent));
                for (var i = 0; i < array.Count; i++)
                {
                    Collect(array[i], $"{path}[{i}]", self, arrays);
                }

                break;
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    Collect(value, path.Length == 0 ? key : $"{path}.{key}", parent, arrays);
                }

                break;
        }
    }

    /// <summary>Узел по пути «Regions[0].Agents»; пустой путь — корень.</summary>
    internal static JsonNode? Resolve(JsonNode root, string path)
    {
        var node = root;
        foreach (Match m in PathPart().Matches(path))
        {
            if (node is null)
            {
                return null;
            }

            node = m.Groups["index"].Success
                ? node is JsonArray a && int.Parse(m.Groups["index"].Value, CultureInfo.InvariantCulture) is var i && i < a.Count ? a[i] : null
                : node is JsonObject o && o.TryGetPropertyValue(m.Groups["key"].Value, out var v) ? v : null;
        }

        return node;
    }

    private static string FitText(string content, int budget)
    {
        var keep = Math.Max(0, budget - 200);
        var cut = content.LastIndexOf('\n', Math.Min(keep, content.Length - 1));
        var shown = content[..(cut > 0 ? cut : keep)];
        return $"{shown}\n…(текст сокращён: показано {shown.Length} из {content.Length} символов — сузьте запрос)";
    }

    private static string Canonical(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => "{" + string.Join(",", element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => JsonSerializer.Serialize(p.Name) + ":" + Canonical(p.Value))) + "}",
        JsonValueKind.Array => "[" + string.Join(",", element.EnumerateArray().Select(Canonical)) + "]",
        JsonValueKind.String => JsonSerializer.Serialize(element.GetString()?.Trim()),
        JsonValueKind.Undefined => "null",
        _ => element.GetRawText(),
    };

    [GeneratedRegex(@"(?<key>[^.\[\]]+)|\[(?<index>\d+)\]")]
    private static partial Regex PathPart();
}
