using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using OneBase.Application.Analytik;
using OneBase.Application.Analytik.Compute;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Simulation;
using OneBase.Domain.Analytik;

namespace OneBase.AI.Analytik.Simulation;

/// <summary>
/// Запрос анализа сценария: снимок (id) или период базы, сценарий (вид, горизонт, параметры), режим (sensitivity, matrix, goal_seek, break_even,
/// monte_carlo, cases, optimize), показатель и параметры режима.
/// </summary>
public sealed record AnalyzeInput(
    Guid? BaselineId, DateOnly? From, DateOnly? To, string? Kind, int? HorizonDays, IReadOnlyList<ScenarioParameter>? Parameters, string? NewRegionName,
    string Mode, string? Metric, IReadOnlyList<string>? Codes = null, string? ParamX = null, IReadOnlyList<decimal>? X = null, string? ParamY = null,
    IReadOnlyList<decimal>? Y = null, decimal? Target = null, string? Param = null, decimal? Lo = null, decimal? Hi = null,
    IReadOnlyList<UncertainParameter>? Uncertain = null, int? Runs = null, ulong? Seed = null, decimal? DemandGrowth = null, decimal? CapacityKgDay = null);

/// <summary>Итог анализа: запись журнала расчётов, режим, снимок, сценарий, результат, короткий вывод кодом.</summary>
public sealed record AnalysisView(Guid Id, string Mode, Guid BaselineId, DateOnly From, DateOnly To, DateOnly AsOf, ScenarioDefinition Scenario, object Result,
    string Summary, string FormulaVersion);

/// <summary>
/// What-If Decision Intelligence: анализ сценария на снимке исходных данных (ScenarioAnalysis — многократный запуск ScenarioEngine), в общей
/// очереди расчётов симулятора. Каждый анализ — в журнале analytik.Computations (метод scenario.&lt;режим&gt;, версия формул, снимок и сценарий —
/// вход): исходный сценарий и все изменения параметров сохраняются, прогноз сценария не смешивается с фактом снимка.
/// </summary>
public sealed class ScenarioAnalysisService(ScenarioService scenarios, IAnalytikDb adb)
{
    public static readonly string[] Modes = ["sensitivity", "matrix", "goal_seek", "break_even", "monte_carlo", "cases", "optimize"];

    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    public async Task<AnalysisView> AnalyzeAsync(AnalytikPrincipal p, AnalyzeInput input, string origin, CancellationToken ct, Guid? investigationId = null)
    {
        var mode = input.Mode.Trim().ToLowerInvariant();
        StatMath.Require(Modes.Contains(mode), $"Режим анализа: {string.Join(", ", Modes)}.");
        var watch = Stopwatch.StartNew();
        var (baselineId, data) = await scenarios.BaselineDataAsync(p, input.BaselineId, input.From, input.To, ct);
        var def = new ScenarioDefinition(input.Kind ?? ScenarioKinds.Combined, Math.Clamp(input.HorizonDays ?? 30, ScenarioCatalog.MinHorizonDays, ScenarioCatalog.MaxHorizonDays),
            input.Parameters ?? [], input.NewRegionName);
        var metric = input.Metric ?? (mode == "break_even" ? "finance.operating_profit" : "finance.gross_profit");
        var (result, summary) = await scenarios.GuardAsync(() => Run(mode, data, def, metric, input), ct);
        var row = new AnalytikComputation
        {
            UserId = p.UserId, Origin = origin, InvestigationId = investigationId, Method = "scenario." + mode, Domain = "whatif",
            Algorithm = Algorithm(mode), Version = ScenarioEngine.FormulaVersion,
            Parameters = JsonSerializer.Serialize(input, ComputeService.JsonOptions),
            Input = JsonSerializer.Serialize(new { baselineId, data.From, data.To, data.AsOf, scenario = def }, ComputeService.JsonOptions),
            Result = JsonSerializer.Serialize(result, ComputeService.JsonOptions), PeriodFrom = data.From, PeriodTo = data.To, Status = "ok",
            Sources = JsonSerializer.Serialize(data.Sources, ComputeService.JsonOptions),
            Limitations = JsonSerializer.Serialize(new[] { "Сценарий — расчёт по снимку данных, а не прогноз факта; допущения — в результатах сценария." }, ComputeService.JsonOptions),
            EvidenceClass = EvidenceClasses.Assumption, ElapsedMs = watch.ElapsedMilliseconds,
        };
        adb.Computations.Add(row);
        await adb.SaveChangesAsync(ct);
        return new AnalysisView(row.Id, mode, baselineId, data.From, data.To, data.AsOf, def, result, summary, ScenarioEngine.FormulaVersion);
    }

