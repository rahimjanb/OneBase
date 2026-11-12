using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Production;
using OneBase.Domain.Production;

namespace OneBase.Application.Analytik.Simulation;

/// <summary>Нормы сырья на 1 кг продукта по действующей техкарте, средние потери строк по позициям, что не так (если нормы не посчитаны).</summary>
public sealed record RecipeNormSet(Guid RecipeId, string Code, IReadOnlyList<BaselineNorm> Norms, IReadOnlyDictionary<Guid, decimal> LossPercent, string? Problem);

/// <summary>Оплаты точек за период: сумма принятых, средний срок оплаты (дней после реализации, взвешенно по сумме), первая и последняя дата загрузки.</summary>
public sealed record PaymentFacts(decimal Total, decimal? LagDays, DateOnly? First, DateOnly? Last);

/// <summary>
/// Чтение исходных данных симулятора, которых нет в готовых сервисах разделов: нормы техкарт на кг (тем же механизмом ProductionCalc, что
/// задания на выпуск), мощность по утверждённым сменам, скидки и торговые представители по филиалам, срок оплаты точек. Только читает.
/// </summary>
public sealed class SimulationSources(IAppDbContext db)
{
    /// <summary>Сколько дней утверждённых смен берётся для оценки мощности.</summary>
    public const int ProductionHistoryDays = 90;

    /// <summary>
    /// Нормы сырья на 1 кг товара Linko по его действующей техкарте: ProductionCalc.Plan на базовое количество техкарты (выход этапов, брак,
    /// потери, замесы) ÷ кг этого количества. Две действующие техкарты на один товар — берётся последняя утверждённая (с пометкой).
    /// </summary>
    public async Task<IReadOnlyDictionary<long, RecipeNormSet>> RecipeNormsAsync(IReadOnlyCollection<long> productIds, CancellationToken ct)
    {
        var ids = productIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<long, RecipeNormSet>();
        }

        var recipes = await ProductionRecipeService.LoadAsync(db,
            r => r.Status == ProductionRecipeStatus.Active && r.LinkoProductId != null && ids.Contains(r.LinkoProductId.Value), false, ct);
        var result = new Dictionary<long, RecipeNormSet>();
        foreach (var group in recipes.GroupBy(r => r.LinkoProductId!.Value))
        {
            var recipe = group.OrderByDescending(r => r.ActivatedAt).ThenByDescending(r => r.VersionNo).First();
            var set = Norms(recipe);
            if (group.Count() > 1)
            {
                set = set with
                {
                    Problem = (set.Problem is null ? "" : set.Problem + " ") +
                              $"У товара {group.Count()} действующих техкарты ({string.Join(", ", group.Select(r => r.Code))}) — взята последняя утверждённая {recipe.Code}.",
                };
            }

            result[group.Key] = set;
        }

