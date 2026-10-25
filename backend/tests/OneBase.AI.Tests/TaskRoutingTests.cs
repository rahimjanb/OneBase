using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OneBase.AI.Agents;
using OneBase.AI.Gateway;
using OneBase.AI.Llm;
using OneBase.AI.Monitoring;
using OneBase.AI.Orchestration;
using OneBase.AI.Providers;
using OneBase.AI.Tools.Data;
using OneBase.Domain.AI;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>Модели по категориям простых задач: выбор модели, откат на исходную, режим сравнения.</summary>
public class TaskRoutingTests
{
    internal static readonly AiModelRef Opus = new("anthropic", "claude-opus-5-5");
    internal static readonly AiModelRef Sonnet = new("anthropic", "claude-sonnet-5-5");
    internal static readonly AiModelRef Haiku = new("anthropic", "claude-haiku-5-5");

    internal static AiSettingsSnapshot Settings(bool enabled, string routerMode, AiModelRef? router = null, string extractMode = AiTaskMode.Off) =>
        new(true, Opus, Sonnet, router ?? Haiku, null, null, 16000, false, TaskRoutingEnabled: enabled, RouterMode: routerMode, Extract: Haiku, ExtractMode: extractMode);

    private static AgentConfig Agent(string code) => new(code, code + " AI", "роль", "описание", "prompt", true, null, null, null, true, 1, code, null, []);

    private static readonly AgentConfig[] Available = [Agent("sales"), Agent("finance"), Agent("supply")];

    [Fact]
    public void Category_model_is_used_only_when_the_flag_and_its_mode_allow_it()
    {
        // Общий флаг выключен — как раньше: основной моделью, без сравнения, даже если модель и режим заданы.
        Assert.Equal(new AiTaskRoute(AiTaskCategory.Route, null, null, null, Opus), AiTaskRouting.For(Settings(false, AiTaskMode.On), AiTaskCategory.Route));
        Assert.Equal(new AiTaskRoute(AiTaskCategory.Route, null, null, null, Opus), AiTaskRouting.For(Settings(true, AiTaskMode.Off), AiTaskCategory.Route));

        // «Сравнение» — пользователь получает результат основной модели, Haiku — в фоне.
        Assert.Equal(new AiTaskRoute(AiTaskCategory.Route, null, null, Haiku, Opus), AiTaskRouting.For(Settings(true, AiTaskMode.Shadow), AiTaskCategory.Route));

        // «Включено» — Haiku, при сбое — исходная модель.
        var on = AiTaskRouting.For(Settings(true, AiTaskMode.On), AiTaskCategory.Route);
        Assert.Equal(new AiTaskRoute(AiTaskCategory.Route, Haiku, Opus, null, Opus), on);
        Assert.True(on.Routed);

        // Модель категории совпадает с основной — переключать некуда.
        Assert.False(AiTaskRouting.For(Settings(true, AiTaskMode.On, router: Opus), AiTaskCategory.Route).Routed);

        // Категории независимы: маршрутизация включена, извлечение — нет.
        Assert.False(AiTaskRouting.For(Settings(true, AiTaskMode.On), AiTaskCategory.Extract).Routed);
        Assert.True(AiTaskRouting.For(Settings(true, AiTaskMode.Off, extractMode: AiTaskMode.On), AiTaskCategory.Extract).Routed);
    }

    [Fact]
    public async Task Routed_question_goes_to_haiku_with_the_original_model_as_fallback_and_no_retry_of_haiku()
    {
        var gateway = new ModelGateway((model, _) => """{"agents":["sales"],"tasks":{"sales":"Продажи за октябрь"},"reason":"продажи"}""");
        var router = new AiRouter(gateway, new FixedSettings(Settings(true, AiTaskMode.On)));

        var decision = await router.RouteAsync("Как продажи за октябрь?", [], Available, new AiCallContext(), default);

        Assert.Equal(["sales"], decision.Agents);
        var call = Assert.Single(gateway.Calls);
        Assert.Equal((Haiku, Opus, false), (call.Model, call.Fallback, call.RetrySameModel));
        Assert.Equal("consultant.route", call.Context.Purpose);
    }

