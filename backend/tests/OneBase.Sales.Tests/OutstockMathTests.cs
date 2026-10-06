using OneBase.Application.Sales.Stock;

namespace OneBase.Sales.Tests;

public class OutstockMathTests
{
    [Fact]
    public void Morning_stock_is_rebuilt_backwards_from_snapshot()
    {
        // Вечером 3-го дня (снимок) — 10 кг. 3-й день: продали 4, привезли 0 → утром 14. 2-й: продали 6, привезли 20 → утром 0.
        // 1-й: продали 5, привезли 0 → утром 5.
        var (morning, negative) = OutstockMath.Reconstruct(10, [5, 6, 4], [0, 20, 0]);

        Assert.Equal([5m, 0m, 14m], morning);
        Assert.All(negative, n => Assert.False(n));
    }

    [Fact]
    public void Negative_stock_is_shown_as_zero_but_the_walk_keeps_the_minus()
    {
        // Привезли больше, чем было с продажами: даты разошлись — утро показывается нулём, день помечен,
        // а ход назад продолжается от −28: предыдущий день тоже в минусе (−26), а не «2 кг».
        var (morning, negative) = OutstockMath.Reconstruct(1, [2, 1], [0, 30]);

        Assert.Equal([0m, 0m], morning);
        Assert.Equal([true, true], negative);
    }

    [Fact]
    public void Carried_minus_keeps_earlier_days_at_zero()
    {
        // Снимок 0, 3-й день привезли 10 без продаж → −10; 2-й день продали 5 → −5; 1-й → −5. Все три дня в нуле —
        // сброс в 0 показал бы на 2-й день «5 кг на складе».
        var (morning, negative) = OutstockMath.Reconstruct(0, [0, 5, 0], [0, 0, 10]);

        Assert.Equal([0m, 0m, 0m], morning);
        Assert.Equal([true, true, true], negative);
        Assert.All(morning, m => Assert.False(OutstockMath.InStock(m)));
    }

    [Fact]
    public void Half_kilogram_is_not_in_stock()
    {
        Assert.False(OutstockMath.InStock(0.5m));
        Assert.True(OutstockMath.InStock(0.51m));
    }

    [Fact]
    public void Receipt_counts_only_when_accepted_by_the_snapshot_moment()
    {
        var snapshot = new DateTime(2026, 10, 3, 12, 52, 9);

        Assert.True(OutstockMath.ReceiptCounts(new DateTime(2026, 10, 3, 9, 0, 0), snapshot));
        Assert.True(OutstockMath.ReceiptCounts(snapshot, snapshot));
        Assert.False(OutstockMath.ReceiptCounts(new DateTime(2026, 10, 3, 18, 10, 0), snapshot)); // принято после снимка
        Assert.False(OutstockMath.ReceiptCounts(null, snapshot)); // выдано, но не принято (перемещение #2306 Завод → Сырдарья)
    }

    [Fact]
    public void Whose_loss_depends_on_factory_stock_that_morning()
    {
        Assert.Equal(LossOwner.Dealer, OutstockMath.Blame(0.51m)); // на заводе товар был — недовоз
        Assert.Equal(LossOwner.Factory, OutstockMath.Blame(0.5m)); // на заводе ноль
        Assert.Equal(LossOwner.Factory, OutstockMath.Blame(0m));
        Assert.Equal(LossOwner.Unknown, OutstockMath.Blame(null)); // данных склада завода по товару нет
    }

    [Fact]
    public void Lost_kg_is_zero_days_times_average_daily_sales()
    {
        // Термез, KEKO декор: 10 485 кг за 30 дней, 4 дня в нуле → 1 398 кг.
        Assert.Equal(1398m, Math.Round(OutstockMath.LostKg(4, 10485, 30)));
        Assert.Equal(0m, OutstockMath.LostKg(0, 10485, 30));
    }

    [Fact]
    public void Core_is_the_top_pairs_that_make_eighty_percent()
    {
        // 50 + 30 = 80 из 100 → две пары; остальное — мелочь.
        Assert.Equal(2, OutstockMath.CoreCount([50m, 30m, 10m, 5m, 5m]));
        Assert.Equal(0, OutstockMath.CoreCount([0m, 0m]));
        Assert.Equal(1, OutstockMath.CoreCount([100m]));
    }

    [Fact]
    public void Chronic_is_half_of_the_period_or_more()
    {
        Assert.True(OutstockMath.IsChronic(15, 30));
        Assert.False(OutstockMath.IsChronic(14, 30));
        Assert.False(OutstockMath.IsChronic(0, 0));
    }

    [Fact]
    public void Every_nth_kilogram_is_sold_over_lost_but_at_least_two()
    {
        Assert.Equal(10, OutstockMath.EveryNthKg(1000, 100));
        Assert.Equal(2, OutstockMath.EveryNthKg(100, 90)); // 1,1 → не меньше 2
        Assert.Null(OutstockMath.EveryNthKg(100, 0));
    }

