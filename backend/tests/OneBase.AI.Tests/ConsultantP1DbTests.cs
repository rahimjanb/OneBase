using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Agents;
using OneBase.AI.Consultant;
using OneBase.AI.Knowledge;
using OneBase.AI.Llm;
using OneBase.AI.Tools;
using OneBase.Application.Security;

namespace OneBase.AI.Tests;

/// <summary>
/// P1 на настоящем цикле AI-сотрудника (ConsultantDb: PostgreSQL, сценарная модель без сети и платных вызовов): результат больше 40 000
/// символов доходит до модели валидным JSON с первыми строками и «_pagination», модель дочитывает остальные строки get_more_rows — каждая
/// строка ровно один раз и по порядку, данные не запрашиваются заново, повтор того же вызова не выполняет инструмент второй раз.
/// </summary>
public class ConsultantP1DbTests(ConsultantDb fixture) : IClassFixture<ConsultantDb>
{
    private const int Count = 1_200;

    private const string FinalJson =
        """{"agent":"sales","status":"completed","summary":"Всего ТП в выгрузке — 1200.","findings":["ТП в выгрузке — 1200"],"metrics":[],"problems":[],"recommendations":[],"data_sources":[]}""";

    private static readonly JsonElement RowsArgs = JsonSerializer.SerializeToElement(new { rows = Count });

    /// <summary>Следующий шаг модели по последнему результату: следующая страница, пока has_more; потом — тот же вызов ещё раз; потом — итог.</summary>
    private static IReadOnlyList<LlmToolCall>? Next(IReadOnlyList<LlmMessage> messages)
    {
        var last = messages[^1];
        if (last.Role != LlmRole.Tool)
        {
            return null;
        }

        var json = JsonNode.Parse(last.Content)!;
        if (json["repeat"] is not null)
        {
            return null;
        }

        var (id, more, offset) = json["_pagination"] is { } p
            ? ((string)p["result_id"]!, (bool)p["lists"]![0]!["has_more"]!, (int)p["lists"]![0]!["next_offset"]!)
            : ((string?)json["result_id"], (bool?)json["has_more"] ?? false, (int?)json["next_offset"] ?? 0);
        return more
            ? [new LlmToolCall($"page-{offset}", ToolResultPager.ToolName, JsonSerializer.SerializeToElement(new { result_id = id, path = "agents", offset }))]
            : [new LlmToolCall("again", ConsultantDb.Rows, RowsArgs)];
    }

    [PostgresFact]
    public async Task Large_result_reaches_the_model_whole_by_pages_without_duplicates_and_without_a_second_query()
    {
        var model = fixture.Model.Reset();
        model.Then([new LlmToolCall("first", ConsultantDb.Rows, RowsArgs)]);
        for (var i = 0; i < 6; i++)
        {
            model.Then(Next, FinalJson);
        }

        RowsTestTool.Executions = 0;
        using var scope = fixture.Services.CreateScope();
        var run = await scope.ServiceProvider.GetRequiredService<AgentRunner>()
            .RunAsync(new AgentTask("sales", "Перечисли всех ТП с продажами", fixture.AdminId), null, CancellationToken.None);

        Assert.Equal("completed", run.Result.Status);
        Assert.Equal(1, RowsTestTool.Executions); // страницы и повтор — без нового запроса к данным

        var tools = model.Calls[^1].Where(m => m.Role == LlmRole.Tool).ToList();
        var first = JsonNode.Parse(tools[0].Content)!;
        Assert.True(tools[0].Content.Length <= 40_000, $"{tools[0].Content.Length}");
        Assert.True(RowsTestTool.Content(Count).Length > 40_000);
        Assert.Equal(Count, (int)first["total"]!["rows"]!); // итоги — по всем строкам, на месте
        Assert.Equal(Count, (int)first["_pagination"]!["lists"]![0]!["total_count"]!);

        var rows = first["agents"]!.AsArray().Select(r => (int)r!["id"]!).ToList();
        var pages = tools.Skip(1).Select(t => JsonNode.Parse(t.Content)!).TakeWhile(p => p["rows"] is not null).ToList();
        Assert.NotEmpty(pages);
        foreach (var page in pages)
        {
            Assert.Equal(rows.Count, (int)page["offset"]!); // подряд: страница начинается там, где кончилась прошлая
            rows.AddRange(page["rows"]!.AsArray().Select(r => (int)r!["id"]!));
        }

        Assert.Equal(Enumerable.Range(0, Count), rows);
        Assert.False((bool)pages[^1]["has_more"]!);
        var repeat = JsonNode.Parse(tools[^1].Content)!;
        Assert.True((bool)repeat["repeat"]!);
        Assert.Equal("r1", (string)repeat["result_id"]!);

        // Страницы — отдельным инструментом get_more_rows, он виден в журнале сотрудника.
        Assert.Contains(ToolResultPager.ToolName, run.Result.ToolsUsed);

        // Системный промпт, который получила модель: выбор инструмента продаж по границе месяца и подсказка get_sales_period.
        var system = Assert.Single(model.Calls[0], m => m.Role == LlmRole.System).Content;
        Assert.Contains("внутри одного календарного месяца — get_sales_days", system);
        Assert.Contains("Даты пересекают границу месяца — сразу get_sales_period, без пробного get_sales_days", system);
        Assert.Contains("один вызов вместо вызова по каждому месяцу", system); // UsageHint get_sales_period
    }
}

/// <summary>Инструмент с длинным списком: rows строк ТП и итог по всем строкам. Executions — сколько раз он выполнен.</summary>
internal sealed class RowsTestTool : ITool
{
    public const string ToolName = "zz_sales_rows";

    public static int Executions;

    public string Name => ToolName;
    public string Title => "Список ТП (тест)";
    public string Description => "Тестовый инструмент: длинный список ТП.";
    public JsonElement InputSchema { get; } = JsonDocument.Parse("""{"type":"object","properties":{"rows":{"type":"integer"}}}""").RootElement.Clone();
    public bool IsCritical => false;
    public string? RequiredPermission => Permissions.SalesRead;
    public string? Source => KnowledgeSources.SalesSecondary;

    public static string Content(int count) => JsonSerializer.Serialize(new
    {
        total = new { rows = count, kg = Enumerable.Range(0, count).Sum(i => i % 50 + 1) },
        agents = Enumerable.Range(0, count).Select(i => new { id = i, name = $"ТП {i:0000} Хоразм", region = "Хоразм", kg = i % 50 + 1 }),
    });

    public Task<ToolResult> ExecuteAsync(ToolContext context, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref Executions);
        var count = arguments.TryGetProperty("rows", out var r) ? r.GetInt32() : 10;
        return Task.FromResult(new ToolResult(true, Content(count), null, [new DataSource("Список ТП (тест)", "сентябрь 2026")]));
    }
}
