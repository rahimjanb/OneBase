using System.Text.Json;
using OneBase.AI.Agents;
using OneBase.AI.Consultant;
using OneBase.AI.Gateway;
using OneBase.AI.Grounding;
using OneBase.AI.Llm;
using OneBase.AI.Orchestration;
using OneBase.AI.Tools;

namespace OneBase.AI.Tests;

/// <summary>
/// P0.1: итоговый ответ консультанта по итогам AI-сотрудников. Состояние каждого итога выводится кодом (AnswerOutcome) и не смешивается:
/// сбой — не «не найдено», «нет прав» — не «не найдено», частичный итог сохраняет факты, честный отказ после проверки допустим, итоги разных
/// отделов объединяются без потерь, а число без подтверждения в ответ не попадает как факт. Без модели и без базы.
/// </summary>
public class ConsultantAnswerTests
{
    private static AgentResult Result(string agent, string status, string summary, string? reason = null, string? error = null,
        IReadOnlyList<string>? findings = null, IReadOnlyList<ToolIssue>? toolErrors = null) =>
        new(agent, agent.ToUpperInvariant() + " AI", status, summary, findings ?? [], findings is null ? [] : [new Metric("Факт", "12 345", "кг")], [], [], [],
            [], error, Reason: reason, ToolErrors: toolErrors);

    private static JsonElement Data(params AgentResult[] results) =>
        JsonDocument.Parse(OrchestratedConsultantEngine.ComposeData(results, name => name == "get_stock_reload" ? "Остатки и дозагрузка" : null)).RootElement.Clone();

    [Theory]
    [InlineData("tool_error")]
    [InlineData("model_unavailable")]
    [InlineData("internal_error")]
    [InlineData("interrupted")]
    [InlineData(null)]
    public void Failed_agent_is_a_technical_failure_never_not_found(string? reason)
    {
        var failed = Result("supply", "failed", "Данных об остатках нет.", reason, "Npgsql: Host=db;Password=s3cr3t");

        Assert.Equal(AnswerOutcome.ToolError, AnswerOutcome.Of(failed));
        Assert.NotEqual(AnswerOutcome.NotFound, AnswerOutcome.Of(failed));
    }

