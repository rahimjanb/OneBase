using System.Text.Json;
using OneBase.AI.Consultant;
using OneBase.AI.Knowledge;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;
using OneBase.Domain.Sales;

namespace OneBase.AI.Tools.Data;

/// <summary>Общее для инструментов вторички: проверка периода и поиск региона.</summary>
public abstract class SalesTool(SalesDataLoader loader) : DataTool
{
    protected SalesDataLoader Loader => loader;

    /// <summary>Отчёт за месяц; месяц без данных — ошибка со списком доступных месяцев.</summary>
    protected async Task<SalesAnalytics> LoadAsync(ToolArgs args, CancellationToken ct)
    {
        var (year, month) = args.Period();
        if (year is not null && month is not null)
        {
            var months = await loader.MonthsAsync(ct);
            if (!months.Any(m => m.Year == year && m.Month == month))
            {
                var available = string.Join(", ", months.Take(12).Select(m => $"{MonthName(m.Month)} {m.Year}"));
                throw new ToolArgumentException($"Данных о продажах за {MonthName(month.Value)} {year} в OneBase нет. Есть: {available}.");
            }
        }

        return await loader.LoadAsync(year, month, ct);
    }

    /// <summary>Регион по id или названию; null — республика. Неизвестный регион — ошибка со списком регионов.</summary>
    protected static UnitRow? FindRegion(GroupView republic, string? region)
    {
        if (region is null)
        {
            return null;
        }

        var match = republic.Regions.FirstOrDefault(r => string.Equals(r.Id, region, StringComparison.OrdinalIgnoreCase))
            ?? republic.Regions.FirstOrDefault(r => string.Equals(r.Name, region, StringComparison.OrdinalIgnoreCase))
            ?? republic.Regions.FirstOrDefault(r => r.Name.Contains(region, StringComparison.OrdinalIgnoreCase) || region.Contains(r.Name, StringComparison.OrdinalIgnoreCase));
        return match ?? throw new ToolArgumentException(
            $"Региона «{region}» нет. Регионы: {string.Join(", ", republic.Regions.Select(r => r.Name))}.");
    }

    protected static DataSource RepublicSource(PeriodInfo p, string title = "OneBase → Продажи → Республика") =>
        new(title, PeriodText(p), $"/sales/republic?{Query(p.Year, p.Month)}");

    protected static DataSource RegionSource(PeriodInfo p, UnitRow region) =>
        new($"OneBase → Продажи → {region.Name}", PeriodText(p), $"/sales/regions/{region.Id}?{Query(p.Year, p.Month)}");

    protected static object Kpi(KpiTiles k) => new
    {
        FactKg = R(k.FactKg),
        PlanKg = R(k.PlanKg),
        k.PlanSource, // rop — план РОП регионов, linko — плана РОП на месяц нет, план = сумма планов ТП из Linko
        ExecutionPct = Pct(k.Execution),
        ForecastKg = R(k.ForecastKg),
        ForecastExecutionPct = Pct(k.ForecastExecution),
        RevenueSum = R(k.Revenue),
        k.Akb,
        StrikePct = Pct(k.Conversion.Value),
        StrikeTargetPct = Pct(k.Conversion.Target),
        AkbPerAgent = R(k.AkbPerAgent.Value, 1),
        AkbPerAgentTarget = R(k.AkbPerAgent.Target, 1),
        RevenuePerOutletSum = R(k.RevenuePerOutlet.Value),
        k.VisitsDone,
        k.VisitsWithoutOrder,
        k.VisitsOutsideTeam,
        SalesReps = k.ActiveAgents,
        RevenuePlan = k.RevenuePlan is { } rp
            ? new { PlanSum = R(rp.Plan), FactSum = R(rp.Fact), ExecutionPct = Pct(rp.Execution), ForecastSum = R(rp.Forecast), rp.Agents }
            : null,
    };

    protected static object Unit(UnitRow u) => new
    {
        u.Id,
        u.Name,
        Direction = u.Subtitle, // у региона — его РМ или канал
        PlanKg = R(u.PlanKg),
        FactKg = R(u.FactKg),
        ExecutionPct = Pct(u.Execution),
        ForecastKg = R(u.ForecastKg),
        ForecastExecutionPct = Pct(u.ForecastExecution),
        RevenueSum = R(u.Revenue),
        u.Akb,
        StrikePct = Pct(u.Strike),
        u.VisitsWithoutOrder,
        SalesReps = u.Agents,
        CriticalAgents = u.Flags.Critical,
        RiskAgents = u.Flags.Risk,
    };
}

