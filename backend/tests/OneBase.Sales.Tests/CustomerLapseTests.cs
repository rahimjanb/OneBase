using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;

namespace OneBase.Sales.Tests;

/// <summary>«Кто не купил» по категории (SalesAnalytics.CustomerLapse): база прошлого месяца, покупка в месяце отчёта, ТП, регион, визиты.</summary>
public class CustomerLapseTests
{
    private static readonly Guid North = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid South = Guid.Parse("00000000-0000-0000-0000-000000000002");

    private const long Bamboo = 1;
    private const long Cake = 2;

    /// <summary>Строка продажи: товар 101/102 — бамбук, 200 — кекс; филиал 101 — Север, 102 — Юг.</summary>
    private static SaleLine L(int month, int day, long market, long product, decimal kg, long agent = 1, long branch = 101, bool isReturn = false) =>
        new(new DateOnly(2026, month, day), agent, market, branch, product == 200 ? Cake : Bamboo, product, kg, kg * 10,
            isReturn ? null : market * 1000 + month * 100 + day);

    private static MonthData Data(int month, IReadOnlyList<SaleLine> current, IReadOnlyList<SaleLine> previous, IReadOnlyList<VisitRecord>? visits = null) => new()
    {
        Year = 2026,
        Month = month,
        DataThrough = new DateOnly(2026, month, 20),
        Current = current,
        Previous = previous,
        Visits = visits ?? [],
        History = [],
        Plans = [],
        YearRegionPlans = [],
        Agents = new Dictionary<long, AgentInfo>
        {
            [1] = new(1, "ТП 1", true, North, false, true),
            [2] = new(2, "ТП 2", true, North, false, true),
            [3] = new(3, "ТП 3", true, South, false, true),
        },
        Regions = [new RegionInfo(North, 101, "Север", null, null, null), new RegionInfo(South, 102, "Юг", null, null, null)],
        Directions = [],
        Markets = new Dictionary<long, MarketInfo>
        {
            [10] = new(10, "Магазин 10", 1, 101),
            [11] = new(11, "Магазин 11", 1, 101),
            [13] = new(13, "Магазин 13", 3, 102),
            [17] = new(17, "Магазин 17", 2, 101),
        },
        Categories = new Dictionary<long, string> { [Bamboo] = "Бамбук", [Cake] = "Кекс" },
        CategoryMap = SalesCategories.Build(new Dictionary<string, string> { ["Бамбук"] = "1", ["Кекс"] = "2" },
            new Dictionary<long, string> { [Bamboo] = "Бамбук", [Cake] = "Кекс" }),
        Products = new Dictionary<long, ProductInfo>
        {
            [101] = new(101, "Бамбук 001", "001", Bamboo),
            [102] = new(102, "Бамбук 336", "336", Bamboo),
            [200] = new(200, "Кекс 318", "318", Cake),
        },
        ActiveSkus = new HashSet<long> { 101, 102, 200 },
        Top = TopProductSet.From([]),
        MarketAssignments = [],
        Targets = new SalesTargets(0.7m, 1_439_000m, 125m, 4m),
        Thresholds = new FlagThresholds(),
    };

    /// <summary>
    /// Август — база: 11 (80 кг бамбука), 15 (40 кг у ТП 1 и 12 кг у ТП 2), 10 (50 кг), 13 (30 кг, Юг), 12 (20 кг и возврат 20 — чистый 0),
    /// 14 — только кекс. Сентябрь: 10 и 15 (на Юге) купили бамбук, 11 — только кекс, 16 — новая точка; к 13 — два визита.
    /// </summary>
    private static SalesAnalytics September() => new(Data(9,
        [
            L(9, 3, 10, 101, 5),
            L(9, 4, 11, 200, 30),
            L(9, 5, 16, 102, 7),
            L(9, 6, 15, 101, 4, agent: 3, branch: 102),
        ],
        [
            L(8, 2, 10, 101, 50),
            L(8, 5, 11, 101, 60), L(8, 25, 11, 102, 20),
            L(8, 7, 12, 101, 20, agent: 2), L(8, 9, 12, 101, -20, agent: 2, isReturn: true),
            L(8, 8, 13, 101, 30, agent: 3, branch: 102),
            L(8, 10, 14, 200, 100),
            L(8, 11, 15, 101, 40), L(8, 12, 15, 102, 12, agent: 2),
        ],
        [
            new VisitRecord(new DateOnly(2026, 9, 3), 3, 13, VisitStatus.Done, true),
            new VisitRecord(new DateOnly(2026, 9, 10), 3, 13, VisitStatus.Done, true),
            new VisitRecord(new DateOnly(2026, 9, 17), 3, 13, VisitStatus.Pending, true),
        ]));

