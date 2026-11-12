using OneBase.Application.Analytik.Supply;

namespace OneBase.Application.Analytik.Simulation;

/// <summary>Параметры сценария для расчёта: общие значения, точечные (по цели), выборы-цели (регион-образец, подразделение), исходные данные.</summary>
internal sealed class ScenarioParams
{
    private readonly Dictionary<string, decimal> _global = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Code, string Target), decimal> _targeted = new();
    private readonly Dictionary<string, string> _choice = new(StringComparer.Ordinal);
    private readonly List<ScenarioParameter> _all = [];

    public static readonly ScenarioParams Empty = new();

    public IReadOnlyList<ScenarioParameter> All => _all;

    /// <summary>Параметры сценария; inputsOnly — только исходные данные (input.*): база считается с ними, но без изменений.</summary>
    public static ScenarioParams Of(IEnumerable<ScenarioParameter> parameters, bool inputsOnly = false)
    {
        var p = new ScenarioParams();
        foreach (var x in parameters)
        {
            var d = ScenarioCatalog.Find(x.Code);
            if (d is null || (inputsOnly && d.Mode != ParameterDefinition.Input))
            {
                continue;
            }

            p._all.Add(x);
            if (d.TargetKind is null || (x.Target is null && d.Code == "workforce.headcount"))
            {
                p._global[x.Code] = x.Value;
            }
            else if (x.Target is not null)
            {
                p._targeted[(x.Code, x.Target)] = x.Value;
                p._choice.TryAdd(x.Code, x.Target);
            }
        }

        return p;
    }

    public decimal Value(string code) => _global.GetValueOrDefault(code);

    public decimal Pct(string code) => Value(code) / 100m;

    public bool Has(string code) => _global.ContainsKey(code);

    /// <summary>Введённое исходное значение (input.*) — null, если не введено или не положительное там, где нужен смысл «есть значение».</summary>
    public decimal? Input(string code) => _global.TryGetValue(code, out var v) ? v : null;

    public decimal TargetValue(string code, string target) => _targeted.GetValueOrDefault((code, target));

    public decimal TargetPct(string code, string target) => TargetValue(code, target) / 100m;

    public bool HasTarget(string code, string target) => _targeted.ContainsKey((code, target));

    /// <summary>Выбор-цель параметра (регион-образец, производственное подразделение, поставщик) — первая указанная цель.</summary>
    public string? Choice(string code) => _choice.GetValueOrDefault(code);

    public IEnumerable<(string Target, decimal Value)> Targets(string code) =>
        _targeted.Where(kv => kv.Key.Code == code).Select(kv => (kv.Key.Target, kv.Value)).OrderBy(x => x.Target, StringComparer.Ordinal);
}

internal sealed class ProductState
{
    public required BaselineProduct Product { get; init; }
    public bool HasRecipe => Product.Norms.Count > 0;
    public decimal BaseKg { get; set; }
    public decimal BaseRevenue { get; set; }
    public decimal SalesReqKg { get; set; }
    public decimal Potential { get; set; }
    public decimal ReqProdKg { get; set; }
    public decimal PlannedKg { get; set; }
    public decimal ProducedKg { get; set; }
    public decimal SoldRatio { get; set; } = 1;
    public decimal SoldKg { get; set; }
    public decimal Revenue { get; set; }
    public decimal Coverage { get; set; } = 1;
    public string? Limit { get; set; }
    public decimal? CostPerKg { get; set; }
}

internal sealed class RegionState
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public bool New { get; init; }
    public decimal BaseKg { get; set; }
    public decimal BaseRevenue { get; set; }
    public decimal ReqKg { get; set; }
    public decimal SoldKg { get; set; }
    public decimal Revenue { get; set; }
    public decimal? PlanKg { get; set; }
}

/// <summary>Ячейка расчёта продаж: товар × регион — запрос (кг) и выручка при полном обеспечении.</summary>
internal sealed record CellState(long ProductId, string RegionId, decimal ReqKg, decimal Potential);

internal sealed class MaterialState
{
    public required BaselineMaterial Item { get; init; }
    public decimal RecipeDaily { get; set; }
    public decimal Daily { get; set; }
    public decimal Available { get; set; }
    public decimal IncomingH { get; set; }
    public decimal RawPurchase { get; set; }
    public decimal Purchase { get; set; }
    public decimal LotSurplus { get; set; }
    public decimal? Price { get; set; }
    public string? Supplier { get; set; }
    public int LeadTime { get; set; }
    public int ArrivalDay { get; set; }
    public DateOnly? Shortage { get; set; }
    public decimal LostDays { get; set; }
    public DateOnly? OrderBy { get; set; }
    public bool OrderLate { get; set; }
    public decimal EndStock { get; set; }
    public bool Surplus { get; set; }
    public decimal Coverage { get; set; } = 1;
    public decimal[] Stock { get; set; } = [];
    public int? PayDay { get; set; }
    public string Risk { get; set; } = "none";
    public decimal NormFactor { get; set; } = 1;
    public decimal LossFactor { get; set; } = 1;
}

/// <summary>Состояние расчёта (база или сценарий): все промежуточные значения графа.</summary>
internal sealed class SimState
{
    public int H { get; init; }
    public bool HasSales { get; set; }
    public Dictionary<long, ProductState> Products { get; } = [];
    public Dictionary<string, RegionState> Regions { get; } = new(StringComparer.Ordinal);
    public decimal RequestedKg { get; set; }
    public decimal SoldKg { get; set; }
    public decimal Revenue { get; set; }
    public decimal LostRevenue { get; set; }
    public decimal? PlanKg { get; set; }

    public bool HasRecipes { get; set; }
    public decimal RecipeShare { get; set; }
    public decimal RequiredKg { get; set; }
    public decimal? CapacityKg { get; set; }
    public decimal CapacityRatio { get; set; } = 1;
    public decimal? Utilization { get; set; }
    public decimal PlannedKg { get; set; }
    public decimal OutputKg { get; set; }
    public decimal FgSurplusKg { get; set; }
    public bool LabourBinding { get; set; }
    public decimal DefectRate { get; set; }

    public Dictionary<Guid, MaterialState> Materials { get; } = [];
    public decimal? NeedValue { get; set; }
    public decimal Budget { get; set; }
    public decimal? StockValueStart { get; set; }
    public decimal? StockValueEnd { get; set; }
    public decimal[]? StockValueSeries { get; set; }
    public decimal? PeakStockKg { get; set; }

    public decimal? Cogs { get; set; }
    public decimal CostCoverage { get; set; }
    public decimal? KnownGross { get; set; }
    public decimal? Gross { get; set; }
    public int? Headcount { get; set; }
    public decimal? Payroll { get; set; }
    public decimal? Opex { get; set; }
    public decimal? Logistics { get; set; }
    public decimal? Operating { get; set; }

    public decimal? CashIn { get; set; }
    public decimal? CashOut { get; set; }
    public decimal[]? CashSeries { get; set; }
    public bool CashHasOpening { get; set; }
    public decimal? MinCash { get; set; }
    public int? MinCashDay { get; set; }
    public int CashGapDays { get; set; }
    public decimal? FinancingNeed { get; set; }
    public decimal? WorkingCapital { get; set; }

    public SimExpansion? Expansion { get; set; }
    public List<string> Assumptions { get; } = [];
}

/// <summary>
/// Движок What-If: детерминированный расчёт сценария по снимку исходных данных. База и сценарий считаются одними и теми же формулами
/// (база — с введёнными исходными данными, но без изменений), поэтому разница — только от параметров. Ничего не читает из базы данных и
/// ничего не пишет: вход — снимок и параметры, выход — результат. Формулы — в ONEBASE_WHATIF_PLAN.md и docs/analytik-simulator.md.
/// </summary>
public static class ScenarioEngine
{
    /// <summary>Версия формул: сохраняется в снимке и сценарии; меняется при любом изменении расчёта.</summary>
    public const string FormulaVersion = "sim-1";

    /// <summary>Доля брака не больше 90 %, скидка не больше 95 % — чтобы формулы не делили на ноль.</summary>
    private const decimal MaxDefect = 0.9m;

