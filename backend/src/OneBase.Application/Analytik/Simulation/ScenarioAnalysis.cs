using OneBase.Application.Analytik.Compute;

namespace OneBase.Application.Analytik.Simulation;

/// <summary>Неопределённый параметр для Монте-Карло и случаев: код, минимум, наиболее вероятное, максимум (треугольное распределение).</summary>
public sealed record UncertainParameter(string Code, decimal Min, decimal Mode, decimal Max);

/// <summary>Строка торнадо: параметр, его значения «низко» и «высоко», показатель при них, размах влияния.</summary>
public sealed record SensitivityRow(string Code, string Title, string Unit, decimal Low, decimal High, decimal? MetricLow, decimal? MetricHigh, decimal? Swing);

public sealed record SensitivityResult(string Metric, string MetricTitle, string Unit, decimal? Base, IReadOnlyList<SensitivityRow> Rows);

/// <summary>Матрица чувствительности: показатель на сетке значений двух параметров.</summary>
public sealed record MatrixResult(string Metric, string MetricTitle, string ParamX, string ParamY, IReadOnlyList<decimal> X, IReadOnlyList<decimal> Y,
    IReadOnlyList<IReadOnlyList<decimal?>> Values, decimal? Base);

/// <summary>Подбор параметра (Goal Seek / точка безубыточности): найдено, недостижимо в пределах, параметр не влияет.</summary>
public sealed record GoalSeekResult(string Metric, string Parameter, decimal Target, string Status, decimal? Value, decimal? Achieved, int Iterations, decimal Lo,
    decimal Hi, decimal? MetricAtLo, decimal? MetricAtHi, string Text);

/// <summary>Столбец гистограммы: от, до, сколько прогонов.</summary>
public sealed record HistogramBin(decimal From, decimal To, int Count);

/// <summary>Монте-Карло (Risk-adjusted Forecast): распределение показателя по неопределённым параметрам, квантили, вероятность убытка.</summary>
public sealed record MonteCarloResult(string Metric, string MetricTitle, int Runs, int Valid, ulong Seed, decimal? Base, decimal Mean, decimal P5, decimal P10,
    decimal P50, decimal P90, decimal P95, double ProbBelowZero, double ProbBelowBase, IReadOnlyList<UncertainParameter> Inputs,
    IReadOnlyList<HistogramBin> Histogram);

/// <summary>Случаи «худший / базовый / лучший»: значения параметров и показатель.</summary>
public sealed record CaseRow(string Name, IReadOnlyList<ScenarioParameter> Parameters, decimal? Value);

public sealed record CasesResult(string Metric, string MetricTitle, IReadOnlyList<CaseRow> Cases);

/// <summary>Строка оптимального выпуска: товар, кг, базовый объём за горизонт, маржа на кг.</summary>
public sealed record OptimizePlanRow(string Product, decimal Kg, decimal BaseKg, decimal MarginPerKg);

/// <summary>Оптимальный выпуск: товар, кг, маржа на кг; итог маржи; узкие места (связывающие ограничения и их теневая цена).</summary>
public sealed record OptimizeResult(string Status, decimal Margin, decimal BaseMargin, IReadOnlyList<OptimizePlanRow> Plan,
    IReadOnlyList<LpBinding> Constraints, IReadOnlyList<string> Excluded, IReadOnlyList<string> Notes);

/// <summary>
/// What-If Decision Intelligence поверх ScenarioEngine (чистая функция снимка и параметров): чувствительность (торнадо), матрица двух параметров,
/// подбор параметра под цель и точка безубыточности (бисекция), Монте-Карло с треугольными распределениями (воспроизводимо по seed), случаи
/// «худший / базовый / лучший», оптимальный выпуск по марже при ограничениях сырья и мощности (симплекс). Исходный сценарий не меняется:
/// каждый расчёт — новый запуск движка на том же снимке.
/// </summary>
public static class ScenarioAnalysis
{
    public const int MaxRuns = 2000;
    public const int MaxGrid = 9;

    public static decimal? Value(SimulationResult r, string metric) => r.Valid ? r.Metric(metric)?.Scenario : null;

