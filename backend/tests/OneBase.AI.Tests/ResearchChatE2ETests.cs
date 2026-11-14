using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Agents;
using OneBase.AI.Analytik;
using OneBase.AI.Consultant;
using OneBase.AI.Llm;
using OneBase.Application.Analytik;
using OneBase.Application.Analytik.Research;

namespace OneBase.AI.Tests;

/// <summary>
/// Сквозная цепочка чата на PostgreSQL с моделью-сценарием (без сети и платных вызовов): маршрутизатор → оркестратор → AgentRunner → инструменты
/// (analytics_research, analytik_compute, analytics_query) → движок срезов и Statistics Engine → проверка чисел → итоговый ответ; и чат Аналитика
/// (исследование по шаблону, ответ одним вызовом модели). Модель-сценарий заменяет только решения модели — всё остальное настоящее.
/// </summary>
public class ResearchChatE2ETests(ResearchDb fx) : IClassFixture<ResearchDb>
{
    private const string FinalJson =
        """{"agent":"sales","status":"completed","summary":"Исследование выполнено, выводы по разделам — в результате инструмента.","findings":["Выводы получены из исследования"],"metrics":[],"problems":[],"recommendations":[],"data_sources":[]}""";

    private const string Route = """{"agents":["sales"],"tasks":{"sales":"Исследуй вопрос по данным продаж"},"reason":"вопрос о продажах"}""";

    private static string ToolContent(IReadOnlyList<LlmMessage> messages, string id) =>
        messages.Last(m => m.Role == LlmRole.Tool && m.ToolCallId == id).Content ?? "";

    private async Task<ConsultantAnswer> Consultant(string question, IReadOnlyList<LlmMessage>? history = null, Guid? conversation = null)
    {
        using var scope = fx.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IConsultantEngine>()
            .AnswerAsync(new ConsultantTurn(fx.AdminId, conversation ?? Guid.NewGuid(), question, history ?? []), _ => Task.CompletedTask, CancellationToken.None);
    }

    private async Task<ConsultantAnswer> Analytik(string question)
    {
        using var scope = fx.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AnalytikEngine>()
            .AnswerAsync(new ConsultantTurn(fx.AdminId, Guid.NewGuid(), question, []), _ => Task.CompletedTask, CancellationToken.None);
    }

    private async Task<AgentRun> Agent(string task)
    {
        using var scope = fx.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AgentRunner>().RunAsync(new AgentTask("sales", task, fx.AdminId), null, CancellationToken.None);
    }

    private async Task<Domain.Analytik.AnalytikInvestigation?> LatestResearch(Guid? conversation = null)
    {
        using var scope = fx.Services.CreateScope();
        // Роли — jsonb: отбор по ним — в памяти.
        var rows = await scope.ServiceProvider.GetRequiredService<IAnalytikDb>().Investigations.AsNoTracking()
            .Where(i => i.UserId == fx.AdminId && (conversation == null || i.ConversationId == conversation))
            .OrderByDescending(i => i.StartedAt).Take(20).ToListAsync();
        return rows.FirstOrDefault(i => i.Roles?.Contains("\"research\"", StringComparison.Ordinal) == true);
    }

    [PostgresFact]
    public async Task E36_correlations_question_goes_router_agent_research_and_back_with_evidence_levels()
    {
        var conversation = Guid.NewGuid();
        string? toolResult = null;
        fx.Model.Reset()
            .Text(Route)
            .Tools(FlowModel.Call("r1", ResearchTool.ToolName, new { kind = "correlations", days = 103, question = "корреляции" }))
            .Step((messages, _) =>
            {
                toolResult = ToolContent(messages, "r1");
                return FlowModel.Result(FinalJson, []);
            })
            .Text("Связи между показателями продаж, регионами и товарными группами проверены; подробности — на странице исследования.");

        var answer = await Consultant("Найди все корреляции между продажами, регионами и товарными группами за последние 103 дня.", conversation: conversation);

        var sales = Assert.Single(answer.Details.Agents);
        Assert.Equal("completed", sales.Status);
        Assert.Contains(ResearchTool.ToolName, sales.ToolsUsed);
        Assert.NotNull(toolResult);
        Assert.Contains("\"findings\"", toolResult);
        Assert.Contains("CORRELATION", toolResult);
        Assert.DoesNotContain("\"tables\"", toolResult);
        var research = await LatestResearch(conversation);
        Assert.NotNull(research);
        Assert.Contains("correlations", research.Roles!);
        using var plan = JsonDocument.Parse(research.Plan!);
        Assert.Equal("2026-07-21", plan.RootElement.GetProperty("from").GetString());
    }

