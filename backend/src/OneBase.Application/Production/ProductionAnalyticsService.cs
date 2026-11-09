using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Procurement;
using OneBase.Application.Reports;
using OneBase.Domain.Procurement;
using OneBase.Domain.Production;

namespace OneBase.Application.Production;

/// <summary>Вкладки «Производства»: кто что видит и счётчики — открытые проверки качества, задания к выдаче (выпущены и в работе).</summary>
public sealed record ProductionNav(bool Sees, bool Recipes, bool Produces, bool Warehouse, bool Quality, bool Analytics, int Open, int Orders);

public sealed record ProductionStockTile(Guid WarehouseId, string Name, int Items, decimal Value);

/// <summary>Счётчик и сумма (по цене партии, а без неё — по последней цене прихода).</summary>
public sealed record ProductionCountValue(int Count, decimal Value);

public sealed record ProductionSupplierReject(string Name, int Decided, int Rejected, decimal Percent);

public sealed record ProductionDeviationLine(Guid OrderId, string OrderNumber, string Item, string UnitCode, decimal Norm, decimal Actual, decimal Deviation, decimal? Percent,
    bool Overconsumed);

public sealed record ProductionDaysOfStock(Guid ItemId, string Sku, string Name, string UnitCode, decimal OnHand, decimal PerDay, decimal Days);

public sealed record ProductionPriceChange(string Name, string UnitCode, decimal FirstPrice, decimal LastPrice, decimal? ChangePercent);

/// <summary>
/// Обзор сырья и производства (раздел 19 плана) на сейчас и за период: запас по складам (сумма), на контроле качества (самая старая — дней),
/// истекает за 7 и 30 дней, просрочено, забраковано за период (доля по поставщикам), ниже минимума, расход и норма закрытых заданий (худшие
/// отклонения), дней запаса (самые малые), закупки сырья на согласовании, рост цен (снабжению и директору), задания в работе и просроченные.
/// </summary>
public sealed record ProductionOverview(
    DateOnly From,
    DateOnly To,
    Guid? WarehouseId,
    IReadOnlyList<ProductionStockTile> Stock,
    decimal StockValue,
    ProductionCountValue OnControl,
    int OldestControlDays,
    ProductionCountValue Expiring7,
    ProductionCountValue Expiring30,
    ProductionCountValue Expired,
    int Decided,
    int Rejected,
    IReadOnlyList<ProductionSupplierReject> RejectSuppliers,
    int BelowMin,
    int ClosedOrders,
    decimal NormTotal,
    decimal ActualTotal,
    int Overconsumed,
    IReadOnlyList<ProductionDeviationLine> Deviations,
    IReadOnlyList<ProductionDaysOfStock> LowDays,
    int SupplyApproval,
    int DirectorApproval,
    decimal ApprovalAmount,
    IReadOnlyList<ProductionPriceChange> Prices,
    int OpenOrders,
    int OverdueOrders,
    int AnalystApproval = 0,
    int FinanceApproval = 0);

/// <summary>
/// Аналитика сырья и производства: обзор и отчёты (предпросмотр, Excel, PDF — одна модель листов): «Партии и сроки», «Контроль качества»,
/// «Расход и норма», «Движения по партиям», «Сверка позиций и партий». Считается запросами к журналу при открытии (без витрин). Видят
/// производство, технолог, кладовщик, лаборатория, снабжение и директор; рост цен — снабжению и директору.
/// </summary>
public sealed partial class ProductionAnalyticsService(IAppDbContext db, ProcurementQualityService quality, ProcurementInsightService insight, TimeProvider clock)
{
    public const int MaxRows = 10_000;

    private static bool Uz => UiLanguage.IsUzbek;
    private static string L(string ru, string uz) => Uz ? UiLanguage.Uzbek(uz) : ru;
    private static JsonElement J(object? value) => JsonSerializer.SerializeToElement(value);
    private static AiReportColumn Text(string header) => new(header, ReportFormats.Text);
    private static AiReportColumn Num(string header, bool sum = false) => new(header, ReportFormats.Number, sum);
    private static AiReportColumn Money(string header) => new(header, ReportFormats.Money);
    private static AiReportColumn Int(string header, bool sum = false) => new(header, ReportFormats.Integer, sum);
    private static AiReportColumn Pct(string header) => new(header, ReportFormats.Percent, false);
    private static AiReportColumn Date(string header) => new(header, ReportFormats.Date, false);
    private static string? D(DateOnly? date) => date?.ToString("yyyy-MM-dd");
    private static string? D(DateTimeOffset? at) => at is { } v ? FieldClock.LocalDate(v).ToString("yyyy-MM-dd") : null;

