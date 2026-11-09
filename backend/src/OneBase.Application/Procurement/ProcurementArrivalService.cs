using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Security;
using OneBase.Application.Work;
using OneBase.Domain.Audit;
using OneBase.Domain.Procurement;
using OneBase.Domain.Work;

namespace OneBase.Application.Procurement;

/// <summary>
/// Строка «Машина приехала»: позиция тендера или заказа «Куплено» (LineId), сколько по накладной (в её единице), партия поставщика, дата
/// изготовления и срок годности.
/// </summary>
public sealed record ProcurementArrivalLineInput(Guid LineId, decimal Declared, string? SupplierBatchNo = null, DateOnly? ManufacturedOn = null, DateOnly? ExpiresOn = null);

/// <summary>
/// «Машина приехала» — по тендеру (TenderId) или по заказу «Куплено — привезёт поставщик» (OrderId): строки, склад (не указан — склад
/// тендера или заказа), госномер, водитель, накладная, комментарий, ключ повтора формы.
/// </summary>
public sealed record ProcurementArrivalInput(
    Guid? TenderId,
    IReadOnlyList<ProcurementArrivalLineInput>? Lines,
    Guid? WarehouseId = null,
    string? VehicleNo = null,
    string? DriverName = null,
    string? Reference = null,
    string? Comment = null,
    Guid? ClientKey = null,
    Guid? OrderId = null);

/// <summary>Приёмка строки: мест принято (в единице тендера или заказа), вес нетто (в единице карточки — у весовой позиции, принятой в другой единице), комментарий.</summary>
public sealed record ProcurementArrivalAcceptLine(Guid LineId, decimal Quantity, decimal? Weight = null, string? Comment = null);

/// <summary>Приёмка на склад: строки — по каждой допущенной; комментарий к приходу; версия поставки.</summary>
public sealed record ProcurementArrivalAcceptInput(IReadOnlyList<ProcurementArrivalAcceptLine>? Lines, string? Comment = null, uint? Version = null);

public sealed record ProcurementArrivalCancelInput(string? Reason, uint? Version = null);

/// <summary>
/// Позиция ожидаемой поставки (LineId — позиция тендера или заказа): заказано и поставлено (в её единице), уже на приёмке (привезли, ещё не
/// принято), осталось, цена; карточка склада, коэффициент к её единице (null — не пересчитать), весовая ли (кг, г, т), партии, срок хранения,
/// контроль качества.
/// </summary>
public sealed record ProcurementExpectedItem(
    Guid LineId,
    int LineNo,
    string Name,
    string UnitCode,
    decimal Quantity,
    decimal Received,
    decimal OnArrival,
    decimal Left,
    decimal Price,
    Guid? ItemId,
    string? Sku,
    string? ItemUnitCode,
    decimal? Factor,
    bool Weighed,
    bool TracksBatches,
    int? ShelfLifeDays,
    ProcurementQualityControl Control);

/// <summary>
/// Ждёт поставки: тендер (Kind = tender) или покупка «Куплено — привезёт поставщик» (Kind = order, номер заказа PO-, заявка) — срок,
/// просрочено, дней до срока (отрицательное — просрочено на), склад, остаток к поставке (сум), кто вёл; позиции; машины на приёмке; может ли
/// пользователь отметить «Машина приехала».
/// </summary>
public sealed record ProcurementExpectedDelivery(
    string Kind,
    Guid SourceId,
    string Number,
    string Title,
    Guid SupplierId,
    string SupplierName,
    DateOnly? DeliveryDate,
    bool Overdue,
    int? DaysLeft,
    Guid WarehouseId,
    string WarehouseName,
    decimal LeftAmount,
    string ResponsibleName,
    IReadOnlyList<ProcurementExpectedItem> Items,
    int OpenArrivals,
    bool CanArrive,
    Guid? RequestId = null,
    string? RequestNumber = null);

/// <summary>
/// Поставка в списке: источник (tender или order — номер и предмет), машина, накладная, кто и когда отметил, строк, ждут проверки, забраковано,
/// приёмка (кто, когда, приход RC-, сумма), коротко — что привезли.
/// </summary>
public sealed record ProcurementArrivalRow(
    Guid Id,
    string Number,
    ProcurementArrivalStatus Status,
    string SourceKind,
    Guid SourceId,
    string SourceNumber,
    string SourceTitle,
    Guid SupplierId,
    string SupplierName,
    Guid WarehouseId,
    string WarehouseName,
    string? VehicleNo,
    string? Reference,
    DateTimeOffset ArrivedAt,
    string RegisteredByName,
    int Lines,
    int Pending,
    int Rejected,
    DateTimeOffset? AcceptedAt,
    string? AcceptedByName,
    Guid? ReceiptId,
    string? ReceiptNumber,
    decimal AcceptedTotal,
    string Summary);

/// <summary>Поставки: status — open (на проверке и к приёмке, по умолчанию), quality, ready, accepted, returned, cancelled, done (закрытые), all; поиск; тендер или заказ.</summary>
public sealed record ProcurementArrivalSearch(string? Status = "open", string? Search = null, Guid? SourceId = null, int Page = 1, int PageSize = 25);

/// <summary>
/// Строка поставки в карточке: позиция тендера или заказа (LineId) и её карточка (единица, коэффициент, весовая, партии), по накладной, партия
/// поставщика и сроки, контроль и проверка (номер, шаг, решение, комментарий решения, проба), цена за единицу позиции и сколько ещё осталось;
/// приёмка (мест, вес, на склад, партия), комментарий.
/// </summary>
public sealed record ProcurementArrivalLineView(
    Guid Id,
    int LineNo,
    Guid LineId,
    Guid ItemId,
    string Sku,
    string Name,
    string UnitCode,
    string ItemUnitCode,
    decimal? Factor,
    bool Weighed,
    bool TracksBatches,
    decimal Declared,
    decimal DeclaredBase,
    string? SupplierBatchNo,
    DateOnly? ManufacturedOn,
    DateOnly? ExpiresOn,
    ProcurementQualityControl Control,
    Guid? QualityCheckId,
    string? QualityCheckNumber,
    ProcurementQualityCheckStatus? CheckStatus,
    ProcurementBatchQuality Quality,
    string? DecisionComment,
    decimal? SampleQty,
    decimal Price,
    decimal Left,
    decimal? AcceptedQty,
    decimal? AcceptedWeight,
    decimal? StockQty,
    Guid? BatchId,
    string? BatchNumber,
    string? Comment);

/// <summary>Что можно с поставкой: принять на склад (кладовщик склада, когда готово к приёмке), отменить (кладовщик или снабжение — до приёмки).</summary>
public sealed record ProcurementArrivalAbilities(bool Accept, bool Cancel);

public sealed record ProcurementArrivalDetail(
    ProcurementArrivalRow Arrival,
    string? DriverName,
    string? Comment,
    DateTimeOffset? ReadyAt,
    DateTimeOffset? CancelledAt,
    string? CancelReason,
    uint Version,
    Guid? RequestId,
    string? RequestNumber,
    IReadOnlyList<ProcurementArrivalLineView> Lines,
    IReadOnlyList<ProcurementEventView> History,
    ProcurementArrivalAbilities Can);

/// <summary>
/// Приезд машины и приёмка на склад в порядке «качество → склад» — по тендеру и по покупке «Куплено — привезёт поставщик». Кладовщик отмечает
/// «Машина приехала» — склад не меняется, по строкам с контролем качества заводятся проверки (лаборатории и назначенным — уведомление).
/// Лаборатория проверяет в очереди «Качество»; решено по всем строкам — «готово к приёмке» (кладовщикам — уведомление) или, если забраковано
/// всё, «возврат». Кладовщик пишет фактические места и вес и принимает: приход RC- по цене тендера или покупки, партии сразу допущены (со
/// ссылкой на свою проверку), растёт «поставлено» у тендера или «получено» у заказа — покупка «Куплено» резервируется под заявку (к выдаче);
/// снабжению, аналитикам, финансисту и автору заявки — «принято на склад». Повтор формы и двойное нажатие второй раз не проводят.
/// </summary>
public sealed class ProcurementArrivalService(IAppDbContext db, IProcurementNumbers numbers, ProcurementStockLedger ledger, UserNotifier notifier, IAuditLogger audit)
{
    public const string Doc = "arrival";
    public const string Prefix = "AR";
    public const int MaxLines = 50;
    public const string TenderKind = "tender";
    public const string OrderKind = "order";

    private static DateTimeOffset Now => DateTimeOffset.UtcNow;

