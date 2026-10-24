using Microsoft.Extensions.Configuration;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Sales.Primary;
using OneBase.Application.Sales.Stock;
using OneBase.Domain.Sales;
using OneBase.Infrastructure.Sales;

namespace OneBase.Sales.Tests;

/// <summary>Первичка: контрагенты (склады дилеров и точки завода), нетто возвратов, дата, склады → регионы, период по дате данных, таблицы, клиенты и календарь.</summary>
public class PrimaryTests
{
    private static readonly SalesOptions Options = new();
    private static readonly PrimaryPricing Pricing = new(new Dictionary<long, decimal>(), new Dictionary<long, PackInfo?>());
    private static readonly DateOnly Aug10 = new(2026, 8, 10);

    private const long Factory = 5;
    private const long Export = 23;
    private const long Kokand = 18;
    private const long KokandBazaar = 21;
    private const long Bukhara = 15;
    private const long Main = 1;

    private static readonly Dictionary<long, string> Stocks = new()
    {
        [Factory] = "Завод",
        [Export] = "Экспорт",
        [Kokand] = "Коканд",
        [KokandBazaar] = "Коканд бозор",
        [Bukhara] = "Бухоро",
        [Main] = "Основной",
    };

    /// <summary>Справочник регионов: дилеры — склады с таким регионом; «Основной» региона не имеет.</summary>
    private static bool IsDealerRegion(string region) => region is "Коканд" or "Бухоро";

    private static PrimaryTransferLine Transfer(long id, long from, long to, decimal kg, DateOnly? delivery = null, DateTime? given = null) =>
        new(id, from, to, delivery ?? Aug10, given, null, null, 1, kg, kg, kg * 1000);

    private static PrimaryShipment Ship(string party, int month, int day, decimal kg, long? product = 1, bool isReturn = false, decimal? sum = null) =>
        new($"t:{party}:{month}:{day}", party, new DateOnly(2026, month, day), product, new PrimaryAmounts(kg, kg / 2, sum ?? kg * 1000, kg * 1200), true, isReturn);

    [Fact]
    public void Transfer_is_dated_by_delivery_then_given_accepted_created()
    {
        var given = new DateTime(2026, 8, 3, 17, 40, 0);
        var accepted = new DateTime(2026, 8, 4, 9, 0, 0);
        var created = new DateTime(2026, 8, 1, 12, 0, 0);

        Assert.Equal(new DateOnly(2026, 8, 5), PrimaryMath.TransferDate(new DateOnly(2026, 8, 5), given, accepted, created));
        Assert.Equal(new DateOnly(2026, 8, 3), PrimaryMath.TransferDate(null, given, accepted, created));
        Assert.Equal(new DateOnly(2026, 8, 4), PrimaryMath.TransferDate(null, null, accepted, created));
        Assert.Equal(new DateOnly(2026, 8, 1), PrimaryMath.TransferDate(null, null, null, created));
        Assert.Null(PrimaryMath.TransferDate(null, null, null, null));
    }

    [Fact]
    public void Stock_aliases_put_bazaar_and_old_stocks_into_their_region()
    {
        Assert.Equal("Коканд", Options.StockRegionName("Коканд бозор"));
        Assert.Equal("Коканд", Options.StockRegionName(" коканд БОЗОР ")); // без учёта регистра и пробелов
        Assert.Equal("Андижон", Options.StockRegionName("Андижон эски 2"));
        Assert.Equal("Андижон", Options.StockRegionName("Андижон эски склад"));
        Assert.Equal("Жиззах", Options.StockRegionName("Жиззах (эски)"));
        Assert.Equal("Бухоро", Options.StockRegionName("Бухоро ")); // склад без записи — регион со своим названием
    }

    [Fact]
    public void Transfers_merge_aliased_stocks_and_net_dealer_returns()
    {
        PrimaryTransferLine[] lines =
        [
            Transfer(1, Factory, Kokand, 100),
            Transfer(2, Factory, KokandBazaar, 50, delivery: new DateOnly(2026, 8, 12), given: new DateTime(2026, 8, 11, 18, 0, 0)),
            Transfer(3, KokandBazaar, Factory, 10), // возврат дилера заводу
            Transfer(4, Factory, Export, 999), // склад экспорта — не дилер
            Transfer(5, Export, Factory, 777),
            Transfer(7, Factory, Main, 7), // «Основной» — склад без региона: не дилер и не клиент
            Transfer(8, Factory, Main, 3, delivery: new DateOnly(2025, 12, 30)),
        ];

        var transfers = PrimaryMath.Transfers(lines, new HashSet<long> { Factory }, new HashSet<long> { Export }, id => Stocks[id], Options, IsDealerRegion,
            region => $"dealer:{region}", Pricing);
        var rows = transfers.Rows;

        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.Equal("dealer:Коканд", r.Counterparty)); // Коканд + Коканд бозор — одна строка
        Assert.Equal(140m, rows.Sum(r => r.Amounts.Kg)); // нетто: 100 + 50 − 10
        var back = Assert.Single(rows, r => r.IsReturn);
        Assert.Equal(-10m, back.Amounts.Kg);
        Assert.Equal(-10_000m, back.Amounts.SumFactory);
        Assert.Equal(new DateOnly(2026, 8, 12), rows.Single(r => r.Doc == "t:2").Date); // доставка, а не выдача

