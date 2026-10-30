using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneBase.AI.Agents;
using OneBase.AI.Gateway;
using OneBase.AI.Llm;
using OneBase.AI.Orchestration;
using OneBase.AI.Providers;
using OneBase.AI.Tools.Data;

namespace OneBase.AI.Monitoring;

/// <summary>
/// Задача эталонного набора маршрутизации. Выбор верен, если в нём есть все Must, хотя бы один из AnyOf (если задан) и нет никого, кроме
/// Must, AnyOf и Allowed. TaskMustContain — слова, которые должна сохранить задача сотруднику (уточнение по истории: период, регион).
/// </summary>
public sealed record RouteCase(string Code, string Question, string[] Must, string[]? AnyOf = null, string[]? Allowed = null,
    (string Question, string Answer)[]? History = null, string[]? TaskMustContain = null)
{
    public string Expected =>
        string.Join("; ", new[]
        {
            Must.Length == 0 ? "никого обязательно" : "обязательно: " + string.Join(",", Must),
            AnyOf is { Length: > 0 } any ? "хотя бы один: " + string.Join(",", any) : null,
            Allowed is { Length: > 0 } allowed ? "можно: " + string.Join(",", allowed) : null,
            TaskMustContain is { Length: > 0 } words ? "в задаче: " + string.Join(",", words) : null,
        }.OfType<string>());
}

/// <summary>
/// Эталонный набор задач маршрутизации: обезличенные вопросы, типичные для OneBase (продажи, поле, финансы, склад, HR, общение), с
/// ожидаемыми AI-сотрудниками по правилам маршрутизатора. Имён людей и данных компании нет — только формулировки вопросов.
/// </summary>
public static class RouteEvalCases
{
    private const string Sales = "sales", Finance = "finance", Marketing = "marketing", Hr = "hr", Production = "production", Supply = "supply";

