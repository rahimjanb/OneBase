using OneBase.Application.Analytik.Compute;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Research;
using OneBase.Application.Analytik.Semantic;

namespace OneBase.AI.Tests;

/// <summary>
/// Контракт Statistics Engine (ТЗ Phase 2, тесты 1–20 вместе с StatisticsEngineMathTests): отчёт корреляции с проверками данных, поправка на
/// множественные сравнения, «часть и целое», общий тренд; совместимость пар из ТЗ; выбор шаблона исследования по вопросу (RU, UZ); уровни вывода.
/// </summary>
public class StatisticsContractTests
{
    private static List<double?> Line(int n, Func<int, double> f) => Enumerable.Range(0, n).Select(i => (double?)f(i)).ToList();

    // Детерминированный шум без Random: дробная часть золотого сечения.
    private static double Noise(int i, double scale) => ((i * 0.6180339887498949) % 1 - 0.5) * scale;

    [Fact]
    public void Correlation_report_has_contract_fields_and_ci_contains_coefficient()
    {
        var x = Line(60, i => i + Noise(i, 20));
        var y = Line(60, i => 2 * i + Noise(i * 7, 30));
        var r = CorrelationAnalysis.Analyze("revenue", "orders", x, y, "pearson", timeSeries: false, from: "2026-09-01", to: "2026-10-30");
        Assert.Equal("pearson", r.Method);
        Assert.Equal(60, r.SampleSize);
        Assert.Equal(0, r.MissingValues);
        Assert.NotNull(r.ConfidenceInterval);
        Assert.InRange(r.Coefficient!.Value, r.ConfidenceInterval!.Lower, r.ConfidenceInterval.Upper);
        Assert.True(r.PValue < 0.001);
        Assert.Equal("valid", r.Status);
        Assert.Equal(EvidenceLevels.Correlation, r.Level);
        Assert.All(CorrelationAnalysis.Methods, m => Assert.True(r.Coefficients.ContainsKey(m)));
        Assert.Equal(("2026-09-01", "2026-10-30"), (r.PeriodFrom, r.PeriodTo));
    }

    [Fact]
    public void Missing_values_are_dropped_pairwise_and_counted()
    {
        var x = Line(40, i => i);
        var y = Line(40, i => 3 * i + Noise(i, 4));
        x[3] = null;
        y[10] = null;
        y[11] = double.NaN;
        var r = CorrelationAnalysis.Analyze("a", "b", x, y, "spearman", false);
        Assert.Equal(37, r.SampleSize);
        Assert.Equal(3, r.MissingValues);
        Assert.Contains(r.Warnings, w => w.StartsWith("Пропущенных значений: 3", StringComparison.Ordinal));
    }

    [Fact]
    public void Outliers_are_detected_and_pearson_is_warned()
    {
        var x = Line(40, i => i + Noise(i, 2));
        var y = Line(40, i => i + Noise(i * 3, 2));
        y[20] = 400;
        var r = CorrelationAnalysis.Analyze("a", "b", x, y, "pearson", false);
        Assert.Equal(1, r.OutliersB);
        Assert.Contains(r.Warnings, w => w.Contains("Пирсон к ним чувствителен", StringComparison.Ordinal));
        Assert.Contains(r.Warnings, w => w.Contains("расходятся", StringComparison.Ordinal));
    }

    [Fact]
    public void Zero_variance_is_invalid_not_zero_correlation()
    {
        var r = CorrelationAnalysis.Analyze("a", "flat", Line(20, i => i), Line(20, _ => 5), "pearson", false);
        Assert.Equal("invalid", r.Status);
        Assert.Null(r.Coefficient);
        Assert.Equal(EvidenceLevels.Unverified, r.Level);
        Assert.Contains(r.Warnings, w => w.Contains("нулевая дисперсия", StringComparison.Ordinal));
    }

