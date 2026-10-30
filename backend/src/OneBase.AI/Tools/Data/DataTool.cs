using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using OneBase.AI.Consultant;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Security;

namespace OneBase.AI.Tools.Data;

/// <summary>Первые N строк списка с учётом полного числа строк (ToolArgs.Top) — для «_meta» результата.</summary>
public static class ToolLists
{
    /// <summary>Первые args.Cap(n) строк; в «_meta» результата — сколько их всего. Только для списков в памяти, не для запросов к базе.</summary>
    public static IEnumerable<T> TakeTop<T>(this IEnumerable<T> source, ToolArgs args, int n, string name) => args.Top(source, n, name);
}

/// <summary>Список результата: имя, сколько строк всего и сколько отдано модели.</summary>
public sealed record ListCount(string Name, int TotalCount, int ReturnedCount);

/// <summary>Неверные аргументы инструмента — текст уходит модели, чтобы она исправила вызов.</summary>
public sealed class ToolArgumentException(string message) : Exception(message);

/// <summary>
/// Инструмент чтения данных OneBase. Числа считает backend теми же сервисами, что строят страницы OneBase;
/// модель получает компактную JSON-сводку и ссылки на страницы-источники. Инструменты только читают.
/// </summary>
public abstract class DataTool : ITool
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly string[] MonthNames =
        ["январь", "февраль", "март", "апрель", "май", "июнь", "июль", "август", "сентябрь", "октябрь", "ноябрь", "декабрь"];

    public abstract string Name { get; }
    public abstract string Title { get; }
    public abstract string Description { get; }
    public abstract string? Source { get; }
    public virtual string? RequiredPermission => Permissions.SalesRead;

    /// <summary>Права, любое из которых заменяет RequiredPermission.</summary>
    public virtual IReadOnlyList<string>? AlternativePermissions => null;
    public bool IsCritical => false;
    public abstract JsonElement InputSchema { get; }

    /// <summary>Подсказка сотруднику, когда и как вызывать инструмент; попадает в его системный промпт. null — без подсказки.</summary>
    public virtual string? UsageHint => null;

    /// <summary>Единицы полей результатов инструментов данных — в «_meta» каждого результата.</summary>
    public const string Units = "Kg и …Kg — кг; RevenueSum и …Sum — сумы (UZS); …Pct, …Percent — проценты; …Hours — часы; …Minutes — минуты.";

    public async Task<ToolResult> ExecuteAsync(ToolContext context, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var args = new ToolArgs(arguments) { Report = context.Report, All = new ToolArgs(arguments).Bool(AllRowsArg) == true };
            return WithMeta(await RunAsync(context, args, cancellationToken), args, arguments);
        }
        catch (ToolArgumentException ex)
        {
            return ToolResult.Error(ex.Message);
        }
    }

    /// <summary>Аргумент любого инструмента данных: списки целиком (дальше — страницами get_more_rows), а не первые N.</summary>
    public const string AllRowsArg = "all_rows";

    /// <summary>
    /// «_meta» результата: инструмент, фильтры (аргументы), источники с периодом, единицы, урезанные списки (total_count, returned_count,
    /// truncated, has_more) и полнота. complete = false — показана выборка (первые N), а не весь список: модель не выдаёт Top-N за всё.
    /// </summary>
    internal ToolResult WithMeta(ToolResult result, ToolArgs args, JsonElement arguments)
    {
        if (!result.Success || result.Content.Length == 0 || result.Content[0] != '{')
        {
            return result;
        }

        JsonObject root;
        try
        {
            if (JsonNode.Parse(result.Content) is not JsonObject parsed)
            {
                return result;
            }

            root = parsed;
        }
        catch (JsonException)
        {
            return result;
        }

        var lists = args.Lists.Select(l => (JsonNode)new JsonObject
        {
            ["name"] = l.Name,
            ["total_count"] = l.TotalCount,
            ["returned_count"] = l.ReturnedCount,
            ["truncated"] = l.ReturnedCount < l.TotalCount,
            ["has_more"] = l.ReturnedCount < l.TotalCount,
        }).ToArray();
        var complete = args.Lists.All(l => l.ReturnedCount >= l.TotalCount);
        var meta = new JsonObject
        {
            ["tool"] = Name,
            ["filters"] = arguments.ValueKind == JsonValueKind.Object ? JsonNode.Parse(arguments.GetRawText()) : new JsonObject(),
            ["sources"] = new JsonArray((result.Sources ?? []).Select(s => (JsonNode)new JsonObject { ["title"] = s.Title, ["period"] = s.Period }).ToArray()),
            ["units"] = Units,
            ["complete"] = complete,
        };
        if (lists.Length > 0)
        {
            meta["lists"] = new JsonArray(lists);
        }

        if (!complete)
        {
            meta["note"] = "Показана выборка — первые строки списков (lists: total_count — сколько всего). Это не весь список: для всех строк вызови тот же " +
                           $"инструмент с {AllRowsArg} = true (строки придут страницами), для сумм и количества — бери итоги результата.";
        }

        root["_meta"] = meta;
        return result with { Content = root.ToJsonString(Json) };
    }

    /// <summary>Инструменты, которым важен пользователь (доступ к документам), переопределяют эту перегрузку.</summary>
    protected virtual Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct) => RunAsync(args, ct);

    protected virtual Task<ToolResult> RunAsync(ToolArgs args, CancellationToken ct) =>
        throw new NotSupportedException($"{GetType().Name} должен переопределить RunAsync.");

    protected static ToolResult Data(object payload, params DataSource[] sources) =>
        new(true, JsonSerializer.Serialize(payload, Json), null, sources);

    public static string MonthName(int month) => MonthNames[month - 1];

    public static string PeriodText(PeriodInfo p) =>
        $"{MonthName(p.Month)} {p.Year}, данные по {p.DataThrough:dd.MM.yyyy}" +
        (p.Today == p.DataThrough ? " (сегодня — неполный день, на время последней синхронизации Linko)" : "");

    public static string Query(int year, int month) => $"year={year}&month={month}";

    /// <summary>Период для модели: месяц, по какой день данные, сколько рабочих дней прошло.</summary>
    protected static object PeriodOf(PeriodInfo p) => new
    {
        p.Year,
        p.Month,
        MonthName = MonthName(p.Month),
        DataThrough = p.DataThrough.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        p.WorkedDays,
        // Прошедшие дни — база прогноза и темпа: сегодняшний неполный день входит своей долей (продано сегодня к среднему дню).
        ElapsedDays = p.ElapsedDays is { } elapsed ? Math.Round(elapsed, 1) : (decimal?)null,
        p.DaysInMonth,
        Complete = p.Closed,
    };

    /// <summary>Округление: кг и суммы — до целых.</summary>
    protected static decimal? R(decimal? value, int digits = 0) => value is null ? null : Math.Round(value.Value, digits);

    protected static decimal R(decimal value, int digits = 0) => Math.Round(value, digits);

    /// <summary>Доля 0..1 → проценты с одним знаком.</summary>
    protected static decimal? Pct(decimal? share) => share is null ? null : Math.Round(share.Value * 100, 1);

    protected static JsonElement Schema(params (string Name, object Definition)[] properties) =>
        JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = properties.ToDictionary(p => p.Name, p => p.Definition),
        });

    protected static readonly (string, object) YearArg = ("year", new { type = "integer", description = "Год, например 2026. Без года — год последних данных." });
    protected static readonly (string, object) MonthArg = ("month", new { type = "integer", description = "Месяц 1–12. Без месяца — месяц последних данных (текущий)." });
    protected static readonly (string, object) RegionArg = ("region", new { type = "string", description = "Регион: id или название (например «Самарканд»). Без региона — вся республика." });
}

