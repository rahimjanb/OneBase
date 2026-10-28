using System.Text.Json;
using OneBase.AI.Agents;
using OneBase.AI.Knowledge;
using OneBase.AI.Orchestration;
using OneBase.AI.Providers;
using OneBase.AI.Tools;
using OneBase.AI.Tools.Data;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Security;

namespace OneBase.AI.Tests;

/// <summary>
/// get_lapsed_customers без базы: схема, источник и право, разбор аргументов до чтения данных (загрузчик не нужен — null), подписи
/// периода, список категорий через запятую, маршрут вопроса «кто не купил» к Sales AI. Расчёт — CustomerLapseTests (OneBase.Sales.Tests).
/// </summary>
public class LapsedCustomersToolTests
{
    private static GetLapsedCustomersTool Tool() => new(null!, new SalesOptions(), null!);

    private static Task<ToolResult> Run(string json) =>
        Tool().ExecuteAsync(new ToolContext("sales", null, 0), JsonDocument.Parse(json).RootElement);

    [Fact]
    public void Tool_reads_secondary_sales_with_sales_permission_and_describes_its_arguments()
    {
        var tool = Tool();
        Assert.Equal(("get_lapsed_customers", KnowledgeSources.SalesSecondary, Permissions.SalesRead), (tool.Name, tool.Source, tool.RequiredPermission));
        Assert.False(tool.IsCritical);
        Assert.Equal(["category", "product", "year", "month", "region", "agent", "base_months", "largest", "status", "limit"],
            tool.InputSchema.GetProperty("properties").EnumerateObject().Select(p => p.Name));
        Assert.All(tool.InputSchema.GetProperty("properties").EnumerateObject(), p => Assert.Matches("[а-яА-Я]", p.Value.GetProperty("description").GetString()!));
        // Модель должна отличать его от get_customers: тот даёт только количество молчащих.
        Assert.Contains("get_customers", tool.UsageHint);
        Assert.Contains("помадке", tool.Description);
    }

    [Theory]
    [InlineData("""{"limit":51}""", "limit — от 1 до 50")]
    [InlineData("""{"base_months":4}""", "base_months — от 1 до 3")]
    [InlineData("""{"largest":0}""", "largest — от 1 до 500")]
    [InlineData("""{"status":"lost"}""", "not_bought")]
    public async Task Wrong_arguments_are_explained_before_reading_data(string json, string expected)
    {
        var result = await Run(json);
        Assert.False(result.Success);
        Assert.Contains(expected, result.Content);
    }

    [Fact]
    public void Status_accepts_codes_and_russian_words()
    {
        Assert.Equal("not_bought", GetLapsedCustomersTool.StatusOf(null));
        Assert.Equal("not_bought", GetLapsedCustomersTool.StatusOf("не купили"));
        Assert.Equal("bought", GetLapsedCustomersTool.StatusOf("Купили"));
        Assert.Equal("all", GetLapsedCustomersTool.StatusOf("all"));
    }

