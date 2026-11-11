using System.Globalization;
using System.Text.Json;
using OneBase.AI.Consultant;
using OneBase.AI.Tools;
using OneBase.AI.Tools.Data;
using OneBase.Application.Analytik.Business;
using OneBase.Application.Analytik.Catalog;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Risk;
using OneBase.Application.Analytik.Supply;
using OneBase.Application.Field;
using OneBase.Application.Security;

namespace OneBase.AI.Analytik;

/// <summary>Общее для инструментов OneBase Intelligence: источник знаний, право раздела, кто спрашивает, период.</summary>
public abstract class AnalytikTool(QueryEngine engine) : DataTool
{
    public override string? Source => AnalytikTools.Source;
    public override string? RequiredPermission => Permissions.AnalytikUse;

    protected QueryEngine Engine => engine;

    protected static QueryCaller Caller(ToolContext context) => new(context.UserId, "chat", context.AgentCode, context.ConversationId);

    protected Task<AnalytikPrincipal> PrincipalAsync(ToolContext context, CancellationToken ct) => engine.PrincipalAsync(Caller(context), ct);

    protected static readonly (string, object) PeriodArg = ("period", new
    {
        type = "string",
        @enum = AnalytikPeriod.Kinds,
        description = "Период: today, yesterday, week (7 дней), month (с начала месяца), quarter, year (с начала), custom (с from и to). Сравнение — тот же отрезок раньше.",
    });

    protected static readonly (string, object) FromArg = ("from", new { type = "string", description = "Начало своего периода, YYYY-MM-DD (period = custom)." });
    protected static readonly (string, object) ToArg = ("to", new { type = "string", description = "Конец своего периода, YYYY-MM-DD (period = custom)." });

    protected static AnalytikPeriod PeriodOf(ToolArgs args)
    {
        static DateOnly? Date(string? s) => s is null ? null
            : DateOnly.TryParse(s, CultureInfo.InvariantCulture, out var d) ? d : throw new ToolArgumentException($"Дата «{s}»: ожидается YYYY-MM-DD.");
        try
        {
            return AnalytikPeriod.Of(args.Str("period"), Date(args.Str("from")), Date(args.Str("to")), FieldClock.Today);
        }
        catch (ArgumentException ex)
        {
            throw new ToolArgumentException(ex.Message);
        }
    }

    /// <summary>Отказ движка — модели как ошибка с видом (нет доступа или неверный план).</summary>
    protected ToolResult Refused(QueryPlanException ex) =>
        ToolResult.Failure(Name, ex.Code == QueryErrors.Denied ? ToolFailures.Forbidden : ToolFailures.InvalidArguments, ex.Message);
}

public static class AnalytikTools
{
    /// <summary>Источник знаний инструментов OneBase Intelligence — только у команды «Аналитик».</summary>
    public const string Source = "analytik.intelligence";
}

public sealed class FindTablesTool(QueryEngine engine, CatalogService catalog) : AnalytikTool(engine)
{
    public override string Name => "analytik_find_tables";
    public override string Title => "Каталог базы: поиск таблиц";

    public override string Description =>
        "Найти таблицы базы OneBase по смыслу вопроса (русский, узбекский, английский): название, назначение, модуль, строк, главная дата, доступна ли " +
        "пользователю; и показатели компании с их инструментом-владельцем. Первый шаг исследования, если готового инструмента нет.";

    public override JsonElement InputSchema { get; } = Schema(
        ("q", new { type = "string", description = "Что ищем: «продажи по точкам», «расход сырья», «опоздания»." }),
        ("module", new { type = "string", @enum = CatalogRules.Modules, description = "Модуль (необязательно)." }));

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        var q = args.Str("q") ?? throw new ToolArgumentException("Нужен текст поиска q.");
        var principal = await PrincipalAsync(context, ct);
        var model = await catalog.ModelAsync(ct);
        var hits = CatalogSearch.Search(model, q, args.Str("module"), 12, principal.Manager);
        return Data(new
        {
            Tables = hits.Select(h =>
            {
                var d = AnalytikAccess.Decide(h.Table, principal);
                return new
                {
                    h.Table.FullName, h.Table.Title, h.Table.Module, h.Table.Rows, h.Table.DateColumn, Available = d.Allowed, Unavailable = d.Message,
                    Description = h.Table.Description is { Length: > 300 } t ? t[..300] + "…" : h.Table.Description, Why = h.Why,
                };
            }),
            Metrics = CatalogSearch.Metrics(q).Select(m => new { m.Code, m.Title, m.Definition, Tool = m.AuthoritativeTool }),
            Hint = "Официальные показатели — инструментом-владельцем (Tool). Для своего среза — analytik_describe_table, затем analytik_query.",
        }, new DataSource("OneBase → Аналитик → Карта данных", null, "/analytik/data"));
    }
}

