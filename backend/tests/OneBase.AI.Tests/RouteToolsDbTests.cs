using OneBase.Application.Telegram.Reports;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Tools;
using OneBase.Application.Sales;
using OneBase.Application.Security;
using OneBase.Domain.Identity;
using OneBase.Domain.Sales;
using OneBase.Domain.Telegram;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// Инструменты консультанта на PostgreSQL: «по маршруту / вне маршрута / без визита» — визиты, визиты с заказом, заказы, выручка, кг, АКБ —
/// у ТП и у региона с каждым ТП (get_visits), продажи за день (get_sales_days), заказы по маршруту у визитов ТП (get_agent_visits);
/// Telegram (get_telegram) — только с правом, без прав разделы пустые.
/// </summary>
public class RouteToolsDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private const string Day = "2026-10-20";
    private static readonly DateOnly Date = new(2026, 10, 20);
    private const long Agent = 93;

    private async Task SeedAsync()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        long[] ids = [7_100_001, 7_100_002, 7_100_003];
        await db.LinkoVisits.Where(v => v.Id >= 7_100_001 && v.Id <= 7_100_003).ExecuteDeleteAsync();
        await db.LinkoOrderLines.Where(l => ids.Contains(l.Id)).ExecuteDeleteAsync();
        await db.LinkoOrders.Where(o => ids.Contains(o.Id)).ExecuteDeleteAsync();
        await db.LinkoMarkets.Where(m => m.Id >= 3011 && m.Id <= 3014).ExecuteDeleteAsync();
        await db.LinkoUsers.Where(u => u.Id == Agent).ExecuteDeleteAsync();

        db.LinkoUsers.Add(new LinkoUser { Id = Agent, FirstName = "Санжар", SecondName = "Тошев", IsActive = true, JobName = "Агент" });
        foreach (var (id, name) in new[] { (3011L, "Магазин Ширин"), (3012L, "Маркет Гулистон"), (3013L, "Киоск Нодира"), (3014L, "Супермаркет Хива") })
        {
            db.LinkoMarkets.Add(new LinkoMarket { Id = id, Name = name, BranchId = 5, BranchName = "Хоразм", ResponsibleAgentId = Agent });
        }

        // По маршруту: Ширин — выполнен, Нодира — не посещён; вне маршрута: Гулистон — выполнен; Хива — заказ без визита.
        db.LinkoVisits.AddRange(
            new LinkoVisit { Id = 7_100_001, Date = Date.ToDateTime(new TimeOnly(10, 0)), Day = Date, Status = "done", IsInPlan = true, MarketId = 3011, UserId = Agent },
            new LinkoVisit { Id = 7_100_002, Date = Date.ToDateTime(new TimeOnly(11, 0)), Day = Date, Status = "done", IsInPlan = false, MarketId = 3012, UserId = Agent },
            new LinkoVisit { Id = 7_100_003, Date = Date.ToDateTime(new TimeOnly(9, 0)), Day = Date, Status = "pending", IsInPlan = true, MarketId = 3013, UserId = Agent });
        foreach (var (id, market, sum, kg) in new[] { (7_100_001L, 3011L, 1_000_000m, 25m), (7_100_002L, 3012L, 600_000m, 15m), (7_100_003L, 3014L, 400_000m, 10m) })
        {
            var at = Date.ToDateTime(new TimeOnly(12, 0));
            db.LinkoOrders.Add(new LinkoOrder
            {
                Id = id, CreatedAt = at, CreatedDate = Date, AcceptedAt = at, AcceptedDate = Date, Status = "delivered", MarketId = market, BranchId = 5,
                BranchName = "Хоразм", AgentId = Agent, TotalPrice = sum, TotalWeight = kg,
            });
            db.LinkoOrderLines.Add(new LinkoOrderLine { Id = id, OrderId = id, ProductId = 100, Amount = kg, Price = sum / kg, TotalPrice = sum, TotalWeight = kg });
        }

        await db.SaveChangesAsync();
        scope.ServiceProvider.GetRequiredService<SalesCacheSignal>().Invalidate();
    }

    private const long PeriodAgent = 94;

    /// <summary>
    /// ТП 94 и три точки Хоразма: 31.08 — Бахор 7 кг; 28.09 — Олма 10 кг; 30.09 — Бахор 5 кг; 02.10 — Олма 20 кг; 04.10 — Нур 8 кг; 06.10 — Нур
    /// 4 кг. Неделя 28.09–04.10 (пн–вс) — через границу месяца.
    /// </summary>
    private async Task SeedPeriodAsync()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        long[] ids = [7_200_001, 7_200_002, 7_200_003, 7_200_004, 7_200_005, 7_200_006];
        await db.LinkoOrderLines.Where(l => ids.Contains(l.Id)).ExecuteDeleteAsync();
        await db.LinkoOrders.Where(o => ids.Contains(o.Id)).ExecuteDeleteAsync();
        await db.LinkoMarkets.Where(m => m.Id >= 3021 && m.Id <= 3023).ExecuteDeleteAsync();
        await db.LinkoUsers.Where(u => u.Id == PeriodAgent).ExecuteDeleteAsync();

        db.LinkoUsers.Add(new LinkoUser { Id = PeriodAgent, FirstName = "Ойбек", SecondName = "Каримов", IsActive = true, JobName = "Агент" });
        foreach (var (id, name) in new[] { (3021L, "Дўкон Олма"), (3022L, "Магазин Бахор"), (3023L, "Минимаркет Нур") })
        {
            db.LinkoMarkets.Add(new LinkoMarket { Id = id, Name = name, BranchId = 5, BranchName = "Хоразм", ResponsibleAgentId = PeriodAgent });
        }

        var orders = new (long Id, DateOnly Date, long Market, decimal Sum, decimal Kg)[]
        {
            (7_200_001, new(2026, 8, 31), 3022, 250_000, 7), (7_200_002, new(2026, 9, 28), 3021, 400_000, 10), (7_200_003, new(2026, 9, 30), 3022, 200_000, 5),
            (7_200_004, new(2026, 10, 2), 3021, 800_000, 20), (7_200_005, new(2026, 10, 4), 3023, 300_000, 8), (7_200_006, new(2026, 10, 6), 3023, 100_000, 4),
        };
        foreach (var (id, date, market, sum, kg) in orders)
        {
            var at = date.ToDateTime(new TimeOnly(12, 0));
            db.LinkoOrders.Add(new LinkoOrder
            {
                Id = id, CreatedAt = at, CreatedDate = date, AcceptedAt = at, AcceptedDate = date, Status = "delivered", MarketId = market, BranchId = 5,
                BranchName = "Хоразм", AgentId = PeriodAgent, TotalPrice = sum, TotalWeight = kg,
            });
            db.LinkoOrderLines.Add(new LinkoOrderLine { Id = id, OrderId = id, ProductId = 100, Amount = kg, Price = sum / kg, TotalPrice = sum, TotalWeight = kg });
        }

        await db.SaveChangesAsync();
        scope.ServiceProvider.GetRequiredService<SalesCacheSignal>().Invalidate();
    }

    private async Task<JsonElement> RunAsync(string tool, object args, Guid? user = null)
    {
        using var scope = fixture.Services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();
        var result = await registry.Find(tool)!.ExecuteAsync(new ToolContext("sales", user ?? fixture.AdminId, 0), JsonSerializer.SerializeToElement(args));
        Assert.True(result.Success, result.Content);
        return JsonDocument.Parse(result.Content).RootElement.Clone();
    }

    private static (int Orders, decimal Revenue, decimal Kg, int Akb) Part(JsonElement p) =>
        (p.GetProperty("orders").GetInt32(), p.GetProperty("revenueSum").GetDecimal(), p.GetProperty("kg").GetDecimal(), p.GetProperty("akb").GetInt32());

    [PostgresFact]
    public async Task Visits_orders_revenue_and_akb_are_split_by_route_for_an_agent_and_every_agent_of_a_region()
    {
        await SeedAsync();

        var agent = await RunAsync("get_visits", new { agent = "Санжар Тошев", date = Day });
        Assert.Equal("Санжар Тошев", agent.GetProperty("scope").GetString());
        var total = agent.GetProperty("total");
        var visits = total.GetProperty("visits");
        Assert.Equal((2, 1, 1, 1), (visits.GetProperty("planned").GetInt32(), visits.GetProperty("doneByRoute").GetInt32(),
            visits.GetProperty("doneOffRoute").GetInt32(), visits.GetProperty("notVisited").GetInt32()));
        Assert.Equal((1, 1), (total.GetProperty("visitsWithOrder").GetProperty("byRoute").GetInt32(), total.GetProperty("visitsWithOrder").GetProperty("offRoute").GetInt32()));
        Assert.Equal((1, 1_000_000m, 25m, 1), Part(total.GetProperty("byRoute")));
        Assert.Equal((1, 600_000m, 15m, 1), Part(total.GetProperty("offRoute")));
        Assert.Equal((1, 400_000m, 10m, 1), Part(total.GetProperty("noVisit")));
        Assert.Equal((3, 2_000_000m, 50m, 3), Part(total.GetProperty("total")));
        Assert.Equal(50m, total.GetProperty("byRouteRevenuePct").GetDecimal());

        // Регион — каждый ТП со своей разбивкой, не одна общая цифра.
        var region = await RunAsync("get_visits", new { region = "Хоразм", date = Day });
        var row = region.GetProperty("agents").EnumerateArray().Single(a => a.GetProperty("name").GetString() == "Санжар Тошев");
        Assert.Equal((1, 1_000_000m, 25m, 1), Part(row.GetProperty("byRoute")));
        Assert.Equal((1, 400_000m, 10m, 1), Part(row.GetProperty("noVisit")));

        // Республика — по регионам; с agents=true — и ТП внутри регионов.
        var republic = await RunAsync("get_visits", new { date = Day, agents = true });
        var khorazm = republic.GetProperty("regions").EnumerateArray().Single(r => r.GetProperty("name").GetString() == "Хоразм");
        Assert.Contains(khorazm.GetProperty("agents").EnumerateArray(), a => a.GetProperty("name").GetString() == "Санжар Тошев");

        // Отрезок — в пределах месяца.
        using var scope = fixture.Services.CreateScope();
        var bad = await scope.ServiceProvider.GetRequiredService<IToolRegistry>().Find("get_visits")!
            .ExecuteAsync(new ToolContext("sales", fixture.AdminId, 0), JsonSerializer.SerializeToElement(new { from = "2026-09-25", to = "2026-10-05" }));
        Assert.False(bad.Success);
        Assert.Contains("одного месяца", bad.Content);
    }

    [PostgresFact]
    public async Task Sales_for_a_day_and_orders_by_route_of_agent_visits()
    {
        await SeedAsync();

        var day = await RunAsync("get_sales_days", new { agent = Agent.ToString(), date = Day });
        Assert.Equal((2_000_000m, 50m, 3, 3), (day.GetProperty("total").GetProperty("revenueSum").GetDecimal(), day.GetProperty("total").GetProperty("factKg").GetDecimal(),
            day.GetProperty("total").GetProperty("orders").GetInt32(), day.GetProperty("total").GetProperty("akb").GetInt32()));
        var only = Assert.Single(day.GetProperty("days").EnumerateArray());
        Assert.Equal((Day, 2_000_000m, 2), (only.GetProperty("date").GetString(), only.GetProperty("revenueSum").GetDecimal(), only.GetProperty("visitsDone").GetInt32()));

        var region = await RunAsync("get_sales_days", new { region = "Хоразм", from = Day, to = Day });
        Assert.Contains(region.GetProperty("agents").EnumerateArray(), a => a.GetProperty("name").GetString() == "Санжар Тошев" && a.GetProperty("revenueSum").GetDecimal() == 2_000_000m);

        // Визиты ТП по точкам: итоги заказов по маршруту — полные, не из урезанного списка.
        var visits = await RunAsync("get_agent_visits", new { agent = "Санжар", date = Day });
        var totals = visits.GetProperty("totals");
        Assert.Equal((1, 1_000_000m), (totals.GetProperty("ordersByRoute").GetProperty("count").GetInt32(), totals.GetProperty("ordersByRoute").GetProperty("sumUzs").GetDecimal()));
        Assert.Equal((1, 1), (totals.GetProperty("ordersOffRoute").GetProperty("count").GetInt32(), totals.GetProperty("ordersNoVisit").GetProperty("count").GetInt32()));
        Assert.Equal((1, 1), (totals.GetProperty("visitsWithOrderByRoute").GetInt32(), totals.GetProperty("visitsWithOrderOffRoute").GetInt32()));
    }

    private static (decimal Kg, decimal Revenue, int Orders, int Akb) Figures(JsonElement p) =>
        (p.GetProperty("factKg").GetDecimal(), p.GetProperty("revenueSum").GetDecimal(), p.GetProperty("orders").GetInt32(), p.GetProperty("akb").GetInt32());

    /// <summary>
    /// P1: продажи за период одним вызовом — неделя через границу месяца, несколько месяцев, сравнение — по тем же расчётам, что «Продажи»:
    /// суммы частей совпадают с get_sales_days по каждому месяцу, АКБ периода — точки за весь период (не сумма АКБ месяцев), разница — кодом,
    /// месяц без данных и дни после последней загрузки — неполнота, источник и период — в «_meta».
    /// </summary>
    [PostgresFact]
    public async Task Sales_period_spans_months_with_period_akb_weeks_comparison_and_coverage()
    {
        await SeedAsync();
        await SeedPeriodAsync();
        var agent = PeriodAgent.ToString();

        // Неделя 28.09–04.10 (пн–вс): одна неделя через границу месяца, АКБ — 3 точки (у частей месяцев — по 2).
        var week = await RunAsync("get_sales_period", new { from = "2026-09-28", to = "2026-10-04", agent, group = "week" });
        Assert.Equal("Ойбек Каримов", week.GetProperty("scope").GetString());
        Assert.Equal((43m, 1_700_000m, 4, 3), Figures(week.GetProperty("total")));
        var w = Assert.Single(week.GetProperty("weeks").EnumerateArray());
        Assert.Equal(("2026-09-28", "2026-10-04"), (w.GetProperty("from").GetString(), w.GetProperty("to").GetString()));
        Assert.Equal((43m, 1_700_000m, 4, 3), Figures(w));
        Assert.True(week.GetProperty("coverage").GetProperty("complete").GetBoolean());
        Assert.Equal(7, week.GetProperty("period").GetProperty("days").GetInt32()); // обе границы входят
        var source = week.GetProperty("_meta").GetProperty("sources")[0];
        Assert.Contains("за период", source.GetProperty("title").GetString());
        Assert.Equal("28.09.2026 — 04.10.2026", source.GetProperty("period").GetString());

        var split = await RunAsync("get_sales_period", new { from = "2026-09-28", to = "2026-10-04", agent });
        var parts = split.GetProperty("months").EnumerateArray().ToList();
        Assert.Equal([(15m, 600_000m, 2, 2), (28m, 1_100_000m, 2, 2)], parts.Select(Figures));

        // Сверка с get_sales_days по каждой части месяца: те же суммы.
        var sep = (await RunAsync("get_sales_days", new { agent, from = "2026-09-28", to = "2026-09-30" })).GetProperty("total");
        var oct = (await RunAsync("get_sales_days", new { agent, from = "2026-10-01", to = "2026-10-04" })).GetProperty("total");
        Assert.Equal(43m, sep.GetProperty("factKg").GetDecimal() + oct.GetProperty("factKg").GetDecimal());
        Assert.Equal(1_700_000m, sep.GetProperty("revenueSum").GetDecimal() + oct.GetProperty("revenueSum").GetDecimal());
        Assert.Equal(4, sep.GetProperty("akb").GetInt32() + oct.GetProperty("akb").GetInt32()); // сумма АКБ месяцев — 4, а точек за неделю — 3

        // Три месяца: по месяцам с изменением к прошлому; октябрь — до последней загрузки, период неполный.
        var three = await RunAsync("get_sales_period", new { from = "2026-08-01", to = "2026-10-31", agent });
        var months = three.GetProperty("months").EnumerateArray().ToList();
        Assert.Equal([7m, 15m, 32m], months.Select(m => m.GetProperty("factKg").GetDecimal()));
        Assert.Equal(114.3m, months[1].GetProperty("vsPreviousKgPct").GetDecimal());
        Assert.Equal(113.3m, months[2].GetProperty("vsPreviousKgPct").GetDecimal());
        Assert.Equal((54m, 3), (three.GetProperty("total").GetProperty("factKg").GetDecimal(), three.GetProperty("total").GetProperty("akb").GetInt32()));
        Assert.False(three.GetProperty("coverage").GetProperty("complete").GetBoolean());
        Assert.NotEmpty(three.GetProperty("coverage").GetProperty("missing").EnumerateArray());
        Assert.True(months[2].GetProperty("partial").GetBoolean());

        // Сентябрь к августу: разница и % — кодом.
        var cmp = await RunAsync("get_sales_period", new { period = "2026-09", agent, compare = "previous" });
        Assert.Equal("01.08.2026 — 31.08.2026", cmp.GetProperty("compare").GetProperty("period").GetProperty("label").GetString());
        var delta = cmp.GetProperty("compare").GetProperty("delta");
        Assert.Equal((8m, 114.3m, 350_000m, 140m, 1), (delta.GetProperty("factKg").GetDecimal(), delta.GetProperty("factKgPct").GetDecimal(),
            delta.GetProperty("revenueSum").GetDecimal(), delta.GetProperty("revenueSumPct").GetDecimal(), delta.GetProperty("akb").GetInt32()));
        Assert.Equal(2, cmp.GetProperty("_meta").GetProperty("sources").GetArrayLength());

        // Средний чек (выручка ÷ заказы) и строки ТП обоих периодов с разницей — кодом, а не расчётом модели по одной строке.
        Assert.Equal(300_000m, cmp.GetProperty("total").GetProperty("avgCheckSum").GetDecimal());
        Assert.Equal((50_000m, 20m), (delta.GetProperty("avgCheckSum").GetDecimal(), delta.GetProperty("avgCheckSumPct").GetDecimal()));
        var row = Assert.Single(cmp.GetProperty("compare").GetProperty("agents").EnumerateArray());
        Assert.Equal((600_000m, 250_000m, 350_000m, 140m), (row.GetProperty("revenueSum").GetDecimal(), row.GetProperty("compareRevenueSum").GetDecimal(),
            row.GetProperty("revenueDeltaSum").GetDecimal(), row.GetProperty("revenueDeltaPct").GetDecimal()));
        Assert.Equal((300_000m, 250_000m, 20m), (row.GetProperty("avgCheckSum").GetDecimal(), row.GetProperty("compareAvgCheckSum").GetDecimal(),
            row.GetProperty("avgCheckDeltaPct").GetDecimal()));
        Assert.Equal((2, 1), (row.GetProperty("orders").GetInt32(), row.GetProperty("compareOrders").GetInt32()));

        // Год назад данных нет: сравнение помечено неполным, а не «ноль продаж».
        var ago = await RunAsync("get_sales_period", new { period = "2026-09", agent, compare = "year_ago" });
        var coverage = ago.GetProperty("compare").GetProperty("coverage");
        Assert.False(coverage.GetProperty("complete").GetBoolean());
        Assert.Equal(["01.09.2025 — 30.09.2025"], coverage.GetProperty("missing").EnumerateArray().Select(m => m.GetString()));
    }

    [PostgresFact]
    public async Task Telegram_reports_are_visible_only_with_the_permission()
    {
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var role = new Role { Name = $"Telegram {Guid.NewGuid():N}"[..20] };
            role.Permissions.Add(new RolePermission { Code = Permissions.TelegramManage });
            var user = new User { Login = $"tg-{Guid.NewGuid():N}"[..20], FullName = "Админ Telegram" };
            user.Roles.Add(new UserRole { Role = role });
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var manager = await RunAsync("get_telegram", new { days = 3 }, user.Id);
            var jobs = manager.GetProperty("reports").GetProperty("jobs").EnumerateArray().ToList();
            Assert.Equal(ReportCatalog.Jobs.Count, jobs.Count);
            Assert.All(jobs, j => Assert.False(j.GetProperty("enabled").GetBoolean()));
            Assert.Equal(JsonValueKind.Array, manager.GetProperty("campaigns").ValueKind); // telegram.manage видит и рассылки
        }

        // Администратор продаж без прав Telegram — разделов нет.
        var sales = await RunAsync("get_telegram", new { });
        Assert.False(sales.TryGetProperty("reports", out _));
        Assert.False(sales.TryGetProperty("campaigns", out _));
    }
}
