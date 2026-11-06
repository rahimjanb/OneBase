using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Domain.Procurement;

namespace OneBase.Application.Procurement;

/// <summary>
/// Партия в реестре: номер, позиция, номер партии производителя, поставщик, даты, остаток (выбранного склада или всех), State — ok, expiring
/// (истекает в пределах «за сколько дней» позиции), expired, unknown (срок не указан); DaysLeft — дней до срока (меньше нуля — просрочена;
/// без срока — null); Quality — статус качества, Usable — можно выдать (допущена и не просрочена), BlockReason — почему заблокирована.
/// </summary>
public sealed record ProcurementBatchRow(
    Guid Id,
    string Number,
    Guid ItemId,
    string Sku,
    string ItemName,
    string UnitCode,
    string? SupplierBatchNo,
    string? SupplierName,
    DateOnly? ManufacturedOn,
    DateOnly? ExpiresOn,
    DateOnly ReceivedOn,
    decimal OnHand,
    string State,
    int? DaysLeft,
    decimal? UnitCost,
    ProcurementBatchQuality Quality = ProcurementBatchQuality.Approved,
    bool Usable = true,
    string? BlockReason = null);

/// <summary>
/// Реестр партий: позиция, склад, состояние (usable — можно выдать, expiring, expired, control — на контроле качества, blocked — забракованные и
/// заблокированные), поиск по номеру, номеру производителя, артикулу, названию; withEmpty — и закончившиеся.
/// </summary>
public sealed record ProcurementBatchSearch(Guid? ItemId = null, Guid? WarehouseId = null, string? State = null, string? Search = null, bool WithEmpty = false, int Page = 1,
    int PageSize = 50);

public sealed record ProcurementBatchWarehouse(Guid WarehouseId, string Name, decimal OnHand);

/// <summary>Проверка качества партии в её карточке.</summary>
public sealed record ProcurementBatchCheck(Guid Id, string Number, ProcurementQualityCheckStatus Status, ProcurementBatchQuality? Decision, DateTimeOffset CreatedAt,
    DateTimeOffset? DecidedAt, string? DecidedByName);

/// <summary>Что можно сделать с партией: заблокировать и снять блокировку (назначенные по качеству), вернуть поставщику (кладовщик), прикрепить документ.</summary>
public sealed record ProcurementBatchAbilities(bool Block, bool Unblock, bool Return, bool Attach);

/// <summary>
/// Карточка партии: строка реестра, условия хранения позиции, документ появления, остаток по складам, движения; проверки качества, история
/// решений по качеству, документы партии (сертификаты), кто и когда решил последним, версия (для блокировки), что можно сделать.
/// </summary>
public sealed record ProcurementBatchCard(
    ProcurementBatchRow Batch,
    string? StorageConditions,
    Guid SourceDocumentId,
    string SourceNumber,
    IReadOnlyList<ProcurementBatchWarehouse> Warehouses,
    IReadOnlyList<ProcurementMovementRow> Movements,
    IReadOnlyList<ProcurementBatchCheck>? Checks = null,
    IReadOnlyList<ProcurementEventView>? History = null,
    IReadOnlyList<ProcurementDocumentView>? Documents = null,
    string? DecidedByName = null,
    DateTimeOffset? DecidedAt = null,
    uint Version = 0,
    ProcurementBatchAbilities? Can = null,
    Guid? SupplierId = null);

/// <summary>Партия позиции на складе для формы (выдача, списание, перемещение, инвентаризация): по FEFO, Usable — можно выдать, Quality — статус качества.</summary>
public sealed record ProcurementBatchOption(Guid Id, string Number, string? SupplierBatchNo, DateOnly? ExpiresOn, decimal OnHand, bool Usable,
    ProcurementBatchQuality Quality = ProcurementBatchQuality.Approved);

/// <summary>Партии сырья: реестр со сроками и качеством, карточка партии с историей по всем складам, партии позиции на складе для форм. Видят все, кто видит склад.</summary>
public sealed class ProcurementBatchService(IAppDbContext db, ProcurementStockLedger ledger, ProcurementStockService stock, ProcurementDocumentService documents)
{
    private sealed class BatchData
    {
        public Guid Id { get; init; }
        public string Number { get; init; } = "";
        public Guid ItemId { get; init; }
        public string Sku { get; init; } = "";
        public string ItemName { get; init; } = "";
        public string UnitCode { get; init; } = "";
        public string? SupplierBatchNo { get; init; }
        public string? SupplierName { get; init; }
        public DateOnly? ManufacturedOn { get; init; }
        public DateOnly? ExpiresOn { get; init; }
        public DateOnly ReceivedOn { get; init; }
        public decimal OnHand { get; init; }
        public int WarningDays { get; init; }
        public decimal? UnitCost { get; init; }
        public ProcurementBatchQuality Quality { get; init; }
        public string? BlockReason { get; init; }
    }

