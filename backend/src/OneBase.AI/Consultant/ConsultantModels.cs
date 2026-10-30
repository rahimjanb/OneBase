using System.Text.Json.Serialization;
using OneBase.AI.Gateway;
using OneBase.AI.Grounding;
using OneBase.AI.Llm;
using OneBase.AI.Providers;
using OneBase.AI.Reports;

namespace OneBase.AI.Consultant;

/// <summary>
/// Вопрос пользователя в контексте чата. Channel — откуда вопрос (null — чат OneBase). LastAnswerId — последний ответ консультанта в чате
/// (если вопрос его оспаривает — на него ссылается проверка в журнале).
/// </summary>
public sealed record ConsultantTurn(Guid UserId, Guid ConversationId, string Question, IReadOnlyList<LlmMessage> History, ConsultantChannel? Channel = null,
    Guid? LastAnswerId = null);

/// <summary>
/// Канал вопроса вне чата OneBase (Telegram): свои инструкции (стиль, язык, ограничения администратора), модель и длина ответа, можно ли
/// формировать отчёты. AgentModel — модель для AI-сотрудников этого вопроса вместо их собственных (Claude, когда просят сформировать отчёт);
/// null — свои модели сотрудников. Права на данные — по-прежнему права пользователя: канал их не расширяет.
/// </summary>
public sealed record ConsultantChannel(string Name, string? Instructions, AiModelRef? Model, int? MaxOutputTokens, bool AllowReports, AiModelRef? AgentModel = null);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProgressStatus
{
    Pending,
    Running,
    Done,
    Failed,
    Skipped,
}

/// <summary>Шаг работы консультанта для индикатора в чате: маршрутизация, работа AI-сотрудника, итоговый ответ.</summary>
public sealed record ConsultantProgress(string Stage, string? Agent, string? AgentName, ProgressStatus Status, string? Note = null);

/// <summary>Источник данных, на который опирается ответ. Href — страница OneBase, где его можно открыть.</summary>
public sealed record DataSource(string Title, string? Period = null, string? Href = null);

/// <summary>Показатель из данных: название, значение и единица.</summary>
public sealed record Metric(string Name, string Value, string? Unit = null);

/// <summary>Сбой инструмента сотрудника: инструмент, вид (forbidden — нет доступа, not_connected — источник не подключён, not_found — не найдено,
/// tool_error — ошибка инструмента) и безопасный для пользователя текст.</summary>
public sealed record ToolIssue(string Tool, string Kind, string Message);

/// <summary>
/// Структурированный результат AI-сотрудника (agent/status/findings/metrics/problems/recommendations/data_sources). Unverified — числа итога,
/// которых нет в результатах инструментов и точных расчётов (после попытки исправить): консультант не выдаёт их за факт.
/// Reason — почему данных нет или они неполные: no_access, source_not_connected, tool_error, not_found, interrupted, model_unavailable,
/// internal_error; ToolErrors — инструменты, которые не дали данных. Статус «нет данных» (no_data) — только когда данные действительно не найдены.
/// </summary>
public sealed record AgentResult(
    string Agent,
    string AgentName,
    string Status,
    string Summary,
    IReadOnlyList<string> Findings,
    IReadOnlyList<Metric> Metrics,
    IReadOnlyList<string> Problems,
    IReadOnlyList<string> Recommendations,
    IReadOnlyList<DataSource> DataSources,
    IReadOnlyList<string> ToolsUsed,
    string? Error = null,
    IReadOnlyList<string>? Unverified = null,
    string? Reason = null,
    IReadOnlyList<ToolIssue>? ToolErrors = null);

/// <summary>
/// Разбор ответа, который сохраняется рядом с текстом и показывается в чате. Reports — отчёты Excel, сформированные по просьбе пользователя.
/// Grounding — проверка чисел ответа по данным; Correction — перепроверка, если вопрос оспаривал прошлый ответ.
/// </summary>
public sealed record ConsultantDetails(
    IReadOnlyList<AgentResult> Agents,
    IReadOnlyList<DataSource> Sources,
    string? Model,
    bool UsedFallback,
    string? RoutingReason = null,
    int MemoryUsed = 0,
    IReadOnlyList<ReportRef>? Reports = null,
    GroundingSummary? Grounding = null,
    CorrectionReview? Correction = null);

public sealed record ConsultantAnswer(string Content, ConsultantDetails Details, AiTokenUsage Usage);

/// <summary>Движок консультанта: получает вопрос с историей и возвращает единый ответ, сообщая о ходе работы.</summary>
public interface IConsultantEngine
{
    Task<ConsultantAnswer> AnswerAsync(ConsultantTurn turn, Func<ConsultantProgress, Task> progress, CancellationToken cancellationToken);
}