    private static (object Result, string Summary) Run(string mode, ScenarioBaselineData data, ScenarioDefinition def, string metric, AnalyzeInput input)
    {
        switch (mode)
        {
            case "sensitivity":
            {
                var codes = input.Codes is { Count: > 0 } c ? c : Default(def);
                var r = ScenarioAnalysis.Sensitivity(data, def, metric, codes);
                var top = r.Rows.Where(x => x.Swing is > 0).Take(3).Select(x => $"{x.Title} ({N(x.MetricLow)} … {N(x.MetricHigh)})");
                return (r, $"«{r.MetricTitle}» (база {N(r.Base)}): сильнее всего влияют — {string.Join("; ", top)}.");
            }

            case "matrix":
            {
                StatMath.Require(input.ParamX is not null && input.ParamY is not null && input.X is { Count: > 0 } && input.Y is { Count: > 0 },
                    "Для матрицы нужны param_x, x, param_y, y.");
                var r = ScenarioAnalysis.Matrix(data, def, metric, input.ParamX!, input.X!, input.ParamY!, input.Y!);
                var flat = r.Values.SelectMany(v => v).OfType<decimal>().ToList();
                return (r, $"«{r.MetricTitle}» на сетке {r.X.Count} × {r.Y.Count}: от {N(flat.DefaultIfEmpty().Min())} до {N(flat.DefaultIfEmpty().Max())}.");
            }

            case "goal_seek":
            {
                StatMath.Require(input.Param is not null && input.Target is not null, "Для подбора нужны param и target.");
                var r = ScenarioAnalysis.GoalSeek(data, def, metric, input.Target!.Value, input.Param!, input.Lo, input.Hi);
                return (r, r.Text);
            }

            case "break_even":
            {
                StatMath.Require(input.Param is not null, "Для точки безубыточности нужен param — какой параметр меняется (например, sales.demand).");
                var r = ScenarioAnalysis.GoalSeek(data, def, metric, 0, input.Param!, input.Lo, input.Hi);
                return (r, "Точка безубыточности: " + r.Text);
            }

            case "monte_carlo":
            {
                var r = ScenarioAnalysis.MonteCarlo(data, def, metric, input.Uncertain ?? [], input.Runs ?? 300, input.Seed ?? 42);
                return (r, $"«{r.MetricTitle}» с учётом неопределённости ({r.Valid} прогонов): медиана {N(r.P50)}, 80 % — от {N(r.P10)} до {N(r.P90)}; " +
                           $"вероятность ниже нуля {r.ProbBelowZero:P0}, ниже базы {r.ProbBelowBase:P0}.");
            }

            case "cases":
            {
                var r = ScenarioAnalysis.Cases(data, def, metric, input.Uncertain ?? []);
                return (r, $"«{r.MetricTitle}»: худший {N(r.Cases[0].Value)}, базовый {N(r.Cases[1].Value)}, лучший {N(r.Cases[2].Value)}.");
            }

            default:
            {
                var r = ScenarioAnalysis.Optimize(data, def.HorizonDays, input.DemandGrowth ?? 0, input.CapacityKgDay);
                return (r, r.Status == "optimal"
                    ? $"Оптимальный выпуск даёт маржу по сырью {N(r.Margin)} против {N(r.BaseMargin)} при текущей структуре; узкие места: " +
                      $"{(r.Constraints.Count == 0 ? "нет" : string.Join(", ", r.Constraints.Select(c => $"{c.Name} (+1 ед. даёт {N((decimal)c.ShadowPrice)})")))}."
                    : $"Оптимизация: {r.Status}.");
            }
        }
    }

    /// <summary>Параметры торнадо по умолчанию: спрос, цена, скидки, цена сырья, нормы, брак, мощность, срок оплаты покупателей.</summary>
    private static IReadOnlyList<string> Default(ScenarioDefinition def) =>
        new[] { "sales.demand", "sales.price", "sales.discount", "materials.price", "production.norms", "production.defect", "production.capacity", "finance.customer_terms" }
            .Concat(def.Parameters.Where(p => p.Target is null).Select(p => p.Code)).Distinct().ToList();

