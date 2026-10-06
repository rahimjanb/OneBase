using System.Text.Json;
using OneBase.Application.Sales.Stock;
using OneBase.Infrastructure.Linko;

namespace OneBase.Sales.Tests;

public class StockMathTests
{
    [Fact]
    public void Balance_string_of_zeros_is_zero_not_truthy()
    {
        // Linko отдаёт balance строкой: непустая строка «истинна», но это ноль.
        var dto = JsonSerializer.Deserialize<LinkoProductBalanceDto>(
            """{"product":{"id":4021},"stock":{"id":8},"balance":"0.000000000","tm":"1741091650.749378"}""", LinkoClient.Json)!;

        Assert.Equal(0m, dto.Balance);
        Assert.Equal(0m, StockMath.ParseQuantity("0.000000000"));
        Assert.Equal(13m, StockMath.ParseQuantity("13.000000000"));
        Assert.Null(StockMath.ParseQuantity("abc"));
    }

    [Fact]
    public void Pieces_convert_to_kg_by_piece_weight_and_then_to_boxes_by_box_weight()
    {
        // Помадка: 0,5 кг штука, 8 штук в коробке 4 кг. Остаток 80 штук = 40 кг = 10 коробок.
        var unitKg = StockMath.UnitKg(totalWeight: 250m, amount: 500m); // продали 500 штук на 250 кг
        var kg = StockMath.Kg(80, unitKg);

        Assert.Equal(0.5m, unitKg);
        Assert.Equal(40m, kg);
        Assert.Equal(10m, StockMath.Boxes(kg, 4m));
        Assert.NotEqual(80m * 4m, kg); // неверно: штуки × вес коробки завысили бы остаток в 8 раз
    }

    [Theory]
    [InlineData("038 Шоколад LILYA бежовый, молоко (4-шт по 0,5-кг) 2-кг", 2.0, 4, 0.5)]
    [InlineData("006 Хрустящие подушечки «Hrustik», (24шт по 40-гр ) 0,96-кг", 0.96, 24, 0.04)]
    [InlineData("254 Печенье «Frully», (10-шт по 300гр), 3-кг", 3.0, 10, 0.3)]
    [InlineData("342 Печенье «Frully», (10-шт по 180гр)", 1.8, 10, 0.18)]
    [InlineData("001 Кукурузные подушечки ROHAT XRUSTIK, 3-кг", 3.0, null, null)]
    [InlineData("059 Печенье с чёрной глазурью, SVETOCHKA, 2,5-кг", 2.5, null, null)]
    [InlineData("004 Глазированные конфеты «Kartofelki» , 3 кг", 3.0, null, null)]
    // «N г × M шт»: граммы и штук в коробке — коробка = N × M г.
    [InlineData("518-Kекс глазированный Nillo Brauni (180гр 12шт)", 2.16, 12, 0.18)]
    [InlineData("600 «Twizos» gazaklari Dengiz Tuzli o'rama tayoqchalar (200г / 20шт)", 4.0, 20, 0.2)]
    [InlineData("494 Хрустяшие палочки \"Bamboo\" со вкусом молока ( 400г ) 10 шт.", 4.0, 10, 0.4)]
    [InlineData("248 Хрустящие палочки \"Stix bamboo\" 250гр × 9шт", 2.25, 9, 0.25)]
    [InlineData("203 Песочное печенье уп.коррекс, RIO, (500г 5шт) 2,5-кг", 2.5, 5, 0.5)]
    [InlineData("315 Nillo (34гр 30шт) шоколадной 1кг", 1.0, 30, 0.034)]
    public void Pack_is_parsed_from_the_product_name(string name, double boxKg, int? pieces, double? pieceKg)
    {
        var pack = StockMath.ParsePack(name)!;

        Assert.Equal((decimal)boxKg, pack.BoxKg);
        Assert.Equal(pieces, pack.PiecesPerBox);
        Assert.Equal(pieceKg is null ? null : (decimal)pieceKg.Value, pack.PieceKg);
    }

    [Theory]
    [InlineData("050 Печенье сахарное глазированное “Candy Gold”")]
    [InlineData("492 Кекс Nillo Panda со вкусом банана 33г (шоубокс 15шт)")] // между граммами и штуками слово — это не «N г × M шт»
    public void Name_without_usable_weight_gives_no_pack(string name) => Assert.Null(StockMath.ParsePack(name));

    [Theory]
    [InlineData(4.0, 0.5, true)] // 8 штук
    [InlineData(3.0, 3.0, true)] // коробка — сама единица учёта
    [InlineData(0.96, 0.04, true)]
    [InlineData(3.0, 0.42, false)] // 7,14 штуки — название не про ту единицу
    [InlineData(2.0, 3.0, false)] // коробка легче штуки
    public void Box_from_name_is_accepted_only_if_it_holds_a_whole_number_of_units(double boxKg, double unitKg, bool ok) =>
        Assert.Equal(ok, StockMath.BoxConsistent((decimal)boxKg, (decimal)unitKg));

