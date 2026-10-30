using System.Text.Json;
using OneBase.AI.Agents;
using OneBase.AI.Consultant;
using OneBase.AI.Memory;
using OneBase.AI.Orchestration;
using OneBase.AI.Reports;
using OneBase.AI.Tools;
using OneBase.Application.Field;

namespace OneBase.AI.Tests;

/// <summary>
/// P0 аудита консультанта без базы и модели: сбои инструментов не превращаются в «данных нет», уже полученные данные сохраняются,
/// «нет доступа» и «источник не подключён» отличаются от «не найдено», прошлый отказ не записывается в память как факт, отчёт Excel — только по
/// просьбе.
/// </summary>
public class ConsultantFailureTests
{
    private static readonly AgentConfig Sales = new("sales", "Sales AI", "роль", "описание", "prompt", true, null, null, null, true, 2, "sales", null, []);

    private static AgentResult Result(string status) =>
        new("sales", "Sales AI", status, status == "no_data" ? "Данных нет." : "Продажи — 12 345 кг.", [], [], [], [], [], ["get_sales"]);

    private static ToolIssue Issue(string kind) => new("get_sales", kind, "текст");

    [Fact]
    public void Tool_error_without_data_is_a_failure_not_no_data()
    {
        var result = AgentRunner.Classify(Result("no_data"), 0, [Issue(ToolFailures.ToolError)]);

        Assert.Equal("failed", result.Status);
        Assert.Equal("tool_error", result.Reason);
        Assert.Contains("сбой источника", result.Error);
        Assert.Single(result.ToolErrors!);
    }

    [Fact]
    public void Tool_error_is_not_hidden_even_when_the_model_says_completed()
    {
        var result = AgentRunner.Classify(Result("completed"), 0, [Issue(ToolFailures.ToolError)]);

        Assert.Equal("failed", result.Status);
        Assert.Equal("tool_error", result.Reason);
    }

    [Fact]
    public void Tool_error_next_to_real_data_keeps_the_data()
    {
        var result = AgentRunner.Classify(Result("completed"), 1, [Issue(ToolFailures.ToolError)]);

        Assert.Equal("completed", result.Status);
        Assert.Equal("Продажи — 12 345 кг.", result.Summary);
        Assert.Equal("get_sales", Assert.Single(result.ToolErrors!).Tool);
    }

    [Fact]
    public void No_access_is_forbidden_and_missing_source_is_not_connected()
    {
        var forbidden = AgentRunner.Classify(Result("no_data"), 0, [Issue(ToolFailures.Forbidden)]);
        var notConnected = AgentRunner.Classify(Result("no_data"), 0, [Issue(ToolFailures.NotConnected)]);
        var notFound = AgentRunner.Classify(Result("no_data"), 2, []);

        Assert.Equal(("forbidden", "no_access"), (forbidden.Status, forbidden.Reason));
        Assert.Equal(("no_data", "source_not_connected"), (notConnected.Status, notConnected.Reason));
        Assert.Equal(("no_data", "not_found"), (notFound.Status, notFound.Reason));
    }

    [Fact]
    public void Interrupted_agent_returns_partial_with_what_tools_returned()
    {
        var content = """{"factKg":12345,"region":"Хоразм"}""";
        var result = AgentRunner.Salvage(Sales, "Анализ прерван: модель не ответила", [("Продажи по регионам", content), ("Пустой", "")],
            [new DataSource("Продажи → Республика", "октябрь 2026")], ["get_sales_by_branch"], [Issue(ToolFailures.ToolError)]);

        Assert.Equal("partial", result.Status);
        Assert.Equal("interrupted", result.Reason);
        Assert.Contains(result.Findings, f => f.Contains("12345") && f.StartsWith("Продажи по регионам"));
        Assert.Single(result.DataSources);
        Assert.Single(result.ToolErrors!);
    }

    [Fact]
    public void Salvage_keeps_the_answer_compact()
    {
        var big = new string('x', 50_000);
        var result = AgentRunner.Salvage(Sales, "прервано", [("A", big), ("B", big), ("C", big), ("D", big), ("E", big)], [], [], []);

        Assert.True(result.Findings.Sum(f => f.Length) < 13_000);
    }

    [Fact]
    public void Exception_text_with_internal_details_never_reaches_the_model()
    {
        var secret = new InvalidOperationException("Npgsql: Host=db;Username=onebase;Password=s3cr3t — connection refused");

        Assert.Equal(ToolFailures.ToolError, ToolFailures.KindOf(secret));
        Assert.DoesNotContain("s3cr3t", ToolFailures.SafeMessage(secret));
        Assert.DoesNotContain("Npgsql", ToolFailures.SafeMessage(secret));

        var failure = ToolResult.Failure("get_sales", ToolFailures.KindOf(secret), ToolFailures.SafeMessage(secret));
        Assert.False(failure.Success);
        Assert.Equal(ToolFailures.ToolError, failure.ErrorKind);
        Assert.DoesNotContain("s3cr3t", failure.Content);
    }

