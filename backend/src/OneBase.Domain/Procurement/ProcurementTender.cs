using OneBase.Domain.Common;
using OneBase.Domain.Identity;

namespace OneBase.Domain.Procurement;

/// <summary>Состояние тендерной закупки: договор заключён — ждёт поставки; поставлено; отменена (запись не удаляется).</summary>
public enum ProcurementTenderStatus
{
    Contracted,
    Delivered,
    Cancelled,
}

/// <summary>
/// Тендерная закупка — журнал снабжения: сырьё и материалы для производства, закупленные через тендер. Запись снабженец добавляет вручную
/// после тендера: предмет, номер и дата тендера, победитель (поставщик), договор, участники, позиции с ценами, срок поставки; потом отмечает
/// поставку. На склад снабжения не приходуется — сырьё идёт в производство. Номер TN-год-NNNNN. Version — xmin.
/// </summary>
public class ProcurementTender : Entity
{
    public required string Number { get; set; }

    /// <summary>Предмет тендера: «Сахар-песок на I квартал».</summary>
    public required string Title { get; set; }

    /// <summary>Номер тендера (лота) на площадке или внутренний.</summary>
    public string? TenderNo { get; set; }

    /// <summary>Дата итогов тендера (местная, Ташкент).</summary>
    public DateOnly TenderDate { get; set; }

    /// <summary>Победитель тендера.</summary>
    public Guid SupplierId { get; set; }
    public ProcurementSupplier? Supplier { get; set; }

    public Guid CategoryId { get; set; }
    public ProcurementCategory? Category { get; set; }

    public string? ContractNo { get; set; }

    /// <summary>Сколько поставщиков участвовало; null — не указано.</summary>
    public int? Participants { get; set; }

    /// <summary>Срок поставки по договору.</summary>
    public DateOnly? DeliveryDate { get; set; }

    public ProcurementTenderStatus Status { get; set; } = ProcurementTenderStatus.Contracted;

    /// <summary>Когда поставлено (фактически).</summary>
    public DateOnly? DeliveredOn { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    /// <summary>Сумма по позициям (количество × цена), сум.</summary>
    public decimal Total { get; set; }

    public string? Comment { get; set; }

    /// <summary>Кто вёл тендер — кто добавил запись.</summary>
    public Guid ResponsibleId { get; set; }
    public User? Responsible { get; set; }

    public Guid? ClientKey { get; set; }

    public uint Version { get; set; }

    public ICollection<ProcurementTenderItem> Items { get; set; } = [];
}

/// <summary>Позиция тендерной закупки: наименование, единица, количество, цена за единицу (сум).</summary>
public class ProcurementTenderItem
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TenderId { get; set; }
    public ProcurementTender? Tender { get; set; }

    public int LineNo { get; set; }

    public required string Name { get; set; }
    public required string UnitCode { get; set; }

    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
}
