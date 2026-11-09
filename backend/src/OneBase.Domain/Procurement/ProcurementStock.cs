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

    /// <summary>
    /// Учёт по партиям (сырьё): остаток — по партиям со сроком годности, выдаётся только непросроченное (FEFO). Включается сразу у позиции
    /// без остатка, с остатком — вводом партий на весь остаток.
    /// </summary>
    public bool TracksBatches { get; set; }

    /// <summary>Срок хранения, дней: срок годности партии по дате производства, когда срок не указан.</summary>
    public int? ShelfLifeDays { get; set; }

    /// <summary>За сколько дней до срока годности напомнить (null — 30).</summary>
    public int? ExpiryWarningDays { get; set; }

    /// <summary>Условия хранения: «сухо, до +25 °C».</summary>
    public string? StorageConditions { get; set; }

    /// <summary>Единицы закупки с коэффициентом (мешок = 50 кг): приход в них, остаток и партии — в единице карточки.</summary>
    public ICollection<ProcurementItemUnit> Units { get; set; } = [];

    /// <summary>
    /// Контроль качества при приходе (только у позиции с партиями): нет — партия сразу допущена; документы или документы и лаборатория —
    /// партия «на контроле» до решения назначенного (спецификация — ProcurementQualitySpec).
    /// </summary>
    public ProcurementQualityControl QualityControl { get; set; }
}

/// <summary>
/// Единица закупки позиции (D10): сколько единиц карточки в одной — «мешок = 50 кг». Приход по тендеру и на складе можно вести в ней:
/// количество и цена пересчитываются в единицу карточки (остаток, партии и рецептуры — только в ней).
/// </summary>
public class ProcurementItemUnit
{
    public Guid ItemId { get; set; }
    public ProcurementItem? Item { get; set; }
    public required string UnitCode { get; set; }

    /// <summary>Единиц карточки в одной единице закупки (больше нуля, до шести знаков).</summary>
    public decimal Factor { get; set; }
}

/// <summary>
/// Партия: свой номер LOT-год-NNNNN, номер партии производителя, поставщик, даты производства и годности, цена прихода за единицу.
/// Партия одна на все склады: перемещение двигает ту же партию, поэтому номер, срок и история сохраняются. Появляется только приходом
/// или вводом партий; не удаляется. Отметки ExpiryWarnedFor и ExpiredNotifiedOn — напоминания о сроке ушли (один раз на срок).
/// QualityStatus — допущена, на контроле, забракована, заблокирована: выдаётся только допущенная и не просроченная (ProcurementBatchRules.Usable).
/// </summary>
public class ProcurementBatch : Entity
{
    public required string Number { get; set; }

    public Guid ItemId { get; set; }
    public ProcurementItem? Item { get; set; }

    public string? SupplierBatchNo { get; set; }

    public Guid? SupplierId { get; set; }
    public ProcurementSupplier? Supplier { get; set; }

    public DateOnly? ManufacturedOn { get; set; }

    /// <summary>
    /// Годен по этот день включительно (Ташкент): со следующего дня партия не выдаётся. null — «срок не указан» (только при вводе остатков):
    /// партия заблокирована, пока назначенный по качеству не укажет срок.
    /// </summary>
    public DateOnly? ExpiresOn { get; set; }

    public DateOnly ReceivedOn { get; set; }

    /// <summary>Документ, которым партия появилась (приход, ввод партий, загрузка журнала).</summary>
    public Guid SourceDocumentId { get; set; }

    public decimal? UnitCost { get; set; }

    public DateOnly? ExpiryWarnedFor { get; set; }
    public DateOnly? ExpiredNotifiedOn { get; set; }

    public ProcurementBatchQuality QualityStatus { get; set; } = ProcurementBatchQuality.Approved;

    /// <summary>Последнее решение по качеству: допуск, брак, блокировка, снятие блокировки — кто и когда.</summary>
    public DateTimeOffset? QualityDecidedAt { get; set; }

    public Guid? QualityDecidedById { get; set; }

    /// <summary>Почему заблокирована (у Blocked).</summary>
    public string? BlockReason { get; set; }

    public uint Version { get; set; }
}

/// <summary>
/// Остаток партии на складе. У позиции с партиями остаток позиции на складе (ProcurementStockBalance) = сумма остатков её партий —
/// держит проводка (ProcurementStockLedger), проверяют тесты. Резерв заявок — у позиции, не у партии. Version — xmin.
/// </summary>
public class ProcurementBatchBalance
{
    public Guid WarehouseId { get; set; }
    public Guid BatchId { get; set; }
    public ProcurementBatch? Batch { get; set; }
    public Guid ItemId { get; set; }

