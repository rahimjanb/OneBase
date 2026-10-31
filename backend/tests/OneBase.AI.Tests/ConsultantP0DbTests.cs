using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OneBase.AI.Agents;
using OneBase.AI.Consultant;
using OneBase.AI.Gateway;
using OneBase.AI.Knowledge;
using OneBase.AI.Llm;
using OneBase.AI.Memory;
using OneBase.AI.Providers;
using OneBase.AI.Tools;
using OneBase.Application.Abstractions;
using OneBase.Application.Security;
using OneBase.Domain.AI;
using OneBase.Domain.Identity;
using OneBase.Infrastructure;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// Консультант на настоящем PostgreSQL (ONEBASE_TEST_POSTGRES, иначе пропускается) со сценарной моделью вместо провайдера (без сети и платных
/// вызовов) и двумя тестовыми инструментами: один всегда падает, другой отдаёт данные. Проверяется P0 аудита: сбой инструмента не роняет
/// сотрудника, полученные данные не теряются, сбой не выдаётся за «данных нет», пользователь без прав получает «нет доступа», прошлый отказ не
/// записывается в память.
/// </summary>
public class ConsultantP0DbTests(ConsultantDb fixture) : IClassFixture<ConsultantDb>
{
    private const string FinalJson =
        """{"agent":"sales","status":"completed","summary":"Факт продаж — 12 345 кг.","findings":["Факт продаж — 12 345 кг"],"metrics":[],"problems":[],"recommendations":[],"data_sources":[]}""";

    private static LlmToolCall Call(string id, string name) => new(id, name, JsonDocument.Parse("{}").RootElement.Clone());

    private async Task<AgentRun> RunAsync(string agent, Guid userId, string task = "Какие продажи в сентябре?")
    {
        using var scope = fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AgentRunner>().RunAsync(new AgentTask(agent, task, userId), null, CancellationToken.None);
    }

    [PostgresFact]
    public async Task Failing_tool_does_not_stop_the_agent_and_its_error_reaches_the_model_without_secrets()
    {
        var model = fixture.Model.Reset();
        model.Then([Call("1", ConsultantDb.Broken), Call("2", ConsultantDb.Echo)]).Then(FinalJson);

        var run = await RunAsync("sales", fixture.AdminId);

        Assert.Equal("completed", run.Result.Status);
        Assert.Contains(ConsultantDb.Echo, run.Result.ToolsUsed);
        var issue = Assert.Single(run.Result.ToolErrors!);
        Assert.Equal((ConsultantDb.Broken, ToolFailures.ToolError), (issue.Tool, issue.Kind));

        // Второй ход модели видит и сбой первого инструмента (структурно, без текста исключения), и данные второго.
        var toolMessages = model.Calls[1].Where(m => m.Role == LlmRole.Tool).ToList();
        var broken = Assert.Single(toolMessages, m => m.ToolCallId == "1").Content;
        Assert.StartsWith("ОШИБКА: ", broken);
        Assert.Contains("\"kind\":\"tool_error\"", broken);
        Assert.DoesNotContain("s3cr3t", broken);
        Assert.Contains("12345", Assert.Single(toolMessages, m => m.ToolCallId == "2").Content);

        // Исключение — в журнале аудита, без служебных подробностей.
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var audit = await db.AuditLogs.AsNoTracking().Where(a => a.Action == "ai.tool.failed" && a.EntityId == ConsultantDb.Broken).ToListAsync();
        Assert.NotEmpty(audit);
        Assert.All(audit, a => Assert.DoesNotContain("s3cr3t", a.Data!));
        Assert.All(audit, a => Assert.Contains("InvalidOperationException", a.Data!));
    }

    [PostgresFact]
    public async Task Model_failure_after_data_returns_partial_with_that_data()
    {
        var model = fixture.Model.Reset();
        model.Then([Call("1", ConsultantDb.Echo)]).Fail(new AiProviderException("anthropic", 529, "Модель перегружена, попробуйте позже.", true));

        var run = await RunAsync("sales", fixture.AdminId);

        Assert.Equal("partial", run.Result.Status);
        Assert.Equal("interrupted", run.Result.Reason);
        Assert.Contains(run.Result.Findings, f => f.Contains("12345"));
        Assert.Contains("перегружена", run.Result.Error);
        Assert.NotNull(run.Evidence);
    }