    private static readonly ProcurementMovementKind[] Consumption =
        [ProcurementMovementKind.Issue, ProcurementMovementKind.ProductionIssue, ProcurementMovementKind.ProductionReturn];

    public static IReadOnlyList<ProcurementReportInfo> Kinds =>
    [
        new("batches", L("Партии и сроки", "Partiyalar va muddatlar"), L("Остатки партий: склад, поставщик, срок годности, статус качества, сумма", "Partiya qoldiqlari: ombor, yetkazib beruvchi, yaroqlilik muddati, sifat holati, summa")),
        new("quality", L("Контроль качества", "Sifat nazorati"), L("Проверки партий за период: шаг, решение, кто и когда решил, сколько дней шла проверка", "Davr bo'yicha partiyalar tekshiruvi: qadam, qaror, kim va qachon qaror qildi, tekshiruv necha kun davom etdi")),
        new("consumption", L("Расход и норма", "Sarf va me'yor"), L("Закрытые задания на выпуск: норма на фактический выход, по результатам смен, расход, отклонение по позициям", "Yopilgan ishlab chiqarish topshiriqlari: haqiqiy chiqishga me'yor, smenalar natijalari bo'yicha, sarf, pozitsiyalar bo'yicha og'ish")),
        new("batch_moves", L("Движения по партиям", "Partiyalar bo'yicha harakatlar"), L("Приход, выдача, перемещения, списания по партиям за период", "Davr bo'yicha partiyalar kirimi, berilishi, ko'chirilishi, hisobdan chiqarilishi")),
        new("reconcile", L("Сверка позиций и партий", "Pozitsiyalar va partiyalar solishtiruvi"), L("Остаток позиции на складе и сумма её партий — должны совпадать", "Ombordagi pozitsiya qoldig'i va uning partiyalari yig'indisi — mos kelishi kerak")),
        .. TechnologistKinds,
    ];

    private static void Require(ProcurementContext ctx)
    {
        if (!ctx.SeesProduction)
        {
            throw new FieldForbiddenException("Аналитику сырья и производства видят производство, технолог, кладовщик, лаборатория, снабжение и директор.");
        }
    }

    private DateOnly Today => ProcurementBatchRules.Today(clock);

    public async Task<ProductionNav> NavAsync(ProcurementContext ctx, CancellationToken ct)
    {
        if (!ctx.SeesProduction && !ctx.SeesQuality)
        {
            return new ProductionNav(false, false, false, false, false, false, 0, 0);
        }

        var q = await quality.NavAsync(ctx, ct);
        var orders = ctx.SeesProduction
            ? await db.ProductionOrders.CountAsync(o => o.Status == ProductionOrderStatus.Released || o.Status == ProductionOrderStatus.InProgress, ct)
            : 0;
        return new ProductionNav(ctx.SeesProduction, ctx.Recipes, ctx.Produces, ctx.Warehouse, q.Sees, ctx.SeesProduction, q.Open, orders);
    }

