using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Reports;
using OneBase.Application.Field;
using OneBase.Application.Procurement;
using OneBase.Domain.Procurement;
using OneBase.Domain.Production;

namespace OneBase.Application.Production;

public sealed record DashboardDay(DateOnly Date, decimal GoodKg, decimal DefectKg, decimal? BakeYieldPercent);

public sealed record DashboardBar(string Name, decimal Value, decimal? Extra = null, string? Unit = null);

public sealed record DashboardNormFact(string Name, string Unit, decimal Norm, decimal Fact, decimal? Percent);

public sealed record DashboardOrder(Guid Id, string Number, string Product, string Unit, decimal Planned, decimal Good, ProductionOrderStatus Status, DateOnly PlannedDate,
    int Shortages);

public sealed record DashboardIssue(string Kind, string Title, string Detail, string Link);

/// <summary>
/// Панель «Технолога» за период (только настоящие данные; масса — по продуктам с выходом в единицах массы, штучные — отдельно): выпуск
/// сегодня и за период, план, выполнение, брак (кг, % и стоимость по цене партий; без цены партии — какие материалы), задания в работе, открытые смены, норма и факт расхода по закрытым заданиям,
/// выход после выпечки; по дням, сменам, продуктам; брак по этапам и видам; отклонения расхода; брак упаковки; задания в работе; проблемы.
/// </summary>
public sealed record ProductionDashboard(
    DateOnly From,
    DateOnly To,
    decimal TodayKg,
    decimal GoodKg,
    decimal PlanKg,
    decimal? CompletionPercent,
    decimal DefectKg,
    decimal? DefectPercent,
    decimal? DefectCost,
    IReadOnlyList<string> DefectCostUnpriced,
    int ActiveOrders,
    int OpenShifts,
    int ClosedShifts,
    int PendingRecipes,
    decimal NormKg,
    decimal FactKg,
    decimal? NormDeviationPercent,
    decimal? BakeYieldPercent,
    int PieceOutputs,
    IReadOnlyList<DashboardDay> Days,
    IReadOnlyList<DashboardBar> ByShift,
    IReadOnlyList<DashboardBar> ByProduct,
    IReadOnlyList<DashboardBar> DefectsByStage,
    IReadOnlyList<DashboardBar> DefectsByType,
    IReadOnlyList<DashboardNormFact> NormFact,
    IReadOnlyList<DashboardBar> PackagingDefects,
    IReadOnlyList<DashboardOrder> Orders,
    IReadOnlyList<DashboardIssue> Issues);

public sealed partial class ProductionAnalyticsService
{
    private static IReadOnlyList<ProcurementReportInfo> TechnologistKinds =>
    [
        new("output", L("Производство", "Ishlab chiqarish"), L("Утверждённые смены: дата, смена, цех, линия, продукт, задание, оператор, годный выход, брак и его доля", "Tasdiqlangan smenalar: sana, smena, sex, liniya, mahsulot, topshiriq, operator, yaroqli chiqish, yaroqsiz va uning ulushi")),
        new("defects", L("Брак", "Yaroqsizlik"), L("Брак продукта и материалов по сменам: этап, вид, продукт, цех, смена, оператор, количество, норматив, факт и отклонение, переработка, стоимость по цене партий", "Smenalar bo'yicha mahsulot va material yaroqsizligi: bosqich, tur, mahsulot, sex, smena, operator, miqdor, me'yor, haqiqat va og'ish, qayta ishlash, partiyalar narxi bo'yicha qiymat")),
        new("check", L("Расход: норма и факт", "Sarf: me'yor va haqiqat"), L("По заданиям периода: норма на факт, по результатам смен, факт склада, взвешено, отклонение и процент", "Davr topshiriqlari bo'yicha: haqiqatga me'yor, smenalar natijalari bo'yicha, ombor haqiqati, tortilgan, og'ish va foiz")),
        new("packaging", L("Упаковка", "Qadoqlash"), L("Материалы этапа упаковки по заданиям: норма, по результатам с браком упаковки, факт, отклонение", "Topshiriqlar bo'yicha qadoqlash bosqichi materiallari: me'yor, qadoq yaroqsizligi bilan natijalar bo'yicha, haqiqat, og'ish")),
        new("semi", L("Полуфабрикаты", "Yarim tayyor mahsulotlar"), L("Приход выхода заданий партиями: произведено, передано в задания (каким продуктам), остаток партии", "Topshiriqlar chiqishining partiyalar bilan kirimi: ishlab chiqarilgan, topshiriqlarga berilgan (qaysi mahsulotlarga), partiya qoldig'i")),
        new("recipes", L("Рецептуры", "Retsepturalar"), L("Версии технологических карт: статус, действует с, этапы, компоненты, расход на выход рецептуры и на замес", "Texnologik karta versiyalari: holat, amal qilish sanasi, bosqichlar, komponentlar, retseptura chiqishiga va qorishmaga sarf")),
    ];