    [Fact]
    public void Small_samples_are_unreliable_or_invalid_by_method_threshold()
    {
        var tiny = CorrelationAnalysis.Analyze("a", "b", Line(4, i => i), Line(4, i => 2 * i), "pearson", false);
        Assert.Equal("invalid", tiny.Status);
        var small = CorrelationAnalysis.Analyze("a", "b", Line(8, i => i), Line(8, i => 2 * i + Noise(i, 1)), "kendall", false);
        Assert.Equal("unreliable", small.Status);
        Assert.Equal(EvidenceLevels.Unverified, small.Level);
        var modest = CorrelationAnalysis.Analyze("a", "b", Line(15, i => i), Line(15, i => 2 * i + Noise(i, 3)), "pearson", false);
        Assert.Contains(modest.Warnings, w => w.StartsWith("Малая выборка", StringComparison.Ordinal));
    }

    [Fact]
    public void Common_trend_is_not_mistaken_for_a_relationship()
    {
        // Оба ряда растут, а их колебания независимы: связь — только через общий тренд.
        var x = Line(80, i => i * 10 + Noise(i, 30));
        var y = Line(80, i => i * 5 + Noise(i * 13 + 5, 30));
        var r = CorrelationAnalysis.Analyze("revenue", "visits_done", x, y, "pearson", timeSeries: true);
        Assert.True(r.Coefficient > 0.9);
        Assert.NotNull(r.Detrended);
        Assert.True(r.DetrendedP > 0.05);
        Assert.Equal(EvidenceLevels.Hypothesis, r.Level);
        Assert.Contains(r.Warnings, w => w.StartsWith("Без общего тренда", StringComparison.Ordinal));
    }

    [Fact]
    public void Common_weekday_cycle_is_not_a_relationship()
    {
        // Оба ряда проседают в воскресенье и субботу, а в остальном независимы: «связь» — только общий календарь.
        double Week(int i) => (i % 7) switch { 6 => 0.5, 5 => 0.8, _ => 1.0 };
        var rx = new SplitMix64(11);
        var ry = new SplitMix64(4242);
        var x = Line(91, i => 1000 * Week(i) + rx.NextNormal() * 30);
        var y = Line(91, i => 400 * Week(i) + ry.NextNormal() * 12);
        var r = CorrelationAnalysis.Analyze("revenue", "visits_done", x, y, "pearson", timeSeries: true, seasonPeriod: 7);
        Assert.True(r.Coefficient > 0.8);
        Assert.NotNull(r.Deseasonalized);
        Assert.True(r.DeseasonalizedP > 0.05, $"без дня недели r = {r.Deseasonalized}, p = {r.DeseasonalizedP}");
        Assert.Equal(EvidenceLevels.Hypothesis, r.Level);
        Assert.Contains(r.Warnings, w => w.StartsWith("Связь объясняется общим циклом дня недели", StringComparison.Ordinal));

        // Настоящая связь сверх общего цикла остаётся подтверждённой.
        var z = x.Select((v, i) => (double?)(v!.Value * 0.4 + ry.NextNormal() * 3)).ToList();
        var real = CorrelationAnalysis.Analyze("revenue", "kg", x, z, "pearson", timeSeries: true, seasonPeriod: 7);
        Assert.True(real.DeseasonalizedP < 0.001);
        Assert.Equal(EvidenceLevels.Correlation, real.Level);
    }

