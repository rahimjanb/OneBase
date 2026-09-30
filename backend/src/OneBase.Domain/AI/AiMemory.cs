using OneBase.Domain.Common;

namespace OneBase.Domain.AI;

public enum AiMemoryKind
{
    /// <summary>Результат AI-сотрудника в чате: чтобы уточняющий вопрос («а сравни с августом») понимал, о чём речь.</summary>
    Analytical,

    /// <summary>Устойчивый вывод AI-сотрудника для пользователя — контекст следующих анализов.</summary>
    Agent,
}

/// <summary>
/// Память AI. Принадлежит пользователю: другим пользователям не показывается, а сотрудники, к которым у пользователя
/// больше нет доступа, из неё не читаются.
/// </summary>
public class AiMemory : Entity
{
    public AiMemoryKind Kind { get; set; }
    public Guid UserId { get; set; }
    public string? AgentCode { get; set; }
    public Guid? ConversationId { get; set; }

    /// <summary>Что анализировалось — задача сотруднику.</summary>
    public required string Topic { get; set; }

    /// <summary>Краткий вывод и ключевые факты.</summary>
    public required string Content { get; set; }

    /// <summary>Полный структурированный результат (jsonb).</summary>
    public string? Data { get; set; }
}