    [Fact]
    public async Task Incorrect_haiku_answer_is_redone_once_by_the_original_model()
    {
        var gateway = new ModelGateway((model, _) => model == Haiku
            ? "Думаю, нужен отдел продаж."                                    // не JSON
            : """{"agents":["sales","finance"],"tasks":{"sales":"Продажи","finance":"Прибыль"},"reason":"прибыль"}""");
        var router = new AiRouter(gateway, new FixedSettings(Settings(true, AiTaskMode.On)));

        var decision = await router.RouteAsync("Почему прибыль упала?", [], Available, new AiCallContext(), default);

        Assert.Equal(["sales", "finance"], decision.Agents);
        Assert.False(decision.ByKeywords);
        Assert.Equal([Haiku, Opus], gateway.Calls.Select(c => c.Model));
        Assert.Equal("consultant.route.fallback", gateway.Calls[1].Context.Purpose);
        Assert.Equal(2, decision.Usage.OutputTokens / 100); // токены обоих вызовов учтены

        // Только несуществующие сотрудники — тоже некорректно.
        var invented = new ModelGateway((model, _) => model == Haiku ? """{"agents":["logistics"]}""" : """{"agents":["supply"]}""");
        Assert.Equal(["supply"], (await new AiRouter(invented, new FixedSettings(Settings(true, AiTaskMode.On)))
            .RouteAsync("Что со складом?", [], Available, new AiCallContext(), default)).Agents);
    }

    [Fact]
    public async Task No_extra_call_when_routing_is_off_the_budget_is_spent_or_the_original_model_already_answered()
    {
        // Маршрутизация выключена: некорректный ответ основной модели — выбор по ключевым словам, без второго вызова (как раньше).
        var off = new ModelGateway((_, _) => "не знаю");
        var decision = await new AiRouter(off, new FixedSettings(Settings(false, AiTaskMode.On))).RouteAsync("Какие остатки на складах?", [], Available, new AiCallContext(), default);
        Assert.True(decision.ByKeywords);
        Assert.Equal([null], off.Calls.Select(c => c.Model));

        // Бюджет исчерпан при повторе — выбор по ключевым словам, ошибки у пользователя нет.
        var budget = new ModelGateway((_, _) => "не знаю") { Fail = o => o.Model == Opus ? new LlmNotConfiguredException("Дневной бюджет на AI исчерпан.") : null };
        var spent = await new AiRouter(budget, new FixedSettings(Settings(true, AiTaskMode.On))).RouteAsync("Какие остатки на складах?", [], Available, new AiCallContext(), default);
        Assert.True(spent.ByKeywords);
        Assert.Equal([Haiku, Opus], budget.Calls.Select(c => c.Model));

        // Haiku уже не ответил и шлюз сам перешёл на исходную модель — её некорректный ответ не повторяется.
        var fellBack = new ModelGateway((_, _) => "не знаю") { UsedFallback = true };
        await new AiRouter(fellBack, new FixedSettings(Settings(true, AiTaskMode.On))).RouteAsync("Какие остатки?", [], Available, new AiCallContext(), default);
        Assert.Single(fellBack.Calls);
    }

