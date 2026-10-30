using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Agents;
using OneBase.AI.Gateway;
using OneBase.AI.Grounding;
using OneBase.AI.Knowledge;
using OneBase.AI.Llm;
using OneBase.AI.Providers;
using OneBase.AI.Reports;
using OneBase.AI.Security;
using OneBase.AI.Tools;
using OneBase.AI.Tools.Data;
using OneBase.Application.DataSources;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// Вызовы инструментов одного шага сотрудника параллельно (ToolBatch): результаты — в порядке вызовов, не больше заданного числа сразу,
/// сбой одного отменяет незаконченные и пробрасывается он сам, отмена запроса — отмена; ни один вызов не работает после выхода.
/// </summary>
public class ToolBatchTests
{
    [Fact]
    public async Task Results_keep_the_call_order_and_calls_overlap_up_to_the_limit()
    {
        var active = 0;
        var peak = 0;
        var delays = new[] { 120, 10, 80, 40, 60, 20 };
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var results = await ToolBatch.RunAsync(delays.Length, 3, async (i, ct) =>
        {
            var now = Interlocked.Increment(ref active);
            InterlockedMax(ref peak, now);
            await Task.Delay(delays[i], ct);
            Interlocked.Decrement(ref active);
            return $"r{i}";
        }, CancellationToken.None);

        Assert.Equal(["r0", "r1", "r2", "r3", "r4", "r5"], results);
        Assert.Equal(3, peak);
        Assert.True(watch.ElapsedMilliseconds < delays.Sum(), $"{watch.ElapsedMilliseconds} мс — вызовы шли по очереди");
    }

    [Fact]
    public async Task One_limit_runs_the_calls_one_by_one()
    {
        var active = 0;
        var peak = 0;
        await ToolBatch.RunAsync(4, 1, async (_, ct) =>
        {
            InterlockedMax(ref peak, Interlocked.Increment(ref active));
            await Task.Delay(10, ct);
            Interlocked.Decrement(ref active);
            return 0;
        }, CancellationToken.None);
        Assert.Equal(1, peak);
    }

    [Fact]
    public async Task A_failing_call_cancels_the_unfinished_and_its_own_error_is_thrown()
    {
        var active = 0;
        var cancelled = 0;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ToolBatch.RunAsync(4, 4, async (i, ct) =>
        {
            Interlocked.Increment(ref active);
            try
            {
                if (i == 2)
                {
                    await Task.Delay(20, CancellationToken.None);
                    throw new InvalidOperationException("сбой инструмента 2");
                }

                await Task.Delay(5_000, ct);
                return i;
            }
            catch (OperationCanceledException)
            {
                Interlocked.Increment(ref cancelled);
                throw;
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
        }, CancellationToken.None));

        Assert.Equal("сбой инструмента 2", error.Message);
        Assert.Equal(3, cancelled);
        Assert.Equal(0, active); // все вызовы закончились до выхода
    }

    [Fact]
    public async Task Cancelling_the_request_cancels_every_call()
    {
        using var cts = new CancellationTokenSource(50);
        var active = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ToolBatch.RunAsync(6, 2, async (_, ct) =>
        {
            Interlocked.Increment(ref active);
            try
            {
                await Task.Delay(5_000, ct);
                return 0;
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
        }, cts.Token));
        Assert.Equal(0, active);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int seen;
        while (value > (seen = Volatile.Read(ref target)) && Interlocked.CompareExchange(ref target, value, seen) != seen)
        {
        }
    }
}

