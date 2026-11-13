using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Analytik.Simulation;
using OneBase.AI.Consultant;
using OneBase.AI.Grounding;
using OneBase.AI.Monitoring;
using OneBase.AI.Tools;
using OneBase.Application.Abstractions;
using OneBase.Application.Analytik;
using OneBase.Application.Analytik.Compute;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Semantic;
using OneBase.Application.Analytik.Simulation;
using OneBase.Application.Security;
using OneBase.Domain.AI;
using OneBase.Domain.Analytik;

namespace OneBase.AI.Analytik.Intelligence;

/// <summary>Итог сценария: прошёл ли, ожидание и факт, подробности (числа эталона), не прошедшие проверки, оценка живого прогона.</summary>
public sealed record BenchmarkCaseResult(string Id, string Category, bool Passed, string Expected, string Actual, string? Detail, IReadOnlyList<string> Failures,
    double? Score = null, object? Value = null);

/// <summary>Итог эталонной операции: статус и результат (для сверки с независимым расчётом в тестах и с ответом модели в живом прогоне).</summary>
public sealed record GoldenOutcome(string Status, object? Value, string? Detail);

/// <summary>
/// Исполнитель набора сложных вопросов. Регрессия (бесплатно): эталонная операция каждого сценария — на текущих данных и правилах; статус
/// сравнивается с ожидаемым. Живой прогон (платно, только с подтверждением и правом управления Аналитиком): вопрос задаётся чату Аналитика,
/// ответ оценивается — глубина, инструменты, упоминания, неподтверждённые числа, совпадение чисел с эталоном; бюджет прогона ограничен.
/// </summary>
public sealed class BenchmarkRunner(IServiceScopeFactory scopes, IAnalytikDb adb, IAppDbContext db, TimeProvider clock)
{
    /// <summary>Источник запросов набора в журналах — их не считает сводка качества (провокационные сценарии ошибаются нарочно).</summary>
    public const string Origin = "bench";

    /// <summary>Подстановки по данным компании: три крупнейших региона и две группы прошлого полного месяца.</summary>
    public async Task<BenchmarkContext> ContextAsync(AnalytikPrincipal p, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<SemanticEngine>();
        var today = engine.Today;
        var m0From = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
        var m0To = m0From.AddMonths(1).AddDays(-1);
        var m1From = m0From.AddMonths(-1);
        var m1To = m1From.AddDays(m0To.DayNumber - m0From.DayNumber);
        var caller = new QueryCaller(p.UserId, Origin);
        async Task<string[]> Top(string dim, int n)
        {
            var r = await engine.RunAsync(new AnalyticsQuery
            {
                Metrics = ["revenue"], Dimensions = [dim], DateFrom = m0From, DateTo = m0To, Sort = [new AnalyticsSort("revenue", "desc")], Limit = n, IncludeTotals = false,
            }, caller, ct, p);
            return r.Rows.Select(row => row[0]?.ToString() ?? "").Where(x => x.Length > 0).ToArray();
        }

        var regions = await Top("region", 3);
        var groups = await Top("product_group", 2);
        StatMath.Require(regions.Length == 3 && groups.Length == 2, "Для набора нужны продажи хотя бы трёх регионов и двух групп за прошлый месяц.");
        return new BenchmarkContext(regions, groups, m0From, m0To, m1From, m1To, m0From, m0From.AddDays(14), m0From.AddMonths(-3));
    }

