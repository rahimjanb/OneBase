using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using OneBase.AI.Agents;
using OneBase.AI.Analytik;
using OneBase.AI.Analytik.Intelligence;
using OneBase.AI.Consultant;
using OneBase.AI.Gateway;
using OneBase.AI.Grounding;
using OneBase.AI.Llm;
using OneBase.AI.Providers;
using OneBase.Application.Abstractions;
using OneBase.Application.Analytik;
using OneBase.Application.Analytik.Compute;
using OneBase.Application.Security;
using OneBase.Domain.AI;
using OneBase.Domain.Identity;
using OneBase.Infrastructure;
using OneBase.Infrastructure.Persistence;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>
/// Модель по назначению вызова (analytik.plan, agent, analytik.answer, analytik.review…) — потокобезопасно: шаги исследования идут
/// параллельно. Ответ — функция сообщений вызова (например, аналитик сначала вызывает инструмент, потом пишет итог). Calls — назначение и агент
/// каждого вызова. Нет обработчика — «AI не настроен».
/// </summary>
public sealed class PurposeModel : IAiGateway
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Func<IReadOnlyList<LlmMessage>, AiCallResult>> _handlers = new(StringComparer.Ordinal);

    public List<(string Purpose, string? Agent, IReadOnlyList<LlmMessage> Messages)> Calls { get; } = [];

    public PurposeModel Reset()
    {
        lock (_gate)
        {
            _handlers.Clear();
            Calls.Clear();
        }

        return this;
    }

    /// <summary>Обработчик назначения (purpose) или «agent:код аналитика».</summary>
    public PurposeModel On(string key, Func<IReadOnlyList<LlmMessage>, AiCallResult> handler)
    {
        lock (_gate)
        {
            _handlers[key] = handler;
        }

        return this;
    }

    public PurposeModel Text(string key, string text) => On(key, _ => Result(text));

    public static AiCallResult Result(string? text, params LlmToolCall[] calls) =>
        new(text, calls, new AiTokenUsage(100, 50), new AiModelRef("fake", "purpose"), false, 1, calls.Length > 0 ? "tool_use" : "stop");

    public int Count(string purpose)
    {
        lock (_gate)
        {
            return Calls.Count(c => c.Purpose == purpose);
        }
    }

    public Task<AiReadiness> GetReadinessAsync(CancellationToken cancellationToken = default) => Task.FromResult(new AiReadiness(true, null));

    public Task<AiCallResult> CompleteAsync(IReadOnlyList<LlmMessage> messages, IReadOnlyList<LlmToolDefinition> tools, AiCallOptions options,
        CancellationToken cancellationToken = default)
    {
        var purpose = options.Context?.Purpose ?? "chat";
        var agent = options.Context?.AgentCode;
        Func<IReadOnlyList<LlmMessage>, AiCallResult>? handler;
        lock (_gate)
        {
            Calls.Add((purpose, agent, messages.ToList()));
            handler = _handlers.GetValueOrDefault($"{purpose}:{agent}") ?? _handlers.GetValueOrDefault(purpose);
        }

        return handler is null
            ? throw new LlmNotConfiguredException($"AI не настроен (тест): нет ответа для «{purpose}».")
            : Task.FromResult(handler(messages));
    }

    public Task<(IReadOnlyList<float[]> Vectors, AiModelRef Model)> EmbedAsync(IReadOnlyList<string> inputs, AiCallContext context,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

/// <summary>Своя база исследований: настоящие сервисы OneBase и Аналитика, модель по назначению, пользователь-директор и чат Аналитика.</summary>
public sealed class IntelligenceDb : IAsyncLifetime
{
    private string? _admin;
    private string? _database;

    public ServiceProvider Services { get; private set; } = null!;
    public PurposeModel Model { get; } = new();
    public CapturingLogs Logs { get; } = new();
    public Guid UserId { get; private set; }
    public Guid ConversationId { get; private set; }

    public async Task InitializeAsync()
    {
        _admin = Environment.GetEnvironmentVariable(DataManagerDb.Variable);
        if (string.IsNullOrWhiteSpace(_admin))
        {
            return;
        }

        _database = $"onebase_ai_intel_{Guid.NewGuid():N}"[..30];
        await using (var connection = new NpgsqlConnection(_admin))
        {
            await connection.OpenAsync();
            await new NpgsqlCommand($"CREATE DATABASE \"{_database}\"", connection).ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(_admin) { Database = _database };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = builder.ConnectionString,
            ["LINKO_BASE_URL"] = "https://linko.test",
            ["LINKO_TOKEN"] = "test-token",
            ["Verifix:WorkerEnabled"] = "false",
            ["Analytik:WorkerEnabled"] = "false",
            ["Analytik:FreshWaitSeconds"] = "0",
            // Эти тесты проверяют исследование моделью (Phase 1): вопросы-шаблоны (Phase 2) не перехватываются исследованием кодом.
            ["Analytik:ResearchMode"] = "off",
            ["Ai:AnswerCache:Minutes"] = "0",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(Logs));
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton(TimeProvider.System);
        services.AddInfrastructure(config);
        services.AddAi(config);
        services.AddSingleton<IFileStorage, MemoryStorage>();
        services.AddSingleton<IAiGateway>(Model);
        Services = services.BuildServiceProvider();

        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            await db.Database.MigrateAsync();
            var role = new Role { Name = "Директор (исследования)" };
            foreach (var code in new[] { Permissions.SalesRead, Permissions.FinanceRead, Permissions.AnalytikUse })
            {
                role.Permissions.Add(new RolePermission { Code = code });
            }

            var user = new User { Login = "intel-director", FullName = "Директор" };
            user.Roles.Add(new UserRole { Role = role });
            db.Users.Add(user);
            await db.SaveChangesAsync();
            UserId = user.Id;
            var conversation = new AiConversation { UserId = user.Id, Title = "Исследование", Kind = ConversationKinds.Analytik };
            db.AiConversations.Add(conversation);
            await db.SaveChangesAsync();
            ConversationId = conversation.Id;
        }

        await AiSeeder.SeedAsync(Services);
    }

    public async Task ResetAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await db.LinkoOrderLines.Where(l => l.OrderId >= DataManagerDb.FakeLinkoIds).ExecuteDeleteAsync();
        await db.LinkoOrders.Where(o => o.Id >= DataManagerDb.FakeLinkoIds).ExecuteDeleteAsync();
        await db.SalesRegions.Where(r => r.LinkoBranchId >= 900).ExecuteDeleteAsync();
        await db.Set<OneBase.Domain.Analytik.AnalytikInvestigation>().ExecuteDeleteAsync();
        await db.Set<OneBase.Domain.Analytik.AnalytikComputation>().ExecuteDeleteAsync();
        Model.Reset();
    }

    public async Task DisposeAsync()
    {
        if (_database is null || _admin is null)
        {
            return;
        }

        await Services.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(_admin);
        await connection.OpenAsync();
        await new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)", connection).ExecuteNonQueryAsync();
    }
}

