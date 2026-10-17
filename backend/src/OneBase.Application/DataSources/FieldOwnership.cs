using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.DataManager;
using OneBase.Domain.Integrations;
using OneBase.Domain.Sales;

namespace OneBase.Application.DataSources;

/// <summary>Поле OneBase, которое ведёт включённая привязка Google Sheets: её таблица, листы и раздел.</summary>
public sealed record FieldOwner(string Field, string Label, Guid BindingId, string Section, string Connection, IReadOnlyList<string> Sheets)
{
    public string Rule => $"поле ведёт Google Sheets «{Connection}» ({DataSourceRules.SheetsLabel(Sheets)})";
}

/// <summary>Кто пытался записать поле — для журнала конфликтов.</summary>
public static class ConflictSources
{
    public const string GoogleSheets = "google_sheets";
    public const string DataManager = "data_manager";
    public const string Manual = "manual";

    public static string Label(string source) => source switch
    {
        GoogleSheets => "Google Sheets",
        DataManager => "AI Data Manager",
        Manual => "Ручная правка",
        _ => source,
    };
}

/// <summary>
/// Владение полями OneBase (Source of Truth). Факт — только Linko: синхронизация Linko пишет свою копию данных и планы ТП, а поля OneBase
/// (планы регионов, дилер и СВР региона, P&L) не трогает. Поле OneBase ведёт либо включённая привязка Google Sheets — тогда меняет его
/// только её синхронизация, — либо сам OneBase: AI Data Manager (чат, файлы) и «Настройки продаж». Чужое поле не записывается: попытка —
/// конфликт в журнале, подтверждённое значение остаётся. «Последняя запись победила» между источниками не действует.
/// </summary>
public sealed class FieldOwnership(IAppDbContext db, TimeProvider clock)
{
    /// <summary>Поля, которые ведут включённые привязки Google Sheets (одно поле — одна привязка).</summary>
    public async Task<IReadOnlyDictionary<string, FieldOwner>> OwnersAsync(CancellationToken ct = default)
    {
        var bindings = await db.DataSourceBindings.AsNoTracking()
            .Where(b => b.Enabled)
            .OrderBy(b => b.CreatedAt)
            .Select(b => new { b.Id, b.Section, b.Fields, b.Sheets, Connection = b.Connection!.Name })
            .ToListAsync(ct);
        var owners = new Dictionary<string, FieldOwner>();
        foreach (var b in bindings)
        {
            foreach (var f in DataSourceRules.ReadFields(b.Fields))
            {
                if (DataSourceCatalog.Field(f.Field) is { } field)
                {
                    owners.TryAdd(field.Key, new FieldOwner(field.Key, field.Label, b.Id, b.Section, b.Connection, DataSourceRules.ReadSheets(b.Sheets)));
                }
            }
        }

        return owners;
    }

    /// <summary>Поле OneBase для вида плана регионов.</summary>
    public static string? PlanField(PlanKind kind) => DataSourceCatalog.Fields.FirstOrDefault(f => f.PlanKind == kind)?.Key;

    /// <summary>
    /// Поля, которые меняет план изменений AI Data Manager: планы регионов — по виду плана, профиль региона — по полям строк (dealer,
    /// supervisor), P&L — pnl. Цели (sales_targets) не ведёт ни один внешний источник — полей нет.
    /// </summary>
    public static IReadOnlyList<string> Fields(string target, string? planKind, IEnumerable<string> profileFields) => target switch
    {
        DataTargets.RegionPlans when Enum.TryParse<PlanKind>(planKind, out var kind) && PlanField(kind) is { } key => [key],
        DataTargets.RegionProfiles => profileFields.Select(f => f.Trim().ToLowerInvariant())
            .Where(f => f is RegionProfilePlanner.Dealer or RegionProfilePlanner.Supervisor)
            .Distinct()
            .ToList(),
        DataTargets.Pnl => [DataSourceCatalog.Pnl],
        _ => [],
    };

    /// <summary>Поля, которыми источник bindingId (null — не Google Sheets) не владеет: их ведёт другая привязка.</summary>
    public async Task<IReadOnlyList<FieldOwner>> ForeignAsync(IReadOnlyCollection<string> fields, Guid? bindingId, CancellationToken ct = default)
    {
        if (fields.Count == 0)
        {
            return [];
        }

        var owners = await OwnersAsync(ct);
        return fields.Select(f => owners.GetValueOrDefault(f)).OfType<FieldOwner>().Where(o => o.BindingId != bindingId).ToList();
    }

    /// <summary>
    /// Проверка перед записью: поле, которое ведёт привязка Google Sheets, меняет только она. Чужое поле — отказ (в журнал конфликтов)
    /// и <see cref="DataManagerException"/> с объяснением; значения не меняются.
    /// </summary>
    public async Task EnsureAsync(IReadOnlyCollection<string> fields, Guid? bindingId, string source, Guid? userId, string? entity, string? period,
        CancellationToken ct = default)
    {
        var foreign = await ForeignAsync(fields, bindingId, ct);
        if (foreign.Count == 0)
        {
            return;
        }

        await RecordRefusalAsync(foreign, source, userId, entity, period, ct);
        throw new DataManagerException(Refusal(foreign));
    }

    /// <summary>Отказ в журнал конфликтов: событие (закрыто сразу), вмешательство не нужно — значение источника-владельца осталось.</summary>
    public async Task RecordRefusalAsync(IReadOnlyList<FieldOwner> foreign, string source, Guid? userId, string? entity, string? period, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        foreach (var owner in foreign)
        {
            db.DataSourceConflicts.Add(new DataSourceConflict
            {
                Source = source,
                Field = owner.Field,
                Entity = Cut(entity, 300),
                Period = Cut(period, 32),
                Kind = ConflictKinds.Owner,
                Message = Cut($"{ConflictSources.Label(source)}: попытка изменить «{owner.Label}» — этим полем владеет Google Sheets", 1000)!,
                Rule = Cut(owner.Rule, 300),
                Decision = "отклонено: значение из Google Sheets сохранено",
                NeedsAction = false,
                Fingerprint = Fingerprint(null, owner.Field, ConflictKinds.Owner, entity, source),
                LastSeenAt = now,
                ResolvedAt = now,
                UserId = userId,
            });
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Текст отказа: кто ведёт поле и что делать.</summary>
    public static string Refusal(IReadOnlyList<FieldOwner> foreign) =>
        string.Join(" ", foreign.Select(o => $"«{o.Label}»: {o.Rule}.")) +
        " Меняйте значения в таблице — следующая синхронизация перезаписала бы правку в OneBase. Чтобы править здесь, отключите или удалите " +
        "привязку в «Настройки → Источники данных».";

    /// <summary>Отпечаток конфликта: одна и та же проблема при каждой синхронизации — одна запись.</summary>
    public static string Fingerprint(Guid? bindingId, string field, string kind, string? entity, string? message)
    {
        var text = string.Join("|", bindingId?.ToString("N") ?? "-", field, kind, DataValues.Normalize(entity), DataValues.Normalize(message));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..32];
    }

    private static string? Cut(string? text, int max) => text is null ? null : text.Length <= max ? text : text[..(max - 1)] + "…";
}

public static class ConflictKinds
{
    /// <summary>Строка таблицы не записана: регион не найден, не число, разные значения у одного региона.</summary>
    public const string Row = "row";

    /// <summary>Попытка изменить поле, которым источник не владеет.</summary>
    public const string Owner = "owner";

    public static string Label(string kind) => kind switch
    {
        Row => "Строка таблицы",
        Owner => "Чужое поле",
        _ => kind,
    };
}
