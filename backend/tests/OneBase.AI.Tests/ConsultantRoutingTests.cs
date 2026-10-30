using OneBase.AI.Agents;
using OneBase.AI.Consultant;
using OneBase.AI.Grounding;
using OneBase.AI.Knowledge;
using OneBase.AI.Llm;
using OneBase.AI.Orchestration;
using OneBase.AI.Providers;

namespace OneBase.AI.Tests;

/// <summary>
/// Страховка маршрута консультанта (P0 аудита ONEBASE_AI_CONSULTANT_AUDIT.md): маршрутизатор вернул пустой список сотрудников, а вопрос — о
/// данных. Без сотрудников остаётся только разговор; вопрос о данных всегда получает сотрудника — по словам (русский и узбекский), словарю
/// продаж, источникам инструментов и истории чата; недоступная пользователю область — «нет доступа», а не ответ другим отделом.
/// </summary>
public class ConsultantRoutingTests
{
    private static AgentConfig Agent(AgentDefault d) =>
        new(d.Code, d.Name, d.Role, d.Description, d.Prompt, true, null, null, null, true, d.SortOrder, d.DepartmentCode, d.RequiredPermission, d.Sources);

    /// <summary>Шесть сотрудников, как в OneBase, — по порядку сортировки.</summary>
    private static readonly AgentConfig[] All = AgentDefaults.Departments.OrderBy(d => d.SortOrder).Select(Agent).ToArray();

    /// <summary>Слова словаря продаж (категории, товары, регионы), как их отдаёт SalesGlossary.KeywordsAsync.</summary>
    private static readonly string[] Glossary = ["помадка", "кекс", "кондитерка", "самарканд", "хоразм", "хорезм", "ташкент", "бухоро"];

    private static readonly RoutingDecision Empty = new([], new Dictionary<string, string>(), "", false, AiTokenUsage.None);

