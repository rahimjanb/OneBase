using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Work;
using OneBase.Domain.Audit;
using OneBase.Domain.Identity;
using OneBase.Domain.Procurement;

namespace OneBase.Application.Procurement;

/// <summary>Строка остатков склада: позиция каталога, на складе, в резерве, доступно, минимум, «ниже минимума».</summary>
public sealed record ProcurementStockRow(
    Guid ItemId,
    string Sku,
    string Name,
    Guid CategoryId,
    string CategoryName,
    string UnitCode,
    decimal OnHand,
    decimal Reserved,
    decimal Available,
    decimal MinQty,
    bool Low,
    bool IsActive,
    string? Barcode,
    string? Description,
    DateTimeOffset? LastMovementAt,
    bool TracksBatches = false,
    decimal Expired = 0,
    DateOnly? NearestExpiry = null,
    int? ShelfLifeDays = null,
    int? ExpiryWarningDays = null,
    string? StorageConditions = null,
    decimal OnControl = 0,
    decimal Blocked = 0,
    ProcurementQualityControl QualityControl = ProcurementQualityControl.None);

/// <summary>
/// Остатки: поиск (SKU, название, штрихкод), категория, фильтр (low — ниже минимума, reserved — есть резерв, empty — нет на складе,
/// inactive — отключённые, expired — есть просроченные партии, expiring — у ближайшей годной партии срок скоро, control — есть партии на
/// контроле качества, blocked — забракованные или заблокированные), склад (не указан — сумма по всем складам). Expired — просроченный
/// остаток допущенных партий, OnControl — на контроле, Blocked — забракованный и заблокированный: всё это не выдаётся и в «доступно» не
/// входит. NearestExpiry — ближайший срок годной партии.
/// </summary>
public sealed record ProcurementStockSearch(string? Search = null, Guid? CategoryId = null, string? Filter = null, string? Sort = null, bool Desc = false, int Page = 1,
    int PageSize = 50, Guid? WarehouseId = null);

public sealed record ProcurementStockSummary(int Items, int Low, int Empty, int WithReserve, int ToIssueRequests, int Expired = 0, int Expiring = 0, int Tracked = 0,
    int OnControl = 0, int Blocked = 0);

/// <summary>
/// Позиция каталога из формы: SKU, название, категория, единица, штрихкод, описание, минимальный остаток, активна; профиль сырья — учёт
/// партий (null — не менять), срок хранения, за сколько дней напоминать о сроке, условия хранения, контроль качества при приходе (null — не
/// менять; не «нет» — только у позиции с партиями), единицы закупки с коэффициентом (null — не менять).
/// </summary>
public sealed record ProcurementCatalogInput(string? Sku, string? Name, Guid? CategoryId, string? UnitCode, string? Barcode, string? Description, decimal MinQty, bool IsActive = true,
    bool? TracksBatches = null, int? ShelfLifeDays = null, int? ExpiryWarningDays = null, string? StorageConditions = null, ProcurementQualityControl? QualityControl = null,
    IReadOnlyList<ProcurementItemUnitInput>? Units = null);

/// <summary>Товар каталога для формы заявки: сколько доступно на складе сейчас; Units — единицы закупки (приход в них).</summary>
public sealed record ProcurementCatalogOption(Guid Id, string Sku, string Name, Guid CategoryId, string CategoryName, string UnitCode, decimal OnHand, decimal Available,
    bool TracksBatches = false, int? ShelfLifeDays = null, IReadOnlyList<ProcurementItemUnitView>? Units = null);

public sealed record ProcurementMovementRow(
    Guid Id,
    DateTimeOffset CreatedAt,
    Guid DocumentId,
    string DocumentNumber,
    ProcurementStockDocumentKind DocumentKind,
    Guid ItemId,
    string Sku,
    string ItemName,
    string UnitCode,
    ProcurementMovementKind Kind,
    decimal Quantity,
    decimal BalanceBefore,
    decimal BalanceAfter,
    decimal? UnitCost,
    string? ActorName,
    string? DepartmentName,
    Guid? RequestId,
    string? RequestNumber,
    string? Receiver,
    string? Reference,
    string? Comment,
    Guid? OrderId = null,
    string? OrderNumber = null,
    string? WarehouseName = null,
    string? CounterpartName = null,
    Guid? BatchId = null,
    string? BatchNumber = null,
    DateOnly? BatchExpiresOn = null,
    decimal? BatchAfter = null,
    string? ReversedBy = null,
    Guid? QualityCheckId = null,
    Guid? TenderId = null);

/// <summary>
/// Журнал движений: позиция, вид (Receipt, Issue, WriteOff, Adjustment, TransferOut, TransferIn), даты (Ташкент), поиск по SKU, названию,
/// номеру документа, склад (не указан — все склады).
/// </summary>
public sealed record ProcurementMovementSearch(Guid? ItemId = null, string? Kind = null, DateOnly? From = null, DateOnly? To = null, string? Search = null, int Page = 1,
    int PageSize = 50, Guid? WarehouseId = null, Guid? BatchId = null);

/// <summary>
/// Карточка позиции: остаток (по выбранному складу или сумма по всем), движения, резервы заявок; Warehouses — остаток на каждом складе, где
/// позиция есть или была.
/// </summary>
public sealed record ProcurementItemCard(ProcurementStockRow Item, IReadOnlyList<ProcurementMovementRow> Movements, IReadOnlyList<ProcurementItemReservation> Reservations,
    IReadOnlyList<ProcurementItemWarehouse>? Warehouses = null, IReadOnlyList<ProcurementItemUnitView>? Units = null);

public sealed record ProcurementItemWarehouse(Guid WarehouseId, string Name, bool IsDefault, decimal OnHand, decimal Reserved, decimal Available);

public sealed record ProcurementItemReservation(Guid RequestId, string RequestNumber, string RequestTitle, string DepartmentName, decimal Quantity, DateTimeOffset CreatedAt);

/// <summary>
/// Строка складского документа: позиция и количество (для инвентаризации — фактический остаток), цена за единицу (приход). У позиции с
/// партиями: приход — Batches (партии со сроком, сумма = количество), выдача, списание, перемещение — BatchId (не указана — FEFO),
/// инвентаризация — BatchId и факт этой партии. UnitCode — приход в единице закупки позиции: количество, партии и цена — в ней, на склад
/// пересчитываются в единицу карточки.
/// </summary>
public sealed record ProcurementStockLineInput(Guid ItemId, decimal Quantity, decimal? UnitCost = null, Guid? BatchId = null, IReadOnlyList<ProcurementBatchInput>? Batches = null,
    string? UnitCode = null);

/// <summary>
/// Складской документ из формы: строки, комментарий (у списания — причина, обязательна), номер внешнего документа (накладная), отдел и
/// получатель (выдача отделу), ключ повтора формы, склад (не указан — склад по умолчанию).
/// </summary>
public sealed record ProcurementStockOperationInput(
    IReadOnlyList<ProcurementStockLineInput>? Lines,
    string? Comment,
    string? Reference = null,
    Guid? DepartmentId = null,
    string? Receiver = null,
    Guid? ClientKey = null,
    Guid? WarehouseId = null,
    Guid? SupplierId = null);

/// <summary>Ввод партий: позиция и по каждому складу с остатком — партии (сумма = остаток склада), комментарий, ключ повтора.</summary>
public sealed record ProcurementBatchEntryInput(Guid ItemId, IReadOnlyList<ProcurementBatchEntryWarehouse>? Warehouses, string? Comment = null, Guid? ClientKey = null);

public sealed record ProcurementBatchEntryWarehouse(Guid WarehouseId, IReadOnlyList<ProcurementBatchInput>? Batches);

/// <summary>Сторно документа: причина (обязательна), ключ повтора формы.</summary>
public sealed record ProcurementReversalInput(string? Reason, Guid? ClientKey = null);

/// <summary>Перемещение из формы: склад-отправитель и склад-получатель, строки (позиция, количество), комментарий, номер документа, ключ повтора.</summary>
public sealed record ProcurementTransferInput(
    Guid? FromWarehouseId,
    Guid? ToWarehouseId,
    IReadOnlyList<ProcurementStockLineInput>? Lines,
    string? Comment = null,
    string? Reference = null,
    Guid? ClientKey = null);

/// <summary>Заявка к выдаче со склада: активный резерв по позициям.</summary>
public sealed record ProcurementToIssue(
    Guid RequestId,
    string Number,
    string Title,
    ProcurementRequestStatus Status,
    string DepartmentName,
    string InitiatorName,
    DateOnly? DesiredDate,
    DateTimeOffset? SubmittedAt,
    IReadOnlyList<ProcurementToIssueLine> Lines);

