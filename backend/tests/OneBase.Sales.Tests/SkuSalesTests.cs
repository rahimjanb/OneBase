using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Sales.SkuSales;
using OneBase.Domain.Sales;

namespace OneBase.Sales.Tests;

/// <summary>«Продажи по SKU» (DOC §9.6, DOC-filters §6): отрезок месяцев, ABC в отфильтрованном охвате, регионы, месяцы, выводы.</summary>
public class SkuSalesTests
{
    private static readonly Guid North = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid South = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid OldNorth = Guid.Parse("00000000-0000-0000-0000-000000000003");

    private static readonly SalesOptions Options = new();

    private static SalesRegion Region(Guid id, long branch, string name) => new() { Id = id, LinkoBranchId = branch, Name = name };

    /// <summary>Регионы OneBase: вторичка (101 — Север, 102 — Юг), старый филиал Севера (103), «Завод» и «К К Мерч» — не вторичка.</summary>
    private static readonly SalesRegion[] Regions =
    [
        Region(South, 102, "Юг"),
        Region(North, 101, "Север"),
        Region(OldNorth, 103, "Север (эски) 2"),
        Region(Guid.Parse("00000000-0000-0000-0000-000000000004"), 4, "Завод"),
        Region(Guid.Parse("00000000-0000-0000-0000-000000000024"), 24, "К К Мерч"),
    ];

    /// <summary>Читатель истории без БД: отдаёт заранее собранный агрегат и запоминает, о чём его спросили.</summary>
    private sealed class FakeReader(SkuSalesAggregate aggregate) : ISalesHistoryReader
    {
        public (DateOnly From, DateOnly To, IReadOnlyDictionary<long, Guid> Branches)? Asked { get; private set; }

        public Task<IReadOnlyList<MonthlyAkb>> AkbByMonthAsync(DateOnly from, DateOnly to, IReadOnlyDictionary<long, long> categoryGroups, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<MonthlyAkb>>([]);

        public Task<SkuSalesAggregate> SkuSalesAsync(DateOnly from, DateOnly to, IReadOnlyDictionary<long, Guid> branchRegions, CancellationToken ct)
        {
            Asked = (from, to, branchRegions);
            return Task.FromResult(aggregate);
        }
    }

    private static readonly SkuSalesCatalog Catalog = new(
        new Dictionary<long, ProductInfo>
        {
            [100] = new(100, "002 Бамбук 3 кг", "002", 1),
            [101] = new(101, "001 Хрустик", "001", 1),
            [200] = new(200, "318 Кеко", "318", 2),
            [900] = new(900, "Бокал", null, 16),
        },
        SalesCategories.Build(new Dictionary<string, string> { ["Бамбук"] = "1", ["Кекс"] = "2" },
            new Dictionary<long, string> { [1] = "Бамбук", [2] = "Кекс", [16] = "бонус" }),
        SkuSalesService.RegionsOf(Regions, Options).Regions,
        TopProductSet.From(["002", "318"]));

    /// <summary>
    /// Март–июль 2026. 100 (002, ТОП): Север — март 310 кг, июль 620 кг; Юг — апрель 100 кг. 101: Север — май 50 кг, Юг — июль 30 кг.
    /// 200 (318, ТОП, Кекс): Юг — июнь 200 кг; филиал вне структуры — июль 10 кг. 900 — бонус (вне категорий отчёта).
    /// </summary>
    private static readonly SkuSalesAggregate Aggregate = new(
        [
            new(100, North, 2026, 3, 310, 3100),
            new(100, North, 2026, 7, 620, 6200),
            new(100, South, 2026, 4, 100, 1000),
            new(101, North, 2026, 5, 50, 500),
            new(101, South, 2026, 7, 30, 300),
            new(200, South, 2026, 6, 200, 8000),
            new(200, null, 2026, 7, 10, 400),
            new(900, North, 2026, 3, 0, 77),
            new(100, North, 2026, 2, 999, 9990), // вне отрезка — не учитывается
        ],
        new Dictionary<long, int> { [100] = 40, [101] = 9, [200] = 25, [900] = 3 },
        [
            new(100, North, 30), new(100, South, 12), new(101, North, 5), new(101, South, 4), new(200, South, 20), new(200, null, 1),
        ]);

    private static readonly DateOnly DataThrough = new(2026, 10, 5);