    private static ConsultantTurn Turn(string question, string? previousQuestion = null, string? previousAnswer = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), question,
            previousQuestion is null ? [] : [new LlmMessage(LlmRole.User, previousQuestion), new LlmMessage(LlmRole.Assistant, previousAnswer ?? "Ответ консультанта.")]);

    private static RoutingDecision Route(ConsultantTurn turn, IReadOnlyList<AgentConfig>? available = null,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? sources = null, IReadOnlyList<AgentConfig>? unavailable = null) =>
        OrchestratedConsultantEngine.Enforce(Empty, turn, CorrectionGuard.Detect(turn.Question, turn.History), available ?? All, Glossary, sources, unavailable);

    /// <summary>Сценарии S01–S28 аудита: вопрос, прошлый вопрос и ответ (для уточнений и возражений) и сотрудник, который обязан попасть в маршрут.</summary>
    public static TheoryData<string, string, string?, string?, string> Scenarios => new()
    {
        { "S01", "Какие продажи в сентябре?", null, null, "sales" },
        { "S02", "Почему упали продажи кондитерки в Хорезме и какие ТП дали снижение", null, null, "sales" },
        { "S03", "Сравни продажи за август и сентябрь по регионам", null, null, "sales" },
        { "S04", "Что происходит в компании и что улучшить", null, null, "finance" },
        { "S05", "План РОП по Самарканду на октябрь", null, null, "sales" },
        { "S06", "Какая маржа у дилера Хоразм в сентябре", null, null, "sales" },
        { "S07", "Что написано в регламенте о командировках", null, null, "hr" },
        { "S08", "Сколько стоит доставка по договору с перевозчиком", null, null, "supply" },
        { "S09", "Топ-5 SKU категории Кекс в Ташкенте за сентябрь с ростом больше 10%", null, null, "sales" },
        { "S10", "Где мы теряем деньги", null, null, "finance" },
        { "S11", "Сколько мы потратили на рекламу в октябре", null, null, "marketing" },
        { "S12", "Связаны ли опоздания склада с недогрузом дилеров", null, null, "hr" },
        { "S13", "На сколько процентов выросла выручка октября к сентябрю", null, null, "sales" },
        { "S14", "Как дела у Алишера", null, null, "sales" },
        { "S15", "Выполнение плана Хорезм сентябрь", null, null, "sales" },
        { "S16", "Sentabr oyida Xorazm bo'yicha savdo rejasi bajarilishi", null, null, "sales" },
        { "S17", "Сколько просроченных задач у отдела маркетинга", null, null, "marketing" },
        { "S18", "Статус проекта запуска новой линии", null, null, "hr" },
        { "S19", "Какие автоотчёты ушли в Telegram вчера", null, null, "sales" },
        { "S20", "Что нашёл AI сегодня", null, null, "sales" },
        { "S21", "Сколько опозданий было у склада ГП за квартал", null, null, "hr" },
        { "S22", "Кто из дилеров должен больше всех", null, null, "finance" },
        { "S23", "Хватит ли помадки на складе Самарканда до конца месяца", null, null, "supply" },
        { "S24", "А по Самарканду?", "Какие продажи кондитерки в сентябре?", "Продажи кондитерки в сентябре — 120 т при плане 150 т.", "sales" },
        { "S25", "Ты ошибся, там было больше", "Сколько отгрузили Хорезму в сентябре?", "В сентябре Хорезму отгружено 84 т.", "sales" },
        { "S26", "Цена Кекс шоколадный 300 г в прайсе", null, null, "sales" },
        { "S27", "Визиты без заказа у ТП Каримов на этой неделе", null, null, "sales" },
        { "S28", "Помадка Самарканд октябрь", null, null, "sales" },
    };

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Every_audit_scenario_about_data_gets_a_route_when_the_router_returns_none(string id, string question, string? previousQuestion,
        string? previousAnswer, string expected)
    {
        var decision = Route(Turn(question, previousQuestion, previousAnswer));

        Assert.True(decision.Agents.Count > 0, $"{id}: «{question}» остался без AI-сотрудников");
        Assert.Contains(expected, decision.Agents);
        Assert.Null(decision.Denied);
    }

    [Fact]
    public void All_28_audit_scenarios_reach_the_data()
    {
        var routed = Scenarios.Count(row => Route(Turn((string)row[1], (string?)row[2], (string?)row[3])).Agents.Count > 0);

        Assert.Equal(28, routed);
    }

    [Fact]
    public void Questions_without_a_department_go_to_one_agent_not_to_everyone()
    {
        Assert.Equal(["sales"], Route(Turn("Как дела у Алишера")).Agents);
        Assert.Equal(["hr"], Route(Turn("Статус проекта запуска новой линии")).Agents);
        Assert.Equal(["sales"], Route(Turn("Что нашёл AI сегодня")).Agents);
    }

    [Fact]
    public void Question_about_the_whole_company_goes_to_everyone()
    {
        Assert.Equal(All.Select(a => a.Code), Route(Turn("Что происходит в компании и что улучшить")).Agents);
    }

    [Theory]
    [InlineData("Привет!")]
    [InlineData("Привет! Что ты умеешь?")]
    [InlineData("Добрый день")]
    [InlineData("Спасибо, всё понятно")]
    [InlineData("Спасибо, план понятен")]
    [InlineData("Большое спасибо!")]
    [InlineData("Пока")]
    [InlineData("До свидания")]
    [InlineData("Что ты умеешь?")]
    [InlineData("Чем ты можешь помочь?")]
    [InlineData("Assalomu alaykum")]
    [InlineData("Salom!")]
    [InlineData("Rahmat, tushunarli")]
    [InlineData("Xayr")]
    [InlineData("Nima qila olasan?")]
    [InlineData("Ассалому алайкум")]
    [InlineData("Рахмат")]
    public void Small_talk_runs_no_agents(string message)
    {
        Assert.True(RouteIntent.IsSmallTalk(message), message);
        Assert.Empty(Route(Turn(message)).Agents);
        Assert.Empty(AiRouter.ByKeywords(message, All, AiTokenUsage.None).Agents);
    }

    [Theory]
    [InlineData("Привет, какие продажи сегодня?")]
    [InlineData("Спасибо. Покажи остатки по Самарканду")]
    [InlineData("Salom, Xorazm bo'yicha savdo qancha?")]
    public void Greeting_with_a_data_question_is_a_data_question(string question)
    {
        Assert.False(RouteIntent.IsSmallTalk(question));
        Assert.NotEmpty(Route(Turn(question)).Agents);
    }

    [Theory]
    [InlineData("Выполнение плана Хорезм сентябрь")]
    [InlineData("что с продажами в Ташкенте")]
    [InlineData("как идёт помадка")]
    [InlineData("найди клиентов, которые не купили")]
    [InlineData("проанализируй выручку за октябрь")]
    [InlineData("расскажи про остатки")]
    [InlineData("хватит ли помадки до конца месяца")]
    [InlineData("сравни сентябрь и октябрь")]
    [InlineData("почему упала явка")]
    [InlineData("Xorazm bo'yicha savdo qancha")]
    [InlineData("Qaysi hududda reja bajarilmadi")]
    [InlineData("Sentabr savdosini tahlil qil")]
    [InlineData("Хоразм бўйича савдо қанча")]
    [InlineData("Қайси ҳудудда режа бажарилиши паст")]
    public void Data_requests_are_recognised_without_a_question_mark(string question)
    {
        Assert.True(RouteIntent.AsksForData(question), question);
        Assert.NotEmpty(Route(Turn(question)).Agents);
    }

    [Theory]
    [InlineData("Sentabr oyida Xorazm bo'yicha savdo rejasi bajarilishi", "sales")]
    [InlineData("Bugun kim ishga kechikdi", "hr")]
    [InlineData("Omborda qoldiq qancha", "supply")]
    [InlineData("Oktyabr foyda va xarajatlar", "finance")]
    [InlineData("Бугун ким ишга кечикди", "hr")]
    [InlineData("Омборда қолдиқ қанча", "supply")]
    [InlineData("Октябр савдо режаси", "sales")]
    public void Uzbek_latin_and_cyrillic_questions_find_their_department(string question, string expected)
    {
        Assert.Contains(expected, Route(Turn(question)).Agents);
    }

    [Theory]
    [InlineData("А по Самарканду?")]
    [InlineData("А за прошлый месяц?")]
    [InlineData("Сравни с предыдущим")]
    [InlineData("А кто хуже всех?")]
    [InlineData("Samarqand-chi?")]
    public void Follow_up_inherits_the_previous_question(string question)
    {
        const string previous = "Какие продажи кондитерки в Хорезме в сентябре?";
        var decision = Route(Turn(question, previous, "Продажи кондитерки в Хорезме в сентябре — 120 т."));

        Assert.Equal(["sales"], decision.Agents);
        Assert.Contains(previous, decision.Tasks["sales"]);
        Assert.Contains(question, decision.Tasks["sales"]);
    }

    [Fact]
    public void Follow_up_of_an_hr_question_stays_with_hr()
    {
        var decision = Route(Turn("А за прошлую неделю?", "Кто опаздывал на этой неделе?", "На этой неделе опаздывали 12 сотрудников."));

        Assert.Equal(["hr"], decision.Agents);
    }

    [Fact]
    public void Router_fallback_uses_the_history_for_follow_ups()
    {
        List<LlmMessage> history = [new(LlmRole.User, "Кто сегодня опоздал?"), new(LlmRole.Assistant, "Опоздали 5 человек.")];

        Assert.Equal(["hr"], AiRouter.ByKeywords("А вчера?", All, AiTokenUsage.None, null, history).Agents);
    }

    [Fact]
    public void Department_the_user_cannot_open_is_denied_not_answered_by_another_department()
    {
        var withoutHr = All.Where(a => a.Code != "hr").ToList();
        var hr = All.Where(a => a.Code == "hr").ToList();

        var decision = Route(Turn("Кто сегодня опоздал на работу?"), withoutHr, null, hr);

        Assert.Empty(decision.Agents);
        var denied = Assert.Single(decision.Denied!);
        Assert.Equal("hr", denied.Agent);
        Assert.Null(denied.Title);
    }

    [Fact]
    public void Data_area_without_tools_for_the_user_is_denied()
    {
        // У пользователя нет права на Telegram: ни у одного сотрудника нет источника system.telegram.
        var sources = All.ToDictionary(a => a.Code, IReadOnlySet<string> (_) => new HashSet<string> { KnowledgeSources.SalesSecondary, KnowledgeSources.HrAttendance });

        var decision = Route(Turn("Какие автоотчёты ушли в Telegram вчера"), All, sources);

        Assert.Empty(decision.Agents);
        var denied = Assert.Single(decision.Denied!);
        Assert.Contains("Telegram", denied.Title);
        Assert.Contains(KnowledgeSources.Telegram, denied.Sources);
    }

    [Fact]
    public void Area_goes_to_the_agent_that_has_its_tools()
    {
        // Задачи отделов: у HR нет права на задачи, у продаж есть — отвечают продажи.
        var sources = new Dictionary<string, IReadOnlySet<string>>
        {
            ["sales"] = new HashSet<string> { KnowledgeSources.SalesSecondary, KnowledgeSources.WorkTasks },
            ["hr"] = new HashSet<string> { KnowledgeSources.HrAttendance },
        };
        IReadOnlyList<AgentConfig> available = All.Where(a => a.Code is "sales" or "hr").ToList();

        Assert.Equal(["sales"], Route(Turn("Статус проекта запуска новой линии"), available, sources).Agents);
    }

    [Fact]
    public void Default_agent_is_the_one_with_the_most_data()
    {
        var sources = new Dictionary<string, IReadOnlySet<string>>
        {
            ["finance"] = new HashSet<string> { KnowledgeSources.FinancePayments },
            ["sales"] = new HashSet<string>(),
            ["hr"] = new HashSet<string> { KnowledgeSources.HrAttendance, KnowledgeSources.WorkTasks, KnowledgeSources.Documents },
        };

        Assert.Equal("hr", RouteIntent.DefaultAgent(All, sources));
        Assert.Equal("sales", RouteIntent.DefaultAgent(All));
    }

    [Fact]
    public void Router_choice_is_kept_as_is()
    {
        var chosen = new RoutingDecision(["finance"], new Dictionary<string, string> { ["finance"] = "задача" }, "x", false, AiTokenUsage.None);

        Assert.Same(chosen, OrchestratedConsultantEngine.Enforce(chosen, Turn("Привет"), null, All));
    }
}