    private static void RequireStock(ProcurementContext ctx)
    {
        if (!ctx.SeesStock)
        {
            throw new FieldForbiddenException("Склад снабжения видят кладовщик, снабжение и директор.");
        }
    }

    private IQueryable<BatchData> Rows(Guid? warehouse)
    {
        var balances = db.ProcurementBatchBalances.AsNoTracking();
        if (warehouse is { } id)
        {
            balances = balances.Where(b => b.WarehouseId == id);
        }

        return from b in db.ProcurementBatches.AsNoTracking()
            let onHand = balances.Where(x => x.BatchId == b.Id).Sum(x => (decimal?)x.OnHand) ?? 0m
            select new BatchData
            {
                Id = b.Id,
                Number = b.Number,
                ItemId = b.ItemId,
                Sku = b.Item!.Sku,
                ItemName = b.Item.Name,
                UnitCode = b.Item.UnitCode,
                SupplierBatchNo = b.SupplierBatchNo,
                SupplierName = b.Supplier == null ? null : b.Supplier.Name,
                ManufacturedOn = b.ManufacturedOn,
                ExpiresOn = b.ExpiresOn,
                ReceivedOn = b.ReceivedOn,
                OnHand = onHand,
                WarningDays = b.Item.ExpiryWarningDays ?? ProcurementBatchRules.DefaultWarningDays,
                UnitCost = b.UnitCost,
                Quality = b.QualityStatus,
                BlockReason = b.BlockReason,
            };
    }

    private static ProcurementBatchRow Row(BatchData b, DateOnly today) => new(b.Id, b.Number, b.ItemId, b.Sku, b.ItemName, b.UnitCode, b.SupplierBatchNo, b.SupplierName,
        b.ManufacturedOn, b.ExpiresOn, b.ReceivedOn, b.OnHand, ProcurementBatchRules.ExpiryState(b.ExpiresOn, today, b.WarningDays),
        b.ExpiresOn is { } expires ? expires.DayNumber - today.DayNumber : null, b.UnitCost, b.Quality, ProcurementBatchRules.Usable(b.Quality, b.ExpiresOn, today),
        b.BlockReason);

    public async Task<PageResult<ProcurementBatchRow>> ListAsync(ProcurementContext ctx, ProcurementBatchSearch s, CancellationToken ct)
    {
        RequireStock(ctx);
        var today = ledger.Today;
        var q = Rows(s.WarehouseId);
        if (!s.WithEmpty)
        {
            q = q.Where(b => b.OnHand > 0);
        }

        if (s.ItemId is { } item)
        {
            q = q.Where(b => b.ItemId == item);
        }

        q = s.State switch
        {
            "usable" => q.Where(b => b.Quality == ProcurementBatchQuality.Approved && b.ExpiresOn >= today),
            "expiring" => q.Where(b => b.Quality != ProcurementBatchQuality.Rejected && b.ExpiresOn >= today && b.ExpiresOn!.Value.AddDays(-b.WarningDays) <= today),
            "expired" => q.Where(b => b.ExpiresOn < today),
            "control" => q.Where(b => b.Quality == ProcurementBatchQuality.Pending),
            "blocked" => q.Where(b => b.Quality == ProcurementBatchQuality.Rejected || b.Quality == ProcurementBatchQuality.Blocked),
            _ => q,
        };
        if (!string.IsNullOrWhiteSpace(s.Search))
        {
            var text = s.Search.Trim().ToLower();
            q = q.Where(b => b.Number.ToLower().Contains(text) || (b.SupplierBatchNo != null && b.SupplierBatchNo.ToLower().Contains(text)) || b.Sku.ToLower().Contains(text)
                             || b.ItemName.ToLower().Contains(text));
        }

        var total = await q.CountAsync(ct);
        var pageSize = Math.Clamp(s.PageSize, 5, 200);
        var page = Math.Max(s.Page, 1);
        var rows = await q.OrderBy(b => b.ExpiresOn).ThenBy(b => b.ReceivedOn).ThenBy(b => b.Number).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PageResult<ProcurementBatchRow>(rows.Select(b => Row(b, today)).ToList(), total, page, pageSize);
    }

