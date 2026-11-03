using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.AI.Consultant;
using OneBase.AI.Knowledge;
using OneBase.AI.Security;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Procurement;
using OneBase.Application.Security;
using OneBase.Domain.Procurement;

namespace OneBase.AI.Tools.Data;

/// <summary>
/// Инструменты снабжения — только чтение. Данные — теми же сервисами, что страницы «Заявки» и «Снабжение», с правами и областью видимости
/// пользователя (ProcurementContext): сотрудник видит свои заявки, руководитель — заявки своего отдела, снабжение и директор — все; заказы,
/// склад, приёмку и поставщиков — снабжение, директор и кладовщик; цены, расходы, предложения и KPI — снабжение и директор. Чего пользователю
/// не видно, инструмент не отдаёт (forbidden). Суммы — сумы с НДС, даты — по Ташкенту. Итоги, проценты, рост цен и переплаты считает OneBase.
/// </summary>
internal abstract class ProcurementTool(ProcurementRequestService requests, IUserPermissions permissions) : DataTool
{
    public override string? Source => KnowledgeSources.Procurement;
    public override string? RequiredPermission => Permissions.ProcurementRead;
    public override IReadOnlyList<string>? AlternativePermissions => [Permissions.ProcurementManage];

    protected ProcurementRequestService Requests { get; } = requests;

    /// <summary>Любое право снабжения — для инструментов заявок, которые видит и обычный сотрудник (свои заявки).</summary>
    protected static readonly IReadOnlyList<string> AnyProcurement =
        [Permissions.ProcurementRead, Permissions.ProcurementManage, Permissions.ProcurementWarehouse, Permissions.ProcurementSettings];

    protected static readonly (string, object) FromArg = ("from", new { type = "string", description = "Начало периода, yyyy-MM-dd." });
    protected static readonly (string, object) ToArg = ("to", new { type = "string", description = "Конец периода включительно, yyyy-MM-dd. Без даты — сегодня." });
    protected static readonly (string, object) CategoryArg = ("category", new { type = "string", description = "Категория закупок: название или код (например «Сырьё», raw_materials)." });
    protected static readonly (string, object) SupplierArg = ("supplier", new { type = "string", description = "Поставщик: название или ИНН." });
    protected static readonly (string, object) SearchArg = ("search", new { type = "string", description = "Поиск по номеру, названию или позиции." });

    protected static (string, object) LimitArg(int byDefault) => ("limit", new { type = "integer", description = $"Сколько строк списка вернуть, 1–50 (по умолчанию {byDefault})." });

    protected static int Limit(ToolArgs args, int byDefault) => args.Cap(args.Int("limit", 1, 50) ?? byDefault);

    protected async Task<ProcurementContext> ContextAsync(ToolContext context, CancellationToken ct)
    {
        var userId = context.UserId ?? throw new ToolArgumentException("Данные снабжения отдаются только от имени пользователя.");
        return await Requests.ContextAsync(userId, await permissions.GetAsync(userId, ct), ct);
    }

    /// <summary>Период from–to; без from — defaultDays дней по to включительно, 0 — с начала месяца.</summary>
    protected static (DateOnly From, DateOnly To) Period(ToolArgs args, int defaultDays)
    {
        var to = WorkFieldText.Date(args, "to") ?? FieldClock.Today;
        var from = WorkFieldText.Date(args, "from") ?? (defaultDays == 0 ? new DateOnly(to.Year, to.Month, 1) : to.AddDays(1 - defaultDays));
        if (from > to)
        {
            throw new ToolArgumentException("Начало периода позже конца.");
        }

        return to.DayNumber - from.DayNumber > 3 * 366 ? throw new ToolArgumentException("Период — не длиннее трёх лет.") : (from, to);
    }

    protected static string Range(DateOnly from, DateOnly to) => $"{from:dd.MM.yyyy}–{to:dd.MM.yyyy}";

    protected static string Day(DateOnly date) => date.ToString("yyyy-MM-dd");

    protected static string? Day(DateOnly? date) => date?.ToString("yyyy-MM-dd");

    protected static string? Day(DateTimeOffset? at) => at is { } v ? Day(FieldClock.LocalDate(v)) : null;

    protected async Task<ProcurementCategoryView?> CategoryAsync(ProcurementContext ctx, ToolArgs args, CancellationToken ct)
    {
        if (args.Str("category") is not { } query)
        {
            return null;
        }

        var all = await Requests.CategoriesAsync(ctx, false, ct);
        return all.FirstOrDefault(c => string.Equals(c.Code, query, StringComparison.OrdinalIgnoreCase))
               ?? WorkFieldText.Find(all, query, c => c.Name, "category", "Категории");
    }

    protected static async Task<ProcurementSupplierRow?> SupplierAsync(ProcurementSupplierService suppliers, ProcurementContext ctx, ToolArgs args, CancellationToken ct)
    {
        if (args.Str("supplier") is not { } query)
        {
            return null;
        }

        var found = (await suppliers.SearchAsync(ctx, new ProcurementSupplierSearch(query, Active: null, PageSize: 20), ct)).Items;
        return found.FirstOrDefault(s => string.Equals(s.Name.Trim(), query, StringComparison.OrdinalIgnoreCase) || s.Inn == query)
               ?? found.Count switch
               {
                   1 => found[0],
                   0 => throw new FieldNotFoundException($"Поставщик «{query}» не найден в реестре снабжения."),
                   _ => throw new ToolArgumentException($"Под «{query}» подходят несколько поставщиков: {string.Join(", ", found.Take(10).Select(s => s.Name))}. Уточни название."),
               };
    }

    protected static string Label(ProcurementRequestStatus status) => ProcurementRules.Label(status);

    protected static string Label(ProcurementOrderStatus status) => ProcurementOrderRules.Label(status);

    /// <summary>Значение аргумента-перечисления: код из списка или ошибка со списком допустимых.</summary>
    protected static string? Choice(ToolArgs args, string name, IReadOnlyDictionary<string, string> allowed)
    {
        if (args.Str(name) is not { } raw)
        {
            return null;
        }

        return allowed.TryGetValue(raw.ToLowerInvariant(), out var value)
            ? value
            : throw new ToolArgumentException($"Аргумент {name}: одно из {string.Join(", ", allowed.Keys)}.");
    }

    protected static object Request(ProcurementRequestRow r) => new
    {
        r.Number,
        r.Title,
        Status = Label(r.Status),
        Priority = ProcurementRules.Label(r.Priority),
        Department = r.DepartmentName,
        Initiator = r.InitiatorName,
        Items = r.ItemsCount,
        r.FirstItem,
        r.Categories,
        EstimatedTotal = R(r.EstimatedTotal),
        PurchaseTotal = R(r.PurchaseTotal),
        DesiredDate = Day(r.DesiredDate),
        Created = Day(r.CreatedAt),
        Submitted = Day(r.SubmittedAt),
        Link = $"/requests/{r.Id}",
    };

    protected static object Order(ProcurementOrderRow o) => new
    {
        o.Number,
        Status = Label(o.Status),
        Supplier = o.SupplierName,
        Responsible = o.ResponsibleName,
        o.Lines,
        o.FirstItem,
        o.Requests,
        Total = R(o.Total),
        OrderDate = Day(o.OrderDate),
        ExpectedDate = Day(o.ExpectedDate),
        o.Overdue,
        Link = $"/procurement/orders/{o.Id}",
    };
}

