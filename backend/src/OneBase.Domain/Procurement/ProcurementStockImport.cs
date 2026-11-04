using OneBase.Domain.Common;

namespace OneBase.Domain.Procurement;

/// <summary>Чем была загрузка движений склада из Excel.</summary>
public enum ProcurementStockImportMode
{
    /// <summary>Первая загрузка: по всем строкам файла посчитаны остатки и заведены одной инвентаризацией; история остаётся в файле.</summary>
    Opening,

    /// <summary>Следующая: только новые строки файла — приходы, выдачи отделам, списания.</summary>
    Movements,
}

/// <summary>
/// Загрузка движений склада из Excel (журнал склада, который ведут в таблице): что за файл, кто и когда загрузил, сколько строк прочитано,
/// учтено и пропущено, сколько документов проведено, по какую дату движения.
/// </summary>
public class ProcurementStockImport : Entity
{
    public Guid WarehouseId { get; set; }
    public required string FileName { get; set; }
    public ProcurementStockImportMode Mode { get; set; }

    /// <summary>Строк движений в файле (без пустых).</summary>
    public int Rows { get; set; }

    /// <summary>Учтено строк: при начальных остатках — все верные строки, дальше — проведённые новые.</summary>
    public int Posted { get; set; }

    /// <summary>Пропущено строк: ошибка в строке или не хватило остатка (такие строки загрузятся в следующий раз).</summary>
    public int Skipped { get; set; }

    public int Documents { get; set; }

    /// <summary>Последняя дата движения среди учтённых строк.</summary>
    public DateOnly? LastDate { get; set; }

    public Guid? ActorId { get; set; }
}

/// <summary>
/// Учтённая строка файла движений — чтобы следующая загрузка того же (дописанного) файла не провела её второй раз. Key — отпечаток строки:
/// дата заполнения, дата, вид, цех или склад, артикул и номер повтора такой же строки в файле. DocumentId — документ, которым строка
/// проведена (у начальных остатков — инвентаризация).
/// </summary>
public class ProcurementStockImportRow
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ImportId { get; set; }
    public Guid WarehouseId { get; set; }
    public required string Key { get; set; }
    public Guid? DocumentId { get; set; }
}