    [Fact]
    public void Partially_loaded_last_day_is_excluded_from_research_period()
    {
        var principal = new AnalytikPrincipal(Guid.NewGuid(), new HashSet<string> { "sales.read" }, SalesRowScope.Organization);
        var ranges = new Dictionary<string, (DateOnly?, DateOnly?)> { ["sales"] = (new DateOnly(2026, 1, 1), new DateOnly(2026, 11, 12)) };
        // Linko загружал заказы 12.11 в 02:39 по Ташкенту — 12.11 неполный.
        var ctx = new ResearchContext(principal, new DateOnly(2026, 11, 14), ranges, [], new DateTimeOffset(2026, 11, 11, 21, 39, 0, TimeSpan.Zero));
        Assert.Equal((new DateOnly(2026, 11, 11), true), ctx.LastFullDay());
        var plan = ResearchPlanner.Build(new ResearchRequest { Kind = ResearchKinds.DeclineDrivers }, ctx);
        Assert.Equal(new DateOnly(2026, 11, 11), plan.To);
        Assert.Contains(plan.Limitations, l => l.Contains("12.11.2026", StringComparison.Ordinal) && l.Contains("не полностью", StringComparison.Ordinal));

        // Загрузка на следующий день — последний день полный.
        var fresh = ctx with { SalesSyncedAt = new DateTimeOffset(2026, 11, 13, 1, 0, 0, TimeSpan.Zero) };
        Assert.Equal((new DateOnly(2026, 11, 12), false), fresh.LastFullDay());
    }

    [Fact]
    public void Part_whole_dependency_downgrades_to_hypothesis()
    {
        var dep = MetricPassports.Dependency("returns_revenue", "gross_revenue");
        Assert.NotNull(dep);
        Assert.Contains("часть", dep);
        var r = CorrelationAnalysis.Analyze("visits_with_order", "visits_done", Line(50, i => i + Noise(i, 5)), Line(50, i => 2 * i + Noise(i * 3, 5)), "pearson", false,
            dependency: MetricPassports.Dependency("visits_with_order", "visits_done"));
        Assert.Equal(EvidenceLevels.Hypothesis, r.Level);
        Assert.Contains(r.Warnings, w => w.Contains("Математическая зависимость", StringComparison.Ordinal));
    }

    [Fact]
    public void Multiple_comparisons_turn_chance_findings_into_exploratory_hypotheses()
    {
        // 30 пар шума и одна настоящая связь: после поправки остаётся только настоящая.
        var reports = new List<CorrelationReport>();
        static List<double?> Random(ulong seed, int n)
        {
            var rng = new SplitMix64(seed);
            return Enumerable.Range(0, n).Select(_ => (double?)rng.NextNormal()).ToList();
        }

        var baseX = Random(1, 40);
        for (var k = 0; k < 30; k++)
        {
            reports.Add(CorrelationAnalysis.Analyze("x", $"noise{k}", baseX, Random((ulong)(100 + k), 40), "pearson", false));
        }

        var jitter = Random(999, 40);
        reports.Add(CorrelationAnalysis.Analyze("x", "real", baseX, baseX.Select((v, i) => v * 2 + jitter[i] * 0.3).ToList(), "pearson", false));
        var adjusted = CorrelationAnalysis.Adjust(reports);
        Assert.All(adjusted, r => Assert.NotNull(r.QValue));
        Assert.All(adjusted.Zip(reports), z => Assert.True(z.First.QValue >= z.Second.PValue - 1e-12));
        var confirmed = adjusted.Where(r => r.Significant).ToList();
        Assert.Contains(confirmed, r => r.MetricB == "real");
        Assert.DoesNotContain(confirmed, r => r.MetricB.StartsWith("noise", StringComparison.Ordinal));
        Assert.All(adjusted.Where(r => r.PValue < 0.05 && r.QValue >= 0.05), r => Assert.Equal(EvidenceLevels.Hypothesis, r.Level));
    }

    [Fact]
    public void Incompatible_pair_is_invalid_with_reason()
    {
        var r = CorrelationAnalysis.Analyze("stock_units", "revenue", Line(30, i => i), Line(30, i => i), "pearson", true,
            compatibility: CompatibilityClasses.NotCompatible, compatibilityText: "Остаток — снимок на сейчас");
        Assert.Equal("invalid", r.Status);
        Assert.Contains("Остаток — снимок на сейчас", r.Warnings);
    }