public sealed record ProcurementToIssueLine(Guid RequestItemId, Guid ItemId, string Sku, string Name, string UnitCode, decimal Left, decimal OnHand);

/// <summary>
/// Склад снабжения: остатки и каталог, журнал движений, приход, выдача отделу, списание, инвентаризация, заявки к выдаче. Смотрят склад
/// кладовщик, снабжение и директор; проводит операции кладовщик; каталог ведут кладовщик и снабжение. Остаток меняется только документом
/// со строкой журнала (ProcurementStockLedger) — без отрицательного остатка и ниже резерва, при одновременном изменении — заново.
/// </summary>
public sealed class ProcurementStockService(IAppDbContext db, ProcurementStockLedger ledger, IAuditLogger audit, UserNotifier notifier)
{
    private static DateTimeOffset Now => DateTimeOffset.UtcNow;

    private static void RequireStock(ProcurementContext ctx)
    {
        if (!ctx.SeesStock)
        {
            throw new FieldForbiddenException("Склад снабжения видят кладовщик, снабжение и директор.");
        }
    }

    private static void RequireWarehouse(ProcurementContext ctx)
    {
        if (!ctx.Warehouse)
        {
            throw new FieldForbiddenException("Приход, выдачу, списание и инвентаризацию проводит кладовщик.");
        }
    }

    // ---------- Остатки и каталог ----------

    public async Task<PageResult<ProcurementStockRow>> StockAsync(ProcurementContext ctx, ProcurementStockSearch s, CancellationToken ct)
    {
        RequireStock(ctx);
        var today = ledger.Today;
        var q = Rows(s.WarehouseId);
        q = s.Filter switch
        {
            "low" => q.Where(r => r.IsActive && r.MinQty > 0 && r.Available < r.MinQty),
            "reserved" => q.Where(r => r.Reserved > 0),
            "empty" => q.Where(r => r.IsActive && r.OnHand <= 0),
            "expired" => q.Where(r => r.IsActive && r.Expired > 0),
            "expiring" => q.Where(r => r.IsActive && r.ExpiringBy != null && r.ExpiringBy <= today),
            "control" => q.Where(r => r.IsActive && r.OnControl > 0),
            "blocked" => q.Where(r => r.Blocked > 0),
            "inactive" => q.Where(r => !r.IsActive),
            "all" => q,
            _ => q.Where(r => r.IsActive),
        };
        if (s.CategoryId is { } category)
        {
            q = q.Where(r => r.CategoryId == category);
        }

        if (!string.IsNullOrWhiteSpace(s.Search))
        {
            var text = s.Search.Trim().ToLower();
            q = q.Where(r => r.Sku.ToLower().Contains(text) || r.Name.ToLower().Contains(text) || (r.Barcode != null && r.Barcode.ToLower() == text));
        }

        var total = await q.CountAsync(ct);
        var pageSize = Math.Clamp(s.PageSize, 5, 200);
        var page = Math.Max(s.Page, 1);
        q = s.Sort switch
        {
            "sku" => s.Desc ? q.OrderByDescending(r => r.Sku) : q.OrderBy(r => r.Sku),
            "onHand" => s.Desc ? q.OrderByDescending(r => r.OnHand) : q.OrderBy(r => r.OnHand),
            "available" => s.Desc ? q.OrderByDescending(r => r.Available) : q.OrderBy(r => r.Available),
            "reserved" => s.Desc ? q.OrderByDescending(r => r.Reserved) : q.OrderBy(r => r.Reserved),
            "category" => s.Desc ? q.OrderByDescending(r => r.CategoryName) : q.OrderBy(r => r.CategoryName).ThenBy(r => r.Name),
            "moved" => s.Desc ? q.OrderByDescending(r => r.LastMovementAt) : q.OrderBy(r => r.LastMovementAt),
            _ => s.Desc ? q.OrderByDescending(r => r.Name) : q.OrderBy(r => r.Name),
        };
        var rows = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PageResult<ProcurementStockRow>(rows.Select(r => r.Row()).ToList(), total, page, pageSize);
    }

    /// <summary>Строка остатков для запроса: класс со свойствами — по ним EF строит фильтры и сортировку в SQL (по record не умеет).</summary>
    private sealed class StockData
    {
        public Guid ItemId { get; init; }
        public string Sku { get; init; } = "";
        public string Name { get; init; } = "";
        public Guid CategoryId { get; init; }
        public string CategoryName { get; init; } = "";
        public string UnitCode { get; init; } = "";
        public decimal OnHand { get; init; }
        public decimal Reserved { get; init; }
        public decimal Available { get; init; }
        public decimal MinQty { get; init; }
        public bool IsActive { get; init; }
        public string? Barcode { get; init; }
        public string? Description { get; init; }
        public DateTimeOffset? LastMovementAt { get; init; }
        public bool TracksBatches { get; init; }
        public decimal Expired { get; init; }
        public DateOnly? NearestExpiry { get; init; }

        /// <summary>С какого дня напоминать о сроке ближайшей годной партии (срок минус «за сколько дней»).</summary>
        public DateOnly? ExpiringBy { get; init; }

        public int? ShelfLifeDays { get; init; }
        public int? ExpiryWarningDays { get; init; }
        public string? StorageConditions { get; init; }
        public decimal OnControl { get; init; }
        public decimal Blocked { get; init; }
        public ProcurementQualityControl QualityControl { get; init; }

        public ProcurementStockRow Row() => new(ItemId, Sku, Name, CategoryId, CategoryName, UnitCode, OnHand, Reserved, Available, MinQty,
            MinQty > 0 && Available < MinQty, IsActive, Barcode, Description, LastMovementAt, TracksBatches, Expired, NearestExpiry, ShelfLifeDays, ExpiryWarningDays,
            StorageConditions, OnControl, Blocked, QualityControl);
    }

    /// <summary>
    /// Позиции каталога с остатком на складе или (склад не указан) суммой по всем складам; нет строки остатка — нули. У позиции с партиями
    /// «доступно» — без того, что не выдать (на контроле качества, забраковано и заблокировано, просрочено): оно не выдаётся и не резервируется.
    /// </summary>
    private IQueryable<StockData> Rows(Guid? warehouse)
    {
        var today = ledger.Today;
        var balances = db.ProcurementStockBalances.AsNoTracking();
        var batches = db.ProcurementBatchBalances.AsNoTracking();
        var moved = db.ProcurementStockMovements.AsNoTracking();
        if (warehouse is { } id)
        {
            balances = balances.Where(b => b.WarehouseId == id);
            batches = batches.Where(b => b.WarehouseId == id);
            moved = moved.Where(m => m.WarehouseId == id);
        }

        // Сумма строк остатка позиции (подзапросы — EF не фильтрует по полям группировки, присоединённой слева).
        return from i in db.ProcurementItems.AsNoTracking()
            let onHand = balances.Where(b => b.ItemId == i.Id).Sum(b => (decimal?)b.OnHand) ?? 0m
            let reserved = balances.Where(b => b.ItemId == i.Id).Sum(b => (decimal?)b.Reserved) ?? 0m
            let expired = batches.Where(b => b.ItemId == i.Id && b.OnHand > 0 && b.Batch!.QualityStatus == ProcurementBatchQuality.Approved
                                             && (b.Batch.ExpiresOn == null || b.Batch.ExpiresOn < today)).Sum(b => (decimal?)b.OnHand) ?? 0m
            let control = batches.Where(b => b.ItemId == i.Id && b.OnHand > 0 && b.Batch!.QualityStatus == ProcurementBatchQuality.Pending)
                .Sum(b => (decimal?)b.OnHand) ?? 0m
            let blocked = batches.Where(b => b.ItemId == i.Id && b.OnHand > 0
                                             && (b.Batch!.QualityStatus == ProcurementBatchQuality.Rejected || b.Batch.QualityStatus == ProcurementBatchQuality.Blocked))
                .Sum(b => (decimal?)b.OnHand) ?? 0m
            let nearest = batches.Where(b => b.ItemId == i.Id && b.OnHand > 0 && b.Batch!.QualityStatus == ProcurementBatchQuality.Approved && b.Batch.ExpiresOn >= today)
                .Min(b => b.Batch!.ExpiresOn)
            select new StockData
            {
                ItemId = i.Id,
                Sku = i.Sku,
                Name = i.Name,
                CategoryId = i.CategoryId,
                CategoryName = i.Category!.Name,
                UnitCode = i.UnitCode,
                OnHand = onHand,
                Reserved = reserved,
                Available = onHand - reserved - expired - control - blocked,
                MinQty = i.MinQty,
                IsActive = i.IsActive,
                Barcode = i.Barcode,
                Description = i.Description,
                LastMovementAt = moved.Where(m => m.ItemId == i.Id).Max(m => (DateTimeOffset?)m.CreatedAt),
                TracksBatches = i.TracksBatches,
                Expired = expired,
                NearestExpiry = nearest,
                ExpiringBy = nearest == null ? null : nearest.Value.AddDays(-(i.ExpiryWarningDays ?? ProcurementBatchRules.DefaultWarningDays)),
                ShelfLifeDays = i.ShelfLifeDays,
                ExpiryWarningDays = i.ExpiryWarningDays,
                StorageConditions = i.StorageConditions,
                OnControl = control,
                Blocked = blocked,
                QualityControl = i.QualityControl,
            };
    }

