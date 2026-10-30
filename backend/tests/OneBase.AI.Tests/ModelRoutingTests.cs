using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OneBase.AI.Agents;
using OneBase.AI.Consultant;
using OneBase.AI.Gateway;
using OneBase.AI.Grounding;
using OneBase.AI.Llm;
using OneBase.AI.Monitoring;
using OneBase.AI.Orchestration;
using OneBase.AI.Providers;
using OneBase.Api.Controllers;

namespace OneBase.AI.Tests;

/// <summary>
/// Маршрутизация моделей по назначению вызова: маршрутизатор и сотрудники — по настройкам, итоговый ответ — по сложности (правила без вызова
/// модели), перепроверка — ограниченное число попыток и один переход вверх; бюджет вопроса и защита от платных вызовов.
/// </summary>
public class ModelRoutingTests
{
    private static readonly AiModelRef Opus = new("anthropic", "claude-opus-5-5");
    private static readonly AiModelRef Sonnet = new("anthropic", "claude-sonnet-5-5");

    private static AiSettingsSnapshot Settings(string mode = "on", string finalMode = "dynamic", string fallback = "review", int threshold = 3, int attempts = 2,
        decimal? budget = null, AiModelRef? agent = null, AiModelRef? complex = null) =>
        new(true, Opus, Sonnet, Sonnet, null, null, 4096, false, TaskRoutingEnabled: true, RouterMode: "on",
            Routing: AiModelRoutingSettings.From(mode, agent ?? Sonnet, finalMode, Sonnet, complex ?? Opus, fallback, threshold, attempts, budget));

    private static AgentRun Run(string agent, string status = "completed", int tools = 2, int calculations = 0, IReadOnlyList<string>? unverified = null,
        int sources = 1, IReadOnlyList<AgentToolError>? errors = null) =>
        new(new AgentResult(agent, agent, status, "итог", [], [], [], [], Enumerable.Range(0, sources).Select(i => new DataSource($"Источник {agent} {i}")).ToList(),
            ["get_sales"], null, unverified), AiTokenUsage.None, Sonnet.ToString(), null, tools, calculations, errors);

    private static readonly RoutingDecision Routed = new(["sales"], new Dictionary<string, string>(), "по вопросу", false, AiTokenUsage.None);

    [Fact]
    public void Router_and_agents_use_the_configured_models_and_explicit_choices_win()
    {
        var s = Settings();
        Assert.Equal(Sonnet, AiTaskRouting.For(s, AiTaskCategory.Route).Model);
        Assert.Equal(new AiModelChoice(Sonnet, "модель сотрудников"), AiModelRouting.Agent(s, null, null));
        Assert.Equal(Opus, AiModelRouting.Agent(s, null, Opus).Model); // модель сотрудника в «Агентах» — выбор человека
        Assert.Equal(Opus, AiModelRouting.Agent(s, Opus, Sonnet).Model); // модель канала — первая

        // Выключено — как раньше: основная модель (null) у сотрудников и итогового ответа.
        var off = Settings(mode: "off");
        Assert.Null(AiModelRouting.Agent(off, null, null).Model);
        Assert.Null(AiModelRouting.Final(off, null, null, new AnswerComplexity(9, ["много"])).Model);
    }