    // ---------- совместимость: примеры ТЗ ----------

    [Theory]
    [InlineData("revenue", "orders", "region", null, CompatibilityClasses.Direct)]
    [InlineData("revenue", "revenue_share", "product_group", null, CompatibilityClasses.WithLimitations)]
    [InlineData("revenue", "visits_done", "region", null, CompatibilityClasses.WithLimitations)]
    [InlineData("revenue", "visits_done", "agent", null, CompatibilityClasses.Direct)]
    [InlineData("revenue", "visits_done", "product_group", null, CompatibilityClasses.NotCompatible)]
    [InlineData("revenue", "promotions", "product", null, CompatibilityClasses.WithLimitations)]
    [InlineData("kg", "plan_kg", "date", "day", CompatibilityClasses.AfterAggregation)]
    [InlineData("kg", "plan_kg", "date", "month", CompatibilityClasses.Direct)]
    [InlineData("revenue", "stock_units", "date", "day", CompatibilityClasses.NotCompatible)]
    [InlineData("revenue", "dealer_debt", "dealer", null, CompatibilityClasses.WithLimitations)]
    [InlineData("revenue", "payments", "date", "day", CompatibilityClasses.WithLimitations)]
    [InlineData("revenue", "primary_kg", "region", "month", CompatibilityClasses.WithLimitations)]
    [InlineData("revenue", "expenses", null, null, CompatibilityClasses.SourceMissing)]
    [InlineData("revenue", "no_such_metric", null, null, CompatibilityClasses.SourceMissing)]
    public void Pairs_from_the_spec_are_classified(string a, string b, string? dim, string? grain, string expected)
    {
        var r = DataCompatibility.Check(a, b, dim is null ? null : [dim], grain);
        Assert.True(expected == r.Class, $"{a} × {b} по {dim}: {r.Class} — {string.Join(" | ", r.Aspects.Select(x => $"{x.Code}:{x.Class}"))}");
        Assert.False(string.IsNullOrWhiteSpace(r.Advice));
    }

    [Fact]
    public void Payments_and_revenue_are_never_mixed_silently()
    {
        var r = DataCompatibility.Check("payments", "revenue", ["region"], null);
        Assert.Contains(r.Aspects, x => x.Code == "meaning" && x.Text.Contains("разница не равна долгу", StringComparison.Ordinal));
        Assert.Contains(r.Aspects, x => x.Code == "region" && x.Class == CompatibilityClasses.WithLimitations);
        var notes = DataCompatibility.Notes([SemanticCatalog.Metric("revenue")!, SemanticCatalog.Metric("payments")!], ["date"], "day");
        Assert.Contains(notes, n => n.StartsWith("Оплаты — поступившие деньги", StringComparison.Ordinal));
    }

    [Fact]
    public void Restricted_principal_gets_access_denied_for_stock()
    {
        var scope = new SalesRowScope(SalesRowScope.RegionLevel, [1], [], [], null, "регион");
        var p = new AnalytikPrincipal(Guid.NewGuid(), new HashSet<string> { "sales.read" }, scope);
        Assert.Equal(CompatibilityClasses.AccessDenied, DataCompatibility.Check("stock_units", "revenue", ["product"], null, p).Class);
    }

    // ---------- шаблон исследования по вопросу ----------

