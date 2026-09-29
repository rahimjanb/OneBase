using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;

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
        IReadOnlyDictionary<long, Guid>? branchAliases = null) => new()
    {
        BranchAliases = branchAliases ?? new Dictionary<long, Guid>(),
        AkbHistory = akbHistory ?? [],
        CategoryMap = reportCategories is null ? null : SalesCategories.Build(reportCategories, categories ?? new Dictionary<long, string> { [1] = "Печенье" }),
        VisitOrders = visitOrders,
        Year = 2026,
        Month = 9,
        DataThrough = new DateOnly(2026, 9, 10),
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
        YearRegionPlans = [],
        Agents = new Dictionary<long, AgentInfo>
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
    public void Agent_category_plan_uses_report_categories_not_linko_types()
    {
        var data = Data(
            current: [Sku(100, 1, market: 10, kg: 60), Sku(101, 2, market: 11, kg: 40)],
            plans: [new PlanRow(null, 1, 9, 2, 50)], // ручной план по фасовке «Помадка 0,5 кг»
            categories: new Dictionary<long, string> { [1] = "Помадка", [2] = "Помадка 0,5 кг" },
            reportCategories: new Dictionary<string, string> { ["Помадка"] = "1,2" });

        var row = Assert.Single(new SalesAnalytics(data).Agent(1).CategoryPlan);

        Assert.Equal(("Помадка", 100m, 50m), (row.Name, row.FactKg, row.PlanKg!.Value));
    }

    [Fact]
    public void Visit_matches_the_order_by_creation_date_not_by_acceptance_date()
    {
        // Агент был в ТТ 10-го и ввёл заказ; магазин принял товар 11-го — продажа 11-го, а визит «с заказом» — 10-го.
        var accepted = new SaleLine(new DateOnly(2026, 9, 11), 1, 10, 101, 1, 100, 50, 500, 7);
        var created = accepted with { Date = new DateOnly(2026, 9, 10) };
        var visits = new[] { new VisitRecord(new DateOnly(2026, 9, 10), 1, 10, VisitStatus.Done, true) };

        var byCreation = new SalesAnalytics(Data(current: [accepted], visitOrders: [created], visits: visits)).Agent(1);
        var byAcceptance = new SalesAnalytics(Data(current: [accepted], visits: visits)).Agent(1);

        Assert.Equal(1m, byCreation.Conversion.Value);
        Assert.Equal(0m, byAcceptance.Conversion.Value); // сшивка по дате приёмки теряет визит
        Assert.Equal(50m, byCreation.FactKg);
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
}