    /// <summary>Весовая единица карточки: при приёмке на склад идёт вес нетто, а не места.</summary>
    public static bool Weighed(string itemUnit) => itemUnit is "кг" or "г" or "т";

    /// <summary>Видят поставки: кладовщик, лаборатория, назначенные по качеству, снабжение, аналитик, директор, финансист маршрута.</summary>
    public static bool Sees(ProcurementContext ctx) => ctx.SeesQuality || ctx.IsFinancier;

    private static void RequireSees(ProcurementContext ctx)
    {
        if (!Sees(ctx))
        {
            throw new FieldForbiddenException("Поставки видят кладовщик, лаборатория, снабжение, аналитик, директор и финансист.");
        }
    }

    private static readonly ProcurementArrivalStatus[] Open = [ProcurementArrivalStatus.Quality, ProcurementArrivalStatus.Ready];

    /// <summary>Заказы «Куплено», которые ждут машину: поставщик привезёт, принято не всё.</summary>
    private static readonly ProcurementOrderStatus[] Awaiting = [ProcurementOrderStatus.Ordered, ProcurementOrderStatus.PartiallyDelivered];

    // ---------- Источник поставки: тендер или заказ «Куплено» ----------

    /// <summary>Позиция источника: сколько заказано, сколько уже закрыто (поставлено или закрыто без поставки), цена за её единицу, карточка склада.</summary>
    private sealed record SourceLine(Guid Id, int LineNo, string Name, string UnitCode, decimal Quantity, decimal Done, decimal Price, Guid? ItemId, decimal Received);

    /// <summary>Тендер или заказ «Куплено»: номер, предмет, поставщик, срок, склад, кто вёл, заявка, позиции.</summary>
    private sealed record Source(
        string Kind,
        Guid Id,
        string Number,
        string Title,
        Guid SupplierId,
        string SupplierName,
        DateOnly? DeliveryDate,
        Guid? WarehouseId,
        Guid ResponsibleId,
        string ResponsibleName,
        Guid? RequestId,
        string? RequestNumber,
        IReadOnlyList<SourceLine> Lines);

    private async Task<List<Source>> TendersAsync(Guid? id, CancellationToken ct) =>
        (await db.ProcurementTenders.AsNoTracking().Include(t => t.Items).Include(t => t.Supplier).Include(t => t.Responsible).Include(t => t.Request)
            .Where(t => t.Status == ProcurementTenderStatus.Contracted && (id == null || t.Id == id))
            .ToListAsync(ct))
        .Select(t => new Source(TenderKind, t.Id, t.Number, t.Title, t.SupplierId, t.Supplier!.Name, t.DeliveryDate, t.WarehouseId, t.ResponsibleId, t.Responsible!.FullName,
            t.RequestId, t.Request?.Number,
            t.Items.OrderBy(i => i.LineNo).Select(i => new SourceLine(i.Id, i.LineNo, i.Name, i.UnitCode, i.Quantity, i.ReceivedQty, i.Price, i.ItemId, i.ReceivedQty)).ToList()))
        .ToList();

    private async Task<List<Source>> OrdersAsync(Guid? id, CancellationToken ct) =>
        (await db.ProcurementPurchaseOrders.AsNoTracking().Include(o => o.Items).ThenInclude(i => i.RequestItem).ThenInclude(i => i!.Request)
            .Include(o => o.Supplier).Include(o => o.Responsible)
            .Where(o => o.Direct && Awaiting.Contains(o.Status) && (id == null || o.Id == id))
            .ToListAsync(ct))
        .Select(o =>
        {
            var request = o.Items.Select(i => i.RequestItem!.Request!).FirstOrDefault();
            return new Source(OrderKind, o.Id, o.Number, request is null ? o.Number : $"Куплено по заявке {request.Number}: {request.Title}", o.SupplierId, o.Supplier!.Name,
                o.ExpectedDate, o.WarehouseId, o.ResponsibleId, o.Responsible!.FullName, request?.Id, request?.Number,
                o.Items.OrderBy(i => i.LineNo).Select(i => new SourceLine(i.Id, i.LineNo, i.Name, i.UnitCode, i.Quantity, i.ReceivedQty + i.ClosedQty, i.UnitPrice,
                    i.RequestItem!.ItemId, i.ReceivedQty)).ToList());
        })
        .ToList();

    /// <summary>Привезено и ещё не принято (поставки на проверке и к приёмке, кроме забракованного) — по позициям источника.</summary>
    private async Task<Dictionary<Guid, decimal>> OnArrivalAsync(IReadOnlyCollection<Guid> lineIds, CancellationToken ct)
    {
        var lines = await db.ProcurementArrivalLines.AsNoTracking()
            .Where(l => ((l.TenderItemId != null && lineIds.Contains(l.TenderItemId.Value)) || (l.OrderItemId != null && lineIds.Contains(l.OrderItemId.Value)))
                        && Open.Contains(l.Arrival!.Status) && l.Quality != ProcurementBatchQuality.Rejected)
            .Select(l => new { Id = l.TenderItemId ?? l.OrderItemId!.Value, l.Declared })
            .ToListAsync(ct);
        return lines.GroupBy(l => l.Id).ToDictionary(g => g.Key, g => g.Sum(l => l.Declared));
    }

    // ---------- Ожидаемые поставки ----------

    /// <summary>
    /// Ждут поставки — тендеры с договором и покупки «Куплено — привезёт поставщик», по которым поставлено не всё: сначала просроченные, потом по
    /// сроку, без срока — в конце. Осталось — за вычетом уже привезённого на приёмку (не забракованного). Один источник (sourceId) или первые take.
    /// </summary>
    public async Task<IReadOnlyList<ProcurementExpectedDelivery>> ExpectedAsync(ProcurementContext ctx, Guid? sourceId, int take, CancellationToken ct)
    {
        RequireSees(ctx);
        var today = ledger.Today;
        var sources = (await TendersAsync(sourceId, ct)).Concat(await OrdersAsync(sourceId, ct))
            .OrderBy(s => s.DeliveryDate == null).ThenBy(s => s.DeliveryDate).ThenBy(s => s.Number)
            .Take(Math.Clamp(take, 1, 200))
            .ToList();
        if (sources.Count == 0)
        {
            return [];
        }

        var lineIds = sources.SelectMany(s => s.Lines).Select(l => l.Id).ToList();
        var onArrival = await OnArrivalAsync(lineIds, ct);
        var ids = sources.Select(s => s.Id).ToList();
        var arrivals = (await db.ProcurementArrivals.AsNoTracking()
                .Where(a => ((a.TenderId != null && ids.Contains(a.TenderId.Value)) || (a.OrderId != null && ids.Contains(a.OrderId.Value))) && Open.Contains(a.Status))
                .Select(a => a.TenderId ?? a.OrderId!.Value).ToListAsync(ct))
            .GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        var itemIds = sources.SelectMany(s => s.Lines).Select(l => l.ItemId).OfType<Guid>().Distinct().ToList();
        var cards = await db.ProcurementItems.AsNoTracking().Include(i => i.Units).Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var defaultWarehouse = await ledger.DefaultWarehouseIdAsync(ct);
        var warehouseIds = sources.Select(s => s.WarehouseId ?? defaultWarehouse).Distinct().ToList();
        var warehouses = await db.ProcurementWarehouses.AsNoTracking().Where(w => warehouseIds.Contains(w.Id)).ToDictionaryAsync(w => w.Id, w => w.Name, ct);

        var result = new List<ProcurementExpectedDelivery>();
        foreach (var s in sources)
        {
            var items = s.Lines.Select(l =>
            {
                var card = l.ItemId is { } id ? cards.GetValueOrDefault(id) : null;
                var factor = card is null ? (decimal?)null : ProcurementUnitRules.Factor(card.UnitCode, l.UnitCode, card.Units.Select(u => (u.UnitCode, u.Factor)));
                // Покупка «Куплено» принимается под заявку в её единице — только в единице карточки.
                if (s.Kind == OrderKind && factor is not 1m)
                {
                    factor = null;
                }

                var arriving = onArrival.GetValueOrDefault(l.Id);
                return new ProcurementExpectedItem(l.Id, l.LineNo, l.Name, l.UnitCode, l.Quantity, l.Received, arriving, Math.Max(0, l.Quantity - l.Done - arriving), l.Price,
                    l.ItemId, card?.Sku, card?.UnitCode, factor, card is not null && Weighed(card.UnitCode) && card.UnitCode != l.UnitCode, card?.TracksBatches ?? false,
                    card?.ShelfLifeDays, card is { TracksBatches: true } ? card.QualityControl : ProcurementQualityControl.None);
            }).ToList();
            var open = arrivals.GetValueOrDefault(s.Id);
            if (items.All(i => i.Left <= 0) && open == 0)
            {
                continue;
            }

            var warehouseId = s.WarehouseId ?? defaultWarehouse;
            var can = ctx.Warehouse && ctx.Posts(warehouseId) && items.Any(i => i.ItemId is not null && i.Factor is not null && i.Left > 0);
            result.Add(new ProcurementExpectedDelivery(s.Kind, s.Id, s.Number, s.Title, s.SupplierId, s.SupplierName, s.DeliveryDate,
                s.DeliveryDate is { } due && due < today, s.DeliveryDate is { } d ? d.DayNumber - today.DayNumber : null, warehouseId,
                warehouses.GetValueOrDefault(warehouseId, ""), Math.Round(items.Sum(i => i.Left * i.Price), 2), s.ResponsibleName, items, open, can, s.RequestId,
                s.RequestNumber));
        }

        return result;
    }

