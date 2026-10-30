using System.Text.Json;
using OneBase.AI.Agents;
using OneBase.AI.Consultant;
using OneBase.AI.Grounding;
using OneBase.AI.Orchestration;
using OneBase.AI.Tools;
using OneBase.AI.Tools.Data;

namespace OneBase.AI.Tests;

/// <summary>
/// P0.2: правила AI без противоречий. Три набора — AI-сотрудники (A), главный консультант (B), проверка достоверности (C); ни в одном нет
/// универсального «данных нет»; честный отказ остаётся для not_found; консультант читает состояние отдела из outcome, в том числе если
/// администратор задал свой промпт; восемь состояний — на русском и узбекском (латиница и кириллица); непривязанная таблица — «источник не
/// подключён»; бизнес-инструкции отделов не тронуты.
/// </summary>
public class ConsultantPromptConsistencyTests
{
    /// <summary>Универсальные формулировки, которые заставляли сообщать «данных нет» независимо от причины.</summary>
    private static readonly string[] UniversalNoData =
    [
        "Если данных нет, прямо скажи",
        "В OneBase нет достаточных данных для точного ответа",
        "Нет данных — «В доступных данных OneBase не найдено подтверждение",
        "значит, данных для ответа нет",
        "Если отдел сообщил, что данных нет, так и скажи",
        "Если сотрудник вернул no_data или ошибку",
        "напиши, что данных нет",
    ];

    private static AgentConfig Config(AgentDefault d, string? prompt = null) =>
        new(d.Code, d.Name, d.Role, d.Description, prompt ?? d.Prompt, prompt is null, null, null, null, true, d.SortOrder, d.DepartmentCode, d.RequiredPermission, d.Sources);

    private static IReadOnlyList<ITool> SomeTools => [new SearchKnowledgeTool(null!)];

    private static string AgentPrompt(AgentDefault d) => AgentRunner.SystemPrompt(Config(d), SomeTools, null);

    public static TheoryData<string> Departments => [.. AgentDefaults.Departments.Select(d => d.Code)];

    [Fact]
    public void No_rule_set_forces_no_data_regardless_of_the_cause()
    {
        string[] texts =
        [
            ConsultantPrompts.AgentRules, ConsultantPrompts.GroundingPolicy, ConsultantPrompts.ConsultantRules, AgentDefaults.Director.Prompt,
            OrchestratedConsultantEngine.ComposeRules(null), .. AgentDefaults.Departments.Select(AgentPrompt),
        ];

        foreach (var text in texts)
        {
            foreach (var phrase in UniversalNoData)
            {
                Assert.DoesNotContain(phrase, text);
            }
        }
    }

    [Fact]
    public void Honest_refusal_stays_for_a_real_not_found()
    {
        Assert.Contains("- not_found — данные не найдены после проверки доступных источников.", ConsultantPrompts.ConsultantRules);
        Assert.Contains("это честный отказ", ConsultantPrompts.ConsultantRules);
        Assert.Contains("status no_data — только после поиска инструментами", ConsultantPrompts.AgentRules);
        Assert.Contains("записи нет — «не найдено»", ConsultantPrompts.AgentRules);
    }

    [Fact]
    public void Agent_and_consultant_instructions_are_separate()
    {
        var agent = AgentPrompt(AgentDefaults.Departments[0]);
        var consultant = OrchestratedConsultantEngine.ComposeRules(null);

        // A — у сотрудников, B — только у консультанта, C — у обоих.
        Assert.Contains(ConsultantPrompts.AgentRules, agent);
        Assert.DoesNotContain("Правила главного консультанта", agent);
        Assert.DoesNotContain("outcome", agent);
        Assert.Contains(ConsultantPrompts.ConsultantRules, consultant);
        Assert.DoesNotContain("Правила AI-сотрудника", consultant);
        Assert.DoesNotContain("find_products", consultant);
        Assert.Contains(ConsultantPrompts.GroundingPolicy, agent);
        Assert.Contains(ConsultantPrompts.GroundingPolicy, consultant);

        // A: пять требований к поиску; C: четыре — к достоверности.
        Assert.Contains("Ищи данные во всех разрешённых тебе инструментах", ConsultantPrompts.AgentRules);
        Assert.Contains("Не утверждай, что данных нет, без проверки", ConsultantPrompts.AgentRules);
        Assert.Contains("Отделяй техническую ошибку от отсутствия информации", ConsultantPrompts.AgentRules);
        Assert.Contains("Указывай ограничения поиска", ConsultantPrompts.AgentRules);
        Assert.Contains("Используй только подтверждённые факты", ConsultantPrompts.GroundingPolicy);
        Assert.Contains("Не придумывай цифры", ConsultantPrompts.GroundingPolicy);
        Assert.Contains("Сохраняй источники", ConsultantPrompts.GroundingPolicy);
        Assert.Contains("Неподтверждённое обозначай отдельно", ConsultantPrompts.GroundingPolicy);

        // B: источник истины о состоянии, без переопределения по тексту, объединение и неопределённость.
        Assert.Contains("поле outcome его итога: это источник истины", ConsultantPrompts.ConsultantRules);
        Assert.Contains("Не переопределяй его по тексту сотрудника", ConsultantPrompts.ConsultantRules);
        Assert.Contains("Объединяй подтверждённые результаты отделов", ConsultantPrompts.ConsultantRules);
        Assert.Contains("не сглаживай неопределённость", ConsultantPrompts.ConsultantRules);
    }

