using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Consultant;
using OneBase.AI.Grounding;
using OneBase.AI.Providers;
using OneBase.AI.Reports;
using OneBase.AI.Tools;
using OneBase.Application.Sales;
using OneBase.Domain.Identity;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>Кэш ответов консультанта: что можно повторить, нормализация вопроса.</summary>
public class AnswerCacheRulesTests
{
    private static AgentResult Agent(params string[] tools) =>
        new("sales", "Продажи", "completed", "Продажи за октябрь — 1 400 кг.", ["1 400 кг"], [], [], [], [], tools);

    private static ConsultantDetails Details(params AgentResult[] agents) => new(agents, [], "anthropic/claude-opus-5-5", false);

    private static readonly IToolRegistry Tools = new FakeRegistry(
        new Tool("get_sales", "sales.secondary"), new Tool("get_visits", "field.sales"), new Tool("get_department_tasks", "work.tasks"),
        new Tool("get_telegram", "system.telegram"), new Tool("delegate_to_agent", null));

    [Fact]
    public void Answer_from_synchronized_sales_data_can_be_repeated()
    {
        Assert.True(ConsultantAnswerCache.Cacheable(Details(Agent("get_sales", "get_visits", Calculator.ToolName, SourceOfTruthTool.ToolName)), Tools));
    }

    [Fact]
    public void Answer_that_can_change_without_a_sync_is_not_repeated()
    {
        Assert.False(ConsultantAnswerCache.Cacheable(Details(), Tools)); // без сотрудников — разговор, не данные
        Assert.False(ConsultantAnswerCache.Cacheable(Details(Agent("get_sales", "get_department_tasks")), Tools)); // задачи меняются в OneBase
        Assert.False(ConsultantAnswerCache.Cacheable(Details(Agent("get_telegram")), Tools));
        Assert.False(ConsultantAnswerCache.Cacheable(Details(Agent("delegate_to_agent")), Tools)); // чьи данные — неизвестно
        Assert.False(ConsultantAnswerCache.Cacheable(Details(Agent("unknown_tool")), Tools));
        Assert.False(ConsultantAnswerCache.Cacheable(Details(Agent("get_sales") with { Unverified = ["1 500 кг"] }), Tools));
        Assert.False(ConsultantAnswerCache.Cacheable(Details(Agent("get_sales") with { Status = "failed", Error = "Превышен лимит шагов" }), Tools));
        Assert.False(ConsultantAnswerCache.Cacheable(Details(Agent("get_sales")) with { Reports = [new ReportRef(Guid.NewGuid(), "Отчёт", 1, 10)] }, Tools));
        Assert.False(ConsultantAnswerCache.Cacheable(
            Details(Agent("get_sales")) with { Grounding = new GroundingSummary(3, 2, ["1 500"], [], true, true) }, Tools));
    }

    [Fact]
    public void Question_is_compared_without_case_spaces_and_final_punctuation()
    {
        Assert.Equal("как продажи сегодня", ConsultantAnswerCache.Normalize("  Как  продажи\nсегодня?? "));
        Assert.Equal(ConsultantAnswerCache.Normalize("Ещё раз план"), ConsultantAnswerCache.Normalize("еще раз план."));
    }

    private sealed class Tool(string name, string? source) : ITool
    {
        public string Name => name;
        public string Title => name;
        public string Description => name;
        public string? Source => source;
        public System.Text.Json.JsonElement InputSchema => System.Text.Json.JsonSerializer.SerializeToElement(new { type = "object" });
        public bool IsCritical => false;

        public Task<ToolResult> ExecuteAsync(ToolContext context, System.Text.Json.JsonElement arguments, CancellationToken cancellationToken = default) =>
            Task.FromResult(ToolResult.Ok("{}"));
    }

    private sealed class FakeRegistry(params ITool[] tools) : IToolRegistry
    {
        public IReadOnlyCollection<ITool> All => tools;
        public ITool? Find(string name) => tools.FirstOrDefault(t => t.Name == name);
    }
}