    [PostgresFact]
    public async Task Tool_errors_are_not_reported_as_no_data()
    {
        var model = fixture.Model.Reset();
        model.Then([Call("1", ConsultantDb.Broken)]).Then("""{"agent":"sales","status":"no_data","summary":"Данных о продажах нет."}""");

        var run = await RunAsync("sales", fixture.AdminId);

        Assert.Equal("failed", run.Result.Status);
        Assert.Equal("tool_error", run.Result.Reason);
        Assert.DoesNotContain("нет данных", run.Result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [PostgresFact]
    public async Task Agent_without_tools_for_the_user_is_forbidden_without_calling_the_model()
    {
        var model = fixture.Model.Reset();

        // У Marketing AI нет обязательного права — но у пользователя без прав нет ни одного его инструмента данных.
        var run = await RunAsync("marketing", fixture.NoAccessId, "Сколько потратили на рекламу?");

        Assert.Equal("forbidden", run.Result.Status);
        Assert.Equal("no_access", run.Result.Reason);
        Assert.Empty(model.Calls);
    }

    [PostgresFact]
    public async Task User_without_permissions_gets_no_access_instead_of_no_data()
    {
        var model = fixture.Model.Reset();
        model.Then("У вас нет доступа к данным продаж — обратитесь к администратору.");

        using var scope = fixture.Services.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<IConsultantEngine>();
        var answer = await engine.AnswerAsync(new ConsultantTurn(fixture.NoAccessId, Guid.NewGuid(), "Какие продажи в сентябре?", []), _ => Task.CompletedTask,
            CancellationToken.None);

        var sales = Assert.Single(answer.Details.Agents);
        Assert.Equal(("sales", "forbidden", "no_access"), (sales.Agent, sales.Status, sales.Reason));
        // Модель вызвана один раз — только итоговый ответ: ни маршрутизатор, ни сотрудники без доступа не запускались.
        var compose = Assert.Single(model.Calls);
        Assert.Contains("\"outcome\":\"forbidden\"", compose[1].Content);
        Assert.DoesNotContain("Если сотрудник вернул no_data или ошибку", compose[1].Content);
        Assert.Contains("tool_error — технический сбой источника", compose[0].Content);
    }

    [PostgresFact]
    public async Task Data_question_reaches_an_agent_when_the_router_returns_nobody()
    {
        var model = fixture.Model.Reset();
        model.Then("""{"agents":[],"reason":"общий вопрос"}""")
            .Then("""{"agent":"sales","status":"completed","summary":"Проверено по данным продаж.","findings":[]}""")
            .Then("План по Хорезму проверен.");

        using var scope = fixture.Services.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<IConsultantEngine>();
        var answer = await engine.AnswerAsync(new ConsultantTurn(fixture.AdminId, Guid.NewGuid(), "Выполнение плана Хорезм сентябрь", []), _ => Task.CompletedTask,
            CancellationToken.None);

        Assert.Equal("sales", Assert.Single(answer.Details.Agents).Agent);
        Assert.Equal("Вопрос о данных компании — ответ только по данным OneBase.", answer.Details.RoutingReason);
    }

    [PostgresFact]
    public async Task Earlier_no_data_does_not_become_a_remembered_fact()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var conversation = new AiConversation { UserId = fixture.AdminId, Title = "Память" };
        db.AiConversations.Add(conversation);
        // Запись «данных нет», сохранённая прежней версией, — её больше не показывают маршрутизатору и сотрудникам, но и не удаляют.
        db.AiMemories.Add(new AiMemory
        {
            Kind = AiMemoryKind.Analytical, UserId = fixture.AdminId, AgentCode = "sales", ConversationId = conversation.Id, Topic = "Остатки помадки",
            Content = "Данных об остатках нет.", Data = """{"agent":"sales","status":"no_data","summary":"Данных об остатках нет."}""",
        });
        await db.SaveChangesAsync();

        var memory = scope.ServiceProvider.GetRequiredService<AiMemoryStore>();
        AgentResult Result(string agent, string status, string summary) => new(agent, agent, status, summary, [], [], [], [], [], []);
        await memory.SaveAsync(fixture.AdminId, conversation.Id, new Dictionary<string, string>(), "Остатки и явка",
            [Result("sales", "no_data", "Данных об остатках нет."), Result("hr", "completed", "Явка — 82,6%.")], CancellationToken.None);

        var notes = await memory.ConversationAsync(fixture.AdminId, conversation.Id, ["sales", "hr"], 8, CancellationToken.None);

        Assert.Equal(["hr"], notes.Select(n => n.AgentCode));
        Assert.Equal(3, await db.AiMemories.CountAsync(m => m.ConversationId == conversation.Id));
    }
}

/// <summary>Инструмент, который всегда падает с исключением, в тексте которого — служебные подробности.</summary>
internal sealed class BrokenTestTool : ITool
{
    public string Name => ConsultantDb.Broken;
    public string Title => "Сломанный источник (тест)";
    public string Description => "Тестовый инструмент: всегда падает.";
    public JsonElement InputSchema { get; } = JsonDocument.Parse("""{"type":"object","properties":{}}""").RootElement.Clone();
    public bool IsCritical => false;
    public string? RequiredPermission => Permissions.SalesRead;
    public string? Source => KnowledgeSources.SalesSecondary;