    public async Task<ProcurementBatchCard> CardAsync(ProcurementContext ctx, Guid id, CancellationToken ct)
    {
        RequireStock(ctx);
        var today = ledger.Today;
        var data = await Rows(null).FirstOrDefaultAsync(b => b.Id == id, ct) ?? throw new FieldNotFoundException("Партия не найдена.");
        var batch = await db.ProcurementBatches.AsNoTracking().Where(b => b.Id == id)
            .Select(b => new
            {
                b.SourceDocumentId, b.Item!.StorageConditions, b.QualityDecidedAt, b.Version, b.SupplierId,
                Source = db.ProcurementStockDocuments.Where(d => d.Id == b.SourceDocumentId).Select(d => d.Number).First(),
                DecidedBy = db.Users.Where(u => u.Id == b.QualityDecidedById).Select(u => u.FullName).FirstOrDefault(),
            })
            .FirstAsync(ct);
        var warehouses = await db.ProcurementBatchBalances.AsNoTracking().Where(b => b.BatchId == id)
            .Join(db.ProcurementWarehouses.AsNoTracking(), b => b.WarehouseId, w => w.Id, (b, w) => new { w.Id, w.Name, w.IsDefault, b.OnHand })
            .OrderByDescending(x => x.OnHand > 0).ThenByDescending(x => x.IsDefault).ThenBy(x => x.Name)
            .Select(x => new ProcurementBatchWarehouse(x.Id, x.Name, x.OnHand))
            .ToListAsync(ct);
        var movements = await stock.MovementsAsync(ctx, new ProcurementMovementSearch(PageSize: 200, BatchId: id), ct);
        var checks = await db.ProcurementQualityChecks.AsNoTracking().Where(c => c.BatchId == id).OrderBy(c => c.CreatedAt)
            .Select(c => new ProcurementBatchCheck(c.Id, c.Number, c.Status, c.Decision, c.CreatedAt, c.DecidedAt, c.DecidedBy == null ? null : c.DecidedBy.FullName))
            .ToListAsync(ct);
        var history = await db.ProcurementAuditLogs.AsNoTracking().Where(e => e.DocType == ProcurementQualityService.BatchDoc && e.DocId == id)
            .OrderBy(e => e.CreatedAt).ThenBy(e => e.Id)
            .Select(e => new ProcurementEventView(e.Id, e.Action, e.Text, e.Actor == null ? "Система" : e.Actor.FullName, e.FromStatus, e.ToStatus, e.Comment, e.CreatedAt))
            .ToListAsync(ct);
        var row = Row(data, today);
        var stocked = warehouses.Any(w => w.OnHand > 0);
        var can = new ProcurementBatchAbilities(
            ctx.DecidesQuality && data.Quality == ProcurementBatchQuality.Approved,
            ctx.DecidesQuality && data.Quality == ProcurementBatchQuality.Blocked,
            ctx.Warehouse && stocked && data.Quality is ProcurementBatchQuality.Rejected or ProcurementBatchQuality.Blocked,
            ctx.Warehouse || ctx.Quality || ctx.DecidesQuality);
        return new ProcurementBatchCard(row, batch.StorageConditions, batch.SourceDocumentId, batch.Source, warehouses, movements.Items, checks, history,
            await documents.ListAsync(ctx, ProcurementQualityService.BatchDoc, id, ct), batch.DecidedBy, batch.QualityDecidedAt, batch.Version, can, batch.SupplierId);
    }

    /// <summary>Партии позиции на складе с остатком — по FEFO: годные первыми по сроку, затем остальные (на контроле, заблокированные, просроченные).</summary>
    public async Task<List<ProcurementBatchOption>> OptionsAsync(ProcurementContext ctx, Guid itemId, Guid warehouseId, CancellationToken ct)
    {
        RequireStock(ctx);
        var today = ledger.Today;
        var rows = await db.ProcurementBatchBalances.AsNoTracking().Where(b => b.WarehouseId == warehouseId && b.ItemId == itemId && b.OnHand > 0)
            .Select(b => new { b.BatchId, b.Batch!.Number, b.Batch.SupplierBatchNo, b.Batch.ExpiresOn, b.Batch.ReceivedOn, b.OnHand, b.Batch.QualityStatus })
            .ToListAsync(ct);
        return rows.OrderByDescending(r => ProcurementBatchRules.Usable(r.QualityStatus, r.ExpiresOn, today)).ThenBy(r => r.ExpiresOn ?? DateOnly.MaxValue).ThenBy(r => r.ReceivedOn)
            .ThenBy(r => r.Number, StringComparer.Ordinal)
            .Select(r => new ProcurementBatchOption(r.BatchId, r.Number, r.SupplierBatchNo, r.ExpiresOn, r.OnHand, ProcurementBatchRules.Usable(r.QualityStatus, r.ExpiresOn, today),
                r.QualityStatus))
            .ToList();
    }
}
