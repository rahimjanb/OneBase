using OneBase.Domain.Production;

namespace OneBase.Application.Production;

/// <summary>Этап для расчёта: порядок, название, тип, нормативный выход % (выход ÷ весь вход этапа), норматив брака продукта % и его база.</summary>
public sealed record CalcStage(int No, string Name, ProductionStageKind Kind, decimal YieldPercent, decimal DefectPercent, ProductionDefectBase DefectBase);

/// <summary>
/// Компонент для расчёта: Key — строка рецептуры; позиция, название; единица строки и единица позиции склада; этап (пусто — рецептура без
/// этапов); база нормы; норма в единице строки на базу; потери % (технологические + нормативы брака этого материала); масса единицы, кг;
/// ItemFactor — единиц склада в одной единице строки (единица закупки позиции: «мешок = 50 кг»), если единица — не масса.
/// </summary>
public sealed record CalcLine(Guid Key, Guid ItemId, string Name, string Unit, string ItemUnit, int? StageNo, ProductionLineBasis Basis, decimal Quantity, decimal LossPercent,
    decimal? UnitKg = null, decimal? ItemFactor = null);

/// <summary>Рецептура для расчёта: выход (на него заданы нормы «на выход»), единица выхода, этапы, компоненты, замес и упаковка.</summary>
public sealed record CalcRecipe(decimal OutputQuantity, string OutputUnit, IReadOnlyList<CalcStage> Stages, IReadOnlyList<CalcLine> Lines, decimal? BatchMinKg = null,
    decimal? BatchMaxKg = null, bool PartialBatches = false, decimal? PieceKg = null, decimal? PackKg = null, int? PacksPerBox = null);

/// <summary>
/// Фактические результаты для расчёта по результатам: годный выход (в единице выхода); брак продукта по этапам, кг (нет записи этапа — расчёт
/// неполный; ноль — «брака не было»); массы фактических замесов, кг; упаковок и коробок; брак материалов по строкам (в единице строки);
/// возвращённый в производство перерабатываемый брак по этапам, кг (вход этапа — повторно сырьё не списывается).
/// </summary>
public sealed record CalcActual(decimal GoodOutput, IReadOnlyDictionary<int, decimal> StageDefects, IReadOnlyList<decimal>? Batches = null, decimal? Packs = null,
    decimal? Boxes = null, IReadOnlyDictionary<Guid, decimal>? MaterialDefects = null, IReadOnlyDictionary<int, decimal>? Rework = null);

/// <summary>
/// Этап в расчёте: обработано (в единицах готовой продукции — сколько готовой продукции соответствует обработанному), годный выход, брак;
/// массы (кг; null — масса потока неизвестна): весь вход, добавлено компонентами, выход, брак, потери массы (вход обработки − выход: влага,
/// налипание), дальше — годное на следующий этап (= вход следующего). HasActual — есть фактический брак этапа. Массы считаются до округления.
/// </summary>
public sealed record CalcStageResult(int No, string Name, ProductionStageKind Kind, decimal YieldPercent, decimal DefectPercent, ProductionDefectBase DefectBase,
    decimal Processed, decimal Good, decimal Rejected, decimal? InputKg, decimal? AddedKg, decimal? OutputKg, decimal? DefectKg, bool HasActual, decimal? LossKg = null,
    decimal? NextKg = null);

public sealed record CalcBatch(int No, decimal MassKg, bool Partial);

/// <summary>Замесы: стандартная (сумма рецептуры замеса), мин. и макс. масса; нужно теста; замесы; всего; излишек; по фактическим замесам.</summary>
public sealed record CalcBatchPlan(decimal StandardKg, decimal MinKg, decimal MaxKg, decimal NeedKg, IReadOnlyList<CalcBatch> Batches, decimal TotalKg, decimal SurplusKg,
    bool FromActual);

/// <summary>
/// Компонент в расчёте: база (количество выхода, замесов, штук, упаковок, коробок), чистый расход, нормативные потери, норма с потерями (в
/// единице строки и в единице склада); по результатам — фактический брак материала и расход с ним (null — брак не внесён).
/// </summary>
public sealed record CalcLineResult(Guid Key, Guid ItemId, string Name, int? StageNo, ProductionLineBasis Basis, string Unit, decimal Base, decimal Net, decimal Loss,
    decimal Gross, string ItemUnit, decimal ItemGross, decimal? MaterialDefect, decimal? WithDefects);