/// <summary>Аргументы вызова инструмента с проверкой.</summary>
public sealed class ToolArgs(JsonElement root)
{
    /// <summary>Вызов для отчёта Excel (ToolContext.Report): списки — целиком.</summary>
    public bool Report { get; init; }

    /// <summary>Модель попросила списки целиком (all_rows = true): большие придут страницами (ToolResultPager).</summary>
    public bool All { get; init; }

    /// <summary>Сколько строк списка отдать: модели — n (компактно), в отчёт или по all_rows — все.</summary>
    public int Cap(int n) => Report || All ? int.MaxValue : n;

    /// <summary>Списки результата, урезанные до первых N: сколько строк всего и сколько отдано (в «_meta» результата).</summary>
    public List<ListCount> Lists { get; } = [];

    /// <summary>Первые Cap(n) строк списка с учётом: в «_meta» попадёт, сколько их всего. name — имя списка в результате.</summary>
    public IEnumerable<T> Top<T>(IEnumerable<T> source, int n, string name)
    {
        var all = source as IReadOnlyList<T> ?? source.ToList();
        var cap = Cap(n);
        var shown = all.Count <= cap ? all : all.Take(cap).ToList();
        Lists.Add(new ListCount(name, all.Count, shown.Count));
        return shown;
    }

    /// <summary>Учесть список, урезанный запросом к базе: total — сколько строк всего (отдельный подсчёт), returned — сколько отдано.</summary>
    public void Count(string name, int total, int returned) => Lists.Add(new ListCount(name, total, returned));

    public int? Int(string name, int min, int max)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        int value;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n))
        {
            value = n;
        }
        else if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var s))
        {
            value = s;
        }
        else
        {
            throw new ToolArgumentException($"Аргумент {name} должен быть целым числом.");
        }

        return value < min || value > max ? throw new ToolArgumentException($"Аргумент {name} — от {min} до {max}.") : value;
    }

    public string? Str(string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()!.Trim()
            : null;

    public bool? Bool(string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var v)
            ? v.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String when bool.TryParse(v.GetString(), out var b) => b,
                _ => null,
            }
            : null;

    public (int? Year, int? Month) Period()
    {
        var year = Int("year", 2000, 2100);
        var month = Int("month", 1, 12);
        return (month is null ? year : year ?? DateTime.UtcNow.Year, month);
    }
}