        return result;
    }

    internal static RecipeNormSet Norms(ProductionRecipe r)
    {
        var loss = r.Lines.GroupBy(l => l.ItemId).ToDictionary(g => g.Key,
            g => g.Sum(l => l.Quantity) > 0 ? g.Sum(l => l.LossPercent * l.Quantity) / g.Sum(l => l.Quantity) : 0);
        decimal? kg = ProductionCalc.MassFactor(r.OutputUnit) is { } factor ? r.OutputQuantity * factor
            : ProductionCalc.Counted(r.OutputUnit) && r.PieceKg is > 0 ? r.OutputQuantity * r.PieceKg : null;
        if (kg is not > 0)
        {
            return new RecipeNormSet(r.Id, r.Code, [], loss, $"Техкарта {r.Code}: выход в «{r.OutputUnit}» без массы штуки — нормы на кг не посчитаны.");
        }

        var calc = ProductionCalc.Plan(ProductionRecipeCalc.From(r), r.OutputQuantity);
        if (calc.Problems.Count > 0)
        {
            return new RecipeNormSet(r.Id, r.Code, [], loss, $"Техкарта {r.Code} не считается: {string.Join("; ", calc.Problems)}");
        }

        var norms = calc.Items.Where(i => i.Quantity > 0).OrderBy(i => i.ItemId).Select(i => new BaselineNorm(i.ItemId, i.Quantity / kg.Value)).ToList();
        return new RecipeNormSet(r.Id, r.Code, norms, loss, norms.Count == 0 ? $"В техкарте {r.Code} нет сырья." : null);
    }

    /// <summary>
    /// Производство по утверждённым сменам за 90 дней до даты: мощность в среднем за календарный день = типичная лучшая смена (90-й процентиль
    /// годного выхода) × смен в рабочий день × доля рабочих дней; выпуск в день, брак (брак ÷ (годное + брак)).
    /// </summary>
    public async Task<BaselineProduction> ProductionAsync(DateOnly asOf, CancellationToken ct)
    {
        var from = asOf.AddDays(-ProductionHistoryDays + 1);
        var shifts = await db.ProductionShifts.AsNoTracking()
            .Where(s => s.Status == ProductionShiftStatus.Approved && s.ShiftDate >= from && s.ShiftDate <= asOf)
            .Select(s => new { s.ShiftDate, s.ShiftNo, Good = s.GoodOutput ?? 0, Defect = s.Defects.Sum(d => (decimal?)d.QuantityKg) ?? 0 })
            .ToListAsync(ct);
        var worked = shifts.Where(s => s.Good > 0).ToList();
        if (worked.Count == 0)
        {
            return new BaselineProduction(null, null, null, null, null, 0, 0, ProductionHistoryDays, "утверждённых смен за 90 дней нет",
                ["Утверждённых смен за 90 дней нет — мощность производства по факту не оценить; её можно ввести в исходных данных сценария."]);
        }

        var notes = new List<string>();
        var first = worked.Min(s => s.ShiftDate);
        var window = asOf.DayNumber - first.DayNumber + 1;
        var workDays = worked.Select(s => s.ShiftDate).Distinct().Count();
        var outputs = worked.Select(s => s.Good).OrderBy(v => v).ToList();
        var p90 = outputs[Math.Max(0, (int)Math.Ceiling(0.9 * outputs.Count) - 1)];
        var shiftsPerDay = (decimal)worked.Count / workDays;
        var share = (decimal)workDays / window;
        var good = shifts.Sum(s => s.Good);
        var defect = shifts.Sum(s => s.Defect);
        if (window < 30)
        {
            notes.Add($"Утверждённые смены есть только за {window} дн. — оценка мощности ориентировочная.");
        }

        var source = $"типичная лучшая смена {Math.Round(p90, 0)} кг × {Math.Round(shiftsPerDay, 2)} смен в рабочий день × доля рабочих дней " +
                     $"{Math.Round(share * 100, 0)} % (утверждённые смены за {window} дн.)";
        return new BaselineProduction(Math.Round(p90 * shiftsPerDay * share, 3), Math.Round(good / window, 3), p90, Math.Round(shiftsPerDay, 4), Math.Round(share, 4),
            good + defect > 0 ? Math.Round(defect / (good + defect), 6) : 0, worked.Count, window, source, notes);
    }

    /// <summary>Скидки по товарам за период: Σ скидки и Σ цены нетто по строкам проданных заказов (по дате реализации), только филиалы области.</summary>
    public async Task<IReadOnlyDictionary<long, (decimal Discount, decimal Net)>> DiscountsAsync(DateOnly from, DateOnly to, IReadOnlyCollection<long>? branches,
        IReadOnlyCollection<string> soldStatuses, CancellationToken ct)
    {
        var orders = db.LinkoOrders.AsNoTracking()
            .Where(o => o.AcceptedDate >= from && o.AcceptedDate <= to && soldStatuses.Contains(o.Status));
        if (branches is not null)
        {
            orders = orders.Where(o => o.BranchId != null && branches.Contains(o.BranchId.Value));
        }

        var rows = await db.LinkoOrderLines.AsNoTracking()
            .Join(orders, l => l.OrderId, o => o.Id, (l, o) => l)
            .Where(l => l.ProductId != null)
            .GroupBy(l => l.ProductId!.Value)
            .Select(g => new { ProductId = g.Key, Discount = g.Sum(l => l.TotalDiscount ?? 0), Net = g.Sum(l => l.TotalPrice) })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.ProductId, r => (r.Discount, r.Net));
    }

    /// <summary>Торговые представители с продажами за период по филиалам Linko и последний день продаж в периоде.</summary>
    public async Task<(IReadOnlyDictionary<long, int> Agents, DateOnly? Through)> AgentsAsync(DateOnly from, DateOnly to, IReadOnlyCollection<string> soldStatuses,
        CancellationToken ct)
    {
        var orders = db.LinkoOrders.AsNoTracking()
            .Where(o => o.AcceptedDate >= from && o.AcceptedDate <= to && soldStatuses.Contains(o.Status) && o.BranchId != null);
        var agents = await orders.Where(o => o.AgentId != null).GroupBy(o => o.BranchId!.Value)
            .Select(g => new { Branch = g.Key, Agents = g.Select(o => o.AgentId).Distinct().Count() })
            .ToListAsync(ct);
        var through = await orders.MaxAsync(o => o.AcceptedDate, ct);
        return (agents.ToDictionary(a => a.Branch, a => a.Agents), through);
    }

    /// <summary>
    /// Принятые оплаты точек за период и средний срок оплаты: по оплатам с заказом — дней от реализации заказа до оплаты (0–180), взвешенно по
    /// сумме.
    /// </summary>
    public async Task<PaymentFacts> PaymentsAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var payments = db.LinkoPayments.AsNoTracking().Where(p => !p.IsDelete && p.Status == "accepted" && p.CreatedDate != null);
        var total = await payments.Where(p => p.CreatedDate >= from && p.CreatedDate <= to).SumAsync(p => (decimal?)p.Amount, ct) ?? 0;
        var first = await payments.MinAsync(p => p.CreatedDate, ct);
        var last = await payments.MaxAsync(p => p.CreatedDate, ct);
        var linked = await payments.Where(p => p.CreatedDate >= from && p.CreatedDate <= to && p.OrderId != null && p.Amount > 0)
            .Join(db.LinkoOrders.AsNoTracking().Where(o => o.AcceptedDate != null), p => p.OrderId, o => o.Id,
                (p, o) => new { p.Amount, Paid = p.CreatedDate!.Value, Sold = o.AcceptedDate!.Value })
            .ToListAsync(ct);
        var valid = linked.Select(x => (x.Amount, Days: x.Paid.DayNumber - x.Sold.DayNumber)).Where(x => x.Days is >= 0 and <= 180).ToList();
        var weight = valid.Sum(x => x.Amount);
        return new PaymentFacts(total, weight > 0 ? Math.Round(valid.Sum(x => x.Amount * x.Days) / weight, 2) : null, first, last);
    }
}