    [PostgresFact]
    public async Task E37_decline_question_runs_decline_research_with_hypotheses()
    {
        string? toolResult = null;
        fx.Model.Reset()
            .Text(Route)
            .Tools(FlowModel.Call("r1", ResearchTool.ToolName, new { kind = "decline_drivers", date_from = "2026-10-01", date_to = "2026-10-31" }))
            .Step((messages, _) =>
            {
                toolResult = ToolContent(messages, "r1");
                return FlowModel.Result(FinalJson, []);
            })
            .Text("Снижение разложено по регионам, точкам и цене; гипотезы проверены.");

        var answer = await Consultant("Почему снизились продажи? Найди конкретные факторы.");
        Assert.Equal("completed", Assert.Single(answer.Details.Agents).Status);
        Assert.Contains("\"hypotheses\"", toolResult);
        Assert.Contains("churn", toolResult! + (await LatestResearch())!.Hypotheses);
    }

    [PostgresFact]
    public async Task E38_whole_company_question_runs_company_health_research()
    {
        string? toolResult = null;
        fx.Model.Reset()
            .Text(Route)
            .Tools(FlowModel.Call("r1", ResearchTool.ToolName, new { kind = "company_health", date_to = "2026-10-31" }))
            .Step((messages, _) =>
            {
                toolResult = ToolContent(messages, "r1");
                return FlowModel.Result(FinalJson, []);
            })
            .Text("Состояние компании оценено по продажам, визитам и оплатам; риски перечислены.");

        await Consultant("Проанализируй состояние всей компании и выдели основные риски.");
        Assert.Contains("\"risks\"", toolResult);
        Assert.Contains("FACT", toolResult);
    }

    [PostgresFact]
    public async Task E39_complex_russian_question_in_analytik_chat_is_researched_by_code_and_answered_in_one_model_call()
    {
        fx.Model.Reset().Text("Найдены подтверждённые связи между показателями продаж; часть и целое отмечены отдельно.");
        var answer = await Analytik("Найди все корреляции между продажами, регионами и товарными группами за последние 103 дня.");

        var call = Assert.Single(fx.Model.Calls);
        Assert.Equal("analytik.research", call.Options.Context?.Purpose);
        Assert.Contains(call.Messages, m => m.Role == LlmRole.System && (m.Content ?? "").Contains("Выводы исследования", StringComparison.Ordinal));
        Assert.Contains(call.Messages, m => m.Role == LlmRole.System && (m.Content ?? "").Contains("русский", StringComparison.Ordinal));
        Assert.NotNull(answer.Details.InvestigationId);
        Assert.Contains("/analytik/investigations/", answer.Content);
    }

    [PostgresFact]
    public async Task E40_uzbek_questions_are_matched_and_answered_in_uzbek()
    {
        Assert.Equal(ResearchKinds.DeclineDrivers, ResearchPlanner.Match("Savdo nega kamaydi? Sabablarini top")?.Kind);
        Assert.Equal(ResearchKinds.Correlations, ResearchPlanner.Match("Savdo va hududlar o'rtasidagi korrelyatsiyani top")?.Kind);
        Assert.Equal(ResearchKinds.Anomalies, ResearchPlanner.Match("Барча кўрсаткичларда аномалияларни топ")?.Kind);

        fx.Model.Reset().Text("Сотувлар камайди: сабаблар ҳудудлар ва нуқталар бўйича кўрсатилган.");
        var answer = await Analytik("Сотувлар нега камайди? Сабабларини топ");
        var call = Assert.Single(fx.Model.Calls);
        Assert.Contains(call.Messages, m => (m.Content ?? "").Contains("узбекский (кириллица)", StringComparison.Ordinal));
        Assert.NotNull(answer.Details.InvestigationId);
    }

