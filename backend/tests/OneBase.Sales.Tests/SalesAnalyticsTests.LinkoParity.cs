using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;

namespace OneBase.Sales.Tests;

/// <summary>
/// ТЗ «что добить до паритета с «Полевым контролем»» (18.11.2026) — то, что считается по данным Linko: план по категориям без двойного
/// «Тотала», план АКБ ТП, «Из чего складывается объём» на регионе, строки из одних нулей в списках, снятые категории в «Ассортименте».
/// </summary>
public partial class SalesAnalyticsTests
{
    private static readonly SalesCategories PlanMap = SalesCategories.Build(PlanReport, PlanTypes);
    private static readonly long Pomadka = PlanMap.GroupByName("Помадка")!.Value;
    private static readonly long Chocolate = PlanMap.GroupByName("Шоколад")!.Value;
    private static readonly long Bamboo = PlanMap.GroupByName("Бамбук")!.Value;

    /// <summary>Категории по названию показателя Linko — как SalesOptions.PlanCategoriesOf: «Могуль» — Помадка.</summary>
    private static IReadOnlyList<long> PlanGroups(string? name) =>
        new (string Word, long Group)[] { ("Могуль", Pomadka), ("Шоколад", Chocolate), ("Бамбук", Bamboo) }
            .Where(x => name!.Contains(x.Word, StringComparison.OrdinalIgnoreCase)).Select(x => x.Group).ToList();

    [Fact]
    public void Linko_total_plan_keeps_its_category_parts_and_only_the_rest_goes_without_breakdown()
    {
        // «Тотал» 1000 включает Могуль 200 и Шоколад 50: строки — 200, 50 и остаток 750 «кроме Помадки и Шоколада»; сумма — 1000, а не 1250.
        var rows = SalesPlans.LinkoCategoryPlans(1, 2026, 9,
            [("Сентябрь Тотал Вес 6 млн", 1000m), ("Сентябрь Могуль Вес 6 млн", 200m), ("Сентябрь Шоколад", 50m)], PlanGroups);

        Assert.Equal(1000m, rows.Sum(r => r.PlanKg));
        Assert.Equal(200m, rows.Single(r => r.Groups.SequenceEqual([Pomadka])).PlanKg);
        Assert.Equal(50m, rows.Single(r => r.Groups.SequenceEqual([Chocolate])).PlanKg);
        var rest = rows.Single(r => r.Except is not null);
        Assert.Equal((750m, 0), (rest.PlanKg, rest.Groups.Count));
        Assert.Equal(new[] { Pomadka, Chocolate }.Order(), rest.Except!);

        // Один «Тотал» — план без разбивки, как раньше; без «Тотала» — каждый показатель своей строкой; части больше «Тотала» — без остатка.
        var total = Assert.Single(SalesPlans.LinkoCategoryPlans(1, 2026, 9, [("Сентябрь Тотал Ташкент", 1000m)], PlanGroups));
        Assert.Equal((0, (IReadOnlyList<long>?)null, 1000m), (total.Groups.Count, total.Except, total.PlanKg));
        Assert.Equal(250m, SalesPlans.LinkoCategoryPlans(1, 2026, 9, [("Сентябрь Могуль", 200m), ("Сентябрь Шоколад", 50m)], PlanGroups)
            .Where(r => r.Except is null).Sum(r => r.PlanKg));
        Assert.DoesNotContain(SalesPlans.LinkoCategoryPlans(1, 2026, 9, [("Тотал", 100m), ("Могуль", 120m)], PlanGroups), r => r.Except is not null);
    }

    [Fact]
    public void Category_plan_block_shows_the_rest_of_the_linko_total_once_and_its_fact_without_the_planned_categories()
    {
        // ТП 1: Помадка 60, Шоколад 30, Бамбук 20; в Linko — «Тотал» 300 и «Могуль» 100.
        var data = Data(
            current: [Sku(100, 1, market: 10, kg: 60), Sku(102, 3, market: 12, kg: 30), Sku(103, 4, market: 13, kg: 20)],
            categories: PlanTypes,
            reportCategories: PlanReport,
            categoryPlans: SalesPlans.LinkoCategoryPlans(1, 2026, 9, [("Сентябрь Тотал Вес", 300m), ("Сентябрь Могуль Вес", 100m)], PlanGroups));

        var republic = new SalesAnalytics(data).Republic(Sep1, new DateOnly(2026, 9, 10));

        var pomadka = republic.CategoryPlans!.Single(r => r.Name == "Помадка");
        Assert.Equal((100m, 60m), (pomadka.PlanKg!.Value, pomadka.FactKg));
        var rest = republic.CategoryPlans!.Single(r => r.Name == "Остальные категории (без разбивки)");
        Assert.Equal((200m, 50m, (long?)null), (rest.PlanKg!.Value, rest.FactKg, rest.CategoryId)); // Шоколад 30 + Бамбук 20, без Помадки
        Assert.Equal((300m, 110m), (republic.CategoryPlanTotal!.PlanKg!.Value, republic.CategoryPlanTotal.FactKg)); // итог = «Тотал»
        Assert.DoesNotContain(republic.CategoryPlans!, r => r.Name == "Без разбивки по категориям");
    }