/// <summary>Потребность по позиции склада (сумма строк всех этапов), в единице склада.</summary>
public sealed record CalcItemTotal(Guid ItemId, string Name, string Unit, decimal Quantity);

/// <summary>
/// Результат: количество (план или годный выход), этапы, замесы, компоненты, итог по позициям. Problems — рецептура не считается (ошибки
/// карты); Missing — нет фактических данных (расчёт неполный, нули не подставлены молча).
/// </summary>
public sealed record CalcResult(decimal Quantity, string OutputUnit, bool ByResults, IReadOnlyList<CalcStageResult> Stages, CalcBatchPlan? Batches,
    IReadOnlyList<CalcLineResult> Lines, IReadOnlyList<CalcItemTotal> Items, IReadOnlyList<string> Problems, IReadOnlyList<string> Missing)
{
    public bool Complete => Problems.Count == 0 && Missing.Count == 0;
}

/// <summary>Выпечка: вход и выход, кг; потеря, кг и %; выход %, норма; отклонение выхода от нормы (п. п.); вне допуска (мин.–макс.).</summary>
public sealed record BakeResult(decimal InputKg, decimal OutputKg, decimal LossKg, decimal LossPercent, decimal YieldPercent, decimal NormYieldPercent, decimal DeviationPoints,
    bool OutOfRange);

/// <summary>
/// Production Calculation Engine — расчёт по технологической цепочке (чистые функции, decimal, проверяется тестами).
/// <para>
/// Нормы «на выход» — на OutputQuantity готовой продукции, на этапе, где компонент добавляется; компоненты замеса — на один замес; упаковка —
/// на штуку, упаковку, коробку. Цепочка считается от конца в единицах готовой продукции: брак этапа (от обработанного) — обработано =
/// годное ÷ (1 − брак), всё прежнее тоже потрачено; брак «на входе» отсеян до обработки — компоненты этапа на него не тратятся; брак «от
/// готовой» — норматив × количество. Так брак до глазури не начисляет глазурь, брак после глазури учитывает все прежние материалы.
/// </para>
/// <para>
/// Масса потока на единицу готовой: от конца вход этапа = выход ÷ (выход % ÷ 100) минус масса добавленных компонентов — из неё масса теста и
/// замесы. Компоненты этапа тяжелее его выхода — ошибка карты, а не отрицательное количество. Без этапов — прежняя формула: норма × количество ÷
/// выход × (1 + потери ÷ 100).
/// </para>
/// </summary>
public static class ProductionCalc
{
    public const int MaxStages = 30;

    /// <summary>Единицы-счёт: потребность округляется вверх до целого.</summary>
    public static bool Counted(string unit) => unit is "шт" or "упак" or "пачка" or "коробка" or "компл";

    /// <summary>Кг в одной единице массы (кг, г, т); не масса — null.</summary>
    public static decimal? MassFactor(string unit) => unit switch
    {
        "кг" => 1m,
        "г" => 0.001m,
        "т" => 1000m,
        _ => null,
    };

    /// <summary>Количество из единицы from в to: одна и та же или обе — массы; иначе null (не переводится).</summary>
    public static decimal? Convert(decimal quantity, string from, string to) =>
        from == to ? quantity : MassFactor(from) is { } f && MassFactor(to) is { } t ? quantity * f / t : null;

    /// <summary>Плановый расчёт на quantity годной готовой продукции — по нормативам выхода и брака каждого этапа.</summary>
    public static CalcResult Plan(CalcRecipe recipe, decimal quantity) => Run(recipe, quantity, null);

    /// <summary>Расчёт по результатам производства: фактический годный выход, фактический брак этапов, фактические замесы и упаковка.</summary>
    public static CalcResult ByResults(CalcRecipe recipe, CalcActual actual) => Run(recipe, actual.GoodOutput, actual);

    /// <summary>Выпечка: потеря = вход − выход, % потери и выхода от входа (до 0,01), отклонение выхода от нормы; вне допуска — ниже мин. или выше макс.</summary>
    public static BakeResult Bake(decimal inputKg, decimal outputKg, decimal normYieldPercent, decimal? minPercent = null, decimal? maxPercent = null)
    {
        if (inputKg <= 0 || outputKg < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(inputKg), "Масса до выпечки — больше нуля, после — не меньше нуля.");
        }