    [Fact]
    public void Empty_or_wrong_settings_never_pick_an_expensive_model_by_accident()
    {
        var routing = AiModelRoutingSettings.From("всегда", Sonnet, "умный", Sonnet, Opus, "повторять", 100, 50, -5);
        Assert.Equal(("off", "fixed", "standard", 10, 3, (decimal?)null),
            (routing.Mode, routing.FinalMode, routing.FallbackMode, routing.ComplexityThreshold, routing.ReviewMaxAttempts, routing.QuestionBudgetUsd));

        // Нет модели сотрудников — модель стандартных ответов, а не основная (Opus).
        var noAgent = AiModelRoutingSettings.From("on", null, "dynamic", Sonnet, Opus, "review", 3, 2, null);
        Assert.Equal(Sonnet, AiModelRouting.PlannedAgent(noAgent).Model);
        // Нет модели сложных ответов — сложный вопрос всё равно стандартной моделью.
        var noComplex = AiModelRoutingSettings.From("on", Sonnet, "dynamic", Sonnet, null, "review", 3, 2, null);
        var choice = AiModelRouting.PlannedFinal(noComplex, new AnswerComplexity(5, ["поиск причин по данным +3"]));
        Assert.Equal(Sonnet, choice.Model);
        Assert.Contains("модель сложных ответов не задана", choice.Reason);
        Assert.Null(AiModelRouting.Escalation(noComplex, Sonnet));

        // Сохранение: во «Включено» без моделей — ошибка, а не тихая основная модель.
        Assert.NotNull(AiSettingsController.ValidateRouting(new AiSettingsController.ModelRoutingInput("on", null, "dynamic", Ref(Sonnet), Ref(Opus), "review", 3, 2, null)));
        Assert.NotNull(AiSettingsController.ValidateRouting(new AiSettingsController.ModelRoutingInput("on", Ref(Sonnet), "dynamic", Ref(Sonnet), null, "review", 3, 2, null)));
        Assert.NotNull(AiSettingsController.ValidateRouting(new AiSettingsController.ModelRoutingInput("on", Ref(Sonnet), "fixed", Ref(Sonnet), null, "review", 3, 9, null)));
        Assert.Null(AiSettingsController.ValidateRouting(new AiSettingsController.ModelRoutingInput("on", Ref(Sonnet), "dynamic", Ref(Sonnet), Ref(Opus), "review", 3, 2, 1m)));
        Assert.Null(AiSettingsController.ValidateRouting(new AiSettingsController.ModelRoutingInput("off", null, "fixed", null, null, "standard", 3, 2, null)));
    }

    private static AiSettingsController.ModelRefInput Ref(AiModelRef m) => new(m.Provider, m.Model);

    [Fact]
    public void Dynamic_picks_sonnet_for_standard_questions_and_opus_for_causal_cross_department_or_troubled_ones()
    {
        var s = Settings();
        AiModelChoice Final(string question, IReadOnlyList<AgentRun> runs, IReadOnlyList<LlmMessage>? history = null, bool correction = false,
            RoutingDecision? routing = null, int toolErrors = 0) =>
            AiModelRouting.Final(s, null, null, AnswerComplexityRules.Assess(question, history ?? [], runs, routing ?? Routed, correction, toolErrors));

        // Sonnet: простые вопросы, планы и KPI, сравнение периодов, данные двух отделов без синтеза — и длинный вопрос.
        Assert.Equal(Sonnet, Final("Как продажи за октябрь?", [Run("sales")]).Model);
        Assert.Equal(Sonnet, Final("Сравни продажи октября и сентября по регионам", [Run("sales", tools: 4, calculations: 2)]).Model);
        Assert.Equal(Sonnet, Final("Как продажи и задолженность дилеров по Хорезму в этом месяце?", [Run("sales"), Run("finance")]).Model);
        Assert.Equal(Sonnet, Final(string.Join(" ", Enumerable.Repeat("Покажи продажи по всем регионам за октябрь подробно", 30)), [Run("sales")]).Model);
        Assert.Equal(Sonnet, Final("Привет! Что ты умеешь?", []).Model);
        // Как на настоящей модели: идущий месяц — сотрудник «partial», много данных и расчётов — всё равно стандартный вопрос.
        Assert.Equal(Sonnet, Final("Как продажи за октябрь?", [Run("sales", "partial", tools: 9, calculations: 2, sources: 11)]).Model);

        // Opus: поиск причин по данным, межотдельный синтез с рекомендациями, ошибки и нехватка доказательств у сотрудников.
        var why = Final("Почему упали продажи в Самарканде на прошлой неделе?", [Run("sales")]);
        Assert.Equal(Opus, why.Model);
        Assert.Contains("поиск причин", why.Reason);
        Assert.Equal(Opus, Final("Что происходит в компании и что улучшить в первую очередь?", [Run("sales"), Run("finance"), Run("hr")]).Model);
        Assert.Equal(Opus, Final("Как продажи и долги дилеров?", [Run("sales", "partial"), Run("finance", unverified: ["12 млн"])]).Model);

        // Уточнение наследует намерение прошлого вопроса: «а в Бухаре?» после «почему» — тоже поиск причин; после «как продажи» — нет.
        var whyHistory = new List<LlmMessage> { new(LlmRole.User, "Почему упали продажи в Самарканде?"), new(LlmRole.Assistant, "…") };
        Assert.Equal(Opus, Final("А в Бухаре?", [Run("sales")], whyHistory).Model);
        var plainHistory = new List<LlmMessage> { new(LlmRole.User, "Как продажи за октябрь?"), new(LlmRole.Assistant, "…") };
        Assert.Equal(Sonnet, Final("А за сентябрь?", [Run("sales")], plainHistory).Model);
        Assert.Equal(Sonnet, Final("Только помадка", [Run("sales")], plainHistory).Model);
        Assert.True(AnswerComplexityRules.IsFollowUp("А по Хоразму?"));
        Assert.False(AnswerComplexityRules.IsFollowUp("Какие регионы не выполняют план продаж в октябре?"));

        // Fixed — всегда стандартная модель, даже для сложного.
        Assert.Equal(Sonnet, AiModelRouting.Final(Settings(finalMode: "fixed"), null, null, new AnswerComplexity(9, ["много"])).Model);
    }