    /// <summary>Значение параметра в сценарии (без цели) или значение по умолчанию каталога.</summary>
    public static decimal Current(ScenarioDefinition def, string code) =>
        def.Parameters.FirstOrDefault(p => p.Code == code && p.Target is null)?.Value ?? ScenarioCatalog.Find(code)?.Default ?? 0;

    /// <summary>Тот же сценарий с другим значением параметра (без цели).</summary>
    public static ScenarioDefinition With(ScenarioDefinition def, string code, decimal value) =>
        def with { Parameters = [.. def.Parameters.Where(p => !(p.Code == code && p.Target is null)), new ScenarioParameter(code, value)] };

    private static ParameterDefinition Param(string code) =>
        ScenarioCatalog.Find(code) ?? throw new ComputeException($"Параметра «{code}» нет в каталоге симулятора.");

    private static string MetricTitle(SimulationResult r, string metric) => r.Metric(metric)?.Title ?? metric;

    /// <summary>Торнадо: каждый параметр ± шаг (изменение — ±10 % или ±2 п.п., дни — ±7, значение — ±10 %), показатель при обоих, размах.</summary>
    public static SensitivityResult Sensitivity(ScenarioBaselineData b, ScenarioDefinition def, string metric, IReadOnlyList<string> codes, decimal? step = null)
    {
        var baseRun = ScenarioEngine.Run(b, def);
        RequireMetric(baseRun, metric);
        var rows = new List<SensitivityRow>();
        foreach (var code in codes.Distinct())
        {
            var p = Param(code);
            var cur = Current(def, code);
            var d = step ?? DefaultStep(p, cur);
            var low = Math.Clamp(cur - d, p.Min, p.Max);
            var high = Math.Clamp(cur + d, p.Min, p.Max);
            var ml = Value(ScenarioEngine.Run(b, With(def, code, low)), metric);
            var mh = Value(ScenarioEngine.Run(b, With(def, code, high)), metric);
            rows.Add(new SensitivityRow(code, p.Title, p.Unit, low, high, ml, mh, ml is { } x && mh is { } y ? Math.Abs(y - x) : null));
        }

        return new SensitivityResult(metric, MetricTitle(baseRun, metric), baseRun.Metric(metric)?.Unit ?? "", Value(baseRun, metric),
            rows.OrderByDescending(r => r.Swing ?? -1).ToList());
    }

    private static decimal DefaultStep(ParameterDefinition p, decimal current) => p.Mode == ParameterDefinition.Change
        ? p.Unit switch { "п.п." => 2, "дн." => 7, _ => 10 }
        : Math.Max(p.Step, Math.Abs(current) * 0.1m);

    /// <summary>Матрица: показатель на сетке значений двух параметров (до 9 × 9).</summary>
    public static MatrixResult Matrix(ScenarioBaselineData b, ScenarioDefinition def, string metric, string paramX, IReadOnlyList<decimal> xs, string paramY,
        IReadOnlyList<decimal> ys)
    {
        StatMath.Require(xs.Count is > 0 and <= MaxGrid && ys.Count is > 0 and <= MaxGrid, $"Сетка матрицы — от 1 до {MaxGrid} значений по каждой оси.");
        var px = Param(paramX);
        var py = Param(paramY);
        var baseRun = ScenarioEngine.Run(b, def);
        RequireMetric(baseRun, metric);
        var values = ys.Select(y => (IReadOnlyList<decimal?>)xs.Select(x =>
            Value(ScenarioEngine.Run(b, With(With(def, paramX, Math.Clamp(x, px.Min, px.Max)), paramY, Math.Clamp(y, py.Min, py.Max))), metric)).ToList()).ToList();
        return new MatrixResult(metric, MetricTitle(baseRun, metric), paramX, paramY, xs, ys, values, Value(baseRun, metric));
    }