public sealed class DescribeTableTool(QueryEngine engine, CatalogService catalog) : AnalytikTool(engine)
{
    public override string Name => "analytik_describe_table";
    public override string Title => "Каталог базы: описание таблицы";

    public override string Description =>
        "Описание таблицы: назначение и бизнес-правила, колонки (тип, роль: дата, сумма, количество, ссылка; скрытые — персональные и секреты), " +
        "первичный ключ, связи в обе стороны (внешние ключи и подтверждённые данными), показатели, в которых она участвует, доступ пользователя.";

    public override JsonElement InputSchema { get; } = Schema(("table", new { type = "string", description = "Таблица schema.Table, например linko.Orders." }));

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        var table = args.Str("table") ?? throw new ToolArgumentException("Нужна таблица (schema.Table).");
        var principal = await PrincipalAsync(context, ct);
        var detail = CatalogViews.Detail(await catalog.ModelAsync(ct), table, principal)
                     ?? throw new ToolArgumentException($"Таблицы «{table}» нет в каталоге — найдите её через analytik_find_tables.");
        return Data(new
        {
            detail.Table.FullName, detail.Table.Title, detail.Table.Description, detail.Table.Module, detail.Table.Rows, detail.Table.DateColumn,
            Available = detail.Table.Available, Unavailable = detail.Table.Unavailable,
            Columns = detail.Columns.Select(c => new { c.Name, c.DataType, c.Role, c.Title, Hidden = c.Sensitivity != Sensitivity.None ? c.Sensitivity : null, c.PrimaryKey }),
            Relations = detail.Relations.Where(r => r.Status != RelationStatus.Rejected).Take(40)
                .Select(r => new { r.Direction, Link = $"{r.From}.{r.FromColumn} → {r.To}.{r.ToColumn}", r.Status, Match = r.MatchRatio, r.Kind }),
            Metrics = detail.Metrics.Select(m => new { m.Title, m.Definition, Tool = m.AuthoritativeTool }),
        }, new DataSource($"OneBase → Аналитик → Карта данных → {detail.Table.FullName}", null, $"/analytik/data/{detail.Table.Schema}/{detail.Table.Name}"));
    }
}

public sealed class QueryTool(QueryEngine engine) : AnalytikTool(engine), ITool
{
    public override string Name => "analytik_query";
    public override string Title => "Запрос к базе по плану";

    public override string Description =>
        "Аналитический запрос к базе OneBase по типизированному плану (SQL строит и проверяет OneBase, только чтение, по правам пользователя). " +
        "Список строк — select; агрегат — measures (+ group_by и bucket по дате). Колонки — Column базовой таблицы, Table.Column или schema.Table.Column: " +
        "таблица подключается сама по подтверждённой связи. Значения фильтров — как в данных (статусы — посмотрите analytik_profile_column). " +
        "Суммы считайте от самой подробной таблицы (строки заказов, движения склада) — через «один ко многим» сумма запрещена.";

    public override string? UsageHint =>
        "analytik_query: план, а не SQL. Пример — выручка по регионам за октябрь 2026: {\"from\":\"linko.OrderLines\",\"group_by\":[\"sales.Regions.Name\"]," +
        "\"measures\":[{\"fn\":\"sum\",\"column\":\"TotalPrice\",\"as\":\"revenue\"}],\"filters\":[{\"column\":\"linko.Orders.Status\",\"op\":\"in\",\"value\":[\"delivered\",\"given\"]}," +
        "{\"column\":\"linko.Orders.AcceptedDate\",\"op\":\"between\",\"value\":[\"2026-10-01\",\"2026-10-31\"]}],\"order_by\":[{\"by\":\"revenue\",\"dir\":\"desc\"}]}. " +
        "Официальную выручку и план берите инструментами продаж — запрос нужен для срезов, которых в них нет.";