    [Fact]
    public void Box_rule_by_weight_goods_accept_any_box_piece_goods_need_whole_units()
    {
        // Весовой товар (единица — 1 кг): «KEKO декор 2,5 кг» — коробка 2,5 кг, хотя 2,5 не целое.
        Assert.Equal(2.5m, StockMath.BoxWeight(StockMath.ParsePack("318 KEKO декор 2,5 кг"), 1m).BoxKg);
        // Штучный: 2 кг = 4 × 0,5 — коробка подтверждена.
        Assert.Equal(2m, StockMath.BoxWeight(StockMath.ParsePack("038 Шоколад (4-шт по 0,5-кг) 2-кг"), 0.5m).BoxKg);
        // Штучный с несходящейся коробкой — коробок нет.
        Assert.Null(StockMath.BoxWeight(StockMath.ParsePack("099 Конфеты, 3-кг"), 0.42m).BoxKg);
        // Без фасовки в названии или без веса единицы — коробок нет.
        Assert.Null(StockMath.BoxWeight(null, 0.5m).BoxKg);
        Assert.Null(StockMath.BoxWeight(StockMath.ParsePack("001 ROHAT XRUSTIK, 3-кг"), null).BoxKg);
    }

    [Fact]
    public void Single_unit_box_is_a_box_only_for_units_not_lighter_than_a_kilogram()
    {
        // 493: в названии «0.18-кг» — вес пачки, равный весу единицы; пачка 0,18 кг коробкой не считается…
        var (box, note) = StockMath.BoxWeight(StockMath.ParsePack("493 Вафельные трубочки, MIS NAT, 0.18-кг"), 0.18m);
        Assert.Null(box);
        Assert.Contains("вес единицы", note);
        // …а по отгрузкам завода (по 10 пачек) коробка — 1,8 кг.
        var (shipped, shippedNote) = StockMath.BoxWeight(StockMath.ParsePack("493 Вафельные трубочки, MIS NAT, 0.18-кг"), 0.18m, 10);
        Assert.Equal(1.8m, shipped);
        Assert.Contains("по отгрузкам завода", shippedNote);
        // 194: блок 1,5 кг — сам себе коробка.
        Assert.Equal(1.5m, StockMath.BoxWeight(StockMath.ParsePack("194 Вафельные трубочки, MANZUR, 1,5-кг"), 1.5m).BoxKg);
        // 324: шоубокс 0,5 кг завод отгружает поштучно — коробки нет; 304 KEKO декор (весовой) уходит по 1 кг — коробка 1 кг.
        Assert.Null(StockMath.BoxWeight(StockMath.ParsePack("324 Кекс Eddy Panda 33г (шоубокс 15шт)"), 0.5m, 1).BoxKg);
        Assert.Equal(1m, StockMath.BoxWeight(null, 1m, 1).BoxKg);
        // Коробка из названия — в приоритете перед отгрузками; без веса единицы отгрузки не помогают.
        Assert.Equal(2m, StockMath.BoxWeight(StockMath.ParsePack("038 Шоколад (4-шт по 0,5-кг) 2-кг"), 0.5m, 8).BoxKg);
        Assert.Null(StockMath.BoxWeight(null, null, 10).BoxKg);
    }

    [Fact]
    public void Box_from_factory_shipments_is_the_largest_divisor_of_almost_all_lines()
    {
        // 493: завод отгружает по 10 пачек (40, 80, 160…); одна строка 25 — не в счёт (≥ 90% строк), а строка 30 не даёт принять 20.
        Assert.Equal(10m, StockMath.BoxUnitsFromShipments([40, 80, 160, 240, 400, 440, 720, 800, 1200, 560, 30, 25]));
        // Весовой KEKO: количества в кг, коробка 2,5 кг — делитель дробный.
        Assert.Equal(2.5m, StockMath.BoxUnitsFromShipments([637.5m, 3750, 2000, 2500, 1812.5m, 75, 50, 7.5m, 22.5m]));
        Assert.Null(StockMath.BoxUnitsFromShipments([10, 20, 30])); // меньше пяти строк — случайность
        Assert.Null(StockMath.BoxUnitsFromShipments([]));
        Assert.Equal(0.5m, StockMath.BoxUnitsFromShipments([0.5m, 1, 1.5m, 2, 2.5m, 3])); // строки по полкило не кратны 1 кг — делитель 0,5
    }

