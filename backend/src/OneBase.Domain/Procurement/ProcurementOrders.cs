using OneBase.Domain.Common;
using OneBase.Domain.Identity;

namespace OneBase.Domain.Procurement;

/// <summary>
/// Поставщик снабжения: реквизиты, контакты, категории товаров, условия оплаты и срок поставки. Не удаляется — отключается (по нему
/// остаются предложения и заказы). ИНН уникален, если указан. Поставщики Linko (внутренние цеха) сюда не входят.
/// </summary>
public class ProcurementSupplier : Entity
{
    public required string Name { get; set; }

    /// <summary>Фирма — юридическое название (ООО «…»), если поставщика в компании зовут иначе («Арахис Наманган»); null — не указано.</summary>
    public string? LegalName { get; set; }

    /// <summary>ИНН (9 цифр в Узбекистане); null — не указан.</summary>
    public string? Inn { get; set; }

    public string? ContactPerson { get; set; }

    /// <summary>Телефоны — один или несколько через запятую.</summary>
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }

    public string? BankName { get; set; }

    /// <summary>МФО банка.</summary>
    public string? BankMfo { get; set; }

    /// <summary>Расчётный счёт.</summary>
    public string? BankAccount { get; set; }

    public string? PaymentTerms { get; set; }

    /// <summary>Обычный срок поставки, дней.</summary>
    public int? LeadTimeDays { get; set; }

    /// <summary>Что поставляет: товары и сырьё, по строке на позицию (ищется в реестре).</summary>
    public string? Products { get; set; }

    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public Guid? CreatedById { get; set; }

    public ICollection<ProcurementSupplierCategory> Categories { get; set; } = [];
}

/// <summary>Категория товаров, которые поставляет поставщик.</summary>
public class ProcurementSupplierCategory
{
    public Guid SupplierId { get; set; }
    public ProcurementSupplier? Supplier { get; set; }
    public Guid CategoryId { get; set; }
    public ProcurementCategory? Category { get; set; }
}

public enum ProcurementOfferStatus
{
    Proposed,
    Selected,
    Rejected,
}

/// <summary>
/// Коммерческое предложение поставщика по заявке: цены по позициям «к закупке», доставка (стоимость и срок), НДС, условия оплаты,
/// гарантия, срок действия, оценка качества (1–5, ставит снабжение), вложения PDF/Excel. Выбранное становится основой заказа.
/// </summary>
public class ProcurementOffer : Entity
{
    public Guid RequestId { get; set; }
    public ProcurementRequest? Request { get; set; }

    public Guid SupplierId { get; set; }
    public ProcurementSupplier? Supplier { get; set; }

    public DateOnly? ValidUntil { get; set; }

    /// <summary>Срок поставки, дней.</summary>
    public int? DeliveryDays { get; set; }

    public decimal DeliveryCost { get; set; }

    /// <summary>Ставка НДС, % (0 — без НДС).</summary>
    public decimal VatRate { get; set; }

    /// <summary>Цены указаны с НДС.</summary>
    public bool PricesIncludeVat { get; set; } = true;

    public string? PaymentTerms { get; set; }
    public string? Warranty { get; set; }

    /// <summary>Оценка качества 1–5 (снабжение); null — не оценено.</summary>
    public int? Quality { get; set; }

    public string? Comment { get; set; }

    public ProcurementOfferStatus Status { get; set; } = ProcurementOfferStatus.Proposed;

    /// <summary>Итог предложения: позиции + НДС (если цены без НДС) + доставка.</summary>
    public decimal Total { get; set; }

    public Guid? CreatedById { get; set; }

    public ICollection<ProcurementOfferItem> Items { get; set; } = [];
}

/// <summary>Цена предложения по позиции заявки: за единицу и предложенное количество.</summary>
public class ProcurementOfferItem
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid OfferId { get; set; }
    public ProcurementOffer? Offer { get; set; }
    public Guid RequestItemId { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Quantity { get; set; }
    public string? Note { get; set; }
}

/// <summary>
/// Статус заказа поставщику. Draft → (оформить) → Approved (в рамках утверждённой заявки) или AwaitingApproval (дороже или без цены —
/// решает директор) → Ordered (отправлен поставщику) → PartiallyDelivered → Delivered → Completed; Cancelled — отменён.
/// </summary>
public enum ProcurementOrderStatus
{
    Draft,
    AwaitingApproval,
    Approved,
    Ordered,
    PartiallyDelivered,
    Delivered,
    Completed,
    Cancelled,
}