    public override JsonElement InputSchema { get; } = JsonSerializer.SerializeToElement(new
    {
        type = "object",
        required = new[] { "from" },
        properties = new Dictionary<string, object>
        {
            ["from"] = new { type = "string", description = "Базовая таблица schema.Table (из analytik_find_tables)." },
            ["join"] = new { type = "array", items = new { type = "string" }, description = "Явно: «schema.Table» или «schema.Table via Колонка» при неоднозначной связи." },
            ["select"] = new
            {
                type = "array",
                description = "Колонки результата (в запросе с measures — колонки группировки).",
                items = new { type = "object", required = new[] { "column" }, properties = new { column = new { type = "string" }, @as = new { type = "string" } } },
            },
            ["measures"] = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    required = new[] { "fn" },
                    properties = new
                    {
                        fn = new { type = "string", @enum = new[] { "sum", "avg", "min", "max", "count", "count_distinct" } },
                        column = new { type = "string", description = "Без колонки — count(*)." },
                        @as = new { type = "string" },
                    },
                },
            },
            ["filters"] = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    required = new[] { "column", "op" },
                    properties = new
                    {
                        column = new { type = "string" },
                        op = new { type = "string", @enum = new[] { "eq", "ne", "gt", "gte", "lt", "lte", "in", "not_in", "between", "contains", "starts_with", "is_null", "not_null" } },
                        value = new { description = "Значение; список — для in, not_in, between ([от, до])." },
                    },
                },
            },
            ["group_by"] = new { type = "array", items = new { type = "string" } },
            ["bucket"] = new
            {
                type = "object",
                required = new[] { "column", "unit" },
                properties = new { column = new { type = "string" }, unit = new { type = "string", @enum = new[] { "day", "week", "month", "quarter", "year" } }, @as = new { type = "string" } },
            },
            ["order_by"] = new
            {
                type = "array",
                items = new { type = "object", required = new[] { "by" }, properties = new { by = new { type = "string" }, dir = new { type = "string", @enum = new[] { "asc", "desc" } } } },
            },
            ["limit"] = new { type = "integer", description = "Строк: список до 500, групп до 2000 (по умолчанию 100 / 200)." },
            ["offset"] = new { type = "integer" },
        },
    });

    protected override Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct) =>
        throw new NotSupportedException("Запрос выполняется через ITool.ExecuteAsync — план разбирается из исходных аргументов.");

    /// <summary>
    /// План разбирается из исходных аргументов (вложенные объекты, а не плоские ToolArgs). Повторная реализация ITool: ToolExecutor вызывает именно её.
    /// </summary>
    async Task<ToolResult> ITool.ExecuteAsync(ToolContext context, JsonElement arguments, CancellationToken ct)
    {
        QueryPlan plan;
        try
        {
            var raw = arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty("all_rows", out _)
                ? JsonSerializer.SerializeToElement(JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(arguments.GetRawText())!
                    .Where(kv => kv.Key != "all_rows").ToDictionary(kv => kv.Key, kv => kv.Value))
                : arguments;
            plan = QueryPlan.Parse(raw);
        }
        catch (QueryPlanException ex)
        {
            return Refused(ex);
        }

        var outcome = await Engine.RunAsync(plan, Caller(context), ct);
        if (!outcome.Ok)
        {
            return ToolResult.Failure(Name, outcome.Status == "denied" ? ToolFailures.Forbidden : ToolFailures.InvalidArguments, outcome.Error!);
        }

        var r = outcome.Result!;
        return Data(new
        {
            QueryId = r.QueryId,
            Columns = r.Columns.Select(c => new { c.Name, c.Source, c.Role }),
            Rows = r.Rows,
            RowCount = r.Rows.Count,
            HasMore = r.HasMore,
            r.Tables,
            Scope = r.Scope,
            Notes = r.Notes,
            Note = r.HasMore ? "Показаны не все строки (has_more): увеличьте limit, сгруппируйте крупнее или возьмите следующую страницу (offset)." : null,
        }, new DataSource($"OneBase → Аналитик → запрос: {string.Join(", ", r.Tables)}", r.At.ToOffset(TimeSpan.FromHours(5)).ToString("dd.MM.yyyy HH:mm"),
            $"/analytik/history?query={r.QueryId}"));
    }
}

public sealed class ProfileColumnTool(QueryEngine engine) : AnalytikTool(engine)
{
    public override string Name => "analytik_profile_column";
    public override string Title => "Профиль колонки";
    public override string Description => "Значения колонки: строк, заполнено, различных значений, минимум и максимум, частые значения с количеством (статусы, виды, названия).";

