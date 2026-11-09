using OneBase.Application.Production;
using OneBase.Domain.Production;

namespace OneBase.AI.Tests;

/// <summary>
/// Расчётный механизм производства (ожидаемые числа посчитаны вручную): выпечка 280 → 245 кг; замесы; брак до и после глазури; брак на входе
/// этапа; упаковка и брак упаковки без брака продукции; 5 000 кг годной продукции; переработка; прежняя формула без этапов; единицы;
/// ошибки карты вместо отрицательных количеств и деления на ноль; неполный расчёт без подстановки нулей.
/// </summary>
public class ProductionCalcTests
{
    private static readonly Guid Flour = Guid.NewGuid(), Sugar = Guid.NewGuid(), Butter = Guid.NewGuid(), Other = Guid.NewGuid(), Filling = Guid.NewGuid(),
        Glaze = Guid.NewGuid(), Nuts = Guid.NewGuid(), Label = Guid.NewGuid(), Film = Guid.NewGuid(), Box = Guid.NewGuid();

    /// <summary>
    /// Печенье глазированное на 1 000 кг: замес 280 кг (мука 150, сахар 60, масло 40, прочее 30) → выпечка (выход 87,5 %, брак bakeDefect %) →
    /// начинка 200 кг → глазурь 100 кг и орехи 50 кг → упаковка: этикетка на упаковку (+0,5 %), плёнка 4 г на упаковку (+2 %), коробка на 20
    /// упаковок по 0,25 кг. Масса потока на 1 кг готовой: после глазури 1; до глазури 0,85; до начинки 0,65; тесто 0,65 ÷ 0,875 = 0,742857…
    /// </summary>
    private static CalcRecipe Cookie(decimal bakeDefect = 0, ProductionDefectBase packBase = ProductionDefectBase.Output, bool partial = false, decimal? min = null,
        decimal? max = null) => new(1000, "кг",
    [
        new(1, "Замес", ProductionStageKind.Mixing, 100, 0, ProductionDefectBase.Output),
        new(2, "Выпечка", ProductionStageKind.Baking, 87.5m, bakeDefect, ProductionDefectBase.Output),
        new(3, "Начинка", ProductionStageKind.Filling, 100, 0, ProductionDefectBase.Output),
        new(4, "Глазурь", ProductionStageKind.Glazing, 100, 0, ProductionDefectBase.Output),
        new(5, "Упаковка", ProductionStageKind.Packing, 100, 0, packBase),
    ],
    [
        Line(Flour, "Мука", 1, ProductionLineBasis.Batch, 150),
        Line(Sugar, "Сахар", 1, ProductionLineBasis.Batch, 60),
        Line(Butter, "Масло", 1, ProductionLineBasis.Batch, 40),
        Line(Other, "Прочее", 1, ProductionLineBasis.Batch, 30),
        Line(Filling, "Начинка", 3, ProductionLineBasis.Output, 200),
        Line(Glaze, "Глазурь", 4, ProductionLineBasis.Output, 100),
        Line(Nuts, "Орехи", 4, ProductionLineBasis.Output, 50),
        Line(Label, "Этикетка", 5, ProductionLineBasis.Pack, 1, 0.5m, "шт"),
        Line(Film, "Плёнка", 5, ProductionLineBasis.Pack, 0.004m, 2),
        Line(Box, "Коробка", 5, ProductionLineBasis.Box, 1, 0, "шт"),
    ], min, max, partial, PackKg: 0.25m, PacksPerBox: 20);

    private static CalcLine Line(Guid item, string name, int? stage, ProductionLineBasis basis, decimal qty, decimal loss = 0, string unit = "кг", string? itemUnit = null,
        decimal? unitKg = null) => new(item, item, name, unit, itemUnit ?? unit, stage, basis, qty, loss, unitKg);

    private static Dictionary<int, decimal> Defects(params (int Stage, decimal Kg)[] defects)
    {
        var all = Enumerable.Range(1, 5).ToDictionary(i => i, _ => 0m);
        foreach (var (stage, kg) in defects)
        {
            all[stage] = kg;
        }

        return all;
    }