    [Fact]
    public void Consultant_rules_win_over_a_director_prompt_set_by_the_administrator()
    {
        const string custom = "Ты — консультант. Если отдел сообщил, что данных нет, так и скажи. Отвечай коротко.";

        var rules = OrchestratedConsultantEngine.ComposeRules(Config(AgentDefaults.Director, custom));

        // Свой текст администратора сохраняется, но правила консультанта идут после него и прямо важнее.
        Assert.StartsWith(custom, rules);
        Assert.True(rules.IndexOf(ConsultantPrompts.ConsultantRules, StringComparison.Ordinal) > rules.IndexOf(custom, StringComparison.Ordinal));
        Assert.Contains("важнее инструкций выше, в том числе заданных администратором", rules);
    }

    [Fact]
    public void Stored_copy_of_the_old_default_director_prompt_is_replaced_custom_text_is_kept()
    {
        var oldDefault = AgentDefaults.OutdatedPrompts[AgentDefaults.Consultant][0];

        Assert.True(AgentDefaults.IsOutdatedPrompt(AgentDefaults.Consultant, oldDefault));
        Assert.True(AgentDefaults.IsOutdatedPrompt(AgentDefaults.Consultant, "  " + oldDefault.Replace("\n", " \n ") + "  "));
        Assert.False(AgentDefaults.IsOutdatedPrompt(AgentDefaults.Consultant, oldDefault + " Отвечай коротко."));
        Assert.False(AgentDefaults.IsOutdatedPrompt(AgentDefaults.Consultant, AgentDefaults.Director.Prompt));
        Assert.False(AgentDefaults.IsOutdatedPrompt("sales", oldDefault));
        Assert.Contains("не своди их к «данных нет»", AgentDefaults.Director.Prompt);
    }

    [Fact]
    public void Eight_states_have_phrases_in_russian_uzbek_latin_and_uzbek_cyrillic()
    {
        string[] codes = ["completed", "partial", "forbidden", "source_not_loaded", "tool_error", "not_found", "out_of_range", "not_in_onebase"];
        Assert.Equal(codes, AnswerOutcome.States.Select(s => s.Code));

        foreach (var state in AnswerOutcome.States)
        {
            Assert.Equal(AnswerLanguage.Ru, AnswerLanguages.Of(state.Ru));
            Assert.Equal(AnswerLanguage.UzLatn, AnswerLanguages.Of(state.UzLatn));
            Assert.Equal(AnswerLanguage.UzCyrl, AnswerLanguages.Of(state.UzCyrl));
            Assert.Equal(state.Ru, AnswerOutcome.Phrase(state.Code, AnswerLanguage.Ru));
            Assert.Equal(state.UzLatn, AnswerOutcome.Phrase(state.Code, AnswerLanguage.UzLatn));
            Assert.Equal(state.UzCyrl, AnswerOutcome.Phrase(state.Code, AnswerLanguage.UzCyrl));
            Assert.Contains(state.Ru, ConsultantPrompts.ConsultantRules);
            Assert.Contains(state.UzLatn, ConsultantPrompts.ConsultantRules);
            Assert.Contains(state.UzCyrl, ConsultantPrompts.ConsultantRules);
        }

        // Сбой, «нет прав» и «не подключено» ни на одном языке не звучат как «данных нет».
        foreach (var code in new[] { "tool_error", "forbidden", "source_not_loaded" })
        {
            var s = AnswerOutcome.States.Single(x => x.Code == code);
            Assert.DoesNotContain("данных нет", s.Ru);
            Assert.DoesNotContain("нет данных", s.Ru);
            Assert.DoesNotContain("ma'lumot yo'q", s.UzLatn);
            Assert.DoesNotContain("маълумот йўқ", s.UzCyrl);
        }
    }