    // ---- Область страницы (Compose) на парах, собранных вручную ----

    private static OutstockPair Pair(string region, string product, string category, decimal soldKg, int zero, int dealer, int factory, int unknown, decimal lostSum, bool top = true)
    {
        var lostKg = OutstockMath.LostKg(zero, soldKg, 30);
        return new OutstockPair(region, region, product.GetHashCode(), product, product, category, top, soldKg, soldKg * 10, soldKg / 30, 10, zero, dealer, factory, unknown, 0,
            lostKg, lostSum, zero == 0 ? 0 : lostSum * dealer / zero, zero == 0 ? 0 : lostSum * factory / zero, zero == 0 ? 0 : lostSum * unknown / zero,
            false, OutstockMath.IsChronic(zero, 30), 0, new string('1', 30 - zero) + new string('0', zero), new string('0', 30));
    }

    private static OutstockBase Base(params OutstockPair[] pairs) =>
        new(2026, 9, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), 30, 30, new DateOnly(2026, 10, 6), null, true, true, "ТОП — 59 кодов",
            pairs.Select(p => p.RegionId).Distinct().Select(r => new OutstockRegionRef(r, r, $"дилер {r}")).ToList(), pairs);

    [Fact]
    public void Insights_pick_the_worst_region_by_share_only_among_regions_with_real_sales()
    {
        // Бухоро теряет больше всех денег; Денов — худший по доле среди продавших > 1 000 кг; у крошечного Олмалика доля выше, но он не в счёт.
        var b = Base(
            Pair("Бухоро", "A", "Кекс", 5000, 3, 3, 0, 0, 900_000),
            Pair("Денов", "A", "Кекс", 2000, 6, 4, 1, 1, 500_000),
            Pair("Олмалик", "B", "Помадка", 300, 15, 15, 0, 0, 100_000));

        var view = OutstockService.Compose(b, new OutstockQuery());
        var i = view.Insights;

        Assert.Equal("Бухоро", i.TopRegion!.Name);
        Assert.Equal("дилер Бухоро", i.TopRegion.Dealer);
        Assert.Equal("Денов", i.WorstRegion!.Name);
        Assert.Equal(OutstockMath.EveryNthKg(2000, OutstockMath.LostKg(6, 2000, 30)), i.WorstRegion.EveryNthKg);
        Assert.Equal("Кекс", i.TopCategory); // карточек две — категория с самой большой потерей
        Assert.Equal("A", i.TopProduct!.Name);
        Assert.Equal(2, i.TopProduct.Regions);
        // Доля дней «потеря дилера» — среди всех дней в нуле, включая дни без данных по заводу: 22 из 24.
        Assert.Equal(22m / 24, i.DealerDaysShare);
        Assert.Equal(1, view.Totals.UnknownDays);
        Assert.Equal(1, view.Totals.FactoryDays);
    }

    [Fact]
    public void Calendar_region_does_not_narrow_the_totals_and_all_categories_with_losses_get_cards()
    {
        var b = Base(
            Pair("Бухоро", "A", "Кекс", 5000, 3, 3, 0, 0, 900_000),
            Pair("Денов", "B", "Песочный", 50, 1, 1, 0, 0, 1_000), // 0,1% потерь — карточка всё равно есть
            Pair("Денов", "C", "Помадка", 500, 0, 0, 0, 0, 0)); // без потерь — карточки нет

        var view = OutstockService.Compose(b, new OutstockQuery(CalendarRegionId: "Денов"));

        Assert.Equal(901_000m, view.Totals.LostSum); // итоги по всей области
        Assert.Equal(["Кекс", "Песочный"], view.Categories.Select(c => c.Name));
        Assert.Equal("Денов", view.CalendarRegionId);
        Assert.Equal(["B"], view.Calendar.Select(p => p.Product)); // календарь — только пары региона с потерями
        Assert.Null(view.RegionId);
        Assert.False(view.CanReset);
    }

    [Fact]
    public void Reset_is_offered_when_scope_is_not_top_or_categories_are_chosen()
    {
        var b = Base(Pair("Бухоро", "A", "Кекс", 5000, 3, 3, 0, 0, 900_000), Pair("Бухоро", "Z", "Бонус", 100, 2, 2, 0, 0, 5_000, top: false));

        Assert.True(OutstockService.Compose(b, new OutstockQuery(Scope: OutstockScope.All)).CanReset);
        Assert.True(OutstockService.Compose(b, new OutstockQuery(Categories: ["Кекс"])).CanReset);
        Assert.False(OutstockService.Compose(b, new OutstockQuery(Scope: OutstockScope.Top)).CanReset);
        Assert.Equal(900_000m, OutstockService.Compose(b, new OutstockQuery()).Totals.LostSum); // по умолчанию только ТОП
        Assert.Equal(905_000m, OutstockService.Compose(b, new OutstockQuery(Scope: OutstockScope.All)).Totals.LostSum);
    }
}