internal sealed class GetSalesTool(SalesDataLoader loader) : SalesTool(loader)
{
    public override string Name => "get_sales";
    public override string Title => "Итоги продаж за месяц";
    public override string Source => KnowledgeSources.SalesSecondary;

    public override string Description =>
        "Итоги вторичных продаж за месяц по республике или региону: факт и план в кг, выполнение, прогноз на конец месяца, выручка в сумах, " +
        "АКБ (активные торговые точки), страйк, визиты, число ТП; сравнение с теми же днями прошлого месяца; экспорт и опт (филиал «Завод») отдельно.";

    public override JsonElement InputSchema { get; } = Schema(YearArg, MonthArg, RegionArg);

    protected override async Task<ToolResult> RunAsync(ToolArgs args, CancellationToken ct)
    {
        var analytics = await LoadAsync(args, ct);
        var republic = analytics.CachedRepublic(null, null);
        var region = FindRegion(republic, args.Str("region"));
        var p = republic.Period;

        if (region is not null)
        {
            var view = analytics.CachedRegion(Guid.Parse(region.Id), null, null, "kg", null);
            var same = view.SameDays.FirstOrDefault();
            return Data(new
            {
                Period = PeriodOf(p),
                Region = view.Name,
                Direction = view.DirectionName,
                view.Supervisor,
                view.Dealer,
                Kpi = Kpi(view.Kpi),
                SameDaysPreviousMonth = SameDays(view.SameDays),
                Vacancies = view.Team.Count(t => t.IsVacancy),
                Note = "Вторичка: заказы − возвраты по дате приёмки, без филиала «Завод».",
            }, RegionSource(p, region));
        }

        var overview = analytics.CachedOverview();
        return Data(new
        {
            Period = PeriodOf(p),
            Scope = "Республика",
            Kpi = Kpi(overview.Kpi),
            CriticalAgents = overview.Flags.Critical,
            RiskAgents = overview.Flags.Risk,
            overview.Vacancies,
            SameDaysPreviousMonth = SameDays(republic.SameDays),
            ExportAndWholesale = overview.Excluded is { } x
                ? new { FactKg = R(x.FactKg), RevenueSum = R(x.Revenue), x.Orders, x.Akb, ForecastKg = R(x.ForecastKg), PrevMonthKg = R(x.PrevMonthKg), VsPrevMonthPct = Pct(x.VsPrevMonth) }
                : null,
            UnassignedKg = R(republic.Unassigned.Kg),
            Note = "Вторичка: заказы − возвраты по дате приёмки, без филиала «Завод» (экспорт и опт — отдельно).",
        }, RepublicSource(p));
    }

    /// <summary>Сумма строк «те же дни прошлого месяца».</summary>
    private static object? SameDays(IReadOnlyList<SameDaysRow> rows)
    {
        if (rows.Count == 0)
        {
            return null;
        }

        var before = rows.Sum(r => r.KgBefore);
        var now = rows.Sum(r => r.KgNow);
        var sumBefore = rows.Sum(r => r.SumBefore);
        var sumNow = rows.Sum(r => r.SumNow);
        return new
        {
            KgBefore = R(before),
            KgNow = R(now),
            KgChangePct = before == 0 ? null : Pct((now - before) / before),
            SumBefore = R(sumBefore),
            SumNow = R(sumNow),
            SumChangePct = sumBefore == 0 ? null : Pct((sumNow - sumBefore) / sumBefore),
        };
    }
}

internal sealed class GetSalesByBranchTool(SalesDataLoader loader) : SalesTool(loader)
{
    public override string Name => "get_sales_by_branch";
    public override string Title => "Продажи по регионам";
    public override string Source => KnowledgeSources.SalesSecondary;

    public override string Description =>
        "Продажи по регионам (филиалам) за месяц: план, факт, выполнение и прогноз в кг, выручка, АКБ, страйк, число ТП, проблемные агенты. " +
        "Отсортировано по прогнозу выполнения плана — сначала отстающие.";

    public override JsonElement InputSchema { get; } = Schema(YearArg, MonthArg);