    [Fact]
    public void Linko_akb_indicators_give_the_agent_akb_plan_and_category_akb_plans()
    {
        var rows = SalesPlans.LinkoAkbPlans(
        [
            (1, "Октябрь АКБ Ташкент", 100m),
            (1, "Октябрь АКБ Ташкент | 08.10.2026", 120m), // новая версия того же плана — берётся больший, не сумма
            (1, "Сентябрь АКБ Шоколад Ташкент", 60m),
            (2, "АКБ Могуль + Шоколад", 30m), // на две категории — не берётся
            (2, "АКБ 0", 0m),
        ], PlanGroups);

        Assert.Equal([new AkbPlanRow(1, null, 120m), new AkbPlanRow(1, Chocolate, 60m)], rows);
    }

    [Fact]
    public void Akb_plan_from_linko_reaches_republic_region_agent_cards_and_the_team()
    {
        // Север: ТП 1 — ТТ 10, ТП 2 — ТТ 11, без агента — ТТ 12; Юг: ТП 3 — ТТ 13. Планы АКБ: 4, 6, 5.
        var analytics = new SalesAnalytics(Data(akbPlans: [new AkbPlanRow(1, null, 4), new AkbPlanRow(2, null, 6), new AkbPlanRow(3, null, 5)]));

        var republic = analytics.Republic(Sep1, new DateOnly(2026, 9, 10)).Kpi.AkbPlan!;
        Assert.Equal((15m, 3, 3), (republic.Plan, republic.Fact, republic.Agents)); // ТТ 12 без агента — не ТП с планом
        Assert.Equal(0.2m, republic.Execution);
        Assert.True(republic.AllAgents >= republic.Agents);

        var north = analytics.Region(North, Sep1, new DateOnly(2026, 9, 10), "kg", null);
        Assert.Equal((10m, 2, 0.2m), (north.Kpi.AkbPlan!.Plan, north.Kpi.AkbPlan.Fact, north.Kpi.AkbPlan.Execution!.Value));
        var team = north.Team.Single(t => t.AgentId == 1);
        Assert.Equal((4m, 0.25m), (team.AkbPlan!.Value, team.AkbExecution!.Value));
        Assert.Equal(10m, analytics.Republic(Sep1, new DateOnly(2026, 9, 10)).Regions.Single(r => r.Name == "Север").AkbPlan!.Plan);

        var card = analytics.CachedAgents().Agents.Single(a => a.AgentId == 1);
        Assert.Equal((4m, 1, 0.25m), (card.AkbPlan!.Plan, card.AkbPlan.Fact, card.AkbPlan.Execution!.Value));
        Assert.Equal(4m, analytics.Agent(1).AkbPlan!.Plan);
        Assert.Equal(6m, analytics.Agent(2).AkbPlan!.Plan);

        // Плана АКБ в Linko нет — плитки нет, «АКБ на агента» по-прежнему сравнивается с целью.
        var without = new SalesAnalytics(Data()).Republic(Sep1, new DateOnly(2026, 9, 10)).Kpi;
        Assert.Null(without.AkbPlan);
        Assert.Equal(125m, without.AkbPerAgent.Target);
    }

    [Fact]
    public void Region_volume_table_shows_category_share_plan_fact_akb_and_coverage_with_the_region_total()
    {
        // Север (план РОП): Помадка 60 + 30 + 10 без агента, план 150; Шоколад 30, план 40; Песочный 20 без плана; Бамбук — план 30 без продаж.
        var analytics = new SalesAnalytics(RegionCategoryPlanData(new DateOnly(2026, 9, 20),
            [new AkbPlanRow(1, null, 10), new AkbPlanRow(1, Chocolate, 7)]));

        var volume = analytics.Region(North, Sep1, new DateOnly(2026, 9, 20), "kg", null).Volume!;

        Assert.Equal(["Помадка", "Шоколад", "Песочный", "Бамбук"], volume.Rows.Select(r => r.Name));
        var pomadka = volume.Rows[0];
        Assert.Equal((100m, 150m, 3, 0.6m), (pomadka.FactKg, pomadka.PlanKg!.Value, pomadka.Akb, pomadka.AkbCoverage!.Value)); // ТТ 10, 11, 14 из 5
        Assert.Equal(100m / 150m, pomadka.Share);
        Assert.Equal(100m / 150m, pomadka.Execution);
        Assert.Equal(100m / 20 * 30 / 150m, pomadka.ForecastExecution); // 100 кг за 20 дн. → 150 за 30
        Assert.Equal((7m, 1), (volume.Rows[1].AkbPlan!.Value, volume.Rows[1].Akb));
        Assert.Null(volume.Rows[2].PlanKg);
        Assert.Equal((0m, 30m), (volume.Rows[3].FactKg, volume.Rows[3].PlanKg!.Value));

        Assert.Equal((150m, 220m, 5, 10m), (volume.Total.FactKg, volume.Total.PlanKg!.Value, volume.Total.Akb, volume.Total.AkbPlan!.Value));
        Assert.Equal(1m, volume.Total.AkbCoverage);
        Assert.Empty(volume.PlanGroups);
        Assert.Equal(PlanSources.Rop, volume.PlanSource);
    }

