using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;

namespace OneBase.Sales.Tests;

/// <summary>Правила «Ассортимента» (DOC §9.2, §9.6): положительная строка SKU, «продаётся», «Только он», матрица, ТОП, регионы, «Товары».</summary>
public class AssortmentTests
{
    private static readonly Guid North = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid South = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid East = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly Guid Rm1 = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

    private const long Bamboo = 1;
    private const long Cake = 2;
    private const long Bonus = 16;

    /// <summary>Строка продажи товара (кг, выручка) в ТТ: агент, филиал (101 — Север, 102 — Юг, 103 — Восток), тип товара Linko.</summary>
    private static SaleLine L(long product, long market, decimal kg, decimal? revenue = null, long agent = 1, long branch = 101, long type = Bamboo,
        int month = 9, bool isReturn = false) =>
        new(new DateOnly(2026, month, 2), agent, market, branch, type, product, kg, revenue ?? kg * 10,
            isReturn ? null : product * 1000 + market * 10 + agent + month * 100_000);

    private static readonly Dictionary<long, ProductInfo> Products = new()
    {
        [100] = new(100, "Бамбук 002", "002", Bamboo),
        [101] = new(101, "Бамбук 001", "001", Bamboo),
        [102] = new(102, "Бамбук 336", "336", Bamboo),
        [103] = new(103, "Бамбук молчит", "777", Bamboo),
        [200] = new(200, "Кекс 318", "318", Cake),
        [900] = new(900, "Бокал", null, Bonus),
    };

    private static MonthData Data(
        IReadOnlyList<SaleLine> current,
        IReadOnlyList<SaleLine>? previous = null,
        IReadOnlySet<long>? activeSkus = null,
        IReadOnlyList<RegionInfo>? regions = null,
        IReadOnlyList<DirectionInfo>? directions = null,
        string[]? top = null) => new()
    {
        Year = 2026,
        Month = 9,
        DataThrough = new DateOnly(2026, 9, 30),
        Current = current,
        Previous = previous ?? [],
        Visits = [],
        History = [],
        Plans = [],
        YearRegionPlans = [],
        Agents = new Dictionary<long, AgentInfo>
        {
            [1] = new(1, "ТП 1", true, North, false, true),
            [2] = new(2, "ТП 2", true, North, false, true),
            [3] = new(3, "ТП 3", true, South, false, true),
        },
        Regions = regions ??
        [
            new RegionInfo(North, 101, "Север", Rm1, null, null),
            new RegionInfo(South, 102, "Юг", Rm1, null, null),
        ],
        Directions = directions ?? [new DirectionInfo(Rm1, "РМ 1", false, null, null, 1)],
        Markets = new Dictionary<long, MarketInfo>(),
        Categories = new Dictionary<long, string> { [Bamboo] = "Бамбук", [Cake] = "Кекс", [Bonus] = "бонус" },
        CategoryMap = SalesCategories.Build(new Dictionary<string, string> { ["Бамбук"] = "1", ["Кекс"] = "2" },
            new Dictionary<long, string> { [Bamboo] = "Бамбук", [Cake] = "Кекс", [Bonus] = "бонус" }),
        Products = Products,
        ActiveSkus = activeSkus ?? new HashSet<long> { 100, 101, 102, 103, 200 },
        Top = TopProductSet.From(top ?? ["002", " 318 "]),
        MarketAssignments = [],
        Targets = new SalesTargets(0.7m, 1_439_000m, 125m, 4m),
        Thresholds = new FlagThresholds(),
    };

    private static readonly AssortmentScope Republic = new(null, null, null, false);

    // ================= Положительная строка SKU =================