        var yield = R(outputKg / inputKg * 100, 2);
        var deviation = yield - normYieldPercent;
        return new BakeResult(inputKg, outputKg, inputKg - outputKg, R((inputKg - outputKg) / inputKg * 100, 2), yield, normYieldPercent, deviation,
            (minPercent is { } min && yield < min) || (maxPercent is { } max && yield > max));
    }

    /// <summary>
    /// Замесы на need кг теста: стандартные; остаток — разложить по замесам до максимальной массы; частичный разрешён — один частичный (не
    /// меньше минимума), а если остаток меньше минимума — поровну на замес больше (каждый не меньше минимума) или один минимальный с излишком;
    /// частичный не разрешён — ещё один стандартный (излишек).
    /// </summary>
    public static CalcBatchPlan Batches(decimal needKg, decimal standardKg, decimal minKg, decimal maxKg, bool partial)
    {
        if (standardKg <= 0 || minKg <= 0 || minKg > standardKg || maxKg < standardKg)
        {
            throw new ArgumentOutOfRangeException(nameof(standardKg), "Масса замеса: 0 < минимум ≤ стандарт ≤ максимум.");
        }

        var need = R(Math.Max(0, needKg), 3);
        var list = new List<decimal>();
        if (need > 0)
        {
            var full = (int)Math.Floor(need / standardKg);
            var rest = need - full * standardKg;
            if (rest == 0)
            {
                list.AddRange(Enumerable.Repeat(standardKg, full));
            }
            else if (full > 0 && rest <= full * (maxKg - standardKg))
            {
                list.AddRange(Split(need, full));
            }
            else if (partial && rest >= minKg)
            {
                list.AddRange(Enumerable.Repeat(standardKg, full));
                list.Add(rest);
            }
            else if (partial && need / (full + 1) >= minKg)
            {
                list.AddRange(Split(need, full + 1));
            }
            else if (partial)
            {
                list.AddRange(Enumerable.Repeat(standardKg, full));
                list.Add(minKg);
            }
            else
            {
                list.AddRange(Enumerable.Repeat(standardKg, full + 1));
            }
        }

        var total = list.Sum();
        return new CalcBatchPlan(standardKg, minKg, maxKg, need, list.Select((m, i) => new CalcBatch(i + 1, m, m < standardKg)).ToList(), total, total - need, false);

        static IEnumerable<decimal> Split(decimal total, int n)
        {
            var each = R(total / n, 3);
            return Enumerable.Repeat(each, n - 1).Append(total - each * (n - 1));
        }
    }

    /// <summary>Ошибки технологической карты, при которых расчёт невозможен (пустой список — карта считается).</summary>
    public static IReadOnlyList<string> Check(CalcRecipe r)
    {
        var problems = new List<string>();
        if (r.OutputQuantity <= 0)
        {
            problems.Add("Выход рецептуры — больше нуля.");
        }

        var stages = r.Stages.OrderBy(s => s.No).ToList();
        if (stages.Count > MaxStages)
        {
            problems.Add($"Этапов — до {MaxStages}.");
        }

        if (stages.Select(s => s.No).Distinct().Count() != stages.Count)
        {
            problems.Add("Порядковые номера этапов повторяются.");
        }

        foreach (var s in stages)
        {
            if (s.YieldPercent <= 0)
            {
                problems.Add($"Этап «{s.Name}»: нормативный выход — больше нуля.");
            }

            if (s.DefectPercent < 0 || s.DefectPercent >= 100)
            {
                problems.Add($"Этап «{s.Name}»: норматив брака — от 0 до 100 % (не включая 100).");
            }
        }

        var mixing = stages.Where(s => s.Kind == ProductionStageKind.Mixing).ToList();
        if (mixing.Count > 1)
        {
            problems.Add("Замес в цепочке — один.");
        }
        else if (mixing.Count == 1 && stages[0].Kind != ProductionStageKind.Mixing)
        {
            problems.Add("Замес — первый этап цепочки: с него начинается масса потока.");
        }

        var byNo = stages.ToDictionary(s => s.No, s => s, EqualityComparer<int>.Default);
        foreach (var l in r.Lines)
        {
            if (stages.Count > 0 && (l.StageNo is not { } no || !byNo.ContainsKey(no)))
            {
                problems.Add($"«{l.Name}»: укажите этап.");
                continue;
            }

            if (l.Quantity <= 0)
            {
                problems.Add($"«{l.Name}»: норма — больше нуля.");
            }

            if (l.LossPercent < 0 || l.LossPercent > 100)
            {
                problems.Add($"«{l.Name}»: потери — от 0 до 100 %.");
            }

            if (ToItem(l, 1) is null)
            {
                problems.Add($"«{l.Name}»: единица строки «{l.Unit}» не переводится в единицу склада «{l.ItemUnit}».");
            }

            var stage = l.StageNo is { } n && byNo.TryGetValue(n, out var st) ? st : null;
            var isMixing = stage?.Kind == ProductionStageKind.Mixing;
            if (isMixing != (l.Basis == ProductionLineBasis.Batch))
            {
                problems.Add(isMixing
                    ? $"«{l.Name}»: компоненты замеса задаются на один замес."
                    : $"«{l.Name}»: норма «на замес» — только у этапа замеса.");
            }

            switch (l.Basis)
            {
                case ProductionLineBasis.Piece when r.PieceKg is null && r.OutputUnit != "шт":
                    problems.Add($"«{l.Name}»: норма на штуку — укажите массу одной штуки.");
                    break;
                case ProductionLineBasis.Pack or ProductionLineBasis.Box when r.PackKg is null:
                    problems.Add($"«{l.Name}»: норма на упаковку или коробку — укажите массу нетто упаковки.");
                    break;
                case ProductionLineBasis.Pack or ProductionLineBasis.Box when OutputKg(r) is null:
                    problems.Add($"«{l.Name}»: выход в «{r.OutputUnit}» — укажите массу одной штуки, чтобы считать упаковки.");
                    break;
            }

            if (l.Basis == ProductionLineBasis.Box && r.PacksPerBox is null)
            {
                problems.Add($"«{l.Name}»: норма на коробку — укажите, сколько упаковок в коробке.");
            }

            if (mixing.Count == 1 && stage?.Kind != ProductionStageKind.Packing && l.Basis is ProductionLineBasis.Output or ProductionLineBasis.Batch && LineKg(l) is null)
            {
                problems.Add($"«{l.Name}»: в «{l.Unit}» — укажите массу единицы, кг (для массы замеса и теста).");
            }
        }

        if (mixing.Count == 1)
        {
            if (OutputKg(r) is null)
            {
                problems.Add($"Выход в «{r.OutputUnit}» — укажите массу одной штуки, кг, чтобы считать тесто.");
            }

            var mix = mixing[0];
            if (!r.Lines.Any(l => l.StageNo == mix.No))
            {
                problems.Add($"Этап «{mix.Name}»: добавьте рецептуру замеса.");
            }

            if (problems.Count == 0)
            {
                var standard = StandardBatch(r, mix.No);
                var (min, max) = (r.BatchMinKg ?? standard, r.BatchMaxKg ?? standard);
                if (min > standard || max < standard)
                {
                    problems.Add($"Масса замеса: минимум ≤ {Q(standard)} кг (рецептура замеса) ≤ максимум.");
                }
            }
        }

        if (problems.Count == 0 && stages.Count > 0)
        {
            Flow(r, stages, problems);
        }

        return problems;
    }

    private static CalcResult Run(CalcRecipe r, decimal quantity, CalcActual? actual)
    {
        var problems = Check(r).ToList();
        if (quantity < 0)
        {
            problems.Add("Количество — не меньше нуля.");
        }

        var empty = new CalcResult(quantity, r.OutputUnit, actual is not null, [], null, [], [], problems, []);
        if (problems.Count > 0)
        {
            return empty;
        }

        var missing = new List<string>();
        var real = r.Stages.OrderBy(s => s.No).ToList();
        // Без этапов — один условный этап: выход 100 %, брака нет — прежняя формула.
        var stages = real.Count > 0 ? real : [new CalcStage(0, "", ProductionStageKind.Other, 100, 0, ProductionDefectBase.Output)];
        var flow = Flow(r, stages, problems);
        var n = stages.Count;
        var processed = new decimal[n];
        var good = new decimal[n];
        var rejected = new decimal[n];
        var has = new bool[n];
        var g = quantity;
        for (var i = n - 1; i >= 0; i--)
        {
            var s = stages[i];
            var d = s.DefectPercent / 100;
            good[i] = g;
            decimal before;
            if (actual is null)
            {
                has[i] = true;
                (processed[i], before) = s.DefectBase switch
                {
                    ProductionDefectBase.Input => (g, g / (1 - d)),
                    ProductionDefectBase.Finished => (g + d * quantity, g + d * quantity),
                    _ => (g / (1 - d), g / (1 - d)),
                };
            }
            else
            {
                has[i] = real.Count == 0 || actual.StageDefects.ContainsKey(s.No);
                var kg = real.Count == 0 ? 0 : actual.StageDefects.GetValueOrDefault(s.No);
                if (!has[i])
                {
                    missing.Add($"Этап «{s.Name}»: брак не внесён.");
                }

                // Брак в кг → в единицы готовой продукции по массе потока (на выходе этапа или, если отсеян на входе, — предыдущего).
                var per = s.DefectBase == ProductionDefectBase.Input ? flow?.MainIn[i] : flow?.Out[i];
                if (kg > 0 && per is not > 0)
                {
                    problems.Add($"Этап «{s.Name}»: брак в кг не пересчитать — масса потока неизвестна (укажите массу штуки).");
                    return empty with { Problems = problems };
                }

                var units = kg > 0 ? kg / per!.Value : 0;
                (processed[i], before) = s.DefectBase == ProductionDefectBase.Input ? (g, g + units) : (g + units, g + units);
            }

            rejected[i] = before - g;
            // Возвращённый в производство брак — вход этапа: свежего сырья на него не нужно, прежний расход не повторяется.
            if (actual?.Rework?.GetValueOrDefault(s.No) is > 0 and var back)
            {
                var per = flow?.MainIn[i];
                if (per is not > 0)
                {
                    problems.Add($"Этап «{s.Name}»: переработку в кг не пересчитать — масса потока неизвестна.");
                    return empty with { Problems = problems };
                }

                before = Math.Max(0, before - back / per.Value);
            }

            g = before;
        }

        // Замесы (замес — первый этап): тесто = нужно на вход замеса (за вычетом возвращённого в тесто брака) × масса входа на единицу готовой.
        CalcBatchPlan? batches = null;
        var mixIndex = stages.FindIndex(s => s.Kind == ProductionStageKind.Mixing);
        if (mixIndex >= 0)
        {
            var standard = StandardBatch(r, stages[mixIndex].No);
            var (min, max) = (r.BatchMinKg ?? standard, r.BatchMaxKg ?? standard);
            var need = R(g * flow!.In[mixIndex], 3);
            if (actual?.Batches is { Count: > 0 } done)
            {
                var list = done.Select((m, i) => new CalcBatch(i + 1, m, m < standard)).ToList();
                batches = new CalcBatchPlan(standard, min, max, need, list, list.Sum(b => b.MassKg), list.Sum(b => b.MassKg) - need, true);
            }
            else
            {
                batches = Batches(need, standard, min, max, r.PartialBatches);
                if (actual is not null)
                {
                    missing.Add("Фактические замесы не внесены — тесто посчитано по норме.");
                }
            }
        }

        var outKg = OutputKg(r);
        var lines = new List<CalcLineResult>();
        foreach (var l in r.Lines)
        {
            var i = real.Count == 0 ? 0 : stages.FindIndex(s => s.No == l.StageNo);
            var through = processed[i];
            var basis = l.Basis switch
            {
                ProductionLineBasis.Batch => batches!.TotalKg / batches.StandardKg,
                ProductionLineBasis.Piece => Count(r.OutputUnit == "шт" ? through : through * outKg!.Value / r.PieceKg!.Value, actual),
                ProductionLineBasis.Pack => actual?.Packs ?? Count(through * outKg!.Value / r.PackKg!.Value, actual),
                ProductionLineBasis.Box => actual?.Boxes ?? Count((actual?.Packs ?? Count(through * outKg!.Value / r.PackKg!.Value, actual)) / r.PacksPerBox!.Value, actual),
                _ => through / r.OutputQuantity,
            };
            var net = l.Quantity * basis;
            var loss = net * l.LossPercent / 100;
            var gross = Round(net + loss, l.Unit);
            var defect = actual?.MaterialDefects?.TryGetValue(l.Key, out var md) == true ? md : (decimal?)null;
            lines.Add(new CalcLineResult(l.Key, l.ItemId, l.Name, l.StageNo, l.Basis, l.Unit, R(basis * (l.Basis == ProductionLineBasis.Output ? r.OutputQuantity : 1), 3),
                R(net, 3), R(loss, 3), gross, l.ItemUnit, Round(ToItem(l, gross)!.Value, l.ItemUnit), defect,
                defect is { } x ? Round(net + x, l.Unit) : null));
        }

        var items = lines.GroupBy(l => l.ItemId)
            .Select(gr => new CalcItemTotal(gr.Key, gr.First().Name, gr.First().ItemUnit, gr.Sum(l => l.ItemGross)))
            .ToList();
        var results = real.Count == 0
            ? []
            : stages.Select((s, i) =>
            {
                decimal? Kg(Func<FlowMass, decimal> pick) => flow is null ? null : R(pick(flow), 3);
                // Брак на входе отсеян из основного входа до обработки; от обработанного — из выхода этапа.
                var atInput = s.DefectBase == ProductionDefectBase.Input;
                return new CalcStageResult(s.No, s.Name, s.Kind, s.YieldPercent, s.DefectPercent, s.DefectBase, R(processed[i], 3), R(good[i], 3), R(rejected[i], 3),
                    Kg(f => processed[i] * f.In[i] + (atInput ? rejected[i] * f.MainIn[i] : 0)), Kg(f => processed[i] * f.Added[i]), Kg(f => processed[i] * f.Out[i]),
                    Kg(f => rejected[i] * (atInput ? f.MainIn[i] : f.Out[i])), has[i], Kg(f => processed[i] * (f.In[i] - f.Out[i])), Kg(f => good[i] * f.Out[i]));
            }).ToList();
        return new CalcResult(quantity, r.OutputUnit, actual is not null, results, batches, lines, items, problems, missing);
    }

    /// <summary>
    /// Материалы в одном кг брака продукта этапа stageNo — по норме с потерями, в единице склада: всё, что добавлено на этапах до него и на нём
    /// самом; брак «на входе» отсеян до обработки — компонентов этапа в нём нет. Так в браке до глазури нет глазури, а в браке после неё — все
    /// прежние материалы. Карта с ошибками, этапа нет или масса потока неизвестна — null.
    /// </summary>
    public static IReadOnlyList<CalcItemTotal>? DefectContent(CalcRecipe r, int stageNo)
    {
        var stages = r.Stages.OrderBy(s => s.No).ToList();
        var i = stages.FindIndex(s => s.No == stageNo);
        if (i < 0 || Check(r).Count > 0 || Flow(r, stages, []) is not { } flow || OutputKg(r) is not { } outKg)
        {
            return null;
        }

        var atInput = stages[i].DefectBase == ProductionDefectBase.Input;
        var perKg = atInput ? flow.MainIn[i] : flow.Out[i];
        if (perKg <= 0)
        {
            return null;
        }

        var mix = stages.FindIndex(s => s.Kind == ProductionStageKind.Mixing);
        var content = new List<(CalcLine Line, decimal Quantity)>();
        foreach (var l in r.Lines)
        {
            var j = stages.FindIndex(s => s.No == l.StageNo);
            if (j > i || (atInput && j == i))
            {
                continue;
            }

            // На единицу готовой продукции: на выход — норма ÷ выход рецептуры; замес — доля рецептуры замеса в тесте единицы; упаковка — по штукам, упаковкам, коробкам.
            var perUnit = l.Basis switch
            {
                ProductionLineBasis.Batch => l.Quantity * flow.In[mix] / StandardBatch(r, stages[mix].No),
                ProductionLineBasis.Piece => l.Quantity * (r.OutputUnit == "шт" ? 1 : outKg / r.PieceKg!.Value),
                ProductionLineBasis.Pack => l.Quantity * outKg / r.PackKg!.Value,
                ProductionLineBasis.Box => l.Quantity * outKg / r.PackKg!.Value / r.PacksPerBox!.Value,
                _ => l.Quantity / r.OutputQuantity,
            };
            content.Add((l, ToItem(l, perUnit * (1 + l.LossPercent / 100) / perKg)!.Value));
        }

        return content.GroupBy(c => c.Line.ItemId).Select(g => new CalcItemTotal(g.Key, g.First().Line.Name, g.First().Line.ItemUnit, g.Sum(c => c.Quantity))).ToList();
    }

    /// <summary>Масса потока на единицу готовой продукции: на выходе этапа, весь вход, основной вход (от предыдущего), добавлено компонентами.</summary>
    private sealed record FlowMass(decimal[] Out, decimal[] In, decimal[] MainIn, decimal[] Added);

    private static FlowMass? Flow(CalcRecipe r, IReadOnlyList<CalcStage> stages, List<string> problems)
    {
        if (OutputKg(r) is not { } outKg)
        {
            return null;
        }

        var n = stages.Count;
        var flow = new FlowMass(new decimal[n], new decimal[n], new decimal[n], new decimal[n]);
        var m = outKg;
        for (var i = n - 1; i >= 0; i--)
        {
            var s = stages[i];
            flow.Out[i] = m;
            flow.In[i] = m / (s.YieldPercent / 100);
            flow.Added[i] = s.Kind is ProductionStageKind.Packing or ProductionStageKind.Mixing
                ? 0
                : r.Lines.Where(l => l.StageNo == s.No && l.Basis == ProductionLineBasis.Output && LineKg(l) is not null)
                    .Sum(l => l.Quantity * LineKg(l)!.Value / r.OutputQuantity);
            flow.MainIn[i] = flow.In[i] - flow.Added[i];
            if (i > 0 && flow.MainIn[i] <= 0)
            {
                problems.Add($"Этап «{s.Name}»: компоненты этапа ({Q(flow.Added[i] * r.OutputQuantity)} кг на {Q(r.OutputQuantity)} {r.OutputUnit}) не меньше его входа — проверьте выход и нормы.");
                return null;
            }

            m = flow.MainIn[i];
        }

        return flow;
    }

    /// <summary>Кг в единице выхода: единица массы — по ней, «шт» — масса штуки; иначе неизвестно.</summary>
    private static decimal? OutputKg(CalcRecipe r) => MassFactor(r.OutputUnit) ?? (r.OutputUnit == "шт" ? r.PieceKg : null);

    /// <summary>Кг в единице строки: единица массы; единица закупки позиции в кг-позиции («мешок = 50 кг»); иначе — масса единицы из карты.</summary>
    private static decimal? LineKg(CalcLine l) => MassFactor(l.Unit) ?? (l.ItemFactor is { } f && MassFactor(l.ItemUnit) is { } m ? f * m : (decimal?)null) ?? l.UnitKg;

    /// <summary>Единиц склада в quantity единиц строки (перевод масс или единица закупки позиции); не переводится — null.</summary>
    public static decimal? ToItemUnit(CalcLine line, decimal quantity) => ToItem(line, quantity);

    /// <summary>Количество из единицы строки в единицу склада: перевод масс или единица закупки позиции; не переводится — null.</summary>
    private static decimal? ToItem(CalcLine l, decimal quantity) => Convert(quantity, l.Unit, l.ItemUnit) ?? (l.ItemFactor is { } f ? quantity * f : (decimal?)null);

    private static decimal StandardBatch(CalcRecipe r, int mixNo) => r.Lines.Where(l => l.StageNo == mixNo).Sum(l => l.Quantity * (LineKg(l) ?? 0));

    /// <summary>Штук, упаковок, коробок: по плану — вверх до целого, по результатам — как есть.</summary>
    private static decimal Count(decimal value, CalcActual? actual) => actual is null ? Math.Ceiling(R(value, 6)) : value;

    private static decimal Round(decimal value, string unit) => Counted(unit) ? Math.Ceiling(R(value, 6)) : R(value, 3);

    private static decimal R(decimal value, int digits) => decimal.Round(value, digits, MidpointRounding.AwayFromZero);

    private static string Q(decimal value) => R(value, 3).ToString("0.###", System.Globalization.CultureInfo.GetCultureInfo("ru-RU"));
}
