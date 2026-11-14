using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Analytik;
using OneBase.Application.Analytik.Compute;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Research;
using OneBase.Application.Analytik.Semantic;

namespace OneBase.AI.Tests;

/// <summary>
/// Phase 2 на PostgreSQL (ONEBASE_TEST_POSTGRES, иначе пропускается): движок срезов с визитами, совместимость, Statistics Engine на данных и
/// исследования-шаблоны. Числа сверяются с эталоном мира (IntelligenceWorld + ResearchWorld), а не «вернулся ответ».
/// </summary>
public class StatisticsResearchDbTests(ResearchDb fx) : IClassFixture<ResearchDb>
{
    private static readonly DateOnly Sep1 = new(2026, 9, 1), Sep30 = new(2026, 9, 30), Oct1 = new(2026, 10, 1), Oct31 = new(2026, 10, 31);

    private async Task<AnalyticsResult> Query(AnalyticsQuery q, Guid? user = null)
    {
        using var scope = fx.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SemanticEngine>().RunAsync(q, new QueryCaller(user ?? fx.AdminId, "api"), CancellationToken.None);
    }

    private async Task<ComputeOutcome> Compute(ComputeRequest r, Guid? user = null)
    {
        using var scope = fx.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ComputeService>().RunAsync(r, new ComputeContext(user ?? fx.AdminId, "api"), CancellationToken.None);
    }

    private async Task<ResearchResult> Research(ResearchRequest r, Guid? user = null, Func<string, Task>? progress = null)
    {
        using var scope = fx.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ResearchService>().RunAsync(r, new ResearchCaller(user ?? fx.AdminId, "api"), CancellationToken.None, progress);
    }

    private static decimal Cell(AnalyticsResult r, object?[] row, string code) => Convert.ToDecimal(row[r.Columns.ToList().FindIndex(c => c.Code == code)], CultureInfo.InvariantCulture);

    private static Dictionary<string, object?[]> ByFirst(AnalyticsResult r) => r.Rows.ToDictionary(x => x[0]?.ToString() ?? "");

    private static AnalyticsFilter Regions(IntelligenceWorld w) => new("region", "in", w.Regions);

    // ---------- 21–35: аналитика на данных ----------

