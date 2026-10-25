using OneBase.AI.Agents;
using OneBase.AI.Consultant;

namespace OneBase.AI.Tests;

public class AgentResultTests
{
    private static readonly AgentConfig Sales = new("sales", "Sales AI", "Директор по продажам", null, "prompt", true, null, null, null, true, 2, "sales", null, []);

    [Fact]
    public void Date_and_time_go_into_the_user_message_so_the_system_prompt_stays_cacheable()
    {
        var now = new DateTimeOffset(2026, 10, 25, 13, 40, 0, TimeSpan.FromHours(5));
        var task = new AgentTask("sales", "Как продажи за октябрь?", Guid.NewGuid(), Context: "Пользователь — РМ Хорезма");

        var message = AgentRunner.UserMessage(task, now);

        Assert.StartsWith("Сегодня 25.10.2026, 13:40 по Ташкенту.", message);
        Assert.Contains("Как продажи за октябрь?", message);
        Assert.EndsWith("Контекст от главного консультанта:\nПользователь — РМ Хорезма", message);
        Assert.Equal("Сегодня 25.10.2026, 13:40 по Ташкенту.\n\nКак продажи?", AgentRunner.UserMessage(task with { Context = null, Task = "Как продажи?" }, now));
    }

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