/// <summary>1. Обзор снабжения для пользователя: что он видит, заявки, заказы, склад, KPI и сигналы для рекомендаций.</summary>
internal sealed class GetProcurementOverviewTool(
    ProcurementRequestService requests,
    IUserPermissions permissions,
    ProcurementOrderService orders,
    ProcurementStockService stock,
    ProcurementAnalyticsService analytics,
    ProcurementInsightService insight) : ProcurementTool(requests, permissions)
{
    public override string Name => "get_procurement_overview";
    public override string Title => "Снабжение: обзор";
    public override string? RequiredPermission => Permissions.ProcurementRequest;
    public override IReadOnlyList<string>? AlternativePermissions => AnyProcurement;

    public override string Description =>
        "Обзор снабжения с правами пользователя: что ему видно, заявки по статусам и что ждёт действия, заказы поставщикам (на согласовании, " +
        "просроченные, на неделе), склад снабжения (ниже минимума), KPI закупок за период (время закупки, поставки в срок, экономия, претензии) и " +
        "сигналы за 90 дней — рост цен, переплата, повторные закупки, слабые поставщики, заказы для объединения — с подсказкой, где смотреть дальше.";

    public override string? UsageHint => "Общий вопрос о закупках, заявках или складе снабжения — сначала get_procurement_overview.";

    public override JsonElement InputSchema { get; } = Schema(FromArg, ToArg);

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        var ctx = await ContextAsync(context, ct);
        var (from, to) = Period(args, 0);
        var overview = await Requests.OverviewAsync(ctx, ct);
        var leader = ctx.ReadsAll || ctx.IsDirector;
        var seesOrders = leader || ctx.Warehouse;
        var orderSummary = seesOrders ? await orders.SummaryAsync(ctx, ct) : null;
        var stockSummary = ctx.SeesStock ? await stock.SummaryAsync(ctx, ct) : null;
        object? kpi = null;
        object? signals = null;
        if (leader)
        {
            var k = await analytics.KpisAsync(ProcurementFilter.Of(from, to), ct);
            kpi = new
            {
                Period = Range(from, to),
                k.AvgPurchaseDays,
                k.PurchasesMeasured,
                k.OnTimePercent,
                k.DeliveriesMeasured,
                Savings = R(k.Savings),
                k.SavingsPercent,
                k.FulfilledRequests,
                k.Claims,
                k.SupplierQuality,
                k.SuppliersRated,
            };
            var prices = await insight.PricesAsync(ctx, new ProcurementPriceQuery(to.AddDays(-89), to), ct);
            var weak = (await insight.SuppliersAsync(ctx, ProcurementFilter.Of(to.AddDays(-364), to), ct)).Count(s => s.Weak.Count > 0);
            var groups = await insight.ConsolidationAsync(ctx, ct);
            signals = new
            {
                Period = Range(to.AddDays(-89), to),
                PriceRiseItems = prices.Items.Count(i => i.ChangePercent >= 10),
                OverpayVsBestPrice = R(prices.Items.Sum(i => i.Overpay)),
                RepeatPurchaseItems = prices.Items.Count(i => i.Purchases >= ProcurementInsightService.RepeatPurchases),
                WeakSuppliersYear = weak,
                ConsolidationGroups = groups.Count,
                Next = "Рост цен, переплаты и повторные закупки — get_purchase_price_history; слабые поставщики — get_supplier_history без поставщика; " +
                       "потребности для одного заказа — get_pending_approvals; просрочки — get_overdue_deliveries; ниже минимума — get_low_stock_items.",
            };
        }

        var sources = new List<DataSource> { new("OneBase → Заявки", null, "/requests") };
        if (ctx.SeesStock)
        {
            sources.Add(new DataSource("OneBase → Снабжение → Обзор", Range(from, to), "/procurement"));
        }

        return Data(new
        {
            Access = new
            {
                Requests = leader ? "все заявки компании" : ctx.HeadOf.Count > 0 ? "свои заявки и заявки отделов, которыми руководит" : ctx.Warehouse ? "свои заявки и заявки с выдачей со склада" : "только свои заявки",
                OrdersAndSuppliers = seesOrders,
                WarehouseStock = ctx.SeesStock,
                PricesExpensesKpi = leader,
            },
            Requests = new
            {
                overview.Open,
                overview.UrgentOpen,
                ByStatus = overview.ByStatus.Where(s => s.Value > 0).ToDictionary(s => Enum.TryParse<ProcurementRequestStatus>(s.Key, out var st) ? Label(st) : s.Key, s => s.Value),
                ThisMonth = new { Requests = overview.MonthRequests, EstimatedTotal = R(overview.MonthTotal), ByCategory = overview.MonthCategories.Take(5).Select(c => new { c.Name, c.Requests, Total = R(c.Total) }) },
                NeedsAction = overview.NeedsAction.TakeTop(args, 5, "needsAction").Select(Request),
            },
            Orders = orderSummary is { } o ? new { o.Open, o.Draft, o.AwaitingApproval, o.Overdue, o.ExpectedWeek, PartiallyDelivered = o.Partial, OpenTotal = R(o.OpenTotal) } : null,
            Stock = stockSummary is { } s ? new { s.Items, BelowMinimum = s.Low, Empty = s.Empty, s.WithReserve, RequestsToIssue = s.ToIssueRequests } : null,
            Kpi = kpi,
            Signals = signals,
            Note = "Суммы — сумы с НДС. Оплаты поставщикам появятся с модулем «Финансы». Разделы, которых нет в результате, пользователю не открыты.",
        }, [.. sources]);
    }
}

/// <summary>2. Заявки на закупку с фильтрами — в области видимости пользователя.</summary>
internal sealed class GetPurchaseRequestsTool(ProcurementRequestService requests, IUserPermissions permissions, IAppDbContext db) : ProcurementTool(requests, permissions)
{
    internal static readonly IReadOnlyDictionary<string, string> Statuses = new Dictionary<string, string>
    {
        ["open"] = "open",
        ["closed"] = "closed",
        ["draft"] = "draft",
        ["submitted"] = nameof(ProcurementRequestStatus.Submitted),
        ["head_approval"] = nameof(ProcurementRequestStatus.HeadApproval),
        ["finance_approval"] = nameof(ProcurementRequestStatus.FinanceApproval),
        ["director_approval"] = nameof(ProcurementRequestStatus.DirectorApproval),
        ["approved"] = nameof(ProcurementRequestStatus.Approved),
        ["rejected"] = nameof(ProcurementRequestStatus.Rejected),
        ["revision_required"] = nameof(ProcurementRequestStatus.RevisionRequired),
        ["partially_fulfilled"] = nameof(ProcurementRequestStatus.PartiallyFulfilled),
        ["fulfilled"] = nameof(ProcurementRequestStatus.Fulfilled),
        ["cancelled"] = nameof(ProcurementRequestStatus.Cancelled),
    };