    [Fact]
    public async Task Review_has_limited_attempts_escalates_once_and_never_goes_back()
    {
        var evidence = new EvidenceSet();
        evidence.AddContent("""{"factKg":1400}""");
        // Модель упорно пишет неподтверждённое число.
        var gateway = new ScriptedGateway(_ => "Продажи — 9 999 кг.");
        var review = new ReviewPlan(2) { Escalation = new AiModelChoice(Opus, "переход") };

        var answer = await new GroundedComposer(gateway).ComposeAsync([new(LlmRole.System, "данные: factKg 1400"), new(LlmRole.User, "Сколько?")],
            new AiCallOptions { Model = Sonnet, Context = new AiCallContext(Purpose: "consultant.answer") }, evidence, "Сколько?", CancellationToken.None, review: review);

        Assert.Equal([Sonnet, Sonnet, Opus], gateway.Calls.Select(c => c.Options.Model)); // ответ, исправление той же моделью, переход вверх
        Assert.Equal(["consultant.answer", "consultant.revise", "consultant.review"], gateway.Calls.Select(c => c.Options.Context.Purpose));
        Assert.Equal(0, review.Left);
        Assert.True(answer.Grounding.Warned); // неподтверждённое не выдано за факт
        Assert.Contains("Не подтверждено данными OneBase", answer.Text);
        Assert.Equal((2, "anthropic/claude-opus-5-5"), (answer.Grounding.Revisions, answer.Grounding.EscalatedTo));
        // Данные сотрудников (системные сообщения) при смене модели не теряются.
        Assert.Equal(gateway.Calls[0].Messages[0].Content, gateway.Calls[2].Messages[0].Content);

        // Попыток больше, чем ступеней, — всё равно не больше одного перехода и без возврата на стандартную модель.
        var many = new ScriptedGateway(_ => "Продажи — 9 999 кг.");
        await new GroundedComposer(many).ComposeAsync([new(LlmRole.User, "Сколько?")], new AiCallOptions { Model = Sonnet }, evidence, "Сколько?",
            CancellationToken.None, review: new ReviewPlan(3) { Escalation = new AiModelChoice(Opus, "переход") });
        Assert.Equal([Sonnet, Sonnet, Opus], many.Calls.Select(c => c.Options.Model));

        // Без плана (standard) — как раньше: одно исправление той же моделью.
        var standard = new ScriptedGateway(_ => "Продажи — 9 999 кг.");
        await new GroundedComposer(standard).ComposeAsync([new(LlmRole.User, "Сколько?")], new AiCallOptions { Model = Sonnet }, evidence, "Сколько?",
            CancellationToken.None);
        Assert.Equal(2, standard.Calls.Count);

        // Бюджет вопроса на исходе — исправлений нет, ответ помечен.
        var broke = new ScriptedGateway(_ => "Продажи — 9 999 кг.");
        var poor = await new GroundedComposer(broke).ComposeAsync([new(LlmRole.User, "Сколько?")], new AiCallOptions { Model = Sonnet }, evidence, "Сколько?",
            CancellationToken.None, review: new ReviewPlan(2, () => false) { Escalation = new AiModelChoice(Opus, "переход") });
        Assert.Single(broke.Calls);
        Assert.True(poor.Grounding.Warned);

        // Исправленный ответ с подтверждёнными числами принимается, переход не нужен.
        var fixedOnce = new ScriptedGateway(n => n == 0 ? "Продажи — 9 999 кг." : "Продажи — 1 400 кг.");
        var good = await new GroundedComposer(fixedOnce).ComposeAsync([new(LlmRole.User, "Сколько?")], new AiCallOptions { Model = Sonnet }, evidence, "Сколько?",
            CancellationToken.None, review: new ReviewPlan(2) { Escalation = new AiModelChoice(Opus, "переход") });
        Assert.Equal(2, fixedOnce.Calls.Count);
        Assert.False(good.Grounding.Warned);
        Assert.Equal("Продажи — 1 400 кг.", good.Text);
    }

