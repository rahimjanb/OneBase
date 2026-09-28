using OneBase.Domain.Common;

namespace OneBase.Domain.AI;

public enum ApprovalStatus
{
    Pending,
    Approved,
    Rejected,
    Executed,
    Failed,
}

/// <summary>Запрос агента на выполнение критического действия, ожидающий решения человека.</summary>
public class ApprovalRequest : Entity
{
    public required string AgentCode { get; set; }
    public required string ToolName { get; set; }

    /// <summary>Аргументы вызова инструмента в JSON (jsonb).</summary>
    public required string Arguments { get; set; }
    public string? Reason { get; set; }
    public Guid? RequestedByUserId { get; set; }

    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public Guid? DecidedById { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionComment { get; set; }
    public string? ExecutionResult { get; set; }

    /// <summary>Токен конкурентности (xmin в PostgreSQL) — защищает от двойного одобрения.</summary>
    public uint Version { get; set; }
}