    [Theory]
    [InlineData("Найди все корреляции между продажами, регионами и товарными группами за последние 103 дня.", ResearchKinds.Correlations, 103)]
    [InlineData("Почему снизились продажи? Найди конкретные факторы.", ResearchKinds.DeclineDrivers, null)]
    [InlineData("Сравни эффективность торговых представителей по визитам и продажам.", ResearchKinds.AgentEfficiency, null)]
    [InlineData("Найди регионы, где количество визитов растёт, а продажи падают.", ResearchKinds.VisitsVsSales, null)]
    [InlineData("Какие товарные группы сильнее всего влияют на общую выручку?", ResearchKinds.GroupInfluence, null)]
    [InlineData("Найди аномалии во всех доступных показателях.", ResearchKinds.Anomalies, null)]
    [InlineData("Проанализируй эффективность промоакций.", ResearchKinds.PromoEffect, null)]
    [InlineData("Проанализируй состояние всей компании и выдели основные риски.", ResearchKinds.CompanyHealth, null)]
    [InlineData("Savdo nega kamaydi? Sabablarini top", ResearchKinds.DeclineDrivers, null)]
    [InlineData("Сотувлар нега камайди?", ResearchKinds.DeclineDrivers, null)]
    [InlineData("Oxirgi 60 kun ichida anomaliyalarni top", ResearchKinds.Anomalies, 60)]
    public void Spec_questions_pick_the_right_research(string question, string kind, int? days)
    {
        var m = ResearchPlanner.Match(question);
        Assert.NotNull(m);
        Assert.Equal(kind, m.Kind);
        Assert.Equal(days, m.Days);
    }

    [Theory]
    [InlineData("Какая выручка в октябре?")]
    [InlineData("Выручка по товарным группам за каждый день")]
    [InlineData("Сколько визитов сделал Олимжон вчера?")]
    public void Simple_questions_are_not_turned_into_research(string question) => Assert.Null(ResearchPlanner.Match(question));

    [Theory]
    [InlineData("Есть ли связь между числом визитов и выручкой по регионам за октябрь? Посчитай корреляцию.")]
    [InlineData("Посчитай корреляцию выручки и визитов по ТП")]
    [InlineData("Найди корреляции по регионам")]
    [InlineData("Hududlar bo'yicha tashriflar va tushum o'rtasida bog'liqlik bormi?")]
    [InlineData("Почему снизились продажи с 1 по 15 октября?")]
    [InlineData("Найди аномалии с 01.10 по 15.10")]
    public void Specific_relations_and_exact_dates_go_to_the_agent_not_to_a_template(string question) =>
        Assert.Null(ResearchPlanner.Match(question, new DateOnly(2026, 11, 14)));

    [Theory]
    [InlineData("Почему снизились продажи в октябре?", ResearchKinds.DeclineDrivers, "2026-10-01", "2026-10-31")]
    [InlineData("Найди аномалии за сентябрь и октябрь", ResearchKinds.Anomalies, "2026-09-01", "2026-10-31")]
    [InlineData("Почему упали продажи в прошлом месяце?", ResearchKinds.DeclineDrivers, "2026-10-01", "2026-10-31")]
    [InlineData("Почему упали продажи в декабре?", ResearchKinds.DeclineDrivers, "2025-12-01", "2025-12-31")]
    [InlineData("Oktabrda savdo nega kamaydi?", ResearchKinds.DeclineDrivers, "2026-10-01", "2026-10-31")]
    [InlineData("Проанализируй эффективность промоакций за май 2026", ResearchKinds.PromoEffect, "2026-05-01", "2026-05-31")]
    [InlineData("Найди все корреляции между продажами и регионами в этом месяце", ResearchKinds.Correlations, "2026-11-01", "2026-11-14")]
    public void Month_in_the_question_becomes_the_research_period(string question, string kind, string from, string to)
    {
        var m = ResearchPlanner.Match(question, new DateOnly(2026, 11, 14));
        Assert.NotNull(m);
        Assert.Equal(kind, m.Kind);
        Assert.Null(m.Days);
        Assert.Equal(DateOnly.Parse(from, System.Globalization.CultureInfo.InvariantCulture), m.From);
        Assert.Equal(DateOnly.Parse(to, System.Globalization.CultureInfo.InvariantCulture), m.To);
    }

