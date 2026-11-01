using OneBase.Domain.Common;
using OneBase.Domain.Identity;

namespace OneBase.Domain.Procurement;

/// <summary>
/// Статус заявки на закупку. Хранится строкой — новые значения добавляются без миграции данных.
/// Draft → Submitted → DirectorApproval → Approved → PartiallyFulfilled → Fulfilled; RevisionRequired — возвращена на доработку (снова
/// отправляется после правки), Rejected — отклонена директором, Cancelled — отменена инициатором.
/// </summary>
public enum ProcurementRequestStatus
{
    Draft,
    Submitted,
    DirectorApproval,
    Approved,
    Rejected,
    RevisionRequired,
    PartiallyFulfilled,
    Fulfilled,
    Cancelled,
}

/// <summary>Приоритет заявки: низкий, обычный, высокий, срочный.</summary>
public enum ProcurementPriority
{
    Low,
    Normal,
    High,
    Urgent,
}

/// <summary>
/// Статус исполнения позиции заявки (у каждой позиции свой): ожидает, зарезервирована на складе (вся позиция — со склада), к закупке
/// (есть часть к закупке), выдана частично, выдана, отменена. Новые значения появятся с заказами поставщикам и приёмкой.
/// </summary>
public enum ProcurementItemStatus
{
    Pending,
    Cancelled,
    Reserved,
    ToPurchase,
    PartiallyIssued,
    Issued,

    /// <summary>Часть «к закупке» заказана у поставщиков целиком.</summary>
    Ordered,

    /// <summary>Закупленное поступило на склад целиком и ждёт выдачи.</summary>
    Received,
}

/// <summary>
/// Заявка на закупку (раздел «Заявки»): номер PR-год-NNNNN, отдел, инициатор, приоритет, желаемая дата, обоснование и позиции.
/// Черновик видит только инициатор; отправленную — руководитель отдела и снабжение. Version — xmin PostgreSQL: два одновременных
/// изменения одной заявки не перезаписывают друг друга.
/// </summary>
public class ProcurementRequest : Entity
{
    /// <summary>PR-2026-00125 — уникален, выдаётся при создании.</summary>
    public required string Number { get; set; }

    public required string Title { get; set; }

    public Guid DepartmentId { get; set; }
    public Department? Department { get; set; }

    /// <summary>Кто создал заявку (сотрудник или руководитель — за свой отдел).</summary>
    public Guid InitiatorId { get; set; }
    public User? Initiator { get; set; }

    public ProcurementPriority Priority { get; set; } = ProcurementPriority.Normal;
    public ProcurementRequestStatus Status { get; set; } = ProcurementRequestStatus.Draft;

    /// <summary>Желаемая дата получения (местная, Ташкент).</summary>
    public DateOnly? DesiredDate { get; set; }

    /// <summary>Причина закупки (обоснование) — обязательна при отправке.</summary>
    public string? Reason { get; set; }

    public string? Comment { get; set; }

    /// <summary>Предполагаемая сумма: количество × предполагаемая цена по позициям с ценой (UZS).</summary>
    public decimal EstimatedTotal { get; set; }

    public DateTimeOffset? SubmittedAt { get; set; }

    /// <summary>Передана директору на согласование (последний раз).</summary>
    public DateTimeOffset? ForwardedAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    /// <summary>Ключ повтора от клиента: повторная отправка той же формы (двойное нажатие) не создаёт вторую заявку.</summary>
    public Guid? ClientKey { get; set; }

    /// <summary>Есть позиции со складской частью — заявку видит кладовщик (к выдаче).</summary>
    public bool UsesStock { get; set; }

    /// <summary>Предполагаемая сумма к закупке: часть «к закупке» × предполагаемая цена (то, что согласует директор).</summary>
    public decimal PurchaseTotal { get; set; }

    /// <summary>Раунд согласования директором: 1 — первое, 2+ — повторное после существенного изменения.</summary>
    public int ApprovalRound { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }
    public Guid? ApprovedById { get; set; }

    /// <summary>Утверждённые условия (JSON позиций): с ними сравнивается правка утверждённой заявки — существенное изменение идёт на повторное согласование.</summary>
    public string? ApprovedSnapshot { get; set; }

    public uint Version { get; set; }

    public ICollection<ProcurementRequestItem> Items { get; set; } = [];
}