    private static CalcLineResult L(CalcResult r, Guid item) => r.Lines.Single(l => l.ItemId == item);

    [Fact]
    public void Defect_content_holds_only_materials_added_up_to_its_stage_so_a_defect_before_glazing_has_no_glaze()
    {
        static decimal Q(IReadOnlyList<CalcItemTotal> content, Guid item) => decimal.Round(content.SingleOrDefault(c => c.ItemId == item)?.Quantity ?? 0, 6);

        // Брак выпечки: в кг — 1 ÷ 0,875 = 1,142857 кг теста (мука 150 из 280 — 0,612245); начинки, глазури, орехов и упаковки нет.
        var baked = ProductionCalc.DefectContent(Cookie(), 2)!;
        Assert.Equal((0.612245m, 0.244898m, 0.163265m, 0.122449m), (Q(baked, Flour), Q(baked, Sugar), Q(baked, Butter), Q(baked, Other)));
        Assert.DoesNotContain(baked, c => c.ItemId == Filling || c.ItemId == Glaze || c.ItemId == Nuts || c.ItemId == Label || c.ItemId == Film || c.ItemId == Box);

        // После глазури: тесто 0,742857 кг (мука 0,397959), начинка 0,2, глазурь 0,1, орехи 0,05 — на кг; упаковки ещё нет.
        var glazed = ProductionCalc.DefectContent(Cookie(), 4)!;
        Assert.Equal((0.397959m, 0.2m, 0.1m, 0.05m), (Q(glazed, Flour), Q(glazed, Filling), Q(glazed, Glaze), Q(glazed, Nuts)));
        Assert.DoesNotContain(glazed, c => c.ItemId == Label);

        // Упаковка: ещё этикетки 4 × 1,005 = 4,02 шт, плёнка 0,016 × 1,02 = 0,01632 кг, коробки 0,2 — на кг; брак на входе упаковки — без упаковки.
        var packed = ProductionCalc.DefectContent(Cookie(), 5)!;
        Assert.Equal((4.02m, 0.01632m, 0.2m, 0.397959m), (Q(packed, Label), Q(packed, Film), Q(packed, Box), Q(packed, Flour)));
        var atInput = ProductionCalc.DefectContent(Cookie(packBase: ProductionDefectBase.Input), 5)!;
        Assert.DoesNotContain(atInput, c => c.ItemId == Label || c.ItemId == Film || c.ItemId == Box);
        Assert.Equal((0.2m, 0.1m), (Q(atInput, Filling), Q(atInput, Glaze)));

        Assert.Null(ProductionCalc.DefectContent(Cookie(), 9)); // такого этапа нет
    }

    [Fact]
    public void Baking_280_to_245_kg_loses_35_kg_12_5_percent_and_flags_a_yield_outside_the_tolerance()
    {
        var bake = ProductionCalc.Bake(280, 245, 87.5m);
        Assert.Equal(35m, bake.LossKg);
        Assert.Equal(12.5m, bake.LossPercent);
        Assert.Equal(87.5m, bake.YieldPercent);
        Assert.Equal(0m, bake.DeviationPoints);
        Assert.False(bake.OutOfRange);

        var low = ProductionCalc.Bake(280, 238, 87.5m, 86, 89); // 85 % — ниже допуска 86–89
        Assert.Equal(42m, low.LossKg);
        Assert.Equal(15m, low.LossPercent);
        Assert.Equal(-2.5m, low.DeviationPoints);
        Assert.True(low.OutOfRange);
        Assert.False(ProductionCalc.Bake(280, 246.4m, 87.5m, 86, 89).OutOfRange); // 88 %
        Assert.Throws<ArgumentOutOfRangeException>(() => ProductionCalc.Bake(0, 10, 87.5m)); // деления на ноль нет
    }