    protected override async Task<ToolResult> RunAsync(ToolArgs args, CancellationToken ct)
    {
        var analytics = await LoadAsync(args, ct);
        var republic = analytics.CachedRepublic(null, null);
        var regions = republic.Regions
            .OrderBy(r => r.ForecastExecution ?? r.Execution ?? decimal.MaxValue)
            .Select(Unit)
            .ToList();
        return Data(new
        {
            Period = PeriodOf(republic.Period),
            Republic = Kpi(republic.Kpi),
            Regions = regions,
            Note = "ExecutionPct — факт к плану на сегодня; ForecastExecutionPct — ожидаемое выполнение к концу месяца при текущем темпе.",
        }, RepublicSource(republic.Period, "OneBase → Продажи → Республика → Регионы"));
    }
}

internal sealed class GetSalesByEmployeeTool(SalesDataLoader loader) : SalesTool(loader)
{
    public override string Name => "get_sales_by_employee";
    public override string Title => "Продажи по торговым представителям";
    public override string Source => KnowledgeSources.SalesTeam;

    public override string Description =>
        "Торговые представители (ТП): план и факт в кг, выполнение, прогноз, выручка, визиты, заказы, страйк, категории, замечания. " +
        "С регионом — вся команда региона; без региона — лучшие и худшие ТП республики по прогнозу выполнения.";

    public override JsonElement InputSchema { get; } = Schema(YearArg, MonthArg, RegionArg,
        ("limit", new { type = "integer", description = "Сколько лучших и худших ТП вернуть без региона (по умолчанию 10, до 30)." }));

    protected override async Task<ToolResult> RunAsync(ToolArgs args, CancellationToken ct)
    {
        var analytics = await LoadAsync(args, ct);
        var republic = analytics.CachedRepublic(null, null);
        var region = FindRegion(republic, args.Str("region"));
        var p = republic.Period;

        if (region is not null)
        {
            var view = analytics.CachedRegion(Guid.Parse(region.Id), null, null, "kg", null);
            var team = view.Team.Where(t => t.IsSalesRep && !t.IsVacancy).OrderBy(t => t.ForecastExecution ?? t.Execution ?? decimal.MaxValue).Select(Row).ToList();
            return Data(new
            {
                Period = PeriodOf(p),
                Region = view.Name,
                Team = team,
                Vacancies = view.Team.Count(t => t.IsVacancy),
                OtherUsersWithSales = view.Team.Where(t => !t.IsSalesRep).Select(t => new { t.Name, t.Job, FactKg = R(t.FactKg) }),
            }, RegionSource(p, region));
        }

        var limit = args.Int("limit", 1, 30) ?? 10;
        var everyone = republic.Regions
            .SelectMany(r => analytics.CachedRegion(Guid.Parse(r.Id), null, null, "kg", null).Team
                .Where(t => t.IsSalesRep && !t.IsVacancy)
                .Select(t => (Region: r.Name, Row: t)))
            .ToList();
        var withPlan = everyone.Where(x => x.Row.PlanKg is > 0).OrderBy(x => x.Row.ForecastExecution ?? x.Row.Execution ?? 0).ToList();
        return Data(new
        {
            Period = PeriodOf(p),
            SalesReps = everyone.Count,
            WithPlan = withPlan.Count,
            Worst = withPlan.Take(limit).Select(x => Row(x.Row, x.Region)),
            Best = withPlan.AsEnumerable().Reverse().Take(limit).Select(x => Row(x.Row, x.Region)),
            WithoutPlanTopByFact = everyone.Where(x => x.Row.PlanKg is not > 0).OrderByDescending(x => x.Row.FactKg).Take(5).Select(x => Row(x.Row, x.Region)),
        }, RepublicSource(p, "OneBase → Продажи → Регионы → Команда ТП"));
    }

    private static object Row(TeamRow t) => Row(t, null);

    private static object Row(TeamRow t, string? region) => new
    {
        t.AgentId,
        t.Name,
        Region = region,
        PlanKg = R(t.PlanKg),
        FactKg = R(t.FactKg),
        ExecutionPct = Pct(t.Execution),
        ForecastExecutionPct = Pct(t.ForecastExecution),
        RevenueSum = R(t.Revenue),
        t.Visits,
        t.Orders,
        StrikePct = Pct(t.Strike),
        SumPerVisit = R(t.SumPerVisit),
        t.Categories,
        Flags = t.Flags.Select(f => f.Label),
    };
}

internal sealed class GetProblemAgentsTool(SalesDataLoader loader) : SalesTool(loader)
{
    public override string Name => "get_problem_agents";
    public override string Title => "Проблемные агенты";
    public override string Source => KnowledgeSources.SalesVisits;

