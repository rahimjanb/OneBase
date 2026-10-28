using OneBase.Application.Abstractions;
using OneBase.Application.DataSources;
using OneBase.Application.Sales.Settlements;
using OneBase.Infrastructure.DataSources;

namespace OneBase.Sales.Tests;

/// <summary>Взаиморасчёты с дилерами: остаток на дату, FIFO, фактический DSO, кешбэк, «Акт сверки» и DSO, разбор реестров Google Sheets.</summary>
public class SettlementsTests
{
    private static DateOnly D(int month, int day, int year = 2026) => new(year, month, day);

    private static DealerBook Book(
        string name,
        DateOnly? anchorDate = null,
        decimal? anchor = null,
        SettlementDoc[]? docs = null,
        SettlementPayment[]? payments = null,
        SettlementExpense[]? expenses = null,
        int? norm = 20,
        bool inTerms = true,
        string? actGroup = null) =>
        new(name.ToLowerInvariant(), name, "dealer", norm, inTerms, anchorDate, anchor, anchor is null ? null : "act", docs ?? [], payments ?? [], expenses ?? [], actGroup);

    private static SettlementExpense Expense(DateOnly date, decimal amount, string code) => new(date, amount, code, SettlementMath.ArtOf(code), null);

    [Fact]
    public void Article_codes_map_to_act_columns_and_wallets_to_cash_or_bank()
    {
        Assert.Equal("601", SettlementMath.CodeOf("601-Дилер бонуслари"));
        Assert.Equal("retro", SettlementMath.ArtOf("601"));
        Assert.Equal("salary", SettlementMath.ArtOf("602"));
        Assert.Equal("other", SettlementMath.ArtOf("604"));
        Assert.Equal("logi", SettlementMath.ArtOf("606"));
        Assert.Equal("promo", SettlementMath.ArtOf("609"));
        Assert.True(SettlementMath.IsCash("Нақд"));
        Assert.False(SettlementMath.IsCash("Банк"));
        Assert.False(SettlementMath.IsCash(null));
    }

    /// <summary>Остаток на дату — от опоры (акт бухгалтерии на 01.09) вперёд и назад по движению.</summary>
    [Fact]
    public void Balance_moves_forward_and_back_from_the_anchor()
    {
        var book = Book("Азизбек", D(9, 1), 1000,
            [new(D(8, 10), 300, "t:1"), new(D(9, 5), 500, "t:2"), new(D(9, 10), 100, "t:3", Return: true)],
            [new(D(8, 20), 200, false, null), new(D(9, 15), 400, true, null)],
            [Expense(D(9, 30), 50, "606")]);

        Assert.Equal(950, SettlementMath.BalanceAt(book, D(10, 1)));
        Assert.Equal(900, SettlementMath.BalanceAt(book, D(8, 1)));
    }

    /// <summary>FIFO: оплаты гасят самые старые отгрузки; фактический DSO — взвешенный по сумме срок закрытия; кешбэк — 2% закрытых в норму.</summary>
    [Fact]
    public void Payments_close_oldest_shipments_first()
    {
        var book = Book("Бобур", docs: [new(D(7, 1), 100_000, "t:1"), new(D(7, 10), 200_000, "t:2"), new(D(7, 20), 300_000, "t:3")],
            payments: [new(D(7, 5), 50_000, true, null), new(D(7, 25), 300_000, false, null)]);

        var items = SettlementMath.Fifo(book, D(7, 1), D(7, 31));

        Assert.Equal([D(7, 25), D(7, 25), null], items.Select(i => i.Closed));
        Assert.Equal(250_000, items[2].Remaining);
        var (fact, late) = SettlementMath.Fact(items, D(7, 31), 20);
        Assert.Equal(18.0m, fact); // (100 000 × 24 + 200 000 × 15) / 300 000
        Assert.Equal(0.333m, late); // закрыта позже 20 дней — первая отгрузка
        var cashback = Assert.Single(SettlementMath.Cashback(items, 2026, 7, 20));
        Assert.Equal(("t:2", 15, 4_000m), (cashback.Item.Doc, cashback.Dso, cashback.Amount));
    }

    /// <summary>Долг на начало FIFO раскладывается на последние отгрузки до него; переплата — аванс, гасит следующую отгрузку.</summary>
    [Fact]
    public void Opening_debt_sits_on_the_latest_shipments_and_overpayment_is_an_advance()
    {
        var owing = Book("Жасур", D(9, 1), 250_000, [new(D(8, 10), 100_000, "t:1"), new(D(8, 20), 200_000, "t:2")], [new(D(9, 5), 100_000, false, null)]);
        var items = SettlementMath.Fifo(owing, D(9, 1), D(9, 30));
        Assert.Equal([(D(8, 10), 0m, (DateOnly?)D(9, 5)), (D(8, 20), 150_000m, null)], items.Select(i => (i.Date, i.Remaining, i.Closed)));

        var ahead = Book("Сохиб", D(9, 1), -100_000, [new(D(9, 3), 150_000, "t:3")]);
        var item = Assert.Single(SettlementMath.Fifo(ahead, D(9, 1), D(9, 30)));
        Assert.Equal(50_000, item.Remaining);
    }