    private const decimal MaxDiscount = 0.95m;

    /// <summary>Остаток больше стольких дней расхода — излишек (как статус «излишек» склада Аналитика).</summary>
    public const int SurplusDays = 90;

    public static SimulationResult Run(ScenarioBaselineData b, ScenarioDefinition def)
    {
        var issues = ConstraintValidator.Validate(b, def);
        var from = b.AsOf.AddDays(1);
        var to = b.AsOf.AddDays(Math.Max(1, def.HorizonDays));
        if (issues.Any(i => i.Severity == ScenarioIssue.Error))
        {
            return new SimulationResult(FormulaVersion, def.Kind, false, def.HorizonDays, from, to, [], [], [], [], [], [], [], issues, [],
                new SimConclusion("Сценарий не посчитан: исправьте параметры.", [], [], [], [], [], []), [], null);
        }

        var all = ScenarioParams.Of(def.Parameters);
        var inputs = ScenarioParams.Of(def.Parameters, inputsOnly: true);
        var baseState = Compute(b, inputs, def.HorizonDays, null, null);
        var s = Compute(b, all, def.HorizonDays, baseState, def.NewRegionName);
        var metrics = Metrics(b, baseState, s);
        var constraints = ConstraintValidator.Check(s, baseState, b, all);
        var risks = Risks(b, baseState, s, all, constraints);
        var chain = DependencyGraph.Chain(def.Parameters);
        var result = new SimulationResult(FormulaVersion, def.Kind, true, def.HorizonDays, from, to, metrics,
            ProductRows(baseState, s), RegionRows(baseState, s), MaterialRows(b, baseState, s), Series(b, baseState, s), constraints, risks, issues, chain,
            new SimConclusion("", [], [], [], [], [], []), InputsUsed(b, all, s), s.Expansion);
        return result with { Conclusion = ScenarioNarrative.Build(b, result, baseState, s, all) };
    }

    // ================= расчёт =================

    internal static SimState Compute(ScenarioBaselineData b, ScenarioParams p, int horizon, SimState? baseState, string? newRegionName)
    {
        var h = Math.Max(1, horizon);
        var s = new SimState { H = h };
        var k = (decimal)h / Math.Max(1, b.Days);
        var scenario = baseState is not null;

        // ---- продажи: запрос и выручка при полном обеспечении ----
        var cells = new List<CellState>();
        if (b.Sales is { } sales)
        {
            s.HasSales = true;
            var demand = p.Pct("sales.demand");
            var price = p.Pct("sales.price");
            var discount = p.Value("sales.discount") / 100m;
            var elasticity = p.Value("sales.elasticity");
            foreach (var product in sales.Products)
            {
                s.Products[product.Id] = new ProductState { Product = product };
            }

            foreach (var region in sales.Regions)
            {
                s.Regions[region.Id] = new RegionState { Id = region.Id, Name = region.Name };
            }

            foreach (var cell in sales.Cells.OrderBy(c => c.ProductId).ThenBy(c => c.RegionId, StringComparer.Ordinal))
            {
                if (!s.Products.TryGetValue(cell.ProductId, out var ps))
                {
                    continue;
                }

                var factor = (1 + demand) * (1 + p.TargetPct("sales.region", cell.RegionId)) * (1 + p.TargetPct("sales.product", cell.ProductId.ToString()))
                                          * (1 + elasticity * price);
                factor = Math.Max(0, factor);
                var priceFactor = PriceFactor(price, ps.Product.DiscountShare, discount);
                var req = cell.Kg * k * factor;
                var potential = cell.Revenue * k * factor * priceFactor;
                cells.Add(new CellState(cell.ProductId, cell.RegionId, req, potential));
                ps.BaseKg += cell.Kg * k;
                ps.BaseRevenue += cell.Revenue * k;
                if (!s.Regions.TryGetValue(cell.RegionId, out var rs))
                {
                    s.Regions[cell.RegionId] = rs = new RegionState { Id = cell.RegionId, Name = cell.RegionId };
                }

                rs.BaseKg += cell.Kg * k;
                rs.BaseRevenue += cell.Revenue * k;
            }

            if (scenario && p.Value("expansion.plan") > 0)
            {
                cells.AddRange(ExpansionCells(b, sales, p, h, price, discount, newRegionName, s));
            }

            if (elasticity == 0 && price != 0)
            {
                s.Assumptions.Add("Эластичность спроса по цене не задана (0): объём продаж от изменения цены не меняется.");
            }

            foreach (var g in cells.GroupBy(c => c.ProductId))
            {
                var ps = s.Products[g.Key];
                ps.SalesReqKg = g.Sum(c => c.ReqKg);
                ps.Potential = g.Sum(c => c.Potential);
            }
        }

        // ---- производство: требуемый выпуск, мощность ----
        var volume = p.Pct("production.volume");
        foreach (var ps in s.Products.Values.Where(x => x.HasRecipe))
        {
            ps.ReqProdKg = ps.SalesReqKg * (1 + volume);
        }

        s.HasRecipes = s.Products.Values.Any(x => x.HasRecipe);
        var totalReq = s.Products.Values.Sum(x => x.SalesReqKg);
        s.RecipeShare = totalReq > 0 ? s.Products.Values.Where(x => x.HasRecipe).Sum(x => x.SalesReqKg) / totalReq : 0;
        s.RequiredKg = s.Products.Values.Where(x => x.HasRecipe).Sum(x => x.ReqProdKg);

        var d0 = Math.Clamp(b.Production?.DefectRate ?? 0, 0, MaxDefect);
        var d1 = Math.Clamp(d0 + p.Value("production.defect") / 100m, 0, MaxDefect);
        s.DefectRate = d1;
        var capacityDay = p.Input("input.capacity") is > 0 and var inputCapacity ? inputCapacity : b.Production?.CapacityKgDay;
        decimal capacityFactor = 1;
        if (capacityDay is { } c0 && c0 > 0)
        {
            var shifts0 = b.Production?.ShiftsPerDay is > 0 and var observed ? observed : 1m;
            var shifts1 = p.Value("production.shifts") is > 0 and var chosen ? chosen : shifts0;
            var equipment = 1 + p.Pct("production.capacity");
            capacityFactor = equipment;
            if (Labour(p) is { } labour)
            {
                capacityFactor = Math.Min(equipment, labour);
                s.LabourBinding = labour < equipment;
            }

            var defectFactor = (1 - d1) / (1 - d0);
            var full = c0 * capacityFactor * (shifts1 / shifts0) * defectFactor * h;
            var usable = full * (p.Has("production.utilization") ? p.Value("production.utilization") / 100m : 1);
            s.CapacityKg = Math.Max(0, usable);
            s.Utilization = full > 0 ? s.RequiredKg / full : null;
            s.CapacityRatio = s.RequiredKg > usable && s.RequiredKg > 0 ? Math.Max(0, usable) / s.RequiredKg : 1;
        }

        foreach (var ps in s.Products.Values.Where(x => x.HasRecipe))
        {
            ps.PlannedKg = ps.ReqProdKg * s.CapacityRatio;
        }

        s.PlannedKg = s.Products.Values.Where(x => x.HasRecipe).Sum(x => x.PlannedKg);

        // ---- сырьё: расход, запас по дням, закупка, обеспечение ----
        if (b.Materials is { } materials)
        {
            SimulateMaterials(b, p, s, baseState, materials, h, d0, d1);
        }

        // ---- выпуск и продажи с учётом ограничений ----
        foreach (var ps in s.Products.Values)
        {
            if (!ps.HasRecipe)
            {
                ps.SoldRatio = 1;
                continue;
            }

            var coverage = 1m;
            string? limit = s.CapacityRatio < 1 ? "мощность производства" : null;
            foreach (var norm in ps.Product.Norms)
            {
                if (s.Materials.TryGetValue(norm.ItemId, out var m) && m.Coverage < coverage)
                {
                    coverage = m.Coverage;
                    limit = $"сырьё «{m.Item.Name}»";
                }
            }

            ps.Coverage = coverage;
            ps.ProducedKg = ps.PlannedKg * coverage;
            ps.SoldRatio = ps.SalesReqKg > 0 ? Math.Min(1, ps.ProducedKg / ps.SalesReqKg) : 1;
            if (ps.SoldRatio < 1 && limit is null && volume < 0)
            {
                limit = "объём производства";
            }

            ps.Limit = ps.SoldRatio < 0.9999m ? limit : null;
        }

        s.OutputKg = s.Products.Values.Where(x => x.HasRecipe).Sum(x => x.ProducedKg);
        s.FgSurplusKg = s.Products.Values.Where(x => x.HasRecipe).Sum(x => Math.Max(0, x.ProducedKg - x.SalesReqKg));

        var revenueFactor = 1 + p.Pct("finance.revenue");
        foreach (var cell in cells)
        {
            var ps = s.Products[cell.ProductId];
            var sold = cell.ReqKg * ps.SoldRatio;
            var revenue = cell.Potential * ps.SoldRatio * revenueFactor;
            ps.SoldKg += sold;
            ps.Revenue += revenue;
            var rs = s.Regions[cell.RegionId];
            rs.ReqKg += cell.ReqKg;
            rs.SoldKg += sold;
            rs.Revenue += revenue;
            s.LostRevenue += cell.Potential * (1 - ps.SoldRatio) * revenueFactor;
        }

        s.RequestedKg = cells.Sum(c => c.ReqKg);
        s.SoldKg = s.Products.Values.Sum(x => x.SoldKg);
        s.Revenue = s.Products.Values.Sum(x => x.Revenue);

        // План: план месяца области × горизонт; новый регион — его план.
        if (b.Sales?.PlanKgMonth is > 0 and var planMonth)
        {
            s.PlanKg = planMonth * h / 30m * (1 + p.Pct("sales.plan"));
        }

        foreach (var region in b.Sales?.Regions ?? [])
        {
            if (region.PlanKgMonth is > 0 and var rp && s.Regions.TryGetValue(region.Id, out var rs))
            {
                rs.PlanKg = rp * h / 30m * (1 + p.Pct("sales.plan"));
            }
        }

        if (s.Regions.TryGetValue(NewRegionId, out var created) && p.Value("expansion.plan") > 0)
        {
            created.PlanKg = p.Value("expansion.plan") * h / 30m;
            s.PlanKg = (s.PlanKg ?? 0) + created.PlanKg;
        }

        // ---- себестоимость и прибыль ----
        var otherCost = p.Input("input.cost_per_kg") is > 0 and var oc ? oc * (1 + p.Pct("finance.cost")) : (decimal?)null;
        decimal cogs = 0, knownRevenue = 0;
        foreach (var ps in s.Products.Values)
        {
            ps.CostPerKg = MaterialCost(ps, s, d0, d1) ?? otherCost;
            if (ps.CostPerKg is { } cost)
            {
                cogs += ps.SoldKg * cost;
                knownRevenue += ps.Revenue;
            }
        }

        s.CostCoverage = s.Revenue > 0 ? knownRevenue / s.Revenue : 0;
        s.Cogs = knownRevenue > 0 || cogs > 0 ? cogs : null;
        s.KnownGross = s.Cogs is not null ? knownRevenue - cogs : null;
        s.Gross = s.Cogs is not null && s.CostCoverage >= 0.9999m ? s.Revenue - cogs : null;

        // ---- персонал, расходы, логистика ----
        var staffExpansion = scenario && p.Value("expansion.plan") > 0 ? (int)p.Value("expansion.staff") : 0;
        if (b.Workforce is { } wf)
        {
            var global = p.Pct("workforce.headcount");
            decimal headcount = wf.Employees * (1 + global);
            foreach (var (target, value) in p.Targets("workforce.headcount"))
            {
                var division = wf.Divisions.FirstOrDefault(d => d.Id == target);
                if (division is not null)
                {
                    headcount += division.Employees * (1 + global) * value / 100m;
                }
            }

            s.Headcount = (int)Math.Round(Math.Max(0, headcount), 0) + staffExpansion;
        }

        var salary = p.Input("input.salary") is > 0 and var sal ? sal * (1 + p.Pct("workforce.salary")) : (decimal?)null;
        if (salary is { } salaryMonth)
        {
            s.Payroll = s.Headcount is { } hc ? hc * salaryMonth * h / 30m : null;
        }

        if (p.Input("input.opex") is > 0 and var opex)
        {
            s.Opex = opex * (1 + p.Pct("finance.expenses")) * h / 30m + (scenario ? p.Value("expansion.opex") * h / 30m : 0);
        }

        // Логистика компании — по введённой ставке за кг; у нового региона — своя ставка.
        var expansionKg = s.Regions.TryGetValue(NewRegionId, out var er) ? er.SoldKg : 0;
        if (p.Input("input.logistics_per_kg") is > 0 and var logisticsKg)
        {
            s.Logistics = logisticsKg * (s.SoldKg - expansionKg) + (scenario ? p.Value("expansion.logistics") * expansionKg : 0);
        }

        if (s.Gross is { } gross && s.Payroll is { } payroll && s.Opex is { } opexTotal)
        {
            s.Operating = gross - payroll - opexTotal - (s.Logistics ?? 0);
        }

        if (scenario && s.Regions.ContainsKey(NewRegionId))
        {
            s.Expansion = Expansion(b, p, s, salary, h, newRegionName);
        }

        // ---- деньги и оборотный капитал ----
        CashFlow(b, p, s, baseState, h, salary is not null);
        return s;
    }

