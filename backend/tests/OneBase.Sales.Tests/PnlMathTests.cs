using OneBase.Application.Sales.Pnl;

namespace OneBase.Sales.Tests;

/// <summary>P&L дилеров: строки отчёта → строки P&L, формулы (как во вкладке «P&L дилеров» Полевого контроля), итоги и темп роста.</summary>
public class PnlMathTests
{
    private static Dictionary<string, decimal?> Compute(Dictionary<string, decimal> inputs, params string[] scope) =>
        PnlMath.Compute(inputs, scope.Length == 0 ? inputs.Keys.ToHashSet() : scope.ToHashSet());

    [Theory]
    [InlineData("Продажи", PnlMath.Sales)]
    [InlineData("sales", PnlMath.Sales)]
    [InlineData("Сумма Диллера", PnlMath.Sales)]
    [InlineData("Себестоимость товара", PnlMath.Cost)]
    [InlineData("Сумма завода", PnlMath.Cost)]
    [InlineData("Минус Отдел продаж и маркетинга", PnlMath.Sm)]
    [InlineData("Плюс Ретробонусы от поставщиков", PnlMath.Retro)]
    [InlineData("Прибыль после продаж", PnlMath.After)]
    [InlineData("ROS рентабельность продаж", PnlMath.Ros)]
    [InlineData("Доставка и склад", PnlMath.Ship)]
    [InlineData("в т. ч. завод оплатил (журнал логистики)", PnlMath.ShipPlant)]
    [InlineData("Дилер тулади", PnlMath.ShipDealer)]
    [InlineData("справочно: компенсировано дилеру по ст. 606", PnlMath.Ship606)]
    [InlineData("601-Дилер бонуслари", PnlMath.Sm)]
    [InlineData("610 - ЗП Агент", PnlMath.Sm)]
    [InlineData("606-Логистика", PnlMath.Ship606)]
    [InlineData("611-Услуги обслуживание программы", PnlMath.Admin)]
    [InlineData("339-Утилизация", PnlMath.Admin)]
    [InlineData("605-Новая статья", PnlMath.Other)]
    [InlineData("EBIT операционная прибыль", PnlMath.Ebit)]
    [InlineData("EAT чистая прибыль", PnlMath.Eat)]
    [InlineData("Маржа", PnlMath.Margin)]
    public void RecognizesReportLines(string text, string line) => Assert.Equal(line, PnlMath.FindLine(text));

    [Theory]
    [InlineData("")]
    [InlineData("Непонятная строка")]
    [InlineData("2026")]
    public void UnknownLineIsNotGuessed(string text) => Assert.Null(PnlMath.FindLine(text));

    /// <summary>Контрольные итоги pnl.json Полевого контроля за август и сентябрь 2026 (административных и прочих расходов не было).</summary>
    [Theory]
    [InlineData(13_403_716_435, 12_633_275_383, 704_236_032, 227_304_000, 770_441_052, -161_098_980)]
    [InlineData(12_192_636_107, 11_501_799_621, 50_852_766, 199_290_000, 690_836_486, 440_693_720)]
    public void ReproducesArtifactControlTotals(double sales, double cost, double sm, double ship, double gross, double ebit)
    {
        var r = Compute(new()
        {
            [PnlMath.Sales] = (decimal)sales, [PnlMath.Cost] = (decimal)cost, [PnlMath.Sm] = (decimal)sm, [PnlMath.ShipPlant] = (decimal)ship,
        });

        Assert.Equal((decimal)gross, r[PnlMath.Gross]);
        Assert.Equal((decimal)ebit, r[PnlMath.Ebit]);
        Assert.Equal((decimal)gross / (decimal)sales, r[PnlMath.Margin]);
        Assert.Equal((decimal)(gross - sm) / (decimal)sales, r[PnlMath.Ros]);
        Assert.Null(r[PnlMath.Eat]); // банковских услуг и налогов нет — EAT не выдумывается
        Assert.Null(r[PnlMath.Roa]);
    }

    [Fact]
    public void Shipping_JournalFirst_606WhenDealerHasNoTrips()
    {
        var journal = Compute(new() { [PnlMath.Sales] = 1000, [PnlMath.ShipPlant] = 30, [PnlMath.ShipDealer] = 20, [PnlMath.Ship606] = 45 });
        Assert.Equal(50, journal[PnlMath.Ship]);
        Assert.Equal(950, journal[PnlMath.Ebit]); // 606 — справочно, второй раз не вычитается

        var only606 = Compute(new() { [PnlMath.Sales] = 1000, [PnlMath.Ship606] = 45 });
        Assert.Equal(45, only606[PnlMath.Ship]);
        Assert.Equal(955, only606[PnlMath.Ebit]);
    }

