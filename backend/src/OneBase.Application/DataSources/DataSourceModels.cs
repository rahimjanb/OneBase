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
/// <summary>
/// Какие месяцы листа брать (планы и P&L): all — все (за раз не больше 24); current — текущий месяц; year — текущий год; last — последние
/// Months месяцев, включая текущий; range — с From по To («2026-01»). «Текущий» — по Ташкенту на момент синхронизации: период сдвигается
/// сам. Строки вне периода не пишутся и не удаляются.
/// </summary>
public sealed record PeriodFilter(string Mode, int? Months = null, string? From = null, string? To = null);

/// <remarks>
/// Period — какие месяцы листа брать (null — все). Headers — снимок заголовков на момент сопоставления (столбец сопоставления → текст заголовка): синхронизация сверяет с ним лист и при
/// переименовании или перестановке столбцов ничего не пишет (StructureError), а не читает не тот столбец. null — старая привязка, без сверки.
/// </remarks>
public sealed record DataSourceFieldBinding(string Field, PlanFieldMapping? Plan = null, ProfileFieldMapping? Profile = null, PnlSheetMapping? Pnl = null,
    bool Replace = true, bool MonthFromSheet = false, bool CurrentMonth = false, IReadOnlyDictionary<string, string>? Headers = null,
    PeriodFilter? Period = null);

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

/// <summary>Лист таблицы Google: название и размер (строк и столбцов).</summary>
public sealed record SheetInfo(string Name, int Rows, int Columns);

/// <summary>
/// Чтение таблицы Google: список листов и значения ячеек только нужных листов — большая книга не читается целиком.
/// Реализация — Infrastructure.
/// </summary>
public interface IGoogleSheetsClient
{
    /// <summary>Листы таблицы с размерами — без значений.</summary>
    /// <exception cref="DataSourceException">Нет доступа, таблица не найдена, сеть — текст для администратора.</exception>
    Task<IReadOnlyList<SheetInfo>> SheetsAsync(SheetSource source, CancellationToken ct = default);

    /// <summary>Значения листов sheets («*» — всех) в порядке sheets, с названиями как в таблице.</summary>
    /// <exception cref="DataSourceException">Листов нет в таблице (с перечнем), нет доступа, сеть.</exception>
    Task<ReportDocument> ReadAsync(SheetSource source, IReadOnlyCollection<string> sheets, CancellationToken ct = default);
}

public static partial class DataSourceRules
{
    public const string Manual = "manual";
    public const string Interval = "interval";
    public const string Daily = "daily";

    /// <summary>Чаще 5 минут не синхронизируем: Google ограничивает выгрузку таблиц, а план так часто не меняется.</summary>
    public const int MinIntervalMinutes = 5;

    public const string PeriodAll = "all";
    public const string PeriodCurrent = "current";
    public const string PeriodYear = "year";
    public const string PeriodLast = "last";
    public const string PeriodRangeMode = "range";

    /// <summary>Месяцев за одну запись плана или P&L (как у AI Data Manager).</summary>
    public const int MaxPeriodMonths = 24;

    /// <summary>Месяцы периода включительно (первые числа) на момент now; null — все месяцы листа.</summary>
    public static (DateOnly From, DateOnly To)? PeriodRange(PeriodFilter? period, DateTimeOffset now)
    {
        var (year, month) = CurrentPeriod(now);
        var current = new DateOnly(year, month, 1);
        switch (period?.Mode)
        {
            case PeriodCurrent:
                return (current, current);
            case PeriodYear:
                return (new DateOnly(year, 1, 1), new DateOnly(year, 12, 1));
            case PeriodLast:
                return (current.AddMonths(-(Math.Clamp(period.Months ?? 1, 1, MaxPeriodMonths) - 1)), current);
            case PeriodRangeMode when MonthOf(period.From) is { } from && MonthOf(period.To) is { } to:
                return (from, to);
            default:
                return null;
        }
    }

    /// <summary>Строка в периоде. Нераспознанный месяц (0) — оставляем: строка станет ошибкой в журнале, а не пропадёт молча.</summary>
    public static bool InPeriod((DateOnly From, DateOnly To)? range, int year, int month)
    {
        if (range is not { } r || year is < 1 or > 9999 || month is < 1 or > 12)
        {
            return true;
        }

        var date = new DateOnly(year, month, 1);
        return date >= r.From && date <= r.To;
    }

    public static string PeriodLabel(PeriodFilter? period) => period?.Mode switch
    {
        PeriodCurrent => "текущий месяц",
        PeriodYear => "текущий год",
        PeriodLast => $"последние {period.Months} мес.",
        PeriodRangeMode => $"{period.From} — {period.To}",
        _ => "все месяцы",
    };