    private static readonly IReadOnlyDictionary<string, string> Priorities = new Dictionary<string, string>
    {
        ["low"] = nameof(ProcurementPriority.Low),
        ["normal"] = nameof(ProcurementPriority.Normal),
        ["high"] = nameof(ProcurementPriority.High),
        ["urgent"] = nameof(ProcurementPriority.Urgent),
    };

    private static readonly IReadOnlyDictionary<string, string> Scopes = new Dictionary<string, string> { ["all"] = "all", ["my"] = "my", ["department"] = "department" };

    public override string Name => "get_purchase_requests";
    public override string Title => "Снабжение: заявки на закупку";
    public override string? RequiredPermission => Permissions.ProcurementRequest;
    public override IReadOnlyList<string>? AlternativePermissions => AnyProcurement;

    public override string Description =>
        "Заявки на закупку, видимые пользователю: номер, статус, приоритет, отдел, инициатор, позиции, категории, сумма по ценам заявки и к " +
        "закупке, желаемая дата, даты создания и отправки. Фильтры: статус, приоритет, свои/отдела/все, категория, отдел, поиск, период создания.";

    public override JsonElement InputSchema { get; } = Schema(
        ("status", new { type = "string", description = "Статус; open — в работе.", @enum = Statuses.Keys.ToArray() }),
        ("priority", new { type = "string", description = "Приоритет.", @enum = Priorities.Keys.ToArray() }),
        ("scope", new { type = "string", description = "my — свои, department — отдела, all — все видимые (по умолчанию).", @enum = Scopes.Keys.ToArray() }),
        CategoryArg,
        ("department", new { type = "string", description = "Отдел-инициатор: название или код." }),
        SearchArg,
        FromArg,
        ToArg,
        LimitArg(15));

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        var ctx = await ContextAsync(context, ct);
        var category = await CategoryAsync(ctx, args, ct);
        string? department = null;
        if (args.Str("department") is { } deptQuery)
        {
            var departments = await db.Departments.AsNoTracking().Select(d => new { d.Code, d.Name }).ToListAsync(ct);
            department = departments.FirstOrDefault(d => string.Equals(d.Code, deptQuery, StringComparison.OrdinalIgnoreCase))?.Code
                         ?? WorkFieldText.Find(departments, deptQuery, d => d.Name, "department", "Отделы")!.Code;
        }

        var from = WorkFieldText.Date(args, "from");
        var to = WorkFieldText.Date(args, "to");
        var limit = Limit(args, 15);
        var page = await Requests.SearchAsync(ctx, new ProcurementRequestSearch(Choice(args, "scope", Scopes) ?? "all", Choice(args, "status", Statuses),
            Choice(args, "priority", Priorities), category?.Id, department, from, to, args.Str("search"), "created", true, 1, Math.Min(limit, 200)), ct);
        args.Count("requests", page.Total, page.Items.Count);
        return Data(new
        {
            Total = page.Total,
            Shown = page.Items.Count,
            EstimatedTotalShown = R(page.Items.Sum(r => r.EstimatedTotal)),
            Requests = page.Items.Select(Request),
        }, new DataSource("OneBase → Заявки", from is null && to is null ? null : $"создано {(from is { } f ? $"с {f:dd.MM.yyyy} " : "")}{(to is { } t ? $"по {t:dd.MM.yyyy}" : "")}".Trim(), "/requests"));
    }
}

/// <summary>3. Что ждёт решения: заявки и заказы на согласовании директора, заявки у снабжения и на доработке, потребности без заказа.</summary>
internal sealed class GetPendingApprovalsTool(ProcurementRequestService requests, IUserPermissions permissions, ProcurementOrderService orders, ProcurementInsightService insight)
    : ProcurementTool(requests, permissions)
{
    public override string Name => "get_pending_approvals";
    public override string Title => "Снабжение: что ждёт согласования";
    public override string? RequiredPermission => Permissions.ProcurementRequest;
    public override IReadOnlyList<string>? AlternativePermissions => AnyProcurement;

    public override string Description =>
        "Что ждёт решения (в видимости пользователя): заявки у директора (сколько дней ждут), отправленные, но не обработанные снабжением, на " +
        "доработке, заказы поставщикам на согласовании; для снабжения и директора — утверждённые потребности без заказа и группы потребностей " +
        "одной категории из разных заявок, которые можно заказать одним заказом.";

    public override JsonElement InputSchema { get; } = Schema(LimitArg(10));

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        var ctx = await ContextAsync(context, ct);
        var limit = Limit(args, 10);
        var today = FieldClock.Today;

        async Task<object> Queue(string status, string name, Func<ProcurementRequestRow, DateTimeOffset?> since)
        {
            var page = await Requests.SearchAsync(ctx, new ProcurementRequestSearch("all", status, Sort: "created", Desc: false, PageSize: Math.Min(limit, 200)), ct);
            args.Count(name, page.Total, page.Items.Count);
            return new
            {
                Total = page.Total,
                EstimatedTotalShown = R(page.Items.Sum(r => r.EstimatedTotal)),
                Items = page.Items.Select(r => new
                {
                    r.Number,
                    r.Title,
                    Department = r.DepartmentName,
                    Initiator = r.InitiatorName,
                    Priority = ProcurementRules.Label(r.Priority),
                    EstimatedTotal = R(r.EstimatedTotal),
                    PurchaseTotal = R(r.PurchaseTotal),
                    WaitingDays = since(r) is { } at ? today.DayNumber - FieldClock.LocalDate(at).DayNumber : (int?)null,
                    Link = $"/requests/{r.Id}",
                }),
            };
        }

        var head = await Queue(nameof(ProcurementRequestStatus.HeadApproval), "headApproval", r => r.ForwardedAt ?? r.SubmittedAt);
        var finance = await Queue(nameof(ProcurementRequestStatus.FinanceApproval), "financeApproval", r => r.ForwardedAt ?? r.SubmittedAt);
        var director = await Queue(nameof(ProcurementRequestStatus.DirectorApproval), "directorApproval", r => r.ForwardedAt ?? r.SubmittedAt);
        var supply = await Queue(nameof(ProcurementRequestStatus.Submitted), "submitted", r => r.SubmittedAt);
        var revision = await Queue(nameof(ProcurementRequestStatus.RevisionRequired), "revisionRequired", r => r.SubmittedAt);
        object? ordersAwaiting = null;
        object? needs = null;
        object? consolidation = null;
        if (ctx.ReadsAll || ctx.IsDirector || ctx.Warehouse)
        {
            var page = await orders.SearchAsync(ctx, new ProcurementOrderSearch("awaiting", PageSize: Math.Min(limit, 200)), ct);
            args.Count("ordersAwaitingApproval", page.Total, page.Items.Count);
            ordersAwaiting = new { Total = page.Total, TotalSumShown = R(page.Items.Sum(o => o.Total)), Items = page.Items.Select(Order) };
            var waiting = await orders.NeedsAsync(ctx, null, ct);
            needs = new
            {
                Lines = waiting.Count,
                Requests = waiting.Select(n => n.RequestId).Distinct().Count(),
                EstimatedTotal = R(waiting.Where(n => n.EstimatedPrice is not null).Sum(n => n.Remaining * n.EstimatedPrice!.Value)),
                Items = waiting.TakeTop(args, limit, "needsToOrder").Select(n => new { Request = n.RequestNumber, Department = n.DepartmentName, n.Name, n.UnitCode, n.Remaining, EstimatedPrice = R(n.EstimatedPrice), Category = n.CategoryName }),
            };
        }

        if (ctx.ReadsAll || ctx.IsDirector)
        {
            consolidation = (await insight.ConsolidationAsync(ctx, ct)).TakeTop(args, limit, "consolidation")
                .Select(c => new { Category = c.CategoryName, c.Requests, c.Lines, EstimatedTotal = R(c.EstimatedTotal), RequestNumbers = c.RequestNumbers.Take(10), Departments = c.Departments.Take(10) });
        }

        return Data(new
        {
            HeadApprovalRequests = head,
            FinanceApprovalRequests = finance,
            DirectorApprovalRequests = director,
            SubmittedWaitingForSupply = supply,
            RevisionRequired = revision,
            OrdersAwaitingDirector = ordersAwaiting,
            ApprovedNotOrdered = needs,
            CanOrderTogether = consolidation,
            Note = "Маршрут: руководитель отдела → финансист → директор (если сумма к закупке больше лимита) → снабжение. WaitingDays — дней на текущем шаге. "
                   + "SubmittedWaitingForSupply — заявки, отправленные до нового маршрута. Решают согласующие; AI решений не принимает.",
        }, new DataSource("OneBase → Заявки → Согласование", null, "/requests/approvals"));
    }
}