    public async Task<ProductionDashboard> TechnologistAsync(ProcurementContext ctx, ProcurementFilter f, CancellationToken ct)
    {
        Require(ctx);
        var today = Today;
        var shifts = await ApprovedShiftsAsync(f, ct);
        var stageNames = await StageNamesAsync(shifts.Select(s => s.RecipeId), ct);
        static decimal? Kg(decimal? qty, string unit) => qty is { } q && ProductionCalc.MassFactor(unit) is { } k ? q * k : null;
        var mass = shifts.Where(s => ProductionCalc.MassFactor(s.Unit) is not null).ToList();
        var goodKg = mass.Sum(s => Kg(s.GoodOutput, s.Unit) ?? 0);
        var defectKg = shifts.Sum(s => s.Defects.Sum(d => d.QuantityKg));
        var costs = (await DefectCostsAsync(shifts, ct)).Values.SelectMany(c => c.Defects.Concat(c.Materials)).ToList();
        var todayKg = await db.ProductionShifts.AsNoTracking().Where(s => s.Status == ProductionShiftStatus.Approved && s.ShiftDate == today
                && (f.DepartmentId == null || s.Order!.DepartmentId == f.DepartmentId))
            .Select(s => new { s.GoodOutput, s.Order!.Recipe!.OutputUnit }).ToListAsync(ct);
        var plans = await db.ProductionOrders.AsNoTracking().Where(o => o.Status != ProductionOrderStatus.Cancelled && o.PlannedDate >= f.From && o.PlannedDate <= f.To
                && (f.DepartmentId == null || o.DepartmentId == f.DepartmentId))
            .Select(o => new { o.PlannedQty, o.Recipe!.OutputUnit }).ToListAsync(ct);
        var planKg = plans.Sum(p => Kg(p.PlannedQty, p.OutputUnit) ?? 0);

        // Норма и факт расхода по закрытым за период заданиям — позиции в единицах массы. Норма — расход по результатам утверждённых смен (как в
        // сверке задания), без смен — норма на фактический выход; перерасход — больше порога снабжения.
        var closedLines = await db.ProductionOrderLines.AsNoTracking()
            .Where(l => l.Order!.Status == ProductionOrderStatus.Completed && l.Order.CompletedAt >= f.FromUtc && l.Order.CompletedAt < f.ToUtc && l.ActualNormQty != null
                        && (f.DepartmentId == null || l.Order.DepartmentId == f.DepartmentId))
            .Select(l => new { l.OrderId, l.Order!.Number, l.Order.RecipeId, l.ItemId, Item = l.Item!.Name, l.Item.UnitCode, Norm = l.ActualNormQty!.Value, Fact = l.IssuedQty - l.ReturnedQty })
            .ToListAsync(ct);
        var used = await UsedAsync(closedLines.Select(l => (l.OrderId, l.RecipeId)).Distinct().ToList(), ct);
        var threshold = await OverThresholdAsync(ct);
        var closed = closedLines.Select(l =>
        {
            var norm = used.TryGetValue(l.OrderId, out var items) && items.TryGetValue(l.ItemId, out var u) ? u : l.Norm;
            return new { l.OrderId, l.Number, l.Item, l.UnitCode, Norm = norm, l.Fact, Overconsumed = ProductionRules.Overconsumed(l.Fact, norm, threshold) };
        }).ToList();
        var normKg = closed.Sum(l => Kg(l.Norm, l.UnitCode) ?? 0);
        var factKg = closed.Sum(l => Kg(l.Fact, l.UnitCode) ?? 0);

        // Выпечка: сумма после ÷ сумма до по этапам выпечки утверждённых смен.
        var bake = shifts.SelectMany(s => s.Bake.Where(b => stageNames.GetValueOrDefault((s.RecipeId, b.StageNo)).Kind == ProductionStageKind.Baking).Select(b => (s.ShiftDate, b.InputKg, b.OutputKg)))
            .ToList();
        decimal? Yield(IEnumerable<(DateOnly, decimal In, decimal Out)> rows)
        {
            var list = rows.ToList();
            var input = list.Sum(r => r.In);
            return input > 0 ? decimal.Round(list.Sum(r => r.Out) / input * 100, 2, MidpointRounding.AwayFromZero) : null;
        }

        var days = new List<DashboardDay>();
        if (f.To.DayNumber - f.From.DayNumber <= 92)
        {
            for (var d = f.From; d <= f.To; d = d.AddDays(1))
            {
                var day = d;
                days.Add(new DashboardDay(day, mass.Where(s => s.ShiftDate == day).Sum(s => Kg(s.GoodOutput, s.Unit) ?? 0),
                    shifts.Where(s => s.ShiftDate == day).Sum(s => s.Defects.Sum(x => x.QuantityKg)), Yield(bake.Where(b => b.ShiftDate == day))));
            }
        }

        var orders = await db.ProductionOrders.AsNoTracking().Where(o => (o.Status == ProductionOrderStatus.Released || o.Status == ProductionOrderStatus.InProgress)
                && (f.DepartmentId == null || o.DepartmentId == f.DepartmentId))
            .OrderBy(o => o.PlannedDate).ThenBy(o => o.Number).Take(50)
            .Select(o => new
            {
                o.Id, o.Number, Product = o.Recipe!.Name, o.Recipe.OutputUnit, o.PlannedQty, o.Status, o.PlannedDate, o.WarehouseId,
                Good = db.ProductionShifts.Where(s => s.OrderId == o.Id && s.Status == ProductionShiftStatus.Approved).Sum(s => s.GoodOutput) ?? 0,
                Lines = o.Lines.Select(l => new { l.ItemId, Left = l.RequiredQty - (l.IssuedQty - l.ReturnedQty) }).ToList(),
            })
            .ToListAsync(ct);
        var shortages = new Dictionary<Guid, int>();
        foreach (var group in orders.GroupBy(o => o.WarehouseId))
        {
            var available = await ProductionStock.AvailableAsync(db, group.SelectMany(o => o.Lines.Select(l => l.ItemId)), group.Key, today, ct);
            foreach (var o in group)
            {
                shortages[o.Id] = o.Lines.Count(l => l.Left > 0 && l.Left > available.GetValueOrDefault(l.ItemId));
            }
        }

        var issues = new List<DashboardIssue>();
        foreach (var o in orders.Where(o => shortages.GetValueOrDefault(o.Id) > 0).Take(5))
        {
            issues.Add(new DashboardIssue("shortage", L($"Не хватает сырья: {o.Number}", $"Xomashyo yetishmaydi: {o.Number}"),
                L($"{o.Product}: позиций без годного остатка — {shortages[o.Id]}", $"{o.Product}: yaroqli qoldiqsiz pozitsiyalar — {shortages[o.Id]}"), $"/production/orders/{o.Id}"));
        }

        foreach (var o in closed.Where(l => l.Overconsumed).GroupBy(l => (l.OrderId, l.Number)).Take(5))
        {
            issues.Add(new DashboardIssue("overconsumption", L($"Перерасход: {o.Key.Number}", $"Ortiqcha sarf: {o.Key.Number}"),
                string.Join(", ", o.Select(l => l.Item).Take(3)), $"/production/orders/{o.Key.OrderId}"));
        }

        foreach (var s in shifts)
        {
            var output = (Kg(s.GoodOutput, s.Unit) ?? s.GoodOutput ?? 0) + s.Defects.Sum(d => d.QuantityKg);
            foreach (var g in s.Defects.GroupBy(d => d.StageNo))
            {
                var stage = stageNames.GetValueOrDefault((s.RecipeId, g.Key));
                var share = output > 0 ? decimal.Round(g.Sum(d => d.QuantityKg) / output * 100, 2, MidpointRounding.AwayFromZero) : 0;
                if (stage.Norm > 0 && share > stage.Norm && issues.Count(i => i.Kind == "defect") < 5)
                {
                    issues.Add(new DashboardIssue("defect", L($"Брак выше норматива: {s.Number}", $"Yaroqsizlik me'yordan yuqori: {s.Number}"),
                        L($"{s.Recipe}, «{stage.Name}»: {share:0.##} % при нормативе {stage.Norm:0.##} %", $"{s.Recipe}, «{stage.Name}»: {share:0.##} %, me'yor {stage.Norm:0.##} %"),
                        $"/production/shifts/{s.Id}"));
                }
            }
        }

        var pendingRecipes = await db.ProductionRecipes.AsNoTracking().Where(r => r.Status == ProductionRecipeStatus.PendingApproval).Select(r => new { r.Id, r.Code, r.Name, r.VersionNo })
            .ToListAsync(ct);
        foreach (var r in pendingRecipes.Take(5))
        {
            issues.Add(new DashboardIssue("approval", L($"Ждёт утверждения: {r.Code} v{r.VersionNo}", $"Tasdiqni kutmoqda: {r.Code} v{r.VersionNo}"), r.Name, $"/production/recipes/{r.Id}"));
        }

        var shiftCounts = await db.ProductionShifts.AsNoTracking().Where(s => s.Status == ProductionShiftStatus.Draft || s.Status == ProductionShiftStatus.Closed)
            .GroupBy(s => s.Status).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        var packaging = await db.ProductionShiftMaterials.AsNoTracking()
            .Where(m => m.Kind == ProductionShiftMaterialKind.Defect && db.ProductionShifts.Any(s => s.Id == m.ShiftId && s.Status == ProductionShiftStatus.Approved
                && s.ShiftDate >= f.From && s.ShiftDate <= f.To && (f.DepartmentId == null || s.Order!.DepartmentId == f.DepartmentId)))
            .GroupBy(m => new { m.Item!.Name, m.Item.UnitCode }).Select(g => new { g.Key.Name, g.Key.UnitCode, Qty = g.Sum(m => m.Quantity) })
            .ToListAsync(ct);

        return new ProductionDashboard(f.From, f.To, todayKg.Sum(s => Kg(s.GoodOutput, s.OutputUnit) ?? 0), goodKg, planKg,
            planKg > 0 ? decimal.Round(goodKg / planKg * 100, 1, MidpointRounding.AwayFromZero) : null, defectKg,
            goodKg + defectKg > 0 ? decimal.Round(defectKg / (goodKg + defectKg) * 100, 2, MidpointRounding.AwayFromZero) : null,
            costs.Any(c => c.Cost != null) ? costs.Sum(c => c.Cost ?? 0) : null, costs.SelectMany(c => c.Unpriced).Distinct().Order().Take(10).ToList(),
            orders.Count, shiftCounts.GetValueOrDefault(ProductionShiftStatus.Draft) + shiftCounts.GetValueOrDefault(ProductionShiftStatus.Closed),
            shiftCounts.GetValueOrDefault(ProductionShiftStatus.Closed), pendingRecipes.Count, normKg, factKg,
            normKg > 0 ? ProductionRules.Deviation(factKg, normKg).Percent : null, Yield(bake), shifts.Count(s => ProductionCalc.MassFactor(s.Unit) is null),
            days,
            mass.GroupBy(s => s.ShiftNo).OrderBy(g => g.Key).Select(g => new DashboardBar(L($"Смена {g.Key}", $"{g.Key}-smena"), g.Sum(s => Kg(s.GoodOutput, s.Unit) ?? 0))).ToList(),
            mass.GroupBy(s => s.Recipe).Select(g => new DashboardBar(g.Key, g.Sum(s => Kg(s.GoodOutput, s.Unit) ?? 0), g.Sum(s => s.Defects.Sum(d => d.QuantityKg))))
                .OrderByDescending(b => b.Value).Take(8).ToList(),
            shifts.SelectMany(s => s.Defects.Select(d => (Stage: stageNames.GetValueOrDefault((s.RecipeId, d.StageNo)).Name ?? $"{d.StageNo}", d.QuantityKg)))
                .GroupBy(x => x.Stage).Select(g => new DashboardBar(g.Key, g.Sum(x => x.QuantityKg))).OrderByDescending(b => b.Value).Take(10).ToList(),
            shifts.SelectMany(s => s.Defects).GroupBy(d => d.Name).Select(g => new DashboardBar(g.Key, g.Sum(d => d.QuantityKg))).OrderByDescending(b => b.Value).Take(10).ToList(),
            closed.GroupBy(l => (l.Item, l.UnitCode)).Select(g =>
                {
                    var (norm, fact) = (g.Sum(l => l.Norm), g.Sum(l => l.Fact));
                    return new DashboardNormFact(g.Key.Item, g.Key.UnitCode, norm, fact, ProductionRules.Deviation(fact, norm).Percent);
                })
                .OrderByDescending(x => Math.Abs(x.Percent ?? 0)).Take(8).ToList(),
            packaging.OrderByDescending(p => p.Qty).Take(8).Select(p => new DashboardBar(p.Name, p.Qty, null, p.UnitCode)).ToList(),
            orders.Take(10).Select(o => new DashboardOrder(o.Id, o.Number, o.Product, o.OutputUnit, o.PlannedQty, o.Good, o.Status, o.PlannedDate, shortages.GetValueOrDefault(o.Id)))
                .ToList(),
            issues);
    }

