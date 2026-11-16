using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Analytik;
using OneBase.AI.Tools;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Sales.Visits;
using OneBase.Domain.Sales;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>Счёт номера визита без базы: пример из вопроса («4 раза был в точке, заказ — на третьем»), сброс на заказе, 5+, дни до периода.</summary>
public class VisitSequenceCounterTests
{
    private static readonly DateOnly From = new(2025, 3, 3);

    private static VisitDay V(int day, bool order = false) => new(From.AddDays(day), true, order);

    private static VisitDay O(int day) => new(From.AddDays(day), false, true);

    [Fact]
    public void Order_on_the_third_of_four_visits_is_counted_on_visit_three()
    {
        var counter = new VisitSequenceCounter();
        Assert.Equal(1, counter.Add([V(0), V(1), V(2, order: true), V(3)], From)); // на конец — серия без заказа из одного визита
        var g = counter.Result("all", "Все");

        Assert.Equal((1, 4, 1, 0), (g.Pairs, g.Visits, g.OrdersOnVisit, g.OrdersWithoutVisit));
        Assert.Equal([0, 0, 1, 0, 0], g.Buckets.Select(b => b.Orders));
        Assert.Equal([2, 1, 1, 0, 0], g.Buckets.Select(b => b.Reached)); // четвёртый визит после заказа — снова первый
        Assert.Equal(1.0, g.Buckets[2].Conversion);
        Assert.Equal(0.0, g.Buckets[0].Conversion);
        Assert.Equal((3.0, 3.0, 0.0), (g.AverageVisitsToOrder!.Value, g.MedianVisitsToOrder!.Value, g.FirstVisitShare!.Value));
        Assert.Equal((1, 1.0), (g.OpenPairs, g.AverageOpenStreak!.Value)); // после заказа был визит, заказа ещё нет
        Assert.Equal(["1", "2", "3", "4", "5+"], g.Buckets.Select(b => b.Label));
    }

    [Fact]
    public void Order_without_visit_resets_count_days_before_period_only_set_it_and_long_series_go_to_five_plus()
    {
        var a = new VisitSequenceCounter();
        // До периода: два визита без заказа — счёт 2; в периоде: третий и четвёртый визиты, заказ без визита, затем 1-й визит с заказом.
        a.Add([V(-5), V(-2), V(0), V(1), O(2), V(3, order: true)], From);
        var b = new VisitSequenceCounter();
        Assert.Equal(0, b.Add([V(0), V(1), V(2), V(3), V(4), V(5), V(6, order: true)], From)); // заказ на 7-м — «5+», серии нет
        Assert.Equal(0, new VisitSequenceCounter().Add([V(-9), V(-3)], From)); // визиты только до периода — пары нет
        var total = new VisitSequenceCounter();
        total.Merge(a);
        total.Merge(b);
        var g = total.Result("all", "Все");

        Assert.Equal((2, 10, 2, 1), (g.Pairs, g.Visits, g.OrdersOnVisit, g.OrdersWithoutVisit)); // 3 + 7 визитов в периоде
        Assert.Equal([1, 0, 0, 0, 1], g.Buckets.Select(x => x.Orders));
        Assert.Equal([2, 1, 2, 2, 3], g.Buckets.Select(x => x.Reached)); // визиты до периода в «дошли» не входят
        Assert.Equal(0.5, g.Buckets[0].Share);
        Assert.Equal((4.0, 0), (g.AverageVisitsToOrder!.Value, g.OpenPairs)); // (1 + 7) / 2
    }
}