    /// <summary>Как SkuSalesService: регионы и филиалы — RegionsOf, дни агрегата — Window по отчётному дню, дальше чистый расчёт страницы.</summary>
    private static async Task<SkuSalesView> BuildAsync(SkuSalesFilter filter)
    {
        var reader = new FakeReader(Aggregate);
        var regions = SkuSalesService.RegionsOf(Regions, Options);
        var query = SkuSalesQuery.Parse("2026-03", "2026-07", null, null, null, null, DataThrough).Query!;
        var (from, to) = SkuSalesService.Window(query, DataThrough)!.Value;
        var aggregate = await reader.SkuSalesAsync(from, to, regions.BranchRegions, CancellationToken.None);
        Assert.Equal((new DateOnly(2026, 3, 1), new DateOnly(2026, 7, 31)), (reader.Asked!.Value.From, reader.Asked.Value.To)); // закрытые месяцы — целиком
        Assert.Equal(North, reader.Asked.Value.Branches[103]); // старый филиал — в текущем регионе
        Assert.DoesNotContain(4L, reader.Asked.Value.Branches.Keys); // «Завод» в агрегат не идёт

        var months = SkuSalesReport.MonthsOf(2026, 3, 2026, 7, DataThrough);
        return SkuSalesReport.Build("2026-03", "2026-07", months, DataThrough, filter, Catalog, aggregate);
    }

    [Fact]
    public void Regions_are_secondary_regions_by_name_and_old_branches_map_to_the_current_region()
    {
        var andijan = Guid.NewGuid();
        var kokand = Guid.NewGuid();
        var regions = SkuSalesService.RegionsOf(
            [
                Region(kokand, 16, "Коканд"),
                Region(Guid.NewGuid(), 23, "Андижон (эски) 2"),
                Region(andijan, 26, "Андижон"),
                Region(Guid.NewGuid(), 4, "Завод"),
                Region(Guid.NewGuid(), 24, "к к мерч"),
            ],
            Options);

        Assert.Equal(["Андижон", "Коканд"], regions.Regions.Select(r => r.Name)); // без «Завода», «К К Мерч» и старого филиала; по названию
        Assert.Equal([(16L, kokand), (23L, andijan), (26L, andijan)], regions.BranchRegions.OrderBy(b => b.Key).Select(b => (b.Key, b.Value)));
    }

    [Fact]
    public void Aggregate_window_ends_at_the_report_day_and_is_empty_before_the_data()
    {
        var latest = new DateOnly(2026, 10, 5);
        (DateOnly, DateOnly)? Window(string from, string to, DateOnly? last) =>
            SkuSalesService.Window(SkuSalesQuery.Parse(from, to, null, null, null, null, latest).Query!, last);

        Assert.Equal((new DateOnly(2026, 1, 1), new DateOnly(2026, 10, 5)), Window("2026-01", "2026-10", latest)); // идущий месяц — по отчётный день
        Assert.Equal((new DateOnly(2026, 3, 1), new DateOnly(2026, 7, 31)), Window("2026-03", "2026-07", latest));
        Assert.Equal((new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 5)), Window("2026-09", "2026-12", latest));
        Assert.Null(Window("2026-11", "2026-12", latest)); // данных за отрезок ещё нет — агрегат не читается
        Assert.Null(Window("2026-01", "2026-03", null));
    }

    private static SkuSalesFilter All(Guid? region = null, string top = SkuSalesTop.All, string abc = "all", params string[] categories) =>
        new(region, top, abc, categories);

    [Theory]
    [InlineData(0.5, AbcGroups.A)]
    [InlineData(0.80, AbcGroups.A)]
    [InlineData(0.8001, AbcGroups.B)]
    [InlineData(0.95, AbcGroups.B)]
    [InlineData(0.9501, AbcGroups.C)]
    [InlineData(1.02, AbcGroups.C)]
    public void Abc_group_follows_the_cumulative_weight_share(double cumulative, string expected) =>
        Assert.Equal(expected, SalesMath.AbcOf((decimal)cumulative));