    [Fact]
    public async Task Comparison_mode_answers_with_the_original_model_and_sends_haiku_to_the_background()
    {
        var gateway = new ModelGateway((model, _) => """{"agents":["sales"],"tasks":{"sales":"Продажи"},"reason":"x"}""");
        var comparisons = new RecordingComparisons();
        var router = new AiRouter(gateway, new FixedSettings(Settings(true, AiTaskMode.Shadow)), null, comparisons);

        var decision = await router.RouteAsync("Как продажи?", [], Available, new AiCallContext(), default);

        Assert.Equal(["sales"], decision.Agents);
        Assert.Null(Assert.Single(gateway.Calls).Model); // пользователю — основная модель
        var shadow = Assert.Single(comparisons.Shadows);
        Assert.Equal((AiTaskCategory.Route, Haiku, "sales", true), (shadow.Category, shadow.Candidate, shadow.Baseline.Result, shadow.Baseline.Valid));
        Assert.Equal(("finance,sales", true), shadow.Evaluate("""{"agents":["sales","finance"]}"""));
        Assert.Equal((null, false), shadow.Evaluate("нет"));
        Assert.Equal(0.5, shadow.Similarity("sales", "finance,sales"));
    }

    [Fact]
    public async Task Failed_haiku_call_falls_back_once_without_retrying_haiku()
    {
        var openAi = new FakeProvider("openai");
        var anthropic = new FakeProvider("anthropic");
        anthropic.Fail(new AiProviderException("anthropic", 529, "Overloaded", true), times: 1);
        var source = new ProviderSettings(Settings(true, AiTaskMode.On));
        var gateway = new AiGateway(source, new AiProviderRegistry([openAi, anthropic]), new NullRecorder(), NullLogger<AiGateway>.Instance, NoAiLimits.Instance);

        var result = await gateway.CompleteAsync([new(LlmRole.User, "x")], [], new AiCallOptions { Model = Haiku, Fallback = Opus, RetrySameModel = false });

        // Один вызов Haiku (сбой) и сразу исходная модель — без второго вызова Haiku.
        Assert.Equal(["claude-haiku-5-5", "claude-opus-5-5"], anthropic.Requests.Select(r => r.Model));
        Assert.True(result.UsedFallback);

        // Обычный вызов, как раньше: тот же сбой — повтор той же модели.
        anthropic.Requests.Clear();
        anthropic.Fail(new AiProviderException("anthropic", 529, "Overloaded", true), times: 1);
        await gateway.CompleteAsync([new(LlmRole.User, "x")], [], new AiCallOptions { Model = Opus, Fallback = Sonnet });
        Assert.Equal(["claude-opus-5-5", "claude-opus-5-5"], anthropic.Requests.Select(r => r.Model));
    }

    [Fact]
    public void Evaluation_case_is_correct_only_with_required_and_permitted_agents()
    {
        var all = new HashSet<string> { "sales", "finance", "marketing", "hr", "production", "supply" };
        RoutingDecision D(params string[] agents) => new(agents, agents.ToDictionary(a => a, a => "задача"), "", false, AiTokenUsage.None);
        var profit = RouteEvalCases.All.Single(c => c.Code == "profit-drop");
        Assert.True(RouteEvalCases.Correct(profit, D("finance", "sales", "marketing"), all));
        Assert.False(RouteEvalCases.Correct(profit, D("finance"), all)); // нет sales
        Assert.False(RouteEvalCases.Correct(profit, D("finance", "sales", "hr"), all)); // hr лишний
        Assert.False(RouteEvalCases.Correct(profit, null, all)); // ответ не разобран

        var greeting = RouteEvalCases.All.Single(c => c.Code == "greeting");
        Assert.True(RouteEvalCases.Correct(greeting, D(), all));
        Assert.False(RouteEvalCases.Correct(greeting, D("sales"), all));

        var shipments = RouteEvalCases.All.Single(c => c.Code == "shipments");
        Assert.True(RouteEvalCases.Correct(shipments, D("production"), all));
        Assert.False(RouteEvalCases.Correct(shipments, D(), all)); // хотя бы один нужен

        // Уточнение по истории: задача должна сохранить регион и период.
        var follow = RouteEvalCases.All.Single(c => c.Code == "follow-up");
        Assert.True(RouteEvalCases.Correct(follow, new RoutingDecision(["sales"], new Dictionary<string, string> { ["sales"] = "Продажи за октябрь по Самарканду" }, "", false, AiTokenUsage.None), all));
        Assert.False(RouteEvalCases.Correct(follow, new RoutingDecision(["sales"], new Dictionary<string, string> { ["sales"] = "Продажи по Самарканду" }, "", false, AiTokenUsage.None), all));

        // Выключенный сотрудник из ожиданий убирается.
        var company = RouteEvalCases.All.Single(c => c.Code == "company");
        Assert.True(RouteEvalCases.Correct(company, D("finance", "sales"), new HashSet<string> { "finance", "sales" }));
        Assert.Equal(24, RouteEvalCases.All.Count);
        Assert.Equal(RouteEvalCases.All.Count, RouteEvalCases.All.Select(c => c.Code).Distinct().Count());
    }