    public decimal OnHand { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public uint Version { get; set; }
}

/// <summary>Вид склада: центральный, склад сырья, кладовая цеха, другой.</summary>
public enum ProcurementWarehouseKind
{
    Central,
    RawMaterials,
    Workshop,
    Other,
}

/// <summary>
/// Склад снабжения. «Центральный склад» засевается при запуске и остаётся складом по умолчанию: на нём резервируют заявки, на него приходуют
/// приёмка и «Куплено». Остальные склады заводятся в «Настройки → Отделы → Снабжение». Не удаляется — отключается (только без остатка).
/// Кладовщики склада (Keepers): закреплённый за складами проводит операции только по ним; без закреплений — по всем складам. Version — xmin.
/// </summary>
public class ProcurementWarehouse : Entity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public bool IsDefault { get; set; }

    public ProcurementWarehouseKind Kind { get; set; } = ProcurementWarehouseKind.Central;

    /// <summary>Отдел — у кладовой цеха (кому она принадлежит).</summary>
    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Кто ведёт данные склада в переходный период (D13): не выбран — как раньше (и ручные операции, и загрузка журнала); OneBase — загрузка
    /// журнала закрыта; таблица — ручные операции закрыты (кроме контроля качества), остатки ведёт загрузка журнала. DataOwnerSince — с какой даты.
    /// </summary>
    public ProcurementDataOwner DataOwner { get; set; } = ProcurementDataOwner.None;

    public DateOnly? DataOwnerSince { get; set; }

    public uint Version { get; set; }

    public ICollection<ProcurementWarehouseKeeper> Keepers { get; set; } = [];
}

/// <summary>Владелец данных склада: не выбран (как раньше), OneBase, таблица (Excel / Google Sheets).</summary>
public enum ProcurementDataOwner
{
    None,
    OneBase,
    Sheet,
}

/// <summary>Кладовщик, закреплённый за складом.</summary>
public class ProcurementWarehouseKeeper
{
    public Guid WarehouseId { get; set; }
    public ProcurementWarehouse? Warehouse { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
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

    /// <summary>Перемещение: ушло со склада-отправителя (минус).</summary>
    TransferOut,

    /// <summary>Перемещение: пришло на склад-получатель (плюс).</summary>
    TransferIn,

    /// <summary>Ввод партий: остаток позиции не меняется (0), партия получает свою часть (BatchAfter).</summary>
    BatchEntry,

    /// <summary>Сторно: зеркально строке исправленного документа.</summary>
    Reversal,

    /// <summary>Возврат поставщику забракованной или заблокированной партии (минус).</summary>
    SupplierReturn,

    /// <summary>Выдача в производство по заданию на выпуск (минус): только годные партии.</summary>
    ProductionIssue,

    /// <summary>Возврат из производства по заданию (плюс): в ту же партию, не больше выданного.</summary>
    ProductionReturn,

    /// <summary>Приход выхода производства по заданию (плюс): новая партия полуфабриката или продукции.</summary>
    ProductionOutput,
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

    /// <summary>Перемещение между складами: со склада документа (WarehouseId) на TargetWarehouseId, обе стороны — одной записью.</summary>
    Transfer,

    /// <summary>Ввод партий: имеющийся остаток позиции разложен по партиям со сроком — позиция начинает вестись по партиям.</summary>
    BatchEntry,

    /// <summary>Сторно: зеркальные движения документа ReversesDocumentId; исправленный документ не меняется.</summary>
    Reversal,

    /// <summary>Возврат поставщику забракованной или заблокированной партии (SRT-); не сторнируется.</summary>
    SupplierReturn,

    /// <summary>Выдача в производство по заданию на выпуск (PIS-); не сторнируется — исправляет возврат из производства.</summary>
    ProductionIssue,

    /// <summary>Возврат из производства по заданию (PRT-); не сторнируется.</summary>
    ProductionReturn,

    /// <summary>Приход выхода производства (полуфабрикат или готовая продукция с позицией склада) по закрытому заданию (PRC-); не сторнируется.</summary>
    ProductionOutput,
}

/// <summary>
/// Складской документ: приход, выдача отделу, списание, инвентаризация, перемещение. Номер — RC-, ISS-, WO-, INV-, TRF-год-NNNNN. Строки — движения журнала.
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

    /// <summary>Склад-получатель (перемещение); у остальных документов — null.</summary>
    public Guid? TargetWarehouseId { get; set; }

    /// <summary>Сторно: какой документ исправляет (уникально — документ сторнируется один раз).</summary>
    public Guid? ReversesDocumentId { get; set; }

    /// <summary>Поставщик прихода без заказа (переходит в партии); у возврата поставщику — кому вернули.</summary>
    public Guid? SupplierId { get; set; }

    /// <summary>Списание проб лаборатории — по какой проверке качества (такое списание не сторнируется).</summary>
    public Guid? QualityCheckId { get; set; }

    /// <summary>Поставка по тендеру (приход): какой тендерной закупки — не сторнируется (поставленное уже в тендере).</summary>
    public Guid? TenderId { get; set; }

    /// <summary>Выдача в производство и возврат из него: по какому заданию на выпуск (не сторнируются — выданное считается в задании).</summary>
    public Guid? ProductionOrderId { get; set; }

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

