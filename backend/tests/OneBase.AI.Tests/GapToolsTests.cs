using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Agents;
using OneBase.AI.Knowledge;
using OneBase.AI.Tools;
using OneBase.AI.Tools.Data;
using OneBase.Application.Sales;
using OneBase.Application.Security;
using OneBase.Domain.AI;
using OneBase.Domain.Sales;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>Инструменты к данным, которых AI раньше не видел: лидеры месяца HR, находки AI, проекты отделов, прайс-листы Linko.</summary>
public class GapToolsTests
{
    private static DataTool[] Tools() =>
    [
        new GetHrLeadersTool(null!, null!), new GetAiAlertsTool(null!, null!), new GetWorkProjectsTool(null!, null!, null!), new GetPricesTool(null!, new SalesOptions()),
    ];

    [Fact]
    public void Tools_read_their_sources_with_page_permissions()
    {
        Assert.Equal(
            [
                ("get_hr_leaders", KnowledgeSources.HrAttendance, Permissions.HrRead),
                ("get_ai_alerts", KnowledgeSources.AiAlerts, Permissions.AgentsRun),
                ("get_work_projects", KnowledgeSources.WorkTasks, Permissions.WorkTasks),
                ("get_prices", KnowledgeSources.SalesPrimary, Permissions.SalesRead),
            ],
            Tools().Select(t => (t.Name, t.Source!, t.RequiredPermission!)));
        foreach (var tool in Tools())
        {
            Assert.False(tool.IsCritical); // только чтение
            Assert.NotNull(KnowledgeSources.Find(tool.Source!));
            Assert.Matches("[а-яА-Я]", tool.Description);
            Assert.All(tool.InputSchema.GetProperty("properties").EnumerateObject(), p => Assert.Matches("[а-яА-Я]", p.Value.GetProperty("description").GetString()!));
        }
    }

    [Fact]
    public void Alerts_source_is_open_to_every_department_agent()
    {
        Assert.Equal(Permissions.AgentsRun, KnowledgeSources.Find(KnowledgeSources.AiAlerts)!.RequiredPermission);
        Assert.All(AgentDefaults.All.Where(a => a.Code != AgentDefaults.Director.Code), a => Assert.Contains(KnowledgeSources.AiAlerts, a.Sources));
    }

    [Theory]
    [InlineData("get_ai_alerts", """{"limit":51}""", "limit — от 1 до 50")]
    [InlineData("get_ai_alerts", """{"category":"logistics"}""", "category —")]
    [InlineData("get_work_projects", """{"limit":0}""", "limit — от 1 до 50")]
    [InlineData("get_prices", """{"limit":61}""", "limit — от 1 до 60")]
    public async Task Wrong_arguments_are_explained_before_reading_data(string name, string json, string expected)
    {
        var tool = Tools().Single(t => t.Name == name);
        var result = await tool.ExecuteAsync(new ToolContext("sales", Guid.NewGuid(), 0), JsonDocument.Parse(json).RootElement);
        Assert.False(result.Success);
        Assert.Contains(expected, result.Content);
    }
}