    [Fact]
    public void Batches_prefer_standard_ones_spread_up_to_the_maximum_and_take_a_partial_only_when_allowed()
    {
        // 2 800 кг теста по 280 — ровно 10 замесов.
        var exact = ProductionCalc.Batches(2800, 280, 280, 280, false);
        Assert.Equal(10, exact.Batches.Count);
        Assert.Equal(0m, exact.SurplusKg);

        // 3 790,087 кг: частичный не разрешён — 14 стандартных, излишек 129,913 кг.
        var whole = ProductionCalc.Batches(3790.087m, 280, 280, 280, false);
        Assert.Equal(14, whole.Batches.Count);
        Assert.Equal(3920m, whole.TotalKg);
        Assert.Equal(129.913m, whole.SurplusKg);

        // Частичный разрешён (мин. 100): 13 стандартных и один 150,087 кг — без излишка.
        var partial = ProductionCalc.Batches(3790.087m, 280, 100, 280, true);
        Assert.Equal(14, partial.Batches.Count);
        Assert.Equal(13, partial.Batches.Count(b => !b.Partial));
        Assert.Equal(150.087m, partial.Batches[^1].MassKg);
        Assert.Equal(0m, partial.SurplusKg);

        // Максимум 300: остаток 150,087 раскладывается по 13 замесам (по 291,545 кг) — частичный не нужен.
        var spread = ProductionCalc.Batches(3790.087m, 280, 280, 300, false);
        Assert.Equal(13, spread.Batches.Count);
        Assert.All(spread.Batches, b => Assert.InRange(b.MassKg, 280m, 300m));
        Assert.Equal(3790.087m, spread.TotalKg);

        // Остаток меньше минимума: поровну на замес больше, если каждый не меньше минимума, иначе — минимальный с излишком.
        Assert.Equal([145m, 145m], ProductionCalc.Batches(290, 280, 50, 280, true).Batches.Select(b => b.MassKg));
        var min = ProductionCalc.Batches(300, 280, 200, 280, true);
        Assert.Equal([280m, 200m], min.Batches.Select(b => b.MassKg));
        Assert.Equal(180m, min.SurplusKg);
        Assert.Equal([50m], ProductionCalc.Batches(30, 280, 50, 280, true).Batches.Select(b => b.MassKg));
        Assert.Empty(ProductionCalc.Batches(0, 280, 280, 280, false).Batches);
        Assert.Throws<ArgumentOutOfRangeException>(() => ProductionCalc.Batches(100, 0, 0, 0, false));
    }

    [Fact]
    public void Plan_for_5000_kg_goes_through_every_stage_yield_and_defect_norm()
    {
        var r = ProductionCalc.Plan(Cookie(bakeDefect: 2), 5000);
        Assert.True(r.Complete, string.Join("; ", r.Problems));

        // Брак выпечки 2 % от обработанного: выпечь 5 000 ÷ 0,98 = 5 102,041 кг-эквивалента; тесто 5 102,0408 × 0,742857 = 3 790,087 кг.
        var bake = r.Stages.Single(s => s.No == 2);
        Assert.Equal(5102.041m, bake.Processed);
        Assert.Equal(102.041m, bake.Rejected);
        Assert.Equal(3790.087m, bake.InputKg);
        Assert.Equal(3316.327m, bake.OutputKg); // × 0,65
        Assert.Equal(66.327m, bake.DefectKg);
        Assert.Equal(473.761m, bake.LossKg); // 3 790,087 − 3 316,327 — влага
        Assert.Equal(3250m, bake.NextKg); // 5 000 × 0,65 — ровно вход начинки
        Assert.Equal(bake.NextKg, r.Stages.Single(s => s.No == 3).InputKg - r.Stages.Single(s => s.No == 3).AddedKg);
        Assert.Equal(3790.087m, r.Batches!.NeedKg);
        Assert.Equal(14, r.Batches.Batches.Count);
        Assert.Equal(2100m, L(r, Flour).Gross); // 14 × 150
        Assert.Equal(840m, L(r, Sugar).Gross);
        Assert.Equal(560m, L(r, Butter).Gross);
        Assert.Equal(420m, L(r, Other).Gross);

        // После выпечки брака нет: начинка, глазурь и орехи — на 5 000 кг.
        Assert.Equal(1000m, L(r, Filling).Gross);
        Assert.Equal(500m, L(r, Glaze).Gross);
        Assert.Equal(250m, L(r, Nuts).Gross);

        // Упаковки — 5 000 ÷ 0,25 = 20 000: этикетки +0,5 % = 20 100, плёнка 80 кг +2 % = 81,6 кг, коробки 1 000.
        Assert.Equal(20000m, L(r, Label).Base);
        Assert.Equal(20100m, L(r, Label).Gross);
        Assert.Equal(81.6m, L(r, Film).Gross);
        Assert.Equal(1000m, L(r, Box).Gross);
    }