    /// <summary>Множитель цены нетто: (1 + цена) · (1 − (скидка₀ + Δ)) ÷ (1 − скидка₀).</summary>
    internal static decimal PriceFactor(decimal priceChange, decimal discount0, decimal discountDelta)
    {
        var d0 = Math.Clamp(discount0, 0, MaxDiscount);
        var d1 = Math.Clamp(d0 + discountDelta, 0, MaxDiscount);
        return (1 + priceChange) * (1 - d1) / (1 - d0);
    }

    /// <summary>
    /// Фактор персонала для мощности: (численность производства сценария ÷ базы) · (1 + производительность). Null — персонал мощность не
    /// ограничивает (численность производства не меняется и производительность та же).
    /// </summary>
    private static decimal? Labour(ScenarioParams p)
    {
        var productivity = p.Pct("workforce.productivity");
        var global = p.Pct("workforce.headcount");
        var division = p.Choice("input.production_division");
        var target = division is not null ? p.TargetPct("workforce.headcount", division) : 0;
        var staff = (1 + global) * (1 + target);
        if (staff == 1 && productivity == 0)
        {
            return null;
        }

        // Изменение численности другого подразделения мощность не меняет; общее — меняет (в нём и производство).
        return staff * (1 + productivity);
    }

    private const string NewRegionId = "new";

    /// <summary>Ячейки нового региона: состав продаж и цены — региона-образца (или средние по компании), объём — min(план, спрос).</summary>
    private static IEnumerable<CellState> ExpansionCells(ScenarioBaselineData b, BaselineSales sales, ScenarioParams p, int h, decimal price, decimal discount,
        string? name, SimState s)
    {
        var plan = p.Value("expansion.plan");
        var demand = p.Has("expansion.demand") && p.Value("expansion.demand") > 0 ? p.Value("expansion.demand") : plan;
        if (!p.Has("expansion.demand") || p.Value("expansion.demand") <= 0)
        {
            s.Assumptions.Add("Спрос нового региона не задан — принят равным плану.");
        }

        var kgHorizon = Math.Min(plan, demand) * h / 30m;
        var reference = p.Choice("expansion.reference");
        var source = sales.Cells.Where(c => reference is null || c.RegionId == reference).Where(c => c.Kg > 0).ToList();
        if (source.Count == 0)
        {
            source = sales.Cells.Where(c => c.Kg > 0).ToList();
        }

        var totalKg = source.Sum(c => c.Kg);
        s.Regions[NewRegionId] = new RegionState { Id = NewRegionId, Name = string.IsNullOrWhiteSpace(name) ? "Новый регион" : name.Trim(), New = true };
        if (totalKg <= 0)
        {
            yield break;
        }

        foreach (var g in source.GroupBy(c => c.ProductId).OrderBy(g => g.Key))
        {
            if (!s.Products.TryGetValue(g.Key, out var ps))
            {
                continue;
            }

            var kg = g.Sum(c => c.Kg);
            var revenue = g.Sum(c => c.Revenue);
            var share = kg / totalKg;
            var req = kgHorizon * share;
            var pricePerKg = revenue / kg;
            yield return new CellState(g.Key, NewRegionId, req, req * pricePerKg * PriceFactor(price, ps.Product.DiscountShare, discount));
        }

        _ = b;
    }