    [Theory]
    [InlineData("Какие продажи в сентябре?", AnswerLanguage.Ru)]
    [InlineData("Сколько опозданий у склада ГП?", AnswerLanguage.Ru)]
    [InlineData("Sentabr oyida Xorazm bo'yicha savdo qancha?", AnswerLanguage.UzLatn)]
    [InlineData("Bugun kim ishga kechikdi?", AnswerLanguage.UzLatn)]
    [InlineData("Хоразм бўйича савдо қанча?", AnswerLanguage.UzCyrl)]
    [InlineData("Октябр савдо режаси", AnswerLanguage.UzCyrl)]
    [InlineData("Бугун ким ишга кечикди?", AnswerLanguage.UzCyrl)]
    [InlineData("How were sales in September?", AnswerLanguage.Other)]
    [InlineData("Oʻtgan oy Toshkent boʻyicha reja qanday bajarildi?", AnswerLanguage.UzLatn)]
    public void Answer_keeps_the_language_and_script_of_the_question(string question, AnswerLanguage expected) =>
        Assert.Equal(expected, AnswerLanguages.Of(question));

    [Theory]
    [InlineData(AnswerLanguage.Ru, "Не удалось получить данные из-за технического сбоя источника")]
    [InlineData(AnswerLanguage.UzLatn, "texnik nosozlik tufayli")]
    [InlineData(AnswerLanguage.UzCyrl, "техник носозлик туфайли")]
    public void Final_answer_data_carries_the_phrase_in_the_question_language(AnswerLanguage language, string expected)
    {
        var failed = new AgentResult("hr", "HR AI", "failed", "Данных нет.", [], [], [], [], [], [], "Npgsql", Reason: "tool_error");

        using var json = JsonDocument.Parse(OrchestratedConsultantEngine.ComposeData([failed], null, language));

        Assert.Contains(expected, json.RootElement[0].GetProperty("message").GetString());
    }

    [Theory]
    [InlineData("Продажи — 12 345 кг, явка — 82,6%.", "Не подтверждено данными OneBase")]
    [InlineData("Savdo — 12 345 kg, davomat — 82,6%.", "OneBase ma'lumotlarida tasdiqlanmagan")]
    [InlineData("Савдо — 12 345 кг, давомат — 82,6%. Бу маълумотлар бўйича.", "OneBase маълумотларида тасдиқланмаган")]
    [InlineData("Sales were 12 345 kg, attendance 82,6%.", "Не подтверждено данными OneBase")]
    public void Unconfirmed_number_warning_follows_the_answer_language(string answer, string expected) =>
        Assert.Contains(expected, GroundedComposer.Warning(answer, ["82,6%"]));

    private static string TruthMap(string status, bool updated = false) => JsonSerializer.Serialize(new
    {
        found = true,
        metric = new { key = "plan_rop", label = "План РОП", owner = "google_sheets" },
        freshness = new
        {
            key = "plan_rop", label = "План РОП", source = "OneBase", status, message = "таблица не привязана — данных нет",
            updatedAt = updated ? "30.10.2026 12:00" : null,
        },
    }, DataTool.Json);

    [Fact]
    public void Unbound_table_found_by_the_source_of_truth_is_source_not_loaded_not_a_missing_search()
    {
        var issue = AgentRunner.SourceIssue(TruthMap("not_bound"));
        Assert.NotNull(issue);
        Assert.Equal((SourceOfTruthTool.ToolName, ToolFailures.NotConnected), (issue.Tool, issue.Kind));
        Assert.Contains("таблица не привязана", issue.Message);

        AgentResult NoData() => new("sales", "Sales AI", "no_data", "План не найден.", [], [], [], [], [], []);

        // Инструменты данных не вызывались, но источник истины проверен: это «источник не подключён», а не «не искали».
        var onlyTruth = AgentRunner.Classify(NoData(), 0, [issue], 0);
        Assert.Equal("source_not_connected", onlyTruth.Reason);
        Assert.Equal(AnswerOutcome.SourceNotLoaded, AnswerOutcome.Of(onlyTruth));

        // Продажи нашлись, а план — нет, потому что таблица плана не привязана: тоже «источник не подключён».
        Assert.Equal("source_not_connected", AgentRunner.Classify(NoData(), 1, [issue], 2).Reason);

        // Без поиска и без проверки источника — «не представлено в OneBase»; поиск без результата — «не найдено».
        Assert.Equal("not_in_onebase", AgentRunner.Classify(NoData(), 0, [], 0).Reason);
        Assert.Equal("not_found", AgentRunner.Classify(NoData(), 1, [], 1).Reason);
    }

