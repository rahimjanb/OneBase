using OneBase.Domain.Common;
using OneBase.Domain.Identity;

namespace OneBase.Domain.Procurement;

/// <summary>
/// Поставка (приезд машины): на проверке качества — лаборатория проверяет строки с контролем; готово к приёмке — по всем строкам решено,
/// кладовщик взвешивает и принимает; принято — приход на склад проведён; возврат — забраковано всё, на склад ничего не пришло; отменена —
/// отмечена по ошибке или машина уехала до приёмки.
/// </summary>
public enum ProcurementArrivalStatus
{
    Quality,
    Ready,
    Accepted,
    Returned,
    Cancelled,
}

/// <summary>
/// Поставка AR-год-NNNNN — приезд машины по тендеру или по покупке «Куплено — привезёт поставщик» (заказ PO-): кладовщик отмечает «Машина приехала» (машина, водитель, накладная, что по накладной,
/// партии поставщика и сроки); склад при этом не меняется. По строкам с контролем качества заводятся проверки (QC-) — лаборатория проверяет
/// до склада. Решено по всем — «готово к приёмке»: кладовщик пишет фактические места и вес и принимает — одной записью приход RC- по цене
/// тендера, партии сразу допущены (со ссылкой на свою проверку), у тендера растёт «поставлено»; снабжению, аналитикам и финансисту —
/// уведомление. Забракованные строки на склад не приходуются — их возвращают поставщику. Version — xmin.
/// </summary>
public class ProcurementArrival : Entity
{
    public required string Number { get; set; }

    public ProcurementArrivalStatus Status { get; set; } = ProcurementArrivalStatus.Quality;

    /// <summary>Тендер, по которому привезли (или заказ — ровно одно из двух).</summary>
    public Guid? TenderId { get; set; }
    public ProcurementTender? Tender { get; set; }

    /// <summary>Заказ «Куплено — привезёт поставщик», по которому привезли.</summary>
    public Guid? OrderId { get; set; }
    public ProcurementPurchaseOrder? Order { get; set; }

    public Guid SupplierId { get; set; }
    public ProcurementSupplier? Supplier { get; set; }

    /// <summary>Склад, на который принимают.</summary>
    public Guid WarehouseId { get; set; }

    /// <summary>Госномер машины.</summary>
    public string? VehicleNo { get; set; }

    public string? DriverName { get; set; }

    /// <summary>Номер накладной поставщика.</summary>
    public string? Reference { get; set; }

    public string? Comment { get; set; }

    /// <summary>Когда машина приехала — кто отметил.</summary>
    public DateTimeOffset ArrivedAt { get; set; }

    public Guid RegisteredById { get; set; }
    public User? RegisteredBy { get; set; }

    /// <summary>Решено по всем проверкам (готово к приёмке или возврат).</summary>
    public DateTimeOffset? ReadyAt { get; set; }

    public DateTimeOffset? AcceptedAt { get; set; }
    public Guid? AcceptedById { get; set; }
    public User? AcceptedBy { get; set; }

    /// <summary>Приход RC-, которым принято на склад.</summary>
    public Guid? ReceiptId { get; set; }

    /// <summary>Сумма принятого по цене тендера, сум.</summary>
    public decimal AcceptedTotal { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancelReason { get; set; }

    public Guid? ClientKey { get; set; }

    public uint Version { get; set; }

    public ICollection<ProcurementArrivalLine> Lines { get; set; } = [];
}

/// <summary>
/// Строка поставки — позиция тендера (одна партия поставщика): по накладной (в единице тендера), партия поставщика, дата изготовления и срок
/// годности; контроль качества позиции на момент приезда и проверка (у позиции с контролем). Качество: Pending — ждёт решения, Approved —
/// принять можно (или контроль не нужен), Rejected — вернуть поставщику. При приёмке: мест принято (в единице тендера — засчитывается тендеру),
/// вес нетто (у позиции, которая ведётся в кг, г или т, — он идёт на склад), на склад — в единице карточки, партия.
/// </summary>
public class ProcurementArrivalLine
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid ArrivalId { get; set; }
    public ProcurementArrival? Arrival { get; set; }

    public int LineNo { get; set; }

    /// <summary>Позиция тендера (у поставки по тендеру).</summary>
    public Guid? TenderItemId { get; set; }
    public ProcurementTenderItem? TenderItem { get; set; }

    /// <summary>Позиция заказа «Куплено» (у поставки по заказу) — через неё позиция заявки: принятое резервируется под заявку.</summary>
    public Guid? OrderItemId { get; set; }
    public ProcurementPurchaseOrderItem? OrderItem { get; set; }

    /// <summary>Карточка склада позиции тендера.</summary>
    public Guid ItemId { get; set; }
    public ProcurementItem? Item { get; set; }

    /// <summary>Единица позиции тендера или заказа (мешок, упак, кг).</summary>
    public required string UnitCode { get; set; }

    /// <summary>По накладной — в единице тендера.</summary>
    public decimal Declared { get; set; }

    /// <summary>По накладной — в единице карточки (пересчёт по единице закупки).</summary>
    public decimal DeclaredBase { get; set; }

    public string? SupplierBatchNo { get; set; }
    public DateOnly? ManufacturedOn { get; set; }
    public DateOnly? ExpiresOn { get; set; }

    public ProcurementQualityControl Control { get; set; }

    public Guid? QualityCheckId { get; set; }
    public ProcurementQualityCheck? QualityCheck { get; set; }

    public ProcurementBatchQuality Quality { get; set; } = ProcurementBatchQuality.Pending;

    /// <summary>Мест принято — в единице тендера.</summary>
    public decimal? AcceptedQty { get; set; }

    /// <summary>Вес нетто — в единице карточки (кг, г, т), если она весовая.</summary>
    public decimal? AcceptedWeight { get; set; }

    /// <summary>Пришло на склад — в единице карточки.</summary>
    public decimal? StockQty { get; set; }

    public Guid? BatchId { get; set; }
    public ProcurementBatch? Batch { get; set; }

    public string? Comment { get; set; }
}
