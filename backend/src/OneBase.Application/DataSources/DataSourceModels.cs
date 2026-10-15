using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using OneBase.Application.Abstractions;
using OneBase.Application.DataManager;
using OneBase.Domain.Integrations;
using OneBase.Domain.Sales;

namespace OneBase.Application.DataSources;

/// <summary>Ошибка настройки источника — текст для администратора.</summary>
public sealed class DataSourceException(string message) : Exception(message);

/// <summary>Поле OneBase, которое можно обновлять из Google Sheets. Kind: plan (регион × категория × месяц), profile (регион → текст), pnl.</summary>
public sealed record DataSourceField(string Key, string Label, string Kind, string Description, PlanKind? PlanKind = null, string? ProfileField = null);

/// <summary>Раздел продаж: откуда факт (всегда Linko) и какие поля OneBase раздела можно брать из Google Sheets.</summary>
public sealed record DataSourceSection(string Key, string Name, string Href, string Facts, IReadOnlyList<string> Fields);

/// <summary>
/// Разделы и поля. Факт продаж, остатков, визитов и перемещений приходит только из Linko; из Google Sheets обновляются данные, которые
/// ведёт сам OneBase: планы регионов, дилеры и СВР регионов, P&L дилеров. Одно поле — одна привязка (иначе две таблицы спорили бы).
/// </summary>
public static class DataSourceCatalog
{
    public const string PlanRop = "plan_rop";
    public const string PlanFactory = "plan_factory";
    public const string PlanPrimary = "plan_primary";
    public const string Dealer = "dealer";
    public const string Supervisor = "supervisor";
    public const string Pnl = "pnl";

    public static readonly IReadOnlyList<DataSourceField> Fields =
    [
        new(PlanRop, "План РОП", "plan", "Регион × категория × месяц, кг. Вторичка: выполнение, прогноз, рейтинг регионов и РМ.", PlanKind.Rop),
        new(PlanFactory, "План «Завод»", "plan", "Регион × категория × месяц, кг. Переключатель «План «Завод»» во вторичке.", PlanKind.Factory),
        new(PlanPrimary, "План первички", "plan", "Дилер (регион) × категория × месяц, кг. Первичка: план, выполнение, остаток.", PlanKind.Primary),
        new(Dealer, "Дилер региона", "profile", "Подпись строк первички, P&L и карточек регионов («Бобур Самарканд»).", null, RegionProfilePlanner.Dealer),
        new(Supervisor, "СВР региона", "profile", "Супервайзер региона во вторичке.", null, RegionProfilePlanner.Supervisor),
        new(Pnl, "P&L дилеров", "pnl", "Продажи, себестоимость и расходы по дилерам за месяц."),
    ];

    public static readonly IReadOnlyList<DataSourceSection> Sections =
    [
        new("secondary", "Вторичка", "/sales", "Заказы, возвраты, визиты, точки и агенты — Linko", [PlanRop, PlanFactory, Supervisor, Dealer]),
        new("assortment", "Ассортимент", "/sales/assortment", "Заказы и товары — Linko", []),
        new("sku", "Продажи по SKU", "/sales/sku-sales", "Заказы и товары — Linko", []),
        new("primary", "Первичка", "/sales/primary", "Перемещения завод → дилеры и заказы прямых клиентов — Linko", [PlanPrimary, Dealer]),
        new("stock", "Рек. остаток", "/sales/stock", "Остатки складов и продажи — Linko", []),
        new("outstock", "Аутсток", "/sales/outstock", "Остатки по дням и заказы — Linko", []),
        new("plans", "Планы", "/sales/plans", "Планы ТП — API планов Linko", [PlanRop, PlanFactory, PlanPrimary]),
        new("problems", "Проблемные агенты", "/sales/problems", "Визиты, заказы и планы ТП — Linko", []),
        new("pnl", "P&L дилеров", "/sales/pnl", "Данные OneBase (Linko не ведёт P&L)", [Pnl]),
    ];

    public static DataSourceField? Field(string key) => Fields.FirstOrDefault(f => f.Key == key);

    public static DataSourceSection? Section(string key) => Sections.FirstOrDefault(s => s.Key == key);
}

/// <summary>Сопоставление плана: столбец региона, категории — столбцом или столбцами-категориями, месяц — столбцом или значением.</summary>
public sealed record PlanFieldMapping(
    string RegionColumn,
    string? CategoryColumn,
    string? ValueColumn,
    IReadOnlyList<PlanColumn>? ValueColumns,
    string? MonthColumn,
    string? YearColumn,
    int? Year,
    int? Month,
    decimal Multiplier = 1m);

