namespace OneBase.AI.Memory;

/// <summary>
/// Виды памяти AI-сотрудников и где они живут:
/// Working → Redis (TTL), Episodic / Semantic / CompanyKnowledge → Qdrant (+ текст в PostgreSQL),
/// OperationalState → PostgreSQL/Redis, AuditHistory → PostgreSQL (AuditLogs).
/// </summary>
public enum MemoryKind
{
    Working,
    Episodic,
    Semantic,
    CompanyKnowledge,
    OperationalState,
    AuditHistory,
}

public sealed record MemoryItem(
    Guid Id,
    MemoryKind Kind,
    string AgentCode,
    string Content,
    DateTimeOffset CreatedAt,
    IReadOnlyDictionary<string, string>? Metadata = null);

public interface IAgentMemory
{
    Task RememberAsync(MemoryItem item, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemoryItem>> RecallAsync(
        string agentCode,
        MemoryKind kind,
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default);
}