    public async Task<ProductionOverview> OverviewAsync(ProcurementContext ctx, ProcurementFilter f, Guid? warehouseId, CancellationToken ct)
    {
        Require(ctx);
        var today = Today;
        var warehouses = await db.ProcurementWarehouses.AsNoTracking().Where(w => w.IsActive && (warehouseId == null || w.Id == warehouseId))
            .OrderByDescending(w => w.IsDefault).ThenBy(w => w.Name).Select(w => new { w.Id, w.Name }).ToListAsync(ct);
        var ids = warehouses.Select(w => w.Id).ToList();
        var costs = await LastCostsAsync(ct);
        var balances = await db.ProcurementStockBalances.AsNoTracking().Where(b => ids.Contains(b.WarehouseId) && b.OnHand > 0)
            .Select(b => new { b.WarehouseId, b.ItemId, b.OnHand, b.Item!.TracksBatches, b.Item.MinQty, b.Reserved }).ToListAsync(ct);
        var batches = await db.ProcurementBatchBalances.AsNoTracking().Where(b => ids.Contains(b.WarehouseId) && b.OnHand > 0)
            .Select(b => new { b.WarehouseId, b.ItemId, b.OnHand, b.Batch!.UnitCost, b.Batch.QualityStatus, b.Batch.ExpiresOn, b.Batch.ReceivedOn }).ToListAsync(ct);
        decimal BatchValue(decimal onHand, decimal? cost, Guid itemId) => Math.Round(onHand * (cost ?? costs.GetValueOrDefault(itemId)), 2);
        var stock = warehouses.Select(w =>
        {
            var plain = balances.Where(b => b.WarehouseId == w.Id && !b.TracksBatches).Sum(b => Math.Round(b.OnHand * costs.GetValueOrDefault(b.ItemId), 2));
            var tracked = batches.Where(b => b.WarehouseId == w.Id).Sum(b => BatchValue(b.OnHand, b.UnitCost, b.ItemId));
            return new ProductionStockTile(w.Id, w.Name, balances.Count(b => b.WarehouseId == w.Id), plain + tracked);
        }).ToList();
        var control = batches.Where(b => b.QualityStatus == ProcurementBatchQuality.Pending).ToList();
        var exp7 = batches.Where(b => b.QualityStatus != ProcurementBatchQuality.Rejected && b.ExpiresOn is { } e && e >= today && e <= today.AddDays(7)).ToList();
        var exp30 = batches.Where(b => b.QualityStatus != ProcurementBatchQuality.Rejected && b.ExpiresOn is { } e && e >= today && e <= today.AddDays(30)).ToList();
        var expired = batches.Where(b => b.ExpiresOn is { } e && e < today).ToList();

        var decisions = await db.ProcurementQualityChecks.AsNoTracking()
            .Where(c => c.DecidedAt >= f.FromUtc && c.DecidedAt < f.ToUtc && c.Decision != null && (warehouseId == null || c.WarehouseId == warehouseId))
            .Select(c => new { c.Decision, Supplier = c.Supplier == null ? null : c.Supplier.Name }).ToListAsync(ct);
        var rejected = decisions.Count(d => d.Decision == ProcurementBatchQuality.Rejected);
        var suppliers = decisions.GroupBy(d => d.Supplier ?? L("без поставщика", "yetkazib beruvchisiz"))
            .Select(g => new ProductionSupplierReject(g.Key, g.Count(), g.Count(d => d.Decision == ProcurementBatchQuality.Rejected),
                Math.Round(100m * g.Count(d => d.Decision == ProcurementBatchQuality.Rejected) / g.Count(), 1)))
            .Where(s => s.Rejected > 0).OrderByDescending(s => s.Percent).ThenByDescending(s => s.Rejected).Take(5).ToList();

        // Ниже минимума — по доступному (годное и свободное) на выбранных складах.
        var minItems = await db.ProcurementItems.AsNoTracking().Where(i => i.IsActive && i.MinQty > 0).Select(i => new { i.Id, i.MinQty }).ToListAsync(ct);
        var free = await ProductionStock.AvailableAsync(db, minItems.Select(i => i.Id), warehouseId, today, ct);
        var belowMin = minItems.Count(i => free.GetValueOrDefault(i.Id) < i.MinQty);

        // Норма закрытых заданий — расход по результатам утверждённых смен (как в сверке задания), без смен — норма на фактический выход.
        var closedLines = await db.ProductionOrderLines.AsNoTracking()
            .Where(l => l.Order!.Status == ProductionOrderStatus.Completed && l.Order.CompletedAt >= f.FromUtc && l.Order.CompletedAt < f.ToUtc
                        && (warehouseId == null || l.Order.WarehouseId == warehouseId) && (f.DepartmentId == null || l.Order.DepartmentId == f.DepartmentId))
            .Select(l => new { l.OrderId, l.Order!.Number, l.Order.RecipeId, l.ItemId, l.Item!.Name, l.Item.UnitCode, l.ActualNormQty, Actual = l.IssuedQty - l.ReturnedQty })
            .ToListAsync(ct);
        var byShifts = await UsedAsync(closedLines.Select(l => (l.OrderId, l.RecipeId)).Distinct().ToList(), ct);
        var overThreshold = await OverThresholdAsync(ct);
        var closed = closedLines.Select(l =>
        {
            decimal? norm = byShifts.TryGetValue(l.OrderId, out var items) && items.TryGetValue(l.ItemId, out var u) ? u : l.ActualNormQty;
            return new { l.OrderId, l.Number, l.Name, l.UnitCode, ActualNormQty = norm, l.Actual, Overconsumed = norm is { } n && ProductionRules.Overconsumed(l.Actual, n, overThreshold) };
        }).ToList();
        var deviations = closed.Where(l => l.ActualNormQty is not null).Select(l =>
            {
                var (dq, dp) = ProductionRules.Deviation(l.Actual, l.ActualNormQty!.Value);
                return new ProductionDeviationLine(l.OrderId, l.Number, l.Name, l.UnitCode, l.ActualNormQty.Value, l.Actual, dq, dp, l.Overconsumed);
            })
            .OrderByDescending(d => d.Percent ?? 0).ThenByDescending(d => d.Deviation).Take(8).ToList();

        // Дней запаса: остаток ÷ средний расход в день за 30 дней (выдачи, в том числе в производство, за вычетом возвратов).
        var since = FieldClock.DayStartUtc(today.AddDays(-30));
        var used = await db.ProcurementStockMovements.AsNoTracking()
            .Where(m => Consumption.Contains(m.Kind) && m.CreatedAt >= since && ids.Contains(m.WarehouseId))
            .GroupBy(m => m.ItemId).Select(g => new { ItemId = g.Key, Qty = -g.Sum(m => m.Quantity) }).ToListAsync(ct);
        var onHand = balances.GroupBy(b => b.ItemId).ToDictionary(g => g.Key, g => g.Sum(b => b.OnHand));
        var usedIds = used.Where(u => u.Qty > 0).Select(u => u.ItemId).ToList();
        var names = await db.ProcurementItems.AsNoTracking().Where(i => usedIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var lowDays = used.Where(u => u.Qty > 0 && names.ContainsKey(u.ItemId)).Select(u =>
            {
                var perDay = Math.Round(u.Qty / 30m, 3);
                var have = onHand.GetValueOrDefault(u.ItemId);
                var item = names[u.ItemId];
                return new ProductionDaysOfStock(u.ItemId, item.Sku, item.Name, item.UnitCode, have, perDay, perDay > 0 ? Math.Round(have / perDay, 1) : 0);
            })
            .OrderBy(d => d.Days).Take(8).ToList();

        var approvals = await db.ProcurementRequests.AsNoTracking()
            .Where(r => r.Status == ProcurementRequestStatus.SupplyApproval || r.Status == ProcurementRequestStatus.AnalystApproval
                        || (r.RawRoute && (r.Status == ProcurementRequestStatus.DirectorApproval || r.Status == ProcurementRequestStatus.FinanceApproval)))
            .Select(r => new { r.Status, r.PurchaseTotal }).ToListAsync(ct);
        IReadOnlyList<ProductionPriceChange> prices = [];
        if (ctx.ReadsAll || ctx.IsDirector)
        {
            var report = await insight.PricesAsync(ctx, new ProcurementPriceQuery(f.From, f.To), ct);
            prices = report.Items.Where(i => i.ChangePercent is > 0).OrderByDescending(i => i.ChangePercent).Take(5)
                .Select(i => new ProductionPriceChange(i.Name, i.UnitCode, i.FirstPrice, i.LastPrice, i.ChangePercent)).ToList();
        }

        var open = await db.ProductionOrders.AsNoTracking()
            .Where(o => (o.Status == ProductionOrderStatus.Released || o.Status == ProductionOrderStatus.InProgress) && (warehouseId == null || o.WarehouseId == warehouseId))
            .Select(o => o.PlannedDate).ToListAsync(ct);
        return new ProductionOverview(f.From, f.To, warehouseId, stock, stock.Sum(s => s.Value),
            new ProductionCountValue(control.Count, control.Sum(b => BatchValue(b.OnHand, b.UnitCost, b.ItemId))),
            control.Count == 0 ? 0 : today.DayNumber - control.Min(b => b.ReceivedOn).DayNumber,
            new ProductionCountValue(exp7.Count, exp7.Sum(b => BatchValue(b.OnHand, b.UnitCost, b.ItemId))),
            new ProductionCountValue(exp30.Count, exp30.Sum(b => BatchValue(b.OnHand, b.UnitCost, b.ItemId))),
            new ProductionCountValue(expired.Count, expired.Sum(b => BatchValue(b.OnHand, b.UnitCost, b.ItemId))),
            decisions.Count, rejected, suppliers, belowMin,
            closed.Select(l => l.OrderId).Distinct().Count(), closed.Sum(l => l.ActualNormQty ?? 0), closed.Sum(l => l.Actual), closed.Count(l => l.Overconsumed), deviations,
            lowDays, approvals.Count(a => a.Status == ProcurementRequestStatus.SupplyApproval), approvals.Count(a => a.Status == ProcurementRequestStatus.DirectorApproval),
            approvals.Sum(a => a.PurchaseTotal), prices, open.Count, open.Count(d => d < today),
            approvals.Count(a => a.Status == ProcurementRequestStatus.AnalystApproval), approvals.Count(a => a.Status == ProcurementRequestStatus.FinanceApproval));
    }

    /// <summary>Последняя цена прихода позиции (за единицу карточки) — для запаса без цены партии.</summary>
    private async Task<Dictionary<Guid, decimal>> LastCostsAsync(CancellationToken ct)
    {
        var rows = await db.ProcurementStockMovements.AsNoTracking().Where(m => m.Kind == ProcurementMovementKind.Receipt && m.UnitCost != null)
            .GroupBy(m => m.ItemId).Select(g => new { ItemId = g.Key, At = g.Max(m => m.CreatedAt) }).ToListAsync(ct);
        var result = new Dictionary<Guid, decimal>();
        foreach (var chunk in rows.Chunk(500))
        {
            var ids = chunk.Select(r => r.ItemId).ToList();
            var costs = await db.ProcurementStockMovements.AsNoTracking()
                .Where(m => m.Kind == ProcurementMovementKind.Receipt && m.UnitCost != null && ids.Contains(m.ItemId))
                .Select(m => new { m.ItemId, m.CreatedAt, m.UnitCost }).ToListAsync(ct);
            foreach (var g in costs.GroupBy(c => c.ItemId))
            {
                result[g.Key] = g.OrderByDescending(c => c.CreatedAt).First().UnitCost!.Value;
            }
        }

        return result;
    }

    public async Task<AiReportContent> ReportAsync(ProcurementContext ctx, string kind, ProcurementFilter f, Guid? warehouseId, CancellationToken ct)
    {
        Require(ctx);
        var info = Kinds.FirstOrDefault(k => k.Key == kind) ?? throw new FieldNotFoundException("Такого отчёта нет.");
        var sheet = kind switch
        {
            "batches" => await BatchesAsync(warehouseId, ct),
            "quality" => await QualityAsync(f, warehouseId, ct),
            "consumption" => await ConsumptionAsync(f, warehouseId, ct),
            "batch_moves" => await BatchMovesAsync(f, warehouseId, ct),
            "output" => await OutputReportAsync(f, ct),
            "defects" => await DefectsReportAsync(f, ct),
            "check" => await CheckReportAsync(f, false, ct),
            "packaging" => await CheckReportAsync(f, true, ct),
            "semi" => await SemiReportAsync(f, warehouseId, ct),
            "recipes" => await RecipesReportAsync(ct),
            _ => await ReconcileAsync(warehouseId, ct),
        };
        var period = kind is "batches" or "reconcile" or "recipes" ? L($"на {Today:dd.MM.yyyy}", $"{Today:dd.MM.yyyy} holatiga") : $"{f.From:dd.MM.yyyy} — {f.To:dd.MM.yyyy}";
        return new AiReportContent($"{info.Title}, {period}", [sheet], [L("Производство → Отчёты", "Ishlab chiqarish → Hisobotlar")]);
    }

    private static string Cap(int total) => total > MaxRows ? L($"Показаны первые {MaxRows} строк из {total}.", $"{total} qatordan birinchi {MaxRows} tasi ko'rsatilgan.") : "";

    private static string QualityLabel(ProcurementBatchQuality q) => q switch
    {
        ProcurementBatchQuality.Approved => L("Допущена", "Ruxsat berilgan"),
        ProcurementBatchQuality.Pending => L("На контроле", "Nazoratda"),
        ProcurementBatchQuality.Rejected => L("Забракована", "Yaroqsiz"),
        _ => L("Заблокирована", "Bloklangan"),
    };

    private async Task<AiReportSheet> BatchesAsync(Guid? warehouseId, CancellationToken ct)
    {
        var today = Today;
        var q = db.ProcurementBatchBalances.AsNoTracking().Where(b => b.OnHand > 0 && (warehouseId == null || b.WarehouseId == warehouseId));
        var total = await q.CountAsync(ct);
        var warehouses = await db.ProcurementWarehouses.AsNoTracking().ToDictionaryAsync(w => w.Id, w => w.Name, ct);
        var rows = await q.OrderBy(b => b.Batch!.ExpiresOn == null).ThenBy(b => b.Batch!.ExpiresOn).ThenBy(b => b.Batch!.Number).Take(MaxRows)
            .Select(b => new
            {
                b.Batch!.Number, b.Batch.SupplierBatchNo, b.Batch.Item!.Sku, b.Batch.Item.Name, b.Batch.Item.UnitCode, b.WarehouseId,
                Supplier = b.Batch.Supplier == null ? null : b.Batch.Supplier.Name, b.Batch.ReceivedOn, b.Batch.ManufacturedOn, b.Batch.ExpiresOn, b.Batch.QualityStatus, b.OnHand,
                b.Batch.UnitCost,
            })
            .ToListAsync(ct);
        return new AiReportSheet(L("Партии", "Partiyalar"),
            [
                Text(L("Партия", "Partiya")), Text(L("Партия поставщика", "Yetkazib beruvchi partiyasi")), Text(L("Артикул", "Artikul")), Text(L("Позиция", "Pozitsiya")),
                Text(L("Ед.", "Birl.")), Text(L("Склад", "Ombor")), Text(L("Поставщик", "Yetkazib beruvchi")), Date(L("Приход", "Kirim")),
                Date(L("Произведена", "Ishlab chiqarilgan")), Date(L("Годна до", "Yaroqli")), Int(L("Дней до срока", "Muddatgacha kun")), Text(L("Качество", "Sifat")),
                Num(L("Остаток", "Qoldiq")), Money(L("Цена", "Narx")) with { Sum = false }, Money(L("Сумма", "Summa")),
            ],
            rows.Select(r => (IReadOnlyList<JsonElement>)[
                J(r.Number), J(r.SupplierBatchNo), J(r.Sku), J(r.Name), J(r.UnitCode), J(warehouses.GetValueOrDefault(r.WarehouseId)), J(r.Supplier), J(D(r.ReceivedOn)),
                J(D(r.ManufacturedOn)), J(D(r.ExpiresOn)), J(r.ExpiresOn is { } e ? e.DayNumber - today.DayNumber : null), J(QualityLabel(r.QualityStatus)), J(r.OnHand),
                J(r.UnitCost), J(r.UnitCost is { } c ? Math.Round(c * r.OnHand, 2) : null),
            ]).ToList(), true, Cap(total));
    }

    private async Task<AiReportSheet> QualityAsync(ProcurementFilter f, Guid? warehouseId, CancellationToken ct)
    {
        var q = db.ProcurementQualityChecks.AsNoTracking().Where(c => c.CreatedAt >= f.FromUtc && c.CreatedAt < f.ToUtc && (warehouseId == null || c.WarehouseId == warehouseId));
        var total = await q.CountAsync(ct);
        var rows = await q.OrderBy(c => c.CreatedAt).Take(MaxRows)
            .Select(c => new
            {
                // Поставка до приёмки — без партии: вместо неё номер поставки (AR-), партия поставщика — из строки поставки.
                c.Number, Batch = c.Batch != null ? c.Batch.Number : c.Arrival == null ? null : c.Arrival.Number,
                SupplierBatchNo = c.Batch != null ? c.Batch.SupplierBatchNo : db.ProcurementArrivalLines.Where(l => l.QualityCheckId == c.Id).Select(l => l.SupplierBatchNo).FirstOrDefault(),
                Item = c.Item!.Name, Supplier = c.Supplier == null ? null : c.Supplier.Name,
                c.Control, c.Status, c.Decision, c.DecisionComment, c.CreatedAt, c.DecidedAt, Decider = c.DecidedBy == null ? null : c.DecidedBy.FullName,
            })
            .ToListAsync(ct);
        return new AiReportSheet(L("Проверки", "Tekshiruvlar"),
            [
                Text(L("Проверка", "Tekshiruv")), Text(L("Партия", "Partiya")), Text(L("Партия поставщика", "Yetkazib beruvchi partiyasi")), Text(L("Позиция", "Pozitsiya")),
                Text(L("Поставщик", "Yetkazib beruvchi")), Text(L("Контроль", "Nazorat")), Text(L("Шаг", "Qadam")), Text(L("Решение", "Qaror")), Date(L("Заведена", "Ochilgan")),
                Date(L("Решено", "Qaror qilingan")), Int(L("Дней", "Kun")), Text(L("Кто решил", "Kim qaror qildi")), Text(L("Комментарий", "Izoh")),
            ],
            rows.Select(r => (IReadOnlyList<JsonElement>)[
                J(r.Number), J(r.Batch), J(r.SupplierBatchNo), J(r.Item), J(r.Supplier),
                J(r.Control == ProcurementQualityControl.Lab ? L("документы и лаборатория", "hujjatlar va laboratoriya") : L("документы", "hujjatlar")),
                J(ProcurementQualityRules.Label(r.Status)), J(r.Decision is { } d ? QualityLabel(d) : null), J(D(r.CreatedAt)), J(D(r.DecidedAt)),
                J((FieldClock.LocalDate(r.DecidedAt ?? DateTimeOffset.UtcNow).DayNumber - FieldClock.LocalDate(r.CreatedAt).DayNumber)), J(r.Decider), J(r.DecisionComment),
            ]).ToList(), false, Cap(total));
    }

    private async Task<AiReportSheet> ConsumptionAsync(ProcurementFilter f, Guid? warehouseId, CancellationToken ct)
    {
        var q = db.ProductionOrderLines.AsNoTracking()
            .Where(l => l.Order!.Status == ProductionOrderStatus.Completed && l.Order.CompletedAt >= f.FromUtc && l.Order.CompletedAt < f.ToUtc
                        && (warehouseId == null || l.Order.WarehouseId == warehouseId) && (f.DepartmentId == null || l.Order.DepartmentId == f.DepartmentId));
        var total = await q.CountAsync(ct);
        var rows = await q.OrderBy(l => l.Order!.CompletedAt).ThenBy(l => l.Order!.Number).ThenBy(l => l.LineNo).Take(MaxRows)
            .Select(l => new
            {
                l.OrderId, l.Order!.RecipeId, l.ItemId, l.Order.Number, l.Order.CompletedAt, Department = l.Order.Department!.Name, l.Order.Recipe!.Code, l.Order.Recipe.VersionNo,
                Recipe = l.Order.Recipe.Name, l.Order.Recipe.OutputUnit, l.Order.PlannedQty, l.Order.OutputQty, l.Item!.Sku, Item = l.Item.Name, l.Item.UnitCode, l.RequiredQty,
                l.ActualNormQty, l.IssuedQty, l.ReturnedQty,
            })
            .ToListAsync(ct);
        // Отклонение и перерасход — к расходу по результатам утверждённых смен (как в сверке задания), без смен — к норме на факт.
        var used = await UsedAsync(rows.Select(r => (r.OrderId, r.RecipeId)).Distinct().ToList(), ct);
        var overThreshold = await OverThresholdAsync(ct);
        return new AiReportSheet(L("Расход и норма", "Sarf va me'yor"),
            [
                Text(L("Задание", "Topshiriq")), Date(L("Закрыто", "Yopilgan")), Text(L("Цех", "Sex")), Text(L("Рецептура", "Retseptura")), Text(L("Выход, ед.", "Chiqish, birl.")),
                Num(L("План", "Reja")), Num(L("Факт выхода", "Haqiqiy chiqish")), Text(L("Артикул", "Artikul")), Text(L("Позиция", "Pozitsiya")), Text(L("Ед.", "Birl.")),
                Num(L("Потребность на план", "Rejaga ehtiyoj")), Num(L("Норма на факт", "Haqiqatga me'yor"), true),
                Num(L("По результатам смен", "Smenalar natijalari bo'yicha"), true), Num(L("Выдано", "Berilgan"), true),
                Num(L("Возвращено", "Qaytarilgan"), true), Num(L("Расход", "Sarf"), true), Num(L("Отклонение", "Og'ish"), true), Pct(L("Отклонение, %", "Og'ish, %")),
                Text(L("Перерасход", "Ortiqcha sarf")),
            ],
            rows.Select(r =>
            {
                var actual = r.IssuedQty - r.ReturnedQty;
                decimal? byResults = used.TryGetValue(r.OrderId, out var items) && items.TryGetValue(r.ItemId, out var u) ? u : null;
                var basis = byResults ?? r.ActualNormQty;
                var (dq, dp) = basis is { } n ? ProductionRules.Deviation(actual, n) : (0m, (decimal?)null);
                return (IReadOnlyList<JsonElement>)[
                    J(r.Number), J(D(r.CompletedAt)), J(r.Department), J($"{r.Code} v{r.VersionNo} — {r.Recipe}"), J(r.OutputUnit), J(r.PlannedQty), J(r.OutputQty), J(r.Sku),
                    J(r.Item), J(r.UnitCode), J(r.RequiredQty), J(r.ActualNormQty), J(byResults), J(r.IssuedQty), J(r.ReturnedQty), J(actual), J(dq), J(dp),
                    J(basis is { } b && ProductionRules.Overconsumed(actual, b, overThreshold) ? L("да", "ha") : null),
                ];
            }).ToList(), true, Cap(total));
    }

    private async Task<AiReportSheet> BatchMovesAsync(ProcurementFilter f, Guid? warehouseId, CancellationToken ct)
    {
        var q = db.ProcurementStockMovements.AsNoTracking()
            .Where(m => m.BatchId != null && m.CreatedAt >= f.FromUtc && m.CreatedAt < f.ToUtc && (warehouseId == null || m.WarehouseId == warehouseId));
        var total = await q.CountAsync(ct);
        var warehouses = await db.ProcurementWarehouses.AsNoTracking().ToDictionaryAsync(w => w.Id, w => w.Name, ct);
        var rows = await q.OrderBy(m => m.CreatedAt).Take(MaxRows)
            .Select(m => new
            {
                m.CreatedAt, m.Document!.Number, m.Kind, m.Item!.Sku, Item = m.Item.Name, m.Item.UnitCode, Batch = m.Batch!.Number, m.Batch.ExpiresOn, m.WarehouseId, m.Quantity,
                m.BatchAfter, Actor = m.Document.Actor == null ? null : m.Document.Actor.FullName,
            })
            .ToListAsync(ct);
        return new AiReportSheet(L("Движения", "Harakatlar"),
            [
                Date(L("Дата", "Sana")), Text(L("Документ", "Hujjat")), Text(L("Вид", "Turi")), Text(L("Артикул", "Artikul")), Text(L("Позиция", "Pozitsiya")),
                Text(L("Ед.", "Birl.")), Text(L("Партия", "Partiya")), Date(L("Годна до", "Yaroqli")), Text(L("Склад", "Ombor")), Num(L("Количество", "Miqdor")),
                Num(L("Остаток партии", "Partiya qoldig'i")), Text(L("Кто", "Kim")),
            ],
            rows.Select(r => (IReadOnlyList<JsonElement>)[
                J(D(r.CreatedAt)), J(r.Number), J(KindLabel(r.Kind)), J(r.Sku), J(r.Item), J(r.UnitCode), J(r.Batch), J(D(r.ExpiresOn)),
                J(warehouses.GetValueOrDefault(r.WarehouseId)), J(r.Quantity), J(r.BatchAfter), J(r.Actor),
            ]).ToList(), false, Cap(total));
    }

    private async Task<AiReportSheet> ReconcileAsync(Guid? warehouseId, CancellationToken ct)
    {
        var items = await db.ProcurementStockBalances.AsNoTracking()
            .Where(b => b.Item!.TracksBatches && (warehouseId == null || b.WarehouseId == warehouseId) && b.OnHand != 0)
            .Select(b => new { b.WarehouseId, b.ItemId, b.Item!.Sku, b.Item.Name, b.Item.UnitCode, b.OnHand }).ToListAsync(ct);
        var sums = await db.ProcurementBatchBalances.AsNoTracking().Where(b => warehouseId == null || b.WarehouseId == warehouseId)
            .GroupBy(b => new { b.WarehouseId, b.ItemId }).Select(g => new { g.Key.WarehouseId, g.Key.ItemId, Qty = g.Sum(b => b.OnHand), N = g.Count(b => b.OnHand > 0) })
            .ToListAsync(ct);
        var warehouses = await db.ProcurementWarehouses.AsNoTracking().ToDictionaryAsync(w => w.Id, w => w.Name, ct);
        var rows = items.Select(i =>
        {
            var s = sums.FirstOrDefault(x => x.WarehouseId == i.WarehouseId && x.ItemId == i.ItemId);
            return (i, Qty: s?.Qty ?? 0, N: s?.N ?? 0);
        }).OrderByDescending(r => r.i.OnHand != r.Qty).ThenBy(r => r.i.Name).ToList();
        return new AiReportSheet(L("Сверка", "Solishtiruv"),
            [
                Text(L("Склад", "Ombor")), Text(L("Артикул", "Artikul")), Text(L("Позиция", "Pozitsiya")), Text(L("Ед.", "Birl.")), Num(L("Остаток позиции", "Pozitsiya qoldig'i")),
                Num(L("Сумма партий", "Partiyalar yig'indisi")), Int(L("Партий", "Partiyalar")), Num(L("Расхождение", "Farq")), Text(L("Итог", "Natija")),
            ],
            rows.Select(r => (IReadOnlyList<JsonElement>)[
                J(warehouses.GetValueOrDefault(r.i.WarehouseId)), J(r.i.Sku), J(r.i.Name), J(r.i.UnitCode), J(r.i.OnHand), J(r.Qty), J(r.N), J(r.i.OnHand - r.Qty),
                J(r.i.OnHand == r.Qty ? L("совпадает", "mos") : L("расхождение", "farq bor")),
            ]).ToList(), false,
            rows.All(r => r.i.OnHand == r.Qty) ? L("Все позиции совпадают с суммой партий.", "Barcha pozitsiyalar partiyalar yig'indisiga mos.") : null);
    }

    private static string KindLabel(ProcurementMovementKind kind) => kind switch
    {
        ProcurementMovementKind.Receipt => L("Приход", "Kirim"),
        ProcurementMovementKind.Issue => L("Выдача", "Berish"),
        ProcurementMovementKind.WriteOff => L("Списание", "Hisobdan chiqarish"),
        ProcurementMovementKind.Adjustment => L("Инвентаризация", "Inventarizatsiya"),
        ProcurementMovementKind.TransferOut => L("Перемещение: ушло", "Ko'chirish: chiqdi"),
        ProcurementMovementKind.TransferIn => L("Перемещение: пришло", "Ko'chirish: keldi"),
        ProcurementMovementKind.BatchEntry => L("Ввод партий", "Partiyalarni kiritish"),
        ProcurementMovementKind.Reversal => L("Сторно", "Storno"),
        ProcurementMovementKind.SupplierReturn => L("Возврат поставщику", "Yetkazib beruvchiga qaytarish"),
        ProcurementMovementKind.ProductionIssue => L("Выдача в производство", "Ishlab chiqarishga berish"),
        _ => L("Возврат из производства", "Ishlab chiqarishdan qaytarish"),
    };
}
