using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;
using OneBase.Infrastructure.Linko;

namespace OneBase.Sales.Tests;

/// <summary>Правила вторички из «Как считается вторичка из Linko API» — на маленьких наборах с известным ответом.</summary>
public class SecondarySalesTests
{
    private static readonly SalesOptions Options = new(); // delivered, дата приёмки, без филиала «Завод»
    private static readonly DateOnly Sep1 = new(2026, 9, 1);
    private static readonly DateOnly Sep28 = new(2026, 9, 28);

    private static DateOnly D(string s) => DateOnly.Parse(s);

    private static RawOrderLine Order(long id, string status = "delivered", string created = "2026-09-10", string? accepted = "2026-09-11",
        string? delivery = null, string? branch = "Самарканд", decimal kg = 10, long market = 100, long agent = 1) =>
        new(id, status, D(created), delivery is null ? null : D(delivery), accepted is null ? null : D(accepted), 1, branch, agent, market, 500, 4, kg, kg * 1000);

    private static RawReturnLine Return(long id, decimal kg, string created = "2026-09-12", string status = "delivered", string? branch = "Самарканд") =>
        new(id, status, D(created), 1, branch, 1, 100, 500, 4, kg, kg * 1000);

    private static SecondarySalesResult Build(IEnumerable<RawOrderLine> orders, IEnumerable<RawReturnLine>? returns = null,
        IEnumerable<RawReturnHeader>? headers = null, DateOnly? from = null, DateOnly? to = null, int missing = 0) =>
        SecondarySales.Build(orders, returns ?? [], headers ?? [], from ?? Sep1, to ?? Sep28, Options, missing);

    [Fact]
    public void Order_created_in_previous_month_but_accepted_in_reporting_month_counts_in_reporting_month()
    {
        var order = Order(1, created: "2026-08-31", accepted: "2026-09-01");

        Assert.Single(Build([order]).Lines);
        Assert.Empty(Build([order], from: D("2026-08-01"), to: D("2026-08-31")).Lines);
    }

    [Fact]
    public void Planned_delivery_date_is_ignored_the_acceptance_date_decides()
    {
        // План доставки — воскресенье 06.09, а товар принят в понедельник 07.09.
        var line = Assert.Single(Build([Order(1, created: "2026-09-05", delivery: "2026-09-06", accepted: "2026-09-07")]).Lines);

        Assert.Equal(D("2026-09-07"), line.Date);
    }

    [Theory]
    [InlineData("cancelled")]
    [InlineData("not_delivered")]
    [InlineData("given")]
    [InlineData("new")]
    public void Only_delivered_orders_are_sales(string status) =>
        Assert.Empty(Build([Order(1, status: status)]).Lines);

    [Fact]
    public void Order_without_acceptance_time_is_not_a_sale_and_is_reported()
    {
        var result = Build([Order(1, accepted: null)], missing: 1);

        Assert.Empty(result.Lines);
        Assert.Equal(1, result.Quality.DeliveredWithoutAcceptance);
    }

    [Fact]
    public void Orders_accepted_after_the_report_day_are_cut()
    {
        var result = Build([Order(1, accepted: "2026-09-28"), Order(2, accepted: "2026-09-29")], to: Sep28);

        Assert.Equal(1, Assert.Single(result.Lines).OrderId);
    }

    [Theory]
    [InlineData("Завод")]
    [InlineData(" завод ")]
    public void Factory_branch_is_not_secondary_sales_and_is_kept_separately(string branch)
    {
        var result = Build([Order(1, branch: branch, kg: 5000), Order(2, kg: 10)], [Return(9, 3, branch: branch)]);

        Assert.Equal(10m, result.Lines.Sum(l => l.Kg));
        Assert.Equal(4997m, result.Excluded.Sum(l => l.Kg));
    }

