using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.AI.Security;
using OneBase.Application.Abstractions;
using OneBase.Domain.AI;
using OneBase.Domain.Audit;

namespace OneBase.AI.Tools;

/// <summary>
/// Permission Layer между агентом и инструментами:
/// проверяет грант агента, открытый ему источник знаний и права пользователя, от имени которого идёт работа,
/// отправляет критические действия на Human Approval и пишет всё в аудит.
/// </summary>
public sealed class ToolExecutor(IToolRegistry registry, IAppDbContext db, IUserPermissions permissions, IAuditLogger audit)
{
    /// <summary>Инструменты, которые агент может вызвать для этого пользователя.</summary>
    public async Task<IReadOnlyList<ITool>> GetAllowedToolsAsync(string agentCode, Guid? userId, CancellationToken cancellationToken = default)
    {
        var granted = await db.AgentToolGrants.AsNoTracking()
            .Where(g => g.AgentCode == agentCode && g.Enabled)
            .Select(g => g.ToolName)
            .ToListAsync(cancellationToken);
        var sources = await OpenSourcesAsync(agentCode, cancellationToken);
        var userPermissions = userId is { } id ? await permissions.GetAsync(id, cancellationToken) : null;

        return registry.All
            .Where(t => granted.Contains(t.Name) && Denial(t, sources, userPermissions) is null)
            .ToList();
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
            .FirstOrDefaultAsync(g => g.AgentCode == context.AgentCode && g.ToolName == toolName && g.Enabled, cancellationToken);

        if (grant is null)
        {
            await audit.LogAsync(ActorType.Agent, context.AgentCode, "ai.tool.denied", "tool", toolName,
                new { context.UserId, reason = "no_grant" }, cancellationToken);
            return ToolResult.Error($"У агента '{context.AgentCode}' нет доступа к инструменту '{toolName}'.");
        }

        var sources = await OpenSourcesAsync(context.AgentCode, cancellationToken);
        var userPermissions = context.UserId is { } id ? await permissions.GetAsync(id, cancellationToken) : null;
        if (Denial(tool, sources, userPermissions) is { } reason)
        {
            await audit.LogAsync(ActorType.Agent, context.AgentCode, "ai.tool.denied", "tool", toolName,
                new { context.UserId, reason }, cancellationToken);
            return ToolResult.Error(reason == "no_permission"
                ? "У пользователя нет доступа к этим данным OneBase — не используй и не угадывай их."
                : $"Источник данных инструмента '{toolName}' закрыт для агента '{context.AgentCode}'.");
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

    /// <summary>Почему инструмент недоступен: source_closed | no_permission; null — доступен.</summary>
    private static string? Denial(ITool tool, IReadOnlySet<string> openSources, IReadOnlySet<string>? userPermissions)
    {
        if (tool.Source is { } source && !openSources.Contains(source))
        {
            return "source_closed";
        }

        // Данные пользователя читаются только от имени пользователя с нужным правом.
        if (tool.RequiredPermission is { } permission
            && (userPermissions is null || (!userPermissions.Contains(permission) && !(tool.AlternativePermissions ?? []).Any(userPermissions.Contains))))
        {
            return "no_permission";
        }

        return null;
    }

    private async Task<IReadOnlySet<string>> OpenSourcesAsync(string agentCode, CancellationToken ct) =>
        (await db.AiAgentKnowledgeSources.AsNoTracking()
            .Where(s => s.AgentCode == agentCode && s.Enabled)
            .Select(s => s.SourceCode)
            .ToListAsync(ct))
        .ToHashSet(StringComparer.Ordinal);
}