    private sealed record ShiftDefectRow(int StageNo, Guid? DefectTypeId, string Name, decimal QuantityKg, ProductionReworkStatus Rework);

    private sealed record ShiftMaterialDefectRow(Guid ItemId, string Name, string Unit, int? StageNo, decimal Quantity);

    private sealed record ShiftBakeRow(int StageNo, decimal InputKg, decimal OutputKg);

    private sealed record ShiftRow(Guid Id, string Number, DateOnly ShiftDate, int ShiftNo, string? Line, Guid OrderId, string OrderNumber, string Department, Guid RecipeId,
        string Recipe, string Unit, decimal? GoodOutput, decimal PlannedQty, string Operator, List<ShiftDefectRow> Defects, List<ShiftBakeRow> Bake,
        List<ShiftMaterialDefectRow> MaterialDefects);

    private async Task<List<ShiftRow>> ApprovedShiftsAsync(ProcurementFilter f, CancellationToken ct) =>
        await db.ProductionShifts.AsNoTracking()
            .Where(s => s.Status == ProductionShiftStatus.Approved && s.ShiftDate >= f.From && s.ShiftDate <= f.To && (f.DepartmentId == null || s.Order!.DepartmentId == f.DepartmentId))
            .OrderBy(s => s.ShiftDate).ThenBy(s => s.ShiftNo).ThenBy(s => s.Number).Take(MaxRows)
            .Select(s => new ShiftRow(s.Id, s.Number, s.ShiftDate, s.ShiftNo, s.Line, s.OrderId, s.Order!.Number, s.Order.Department!.Name, s.Order.RecipeId, s.Order.Recipe!.Name,
                s.Order.Recipe.OutputUnit, s.GoodOutput, s.Order.PlannedQty, s.Operator!.FullName,
                s.Defects.Select(d => new ShiftDefectRow(d.StageNo, d.DefectTypeId, d.Name, d.QuantityKg, d.Rework)).ToList(),
                s.Stages.Where(x => x.InputKg > 0 && x.OutputKg != null).Select(x => new ShiftBakeRow(x.StageNo, x.InputKg!.Value, x.OutputKg!.Value)).ToList(),
                s.Materials.Where(m => m.Kind == ProductionShiftMaterialKind.Defect && m.Quantity > 0)
                    .Select(m => new ShiftMaterialDefectRow(m.ItemId, m.Item!.Name, m.Item.UnitCode, m.StageNo, m.Quantity)).ToList()))
            .ToListAsync(ct);