    [PostgresFact]
    public async Task T21_revenue_by_region_matches_world()
    {
        var w = fx.World;
        var r = await Query(new AnalyticsQuery { Metrics = ["revenue", "kg"], Dimensions = ["region"], DateFrom = Sep1, DateTo = Sep30, Filters = [Regions(w)] });
        var rows = ByFirst(r);
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(w.Revenue(i, Sep1, Sep30), Cell(r, rows[w.Regions[i]], "revenue"), 0);
            Assert.Equal(w.Kg(i, Sep1, Sep30), Cell(r, rows[w.Regions[i]], "kg"), 0);
        }
    }

    [PostgresFact]
    public async Task T22_revenue_by_product_group_matches_world()
    {
        var w = fx.World;
        var r = await Query(new AnalyticsQuery { Metrics = ["revenue"], Dimensions = ["product_group"], DateFrom = Oct1, DateTo = Oct31, Filters = [Regions(w)] });
        var rows = ByFirst(r);
        var a = w.Between(Oct1, Oct31).Where(l => l.Product == 0).Sum(l => l.Revenue);
        var b = w.Between(Oct1, Oct31).Where(l => l.Product == 1).Sum(l => l.Revenue);
        Assert.Equal(a, Cell(r, rows[IntelligenceWorld.GroupA], "revenue"), 0);
        Assert.Equal(b, Cell(r, rows[IntelligenceWorld.GroupB], "revenue"), 0);
    }

    [PostgresFact]
    public async Task T23_T28_daily_revenue_across_month_boundary_matches_world()
    {
        var w = fx.World;
        DateOnly from = new(2026, 9, 25), to = new(2026, 10, 5);
        var r = await Query(new AnalyticsQuery { Metrics = ["revenue"], Dimensions = ["date"], Grain = "day", DateFrom = from, DateTo = to, Filters = [Regions(w)] });
        Assert.Equal(11, r.Rows.Count);
        foreach (var row in r.Rows)
        {
            var day = DateOnly.Parse(row[0]!.ToString()!, CultureInfo.InvariantCulture);
            Assert.Equal(w.Between(day, day).Sum(l => l.Revenue), Cell(r, row, "revenue"), 0);
        }

        Assert.True(r.Complete);
    }

    [PostgresFact]
    public async Task T24_visits_and_sales_by_agent_follow_visit_calendar_rules()
    {
        var w = fx.World;
        var r = await Query(new AnalyticsQuery
        {
            Metrics = ["visits_planned", "visits_done", "visits_done_in_plan", "visits_off_plan", "visits_missed", "visits_with_order", "visits_without_order", "visit_strike",
                "route_execution", "revenue"],
            Dimensions = ["region"], DateFrom = Oct1, DateTo = Oct31, Filters = [Regions(w)],
        });
        var rows = ByFirst(r);
        for (var i = 0; i < 3; i++)
        {
            var row = rows[w.Regions[i]];
            Assert.Equal(ResearchWorld.Planned(i, Oct1, Oct31), (int)Cell(r, row, "visits_planned"));
            Assert.Equal(ResearchWorld.Done(i, Oct1, Oct31), (int)Cell(r, row, "visits_done"));
            Assert.Equal(ResearchWorld.DoneInPlan(i, Oct1, Oct31), (int)Cell(r, row, "visits_done_in_plan"));
            Assert.Equal(ResearchWorld.Missed(i, Oct1, Oct31), (int)Cell(r, row, "visits_missed"));
            var withOrder = ResearchWorld.WithOrder(w, i, Oct1, Oct31);
            Assert.Equal(withOrder, (int)Cell(r, row, "visits_with_order"));
            Assert.Equal(ResearchWorld.Done(i, Oct1, Oct31) - withOrder, (int)Cell(r, row, "visits_without_order"));
            Assert.Equal(Math.Round((decimal)withOrder / ResearchWorld.Done(i, Oct1, Oct31) * 100, 1), Cell(r, row, "visit_strike"));
            Assert.Equal(Math.Round((decimal)ResearchWorld.DoneInPlan(i, Oct1, Oct31) / ResearchWorld.Planned(i, Oct1, Oct31) * 100, 1), Cell(r, row, "route_execution"));
            Assert.Equal(w.Revenue(i, Oct1, Oct31), Cell(r, row, "revenue"), 0);
        }

        // В регионе 0 точка 5 в октябре не покупает: её визиты — без заказа.
        Assert.True(Cell(r, rows[w.Regions[0]], "visit_strike") < Cell(r, rows[w.Regions[1]], "visit_strike"));
        Assert.Contains(r.Sources, s => s.Title.Contains("визиты", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(r.Notes, n => n.Contains("Регион визита — по филиалу точки", StringComparison.Ordinal));
    }

    [PostgresFact]
    public async Task T25_promo_research_checks_effect_with_control_group_and_discounts()
    {
        var r = await Research(new ResearchRequest { Kind = ResearchKinds.PromoEffect, From = new DateOnly(2026, 9, 1), To = Oct31 });
        // «Б» и «Б2» — те же товары и даты: одна проверка; «Б3» по списку товаров другая (есть непроданный товар) — своя проверка.
        var checks = r.Plan.Operations.Where(o => o.Id.StartsWith("c_did_", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, checks.Count);
        var did = checks.Single(o => o.Id == $"c_did_{ResearchWorld.PromoId}");
        var b3 = checks.Single(o => o.Id == $"c_did_{ResearchWorld.PromoId + 2}");
        Assert.Contains("ИИ акция Б / ИИ акция Б2", did.Title);
        Assert.All(checks, o => Assert.Equal(ResearchStatuses.Success, o.Status));
        Assert.Contains(r.Plan.Operations, o => o.Id == $"q_disc_{ResearchWorld.PromoId}" && ResearchStatuses.Usable(o.Status));
        // По продажам «Б3» — то же самое (окно, продажи и скидки совпадают): один вывод под общим названием, а не два одинаковых.
        var effect = Assert.Single(r.Findings, f => f.Section == "causes" && f.Evidence!.Any(e => e.StartsWith("c_did_", StringComparison.Ordinal)));
        Assert.Contains(did.Id, effect.Evidence!);
        Assert.Contains(b3.Id, effect.Evidence!);
        Assert.Contains("ИИ акция Б3", effect.Text, StringComparison.Ordinal);
        // Даты проверки в тексте: одноимённые акции разных месяцев не сливаются в одну с разными цифрами.
        Assert.Matches(@"» \(01\.10 — \d\d\.\d\d\.2026\): эффект", effect.Text);
        Assert.Contains(r.Limitations, l => l.Contains("не связана", StringComparison.Ordinal));
    }

    [PostgresFact]
    public async Task T26_T32_plan_vs_fact_without_plans_is_reported_missing_not_zero()
    {
        var w = fx.World;
        var r = await Query(new AnalyticsQuery
        {
            Metrics = ["kg", "plan_kg", "plan_execution"], Dimensions = ["region"], Grain = "month", DateFrom = Sep1, DateTo = Sep30, Filters = [Regions(w)],
        });
        Assert.False(r.Complete);
        Assert.Contains(r.Checks, c => c.Code == "plan" && !c.Ok && c.Text.Contains("Плана нет", StringComparison.Ordinal));
        Assert.All(r.Rows, row => Assert.Null(row[r.Columns.ToList().FindIndex(c => c.Code == "plan_execution")]));
    }

    [Fact]
    public void T27_stock_and_sales_compatibility_snapshot_vs_flow()
    {
        var byDate = DataCompatibility.Check("stock_units", "revenue", ["date"], "day");
        Assert.Equal(CompatibilityClasses.NotCompatible, byDate.Class);
        var byProduct = DataCompatibility.Check("stock_units", "revenue", ["product"]);
        Assert.Equal(CompatibilityClasses.WithLimitations, byProduct.Class);
        Assert.Contains(byProduct.Aspects, a => a.Code == "grain" && a.Text.Contains("остаток на дату", StringComparison.Ordinal));
    }

    [PostgresFact]
    public async Task T29_incomplete_period_is_flagged_not_counted_as_zero()
    {
        var r = await Query(new AnalyticsQuery { Metrics = ["revenue"], DateFrom = Oct1, DateTo = new DateOnly(2026, 11, 20), Filters = [Regions(fx.World)] });
        // Данные мира — по 31.10, но в базе стенда могут быть и другие продажи: проверяем, что период сверяется с последним днём данных.
        Assert.Contains(r.Checks, c => c.Code == "sales.period");
        Assert.Equal(fx.World.Between(Oct1, Oct31).Sum(l => l.Revenue), r.Totals["revenue"]!.Value, 0);
    }

    [Fact]
    public void T30_incompatible_granularity_has_its_own_status()
    {
        Assert.Equal(ResearchStatuses.InsufficientGranularity,
            ResearchStatuses.OfQuery("unsupported", "План продаж в OneBase — помесячный (Google Sheets / Linko): по дням и неделям он не делится"));
        var c = DataCompatibility.Check("plan_kg", "kg", ["date"], "day");
        Assert.Equal(CompatibilityClasses.AfterAggregation, c.Class);
        Assert.Equal("month", c.Grain);
    }

    [PostgresFact]
    public async Task T30b_plan_by_day_is_rejected_by_engine_with_unsupported()
    {
        var ex = await Assert.ThrowsAsync<AnalyticsQueryException>(() => Query(new AnalyticsQuery
            { Metrics = ["plan_kg"], Dimensions = ["date"], Grain = "day", DateFrom = Sep1, DateTo = Sep30 }));
        Assert.Equal("unsupported", ex.Code);
        Assert.Equal(ResearchStatuses.InsufficientGranularity, ResearchStatuses.OfQuery(ex.Code, ex.Message));
    }

    [PostgresFact]
    public async Task T31_duplicate_keys_in_structured_series_are_counted_and_reported()
    {
        var dates = Enumerable.Range(0, 30).Select(i => Sep1.AddDays(i)).ToList();
        var a = dates.Select((_, i) => (double?)(100 + i * 3 + (i % 3))).ToList();
        var b = dates.Select((_, i) => (double?)(50 + i * 2 - (i % 4))).ToList();
        var o = await Compute(new ComputeRequest
        {
            Method = "correlation",
            Data = new ComputeData
            {
                Series = [new InlineSeries("a", [.. a, 999], [.. dates, dates[5]]), new InlineSeries("b", b, dates)],
            },
        });
        Assert.Equal("ok", o.Status);
        var json = JsonSerializer.SerializeToElement(o.Result, ComputeService.JsonOptions);
        var report = json.GetProperty("reports")[0];
        Assert.Equal(1, report.GetProperty("duplicates").GetInt32());
        Assert.Equal(30, report.GetProperty("sample_size").GetInt32());
        Assert.Contains(report.GetProperty("warnings").EnumerateArray(), w => w.GetString()!.Contains("Дублей ключей", StringComparison.Ordinal));
    }

    [PostgresFact]
    public async Task T33_access_restrictions_hold_for_slices_compute_and_research()
    {
        var w = fx.World;
        // Пользователь одного региона: визиты и продажи — только своего региона, даже если просит все.
        var r = await Query(new AnalyticsQuery { Metrics = ["visits_done", "revenue"], Dimensions = ["region"], DateFrom = Oct1, DateTo = Oct31 }, fx.RegionUserId);
        Assert.All(r.Rows, row => Assert.Equal(w.Regions[1], row[0]));
        Assert.Equal(ResearchWorld.Done(1, Oct1, Oct31), (int)Cell(r, r.Rows.Single(), "visits_done"));

        // Остатки и риски всей организации — нет; план исследования это учитывает.
        await Assert.ThrowsAsync<AnalyticsQueryException>(() => Query(new AnalyticsQuery { Metrics = ["stock_units"], Dimensions = ["product"] }, fx.RegionUserId));
        var health = await Research(new ResearchRequest { Kind = ResearchKinds.CompanyHealth, To = Oct31 }, fx.RegionUserId);
        Assert.DoesNotContain(health.Plan.Operations, o => o.Id == "q_stock");
        Assert.Equal(ResearchStatuses.NoAccess, health.Plan.Operations.Single(o => o.Id == "p_risks").Status);
        Assert.NotEqual("вся организация", health.Plan.AccessScope);

        // Без прав на продажи и финансы — отказ, а не пустой результат.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Research(new ResearchRequest { Kind = ResearchKinds.Anomalies }, fx.NoAccessId));
        var denied = DataCompatibility.Check("payments", "revenue", null, null, new AnalytikPrincipal(fx.RegionUserId, new HashSet<string> { "sales.read" }, SalesRowScope.Organization));
        Assert.Equal(CompatibilityClasses.AccessDenied, denied.Class);
    }

    [PostgresFact]
    public async Task T34_large_result_is_paged_and_compute_reads_every_page()
    {
        var w = fx.World;
        var r = await Query(new AnalyticsQuery { Metrics = ["revenue"], Dimensions = ["date", "market"], Grain = "day", DateFrom = Sep1, DateTo = Oct31, Filters = [Regions(w)] });
        Assert.True(r.HasMore);
        Assert.True(r.TotalRows > r.Rows.Count);
        Assert.Contains(r.Checks, c => c.Code == "limit");

        // Расчёт по участникам читает все страницы: сумма по точкам = итог мира.
        var o = await Compute(new ComputeRequest { Method = "describe", Data = new ComputeData { Metrics = ["revenue"], Dimension = "market", From = Sep1, To = Oct31, Filters = [Regions(w)] } });
        var stats = JsonSerializer.SerializeToElement(o.Result, ComputeService.JsonOptions).GetProperty("stats");
        Assert.Equal((double)w.Between(Sep1, Oct31).Sum(l => l.Revenue), stats.GetProperty("sum").GetDouble(), 0);
    }

    [PostgresFact]
    public async Task T35_partial_result_keeps_data_and_names_the_gap()
    {
        var r = await Research(new ResearchRequest { Kind = ResearchKinds.CompanyHealth, To = Oct31, Filters = [Regions(fx.World)] });
        Assert.Equal("partial", r.Status);
        var plan = r.Plan.Operations.Single(o => o.Id == "q_plan");
        Assert.Equal(ResearchStatuses.PartialResult, plan.Status);
        Assert.Contains(r.Limitations, l => l.Contains("План и прогноз месяца", StringComparison.Ordinal) && l.Contains("Плана нет", StringComparison.Ordinal));
        Assert.Contains(r.Findings, f => f.Section == "metrics" && f.Level == EvidenceLevels.Fact);
    }

    // ---------- Statistics Engine на данных ----------

    [PostgresFact]
    public async Task S1_group_matrix_separates_part_whole_from_group_vs_rest_with_bh()
    {
        var o = await Compute(new ComputeRequest
        {
            Method = "correlation_matrix",
            Data = new ComputeData { Metrics = ["revenue"], Grain = "day", From = Sep1, To = Oct31, Filters = [Regions(fx.World)] },
            Params = new Dictionary<string, JsonElement> { ["members"] = JsonSerializer.SerializeToElement("product_group") },
        });
        Assert.Equal("ok", o.Status);
        var r = JsonSerializer.SerializeToElement(o.Result, ComputeService.JsonOptions);
        var all = r.GetProperty("confirmed").EnumerateArray().Concat(r.GetProperty("flagged").EnumerateArray()).Concat(r.GetProperty("exploratory").EnumerateArray()).ToList();
        Assert.Contains(all, p => p.GetProperty("kind").GetString() == "part_whole");
        Assert.Contains(all, p => p.GetProperty("kind").GetString() == "part_rest");
        // «Все, кроме X» — только в паре с самим X: другой участник входит в «всех, кроме X», и связь с ними завышена.
        Assert.All(all.Where(p => p.GetProperty("kind").GetString() == "part_rest"), p =>
        {
            var (a, b) = (p.GetProperty("a").GetString()!, p.GetProperty("b").GetString()!);
            var (rest, member) = a.StartsWith("Все, кроме", StringComparison.Ordinal) ? (a, b) : (b, a);
            Assert.Equal($"Все, кроме «{member}»", rest);
        });
        Assert.DoesNotContain(all, p => p.GetProperty("a").GetString()!.StartsWith("Все, кроме", StringComparison.Ordinal)
                                         && p.GetProperty("b").GetString()!.StartsWith("Все, кроме", StringComparison.Ordinal));
        // Часть и целое никогда не «подтверждённая связь»: только с оговоркой.
        Assert.DoesNotContain(r.GetProperty("confirmed").EnumerateArray(), p => p.GetProperty("kind").GetString() == "part_whole");
        Assert.All(all.Where(p => p.TryGetProperty("q", out var q) && q.ValueKind == JsonValueKind.Number), p => Assert.InRange(p.GetProperty("q").GetDouble(), 0, 1));
        Assert.True(r.GetProperty("matrix").GetProperty("titles").GetArrayLength() >= 3);
    }

    [PostgresFact]
    public async Task S2_weekend_dip_is_not_an_anomaly_but_seasonality()
    {
        var o = await Compute(new ComputeRequest
        {
            Method = "anomaly_scan", Data = new ComputeData { Metrics = ["revenue", "orders"], Grain = "day", From = Sep1, To = Oct31, Filters = [Regions(fx.World)] },
        });
        var r = JsonSerializer.SerializeToElement(o.Result, ComputeService.JsonOptions);
        var sundays = r.GetProperty("anomalies").EnumerateArray()
            .Count(a => DateOnly.Parse(a.GetProperty("date").GetString()!, CultureInfo.InvariantCulture).DayOfWeek == DayOfWeek.Sunday);
        Assert.Equal(0, sundays);

        var dec = await Compute(new ComputeRequest { Method = "decomposition", Data = new ComputeData { Metrics = ["revenue"], Grain = "day", From = Sep1, To = Oct31, Filters = [Regions(fx.World)] } });
        var d = JsonSerializer.SerializeToElement(dec.Result, ComputeService.JsonOptions);
        Assert.True(d.GetProperty("seasonalStrength").GetDouble() > 0.6);
        var series = d.GetProperty("series").EnumerateArray().ToList();
        var sunday = series.Where(x => DateOnly.Parse(x.GetProperty("date").GetString()!, CultureInfo.InvariantCulture).DayOfWeek == DayOfWeek.Sunday).Average(x => x.GetProperty("seasonal").GetDouble());
        var tuesday = series.Where(x => DateOnly.Parse(x.GetProperty("date").GetString()!, CultureInfo.InvariantCulture).DayOfWeek == DayOfWeek.Tuesday).Average(x => x.GetProperty("seasonal").GetDouble());
        Assert.True(sunday < tuesday);
    }

    [PostgresFact]
    public async Task S2b_planted_drop_is_found_with_consistent_direction_and_sundays_are_not()
    {
        var dates = Enumerable.Range(0, 70).Select(i => new DateOnly(2026, 8, 3).AddDays(i)).ToList();
        var rng = new SplitMix64(77);
        var values = dates.Select(d => (double?)((d.DayOfWeek == DayOfWeek.Sunday ? 500 : 1000) + rng.NextNormal() * 20)).ToList();
        var drop = dates.FindIndex(d => d == new DateOnly(2026, 9, 30));
        values[drop] = 400;
        var o = await Compute(new ComputeRequest
        {
            Method = "anomaly_scan", Data = new ComputeData { Series = [new InlineSeries("revenue_inline", values, dates)] },
            Params = new Dictionary<string, JsonElement> { ["period"] = JsonSerializer.SerializeToElement(7) },
        });
        Assert.Equal("ok", o.Status);
        var anomalies = JsonSerializer.SerializeToElement(o.Result, ComputeService.JsonOptions).GetProperty("anomalies").EnumerateArray().ToList();
        var hit = Assert.Single(anomalies, a => a.GetProperty("date").GetString() == "2026-09-30");
        Assert.Equal("down", hit.GetProperty("direction").GetString());
        Assert.Contains(hit.GetProperty("methods").EnumerateArray(), m => m.GetString() == "sharp_drop");
        Assert.DoesNotContain(hit.GetProperty("methods").EnumerateArray(), m => m.GetString() == "sharp_rise");
        Assert.DoesNotContain(anomalies, a => DateOnly.Parse(a.GetProperty("date").GetString()!, CultureInfo.InvariantCulture).DayOfWeek == DayOfWeek.Sunday);
    }

    [PostgresFact]
    public async Task S3_compute_by_saved_query_id_uses_that_slice()
    {
        var q = await Query(new AnalyticsQuery { Metrics = ["revenue", "kg"], Dimensions = ["date"], Grain = "day", DateFrom = Sep1, DateTo = Sep30, Filters = [Regions(fx.World)] });
        var o = await Compute(new ComputeRequest { Method = "correlation", Data = new ComputeData { QueryId = q.QueryId } });
        Assert.Equal("ok", o.Status);
        Assert.Equal((Sep1, Sep30), (o.From, o.To));
        var report = JsonSerializer.SerializeToElement(o.Result, ComputeService.JsonOptions).GetProperty("reports")[0];
        Assert.Equal("revenue", report.GetProperty("metric_a").GetString());
        Assert.Equal("kg", report.GetProperty("metric_b").GetString());
        Assert.True(report.GetProperty("coefficient").GetDouble() > 0.9);
        Assert.Equal(30, report.GetProperty("sample_size").GetInt32());
    }

    // ---------- исследования-шаблоны ----------

    [PostgresFact]
    public async Task R1_correlations_research_confirms_links_reuses_slices_and_records_everything()
    {
        var r = await Research(new ResearchRequest { Kind = ResearchKinds.Correlations, From = Sep1, To = Oct31, Filters = [Regions(fx.World)] });
        Assert.Contains(r.Status, new[] { "completed", "partial" });
        Assert.Contains(r.Findings, f => f.Section == "correlations" && f.Level == EvidenceLevels.Correlation);
        Assert.Contains(r.Findings, f => f.Section == "significance" && f.Text.Contains("Бенджамини", StringComparison.Ordinal));
        Assert.True(((ResearchView)r.Data!).Matrices.Count >= 2);
        Assert.True(r.Timings.CacheHits > 0, "одинаковые срезы (выручка по дням) должны браться из кэша");
        Assert.Single(r.Plan.Waves());

        using var scope = fx.Services.CreateScope();
        var row = await scope.ServiceProvider.GetRequiredService<IAnalytikDb>().Investigations.AsNoTracking().SingleAsync(i => i.Id == r.Id);
        Assert.Contains("\"research\"", row.Roles!);
        Assert.Equal(r.Status, row.Status);
        Assert.Contains("\"findings\"", row.Hypotheses!);
        var computations = await scope.ServiceProvider.GetRequiredService<IAnalytikDb>().Computations.AsNoTracking().CountAsync(c => c.InvestigationId == r.Id);
        Assert.True(computations >= 3);
    }

    [PostgresFact]
    public async Task R2_decline_drivers_find_lost_outlet_and_reject_price()
    {
        var w = fx.World;
        var r = await Research(new ResearchRequest { Kind = ResearchKinds.DeclineDrivers, From = Oct1, To = Oct31, Filters = [new AnalyticsFilter("region", "in", [w.Regions[0]])] });
        Assert.Equal((new DateOnly(2026, 8, 31), new DateOnly(2026, 9, 30)), (r.Plan.CompareFrom!.Value, r.Plan.CompareTo!.Value));
        Assert.Contains(r.Findings, f => f.Section == "summary" && f.Text.StartsWith("Выручка снизилась", StringComparison.Ordinal));
        Assert.Equal("confirmed", r.Hypotheses.Single(h => h.Id == "churn").Status);
        Assert.Contains(r.Hypotheses.Single(h => h.Id == "outlets").Status, new[] { "confirmed", "partially_confirmed" });
        // Цена товара Б выросла: эффект цены положительный — не причина снижения.
        Assert.Equal("rejected", r.Hypotheses.Single(h => h.Id == "pvm_price").Status);
        Assert.Contains(r.Findings, f => f.Section == "metrics" && f.Text.StartsWith("Выручка:", StringComparison.Ordinal) && f.Level == EvidenceLevels.Fact);
    }

    [PostgresFact]
    public async Task R3_agent_efficiency_ranks_agents_by_revenue_per_visit()
    {
        var r = await Research(new ResearchRequest { Kind = ResearchKinds.AgentEfficiency, From = Oct1, To = Oct31, Filters = [Regions(fx.World)] });
        var table = ((ResearchView)r.Data!).Tables.Single(t => t.Id == "agents");
        Assert.Equal(3, table.Rows.Count);
        var perVisit = table.Rows.Select(x => Convert.ToDouble(x[5], CultureInfo.InvariantCulture)).ToList();
        Assert.Equal(perVisit.OrderByDescending(x => x).ToList(), perVisit);
        Assert.Contains(r.Findings, f => f.Section == "correlations");
    }

    [PostgresFact]
    public async Task R4_visits_up_sales_down_region_is_found()
    {
        var w = fx.World;
        var r = await Research(new ResearchRequest { Kind = ResearchKinds.VisitsVsSales, From = Oct1, To = Oct31, Filters = [Regions(w)] });
        var summary = r.Findings.Single(f => f.Section == "summary");
        Assert.Contains(w.Regions[0], summary.Text);
        Assert.DoesNotContain(w.Regions[1], summary.Text);
        Assert.Contains(r.Limitations, l => l.Contains("по филиалу точки", StringComparison.Ordinal));
    }

    [PostgresFact]
    public async Task R4b_no_matching_region_still_shows_the_picture_and_closest_case()
    {
        var w = fx.World;
        var r = await Research(new ResearchRequest
        {
            Kind = ResearchKinds.VisitsVsSales, From = Oct1, To = Oct31, Filters = [new AnalyticsFilter("region", "in", [w.Regions[1]])],
        });
        Assert.Contains("нет", r.Findings.Single(f => f.Section == "summary").Text, StringComparison.Ordinal);
        Assert.Contains(r.Findings, f => f.Section == "metrics" && f.Text.StartsWith("Регионов в сравнении: 1;", StringComparison.Ordinal));
        Assert.Contains(r.Findings, f => f.Section == "differences" && f.Text.StartsWith("Ближе всего к условию", StringComparison.Ordinal) && f.Text.Contains(w.Regions[1], StringComparison.Ordinal));
    }

    [PostgresFact]
    public async Task R5_group_influence_variance_shares_sum_to_100()
    {
        var r = await Research(new ResearchRequest { Kind = ResearchKinds.GroupInfluence, From = Sep1, To = Oct31, Filters = [Regions(fx.World)] });
        var table = ((ResearchView)r.Data!).Tables.Single(t => t.Id == "groups");
        var shares = table.Rows.Select(x => Convert.ToDouble(x[2], CultureInfo.InvariantCulture)).ToList();
        Assert.Equal(100, shares.Sum(), 1);
        Assert.Equal(100, table.Rows.Sum(x => Convert.ToDouble(x[1], CultureInfo.InvariantCulture)), 1);
    }

    [PostgresFact]
    public async Task R6_anomalies_research_respects_weekly_seasonality()
    {
        var r = await Research(new ResearchRequest { Kind = ResearchKinds.Anomalies, From = Sep1, To = Oct31, Filters = [Regions(fx.World)] });
        var table = ((ResearchView)r.Data!).Tables.Single(t => t.Id == "anomalies");
        var sundays = table.Rows.Where(x => DateOnly.Parse(x[0]!.ToString()!, CultureInfo.InvariantCulture).DayOfWeek == DayOfWeek.Sunday).ToList();
        Assert.True(sundays.Count == 0, string.Join(" | ", sundays.Select(x => string.Join("; ", x))));
        Assert.Contains(r.Findings, f => f.Section == "summary");
    }

    [PostgresFact]
    public async Task R7_research_can_be_cancelled_and_says_so()
    {
        Guid? id = null;
        var r = await Research(new ResearchRequest { Kind = ResearchKinds.Correlations, From = Sep1, To = Oct31 }, progress: async _ =>
        {
            if (id is not null)
            {
                return;
            }

            using var scope = fx.Services.CreateScope();
            id = await scope.ServiceProvider.GetRequiredService<IAnalytikDb>().Investigations.AsNoTracking()
                .Where(i => i.UserId == fx.AdminId && i.Status == "running").OrderByDescending(i => i.StartedAt).Select(i => (Guid?)i.Id).FirstAsync();
            Assert.True(await scope.ServiceProvider.GetRequiredService<ResearchService>().CancelAsync(id.Value, fx.AdminId, CancellationToken.None));
        });
        Assert.Equal("cancelled", r.Status);
        Assert.Contains(r.Plan.Operations, o => o.Status == ResearchStatuses.Cancelled);
    }

    [PostgresFact]
    public async Task R8_unknown_kind_is_rejected_with_list()
    {
        var ex = await Assert.ThrowsAsync<ComputeException>(() => Research(new ResearchRequest { Kind = "everything" }));
        Assert.Contains(ResearchKinds.Correlations, ex.Message);
    }
}