/// <summary>Правила исследования без базы: триаж, план (зависимости и волны), проверка ответа, противоречия.</summary>
public sealed class InvestigationRulesTests
{
    private static List<LlmMessage> History(params string[] questions) => questions.Select(q => new LlmMessage(LlmRole.User, q)).ToList();

    [Theory]
    [InlineData("Как продажи за октябрь?", false, false)]
    [InlineData("Сколько сырья на складе?", false, false)]
    [InlineData("Почему упали продажи в Хорезме в октябре?", true, false)]
    [InlineData("Исследуй, почему падают продажи, хватает ли сырья и закупок для производства и что делать в первую очередь", true, true)]
    [InlineData("Проверь гипотезу: продажи зависят от визитов агентов?", true, false)]
    public void Triage_sends_only_complex_questions_to_investigation(string question, bool investigate, bool deep)
    {
        var t = InvestigationTriage.Assess(question, [], "auto", 3, 6);
        Assert.Equal(investigate, t.Investigate);
        Assert.Equal(deep, t.Deep);
    }

    [Fact]
    public void Triage_respects_mode_and_inherits_intent_of_follow_up()
    {
        Assert.False(InvestigationTriage.Assess("Почему упали продажи?", [], "off", 3, 6).Investigate);
        Assert.True(InvestigationTriage.Assess("Как продажи за октябрь?", [], "always", 3, 6).Investigate);
        Assert.False(InvestigationTriage.Assess("Привет!", [], "always", 3, 6).Investigate);
        Assert.True(InvestigationTriage.Assess("а в Бухаре?", History("Почему упали продажи в Хорезме?"), "auto", 3, 6).Investigate);
    }