    public override JsonElement InputSchema { get; } = Schema(("table", new { type = "string" }), ("column", new { type = "string" }));

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        try
        {
            var p = await Engine.ProfileAsync(args.Str("table") ?? "", args.Str("column") ?? "", Caller(context), ct);
            return Data(new { p.Table, p.Column, p.DataType, p.Rows, p.Filled, p.Distinct, p.Min, p.Max, Top = p.Top.Select(t => new { t.Value, t.Count }), p.Scope },
                new DataSource($"OneBase → Аналитик → {p.Table}.{p.Column}"));
        }
        catch (QueryPlanException ex)
        {
            return Refused(ex);
        }
    }
}

public sealed class EntityTool(QueryEngine engine) : AnalytikTool(engine)
{
    public override string Name => "analytik_entity";
    public override string Title => "Всё о записи";

    public override string Description =>
        "Всё о записи по ключу: сама запись, записи, на которые она ссылается (с названиями), и связанные записи других таблиц — сколько и последние " +
        "(например, точка → её заказы, визиты, оплаты; сотрудник → табель). Только доступное пользователю.";

    public override JsonElement InputSchema { get; } = Schema(("table", new { type = "string" }), ("key", new { type = "string", description = "Значение первичного ключа." }));

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        try
        {
            var e = await Engine.EntityAsync(args.Str("table") ?? "", args.Str("key") ?? "", Caller(context), ct);
            return Data(new
            {
                e.Table, e.Key, Found = e.Record is not null, e.Record, References = e.References,
                Related = e.Related.Select(r => new { r.Table, r.Title, Via = r.Column, r.Count, Columns = r.Columns.Select(c => c.Name), Latest = r.Latest }),
                HiddenTables = e.Hidden, e.Scope,
            }, new DataSource($"OneBase → Аналитик → {e.Table} #{e.Key}"));
        }
        catch (QueryPlanException ex)
        {
            return Refused(ex);
        }
    }
}

public sealed class DashboardTool(QueryEngine engine, DashboardService dashboard) : AnalytikTool(engine)
{
    public override string Name => "analytik_dashboard";
    public override string Title => "Состояние компании";

    public override string Description =>
        "Состояние компании за период по всем отделам, доступным пользователю: оценка 0–100 и из чего она сложилась, ключевые показатели продаж, " +
        "финансов, производства, склада и снабжения, персонала, критические риски, рекомендации, прогноз выручки и свежесть данных.";

    public override JsonElement InputSchema { get; } = Schema(PeriodArg, FromArg, ToArg);

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        var d = await dashboard.BuildAsync(PeriodOf(args), await PrincipalAsync(context, ct), ct);
        object? Block(IAnalytikBlock? b) => b is null ? null : new { Kpis = b.Kpis.Select(k => new { k.Title, k.Value, k.Unit, k.Previous, k.ChangePercent, k.Kind }), b.Notes };
        return Data(new
        {
            Period = new { d.Period.Label, From = d.Period.From, To = d.Period.To, Compare = d.Period.PreviousLabel },
            d.Health, Sales = Block(d.Sales), SalesPlan = d.Sales?.Plan, Finance = Block(d.Finance), Production = Block(d.Production), Hr = Block(d.Hr), Work = Block(d.Work),
            Supply = d.Supply is null ? null : new { d.Supply.Risks, d.Supply.Horizons, Top = d.Supply.Top.Select(r => new { r.Name, r.Risk, r.ShortageDate, Need30 = r.Horizon(30)?.Required, r.Unit }) },
            Risks = d.Risks.Take(10).Select(r => new { r.Severity, r.Module, r.Title, r.Description }),
            d.Recommendations,
            Forecast = d.Forecast is null ? null : new { d.Forecast.Title, d.Forecast.Total, d.Forecast.Lower, d.Forecast.Upper, d.Forecast.Steps, d.Forecast.Method, d.Forecast.Reliable },
            d.Freshness, d.Scope, Unavailable = d.Errors,
        }, new DataSource("OneBase → Аналитик → Обзор", d.Period.Label, "/analytik"));
    }
}

public sealed class SalesOverviewTool(QueryEngine engine, SalesAnalytik sales) : AnalytikTool(engine)
{
    public override string Name => "analytik_sales_overview";
    public override string Title => "Продажи за период (Аналитик)";