    /// <summary>Ошибка периода поля; null — всё верно.</summary>
    public static string? PeriodError(PeriodFilter? period)
    {
        switch (period?.Mode)
        {
            case null or PeriodAll or PeriodCurrent or PeriodYear:
                return null;
            case PeriodLast:
                return period.Months is null or < 1 or > MaxPeriodMonths ? $"последних месяцев — от 1 до {MaxPeriodMonths}" : null;
            case PeriodRangeMode:
                if (MonthOf(period.From) is not { } from || MonthOf(period.To) is not { } to)
                {
                    return "период — месяцы в формате ГГГГ-ММ";
                }

                var months = (to.Year - from.Year) * 12 + to.Month - from.Month + 1;
                return months < 1 ? "начало периода позже конца" : months > MaxPeriodMonths ? $"период — не больше {MaxPeriodMonths} месяцев" : null;
            default:
                return "период: все месяцы, текущий месяц, текущий год, последние N месяцев или диапазон";
        }
    }

    private static DateOnly? MonthOf(string? text) =>
        DateOnly.TryParseExact($"{text?.Trim()}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    /// <summary>Расписание новой привязки по умолчанию — каждые 30 минут.</summary>
    public const int DefaultIntervalMinutes = 30;

    /// <summary>Ошибка синхронизации по расписанию (Google недоступен, сеть) — один повтор через столько минут, дальше — по расписанию.</summary>
    public const int RetryMinutes = 5;

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
            if (field.Kind != "profile" && PeriodError(f.Period) is { } periodError)
            {
                return $"«{field.Label}»: {periodError}.";
            }

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

    /// <summary>
    /// Структура листа против снимка заголовков поля: столбец по заголовку — заголовок должен быть на месте (порядок столбцов не важен);
    /// столбец по букве (заголовок повторялся или пуст) — в этой букве тот же заголовок. Иначе — текст ошибки: поле не пишется, пока
    /// столбцы не сопоставят заново. null — всё на месте или снимка нет.
    /// </summary>
    public static string? StructureError(ReportDocument document, IReadOnlyList<string> sheets, int headerRow, DataSourceFieldBinding field)
    {
        if (field.Headers is not { Count: > 0 } headers)
        {
            return null;
        }

        var problems = new List<string>();
        foreach (var sheet in document.Sheets.Where(s => sheets.Contains(s.Name)))
        {
            var header = headerRow >= 1 && headerRow <= sheet.Rows.Count ? sheet.Rows[headerRow - 1] : [];
            var names = header.Select(h => DataValues.Normalize(h)).ToList();
            foreach (var (column, saved) in headers)
            {
                if (names.Contains(DataValues.Normalize(column)))
                {
                    continue;
                }

                if (DataValues.ColumnIndex(column) is not { } index)
                {
                    problems.Add($"«{sheet.Name}»: столбца «{column}» больше нет");
                    continue;
                }

                var now = index < header.Count ? header[index].Trim() : string.Empty;
                if (DataValues.Normalize(now) != DataValues.Normalize(saved))
                {
                    problems.Add($"«{sheet.Name}»: в столбце {column} теперь «{now}», а при сопоставлении был «{saved}»");
                }
            }
        }

        return problems.Count == 0
            ? null
            : $"структура листа изменилась — {string.Join("; ", problems.Distinct().Take(5))}. Ничего не записано: откройте привязку и сопоставьте " +
              "столбцы заново (можно «Сопоставить с помощью ИИ»)";
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

    /// <summary>
    /// Какие листы таблицы читать: «все» — все листы; иначе выбранные (названия — как в таблице), а пропавших нет — ошибка с перечнем.
    /// names — листы таблицы.
    /// </summary>
    public static IReadOnlyList<string> ResolveSheets(IReadOnlyList<string> names, IReadOnlyCollection<string> sheets)
    {
        if (sheets.Contains(AllSheets))
        {
            return names.Count == 0 ? throw new DataSourceException("В таблице нет листов.") : names;
        }

        var found = sheets.Select(s => (Wanted: s, Name: names.FirstOrDefault(n => DataValues.Normalize(n) == DataValues.Normalize(s)))).ToList();
        var missing = found.Where(f => f.Name is null).Select(f => f.Wanted).ToList();
        return missing.Count > 0
            ? throw new DataSourceException($"В таблице нет листов {string.Join(", ", missing.Select(m => $"«{m}»"))}. Листы: {string.Join(", ", names)}.")
            : found.Select(f => f.Name!).ToList();
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