    [Fact]
    public void Plan_steps_keep_known_dependencies_break_cycles_and_run_in_waves()
    {
        using var doc = JsonDocument.Parse("""
            {"steps":[
              {"id":"s1","question":"Продажи по регионам","analyst":"analyst.sales"},
              {"id":"s2","question":"Группы в худшем регионе","analyst":"analyst.sales","depends_on":["s1"]},
              {"id":"s3","question":"Сырьё","analyst":"analyst.unknown","depends_on":["s4"]},
              {"id":"s4","question":"Закупки","analyst":"analyst.supply","depends_on":["s3"]},
              {"id":"s5","question":"Финансы","analyst":"analyst.finance","depends_on":["s1","nope"]}
            ]}
            """);
        var steps = InvestigationJson.Steps(doc.RootElement, new HashSet<string> { "analyst.sales", "analyst.supply", "analyst.finance", "analyst.bi" }, "analyst.bi", 8, 0);
        Assert.Equal(5, steps.Count);
        Assert.Equal("analyst.bi", steps[2].Analyst);
        Assert.Equal(["s1"], steps[4].DependsOn);
        Assert.False(steps[2].DependsOn.Contains("s4") && steps[3].DependsOn.Contains("s3"), "цикл s3 ↔ s4 разорван");
        var waves = InvestigationJson.Waves(steps);
        Assert.Contains("s1", waves[0].Select(s => s.Id));
        Assert.Contains("s2", waves[1].Select(s => s.Id));
        Assert.Equal(5, waves.Sum(w => w.Count));
        Assert.Equal(3, InvestigationJson.Steps(doc.RootElement, new HashSet<string> { "analyst.bi" }, "analyst.bi", 3, 0).Count);
    }

    private static HypothesisVerdict V(string id, string statement, string status, string cls) =>
        new(new PlannedHypothesis(id, statement, null, new HypothesisExpectation()), status, cls, 0.6, [], [], [], null, null, null, null);

    private static StepOutcome Step(string id, string status, string summary, params Metric[] metrics) =>
        new(new InvestigationStep(id, "?", "analyst.sales", [], null), status, summary, [summary], metrics, [], 1, [], 10);

    private static readonly GroundingSummary Clean = new(0, 0, [], [], false, false);

    [Fact]
    public void Reviewer_rejects_a_cause_that_the_check_rejected_and_unproven_causes()
    {
        var verdicts = new[]
        {
            V("h1", "Падение выручки дал Хорезм", HypothesisStatuses.Confirmed, EvidenceClasses.Observed),
            V("h2", "Падение выручки вызвано подорожанием", HypothesisStatuses.Rejected, EvidenceClasses.Observed),
        };
        var steps = new[] { Step("s1", "completed", "Хорезм дал главный вклад в снижение") };

        var bad = InvestigationReviewer.Check("Падение выручки вызвано подорожанием товаров.", "Почему упала выручка?", Clean, steps, verdicts, []);
        Assert.Contains(bad, i => i.Kind == "rejected_cause");

        var invented = InvestigationReviewer.Check("Продажи снизились из-за плохой погоды в регионах страны.", "Почему?", Clean, steps, verdicts, []);
        Assert.Contains(invented, i => i.Kind == "unproven_cause");

        var good = InvestigationReviewer.Check("Главный вклад в снижение дал Хорезм. Подорожание как причина не подтвердилось.", "Почему?", Clean, steps, verdicts, []);
        Assert.DoesNotContain(good, i => i.Severity == "error");
    }

    [Fact]
    public void Reviewer_requires_limitations_when_data_is_missing_and_flags_foreign_years()
    {
        var steps = new[] { Step("s1", "no_data", "Данных о сменах нет") };
        var issues = InvestigationReviewer.Check("Производство работает стабильно.", "Как производство?", Clean, steps, [], ["2026-10-01 — 2026-10-31"]);
        Assert.Contains(issues, i => i.Kind == "limitations");
        var years = InvestigationReviewer.Check("В 2024 году было так же. Данных о сменах не хватает.", "Как производство?", Clean, steps, [], ["2026-10-01"]);
        Assert.Contains(years, i => i.Kind == "period");
        Assert.DoesNotContain(years, i => i.Kind == "limitations");
    }