    public override string Description =>
        "Продажи за период по правилам отчётов продаж с сравнением с прошлым таким же периодом: выручка, кг, заказы, АКБ, средний чек, возвраты, " +
        "по дням, по регионам (с планом и прогнозом выполнения месяца), план и прогноз месяца. Учитывает область продаж пользователя.";

    public override JsonElement InputSchema { get; } = Schema(PeriodArg, FromArg, ToArg);

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        var block = await sales.BlockAsync(PeriodOf(args), await PrincipalAsync(context, ct), ct);
        return block is null
            ? ToolResult.Failure(Name, ToolFailures.Forbidden, "Нет доступа к продажам.")
            : Data(new
            {
                Period = new { block.Period.Label, Compare = block.Period.PreviousLabel }, block.DataThrough, block.Complete,
                Kpis = block.Kpis.Select(k => new { k.Code, k.Title, k.Value, k.Unit, k.Previous, k.ChangePercent, k.Kind }),
                Regions = args.Top(block.Regions, 30, "regions"), Daily = args.Top(block.Daily, 62, "daily"), block.Plan, block.Scope, block.Notes,
            }, new DataSource("OneBase → Продажи", block.Period.Label, "/sales"));
    }
}

public sealed class ProcurementPlanTool(QueryEngine engine, SupplyPlanner planner) : AnalytikTool(engine)
{
    public override string Name => "analytik_procurement_plan";
    public override string Title => "План закупок сырья";

    public override string Description =>
        "Что, сколько и когда закупить: по каждой позиции — остаток (физический, резерв, доступный), средний расход, открытые задания производства, " +
        "подтверждённые поставки, страховой запас, потребность на 7/14/30/60/90 дней, дата дефицита, срок заказа, поставщик, цена и стоимость, риск, " +
        "обоснование и допущения; итоги по горизонтам и поставщикам. Формула: спрос + страховой запас − доступный остаток − подтверждённые поставки.";

    public override JsonElement InputSchema { get; } = Schema(
        ("risk", new { type = "string", @enum = new[] { "critical", "high", "medium", "low", "all" }, description = "Какие позиции (по умолчанию — critical, high, medium)." }),
        ("search", new { type = "string", description = "Позиция по названию или артикулу." }));

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        try
        {
            var plan = await planner.PlanAsync(await PrincipalAsync(context, ct), ct, args.Str("search"));
            var risk = args.Str("risk");
            var items = plan.Items.Where(r => risk == "all" || (risk is null ? r.Risk is "critical" or "high" or "medium" : r.Risk == risk)).ToList();
            return Data(new
            {
                plan.AsOf, plan.Horizons, plan.Risks, Suppliers = plan.Suppliers.Take(15),
                Items = args.Top(items, 40, "items").Select(r => new
                {
                    r.Name, r.Sku, r.Unit, r.OnHand, r.Reserved, r.Unusable, r.Available, r.AvgDailyConsumption, r.CommittedTotal, r.IncomingTotal, r.SafetyStock,
                    r.DaysOfCover, r.ShortageDate, r.OrderBy, r.OrderNow, r.LeadTimeDays, r.LeadTimeAssumed, r.UnitPrice, r.Supplier, r.Risk, r.Deferrable,
                    Need = r.Horizons.Select(h => new { h.Days, h.Demand, h.Incoming, h.Required, h.Cost }), r.Reasons, r.Assumptions,
                }),
                plan.DataNotes,
            }, new DataSource("OneBase → Аналитик → Снабжение (план закупок)", plan.AsOf.ToString("dd.MM.yyyy"), "/analytik/procurement"));
        }
        catch (QueryPlanException ex)
        {
            return Refused(ex);
        }
    }
}

public sealed class WarehouseTool(QueryEngine engine, SupplyPlanner planner) : AnalytikTool(engine)
{
    public override string Name => "analytik_warehouse";
    public override string Title => "Склад сырья";

    public override string Description =>
        "Склад сырья и материалов: по позициям — на складе, резерв, непригодно (просрочено, на контроле, заблокировано), доступно, минимум, расход в " +
        "день и за 30 дней, дней запаса, статус (out — закончилось, deficit — дефицит, below_min — ниже минимума, excess — излишек, dead — без расхода " +
        "90 дней), стоимость по последней цене; сводка по статусам.";

