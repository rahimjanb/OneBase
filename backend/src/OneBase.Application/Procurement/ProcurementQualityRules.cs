using OneBase.Domain.Procurement;

namespace OneBase.Application.Procurement;

/// <summary>
/// Правила контроля качества (чистые функции — проверяются тестами): соответствие показателя норме, следующий шаг проверки, что можно решить
/// на каком шаге.
/// </summary>
public static class ProcurementQualityRules
{
    public const string Prefix = "QC";

    /// <summary>Причина блокировки партии, введённой без срока годности.</summary>
    public const string UnknownExpiry = "срок годности не указан при вводе остатков";

    public const int MaxSpecLines = 50;

    /// <summary>Открытые шаги: документы, лаборатория, решение.</summary>
    public static readonly ProcurementQualityCheckStatus[] Open =
        [ProcurementQualityCheckStatus.Documents, ProcurementQualityCheckStatus.Lab, ProcurementQualityCheckStatus.Decision];

    public static bool IsOpen(ProcurementQualityCheckStatus status) => Open.Contains(status);

    /// <summary>Показатель в норме: не меньше Min и не больше Max (границы входят; не задана — не проверяется).</summary>
    public static bool Passed(decimal? min, decimal? max, decimal value) => (min is null || value >= min) && (max is null || value <= max);

    /// <summary>
    /// Шаг проверки по её итогам: документы не проверены — «документы»; документы не в порядке — сразу решение (обычно забраковать);
    /// контроль «по документам» — решение; лаборатория не закончила — «лаборатория»; иначе — решение.
    /// </summary>
    public static ProcurementQualityCheckStatus Next(ProcurementQualityControl control, bool? documentsOk, bool? labOk) => documentsOk switch
    {
        null => ProcurementQualityCheckStatus.Documents,
        false => ProcurementQualityCheckStatus.Decision,
        _ when control != ProcurementQualityControl.Lab => ProcurementQualityCheckStatus.Decision,
        _ => labOk is null ? ProcurementQualityCheckStatus.Lab : ProcurementQualityCheckStatus.Decision,
    };

    /// <summary>Лабораторию можно вести: контроль с лабораторией, документы в порядке, решение ещё не принято.</summary>
    public static bool LabAllowed(ProcurementQualityControl control, ProcurementQualityCheckStatus status, bool? documentsOk) =>
        control == ProcurementQualityControl.Lab && documentsOk == true && status is ProcurementQualityCheckStatus.Lab or ProcurementQualityCheckStatus.Decision;

    /// <summary>Допустить можно только на шаге «решение» (все проверки пройдены по порядку); забраковать — на любом открытом шаге.</summary>
    public static bool CanApprove(ProcurementQualityCheckStatus status) => status == ProcurementQualityCheckStatus.Decision;

    /// <summary>Допуск с отклонениями (документы не в порядке или показатель вне нормы) — только с комментарием.</summary>
    public static bool Deviates(bool? documentsOk, bool? labOk) => documentsOk == false || labOk == false;

    public static string Label(ProcurementQualityCheckStatus status) => status switch
    {
        ProcurementQualityCheckStatus.Documents => "проверка документов",
        ProcurementQualityCheckStatus.Lab => "лаборатория",
        ProcurementQualityCheckStatus.Decision => "ждёт решения",
        ProcurementQualityCheckStatus.Decided => "решение принято",
        _ => "отменена",
    };

    public static string Label(ProcurementBatchQuality quality) => quality switch
    {
        ProcurementBatchQuality.Approved => "допущена",
        ProcurementBatchQuality.Pending => "на контроле",
        ProcurementBatchQuality.Rejected => "забракована",
        _ => "заблокирована",
    };
}
