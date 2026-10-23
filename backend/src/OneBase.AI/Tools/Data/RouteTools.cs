using System.Text.Json;
using OneBase.AI.Consultant;
using OneBase.AI.Knowledge;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;

namespace OneBase.AI.Tools.Data;

/// <summary>
/// Отрезок дней месяца и область (республика, направление РМ, регион, ТП) для инструментов «по дням» и «по маршруту»: те же строки
/// продаж и визиты, что на страницах «Продаж» (SalesAnalytics.RouteSplit).
/// </summary>
internal abstract class RouteToolBase(SalesDataLoader loader, IAppDbContext db) : SalesTool(loader)
{
    protected static readonly (string, object) AgentsArg =
        ("agents", new { type = "boolean", description = "Строки каждого ТП и у республики и направления (у региона и ТП — всегда)." });

    protected sealed record RouteQuery(SalesAnalytics Analytics, RouteSplitView View, long? AgentId, string? AgentName, UnitRow? Region, UnitRow? Direction)
    {
        public PeriodInfo Period => Analytics.Period;
        public bool Detailed => AgentId is not null || Region is not null;
    }

    /// <summary>
    /// date — один день; from/to — отрезок; без дат — месяц (year/month или текущий) по отчётный день. Отрезок — в одном месяце: месяц
    /// берётся по датам, если year/month не заданы.
    /// </summary>
    protected async Task<RouteQuery> ResolveAsync(ToolArgs args, CancellationToken ct)
    {
        var date = DrillLookup.Day(args, "date");
        var fromArg = date ?? DrillLookup.Day(args, "from");
        var toArg = date ?? DrillLookup.Day(args, "to");
        var (year, _) = args.Period();
        var anchor = fromArg ?? toArg;
        var analytics = await LoadAsync(anchor is { } a && year is null ? new ToolArgs(JsonSerializer.SerializeToElement(new { year = a.Year, month = a.Month })) : args, ct);
        var p = analytics.Period;
        var from = fromArg ?? new DateOnly(p.Year, p.Month, 1);
        var to = toArg ?? p.DataThrough;
        if (from > to)
        {
            throw new ToolArgumentException("Аргумент from должен быть не позже to.");
        }

        if (from.Year != p.Year || from.Month != p.Month || to.Year != p.Year || to.Month != p.Month)
        {
            throw new ToolArgumentException($"Отрезок — в пределах одного месяца ({MonthName(p.Month)} {p.Year}): для нескольких месяцев вызовите по каждому.");
        }

        var republic = analytics.CachedRepublic(null, null);
        long? agentId = null;
        string? agentName = null;
        if (args.Str("agent") is { } agentQuery)
        {
            (agentId, agentName) = await DrillLookup.AgentAsync(db, analytics, agentQuery, ct);
        }

        var region = agentId is null ? FindRegion(republic, args.Str("region")) : null;
        var direction = agentId is null && region is null ? FindDirection(republic, args.Str("direction")) : null;
        var view = analytics.RouteSplit(from, to, region is null ? null : Guid.Parse(region.Id), direction is null ? null : Guid.Parse(direction.Id), agentId,
            args.Bool("agents") ?? false);
        return new RouteQuery(analytics, view, agentId, agentName, region, direction);
    }

    protected static DataSource SourceOf(RouteQuery q, string what)
    {
        var p = q.Period;
        var range = q.View.From == q.View.To ? DrillLookup.Date(q.View.From) : $"{DrillLookup.Date(q.View.From)} — {DrillLookup.Date(q.View.To)}";
        return q.AgentId is { } id
            ? new DataSource($"OneBase → Продажи → {q.AgentName} → {what}", range, $"/sales/agents/{id}?{Query(p.Year, p.Month)}")
            : q.Region is { } region
                ? new DataSource($"OneBase → Продажи → {region.Name} → {what}", range, $"/sales/regions/{region.Id}?{Query(p.Year, p.Month)}")
                : q.Direction is { } direction
                    ? new DataSource($"OneBase → Продажи → {direction.Name} → {what}", range, $"/sales/directions/{direction.Id}?{Query(p.Year, p.Month)}")
                    : new DataSource($"OneBase → Продажи → Республика → {what}", range, $"/sales/republic?{Query(p.Year, p.Month)}");
    }