    [Fact]
    public void Defect_before_glazing_uses_dough_baking_and_filling_but_no_glaze()
    {
        // По результатам: 1 000 кг годной, брак 50 кг после начинки (до глазури).
        var r = ProductionCalc.ByResults(Cookie(), new CalcActual(1000, Defects((3, 50))));
        Assert.Empty(r.Problems);

        // Глазурь и орехи — только на дошедшее до глазури: 1 000 кг.
        Assert.Equal(100m, L(r, Glaze).Gross);
        Assert.Equal(50m, L(r, Nuts).Gross);

        // 50 кг брака с начинкой = 58,8235 кг-эквивалента готовой (÷ 0,85): начинки 200 × 1,0588 = 211,765 кг (11,765 — в браке).
        Assert.Equal(211.765m, L(r, Filling).Gross);
        var fill = r.Stages.Single(s => s.No == 3);
        Assert.Equal(50m, fill.DefectKg);
        Assert.Equal(1058.824m, fill.Processed);

        // Тесто: 1 058,8235 × 0,742857 = 786,555 кг против 742,857 без брака — основа брака тоже израсходована.
        Assert.Equal(786.555m, r.Batches!.NeedKg);
        Assert.Equal(742.857m, ProductionCalc.ByResults(Cookie(), new CalcActual(1000, Defects())).Batches!.NeedKg);
    }

    [Fact]
    public void Defect_after_glazing_uses_every_earlier_material_and_defect_at_the_packing_input_uses_no_packaging()
    {
        // Брак 50 кг после глазури: глазурь 105, орехи 52,5, начинка 210; упаковка — только на 1 000 кг (4 000 упаковок).
        var after = ProductionCalc.ByResults(Cookie(), new CalcActual(1000, Defects((4, 50))));
        Assert.Equal(105m, L(after, Glaze).Gross);
        Assert.Equal(52.5m, L(after, Nuts).Gross);
        Assert.Equal(210m, L(after, Filling).Gross);
        Assert.Equal(4000m, L(after, Label).Base);
        Assert.Equal(780m, after.Batches!.NeedKg); // 1 050 × 0,65 ÷ 0,875

        // Брак 50 кг отсеян на входе упаковки: упаковка на него не потрачена, глазурь — потрачена.
        var input = ProductionCalc.ByResults(Cookie(packBase: ProductionDefectBase.Input), new CalcActual(1000, Defects((5, 50))));
        Assert.Equal(4000m, L(input, Label).Base);
        Assert.Equal(105m, L(input, Glaze).Gross);
        var pack = input.Stages.Single(s => s.No == 5);
        Assert.Equal(1000m, pack.Processed);
        Assert.Equal(50m, pack.Rejected);
        Assert.Equal(50m, pack.DefectKg);
    }