    public override JsonElement InputSchema { get; } = Schema(
        ("status", new { type = "string", @enum = new[] { "out", "deficit", "below_min", "excess", "dead", "ok", "empty", "all" } }),
        ("search", new { type = "string" }));

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        try
        {
            var plan = await planner.PlanAsync(await PrincipalAsync(context, ct), ct, args.Str("search"));
            var status = args.Str("status");
            var items = plan.Warehouse.Where(w => status is null or "all" ? w.Status != "empty" : w.Status == status).ToList();
            return Data(new { plan.AsOf, plan.WarehouseStatus, plan.StockValue, plan.ItemsWithoutPrice, Items = args.Top(items, 60, "items"), plan.DataNotes },
                new DataSource("OneBase → Снабжение → Склад", plan.AsOf.ToString("dd.MM.yyyy"), "/analytik/warehouse"));
        }
        catch (QueryPlanException ex)
        {
            return Refused(ex);
        }
    }
}

public sealed class ProductionTool(QueryEngine engine, OperationsAnalytik operations, OneBase.Application.Procurement.ProcurementRequestService requests) : AnalytikTool(engine)
{
    public override string Name => "analytik_production";
    public override string Title => "Производство (Аналитик)";

    public override string Description =>
        "Производство за период: план и факт выпуска по заданиям, выпуск и брак по сменам, расход против нормы и перерасход, просроченные задания, " +
        "рост цен сырья, дни запаса; сколько можно произвести по действующим техкартам из доступного сырья, что ограничивает, себестоимость сырья на " +
        "базу техкарты и её состав. price_change_percent — как изменится себестоимость сырья при изменении цен (например 10).";

    public override JsonElement InputSchema { get; } = Schema(PeriodArg, FromArg, ToArg,
        ("price_change_percent", new { type = "integer", description = "Изменение цен сырья в % для сценария себестоимости (например 10 или -5)." }));

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        var principal = await PrincipalAsync(context, ct);
        var block = await operations.ProductionAsync(PeriodOf(args), principal, ct);
        if (block is null)
        {
            return ToolResult.Failure(Name, ToolFailures.Forbidden, "Нет доступа к производству.");
        }

        object? scenario = null;
        if (args.Int("price_change_percent", -90, 500) is { } change && change != 0 && principal.UserId is not null)
        {
            var ctx = await requests.ContextAsync(principal.UserId!.Value, principal.Permissions, ct);
            var changed = await operations.ProducibleAsync(ctx, 30, ct, change / 100m);
            scenario = new
            {
                PriceChangePercent = change,
                Recipes = changed.Select(c => new { c.Code, c.Name, MaterialCost = c.MaterialCost, Before = block.Producible.FirstOrDefault(b => b.RecipeId == c.RecipeId)?.MaterialCost, c.CostComplete }),
            };
        }

        return Data(new
        {
            Period = block.Period.Label, Kpis = block.Kpis.Select(k => new { k.Code, k.Title, k.Value, k.Unit, k.Previous, k.Kind, k.Note }),
            Orders = args.Top(block.Orders, 30, "orders"), Producible = block.Producible, Deviations = args.Top(block.Deviations, 20, "deviations"),
            Prices = block.Prices, LowDays = block.LowDays, Scenario = scenario, block.Notes,
        }, new DataSource("OneBase → Производство", block.Period.Label, "/analytik/production"));
    }

}

public sealed class FinanceTool(QueryEngine engine, FinanceAnalytik finance, SalesAnalytik sales) : AnalytikTool(engine)
{
    public override string Name => "analytik_finance";
    public override string Title => "Финансы (Аналитик)";

    public override string Description =>
        "Финансы за период с сравнением: оплаты точек по видам, оплаты к выручке, расходы на закупки, наличие P&L дилеров; честно — каких финансовых " +
        "данных в OneBase нет (расходы компании, бюджеты, кредиторка, денежный поток).";

    public override JsonElement InputSchema { get; } = Schema(PeriodArg, FromArg, ToArg);

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        var principal = await PrincipalAsync(context, ct);
        var period = PeriodOf(args);
        var block = await finance.BlockAsync(period, principal, await sales.BlockAsync(period, principal, ct), ct);
        return block is null
            ? ToolResult.Failure(Name, ToolFailures.Forbidden, "Нет доступа к финансам.")
            : Data(new { Period = block.Period.Label, Kpis = block.Kpis.Select(k => new { k.Code, k.Title, k.Value, k.Unit, k.Previous, k.ChangePercent, k.Kind }), block.Payments, block.Notes },
                new DataSource("OneBase → Финансы", block.Period.Label, "/analytik/finance"));
    }
}