    [Fact]
    public void Contradictions_find_one_metric_with_different_values()
    {
        var steps = new[]
        {
            Step("s1", "completed", "a", new Metric("Выручка октябрь", "15 258 044 356", "сум")),
            Step("s2", "completed", "b", new Metric("выручка  октябрь", "14 100 000 000", "сум")),
            Step("s3", "completed", "c", new Metric("Кг октябрь", "421 653", "кг")),
        };
        var c = InvestigationReviewer.Contradictions(steps, [V("h1", "X", HypothesisStatuses.Confirmed, "observed"), V("h1b", "x", HypothesisStatuses.Rejected, "observed")]);
        Assert.Contains(c, x => x.Kind == "metric" && x.Parties.SequenceEqual(["s1", "s2"]));
        Assert.Contains(c, x => x.Kind == "hypothesis");
    }
}

/// <summary>
/// Исследование целиком на настоящей базе с миром IntelligenceWorld и моделью по назначению: план, шаги волнами, гипотезы — расчётом (статусы
/// ставит код), перепланирование, скептик, ревизор с возвратом на этап, трасса и метрики в analytik.Investigations; простой вопрос — прежним путём.
/// </summary>
public sealed class IntelligenceInvestigationTests(IntelligenceDb fixture) : IClassFixture<IntelligenceDb>
{
    private const string Oct = "\"date_from\":\"2026-10-01\",\"date_to\":\"2026-10-30\",\"compare_from\":\"2026-09-01\",\"compare_to\":\"2026-09-30\"";

    private static string Regions(IntelligenceWorld w) => JsonSerializer.Serialize(w.Regions);

    private static string Plan(IntelligenceWorld w, string extraSteps = "") => $$$"""
        {"goal":"Выяснить, почему упала выручка в октябре","depth":"standard",
         "steps":[{"id":"s1","question":"Как изменилась выручка по регионам в октябре к сентябрю?","analyst":"analyst.sales","depends_on":[]},
                  {"id":"s2","question":"Какие товарные группы дали изменение в худшем регионе?","analyst":"analyst.sales","depends_on":["s1"]}{{{extraSteps}}}],
         "hypotheses":[
          {"id":"h1","statement":"Падение выручки дал Хорезм","test":{"method":"drivers","metrics":["revenue"],"dimension":"region",{{{Oct}}},
            "filters":[{"dimension":"region","op":"in","values":{{{Regions(w)}}}}]},"expect":{"direction":"down","member":"Хорезм"}},
          {"id":"h2","statement":"Падение выручки вызвано подорожанием","test":{"method":"pvm",{{{Oct}}},
            "filters":[{"dimension":"region","op":"in","values":{{{Regions(w)}}}}]},"expect":{"effect":"price","direction":"down"}},
          {"id":"h3","statement":"Объём Хорезма упал после начала октября сильнее контрольных регионов","test":{"method":"did","metrics":["kg"],"dimension":"region",
            "date_from":"2026-09-01","date_to":"2026-10-30","filters":[{"dimension":"region","op":"in","values":{{{Regions(w)}}}}],
            "params":{"change_date":"2026-10-01","treatment":["{{{w.Treated}}}"]}},"expect":{"direction":"down"}}]}
        """;

    /// <summary>Аналитик: сначала срез выручки по регионам (настоящий инструмент), затем итог без чисел.</summary>
    private static Func<IReadOnlyList<LlmMessage>, AiCallResult> Analyst(IntelligenceWorld w, string code, string status = "completed") => messages =>
        messages.Any(m => m.Role == LlmRole.Tool) || status != "completed"
            ? PurposeModel.Result($$"""{"agent":"{{code}}","status":"{{status}}","summary":"Выручка Хорезма снизилась сильнее остальных регионов.","findings":["Хорезм — главный вклад в снижение выручки"],"metrics":[],"problems":[],"recommendations":["Проверить работу точек Хорезма"],"data_sources":[]}""")
            : PurposeModel.Result(null, new LlmToolCall("q1", AnalyticsQueryTool.ToolName, JsonSerializer.SerializeToElement(new
            {
                metrics = new[] { "revenue" }, dimensions = new[] { "region" }, date_from = "2026-10-01", date_to = "2026-10-30", compare = "previous",
                filters = new[] { new { dimension = "region", op = "in", values = w.Regions } },
            })));