    [Fact]
    public async Task Cut_off_answer_is_rewritten_once_by_review()
    {
        var gateway = new ScriptedGateway(n => n == 0 ? "Главное: продажи растут, а по регионам" : "Главное: продажи растут.", n => n == 0 ? "max_tokens" : "end_turn");
        var answer = await new GroundedComposer(gateway).ComposeAsync([new(LlmRole.User, "Как дела?")], new AiCallOptions { Model = Sonnet }, new EvidenceSet(),
            "Как дела?", CancellationToken.None, review: new ReviewPlan(2) { Escalation = new AiModelChoice(Opus, "переход") });

        Assert.Equal(2, gateway.Calls.Count);
        Assert.Contains("оборвался", gateway.Calls[1].Messages[^1].Content);
        Assert.Equal("Главное: продажи растут.", answer.Text);
        Assert.Equal([AiReviewCauses.Incomplete], answer.Grounding.Causes);
    }

    [Fact]
    public void Agent_retry_is_only_for_unfinished_or_wrong_tool_runs_never_for_access_refusals()
    {
        Assert.Equal(AiReviewCauses.Incomplete, AgentOrchestrator.RetryReason(Run("sales", "failed")));
        Assert.Equal(AiReviewCauses.WrongTool, AgentOrchestrator.RetryReason(Run("sales", "no_data",
            errors: [new AgentToolError("get_salez", AgentToolError.Denied, "Инструмент 'get_salez' не найден.")])));
        Assert.Null(AgentOrchestrator.RetryReason(Run("sales", "no_data")));
        Assert.Null(AgentOrchestrator.RetryReason(Run("finance", "forbidden")));
        Assert.Null(AgentOrchestrator.RetryReason(Run("sales", "completed")));
        Assert.Equal(AgentToolError.Denied, AgentRunner.ErrorKind("У агента 'sales' нет доступа к инструменту 'get_hr_day'."));
        Assert.Equal(AgentToolError.Error, AgentRunner.ErrorKind("У пользователя нет доступа к этим данным OneBase — не используй и не угадывай их."));
        var task = AgentOrchestrator.RetryTask("Продажи за октябрь", Run("sales", "failed",
            errors: [new AgentToolError("get_salez", AgentToolError.Denied, "Инструмент 'get_salez' не найден.")]), AiReviewCauses.WrongTool);
        Assert.Contains("Продажи за октябрь", task);
        Assert.Contains("get_salez", task);
        Assert.Contains("no_data", task);

        // Общий счётчик попыток ответа — потокобезопасно, не больше заданного.
        var plan = new ReviewPlan(3);
        var taken = Enumerable.Range(0, 50).AsParallel().Count(_ => plan.TryTake());
        Assert.Equal(3, taken);
        Assert.False(new ReviewPlan(1).TryEscalate()); // переходить некуда
    }