    /// <summary>Партия (у позиции с партиями — у каждой строки) и её остаток на складе до и после.</summary>
    public Guid? BatchId { get; set; }
    public ProcurementBatch? Batch { get; set; }
    public decimal? BatchBefore { get; set; }
    public decimal? BatchAfter { get; set; }
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

/// <summary>Шаг маршрута согласования заявки: руководитель отдела, финансист, директор; в маршруте сырья — руководитель снабжения.</summary>
public static class ProcurementApprovalStages
{
    public const string Head = "head";
    public const string Finance = "finance";
    public const string Director = "director";
    public const string SupplyHead = "supply_head";
    public const string Analyst = "analyst";
}

/// <summary>
/// Решение согласующего по заявке: раунд согласования (повторная отправка — следующий раунд), шаг маршрута (руководитель отдела, финансист,
/// директор), решение, комментарий (обязателен при отказе и возврате), кто и когда, снимок заявки на момент решения (позиции, количества,
/// цены, сумма закупки).
/// </summary>
public class ProcurementApproval : Entity
{
    public Guid RequestId { get; set; }
    public int Round { get; set; }

    /// <summary>Шаг маршрута (ProcurementApprovalStages); решения до нового маршрута — директора.</summary>
    public string Stage { get; set; } = ProcurementApprovalStages.Director;

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
/// Настройки снабжения (одна строка, Id = 1, «Настройки → Отделы → Снабжение»): маршрут согласования заявок (финансист, директор, лимит без
/// директора), допуск роста цены или суммы без повторного согласования, %; через сколько дней просрочки задача снабжения эскалируется
/// руководителю снабжения и директору.
/// </summary>
public class ProcurementSettings
{
    /// <summary>Лимит покупки без директора по умолчанию: 5 млн сум.</summary>
    public const decimal DefaultDirectorLimit = 5_000_000m;

    public int Id { get; set; } = 1;

    public decimal ReapprovalTolerancePercent { get; set; }

    /// <summary>Просрочка задачи снабжения (дней), после которой — руководителю снабжения.</summary>
    public int EscalateHeadDays { get; set; } = 1;

    /// <summary>Просрочка задачи снабжения (дней), после которой — директору.</summary>
    public int EscalateDirectorDays { get; set; } = 3;

    /// <summary>Финансист: согласует заявку после руководителя отдела (заявку руководителя — первым).</summary>
    public Guid? FinanceApproverId { get; set; }

    /// <summary>Директор: согласует заявку после финансиста, если её сумма больше лимита.</summary>
    public Guid? DirectorApproverId { get; set; }

    /// <summary>Лимит покупки без директора, сум: заявка на большую сумму после финансиста идёт к директору.</summary>
    public decimal DirectorLimit { get; set; } = DefaultDirectorLimit;

    /// <summary>
    /// Маршрут сырья включён: заявка с закупкой позиции категории сырья идёт руководителю снабжения, затем директору (всегда). Выключен —
    /// все заявки идут обычным маршрутом (так было до этапа закупки сырья).
    /// </summary>
    public bool RawRouteEnabled { get; set; }

    /// <summary>Руководитель снабжения в маршруте сырья; не выбран — руководители отдела «Снабжение».</summary>
    public Guid? SupplyApproverId { get; set; }

    /// <summary>
    /// Маршрут заявок «Закупка сырья» — шаги по автору заявки (JSON: {"supply":["analyst","finance"],"analyst":["finance"],"finance":["analyst"]}).
    /// null — маршрут по умолчанию (ProcurementRawRoute.Default). Флажок RawRouteEnabled и SupplyApproverId — прежний маршрут, для заявок,
    /// отправленных до типов заявок.
    /// </summary>
    public string? RawRouteSteps { get; set; }

    /// <summary>Перерасход сырья по заданию на выпуск, %, выше которого строка помечается и руководитель производства получает уведомление.</summary>
    public decimal OverconsumptionPercent { get; set; } = DefaultOverconsumptionPercent;

    public const decimal DefaultOverconsumptionPercent = 5m;

    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedById { get; set; }
}

/// <summary>
/// Загрузка журнала склада из таблицы Google (переходный период, этап 7): подключение из «Источников данных», лист, склад, расписание
/// (manual — кнопкой, interval — каждые IntervalMinutes, daily — в DailyAt по Ташкенту). Разбор — тот же, что у файла: те же проверки и отчёт,
/// повторная загрузка не проводит строки второй раз. OwnerId — кладовщик, от имени которого проводится загрузка по расписанию.
/// </summary>
public class ProcurementSheetImport : Entity
{
    public Guid WarehouseId { get; set; }
    public ProcurementWarehouse? Warehouse { get; set; }
    public Guid ConnectionId { get; set; }
    public required string Sheet { get; set; }
    public string Schedule { get; set; } = "manual";
    public int? IntervalMinutes { get; set; }
    public string? DailyAt { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTimeOffset? NextRunAt { get; set; }
    public DateTimeOffset? LastRunAt { get; set; }
    public string? LastStatus { get; set; }
    public string? LastMessage { get; set; }
    public Guid OwnerId { get; set; }
    public User? Owner { get; set; }
    public uint Version { get; set; }
}
