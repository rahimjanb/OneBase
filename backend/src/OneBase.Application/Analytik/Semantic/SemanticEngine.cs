using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.Application.Abstractions;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Sales;
using OneBase.Domain.Analytik;
using OneBase.Domain.Sales;

namespace OneBase.Application.Analytik.Semantic;

/// <summary>
/// Universal Analytics Engine — смысловой слой над движком Аналитика: вопрос «выручка по товарным группам за каждый день по регионам»
/// превращается в запрос «показатели × измерения × шаг × период × фильтры», а не в выбор готового отчёта. Поток:
/// QueryPlanner (проверка и нормализация, названия → id) → SourceResolver (показатель → факт и источник) → SQL (белый список, параметры,
/// READ ONLY, таймаут) → Normalizer (подписи, нулевые дни по запросу, без интерполяции) → Calculator (доли, средние, выполнение и прогноз
/// плана, дни запаса, сравнение периодов) → EvidenceValidator (период и данные по, сопоставление регионов и групп, валюта, итоги, загрузки,
/// обрезка) → журнал analytik.Queries. Права и область продаж пользователя — те же, что у движка таблиц (AnalytikAccess).
/// </summary>
public sealed class SemanticEngine(
    IAppDbContext db,
    IAnalytikDb adb,
    IAnalytikSql sql,
    AnalytikAccess access,
    SemanticContextLoader contexts,
    SemanticResolver resolver,
    SemanticPlans plans,
    SalesOptions salesOptions,
    AnalytikOptions options,
    TimeProvider clock,
    ILogger<SemanticEngine> logger)
{
    /// <summary>Сколько групп может вернуть один срез до страниц (больше — сузить период или измерения).</summary>
    public const int MaxGroups = 60_000;

    public const int MaxDays = 732;
    public const int DefaultLimit = 500;
    public const int MaxLimit = 20_000;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private sealed class Row(object?[] keys)
    {
        public object?[] Keys { get; } = keys;
        public Dictionary<string, decimal?> Values { get; } = [];
        public Dictionary<string, decimal?> Prev { get; } = [];
        public bool Filled { get; init; }
    }

    private sealed record Normalized(
        IReadOnlyList<MetricDef> Requested,
        IReadOnlyList<MetricDef> All,
        IReadOnlyList<string> Dimensions,
        string Grain,
        DateOnly From,
        DateOnly To,
        DateOnly? CompareFrom,
        DateOnly? CompareTo,
        string Segment,
        string PlanSource,
        PlanKind PlanKind,
        int CoverDays,
        int Limit,
        int Offset);

    public DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddHours(5));

    public async Task<AnalyticsResult> RunAsync(AnalyticsQuery query, QueryCaller caller, CancellationToken ct, AnalytikPrincipal? principal = null)
    {
        var watch = Stopwatch.StartNew();
        principal ??= caller.UserId is { } id ? await access.ResolveAsync(id, ct)
            : caller.Origin == "job" ? AnalytikPrincipal.SystemPrincipal
            : throw new UnauthorizedAccessException("Срез без пользователя — только для фоновых задач Аналитика.");
        var log = new AnalytikQuery
        {
            UserId = caller.UserId, Tenant = options.Tenant, Origin = caller.Origin + ":semantic", AgentCode = caller.AgentCode,
            ConversationId = caller.ConversationId, Plan = JsonSerializer.Serialize(query, Json), Status = "ok",
        };
        try
        {
            var result = await ExecuteAsync(query, principal, log, watch, ct);
            log.RowCount = result.Rows.Count;
            log.Truncated = result.HasMore;
            log.DurationMs = watch.ElapsedMilliseconds;
            await SaveAsync(log, ct);
            return result with { QueryId = log.Id, ElapsedMs = watch.ElapsedMilliseconds };
        }
        catch (AnalyticsQueryException ex)
        {
            await FailAsync(log, watch, ex.Code, ex.Message, ct);
            throw;
        }
        catch (AnalytikTimeoutException ex)
        {
            await FailAsync(log, watch, "timeout", ex.Message, ct);
            throw new AnalyticsQueryException("timeout", ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Аналитик: срез не выполнен");
            await FailAsync(log, watch, "error", "Срез не выполнен из-за ошибки базы данных — упростите условия.", ct);
            throw new AnalyticsQueryException("error", "Срез не выполнен из-за ошибки базы данных — упростите условия или сузьте период.");
        }
    }

    // ---------- QueryPlanner ----------

    private Normalized Normalize(AnalyticsQuery q, AnalytikPrincipal p)
    {
        if (!string.IsNullOrWhiteSpace(q.Tenant) && !string.Equals(q.Tenant.Trim(), options.Tenant, StringComparison.OrdinalIgnoreCase))
        {
            throw new AnalyticsQueryException("denied", "Данные другой компании недоступны: в этой базе только данные текущей компании.");
        }

        if (q.Metrics.Count == 0)
        {
            throw new AnalyticsQueryException("invalid", "Не указан показатель (metrics), например revenue.");
        }

        var requested = new List<MetricDef>();
        foreach (var code in q.Metrics.Where(c => !string.IsNullOrWhiteSpace(c)))
        {
            var m = SemanticCatalog.Metric(code)
                    ?? throw new AnalyticsQueryException("invalid", $"Показатель «{code}» не найден. Есть: {string.Join(", ", SemanticCatalog.Metrics.Select(x => x.Code))}.");
            if (!requested.Contains(m))
            {
                requested.Add(m);
            }
        }

        if (requested.Count > 12)
        {
            throw new AnalyticsQueryException("invalid", "Не больше 12 показателей в одном срезе.");
        }

        var all = SemanticCatalog.Expand(requested);
        foreach (var m in all.Where(m => !p.Has(m.Permission)))
        {
            throw new AnalyticsQueryException("denied", $"Нет права на показатель «{m.Title}» ({m.Permission}).");
        }

        var facts = SemanticCatalog.FactsOf(requested);
        var dims = new List<string>();
        foreach (var raw in q.Dimensions ?? [])
        {
            var d = SemanticCatalog.Dimension(raw)
                    ?? throw new AnalyticsQueryException("invalid", $"Измерение «{raw}» не найдено. Есть: {string.Join(", ", SemanticCatalog.Dimensions.Select(x => x.Code))}.");
            foreach (var fact in facts.Where(f => !d.Facts.Contains(f)))
            {
                throw new AnalyticsQueryException("unsupported",
                    $"«{FactTitle(fact)}» нельзя разложить по измерению «{d.Title}»: {DimensionHint(fact)}");
            }

            if (!dims.Contains(d.Code))
            {
                dims.Add(d.Code);
            }
        }

        if (dims.Count > 5)
        {
            throw new AnalyticsQueryException("invalid", "Не больше 5 измерений в одном срезе — для подробностей используйте фильтр по значению.");
        }

        var grain = (q.Grain ?? "day").Trim().ToLowerInvariant();
        if (!SemanticCatalog.Grains.Contains(grain))
        {
            throw new AnalyticsQueryException("invalid", $"Шаг времени «{q.Grain}» не поддерживается: day, week, month, quarter, year.");
        }

        if (dims.Contains("date") && facts.Contains(SemanticCatalog.Plan) && grain is "day" or "week")
        {
            throw new AnalyticsQueryException("unsupported",
                "План продаж в OneBase — помесячный (Google Sheets / Linko): по дням и неделям он не делится, и движок его не распределяет. " +
                "Возьмите шаг month (план, факт и выполнение по месяцам) или факт по дням без плана.");
        }

        if (facts.Contains(SemanticCatalog.Stock) && facts.Count > 1)
        {
            throw new AnalyticsQueryException("unsupported", "Остатки (снимок на сейчас) считаются отдельным срезом, без показателей за период.");
        }

        if (facts.Contains(SemanticCatalog.Stock) && p.Sales.Restricted)
        {
            throw new AnalyticsQueryException("denied", "Остатки складов видны при доступе ко всей организации.");
        }

        var to = q.DateTo ?? Today;
        var from = q.DateFrom ?? new DateOnly(to.Year, to.Month, 1);
        if (from > to)
        {
            throw new AnalyticsQueryException("invalid", "Начало периода позже конца.");
        }

        if (to.DayNumber - from.DayNumber + 1 > MaxDays)
        {
            throw new AnalyticsQueryException("too_heavy", $"Период длиннее {MaxDays} дней — разбейте его или возьмите шаг крупнее за меньший период.");
        }

        DateOnly? cFrom = null, cTo = null;
        switch ((q.Compare ?? "").Trim().ToLowerInvariant())
        {
            case "":
            case "none":
                break;
            case "previous":
                var days = to.DayNumber - from.DayNumber + 1;
                // Целые месяцы сравниваются с такими же предыдущими месяцами (октябрь — с сентябрём), иначе — столько же дней перед периодом.
                if (from.Day == 1 && to == new DateOnly(to.Year, to.Month, 1).AddMonths(1).AddDays(-1))
                {
                    var months = (to.Year - from.Year) * 12 + to.Month - from.Month + 1;
                    cFrom = from.AddMonths(-months);
                    cTo = from.AddDays(-1);
                }
                else
                {
                    cFrom = from.AddDays(-days);
                    cTo = from.AddDays(-1);
                }

                break;
            case "year_ago":
                cFrom = from.AddYears(-1);
                cTo = to.AddYears(-1);
                break;
            case "custom":
                cFrom = q.CompareFrom ?? throw new AnalyticsQueryException("invalid", "Для compare=custom нужны compareFrom и compareTo.");
                cTo = q.CompareTo ?? throw new AnalyticsQueryException("invalid", "Для compare=custom нужны compareFrom и compareTo.");
                break;
            default:
                throw new AnalyticsQueryException("invalid", "compare — previous, year_ago или custom.");
        }

        if (cFrom is not null && facts.Contains(SemanticCatalog.Stock))
        {
            throw new AnalyticsQueryException("unsupported", "Остатки — снимок на сейчас: сравнение периодов для них невозможно (истории остатков нет).");
        }

        var segment = (q.Segment ?? "secondary").Trim().ToLowerInvariant();
        if (segment is not ("secondary" or "factory" or "all"))
        {
            throw new AnalyticsQueryException("invalid", "segment — secondary (вторичка), factory (Завод: экспорт и опт) или all.");
        }

        var planSource = (q.PlanSource ?? "auto").Trim().ToLowerInvariant();
        if (planSource is not ("auto" or "regions" or "agents"))
        {
            throw new AnalyticsQueryException("invalid", "planSource — auto, regions или agents.");
        }

        var kind = (q.PlanKind ?? "rop").Trim().ToLowerInvariant() switch
        {
            "rop" => PlanKind.Rop,
            "factory" => PlanKind.Factory,
            "primary" => PlanKind.Primary,
            _ => throw new AnalyticsQueryException("invalid", "planKind — rop, factory или primary."),
        };

        return new Normalized(requested, all, dims, grain, from, to, cFrom, cTo, segment, planSource, kind, Math.Clamp(q.CoverDays ?? 14, 1, 120),
            Math.Clamp(q.Limit ?? DefaultLimit, 1, MaxLimit), Math.Max(0, q.Offset ?? 0));
    }

    private static string FactTitle(string fact) => fact switch
    {
        SemanticCatalog.Plan => "План",
        SemanticCatalog.Payments => "Оплаты",
        SemanticCatalog.Stock => "Остатки",
        _ => "Продажи",
    };

    private static string DimensionHint(string fact) => fact switch
    {
        SemanticCatalog.Plan => "план задан по регионам и категориям (Google Sheets) или по ТП (Linko) на месяц.",
        SemanticCatalog.Payments => "оплата привязана к точке, агенту и дате, без товаров.",
        SemanticCatalog.Stock => "остаток — по товару и складу, без дат, регионов и агентов.",
        _ => "у строк продаж такого признака нет.",
    };

    // ---------- выполнение ----------

    private async Task<AnalyticsResult> ExecuteAsync(AnalyticsQuery query, AnalytikPrincipal principal, AnalytikQuery log, Stopwatch watch, CancellationToken ct)
    {
        var n = Normalize(query, principal);
        var ctx = await contexts.LoadAsync(ct);
        var notes = new List<string>();
        var facts = SemanticCatalog.FactsOf(n.Requested);
        var filters = new List<ResolvedFilter>();
        foreach (var f in query.Filters ?? [])
        {
            var d = SemanticCatalog.Dimension(f.Dimension)
                    ?? throw new AnalyticsQueryException("invalid", $"Фильтр: измерение «{f.Dimension}» не найдено.");
            foreach (var fact in facts.Where(x => !d.Facts.Contains(x)))
            {
                throw new AnalyticsQueryException("unsupported", $"«{FactTitle(fact)}» нельзя отфильтровать по «{d.Title}»: {DimensionHint(fact)}");
            }

            filters.Add(await resolver.ResolveAsync(f, d.Code, ctx, notes, ct));
        }

        var scope = principal.Sales;
        var sqlLog = new List<string>();
        var paramLog = new List<SqlParam>();
        var tables = new HashSet<string>();
        var dataThrough = new Dictionary<string, (DateOnly? First, DateOnly? Last)>();

        // Факты периода и сравнения.
        var rows = await FactsAsync(n, n.From, n.To, ctx, filters, scope, sqlLog, paramLog, tables, notes, ct);
        var totals = await TotalsAsync(n, n.From, n.To, ctx, filters, scope, ct);
        Dictionary<string, Row>? compareRows = null;
        Dictionary<string, decimal?>? compareTotals = null;
        if (n.CompareFrom is { } cf && n.CompareTo is { } cto)
        {
            compareRows = await FactsAsync(n, cf, cto, ctx, filters, scope, null, null, tables, [], ct);
            compareTotals = await TotalsAsync(n, cf, cto, ctx, filters, scope, ct);
        }

        foreach (var fact in facts)
        {
            dataThrough[fact] = await DataRangeAsync(fact, ct);
        }

        var salesThrough = dataThrough.TryGetValue(SemanticCatalog.Sales, out var s) ? s.Last : null;

        // Сравнение: строки сопоставляются по измерениям; у даты — по номеру шага от начала периода.
        if (compareRows is not null)
        {
            var dateIndex = n.Dimensions.ToList().IndexOf("date");
            var byAligned = compareRows.Values.ToDictionary(r => AlignedKey(r.Keys, dateIndex, n.Grain, n.CompareFrom!.Value));
            foreach (var r in rows.Values)
            {
                if (byAligned.TryGetValue(AlignedKey(r.Keys, dateIndex, n.Grain, n.From), out var prev))
                {
                    foreach (var (k, v) in prev.Values)
                    {
                        r.Prev[k] = v;
                    }
                }
            }
        }

        // Нулевые шаги для графиков: только явно по запросу, нулём, без интерполяции.
        var filled = 0;
        if (query.FillZeros && n.Dimensions.Contains("date") && !facts.Contains(SemanticCatalog.Plan))
        {
            filled = FillZeros(rows, n);
            if (filled > 0)
            {
                notes.Add($"Добавлено {filled} шагов без продаж (нулём) — для непрерывного графика; это не данные, а их отсутствие.");
            }
        }

        // Калькулятор.
        var stockVelocity = facts.Contains(SemanticCatalog.Stock) && n.Requested.Any(m => m.Code is "cover_days" or "need_units")
            ? await VelocityAsync(n, ctx, salesThrough ?? Today, notes, ct)
            : null;
        Calculate(rows.Values, n, totals, salesThrough, stockVelocity, notes);
        CalculateTotals(totals, n, salesThrough, stockVelocity);
        if (compareTotals is not null)
        {
            CalculateTotals(compareTotals, n, salesThrough, stockVelocity);
            foreach (var r in rows.Values)
            {
                CalculateValues(r.Prev, n, null, salesThrough, null, null);
            }
        }

        // Проверка доказательств — до подписей и обрезки (по всем строкам).
        var checks = await EvidenceAsync(n, facts, rows.Values.ToList(), totals, filters, scope, dataThrough, notes, ct);

        // Подписи.
        var labels = new List<Dictionary<object, string>>();
        for (var i = 0; i < n.Dimensions.Count; i++)
        {
            var dim = n.Dimensions[i];
            var ids = rows.Values.Select(r => r.Keys[i]).OfType<object>().Distinct().ToList();
            labels.Add(dim == "date" ? [] : await resolver.LabelsAsync(dim, ids, ctx, ct));
        }

        // Сортировка и страница.
        var sorted = Sort(rows.Values, n, query.Sort, labels).ToList();
        var page = sorted.Skip(n.Offset).Take(n.Limit).ToList();
        var columns = Columns(n, compareRows is not null);
        var outRows = page.Select(r => RowValues(r, n, labels, compareRows is not null)).ToList();
        var keys = page.Select(r => r.Keys.Select(k => k is DateOnly d ? d.ToString("yyyy-MM-dd") : k).ToArray()).ToList();

        if (sorted.Count > page.Count + n.Offset)
        {
            checks.Add(new EvidenceCheck("limit", true, $"Показаны строки {n.Offset + 1}–{n.Offset + page.Count} из {sorted.Count}; остальные — следующей страницей, в Excel или в «Срезах»."));
        }

        log.Sql = string.Join("\n;\n", sqlLog);
        log.Parameters = ParametersJson(paramLog);
        log.Tables = string.Join(",", tables);
        log.Scope = scope.Restricted ? scope.Description : null;

        var sources = Sources(facts, dataThrough, await SyncTimesAsync(ct));
        var complete = checks.All(c => c.Ok);
        var totalsOut = Round(totals, n);
        return new AnalyticsResult(Guid.Empty, query, columns, outRows, keys, totalsOut, compareTotals is null ? null : Round(compareTotals, n), sorted.Count,
            sorted.Count > n.Offset + page.Count, complete, checks, sources, notes.Distinct().ToList(), scope.Restricted ? scope.Description : null,
            watch.ElapsedMilliseconds, n.From, n.To, n.CompareFrom, n.CompareTo);
    }

    /// <summary>Строки всех фактов запроса по измерениям (план — помесячный, из sales.RegionPlans / staff_balance).</summary>
    private async Task<Dictionary<string, Row>> FactsAsync(Normalized n, DateOnly from, DateOnly to, SemanticContext ctx, IReadOnlyList<ResolvedFilter> filters,
        SalesRowScope scope, List<string>? sqlLog, List<SqlParam>? paramLog, HashSet<string> tables, List<string> notes, CancellationToken ct)
    {
        var rows = new Dictionary<string, Row>();
        var byFact = n.All.Where(m => !m.Calculated).GroupBy(m => m.Fact);
        foreach (var group in byFact)
        {
            var metrics = group.Select(m => m.Code).ToList();
            if (group.Key == SemanticCatalog.Plan)
            {
                await PlanRowsAsync(rows, n, from, to, ctx, filters, scope, metrics, notes, tables, ct);
                continue;
            }

            var built = new SemanticSql(ctx, salesOptions).Build(group.Key, n.Dimensions, metrics, n.Grain, from, to, n.Segment, scope, filters);
            sqlLog?.Add(built.Sql);
            paramLog?.AddRange(built.Parameters);
            foreach (var t in built.Tables.Split(", "))
            {
                tables.Add(t);
            }

            await CostAsync(built, ct);
            var result = await sql.QueryAsync(built.Sql, built.Parameters, new SqlLimits(MaxGroups, options.QueryTimeoutSeconds), ct);
            if (result.Truncated)
            {
                throw new AnalyticsQueryException("too_heavy",
                    $"Срез даёт больше {MaxGroups:N0} строк — сузьте период, уберите измерение (например, торговую точку) или возьмите шаг крупнее.");
            }

            foreach (var r in result.Rows)
            {
                var keys = r.Take(n.Dimensions.Count).Select(NormalizeKey).ToArray();
                var row = Get(rows, keys);
                for (var i = 0; i < metrics.Count; i++)
                {
                    row.Values[metrics[i]] = ToDecimal(r[n.Dimensions.Count + i]);
                }
            }
        }

        return rows;
    }

    private async Task PlanRowsAsync(Dictionary<string, Row> rows, Normalized n, DateOnly from, DateOnly to, SemanticContext ctx, IReadOnlyList<ResolvedFilter> filters,
        SalesRowScope scope, IReadOnlyList<string> metrics, List<string> notes, HashSet<string> tables, CancellationToken ct)
    {
        var byGroup = n.Dimensions.Contains("product_group") || filters.Any(f => f.Dimension == "product_group");
        var agentLevel = n.Dimensions.Contains("agent") || filters.Any(f => f.Dimension == "agent");
        var facts = await plans.LoadAsync(ctx, from, to, n.PlanSource, n.PlanKind, byGroup, scope, ct, regionLevel: !agentLevel);
        tables.Add("sales.RegionPlans");
        tables.Add("sales.StaffPlans");
        notes.AddRange(facts.Notes);
        var regionDims = new[] { "region", "direction", "manager", "supervisor", "dealer" };
        var needsRegion = n.Dimensions.Any(regionDims.Contains) || filters.Any(f => regionDims.Contains(f.Dimension));
        var needsAgent = n.Dimensions.Contains("agent") || filters.Any(f => f.Dimension == "agent");
        if (needsRegion && facts.AgentMonths.Count > 0)
        {
            notes.Add($"План по регионам за {string.Join(", ", facts.AgentMonths.Select(MonthName))} не задан (есть только планы ТП Linko) — в этих месяцах план по регионам пуст.");
        }

        if (needsAgent && facts.RegionMonths.Count > 0)
        {
            notes.Add($"План по ТП за {string.Join(", ", facts.RegionMonths.Select(MonthName))} не используется: в этих месяцах план задан по регионам (Google Sheets).");
        }

        if (byGroup && facts.AgentMonths.Count > 0)
        {
            notes.Add("Планы ТП Linko — без разбивки по товарным группам: по группам план есть только у планов регионов.");
        }

        foreach (var p in facts.Rows)
        {
            if ((needsRegion && p.Region is null) || (needsAgent && p.Agent is null) || (byGroup && p.Agent is not null))
            {
                continue;
            }

            var region = p.Region is { } rid && ctx.Regions.TryGetValue(rid, out var info) ? info : null;
            var direction = region?.DirectionId is { } did && ctx.Directions.TryGetValue(did, out var dinfo) ? dinfo : null;
            if (!PlanPasses(p, region, direction, filters))
            {
                continue;
            }

            var keys = n.Dimensions.Select(d => d switch
            {
                "date" => (object?)Trunc(p.Month, n.Grain),
                "region" => p.Region,
                "direction" => region?.DirectionId,
                "manager" => direction?.ManagerName,
                "supervisor" => region?.Supervisor,
                "dealer" => region?.Dealer,
                "agent" => p.Agent,
                "product_group" => p.Group,
                "source" => "linko",
                _ => null,
            }).ToArray();
            var row = Get(rows, keys);
            if (metrics.Contains("plan_kg") && p.Kg is { } kg)
            {
                row.Values["plan_kg"] = (row.Values.GetValueOrDefault("plan_kg") ?? 0) + kg;
            }

            if (metrics.Contains("plan_revenue") && p.Revenue is { } rev)
            {
                row.Values["plan_revenue"] = (row.Values.GetValueOrDefault("plan_revenue") ?? 0) + rev;
            }

            if (metrics.Contains("plan_fact_kg") && p.PlanFactKg is { } pf)
            {
                row.Values["plan_fact_kg"] = (row.Values.GetValueOrDefault("plan_fact_kg") ?? 0) + pf;
            }
        }
    }

    private static bool PlanPasses(PlanFactRow p, Sales.Metrics.RegionInfo? region, Sales.Metrics.DirectionInfo? direction, IReadOnlyList<ResolvedFilter> filters)
    {
        foreach (var f in filters)
        {
            object? value = f.Dimension switch
            {
                "region" => p.Region,
                "direction" => region?.DirectionId,
                "manager" => direction?.ManagerName?.Trim().ToLowerInvariant(),
                "supervisor" => region?.Supervisor?.Trim().ToLowerInvariant(),
                "dealer" => region?.Dealer?.Trim().ToLowerInvariant(),
                "agent" => p.Agent,
                "product_group" => p.Group,
                "source" => "linko",
                _ => null,
            };
            var hit = value is null ? f.MatchNull : f.Values.Contains(value);
            if (hit == f.Exclude)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Итоги среза (без измерений): отдельным запросом — АКБ и заказы за период не равны сумме по строкам.</summary>
    private async Task<Dictionary<string, decimal?>> TotalsAsync(Normalized n, DateOnly from, DateOnly to, SemanticContext ctx, IReadOnlyList<ResolvedFilter> filters,
        SalesRowScope scope, CancellationToken ct)
    {
        var total = new Dictionary<string, decimal?>();
        foreach (var group in n.All.Where(m => !m.Calculated).GroupBy(m => m.Fact))
        {
            var metrics = group.Select(m => m.Code).ToList();
            if (group.Key == SemanticCatalog.Plan)
            {
                var rows = new Dictionary<string, Row>();
                await PlanRowsAsync(rows, n with { Dimensions = n.Dimensions.Where(d => d == "product_group").ToList() }, from, to, ctx, filters, scope, metrics, [], [], ct);
                foreach (var m in metrics)
                {
                    total[m] = rows.Values.Any(r => r.Values.ContainsKey(m)) ? rows.Values.Sum(r => r.Values.GetValueOrDefault(m) ?? 0) : null;
                }

                continue;
            }

            var built = new SemanticSql(ctx, salesOptions).Build(group.Key, [], metrics, n.Grain, from, to, n.Segment, scope, filters);
            var result = await sql.QueryAsync(built.Sql, built.Parameters, new SqlLimits(1, options.QueryTimeoutSeconds), ct);
            var row = result.Rows.FirstOrDefault();
            for (var i = 0; i < metrics.Count; i++)
            {
                total[metrics[i]] = row is null ? 0 : ToDecimal(row[i]) ?? 0;
            }
        }

        return total;
    }

    /// <summary>Оценка стоимости EXPLAIN: дороже предела Аналитика — отказ с советом сузить срез.</summary>
    private async Task CostAsync(FactSql built, CancellationToken ct)
    {
        var explain = await sql.QueryAsync("EXPLAIN (FORMAT JSON) " + built.Sql, built.Parameters, new SqlLimits(1, 10), ct);
        if (explain.Rows.FirstOrDefault()?[0]?.ToString() is not { } text)
        {
            return;
        }

        using var doc = JsonDocument.Parse(text);
        var cost = doc.RootElement[0].GetProperty("Plan").GetProperty("Total Cost").GetDouble();
        if (cost > options.MaxQueryCost * 4)
        {
            throw new AnalyticsQueryException("too_heavy",
                $"Срез слишком тяжёлый (оценка PostgreSQL {cost:N0}) — сузьте период, уберите измерение «торговая точка» или возьмите шаг крупнее.");
        }
    }

    /// <summary>Первая и последняя дата данных факта (продажи — по дате реализации проданных заказов, не позже сегодня).</summary>
    public async Task<(DateOnly? First, DateOnly? Last)> DataRangeAsync(string fact, CancellationToken ct)
    {
        var today = Today;
        switch (fact)
        {
            case SemanticCatalog.Sales:
            {
                var sold = salesOptions.SoldStatuses;
                var orders = db.LinkoOrders.AsNoTracking().Where(o => sold.Contains(o.Status));
                var last = salesOptions.DateField == SaleDateField.Accepted
                    ? await orders.Where(o => o.AcceptedDate <= today).MaxAsync(o => o.AcceptedDate, ct)
                    : await orders.Where(o => o.CreatedDate <= today).MaxAsync(o => (DateOnly?)o.CreatedDate, ct);
                var first = salesOptions.DateField == SaleDateField.Accepted
                    ? await orders.MinAsync(o => o.AcceptedDate, ct)
                    : await orders.MinAsync(o => (DateOnly?)o.CreatedDate, ct);
                return (first, last);
            }
            case SemanticCatalog.Payments:
            {
                var payments = db.LinkoPayments.AsNoTracking().Where(p => !p.IsDelete && p.Status == "accepted");
                return (await payments.MinAsync(p => p.CreatedDate, ct), await payments.Where(p => p.CreatedDate <= today).MaxAsync(p => p.CreatedDate, ct));
            }
            case SemanticCatalog.Plan:
            {
                var plansRange = await db.SalesRegionPlans.AsNoTracking().Select(p => p.Year * 100 + p.Month).ToListAsync(ct);
                var staffRange = await db.SalesStaffPlans.AsNoTracking().Select(p => p.Year * 100 + p.Month).Distinct().ToListAsync(ct);
                var allMonths = plansRange.Concat(staffRange).ToList();
                return allMonths.Count == 0 ? (null, null)
                    : (new DateOnly(allMonths.Min() / 100, allMonths.Min() % 100, 1), new DateOnly(allMonths.Max() / 100, allMonths.Max() % 100, 1).AddMonths(1).AddDays(-1));
            }
            default:
                return (null, today);
        }
    }

    /// <summary>Когда Linko загружал данные в последний раз (по шагам синхронизации) и была ли ошибка.</summary>
    public async Task<Dictionary<string, (DateTimeOffset? At, string? Error)>> SyncTimesAsync(CancellationToken ct) =>
        (await db.LinkoSyncStates.AsNoTracking().Select(s => new { s.Entity, s.LastSuccessAt, s.LastError }).ToListAsync(ct))
        .ToDictionary(s => s.Entity, s => (s.LastSuccessAt, s.LastError));

    /// <summary>Продажи в день за 30 дней по товарным измерениям среза — для дней запаса и потребности.</summary>
    private async Task<Dictionary<string, decimal>?> VelocityAsync(Normalized n, SemanticContext ctx, DateOnly through, List<string> notes, CancellationToken ct)
    {
        if (n.Dimensions.Contains("stock"))
        {
            notes.Add("Дни запаса по складам не считаются: продажи в OneBase не привязаны к складу — уберите измерение «Склад».");
            return null;
        }

        var built = new SemanticSql(ctx, salesOptions).Build(SemanticCatalog.Sales, n.Dimensions, ["units"], "day", through.AddDays(-29), through, "all",
            SalesRowScope.Organization, []);
        var result = await sql.QueryAsync(built.Sql, built.Parameters, new SqlLimits(MaxGroups, options.QueryTimeoutSeconds), ct);
        notes.Add($"Скорость продаж — средние единицы в день за {through.AddDays(-29):dd.MM.yyyy} — {through:dd.MM.yyyy} (вторичка и «Завод»).");
        return result.Rows.ToDictionary(r => Key(r.Take(n.Dimensions.Count).Select(NormalizeKey).ToArray()), r => (ToDecimal(r[n.Dimensions.Count]) ?? 0) / 30m);
    }

    // ---------- калькулятор ----------

    private void Calculate(IEnumerable<Row> rows, Normalized n, Dictionary<string, decimal?> totals, DateOnly? through,
        Dictionary<string, decimal>? velocity, List<string> notes)
    {
        var list = rows.ToList();
        var dateIndex = n.Dimensions.ToList().IndexOf("date");
        Dictionary<object, decimal>? revenueByDate = null;
        if (n.Requested.Any(m => m.Code == "revenue_share") && dateIndex >= 0)
        {
            revenueByDate = list.GroupBy(r => r.Keys[dateIndex]!).ToDictionary(g => g.Key, g => g.Sum(r => r.Values.GetValueOrDefault("revenue") ?? 0));
        }

        foreach (var r in list)
        {
            var month = MonthOf(r, n, dateIndex);
            decimal? denominator = revenueByDate is not null ? revenueByDate[r.Keys[dateIndex]!] : totals.GetValueOrDefault("revenue");
            CalculateValues(r.Values, n, denominator, through, month, velocity?.GetValueOrDefault(Key(r.Keys)));
        }

        if (n.Requested.Any(m => m.Code.StartsWith("forecast")) && dateIndex < 0 && (n.From.Year != n.To.Year || n.From.Month != n.To.Month))
        {
            notes.Add("Прогноз месяца считается для одного месяца: возьмите период внутри месяца или шаг month.");
        }
    }

    private void CalculateTotals(Dictionary<string, decimal?> totals, Normalized n, DateOnly? through, Dictionary<string, decimal>? velocity)
    {
        DateOnly? month = n.From.Year == n.To.Year && n.From.Month == n.To.Month && n.From.Day == 1 ? new DateOnly(n.From.Year, n.From.Month, 1) : null;
        CalculateValues(totals, n, totals.GetValueOrDefault("revenue"), through, month, velocity is null ? null : velocity.Values.Sum());
    }

    private void CalculateValues(Dictionary<string, decimal?> v, Normalized n, decimal? revenueDenominator, DateOnly? through, DateOnly? month, decimal? dailyUnits)
    {
        decimal? Div(decimal? a, decimal? b) => a is { } x && b is { } y && y != 0 ? x / y : null;
        foreach (var m in n.All.Where(m => m.Calculated))
        {
            v[m.Code] = m.Code switch
            {
                "avg_check" => Div(v.GetValueOrDefault("revenue"), v.GetValueOrDefault("orders")),
                "avg_price_kg" => Div(v.GetValueOrDefault("revenue"), v.GetValueOrDefault("kg")),
                "revenue_share" => Div(v.GetValueOrDefault("revenue"), revenueDenominator) * 100,
                "plan_execution" => Div(v.GetValueOrDefault("plan_fact_kg") ?? v.GetValueOrDefault("kg"), v.GetValueOrDefault("plan_kg")) * 100,
                "plan_deviation_kg" => v.GetValueOrDefault("plan_kg") is null ? null : (v.GetValueOrDefault("plan_fact_kg") ?? v.GetValueOrDefault("kg") ?? 0) - v["plan_kg"],
                "forecast_kg" => Forecast(v.GetValueOrDefault("kg"), month, through, n.From),
                "forecast_execution" => Div(Forecast(v.GetValueOrDefault("plan_fact_kg") ?? v.GetValueOrDefault("kg"), month, through, n.From), v.GetValueOrDefault("plan_kg")) * 100,
                "cover_days" => dailyUnits is > 0 ? v.GetValueOrDefault("stock_units") / dailyUnits : null,
                "need_units" => dailyUnits is { } du && v.GetValueOrDefault("stock_units") is { } st ? Math.Max(0, du * n.CoverDays - st) : null,
                _ => v.GetValueOrDefault(m.Code),
            };
        }
    }

    /// <summary>
    /// Прогноз месяца по темпу: закрытый месяц — факт; идущий — факт ÷ прошедшие дни (по последний день данных) × дни месяца; период,
    /// начатый не с 1-го числа, — без прогноза (неполный месяц не продлить честно).
    /// </summary>
    private static decimal? Forecast(decimal? kg, DateOnly? month, DateOnly? through, DateOnly from)
    {
        if (kg is null || month is not { } m || through is not { } t || from > m)
        {
            return null;
        }

        var end = m.AddMonths(1).AddDays(-1);
        if (t >= end)
        {
            return kg;
        }

        return t < m ? null : kg / t.Day * end.Day;
    }

    private static DateOnly? MonthOf(Row r, Normalized n, int dateIndex)
    {
        if (dateIndex >= 0)
        {
            return n.Grain == "month" && r.Keys[dateIndex] is DateOnly d ? d : null;
        }

        return n.From.Year == n.To.Year && n.From.Month == n.To.Month ? new DateOnly(n.From.Year, n.From.Month, 1) : null;
    }

    // ---------- нулевые шаги, сравнение, сортировка ----------

    private static int FillZeros(Dictionary<string, Row> rows, Normalized n)
    {
        var dateIndex = n.Dimensions.ToList().IndexOf("date");
        var buckets = new List<DateOnly>();
        for (var d = Trunc(n.From, n.Grain); d <= n.To; d = Next(d, n.Grain))
        {
            buckets.Add(d);
        }

        var combos = rows.Values.Select(r => r.Keys.Where((_, i) => i != dateIndex).ToArray()).DistinctBy(Key).ToList();
        if ((long)combos.Count * buckets.Count > MaxGroups)
        {
            return 0;
        }

        var added = 0;
        var metrics = n.All.Where(m => !m.Calculated && m.Fact != SemanticCatalog.Plan).Select(m => m.Code).ToList();
        foreach (var combo in combos)
        {
            foreach (var b in buckets)
            {
                var keys = combo.ToList();
                keys.Insert(dateIndex, b);
                var key = Key(keys.ToArray());
                if (rows.ContainsKey(key))
                {
                    continue;
                }

                var row = new Row(keys.ToArray()) { Filled = true };
                foreach (var m in metrics)
                {
                    row.Values[m] = 0;
                }

                rows[key] = row;
                added++;
            }
        }

        return added;
    }

    private static string AlignedKey(object?[] keys, int dateIndex, string grain, DateOnly start)
    {
        if (dateIndex < 0)
        {
            return Key(keys);
        }

        var copy = keys.ToArray();
        copy[dateIndex] = copy[dateIndex] is DateOnly d ? Steps(Trunc(start, grain), d, grain) : -1;
        return Key(copy);
    }

    private static int Steps(DateOnly start, DateOnly d, string grain) => grain switch
    {
        "day" => d.DayNumber - start.DayNumber,
        "week" => (d.DayNumber - start.DayNumber) / 7,
        "month" => (d.Year - start.Year) * 12 + d.Month - start.Month,
        "quarter" => ((d.Year - start.Year) * 12 + d.Month - start.Month) / 3,
        _ => d.Year - start.Year,
    };

    public static DateOnly Trunc(DateOnly d, string grain) => grain switch
    {
        "week" => d.AddDays(-(((int)d.DayOfWeek + 6) % 7)),
        "month" => new DateOnly(d.Year, d.Month, 1),
        "quarter" => new DateOnly(d.Year, (d.Month - 1) / 3 * 3 + 1, 1),
        "year" => new DateOnly(d.Year, 1, 1),
        _ => d,
    };

    private static DateOnly Next(DateOnly d, string grain) => grain switch
    {
        "week" => d.AddDays(7),
        "month" => d.AddMonths(1),
        "quarter" => d.AddMonths(3),
        "year" => d.AddYears(1),
        _ => d.AddDays(1),
    };

    private static IEnumerable<Row> Sort(IEnumerable<Row> rows, Normalized n, IReadOnlyList<AnalyticsSort>? sort, List<Dictionary<object, string>> labels)
    {
        var specs = (sort ?? []).Where(s => !string.IsNullOrWhiteSpace(s.By)).ToList();
        if (specs.Count == 0)
        {
            if (n.Dimensions.Contains("date"))
            {
                specs.Add(new AnalyticsSort("date", "asc"));
            }

            specs.Add(new AnalyticsSort(n.Requested[0].Code, "desc"));
        }

        IOrderedEnumerable<Row>? ordered = null;
        foreach (var s in specs)
        {
            var desc = string.Equals(s.Dir, "desc", StringComparison.OrdinalIgnoreCase);
            var dimIndex = n.Dimensions.ToList().IndexOf(SemanticCatalog.Dimension(s.By)?.Code ?? "");
            var by = s.By.Trim().ToLowerInvariant();
            Func<Row, IComparable?> key = dimIndex >= 0
                ? r => r.Keys[dimIndex] is DateOnly d ? d : r.Keys[dimIndex] is { } k && labels[dimIndex].TryGetValue(k, out var l) ? l : r.Keys[dimIndex]?.ToString()
                : r => SortValue(r, by) ?? (desc ? decimal.MinValue : decimal.MaxValue);
            ordered = ordered is null
                ? desc ? rows.OrderByDescending(key) : rows.OrderBy(key)
                : desc ? ordered.ThenByDescending(key) : ordered.ThenBy(key);
        }

        return ordered ?? rows;
    }

    /// <summary>Значение для сортировки: показатель или колонка сравнения ({metric}__prev, __delta, __pct); пустые — в конец.</summary>
    private static decimal? SortValue(Row r, string by)
    {
        var parts = by.Split("__");
        var value = r.Values.GetValueOrDefault(parts[0]);
        var prev = r.Prev.GetValueOrDefault(parts[0]);
        return parts.Length < 2 ? value : parts[1] switch
        {
            "prev" => prev,
            "delta" => value is { } a && prev is { } b ? a - b : null,
            "pct" => value is { } c && prev is { } p && p != 0 ? (c - p) / Math.Abs(p) * 100 : null,
            _ => value,
        };
    }

    // ---------- проверка доказательств ----------

    private async Task<List<EvidenceCheck>> EvidenceAsync(Normalized n, IReadOnlySet<string> facts, List<Row> rows, Dictionary<string, decimal?> totals,
        IReadOnlyList<ResolvedFilter> filters, SalesRowScope scope, Dictionary<string, (DateOnly? First, DateOnly? Last)> ranges, List<string> notes, CancellationToken ct)
    {
        var checks = new List<EvidenceCheck>();
        var today = Today;
        var syncs = await SyncTimesAsync(ct);
        foreach (var fact in facts.Where(f => f is SemanticCatalog.Sales or SemanticCatalog.Payments))
        {
            var (first, last) = ranges[fact];
            var title = FactTitle(fact);
            if (last is null)
            {
                checks.Add(new EvidenceCheck($"{fact}.period", false, $"{title}: в OneBase нет ни одной записи — проверьте подключение Linko."));
                continue;
            }

            var missing = n.To.DayNumber - last.Value.DayNumber;
            if (missing > 0 && !(n.To == today && missing == 1))
            {
                checks.Add(new EvidenceCheck($"{fact}.period", false,
                    $"{title}: данные есть по {last:dd.MM.yyyy}; за {last.Value.AddDays(1):dd.MM.yyyy} — {n.To:dd.MM.yyyy} их ещё нет — это не нулевые продажи."));
            }
            else
            {
                checks.Add(new EvidenceCheck($"{fact}.period", true, $"{title}: данные покрывают период {n.From:dd.MM.yyyy} — {n.To:dd.MM.yyyy} (последний день данных {last:dd.MM.yyyy})."));
            }

            if (first is { } f && n.From < f)
            {
                checks.Add(new EvidenceCheck($"{fact}.start", false, $"{title}: в OneBase данные начинаются с {f:dd.MM.yyyy} — раньше их нет (загрузка истории Linko)."));
            }
        }

        if (facts.Contains(SemanticCatalog.Sales))
        {
            var grainText = n.Dimensions.Contains("date")
                ? $"Шаг — {GrainName(n.Grain)}: каждая строка — фактическая сумма продаж за {GrainName(n.Grain)}, без средних и без распределения месячных итогов."
                : "Итоги за период — суммы фактических строк продаж.";
            checks.Add(new EvidenceCheck("grain", true, grainText));
            checks.Add(new EvidenceCheck("keys", true,
                $"Строки уникальны по id Linko (повторная загрузка не дублирует, исправленный документ перезаписан); в выручку входят статусы " +
                $"{string.Join(", ", salesOptions.SoldStatuses)}, отменённые заказы — нет; возвраты — статус {string.Join(", ", salesOptions.ReturnStatuses)}."));

            var orderSync = syncs.GetValueOrDefault("orders");
            if (orderSync.Error is { Length: > 0 } error)
            {
                checks.Add(new EvidenceCheck("sync", false, $"Последняя загрузка заказов Linko завершилась ошибкой: {error}"));
            }

            var other = totals.GetValueOrDefault("other_currency_revenue");
            if (other is > 0)
            {
                notes.Add($"Заказы не в сумах ({other:N0} в валюте) в выручку не входят: курса в данных нет.");
            }
        }

        var regionDims = new[] { "region", "direction", "manager", "supervisor", "dealer" };
        var regionIndex = n.Dimensions.Select((d, i) => (d, i)).FirstOrDefault(x => x.d == "region").i;
        if (n.Dimensions.Contains("region") && facts.Contains(SemanticCatalog.Sales))
        {
            var metric = n.All.Any(m => m.Code == "revenue") ? "revenue" : n.All.FirstOrDefault(m => !m.Calculated && m.Fact == SemanticCatalog.Sales)?.Code;
            if (metric is not null)
            {
                var unmapped = rows.Where(r => r.Keys[regionIndex] is null).Sum(r => r.Values.GetValueOrDefault(metric) ?? 0);
                var total = rows.Sum(r => r.Values.GetValueOrDefault(metric) ?? 0);
                checks.Add(unmapped != 0
                    ? new EvidenceCheck("region_mapping", false,
                        $"Не сопоставлено с регионом: {unmapped:N0} ({(total == 0 ? 0 : unmapped / total * 100):N1}% показателя) — филиал документа не найден в регионах OneBase («Без региона»).")
                    : new EvidenceCheck("region_mapping", true, "Все строки сопоставлены с регионами вторичке (по филиалу заказа, как «Продажи по регионам»)."));
            }
        }

        if (n.Dimensions.Contains("product_group") && facts.Contains(SemanticCatalog.Sales))
        {
            var index = n.Dimensions.ToList().IndexOf("product_group");
            var metric = n.All.Any(m => m.Code == "revenue") ? "revenue" : n.All.FirstOrDefault(m => !m.Calculated && m.Fact == SemanticCatalog.Sales)?.Code;
            if (metric is not null)
            {
                var noGroup = rows.Where(r => r.Keys[index] is null).Sum(r => r.Values.GetValueOrDefault(metric) ?? 0);
                var outside = rows.Where(r => r.Keys[index] is long g && g > 0).GroupBy(r => (long)r.Keys[index]!).Select(g => g.Key).ToList();
                checks.Add(noGroup != 0
                    ? new EvidenceCheck("group_mapping", false, $"У части строк нет типа товара в Linko: {noGroup:N0} — «Без группы».")
                    : new EvidenceCheck("group_mapping", true, "Все товары сопоставлены с товарными группами (тип Linko → категория отчёта)."));
                if (outside.Count > 0)
                {
                    notes.Add("Группы вне категорий отчёта «Продаж» (например, бонусы и оборудование) показаны отдельно — в итогах «Продаж по SKU» их нет.");
                }
            }
        }

        if (facts.Contains(SemanticCatalog.Plan))
        {
            var planRanges = ranges[SemanticCatalog.Plan];
            var monthsWithout = new List<string>();
            for (var m = new DateOnly(n.From.Year, n.From.Month, 1); m <= n.To; m = m.AddMonths(1))
            {
                if (planRanges.Last is null || m > planRanges.Last || m < planRanges.First)
                {
                    monthsWithout.Add(MonthName(m));
                }
            }

            checks.Add(monthsWithout.Count > 0
                ? new EvidenceCheck("plan", false, $"Плана нет за {string.Join(", ", monthsWithout)} — выполнение за эти месяцы не считается.")
                : new EvidenceCheck("plan", true, "План есть за все месяцы периода (помесячный, по дням не делится)."));
            if (n.From.Day != 1 || n.To != new DateOnly(n.To.Year, n.To.Month, 1).AddMonths(1).AddDays(-1))
            {
                notes.Add("План — за целые месяцы, а период начинается или заканчивается внутри месяца: факт за часть месяца сравнивается с планом всего месяца (см. прогноз).");
            }
        }

        // Итоги: сумма строк = итог для аддитивных показателей (АКБ и заказы за период не суммируются по строкам).
        var additive = n.All.Where(m => !m.Calculated && m.Code is not ("outlets" or "orders") && m.Fact is SemanticCatalog.Sales or SemanticCatalog.Payments or SemanticCatalog.Stock).ToList();
        var mismatch = additive.Where(m => totals.GetValueOrDefault(m.Code) is { } t && Math.Abs(rows.Where(r => !r.Filled).Sum(r => r.Values.GetValueOrDefault(m.Code) ?? 0) - t) > 0.5m).ToList();
        if (additive.Count > 0)
        {
            checks.Add(mismatch.Count == 0
                ? new EvidenceCheck("totals", true, "Сумма строк совпадает с итогом периода.")
                : new EvidenceCheck("totals", false, $"Сумма строк не совпадает с итогом: {string.Join(", ", mismatch.Select(m => m.Title))}."));
        }

        if (scope.Restricted)
        {
            checks.Add(new EvidenceCheck("scope", true, $"Только ваша область: {scope.Description}."));
        }

        foreach (var f in filters)
        {
            notes.Add($"Фильтр: {f.Description}.");
        }

        if (n.Dimensions.Any(regionDims.Contains) && facts.Contains(SemanticCatalog.Sales))
        {
            notes.Add("Регион — по филиалу заказа (как «Продажи по регионам» и «Продажи по SKU»); «Продажи за период» относят продажи агента к его основному региону — суммы регионов там могут отличаться.");
        }

        if (n.Segment != "secondary" && facts.Contains(SemanticCatalog.Sales))
        {
            notes.Add(n.Segment == "factory" ? "Только «Завод» — экспорт и опт, не вторичка." : "Вторичка и «Завод» (экспорт и опт) вместе.");
        }

        return checks;
    }

    private static string GrainName(string grain) => grain switch
    {
        "week" => "неделю",
        "month" => "месяц",
        "quarter" => "квартал",
        "year" => "год",
        _ => "день",
    };

    private static readonly string[] Months = ["январь", "февраль", "март", "апрель", "май", "июнь", "июль", "август", "сентябрь", "октябрь", "ноябрь", "декабрь"];

    private static string MonthName(DateOnly m) => $"{Months[m.Month - 1]} {m.Year}";

    private static IReadOnlyList<AnalyticsSource> Sources(IReadOnlySet<string> facts, Dictionary<string, (DateOnly? First, DateOnly? Last)> ranges,
        Dictionary<string, (DateTimeOffset? At, string? Error)> syncs)
    {
        var list = new List<AnalyticsSource>();
        if (facts.Contains(SemanticCatalog.Sales))
        {
            list.Add(new AnalyticsSource("linko", "Linko: продажи (вторичка)", "linko.Orders, OrderLines, OrderReturns, OrderReturnLines, Products, ProductTypes; sales.Regions",
                syncs.GetValueOrDefault("orders").At, ranges[SemanticCatalog.Sales].Last,
                "Заказы «доставлен» и «выдан» по дате приёмки, сумма строки в сумах; возвраты «доставлен» по дате создания; без «К К Мерч»; «Завод» — экспорт и опт; точки-каналы — в своём регионе в закрытых месяцах"));
        }

        if (facts.Contains(SemanticCatalog.Payments))
        {
            list.Add(new AnalyticsSource("linko", "Linko: оплаты точек", "linko.Payments, Markets", syncs.GetValueOrDefault("payments").At, ranges[SemanticCatalog.Payments].Last,
                "Принятые оплаты без удалённых, по дате создания; регион — по филиалу точки"));
        }

        if (facts.Contains(SemanticCatalog.Plan))
        {
            list.Add(new AnalyticsSource("google_sheets", "Планы регионов", "sales.RegionPlans", null, ranges[SemanticCatalog.Plan].Last,
                "План РОП (или выбранного вида) регионов по категориям, кг в месяц — Google Sheets / AI Data Manager"));
            list.Add(new AnalyticsSource("linko", "Планы ТП Linko", "sales.StaffPlans", syncs.GetValueOrDefault("staff_balance").At, ranges[SemanticCatalog.Plan].Last,
                "staff_balance: «Тотал» или сумма показателей ТП, без командных планов СВР; используется в месяцы без планов регионов"));
        }

        if (facts.Contains(SemanticCatalog.Stock))
        {
            list.Add(new AnalyticsSource("linko", "Linko: остатки", "linko.ProductBalances, Stocks, Products", syncs.GetValueOrDefault("product_balances").At, null,
                "Текущий снимок остатков в единицах товара"));
        }

        return list;
    }

    // ---------- вывод ----------

    private static IReadOnlyList<AnalyticsColumn> Columns(Normalized n, bool compare)
    {
        var list = n.Dimensions.Select(d => new AnalyticsColumn(d, SemanticCatalog.Dimension(d)!.Title, "dimension", null, d == "date" ? "date" : "text")).ToList();
        foreach (var m in n.Requested)
        {
            list.Add(new AnalyticsColumn(m.Code, m.Title, "metric", m.Unit, Format(m.Unit)));
            if (compare && m.Code != "revenue_share")
            {
                list.Add(new AnalyticsColumn($"{m.Code}__prev", $"{m.Title} (сравнение)", "compare", m.Unit, Format(m.Unit)));
                list.Add(new AnalyticsColumn($"{m.Code}__delta", $"{m.Title}: разница", "delta", m.Unit, Format(m.Unit)));
                list.Add(new AnalyticsColumn($"{m.Code}__pct", $"{m.Title}: разница, %", "delta_pct", "%", "percent"));
            }
        }

        return list;
    }

    private static string Format(string unit) => unit switch
    {
        "сум" or "сум/кг" => "money",
        "кг" => "kg",
        "%" => "percent",
        "шт" or "точек" => "integer",
        _ => "number",
    };

    private static object?[] RowValues(Row r, Normalized n, List<Dictionary<object, string>> labels, bool compare)
    {
        var list = new List<object?>();
        for (var i = 0; i < n.Dimensions.Count; i++)
        {
            var k = r.Keys[i];
            list.Add(k switch
            {
                DateOnly d => d.ToString("yyyy-MM-dd"),
                null => SemanticResolver.Empty(n.Dimensions[i]),
                _ => labels[i].TryGetValue(k, out var l) ? l : k.ToString(),
            });
        }

        foreach (var m in n.Requested)
        {
            var value = RoundValue(r.Values.GetValueOrDefault(m.Code), m.Unit);
            list.Add(value);
            if (compare && m.Code != "revenue_share")
            {
                var prev = RoundValue(r.Prev.GetValueOrDefault(m.Code), m.Unit);
                list.Add(prev);
                list.Add(value is { } a && prev is { } b ? a - b : null);
                list.Add(value is { } c && prev is { } p && p != 0 ? Math.Round((c - p) / Math.Abs(p) * 100, 1) : null);
            }
        }

        return list.ToArray();
    }

    private static decimal? RoundValue(decimal? v, string unit) => v is not { } x ? null : unit switch
    {
        "%" or "дн." => Math.Round(x, 1),
        "кг" or "ед." => Math.Round(x, 2),
        _ => Math.Round(x, 2),
    };

    private static Dictionary<string, decimal?> Round(Dictionary<string, decimal?> totals, Normalized n) =>
        n.Requested.ToDictionary(m => m.Code, m => RoundValue(totals.GetValueOrDefault(m.Code), m.Unit));

    // ---------- служебное ----------

    private static Row Get(Dictionary<string, Row> rows, object?[] keys)
    {
        var key = Key(keys);
        if (!rows.TryGetValue(key, out var row))
        {
            rows[key] = row = new Row(keys);
        }

        return row;
    }

    private static string Key(object?[] keys) => string.Join("\u001f", keys.Select(k => k switch
    {
        null => SemanticResolver.NoValue,
        DateOnly d => d.ToString("yyyy-MM-dd"),
        string s => s.Trim().ToLowerInvariant(),
        _ => k.ToString(),
    }));

    private static object? NormalizeKey(object? value) => value switch
    {
        DateTime dt => DateOnly.FromDateTime(dt),
        int i => (long)i,
        string s => s,
        _ => value,
    };

    private static decimal? ToDecimal(object? value) => value switch
    {
        null => null,
        decimal d => d,
        long l => l,
        int i => i,
        double db => (decimal)db,
        _ => Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture),
    };

    private static string ParametersJson(IReadOnlyList<SqlParam> parameters) =>
        JsonSerializer.Serialize(parameters.GroupBy(p => p.Name).ToDictionary(g => g.Key, g => g.First().Value is Array a && a.Length > 20
            ? (object)a.Cast<object>().Take(20).Append($"… всего {a.Length}").ToArray()
            : QueryEngine.Normalize(g.First().Value)), Json);

    private async Task FailAsync(AnalytikQuery log, Stopwatch watch, string status, string message, CancellationToken ct)
    {
        log.Status = status;
        log.Error = message.Length <= 2000 ? message : message[..2000];
        log.DurationMs = watch.ElapsedMilliseconds;
        await SaveAsync(log, ct);
    }

    private async Task SaveAsync(AnalytikQuery log, CancellationToken ct)
    {
        try
        {
            log.Origin = log.Origin.Length <= 16 ? log.Origin : log.Origin[..16];
            adb.Queries.Add(log);
            await adb.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Неудачная запись не остаётся в контексте: иначе следующие сохранения той же области (журнал расчётов, исследование) упадут вместе с ней.
            adb.Queries.Entry(log).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
            logger.LogWarning(ex, "Аналитик: срез не записан в журнал");
        }
    }
}
