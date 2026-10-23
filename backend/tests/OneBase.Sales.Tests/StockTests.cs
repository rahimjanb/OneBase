using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Sales.Stock;

namespace OneBase.Sales.Tests;

/// <summary>Охват, фильтры и итоги рекомендуемого остатка (StockService.Compose) на базе, собранной вручную.</summary>
public class StockTests
{
    private const string Rm1 = "d1";
    private const string Rm2 = "d2";
    private static readonly StockRegion Bukhara = new("r1", "Бухоро", 15, Rm1);
    private static readonly StockRegion Denov = new("r2", "Денов", 14, Rm2);
    private static readonly StockRegion Factory = new("factory", "Завод", 5);
    private static readonly StockRegion Export = new("export", "Экспорт", 23);

    private static readonly StockProduct Bambuk = new(1, "002 Кукурузные палочки, ROHAT BAMBUK, 3-кг", "002", 4, StockMath.ParsePack("3-кг"), 1m, WeightSources.Orders, 3m, "по названию", 3m, 22800m);
    private static readonly StockProduct Keko = new(2, "318 KEKO декор 2,5 кг", "318", 10, StockMath.ParsePack("2,5 кг"), 1m, WeightSources.Orders, 2.5m, "по названию", 2.5m, 33500m);
    private static readonly StockProduct Tube = new(3, "493 Вафельные трубочки, MIS NAT, 0.18-кг", "493", 9, StockMath.ParsePack("0.18-кг"), 0.18m, WeightSources.OrdersYear, null, "вес единицы", 0.18m, 45000m);

    /// <summary>Клетка склада, как её считает StockService: кг, коробки, стоимость, скорость и запас на 15 дней в трёх единицах, заказ до запаса на 15 дней (у завода — заказ у завода).</summary>
    private static StockCell Cell(StockProduct p, decimal pieces, decimal? speed, decimal? raw = null, int zero = 0, decimal? dealerOrders = null)
    {
        var kg = pieces * p.UnitKg!.Value;
        var order = dealerOrders is { } d ? StockMath.FactoryOrderKg(d, kg) : speed is { } s ? StockMath.OrderKg(s, kg, p.BoxKg) : 0;
        var orderPieces = order / p.UnitKg.Value;
        var need15 = speed is { } sp ? sp * 15 : (decimal?)null;
        return new StockCell(pieces, kg, StockMath.Boxes(kg, p.BoxKg), pieces * p.Price, speed, raw ?? speed, StockMath.Boxes(speed, p.BoxKg), StockMath.SumOfKg(speed, p.UnitKg, p.Price),
            zero, speed is > 0 ? kg / speed : null, need15, StockMath.Boxes(need15, p.BoxKg), StockMath.SumOfKg(need15, p.UnitKg, p.Price),
            order, StockMath.Boxes(order, p.BoxKg), orderPieces, orderPieces * p.Price, speed is null ? StockStatuses.None : StockStatuses.Of(kg, speed));
    }