    public static readonly IReadOnlyList<RouteCase> All =
    [
        new("sales-month", "Как продажи за октябрь?", [Sales]),
        new("sales-drop-region", "Почему упали продажи в Самарканде на прошлой неделе?", [Sales], Allowed: [Marketing, Supply]),
        new("field-visits", "Сколько визитов сделали торговые представители сегодня?", [Sales], Allowed: [Hr]),
        new("plan-category", "Как выполняется план по помадке в Ташкенте в этом месяце?", [Sales]),
        new("lapsed-category", "Кто из клиентов по помадке, которые покупали в сентябре, ещё не купил в октябре?", [Sales], Allowed: [Marketing]),
        new("lapsed-largest", "Кто из крупнейших клиентов по помадке ещё не купил в октябре?", [Sales], Allowed: [Marketing]),
        new("agents-plan", "Какие торговые представители не выполняют план и почему?", [Sales], Allowed: [Hr]),
        new("sku-growth", "Какие SKU растут быстрее всего в этом квартале?", [Sales], Allowed: [Marketing, Supply]),
        new("forecast", "Какой прогноз продаж на конец месяца по регионам?", [Sales]),
        new("debts", "Какая задолженность торговых точек по оплатам?", [Finance], Allowed: [Sales]),
        new("dealer-pnl", "Сравни прибыль дилеров по P&L за сентябрь", [Finance], Allowed: [Sales]),
        new("profit-drop", "Почему прибыль упала?", [Finance, Sales], Allowed: [Marketing, Supply, Production]),
        new("grow-sales", "Как увеличить продажи?", [Sales], Allowed: [Marketing, Finance, Supply]),
        new("stock-out", "Какие товары скоро закончатся на складах регионов?", [Supply], Allowed: [Sales, Production]),
        new("suppliers", "Каких поставщиков у нас больше всего в справочнике?", [Supply]),
        new("purchase-season", "Нужно ли увеличить закупки к сезону?", [Supply], Allowed: [Production, Finance, Sales]),
        new("production-lag", "Почему производство отстаёт от плана?", [Production], Allowed: [Supply, Finance]),
        new("shipments", "Сколько завод отгрузил дилерам в октябре?", [], AnyOf: [Sales, Finance, Production, Supply]),
        new("export", "Как идёт экспорт по странам в этом месяце?", [], AnyOf: [Sales, Finance], Allowed: [Marketing]),
        new("campaigns", "Какие рекламные кампании дали больше всего лидов?", [Marketing]),
        new("team-load", "Какая нагрузка у сотрудников и кто чаще пропускает маршруты?", [Hr], Allowed: [Sales]),
        new("overdue-tasks", "Какие задачи отделов просрочены?", [Hr], Allowed: [Sales]),
        new("hr-late-today", "Кто сегодня опоздал на работу?", [Hr]),
        new("hr-timesheet", "Сколько часов отработал каждый отдел в этом месяце и где больше всего прогулов?", [Hr]),
        new("company", "Что происходит в компании и что улучшить в первую очередь?", [Finance, Sales, Marketing, Hr, Production, Supply]),
        new("greeting", "Привет! Что ты умеешь?", []),
        new("thanks", "Спасибо, всё понятно", []),
        new("follow-up", "А по Самарканду?", [Sales],
            History: [("Как продажи за октябрь?", "Продажи за октябрь по республике выполнены на 92% плана; отстают Самарканд и Бухара.")],
            TaskMustContain: ["самарканд", "октябр"]),

        // Несколько отделов в одном вопросе: нужен каждый, чьи данные названы.
        new("sales-and-debts", "Как продажи и задолженность дилеров по Хорезму в этом месяце?", [Sales, Finance], Allowed: [Marketing]),
        new("stock-vs-plan", "Хватит ли остатков помадки на складах регионов под план продаж месяца?", [Sales, Supply], Allowed: [Production]),
        new("late-and-sales", "Какие торговые представители чаще опаздывают и как это сказывается на их продажах?", [Hr, Sales]),

        // Уточнения по истории: тема и место — из прошлого вопроса, меняется только названное.
        new("follow-up-period", "А что было в сентябре?", [Sales],
            History: [("Какие регионы не выполняют план продаж?", "В октябре план не выполняют Самарканд (81%) и Бухара (85%).")],
            TaskMustContain: ["сентябр"]),
        new("follow-up-metric", "А задолженность там какая?", [Finance], Allowed: [Sales],
            History: [("Как продажи помадки в Ташкенте за октябрь?", "Продажи помадки в Ташкенте за октябрь — 96% плана.")],
            TaskMustContain: ["ташкент"]),
        new("follow-up-dispute", "Это неправильно, проверь ещё раз", [Sales],
            History: [("Сколько продали в октябре?", "За октябрь продано 1 200 тонн при плане 1 300 тонн.")],
            TaskMustContain: ["октябр"]),
    ];

    /// <summary>Верен ли выбор для задачи с учётом сотрудников, которые сейчас доступны (выключенные из ожиданий убираются).</summary>
    public static bool Correct(RouteCase c, RoutingDecision? decision, IReadOnlySet<string> available)
    {
        if (decision is null)
        {
            return false;
        }

        var must = c.Must.Where(available.Contains).ToHashSet();
        var any = (c.AnyOf ?? []).Where(available.Contains).ToHashSet();
        var allowed = must.Union(any).Union((c.Allowed ?? []).Where(available.Contains)).ToHashSet();
        var chosen = decision.Agents.ToHashSet();
        if (!must.IsSubsetOf(chosen) || (any.Count > 0 && !chosen.Overlaps(any)) || !chosen.IsSubsetOf(allowed))
        {
            return false;
        }

        if (c.TaskMustContain is { Length: > 0 } words)
        {
            var tasks = string.Join("\n", decision.Tasks.Values).ToLowerInvariant().Replace('ё', 'е');
            return words.All(w => tasks.Contains(w, StringComparison.Ordinal));
        }

        return true;
    }
}

/// <summary>Итог модели на эталонном наборе.</summary>
/// <summary>Итог модели на эталонном наборе. P50Ms / P95Ms — задержка вызова маршрутизатора по задачам набора.</summary>
public sealed record EvalModelSummary(string Model, int Cases, int Valid, int Correct, int Errors, long InputTokens, long CacheReadTokens,
    long CacheWriteTokens, long OutputTokens, decimal? CostUsd, long AvgMs, long P50Ms = 0, long P95Ms = 0);

public sealed record EvalCaseResult(string Code, string Question, string Expected, string? Baseline, bool BaselineCorrect, string? Candidate,
    bool CandidateCorrect, string? CandidateError);