    [Fact]
    public async Task Sheet_mapping_on_haiku_falls_back_to_the_original_model_when_json_is_broken()
    {
        var gateway = new ModelGateway((model, _) => model == Haiku
            ? """{"suggestions":"нет"}"""
            : """{"suggestions":[{"sourceColumn":"D","targetField":"plan_primary.value","confidence":0.9,"reason":"план"}],"fields":[],"notes":[]}""");
        var advisor = new OneBase.AI.DataSources.SheetMappingAdvisor(gateway, new FixedSettings(Settings(false, AiTaskMode.Off) with { TaskRoutingEnabled = true, ExtractMode = AiTaskMode.On }));

        var raw = await advisor.AdviseAsync(SheetMappingTests.Primary(), Guid.NewGuid());

        Assert.Equal("plan_primary.value", Assert.Single(raw.Suggestions).TargetField);
        Assert.Equal([Haiku, Opus], gateway.Calls.Select(c => c.Model));
        Assert.Equal("datasources.automap.fallback", gateway.Calls[1].Context.Purpose);
        Assert.Equal("anthropic/claude-opus-5-5", raw.Model);
        Assert.Equal(0.5, OneBase.AI.DataSources.SheetMappingAdvisor.ExtractSimilarity("A→x; B→y", "A→x; B→z"));
    }

    internal sealed class ModelGateway(Func<AiModelRef, IReadOnlyList<LlmMessage>, string> reply) : IAiGateway
    {
        public List<AiCallOptions> Calls { get; } = [];
        public Func<AiCallOptions, Exception?>? Fail { get; init; }
        public bool UsedFallback { get; init; }

        public Task<AiReadiness> GetReadinessAsync(CancellationToken cancellationToken = default) => Task.FromResult(new AiReadiness(true, null));

        public Task<AiCallResult> CompleteAsync(IReadOnlyList<LlmMessage> messages, IReadOnlyList<LlmToolDefinition> tools, AiCallOptions options,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(options);
            if (Fail?.Invoke(options) is { } error)
            {
                throw error;
            }

            var model = options.Model ?? Opus;
            return Task.FromResult(new AiCallResult(reply(model, messages), [], new AiTokenUsage(1_000, 100, CacheReadTokens: 2_000), model, UsedFallback,
                model == Haiku ? 300 : 2_000, "end_turn"));
        }