    private static StockBase Base()
    {
        var snapshot = new StockSnapshot(
            DateTimeOffset.Parse("2026-10-05T22:36:04Z"), new DateOnly(2026, 10, 6), new DateTime(2026, 10, 6, 3, 36, 4), 2026, 9, new DateOnly(2026, 9, 1),
            new DateOnly(2026, 10, 1), "Дилерга кириш нарх", [Bukhara, Denov], Factory, Export, [new OtherStock(24, "Жиззах (эски)", 100, 100, 3, "Жиззах")],
            new Dictionary<long, StockProduct> { [1] = Bambuk, [2] = Keko, [3] = Tube }, new Dictionary<(long, long), decimal>(), new List<RegionInfo>(), new Dictionary<long, Guid>());

        // Бамбук: Бухоро 300 кг при 10 кг/день (хватит 30 дн., заказ 0); Денов 30 кг при 10 кг/день с поправкой (8 без) → заказ 120 кг = 40 коробок.
        var bambuk = new StockBaseItem(Bambuk, "Бамбук", true, true,
            new Dictionary<string, StockCell> { ["r1"] = Cell(Bambuk, 300, 10), ["r2"] = Cell(Bambuk, 30, 10, 8, 17) },
            Cell(Bambuk, 100, 20, 18, dealerOrders: 120), Cell(Bambuk, 50, null));
        // KEKO: только в Бухоро, 50 кг при 5 кг/день (дефицит, заказ 25 кг → 10 коробок по 2,5); на заводе нет.
        var keko = new StockBaseItem(Keko, "Кекс", true, true,
            new Dictionary<string, StockCell> { ["r1"] = Cell(Keko, 50, 5), ["r2"] = Cell(Keko, 0, 0) },
            Cell(Keko, 0, 5, dealerOrders: 25), Cell(Keko, 0, null));
        // Трубочки (не ТОП): Денов 1 000 пачек = 180 кг без продаж — не продаётся; завод 0.
        var tube = new StockBaseItem(Tube, "Трубочки", true, false,
            new Dictionary<string, StockCell> { ["r1"] = Cell(Tube, 0, 0), ["r2"] = Cell(Tube, 1000, 0) },
            Cell(Tube, 0, 0, dealerOrders: 0), null);

        return new StockBase(snapshot, 87, new DateOnly(2026, 7, 11), new DateOnly(2026, 10, 5), true, [new StockDirection(Rm1, "РМ 1"), new StockDirection(Rm2, "РМ 2")],
            [bambuk, keko, tube], [new StockExcluded(9, "Футболка CandyGold", null, "бонус", StockExcludedReasons.OutsideReport, 12, null, 0)],
            new StockCorrection(2026, 9, 23, 25, 1, 25m / 23 - 1));
    }

    [Fact]
    public void Matrix_gets_speed_and_cover_in_every_unit_and_totals_per_warehouse_from_the_server()
    {
        var view = StockService.Compose(Base(), new StockQuery());

        var bambuk = view.Items.Single(i => i.Code == "002");
        Assert.Equal(20m / 3, bambuk.BoxesPerDay); // 20 кг/день ÷ коробка 3 кг
        Assert.Equal(20m * 22800, bambuk.SumPerDay); // весовой: кг = штуки, × цена единицы
        Assert.Equal(300m, bambuk.Need15Kg);
        Assert.Equal(100m, bambuk.Need15Boxes);
        Assert.Equal(300m * 22800, bambuk.Need15Sum);
        Assert.Null(view.Items.Single(i => i.Code == "493").BoxesPerDay); // коробки нет — и скорости в коробках нет
        Assert.True(view.TopConfigured);

        // Подвал матрицы — итоги по каждому складу охвата, по отфильтрованным строкам.
        Assert.Equal(["r1", "r2"], view.RegionTotals.Keys.Order());
        Assert.Equal(350m, view.RegionTotals["r1"].Kg); // Бухоро: бамбук 300 + KEKO 50
        Assert.Equal(25m, view.RegionTotals["r1"].OrderKg);
        Assert.Equal(210m, view.RegionTotals["r2"].Kg); // Денов: бамбук 30 + трубочки 180
        Assert.Equal(120m, view.RegionTotals["r2"].OrderKg);
        Assert.Equal(["r1"], StockService.Compose(Base(), new StockQuery("region:r1")).RegionTotals.Keys);
        Assert.Empty(StockService.Compose(Base(), new StockQuery("plant")).RegionTotals);
    }

