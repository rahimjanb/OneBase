using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Agents;
using OneBase.AI.Analytik;
using OneBase.AI.Llm;
using OneBase.AI.Tools;
using OneBase.Application.Analytik.Semantic;
using OneBase.Application.Security;
using OneBase.Domain.AI;
using OneBase.Domain.Identity;
using OneBase.Infrastructure.Persistence;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>Query Repair без базы: исправление отклонённого среза строит код, а не модель.</summary>
public sealed class QueryRepairTests
{
    private static readonly DateOnly From = new(2026, 9, 3), To = new(2026, 10, 20);

    [Fact]
    public void Plan_by_day_becomes_a_safe_monthly_slice()
    {
        var q = new AnalyticsQuery { Metrics = ["kg", "plan_kg"], Dimensions = ["date", "region"], Grain = "day", DateFrom = From, DateTo = To };
        var r = QueryRepair.Suggest(q, new AnalyticsQueryException("unsupported", "План продаж в OneBase — помесячный (Google Sheets / Linko): …"))!;
        Assert.True(r.Safe);
        var fixedQuery = Assert.Single(r.Queries);
        Assert.Equal("month", fixedQuery.Grain);
        Assert.Equal(new DateOnly(2026, 9, 1), fixedQuery.DateFrom);
        Assert.Equal(new DateOnly(2026, 10, 31), fixedQuery.DateTo);
    }

    [Fact]
    public void Typo_in_metric_code_is_corrected_only_when_unambiguous()
    {
        var q = new AnalyticsQuery { Metrics = ["revenu"], DateFrom = From, DateTo = To };
        var r = QueryRepair.Suggest(q, new AnalyticsQueryException("invalid", "Показатель «revenu» не найден. Есть: …"))!;
        Assert.True(r.Safe);
        Assert.Equal(["revenue"], r.Queries[0].Metrics);
        Assert.Null(QueryRepair.Nearest("zzzz", SemanticCatalog.Metrics.Select(m => m.Code)));
    }

    [Fact]
    public void Incompatible_facts_are_split_into_separate_slices()
    {
        var q = new AnalyticsQuery { Metrics = ["revenue", "payments"], Dimensions = ["region", "product_group"], DateFrom = From, DateTo = To };
        var r = QueryRepair.Suggest(q, new AnalyticsQueryException("unsupported", "«Оплаты» нельзя разложить по измерению «Товарная группа»: …"))!;
        Assert.False(r.Safe);
        Assert.Equal(2, r.Queries.Count);
        var payments = r.Queries.Single(x => x.Metrics.Contains("payments"));
        Assert.Equal(["region"], payments.Dimensions);
        Assert.Equal(["region", "product_group"], r.Queries.Single(x => x.Metrics.Contains("revenue")).Dimensions);
    }

    [Fact]
    public void Heavy_slice_gets_a_coarser_grain_and_long_period_is_split()
    {
        var heavy = new AnalyticsQuery { Metrics = ["revenue"], Dimensions = ["date", "market"], Grain = "day", DateFrom = From, DateTo = To };
        var lighter = QueryRepair.Suggest(heavy, new AnalyticsQueryException("too_heavy", "Срез даёт больше 60 000 строк"))!;
        Assert.Equal("week", lighter.Queries[0].Grain);

        var noDate = heavy with { Dimensions = ["market", "product"] };
        Assert.Equal(["product"], QueryRepair.Suggest(noDate, new AnalyticsQueryException("too_heavy", "тяжёлый"))!.Queries[0].Dimensions);

        var longPeriod = new AnalyticsQuery { Metrics = ["revenue"], DateFrom = new DateOnly(2024, 1, 1), DateTo = new DateOnly(2026, 6, 30) };
        var parts = QueryRepair.Suggest(longPeriod, new AnalyticsQueryException("too_heavy", "Период длиннее 732 дней — разбейте его"))!;
        Assert.Equal(3, parts.Queries.Count);
        Assert.Equal(longPeriod.DateTo, parts.Queries[^1].DateTo);
        Assert.All(parts.Queries, p => Assert.True(p.DateTo!.Value.DayNumber - p.DateFrom!.Value.DayNumber <= 365));
    }
}