    [Fact]
    public void Query_defaults_to_the_year_of_the_last_data_and_validates_the_range_in_russian()
    {
        var latest = new DateOnly(2026, 10, 5);

        var (query, error) = SkuSalesQuery.Parse(null, null, null, null, null, null, latest);
        Assert.Null(error);
        Assert.Equal(("2026-01", "2026-10", SkuSalesTop.All, "all"), (query!.From, query.To, query.Top, query.Abc));

        var custom = SkuSalesQuery.Parse("2025-11", "2026-02", North.ToString(), "ONLY", "b", " Кекс, помадка,кекс ", latest).Query!;
        Assert.Equal(("2025-11", "2026-02", North, SkuSalesTop.Only, AbcGroups.B), (custom.From, custom.To, custom.Region!.Value, custom.Top, custom.Abc));
        Assert.Equal(["Кекс", "помадка"], custom.Categories);

        Assert.Contains("позже конца", SkuSalesQuery.Parse("2026-09", "2026-03", null, null, null, null, latest).Error);
        Assert.Contains("не длиннее 24 месяцев", SkuSalesQuery.Parse("2024-01", "2026-01", null, null, null, null, latest).Error);
        Assert.Null(SkuSalesQuery.Parse("2024-02", "2026-01", null, null, null, null, latest).Error); // ровно 24 месяца
        Assert.Contains("ГГГГ-ММ", SkuSalesQuery.Parse("2026-13", null, null, null, null, null, latest).Error);
        Assert.Contains("ГГГГ-ММ", SkuSalesQuery.Parse(null, "сентябрь", null, null, null, null, latest).Error);
        Assert.Equal("Период — до 2026-10.", SkuSalesQuery.Parse("2028-01", "2028-02", null, null, null, null, latest).Error); // дальше следующего года — опечатка
        Assert.Null(SkuSalesQuery.Parse("2027-01", "2027-03", null, null, null, null, latest).Error); // следующий год — можно
        Assert.Contains("Регион", SkuSalesQuery.Parse(null, null, "север", null, null, null, latest).Error);
        Assert.Equal((SkuSalesTop.All, "all"), (SkuSalesQuery.Parse(null, null, null, "что-то", "D", null, latest).Query!.Top, "all"));
    }

    [Fact]
    public void Months_of_the_range_count_days_with_data()
    {
        var months = SkuSalesReport.MonthsOf(2026, 8, 2026, 11, new DateOnly(2026, 10, 5));

        Assert.Equal([31, 30, 5, 0], months.Select(m => m.Days));
        Assert.Equal([false, false, true, true], months.Select(m => m.Partial));
        Assert.Equal((2026, 11), (months[^1].Year, months[^1].Month));
    }

    [Fact]
    public void View_is_built_from_the_reader_aggregate_for_the_whole_country()
    {
        var view = await_(BuildAsync(All()));

        Assert.Equal(("2026-03", "2026-07", 5), (view.From, view.To, view.Months.Count));
        Assert.Equal(["Север", "Юг", "Без региона"], view.Regions.Select(r => r.Name)); // по убыванию веса; филиал вне структуры — в конце
        Assert.Equal(1, view.OutsideReport); // бонус

        // ABC по стране: 100 — 1030 из 1320 кг (78%) — A, 200 — 210 кг (до 94%) — B, 101 — 80 кг — C.
        Assert.Equal([100L, 200L, 101L], view.Rows.Select(r => r.ProductId));
        var bamboo = view.Rows[0];
        Assert.Equal((1, 1030m, 10300m, 10m, AbcGroups.A, 40, 2, true), (bamboo.Rank, bamboo.Kg, bamboo.Revenue, bamboo.PricePerKg!.Value, bamboo.Abc, bamboo.Akb,
            bamboo.Regions, bamboo.IsTop));
        Assert.Equal(1030m / 1320m, bamboo.Share);
        Assert.Equal([AbcGroups.A, AbcGroups.B, AbcGroups.C], view.Rows.Select(r => r.Abc));
        Assert.Equal(1m, view.Rows[^1].Cumulative);
        Assert.Equal([310m, 100m, 0m, 0m, 620m], bamboo.Months);

        // Динамика: первый квартал отрезка — I (только март, 31 день), последний — III (июль): 10 → 20 кг в день.
        Assert.Equal(new SkuSalesQuarters("I кв. 2026: мар", "III кв. 2026: июл", 31, 31), view.Quarters);
        Assert.Equal((10m, 20m, 1m, false), (bamboo.FirstKgPerDay!.Value, bamboo.LastKgPerDay!.Value, bamboo.Dynamics!.Value, bamboo.IsNew));
        var cake = view.Rows.Single(r => r.ProductId == 200);
        Assert.True(cake.IsNew); // в первом квартале продаж не было
        Assert.Null(cake.Dynamics);

        Assert.Equal(1320m, view.Totals.Kg);
        Assert.Equal((3, 1, 2, 1m), (view.Totals.Skus, view.Totals.SkusA, view.Totals.Regions, view.Totals.ShareOfAll!.Value));
        Assert.Equal(new SkuSalesMonthTotal(2026, 7, 660m, 660m / 31m, false), view.MonthTotals[^1]);

        // SKU × регионы: доля SKU в весе региона; Север — 980 кг (100: 930, 101: 50).
        var matrix = view.RegionMatrix;
        Assert.Equal([980m, 330m, 10m], matrix.TotalKg);
        var cell = matrix.Rows.Single(r => r.ProductId == 100);
        Assert.Equal([930m, 100m, 0m], cell.RegionKg);
        Assert.Equal(930m / 980m, cell.RegionShare[0]);
        Assert.Equal([30, 12, 0], cell.RegionAkb);
        Assert.Null(cell.RegionShare[2]); // в регионе не продавался — «—»

        var categories = view.CategoryRegions.Rows.ToDictionary(r => r.Name);
        Assert.Equal([980m, 130m, 0m], categories["Бамбук"].Kg);
        Assert.Equal(1110m, categories["Бамбук"].TotalKg);
        Assert.Equal(200m / 330m, categories["Кекс"].Share[1]);

        var north = view.TopRegions.Single(r => r.Name == "Север");
        Assert.Equal((980m, 100L, 930m / 980m), (north.Kg, north.Items[0].ProductId, north.Items[0].Share!.Value));

        Assert.Equal(("002 Бамбук 3 кг", 1030m / 1320m, 10300m / 19500m), (view.Facts.TopName, view.Facts.TopKgShare!.Value, view.Facts.TopRevenueShare!.Value));
        Assert.Equal((1, 1, 1, 80m / 1320m), (view.Facts.SkusA, view.Facts.SkusB, view.Facts.SkusC, view.Facts.GroupCShare!.Value));
        Assert.Equal(1m, view.Facts.Top5Share);
    }

