using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;
using OneBase.Domain.Sales;

namespace OneBase.Sales.Tests;

public class SalesAnalyticsTests
{
    private static readonly Guid North = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid South = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Rm1 = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

    private static SaleLine Line(int day, long? agent, long market, long branch, decimal kg, decimal revenue, long? order, int month = 9) =>
        new(new DateOnly(2026, month, day), agent, market, branch, 1, 100, kg, revenue, order);

    /// <summary>Позиция заказа конкретного SKU (агент 1, регион «Север»); выручка = кг × 10.</summary>
    private static SaleLine Sku(long product, long category, long market, decimal kg, int month = 9) =>
        new(new DateOnly(2026, month, 2), 1, market, 101, category, product, kg, kg * 10, product * 1000 + market * 10 + month);

    private static MonthData Data(
        IReadOnlyList<SaleLine>? previous = null,
        IReadOnlyList<PlanRow>? plans = null,
        IReadOnlyList<PlanRow>? revenuePlans = null,
        IReadOnlyList<DirectionInfo>? directions = null,
        IReadOnlyList<SaleLine>? current = null,
        IReadOnlyDictionary<long, string>? categories = null,
        IReadOnlyDictionary<long, ProductInfo>? products = null,
        IReadOnlySet<long>? activeSkus = null,
        IReadOnlyList<MonthlyAkb>? akbHistory = null,
        IReadOnlyDictionary<string, string>? reportCategories = null,
        IReadOnlyList<SaleLine>? visitOrders = null,
        IReadOnlyList<VisitRecord>? visits = null,
        IReadOnlyList<RegionInfo>? regions = null,
        IReadOnlyDictionary<long, Guid>? branchAliases = null,
        IReadOnlyList<CategoryPlanRow>? categoryPlans = null,
        DateOnly? dataThrough = null,
        IReadOnlyDictionary<long, AgentInfo>? agents = null,
        IReadOnlyList<string>? salesRepJobs = null,
        IReadOnlyList<SaleLine>? excludedCurrent = null,
        IReadOnlyList<SaleLine>? excludedPrevious = null,
        string planSource = PlanSources.Linko,
        IReadOnlyList<PlanRow>? yearRegionPlans = null,
        IReadOnlyList<PlanRow>? yearAgentPlans = null,
        IReadOnlyList<PlanRow>? nextRegionPlans = null,
        IReadOnlyList<PlanRow>? nextPlans = null,
        string selectedPlan = PlanSources.Rop,
        IReadOnlyList<string>? availablePlans = null,
        IReadOnlyList<string>? akbHidden = null) => new()
    {
        PlanSource = planSource,
        SelectedPlan = selectedPlan,
        AvailablePlans = availablePlans ?? [],
        AkbChartHiddenCategories = akbHidden ?? [],
        YearAgentPlans = yearAgentPlans ?? [],
        NextRegionPlans = nextRegionPlans ?? [],
        NextPlans = nextPlans ?? [],
        NextMonth = (2026, 10),
        CategoryPlans = categoryPlans ?? [],
        BranchAliases = branchAliases ?? new Dictionary<long, Guid>(),
        AkbHistory = akbHistory ?? [],
        CategoryMap = reportCategories is null ? null : SalesCategories.Build(reportCategories, categories ?? new Dictionary<long, string> { [1] = "Печенье" }),
        VisitOrders = visitOrders,
        ExcludedCurrent = excludedCurrent ?? [],
        ExcludedPrevious = excludedPrevious ?? [],
        SalesRepJobs = salesRepJobs ?? [],
        Year = 2026,
        Month = 9,
        DataThrough = dataThrough ?? new DateOnly(2026, 9, 10),
        Current = current ??
        [
            Line(1, 1, 10, branch: 101, kg: 100, revenue: 1000, order: 1),
            Line(2, 2, 11, branch: 101, kg: 50, revenue: 500, order: 2),
            Line(3, null, 12, branch: 101, kg: 25, revenue: 250, order: 3), // факт без агента
            Line(4, 3, 13, branch: 102, kg: 40, revenue: 400, order: 4),
            Line(5, 3, 13, branch: 102, kg: -10, revenue: -100, order: null), // возврат
            Line(6, 4, 14, branch: 999, kg: 5, revenue: 50, order: 5), // филиал без региона
        ],
        Products = products ?? new Dictionary<long, ProductInfo>(),
        ActiveSkus = activeSkus ?? new HashSet<long>(),
        Previous = previous ?? [],
        Visits = visits ?? [],
        History = [],
        Plans = plans ?? [new PlanRow(North, null, 9, null, 300), new PlanRow(South, null, 9, null, 60)],
        RevenuePlans = revenuePlans ?? [],
        YearRegionPlans = yearRegionPlans ?? [],
        Agents = agents ?? new Dictionary<long, AgentInfo>
        {
            [1] = new(1, "Агент 1", true, North, false, true),
            [2] = new(2, "Агент 2", true, North, false, true),
            [3] = new(3, "Агент 3", true, South, false, true),
        },
        Regions = regions ?? [new RegionInfo(North, 101, "Север", Rm1, null, null), new RegionInfo(South, 102, "Юг", Rm1, null, null)],
        Directions = directions ?? [new DirectionInfo(Rm1, "РМ 1", false, null, null, 1)],
        Markets = new Dictionary<long, MarketInfo>(),
        Categories = categories ?? new Dictionary<long, string> { [1] = "Печенье" },
        MarketAssignments = [],
        Targets = new SalesTargets(0.7m, 1_439_000m, 125m, 4m),
        Thresholds = new FlagThresholds(),
    };

    [Fact]
    public void Sum_of_regions_equals_republic_total()
    {
        var republic = new SalesAnalytics(Data()).Republic(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10));

