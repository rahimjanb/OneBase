using OneBase.Application.Finance;
using OneBase.Finance.Integration.Worker;
using OneBase.Finance.Integration.Worker.Mock;
using Xunit.Abstractions;

namespace OneBase.Finance.Tests;

/// <summary>Демо-данные похожи на настоящие: деньги не уходят в минус, склад не раздувается, маржа и прибыль правдоподобны.</summary>
public class MockEconomicsTests(ITestOutputHelper output)
{
    internal static MockDataSet Set(DateOnly start, DateTimeOffset now) => new(new MockOptions { StartDate = start }, now);

    [Fact]
    public void Demo_company_has_plausible_cash_stock_and_margin()
    {
        var now = new DateTimeOffset(2026, 11, 16, 18, 0, 0, TimeSpan.FromHours(5));
        var set = Set(new DateOnly(2025, 1, 1), now);
        var report = new List<string>();
        foreach (var org in MockCatalog.Organizations)
        {
            for (var month = new DateOnly(2025, 3, 1); month <= new DateOnly(2026, 10, 1); month = month.AddMonths(1))
            {
                var rows = set.Balances(org.Index, month);
                decimal Closing(string prefix) => rows.Where(r => r.Account!.StartsWith(prefix)).Sum(r => (r.Closing!.Debit) - r.Closing.Credit);
                var cash = Closing("50") + Closing("51") + Closing("52");
                var materials = Closing("10");
                var goods = Closing("28");
                var turnovers = set.Documents.Where(d => d.Org == org.Index && d.Doc.Posted).SelectMany(d => d.Entries)
                    .Where(e => MockDataSet.DayOf(e.Period!.Value) >= month && MockDataSet.DayOf(e.Period!.Value) < month.AddMonths(1))
                    .Select(e => new FinanceChart.Turnover(e.Debit!.Account!, e.Credit!.Account!, month, e.Amount)).ToList();
                var pnl = FinanceChart.Pnl(turnovers);
                report.Add($"{org.Code} {month:yyyy-MM}: деньги {cash / 1e9m:N2} млрд, сырьё {materials / 1e9m:N2}, продукция {goods / 1e9m:N2}, выручка {pnl["net_revenue"] / 1e9m:N2}, валовая {pnl["gross_profit"] / 1e9m:N2}, чистая {pnl["net_profit"] / 1e9m:N2}");
                output.WriteLine(report[^1]);
                Assert.True(cash > 0, report[^1]);
                Assert.True(materials < pnl["net_revenue"] * 1.5m, report[^1]);
                Assert.True(goods < pnl["net_revenue"] * 1.5m, report[^1]);
                var margin = pnl["gross_profit"] / pnl["net_revenue"];
                Assert.InRange(margin, 0.15m, 0.55m);
            }
        }
    }
}
