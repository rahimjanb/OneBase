using System.Text.Json;
using OneBase.Application.Abstractions;
using OneBase.Domain.Audit;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.Audit;

internal sealed class AuditLogger(OneBaseDbContext db) : IAuditLogger
{
    public async Task LogAsync(
        ActorType actorType,
        string actorId,
        string action,
        string? entityType = null,
        string? entityId = null,
        object? data = null,
        CancellationToken cancellationToken = default)
    {
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = actorType,
            ActorId = actorId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Data = data is null ? null : JsonSerializer.Serialize(data),
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
