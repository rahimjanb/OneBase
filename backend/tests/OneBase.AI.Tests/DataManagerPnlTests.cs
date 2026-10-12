using OneBase.Application.Abstractions;
using OneBase.Application.DataManager;
using OneBase.Application.Sales.Pnl;
using OneBase.Domain.AI;

namespace OneBase.AI.Tests;

/// <summary>AI Data Manager → P&L дилеров без базы: чтение реестра, базы продаж и таблицы P&L, план изменений, сверка расчётных строк.</summary>
public class DataManagerPnlTests
{
    private static ReportDocument Doc(string sheet, params string[][] rows) =>
        new([new ReportSheet(sheet, rows.Select(r => (IReadOnlyList<string>)r).ToList(), rows.Length, rows.Max(r => r.Length), false)]);

    /// <summary>Реестр «Прочие расходы» как в ГП-таблице: Год, Месяц, Сумма, Диллер, Статья — строка на операцию.</summary>
    private static readonly ReportDocument Registry = Doc("Прочие расходы",
        ["Год", "Месяц", "Сумма", "Диллер", "Статья"],
        ["2026", "9", "1 000", "Бобур Самарканд", "601-Дилер бонуслари"],
        ["2026", "9", "500", "бобур  самарканд", "609-Акция ва скидкалар"],
        ["2026", "9", "300", "Бобур Самарканд", "606-Логистика"],
        ["2026", "9", "200", "Азизбек Хоразм", "339-Утилизация"],
        ["2026", "9", "75", "Азизбек Хоразм", "605-Новая статья"],
        ["", "", "2 075", "Итого", ""]);

    private static readonly PnlSheetMapping RegistryMap = new("Прочие расходы", 1, "Диллер", null, "Статья", "Сумма", "Год", "Месяц", null, null, SumRows: true);

    private static PnlPlanningResult Plan(IReadOnlyList<PnlRawValue> values, bool replace = false, bool sumRows = true,
        IReadOnlyList<ExistingPnlRow>? existing = null, IReadOnlyDictionary<string, string>? known = null,
        IReadOnlyDictionary<string, string>? dealerMap = null, IReadOnlyDictionary<string, string>? lineMap = null) =>
        PnlPlanner.Plan(replace, sumRows, values, lineMap, dealerMap, existing ?? [], known ?? new Dictionary<string, string>());

    private static PlannedItem Item(PnlPlanningResult r, string dealer, string line) =>
        Assert.Single(r.Items, i => i.EntityKey == PnlPlanner.Key(2026, 9, line, dealer));

    [Fact]
    public void Registry_ArticlesToLines_RowsSummed_TotalsSkipped()
    {
        var values = PnlExtraction.Values(Registry, RegistryMap);
        Assert.DoesNotContain(values, v => v.Dealer == "Итого");

        var r = Plan(values);
        Assert.Equal("1500", Item(r, "Бобур Самарканд", PnlMath.Sm).After); // 601 + 609, имя без учёта регистра и пробелов
        Assert.Equal("сумма 2 строк отчёта", Item(r, "Бобур Самарканд", PnlMath.Sm).Message);
        Assert.Equal("300", Item(r, "Бобур Самарканд", PnlMath.Ship606).After);
        Assert.Equal("200", Item(r, "Азизбек Хоразм", PnlMath.Admin).After);
        Assert.Equal("75", Item(r, "Азизбек Хоразм", PnlMath.Other).After);
        Assert.All(r.Items, i => Assert.Equal(DataChangeAction.Create, i.Action));

        // Итог месяца по формулам OneBase: доставка Бобура — 606 (рейсов нет), EBIT = −(1500 + 300) − (200 + 75)
        var month = Assert.Single(r.Summary.Months);
        Assert.Equal(-2075m, month.After[PnlMath.Ebit]);
        Assert.Null(month.Before[PnlMath.Ebit]);
    }

    [Fact]
    public void RepeatedRowsWithoutSumRows_AreConflict()
    {
        var r = Plan(PnlExtraction.Values(Registry, RegistryMap with { SumRows = false }), sumRows: false);
        var conflict = Item(r, "Бобур Самарканд", PnlMath.Sm);
        Assert.Equal(DataChangeAction.Conflict, conflict.Action);
        Assert.Contains("sumRows", conflict.Message);
        Assert.StartsWith("Конфликтов: 1", r.Warnings[0]);
    }

