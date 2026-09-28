using OneBase.Domain.Common;

namespace OneBase.Domain.AI;

/// <summary>Разрешение агенту вызывать инструмент из Tool Registry.</summary>
public class AgentToolGrant : Entity
{
    public required string AgentCode { get; set; }
    public required string ToolName { get; set; }

    /// <summary>Если true — каждый вызов проходит через Human Approval, даже если сам инструмент не критический.</summary>
    public bool RequiresApproval { get; set; }
}