/// <summary>Состояние проверки на эталонном наборе: идёт, итог или ошибка.</summary>
public sealed record EvalRun(string Category, string Status, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, int Done, int Total,
    EvalModelSummary? Baseline, EvalModelSummary? Candidate, int Agree, IReadOnlyList<EvalCaseResult> Cases, string? Error);

/// <summary>
/// Проверка модели категории «Маршрутизация» на эталонном наборе: каждая задача — исходной моделью и моделью категории, одинаковые
/// сообщения, без резервной модели и повторов (ошибка — так и записывается). Пользователи на результат не влияют. Итоги — в ai.ModelComparisons
/// (source = eval) и в ответе. Запускает администратор; бюджет проверяется перед каждым вызовом.
/// </summary>
public sealed class RoutingEvaluation(
    IAiGateway gateway,
    IAiSettingsSource settings,
    AiAgentStore agents,
    SalesGlossary glossary,
    IModelComparisons comparisons,
    IAiLimits limits)
{
    public async Task<EvalRun> RunRouteAsync(Guid userId, Func<EvalRun, Task>? progress, CancellationToken ct)
    {
        var s = await settings.GetSettingsAsync(ct);
        var baseline = s.Primary ?? throw new LlmNotConfiguredException("Основная модель не выбрана — сравнивать не с чем.");
        var candidate = s.Router ?? throw new LlmNotConfiguredException("Выберите «Модель маршрутизации» — её и сравним с основной.");
        if (candidate == baseline)
        {
            throw new LlmNotConfiguredException("Модель маршрутизации совпадает с основной — сравнивать нечего.");
        }

        await limits.EnsureBudgetAsync(ct);
        var available = (await agents.ListAsync(ct)).Where(a => !a.IsConsultant && a.Enabled).ToList();
        var codes = available.Select(a => a.Code).ToHashSet();
        var glossaryText = codes.Contains("sales") ? await glossary.TextAsync(ct) : null;
        var started = DateTimeOffset.UtcNow;
        var results = new List<(EvalCaseResult Case, ComparisonSide Baseline, ComparisonSide Candidate, bool Agree)>();
        EvalRun Snapshot(string status, string? error = null) => new("route", status, started, status == "running" ? null : DateTimeOffset.UtcNow,
            results.Count, RouteEvalCases.All.Count, Summary(baseline, results.Select(r => (r.Baseline, r.Case.BaselineCorrect)).ToList()),
            Summary(candidate, results.Select(r => (r.Candidate, r.Case.CandidateCorrect)).ToList()), results.Count(r => r.Agree),
            results.Select(r => r.Case).ToList(), error);

        foreach (var c in RouteEvalCases.All)
        {
            var history = (c.History ?? []).SelectMany(h => new[] { new LlmMessage(LlmRole.User, h.Question), new LlmMessage(LlmRole.Assistant, h.Answer) }).ToList();
            var messages = AiRouter.Messages(available, glossaryText, c.Question, history, null);
            var (b, bd) = await RunAsync(messages, baseline, available, userId, ct);
            var (k, kd) = await RunAsync(messages, candidate, available, userId, ct);
            var bc = RouteEvalCases.Correct(c, bd, codes);
            var kc = RouteEvalCases.Correct(c, kd, codes);
            var agree = b.Valid && k.Valid && b.Result == k.Result;
            await comparisons.RecordAsync(AiTaskCategory.Route, "eval", c.Code, c.Question, c.Expected, b, k, AiRouter.RouteSimilarity(b.Result, k.Result), bc, kc, ct);
            results.Add((new EvalCaseResult(c.Code, c.Question, c.Expected, b.Result, bc, k.Result, kc, k.Error), b, k, agree));
            if (progress is not null)
            {
                await progress(Snapshot("running"));
            }
        }

        return Snapshot("done");
    }

    /// <summary>Один вызов маршрутизатора указанной моделью: без резервной модели и повторов; бюджет исчерпан — проверка останавливается.</summary>
    private async Task<(ComparisonSide Side, RoutingDecision? Decision)> RunAsync(IReadOnlyList<LlmMessage> messages, AiModelRef model,
        IReadOnlyList<AgentConfig> available, Guid userId, CancellationToken ct)
    {
        var options = AiRouter.Options(new AiCallContext(), new AiTaskRoute(AiTaskCategory.Route, model, null, null, model)) with
        {
            AllowFallback = false,
            RetrySameModel = false,
            Context = new AiCallContext(userId, null, null, "eval.route"),
        };
        var watch = Stopwatch.StartNew();
        try
        {
            var result = await gateway.CompleteAsync(messages, [], options, ct);
            var decision = AiRouter.Parse(result.Text, available, result.Usage);
            return (new ComparisonSide(result.Model.ToString(), decision is null ? null : AiRouter.RouteResult(decision), decision is not null, result.Usage,
                watch.ElapsedMilliseconds, decision is null ? "Ответ не разобран (не JSON или не по схеме)." : null), decision);
        }
        catch (AiProviderException ex)
        {
            return (new ComparisonSide(model.ToString(), null, false, AiTokenUsage.None, watch.ElapsedMilliseconds, ex.Message), null);
        }
    }

    private static EvalModelSummary? Summary(AiModelRef model, IReadOnlyList<(ComparisonSide Side, bool Correct)> rows) =>
        rows.Count == 0 ? null : new EvalModelSummary(model.ToString(), rows.Count, rows.Count(r => r.Side.Valid), rows.Count(r => r.Correct),
            rows.Count(r => r.Side.Error is not null && !r.Side.Valid && r.Side.Usage.OutputTokens == 0),
            rows.Sum(r => (long)r.Side.Usage.InputTokens), rows.Sum(r => (long)r.Side.Usage.CacheReadTokens), rows.Sum(r => (long)r.Side.Usage.CacheWriteTokens),
            rows.Sum(r => (long)r.Side.Usage.OutputTokens), null, (long)rows.Average(r => r.Side.Ms), Percentile(rows, 0.5), Percentile(rows, 0.95));

    /// <summary>Перцентиль задержки (ближайший ранг): P95 из 34 задач — 33-я по возрастанию.</summary>
    internal static long Percentile(IReadOnlyList<(ComparisonSide Side, bool Correct)> rows, double p)
    {
        var sorted = rows.Select(r => r.Side.Ms).Order().ToList();
        return sorted[Math.Clamp((int)Math.Ceiling(p * sorted.Count) - 1, 0, sorted.Count - 1)];
    }
}