    // ---------- Список и карточка ----------

    private IQueryable<ProcurementArrivalRow> Rows(IQueryable<ProcurementArrival> q) =>
        q.Select(a => new ProcurementArrivalRow(a.Id, a.Number, a.Status, a.TenderId != null ? TenderKind : OrderKind, a.TenderId ?? a.OrderId!.Value,
            a.Tender != null ? a.Tender.Number : a.Order!.Number,
            a.Tender != null
                ? a.Tender.Title
                : a.Order!.Items.Select(i => "Куплено по заявке " + i.RequestItem!.Request!.Number + ": " + i.RequestItem.Request.Title).FirstOrDefault() ?? a.Order.Number,
            a.SupplierId, a.Supplier!.Name, a.WarehouseId,
            db.ProcurementWarehouses.Where(w => w.Id == a.WarehouseId).Select(w => w.Name).FirstOrDefault() ?? "", a.VehicleNo, a.Reference, a.ArrivedAt,
            a.RegisteredBy!.FullName, a.Lines.Count, a.Lines.Count(l => l.Quality == ProcurementBatchQuality.Pending),
            a.Lines.Count(l => l.Quality == ProcurementBatchQuality.Rejected), a.AcceptedAt, a.AcceptedBy == null ? null : a.AcceptedBy.FullName, a.ReceiptId,
            db.ProcurementStockDocuments.Where(d => d.Id == a.ReceiptId).Select(d => d.Number).FirstOrDefault(), a.AcceptedTotal,
            string.Join(", ", a.Lines.OrderBy(l => l.LineNo).Select(l => l.Item!.Name).Distinct())));