    [Fact]
    public void Filters_recompute_abc_inside_the_region_while_matrices_stay_national()
    {
        // Юг: 200 — 200 кг (61%, A), 100 — 100 кг (до 91%, B), 101 — 30 кг (C).
        var south = await_(BuildAsync(All(South)));
        Assert.Equal("Юг", south.RegionName);
        Assert.Equal([200L, 100L, 101L], south.Rows.Select(r => r.ProductId));
        Assert.Equal([AbcGroups.A, AbcGroups.B, AbcGroups.C], south.Rows.Select(r => r.Abc));
        Assert.Equal((20, 1), (south.Rows[0].Akb, south.Rows[0].Regions)); // АКБ — региона; регионов — по стране, «Без региона» не регион
        Assert.Equal([0m, 100m, 0m, 0m, 0m], south.Rows[1].Months);
        Assert.Equal(1, south.Totals.Regions);
        Assert.Equal(1320m, south.RegionMatrix.Kg); // матрица по регионам — по всей стране
        Assert.Equal(["Юг"], south.TopRegions.Select(r => r.Name));

        // Только ТОП: ABC пересчитан внутри выборки — у 100 там 1030 из 1240 кг (83%), это уже B; группы A нет вовсе.
        var topB = await_(BuildAsync(All(top: SkuSalesTop.Only, abc: AbcGroups.B)));
        Assert.Equal([100L], topB.Rows.Select(r => r.ProductId));
        Assert.Equal((2, 0, 1, 1), (topB.Facts.Skus, topB.Facts.SkusA, topB.Facts.SkusB, topB.Facts.SkusC)); // выводы — до фильтра ABC
        Assert.Empty(await_(BuildAsync(All(top: SkuSalesTop.Only, abc: AbcGroups.A))).Rows);

        // Карточки категорий — с фильтрами ТОП и ABC, без фильтра категорий (как у эталона): у «только ТОП, B» — один Бамбук;
        // «доля выборки» — вес таблицы к весу этого же набора: без фильтра категорий это 100%.
        Assert.Equal([("Бамбук", 1030m, 1)], topB.Categories.Select(c => (c.Name, c.Kg, c.Skus)));
        Assert.Equal(1m, topB.Totals.ShareOfAll);
        var groupA = await_(BuildAsync(All(abc: AbcGroups.A)));
        Assert.Equal(["Бамбук"], groupA.Categories.Select(c => c.Name)); // 200 (Кекс) — группа B, карточки нет

        // Категория «кекс» (без учёта регистра): в таблице только Кекс, карточки — все категории охвата, «Кекс» выбрана.
        var cakes = await_(BuildAsync(All(categories: "кекс")));
        Assert.Equal([200L], cakes.Rows.Select(r => r.ProductId));
        Assert.Equal(["Кекс"], cakes.SelectedCategories);
        Assert.Equal([("Бамбук", false), ("Кекс", true)], cakes.Categories.Select(c => (c.Name, c.Selected)));
        Assert.Equal(1110m / 1320m, cakes.Categories[0].KgShare);
        Assert.Equal(210m / 1320m, cakes.Totals.ShareOfAll); // доля выбранной категории в весе всех карточек

        var notTop = await_(BuildAsync(All(top: SkuSalesTop.Not)));
        Assert.Equal([101L], notTop.Rows.Select(r => r.ProductId));
        Assert.Equal(AbcGroups.C, notTop.Rows[0].Abc); // накопленная доля — вместе с самим SKU: единственный SKU выборки — 100%, это C
    }

