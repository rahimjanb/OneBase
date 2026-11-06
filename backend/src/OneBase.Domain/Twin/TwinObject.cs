using OneBase.Domain.Common;
using OneBase.Domain.Identity;
using OneBase.Domain.Procurement;

namespace OneBase.Domain.Twin;

/// <summary>
/// Здание цифрового двойника (Operational Twin): постоянный ключ объекта модели территории («A»…«J») и его название в компании. Строка
/// появляется, когда администратор впервые привязывает или переименовывает здание; до этого — название из модели и привязок нет.
/// </summary>
public class TwinObject : Entity
{
    public required string Key { get; set; }

    public required string Name { get; set; }

    public Guid? UpdatedById { get; set; }

    public ICollection<TwinObjectLink> Links { get; set; } = [];
}

/// <summary>
/// Что находится в здании: отдел OneBase, склад снабжения или склад Linko (готовая продукция «Завод») — по постоянному идентификатору.
/// В строке задано ровно одно из трёх.
/// </summary>
public class TwinObjectLink : Entity
{
    public Guid ObjectId { get; set; }
    public TwinObject? Object { get; set; }

    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public Guid? WarehouseId { get; set; }
    public ProcurementWarehouse? Warehouse { get; set; }

    /// <summary>Склад Linko (<c>linko."Stocks"."Id"</c>) — зеркало Linko, поэтому без внешнего ключа.</summary>
    public long? LinkoStockId { get; set; }
}