    [PostgresFact]
    public async Task E41_first_tool_fails_with_status_and_alternatives_second_succeeds()
    {
        string? failure = null;
        fx.Model.Reset()
            .Tools(FlowModel.Call("c1", ComputeTool.ToolName, new { method = "no_such_method", metrics = new[] { "revenue" } }))
            .Step((messages, _) =>
            {
                failure = ToolContent(messages, "c1");
                return FlowModel.Result(null, [FlowModel.Call("r1", ResearchTool.ToolName, new { kind = "anomalies", date_to = "2026-10-31" })]);
            })
            .Text(FinalJson);

        var run = await Agent("Найди аномалии во всех доступных показателях");
        Assert.Equal("completed", run.Result.Status);
        Assert.Contains("\"status\":\"TOOL_UNSUPPORTED\"", failure);
        Assert.Contains("\"alternatives\"", failure);
        Assert.Contains(ResearchTool.ToolName, run.Result.ToolsUsed);
    }

    [PostgresFact]
    public async Task E42_unavailable_source_is_a_failure_status_not_no_data()
    {
        string? failure = null;
        fx.Model.Reset()
            .Tools(FlowModel.Call("b1", ConsultantDb.Broken, new { }))
            .Step((messages, _) =>
            {
                failure = ToolContent(messages, "b1");
                return FlowModel.Result(FinalJson, []);
            });

        var run = await Agent("Какие продажи в октябре?");
        Assert.Contains("\"status\":\"SOURCE_UNAVAILABLE\"", failure);
        Assert.Contains("а не что данных нет", failure);
        Assert.DoesNotContain("s3cr3t", failure);
        Assert.Equal(Tools.ToolFailures.ToolError, Assert.Single(run.Result.ToolErrors!).Kind);
    }

    [PostgresFact]
    public async Task E43_incomplete_result_is_returned_with_named_gap()
    {
        string? toolResult = null;
        fx.Model.Reset()
            .Tools(FlowModel.Call("r1", ResearchTool.ToolName, new { kind = "company_health", date_to = "2026-10-31" }))
            .Step((messages, _) =>
            {
                toolResult = ToolContent(messages, "r1");
                return FlowModel.Result(FinalJson, []);
            });

        await Agent("Оцени состояние компании");
        Assert.Contains("\"status\":\"partial\"", toolResult);
        Assert.Contains("Плана нет", toolResult);
        Assert.Contains("PARTIAL_RESULT", toolResult);
    }

    [PostgresFact]
    public async Task E44_follow_up_question_keeps_the_conversation_context()
    {
        var history = new List<LlmMessage>
        {
            new(LlmRole.User, "Почему снизились продажи в Хорезме?"),
            new(LlmRole.Assistant, "Выручка Хорезма снизилась из-за одной точки, которая перестала покупать."),
        };
        fx.Model.Reset().Text(Route).Text(FinalJson).Text("По Самарканду снижения нет: выручка выросла за счёт цены товара Б.");

        await Consultant("А по Самарканду?", history);
        var router = fx.Model.Calls[0].Messages;
        Assert.Contains(router, m => (m.Content ?? "").Contains("Почему снизились продажи в Хорезме", StringComparison.Ordinal));
        Assert.Contains(fx.Model.Calls.Last().Messages, m => (m.Content ?? "").Contains("Почему снизились продажи в Хорезме", StringComparison.Ordinal));
    }