/// <summary>Сопоставление дилера или СВР: столбец региона и столбец значения.</summary>
public sealed record ProfileFieldMapping(string RegionColumn, string ValueColumn);

/// <summary>
/// Поле привязки: какое поле OneBase обновлять и как читать его из листа. Replace (планы и P&L) — таблица полная за свои месяцы:
/// строки этих месяцев, которых в таблице нет, удаляются; иначе — только добавляются и обновляются. Месяц (планы и P&L): MonthFromSheet —
/// из названия листа («Сентябрь 2026», «09.2026»): лист на месяц; CurrentMonth — текущий месяц по Ташкенту на момент синхронизации:
/// лист «План» всегда про текущий месяц, с 1-го числа значения идут в новый месяц.
/// </summary>
public sealed record DataSourceFieldBinding(string Field, PlanFieldMapping? Plan = null, ProfileFieldMapping? Profile = null, PnlSheetMapping? Pnl = null,
    bool Replace = true, bool MonthFromSheet = false, bool CurrentMonth = false);

/// <summary>
/// Итог синхронизации поля: операция AI Data Manager, строки, ошибки. Period и Fingerprint — у поля «текущий месяц»: за какой месяц
/// записано и отпечаток значений листа (без месяца) — чтобы в новом месяце не скопировать в него прошлый план.
/// </summary>
public sealed record DataSourceFieldResult(
    string Field,
    string Label,
    string Status,
    string Message,
    Guid? ChangeSetId = null,
    int Created = 0,
    int Updated = 0,
    int Deleted = 0,
    int Conflicts = 0,
    IReadOnlyList<string>? Problems = null,
    string? Period = null,
    string? Fingerprint = null);

/// <summary>Откуда читать таблицу: ID, доступ и (для сервисного аккаунта) ключ JSON.</summary>
public sealed record SheetSource(string SpreadsheetId, SheetAccess Access, string? ServiceAccountJson);

/// <summary>Чтение таблицы Google целиком (все листы, значения ячеек). Реализация — Infrastructure.</summary>
public interface IGoogleSheetsClient
{
    /// <exception cref="DataSourceException">Нет доступа, таблица не найдена, сеть — текст для администратора.</exception>
    Task<ReportDocument> ReadAsync(SheetSource source, CancellationToken ct = default);
}

public static partial class DataSourceRules
{
    public const string Manual = "manual";
    public const string Interval = "interval";
    public const string Daily = "daily";

    /// <summary>Чаще 5 минут не синхронизируем: Google ограничивает выгрузку таблиц, а план так часто не меняется.</summary>
    public const int MinIntervalMinutes = 5;

    /// <summary>«Все листы таблицы» в списке листов привязки — и те, что появятся позже.</summary>
    public const string AllSheets = "*";

    public const int MaxSheets = 60;

    /// <summary>Итоги синхронизации: записано, без изменений, ошибки в строках, ждёт обновления таблицы (новый месяц), ошибка.</summary>
    public const string Ok = "ok";
    public const string Unchanged = "unchanged";
    public const string Conflicts = "conflicts";
    public const string Waiting = "waiting";
    public const string Error = "error";

    /// <summary>Итог привязки по полям: худший из них (ошибка → ошибки строк → ждёт таблицу → записано → без изменений).</summary>
    public static string Overall(IReadOnlyCollection<string> statuses) =>
        new[] { Error, Conflicts, Waiting, Ok }.FirstOrDefault(statuses.Contains) ?? Unchanged;

    /// <summary>Текущий месяц по Ташкенту.</summary>
    public static (int Year, int Month) CurrentPeriod(DateTimeOffset now)
    {
        var local = now.ToOffset(CompanyOffset);
        return (local.Year, local.Month);
    }

    public static string PeriodKey(int year, int month) => $"{year:D4}-{month:D2}";

