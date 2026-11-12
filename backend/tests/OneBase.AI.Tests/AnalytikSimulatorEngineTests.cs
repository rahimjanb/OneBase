using OneBase.AI.Agents;
using OneBase.AI.Analytik;
using OneBase.AI.Consultant;
using OneBase.AI.Orchestration;
using OneBase.AI.Providers;
using OneBase.Application.Analytik.Simulation;
using OneBase.Application.Security;
using OneBase.Application.Analytik.Supply;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>
/// Движок What-If без базы: на маленьком снимке, где каждое число можно пересчитать вручную. База — 30 дней (01.10–30.10.2026), горизонт —
/// 30 дней, поэтому коэффициент пересчёта на горизонт = 1. Товар «Печенье» (техкарта: мука 0,6 кг/кг, сахар 0,2 кг/кг) продаётся в двух
/// регионах, «Вафли» — без техкарты. Мощность 60 кг/сут в 2 смены, брак 5 %. Мука: доступно 1000 кг, поставка 500 кг через 5 дней, срок 10 дн.,
/// 5000 сум/кг. Сахар: доступно 120 кг, срок 7 дн., 8000 сум/кг. Оплаты — 90 % выручки через 3 дня.
/// </summary>
public class AnalytikSimulatorEngineTests
{
    private static readonly DateOnly AsOf = new(2026, 10, 30);
    private static readonly Guid Flour = Guid.Parse("00000000-0000-0000-0000-00000000f100");
    private static readonly Guid Sugar = Guid.Parse("00000000-0000-0000-0000-00000000a200");
    private const long Cookies = 101;
    private const long Wafers = 202;