/// <summary>Кэш ответов в работе чата: повтор вопроса — без модели; другой пользователь, продолжение разговора, новые данные — заново.</summary>
public class AnswerCacheDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    [PostgresFact]
    public async Task Same_first_question_is_answered_from_cache_until_data_changes_and_never_across_users()
    {
        var engine = fixture.Consultant;
        engine.Answer = turn => new ConsultantAnswer($"Продажи за октябрь — 1 400 кг ({turn.Question}).",
            new ConsultantDetails([new AgentResult("sales", "Продажи", "completed", "1 400 кг", ["1 400 кг"], [], [], [], [], ["get_sales"])], [],
                "anthropic/claude-opus-5-5", false),
            new AiTokenUsage(1_000, 200, CacheReadTokens: 5_000));
        Guid otherUser;
        using (var seed = fixture.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var other = new User { Login = "answer-cache-other", FullName = "Другой пользователь" };
            db.Users.Add(other);
            await db.SaveChangesAsync();
            otherUser = other.Id;
        }

        async Task<ConsultantReply> AskAsync(Guid userId, string question, Guid? conversationId = null)
        {
            using var scope = fixture.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ConsultantChatService>()
                .AskAsync(userId, conversationId, question, new ConsultantChannel("telegram", null, null, null, false), CancellationToken.None);
        }

        var before = engine.Turns.Count;
        var first = await AskAsync(fixture.AdminId, "Как продажи за октябрь?");
        Assert.Equal(before + 1, engine.Turns.Count);
        Assert.Equal(6_000, first.Usage.InputTokens); // в сообщении — весь промпт, включая кэш промпта

        // Тот же вопрос (другой регистр и знаки) в новом чате — из кэша: модель не вызывается, ответ тот же.
        var repeat = await AskAsync(fixture.AdminId, "как продажи за октябрь");
        Assert.Equal(before + 1, engine.Turns.Count);
        Assert.Equal(first.Content, repeat.Content);
        Assert.True(repeat.Ok);
        Assert.Equal(["get_sales"], repeat.Details!.Agents.Single().ToolsUsed);

        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var log = await db.AiAuditLogs.AsNoTracking().SingleAsync(a => a.ConversationId == repeat.ConversationId);
            Assert.True(log.FromCache);
            Assert.Equal((0, 0, 0m), (log.InputTokens, log.OutputTokens, log.CostUsd ?? -1));
            Assert.False((await db.AiAuditLogs.AsNoTracking().SingleAsync(a => a.ConversationId == first.ConversationId)).FromCache);
        }

        // Другой пользователь — свои права и данные: ответ не берётся из чужого кэша.
        await AskAsync(otherUser, "Как продажи за октябрь?");
        Assert.Equal(before + 2, engine.Turns.Count);

        // Продолжение разговора зависит от истории — не кэшируется.
        await AskAsync(fixture.AdminId, "Как продажи за октябрь?", repeat.ConversationId);
        Assert.Equal(before + 3, engine.Turns.Count);

        // Данные обновились (правка, загрузка, синхронизация сбрасывают кэш продаж) — ответ строится заново.
        fixture.Services.GetRequiredService<SalesCacheSignal>().Invalidate();
        await AskAsync(fixture.AdminId, "Как продажи за октябрь?");
        Assert.Equal(before + 4, engine.Turns.Count);

        // Ответ по задачам отделов (меняются в OneBase без синхронизации) — каждый раз заново.
        engine.Answer = turn => new ConsultantAnswer("Задач 5.",
            new ConsultantDetails([new AgentResult("work", "Задачи", "completed", "5", [], [], [], [], [], ["get_department_tasks"])], [], "m", false),
            new AiTokenUsage(10, 5));
        await AskAsync(fixture.AdminId, "Сколько задач у отдела?");
        await AskAsync(fixture.AdminId, "Сколько задач у отдела?");
        Assert.Equal(before + 6, engine.Turns.Count);
        engine.Answer = null;
    }
}
