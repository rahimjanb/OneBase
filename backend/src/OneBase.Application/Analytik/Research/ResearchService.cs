using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneBase.Application.Abstractions;
using OneBase.Application.Analytik.Compute;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Semantic;
using OneBase.Application.Sales.Promo;
using OneBase.Application.Security;
using OneBase.Domain.Analytik;

namespace OneBase.Application.Analytik.Research;

/// <summary>Открытый риск Аналитика для исследования состояния компании.</summary>
public sealed record ResearchRisk(string Title, string Severity, string Module, decimal? Impact, string? ImpactUnit, string? Cause, DateTimeOffset DetectedAt);

/// <summary>Результаты операций исследования: срезы, расчёты (с результатом JSON), эффекты акций, открытые риски.</summary>
public sealed class ResearchData
{
    public ConcurrentDictionary<string, AnalyticsResult> Queries { get; } = new();
    public ConcurrentDictionary<string, (ComputeOutcome Outcome, JsonElement Result)> Computes { get; } = new();
    public IReadOnlyList<PromoEffect>? Promo { get; set; }
    public IReadOnlyList<ResearchRisk>? Risks { get; set; }
}

/// <summary>
/// Research Executor: выполняет план исследования. Операции без невыполненных зависимостей идут параллельно (каждая — в своей области DI,
/// не больше InvestigationParallelSteps одновременно), одинаковые срезы — один раз (SliceCache), у каждой операции — таймаут; отказ среза
/// исправляется Query Repair один раз (без бесконечных повторов), операция с упавшей зависимостью пропускается с причиной. Прогресс пишется в
/// analytik.Investigations после каждой операции; исследование можно отменить. Выводы строятся кодом (ResearchFindings) только из успешных
/// операций; модель в исследовании не участвует.
/// </summary>
public sealed class ResearchService(
    IServiceScopeFactory scopes,
    SemanticEngine engine,
    AnalytikAccess access,
    IAnalytikDb adb,
    IAppDbContext db,
    AnalytikOptions options,
    TimeProvider clock,
    ILogger<ResearchService> logger)
{
    private static readonly ConcurrentDictionary<Guid, (Guid? Owner, CancellationTokenSource Cts)> Running = new();

    /// <summary>Отменить идущее исследование (своё; чужое — руководителю Аналитика). false — не идёт или чужое.</summary>
    public async Task<bool> CancelAsync(Guid id, Guid? userId, CancellationToken ct)
    {
        if (!Running.TryGetValue(id, out var run))
        {
            return false;
        }

        if (run.Owner != userId && userId is { } uid && !(await access.ResolveAsync(uid, ct)).Manager)
        {
            return false;
        }

        await run.Cts.CancelAsync();
        return true;
    }

    public async Task<ResearchResult> RunAsync(ResearchRequest request, ResearchCaller caller, CancellationToken ct, Func<string, Task>? progress = null)
    {
        var total = Stopwatch.StartNew();
        var principal = caller.UserId is { } id ? await access.ResolveAsync(id, ct)
            : caller.Origin is "proactive" or "benchmark" ? AnalytikPrincipal.SystemPrincipal
            : throw new UnauthorizedAccessException("Исследование без пользователя — только для фоновых задач Аналитика.");
        if (!principal.HasAny([Permissions.SalesRead, Permissions.FinanceRead]))
        {
            throw new UnauthorizedAccessException("Для исследования нужно право на продажи или финансы.");
        }

        var ranges = new Dictionary<string, (DateOnly? First, DateOnly? Last)>();
        foreach (var fact in new[] { SemanticCatalog.Sales, SemanticCatalog.Payments, SemanticCatalog.Visits, SemanticCatalog.Plan })
        {
            ranges[fact] = await engine.DataRangeAsync(fact, ct);
        }

        var syncedAt = (await engine.SyncTimesAsync(ct)).GetValueOrDefault("orders").At;
        var planWatch = Stopwatch.StartNew();
        var promos = request.Kind == ResearchKinds.PromoEffect ? await PromotionsAsync(request, principal, ranges, syncedAt, ct) : [];
        var plan = ResearchPlanner.Build(request, new ResearchContext(principal, engine.Today, ranges, promos, syncedAt));
        planWatch.Stop();

        var record = new AnalytikInvestigation
        {
            UserId = caller.UserId, ConversationId = caller.ConversationId, Origin = caller.Origin.Length > 16 ? caller.Origin[..16] : caller.Origin,
            Question = Cut(plan.UserQuestion, 4000)!, Goal = Cut(plan.Objective, 2000), Complexity = plan.Operations.Count, Status = "running",
            Roles = JsonSerializer.Serialize(new[] { "research", plan.Kind }), StepCount = plan.Operations.Count, Depth = plan.Waves().Count,
            StartedAt = clock.GetUtcNow(),
        };
        adb.Investigations.Add(record);
        await adb.SaveChangesAsync(ct);
        plan.ResearchId = record.Id;
        plan.ExecutionStatus = "running";
        var trace = new List<object> { new { atMs = 0L, kind = "plan", text = $"План: {plan.Operations.Count} операций, волн {plan.Waves().Count}" } };
        record.Plan = JsonSerializer.Serialize(plan, ResearchJson.Options);
        await adb.SaveChangesAsync(ct);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Running[record.Id] = (caller.UserId, cts);
        var cache = new SliceCache();
        var data = new ResearchData();
        var gate = new SemaphoreSlim(1, 1);
        var cancelled = false;
        try
        {
            using var slots = new SemaphoreSlim(Math.Max(1, options.InvestigationParallelSteps));
            foreach (var wave in plan.Waves())
            {
                var ops = wave.Select(w => plan.Operations.First(o => o.Id == w)).Where(o => o.Status == ResearchStatuses.Pending).ToList();
                foreach (var op in ops.ToList())
                {
                    var failed = op.DependsOn.Select(d => plan.Operations.FirstOrDefault(o => o.Id == d)).Where(o => o is not null && !ResearchStatuses.Usable(o.Status)).ToList();
                    if (failed.Count > 0)
                    {
                        op.Status = ResearchStatuses.Skipped;
                        op.ErrorReason = $"Зависит от «{failed[0]!.Title}» ({ResearchStatuses.Title(failed[0]!.Status)}).";
                        ops.Remove(op);
                    }
                }

                await Task.WhenAll(ops.Select(async op =>
                {
                    await slots.WaitAsync(cts.Token);
                    try
                    {
                        if (progress is not null)
                        {
                            await progress(op.Title);
                        }

                        await RunOperationAsync(op, plan, record.Id, caller, principal, cache, data, cts.Token);
                        await gate.WaitAsync(CancellationToken.None);
                        try
                        {
                            trace.Add(new { atMs = total.ElapsedMilliseconds, kind = "step", text = $"{op.Title}: {ResearchStatuses.Title(op.Status)}{(op.FromCache ? " (из кэша)" : "")}" });
                            record.Steps = JsonSerializer.Serialize(plan.Operations, ResearchJson.Options);
                            record.Trace = JsonSerializer.Serialize(trace, ResearchJson.Options);
                            await adb.SaveChangesAsync(CancellationToken.None);
                        }
                        finally
                        {
                            gate.Release();
                        }
                    }
                    finally
                    {
                        slots.Release();
                    }
                }));
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            cancelled = true;
            foreach (var op in plan.Operations.Where(o => o.Status is ResearchStatuses.Pending or ResearchStatuses.Running))
            {
                op.Status = ResearchStatuses.Cancelled;
            }
        }
        finally
        {
            Running.TryRemove(record.Id, out _);
        }

        // Выводы — кодом, только из успешных операций; затем проверка доказательств.
        var evidenceWatch = Stopwatch.StartNew();
        var built = ResearchFindings.Build(plan, data);
        var findings = Validate(built.Findings, plan);
        evidenceWatch.Stop();

        var limitations = plan.Limitations.Concat(built.Limitations)
            .Concat(plan.Operations.Where(o => !ResearchStatuses.Usable(o.Status) && o.Status != ResearchStatuses.Pending)
                .Select(o => $"«{o.Title}» — {ResearchStatuses.Title(o.Status)}{(o.ErrorReason is { Length: > 0 } e ? $": {e}" : "")}"))
            .Concat(plan.Operations.Where(o => o.Status == ResearchStatuses.PartialResult && o.Note is { Length: > 0 }).Select(o => $"«{o.Title}»: {o.Note}"))
            .Distinct().ToList();
        var required = plan.Operations.Where(o => !o.Optional).ToList();
        var status = cancelled ? "cancelled"
            : required.All(o => ResearchStatuses.Usable(o.Status)) && plan.Operations.All(o => ResearchStatuses.Usable(o.Status) || o.Optional && o.Status != ResearchStatuses.Cancelled)
                ? plan.Operations.All(o => o.Status == ResearchStatuses.Success) ? "completed" : "partial"
            : findings.Count > 0 ? "partial" : "failed";
        plan.ExecutionStatus = status;

        var sqlMs = plan.Operations.Where(o => o.Type == "query").Sum(o => o.ElapsedMs);
        var statMs = plan.Operations.Where(o => o.Type == "compute").Sum(o => o.ElapsedMs);
        var timings = new ResearchTimings(planWatch.ElapsedMilliseconds, sqlMs, statMs, evidenceWatch.ElapsedMilliseconds, total.ElapsedMilliseconds,
            plan.Operations.Count, plan.Operations.Count(o => o.Type == "query"), plan.Operations.Count(o => o.Type == "compute"), cache.Hits,
            plan.Operations.Sum(o => Math.Max(0, o.Attempts - 1)), cache.HitRatio);
        var sources = data.Queries.Values.SelectMany(q => q.Sources).Concat(data.Computes.Values.SelectMany(c => c.Outcome.Sources))
            .GroupBy(s => s.Title + s.Tables).Select(g => g.First()).ToList();

        trace.Add(new { atMs = total.ElapsedMilliseconds, kind = "review", text = $"Выводов: {findings.Count}; статус: {status}" });
        record.Status = status;
        record.Plan = JsonSerializer.Serialize(plan, ResearchJson.Options);
        record.Steps = JsonSerializer.Serialize(plan.Operations, ResearchJson.Options);
        record.Hypotheses = JsonSerializer.Serialize(new { findings, hypotheses = built.Hypotheses, data = built.Data, timings, sources }, ResearchJson.Options);
        record.Limitations = JsonSerializer.Serialize(limitations, ResearchJson.Options);
        record.Trace = JsonSerializer.Serialize(trace, ResearchJson.Options);
        record.Summary = Cut(findings.FirstOrDefault(f => f.Section == "summary")?.Text ?? findings.FirstOrDefault()?.Text, 4000);
        record.ToolCalls = plan.Operations.Count;
        record.DurationMs = total.ElapsedMilliseconds;
        record.FailureReason = status == "failed" ? Cut(plan.Operations.FirstOrDefault(o => !ResearchStatuses.Usable(o.Status))?.Status.ToLowerInvariant(), 64) : null;
        record.FinishedAt = clock.GetUtcNow();
        await adb.SaveChangesAsync(CancellationToken.None);

        return new ResearchResult(record.Id, plan.Kind, ResearchKinds.Title(plan.Kind), status, plan, findings, built.Hypotheses, limitations, sources, timings, built.Data);
    }

    /// <summary>
    /// Проверка доказательств: вывод, опирающийся на операцию без результата, понижается до UNVERIFIED; опирающийся на неполный срез — получает
    /// пометку. Выводы без ссылок на операции (итоги плана, рекомендации) остаются как есть.
    /// </summary>
    private static List<ResearchFinding> Validate(IReadOnlyList<ResearchFinding> findings, ResearchPlan plan)
    {
        var byId = plan.Operations.ToDictionary(o => o.Id);
        var list = new List<ResearchFinding>();
        foreach (var f in findings)
        {
            var ops = (f.Evidence ?? []).Select(e => byId.GetValueOrDefault(e)).OfType<ResearchOperation>().ToList();
            if (ops.Any(o => !ResearchStatuses.Usable(o.Status)))
            {
                list.Add(f with { Level = EvidenceLevels.Unverified });
            }
            else if (ops.Any(o => o.Status == ResearchStatuses.PartialResult) && f.Level is EvidenceLevels.Fact or EvidenceLevels.Calculated or EvidenceLevels.Statistical)
            {
                list.Add(f with { Text = f.Text + " (по неполным данным)" });
            }
            else
            {
                list.Add(f);
            }
        }

        return list;
    }

    private async Task RunOperationAsync(ResearchOperation op, ResearchPlan plan, Guid researchId, ResearchCaller caller, AnalytikPrincipal principal, SliceCache cache,
        ResearchData data, CancellationToken ct)
    {
        op.Status = ResearchStatuses.Running;
        var watch = Stopwatch.StartNew();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, op.TimeoutSeconds)));
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        try
        {
            switch (op.Type)
            {
                case "query":
                    await QueryAsync(op, sp.GetRequiredService<SemanticEngine>(), caller, principal, cache, data, timeout.Token);
                    break;
                case "compute":
                    await ComputeAsync(op, sp.GetRequiredService<ComputeService>(), researchId, caller, principal, cache, data, timeout.Token);
                    break;
                case "promo":
                    op.Attempts++;
                    var view = await sp.GetRequiredService<PromoService>().GetAsync(op.Parameters?.GetValueOrDefault("month"), null, [], timeout.Token);
                    data.Promo = view?.Effects ?? [];
                    op.Status = view is null || view.Effects.Count == 0 ? ResearchStatuses.DataNotFound : ResearchStatuses.Success;
                    op.ErrorReason = view is null ? "Строк со скидками за год в OneBase нет." : view.Effects.Count == 0 ? "Эффект акций за месяц не посчитан (нет акций с базой сравнения)." : null;
                    op.Summary = view is null ? null : $"акций с эффектом: {view.Effects.Count}";
                    break;
                case "risks":
                    op.Attempts++;
                    if (principal.Sales.Restricted)
                    {
                        op.Status = ResearchStatuses.NoAccess;
                        op.ErrorReason = "Риски Аналитика — по всей организации, а ваша область ограничена.";
                        break;
                    }

                    var rows = await sp.GetRequiredService<IAnalytikDb>().Insights.AsNoTracking().Where(i => i.ResolvedAt == null)
                        .OrderByDescending(i => i.LastSeenAt).Take(200).ToListAsync(timeout.Token);
                    data.Risks = rows.Where(i => i.RequiredPermission is null || principal.Has(i.RequiredPermission))
                        .OrderBy(i => i.Severity == "critical" ? 0 : i.Severity == "warning" ? 1 : 2).ThenByDescending(i => i.Impact ?? 0).Take(15)
                        .Select(i => new ResearchRisk(i.Title, i.Severity, i.Module, i.Impact, i.ImpactUnit, i.CauseConfirmed ? i.Cause : null, i.DetectedAt)).ToList();
                    op.Status = ResearchStatuses.Success;
                    op.Summary = $"открытых рисков: {data.Risks.Count}";
                    break;
                default:
                    op.Status = ResearchStatuses.ToolUnsupported;
                    op.ErrorReason = $"Вид операции «{op.Type}» не поддерживается.";
                    break;
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            op.Status = ResearchStatuses.SourceUnavailable;
            op.ErrorReason = $"Операция не уложилась в {op.TimeoutSeconds} с — сузьте период или условия.";
        }
        catch (UnauthorizedAccessException ex)
        {
            op.Status = ResearchStatuses.NoAccess;
            op.ErrorReason = ex.Message;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Исследование: операция {Op} не выполнена", op.Id);
            op.Status = ResearchStatuses.SourceUnavailable;
            op.ErrorReason = "Внутренняя ошибка OneBase при выполнении операции — данные не получены (это сбой, а не отсутствие данных).";
        }
        finally
        {
            op.ElapsedMs = watch.ElapsedMilliseconds;
        }
    }

    private static async Task QueryAsync(ResearchOperation op, SemanticEngine engine, ResearchCaller caller, AnalytikPrincipal principal, SliceCache cache,
        ResearchData data, CancellationToken ct)
    {
        var query = op.Query ?? throw new InvalidOperationException("У операции среза нет запроса.");
        var qc = new QueryCaller(caller.UserId, caller.Origin is "chat" or "api" ? caller.Origin : "research", caller.AgentCode, caller.ConversationId);
        var key = $"{principal.UserId}|{principal.Sales.Description}";
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            op.Attempts = attempt;
            var hits = cache.Hits;
            try
            {
                var r = await cache.GetAsync(query, key, () => engine.RunAsync(query, qc, ct, principal));
                op.FromCache = cache.Hits > hits;
                data.Queries[op.Id] = r;
                op.ResultReference = r.QueryId.ToString();
                op.Status = r.Complete ? ResearchStatuses.Success : ResearchStatuses.PartialResult;
                op.Summary = $"строк: {r.TotalRows}";
                if (!r.Complete)
                {
                    op.Note = string.Join(" ", r.Checks.Where(c => !c.Ok).Select(c => c.Text));
                }

                return;
            }
            catch (AnalyticsQueryException ex) when (ex.Code == "denied")
            {
                op.Status = ResearchStatuses.NoAccess;
                op.ErrorReason = ex.Message;
                return;
            }
            catch (AnalyticsQueryException ex)
            {
                // Исправление строит код и выполняется один раз; второй отказ — статус с причиной, без новых попыток.
                var repair = attempt == 1 ? QueryRepair.Suggest(query, ex) : null;
                if (repair is { Safe: true, Queries.Count: 1 })
                {
                    query = repair.Queries[0];
                    op.Note = $"Исправлено автоматически: {repair.Hint}";
                    continue;
                }

                op.Status = ResearchStatuses.OfQuery(ex.Code, ex.Message);
                op.ErrorReason = ex.Message;
                return;
            }
        }
    }

    private static async Task ComputeAsync(ResearchOperation op, ComputeService compute, Guid researchId, ResearchCaller caller, AnalytikPrincipal principal,
        SliceCache cache, ResearchData data, CancellationToken ct)
    {
        op.Attempts++;
        var request = op.Compute ?? throw new InvalidOperationException("У операции расчёта нет запроса.");
        var hits = cache.Hits;
        var outcome = await compute.RunAsync(request, new ComputeContext(caller.UserId, "research", researchId, caller.ConversationId, caller.AgentCode, cache), ct, principal);
        op.FromCache = cache.Hits > hits;
        op.ResultReference = outcome.Id.ToString();
        op.Summary = outcome.Summary is { Length: > 600 } s ? s[..600] + "…" : outcome.Summary;
        switch (outcome.Status)
        {
            case "ok":
                op.Status = outcome.Limitations.Any(l => l.StartsWith("Срез данных неполный", StringComparison.Ordinal)) ? ResearchStatuses.PartialResult : ResearchStatuses.Success;
                data.Computes[op.Id] = (outcome, outcome.Result is null ? default : JsonSerializer.SerializeToElement(outcome.Result, ComputeService.JsonOptions));
                break;
            case "insufficient":
                var error = outcome.Error ?? "";
                op.Status = error.Contains("нет", StringComparison.OrdinalIgnoreCase) && (error.Contains("данн", StringComparison.OrdinalIgnoreCase)
                                                                                           || error.Contains("покупател", StringComparison.OrdinalIgnoreCase)
                                                                                           || error.Contains("не найден", StringComparison.OrdinalIgnoreCase))
                    ? ResearchStatuses.DataNotFound
                    : error.Contains("нужно", StringComparison.OrdinalIgnoreCase) || error.Contains("мало", StringComparison.OrdinalIgnoreCase)
                        ? ResearchStatuses.InsufficientGranularity
                        : ResearchStatuses.CalculationFailed;
                op.ErrorReason = error;
                break;
            default:
                op.Status = ResearchStatuses.CalculationFailed;
                op.ErrorReason = outcome.Error;
                break;
        }
    }

    /// <summary>Акции Linko, идущие в периоде исследования, с товарами — крупнейшие по числу товаров.</summary>
    private async Task<IReadOnlyList<PromoInfo>> PromotionsAsync(ResearchRequest r, AnalytikPrincipal principal, Dictionary<string, (DateOnly? First, DateOnly? Last)> ranges,
        DateTimeOffset? syncedAt, CancellationToken ct)
    {
        if (!principal.Has(Permissions.SalesRead))
        {
            return [];
        }

        var (from, to) = ResearchPlanner.Period(r, new ResearchContext(principal, engine.Today, ranges, [], syncedAt), 60);
        var promos = await db.LinkoPromotions.AsNoTracking()
            .Where(p => p.BeginDate != null && p.BeginDate <= to && (p.TillDate == null || p.TillDate >= from))
            .Select(p => new { p.Id, p.Name, p.BeginDate, p.TillDate })
            .ToListAsync(ct);
        var ids = promos.Select(p => p.Id).ToList();
        var products = await db.LinkoPromotionProducts.AsNoTracking().Where(p => ids.Contains(p.PromotionId)).Select(p => new { p.PromotionId, p.ProductId }).ToListAsync(ct);
        var byPromo = products.GroupBy(p => p.PromotionId).ToDictionary(g => g.Key, g => (IReadOnlyList<long>)g.Select(x => x.ProductId).Distinct().ToList());
        // Акции в одни даты с теми же или почти теми же товарами по продажам неразличимы — проверяются один раз (ResearchPlanner.MergeSimilar).
        return ResearchPlanner.MergeSimilar(promos.Where(p => byPromo.ContainsKey(p.Id) && p.BeginDate >= from.AddDays(-30))
                .Select(p => new PromoInfo(p.Id, p.Name, p.BeginDate!.Value, p.TillDate ?? to, byPromo[p.Id]))
                .Where(p => p.Products.Count <= 200), from, to)
            .OrderByDescending(p => p.Products.Count).ThenByDescending(p => p.Begin).Take(5).ToList();
    }

    private static string? Cut(string? s, int max) => s is { } v && v.Length > max ? v[..max] : s;
}