    [Fact]
    public void Dealer_stock_is_the_stock_named_like_the_region_or_its_alias_but_never_an_old_branch_stock()
    {
        var options = new SalesOptions();
        var jizzakh = new RegionInfo(Guid.NewGuid(), 27, "Жиззах", Guid.NewGuid(), null, null);
        var kokand = new RegionInfo(Guid.NewGuid(), 16, "Коканд", null, null, null);
        var navoi = new RegionInfo(Guid.NewGuid(), 15, "Навои", null, null, null);
        (long Id, string Name)[] stocks = [(5, "Завод"), (23, "Экспорт"), (24, "Жиззах (эски)"), (30, "Жиззах "), (31, "Коканд бозор"), (32, "Основной")];

        var regions = StockSnapshotBuilder.MatchDealerStocks([jizzakh, kokand, navoi], stocks, 5, 23, options);

        Assert.Equal(["Жиззах", "Коканд"], regions.Select(r => r.Name)); // у Навои склада в Linko нет
        Assert.Equal(30, regions.Single(r => r.Name == "Жиззах").StockId); // не «Жиззах (эски)», хотя алиас ведёт в Жиззах
        Assert.Equal(jizzakh.DirectionId.ToString(), regions.Single(r => r.Name == "Жиззах").DirectionId);
        Assert.Equal(31, regions.Single(r => r.Name == "Коканд").StockId); // по алиасу «Коканд бозор» → Коканд
        // Без склада «Жиззах» старый склад регион не получает: он — прочий склад («Вся страна» — только 19 дилеров).
        Assert.Empty(StockSnapshotBuilder.MatchDealerStocks([jizzakh], [(24L, "Жиззах (эски)")], 5, 23, options));
    }

    [Fact]
    public void Country_scope_sums_dealer_warehouses_and_keeps_factory_and_export_apart()
    {
        var view = StockService.Compose(Base(), new StockQuery());

        Assert.Equal("country", view.Scope);
        Assert.Equal(["002 Кукурузные палочки, ROHAT BAMBUK, 3-кг", "493 Вафельные трубочки, MIS NAT, 0.18-кг", "318 KEKO декор 2,5 кг"], view.Items.Select(i => i.Name)); // по убыванию кг
        var bambuk = view.Items.Single(i => i.Code == "002");
        Assert.Equal(330m, bambuk.Kg);
        Assert.Equal(110m, bambuk.Boxes);
        Assert.Equal(20m, bambuk.KgPerDay);
        Assert.Equal(18m, bambuk.RawKgPerDay);
        Assert.Equal(120m, bambuk.OrderKg); // только Денов: 150 − 30 = 120 = 40 коробок ровно
        Assert.Equal(40m, bambuk.OrderBoxes);
        Assert.Equal(120m * 22800, bambuk.OrderSum);
        Assert.Equal(StockStatuses.Ok, bambuk.Status); // 330 ÷ 20 = 16,5 дн.
        Assert.Equal(StockStatuses.Deficit, view.Items.Single(i => i.Code == "318").Status); // 10 дн.
        Assert.Equal(StockStatuses.Dead, view.Items.Single(i => i.Code == "493").Status);

        var t = view.Totals;
        Assert.Equal(3, t.Skus);
        Assert.Equal(330m + 50m + 180m, t.Kg);
        Assert.Equal(25m, t.KgPerDay);
        Assert.Equal(23m, t.RawKgPerDay);
        Assert.Equal(120m + 25m, t.OrderKg);
        Assert.Equal(2, t.OrderSkus);
        Assert.Equal(1, t.Deficit);
        Assert.Equal(1, t.Dead);
        Assert.Equal(0, t.WithoutWeight);
        Assert.Equal(1, view.ExcludedOutsideReport);
        // Завод — отдельно: 100 кг бамбука, заказ у завода = 120 − 100 = 20 кг; KEKO на заводе нет — заказ 25.
        Assert.Equal(100m, view.FactoryTotals!.Kg);
        Assert.Equal(45m, view.FactoryTotals.OrderKg);
        Assert.Equal(2, view.FactoryTotals.OrderSkus);
        Assert.Equal(25m / 23 - 1, view.Correction.Change);
        Assert.Equal([0.18m, 2.5m, 3m], view.Packs);
        Assert.Equal(["Бамбук", "Кекс", "Трубочки"], view.Categories);
    }

