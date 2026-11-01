using OneBase.Domain.Common;
using OneBase.Domain.Identity;

namespace OneBase.Domain.Procurement;

/// <summary>
/// Позиция каталога склада (товар, материал, расходник): SKU, название, категория, единица, штрихкод, минимальный остаток. Не удаляется —
/// отключается (по ней остаются движения и заявки). Готовая продукция Linko сюда не входит: это свой склад снабжения.
/// </summary>
public class ProcurementItem : Entity
{
    /// <summary>Артикул — уникален.</summary>
    public required string Sku { get; set; }

    public required string Name { get; set; }

    public Guid CategoryId { get; set; }
    public ProcurementCategory? Category { get; set; }

    public required string UnitCode { get; set; }

    public string? Barcode { get; set; }
    public string? Description { get; set; }

    /// <summary>Минимальный остаток: ниже — «нужно пополнить».</summary>
    public decimal MinQty { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Когда ушло уведомление «ниже минимума»; снимается, когда доступно снова не ниже минимума.</summary>
    public DateTimeOffset? LowStockNotifiedAt { get; set; }
}

/// <summary>Склад. Пока один — «Центральный склад» (засевается при запуске); несколько складов — позже.</summary>
public class ProcurementWarehouse : Entity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public bool IsDefault { get; set; }
}

/// <summary>
/// Остаток позиции на складе: на складе (OnHand) и в резерве (Reserved); доступно = OnHand − Reserved. Меняется только вместе со строкой
/// журнала движений (или резерва) в одной записи; Version — xmin: два одновременных изменения не перезаписывают друг друга, поэтому
/// отрицательного остатка и двойного резерва не бывает.
/// </summary>
public class ProcurementStockBalance
{
    public Guid WarehouseId { get; set; }
    public Guid ItemId { get; set; }
    public ProcurementItem? Item { get; set; }

    public decimal OnHand { get; set; }
    public decimal Reserved { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public uint Version { get; set; }
}

/// <summary>Вид складского движения.</summary>
public enum ProcurementMovementKind
{
    /// <summary>Приход на склад.</summary>
    Receipt,

    /// <summary>Выдача отделу (расход).</summary>
    Issue,

    /// <summary>Списание (брак, порча, утеря) — с причиной.</summary>
    WriteOff,

    /// <summary>Корректировка по инвентаризации (± разница учёта и факта).</summary>
    Adjustment,
}

/// <summary>Состояние складского документа: приёмка по заказу сначала черновик (остатки не меняются) и проводится подтверждением.</summary>
public enum ProcurementStockDocumentStatus
{
    /// <summary>Черновик приёмки — склад не изменился.</summary>
    Draft,

    /// <summary>Проведён: движения в журнале, остатки изменены; не правится и не удаляется.</summary>
    Posted,
}

/// <summary>Вид складского документа.</summary>
public enum ProcurementStockDocumentKind
{
    Receipt,
    Issue,
    WriteOff,
    Inventory,
}

/// <summary>
/// Складской документ: приход, выдача отделу, списание, инвентаризация. Номер — RC-, ISS-, WO-, INV-год-NNNNN. Строки — движения журнала.
/// ClientKey — ключ повтора формы: повторное нажатие не проводит документ второй раз. Приход по заказу поставщику (OrderId) — приёмка:
/// черновик с фактическим количеством, браком и недостачей (ReceiptLines); склад меняется только при подтверждении. Version — xmin:
/// двойное подтверждение одной приёмки не проводит её дважды.
/// </summary>
public class ProcurementStockDocument : Entity
{
    public required string Number { get; set; }
    public ProcurementStockDocumentKind Kind { get; set; }
    public ProcurementStockDocumentStatus Status { get; set; } = ProcurementStockDocumentStatus.Posted;

    /// <summary>Когда проведён (приёмка — подтверждена кладовщиком).</summary>
    public DateTimeOffset? PostedAt { get; set; }

    /// <summary>Заказ поставщику, по которому принят товар (приёмка).</summary>
    public Guid? OrderId { get; set; }
    public ProcurementPurchaseOrder? Order { get; set; }

    public Guid WarehouseId { get; set; }

    /// <summary>Отдел-получатель (выдача).</summary>
    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }

    /// <summary>Заявка, по которой выдано.</summary>
    public Guid? RequestId { get; set; }

    /// <summary>Кто получил товар (выдача).</summary>
    public string? Receiver { get; set; }

    /// <summary>Номер внешнего документа (накладная поставщика, акт).</summary>
    public string? Reference { get; set; }

    /// <summary>Комментарий; для списания — причина (обязательна).</summary>
    public string? Comment { get; set; }

    public Guid? ActorId { get; set; }
    public User? Actor { get; set; }

    public Guid? ClientKey { get; set; }

    public uint Version { get; set; }

    /// <summary>Строки приёмки по заказу (у прочих документов — пусто).</summary>
    public ICollection<ProcurementReceiptLine> ReceiptLines { get; set; } = [];
}

/// <summary>Претензия поставщику по строке приёмки (брак или недостача): открыта — ждёт решения снабжения, закрыта — с решением.</summary>
public enum ProcurementClaimStatus
{
    None,
    Open,
    Resolved,
}