    private static void SimulateMaterials(ScenarioBaselineData b, ScenarioParams p, SimState s, SimState? baseState, IReadOnlyList<BaselineMaterial> materials,
        int h, decimal d0, decimal d1)
    {
        // Расход по техкартам в день: выпуск в пределах мощности × норма на кг.
        var recipeDaily = new Dictionary<Guid, decimal>();
        foreach (var ps in s.Products.Values.Where(x => x.HasRecipe))
        {
            foreach (var norm in ps.Product.Norms)
            {
                recipeDaily[norm.ItemId] = recipeDaily.GetValueOrDefault(norm.ItemId) + ps.PlannedKg / h * norm.PerKg;
            }
        }

        var normAll = p.Pct("production.norms");
        var lossDelta = p.Value("materials.loss") / 100m;
        var defectMaterial = (1 - d0) / (1 - d1);
        var stockAll = p.Pct("materials.stock");
        var priceAll = p.Pct("materials.price");
        var leadDelta = (int)p.Value("materials.lead_time");
        var delay = Math.Max(0, leadDelta);
        var safetyChange = p.Pct("procurement.safety");
        var purchaseChange = p.Pct("procurement.volume");
        var termsDays = (int)Math.Max(0, (p.Input("input.supplier_terms") ?? 0) + p.Value("procurement.supplier_terms"));
        var stockValue = new decimal[h];
        var stockKg = new decimal[h];
        var anyPrice = false;
        decimal needValue = 0, startValue = 0, endValue = 0;
        var needPriced = false;

        foreach (var item in materials.OrderBy(m => m.ItemId))
        {
            var key = item.ItemId.ToString();
            var m = new MaterialState { Item = item };
            m.RecipeDaily = recipeDaily.GetValueOrDefault(item.ItemId);
            m.NormFactor = (1 + normAll) * (1 + p.TargetPct("production.norm_item", key));
            var l0 = Math.Clamp(item.LossPercent / 100m, 0, 0.9m);
            m.LossFactor = (1 - l0) / Math.Max(0.05m, 1 - l0 - lossDelta);

            // Расход: в базе — больший из фактического по складу и расчётного по техкартам (без двойного учёта, как спрос в плане закупок);
            // в сценарии к нему добавляется изменение расхода по техкартам, затем нормы, потери и брак.
            decimal baseDaily;
            if (baseState is null)
            {
                baseDaily = Math.Max(item.HistoryRate, m.RecipeDaily);
                m.Daily = baseDaily;
            }
            else
            {
                var b0 = baseState.Materials.TryGetValue(item.ItemId, out var bm) ? bm : null;
                var recipe0 = b0?.RecipeDaily ?? 0;
                baseDaily = Math.Max(item.HistoryRate, recipe0);
                var production = Math.Max(0, baseDaily + (m.RecipeDaily - recipe0));
                m.Daily = production * m.NormFactor * m.LossFactor * (m.RecipeDaily > 0 || recipe0 > 0 ? defectMaterial : 1);
            }

            // Поставщик сценария: его последняя цена и срок поставки.
            var supplier = p.Targets("procurement.supplier").Select(t => ConstraintValidator.SupplierOf(b, t.Target))
                .FirstOrDefault(x => x is { } pair && pair.Item.ItemId == item.ItemId);
            var basePrice = supplier?.Supplier.Price ?? item.Price;
            m.Supplier = supplier?.Supplier.Name ?? item.Supplier;
            var lead = supplier?.Supplier.LeadTimeDays ?? item.LeadTimeDays;
            m.Price = basePrice is { } bp ? bp * (1 + priceAll) * (1 + p.TargetPct("materials.price_item", key)) : null;
            m.LeadTime = Math.Max(0, lead + leadDelta);
            m.ArrivalDay = Math.Max(1, m.LeadTime);
            m.Available = Math.Max(0, item.Available * (1 + stockAll) * (1 + p.TargetPct("materials.stock_item", key)));

            // Поставки: дата − дата данных, без даты — к 30-му дню (как в плане закупок), задержка сдвигает.
            var arrivals = new Dictionary<int, decimal>();
            foreach (var inc in item.Incoming)
            {
                var day = (inc.Expected is { } e ? Math.Max(1, e.DayNumber - b.AsOf.DayNumber) : 30) + delay;
                arrivals[day] = arrivals.GetValueOrDefault(day) + inc.Quantity;
            }

            m.IncomingH = arrivals.Where(a => a.Key <= h).Sum(a => a.Value);

            // Закупка по формуле плана закупок: спрос за горизонт + страховой запас − доступно − поставки; округление до партии.
            var demandH = Math.Max(item.Committed, m.Daily * h);
            var sigma = baseDaily > 0 ? (double)item.HistorySigma * (double)(m.Daily / baseDaily) : (double)item.HistorySigma;
            var safety = Math.Max(0, SupplyPlanMath.SafetyStock(sigma, Math.Max(1, m.LeadTime), item.MinQty) * (1 + safetyChange));
            m.RawPurchase = Math.Max(0, demandH + safety - m.Available - m.IncomingH);
            var minLot = p.TargetValue("procurement.min_lot", key);
            var purchase = m.RawPurchase > 0 ? SupplyPlanMath.RoundUp(Math.Max(m.RawPurchase, minLot), item.PurchaseFactor, item.Unit) : 0;
            if (purchase > 0 && purchaseChange != 0)
            {
                purchase = purchaseChange <= -1 ? 0 : SupplyPlanMath.RoundUp(purchase * (1 + purchaseChange), item.PurchaseFactor, item.Unit);
            }

            m.Purchase = purchase;
            m.LotSurplus = minLot > 0 && purchase > 0 ? Math.Max(0, purchase - SupplyPlanMath.RoundUp(m.RawPurchase, item.PurchaseFactor, item.Unit)) : 0;

            // Остаток по дням: поставки и приход закупки (через срок поставки) − расход; дни без сырья — доля обеспечения.
            var daily = h > 0 ? demandH / h : 0;
            decimal stock = m.Available, stockNoPurchase = m.Available, lost = 0;
            int? shortageDay = null, shortageNoPurchase = null;
            m.Stock = new decimal[h];
            for (var t = 1; t <= h; t++)
            {
                var arriving = arrivals.GetValueOrDefault(t);
                stock += arriving + (t == m.ArrivalDay ? m.Purchase : 0);
                stockNoPurchase += arriving;
                if (stock >= daily)
                {
                    stock -= daily;
                }
                else
                {
                    lost += daily > 0 ? (daily - stock) / daily : 0;
                    stock = 0;
                    shortageDay ??= t;
                }

                if (stockNoPurchase >= daily)
                {
                    stockNoPurchase -= daily;
                }
                else
                {
                    shortageNoPurchase ??= t;
                    stockNoPurchase = 0;
                }

                m.Stock[t - 1] = stock;
                if (m.Price is { } pr)
                {
                    stockValue[t - 1] += stock * pr;
                }

                if (item.KgPerUnit is { } kgUnit)
                {
                    stockKg[t - 1] += stock * kgUnit;
                }
            }

            m.LostDays = Math.Round(lost, 2);
            m.Coverage = h > 0 ? Math.Max(0, 1 - lost / h) : 1;
            m.Shortage = shortageDay is { } sd ? b.AsOf.AddDays(sd) : null;
            if (shortageNoPurchase is { } sn)
            {
                var orderDay = sn - m.LeadTime;
                m.OrderLate = orderDay < 1;
                m.OrderBy = b.AsOf.AddDays(Math.Max(0, orderDay));
            }

            m.EndStock = stock;
            m.Surplus = daily > 0 ? stock > daily * SurplusDays : stock > 0 && m.Purchase > 0;
            m.PayDay = m.Purchase > 0 ? m.ArrivalDay + termsDays : null;
            m.Risk = m.LostDays > 0 ? "critical" : m.OrderLate ? "high" : m.Purchase > 0 ? "medium" : m.Surplus ? "low" : "none";
            if (m.Price is { } price)
            {
                anyPrice = true;
                s.Budget += m.Purchase * price;
                startValue += m.Available * price;
                endValue += m.EndStock * price;
                if (m.Daily > 0)
                {
                    needValue += m.Daily * h * price;
                    needPriced = true;
                }
            }

            s.Materials[item.ItemId] = m;
        }

        s.NeedValue = needPriced ? needValue : null;
        s.StockValueStart = anyPrice ? startValue : null;
        s.StockValueEnd = anyPrice ? endValue : null;
        s.StockValueSeries = anyPrice ? stockValue : null;
        s.PeakStockKg = materials.Any(m => m.KgPerUnit is not null) ? stockKg.DefaultIfEmpty(0).Max() : null;
        if (materials.Any(m => m.Incoming.Count > 0))
        {
            s.Assumptions.Add("Ожидаемые поставки, заказанные до сценария, в выплатах не учитываются — их оплата уже согласована раньше.");
        }
    }