    internal static ScenarioBaselineData Baseline(decimal sugarAvailable = 120, BaselineProduction? production = null)
    {
        var products = new List<BaselineProduct>
        {
            new(Cookies, "Печенье", 1500, 30_000_000, 0.1m, Guid.NewGuid(), "TK-1", [new(Flour, 0.6m), new(Sugar, 0.2m)], null),
            new(Wafers, "Вафли", 300, 9_000_000, 0, null, null, [], "нет действующей техкарты"),
        };
        var regions = new List<BaselineRegion>
        {
            new("r1", "Ташкент", 1300, 29_000_000, 1500, 5),
            new("r2", "Самарканд", 500, 10_000_000, 600, 2),
        };
        var cells = new List<BaselineCell>
        {
            new(Cookies, "r1", 1000, 20_000_000),
            new(Cookies, "r2", 500, 10_000_000),
            new(Wafers, "r1", 300, 9_000_000),
        };
        var sales = new BaselineSales(1800, 39_000_000, 0.077m, AsOf, null, true, 2100, "РОП", regions, products, cells);
        var materials = new List<BaselineMaterial>
        {
            new(Flour, "M-1", "Мука", "кг", 1000, 0, 0, 1000, 0, 25, 5, 90, 0, [new(500, AsOf.AddDays(5), "PO-1")], 10, false, "у поставщика",
                5000, "приход", "Мельница", null, null, 0, 1, [new("s1", "Мельница", 5000, 10, AsOf), new("s2", "Агро", 4500, 20, AsOf.AddDays(-40))]),
            new(Sugar, "M-2", "Сахар", "кг", sugarAvailable, 0, 0, sugarAvailable, 0, 5, 1, 90, 0, [], 7, false, "у поставщика", 8000, "приход", "Сахарный завод",
                null, null, 0, 1, [new("s3", "Сахарный завод", 8000, 7, AsOf)]),
        };
        return new ScenarioBaselineData(ScenarioEngine.FormulaVersion, "onebase", new DateOnly(2026, 10, 1), AsOf, AsOf, new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero),
            ["sales", "production", "materials", "finance", "workforce"], null, sales,
            production ?? new BaselineProduction(60, 50, 30, 2, 1, 0.05m, 40, 90, "лучшая смена × смен в день", []),
            materials, new BaselineFinance(35_100_000, 0.9m, 3, 5_000_000, 0, []),
            new BaselineWorkforce(100, true, [new("40", "Производство", 40), new("60", "Продажи", 60)]), [], []);
    }

    private static SimulationResult Run(params ScenarioParameter[] parameters) => Run(Baseline(), 30, parameters);

    private static SimulationResult Run(ScenarioBaselineData b, int horizon, params ScenarioParameter[] parameters) =>
        ScenarioEngine.Run(b, new ScenarioDefinition(ScenarioKinds.Combined, horizon, parameters));

    private static ScenarioParameter P(string code, decimal value, string? target = null) => new(code, value, target);

    private static decimal Base(SimulationResult r, string code) => r.Metric(code)!.Base!.Value;

    private static decimal Scen(SimulationResult r, string code) => r.Metric(code)!.Scenario!.Value;

    [Fact]
    public void Base_scenario_reproduces_the_baseline_and_changes_nothing()
    {
        var r = Run();

        Assert.True(r.Valid);
        Assert.Equal(39_000_000, Base(r, "sales.revenue"));
        Assert.Equal(1800, Base(r, "sales.kg"));
        // Требуемый выпуск — только товары с техкартой: 1500 кг; мощность 60 × 30 = 1800 кг, загрузка 1500 ÷ 1800.
        Assert.Equal(1500, Base(r, "production.required"));
        Assert.Equal(1800, Base(r, "production.capacity"));
        Assert.Equal(83.3m, Base(r, "production.utilization"));
        Assert.All(r.Metrics.Where(m => m.Base is not null), m => Assert.Equal(m.Base, m.Scenario));
        Assert.Empty(r.Chain);
        Assert.StartsWith("Базовый сценарий", r.Conclusion.Headline);
        // Себестоимость кг печенья: 0,6 × 5000 + 0,2 × 8000 = 4600; вафли без техкарты — неизвестна, маржа только по печенью.
        Assert.Equal(4600, r.Products.Single(p => p.Id == Cookies).CostPerKg);
        Assert.Null(r.Products.Single(p => p.Id == Wafers).CostPerKg);
        Assert.Equal(30_000_000 - 1500 * 4600, Base(r, "finance.gross_profit"));
    }

    [Fact]
    public void Price_and_discount_change_revenue_by_the_net_price_formula_and_keep_volume()
    {
        var r = Run(P("sales.price", 10), P("sales.discount", 5));

        // Цена нетто = цена × (1 + 10 %) × (1 − (скидка + 5 п.п.)) ÷ (1 − скидка): печенье (скидка 10 %) — 1,1 × 0,85 ÷ 0,9, вафли (0 %) — 1,1 × 0,95.
        var cookies = 30_000_000m * 1.1m * 0.85m / 0.9m;
        var wafers = 9_000_000m * 1.1m * 0.95m;
        Assert.Equal(Math.Round(cookies + wafers, 0), Scen(r, "sales.revenue"));
        Assert.Equal(1800, Scen(r, "sales.kg"));
        Assert.Contains(r.Risks, x => x.Code == "elasticity");
        Assert.Contains("sales.revenue", r.Chain);
        Assert.DoesNotContain("production.required", r.Chain);
    }

    [Fact]
    public void Elasticity_turns_price_into_volume_explicitly()
    {
        var r = Run(P("sales.price", 10), P("sales.elasticity", -1.5m));

        // Объём × (1 + эластичность × изменение цены) = × (1 − 0,15).
        Assert.Equal(1800 * 0.85m, Scen(r, "sales.requested_kg"));
        Assert.DoesNotContain(r.Risks, x => x.Code == "elasticity");
    }

    [Fact]
    public void Demand_growth_flows_through_production_materials_and_purchases()
    {
        var r = Run(P("sales.demand", 20));

        // Спрос +20 %: печенье 1800 кг → сахар по техкарте 1800 ÷ 30 × 0,2 = 12 кг/день (база — max(факт 5, техкарта 10) = 10).
        Assert.Equal(1800, Scen(r, "production.required"));
        var sugar = r.Materials.Single(m => m.ItemId == Sugar);
        Assert.Equal(10, sugar.BaseDaily);
        Assert.Equal(12, sugar.Daily);
        Assert.True(Scen(r, "materials.need_value") > Base(r, "materials.need_value"));
        Assert.True(Scen(r, "procurement.budget") > Base(r, "procurement.budget"));
        Assert.Equal(["sales.requested", "production.required", "production.planned", "materials.need"],
            r.Chain.Take(4).ToArray());
    }

    [Fact]
    public void Capacity_limits_sales_and_demand_growth_is_not_revenue_growth()
    {
        var r = Run(P("sales.demand", 100));

        // Нужно 3000 кг печенья, мощность 1800 кг: выпуск урезан до 60 %, вафли (без техкарты) не ограничены.
        Assert.Equal(3000, Scen(r, "production.required"));
        Assert.Equal(166.7m, Scen(r, "production.utilization"));
        var cookies = r.Products.Single(p => p.Id == Cookies);
        Assert.Equal(3000, cookies.RequestedKg);
        Assert.True(cookies.SoldKg <= 1800);
        Assert.Equal("мощность производства", cookies.Limit);
        Assert.Contains(r.Constraints, c => c.Code == "capacity" && c.Severity == "critical");
        Assert.True(Scen(r, "sales.revenue") < 2 * Base(r, "sales.revenue"));
        Assert.True(Scen(r, "sales.lost_revenue") > 0);
        Assert.Contains(r.Constraints, c => c.Code == "demand_not_revenue");
        Assert.Contains(r.Conclusion.Recommendations, x => x.Contains("мощность нужно увеличить", StringComparison.Ordinal));
    }

    [Fact]
    public void Supplier_delay_creates_a_shortage_with_exact_lost_days()
    {
        var r = Run(P("materials.lead_time", 7));

        // Сахар: 120 кг, 10 кг/день — хватает ровно на 12 дней; закупка приходит через 7 + 7 = 14 дней → 13-й день без сахара.
        var sugar = r.Materials.Single(m => m.ItemId == Sugar);
        Assert.Equal(14, sugar.LeadTimeDays);
        Assert.Equal(1, sugar.LostDays);
        Assert.Equal(AsOf.AddDays(13), sugar.ShortageDate);
        Assert.Equal(Math.Round(1 - 1m / 30, 4), sugar.Coverage);
        Assert.Contains(r.Constraints, c => c.Code == "material_shortage" && c.Target == Sugar.ToString());
        // Печенье теряет 1/30 выпуска; поставка муки (день 5 → 12) дефицита не даёт.
        Assert.Equal(Math.Round(1500 * (1 - 1m / 30), 1), r.Products.Single(p => p.Id == Cookies).SoldKg);
        Assert.Equal(0, r.Materials.Single(m => m.ItemId == Flour).LostDays);
        Assert.Contains(r.Conclusion.Recommendations, x => x.Contains("Заказать «Сахар» сегодня", StringComparison.Ordinal));
    }

    [Fact]
    public void Raw_material_price_changes_cost_and_budget_but_not_sales()
    {
        var r = Run(P("materials.price", 15));

        Assert.Equal(Base(r, "sales.revenue"), Scen(r, "sales.revenue"));
        Assert.Equal(4600 * 1.15m, r.Products.Single(p => p.Id == Cookies).CostPerKg);
        Assert.Equal(Math.Round(Base(r, "procurement.budget") * 1.15m, 0), Scen(r, "procurement.budget"));
        Assert.Equal(30_000_000 - 1500 * 4600 * 1.15m, Scen(r, "finance.gross_profit"));
        Assert.Contains(r.Risks, x => x.Code == "margin");
    }

    [Fact]
    public void Supplier_switch_uses_that_suppliers_price_and_lead_time()
    {
        var r = Run(P("procurement.supplier", 0, $"{Flour}/s2"));

        var flour = r.Materials.Single(m => m.ItemId == Flour);
        Assert.Equal(4500, flour.Price);
        Assert.Equal(20, flour.LeadTimeDays);
        Assert.Equal("Агро", flour.Supplier);
        Assert.Equal(0.6m * 4500 + 0.2m * 8000, r.Products.Single(p => p.Id == Cookies).CostPerKg);
    }

    /// <summary>
    /// Сценарий из ТЗ: продажи +30 %, сырьё +15 %, мощность +20 %, поставки задерживаются на 7 дней. Каждое число пересчитывается вручную.
    /// </summary>
    [Fact]
    public void Combined_sales_up_30_materials_up_15_capacity_up_20_delay_7_days()
    {
        var r = Run(P("sales.demand", 30), P("materials.price", 15), P("production.capacity", 20), P("materials.lead_time", 7));
        Assert.True(r.Valid);

        // Мощность: 60 × 1,2 × 30 = 2160 кг; нужно печенья 1500 × 1,3 = 1950 кг — загрузка 90,3 %, мощности хватает.
        Assert.Equal(1950, Scen(r, "production.required"));
        Assert.Equal(2160, Scen(r, "production.capacity"));
        Assert.Equal(90.3m, Scen(r, "production.utilization"));

        // Сахар: база max(5, 10) = 10 кг/день + рост по техкарте (1950 − 1500) ÷ 30 × 0,2 = 3 → 13 кг/день. 120 кг хватает на 9 дней
        // (остаётся 3 кг), закупка приходит на 14-й день (7 + 7): день 10 — без (13 − 3) ÷ 13, дни 11–13 — целиком.
        var sugar = r.Materials.Single(m => m.ItemId == Sugar);
        Assert.Equal(13, sugar.Daily);
        Assert.Equal(14, sugar.LeadTimeDays);
        var lost = (13m - 3) / 13 + 3;
        Assert.Equal(Math.Round(lost, 2), sugar.LostDays);
        Assert.Equal(AsOf.AddDays(10), sugar.ShortageDate);
        var coverage = 1 - lost / 30;
        Assert.Equal(Math.Round(coverage, 4), sugar.Coverage);

        // Закупка сахара: спрос 13 × 30 + страховой запас max(1,645 × σ 1 × 13/10 × √14, 0) − доступно 120.
        var safety = SupplyPlanMath.SafetyStock(1.3, 14, 0);
        Assert.Equal(SupplyPlanMath.RoundUp(390 + safety - 120, null, "кг"), sugar.Purchase);
        Assert.Equal(8000 * 1.15m, sugar.Price);

        // Мука: 39 кг/день, поставка 500 кг сдвинута с 5-го на 12-й день: 1000 кг хватает на 25 дней — дефицита нет.
        var flour = r.Materials.Single(m => m.ItemId == Flour);
        Assert.Equal(39, flour.Daily);
        Assert.Equal(0, flour.LostDays);

        // Печенье: выпуск = 1950 × обеспечение сахаром; продано столько же; вафли (без техкарты) +30 % без ограничений.
        var cookies = r.Products.Single(p => p.Id == Cookies);
        var produced = 1950 * coverage;
        Assert.Equal(Math.Round(produced, 1), cookies.ProducedKg);
        Assert.Equal(Math.Round(produced, 1), cookies.SoldKg);
        Assert.Equal("сырьё «Сахар»", cookies.Limit);
        var revenue = 30_000_000m * 1.3m * coverage + 9_000_000m * 1.3m;
        Assert.Equal(Math.Round(revenue, 0), Scen(r, "sales.revenue"));
        Assert.Equal(Math.Round(30_000_000m * 1.3m * (1 - coverage), 0), Scen(r, "sales.lost_revenue"));

        // Себестоимость кг печенья: (0,6 × 5000 + 0,2 × 8000) × 1,15 = 5290; валовая прибыль — по печенью (себестоимость вафель неизвестна).
        Assert.Equal(5290, cookies.CostPerKg);
        Assert.Equal(Math.Round(30_000_000m * 1.3m * coverage - produced * 5290, 0), Scen(r, "finance.gross_profit"));

        Assert.Contains(r.Constraints, c => c.Code == "material_shortage");
        Assert.Contains(r.Constraints, c => c.Code == "capacity_high");
        Assert.Contains(r.Constraints, c => c.Code == "demand_not_revenue");
        Assert.Contains("Главное ограничение — дефицит сырья: сахар", r.Conclusion.Headline, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Customer_terms_shift_cash_inflows()
    {
        var r = Run(P("finance.customer_terms", 10));

        // Поступления: продажи до сценария платят через 3 дня (дни 1–3), продажи сценария — через 13 дней (дни 14–30): 90 % × 1,3 млн/день.
        var daily = 39_000_000m / 30;
        Assert.Equal(Math.Round(0.9m * daily * 30, 0), Base(r, "finance.cash_in"));
        Assert.Equal(Math.Round(0.9m * daily * (3 + 17), 0), Scen(r, "finance.cash_in"));
        Assert.True(Scen(r, "finance.working_capital") > Base(r, "finance.working_capital"));
    }

    [Fact]
    public void Opening_cash_and_credit_limit_turn_flow_into_gaps_and_limits()
    {
        var r = Run(P("input.cash", 0), P("input.credit_limit", 1_000_000), P("input.salary", 5_000_000), P("input.opex", 80_000_000),
            P("finance.customer_terms", 20));

        Assert.True(Scen(r, "finance.gap_days") > 0);
        Assert.True(Scen(r, "finance.financing_need") > 1_000_000);
        Assert.Contains(r.Constraints, c => c.Code == "credit_limit");
        Assert.Contains(r.Constraints, c => c.Code == "cash_gap");
        // ФОТ = 100 чел. × 5 млн × 30/30.
        Assert.Equal(500_000_000, Base(r, "finance.payroll"));
    }

    [Fact]
    public void Workforce_headcount_cut_in_production_limits_capacity()
    {
        var r = Run(P("input.production_division", 0, "40"), P("workforce.headcount", -50, "40"), P("input.salary", 5_000_000));

        // Производство −50 % людей: мощность 1800 × 0,5 = 900 кг при потребности 1500.
        Assert.Equal(900, Scen(r, "production.capacity"));
        Assert.Contains(r.Constraints, c => c.Code == "labour");
        // Численность: 100 − 40 × 0,5 = 80; ФОТ — 80 × 5 млн.
        Assert.Equal(80, Scen(r, "workforce.headcount"));
        Assert.Equal(400_000_000, Scen(r, "finance.payroll"));
    }

    [Fact]
    public void Headcount_of_another_division_does_not_change_capacity()
    {
        var r = Run(P("input.production_division", 0, "40"), P("workforce.headcount", -50, "60"));

        Assert.Equal(Base(r, "production.capacity"), Scen(r, "production.capacity"));
        Assert.Equal(70, Scen(r, "workforce.headcount"));
    }

    [Fact]
    public void Regional_expansion_uses_the_reference_region_mix_and_productivity()
    {
        var r = ScenarioEngine.Run(Baseline(), new ScenarioDefinition(ScenarioKinds.Expansion, 30,
        [
            P("expansion.plan", 650), P("expansion.reference", 0, "r1"), P("expansion.staff", 4), P("expansion.opex", 10_000_000),
            P("expansion.logistics", 500), P("expansion.investment", 50_000_000), P("input.salary", 5_000_000), P("input.capacity", 100),
        ], "Наманган"));

        // Образец «Ташкент»: печенье 1000 кг по 20 000, вафли 300 кг по 30 000 → 650 кг = 500 кг печенья + 150 кг вафель.
        var region = r.Regions.Single(x => x.New);
        Assert.Equal("Наманган", region.Name);
        Assert.Equal(650, region.Kg);
        Assert.Equal(500 * 20_000 + 150 * 30_000, region.Revenue);
        // Продаж на агента у образца: 1300 кг за 30 дней ÷ 5 агентов = 260 кг/мес → на 650 кг нужно 3 человека.
        Assert.Equal(3, r.Expansion!.StaffNeed);
        Assert.Equal(4 * 5_000_000 + 10_000_000, r.Expansion.FixedMonth);
        Assert.Null(r.Expansion.ProfitMonth); // себестоимость вафель неизвестна
        Assert.Contains(r.Expansion.Notes, n => n.Contains("Себестоимость части товаров", StringComparison.Ordinal));
        Assert.Equal(104, Scen(r, "workforce.headcount"));
    }

    [Fact]
    public void Expansion_break_even_and_payback_with_known_cost()
    {
        var r = ScenarioEngine.Run(Baseline(), new ScenarioDefinition(ScenarioKinds.Expansion, 30,
        [
            P("expansion.plan", 650), P("expansion.reference", 0, "r1"), P("expansion.staff", 1), P("expansion.opex", 2_000_000),
            P("expansion.logistics", 500), P("expansion.investment", 50_000_000), P("input.salary", 5_000_000), P("input.cost_per_kg", 20_000),
            P("input.capacity", 100),
        ], "Наманган"));

        // Себестоимость кг: печенье 4600 (техкарта), вафли 20 000 (введено); по составу образца: (1000 × 4600 + 300 × 20 000) ÷ 1300.
        var cost = (1000m * 4600 + 300 * 20_000) / 1300;
        var price = (500m * 20_000 + 150 * 30_000) / 650;
        var fixedMonth = 1 * 5_000_000m + 2_000_000;
        var profit = 650 * price - 650 * (cost + 500) - fixedMonth;
        Assert.Equal(Math.Round(profit, 0), r.Expansion!.ProfitMonth);
        Assert.Equal(Math.Round(fixedMonth / (price - cost - 500), 0), r.Expansion.BreakEvenKgMonth);
        Assert.Equal(Math.Round(50_000_000 / profit, 1), r.Expansion.PaybackMonths);
    }

    [Fact]
    public void Revenue_override_together_with_sales_drivers_is_double_counting()
    {
        var r = Run(P("finance.revenue", 10), P("sales.demand", 10));

        Assert.False(r.Valid);
        Assert.Contains(r.Issues, i => i.Code == "double_count" && i.Severity == ScenarioIssue.Error);
        Assert.Empty(r.Metrics);
    }

    [Fact]
    public void Revenue_override_alone_scales_revenue_only()
    {
        var r = Run(P("finance.revenue", 10));

        Assert.Equal(Math.Round(39_000_000m * 1.1m, 0), Scen(r, "sales.revenue"));
        Assert.Equal(1800, Scen(r, "sales.kg"));
    }

    [Theory]
    [InlineData("sales.unknown", 1, null, "unknown")]
    [InlineData("sales.demand", 500, null, "range")]
    [InlineData("production.shifts", 4, null, "range")]
    [InlineData("production.shifts", 1.5, null, "range")]
    [InlineData("sales.region", 10, null, "target")]
    [InlineData("sales.region", 10, "nowhere", "target")]
    [InlineData("sales.demand", 10, "r1", "target")]
    [InlineData("procurement.supplier", 0, "00000000-0000-0000-0000-00000000f100/nobody", "target")]
    public void Invalid_parameters_are_rejected(string code, double value, string? target, string issue)
    {
        var r = Run(P(code, (decimal)value, target));

        Assert.False(r.Valid);
        Assert.Contains(r.Issues, i => i.Code == issue && i.Severity == ScenarioIssue.Error);
    }

    [Fact]
    public void Horizon_and_complexity_are_limited()
    {
        Assert.Contains(Run(Baseline(), 400).Issues, i => i.Code == "horizon");
        var many = Enumerable.Range(0, ScenarioCatalog.MaxParameters + 1).Select(i => P("sales.demand", 1)).ToArray();
        Assert.Contains(Run(Baseline(), 30, many).Issues, i => i.Code == "complexity");
    }

    [Fact]
    public void Same_input_gives_identical_result_even_after_snapshot_round_trip()
    {
        var parameters = new[] { P("sales.demand", 30), P("materials.price", 15), P("production.capacity", 20), P("materials.lead_time", 7) };
        var first = SimulationJson.Write(Run(Baseline(), 30, parameters));
        var stored = SimulationJson.Read<ScenarioBaselineData>(SimulationJson.Write(Baseline()));
        var second = SimulationJson.Write(Run(stored, 30, parameters));

        // Снимок хранит только данные — id техкарты новый у каждого Baseline(), поэтому сравнивается результат, а не снимок.
        Assert.Equal(first, second);
    }

    [Fact]
    public void Each_parameter_changes_only_metrics_reachable_in_the_dependency_graph()
    {
        var samples = new Dictionary<string, ScenarioParameter[]>
        {
            ["sales.demand"] = [P("sales.demand", 20)],
            ["sales.region"] = [P("sales.region", 20, "r2")],
            ["sales.product"] = [P("sales.product", 20, Cookies.ToString())],
            ["sales.price"] = [P("sales.price", 10)],
            ["sales.discount"] = [P("sales.discount", 5)],
            ["sales.elasticity"] = [P("sales.price", 10), P("sales.elasticity", -1)],
            ["sales.plan"] = [P("sales.plan", 10)],
            ["production.volume"] = [P("production.volume", 10)],
            ["production.capacity"] = [P("production.capacity", -30)],
            ["production.utilization"] = [P("production.utilization", 70)],
            ["production.shifts"] = [P("production.shifts", 1)],
            ["production.defect"] = [P("production.defect", 5)],
            ["production.norms"] = [P("production.norms", 10)],
            ["production.norm_item"] = [P("production.norm_item", 10, Sugar.ToString())],
            ["materials.price"] = [P("materials.price", 10)],
            ["materials.price_item"] = [P("materials.price_item", 10, Flour.ToString())],
            ["materials.stock"] = [P("materials.stock", -50)],
            ["materials.stock_item"] = [P("materials.stock_item", -50, Sugar.ToString())],
            ["materials.loss"] = [P("materials.loss", 5)],
            ["materials.lead_time"] = [P("materials.lead_time", 7)],
            ["procurement.supplier"] = [P("procurement.supplier", 0, $"{Flour}/s2")],
            ["procurement.min_lot"] = [P("procurement.min_lot", 1000, Sugar.ToString())],
            ["procurement.volume"] = [P("procurement.volume", 50)],
            ["procurement.safety"] = [P("procurement.safety", 100)],
            ["procurement.supplier_terms"] = [P("procurement.supplier_terms", 10)],
            ["finance.revenue"] = [P("finance.revenue", 10)],
            ["finance.expenses"] = [P("input.opex", 100_000_000), P("finance.expenses", 10)],
            ["finance.cost"] = [P("input.cost_per_kg", 10_000), P("finance.cost", 10)],
            ["finance.customer_terms"] = [P("finance.customer_terms", 10)],
            ["workforce.headcount"] = [P("input.salary", 5_000_000), P("workforce.headcount", 10, "60")],
            ["workforce.salary"] = [P("input.salary", 5_000_000), P("workforce.salary", 10)],
            ["workforce.productivity"] = [P("workforce.productivity", -20)],
            ["expansion.plan"] = [P("expansion.plan", 300)],
        };

        foreach (var (code, parameters) in samples)
        {
            var r = Run(parameters);
            Assert.True(r.Valid, code);
            var node = ScenarioCatalog.Find(code)!.Node;
            var affected = DependencyGraph.Affected([node]).ToHashSet();
            foreach (var m in r.Metrics.Where(m => m.Base != m.Scenario))
            {
                Assert.True(affected.Contains(m.Node), $"«{code}» изменил «{m.Code}» (узел {m.Node}), которого нет в графе зависимостей от {node}.");
            }

            Assert.True(r.Metrics.Any(m => m.Base != m.Scenario) || code is "procurement.supplier_terms", $"«{code}» ничего не изменил.");
        }
    }

    [Fact]
    public void Dependency_graph_has_no_cycles_and_every_parameter_has_a_node()
    {
        var order = DependencyGraph.Order();
        Assert.Equal(DependencyGraph.Nodes.Count, order.Count);
        Assert.All(ScenarioCatalog.Parameters, p => Assert.NotNull(DependencyGraph.Find(p.Node)));
        Assert.True(order.ToList().IndexOf("sales.requested") < order.ToList().IndexOf("materials.need"));
        Assert.True(order.ToList().IndexOf("materials.need") < order.ToList().IndexOf("finance.cash"));
    }

    [Fact]
    public void What_if_questions_go_to_the_scenario_analyst_but_company_reviews_do_not()
    {
        static AgentConfig Agent(string code) =>
            new(code, code, null, null, "", true, null, null, null, true, 0, null, Permissions.AnalytikUse, [AnalytikTools.Source]);
        var available = new[] { "analyst.sales", "analyst.supply", AnalytikAgents.Scenario, AnalytikAgents.Bi }.Select(Agent).ToList();
        var failed = new RoutingDecision(available.Select(a => a.Code).ToList(), new Dictionary<string, string>(), "", true, AiTokenUsage.None);
        ConsultantTurn Turn(string q) => new(Guid.NewGuid(), Guid.NewGuid(), q, []);

        Assert.Contains(AnalytikAgents.Scenario,
            AnalytikEngine.Enforce(failed, Turn("Что произойдёт, если продажи вырастут на 25%, а сырьё подорожает на 10%? Что будет с прибылью?"), available).Agents);
        Assert.Contains(AnalytikAgents.Scenario, AnalytikEngine.Enforce(failed, Turn("Посчитай сценарий: мощность +20 %"), available).Agents);
        Assert.DoesNotContain(AnalytikAgents.Scenario, AnalytikEngine.Enforce(failed, Turn("Проанализируй всю компанию за месяц"), available).Agents);
        Assert.Contains(AgentDefaults.All, a => a.Code == AnalytikAgents.Scenario);
    }

    [Fact]
    public void Missing_blocks_ignore_their_parameters_with_a_warning()
    {
        var b = Baseline() with { Materials = null, Production = null };
        var r = Run(b, 30, P("materials.price", 10));

        Assert.True(r.Valid);
        Assert.Contains(r.Issues, i => i.Code == "no_data" && i.Parameter == "materials.price");
        Assert.Null(r.Metric("procurement.budget"));
    }

    [Fact]
    public void Without_recipes_the_headline_says_what_was_not_checked_instead_of_feasible()
    {
        var b = Baseline();
        b = b with { Sales = b.Sales! with { Products = b.Sales.Products.Select(p => p with { Norms = [], RecipeId = null }).ToList() } };
        var r = Run(b, 30, P("sales.demand", 30));

        Assert.DoesNotContain("выполним", r.Conclusion.Headline, StringComparison.Ordinal);
        Assert.Contains("производство и сырьё не проверены", r.Conclusion.Headline, StringComparison.Ordinal);
        Assert.Equal(Math.Round(39_000_000m * 1.3m, 0), Scen(r, "sales.revenue"));
    }

    [Fact]
    public void Unknown_capacity_is_said_not_assumed()
    {
        var b = Baseline(production: new BaselineProduction(null, null, null, null, null, 0, 0, 90, "смен нет", []));
        var r = Run(b, 30, P("production.capacity", 20));

        Assert.Contains(r.Issues, i => i.Code == "capacity_unknown");
        Assert.Null(r.Metric("production.capacity")!.Scenario);
        Assert.Contains(r.Constraints, c => c.Code == "capacity_unknown");
        // С введённой мощностью — проверяется.
        var withInput = Run(b, 30, P("input.capacity", 40), P("production.capacity", 20));
        Assert.Equal(40 * 1.2m * 30, Scen(withInput, "production.capacity"));
        Assert.Equal("input", withInput.Metric("production.capacity")!.Kind);
    }
}