/// <summary>
/// Сотрудник на настоящей PostgreSQL: несколько инструментов одного шага идут параллельно, каждый в своей области DI (общий DbContext дал бы
/// «A second operation was started on this context»), а результаты, доказательства, ошибки и аудит — те же, что при вызовах по очереди.
/// </summary>
public class ParallelToolsDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly (string Name, object Args)[] Calls =
    [
        ("get_sales", new { year = 2026, month = 9 }),
        ("calculate", new { expression = "900 * 3 + 7", label = "Проверка расчёта" }),
        ("get_sales_by_branch", new { year = 2026, month = 9 }),
        ("no_such_tool", new { }),
        ("get_products", new { year = 2026, month = 9 }),
        ("get_sales_kpi", new { year = 2026, month = 9 }),
    ];

    private async Task<(IReadOnlyList<LlmMessage> Results, AgentRun Run, int Audit)> RunAsync(int parallel)
    {
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        await AiSeeder.SeedAsync(fixture.Services); // сотрудники, их источники и гранты по умолчанию — как при запуске OneBase
        var gateway = new ToolCallingGateway(Calls);
        var runner = new AgentRunner(sp.GetRequiredService<AiAgentStore>(), gateway, sp.GetRequiredService<ToolExecutor>(), sp.GetRequiredService<IUserPermissions>(),
            sp.GetRequiredService<KnowledgeService>(), sp.GetRequiredService<SalesGlossary>(), sp.GetRequiredService<ReportService>(),
            sp.GetRequiredService<SourceOfTruthService>(), TimeProvider.System, fixture.Services.GetRequiredService<IServiceScopeFactory>(),
            new AgentRunnerOptions(parallel), sp.GetRequiredService<IAiSettingsSource>());
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var before = await db.AuditLogs.CountAsync(a => a.Action == "ai.tool.executed" || a.Action == "ai.tool.denied");

        var run = await runner.RunAsync(new AgentTask("sales", "Как продажи за сентябрь?", fixture.AdminId), null, CancellationToken.None);

        var audit = await db.AuditLogs.CountAsync(a => a.Action == "ai.tool.executed" || a.Action == "ai.tool.denied") - before;
        return (gateway.Calls[1].Where(m => m.Role == LlmRole.Tool).ToList(), run, audit);
    }

    [PostgresFact]
    public async Task Parallel_calls_give_the_same_results_in_the_same_order_as_one_by_one()
    {
        await fixture.ResetAsync();

        var (parallel, run, audit) = await RunAsync(4);
        var (sequential, _, sequentialAudit) = await RunAsync(1);

        Assert.Equal(Calls.Length, parallel.Count);
        Assert.Equal(sequential.Select(m => m.ToolCallId), parallel.Select(m => m.ToolCallId));
        Assert.Equal(Enumerable.Range(0, Calls.Length).Select(i => $"call_{i}"), parallel.Select(m => m.ToolCallId));
        Assert.Equal(sequential.Select(m => m.Content), parallel.Select(m => m.Content));
        Assert.Contains("2707", parallel[1].Content); // точный расчёт — на своём месте
        Assert.StartsWith("ОШИБКА:", parallel[3].Content); // неизвестный инструмент — ошибка на своём месте, остальные выполнены
        Assert.All(parallel.Where((_, i) => i != 3), m => Assert.DoesNotContain("ОШИБКА", m.Content[..Math.Min(20, m.Content.Length)]));
        Assert.Equal("completed", run.Result.Status);
        // Данные инструментов — доказательства: число из get_sales в итоге сотрудника подтверждено (регрессия: при подсчёте вызовов они терялись).
        Assert.Null(run.Result.Unverified);
        Assert.True(run.Evidence!.Supports(NumberClaims.Find(run.Result.Summary).First()), run.Result.Summary);
        Assert.True(run.Evidence!.Supports(NumberClaims.Find("2707 кг").First()), "результат расчёта — доказательство");
        Assert.Equal(4, audit); // каждый инструмент данных прошёл ToolExecutor: гранты, права и аудит
        Assert.Equal(sequentialAudit, audit);
        Assert.Equal(["get_sales", "calculate", "get_sales_by_branch", "no_such_tool", "get_products", "get_sales_kpi"], run.Result.ToolsUsed);
    }

    /// <summary>Модель: первый шаг — все вызовы одним ответом, второй — итог JSON без чисел.</summary>
    private sealed class ToolCallingGateway((string Name, object Args)[] calls) : IAiGateway
    {
        public List<IReadOnlyList<LlmMessage>> Calls { get; } = [];

        public Task<AiReadiness> GetReadinessAsync(CancellationToken cancellationToken = default) => Task.FromResult(new AiReadiness(true, null));

        public Task<AiCallResult> CompleteAsync(IReadOnlyList<LlmMessage> messages, IReadOnlyList<LlmToolDefinition> tools, AiCallOptions options,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(messages.ToList());
            var result = Calls.Count == 1
                ? new AiCallResult(null, calls.Select((c, i) => new LlmToolCall($"call_{i}", c.Name, JsonSerializer.SerializeToElement(c.Args))).ToList(),
                    new AiTokenUsage(10, 5), new AiModelRef("fake", "agent"), false, 1, "tool_use")
                : new AiCallResult(Final(messages),
                    [], new AiTokenUsage(10, 5), new AiModelRef("fake", "agent"), false, 1, "end_turn");
            return Task.FromResult(result);
        }

        public Task<(IReadOnlyList<float[]> Vectors, AiModelRef Model)> EmbedAsync(IReadOnlyList<string> inputs, AiCallContext context,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <summary>Итог сотрудника с числом из результата get_sales — как настоящая модель: оно должно подтверждаться данными инструмента.</summary>
        private static string Final(IReadOnlyList<LlmMessage> messages)
        {
            var sales = messages.First(m => m.Role == LlmRole.Tool && m.ToolCallId == "call_0").Content;
            var number = NumberClaims.Find(sales).First(c => c.Value >= 100).Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return JsonSerializer.Serialize(new { agent = "sales", status = "completed", summary = $"Продано {number} кг.", findings = Array.Empty<string>(),
                metrics = Array.Empty<object>(), problems = Array.Empty<string>(), recommendations = Array.Empty<string>() });
        }
    }
}