    /// <summary>Порог перерасхода из настроек снабжения, %.</summary>
    private async Task<decimal> OverThresholdAsync(CancellationToken ct) =>
        (await db.ProcurementSettings.AsNoTracking().FirstOrDefaultAsync(ct))?.OverconsumptionPercent ?? ProcurementSettings.DefaultOverconsumptionPercent;

    /// <summary>Расход по результатам утверждённых смен по заданиям: задание → позиция → количество; без смен или карта с ошибками — задания нет.</summary>
    private async Task<Dictionary<Guid, Dictionary<Guid, decimal>>> UsedAsync(IReadOnlyCollection<(Guid Order, Guid Recipe)> orders, CancellationToken ct)
    {
        var orderIds = orders.Select(o => o.Order).ToList();
        var recipeIds = orders.Select(o => o.Recipe).Distinct().ToList();
        var result = new Dictionary<Guid, Dictionary<Guid, decimal>>();
        if (orderIds.Count == 0)
        {
            return result;
        }

        var recipes = (await ProductionRecipeService.LoadAsync(db, r => recipeIds.Contains(r.Id), false, ct)).ToDictionary(r => r.Id);
        var shifts = (await ProductionShiftService.Load(db.ProductionShifts.AsNoTracking())
                .Where(s => orderIds.Contains(s.OrderId) && s.Status == ProductionShiftStatus.Approved).ToListAsync(ct))
            .GroupBy(s => s.OrderId).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var (order, recipeId) in orders)
        {
            if (shifts.TryGetValue(order, out var list) && recipes.TryGetValue(recipeId, out var recipe))
            {
                var calc = ProductionRecipeCalc.From(recipe);
                if (ProductionOrderService.ResultItems(calc, ProductionCalc.ByResults(calc, ProductionShiftRules.Actual(recipe, list))) is { } items)
                {
                    result[order] = items;
                }
            }
        }