    [Fact]
    public void Sku_outlets_are_distinct_markets_with_a_positive_net_line_by_weight_or_money()
    {
        var lines = new[]
        {
            L(100, 10, 5),
            L(100, 10, 3, agent: 2), // та же ТТ у второго ТП — одна ТТ
            L(100, 11, 2),
            L(100, 11, -2, isReturn: true), // вернула всё — строка 0
            L(100, 12, 0, revenue: 5_000), // бонусная строка без веса, но с деньгами — положительна
            L(100, 13, 4),
            L(100, 13, -6, isReturn: true), // возврат больше покупки
            L(100, 14, 1, revenue: 10) with { MarketId = null }, // строка без ТТ
        };

        Assert.Equal(new HashSet<long> { 10, 12 }, SalesMath.SkuMarkets(lines));
        Assert.Equal(2, SalesMath.SkuTt(lines));
        Assert.True(SalesMath.Positive(0, 1));
        Assert.True(SalesMath.Positive(1, -1));
        Assert.False(SalesMath.Positive(0, 0));
    }

    [Fact]
    public void Sku_sells_in_a_scope_when_at_least_one_outlet_has_a_positive_line_even_if_the_scope_total_is_not_positive()
    {
        // Коканд, август 2026, SKU 336: в регионе продажи погашены возвратами (итог 0), но у ТТ 10 строка положительна — «продаётся».
        var current = new[]
        {
            L(100, 10, 50),
            L(102, 10, 3),
            L(102, 11, 3),
            L(102, 11, -6, isReturn: true),
            L(101, 12, 2),
            L(101, 12, -2, isReturn: true), // ни одной положительной строки — не продаётся
        };

        var card = new SalesAnalytics(Data(current)).Assortment(null, null).Categories.Single(c => c.Name == "Бамбук");
        var sku = card.Skus.ToDictionary(s => s.ProductId);

        Assert.Equal(0m, sku[102].FactKg);
        Assert.Equal((SkuStatuses.Selling, 1), (sku[102].Status, sku[102].Akb));
        Assert.Equal((SkuStatuses.Silent, 0), (sku[101].Status, sku[101].Akb));
        Assert.Equal((2, 4, 2), (card.SkuSold, card.SkuTotal, card.Silent)); // 100 и 102 из 100–103
    }

    [Fact]
    public void Category_outlets_keep_the_documented_rule_net_category_weight_above_zero()
    {
        // ТТ 11: строка 101 положительна (+3), а итог категории −1 (возврат 102) — в ТТ артикула входит, в АКБ категории — нет.
        var current = new[] { L(100, 10, 5), L(101, 11, 3), L(102, 11, -4, isReturn: true) };

        var card = new SalesAnalytics(Data(current)).Assortment(null, null).Categories.Single(c => c.Name == "Бамбук");

        Assert.Equal(1, card.Akb);
        Assert.Equal(1, card.Skus.Single(s => s.ProductId == 101).Akb);
    }

