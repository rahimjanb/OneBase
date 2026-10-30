using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.AI.Security;
using OneBase.Application.Abstractions;
using OneBase.Domain.AI;
using OneBase.Domain.Audit;

namespace OneBase.AI.Tools;

/// <summary>
/// Permission Layer между агентом и инструментами:
/// проверяет грант агента, открытый ему источник знаний и права пользователя, от имени которого идёт работа,
/// отправляет критические действия на Human Approval и пишет всё в аудит. Исключение инструмента не выходит наружу: модель получает
/// структурный сбой (ToolResult.Failure) и может попробовать другой инструмент, исключение — в журнал сервера и аудит.
/// </summary>
public sealed class ToolExecutor(IToolRegistry registry, IAppDbContext db, IUserPermissions permissions, IAuditLogger audit, ILogger<ToolExecutor> logger)
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

    /// <summary>
    /// Источники данных (KnowledgeSources), которые каждый из сотрудников может прочитать для пользователя: инструменты с грантом, открытым
    /// источником и правом пользователя. Пустое множество — у сотрудника для этого пользователя нет ни одного инструмента данных.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, IReadOnlySet<string>>> DataSourcesAsync(IReadOnlyCollection<string> agentCodes, Guid userId,
        CancellationToken cancellationToken = default)
    {
        var codes = agentCodes.ToList();
        var grants = await db.AgentToolGrants.AsNoTracking()
            .Where(g => codes.Contains(g.AgentCode) && g.Enabled)
            .Select(g => new { g.AgentCode, g.ToolName })
            .ToListAsync(cancellationToken);
        var open = await db.AiAgentKnowledgeSources.AsNoTracking()
            .Where(s => codes.Contains(s.AgentCode) && s.Enabled)
            .Select(s => new { s.AgentCode, s.SourceCode })
            .ToListAsync(cancellationToken);
        var userPermissions = await permissions.GetAsync(userId, cancellationToken);

        return codes.Distinct().ToDictionary(code => code, IReadOnlySet<string> (code) =>
        {
            var granted = grants.Where(g => g.AgentCode == code).Select(g => g.ToolName).ToHashSet(StringComparer.Ordinal);
            var sources = open.Where(s => s.AgentCode == code).Select(s => s.SourceCode).ToHashSet(StringComparer.Ordinal);
            return registry.All
                .Where(t => t.Source is not null && granted.Contains(t.Name) && Denial(t, sources, userPermissions) is null)
                .Select(t => t.Source!)
                .ToHashSet(StringComparer.Ordinal);
        });
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
            return ToolResult.Failure(toolName, ToolFailures.NotConnected, $"Инструмента «{toolName}» нет в OneBase.");
        }

        var grant = await db.AgentToolGrants
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.AgentCode == context.AgentCode && g.ToolName == toolName && g.Enabled, cancellationToken);

        if (grant is null)
        {
            await audit.LogAsync(ActorType.Agent, context.AgentCode, "ai.tool.denied", "tool", toolName,
                new { context.UserId, reason = "no_grant" }, cancellationToken);
            return ToolResult.Failure(toolName, ToolFailures.NotConnected, $"Инструмент «{toolName}» не подключён этому AI-сотруднику.");
        }

        var sources = await OpenSourcesAsync(context.AgentCode, cancellationToken);
        var userPermissions = context.UserId is { } id ? await permissions.GetAsync(id, cancellationToken) : null;
        if (Denial(tool, sources, userPermissions) is { } reason)
        {
            await audit.LogAsync(ActorType.Agent, context.AgentCode, "ai.tool.denied", "tool", toolName,
                new { context.UserId, reason }, cancellationToken);
            return reason == "no_permission"
                ? ToolResult.Failure(toolName, ToolFailures.Forbidden, "У пользователя нет доступа к этим данным OneBase — не используй и не угадывай их.")
                : ToolResult.Failure(toolName, ToolFailures.NotConnected, $"Источник данных инструмента «{toolName}» не подключён этому AI-сотруднику.");
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

        ToolResult result;
        try
        {
            result = await tool.ExecuteAsync(context, arguments, cancellationToken);
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            // Сбой одного инструмента — не сбой сотрудника: модель узнаёт, что данных этого инструмента нет из-за ошибки, и пробует другой.
            var kind = ToolFailures.KindOf(ex);
            if (kind == ToolFailures.ToolError)
            {
                logger.LogError(ex, "AI: инструмент {Tool} сотрудника {Agent} завершился ошибкой", toolName, context.AgentCode);
            }
            else
            {
                logger.LogWarning("AI: инструмент {Tool} сотрудника {Agent}: {Kind} — {Error}", toolName, context.AgentCode, kind, ex.Message);
            }

            var message = ToolFailures.SafeMessage(ex);
            try
            {
                await audit.LogAsync(ActorType.Agent, context.AgentCode, "ai.tool.failed", "tool", toolName,
                    new { context.UserId, kind, error = ex.GetType().Name, message }, cancellationToken);
            }
            catch (Exception auditError) when (auditError is not OperationCanceledException)
            {
                logger.LogWarning(auditError, "AI: не удалось записать сбой инструмента {Tool} в аудит", toolName);
            }

            return ToolResult.Failure(toolName, kind, message);
        }

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