    [Fact]
    public void Packaging_is_counted_per_pack_and_box_and_packaging_defects_never_become_product_defects()
    {
        var plan = ProductionCalc.Plan(Cookie(), 1000);
        Assert.Equal(4000m, L(plan, Label).Base); // 1 000 ÷ 0,25
        Assert.Equal(4020m, L(plan, Label).Gross); // +0,5 %
        Assert.Equal(16.32m, L(plan, Film).Gross); // 16 кг +2 %
        Assert.Equal(200m, L(plan, Box).Gross); // 4 000 ÷ 20

        // 80 испорченных этикеток — 80 этикеток, а не 80 кг продукции: упаковка обработала те же 1 000 кг.
        var r = ProductionCalc.ByResults(Cookie(), new CalcActual(1000, Defects(), Batches: [280, 280, 280], MaterialDefects: new Dictionary<Guid, decimal> { [Label] = 80 }));
        Assert.Equal(4080m, L(r, Label).WithDefects);
        Assert.Equal(4020m, L(r, Label).Gross); // норма — с нормативными потерями
        Assert.Equal(1000m, r.Stages.Single(s => s.No == 5).Processed);
        Assert.Equal(1000m, r.Stages.Single(s => s.No == 5).Good);
        Assert.Equal(0m, r.Stages.Single(s => s.No == 5).Rejected);

        // Фактически упаковано 3 990 упаковок — по ним, а не по массе.
        var packed = ProductionCalc.ByResults(Cookie(), new CalcActual(1000, Defects(), Batches: [280, 280, 280], Packs: 3990));
        Assert.Equal(3990m, L(packed, Label).Base);
        Assert.Equal(199.5m, L(packed, Box).Base);
    }

    [Fact]
    public void Producing_5000_kg_with_100_kg_final_defect_80_bad_labels_and_5_kg_bad_film_gives_every_material()
    {
        var actual = new CalcActual(5000, Defects((5, 100)), MaterialDefects: new Dictionary<Guid, decimal> { [Label] = 80, [Film] = 5 });
        var r = ProductionCalc.ByResults(Cookie(), actual);
        Assert.Empty(r.Problems);

        // Финальный брак 100 кг — после всех этапов: всё до упаковки включительно — на 5 100 кг.
        var pack = r.Stages.Single(s => s.No == 5);
        Assert.Equal(5100m, pack.Processed);
        Assert.Equal(5000m, pack.Good);
        Assert.Equal(100m, pack.DefectKg);

        // Тесто 5 100 × 0,742857 = 3 788,571 кг → 14 замесов по 280 (частичный не разрешён): мука 2 100, сахар 840, масло 560, прочее 420.
        Assert.Equal(3788.571m, r.Batches!.NeedKg);
        Assert.Equal(14, r.Batches.Batches.Count);
        Assert.Equal(2100m, L(r, Flour).Gross);
        Assert.Equal(840m, L(r, Sugar).Gross);
        Assert.Equal(560m, L(r, Butter).Gross);
        Assert.Equal(420m, L(r, Other).Gross);
        Assert.Equal(1020m, L(r, Filling).Gross);
        Assert.Equal(510m, L(r, Glaze).Gross);
        Assert.Equal(255m, L(r, Nuts).Gross);

        // Упаковок 5 100 ÷ 0,25 = 20 400: этикеток по норме 20 502, с фактическим браком 20 480; плёнки 81,6 кг: норма 83,232, с браком 86,6;
        // коробок 1 020. Брак упаковки на годную продукцию не влияет.
        Assert.Equal(20400m, L(r, Label).Net);
        Assert.Equal(20502m, L(r, Label).Gross);
        Assert.Equal(20480m, L(r, Label).WithDefects);
        Assert.Equal(81.6m, L(r, Film).Net);
        Assert.Equal(83.232m, L(r, Film).Gross);
        Assert.Equal(86.6m, L(r, Film).WithDefects);
        Assert.Equal(1020m, L(r, Box).Gross);

        // Замесы не внесены — расчёт неполный: тесто посчитано по норме, и это видно.
        Assert.False(r.Complete);
        Assert.Contains(r.Missing, m => m.Contains("замесы"));
        var done = ProductionCalc.ByResults(Cookie(), actual with { Batches = Enumerable.Repeat(280m, 14).ToList() });
        Assert.True(done.Complete);
        Assert.True(done.Batches!.FromActual);
        Assert.Equal(2100m, L(done, Flour).Gross);
    }