    [Fact]
    public void Base_period_is_written_in_words()
    {
        Assert.Equal("сентябрь 2026", GetLapsedCustomersTool.BaseText(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30)));
        Assert.Equal("июль – сентябрь 2026", GetLapsedCustomersTool.BaseText(new DateOnly(2026, 7, 1), new DateOnly(2026, 9, 30)));
        Assert.Equal("ноябрь 2025 – январь 2026", GetLapsedCustomersTool.BaseText(new DateOnly(2025, 11, 1), new DateOnly(2026, 1, 31)));
    }

    [Fact]
    public void Categories_are_split_by_comma_but_not_inside_numbers()
    {
        Assert.Equal(["Помадка 0,420 кг"], GetLapsedCustomersTool.Parts("Помадка 0,420 кг"));
        Assert.Equal(["помадка", "шоколад"], GetLapsedCustomersTool.Parts("помадка, шоколад"));
        Assert.Equal(["помадка", "печенье"], GetLapsedCustomersTool.Parts("помадка;печенье"));
        Assert.Equal(["Помадка 0,5/0,420 кг"], GetLapsedCustomersTool.Parts(" Помадка 0,5/0,420 кг "));
    }

    [Theory]
    [InlineData("Кто из клиентов по помадке, которые покупали в сентябре, ещё не купил в октябре?")]
    [InlineData("Кто из крупнейших клиентов по помадке ещё не купил в октябре?")]
    public void Lapsed_customer_questions_go_to_sales(string question)
    {
        var decision = AiRouter.ByKeywords(question, [Agent("sales"), Agent("marketing"), Agent("hr")], AiTokenUsage.None, ["помадка"]);
        Assert.Contains("sales", decision.Agents);
        Assert.DoesNotContain("hr", decision.Agents);
    }

    private static AgentConfig Agent(string code) => new(code, code + " AI", "роль", "описание", "prompt", true, null, null, null, true, 1, code, null, []);

    [Fact]
    public void Row_summary_keeps_bought_other_goods_and_visits_in_one_phrase()
    {
        var october = new PeriodInfo(2026, 10, new DateOnly(2026, 10, 28), 28, 31, new DateOnly(2026, 9, 28), "rop", []);
        CustomerLapseRow Row(bool bought, decimal other, int visits, DateOnly? last, int planned = 0, int missed = 0, int outside = 0) =>
            new(1, "Точка", null, "Север", 1, "ТП 1", 1, 100, 4_000_000, 1, null, bought ? 12.5m : 0, bought ? 500_000 : 0, bought, other, visits, last, planned, missed, outside);

        Assert.Equal("не купил; другие товары — 3 430 000 сум; визитов в октябре: 3, последний 22.10 (по маршруту 4, не выполнено 1)",
            GetLapsedCustomersTool.RowSummary(Row(false, 3_430_000, 3, new DateOnly(2026, 10, 22), 4, 1), october).Replace(' ', ' '));
        Assert.Equal("не купил; визитов в октябре: 0 (в маршруте не было)", GetLapsedCustomersTool.RowSummary(Row(false, 0, 0, null), october));
        Assert.Equal("не купил; визитов в октябре: 0 (по маршруту 3, не выполнено 3)", GetLapsedCustomersTool.RowSummary(Row(false, 0, 0, null, 3, 3), october));
        Assert.Equal("не купил; визитов в октябре: 4, последний 23.10 (по маршруту 4, не выполнено 3; вне маршрута 3)", // ТП ходил не в плановые дни
            GetLapsedCustomersTool.RowSummary(Row(false, 0, 4, new DateOnly(2026, 10, 23), 4, 3, 3), october));
        Assert.Equal("не купил; визитов в октябре: 1, последний 05.10 (в маршруте не было; вне маршрута 1)",
            GetLapsedCustomersTool.RowSummary(Row(false, 0, 1, new DateOnly(2026, 10, 5), outside: 1), october));
        Assert.StartsWith("купил: 12,5 кг на 500 000 сум", GetLapsedCustomersTool.RowSummary(Row(true, 0, 1, new DateOnly(2026, 10, 3)), october).Replace(' ', ' '));
        Assert.Equal(["январе", "мае", "марте", "августе", "сентябре"], new[] { 1, 5, 3, 8, 9 }.Select(GetLapsedCustomersTool.MonthIn));
    }

    [Fact]
    public void Category_plans_list_planned_categories_and_those_sold_without_plan()
    {
        var json = JsonSerializer.Serialize(SalesTool.CategoryPlans(
        [
            new CategoryPlanFact(-8, "Помадка", 60_000, 52_000, 2_000_000_000, 52m / 60, RemainingKg: 8_000, ForecastKg: 59_000, ForecastExecution: 59m / 60),
            new CategoryPlanFact(-4, "Бамбук", null, 1_000, 40_000_000, null),
            new CategoryPlanFact(-5, "Шоколад", null, 0, 0, null),
        ]), DataTool.Json);
        using var doc = JsonDocument.Parse(json);
        var row = Assert.Single(doc.RootElement.GetProperty("rows").EnumerateArray());
        Assert.Equal(("Помадка", 60_000m, 86.7m, 98.3m), (row.GetProperty("category").GetString(), row.GetProperty("planKg").GetDecimal(),
            row.GetProperty("executionPct").GetDecimal(), row.GetProperty("forecastExecutionPct").GetDecimal()));
        Assert.Equal(["Бамбук"], doc.RootElement.GetProperty("withoutPlan").EnumerateArray().Select(x => x.GetString())); // без продаж — не в списке
        Assert.Contains("CategoryPlans", new GetSalesTool(null!).UsageHint);
    }
}