    [Fact]
    public void Planner_marks_unavailable_data_and_respects_rights_before_running()
    {
        var principal = new AnalytikPrincipal(Guid.NewGuid(), new HashSet<string> { "sales.read" }, SalesRowScope.Organization);
        var ranges = new Dictionary<string, (DateOnly?, DateOnly?)> { ["sales"] = (new DateOnly(2026, 1, 1), new DateOnly(2026, 10, 31)), ["visits"] = (null, null) };
        var ctx = new ResearchContext(principal, new DateOnly(2026, 11, 14), ranges, []);
        var plan = ResearchPlanner.Build(new ResearchRequest { Kind = ResearchKinds.AgentEfficiency }, ctx);
        var op = Assert.Single(plan.Operations);
        Assert.Equal(ResearchStatuses.DataNotFound, op.Status);
        Assert.Contains(plan.Limitations, l => l.Contains("Визитов в OneBase нет", StringComparison.Ordinal));
        Assert.Equal(new DateOnly(2026, 10, 31), plan.To);

        var health = ResearchPlanner.Build(new ResearchRequest { Kind = ResearchKinds.CompanyHealth }, ctx);
        Assert.DoesNotContain(health.Operations, o => o.Id == "q_pay");
        Assert.Contains(health.Limitations, l => l.Contains("нет права на финансы", StringComparison.Ordinal) || l.Contains("Оплат в OneBase нет", StringComparison.Ordinal));
    }

    [Fact]
    public void Plan_waves_run_independent_operations_together_and_dependent_after()
    {
        var plan = new ResearchPlan
        {
            Kind = ResearchKinds.Correlations, UserQuestion = "q", Objective = "o",
            Operations =
            [
                new ResearchOperation { Id = "a", Type = "query", Title = "a", Source = "s" },
                new ResearchOperation { Id = "b", Type = "query", Title = "b", Source = "s" },
                new ResearchOperation { Id = "c", Type = "compute", Title = "c", Source = "s", DependsOn = ["a", "b"] },
                new ResearchOperation { Id = "d", Type = "compute", Title = "d", Source = "s", DependsOn = ["e"] },
                new ResearchOperation { Id = "e", Type = "compute", Title = "e", Source = "s", DependsOn = ["d"] },
            ],
        };
        var waves = plan.Waves();
        Assert.Equal(["a", "b"], waves[0]);
        Assert.Contains(waves, w => w.Contains("c"));
        Assert.Equal(5, waves.Sum(w => w.Count));
    }

    [Fact]
    public void Evidence_levels_map_from_compute_classes_without_upgrading_correlation()
    {
        Assert.Equal(EvidenceLevels.Correlation, EvidenceLevels.FromClass(EvidenceClasses.Statistical, association: true));
        Assert.Equal(EvidenceLevels.Statistical, EvidenceLevels.FromClass(EvidenceClasses.Statistical));
        Assert.Equal(EvidenceLevels.Statistical, EvidenceLevels.FromClass(EvidenceClasses.TestedCausal, association: true));
        Assert.Equal(EvidenceLevels.Hypothesis, EvidenceLevels.FromClass(EvidenceClasses.Assumption));
        Assert.Equal(EvidenceLevels.Unverified, EvidenceLevels.FromClass(null));
        Assert.Equal(7, EvidenceLevels.All.Count);
    }

    [Fact]
    public void Query_failures_map_to_spec_statuses_and_db_error_is_not_no_data()
    {
        Assert.Equal(ResearchStatuses.NoAccess, ResearchStatuses.OfQuery("denied", "Нет права"));
        Assert.Equal(ResearchStatuses.SourceUnavailable, ResearchStatuses.OfQuery("error", "Срез не выполнен из-за ошибки базы данных"));
        Assert.Equal(ResearchStatuses.SourceUnavailable, ResearchStatuses.OfQuery("timeout", "время"));
        Assert.Equal(ResearchStatuses.ToolUnsupported, ResearchStatuses.OfQuery("invalid", "Показатель не найден"));
        Assert.Equal("SOURCE_UNAVAILABLE", Tools.ToolFailures.Status(Tools.ToolFailures.ToolError));
        Assert.Equal("DATA_NOT_FOUND", Tools.ToolFailures.Status(Tools.ToolFailures.NotFound));
        Assert.Contains("analytics_query", Tools.ToolFailures.Alternatives("get_sales_days"));
    }