    /// <summary>Эталонная операция сценария — тем же кодом, что у аналитиков. restricted — пользователь без права финансов (сценарии прав).</summary>
    public async Task<GoldenOutcome> GoldenAsync(BenchmarkCase c, BenchmarkContext ctx, AnalytikPrincipal p, CancellationToken ct)
    {
        if (c.Golden is null)
        {
            return new GoldenOutcome("none", null, null);
        }

        var op = c.Golden(ctx);
        var principal = op.Restricted
            ? p with { Permissions = p.Permissions.Where(x => x != Permissions.FinanceRead).ToHashSet() }
            : p;
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        try
        {
            switch (op.Kind)
            {
                case "slice":
                {
                    var r = await sp.GetRequiredService<SemanticEngine>().RunAsync(op.Slice!, new QueryCaller(p.UserId, Origin), ct, principal);
                    return new GoldenOutcome("ok", r, $"строк {r.TotalRows}");
                }

                case "compute":
                {
                    var o = await sp.GetRequiredService<ComputeService>().RunAsync(op.Compute!, new ComputeContext(p.UserId, "benchmark"), ct, principal);
                    return new GoldenOutcome(o.Status, o, o.Summary ?? o.Error);
                }

                case "scenario":
                {
                    var service = sp.GetRequiredService<ScenarioAnalysisService>();
                    var input = op.Scenario!;
                    if (op.GrowthPercent is { } growth && input.Mode == "goal_seek")
                    {
                        // Цель — рост показателя на growth % от базы: база берётся прогоном того же снимка без изменений.
                        var scenarios = sp.GetRequiredService<ScenarioService>();
                        var (_, data) = await scenarios.BaselineDataAsync(principal, null, input.From, input.To, ct);
                        var baseRun = ScenarioEngine.Run(data, new ScenarioDefinition(ScenarioKinds.Combined, input.HorizonDays ?? 30, []));
                        var baseValue = ScenarioAnalysis.Value(baseRun, input.Metric ?? "sales.revenue")
                                        ?? throw new ComputeException("Показатель цели не считается на этих данных.");
                        input = input with { Target = Math.Round(baseValue * (1 + growth / 100), 0) };
                    }

                    var view = await service.AnalyzeAsync(principal, input, "benchmark", ct);
                    var status = view.Result is GoalSeekResult g ? g.Status : "ok";
                    return new GoldenOutcome(status, view, view.Summary);
                }

                case "repair":
                {
                    try
                    {
                        await sp.GetRequiredService<SemanticEngine>().RunAsync(op.Slice!, new QueryCaller(p.UserId, Origin), ct, principal);
                        return new GoldenOutcome("ok", null, "срез выполнен без исправления");
                    }
                    catch (AnalyticsQueryException ex)
                    {
                        var repair = QueryRepair.Suggest(op.Slice!, ex);
                        return repair is { Queries.Count: > 0 } ? new GoldenOutcome("repaired", repair, repair.Hint) : new GoldenOutcome(ex.Code, null, ex.Message);
                    }
                }

                case "triage":
                {
                    var t = InvestigationTriage.Assess(ctx.Fill(op.Text!), [], "auto", 3, 6);
                    return new GoldenOutcome(t.Investigate ? "investigate" : "direct", t, string.Join(", ", t.Signals));
                }

                case "methods":
                {
                    var m = ComputeCatalog.Expert.FirstOrDefault(x => x.Code == op.Text);
                    return new GoldenOutcome(m?.Kind ?? "unknown", m, m?.Note);
                }

                case "review":
                {
                    var verdicts = (op.Verdicts ?? []).Select((v, i) => new HypothesisVerdict(new PlannedHypothesis($"h{i + 1}", v.Statement, null, new HypothesisExpectation()),
                        v.Status, EvidenceClasses.Observed, 0.6, [], [], [], null, null, null, null)).ToList();
                    var issues = InvestigationReviewer.Check(op.Text!, c.Question, new GroundingSummary(0, 0, [], [], false, false), [], verdicts, []);
                    return new GoldenOutcome(issues.Any(i => i.Severity == "error") ? "flagged" : "clean", issues, string.Join("; ", issues.Select(i => i.Kind)));
                }

                case "contradiction":
                {
                    var parts = op.Text!.Split('|');
                    StepOutcome Step(string id, string value) =>
                        new(new InvestigationStep(id, "?", "analyst.sales", [], null), "completed", "", [], [new Metric(parts[0], value, "сум")], [], 0, [], 0);
                    var found = InvestigationReviewer.Contradictions([Step("s1", parts[1]), Step("s2", parts[2])], []);
                    return new GoldenOutcome(found.Count > 0 ? "flagged" : "clean", found, null);
                }

                case "plan":
                {
                    using var doc = JsonDocument.Parse(op.Text!);
                    var steps = InvestigationJson.Steps(doc.RootElement, new HashSet<string> { "analyst.sales" }, "analyst.sales", 8, 0);
                    var waves = InvestigationJson.Waves(steps);
                    return new GoldenOutcome(waves.Sum(w => w.Count) == steps.Count ? "ok" : "broken", waves, $"волн {waves.Count}");
                }

                case "level":
                    return new GoldenOutcome(ToolLevels.MaxFor(op.Text!) < ToolLevels.Execute ? "denied" : "allowed", null, ToolLevels.Title(ToolLevels.MaxFor(op.Text!)));

                default:
                    return new GoldenOutcome("unknown", null, op.Kind);
            }
        }
        catch (AnalyticsQueryException ex)
        {
            return new GoldenOutcome(ex.Code, null, ex.Message);
        }
        catch (ComputeException ex)
        {
            return new GoldenOutcome("insufficient", null, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return new GoldenOutcome("denied", null, ex.Message);
        }
    }

    /// <summary>Регрессия: все сценарии с эталоном на текущих данных; итог — в analytik.BenchmarkRuns (mode regression, без платных вызовов).</summary>
    public async Task<AnalytikBenchmarkRun> RegressionAsync(AnalytikPrincipal p, BenchmarkContext? ctx, CancellationToken ct)
    {
        var run = new AnalytikBenchmarkRun { Mode = "regression", Status = "running", StartedAt = clock.GetUtcNow(), StartedById = p.UserId, Total = BenchmarkCatalog.Cases.Count };
        adb.BenchmarkRuns.Add(run);
        await adb.SaveChangesAsync(ct);
        var results = new List<BenchmarkCaseResult>();
        try
        {
            ctx ??= await ContextAsync(p, ct);
            // Прошлый прогон регрессии: статус сценария, зависящий от данных компании (есть ли планы, оплаты, остатки), сравнивается и с ним —
            // регрессия — это изменение того, что работало, а не расхождение с ожиданием тестового мира.
            var previous = await PreviousAsync(ct);
            foreach (var c in BenchmarkCatalog.Cases)
            {
                var g = await GoldenAsync(c, ctx, p, ct);
                var was = previous.GetValueOrDefault(c.Id);
                var ok = g.Status == c.Check.Status || (was is not null && g.Status == was);
                var failures = new List<string>();
                if (!ok)
                {
                    failures.Add(was is null ? $"ожидалось {c.Check.Status}, получено {g.Status}" : $"было {was}, стало {g.Status} (ожидалось {c.Check.Status})");
                }

                results.Add(new BenchmarkCaseResult(c.Id, c.Category, ok, c.Check.Status, g.Status, g.Detail, failures));
            }

            Finish(run, results, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            run.Status = "failed";
            run.Error = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            run.FinishedAt = clock.GetUtcNow();
        }

        await adb.SaveChangesAsync(CancellationToken.None);
        return run;
    }

    /// <summary>Статусы сценариев последнего завершённого прогона регрессии (код → статус).</summary>
    private async Task<IReadOnlyDictionary<string, string>> PreviousAsync(CancellationToken ct)
    {
        var last = await adb.BenchmarkRuns.AsNoTracking().Where(r => r.Mode == "regression" && r.Status == "completed").OrderByDescending(r => r.StartedAt)
            .Select(r => r.Results).FirstOrDefaultAsync(ct);
        if (last is null)
        {
            return new Dictionary<string, string>();
        }

        using var doc = JsonDocument.Parse(last);
        return doc.RootElement.EnumerateArray()
            .Where(x => x.TryGetProperty("id", out _) && x.TryGetProperty("actual", out _))
            .ToDictionary(x => x.GetProperty("id").GetString()!, x => x.GetProperty("actual").GetString() ?? "");
    }

    /// <summary>
    /// Живой прогон моделью — ПЛАТНЫЙ: только с подтверждением (confirm) и правом управления Аналитиком; вопросы задаются чату Аналитика от имени
    /// пользователя, ответ оценивается. Останавливается, когда расход прогона дошёл до maxCostUsd.
    /// </summary>
    public async Task<AnalytikBenchmarkRun> LiveAsync(AnalytikPrincipal p, IReadOnlyList<string>? ids, decimal maxCostUsd, bool confirm, CancellationToken ct)
    {
        if (!confirm)
        {
            throw new ArgumentException("Живой прогон — платные вызовы модели: нужно явное подтверждение (confirm = true).");
        }

        if (!p.Manager || p.UserId is null)
        {
            throw new UnauthorizedAccessException("Живой прогон запускает управляющий Аналитиком.");
        }

        var cases = BenchmarkCatalog.Cases.Where(c => ids is not { Count: > 0 } || ids.Contains(c.Id)).ToList();
        var run = new AnalytikBenchmarkRun { Mode = "live", Status = "running", StartedAt = clock.GetUtcNow(), StartedById = p.UserId, Total = cases.Count };
        adb.BenchmarkRuns.Add(run);
        var conversation = new AiConversation { UserId = p.UserId.Value, Title = $"Набор сложных вопросов {clock.GetUtcNow():dd.MM HH:mm}", Kind = ConversationKinds.Analytik };
        db.AiConversations.Add(conversation);
        await adb.SaveChangesAsync(ct);
        var ctx = await ContextAsync(p, ct);
        var results = new List<BenchmarkCaseResult>();
        decimal spent = 0;
        foreach (var c in cases)
        {
            if (spent >= maxCostUsd)
            {
                results.Add(new BenchmarkCaseResult(c.Id, c.Category, false, "answer", "skipped", "бюджет прогона исчерпан", ["бюджет"]));
                continue;
            }

            var golden = await GoldenAsync(c, ctx, p, ct);
            var turn = AiTurnTimings.Start();
            try
            {
                using var scope = scopes.CreateScope();
                var engine = scope.ServiceProvider.GetRequiredService<AnalytikEngine>();
                var answer = await engine.AnswerAsync(new ConsultantTurn(p.UserId.Value, conversation.Id, ctx.Fill(c.Question), []), _ => Task.CompletedTask, ct);
                spent += turn.CostUsd;
                results.Add(Score(c, ctx, answer, golden));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                spent += turn.CostUsd;
                results.Add(new BenchmarkCaseResult(c.Id, c.Category, false, "answer", "error", ex.Message, ["ошибка ответа"]));
            }
            finally
            {
                AiTurnTimings.Detach();
            }
        }

        run.CostUsd = spent;
        run.Model = "chat";
        Finish(run, results, null);
        await adb.SaveChangesAsync(CancellationToken.None);
        return run;
    }

    /// <summary>
    /// Оценка ответа: глубина (исследование или прямой путь), инструменты (методы, срезы), упоминания и запреты, неподтверждённые числа, числа
    /// эталона (итог среза или результат расчёта) среди чисел ответа.
    /// </summary>
    public static BenchmarkCaseResult Score(BenchmarkCase c, BenchmarkContext ctx, ConsultantAnswer answer, GoldenOutcome golden)
    {
        var failures = new List<string>();
        var checks = 0;
        void Check(bool ok, string text)
        {
            checks++;
            if (!ok)
            {
                failures.Add(text);
            }
        }

        if (c.Check.Investigate is { } investigate)
        {
            Check((answer.Details.InvestigationId is not null) == investigate, investigate ? "вопрос не исследовался" : "лишнее исследование простого вопроса");
        }

        var tools = answer.Details.Agents.SelectMany(a => a.ToolsUsed).ToHashSet(StringComparer.Ordinal);
        if (c.Check.Methods is { Length: > 0 })
        {
            var needed = c.Check.Methods.Contains("analytik_scenario_analysis") ? "analytik_scenario_analysis" : ComputeTool.ToolName;
            Check(tools.Contains(needed) || answer.Details.InvestigationId is not null, $"не использован {needed}");
        }

        if (c.Check.Metrics is { Length: > 0 } || c.Check.Dimensions is { Length: > 0 })
        {
            Check(tools.Overlaps([AnalyticsQueryTool.ToolName, ComputeTool.ToolName, "get_sales", "get_sales_days", "analytik_sales_overview", "find_products"]),
                "данные не получены инструментом срезов или продаж");
        }

        foreach (var m in c.Check.Mention ?? [])
        {
            Check(answer.Content.Contains(ctx.Fill(m), StringComparison.OrdinalIgnoreCase), $"не упомянуто «{ctx.Fill(m)}»");
        }

        foreach (var a in c.Check.Avoid ?? [])
        {
            Check(!answer.Content.Contains(ctx.Fill(a), StringComparison.OrdinalIgnoreCase), $"упомянуто запрещённое «{ctx.Fill(a)}»");
        }

        Check(answer.Details.Grounding is not { Unsupported.Count: > 0 }, "в ответе неподтверждённые числа");
        if (golden is { Status: "ok", Value: AnalyticsResult r } && r.Totals.Values.FirstOrDefault(v => v is not null) is { } total)
        {
            var evidence = new EvidenceSet();
            evidence.Add(total);
            Check(NumberClaims.Find(answer.Content).Any(evidence.Supports), $"нет числа эталона ({total:N0})");
        }

        var score = checks == 0 ? 1 : (double)(checks - failures.Count) / checks;
        return new BenchmarkCaseResult(c.Id, c.Category, failures.Count == 0, "answer", failures.Count == 0 ? "passed" : "failed", golden.Detail, failures, score);
    }

    private static void Finish(AnalytikBenchmarkRun run, IReadOnlyList<BenchmarkCaseResult> results, string? error)
    {
        run.Passed = results.Count(r => r.Passed);
        run.Total = results.Count;
        run.Score = results.Count == 0 ? 0 : Math.Round((decimal)results.Average(r => r.Score ?? (r.Passed ? 1 : 0)), 4);
        run.ByCategory = JsonSerializer.Serialize(results.GroupBy(r => r.Category).ToDictionary(g => g.Key, g => new { total = g.Count(), passed = g.Count(x => x.Passed) }),
            InvestigationJson.Options);
        run.Results = JsonSerializer.Serialize(results.Select(r => new { r.Id, r.Category, r.Passed, r.Expected, r.Actual, r.Detail, r.Failures, r.Score }),
            InvestigationJson.Options);
        run.Status = error is null ? "completed" : "failed";
        run.Error = error;
        run.FinishedAt = DateTimeOffset.UtcNow;
    }
}
