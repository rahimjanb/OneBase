using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneBase.AI.Agents;
using OneBase.AI.Consultant;
using OneBase.AI.Gateway;
using OneBase.AI.Llm;
using OneBase.AI.Monitoring;
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
        CancellationToken ct,
        bool report = false,
        AiModelRef? model = null,
        OneBase.AI.Grounding.ReviewPlan? review = null)
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
            using var stage = AiTurnTimings.Current?.Stage("agent", a.Code);
            try
            {
                using var scope = scopes.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<AgentRunner>();
                var task = tasks.GetValueOrDefault(a.Code) ?? question;
                var run = await runner.RunAsync(new AgentTask(a.Code, task, userId, conversationId, contexts.GetValueOrDefault(a.Code), report, model), Report, ct);
                if (review is not null && RetryReason(run) is { } why)
                {
                    run = await RetryAsync(a, run, why, review, () => runner.RunAsync(
                        new AgentTask(a.Code, RetryTask(task, run, why), userId, conversationId, contexts.GetValueOrDefault(a.Code), report, model), Report, ct));
                }

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

    /// <summary>
    /// Нужен ли повтор сотрудника (review): не завершил анализ (лимит шагов) — failed; не нашёл данных, а инструменты отвечали «нет такого,
    /// нет доступа к инструменту» — выбрал неподходящий. Отказ по правам, выключенный или неизвестный сотрудник, сбой модели — не повторяются.
    /// </summary>
    internal static string? RetryReason(AgentRun run)
    {
        var denied = run.ToolErrors?.Any(e => e.Kind == AgentToolError.Denied) == true;
        return run.Result.Status switch
        {
            "failed" => denied ? OneBase.AI.Monitoring.AiReviewCauses.WrongTool : OneBase.AI.Monitoring.AiReviewCauses.Incomplete,
            "no_data" when denied => OneBase.AI.Monitoring.AiReviewCauses.WrongTool,
            _ => null,
        };
    }

    /// <summary>Задача повтора: исходная задача и что было не так (без данных пользователя — только инструменты и причина).</summary>
    internal static string RetryTask(string task, AgentRun run, string why)
    {
        var errors = (run.ToolErrors ?? []).Take(5).Select(e => $"{e.Tool}: {e.Message}").ToList();
        return $"""
            {task}

            Повторная попытка: прошлая не удалась ({(why == OneBase.AI.Monitoring.AiReviewCauses.WrongTool ? "выбраны неподходящие инструменты" : "анализ не завершён за отведённые шаги")}).
            {(errors.Count == 0 ? string.Empty : "Ошибки инструментов: " + string.Join("; ", errors) + ".")}
            Выбери инструменты из своего списка, подходящие к задаче, и правильные аргументы; запрашивай сразу всё нужное одним шагом. Если нужных данных
            нет — верни status no_data и прямо напиши, каких данных нет. Чисел, которых нет в результатах инструментов, не приводи.
            """;
    }

    /// <summary>Повтор сотрудника по плану перепроверки: есть попытка и бюджет вопроса; результат повтора берётся, только если он лучше.</summary>
    private static async Task<AgentRun> RetryAsync(AgentConfig a, AgentRun run, string why, OneBase.AI.Grounding.ReviewPlan review,
        Func<Task<AgentRun>> again)
    {
        var turn = OneBase.AI.Monitoring.AiTurnTimings.Current;
        if (!review.BudgetLeft)
        {
            turn?.Review(OneBase.AI.Monitoring.AiReviewCauses.Budget, "повтор сотрудника пропущен: бюджет вопроса на исходе", null, a.Code);
            return run;
        }

        if (!review.TryTake())
        {
            turn?.Review(OneBase.AI.Monitoring.AiReviewCauses.Attempts, "повтор сотрудника пропущен: попытки перепроверки исчерпаны", null, a.Code);
            return run;
        }

        var retried = await again();
        var better = Rank(retried.Result.Status) > Rank(run.Result.Status);
        turn?.Review(why, better ? "повтор сотрудника с исправленной задачей — результат лучше" : "повтор сотрудника с исправленной задачей — не лучше, оставлен первый",
            retried.Model, a.Code);
        return better
            ? retried with { Usage = run.Usage.Add(retried.Usage) }
            : run with { Usage = run.Usage.Add(retried.Usage) };
    }

    private static int Rank(string status) => status switch
    {
        "completed" => 3,
        "partial" => 2,
        "no_data" => 1,
        _ => 0,
    };

    private static AgentRun Failed(AgentConfig a, string message) =>
        new(new AgentResult(a.Code, a.Name, "failed", message, [], [], [], [], [], [], message), AiTokenUsage.None, null);
}
