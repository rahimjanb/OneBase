using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneBase.AI.Agents;
using OneBase.AI.Consultant;
using OneBase.AI.Llm;
using OneBase.AI.Providers;

namespace OneBase.AI.Orchestration;

/// <summary>
/// Запускает выбранных AI-сотрудников параллельно — каждого в своей области DI (свой DbContext) —
/// и собирает их структурированные результаты. Ошибка одного сотрудника не останавливает остальных.
/// </summary>
public sealed class AgentOrchestrator(IServiceScopeFactory scopes, ILogger<AgentOrchestrator> logger)
{
    public async Task<(IReadOnlyList<AgentRun> Runs, AiTokenUsage Usage)> RunAsync(
        IReadOnlyList<AgentConfig> agents,
        IReadOnlyDictionary<string, string> tasks,
        string question,
        IReadOnlyDictionary<string, string?> contexts,
        Guid userId,
        Guid? conversationId,
        Func<ConsultantProgress, Task> progress,
        CancellationToken ct)
    {
        // События прогресса идут из нескольких потоков — пишем их в поток ответа по одному.
        var gate = new SemaphoreSlim(1, 1);
        async Task Report(ConsultantProgress p)
        {
            await gate.WaitAsync(CancellationToken.None);
            try
            {
                await progress(p);
            }
            finally
            {
                gate.Release();
            }
        }

        foreach (var a in agents)
        {
            await Report(new ConsultantProgress("agent", a.Code, a.Name, ProgressStatus.Pending));
        }

        var runs = await Task.WhenAll(agents.Select(async a =>
        {
            await Report(new ConsultantProgress("agent", a.Code, a.Name, ProgressStatus.Running));
            try
            {
                using var scope = scopes.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<AgentRunner>();
                var task = tasks.GetValueOrDefault(a.Code) ?? question;
                var run = await runner.RunAsync(new AgentTask(a.Code, task, userId, conversationId, contexts.GetValueOrDefault(a.Code)), Report, ct);
                var ok = run.Result.Status is "completed" or "partial" or "no_data";
                await Report(new ConsultantProgress("agent", a.Code, a.Name, ok ? ProgressStatus.Done : ProgressStatus.Failed,
                    run.Result.Status == "no_data" ? "нет данных" : null));
                return run;
            }
            catch (Exception ex) when (ex is AiProviderException or LlmNotConfiguredException)
            {
                await Report(new ConsultantProgress("agent", a.Code, a.Name, ProgressStatus.Failed, "модель недоступна"));
                return Failed(a, ex.Message);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "AI-сотрудник {Agent} завершился с ошибкой", a.Code);
                await Report(new ConsultantProgress("agent", a.Code, a.Name, ProgressStatus.Failed, "ошибка"));
                return Failed(a, "Внутренняя ошибка OneBase при работе сотрудника.");
            }
        }));

        var usage = runs.Aggregate(AiTokenUsage.None, (sum, r) => sum.Add(r.Usage));
        return (runs, usage);
    }

    private static AgentRun Failed(AgentConfig a, string message) =>
        new(new AgentResult(a.Code, a.Name, "failed", message, [], [], [], [], [], [], message), AiTokenUsage.None, null);
}