/// <summary>Позиция заявки: товар (своё название — каталог появится со складом), категория, количество, единица, предполагаемая цена за единицу.</summary>
public class ProcurementRequestItem : Entity
{
    public Guid RequestId { get; set; }
    public ProcurementRequest? Request { get; set; }

    /// <summary>Порядок в заявке (1, 2, 3…).</summary>
    public int LineNo { get; set; }

    public required string Name { get; set; }

    /// <summary>Позиция каталога склада; null — своё наименование (на складе такого нет — всё к закупке).</summary>
    public Guid? ItemId { get; set; }
    public ProcurementItem? Item { get; set; }

    public Guid CategoryId { get; set; }
    public ProcurementCategory? Category { get; set; }

    public required string UnitCode { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>Сколько взять со склада (резервируется при отправке); остальное — к закупке.</summary>
    public decimal FromStock { get; set; }

    /// <summary>К закупке: количество минус часть со склада.</summary>
    public decimal ToPurchase => Quantity - FromStock;

    /// <summary>Обоснование закупки сверх доступного на складе (на складе есть, а сотрудник просит купить).</summary>
    public string? PurchaseReason { get; set; }

    /// <summary>Сколько уже выдано со склада.</summary>
    public decimal IssuedQty { get; set; }

    /// <summary>Сколько заказано у поставщиков (по неотменённым заказам) — не больше части «к закупке».</summary>
    public decimal OrderedQty { get; set; }

    /// <summary>Сколько закупленного принято на склад (подтверждённые приёмки) — зарезервировано под заявку до выдачи.</summary>
    public decimal ReceivedQty { get; set; }

    /// <summary>Предполагаемая цена за единицу (UZS); null — не известна.</summary>
    public decimal? EstimatedPrice { get; set; }

    public string? Comment { get; set; }

    public ProcurementItemStatus Status { get; set; } = ProcurementItemStatus.Pending;
}

/// <summary>Категория закупки (Сырьё, Канцелярия, IT-оборудование…). Настраивается в разделе «Заявки»; не удаляется — отключается.</summary>
public class ProcurementCategory : Entity
{
    public required string Name { get; set; }

    /// <summary>Код системной категории (засевается при запуске); у добавленных вручную — null.</summary>
    public string? Code { get; set; }

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Единица измерения (шт, кг, л, упак…): системный справочник, засевается при запуске.</summary>
public class ProcurementUnit
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Счётчик номеров документов снабжения: префикс (PR — заявка) и год → последний выданный номер.</summary>
public class ProcurementCounter
{
    public required string Prefix { get; set; }
    public int Year { get; set; }
    public long Last { get; set; }
}

/// <summary>
/// История документа снабжения: кто, когда, действие, статус до и после, комментарий и изменённые данные (было/стало, JSON).
/// Пишется в той же записи, что и само изменение, — изменения без истории не бывает.
/// </summary>
public class ProcurementAuditLog : Entity
{
    /// <summary>Тип документа: request.</summary>
    public required string DocType { get; set; }

    public Guid DocId { get; set; }

    public Guid? ActorId { get; set; }
    public User? Actor { get; set; }

    /// <summary>created, updated, submitted, forwarded, returned, cancelled, attachment.</summary>
    public required string Action { get; set; }

    public string? FromStatus { get; set; }
    public string? ToStatus { get; set; }

    /// <summary>Описание по-русски: «Отправлена на согласование», «Изменено: количество бумаги 20 → 25».</summary>
    public required string Text { get; set; }

    public string? Comment { get; set; }

    /// <summary>Изменённые данные: {"before": …, "after": …} — только изменившиеся поля и позиции.</summary>
    public string? Changes { get; set; }
}

/// <summary>Вложение документа снабжения: содержимое — в хранилище файлов (MinIO) под ObjectKey, здесь — описание.</summary>
public class ProcurementDocument : Entity
{
    /// <summary>Тип владельца: request, supplier, offer, order.</summary>
    public required string OwnerType { get; set; }

    /// <summary>Вид документа: contract (договор), offer (КП), invoice (счёт), waybill (накладная), other; null — без вида.</summary>
    public string? Kind { get; set; }

    public Guid OwnerId { get; set; }

    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public required string ObjectKey { get; set; }

    public Guid? UploadedById { get; set; }
    public User? UploadedBy { get; set; }
}