    [Fact]
    public void Members_without_any_records_of_a_source_are_named_and_not_counted_as_zero()
    {
        string[] regions = ["Коканд", "Сети", "Урикзор", "Навои"];
        var note = ComputeService.MissingMembersNote("visits_done", regions, [120, null, null, 80]);
        Assert.NotNull(note);
        Assert.Contains("нет данных у 2 из 4 (Сети, Урикзор)", note, StringComparison.Ordinal);
        Assert.Contains("это не ноль", note, StringComparison.Ordinal);
        // Пропусков нет, пропуск у всех (расчёт сам скажет «нет данных») или показатель-отношение (пусто и при нулевом знаменателе) — без оговорки.
        Assert.Null(ComputeService.MissingMembersNote("visits_done", regions, [1, 2, 3, 4]));
        Assert.Null(ComputeService.MissingMembersNote("visits_done", regions, [null, null, null, null]));
        Assert.Null(ComputeService.MissingMembersNote("visit_strike", regions, [50, null, null, 40]));
    }

    [Fact]
    public void Promotions_with_the_same_dates_and_nearly_the_same_products_are_checked_once()
    {
        var oct1 = new DateOnly(2026, 10, 1);
        var oct31 = new DateOnly(2026, 10, 31);
        var ten = Enumerable.Range(1, 10).Select(i => (long)i).ToList();
        var merged = ResearchPlanner.MergeSimilar([
            new PromoInfo(5, "Могул «E»", oct1, oct31, ten.Take(9).ToList()), // 9 из 10 товаров «A» — та же механика
            new PromoInfo(3, "Могул «A»", oct1, oct31, ten),
            new PromoInfo(4, "Могул «B»", oct1, oct31, ten), // те же товары и даты
            new PromoInfo(6, "Могул, ноябрь", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), ten), // другие даты — отдельная проверка
            new PromoInfo(7, "Кекс", oct1, oct31, [1, 2, 11, 12, 13]), // мало общих товаров — отдельная проверка
        ]);

        Assert.Equal(3, merged.Count);
        var mogul = merged.Single(p => p.Begin == oct1 && p.Products.Contains(10));
        Assert.Equal(3, mogul.Id); // голова — больше товаров, затем меньший Id
        Assert.Equal("Могул «A» / Могул «B» и ещё 1", mogul.Name);
        Assert.Equal(ten, mogul.Products);
        Assert.Contains(merged, p => p.Id == 6);
        Assert.Contains(merged, p => p.Id == 7 && p.Name == "Кекс");

        // Начались до периода исследования в разные дни, кончились вместе — окно проверки общее (с первого дня периода): одна проверка.
        var sep13 = new DateOnly(2026, 9, 13);
        var clipped = ResearchPlanner.MergeSimilar([
            new PromoInfo(8, "Могул сентябрь «S»", new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 6), ten),
            new PromoInfo(9, "Могул сентябрь «E»", new DateOnly(2026, 9, 5), new DateOnly(2026, 10, 6), ten),
        ], sep13, new DateOnly(2026, 11, 11));
        Assert.Equal("Могул сентябрь «S» / Могул сентябрь «E»", Assert.Single(clipped).Name);
        Assert.Equal(2, ResearchPlanner.MergeSimilar([
            new PromoInfo(8, "Могул сентябрь «S»", new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 6), ten),
            new PromoInfo(9, "Могул сентябрь «E»", new DateOnly(2026, 9, 5), new DateOnly(2026, 10, 6), ten),
        ]).Count);
    }
}
