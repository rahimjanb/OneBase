using OneBase.Application.Sales.Promo;

namespace OneBase.Sales.Tests;

/// <summary>
/// Акция строки заказа (в Linko External API её нет): сопоставление по справочнику акций — правила сверены с отчётом Linko «Промо продукты
/// заказов» (вкладка «Акции» «Полевого контроля», январь–октябрь 2026: суммы по точке и дню совпали в 99–100% ячеек).
/// </summary>
public class PromoMatchTests
{
    private static readonly string[] Regions = ["Фаргона", "Коканд", "Сырдарья", "Бухоро", "Ташкент", "Шахрисабз", "Хоразм", "Жиззах", "Андижон"];
    private static readonly DateOnly Sep10 = new(2026, 9, 10);

    private static bool InName(string name, string region) => PromoMatch.RegionMentioned(name, region, Regions);

    private static PromoInfo Discount(long id, string name, decimal? percent, IEnumerable<long>? products = null, bool all = false, string type = "promotion_branches",
        string discountType = "percent", DateOnly? begin = null, DateOnly? till = null) =>
        new(id, name, "discount", type, discountType, all ? percent : 0, begin ?? new DateOnly(2026, 9, 8), till ?? new DateOnly(2026, 10, 6), all,
            (products ?? []).ToDictionary(p => p, _ => all ? (decimal?)null : percent), new HashSet<long>());

    private static PromoLine Line(decimal percent, long product = 265, string region = "Хоразм", long order = 1, DateOnly? created = null, DateOnly? accepted = null,
        decimal price = 16_815) =>
        new(order, created ?? Sep10, accepted ?? Sep10, 1, region, 7, 9, product, 10, 0, price * 10, 1_000, percent, false);

    [Fact]
    public void Region_names_match_whatever_the_spelling()
    {
        Assert.True(InName("Акция на Могул сентябрь 2026г. 10% Фергана", "Фаргона"));
        Assert.True(InName("Сырдарье Скидка 2%", "Сырдарья"));
        Assert.True(InName("Buxoro 2 %", "Бухоро"));
        Assert.True(InName("Агент Джиззах", "Жиззах"));
        Assert.True(InName("Агент Андижан", "Андижон"));
        Assert.False(InName("Акция на Могул сентябрь 2026г. 10% \"E\"", "*"));
        Assert.Equal("фргн", PromoMatch.Skeleton("Фергана"));
    }

    /// <summary>«Могул 8% "A"» и «8% "S"» — те же товары и процент, различают их списки точек, которых в API нет: строка — обеим (группа).</summary>
    [Fact]
    public void Same_products_and_percent_make_a_group_and_own_region_wins()
    {
        var a = Discount(136, "Акция на Могул сентябрь 2026г. 8% \"A\"", 8, [265, 264]);
        var s = Discount(144, "Акция на Могул сентябрь 2026г. 8% \"S\"", 8, [265, 264]);
        var e = Discount(133, "Акция на Могул сентябрь 2026г. 10% \"E\"", 10, [265, 264]);
        var fergana = Discount(148, "Акция на Могул сентябрь 2026г. 10% Фергана", 10, [265, 264], till: new DateOnly(2026, 10, 31));
        PromoInfo[] promos = [a, s, e, fergana];

        Assert.Equal([136L, 144L], PromoMatch.Match(Line(8), promos, InName));
        Assert.Equal([133L], PromoMatch.Match(Line(10), promos, InName));
        Assert.Equal([148L], PromoMatch.Match(Line(10, region: "Фаргона"), promos, InName)); // «своя по региону»
        Assert.Empty(PromoMatch.Match(Line(3), promos, InName)); // ручная скидка
        Assert.Empty(PromoMatch.Match(Line(8, product: 45), promos, InName)); // товара нет в акции
        Assert.Empty(PromoMatch.Match(Line(8, created: new DateOnly(2026, 9, 1), accepted: new DateOnly(2026, 9, 2)), promos, InName)); // до старта
        Assert.Equal([136L, 144L], PromoMatch.Match(Line(8, created: new DateOnly(2026, 9, 6), accepted: new DateOnly(2026, 9, 8)), promos, InName)); // принят в акции
        Assert.Equal([136L, 144L], PromoMatch.Match(Line(8, created: new DateOnly(2026, 10, 8), accepted: new DateOnly(2026, 10, 9)), promos, InName)); // 2 дня после конца
        Assert.Empty(PromoMatch.Match(Line(8, created: new DateOnly(2026, 10, 9), accepted: new DateOnly(2026, 10, 10)), promos, InName));
    }

