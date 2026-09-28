using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneBase.AI.Approvals;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Security;
using OneBase.Domain.AI;

namespace OneBase.Api.Controllers;

[ApiController]
[Route("api/approvals")]
[HasPermission(Permissions.ApprovalsDecide)]
public sealed class ApprovalsController(IAppDbContext db, ApprovalService approvals) : ControllerBase
{
    public sealed record DecisionRequest(string? Comment);

    [HttpGet]
    public async Task<IReadOnlyList<ApprovalRequest>> List([FromQuery] ApprovalStatus status = ApprovalStatus.Pending, CancellationToken ct = default) =>
        await db.ApprovalRequests.AsNoTracking()
            .Where(r => r.Status == status)
            .OrderByDescending(r => r.CreatedAt)
            .Take(200)
            .ToListAsync(ct);

    [HttpPost("{id:guid}/approve")]
    public Task<IActionResult> Approve(Guid id, DecisionRequest request, CancellationToken ct) =>
        Decide(() => approvals.ApproveAsync(id, User.GetUserId(), request.Comment, ct));

    [HttpPost("{id:guid}/reject")]
    public Task<IActionResult> Reject(Guid id, DecisionRequest request, CancellationToken ct) =>
        Decide(() => approvals.RejectAsync(id, User.GetUserId(), request.Comment, ct));

    private async Task<IActionResult> Decide(Func<Task<ApprovalRequest>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex) when (ex is InvalidOperationException or DbUpdateConcurrencyException)
        {
            return Conflict(new { error = ex.Message });
        }
    }
}
