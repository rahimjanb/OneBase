using OneBase.Domain.Common;

namespace OneBase.Domain.Analytik;

/// <summary>
/// Расчёт вычислительного движка (Advanced Intelligence): метод, алгоритм и его версия, параметры, вход (запрос среза, число строк, отпечаток
/// данных, период), результат, источники, ограничения, класс вывода (наблюдаемая зависимость, статистически подтверждённая связь, проверенная
/// причинная гипотеза, предположение). Пишется на каждый вызов — расчёт можно повторить по записи и сверить.
/// </summary>
public class AnalytikComputation : Entity
{
    public Guid? UserId { get; set; }

    /// <summary>chat | investigation | api | proactive | decision | benchmark.</summary>
    public required string Origin { get; set; }

    public Guid? InvestigationId { get; set; }
    public Guid? ConversationId { get; set; }

    public required string Method { get; set; }
    public required string Domain { get; set; }
    public required string Algorithm { get; set; }
    public required string Version { get; set; }

    /// <summary>Параметры метода (jsonb).</summary>
    public required string Parameters { get; set; }

    /// <summary>Вход (jsonb): запрос среза или сервиса, id запроса в журнале, строк, отпечаток данных.</summary>
    public required string Input { get; set; }

    /// <summary>Результат (jsonb); null — ошибка.</summary>
    public string? Result { get; set; }

    public string? Sources { get; set; }
    public string? Limitations { get; set; }
    public DateOnly? PeriodFrom { get; set; }
    public DateOnly? PeriodTo { get; set; }

    /// <summary>observed | statistical | tested_causal | assumption; null — не применимо (описательный расчёт).</summary>
    public string? EvidenceClass { get; set; }

    /// <summary>ok | insufficient | error.</summary>
    public required string Status { get; set; }

    public string? Error { get; set; }
    public string? Scope { get; set; }
    public long ElapsedMs { get; set; }
}

/// <summary>
/// Исследование сложного вопроса: цель, план (подзадачи с зависимостями), шаги с промежуточными результатами, гипотезы со статусами,
/// противоречия, проверки Reviewer, трасса событий и метрики (вызовы моделей и инструментов, токены, стоимость, время, глубина, отклонения
/// проверки, перепланирования). Сводные числа — колонками для мониторинга качества, подробности — jsonb.
/// </summary>
public class AnalytikInvestigation : Entity
{
    public Guid? UserId { get; set; }
    public Guid? ConversationId { get; set; }

    /// <summary>Общий идентификатор вызовов модели одного ответа (ai.Usage.TurnId, ai.AuditLogs.TurnId).</summary>
    public Guid? TurnId { get; set; }

    /// <summary>chat | proactive | benchmark | api.</summary>
    public required string Origin { get; set; }

    public required string Question { get; set; }
    public string? Goal { get; set; }
    public int Complexity { get; set; }

    /// <summary>planned | running | completed | partial | failed | cancelled.</summary>
    public required string Status { get; set; }

    public string? Roles { get; set; }
    public string? Plan { get; set; }
    public string? Steps { get; set; }
    public string? Hypotheses { get; set; }
    public string? Contradictions { get; set; }
    public string? Reviews { get; set; }
    public string? Trace { get; set; }
    public string? Limitations { get; set; }
    public string? Summary { get; set; }

    public int Depth { get; set; }
    public int StepCount { get; set; }
    public int ToolCalls { get; set; }
    public int LlmCalls { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public decimal? CostUsd { get; set; }
    public long DurationMs { get; set; }
    public int ReviewRejections { get; set; }
    public int Replans { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
}

/// <summary>История находки: появилась, повторилась, изменилась важность или масштаб, расследована, закрыта, вернулась.</summary>
public class AnalytikInsightEvent : Entity
{
    public Guid InsightId { get; set; }
    public DateTimeOffset At { get; set; }

    /// <summary>created | severity | impact | investigated | resolved | reopened.</summary>
    public required string Event { get; set; }

    public string? Severity { get; set; }
    public decimal? Impact { get; set; }
    public string? Note { get; set; }
    public string? Data { get; set; }
}

/// <summary>
/// Управленческое решение: что решили, кто, когда и почему, какой показатель должен измениться и насколько к какой дате, исходное значение
/// показателя (снимок среза), фактическое значение после срока и вердикт. Вердикт — сравнение «до / после», а не доказательство причины:
/// без контрольной группы влияние решения не отделить от других факторов, это пишется в оценке.
/// </summary>
public class AnalytikDecision : Entity
{
    public const string Active = "active";
    public const string Evaluated = "evaluated";
    public const string Cancelled = "cancelled";

    public required string Tenant { get; set; }
    public Guid AuthorId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? Reason { get; set; }
    public required string Module { get; set; }
    public DateOnly DecidedOn { get; set; }

    /// <summary>Показатель решения (jsonb): код показателя и фильтры среза (регион, группа…).</summary>
    public required string Metric { get; set; }

    public string? MetricTitle { get; set; }
    public DateOnly BaselineFrom { get; set; }
    public DateOnly BaselineTo { get; set; }
    public decimal? BaselineValue { get; set; }

    /// <summary>Снимок исходных показателей (jsonb): значение, источники, запрос в журнале.</summary>
    public string? Baseline { get; set; }

    /// <summary>up | down | keep.</summary>
    public required string ExpectedDirection { get; set; }

    public decimal? ExpectedChangePct { get; set; }
    public string? ExpectedText { get; set; }
    public DateOnly ReviewOn { get; set; }

    /// <summary>active | evaluated | cancelled.</summary>
    public string Status { get; set; } = Active;

    /// <summary>achieved | partial | missed | inconclusive.</summary>
    public string? Outcome { get; set; }

    public decimal? ActualValue { get; set; }
    public string? Actual { get; set; }
    public DateTimeOffset? EvaluatedAt { get; set; }

    /// <summary>Связи (jsonb): исследования, находки, сценарии.</summary>
    public string? Links { get; set; }

    public string? Notes { get; set; }
    public uint Version { get; set; }
}

/// <summary>
/// Прогон набора сложных вопросов (Benchmark): regression — эталонные планы через движки без модели (бесплатно), live — вопросы моделью
/// (платно, только по разрешению). Итог по категориям и по каждому сценарию.
/// </summary>
public class AnalytikBenchmarkRun : Entity
{
    public required string Mode { get; set; }
    public string? Model { get; set; }

    /// <summary>running | completed | failed.</summary>
    public required string Status { get; set; }

    public int Total { get; set; }
    public int Passed { get; set; }
    public decimal Score { get; set; }
    public string? ByCategory { get; set; }
    public string? Results { get; set; }
    public decimal? CostUsd { get; set; }
    public Guid? StartedById { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? Error { get; set; }
}
