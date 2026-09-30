using OneBase.AI.Agents;
using OneBase.AI.Consultant;

namespace OneBase.AI.Tests;

public class AgentResultTests
{
    private static readonly AgentConfig Sales = new("sales", "Sales AI", "Директор по продажам", null, "prompt", true, null, null, null, true, 2, "sales", null, []);

    [Fact]
    public void Structured_result_is_read_from_json_even_with_text_around()
    {
        const string text = """
            Вот итог:
            ```json
            {"agent":"sales","status":"completed","summary":"Продажи выросли.",
             "findings":["Факт сентября 380 т"],"metrics":[{"name":"Факт","value":"380","unit":"т"},{"name":"АКБ","value":13025}],
             "problems":["Самарканд ниже плана"],"recommendations":["Разобрать агентов Самарканда"],
             "data_sources":[{"title":"Продажи","period":"сентябрь 2026"},"Планы"]}
            ```
            """;
        var toolSource = new DataSource("Продажи: регионы", "сентябрь 2026", "/sales/republic");

        var result = AgentRunner.Parse(Sales, text, ["get_sales_by_branch"], [toolSource]);

        Assert.Equal("completed", result.Status);
        Assert.Equal("Продажи выросли.", result.Summary);
        Assert.Equal(["Факт сентября 380 т"], result.Findings);
        Assert.Equal(new Metric("АКБ", "13025", null), result.Metrics[1]);
        Assert.Equal(["Самарканд ниже плана"], result.Problems);
        Assert.Equal(["Продажи: регионы", "Продажи", "Планы"], result.DataSources.Select(s => s.Title));
        Assert.Equal("/sales/republic", result.DataSources[0].Href);
        Assert.Equal(["get_sales_by_branch"], result.ToolsUsed);
    }

    [Fact]
    public void No_data_status_is_kept()
    {
        var result = AgentRunner.Parse(Sales, """{"status":"no_data","summary":"Данных о кампаниях нет."}""", [], []);

        Assert.Equal("no_data", result.Status);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Plain_text_answer_becomes_partial_summary()
    {
        var result = AgentRunner.Parse(Sales, "Не удалось сформировать JSON, но продажи выросли.", [], []);

        Assert.Equal("partial", result.Status);
        Assert.Contains("продажи выросли", result.Summary);
    }
}