/// <summary>4. Заказы поставщикам с фильтрами.</summary>
internal sealed class GetPurchaseOrdersTool(ProcurementRequestService requests, IUserPermissions permissions, ProcurementOrderService orders, ProcurementSupplierService suppliers)
    : ProcurementTool(requests, permissions)
{
    private static readonly IReadOnlyDictionary<string, string> Statuses = new Dictionary<string, string>
    {
        ["any"] = "any",
        ["open"] = "open",
        ["draft"] = nameof(ProcurementOrderStatus.Draft),
        ["awaiting"] = "awaiting",
        ["approved"] = nameof(ProcurementOrderStatus.Approved),
        ["ordered"] = nameof(ProcurementOrderStatus.Ordered),
        ["expected"] = "expected",
        ["overdue"] = "overdue",
        ["partial"] = "partial",
        ["done"] = "done",
        ["cancelled"] = "cancelled",
    };

    public override string Name => "get_purchase_orders";
    public override string Title => "Снабжение: заказы поставщикам";
    public override IReadOnlyList<string>? AlternativePermissions => [Permissions.ProcurementManage, Permissions.ProcurementWarehouse];

    public override string Description =>
        "Заказы поставщикам: номер, статус, поставщик, ответственный, позиции, заявки, сумма с НДС, дата заказа, ожидаемая дата, просрочка; " +
        "сводка (открытые, на согласовании, просроченные, на неделе, частично поставлены, сумма открытых). Фильтры: статус, поставщик, поиск, период создания.";

    public override JsonElement InputSchema { get; } = Schema(
        ("status", new { type = "string", description = "Статус (по умолчанию open); expected — ожидаются за 7 дней, awaiting — у директора.", @enum = Statuses.Keys.ToArray() }),
        SupplierArg,
        SearchArg,
        FromArg,
        ToArg,
        LimitArg(15));

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        var ctx = await ContextAsync(context, ct);
        var supplier = await SupplierAsync(suppliers, ctx, args, ct);
        var limit = Limit(args, 15);
        var summary = await orders.SummaryAsync(ctx, ct);
        var page = await orders.SearchAsync(ctx, new ProcurementOrderSearch(Choice(args, "status", Statuses) ?? "open", supplier?.Id, null, args.Str("search"),
            WorkFieldText.Date(args, "from"), WorkFieldText.Date(args, "to"), "created", true, 1, Math.Min(limit, 200)), ct);
        args.Count("orders", page.Total, page.Items.Count);
        return Data(new
        {
            Summary = new { summary.Open, summary.Draft, summary.AwaitingApproval, summary.Overdue, summary.ExpectedWeek, PartiallyDelivered = summary.Partial, CancelledThisMonth = summary.CancelledMonth, OpenTotal = R(summary.OpenTotal) },
            Total = page.Total,
            TotalSumShown = R(page.Items.Sum(o => o.Total)),
            Orders = page.Items.Select(Order),
        }, new DataSource("OneBase → Снабжение → Заказы", null, "/procurement/orders"));
    }
}

/// <summary>5. Просроченные и скорые поставки по заказам, которые ждут приёмки.</summary>
internal sealed class GetOverdueDeliveriesTool(ProcurementRequestService requests, IUserPermissions permissions, ProcurementReceiptService receipts)
    : ProcurementTool(requests, permissions)
{
    public override string Name => "get_overdue_deliveries";
    public override string Title => "Снабжение: просроченные поставки";
    public override IReadOnlyList<string>? AlternativePermissions => [Permissions.ProcurementManage, Permissions.ProcurementWarehouse];

    public override string Description =>
        "Поставки, которые ждут приёмки: просроченные (на сколько дней, поставщик, ответственный, сколько позиций не пришло, заявки), ожидаемые " +
        "в ближайшие дни, без даты; просрочки по поставщикам. Только заказы, оформленные у поставщика и не закрытые.";

    public override JsonElement InputSchema { get; } = Schema(
        ("days_ahead", new { type = "integer", description = "Сколько дней вперёд считать «скоро» (0–30, по умолчанию 3)." }),
        LimitArg(15));

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        var ctx = await ContextAsync(context, ct);
        var ahead = args.Int("days_ahead", 0, 30) ?? 3;
        var limit = Limit(args, 15);
        var today = FieldClock.Today;
        var expected = await receipts.ExpectedAsync(ctx, ct);
        object Row(ProcurementExpectedRow r) => new
        {
            r.Number,
            Status = Label(r.Status),
            Supplier = r.SupplierName,
            Responsible = r.ResponsibleName,
            ExpectedDate = Day(r.ExpectedDate),
            DaysOverdue = r.Overdue && r.ExpectedDate is { } d ? today.DayNumber - d.DayNumber : (int?)null,
            r.Lines,
            WaitingLines = r.Waiting,
            r.Requests,
            DraftReceipts = r.Drafts,
            Link = $"/procurement/orders/{r.OrderId}",
        };

        var overdue = expected.Where(r => r.Overdue).OrderBy(r => r.ExpectedDate).ToList();
        var soon = expected.Where(r => !r.Overdue && r.ExpectedDate is { } d && d <= today.AddDays(ahead)).OrderBy(r => r.ExpectedDate).ToList();
        return Data(new
        {
            Today = Day(today),
            Waiting = expected.Count,
            OverdueCount = overdue.Count,
            Overdue = overdue.TakeTop(args, limit, "overdue").Select(Row),
            DueSoonDays = ahead,
            DueSoon = soon.TakeTop(args, limit, "dueSoon").Select(Row),
            WithoutExpectedDate = expected.Count(r => r.ExpectedDate is null),
            OverdueBySupplier = overdue.GroupBy(r => r.SupplierName).Select(g => new { Supplier = g.Key, Orders = g.Count(), MaxDaysOverdue = g.Max(r => today.DayNumber - r.ExpectedDate!.Value.DayNumber) })
                .OrderByDescending(g => g.Orders).TakeTop(args, 10, "overdueBySupplier"),
        }, new DataSource("OneBase → Снабжение → Приёмка", $"на {today:dd.MM.yyyy}", "/procurement/receipts"));
    }
}