/// <summary>
/// Запуск проверки в фоне (она идёт минуты): одна проверка за раз, состояние — в памяти процесса, итоги по задачам — в ai.ModelComparisons.
/// Стоимость — по записям сравнения (те же цены моделей, что в «Использование»).
/// </summary>
public sealed class RoutingEvaluationRunner(IServiceScopeFactory scopes, ILogger<RoutingEvaluationRunner> logger)
{
    private readonly object _gate = new();
    private EvalRun? _last;

    public EvalRun? Last
    {
        get
        {
            lock (_gate)
            {
                return _last;
            }
        }
    }

    /// <summary>false — проверка уже идёт.</summary>
    public bool Start(Guid userId)
    {
        lock (_gate)
        {
            if (_last is { Status: "running" })
            {
                return false;
            }

            _last = new EvalRun("route", "running", DateTimeOffset.UtcNow, null, 0, RouteEvalCases.All.Count, null, null, 0, [], null);
        }

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopes.CreateScope();
                var evaluation = scope.ServiceProvider.GetRequiredService<RoutingEvaluation>();
                var run = await evaluation.RunRouteAsync(userId, r => { Set(r); return Task.CompletedTask; }, CancellationToken.None);
                Set(run);
            }
            catch (Exception ex)
            {
                if (ex is not (LlmNotConfiguredException or AiProviderException))
                {
                    logger.LogError(ex, "AI: проверка маршрутизации на эталонном наборе не завершилась");
                }

                lock (_gate)
                {
                    _last = (_last ?? new EvalRun("route", "failed", DateTimeOffset.UtcNow, null, 0, RouteEvalCases.All.Count, null, null, 0, [], null)) with
                    {
                        Status = "failed",
                        FinishedAt = DateTimeOffset.UtcNow,
                        Error = ex is LlmNotConfiguredException or AiProviderException ? ex.Message : "Внутренняя ошибка OneBase — подробности в журнале сервера.",
                    };
                }
            }
        });
        return true;
    }

    private void Set(EvalRun run)
    {
        lock (_gate)
        {
            _last = run;
        }
    }
}