        return result;
    }

    /// <summary>Этапы версий: (версия, номер) → название, тип, норматив брака продукта этапа.</summary>
    private async Task<Dictionary<(Guid, int), (string Name, ProductionStageKind Kind, decimal Norm)>> StageNamesAsync(IEnumerable<Guid> recipeIds, CancellationToken ct)
    {
        var ids = recipeIds.Distinct().ToList();
        var stages = await db.ProductionRecipeStages.AsNoTracking().Where(s => ids.Contains(s.RecipeId))
            .Select(s => new
            {
                s.RecipeId, s.StageNo, s.Name, s.Kind,
                Norm = db.ProductionDefectTypes.Where(d => d.StageId == s.Id && d.ItemId == null).Sum(d => (decimal?)d.NormPercent) ?? 0,
            })
            .ToListAsync(ct);
        return stages.ToDictionary(s => (s.RecipeId, s.StageNo), s => (s.Name, s.Kind, s.Norm));
    }

    /// <summary>Стоимость записи брака: сумма по цене партий задания (null — ни у одного материала нет цены) и материалы без цены партии.</summary>
    private sealed record DefectCost(decimal? Cost, IReadOnlyList<string> Unpriced);

    /// <summary>Стоимость брака смен: брак продукта (по записям) и брак материалов (по строкам) — по цене партий своего задания.</summary>
    private sealed record ShiftCosts(IReadOnlyList<DefectCost> Defects, IReadOnlyList<DefectCost> Materials);

    /// <summary>
    /// Стоимость брака по цене партий, выданных в задание смены: брак продукта — состав кг брака этапа по технологической карте (всё, что
    /// добавлено до него и на нём, по норме с потерями) × кг × цены; брак материала — количество × цена. Без цены партии — материал в списке.
    /// </summary>
    private async Task<Dictionary<Guid, ShiftCosts>> DefectCostsAsync(IReadOnlyList<ShiftRow> shifts, CancellationToken ct)
    {
        var ids = shifts.Where(s => s.Defects.Count > 0 || s.MaterialDefects.Count > 0).Select(s => s.RecipeId).Distinct().ToList();
        var recipes = (await ProductionRecipeService.LoadAsync(db, r => ids.Contains(r.Id), false, ct)).ToDictionary(r => r.Id, r => ProductionRecipeCalc.From(r));
        var prices = await ProductionCost.BatchPricesAsync(db, shifts.Select(s => s.OrderId).Distinct().ToList(), ct);
        var contents = new Dictionary<(Guid, int), IReadOnlyList<CalcItemTotal>?>();
        DefectCost Cost(Guid order, IEnumerable<(Guid Item, string Name, decimal Qty)> parts)
        {
            decimal? sum = null;
            var unpriced = new List<string>();
            foreach (var (item, name, qty) in parts)
            {
                if (prices.GetValueOrDefault((order, item)) is { } price)
                {
                    sum = (sum ?? 0) + qty * price;
                }
                else
                {
                    unpriced.Add(name);
                }
            }

            return new DefectCost(sum is { } x ? decimal.Round(x, 2, MidpointRounding.AwayFromZero) : null, unpriced.Distinct().ToList());
        }

        return shifts.ToDictionary(s => s.Id, s =>
        {
            var calc = recipes.GetValueOrDefault(s.RecipeId);
            return new ShiftCosts(
                s.Defects.Select(d =>
                {
                    if (!contents.TryGetValue((s.RecipeId, d.StageNo), out var content))
                    {
                        contents[(s.RecipeId, d.StageNo)] = content = calc is null ? null : ProductionCalc.DefectContent(calc, d.StageNo);
                    }

                    return content is null
                        ? new DefectCost(null, [L("состав брака не считается по карте", "yaroqsiz tarkibi karta bo'yicha hisoblanmaydi")])
                        : Cost(s.OrderId, content.Select(c => (c.ItemId, c.Name, c.Quantity * d.QuantityKg)));
                }).ToList(),
                s.MaterialDefects.Select(m => Cost(s.OrderId, [(m.ItemId, m.Name, m.Quantity)])).ToList());
        });
    }

    private async Task<AiReportSheet> OutputReportAsync(ProcurementFilter f, CancellationToken ct)
    {
        var shifts = await ApprovedShiftsAsync(f, ct);
        return new AiReportSheet(L("Производство", "Ishlab chiqarish"),
            [
                Date(L("Дата", "Sana")), Int(L("Смена", "Smena")), Text(L("Номер смены", "Smena raqami")), Text(L("Цех", "Sex")), Text(L("Линия", "Liniya")), Text(L("Продукт", "Mahsulot")),
                Text(L("Задание", "Topshiriq")), Text(L("Оператор", "Operator")), Text(L("Ед.", "Birl.")), Num(L("План задания", "Topshiriq rejasi")), Num(L("Годный выход", "Yaroqli chiqish"), true),
                Num(L("Брак, кг", "Yaroqsiz, kg"), true), Pct(L("Брак, %", "Yaroqsiz, %")),
            ],
            shifts.Select(s =>
            {
                var defect = s.Defects.Sum(d => d.QuantityKg);
                var good = (ProductionCalc.MassFactor(s.Unit) is { } k ? (s.GoodOutput ?? 0) * k : s.GoodOutput ?? 0);
                return (IReadOnlyList<JsonElement>)[
                    J(D(s.ShiftDate)), J(s.ShiftNo), J(s.Number), J(s.Department), J(s.Line), J(s.Recipe), J(s.OrderNumber), J(s.Operator), J(s.Unit), J(s.PlannedQty), J(s.GoodOutput),
                    J(defect), J(good + defect > 0 ? decimal.Round(defect / (good + defect) * 100, 2, MidpointRounding.AwayFromZero) : (decimal?)null),
                ];
            }).ToList(), true, Cap(shifts.Count));
    }

    /// <summary>
    /// Брак утверждённых смен: продукта — по записям (этап, вид, кг, норматив вида или этапа, доля от выпуска смены и отклонение, переработка) и
    /// материалов (количество в единице склада); стоимость — по цене партий, выданных в задание, без цены партии — какие материалы.
    /// </summary>
    private async Task<AiReportSheet> DefectsReportAsync(ProcurementFilter f, CancellationToken ct)
    {
        var shifts = await ApprovedShiftsAsync(f, ct);
        var stages = await StageNamesAsync(shifts.Select(s => s.RecipeId), ct);
        var typeIds = shifts.SelectMany(s => s.Defects).Where(d => d.DefectTypeId != null).Select(d => d.DefectTypeId!.Value).Distinct().ToList();
        var norms = await db.ProductionDefectTypes.AsNoTracking().Where(t => typeIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.NormPercent, ct);
        var costs = await DefectCostsAsync(shifts, ct);
        static decimal R2(decimal x) => decimal.Round(x, 2, MidpointRounding.AwayFromZero);
        var rows = shifts.SelectMany(s =>
        {
            var good = ProductionCalc.MassFactor(s.Unit) is { } k ? (s.GoodOutput ?? 0) * k : s.GoodOutput ?? 0;
            var total = good + s.Defects.Sum(d => d.QuantityKg);
            var cost = costs[s.Id];
            string? Unpriced(DefectCost c) => c.Unpriced.Count > 0 ? string.Join(", ", c.Unpriced) : null;
            IReadOnlyList<JsonElement> Row(int? stageNo, string kind, decimal? kg, decimal? material, string? unit, decimal? norm, decimal? share, string? rework, DefectCost c)
            {
                var stage = stageNo is { } no ? stages.GetValueOrDefault((s.RecipeId, no)) : default;
                return [
                    J(D(s.ShiftDate)), J(s.ShiftNo), J(s.Number), J(s.Department), J(s.Recipe), J(s.OrderNumber), J(stage.Name ?? (stageNo is { } n ? $"{n}" : null)), J(kind),
                    J(s.Operator), J(kg), J(material), J(unit), J(norm), J(share), J(norm is { } a && share is { } b ? R2(b - a) : null), J(rework), J(c.Cost), J(Unpriced(c)),
                ];
            }

            return s.Defects.Select((d, i) =>
            {
                var norm = d.DefectTypeId is { } t && norms.TryGetValue(t, out var n) ? n : stages.GetValueOrDefault((s.RecipeId, d.StageNo)).Norm;
                return Row(d.StageNo, d.Name, d.QuantityKg, null, "кг", norm, total > 0 ? R2(d.QuantityKg / total * 100) : null, d.Rework switch
                {
                    ProductionReworkStatus.Sent => L("направлен на переработку", "qayta ishlashga yuborilgan"),
                    ProductionReworkStatus.Allowed => L("разрешён к переработке", "qayta ishlashga ruxsat"),
                    _ => L("запрещён к переработке", "qayta ishlash taqiqlangan"),
                }, cost.Defects[i]);
            }).Concat(s.MaterialDefects.Select((m, i) =>
                Row(m.StageNo, L($"Брак материала: {m.Name}", $"Material yaroqsizligi: {m.Name}"), null, m.Quantity, m.Unit, null, null, null, cost.Materials[i])));
        }).ToList();
        return new AiReportSheet(L("Брак", "Yaroqsizlik"),
            [
                Date(L("Дата", "Sana")), Int(L("Смена", "Smena")), Text(L("Номер смены", "Smena raqami")), Text(L("Цех", "Sex")), Text(L("Продукт", "Mahsulot")),
                Text(L("Задание", "Topshiriq")), Text(L("Этап", "Bosqich")), Text(L("Вид брака", "Yaroqsizlik turi")), Text(L("Оператор", "Operator")),
                Num(L("Брак продукта, кг", "Mahsulot yaroqsizligi, kg"), true), Num(L("Брак материала", "Material yaroqsizligi")), Text(L("Ед.", "Birl.")),
                Pct(L("Норматив, %", "Me'yor, %")), Pct(L("Доля от выпуска смены, %", "Smena chiqishidan ulush, %")), Num(L("Отклонение, п. п.", "Og'ish, f. p.")),
                Text(L("Переработка", "Qayta ishlash")), Num(L("Стоимость брака, сум", "Yaroqsizlik qiymati, so'm"), true), Text(L("Без цены партии", "Partiya narxisiz")),
            ], rows, true, Cap(rows.Count));
    }

    /// <summary>
    /// Расход «норма — факт» по заданиям, выпущенным или закрытым за период (по дате выпуска): норма на факт (выход при закрытии или годный по
    /// сменам), по результатам смен, факт склада, взвешено, отклонение. packaging — только материалы этапов упаковки.
    /// </summary>
    private async Task<AiReportSheet> CheckReportAsync(ProcurementFilter f, bool packaging, CancellationToken ct)
    {
        var orders = await db.ProductionOrders.AsNoTracking()
            .Where(o => o.Status != ProductionOrderStatus.Draft && o.Status != ProductionOrderStatus.Cancelled && o.PlannedDate >= f.From && o.PlannedDate <= f.To
                        && (f.DepartmentId == null || o.DepartmentId == f.DepartmentId))
            .OrderBy(o => o.PlannedDate).ThenBy(o => o.Number).Take(500).Select(o => o.Id).ToListAsync(ct);
        var rows = new List<IReadOnlyList<JsonElement>>();
        foreach (var id in orders)
        {
            var o = await db.ProductionOrders.AsNoTracking().Include(x => x.Department).Include(x => x.Recipe).Include(x => x.Lines).ThenInclude(l => l.Item).FirstAsync(x => x.Id == id, ct);
            var recipe = (await ProductionRecipeService.LoadAsync(db, r => r.Id == o.RecipeId, false, ct)).Single();
            var packing = recipe.Stages.Where(s => s.Kind == ProductionStageKind.Packing).Select(s => s.Id).ToHashSet();
            var packItems = recipe.Lines.Where(l => l.StageId is { } sid && packing.Contains(sid)).Select(l => l.ItemId).ToHashSet();
            if (packaging && packItems.Count == 0)
            {
                continue;
            }

            var shiftRows = await ProductionShiftService.Load(db.ProductionShifts.AsNoTracking()).Where(s => s.OrderId == id && s.Status == ProductionShiftStatus.Approved).ToListAsync(ct);
            var calc = ProductionRecipeCalc.From(recipe);
            var output = o.OutputQty ?? (shiftRows.Count > 0 ? shiftRows.Sum(s => s.GoodOutput ?? 0) : null);
            var norm = output is { } q ? ProductionCalc.Plan(calc, q) : null;
            var results = shiftRows.Count > 0 ? ProductionCalc.ByResults(calc, ProductionShiftRules.Actual(recipe, shiftRows)) : null;
            var weighed = shiftRows.SelectMany(s => s.Materials).Where(m => m.Kind == ProductionShiftMaterialKind.Weighed).GroupBy(m => m.ItemId).ToDictionary(g => g.Key, g => g.Sum(m => m.Quantity));
            var used = ProductionOrderService.ResultItems(calc, results);
            foreach (var l in o.Lines.OrderBy(l => l.LineNo).Where(l => !packaging || packItems.Contains(l.ItemId)))
            {
                decimal? n = norm is { Problems.Count: 0 } ? norm.Items.FirstOrDefault(i => i.ItemId == l.ItemId)?.Quantity : null;
                decimal? b = used is not null && used.TryGetValue(l.ItemId, out var u) ? u : null;
                var defect = results?.Lines.Where(x => x.ItemId == l.ItemId).Sum(x => x.MaterialDefect ?? 0);
                var fact = l.IssuedQty - l.ReturnedQty;
                var (dq, dp) = (b ?? n) is { } basis ? ProductionRules.Deviation(fact, decimal.Round(basis, 3)) : (0m, (decimal?)null);
                rows.Add([
                    J(o.Number), J(D(o.PlannedDate)), J(o.Department!.Name), J($"{recipe.Code} v{recipe.VersionNo} — {recipe.Name}"), J(output), J(l.Item!.Sku), J(l.Item.Name),
                    J(l.Item.UnitCode), J(l.RequiredQty), J(n), J(b), J(defect > 0 ? defect : null), J(fact),
                    J(weighed.TryGetValue(l.ItemId, out var w) ? w : null), J((b ?? n) is null ? null : dq), J(dp),
                ]);
            }
        }

        return new AiReportSheet(packaging ? L("Упаковка", "Qadoqlash") : L("Расход: норма и факт", "Sarf: me'yor va haqiqat"),
            [
                Text(L("Задание", "Topshiriq")), Date(L("Дата выпуска", "Chiqarish sanasi")), Text(L("Цех", "Sex")), Text(L("Рецептура", "Retseptura")), Num(L("Выход", "Chiqish")),
                Text(L("Код материала", "Material kodi")), Text(L("Материал", "Material")), Text(L("Ед.", "Birl.")), Num(L("План", "Reja"), true), Num(L("Норма на факт", "Haqiqatga me'yor"), true),
                Num(L("По результатам", "Natijalar bo'yicha"), true), Num(L("Брак материала", "Material yaroqsizligi"), true), Num(L("Факт склада", "Ombor haqiqati"), true),
                Num(L("Взвешено", "Tortilgan"), true), Num(L("Отклонение", "Og'ish"), true), Pct(L("Отклонение, %", "Og'ish, %")),
            ], rows, true, "");
    }

    private async Task<AiReportSheet> SemiReportAsync(ProcurementFilter f, Guid? warehouseId, CancellationToken ct)
    {
        var receipts = await db.ProcurementStockMovements.AsNoTracking()
            .Where(m => m.Kind == ProcurementMovementKind.ProductionOutput && m.CreatedAt >= f.FromUtc && m.CreatedAt < f.ToUtc && (warehouseId == null || m.WarehouseId == warehouseId))
            .OrderBy(m => m.CreatedAt).Take(MaxRows)
            .Select(m => new
            {
                m.CreatedAt, m.BatchId, Batch = m.Batch == null ? null : m.Batch.Number, ExpiresOn = m.Batch == null ? null : m.Batch.ExpiresOn, m.Item!.Sku, Item = m.Item.Name,
                m.Item.UnitCode, m.Quantity, Order = m.Document!.ProductionOrderId, Number = db.ProductionOrders.Where(o => o.Id == m.Document.ProductionOrderId).Select(o => o.Number).FirstOrDefault(),
            })
            .ToListAsync(ct);
        var batchIds = receipts.Where(r => r.BatchId != null).Select(r => r.BatchId!.Value).ToList();
        var uses = await db.ProcurementStockMovements.AsNoTracking()
            .Where(m => m.BatchId != null && batchIds.Contains(m.BatchId.Value) && (m.Kind == ProcurementMovementKind.ProductionIssue || m.Kind == ProcurementMovementKind.ProductionReturn))
            .Select(m => new { m.BatchId, m.Quantity, Product = db.ProductionOrders.Where(o => o.Id == m.Document!.ProductionOrderId).Select(o => o.Recipe!.Name).FirstOrDefault() })
            .ToListAsync(ct);
        var onHand = await db.ProcurementBatchBalances.AsNoTracking().Where(b => batchIds.Contains(b.BatchId)).GroupBy(b => b.BatchId)
            .Select(g => new { g.Key, Qty = g.Sum(b => b.OnHand) }).ToDictionaryAsync(x => x.Key, x => x.Qty, ct);
        return new AiReportSheet(L("Полуфабрикаты", "Yarim tayyor mahsulotlar"),
            [
                Date(L("Произведено", "Ishlab chiqarilgan")), Text(L("Задание", "Topshiriq")), Text(L("Артикул", "Artikul")), Text(L("Полуфабрикат", "Yarim tayyor mahsulot")), Text(L("Ед.", "Birl.")),
                Text(L("Партия", "Partiya")), Date(L("Годен до", "Yaroqlilik muddati")), Num(L("Произведено", "Ishlab chiqarilgan"), true), Num(L("Передано в задания", "Topshiriqlarga berilgan"), true),
                Text(L("В какие продукты", "Qaysi mahsulotlarga")), Num(L("Остаток партии", "Partiya qoldig'i"), true),
            ],
            receipts.Select(r =>
            {
                var used = uses.Where(u => u.BatchId == r.BatchId).ToList();
                return (IReadOnlyList<JsonElement>)[
                    J(D(r.CreatedAt)), J(r.Number), J(r.Sku), J(r.Item), J(r.UnitCode), J(r.Batch), J(D(r.ExpiresOn)), J(r.Quantity), J(-used.Sum(u => u.Quantity)),
                    J(string.Join(", ", used.Where(u => u.Quantity < 0).Select(u => u.Product).Distinct())), J(r.BatchId is { } b ? onHand.GetValueOrDefault(b) : null),
                ];
            }).ToList(), true, Cap(receipts.Count));
    }

    private async Task<AiReportSheet> RecipesReportAsync(CancellationToken ct)
    {
        var recipes = await ProductionRecipeService.LoadAsync(db, _ => true, false, ct, q => q.Include(r => r.Technologist));
        var rows = new List<IReadOnlyList<JsonElement>>();
        foreach (var r in recipes.OrderBy(r => r.Code).ThenByDescending(r => r.VersionNo).Take(500))
        {
            var calc = ProductionRecipeCalc.From(r);
            var plan = ProductionCalc.Plan(calc, r.OutputQuantity);
            var stages = r.Stages.ToDictionary(s => s.Id);
            foreach (var l in r.Lines.OrderBy(l => l.LineNo))
            {
                var line = plan.Lines.FirstOrDefault(x => x.Key == l.Id);
                var stage = l.StageId is { } sid ? stages.GetValueOrDefault(sid) : null;
                rows.Add([
                    J(r.Code), J(r.VersionNo), J(r.Name), J(ProductionRules.Label(r.Status)), J(D(r.EffectiveFrom)), J(r.Technologist?.FullName), J(r.Stages.Count),
                    J(stage is null ? null : $"{stage.StageNo}. {stage.Name}"), J(l.Item!.Sku), J(l.Item.Name), J(ProductionRecipeService.Basis(l.Basis).Trim() is { Length: > 0 } b ? b : L("на выход", "chiqishga")),
                    J(l.Quantity), J(l.UnitCode ?? l.Item.UnitCode), J(l.LossPercent), J($"{ProcurementRules.Quantity(r.OutputQuantity)} {r.OutputUnit}"), J(line?.ItemGross),
                    J(l.Basis == ProductionLineBasis.Batch ? l.Quantity : null),
                ]);
            }
        }

        return new AiReportSheet(L("Рецептуры", "Retsepturalar"),
            [
                Text(L("Код", "Kod")), Int(L("Версия", "Versiya")), Text(L("Название", "Nomi")), Text(L("Статус", "Holat")), Date(L("Действует с", "Amal qiladi")), Text(L("Технолог", "Texnolog")),
                Int(L("Этапов", "Bosqichlar")), Text(L("Этап", "Bosqich")), Text(L("Артикул", "Artikul")), Text(L("Компонент", "Komponent")), Text(L("База нормы", "Me'yor asosi")),
                Num(L("Норма", "Me'yor")), Text(L("Ед.", "Birl.")), Pct(L("Потери, %", "Yo'qotishlar, %")), Text(L("Выход рецептуры", "Retseptura chiqishi")),
                Num(L("Расход на выход рецептуры (с потерями и браком)", "Retseptura chiqishiga sarf (yo'qotish va yaroqsiz bilan)")), Num(L("На один замес", "Bitta qorishmaga")),
            ], rows, false, "");
    }
}