/// <summary>6. Остатки склада снабжения.</summary>
internal sealed class GetWarehouseStockTool(ProcurementRequestService requests, IUserPermissions permissions, ProcurementStockService stock)
    : ProcurementTool(requests, permissions)
{
    private static readonly IReadOnlyDictionary<string, string> Filters = new Dictionary<string, string>
    {
        ["active"] = "active", ["low"] = "low", ["empty"] = "empty", ["reserved"] = "reserved", ["all"] = "all",
    };

    public override string Name => "get_warehouse_stock";
    public override string Title => "Снабжение: остатки склада";
    public override IReadOnlyList<string>? AlternativePermissions => [Permissions.ProcurementManage, Permissions.ProcurementWarehouse];

    public override string Description =>
        "Склад снабжения OneBase (не склады продаж и дилеров): SKU, название, категория, на складе, в резерве, доступно, минимальный остаток, " +
        "последнее движение; сводка (позиций, ниже минимума, пустых, с резервом, заявок к выдаче). Фильтры: поиск, категория, состояние.";

    public override JsonElement InputSchema { get; } = Schema(
        SearchArg,
        CategoryArg,
        ("filter", new { type = "string", description = "active (по умолчанию), low — ниже минимума, empty — пустые, reserved — с резервом.", @enum = Filters.Keys.ToArray() }),
        LimitArg(20));

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        var ctx = await ContextAsync(context, ct);
        var category = await CategoryAsync(ctx, args, ct);
        var limit = Limit(args, 20);
        var summary = await stock.SummaryAsync(ctx, ct);
        var page = await stock.StockAsync(ctx, new ProcurementStockSearch(args.Str("search"), category?.Id, Choice(args, "filter", Filters), null, false, 1, Math.Min(limit, 200)), ct);
        args.Count("items", page.Total, page.Items.Count);
        return Data(new
        {
            Summary = new { summary.Items, BelowMinimum = summary.Low, summary.Empty, summary.WithReserve, RequestsToIssue = summary.ToIssueRequests },
            Total = page.Total,
            Items = page.Items.Select(i => new
            {
                i.Sku,
                i.Name,
                Category = i.CategoryName,
                Unit = i.UnitCode,
                i.OnHand,
                i.Reserved,
                i.Available,
                i.MinQty,
                BelowMinimum = i.Low,
                LastMovement = Day(i.LastMovementAt),
            }),
        }, new DataSource("OneBase → Снабжение → Склад", $"на {FieldClock.Today:dd.MM.yyyy}", "/procurement/stock"));
    }
}

/// <summary>7. История поставщика или рейтинг поставщиков с причинами слабости.</summary>
internal sealed class GetSupplierHistoryTool(ProcurementRequestService requests, IUserPermissions permissions, ProcurementSupplierService suppliers, ProcurementInsightService insight)
    : ProcurementTool(requests, permissions)
{
    public override string Name => "get_supplier_history";
    public override string Title => "Снабжение: история поставщика";
    public override IReadOnlyList<string>? AlternativePermissions => [Permissions.ProcurementManage, Permissions.ProcurementWarehouse];

    public override string Description =>
        "С поставщиком — его история: категории, срок поставки, условия оплаты, рейтинг 0–100 и части (в срок, качество, цены, претензии, " +
        "полнота), заказы, закупочные цены, претензии. Без поставщика — поставщики с закупками за период (по умолчанию год) по сумме, с " +
        "рейтингом, опозданиями, просрочками, претензиями и причинами слабости. Контакты и реквизиты не отдаются.";

    public override JsonElement InputSchema { get; } = Schema(SupplierArg, FromArg, ToArg, LimitArg(15));

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        var ctx = await ContextAsync(context, ct);
        var limit = Limit(args, 15);
        if (await SupplierAsync(suppliers, ctx, args, ct) is { } row)
        {
            var card = await suppliers.GetAsync(ctx, row.Id, ct);
            var r = card.Rating;
            args.Count("orders", card.Orders.Count, Math.Min(card.Orders.Count, limit));
            args.Count("prices", card.Prices.Count, Math.Min(card.Prices.Count, limit));
            return Data(new
            {
                card.Name,
                card.Inn,
                Categories = card.Categories.Select(c => c.Name),
                card.LeadTimeDays,
                card.PaymentTerms,
                Active = card.IsActive,
                Rating = new
                {
                    r.Overall,
                    OnTimePercent = r.OnTime,
                    r.Quality,
                    r.Prices,
                    r.Claims,
                    r.Completeness,
                    DueOrders = r.DueOrders,
                    OverdueNow = r.OverdueOrders,
                    FinishedOrders = r.FinishedOrders,
                    LateOrders = r.LateOrders,
                    ClaimLines = r.ClaimLines,
                    RatedOffers = r.RatedOffers,
                },
                OrdersTotal = R(card.OrdersTotal),
                Offers = card.Offers,
                Orders = card.Orders.Take(limit).Select(o => new { o.Number, Status = Label(o.Status), OrderDate = Day(o.OrderDate), ExpectedDate = Day(o.ExpectedDate), Total = R(o.Total), o.Overdue, o.Lines, Link = $"/procurement/orders/{o.Id}" }),
                Prices = card.Prices.Take(limit).Select(p => new { Date = Day(p.Date), Item = p.ItemName, Unit = p.UnitCode, Price = R(p.UnitPrice, 2), p.Quantity, Order = p.OrderNumber }),
                Claims = (card.Claims ?? []).TakeTop(args, 10, "claims").Select(c => new { Receipt = c.ReceiptNumber, Order = c.OrderNumber, c.Name, c.Defective, c.Shortage, Amount = R(c.Amount), Status = c.Status.ToString(), c.Resolution }),
                Note = "Рейтинг — за всё время работы с поставщиком. Цены в истории — как в заказе (с НДС или без — по условиям заказа).",
            }, new DataSource($"OneBase → Снабжение → Поставщики → {card.Name}", null, $"/procurement/suppliers/{card.Id}"));
        }

        var (from, to) = Period(args, 365);
        if (ctx.ReadsAll || ctx.IsDirector)
        {
            var list = await insight.SuppliersAsync(ctx, ProcurementFilter.Of(from, to), ct);
            return Data(new
            {
                Period = Range(from, to),
                Suppliers = list.Count,
                Weak = list.Count(s => s.Weak.Count > 0),
                Items = list.TakeTop(args, limit, "suppliers").Select(s => new
                {
                    s.Name,
                    s.Orders,
                    Total = R(s.Total),
                    s.Rating,
                    OnTimePercent = s.OnTime,
                    s.Quality,
                    s.Prices,
                    s.Completeness,
                    OverdueNow = s.Overdue,
                    LateOrders = s.Late,
                    FinishedOrders = s.Finished,
                    s.ClaimLines,
                    WeakBecause = s.Weak,
                    Link = $"/procurement/suppliers/{s.Id}",
                }),
                WeakSuppliers = list.Where(s => s.Weak.Count > 0).TakeTop(args, limit, "weak").Select(s => new { s.Name, s.Rating, WeakBecause = s.Weak }),
                Note = "Заказы и суммы — утверждённые за период; рейтинг, опоздания и претензии — за всё время. Слабый: рейтинг ниже 60, опоздал с 30%+ поставок, есть просрочка или претензии.",
            }, new DataSource("OneBase → Снабжение → Поставщики", Range(from, to), "/procurement/suppliers"));
        }

        var registry = await suppliers.SearchAsync(ctx, new ProcurementSupplierSearch(Sort: "total", Desc: true, PageSize: Math.Min(limit, 200)), ct);
        args.Count("suppliers", registry.Total, registry.Items.Count);
        return Data(new
        {
            Total = registry.Total,
            Items = registry.Items.Select(s => new { s.Name, s.Categories, s.Orders, Total = R(s.OrdersTotal), s.Rating, OverdueNow = s.Overdue, s.LeadTimeDays, LastOrder = Day(s.LastOrderAt) }),
            Note = "Реестр поставщиков за всё время; разбор за период и причины слабости видят снабжение и директор.",
        }, new DataSource("OneBase → Снабжение → Поставщики", null, "/procurement/suppliers"));
    }
}