    /// <summary>Сырьевая себестоимость кг товара: Σ норма · нормы и потери сценария · брак · цена. Null — у части сырья нет цены или нет техкарты.</summary>
    private static decimal? MaterialCost(ProductState ps, SimState s, decimal d0, decimal d1)
    {
        if (!ps.HasRecipe || s.Materials.Count == 0)
        {
            return null;
        }

        decimal cost = 0;
        var defect = (1 - d0) / (1 - d1);
        foreach (var norm in ps.Product.Norms)
        {
            if (!s.Materials.TryGetValue(norm.ItemId, out var m) || m.Price is not { } price)
            {
                return null;
            }

            cost += norm.PerKg * m.NormFactor * m.LossFactor * defect * price;
        }

        return cost;
    }

    private static SimExpansion Expansion(ScenarioBaselineData b, ScenarioParams p, SimState s, decimal? salary, int h, string? name)
    {
        var region = s.Regions[NewRegionId];
        var notes = new List<string>();
        var kgMonth = region.SoldKg * 30m / h;
        var revenueMonth = region.Revenue * 30m / h;
        var staff = (int)p.Value("expansion.staff");
        var opex = p.Value("expansion.opex");
        var logistics = p.Value("expansion.logistics");
        var reference = p.Choice("expansion.reference");
        var referenceRegion = reference is null ? null : b.Sales?.Regions.FirstOrDefault(r => r.Id == reference);

        // Себестоимость кг нового региона — средневзвешенная по составу продаж образца (или компании); неизвестна у части товаров — прибыль
        // не считается.
        decimal? costPerKg = null;
        var refCells = (b.Sales?.Cells ?? []).Where(c => reference is null || c.RegionId == reference).Where(c => c.Kg > 0).ToList();
        if (refCells.Count == 0)
        {
            refCells = (b.Sales?.Cells ?? []).Where(c => c.Kg > 0).ToList();
        }

        var refKg = refCells.Sum(c => c.Kg);
        if (region.SoldKg > 0 && refKg > 0 && refCells.All(c => s.Products.TryGetValue(c.ProductId, out var ps) && ps.CostPerKg is not null))
        {
            costPerKg = refCells.Sum(c => c.Kg * s.Products[c.ProductId].CostPerKg!.Value) / refKg;
        }

        if (costPerKg is null)
        {
            notes.Add("Себестоимость части товаров неизвестна (нет техкарты с ценами и не введена себестоимость кг) — прибыль региона не считается.");
        }

        var payrollMonth = staff > 0 ? (salary is { } sm ? staff * sm : (decimal?)null) : 0;
        if (staff > 0 && salary is null)
        {
            notes.Add("Средняя зарплата не введена — ФОТ региона не считается.");
        }

        decimal? fixedMonth = payrollMonth is { } pm ? pm + opex : null;
        decimal? cost = costPerKg is { } ck && fixedMonth is { } fm ? (ck + logistics) * region.SoldKg + fm * h / 30m : null;
        decimal? profitMonth = costPerKg is { } c && fixedMonth is { } f ? revenueMonth - kgMonth * (c + logistics) - f : null;
        var pricePerKg = kgMonth > 0 ? revenueMonth / kgMonth : (decimal?)null;
        decimal? breakEven = pricePerKg is { } pk && costPerKg is { } c2 && fixedMonth is { } f2 && pk - c2 - logistics > 0 ? f2 / (pk - c2 - logistics) : null;
        if (pricePerKg is { } pk2 && costPerKg is { } c3 && pk2 - c3 - logistics <= 0)
        {
            notes.Add("Цена кг не покрывает себестоимость и логистику — безубыточности нет при любом объёме.");
        }

        var investment = p.Value("expansion.investment");
        decimal? payback = investment > 0 && profitMonth is > 0 ? investment / profitMonth.Value : null;
        if (investment > 0 && profitMonth is <= 0)
        {
            notes.Add("Регион в месяц убыточен — вложения не окупаются.");
        }

        // Нужный персонал: план ÷ продажи на агента в месяц у образца (или в среднем по компании).
        int? staffNeed = null;
        var refRegions = referenceRegion is not null ? [referenceRegion] : b.Sales?.Regions ?? [];
        var agents = refRegions.Sum(r => r.Agents);
        var kgPerAgentMonth = agents > 0 ? refRegions.Sum(r => r.Kg) / b.Days * 30m / agents : 0;
        if (kgPerAgentMonth > 0)
        {
            staffNeed = (int)Math.Ceiling(p.Value("expansion.plan") / kgPerAgentMonth);
        }
        else
        {
            notes.Add("Продаж на агента у образца нет — нужный персонал не посчитан.");
        }

        return new SimExpansion(region.Name, referenceRegion?.Name, Math.Round(region.SoldKg, 1), Math.Round(region.Revenue, 0), Round(cost), Round(fixedMonth),
            Round(profitMonth), breakEven is { } be ? Math.Round(be, 0) : null, payback is { } pb ? Math.Round(pb, 1) : null, staffNeed, staff, notes);
    }