    private static string Algorithm(string mode) => mode switch
    {
        "sensitivity" => "Торнадо: каждый параметр ± шаг, остальные — как в сценарии; размах показателя",
        "matrix" => "Показатель на сетке значений двух параметров",
        "goal_seek" => "Бисекция по параметру до значения показателя, равного цели (в пределах параметра)",
        "break_even" => "Бисекция по параметру до нулевой прибыли",
        "monte_carlo" => "Монте-Карло: треугольные распределения параметров, генератор SplitMix64 по seed, квантили показателя",
        "cases" => "Худший / базовый / лучший: параметры на худших / наиболее вероятных / лучших значениях",
        _ => "Симплекс: максимум маржи по сырью при ограничениях сырья, мощности и спроса",
    };

    private static string N(decimal? v) => v is { } x ? x.ToString("N0", Ru) : "—";
}

/// <summary>
/// analytik_scenario_analysis — анализ сценария What-If: чувствительность (торнадо), матрица двух параметров, подбор параметра под цель, точка
/// безубыточности, Монте-Карло с вероятностью убытка, худший / базовый / лучший случаи, оптимальный выпуск по марже при ограничениях сырья и
/// мощности. Сценарий — те же параметры, что у analytik_simulate. Считает движок (код), каждый анализ — в журнале расчётов.
/// </summary>
public sealed class ScenarioAnalysisTool(Application.Analytik.Query.QueryEngine engine, ScenarioAnalysisService analysis) : AnalytikTool(engine)
{
    public const string ToolName = "analytik_scenario_analysis";

    public override string Name => ToolName;
    public override string Title => "Анализ сценария (чувствительность, безубыточность, риск)";
    public override int Level => Tools.ToolLevels.Analyze;

    public override string Description =>
        "Анализ сценария What-If движком симулятора: sensitivity — какие параметры сильнее всего влияют на показатель (торнадо); matrix — показатель " +
        "на сетке двух параметров; goal_seek — при каком значении параметра показатель достигнет цели; break_even — при каком значении параметра " +
        "прибыль станет нулевой; monte_carlo — распределение показателя при неопределённых параметрах (min / наиболее вероятное / max), вероятность " +
        "убытка; cases — худший, базовый и лучший случаи; optimize — оптимальный выпуск товаров по марже при ограничениях сырья и мощности (узкие " +
        "места и их теневые цены). Показатели: sales.revenue, sales.kg, finance.gross_profit, finance.operating_profit, finance.net_cash, " +
        "finance.min_cash, production.output, procurement.budget и др. (коды из результата analytik_simulate). Числа — только из результата.";

    public override System.Text.Json.JsonElement InputSchema { get; } = Schema(
        ("mode", new { type = "string", @enum = ScenarioAnalysisService.Modes }),
        ("metric", new { type = "string", description = "Показатель сценария (по умолчанию finance.gross_profit; для break_even — finance.operating_profit)." }),
        ("param", new { type = "string", description = "Параметр для goal_seek / break_even: код каталога (sales.demand, sales.price, materials.price…)." }),
        ("target", new { type = "number", description = "Цель показателя для goal_seek." }),
        ("lo", new { type = "number", description = "Нижняя граница поиска параметра." }),
        ("hi", new { type = "number", description = "Верхняя граница поиска параметра." }),
        ("codes", new { type = "array", items = new { type = "string" }, description = "Параметры торнадо (по умолчанию — основные)." }),
        ("param_x", new { type = "string" }), ("x", new { type = "array", items = new { type = "number" } }),
        ("param_y", new { type = "string" }), ("y", new { type = "array", items = new { type = "number" } }),
        ("uncertain", new
        {
            type = "array",
            description = "Неопределённые параметры для monte_carlo и cases: {code, min, mode, max}.",
            items = new { type = "object", properties = new { code = new { type = "string" }, min = new { type = "number" }, mode = new { type = "number" }, max = new { type = "number" } } },
        }),
        ("runs", new { type = "integer", description = "Прогонов Монте-Карло (20–2000, по умолчанию 300)." }),
        ("seed", new { type = "integer", description = "Seed генератора (тот же seed — те же числа)." }),
        ("demand_growth_percent", new { type = "number", description = "optimize: на сколько % спрос товара может превысить базовый темп." }),
        ("capacity_kg_day", new { type = "number", description = "optimize: мощность, кг/сут (если её нет в OneBase и пользователь её назвал)." }),
        ("scenario", new
        {
            type = "array",
            description = "Изменения сценария, от которых считается анализ: {code, value} (как parameters у analytik_simulate, без целей).",
            items = new { type = "object", properties = new { code = new { type = "string" }, value = new { type = "number" } }, required = new[] { "code", "value" } },
        }),
        ("horizon_days", new { type = "integer", description = "Горизонт, дней (7–365, по умолчанию 30)." }),
        ("base_from", new { type = "string", description = "Начало базового периода YYYY-MM-DD." }),
        ("base_to", new { type = "string", description = "Конец базового периода YYYY-MM-DD." }));