/// <summary>8. Сравнение коммерческих предложений по заявке.</summary>
internal sealed class CompareSupplierOffersTool(ProcurementRequestService requests, IUserPermissions permissions, ProcurementOrderService orders, ProcurementSupplierService suppliers)
    : ProcurementTool(requests, permissions)
{
    public override string Name => "compare_supplier_offers";
    public override string Title => "Снабжение: сравнение предложений";

    public override string Description =>
        "Коммерческие предложения по заявке: каждое — поставщик и рейтинг, срок и стоимость доставки, НДС, оплата, гарантия, качество, срок " +
        "действия, итог с НДС, покрытие позиций, лучший итог и самый быстрый, цены по позициям; по каждой позиции — лучшая цена, поставщик и " +
        "экономия против цены в заявке.";

    public override JsonElement InputSchema { get; } = Schema(("request", new { type = "string", description = "Номер заявки, например PR-2026-00012." }));

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        var number = args.Str("request") ?? throw new ToolArgumentException("Укажи номер заявки (request), например PR-2026-00012; найти заявку — get_purchase_requests.");
        var ctx = await ContextAsync(context, ct);
        var found = (await Requests.SearchAsync(ctx, new ProcurementRequestSearch(Search: number, PageSize: 10), ct)).Items;
        var request = found.FirstOrDefault(r => string.Equals(r.Number, number, StringComparison.OrdinalIgnoreCase))
                      ?? (found.Count == 1 ? found[0] : throw new FieldNotFoundException($"Заявка «{number}» не найдена среди заявок, которые видит пользователь."));
        var c = await orders.CompareAsync(ctx, request.Id, suppliers, ct);
        var live = c.Offers.Where(o => o.Status != ProcurementOfferStatus.Rejected).ToList();
        var best = c.Items.Select(i =>
            {
                var prices = live.SelectMany(o => o.Lines.Where(l => l.RequestItemId == i.RequestItemId).Select(l => (o.SupplierName, l.GrossPrice))).OrderBy(p => p.GrossPrice).ToList();
                var top = prices.FirstOrDefault();
                return new
                {
                    i.Name,
                    Unit = i.UnitCode,
                    i.ToPurchase,
                    i.OrderedQty,
                    EstimatedPrice = R(i.EstimatedPrice, 2),
                    Offers = prices.Count,
                    BestPrice = prices.Count > 0 ? R(top.GrossPrice, 2) : (decimal?)null,
                    BestSupplier = prices.Count > 0 ? top.SupplierName : null,
                    SavingVsRequest = prices.Count > 0 && i.EstimatedPrice is { } est ? R((est - top.GrossPrice) * i.ToPurchase) : (decimal?)null,
                };
            })
            .ToList();
        return Data(new
        {
            c.Number,
            c.Title,
            Status = Label(c.Status),
            Items = best,
            Offers = c.Offers.Select(o => new
            {
                Supplier = o.SupplierName,
                o.SupplierRating,
                o.SupplierOrders,
                o.SupplierOverdue,
                Status = o.Status.ToString(),
                ValidUntil = Day(o.ValidUntil),
                o.Expired,
                o.DeliveryDays,
                DeliveryCost = R(o.DeliveryCost),
                o.VatRate,
                o.PricesIncludeVat,
                o.PaymentTerms,
                o.Warranty,
                o.Quality,
                Total = R(o.Total),
                CoveredItems = o.Covered,
                ItemsInRequest = c.Items.Count,
                BestTotal = o.BestTotal,
                o.Fastest,
                Lines = o.Lines.Select(l => new { l.Name, l.Quantity, GrossPrice = R(l.GrossPrice, 2), Sum = R(l.Sum), l.BestPrice }),
            }),
            Note = live.Count < 2 ? "Сравнивать не с чем: живых предложений меньше двух." : "BestTotal и BestPrice — среди неотклонённых предложений; итог с НДС и доставкой.",
        }, new DataSource($"OneBase → Заявки → {c.Number} → Предложения", null, $"/requests/{c.RequestId}/offers"));
    }
}

/// <summary>9. Расходы на закупки за период.</summary>
internal sealed class GetProcurementExpensesTool(ProcurementRequestService requests, IUserPermissions permissions, ProcurementInsightService insight, ProcurementSupplierService suppliers, IAppDbContext db)
    : ProcurementTool(requests, permissions)
{
    public override string Name => "get_procurement_expenses";
    public override string Title => "Снабжение: расходы на закупки";

    public override string Description =>
        "Расходы на закупки за период (позиции утверждённых заказов с НДС, без скидки и доставки; по умолчанию текущий месяц): итог, заказов, " +
        "предыдущий такой же период и изменение, по отделам, категориям, поставщикам и месяцам. Фильтры: отдел, категория, поставщик. Это " +
        "заказанное, а не оплаченное.";

    public override JsonElement InputSchema { get; } = Schema(FromArg, ToArg, ("department", new { type = "string", description = "Отдел-инициатор: название или код." }), CategoryArg, SupplierArg, LimitArg(10));

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        var ctx = await ContextAsync(context, ct);
        var (from, to) = Period(args, 0);
        var category = await CategoryAsync(ctx, args, ct);
        var supplier = await SupplierAsync(suppliers, ctx, args, ct);
        Guid? department = null;
        if (args.Str("department") is { } deptQuery)
        {
            var departments = await db.Departments.AsNoTracking().Select(d => new { d.Id, d.Code, d.Name }).ToListAsync(ct);
            department = (departments.FirstOrDefault(d => string.Equals(d.Code, deptQuery, StringComparison.OrdinalIgnoreCase))
                          ?? WorkFieldText.Find(departments, deptQuery, d => d.Name, "department", "Отделы"))!.Id;
        }

        var limit = Limit(args, 10);
        var e = await insight.ExpensesAsync(ctx, ProcurementFilter.Of(from, to, department, category?.Id, null, supplier?.Id), ct);
        object Amount(ProcurementAmount a) => new { Name = a.Label, Amount = R(a.Amount), Lines = a.Count, SharePercent = e.Total > 0 ? R(a.Amount / e.Total * 100, 1) : (decimal?)null };
        return Data(new
        {
            Period = Range(e.From, e.To),
            Total = R(e.Total),
            e.Orders,
            e.Lines,
            Previous = new { Period = Range(e.PreviousFrom, e.PreviousTo), Total = R(e.PreviousTotal), ChangePercent = e.PreviousTotal > 0 ? R((e.Total - e.PreviousTotal) / e.PreviousTotal * 100, 1) : (decimal?)null },
            ByDepartment = e.ByDepartment.TakeTop(args, limit, "byDepartment").Select(Amount),
            ByCategory = e.ByCategory.TakeTop(args, limit, "byCategory").Select(Amount),
            BySupplier = e.BySupplier.TakeTop(args, limit, "bySupplier").Select(Amount),
            ByMonth = e.ByMonth.Select(m => new { Month = m.Label, Amount = R(m.Amount), Lines = m.Count }),
            Note = "Суммы — позиции заказов с НДС без скидки и доставки (как в «Обзоре» и отчёте «Расходы»). Оплаты появятся с модулем «Финансы».",
        }, new DataSource("OneBase → Снабжение → Отчёты → Расходы", Range(e.From, e.To), $"/procurement/reports?kind=spend&from={e.From:yyyy-MM-dd}&to={e.To:yyyy-MM-dd}"));
    }
}

