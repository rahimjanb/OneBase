using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Analytik.Forecasting;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Field;
using OneBase.Application.Procurement;
using OneBase.Application.Production;
using OneBase.Application.Security;
using OneBase.Domain.Procurement;
using OneBase.Domain.Production;

namespace OneBase.Application.Analytik.Supply;

/// <summary>Позиция склада сырья: остатки, расход, дни запаса, статус и стоимость (по последней цене прихода, если она есть).</summary>
public sealed record WarehouseItemView(
    Guid ItemId, string Sku, string Name, string Category, string Unit, decimal OnHand, decimal Reserved, decimal Unusable, decimal Available, decimal MinQty,
    decimal AvgDailyConsumption, decimal Consumed30, decimal? DaysOfCover, string Status, decimal? UnitPrice, decimal? Value, DateOnly? LastMovement);

/// <summary>Итог по горизонту: позиций к закупке, известный бюджет, позиций без цены.</summary>
public sealed record SupplyHorizonTotal(int Days, int Items, decimal Budget, int ItemsWithoutPrice);

public sealed record SupplySupplierTotal(string Supplier, int Items, decimal Budget30, int Critical);

/// <summary>
/// План закупок и склад сырья на дату: рекомендации по позициям (с причинами и допущениями), итоги горизонтов 7/14/30/60/90 дней, по
/// поставщикам, склад (статусы и стоимость), заметки о полноте данных.
/// </summary>
public sealed record SupplyPlanView(
    DateOnly AsOf, IReadOnlyList<SupplyRecommendation> Items, IReadOnlyList<SupplyHorizonTotal> Horizons, IReadOnlyDictionary<string, int> Risks,
    IReadOnlyList<SupplySupplierTotal> Suppliers, IReadOnlyList<WarehouseItemView> Warehouse, IReadOnlyDictionary<string, int> WarehouseStatus,
    decimal StockValue, int ItemsWithoutPrice, IReadOnlyList<string> DataNotes, IReadOnlyList<string> Sources);