    [Fact]
    public void Region_volume_table_lists_linko_plans_on_several_categories_separately()
    {
        var data = Data(
            current: [Sku(100, 1, market: 10, kg: 60), Sku(102, 3, market: 12, kg: 30), Sku(103, 4, market: 13, kg: 20)],
            categories: PlanTypes,
            reportCategories: PlanReport,
            categoryPlans: SalesPlans.LinkoCategoryPlans(1, 2026, 9, [("Сентябрь Тотал Вес", 300m), ("Сентябрь Могуль Вес", 100m)], PlanGroups));

        var volume = new SalesAnalytics(data).Region(North, Sep1, new DateOnly(2026, 9, 10), "kg", null).Volume!;

        Assert.Equal(100m, volume.Rows.Single(r => r.Name == "Помадка").PlanKg);
        Assert.Null(volume.Rows.Single(r => r.Name == "Бамбук").PlanKg); // часть остатка «Тотала» — плана категории нет
        var rest = Assert.Single(volume.PlanGroups);
        Assert.Equal(("Остальные категории (без разбивки)", 200m, 50m), (rest.Name, rest.PlanKg!.Value, rest.FactKg));
        Assert.Equal(PlanSources.Linko, volume.PlanSource);
    }

    [Fact]
    public void Region_rows_of_zeros_are_not_listed()
    {
        // «Сети / КА» в идущем месяце и филиал без продаж, ТП и плана — строки из одних нулей: в «Все регионы» и «к прошлому месяцу» их нет.
        var empty = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var analytics = new SalesAnalytics(Data(regions:
        [
            new RegionInfo(North, 101, "Север", Rm1, null, null), new RegionInfo(South, 102, "Юг", Rm1, null, null),
            new RegionInfo(empty, 103, "Пусто", Rm1, null, null),
        ]));

        var republic = analytics.Republic(Sep1, new DateOnly(2026, 9, 10));

        Assert.DoesNotContain(republic.Regions, r => r.Name == "Пусто");
        Assert.DoesNotContain(republic.SameDays, r => r.Name == "Пусто");
        Assert.Contains(republic.Regions, r => r.Name == "Юг");
    }

    [Fact]
    public void Assortment_hides_a_category_nobody_sells_this_or_last_month_and_nobody_plans()
    {
        // Песочный есть в ассортименте года (SKU 105), но в этом и прошлом месяце его не продавали нигде и плана на него нет — снят.
        var products = new Dictionary<long, ProductInfo>
        {
            [100] = new(100, "Помадка", "100", 1), [104] = new(104, "Бамбук", "104", 4), [105] = new(105, "Песочный", "105", 5),
        };
        MonthData Of(IReadOnlyList<CategoryPlanRow> plans) => Data(
            current: [Sku(100, 1, market: 10, kg: 60)],
            previous: [Sku(104, 4, market: 10, kg: 5, month: 8)],
            categories: PlanTypes,
            reportCategories: PlanReport,
            products: products,
            activeSkus: new HashSet<long> { 100, 104, 105 },
            categoryPlans: plans);

        var names = new SalesAnalytics(Of([])).Assortment(null, null).Categories.Select(c => c.Name).ToList();
        Assert.Contains("Бамбук", names); // продавали в прошлом месяце — карточка остаётся
        Assert.DoesNotContain("Песочный", names);

        // План месяца на категорию есть — карточка «продаётся 0 из M» остаётся: это пробел, а не снятая категория.
        var planned = new SalesAnalytics(Of([new CategoryPlanRow(1, 2026, 9, [PlanMap.GroupByName("Песочный")!.Value], 50)]))
            .Assortment(null, null).Categories.Select(c => c.Name);
        Assert.Contains("Песочный", planned);
    }

    [Fact]
    public void Limited_users_get_data_quality_without_republic_wide_counters()
    {
        var quality = new DataQualityView(3, 2, 10m, 1, 5m, [new UncategorizedType("16", "бонус", 2m, 20m, 1)], 4,
            [new CurrencyTotal("USD", 100m, 1)]);

        var own = quality.ScopeOnly();

        Assert.Equal((0, 0, 0m, 0, 0m, 0), (own.DeliveredWithoutAcceptance, own.ZeroHeaderReturns, own.ZeroHeaderReturnsKg, own.ReturnsWithoutLines,
            own.ReturnsWithoutLinesHeaderKg, own.AcceptedInFuture));
        Assert.Null(own.OtherCurrency);
        Assert.Single(own.Uncategorized); // типы вне категорий считаются по охвату — остаются
    }
}
