using System.Text.Json;
using System.Text.Json.Serialization;

namespace OneBase.Application.Analytik.Query;

/// <summary>
/// Типизированный план аналитического запроса. Модель (или экран) не пишет SQL — она описывает, что нужно: базовая таблица, колонки,
/// показатели, фильтры, группировка, интервал времени, сортировка, предел. SQL строит и проверяет QueryBuilder по каталогу.
/// Колонка — «Column» (базовой таблицы), «Table.Column» или «schema.Table.Column»; таблица не из запроса подключается сама по
/// подтверждённой связи (до 3 шагов).
/// </summary>
public sealed record QueryPlan
{
    public string From { get; init; } = "";
    public IReadOnlyList<string>? Join { get; init; }
    public IReadOnlyList<PlanField>? Select { get; init; }
    public IReadOnlyList<PlanMeasure>? Measures { get; init; }
    public IReadOnlyList<PlanFilter>? Filters { get; init; }
    public IReadOnlyList<string>? GroupBy { get; init; }
    public PlanBucket? Bucket { get; init; }
    public IReadOnlyList<PlanOrder>? OrderBy { get; init; }
    public int? Limit { get; init; }
    public int? Offset { get; init; }

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public static QueryPlan Parse(JsonElement json)
    {
        try
        {
            return json.Deserialize<QueryPlan>(Json) ?? throw new QueryPlanException(QueryErrors.Invalid, "План запроса пуст.");
        }
        catch (JsonException ex)
        {
            throw new QueryPlanException(QueryErrors.Invalid, $"План запроса не разобран: {ex.Message}");
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, Json);
}

/// <summary>Колонка результата: ссылка на колонку и подпись (as).</summary>
public sealed record PlanField(string Column, string? As = null);

/// <summary>Показатель: sum, avg, min, max, count (без колонки — число строк), count_distinct.</summary>
public sealed record PlanMeasure(string Fn, string? Column = null, string? As = null);

/// <summary>
/// Фильтр: eq, ne, gt, gte, lt, lte, in, not_in, between (два значения, включительно), contains, starts_with (текст без учёта регистра),
/// is_null, not_null. Value — значение, массив (in, not_in, between) или пусто.
/// </summary>
public sealed record PlanFilter(string Column, string Op, JsonElement? Value = null);

/// <summary>Интервал времени: колонка-дата и шаг day, week, month, quarter, year.</summary>
public sealed record PlanBucket(string Column, string Unit, string? As = null);

/// <summary>Сортировка по подписи колонки результата или колонке; dir — asc | desc.</summary>
public sealed record PlanOrder(string By, string? Dir = null);

public static class QueryErrors
{
    /// <summary>План неверен (нет таблицы, колонки, связи, неверный тип значения) — модель исправит план.</summary>
    public const string Invalid = "invalid";

    /// <summary>Нет доступа к таблице или колонке.</summary>
    public const string Denied = "denied";

    /// <summary>Запрос слишком тяжёлый — сузить период или условия.</summary>
    public const string TooHeavy = "too_heavy";

    /// <summary>Данных нет (у показателя нет истории): экран показывает «Нет данных», а не ошибку.</summary>
    public const string NoData = "no_data";
}

/// <summary>План отклонён: Code — QueryErrors, сообщение безопасно показать пользователю и модели.</summary>
public sealed class QueryPlanException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