    private async Task<(ConsultantAnswer Answer, OneBase.Domain.Analytik.AnalytikInvestigation? Record)> AskAsync(string question)
    {
        using var scope = fixture.Services.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<AnalytikEngine>();
        var answer = await engine.AnswerAsync(new ConsultantTurn(fixture.UserId, fixture.ConversationId, question, []), _ => Task.CompletedTask, default);
        var db = scope.ServiceProvider.GetRequiredService<IAnalytikDb>();
        var record = await db.Investigations.AsNoTracking().OrderByDescending(i => i.StartedAt).FirstOrDefaultAsync();
        return (answer, record);
    }

    [PostgresFact]
    public async Task Complex_question_is_investigated_hypotheses_are_judged_by_code_and_reviewer_returns_a_rejected_cause()
    {
        await fixture.ResetAsync();
        var w = await IntelligenceWorld.SeedAsync(fixture.Services, fixture.ResetAsync);
        var answers = new Queue<string>(
        [
            "Выручка в октябре снизилась, главный вклад дал Хорезм. Падение выручки вызвано подорожанием товаров.",
            "Выручка в октябре снизилась: главный вклад дал Хорезм, это подтверждено сравнением с другими регионами. Подорожание как причина не подтвердилось.",
        ]);
        fixture.Model
            .Text("analytik.plan", Plan(w))
            .On("agent", Analyst(w, "analyst.sales"))
            .On("analytik.answer", _ => PurposeModel.Result(answers.Dequeue()))
            .On("analytik.review.revise", _ => PurposeModel.Result(answers.Dequeue()));

        var (answer, record) = await AskAsync("Почему упала выручка в октябре?");

        Assert.NotNull(record);
        Assert.Equal(record!.Id, answer.Details.InvestigationId);
        Assert.Equal(0, fixture.Model.Count("consultant.route")); // планировщик заменил маршрутизатор
        Assert.Equal(1, fixture.Model.Count("analytik.plan"));
        Assert.Equal(2, record.StepCount);
        Assert.Equal(InvestigationStatuses.Completed, record.Status);

        // Шаг s2 зависит от s1: его задача получила вывод s1.
        var stepCalls = fixture.Model.Calls.Where(c => c.Purpose == "agent").ToList();
        Assert.Contains(stepCalls, c => c.Messages.Any(m => m.Role == LlmRole.User && m.Content.Contains("s1: Выручка Хорезма снизилась")));

        // Статусы гипотез — расчётом.
        using var hypotheses = JsonDocument.Parse(record.Hypotheses!);
        var byId = hypotheses.RootElement.EnumerateArray().ToDictionary(h => h.GetProperty("id").GetString()!);
        Assert.Equal(HypothesisStatuses.Confirmed, byId["h1"].GetProperty("status").GetString());
        Assert.Equal(HypothesisStatuses.Rejected, byId["h2"].GetProperty("status").GetString());
        Assert.Equal(HypothesisStatuses.Confirmed, byId["h3"].GetProperty("status").GetString());
        Assert.Equal(EvidenceClasses.TestedCausal, byId["h3"].GetProperty("evidenceClass").GetString());

        // Ревизор вернул ответ: причиной названа отклонённая гипотеза → ответ переписан.
        Assert.Equal(1, record.ReviewRejections);
        Assert.Contains("rejected_cause", record.Reviews);
        Assert.DoesNotContain("вызвано подорожанием товаров", answer.Content); // первая версия ответа; формулировка гипотезы — в таблице
        Assert.Contains("**Проверка гипотез**", answer.Content);
        Assert.Contains("отклонена", answer.Content);
        Assert.Contains($"/analytik/investigations/{record.Id}", answer.Content);

        // Трасса, роли, метрики; расчёты гипотез — в журнале с id исследования.
        Assert.Contains("\"plan\"", record.Trace);
        Assert.Contains(InvestigationRoles.Statistician, record.Roles);
        Assert.True(record.LlmCalls >= 5, $"вызовов модели: {record.LlmCalls}"); // план, два ответа (до и после проверки), два шага
        using var scope = fixture.Services.CreateScope();
        var all = await scope.ServiceProvider.GetRequiredService<IAnalytikDb>().Computations.AsNoTracking()
            .Select(c => new { c.InvestigationId, c.Origin, c.Method, c.Status }).ToListAsync();
        Assert.True(all.Count(c => c.InvestigationId == record.Id) == 3, string.Join("; ", all.Select(c => $"{c.Origin}/{c.Method}/{c.Status}/{c.InvestigationId}")) + $" — исследование {record.Id}. Журнал: " + string.Join(" | ", fixture.Logs.Entries.Take(8)));
    }