    [Fact]
    public void Direction_and_region_scopes_take_only_their_warehouses()
    {
        var rm2 = StockService.Compose(Base(), new StockQuery($"rm:{Rm2}"));
        Assert.Equal("РМ 2", rm2.ScopeName);
        Assert.Equal(["r2"], rm2.ScopeRegions.Select(r => r.Id));
        Assert.Equal(["002", "493"], rm2.Items.Select(i => i.Code).Order()); // KEKO в Денове ни остатка, ни продаж
        Assert.Equal(30m, rm2.Items.Single(i => i.Code == "002").Kg);
        Assert.Equal(120m, rm2.Totals.OrderKg);
        Assert.Equal(["r2"], rm2.Items.Single(i => i.Code == "002").Regions.Keys);

        var bukhara = StockService.Compose(Base(), new StockQuery("region:r1"));
        Assert.Equal("Бухоро", bukhara.ScopeName);
        Assert.Equal(["002", "318"], bukhara.Items.Select(i => i.Code).Order());
        Assert.Equal(25m, bukhara.Totals.OrderKg);

        Assert.Equal("Регион не найден", StockService.Compose(Base(), new StockQuery("region:nope")).ScopeName);
        Assert.Equal("country", StockService.Compose(Base(), new StockQuery("whatever")).Scope);
    }

    [Fact]
    public void Plant_scope_shows_factory_stock_country_speed_and_factory_order()
    {
        var plant = StockService.Compose(Base(), new StockQuery("plant"));

        Assert.Equal("plant", plant.Scope);
        Assert.Equal(["002", "318"], plant.Items.Select(i => i.Code).Order()); // трубочки: на заводе 0 и продаж нет
        var bambuk = plant.Items.Single(i => i.Code == "002");
        Assert.Equal(100m, bambuk.Kg);
        Assert.Equal(20m, bambuk.KgPerDay); // скорость всей страны
        Assert.Equal(5m, bambuk.DaysOfCover);
        Assert.Equal(20m, bambuk.OrderKg); // заказы дилеров 120 − остаток завода 100
        Assert.Equal(StockStatuses.Deficit, bambuk.Status);
        Assert.Equal(45m, plant.Totals.OrderKg);
        Assert.Equal(plant.Totals.Kg, plant.FactoryTotals!.Kg);
    }

    [Fact]
    public void Export_scope_has_stock_but_no_speed_no_order_no_status()
    {
        var export = StockService.Compose(Base(), new StockQuery("export"));

        var bambuk = Assert.Single(export.Items); // KEKO на экспорте 0, у трубочек экспорта нет
        Assert.Equal(50m, bambuk.Kg);
        Assert.Null(bambuk.KgPerDay);
        Assert.Null(bambuk.DaysOfCover);
        Assert.Equal(0m, bambuk.OrderKg);
        Assert.Equal(StockStatuses.None, bambuk.Status);
        Assert.Equal(0, export.Totals.Deficit);
    }