    [Fact]
    public void Missing_stage_results_mark_the_calculation_incomplete_instead_of_assuming_zero()
    {
        var r = ProductionCalc.ByResults(Cookie(), new CalcActual(1000, new Dictionary<int, decimal> { [1] = 0, [2] = 0, [5] = 0 }, Batches: [280, 280, 280]));
        Assert.False(r.Complete);
        Assert.Equal(2, r.Missing.Count);
        Assert.Contains(r.Missing, m => m.Contains("«Начинка»"));
        Assert.Contains(r.Missing, m => m.Contains("«Глазурь»"));
        Assert.False(r.Stages.Single(s => s.No == 3).HasActual);
        Assert.True(r.Stages.Single(s => s.No == 2).HasActual);
    }

    [Fact]
    public void Rework_returned_into_the_dough_saves_fresh_dough_without_writing_raw_materials_off_twice()
    {
        var plain = ProductionCalc.ByResults(Cookie(), new CalcActual(1000, Defects()));
        var rework = ProductionCalc.ByResults(Cookie(), new CalcActual(1000, Defects(), Rework: new Dictionary<int, decimal> { [1] = 20 }));
        Assert.Equal(742.857m, plain.Batches!.NeedKg);
        Assert.Equal(722.857m, rework.Batches!.NeedKg); // 20 кг возвращённого брака заменяют 20 кг свежего теста
        Assert.Equal(L(plain, Glaze).Gross, L(rework, Glaze).Gross);
    }

    [Fact]
    public void Recipe_without_stages_keeps_the_previous_formula_and_units_convert()
    {
        var flat = new CalcRecipe(100, "кг", [],
        [
            Line(Flour, "Мука", null, ProductionLineBasis.Output, 55, 2),
            Line(Sugar, "Сахар", null, ProductionLineBasis.Output, 12.5m),
        ]);
        var r = ProductionCalc.Plan(flat, 250);
        Assert.Empty(r.Stages);
        Assert.Equal(ProductionRules.Requirement(55, 250, 100, 2), L(r, Flour).ItemGross); // 140,25
        Assert.Equal(ProductionRules.Requirement(12.5m, 250, 100, 0), L(r, Sugar).ItemGross);
        Assert.Equal(0.334m, ProductionCalc.Plan(new CalcRecipe(3, "кг", [], [Line(Flour, "Мука", null, ProductionLineBasis.Output, 1, 0.1m)]), 1).Lines[0].ItemGross);

        // Ванилин 500 г на 1 000 кг, на складе — в кг: на 2 000 кг — 1 000 г = 1 кг.
        var vanilla = ProductionCalc.Plan(new CalcRecipe(1000, "кг", [], [Line(Flour, "Ванилин", null, ProductionLineBasis.Output, 500, 0, "г", "кг")]), 2000);
        Assert.Equal(1000m, vanilla.Lines[0].Gross);
        Assert.Equal(1m, vanilla.Lines[0].ItemGross);
        Assert.Equal(1m, vanilla.Items.Single().Quantity);
        Assert.Equal(2.5m, ProductionCalc.Convert(2500, "г", "кг"));

        // Мука мешками по 50 кг (единица закупки позиции): 3 мешка на замес = 150 кг — масса замеса та же, потребность склада — в кг.
        var sacks = Cookie() with { Lines = [.. Cookie().Lines.Select(l => l.ItemId == Flour ? l with { Unit = "мешок", Quantity = 3, ItemFactor = 50 } : l)] };
        var bySack = ProductionCalc.Plan(sacks, 1000);
        Assert.Equal(280m, bySack.Batches!.StandardKg);
        Assert.Equal(9m, L(bySack, Flour).Gross); // 3 замеса × 3 мешка
        Assert.Equal(450m, L(bySack, Flour).ItemGross);
        Assert.Equal(L(ProductionCalc.Plan(Cookie(), 1000), Flour).ItemGross, L(bySack, Flour).ItemGross);
        Assert.Null(ProductionCalc.Convert(1, "л", "кг"));

        // Сахар и в тесте, и в глазури — потребность по позиции складывается.
        var twice = ProductionCalc.Plan(Cookie() with { Lines = [.. Cookie().Lines, Line(Sugar, "Сахар", 4, ProductionLineBasis.Output, 20)] }, 1000);
        Assert.Equal(twice.Lines.Where(l => l.ItemId == Sugar).Sum(l => l.ItemGross), twice.Items.Single(i => i.ItemId == Sugar).Quantity);
        Assert.Equal(2, twice.Lines.Count(l => l.ItemId == Sugar));
    }