    /// <summary>
    /// Деньги по дням: поступления — доля оплат × выручка со сдвигом на срок оплаты (продажи до сценария платят по прежнему сроку, продажи
    /// сценария — по новому); выплаты — закупки (в день прихода + отсрочка поставщику), ФОТ, расходы, логистика, вложения нового региона.
    /// </summary>
    private static void CashFlow(ScenarioBaselineData b, ScenarioParams p, SimState s, SimState? baseState, int h, bool payrollKnown)
    {
        if (b.Finance?.CollectionRate is not { } rate || !s.HasSales)
        {
            return;
        }

        var lag0 = (int)Math.Round(b.Finance.PaymentLagDays ?? 0, 0);
        var lag1 = Math.Max(0, lag0 + (int)p.Value("finance.customer_terms"));
        var revenue0 = (baseState ?? s).Revenue / h;
        var revenue1 = s.Revenue / h;
        var opening = p.Input("input.cash");
        s.CashHasOpening = opening is not null;
        var balance = opening ?? 0;
        var series = new decimal[h];
        decimal cashIn = 0, cashOut = 0, min = decimal.MaxValue;
        var minDay = 0;
        var gap = 0;
        var purchases = s.Materials.Values.Where(m => m.PayDay is not null && m.Price is not null)
            .GroupBy(m => m.PayDay!.Value).ToDictionary(g => g.Key, g => g.Sum(m => m.Purchase * m.Price!.Value));
        var fixedDaily = ((payrollKnown ? s.Payroll ?? 0 : 0) + (s.Opex ?? 0) + (s.Logistics ?? 0)) / h;
        var investment = baseState is not null && p.Value("expansion.plan") > 0 ? p.Value("expansion.investment") : 0;
        for (var t = 1; t <= h; t++)
        {
            var inflow = rate * ((t <= lag0 ? revenue0 : 0) + (t - lag1 >= 1 ? revenue1 : 0));
            var outflow = purchases.GetValueOrDefault(t) + fixedDaily + (t == 1 ? investment : 0);
            cashIn += inflow;
            cashOut += outflow;
            balance += inflow - outflow;
            series[t - 1] = balance;
            if (balance < min)
            {
                min = balance;
                minDay = t;
            }

            if (balance < 0)
            {
                gap++;
            }
        }

        s.CashIn = cashIn;
        s.CashOut = cashOut;
        s.CashSeries = series;
        s.MinCash = min;
        s.MinCashDay = minDay;
        s.CashGapDays = s.CashHasOpening ? gap : 0;
        s.FinancingNeed = Math.Max(0, -min);
        s.Assumptions.Add(rate >= 1
            ? "Поступления — по доле оплат к выручке базового периода."
            : $"Поступления — {Math.Round(rate * 100, 1)} % выручки (доля оплат к выручке базового периода), через {lag1} дн. после реализации.");
        if (p.Input("input.supplier_terms") is null)
        {
            s.Assumptions.Add("Отсрочка поставщикам не введена — закупка оплачивается в день прихода.");
        }

        if (!s.CashHasOpening)
        {
            s.Assumptions.Add("Деньги на начало не введены — показан накопленный поток от нуля; кассовые разрывы — по нему.");
        }

        // Оборотный капитал на конец: запас сырья + дебиторка (неоплаченные продажи последних дней) − кредиторка (неоплаченные закупки).
        var receivables = rate * revenue1 * Math.Min(lag1, h);
        var payables = purchases.Where(x => x.Key > h).Sum(x => x.Value);
        s.WorkingCapital = (s.StockValueEnd ?? 0) + receivables - payables;
    }

    // ================= результат =================

    private static decimal? Round(decimal? v, int digits = 0) => v is { } x ? Math.Round(x, digits) : null;

    private static SimMetric Metric(string code, string group, string title, string unit, string node, decimal? baseValue, decimal? scenario, string kind,
        string? note = null, int digits = 0)
    {
        var b = Round(baseValue, digits);
        var s = Round(scenario, digits);
        decimal? delta = b is { } x && s is { } y ? y - x : null;
        decimal? pct = b is { } bx && s is { } sy && bx != 0 ? Math.Round((sy - bx) / Math.Abs(bx) * 100, 1) : null;
        return new SimMetric(code, group, title, unit, node, b, s, delta, pct, kind, note);
    }

    private static List<SimMetric> Metrics(ScenarioBaselineData b, SimState bs, SimState s)
    {
        var list = new List<SimMetric>();
        if (s.HasSales)
        {
            list.Add(Metric("sales.requested_kg", "sales", "Запрошенный объём", "кг", "sales.requested", bs.RequestedKg, s.RequestedKg, "fact", digits: 1));
            list.Add(Metric("sales.kg", "sales", "Продажи (обеспечено)", "кг", "sales.sold", bs.SoldKg, s.SoldKg, "fact", digits: 1));
            list.Add(Metric("sales.revenue", "sales", "Выручка", "сум", "sales.revenue", bs.Revenue, s.Revenue, "fact"));
            list.Add(Metric("sales.avg_price", "sales", "Средняя цена кг", "сум/кг", "sales.revenue", bs.SoldKg > 0 ? bs.Revenue / bs.SoldKg : null,
                s.SoldKg > 0 ? s.Revenue / s.SoldKg : null, "calculated"));
            list.Add(Metric("sales.lost_revenue", "sales", "Недополученная выручка", "сум", "sales.sold", bs.LostRevenue, s.LostRevenue, "calculated"));
            list.Add(Metric("sales.plan_kg", "sales", "План продаж", "кг", "sales.plan", bs.PlanKg, s.PlanKg, "fact",
                bs.PlanKg is null && s.PlanKg is null ? "Планов продаж на месяц в OneBase нет." : null, 1));
            list.Add(Metric("sales.plan_execution", "sales", "Выполнение плана", "%", "sales.plan",
                bs.PlanKg is > 0 ? bs.SoldKg / bs.PlanKg * 100 : null, s.PlanKg is > 0 ? s.SoldKg / s.PlanKg * 100 : null, "calculated", digits: 1));
        }

        if (s.HasRecipes)
        {
            var capacityNote = s.CapacityKg is null ? "Мощность неизвестна: утверждённых смен нет и мощность не введена." : null;
            list.Add(Metric("production.recipe_share", "production", "Продаж покрыто техкартами", "%", "sales.requested", bs.RecipeShare * 100, s.RecipeShare * 100,
                "calculated", digits: 1));
            list.Add(Metric("production.required", "production", "Требуемый выпуск", "кг", "production.required", bs.RequiredKg, s.RequiredKg, "calculated", digits: 1));
            list.Add(Metric("production.capacity", "production", "Мощность за период", "кг", "production.capacity", bs.CapacityKg, s.CapacityKg,
                CapacityKind(b), capacityNote, 1));
            list.Add(Metric("production.utilization", "production", "Загрузка мощностей", "%", "production.planned", bs.Utilization * 100, s.Utilization * 100,
                CapacityKind(b), capacityNote, 1));
            list.Add(Metric("production.output", "production", "Выпуск", "кг", "production.output", bs.OutputKg, s.OutputKg, "calculated", digits: 1));
            list.Add(Metric("production.fg_surplus", "production", "Прирост запаса готовой продукции", "кг", "production.output", bs.FgSurplusKg, s.FgSurplusKg,
                "calculated", digits: 1));
        }
        else if (s.HasSales)
        {
            list.Add(Metric("production.required", "production", "Требуемый выпуск", "кг", "production.required", null, null, "calculated",
                "Ни один товар продаж не связан с действующей техкартой — производство и сырьё от продаж не пересчитываются. Свяжите техкарты с товарами Linko."));
        }

        if (b.Materials is not null)
        {
            list.Add(Metric("materials.need_value", "materials", "Потребность в сырье", "сум", "materials.need_value", bs.NeedValue, s.NeedValue, "calculated",
                s.NeedValue is null ? "У позиций с расходом нет цены прихода." : null));
            list.Add(Metric("materials.items_short", "materials", "Позиций с дефицитом", "шт", "materials.coverage", bs.Materials.Values.Count(m => m.LostDays > 0),
                s.Materials.Values.Count(m => m.LostDays > 0), "calculated"));
            list.Add(Metric("materials.max_lost_days", "materials", "Дней без сырья (максимум)", "дн.", "materials.coverage",
                bs.Materials.Values.Select(m => m.LostDays).DefaultIfEmpty(0).Max(), s.Materials.Values.Select(m => m.LostDays).DefaultIfEmpty(0).Max(), "calculated",
                digits: 1));
            list.Add(Metric("materials.end_stock_value", "materials", "Запас сырья на конец", "сум", "inventory.stock", bs.StockValueEnd, s.StockValueEnd, "calculated",
                s.StockValueEnd is null ? "Цен прихода нет — стоимость запаса не посчитана." : null));
            list.Add(Metric("procurement.budget", "procurement", "Бюджет закупок", "сум", "procurement.budget", bs.Budget, s.Budget, "calculated",
                s.Materials.Values.Any(m => m.Purchase > 0 && m.Price is null) ? "Без позиций, у которых нет цены прихода." : null));
            list.Add(Metric("procurement.items", "procurement", "Позиций к закупке", "шт", "procurement.purchase", bs.Materials.Values.Count(m => m.Purchase > 0),
                s.Materials.Values.Count(m => m.Purchase > 0), "calculated"));
            list.Add(Metric("procurement.unpriced", "procurement", "Позиций к закупке без цены", "шт", "procurement.purchase",
                bs.Materials.Values.Count(m => m.Purchase > 0 && m.Price is null), s.Materials.Values.Count(m => m.Purchase > 0 && m.Price is null), "calculated"));
            list.Add(Metric("procurement.surplus_items", "procurement", "Позиций с излишком", "шт", "inventory.stock", bs.Materials.Values.Count(m => m.Surplus),
                s.Materials.Values.Count(m => m.Surplus), "calculated"));
        }

        if (s.HasSales)
        {
            var costNote = s.Cogs is null ? "Себестоимость неизвестна: нет техкарт с ценами сырья и не введена себестоимость кг." :
                s.CostCoverage < 0.9999m ? $"Себестоимость известна для {Math.Round(s.CostCoverage * 100, 1)} % выручки — валовая прибыль только по ним." : null;
            list.Add(Metric("finance.cost_per_kg", "finance", "Себестоимость кг (в среднем)", "сум/кг", "cost.unit", AvgCost(bs), AvgCost(s), "calculated", costNote));
            list.Add(Metric("finance.cogs", "finance", "Себестоимость проданного", "сум", "finance.cogs", bs.Cogs, s.Cogs, "calculated", costNote));
            list.Add(Metric("finance.gross_profit", "finance", "Валовая прибыль", "сум", "finance.gross", bs.Gross ?? bs.KnownGross, s.Gross ?? s.KnownGross,
                "calculated", costNote));
            list.Add(Metric("finance.gross_margin", "finance", "Валовая маржа", "%", "finance.gross", Margin(bs), Margin(s), "calculated", costNote, 1));
            list.Add(Metric("finance.payroll", "finance", "ФОТ", "сум", "workforce.payroll", bs.Payroll, s.Payroll, "input",
                s.Payroll is null ? "Средняя зарплата не введена (ФОТ в OneBase нет)." : null));
            list.Add(Metric("finance.opex", "finance", "Расходы компании", "сум", "finance.opex", bs.Opex, s.Opex, "input",
                s.Opex is null ? "Расходы компании не введены (в OneBase их нет)." : null));
            list.Add(Metric("finance.logistics", "finance", "Логистика", "сум", "finance.logistics", bs.Logistics, s.Logistics, "input",
                s.Logistics is null ? "Логистика за кг не введена." : null));
            list.Add(Metric("finance.operating_profit", "finance", "Операционная прибыль", "сум", "finance.operating", bs.Operating, s.Operating, "input",
                s.Operating is null ? "Нужны себестоимость всех товаров, средняя зарплата и расходы компании." : null));
            var cashNote = s.CashIn is null ? "Оплаты точек вам не видны или не загружены — денежный поток не считается." : null;
            list.Add(Metric("finance.cash_in", "finance", "Поступления", "сум", "finance.cash", bs.CashIn, s.CashIn, "calculated", cashNote));
            list.Add(Metric("finance.cash_out", "finance", "Выплаты", "сум", "finance.cash", bs.CashOut, s.CashOut, "calculated", cashNote));
            list.Add(Metric("finance.net_cash", "finance", "Денежный поток", "сум", "finance.cash", bs.CashIn - bs.CashOut, s.CashIn - s.CashOut, "calculated", cashNote));
            list.Add(Metric("finance.min_cash", "finance", s.CashHasOpening ? "Минимальный остаток денег" : "Минимум накопленного потока", "сум", "finance.cash",
                bs.MinCash, s.MinCash, s.CashHasOpening ? "input" : "calculated", cashNote));
            list.Add(Metric("finance.financing_need", "finance", "Потребность в финансировании", "сум", "finance.financing", bs.FinancingNeed, s.FinancingNeed,
                "calculated", cashNote));
            list.Add(Metric("finance.gap_days", "finance", "Дней кассового разрыва", "дн.", "finance.cash", s.CashHasOpening ? bs.CashGapDays : null,
                s.CashHasOpening ? s.CashGapDays : null, "input", s.CashHasOpening ? cashNote : "Введите деньги на начало, чтобы считать дни разрыва."));
            list.Add(Metric("finance.working_capital", "finance", "Оборотный капитал на конец", "сум", "finance.working_capital", bs.WorkingCapital, s.WorkingCapital,
                "calculated", cashNote));
        }

        if (s.Headcount is not null || bs.Headcount is not null)
        {
            list.Add(Metric("workforce.headcount", "workforce", "Численность", "чел.", "workforce.headcount", bs.Headcount, s.Headcount, "fact"));
        }

        return list;
    }