    [Fact]
    public void Filters_narrow_rows_and_totals_while_category_tiles_stay_before_the_category_filter()
    {
        var b = Base();

        var byCode = StockService.Compose(b, new StockQuery(Query: "2"));
        Assert.Equal(["002"], byCode.Items.Select(i => i.Code)); // «2» = «002», точный код
        Assert.Equal(330m, byCode.Totals.Kg);

        var byName = StockService.Compose(b, new StockQuery(Query: "keko"));
        Assert.Equal(["318"], byName.Items.Select(i => i.Code));

        var keks = StockService.Compose(b, new StockQuery(Categories: ["Кекс"]));
        Assert.Equal(["318"], keks.Items.Select(i => i.Code));
        Assert.Equal(["Кекс"], keks.SelectedCategories);
        Assert.Equal(50m, keks.Totals.Kg); // итоги — по отфильтрованным строкам
        Assert.Equal(["Бамбук", "Трубочки", "Кекс"], keks.CategoryTiles.Select(t => t.Name)); // плитки — до фильтра по категориям, по убыванию кг
        Assert.True(keks.CategoryTiles.Single(t => t.Name == "Кекс").Selected);
        Assert.Equal(1, keks.CategoryTiles.Single(t => t.Name == "Кекс").Deficit);

        var packs = StockService.Compose(b, new StockQuery(Packs: [2.5m, 0.18m]));
        Assert.Equal(["318", "493"], packs.Items.Select(i => i.Code).Order());

        Assert.Equal(["002", "318"], StockService.Compose(b, new StockQuery(Top: "only")).Items.Select(i => i.Code).Order());
        Assert.Equal(["493"], StockService.Compose(b, new StockQuery(Top: "not")).Items.Select(i => i.Code));
        Assert.Equal(["318"], StockService.Compose(b, new StockQuery(Status: "deficit")).Items.Select(i => i.Code));
        Assert.Equal(["493"], StockService.Compose(b, new StockQuery(Status: "dead")).Items.Select(i => i.Code));
        Assert.Equal("all", StockService.Compose(b, new StockQuery(Status: "weird")).Status);
    }

    /// <summary>Снимок с бронью: Бухоро 100 кг (бронь 40), Денов 30 кг (бронь 30 — всё), завод 100 кг (бронь 100).</summary>
    private static StockSnapshot WithReservations() =>
        new(DateTimeOffset.Parse("2026-10-22T12:00:00Z"), new DateOnly(2026, 10, 22), new DateTime(2026, 10, 22, 17, 0, 0), 2026, 9, new DateOnly(2026, 9, 1),
            new DateOnly(2026, 10, 1), "Дилерга кириш нарх", [Bukhara, Denov], Factory, Export, [], new Dictionary<long, StockProduct> { [1] = Bambuk },
            new Dictionary<(long, long), decimal> { [(1, 15)] = 100, [(1, 14)] = 30, [(1, 5)] = 100 }, new List<RegionInfo>(), new Dictionary<long, Guid>(),
            new Dictionary<(long, long), decimal> { [(1, 15)] = 40, [(1, 14)] = 30, [(1, 5)] = 100 }, DateTimeOffset.Parse("2026-10-22T12:05:00Z"));

    /// <summary>
    /// Бронь = «Забронировано» + «В пути» Linko: заказы «не доставлен», перемещения одобренные, но не отданные, и отданные, но не принятые
    /// получателем (решение 23.10.2026) — всё это уже не свободный товар склада-отправителя.
    /// </summary>
    [Fact]
    public void Reservation_includes_reserved_and_in_transit_documents()
    {
        var options = new SalesOptions();
        Assert.Equal(["not_delivered"], options.ReservedOrderStatuses);
        Assert.Equal(["approved", "given"], options.ReservedTransferStatuses);
    }

    /// <summary>
    /// Бронь (как в «Полевом контроле»): у дилера она уже продана магазинам — «хватит, дн.», статус и заказ считаются от доступного (остаток − бронь);
    /// у завода бронь — отгрузки по тем же заказам дилеров, поэтому заказ у завода считается от всего остатка, иначе бронь заказали бы дважды.
    /// </summary>
    [Fact]
    public void Reservation_lowers_cover_and_dealer_order_but_not_the_factory_order()
    {
        var s = WithReservations();

        // Бухоро: доступно 60 кг при 10 кг/день — хватит на 6 дн.; заказ до 15 дней — 150 − 60 = 90 кг = 30 коробок (без брони было бы 17).
        var dealer = StockService.Cell(s, Bambuk, Bukhara.StockId, 10, 10, 0, 10, null);
        Assert.Equal((100m, 40m, 60m, 6m, 90m), (dealer.Kg!.Value, dealer.ReservedKg, dealer.FreeKg!.Value, dealer.DaysOfCover!.Value, dealer.OrderKg));
        Assert.Equal(StockStatuses.Deficit, dealer.Status);
        Assert.Equal(40m / 3, dealer.ReservedBoxes);
        Assert.Equal(40m * 22800, dealer.ReservedSum);

        // Завод: всё в брони, заказы дилеров 120 кг — не хватает 120 − 100 = 20 кг (от всего остатка), доступно 0.
        var factory = StockService.Cell(s, Bambuk, Factory.StockId, 20, 20, 0, null, 120);
        Assert.Equal((0m, 0m, 20m), (factory.FreeKg!.Value, factory.DaysOfCover!.Value, factory.OrderKg));
    }