/// <summary>
/// Визиты ТП на PostgreSQL: номер визита по паре «ТП × точка» с днями до периода, заказы без визита и отменённые, область пользователя
/// (ТП — только свои точки, регион — точки своих филиалов), «День ТП» (точки по порядку отметок, заказы со строками, пропущенная точка
/// маршрута, номер визита, что точка обычно берёт), инструменты ИИ с правами.
/// </summary>
public class VisitAnalyticsDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private const long AgentA = 9_400_001, AgentB = 9_400_002, MarketM = 9_400_101, MarketN = 9_400_102, MarketZ = 9_400_103, Cake = 9_400_201, Bamboo = 9_400_202;
    private static readonly DateOnly Mar3 = new(2025, 3, 3);
    private static readonly long[] OrderIds = [9_400_301, 9_400_302, 9_400_303, 9_400_304, 9_400_305];
    private static readonly long[] VisitIds = Enumerable.Range(0, 11).Select(i => 9_400_401L + i).ToArray();

    private static DateTime At(DateOnly day, int hour, int minute = 0) => day.ToDateTime(new TimeOnly(hour, minute));

    private async Task SeedAsync(OneBaseDbContext db)
    {
        await CleanAsync(db);
        db.LinkoUsers.AddRange(
            new LinkoUser { Id = AgentA, FirstName = "Тест", SecondName = "Агент А", IsActive = true, JobName = "Агент" },
            new LinkoUser { Id = AgentB, FirstName = "Тест", SecondName = "Агент Б", IsActive = true, JobName = "Агент" });
        db.LinkoMarkets.AddRange(
            new LinkoMarket { Id = MarketM, Name = "Магазин «М»", BranchId = 5, BranchName = "Хоразм", Address = "ул. Тестовая, 1", Lat = 41.55, Lon = 60.63 },
            new LinkoMarket { Id = MarketN, Name = "Минимаркет «Н»", BranchId = 5, BranchName = "Хоразм" });
        db.LinkoProducts.AddRange(new LinkoProduct { Id = Cake, Name = "Тест Кекс 1 кг", TypeId = 10 }, new LinkoProduct { Id = Bamboo, Name = "Тест Бамбук 400 г", TypeId = 4 });

        var v = 0;
        void Visit(DateOnly day, int hour, long market, string status = "done", bool inPlan = true) =>
            db.LinkoVisits.Add(new LinkoVisit { Id = VisitIds[v++], Date = status == "done" ? At(day, hour) : day.ToDateTime(TimeOnly.MinValue), Day = day, Status = status, IsInPlan = inPlan, MarketId = market, UserId = AgentA });
        Visit(Mar3.AddDays(-11), 10, MarketM); // 20.02 — визит с заказом: счёт сбрасывается
        Visit(Mar3.AddDays(-7), 10, MarketM); // 24.02 — визит без заказа (счёт 1, до периода)
        Visit(Mar3, 9, MarketM); // 03.03 — 2-й
        Visit(Mar3, 9, MarketN, inPlan: false); // 03.03 — Н: 1-й, с заказом
        Visit(Mar3.AddDays(1), 10, MarketM); // 04.03 — 3-й
        Visit(Mar3.AddDays(2), 11, MarketM); // 05.03 — 4-й, с заказом
        Visit(Mar3.AddDays(2), 0, MarketN, status: "pending"); // 05.03 — Н по маршруту не посещена
        Visit(Mar3.AddDays(3), 12, MarketM); // 06.03 — 1-й после заказа, заказа нет

        var o = 0;
        void Order(DateOnly day, int hour, long market, long product, decimal kg, decimal sum, string status = "delivered")
        {
            var id = OrderIds[o++];
            db.LinkoOrders.Add(new LinkoOrder
            {
                Id = id, CreatedAt = At(day, hour, 1), CreatedDate = day, Status = status, MarketId = market, BranchId = 5, BranchName = "Хоразм", AgentId = AgentA,
                TotalPrice = sum, TotalWeight = kg,
            });
            db.LinkoOrderLines.Add(new LinkoOrderLine { Id = id, OrderId = id, ProductId = product, Amount = kg, Price = sum / kg, TotalPrice = sum, TotalWeight = kg });
        }

        Order(Mar3.AddDays(-11), 10, MarketM, Cake, 15, 450_000); // 20.02
        Order(Mar3, 9, MarketN, Bamboo, 10, 300_000); // 03.03 — на визите
        Order(Mar3.AddDays(1), 15, MarketN, Cake, 5, 150_000); // 04.03 — без визита (по телефону)
        Order(Mar3.AddDays(2), 11, MarketM, Cake, 20, 600_000); // 05.03 — на 4-м визите
        Order(Mar3.AddDays(2), 16, MarketM, Bamboo, 3, 90_000, status: "cancelled"); // 05.03 — отменён: не продажа
        await db.SaveChangesAsync();
    }

    private static async Task CleanAsync(OneBaseDbContext db)
    {
        await db.LinkoOrderLines.Where(l => OrderIds.Contains(l.Id)).ExecuteDeleteAsync();
        await db.LinkoOrders.Where(x => OrderIds.Contains(x.Id)).ExecuteDeleteAsync();
        await db.LinkoVisits.Where(x => VisitIds.Contains(x.Id)).ExecuteDeleteAsync();
        await db.LinkoProducts.Where(x => x.Id == Cake || x.Id == Bamboo).ExecuteDeleteAsync();
        await db.LinkoMarkets.Where(x => x.Id == MarketM || x.Id == MarketN || x.Id == MarketZ).ExecuteDeleteAsync();
        await db.LinkoUsers.Where(x => x.Id == AgentA || x.Id == AgentB).ExecuteDeleteAsync();
    }

    [PostgresFact]
    public async Task Visit_number_counts_pairs_with_days_before_period_orders_without_visit_and_respects_scope()
    {
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        await SeedAsync(db);
        try
        {
            var service = sp.GetRequiredService<VisitSequenceService>();
            var report = await service.BuildAsync(SalesRowScope.Organization, Mar3, Mar3.AddDays(3), null, AgentA, default);
            var g = report.Total;
            Assert.Equal((2, 5, 2, 1), (g.Pairs, g.Visits, g.OrdersOnVisit, g.OrdersWithoutVisit));
            Assert.Equal([1, 0, 0, 1, 0], g.Buckets.Select(b => b.Orders)); // Н — на 1-м визите, М — на 4-м (счёт начат 24.02)
            Assert.Equal([2, 1, 1, 1, 0], g.Buckets.Select(b => b.Reached));
            Assert.Equal((2.5, 0.5, 1), (g.AverageVisitsToOrder!.Value, g.FirstVisitShare!.Value, g.OpenPairs)); // отменённый заказ 05.03 не считается
            var agent = Assert.Single(report.Agents);
            Assert.Equal("Тест Агент А", agent.Name);

            // Серия без заказа: М — визит 06.03 после заказа 05.03; у Н счёт сброшен заказом без визита 04.03.
            var streak = Assert.Single(report.OpenStreaks);
            Assert.Equal((AgentA, "Тест Агент А", MarketM, "Магазин «М»", 1, Mar3.AddDays(3), (DateOnly?)Mar3.AddDays(2), 1),
                (streak.AgentId, streak.Agent, streak.MarketId, streak.Market, streak.Visits, streak.LastVisit, streak.LastOrder, report.OpenStreaksTotal));

            // Период по 05.03: у М серии нет, у Н — тоже (заказ 03.03 на визите, дальше визитов нет).
            Assert.Empty((await service.BuildAsync(SalesRowScope.Organization, Mar3, Mar3.AddDays(2), null, AgentA, default)).OpenStreaks);

            // Период до 04.03: М — 24.02, 03.03, 04.03 без заказа после 20.02.
            var early = Assert.Single((await service.BuildAsync(SalesRowScope.Organization, Mar3, Mar3.AddDays(1), null, AgentA, default)).OpenStreaks);
            Assert.Equal((3, Mar3.AddDays(1), (DateOnly?)Mar3.AddDays(-11)), (early.Visits, early.LastVisit, early.LastOrder));

            // Визит в точку «Завода» (её заказы — не вторичка) не в счёте: ни визитов, ни серии без заказа.
            db.LinkoMarkets.Add(new LinkoMarket { Id = MarketZ, Name = "Дегустация", BranchId = 4, BranchName = "Завод" });
            db.LinkoVisits.Add(new LinkoVisit { Id = VisitIds[^1], Date = At(Mar3, 13), Day = Mar3, Status = "done", IsInPlan = false, MarketId = MarketZ, UserId = AgentA });
            await db.SaveChangesAsync();
            var withFactory = await service.BuildAsync(SalesRowScope.Organization, Mar3, Mar3.AddDays(3), null, AgentA, default);
            Assert.Equal((2, 5, 1), (withFactory.Total.Pairs, withFactory.Total.Visits, withFactory.OpenStreaksTotal));
            Assert.Contains("счёт начинается заново", report.Rule);

            // ТП видит только себя; регион вне его филиалов — пусто.
            var other = new SalesRowScope(SalesRowScope.AgentLevel, [], [], [], AgentB, "ТП Б");
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.BuildAsync(other, Mar3, Mar3.AddDays(3), null, AgentA, default));
            Assert.Equal(0, (await service.BuildAsync(other, Mar3, Mar3.AddDays(3), null, null, default)).Total.Visits);
            var foreign = new SalesRowScope(SalesRowScope.RegionLevel, [999_999], [Guid.NewGuid()], [], null, "Чужой регион");
            Assert.Equal(0, (await service.BuildAsync(foreign, Mar3, Mar3.AddDays(3), null, AgentA, default)).Total.Visits);
            await Assert.ThrowsAsync<ArgumentException>(() => service.BuildAsync(SalesRowScope.Organization, Mar3, Mar3.AddDays(400), null, null, default));
        }
        finally
        {
            await CleanAsync(db);
        }
    }

    [PostgresFact]
    public async Task Agent_day_lists_where_the_agent_went_what_was_sold_and_not_with_visit_number_and_gaps()
    {
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        await SeedAsync(db);
        try
        {
            var days = sp.GetRequiredService<AgentDayService>();

            // 05.03: М — 4-й визит с заказом (отменённый — не в продажах), Н — точка маршрута без визита.
            var d5 = (await days.BuildAsync(AgentA, Mar3.AddDays(2), default))!;
            Assert.Equal("Тест Агент А", d5.Agent);
            var m = d5.Stops[0];
            Assert.Equal(("done", "Магазин «М»", new TimeOnly(11, 0), 4), (m.Status, m.Market, m.At!.Value, m.VisitNumber!.Value));
            Assert.Equal((600_000m, 20m), (m.Sum, m.Kg));
            Assert.Equal("Тест Кекс 1 кг", Assert.Single(Assert.Single(m.Orders).Lines).Product);
            Assert.Equal(("ул. Тестовая, 1", 41.55), (m.Address, m.Lat!.Value));
            var n = d5.Stops[1];
            Assert.Equal(("missed", true, (int?)null), (n.Status, n.InPlan, n.VisitNumber));
            var s = d5.Summary;
            Assert.Equal((2, 1, 1, 0, 1, 1, 1, 600_000m, 1), (s.Planned, s.Done, s.DoneInPlan, s.OffPlan, s.Missed, s.WithOrder, s.Orders, s.Sum, s.Cancelled));
            Assert.Equal(1.0, s.Strike);
            Assert.Contains(d5.Notes, x => x.Contains("одна отметка", StringComparison.Ordinal));

            // 04.03: М — визит без заказа (последний заказ точки 20.02, обычно берёт кекс); Н — заказ без визита.
            var d4 = (await days.BuildAsync(AgentA, Mar3.AddDays(1), default))!;
            var noOrder = d4.Stops.Single(x => x.MarketId == MarketM);
            Assert.Equal(("done", 0, new DateOnly(2025, 2, 20), 3), (noOrder.Status, noOrder.Orders.Count, noOrder.LastOrderDay!.Value, noOrder.VisitNumber!.Value));
            Assert.Equal(["Тест Кекс 1 кг"], noOrder.Usual);
            var phone = d4.Stops.Single(x => x.MarketId == MarketN);
            Assert.Equal(("no_visit", 150_000m), (phone.Status, phone.Sum));
            Assert.Equal(1, d4.Summary.OrdersWithoutVisit);

            // 03.03: две отметки — минуты между ними; заказ Н — на визите вне маршрута; в заказе М не было — «обычное» не считается.
            var d3 = (await days.BuildAsync(AgentA, Mar3, default))!;
            Assert.Equal((1, 1), (d3.Summary.DoneInPlan, d3.Summary.OffPlan));
            Assert.Equal(0, d3.Stops[1].GapMinutes); // обе отметки в 9:00
            Assert.Null(await days.BuildAsync(9_499_999, Mar3, default));
        }
        finally
        {
            await CleanAsync(db);
        }
    }

    [PostgresFact]
    public async Task Ai_tools_answer_visit_number_and_agent_day_for_the_whole_organization()
    {
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        await SeedAsync(db);
        try
        {
            var tools = sp.GetServices<ITool>().ToList();
            var sequence = tools.Single(t => t.Name == VisitSequenceTool.ToolName);
            var day = tools.Single(t => t.Name == AgentDayTool.ToolName);
            Assert.Equal("sales.stats", sequence.Source);
            Assert.Equal("sales.stats", day.Source);
            var context = new ToolContext("analyst.sales", fixture.AdminId, 0);

            var seq = await sequence.ExecuteAsync(context, JsonSerializer.SerializeToElement(new { date_from = "2025-03-03", date_to = "2025-03-06", agent = "Агент А" }));
            Assert.True(seq.Success, seq.Content);
            using (var json = JsonDocument.Parse(seq.Content))
            {
                var total = json.RootElement.GetProperty("total");
                Assert.Equal(2, total.GetProperty("ordersOnVisit").GetInt32());
                Assert.Equal(50.0, total.GetProperty("byVisitNumber")[3].GetProperty("sharePct").GetDouble());
                var streak = json.RootElement.GetProperty("openStreaks")[0];
                Assert.Equal(("Магазин «М»", 1, "2025-03-05"),
                    (streak.GetProperty("market").GetString(), streak.GetProperty("visitsWithoutOrder").GetInt32(), streak.GetProperty("lastOrder").GetString()));
                Assert.Equal(1, json.RootElement.GetProperty("openStreaksTotal").GetInt32());
            }

            var dayResult = await day.ExecuteAsync(context, JsonSerializer.SerializeToElement(new { agent = AgentA.ToString(), date = "2025-03-05" }));
            Assert.True(dayResult.Success, dayResult.Content);
            Assert.Contains("Магазин «М»", dayResult.Content);
            Assert.Contains("/sales/agents/9400001/day?date=2025-03-05", dayResult.Content);

            // Неоднозначное имя — подсказка с id, а не первый попавшийся.
            var ambiguous = await day.ExecuteAsync(context, JsonSerializer.SerializeToElement(new { agent = "Тест Агент", date = "2025-03-05" }));
            Assert.False(ambiguous.Success);
            Assert.Contains("9400001", ambiguous.Content);
        }
        finally
        {
            await CleanAsync(db);
        }
    }
}