/// <summary>10. KPI сотрудников снабжения.</summary>
internal sealed class GetProcurementEmployeeKpiTool(ProcurementRequestService requests, IUserPermissions permissions, ProcurementInsightService insight)
    : ProcurementTool(requests, permissions)
{
    public override string Name => "get_procurement_employee_kpi";
    public override string Title => "Снабжение: KPI сотрудников";

    public override string Description =>
        "KPI сотрудников снабжения: открытые и просроченные задачи, активные и просроченные заказы, поставлено за период, дни от утверждения " +
        "заявки до заказа и от заказа до поставки, нагрузка; по отделу — потребности без заказа, заказы в работе, просрочки. Период — для " +
        "«поставлено» и сроков, по умолчанию текущий месяц.";

    public override JsonElement InputSchema { get; } = Schema(FromArg, ToArg, ("employee", new { type = "string", description = "Сотрудник снабжения: ФИО или его часть." }));

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        var ctx = await ContextAsync(context, ct);
        var (from, to) = Period(args, 0);
        var board = await insight.StaffAsync(ctx, ProcurementFilter.Of(from, to), ct);
        var staff = board.Staff.ToList();
        if (args.Str("employee") is { } name)
        {
            staff = [WorkFieldText.Find(staff, name, s => s.Name, "employee", "Сотрудники снабжения")!];
        }

        return Data(new
        {
            Period = Range(from, to),
            Department = new
            {
                ApprovedNotOrderedRequests = board.WaitingOrder,
                board.OrdersInWork,
                board.OverdueOrders,
                OverdueSupplyTasks = board.OverdueTasks,
                AvgDaysApprovalToOrder = board.AvgToOrderDays,
                AvgDaysOrderToDelivery = board.AvgDeliveryDays,
            },
            Staff = staff.TakeTop(args, 30, "staff").Select(s => new
            {
                s.Name,
                s.OpenTasks,
                s.OverdueTasks,
                s.ActiveOrders,
                s.OverdueOrders,
                DeliveredInPeriod = s.Delivered,
                AvgDaysApprovalToOrder = s.AvgToOrderDays,
                AvgDaysOrderToDelivery = s.AvgDeliveryDays,
                s.Load,
            }),
            Note = "Задачи и активные заказы — на сегодня; «поставлено» и средние сроки — за период. Пустой срок — в периоде не было таких заказов.",
        }, new DataSource("OneBase → Снабжение → Обзор → Сотрудники", Range(from, to), "/procurement"));
    }
}

/// <summary>11. Позиции склада снабжения ниже минимального остатка — и уже ли они в закупке.</summary>
internal sealed class GetLowStockItemsTool(ProcurementRequestService requests, IUserPermissions permissions, ProcurementStockService stock, IAppDbContext db)
    : ProcurementTool(requests, permissions)
{
    public override string Name => "get_low_stock_items";
    public override string Title => "Снабжение: ниже минимального остатка";
    public override IReadOnlyList<string>? AlternativePermissions => [Permissions.ProcurementManage, Permissions.ProcurementWarehouse];

    public override string Description =>
        "Позиции склада снабжения ниже минимального остатка: доступно, минимум, сколько не хватает, резерв, последнее движение и открытые " +
        "заявки на эту позицию (уже в закупке или нет); сколько позиций пусто. Склады продаж и дилеров — не здесь.";

    public override JsonElement InputSchema { get; } = Schema(CategoryArg, LimitArg(20));

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        var ctx = await ContextAsync(context, ct);
        var category = await CategoryAsync(ctx, args, ct);
        var limit = Limit(args, 20);
        var summary = await stock.SummaryAsync(ctx, ct);
        var page = await stock.StockAsync(ctx, new ProcurementStockSearch(null, category?.Id, "low", null, false, 1, Math.Min(limit, 200)), ct);
        args.Count("items", page.Total, page.Items.Count);
        var ids = page.Items.Select(i => (Guid?)i.ItemId).ToList();
        var open = ProcurementRules.Open;
        var purchasing = await db.ProcurementRequestItems.AsNoTracking()
            .Where(i => ids.Contains(i.ItemId) && open.Contains(i.Request!.Status) && i.Status != ProcurementItemStatus.Cancelled && i.Quantity - i.FromStock > i.ReceivedQty)
            .Select(i => new { i.ItemId, i.Request!.Number })
            .ToListAsync(ct);
        return Data(new
        {
            BelowMinimum = summary.Low,
            Empty = summary.Empty,
            Total = page.Total,
            Items = page.Items.Select(i => new
            {
                i.Sku,
                i.Name,
                Category = i.CategoryName,
                Unit = i.UnitCode,
                i.Available,
                i.MinQty,
                ShortToMinimum = i.MinQty - i.Available,
                i.Reserved,
                LastMovement = Day(i.LastMovementAt),
                InPurchase = purchasing.Where(p => p.ItemId == i.ItemId).Select(p => p.Number).Distinct().Take(5).ToList(),
            }),
            Note = "InPurchase пуст — открытой заявки на эту позицию нет: её стоит заказать.",
        }, new DataSource("OneBase → Снабжение → Склад → ниже минимума", $"на {FieldClock.Today:dd.MM.yyyy}", "/procurement/stock?filter=low"));
    }
}