    [PostgresFact]
    public async Task Explain_names_tool_failures_rejected_slices_and_failed_operations_of_a_conversation()
    {
        var conversation = Guid.NewGuid();
        var at = DateTimeOffset.UtcNow;
        using (var scope = fx.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBase.Infrastructure.Persistence.OneBaseDbContext>();
            db.AiAuditLogs.Add(new Domain.AI.AiAuditLog
            {
                UserId = fx.AdminId, ConversationId = conversation, Kind = "analytik", Question = "Почему упали продажи?", Status = "completed", DurationMs = 4000,
                CreatedAt = at, Grounding = """{"checked":3,"unsupported":["12 %"]}""",
            });
            db.AuditLogs.Add(new Domain.Audit.AuditLog
            {
                ActorType = Domain.Audit.ActorType.Agent, ActorId = "sales", Action = "ai.tool.failed", EntityType = "tool", EntityId = "get_visits", Timestamp = at.AddSeconds(-2),
                Data = JsonSerializer.Serialize(new { UserId = fx.AdminId, ConversationId = conversation, kind = "tool_error", message = "Инструмент завершился внутренней ошибкой" }),
            });
            await db.SaveChangesAsync();
        }

        using (var scope = fx.Services.CreateScope())
        {
            // Отклонённый срез этого разговора — через настоящий движок (план по дням).
            var engine = scope.ServiceProvider.GetRequiredService<Application.Analytik.Semantic.SemanticEngine>();
            await Assert.ThrowsAsync<Application.Analytik.Semantic.AnalyticsQueryException>(() => engine.RunAsync(
                new Application.Analytik.Semantic.AnalyticsQuery { Metrics = ["plan_kg"], Dimensions = ["date"], Grain = "day" },
                new Application.Analytik.Query.QueryCaller(fx.AdminId, "chat", "sales", conversation), CancellationToken.None));
        }

        using var s2 = fx.Services.CreateScope();
        var e = await s2.ServiceProvider.GetRequiredService<OneBase.AI.Analytik.Intelligence.QualityService>().ExplainAsync(conversation, null, CancellationToken.None);
        Assert.NotNull(e);
        Assert.Equal(1, e.UnsupportedNumbers);
        Assert.Contains(e.Items, x => x.Area == "Инструмент get_visits" && x.Status == "SOURCE_UNAVAILABLE");
        Assert.Contains(e.Items, x => x.Area == "Срез данных" && x.Status == "INSUFFICIENT_GRANULARITY");
        Assert.Contains("сбоя источника", e.Verdict);
    }

    [PostgresFact]
    public async Task E45_agent_continues_from_intermediate_slice_by_query_id()
    {
        string? computeResult = null;
        fx.Model.Reset()
            .Tools(FlowModel.Call("q1", AnalyticsQueryTool.ToolName,
                new { metrics = new[] { "revenue", "kg" }, dimensions = new[] { "date" }, grain = "day", date_from = "2026-09-01", date_to = "2026-09-30" }))
            .Step((messages, _) =>
            {
                using var doc = JsonDocument.Parse(ToolContent(messages, "q1").Replace("ОШИБКА: ", ""));
                var id = doc.RootElement.GetProperty("queryId").GetString();
                return FlowModel.Result(null, [FlowModel.Call("c1", ComputeTool.ToolName, new { method = "correlation", query_id = id })]);
            })
            .Step((messages, _) =>
            {
                computeResult = ToolContent(messages, "c1");
                return FlowModel.Result(FinalJson, []);
            });

        var run = await Agent("Есть ли связь выручки и кг в сентябре?");
        Assert.Equal("completed", run.Result.Status);
        Assert.Contains(AnalyticsQueryTool.ToolName, run.Result.ToolsUsed);
        Assert.Contains(ComputeTool.ToolName, run.Result.ToolsUsed);
        Assert.Contains("\"computation_id\"", computeResult);
        Assert.Contains("\"level\":\"CORRELATION\"", computeResult);
        Assert.Contains("2026-09-30", computeResult);
    }
}
