using System.Text.Json;
using OneBase.AI.Agents;
using OneBase.AI.Knowledge;
using OneBase.AI.Orchestration;
using OneBase.AI.Providers;
using OneBase.AI.Tools;
using OneBase.AI.Tools.Data;
using OneBase.Application.Sales;
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
}