/// <summary>Execution Safety и защита от зацикливания на настоящей базе со сценарной моделью: уровни инструментов, отказ аналитику, подтверждение человеком.</summary>
public sealed class IntelligenceSafetyTests(ConsultantDb fixture) : IClassFixture<ConsultantDb>
{
    private static JsonElement Args(object value) => JsonSerializer.SerializeToElement(value);

    private async Task GrantAsync(string agent, string tool)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        if (!await db.AgentToolGrants.AnyAsync(g => g.AgentCode == agent && g.ToolName == tool))
        {
            db.AgentToolGrants.Add(new AgentToolGrant { AgentCode = agent, ToolName = tool });
            await db.SaveChangesAsync();
        }
    }

    [PostgresFact]
    public async Task Analysts_never_get_tools_above_prepare_and_cannot_execute_operations()
    {
        await GrantAsync("analyst.sales", ExecuteTestTool.ToolName);
        using var scope = fixture.Services.CreateScope();
        var executor = scope.ServiceProvider.GetRequiredService<ToolExecutor>();
        var before = ExecuteTestTool.Executed;

        var allowed = await executor.GetAllowedToolsAsync("analyst.sales", fixture.AdminId);
        Assert.DoesNotContain(allowed, t => t.Name == ExecuteTestTool.ToolName);

        var result = await executor.ExecuteAsync(new ToolContext("analyst.sales", fixture.AdminId, 0), ExecuteTestTool.ToolName, Args(new { }));
        Assert.False(result.Success);
        Assert.Equal(ToolFailures.Forbidden, result.ErrorKind);
        Assert.Contains("выполнение операции", result.Content);
        Assert.Equal(before, ExecuteTestTool.Executed);
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        Assert.False(await db.ApprovalRequests.AnyAsync(a => a.AgentCode == "analyst.sales" && a.ToolName == ExecuteTestTool.ToolName));

        // Все инструменты, которые сидер выдал аналитикам, — не выше «подготовить».
        var registry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();
        var analystGrants = await db.AgentToolGrants.Where(g => g.AgentCode.StartsWith(AnalytikAgents.Prefix) && g.ToolName != ExecuteTestTool.ToolName)
            .Select(g => g.ToolName).Distinct().ToListAsync();
        Assert.NotEmpty(analystGrants);
        Assert.All(analystGrants, name => Assert.True((registry.Find(name)?.Level ?? ToolLevels.Read) <= ToolLevels.Prepare, name));
        Assert.Equal(ToolLevels.Analyze, registry.Find(ComputeTool.ToolName)!.Level);
    }

    [PostgresFact]
    public async Task Execute_level_tool_of_other_agents_waits_for_human_approval()
    {
        await GrantAsync("sales", ExecuteTestTool.ToolName);
        using var scope = fixture.Services.CreateScope();
        var executor = scope.ServiceProvider.GetRequiredService<ToolExecutor>();
        var before = ExecuteTestTool.Executed;
        var result = await executor.ExecuteAsync(new ToolContext("sales", fixture.AdminId, 0), ExecuteTestTool.ToolName, Args(new { }));
        Assert.False(result.Success);
        Assert.NotNull(result.ApprovalRequestId);
        Assert.Equal(before, ExecuteTestTool.Executed);
    }

    [PostgresFact]
    public async Task Failed_call_is_not_repeated_and_a_tool_is_closed_after_three_failures()
    {
        var since = DateTimeOffset.UtcNow;
        LlmToolCall Broken(string id, object args) => new(id, ConsultantDb.Broken, Args(args));
        fixture.Model.Reset()
            .Then([Broken("c1", new { })])
            .Then([Broken("c2", new { })])
            .Then([Broken("c3", new { x = 1 }), Broken("c4", new { x = 2 })])
            .Then([Broken("c5", new { x = 3 })])
            .Then("""{"agent":"sales","status":"no_data","summary":"Источник недоступен.","findings":[],"metrics":[],"problems":[],"recommendations":[],"data_sources":[]}""");

        using var scope = fixture.Services.CreateScope();
        var run = await scope.ServiceProvider.GetRequiredService<AgentRunner>()
            .RunAsync(new AgentTask("sales", "Проверка источника", fixture.AdminId), null, default);

        // Выполнено только три вызова: первый, и два новых с другими аргументами; повтор и вызов после трёх сбоев — отказ без выполнения.
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var executed = await db.AuditLogs.CountAsync(a => a.Action == "ai.tool.failed" && a.EntityId == ConsultantDb.Broken && a.Timestamp >= since);
        Assert.Equal(3, executed);
        var messages = fixture.Model.Calls[^1];
        Assert.Contains(messages, m => m.Role == LlmRole.Tool && m.Content.Contains("уже завершился ошибкой"));
        Assert.Contains(messages, m => m.Role == LlmRole.Tool && m.Content.Contains($"уже {AgentRunner.MaxToolFailures} раза"));
        Assert.Equal(3, run.ToolErrors!.Count);
    }
}