    [Theory]
    [InlineData("002 Кукурузные палочки, ROHAT BAMBUK, 3-кг", 3.0)]
    [InlineData("518-Kекс Nillo Brauni (180гр 12шт)", 0.18)]
    [InlineData("038 Шоколад (4-шт по 0,5-кг) 2-кг", 2.0)]
    [InlineData("489 Вафельные трубочки уп.коррекс (0,19-кг 12-шт), 2,28-кг, WAFFLE ROLLS 2,28 кг", 2.28)]
    [InlineData("304 KEKO декор", null)]
    public void Pack_weight_for_the_filter_is_the_last_weight_in_the_name(string name, double? kg) =>
        Assert.Equal(kg is null ? null : (decimal)kg.Value, StockMath.PackKg(name));

    private static readonly (string Code, string Name)[] Catalog =
    [
        ("002", "002 Кукурузные палочки, ROHAT BAMBUK, 3-кг"),
        ("187-1", "187-1 Вафельные трубочки, MIS NAT MAX"),
        ("318", "318 KEKO декор 2,5 кг"),
        ("t118", "t118 Конфеты помадные микс"),
    ];

    [Fact]
    public void Search_matches_numeric_code_without_leading_zeros_then_exact_code_then_substring()
    {
        string[] Find(string q) => StockMath.Search(Catalog, q, c => c.Code, c => c.Name).Select(c => c.Code).ToArray();

        Assert.Equal(["002"], Find("2")); // «002» = «2»
        Assert.Equal(["002"], Find("002"));
        Assert.Equal(["187-1"], Find("187-1")); // точный код
        Assert.Equal(["t118"], Find("T118")); // без учёта регистра
        Assert.Equal(["318"], Find("keko")); // подстрока в названии
        Assert.Equal(["002", "187-1", "318", "t118"], Find(" ")); // пусто — все
        Assert.Empty(Find("999"));
        Assert.Null(StockMath.NumericKey("187-1"));
        Assert.Equal("0", StockMath.NumericKey("000"));
    }

    [Fact]
    public void Corrected_speed_divides_by_days_in_stock_with_capped_zero_days()
    {
        Assert.Equal((10m, 0), StockMath.CorrectedKgPerDay(870, 87, 0));
        Assert.Equal((870m / 77, 10), StockMath.CorrectedKgPerDay(870, 87, 10));
        Assert.Equal((870m / 57, 30), StockMath.CorrectedKgPerDay(870, 87, 45)); // не больше 30 дней — знаменатель ≥ 57
        Assert.Equal((10m, 4), StockMath.CorrectedKgPerDay(10, 5, 30)); // и не больше дней базы − 1
        Assert.Equal((0m, 0), StockMath.CorrectedKgPerDay(-5, 87, 3)); // возвратов больше продаж — скорость 0
    }

    [Fact]
    public void Dealer_order_tops_up_to_15_days_rounded_up_to_whole_boxes()
    {
        // 10 кг/день × 15 = 150, на складе 100 → не хватает 50; коробка 3 кг → 17 коробок = 51 кг.
        Assert.Equal(51m, StockMath.OrderKg(10, 100, 3));
        Assert.Equal(50m, StockMath.OrderKg(10, 100, null)); // веса коробки нет — ровно сколько не хватает
        Assert.Equal(0m, StockMath.OrderKg(10, 200, 3)); // хватает
        Assert.Equal(0m, StockMath.OrderKg(10, 150 - 0.0001m, 3)); // нужно ≤ 0,0001 кг — 0
        Assert.Equal(6m, StockMath.OrderKg(0.4000000001m, 0, 3)); // 6,0000000015 ÷ 3 с допуском 1e-9 — две коробки, а не три
    }

    [Fact]
    public void Factory_order_is_dealer_orders_beyond_factory_stock()
    {
        Assert.Equal(300m, StockMath.FactoryOrderKg(1000, 700));
        Assert.Equal(0m, StockMath.FactoryOrderKg(500, 700));
    }

    [Fact]
    public void Closed_month_is_the_month_before_the_snapshot()
    {
        Assert.Equal((2026, 9), StockMath.ClosedMonth(new DateOnly(2026, 10, 6)));
        Assert.Equal((2025, 12), StockMath.ClosedMonth(new DateOnly(2026, 1, 15)));
    }

    [Fact]
    public void Velocity_window_ends_at_the_last_full_data_day()
    {
        var cutoff = new DateOnly(2026, 10, 5);
        Assert.Equal((new DateOnly(2026, 7, 11), cutoff), StockMath.VelocityWindow(cutoff, new DateOnly(2026, 10, 5), 87));
        Assert.Equal((new DateOnly(2026, 7, 9), new DateOnly(2026, 10, 3)), StockMath.VelocityWindow(cutoff, new DateOnly(2026, 10, 3), 87)); // данных за 4–5.10 ещё нет
        Assert.Equal((new DateOnly(2026, 7, 11), cutoff), StockMath.VelocityWindow(cutoff, new DateOnly(2026, 10, 7), 87)); // приёмка «в будущем» — не позже вчера
        Assert.Equal((new DateOnly(2026, 7, 11), cutoff), StockMath.VelocityWindow(cutoff, null, 87));
    }
}