    [Fact]
    public void SalesBase_ReturnsNegative_FactoryAndDealerSums()
    {
        var doc = Doc("База",
            ["Месяц", "Диллер", "Тип", "Сумма завода", "Сумма Диллера"],
            ["сентябрь 2026", "Бобур Самарканд", "Сотув", "1 000", "1 100"],
            ["сентябрь 2026", "Бобур Самарканд", "Возврат", "100", "110"],
            ["сентябрь 2026", "Бобур Самарканд", "Сотув", "2 000", "2 200"]);
        var values = PnlExtraction.Values(doc, new PnlSheetMapping("База", 1, "Диллер",
            [new PnlColumn("Сумма Диллера", "sales"), new PnlColumn("Сумма завода", "cost")], null, null, null, "Месяц", null, null,
            ReturnColumn: "Тип", ReturnValues: ["возврат"], SumRows: true));

        var r = Plan(values);
        Assert.Equal("3190", Item(r, "Бобур Самарканд", PnlMath.Sales).After); // 1100 + 2200 − 110
        Assert.Equal("2900", Item(r, "Бобур Самарканд", PnlMath.Cost).After); // возврат уменьшает себестоимость
        Assert.Contains("(возврат)", Item(r, "Бобур Самарканд", PnlMath.Sales).SourceRef);
    }

    /// <summary>Выгрузка «P&L таблицей»: строки — статьи, столбцы — дилеры и «Итого». Расчётные строки не пишутся, а сверяются.</summary>
    [Fact]
    public void PnlTable_LinesInRows_ComputedLinesChecked()
    {
        var doc = Doc("P&L",
            ["Строка P&L", "Бобур Самарканд", "Азизбек Хоразм", "Итого"],
            ["Продажи", "1 000", "2 000", "3 000"],
            ["Себестоимость товара", "900", "1 900", "2 800"],
            ["Валовая прибыль", "100", "90", "190"],
            ["Маржа", "10%", "5,0%", ""],
            ["ROA рентабельность активов", "—", "—", "—"],
            ["EBIT операционная прибыль", "100", "100", "200"]);
        var values = PnlExtraction.Values(doc, new PnlSheetMapping("P&L", 1, null, null, "Строка P&L", null, null, null, 2026, 9,
            FirstDealerColumn: "B", LastDealerColumn: "D"));

        var r = Plan(values, sumRows: false);
        Assert.Equal(4, r.Items.Count); // продажи и себестоимость двух дилеров; «Итого» и расчётные строки не загружаются
        Assert.All(r.Items, i => Assert.True(i.EntityKey.Contains(":sales:") || i.EntityKey.Contains(":cost:")));

        Assert.Equal(6, r.Summary.CheckedValues); // валовая, маржа, EBIT × 2; ROA пропущен
        var mismatch = Assert.Single(r.Summary.Checks);
        Assert.Equal("Азизбек Хоразм", mismatch.Dealer);
        Assert.Equal(PnlMath.Gross, mismatch.Line);
        Assert.Equal(90, mismatch.Report);
        Assert.Equal(100, mismatch.OneBase);
        Assert.Contains(r.Warnings, w => w.Contains("не совпадают с расчётом OneBase: 1 из 6"));
    }

    [Fact]
    public void Replace_RemovesOnlySameLinesOfSameMonth()
    {
        var existing = new List<ExistingPnlRow>
        {
            new(2026, 9, "Бобур Самарканд", "бобур самарканд", PnlMath.Sm, 700),
            new(2026, 9, "Каримов", "каримов", PnlMath.Sm, 300),
            new(2026, 9, "Каримов", "каримов", PnlMath.Sales, 5000),
            new(2026, 8, "Каримов", "каримов", PnlMath.Sm, 100),
        };
        var values = new List<PnlRawValue> { new("Бобур Самарканд", "601", 2026, 9, 700, "A2") };

        var upsert = Plan(values, existing: existing);
        Assert.Equal(DataChangeAction.Unchanged, Assert.Single(upsert.Items).Action);

        var replace = Plan(values, replace: true, existing: existing);
        var removed = Assert.Single(replace.Items, i => i.Action == DataChangeAction.Delete);
        Assert.Equal(PnlPlanner.Key(2026, 9, PnlMath.Sm, "Каримов"), removed.EntityKey); // продажи Каримова и август не тронуты
        Assert.Equal(-700m - 300m + 5000m, replace.Summary.Months[0].Before[PnlMath.Ebit]);
        Assert.Equal(5000m - 700m, replace.Summary.Months[0].After[PnlMath.Ebit]);
    }