    [Fact]
    public void Domain_errors_keep_their_human_message_and_kind()
    {
        Assert.Equal((ToolFailures.NotFound, "Регион не найден среди складов дилеров."),
            (ToolFailures.KindOf(new KeyNotFoundException("Регион не найден среди складов дилеров.")),
                ToolFailures.SafeMessage(new KeyNotFoundException("Регион не найден среди складов дилеров."))));
        Assert.Equal(ToolFailures.InvalidArguments, ToolFailures.KindOf(new FieldValidationException("Укажите либо date, либо from и to.")));
        Assert.Equal(ToolFailures.OutOfRange, ToolFailures.KindOf(new FieldValidationException("Период — не больше 62 дней.")));
        Assert.Equal("Период — не больше 62 дней.", ToolFailures.SafeMessage(new FieldValidationException("Период — не больше 62 дней.")));
        Assert.Equal(ToolFailures.Forbidden, ToolFailures.KindOf(new FieldForbiddenException("Нет доступа к полю.")));
    }

    [Fact]
    public void Tool_failure_is_structured_for_the_model()
    {
        var failure = ToolResult.Failure("get_stock_reload", ToolFailures.ToolError, "Инструмент завершился внутренней ошибкой OneBase — его данные не получены.");
        using var json = JsonDocument.Parse(failure.Content);
        var error = json.RootElement.GetProperty("error");

        Assert.Equal("get_stock_reload", error.GetProperty("tool").GetString());
        Assert.Equal("tool_error", error.GetProperty("kind").GetString());
        Assert.Contains("другой инструмент", error.GetProperty("next").GetString());
        Assert.Contains("не что данных нет", error.GetProperty("next").GetString());
    }

    [Fact]
    public void Denied_areas_become_forbidden_or_not_connected_results()
    {
        IReadOnlyList<AgentConfig> all = [Sales, Sales with { Code = "hr", Name = "HR AI" }];
        var granted = new Dictionary<string, IReadOnlySet<string>> { ["sales"] = new HashSet<string> { "knowledge.documents" } };
        DeniedArea[] denied =
        [
            new("hr", null, []),
            new("sales", "Telegram: автоотчёты и рассылки", ["system.telegram"]),
            new("sales", "Документы базы знаний", ["knowledge.documents"]),
        ];

        var results = OrchestratedConsultantEngine.Denied(denied, all, granted, []).ToList();

        Assert.Equal(["forbidden", "forbidden", "no_data"], results.Select(r => r.Status));
        Assert.Equal(["no_access", "no_access", "source_not_connected"], results.Select(r => r.Reason));
        Assert.Equal("HR AI", results[0].AgentName);
        Assert.Contains("Telegram", results[1].AgentName);
        Assert.All(results, r => Assert.NotNull(r.Error));
    }

    [Theory]
    [InlineData("Какие автоотчёты ушли в Telegram?", false)]
    [InlineData("Какие автоотчеты ушли в Telegram вчера", false)]
    [InlineData("Какие отчёты отправлялись вчера?", false)]
    [InlineData("Когда пришёл отчёт по продажам?", false)]
    [InlineData("hisobot yuborildimi?", false)]
    [InlineData("Сделай отчёт по продажам в Excel", true)]
    [InlineData("Сформируй отчёт по остаткам", true)]
    [InlineData("hisobot tayyorla", true)]
    [InlineData("Sentabr bo'yicha hisobot tayyorlang", true)]
    [InlineData("Отчёт по продажам за сентябрь", true)]
    [InlineData("Можешь сделать отчёт по ТП?", true)]
    [InlineData("Выгрузи заказы за неделю", true)]
    public void Excel_report_needs_an_action_or_a_format(string question, bool expected) =>
        Assert.Equal(expected, ReportIntent.Requested(question));

    [Fact]
    public void Old_no_data_memory_is_recognised()
    {
        Assert.True(AiMemoryStore.IsNoData("""{"agent":"sales","status":"no_data","summary":"Данных нет."}"""));
        Assert.False(AiMemoryStore.IsNoData("""{"agent":"sales","status":"completed","summary":"Продажи — 12 т."}"""));
        Assert.False(AiMemoryStore.IsNoData(null));
        Assert.False(AiMemoryStore.IsNoData("не JSON"));
    }
}