/// <summary>12. История закупочных цен: рост, переплата, повторные закупки и возможные причины.</summary>
internal sealed class GetPurchasePriceHistoryTool(ProcurementRequestService requests, IUserPermissions permissions, ProcurementInsightService insight, ProcurementSupplierService suppliers)
    : ProcurementTool(requests, permissions)
{
    private static readonly IReadOnlyDictionary<string, string> Sorts = new Dictionary<string, string>
    {
        ["amount"] = "amount", ["change"] = "change", ["extra_cost"] = "extra_cost", ["overpay"] = "overpay", ["purchases"] = "purchases",
    };

    private static readonly IReadOnlyDictionary<string, string> Directions = new Dictionary<string, string> { ["rise"] = "rise", ["fall"] = "fall", ["any"] = "any" };

    public override string Name => "get_purchase_price_history";
    public override string Title => "Снабжение: история закупочных цен";

    public override string Description =>
        "Закупочные цены по позициям за период (по умолчанию 90 дней; цена с НДС): покупки, количество, сумма, база сравнения (покупка до " +
        "периода, иначе первая в периоде), первая, последняя, мин., макс. и средняя цена, изменение к базе в %, стоимость сверх базы, переплата " +
        "против лучшей цены периода, поставщики, лучший поставщик, смена поставщика, срочные покупки, покупки без сравнения предложений, " +
        "повторные закупки (3+), возможные причины. Фильтры: категория, поставщик, поиск, минимальное изменение в %.";

    public override string? UsageHint =>
        "Рост или падение закупочных цен, переплаты, повторные закупки — get_purchase_price_history (рост больше 10% — min_change_percent = 10).";

    public override JsonElement InputSchema { get; } = Schema(
        FromArg,
        ToArg,
        CategoryArg,
        SupplierArg,
        SearchArg,
        ("min_change_percent", new { type = "integer", description = "Только позиции с изменением цены к базе не меньше стольких процентов." }),
        ("direction", new { type = "string", description = "Направление изменения (по умолчанию rise — рост).", @enum = Directions.Keys.ToArray() }),
        ("sort", new { type = "string", description = "Сортировка (по умолчанию amount — сумма).", @enum = Sorts.Keys.ToArray() }),
        LimitArg(15));

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        var ctx = await ContextAsync(context, ct);
        var (from, to) = Period(args, 90);
        var category = await CategoryAsync(ctx, args, ct);
        var supplier = await SupplierAsync(suppliers, ctx, args, ct);
        var minChange = args.Int("min_change_percent", 0, 1000);
        var direction = Choice(args, "direction", Directions) ?? "rise";
        var limit = Limit(args, 15);
        var report = await insight.PricesAsync(ctx, new ProcurementPriceQuery(from, to, category?.Id, supplier?.Id, args.Str("search")), ct);
        IEnumerable<ProcurementPriceItem> items = report.Items;
        if (minChange is { } min)
        {
            items = items.Where(i => i.ChangePercent is { } c && direction switch
            {
                "fall" => -c >= min,
                "any" => Math.Abs(c) >= min,
                _ => c >= min,
            });
        }

        var matched = (Choice(args, "sort", Sorts) switch
        {
            "change" => direction == "fall" ? items.OrderBy(i => i.ChangePercent ?? 0) : items.OrderByDescending(i => Math.Abs(i.ChangePercent ?? 0)),
            "extra_cost" => items.OrderByDescending(i => i.ExtraCost),
            "overpay" => items.OrderByDescending(i => i.Overpay),
            "purchases" => items.OrderByDescending(i => i.Purchases),
            _ => items.OrderByDescending(i => i.Amount),
        }).ToList();
        var points = args.Report || args.All ? 12 : 6;
        return Data(new
        {
            Period = Range(from, to),
            Category = category?.Name,
            Supplier = supplier?.Name,
            Lines = report.Lines,
            Amount = R(report.Amount),
            ItemsInPeriod = report.Items.Count,
            Matched = matched.Count,
            MatchedAmount = R(matched.Sum(i => i.Amount)),
            MatchedExtraCost = R(matched.Sum(i => i.ExtraCost)),
            MatchedOverpay = R(matched.Sum(i => i.Overpay)),
            RepeatPurchaseItems = report.Items.Count(i => i.Purchases >= ProcurementInsightService.RepeatPurchases),
            WithoutBaseline = matched.Count(i => i.BaselinePrice is null),
            Items = matched.TakeTop(args, limit, "items").Select(i => new
            {
                i.Name,
                Unit = i.UnitCode,
                Category = i.CategoryName,
                i.Purchases,
                i.Quantity,
                Amount = R(i.Amount),
                Baseline = i.BaselinePrice is null ? null : new { Price = R(i.BaselinePrice, 2), Date = Day(i.BaselineDate), BeforePeriod = i.BaselineBeforePeriod, Supplier = i.BaselineSupplier },
                FirstPrice = R(i.FirstPrice, 2),
                LastPrice = R(i.LastPrice, 2),
                MinPrice = R(i.MinPrice, 2),
                MaxPrice = R(i.MaxPrice, 2),
                AvgPrice = R(i.AvgPrice, 2),
                i.ChangePercent,
                ExtraCost = R(i.ExtraCost),
                Overpay = R(i.Overpay),
                i.CheapestSupplier,
                SuppliersCount = i.Suppliers.Count,
                Suppliers = i.Suppliers.Take(5),
                Repeat = i.Purchases >= ProcurementInsightService.RepeatPurchases,
                PossibleReasons = Reasons(i),
                LastPurchases = i.Points.TakeLast(points).Select(p => new { Date = Day(p.Date), Order = p.OrderNumber, Supplier = p.SupplierName, p.Quantity, Price = R(p.Price, 2), p.Urgent, p.FromOffer }),
            }),
            Method = "Цена — за единицу с НДС. ChangePercent — последняя цена к базе; ExtraCost — Σ (цена − база) × количество по покупкам после базы " +
                     "(минус — экономия); Overpay — Σ (цена − лучшая цена периода) × количество. Позиция — карточка склада, без неё — название и единица.",
        }, new DataSource("OneBase → Снабжение → Отчёты → Позиции закупок", Range(from, to), $"/procurement/reports?kind=lines&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}"));
    }

    /// <summary>Возможные причины изменения цены — только из данных (предположения, а не выводы).</summary>
    internal static List<string> Reasons(ProcurementPriceItem i)
    {
        var reasons = new List<string>();
        if (i.SupplierChanged)
        {
            reasons.Add($"сменился поставщик: база — {i.BaselineSupplier}, последняя покупка — {i.Points[^1].SupplierName}");
        }

        if (i.UrgentPurchases > 0)
        {
            reasons.Add($"срочные или высокоприоритетные заявки: {i.UrgentPurchases} из {i.Purchases} покупок");
        }

        if (i.OfferPurchases < i.Purchases)
        {
            reasons.Add($"без сравнения коммерческих предложений: {i.Purchases - i.OfferPurchases} из {i.Purchases} покупок");
        }

        if (i.Suppliers.Count > 1 && i.Overpay > 0)
        {
            reasons.Add($"у поставщиков разные цены: лучшая — у {i.CheapestSupplier}");
        }

        if (i.Purchases >= ProcurementInsightService.RepeatPurchases)
        {
            reasons.Add($"повторные закупки: {i.Purchases} раз за период — возможно, выгоднее заказывать реже и больше");
        }

        return reasons;
    }
}
