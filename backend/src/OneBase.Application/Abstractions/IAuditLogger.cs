using OneBase.Domain.Audit;

namespace OneBase.Application.Abstractions;

public interface IAuditLogger
{
    Task LogAsync(
        ActorType actorType,
        string actorId,
        string action,
        string? entityType = null,
        string? entityId = null,
        object? data = null,
        CancellationToken cancellationToken = default);
}