    protected static object Part(RouteSales s) => new { s.Orders, RevenueSum = R(s.Revenue), Kg = R(s.Kg, 1), s.Akb };
}

/// <summary>
/// Визиты и маршрут: по республике, направлению (РМ), региону или ТП, за день, отрезок дней или месяц — визиты по маршруту и вне его,
/// визиты с заказом, заказы и выручка, кг и АКБ с визитов по маршруту, вне маршрута и без визита (правила календаря визитов).
/// </summary>
internal sealed class GetVisitsTool(SalesDataLoader loader, IAppDbContext db) : RouteToolBase(loader, db)
{
    public override string Name => "get_visits";
    public override string Title => "Визиты и маршрут";
    public override string Source => KnowledgeSources.SalesVisits;

    public override string UsageHint =>
        "«По маршруту / вне маршрута» — визиты, визиты с заказом, заказы и их сумма, выручка, кг, АКБ — по республике, направлению (РМ), региону " +
        "со всеми ТП или одному ТП, за день, неделю или месяц — get_visits. Точки и время каждого визита одного ТП — get_agent_visits.";

    public override string Description =>
        "Маршрут и визиты за день, отрезок дней или месяц — по республике (регионы), направлению РМ (регионы), региону (каждый ТП) или одному ТП. " +
        "У каждой строки: визиты — план (в маршруте), выполнено по маршруту, вне маршрута, не посещено, % выполнения плана; визиты с заказом — " +
        "по маршруту и вне маршрута; продажи — заказы, выручка (сум), кг и АКБ (точки с чистой покупкой) отдельно с визитов по маршруту, с визитов " +
        "вне маршрута и без визита, возвраты и итог (= факт «Продаж» за эти дни), доля выручки с маршрута, страйк (заказы ÷ выполненные визиты). " +
        "agents=true — строки ТП и у республики и направления. Отрезок — в пределах одного месяца; без дат — месяц по отчётный день.";

    public override JsonElement InputSchema { get; } = Schema(YearArg, MonthArg, RegionArg, DirectionArg, DrillLookup.AgentArg,
        DrillLookup.DateArg, DrillLookup.FromArg, DrillLookup.ToArg, AgentsArg);

    protected override async Task<ToolResult> RunAsync(ToolArgs args, CancellationToken ct)
    {
        var q = await ResolveAsync(args, ct);
        var view = q.View;
        var cap = args.Cap(80);
        return Data(new
        {
            Period = PeriodOf(q.Period),
            From = DrillLookup.Date(view.From),
            To = DrillLookup.Date(view.To),
            Scope = view.Total.Name,
            Total = Row(view.Total),
            Regions = q.Detailed ? null : view.Regions.Select(r => Row(r, cap)).ToList(),
            Agents = q.Detailed ? view.Regions.SelectMany(r => r.Agents).Take(cap).Select(r => Row(r)).ToList() : null,
            AgentsTotal = q.Detailed ? view.Regions.Sum(r => r.Agents.Count) : (int?)null,
            Note = "Визит «по маршруту» — визит из плана (маршрута) Linko. Заказ «по маршруту» — в день его ввода в этой точке был выполненный визит " +
                   "по плану, «вне маршрута» — только визит без плана, «без визита» — визита не было (как календарь визитов в OneBase). Заказы, выручка " +
                   "и кг — заказы, принятые в эти дни (те же, что дали факт «Продаж»); Total — факт целиком, с возвратами (возвраты к маршруту не " +
                   "относятся). АКБ части — точки с чистой покупкой в ней: точка, купившая и по маршруту, и вне его, есть в обеих, поэтому АКБ " +
                   "частей в сумме может быть больше итога. Визит «с заказом» — у этого ТП в этой точке в этот день есть заказ. Суммы — в сумах.",
        }, SourceOf(q, "визиты"));
    }