    /// <summary>Плитки склада: по выбранному складу или (не указан) по всем складам вместе; просрочено и истекает — у позиций с партиями.</summary>
    public async Task<ProcurementStockSummary> SummaryAsync(ProcurementContext ctx, CancellationToken ct, Guid? warehouseId = null)
    {
        RequireStock(ctx);
        var today = ledger.Today;
        var rows = Rows(warehouseId).Where(r => r.IsActive);
        var toIssue = await db.ProcurementStockReservations.Where(x => x.Status == ProcurementReservationStatus.Active).Select(x => x.RequestId).Distinct().CountAsync(ct);
        return new ProcurementStockSummary(
            await rows.CountAsync(ct),
            await rows.CountAsync(r => r.MinQty > 0 && r.Available < r.MinQty, ct),
            await rows.CountAsync(r => r.OnHand <= 0, ct),
            await rows.CountAsync(r => r.Reserved > 0, ct),
            toIssue,
            await rows.CountAsync(r => r.Expired > 0, ct),
            await rows.CountAsync(r => r.ExpiringBy != null && r.ExpiringBy <= today, ct),
            await rows.CountAsync(r => r.TracksBatches, ct),
            await rows.CountAsync(r => r.OnControl > 0, ct),
            await rows.CountAsync(r => r.Blocked > 0, ct));
    }

    /// <summary>
    /// Поиск товара для формы заявки: активные позиции по SKU, названию, штрихкоду — с доступным остатком склада по умолчанию (на нём
    /// резервируют заявки). Видят все, кто подаёт заявки. warehouseId — остаток другого склада (формы складских операций).
    /// </summary>
    public async Task<List<ProcurementCatalogOption>> CatalogSearchAsync(ProcurementContext ctx, string? search, CancellationToken ct, Guid? warehouseId = null)
    {
        if (!ctx.CanOpen)
        {
            throw new FieldForbiddenException("Раздел «Заявки» вам недоступен — нужно право «Заявки».");
        }

        var warehouse = warehouseId ?? await ledger.DefaultWarehouseIdAsync(ct);
        var q = Rows(warehouse).Where(r => r.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim().ToLower();
            q = q.Where(r => r.Sku.ToLower().Contains(text) || r.Name.ToLower().Contains(text) || (r.Barcode != null && r.Barcode.ToLower() == text));
        }

        var options = await q.OrderByDescending(r => r.Available > 0).ThenBy(r => r.Name).Take(12)
            .Select(r => new ProcurementCatalogOption(r.ItemId, r.Sku, r.Name, r.CategoryId, r.CategoryName, r.UnitCode, r.OnHand, r.Available, r.TracksBatches, r.ShelfLifeDays))
            .ToListAsync(ct);
        var units = await UnitsAsync(options.Select(o => o.Id), ct);
        return options.Select(o => o with { Units = units.GetValueOrDefault(o.Id) ?? [] }).ToList();
    }

    /// <summary>Единицы закупки позиций (для форм прихода).</summary>
    private async Task<Dictionary<Guid, List<ProcurementItemUnitView>>> UnitsAsync(IEnumerable<Guid> itemIds, CancellationToken ct)
    {
        var ids = itemIds.Distinct().ToList();
        return (await db.ProcurementItemUnits.AsNoTracking().Where(u => ids.Contains(u.ItemId)).OrderBy(u => u.UnitCode).ToListAsync(ct))
            .GroupBy(u => u.ItemId).ToDictionary(g => g.Key, g => g.Select(u => new ProcurementItemUnitView(u.UnitCode, u.Factor)).ToList());
    }

    /// <summary>Карточка позиции: остаток выбранного склада или (не указан) сумма по всем складам, движения этого склада, резервы, остаток по складам.</summary>
    public async Task<ProcurementItemCard> ItemAsync(ProcurementContext ctx, Guid id, CancellationToken ct, Guid? warehouseId = null)
    {
        RequireStock(ctx);
        var row = (await Rows(warehouseId).FirstOrDefaultAsync(r => r.ItemId == id, ct))?.Row() ?? throw new FieldNotFoundException("Позиция каталога не найдена.");
        var movements = await MovementsAsync(ctx, new ProcurementMovementSearch(id, PageSize: 50, WarehouseId: warehouseId), ct);
        var warehouses = await db.ProcurementStockBalances.AsNoTracking().Where(b => b.ItemId == id)
            .Join(db.ProcurementWarehouses.AsNoTracking(), b => b.WarehouseId, w => w.Id, (b, w) => new { w.Id, w.Name, w.IsDefault, w.IsActive, b.OnHand, b.Reserved })
            .Where(x => x.IsActive || x.OnHand != 0 || x.Reserved != 0)
            .OrderByDescending(x => x.IsDefault).ThenBy(x => x.Name)
            .Select(x => new ProcurementItemWarehouse(x.Id, x.Name, x.IsDefault, x.OnHand, x.Reserved, x.OnHand - x.Reserved))
            .ToListAsync(ct);
        var reservations = await db.ProcurementStockReservations.AsNoTracking()
            .Where(x => x.ItemId == id && x.Status == ProcurementReservationStatus.Active)
            .Join(db.ProcurementRequests.AsNoTracking(), x => x.RequestId, r => r.Id, (x, r) => new { x, r.Number, r.Title, Department = r.Department!.Name })
            .OrderBy(x => x.x.CreatedAt)
            .Select(x => new ProcurementItemReservation(x.x.RequestId, x.Number, x.Title, x.Department, x.x.Quantity - x.x.Issued, x.x.CreatedAt))
            .ToListAsync(ct);
        return new ProcurementItemCard(row, movements.Items, reservations, warehouses, (await UnitsAsync([id], ct)).GetValueOrDefault(id) ?? []);
    }