/// <summary>Находки AI и прайс-листы на базе: права находок, закрытые за 30 дней, цены по прайс-листам и категориям.</summary>
public class GapToolsDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static JsonElement Args(object value) => JsonSerializer.SerializeToElement(value);

    private ITool Tool(IServiceProvider sp, string name) => sp.GetRequiredService<IToolRegistry>().Find(name)!;

    [PostgresFact]
    public async Task Alerts_are_shown_by_the_user_rights_and_resolved_on_request()
    {
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            await db.AiAlerts.Where(a => a.Rule.StartsWith("gap-test")).ExecuteDeleteAsync();
            db.AiAlerts.AddRange(
                new AiAlert { Category = "sales", Rule = "gap-test-sales", Key = "1", Severity = AiAlertSeverity.Critical, Title = "Хоразм отстаёт", Message = "−20% к плану", RequiredPermission = Permissions.SalesRead },
                new AiAlert { Category = "finance", Rule = "gap-test-fin", Key = "2", Severity = AiAlertSeverity.Warning, Title = "Долг дилера", Message = "45 дней", RequiredPermission = Permissions.FinanceRead },
                new AiAlert { Category = "sales", Rule = "gap-test-opp", Key = "3", Severity = AiAlertSeverity.Opportunity, Title = "Новые точки", Message = "+40" },
                new AiAlert { Category = "sales", Rule = "gap-test-old", Key = "4", Severity = AiAlertSeverity.Warning, Title = "Закрыто", Message = "уже нет", ResolvedAt = DateTimeOffset.UtcNow.AddDays(-2) });
            await db.SaveChangesAsync();
        }

        using var s = fixture.Services.CreateScope();
        var tool = Tool(s.ServiceProvider, "get_ai_alerts");
        var viewer = new ToolContext("sales", fixture.ViewerId, 0); // права продаж, без «Финансов»

        var active = await tool.ExecuteAsync(viewer, Args(new { }));
        Assert.True(active.Success, active.Content);
        using (var json = JsonDocument.Parse(active.Content))
        {
            var titles = json.RootElement.GetProperty("alerts").EnumerateArray().Select(a => a.GetProperty("title").GetString()).ToList();
            Assert.Contains("Хоразм отстаёт", titles);
            Assert.Contains("Новые точки", titles);
            Assert.DoesNotContain("Долг дилера", titles); // находка финансов — без права «Финансы» не видна
            Assert.DoesNotContain("Закрыто", titles);
            Assert.Equal("Хоразм отстаёт", titles[0]); // критичные — первыми
        }

        var critical = await tool.ExecuteAsync(viewer, Args(new { severity = "критичные" }));
        Assert.Equal(1, JsonDocument.Parse(critical.Content).RootElement.GetProperty("total").GetInt32());

        var closed = await tool.ExecuteAsync(viewer, Args(new { resolved = true }));
        Assert.Contains("Закрыто", closed.Content);
        Assert.DoesNotContain("Хоразм отстаёт", closed.Content);
    }

    [PostgresFact]
    public async Task Prices_are_listed_per_price_list_for_products_and_categories()
    {
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            long[] products = [9_700_001, 9_700_002];
            await db.LinkoPriceListItems.Where(i => i.ProductId != null && products.Contains(i.ProductId.Value)).ExecuteDeleteAsync();
            await db.LinkoPriceLists.Where(l => l.Id == 9_700_101 || l.Id == 9_700_102).ExecuteDeleteAsync();
            await db.LinkoProducts.Where(p => products.Contains(p.Id)).ExecuteDeleteAsync();
            await db.LinkoProductTypes.Where(t => t.Id == 9_700_201).ExecuteDeleteAsync();
            db.LinkoProductTypes.Add(new LinkoProductType { Id = 9_700_201, Name = "Тестовая помадка 9 кг" });
            db.LinkoProducts.AddRange(
                new LinkoProduct { Id = 9_700_001, Name = "Тест ARBUZ 9 кг", Code = "T480", TypeId = 9_700_201 },
                new LinkoProduct { Id = 9_700_002, Name = "Тест OFARIN 9 кг", Code = "T484", TypeId = 9_700_201 });
            db.LinkoPriceLists.AddRange(new LinkoPriceList { Id = 9_700_101, Name = "Тест Диллер" }, new LinkoPriceList { Id = 9_700_102, Name = "Тест Магазин" });
            db.LinkoPriceListItems.AddRange(
                new LinkoPriceListItem { Id = 9_700_301, ProductId = 9_700_001, PriceListId = 9_700_101, Price = 100_000, Tm = 1 },
                new LinkoPriceListItem { Id = 9_700_302, ProductId = 9_700_001, PriceListId = 9_700_101, Price = 110_000, Tm = 2 }, // более поздняя запись
                new LinkoPriceListItem { Id = 9_700_303, ProductId = 9_700_001, PriceListId = 9_700_102, Price = 125_000, Tm = 1 },
                new LinkoPriceListItem { Id = 9_700_304, ProductId = 9_700_002, PriceListId = 9_700_102, Price = 130_000, Tm = 1 });
            await db.SaveChangesAsync();
        }

        using var s = fixture.Services.CreateScope();
        var tool = Tool(s.ServiceProvider, "get_prices");
        var context = new ToolContext("sales", fixture.AdminId, 0);

        var byCode = await tool.ExecuteAsync(context, Args(new { product = "T480" }));
        Assert.True(byCode.Success, byCode.Content);
        using (var json = JsonDocument.Parse(byCode.Content))
        {
            var product = Assert.Single(json.RootElement.GetProperty("products").EnumerateArray());
            Assert.Equal("Тест ARBUZ 9 кг", product.GetProperty("name").GetString());
            var prices = product.GetProperty("prices").EnumerateArray().ToDictionary(p => p.GetProperty("priceList").GetString()!, p => p.GetProperty("price").GetDecimal());
            Assert.Equal(new Dictionary<string, decimal> { ["Тест Диллер"] = 110_000, ["Тест Магазин"] = 125_000 }, prices);
        }

        var byType = await tool.ExecuteAsync(context, Args(new { category = "Тестовая помадка", price_list = "Тест Магазин" }));
        Assert.True(byType.Success, byType.Content);
        Assert.Equal(2, JsonDocument.Parse(byType.Content).RootElement.GetProperty("withPrice").GetInt32());

        var lists = await tool.ExecuteAsync(context, Args(new { price_list = "Тест" }));
        Assert.Contains("Тест Диллер", lists.Content);

        var missing = await tool.ExecuteAsync(context, Args(new { price_list = "Несуществующий" }));
        Assert.False(missing.Success);
    }
}
