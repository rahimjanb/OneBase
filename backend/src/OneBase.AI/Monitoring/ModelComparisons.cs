using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneBase.AI.Gateway;
using OneBase.AI.Llm;
using OneBase.AI.Providers;
using OneBase.Application.Abstractions;
using OneBase.Domain.AI;

namespace OneBase.AI.Monitoring;

/// <summary>Одна сторона сравнения: модель, итог в сравнимом виде, разобран ли ответ, токены и время.</summary>
public sealed record ComparisonSide(string Model, string? Result, bool Valid, AiTokenUsage Usage, long Ms, string? Error = null);

/// <summary>Сравнение исходной модели и модели категории простых задач на одной задаче; итоги — в ai.ModelComparisons.</summary>
public interface IModelComparisons
{
    /// <summary>
    /// Режим «Сравнение»: модель категории решает ту же задачу в фоне, пользователь её результат не получает. Не выполняется, если траты
    /// дошли до порога предупреждения бюджета. Сбой — запись с ошибкой модели категории, без повторов и без резервной модели.
    /// </summary>
    Task ShadowAsync(AiTaskCategory category, IReadOnlyList<LlmMessage> messages, AiCallOptions options, AiModelRef candidate, string? task,
        ComparisonSide baseline, Func<string?, (string? Result, bool Valid)> evaluate, Func<string?, string?, double> similarity);

    /// <summary>Записать сравнение (эталонный набор или фон): стоимость обеих сторон — по ценам моделей из «Настройки → AI → Модели».</summary>
    Task RecordAsync(AiTaskCategory category, string source, string? caseCode, string? task, string? expected, ComparisonSide baseline,
        ComparisonSide candidate, double similarity, bool? baselineCorrect, bool? candidateCorrect, CancellationToken ct = default);
}

public sealed class ModelComparisons(IAiGateway gateway, IAiLimits limits, IServiceScopeFactory scopes, ILogger<ModelComparisons> logger) : IModelComparisons
{
    /// <summary>Фоновый вызов не держит ресурсы дольше: модель категории — быстрая, ответ сравнения пользователю не нужен.</summary>
    private static readonly TimeSpan ShadowTimeout = TimeSpan.FromSeconds(120);

    public Task ShadowAsync(AiTaskCategory category, IReadOnlyList<LlmMessage> messages, AiCallOptions options, AiModelRef candidate, string? task,
        ComparisonSide baseline, Func<string?, (string? Result, bool Valid)> evaluate, Func<string?, string?, double> similarity) =>
        Task.Run(async () =>
        {
            try
            {
                var budget = await limits.StatusAsync();
                if (budget.Exceeded || budget.DailyWarning || budget.MonthlyWarning)
                {
                    return; // сравнение — не обязательная работа: у порога бюджета не тратим на него
                }

                using var timeout = new CancellationTokenSource(ShadowTimeout);
                var watch = Stopwatch.StartNew();
                ComparisonSide side;
                try
                {
                    var result = await gateway.CompleteAsync(messages, [], options with
                    {
                        Model = candidate,
                        Fallback = null,
                        AllowFallback = false,
                        RetrySameModel = false,
                        // Без пользователя и чата: вызов не попадает в стоимость его вопроса в AI-журнале.
                        Context = new AiCallContext(null, null, null, $"{options.Context.Purpose}.shadow"),
                    }, timeout.Token);
                    var (text, valid) = evaluate(result.Text);
                    side = new ComparisonSide(result.Model.ToString(), text, valid, result.Usage, watch.ElapsedMilliseconds,
                        valid ? null : "Ответ не разобран (не JSON или не по схеме).");
                }
                catch (Exception ex) when (ex is AiProviderException or LlmNotConfiguredException or OperationCanceledException)
                {
                    side = new ComparisonSide(candidate.ToString(), null, false, AiTokenUsage.None, watch.ElapsedMilliseconds,
                        ex is OperationCanceledException ? "Модель не ответила вовремя." : ex.Message);
                }

                await RecordAsync(category, "shadow", null, task, null, baseline, side, similarity(baseline.Result, side.Result), null, null);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "AI: сравнение моделей ({Category}) не записано", category);
            }
        });

    public async Task RecordAsync(AiTaskCategory category, string source, string? caseCode, string? task, string? expected, ComparisonSide baseline,
        ComparisonSide candidate, double similarity, bool? baselineCorrect, bool? candidateCorrect, CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        db.AiModelComparisons.Add(new AiModelComparison
        {
            Category = AiTaskRouting.Code(category),
            Source = source,
            CaseCode = caseCode,
            Task = Cut(task, 500),
            Expected = Cut(expected, 1000),
            BaselineModel = baseline.Model,
            CandidateModel = candidate.Model,
            BaselineResult = Cut(baseline.Result, 1000),
            CandidateResult = Cut(candidate.Result, 1000),
            Agree = baseline.Valid && candidate.Valid && baseline.Result == candidate.Result,
            Similarity = baseline.Valid && candidate.Valid ? Math.Round(similarity, 4) : 0,
            BaselineValid = baseline.Valid,
            CandidateValid = candidate.Valid,
            BaselineCorrect = baselineCorrect,
            CandidateCorrect = candidateCorrect,
            BaselineInputTokens = baseline.Usage.InputTokens,
            BaselineCacheReadTokens = baseline.Usage.CacheReadTokens,
            BaselineCacheWriteTokens = baseline.Usage.CacheWriteTokens,
            BaselineOutputTokens = baseline.Usage.OutputTokens,
            BaselineCostUsd = await CostAsync(db, baseline, ct),
            BaselineMs = baseline.Ms,
            CandidateInputTokens = candidate.Usage.InputTokens,
            CandidateCacheReadTokens = candidate.Usage.CacheReadTokens,
            CandidateCacheWriteTokens = candidate.Usage.CacheWriteTokens,
            CandidateOutputTokens = candidate.Usage.OutputTokens,
            CandidateCostUsd = await CostAsync(db, candidate, ct),
            CandidateMs = candidate.Ms,
            CandidateError = Cut(candidate.Error, 500),
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Стоимость стороны по ценам её модели (тот же расчёт, что в ai.Usage).</summary>
    private static async Task<decimal?> CostAsync(IAppDbContext db, ComparisonSide side, CancellationToken ct)
    {
        var slash = side.Model.IndexOf('/');
        if (slash <= 0)
        {
            return side.Usage.PromptTokens == 0 && side.Usage.OutputTokens == 0 ? 0 : null;
        }

        var (provider, model) = (side.Model[..slash], side.Model[(slash + 1)..]);
        var prices = await db.AiModels.AsNoTracking()
            .Where(m => m.ProviderCode == provider && m.ModelId == model)
            .Select(m => new AiModelPrices(m.InputPricePerMillion, m.OutputPricePerMillion, m.CacheReadPricePerMillion, m.CacheWritePricePerMillion))
            .FirstOrDefaultAsync(ct);
        return AiPricing.Cost(side.Usage, prices, model);
    }

    private static string? Cut(string? text, int max) => text is null ? null : text.Length <= max ? text : text[..max];
}
