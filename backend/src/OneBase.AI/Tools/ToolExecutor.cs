using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Domain.AI;
using OneBase.Domain.Audit;

namespace OneBase.AI.Tools;

/// <summary>
/// Permission Layer между агентом и инструментами:
/// проверяет грант агента, отправляет критические действия на Human Approval и пишет всё в аудит.
/// </summary>
public sealed class ToolExecutor(IToolRegistry registry, IAppDbContext db, IAuditLogger audit)
{
    public async Task<IReadOnlyList<ITool>> GetAllowedToolsAsync(string agentCode, CancellationToken cancellationToken = default)
    {
        var granted = await db.AgentToolGrants
            .Where(g => g.AgentCode == agentCode)
            .Select(g => g.ToolName)
            .ToListAsync(cancellationToken);

        return registry.All.Where(t => granted.Contains(t.Name)).ToList();
    }

    public async Task<ToolResult> ExecuteAsync(
        ToolContext context,
        string toolName,
        JsonElement arguments,
        CancellationToken cancellationToken = default)
    {
        var tool = registry.Find(toolName);
        if (tool is null)
        {
            return ToolResult.Error($"Инструмент '{toolName}' не найден.");
        }

        var grant = await db.AgentToolGrants
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.AgentCode == context.AgentCode && g.ToolName == toolName, cancellationToken);

        if (grant is null)
        {
            await audit.LogAsync(ActorType.Agent, context.AgentCode, "ai.tool.denied", "tool", toolName,
                new { context.UserId }, cancellationToken);
            return ToolResult.Error($"У агента '{context.AgentCode}' нет доступа к инструменту '{toolName}'.");
        }

        if (tool.IsCritical || grant.RequiresApproval)
        {
            var request = new ApprovalRequest
            {
                AgentCode = context.AgentCode,
                ToolName = toolName,
                Arguments = arguments.GetRawText(),
                RequestedByUserId = context.UserId,
            };
            db.ApprovalRequests.Add(request);
            await db.SaveChangesAsync(cancellationToken);

            await audit.LogAsync(ActorType.Agent, context.AgentCode, "ai.approval.requested", nameof(ApprovalRequest),
                request.Id.ToString(), new { toolName, context.UserId }, cancellationToken);
            return ToolResult.PendingApproval(request.Id);
        }

        var result = await tool.ExecuteAsync(context, arguments, cancellationToken);

        await audit.LogAsync(ActorType.Agent, context.AgentCode, "ai.tool.executed", "tool", toolName,
            new { context.UserId, result.Success }, cancellationToken);
        return result;
    }
}