    private static string CapacityKind(ScenarioBaselineData b) => b.Production?.CapacityKgDay is > 0 ? "calculated" : "input";

    private static decimal? AvgCost(SimState s)
    {
        var known = s.Products.Values.Where(x => x.CostPerKg is not null && x.SoldKg > 0).ToList();
        var kg = known.Sum(x => x.SoldKg);
        return kg > 0 ? known.Sum(x => x.SoldKg * x.CostPerKg!.Value) / kg : null;
    }

    private static decimal? Margin(SimState s)
    {
        if (s.Cogs is null)
        {
            return null;
        }

        var revenue = s.Products.Values.Where(x => x.CostPerKg is not null).Sum(x => x.Revenue);
        return revenue > 0 ? (s.Gross ?? s.KnownGross) / revenue * 100 : null;
    }

    private static IReadOnlyList<SimProductRow> ProductRows(SimState bs, SimState s) =>
        s.Products.Values.Where(x => x.SalesReqKg > 0 || x.BaseKg > 0)
            .OrderByDescending(x => x.Revenue).ThenBy(x => x.Product.Id)
            .Take(200)
            .Select(x =>
            {
                var b = bs.Products.GetValueOrDefault(x.Product.Id);
                return new SimProductRow(x.Product.Id, x.Product.Name, x.HasRecipe, Math.Round(x.BaseKg, 1), Math.Round(x.SalesReqKg, 1),
                    x.HasRecipe ? Math.Round(x.ProducedKg, 1) : null, Math.Round(x.SoldKg, 1), Math.Round(x.BaseRevenue, 0), Math.Round(x.Revenue, 0),
                    Round(b?.CostPerKg), Round(x.CostPerKg), x.CostPerKg is { } c ? Math.Round(x.Revenue - x.SoldKg * c, 0) : null, Math.Round(x.Coverage, 4), x.Limit);
            }).ToList();

    private static IReadOnlyList<SimRegionRow> RegionRows(SimState bs, SimState s) =>
        s.Regions.Values.Where(r => r.ReqKg > 0 || r.BaseKg > 0 || r.New)
            .OrderBy(r => r.New ? 1 : 0).ThenByDescending(r => r.Revenue).ThenBy(r => r.Id, StringComparer.Ordinal)
            .Select(r =>
            {
                var b = bs.Regions.GetValueOrDefault(r.Id);
                return new SimRegionRow(r.Id, r.Name, r.New, Math.Round(r.BaseKg, 1), Math.Round(r.SoldKg, 1), Math.Round(r.BaseRevenue, 0), Math.Round(r.Revenue, 0),
                    Round(r.PlanKg, 1), r.PlanKg is > 0 ? Math.Round(r.SoldKg / r.PlanKg.Value * 100, 1) : null);
            }).ToList();

    private static IReadOnlyList<SimMaterialRow> MaterialRows(ScenarioBaselineData b, SimState bs, SimState s) =>
        s.Materials.Values.Where(m => m.Daily > 0 || m.Purchase > 0 || (bs.Materials.GetValueOrDefault(m.Item.ItemId)?.Daily ?? 0) > 0)
            .OrderBy(m => SupplyPlanMath.RiskRank(m.Risk)).ThenByDescending(m => m.LostDays).ThenByDescending(m => m.Purchase * (m.Price ?? 0)).ThenBy(m => m.Item.Name)
            .Take(300)
            .Select(m =>
            {
                var bm = bs.Materials.GetValueOrDefault(m.Item.ItemId);
                return new SimMaterialRow(m.Item.ItemId, m.Item.Name, m.Item.Unit, Math.Round(bm?.Daily ?? 0, 3), Math.Round(m.Daily, 3), Math.Round(m.Available, 3),
                    Math.Round(m.IncomingH, 3), Math.Round(m.Purchase, 3), m.Price is { } pr ? Math.Round(m.Purchase * pr, 0) : null, Round(bm?.Price, 2),
                    Round(m.Price, 2), m.Shortage, m.LostDays, m.OrderBy, m.OrderLate, m.LeadTime, Math.Round(m.EndStock, 3),
                    m.Price is { } p2 ? Math.Round(m.EndStock * p2, 0) : null, m.Surplus, Math.Round(m.Coverage, 4), m.Risk, m.Supplier);
            }).ToList();