    private static SettlementsBase Base(DateOnly asOf, params DealerBook[] books) =>
        new(asOf, books, true, true, true, true, D(8, 1), asOf, D(9, 30), ["Диллер", "Ритейл"]);

    /// <summary>
    /// Акт по акту бухгалтерии: строки и группы — его контрагенты (и пустые), прочие — в проверке «outside»; остаток месяца — по цепочке от
    /// опоры; ретробонус — 601 за вычетом кешбэка, кешбэк сверх 601 — в расходы акта и в проверку.
    /// </summary>
    [Fact]
    public void Act_rows_follow_the_accounting_act()
    {
        var data = Base(D(10, 20),
            Book("Азизбек", D(9, 1), 1_000_000, [new(D(9, 5), 500_000, "t:1")], [new(D(9, 10), 300_000, false, null)],
                [Expense(D(9, 30), 20_000, "601"), Expense(D(9, 30), 10_000, "606")], actGroup: "Диллер"),
            Book("Актам", D(9, 1), 0, [new(D(9, 2), 1_000_000, "t:2")], [new(D(9, 15), 1_000_000, true, null)], [Expense(D(9, 30), 30_000, "601")], actGroup: "Диллер"),
            Book("Афзал", D(9, 1), 0, [new(D(9, 2), 1_000_000, "t:3")], [new(D(9, 15), 1_000_000, true, null)], [Expense(D(9, 30), 5_000, "601")], actGroup: "Диллер"),
            Book("Хавас", D(9, 1), 0, actGroup: "Ритейл"),
            Book("Андижон", docs: [new(D(9, 12), 70_000, "t:4")]));

        var september = AktCompute.Build(data, "2026-9");

        Assert.Equal([("Диллер", "Дилеры", 3), ("Ритейл", "Ритейл", 1)], september.Groups.Select(g => (g.Key, g.Name, g.Rows.Count)));
        var rows = september.Groups.SelectMany(g => g.Rows).ToDictionary(r => r.Name);
        Assert.Equal((1_000_000m, 500_000m, 300_000m, 20_000m, 10_000m, 30_000m, 1_170_000m),
            (rows["Азизбек"].Open, rows["Азизбек"].Ship, rows["Азизбек"].Bank, rows["Азизбек"].Expenses["retro"], rows["Азизбек"].Expenses["logi"], rows["Азизбек"].Total, rows["Азизбек"].Close));
        Assert.Equal((20_000m, 10_000m, -30_000m), (rows["Актам"].Expenses["cashback"], rows["Актам"].Expenses["retro"], rows["Актам"].Close));
        Assert.Equal((20_000m, 0m, 15_000m), (rows["Афзал"].Expenses["cashback"], rows["Афзал"].Expenses["retro"], rows["Афзал"].CashbackPending));
        Assert.Equal(0, rows["Хавас"].Close);
        var ru = System.Globalization.CultureInfo.GetCultureInfo("ru-RU");
        Assert.Equal([$"Андижон — {70_000m.ToString("N0", ru)}"], september.Checks.Single(c => c.Kind == "outside").Items);
        Assert.Equal([$"Афзал — {15_000m.ToString("N0", ru)}"], september.Checks.Single(c => c.Kind == "cashback").Items);
        // Расходы месяца внесены не до конца — 601 ещё не провели, «кешбэк сверх 601» не сигнал.
        Assert.DoesNotContain(AktCompute.Build(data with { ExpensesTo = D(9, 29) }, "2026-9").Checks, c => c.Kind == "cashback");

        Assert.Equal(1_170_000, AktCompute.Build(data, "2026-10").Groups[0].Rows.Single(r => r.Name == "Азизбек").Open);
        Assert.Equal(1_000_000, AktCompute.Build(data, "2026-8").Groups[0].Rows.Single(r => r.Name == "Азизбек").Close);
    }

    /// <summary>DSO: дилеры с условиями; со сроком — просрочка по норме, без нормы — «без срока», без отгрузок 60+ дней с долгом — «не работают».</summary>
    [Fact]
    public void Dso_statuses_and_overdue()
    {
        var data = Base(D(10, 10),
            Book("Сирдарё", docs: [new(D(9, 1), 100_000, "t:1")]),
            Book("Бахтиёр", docs: [new(D(10, 1), 50_000, "t:2")], norm: null),
            Book("Билолиддин", docs: [new(D(7, 1), 80_000, "t:3")]),
            Book("Макро", docs: [new(D(9, 1), 30_000, "t:4")], inTerms: false));

        var view = DsoCompute.Build(data);

        Assert.Equal([("Сирдарё", "active"), ("Бахтиёр", "noterm"), ("Билолиддин", "gone")], view.Dealers.Select(d => (d.Name, d.Status)));
        var active = view.Dealers[0];
        var open = Assert.Single(active.Open);
        Assert.Equal((39, D(9, 21), 19), (open.Age, open.Due, open.Late));
        Assert.Equal(100_000, active.Overdue);
        Assert.Equal((230_000m, 100_000m), (view.Debt, view.Overdue));
        Assert.Null(view.Dealers[2].Weeks); // ушедшие в прогноз не входят
    }