    private static object Row(RouteSplitRow r, int agentsCap = 0) => new
    {
        r.Name,
        r.Region,
        Visits = new
        {
            Planned = r.Plan,
            DoneByRoute = r.DoneInPlan,
            DoneOffRoute = r.DoneOffPlan,
            DoneAll = r.DoneInPlan + r.DoneOffPlan,
            r.NotVisited,
            PlanDonePct = Pct(r.PlanShare),
        },
        VisitsWithOrder = new { ByRoute = r.VisitsWithOrderInPlan, OffRoute = r.VisitsWithOrderOffPlan },
        ByRoute = Part(r.ByRoute),
        OffRoute = Part(r.OffRoute),
        NoVisit = Part(r.NoVisit),
        Returns = new { RevenueSum = R(r.ReturnsRevenue), Kg = R(r.ReturnsKg, 1) },
        Total = Part(r.Total),
        ByRouteRevenuePct = Pct(r.ByRouteRevenueShare),
        StrikePct = Pct(SalesMath.Ratio(r.Total.Orders, r.DoneInPlan + r.DoneOffPlan)),
        Agents = agentsCap > 0 && r.Agents.Count > 0 ? r.Agents.Take(agentsCap).Select(a => Row(a)).ToList() : null,
        AgentsTotal = agentsCap > 0 && r.Agents.Count > agentsCap ? r.Agents.Count : (int?)null,
    };
}

/// <summary>Продажи за любой отрезок дней месяца (сегодня, вчера, неделя, «с 1 по 15»): итог, каждый день, регионы или ТП.</summary>
internal sealed class GetSalesDaysTool(SalesDataLoader loader, IAppDbContext db) : RouteToolBase(loader, db)
{
    public override string Name => "get_sales_days";
    public override string Title => "Продажи по дням";
    public override string Source => KnowledgeSources.SalesSecondary;

    public override string UsageHint =>
        "Продажи за сегодня, вчера, конкретный день, неделю или «с N по M число» (а не за весь месяц) — get_sales_days: итог, каждый день, регионы или ТП.";

    public override string Description =>
        "Вторичные продажи за день или отрезок дней одного месяца — по республике, направлению РМ, региону или ТП: факт за отрезок (кг, выручка в " +
        "сумах, заказы, АКБ — точки с чистой покупкой за отрезок, возвраты), каждый день отрезка (кг, выручка, заказы, АКБ дня, выполненные визиты) " +
        "и строки регионов (у республики и направления) или ТП (у региона; agents=true — ТП и шире). Правила — как в «Продажах»: заказы − возвраты " +
        "по дате приёмки, без филиала «Завод»; сегодняшний день — на время последней синхронизации Linko. Без дат — месяц по отчётный день.";

    public override JsonElement InputSchema { get; } = Schema(YearArg, MonthArg, RegionArg, DirectionArg, DrillLookup.AgentArg,
        DrillLookup.DateArg, DrillLookup.FromArg, DrillLookup.ToArg, AgentsArg);

    protected override async Task<ToolResult> RunAsync(ToolArgs args, CancellationToken ct)
    {
        var q = await ResolveAsync(args, ct);
        var view = q.View;
        var cap = args.Cap(80);
        object Row(RouteSplitRow r) => new
        {
            r.Name,
            r.Region,
            FactKg = R(r.Total.Kg, 1),
            RevenueSum = R(r.Total.Revenue),
            r.Total.Orders,
            r.Total.Akb,
            ReturnsKg = R(r.ReturnsKg, 1),
            VisitsDone = r.DoneInPlan + r.DoneOffPlan,
        };

        return Data(new
        {
            Period = PeriodOf(q.Period),
            From = DrillLookup.Date(view.From),
            To = DrillLookup.Date(view.To),
            Scope = view.Total.Name,
            Total = Row(view.Total),
            Days = view.Days.Select(d => new
            {
                Date = DrillLookup.Date(d.Date),
                FactKg = R(d.Kg, 1),
                RevenueSum = R(d.Revenue),
                d.Orders,
                d.Akb,
                d.VisitsDone,
            }),
            Regions = q.Detailed ? null : view.Regions.Select(Row).ToList(),
            Agents = q.Detailed || (args.Bool("agents") ?? false) ? view.Regions.SelectMany(r => r.Agents).OrderByDescending(a => a.Total.Revenue).Take(cap).Select(Row).ToList() : null,
            Note = "Факт — заказы − возвраты по дате приёмки, без «Завода» (как в «Продажах»). АКБ отрезка — точки с чистой покупкой за весь отрезок " +
                   "(не сумма АКБ дней). Сегодняшний день — неполный, на время последней синхронизации Linko. Суммы — в сумах.",
        }, SourceOf(q, "по дням"));
    }
}