    [Fact]
    public void Range_within_one_quarter_has_no_dynamics()
    {
        var months = SkuSalesReport.MonthsOf(2026, 7, 2026, 9, DataThrough);
        var view = SkuSalesReport.Build("2026-07", "2026-09", months, DataThrough, All(), Catalog, Aggregate);

        Assert.Null(view.Quarters);
        Assert.All(view.Rows, r => Assert.True(r.FirstKgPerDay is null && r.Dynamics is null && !r.IsNew));
        Assert.Equal([100L, 101L, 200L], view.Rows.Select(r => r.ProductId)); // июль: 620, 30, 10 кг
    }

    [Fact]
    public void Dynamics_needs_a_positive_first_quarter_and_new_means_nothing_sold_there()
    {
        // 100: I кв. −20 кг (возвраты больше продаж), III кв. +31 — не «рост со знаком», а новый; 101: 31 → 0 — падение на 100%; 200: 31 → 62 — +100%.
        var aggregate = new SkuSalesAggregate(
            [new(100, North, 2026, 3, -20, -200), new(100, North, 2026, 7, 31, 310), new(101, North, 2026, 3, 31, 310), new(200, North, 2026, 3, 31, 1200), new(200, North, 2026, 7, 62, 2400)],
            new Dictionary<long, int>(),
            []);
        var months = SkuSalesReport.MonthsOf(2026, 3, 2026, 7, DataThrough);

        var rows = SkuSalesReport.Build("2026-03", "2026-07", months, DataThrough, All(), Catalog, aggregate).Rows.ToDictionary(r => r.ProductId);

        Assert.Equal(((decimal?)null, true), (rows[100].Dynamics, rows[100].IsNew));
        Assert.Equal((-1m, false), (rows[101].Dynamics!.Value, rows[101].IsNew));
        Assert.Equal((1m, false), (rows[200].Dynamics!.Value, rows[200].IsNew));
    }

    [Fact]
    public void No_region_scope_is_empty_when_the_range_has_no_sales_outside_the_structure()
    {
        // Март–июнь: продаж филиалов вне структуры нет — охват «Без региона» пустой, а не вся страна.
        var months = SkuSalesReport.MonthsOf(2026, 3, 2026, 6, DataThrough);
        var view = SkuSalesReport.Build("2026-03", "2026-06", months, DataThrough, All(SalesAnalytics.NoRegionId), Catalog, Aggregate);

        Assert.Equal("Без региона", view.RegionName);
        Assert.Empty(view.Rows);
        Assert.Equal(0m, view.Totals.Kg);
        Assert.Contains(view.Regions, r => r.Name == "Без региона");

        // Июль: 10 кг SKU 200 вне структуры — охват есть.
        var july = SkuSalesReport.Build("2026-07", "2026-07", SkuSalesReport.MonthsOf(2026, 7, 2026, 7, DataThrough), DataThrough, All(SalesAnalytics.NoRegionId), Catalog, Aggregate);
        Assert.Equal([200L], july.Rows.Select(r => r.ProductId));
        Assert.Equal(10m, july.Totals.Kg);

        // Регион не из списка — ошибка, а не вся страна (сервис отвечает 400 раньше).
        Assert.Throws<ArgumentException>(() => SkuSalesReport.Build("2026-03", "2026-06", months, DataThrough, All(Guid.NewGuid()), Catalog, Aggregate));
    }

    private static T await_<T>(Task<T> task) => task.GetAwaiter().GetResult();
}