    [Fact]
    public void ZeroDeletes_SameValueUnchanged_InvalidAndUnknownAreConflicts()
    {
        var existing = new List<ExistingPnlRow>
        {
            new(2026, 9, "Бобур Самарканд", "бобур самарканд", PnlMath.Admin, 50),
            new(2026, 9, "Бобур Самарканд", "бобур самарканд", PnlMath.Sales, 1000),
        };
        var r = Plan([
            new("Бобур Самарканд", "admin", 2026, 9, 0, "A1"),
            new("Бобур Самарканд", "Продажи", 2026, 9, 1000, "A2"),
            new("Бобур Самарканд", "Непонятно что", 2026, 9, 10, "A3"),
            new("Бобур Самарканд", "Налоги", 2026, 9, null, "A4", "сорок"),
            new("Бобур Самарканд", "Налоги", 2026, 13, 5, "A5"),
        ], sumRows: false, existing: existing);

        Assert.Equal(DataChangeAction.Delete, Item(r, "Бобур Самарканд", PnlMath.Admin).Action);
        Assert.Equal(DataChangeAction.Unchanged, Item(r, "Бобур Самарканд", PnlMath.Sales).Action);
        var conflicts = r.Items.Where(i => i.Action == DataChangeAction.Conflict).ToList();
        Assert.Equal(3, conflicts.Count);
        Assert.Contains(conflicts, c => c.Message!.Contains("не распознана"));
        Assert.Contains(conflicts, c => c.Message!.Contains("не число"));
        Assert.Contains(conflicts, c => c.Message!.Contains("неверный период"));
    }

    [Fact]
    public void LineMap_OverridesArticleRule()
    {
        var r = Plan([new("Бобур Самарканд", "605-Новая статья", 2026, 9, 40, "A1")], lineMap: new Dictionary<string, string> { ["605"] = "sm" });
        Assert.Equal("40", Item(r, "Бобур Самарканд", PnlMath.Sm).After);
        Assert.Throws<DataManagerException>(() => Plan([new("Бобур", "605", 2026, 9, 40, "A1")], lineMap: new Dictionary<string, string> { ["605"] = "прибыль" }));
    }

    [Fact]
    public void NewDealerSimilarToKnown_Warned_DealerMapJoinsIt()
    {
        var known = new Dictionary<string, string> { ["бобур самарканд"] = "Бобур Самарканд" };
        var values = new List<PnlRawValue> { new("Бобур (Самарканд)", "sales", 2026, 9, 100, "A1") };

        var warned = Plan(values, known: known);
        Assert.Contains(warned.Warnings, w => w.Contains("Бобур (Самарканд) (похож на «Бобур Самарканд»)"));

        var mapped = Plan(values, known: known, dealerMap: new Dictionary<string, string> { ["Бобур (Самарканд)"] = "Бобур Самарканд" });
        Assert.Equal(PnlPlanner.Key(2026, 9, PnlMath.Sales, "Бобур Самарканд"), Assert.Single(mapped.Items).EntityKey);
        Assert.Empty(mapped.Summary.NewDealers);
    }

    [Fact]
    public void Key_RoundTrip_DealerMayContainColon()
    {
        var key = PnlPlanner.Key(2026, 9, PnlMath.ShipDealer, "ООО «Свит: Опт»");
        Assert.Equal((2026, 9, PnlMath.ShipDealer, "ООО «Свит: Опт»"), PnlPlanner.ParseKey(key));
        Assert.True(key.Length <= 200);
    }

    [Fact]
    public void MissingMonth_IsReportedToModel()
    {
        var ex = Assert.Throws<DataManagerException>(() => PnlExtraction.Values(Registry, RegistryMap with { MonthColumn = null }));
        Assert.Contains("месяц", ex.Message);
    }
}