    [PostgresFact]
    public async Task Quality_report_shows_investigations_hypotheses_review_issues_and_weak_spots()
    {
        await fixture.ResetAsync();
        var w = await IntelligenceWorld.SeedAsync(fixture.Services, fixture.ResetAsync);
        var answers = new Queue<string>(
        [
            "Падение выручки вызвано подорожанием товаров.",
            "Главный вклад в снижение дал Хорезм. Подорожание как причина не подтвердилось.",
        ]);
        fixture.Model
            .Text("analytik.plan", Plan(w))
            .On("agent", Analyst(w, "analyst.sales"))
            .On("analytik.answer", _ => PurposeModel.Result(answers.Dequeue()))
            .On("analytik.review.revise", _ => PurposeModel.Result(answers.Dequeue()));
        await AskAsync("Почему упала выручка в октябре?");

        using var scope = fixture.Services.CreateScope();
        // Два одинаковых отказа срезов (разные значения в тексте) складываются в одну причину.
        var engine = scope.ServiceProvider.GetRequiredService<OneBase.Application.Analytik.Semantic.SemanticEngine>();
        var principal = new OneBase.Application.Analytik.Query.AnalytikPrincipal(fixture.UserId, new HashSet<string> { Permissions.SalesRead }, OneBase.Application.Analytik.Query.SalesRowScope.Organization);
        foreach (var bad in new[] { "revenu", "kgg" })
        {
            await Assert.ThrowsAsync<OneBase.Application.Analytik.Semantic.AnalyticsQueryException>(() => engine.RunAsync(
                new OneBase.Application.Analytik.Semantic.AnalyticsQuery { Metrics = [bad] }, new OneBase.Application.Analytik.Query.QueryCaller(fixture.UserId, "api"), default, principal));
        }

        var report = await scope.ServiceProvider.GetRequiredService<QualityService>().ReportAsync(30, default);
        Assert.Equal(1, report.Investigations.Total);
        Assert.Equal(1, report.Investigations.ReviewRejectionRate);
        Assert.Equal(3, report.Hypotheses.Total);
        Assert.Contains(report.Hypotheses.ByStatus, x => x.Key == HypothesisStatuses.Rejected && x.Count == 1);
        Assert.Contains(report.ReviewIssues, x => x.Key == "rejected_cause");
        Assert.True(report.Computations.Total >= 3);
        Assert.Contains(report.Queries.TopErrors, e => e.Count == 2 && e.Key.StartsWith("Показатель «…» не найден"));
        Assert.Contains(report.WeakSpots, s => s.Area == "Планировщик срезов" && s.Count == 2);
    }

    [PostgresFact]
    public async Task Simple_question_keeps_the_fast_path_without_planner()
    {
        await fixture.ResetAsync();
        var w = await IntelligenceWorld.SeedAsync(fixture.Services, fixture.ResetAsync);
        fixture.Model
            .Text("consultant.route", """{"agents":["analyst.sales"],"tasks":{"analyst.sales":"Продажи за октябрь"},"reason":"продажи"}""")
            .On("agent", Analyst(w, "analyst.sales"))
            .Text("analytik.answer", "Выручка в октябре снизилась сильнее всего в Хорезме.");

        var (answer, record) = await AskAsync("Как продажи за октябрь?");
        Assert.Null(record);
        Assert.Null(answer.Details.InvestigationId);
        Assert.Equal(0, fixture.Model.Count("analytik.plan"));
        Assert.Equal(1, fixture.Model.Count("consultant.route"));
    }