        public Task<(IReadOnlyList<float[]> Vectors, AiModelRef Model)> EmbedAsync(IReadOnlyList<string> inputs, AiCallContext context,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    internal sealed class FixedSettings(AiSettingsSnapshot settings) : IAiSettingsSource
    {
        public Task<AiSettingsSnapshot> GetSettingsAsync(CancellationToken ct = default) => Task.FromResult(settings);

        public Task<AiProviderState?> GetProviderAsync(string code, CancellationToken ct = default) => Task.FromResult<AiProviderState?>(null);
    }

    private sealed class ProviderSettings(AiSettingsSnapshot settings) : IAiSettingsSource
    {
        public Task<AiSettingsSnapshot> GetSettingsAsync(CancellationToken ct = default) => Task.FromResult(settings);

        public Task<AiProviderState?> GetProviderAsync(string code, CancellationToken ct = default) =>
            Task.FromResult<AiProviderState?>(new AiProviderState(code, code, $"https://{code}.test", false, true, "key", AiKeySource.OneBase, null));
    }

    private sealed class NullRecorder : IAiUsageRecorder
    {
        public Task RecordAsync(AiCallRecord record, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingComparisons : IModelComparisons
    {
        public List<(AiTaskCategory Category, AiModelRef Candidate, ComparisonSide Baseline, Func<string?, (string? Result, bool Valid)> Evaluate,
            Func<string?, string?, double> Similarity)> Shadows { get; } = [];

        public Task ShadowAsync(AiTaskCategory category, IReadOnlyList<LlmMessage> messages, AiCallOptions options, AiModelRef candidate, string? task,
            ComparisonSide baseline, Func<string?, (string? Result, bool Valid)> evaluate, Func<string?, string?, double> similarity)
        {
            Shadows.Add((category, candidate, baseline, evaluate, similarity));
            return Task.CompletedTask;
        }

        public Task RecordAsync(AiTaskCategory category, string source, string? caseCode, string? task, string? expected, ComparisonSide baseline,
            ComparisonSide candidate, double similarity, bool? baselineCorrect, bool? candidateCorrect, CancellationToken ct = default) => Task.CompletedTask;
    }
}

/// <summary>Сравнение в базе: фон пишет итоги со стоимостью по ценам моделей; эталонный набор — по задаче на строку; у порога бюджета фон молчит.</summary>
public class ModelComparisonDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    [PostgresFact]
    public async Task Shadow_and_evaluation_record_both_models_with_tokens_cost_and_correctness()
    {
        var limits = fixture.Services.GetRequiredService<IAiLimits>();
        var scopes = fixture.Services.GetRequiredService<IServiceScopeFactory>();

        // Фон: Haiku выбрал на одного сотрудника больше — расхождение со стоимостью обеих моделей.
        var haikuGateway = new TaskRoutingTests.ModelGateway((_, _) => """{"agents":["sales","finance"]}""");
        var comparisons = new ModelComparisons(haikuGateway, limits, scopes, NullLogger<ModelComparisons>.Instance);
        var baseline = new ComparisonSide("anthropic/claude-opus-5-5", "sales", true, new AiTokenUsage(1_000, 100, CacheReadTokens: 2_000), 2_000);
        await comparisons.ShadowAsync(AiTaskCategory.Route, [new(LlmRole.User, "Почему прибыль упала?")], new AiCallOptions { Context = new AiCallContext(Guid.NewGuid(), "director", Guid.NewGuid(), "consultant.route") },
            TaskRoutingTests.Haiku, "Почему прибыль упала?", baseline,
            text => AiRouter.Parse(text, [new("sales", "s", "r", "d", "p", true, null, null, null, true, 1, "sales", null, []), new("finance", "f", "r", "d", "p", true, null, null, null, true, 1, "finance", null, [])],
                AiTokenUsage.None) is { } d ? (AiRouter.RouteResult(d), true) : (null, false), AiRouter.RouteSimilarity);

        var call = Assert.Single(haikuGateway.Calls);
        Assert.Equal((TaskRoutingTests.Haiku, false, false), (call.Model, call.AllowFallback, call.RetrySameModel));
        Assert.Equal(new AiCallContext(null, null, null, "consultant.route.shadow"), call.Context); // не в стоимости вопроса пользователя

        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var row = await db.AiModelComparisons.AsNoTracking().SingleAsync(c => c.Source == "shadow");
            Assert.Equal(("route", "anthropic/claude-opus-5-5", "anthropic/claude-haiku-5-5", "sales", "finance,sales"),
                (row.Category, row.BaselineModel, row.CandidateModel, row.BaselineResult, row.CandidateResult));
            Assert.False(row.Agree);
            Assert.Equal(0.5, row.Similarity);
            // Opus: 1 000 × $4 + 2 000 × $0.20 + 100 × $20 = $0.0064; Haiku: 1 000 × $0.10 + 2 000 × $0.01 + 100 × $0.50 = $0.00017.
            Assert.Equal((0.0064m, 0.00017m), (row.BaselineCostUsd, row.CandidateCostUsd));
            Assert.Equal((2_000L, 300L), (row.BaselineMs >= 0 ? 2_000L : -1, row.CandidateMs >= 0 ? 300L : -1));
        }

        // Эталонный набор: Haiku верно отвечает на всё, кроме приветствий (зовёт продажи).
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var settings = await db.AiSettings.FirstOrDefaultAsync(s => s.Id == AiSettings.SingletonId) ?? db.AiSettings.Add(new AiSettings()).Entity;
            (settings.PrimaryProvider, settings.PrimaryModel, settings.RouterProvider, settings.RouterModel) = ("anthropic", "claude-opus-5-5", "anthropic", "claude-haiku-5-5");
            await db.SaveChangesAsync();
            fixture.Services.GetRequiredService<AiSettingsStore>().Invalidate();
        }

        var answers = RouteEvalCases.All.ToDictionary(c => c.Question, c => c);
        var evalGateway = new TaskRoutingTests.ModelGateway((model, messages) =>
        {
            var request = messages[1].Content;
            var c = RouteEvalCases.All.Last(x => request.Contains(x.Question, StringComparison.Ordinal));
            var agents = model == TaskRoutingTests.Haiku && c.Must.Length == 0 && c.AnyOf is null ? ["sales"] : c.Must.Concat(c.AnyOf?.Take(1) ?? []).ToArray();
            var task = c.TaskMustContain is null ? "задача" : "Продажи за октябрь по Самарканду";
            return System.Text.Json.JsonSerializer.Serialize(new { agents, tasks = agents.ToDictionary(a => a, _ => task), reason = "тест" });
        });
        using (var scope = fixture.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var evaluation = new RoutingEvaluation(evalGateway, sp.GetRequiredService<IAiSettingsSource>(), sp.GetRequiredService<AiAgentStore>(),
                sp.GetRequiredService<SalesGlossary>(), new ModelComparisons(evalGateway, limits, scopes, NullLogger<ModelComparisons>.Instance), limits);
            var run = await evaluation.RunRouteAsync(fixture.AdminId, null, CancellationToken.None);

            Assert.Equal(("done", 24, 24), (run.Status, run.Done, run.Total));
            Assert.Equal((24, 24), (run.Baseline!.Correct, run.Baseline.Valid));
            Assert.Equal(22, run.Candidate!.Correct); // «Привет» и «Спасибо» — лишний вызов продаж
            Assert.Equal(22, run.Agree);
            Assert.Equal(48, evalGateway.Calls.Count); // по вызову каждой модели на задачу, без повторов и резерва
            Assert.All(evalGateway.Calls, o => Assert.Equal(("eval.route", false, false), (o.Context.Purpose, o.AllowFallback, o.RetrySameModel)));

            var db = sp.GetRequiredService<OneBaseDbContext>();
            var rows = await db.AiModelComparisons.AsNoTracking().Where(c => c.Source == "eval").ToListAsync();
            Assert.Equal(24, rows.Count);
            Assert.Equal(["greeting", "thanks"], rows.Where(r => r.CandidateCorrect == false).Select(r => r.CaseCode).Order());
            Assert.All(rows, r => Assert.NotNull(r.Expected));
        }
    }
}

/// <summary>У порога бюджета сравнение в фоне не запускается — не тратим на необязательную работу.</summary>
public class ModelComparisonBudgetTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    [PostgresFact]
    public async Task Shadow_is_skipped_when_spending_reached_the_warning_threshold()
    {
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var settings = await db.AiSettings.FirstOrDefaultAsync(s => s.Id == AiSettings.SingletonId) ?? db.AiSettings.Add(new AiSettings()).Entity;
            settings.DailyBudgetUsd = 0.01m;
            settings.BudgetWarnPercent = 50;
            db.AiUsage.Add(new AiUsage { Purpose = "test", Provider = "anthropic", Model = "claude-opus-5-5", CostUsd = 0.006m, Success = true });
            await db.SaveChangesAsync();
            fixture.Services.GetRequiredService<AiSettingsStore>().Invalidate();
        }