    protected override async Task<Tools.ToolResult> RunAsync(Tools.ToolContext context, Tools.Data.ToolArgs args, CancellationToken ct)
    {
        var principal = await PrincipalAsync(context, ct);
        static IReadOnlyList<decimal>? Numbers(JsonElement? e) =>
            e is { ValueKind: JsonValueKind.Array } a ? a.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Number).Select(x => x.GetDecimal()).ToList() : null;
        static DateOnly? Date(string? s) => s is { Length: > 0 } && DateOnly.TryParse(s, CultureInfo.InvariantCulture, out var d) ? d : null;
        var scenario = args.Element("scenario") is { ValueKind: JsonValueKind.Array } sc
            ? sc.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object && x.TryGetProperty("code", out _) && x.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.Number)
                .Select(x => new ScenarioParameter(x.GetProperty("code").GetString() ?? "", x.GetProperty("value").GetDecimal())).Where(x => ScenarioCatalog.Find(x.Code) is not null).ToList()
            : [];
        var uncertain = args.Element("uncertain") is { ValueKind: JsonValueKind.Array } un
            ? un.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object).Select(x => new UncertainParameter(
                x.TryGetProperty("code", out var c) ? c.GetString() ?? "" : "", Dec(x, "min"), Dec(x, "mode"), Dec(x, "max"))).ToList()
            : null;
        var input = new AnalyzeInput(null, Date(args.Str("base_from")), Date(args.Str("base_to")), ScenarioKinds.Combined, args.Int("horizon_days", 7, 365), scenario, null,
            args.Str("mode") ?? "sensitivity", args.Str("metric"),
            args.Element("codes") is { ValueKind: JsonValueKind.Array } cs ? cs.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToList() : null,
            args.Str("param_x"), Numbers(args.Element("x")), args.Str("param_y"), Numbers(args.Element("y")), args.Dec("target", decimal.MinValue, decimal.MaxValue),
            args.Str("param"), args.Dec("lo", decimal.MinValue, decimal.MaxValue), args.Dec("hi", decimal.MinValue, decimal.MaxValue), uncertain, args.Int("runs", 20, 2000),
            args.Int("seed", 0, int.MaxValue) is { } seed ? (ulong)seed : null, args.Dec("demand_growth_percent", -90, 500), args.Dec("capacity_kg_day", 0, 100_000_000));
        try
        {
            var r = await analysis.AnalyzeAsync(principal, input, context.Depth > 0 ? "investigation" : "chat", ct);
            return Data(new
            {
                ComputationId = r.Id, r.Mode, Summary = r.Summary, BasePeriod = $"{r.From:dd.MM.yyyy} — {r.To:dd.MM.yyyy}", DataAsOf = r.AsOf.ToString("dd.MM.yyyy"),
                r.Scenario, Result = r.Result, r.FormulaVersion,
                Note = "Это расчёт сценария по снимку данных (прогноз при допущениях), а не факт.",
            }, new OneBase.AI.Consultant.DataSource("OneBase → Аналитик → Симулятор (анализ сценария)", $"{r.From:dd.MM.yyyy} — {r.To:dd.MM.yyyy}", "/analytik/simulator"));
        }
        catch (ComputeException ex)
        {
            return Tools.ToolResult.Failure(Name, Tools.ToolFailures.InvalidArguments, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Tools.ToolResult.Failure(Name, Tools.ToolFailures.Forbidden, ex.Message);
        }
    }

    private static decimal Dec(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : 0;
}