    [Fact]
    public void Source_of_truth_in_order_or_without_a_metric_is_not_an_issue()
    {
        Assert.NotNull(AgentRunner.SourceIssue(TruthMap("never")));
        Assert.NotNull(AgentRunner.SourceIssue(TruthMap("error")));
        Assert.Null(AgentRunner.SourceIssue(TruthMap("error", updated: true)));
        Assert.Null(AgentRunner.SourceIssue(TruthMap("ok", updated: true)));
        Assert.Null(AgentRunner.SourceIssue("""{"rules":"…","metrics":[],"freshness":[]}"""));
        Assert.Null(AgentRunner.SourceIssue("""{"found":false,"note":"Показатель не найден"}"""));
        Assert.Null(AgentRunner.SourceIssue("не JSON"));
    }

    [Theory]
    [MemberData(nameof(Departments))]
    public void Department_business_instructions_are_unchanged_and_reach_the_agent_first(string code)
    {
        var department = AgentDefaults.Find(code)!;

        Assert.StartsWith(department.Prompt.Trim(), AgentPrompt(department));
        var key = code switch
        {
            "finance" => "Бюджетов, начальных сальдо и общего P&L компании в OneBase нет",
            "sales" => "Воронки сделок и потерянных сделок в OneBase нет",
            "marketing" => "Данных о кампаниях, лидах и рекламных расходах в OneBase сейчас нет",
            "hr" => "ФОТ, зарплат, кандидатов, оценок и обучения в OneBase нет",
            "production" => "Данных о выпуске, простоях и загрузке в OneBase нет",
            _ => "Заказов поставщикам, закупочных цен и сроков поставки нет",
        };
        Assert.Contains(key, department.Prompt);
        // Как такие инструкции сочетаются с правилом «не утверждай без проверки» — сказано в правилах сотрудника.
        Assert.Contains("Если в твоих инструкциях сказано, что каких-то данных в OneBase нет", ConsultantPrompts.AgentRules);
    }

    /// <summary>
    /// P1: выбор инструмента продаж в промпте сотрудника — get_sales_days только внутри одного календарного месяца; через границу месяца,
    /// несколько месяцев, квартал, полугодие, год и сравнение периодов — get_sales_period сразу, без пробного get_sales_days и без разбиения по
    /// месяцам. UsageHint get_sales_period остаётся и попадает в тот же промпт.
    /// </summary>
    [Fact]
    public void Agent_prompt_picks_get_sales_days_inside_a_month_and_get_sales_period_across_months()
    {
        var period = new GetSalesPeriodTool(null!, null!, TimeProvider.System);
        var days = new GetSalesDaysTool(null!, null!);
        var prompt = AgentRunner.SystemPrompt(Config(AgentDefaults.Find("sales")!), [days, period], null);

        // Названия в инструкции — настоящие инструменты, предел — как у инструмента.
        Assert.Equal(("get_sales_period", "get_sales_days"), (period.Name, days.Name));
        Assert.Contains("продажи за день, неделю или «с N по M» внутри одного календарного месяца — get_sales_days", prompt);
        Assert.Contains($"период через границу месяца, несколько месяцев, квартал, полугодие или год (до {PeriodRanges.MaxDays} дней) — get_sales_period одним вызовом", prompt);
        Assert.Contains("сравнение периодов — тоже get_sales_period: разница и % в его результате посчитаны кодом", prompt);
        Assert.Contains("Даты пересекают границу месяца — сразу get_sales_period, без пробного get_sales_days", prompt);
        Assert.Contains("квартал или несколько месяцев не разбивай на вызовы get_sales_days", prompt);
        Assert.DoesNotContain("неделю или «с N по M» — get_sales_days", prompt); // прежняя инструкция без границы месяца
        // Соседние правила того же абзаца — на месте.
        Assert.Contains("одного ТП за любой отрезок дней месяца — get_visits", prompt);
        Assert.Contains("Не перечисляй пользователю, каких данных «нет»", prompt);

        // UsageHint не убран и попадает в промпт сотрудника.
        Assert.False(string.IsNullOrWhiteSpace(period.UsageHint));
        Assert.Contains(period.UsageHint!, prompt);

        // Инструкция — у каждого сотрудника отдела с инструментами; без инструментов абзаца нет.
        Assert.All(AgentDefaults.Departments, d => Assert.Contains("сразу get_sales_period, без пробного get_sales_days", AgentPrompt(d)));
        Assert.DoesNotContain("get_sales_period", AgentRunner.SystemPrompt(Config(AgentDefaults.Find("sales")!), [], null));
    }
}