    public async Task<PageResult<ProcurementArrivalRow>> SearchAsync(ProcurementContext ctx, ProcurementArrivalSearch s, CancellationToken ct)
    {
        RequireSees(ctx);
        var q = db.ProcurementArrivals.AsNoTracking();
        q = s.Status switch
        {
            "quality" => q.Where(a => a.Status == ProcurementArrivalStatus.Quality),
            "ready" => q.Where(a => a.Status == ProcurementArrivalStatus.Ready),
            "accepted" => q.Where(a => a.Status == ProcurementArrivalStatus.Accepted),
            "returned" => q.Where(a => a.Status == ProcurementArrivalStatus.Returned),
            "cancelled" => q.Where(a => a.Status == ProcurementArrivalStatus.Cancelled),
            "done" => q.Where(a => !Open.Contains(a.Status)),
            "all" => q,
            _ => q.Where(a => Open.Contains(a.Status)),
        };
        if (s.SourceId is { } source)
        {
            q = q.Where(a => a.TenderId == source || a.OrderId == source);
        }

        if (s.Search?.Trim().ToLower() is { Length: > 0 } text)
        {
            q = q.Where(a => a.Number.ToLower().Contains(text) || (a.Tender != null && a.Tender.Number.ToLower().Contains(text))
                             || (a.Order != null && a.Order.Number.ToLower().Contains(text)) || a.Supplier!.Name.ToLower().Contains(text)
                             || (a.VehicleNo != null && a.VehicleNo.ToLower().Contains(text)) || (a.Reference != null && a.Reference.ToLower().Contains(text))
                             || a.Lines.Any(l => l.Item!.Name.ToLower().Contains(text) || (l.SupplierBatchNo != null && l.SupplierBatchNo.ToLower().Contains(text))));
        }

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, s.Page);
        var size = Math.Clamp(s.PageSize, 1, 200);
        // Открытые — сначала самые давние (машина ждёт), закрытые — сначала последние.
        q = s.Status is null or "" or "open" or "quality" or "ready" ? q.OrderBy(a => a.ArrivedAt) : q.OrderByDescending(a => a.AcceptedAt ?? a.CancelledAt ?? a.ArrivedAt);
        var rows = await Rows(q.Skip((page - 1) * size).Take(size)).ToListAsync(ct);
        return new PageResult<ProcurementArrivalRow>(rows, total, page, size);
    }

    public async Task<ProcurementArrivalDetail> GetAsync(ProcurementContext ctx, Guid id, CancellationToken ct)
    {
        RequireSees(ctx);
        var row = await Rows(db.ProcurementArrivals.AsNoTracking().Where(a => a.Id == id)).FirstOrDefaultAsync(ct) ?? throw new FieldNotFoundException("Поставка не найдена.");
        var a = await db.ProcurementArrivals.AsNoTracking()
            .Include(x => x.Lines).ThenInclude(l => l.Item).ThenInclude(i => i!.Units)
            .Include(x => x.Lines).ThenInclude(l => l.QualityCheck)
            .Include(x => x.Lines).ThenInclude(l => l.Batch)
            .Include(x => x.Lines).ThenInclude(l => l.TenderItem)
            .Include(x => x.Lines).ThenInclude(l => l.OrderItem).ThenInclude(i => i!.RequestItem)
            .Include(x => x.Tender)
            .FirstAsync(x => x.Id == id, ct);
        var history = await db.ProcurementAuditLogs.AsNoTracking().Where(e => e.DocType == Doc && e.DocId == id).OrderBy(e => e.CreatedAt).ThenBy(e => e.Id)
            .Select(e => new ProcurementEventView(e.Id, e.Action, e.Text, e.Actor == null ? "Система" : e.Actor.FullName, e.FromStatus, e.ToStatus, e.Comment, e.CreatedAt))
            .ToListAsync(ct);
        var requestId = a.Tender?.RequestId ?? a.Lines.Select(l => l.OrderItem?.RequestItem?.RequestId).FirstOrDefault(r => r is not null);
        var request = requestId is { } rid
            ? await db.ProcurementRequests.AsNoTracking().Where(r => r.Id == rid).Select(r => r.Number).FirstOrDefaultAsync(ct)
            : null;
        var lines = a.Lines.OrderBy(l => l.LineNo).Select(l =>
        {
            var card = l.Item!;
            var check = l.QualityCheck;
            var (lineId, price, left) = l.TenderItem is { } ti
                ? (ti.Id, ti.Price, Math.Max(0, ti.Quantity - ti.ReceivedQty))
                : (l.OrderItem!.Id, l.OrderItem.UnitPrice, ProcurementOrderRules.Remaining(l.OrderItem.Quantity, l.OrderItem.ReceivedQty, l.OrderItem.ClosedQty));
            return new ProcurementArrivalLineView(l.Id, l.LineNo, lineId, l.ItemId, card.Sku, card.Name, l.UnitCode, card.UnitCode,
                ProcurementUnitRules.Factor(card.UnitCode, l.UnitCode, card.Units.Select(u => (u.UnitCode, u.Factor))), Weighed(card.UnitCode) && card.UnitCode != l.UnitCode,
                card.TracksBatches, l.Declared, l.DeclaredBase, l.SupplierBatchNo, l.ManufacturedOn, l.ExpiresOn, l.Control, l.QualityCheckId, check?.Number, check?.Status,
                l.Quality, check?.DecisionComment, check?.SampleQty, price, left, l.AcceptedQty, l.AcceptedWeight, l.StockQty, l.BatchId, l.Batch?.Number, l.Comment);
        }).ToList();
        var can = new ProcurementArrivalAbilities(a.Status == ProcurementArrivalStatus.Ready && ctx.Posts(a.WarehouseId),
            Open.Contains(a.Status) && (ctx.Posts(a.WarehouseId) || ctx.Manages));
        return new ProcurementArrivalDetail(row, a.DriverName, a.Comment, a.ReadyAt, a.CancelledAt, a.CancelReason, a.Version, requestId, request, lines, history, can);
    }

    // ---------- Машина приехала ----------

    /// <summary>
    /// «Машина приехала» (кладовщик склада) — по тендеру или по покупке «Куплено — привезёт поставщик»: по позициям с карточкой склада —
    /// сколько по накладной (не больше осталось, за вычетом уже привезённого на приёмку); у позиции с партиями — партия поставщика и срок (или
    /// дата изготовления при сроке хранения). Одну позицию можно привезти несколькими партиями — по строке на партию. Склад не меняется.
    /// Строки с контролем качества — проверки QC- в очереди «Качество»; без контроля — сразу к приёмке. Повтор формы с тем же ключом не
    /// заводит вторую поставку.
    /// </summary>
    public Task<Guid> RegisterAsync(ProcurementContext ctx, ProcurementArrivalInput input, CancellationToken ct) => ledger.RetryAsync(async () =>
    {
        if (!ctx.Warehouse)
        {
            throw new FieldForbiddenException("Приезд машины отмечает кладовщик.");
        }

        if (input.ClientKey is { } key)
        {
            var done = await db.ProcurementArrivals.AsNoTracking().Where(a => a.RegisteredById == ctx.UserId && a.ClientKey == key).Select(a => a.Id).FirstOrDefaultAsync(ct);
            if (done != Guid.Empty)
            {
                return done;
            }
        }

        if ((input.TenderId is null) == (input.OrderId is null))
        {
            throw new FieldValidationException("Укажите, по чему приехала машина: тендер или покупка «Куплено».");
        }

        var source = input.TenderId is { } tenderId
            ? (await TendersAsync(tenderId, ct)).FirstOrDefault() ?? await ClosedSourceAsync(tenderId, true, ct)
            : (await OrdersAsync(input.OrderId, ct)).FirstOrDefault() ?? await ClosedSourceAsync(input.OrderId!.Value, false, ct);

        var lines = (input.Lines ?? []).Where(l => l.Declared != 0).ToList();
        if (lines.Count == 0)
        {
            throw new FieldValidationException("Укажите, что привезли: количество по накладной хотя бы по одной позиции.");
        }

        if (lines.Count > MaxLines)
        {
            throw new FieldValidationException($"В поставке — до {MaxLines} строк.");
        }

        var store = await ledger.WarehouseAsync(ctx, input.WarehouseId ?? source.WarehouseId, ct);
        var itemIds = source.Lines.Select(i => i.ItemId).OfType<Guid>().Distinct().ToList();
        var cards = await db.ProcurementItems.AsNoTracking().Include(i => i.Units).Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var onArrival = await OnArrivalAsync(source.Lines.Select(l => l.Id).ToList(), ct);
        var arrival = new ProcurementArrival
        {
            Number = await numbers.NextAsync(Prefix, ct),
            TenderId = source.Kind == TenderKind ? source.Id : null,
            OrderId = source.Kind == OrderKind ? source.Id : null,
            SupplierId = source.SupplierId,
            WarehouseId = store.Id,
            VehicleNo = Text(input.VehicleNo, 32, "Номер машины"),
            DriverName = Text(input.DriverName, 200, "Водитель"),
            Reference = Text(input.Reference, 100, "Номер накладной"),
            Comment = Text(input.Comment, 1000, "Комментарий"),
            ArrivedAt = Now,
            RegisteredById = ctx.UserId,
            ClientKey = input.ClientKey,
        };
        var checks = new List<(ProcurementQualityCheck Check, string Label)>();
        var brought = new List<string>();
        var lineNo = 0;
        foreach (var group in lines.GroupBy(l => l.LineId))
        {
            var sl = source.Lines.FirstOrDefault(i => i.Id == group.Key)
                     ?? throw new FieldValidationException(source.Kind == TenderKind ? "Позиция не из этой тендерной закупки." : "Позиция не из этой покупки.");
            if (sl.ItemId is not { } itemId || cards.GetValueOrDefault(itemId) is not { } card)
            {
                throw new FieldValidationException(source.Kind == TenderKind
                    ? $"«{sl.Name}»: позиция не связана с карточкой склада — свяжите её в тендере, чтобы принять на склад."
                    : $"«{sl.Name}»: позиция заявки не связана с карточкой склада.");
            }

            var factor = ProcurementUnitRules.Factor(card.UnitCode, sl.UnitCode, card.Units.Select(u => (u.UnitCode, u.Factor)))
                         ?? throw new FieldValidationException(
                             $"«{sl.Name}»: единица «{sl.UnitCode}» не совпадает с карточкой ({card.UnitCode}) — добавьте её в единицы закупки карточки.");
            if (source.Kind == OrderKind && factor != 1)
            {
                throw new FieldValidationException($"«{sl.Name}»: покупка в «{sl.UnitCode}», а карточка склада — в «{card.UnitCode}»: под заявку принимается только в единице карточки.");
            }

            var label = $"{card.Sku} {card.Name}";
            var arriving = onArrival.GetValueOrDefault(sl.Id);
            var left = sl.Quantity - sl.Done - arriving;
            var declared = group.Sum(l => l.Declared);
            if (declared > left)
            {
                var waiting = arriving > 0 ? $" (ещё {ProcurementRules.Quantity(arriving)} {sl.UnitCode} — уже на приёмке)" : "";
                var by = source.Kind == TenderKind ? "по тендеру" : "по покупке";
                throw new FieldConflictException(left > 0
                    ? $"«{sl.Name}»: {by} осталось поставить {ProcurementRules.Quantity(left)} {sl.UnitCode}{waiting} — больше не принять."
                    : $"«{sl.Name}»: {by} поставлено всё{waiting}.");
            }

            var control = card.TracksBatches ? card.QualityControl : ProcurementQualityControl.None;
            foreach (var l in group)
            {
                if (l.Declared < 0 || l.Declared > ProcurementRules.MaxQuantity || decimal.Round(l.Declared, 3) != l.Declared)
                {
                    throw new FieldValidationException($"«{sl.Name}»: по накладной — больше нуля, не больше трёх знаков после запятой.");
                }

                var baseQty = ProcurementUnitRules.ToBase(l.Declared, factor)
                              ?? throw new FieldValidationException($"«{sl.Name}»: {ProcurementRules.Quantity(l.Declared)} {sl.UnitCode} — больше трёх знаков в {card.UnitCode}.");
                var lot = string.IsNullOrWhiteSpace(l.SupplierBatchNo) ? null : l.SupplierBatchNo.Trim();
                DateOnly? expires = null;
                if (card.TracksBatches)
                {
                    expires = ProcurementBatchRules.ExpiresOn(l.ExpiresOn, l.ManufacturedOn, card.ShelfLifeDays);
                    if (ProcurementBatchRules.Check(label, new ProcurementBatchInput(lot, l.ManufacturedOn, l.ExpiresOn, baseQty), expires, ledger.Today) is { } error)
                    {
                        throw new FieldValidationException(error);
                    }
                }
                else if (lot is not null || l.ManufacturedOn is not null || l.ExpiresOn is not null)
                {
                    throw new FieldValidationException($"«{label}»: позиция склада ведётся без партий — партию и срок не указывайте.");
                }

                var line = new ProcurementArrivalLine
                {
                    ArrivalId = arrival.Id,
                    LineNo = ++lineNo,
                    TenderItemId = source.Kind == TenderKind ? sl.Id : null,
                    OrderItemId = source.Kind == OrderKind ? sl.Id : null,
                    ItemId = card.Id,
                    UnitCode = sl.UnitCode,
                    Declared = l.Declared,
                    DeclaredBase = baseQty,
                    SupplierBatchNo = lot,
                    ManufacturedOn = l.ManufacturedOn,
                    ExpiresOn = expires,
                    Control = control,
                    Quality = control == ProcurementQualityControl.None ? ProcurementBatchQuality.Approved : ProcurementBatchQuality.Pending,
                };
                var what = $"{card.Name} — {ProcurementRules.Quantity(l.Declared)} {sl.UnitCode}{(lot is null ? "" : $", партия {lot}")}";
                if (control != ProcurementQualityControl.None)
                {
                    var check = new ProcurementQualityCheck
                    {
                        Number = await numbers.NextAsync(ProcurementQualityRules.Prefix, ct), ItemId = card.Id, SupplierId = source.SupplierId, ArrivalId = arrival.Id,
                        WarehouseId = store.Id, Control = control,
                    };
                    db.ProcurementQualityChecks.Add(check);
                    db.ProcurementAuditLogs.Add(new ProcurementAuditLog
                    {
                        DocType = ProcurementQualityService.CheckDoc, DocId = check.Id, ActorId = ctx.UserId, Action = "created",
                        Text = Cut($"Машина приехала — поставка {arrival.Number} от «{source.SupplierName}»: {what}. Товар ещё не на складе — его примут после проверки", 1000),
                    });
                    line.QualityCheckId = check.Id;
                    checks.Add((check, what));
                }

                arrival.Lines.Add(line);
                brought.Add(what);
            }
        }

        arrival.Status = checks.Count > 0 ? ProcurementArrivalStatus.Quality : ProcurementArrivalStatus.Ready;
        arrival.ReadyAt = checks.Count > 0 ? null : Now;
        db.ProcurementArrivals.Add(arrival);
        var vehicle = arrival.VehicleNo is { } v ? $" ({v})" : "";
        Log(ctx.UserId, arrival.Id, "registered", null, arrival.Status,
            $"Машина приехала{vehicle}: {string.Join("; ", brought)}{(checks.Count > 0 ? $". На проверку качества — {checks.Count} поз." : ". Контроль качества не нужен — можно принимать")}",
            arrival.Reference);
        db.ProcurementAuditLogs.Add(new ProcurementAuditLog
        {
            DocType = SourceDoc(source.Kind), DocId = source.Id, ActorId = ctx.UserId, Action = "arrived",
            Text = Cut($"Машина приехала — поставка {arrival.Number}{vehicle}: {string.Join("; ", brought)}", 1000), Comment = arrival.Reference,
        });

        // Лаборатории и назначенным — проверить; снабжению (кто вёл тендер или покупку) — машина приехала.
        if (checks.Count > 0)
        {
            var title = $"Приехала машина: {source.SupplierName} — проверьте качество";
            var body = Cut($"Поставка {arrival.Number}{vehicle}: {string.Join("; ", checks.Select(c => c.Label))}", 500);
            var link = checks.Count == 1 ? $"/production/quality/{checks[0].Check.Id}" : $"/procurement/arrivals/{arrival.Id}";
            var lab = (await ProcurementPeople.WithPermissionAsync(db, Permissions.QualityCheck, ct)).Concat(await ProcurementQualityNotices.DecidersAsync(db, ct));
            foreach (var user in lab.Distinct().Where(u => u != ctx.UserId))
            {
                notifier.Notify(user, UserNotificationKind.ProcurementRequest, title, body, link);
            }
        }

        if (source.ResponsibleId != ctx.UserId)
        {
            notifier.Notify(source.ResponsibleId, UserNotificationKind.ProcurementStatus,
                source.Kind == TenderKind ? $"Машина приехала по тендеру {source.Number}" : $"Машина приехала по покупке {source.Number}",
                Cut($"{source.SupplierName}, поставка {arrival.Number}: {string.Join("; ", brought)}", 500), $"/procurement/arrivals/{arrival.Id}");
        }

        await db.SaveChangesAsync(ct);
        return arrival.Id;
    });

    /// <summary>Источник есть, но поставку по нему больше не ждут (поставлено, отменён) — 409 с понятным текстом; нет — 404.</summary>
    private async Task<Source> ClosedSourceAsync(Guid id, bool tender, CancellationToken ct)
    {
        if (tender)
        {
            var t = await db.ProcurementTenders.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Number, x.Status }).FirstOrDefaultAsync(ct)
                    ?? throw new FieldNotFoundException("Тендерная закупка не найдена.");
            throw new FieldConflictException(t.Status == ProcurementTenderStatus.Delivered
                ? $"По тендеру {t.Number} всё поставлено — принимать больше нечего."
                : $"Тендерная закупка {t.Number} отменена — поставку по ней не принять.");
        }

        var o = await db.ProcurementPurchaseOrders.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Number, x.Status, x.Direct }).FirstOrDefaultAsync(ct)
                ?? throw new FieldNotFoundException("Покупка не найдена.");
        throw new FieldConflictException(!o.Direct
            ? $"Заказ {o.Number} принимается приёмкой по заказу."
            : o.Status == ProcurementOrderStatus.Cancelled
                ? $"Покупка {o.Number} отменена — поставку по ней не принять."
                : $"По покупке {o.Number} всё поставлено — принимать больше нечего.");
    }

    private static string SourceDoc(string kind) => kind == TenderKind ? ProcurementTenderService.Doc : ProcurementOrderRules.OrderDoc;

    // ---------- Приёмка ----------

    /// <summary>
    /// «Принять на склад» (кладовщик склада, поставка готова к приёмке): по каждой допущенной строке — мест принято (в единице тендера или
    /// покупки) и вес нетто, если карточка весовая, а тендер в другой единице: на склад идёт вес, тендеру засчитываются места. Ничего не принято
    /// по строке — с причиной. Одной записью: приход RC- по цене тендера или покупки, партии — сразу допущенные (со ссылкой на свою проверку),
    /// «поставлено» у тендера (всё — тендер поставлен) или «получено» у покупки «Куплено» — принятое резервируется под заявку (к выдаче), всё
    /// получено — покупка завершена; поставка принята; снабжению, аналитикам, финансисту и автору заявки — уведомление. Повтор — тот же приход.
    /// </summary>
    public Task<Guid> AcceptAsync(ProcurementContext ctx, Guid id, ProcurementArrivalAcceptInput input, CancellationToken ct) => ledger.RetryAsync(async () =>
    {
        if (!ctx.Warehouse)
        {
            throw new FieldForbiddenException("На склад принимает кладовщик.");
        }

        var arrival = await db.ProcurementArrivals.Include(a => a.Lines).Include(a => a.Supplier).FirstOrDefaultAsync(a => a.Id == id, ct)
                      ?? throw new FieldNotFoundException("Поставка не найдена.");
        if (arrival is { Status: ProcurementArrivalStatus.Accepted, ReceiptId: { } receipt })
        {
            return receipt;
        }

        if (arrival.Status != ProcurementArrivalStatus.Ready)
        {
            throw new FieldConflictException(arrival.Status switch
            {
                ProcurementArrivalStatus.Quality =>
                    $"Лаборатория ещё проверяет: ждут решения {arrival.Lines.Count(l => l.Quality == ProcurementBatchQuality.Pending)} поз. — принять можно после проверки.",
                ProcurementArrivalStatus.Returned => $"Поставка {arrival.Number} забракована целиком — принимать нечего, верните поставщику.",
                _ => $"Поставка {arrival.Number} отменена — её не принять.",
            });
        }

        if (input.Version is { } version && version != arrival.Version)
        {
            throw new FieldConflictException($"Поставку {arrival.Number} уже изменили — обновите страницу.");
        }

        ProcurementTender? tender = null;
        ProcurementPurchaseOrder? order = null;
        if (arrival.TenderId is { } tenderId)
        {
            tender = await db.ProcurementTenders.Include(t => t.Items).FirstAsync(t => t.Id == tenderId, ct);
            if (tender.Status == ProcurementTenderStatus.Cancelled)
            {
                throw new FieldConflictException($"Тендерная закупка {tender.Number} отменена — поставку по ней не принять.");
            }
        }
        else
        {
            order = await db.ProcurementPurchaseOrders.Include(o => o.Items).ThenInclude(i => i.RequestItem).ThenInclude(i => i!.Request)
                .FirstAsync(o => o.Id == arrival.OrderId, ct);
            if (!Awaiting.Contains(order.Status))
            {
                throw new FieldConflictException($"Покупка {order.Number} {(order.Status == ProcurementOrderStatus.Cancelled ? "отменена" : "закрыта")} — поставку по ней не принять.");
            }
        }

        var source = tender?.Number ?? order!.Number;
        var request = order?.Items.Select(i => i.RequestItem!.Request!).FirstOrDefault();
        var store = await ledger.WarehouseAsync(ctx, arrival.WarehouseId, ct);
        var entered = input.Lines ?? [];
        if (entered.GroupBy(l => l.LineId).Any(g => g.Count() > 1))
        {
            throw new FieldValidationException("Строка поставки повторяется — укажите её один раз.");
        }

        var byLine = entered.ToDictionary(l => l.LineId);
        foreach (var l in entered)
        {
            var line = arrival.Lines.FirstOrDefault(x => x.Id == l.LineId) ?? throw new FieldValidationException("Строка не из этой поставки.");
            if (line.Quality == ProcurementBatchQuality.Rejected && l.Quantity != 0)
            {
                throw new FieldValidationException($"Строка {line.LineNo} забракована лабораторией — её не принять, верните поставщику.");
            }
        }

        var acceptable = arrival.Lines.Where(l => l.Quality == ProcurementBatchQuality.Approved).OrderBy(l => l.LineNo).ToList();
        var itemIds = acceptable.Select(l => l.ItemId).Distinct().ToList();
        var cards = await db.ProcurementItems.AsNoTracking().Include(i => i.Units).Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var missing = acceptable.FirstOrDefault(l => !byLine.ContainsKey(l.Id));
        if (missing is not null)
        {
            throw new FieldValidationException($"Укажите, сколько принято: «{cards[missing.ItemId].Name}» (строка {missing.LineNo}).");
        }

        var balances = await ledger.BalancesAsync(store.Id, itemIds, ct);
        var requestItemIds = order?.Items.Select(i => i.RequestItemId).ToList() ?? [];
        var active = await db.ProcurementStockReservations
            .Where(x => requestItemIds.Contains(x.RequestItemId) && x.Status == ProcurementReservationStatus.Active).ToDictionaryAsync(x => x.RequestItemId, ct);
        var doc = await ledger.DocumentAsync(ProcurementStockDocumentKind.Receipt, store.Id, ctx.UserId, ct);
        doc.TenderId = tender?.Id;
        doc.OrderId = order?.Id;
        doc.RequestId = request?.Id;
        doc.DepartmentId = request?.DepartmentId;
        doc.SupplierId = arrival.SupplierId;
        doc.PostedAt = Now;
        doc.Reference = arrival.Reference;
        var defaultComment = tender is not null ? $"Поставка {arrival.Number} по тендеру {source}" : $"Поставка {arrival.Number} по покупке {source}";
        doc.Comment = Text(input.Comment, 1000, "Комментарий") ?? defaultComment;
        var taken = new Dictionary<Guid, decimal>();
        var received = new List<string>();
        var differences = new List<string>();
        var total = 0m;
        var receiptLineNo = 0;
        foreach (var line in acceptable)
        {
            var l = byLine[line.Id];
            var card = cards[line.ItemId];
            var label = $"{card.Sku} {card.Name}";
            var ti = tender?.Items.First(i => i.Id == line.TenderItemId);
            var oi = order?.Items.First(i => i.Id == line.OrderItemId);
            var factor = ProcurementUnitRules.Factor(card.UnitCode, line.UnitCode, card.Units.Select(u => (u.UnitCode, u.Factor)))
                         ?? throw new FieldValidationException($"«{card.Name}»: единицы «{line.UnitCode}» больше нет в карточке — верните её в единицы закупки.");
            if (l.Quantity < 0 || l.Quantity > ProcurementRules.MaxQuantity || decimal.Round(l.Quantity, 3) != l.Quantity)
            {
                throw new FieldValidationException($"«{card.Name}»: принято — не меньше нуля, не больше трёх знаков после запятой.");
            }

            var comment = Text(l.Comment, 500, $"«{card.Name}»: комментарий");
            var weighed = Weighed(card.UnitCode) && card.UnitCode != line.UnitCode;
            decimal stock;
            if (l.Quantity == 0)
            {
                stock = 0;
                if (comment is null)
                {
                    throw new FieldValidationException($"«{card.Name}»: ничего не принято — напишите почему (бой, недогруз, отказ).");
                }
            }
            else if (weighed)
            {
                if (l.Weight is not { } weight || weight <= 0)
                {
                    throw new FieldValidationException($"«{card.Name}»: укажите вес нетто, {card.UnitCode} — на склад идёт фактический вес.");
                }

                if (weight > ProcurementRules.MaxQuantity || decimal.Round(weight, 3) != weight)
                {
                    throw new FieldValidationException($"«{card.Name}»: вес — не больше трёх знаков после запятой.");
                }

                stock = weight;
            }
            else
            {
                stock = ProcurementUnitRules.ToBase(l.Quantity, factor)
                        ?? throw new FieldValidationException($"«{card.Name}»: {ProcurementRules.Quantity(l.Quantity)} {line.UnitCode} — больше трёх знаков в {card.UnitCode}.");
            }

            var sourceLine = ti?.Id ?? oi!.Id;
            taken[sourceLine] = taken.GetValueOrDefault(sourceLine) + l.Quantity;
            var left = ti is not null ? ti.Quantity - ti.ReceivedQty : ProcurementOrderRules.Remaining(oi!.Quantity, oi.ReceivedQty, oi.ClosedQty);
            if (taken[sourceLine] > left)
            {
                var by = ti is not null ? "по тендеру" : "по покупке";
                throw new FieldConflictException(left > 0
                    ? $"«{card.Name}»: {by} осталось поставить {ProcurementRules.Quantity(left)} {line.UnitCode} — больше не принять."
                    : $"«{card.Name}»: {by} поставлено всё.");
            }

            line.AcceptedQty = l.Quantity;
            line.AcceptedWeight = weighed && l.Quantity > 0 ? stock : null;
            line.StockQty = stock;
            line.Comment = comment;
            var places = weighed ? $" ({ProcurementRules.Quantity(l.Quantity)} {line.UnitCode})" : "";
            var diff = stock - line.DeclaredBase;
            if (diff != 0)
            {
                differences.Add($"{card.Name}: по накладной {ProcurementRules.Quantity(line.DeclaredBase)} {card.UnitCode}, принято {ProcurementRules.Quantity(stock)} — " +
                                $"{(diff < 0 ? "недостача" : "излишек")} {ProcurementRules.Quantity(Math.Abs(diff))} {card.UnitCode}{(comment is null ? "" : $" ({comment})")}");
            }

            if (stock == 0)
            {
                continue;
            }

            var cost = ProcurementUnitRules.CostToBase(ti?.Price ?? oi!.UnitPrice, factor);
            var requestItem = oi?.RequestItem;
            if (ledger.Tracks(card.Id))
            {
                if (ProcurementBatchRules.Check(label, new ProcurementBatchInput(line.SupplierBatchNo, line.ManufacturedOn, line.ExpiresOn, stock), line.ExpiresOn, ledger.Today)
                    is { } error)
                {
                    throw new FieldValidationException(error);
                }

                var batch = await ledger.NewBatchAsync(doc, store.Id, card.Id, line.SupplierBatchNo, line.ManufacturedOn, line.ExpiresOn, cost, arrival.SupplierId, ct);
                ledger.MoveBatch(doc, balances[card.Id], batch, ProcurementMovementKind.Receipt, stock, label, cost, requestItem?.Id);
                if (requestItem is not null)
                {
                    ledger.ReceivedForRequest(balances[card.Id], requestItem, stock, label, active);
                }

                line.BatchId = batch.BatchId;
                if (line.QualityCheckId is { } checkId)
                {
                    // Партия допущена проверкой до склада — ссылка на неё и в партии, и в проверке.
                    var check = await db.ProcurementQualityChecks.FirstAsync(c => c.Id == checkId, ct);
                    check.BatchId = batch.BatchId;
                    check.UpdatedAt = Now;
                    batch.Batch!.QualityDecidedAt = check.DecidedAt;
                    batch.Batch.QualityDecidedById = check.DecidedById;
                    db.ProcurementAuditLogs.Add(new ProcurementAuditLog
                    {
                        DocType = ProcurementQualityService.BatchDoc, DocId = batch.BatchId, ActorId = ctx.UserId, Action = "approved",
                        Text = $"Допущена по проверке {check.Number} до приёмки (поставка {arrival.Number})", FromStatus = ProcurementBatchQuality.Pending.ToString(),
                        ToStatus = ProcurementBatchQuality.Approved.ToString(),
                    });
                    db.ProcurementAuditLogs.Add(new ProcurementAuditLog
                    {
                        DocType = ProcurementQualityService.CheckDoc, DocId = check.Id, ActorId = ctx.UserId, Action = "received",
                        Text = $"Принято на склад «{store.Name}» ({doc.Number}): {ProcurementRules.Quantity(stock)} {card.UnitCode} — партия {batch.Batch.Number}",
                    });
                }
            }
            else if (requestItem is not null)
            {
                ledger.ReceiveForRequest(doc, balances[card.Id], requestItem, stock, cost, label, active);
            }
            else
            {
                ledger.Move(doc, balances[card.Id], ProcurementMovementKind.Receipt, stock, label, cost);
            }

            if (ti is not null)
            {
                ti.ReceivedQty += l.Quantity;
            }
            else
            {
                oi!.ReceivedQty += l.Quantity;
                var receiptLine = new ProcurementReceiptLine
                {
                    DocumentId = doc.Id, LineNo = ++receiptLineNo, OrderItemId = oi.Id, ItemId = card.Id, Received = stock, Accepted = stock,
                    Shortage = Math.Max(0, line.DeclaredBase - stock), Comment = comment,
                };
                doc.ReceiptLines.Add(receiptLine);
                db.ProcurementReceiptLines.Add(receiptLine);
            }

            total += Math.Round(stock * (cost ?? 0m), 2);
            received.Add($"{card.Name} — {ProcurementRules.Quantity(stock)} {card.UnitCode}{places}");
        }

        if (received.Count == 0)
        {
            throw new FieldValidationException("Ничего не принято — если машину не разгружают, отмените поставку с причиной.");
        }

        var returned = arrival.Lines.Where(l => l.Quality == ProcurementBatchQuality.Rejected).OrderBy(l => l.LineNo)
            .Select(l => $"{cards.GetValueOrDefault(l.ItemId)?.Name ?? $"строка {l.LineNo}"} — {ProcurementRules.Quantity(l.Declared)} {l.UnitCode}").ToList();
        string? sourceFrom = null, sourceTo = null;
        var all = false;
        if (tender is not null)
        {
            var was = tender.Status;
            if (tender.Items.All(i => i.ReceivedQty >= i.Quantity))
            {
                tender.Status = ProcurementTenderStatus.Delivered;
                tender.DeliveredOn = ledger.Today;
                all = true;
            }

            tender.UpdatedAt = Now;
            (sourceFrom, sourceTo) = was == tender.Status ? (null, null) : (was.ToString(), tender.Status.ToString());
        }
        else
        {
            // Покупка «Куплено»: получено всё — завершена (как покупка, принятая сразу), иначе — поставлена частично.
            var was = order!.Status;
            var status = ProcurementOrderRules.DeliveryStatus(order.Status, order.Items.Select(i => (i.Quantity, i.ReceivedQty, i.ClosedQty)));
            all = status == ProcurementOrderStatus.Delivered;
            order.Status = all ? ProcurementOrderStatus.Completed : status;
            if (all)
            {
                order.DeliveredAt = Now;
                order.CompletedAt = Now;
            }

            order.UpdatedAt = Now;
            (sourceFrom, sourceTo) = was == order.Status ? (null, null) : (was.ToString(), order.Status.ToString());
        }

        var from = arrival.Status;
        arrival.Status = ProcurementArrivalStatus.Accepted;
        arrival.AcceptedAt = Now;
        arrival.AcceptedById = ctx.UserId;
        arrival.ReceiptId = doc.Id;
        arrival.AcceptedTotal = total;
        arrival.UpdatedAt = Now;
        var text = $"Принято на склад «{store.Name}» ({doc.Number}): {string.Join("; ", received)} — на {ProcurementRules.Money(total)} сум" +
                   (differences.Count > 0 ? $". Расхождения: {string.Join("; ", differences)}" : "") +
                   (returned.Count > 0 ? $". Возврат поставщику: {string.Join("; ", returned)}" : "");
        Log(ctx.UserId, arrival.Id, "accepted", from, arrival.Status, text, doc.Comment == defaultComment ? null : doc.Comment);
        db.ProcurementAuditLogs.Add(new ProcurementAuditLog
        {
            DocType = SourceDoc(tender is not null ? TenderKind : OrderKind), DocId = tender?.Id ?? order!.Id, ActorId = ctx.UserId, Action = "received",
            FromStatus = sourceFrom, ToStatus = sourceTo,
            Text = Cut($"Принято на склад по поставке {arrival.Number} ({doc.Number}): {string.Join("; ", received)}" + (all ? " — поставлено всё" : ""), 1000),
            Comment = arrival.Reference,
        });
        if (request is not null)
        {
            db.ProcurementAuditLogs.Add(new ProcurementAuditLog
            {
                DocType = ProcurementRules.RequestDoc, DocId = request.Id, ActorId = ctx.UserId, Action = "received",
                Text = Cut($"Принято на склад по поставке {arrival.Number} ({doc.Number}): {string.Join("; ", received)} — можно получить на складе", 1000),
            });
        }

        await NotifyAcceptedAsync(ctx, arrival, tender, order, request, store.Name, doc.Number, received, differences, returned, total, ct);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "procurement.arrival.accepted", "procurement_arrival", arrival.Id.ToString(),
            new { arrival.Number, receipt = doc.Number, source, total }, ct);
        return doc.Id;
    });

    /// <summary>
    /// «Принято на склад» — снабжению (кто вёл тендер или покупку и все с правом обработки заявок), аналитикам, финансисту; автору заявки — «можно
    /// получить»; кроме принявшего.
    /// </summary>
    private async Task NotifyAcceptedAsync(ProcurementContext ctx, ProcurementArrival arrival, ProcurementTender? tender, ProcurementPurchaseOrder? order,
        ProcurementRequest? request, string store, string receipt, List<string> received, List<string> differences, List<string> returned, decimal total, CancellationToken ct)
    {
        var people = new List<Guid> { tender?.ResponsibleId ?? order!.ResponsibleId };
        people.AddRange(await ProcurementPeople.WithPermissionAsync(db, Permissions.ProcurementManage, ct));
        people.AddRange(await ProcurementPeople.AnalystsAsync(db, ct));
        people.AddRange(await ProcurementPeople.FinanceAsync(db, ct));
        var requestId = tender?.RequestId ?? request?.Id;
        var initiator = requestId is { } rid
            ? await db.ProcurementRequests.AsNoTracking().Where(r => r.Id == rid).Select(r => new { r.Id, r.Number, r.InitiatorId }).FirstOrDefaultAsync(ct)
            : null;
        var source = tender is not null ? $"тендер {tender.Number}" : $"покупка {order!.Number}";
        var title = $"Принято на склад: {arrival.Supplier!.Name} — {arrival.Number}";
        var body = Cut($"{string.Join("; ", received)}. На {ProcurementRules.Money(total)} сум, склад «{store}», приход {receipt}, {source}" +
                       (differences.Count > 0 ? $". Расхождения: {string.Join("; ", differences)}" : "") +
                       (returned.Count > 0 ? $". Возврат поставщику: {string.Join("; ", returned)}" : ""), 500);
        foreach (var user in people.Distinct().Where(u => u != ctx.UserId && u != initiator?.InitiatorId))
        {
            notifier.Notify(user, UserNotificationKind.ProcurementStatus, title, body, $"/procurement/arrivals/{arrival.Id}");
        }

        if (initiator is not null && initiator.InitiatorId != ctx.UserId)
        {
            notifier.Notify(initiator.InitiatorId, UserNotificationKind.ProcurementStatus, $"Принято на склад по заявке {initiator.Number} — можно получить", body,
                $"/requests/{initiator.Id}");
        }
    }

    // ---------- Отмена ----------

    /// <summary>
    /// Отменить поставку до приёмки (отметили по ошибке, машина уехала): кладовщик склада или снабжение, причина обязательна. Открытые проверки
    /// по ней отменяются; склад не менялся. Принятую не отменить — она уже на складе.
    /// </summary>
    public async Task CancelAsync(ProcurementContext ctx, Guid id, ProcurementArrivalCancelInput input, CancellationToken ct)
    {
        var arrival = await db.ProcurementArrivals.Include(a => a.Lines).Include(a => a.Tender).Include(a => a.Order).FirstOrDefaultAsync(a => a.Id == id, ct)
                      ?? throw new FieldNotFoundException("Поставка не найдена.");
        if (!ctx.Posts(arrival.WarehouseId) && !ctx.Manages)
        {
            throw new FieldForbiddenException("Отменить поставку может кладовщик склада или снабжение.");
        }

        if (arrival.Status == ProcurementArrivalStatus.Cancelled)
        {
            return;
        }

        if (!Open.Contains(arrival.Status))
        {
            throw new FieldConflictException(arrival.Status == ProcurementArrivalStatus.Accepted
                ? $"Поставка {arrival.Number} уже принята на склад — её не отменить."
                : $"Поставка {arrival.Number} закрыта (возврат поставщику) — её не отменить.");
        }

        if (input.Version is { } version && version != arrival.Version)
        {
            throw new FieldConflictException($"Поставку {arrival.Number} уже изменили — обновите страницу.");
        }

        var reason = Text(input.Reason, 1000, "Причина") ?? throw new FieldValidationException("Укажите причину отмены.");
        var open = ProcurementQualityRules.Open;
        var checkIds = arrival.Lines.Select(l => l.QualityCheckId).OfType<Guid>().ToList();
        var checks = await db.ProcurementQualityChecks.Where(c => checkIds.Contains(c.Id) && open.Contains(c.Status)).ToListAsync(ct);
        foreach (var check in checks)
        {
            var was = check.Status;
            check.Status = ProcurementQualityCheckStatus.Cancelled;
            check.DecisionComment = $"Поставка {arrival.Number} отменена: {reason}";
            check.UpdatedAt = Now;
            db.ProcurementAuditLogs.Add(new ProcurementAuditLog
            {
                DocType = ProcurementQualityService.CheckDoc, DocId = check.Id, ActorId = ctx.UserId, Action = "cancelled", Text = "Проверка отменена — поставка отменена",
                Comment = reason, FromStatus = was.ToString(), ToStatus = check.Status.ToString(),
            });
        }

        var from = arrival.Status;
        arrival.Status = ProcurementArrivalStatus.Cancelled;
        arrival.CancelledAt = Now;
        arrival.CancelReason = reason;
        arrival.UpdatedAt = Now;
        Log(ctx.UserId, arrival.Id, "cancelled", from, arrival.Status, "Поставка отменена", reason);
        db.ProcurementAuditLogs.Add(new ProcurementAuditLog
        {
            DocType = SourceDoc(arrival.TenderId is not null ? TenderKind : OrderKind), DocId = arrival.TenderId ?? arrival.OrderId!.Value, ActorId = ctx.UserId,
            Action = "arrival_cancelled", Text = $"Поставка {arrival.Number} отменена", Comment = reason,
        });
        var people = new List<Guid> { arrival.Tender?.ResponsibleId ?? arrival.Order!.ResponsibleId };
        if (checks.Count > 0)
        {
            people.AddRange(await ProcurementPeople.WithPermissionAsync(db, Permissions.QualityCheck, ct));
        }

        foreach (var user in people.Distinct().Where(u => u != ctx.UserId))
        {
            notifier.Notify(user, UserNotificationKind.ProcurementStatus, $"Поставка {arrival.Number} отменена", Cut(reason, 500), $"/procurement/arrivals/{arrival.Id}");
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Машины по заказу на проверке или к приёмке — заказ нельзя отменить или закрыть, пока их не примут или не отменят.</summary>
    public static Task<bool> HasOpenAsync(IAppDbContext db, Guid orderId, CancellationToken ct) =>
        db.ProcurementArrivals.AsNoTracking().AnyAsync(a => a.OrderId == orderId && Open.Contains(a.Status), ct);

    private void Log(Guid actor, Guid id, string action, ProcurementArrivalStatus? from, ProcurementArrivalStatus? to, string text, string? comment) =>
        db.ProcurementAuditLogs.Add(new ProcurementAuditLog
        {
            DocType = Doc, DocId = id, ActorId = actor, Action = action, FromStatus = from == to ? null : from?.ToString(), ToStatus = from == to ? null : to?.ToString(),
            Text = Cut(text, 1000), Comment = comment,
        });

    internal static string Cut(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";

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

/// <summary>Решение по проверке поставки до склада: строка поставки получает решение; решено по всем — «готово к приёмке» или «возврат».</summary>
internal static class ProcurementArrivalEffects
{
    /// <summary>
    /// Проверка поставки решена (вызывается из решения по качеству, сохраняет вызывающий): решение — в строку; ещё ждут — только отметка
    /// изменения (версия: два одновременных решения по одной поставке не оставят её «на проверке»). Решено по всем: есть допущенные — «готово к
    /// приёмке», кладовщикам — «можно принимать»; забраковано всё — «возврат». Забракованное — снабжению: вернуть поставщику.
    /// </summary>
    public static async Task CheckDecidedAsync(IAppDbContext db, UserNotifier notifier, ProcurementQualityCheck check, Guid actor, CancellationToken ct)
    {
        if (check.ArrivalId is not { } arrivalId)
        {
            return;
        }

        var arrival = await db.ProcurementArrivals.Include(a => a.Lines).ThenInclude(l => l.Item).Include(a => a.Supplier).Include(a => a.Tender).Include(a => a.Order)
            .FirstOrDefaultAsync(a => a.Id == arrivalId, ct);
        var line = arrival?.Lines.FirstOrDefault(l => l.QualityCheckId == check.Id);
        if (arrival is null || line is null || arrival.Status != ProcurementArrivalStatus.Quality)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        line.Quality = check.Decision ?? ProcurementBatchQuality.Pending;
        arrival.UpdatedAt = now;
        if (arrival.Lines.Any(l => l.Quality == ProcurementBatchQuality.Pending))
        {
            return;
        }

        string Describe(ProcurementArrivalLine l) =>
            $"{l.Item!.Name} — {ProcurementRules.Quantity(l.Declared)} {l.UnitCode}{(l.SupplierBatchNo is { } lot ? $", партия {lot}" : "")}";
        var rejected = arrival.Lines.Where(l => l.Quality == ProcurementBatchQuality.Rejected).OrderBy(l => l.LineNo).Select(Describe).ToList();
        var approved = arrival.Lines.Where(l => l.Quality == ProcurementBatchQuality.Approved).OrderBy(l => l.LineNo).Select(Describe).ToList();
        var from = arrival.Status;
        arrival.Status = approved.Count > 0 ? ProcurementArrivalStatus.Ready : ProcurementArrivalStatus.Returned;
        arrival.ReadyAt = now;
        var text = arrival.Status == ProcurementArrivalStatus.Ready
            ? $"Качество проверено — можно принимать: {string.Join("; ", approved)}{(rejected.Count > 0 ? $". Вернуть поставщику: {string.Join("; ", rejected)}" : "")}"
            : $"Забраковано всё — вернуть поставщику: {string.Join("; ", rejected)}";
        db.ProcurementAuditLogs.Add(new ProcurementAuditLog
        {
            DocType = ProcurementArrivalService.Doc, DocId = arrival.Id, ActorId = actor, Action = arrival.Status == ProcurementArrivalStatus.Ready ? "ready" : "returned",
            FromStatus = from.ToString(), ToStatus = arrival.Status.ToString(), Text = ProcurementArrivalService.Cut(text, 1000),
        });

        var keepers = await db.ProcurementWarehouseKeepers.AsNoTracking().Where(k => k.WarehouseId == arrival.WarehouseId).Select(k => k.UserId).ToListAsync(ct);
        if (keepers.Count == 0)
        {
            keepers = await ProcurementPeople.WithPermissionAsync(db, Permissions.ProcurementWarehouse, ct);
        }

        var link = $"/procurement/arrivals/{arrival.Id}";
        var vehicle = arrival.VehicleNo is { } v ? $" ({v})" : "";
        foreach (var user in keepers.Distinct().Where(u => u != actor))
        {
            if (arrival.Status == ProcurementArrivalStatus.Ready)
            {
                notifier.Notify(user, UserNotificationKind.ProcurementRequest, $"Можно принимать: {arrival.Supplier!.Name} — {arrival.Number}{vehicle}",
                    ProcurementArrivalService.Cut($"Качество проверено. Взвесьте и примите: {string.Join("; ", approved)}" +
                                                  (rejected.Count > 0 ? $". Вернуть поставщику: {string.Join("; ", rejected)}" : ""), 500), link);
            }
            else
            {
                notifier.Notify(user, UserNotificationKind.ProcurementStatus, $"Не разгружать — вернуть поставщику: {arrival.Number}{vehicle}",
                    ProcurementArrivalService.Cut($"Забраковано лабораторией: {string.Join("; ", rejected)}", 500), link);
            }
        }

        if (rejected.Count > 0)
        {
            var supply = new List<Guid> { arrival.Tender?.ResponsibleId ?? arrival.Order!.ResponsibleId };
            supply.AddRange(await ProcurementPeople.WithPermissionAsync(db, Permissions.ProcurementManage, ct));
            var source = arrival.Tender is { } t ? $"Тендер {t.Number}" : $"Покупка {arrival.Order!.Number}";
            foreach (var user in supply.Distinct().Where(u => u != actor && !keepers.Contains(u)))
            {
                notifier.Notify(user, UserNotificationKind.ProcurementStatus, $"Забраковано при приёмке: {arrival.Supplier!.Name} — {arrival.Number}",
                    ProcurementArrivalService.Cut($"Вернуть поставщику: {string.Join("; ", rejected)}. {source}", 500), link);
            }
        }
    }
}