    /// <summary>Бронь и доступное складываются в строке охвата и итогах (дни запаса — от доступного) и приходят в автозаказ — со складом завода.</summary>
    [Fact]
    public void Reservation_reaches_rows_totals_and_auto_order()
    {
        var s = WithReservations();
        var item = new StockBaseItem(Bambuk, "Бамбук", true, false,
            new Dictionary<string, StockCell>
            {
                ["r1"] = StockService.Cell(s, Bambuk, Bukhara.StockId, 10, 10, 0, 10, null),
                ["r2"] = StockService.Cell(s, Bambuk, Denov.StockId, 10, 10, 0, 10, null),
            },
            StockService.Cell(s, Bambuk, Factory.StockId, 20, 20, 0, null, 240), null);
        var b = new StockBase(s, 87, new DateOnly(2026, 7, 27), new DateOnly(2026, 10, 21), false, [], [item], [], new StockCorrection(2026, 9, 20, 20, 0, 0));

        var view = StockService.Compose(b, new StockQuery());
        var row = Assert.Single(view.Items);
        Assert.Equal((130m, 70m, 60m, 3m), (row.Kg!.Value, row.ReservedKg, row.FreeKg!.Value, row.DaysOfCover!.Value));
        Assert.Equal((70m, 60m, 3m), (view.Totals.ReservedKg, view.Totals.FreeKg, view.Totals.DaysOfCover!.Value));
        Assert.Equal(s.ReservedAt, view.ReservedAt);
        Assert.Equal(140m, view.FactoryTotals!.OrderKg); // 240 − 100: бронь завода второй раз не заказывается

        var order = StockService.ComposeAutoOrder(b, "r2");
        var line = Assert.Single(order.Items);
        Assert.Equal((30m, 30m, 0m, 50, 100m, 0m), (line.StockKg, line.ReservedKg, line.FreeKg!.Value, line.RecBoxes, line.FactoryKg!.Value, line.FactoryFreeKg!.Value));
        Assert.Equal(s.ReservedAt, order.ReservedAt);
    }

    [Fact]
    public void Packs_from_the_address_are_parsed_with_either_separator()
    {
        Assert.Equal([3m, 2.5m, 0.18m], StockService.ParsePacks("3,2.5,0.18"));
        Assert.Empty(StockService.ParsePacks(" "));
        Assert.Equal(("rm", "abc"), StockService.ParseScope("rm:abc"));
        Assert.Equal(("country", null), StockService.ParseScope("rm:"));
    }

    /// <summary>
    /// Как в «Полевом контроле»: ни остатка, ни продаж — «нет остатка», а не «норма»; ТОП-товар, которого у дилера нет вовсе, —
    /// стартовый заказ 10 коробок (иначе по скорости заказ был бы нулём навсегда).
    /// </summary>
    [Fact]
    public void Empty_pair_status_and_top_start_order()
    {
        Assert.Equal(StockStatuses.Empty, StockStatuses.Of(0, 0));
        Assert.Equal(StockStatuses.Empty, StockStatuses.Of(0, null));
        Assert.Equal(StockStatuses.Dead, StockStatuses.Of(5, 0));
        Assert.Equal(StockStatuses.Deficit, StockStatuses.Of(5, 1));

        Assert.Equal(30m, StockMath.TopStartKg(0, 0, 3));
        Assert.Null(StockMath.TopStartKg(0.5m, 0, 3)); // продаётся — заказ по скорости
        Assert.Null(StockMath.TopStartKg(0, 2, 3));    // лежит остаток
        Assert.Null(StockMath.TopStartKg(0, 0, null)); // коробки не знаем
    }