    [Fact]
    public void Regional_and_agent_promos_stay_in_their_region()
    {
        var syrdarya = Discount(100, "Сырдарье Скидка 2%", 2, all: true, till: new DateOnly(2026, 11, 30));
        var buxoro = Discount(8, "Buxoro 2 %", 2, [265, 45], type: "promotion_agents", till: new DateOnly(2026, 12, 31));
        var tashkent3 = Discount(73, "скидки 3% Ташкент", 3, all: true);
        var tashkent8 = Discount(99, "Ручная скидка Ташкент 8%", 8, all: true, discountType: "manual");
        var mogul8 = Discount(136, "Акция на Могул сентябрь 2026г. 8% \"A\"", 8, [265]);
        var bambuk = Discount(168, "Бамбук Коканд 002_10%", 10, [78]);
        PromoInfo[] promos = [syrdarya, buxoro, tashkent3, tashkent8, mogul8, bambuk];

        Assert.Equal([100L], PromoMatch.Match(Line(2, region: "Сырдарья"), promos, InName));
        Assert.Equal([8L], PromoMatch.Match(Line(2, region: "Бухоро"), promos, InName));
        Assert.Empty(PromoMatch.Match(Line(2, region: "Шахрисабз"), promos, InName)); // «Buxoro 2 %» — агентам Бухары
        Assert.Empty(PromoMatch.Match(Line(3, region: "Шахрисабз"), promos, InName)); // «на все товары» чужого региона
        Assert.Equal([73L], PromoMatch.Match(Line(3, region: "Ташкент"), promos, InName));
        Assert.Equal([136L], PromoMatch.Match(Line(8, region: "Ташкент"), promos, InName)); // товар назван явно — сильнее «на все товары»
        Assert.Equal([99L], PromoMatch.Match(Line(8, product: 45, region: "Ташкент"), promos, InName));
        Assert.Equal([168L], PromoMatch.Match(Line(10, product: 78, region: "Фаргона"), promos, InName)); // других нет — действует и в Фаргоне
    }

    /// <summary>Сложенные скидки делятся по процентам: 7% = «Могул 5%» + «Сырдарье 2%»; 12% = «Могул 10%» + ручные 2%.</summary>
    [Fact]
    public void Stacked_discounts_split_by_percent()
    {
        var mogul5 = Discount(139, "Акция на Могул сентябрь 2026г. 5% \"E\"", 5, [265]);
        var mogul10 = Discount(133, "Акция на Могул сентябрь 2026г. 10% \"E\"", 10, [265]);
        var syrdarya = Discount(100, "Сырдарье Скидка 2%", 2, all: true, till: new DateOnly(2026, 11, 30));
        var buxoro = Discount(8, "Buxoro 2 %", 2, [265], type: "promotion_agents", till: new DateOnly(2026, 12, 31));
        PromoInfo[] promos = [mogul5, mogul10, syrdarya, buxoro];

        var seven = PromoMatch.Allocate(Line(7, region: "Сырдарья"), promos, InName);
        Assert.Equal(2, seven.Count);
        Assert.Equal(([139L], 5m / 7), (seven[0].Promos.ToArray(), seven[0].Share), new TupleComparer());
        Assert.Equal(([100L], 2m / 7), (seven[1].Promos.ToArray(), seven[1].Share), new TupleComparer());
        var twelve = PromoMatch.Allocate(Line(12, region: "Хоразм"), promos, InName);
        Assert.Equal(2, twelve.Count);
        Assert.Equal([133L], twelve[0].Promos);
        Assert.Empty(twelve[1].Promos); // остаток — ручная надбавка, «Buxoro 2 %» в Хоразме не действует
        Assert.Equal(2m / 12, twelve[1].Share);
        Assert.Single(PromoMatch.Allocate(Line(4, region: "Хоразм"), promos, InName)); // 4% = ничего не складывается — ручная
    }