/// <summary>
/// Supply &amp; Procurement Intelligence: загружает данные склада снабжения и производства и считает потребность (SupplyPlanMath) по каждой
/// активной позиции. Доступный остаток — как у производства (годные партии минус резерв, ProductionStock), расход — как на экране склада
/// (выдачи и выдачи в производство минус возвраты и сторно выдач), задания — открытые (выпущенные и в работе), поставки — подтверждённые
/// (ProcurementArrivalService.ExpectedAsync: тендеры с договором и «Куплено», за вычетом уже приехавшего), цена — последний приход, срок
/// поставки — у поставщика последнего прихода, иначе по факту его заказов. Только читает.
/// </summary>
public sealed class SupplyPlanner(
    IAppDbContext db,
    ProcurementArrivalService arrivals,
    ProcurementRequestService requests,
    AnalytikOptions options,
    TimeProvider clock)
{
    /// <summary>Сколько дней истории расхода берётся для среднего и разброса.</summary>
    public const int HistoryDays = 90;

    public static readonly string[] ViewPermissions =
        [Permissions.ProcurementRead, Permissions.ProcurementManage, Permissions.ProcurementWarehouse, Permissions.ProductionRead, Permissions.ProductionManage];

    public static bool CanView(AnalytikPrincipal p) => p.HasAny(ViewPermissions);

    public async Task<SupplyPlanView> PlanAsync(AnalytikPrincipal principal, CancellationToken ct, string? search = null)
    {
        if (!CanView(principal))
        {
            throw new QueryPlanException(QueryErrors.Denied, "План закупок и склад сырья видят снабжение, склад, производство и директор.");
        }

        var today = ProcurementBatchRules.Today(clock);
        var notes = new List<string>();
        var items = await db.ProcurementItems.AsNoTracking().Include(i => i.Units).Include(i => i.Category)
            .Where(i => i.IsActive)
            .ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            items = items.Where(i => i.Name.Contains(s, StringComparison.OrdinalIgnoreCase) || i.Sku.Contains(s, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var ids = items.Select(i => i.Id).ToList();
        var activeWarehouses = db.ProcurementWarehouses.Where(w => w.IsActive).Select(w => w.Id);
        var balances = (await db.ProcurementStockBalances.AsNoTracking()
                .Where(b => ids.Contains(b.ItemId) && activeWarehouses.Contains(b.WarehouseId))
                .GroupBy(b => b.ItemId)
                .Select(g => new { ItemId = g.Key, OnHand = g.Sum(b => b.OnHand), Reserved = g.Sum(b => b.Reserved) })
                .ToListAsync(ct))
            .ToDictionary(b => b.ItemId);
        var available = await ProductionStock.AvailableAsync(db, ids, null, today, ct);

        // Расход за историю — по дням (Ташкент), как на экране склада.
        var since = FieldClock.DayStartUtc(today.AddDays(-HistoryDays));
        var used = await db.ProcurementStockMovements.AsNoTracking()
            .Where(m => ids.Contains(m.ItemId) && m.CreatedAt >= since
                && (m.Kind == ProcurementMovementKind.Issue || m.Kind == ProcurementMovementKind.ProductionIssue || m.Kind == ProcurementMovementKind.ProductionReturn
                    || (m.Kind == ProcurementMovementKind.Reversal
                        && db.ProcurementStockDocuments.Any(d => d.Id == m.Document!.ReversesDocumentId && d.Kind == ProcurementStockDocumentKind.Issue))))
            .Select(m => new { m.ItemId, m.CreatedAt, m.Quantity })
            .ToListAsync(ct);
        var consumption = used.GroupBy(m => m.ItemId).ToDictionary(g => g.Key, g => g
            .GroupBy(m => FieldClock.LocalDate(m.CreatedAt)).ToDictionary(d => d.Key, d => -d.Sum(m => m.Quantity)));
        var lastMovement = (await db.ProcurementStockMovements.AsNoTracking().Where(m => ids.Contains(m.ItemId))
                .GroupBy(m => m.ItemId).Select(g => new { ItemId = g.Key, Last = g.Max(m => m.CreatedAt) }).ToListAsync(ct))
            .ToDictionary(x => x.ItemId, x => FieldClock.LocalDate(x.Last));

        // Открытые задания производства: невыданная потребность к дате задания.
        var open = new[] { ProductionOrderStatus.Released, ProductionOrderStatus.InProgress };
        var committedRows = await db.ProductionOrderLines.AsNoTracking()
            .Where(l => open.Contains(l.Order!.Status) && ids.Contains(l.ItemId))
            .Select(l => new { l.ItemId, l.RequiredQty, l.IssuedQty, l.ReturnedQty, l.Order!.PlannedDate, l.Order.Number })
            .ToListAsync(ct);
        var drafts = await db.ProductionOrders.CountAsync(o => o.Status == ProductionOrderStatus.Draft, ct);
        if (drafts > 0)
        {
            notes.Add($"Черновики производственных заданий ({drafts}) в потребность не входят — только выпущенные и в работе.");
        }

        // Подтверждённые поставки.
        var ctx = principal.UserId is { } userId && !principal.System
            ? await requests.ContextAsync(userId, principal.Permissions, ct)
            : SystemContext;
        var incoming = new Dictionary<Guid, List<SupplyIncoming>>();
        var unconverted = 0;
        try
        {
            foreach (var delivery in await arrivals.ExpectedAsync(ctx, null, 200, ct))
            {
                foreach (var line in delivery.Items.Where(i => i.ItemId is not null && i.Left > 0))
                {
                    if (line.Factor is not { } factor)
                    {
                        unconverted++;
                        continue;
                    }

                    if (!incoming.TryGetValue(line.ItemId!.Value, out var list))
                    {
                        incoming[line.ItemId.Value] = list = [];
                    }

                    list.Add(new SupplyIncoming(line.Left * factor, delivery.DeliveryDate, $"{delivery.Number} · {delivery.SupplierName}", delivery.Overdue));
                }
            }
        }
        catch (FieldForbiddenException)
        {
            notes.Add("Ожидаемые поставки вам не видны (их видят снабжение, склад, аналитик и директор) — в расчёте их нет, потребность может быть завышена.");
        }

        if (unconverted > 0)
        {
            notes.Add($"Строк поставок без пересчёта в единицу склада: {unconverted} — в расчёт не вошли.");
        }

        // Последний приход: цена, поставщик; срок поставки — у поставщика или по факту его заказов.
        var receipts = await db.ProcurementStockMovements.AsNoTracking()
            .Where(m => ids.Contains(m.ItemId) && m.Kind == ProcurementMovementKind.Receipt && m.UnitCost != null && m.UnitCost > 0)
            .Select(m => new { m.ItemId, m.CreatedAt, m.UnitCost, SupplierId = m.Document!.SupplierId })
            .ToListAsync(ct);
        var lastReceipt = receipts.GroupBy(r => r.ItemId).ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.CreatedAt).First());
        var supplierIds = lastReceipt.Values.Select(r => r.SupplierId).OfType<Guid>().Distinct().ToList();
        var suppliers = await db.ProcurementSuppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => new { s.Name, s.LeadTimeDays }, ct);
        var observed = (await db.ProcurementPurchaseOrders.AsNoTracking()
                .Where(o => supplierIds.Contains(o.SupplierId) && o.OrderedAt != null && o.DeliveredAt != null)
                .Select(o => new { o.SupplierId, o.OrderedAt, o.DeliveredAt })
                .ToListAsync(ct))
            .GroupBy(o => o.SupplierId)
            .ToDictionary(g => g.Key, g => (int)Math.Ceiling(g.Average(o => (o.DeliveredAt!.Value - o.OrderedAt!.Value).TotalDays)));
        var anyLeadTime = await db.ProcurementSuppliers.AnyAsync(s => s.LeadTimeDays != null, ct);
        if (!anyLeadTime)
        {
            notes.Add("Срок поставки не задан ни у одного поставщика — в расчёте он принят по умолчанию, уточните сроки в карточках поставщиков.");
        }

        var recommendations = new List<SupplyRecommendation>();
        var warehouse = new List<WarehouseItemView>();
        foreach (var item in items)
        {
            var balance = balances.GetValueOrDefault(item.Id);
            var onHand = balance?.OnHand ?? 0;
            var reserved = balance?.Reserved ?? 0;
            var usable = available.GetValueOrDefault(item.Id);
            var unusable = Math.Max(0, onHand - reserved - usable);
            var days = consumption.GetValueOrDefault(item.Id);
            var series = Enumerable.Range(0, HistoryDays)
                .Select(i => today.AddDays(-HistoryDays + 1 + i))
                .Select(d => new SeriesPoint(d, Math.Max(0, days?.GetValueOrDefault(d) ?? 0)))
                .ToList();
            var committed = committedRows.Where(c => c.ItemId == item.Id)
                .Select(c => new SupplyCommitment(c.PlannedDate, ProductionRules.LeftToIssue(c.RequiredQty, c.IssuedQty, c.ReturnedQty), c.Number))
                .Where(c => c.Quantity > 0)
                .ToList();
            var receipt = lastReceipt.GetValueOrDefault(item.Id);
            var supplier = receipt?.SupplierId is { } sid ? suppliers.GetValueOrDefault(sid) : null;
            int? lead = supplier?.LeadTimeDays ?? (receipt?.SupplierId is { } os && observed.TryGetValue(os, out var o) ? o : null);
            var leadSource = supplier?.LeadTimeDays is not null ? $"у поставщика «{supplier.Name}»"
                : lead is not null ? $"по факту заказов «{supplier?.Name}»" : null;
            var purchase = item.Units.Where(u => u.Factor > 1).OrderByDescending(u => u.Factor).FirstOrDefault();
            var input = new SupplyItemInput(item.Id, item.Sku, item.Name, item.UnitCode, onHand, reserved, unusable, item.MinQty, series, committed,
                incoming.GetValueOrDefault(item.Id) ?? [], lead, leadSource, receipt?.UnitCost,
                receipt is null ? null : $"приход {FieldClock.LocalDate(receipt.CreatedAt):dd.MM.yyyy}", supplier?.Name, purchase?.Factor, purchase?.UnitCode);
            var plan = SupplyPlanMath.Plan(input, today, options.DefaultLeadTimeDays, HistoryDays);
            recommendations.Add(plan);

            var consumed30 = series.TakeLast(30).Sum(p => p.Value);
            warehouse.Add(new WarehouseItemView(item.Id, item.Sku, item.Name, item.Category?.Name ?? "", item.UnitCode, onHand, reserved, unusable, plan.Available,
                item.MinQty, plan.AvgDailyConsumption, consumed30, plan.DaysOfCover, StatusOf(plan, onHand, lead ?? options.DefaultLeadTimeDays),
                receipt?.UnitCost, receipt?.UnitCost is { } price ? Math.Round(onHand * price, 0) : null, lastMovement.TryGetValue(item.Id, out var lm) ? lm : null));
        }

        var withoutPrice = recommendations.Count(r => r.UnitPrice is null);
        if (withoutPrice > 0)
        {
            notes.Add($"У {withoutPrice} из {recommendations.Count} позиций нет цены прихода — бюджет посчитан только по позициям с ценой.");
        }

        var historyMovements = used.Count;
        if (historyMovements < 30)
        {
            notes.Add($"Расход сырья за {HistoryDays} дн. — всего {historyMovements} движений: средний расход и прогноз ориентировочные.");
        }

        var ordered = recommendations.OrderBy(r => SupplyPlanMath.RiskRank(r.Risk)).ThenBy(r => r.ShortageDate ?? DateOnly.MaxValue)
            .ThenByDescending(r => r.Horizon(30)?.Required ?? 0).ThenBy(r => r.Name).ToList();
        var horizons = SupplyPlanMath.HorizonDays.Select(d =>
        {
            var need = ordered.Where(r => r.Horizon(d)!.Required > 0).ToList();
            return new SupplyHorizonTotal(d, need.Count, need.Sum(r => r.Horizon(d)!.Cost ?? 0), need.Count(r => r.UnitPrice is null));
        }).ToList();
        var supplierTotals = ordered.Where(r => r.Horizon(30)!.Required > 0)
            .GroupBy(r => r.Supplier ?? "Поставщик не определён")
            .Select(g => new SupplySupplierTotal(g.Key, g.Count(), g.Sum(r => r.Horizon(30)!.Cost ?? 0), g.Count(r => r.Risk == "critical")))
            .OrderByDescending(s => s.Critical).ThenByDescending(s => s.Budget30).ToList();
        return new SupplyPlanView(today, ordered, horizons,
            ordered.GroupBy(r => r.Risk).ToDictionary(g => g.Key, g => g.Count()), supplierTotals,
            warehouse.OrderBy(w => StatusRank(w.Status)).ThenBy(w => w.Name).ToList(),
            warehouse.GroupBy(w => w.Status).ToDictionary(g => g.Key, g => g.Count()), warehouse.Sum(w => w.Value ?? 0), withoutPrice, notes,
            ["OneBase → Снабжение → Склад (остатки, движения, партии)", "OneBase → Производство → Задания", "OneBase → Снабжение → Ожидаемые поставки",
                "OneBase → Снабжение → Поставщики"]);
    }

    /// <summary>Статус позиции склада: нет (закончилось при расходе), дефицит (запаса меньше срока поставки), ниже минимума, излишек (больше 90 дней), неликвид (90 дней без расхода), норма.</summary>
    internal static string StatusOf(SupplyRecommendation plan, decimal onHand, int leadTime) =>
        plan.AvgDailyConsumption > 0 && plan.Available <= 0 ? "out"
        : plan.DaysOfCover is { } d && d < leadTime ? "deficit"
        : plan.MinQty > 0 && plan.Available < plan.MinQty ? "below_min"
        : plan.DaysOfCover is > 90 ? "excess"
        : plan.AvgDailyConsumption == 0 && onHand > 0 ? "dead"
        : onHand <= 0 ? "empty"
        : "ok";

    private static int StatusRank(string status) => status switch
    {
        "out" => 0,
        "deficit" => 1,
        "below_min" => 2,
        "excess" => 3,
        "dead" => 4,
        "ok" => 5,
        _ => 6,
    };

    /// <summary>Контекст снабжения фоновой задачи: только чтение, видит поставки как директор (ничего не проводит).</summary>
    private static readonly ProcurementContext SystemContext =
        new(Guid.Empty, new HashSet<string> { Permissions.ProcurementRead, Permissions.ProcurementManage }, null, new HashSet<Guid>(), IsDirector: true);
}