    private static ReportDocument Sheet(string name, params string[][] rows) =>
        new([new ReportSheet(name, rows.Select(r => (IReadOnlyList<string>)r).ToList(), rows.Length, rows.Max(r => r.Length), false)]);

    /// <summary>Акт бухгалтерии: дата — из ячейки F1; заголовки групп («Диллер», «Ритейл») — группа строк ниже, «Итого» не контрагент.</summary>
    [Fact]
    public void Balances_keep_act_groups_and_skip_headers()
    {
        var document = Sheet("АКТ-СВЕРКА",
            ["", "", "", "", "", "2026-09-01"],
            ["№", "Контрагент", "Остаток"],
            ["", "Диллер", "1500"],
            ["1", "Азизбек Хоразм", "1000"],
            ["2", "Бобур Самарканд", "500"],
            ["", "Ритейл", "280"],
            ["1", "Хавас", "300"],
            ["", "", ""],
            ["", "КПП офис", "-20"],
            ["", "Итого", "1780"]);

        var rows = DataSourceRegisters.Balances(DataSourceCatalog.Field(DataSourceCatalog.DealerBalances)!, Guid.NewGuid(), 2,
            new BalanceFieldMapping("Контрагент", "Остаток", "F1"), document, ["АКТ-СВЕРКА"]);

        Assert.Equal(
            [("Азизбек Хоразм", 1000m, "Диллер"), ("Бобур Самарканд", 500m, "Диллер"), ("Хавас", 300m, "Ритейл"), ("КПП офис", -20m, "Ритейл")],
            rows.Select(r => (r.Dealer, r.Amount, r.Category)));
        Assert.All(rows, r => Assert.Equal(D(9, 1), r.Date));
    }

    /// <summary>Реестр оплат: отбор по началу статьи (101, 103), строки раньше DateFrom (остатки на начало расходом) и «Итого» не берутся.</summary>
    [Fact]
    public void Ledger_filters_categories_and_rows_before_date_from()
    {
        var document = Sheet("Kassa Bank",
            ["Sana", "Koshelok", "Kategoriya", "Subkategoriya", "Izoh", "Сумма"],
            ["2025-07-31", "Банк", "101-Махаллий сотувдан тушум", "Бобур Самарканд", "", "-1303461206"],
            ["2025-08-02", "Нақд", "101-Махаллий сотувдан тушум", "Бобур Самарканд", "Бобур", "100000000"],
            ["2026-09-17", "Банк", "103-Тармокли мижоздардан тушум", "Хавас", "HAVAS FOOD", "46216660"],
            ["2026-09-18", "Банк", "102-Экспорт", "Экспорт", "", "500"],
            ["2026-09-19", "Нақд", "101-Махаллий сотувдан тушум", "Итого", "", "999"]);

        var rows = DataSourceRegisters.Ledger(DataSourceCatalog.Field(DataSourceCatalog.DealerPayments)!, Guid.NewGuid(), 1,
            new LedgerFieldMapping("Sana", "Subkategoriya", "Сумма", "Koshelok", "Kategoriya", ["101", "103"], "Izoh", D(8, 1, 2025)), document, ["Kassa Bank"]);

        Assert.Equal([(D(8, 2, 2025), "Бобур Самарканд", 100_000_000m, true), (D(9, 17), "Хавас", 46_216_660m, false)],
            rows.Select(r => (r.Date, r.Dealer, r.Amount, SettlementMath.IsCash(r.Kind))));
    }

    /// <summary>Отгрузки ГП («Сотув маълумотлар Базаси»): сумма в листе всегда положительная, возврат — по «П/В»; строка «Итого» не берётся.</summary>
    [Fact]
    public void Sheet_shipments_take_the_return_sign_from_the_operation_column()
    {
        var document = Sheet("Сотув маълумотлар Базаси",
            ["Дата", "П/В", "Контрагент", "Сумма Диллера", "Коментария"],
            ["2026-09-01", "Продажа", "Дилер Хоразм", "1000", ""],
            ["2026-09-02", "Возврат", "Дилер Хоразм", "200", "брак"],
            ["2026-09-03", "Продажа", "Итого", "999", ""]);

        var rows = DataSourceRegisters.Ledger(DataSourceCatalog.Field(DataSourceCatalog.DealerShipments)!, Guid.NewGuid(), 1,
            new LedgerFieldMapping("Дата", "Контрагент", "Сумма Диллера", "П/В", null, null, "Коментария"), document, ["Сотув маълумотлар Базаси"]);

        Assert.Equal([(D(9, 1), 1000m), (D(9, 2), -200m)], rows.Select(r => (r.Date, r.Amount)));
        Assert.True(DataSourceRegisters.IsReturn("возврат товара"));
        Assert.False(DataSourceRegisters.IsReturn("Продажа"));
        Assert.Contains(DataSourceCatalog.DealerShipments, DataSourceCatalog.Section("akt")!.Fields);
        Assert.Contains(DataSourceCatalog.DealerShipments, DataSourceCatalog.Section("dso")!.Fields);
    }
}
