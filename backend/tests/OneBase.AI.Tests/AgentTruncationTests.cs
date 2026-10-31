using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Agents;
using OneBase.AI.Consultant;
using OneBase.AI.Llm;
using OneBase.AI.Providers;
using OneBase.AI.Tools.Data;

namespace OneBase.AI.Tests;

/// <summary>
/// Итог AI-сотрудника, обрезанный лимитом длины ответа (stop_reason max_tokens): JSON недописан. Раньше такой итог не разбирался — сотрудник
/// выходил «частично, без выводов», а то, что не успел дописать (визиты за сентябрь), консультант называл «данных нет».
/// </summary>
public class AgentTruncationTests
{
    private static readonly AgentConfig Sales = new("sales", "Sales AI", "роль", "описание", "prompt", true, null, null, null, true, 2, "sales", null, []);

    private const string Cut =
        """{"agent":"sales","status":"completed","summary":"Выручка октября 13 488 246 629 сум, +0,5% к сентябрю.","findings":["Выручка выросла на 72 693 768 сум","Заказов меньше на 10,2%","Визиты в сентяб""";

    [Fact]
    public void Truncated_json_keeps_whole_fields_and_drops_the_unfinished_tail()
    {
        var closed = JsonText.CloseTruncated(Cut);

        Assert.NotNull(closed);
        using var doc = JsonDocument.Parse(closed!);
        Assert.Equal("Выручка октября 13 488 246 629 сум, +0,5% к сентябрю.", doc.RootElement.GetProperty("summary").GetString());
        Assert.Equal(["Выручка выросла на 72 693 768 сум", "Заказов меньше на 10,2%"],
            doc.RootElement.GetProperty("findings").EnumerateArray().Select(e => e.GetString()));
    }

    [Theory]
    [InlineData("""{"summary":"А","metrics":[{"name":"Средний чек","value":"454""", """{"summary":"А","metrics":[{"name":"Средний чек"}]}""")]
    [InlineData("""{"summary":"А","findings":["x"],"problems""", """{"summary":"А","findings":["x"]}""")]
    [InlineData("""{"summary":"А","count":12""", """{"summary":"А"}""")]
    [InlineData("""{"summary":"А","count":12,""", """{"summary":"А","count":12}""")]
    [InlineData("""{"summary":"Кавычка \" внутри","findings":["a\\""", """{"summary":"Кавычка \" внутри","findings":[]}""")]
    public void Truncated_json_is_closed_at_the_last_whole_value(string cut, string expected) =>
        Assert.Equal(expected, JsonText.CloseTruncated(cut));

    [Fact]
    public void Whole_json_and_plain_text_are_not_changed()
    {
        Assert.Equal("""{"a":1}""", JsonText.CloseTruncated("""Ответ: {"a":1} — готово"""));
        Assert.Null(JsonText.CloseTruncated("Просто текст без JSON"));
    }

    [Fact]
    public void Truncated_agent_result_is_partial_with_its_findings()
    {
        var result = AgentRunner.Parse(Sales, Cut, ["get_sales_period"], []);

        Assert.Equal(("partial", "truncated"), (result.Status, result.Reason));
        Assert.Equal(2, result.Findings.Count);
        Assert.StartsWith("Выручка октября", result.Summary);
        Assert.Equal("Итог сотрудника не уместился в лимит длины ответа — ниже только то, что он успел написать.",
            AnswerOutcome.Note(result, AnswerOutcome.Of(result)));
        Assert.True(AgentRunner.Truncated("max_tokens"));
        Assert.True(AgentRunner.Truncated("length"));
        Assert.False(AgentRunner.Truncated("end_turn"));
    }

    [Fact]
    public void Model_partial_without_real_gaps_is_completed()
    {
        AgentResult R(string status, string? reason = null, IReadOnlyList<string>? unverified = null) =>
            new("sales", "Sales AI", status, "Октябрь — по 30.10.", ["Выручка 13,5 млрд сум"], [], [], [], [], [], Reason: reason, Unverified: unverified);
        ToolIssue[] none = [];

        // «Данных за 31.10 ещё нет» — идущий месяц, а не неполный ответ.
        Assert.Equal("completed", AgentRunner.Settle(R("partial"), none).Status);
        // Настоящие пробелы — partial остаётся.
        Assert.Equal("partial", AgentRunner.Settle(R("partial", unverified: ["345,6 млн"]), none).Status);
        Assert.Equal("partial", AgentRunner.Settle(R("partial", "truncated"), none).Status);
        Assert.Equal("partial", AgentRunner.Settle(R("partial", "unparsed"), none).Status);
        Assert.Equal("partial", AgentRunner.Settle(R("partial"), [new ToolIssue("get_visits", OneBase.AI.Tools.ToolFailures.ToolError, "сбой")]).Status);
        Assert.Equal("no_data", AgentRunner.Settle(R("no_data"), none).Status);
        // Текст вместо JSON — partial с причиной unparsed.
        Assert.Equal(("partial", "unparsed"), (AgentRunner.Parse(Sales, "Продажи выросли.", [], []).Status, AgentRunner.Parse(Sales, "Продажи выросли.", [], []).Reason));
    }

    [Fact]
    public void Partial_rule_says_not_received_instead_of_no_data()
    {
        var rule = AnswerOutcome.States.Single(s => s.Code == AnswerOutcome.Partial).Rule;
        Assert.Contains("«не получено», а не «данных нет»", rule);
        Assert.Contains("«не получено», а не «данных нет»", ConsultantPrompts.ConsultantRules);
    }