public sealed class HrTool(QueryEngine engine, OperationsAnalytik operations) : AnalytikTool(engine)
{
    public override string Name => "analytik_hr";
    public override string Title => "Персонал и задачи (Аналитик)";

    public override string Description =>
        "Персонал за период с сравнением: работает, пришли и отсутствуют сегодня, опоздания (случаи и минуты), отсутствия, ранние уходы — по " +
        "подразделениям; задачи отделов — открыто, просрочено, выполнено, в срок (управляющему Аналитиком).";

    public override JsonElement InputSchema { get; } = Schema(PeriodArg, FromArg, ToArg);

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        var principal = await PrincipalAsync(context, ct);
        var period = PeriodOf(args);
        var hr = await operations.HrAsync(period, principal, ct);
        var work = await operations.WorkAsync(period, principal, ct);
        if (hr is null && work is null)
        {
            return ToolResult.Failure(Name, ToolFailures.Forbidden, "Нет доступа к данным HR и задач.");
        }

        return Data(new
        {
            Period = period.Label,
            Hr = hr is null ? null : new { Kpis = hr.Kpis.Select(k => new { k.Code, k.Title, k.Value, k.Unit, k.Previous, k.ChangePercent }), Divisions = args.Top(hr.Divisions, 30, "divisions"), hr.Notes },
            Work = work is null ? null : new { Kpis = work.Kpis.Select(k => new { k.Code, k.Title, k.Value, k.Unit }), work.Departments },
        }, new DataSource("OneBase → HR и задачи", period.Label, "/analytik/hr"));
    }
}

public sealed class ForecastTool(QueryEngine engine, ForecastService forecasts) : AnalytikTool(engine)
{
    public override string Name => "analytik_forecast";
    public override string Title => "Прогноз";

    public override string Description =>
        "Прогноз показателя математикой (не моделью): sales.revenue, sales.kg, sales.orders, finance.payments, procurement.consumption (item_id — одна " +
        "позиция). Шаг day/week/month, горизонт. Отдаёт прогноз с диапазоном (пессимистичный — нижняя граница, оптимистичный — верхняя), метод, " +
        "ошибку на истории (WAPE), точность прошлых прогнозов, допущения. scenario_percent — сценарий «что если» (например 20 = +20%).";

    public override JsonElement InputSchema { get; } = Schema(
        ("metric", new { type = "string", @enum = ForecastService.Metrics.Keys.ToArray() }),
        ("granularity", new { type = "string", @enum = new[] { "day", "week", "month" } }),
        ("horizon", new { type = "integer", description = "Шагов вперёд: дни до 120, недели до 26, месяцы до 12." }),
        ("item_id", new { type = "string", description = "Позиция склада (uuid) для procurement.consumption." }),
        ("scenario_percent", new { type = "integer", description = "Сценарий «что если» в % (20 = +20 %, -15 = −15 %)." }));

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        try
        {
            Guid? item = args.Str("item_id") is { } id ? Guid.TryParse(id, out var g) ? g : throw new ToolArgumentException("item_id — uuid позиции.") : null;
            decimal? scenario = args.Int("scenario_percent", -90, 500) is { } v ? v / 100m : null;
            var f = await forecasts.ForecastAsync(args.Str("metric") ?? "sales.revenue", args.Str("granularity") ?? "month", args.Int("horizon", 1, 366) ?? 3,
                await PrincipalAsync(context, ct), ct, item, scenario);
            return Data(new
            {
                f.Metric, f.Title, f.Unit, f.Granularity, Method = f.Result.Method, Points = f.Result.Points, Total = f.Result.Total, TotalLower = f.Result.TotalLower,
                TotalUpper = f.Result.TotalUpper, Wape = f.Result.Wape, f.Result.Reliable, f.Result.Assumptions, Methods = f.Result.Methods, History = args.Top(f.History, 36, "history"),
                PastAccuracy = f.Past, f.Scope,
            }, new DataSource($"OneBase → Аналитик → Прогноз: {f.Title}", null, "/analytik/forecasts"));
        }
        catch (QueryPlanException ex)
        {
            return Refused(ex);
        }
    }
}

public sealed class RisksTool(QueryEngine engine, RiskEngine risks) : AnalytikTool(engine)
{
    public override string Name => "analytik_risks";
    public override string Title => "Риски и отклонения";