    public override string Description =>
        "ТП с замечаниями за месяц (относительно медианы своего региона): низкий страйк, визиты без продаж, малый чек, узкий ассортимент, падение темпа. " +
        "У каждого — регион, страйк, визиты, выручка и объяснение замечаний.";

    public override JsonElement InputSchema { get; } = Schema(YearArg, MonthArg);

    protected override async Task<ToolResult> RunAsync(ToolArgs args, CancellationToken ct)
    {
        var analytics = await LoadAsync(args, ct);
        var view = analytics.CachedProblems(null, null, false);
        return Data(new
        {
            Period = PeriodOf(view.Period),
            Total = view.Agents.Count,
            view.Vacancies,
            // Сначала критичные; объяснение — только у критичных замечаний, чтобы ответ оставался компактным.
            Agents = view.Agents.Take(25).Select(a => new
            {
                a.AgentId,
                a.Name,
                Region = a.RegionName,
                ConversionPct = Pct(a.Conversion),
                a.Visits,
                RevenueSum = R(a.Revenue),
                Flags = a.Flags.Select(f => new
                {
                    f.Label,
                    Severity = f.Severity.ToString(),
                    Explanation = f.Severity == FlagSeverity.Critical ? f.Explanation : null,
                }),
            }),
            ByRegion = view.Agents.GroupBy(a => a.RegionName ?? "без региона").Select(g => new { Region = g.Key, Agents = g.Count() }).OrderByDescending(x => x.Agents),
        }, new DataSource("OneBase → Продажи → Проблемные агенты", PeriodText(view.Period), $"/sales/problems?{Query(view.Period.Year, view.Period.Month)}"));
    }
}

internal sealed class GetProductsTool(SalesDataLoader loader) : SalesTool(loader)
{
    public override string Name => "get_products";
    public override string Title => "Товары и категории";
    public override string Source => KnowledgeSources.SalesSecondary;

    public override string Description =>
        "Ассортимент за месяц по республике или региону: все категории (кг, выручка, доля, АКБ, дистрибуция, изменение к прошлому месяцу, «молчащие» и пропавшие SKU), " +
        "лучшие и худшие товары по выручке, пропавшие SKU. Конкретный товар, категорию или бренд по названию — find_products.";

    public override JsonElement InputSchema { get; } = Schema(YearArg, MonthArg, RegionArg);

    protected override async Task<ToolResult> RunAsync(ToolArgs args, CancellationToken ct)
    {
        var analytics = await LoadAsync(args, ct);
        var republic = analytics.CachedRepublic(null, null);
        var region = FindRegion(republic, args.Str("region"));
        var view = analytics.CachedAssortment(null, region is null ? null : Guid.Parse(region.Id));
        var selling = view.Products.Where(x => x.Kg > 0).ToList();
        return Data(new
        {
            Period = PeriodOf(view.Period),
            Scope = view.ScopeName,
            Summary = new
            {
                FactKg = R(view.Summary.FactKg),
                RevenueSum = R(view.Summary.Revenue),
                PrevMonthKg = R(view.Summary.PrevMonthKg),
                view.Summary.SkuSold,
                view.Summary.SkuTotal,
                view.Summary.SkuLost,
                view.Summary.Outlets,
            },
            Categories = view.Categories.Select(c => new
            {
                c.Name,
                FactKg = R(c.FactKg),
                WeightSharePct = Pct(c.WeightShare),
                RevenueSum = R(c.Revenue),
                c.Akb,
                DistributionPct = Pct(c.Distribution),
                ForecastKg = R(c.ForecastKg),
                VsPrevMonthPct = Pct(c.VsPrevMonth),
                c.SkuSold,
                c.SkuTotal,
                SilentSku = c.Silent,
                LostSku = c.Lost,
                LostNames = c.Skus.Where(s => s.Status == SkuStatuses.Lost).Take(5).Select(s => s.Name),
            }),
            TopProducts = selling.OrderByDescending(x => x.Revenue).Take(10).Select(Product),
            WeakestProducts = selling.OrderBy(x => x.Revenue).Take(10).Select(Product),
        }, region is null
            ? new DataSource("OneBase → Продажи → Ассортимент", PeriodText(view.Period), $"/sales/assortment?{Query(view.Period.Year, view.Period.Month)}")
            : new DataSource($"OneBase → Продажи → Ассортимент → {region.Name}", PeriodText(view.Period), $"/sales/assortment?{Query(view.Period.Year, view.Period.Month)}&region={region.Id}"));
    }