    /// <summary>Тот же процент и на товаре вне акций — ручная скидка агента на заказ: все строки заказа с ним ручные (заказ 582411).</summary>
    [Fact]
    public void Order_wide_manual_discount_wins_over_a_coincidence()
    {
        var mogul5 = Discount(139, "Акция на Могул сентябрь 2026г. 5% \"E\"", 5, [265, 263]);
        PromoInfo[] promos = [mogul5];
        PromoLine[] order = [Line(5, product: 265, order: 582411), Line(5, product: 263, order: 582411), Line(5, product: 45, order: 582411)];
        Assert.All(PromoMatch.AllocateAll(order, promos, InName), parts => Assert.Empty(Assert.Single(parts).Promos));

        PromoLine[] promoOnly = [Line(5, product: 265, order: 2), Line(5, product: 263, order: 2)];
        Assert.All(PromoMatch.AllocateAll(promoOnly, promos, InName), parts => Assert.Equal([139L], Assert.Single(parts).Promos));
    }

    [Fact]
    public void Bonus_lines_go_to_bonus_promos()
    {
        var fivePlusOne = new PromoInfo(132, "5+1 Август Сентябрь 2026 год", "bonus", "promotion_branches", "percent", null, new DateOnly(2026, 8, 13),
            new DateOnly(2026, 9, 15), false, new Dictionary<long, decimal?> { [247] = 0 }, new HashSet<long> { 247 });
        var bonus = new PromoLine(1, Sep10, Sep10, 1, "Хоразм", 7, 9, 247, 1, 0, 0, 47_200, 100, false);
        Assert.True(bonus.IsBonus);
        Assert.Equal([132L], PromoMatch.Match(bonus, [fivePlusOne], InName));
        Assert.Equal(5m, PromoMatch.BonusSaleFactor("5+1 Август Сентябрь 2026 год"));
        Assert.Equal(5m, PromoMatch.BonusSaleFactor("twizos 10+2 - 555"));
        Assert.Equal(0m, PromoMatch.BonusSaleFactor("257 ROLLER (акция)"));

        var partial = new PromoLine(1, Sep10, Sep10, 1, "Сырдарья", 7, 9, 265, 20, 5, 0, 1_000, 2, false);
        Assert.Equal(0.75m, partial.Kept); // возврат — на день приёмки заказа
        Assert.Equal(0m, (partial with { FullReturn = true }).Kept);
    }

    [Fact]
    public void Group_names_and_merging()
    {
        Assert.Equal("Акция на Могул Сентябрь 2026г. 8% \"A\" / \"S\"",
            PromoService.GroupName(["Акция на Могул Сентябрь 2026г. 8% \"A\"", "Акция на Могул сентябрь 2026г. 8% \"S\""]));
        Assert.Equal("Бамбук / Шоколад", PromoService.GroupName(["Бамбук", "Шоколад"]));
        var merged = PromoService.MergeGroups([[1L, 2L], [2L, 3L], [7L, 8L]]);
        Assert.Equal([1L, 2L, 3L], merged["1,2"]);
        Assert.Equal([1L, 2L, 3L], merged["2,3"]);
        Assert.Equal([7L, 8L], merged["7,8"]);
        Assert.True(PromoService.GroupId("136,144") < -1);
        Assert.Equal(PromoService.GroupId("136,144"), PromoService.GroupId("136,144"));
    }

    private sealed class TupleComparer : IEqualityComparer<(long[], decimal)>
    {
        public bool Equals((long[], decimal) x, (long[], decimal) y) => x.Item1.SequenceEqual(y.Item1) && Math.Abs(x.Item2 - y.Item2) < 0.0000001m;

        public int GetHashCode((long[], decimal) obj) => obj.Item1.Length;
    }
}