    [Fact]
    public void Broken_cards_are_reported_instead_of_negative_quantities_or_division_by_zero()
    {
        static IReadOnlyList<string> Problems(CalcRecipe r) => ProductionCalc.Plan(r, 1000).Problems;

        // Глазури 1 200 кг на 1 000 кг выхода — вход этапа отрицательный: ошибка карты.
        Assert.Contains(Problems(Cookie() with { Lines = [.. Cookie().Lines.Where(l => l.ItemId != Glaze), Line(Glaze, "Глазурь", 4, ProductionLineBasis.Output, 1200)] }),
            p => p.Contains("не меньше его входа"));
        Assert.Contains(Problems(Cookie() with { OutputQuantity = 0 }), p => p.Contains("Выход рецептуры"));
        Assert.Contains(Problems(Cookie() with { Stages = [.. Cookie().Stages.Select(s => s.No == 2 ? s with { YieldPercent = 0 } : s)] }), p => p.Contains("выход — больше нуля"));
        Assert.Contains(Problems(Cookie() with { PackKg = null }), p => p.Contains("массу нетто упаковки"));
        Assert.Contains(Problems(Cookie() with { PacksPerBox = null }), p => p.Contains("упаковок в коробке"));
        Assert.Contains(Problems(Cookie() with { Stages = [.. Cookie().Stages.Select(s => s.No == 2 ? s with { DefectPercent = 100 } : s)] }), p => p.Contains("норматив брака"));
        Assert.Contains(Problems(Cookie() with { Stages = [.. Cookie().Stages.Select(s => s.No == 1 ? s with { Kind = ProductionStageKind.Other } : s.No == 2 ? s with { Kind = ProductionStageKind.Mixing } : s)] }),
            p => p.Contains("первый этап"));
        Assert.Contains(Problems(Cookie(min: 300)), p => p.Contains("Масса замеса"));

        // Яйца в штуках в тесте — без массы штуки сумма замеса неизвестна.
        Assert.Contains(Problems(Cookie() with { Lines = [.. Cookie().Lines, Line(Other, "Яйца", 1, ProductionLineBasis.Batch, 10, 0, "шт")] }), p => p.Contains("массу единицы"));
        var eggs = ProductionCalc.Plan(Cookie() with { Lines = [.. Cookie().Lines.Where(l => l.ItemId != Other), Line(Other, "Яйца", 1, ProductionLineBasis.Batch, 500, 0, "шт", unitKg: 0.06m)] },
            1000);
        Assert.Empty(eggs.Problems);
        Assert.Equal(280m, eggs.Batches!.StandardKg); // 150 + 60 + 40 + 500 × 0,06

        // Компонент без этапа в рецептуре с этапами и норма «на замес» вне замеса.
        Assert.Contains(Problems(Cookie() with { Lines = [.. Cookie().Lines, Line(Other, "Соль", null, ProductionLineBasis.Output, 1)] }), p => p.Contains("укажите этап"));
        Assert.Contains(Problems(Cookie() with { Lines = [.. Cookie().Lines, Line(Other, "Соль", 3, ProductionLineBasis.Batch, 1)] }), p => p.Contains("только у этапа замеса"));
        Assert.Contains(ProductionCalc.Plan(Cookie(), -1).Problems, p => p.Contains("не меньше нуля"));
    }
}
