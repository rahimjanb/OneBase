using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Agents;
using OneBase.AI.Knowledge;
using OneBase.AI.Tools;
using OneBase.AI.Tools.Data;
using OneBase.Domain.Sales;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// Детализация до записей на PostgreSQL: визиты ТП по точкам (по маршруту, вне маршрута, не посещено, заказ с визита и без),
/// заказы с позициями, карточка точки, поиск ТП по имени кириллицей и латиницей. Плюс: у каждого AI-сотрудника — все источники данных.
/// </summary>
public class DrillToolsDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private const string Day = "2026-10-20";
    private static readonly DateOnly Date = new(2026, 10, 20);

    private async Task SeedAsync()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await db.LinkoVisits.Where(v => v.Id >= 7_000_001 && v.Id <= 7_000_010).ExecuteDeleteAsync();
        await db.LinkoOrderLines.Where(l => l.Id >= 7_000_001 && l.Id <= 7_000_010).ExecuteDeleteAsync();
        await db.LinkoOrders.Where(o => o.Id >= 7_000_001 && o.Id <= 7_000_010).ExecuteDeleteAsync();
        await db.LinkoMarkets.Where(m => m.Id >= 3001 && m.Id <= 3004).ExecuteDeleteAsync();
        await db.LinkoUsers.Where(u => u.Id == 91 || u.Id == 92).ExecuteDeleteAsync();

        db.LinkoUsers.AddRange(
            new LinkoUser { Id = 91, FirstName = "Жасур", SecondName = "Хамидов", IsActive = true, JobName = "Агент" },
            new LinkoUser { Id = 92, FirstName = "Жасур", SecondName = "Алиев", IsActive = true, JobName = "Агент" });
        db.LinkoMarkets.AddRange(
            new LinkoMarket { Id = 3001, Name = "Магазин Нур", Address = "Ургенч, ул. Амира Темура, 5", BranchId = 5, BranchName = "Хоразм", ResponsibleAgentId = 91 },
            new LinkoMarket { Id = 3002, Name = "Мини-маркет Бахор", Address = "Ургенч, ул. Ал-Хорезми, 12", BranchId = 5, BranchName = "Хоразм" },
            new LinkoMarket { Id = 3003, Name = "Супермаркет Олтин", BranchId = 5, BranchName = "Хоразм" },
            new LinkoMarket { Id = 3004, Name = "Киоск Самандар", BranchId = 5, BranchName = "Хоразм" });

        // По маршруту: Нур — выполнен, Олтин — не посещён; вне маршрута: Бахор — выполнен.
        db.LinkoVisits.AddRange(
            new LinkoVisit { Id = 7_000_001, Date = Date.ToDateTime(new TimeOnly(10, 15)), Day = Date, Status = "done", IsInPlan = true, MarketId = 3001, UserId = 91 },
            new LinkoVisit { Id = 7_000_002, Date = Date.ToDateTime(new TimeOnly(11, 30)), Day = Date, Status = "done", IsInPlan = false, MarketId = 3002, UserId = 91 },
            new LinkoVisit { Id = 7_000_003, Date = Date.ToDateTime(new TimeOnly(9, 0)), Day = Date, Status = "pending", IsInPlan = true, MarketId = 3003, UserId = 91 });

        // Заказ с визита (Нур) и заказ без визита (Самандар — по телефону).
        db.LinkoOrders.AddRange(
            new LinkoOrder
            {
                Id = 7_000_001, CreatedAt = Date.ToDateTime(new TimeOnly(10, 20)), CreatedDate = Date, Status = "delivered", MarketId = 3001, BranchId = 5,
                BranchName = "Хоразм", AgentId = 91, TotalPrice = 2_000_000, TotalWeight = 50,
            },
            new LinkoOrder
            {
                Id = 7_000_002, CreatedAt = Date.ToDateTime(new TimeOnly(16, 0)), CreatedDate = Date, Status = "given", MarketId = 3004, BranchId = 5,
                BranchName = "Хоразм", AgentId = 91, TotalPrice = 500_000, TotalWeight = 12,
            });
        db.LinkoOrderLines.Add(new LinkoOrderLine { Id = 7_000_001, OrderId = 7_000_001, ProductId = 100, Amount = 50, Price = 40_000, TotalPrice = 2_000_000, TotalWeight = 50 });
        await db.SaveChangesAsync();
    }

    private async Task<JsonElement> RunAsync(string tool, object args)
    {
        using var scope = fixture.Services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();
        var result = await registry.Find(tool)!.ExecuteAsync(new ToolContext("sales", fixture.AdminId, 0), JsonSerializer.SerializeToElement(args));
        Assert.True(result.Success, result.Content);
        return JsonDocument.Parse(result.Content).RootElement.Clone();
    }

    [PostgresFact]
    public async Task Agent_visits_show_every_store_by_route_off_route_and_orders()
    {
        await SeedAsync();

        var visits = await RunAsync("get_agent_visits", new { agent = "Жасур Хамидов", date = Day });

        var totals = visits.GetProperty("totals");
        Assert.Equal((2, 1, 1, 2, 1), (totals.GetProperty("planned").GetInt32(), totals.GetProperty("doneByRoute").GetInt32(),
            totals.GetProperty("doneOffRoute").GetInt32(), totals.GetProperty("doneAll").GetInt32(), totals.GetProperty("notVisited").GetInt32()));
        Assert.Equal((1, 1), (totals.GetProperty("visitsWithOrder").GetInt32(), totals.GetProperty("ordersWithoutVisit").GetInt32()));

        var list = visits.GetProperty("visits").EnumerateArray().ToList();
        Assert.Equal(["Супермаркет Олтин", "Магазин Нур", "Мини-маркет Бахор"], list.Select(v => v.GetProperty("store").GetString())); // по времени
        var nur = list[1];
        Assert.Equal("2026-10-20 10:15", nur.GetProperty("time").GetString());
        Assert.True(nur.GetProperty("byRoute").GetBoolean());
        Assert.Equal(2_000_000m, nur.GetProperty("order").GetProperty("sumUzs").GetDecimal());
        Assert.False(list[2].TryGetProperty("order", out _)); // Бахор — визит без заказа
        Assert.Equal("Киоск Самандар", visits.GetProperty("ordersWithoutVisit")[0].GetProperty("store").GetString());
    }

    [PostgresFact]
    public async Task Agent_is_found_by_part_of_the_name_in_latin_and_ambiguous_names_are_listed()
    {
        await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<IToolRegistry>().Find("get_agent_visits")!;

        var ambiguous = await tool.ExecuteAsync(new ToolContext("sales", fixture.AdminId, 0), JsonSerializer.SerializeToElement(new { agent = "Жасур", date = Day }));
        Assert.False(ambiguous.Success);
        Assert.Contains("Жасур Хамидов (id 91", ambiguous.Content);
        Assert.Contains("Жасур Алиев (id 92", ambiguous.Content);

        var latin = await RunAsync("get_agent_visits", new { agent = "Jasur Xamidov", date = Day }); // узбекская латиница
        Assert.Equal(91, latin.GetProperty("agentId").GetInt64());
        var part = await RunAsync("get_agent_visits", new { agent = "хамид", date = Day });
        Assert.Equal(91, part.GetProperty("agentId").GetInt64());
    }

    [PostgresFact]
    public async Task Orders_come_with_lines_and_totals_by_status()
    {
        await SeedAsync();

        var orders = await RunAsync("get_orders", new { agent = "91", date = Day, lines = true });

        Assert.Equal(2, orders.GetProperty("total").GetProperty("orders").GetInt32());
        Assert.Equal(2_500_000m, orders.GetProperty("total").GetProperty("sumUzs").GetDecimal());
        Assert.Equal(["delivered", "given"], orders.GetProperty("byStatus").EnumerateArray().Select(s => s.GetProperty("status").GetString()).Order());
        var nur = orders.GetProperty("orders").EnumerateArray().Single(o => o.GetProperty("id").GetInt64() == 7_000_001);
        Assert.Equal("Магазин Нур", nur.GetProperty("store").GetString());
        Assert.Equal(50m, Assert.Single(nur.GetProperty("lines").EnumerateArray()).GetProperty("amount").GetDecimal());

        var one = await RunAsync("get_orders", new { order = 7_000_002 });
        Assert.Equal("Киоск Самандар", Assert.Single(one.GetProperty("orders").EnumerateArray()).GetProperty("store").GetString());
    }

    [PostgresFact]
    public async Task Store_card_shows_responsible_agent_last_visits_and_orders()
    {
        await SeedAsync();

        var several = await RunAsync("get_store", new { store = "ургенч" }); // по адресу — две точки: список для выбора
        Assert.Equal(2, several.GetProperty("found").GetInt32());

        var store = await RunAsync("get_store", new { store = "Нур" });
        Assert.Equal("Жасур Хамидов", store.GetProperty("store").GetProperty("responsibleAgent").GetString());
        Assert.Equal("Жасур Хамидов", store.GetProperty("lastVisits")[0].GetProperty("agent").GetString());
        Assert.Equal(7_000_001, store.GetProperty("lastOrders")[0].GetProperty("id").GetInt64());
    }

    [PostgresFact]
    public async Task Page_tools_answer_with_data_or_a_readable_reason()
    {
        await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();
        foreach (var (tool, args) in new (string, object)[]
                 {
                     ("get_agent", new { agent = "91" }), ("get_kpi", new { }), ("get_kpi", new { region = "Хоразм" }), ("get_promo", new { }),
                     ("get_akt", new { }), ("get_dso", new { }),
                 })
        {
            var result = await registry.Find(tool)!.ExecuteAsync(new ToolContext("finance", fixture.AdminId, 0), JsonSerializer.SerializeToElement(args));
            Assert.False(string.IsNullOrWhiteSpace(result.Content), tool);
            if (result.Success)
            {
                JsonDocument.Parse(result.Content).Dispose();
            }
        }
    }

    [Fact]
    public void Page_json_keeps_the_first_rows_counts_all_and_rounds_fractions()
    {
        var node = PageJson.Of(new { Rows = Enumerable.Range(1, 50).Select(i => new { Id = i, Share = 1m / 3m }).ToList(), Sum = 1234567.891m, Count = 7 }, 10)!;

        Assert.Equal(10, node["rows"]!.AsArray().Count);
        Assert.Equal(50, (int)node["rowsTotal"]!);
        Assert.Equal(0.3333m, (decimal)node["rows"]![0]!["share"]!);
        Assert.Equal(1234567.9m, (decimal)node["sum"]!);
        Assert.Equal(7, (int)node["count"]!);
    }

    [Fact]
    public void Every_department_ai_employee_has_every_data_source()
    {
        var all = KnowledgeSources.All.Select(s => s.Code).Order().ToList();
        Assert.All(AgentDefaults.Departments, d => Assert.Equal(all, d.Sources.Order().ToList()));
    }
}