        Assert.Equal(210m, republic.Kpi.FactKg); // 100 + 50 + 25 + 40 − 10 + 5
        Assert.Equal(republic.Kpi.FactKg, republic.Regions.Sum(r => r.FactKg));
        Assert.Equal(republic.Kpi.Revenue, republic.Regions.Sum(r => r.Revenue));
        Assert.Equal(360m, republic.Kpi.PlanKg);
    }

    [Fact]
    public void Sum_of_agents_plus_unassigned_equals_region_total()
    {
        var region = new SalesAnalytics(Data()).Region(North, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10), "kg", null);

        Assert.Equal(175m, region.Kpi.FactKg);
        Assert.Equal(25m, region.Unassigned.Kg);
        Assert.Equal(region.Kpi.FactKg, region.Team.Sum(t => t.FactKg) + region.Unassigned.Kg);
        Assert.Equal(175m / 300m, region.Kpi.Execution);
    }

    [Fact]
    public void Republic_has_no_region_cards_regions_are_in_the_table()
    {
        var republic = new SalesAnalytics(Data(directions: [])).Republic(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10));

        Assert.Empty(republic.Cards);
        Assert.Contains(republic.Regions, r => r.Id == North.ToString());
        Assert.DoesNotContain(republic.Regions, r => r.Name == "Без направления");
    }

    [Fact]
    public void Republic_shows_direction_cards_when_regions_are_assigned()
    {
        var republic = new SalesAnalytics(Data()).Republic(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10));

        var card = Assert.Single(republic.Cards);
        Assert.Equal(UnitKinds.Direction, card.Kind);
        Assert.Equal("РМ 1", card.Name);
    }

    [Fact]
    public void Akb_by_month_takes_old_months_from_history_and_recent_ones_from_sales()
    {
        var history = new[]
        {
            new MonthlyAkb(2026, 1, ByBranch: false, BranchId: null, IsTotal: true, CategoryId: null, Akb: 40),
            new MonthlyAkb(2026, 1, false, null, false, 1, 30),
            new MonthlyAkb(2026, 1, true, 101, true, null, 25),
            new MonthlyAkb(2026, 1, true, 101, false, 1, 20),
            new MonthlyAkb(2025, 1, false, null, true, null, 99), // другой год — не берётся
        };
        var previous = new[]
        {
            Line(5, 1, 10, branch: 101, kg: 1, revenue: 100, order: 90, month: 8),
            Line(6, 3, 20, branch: 102, kg: 1, revenue: 100, order: 91, month: 8),
        };
        var analytics = new SalesAnalytics(Data(previous, akbHistory: history));

        var republic = analytics.Republic(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10));
        var akb = republic.AkbMonths;
        Assert.Equal(Enumerable.Range(1, 9), akb.Months);
        Assert.Equal(40, akb.Total[0]);
        Assert.Null(akb.Total[1]); // за февраль данных нет
        Assert.Equal(2, akb.Total[7]); // август — из строк продаж
        Assert.Equal(republic.Kpi.Akb, akb.Total[8]); // сентябрь — как плитка АКБ
        Assert.True(akb.LastPartial);
        Assert.Equal(30, Assert.Single(akb.Categories).Values[0]);

        var north = analytics.Region(North, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10), "kg", null).AkbMonths;
        Assert.Equal(25, north.Total[0]);
        Assert.Equal(20, north.Categories.Single().Values[0]);
        Assert.Equal(1, north.Total[7]);
        Assert.Equal(3, north.Total[8]); // ТТ 10, 11, 12
    }

    /// <summary>ТП 1 и 2 — в оргструктуре; 3 — оператор Linko (не ТП), 4 — «Агент» по должности без оргструктуры.</summary>
    private static Dictionary<long, AgentInfo> RepsAndOperator() => new()
    {
        [1] = new(1, "Агент 1", true, North, false, true),
        [2] = new(2, "Агент 2", true, North, false, true),
        [3] = new(3, "Оператор", true, South, false, false, "Филиал Оператор"),
        [4] = new(4, "Агент 4", true, null, false, false, "Агент"),
    };

    [Fact]
    public void Akb_per_agent_divides_by_sales_reps_of_the_month_not_only_by_those_with_sales()
    {
        // 5 — «Агент» с визитами, но без продаж: в численность ТП месяца входит; 6 — вакансия: показатель не занижает.
        var agents = RepsAndOperator();
        agents[5] = new(5, "Агент 5", true, null, false, false, "Агент");
        agents[6] = new(6, "Вакант Агент", true, null, true, false, "Агент");
        var visits = new[]
        {
            new VisitRecord(new DateOnly(2026, 9, 3), 5, 15, VisitStatus.Done, true),
            new VisitRecord(new DateOnly(2026, 9, 3), 6, 16, VisitStatus.Done, true),
        };

        var republic = new SalesAnalytics(Data(agents: agents, salesRepJobs: ["Агент"], visits: visits))
            .Republic(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10));

        Assert.Equal(4, republic.Kpi.ActiveAgents); // 1, 2, 4 и 5; оператор 3 продавал, но не ТП; вакансия 6 не считается
        Assert.Equal(5m / 4m, republic.Kpi.AkbPerAgent.Value); // 5 ТТ с покупкой ÷ 4 ТП месяца
        Assert.Equal(210m, republic.Kpi.FactKg); // продажи оператора остаются в итоге
    }

    [Fact]
    public void Visit_tiles_count_team_visits_and_accepted_orders_other_visits_are_reported_separately()
    {
        // ТП 1: 4 визита и заказ 1; ТП 2: 2 визита и заказ 2; ТП 4 без региона: заказ 5 без визитов;
        // оператор 3 (не ТП, «Юг»): 3 визита и заказ 4.
        static VisitRecord Visit(long agent, long market, int day, VisitStatus status = VisitStatus.Done) =>
            new(new DateOnly(2026, 9, day), agent, market, status, true);
        var visits = new[]
        {
            Visit(1, 10, 1), Visit(1, 10, 2), Visit(1, 17, 3), Visit(1, 18, 4), Visit(1, 19, 5, VisitStatus.Pending),
            Visit(2, 11, 2), Visit(2, 12, 3),
            Visit(3, 13, 4), Visit(3, 13, 5), Visit(3, 13, 6),
        };
        var analytics = new SalesAnalytics(Data(agents: RepsAndOperator(), salesRepJobs: ["Агент"], visits: visits));

        var republic = analytics.Republic(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10));
        Assert.Equal(6, republic.Kpi.VisitsDone); // только ТП
        Assert.Equal(3m / 6m, republic.Kpi.Conversion.Value); // заказы ТП, принятые в месяце (1, 2 и 5) ÷ их визиты
        Assert.Equal(3, republic.Kpi.VisitsWithoutOrder); // визиты − заказы
        Assert.Equal(3, republic.Kpi.VisitsOutsideTeam); // визиты оператора — отдельно, не теряются

        var north = analytics.Region(North, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10), "kg", null);
        Assert.Equal((6, 2m / 6m, 0), (north.Kpi.VisitsDone, north.Kpi.Conversion.Value!.Value, north.Kpi.VisitsOutsideTeam));
        var first = north.Team.Single(t => t.AgentId == 1);
        Assert.Equal((4, 1, 0.25m), (first.Visits, first.Orders, first.Strike!.Value));

        var south = analytics.Region(South, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10), "kg", null);
        Assert.Equal((0, 3), (south.Kpi.VisitsDone, south.Kpi.VisitsOutsideTeam));
        Assert.Null(south.Kpi.Conversion.Value); // визитов ТП нет — страйк «—», а не 0
    }

    [Fact]
    public void Agent_akb_by_month_takes_old_months_from_the_agent_history()
    {
        var history = new[]
        {
            new MonthlyAkb(2026, 1, false, null, true, null, 7, AgentId: 1),
            new MonthlyAkb(2026, 1, false, null, false, 1, 5, AgentId: 1),
            new MonthlyAkb(2026, 1, false, null, true, null, 40), // республика — не этот агент
        };

        var agent = new SalesAnalytics(Data(akbHistory: history)).Agent(1);

        Assert.Equal(1, agent.Akb); // ТТ 10
        Assert.Equal(7, agent.AkbMonths!.Total[0]);
        Assert.Null(agent.AkbMonths.Total[1]); // за февраль данных нет
        Assert.Equal(1, agent.AkbMonths.Total[8]); // сентябрь — из строк продаж
        var category = Assert.Single(agent.AkbMonths.Categories);
        Assert.Equal((5, 1), (category.Values[0], category.Values[8]));
    }

    [Fact]
    public void Region_without_own_plan_uses_sum_of_its_agents_plans()
    {
        // Планы агентов (как из Linko): 1 и 2 — в «Севере», 3 — в «Юге»; у «Юга» есть ручной план региона.
        var plans = new[]
        {
            new PlanRow(null, 1, 9, null, 100),
            new PlanRow(null, 2, 9, null, 50),
            new PlanRow(null, 3, 9, null, 40),
            new PlanRow(South, null, 9, null, 60),
        };

        var republic = new SalesAnalytics(Data(plans: plans)).Republic(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10));

        Assert.Equal(150m, republic.Regions.Single(r => r.Id == North.ToString()).PlanKg);
        Assert.Equal(60m, republic.Regions.Single(r => r.Id == South.ToString()).PlanKg); // ручной план важнее
        Assert.Equal(210m, republic.Kpi.PlanKg);
    }

    [Fact]
    public void Plan_execution_without_region_plan_uses_fact_of_agents_with_plans_only()
    {
        // В «Севере» план только у агента 1 (100 кг, факт 100). Агент 2 (без плана, 50 кг) не должен «выполнять» его план.
        var plans = new[] { new PlanRow(null, 1, 9, null, 100) };

        var region = new SalesAnalytics(Data(plans: plans)).Region(North, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10), "kg", null);

        Assert.Equal(175m, region.Kpi.FactKg); // факт региона — всё, включая агента без плана и факт без агента
        Assert.Equal(100m, region.Kpi.PlanKg);
        Assert.Equal(100m, region.Kpi.PlanFactKg);
        Assert.Equal(1m, region.Kpi.Execution);
        Assert.Equal(1, region.Kpi.PlanAgents);
    }

    [Fact]
    public void Revenue_plan_counts_only_revenue_of_agents_that_have_a_revenue_plan()
    {
        // План по выручке есть только у агента 1 (2000); его выручка — 1000. Выручка остальных не должна «выполнять» его план.
        var republic = new SalesAnalytics(Data(revenuePlans: [new PlanRow(null, 1, 9, null, 2000)]))
            .Republic(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10));

        var tile = republic.Kpi.RevenuePlan!;
        Assert.Equal(2000m, tile.Plan);
        Assert.Equal(1000m, tile.Fact);
        Assert.Equal(0.5m, tile.Execution);
        Assert.Equal(1, tile.Agents);

        // В «Юге» агентов с планом по выручке нет — и плана нет.
        var south = new SalesAnalytics(Data(revenuePlans: [new PlanRow(null, 1, 9, null, 2000)]))
            .Region(South, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10), "kg", null);
        Assert.Null(south.Kpi.RevenuePlan);
    }

    [Fact]
    public void Category_cards_count_sku_silent_lost_share_and_distribution()
    {
        // «Помадка 0,5 кг» — подтип «Помадки»: одна карточка. SKU 103 в ассортименте, но не продаётся; 102 продавался в августе.
        var data = Data(
            current: [Sku(100, 1, market: 10, kg: 60), Sku(101, 2, market: 11, kg: 40), Sku(200, 3, market: 12, kg: 100)],
            previous: [Sku(102, 2, market: 12, kg: 30, month: 8), Sku(200, 3, market: 12, kg: 50, month: 8)],
            categories: new Dictionary<long, string> { [1] = "Помадка", [2] = "Помадка 0,5 кг", [3] = "Печенье" },
            reportCategories: new Dictionary<string, string> { ["Помадка"] = "1,2", ["Печенье"] = "3" },
            products: new Dictionary<long, ProductInfo>
            {
                [100] = new(100, "Помадка А", "A", 1),
                [101] = new(101, "Помадка Б", "B", 2),
                [102] = new(102, "Помадка В", "C", 2),
                [103] = new(103, "Помадка Г", "D", 1),
                [200] = new(200, "Печенье А", "E", 3),
            },
            activeSkus: new HashSet<long> { 100, 101, 102, 103, 200 });

        var cards = new SalesAnalytics(data).Republic(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10)).CategoryCards;

        Assert.Equal(2, cards.Count);
        var fondant = Assert.Single(cards, c => c.Name == "Помадка");
        Assert.Equal(2, fondant.SkuSold);
        Assert.Equal(4, fondant.SkuTotal);
        Assert.Equal(2, fondant.Silent);
        Assert.Equal(1, fondant.Lost);
        Assert.Equal(100m, fondant.FactKg);
        Assert.Equal(0.5m, fondant.WeightShare); // 100 из 200 кг
        Assert.Equal(2, fondant.Akb);
        Assert.Equal(2m / 3m, fondant.Distribution); // 2 из 3 ТТ с покупкой
        Assert.Equal(300m, fondant.ForecastKg); // 100 кг за 10 дней × 30
        Assert.Equal(9m, fondant.VsPrevMonth); // прогноз 300 к 30 кг августа
        Assert.Equal(SkuStatuses.Lost, fondant.Skus.Single(s => s.ProductId == 102).Status);
        Assert.Equal(SkuStatuses.Silent, fondant.Skus.Single(s => s.ProductId == 103).Status);
        Assert.Equal(SkuStatuses.Selling, fondant.Skus[0].Status);

        var cookies = Assert.Single(cards, c => c.Name == "Печенье");
        Assert.Equal((1, 1, 0, 0), (cookies.SkuSold, cookies.SkuTotal, cookies.Silent, cookies.Lost));
        Assert.Equal(5m, cookies.VsPrevMonth); // прогноз 300 к 50 кг августа
    }

    [Fact]
    public void Product_types_outside_report_categories_are_not_lost_but_shown_in_diagnostics()
    {
        var data = Data(
            current: [Sku(100, 1, market: 10, kg: 60), Sku(900, 16, market: 11, kg: 2)],
            categories: new Dictionary<long, string> { [1] = "Бамбук", [16] = "бонус" },
            reportCategories: new Dictionary<string, string> { ["Бамбук"] = "1" });

        var republic = new SalesAnalytics(data).Republic(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10));

        Assert.Equal("Бамбук", Assert.Single(republic.CategoryCards).Name);
        var bonus = Assert.Single(republic.Quality.Uncategorized);
        Assert.Equal(("бонус", 2m, 1), (bonus.Name, bonus.Kg, bonus.Orders));
        Assert.Equal(62m, republic.Kpi.FactKg); // в итоге они есть — просто не входят в восемь категорий
    }

    [Fact]
    public void Agent_category_plan_comes_from_linko_indicators_and_shows_categories_without_plan()
    {
        var types = new Dictionary<long, string> { [1] = "Помадка", [2] = "Помадка 0,5 кг", [3] = "Шоколад", [4] = "Бамбук" };
        var report = new Dictionary<string, string> { ["Помадка"] = "1,2", ["Шоколад"] = "3", ["Бамбук"] = "4" };
        var map = SalesCategories.Build(report, types);
        var pomadka = map.GroupByName("Помадка")!.Value;
        var chocolate = map.GroupByName("Шоколад")!.Value;
        var data = Data(
            current: [Sku(100, 1, market: 10, kg: 60), Sku(101, 2, market: 11, kg: 40), Sku(102, 3, market: 12, kg: 30), Sku(103, 4, market: 13, kg: 20)],
            categories: types,
            reportCategories: report,
            // Показатель Linko «Сентябрь Могуль + Шоколад …» — план на две категории сразу.
            categoryPlans: [new CategoryPlanRow(1, 2026, 9, [pomadka, chocolate], 200)]);

        var rows = new SalesAnalytics(data).Agent(1).CategoryPlan;

        Assert.Equal(2, rows.Count);
        Assert.Equal(("Помадка + Шоколад", 200m, 130m), (rows[0].Name, rows[0].PlanKg!.Value, rows[0].FactKg)); // 60 + 40 + 30
        Assert.Equal(("Бамбук", null, 20m), (rows[1].Name, rows[1].PlanKg, rows[1].FactKg)); // продаётся без плана
    }

    [Fact]
    public void Conversion_counts_accepted_orders_while_visit_with_order_is_matched_by_creation_date()
    {
        // Агент был в ТТ 10-го и ввёл заказ; магазин принял товар 11-го — продажа 11-го, а визит «с заказом» — 10-го.
        var accepted = new SaleLine(new DateOnly(2026, 9, 11), 1, 10, 101, 1, 100, 50, 500, 7);
        var created = accepted with { Date = new DateOnly(2026, 9, 10) };
        var visits = new[] { new VisitRecord(new DateOnly(2026, 9, 10), 1, 10, VisitStatus.Done, true) };

        var byCreation = new SalesAnalytics(Data(current: [accepted], visitOrders: [created], visits: visits)).Agent(1);
        var byAcceptance = new SalesAnalytics(Data(current: [accepted], visits: visits)).Agent(1);

        Assert.Equal((1, 1, 1m), (byCreation.Orders, byCreation.VisitsWithOrder, byCreation.Conversion.Value!.Value));
        // Сшивка по дате приёмки теряет «визит с заказом», но конверсия — заказы месяца ÷ визиты — от неё не зависит.
        Assert.Equal((1, 0, 1m), (byAcceptance.Orders, byAcceptance.VisitsWithOrder, byAcceptance.Conversion.Value!.Value));
        Assert.Equal(50m, byCreation.FactKg);
    }

    [Fact]
    public void Forecast_is_shown_only_for_the_running_month()
    {
        var previous = new[] { Line(5, 1, 10, branch: 101, kg: 80, revenue: 800, order: 90, month: 8) };
        var revenuePlans = new[] { new PlanRow(null, 1, 9, null, 2000) };
        var factory = new[] { Line(3, 9, 50, branch: 4, kg: 300, revenue: 3000, order: 99) };
        var factoryBefore = new[] { Line(5, 9, 50, branch: 4, kg: 600, revenue: 6000, order: 98, month: 8) };

        var running = new SalesAnalytics(Data(previous, revenuePlans: revenuePlans, excludedCurrent: factory, excludedPrevious: factoryBefore))
            .Republic(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10));
        Assert.False(running.Period.Closed);
        Assert.Equal(630m, running.Kpi.ForecastKg); // 210 кг за 10 дней × 30
        Assert.NotNull(running.CategoryCards.Single().VsPrevMonth);
        Assert.Equal(900m, running.Excluded!.ForecastKg);
        Assert.Equal(0.5m, running.Excluded.VsPrevMonth); // прогноз 900 кг к 600 кг прошлого месяца

        // Сентябрь закрыт (данные по 30-е): прогноза нет нигде, выполнение и факт остаются.
        var closed = new SalesAnalytics(Data(previous, revenuePlans: revenuePlans, excludedCurrent: factory, excludedPrevious: factoryBefore,
            dataThrough: new DateOnly(2026, 9, 30)));
        var republic = closed.Republic(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));
        Assert.True(republic.Period.Closed);
        Assert.Null(republic.Kpi.ForecastKg);
        Assert.Null(republic.Kpi.ForecastExecution);
        Assert.Null(republic.Kpi.PlanForecastKg);
        Assert.Equal(205m / 360m, republic.Kpi.Execution); // факт регионов с планом ÷ план
        Assert.Null(republic.Kpi.RevenuePlan!.Forecast);
        Assert.Null(republic.Kpi.RevenuePlan.ForecastExecution);
        Assert.All(republic.Regions, r => Assert.True(r.ForecastKg is null && r.ForecastExecution is null));
        var card = Assert.Single(republic.CategoryCards);
        Assert.True(card.ForecastKg is null && card.ForecastRevenue is null && card.VsPrevMonth is null);
        // «Экспорт и опт» — по тому же правилу, что карточки категорий: ни прогноза, ни сравнения с прошлым месяцем по нему.
        Assert.True(republic.Excluded!.ForecastKg is null && republic.Excluded.VsPrevMonth is null);
        Assert.Equal((300m, 600m), (republic.Excluded.FactKg, republic.Excluded.PrevMonthKg));
        Assert.All(closed.Region(North, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), "kg", null).Team,
            t => Assert.True(t.ForecastKg is null && t.ForecastExecution is null));
        Assert.Null(closed.CachedOverview().Republic.ForecastKg);
    }

    [Fact]
    public void Agent_category_count_includes_only_report_categories()
    {
        // «бонус» (тип 16) — не категория отчёта: ассортимент ТП он не расширяет.
        var data = Data(
            current: [Sku(100, 1, market: 10, kg: 60), Sku(900, 16, market: 10, kg: 0)],
            categories: new Dictionary<long, string> { [1] = "Бамбук", [16] = "бонус" },
            reportCategories: new Dictionary<string, string> { ["Бамбук"] = "1" });

        var analytics = new SalesAnalytics(data);

        Assert.Equal(1, analytics.Agent(1).Categories);
        Assert.Equal(1, analytics.Region(North, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10), "kg", null).Team.Single(t => t.AgentId == 1).Categories);
    }

    [Fact]
    public void Sku_whose_returns_exceed_sales_is_not_selling()
    {
        var data = Data(
            current:
            [
                Sku(100, 1, market: 10, kg: 60),
                Sku(101, 1, market: 11, kg: 5),
                new SaleLine(new DateOnly(2026, 9, 3), 1, 11, 101, 1, 101, -8, -80, null), // возврат больше продажи
            ],
            categories: new Dictionary<long, string> { [1] = "Бамбук" },
            products: new Dictionary<long, ProductInfo> { [100] = new(100, "А", null, 1), [101] = new(101, "Б", null, 1) },
            activeSkus: new HashSet<long> { 100, 101 });

        var card = Assert.Single(new SalesAnalytics(data).Republic(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10)).CategoryCards);

        Assert.Equal((1, 2, 1), (card.SkuSold, card.SkuTotal, card.Silent));
        Assert.Equal(SkuStatuses.Silent, card.Skus.Single(s => s.ProductId == 101).Status);
    }

    [Fact]
    public void Cached_views_are_computed_once_per_parameters()
    {
        var analytics = new SalesAnalytics(Data());

        Assert.Same(analytics.CachedRepublic(null, null), analytics.CachedRepublic(1, 10)); // по умолчанию — с 1-го по 10-е
        Assert.NotSame(analytics.CachedRepublic(null, null), analytics.CachedRepublic(2, 10));
        Assert.Same(analytics.CachedAgent(1), analytics.CachedAgent(1));
        Assert.Equal((new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10)), analytics.VisitRange(null, null));
    }

    [Fact]
    public void Returns_are_subtracted_in_the_region_of_the_return()
    {
        var region = new SalesAnalytics(Data()).Region(South, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10), "kg", null);

        Assert.Equal(30m, region.Kpi.FactKg);
        Assert.Equal(300m, region.Kpi.Revenue);
    }

    [Fact]
    public void Same_days_comparison_cuts_previous_month_to_the_same_day_numbers()
    {
        // Отработано 10 дней: из августа берём 1–10, а продажа 20 августа не участвует.
        var previous = new[]
        {
            Line(5, 1, 10, branch: 101, kg: 80, revenue: 800, order: 90, month: 8),
            Line(20, 1, 10, branch: 101, kg: 500, revenue: 5000, order: 91, month: 8),
        };

        var republic = new SalesAnalytics(Data(previous)).Republic(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10));
        var north = republic.SameDays.Single(r => r.Id == North.ToString());

        Assert.Equal(80m, north.KgBefore);
        Assert.Equal(175m, north.KgNow);
    }

    [Fact]
    public void Not_bought_yet_is_previous_month_outlets_minus_current_ones()
    {
        var previous = new[]
        {
            Line(5, 1, 10, branch: 101, kg: 1, revenue: 100, order: 90, month: 8), // купила и в сентябре
            Line(6, 1, 20, branch: 101, kg: 1, revenue: 300, order: 91, month: 8), // молчит
        };

        var republic = new SalesAnalytics(Data(previous)).Republic(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10));
        var north = republic.NotBought.Single(r => r.Id == North.ToString());

        Assert.Equal(2, north.Base);
        Assert.Equal(1, north.Silent);
        Assert.Equal(300m, north.SilentPrevRevenue);
        Assert.Equal(2, north.New); // ТТ 11 и 12 — новые
        Assert.Equal(north.Base + republic.NotBought.Where(r => r.Id != North.ToString()).Sum(r => r.Base), republic.NotBoughtTotal!.Base);
    }

    [Fact]
    public void Region_without_purchases_this_month_has_no_data_instead_of_a_share()
    {
        // В августе покупали и Север, и Юг; в сентябре в Юге нет ни одной покупки — это дыра в данных, а не замолчавшая база.
        var previous = new[]
        {
            Line(5, 1, 10, branch: 101, kg: 1, revenue: 100, order: 90, month: 8), // Север: купила и в сентябре
            Line(6, 1, 20, branch: 101, kg: 1, revenue: 300, order: 91, month: 8), // Север: молчит
            Line(6, 3, 30, branch: 102, kg: 2, revenue: 200, order: 92, month: 8), // Юг
        };
        var analytics = new SalesAnalytics(Data(previous, current: [Line(1, 1, 10, branch: 101, kg: 100, revenue: 1000, order: 1)]));
        var republic = analytics.Republic(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10));

        var south = republic.NotBought.Single(r => r.Id == South.ToString());
        Assert.True(south.NoData);
        Assert.Null(south.Share);
        Assert.Equal((1, 1), (south.Base, south.Silent));
        var north = republic.NotBought.Single(r => r.Id == North.ToString());
        Assert.False(north.NoData);
        Assert.Equal(0.5m, north.Share);

        // Итог — с сервера: суммы строк; доли у итога нет, раз у Юга нет данных.
        var total = republic.NotBoughtTotal!;
        Assert.Equal((3, 2, 500m), (total.Base, total.Silent, total.SilentPrevRevenue));
        Assert.True(total.NoData);
        Assert.Null(total.Share);

        var region = analytics.Region(South, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10), "kg", null);
        var agent = Assert.Single(region.NotBought);
        Assert.True(agent.NoData && agent.Share is null);
        Assert.Equal((1, 1), (region.NotBoughtTotal!.Base, region.NotBoughtTotal.Silent));
    }

    [Fact]
    public void Old_branch_is_counted_in_the_current_region_with_the_same_name()
    {
        // «Юг (эски) 2» — прежний филиал Юга: точки перенесли в новый филиал, продажи прошлого остались на старом.
        var old = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var (regions, aliases) = OldBranches.Merge(
            [new RegionInfo(North, 101, "Север", Rm1, null, null), new RegionInfo(South, 102, "Юг", Rm1, null, null), new RegionInfo(old, 103, "Юг (эски) 2", Rm1, null, null)],
            new SalesOptions().OldBranchSuffix);

        Assert.Equal(["Север", "Юг"], regions.Select(r => r.Name));
        Assert.Equal(South, aliases[103]);

        var data = Data(
            current: [Line(1, 3, 20, branch: 103, kg: 30, revenue: 300, order: 9), Line(2, 3, 21, branch: 102, kg: 10, revenue: 100, order: 10)],
            regions: regions,
            branchAliases: aliases);
        var republic = new SalesAnalytics(data).Republic(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10));

        var south = Assert.Single(republic.Regions, r => r.Id == South.ToString());
        Assert.Equal((40m, 2), (south.FactKg, south.Akb)); // продажи старого филиала — в Юге
        Assert.DoesNotContain(republic.Regions, r => r.Name.Contains("эски"));
        Assert.Empty(OldBranches.Merge(regions, "").Aliases); // пустая настройка — без объединения
    }

    /// <summary>Позиция заказа категории 1 («Бамбук»): агент, филиал, артикул, магазин; выручка = кг × 10.</summary>
    private static SaleLine At(long agent, long branch, long product, long market, decimal kg, int month = 9) =>
        new(new DateOnly(2026, month, 2), agent, market, branch, 1, product, kg, kg * 10, product * 1000 + market * 10 + month);

    /// <summary>
    /// Север: ТП 1 продаёт 100 в ТТ 10, ТП 2 — 101 в ТТ 11. Юг: ТП 3 продаёт 101 в ТТ 20, а в августе продавал 102.
    /// 103 в ассортименте, но не продаётся нигде.
    /// </summary>
    private static MonthData CatalogData() => Data(
        current: [At(1, 101, 100, 10, 60), At(2, 101, 101, 11, 10), At(3, 102, 101, 20, 20)],
        previous: [At(3, 102, 102, 20, 5, month: 8)],
        categories: new Dictionary<long, string> { [1] = "Бамбук" },
        reportCategories: new Dictionary<string, string> { ["Бамбук"] = "1" },
        products: new Dictionary<long, ProductInfo>
        {
            [100] = new(100, "Бамбук А", "A", 1),
            [101] = new(101, "Бамбук Б", "B", 1),
            [102] = new(102, "Бамбук В", "C", 1),
            [103] = new(103, "Бамбук Г", "D", 1),
        },
        activeSkus: new HashSet<long> { 100, 101, 102, 103 });

    [Fact]
    public void Category_page_in_a_region_marks_sku_sold_elsewhere_as_not_carried()
    {
        var analytics = new SalesAnalytics(CatalogData());
        var id = Assert.Single(analytics.Republic(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10)).CategoryCards).Id;

        var south = analytics.Category(id, new AssortmentScope(null, South, null, false))!;
        string StatusOf(CategoryView view, long product) => view.Card!.Skus.Single(s => s.ProductId == product).Status;

        Assert.Equal(("Юг", "Бамбук"), (south.ScopeName, south.Name));
        Assert.Equal(SkuStatuses.Selling, StatusOf(south, 101));
        Assert.Equal(SkuStatuses.Lost, StatusOf(south, 102));
        Assert.Equal(SkuStatuses.Elsewhere, StatusOf(south, 100)); // здесь ноль, а на Севере идёт
        Assert.Equal(SkuStatuses.Silent, StatusOf(south, 103)); // не продаётся нигде
        Assert.Empty(south.Regions); // у одного региона сводки по регионам нет

        var republic = analytics.Category(id, new AssortmentScope(null, null, null, false))!;
        Assert.Equal(SkuStatuses.Silent, StatusOf(republic, 103)); // у республики «не возят» не бывает
        var rows = republic.Regions.ToDictionary(r => r.Name);
        Assert.Equal((2, 0, 0), (rows["Север"].SkuSelling, rows["Север"].SkuNotCarried, rows["Север"].SkuLost));
        Assert.Equal((1, 1, 1), (rows["Юг"].SkuSelling, rows["Юг"].SkuNotCarried, rows["Юг"].SkuLost)); // база — 100 и 101, что идут по республике
        Assert.Equal("Север", republic.Regions[0].Name); // по убыванию веса

        Assert.Null(analytics.Category(id, new AssortmentScope(null, Guid.NewGuid(), null, false))); // неизвестный регион
    }

    [Fact]
    public void Assortment_tiles_sum_categories_and_count_outlets()
    {
        var summary = new SalesAnalytics(CatalogData()).Assortment(null, null).Summary;

        Assert.Equal((90m, 900m, 5m), (summary.FactKg, summary.Revenue, summary.PrevMonthKg)); // 60 + 10 + 20 кг; август — 5 кг
        Assert.Equal((2, 4, 1, 3), (summary.SkuSold, summary.SkuTotal, summary.SkuLost, summary.Outlets)); // 102 пропал; ТТ 10, 11, 20
    }

    [Fact]
    public void Product_page_shows_where_the_product_sells_by_region_agent_and_store()
    {
        var analytics = new SalesAnalytics(CatalogData());

        var republic = analytics.Product(101, new AssortmentScope(null, null, null, false))!;
        Assert.Equal((ProductBreakdowns.Regions, 30m, 300m, 10m), (republic.Breakdown, republic.FactKg, republic.Revenue, republic.PricePerKg!.Value));
        Assert.Equal((2, 3), (republic.Tt, republic.Outlets)); // ТТ 11 и 20 из 10, 11, 20
        var north = republic.Rows.Single(r => r.Name == "Север");
        Assert.Equal((SkuStatuses.Selling, 10m, 1, 2, 0.5m), (north.Status, north.Kg, north.Tt, north.Outlets, north.Distribution!.Value));

        var inSouth = analytics.Product(100, new AssortmentScope(null, South, null, false))!;
        Assert.Equal((ProductBreakdowns.Agents, SkuStatuses.Elsewhere), (inSouth.Breakdown, inSouth.Status));
        var agent = Assert.Single(inSouth.Rows);
        Assert.Equal(("3", SkuStatuses.Elsewhere, 0, 1), (agent.Id, agent.Status, agent.Tt, agent.Outlets));

        var ofAgent = analytics.Product(101, new AssortmentScope(null, null, 1, false))!;
        Assert.Equal((ProductBreakdowns.Stores, SkuStatuses.Elsewhere), (ofAgent.Breakdown, ofAgent.Status));
        var store = Assert.Single(ofAgent.Rows);
        Assert.Equal(("10", SkuStatuses.Silent), (store.Id, store.Status)); // ТТ 10 у ТП 1 этот артикул не брала

        Assert.Null(analytics.Product(999, new AssortmentScope(null, null, null, false))); // нет ни в справочнике, ни в продажах
    }
    // ================= План РОП / «Завод» и оргструктура =================

    private static readonly DateOnly Sep1 = new(2026, 9, 1);

    [Fact]
    public void Region_plan_execution_is_whole_region_fact_over_its_plan_and_linko_plans_are_not_mixed_in()
    {
        // План РОП (регион × категория): у Севера 200 + 100 кг, у Юга плана РОП нет. Планы ТП из Linko: ТП 1 — 100 кг, ТП 3 (Юг) — 40 кг.
        var plans = new[]
        {
            new PlanRow(North, null, 9, 4, 200), new PlanRow(North, null, 9, 10, 100),
            new PlanRow(null, 1, 9, null, 100), new PlanRow(null, 3, 9, null, 40),
        };
        var analytics = new SalesAnalytics(Data(plans: plans, planSource: PlanSources.Rop));
        var republic = analytics.Republic(Sep1, new DateOnly(2026, 9, 10));

        // Весь факт Севера — и ТП без плана, и факт без агента: 100 + 50 + 25 кг.
        var north = republic.Regions.Single(r => r.Id == North.ToString());
        Assert.Equal((300m, 175m, 175m / 300m), (north.PlanKg!.Value, north.PlanFactKg!.Value, north.Execution!.Value));
        Assert.Equal(525m / 300m, north.ForecastExecution); // прогноз: 175 кг за 10 дней × 30 ÷ план
        var south = republic.Regions.Single(r => r.Id == South.ToString());
        Assert.True(south.PlanKg is null && south.Execution is null); // у Юга РОП нет — план ТП 3 из Linko не подставляется

        // Республика: весь факт — и Юга без плана РОП (40 − 10 кг), и филиала без региона (5 кг) — ÷ Σ планов регионов.
        Assert.Equal((PlanSources.Rop, 300m, 210m, 0), (republic.Kpi.PlanSource, republic.Kpi.PlanKg!.Value, republic.Kpi.PlanFactKg!.Value, republic.Kpi.PlanAgents));
        Assert.Equal(republic.Kpi.FactKg, republic.Kpi.PlanFactKg);
        Assert.Equal(210m / 300m, republic.Kpi.Execution);
        Assert.Equal(630m / 300m, republic.Kpi.ForecastExecution);

        // ТП — по своему плану из Linko и при плане РОП: в карточке и в команде региона.
        var agent = analytics.Agent(1);
        Assert.Equal((100m, 1m), (agent.PlanKg!.Value, agent.Execution!.Value));
        Assert.Equal(100m, analytics.Region(North, Sep1, new DateOnly(2026, 9, 10), "kg", null).Team.Single(t => t.AgentId == 1).PlanKg);

        // «Завод» — тот же расчёт, в ответе — его источник; без планов регионов — сумма планов ТП из Linko.
        Assert.Equal(PlanSources.Factory, new SalesAnalytics(Data(plans: plans, planSource: PlanSources.Factory)).CachedOverview().Kpi.PlanSource);

        // Период ответа — по какому плану посчитан и какие планы регионов на месяц заведены (по ним переключатель плана).
        var period = new SalesAnalytics(Data(plans: plans, planSource: PlanSources.Factory, selectedPlan: PlanSources.Factory,
            availablePlans: [PlanSources.Rop, PlanSources.Factory])).CachedOverview().Period;
        Assert.Equal(PlanSources.Factory, period.Plan);
        Assert.Equal([PlanSources.Rop, PlanSources.Factory], period.AvailablePlans);
        var linko = new SalesAnalytics(Data(plans: [new PlanRow(null, 1, 9, null, 100), new PlanRow(null, 3, 9, null, 40)])).Republic(Sep1, new DateOnly(2026, 9, 10));
        Assert.Equal((PlanSources.Linko, 140m, 2), (linko.Kpi.PlanSource, linko.Kpi.PlanKg!.Value, linko.Kpi.PlanAgents));
    }

    [Fact]
    public void Region_without_own_region_plan_adds_its_whole_fact_but_no_plan_to_the_direction_and_the_republic()
    {
        // Июнь 2026, план РОП: у Олмалика строки РОП нет, а продажи есть. Выполнение = факт ÷ план (DOC-rules §3):
        // республика — весь факт 325 995,93 кг ÷ 387 900 кг = 84,04%; без Олмалика в числителе было бы 83,60%.
        var olmalik = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var analytics = new SalesAnalytics(Data(
            current:
            [
                Line(1, 1, 10, branch: 101, kg: 250_000.50m, revenue: 1000, order: 1),
                Line(2, 3, 20, branch: 102, kg: 74_290.85m, revenue: 1000, order: 2),
                Line(3, 5, 30, branch: 103, kg: 1_704.58m, revenue: 1000, order: 3),
            ],
            regions:
            [
                new RegionInfo(North, 101, "Север", Rm1, null, null),
                new RegionInfo(South, 102, "Юг", null, null, null),
                new RegionInfo(olmalik, 103, "Олмалик", Rm1, null, null),
            ],
            planSource: PlanSources.Rop,
            plans: [new PlanRow(North, null, 9, 4, 300_000), new PlanRow(South, null, 9, 4, 87_900), new PlanRow(null, 5, 9, null, 1_000)],
            dataThrough: new DateOnly(2026, 9, 30)));

        var republic = analytics.Republic(Sep1, new DateOnly(2026, 9, 30));
        Assert.Equal((387_900m, 325_995.93m), (republic.Kpi.PlanKg!.Value, republic.Kpi.PlanFactKg!.Value));
        Assert.Equal(325_995.93m / 387_900m, republic.Kpi.Execution);
        Assert.Equal(0.8404m, Math.Round(republic.Kpi.Execution!.Value, 4));

        // РМ 1 (Север и Олмалик): весь факт его регионов ÷ план Севера.
        var rm = analytics.Direction(Rm1.ToString(), Sep1, new DateOnly(2026, 9, 30)).Kpi;
        Assert.Equal((300_000m, 251_705.08m, 251_705.08m / 300_000m), (rm.PlanKg!.Value, rm.PlanFactKg!.Value, rm.Execution!.Value));

        // Сам Олмалик — без плана: ни выполнения, ни «факта в плане»; план его ТП из Linko не подставляется.
        var region = analytics.Region(olmalik, Sep1, new DateOnly(2026, 9, 30), "kg", null).Kpi;
        Assert.True(region.PlanKg is null && region.Execution is null && region.PlanFactKg is null && region.PlanForecastKg is null);
        Assert.Equal(1_704.58m, region.FactKg);
    }

    /// <summary>Типы Linko 1 и 2 — Помадка, 3 — Шоколад, 4 — Бамбук, 5 — Песочный.</summary>
    private static readonly Dictionary<long, string> PlanTypes = new() { [1] = "Помадка", [2] = "Помадка 0,5 кг", [3] = "Шоколад", [4] = "Бамбук", [5] = "Песочный" };

    private static readonly Dictionary<string, string> PlanReport = new() { ["Помадка"] = "1,2", ["Шоколад"] = "3", ["Бамбук"] = "4", ["Песочный"] = "5" };

    /// <summary>
    /// Север: ТП 1 — Помадка 60 + 30 кг, Шоколад 30, Песочный 20; без агента — Помадка 10. Юг: ТП 3 — Помадка 50.
    /// План РОП по типам Linko: Север — 1: 100, 2: 50, 3: 40, 4: 30; Юг — 1: 80. У ТП 1 в Linko — свои планы.
    /// </summary>
    private static MonthData RegionCategoryPlanData(DateOnly dataThrough) => Data(
        current:
        [
            Sku(100, 1, market: 10, kg: 60), Sku(101, 2, market: 11, kg: 30), Sku(102, 3, market: 12, kg: 30), Sku(103, 5, market: 13, kg: 20),
            new SaleLine(new DateOnly(2026, 9, 3), null, 14, 101, 2, 101, 10, 100, 77),
            new SaleLine(new DateOnly(2026, 9, 3), 3, 20, 102, 1, 100, 50, 500, 78),
        ],
        categories: PlanTypes,
        reportCategories: PlanReport,
        dataThrough: dataThrough,
        planSource: PlanSources.Rop,
        plans:
        [
            new PlanRow(North, null, 9, 1, 100), new PlanRow(North, null, 9, 2, 50), new PlanRow(North, null, 9, 3, 40), new PlanRow(North, null, 9, 4, 30),
            new PlanRow(South, null, 9, 1, 80),
            new PlanRow(null, 1, 9, null, 500),
        ],
        categoryPlans: [new CategoryPlanRow(1, 2026, 9, [SalesCategories.Build(PlanReport, PlanTypes).GroupByName("Шоколад")!.Value], 999)]);

    [Fact]
    public void Category_plan_is_the_sum_of_region_category_plans_and_its_fact_is_all_category_sales_of_the_scope()
    {
        var analytics = new SalesAnalytics(RegionCategoryPlanData(new DateOnly(2026, 9, 20)));
        var republic = analytics.Republic(Sep1, new DateOnly(2026, 9, 20));

        // Помадка: план 100 + 50 + 80 (типы 1 и 2 — одна категория), факт 60 + 30 + 10 + 50 — и без агента, и ТП без плана.
        Assert.Equal(["Помадка", "Шоколад", "Бамбук", "Песочный"], republic.CategoryPlans!.Select(r => r.Name));
        var pomadka = republic.CategoryPlans![0];
        Assert.Equal((230m, 150m, 150m / 230m, 80m), (pomadka.PlanKg!.Value, pomadka.FactKg, pomadka.Execution!.Value, pomadka.RemainingKg!.Value));
        Assert.Equal((225m, 225m / 230m, TargetLevel.Warning), (pomadka.ForecastKg!.Value, pomadka.ForecastExecution!.Value, pomadka.ForecastLevel!.Value)); // 150 ÷ 20 дн. × 30
        var chocolate = republic.CategoryPlans[1];
        Assert.Equal((40m, 30m, 10m, TargetLevel.Good), (chocolate.PlanKg!.Value, chocolate.FactKg, chocolate.RemainingKg!.Value, chocolate.ForecastLevel!.Value)); // показатель ТП 1 из Linko (999) не участвует
        var bamboo = republic.CategoryPlans[2];
        Assert.Equal((30m, 0m, 30m, TargetLevel.Bad), (bamboo.PlanKg!.Value, bamboo.FactKg, bamboo.RemainingKg!.Value, bamboo.ForecastLevel!.Value)); // план без продаж — строка остаётся
        var sandy = republic.CategoryPlans[3];
        Assert.True(sandy.PlanKg is null && sandy.Execution is null && sandy.RemainingKg is null && sandy.FactKg == 20m); // продаётся без плана

        // Итог — с сервера, по строкам с планом: 300 кг плана, 180 кг факта, прогноз 270 кг — ровно 90% плана.
        var total = republic.CategoryPlanTotal!;
        Assert.Equal((300m, 180m, 0.6m, 120m), (total.PlanKg!.Value, total.FactKg, total.Execution!.Value, total.RemainingKg!.Value));
        Assert.Equal((0.9m, TargetLevel.Warning), (total.ForecastExecution!.Value, total.ForecastLevel!.Value));

        // Регион — только его планы и продажи.
        var region = analytics.Region(North, Sep1, new DateOnly(2026, 9, 20), "kg", null);
        Assert.Equal((150m, 100m), (region.CategoryPlans![0].PlanKg!.Value, region.CategoryPlans[0].FactKg));
        Assert.Equal((220m, 130m), (region.CategoryPlanTotal!.PlanKg!.Value, region.CategoryPlanTotal.FactKg));

        // У ТП плана по категориям из планов регионов нет — карточка ТП по его показателям Linko.
        Assert.Equal((999m, 30m), (analytics.Agent(1).CategoryPlan[0].PlanKg!.Value, analytics.Agent(1).CategoryPlan[0].FactKg));

        // Закрытый месяц: прогноза к плану нет, выполнение и «осталось» остаются.
        var closed = new SalesAnalytics(RegionCategoryPlanData(new DateOnly(2026, 9, 30))).Republic(Sep1, new DateOnly(2026, 9, 30)).CategoryPlanTotal!;
        Assert.True(closed.ForecastKg is null && closed.ForecastExecution is null && closed.ForecastLevel is null);
        Assert.Equal((0.6m, 120m), (closed.Execution!.Value, closed.RemainingKg!.Value));
    }

    [Fact]
    public void Without_region_plans_category_plan_comes_from_linko_indicators_and_the_total_is_computed_on_the_server()
    {
        var map = SalesCategories.Build(PlanReport, PlanTypes);
        var data = Data(
            current: [Sku(100, 1, market: 10, kg: 60), Sku(101, 2, market: 11, kg: 40), Sku(102, 3, market: 12, kg: 30), Sku(103, 4, market: 13, kg: 20)],
            categories: PlanTypes,
            reportCategories: PlanReport,
            categoryPlans: [new CategoryPlanRow(1, 2026, 9, [map.GroupByName("Помадка")!.Value, map.GroupByName("Шоколад")!.Value], 200)]);

        var republic = new SalesAnalytics(data).Republic(Sep1, new DateOnly(2026, 9, 10));

        Assert.Equal(PlanSources.Linko, republic.Kpi.PlanSource);
        Assert.Equal(["Помадка + Шоколад", "Бамбук"], republic.CategoryPlans!.Select(r => r.Name));
        var total = republic.CategoryPlanTotal!; // только строки с планом: «Бамбук» без плана в итог не входит
        Assert.Equal((200m, 130m, 0.65m, 70m), (total.PlanKg!.Value, total.FactKg, total.Execution!.Value, total.RemainingKg!.Value));
        Assert.Equal(390m / 200m, total.ForecastExecution); // 130 ÷ 10 дн. × 30

        Assert.Null(new SalesAnalytics(Data(categoryPlans: [])).Republic(Sep1, new DateOnly(2026, 9, 10)).CategoryPlanTotal); // плана нет — итога нет
    }

    [Fact]
    public void Plan_and_fact_by_month_uses_region_plans_where_the_month_has_them_and_linko_plans_otherwise()
    {
        var data = Data(
            planSource: PlanSources.Rop,
            plans: [new PlanRow(North, null, 9, 4, 300)],
            yearRegionPlans: [new PlanRow(North, null, 9, 4, 300), new PlanRow(North, null, 8, 4, 200), new PlanRow(South, null, 6, 4, 70)],
            yearAgentPlans: [new PlanRow(null, 1, 7, null, 90), new PlanRow(null, 1, 6, null, 80)]);

        var months = new SalesAnalytics(data).Region(North, Sep1, new DateOnly(2026, 9, 10), "kg", null).Months;

        Assert.Equal((300m, PlanSources.Rop), (months[8].PlanKg!.Value, months[8].PlanSource));
        Assert.Equal((200m, PlanSources.Rop), (months[7].PlanKg!.Value, months[7].PlanSource));
        Assert.Equal((90m, PlanSources.Linko), (months[6].PlanKg!.Value, months[6].PlanSource)); // в июле планов регионов нет — планы ТП из Linko
        Assert.True(months[5].PlanKg is null && months[5].PlanSource is null); // в июне РОП есть только у Юга: планы ТП Севера не подмешиваются
    }

    [Fact]
    public void Next_month_card_takes_region_plans_of_the_next_month_and_falls_back_to_linko_plans()
    {
        var current = new[] { new PlanRow(North, null, 9, 1, 250), new PlanRow(North, null, 9, 3, 50), new PlanRow(South, null, 9, 1, 60), new PlanRow(null, 1, 9, null, 100) };
        var next = new[] { new PlanRow(North, null, 10, 1, 200), new PlanRow(North, null, 10, 3, 110), new PlanRow(South, null, 10, 1, 70) };
        var linkoNext = new[] { new PlanRow(null, 1, 10, null, 120) };
        var analytics = new SalesAnalytics(Data(plans: current, planSource: PlanSources.Rop, nextRegionPlans: next, nextPlans: linkoNext,
            categories: PlanTypes, reportCategories: PlanReport));

        var republic = analytics.Republic(Sep1, new DateOnly(2026, 9, 10)).NextMonth!;
        Assert.Equal((PlanSources.Rop, 380m, 360m, 0), (republic.Source, republic.PlanKg, republic.CurrentPlanKg!.Value, republic.Agents));
        Assert.Equal(20m / 360m, republic.Change);
        var north = republic.Rows.Single(r => r.Id == North.ToString());
        Assert.Equal((310m, 300m, 10m / 300m), (north.PlanKg, north.CurrentPlanKg!.Value, north.Change!.Value));

        // В регионе — по категориям отчёта, изменение к плану РОП текущего месяца.
        var byCategory = analytics.Region(North, Sep1, new DateOnly(2026, 9, 10), "kg", null).NextMonth!;
        Assert.Equal(["Помадка", "Шоколад"], byCategory.Rows.Select(r => r.Name));
        Assert.Equal((-0.2m, 1.2m), (byCategory.Rows[0].Change!.Value, byCategory.Rows[1].Change!.Value));

        // Планов регионов на следующий месяц нет — карточка по планам ТП из Linko.
        var linko = new SalesAnalytics(Data(plans: current, planSource: PlanSources.Rop, nextPlans: linkoNext)).Republic(Sep1, new DateOnly(2026, 9, 10)).NextMonth!;
        Assert.Equal((PlanSources.Linko, 120m, 100m, 1), (linko.Source, linko.PlanKg, linko.CurrentPlanKg!.Value, linko.Agents));
        Assert.Equal(0.2m, linko.Change);
    }

    [Fact]
    public void Directions_supervisors_and_dealers_are_loaded_from_the_onebase_structure()
    {
        var rm1 = new SalesDirection { Id = Rm1, Name = "РМ 1", Kind = DirectionKind.RegionalManager, ManagerName = " Алиев ", SortOrder = 1 };
        var bazaar = new SalesDirection { Name = "Базар", Kind = DirectionKind.Channel, SortOrder = 0 };
        var empty = new SalesDirection { Name = "РМ 4", Kind = DirectionKind.RegionalManager, SortOrder = 4 };
        var old = new SalesRegion { LinkoBranchId = 103, Name = "Юг (эски) 2", DirectionId = Rm1, DealerName = "старый дилер" };
        var factory = new SalesRegion { LinkoBranchId = 4, Name = "Завод", DirectionId = empty.Id };

        var structure = SalesStructure.Build(
            [rm1, bazaar, empty],
            [
                new SalesRegion { Id = North, LinkoBranchId = 101, Name = "Север", DirectionId = Rm1, SupervisorName = " Каримов ", DealerName = "ООО Север" },
                new SalesRegion { Id = South, LinkoBranchId = 102, Name = "Юг", DirectionId = bazaar.Id, DealerName = "  " },
                old,
                factory,
            ],
            new HashSet<long> { 4 },
            new SalesOptions().OldBranchSuffix);

        Assert.Equal(["РМ 1", "Базар", "РМ 4"], structure.Directions.Select(d => d.Name));
        Assert.Equal((false, true, "Алиев"), (structure.Directions[0].IsChannel, structure.Directions[1].IsChannel, structure.Directions[0].ManagerName));
        Assert.Equal(["Север", "Юг"], structure.Regions.Select(r => r.Name)); // «Завод» — не регион вторички, старый филиал — в Юге
        var north = structure.Regions[0];
        Assert.Equal((Rm1, "Каримов", "ООО Север"), (north.DirectionId!.Value, north.Supervisor, north.Dealer));
        Assert.Null(structure.Regions[1].Dealer); // пустое значение — дилер не задан
        Assert.Equal(South, structure.BranchAliases[103]);
        Assert.Equal(((Guid?)South, (Guid?)North, (Guid?)null), (structure.RegionOf(old.Id), structure.RegionOf(North), structure.RegionOf(factory.Id)));

        var analytics = new SalesAnalytics(Data(regions: structure.Regions, directions: structure.Directions, branchAliases: structure.BranchAliases));

        // Карточки РМ и каналов (каналы — после РМ); направление без регионов карточки не даёт.
        Assert.Equal(["РМ 1", "Базар"], analytics.Republic(Sep1, new DateOnly(2026, 9, 10)).Cards.Select(c => c.Name));
        Assert.Equal("Север", analytics.Direction(Rm1.ToString(), Sep1, new DateOnly(2026, 9, 10)).Regions.Single().Name);
        var region = analytics.Region(North, Sep1, new DateOnly(2026, 9, 10), "kg", null);
        Assert.Equal(("РМ 1", "Каримов", "ООО Север"), (region.DirectionName, region.Supervisor, region.Dealer));

        // Охват РМ в «Проблемных агентах» и «Ассортименте».
        Assert.Equal([1L, 2L], analytics.Problems(Rm1.ToString(), FlagKind.LowData, false).Agents.Select(a => a.AgentId).Order());
        Assert.Equal("Базар", analytics.Assortment(bazaar.Id.ToString(), null).ScopeName);
    }

    // ================= Календарь визитов, календарь месяца, «АКБ по месяцам», проблемные агенты =================

    private static VisitRecord Visit(long agent, long market, int day, bool inPlan = true, VisitStatus status = VisitStatus.Done) =>
        new(new DateOnly(2026, 9, day), agent, market, status, inPlan);

    /// <summary>Заказ ТП 1 (Север) в ТТ: введён created-го, принят accepted-го; 10 кг / 100 сум.</summary>
    private static SaleLine Accepted(long order, long market, int created, int accepted, long agent = 1, long branch = 101) =>
        new(new DateOnly(2026, 9, accepted), agent, market, branch, 1, 100, 10, 100, order, new DateOnly(2026, 9, created));

    [Fact]
    public void Visit_calendar_places_orders_by_acceptance_and_classifies_them_by_the_visit_on_the_creation_date()
    {
        // ТП 1: визит по плану в ТТ 10 третьего (заказ введён 3-го, принят 4-го), визит без плана в ТТ 11 пятого (заказ 5-го / 6-го),
        // заказ в ТТ 12 без визита в день ввода, невыполненный визит по плану 9-го. В ТТ 13 седьмого по плану был ТП 2 — заказ ТП 1,
        // введённый 7-го, всё равно «с визита по маршруту» (сшивка по магазину и дню ввода, любой ТП). Заказ 5 введён 10-го, принят 12-го.
        var visits = new[] { Visit(1, 10, 3), Visit(1, 11, 5, inPlan: false), Visit(2, 13, 7), Visit(1, 14, 9, status: VisitStatus.Pending) };
        var current = new[] { Accepted(1, 10, 3, 4), Accepted(2, 11, 5, 6), Accepted(3, 12, 6, 6), Accepted(4, 13, 7, 8), Accepted(5, 10, 10, 12) };
        var analytics = new SalesAnalytics(Data(current: current, visits: visits, agents: RepsAndOperator(), salesRepJobs: ["Агент"]));

        var region = analytics.Region(North, Sep1, new DateOnly(2026, 9, 10), "kg", null).VisitCalendar.Single();
        var agent = region.Children.Single(c => c.Id == "1");
        Assert.Equal((2, 1, 1, 2), (agent.Plan, agent.DoneInPlan, agent.DoneOffPlan, agent.DoneAll)); // план — визиты по маршруту (и невыполненный)
        Assert.Equal((4, 2, 1, 1), (agent.OrdersTotal, agent.OrdersInPlan, agent.OrdersOffPlan, agent.OrdersNoVisit)); // заказ 5 принят 12-го — вне дней
        Assert.Equal((200m, 100m, 400m), (agent.OrdersInPlanSum, agent.OrdersOffPlanSum, agent.OrdersTotalSum));
        Assert.Equal((1, 0.5m, TargetLevel.Bad), (agent.NotVisited, agent.PlanShare!.Value, agent.PlanShareLevel!.Value));
        var second = region.Children.Single(c => c.Id == "2");
        Assert.Equal((1, 1, 0), (second.Plan, second.DoneInPlan, second.OrdersTotal));
        // Итог региона — по его суммам.
        Assert.Equal((3, 2, 1, 3, 4, 2, 1, 1), (region.Plan, region.DoneInPlan, region.DoneOffPlan, region.DoneAll, region.OrdersTotal, region.OrdersInPlan, region.OrdersOffPlan, region.OrdersNoVisit));
        Assert.Equal((1, 2m / 3m), (region.NotVisited, region.PlanShare!.Value));

        // Один день: заказ на календаре — в день приёмки, а «с визита» он по визиту в день ввода (3-го).
        var day4 = analytics.Region(North, new DateOnly(2026, 9, 4), new DateOnly(2026, 9, 4), "kg", null).VisitCalendar.Single().Children.Single(c => c.Id == "1");
        Assert.Equal((0, 0, 1, 1), (day4.Plan, day4.DoneAll, day4.OrdersTotal, day4.OrdersInPlan));
        var day12 = analytics.Region(North, new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 12), "kg", null).VisitCalendar.Single().Children.Single(c => c.Id == "1");
        Assert.Equal((1, 0, 1), (day12.OrdersTotal, day12.OrdersInPlan, day12.OrdersNoVisit)); // введён 10-го без визита — «без визита»
        Assert.Equal((new DateOnly(2026, 9, 4), new DateOnly(2026, 9, 4)), analytics.VisitRange(4, 4));
    }

    [Fact]
    public void Visit_calendar_rows_are_the_team_and_the_total_comes_from_the_server_with_shortfall_on_sums()
    {
        // ТП 1 (Север): план 3, по маршруту 1; ТП 2 (Север): план 1, по маршруту 1; ТП 4 («Агент» без региона) — вне охвата регионов;
        // оператор 3 (Юг, не ТП): 2 выполненных визита — отдельным числом, не строкой.
        var visits = new[]
        {
            Visit(1, 10, 1), Visit(1, 15, 2, status: VisitStatus.Pending), Visit(1, 16, 3, status: VisitStatus.Pending), Visit(2, 11, 2),
            Visit(3, 13, 4), Visit(3, 13, 5),
        };
        var republic = new SalesAnalytics(Data(agents: RepsAndOperator(), salesRepJobs: ["Агент"], visits: visits)).Republic(Sep1, new DateOnly(2026, 9, 10));

        var north = republic.VisitCalendar.Single(r => r.Id == North.ToString());
        Assert.Equal(["1", "2"], north.Children.Select(c => c.Id).Order());
        Assert.Equal((4, 2, 2, 0.5m), (north.Plan, north.DoneInPlan, north.NotVisited, north.PlanShare!.Value));
        var south = republic.VisitCalendar.Single(r => r.Id == South.ToString());
        Assert.Empty(south.Children);
        Assert.Equal((0, 0, 2), (south.Plan, south.DoneAll, south.VisitsOutsideTeam)); // визиты оператора не пропали

        var total = republic.VisitCalendarTotal!;
        Assert.Equal((4, 2, 2, 0.5m, 2), (total.Plan, total.DoneInPlan, total.NotVisited, total.PlanShare!.Value, total.VisitsOutsideTeam));
        Assert.Equal(republic.VisitCalendar.Sum(r => r.OrdersTotal), total.OrdersTotal);
        Assert.Equal(TargetLevel.Bad, total.PlanShareLevel);
    }

    [Fact]
    public void Group_calendar_by_regions_sums_days_to_the_month_value()
    {
        var analytics = new SalesAnalytics(Data());
        var republic = analytics.Republic(Sep1, new DateOnly(2026, 9, 10), "kg", null);
        var calendar = republic.Calendar!;

        Assert.Equal("kg", calendar.Metric);
        var north = calendar.Rows.Single(r => r.Id == North.ToString());
        Assert.Equal((175m, 175m), (north.Total, north.Days.Sum(d => d ?? 0)));
        Assert.Equal((republic.Kpi.FactKg, calendar.Total), (calendar.TotalDays.Sum(d => d ?? 0), calendar.Rows.Sum(r => r.Total)));
        Assert.All(calendar.TotalDays.Skip(10), d => Assert.Null(d)); // после отчётного дня — нет данных

        var sum = analytics.Republic(Sep1, new DateOnly(2026, 9, 10), "sum", null).Calendar!;
        Assert.Equal((republic.Kpi.Revenue, sum.Total), (sum.TotalDays.Sum(d => d ?? 0), sum.Rows.Sum(r => r.Total)));

        var akb = analytics.Republic(Sep1, new DateOnly(2026, 9, 10), "akb", 1).Calendar!;
        Assert.Equal((1L, 5m), (akb.CategoryId!.Value, akb.Total)); // ТТ 10–14, уникальные за месяц
        Assert.Equal(3m, akb.Rows.Single(r => r.Id == North.ToString()).Total);
        Assert.Same(analytics.CachedRepublic(null, null), analytics.CachedRepublic(1, 10, "kg", null));
        Assert.NotSame(analytics.CachedRepublic(null, null), analytics.CachedRepublic(1, 10, "sum", null));

        // РМ — те же строки по его регионам.
        var direction = analytics.Direction(Rm1.ToString(), Sep1, new DateOnly(2026, 9, 10), "kg", null).Calendar!;
        Assert.Equal(["Север", "Юг"], direction.Rows.Select(r => r.Name));
        Assert.Equal(205m, direction.Total);
    }

    [Fact]
    public void Akb_by_month_returns_kg_and_sum_series_averages_and_hides_configured_categories()
    {
        var types = new Dictionary<long, string> { [1] = "Бамбук", [2] = "Песочный" };
        var report = new Dictionary<string, string> { ["Бамбук"] = "1", ["Песочный"] = "2" };
        var bamboo = SalesCategories.Build(report, types).GroupByName("Бамбук")!.Value;
        var history = new[]
        {
            new MonthlyAkb(2026, 1, false, null, true, null, 40, Kg: 400, Revenue: 4000),
            new MonthlyAkb(2026, 1, false, null, false, bamboo, 30, Kg: 300, Revenue: 3000),
        };
        var data = Data(
            current: [Sku(100, 1, market: 10, kg: 60), Sku(101, 2, market: 11, kg: 40)],
            previous: [Sku(100, 1, market: 12, kg: 30, month: 8)],
            categories: types,
            reportCategories: report,
            akbHistory: history,
            akbHidden: ["песочный"]);

        var republic = new SalesAnalytics(data).Republic(Sep1, new DateOnly(2026, 9, 10));
        var akb = republic.AkbMonths;

        Assert.Equal(2, republic.CategoryCards.Count); // в карточках «Песочный» остаётся
        var series = Assert.Single(akb.Categories); // в графике — нет
        Assert.Equal("Бамбук", series.Name);
        Assert.Equal((30, 1, 1), (series.Values[0], series.Values[7], series.Values[8]));
        Assert.Equal(32m / 3m, series.Average); // по месяцам с данными: январь, август, сентябрь
        Assert.Equal((40, 1, 2), (akb.Total[0], akb.Total[7], akb.Total[8]));
        Assert.Equal(43m / 3m, akb.Average);

        var kgSeries = Assert.Single(akb.Kg!.Categories);
        Assert.Equal(("Бамбук", 300m, 30m, 60m, 130m), (kgSeries.Name, kgSeries.Values[0]!.Value, kgSeries.Values[7]!.Value, kgSeries.Values[8]!.Value, kgSeries.Average!.Value));
        Assert.Equal((400m, 30m, 100m), (akb.Kg.Total[0]!.Value, akb.Kg.Total[7]!.Value, akb.Kg.Total[8]!.Value)); // итог — все строки, и скрытая категория тоже
        Assert.Null(akb.Kg.Total[1]); // за февраль данных нет
        Assert.Equal((4000m, 300m, 1000m, 600m), (akb.Sum!.Total[0]!.Value, akb.Sum.Total[7]!.Value, akb.Sum.Total[8]!.Value, akb.Sum.Categories[0].Values[8]!.Value));
        Assert.Equal(5300m / 3m, akb.Sum.Average);
    }

    [Fact]
    public void Akb_by_month_draws_at_most_seven_categories_by_volume()
    {
        var types = Enumerable.Range(1, 9).ToDictionary(i => (long)i, i => $"Категория {i}");
        var report = Enumerable.Range(1, 9).ToDictionary(i => $"Категория {i}", i => i.ToString());
        // Категория i продаёт i кг: самые лёгкие — 1 и 2 — остаются только в карточках.
        var data = Data(
            current: Enumerable.Range(1, 9).Select(i => Sku(100 + i, i, market: 10 + i, kg: i)).ToList(),
            categories: types,
            reportCategories: report);

        var republic = new SalesAnalytics(data).Republic(Sep1, new DateOnly(2026, 9, 10));

        Assert.Equal(9, republic.CategoryCards.Count);
        Assert.Equal(7, republic.AkbMonths.Categories.Count);
        Assert.Equal(["Категория 9", "Категория 8", "Категория 7", "Категория 6", "Категория 5", "Категория 4", "Категория 3"], republic.AkbMonths.Categories.Select(c => c.Name));
        Assert.Equal(republic.AkbMonths.Categories.Select(c => c.Id), republic.AkbMonths.Kg!.Categories.Select(c => c.Id));
    }

    [Fact]
    public void Agent_card_gives_revenue_and_kg_per_outlet_and_execution_levels_from_the_server()
    {
        var analytics = new SalesAnalytics(Data(plans: [new PlanRow(null, 3, 9, null, 50)], revenuePlans: [new PlanRow(null, 3, 9, null, 1000)]));

        var agent = analytics.Agent(3); // ТТ 13: 40 − 10 кг, 400 − 100 сум
        Assert.Equal((1, 300m, 30m), (agent.Akb, agent.RevenuePerOutlet!.Value, agent.KgPerOutlet!.Value));
        Assert.Equal((0.6m, TargetLevel.Warning), (agent.Execution!.Value, agent.ExecutionLevel!.Value));
        Assert.Equal((0.3m, TargetLevel.Bad), (agent.RevenueExecution!.Value, agent.RevenueExecutionLevel!.Value));

        var none = new SalesAnalytics(Data(visits: [Visit(5, 10, 1)])).Agent(5); // визиты есть, покупок нет
        Assert.True(none.Akb == 0 && none.RevenuePerOutlet is null && none.KgPerOutlet is null && none.ExecutionLevel is null);
    }

    [Fact]
    public void Execution_levels_follow_the_reference_thresholds_on_tiles_rows_and_plans()
    {
        // Север: план 300, факт 175 → 58% — красный; Юг: план 60, факт 30 → 50% — красный; ТП 1: план 100, факт 100 — зелёный.
        var analytics = new SalesAnalytics(Data(plans: [new PlanRow(North, null, 9, null, 300), new PlanRow(South, null, 9, null, 60), new PlanRow(null, 1, 9, null, 100)],
            planSource: PlanSources.Rop));
        var republic = analytics.Republic(Sep1, new DateOnly(2026, 9, 10));

        Assert.Equal(TargetLevel.Bad, republic.Kpi.ExecutionLevel);
        Assert.Equal(TargetLevel.Bad, republic.Regions.Single(r => r.Id == North.ToString()).ExecutionLevel);
        Assert.Equal(TargetLevel.Good, republic.Regions.Single(r => r.Id == North.ToString()).ForecastExecutionLevel); // 525 ÷ 300
        var team = analytics.Region(North, Sep1, new DateOnly(2026, 9, 10), "kg", null).Team.Single(t => t.AgentId == 1);
        Assert.Equal((TargetLevel.Good, TargetLevel.Good), (team.ExecutionLevel!.Value, team.ForecastExecutionLevel!.Value));
        Assert.Null(analytics.Region(North, Sep1, new DateOnly(2026, 9, 10), "kg", null).Team.Single(t => t.AgentId == 2).ExecutionLevel); // плана нет
    }

    [Fact]
    public void Problem_agents_are_ordered_by_score_and_numbered_by_their_position()
    {
        // ТП 1 (Север): 20 визитов, 4 заказа (20% — риск), 3 категории (риск при цели 4): 4 + 4 + (1 − 0,2) × 3 = 10,4.
        // ТП 2 (Юг): 20 визитов, 20 заказов (100%), 2 категории (критично): 10 + 0 = 10. По числу критичных ТП 2 был бы первым — по тяжести первый ТП 1.
        var agents = new Dictionary<long, AgentInfo> { [1] = new(1, "А", true, North, false, true), [2] = new(2, "Б", true, South, false, true) };
        var visits = Enumerable.Range(1, 20).SelectMany(i => new[] { Visit(1, 100 + i, 1 + i % 10), Visit(2, 200 + i, 1 + i % 10) }).ToList();
        var current = Enumerable.Range(1, 4).Select(i => new SaleLine(new DateOnly(2026, 9, 2), 1, 100 + i, 101, i % 3 + 1, 100, 10, 100, i))
            .Concat(Enumerable.Range(1, 20).Select(i => new SaleLine(new DateOnly(2026, 9, 2), 2, 200 + i, 102, i % 2 + 1, 100, 10, 100, 100 + i)))
            .Concat([new SaleLine(new DateOnly(2026, 9, 3), null, 300, 101, 1, 100, 60, 600, 999)]) // факт без агента
            .ToList();
        var categories = new Dictionary<long, string> { [1] = "Бамбук", [2] = "Кекс", [3] = "Печенье" };

        var problems = new SalesAnalytics(Data(current: current, visits: visits, agents: agents, categories: categories)).Problems(null, null, false);

        Assert.Equal([1L, 2L], problems.Agents.Select(a => a.AgentId));
        Assert.Equal([1, 2], problems.Agents.Select(a => a.Rank!.Value));
        Assert.Equal((10.4m, 10m), (problems.Agents[0].Score, problems.Agents[1].Score));
        Assert.Equal(2, problems.Found);
        Assert.Equal((300m, 60m, 0.2m, 240m), (problems.Fact!.FactKg, problems.Fact.UnassignedKg, problems.Fact.UnassignedShare!.Value, problems.Fact.AssignedKg));
        Assert.Equal("РМ 1", Assert.Single(problems.Directions!).Name);
    }
}