    public override string Description =>
        "Активные риски и отклонения, найденные OneBase (правила кодом, каждый час): суть, масштаб, причина (только подтверждённая) и гипотезы, " +
        "последствия, рекомендации, доказательства; включая находки проактивного анализа продаж. Только доступные пользователю.";

    public override JsonElement InputSchema { get; } = Schema(
        ("module", new { type = "string", @enum = new[] { "sales", "finance", "procurement", "production", "hr", "system" } }),
        ("kind", new { type = "string", @enum = new[] { InsightKinds.Risk, InsightKinds.Anomaly, InsightKinds.Quality, InsightKinds.Opportunity } }));

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        var list = await risks.ListAsync(await PrincipalAsync(context, ct), args.Str("module"), false, 200, ct);
        var kind = args.Str("kind");
        var items = list.Where(i => kind is null || i.Kind == kind).ToList();
        return Data(new
        {
            Total = items.Count,
            BySeverity = items.GroupBy(i => i.Severity).ToDictionary(g => g.Key, g => g.Count()),
            Items = args.Top(items, 40, "items").Select(i => new
            {
                i.Severity, i.Kind, i.Module, i.Title, i.Description, i.Cause, i.CauseConfirmed, i.Hypotheses, i.Impact, i.ImpactUnit, i.Consequences, i.Recommendations,
                Detected = i.DetectedAt, i.Source,
            }),
        }, new DataSource("OneBase → Аналитик → Риски", null, "/analytik/risks"));
    }
}

public sealed class DataQualityTool(QueryEngine engine, RiskEngine risks, CatalogService catalog) : AnalytikTool(engine)
{
    public override string Name => "analytik_data_quality";
    public override string Title => "Качество данных";

    public override string Description =>
        "Качество данных OneBase: свежесть источников (Linko, Verifix), конфликты источников, доля «висящих» ссылок, будущие даты, дубли, пробелы " +
        "настроек (минимумы, сроки поставки, связь техкарт с товаром); каталог базы — таблиц, связей, подтверждённых данными.";

    public override JsonElement InputSchema { get; } = Schema();

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        var principal = await PrincipalAsync(context, ct);
        var list = (await risks.ListAsync(principal, null, false, 200, ct)).Where(i => i.Kind == InsightKinds.Quality).ToList();
        var model = await catalog.ModelAsync(ct);
        return Data(new
        {
            Catalog = new
            {
                Tables = model.Tables.Count, Relations = model.Relations.Count, Confirmed = model.Relations.Count(r => r.Status == RelationStatus.Confirmed),
                Unverified = model.Relations.Count(r => r.Status == RelationStatus.Unverified), Rejected = model.Relations.Count(r => r.Status == RelationStatus.Rejected),
                model.RefreshedAt,
            },
            Issues = list.Select(i => new { i.Severity, i.Module, i.Title, i.Description, i.Recommendations, i.Impact, i.ImpactUnit }),
        }, new DataSource("OneBase → Аналитик → Качество данных", null, "/analytik/data"));
    }
}

public sealed class ManagementReportTool(QueryEngine engine, AnalytikReportService reports) : AnalytikTool(engine)
{
    public override string Name => "analytik_management_report";
    public override string Title => "Управленческий отчёт";

    public override string Description =>
        "Сформировать управленческий отчёт (Excel и PDF) по данным OneBase: daily, weekly, monthly, sales, production, inventory, procurement, finance, hr, " +
        "health — за период. Вызывай, только если пользователь просит отчёт. Возвращает ссылку и краткий вывод.";

    public override JsonElement InputSchema { get; } = Schema(("kind", new { type = "string", @enum = AnalytikReportService.Kinds.Keys.ToArray() }), PeriodArg, FromArg, ToArg);

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        try
        {
            var principal = await PrincipalAsync(context, ct);
            var report = await reports.CreateAsync(args.Str("kind") ?? "health", PeriodOf(args), principal, principal.UserId, null, ct);
            var doc = await reports.GetAsync(report.Id, principal, ct);
            return Data(new { ReportId = report.Id, report.Title, Link = $"/analytik/reports/{report.Id}", doc!.Summary, Sections = doc.Sections.Select(s => s.Title) },
                new DataSource($"OneBase → Аналитик → Отчёты → {report.Title}", null, $"/analytik/reports/{report.Id}"));
        }
        catch (QueryPlanException ex)
        {
            return Refused(ex);
        }
    }
}
