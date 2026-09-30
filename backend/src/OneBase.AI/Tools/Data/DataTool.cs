using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using OneBase.AI.Consultant;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Security;

namespace OneBase.AI.Tools.Data;

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
    public bool IsCritical => false;
    public abstract JsonElement InputSchema { get; }

    public async Task<ToolResult> ExecuteAsync(ToolContext context, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            return await RunAsync(new ToolArgs(arguments), cancellationToken);
        }
        catch (ToolArgumentException ex)
        {
            return ToolResult.Error(ex.Message);
        }
    }

    protected abstract Task<ToolResult> RunAsync(ToolArgs args, CancellationToken ct);

    protected static ToolResult Data(object payload, params DataSource[] sources) =>
        new(true, JsonSerializer.Serialize(payload, Json), null, sources);

    public static string MonthName(int month) => MonthNames[month - 1];

    public static string PeriodText(PeriodInfo p) =>
        $"{MonthName(p.Month)} {p.Year}, данные по {p.DataThrough:dd.MM.yyyy}";

    public static string Query(int year, int month) => $"year={year}&month={month}";

    /// <summary>Период для модели: месяц, по какой день данные, сколько рабочих дней прошло.</summary>
    protected static object PeriodOf(PeriodInfo p) => new
    {
        p.Year,
        p.Month,
        MonthName = MonthName(p.Month),
        DataThrough = p.DataThrough.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        p.WorkedDays,
        p.DaysInMonth,
        Complete = p.DataThrough.Day >= p.DaysInMonth,
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

    public (int? Year, int? Month) Period()
    {
        var year = Int("year", 2000, 2100);
        var month = Int("month", 1, 12);
        return (month is null ? year : year ?? DateTime.UtcNow.Year, month);
    }
}