    public Task<ToolResult> ExecuteAsync(ToolContext context, JsonElement arguments, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Npgsql: Host=db;Username=onebase;Password=s3cr3t — connection refused");
}

/// <summary>Инструмент с данными продаж.</summary>
internal sealed class EchoTestTool : ITool
{
    public string Name => ConsultantDb.Echo;
    public string Title => "Продажи (тест)";
    public string Description => "Тестовый инструмент: факт продаж.";
    public JsonElement InputSchema { get; } = JsonDocument.Parse("""{"type":"object","properties":{}}""").RootElement.Clone();
    public bool IsCritical => false;
    public string? RequiredPermission => Permissions.SalesRead;
    public string? Source => KnowledgeSources.SalesSecondary;

    public Task<ToolResult> ExecuteAsync(ToolContext context, JsonElement arguments, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ToolResult(true, """{"factKg":12345,"region":"Хоразм"}""", null, [new DataSource("Продажи (тест)", "сентябрь 2026")]));
}

/// <summary>
/// Модель по сценарию: каждый вызов берёт следующий шаг — текст, вызовы инструментов, вызовы по уже полученным результатам или исключение.
/// Calls — сообщения каждого вызова. Шаги кончились — «AI не настроен» (как без ключа провайдера).
/// </summary>
public sealed class ScriptedConsultantModel : IAiGateway
{
    private readonly Queue<Func<IReadOnlyList<LlmMessage>, AiCallResult>> _steps = new();
    private readonly object _gate = new();

    public List<IReadOnlyList<LlmMessage>> Calls { get; } = [];

    /// <summary>Параметры каждого вызова (лимит длины ответа, разрешены ли инструменты).</summary>
    public List<AiCallOptions> Options { get; } = [];

    public ScriptedConsultantModel Reset()
    {
        lock (_gate)
        {
            _steps.Clear();
            Calls.Clear();
            Options.Clear();
        }

        return this;
    }

    public ScriptedConsultantModel Then(string text) => Add(() => Result(text, []));

    public ScriptedConsultantModel Then(IReadOnlyList<LlmToolCall> calls) => Add(() => Result(null, calls));

    public ScriptedConsultantModel Fail(Exception error) => Add(() => throw error);

    /// <summary>Ответ, обрезанный лимитом длины (stop_reason max_tokens): текст недописан.</summary>
    public ScriptedConsultantModel ThenTruncated(string text) =>
        Add(() => new AiCallResult(text, [], new AiTokenUsage(100, 50), new AiModelRef("fake", "scripted"), false, 1, "max_tokens"));