    /// <summary>
    /// Подбор значения параметра, при котором показатель равен цели (бисекция в пределах параметра или заданных). Показатель на концах по одну
    /// сторону от цели — «недостижимо в пределах»; не меняется — «параметр не влияет». Найденное значение — с шагом каталога.
    /// </summary>
    public static GoalSeekResult GoalSeek(ScenarioBaselineData b, ScenarioDefinition def, string metric, decimal target, string code, decimal? lo = null,
        decimal? hi = null)
    {
        var p = Param(code);
        var a = Math.Max(lo ?? p.Min, p.Min);
        var z = Math.Min(hi ?? p.Max, p.Max);
        StatMath.Require(a < z, "Нижняя граница поиска должна быть меньше верхней.");
        decimal? F(decimal v) => Value(ScenarioEngine.Run(b, With(def, code, v)), metric) is { } m ? m - target : null;
        var fa = F(a);
        var fz = F(z);
        var title = ScenarioEngine.Run(b, def).Metric(metric)?.Title ?? metric;
        if (fa is null || fz is null)
        {
            return new GoalSeekResult(metric, code, target, "invalid", null, null, 0, a, z, fa + target, fz + target,
                $"«{title}» не считается при значениях параметра на границах — не хватает исходных данных (см. ограничения сценария).");
        }

        if (fa == fz)
        {
            return new GoalSeekResult(metric, code, target, "no_effect", null, null, 0, a, z, fa + target, fz + target,
                $"«{p.Title}» не влияет на «{title}» в этом сценарии.");
        }

        if (Math.Sign(fa.Value) == Math.Sign(fz.Value) && fa != 0 && fz != 0)
        {
            return new GoalSeekResult(metric, code, target, "unreachable", null, null, 0, a, z, fa + target, fz + target,
                $"Цель недостижима при «{p.Title}» от {a} до {z} {p.Unit}: «{title}» от {fa + target:N0} до {fz + target:N0}.");
        }

        var tol = Math.Max(1m, Math.Abs(target) * 0.0001m);
        var iterations = 0;
        decimal mid = a, fm = fa.Value;
        while (iterations++ < 60 && z - a > p.Step / 10)
        {
            mid = (a + z) / 2;
            fm = F(mid) ?? 0;
            if (Math.Abs(fm) <= tol)
            {
                break;
            }

            if (Math.Sign(fm) == Math.Sign(fa.Value))
            {
                a = mid;
                fa = fm;
            }
            else
            {
                z = mid;
            }
        }

        var rounded = p.Step > 0 ? Math.Round(mid / p.Step) * p.Step : mid;
        var achieved = Value(ScenarioEngine.Run(b, With(def, code, rounded)), metric);
        return new GoalSeekResult(metric, code, target, "found", rounded, achieved, iterations, lo ?? p.Min, hi ?? p.Max, null, null,
            $"«{title}» = {target:N0} при «{p.Title}» = {rounded} {p.Unit} (показатель {achieved:N0}).");
    }

    /// <summary>Точка безубыточности: значение параметра, при котором прибыль (операционная или валовая) равна нулю.</summary>
    public static GoalSeekResult BreakEven(ScenarioBaselineData b, ScenarioDefinition def, string code, string metric = "finance.operating_profit") =>
        GoalSeek(b, def, metric, 0, code);