    private static object Product(ProductRow x) => new
    {
        x.Name,
        x.Category,
        Kg = R(x.Kg),
        RevenueSum = R(x.Revenue),
        SharePct = Pct(x.Share),
        x.Akb,
        DistributionPct = Pct(x.Distribution),
    };
}

internal sealed class GetCustomersTool(SalesDataLoader loader) : SalesTool(loader)
{
    public override string Name => "get_customers";
    public override string Title => "Клиенты (АКБ)";
    public override string Source => KnowledgeSources.SalesSecondary;

    public override string Description =>
        "Клиенты — торговые точки: АКБ по месяцам года (точки с чистой покупкой > 0), точки, которые покупали в прошлом месяце и не купили в этом " +
        "(«молчат», с выручкой прошлого месяца), новые точки — по республике или по регионам.";

    public override JsonElement InputSchema { get; } = Schema(YearArg, MonthArg, RegionArg);

    protected override async Task<ToolResult> RunAsync(ToolArgs args, CancellationToken ct)
    {
        var analytics = await LoadAsync(args, ct);
        var republic = analytics.CachedRepublic(null, null);
        var region = FindRegion(republic, args.Str("region"));
        var p = republic.Period;
        var akb = republic.AkbMonths;
        var notBought = republic.NotBought;
        if (region is not null)
        {
            var view = analytics.CachedRegion(Guid.Parse(region.Id), null, null, "kg", null);
            (akb, notBought) = (view.AkbMonths, view.NotBought);
        }

        return Data(new
        {
            Period = PeriodOf(p),
            Scope = region?.Name ?? "Республика",
            AkbByMonth = new
            {
                akb.Year,
                Months = akb.Months.Select((m, i) => new { Month = MonthName(m), Akb = akb.Total.ElementAtOrDefault(i) }),
                LastMonthPartial = akb.LastPartial,
            },
            NotBoughtThisMonth = notBought.Select(n => new
            {
                n.Name,
                BaseOutlets = n.Base,
                SilentOutlets = n.Silent,
                SilentSharePct = Pct(n.Share),
                SilentPrevRevenueSum = R(n.SilentPrevRevenue),
                NewOutlets = n.New,
            }),
        }, region is null ? RepublicSource(p) : RegionSource(p, region));
    }
}

internal sealed class GetVisitsTool(SalesDataLoader loader) : SalesTool(loader)
{
    public override string Name => "get_visits";
    public override string Title => "Визиты и страйк";
    public override string Source => KnowledgeSources.SalesVisits;

    public override string Description =>
        "Визиты ТП за месяц по регионам: план визитов, выполненные (в плане и вне), заказы с визитов и их сумма, непосещённые точки, " +
        "страйк (заказы ТП, принятые в месяце ÷ выполненные визиты ТП; бывает больше 100%), визиты без заказа (визиты − заказы), визиты не ТП отдельно.";

    public override JsonElement InputSchema { get; } = Schema(YearArg, MonthArg);

    protected override async Task<ToolResult> RunAsync(ToolArgs args, CancellationToken ct)
    {
        var analytics = await LoadAsync(args, ct);
        var republic = analytics.CachedRepublic(null, null);
        var strike = republic.Regions.ToDictionary(r => r.Name, r => r.Strike);
        return Data(new
        {
            Period = PeriodOf(republic.Period),
            StrikePct = Pct(republic.Kpi.Conversion.Value),
            StrikeTargetPct = Pct(republic.Kpi.Conversion.Target),
            republic.Kpi.VisitsDone,
            republic.Kpi.VisitsWithoutOrder,
            republic.Kpi.VisitsOutsideTeam,
            Regions = republic.VisitCalendar.Select(r => new
            {
                r.Name,
                PlannedVisits = r.Plan,
                r.DoneInPlan,
                r.DoneOffPlan,
                r.NotVisited,
                PlanDonePct = Pct(r.PlanShare),
                Orders = r.OrdersTotal,
                OrdersSum = R(r.OrdersTotalSum),
                StrikePct = Pct(strike.GetValueOrDefault(r.Name)),
            }),
        }, RepublicSource(republic.Period, "OneBase → Продажи → Республика → Визиты"));
    }
}

