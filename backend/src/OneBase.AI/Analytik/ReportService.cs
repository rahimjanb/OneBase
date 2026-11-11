using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Analytik;
using OneBase.Application.Analytik.Business;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Risk;
using OneBase.Application.Analytik.Supply;
using OneBase.Application.Reports;
using OneBase.Application.Security;
using OneBase.Domain.Analytik;

namespace OneBase.AI.Analytik;

/// <summary>Таблица раздела отчёта: колонки (форматы — как у отчётов Excel OneBase) и строки.</summary>
public sealed record ReportTable(string Title, IReadOnlyList<AiReportColumn> Columns, IReadOnlyList<IReadOnlyList<JsonElement>> Rows, string? Note = null);

/// <summary>Раздел отчёта: показатели, таблицы, заметки, источники и право просмотра (SalesScoped — только тем, кто видит всю организацию).</summary>
public sealed record ReportSection(string Key, string Title, string Module, string? RequiredPermission, bool SalesScoped, IReadOnlyList<Kpi> Kpis,
    IReadOnlyList<ReportTable> Tables, IReadOnlyList<string> Notes, IReadOnlyList<string> Sources);

/// <summary>Управленческий отчёт: вид, период, дата формирования, краткий вывод (посчитан кодом) и разделы.</summary>
public sealed record ReportDoc(string Title, string Kind, string PeriodLabel, DateOnly From, DateOnly To, DateTimeOffset GeneratedAt, string Summary,
    IReadOnlyList<ReportSection> Sections);

public sealed record ReportListRow(Guid Id, string Kind, string Title, DateOnly From, DateOnly To, DateTimeOffset CreatedAt, bool Scheduled);