    /// <summary>Монте-Карло: параметры — треугольные распределения, генератор SplitMix64 по seed (тот же seed — те же числа), до MaxRuns прогонов.</summary>
    public static MonteCarloResult MonteCarlo(ScenarioBaselineData b, ScenarioDefinition def, string metric, IReadOnlyList<UncertainParameter> inputs, int runs = 300,
        ulong seed = 42)
    {
        StatMath.Require(inputs.Count > 0, "Нужен хотя бы один неопределённый параметр (min, наиболее вероятное, max).");
        StatMath.Require(inputs.All(i => i.Min <= i.Mode && i.Mode <= i.Max), "У параметра должно быть min ≤ наиболее вероятное ≤ max.");
        runs = Math.Clamp(runs, 20, MaxRuns);
        var baseRun = ScenarioEngine.Run(b, def);
        RequireMetric(baseRun, metric);
        var rng = new SplitMix64(seed);
        var values = new List<double>();
        for (var i = 0; i < runs; i++)
        {
            var d = def;
            foreach (var u in inputs)
            {
                var p = Param(u.Code);
                d = With(d, u.Code, Math.Clamp((decimal)rng.NextTriangular((double)u.Min, (double)u.Mode, (double)u.Max), p.Min, p.Max));
            }

            if (Value(ScenarioEngine.Run(b, d), metric) is { } v)
            {
                values.Add((double)v);
            }
        }

        StatMath.Require(values.Count >= 10, "Показатель не считается в большинстве прогонов — не хватает исходных данных.");
        var baseValue = Value(baseRun, metric);
        decimal Q(double q) => (decimal)StatMath.Quantile(values, q);
        var min = values.Min();
        var max = values.Max();
        var width = (max - min) / 10;
        var histogram = width <= 0
            ? new List<HistogramBin> { new((decimal)min, (decimal)max, values.Count) }
            : Enumerable.Range(0, 10).Select(k => new HistogramBin((decimal)(min + k * width), (decimal)(min + (k + 1) * width),
                values.Count(v => v >= min + k * width && (k == 9 ? v <= max : v < min + (k + 1) * width)))).ToList();
        return new MonteCarloResult(metric, MetricTitle(baseRun, metric), runs, values.Count, seed, baseValue, (decimal)values.Average(), Q(0.05), Q(0.1), Q(0.5),
            Q(0.9), Q(0.95), values.Count(v => v < 0) / (double)values.Count, baseValue is { } bv ? values.Count(v => v < (double)bv) / (double)values.Count : 0,
            inputs, histogram);
    }

    /// <summary>
    /// Худший / базовый / лучший: каждый неопределённый параметр — на той границе, которая сама по себе хуже (лучше) для показателя; базовый —
    /// наиболее вероятные значения. «Лучше» — больше показатель (для затрат и дефицита задайте показатель прибыли или выручки).
    /// </summary>
    public static CasesResult Cases(ScenarioBaselineData b, ScenarioDefinition def, string metric, IReadOnlyList<UncertainParameter> inputs)
    {
        StatMath.Require(inputs.Count > 0, "Нужен хотя бы один неопределённый параметр.");
        var baseDef = inputs.Aggregate(def, (d, u) => With(d, u.Code, u.Mode));
        var baseRun = ScenarioEngine.Run(b, baseDef);
        RequireMetric(baseRun, metric);
        var worst = baseDef;
        var best = baseDef;
        foreach (var u in inputs)
        {
            var atMin = Value(ScenarioEngine.Run(b, With(baseDef, u.Code, u.Min)), metric) ?? 0;
            var atMax = Value(ScenarioEngine.Run(b, With(baseDef, u.Code, u.Max)), metric) ?? 0;
            worst = With(worst, u.Code, atMin <= atMax ? u.Min : u.Max);
            best = With(best, u.Code, atMin <= atMax ? u.Max : u.Min);
        }

        CaseRow Row(string name, ScenarioDefinition d) =>
            new(name, d.Parameters.Where(p => inputs.Any(u => u.Code == p.Code) && p.Target is null).ToList(), Value(ScenarioEngine.Run(b, d), metric));
        return new CasesResult(metric, MetricTitle(baseRun, metric), [Row("worst", worst), Row("base", baseDef), Row("best", best)]);
    }