/// <summary>
/// Строка приёмки по заказу: позиция заказа, карточка склада, фактически пришло, из них брак, недостача против накладной. Принято на склад =
/// пришло − брак, не больше незакрытого остатка заказа. Брак и недостача — претензия поставщику (входит в его рейтинг); непринятое
/// остаётся в заказе к поставке.
/// </summary>
public class ProcurementReceiptLine
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid DocumentId { get; set; }
    public ProcurementStockDocument? Document { get; set; }

    public int LineNo { get; set; }

    public Guid OrderItemId { get; set; }
    public ProcurementPurchaseOrderItem? OrderItem { get; set; }

    /// <summary>Карточка склада, на которую приходуется (позиции заявки без карточки её выбирают при приёмке).</summary>
    public Guid? ItemId { get; set; }
    public ProcurementItem? Item { get; set; }

    /// <summary>Фактически пришло (вместе с браком).</summary>
    public decimal Received { get; set; }

    /// <summary>Брак — из пришедшего; на склад не идёт.</summary>
    public decimal Defective { get; set; }

    /// <summary>Недостача: по накладной поставщика больше, чем пришло.</summary>
    public decimal Shortage { get; set; }

    /// <summary>Принято на склад = пришло − брак (записывается при подтверждении).</summary>
    public decimal Accepted { get; set; }

    public string? Comment { get; set; }

    public ProcurementClaimStatus ClaimStatus { get; set; }

    /// <summary>Решение по претензии (замена, возврат денег, уценка…).</summary>
    public string? ClaimResolution { get; set; }

    public DateTimeOffset? ClaimResolvedAt { get; set; }

    public Guid? ClaimResolvedById { get; set; }
}

/// <summary>
/// Строка журнала движений: позиция, количество (+ приход, − расход), остаток до и после, цена за единицу (приход), документ, заявка.
/// Журнал не правится и не удаляется — исправление делается новым документом.
/// </summary>
public class ProcurementStockMovement : Entity
{
    public Guid DocumentId { get; set; }
    public ProcurementStockDocument? Document { get; set; }

    public Guid WarehouseId { get; set; }

    public Guid ItemId { get; set; }
    public ProcurementItem? Item { get; set; }

    public ProcurementMovementKind Kind { get; set; }

    /// <summary>Изменение остатка: приход — плюс, выдача и списание — минус, инвентаризация — разница (бывает 0).</summary>
    public decimal Quantity { get; set; }

    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }

    public decimal? UnitCost { get; set; }

    /// <summary>Позиция заявки, по которой выдано.</summary>
    public Guid? RequestItemId { get; set; }

    public Guid? ActorId { get; set; }
}

public enum ProcurementReservationStatus
{
    Active,
    Issued,
    Released,
}

/// <summary>
/// Резерв товара под позицию заявки: создаётся при отправке заявки (часть «со склада»), закрывается выдачей или снимается (отмена,
/// отклонение, возврат на доработку). Issued — сколько из резерва уже выдано.
/// </summary>
public class ProcurementStockReservation : Entity
{
    public Guid WarehouseId { get; set; }
    public Guid ItemId { get; set; }

    public Guid RequestId { get; set; }
    public Guid RequestItemId { get; set; }

    public decimal Quantity { get; set; }
    public decimal Issued { get; set; }

    public ProcurementReservationStatus Status { get; set; } = ProcurementReservationStatus.Active;
    public DateTimeOffset? ClosedAt { get; set; }
}

public enum ProcurementApprovalDecision
{
    Approved,
    Rejected,
    Revision,
}

/// <summary>
/// Решение директора по заявке: раунд согласования (повторное — следующий раунд), решение, комментарий (обязателен при отказе и возврате),
/// кто и когда, снимок заявки на момент решения (позиции, количества, цены, сумма закупки).
/// </summary>
public class ProcurementApproval : Entity
{
    public Guid RequestId { get; set; }
    public int Round { get; set; }
    public ProcurementApprovalDecision Decision { get; set; }
    public string? Comment { get; set; }

    public Guid DeciderId { get; set; }
    public User? Decider { get; set; }

    /// <summary>Сумма к закупке на момент решения (UZS).</summary>
    public decimal PurchaseTotal { get; set; }

    /// <summary>Снимок позиций (JSON).</summary>
    public required string Snapshot { get; set; }
}

/// <summary>
/// Настройки снабжения (одна строка, Id = 1): допуск роста цены или суммы без повторного согласования, %; через сколько дней просрочки
/// задача снабжения эскалируется руководителю снабжения и директору.
/// </summary>
public class ProcurementSettings
{
    public int Id { get; set; } = 1;

    public decimal ReapprovalTolerancePercent { get; set; }

    /// <summary>Просрочка задачи снабжения (дней), после которой — руководителю снабжения.</summary>
    public int EscalateHeadDays { get; set; } = 1;

    /// <summary>Просрочка задачи снабжения (дней), после которой — директору.</summary>
    public int EscalateDirectorDays { get; set; } = 3;

    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedById { get; set; }
}
