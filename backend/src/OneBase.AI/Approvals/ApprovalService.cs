using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.AI.Tools;
using OneBase.Application.Abstractions;
using OneBase.Domain.AI;
using OneBase.Domain.Audit;

namespace OneBase.AI.Approvals;

/// <summary>Human Approval: человек одобряет или отклоняет критическое действие агента.</summary>
public sealed class ApprovalService(IAppDbContext db, IToolRegistry registry, IAuditLogger audit)
{
    public async Task<ApprovalRequest> ApproveAsync(Guid id, Guid userId, string? comment, CancellationToken cancellationToken = default)
    {
        var request = await DecideAsync(id, userId, comment, ApprovalStatus.Approved, cancellationToken);

        var tool = registry.Find(request.ToolName);
        ToolResult result;
        if (tool is null)
        {
            result = ToolResult.Error($"Инструмент '{request.ToolName}' больше не зарегистрирован.");
        }
        else
        {
            try
            {
                using var arguments = JsonDocument.Parse(request.Arguments);
                result = await tool.ExecuteAsync(new ToolContext(request.AgentCode, userId, 0), arguments.RootElement, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                result = ToolResult.Error(ex.Message);
            }
        }

        request.Status = result.Success ? ApprovalStatus.Executed : ApprovalStatus.Failed;
        request.ExecutionResult = result.Content;
        await db.SaveChangesAsync(cancellationToken);

        await audit.LogAsync(ActorType.User, userId.ToString(), "ai.approval.executed", nameof(ApprovalRequest),
            id.ToString(), new { request.ToolName, result.Success }, cancellationToken);
        return request;
    }

    public Task<ApprovalRequest> RejectAsync(Guid id, Guid userId, string? comment, CancellationToken cancellationToken = default) =>
        DecideAsync(id, userId, comment, ApprovalStatus.Rejected, cancellationToken);

    private async Task<ApprovalRequest> DecideAsync(
        Guid id, Guid userId, string? comment, ApprovalStatus decision, CancellationToken cancellationToken)
    {
        var request = await db.ApprovalRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Запрос {id} не найден.");

        if (request.Status != ApprovalStatus.Pending)
        {
            throw new InvalidOperationException($"Запрос {id} уже обработан ({request.Status}).");
        }

        request.Status = decision;
        request.DecidedById = userId;
        request.DecidedAt = DateTimeOffset.UtcNow;
        request.DecisionComment = comment;
        await db.SaveChangesAsync(cancellationToken); // DbUpdateConcurrencyException при параллельном решении

        await audit.LogAsync(ActorType.User, userId.ToString(), $"ai.approval.{decision.ToString().ToLowerInvariant()}",
            nameof(ApprovalRequest), id.ToString(), new { comment }, cancellationToken);
        return request;
    }
}