    /// <summary>Шаг по сообщениям вызова (например, следующая страница по прошлому результату): вызовы инструментов, а null — текст final.</summary>
    public ScriptedConsultantModel Then(Func<IReadOnlyList<LlmMessage>, IReadOnlyList<LlmToolCall>?> next, string final) =>
        Add(messages => next(messages) is { } calls ? Result(null, calls) : Result(final, []));

    private ScriptedConsultantModel Add(Func<AiCallResult> step) => Add(_ => step());

    private ScriptedConsultantModel Add(Func<IReadOnlyList<LlmMessage>, AiCallResult> step)
    {
        lock (_gate)
        {
            _steps.Enqueue(step);
        }

        return this;
    }

    private static AiCallResult Result(string? text, IReadOnlyList<LlmToolCall> calls) =>
        new(text, calls, new AiTokenUsage(100, 50), new AiModelRef("fake", "scripted"), false, 1, calls.Count > 0 ? "tool_use" : "stop");

    public Task<AiReadiness> GetReadinessAsync(CancellationToken cancellationToken = default) => Task.FromResult(new AiReadiness(true, null));

    public Task<AiCallResult> CompleteAsync(IReadOnlyList<LlmMessage> messages, IReadOnlyList<LlmToolDefinition> tools, AiCallOptions options,
        CancellationToken cancellationToken = default)
    {
        Func<IReadOnlyList<LlmMessage>, AiCallResult> step;
        var copy = messages.ToList();
        lock (_gate)
        {
            Calls.Add(copy);
            Options.Add(options);
            if (_steps.Count == 0)
            {
                throw new LlmNotConfiguredException("AI не настроен (тест): сценарий модели закончился.");
            }

            step = _steps.Dequeue();
        }

        return Task.FromResult(step(copy));
    }

    public Task<(IReadOnlyList<float[]> Vectors, AiModelRef Model)> EmbedAsync(IReadOnlyList<string> inputs, AiCallContext context,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

/// <summary>Своя база для тестов консультанта: настоящие сервисы OneBase, сценарная модель, тестовые инструменты, пользователи с правами и без.</summary>
public sealed class ConsultantDb : IAsyncLifetime
{
    public const string Broken = "zz_broken_tool";
    public const string Echo = "zz_sales_echo";
    public const string Rows = RowsTestTool.ToolName;

    private string? _admin;
    private string? _database;

    public ServiceProvider Services { get; private set; } = null!;
    public ScriptedConsultantModel Model { get; } = new();
    public Guid AdminId { get; private set; }
    public Guid NoAccessId { get; private set; }

    public async Task InitializeAsync()
    {
        _admin = Environment.GetEnvironmentVariable(DataManagerDb.Variable);
        if (string.IsNullOrWhiteSpace(_admin))
        {
            return;
        }

        _database = $"onebase_ai_p0_{Guid.NewGuid():N}"[..28];
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
            // Кэш ответов не нужен: тесты вызывают движок напрямую.
            ["Ai:AnswerCache:Minutes"] = "0",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton(TimeProvider.System);
        services.AddInfrastructure(config);
        services.AddAi(config);
        services.AddSingleton<IFileStorage, MemoryStorage>();
        services.AddSingleton<IAiGateway>(Model);
        services.AddScoped<ITool, BrokenTestTool>();
        services.AddScoped<ITool, EchoTestTool>();
        services.AddScoped<ITool, RowsTestTool>();
        Services = services.BuildServiceProvider();

        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            await db.Database.MigrateAsync();

            var admin = new Role { Name = "Админ консультанта" };
            foreach (var code in new[] { Permissions.SalesRead, Permissions.FinanceRead, Permissions.HrRead })
            {
                admin.Permissions.Add(new RolePermission { Code = code });
            }

            var adminUser = new User { Login = "ai-admin", FullName = "Администратор" };
            adminUser.Roles.Add(new UserRole { Role = admin });
            // Без единого права на данные.
            var noAccess = new User { Login = "ai-no-access", FullName = "Без доступа" };
            db.Users.AddRange(adminUser, noAccess);
            await db.SaveChangesAsync();
            AdminId = adminUser.Id;
            NoAccessId = noAccess.Id;
        }

        await AiSeeder.SeedAsync(Services);
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