    [Fact]
    public void Tool_error_reaches_the_final_answer_as_a_failure_not_as_missing_data()
    {
        var failed = AgentRunner.Classify(Result("supply", "no_data", "Данных об остатках нет."), 0,
            [new ToolIssue("get_stock_reload", ToolFailures.ToolError, "Инструмент завершился внутренней ошибкой OneBase — его данные не получены.")], 1);
        var supply = Data(failed)[0];
        var json = supply.GetRawText();

        Assert.Equal("tool_error", supply.GetProperty("outcome").GetString());
        // Слова модели сотрудника «данных нет» итог не получает — только пояснение о сбое.
        Assert.Equal(JsonValueKind.Null, supply.GetProperty("summary").ValueKind);
        Assert.DoesNotContain("данных нет", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("нет данных", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Технический сбой", supply.GetProperty("note").GetString());
        Assert.Equal("Остатки и дозагрузка: технический сбой", supply.GetProperty("limitations")[0].GetString());
        // Ни кода инструмента, ни текста исключения.
        Assert.DoesNotContain("get_stock_reload", json);
        Assert.DoesNotContain("внутренней ошибкой OneBase", json);
    }

    [Fact]
    public void Exception_text_of_a_failed_agent_never_reaches_the_final_answer()
    {
        var failed = Result("supply", "failed", "Модель anthropic не ответила за 180 с: https://api.example/v1", "model_unavailable",
            "Npgsql: Host=db;Username=onebase;Password=s3cr3t");
        var json = Data(failed)[0].GetRawText();

        Assert.DoesNotContain("s3cr3t", json);
        Assert.DoesNotContain("Npgsql", json);
        Assert.DoesNotContain("https://", json);
    }

    [Theory]
    [InlineData("forbidden", null)]
    [InlineData("forbidden", "no_access")]
    [InlineData("no_data", "no_access")]
    public void No_access_is_forbidden_never_not_found(string status, string? reason)
    {
        var result = Result("finance", status, "Нет доступа.", reason, "Нет доступа: у пользователя нет прав на данные «P&L дилеров».");

        Assert.Equal(AnswerOutcome.Forbidden, AnswerOutcome.Of(result));
        var data = Data(result)[0];
        Assert.Equal("forbidden", data.GetProperty("outcome").GetString());
        Assert.Contains("нет прав", data.GetProperty("note").GetString());
    }

    [Fact]
    public void Missing_source_out_of_range_and_not_searched_are_their_own_states()
    {
        var notLoaded = Result("hr", "no_data", "—", "source_not_connected", "Источник не подключён: в базе знаний нет документов, доступных пользователю.");
        var range = AgentRunner.Classify(Result("hr", "no_data", "Данных нет."), 0,
            [new ToolIssue("get_hr_timesheet", ToolFailures.OutOfRange, "Период — не больше 62 дней: разбейте запрос на части.")], 1);
        var notSearched = AgentRunner.Classify(Result("marketing", "no_data", "Данных о кампаниях в OneBase нет."), 0, [], 0);

        Assert.Equal(AnswerOutcome.SourceNotLoaded, AnswerOutcome.Of(notLoaded));
        Assert.Equal(AnswerOutcome.OutOfRange, AnswerOutcome.Of(range));
        Assert.Contains("62 дней", AnswerOutcome.Note(range, AnswerOutcome.OutOfRange));
        Assert.Equal(AnswerOutcome.NotInOneBase, AnswerOutcome.Of(notSearched));
        Assert.Contains("поиск по ней не выполнялся", AnswerOutcome.Note(notSearched, AnswerOutcome.NotInOneBase));
    }

    [Fact]
    public void Partial_keeps_its_confirmed_facts_and_says_it_is_incomplete()
    {
        var partial = Result("sales", "partial", "Анализ прерван: модель не ответила. Ниже — данные, которые инструменты успели вернуть.", "interrupted",
            "Анализ прерван: модель не ответила", ["Продажи по регионам: факт 12 345 кг"],
            [new ToolIssue("get_stock_reload", ToolFailures.ToolError, "сбой")]);
        var data = Data(partial)[0];

        Assert.Equal("partial", data.GetProperty("outcome").GetString());
        Assert.Equal("Продажи по регионам: факт 12 345 кг", data.GetProperty("findings")[0].GetString());
        Assert.Equal("12 345", data.GetProperty("metrics")[0].GetProperty("value").GetString());
        Assert.Contains("прерван", data.GetProperty("note").GetString());
        Assert.Equal("Остатки и дозагрузка: технический сбой", data.GetProperty("limitations")[0].GetString());
    }

    [Fact]
    public void Honest_not_found_after_a_real_search_is_allowed()
    {
        // Сотрудник искал (инструмент вернул данные), но нужного в них нет — честное «не найдено после проверки».
        var searched = AgentRunner.Classify(Result("sales", "no_data", "Клиентов, не купивших в октябре, не найдено."), 1, [], 1);
        var data = Data(searched)[0];

        Assert.Equal(("no_data", "not_found"), (searched.Status, searched.Reason));
        Assert.Equal("not_found", data.GetProperty("outcome").GetString());
        Assert.Equal("Клиентов, не купивших в октябре, не найдено.", data.GetProperty("summary").GetString());
        Assert.Contains("после проверки", data.GetProperty("note").GetString());

        // completed с «ничего нет» по данным — тоже ответ по данным, отказ честный.
        var empty = Result("sales", "completed", "Неоплаченных заказов за октябрь нет.");
        Assert.Equal("completed", Data(empty)[0].GetProperty("outcome").GetString());
        Assert.Equal("Неоплаченных заказов за октябрь нет.", Data(empty)[0].GetProperty("summary").GetString());
    }

    [Fact]
    public void Agents_with_different_states_are_combined_without_mixing()
    {
        var data = Data(
            Result("sales", "completed", "Продажи за сентябрь — 12 345 кг.", findings: ["Продажи за сентябрь — 12 345 кг"]),
            Result("hr", "failed", "Данных о явке нет.", "tool_error", "Инструменты завершились ошибкой (get_hr_day)"),
            Result("finance", "forbidden", "Нет доступа.", "no_access", "Нет доступа: у пользователя нет прав на данные этого отдела."),
            Result("supply", "no_data", "Остатков по запросу не найдено.", "not_found"),
            Result("marketing", "no_data", "Кампаний в OneBase нет.", "not_in_onebase"),
            Result("production", "no_data", "Данных нет.", "out_of_range", "Отрезок — в пределах одного месяца (октябрь 2026): для нескольких месяцев вызовите по каждому."));

        Assert.Equal(["completed", "tool_error", "forbidden", "not_found", "not_in_onebase", "out_of_range"],
            data.EnumerateArray().Select(a => a.GetProperty("outcome").GetString()));
        // Факты — только у найденных данных; у остальных — пояснение, без чужих фактов и без слов модели «данных нет».
        Assert.Equal("Продажи за сентябрь — 12 345 кг", data[0].GetProperty("findings")[0].GetString());
        Assert.All(data.EnumerateArray().Skip(1), a => Assert.Equal(0, a.GetProperty("findings").GetArrayLength()));
        Assert.Equal(JsonValueKind.Null, data[1].GetProperty("summary").ValueKind);
        Assert.Equal(JsonValueKind.Null, data[2].GetProperty("summary").ValueKind);
        Assert.Contains("одного месяца", data[5].GetProperty("note").GetString());
        Assert.DoesNotContain("get_hr_day", data.GetRawText());
    }

    [Fact]
    public void Final_answer_rules_map_every_state_and_forbid_no_data_for_failures()
    {
        var rules = OrchestratedConsultantEngine.ComposeRules(null);

        foreach (var outcome in new[] { "completed", "partial", "forbidden", "source_not_loaded", "tool_error", "not_found", "out_of_range", "not_in_onebase" })
        {
            Assert.Contains($"- {outcome} — ", rules);
        }

        Assert.Contains("Нельзя говорить «данных нет»", rules);
        Assert.Contains("если outcome — tool_error, forbidden или source_not_loaded", rules);
        Assert.Contains("если её не искали", rules);
        Assert.Contains("Не скрывай частичные результаты", rules);
        Assert.Contains("Не придумывай недостающие цифры", rules);
        Assert.Contains("ответь по подтверждённым результатам остальных", rules);
        Assert.Contains("Не раскрывай внутренние ошибки, исключения, SQL", rules);
        // Правила итогового ответа — только у консультанта: в правилах AI-сотрудников их нет.
        Assert.DoesNotContain("outcome", ConsultantPrompts.AgentRules);
    }

    [Fact]
    public async Task Final_answer_drops_a_number_that_no_agent_confirmed()
    {
        // Продажи ответили (12 345 кг), HR — сбой: явки 82,6% в данных нет. Первый ответ её называет — после проверки число убрано.
        var evidence = new EvidenceSet();
        evidence.AddContent("""{"factKg":12345,"region":"Хоразм"}""");
        var model = new ScriptedConsultantModel()
            .Then("Продажи за сентябрь — 12 345 кг, явка — 82,6%.")
            .Then("Продажи за сентябрь — 12 345 кг. По HR — технический сбой источника данных, явку получить не удалось.");

        var answer = await new GroundedComposer(model).ComposeAsync([new LlmMessage(LlmRole.User, "Как продажи и явка?")], new AiCallOptions(), evidence,
            "Как продажи и явка?", CancellationToken.None);

        Assert.DoesNotContain("82,6", answer.Text);
        Assert.Contains("12 345", answer.Text);
        Assert.True(answer.Grounding.Revised);
        Assert.False(answer.Grounding.Warned);
        Assert.Contains("82,6", model.Calls[1].Last().Content);
    }

    [Fact]
    public async Task Unconfirmed_number_that_survives_the_revision_is_marked_not_a_fact()
    {
        var evidence = new EvidenceSet();
        evidence.AddContent("""{"factKg":12345}""");
        var model = new ScriptedConsultantModel()
            .Then("Продажи — 12 345 кг, явка — 82,6%.")
            .Then("Продажи — 12 345 кг, явка — 82,6%.");

        var answer = await new GroundedComposer(model).ComposeAsync([new LlmMessage(LlmRole.User, "Как продажи и явка?")], new AiCallOptions(), evidence,
            "Как продажи и явка?", CancellationToken.None);

        Assert.True(answer.Grounding.Warned);
        Assert.Contains("82,6%", answer.Grounding.Unsupported);
        Assert.Contains("Не подтверждено данными OneBase", answer.Text);
    }

    [Theory]
    [InlineData("Период — не больше 62 дней: разбейте запрос на части.", true)]
    [InlineData("Отрезок — в пределах одного месяца (октябрь 2026): для нескольких месяцев вызовите по каждому.", true)]
    [InlineData("Данных о продажах за июнь 2025 в OneBase нет. Есть: октябрь 2026, сентябрь 2026.", true)]
    [InlineData("Отрезок — не длиннее 31 дней: сузьте from/to.", true)]
    [InlineData("Региона «Хива» нет. Регионы: Хоразм, Бухоро.", false)]
    [InlineData("Аргумент month — от 1 до 12.", false)]
    public void Period_limits_are_recognised_as_out_of_range(string message, bool expected) =>
        Assert.Equal(expected, ToolFailures.IsOutOfRange(message));
}