        var gateway = new TaskRoutingTests.ModelGateway((_, _) => """{"agents":["sales"]}""");
        var comparisons = new ModelComparisons(gateway, fixture.Services.GetRequiredService<IAiLimits>(), fixture.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ModelComparisons>.Instance);
        await comparisons.ShadowAsync(AiTaskCategory.Route, [new(LlmRole.User, "x")], new AiCallOptions(), TaskRoutingTests.Haiku, "x",
            new ComparisonSide("anthropic/claude-opus-5-5", "sales", true, AiTokenUsage.None, 1), _ => ("sales", true), AiRouter.RouteSimilarity);

        Assert.Empty(gateway.Calls);
        using var check = fixture.Services.CreateScope();
        Assert.False(await check.ServiceProvider.GetRequiredService<OneBaseDbContext>().AiModelComparisons.AnyAsync());
    }
}

/// <summary>Миграция AiTaskModelRouting: где модель маршрутизации уже выбрана — она и дальше выбирает сотрудников; где нет — как раньше, основной.</summary>
public class TaskRoutingMigrationTests
{
    private const string Before = "20261025112329_AiAnswerCacheFlag";

    [PostgresFact]
    public async Task Existing_router_model_keeps_working_and_fresh_settings_stay_on_the_primary_model()
    {
        var admin = Environment.GetEnvironmentVariable(DataManagerDb.Variable)!;
        var database = $"onebase_route_{Guid.NewGuid():N}"[..28];
        await using (var connection = new Npgsql.NpgsqlConnection(admin))
        {
            await connection.OpenAsync();
            await new Npgsql.NpgsqlCommand($"CREATE DATABASE \"{database}\"", connection).ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<OneBaseDbContext>()
            .UseNpgsql(new Npgsql.NpgsqlConnectionStringBuilder(admin) { Database = database }.ConnectionString).Options;
        try
        {
            await using var db = new OneBaseDbContext(options);
            await db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>().MigrateAsync(Before);
            await db.Database.ExecuteSqlRawAsync("""
                UPDATE ai."Settings" SET "RouterProvider" = 'anthropic', "RouterModel" = 'claude-haiku-5-5'  -- строку создаёт миграция перехода на Claude
                """);
            await db.Database.MigrateAsync();

            var s = await db.AiSettings.AsNoTracking().SingleAsync();
            Assert.Equal((true, AiTaskMode.On, AiTaskMode.Off, null), (s.TaskRoutingEnabled, s.RouterMode, s.ExtractMode, s.ExtractModel));

            // Без модели маршрутизации — флаг выключен, режимы «Выкл».
            await db.Database.ExecuteSqlRawAsync("""UPDATE ai."Settings" SET "RouterModel" = NULL, "RouterProvider" = NULL, "TaskRoutingEnabled" = FALSE, "RouterMode" = 'off'""");
            await db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>().MigrateAsync(Before);
            await db.Database.MigrateAsync();
            var fresh = await db.AiSettings.AsNoTracking().SingleAsync();
            Assert.Equal((false, AiTaskMode.Off), (fresh.TaskRoutingEnabled, fresh.RouterMode));
        }
        finally
        {
            Npgsql.NpgsqlConnection.ClearAllPools();
            await using var connection = new Npgsql.NpgsqlConnection(admin);
            await connection.OpenAsync();
            await new Npgsql.NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", connection).ExecuteNonQueryAsync();
        }
    }
}