internal sealed class GetSalesKpiTool(SalesDataLoader loader) : SalesTool(loader)
{
    public override string Name => "get_sales_kpi";
    public override string Title => "Планы и KPI продаж";
    public override string Source => KnowledgeSources.SalesPlans;

    public override string Description =>
        "Планы продаж из Linko за месяц и их выполнение: план и факт по весу и выручке — всего и по регионам, число ТП с планом, KPI-показатели.";

    public override JsonElement InputSchema { get; } = Schema(YearArg, MonthArg);

    protected override async Task<ToolResult> RunAsync(ToolArgs args, CancellationToken ct)
    {
        var analytics = await LoadAsync(args, ct);
        var view = analytics.CachedPlans();
        return Data(new
        {
            Period = PeriodOf(view.Period),
            WeightPlanKg = R(view.WeightPlan),
            WeightFactKg = R(view.WeightFact),
            WeightExecutionPct = Pct(view.WeightExecution),
            RevenuePlanSum = R(view.RevenuePlan),
            RevenueFactSum = R(view.RevenueFact),
            view.AgentsWithPlan,
            IndicatorCount = view.Indicators,
            Regions = view.Regions.OrderBy(r => r.WeightExecution ?? decimal.MaxValue).Select(r => new
            {
                r.Name,
                r.Agents,
                WeightPlanKg = R(r.WeightPlan),
                WeightFactKg = R(r.WeightFact),
                WeightExecutionPct = Pct(r.WeightExecution),
                RevenuePlanSum = R(r.RevenuePlan),
                RevenueFactSum = R(r.RevenueFact),
            }),
            Note = "Факт здесь — по расчёту Linko (staff_balance) и может немного отличаться от факта вторички OneBase.",
        }, new DataSource("OneBase → Продажи → Планы", PeriodText(view.Period), $"/sales/plans?{Query(view.Period.Year, view.Period.Month)}"));
    }
}

internal sealed class GetSalesTrendTool(SalesDataLoader loader) : SalesTool(loader)
{
    public override string Name => "get_sales_trend";
    public override string Title => "Динамика продаж по месяцам";
    public override string Source => KnowledgeSources.SalesSecondary;

    public override string Description =>
        "Продажи по месяцам — для сравнения периодов: факт и план в кг, выполнение, выручка, АКБ, страйк за последние N месяцев (по республике или региону). " +
        "Последний месяц может быть неполным — смотри dataThrough.";

    public override JsonElement InputSchema { get; } = Schema(RegionArg,
        ("months", new { type = "integer", description = "Сколько последних месяцев (по умолчанию 6, до 12)." }));

    protected override async Task<ToolResult> RunAsync(ToolArgs args, CancellationToken ct)
    {
        var count = args.Int("months", 1, 12) ?? 6;
        var months = (await Loader.MonthsAsync(ct)).Take(count).Reverse().ToList();
        var regionArg = args.Str("region");
        var rows = new List<object>();
        UnitRow? region = null;
        foreach (var m in months)
        {
            var analytics = await Loader.LoadAsync(m.Year, m.Month, ct);
            var republic = analytics.CachedRepublic(null, null);
            var kpi = republic.Kpi;
            if (regionArg is not null)
            {
                var r = FindRegion(republic, regionArg);
                region ??= r;
                if (r is null || !analytics.HasRegion(Guid.Parse(r.Id)))
                {
                    continue;
                }

                kpi = analytics.CachedRegion(Guid.Parse(r.Id), null, null, "kg", null).Kpi;
            }

            rows.Add(new
            {
                m.Year,
                Month = MonthName(m.Month),
                DataThrough = republic.Period.DataThrough.ToString("yyyy-MM-dd"),
                FactKg = R(kpi.FactKg),
                PlanKg = R(kpi.PlanKg),
                ExecutionPct = Pct(kpi.Execution),
                ForecastKg = R(kpi.ForecastKg),
                RevenueSum = R(kpi.Revenue),
                kpi.Akb,
                StrikePct = Pct(kpi.Conversion.Value),
                SalesReps = kpi.ActiveAgents,
            });
        }

        var last = months.LastOrDefault();
        return Data(new { Scope = region?.Name ?? "Республика", Months = rows },
            new DataSource(region is null ? "OneBase → Продажи → Республика по месяцам" : $"OneBase → Продажи → {region.Name} по месяцам",
                months.Count == 0 ? null : $"{MonthName(months[0].Month)} {months[0].Year} — {MonthName(last!.Month)} {last.Year}",
                "/sales/republic"));
    }
}