/// <summary>Query Repair и расчёт через инструменты AI на настоящем движке срезов (мир IntelligenceWorld).</summary>
public sealed class IntelligenceToolsDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private async Task<Guid> UserAsync(string suffix)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var role = new Role { Name = $"Аналитик {suffix}" };
        foreach (var code in new[] { Permissions.SalesRead, Permissions.AnalytikUse })
        {
            role.Permissions.Add(new RolePermission { Code = code });
        }

        var user = new User { Login = $"intel-{suffix}", FullName = "Директор" };
        user.Roles.Add(new UserRole { Role = role });
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<ToolResult> CallAsync(string tool, Guid user, object args)
    {
        using var scope = fixture.Services.CreateScope();
        var t = scope.ServiceProvider.GetServices<ITool>().Single(x => x.Name == tool);
        return await t.ExecuteAsync(new ToolContext("analyst.sales", user, 0), JsonSerializer.SerializeToElement(args));
    }

    [PostgresFact]
    public async Task Daily_plan_slice_is_repaired_to_months_and_answered()
    {
        var w = await IntelligenceWorld.SeedAsync(fixture);
        var user = await UserAsync(w.Suffix);
        var result = await CallAsync(AnalyticsQueryTool.ToolName, user, new
        {
            metrics = new[] { "kg", "plan_kg" }, dimensions = new[] { "date", "region" }, grain = "day", date_from = "2026-09-01", date_to = "2026-10-30",
            filters = new[] { new { dimension = "region", op = "in", values = w.Regions } },
        });
        Assert.True(result.Success, result.Content);
        using var doc = JsonDocument.Parse(result.Content);
        Assert.Contains("Исправлено автоматически", doc.RootElement.GetProperty("repaired").GetString());
        Assert.Equal("month", doc.RootElement.GetProperty("grain").GetString());
    }

    [PostgresFact]
    public async Task Incompatible_slice_returns_ready_corrected_calls_instead_of_a_dead_end()
    {
        var w = await IntelligenceWorld.SeedAsync(fixture);
        var user = await UserAsync(w.Suffix);
        var result = await CallAsync(AnalyticsQueryTool.ToolName, user, new
        {
            metrics = new[] { "revenue", "stock_units" }, dimensions = new[] { "product" }, date_from = "2026-09-01", date_to = "2026-09-30",
        });
        Assert.False(result.Success);
        using var doc = JsonDocument.Parse(result.Content);
        var calls = doc.RootElement.GetProperty("error").GetProperty("repair").GetProperty("calls");
        Assert.Equal(2, calls.GetArrayLength());
    }

    [PostgresFact]
    public async Task Compute_tool_answers_with_journaled_result_and_evidence_class()
    {
        var w = await IntelligenceWorld.SeedAsync(fixture);
        var user = await UserAsync(w.Suffix);
        var result = await CallAsync(ComputeTool.ToolName, user, new
        {
            method = "drivers", metrics = new[] { "revenue" }, dimension = "region", date_from = "2026-10-01", date_to = "2026-10-30",
            compare_from = "2026-09-01", compare_to = "2026-09-30", filters = new[] { new { dimension = "region", op = "in", values = w.Regions } },
        });
        Assert.True(result.Success, result.Content);
        using var doc = JsonDocument.Parse(result.Content);
        Assert.True(Guid.TryParse(doc.RootElement.GetProperty("computation_id").GetString(), out _));
        Assert.Equal("observed", doc.RootElement.GetProperty("evidence_class").GetString());
        Assert.Contains(w.Treated, doc.RootElement.GetProperty("summary").GetString());
    }
}