    [Fact]
    public async Task Question_budget_stops_new_calls_of_this_answer()
    {
        var provider = new FakeProvider("anthropic");
        var gateway = new AiGateway(new FixedSettings(Settings(budget: 0.10m)), new AiProviderRegistry([provider]), new NoUsage(), NullLogger<AiGateway>.Instance,
            NoAiLimits.Instance);
        var turn = AiTurnTimings.Start();

        await gateway.CompleteAsync([new(LlmRole.User, "Привет")], [], new AiCallOptions());
        turn.AddCost(0.06m);
        await gateway.CompleteAsync([new(LlmRole.User, "Привет")], [], new AiCallOptions());
        turn.AddCost(0.06m);
        var stop = await Assert.ThrowsAsync<LlmNotConfiguredException>(() => gateway.CompleteAsync([new(LlmRole.User, "Привет")], [], new AiCallOptions()));

        Assert.Contains("Бюджет одного вопроса исчерпан", stop.Message);
        Assert.Equal(2, provider.Requests.Count);
        Assert.Contains(turn.Reviews, r => r.Cause == AiReviewCauses.Budget);
        turn.AddCost(null);
        Assert.True(turn.CostIncomplete); // вызов без цены — сумма неполная, а не ноль
    }

    [Fact]
    public async Task Emulator_never_exposes_the_real_key_and_block_or_process_limits_stop_paid_calls()
    {
        var real = new AiProviderState("anthropic", "Anthropic", "https://api.anthropic.com", false, true, "sk-ant-REAL", AiKeySource.OneBase, "••••REAL");
        var emulator = AiSafetyOptions.From(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ai:Safety:EmulatorUrl"] = "http://localhost:5098/",
        }).Build());
        var guarded = emulator.Apply(real);
        Assert.Equal(("http://localhost:5098", AiSafetyOptions.EmulatorKey, true), (guarded.BaseUrl, guarded.ApiKey, guarded.IsReady));
        Assert.DoesNotContain("REAL", $"{guarded.ApiKey}{guarded.KeyHint}");

        var blocked = new AiSafetyOptions(BlockPaidCalls: true).Apply(real);
        Assert.False(blocked.IsReady);
        Assert.Empty(blocked.ApiKey);

        // Неверный адрес эмулятора — не настоящий API, а блокировка всех вызовов.
        var broken = AiSafetyOptions.From(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ai:Safety:EmulatorUrl"] = "не адрес",
        }).Build());
        Assert.True(broken.BlockPaidCalls);
        Assert.False(broken.Apply(real).IsReady);

        // Лимит вызовов процесса: дошли — новые вызовы отклоняются.
        var limits = new AiLimits(new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), new FixedSettings(Settings()), TimeProvider.System,
            NullLogger<AiLimits>.Instance, new AiSafetyOptions(MaxCalls: 2));
        limits.Recorded(0.01m);
        limits.Recorded(null);
        var limit = await Assert.ThrowsAsync<LlmNotConfiguredException>(() => limits.EnsureBudgetAsync());
        Assert.Contains("Ai:Safety:MaxCalls", limit.Message);
    }

    private sealed class ScriptedGateway(Func<int, string> reply, Func<int, string>? stop = null) : IAiGateway
    {
        public List<(IReadOnlyList<LlmMessage> Messages, AiCallOptions Options)> Calls { get; } = [];

        public Task<AiReadiness> GetReadinessAsync(CancellationToken cancellationToken = default) => Task.FromResult(new AiReadiness(true, null));

        public Task<AiCallResult> CompleteAsync(IReadOnlyList<LlmMessage> messages, IReadOnlyList<LlmToolDefinition> tools, AiCallOptions options,
            CancellationToken cancellationToken = default)
        {
            var n = Calls.Count;
            Calls.Add((messages.ToList(), options));
            var model = options.Model ?? Opus;
            return Task.FromResult(new AiCallResult(reply(n), [], new AiTokenUsage(100, 50), model, false, 1, stop?.Invoke(n) ?? "end_turn"));
        }

        public Task<(IReadOnlyList<float[]> Vectors, AiModelRef Model)> EmbedAsync(IReadOnlyList<string> inputs, AiCallContext context,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FixedSettings(AiSettingsSnapshot settings) : IAiSettingsSource
    {
        public Task<AiSettingsSnapshot> GetSettingsAsync(CancellationToken ct = default) => Task.FromResult(settings);

        public Task<AiProviderState?> GetProviderAsync(string code, CancellationToken ct = default) =>
            Task.FromResult<AiProviderState?>(new AiProviderState(code, code, $"https://{code}.test", false, true, "key", AiKeySource.OneBase, null));
    }

    private sealed class NoUsage : IAiUsageRecorder
    {
        public Task RecordAsync(AiCallRecord record, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