    /// <summary>Отпечаток значений листа без месяца: те же строки и цифры — тот же отпечаток, в каком бы месяце их ни прочитали.</summary>
    public static string Fingerprint(IEnumerable<string> lines)
    {
        var text = string.Join("\n", lines.Select(DataValues.Normalize).Order(StringComparer.Ordinal));
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)))[..16];
    }

    /// <summary>
    /// В новом месяце значения листа «текущий месяц» те же, что записаны за прошлый, — по расписанию не пишем: иначе в новый месяц
    /// скопировался бы прошлый план, пока таблицу не обновили. true — ждать обновления таблицы.
    /// </summary>
    public static bool SameAsPreviousMonth(string period, string fingerprint, string? lastPeriod, string? lastFingerprint) =>
        lastPeriod is not null && lastPeriod != period && lastFingerprint == fingerprint;

    /// <summary>Узбекистан — UTC+5 без перехода на летнее время.</summary>
    public static readonly TimeSpan CompanyOffset = TimeSpan.FromHours(5);

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>ID таблицы из ссылки (…/spreadsheets/d/{id}/…) или сам ID; null — не похоже на таблицу Google.</summary>
    public static string? SpreadsheetId(string? urlOrId)
    {
        var text = (urlOrId ?? string.Empty).Trim();
        var match = SheetUrl().Match(text);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        return PlainId().IsMatch(text) ? text : null;
    }

    /// <summary>Следующая синхронизация после момента after; null — только вручную.</summary>
    public static DateTimeOffset? NextRun(string schedule, int? intervalMinutes, string? dailyAt, DateTimeOffset after)
    {
        switch (schedule)
        {
            case Interval:
                return after.AddMinutes(Math.Max(MinIntervalMinutes, intervalMinutes ?? 60));
            case Daily when TimeOnly.TryParseExact(dailyAt, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time):
                var local = after.ToOffset(CompanyOffset);
                var today = new DateTimeOffset(local.Date + time.ToTimeSpan(), CompanyOffset);
                return (today > local ? today : today.AddDays(1)).ToUniversalTime();
            default:
                return null;
        }
    }

    /// <summary>Проверка расписания; null — всё верно, иначе текст ошибки.</summary>
    public static string? ScheduleError(string schedule, int? intervalMinutes, string? dailyAt) => schedule switch
    {
        Manual => null,
        Interval when intervalMinutes is null or < MinIntervalMinutes or > 7 * 24 * 60 => $"Интервал — от {MinIntervalMinutes} минут до 7 дней.",
        Interval => null,
        Daily when !TimeOnly.TryParseExact(dailyAt, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) => "Время синхронизации — в формате ЧЧ:ММ.",
        Daily => null,
        _ => "Расписание: вручную, каждые N минут или ежедневно.",
    };

    /// <summary>Поля привязки из JSON (jsonb).</summary>
    public static IReadOnlyList<DataSourceFieldBinding> ReadFields(string? json) =>
        string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<DataSourceFieldBinding>>(json, Json) ?? [];

    /// <summary>
    /// Проверка полей привязки раздела: поле есть в разделе, у него есть сопоставление нужного вида, поле выбрано один раз.
    /// null — всё верно, иначе текст ошибки.
    /// </summary>
    public static string? FieldsError(string section, IReadOnlyList<DataSourceFieldBinding> fields)
    {
        var info = DataSourceCatalog.Section(section);
        if (info is null)
        {
            return $"Раздела «{section}» нет.";
        }

        if (fields.Count == 0)
        {
            return "Выберите хотя бы одно поле, которое нужно обновлять из таблицы.";
        }

        foreach (var f in fields)
        {
            var field = DataSourceCatalog.Field(f.Field);
            if (field is null || !info.Fields.Contains(f.Field))
            {
                return $"Поле «{f.Field}» не относится к разделу «{info.Name}».";
            }

            var missing = field.Kind switch
            {
                "plan" => f.Plan is null || string.IsNullOrWhiteSpace(f.Plan.RegionColumn)
                          || (f.Plan.ValueColumns is not { Count: > 0 } && string.IsNullOrWhiteSpace(f.Plan.ValueColumn))
                          || (string.IsNullOrWhiteSpace(f.Plan.MonthColumn) && f.Plan.Month is null && !f.MonthFromSheet && !f.CurrentMonth),
                "profile" => f.Profile is null || string.IsNullOrWhiteSpace(f.Profile.RegionColumn) || string.IsNullOrWhiteSpace(f.Profile.ValueColumn),
                _ => f.Pnl is null,
            };
            if (missing)
            {
                return field.Kind switch
                {
                    "plan" => $"«{field.Label}»: укажите столбцы региона, значения (или столбцы категорий) и месяц.",
                    "profile" => $"«{field.Label}»: укажите столбцы региона и значения.",
                    _ => $"«{field.Label}»: укажите сопоставление столбцов.",
                };
            }
        }

        var twice = fields.GroupBy(f => f.Field).FirstOrDefault(g => g.Count() > 1);
        return twice is null ? null : $"Поле «{DataSourceCatalog.Field(twice.Key)!.Label}» выбрано дважды.";
    }

    /// <summary>Листы привязки из JSON (jsonb).</summary>
    public static IReadOnlyList<string> ReadSheets(string? json) =>
        string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<string>>(json, Json) ?? [];

    /// <summary>Подпись листов: «Лист1», «Январь», «Февраль» или «все листы».</summary>
    public static string SheetsLabel(IReadOnlyList<string> sheets) =>
        sheets.Contains(AllSheets) ? "все листы" : string.Join(", ", sheets.Select(s => $"«{s}»"));

    /// <summary>Чистый список листов: без пустых и повторов; «все листы» — один элемент «*». null — ошибок нет, иначе текст ошибки.</summary>
    public static (IReadOnlyList<string> Sheets, string? Error) CleanSheets(IReadOnlyList<string>? sheets)
    {
        var list = (sheets ?? []).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (list.Contains(AllSheets))
        {
            return ([AllSheets], null);
        }

        return list.Count == 0 ? (list, "Выберите хотя бы один лист таблицы.")
            : list.Count > MaxSheets ? (list, $"Листов — не больше {MaxSheets}; для всех листов выберите «Все листы».")
            : list.Any(s => s.Length > 200) ? (list, "Название листа — до 200 символов.")
            : (list, null);
    }

    /// <summary>Листы документа для синхронизации: «все» — все листы; иначе выбранные, а пропавших нет — ошибка с перечнем.</summary>
    public static IReadOnlyList<string> ResolveSheets(ReportDocument document, IReadOnlyList<string> sheets)
    {
        if (sheets.Contains(AllSheets))
        {
            return document.Sheets.Count == 0 ? throw new DataSourceException("В таблице нет листов.") : document.Sheets.Select(s => s.Name).ToList();
        }

        var names = document.Sheets.Select(s => s.Name).ToList();
        var missing = sheets.Where(s => !names.Any(n => DataValues.Normalize(n) == DataValues.Normalize(s))).ToList();
        return missing.Count > 0
            ? throw new DataSourceException($"В таблице нет листов {string.Join(", ", missing.Select(m => $"«{m}»"))}. Листы: {string.Join(", ", names)}.")
            : sheets;
    }

    /// <summary>Месяц из названия листа («Сентябрь 2026», «09.2026», «2026-09»); год — из названия или fallbackYear.</summary>
    public static (int Year, int Month) SheetMonth(string sheetName, int? fallbackYear)
    {
        var (year, month) = DataValues.ParseMonth(sheetName);
        if (month is null)
        {
            throw new DataManagerException($"из названия листа «{sheetName}» не понять месяц — назовите его «Сентябрь 2026» или «09.2026»");
        }

        return (year ?? fallbackYear ?? throw new DataManagerException($"в названии листа «{sheetName}» нет года — укажите год в привязке"), month.Value);
    }

    /// <summary>
    /// Дилер или СВР по регионам из листа: строка — регион и значение. Пустое значение не стирает записанное в OneBase (строка
    /// пропускается), строки «Итого» и без региона — тоже.
    /// </summary>
    public static IReadOnlyList<RegionProfileValue> ProfileValues(ReportDocument document, string sheetName, int headerRow, ProfileFieldMapping map, string field)
    {
        var sheet = ReportExtraction.Sheet(document, sheetName);
        if (headerRow < 1 || headerRow > sheet.Rows.Count)
        {
            throw new DataManagerException($"Строка заголовка {headerRow} вне листа (строк: {sheet.Rows.Count}).");
        }

        var regionCol = ReportExtraction.Column(sheet, headerRow, map.RegionColumn);
        var valueCol = ReportExtraction.Column(sheet, headerRow, map.ValueColumn);
        var values = new List<RegionProfileValue>();
        for (var r = headerRow + 1; r <= sheet.Rows.Count; r++)
        {
            var row = sheet.Rows[r - 1];
            var region = ReportExtraction.Cell(row, regionCol);
            var value = ReportExtraction.Cell(row, valueCol);
            if (region.Length == 0 || value.Length == 0 || ReportExtraction.IsSummary(region))
            {
                continue;
            }

            values.Add(new RegionProfileValue(region, field, value, $"{sheet.Name}!{DataValues.ColumnLetter(valueCol)}{r}"));
        }

        return values;
    }

    [GeneratedRegex(@"/spreadsheets/d/([A-Za-z0-9_-]{20,})")]
    private static partial Regex SheetUrl();

    [GeneratedRegex(@"^[A-Za-z0-9_-]{25,}$")]
    private static partial Regex PlainId();
}
