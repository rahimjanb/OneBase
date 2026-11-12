using System.Text.Json;
using OneBase.Application.Abstractions;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Simulation;
using OneBase.Application.Reports;
using OneBase.Domain.Audit;

namespace OneBase.AI.Analytik.Simulation;

/// <summary>Файл экспорта: содержимое, тип, имя.</summary>
public sealed record ExportFile(byte[] Content, string ContentType, string FileName);

/// <summary>
/// Экспорт сценария в Excel и PDF (общие построители отчётов OneBase): название и сводка, изменённые параметры, исходные показатели (с
/// происхождением: OneBase или введено), результаты «база → сценарий» с графиками, ограничения, риски, рекомендации, товары, регионы, сырьё и
/// закупки, сравнение с другими сценариями (если выбраны), источники и допущения, AI-заключение, дата расчёта.
/// </summary>
public sealed class ScenarioExport(ScenarioService scenarios, IReportWorkbookWriter excel, IReportPdfWriter pdf, IAuditLogger audit, TimeProvider clock)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<ExportFile> FileAsync(AnalytikPrincipal p, Guid id, string format, IReadOnlyList<Guid> compare, CancellationToken ct)
    {
        if (format is not ("xlsx" or "pdf"))
        {
            throw new ArgumentException("Формат экспорта: xlsx или pdf.");
        }

        var view = await scenarios.GetAsync(p, id, ct);
        if (view.Result is not { Valid: true })
        {
            throw new ArgumentException("Сценарий не посчитан — экспортировать нечего.");
        }

        ScenarioComparison? comparison = null;
        var others = compare.Where(x => x != id).Distinct().Take(ScenarioService.MaxCompare - 1).ToList();
        if (others.Count > 0)
        {
            comparison = await scenarios.CompareAsync(p, [id, .. others], ct);
        }

        var content = Content(view, comparison);
        var now = clock.GetUtcNow();
        var bytes = format == "pdf" ? pdf.Write(content, now) : excel.Write(content, now);
        await audit.LogAsync(ActorType.User, p.UserId?.ToString() ?? "system", "analytik.scenario.export", "analytik.scenario", id.ToString(),
            new { format, compare = others }, ct);
        var name = string.Concat($"Сценарий {view.Name}".Select(c => Path.GetInvalidFileNameChars().Contains(c) || c is '"' or '/' or '\\' ? '_' : c));
        name = (name.Length > 80 ? name[..80] : name) + $" {now.ToOffset(TimeSpan.FromHours(5)):yyyy-MM-dd}.{format}";
        return new ExportFile(bytes, format == "pdf" ? "application/pdf" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }

    private static IReadOnlyList<JsonElement> Row(params object?[] values) => values.Select(v => JsonSerializer.SerializeToElement(v, Json)).ToList();

    private static AiReportColumn Col(string header, string format = ReportFormats.Text, bool sum = false) => new(header, format, sum);

    internal static string FormatOf(string unit) => unit switch
    {
        "сум" => ReportFormats.Money,
        "кг" => ReportFormats.Kg,
        "%" => ReportFormats.Percent,
        _ => ReportFormats.Number,
    };

    internal static AiReportContent Content(ScenarioView v, ScenarioComparison? comparison)
    {
        var r = v.Result!;
        var b = v.Baseline;
        var kind = ScenarioCatalog.Kinds.FirstOrDefault(k => k.Code == v.Kind)?.Title ?? v.Kind;
        var sheets = new List<AiReportSheet>
        {
            new("Сводка", [Col("Поле"), Col("Значение")],
            [
                Row("Сценарий", v.Name), Row("Вид", kind), Row("Набор", Preset(v.Preset)),
                Row("Базовый период", $"{b.From:dd.MM.yyyy} — {b.To:dd.MM.yyyy} ({b.Days} дн.)"), Row("Дата исходных данных", b.AsOf.ToString("dd.MM.yyyy")),
                Row("Горизонт", $"{r.HorizonDays} дн.: {r.From:dd.MM.yyyy} — {r.To:dd.MM.yyyy}"),
                Row("Дата расчёта", v.CalculatedAt?.ToOffset(TimeSpan.FromHours(5)).ToString("dd.MM.yyyy HH:mm") ?? "—"),
                Row("Версия формул", r.FormulaVersion), Row("Расчёт №", v.Revision), Row("Главный вывод", r.Conclusion.Headline),
                Row("Цепочка влияния", string.Join(" → ", r.Chain.Select(c => DependencyGraph.Find(c)?.Title ?? c))),
            ], Note: "Расчёт выполнил движок симулятора по снимку данных OneBase; фактические данные OneBase сценарий не меняет."),
        };

        var parameters = v.Parameters.Where(x => ScenarioCatalog.Find(x.Code) is { Mode: not ParameterDefinition.Input }).ToList();
        sheets.Add(new AiReportSheet("Изменённые параметры", [Col("Параметр"), Col("Цель"), Col("Значение", ReportFormats.Number), Col("Ед.")],
            parameters.Select(x => Row(ScenarioCatalog.Find(x.Code)!.Title, Target(b, x), x.Value, ScenarioCatalog.Find(x.Code)!.Unit)).ToList(),
            Note: parameters.Count == 0 ? "Изменений нет — базовый сценарий." : null));

        var s = b.Summary;
        var inputs = new List<IReadOnlyList<JsonElement>>
        {
            Row("Выручка за базовый период", s.Revenue, "сум", "OneBase → Linko"), Row("Продажи за базовый период", s.Kg, "кг", "OneBase → Linko"),
            Row("Товаров / с техкартой", $"{s.Products} / {s.ProductsWithRecipe}", "", "OneBase → Техкарты"),
            Row("Продаж покрыто техкартами", s.RecipeShare is { } rs ? Math.Round(rs * 100, 1) : null, "%", "OneBase → Техкарты"),
            Row("План продаж в месяц", s.PlanKgMonth, "кг", s.PlanSource ?? "нет планов"),
            Row("Позиций сырья / с ценой", $"{s.Materials} / {s.MaterialsPriced}", "", "OneBase → Снабжение"),
            Row("Расходы на закупки за период", s.ProcurementSpend, "сум", "OneBase → Снабжение"),
            Row("Оплаты точек за период", s.Payments, "сум", "OneBase → Linko"),
            Row("Численность", s.Employees, "чел.", "Verifix"),
        };
        inputs.AddRange(r.Inputs.Select(i => Row(i.Title, i.Value, i.Unit, i.Origin)));
        sheets.Add(new AiReportSheet("Исходные показатели", [Col("Показатель"), Col("Значение", ReportFormats.Number), Col("Ед."), Col("Откуда")], inputs,
            Note: string.Join(" ", b.Notes.Take(6)) is { Length: > 0 } dataNotes ? dataNotes : null));

        var metricColumns = new[]
        {
            Col("Показатель"), Col("База", ReportFormats.Number), Col("Сценарий", ReportFormats.Number), Col("Разница", ReportFormats.Number),
            Col("Изменение, %", ReportFormats.Percent), Col("Ед."), Col("Вид"), Col("Примечание"),
        };
        IReadOnlyList<JsonElement> MetricRow(SimMetric m) => Row(m.Title, m.Base, m.Scenario, m.Delta, m.DeltaPercent, m.Unit,
            m.Kind switch { "fact" => "факт", "input" => "введено", _ => "расчёт" }, m.Note);

        var money = r.Metrics.Where(m => m.Unit == "сум" && m.Base is not null && m.Scenario is not null && m.Code is not ("finance.min_cash" or "finance.net_cash"))
            .Take(10).ToList();
        if (money.Count > 0)
        {
            sheets.Add(new AiReportSheet("График — деньги", metricColumns, money.Select(MetricRow).ToList(), Chart: new AiReportChart(0, [1, 2])));
        }

        var volume = r.Metrics.Where(m => m.Unit == "кг" && m.Base is not null && m.Scenario is not null).Take(10).ToList();
        if (volume.Count > 0)
        {
            sheets.Add(new AiReportSheet("График — объёмы", metricColumns, volume.Select(MetricRow).ToList(), Chart: new AiReportChart(0, [1, 2])));
        }

        foreach (var group in r.Metrics.GroupBy(m => m.Group))
        {
            sheets.Add(new AiReportSheet($"Результаты — {GroupTitle(group.Key)}", metricColumns, group.Select(MetricRow).ToList()));
        }

        sheets.Add(new AiReportSheet("Ограничения", [Col("Важность"), Col("Ограничение"), Col("Пояснение")],
            r.Constraints.Select(c => Row(Severity(c.Severity), c.Title, c.Text)).ToList(), Note: r.Constraints.Count == 0 ? "Ограничений не найдено." : null));
        sheets.Add(new AiReportSheet("Риски", [Col("Уровень"), Col("Риск"), Col("Пояснение")],
            r.Risks.Select(x => Row(Severity(x.Level), x.Title, x.Text)).ToList(), Note: r.Risks.Count == 0 ? "Рисков не найдено." : null));
        sheets.Add(new AiReportSheet("Рекомендации", [Col("№", ReportFormats.Integer), Col("Рекомендация")],
            r.Conclusion.Recommendations.Select((x, i) => Row(i + 1, x)).ToList()));

        if (r.Products.Count > 0)
        {
            sheets.Add(new AiReportSheet("Товары",
            [
                Col("Товар"), Col("Техкарта"), Col("База, кг", ReportFormats.Kg, true), Col("Запрос, кг", ReportFormats.Kg, true), Col("Выпуск, кг", ReportFormats.Kg, true),
                Col("Продано, кг", ReportFormats.Kg, true), Col("Выручка база", ReportFormats.Money, true), Col("Выручка", ReportFormats.Money, true),
                Col("Себест. кг", ReportFormats.Money), Col("Валовая прибыль", ReportFormats.Money, true), Col("Обеспечение, %", ReportFormats.Percent), Col("Ограничение"),
            ], r.Products.Select(x => Row(x.Name, x.HasRecipe ? "да" : "нет", x.BaseKg, x.RequestedKg, x.ProducedKg, x.SoldKg, x.BaseRevenue, x.Revenue, x.CostPerKg,
                x.GrossProfit, Math.Round(x.Coverage * 100, 1), x.Limit)).ToList(), Total: true));
        }

        if (r.Regions.Count > 0)
        {
            sheets.Add(new AiReportSheet("Регионы",
            [
                Col("Регион"), Col("База, кг", ReportFormats.Kg, true), Col("Сценарий, кг", ReportFormats.Kg, true), Col("Выручка база", ReportFormats.Money, true),
                Col("Выручка", ReportFormats.Money, true), Col("План, кг", ReportFormats.Kg, true), Col("Выполнение, %", ReportFormats.Percent),
            ], r.Regions.Select(x => Row(x.New ? $"{x.Name} (новый)" : x.Name, x.BaseKg, x.Kg, x.BaseRevenue, x.Revenue, x.PlanKg, x.Execution)).ToList(), Total: true,
                Chart: new AiReportChart(0, [3, 4])));
        }

        if (r.Materials.Count > 0)
        {
            sheets.Add(new AiReportSheet("Сырьё и закупки",
            [
                Col("Позиция"), Col("Ед."), Col("Расход/день база", ReportFormats.Number), Col("Расход/день", ReportFormats.Number), Col("Доступно", ReportFormats.Number),
                Col("Поставки", ReportFormats.Number), Col("Закупить", ReportFormats.Number), Col("Стоимость", ReportFormats.Money, true), Col("Цена", ReportFormats.Money),
                Col("Дефицит с", ReportFormats.Date), Col("Дней без сырья", ReportFormats.Number), Col("Заказать до", ReportFormats.Date), Col("Срок, дн.", ReportFormats.Integer),
                Col("Остаток на конец", ReportFormats.Number), Col("Риск"), Col("Поставщик"),
            ], r.Materials.Select(m => Row(m.Name, m.Unit, m.BaseDaily, m.Daily, m.Available, m.Incoming, m.Purchase, m.PurchaseCost, m.Price,
                m.ShortageDate?.ToString("yyyy-MM-dd"), m.LostDays, m.OrderBy?.ToString("yyyy-MM-dd"), m.LeadTimeDays, m.EndStock, Severity(m.Risk), m.Supplier)).ToList(),
                Total: true));
        }

        if (r.Expansion is { } e)
        {
            sheets.Add(new AiReportSheet($"Новый регион — {Short(e.Name, 15)}", [Col("Показатель"), Col("Значение", ReportFormats.Number), Col("Ед.")],
            [
                Row("Продажи за горизонт", e.Kg, "кг"), Row("Выручка за горизонт", e.Revenue, "сум"), Row("Расходы за горизонт", e.Cost, "сум"),
                Row("Постоянные расходы в месяц", e.FixedMonth, "сум"), Row("Прибыль в месяц", e.ProfitMonth, "сум"),
                Row("Точка безубыточности", e.BreakEvenKgMonth, "кг/мес"), Row("Срок окупаемости", e.PaybackMonths, "мес"),
                Row("Нужно сотрудников (по образцу)", e.StaffNeed, "чел."), Row("Заложено сотрудников", e.Staff, "чел."),
            ], Note: string.Join(" ", e.Notes) is { Length: > 0 } en ? en : null));
        }

        if (comparison is not null)
        {
            var columns = new List<AiReportColumn> { Col("Показатель"), Col("Ед.") };
            columns.AddRange(comparison.Columns.Select(c => Col(Short(c.Name, 40), ReportFormats.Number)));
            columns.AddRange(comparison.Columns.Skip(1).Select(c => Col($"Δ % — {Short(c.Name, 30)}", ReportFormats.Percent)));
            sheets.Add(new AiReportSheet("Сравнение сценариев", columns,
                comparison.Rows.Select(x => Row([x.Title, x.Unit, .. x.Values.Cast<object?>(), .. x.DeltaPercent.Skip(1).Cast<object?>()])).ToList(),
                Note: string.Join(" ", comparison.Notes) is { Length: > 0 } cn ? cn : "Разница — к первому сценарию."));
        }

        var notes = r.Conclusion.Sources.Select(x => Row("Источник", x))
            .Concat(r.Conclusion.Assumptions.Select(x => Row("Допущение", x)))
            .Concat(r.Issues.Where(i => i.Severity == ScenarioIssue.Warning).Select(i => Row("Замечание к параметрам", i.Text)))
            .ToList();
        sheets.Add(new AiReportSheet("Источники и допущения", [Col("Вид"), Col("Текст")], notes));
        if (!string.IsNullOrWhiteSpace(v.AiAnalysis))
        {
            sheets.Add(new AiReportSheet("AI-заключение", [Col("Текст")],
                v.AiAnalysis.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => Row(x.Replace("**", ""))).ToList(),
                Note: v.AiAt is { } at ? $"AI-аналитик, {at.ToOffset(TimeSpan.FromHours(5)):dd.MM.yyyy HH:mm}; числа — из расчёта движка." : null));
        }

        return new AiReportContent($"Сценарий «{v.Name}» — {kind}", sheets,
            ["OneBase → Аналитик → Симулятор", $"данные на {b.AsOf:dd.MM.yyyy}", $"база {b.From:dd.MM.yyyy} — {b.To:dd.MM.yyyy}"]);
    }

    private static string Target(BaselineView b, ScenarioParameter x)
    {
        if (x.Target is null)
        {
            return "все";
        }

        var c = b.Choices;
        return c.Regions.Concat(c.Products).Concat(c.Items).Concat(c.Suppliers).Concat(c.Divisions).FirstOrDefault(i => i.Id == x.Target)?.Name ?? x.Target;
    }

    private static string Preset(string preset) => preset switch
    {
        ScenarioPresets.Base => "базовый",
        ScenarioPresets.Optimistic => "оптимистичный",
        ScenarioPresets.Pessimistic => "пессимистичный",
        _ => "свой",
    };

    private static string GroupTitle(string group) => group switch
    {
        "sales" => "продажи",
        "production" => "производство",
        "materials" => "сырьё",
        "procurement" => "закупки",
        "finance" => "финансы",
        "workforce" => "персонал",
        _ => group,
    };

    private static string Severity(string s) => s switch
    {
        "critical" => "критично",
        "high" => "высокий",
        "warning" or "medium" => "внимание",
        "low" => "низкий",
        "info" => "к сведению",
        "none" => "нет",
        _ => s,
    };

    private static string Short(string s, int n) => s.Length > n ? s[..n] : s;
}