/// <summary>
/// Management Reports: ежедневный, еженедельный, ежемесячный и отчёты отделов — из тех же блоков, что панель (цифры считает код, краткий вывод
/// — шаблон по цифрам, без вызова модели). Плановые отчёты (фоновая задача) строятся по всем данным, а при чтении каждому показываются только
/// разделы, на которые у него есть права; отчёт пользователя виден только ему. Выгрузка — Excel и PDF общими писателями отчётов OneBase.
/// </summary>
public sealed class AnalytikReportService(
    IAnalytikDb adb,
    SalesAnalytik sales,
    FinanceAnalytik finance,
    OperationsAnalytik operations,
    SupplyPlanner supply,
    RiskEngine risks,
    ForecastService forecasts,
    IReportWorkbookWriter excel,
    IReportPdfWriter pdf,
    TimeProvider clock)
{
    public static readonly IReadOnlyDictionary<string, string> Kinds = new Dictionary<string, string>
    {
        ["daily"] = "Ежедневный управленческий отчёт",
        ["weekly"] = "Еженедельный отчёт",
        ["monthly"] = "Ежемесячный управленческий отчёт",
        ["sales"] = "Отчёт по продажам",
        ["production"] = "Отчёт по производству",
        ["inventory"] = "Отчёт по складу сырья",
        ["procurement"] = "Отчёт по снабжению",
        ["finance"] = "Финансовый отчёт",
        ["hr"] = "Отчёт по персоналу",
        ["health"] = "Состояние компании",
    };

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    private static bool Has(string kind, params string[] sections) => kind is "daily" or "weekly" or "monthly" or "health" || sections.Contains(kind);

    public async Task<AnalytikReport> CreateAsync(string kind, AnalytikPeriod period, AnalytikPrincipal principal, Guid? createdBy, Guid? jobRunId, CancellationToken ct)
    {
        if (!Kinds.TryGetValue(kind, out var title))
        {
            throw new QueryPlanException(QueryErrors.Invalid, $"Вид отчёта: {string.Join(", ", Kinds.Keys)}.");
        }

        var sections = new List<ReportSection>();
        var summary = new List<string>();
        SalesBlock? salesBlock = null;
        if (Has(kind, "sales", "finance") && (salesBlock = await sales.BlockAsync(period, principal, ct)) is { } s)
        {
            sections.Add(new ReportSection("sales", "Продажи", "sales", Permissions.SalesRead, true, s.Kpis,
            [
                Table("По регионам", [("Регион", ReportFormats.Text), ("Выручка", ReportFormats.Money), ("Продажи, кг", ReportFormats.Kg), ("АКБ", ReportFormats.Integer),
                        ("К прошлому периоду, %", ReportFormats.Percent), ("План месяца, кг", ReportFormats.Kg), ("Выполнение, %", ReportFormats.Percent),
                        ("Прогноз выполнения, %", ReportFormats.Percent)],
                    s.Regions.Select(r => new object?[] { r.Name, r.Revenue, r.Kg, r.Akb, r.ChangePercent, r.PlanKg, r.Execution, r.ForecastExecution })),
            ], s.Notes, s.Sources));
            if (Value(s, "sales.revenue") is { } revenue)
            {
                summary.Add($"Выручка {revenue.Value:N0} сум" + (revenue.ChangePercent is { } c ? $" ({c:+0.#;−0.#}% к прошлому периоду)" : ""));
            }

            if (s.Plan is { Execution: { } e } plan)
            {
                summary.Add($"план месяца выполнен на {e:0.#}%" + (plan.ForecastExecution is { } f ? $", прогноз на конец месяца — {f:0.#}%" : ""));
            }
        }

        if (Has(kind, "finance") && await finance.BlockAsync(period, principal, salesBlock, ct) is { } fin)
        {
            sections.Add(new ReportSection("finance", "Финансы", "finance", Permissions.FinanceRead, true, fin.Kpis,
            [
                Table("Оплаты точек по видам", [("Вид оплаты", ReportFormats.Text), ("Сумма", ReportFormats.Money), ("Оплат", ReportFormats.Integer),
                    ("Прошлый период", ReportFormats.Money)], fin.Payments.Select(p => new object?[] { p.Type, p.Amount, p.Count, p.PreviousAmount })),
            ], fin.Notes, fin.Sources));
        }

        if (Has(kind, "production") && await operations.ProductionAsync(period, principal, ct) is { } prod)
        {
            sections.Add(new ReportSection("production", "Производство", "production", string.Join("|", OperationsAnalytik.ProductionPermissions), false, prod.Kpis,
            [
                Table("Задания периода", [("Задание", ReportFormats.Text), ("Техкарта", ReportFormats.Text), ("Дата", ReportFormats.Date), ("Статус", ReportFormats.Text),
                        ("План", ReportFormats.Number), ("Факт", ReportFormats.Number), ("Выполнение, %", ReportFormats.Percent)],
                    prod.Orders.Select(o => new object?[] { o.Number, o.Recipe, o.PlannedDate.ToString("yyyy-MM-dd"), o.Status, o.PlannedQty, o.OutputQty, o.CompletionPercent })),
                Table("Сколько можно произвести из остатков", [("Техкарта", ReportFormats.Text), ("База", ReportFormats.Number), ("Можно произвести", ReportFormats.Number),
                        ("Ед.", ReportFormats.Text), ("Ограничивает", ReportFormats.Text), ("Сырьё на базу, сум", ReportFormats.Money)],
                    prod.Producible.Select(p => new object?[] { $"{p.Code} {p.Name}", p.BaseQuantity, p.Producible, p.OutputUnit, p.LimitingItem, p.MaterialCost })),
            ], prod.Notes, prod.Sources));
        }

        if (Has(kind, "inventory", "procurement") && SupplyPlanner.CanView(principal))
        {
            var plan = await supply.PlanAsync(principal, ct);
            var permission = string.Join("|", SupplyPlanner.ViewPermissions);
            if (Has(kind, "procurement"))
            {
                var need30 = plan.Horizons.First(h => h.Days == 30);
                sections.Add(new ReportSection("procurement", "Потребность в закупках", "procurement", permission, false,
                [
                    Kpi.Of("procurement.items30", "Позиций к закупке на 30 дней", need30.Items, "шт", null, KpiKinds.Calculated, "Аналитик → План закупок", "/analytik/procurement"),
                    Kpi.Of("procurement.budget30", "Бюджет на 30 дней (по известным ценам)", need30.Budget, "сум", null, KpiKinds.Calculated, "Аналитик → План закупок",
                        "/analytik/procurement", need30.ItemsWithoutPrice > 0 ? $"без цены: {need30.ItemsWithoutPrice} позиций" : null),
                    Kpi.Of("procurement.critical", "Критичный дефицит", plan.Risks.GetValueOrDefault("critical"), "позиций", null, KpiKinds.Calculated, "Аналитик → План закупок",
                        "/analytik/procurement"),
                ],
                [
                    Table("Рекомендации", [("Позиция", ReportFormats.Text), ("Ед.", ReportFormats.Text), ("Доступно", ReportFormats.Number), ("Расход в день", ReportFormats.Number),
                            ("Закончится", ReportFormats.Date), ("Заказать до", ReportFormats.Date), ("Закупить на 30 дней", ReportFormats.Number),
                            ("Стоимость", ReportFormats.Money), ("Поставщик", ReportFormats.Text), ("Риск", ReportFormats.Text)],
                        plan.Items.Where(r => r.Risk != "none").Take(60).Select(r => new object?[]
                        {
                            r.Name, r.Unit, r.Available, r.AvgDailyConsumption, r.ShortageDate?.ToString("yyyy-MM-dd"), r.OrderBy?.ToString("yyyy-MM-dd"),
                            r.Horizon(30)!.Required, r.Horizon(30)!.Cost, r.Supplier, RiskText(r.Risk),
                        })),
                ], plan.DataNotes, plan.Sources));
                if (need30.Items > 0)
                {
                    summary.Add($"к закупке на 30 дней {need30.Items} позиций" + (need30.Budget > 0 ? $" на {need30.Budget:N0} сум (по известным ценам)" : ""));
                }
            }

            if (Has(kind, "inventory"))
            {
                sections.Add(new ReportSection("inventory", "Склад сырья", "procurement", permission, false,
                [
                    Kpi.Of("inventory.value", "Стоимость запаса (по известным ценам)", plan.StockValue, "сум", null, KpiKinds.Calculated, "Склад снабжения", "/analytik/warehouse"),
                    .. plan.WarehouseStatus.Select(kv => Kpi.Of($"inventory.{kv.Key}", StatusText(kv.Key), kv.Value, "позиций", null, KpiKinds.Calculated, "Склад снабжения",
                        "/analytik/warehouse")),
                ],
                [
                    Table("Позиции с риском", [("Позиция", ReportFormats.Text), ("Статус", ReportFormats.Text), ("На складе", ReportFormats.Number), ("Резерв", ReportFormats.Number),
                            ("Доступно", ReportFormats.Number), ("Минимум", ReportFormats.Number), ("Расход в день", ReportFormats.Number), ("Дней запаса", ReportFormats.Number),
                            ("Стоимость", ReportFormats.Money)],
                        plan.Warehouse.Where(w => w.Status is not ("ok" or "empty")).Take(80).Select(w => new object?[]
                            { w.Name, StatusText(w.Status), w.OnHand, w.Reserved, w.Available, w.MinQty, w.AvgDailyConsumption, w.DaysOfCover, w.Value })),
                ], plan.DataNotes, plan.Sources));
            }
        }

        if (Has(kind, "hr") && await operations.HrAsync(period, principal, ct) is { } hrBlock)
        {
            sections.Add(new ReportSection("hr", "Персонал", "hr", Permissions.HrRead, false, hrBlock.Kpis,
            [
                Table("Отклонения по подразделениям", [("Подразделение", ReportFormats.Text), ("Опоздания", ReportFormats.Integer), ("Минут опозданий", ReportFormats.Integer),
                        ("Ранние уходы", ReportFormats.Integer), ("Отсутствия", ReportFormats.Integer), ("Сотрудников с отклонениями", ReportFormats.Integer)],
                    hrBlock.Divisions.Select(d => new object?[] { d.Division, d.Late, d.LateMinutes, d.EarlyLeave, d.Absent, d.Employees })),
            ], hrBlock.Notes, hrBlock.Sources));
        }

        if (kind is "daily" or "weekly" or "monthly" or "health")
        {
            if (await operations.WorkAsync(period, principal, ct) is { } work)
            {
                sections.Add(new ReportSection("work", "Задачи отделов", "work", Permissions.AnalytikManage, false, work.Kpis,
                [
                    Table("По отделам", [("Отдел", ReportFormats.Text), ("Открыто", ReportFormats.Integer), ("Просрочено", ReportFormats.Integer),
                            ("Выполнено за период", ReportFormats.Integer), ("Заблокировано", ReportFormats.Integer), ("В срок, %", ReportFormats.Percent)],
                        work.Departments.Select(d => new object?[] { d.Department, d.Open, d.Overdue, d.DoneInPeriod, d.Blocked, d.OnTimePercent })),
                ], work.Notes, work.Sources));
            }

            var list = await risks.ListAsync(principal, null, false, 100, ct);
            var critical = list.Count(i => i.Severity == Severities.Critical);
            summary.Add(critical == 0 ? "критических рисков нет" : $"критических рисков: {critical}");
            // Риски разных модулей — разные права: раздел отчёта по модулю, чтобы каждый читатель видел только свои.
            foreach (var group in list.GroupBy(i => i.Module))
            {
                sections.Add(new ReportSection($"risks.{group.Key}", $"Риски и отклонения: {ModuleText(group.Key)}", group.Key, PermissionOf(group.Key),
                    group.Key is "sales" or "finance", [],
                    [
                        Table("Находки", [("Критичность", ReportFormats.Text), ("Находка", ReportFormats.Text), ("Причина / гипотезы", ReportFormats.Text),
                                ("Рекомендация", ReportFormats.Text), ("Найдено", ReportFormats.Date)],
                            group.Select(i => new object?[]
                            {
                                RiskText(i.Severity), i.Title, i.CauseConfirmed ? i.Cause : i.Hypotheses.Count > 0 ? "Гипотезы: " + string.Join("; ", i.Hypotheses) : null,
                                i.Recommendations.FirstOrDefault(), i.DetectedAt.ToOffset(TimeSpan.FromHours(5)).ToString("yyyy-MM-dd"),
                            })),
                    ], [], ["Аналитик → Риски"]));
            }
        }

        if (Has(kind, "sales") && SalesAnalytik.CanView(principal))
        {
            try
            {
                var f = await forecasts.ForecastAsync("sales.revenue", "day", 30, principal, ct);
                sections.Add(new ReportSection("forecast", "Прогноз выручки на 30 дней", "sales", Permissions.SalesRead, true,
                [
                    Kpi.Of("forecast.revenue30", "Прогноз выручки на 30 дней", f.Result.Total, "сум", null, KpiKinds.Forecast, "Аналитик → Прогнозы", "/analytik/forecasts",
                        $"диапазон {f.Result.TotalLower:N0} — {f.Result.TotalUpper:N0}; метод {f.Result.Method}"),
                ],
                [
                    Table("По дням", [("Дата", ReportFormats.Date), ("Прогноз", ReportFormats.Money), ("Пессимистичный", ReportFormats.Money), ("Оптимистичный", ReportFormats.Money)],
                        f.Result.Points.Select(p => new object?[] { p.Date.ToString("yyyy-MM-dd"), p.Value, p.Lower, p.Upper })),
                ], f.Result.Assumptions, f.Sources));
            }
            catch (QueryPlanException)
            {
                // Истории нет — раздела прогноза нет.
            }
        }

        var now = clock.GetUtcNow();
        var doc = new ReportDoc($"{title}: {period.Label}", kind, period.Label, period.From, period.To, now,
            summary.Count == 0 ? "Данных для вывода по доступным разделам нет." : Capitalize(string.Join("; ", summary)) + ".", sections);
        var row = new AnalytikReport
        {
            Kind = kind, Title = doc.Title, PeriodFrom = period.From, PeriodTo = period.To, Content = JsonSerializer.Serialize(doc, Json), CreatedById = createdBy,
            JobRunId = jobRunId,
        };
        adb.Reports.Add(row);
        await adb.SaveChangesAsync(ct);
        return row;
    }

    private static string PermissionOf(string module) => module switch
    {
        "sales" => Permissions.SalesRead,
        "finance" => Permissions.FinanceRead,
        "hr" => Permissions.HrRead,
        "production" => string.Join("|", OperationsAnalytik.ProductionPermissions),
        "procurement" => string.Join("|", SupplyPlanner.ViewPermissions),
        "system" => Permissions.IntegrationsManage,
        _ => Permissions.AnalytikManage,
    };

    public static string ModuleText(string module) => module switch
    {
        "sales" => "продажи",
        "finance" => "финансы",
        "production" => "производство",
        "procurement" => "снабжение и склад",
        "hr" => "персонал",
        "system" => "источники данных",
        _ => module,
    };

    private static string RiskText(string risk) => risk switch
    {
        "critical" => "критично",
        "high" => "высокий",
        "warning" => "внимание",
        "medium" => "средний",
        "low" => "низкий",
        "info" => "к сведению",
        _ => risk,
    };

    private static string StatusText(string status) => status switch
    {
        "out" => "Закончилось",
        "deficit" => "Дефицит",
        "below_min" => "Ниже минимума",
        "excess" => "Излишек",
        "dead" => "Без расхода 90 дней",
        "ok" => "Норма",
        "empty" => "Нет на складе",
        _ => status,
    };

    private static Kpi? Value(SalesBlock s, string code) => s.Kpis.FirstOrDefault(k => k.Code == code && k.Value is not null);

    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpper(s[0], Ru) + s[1..];

    private static ReportTable Table(string title, (string Header, string Format)[] columns, IEnumerable<object?[]> rows) =>
        new(title, columns.Select(c => new AiReportColumn(c.Header, c.Format)).ToList(),
            rows.Select(r => (IReadOnlyList<JsonElement>)r.Select(v => JsonSerializer.SerializeToElement(v, Json)).ToList()).ToList());

    // ---- чтение и выгрузка ----

    public async Task<IReadOnlyList<ReportListRow>> ListAsync(AnalytikPrincipal principal, int take, CancellationToken ct) =>
        await adb.Reports.AsNoTracking()
            .Where(r => r.CreatedById == null || r.CreatedById == principal.UserId)
            .OrderByDescending(r => r.CreatedAt).Take(Math.Clamp(take, 1, 200))
            .Select(r => new ReportListRow(r.Id, r.Kind, r.Title, r.PeriodFrom, r.PeriodTo, r.CreatedAt, r.CreatedById == null))
            .ToListAsync(ct);

    /// <summary>Отчёт для читателя: только разделы, на которые у него есть права; чужой отчёт пользователя — null.</summary>
    public async Task<ReportDoc?> GetAsync(Guid id, AnalytikPrincipal principal, CancellationToken ct)
    {
        var row = await adb.Reports.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (row is null || (row.CreatedById is { } owner && owner != principal.UserId))
        {
            return null;
        }

        var doc = JsonSerializer.Deserialize<ReportDoc>(row.Content, Json)!;
        var visible = doc.Sections.Where(s => (s.RequiredPermission is null || principal.HasAny(s.RequiredPermission.Split('|', StringSplitOptions.RemoveEmptyEntries)))
                                              && !(s.SalesScoped && principal.Sales.Restricted)).ToList();
        // Краткий вывод плановых отчётов собран по всем разделам — читателю без части прав он не показывается.
        var summary = visible.Count == doc.Sections.Count ? doc.Summary : "Показаны разделы по вашим правам.";
        return doc with { Sections = visible, Summary = summary };
    }

    public static AiReportContent Content(ReportDoc doc)
    {
        var sheets = new List<AiReportSheet>
        {
            new("Показатели",
            [
                new("Раздел", ReportFormats.Text), new("Показатель", ReportFormats.Text), new("Значение", ReportFormats.Number, false), new("Ед.", ReportFormats.Text),
                new("Прошлый период", ReportFormats.Number, false), new("Изменение, %", ReportFormats.Percent), new("Вид", ReportFormats.Text), new("Источник", ReportFormats.Text),
            ], doc.Sections.SelectMany(s => s.Kpis.Select(k => (IReadOnlyList<JsonElement>)new object?[]
            {
                s.Title, k.Title, k.Value, k.Unit, k.Previous, k.ChangePercent, k.Kind switch { KpiKinds.Forecast => "прогноз", KpiKinds.Calculated => "расчёт", _ => "факт" }, k.Source,
            }.Select(v => JsonSerializer.SerializeToElement(v, Json)).ToList())).ToList(), Note: doc.Summary),
        };
        foreach (var s in doc.Sections)
        {
            foreach (var t in s.Tables.Where(t => t.Rows.Count > 0))
            {
                var name = $"{s.Title} — {t.Title}";
                sheets.Add(new AiReportSheet(name.Length > 31 ? name[..31] : name, t.Columns, t.Rows, false,
                    string.Join(" ", s.Notes.Take(3)) is { Length: > 0 } note ? note : null));
            }
        }

        return new AiReportContent(doc.Title, sheets, doc.Sections.SelectMany(s => s.Sources).Distinct().ToList());
    }

    public byte[] Excel(ReportDoc doc) => excel.Write(Content(doc), doc.GeneratedAt);

    public byte[] Pdf(ReportDoc doc) => pdf.Write(Content(doc), doc.GeneratedAt);
}
