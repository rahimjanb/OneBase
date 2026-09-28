using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Security;
using OneBase.Domain.Audit;

namespace OneBase.Api.Controllers;

[ApiController]
[Route("api/audit")]
[HasPermission(Permissions.AuditRead)]
public sealed class AuditController(IAppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<AuditLog>> List([FromQuery] int take = 100, CancellationToken ct = default) =>
        await db.AuditLogs.AsNoTracking()
            .OrderByDescending(a => a.Id)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(ct);
}