    /// <summary>«Собрать в Excel»: столбцы — по ключам в порядке каталога, неизвестные отброшены, пусто — как в таблице; дни и цена не складываются.</summary>
    [Fact]
    public void Excel_columns_come_from_the_catalog()
    {
        Assert.Equal(["name", "stockKg", "orderKg"], StockExport.Parse("orderKg, name ,unknown,stockKg").Select(c => c.Key));
        Assert.Equal(StockExport.DefaultKeys, StockExport.Parse(null).Select(c => c.Key));
        Assert.Equal(StockExport.DefaultKeys, StockExport.Parse("nothing").Select(c => c.Key));
        Assert.False(StockExport.Columns.Single(c => c.Key == "days").Sum);
        Assert.False(StockExport.Columns.Single(c => c.Key == "price").Sum);
        Assert.True(StockExport.Columns.Single(c => c.Key == "orderKg").Sum);
        Assert.All(StockExport.Columns, c => Assert.False(string.IsNullOrWhiteSpace(c.Uz)));
        Assert.Equal("дефицит", StockExport.StatusLabel(StockStatuses.Deficit));
    }

    /// <summary>Единица Linko и единиц в коробке: «кг» — вес коробки, «шт» — штук из названия, иначе коробка ÷ штука; строка акта — коробки × единиц × цена.</summary>
    [Fact]
    public void Auto_order_units_per_box_and_act_row()
    {
        Assert.Equal(3m, AutoOrderMath.UnitsPerBox("кг", null, 1m, 3m));
        Assert.Equal(28m, AutoOrderMath.UnitsPerBox("шт", new PackInfo(0.98m, 28, 0.035m), 0.035m, 0.98m));
        Assert.Equal(10m, AutoOrderMath.UnitsPerBox("шт", null, 0.4m, 4m));
        Assert.Null(AutoOrderMath.UnitsPerBox("шт", null, null, 4m));
        Assert.Null(AutoOrderMath.UnitsPerBox("кг", null, 1m, null));
        Assert.Equal("шт", AutoOrderMath.UnitOf(" ШТ ", 1m));
        Assert.Equal("кг", AutoOrderMath.UnitOf(null, 1m));
        Assert.Equal("шт", AutoOrderMath.UnitOf(null, 0.4m));

        var bamboo = new AutoOrderItem(494, "494", "494 Хрустящие палочки Bamboo (400г) 10 шт.", "Бамбук", true, "шт", 10, 4, 9396, 129, 63.6m, 38.6m);
        Assert.Equal(new TransferActRow(bamboo.Name, 129, "шт", 1290, 9396, 12_120_840), AutoOrderMath.Row(bamboo, 129));
        var keko = bamboo with { Unit = "кг", UnitsPerBox = 2.5m, BoxKg = 2.5m, Price = null };
        Assert.Equal(new TransferActRow(keko.Name, 3, "кг", 7.5m, null, null), AutoOrderMath.Row(keko, 3));
    }

