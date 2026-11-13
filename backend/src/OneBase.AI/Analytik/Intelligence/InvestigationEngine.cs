using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneBase.AI.Agents;
using OneBase.AI.Consultant;
using OneBase.AI.Gateway;
using OneBase.AI.Grounding;
using OneBase.AI.Llm;
using OneBase.AI.Memory;
using OneBase.AI.Monitoring;
using OneBase.AI.Orchestration;
using OneBase.AI.Providers;
using OneBase.AI.Tools;
using OneBase.Application.Analytik;
using OneBase.Application.Analytik.Compute;
using OneBase.Domain.Analytik;

namespace OneBase.AI.Analytik.Intelligence;

/// <summary>Что нужно исследованию от чата Аналитика: ход, решение о глубине, доступные и закрытые аналитики, Chief Analyst, свежесть данных, память.</summary>
public sealed record InvestigationRequest(
    ConsultantTurn Turn, TriageResult Triage, IReadOnlyList<AgentConfig> Available, IReadOnlyList<AgentConfig> Forbidden, AgentConfig? Chief,
    IReadOnlyDictionary<string, string> Names, string? Readiness, string? Remembered, int MemoryUsed);

/// <summary>
/// Autonomous Deep Reasoning: исследование сложного вопроса командой ролей. Planner (модель) строит план — цель, подзадачи с зависимостями,
/// гипотезы с проверкой расчётом; Investigators / Domain Experts (аналитики отделов через AgentOrchestrator и AgentRunner — те же инструменты,
/// права и проверка чисел) выполняют шаги волнами по зависимостям, параллельно; Statistician (ComputeService, код) проверяет гипотезы — статус
/// ставит код (HypothesisJudge); противоречия ищет код; Planner перепланирует, если шаги без данных, есть противоречия или гипотезы не доказаны;
/// Skeptic (только глубокое исследование) предлагает альтернативы — их тоже проверяет код; Decision Advisor (Chief Analyst) пишет ответ;
/// Reviewer (правила кода, пересчёт финансовых расчётов, для глубокого — независимая модель) возвращает на этап: переписать ответ или
/// перепроверить гипотезу. Всё — с трассой и метриками в analytik.Investigations. Ничего не меняет в данных: операции только рекомендуются.
/// </summary>
public sealed class InvestigationEngine(
    AgentOrchestrator orchestrator,
    IAiGateway gateway,
    IServiceScopeFactory scopes,
    IAnalytikDb adb,
    IAiSettingsSource aiSettings,
    IToolRegistry tools,
    AiMemoryStore memory,
    TimeProvider clock,
    AnalytikOptions options,
    ILogger<InvestigationEngine> logger)
{
    private static readonly TimeSpan CompanyOffset = TimeSpan.FromHours(5);
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    public async Task<ConsultantAnswer> AnswerAsync(InvestigationRequest req, Func<ConsultantProgress, Task> progress, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        var turn = req.Turn;
        var timings = AiTurnTimings.Current;
        var llmStart = timings?.Now ?? 0;
        var costStart = timings?.CostUsd ?? 0;
        var aiConfig = await aiSettings.GetSettingsAsync(ct);
        var routing = aiConfig.ModelRouting;
        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(CompanyOffset).DateTime);
        var state = new State(watch);
        // Шаги и проверки идут параллельно: события прогресса пишутся в поток ответа по одному.
        var outer = progress;
        progress = p => state.ProgressAsync(outer, p);
        var usage = AiTokenUsage.None;
        var record = new AnalytikInvestigation
        {
            UserId = turn.UserId, ConversationId = turn.ConversationId, TurnId = timings?.Id, Origin = "chat", Question = Cut(turn.Question, 4000),
            Complexity = req.Triage.Score, Status = InvestigationStatuses.Running, StartedAt = clock.GetUtcNow(),
        };
        state.Trace("triage", $"Сложность {req.Triage.Score} ({string.Join(", ", req.Triage.Signals)}) — {(req.Triage.Deep ? "глубокое исследование" : "исследование")}.");
        await SaveAsync(record, ct);

        bool BudgetLeft() => routing.QuestionBudgetUsd is not { } limit || timings is null || timings.CostUsd < limit * 0.75m;
        var complexity = new AnswerComplexity(Math.Max(req.Triage.Score, routing.ComplexityThreshold), req.Triage.Signals);
        var choice = AiModelRouting.Final(aiConfig, turn.Channel?.Model, req.Chief?.Model, complexity);
        var context = new AiCallContext(turn.UserId, AnalytikAgents.Chief, turn.ConversationId, turn.Channel?.Name ?? "analytik");
        var analysts = req.Available.Select(a => a.Code).ToHashSet(StringComparer.Ordinal);
        var fallbackAnalyst = analysts.Contains(AnalytikAgents.Bi) ? AnalytikAgents.Bi : req.Available.FirstOrDefault()?.Code ?? AnalytikAgents.Bi;

        // ---------- Planner ----------
        await progress(new ConsultantProgress("plan", null, null, ProgressStatus.Running, "составляю план исследования"));
        state.Roles.Add(InvestigationRoles.Planner);
        var (planRoot, planUsage) = await JsonCallAsync(InvestigationPrompts.Planner,
            InvestigationPrompts.PlannerMessage(turn.Question, History(turn.History), req.Available.Select(a => (a.Code, a.Name, a.Description ?? a.Role ?? "")), today,
                options.InvestigationMaxSteps, options.InvestigationMaxHypotheses, req.Readiness),
            choice, context with { Purpose = "analytik.plan" }, 4096, state, ct);
        usage = usage.Add(planUsage);
        var plan = planRoot is { } pr
            ? new InvestigationPlan(JsonText.Str(pr, "goal") ?? turn.Question, JsonText.Str(pr, "depth") ?? (req.Triage.Deep ? "deep" : "standard"),
                InvestigationJson.Steps(pr, analysts, fallbackAnalyst, options.InvestigationMaxSteps, 0),
                InvestigationJson.Hypotheses(pr, options.InvestigationMaxHypotheses, "planner"),
                JsonText.Array(pr, "data_needed").Select(x => x.ToString()).Where(x => x.Length > 0).Take(10).ToList())
            : FallbackPlan(turn.Question, req.Available, fallbackAnalyst);
        if (plan.Steps.Count == 0)
        {
            plan = plan with { Steps = FallbackPlan(turn.Question, req.Available, fallbackAnalyst).Steps };
        }

        var deep = req.Triage.Deep || plan.Depth == "deep";
        record.Goal = Cut(plan.Goal, 2000);
        state.Trace("plan", plan.Fallback
            ? $"План без модели (планировщик недоступен): шагов {plan.Steps.Count}."
            : $"План: шагов {plan.Steps.Count}, гипотез {plan.Hypotheses.Count}, волн {InvestigationJson.Waves(plan.Steps).Count}.");
        await progress(new ConsultantProgress("plan", null, null, ProgressStatus.Done, $"шагов {plan.Steps.Count}, гипотез {plan.Hypotheses.Count}"));

        // ---------- Statistician: гипотезы проверяются кодом параллельно со шагами ----------
        var hypothesisTask = TestAsync(plan.Hypotheses, turn, record.Id, state, progress, ct);

        // ---------- Investigators: шаги волнами по зависимостям ----------
        var steps = new List<StepOutcome>();
        var runs = new List<AgentRun>();
        await RunStepsAsync(plan.Steps, plan, req, steps, runs, state, progress, routing, ct);
        var verdicts = (await hypothesisTask).ToList();
        var contradictions = InvestigationReviewer.Contradictions(steps, verdicts).ToList();
        foreach (var c in contradictions)
        {
            state.Trace("contradiction", c.Text);
        }

        // ---------- Replan: пробелы, противоречия, недоказанные гипотезы ----------
        for (var round = 1; round <= options.InvestigationMaxReplans && NeedsMore(steps, verdicts, contradictions) && BudgetLeft(); round++)
        {
            await progress(new ConsultantProgress("replan", null, null, ProgressStatus.Running, "дополняю план: пробелы и противоречия"));
            var (more, moreUsage) = await JsonCallAsync(InvestigationPrompts.Replanner,
                $"Сегодня {today:yyyy-MM-dd}. Пределы: новых шагов — не больше 4, новых гипотез — не больше 3.\n" +
                $"Доступные аналитики: {string.Join(", ", req.Available.Select(a => $"{a.Code} ({a.Name})"))}.\n\n" +
                InvestigationPrompts.State(plan.Goal, steps, verdicts, contradictions),
                choice, context with { Purpose = "analytik.replan" }, 3072, state, ct);
            usage = usage.Add(moreUsage);
            record.Replans++;
            if (more is not { } m || (m.TryGetProperty("stop", out var stop) && stop.ValueKind == JsonValueKind.True))
            {
                state.Trace("replan", more is null ? "Перепланирование не удалось — ответ по собранным данным." : $"Данных достаточно: {JsonText.Str(more.Value, "reason")}");
                await progress(new ConsultantProgress("replan", null, null, ProgressStatus.Done, "данных достаточно"));
                break;
            }

            var newSteps = InvestigationJson.Steps(m, analysts, fallbackAnalyst, 4, round, $"r{round}s")
                .Where(s => !steps.Any(x => string.Equals(x.Step.Question, s.Question, StringComparison.OrdinalIgnoreCase))).ToList();
            var newHypotheses = InvestigationJson.Hypotheses(m, 3, "replan", $"r{round}h")
                .Where(h => !verdicts.Any(v => string.Equals(v.Hypothesis.Statement, h.Statement, StringComparison.OrdinalIgnoreCase))).ToList();
            state.Trace("replan", $"Дополнение плана ({JsonText.Str(m, "reason")}): шагов {newSteps.Count}, гипотез {newHypotheses.Count}.");
            plan = plan with { Steps = [.. plan.Steps, .. newSteps], Hypotheses = [.. plan.Hypotheses, .. newHypotheses] };
            var extraTests = TestAsync(newHypotheses, turn, record.Id, state, progress, ct);
            await RunStepsAsync(newSteps, plan, req, steps, runs, state, progress, routing, ct);
            verdicts.AddRange(await extraTests);
            contradictions = InvestigationReviewer.Contradictions(steps, verdicts).ToList();
            await progress(new ConsultantProgress("replan", null, null, ProgressStatus.Done, $"шагов +{newSteps.Count}, гипотез +{newHypotheses.Count}"));
        }

        // ---------- Skeptic (глубокое исследование) ----------
        var objections = new List<string>();
        if (deep && BudgetLeft() && (steps.Count > 0 || verdicts.Count > 0))
        {
            state.Roles.Add(InvestigationRoles.Skeptic);
            await progress(new ConsultantProgress("skeptic", null, null, ProgressStatus.Running, "ищу альтернативные объяснения"));
            var (sk, skUsage) = await JsonCallAsync(InvestigationPrompts.Skeptic, InvestigationPrompts.State(plan.Goal, steps, verdicts, contradictions),
                choice, context with { Purpose = "analytik.skeptic" }, 2048, state, ct);
            usage = usage.Add(skUsage);
            if (sk is { } s)
            {
                objections.AddRange(JsonText.Array(s, "objections").Select(x => x.ToString()).Where(x => x.Length > 0).Take(5));
                var alternatives = InvestigationJson.Hypotheses(s, 3, "skeptic", "k")
                    .Where(h => !verdicts.Any(v => string.Equals(v.Hypothesis.Statement, h.Statement, StringComparison.OrdinalIgnoreCase))).ToList();
                verdicts.AddRange(await TestAsync(alternatives, turn, record.Id, state, progress, ct));
                state.Trace("skeptic", $"Возражений {objections.Count}, альтернатив проверено {alternatives.Count}.");
            }

            await progress(new ConsultantProgress("skeptic", null, null, ProgressStatus.Done, objections.Count == 0 ? null : $"возражений: {objections.Count}"));
        }

        // ---------- Decision Advisor: ответ ----------
        state.Roles.Add(InvestigationRoles.DecisionAdvisor);
        await progress(new ConsultantProgress("compose", null, null, ProgressStatus.Running));
        var results = steps.Select((s, i) => runs[i].Result with { AgentName = $"{runs[i].Result.AgentName} · {s.Step.Id}" }).ToList();
        var evidence = new EvidenceSet();
        foreach (var r in runs)
        {
            if (r.Evidence is { } e)
            {
                evidence.AddAll(e);
            }
        }

        foreach (var v in verdicts)
        {
            evidence.AddText(string.Join(" ", v.Supporting.Concat(v.Refuting).Append(v.Summary ?? "")));
        }

        evidence.AddContent(state.ComputationContent.ToString());
        evidence.AddText(req.Remembered);
        var reviewPlan = routing.On && routing.Review ? new ReviewPlan(routing.ReviewMaxAttempts, BudgetLeft) : null;
        if (reviewPlan is not null)
        {
            reviewPlan.Escalation = AiModelRouting.Escalation(routing, choice.Model ?? aiConfig.Primary);
        }

        var composeRules = OrchestratedConsultantEngine.ComposeRules(req.Chief) + "\n" + AnalytikEngine.Format + "\n" + InvestigationPrompts.Advisor;
        var data = ComposeData(req, plan, results, verdicts, contradictions, objections, today);
        var callOptions = new AiCallOptions
        {
            Model = choice.Model, ModelReason = choice.Reason, Temperature = req.Chief?.Temperature,
            MaxOutputTokens = turn.Channel?.MaxOutputTokens ?? req.Chief?.MaxOutputTokens ?? Math.Max(aiConfig.MaxOutputTokens, AgentRunner.AgentOutputTokens),
            CacheConversation = false, Context = context with { Purpose = "analytik.answer" },
        };
        var composer = new GroundedComposer(gateway);
        GroundedAnswer answer;
        using (timings?.Stage("compose"))
        {
            Interlocked.Increment(ref state.LlmCalls);
            answer = await composer.ComposeAsync(Messages(composeRules, data, turn, null), callOptions, evidence, turn.Question, ct, null, turn.Draft, reviewPlan);
        }

        usage = usage.Add(answer.Usage);

        // ---------- Reviewer: правила кода, пересчёт, независимая модель; возврат на этап ----------
        state.Roles.Add(InvestigationRoles.Reviewer);
        var periods = steps.SelectMany(s => s.Sources).Select(s => s.Period ?? "").Concat(plan.Hypotheses.Select(h => $"{h.Test?.Data.From:yyyy-MM-dd} {h.Test?.Data.To:yyyy-MM-dd}"))
            .Append(today.ToString("yyyy-MM-dd")).ToList();
        var cycles = new List<ReviewCycle>();
        for (var round = 1; round <= Math.Max(1, options.InvestigationReviewCycles); round++)
        {
            await progress(new ConsultantProgress("review", null, null, ProgressStatus.Running, "проверяю выводы"));
            var issues = InvestigationReviewer.Check(answer.Text, turn.Question, answer.Grounding, steps, verdicts, periods).ToList();
            if (round == 1 && InvestigationReviewer.Financial(turn.Question))
            {
                issues.AddRange(await RecalculateAsync(verdicts, turn, state, ct));
            }

            var by = "code";
            if (deep && round == 1 && BudgetLeft())
            {
                by = "code+model";
                var (rv, rvUsage) = await JsonCallAsync(InvestigationPrompts.Reviewer,
                    InvestigationPrompts.State(plan.Goal, steps, verdicts, contradictions) + "\nВопрос: " + turn.Question + "\n\nОтвет на проверку:\n" + answer.Text,
                    choice, context with { Purpose = "analytik.review" }, 2048, state, ct);
                usage = usage.Add(rvUsage);
                if (rv is { } r && JsonText.Str(r, "verdict") == "revise")
                {
                    issues.AddRange(JsonText.Array(r, "issues").Where(i => JsonText.Str(i, "severity") == "error")
                        .Select(i => new ReviewIssue("model:" + (JsonText.Str(i, "kind") ?? "other"), "error", JsonText.Str(i, "text") ?? "")).Where(i => i.Text.Length > 0).Take(5));
                }
            }

            var errors = issues.Where(i => i.Severity == "error").ToList();
            if (errors.Count == 0 || round == Math.Max(1, options.InvestigationReviewCycles) || !BudgetLeft())
            {
                cycles.Add(new ReviewCycle(round, by, errors.Count == 0 ? "approve" : "revise", issues, errors.Count == 0 ? null : "предупреждение в ответе"));
                if (errors.Count > 0)
                {
                    record.ReviewRejections++;
                    answer = answer with { Text = answer.Text + "\n\n⚠️ Проверка выводов: " + string.Join(" ", errors.Select(e => e.Text).Distinct().Take(3)) };
                }

                await progress(new ConsultantProgress("review", null, null, ProgressStatus.Done, errors.Count == 0 ? "выводы проверены" : $"замечаний: {errors.Count}"));
                break;
            }

            record.ReviewRejections++;
            string action;
            if (errors.Any(e => e.Action == "retest"))
            {
                // Возврат на этап: расхождение пересчёта — гипотезы перепроверяются, данные ответа обновляются.
                action = "перепроверка гипотез";
                var retest = verdicts.Where(v => errors.Any(e => e.Action == "retest" && e.Text.Contains(v.Hypothesis.Id, StringComparison.Ordinal))).Select(v => v.Hypothesis).ToList();
                var again = await TestAsync(retest, turn, record.Id, state, progress, ct);
                verdicts = verdicts.Select(v => again.FirstOrDefault(a => a.Hypothesis.Id == v.Hypothesis.Id) ?? v).ToList();
                data = ComposeData(req, plan, results, verdicts, contradictions, objections, today);
            }
            else
            {
                action = "ответ переписан";
            }

            cycles.Add(new ReviewCycle(round, by, "revise", issues, action));
            state.Trace("review", $"Проверка {round}: замечаний {errors.Count} — {action}.");
            var notes = "Замечания независимой проверки (исправь ответ по ним, не меняя подтверждённых чисел):\n" +
                        string.Join("\n", errors.Select(e => $"- {e.Text}"));
            Interlocked.Increment(ref state.LlmCalls);
            answer = await composer.ComposeAsync(Messages(composeRules, data, turn, notes), callOptions with { Context = context with { Purpose = "analytik.review.revise" } },
                evidence, turn.Question, ct, null, null, reviewPlan);
            usage = usage.Add(answer.Usage);
        }

        // ---------- Итог ----------
        var text = answer.Text + "\n\n" + HypothesisTable(verdicts) + Footer(record.Id, steps, verdicts, cycles, record.Replans);
        await progress(new ConsultantProgress("compose", null, null, ProgressStatus.Done,
            answer.Grounding.Checked == 0 ? null : $"чисел сверено с данными: {answer.Grounding.Checked}"));
        var stepsUsage = runs.Aggregate(AiTokenUsage.None, (sum, r) => sum.Add(r.Usage));
        usage = usage.Add(stepsUsage);

        try
        {
            await memory.SaveAsync(turn.UserId, turn.ConversationId, plan.Steps.GroupBy(s => s.Analyst).ToDictionary(g => g.Key, g => g.First().Question),
                turn.Question, results, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Аналитик: память исследования не сохранена");
        }

        var limitations = steps.Where(s => s.Status is not "completed").Select(s => $"Шаг {s.Step.Id} ({s.Step.Analyst}): {s.Status} — {Cut(s.Summary, 200)}")
            .Concat(verdicts.Where(v => v.Status == HypothesisStatuses.Inconclusive).SelectMany(v => v.Needed.Take(1).Select(n => $"«{v.Hypothesis.Statement}»: {n}")))
            .Concat(plan.Fallback ? ["План составлен без модели (планировщик недоступен)."] : [])
            .ToList();
        var view = timings?.View();
        record.Status = steps.Count > 0 && steps.All(s => s.Status is "completed") ? InvestigationStatuses.Completed
            : steps.Any(s => s.Status is "completed" or "partial") || verdicts.Any(v => v.ComputationId is not null) ? InvestigationStatuses.Partial
            : InvestigationStatuses.Failed;
        record.FailureReason = record.Status == InvestigationStatuses.Failed ? steps.Select(s => s.Status).FirstOrDefault() ?? "no_steps" : null;
        record.Roles = InvestigationJson.Write(state.Roles.Distinct().ToList());
        record.Plan = InvestigationJson.Write(new
        {
            plan.Goal, plan.Depth, plan.Fallback, plan.DataNeeded,
            steps = plan.Steps.Select(s => new { s.Id, s.Question, s.Analyst, s.DependsOn, s.Purpose, s.Round }),
            // Волны — по раундам: шаги перепланирования выполнялись после всех волн исходного плана.
            waves = plan.Steps.GroupBy(s => s.Round).OrderBy(g => g.Key).SelectMany(g => InvestigationJson.Waves(g.ToList())).Select(w => w.Select(s => s.Id)),
        });
        record.Steps = InvestigationJson.Write(steps.Select(s => new
        {
            s.Step.Id, s.Step.Question, s.Step.Analyst, s.Step.DependsOn, s.Step.Round, s.Status, Summary = Cut(s.Summary, 1500), Findings = s.Findings.Take(12),
            Metrics = s.Metrics.Take(20), Sources = s.Sources.Take(10), s.ToolCalls, s.Unverified, s.Ms,
        }));
        record.Hypotheses = InvestigationJson.Write(verdicts.Select(v => new
        {
            v.Hypothesis.Id, v.Hypothesis.Statement, v.Hypothesis.Origin, Test = v.Hypothesis.Test is { } t ? new { t.Method, t.Data, t.Params } : null, v.Hypothesis.Expect,
            v.Status, v.EvidenceClass, v.Confidence, v.Supporting, v.Refuting, v.Needed, v.Impact, v.ImpactUnit, v.ComputationId, v.Summary,
        }));
        record.Contradictions = InvestigationJson.Write(contradictions);
        record.Reviews = InvestigationJson.Write(new { cycles, objections, grounding = answer.Grounding });
        record.Trace = InvestigationJson.Write(state.Events);
        record.Limitations = InvestigationJson.Write(limitations);
        record.Summary = Cut(answer.Text, 4000);
        record.Depth = 1 + record.Replans + (objections.Count > 0 || state.Roles.Contains(InvestigationRoles.Skeptic) ? 1 : 0);
        record.StepCount = steps.Count;
        record.ToolCalls = runs.Sum(r => r.ToolCalls + r.Calculations) + state.Computations;
        record.LlmCalls = view?.Calls.Count(c => c.StartMs >= llmStart) ?? state.LlmCalls + runs.Count;
        record.InputTokens = usage.PromptTokens;
        record.OutputTokens = usage.OutputTokens;
        record.CostUsd = timings is null || timings.CostIncomplete ? null : timings.CostUsd - costStart;
        record.DurationMs = watch.ElapsedMilliseconds;
        record.FinishedAt = clock.GetUtcNow();
        await SaveAsync(record, CancellationToken.None);

        var sources = new List<DataSource>();
        foreach (var s in results.SelectMany(r => r.DataSources))
        {
            if (!sources.Any(x => x.Title == s.Title && x.Period == s.Period))
            {
                sources.Add(s);
            }
        }

        return new ConsultantAnswer(text,
            new ConsultantDetails(results, sources, answer.Model.ToString(), answer.UsedFallback,
                $"Исследование: шагов {steps.Count}, гипотез {verdicts.Count}, перепланирований {record.Replans}, проверок {cycles.Count}.", req.MemoryUsed, null,
                answer.Grounding, null, record.Id),
            usage);
    }

    // ---------- Шаги ----------

    private async Task RunStepsAsync(IReadOnlyList<InvestigationStep> stepsToRun, InvestigationPlan plan, InvestigationRequest req, List<StepOutcome> outcomes,
        List<AgentRun> runs, State state, Func<ConsultantProgress, Task> progress, AiModelRoutingSettings routing, CancellationToken ct)
    {
        var done = outcomes.Select(o => o.Step.Id).ToHashSet(StringComparer.Ordinal);
        using var slots = new SemaphoreSlim(Math.Max(1, options.InvestigationParallelSteps));
        var reviewPlan = routing.On && routing.Review ? new ReviewPlan(routing.ReviewMaxAttempts) : null;
        foreach (var wave in InvestigationJson.Waves(stepsToRun, done))
        {
            state.Roles.Add(wave.Any(s => s.Analyst == AnalytikAgents.Bi) ? InvestigationRoles.Investigator : InvestigationRoles.DomainExpert);
            var finished = await Task.WhenAll(wave.Select(async step =>
            {
                await slots.WaitAsync(ct);
                try
                {
                    var agent = req.Available.FirstOrDefault(a => a.Code == step.Analyst) ?? req.Available.First();
                    var watch = Stopwatch.StartNew();
                    var (r, _) = await orchestrator.RunAsync([agent], new Dictionary<string, string> { [agent.Code] = step.Question }, req.Turn.Question,
                        new Dictionary<string, string?> { [agent.Code] = StepContext(step, plan, outcomes, req) }, req.Turn.UserId, req.Turn.ConversationId, progress, ct,
                        false, req.Turn.Channel?.AgentModel, reviewPlan);
                    var run = r[0];
                    return (step, run, watch.ElapsedMilliseconds);
                }
                finally
                {
                    slots.Release();
                }
            }));

            foreach (var (step, run, ms) in finished)
            {
                var res = run.Result;
                outcomes.Add(new StepOutcome(step, res.Status, res.Summary, res.Findings, res.Metrics, res.DataSources, run.ToolCalls, res.Unverified ?? [], ms));
                runs.Add(run);
                state.Trace("step", $"{step.Id} [{step.Analyst}] → {res.Status}: {Cut(res.Summary, 200)}");
            }
        }
    }

    /// <summary>Контекст шага: цель исследования, зачем шаг, выводы шагов, от которых он зависит, гипотезы на проверке, правила при отставании данных.</summary>
    private static string StepContext(InvestigationStep step, InvestigationPlan plan, IReadOnlyList<StepOutcome> outcomes, InvestigationRequest req)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Это шаг {step.Id} исследования. Цель исследования: {plan.Goal}");
        if (step.Purpose is { Length: > 0 } purpose)
        {
            sb.AppendLine($"Зачем этот шаг: {purpose}");
        }

        var deps = outcomes.Where(o => step.DependsOn.Contains(o.Step.Id)).ToList();
        if (deps.Count > 0)
        {
            sb.AppendLine("Результаты шагов, от которых зависит этот (используй их, данные бери инструментами заново, если нужны числа):");
            foreach (var d in deps)
            {
                sb.AppendLine($"- {d.Step.Id}: {Cut(d.Summary, 500)}");
                foreach (var f in d.Findings.Take(5))
                {
                    sb.AppendLine($"    • {Cut(f, 200)}");
                }
            }
        }

        if (plan.Hypotheses.Count > 0)
        {
            sb.AppendLine("Гипотезы исследования (их проверяет расчёт; если твои данные подтверждают или опровергают гипотезу — напиши это в findings с числами):");
            foreach (var h in plan.Hypotheses.Take(8))
            {
                sb.AppendLine($"- {h.Id}: {h.Statement}");
            }
        }

        sb.AppendLine("Для разложения изменения, сегментов и проверки связей используй analytik_compute. Причину называй только по проверке.");
        if (req.Readiness is not null)
        {
            sb.AppendLine(req.Readiness).AppendLine(AnalytikEngine.AgentReadinessRules);
        }

        return sb.ToString();
    }

    private static bool NeedsMore(IReadOnlyList<StepOutcome> steps, IReadOnlyList<HypothesisVerdict> verdicts, IReadOnlyList<Contradiction> contradictions) =>
        steps.Any(s => s.Status is "failed" or "no_data") || contradictions.Count > 0
        || (verdicts.Count > 0 && verdicts.All(v => v.Status is HypothesisStatuses.Inconclusive or HypothesisStatuses.Rejected));

    /// <summary>План без модели: по аналитику на отдел, о котором вопрос (по словам), иначе BI-аналитик; гипотез нет.</summary>
    private static InvestigationPlan FallbackPlan(string question, IReadOnlyList<AgentConfig> available, string fallback)
    {
        var text = RouteIntent.Normalize(question);
        var codes = AnalytikAgents.Keywords.Where(k => available.Any(a => a.Code == k.Key) && k.Value.Any(text.Contains)).Select(k => k.Key).Take(4).ToList();
        if (codes.Count == 0)
        {
            codes = [fallback];
        }

        return new InvestigationPlan(question, "standard", codes.Select((c, i) => new InvestigationStep($"s{i + 1}", question, c, [], null)).ToList(), [], [], true);
    }

    // ---------- Гипотезы ----------

    private async Task<IReadOnlyList<HypothesisVerdict>> TestAsync(IReadOnlyList<PlannedHypothesis> hypotheses, ConsultantTurn turn, Guid investigationId, State state,
        Func<ConsultantProgress, Task> progress, CancellationToken ct)
    {
        if (hypotheses.Count == 0)
        {
            return [];
        }

        state.Roles.Add(InvestigationRoles.Statistician);
        state.Roles.Add(InvestigationRoles.HypothesisAnalyst);
        await progress(new ConsultantProgress("hypotheses", null, null, ProgressStatus.Running, $"проверяю гипотезы: {hypotheses.Count}"));
        var verdicts = await Task.WhenAll(hypotheses.Select(async h =>
        {
            if (h.Test is null)
            {
                return HypothesisJudge.Evaluate(h, null);
            }

            try
            {
                using var scope = scopes.CreateScope();
                var compute = scope.ServiceProvider.GetRequiredService<ComputeService>();
                var o = await compute.RunAsync(h.Test, new ComputeContext(turn.UserId, "investigation", investigationId, turn.ConversationId, "statistician"), ct);
                Interlocked.Increment(ref state.Computations);
                lock (state.ComputationContent)
                {
                    state.ComputationContent.AppendLine(JsonSerializer.Serialize(o.Result, ComputeService.JsonOptions));
                }

                return HypothesisJudge.Evaluate(h, o);
            }
            catch (Exception ex) when (ex is ComputeException or UnauthorizedAccessException)
            {
                return HypothesisJudge.Evaluate(h, null) with
                {
                    Needed = [ex is UnauthorizedAccessException ? "Нет прав на данные проверки." : ex.Message],
                };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Аналитик: проверка гипотезы {Hypothesis} не выполнена", h.Id);
                return HypothesisJudge.Evaluate(h, null);
            }
        }));
        foreach (var v in verdicts)
        {
            state.Trace("hypothesis", $"{v.Hypothesis.Id} «{v.Hypothesis.Statement}» → {HypothesisStatuses.Title(v.Status)} ({EvidenceClasses.Title(v.EvidenceClass)}).");
        }

        await progress(new ConsultantProgress("hypotheses", null, null, ProgressStatus.Done,
            $"подтверждено {verdicts.Count(v => v.Status == HypothesisStatuses.Confirmed)}, отклонено {verdicts.Count(v => v.Status == HypothesisStatuses.Rejected)}"));
        return verdicts;
    }

    /// <summary>Финансовый вывод — пересчёт расчётов подтверждённых гипотез по записи журнала; расхождение без обновления данных — возврат на этап.</summary>
    private async Task<IReadOnlyList<ReviewIssue>> RecalculateAsync(IReadOnlyList<HypothesisVerdict> verdicts, ConsultantTurn turn, State state, CancellationToken ct)
    {
        var issues = new List<ReviewIssue>();
        foreach (var v in verdicts.Where(v => v.ComputationId is not null && v.Status is HypothesisStatuses.Confirmed or HypothesisStatuses.PartiallyConfirmed))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var (_, same, changed) = await scope.ServiceProvider.GetRequiredService<ComputeService>()
                    .ReplayAsync(v.ComputationId!.Value, new ComputeContext(turn.UserId, "investigation"), ct);
                Interlocked.Increment(ref state.Computations);
                state.Trace("recalc", $"{v.Hypothesis.Id}: пересчёт {(same ? "совпал" : changed ? "отличается — данные обновились" : "не совпал")}.");
                if (!same)
                {
                    issues.Add(new ReviewIssue("recalc", "error",
                        $"Пересчёт гипотезы {v.Hypothesis.Id} дал другой результат{(changed ? " (данные обновились во время исследования)" : "")} — гипотеза перепроверена.", "retest"));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Аналитик: пересчёт {Computation} не выполнен", v.ComputationId);
            }
        }

        return issues;
    }

    // ---------- Вызовы модели ----------

    /// <summary>Вызов роли с ответом JSON. Сбой модели, бюджет, неверный JSON — null (исследование продолжается без этой роли).</summary>
    private async Task<(JsonElement? Root, AiTokenUsage Usage)> JsonCallAsync(string system, string user, AiModelChoice choice, AiCallContext context, int maxTokens,
        State state, CancellationToken ct)
    {
        try
        {
            Interlocked.Increment(ref state.LlmCalls);
            var r = await gateway.CompleteAsync([new LlmMessage(LlmRole.System, system), new LlmMessage(LlmRole.User, user)], [], new AiCallOptions
            {
                Model = choice.Model, ModelReason = choice.Reason, JsonOutput = true, MaxOutputTokens = maxTokens, Temperature = 0.2, Context = context,
            }, ct);
            var root = JsonText.ExtractObject(r.Text) ?? (JsonText.CloseTruncated(r.Text) is { } closed ? JsonText.ExtractObject(closed) : null);
            if (root is null)
            {
                state.Trace("model", $"{context.Purpose}: ответ модели не JSON — роль пропущена.");
            }

            return (root, r.Usage);
        }
        catch (Exception ex) when (ex is AiProviderException or LlmNotConfiguredException)
        {
            state.Trace("model", $"{context.Purpose}: модель недоступна — {ex.Message}");
            return (null, AiTokenUsage.None);
        }
    }

    private static List<LlmMessage> Messages(string rules, string data, ConsultantTurn turn, string? reviewNotes) =>
    [
        new(LlmRole.System, rules),
        new(LlmRole.System, reviewNotes is null ? data : data + "\n\n" + reviewNotes),
        .. turn.History,
        new(LlmRole.User, turn.Question),
    ];

    private string ComposeData(InvestigationRequest req, InvestigationPlan plan, IReadOnlyList<AgentResult> results, IReadOnlyList<HypothesisVerdict> verdicts,
        IReadOnlyList<Contradiction> contradictions, IReadOnlyList<string> objections, DateOnly today)
    {
        var now = clock.GetUtcNow().ToOffset(CompanyOffset);
        var sb = new StringBuilder();
        sb.AppendLine($"Сейчас {now:dd.MM.yyyy HH:mm} (Ташкент).");
        sb.AppendLine($"Язык вопроса — {AnswerLanguages.Label(AnswerLanguages.Of(req.Turn.Question))}: отвечай на нём.");
        if (req.Turn.Channel?.Instructions is { Length: > 0 } instructions)
        {
            sb.AppendLine(instructions);
        }

        sb.AppendLine().AppendLine($"Цель исследования: {plan.Goal}");
        sb.AppendLine("Результаты шагов аналитиков (единственный источник данных компании; состояние каждого — в поле outcome):");
        sb.AppendLine(OrchestratedConsultantEngine.ComposeData(results, name => tools.Find(name)?.Title, AnswerLanguages.Of(req.Turn.Question)));
        sb.AppendLine().AppendLine("Гипотезы — статус и класс вывода поставил расчёт (код), меняй их нельзя:");
        foreach (var v in verdicts)
        {
            sb.AppendLine($"- «{v.Hypothesis.Statement}» → {HypothesisStatuses.Title(v.Status)}; {EvidenceClasses.Title(v.EvidenceClass)}; уверенность {v.Confidence:P0}" +
                          $"{(v.Impact is { } i ? $"; влияние {i.ToString("N1", Ru)} {v.ImpactUnit}" : "")}. За: {string.Join(" ", v.Supporting)} Против: {string.Join(" ", v.Refuting)}" +
                          $"{(v.Needed.Count > 0 ? " Не хватает: " + string.Join("; ", v.Needed) : "")}");
        }

        if (contradictions.Count > 0)
        {
            sb.AppendLine().AppendLine("Противоречия в данных:");
            foreach (var c in contradictions)
            {
                sb.AppendLine($"- {c.Text}");
            }
        }

        if (objections.Count > 0)
        {
            sb.AppendLine().AppendLine("Возражения скептика (учти: назови, что они меняют или почему не меняют вывод):");
            foreach (var o in objections)
            {
                sb.AppendLine($"- {o}");
            }
        }

        if (req.Readiness is not null)
        {
            sb.AppendLine().AppendLine(req.Readiness).AppendLine(AnalytikEngine.ComposeReadinessRules);
        }

        if (req.Remembered is not null)
        {
            sb.AppendLine().AppendLine("Что аналитики выяснили раньше в этом чате:").AppendLine(req.Remembered);
        }

        if (req.Forbidden.Count > 0)
        {
            sb.AppendLine().AppendLine($"Аналитики без доступных пользователю данных: {string.Join(", ", req.Forbidden.Select(f => f.Name))}. Если вопрос о их области — скажи, " +
                                       "что у пользователя нет доступа, и не оценивай.");
        }

        return sb.ToString();
    }

    /// <summary>Таблица гипотез — пишет код (статусы не может исказить модель).</summary>
    internal static string HypothesisTable(IReadOnlyList<HypothesisVerdict> verdicts)
    {
        if (verdicts.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.AppendLine("**Проверка гипотез**");
        sb.AppendLine();
        sb.AppendLine("| Гипотеза | Статус | Вид вывода | Уверенность | Данные |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var v in verdicts)
        {
            var evidence = v.Supporting.Concat(v.Refuting).FirstOrDefault() ?? v.Needed.FirstOrDefault() ?? "—";
            sb.AppendLine($"| {Cell(v.Hypothesis.Statement)} | {HypothesisStatuses.Title(v.Status)} | {EvidenceClasses.Title(v.EvidenceClass)} | {v.Confidence:P0} | {Cell(evidence)} |");
        }

        return sb.ToString();
    }

    private static string Footer(Guid id, IReadOnlyList<StepOutcome> steps, IReadOnlyList<HypothesisVerdict> verdicts, IReadOnlyList<ReviewCycle> cycles, int replans) =>
        $"\n_Ход исследования: шагов {steps.Count}, гипотез {verdicts.Count}{(replans > 0 ? $", план дополнялся {replans} раз" : "")}, проверок ответа {cycles.Count} — " +
        $"[открыть](/analytik/investigations/{id})._";

    private static string Cell(string s) => Cut(s.Replace('|', '/').Replace('\n', ' '), 220);

    private static string? History(IReadOnlyList<LlmMessage> history) => history.Count == 0
        ? null
        : string.Join("\n", history.TakeLast(4).Select(m => $"{(m.Role == LlmRole.User ? "Вопрос" : "Ответ")}: {Cut(m.Content, 400)}"));

    private static string Cut(string s, int max) => s.Length > max ? s[..max] + "…" : s;

    private async Task SaveAsync(AnalytikInvestigation record, CancellationToken ct)
    {
        try
        {
            if (adb.Investigations.Local.All(i => i.Id != record.Id))
            {
                adb.Investigations.Add(record);
            }

            await adb.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Аналитик: исследование {Id} не сохранено", record.Id);
        }
    }

    /// <summary>Состояние исследования: роли, трасса, счётчики, числа расчётов (доказательства ответа); события прогресса — по одному.</summary>
    private sealed class State(Stopwatch watch)
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        public readonly List<TraceEvent> Events = [];
        public readonly HashSet<string> Roles = [];
        public readonly StringBuilder ComputationContent = new();
        public int Computations;
        public int LlmCalls;

        public void Trace(string kind, string text)
        {
            lock (Events)
            {
                Events.Add(new TraceEvent(watch.ElapsedMilliseconds, kind, text.Length > 600 ? text[..600] + "…" : text));
            }
        }

        public async Task ProgressAsync(Func<ConsultantProgress, Task> progress, ConsultantProgress p)
        {
            await _gate.WaitAsync(CancellationToken.None);
            try
            {
                await progress(p);
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}