    [Fact]
    public void Ebit_SubtractsAllExpenseGroups_RetroAdds()
    {
        var r = Compute(new()
        {
            [PnlMath.Sales] = 1000, [PnlMath.Cost] = 800, [PnlMath.Sm] = 50, [PnlMath.Retro] = 10, [PnlMath.Admin] = 20, [PnlMath.Other] = 5,
            [PnlMath.Amort] = 15,
        });

        Assert.Equal(160, r[PnlMath.After]); // 200 − 50 + 10
        Assert.Equal(120, r[PnlMath.Ebit]); // 160 − 20 − 5 − 15
    }

    [Fact]
    public void EatAndReinvested_OnlyWhenLoadedForMonth()
    {
        var inputs = new Dictionary<string, decimal> { [PnlMath.Sales] = 1000, [PnlMath.Cost] = 700, [PnlMath.Tax] = 30 };
        var r = Compute(inputs, PnlMath.Sales, PnlMath.Cost, PnlMath.Tax);
        Assert.Equal(270, r[PnlMath.Eat]);
        Assert.Null(r[PnlMath.Reinv]);

        // Налоги загружены в месяце у другого дилера — у этого EAT = EBIT (налогов 0), а не «—».
        var other = Compute(new() { [PnlMath.Sales] = 500, [PnlMath.Cost] = 400 }, PnlMath.Sales, PnlMath.Cost, PnlMath.Tax, PnlMath.Div);
        Assert.Equal(100, other[PnlMath.Eat]);
        Assert.Equal(100, other[PnlMath.Reinv]);
    }

    [Fact]
    public void ExpensesWithoutSales_NegativeProfit_NoMargin()
    {
        var r = Compute(new() { [PnlMath.Sm] = 2_562.2m });
        Assert.Equal(-2_562.2m, r[PnlMath.Ebit]);
        Assert.Null(r[PnlMath.Margin]);
        Assert.Null(r[PnlMath.Sales]);
    }

    [Fact]
    public void NothingLoaded_AllLinesEmpty() => Assert.All(Compute([]), v => Assert.Null(v.Value));

    [Fact]
    public void Totals_SumMoney_PercentFromSums()
    {
        var a = Compute(new() { [PnlMath.Sales] = 1000, [PnlMath.Cost] = 900, [PnlMath.Ship606] = 10 });
        var b = Compute(new() { [PnlMath.Sales] = 3000, [PnlMath.Cost] = 2400, [PnlMath.ShipPlant] = 100 });
        var total = PnlMath.Sum([a, b]);

        Assert.Equal(4000, total[PnlMath.Sales]);
        Assert.Equal(700, total[PnlMath.Gross]);
        Assert.Equal(700m / 4000m, total[PnlMath.Margin]); // взвешенно, не среднее 10 % и 20 %
        Assert.Equal(110, total[PnlMath.Ship]); // у каждого дилера своя доставка: 606 у одного, журнал у другого
        Assert.Equal(590, total[PnlMath.Ebit]);
        Assert.Null(total[PnlMath.Admin]);
    }

    [Fact]
    public void Growth_MoneyRelative_PercentInPoints()
    {
        var sep = Compute(new() { [PnlMath.Sales] = 1200, [PnlMath.Cost] = 1080 });
        var aug = Compute(new() { [PnlMath.Sales] = 1000, [PnlMath.Cost] = 950 });
        var g = PnlMath.Growth(sep, aug);

        Assert.Equal(0.2m, g[PnlMath.Sales]);
        Assert.Equal((120m - 50m) / 50m, g[PnlMath.Gross]);
        Assert.Equal(120m / 1200m - 50m / 1000m, g[PnlMath.Margin]); // 10 % − 5 % = +5 п. п.

        var loss = PnlMath.Growth(Compute(new() { [PnlMath.Sm] = 10 }), Compute(new() { [PnlMath.Sm] = 20 }));
        Assert.Equal(0.5m, loss[PnlMath.Ebit]); // −10 к −20: (−10 − (−20)) ÷ |−20|
    }

    [Theory]
    [InlineData("  ООО «Бобур   Самарканд» ", "ооо бобур самарканд")]
    [InlineData("Ёлка", "елка")]
    public void DealerKey_IgnoresCaseQuotesSpaces(string name, string key) => Assert.Equal(key, PnlMath.DealerKey(name));
}