/// <summary>
/// Заказ поставщику (PO-год-NNNNN): позиции из утверждённых заявок (одна заявка — несколько заказов, один заказ — позиции нескольких
/// заявок), цены, скидка, НДС, доставка, итог, даты, условия оплаты, ответственный. Утверждён не созданием черновика: оформление
/// сверяет заказ с утверждённой директором заявкой — дороже допуска, без цены или после смены поставщика решает директор. Version — xmin.
/// </summary>
public class ProcurementPurchaseOrder : Entity
{
    public required string Number { get; set; }

    public Guid SupplierId { get; set; }
    public ProcurementSupplier? Supplier { get; set; }

    /// <summary>Предложение, из которого создан заказ.</summary>
    public Guid? OfferId { get; set; }

    /// <summary>Ответственный сотрудник снабжения.</summary>
    public Guid ResponsibleId { get; set; }
    public User? Responsible { get; set; }

    public Guid? CreatedById { get; set; }

    public ProcurementOrderStatus Status { get; set; } = ProcurementOrderStatus.Draft;

    /// <summary>Дата заказа (отправлен поставщику).</summary>
    public DateOnly? OrderDate { get; set; }

    /// <summary>Ожидаемая дата доставки.</summary>
    public DateOnly? ExpectedDate { get; set; }

    public string? PaymentTerms { get; set; }

    /// <summary>Скидка на заказ, сумма (UZS).</summary>
    public decimal Discount { get; set; }

    public decimal VatRate { get; set; }
    public bool PricesIncludeVat { get; set; } = true;
    public decimal DeliveryCost { get; set; }

    /// <summary>Позиции: количество × цена.</summary>
    public decimal Subtotal { get; set; }

    /// <summary>НДС, который добавляется сверху (если цены без НДС).</summary>
    public decimal VatAmount { get; set; }

    /// <summary>Итог: позиции − скидка + НДС + доставка.</summary>
    public decimal Total { get; set; }

    public string? Comment { get; set; }

    /// <summary>Раунд согласования директором (0 — в рамках утверждённой заявки, без директора).</summary>
    public int ApprovalRound { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }
    public Guid? ApprovedById { get; set; }

    /// <summary>Утверждённые условия (JSON: поставщик, позиции, цены, итог) — с ними сравнивается правка.</summary>
    public string? ApprovedSnapshot { get; set; }

    public DateTimeOffset? OrderedAt { get; set; }

    /// <summary>Поставлен целиком (или закрыт недопоставленный остаток) — по этой дате считается поставка в срок.</summary>
    public DateTimeOffset? DeliveredAt { get; set; }

    /// <summary>Закрыт снабжением после поставки.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    /// <summary>На какую ожидаемую дату ушло «поставка завтра» (дату перенесли — будет новое напоминание).</summary>
    public DateOnly? DueSoonNotifiedFor { get; set; }

    /// <summary>На какую ожидаемую дату ушло «поставка просрочена».</summary>
    public DateOnly? OverdueNotifiedFor { get; set; }

    /// <summary>Задача ответственному в «Задачах» (отдел «Снабжение»).</summary>
    public Guid? TaskId { get; set; }

    /// <summary>
    /// Покупка снабжения («Куплено» по заявке): снабженец купил сам — заказ сразу закрыт и оприходован на склад, без отправки поставщику,
    /// доставки и приёмки.
    /// </summary>
    public bool Direct { get; set; }

    public Guid? ClientKey { get; set; }

    public uint Version { get; set; }

    public ICollection<ProcurementPurchaseOrderItem> Items { get; set; } = [];
}

/// <summary>
/// Позиция заказа: позиция утверждённой заявки, количество, цена за единицу, получено (подтверждённые приёмки), закрыто без поставки
/// (снабжение закрыло недопоставленный остаток — его можно заказать снова). Ждёт поставки = количество − получено − закрыто.
/// </summary>
public class ProcurementPurchaseOrderItem
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid OrderId { get; set; }
    public ProcurementPurchaseOrder? Order { get; set; }

    public int LineNo { get; set; }

    public Guid RequestItemId { get; set; }
    public ProcurementRequestItem? RequestItem { get; set; }

    public required string Name { get; set; }
    public required string UnitCode { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal ReceivedQty { get; set; }

    public decimal ClosedQty { get; set; }
}

/// <summary>Решение директора по заказу (раунд, решение, комментарий, снимок условий, итог).</summary>
public class ProcurementOrderApproval : Entity
{
    public Guid OrderId { get; set; }
    public int Round { get; set; }
    public ProcurementApprovalDecision Decision { get; set; }
    public string? Comment { get; set; }

    public Guid DeciderId { get; set; }
    public User? Decider { get; set; }

    public decimal Total { get; set; }
    public required string Snapshot { get; set; }
}