        Assert.Equal(["Основной", "Основной"], transfers.OtherStocks.Select(s => s.Counterparty)); // прочие склады — отдельно, по названию склада
        Assert.Equal([("Основной", 1, 7m)], PrimaryMath.OtherStocksOf(transfers.OtherStocks, 2026).Select(s => (s.Name, s.Transfers, s.Kg))); // за год
    }

    /// <summary>
    /// Как в таблице первички ГП: «Андижон эски склад» → Коканд (28.02, 1 170,3 кг) — возврат у Андижона и отгрузка Коканду;
    /// Фаргона → Наманган (11.06, 800 кг) — так же. Внутри региона (Коканд ↔ «Коканд бозор») и со склада без региона — не первичка.
    /// </summary>
    [Fact]
    public void Transfer_between_dealers_of_different_regions_is_a_return_and_a_shipment()
    {
        PrimaryTransferLine[] lines =
        [
            Transfer(1, Kokand, Bukhara, 40),
            Transfer(2, Kokand, KokandBazaar, 25), // один регион
            Transfer(3, Main, Bukhara, 9), // «Основной» — не дилер
        ];

        var rows = PrimaryMath.Transfers(lines, new HashSet<long> { Factory }, new HashSet<long> { Export }, id => Stocks[id], Options, IsDealerRegion,
            region => $"dealer:{region}", Pricing).Rows;

        Assert.Equal(2, rows.Count);
        var back = Assert.Single(rows, r => r.IsReturn);
        Assert.Equal(("dealer:Коканд", -40m, -40_000m), (back.Counterparty, back.Amounts.Kg, back.Amounts.SumFactory));
        var shipped = Assert.Single(rows, r => !r.IsReturn);
        Assert.Equal(("dealer:Бухоро", 40m, 40_000m), (shipped.Counterparty, shipped.Amounts.Kg, shipped.Amounts.SumFactory));
        Assert.Equal(0m, rows.Sum(r => r.Amounts.Kg)); // республика не меняется
        Assert.All(rows, r => Assert.Equal("t:1", r.Doc)); // один документ
    }

    [Fact]
    public void Rows_after_the_report_day_are_left_out_of_the_fact_and_the_forecast()
    {
        // Отчётный день — 5 октября (вчера; синхронизация 6-го): строка с плановой доставкой 9-го в факт, прогноз и «данные по» не входит.
        PrimaryShipment[] rows = [Ship("A", 10, 3, 100), Ship("A", 10, 5, 50), Ship("A", 10, 9, 999)];
        var cutoff = PrimaryMath.DataLimit(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 6));

        var (kept, after) = PrimaryMath.ReportRows(rows, cutoff);
        var asOf = PrimaryMath.AsOf(kept.Select(r => r.Date), cutoff, null);
        var months = PrimaryMath.ByMonth(kept, PrimaryAmounts.Zero);

        Assert.Equal((2, 1), (kept.Count, after));
        Assert.Equal(new DateOnly(2026, 10, 5), asOf);
        Assert.Equal(150m, months[9].Kg);
        Assert.Equal(150m / 5 * 31, PrimaryMath.Forecast(months[9].Kg, 2026, 10, asOf));
        Assert.Equal(5, PrimaryMath.WorkedDays(2026, 10, asOf, kept.Count > 0));
        Assert.Equal(new DateOnly(2026, 10, 3), PrimaryMath.DataLimit(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 3))); // синхронизация раньше вчера
        Assert.Equal(new DateOnly(2026, 10, 5), PrimaryMath.DataLimit(new DateOnly(2026, 10, 5), null));
    }

    [Fact]
    public void Direct_orders_are_given_and_delivered_orders_of_factory_branches_to_non_export_markets()
    {
        var options = new SalesOptions { PrimaryExcludedMarkets = ["Дегустатсия + Акция", "555"] };
        PrimaryOrderLine Order(long id, long market, string name, string? type, string branch, string status, string? currency = "SUM", decimal kg = 10) =>
            new(id, market, name, type, branch, status, Aug10, currency, 7, kg, kg, kg * 30_000);
        PrimaryOrderLine[] orders =
        [
            Order(1, 26576, "Murod D/63", "Оптовая торговля", "Завод", "given"),
            Order(2, 26576, "Murod D/63", "Оптовая торговля", "Завод", "delivered"),
            Order(3, 26576, "Murod D/63", "Оптовая торговля", "Завод", "cancelled"),
            Order(4, 26777, "Daler Tojikiston", "EXPORT", "Завод", "delivered"), // экспорт — отдельная ветка
            Order(5, 27896, "Дегустатсия + Акция", "Оптовая торговля", "Завод", "delivered"), // исключена по названию
            Order(6, 555, "Акция", "Retail", "Завод", "delivered"), // исключена по id
            Order(7, 1001, "Магазин", "Retail", "Ташкент", "delivered"), // филиал вторички
            Order(8, 69891, "Bolajon citymall", "Retail", "к к мерч", "given"), // филиал без учёта регистра
            Order(9, 26614, "Havas", "Retail", "Завод", "delivered", currency: "USD", kg: 4),
        ];
        PrimaryReturnLine[] returns =
        [
            new(50, 26576, "Murod D/63", "Оптовая торговля", "Завод", "delivered", new DateOnly(2026, 9, 2), 7, 3, 31_000, 3),
            new(51, 26576, "Murod D/63", "Оптовая торговля", "Завод", "not_delivered", new DateOnly(2026, 9, 2), 7, 100, 31_000, 100),
        ];

        var direct = PrimaryMath.DirectOrders(orders, returns, options, Pricing);

        Assert.Equal(["o:1", "o:2", "o:8", "o:9", "r:50"], direct.Rows.Select(r => r.Doc));
        Assert.Equal(PrimaryMath.ClientId("Мурод бозор/Д63"), direct.Rows[0].Counterparty); // название таблицы первички (PrimaryClientNames)
        Assert.Equal(KeyValuePair.Create(9L, Aug10), Assert.Single(direct.OtherCurrencyOrders)); // заказ в другой валюте — с датой: заметка за выбранный год
        Assert.Equal(0m, direct.Rows.Single(r => r.Doc == "o:9").Amounts.SumFactory); // вес USD-заказа учтён, суммы в сумах нет
        Assert.Equal(4m, direct.Rows.Single(r => r.Doc == "o:9").Amounts.Kg);
        var back = direct.Rows.Single(r => r.IsReturn);
        Assert.Equal((-3m, -93_000m, new DateOnly(2026, 9, 2)), (back.Amounts.Kg, back.Amounts.SumFactory, back.Date)); // количество × цена, дата возврата
        var murod = direct.Parties[PrimaryMath.ClientId("Мурод бозор/Д63")];
        Assert.Equal(("Мурод бозор/Д63", "Murod D/63 · Оптовая торговля · Завод", PrimaryGroups.Direct), (murod.Name, murod.Sub, murod.Group));
        Assert.Equal(("Havas", "Retail · Завод"), (direct.Parties[PrimaryMath.DirectId(26614)].Name, direct.Parties[PrimaryMath.DirectId(26614)].Sub)); // без правила — название Linko
        Assert.Equal(3, direct.Parties.Count); // Мурод, Болажон, Havas
    }

    /// <summary>
    /// Коробки — как в таблице первички: «304 KEKO декор» без веса в названии — коробка 1 кг из справочника; «493 … 0.18-кг» — в названии вес
    /// пачки, справочник даёт коробку 1,8 кг (10 пачек); товар без записи — по названию, как раньше.
    /// </summary>
    [Fact]
    public void Box_weight_from_the_directory_wins_over_the_name()
    {
        const long keko = 63, misNat = 271, bamboo = 78;
        var packs = new Dictionary<long, PackInfo?>
        {
            [keko] = StockMath.ParsePack("304 KEKO декор"),
            [misNat] = StockMath.ParsePack("493 Вафельные трубочки шоколадной глазурью сверху жаренный арахис, MIS NAT, 0.18-кг"),
            [bamboo] = StockMath.ParsePack("002 Кукурузные палочки с молочной начинкой, ROHAT BAMBUK, 3-кг"),
        };
        var directory = new Dictionary<long, decimal> { [keko] = Options.PrimaryBoxKg["304"], [misNat] = Options.PrimaryBoxKg["493"] };
        var pricing = new PrimaryPricing(new Dictionary<long, decimal>(), packs, boxKgByProduct: directory);

        Assert.Equal((100m, true), (pricing.Value(keko, 100, 100, 0).Amounts.Boxes, pricing.Value(keko, 100, 100, 0).BoxesKnown));
        Assert.Equal(10m, pricing.Value(misNat, 100, 18, 0).Amounts.Boxes); // 100 пачек по 0,18 кг = 18 кг = 10 коробок
        Assert.Equal(100m, pricing.Value(bamboo, 300, 300, 0).Amounts.Boxes); // из названия: блок 3 кг
        Assert.Equal((0m, false), (new PrimaryPricing(new Dictionary<long, decimal>(), packs).Value(keko, 1, 1, 0).Amounts.Boxes!.Value,
            new PrimaryPricing(new Dictionary<long, decimal>(), packs).Value(keko, 1, 1, 0).BoxesKnown)); // без справочника — неизвестно
    }

    /// <summary>Заказ в пути («не доставлен») — первичка: таблица ГП записывает его в день создания; отменённый — нет.</summary>
    [Fact]
    public void Order_in_transit_counts_cancelled_does_not()
    {
        PrimaryOrderLine[] orders =
        [
            new(1, 26614, "OOO \"Havas Food\" (Havas)", "Retail", "Завод", "not_delivered", new DateOnly(2026, 10, 13), "SUM", 7, 340.4m, 340.4m, 1m),
            new(2, 26614, "OOO \"Havas Food\" (Havas)", "Retail", "Завод", "cancelled", new DateOnly(2026, 10, 13), "SUM", 7, 50, 50, 1m),
        ];

        var rows = PrimaryMath.DirectOrders(orders, [], Options, Pricing).Rows;

        Assert.Equal(340.4m, Assert.Single(rows).Amounts.Kg);
    }

    [Fact]
    public void Primary_order_options_have_the_documented_defaults_and_are_replaced_from_settings()
    {
        Assert.Equal(["Завод", "К К Мерч"], Options.PrimaryOrderBranches);
        Assert.Equal(["delivered", "given", "not_delivered"], Options.PrimaryOrderStatuses);
        Assert.True(Options.IsPrimaryExcludedMarket(27896, "дегустатсия + акция"));
        Assert.False(Options.IsPrimaryExcludedMarket(26576, "Murod D/63"));

        var options = SalesOptionsBinding.Load(new ConfigurationBuilder().AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes("""
            {
              "Sales": {
                "PrimaryOrderStatuses": [ "delivered" ],
                "PrimaryExcludedMarkets": [ "27896" ],
                "StockRegionAliases": { "Чирчик": "Ташкент", "андижон ЭСКИ 2": "Фаргона" }
              }
            }
            """))).Build());

        Assert.Equal(["delivered"], options.PrimaryOrderStatuses);
        Assert.True(options.IsPrimaryExcludedMarket(27896, "Переименована"));
        Assert.Equal("Ташкент", options.StockRegionName("Чирчик"));
        Assert.Equal("Коканд", options.StockRegionName("Коканд бозор")); // словарь дополняется, записи из кода остаются
        Assert.Equal("Фаргона", options.StockRegionName("Андижон эски 2")); // запись из настроек заменяет запись из кода без учёта регистра
        Assert.Equal(5, options.StockRegionAliases.Count); // а не добавляется второй
    }

    [Fact]
    public void Dealer_row_is_labelled_by_the_dealer_of_the_region()
    {
        var named = PrimaryMath.DealerParty("18", "Коканд", "Кахрамон Кукон", PrimaryMath.NoPlan);
        Assert.Equal(("Кахрамон Кукон", "Коканд", PrimaryGroups.Dealer), (named.Name, named.Sub, named.Group));

        var plain = PrimaryMath.DealerParty("1", "Основной", " ", PrimaryMath.NoPlan);
        Assert.Equal(("Основной", (string?)null), (plain.Name, plain.Sub));
    }

    /// <summary>
    /// Как в таблице первички (ГП): склад прежнего дилера — его строка (старый филиал): «Андижон эски 2» (Икбол) — «Андижон (эски) 2»,
    /// «Жиззах (эски)» (Билолиддин) — «Жиззах (эски)»; перемещение со старого склада на новый (07.2026, 252 кг) — возврат у прежнего дилера
    /// и отгрузка новому. «Коканд бозор» по-прежнему в Коканде; вторичка и остаток сводят старые склады к текущему региону.
    /// </summary>
    [Fact]
    public void Stocks_of_former_dealers_are_their_own_dealer_rows()
    {
        const long andijan = 34, andijanOld2 = 27, jizzakhOld = 24;
        var stocks = new Dictionary<long, string>(Stocks)
        {
            [andijan] = "Андижон",
            [andijanOld2] = "Андижон эски 2",
            [jizzakhOld] = "Жиззах (эски)",
        };
        static bool IsDealer(string region) => region is "Андижон" or "Андижон (эски) 2" or "Жиззах (эски)" or "Коканд";
        PrimaryTransferLine[] lines =
        [
            Transfer(1, Factory, andijanOld2, 100),
            Transfer(2, andijanOld2, andijan, 252),
            Transfer(3, Factory, jizzakhOld, 30),
            Transfer(4, Factory, KokandBazaar, 5),
        ];

        var rows = PrimaryMath.Transfers(lines, new HashSet<long> { Factory }, new HashSet<long> { Export }, id => stocks[id], Options, IsDealer,
            region => $"dealer:{region}", Pricing).Rows;

        Assert.Equal("Андижон (эски) 2", Options.PrimaryStockRegion("андижон ЭСКИ 2"));
        Assert.Equal("Андижон (эски) 1", Options.PrimaryStockRegion("Андижон эски склад"));
        Assert.Equal("Коканд", Options.PrimaryStockRegion("Коканд бозор")); // без записи — StockRegionAliases
        Assert.Equal("Андижон", Options.StockRegionName("Андижон эски 2")); // вне первички — текущий регион
        Assert.Equal(-152m, rows.Where(r => r.Counterparty == "dealer:Андижон (эски) 2").Sum(r => r.Amounts.Kg)); // 100 − 252
        Assert.Equal(252m, Assert.Single(rows, r => r.Counterparty == "dealer:Андижон").Amounts.Kg);
        Assert.Equal(30m, Assert.Single(rows, r => r.Counterparty == "dealer:Жиззах (эски)").Amounts.Kg);
        Assert.Equal(5m, Assert.Single(rows, r => r.Counterparty == "dealer:Коканд").Amounts.Kg);
    }

    /// <summary>
    /// Прямые клиенты — под названиями таблицы первички: «Макро РЦ» и магазины Makro «К К Мерч» (M015, М135 кириллицей, «м 104 …», «M140-…»)
    /// — одна строка «Макро» с числом точек второй строкой; «Bolajon citymall» — «Болажон Сити молл»; точка без правила — под своим названием.
    /// </summary>
    [Fact]
    public void Direct_clients_get_the_names_of_the_primary_table_and_makro_stores_are_one_row()
    {
        PrimaryOrderLine Order(long id, long market, string name, string branch, decimal kg) =>
            new(id, market, name, "Retail", branch, "delivered", Aug10, "SUM", 7, kg, kg, kg * 30_000);
        PrimaryOrderLine[] orders =
        [
            Order(1, 32553, "Макро РЦ", "Завод", 44),
            Order(2, 46693, "M015 КАТАРТАЛ", "К К Мерч", 15),
            Order(3, 46738, "М135 ЮНУСАБАД 18КВ", "К К Мерч", 8),
            Order(4, 53904, "м 104 МВД макро", "К К Мерч", 1),
            Order(5, 59822, "M140-ТАШ..ВУЗГОРОДОК 2", "К К Мерч", 4),
            Order(6, 69891, "Bolajon citymall", "К К Мерч", 263),
            Order(7, 26614, "OOO \"Havas Food\" (Havas)", "Завод", 711),
            Order(8, 26500, "Ятт Илгоров", "Завод", 108),
        ];

        var direct = PrimaryMath.DirectOrders(orders, [], Options, Pricing);
        var byName = direct.Rows.GroupBy(r => direct.Parties[r.Counterparty].Name).ToDictionary(g => g.Key, g => g.Sum(r => r.Amounts.Kg));

        Assert.Equal(72m, byName["Макро"]); // 44 + 15 + 8 + 1 + 4
        Assert.Equal("5 точек Linko", direct.Parties[PrimaryMath.ClientId("Макро")].Sub);
        Assert.Equal(263m, byName["Болажон Сити молл"]);
        Assert.Equal(711m, byName["Хавас"]);
        Assert.Equal(108m, byName["Накт пул бозор"]); // «Ятт Илгоров», заказ «доставлен»
        Assert.Equal(4, direct.Parties.Count);
        Assert.Null(Options.PrimaryClientName("Makro M001 склад")); // латиница без номера магазина — не Makro-магазин
    }

    /// <summary>
    /// «Ятт Илгоров» — как в таблице первички две строки: заказы «отдан» — «Фирменный магазин» (сверка 2026: вес совпадает до 0,1 кг
    /// в 9 месяцах из 10), остальные заказы и возвраты — «Накт пул бозор». Деление задаётся в настройках и заменяет запись из кода.
    /// </summary>
    [Fact]
    public void Shop_of_the_factory_is_split_by_order_status_like_the_primary_table()
    {
        PrimaryOrderLine Order(long id, string status, decimal kg) =>
            new(id, 26500, "Ятт Илгоров", "Retail", "Завод", status, Aug10, "SUM", 7, kg, kg, kg * 30_000);
        PrimaryOrderLine[] orders = [Order(1, "given", 144.3m), Order(2, "delivered", 4.5m), Order(3, "not_delivered", 2), Order(4, "cancelled", 9)];
        PrimaryReturnLine[] returns = [new(50, 26500, "Ятт Илгоров", "Retail", "Завод", "delivered", Aug10, 7, 1, 30_000, 1)];

        var direct = PrimaryMath.DirectOrders(orders, returns, Options, Pricing);
        var byName = direct.Rows.GroupBy(r => direct.Parties[r.Counterparty].Name).ToDictionary(g => g.Key, g => g.Sum(r => r.Amounts.Kg));

        Assert.Equal(144.3m, byName["Фирменный магазин"]);
        Assert.Equal(5.5m, byName["Накт пул бозор"]); // 4,5 + 2 − 1 (возврат)
        Assert.Equal("Ятт Илгоров · Retail · Завод", direct.Parties[PrimaryMath.ClientId("Фирменный магазин")].Sub);
        Assert.Equal("Накт пул бозор", Options.PrimaryClientName(" ятт илгоров ", "DELIVERED"));
        Assert.Equal("Мурод бозор/Д63", Options.PrimaryClientName("Murod D/63", "given")); // остальные точки — как раньше

        var options = SalesOptionsBinding.Load(new ConfigurationBuilder().AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes("""
            { "Sales": { "PrimaryClientStatusNames": { "^Ятт Илгоров$": { "*": "Ятт Илгоров" } } } }
            """))).Build());
        Assert.Equal("Ятт Илгоров", options.PrimaryClientName("Ятт Илгоров", "delivered"));
    }

    [Fact]
    public void Returns_are_netted_in_months_rows_and_year_to_date()
    {
        PrimaryShipment[] rows = [Ship("A", 8, 3, 100), Ship("A", 8, 20, -30, isReturn: true), Ship("B", 7, 5, 50), Ship("B", 8, 1, 20)];
        var parties = new Dictionary<string, PrimaryParty>
        {
            ["A"] = new("A", "Дилер А", null, PrimaryGroups.Dealer, PrimaryMath.NoPlan),
            ["B"] = new("B", "Точка Б", null, PrimaryGroups.Direct, PrimaryMath.NoPlan),
        };
        var months = PrimaryMath.ByMonth(rows, PrimaryAmounts.Zero);
        var ytd = PrimaryMath.SumOf(rows.Select(r => r.Amounts), PrimaryAmounts.Zero);

        var table = PrimaryMath.PartyRows(rows, parties, 8, PrimaryAmounts.Zero, months[7], ytd);

        Assert.Equal(90m, months[7].Kg); // август: 100 − 30 + 20
        Assert.Equal(["A", "B"], table.Select(r => r.Id)); // дилеры — раньше прямых клиентов
        Assert.Equal(70m, table[0].Month.Kg);
        Assert.Equal(70m, table[0].Ytd.Kg);
        Assert.Equal(70m, table[1].Ytd.Kg); // 50 в июле + 20 в августе
        var groups = PrimaryMath.GroupTotals(table, 8, PrimaryAmounts.Zero, months[7], ytd);
        Assert.Equal([("dealer", "Дилеры", 70m), ("direct", "Прямые клиенты завода", 20m)], groups.Select(g => (g.Id, g.Name, g.Month.Kg)));
        Assert.Equal(20m / 90m, groups[1].MonthShare.Kg); // доля группы во всей отгрузке месяца
    }

    [Fact]
    public void Data_date_is_the_last_day_with_rows_not_after_yesterday_and_the_sync()
    {
        DateOnly[] days = [new(2026, 10, 1), new(2026, 10, 3), new(2026, 10, 5), new(2026, 10, 9)]; // 9-е — плановая дата в будущем

        Assert.Equal(new DateOnly(2026, 10, 5), PrimaryMath.AsOf(days, cutoff: new DateOnly(2026, 10, 5), syncedOn: new DateOnly(2026, 10, 6)));
        Assert.Equal(new DateOnly(2026, 10, 3), PrimaryMath.AsOf(days, cutoff: new DateOnly(2026, 10, 5), syncedOn: new DateOnly(2026, 10, 3))); // данные — на день синхронизации
        Assert.Equal(new DateOnly(2026, 10, 3), PrimaryMath.AsOf(days, cutoff: new DateOnly(2026, 10, 4), syncedOn: null));
        Assert.Null(PrimaryMath.AsOf([new DateOnly(2026, 10, 9)], new DateOnly(2026, 10, 5), null));
    }

    [Fact]
    public void Forecast_uses_the_share_of_the_month_by_data_date_and_only_for_the_running_month()
    {
        var asOf = new DateOnly(2026, 10, 5);

        Assert.Equal(5m / 31, PrimaryMath.MonthShare(2026, 10, asOf));
        Assert.True(PrimaryMath.IsRunning(2026, 10, asOf));
        Assert.Equal(10, PrimaryMath.RunningMonth(2026, asOf));
        Assert.Equal(5, PrimaryMath.WorkedDays(2026, 10, asOf, hasData: true));
        Assert.Equal(78_855m / 5 * 31, PrimaryMath.Forecast(78_855m, 2026, 10, asOf)); // факт ÷ доля месяца, не по сегодняшнему дню

        Assert.Equal(1m, PrimaryMath.MonthShare(2026, 9, asOf)); // закрытый месяц
        Assert.Null(PrimaryMath.Forecast(369_524m, 2026, 9, asOf));
        Assert.Equal(30, PrimaryMath.WorkedDays(2026, 9, asOf, hasData: true));
        Assert.Equal(0m, PrimaryMath.MonthShare(2026, 11, asOf)); // данных ещё нет
        Assert.Null(PrimaryMath.Forecast(10m, 2026, 11, asOf));
        Assert.Equal(0, PrimaryMath.WorkedDays(2026, 11, asOf, hasData: false));
        Assert.Null(PrimaryMath.RunningMonth(2025, asOf));

        // Больше 98% месяца — закрыт: прогноза нет и месяц не «идёт».
        Assert.True(PrimaryMath.IsRunning(2026, 10, new DateOnly(2026, 10, 30))); // 30/31 = 96,8%
        Assert.False(PrimaryMath.IsRunning(2026, 10, new DateOnly(2026, 10, 31)));
        Assert.Null(PrimaryMath.RunningMonth(2026, new DateOnly(2026, 10, 31)));
        Assert.Null(PrimaryMath.Forecast(1m, 2026, 10, null));

        // Отчётный день — сегодня, последний день месяца: день ещё идёт — месяц не закрыт; наступило 1-е — закрыт.
        var lastDay = new DateOnly(2026, 10, 31);
        Assert.True(PrimaryMath.IsRunning(2026, 10, lastDay, today: lastDay));
        Assert.Equal(10, PrimaryMath.RunningMonth(2026, lastDay, today: lastDay));
        Assert.Equal(31, PrimaryMath.WorkedDays(2026, 10, lastDay, hasData: true, today: lastDay));
        Assert.Equal(62m, PrimaryMath.Forecast(62m, 2026, 10, lastDay, today: lastDay)); // за все дни — прогноз = факт
        Assert.False(PrimaryMath.IsRunning(2026, 10, lastDay, today: new DateOnly(2026, 11, 1)));
        Assert.False(PrimaryMath.IsRunning(2026, 9, lastDay, today: lastDay)); // прошлый месяц закрыт и сегодня
    }

    [Fact]
    public void Clients_by_month_count_counterparties_with_positive_net_and_hide_empty_categories()
    {
        (string, string) Category(long? product) => product switch { 1 => ("-1", "Бамбук"), 2 => ("-2", "Кекс"), _ => ("-3", "Шоколад") };
        PrimaryShipment[] rows =
        [
            Ship("A", 8, 3, 10, product: 1),
            Ship("B", 8, 4, 5, product: 1),
            Ship("B", 8, 20, -5, product: 1, isReturn: true), // нетто Б за август — ноль: не клиент
            Ship("C", 8, 6, 3, product: 2),
            Ship("A", 6, 2, 6, product: 2),
            Ship("D", 8, 9, -2, product: 3, isReturn: true), // только возврат: «Шоколад» без отгрузок — не показывается
        ];

        var clients = PrimaryMath.Clients(2026, 8, lastPartial: true, rows, Category);

        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], clients.Akb.Months);
        Assert.True(clients.Akb.LastPartial);
        Assert.Equal(2m, clients.Akb.Total[7]); // A и C
        Assert.Null(clients.Akb.Total[6]); // в июле отгрузок не было
        Assert.Equal(1m, clients.Akb.Total[5]);
        Assert.Equal(["Бамбук", "Кекс"], clients.Akb.Categories.Select(c => c.Name)); // по весу (10 и 9 кг); «Шоколад» скрыт
        Assert.Equal(1m, clients.Akb.Categories[0].Values[7]); // в «Бамбуке» нетто > 0 только у А
        Assert.Equal(1m, clients.Akb.Categories[1].Values[5]); // «Кекс» в июне — А
        Assert.Equal(11m, clients.Kg.Total[7]); // 10 + 5 − 5 + 3 − 2
        Assert.Equal(11_000m, clients.Sum.Total[7]);
        Assert.Equal([null, null, null, null, null, 0m, null, 10m], clients.Kg.Categories[0].Values); // в июне отгрузки были, «Бамбука» — нет

        // Среднее за месяц — по закрытым месяцам с данными: идущий август не в счёт, остаётся июнь.
        Assert.Equal((1m, 6m, 6_000m), (clients.Akb.Average!.Value, clients.Kg.Average!.Value, clients.Sum.Average!.Value));
        Assert.Equal((0m, 1m), (clients.Akb.Categories[0].Average!.Value, clients.Akb.Categories[1].Average!.Value)); // Бамбук в июне — 0 клиентов, Кекс — А
        var closed = PrimaryMath.Clients(2026, 8, lastPartial: false, rows, Category);
        Assert.Equal(1.5m, closed.Akb.Average); // август закрыт — (1 + 2) / 2
        Assert.Null(PrimaryMath.Clients(2026, 8, lastPartial: true, [Ship("A", 8, 3, 10)], Category).Akb.Average); // только идущий месяц — среднего нет
    }

    [Fact]
    public void Calendar_swaps_bounds_shows_only_parties_with_shipments_and_the_picked_detail()
    {
        PrimaryShipment[] rows = [Ship("A", 9, 2, 10), Ship("A", 9, 5, 4, product: 2), Ship("B", 9, 5, 6), Ship("B", 9, 9, -1, isReturn: true), Ship("C", 9, 20, 8)];
        var parties = new Dictionary<string, PrimaryParty> { ["A"] = new("A", "Дилер А", "Регион А", PrimaryGroups.Dealer, PrimaryMath.NoPlan) };
        PrimaryProduct Product(long? id) => new($"Товар {id}", $"{id:000}", "Бамбук");

        var all = PrimaryMath.Calendar(rows, 30, null, parties, Product, PrimaryAmounts.Zero);
        Assert.Equal([2, 5, 9, 20], all.MonthDays);
        Assert.Equal<(int?, int?)>((2, 20), (all.From, all.To));
        Assert.Null(all.Detail);

        var range = PrimaryMath.Calendar(rows, 30, new PrimaryCalendarQuery(From: 10, To: 3), parties, Product, PrimaryAmounts.Zero);
        Assert.Equal<(int?, int?)>((3, 10), (range.From, range.To)); // перепутанные границы — местами
        Assert.Equal([5, 9], range.Days);
        Assert.Equal(["B", "A"], range.Rows.Select(r => r.Id)); // C отгружали 20-го — строки нет; по убыванию веса
        var a = range.Rows.Single(r => r.Id == "A");
        Assert.Equal(("Дилер А", "Регион А", 4m, 1), (a.Name, a.Sub, a.Total.Kg, a.Days));
        Assert.Null(a.Cells[1]); // 9-го дилеру А не отгружали
        Assert.Equal([10m, -1m], range.DayTotals.Select(t => t.Kg));
        Assert.Equal(9m, range.Total.Kg);

        var day = PrimaryMath.Calendar(rows, 30, new PrimaryCalendarQuery(Day: 5), parties, Product, PrimaryAmounts.Zero);
        Assert.Equal([("B", "B", 6m), ("A", "Дилер А", 4m)], day.Detail!.Select(i => (i.DealerId!, i.DealerName!, i.Amounts.Kg))); // весь день — по каждому контрагенту
        Assert.Equal(10m, day.DetailTotal!.Kg);

        var dealer = PrimaryMath.Calendar(rows, 30, new PrimaryCalendarQuery(Dealer: "A"), parties, Product, PrimaryAmounts.Zero);
        Assert.Equal([(1L, 10m), (2L, 4m)], dealer.Detail!.Select(i => (i.ProductId!.Value, i.Amounts.Kg))); // весь период дилера, без колонки контрагента
        Assert.All(dealer.Detail!, i => Assert.Null(i.DealerId));

        var junk = PrimaryMath.Calendar(rows, 30, new PrimaryCalendarQuery(From: 0, To: 45, Day: 31, Dealer: "нет такого"), parties, Product, PrimaryAmounts.Zero);
        Assert.Equal<(int?, int?, int?, string?)>((2, 20, null, null), (junk.From, junk.To, junk.Day, junk.Dealer)); // дни вне месяца и чужой контрагент не берутся
        Assert.Null(junk.Detail);

        var empty = PrimaryMath.Calendar([], 30, new PrimaryCalendarQuery(From: 1, To: 5), parties, Product, PrimaryAmounts.Zero);
        Assert.Empty(empty.Rows);
        Assert.Null(empty.From);
    }

    [Fact]
    public void Plan_comes_from_primary_region_plans_and_its_rows_stay_without_fact()
    {
        var north = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var oldNorth = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var south = Guid.Parse("00000000-0000-0000-0000-000000000003");
        RegionPlanRow[] stored =
        [
            new(north, PlanKind.Primary, 2026, 8, 4, 60),
            new(north, PlanKind.Primary, 2026, 8, 10, 40),
            new(oldNorth, PlanKind.Primary, 2026, 8, null, 25), // старый склад — в той же строке дилера
            new(south, PlanKind.Primary, 2026, 8, null, 80), // без отгрузок — строка остаётся
            new(north, PlanKind.Rop, 2026, 8, null, 999), // план вторички в первичку не идёт
            new(north, PlanKind.Primary, 2025, 8, null, 777),
        ];

        var plans = PrimaryMath.PlanRows(stored, 2026);
        Assert.Equal(4, plans.Count);
        var byDealer = PrimaryMath.DealerPlans(plans, id => id == south ? "S" : "N");
        Assert.Equal(125m, byDealer["N"][7]); // 60 + 40 по категориям + 25 итогом старого склада
        Assert.Null(byDealer["N"][6]);

        var parties = new Dictionary<string, PrimaryParty>
        {
            ["N"] = PrimaryMath.DealerParty("N", "Север", null, byDealer["N"]),
            ["S"] = PrimaryMath.DealerParty("S", "Юг", null, byDealer["S"]),
            ["X"] = PrimaryMath.DealerParty("X", "Без плана и отгрузок", null, PrimaryMath.NoPlan),
        };
        PrimaryShipment[] rows = [Ship("N", 8, 3, 150)];
        var months = PrimaryMath.ByMonth(rows, PrimaryAmounts.Zero);

        var table = PrimaryMath.PartyRows(rows, parties, 8, PrimaryAmounts.Zero, months[7], months[7]);

        Assert.Equal(["N", "S"], table.Select(r => r.Id)); // строка с планом без факта есть, без плана и факта — нет
        var n = table[0];
        Assert.Equal((125m, 1.2m, 0m), (n.PlanMonthKg!.Value, n.MonthExecution!.Value, n.MonthRemainingKg!.Value)); // перевыполнен — осталось 0
        var s = table[1];
        Assert.Equal((0m, 0m, 80m), (s.Month.Kg, s.MonthExecution!.Value, s.MonthRemainingKg!.Value));
        var total = PrimaryMath.Total("total", "Итого", null, table, 8, PrimaryAmounts.Zero, months[7], months[7]);
        Assert.Equal((205m, 150m / 205m, 55m), (total.PlanMonthKg!.Value, total.MonthExecution!.Value, total.MonthRemainingKg!.Value));
        Assert.Equal(205m, total.PlanYtdKg);
    }

    [Fact]
    public void Row_shares_execution_and_year_to_date_are_computed_by_the_server()
    {
        var months = Enumerable.Range(1, 12).Select(m => m <= 3 ? new PrimaryAmounts(m * 10, m, m * 100, m * 150) : PrimaryAmounts.Zero).ToList();
        decimal?[] plans = [null, 25, 50, null, null, null, null, null, null, null, null, null];
        var monthTotal = new PrimaryAmounts(60, 6, 600, 600);
        var ytdTotal = new PrimaryAmounts(120, 12, 1200, 1800);

        var row = PrimaryMath.Row("r", "Строка", null, PrimaryGroups.Dealer, months, plans, 3, PrimaryAmounts.Zero, monthTotal, ytdTotal);

        Assert.Equal(new PrimaryShares(0.5m, 0.5m, 0.5m, 0.75m), row.MonthShare);
        Assert.Equal((0.6m, 20m), (row.MonthExecution!.Value, row.MonthRemainingKg!.Value)); // 30 из 50
        Assert.Equal(new PrimaryAmounts(60, 6, 600, 900), row.Ytd); // январь–март
        Assert.Equal(0.5m, row.YtdShare.Kg);
        Assert.Equal((75m, 0.8m), (row.PlanYtdKg!.Value, row.YtdExecution!.Value)); // 60 из 25 + 50

        var noPlan = PrimaryMath.Row("r", "Строка", null, null, months, PrimaryMath.NoPlan, 3, PrimaryAmounts.Zero, monthTotal, ytdTotal);
        Assert.Equal(((decimal?)null, (decimal?)null, (decimal?)null), (noPlan.MonthExecution, noPlan.MonthRemainingKg, noPlan.PlanYtdKg));

        Assert.Equal(30m, PrimaryMath.PricePerKg(new PrimaryAmounts(10, 0, 300, 360)));
        Assert.Equal(0.2m, PrimaryMath.Markup(new PrimaryAmounts(10, 0, 300, 360)));
        Assert.Null(PrimaryMath.Markup(new PrimaryAmounts(10, null, 300, 0))); // у экспорта цены продажи дилера нет
    }

    [Fact]
    public void Export_has_no_boxes_while_republic_counts_them_from_the_pack()
    {
        var packs = new Dictionary<long, PackInfo?> { [1] = new PackInfo(3m, null, null) };
        var republic = new PrimaryPricing(new Dictionary<long, decimal> { [1] = 40_000 }, packs).Value(1, 30, 30, 900_000);
        Assert.Equal((10m, 1_200_000m, true), (republic.Amounts.Boxes!.Value, republic.Amounts.SumDealer, republic.BoxesKnown)); // весовой товар: 30 кг ÷ 3 кг

        var export = PrimaryPricing.WeightOnly.Value(1, 30, 30, 900_000);
        Assert.Null(export.Amounts.Boxes);
        Assert.Equal(0m, export.Amounts.SumDealer);
        Assert.Null(PrimaryMath.SumOf([export.Amounts, export.Amounts], PrimaryPricing.WeightOnly.Zero).Boxes);
        Assert.Null(PrimaryMath.SumOf([], PrimaryPricing.WeightOnly.Zero).Boxes); // и пустой месяц экспорта — без коробок
        Assert.Equal(new PrimaryShares(0.5m, null, 0.5m, null), PrimaryMath.Shares(export.Amounts, PrimaryMath.SumOf([export.Amounts, export.Amounts], PrimaryAmounts.NoBoxes)));
    }
}