    private static IReadOnlyList<SimSeries> Series(ScenarioBaselineData b, SimState bs, SimState s)
    {
        var list = new List<SimSeries>();
        if (s.CashSeries is not null && bs.CashSeries is not null)
        {
            list.Add(new SimSeries("cash", s.CashHasOpening ? "Остаток денег" : "Накопленный денежный поток", "сум",
                bs.CashSeries.Select(v => (decimal?)Math.Round(v, 0)).ToList(), s.CashSeries.Select(v => (decimal?)Math.Round(v, 0)).ToList()));
        }

        if (s.StockValueSeries is not null && bs.StockValueSeries is not null)
        {
            list.Add(new SimSeries("stock_value", "Запас сырья, стоимость", "сум", bs.StockValueSeries.Select(v => (decimal?)Math.Round(v, 0)).ToList(),
                s.StockValueSeries.Select(v => (decimal?)Math.Round(v, 0)).ToList()));
        }

        // Графики запаса — самые важные позиции: риск, дни без сырья, стоимость расхода за горизонт.
        foreach (var m in s.Materials.Values.Where(m => m.Daily > 0).OrderBy(m => SupplyPlanMath.RiskRank(m.Risk)).ThenByDescending(m => m.LostDays)
                     .ThenByDescending(m => m.Daily * (m.Price ?? 0)).ThenBy(m => m.Item.Name).Take(3))
        {
            var bm = bs.Materials.GetValueOrDefault(m.Item.ItemId);
            list.Add(new SimSeries($"stock:{m.Item.ItemId}", m.Item.Name, m.Item.Unit,
                (bm?.Stock ?? []).Select(v => (decimal?)Math.Round(v, 3)).ToList(), m.Stock.Select(v => (decimal?)Math.Round(v, 3)).ToList()));
        }

        _ = b;
        return list;
    }

    private static IReadOnlyList<SimInput> InputsUsed(ScenarioBaselineData b, ScenarioParams p, SimState s)
    {
        const string user = "введено пользователем";
        var list = new List<SimInput>();
        void Add(string code, string title, decimal? value, string unit, string origin) => list.Add(new SimInput(code, title, value, unit, origin));

        if (p.Input("input.capacity") is > 0 and var cap)
        {
            Add("capacity", "Мощность производства", cap, "кг/сут", user);
        }
        else if (b.Production?.CapacityKgDay is { } c)
        {
            Add("capacity", "Мощность производства", Math.Round(c, 1), "кг/сут", b.Production.CapacitySource);
        }

        if (b.Production is { } prod)
        {
            Add("defect_rate", "Брак (база)", Math.Round(prod.DefectRate * 100, 2), "%", prod.Shifts > 0 ? $"OneBase → утверждённые смены ({prod.Shifts})" : "нет смен — принят 0");
            if (prod.ShiftsPerDay is { } spd)
            {
                Add("shifts", "Смен в рабочий день (база)", Math.Round(spd, 2), "смен", "OneBase → утверждённые смены");
            }
        }

        if (b.Finance is { } f)
        {
            Add("collection_rate", "Доля оплат к выручке", f.CollectionRate is { } cr ? Math.Round(cr * 100, 1) : null, "%", "OneBase → Linko: оплаты точек");
            Add("payment_lag", "Срок оплаты точек", f.PaymentLagDays is { } lag ? Math.Round(lag, 1) : null, "дн.", "OneBase → Linko: оплаты с заказом");
        }

        foreach (var (code, title, unit) in new[]
                 {
                     ("input.salary", "Средняя зарплата", "сум/мес"), ("input.opex", "Расходы компании", "сум/мес"), ("input.cash", "Деньги на начало", "сум"),
                     ("input.credit_limit", "Кредитный лимит", "сум"), ("input.cost_per_kg", "Себестоимость кг (без техкарты)", "сум/кг"),
                     ("input.logistics_per_kg", "Логистика", "сум/кг"), ("input.supplier_terms", "Отсрочка поставщикам", "дн."),
                     ("input.warehouse_kg", "Вместимость склада сырья", "кг"),
                 })
        {
            if (p.Input(code) is { } v)
            {
                Add(code, title, v, unit, user);
            }
        }

        if (p.Choice("input.production_division") is { } division)
        {
            var d = b.Workforce?.Divisions.FirstOrDefault(x => x.Id == division);
            Add("input.production_division", $"Производственное подразделение: {d?.Name ?? division}", d?.Employees, "чел.", user);
        }

        _ = s;
        return list;
    }

    private static IReadOnlyList<SimRisk> Risks(ScenarioBaselineData b, SimState bs, SimState s, ScenarioParams p, IReadOnlyList<SimConstraint> constraints)
    {
        var list = new List<SimRisk>();
        var short_ = s.Materials.Values.Where(m => m.LostDays > 0).ToList();
        if (short_.Count > 0)
        {
            list.Add(new("shortage", "critical", "Остановка выпуска из-за сырья",
                $"Позиций с дефицитом: {short_.Count}; дольше всего без сырья — «{short_.MaxBy(m => m.LostDays)!.Item.Name}» ({ConstraintValidator.Days(short_.Max(m => m.LostDays))})."));
        }

        if (s.Utilization is > 0.9m)
        {
            list.Add(new("capacity", s.Utilization > 1 ? "critical" : "high", "Перегрузка производства",
                $"Загрузка мощностей {ConstraintValidator.Pct(s.Utilization)} — нет резерва на ремонт и сбои."));
        }

        if (s.CashHasOpening && s.CashGapDays > 0)
        {
            list.Add(new("cash", "critical", "Кассовый разрыв", $"{s.CashGapDays} дн. с отрицательным остатком; минимум {ConstraintValidator.Money(s.MinCash)}."));
        }
        else if (!s.CashHasOpening && s.CashIn is not null && s.MinCash < 0 && (bs.MinCash ?? 0) >= s.MinCash)
        {
            list.Add(new("cash_flow", "high", "Отрицательный денежный поток",
                $"Накопленный поток уходит в минус до {ConstraintValidator.Money(s.MinCash)} — нужен остаток денег не меньше этой суммы."));
        }

        if (Margin(bs) is { } m0 && Margin(s) is { } m1 && m1 < m0 - 2)
        {
            list.Add(new("margin", "high", "Снижение маржи", $"Валовая маржа {Math.Round(m0, 1)} % → {Math.Round(m1, 1)} %."));
        }

        if (p.Value("sales.price") != 0 && p.Value("sales.elasticity") == 0)
        {
            list.Add(new("elasticity", "medium", "Реакция спроса на цену не учтена",
                "Цена изменена, а эластичность не задана: объём считается неизменным — на деле спрос может сдвинуться."));
        }

        if (s.HasSales && s.RecipeShare < 0.5m)
        {
            list.Add(new("recipes", "low", "Цепочка производства покрыта частично",
                $"Техкартами покрыто {Math.Round(s.RecipeShare * 100, 1)} % продаж — для остального производство и сырьё не пересчитываются."));
        }

        var unpriced = s.Materials.Values.Count(m => m.Purchase > 0 && m.Price is null);
        if (unpriced > 0)
        {
            list.Add(new("prices", "low", "Бюджет закупок неполный", $"У {unpriced} позиций к закупке нет цены прихода — их стоимость не посчитана."));
        }

        _ = b;
        _ = constraints;
        return list;
    }
}
