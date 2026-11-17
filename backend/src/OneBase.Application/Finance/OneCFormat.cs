using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OneBase.Finance.Integration.Contracts;

namespace OneBase.Application.Finance;

/// <summary>Общие правила данных 1С в OneBase: время Ташкента, отпечатки, виды субконто, сравнение полей для журнала изменений.</summary>
public static class OneCFormat
{
    /// <summary>Ташкент: UTC+5 круглый год.</summary>
    public static readonly TimeSpan Offset = TimeSpan.FromHours(5);

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Начало дня по Ташкенту в UTC.</summary>
    public static DateTimeOffset DayStart(DateOnly day) => new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), Offset).ToUniversalTime();

    /// <summary>Дата по Ташкенту.</summary>
    public static DateOnly LocalDate(DateTimeOffset moment) => DateOnly.FromDateTime(moment.ToOffset(Offset).DateTime);

    public static DateOnly MonthStart(DateOnly day) => new(day.Year, day.Month, 1);

    public static string Sha256(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    /// <summary>Отпечаток записи источника: то же содержимое — тот же отпечаток.</summary>
    public static string Hash<T>(T record) => Sha256(JsonSerializer.Serialize(record, Json));

    /// <summary>
    /// Вид субконто OneBase по названию вида субконто 1С: counterparty, contract, item, warehouse, batch, department, cost_item, cash_flow_item,
    /// person (персональные данные — AI их не получает) или null (прочие — хранятся как есть).
    /// </summary>
    public static string? SubcontoKind(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return null;
        }

        var t = type.Replace(" ", string.Empty).ToLowerInvariant();
        return t switch
        {
            _ when t.Contains("контрагент") || t.Contains("counterpart") => "counterparty",
            _ when t.Contains("договор") || t.Contains("contract") => "contract",
            _ when t.Contains("номенклатур") || t.Contains("товар") || t.Contains("продукци") || t.Contains("материал") || t.Contains("item") => "item",
            _ when t.Contains("склад") || t.Contains("warehouse") => "warehouse",
            _ when t.Contains("парти") || t.Contains("batch") => "batch",
            _ when t.Contains("подразделени") || t.Contains("department") => "department",
            _ when t.Contains("статьизатрат") || t.Contains("статьязатрат") || t.Contains("costitem") => "cost_item",
            _ when t.Contains("движенияденеж") || t.Contains("ддс") || t.Contains("cashflow") => "cash_flow_item",
            _ when t.Contains("сотрудник") || t.Contains("работник") || t.Contains("физическ") || t.Contains("физлиц") || t.Contains("employee") => "person",
            _ => null,
        };
    }

    /// <summary>Первое значение субконто нужного вида.</summary>
    public static string? SubcontoId(IEnumerable<ImportSubconto> subconto, string kind) =>
        subconto.FirstOrDefault(s => SubcontoKind(s.Type) == kind && !string.IsNullOrEmpty(s.Id))?.Id;

    /// <summary>Ключ набора субконто (для строки остатков): виды и ссылки по порядку.</summary>
    public static string SubcontoKey(IEnumerable<ImportSubconto> subconto) =>
        Sha256(string.Join("|", subconto.OrderBy(s => s.Index).Select(s => $"{s.Index}:{s.Type}:{s.Id ?? s.Name}")))[..16];

    public static string BalanceKey(ImportBalance b) =>
        $"{b.Period:yyyy-MM}:{b.Account}:{b.Currency ?? "-"}:{SubcontoKey(b.Subconto)}";

    private static readonly HashSet<string> MetaFields =
    [
        "Id", "SourceSystem", "SourceOrganizationId", "SourceEntityType", "SourceEntityId", "SourceVersion", "SourceUpdatedAt", "ImportedAt", "UpdatedAt",
        "ContentHash", "LastRunId", "DeletedAt", "SnapshotId", "Lines",
    ];

    private static readonly Dictionary<Type, PropertyInfo[]> Properties = [];

    /// <summary>Значения полей записи (без служебных) — для журнала изменений.</summary>
    public static Dictionary<string, object?> Snapshot(object entity)
    {
        var type = entity.GetType();
        PropertyInfo[] props;
        lock (Properties)
        {
            if (!Properties.TryGetValue(type, out props!))
            {
                props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && !MetaFields.Contains(p.Name)).ToArray();
                Properties[type] = props;
            }
        }

        return props.ToDictionary(p => p.Name, p => p.GetValue(entity));
    }

    /// <summary>Изменённые поля: { поле: [было, стало] } или null, если ничего не изменилось.</summary>
    public static string? Diff(Dictionary<string, object?> before, Dictionary<string, object?> after, Dictionary<string, object?[]>? extra = null)
    {
        var changes = new Dictionary<string, object?[]>();
        foreach (var (name, value) in after)
        {
            before.TryGetValue(name, out var old);
            if (!Equals(old, value))
            {
                changes[name] = [old, value];
            }
        }

        if (extra is not null)
        {
            foreach (var (name, pair) in extra)
            {
                changes[name] = pair;
            }
        }

        return changes.Count == 0 ? null : JsonSerializer.Serialize(changes, Json);
    }

    /// <summary>Текст ошибки для журнала: без переводов строк, не длиннее предела.</summary>
    public static string Short(string? text, int max = 1000)
    {
        var s = (text ?? string.Empty).ReplaceLineEndings(" ").Trim();
        return s.Length <= max ? s : s[..(max - 1)] + "…";
    }
}

/// <summary>Пакет нарушает протокол (400): запрос отклоняется целиком, ничего не записывается.</summary>
public sealed class OneCImportException(string message) : Exception(message);

/// <summary>Состояние не позволяет запись (409): запуск не идёт, пауза, другая база 1С.</summary>
public sealed class OneCConflictException(string message) : Exception(message);