    [Fact]
    public void Catalog_window_runs_from_january_of_the_selected_year_to_the_end_of_the_selected_month()
    {
        Assert.Equal((new DateOnly(2026, 1, 1), new DateOnly(2026, 9, 30)), SalesDataLoader.AssortmentWindow(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30)));
        Assert.Equal((new DateOnly(2026, 1, 1), new DateOnly(2026, 10, 5)), SalesDataLoader.AssortmentWindow(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 5)));
        Assert.Equal((new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 31)), SalesDataLoader.AssortmentWindow(new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 31)));
    }

    [Fact]
    public void Catalog_denominator_is_the_assortment_window_plus_sku_sold_now_or_last_month_never_the_whole_catalog()
    {
        // 103 есть в справочнике, но не в окне ассортимента и без продаж — в знаменатель не входит; 101 продавался в августе — «пропал».
        var data = Data([L(100, 10, 5)], previous: [L(101, 10, 2, month: 8)], activeSkus: new HashSet<long> { 100 });

        var card = Assert.Single(new SalesAnalytics(data).Assortment(null, null).Categories);

        Assert.Equal((1, 2, 1, 1), (card.SkuSold, card.SkuTotal, card.Silent, card.Lost));
        Assert.DoesNotContain(card.Skus, s => s.ProductId == 103);
    }

    [Fact]
    public void Scope_without_a_category_still_lists_it_with_zero_of_the_catalog_denominator()
    {
        // Север продаёт только Бамбук, Кекс (SKU 200) идёт на Юге: в ассортименте Севера Кекс — «продаётся 0 из 1», его SKU — «не возят».
        var analytics = new SalesAnalytics(Data([L(100, 10, 5), L(200, 20, 3, branch: 102, agent: 3, type: Cake)]));

        var north = analytics.Assortment(null, North);
        Assert.Equal(["Бамбук", "Кекс"], north.Categories.Select(c => c.Name)); // категория без продаж — после карточек с продажами
        var cake = north.Categories.Single(c => c.Name == "Кекс");
        Assert.Equal((0, 1, 1, 0, 0m, 0), (cake.SkuSold, cake.SkuTotal, cake.Silent, cake.Lost, cake.FactKg, cake.Akb));
        Assert.Equal(SkuStatuses.Elsewhere, Assert.Single(cake.Skus).Status);
        Assert.Equal((1, 5), (north.Summary.SkuSold, north.Summary.SkuTotal)); // знаменатель — весь каталог, тот же, что у республики
        Assert.Equal(5, analytics.Assortment(null, null).Summary.SkuTotal);

        var page = analytics.Category(cake.Id, new AssortmentScope(null, North, null, false))!;
        Assert.Equal((0, 1), (page.Card!.SkuSold, page.Card.SkuTotal));

        // Во вторичке карточки категории без продаж по-прежнему нет (DOC-filters §1).
        var region = analytics.Region(North, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), "kg", null);
        Assert.Equal(["Бамбук"], region.CategoryCards.Select(c => c.Name));
    }

    // ================= «Только он» =================

    [Fact]
    public void Solo_counts_outlets_whose_only_positive_line_is_this_sku()
    {
        var lines = new[]
        {
            L(100, 10, 5), // ТТ 10 — только 100
            L(100, 11, 5), L(101, 11, 1), // ТТ 11 — два SKU
            L(100, 12, 5), L(101, 12, 1), L(101, 12, -1, isReturn: true), // ТТ 12 — у 101 строка 0: моно по 100
            L(101, 13, 2, agent: 2), // ТТ 13 — только 101
        };

        Assert.Equal(new Dictionary<long, int> { [100] = 2, [101] = 1 }, SalesMath.SoloByProduct(lines));
    }

    [Fact]
    public void Solo_and_mono_reach_sku_rows_category_view_product_view_and_tiles()
    {
        var current = new[]
        {
            L(100, 10, 5),
            L(100, 11, 5), L(200, 11, 1, type: Cake),
            L(200, 12, 3, type: Cake),
            L(100, 13, 2), L(900, 13, 0, revenue: 77, type: Bonus), // бонус моно-точку не портит
        };
        var analytics = new SalesAnalytics(Data(current));

        var view = analytics.Assortment(null, null);
        Assert.Equal(3, view.Summary.Mono); // ТТ 10, 12, 13
        var bamboo = view.Categories.Single(c => c.Name == "Бамбук").Skus.Single(s => s.ProductId == 100);
        Assert.Equal((2, 3, 2m / 3m), (bamboo.Solo, bamboo.Akb, bamboo.SoloShare!.Value));

        var category = analytics.Category(view.Categories.Single(c => c.Name == "Кекс").Id, Republic)!;
        Assert.Equal(3, category.Mono);
        Assert.Equal(1, category.Card!.Skus.Single(s => s.ProductId == 200).Solo);

        var product = analytics.Product(100, Republic)!;
        Assert.Equal((2, 3, 3), (product.Solo, product.Tt, product.Mono));
    }

    // ================= Матрица «товар × регион» =================

    [Fact]
    public void Matrix_level_is_red_when_absent_orange_below_forty_percent_of_average_and_ok_otherwise()
    {
        Assert.Equal(MatrixLevels.None, SalesMath.MatrixLevelOf(0, 0, 0.5m, 0.4m));
        Assert.Equal(MatrixLevels.Low, SalesMath.MatrixLevelOf(1, 0.19m, 0.5m, 0.4m));
        Assert.Equal(MatrixLevels.Ok, SalesMath.MatrixLevelOf(1, 0.2m, 0.5m, 0.4m));
        Assert.Equal(MatrixLevels.Ok, SalesMath.MatrixLevelOf(1, 0.01m, null, 0.4m));
    }

    [Fact]
    public void Matrix_cells_are_distribution_by_region_and_average_is_distribution_over_the_whole_scope()
    {
        // Север: 10 ТТ с покупкой, 100 — во всех; Юг: 10 ТТ, 100 — в одной; Восток: 10 ТТ, 100 нет вовсе. Средняя — 11 из 30 ТТ (36,7%),
        // порог оранжевого — 40% от неё (14,7%): у Юга 10% — оранжевый.
        var current = new List<SaleLine>();
        for (var m = 0; m < 10; m++)
        {
            current.Add(L(100, 1000 + m, 10, branch: 101));
            current.Add(L(101, 2000 + m, 1, branch: 102, agent: 3));
            current.Add(L(101, 3000 + m, 1, branch: 103, agent: 3));
        }

        current.Add(L(100, 2000, 1, branch: 102, agent: 3));
        var regions = new[]
        {
            new RegionInfo(North, 101, "Север", Rm1, null, null),
            new RegionInfo(South, 102, "Юг", Rm1, null, null),
            new RegionInfo(East, 103, "Восток", Rm1, null, null),
        };

        var view = new SalesAnalytics(Data(current, regions: regions, top: ["002"])).Assortment(null, null);

        Assert.Equal(3, view.MatrixRegions.Count);
        var row = view.Matrix.Single(r => r.ProductId == 100);
        Assert.True(row.IsTop);
        Assert.Equal(11m / 30m, row.AverageDistribution); // Σ ТТ с товаром ÷ Σ АКБ регионов, а не среднее по регионам
        var cells = row.Cells.ToDictionary(c => c.RegionId);
        Assert.Equal((1m, MatrixLevels.Ok), (cells[North.ToString()].Distribution!.Value, cells[North.ToString()].Level));
        Assert.Equal((0.1m, MatrixLevels.Low), (cells[South.ToString()].Distribution!.Value, cells[South.ToString()].Level));
        Assert.Equal((0m, MatrixLevels.None), (cells[East.ToString()].Distribution!.Value, cells[East.ToString()].Level));
    }

    [Fact]
    public void Matrix_takes_twenty_products_with_the_largest_revenue_and_needs_two_regions_with_sales()
    {
        var current = Enumerable.Range(0, 25).Select(i => L(5000 + i, 10 + i, 1, revenue: 100 + i, branch: i % 2 == 0 ? 101 : 102, agent: i % 2 == 0 ? 1 : 3)).ToList();

        var view = new SalesAnalytics(Data(current)).Assortment(null, null);
        Assert.Equal(20, view.Matrix.Count);
        Assert.Equal(5024, view.Matrix[0].ProductId);

        // Продажи только на Севере: регион с продажами один — матрицы нет, таблица регионов есть (Юг — «нет данных»).
        var single = new SalesAnalytics(Data([L(100, 10, 5)])).Assortment(null, null);
        Assert.Empty(single.Matrix);
        Assert.Empty(single.MatrixRegions);
        Assert.True(single.Regions.Single(r => r.Name == "Юг").NoData);
    }

    // ================= Регионы, магазин, «Товары», ТОП =================

    [Fact]
    public void Regions_without_sales_are_kept_with_no_data_and_the_table_needs_two_regions()
    {
        var analytics = new SalesAnalytics(Data([L(100, 10, 5), L(101, 11, 2)]));

        var rows = analytics.Assortment(null, null).Regions;
        Assert.Equal(["Север", "Юг"], rows.Select(r => r.Name));
        Assert.Equal((false, 2), (rows[0].NoData, rows[0].SkuSelling));
        Assert.Equal((true, 0, 0, 0), (rows[1].NoData, rows[1].SkuSelling, rows[1].SkuNotCarried, rows[1].SkuLost));

        Assert.Empty(analytics.Assortment(null, North).Regions); // один регион — таблицы нет
        var product = analytics.Product(100, Republic)!;
        Assert.True(product.Rows.Single(r => r.Name == "Юг").NoData);
        Assert.Equal("Север", product.Rows[0].Name); // «нет данных» — в конце
    }

    [Fact]
    public void Direction_with_one_region_breaks_the_product_down_by_agents()
    {
        var other = Guid.Parse("00000000-0000-0000-0000-0000000000a2");
        var data = Data([L(100, 10, 5), L(100, 20, 2, branch: 102, agent: 3)],
            regions: [new RegionInfo(North, 101, "Север", Rm1, null, null), new RegionInfo(South, 102, "Юг", other, null, null)],
            directions: [new DirectionInfo(Rm1, "РМ 1", false, null, null, 1), new DirectionInfo(other, "РМ 2", false, null, null, 2)]);
        var analytics = new SalesAnalytics(data);

        var product = analytics.Product(100, new AssortmentScope(Rm1.ToString(), null, null, false))!;
        Assert.Equal(ProductBreakdowns.Agents, product.Breakdown);
        Assert.Empty(analytics.Assortment(Rm1.ToString(), null).Regions);
    }

    [Fact]
    public void Store_share_of_agent_is_by_weight()
    {
        // ТП 1: ТТ 10 — 30 кг на 900 сум, ТТ 11 — 70 кг на 100 сум. Доля ТТ 10 в объёме ТП — 30%, а не 90% по выручке.
        var analytics = new SalesAnalytics(Data([L(100, 10, 30, revenue: 900), L(101, 11, 70, revenue: 100)]));

        var store = analytics.Store(10, 1);

        Assert.Equal(0.3m, store.ShareOfAgent);
        Assert.True(store.Products.Single().IsTop); // 002 — в списке ТОП
        Assert.Null(analytics.Store(10, null).ShareOfAgent);
    }

    [Fact]
    public void Products_table_has_only_report_categories_and_counts_the_rest_separately()
    {
        var current = new[]
        {
            L(100, 10, 6, revenue: 600),
            L(100, 11, 2, revenue: 200),
            L(100, 11, -2, revenue: -200, isReturn: true), // ТТ 11 вернула 100 — в ТТ артикула не входит
            L(200, 11, 4, revenue: 200, type: Cake),
            L(900, 10, 0, revenue: 77, type: Bonus), // «Бокал» — тип вне отчёта
        };

        var view = new SalesAnalytics(Data(current)).Assortment(null, null);

        Assert.Equal([100L, 200L], view.Products.Select(p => p.ProductId));
        Assert.Equal(1, view.ProductsOutsideReport);
        var bamboo = view.Products[0];
        Assert.Equal((1, 0.5m, 0.75m, true), (bamboo.Akb, bamboo.Distribution!.Value, bamboo.Share!.Value, bamboo.IsTop)); // 600 из 800 сум
        Assert.True(view.Products[1].IsTop); // «318» с пробелами в настройке — тоже ТОП
    }

    [Fact]
    public void Sku_rows_carry_weight_share_and_top_from_the_server()
    {
        var card = new SalesAnalytics(Data([L(100, 10, 30), L(101, 11, 10)])).Assortment(null, null).Categories.Single(c => c.Name == "Бамбук");
        var sku = card.Skus.ToDictionary(s => s.ProductId);

        Assert.Equal((0.75m, true), (sku[100].WeightShare!.Value, sku[100].IsTop));
        Assert.Equal((0.25m, false), (sku[101].WeightShare!.Value, sku[101].IsTop));
        Assert.Null(sku[103].WeightShare); // не продавался — «—»
    }

    [Fact]
    public void Top_set_matches_linko_codes_ignoring_case_and_spaces()
    {
        var top = TopProductSet.From(["002", " T118 ", ""]);

        Assert.True(top.Configured);
        Assert.Equal(2, top.Count);
        Assert.True(top.Contains("t118"));
        Assert.True(top.Contains(" 002"));
        Assert.False(top.Contains("118"));
        Assert.False(top.Contains(null));
        Assert.False(TopProductSet.Empty.Configured);
        Assert.False(TopProductSet.Of(new SalesOptions()).Configured); // без настройки меток «ТОП» нет
    }
}