    [PostgresFact]
    public async Task Step_without_data_triggers_replanning_with_an_alternative_source()
    {
        await fixture.ResetAsync();
        var w = await IntelligenceWorld.SeedAsync(fixture.Services, fixture.ResetAsync);
        var extra = """,{"id":"s3","question":"Как работала команда Хорезма (посещаемость)?","analyst":"analyst.hr","depends_on":[]}""";
        fixture.Model
            .Text("analytik.plan", Plan(w, extra))
            .On("agent", Analyst(w, "analyst.sales"))
            .On("agent:analyst.hr", Analyst(w, "analyst.hr", "no_data"))
            .On("agent:analyst.bi", Analyst(w, "analyst.bi"))
            .Text("analytik.replan", """{"stop":false,"reason":"нет данных HR","steps":[{"id":"r1","question":"Как изменилось число активных точек Хорезма?","analyst":"analyst.bi"}],"hypotheses":[]}""")
            .Text("analytik.answer", "Выручка снизилась, главный вклад дал Хорезм. Данных о посещаемости команды не хватает.");

        var (_, record) = await AskAsync("Почему упала выручка в октябре?");
        Assert.Equal(1, record!.Replans);
        Assert.Equal(4, record.StepCount);
        Assert.Contains("\"r1\"", record.Steps);
        Assert.Contains("replan", record.Trace);
        Assert.Contains("analyst.hr", record.Limitations);
    }

    [PostgresFact]
    public async Task Deep_investigation_adds_skeptic_alternatives_and_independent_review()
    {
        await fixture.ResetAsync();
        var w = await IntelligenceWorld.SeedAsync(fixture.Services, fixture.ResetAsync);
        var skeptic = $$$"""
            {"objections":["Падение может быть сезонным"],
             "alternatives":[{"id":"k1","statement":"Выручка Самарканда тоже снижалась","test":{"method":"drivers","metrics":["revenue"],"dimension":"region",{{{Oct}}},
              "filters":[{"dimension":"region","op":"in","values":{{{Regions(w)}}}}]},"expect":{"direction":"down","member":"Самарканд"}}]}
            """;
        fixture.Model
            .Text("analytik.plan", Plan(w))
            .On("agent", Analyst(w, "analyst.sales"))
            .Text("analytik.skeptic", skeptic)
            .Text("analytik.review", """{"verdict":"approve","issues":[]}""")
            .Text("analytik.answer", "Главный вклад в снижение выручки дал Хорезм; это подтверждено сравнением с другими регионами. Сезонность не подтвердилась.");

        var (answer, record) = await AskAsync("Исследуй, почему падают продажи, хватает ли сырья и закупок для производства и что делать в первую очередь");
        Assert.Equal(1, fixture.Model.Count("analytik.skeptic"));
        Assert.Equal(1, fixture.Model.Count("analytik.review"));
        Assert.Contains(InvestigationRoles.Skeptic, record!.Roles);
        using var hypotheses = JsonDocument.Parse(record.Hypotheses!);
        var k1 = hypotheses.RootElement.EnumerateArray().Single(h => h.GetProperty("id").GetString() == "k1");
        Assert.Equal("skeptic", k1.GetProperty("origin").GetString());
        Assert.Equal(HypothesisStatuses.Rejected, k1.GetProperty("status").GetString()); // Самарканд вырос (подорожание Б)
        Assert.Contains("сезонным", record.Reviews);
        Assert.Equal(2, record.Depth);
        Assert.Contains("Выручка Самарканда тоже снижалась", answer.Content);
    }

    [PostgresFact]
    public async Task Without_a_planner_the_investigation_still_answers_on_a_fallback_plan()
    {
        await fixture.ResetAsync();
        var w = await IntelligenceWorld.SeedAsync(fixture.Services, fixture.ResetAsync);
        fixture.Model
            .Text("analytik.plan", "Не могу составить план.")
            .On("agent", Analyst(w, "analyst.sales"))
            .Text("analytik.answer", "Главный вклад в снижение выручки дал Хорезм. План исследования составлен без модели — ограничение.");

        var (answer, record) = await AskAsync("Почему упали продажи в октябре?");
        Assert.NotNull(record);
        using (var plan = JsonDocument.Parse(record!.Plan!))
        {
            Assert.True(plan.RootElement.GetProperty("fallback").GetBoolean());
        }

        Assert.Contains("без модели", record.Limitations);
        Assert.Equal(InvestigationStatuses.Completed, record.Status);
        Assert.Contains("Хорезм", answer.Content);
    }
}