    /// <summary>
    /// Автозаказ региона: товары с коробкой (трубочки без коробки — в счётчике), рекомендация — заказ склада в коробках, рекомендованные —
    /// первыми; без региона — первый по алфавиту.
    /// </summary>
    [Fact]
    public void Auto_order_recommends_boxes_for_the_region()
    {
        var denov = StockService.ComposeAutoOrder(Base(), "R2");
        Assert.Equal(("r2", "Денов"), (denov.RegionId, denov.RegionName));
        Assert.Equal(["Бухоро", "Денов"], denov.Regions.Select(r => r.Name));
        Assert.Equal(1, denov.WithoutBox);
        Assert.Equal(15, denov.CoverDays);
        Assert.Equal([(1L, 40), (2L, 0)], denov.Items.Select(i => (i.ProductId, i.RecBoxes))); // 120 кг ÷ 3 кг
        var bambuk = denov.Items[0];
        Assert.Equal(("кг", 3m, 22800m, 30m), (bambuk.Unit, bambuk.UnitsPerBox, bambuk.Price, bambuk.StockKg));

        var first = StockService.ComposeAutoOrder(Base(), null);
        Assert.Equal("Бухоро", first.RegionName);
        Assert.Equal([(2L, 10), (1L, 0)], first.Items.Select(i => (i.ProductId, i.RecBoxes))); // KEKO: 25 кг ÷ 2,5 кг
    }

    /// <summary>
    /// Акт из автозаказа: с клиента только коробки — товары без коробки, чужие и нулевые отбрасываются, строки — по категории и названию,
    /// цены и единицы — свои; файл — Excel по бланку Linko, суммы — числами.
    /// </summary>
    [Fact]
    public void Transfer_act_takes_only_boxes_from_the_client_and_is_an_excel_sheet()
    {
        var input = new AutoOrderActInput("r2", null, [new(2, 4), new(1, 40), new(3, 5), new(99, 1), new(1, 0)]);
        var act = StockService.ComposeAct(Base(), input, "Ritm Admin", new DateOnly(2026, 10, 22));
        Assert.Equal(("Завод", "Денов", new DateOnly(2026, 10, 22), "Ritm Admin"), (act.From, act.To, act.Date, act.Sender));
        Assert.Equal(
            [new TransferActRow(Bambuk.Name, 40, "кг", 120, 22800, 2_736_000), new TransferActRow(Keko.Name, 4, "кг", 10, 33500, 335_000)],
            act.Rows);
        Assert.Equal(3_071_000m, act.Total);
        Assert.Equal(new DateOnly(2026, 11, 1), StockService.ComposeAct(Base(), input with { Date = new DateOnly(2026, 11, 1) }, "x", default).Date);

        Assert.Throws<ArgumentException>(() => StockService.ComposeAct(Base(), input with { RegionId = " " }, "x", default));
        Assert.Throws<KeyNotFoundException>(() => StockService.ComposeAct(Base(), input with { RegionId = "nope" }, "x", default));
        Assert.Throws<ArgumentException>(() => StockService.ComposeAct(Base(), input with { Lines = [new(3, 5), new(1, 0)] }, "x", default));

        var bytes = new OneBase.Infrastructure.Reports.TransferActWorkbookWriter().Write(act);
        using var workbook = new ClosedXML.Excel.XLWorkbook(new MemoryStream(bytes));
        var cells = workbook.Worksheet(1).CellsUsed().ToList();
        var text = string.Join("\n", cells.Select(c => c.GetString()));
        foreach (var expected in new[] { "ПРИЕМА-ПЕРЕДАЧИ", "Завод → Денов", "22 Октябрь 2026", "передает Ritm Admin", "Итого:", "Менеджер по продажам", "Сдал", "Принял" })
        {
            Assert.Contains(expected, text);
        }

        var bambuk = cells.Single(c => c.GetString() == Bambuk.Name).WorksheetRow();
        Assert.Equal((40d, 120d, 2_736_000d), (bambuk.Cell(2).GetDouble(), bambuk.Cell(4).GetDouble(), bambuk.Cell(6).GetDouble()));
        Assert.Equal(3_071_000d, cells.Single(c => c.GetString() == "Итого:").WorksheetRow().Cell(6).GetDouble());
    }
}
