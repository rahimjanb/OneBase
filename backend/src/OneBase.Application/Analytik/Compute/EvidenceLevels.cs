namespace OneBase.Application.Analytik.Compute;

/// <summary>
/// Уровень доказательности вывода (ТЗ Phase 2, Evidence &amp; Validation Engine). Каждый вывод исследования помечен одним уровнем:
/// FACT — значение прямо из данных («выручка снизилась на 8 %»); CALCULATED — посчитано кодом из фактов (доля, вклад, разница);
/// STATISTICALLY_SUPPORTED — проверка значимости пройдена (тренд, сдвиг, разница средних, причинная проверка с контрольной группой);
/// CORRELATION — показатели меняются вместе (связь, не причина); HYPOTHESIS — возможное объяснение, данными не подтверждено;
/// FORECAST — расчёт будущего; UNVERIFIED — проверить не удалось (данных не хватило, расчёт не прошёл). Гипотеза никогда не выдаётся за факт.
/// </summary>
public static class EvidenceLevels
{
    public const string Fact = "FACT";
    public const string Calculated = "CALCULATED";
    public const string Statistical = "STATISTICALLY_SUPPORTED";
    public const string Correlation = "CORRELATION";
    public const string Hypothesis = "HYPOTHESIS";
    public const string Forecast = "FORECAST";
    public const string Unverified = "UNVERIFIED";

    public static readonly IReadOnlyList<string> All = [Fact, Calculated, Statistical, Correlation, Hypothesis, Forecast, Unverified];

    public static string Title(string? level) => level switch
    {
        Fact => "Факт",
        Calculated => "Расчёт",
        Statistical => "Статистически подтверждено",
        Correlation => "Корреляция (связь, не причина)",
        Hypothesis => "Гипотеза",
        Forecast => "Прогноз",
        Unverified => "Не проверено",
        _ => "—",
    };

    /// <summary>
    /// Уровень по классу вывода вычислительного движка: проверенная причинная гипотеза и статистическая связь — «статистически подтверждено»
    /// (для корреляций — CORRELATION: значимая связь всё равно не причина), наблюдаемая зависимость — CORRELATION, предположение — HYPOTHESIS.
    /// </summary>
    public static string FromClass(string? evidenceClass, bool association = false) => evidenceClass switch
    {
        EvidenceClasses.TestedCausal => Statistical,
        EvidenceClasses.Statistical => association ? Correlation : Statistical,
        EvidenceClasses.Observed => association ? Correlation : Calculated,
        EvidenceClasses.Assumption => Hypothesis,
        _ => Unverified,
    };

    /// <summary>Сила уровня для сортировки выводов (факт — сильнее всего, не проверено — слабее всего).</summary>
    public static int Rank(string? level) => level switch
    {
        Fact => 6,
        Calculated => 5,
        Statistical => 4,
        Correlation => 3,
        Forecast => 2,
        Hypothesis => 1,
        _ => 0,
    };
}