    /// <summary>Новая позиция каталога (кладовщик или снабжение): SKU уникален; строка остатка на центральном складе — с нулями.</summary>
    public async Task<Guid> CreateItemAsync(ProcurementContext ctx, ProcurementCatalogInput input, CancellationToken ct)
    {
        RequireCatalog(ctx);
        var item = new ProcurementItem { Sku = "", Name = "", UnitCode = "" };
        await ApplyAsync(item, input, ct);
        await UnitsAsync(item, input.Units, ct);
        item.TracksBatches = input.TracksBatches ?? false; // остатка ещё нет — учёт партий включается сразу
        item.QualityControl = input.QualityControl ?? ProcurementQualityControl.None;
        RequireBatchesForQuality(item);
        db.ProcurementItems.Add(item);
        db.ProcurementStockBalances.Add(new ProcurementStockBalance { WarehouseId = await ledger.DefaultWarehouseIdAsync(ct), ItemId = item.Id });
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "procurement.item.created", "procurement_item", item.Id.ToString(), Card(item), ct);
        return item.Id;
    }

    /// <summary>Правка позиции каталога; единицу нельзя сменить, если по позиции уже были движения (остаток посчитан в прежней).</summary>
    public async Task UpdateItemAsync(ProcurementContext ctx, Guid id, ProcurementCatalogInput input, CancellationToken ct)
    {
        RequireCatalog(ctx);
        var item = await db.ProcurementItems.Include(i => i.Units).FirstOrDefaultAsync(i => i.Id == id, ct) ?? throw new FieldNotFoundException("Позиция каталога не найдена.");
        var unit = item.UnitCode;
        var before = Card(item);
        await ApplyAsync(item, input, ct);
        await UnitsAsync(item, input.Units, ct);
        if (unit != item.UnitCode && await db.ProcurementStockMovements.AnyAsync(m => m.ItemId == id, ct))
        {
            throw new FieldValidationException("Единицу измерения не сменить: по позиции уже есть движения — остаток посчитан в прежней.");
        }

        // Учёт партий: включить сразу — только без остатка (с остатком — «Ввести партии»); выключить — только когда по партиям ничего не лежит.
        if (input.TracksBatches is { } tracks && tracks != item.TracksBatches)
        {
            if (tracks && await db.ProcurementStockBalances.AnyAsync(b => b.ItemId == id && b.OnHand != 0, ct))
            {
                throw new FieldConflictException("У позиции есть остаток — разложите его по партиям со сроком: «Ввести партии» в карточке позиции.");
            }

            if (!tracks && await db.ProcurementStockBalances.AnyAsync(b => b.ItemId == id && b.OnHand != 0, ct))
            {
                throw new FieldConflictException("Позиция лежит на складе по партиям — учёт партий не выключить, пока остаток не ноль.");
            }

            item.TracksBatches = tracks;
        }

        item.QualityControl = input.QualityControl ?? item.QualityControl;
        RequireBatchesForQuality(item);
        item.UpdatedAt = Now;
        await db.SaveChangesAsync(ct);
        // Карточка склада (в том числе минимальный остаток) — в журнал аудита: было и стало.
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "procurement.item.updated", "procurement_item", item.Id.ToString(), new { before, after = Card(item) }, ct);
    }

    private static object Card(ProcurementItem item) =>
        new
        {
            item.Sku, item.Name, item.CategoryId, item.UnitCode, item.MinQty, item.IsActive, item.TracksBatches, item.ShelfLifeDays, item.ExpiryWarningDays, item.StorageConditions,
            QualityControl = item.QualityControl.ToString(), Units = item.Units.OrderBy(u => u.UnitCode).Select(u => $"{u.UnitCode} = {u.Factor}").ToList(),
        };

    /// <summary>Единицы закупки позиции из формы (null — не менять): заменяются целиком.</summary>
    private async Task UnitsAsync(ProcurementItem item, IReadOnlyList<ProcurementItemUnitInput>? input, CancellationToken ct)
    {
        if (input is null)
        {
            return;
        }

        var known = await db.ProcurementUnits.AsNoTracking().Select(u => u.Code).ToListAsync(ct);
        if (ProcurementUnitRules.Check(item.UnitCode, input, known) is { } error)
        {
            throw new FieldValidationException(error);
        }

        var wanted = input.ToDictionary(u => u.UnitCode!.Trim(), u => u.Factor);
        foreach (var gone in item.Units.Where(u => !wanted.ContainsKey(u.UnitCode)).ToList())
        {
            item.Units.Remove(gone);
            db.ProcurementItemUnits.Remove(gone);
        }

        foreach (var (code, factor) in wanted)
        {
            if (item.Units.FirstOrDefault(u => u.UnitCode == code) is { } kept)
            {
                kept.Factor = factor;
            }
            else
            {
                var added = new ProcurementItemUnit { ItemId = item.Id, UnitCode = code, Factor = factor };
                item.Units.Add(added);
                db.ProcurementItemUnits.Add(added);
            }
        }
    }

    /// <summary>Контроль качества — по партиям: решение принимается о партии, поэтому позиция без партий его не получает.</summary>
    private static void RequireBatchesForQuality(ProcurementItem item)
    {
        if (item.QualityControl != ProcurementQualityControl.None && !item.TracksBatches)
        {
            throw new FieldValidationException("Контроль качества — у позиции, которая ведётся по партиям: включите учёт партий (с остатком — «Ввести партии»).");
        }
    }

    private static void RequireCatalog(ProcurementContext ctx)
    {
        if (!ctx.Warehouse && !ctx.Manages)
        {
            throw new FieldForbiddenException("Каталог склада ведут кладовщик и снабжение.");
        }
    }

    private async Task ApplyAsync(ProcurementItem item, ProcurementCatalogInput input, CancellationToken ct)
    {
        var sku = Text(input.Sku, 64, "Артикул (SKU)") ?? throw new FieldValidationException("Укажите артикул (SKU).");
        var name = Text(input.Name, 300, "Название") ?? throw new FieldValidationException("Укажите название позиции.");
        var lower = sku.ToLower();
        if (await db.ProcurementItems.AnyAsync(i => i.Id != item.Id && i.Sku.ToLower() == lower, ct))
        {
            throw new FieldValidationException($"Артикул «{sku}» уже есть в каталоге.");
        }

        if (input.CategoryId is not { } categoryId || !await db.ProcurementCategories.AnyAsync(c => c.Id == categoryId && (c.IsActive || c.Id == item.CategoryId), ct))
        {
            throw new FieldValidationException("Выберите категорию.");
        }

        var unit = input.UnitCode?.Trim();
        if (string.IsNullOrEmpty(unit) || !await db.ProcurementUnits.AnyAsync(u => u.Code == unit, ct))
        {
            throw new FieldValidationException("Выберите единицу измерения.");
        }

        if (input.MinQty < 0 || input.MinQty > ProcurementRules.MaxQuantity || decimal.Round(input.MinQty, 3) != input.MinQty)
        {
            throw new FieldValidationException("Минимальный остаток — не отрицательный, не больше трёх знаков после запятой.");
        }

        item.Sku = sku;
        item.Name = name;
        item.CategoryId = categoryId;
        item.UnitCode = unit;
        item.Barcode = Text(input.Barcode, 64, "Штрихкод");
        item.Description = Text(input.Description, 1000, "Описание");
        item.MinQty = input.MinQty;
        item.IsActive = input.IsActive;
        if (input.ShelfLifeDays is { } shelf && (shelf < 1 || shelf > ProcurementBatchRules.MaxShelfLifeDays))
        {
            throw new FieldValidationException($"Срок хранения — от 1 до {ProcurementBatchRules.MaxShelfLifeDays} дней.");
        }

        if (input.ExpiryWarningDays is { } warn && (warn < 0 || warn > 365))
        {
            throw new FieldValidationException("Напоминать о сроке — за 0–365 дней.");
        }

        item.ShelfLifeDays = input.ShelfLifeDays;
        item.ExpiryWarningDays = input.ExpiryWarningDays;
        item.StorageConditions = Text(input.StorageConditions, 300, "Условия хранения");
    }

    // ---------- Журнал движений ----------

    public async Task<PageResult<ProcurementMovementRow>> MovementsAsync(ProcurementContext ctx, ProcurementMovementSearch s, CancellationToken ct)
    {
        RequireStock(ctx);
        var q = db.ProcurementStockMovements.AsNoTracking();
        if (s.ItemId is { } itemId)
        {
            q = q.Where(m => m.ItemId == itemId);
        }

        if (s.WarehouseId is { } warehouseId)
        {
            q = q.Where(m => m.WarehouseId == warehouseId);
        }

        if (s.BatchId is { } batchId)
        {
            q = q.Where(m => m.BatchId == batchId);
        }

        if (!string.IsNullOrWhiteSpace(s.Kind) && Enum.TryParse<ProcurementMovementKind>(s.Kind, true, out var kind))
        {
            q = q.Where(m => m.Kind == kind);
        }

        if (s.From is { } from)
        {
            var fromUtc = FieldClock.DayStartUtc(from);
            q = q.Where(m => m.CreatedAt >= fromUtc);
        }

        if (s.To is { } to)
        {
            var toUtc = FieldClock.DayStartUtc(to.AddDays(1));
            q = q.Where(m => m.CreatedAt < toUtc);
        }

        if (!string.IsNullOrWhiteSpace(s.Search))
        {
            var text = s.Search.Trim().ToLower();
            q = q.Where(m => m.Item!.Sku.ToLower().Contains(text) || m.Item.Name.ToLower().Contains(text) || m.Document!.Number.ToLower().Contains(text));
        }

        var total = await q.CountAsync(ct);
        var pageSize = Math.Clamp(s.PageSize, 5, 200);
        var page = Math.Max(s.Page, 1);
        var rows = await q.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(m => new ProcurementMovementRow(m.Id, m.CreatedAt, m.DocumentId, m.Document!.Number, m.Document.Kind, m.ItemId, m.Item!.Sku, m.Item.Name,
                m.Item.UnitCode, m.Kind, m.Quantity, m.BalanceBefore, m.BalanceAfter, m.UnitCost, m.Document.Actor == null ? null : m.Document.Actor.FullName,
                m.Document.Department == null ? null : m.Document.Department.Name, m.Document.RequestId,
                db.ProcurementRequests.Where(r => r.Id == m.Document.RequestId).Select(r => r.Number).FirstOrDefault(),
                m.Document.Receiver, m.Document.Reference, m.Document.Comment, m.Document.OrderId, m.Document.Order == null ? null : m.Document.Order.Number,
                db.ProcurementWarehouses.Where(w => w.Id == m.WarehouseId).Select(w => w.Name).FirstOrDefault(),
                // Перемещение: у ушедшего — склад-получатель, у пришедшего — склад-отправитель.
                m.Kind == ProcurementMovementKind.TransferOut
                    ? db.ProcurementWarehouses.Where(w => w.Id == m.Document.TargetWarehouseId).Select(w => w.Name).FirstOrDefault()
                    : m.Kind == ProcurementMovementKind.TransferIn
                        ? db.ProcurementWarehouses.Where(w => w.Id == m.Document.WarehouseId).Select(w => w.Name).FirstOrDefault()
                        : null,
                m.BatchId, m.Batch == null ? null : m.Batch.Number, m.Batch == null ? null : m.Batch.ExpiresOn, m.BatchAfter,
                db.ProcurementStockDocuments.Where(d => d.ReversesDocumentId == m.DocumentId).Select(d => d.Number).FirstOrDefault(), m.Document.QualityCheckId,
                m.Document.TenderId))
            .ToListAsync(ct);

        return new PageResult<ProcurementMovementRow>(rows, total, page, pageSize);
    }

    // ---------- Операции ----------

    /// <summary>Приход на склад: количество больше нуля, цена за единицу — по желанию; номер накладной — в Reference; у позиции с партиями — партии со сроком.</summary>
    public Task<Guid> ReceiptAsync(ProcurementContext ctx, ProcurementStockOperationInput input, CancellationToken ct) =>
        PostAsync(ctx, ProcurementStockDocumentKind.Receipt, input, ct);

    /// <summary>Списание (брак, порча, утеря, просрочка): не больше свободного остатка, причина обязательна; у позиции с партиями — указанная партия или FEFO.</summary>
    public Task<Guid> WriteOffAsync(ProcurementContext ctx, ProcurementStockOperationInput input, CancellationToken ct) =>
        PostAsync(ctx, ProcurementStockDocumentKind.WriteOff, input, ct);

    /// <summary>
    /// Инвентаризация: в строках — фактический остаток; разница с учётом — корректировка. Факт меньше резерва — нельзя (сначала снять резерв).
    /// У позиции с партиями — по партиям: строка — партия и её факт.
    /// </summary>
    public Task<Guid> InventoryAsync(ProcurementContext ctx, ProcurementStockOperationInput input, CancellationToken ct) =>
        PostAsync(ctx, ProcurementStockDocumentKind.Inventory, input, ct);

    /// <summary>Выдача отделу без заявки: не больше свободного остатка (резерв заявок не трогается), отдел обязателен; у позиции с партиями — только годные партии.</summary>
    public Task<Guid> IssueAsync(ProcurementContext ctx, ProcurementStockOperationInput input, CancellationToken ct) =>
        PostAsync(ctx, ProcurementStockDocumentKind.Issue, input, ct);

    private Task<Guid> PostAsync(ProcurementContext ctx, ProcurementStockDocumentKind kind, ProcurementStockOperationInput input, CancellationToken ct) => ledger.RetryAsync(async () =>
    {
        RequireWarehouse(ctx);
        if (await DoneAsync(ctx, input.ClientKey, ct) is { } done)
        {
            return done;
        }

        var lines = Lines(input.Lines);
        var comment = Text(input.Comment, 1000, "Комментарий");
        if (kind == ProcurementStockDocumentKind.WriteOff && comment is null)
        {
            throw new FieldValidationException("Укажите причину списания.");
        }

        Department? department = null;
        if (kind == ProcurementStockDocumentKind.Issue)
        {
            department = input.DepartmentId is { } dep ? await db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dep, ct) : null;
            if (department is null)
            {
                throw new FieldValidationException("Выберите отдел-получатель.");
            }
        }

        Guid? supplier = null;
        if (kind == ProcurementStockDocumentKind.Receipt && input.SupplierId is { } sid)
        {
            supplier = await db.ProcurementSuppliers.AnyAsync(s => s.Id == sid, ct) ? sid : throw new FieldValidationException("Поставщик не найден.");
        }

        var ids = lines.Select(l => l.ItemId).ToList();
        var items = await db.ProcurementItems.AsNoTracking().Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var purchaseUnits = await db.ProcurementItemUnits.AsNoTracking().Where(u => ids.Contains(u.ItemId)).ToListAsync(ct);
        var store = await ledger.WarehouseAsync(ctx, input.WarehouseId, ct);
        var balances = await ledger.BalancesAsync(store.Id, ids, ct);
        var doc = await ledger.DocumentAsync(kind, store.Id, ctx.UserId, ct);
        doc.Comment = comment;
        doc.Reference = Text(input.Reference, 100, "Номер документа");
        doc.DepartmentId = department?.Id;
        doc.Receiver = Text(input.Receiver, 200, "Кто получил");
        doc.ClientKey = input.ClientKey;
        doc.SupplierId = supplier;

        foreach (var input0 in lines)
        {
            var item = items.GetValueOrDefault(input0.ItemId) ?? throw new FieldValidationException("Позиция не найдена в каталоге склада.");
            var label = $"{item.Sku} {item.Name}";
            var line = kind == ProcurementStockDocumentKind.Receipt
                ? ToBase(input0, item, purchaseUnits.Where(u => u.ItemId == item.Id).Select(u => (u.UnitCode, u.Factor)), label)
                : string.IsNullOrWhiteSpace(input0.UnitCode) || input0.UnitCode == item.UnitCode
                    ? input0
                    : throw new FieldValidationException($"«{label}»: в единице закупки проводят только приход — здесь укажите {item.UnitCode}.");
            var inventory = kind == ProcurementStockDocumentKind.Inventory;
            if (line.Quantity < 0 || (!inventory && line.Quantity == 0) || line.Quantity > ProcurementRules.MaxQuantity || decimal.Round(line.Quantity, 3) != line.Quantity)
            {
                throw new FieldValidationException(inventory
                    ? $"«{label}»: фактический остаток — не отрицательный, не больше трёх знаков после запятой."
                    : $"«{label}»: количество — больше нуля, не больше трёх знаков после запятой.");
            }

            if (line.UnitCost is { } cost && (kind != ProcurementStockDocumentKind.Receipt || cost < 0 || cost > ProcurementRules.MaxPrice || decimal.Round(cost, 2) != cost))
            {
                throw new FieldValidationException($"«{label}»: цена за единицу — только у прихода, не отрицательная, до двух знаков.");
            }

            if (!item.IsActive && kind == ProcurementStockDocumentKind.Receipt)
            {
                throw new FieldValidationException($"«{label}»: позиция отключена в каталоге — включите её, чтобы принять на склад.");
            }

            var balance = balances[line.ItemId];
            if (ledger.Tracks(item.Id))
            {
                await PostBatchesAsync(doc, kind, store, item, balance, line, label, supplier, ct);
                continue;
            }

            if (line.BatchId is not null || line.Batches is { Count: > 0 })
            {
                throw new FieldValidationException($"«{label}»: позиция ведётся без партий — партию не указывайте.");
            }

            switch (kind)
            {
                case ProcurementStockDocumentKind.Receipt:
                    ledger.Move(doc, balance, ProcurementMovementKind.Receipt, line.Quantity, label, line.UnitCost);
                    break;
                case ProcurementStockDocumentKind.WriteOff:
                    ledger.Move(doc, balance, ProcurementMovementKind.WriteOff, -line.Quantity, label);
                    break;
                case ProcurementStockDocumentKind.Issue:
                    ledger.Move(doc, balance, ProcurementMovementKind.Issue, -line.Quantity, label);
                    break;
                default:
                    if (line.Quantity < balance.Reserved)
                    {
                        throw new FieldConflictException(
                            $"«{label}»: факт {ProcurementRules.Quantity(line.Quantity)} меньше резерва заявок {ProcurementRules.Quantity(balance.Reserved)} — сначала выдайте или снимите резерв.");
                    }

                    ledger.Move(doc, balance, ProcurementMovementKind.Adjustment, line.Quantity - balance.OnHand, label);
                    break;
            }
        }

        await ProcurementQualityNotices.NewChecksAsync(db, notifier, ledger.NewChecks, ct);
        await db.SaveChangesAsync(ct);
        return doc.Id;
    });

    /// <summary>
    /// Строка прихода в единице закупки → в единице карточки: количество, партии и цена пересчитываются по коэффициенту; единица не карточки и
    /// не закупки — ошибка; после пересчёта больше трёх знаков — ошибка (молча не округляем).
    /// </summary>
    private static ProcurementStockLineInput ToBase(ProcurementStockLineInput line, ProcurementItem item, IEnumerable<(string UnitCode, decimal Factor)> units, string label)
    {
        if (string.IsNullOrWhiteSpace(line.UnitCode) || line.UnitCode == item.UnitCode)
        {
            return line;
        }

        var factor = ProcurementUnitRules.Factor(item.UnitCode, line.UnitCode, units)
                     ?? throw new FieldValidationException($"«{label}»: единица «{line.UnitCode}» не задана у позиции — добавьте её в единицы закупки карточки.");
        decimal Base(decimal quantity) => ProcurementUnitRules.ToBase(quantity, factor)
                                          ?? throw new FieldValidationException($"«{label}»: {ProcurementRules.Quantity(quantity)} {line.UnitCode} — больше трёх знаков в {item.UnitCode}.");
        return line with
        {
            Quantity = Base(line.Quantity),
            UnitCost = ProcurementUnitRules.CostToBase(line.UnitCost, factor),
            Batches = line.Batches?.Select(b => b with { Quantity = Base(b.Quantity) }).ToList(),
            UnitCode = item.UnitCode,
        };
    }

    /// <summary>
    /// Строка документа по позиции с партиями: приход — новые партии со сроком (сумма партий = количество строки), у позиции с контролем
    /// качества — «на контроле» с проверкой; выдача — годные партии (указанная или FEFO), резерв заявок остаётся; списание — любая партия
    /// (указанная или FEFO); инвентаризация — факт партии.
    /// </summary>
    private async Task PostBatchesAsync(ProcurementStockDocument doc, ProcurementStockDocumentKind kind, ProcurementWarehouse store, ProcurementItem item,
        ProcurementStockBalance balance, ProcurementStockLineInput line, string label, Guid? supplier, CancellationToken ct)
    {
        switch (kind)
        {
            case ProcurementStockDocumentKind.Receipt:
                var batches = line.Batches ?? [];
                if (batches.Count == 0)
                {
                    throw new FieldValidationException($"«{label}» ведётся по партиям — укажите партию и срок годности.");
                }

                if (batches.Count > ProcurementBatchRules.MaxBatchesPerLine)
                {
                    throw new FieldValidationException($"«{label}»: в одной строке — до {ProcurementBatchRules.MaxBatchesPerLine} партий.");
                }

                if (batches.Sum(b => b.Quantity) != line.Quantity)
                {
                    throw new FieldValidationException(
                        $"«{label}»: партии вместе — {ProcurementRules.Quantity(batches.Sum(b => b.Quantity))}, а в строке {ProcurementRules.Quantity(line.Quantity)} — должно совпасть.");
                }

                foreach (var b in batches)
                {
                    var expires = ProcurementBatchRules.ExpiresOn(b.ExpiresOn, b.ManufacturedOn, item.ShelfLifeDays);
                    if (ProcurementBatchRules.Check(label, b, expires, ledger.Today) is { } error)
                    {
                        throw new FieldValidationException(error);
                    }

                    var batch = await ledger.NewBatchAsync(doc, store.Id, item.Id, b.SupplierBatchNo, b.ManufacturedOn, expires!.Value, line.UnitCost, supplier, ct,
                        item.QualityControl);
                    ledger.MoveBatch(doc, balance, batch, ProcurementMovementKind.Receipt, b.Quantity, label, line.UnitCost);
                }

                break;
            case ProcurementStockDocumentKind.Issue or ProcurementStockDocumentKind.WriteOff:
                var issue = kind == ProcurementStockDocumentKind.Issue;
                foreach (var (batch, quantity) in await ledger.PickAsync(balance, store.Name, label, line.Quantity, line.BatchId, usableOnly: issue, keepReserve: true, ct))
                {
                    ledger.MoveBatch(doc, balance, batch, issue ? ProcurementMovementKind.Issue : ProcurementMovementKind.WriteOff, -quantity, label);
                }

                break;
            default:
                if (line.BatchId is not { } batchId)
                {
                    throw new FieldValidationException($"«{label}» ведётся по партиям — в инвентаризации укажите партию и её фактический остаток.");
                }

                var counted = await db.ProcurementBatches.FirstOrDefaultAsync(b => b.Id == batchId && b.ItemId == item.Id, ct)
                              ?? throw new FieldValidationException($"«{label}»: партия не найдена.");
                var stock = await ledger.BatchBalanceAsync(store.Id, counted, ct);
                ledger.MoveBatch(doc, balance, stock, ProcurementMovementKind.Adjustment, line.Quantity - stock.OnHand, label);
                break;
        }
    }

    /// <summary>
    /// Перемещение между складами (кладовщик склада-отправителя): из свободного остатка — резерв заявок не перемещается. Один документ TRF-…:
    /// «ушло» на отправителе и «пришло» на получателе одной записью, поэтому итог по компании не меняется. Отключённый склад не участвует.
    /// Позиция с партиями перемещается партиями (указанная или FEFO годных): на получателе — та же партия, с тем же номером и сроком.
    /// </summary>
    public Task<Guid> TransferAsync(ProcurementContext ctx, ProcurementTransferInput input, CancellationToken ct) => ledger.RetryAsync(async () =>
    {
        RequireWarehouse(ctx);
        if (await DoneAsync(ctx, input.ClientKey, ct) is { } done)
        {
            return done;
        }

        if (input.FromWarehouseId is not { } fromId || input.ToWarehouseId is not { } toId)
        {
            throw new FieldValidationException("Выберите склад-отправитель и склад-получатель.");
        }

        if (fromId == toId)
        {
            throw new FieldValidationException("Склад-отправитель и склад-получатель совпадают — выберите другой склад.");
        }

        var lines = Lines(input.Lines);
        var source = await ledger.WarehouseAsync(ctx, fromId, ct);
        var target = await db.ProcurementWarehouses.AsNoTracking().FirstOrDefaultAsync(w => w.Id == toId, ct) ?? throw new FieldNotFoundException("Склад-получатель не найден.");
        if (!target.IsActive)
        {
            throw new FieldConflictException($"Склад «{target.Name}» отключён — на него не переместить.");
        }

        ProcurementStockLedger.CheckOwner(target, ProcurementWarehouseUse.Manual);

        var ids = lines.Select(l => l.ItemId).ToList();
        var items = await db.ProcurementItems.AsNoTracking().Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var from = await ledger.BalancesAsync(source.Id, ids, ct);
        var to = await ledger.BalancesAsync(target.Id, ids, ct);
        var doc = await ledger.DocumentAsync(ProcurementStockDocumentKind.Transfer, source.Id, ctx.UserId, ct);
        doc.TargetWarehouseId = target.Id;
        doc.Comment = Text(input.Comment, 1000, "Комментарий");
        doc.Reference = Text(input.Reference, 100, "Номер документа");
        doc.ClientKey = input.ClientKey;

        foreach (var line in lines)
        {
            var item = items.GetValueOrDefault(line.ItemId) ?? throw new FieldValidationException("Позиция не найдена в каталоге склада.");
            var label = $"{item.Sku} {item.Name}";
            if (line.Quantity <= 0 || line.Quantity > ProcurementRules.MaxQuantity || decimal.Round(line.Quantity, 3) != line.Quantity)
            {
                throw new FieldValidationException($"«{label}»: количество — больше нуля, не больше трёх знаков после запятой.");
            }

            if (line.UnitCost is not null || line.Batches is { Count: > 0 })
            {
                throw new FieldValidationException($"«{label}»: цена и новые партии указываются только у прихода.");
            }

            var balance = from[line.ItemId];
            if (ledger.Tracks(item.Id))
            {
                // Выбранную партию можно переместить и просроченной (в зону списания); без выбора — только годные, по FEFO.
                foreach (var (batch, quantity) in await ledger.PickAsync(balance, source.Name, label, line.Quantity, line.BatchId, usableOnly: line.BatchId is null,
                             keepReserve: true, ct))
                {
                    ledger.MoveBatch(doc, balance, batch, ProcurementMovementKind.TransferOut, -quantity, label);
                    ledger.MoveBatch(doc, to[line.ItemId], await ledger.BatchBalanceAsync(target.Id, batch.Batch!, ct), ProcurementMovementKind.TransferIn, quantity, label);
                }

                continue;
            }

            if (line.BatchId is not null)
            {
                throw new FieldValidationException($"«{label}»: позиция ведётся без партий — партию не указывайте.");
            }

            var free = balance.OnHand - balance.Reserved;
            if (line.Quantity > free)
            {
                throw new FieldConflictException(balance.Reserved > 0
                    ? $"«{label}»: на складе «{source.Name}» {ProcurementRules.Quantity(balance.OnHand)}, из них в резерве заявок {ProcurementRules.Quantity(balance.Reserved)} — переместить можно {ProcurementRules.Quantity(free)}."
                    : $"«{label}»: на складе «{source.Name}» {ProcurementRules.Quantity(balance.OnHand)} — столько не переместить.");
            }

            ledger.Move(doc, balance, ProcurementMovementKind.TransferOut, -line.Quantity, label);
            ledger.Move(doc, to[line.ItemId], ProcurementMovementKind.TransferIn, line.Quantity, label);
        }

        await db.SaveChangesAsync(ct);
        return doc.Id;
    });

    /// <summary>
    /// Ввод партий (кладовщик): имеющийся остаток позиции раскладывается по партиям со сроком — на каждом складе, где он есть, сумма партий =
    /// остаток склада. Остаток не меняется; позиция начинает вестись по партиям. Документ BE-… на каждый склад. Без остатка учёт партий
    /// включается в карточке позиции.
    /// </summary>
    public Task<Guid> BatchEntryAsync(ProcurementContext ctx, ProcurementBatchEntryInput input, CancellationToken ct) => ledger.RetryAsync(async () =>
    {
        RequireWarehouse(ctx);
        if (await DoneAsync(ctx, input.ClientKey, ct) is { } done)
        {
            return done;
        }

        var item = await db.ProcurementItems.FirstOrDefaultAsync(i => i.Id == input.ItemId, ct) ?? throw new FieldNotFoundException("Позиция каталога не найдена.");
        var label = $"{item.Sku} {item.Name}";
        if (item.TracksBatches)
        {
            throw new FieldConflictException($"«{label}» уже ведётся по партиям.");
        }

        var stocked = await db.ProcurementStockBalances.AsNoTracking().Where(b => b.ItemId == item.Id && b.OnHand > 0).Select(b => b.WarehouseId).ToListAsync(ct);
        if (stocked.Count == 0)
        {
            throw new FieldValidationException($"«{label}»: остатка нет — включите учёт партий в карточке позиции.");
        }

        var entries = (input.Warehouses ?? []).ToList();
        if (entries.GroupBy(e => e.WarehouseId).Any(g => g.Count() > 1))
        {
            throw new FieldValidationException("Склад повторяется — укажите его партии одной строкой.");
        }

        if (stocked.Except(entries.Select(e => e.WarehouseId)).Any() || entries.Select(e => e.WarehouseId).Except(stocked).Any())
        {
            throw new FieldValidationException($"«{label}»: разложите по партиям остаток на всех складах, где он есть, — и только на них.");
        }

        var comment = Text(input.Comment, 1000, "Комментарий");
        var first = true;
        Guid firstDoc = Guid.Empty;
        foreach (var entry in entries)
        {
            var store = await ledger.WarehouseAsync(ctx, entry.WarehouseId, ct);
            var balance = (await ledger.BalancesAsync(store.Id, [item.Id], ct))[item.Id];
            var batches = entry.Batches ?? [];
            if (batches.Count == 0 || batches.Count > ProcurementBatchRules.MaxBatchesPerLine)
            {
                throw new FieldValidationException($"«{label}», склад «{store.Name}»: от 1 до {ProcurementBatchRules.MaxBatchesPerLine} партий.");
            }

            if (batches.Sum(b => b.Quantity) != balance.OnHand)
            {
                throw new FieldValidationException(
                    $"«{label}», склад «{store.Name}»: партии вместе — {ProcurementRules.Quantity(batches.Sum(b => b.Quantity))}, а на складе {ProcurementRules.Quantity(balance.OnHand)} — должно совпасть (расхождение — через инвентаризацию).");
            }

            var doc = await ledger.DocumentAsync(ProcurementStockDocumentKind.BatchEntry, store.Id, ctx.UserId, ct);
            doc.Comment = comment ?? "Ввод партий на остаток";
            doc.ClientKey = first ? input.ClientKey : null;
            firstDoc = first ? doc.Id : firstDoc;
            first = false;
            foreach (var b in batches)
            {
                // Имеющийся остаток мог и просрочиться — партия вводится как есть (её можно будет только списать или переместить). Срок не
                // известен — «срок не указан»: партия заблокирована, пока назначенный по качеству не укажет срок. Контроля прихода нет —
                // остаток уже на складе.
                var expires = b.ExpiryUnknown ? null : ProcurementBatchRules.ExpiresOn(b.ExpiresOn, b.ManufacturedOn, item.ShelfLifeDays);
                if (ProcurementBatchRules.Check(label, b, expires, ledger.Today, allowExpired: true, allowUnknown: true) is { } error)
                {
                    throw new FieldValidationException(error);
                }

                var batch = await ledger.NewBatchAsync(doc, store.Id, item.Id, b.SupplierBatchNo, b.ManufacturedOn, expires, null, null, ct);
                ledger.EnterBatch(doc, balance, batch, b.Quantity, label);
            }
        }

        item.TracksBatches = true;
        item.UpdatedAt = Now;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "procurement.item.batches_entered", "procurement_item", item.Id.ToString(),
            new { warehouses = entries.Count, batches = entries.Sum(e => e.Batches?.Count ?? 0) }, ct);
        return firstDoc;
    });

    /// <summary>
    /// Виды документов, которые можно сторнировать: приход без заказа, выдача отделу, списание (кроме проб лаборатории), инвентаризация,
    /// перемещение. Возврат поставщику не сторнируется: товар уже у поставщика.
    /// </summary>
    public static readonly ProcurementStockDocumentKind[] Reversible =
    [
        ProcurementStockDocumentKind.Receipt, ProcurementStockDocumentKind.Issue, ProcurementStockDocumentKind.WriteOff, ProcurementStockDocumentKind.Inventory,
        ProcurementStockDocumentKind.Transfer,
    ];

    /// <summary>
    /// Сторно (кладовщик склада документа): зеркальные движения каждой строки — те же склады и партии, документ STN-… со ссылкой на исправленный;
    /// исправленный не меняется. Один раз на документ. Нельзя: приёмку по заказу и «Куплено» (есть заказ), выдачу по заявке (есть заявка), ввод
    /// партий и само сторно; и если остатка уже нет (приход выдали) — тогда сначала верните выданное.
    /// </summary>
    public Task<Guid> ReverseAsync(ProcurementContext ctx, Guid documentId, ProcurementReversalInput input, CancellationToken ct) => ledger.RetryAsync(async () =>
    {
        RequireWarehouse(ctx);
        if (await DoneAsync(ctx, input.ClientKey, ct) is { } done)
        {
            return done;
        }

        var reason = Text(input.Reason, 1000, "Причина") ?? throw new FieldValidationException("Укажите причину сторно.");
        var original = await db.ProcurementStockDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == documentId, ct)
                       ?? throw new FieldNotFoundException("Документ не найден.");
        if (await db.ProcurementStockDocuments.AnyAsync(d => d.ReversesDocumentId == documentId, ct))
        {
            throw new FieldConflictException($"Документ {original.Number} уже сторнирован.");
        }

        if (original.Status != ProcurementStockDocumentStatus.Posted || !Reversible.Contains(original.Kind) || original.OrderId is not null || original.RequestId is not null
            || original.QualityCheckId is not null || original.TenderId is not null)
        {
            throw new FieldConflictException(
                $"Документ {original.Number} так не исправить: приёмку по заказу, «Куплено», поставку по тендеру, выдачу по заявке и по заданию на выпуск, возврат из производства, ввод партий, пробы лаборатории, возврат поставщику и сторно не сторнируют.");
        }

        var store = await ledger.WarehouseAsync(ctx, original.WarehouseId, ct);
        var movements = await db.ProcurementStockMovements.AsNoTracking().Where(m => m.DocumentId == documentId).OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).ToListAsync(ct);
        var labels = await db.ProcurementItems.AsNoTracking().Where(i => movements.Select(m => m.ItemId).Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => $"{i.Sku} {i.Name}", ct);
        var doc = await ledger.DocumentAsync(ProcurementStockDocumentKind.Reversal, store.Id, ctx.UserId, ct);
        doc.ReversesDocumentId = original.Id;
        doc.TargetWarehouseId = original.TargetWarehouseId;
        doc.DepartmentId = original.DepartmentId;
        doc.Reference = original.Number;
        doc.Comment = reason;
        doc.ClientKey = input.ClientKey;
        // Сначала возвращается то, что ушло (плюс), потом снимается пришедшее (минус): так перемещение сторнируется и при пустом складе-получателе.
        foreach (var m in movements.Where(m => m.Quantity != 0).OrderBy(m => m.Quantity))
        {
            var balance = (await ledger.BalancesAsync(m.WarehouseId, [m.ItemId], ct))[m.ItemId];
            var label = labels.GetValueOrDefault(m.ItemId, "Позиция");
            if (m.BatchId is { } batchId)
            {
                var batch = await db.ProcurementBatches.FirstAsync(b => b.Id == batchId, ct);
                ledger.MoveBatch(doc, balance, await ledger.BatchBalanceAsync(m.WarehouseId, batch, ct), ProcurementMovementKind.Reversal, -m.Quantity, label);
            }
            else
            {
                ledger.Move(doc, balance, ProcurementMovementKind.Reversal, -m.Quantity, label);
            }
        }

        // Сторнированный приход: его партии ушли со склада — открытые проверки качества по ним отменяются.
        if (original.Kind == ProcurementStockDocumentKind.Receipt)
        {
            var open = ProcurementQualityRules.Open;
            foreach (var check in await db.ProcurementQualityChecks.Where(c => open.Contains(c.Status) && c.Batch!.SourceDocumentId == original.Id).ToListAsync(ct))
            {
                check.Status = ProcurementQualityCheckStatus.Cancelled;
                check.DecisionComment = $"Приход {original.Number} сторнирован ({doc.Number})";
                check.UpdatedAt = Now;
            }
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "procurement.stock.reversed", "procurement_stock_document", original.Id.ToString(),
            new { original.Number, reversal = doc.Number, reason }, ct);
        return doc.Id;
    });

    /// <summary>
    /// Возврат поставщику (кладовщик склада): забракованные или заблокированные партии уходят со склада документом SRT-…, причина обязательна.
    /// Поставщик — указанный или поставщик партий (в одном документе — партии одного поставщика). Допущенную партию так не вернуть — сначала
    /// решение по качеству или блокировка.
    /// </summary>
    public Task<Guid> SupplierReturnAsync(ProcurementContext ctx, ProcurementStockOperationInput input, CancellationToken ct) => ledger.RetryAsync(async () =>
    {
        RequireWarehouse(ctx);
        if (await DoneAsync(ctx, input.ClientKey, ct) is { } done)
        {
            return done;
        }

        var lines = Lines(input.Lines);
        var reason = Text(input.Comment, 1000, "Причина") ?? throw new FieldValidationException("Укажите причину возврата поставщику.");
        var ids = lines.Select(l => l.ItemId).ToList();
        var items = await db.ProcurementItems.AsNoTracking().Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var store = await ledger.WarehouseAsync(ctx, input.WarehouseId, ct);
        var balances = await ledger.BalancesAsync(store.Id, ids, ct);
        var doc = await ledger.DocumentAsync(ProcurementStockDocumentKind.SupplierReturn, store.Id, ctx.UserId, ct);
        doc.Comment = reason;
        doc.Reference = Text(input.Reference, 100, "Номер документа");
        doc.ClientKey = input.ClientKey;
        var suppliers = new HashSet<Guid>();
        foreach (var line in lines)
        {
            var item = items.GetValueOrDefault(line.ItemId) ?? throw new FieldValidationException("Позиция не найдена в каталоге склада.");
            var label = $"{item.Sku} {item.Name}";
            if (line.Quantity <= 0 || line.Quantity > ProcurementRules.MaxQuantity || decimal.Round(line.Quantity, 3) != line.Quantity)
            {
                throw new FieldValidationException($"«{label}»: количество — больше нуля, не больше трёх знаков после запятой.");
            }

            if (!ledger.Tracks(item.Id) || line.BatchId is not { } batchId)
            {
                throw new FieldValidationException($"«{label}»: поставщику возвращают партию — выберите забракованную или заблокированную партию.");
            }

            var batch = await db.ProcurementBatches.FirstOrDefaultAsync(b => b.Id == batchId && b.ItemId == item.Id, ct) ?? throw new FieldValidationException($"«{label}»: партия не найдена.");
            if (batch.QualityStatus is not (ProcurementBatchQuality.Rejected or ProcurementBatchQuality.Blocked))
            {
                throw new FieldConflictException(
                    $"«{label}»: партия {batch.Number} {ProcurementQualityRules.Label(batch.QualityStatus)} — поставщику возвращают забракованную или заблокированную партию.");
            }

            if (batch.SupplierId is { } supplierOfBatch)
            {
                suppliers.Add(supplierOfBatch);
            }

            var stock = await ledger.BatchBalanceAsync(store.Id, batch, ct);
            ledger.MoveBatch(doc, balances[item.Id], stock, ProcurementMovementKind.SupplierReturn, -line.Quantity, label);
        }

        if (input.SupplierId is { } chosen)
        {
            doc.SupplierId = await db.ProcurementSuppliers.AnyAsync(s => s.Id == chosen, ct) ? chosen : throw new FieldValidationException("Поставщик не найден.");
        }
        else if (suppliers.Count > 1)
        {
            throw new FieldValidationException("Партии разных поставщиков — оформите возврат каждому поставщику отдельно.");
        }
        else
        {
            doc.SupplierId = suppliers.FirstOrDefault() is var only && only != Guid.Empty ? only : null;
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "procurement.stock.supplier_returned", "procurement_stock_document", doc.Id.ToString(),
            new { doc.Number, lines = lines.Count, reason }, ct);
        return doc.Id;
    });

    /// <summary>Повтор формы с тем же ключом — документ, который она уже провела.</summary>
    private async Task<Guid?> DoneAsync(ProcurementContext ctx, Guid? key, CancellationToken ct)
    {
        if (key is not { } k)
        {
            return null;
        }

        var id = await db.ProcurementStockDocuments.AsNoTracking().Where(d => d.ActorId == ctx.UserId && d.ClientKey == k).Select(d => d.Id).FirstOrDefaultAsync(ct);
        return id == Guid.Empty ? null : id;
    }

    /// <summary>Строки документа из формы: хотя бы одна, до 200, позиция (и партия) — одной строкой.</summary>
    private static List<ProcurementStockLineInput> Lines(IReadOnlyList<ProcurementStockLineInput>? input)
    {
        var lines = (input ?? []).ToList();
        if (lines.Count == 0)
        {
            throw new FieldValidationException("Добавьте хотя бы одну позицию.");
        }

        if (lines.Count > 200)
        {
            throw new FieldValidationException("В одном документе — до 200 позиций.");
        }

        if (lines.GroupBy(l => (l.ItemId, l.BatchId)).Any(g => g.Count() > 1))
        {
            throw new FieldValidationException("Позиция повторяется — укажите её одной строкой.");
        }

        return lines;
    }

    /// <summary>Отделы — получатели выдачи со склада (все отделы компании).</summary>
    public async Task<List<ProcurementDepartmentOption>> DepartmentsAsync(ProcurementContext ctx, CancellationToken ct)
    {
        RequireStock(ctx);
        var list = await db.Departments.AsNoTracking().Select(d => new ProcurementDepartmentOption(d.Id, d.Code, d.Name)).ToListAsync(ct);
        return list.OrderBy(d => Array.IndexOf(OneBase.Application.Work.WorkTaskService.Order, d.Code) is var i and >= 0 ? i : 99).ThenBy(d => d.Name).ToList();
    }

    // ---------- К выдаче ----------

    /// <summary>Заявки с активным резервом — что выдать со склада: по позициям, сколько осталось в резерве и сколько на складе.</summary>
    public async Task<List<ProcurementToIssue>> ToIssueAsync(ProcurementContext ctx, CancellationToken ct)
    {
        RequireStock(ctx);
        var rows = await db.ProcurementStockReservations.AsNoTracking()
            .Where(x => x.Status == ProcurementReservationStatus.Active)
            .Join(db.ProcurementRequestItems.AsNoTracking(), x => x.RequestItemId, i => i.Id, (x, i) => new { x, i })
            .Select(v => new
            {
                v.x.RequestId,
                v.x.RequestItemId,
                v.x.ItemId,
                Left = v.x.Quantity - v.x.Issued,
                v.i.Name,
                v.i.UnitCode,
                v.i.LineNo,
                Sku = v.i.Item!.Sku,
                OnHand = db.ProcurementStockBalances.Where(b => b.WarehouseId == v.x.WarehouseId && b.ItemId == v.x.ItemId).Select(b => b.OnHand).FirstOrDefault(),
            })
            .ToListAsync(ct);
        var requestIds = rows.Select(r => r.RequestId).Distinct().ToList();
        var requests = await db.ProcurementRequests.AsNoTracking().Where(r => requestIds.Contains(r.Id))
            .Select(r => new { r.Id, r.Number, r.Title, r.Status, Department = r.Department!.Name, Initiator = r.Initiator!.FullName, r.DesiredDate, r.SubmittedAt })
            .ToListAsync(ct);
        return requests
            .OrderBy(r => r.DesiredDate ?? DateOnly.MaxValue).ThenBy(r => r.SubmittedAt)
            .Select(r => new ProcurementToIssue(r.Id, r.Number, r.Title, r.Status, r.Department, r.Initiator, r.DesiredDate, r.SubmittedAt,
                rows.Where(x => x.RequestId == r.Id).OrderBy(x => x.LineNo)
                    .Select(x => new ProcurementToIssueLine(x.RequestItemId, x.ItemId, x.Sku, x.Name, x.UnitCode, x.Left, x.OnHand)).ToList()))
            .ToList();
    }

    private static string? Text(string? value, int max, string field)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return text.Length <= max ? text : throw new FieldValidationException($"{field} — до {max} символов.");
    }
}