    [Fact]
    public void Return_weight_comes_from_lines_even_when_the_header_weight_is_zero()
    {
        var headers = new[]
        {
            new RawReturnHeader(9, "delivered", D("2026-09-12"), "Жиззах", HeaderKg: 0, Lines: 2, LinesKg: 3.5m),
            new RawReturnHeader(10, "delivered", D("2026-09-13"), "Бухоро", HeaderKg: 4, Lines: 0, LinesKg: 0), // строк нет
        };

        var result = Build([Order(1, kg: 20)], [Return(9, 1.5m), Return(9, 2m)], headers);

        Assert.Equal(20m - 3.5m, result.Lines.Sum(l => l.Kg));
        Assert.Equal(-3500m, result.Lines.Where(l => l.OrderId == null).Sum(l => l.Revenue));
        Assert.Equal((1, 3.5m), (result.Quality.ZeroHeaderReturns, result.Quality.ZeroHeaderReturnsKg));
        Assert.Equal((1, 4m), (result.Quality.ReturnsWithoutLines, result.Quality.ReturnsWithoutLinesHeaderKg));
    }

    [Fact]
    public void Only_delivered_returns_of_the_period_are_subtracted()
    {
        var result = Build([Order(1, kg: 20)], [Return(9, 2, status: "not_delivered"), Return(10, 3, created: "2026-08-31"), Return(11, 1)]);

        Assert.Equal(19m, result.Lines.Sum(l => l.Kg));
    }

    [Fact]
    public void Returns_do_not_reduce_akb()
    {
        var result = Build([Order(1, kg: 5, market: 100)], [Return(9, 5)]); // вернули всё, что купили

        Assert.Equal(0m, result.Lines.Sum(l => l.Kg));
        Assert.Equal(1, SalesMath.Akb(result.Lines));
    }

    [Fact]
    public void Visit_orders_are_dated_by_creation_while_the_sale_is_dated_by_acceptance()
    {
        var order = Order(1, created: "2026-09-10", accepted: "2026-09-11");

        Assert.Equal(D("2026-09-11"), Assert.Single(Build([order]).Lines).Date);
        Assert.Equal(D("2026-09-10"), Assert.Single(SecondarySales.VisitOrders([order], Sep1, Sep28, Options)).Date);
        Assert.Empty(SecondarySales.VisitOrders([Order(2, branch: "Завод"), Order(3, status: "cancelled")], Sep1, Sep28, Options));
    }

    // ---------- Пагинация ----------

    private static Func<int, int, Task<IReadOnlyList<int>>> Server(int rows, int cap = 1000) =>
        (offset, limit) => Task.FromResult<IReadOnlyList<int>>(Enumerable.Range(offset, Math.Max(0, Math.Min(Math.Min(limit, cap), rows - offset))).ToList());

    private static async Task<List<int>> ReadAll(Func<int, int, Task<IReadOnlyList<int>>> server, int limit, int parallel = 4)
    {
        var got = new List<int>();
        var total = await LinkoPaging.ReadAllAsync(server, limit, parallel, page => { got.AddRange(page); return Task.CompletedTask; });
        Assert.Equal(got.Count, total);
        return got;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    [InlineData(1000)]
    [InlineData(2000)]
    [InlineData(4001)]
    [InlineData(9999)]
    public async Task Paging_reads_every_row_once_in_order(int rows)
    {
        var got = await ReadAll(Server(rows), limit: 1000);

        Assert.Equal(Enumerable.Range(0, rows), got);
    }

    [Theory]
    [InlineData(2302)]
    [InlineData(500)]
    [InlineData(501)]
    public async Task Paging_adapts_when_the_server_returns_less_than_the_requested_limit(int rows)
    {
        // Сервер отдаёт по 500 при limit=1000: без подстройки строки 500–999 каждой тысячи пропали бы.
        var got = await ReadAll(Server(rows, cap: 500), limit: 1000);

        Assert.Equal(Enumerable.Range(0, rows), got);
    }
}
