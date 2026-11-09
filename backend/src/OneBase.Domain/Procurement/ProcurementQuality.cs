using OneBase.Domain.Common;
using OneBase.Domain.Identity;

namespace OneBase.Domain.Procurement;

/// <summary>Контроль качества позиции при приходе: нет (партия сразу допущена), проверка документов, документы и лаборатория.</summary>
public enum ProcurementQualityControl
{
    None,
    Documents,
    Lab,
}

/// <summary>
/// Статус качества партии: допущена (можно выдавать, пока не просрочена), на контроле (ждёт решения), забракована (вернуть поставщику или
/// списать), заблокирована (жалоба, отзыв, «срок не указан» при вводе остатков). Выдаётся только допущенная.
/// </summary>
public enum ProcurementBatchQuality
{
    Approved,
    Pending,
    Rejected,
    Blocked,
}

/// <summary>Строка спецификации: документ, который проверяют при приходе, или показатель лаборатории.</summary>
public enum ProcurementQualitySpecKind
{
    Document,
    Parameter,
}

/// <summary>
/// Шаг проверки партии: ждёт проверки документов, ждёт лаборатории, ждёт решения назначенного, решение принято, отменена (приход
/// сторнирован). Порядок — документы, лаборатория (если нужна), решение.
/// </summary>
public enum ProcurementQualityCheckStatus
{
    Documents,
    Lab,
    Decision,
    Decided,
    Cancelled,
}

/// <summary>
/// Спецификация материала — строка: документ («Сертификат качества») или показатель («Влажность», %, не больше 14). Количественный
/// показатель — с нормой Min/Max, качественный — с ожидаемым («без посторонних запахов»). Не удаляется — отключается: по строке остаются
/// результаты проверок (они хранят снимок нормы).
/// </summary>
public class ProcurementQualitySpec : Entity
{
    public Guid ItemId { get; set; }
    public ProcurementItem? Item { get; set; }

    public int LineNo { get; set; }
    public ProcurementQualitySpecKind Kind { get; set; }

    public required string Name { get; set; }
    public string? Unit { get; set; }
    public decimal? Min { get; set; }
    public decimal? Max { get; set; }
    public string? Expected { get; set; }
    public string? Method { get; set; }

    /// <summary>Обязательная строка: без неё проверку не завершить.</summary>
    public bool Required { get; set; } = true;

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Проверка качества партии (QC-год-NNNNN): заводится приходом позиции с контролем или приездом машины (поставка AR-, до склада — партии ещё
/// нет, она появится при приёмке). Документы → лаборатория (отбор пробы — списание «Пробы лаборатории»; до склада проба только
/// записывается, показатели по спецификации) → решение назначенного: допустить или забраковать (с комментарием). Решение неизменяемо;
/// открытая проверка у партии одна. Version — xmin: двойное нажатие не принимает второе решение.
/// </summary>
public class ProcurementQualityCheck : Entity
{
    public required string Number { get; set; }

    /// <summary>Партия; у проверки поставки до приёмки — null (партия появится, когда кладовщик примет допущенное).</summary>
    public Guid? BatchId { get; set; }
    public ProcurementBatch? Batch { get; set; }

    /// <summary>Позиция склада — у партии та же.</summary>
    public Guid ItemId { get; set; }
    public ProcurementItem? Item { get; set; }

    /// <summary>Поставщик — для рейтинга поставщика, в том числе по забракованному при приезде, что на склад не пришло.</summary>
    public Guid? SupplierId { get; set; }
    public ProcurementSupplier? Supplier { get; set; }

    /// <summary>Поставка (приезд машины), строку которой проверяют до склада.</summary>
    public Guid? ArrivalId { get; set; }
    public ProcurementArrival? Arrival { get; set; }

    /// <summary>Склад, на который пришла (или принимается) партия.</summary>
    public Guid WarehouseId { get; set; }

    /// <summary>Контроль позиции на момент прихода (документы или документы и лаборатория).</summary>
    public ProcurementQualityControl Control { get; set; }

    public ProcurementQualityCheckStatus Status { get; set; } = ProcurementQualityCheckStatus.Documents;

    public bool? DocumentsOk { get; set; }
    public string? DocumentsNote { get; set; }
    public Guid? DocumentsById { get; set; }
    public DateTimeOffset? DocumentsAt { get; set; }

    /// <summary>Отобрано на анализ (списано документом WO- с QualityCheckId).</summary>
    public decimal? SampleQty { get; set; }

    public bool? LabOk { get; set; }
    public string? LabNote { get; set; }
    public Guid? LabById { get; set; }
    public DateTimeOffset? LabAt { get; set; }

    /// <summary>Решение: Approved или Rejected.</summary>
    public ProcurementBatchQuality? Decision { get; set; }

    public string? DecisionComment { get; set; }
    public Guid? DecidedById { get; set; }
    public User? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }

    public uint Version { get; set; }

    public ICollection<ProcurementQualityResult> Results { get; set; } = [];
}

/// <summary>
/// Результат проверки по строке спецификации — со снимком нормы: документ (есть и в порядке) или показатель (значение или текст и
/// «соответствует»: по норме Min/Max — считается, у качественного — ставит лаборант). Passed null — не проверено.
/// </summary>
public class ProcurementQualityResult
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid CheckId { get; set; }
    public ProcurementQualityCheck? Check { get; set; }

    public Guid? SpecId { get; set; }
    public ProcurementQualitySpecKind Kind { get; set; }
    public int LineNo { get; set; }

    public required string Name { get; set; }
    public string? Unit { get; set; }
    public decimal? Min { get; set; }
    public decimal? Max { get; set; }
    public string? Expected { get; set; }

    public decimal? Value { get; set; }
    public string? Text { get; set; }
    public bool? Passed { get; set; }
}

/// <summary>
/// Назначенный решать по качеству («Настройки → Отделы → Снабжение → Контроль качества»): допускает и бракует партии, блокирует и снимает
/// блокировку. Решает место, а не право: технический администратор со всеми правами партии не допускает.
/// </summary>
public class ProcurementQualityApprover
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
}