    private static IReadOnlySet<long> BambooGroup(SalesAnalytics analytics) =>
        new HashSet<long> { long.Parse(analytics.CategoryGlossary().Single(g => g.Name == "Бамбук").CategoryId) };

    [Fact]
    public void Base_customers_of_the_category_are_ranked_by_kg_and_marked_bought_or_not()
    {
        var analytics = September();
        var view = analytics.CustomerLapse(new CustomerLapseQuery(BambooGroup(analytics), null, null, null));

        Assert.Equal((new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)), (view.BaseFrom, view.BaseTo));
        Assert.Equal([11L, 15, 10, 13], view.Rows.Select(r => r.MarketId)); // 12 — чистый 0, 14 — только кекс
        Assert.Equal([1, 2, 3, 4], view.Rows.Select(r => r.Rank));
        Assert.Equal((4, 212m, 2, 2, 110m), (view.BaseCustomers, view.BaseKg, view.Bought, view.NotBought, view.NotBoughtBaseKg));
        Assert.Equal(1, view.NotBoughtActive); // 11 берёт кекс
        Assert.Equal(1, view.NewCustomers); // 16
        Assert.False(view.NoData);

        var lost = view.Rows.Single(r => r.MarketId == 11);
        Assert.False(lost.Bought);
        Assert.Equal(("Магазин 11", 1L, "ТП 1", "Север"), (lost.Name, lost.AgentId!.Value, lost.Agent, lost.Region));
        Assert.Equal((80m, new DateOnly(2026, 8, 25), 300m), (lost.BaseKg, lost.LastPurchase!.Value, lost.OtherNowRevenue));

        var south = view.Rows.Single(r => r.MarketId == 13);
        Assert.Equal(("Юг", 2, new DateOnly(2026, 9, 10)), (south.Region, south.VisitsNow, south.LastVisit!.Value)); // запланированный не считается

        var bought = view.Rows.Single(r => r.MarketId == 15);
        Assert.True(bought.Bought); // купил на Юге — тоже купил
        Assert.Equal((1L, 4m), (bought.AgentId!.Value, bought.NowKg)); // ТП — кто продал больше всех в базе
    }

    [Fact]
    public void Region_limits_the_base_agent_and_products_narrow_it()
    {
        var analytics = September();
        var group = BambooGroup(analytics);

        var north = analytics.CustomerLapse(new CustomerLapseQuery(group, null, North, null));
        Assert.Equal([11L, 15, 10], north.Rows.Select(r => r.MarketId));
        Assert.Equal([11L], north.Rows.Where(r => !r.Bought).Select(r => r.MarketId)); // 15 купил на Юге — засчитано

        var agent3 = analytics.CustomerLapse(new CustomerLapseQuery(group, null, null, 3));
        Assert.Equal([13L], agent3.Rows.Select(r => r.MarketId));

        var sku = analytics.CustomerLapse(new CustomerLapseQuery(null, new HashSet<long> { 102 }, null, null));
        Assert.Equal([11L, 15], sku.Rows.Select(r => r.MarketId)); // 336 брали 11 и 15
        Assert.Equal([11L, 15], sku.Rows.Where(r => !r.Bought).Select(r => r.MarketId)); // в сентябре 336 не взял никто
        Assert.Equal(new HashSet<long> { 101, 102 }, analytics.ProductsIn(group));
        Assert.Equal(new HashSet<long> { 200 }, analytics.ProductsOfTypes(["кекс"]));
    }

    [Fact]
    public void Earlier_months_extend_the_base()
    {
        var analytics = September();
        var july = new SalesAnalytics(Data(7, [L(7, 15, 17, 101, 9, agent: 2)], [L(6, 15, 10, 101, 3)]));
        var group = BambooGroup(analytics);

        var two = analytics.CustomerLapse(new CustomerLapseQuery(group, null, null, null), july, 1);
        Assert.Equal(new DateOnly(2026, 7, 1), two.BaseFrom);
        Assert.Contains(two.Rows, r => r.MarketId == 17 && !r.Bought && r.BaseMonths == 1 && r.Agent == "ТП 2");

        var three = analytics.CustomerLapse(new CustomerLapseQuery(group, null, null, null), july, 2);
        Assert.Equal(new DateOnly(2026, 6, 1), three.BaseFrom);
        Assert.Equal((53m, 2), three.Rows.Where(r => r.MarketId == 10).Select(r => (r.BaseKg, r.BaseMonths)).Single());
    }

    [Fact]
    public void Month_without_purchases_is_no_data()
    {
        var analytics = new SalesAnalytics(Data(9, [], [L(8, 2, 10, 101, 50)]));
        var view = analytics.CustomerLapse(new CustomerLapseQuery(BambooGroup(analytics), null, null, null));
        Assert.True(view.NoData);
        Assert.Equal(1, view.NotBought);
    }
}