    /// <summary>
    /// Оптимальный выпуск по марже: переменные — товары с техкартой и нормами, цель — Σ (цена кг − сырьё кг по нормам) × кг; ограничения — доступное
    /// сырьё с поставками за горизонт, мощность за горизонт (если известна), спрос — не больше базового темпа × (1 + рост). Товары без техкарты
    /// или без цены сырья не оптимизируются — список с причиной.
    /// </summary>
    public static OptimizeResult Optimize(ScenarioBaselineData b, int horizonDays, decimal demandGrowthPercent = 0, decimal? capacityKgDay = null)
    {
        StatMath.Require(b.Sales is { Products.Count: > 0 }, "В снимке нет продаж по товарам — оптимизировать нечего.");
        StatMath.Require(b.Materials is { Count: > 0 }, "В снимке нет склада сырья — ограничений по сырью не построить.");
        var materials = b.Materials!.ToDictionary(m => m.ItemId);
        var excluded = new List<string>();
        var products = new List<(BaselineProduct P, decimal Price, decimal Cost, decimal Cap, decimal BaseKg)>();
        foreach (var p in b.Sales!.Products.Where(p => p.Kg > 0))
        {
            if (p.Norms.Count == 0)
            {
                excluded.Add($"{p.Name}: {p.RecipeProblem ?? "нет техкарты"}");
                continue;
            }

            var unpriced = p.Norms.FirstOrDefault(n => !materials.TryGetValue(n.ItemId, out var m) || m.Price is null);
            if (unpriced is not null)
            {
                excluded.Add($"{p.Name}: нет цены сырья {(materials.TryGetValue(unpriced.ItemId, out var mm) ? mm.Name : unpriced.ItemId.ToString())}");
                continue;
            }

            var cost = p.Norms.Sum(n => n.PerKg * materials[n.ItemId].Price!.Value);
            var baseKg = p.Kg / b.Days * horizonDays;
            products.Add((p, p.Revenue / p.Kg, cost, baseKg * (1 + demandGrowthPercent / 100), baseKg));
        }

        StatMath.Require(products.Count > 0, "Ни у одного проданного товара нет техкарты с ценами сырья — оптимальный выпуск не считается. " +
                                             string.Join("; ", excluded.Take(5)));
        var vars = products.Select(x => new LpVariable(x.P.Name, (double)(x.Price - x.Cost), 0, (double)x.Cap)).ToList();
        var constraints = new List<LpConstraint>();
        foreach (var itemId in products.SelectMany(x => x.P.Norms.Select(n => n.ItemId)).Distinct())
        {
            var m = materials[itemId];
            var limit = m.Available + m.Incoming.Where(i => i.Expected is null || i.Expected <= b.AsOf.AddDays(horizonDays)).Sum(i => i.Quantity);
            constraints.Add(new LpConstraint($"Сырьё: {m.Name}, {m.Unit}",
                products.Select(x => (double)(x.P.Norms.FirstOrDefault(n => n.ItemId == itemId)?.PerKg ?? 0)).ToList(), (double)Math.Max(0, limit)));
        }

        var capacity = capacityKgDay ?? b.Production?.CapacityKgDay;
        var notes = new List<string>();
        if (capacity is { } c)
        {
            constraints.Add(new LpConstraint("Мощность производства, кг", products.Select(_ => 1d).ToList(), (double)(c * horizonDays)));
        }
        else
        {
            notes.Add("Мощность неизвестна (нет утверждённых смен и не введена) — ограничение мощности не учтено.");
        }

        var lp = LinearProgram.Maximize(vars, constraints);
        var plan = lp.Status == "optimal"
            ? lp.Values.Select((v, i) => new OptimizePlanRow(products[i].P.Name, (decimal)v.Value, products[i].BaseKg, products[i].Price - products[i].Cost))
                .OrderByDescending(x => x.Kg).ToList()
            : [];
        var baseMargin = products.Sum(x => Math.Min(x.BaseKg, x.Cap) * (x.Price - x.Cost));
        notes.Add("Маржа — цена кг минус сырьё по нормам техкарт; прочая себестоимость, зарплаты и расходы не учтены.");
        return new OptimizeResult(lp.Status, (decimal)lp.Objective, baseMargin, plan, lp.Constraints.Where(x => x.Binding).ToList(), excluded, notes);
    }

    private static void RequireMetric(SimulationResult r, string metric)
    {
        StatMath.Require(r.Valid, "Базовый сценарий не считается: " + string.Join(" ", r.Issues.Where(i => i.Severity == ScenarioIssue.Error).Select(i => i.Text)));
        StatMath.Require(r.Metric(metric) is not null, $"Показателя «{metric}» нет в результате сценария. Есть: {string.Join(", ", r.Metrics.Select(m => m.Code))}.");
        StatMath.Require(r.Metric(metric)!.Scenario is not null,
            $"«{r.Metric(metric)!.Title}» не считается на этих данных: {r.Metric(metric)!.Note ?? "не хватает исходных данных"}.");
    }
}