    [Fact]
    public void Sales_period_has_the_average_check_counted_by_code()
    {
        Assert.Equal(454_032m, GetSalesPeriodTool.AvgCheck(new SalesFigures(29_545, 13_414_376_000m, 365_042.6m, 0)));
        Assert.Null(GetSalesPeriodTool.AvgCheck(new SalesFigures(0, 0, 0, 0)));
    }
}

/// <summary>
/// Сотрудник на настоящем PostgreSQL со сценарной моделью (без сети и платных вызовов): обрезанный итог — повтор короче с запасом по длине;
/// обрезан и повтор — partial с тем, что успел написать; сбой модели после данных — итог по уже полученным данным, а не сырые куски
/// результатов; лимит длины ответа сотрудника — не меньше 8192.
/// </summary>
public class AgentTruncationDbTests(ConsultantDb fixture) : IClassFixture<ConsultantDb>
{
    private const string FinalJson =
        """{"agent":"sales","status":"completed","summary":"Факт продаж — 12 345 кг.","findings":["Факт продаж — 12 345 кг"],"metrics":[],"problems":[],"recommendations":[],"data_sources":[]}""";

    private const string CutJson = """{"agent":"sales","status":"completed","summary":"Факт продаж — 12 345 кг.","findings":["Факт продаж — 12 345 кг","Визиты в сентяб""";

    private static LlmToolCall Call(string id, string name) => new(id, name, JsonDocument.Parse("{}").RootElement.Clone());

    private async Task<AgentRun> RunAsync()
    {
        using var scope = fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AgentRunner>().RunAsync(new AgentTask("sales", "Сравни продажи октября с сентябрём", fixture.AdminId), null,
            CancellationToken.None);
    }

    [PostgresFact]
    public async Task Truncated_result_is_asked_again_shorter_with_a_bigger_limit()
    {
        var model = fixture.Model.Reset();
        model.Then([Call("1", ConsultantDb.Echo)]).ThenTruncated(CutJson).Then(FinalJson);

        var run = await RunAsync();

        Assert.Equal("completed", run.Result.Status);
        Assert.Null(run.Result.Reason);
        Assert.Equal(3, model.Calls.Count);
        // Лимит сотрудника — не меньше 8192 (общий лимит новой установки — 4096); повтор — вдвое больше (до потолка) и без инструментов.
        var first = model.Options[0].MaxOutputTokens!.Value;
        Assert.True(first >= AgentRunner.AgentOutputTokens);
        Assert.Equal(Math.Min(first * 2, AgentRunner.MaxAgentOutputTokens), model.Options[2].MaxOutputTokens);
        Assert.True(model.Options[2].MaxOutputTokens > first);
        Assert.False(model.Options[2].AllowToolCalls);
        Assert.Contains("обрезан", model.Calls[2][^1].Content);
    }

    [PostgresFact]
    public async Task Truncated_twice_keeps_what_was_written_as_partial()
    {
        var model = fixture.Model.Reset();
        model.Then([Call("1", ConsultantDb.Echo)]).ThenTruncated(CutJson).ThenTruncated(CutJson);

        var run = await RunAsync();

        Assert.Equal(("partial", "truncated"), (run.Result.Status, run.Result.Reason));
        Assert.Equal(["Факт продаж — 12 345 кг"], run.Result.Findings);
        Assert.Null(run.Result.Unverified);
    }

    [PostgresFact]
    public async Task Late_result_with_unconfirmed_numbers_gets_steps_to_fix_them()
    {
        var model = fixture.Model.Reset();
        // Семь шагов данных, на восьмом (последнем, без инструментов) — итог с числом, которого нет в данных.
        for (var i = 0; i < 7; i++)
        {
            model.Then([Call($"c{i}", ConsultantDb.Echo)]);
        }

        model.Then("""{"agent":"sales","status":"completed","summary":"Факт продаж — 12 345 кг, рост на 345,6 млн сум.","findings":[]}""")
            .Then(FinalJson);

        var run = await RunAsync();

        Assert.Equal("completed", run.Result.Status);
        Assert.Null(run.Result.Unverified);
        Assert.Equal(9, model.Calls.Count);
        Assert.False(model.Options[7].AllowToolCalls);
        // После проверки — ещё шаг с инструментами (расчёт), а не потеря чисел.
        Assert.True(model.Options[8].AllowToolCalls);
        Assert.Contains("не найдены в результатах инструментов", model.Calls[8][^1].Content);
    }

    [PostgresFact]
    public async Task Result_as_plain_text_is_asked_once_as_json()
    {
        var model = fixture.Model.Reset();
        model.Then([Call("1", ConsultantDb.Echo)]).Then("По продажам: факт 12 345 кг, рост к сентябрю.").Then(FinalJson);

        var run = await RunAsync();

        Assert.Equal("completed", run.Result.Status);
        Assert.Equal(["Факт продаж — 12 345 кг"], run.Result.Findings);
        Assert.Contains("JSON", model.Calls[2][^1].Content);
        Assert.False(model.Options[2].AllowToolCalls);
    }

    [PostgresFact]
    public async Task Model_failure_after_data_gets_one_summary_call_instead_of_raw_pieces()
    {
        var model = fixture.Model.Reset();
        model.Then([Call("1", ConsultantDb.Echo)]).Fail(new AiProviderException("anthropic", 529, "Модель перегружена, попробуйте позже.", true)).Then(FinalJson);

        var run = await RunAsync();

        Assert.Equal("completed", run.Result.Status);
        Assert.Null(run.Result.Reason);
        Assert.Equal(["Факт продаж — 12 345 кг"], run.Result.Findings);
        // Итог — без инструментов и без повтора внутри шлюза.
        Assert.False(model.Options[2].AllowToolCalls);
        Assert.False(model.Options[2].RetrySameModel);
        Assert.Contains("не получено", model.Calls[2][^1].Content);
    }
}
