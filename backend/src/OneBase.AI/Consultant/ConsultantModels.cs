using System.Text.Json.Serialization;
using OneBase.AI.Gateway;
using OneBase.AI.Llm;
using OneBase.AI.Providers;
using OneBase.AI.Reports;

namespace OneBase.AI.Consultant;

/// <summary>Вопрос пользователя в контексте чата. Channel — откуда вопрос (null — чат OneBase).</summary>
public sealed record ConsultantTurn(Guid UserId, Guid ConversationId, string Question, IReadOnlyList<LlmMessage> History, ConsultantChannel? Channel = null);

/// <summary>
/// Канал вопроса вне чата OneBase (Telegram): свои инструкции (стиль, язык, ограничения администратора), модель и длина ответа, можно ли
/// формировать отчёты. Права на данные — по-прежнему права пользователя: канал их не расширяет.
/// </summary>
public sealed record ConsultantChannel(string Name, string? Instructions, AiModelRef? Model, int? MaxOutputTokens, bool AllowReports);

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

/// <summary>Структурированный результат AI-сотрудника (agent/status/findings/metrics/problems/recommendations/data_sources).</summary>
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
    string? Error = null);

/// <summary>Разбор ответа, который сохраняется рядом с текстом и показывается в чате. Reports — отчёты Excel, сформированные по просьбе пользователя.</summary>
public sealed record ConsultantDetails(
    IReadOnlyList<AgentResult> Agents,
    IReadOnlyList<DataSource> Sources,
    string? Model,
    bool UsedFallback,
    string? RoutingReason = null,
    int MemoryUsed = 0,
    IReadOnlyList<ReportRef>? Reports = null);

public sealed record ConsultantAnswer(string Content, ConsultantDetails Details, AiTokenUsage Usage);

/// <summary>Движок консультанта: получает вопрос с историей и возвращает единый ответ, сообщая о ходе работы.</summary>
public interface IConsultantEngine
{
    Task<ConsultantAnswer> AnswerAsync(ConsultantTurn turn, Func<ConsultantProgress, Task> progress, CancellationToken cancellationToken);
}
