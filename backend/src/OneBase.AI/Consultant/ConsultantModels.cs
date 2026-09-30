using System.Text.Json.Serialization;
using OneBase.AI.Llm;
using OneBase.AI.Providers;

namespace OneBase.AI.Consultant;

/// <summary>Вопрос пользователя в контексте чата.</summary>
public sealed record ConsultantTurn(Guid UserId, Guid ConversationId, string Question, IReadOnlyList<LlmMessage> History);

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

/// <summary>Разбор ответа, который сохраняется рядом с текстом и показывается в чате.</summary>
public sealed record ConsultantDetails(
    IReadOnlyList<AgentResult> Agents,
    IReadOnlyList<DataSource> Sources,
    string? Model,
    bool UsedFallback,
    string? RoutingReason = null,
    int MemoryUsed = 0);

public sealed record ConsultantAnswer(string Content, ConsultantDetails Details, AiTokenUsage Usage);

/// <summary>Движок консультанта: получает вопрос с историей и возвращает единый ответ, сообщая о ходе работы.</summary>
public interface IConsultantEngine
{
    Task<ConsultantAnswer> AnswerAsync(ConsultantTurn turn, Func<ConsultantProgress, Task> progress, CancellationToken cancellationToken);
}
